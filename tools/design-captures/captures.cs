#:project ../../src/KyfRev.DesignSystem/KyfRev.DesignSystem.csproj
#:project ../../src/KyfRev.Presentation.Wpf/KyfRev.Presentation.Wpf.csproj
#:project ../../src/KyfRev.Infrastructure.Vault/KyfRev.Infrastructure.Vault.csproj
#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property ImplicitUsings=enable
#:property PublishAot=false

// Captures du design system pour la revue visuelle (voir tools/design-captures/README.md).
// Usage : dotnet run tools/design-captures/captures.cs -- <dossier>
// Écrit galerie-clair.png, galerie-sombre.png et galerie-contraste-eleve.png (couleurs système
// actuelles du poste), les vues de la coquille (coquille-clair.png, coquille-sombre.png,
// recherche-sombre.png, aide-clair.png), le menu selon la largeur de la fenêtre
// (coquille-etroite-clair.png, coquille-etroite-plus-sombre.png, coquille-moyenne-sombre.png,
// coquille-large-clair.png, aide-etroite-sombre.png), l'accueil sans coffre (accueil-clair.png,
// accueil-sombre.png), le tableau de bord (aujourdhui-clair.png, aujourdhui-sombre.png,
// aujourdhui-etroit-clair.png, aujourdhui-tablette-sombre.png, aujourdhui-ecran-clair.png,
// aujourdhui-grand-clair.png, aujourdhui-ultralarge-sombre.png, aujourdhui-contraste-eleve.png), une fiche
// et un passage en révision (fiche-clair.png, passage-sombre.png, sur un coffre d'essai jetable),
// la réponse à un rappel (rappel-clair.png, rappel-marquage-sombre.png), la séance d'étude et son
// bilan (seance-clair.png, seance-contraste-eleve.png, seance-etroite-sombre.png, bilan-sombre.png), et affiche le contraste de
// chaque paire de rôles.
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

var folder = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(folder);
var thread = new Thread(() =>
{
    // Comme dans l'application : les polices livrées (pack://application) se lisent une fois WPF initialisé.
    _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
    foreach (var (theme, name) in new[] { (ThemeKind.Light, "clair"), (ThemeKind.Dark, "sombre"), (ThemeKind.HighContrast, "contraste-eleve") })
    {
        var design = new DesignResources(theme, reducedMotion: true);
        Save(Gallery(design, name), Path.Combine(folder, $"galerie-{name}.png"));
    }
    Shell(folder, "coquille-clair.png", ThemePreference.Light, _ => { });
    Shell(folder, "coquille-sombre.png", ThemePreference.Dark, _ => { });
    Shell(folder, "recherche-sombre.png", ThemePreference.Dark, shell =>
    {
        shell.OpenPalette();
        shell.PaletteQuery = "aller";
    });
    Shell(folder, "aide-clair.png", ThemePreference.Light, shell => shell.ShowHelp());
    // Le menu selon la largeur : onglets (fenêtre étroite), colonne d'icônes, barre latérale.
    Shell(folder, "coquille-etroite-clair.png", ThemePreference.Light, _ => { }, width: 390, height: 760);
    Shell(folder, "coquille-etroite-plus-sombre.png", ThemePreference.Dark, shell => shell.Navigate(shell.SecondaryNavigation[1]), width: 390, height: 760);
    Shell(folder, "coquille-moyenne-sombre.png", ThemePreference.Dark, _ => { }, width: 1024, height: 700);
    Shell(folder, "coquille-large-clair.png", ThemePreference.Light, _ => { }, width: 1460, height: 860);
    Shell(folder, "aide-etroite-sombre.png", ThemePreference.Dark, shell => shell.ShowHelp(), width: 390, height: 760);
    // L'accueil d'une installation dont le coffre prévu reste à créer, avec un refus affiché.
    using var app = new KyfRevApplication(new VaultStore(), new BackupFiles(), new PlannedVault(), TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, "1.0.0");
    foreach (var (theme, name) in new[] { (ThemePreference.Light, "clair"), (ThemePreference.Dark, "sombre") })
    {
        Shell(folder, $"accueil-{name}.png", theme, shell => shell.ShowWelcome(new WelcomePageViewModel(
            app.Vaults,
            new NoPicker(),
            app.Vaults.State() with { OpenError = @"Le coffre « D:\Cours\Coffre » est introuvable." },
            _ => { })));
    }
    // Le tableau de bord d'une journée chargée : retards, passage caché par le rappel actif,
    // définition, rappel de consolidation, « À venir », un point de santé à vérifier, et un exemple
    // d'encart de module (les modules ne sont pas encore branchés dans l'application).
    foreach (var (theme, name) in new[] { (ThemePreference.Light, "clair"), (ThemePreference.Dark, "sombre") })
    {
        var today = new DashboardPageViewModel(app.Reminders, app.Sheets, new ReviewNavigation(app.Sheets, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), TimeProvider.System));
        today.Show(SampleDashboard(), SampleCards);
        Shell(folder, $"aujourdhui-{name}.png", theme, _ => { }, new Dictionary<string, object> { [CorePages.Today] = today });
    }
    var contrast = new DashboardPageViewModel(app.Reminders, app.Sheets, new ReviewNavigation(app.Sheets, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), TimeProvider.System));
    contrast.Show(SampleDashboard(), SampleCards);
    Shell(folder, "aujourdhui-contraste-eleve.png", ThemePreference.System, _ => { }, new Dictionary<string, object> { [CorePages.Today] = contrast }, highContrast: true);
    foreach (var (file, theme, width, height) in new[]
    {
        ("aujourdhui-etroit-clair.png", ThemePreference.Light, 390.0, 844.0),
        ("aujourdhui-tablette-sombre.png", ThemePreference.Dark, 834.0, 1000.0),
        ("aujourdhui-ecran-clair.png", ThemePreference.Light, 1600.0, 1000.0),
    })
    {
        var today = new DashboardPageViewModel(app.Reminders, app.Sheets, new ReviewNavigation(app.Sheets, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), TimeProvider.System));
        today.Show(SampleDashboard(), SampleCards);
        Shell(folder, file, theme, _ => { }, new Dictionary<string, object> { [CorePages.Today] = today }, width, height);
    }
    ReviewCaptures(folder);
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();

foreach (var theme in new[] { ThemeKind.Light, ThemeKind.Dark })
{
    var colors = ThemeColors.For(theme);
    Console.WriteLine($"## {theme}");
    foreach (var pair in ContrastRequirements.All)
    {
        var ratio = colors[pair.Foreground].ContrastWith(colors[pair.Background]);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{pair.Foreground} sur {pair.Background} : {ratio:0.00} (≥ {pair.Minimum})"));
    }
}

// La fenêtre de la coquille, hors de l'écran, dans un état donné ; seul son contenu est rendu.
static void Shell(string folder, string file, ThemePreference theme, Action<ShellViewModel> prepare, IReadOnlyDictionary<string, object>? pages = null, double width = 1280, double height = 800, Func<bool>? ready = null, bool highContrast = false)
{
    var design = new DesignResources(ThemeKind.Light, reducedMotion: true);
    var appearance = new AppearanceController(design, new StillAppearance(highContrast), new NoChrome()) { Preference = theme };
    var shell = new ShellViewModel(CorePages.All(pages ?? new Dictionary<string, object>()).Append(CorePages.FromModule(new NavigationEntry("citations", "Citations", 60))), appearance, new GalleryPageViewModel(appearance));
    var window = new ShellWindow(shell) { Width = width, Height = height, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false };
    window.Resources.MergedDictionaries.Add(design.Dictionary);
    window.Show();
    prepare(shell);
    Pump(ready ?? (() => true));
    window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => { });
    var content = (Panel)window.Content;
    // Le fond de la fenêtre n'appartient pas à son contenu : il est repeint dessous pour la capture.
    content.Background = window.Background;
    content.UpdateLayout();
    var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(content);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    var path = Path.Combine(folder, file);
    using (var stream = File.Create(path))
    {
        encoder.Save(stream);
    }
    window.Close();
    Console.WriteLine($"{path} : {bitmap.PixelWidth} × {bitmap.PixelHeight}");
}

// Une fiche et un de ses passages en révision, sur un coffre d'essai jetable : la fiche créée
// le jeudi 1er octobre 2026, ouverte le samedi 3 (J1 en retard d'un jour), rappel actif.
static void ReviewCaptures(string folder)
{
    // Les pages lisent le coffre en tâche de fond : leurs suites reviennent sur ce fil.
    SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
    var root = Path.Combine(Path.GetTempPath(), "kyfrev-captures", Guid.NewGuid().ToString("N"));
    var time = new FixedTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    try
    {
        using var app = new KyfRevApplication(new VaultStore(), new BackupFiles(), new PlannedVault(), time, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, "1.0.0");
        app.Vaults.Create(Path.Combine(root, "Coffre"));
        var sheet = app.Sheets.Create("Histoire", "La Révolution française", null).Value;
        app.Sheets.SetTags(sheet, ["histoire", "partiel"]);
        var opened = app.Sheets.Open(sheet).Value;
        app.Editing.Save(sheet, """
            # La Révolution française

            ## Les causes

            En 1789, la société d'Ancien Régime est divisée en trois ordres : le clergé, la noblesse et le tiers état.
            La crise financière pousse Louis XVI à convoquer les États généraux.

            ## Les étapes

            - 5 mai 1789 : ouverture des États généraux ;
            - 17 juin : les députés du tiers état se proclament Assemblée nationale ;
            - 14 juillet : prise de la Bastille ;
            - 26 août : Déclaration des droits de l'homme et du citoyen.
            """, opened.Fingerprint, 1);
        var anchor = new Anchor("trois ordres", "ciété d'Ancien Régime est divisée en ", " : le clergé, la noblesse et le tie", 50, 62, ["La Révolution française", "Les causes"], 5, opened.Fingerprint);
        var passage = app.Annotations.Create(sheet, anchor, "Qui paie l'impôt ?").Value;
        time.Now = time.Now.AddDays(2);

        ReviewBarViewModel Bar(ItemKind kind, string id, ReviewState state) => new(app.Reviews, app.Reminders, app.Sheets, time, kind, id, state);
        var sheetPage = new SheetPageViewModel(app.Sheets, Bar, sheet, () => { });
        sheetPage.LoadCommand.Execute(null);
        Pump(() => !sheetPage.IsLoading);
        Shell(folder, "fiche-clair.png", ThemePreference.Light, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, sheetPage)));

        var passagePage = new PassagePageViewModel(app.Sheets, Bar, passage, sheet, activeRecall: true, () => { }, _ => { });
        passagePage.LoadCommand.Execute(null);
        Pump(() => !passagePage.IsLoading);
        Shell(folder, "passage-sombre.png", ThemePreference.Dark, shell => shell.Navigate(new NavigationItem("passage", "Passage", Icons.Highlighter, int.MaxValue, passagePage)));

        // La séance : la fiche en cours, puis le bilan d'une séance de douze éléments.
        object Item(SessionItem item, bool activeRecall)
        {
            var page = new SheetPageViewModel(app.Sheets, Bar, item.Id, () => { });
            page.LoadCommand.Execute(null);
            Pump(() => !page.IsLoading);
            return page;
        }
        var items = Enumerable.Repeat(new SessionItem(ItemKind.Sheet, sheet, sheet), 12).ToImmutableArray();
        var running = new StudySessionViewModel(new StudySession(root, new DateOnly(2026, 10, 3), items, 4, 3, 1, 0, TimeSpan.FromMinutes(9), true), new NoSessions(), time, Item, () => { }, () => { });
        Shell(folder, "seance-clair.png", ThemePreference.Light, shell =>
        {
            shell.ToggleFocusMode();
            shell.Navigate(new NavigationItem("seance", "Séance", Icons.Calendar, int.MaxValue, running));
        });
        Shell(
            folder,
            "seance-contraste-eleve.png",
            ThemePreference.System,
            shell =>
            {
                shell.ToggleFocusMode();
                shell.Navigate(new NavigationItem("seance", "Séance", Icons.Calendar, int.MaxValue, running));
            },
            highContrast: true);
        Shell(
            folder,
            "seance-etroite-sombre.png",
            ThemePreference.Dark,
            shell =>
            {
                shell.ToggleFocusMode();
                shell.Navigate(new NavigationItem("seance", "Séance", Icons.Calendar, int.MaxValue, running));
            },
            width: 390,
            height: 844);
        // Les grands écrans : l'aperçu de la ligne choisie, sur la fiche du coffre d'essai ; puis la liste en deux.
        foreach (var (file, theme, width, height) in new[] { ("aujourdhui-grand-clair.png", ThemePreference.Light, 1920.0, 1080.0), ("aujourdhui-ultralarge-sombre.png", ThemePreference.Dark, 2560.0, 1200.0) })
        {
            var sample = SampleDashboard();
            DueItem Real(DueItem item) => item with { SheetId = sheet };
            var today = new DashboardPageViewModel(app.Reminders, app.Sheets, new ReviewNavigation(app.Sheets, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), TimeProvider.System));
            today.Show(
                sample with
                {
                    Overdue = [Real(sample.Overdue[0]) with { Title = "La Révolution française", Folder = "Histoire", Tags = ["histoire", "partiel"] }, .. sample.Overdue.Skip(1).Select(Real)],
                    Today = [.. sample.Today.Select(Real)],
                    Upcoming = [.. sample.Upcoming.Select(Real)],
                    Workload = [.. Enumerable.Range(0, 31).Select(offset => new WorkloadDay(sample.Day.AddDays(offset), [.. sample.Upcoming.Take(SampleWorkload[offset % SampleWorkload.Length])], []))],
                },
                SampleCards);
            Shell(folder, file, theme, _ => { }, new Dictionary<string, object> { [CorePages.Today] = today }, width, height, () => today.ShowsPreview && today.Preview!.HasBody);
        }
        // Un rappel de consolidation : la fiche archivée depuis deux mois, tirée ce jour-là.
        app.Reviews.Archive(sheet).Value.ToString();
        for (var days = 0; days < 75; days++)
        {
            time.Now = time.Now.AddDays(1);
            app.Reminders.PrepareDay().Value.ToString();
        }
        var reminderPage = new SheetPageViewModel(app.Sheets, Bar, sheet, () => { });
        reminderPage.LoadCommand.Execute(null);
        Pump(() => !reminderPage.IsLoading);
        Shell(folder, "rappel-clair.png", ThemePreference.Light, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, reminderPage)));
        reminderPage.Review!.StartMarkingCommand.Execute(null);
        Pump(() => reminderPage.Review.IsMarking);
        Shell(folder, "rappel-marquage-sombre.png", ThemePreference.Dark, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, reminderPage)));
        var finished = new StudySessionViewModel(new StudySession(root, new DateOnly(2026, 10, 3), items, 11, 9, 2, 0, TimeSpan.FromSeconds(754), true), new NoSessions(), time, Item, () => { }, () => { });
        finished.EndCommand.Execute(null);
        Shell(folder, "bilan-sombre.png", ThemePreference.Dark, shell => shell.Navigate(new NavigationItem("seance", "Séance", Icons.Calendar, int.MaxValue, finished)));
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

// Fait tourner le fil de l'interface jusqu'à ce que la page soit lue.
static void Pump(Func<bool> done)
{
    while (!done())
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
}

static FrameworkElement Gallery(DesignResources design, string name)
{
    var page = new StackPanel { Margin = new Thickness(32) };
    page.Children.Add(Text("Kyf.Text.Display", $"kyfRev · design system ({name})"));
    page.Children.Add(Text("Kyf.Text.Secondary", "Identité Veillée : jetons, polices Atkinson, contrastes vérifiés par les tests, composants."));

    page.Children.Add(Section("Typographie"));
    foreach (var style in new[] { "Display", "Title", "Subtitle", "BodyLarge", "Body", "BodyStrong", "Caption" })
    {
        page.Children.Add(Text($"Kyf.Text.{style}", $"{style} — Le cœur a ses raisons que la raison ne connaît point."));
    }

    page.Children.Add(Section("Boutons et champs"));
    var buttons = Row();
    buttons.Children.Add(new Button { Content = "Valider la révision", Style = (Style)design.Dictionary["Kyf.Button.Primary"] });
    buttons.Children.Add(new Button { Content = "Reporter" });
    buttons.Children.Add(new Button { Content = "Plus tard", Style = (Style)design.Dictionary["Kyf.Button.Subtle"] });
    buttons.Children.Add(new Button { Content = Icons.Close, Style = (Style)design.Dictionary["Kyf.Button.Icon"] });
    buttons.Children.Add(new Button { Content = "Indisponible", IsEnabled = false });
    page.Children.Add(buttons);
    var fields = Row();
    var empty = new TextBox { Width = 240 };
    Field.SetPlaceholder(empty, "Rechercher une fiche");
    fields.Children.Add(empty);
    fields.Children.Add(new TextBox { Width = 240, Text = "Mitose et méiose" });
    page.Children.Add(fields);

    page.Children.Add(Section("Pastilles"));
    var pills = Row();
    foreach (var (kind, text) in new[] { (PillKind.Sheet, "Fiche · J7"), (PillKind.Passage, "Passage · J1"), (PillKind.Reminder, "Rappel"), (PillKind.Overdue, "Retard 4 j"), (PillKind.Archived, "Archivée"), (PillKind.NeedsReview, "À vérifier") })
    {
        pills.Children.Add(new Pill { Kind = kind, Text = text });
    }
    page.Children.Add(pills);

    page.Children.Add(Section("Cartes"));
    var cards = Row();
    foreach (var (marker, title) in new[] { (ItemMarker.Sheet, "Mitose"), (ItemMarker.Passage, "« La prophase précède… »"), (ItemMarker.Reminder, "Rappel : Méiose") })
    {
        cards.Children.Add(new Card { Marker = marker, Width = 220, Content = Text("Kyf.Text.BodyStrong", title) });
    }
    page.Children.Add(cards);

    page.Children.Add(Section("Notifications"));
    foreach (var (severity, title, message) in new[]
    {
        (NotificationSeverity.Info, "Nouvelle version", "kyfRev 1.0.0-alpha.2 est disponible."),
        (NotificationSeverity.Success, "Sauvegarde faite", "Le coffre est sauvegardé."),
        (NotificationSeverity.Warning, "Passage à vérifier", "Le texte de la fiche a changé autour de cette annotation."),
        (NotificationSeverity.Critical, "Sauvegarde impossible", "Le dossier des sauvegardes est introuvable."),
    })
    {
        page.Children.Add(new Notification { Severity = severity, Title = title, Message = message, Margin = new Thickness(0, 0, 0, 8), Width = 560, HorizontalAlignment = HorizontalAlignment.Left });
    }

    page.Children.Add(Section("Chargement, état vide, menu et dialogue"));
    var last = Row();
    var skeletons = new StackPanel { Width = 200 };
    foreach (var width in new[] { 200.0, 160, 180 })
    {
        skeletons.Children.Add(new Skeleton { Width = width, Height = 16, Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left });
    }
    last.Children.Add(skeletons);
    last.Children.Add(new Card { Width = 280, Content = new EmptyState { Title = "Rien à réviser", Description = "Toutes les fiches du jour sont faites.", Glyph = Icons.Success, Action = new Button { Content = "Voir demain" } } });
    var menu = new ContextMenu { Resources = design.Dictionary, Placement = System.Windows.Controls.Primitives.PlacementMode.Relative };
    menu.Items.Add(new MenuItem { Header = "Ouvrir", InputGestureText = "Entrée" });
    menu.Items.Add(new MenuItem { Header = "Renommer", InputGestureText = "F2" });
    menu.Items.Add(new Separator());
    menu.Items.Add(new MenuItem { Header = "Mettre à la corbeille", InputGestureText = "Suppr" });
    last.Children.Add(new Image { Source = Render(menu, 260, 190), Width = 260, Height = 190, VerticalAlignment = VerticalAlignment.Top });
    var dialogArea = new Grid { Width = 420, Height = 240 };
    dialogArea.Children.Add(new Card { Content = Text("Kyf.Text.Body", "Fiche « Mitose »") });
    dialogArea.Children.Add(new Dialog { Title = "Mettre à la corbeille ?", Content = Text("Kyf.Text.Body", "La fiche reste récupérable dans la corbeille."), PrimaryButtonText = "Mettre à la corbeille", IsOpen = true });
    last.Children.Add(dialogArea);
    page.Children.Add(last);

    var root = new Border { Resources = design.Dictionary, Child = page, Width = 1280 };
    root.SetResourceReference(Border.BackgroundProperty, DesignKeys.Brush(ColorRole.Background));
    return root;

    TextBlock Text(string style, string text) => new() { Style = (Style)design.Dictionary[style], Text = text, Margin = new Thickness(0, 0, 0, 4) };

    TextBlock Section(string title)
    {
        var block = Text("Kyf.Text.Subtitle", title);
        block.Margin = new Thickness(0, 24, 0, 12);
        return block;
    }

    static WrapPanel Row() => new() { Margin = new Thickness(0, 0, 0, 8), ItemWidth = double.NaN };
}

static BitmapSource Render(FrameworkElement element, double width, double height)
{
    element.Measure(new Size(width, height));
    element.Arrange(new Rect(0, 0, width, height));
    element.UpdateLayout();
    var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(element);
    bitmap.Freeze();
    return bitmap;
}

static void Save(FrameworkElement gallery, string path)
{
    foreach (var panel in ((StackPanel)((Border)gallery).Child).Children.OfType<WrapPanel>())
    {
        foreach (FrameworkElement child in panel.Children)
        {
            child.Margin = new Thickness(0, 0, 12, 12);
            child.VerticalAlignment = VerticalAlignment.Top;
        }
    }
    gallery.Measure(new Size(gallery.Width, double.PositiveInfinity));
    var bitmap = Render(gallery, gallery.Width, Math.Ceiling(gallery.DesiredSize.Height));
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var file = File.Create(path);
    encoder.Save(file);
    Console.WriteLine($"{path} : {bitmap.PixelWidth} × {bitmap.PixelHeight}");
}

// Windows tel qu'il est : ni sombre ni contraste élevé ; le thème vient du choix fait ici.
internal sealed class StillAppearance(bool highContrast) : ISystemAppearance
{
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public bool PrefersDarkTheme => false;

    public bool HighContrast => highContrast;

    public bool ReducedMotion => true;
}

internal sealed class NoChrome : IWindowChrome
{
    public bool UseDarkTitleBar(nint windowHandle, bool dark) => false;
}

// Une installation dont le coffre prévu, dans les Documents, reste à créer.
internal sealed class PlannedVault : IVaultLocations
{
    private const string Planned = @"C:\Users\Élève\Documents\kyfRev\Coffre";

    public AppMode Mode => AppMode.Installed;

    public string? VaultToOpen() => Planned;

    public string? VaultToCreate() => Planned;

    public bool Remember(string root) => true;
}

internal sealed class NoPicker : IFolderPicker
{
    public string? Pick(string title) => null;
}

internal static partial class Program
{
    private static readonly DateOnly Monday = new(2026, 11, 2);

    private static readonly int[] SampleWorkload = [2, 3, 1, 0, 2, 0, 0, 1];

    private static readonly int[] SampleReviews = [4, 3, 5, 0, 6, 2, 0];

    private static readonly DashboardCard[] SampleCards = [new("Citation du jour", "« Le cœur a ses raisons que la raison ne connaît point. »", "Blaise Pascal")];

    // Une journée de révision vraisemblable, sans donnée personnelle.
    private static Dashboard SampleDashboard()
    {
        static DueItem Item(ItemKind kind, string title, string folder, string step, int late, int cycle, string? excerpt = null, string? term = null, string? comment = null, ImmutableArray<string> tags = default) =>
            new(kind, $"id-{title}-{step}", $"fiche-{title}", title, excerpt, term, ["Prophase I"], comment, folder, tags.IsDefault ? [] : tags, step, Monday.AddDays(-late), late, Monday.AddDays(-late - 7), cycle, true);
        return new Dashboard(
            Monday,
            [
                Item(ItemKind.Sheet, "Mitose", "Biologie", "J7", 3, 1, tags: ["bio", "partiel"]),
                Item(ItemKind.Passage, "Méiose", "Biologie", "J3", 1, 1, excerpt: "Les chromosomes homologues s'apparient", comment: "Avant la métaphase I"),
            ],
            [
                Item(ItemKind.Sheet, "Loi d'Ohm", "Physique/Électricité", "J14", 0, 2),
                Item(ItemKind.Sheet, "Théorème de Pythagore", "Mathématiques", "J30", 0, 1),
                Item(ItemKind.Passage, "Circuits RLC", "Physique/Électricité", "J1", 0, 1, excerpt: "impédance", term: "Impédance"),
            ],
            [new PendingReminder("fiche-revolution", "La Révolution française", "Histoire", [], new DateOnly(2026, 5, 4), Monday, 0)],
            [.. Enumerable.Range(1, 6).Select(n => Item(ItemKind.Sheet, $"Chapitre {n}", "Chimie", "J3", 0, 1) with { DueDate = Monday.AddDays(n), DaysLate = 0 })],
            [.. SampleWorkload.Select((count, offset) => new WorkloadDay(Monday.AddDays(offset), [.. Enumerable.Range(0, count).Select(n => Item(ItemKind.Sheet, $"Jour {offset}-{n}", "Chimie", "J3", 0, 1))], []))],
            [.. SampleReviews.Select((reviews, index) => new ReviewDay(Monday.AddDays(index - 6), reviews))],
            2,
            true,
            [],
            Monday.AddDays(1),
            48,
            12,
            1,
            0,
            true,
            false);
    }
}

// Horloge arrêtée, avancée à la main : les captures ne dépendent pas du jour où on les fait.
internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class NoSessions : IStudySessionStore
{
    public StudySession? Load() => null;

    public bool Save(StudySession session) => true;

    public bool Forget() => true;
}
