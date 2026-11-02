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
// bilan (seance-clair.png, seance-contraste-eleve.png, seance-etroite-sombre.png, bilan-sombre.png), les archives
// (archives-clair.png, archives-sombre.png, archives-etroit-clair.png, archives-etroit-filtres-sombre.png,
// archives-contraste-eleve.png), les fiches (fiches-clair.png, fiches-sombre.png, fiches-etroit-clair.png,
// fiches-nouvelle-sombre.png, fiches-contraste-eleve.png), le glossaire (definitions-clair.png, definitions-sombre.png,
// definitions-large-sombre.png, definitions-etroit-clair.png, definitions-modification-sombre.png, definitions-contraste-eleve.png),
// la lecture à voix haute (ecoute-sombre.png, ecoute-clair.png, ecoute-etroite-sombre.png), les réglages (parametres-clair.png, parametres-sombre.png, parametres-etroit-sombre.png, parametres-refus-sombre.png, parametres-contraste-eleve.png), la lecture d'une fiche (fiche-clair.png,
// lecture-sombre.png, lecture-moyenne-clair.png, lecture-etroite-sombre.png, lecture-volet-clair.png, lecture-contraste-eleve.png), l'écriture (ecriture-clair.png,
// ecriture-sombre.png, ecriture-insertion-sombre.png, ecriture-moyenne-clair.png, ecriture-etroite-sombre.png, ecriture-contraste-eleve.png,
// ecriture-markdown-sombre.png, ecriture-markdown-clair.png), et affiche le contraste de
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
using KyfRev.Presentation.Wpf.Editing;
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
        var today = new DashboardPageViewModel(app.Reminders, app.Sheets, new ReviewNavigation(app.Sheets, app.Annotations, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), TimeProvider.System, new NoLinks(), app.Editing, new EditorEnvironment(new OneMistake(), Path.Combine(Path.GetTempPath(), "kyfrev-captures-webview2"))));
        today.Show(SampleDashboard(), SampleCards);
        Shell(folder, $"aujourdhui-{name}.png", theme, _ => { }, new Dictionary<string, object> { [CorePages.Today] = today });
    }
    var contrast = new DashboardPageViewModel(app.Reminders, app.Sheets, new ReviewNavigation(app.Sheets, app.Annotations, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), TimeProvider.System, new NoLinks(), app.Editing, new EditorEnvironment(new OneMistake(), Path.Combine(Path.GetTempPath(), "kyfrev-captures-webview2"))));
    contrast.Show(SampleDashboard(), SampleCards);
    Shell(folder, "aujourdhui-contraste-eleve.png", ThemePreference.System, _ => { }, new Dictionary<string, object> { [CorePages.Today] = contrast }, highContrast: true);
    foreach (var (file, theme, width, height) in new[]
    {
        ("aujourdhui-etroit-clair.png", ThemePreference.Light, 390.0, 844.0),
        ("aujourdhui-tablette-sombre.png", ThemePreference.Dark, 834.0, 1000.0),
        ("aujourdhui-ecran-clair.png", ThemePreference.Light, 1600.0, 1000.0),
    })
    {
        var today = new DashboardPageViewModel(app.Reminders, app.Sheets, new ReviewNavigation(app.Sheets, app.Annotations, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), TimeProvider.System, new NoLinks(), app.Editing, new EditorEnvironment(new OneMistake(), Path.Combine(Path.GetTempPath(), "kyfrev-captures-webview2"))));
        today.Show(SampleDashboard(), SampleCards);
        Shell(folder, file, theme, _ => { }, new Dictionary<string, object> { [CorePages.Today] = today }, width, height);
    }
    // Les archives : chaque état de rappel, un dernier rappel, un fichier introuvable.
    foreach (var (file, theme, width, height, highContrast) in new[]
    {
        ("archives-clair.png", ThemePreference.Light, 1280.0, 800.0, false),
        ("archives-sombre.png", ThemePreference.Dark, 1280.0, 800.0, false),
        ("archives-etroit-clair.png", ThemePreference.Light, 390.0, 844.0, false),
        ("archives-contraste-eleve.png", ThemePreference.System, 1280.0, 800.0, true),
    })
    {
        var archives = new ArchivesPageViewModel(app.Reminders, app.Reviews, new ReviewNavigation(app.Sheets, app.Annotations, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), TimeProvider.System, new NoLinks(), app.Editing, new EditorEnvironment(new OneMistake(), Path.Combine(Path.GetTempPath(), "kyfrev-captures-webview2"))), TimeProvider.System);
        archives.Show(SampleArchives());
        Shell(
            folder,
            file,
            theme,
            shell => shell.Navigate(shell.MainNavigation.Single(item => item.Id == CorePages.Archives)),
            new Dictionary<string, object> { [CorePages.Archives] = archives },
            width,
            height,
            highContrast: highContrast);
    }
    // Les filtres dépliés sur petit écran, l'un d'eux choisi.
    var filtered = new ArchivesPageViewModel(app.Reminders, app.Reviews, new ReviewNavigation(app.Sheets, app.Annotations, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), TimeProvider.System, new NoLinks(), app.Editing, new EditorEnvironment(new OneMistake(), Path.Combine(Path.GetTempPath(), "kyfrev-captures-webview2"))), TimeProvider.System);
    filtered.Show(SampleArchives());
    filtered.Bar.Filters[0].Selected = filtered.Bar.Filters[0].Choices[1];
    filtered.Bar.AreOpen = true;
    Shell(
        folder,
        "archives-etroit-filtres-sombre.png",
        ThemePreference.Dark,
        shell => shell.Navigate(shell.MainNavigation.Single(item => item.Id == CorePages.Archives)),
        new Dictionary<string, object> { [CorePages.Archives] = filtered },
        390,
        844);
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
// L'image du texte mis en forme, une fois la page prête et ses polices chargées.
static BitmapSource WebPicture(Microsoft.Web.WebView2.Wpf.WebView2 web)
{
    Pump(() => web.CoreWebView2 is not null);
    Task<string>? ready = null;
    Pump(() =>
    {
        ready ??= web.ExecuteScriptAsync("document.querySelector('.ProseMirror') !== null && getComputedStyle(document.body).backgroundColor !== 'rgba(0, 0, 0, 0)' && document.fonts.status === 'loaded'");
        if (!ready.IsCompleted)
        {
            return false;
        }
        var done = ready.Result == "true";
        ready = null;
        return done;
    });
    var picture = CapturedPicture(web);
    Pump(() => picture.IsCompleted);
    return picture.Result;
}

static async Task<BitmapSource> CapturedPicture(Microsoft.Web.WebView2.Wpf.WebView2 web)
{
    using var stream = new MemoryStream();
    await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, stream).ConfigureAwait(true);
    stream.Position = 0;
    return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
}

static IEnumerable<DependencyObject> Descendants(DependencyObject root)
{
    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
    {
        var child = VisualTreeHelper.GetChild(root, i);
        yield return child;
        foreach (var deeper in Descendants(child))
        {
            yield return deeper;
        }
    }
}

static void Shell(string folder, string file, ThemePreference theme, Action<ShellViewModel> prepare, IReadOnlyDictionary<string, object>? pages = null, double width = 1280, double height = 800, Func<bool>? ready = null, bool highContrast = false, Action<DependencyObject>? adjust = null)
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
    if (adjust is not null)
    {
        adjust(window);
        window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => { });
    }
    var content = (Panel)window.Content;
    // Le fond de la fenêtre n'appartient pas à son contenu : il est repeint dessous pour la capture.
    content.Background = window.Background;
    content.UpdateLayout();
    var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(content);
    // Le texte mis en forme a sa propre fenêtre, que la capture de WPF ne voit pas : son image est posée dessus.
    foreach (var web in Descendants(content).OfType<Microsoft.Web.WebView2.Wpf.WebView2>().Where(view => view.IsVisible))
    {
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawImage(WebPicture(web), new Rect(web.TranslatePoint(default, content), web.RenderSize));
        }
        bitmap.Render(drawing);
    }
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

            > Qu'est-ce que le tiers état ? Tout. Qu'a-t-il été jusqu'à présent ? Rien.

            | Ordre | Part de la population | Privilèges |
            |---|:-:|---|
            | Clergé | ~0,5 % | exemption d'impôts |
            | Noblesse | ~1,5 % | droits seigneuriaux |
            | Tiers état | ~98 % | **aucun** |

            ## Les étapes

            - 5 mai 1789 : ouverture des États généraux ;
            - 17 juin : les députés du tiers état se proclament Assemblée nationale ;
            - 14 juillet : prise de la *Bastille* ;
            - 26 août : Déclaration des droits de l'homme et du citoyen[^1].

            ## La dette

            La charge de la dette approche $\frac{1}{2}$ des dépenses : $d = \sum_{i} r_i c_i$ en 1788.

            $$
            \frac{\text{intérêts}}{\text{dépenses}} \approx 0{,}5
            $$

            ```python
            def ordres():
                return ["clergé", "noblesse", "tiers état"]
            ```

            Voir aussi [une autre fiche](Autre.md) et [le site](https://exemple.org).

            [^1]: Votée par l'Assemblée constituante.
            """, opened.Fingerprint, 1);
        var anchor = new Anchor("trois ordres", "ciété d'Ancien Régime est divisée en ", " : le clergé, la noblesse et le tie", 50, 62, ["La Révolution française", "Les causes"], 5, opened.Fingerprint);
        var passage = app.Annotations.Create(sheet, anchor, "Qui paie l'impôt ?").Value;
        app.Annotations.Create(sheet, new Anchor("Bastille", "14 juillet : prise de la ", " ;", 400, 408, ["Les étapes"], null, app.Sheets.Open(sheet).Value.Fingerprint), "Le symbole de la Révolution.");
        app.Annotations.CreateDefinition(sheet, new Anchor("tiers état", "le clergé, la noblesse et le ", ".", 95, 105, ["Les causes"], null, app.Sheets.Open(sheet).Value.Fingerprint), new Definition("Tiers état", "Ceux qui ne sont ni nobles ni clercs."));
        time.Now = time.Now.AddDays(2);

        ReviewBarViewModel Bar(ItemKind kind, string id, ReviewState state) => new(app.Reviews, app.Reminders, app.Sheets, time, kind, id, state);
        SheetEditorViewModel Editor(OpenedSheet opened) => new(opened, app.Editing, app.Sheets, new EditorEnvironment(new OneMistake(), Path.Combine(Path.GetTempPath(), "kyfrev-captures-webview2")));
        var sheetPage = new SheetPageViewModel(app.Sheets, app.Annotations, Bar, sheet, () => { }, _ => { }, new NoLinks(), Editor);
        sheetPage.LoadCommand.Execute(null);
        Pump(() => !sheetPage.IsLoading);
        void Reading(string file, ThemePreference theme, double width = 1600, double height = 1000) =>
            Shell(folder, file, theme, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, sheetPage)), null, width, height);
        Reading("fiche-clair.png", ThemePreference.Light);
        Reading("lecture-sombre.png", ThemePreference.Dark);
        Reading("lecture-moyenne-clair.png", ThemePreference.Light, 1200, 820);
        Reading("lecture-etroite-sombre.png", ThemePreference.Dark, 800, 760);
        sheetPage.IsOutlineOn = true;
        Reading("lecture-volet-clair.png", ThemePreference.Light, 800, 760);
        sheetPage.CloseOverlayCommand.Execute(null);
        Shell(folder, "lecture-contraste-eleve.png", ThemePreference.Light, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, sheetPage)), null, 1600, 1000, highContrast: true);

        // L'écriture : le texte mis en forme par défaut, le ruban et ses deux onglets, puis le code à gauche et son rendu.
        sheetPage.StartWritingCommand.Execute(null);
        Pump(() => sheetPage.Editor is not null);
        void Writing(string file, ThemePreference theme, double width = 1600, double height = 1000, bool highContrast = false, bool insert = false)
        {
            void ShowTab(DependencyObject root)
            {
                if (insert)
                {
                    var tab = Descendants(root).OfType<System.Windows.Controls.Primitives.ToggleButton>().First(button => button.Name == "InsertTab");
                    tab.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                }
            }
            Shell(folder, file, theme, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, sheetPage)), null, width, height, () => sheetPage.Editor!.Preview is not null, highContrast, ShowTab);
        }
        Writing("ecriture-clair.png", ThemePreference.Light);
        Writing("ecriture-sombre.png", ThemePreference.Dark);
        Writing("ecriture-insertion-sombre.png", ThemePreference.Dark, insert: true);
        Writing("ecriture-moyenne-clair.png", ThemePreference.Light, 1100, 800);
        Writing("ecriture-etroite-sombre.png", ThemePreference.Dark, 640, 800);
        Writing("ecriture-contraste-eleve.png", ThemePreference.Light, highContrast: true);
        sheetPage.Editor!.IsMarkdownOn = true;
        Pump(() => sheetPage.Editor!.IsMarkdownShown);
        Writing("ecriture-markdown-sombre.png", ThemePreference.Dark);
        Writing("ecriture-markdown-clair.png", ThemePreference.Light);
        sheetPage.StopWritingCommand.Execute(null);
        Pump(() => sheetPage.Editor is null);

        // Le format étendu : couleur, taille, alignement et tâches, en lecture et à l'écriture ; et le choix de la couleur.
        var formatted = app.Sheets.Create("Histoire", "Mise en forme", null).Value;
        app.Editing.Save(formatted, """
            # Mise en forme

            Un texte avec du <span style="color:#d32f2f">rouge</span>, du <span style="color:#00897b">vert</span>, du <span style="color:#1565c0">bleu</span>, de l'<span style="color:#b7791f">or</span> et du <span style="color:#7b1fa2">violet</span>.

            Du <span style="font-size:0.71em">petit</span>, du normal, du <span style="font-size:1.41em">grand</span> et du <span style="font-size:2.35em">très grand</span>.

            <div align="center">

            Un paragraphe au centre, avec **du gras**.

            </div>

            <div align="right">

            Un paragraphe à droite.

            </div>

            <div align="justify">

            Un paragraphe justifié : ses lignes vont d'un bord à l'autre du texte, sauf la dernière. Il faut assez de mots pour que cela se voie, et ceux-ci devraient suffire à remplir plusieurs lignes dans la colonne de lecture.

            </div>

            ## Tâches

            - [x] Relire le chapitre
            - [ ] Faire les exercices
            - [ ] Préparer les questions
            """, app.Sheets.Open(formatted).Value.Fingerprint, 1);
        var formatPage = new SheetPageViewModel(app.Sheets, app.Annotations, Bar, formatted, () => { }, _ => { }, new NoLinks(), Editor);
        formatPage.LoadCommand.Execute(null);
        Pump(() => !formatPage.IsLoading);
        // L'historique : une version en face du texte actuel.
        var kept = app.Sheets.Create("Histoire", "Historique", null).Value;
        app.Editing.Save(kept, "# Historique\n\nUne ligne qui ne change pas.\n\nUne ligne que l'on va modifier.\n\nUne ligne que l'on va retirer.\n\nFin.\n", app.Sheets.Open(kept).Value.Fingerprint, 1);
        var keptPage = new SheetPageViewModel(app.Sheets, app.Annotations, Bar, kept, () => { }, _ => { }, new NoLinks(), Editor);
        keptPage.LoadCommand.Execute(null);
        Pump(() => !keptPage.IsLoading);
        keptPage.StartWritingCommand.Execute(null);
        Pump(() => keptPage.Editor is not null);
        app.Editing.KeepAsVersion(kept, "# Historique\n\nUne ligne qui ne change pas.\n\nUne ligne avant sa modification.\n\nUne ligne que l'on va retirer.\n\nFin.\n");
        foreach (var (file, theme) in new[] { ("historique-clair.png", ThemePreference.Light), ("historique-sombre.png", ThemePreference.Dark) })
        {
            var historyReady = DateTime.UtcNow.AddSeconds(3);
            Shell(folder, file, theme, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, keptPage)), null, 1600, 1000, () =>
            {
                if (!keptPage.Editor!.IsHistoryOpen && !keptPage.Editor.OpenHistoryCommand.IsRunning)
                {
                    keptPage.Editor.OpenHistoryCommand.Execute(null);
                }
                return keptPage.Editor.IsHistoryOpen && DateTime.UtcNow > historyReady;
            });
        }
        keptPage.Editor!.CloseHistoryCommand.Execute(null);
        keptPage.StopWritingCommand.Execute(null);
        Pump(() => keptPage.Editor is null);

        // Écouter la fiche : le bloc lu ressort, et la barre passe à la ligne sur une fenêtre étroite.
        foreach (var (file, theme, width, height) in new[]
        {
            ("ecoute-sombre.png", ThemePreference.Dark, 1280.0, 800.0),
            ("ecoute-clair.png", ThemePreference.Light, 1280.0, 800.0),
            ("ecoute-etroite-sombre.png", ThemePreference.Dark, 390.0, 844.0),
        })
        {
            var listened = new SheetPageViewModel(app.Sheets, app.Annotations, Bar, sheet, () => { }, _ => { }, new NoLinks(), Editor) { ReadAloud = new ReadAloudViewModel(new SilentSpeech()) };
            listened.LoadCommand.Execute(null);
            Pump(() => !listened.IsLoading);
            Shell(folder, file, theme, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, listened)), null, width, height, null, adjust: root =>
            {
                Descendants(root).OfType<System.Windows.Controls.Primitives.ToggleButton>().First(toggle => toggle.Name == "ListenToggle")
                    .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                listened.ReadAloud!.PlayPauseCommand.Execute(null);
                listened.ReadAloud.NextCommand.Execute(null);
                listened.ReadAloud.NextCommand.Execute(null);
            });
        }

        void Format(string file, ThemePreference theme) =>
            Shell(folder, file, theme, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, formatPage)), null, 1600, 1000, () => formatPage.Editor is null || formatPage.Editor.Preview is not null);
        Format("format-lecture-clair.png", ThemePreference.Light);
        Format("format-lecture-sombre.png", ThemePreference.Dark);
        formatPage.StartWritingCommand.Execute(null);
        Pump(() => formatPage.Editor is not null);
        Format("format-ecriture-clair.png", ThemePreference.Light);
        Format("format-ecriture-sombre.png", ThemePreference.Dark);
        formatPage.StopWritingCommand.Execute(null);
        Pump(() => formatPage.Editor is null);

        // Les SmartArt : tous les modèles dans une fiche, en lecture, puis la galerie.
        var drawn = app.Sheets.Create("Histoire", "SmartArt", null).Value;
        var drawings = string.Join("\n\n", KyfRev.Rendering.Diagrams.SmartArt.Layouts.Select(layout => $"```smartart {layout.Id}\n{layout.Sample}\n```"));
        app.Editing.Save(drawn, drawings, app.Sheets.Open(drawn).Value.Fingerprint, 1);
        var drawnPage = new SheetPageViewModel(app.Sheets, app.Annotations, Bar, drawn, () => { }, _ => { }, new NoLinks(), Editor);
        drawnPage.LoadCommand.Execute(null);
        Pump(() => !drawnPage.IsLoading);
        void Drawn(string file, ThemePreference theme) =>
            Shell(folder, file, theme, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, drawnPage)), null, 1600, 1000, () => drawnPage.Editor is null || drawnPage.Editor.Preview is not null);
        Drawn("smartart-lecture-clair.png", ThemePreference.Light);
        Drawn("smartart-lecture-sombre.png", ThemePreference.Dark);
        drawnPage.StartWritingCommand.Execute(null);
        Pump(() => drawnPage.Editor is not null);
        var drawingsReady = DateTime.UtcNow.AddSeconds(4);
        Shell(folder, "smartart-ecriture-sombre.png", ThemePreference.Dark, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, drawnPage)), null, 1600, 1000, () => DateTime.UtcNow > drawingsReady);
        drawnPage.Editor!.OpenGalleryCommand.Execute(GalleryKind.SmartArt);
        Drawn("galerie-smartart-clair.png", ThemePreference.Light);
        Drawn("galerie-smartart-sombre.png", ThemePreference.Dark);
        drawnPage.Editor!.AnswerGalleryCommand.Execute(false);
        drawnPage.StopWritingCommand.Execute(null);
        Pump(() => drawnPage.Editor is null);

        // Les schémas et les graphiques : la galerie, dans le vrai moteur, puis tous les modèles en lecture.
        using var mermaid = new MermaidEngine(Path.Combine(Path.GetTempPath(), "kyfrev-captures-webview2"));
        var host_ = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        host_.Resources[MermaidEngine.ResourceKey] = mermaid;
        var schemas = app.Sheets.Create("Histoire", "Schémas et graphiques", null).Value;
        var schemaBody = string.Join("\n\n", KyfRev.Rendering.Diagrams.MermaidTemplates.All.Select(template => $"## {template.Name}\n\n```mermaid\n{KyfRev.Rendering.Diagrams.MermaidTemplates.Code(template.Id, template.Sample)}\n```"));
        app.Editing.Save(schemas, schemaBody, app.Sheets.Open(schemas).Value.Fingerprint, 1);
        var schemasPage = new SheetPageViewModel(app.Sheets, app.Annotations, Bar, schemas, () => { }, _ => { }, new NoLinks(), Editor);
        schemasPage.LoadCommand.Execute(null);
        Pump(() => !schemasPage.IsLoading);
        void Schemas(string file, ThemePreference theme, double seconds)
        {
            var ready = DateTime.UtcNow.AddSeconds(seconds);
            Shell(folder, file, theme, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, schemasPage)), null, 1600, 1000, () => DateTime.UtcNow > ready);
        }
        Schemas("schemas-lecture-clair.png", ThemePreference.Light, 6);
        Schemas("schemas-lecture-sombre.png", ThemePreference.Dark, 6);
        schemasPage.StartWritingCommand.Execute(null);
        Pump(() => schemasPage.Editor is not null);
        schemasPage.Editor!.OpenGalleryCommand.Execute(GalleryKind.Schema);
        Schemas("galerie-schema-clair.png", ThemePreference.Light, 5);
        Schemas("galerie-schema-sombre.png", ThemePreference.Dark, 5);
        schemasPage.Editor!.AnswerGalleryCommand.Execute(false);
        schemasPage.Editor!.OpenGalleryCommand.Execute(GalleryKind.Chart);
        Schemas("galerie-graphique-clair.png", ThemePreference.Light, 5);
        Schemas("galerie-graphique-sombre.png", ThemePreference.Dark, 5);
        schemasPage.Editor!.AnswerGalleryCommand.Execute(false);
        schemasPage.StopWritingCommand.Execute(null);
        Pump(() => schemasPage.Editor is null);

        // Tous les schémas et les graphiques d'un coup d'œil, dans chaque thème.
        foreach (var (name, kind) in new[] { ("clair", ThemeKind.Light), ("sombre", ThemeKind.Dark) })
        {
            var design = new DesignResources(kind, reducedMotion: true);
            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Width = 1720 };
            grid.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty, "Kyf.Brush.Background");
            var host = new Window { Content = grid, SizeToContent = SizeToContent.WidthAndHeight, Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            host.Resources.MergedDictionaries.Add(design.Dictionary);
            host.Show();
            foreach (var template in KyfRev.Rendering.Diagrams.MermaidTemplates.All)
            {
                var drawing = mermaid.DrawAsync(KyfRev.Rendering.Diagrams.MermaidTemplates.Code(template.Id, template.Sample)!, MermaidEngine.ThemeOf(grid));
                Pump(() => drawing.IsCompleted);
                var cell = new StackPanel { Margin = new Thickness(10) };
                var title = new TextBlock { Text = template.Name };
                title.SetResourceReference(TextBlock.ForegroundProperty, "Kyf.Brush.TextSecondary");
                cell.Children.Add(title);
                if (drawing.Result is { } picture)
                {
                    using var stream = new MemoryStream(picture.Png.ToArray());
                    cell.Children.Add(new System.Windows.Controls.Image { Source = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad), MaxHeight = 300, MaxWidth = 400 });
                }
                grid.Children.Add(cell);
            }
            host.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => { });
            var sheetPicture = new RenderTargetBitmap((int)grid.ActualWidth, (int)grid.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            sheetPicture.Render(grid);
            var sheetEncoder = new PngBitmapEncoder();
            sheetEncoder.Frames.Add(BitmapFrame.Create(sheetPicture));
            using (var stream = File.Create(Path.Combine(folder, $"mermaid-tous-{name}.png")))
            {
                sheetEncoder.Save(stream);
            }
            host.Close();
        }

        // Tous les SmartArt d'un coup d'œil, et leur vignette.
        foreach (var (name, kind) in new[] { ("clair", ThemeKind.Light), ("sombre", ThemeKind.Dark) })
        {
            var design = new DesignResources(kind, reducedMotion: true);
            var viewType = typeof(SheetEditorViewModel).Assembly.GetType("KyfRev.Presentation.Wpf.Reading.DiagramView")!;
            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Width = 1720 };
            grid.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty, "Kyf.Brush.Background");
            foreach (var layout in KyfRev.Rendering.Diagrams.SmartArt.Layouts)
            {
                var drawing = (FrameworkElement)Activator.CreateInstance(viewType)!;
                viewType.GetProperty("Scene")!.SetValue(drawing, KyfRev.Rendering.Diagrams.SmartArt.Draw(layout.Id, layout.Sample));
                drawing.Margin = new Thickness(10);
                grid.Children.Add(drawing);
            }
            var host = new Window { Content = grid, SizeToContent = SizeToContent.WidthAndHeight, Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            host.Resources.MergedDictionaries.Add(design.Dictionary);
            host.Show();
            host.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => { });
            var sheetPicture = new RenderTargetBitmap((int)grid.ActualWidth, (int)grid.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            sheetPicture.Render(grid);
            var sheetEncoder = new PngBitmapEncoder();
            sheetEncoder.Frames.Add(BitmapFrame.Create(sheetPicture));
            using (var stream = File.Create(Path.Combine(folder, $"smartart-tous-{name}.png")))
            {
                sheetEncoder.Save(stream);
            }
            host.Close();
        }

        foreach (var (name, kind) in new[] { ("clair", ThemeKind.Light), ("sombre", ThemeKind.Dark) })
        {
            var chooser = (FrameworkElement)Activator.CreateInstance(typeof(SheetEditorViewModel).Assembly.GetType("KyfRev.Presentation.Wpf.Editing.ColorChooser")!)!;
            var host = new Window { Content = chooser, SizeToContent = SizeToContent.WidthAndHeight, Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            host.Resources.MergedDictionaries.Add(new DesignResources(kind, reducedMotion: true).Dictionary);
            host.Show();
            chooser.GetType().GetMethod("Prepare")!.Invoke(chooser, null);
            host.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => { });
            var picture = new RenderTargetBitmap((int)chooser.ActualWidth, (int)chooser.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            picture.Render(chooser);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(picture));
            using (var stream = File.Create(Path.Combine(folder, $"format-couleurs-{name}.png")))
            {
                encoder.Save(stream);
            }
            host.Close();
        }

        // Le texte à trous : la fiche en révision, avec le rappel actif, un trou déjà affiché.
        var revisingPage = new SheetPageViewModel(app.Sheets, app.Annotations, Bar, sheet, () => { }, _ => { }, new NoLinks(), Editor, revising: true, () => true);
        revisingPage.LoadCommand.Execute(null);
        Pump(() => !revisingPage.IsLoading);
        void Holes(string file, ThemePreference theme) =>
            Shell(folder, file, theme, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, revisingPage)), null, 1600, 1000);
        Holes("trous-clair.png", ThemePreference.Light);
        Holes("trous-sombre.png", ThemePreference.Dark);

        var passagePage = new PassagePageViewModel(app.Sheets, Bar, passage, sheet, activeRecall: true, () => { }, _ => { });
        passagePage.LoadCommand.Execute(null);
        Pump(() => !passagePage.IsLoading);
        Shell(folder, "passage-sombre.png", ThemePreference.Dark, shell => shell.Navigate(new NavigationItem("passage", "Passage", Icons.Highlighter, int.MaxValue, passagePage)));

        // La séance : la fiche en cours, puis le bilan d'une séance de douze éléments.
        object Item(SessionItem item, bool activeRecall)
        {
            var page = new SheetPageViewModel(app.Sheets, app.Annotations, Bar, item.Id, () => { }, _ => { }, new NoLinks(), Editor);
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
            var today = new DashboardPageViewModel(app.Reminders, app.Sheets, new ReviewNavigation(app.Sheets, app.Annotations, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), TimeProvider.System, new NoLinks(), app.Editing, new EditorEnvironment(new OneMistake(), Path.Combine(Path.GetTempPath(), "kyfrev-captures-webview2"))));
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
        var reminderPage = new SheetPageViewModel(app.Sheets, app.Annotations, Bar, sheet, () => { }, _ => { }, new NoLinks(), Editor);
        reminderPage.LoadCommand.Execute(null);
        Pump(() => !reminderPage.IsLoading);
        Shell(folder, "rappel-clair.png", ThemePreference.Light, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, reminderPage)));
        reminderPage.Review!.StartMarkingCommand.Execute(null);
        Pump(() => reminderPage.Review.IsMarking);
        Shell(folder, "rappel-marquage-sombre.png", ThemePreference.Dark, shell => shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, reminderPage)));
        var finished = new StudySessionViewModel(new StudySession(root, new DateOnly(2026, 10, 3), items, 11, 9, 2, 0, TimeSpan.FromSeconds(754), true), new NoSessions(), time, Item, () => { }, () => { });
        finished.EndCommand.Execute(null);
        Shell(folder, "bilan-sombre.png", ThemePreference.Dark, shell => shell.Navigate(new NavigationItem("seance", "Séance", Icons.Calendar, int.MaxValue, finished)));

        // Les fiches : par dossier, avec l'aperçu de la première et une fiche archivée.
        foreach (var (folderName, tag, title, body) in new[]
        {
            ("Biologie", "biologie", "La cellule eucaryote", "# La cellule eucaryote\n\nLa cellule eucaryote possède un noyau délimité par une membrane, qui renferme l'ADN.\nLes mitochondries produisent l'énergie sous forme d'ATP.\n"),
            ("Biologie", "biologie", "Mitose et méiose", "# Mitose et méiose\n"),
            ("Histoire", "histoire", "Napoléon et l'Empire", "# Napoléon et l'Empire\n"),
            ("Réseaux", "réseaux", "Le modèle OSI", "# Le modèle OSI\n"),
        })
        {
            var created = app.Sheets.Create(folderName, title, null).Value;
            app.Sheets.SetTags(created, [tag]);
            app.Editing.Save(created, body, app.Sheets.Open(created).Value.Fingerprint, 1);
            if (title == "Mitose et méiose")
            {
                app.Reviews.Archive(created);
            }
        }
        SheetsPageViewModel SheetsPage()
        {
            var navigation = new ReviewNavigation(app.Sheets, app.Annotations, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), time, new NoLinks(), app.Editing, new EditorEnvironment(new OneMistake(), Path.Combine(Path.GetTempPath(), "kyfrev-captures-webview2")));
            var sheets = new SheetsPageViewModel(app.Sheets, app.Folders, app.Templates, navigation, time);
            sheets.RefreshCommand.Execute(null);
            Pump(() => !sheets.IsLoading && sheets.HasRows && sheets.Preview is not null);
            return sheets;
        }
        foreach (var (file, theme, width, height, highContrast) in new[]
        {
            ("fiches-clair.png", ThemePreference.Light, 1280.0, 800.0, false),
            ("fiches-sombre.png", ThemePreference.Dark, 1280.0, 800.0, false),
            ("fiches-etroit-clair.png", ThemePreference.Light, 390.0, 844.0, false),
            ("fiches-contraste-eleve.png", ThemePreference.System, 1280.0, 800.0, true),
        })
        {
            var sheets = SheetsPage();
            Shell(
                folder,
                file,
                theme,
                shell => shell.Navigate(shell.MainNavigation.Single(item => item.Id == CorePages.Sheets)),
                new Dictionary<string, object> { [CorePages.Sheets] = sheets },
                width,
                height,
                highContrast: highContrast);
        }
        var creating = SheetsPage();
        creating.RequestCreateCommand.Execute(null);
        Pump(() => creating.IsCreating);
        Shell(
            folder,
            "fiches-nouvelle-sombre.png",
            ThemePreference.Dark,
            shell => shell.Navigate(shell.MainNavigation.Single(item => item.Id == CorePages.Sheets)),
            new Dictionary<string, object> { [CorePages.Sheets] = creating },
            1280,
            800);

        // Le glossaire : des définitions dans plusieurs dossiers, une position à vérifier et un passage perdu.
        var sheetsByTitle = app.Sheets.List().Value.ToDictionary(entry => entry.Title, entry => entry.Id);
        string Define(string title, string term, string explanation, params string[] section) =>
            app.Annotations.CreateDefinition(
                sheetsByTitle[title],
                new Anchor(term, string.Empty, string.Empty, 0, term.Length, [.. section], null, app.Sheets.Open(sheetsByTitle[title]).Value.Fingerprint),
                new Definition(term, explanation)).Value;
        Define("La cellule eucaryote", "Noyau", "Compartiment délimité par une double membrane, qui renferme l'ADN de la cellule.", "Structure");
        Define("La cellule eucaryote", "Mitochondrie", "Organite qui produit l'énergie de la cellule sous forme d'ATP.", "Structure", "Les organites");
        Define("Mitose et méiose", "Prophase", "Première phase de la division : les chromosomes se condensent et l'enveloppe du noyau se fragmente.", "La mitose");
        Define("Napoléon et l'Empire", "Code civil", "Recueil de lois promulgué en 1804, qui unifie le droit français.");
        var toCheck = Define("Le modèle OSI", "Couche transport", "Quatrième couche : elle assure le transfert de bout en bout, avec TCP ou UDP.", "Les sept couches");
        app.Annotations.Reanchor(toCheck, new Anchor("Couche transport", string.Empty, string.Empty, 0, 16, ["Les sept couches"], null, "sha256:aa"), AnchorState.NeedsReview);
        var lost = Define("Le modèle OSI", "Encapsulation", "Chaque couche ajoute son en-tête aux données de la couche du dessus.");
        app.Annotations.Reanchor(lost, new Anchor("Encapsulation", string.Empty, string.Empty, 0, 13, [], null, "sha256:aa"), AnchorState.NotFound);
        foreach (var (file, theme, width, height, highContrast) in new[]
        {
            ("definitions-clair.png", ThemePreference.Light, 1280.0, 800.0, false),
            ("definitions-sombre.png", ThemePreference.Dark, 1280.0, 800.0, false),
            ("definitions-large-sombre.png", ThemePreference.Dark, 1920.0, 1080.0, false),
            ("definitions-etroit-clair.png", ThemePreference.Light, 390.0, 844.0, false),
            ("definitions-contraste-eleve.png", ThemePreference.System, 1280.0, 800.0, true),
        })
        {
            var glossary = new DefinitionsPageViewModel(app.Annotations, new ReviewNavigation(app.Sheets, app.Annotations, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), time, new NoLinks(), app.Editing, new EditorEnvironment(new OneMistake(), Path.Combine(Path.GetTempPath(), "kyfrev-captures-webview2"))));
            Shell(
                folder,
                file,
                theme,
                shell => shell.Navigate(shell.MainNavigation.Single(item => item.Id == CorePages.Definitions)),
                new Dictionary<string, object> { [CorePages.Definitions] = glossary },
                width,
                height,
                highContrast: highContrast);
        }
        foreach (var (file, theme, width, height, highContrast) in new[]
        {
            ("parametres-clair.png", ThemePreference.Light, 1280.0, 800.0, false),
            ("parametres-sombre.png", ThemePreference.Dark, 1280.0, 800.0, false),
            ("parametres-etroit-sombre.png", ThemePreference.Dark, 390.0, 844.0, false),
            ("parametres-contraste-eleve.png", ThemePreference.System, 1280.0, 800.0, true),
        })
        {
            var settingsPage = new SettingsPageViewModel(app.Settings, time);
            Shell(
                folder,
                file,
                theme,
                shell => shell.Navigate(shell.SecondaryNavigation.Single(item => item.Id == CorePages.Settings)),
                new Dictionary<string, object> { [CorePages.Settings] = settingsPage },
                width,
                height,
                () => settingsPage.IsReady,
                highContrast: highContrast);
        }
        var refusedSettings = new SettingsPageViewModel(app.Settings, time);
        Shell(
            folder,
            "parametres-refus-sombre.png",
            ThemePreference.Dark,
            shell => shell.Navigate(shell.SecondaryNavigation.Single(item => item.Id == CorePages.Settings)),
            new Dictionary<string, object> { [CorePages.Settings] = refusedSettings },
            1280,
            800,
            () =>
            {
                if (refusedSettings.IsReady && !refusedSettings.HasError && !refusedSettings.SaveCommand.IsRunning)
                {
                    refusedSettings.Steps = "3, 1";
                    refusedSettings.SaveCommand.Execute(null);
                }
                return refusedSettings.HasError;
            });
        var editedGlossary = new DefinitionsPageViewModel(app.Annotations, new ReviewNavigation(app.Sheets, app.Annotations, app.Reviews, app.Reminders, app.Vaults, new NoSessions(), time, new NoLinks(), app.Editing, new EditorEnvironment(new OneMistake(), Path.Combine(Path.GetTempPath(), "kyfrev-captures-webview2"))));
        Shell(
            folder,
            "definitions-modification-sombre.png",
            ThemePreference.Dark,
            shell => shell.Navigate(shell.MainNavigation.Single(item => item.Id == CorePages.Definitions)),
            new Dictionary<string, object> { [CorePages.Definitions] = editedGlossary },
            1280,
            800,
            () =>
            {
                if (editedGlossary.Rows.Count > 0 && editedGlossary.Rows.All(row => row.IsShown))
                {
                    editedGlossary.Rows[0].EditCommand.Execute(null);
                }
                return editedGlossary.Rows.Any(row => row.IsEditing);
            });
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
    private static ImmutableArray<ArchivedSheetEntry> SampleArchives() =>
    [
        new("f_1", "La Révolution française : les causes", "Histoire", ["histoire", "partiel"], new DateOnly(2026, 8, 20), false, ReminderStatus.Pending, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 20), 1, 0, null, null, true),
        new("f_2", "Mitose et méiose", "Sciences/Biologie", ["biologie"], new DateOnly(2026, 7, 29), false, ReminderStatus.Resting, new DateOnly(2027, 1, 30), null, 1, 1, new DateOnly(2026, 9, 30), KyfRev.Domain.Reviews.ReminderOutcome.Known, true),
        new("f_3", "L'ADN et la synthèse des protéines", "Sciences/Biologie", ["biologie", "génétique"], new DateOnly(2026, 8, 3), false, ReminderStatus.Eligible, new DateOnly(2026, 10, 4), null, 2, 1, new DateOnly(2026, 10, 4), KyfRev.Domain.Reviews.ReminderOutcome.SomePointsToReview, true),
        new("f_4", "Les sources du droit", "Droit", ["droit"], new DateOnly(2026, 9, 12), true, ReminderStatus.Excluded, new DateOnly(2026, 11, 12), null, 0, 0, null, null, true),
        new("f_5", "Le théorème de Thalès", "", [], new DateOnly(2026, 6, 2), false, ReminderStatus.Resting, new DateOnly(2026, 11, 2), null, 3, 2, new DateOnly(2026, 7, 2), KyfRev.Domain.Reviews.ReminderOutcome.ReviewEntirely, false),
    ];

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

internal sealed class OneMistake : KyfRev.Application.Ports.ISpelling
{
    private const string Wrong = "Bastille";

    public IReadOnlyList<KyfRev.Application.Ports.SpellingMistake> Check(string text)
    {
        var at = text.IndexOf(Wrong, StringComparison.Ordinal);
        return at < 0 ? [] : [new KyfRev.Application.Ports.SpellingMistake(at, Wrong.Length)];
    }

    public KyfRev.Application.Ports.SpellingCorrection? Correct(string word) => null;

    public bool Add(string word) => false;
}

/// <summary>Des voix muettes : une capture n'a pas besoin de les entendre.</summary>
internal sealed class SilentSpeech : KyfRev.Application.Ports.ISpeech
{
    public IReadOnlyList<string> FrenchVoices() => ["Microsoft Hortense Desktop"];

    public Task<KyfRev.Application.Outcome<KyfRev.Application.Done>> SpeakAsync(string text, string? voice, double rate, CancellationToken cancellation) =>
        new TaskCompletionSource<KyfRev.Application.Outcome<KyfRev.Application.Done>>().Task;

    public void PauseSpeaking()
    {
    }

    public void ResumeSpeaking()
    {
    }
}

internal sealed class NoLinks : KyfRev.Application.Ports.IExternalLinks
{
    public bool Open(string address) => true;
}

internal sealed class NoSessions : IStudySessionStore
{
    public StudySession? Load() => null;

    public bool Save(StudySession session) => true;

    public bool Forget() => true;
}
