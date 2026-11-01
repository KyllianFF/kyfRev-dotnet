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
// recherche-sombre.png, aide-clair.png), l'accueil sans coffre (accueil-clair.png,
// accueil-sombre.png), le tableau de bord (aujourdhui-clair.png, aujourdhui-sombre.png), et
// affiche le contraste de chaque paire de rôles.
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
    // définition, rappel de consolidation, « À venir » replié, un point de santé à vérifier.
    foreach (var (theme, name) in new[] { (ThemePreference.Light, "clair"), (ThemePreference.Dark, "sombre") })
    {
        var today = new DashboardPageViewModel(app.Reminders);
        today.Show(SampleDashboard());
        Shell(folder, $"aujourdhui-{name}.png", theme, _ => { }, new Dictionary<string, object> { [CorePages.Today] = today });
    }
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
static void Shell(string folder, string file, ThemePreference theme, Action<ShellViewModel> prepare, IReadOnlyDictionary<string, object>? pages = null)
{
    var design = new DesignResources(ThemeKind.Light, reducedMotion: true);
    var appearance = new AppearanceController(design, new StillAppearance(), new NoChrome()) { Preference = theme };
    var shell = new ShellViewModel(CorePages.All(pages ?? new Dictionary<string, object>()).Append(CorePages.FromModule(new NavigationEntry("citations", "Citations", 60))), appearance, new GalleryPageViewModel(appearance));
    var window = new ShellWindow(shell) { Width = 1280, Height = 800, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false };
    window.Resources.MergedDictionaries.Add(design.Dictionary);
    window.Show();
    prepare(shell);
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

static FrameworkElement Gallery(DesignResources design, string name)
{
    var page = new StackPanel { Margin = new Thickness(32) };
    page.Children.Add(Text("Kyf.Text.Display", $"kyfRev · design system ({name})"));
    page.Children.Add(Text("Kyf.Text.Secondary", "Jetons de la conception §12, contrastes vérifiés par les tests, composants de J5."));

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
internal sealed class StillAppearance : ISystemAppearance
{
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public bool PrefersDarkTheme => false;

    public bool HighContrast => false;

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
            [],
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
