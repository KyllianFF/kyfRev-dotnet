using System.Windows;
using KyfRev.Application.UseCases;
using KyfRev.Infrastructure.Windows;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyfRev.App;

/// <summary>
/// L'application : se signale ouverte à l'installateur, assemble les services, applique
/// l'apparence de Windows, ouvre le coffre prévu
/// (l'accueil à défaut), puis la fenêtre principale. Avec <c>--galerie</c>, la fenêtre s'ouvre sur
/// la galerie des composants, sans ouvrir de coffre. Une mise à jour prête s'installe une fois l'application
/// fermée ; pendant qu'elle s'installe, l'application lancée se referme aussitôt.
/// </summary>
internal sealed partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private UpdateFlow? _updates;
    private bool _sessionEnding;
    private TrayIcon? _tray;
    private SingleInstance? _instance;

    /// <summary>L'horloge de l'application ; les scénarios d'acceptation y mettent une date simulée.</summary>
    internal TimeProvider Time { get; init; } = TimeProvider.System;

    /// <summary>Les emplacements de l'application ; les scénarios d'acceptation y mettent un dossier à eux.</summary>
    internal VaultLocations? Locations { get; init; }

    /// <summary>
    /// L'animation d'ouverture se joue quand la fenêtre paraît. Les scénarios d'acceptation la coupent : ils agissent dès
    /// que la fenêtre existe, sans attendre qu'elle ait laissé la place.
    /// </summary>
    internal bool PlaysOpening { get; init; } = true;

    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Une mise à jour s'installe : ouvert maintenant, kyfRev retiendrait des fichiers qu'elle remplace. Il se referme
        // aussitôt, sans rien ouvrir ; la mise à jour finie, il s'ouvre normalement. L'installateur qui l'ouvre lui-même à
        // la fin tient encore son mutex : il le dit par une option, ses fichiers étant déjà en place.
        if (SilentInstaller.StepsAside(e.Args))
        {
            Shutdown();
            return;
        }
        var gallery = e.Args.Contains("--galerie", StringComparer.Ordinal);
        if (!gallery && Locations is null && Environment.ProcessPath is { } executable)
        {
            // Déjà ouvert, même réduit dans la barre d'état : c'est lui qui se montre, et ce lancement s'arrête là. Lancé
            // avec la session alors qu'il tourne, il n'a rien à montrer. La galerie et les scénarios d'acceptation, eux,
            // s'ouvrent toujours.
            _instance = SingleInstance.Claim(executable);
            if (!_instance.IsFirst)
            {
                if (!e.Args.Contains(SessionStartup.InTrayArgument, StringComparer.Ordinal))
                {
                    _instance.WakeFirst();
                }
                _instance.Dispose();
                _instance = null;
                Shutdown();
                return;
            }
        }
        _services = Composition.Build(Time, Locations);
        _services.GetRequiredService<OpenMarker>();
        WatchUnexpectedErrors(_services.GetRequiredService<ILoggerFactory>().CreateLogger("kyfRev"));
        Resources.MergedDictionaries.Add(_services.GetRequiredService<AppearanceController>().Resources);
        Resources[MermaidEngine.ResourceKey] = _services.GetRequiredService<MermaidEngine>();
        Resources[EditorEnvironment.WebDataFolderKey] = _services.GetRequiredService<EditorEnvironment>().WebDataFolder;
        Resources[NetworkBlock.ResourceKey] = _services.GetRequiredService<INetworkSchemas>();
        Resources[ExtensionBlocks.ResourceKey] = _services.GetRequiredService<ExtensionBlocks>();
        Resources[ExtensionPages.ResourceKey] = _services.GetRequiredService<ExtensionPages>();
        var shell = _services.GetRequiredService<ShellViewModel>();
        if (gallery)
        {
            // Revue visuelle : la galerie des composants d'emblée.
            shell.Navigate(shell.Gallery);
        }
        else
        {
            VaultStartup.Run(shell, _services.GetRequiredService<VaultUseCases>(), _services.GetRequiredService<IFolderPicker>());
        }
        var window = _services.GetRequiredService<ShellWindow>();
        MainWindow = window;
        var machine = _services.GetRequiredService<MachineSettings>();
        // Lancé par Windows avec la session, réglé pour rester discret : kyfRev reste dans la barre d'état.
        var inTray = !gallery && e.Args.Contains(SessionStartup.InTrayArgument, StringComparer.Ordinal);
        // Posée avant que la fenêtre ne paraisse, elle en est la première image : longue la toute première fois, sur
        // l'accueil, plus courte ensuite. La galerie des composants, elle, s'ouvre telle quelle.
        if (PlaysOpening && !gallery && !inTray)
        {
            window.PlayOpening();
        }
        var palette = PaletteFor(window, shell);
        _tray = TrayFor(window, shell, palette, machine);
        if (inTray)
        {
            // Sa fenêtre existe, cachée : le raccourci global a besoin d'elle.
            new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
            _tray.Visible = true;
        }
        else
        {
            window.Show();
        }
        FollowCaptureKey(window, palette, machine);
        // Relancé pendant qu'il tourne : la fenêtre revient, même cachée dans la barre d'état.
        _instance?.Listen(() => Dispatcher.BeginInvoke(() => Bring(window)));
        FollowSessionStartup(machine);
        // Le coffre chiffré se verrouille de lui-même après un temps d'inactivité, ou quand Windows se verrouille.
        _services.GetRequiredService<AutoLock>().Start();
        // La mise à jour dit ce qu'est devenue la dernière, puis en cherche une si on l'a choisi ; « Redémarrer
        // maintenant » ferme la fenêtre, et l'installation part de OnExit. La galerie, elle, ne cherche rien.
        if (gallery)
        {
            return;
        }
        _updates = _services.GetRequiredService<UpdateFlow>();
        var closed = false;
        window.Closed += (_, _) => closed = true;
        _updates.CloseApplication = () =>
        {
            window.Quit();
            return closed;
        };
        _ = _updates.StartAsync(_services.GetRequiredService<MachineSettings>().Current.CheckUpdatesAtStartup);
    }

    /// <summary>Windows ferme la session : une mise à jour prévue attendra la prochaine fermeture de kyfRev.</summary>
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _sessionEnding = true;
        // La session se ferme : la croix ne cache plus kyfRev, elle le ferme.
        (MainWindow as ShellWindow)?.LetClose();
        base.OnSessionEnding(e);
    }

    /// <summary>
    /// Un arrêt inattendu est consigné dans le journal avant le message habituel de Windows, comme
    /// dans la 0.x ; rien n'est masqué ni rattrapé.
    /// </summary>
    private void WatchUnexpectedErrors(ILogger log)
    {
        DispatcherUnhandledException += (_, args) => DiagnosticLog.UnexpectedStop(log, args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => DiagnosticLog.UnexpectedStop(log, args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => DiagnosticLog.UnobservedTask(log, args.Exception);
        // Une animation relancée en boucle est coupée par le design system ; le journal dit laquelle.
        KyfRev.DesignSystem.Controls.Transitions.LoopCut += (_, cut) => DiagnosticLog.AnimationLoopCut(log, cut.Element);
    }

    /// <summary>
    /// Le raccourci global de la capture rapide : demandé à Windows seulement si le réglage est actif, maintenant et à
    /// chaque changement. Tapé, il ramène la fenêtre devant et ouvre la capture ; sans coffre ouvert, seule la fenêtre
    /// revient.
    /// </summary>
    private static void FollowCaptureKey(Window window, CapturePaletteWindow palette, MachineSettings machine)
    {
        // La palette paraît seule, au-dessus de tout : la fenêtre de kyfRev reste où elle est, même cachée (rangée 25B).
        var captureKey = new GlobalHotKey(window, () => palette.ShowNear(window));
        void Apply() => captureKey.Set(machine.Current.GlobalCapture);
        Apply();
        machine.Changed += (_, _) => Apply();
        // Rendu à Windows avec la fenêtre : il ne lui survit pas.
        window.Closed += (_, _) => captureKey.Release();
    }

    /// <summary>
    /// La palette de capture (rangée 25B) : Ctrl+Maj+N dans kyfRev et Ctrl+Alt+N partout l'ouvrent. Elle note dans la Boîte
    /// de réception, crée une fiche ou une note à la racine, ou ramène kyfRev pour chercher, réviser, ouvrir.
    /// </summary>
    private CapturePaletteWindow PaletteFor(ShellWindow window, ShellViewModel shell)
    {
        var services = _services!;
        var sheets = services.GetRequiredService<SheetUseCases>();
        var reminders = services.GetRequiredService<ReminderUseCases>();
        var model = new CapturePaletteViewModel(
            shell,
            (title, note) => note ? sheets.CreateNote(string.Empty, title, null) : sheets.Create(string.Empty, title, null),
            () => reminders.Dashboard() is { Succeeded: true } dashboard ? dashboard.Value.Overdue.Length + dashboard.Value.Today.Length : null)
        {
            ShowApplication = () => Bring(window),
        };
        var palette = new CapturePaletteWindow(model);
        shell.ShowCapturePalette = () => palette.ShowNear(window);
        window.Closed += (_, _) => palette.Close();
        return palette;
    }

    /// <summary>
    /// L'icône de la barre d'état (rangée 25D) : montrée quand la croix cache kyfRev, quand il se lance avec la session, et
    /// tant que sa fenêtre est cachée. La première fois que la croix le cache, une bulle le dit.
    /// </summary>
    private static TrayIcon TrayFor(ShellWindow window, ShellViewModel shell, CapturePaletteWindow palette, MachineSettings machine)
    {
        var actions = new TrayActions(
            Open: () => Bring(window),
            Capture: () => palette.ShowNear(window),
            Revise: () =>
            {
                Bring(window);
                if (shell.MainNavigation.FirstOrDefault(item => item.Id == CorePages.Today) is { } today)
                {
                    shell.Navigate(today);
                }
            },
            Settings: () =>
            {
                Bring(window);
                if (shell.MainNavigation.Concat(shell.SecondaryNavigation).FirstOrDefault(item => item.Id == CorePages.Settings) is { } settings)
                {
                    shell.Navigate(settings);
                }
            },
            Quit: window.Quit);
        var tray = new TrayIcon(() => TrayMenu.Items(actions, machine.Current.GlobalCapture ? GlobalHotKey.Keys : null), () => Bring(window));
        void Show() => tray.Visible = machine.Current.CloseToTray || machine.Current.StartWithSession || !window.IsVisible;
        Show();
        machine.Changed += (_, _) => Show();
        window.IsVisibleChanged += (_, _) => Show();
        window.ClosesToTray = () => machine.Current.CloseToTray;
        window.HiddenToTray += (_, _) =>
        {
            Show();
            if (!machine.Current.TrayTipShown)
            {
                tray.Tell(TrayText.TipTitle, TrayText.Tip);
                machine.Update(preferences => preferences with { TrayTipShown = true });
            }
        };
        return tray;
    }

    /// <summary>Ramène la fenêtre de kyfRev, au premier plan.</summary>
    private static void Bring(Window window)
    {
        if (!window.IsVisible)
        {
            window.Show();
        }
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }
        window.Activate();
        // Rappelé d'ailleurs (un second lancement, l'icône de la barre d'état), il passe devant les autres applications.
        ForegroundWindow.Bring(new System.Windows.Interop.WindowInteropHelper(window).Handle);
    }

    /// <summary>Le lancement avec la session suit le réglage, maintenant et à chaque changement.</summary>
    private static void FollowSessionStartup(MachineSettings machine)
    {
        var startup = new SessionStartup();
        void Apply() => startup.Apply(machine.Current.StartWithSession, machine.Current.StartInTray);
        Apply();
        machine.Changed += (_, _) => Apply();
    }

    /// <inheritdoc/>
    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _instance?.Dispose();
        var update = _sessionEnding ? null : _updates?.ExitLaunch;
        // Les services rendus, kyfRev ne tient plus le verrou qui arrête l'installateur.
        _services?.Dispose();
        update?.Invoke();
        base.OnExit(e);
    }
}
