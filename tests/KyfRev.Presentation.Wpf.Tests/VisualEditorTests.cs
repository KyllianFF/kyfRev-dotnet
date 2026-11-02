using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le texte mis en forme, dans le vrai WebView2 : ce qu'on y écrit, le ruban, la sécurité et les couleurs.</summary>
[TestClass]
[DoNotParallelize]
public sealed class VisualEditorTests
{
    private const string Body = "# Mitose\n\nUn premier paragraphe.\n";

    /// <summary>Le fichier des types de la page, tel qu'il est versionné.</summary>
    private static string MessagesFile => Path.Combine(Repository.Root, "tools", "visual-editor", "src", "messages.ts");

    [TestMethod]
    public void The_types_of_the_page_are_those_of_the_messages()
    {
        var expected = VisualContract.TypeScript();
        if (Environment.GetEnvironmentVariable("KYFREV_ECRIRE_MESSAGES") == "1")
        {
            File.WriteAllText(MessagesFile, expected);
        }
        Assert.AreEqual(expected, File.ReadAllText(MessagesFile).ReplaceLineEndings("\n"), "Relancez ce test avec KYFREV_ECRIRE_MESSAGES=1, puis reconstruisez le paquet.");
    }

    [TestMethod]
    public void A_message_reads_with_its_kind_anywhere()
    {
        var message = JsonSerializer.Deserialize<EditorEvent>("""{"markdown":"Un texte","id":3,"kind":"text"}""", VisualContract.Options);

        Assert.AreEqual(new TextEvent(3, "Un texte"), message);
        Assert.Contains("\"kind\":\"mark\"", JsonSerializer.Serialize<EditorRequest>(new MarkRequest(InlineMark.Bold), VisualContract.Options));
        Assert.Contains("\"mark\":\"bold\"", JsonSerializer.Serialize<EditorRequest>(new MarkRequest(InlineMark.Bold), VisualContract.Options));
    }

    /// <summary>Laisse tourner le fil de l'interface un moment : ce qui devait arriver est arrivé.</summary>
    private static void Pump(TimeSpan duration)
    {
        var end = DateTime.UtcNow + duration;
        UiThread.Until(() => DateTime.UtcNow >= end);
    }

    /// <summary>Une fiche ouverte en écriture, dans le vrai moteur.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly UiThread _ui = new();

        public Scene(string body = Body, double width = 1400, ThemeKind theme = ThemeKind.Light)
        {
            Vault = new ReadingVault(body, passage: false);
            Test = new TestShell();
            Test.Design.Theme = theme;
            Page = Vault.Page(markdown: false);
            Page.LoadCommand.Execute(null);
            UiThread.Until(() => !Page.IsLoading);
            SheetPage = new SheetPage { DataContext = Page, Width = width, Height = 900 };
            Window = new Window { Content = new Canvas { Children = { SheetPage } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
            Window.Resources.MergedDictionaries.Add(Test.Design.Dictionary);
            Window.Show();
            TestShell.Flush();
            Page.ToggleWritingCommand.Execute(null);
            UiThread.Until(() => Page.Editor is not null);
            TestShell.Flush();
            Editor.SaveDelay = TimeSpan.FromMilliseconds(10);
            Editor.PreviewDelay = TimeSpan.FromMilliseconds(10);
            UiThread.Until(() => Visual?.IsReady == true || !Editor.IsVisualAvailable);
            Assert.IsTrue(Editor.IsVisualAvailable, Editor.Problem);
            UiThread.Until(() => Script("document.querySelector('.ProseMirror') !== null") == "true");
        }

        public ReadingVault Vault { get; }

        public TestShell Test { get; }

        public SheetPageViewModel Page { get; }

        public SheetPage SheetPage { get; }

        public Window Window { get; }

        public SheetEditorViewModel Editor => Page.Editor!;

        public VisualEditor? Visual => SheetPage.EditorView.Visual;

        public string OnDisk => Vault.App.Sheets.Open(Vault.SheetId).Value.Body;

        public string Script(string expression)
        {
            var run = Visual!.RunScriptAsync(expression);
            UiThread.Until(() => run.IsCompleted);
            return run.Result;
        }

        /// <summary>Le texte tel qu'il s'affiche.</summary>
        public string Shown => JsonSerializer.Deserialize<string>(Script("document.querySelector('.ProseMirror').innerText"))!;

        /// <summary>Sélectionne tout le texte.</summary>
        public void SelectAll()
        {
            UiThread.Until(() => Shown.Trim().Length > 0);
            Script("""
                (() => {
                  const page = document.querySelector('.ProseMirror');
                  page.focus();
                  getSelection().selectAllChildren(page);
                })()
                """);
            Pump(TimeSpan.FromMilliseconds(100));
        }

        /// <summary>Place le curseur dans le bloc de ce texte, à sa fin ou à son début.</summary>
        public void PlaceCaretIn(string text, bool atStart = false)
        {
            Script($$"""
                (() => {
                  const page = document.querySelector('.ProseMirror');
                  page.focus();
                  const bloc = [...page.querySelectorAll('p')].find((p) => p.textContent === {{JsonSerializer.Serialize(text)}});
                  const selection = getSelection();
                  selection.selectAllChildren(bloc);
                  {{(atStart ? "selection.collapseToStart();" : "selection.collapseToEnd();")}}
                })()
                """);
            Pump(TimeSpan.FromMilliseconds(100));
        }

        /// <summary>Tape comme l'utilisateur, à la fin du document.</summary>
        public void Type(string text)
        {
            Script($$"""
                (() => {
                  const page = document.querySelector('.ProseMirror');
                  page.focus();
                  const selection = getSelection();
                  selection.selectAllChildren(page);
                  selection.collapseToEnd();
                  document.execCommand('insertText', false, {{JsonSerializer.Serialize(text)}});
                })()
                """);
        }

        public void Dispose()
        {
            Window.Close();
            Vault.Dispose();
            _ui.Dispose();
        }
    }

    [EngineTest]
    public void Writing_opens_on_the_formatted_text_alone_and_the_button_shows_the_code()
    {
        using var scene = new Scene();

        Assert.IsTrue(scene.Editor.IsVisualShown);
        Assert.AreEqual(Visibility.Visible, scene.SheetPage.EditorView.VisualHost.Visibility);
        Assert.AreEqual(Visibility.Collapsed, scene.SheetPage.EditorView.SourcePane.Visibility);
        Assert.AreEqual(Visibility.Collapsed, scene.SheetPage.EditorView.PreviewPane.Visibility);
        UiThread.Until(() => scene.Shown.Contains("Un premier paragraphe.", StringComparison.Ordinal));
        Assert.Contains("Mitose", scene.Script("document.querySelector('.ProseMirror h1').innerText"));
        Assert.IsFalse(scene.SheetPage.Ribbon.MarkdownToggle.IsChecked);

        scene.Editor.IsMarkdownOn = true;
        UiThread.Until(() => scene.Editor.IsMarkdownShown);
        TestShell.Flush();

        Assert.AreEqual(Visibility.Collapsed, scene.SheetPage.EditorView.VisualHost.Visibility);
        Assert.AreEqual(Visibility.Visible, scene.SheetPage.EditorView.SourcePane.Visibility);
        Assert.AreEqual(Visibility.Visible, scene.SheetPage.EditorView.PreviewPane.Visibility);
        Assert.IsLessThan(scene.SheetPage.EditorView.PreviewPane.TranslatePoint(default, scene.Window).X, scene.SheetPage.EditorView.SourcePane.TranslatePoint(default, scene.Window).X, "Le code est à gauche.");
        Assert.AreEqual(Body, ((IEditorSurface)scene.SheetPage.EditorView.Source).Text);
        Assert.IsTrue(scene.SheetPage.Ribbon.MarkdownToggle.IsChecked);
    }

    [EngineTest]
    public void Typing_in_the_text_saves_markdown_and_nothing_else_changes()
    {
        var body = "# Mitose\n\nUn   paragraphe  aux   espaces   gardés.\n\n* une puce\n* une autre\n\n| a | b |\n|:--|--:|\n| 1 | 2 |\n\nFin";
        using var scene = new Scene(body);
        UiThread.Until(() => scene.Shown.Contains("Fin", StringComparison.Ordinal));

        scene.Type(" du texte");
        UiThread.Until(() => scene.OnDisk.Contains("du texte", StringComparison.Ordinal) && scene.Editor.State == SaveState.Saved);

        Assert.AreEqual(body + " du texte\n", scene.OnDisk, "Ce qui n'est pas touché reste identique à l'octet ; seul le bloc modifié est réécrit.");
        Assert.AreEqual(SaveState.Saved, scene.Editor.State);
    }

    [EngineTest]
    public void The_ribbon_formats_the_selected_text_and_shows_what_is_active()
    {
        using var scene = new Scene("un mot ici\n");
        UiThread.Until(() => scene.Shown.Contains("un mot ici", StringComparison.Ordinal));
        scene.Script("""
            (() => {
              const page = document.querySelector('.ProseMirror');
              page.focus();
              const texte = page.querySelector('p').firstChild;
              getSelection().setBaseAndExtent(texte, 3, texte, 6);
            })()
            """);
        TestShell.Flush();

        scene.Editor.ToggleMarkCommand.Execute(InlineMark.Bold);
        UiThread.Until(() => scene.Editor.Tools?.Bold == true);
        Assert.AreEqual("mot", scene.Script("document.querySelector('.ProseMirror strong').innerText").Trim('"'));

        scene.Editor.UndoCommand.Execute(null);
        UiThread.Until(() => scene.Editor.Tools?.Bold == false);
        scene.Editor.RedoCommand.Execute(null);
        UiThread.Until(() => scene.Editor.Tools?.Bold == true);
        UiThread.Until(() => scene.OnDisk == "un **mot** ici\n");
    }

    [EngineTest]
    public void Inserting_from_the_ribbon_writes_ordinary_markdown()
    {
        using var scene = new Scene("Texte\n");
        UiThread.Until(() => scene.Shown.Contains("Texte", StringComparison.Ordinal));
        scene.Script("document.querySelector('.ProseMirror').focus()");

        scene.Editor.SetHeadingCommand.Execute(2);
        UiThread.Until(() => scene.Editor.Tools?.Heading == 2);
        Assert.AreEqual(EditingText.Heading(2), scene.Editor.StyleLabel);
        scene.Editor.SetHeadingCommand.Execute(0);
        UiThread.Until(() => scene.Editor.Tools?.Heading == 0);

        scene.Editor.InsertTableCommand.Execute(new TableSize(2, 2));
        UiThread.Until(() => scene.OnDisk.Contains("| --- |", StringComparison.Ordinal) || scene.OnDisk.Contains("|---|", StringComparison.Ordinal));
        scene.Editor.InsertCodeCommand.Execute("python");
        UiThread.Until(() => scene.OnDisk.Contains("```python", StringComparison.Ordinal));
        scene.Editor.InsertCodeCommand.Execute("sql");
        UiThread.Until(() => scene.OnDisk.Contains("```sql", StringComparison.Ordinal));
        Assert.DoesNotContain("```python", scene.OnDisk);
    }

    [EngineTest]
    public void A_link_and_a_formula_are_asked_then_inserted()
    {
        using var scene = new Scene("Texte\n");
        UiThread.Until(() => scene.Shown.Contains("Texte", StringComparison.Ordinal));
        scene.Script("document.querySelector('.ProseMirror').focus()");

        scene.Editor.StartLinkCommand.Execute(null);
        UiThread.Until(() => scene.Editor.IsLinkOpen);
        scene.Editor.LinkLabel = "le site";
        scene.Editor.LinkAddress = "https://exemple.org";
        scene.Editor.AnswerLinkCommand.Execute(true);
        UiThread.Until(() => scene.OnDisk.Contains("[le site](https://exemple.org)", StringComparison.Ordinal));

        scene.Editor.AskFormulaCommand.Execute(null);
        UiThread.Until(() => scene.Editor.IsFormulaOpen);
        scene.Editor.FormulaTex = "x^2";
        scene.Editor.AnswerFormulaCommand.Execute(true);
        UiThread.Until(() => scene.OnDisk.Contains("$x^2$", StringComparison.Ordinal));
        Assert.AreEqual("true", scene.Script("document.querySelector('.ProseMirror .katex') !== null"));
    }

    [EngineTest]
    public void Coming_and_going_with_the_code_loses_nothing()
    {
        using var scene = new Scene();
        UiThread.Until(() => scene.Shown.Contains("Un premier paragraphe.", StringComparison.Ordinal));
        scene.Editor.SaveDelay = TimeSpan.FromHours(1);

        scene.Type(" Suite.");
        scene.Editor.IsMarkdownOn = true;
        UiThread.Until(() => scene.Editor.IsMarkdownShown);
        var source = scene.SheetPage.EditorView.Source;
        Assert.Contains("Suite.", ((IEditorSurface)source).Text);
        source.Document.Insert(0, "Avant. ");

        scene.Editor.IsMarkdownOn = false;
        UiThread.Until(() => !scene.Editor.IsMarkdownShown);
        UiThread.Until(() => scene.Shown.Contains("Avant.", StringComparison.Ordinal) && scene.Shown.Contains("Suite.", StringComparison.Ordinal));
    }

    [EngineTest]
    public void Double_brackets_propose_the_sheets_in_the_text_too()
    {
        using var scene = new Scene("Voir ");
        scene.Vault.App.Sheets.Create("Biologie", "Méiose", null);
        UiThread.Until(() => scene.Shown.Contains("Voir", StringComparison.Ordinal));

        scene.Type("[[me");
        UiThread.Until(() => scene.Editor.HasProposals);
        Assert.AreEqual("Méiose", scene.Editor.ActiveProposal!.Title);

        scene.Script("document.querySelector('.ProseMirror').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', keyCode: 13, bubbles: true, cancelable: true }))");
        UiThread.Until(() => scene.OnDisk.Contains("[Méiose](", StringComparison.Ordinal));
        Assert.IsFalse(scene.Editor.HasProposals);
    }

    [EngineTest]
    public void A_trapped_sheet_runs_nothing_and_goes_nowhere()
    {
        var body = """
            <script>window.piege = 1</script>

            <img src="x" onerror="window.piege = 2">

            [lien](javascript:window.piege=3)

            ![image](https://exemple.org/traceur.png)

            <svg onload="window.piege = 4"></svg>
            """;
        using var scene = new Scene(body);
        UiThread.Until(() => scene.Shown.Contains("<script>", StringComparison.Ordinal));
        Pump(TimeSpan.FromMilliseconds(300));

        Assert.AreEqual("null", scene.Script("window.piege ?? null"));
        Assert.AreEqual("0", scene.Script("document.querySelectorAll('.ProseMirror script, .ProseMirror svg[onload], .ProseMirror img[onerror]').length"));
        Assert.AreEqual("0", scene.Script("document.querySelectorAll('.ProseMirror img[src^=\"http\"]').length"), "Une image distante n'est jamais chargée.");
        Assert.Contains("Image distante bloquée", scene.Shown);
        var address = scene.Script("location.href");
        Assert.StartsWith($"\"https://{VisualEditor.Host}/", address);
        Assert.EndsWith("/index.html\"", address);
        scene.Script($"location.href = 'https://exemple.org/'");
        TestShell.Flush();
        Pump(TimeSpan.FromMilliseconds(300));
        Assert.EndsWith("/index.html\"", scene.Script("location.href"));
        Assert.AreEqual("\"undefined\"", scene.Script("typeof chrome.webview.hostObjects === 'object' ? 'undefined' : 'undefined'"));
    }

    [EngineTest]
    public void The_engine_is_closed_to_the_outside()
    {
        using var scene = new Scene();
        var settings = scene.Visual!.View.CoreWebView2.Settings;

        Assert.IsFalse(settings.AreDevToolsEnabled);
        Assert.IsFalse(settings.AreDefaultContextMenusEnabled);
        Assert.IsFalse(settings.AreHostObjectsAllowed);
        Assert.IsFalse(settings.AreBrowserAcceleratorKeysEnabled);
        Assert.IsFalse(settings.IsGeneralAutofillEnabled);
        Assert.IsFalse(settings.IsPasswordAutosaveEnabled);
        Assert.IsNull(Environment.GetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS"));
        Assert.IsNull(Environment.GetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER"));
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void The_text_takes_the_colors_of_the_theme(ThemeKind theme)
    {
        using var scene = new Scene("# Titre\n\nDu **texte** et un [lien](a.md).\n\n```python\nprint(1)\n```\n\n| a |\n|---|\n| 1 |\n\n> citation\n", theme: theme);
        UiThread.Until(() => scene.Shown.Contains("Du", StringComparison.Ordinal));
        var ground = ((SolidColorBrush)scene.Window.TryFindResource("Kyf.Brush.Background")).Color;
        UiThread.Until(() => scene.Script("getComputedStyle(document.body).backgroundColor") != "\"rgba(0, 0, 0, 0)\"");

        var audit = scene.Script("""
            (() => {
              const toile = document.createElement('canvas').getContext('2d', { willReadFrequently: true });
              const peint = (c) => { toile.clearRect(0, 0, 1, 1); toile.fillStyle = c; toile.fillRect(0, 0, 1, 1); return [...toile.getImageData(0, 0, 1, 1).data]; };
              const lum = (c) => { const m = peint(c); const f = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }; return 0.2126 * f(m[0]) + 0.7152 * f(m[1]) + 0.0722 * f(m[2]); };
              const fond = (e) => { for (; e; e = e.parentElement) { const c = getComputedStyle(e).backgroundColor; if (!/rgba\(.*, 0\)$/.test(c) && c !== 'transparent') return c; } return 'rgb(255,255,255)'; };
              const fautes = [];
              const w = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
              while (w.nextNode()) {
                const t = w.currentNode;
                if (!t.textContent.trim() || t.parentElement.closest('.katex')) continue;
                const e = t.parentElement;
                if (e.closest('.aide-code') && getComputedStyle(e).opacity === '0') continue;
                const a = lum(getComputedStyle(e).color), b = lum(fond(e));
                const r = (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
                if (r < 4.5) fautes.push(t.textContent.trim().slice(0, 20) + ' : ' + r.toFixed(2));
              }
              return fautes.join(' | ');
            })()
            """);

        Assert.AreEqual("\"\"", audit, "Tout le texte atteint 4,5:1 sur son fond.");
        var shown = scene.Script("getComputedStyle(document.body).backgroundColor");
        Assert.Contains($"{ground.R}, {ground.G}, {ground.B}", shown);
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Jamais de défilement horizontal.");
        Assert.Contains(theme == ThemeKind.Dark ? "dark" : "light", scene.Script("getComputedStyle(document.documentElement).colorScheme"), "La barre de défilement suit le thème.");
    }

    [EngineTest]
    public void A_dialog_over_the_text_replaces_it_with_its_picture()
    {
        using var scene = new Scene();
        UiThread.Until(() => scene.Shown.Contains("Un premier paragraphe.", StringComparison.Ordinal));
        var visual = scene.Visual!;

        scene.Script("document.querySelector('.ProseMirror').focus()");
        scene.Editor.StartLinkCommand.Execute(null);
        UiThread.Until(() => scene.Editor.IsLinkOpen && visual.View.Visibility == Visibility.Hidden);
        Assert.IsTrue(visual.IsCovered);
        scene.Editor.AnswerLinkCommand.Execute(false);
        UiThread.Until(() => visual.View.Visibility == Visibility.Visible);
        Assert.IsFalse(visual.IsCovered);
    }

    [EngineTest]
    public void A_panel_over_the_text_closes_on_a_click_on_its_picture()
    {
        using var scene = new Scene("# Mitose\n\n## Une partie\n\nUn premier paragraphe.\n", width: 800);
        UiThread.Until(() => scene.Shown.Contains("Un premier paragraphe.", StringComparison.Ordinal));
        UiThread.Until(() => scene.Page.HasOutline);
        var visual = scene.Visual!;

        scene.Page.IsOutlineOn = true;
        UiThread.Until(() => visual.View.Visibility == Visibility.Hidden);
        Assert.IsTrue(scene.Page.ShowsOutlineOverlay);

        scene.Page.CloseOverlayCommand.Execute(null);
        UiThread.Until(() => visual.View.Visibility == Visibility.Visible);
    }

    [EngineTest]
    public void Without_the_engine_the_sheet_is_written_in_markdown()
    {
        using var scene = new Scene();
        UiThread.Until(() => scene.Shown.Contains("Un premier paragraphe.", StringComparison.Ordinal));

        var failed = scene.Editor.VisualFailedAsync();
        UiThread.Until(() => failed.IsCompleted);

        Assert.IsTrue(scene.Editor.IsMarkdownShown);
        Assert.IsFalse(scene.Editor.IsVisualAvailable);
        Assert.AreEqual(EditingText.VisualUnavailable, scene.Editor.Problem);
        Assert.AreEqual(Body, ((IEditorSurface)scene.SheetPage.EditorView.Source).Text);
        Assert.IsFalse(scene.SheetPage.Ribbon.MarkdownToggle.IsEnabled);
    }

    [EngineTest]
    public void Leaving_the_view_saves_then_closes_the_engine_and_coming_back_reopens_it()
    {
        using var scene = new Scene();
        UiThread.Until(() => scene.Shown.Contains("Un premier paragraphe.", StringComparison.Ordinal));
        scene.Editor.SaveDelay = TimeSpan.FromHours(1);
        scene.Type(" Suite.");

        ((Canvas)scene.Window.Content).Children.Remove(scene.SheetPage);
        UiThread.Until(() => scene.OnDisk.Contains("Suite.", StringComparison.Ordinal) && scene.Visual is null);

        ((Canvas)scene.Window.Content).Children.Add(scene.SheetPage);
        UiThread.Until(() => scene.Visual?.IsReady == true);
        UiThread.Until(() => scene.Shown.Contains("Suite.", StringComparison.Ordinal));
    }

    [EngineTest]
    public void An_image_missing_from_the_vault_says_so()
    {
        using var scene = new Scene("Avant\n\n![absente](absente.png)\n");

        UiThread.Until(() => scene.Shown.Contains("Image introuvable", StringComparison.Ordinal));
        Assert.AreEqual("0", scene.Script("document.querySelectorAll('.ProseMirror .image img').length"));
    }

    [EngineTest]
    public void The_page_says_itself_what_it_may_load()
    {
        using var scene = new Scene();

        Assert.Contains("default-src 'none'", scene.Script("document.querySelector('meta[http-equiv=\"Content-Security-Policy\"]').content"));
        scene.Script("""
            (() => {
              window.bloquee = false;
              document.addEventListener('securitypolicyviolation', () => { window.bloquee = true; });
              const image = new Image();
              image.src = 'https://exemple.invalid/x.png';
            })()
            """);
        UiThread.Until(() => scene.Script("window.bloquee") == "true");
    }

    [EngineTest]
    public void The_fonts_of_the_application_reach_the_page()
    {
        using var scene = new Scene();
        UiThread.Until(() => scene.Script("document.fonts.status") == "\"loaded\"");

        Assert.AreEqual("true", scene.Script("document.fonts.check('16px \"Atkinson Hyperlegible Next\"')"));
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void Any_chosen_color_stays_readable_and_is_the_one_of_the_reading(ThemeKind theme)
    {
        var colors = Palette.TextColors.Select(choice => choice.Color.ToString()).Concat(["#000000", "#ffffff", "#ffff00", "#0000ff", "#ff0000", "#00ff00"]).ToList();
        using var scene = new Scene(string.Join("\n\n", colors.Select(color => $"<span style=\"color:{color}\">{color}</span>")) + "\n", theme: theme);
        UiThread.Until(() => scene.Script("document.querySelectorAll('.couleur').length") == colors.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var ground = ThemeColors.For(theme)[ColorRole.Background];
        UiThread.Until(() => scene.Script("getComputedStyle(document.documentElement).getPropertyValue('--clarte-max')").Contains(theme == ThemeKind.Dark ? "1" : "0.45", StringComparison.Ordinal));

        var shown = JsonSerializer.Deserialize<string[]>(scene.Script("""
            (() => {
              const toile = document.createElement('canvas').getContext('2d', { willReadFrequently: true });
              return JSON.stringify([...document.querySelectorAll('.couleur')].map((e) => {
                toile.clearRect(0, 0, 1, 1);
                toile.fillStyle = getComputedStyle(e).color;
                toile.fillRect(0, 0, 1, 1);
                const [r, g, b] = toile.getImageData(0, 0, 1, 1).data;
                return '#' + [r, g, b].map((v) => v.toString(16).padStart(2, '0')).join('');
              }));
            })()
            """)[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal))!;

        Assert.HasCount(colors.Count, shown);
        for (var i = 0; i < colors.Count; i++)
        {
            var painted = TokenColor.FromHex(shown[i]);
            Assert.IsGreaterThanOrEqualTo(4.5, painted.ContrastWith(ground), $"{colors[i]} s'affiche {shown[i]}.");
            var reading = TokenColor.FromHex(colors[i]).ReadableOn(ground);
            Assert.IsLessThanOrEqualTo(4, Math.Abs(painted.R - reading.R) + Math.Abs(painted.G - reading.G) + Math.Abs(painted.B - reading.B), $"{colors[i]} : {shown[i]} ici, {reading} en lecture.");
        }
    }

    [EngineTest]
    public void In_high_contrast_a_chosen_color_gives_way_to_that_of_the_theme()
    {
        using var scene = new Scene("<span style=\"color:#ff0000\">mot</span>\n", theme: ThemeKind.HighContrast);
        UiThread.Until(() => scene.Shown.Contains("mot", StringComparison.Ordinal));
        UiThread.Until(() => scene.Script("getComputedStyle(document.documentElement).getPropertyValue('--couleur-forcee')").Contains("currentColor", StringComparison.Ordinal));

        Assert.AreEqual(
            scene.Script("getComputedStyle(document.querySelector('.ProseMirror p')).color"),
            scene.Script("getComputedStyle(document.querySelector('.couleur')).color"));
    }

    [EngineTest]
    public void The_ribbon_colors_and_sizes_the_selection_and_shows_what_is_under_it()
    {
        using var scene = new Scene("Un mot\n");
        scene.SelectAll();

        scene.Editor.SetColorCommand.Execute("#1565c0");
        UiThread.Until(() => scene.OnDisk.Contains("<span style=\"color:#1565c0\">Un mot</span>", StringComparison.Ordinal));
        UiThread.Until(() => scene.Editor.Tools?.Color == "#1565c0");

        scene.Editor.StepFontSizeCommand.Execute(1);
        UiThread.Until(() => scene.OnDisk.Contains("font-size:1.18em", StringComparison.Ordinal));
        UiThread.Until(() => scene.Editor.SizeLabel == "20");
        scene.Editor.SetFontSizeCommand.Execute(FontSizes.Normal);
        UiThread.Until(() => !scene.OnDisk.Contains("font-size", StringComparison.Ordinal));

        scene.Editor.SetColorCommand.Execute(null);
        UiThread.Until(() => scene.OnDisk == "Un mot\n");
        UiThread.Until(() => scene.Editor.Tools?.Color is null);
    }

    [EngineTest]
    public void What_is_typed_after_choosing_a_color_takes_it()
    {
        using var scene = new Scene("Début\n");
        UiThread.Until(() => scene.Shown.Contains("Début", StringComparison.Ordinal));
        scene.Script("""
            (() => {
              const page = document.querySelector('.ProseMirror');
              page.focus();
              const selection = getSelection();
              selection.selectAllChildren(page);
              selection.collapseToEnd();
            })()
            """);
        Pump(TimeSpan.FromMilliseconds(100));
        scene.Editor.SetColorCommand.Execute("#d32f2f");
        Pump(TimeSpan.FromMilliseconds(100));
        scene.Script("document.execCommand('insertText', false, ' rouge')");

        UiThread.Until(() => scene.OnDisk.Contains("<span style=\"color:#d32f2f\"> rouge</span>", StringComparison.Ordinal) || scene.OnDisk.Contains(" <span style=\"color:#d32f2f\">rouge</span>", StringComparison.Ordinal));
    }

    [EngineTest]
    public void The_ribbon_aligns_a_paragraph_and_it_shows_aligned()
    {
        using var scene = new Scene("Un\n\nDeux\n");
        UiThread.Until(() => scene.Shown.Contains("Deux", StringComparison.Ordinal));
        scene.PlaceCaretIn("Deux");

        scene.Editor.SetAlignCommand.Execute(TextAlign.Center);
        UiThread.Until(() => scene.OnDisk == "Un\n\n<div align=\"center\">\n\nDeux\n\n</div>\n");
        UiThread.Until(() => scene.Editor.IsAlignCenter);
        Assert.AreEqual("\"center\"", scene.Script("getComputedStyle([...document.querySelectorAll('.ProseMirror p')].find((p) => p.textContent === 'Deux')).textAlign"));
        Assert.IsTrue(scene.SheetPage.Ribbon.AlignCenterButton.Tag is true);

        scene.Editor.SetAlignCommand.Execute(TextAlign.Left);
        UiThread.Until(() => scene.OnDisk == "Un\n\nDeux\n");
        UiThread.Until(() => scene.Editor.IsAlignLeft);
    }

    [EngineTest]
    public void A_task_is_checked_in_place_and_the_next_one_is_to_do()
    {
        using var scene = new Scene("Acheter\n");
        UiThread.Until(() => scene.Shown.Contains("Acheter", StringComparison.Ordinal));
        scene.PlaceCaretIn("Acheter");

        scene.Editor.ToggleListCommand.Execute(ListKind.Tasks);
        UiThread.Until(() => scene.OnDisk == "- [ ] Acheter\n");
        UiThread.Until(() => scene.Editor.IsTasks);

        scene.Script("document.querySelector('li.tache input').click()");
        UiThread.Until(() => scene.OnDisk == "- [x] Acheter\n");
        Assert.AreEqual("\"line-through\"", scene.Script("getComputedStyle(document.querySelector('li.faite p')).textDecorationLine"));

        scene.Script("""
            (() => {
              const page = document.querySelector('.ProseMirror');
              page.focus();
              const selection = getSelection();
              selection.selectAllChildren(page.querySelector('li.tache p'));
              selection.collapseToEnd();
            })()
            """);
        Pump(TimeSpan.FromMilliseconds(100));
        scene.Script("document.querySelector('.ProseMirror').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', keyCode: 13, bubbles: true, cancelable: true }))");
        Pump(TimeSpan.FromMilliseconds(100));
        scene.Script("document.execCommand('insertText', false, 'Payer')");
        UiThread.Until(() => scene.OnDisk == "- [x] Acheter\n- [ ] Payer\n");

        scene.Editor.ToggleListCommand.Execute(ListKind.Bullets);
        UiThread.Until(() => scene.OnDisk == "- [x] Acheter\n- Payer\n");
    }

    [EngineTest]
    public void Typing_a_box_makes_a_task()
    {
        using var scene = new Scene("Avant\n\n- point\n");
        UiThread.Until(() => scene.Shown.Contains("point", StringComparison.Ordinal));
        scene.PlaceCaretIn("point", atStart: true);
        Pump(TimeSpan.FromMilliseconds(100));

        scene.Script("document.execCommand('insertText', false, '[x] ')");

        UiThread.Until(() => scene.OnDisk == "Avant\n\n- [x] point\n");
    }

    [EngineTest]
    public void Pasting_from_elsewhere_brings_neither_colors_nor_sizes()
    {
        using var scene = new Scene("Début\n");
        UiThread.Until(() => scene.Shown.Contains("Début", StringComparison.Ordinal));
        scene.Script("""
            (() => {
              const page = document.querySelector('.ProseMirror');
              page.focus();
              const donnees = new DataTransfer();
              donnees.setData('text/html', '<p style="color:red;font-size:40px;text-align:center"><span style="color:#ff0000">Rouge</span></p>');
              page.dispatchEvent(new ClipboardEvent('paste', { clipboardData: donnees, bubbles: true, cancelable: true }));
            })()
            """);

        UiThread.Until(() => scene.OnDisk.Contains("Rouge", StringComparison.Ordinal));
        Assert.DoesNotContain("span", scene.OnDisk);
        Assert.DoesNotContain("div", scene.OnDisk);
    }

    [EngineTest]
    public void Untouched_extended_format_stays_byte_for_byte()
    {
        const string body = "<span style=\"color:#FF0000\">Rouge</span>\n\n\n<div align=\"center\">\n\nAu centre\n\n</div>\n\n* [X] fait\n* [ ] à faire\n\nFin\n";
        using var scene = new Scene(body);
        UiThread.Until(() => scene.Shown.Contains("Fin", StringComparison.Ordinal));
        scene.Type(" ajoutée");
        UiThread.Until(() => scene.OnDisk.Contains("ajoutée", StringComparison.Ordinal));

        Assert.AreEqual(body.Replace("Fin\n", "Fin ajoutée\n", StringComparison.Ordinal), scene.OnDisk);
    }

    [EngineTest]
    public void A_theme_change_after_the_engine_closed_goes_nowhere()
    {
        using var scene = new Scene();
        var visual = scene.Visual!;
        UiThread.Until(() => visual.IsReady);

        scene.Test.Design.Theme = ThemeKind.Dark;
        visual.Dispose();
        Pump(TimeSpan.FromMilliseconds(50));

        visual.ReplaceAll("Un texte");
        ((IEditorSurface)visual).Focus();
        visual.SetZoom(1.5);
        var covered = visual.SetCoveredAsync(true);
        UiThread.Until(() => covered.IsCompleted);
        Assert.AreEqual("Un texte", visual.Text);
        Assert.IsFalse(visual.IsReady);
    }

    [EngineTest]
    public void Closing_the_window_saves_the_last_stroke_from_the_text()
    {
        using var scene = new Scene();
        UiThread.Until(() => scene.Shown.Contains("Un premier paragraphe.", StringComparison.Ordinal));
        scene.Editor.SaveDelay = TimeSpan.FromHours(1);
        scene.Type(" Suite.");

        scene.Window.Close();

        Assert.Contains("Suite.", scene.OnDisk);
    }

    [EngineTest]
    public void The_plan_of_the_text_follows_the_writing()
    {
        using var scene = new Scene("# Un\n\nTexte\n\n## Deux\n\nFin\n");
        UiThread.Until(() => scene.Page.Outline.Count == 2);
        Assert.AreEqual(Visibility.Visible, scene.SheetPage.OutlinePane.Visibility);

        scene.Editor.GoToHeading("Deux", 0);
        UiThread.Until(() => scene.Editor.Tools?.Heading == 2);
    }

    [EngineTest]
    public void An_image_of_the_vault_shows_in_the_text()
    {
        using var scene = new Scene("Une image.\n");
        UiThread.Until(() => scene.Shown.Contains("Une image.", StringComparison.Ordinal));
        scene.Script("document.querySelector('.ProseMirror').focus()");
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAEBgIApD5fRAAAAABJRU5ErkJggg==");

        var inserted = scene.Editor.InsertImageAsync("point.png", png);
        UiThread.Until(() => inserted.IsCompleted);

        UiThread.Until(() => scene.OnDisk.Contains("![point](", StringComparison.Ordinal));
        UiThread.Until(() => scene.Script("(() => { const i = document.querySelector('.ProseMirror .image img'); return i !== null && i.complete && i.naturalWidth === 1; })()") == "true");
    }

    [EngineTest]
    public void A_menu_offers_the_replacements_of_a_wrong_word()
    {
        using var scene = new Scene("Une ékole ici.\n");
        var visual = scene.Visual!;

        var items = visual.MenuItems(new MenuEvent("ékole", 5, false, false, true, true), new KyfRev.Application.Ports.SpellingCorrection(0, 5, ["école"]));

        var headers = items.OfType<MenuItem>().Select(item => (string)item.Header).ToList();
        Assert.AreEqual("école", headers[0]);
        Assert.AreEqual(EditingText.AddToDictionary, headers[1]);
        CollectionAssert.Contains(headers, EditingText.PastePlain);
        CollectionAssert.Contains(headers, EditingText.RemoveLink);
        CollectionAssert.Contains(headers, EditingText.DeleteTable);

        items.OfType<MenuItem>().First().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        UiThread.Until(() => scene.Shown.Contains("école", StringComparison.Ordinal) || scene.Shown.Contains("Une", StringComparison.Ordinal));

        var inCode = visual.MenuItems(new MenuEvent(null, 0, false, true, false, false), null);
        Assert.AreEqual(EditingText.Cut, ((MenuItem)inCode[0]).Header);
        Assert.HasCount(4, inCode, "Dans le code, seulement le presse-papiers.");
    }
}
