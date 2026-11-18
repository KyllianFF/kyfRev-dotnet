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
        // aussitôt, sans rien ouvrir ; la mise à jour finie, il s'ouvre normalement.
        if (SilentInstaller.IsInstalling())
        {
            Shutdown();
            return;
        }
        _services = Composition.Build(Time, Locations);
        _services.GetRequiredService<OpenMarker>();
        WatchUnexpectedErrors(_services.GetRequiredService<ILoggerFactory>().CreateLogger("kyfRev"));
        Resources.MergedDictionaries.Add(_services.GetRequiredService<AppearanceController>().Resources);
        Resources[MermaidEngine.ResourceKey] = _services.GetRequiredService<MermaidEngine>();
        Resources[NetworkBlock.ResourceKey] = _services.GetRequiredService<INetworkSchemas>();
        Resources[ExtensionBlocks.ResourceKey] = _services.GetRequiredService<ExtensionBlocks>();
        Resources[ExtensionPages.ResourceKey] = _services.GetRequiredService<ExtensionPages>();
        var shell = _services.GetRequiredService<ShellViewModel>();
        var gallery = e.Args.Contains("--galerie", StringComparer.Ordinal);
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
        window.Show();
        // Par-dessus la vue qui s'ouvre : longue la toute première fois, sur l'accueil ; ensuite moins d'une seconde. La
        // galerie des composants, elle, s'ouvre telle quelle.
        if (PlaysOpening && !gallery)
        {
            window.PlayOpening();
        }
        FollowCaptureKey(window, shell, _services.GetRequiredService<MachineSettings>());
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
            window.Close();
            return closed;
        };
        _ = _updates.StartAsync(_services.GetRequiredService<MachineSettings>().Current.CheckUpdatesAtStartup);
    }

    /// <summary>Windows ferme la session : une mise à jour prévue attendra la prochaine fermeture de kyfRev.</summary>
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _sessionEnding = true;
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
    }

    /// <summary>
    /// Le raccourci global de la capture rapide : demandé à Windows seulement si le réglage est actif, maintenant et à
    /// chaque changement. Tapé, il ramène la fenêtre devant et ouvre la capture ; sans coffre ouvert, seule la fenêtre
    /// revient.
    /// </summary>
    private static void FollowCaptureKey(Window window, ShellViewModel shell, MachineSettings machine)
    {
        var captureKey = new GlobalHotKey(window, () =>
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }
            window.Activate();
            shell.OpenCapture();
        });
        void Apply() => captureKey.Set(machine.Current.GlobalCapture);
        Apply();
        machine.Changed += (_, _) => Apply();
        // Rendu à Windows avec la fenêtre : il ne lui survit pas.
        window.Closed += (_, _) => captureKey.Release();
    }

    /// <inheritdoc/>
    protected override void OnExit(ExitEventArgs e)
    {
        var update = _sessionEnding ? null : _updates?.ExitLaunch;
        // Les services rendus, kyfRev ne tient plus le verrou qui arrête l'installateur.
        _services?.Dispose();
        update?.Invoke();
        base.OnExit(e);
    }
}
