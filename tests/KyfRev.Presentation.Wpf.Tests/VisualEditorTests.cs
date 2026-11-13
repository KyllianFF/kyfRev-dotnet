using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using Microsoft.Web.WebView2.Core;

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
    [EngineTest]
    public void An_extension_block_is_drawn_in_the_editor_as_a_picture()
    {
        var root = Path.Combine(Path.GetTempPath(), "kyfrev-blocs-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, ExtensionManifest.FileName), """{"id":"bloc-quiz","nom":"Q","version":"1","blocs":["quiz"]}""");
        File.WriteAllText(Path.Combine(source, "extension.js"), """kyfrev.bloc("quiz", (s) => `<p><strong>${s.split("\n").length} lignes</strong></p>`);""");
        var library = new ExtensionLibrary(Path.Combine(root, "bibliotheque"), TimeProvider.System);
        Assert.IsTrue(library.Install(source).Succeeded);
        var engine = new ExtensionEngine(FakeEnvironment.ExtensionData, library, new ExtensionRequests(library, new NoData()));
        try
        {
            var blocks = new ExtensionBlocks(library, engine, () => false);
            using var scene = new Scene("Début\n\n```quiz\nQ: a\nR: b\n```\n", blocks: blocks);
            UiThread.Until(
                () => scene.Script("(document.querySelector('.ProseMirror .bloc-dessin img')?.getAttribute('src') ?? '').startsWith('data:image/png;base64,')") == "true",
                TimeSpan.FromMinutes(2));
            Assert.AreEqual("true", scene.Script("[...document.querySelectorAll('.entete-code select option')].some(option => option.value === 'quiz' && option.text.includes('extension'))"));
            Assert.AreEqual("0", scene.Script("document.querySelectorAll('.ProseMirror .bloc-dessin strong').length"), "Le HTML de l'extension n'entre pas dans l'éditeur : seule son image.");
        }
        finally
        {
            engine.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class NoData : IExtensionData
    {
        public Task<Outcome<IReadOnlyList<ExtensionSheet>>> SheetsAsync() => Task.FromResult(Outcome.Of<IReadOnlyList<ExtensionSheet>>([]));

        public Task<Outcome<ExtensionSheetText>> SheetAsync(string id) => Task.FromResult(Outcome.Refused<ExtensionSheetText>("non"));

        public Task<Outcome<string?>> StoredAsync(string extension) => Task.FromResult(Outcome.Of<string?>(null));

        public Task<Outcome<Done>> StoreAsync(string extension, string json) => Task.FromResult(Outcome.Done());

        public Task<Outcome<string>> CreateSheetAsync(string extension, ExtensionNewSheet sheet) => Task.FromResult(Outcome.Refused<string>("non"));

        public Task<Outcome<IReadOnlyList<string>>> SetTagsAsync(string extension, string id, IReadOnlyList<string> tags) => Task.FromResult(Outcome.Refused<IReadOnlyList<string>>("non"));

        public Task<Outcome<Done>> WriteSheetAsync(string extension, string id, string text) => Task.FromResult(Outcome.Refused<Done>("non"));

        public Task<Outcome<IReadOnlyList<ExtensionJournalEntry>>> JournalAsync(string extension) => Task.FromResult(Outcome.Of<IReadOnlyList<ExtensionJournalEntry>>([]));
    }

    private sealed class Scene : IDisposable
    {
        // Le tout premier démarrage du moteur web sur une machine neuve dépasse largement 10 s sur le runner de la CI.
        private static readonly TimeSpan EnginePatience = TimeSpan.FromMinutes(2);

        private readonly UiThread _ui = new();

        public Scene(string body = Body, double width = 1400, ThemeKind theme = ThemeKind.Light, ExtensionBlocks? blocks = null)
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
            if (blocks is not null)
            {
                Window.Resources[ExtensionBlocks.ResourceKey] = blocks;
            }
            Window.Show();
            TestShell.Flush();
            Page.ToggleWritingCommand.Execute(null);
            UiThread.Until(() => Page.Editor is not null);
            TestShell.Flush();
            Editor.SaveDelay = TimeSpan.FromMilliseconds(10);
            Editor.PreviewDelay = TimeSpan.FromMilliseconds(10);
            UiThread.Until(() => Visual?.IsReady == true || !Editor.IsVisualAvailable, EnginePatience);
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

        /// <summary>
        /// Pose une sélection et la fait lire aussitôt à ProseMirror. Il ne la lirait qu'à l'événement du
        /// navigateur ; d'ici là, le minuteur de sa dernière prise de focus peut remettre l'ancienne, et le
        /// ruban agit alors sur le premier bloc.
        /// </summary>
        public void Select(string selecting)
        {
            Script($$"""
                (() => {
                  const page = document.querySelector('.ProseMirror');
                  page.focus();
                  const selection = getSelection();
                  {{selecting}}
                  document.dispatchEvent(new Event('selectionchange'));
                })()
                """);
            Pump(TimeSpan.FromMilliseconds(100));
        }

        /// <summary>Sélectionne tout le texte.</summary>
        public void SelectAll()
        {
            UiThread.Until(() => Shown.Trim().Length > 0);
            Select("selection.selectAllChildren(page);");
        }

        /// <summary>Place le curseur dans le bloc de ce texte, à sa fin ou à son début.</summary>
        public void PlaceCaretIn(string text, bool atStart = false)
        {
            Select($$"""
                const bloc = [...page.querySelectorAll('p')].find((p) => p.textContent === {{JsonSerializer.Serialize(text)}});
                selection.selectAllChildren(bloc);
                {{(atStart ? "selection.collapseToStart();" : "selection.collapseToEnd();")}}
                """);
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
        scene.Select("""
            const texte = page.querySelector('p').firstChild;
            selection.setBaseAndExtent(texte, 3, texte, 6);
            """);

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
    public void The_ribbon_marks_code_in_the_text_and_quotes_a_paragraph()
    {
        using var scene = new Scene("un mot ici\n");
        UiThread.Until(() => scene.Shown.Contains("un mot ici", StringComparison.Ordinal));
        scene.Select("""
            const texte = page.querySelector('p').firstChild;
            selection.setBaseAndExtent(texte, 3, texte, 6);
            """);

        scene.Editor.ToggleMarkCommand.Execute(InlineMark.Code);
        UiThread.Until(() => scene.Editor.Tools?.Code == true);
        UiThread.Until(() => scene.OnDisk == "un `mot` ici\n");

        scene.Editor.ToggleQuoteCommand.Execute(null);
        UiThread.Until(() => scene.Editor.Tools?.Quote == true);
        UiThread.Until(() => scene.OnDisk == "> un `mot` ici\n");

        scene.Editor.ToggleQuoteCommand.Execute(null);
        UiThread.Until(() => scene.Editor.Tools?.Quote == false);
        UiThread.Until(() => scene.OnDisk == "un `mot` ici\n");
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
        Assert.AreEqual($"\"https://{VisualEditor.Host}/index.html\"", scene.Script("location.href"));
        scene.Script($"location.href = 'https://exemple.org/'");
        TestShell.Flush();
        Pump(TimeSpan.FromMilliseconds(300));
        Assert.AreEqual($"\"https://{VisualEditor.Host}/index.html\"", scene.Script("location.href"), "La page ne navigue nulle part.");
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
    public void A_missing_engine_is_named_and_the_sheet_is_written_in_markdown()
    {
        using var scene = new Scene();
        UiThread.Until(() => scene.Shown.Contains("Un premier paragraphe.", StringComparison.Ordinal));

        var failed = scene.Editor.VisualFailedAsync(missesEngine: true);
        UiThread.Until(() => failed.IsCompleted);

        Assert.IsTrue(scene.Editor.IsMarkdownShown);
        Assert.AreEqual(EditingText.VisualEngineMissing, scene.Editor.Problem);
        Assert.AreEqual(Body, ((IEditorSurface)scene.SheetPage.EditorView.Source).Text);
    }

    [TestMethod]
    [DataRow(CoreWebView2ProcessFailedKind.BrowserProcessExited, true)]
    [DataRow(CoreWebView2ProcessFailedKind.RenderProcessExited, true)]
    [DataRow(CoreWebView2ProcessFailedKind.RenderProcessUnresponsive, true)]
    [DataRow(CoreWebView2ProcessFailedKind.GpuProcessExited, false)]
    [DataRow(CoreWebView2ProcessFailedKind.UtilityProcessExited, false)]
    [DataRow(CoreWebView2ProcessFailedKind.FrameRenderProcessExited, false)]
    public void Only_the_engine_or_the_page_stopping_stops_the_editor(CoreWebView2ProcessFailedKind kind, bool stops)
        => Assert.AreEqual(stops, WebProcesses.StopsThePage(kind));

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
    public void The_page_may_load_nothing_from_elsewhere()
    {
        using var scene = new Scene();

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
        UiThread.Until(() => scene.OnDisk.Contains("font-size:1.17em", StringComparison.Ordinal));
        UiThread.Until(() => scene.Editor.SizeLabel == "14");
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
        scene.Select("""
            selection.selectAllChildren(page);
            selection.collapseToEnd();
            """);
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

        scene.Select("""
            selection.selectAllChildren(page.querySelector('li.tache p'));
            selection.collapseToEnd();
            """);
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
    public void A_smartart_shows_its_drawing_and_its_code_only_when_asked()
    {
        using var scene = new Scene("Avant\n\n```smartart cycle\nG1\nS\n```\n\nAprès\n");
        UiThread.Until(() => scene.Script("document.querySelectorAll('.apercu-dessin svg text').length") == "2");

        Assert.AreEqual("\"none\"", scene.Script("getComputedStyle(document.querySelector('.bloc-dessin pre')).display"));
        scene.Script("[...document.querySelectorAll('.action-dessin')].find((b) => b.textContent === 'Voir le code').click()");
        UiThread.Until(() => scene.Script("getComputedStyle(document.querySelector('.bloc-dessin pre')).display") != "\"none\"");

        scene.Script("document.execCommand('insertText', false, 'G2\\n')");
        UiThread.Until(() => scene.Script("document.querySelectorAll('.apercu-dessin svg text').length") == "3");
    }

    private static void Write(DiagramEditorViewModel editor, params string[] texts)
    {
        foreach (var node in editor.Nodes)
        {
            node.Text = string.Empty;
        }
        for (var i = 0; i < texts.Length; i++)
        {
            editor.Nodes[i].Text = texts[i];
        }
    }

    [EngineTest]
    public void The_gallery_inserts_a_smartart_and_opens_again_on_it()
    {
        using var scene = new Scene("Avant\n");
        UiThread.Until(() => scene.Shown.Contains("Avant", StringComparison.Ordinal));
        scene.Editor.OpenGalleryCommand.Execute(GalleryKind.SmartArt);
        UiThread.Until(() => scene.Visual!.IsCovered);
        Assert.IsTrue(scene.SheetPage.GalleryDialog.IsOpen);
        var gallery = scene.Editor.Gallery!;
        gallery.SelectedItem = gallery.Items.Single(item => item.Id == "pyramide");

        scene.Editor.AnswerGalleryCommand.Execute(true);
        Assert.IsTrue(scene.Editor.IsDiagramEditorOpen);
        Write(scene.Editor.DiagramEditor!, "Sommet", "Base");
        scene.Editor.AnswerDiagramEditorCommand.Execute(true);

        UiThread.Until(() => scene.OnDisk.Contains("```smartart pyramide\nSommet\nBase\n```", StringComparison.Ordinal));
        UiThread.Until(() => !scene.Visual!.IsCovered);
        Assert.IsFalse(scene.Editor.IsDiagramEditorOpen);
        UiThread.Until(() => scene.Script("document.querySelectorAll('.apercu-dessin svg text').length") == "2");

        scene.Script("[...document.querySelectorAll('.action-dessin')].find((b) => b.textContent === 'Modifier…').click()");
        UiThread.Until(() => scene.Editor.IsDiagramEditorOpen);
        var editor = scene.Editor.DiagramEditor!;
        Assert.AreEqual("pyramide", editor.SelectedModel.Id);
        Assert.AreEqual("Sommet\nBase", editor.Source()!.Value.Code);
        editor.Select(editor.Nodes[0]);
        editor.AddSiblingCommand.Execute(null);
        editor.Selected!.Text = "Milieu";
        scene.Editor.AnswerDiagramEditorCommand.Execute(true);

        UiThread.Until(() => scene.OnDisk.Contains("```smartart pyramide\nSommet\nMilieu\nBase\n```", StringComparison.Ordinal));
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Count(scene.OnDisk, "smartart"), "Il est remplacé, pas ajouté.");
    }

    [EngineTest]
    public void A_schema_of_the_gallery_opens_again_and_the_same_one_retouched_stays_in_its_code()
    {
        using var scene = new Scene("Avant\n");
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        scene.Window.Resources[MermaidEngine.ResourceKey] = engine;
        UiThread.Until(() => scene.Shown.Contains("Avant", StringComparison.Ordinal));
        scene.Editor.OpenGalleryCommand.Execute(GalleryKind.Schema);
        scene.Editor.AnswerGalleryCommand.Execute(true);
        Write(scene.Editor.DiagramEditor!, "Début", "Fin");
        scene.Editor.AnswerDiagramEditorCommand.Execute(true);

        UiThread.Until(() => scene.OnDisk.Contains("```mermaid\n%% kyfrev etapes\n%% Début\n%% Fin\nflowchart LR\n", StringComparison.Ordinal));
        UiThread.Until(() => scene.Script("document.querySelectorAll('.apercu-dessin img').length") == "1", TimeSpan.FromSeconds(25));
        Assert.AreEqual("false", scene.Script("[...document.querySelectorAll('.action-dessin')].find((b) => b.textContent === 'Modifier…').hidden"));

        scene.Script("[...document.querySelectorAll('.action-dessin')].find((b) => b.textContent === 'Modifier…').click()");
        UiThread.Until(() => scene.Editor.IsDiagramEditorOpen);
        var editor = scene.Editor.DiagramEditor!;
        Assert.AreEqual("etapes", editor.SelectedModel.Id);
        Assert.AreEqual("Fin", editor.Nodes[1].Text);
        editor.Nodes[1].Text = "Arrivée";
        scene.Editor.AnswerDiagramEditorCommand.Execute(true);
        UiThread.Until(() => scene.OnDisk.Contains("%% Arrivée\n", StringComparison.Ordinal));
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Count(scene.OnDisk, "```mermaid"), "Il est remplacé, pas ajouté.");

        // Retouché à la main, il ne se rouvre plus dans l'éditeur : son code serait écrasé.
        var retouched = KyfRev.Rendering.Diagrams.MermaidTemplates.Source("etapes", "A\nB")!.Value.Code + "\n    n1 --> n0";
        using var hand = new Scene($"Début\n\n```mermaid\n{retouched}\n```\n");
        hand.Window.Resources[MermaidEngine.ResourceKey] = engine;
        hand.Script("document.dispatchEvent(new Event('theme'))");
        UiThread.Until(() => hand.Script("document.querySelectorAll('.apercu-dessin img').length") == "1", TimeSpan.FromSeconds(25));
        Assert.AreEqual("\"none\"", hand.Script("getComputedStyle(document.querySelector('.bloc-dessin pre')).display"), "Dessiné, son code ne se voit pas encore.");
        Assert.AreEqual("true", hand.Script("[...document.querySelectorAll('.action-dessin')].find((b) => b.textContent === 'Modifier…').hidden"));
        var items = hand.Visual!.MenuItems(new MenuEvent(null, 0, false, false, false, false, 0, "mermaid", retouched), null).OfType<MenuItem>().Select(item => (string)item.Header).ToList();
        CollectionAssert.DoesNotContain(items, EditingText.EditDrawing);

        // Un double-clic mène à son code.
        hand.Script("document.querySelector('.bloc-dessin').querySelector('.apercu-dessin').dispatchEvent(new MouseEvent('dblclick', { bubbles: true }))");
        UiThread.Until(() => hand.Script("getComputedStyle(document.querySelector('.bloc-dessin pre')).display") != "\"none\"");
        Assert.IsFalse(hand.Editor.IsDiagramEditorOpen);
    }

    [EngineTest]
    public void A_trapped_smartart_shows_its_text_and_runs_nothing()
    {
        using var scene = new Scene("```smartart processus\n<img src=x onerror=\"window.piege=1\">\n<script>window.piege=2</script>\n```\n");
        UiThread.Until(() => scene.Script("document.querySelectorAll('.apercu-dessin svg text').length") == "2");
        Pump(TimeSpan.FromMilliseconds(300));

        Assert.AreEqual("0", scene.Script("document.querySelectorAll('.apercu-dessin img, .apercu-dessin script').length"));
        Assert.AreEqual("\"undefined\"", scene.Script("typeof window.piege"));
    }

    [EngineTest]
    public void A_code_block_in_another_language_does_not_draw()
    {
        using var scene = new Scene("```python\nprint(1)\n```\n\n```smartart inconnu\nx\n```\n");
        UiThread.Until(() => scene.Shown.Contains("print", StringComparison.Ordinal));
        Pump(TimeSpan.FromMilliseconds(400));

        Assert.AreEqual("0", scene.Script("document.querySelectorAll('.apercu-dessin svg').length"));
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void Every_template_draws_in_the_hidden_page(ThemeKind theme)
    {
        using var scene = new Scene();
        scene.Test.Design.Theme = theme;
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        var problems = new List<string>();
        foreach (var template in KyfRev.Rendering.Diagrams.MermaidTemplates.All)
        {
            // Avec le commentaire qui retient son modèle : c'est lui qui s'écrit dans la fiche.
            foreach (var code in new[] { KyfRev.Rendering.Diagrams.MermaidTemplates.Code(template.Id, template.Sample)!, KyfRev.Rendering.Diagrams.MermaidTemplates.Source(template.Id, template.Sample)!.Value.Code })
            {
                var drawing = engine.DrawAsync(code, MermaidEngine.ThemeOf(scene.SheetPage));
                UiThread.Until(() => drawing.IsCompleted, TimeSpan.FromSeconds(25));
                if (drawing.Result is not { } picture || picture.Png.Length < 500 || picture.Width < 10)
                {
                    problems.Add(template.Id);
                }
            }
        }

        Assert.AreEqual(string.Empty, string.Join(", ", problems));
    }

    [EngineTest]
    public void The_same_drawing_is_only_made_once_and_bad_code_draws_nothing()
    {
        using var scene = new Scene();
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        var theme = MermaidEngine.ThemeOf(scene.SheetPage);
        var first = engine.DrawAsync("flowchart LR\n  a --> b", theme);
        UiThread.Until(() => first.IsCompleted, TimeSpan.FromSeconds(25));
        var second = engine.DrawAsync("flowchart LR\n  a --> b", theme);

        Assert.IsTrue(second.IsCompleted);
        Assert.AreSame(first.Result, second.Result);

        var bad = engine.DrawAsync("ceci n'est pas un schéma", theme);
        UiThread.Until(() => bad.IsCompleted, TimeSpan.FromSeconds(25));
        Assert.IsNull(bad.Result);
    }

    [EngineTest]
    public void What_is_written_sits_on_the_lines_of_each_paper()
    {
        // Des capitales seulement : rien ne descend sous la ligne de base, que l'essai cherche dans l'image.
        const string body = "# MITOSE\n\nHIHI PREMIER PARAGRAPHE AVEC ASSEZ DE MOTS POUR PASSER À LA LIGNE SUIVANTE ET MONTRER DEUX LIGNES POSÉES SUR LE PAPIER, PUIS UNE TROISIÈME.\n\nHIHI SECOND PARAGRAPHE.\\\nLIGNE VENUE APRÈS MAJ+ENTRÉE.\n\n## PHASES\n\n- PROPHASE\n- ANAPHASE\n\n| Phase | Durée |\n| --- | --- |\n| Prophase | longue |\n| Métaphase | courte |\n\nTEXTE VENU SOUS LE TABLEAU.\n\n```\nligne de code\n```\n\nTEXTE VENU SOUS LE CODE.\n";
        using var scene = new Scene(body, width: 760);
        var visual = scene.Visual!;
        var ink = ((SolidColorBrush)scene.Test.Design.Dictionary["Kyf.Brush.Text"]).Color;
        var paper = ((SolidColorBrush)scene.Test.Design.Dictionary["Kyf.Brush.Surface"]).Color;
        (PaperStyle Style, PageFormat Format)[] cases =
        [
            (PaperStyle.LargeSquares, PageFormat.Endless), (PaperStyle.Lines, PageFormat.Endless), (PaperStyle.SmallSquares, PageFormat.Endless),
            (PaperStyle.Dots, PageFormat.Endless), (PaperStyle.Graph, PageFormat.Endless),
            (PaperStyle.LargeSquares, PageFormat.A5), (PaperStyle.Lines, PageFormat.A5), (PaperStyle.SmallSquares, PageFormat.A5),
            (PaperStyle.Dots, PageFormat.A5), (PaperStyle.Graph, PageFormat.A5),
        ];
        var astray = new List<string>();
        foreach (var (style, format) in cases)
        {
            var name = $"{PaperStyles.Name(style)} {PaperStyles.FormatName(format)}";
            var step = PaperStyles.LineStepPixels(style);
            visual.SetPage(new PageLayout(format));
            visual.SetPaper(PaperStyles.Name(style));
            UiThread.Until(() => scene.Script($"document.documentElement.classList.contains('sur-lignes') && document.documentElement.getAttribute('data-papier') === '{PaperStyles.Name(style)}'") == "true");
            // La mise en pages et le calage se mesurent après coup : on leur laisse le temps de se poser.
            Pump(TimeSpan.FromMilliseconds(900));
            // La flèche de repli d'un titre est posée dans la marge, là où l'essai cherche les traits du papier, et compte dans
            // les boîtes de son titre : elle sort de la mesure, sans rien déplacer (elle ne prend pas de place dans le texte).
            scene.Script("document.querySelectorAll('.ProseMirror button.repli').forEach((fleche) => { fleche.style.display = 'none'; })");
            var shot = visual.ShowSnapshotAsync();
            UiThread.Until(() => shot.IsCompleted);
            TestShell.Flush();
            var image = visual.Capture($"ecriture-{PaperStyles.Name(style)}-{PaperStyles.FormatName(format)}");
            var ratio = image.PixelWidth / double.Parse(scene.Script("innerWidth"), System.Globalization.CultureInfo.InvariantCulture);

            // Chaque ligne de chaque bloc de texte, là où elle est dans la page : haut, bas, gauche ; et le bloc d'où elle vient.
            var read = JsonSerializer.Deserialize<string>(scene.Script("""
                JSON.stringify([...document.querySelectorAll('.ProseMirror :is(h1, h2, p)')].filter((bloc) => !bloc.closest('table, pre')).map((bloc) => {
                  const part = document.createRange();
                  part.selectNodeContents(bloc);
                  const tops = [];
                  for (const box of part.getClientRects()) {
                    if (box.width > 2 && !tops.some((seen) => Math.abs(seen[0] - box.top) < 3)) tops.push([box.top, box.bottom, box.left]);
                  }
                  return { text: bloc.textContent.slice(0, 18), lines: tops };
                }))
                """))!;
            using var parsed = JsonDocument.Parse(read);
            var blocks = parsed.RootElement.EnumerateArray()
                .Select(block => (Text: block.GetProperty("text").GetString()!, Lines: block.GetProperty("lines").EnumerateArray().Select(line => line.EnumerateArray().Select(value => value.GetDouble()).ToArray()).ToList()))
                .ToList();
            Assert.IsGreaterThanOrEqualTo(8, blocks.Count, name);
            var left = blocks[0].Lines[0][2] * ratio;
            var ruled = image.RuledRows(left - 30, left - 12, paper);
            Assert.IsGreaterThan(5, ruled.Count, $"{name} : le papier montre ses lignes");
            var measured = 0;
            foreach (var block in blocks)
            {
                foreach (var line in block.Lines)
                {
                    var (top, bottom, start) = (line[0] * ratio, line[1] * ratio, line[2] * ratio);
                    if (bottom > image.PixelHeight - 4 || image.LowestInk(top - 2, bottom, start, start + 170, ink) is not { } row)
                    {
                        continue;
                    }
                    measured++;
                    // Le trait est juste sous la ligne de base : la rangée qui suit le bas des lettres, à une près.
                    var nearest = ruled.MinBy(candidate => Math.Abs(candidate - (row + 1)));
                    if (Math.Abs(nearest - (row + 1)) > 2)
                    {
                        astray.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{name} « {block.Text} » : bas des lettres {row + 1}, trait le plus proche {nearest}"));
                    }
                }
            }
            Assert.IsGreaterThanOrEqualTo(6, measured, $"{name} : des lignes de texte ont été regardées");

            // Entrée laisse une ligne vide, Maj+Entrée n'en laisse pas : les écarts se comptent en pas.
            var first = blocks.First(block => block.Text.StartsWith("HIHI PREMIER", StringComparison.Ordinal)).Lines;
            var second = blocks.First(block => block.Text.StartsWith("HIHI SECOND", StringComparison.Ordinal)).Lines;
            Assert.IsGreaterThanOrEqualTo(2, first.Count, name);
            Assert.AreEqual(step, first[1][0] - first[0][0], 0.6, $"{name} : deux lignes d'un paragraphe");
            if (Math.Abs(second[0][0] - first[^1][0]) < 400)
            {
                // Sauf si une page les sépare.
                Assert.AreEqual(2 * step, second[0][0] - first[^1][0], 0.6, $"{name} : un nouveau paragraphe laisse une ligne vide");
            }
            Assert.AreEqual(step, second[1][0] - second[0][0], 0.6, $"{name} : Maj+Entrée passe à la ligne du dessous");

            // Un tableau a un fond plein : la réglure ne passe pas à travers ses cases.
            Assert.AreEqual("true", scene.Script("(() => { const fond = getComputedStyle(document.querySelector('.ProseMirror td')).backgroundColor; const parts = fond.match(/[\\d.]+/g) || []; return fond !== 'transparent' && (parts.length < 4 || Number(parts[3]) === 1); })()"), $"{name} : le fond des cases");
        }
        Assert.IsEmpty(astray, $"Des lignes écrites ne sont pas posées sur le papier : {string.Join(" ; ", astray)}");

        // La police des fiches arrive à la page avec le thème, et le texte reste posé sur les lignes avec elle. Une police
        // manuscrite ne pose pas toutes ses lettres sur la ligne de base : on lui laisse un peu de jeu.
        visual.SetPage(PageLayout.Default);
        visual.SetPaper(PaperStyles.Name(PaperStyle.Lines));
        foreach (var (font, family, play) in new[] { (ReadingFont.Serif, "Georgia", 2d), (ReadingFont.Typewriter, "Mono", 2d), (ReadingFont.Handwriting, "Segoe Print", 4d) })
        {
            scene.Window.Resources[ReadingComfort.ResourceKey] = ReadingComfort.Of(new MachinePreferences { Font = font });
            UiThread.Until(() => scene.Script("getComputedStyle(document.querySelector('.ProseMirror p')).fontFamily").Contains(family, StringComparison.Ordinal));
            StringAssert.Contains(scene.Script("getComputedStyle(document.querySelector('.ProseMirror h1')).fontFamily"), family, StringComparison.Ordinal, "les titres prennent la police choisie");
            Pump(TimeSpan.FromMilliseconds(900));
            // La flèche de repli d'un titre est posée dans la marge, là où l'essai cherche les traits du papier, et compte dans
            // les boîtes de son titre : elle sort de la mesure, sans rien déplacer (elle ne prend pas de place dans le texte).
            scene.Script("document.querySelectorAll('.ProseMirror button.repli').forEach((fleche) => { fleche.style.display = 'none'; })");
            var shot = visual.ShowSnapshotAsync();
            UiThread.Until(() => shot.IsCompleted);
            TestShell.Flush();
            var image = visual.Capture($"ecriture-lignes-{font}");
            var ratio = image.PixelWidth / double.Parse(scene.Script("innerWidth"), System.Globalization.CultureInfo.InvariantCulture);
            var box = JsonSerializer.Deserialize<double[]>(JsonSerializer.Deserialize<string>(scene.Script("""
                (() => {
                  const bloc = [...document.querySelectorAll('.ProseMirror p')].find((p) => p.textContent.startsWith('HIHI PREMIER'));
                  const part = document.createRange();
                  part.selectNodeContents(bloc);
                  const box = part.getClientRects()[0];
                  return JSON.stringify([box.top, box.bottom, box.left]);
                })()
                """))!)!;
            var ruled = image.RuledRows((box[2] * ratio) - 30, (box[2] * ratio) - 12, paper);
            Assert.IsGreaterThan(5, ruled.Count, font.ToString());
            // Les premières lettres seulement, des « H » et des « I » : rien n'y descend sous la ligne.
            var row = image.LowestInk((box[0] * ratio) - 2, box[1] * ratio, box[2] * ratio, (box[2] * ratio) + 34, ink);
            Assert.IsNotNull(row, font.ToString());
            Assert.AreEqual(ruled.MinBy(candidate => Math.Abs(candidate - (row.Value + 1))), row.Value + 1, play, $"{font} : le bas des premières lettres est sur une ligne");
        }
        scene.Window.Resources.Remove(ReadingComfort.ResourceKey);

        // Sans papier réglé, plus rien ne cale le texte.
        visual.SetPaper("uni");
        UiThread.Until(() => scene.Script("document.documentElement.classList.contains('sur-lignes')") == "false");
        visual.SetPaper(null);
        visual.SetPage(PageLayout.Default);
    }

    [EngineTest]
    public void A_trapped_schema_is_only_a_picture()
    {
        using var scene = new Scene();
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        var code = "flowchart LR\n  a[\"<img src=x onerror=window.piege=1>\"] --> b\n  click a href \"javascript:window.piege=2\"\n  click b call piege()";
        var drawing = engine.DrawAsync(code, MermaidEngine.ThemeOf(scene.SheetPage));
        UiThread.Until(() => drawing.IsCompleted, TimeSpan.FromSeconds(25));

        var check = engine.RunScriptAsync("typeof window.piege");
        UiThread.Until(() => check.IsCompleted);
        Assert.AreEqual("\"undefined\"", check.Result);
    }

    [EngineTest]
    public void An_svg_image_becomes_a_picture_and_nothing_in_it_acts()
    {
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        const string trapped = """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 120 60" onload="window.piege = 1">
              <script>window.piege = 2;</script>
              <image href="https://exemple.invalid/a.png" width="10" height="10" onerror="window.piege = 3" />
              <a xlink:href="javascript:window.piege = 4"><rect width="120" height="60" fill="#3a7" /></a>
            </svg>
            """;

        var drawing = engine.DrawSvgAsync(System.Text.Encoding.UTF8.GetBytes(trapped));
        UiThread.Until(() => drawing.IsCompleted, TimeSpan.FromSeconds(25));

        Assert.IsNotNull(drawing.Result, "le dessin reste une image");
        Assert.AreEqual(120, drawing.Result.Width);
        Assert.AreEqual(60, drawing.Result.Height);
        Assert.IsGreaterThan(0, drawing.Result.Png.Length);
        var check = engine.RunScriptAsync("typeof window.piege");
        UiThread.Until(() => check.IsCompleted);
        Assert.AreEqual("\"undefined\"", check.Result, "ni script, ni attribut, ni lien n'a agi");
    }

    [EngineTest]
    [DataRow("""<svg xmlns="http://www.w3.org/2000/svg" width="210mm" height="297mm" viewBox="0 0 21000 29700"><rect width="21000" height="29700" /></svg>""", 794, 1123)]
    [DataRow("""<svg xmlns="http://www.w3.org/2000/svg" width="600" height="300" viewBox="0 0 1 1"><rect width="1" height="1" /></svg>""", 600, 300)]
    [DataRow("""<svg xmlns="http://www.w3.org/2000/svg" width="210mm" height="99mm"><rect width="10" height="10" /></svg>""", 794, 375)]
    [DataRow("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 60" width="100%"><rect width="1" height="1" /></svg>""", 120, 60)]
    [DataRow("""<svg xmlns="http://www.w3.org/2000/svg" width="200" viewBox="0 0 100 50"><rect width="1" height="1" /></svg>""", 200, 100)]
    [DataRow("""<svg xmlns="http://www.w3.org/2000/svg"><rect width="1" height="1" /></svg>""", 640, 400)]
    [DataRow("""<svg xmlns="http://www.w3.org/2000/svg" width="40000" height="30000"><rect width="1" height="1" /></svg>""", 40000, 30000)]
    public void An_svg_keeps_the_size_it_declares(string svg, int width, int height)
    {
        using var engine = new MermaidEngine(FakeEnvironment.WebData);

        var drawing = engine.DrawSvgAsync(System.Text.Encoding.UTF8.GetBytes(svg));
        UiThread.Until(() => drawing.IsCompleted, TimeSpan.FromSeconds(25));

        Assert.IsNotNull(drawing.Result, svg);
        Assert.AreEqual(width, drawing.Result.Width);
        Assert.AreEqual(height, drawing.Result.Height);
        Assert.IsGreaterThan(0, drawing.Result.Png.Length);
    }

    [EngineTest]
    public void An_svg_keeps_its_style_and_its_encoding()
    {
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        var latin = System.Text.Encoding.Latin1.GetBytes("""<?xml version="1.0" encoding="ISO-8859-1"?><svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" style="fill:none"><title>été</title><rect width="1" height="1" /></svg>""");
        var unicode = System.Text.Encoding.Unicode.GetPreamble().Concat(System.Text.Encoding.Unicode.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><title>été</title></svg>""")).ToArray();

        var drawing = engine.DrawSvgAsync(latin);
        UiThread.Until(() => drawing.IsCompleted, TimeSpan.FromSeconds(25));
        var wide = engine.DrawSvgAsync(unicode);
        UiThread.Until(() => wide.IsCompleted, TimeSpan.FromSeconds(25));

        Assert.IsNotNull(wide.Result, "UTF-16");
        var source = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(drawing.Result!.Image[(drawing.Result.Image.IndexOf(',', StringComparison.Ordinal) + 1)..]));
        StringAssert.Contains(source, "été", StringComparison.Ordinal);
        StringAssert.Contains(source, "fill:none", StringComparison.Ordinal);
    }

    [EngineTest]
    public void A_start_that_takes_too_long_lets_the_next_drawing_try_again()
    {
        using var scene = new Scene();
        using var engine = new MermaidEngine(FakeEnvironment.WebData) { Patience = TimeSpan.Zero };
        var theme = MermaidEngine.ThemeOf(scene.SheetPage);

        var cut = engine.DrawAsync("flowchart LR\n  a --> b", theme);
        UiThread.Until(() => cut.IsCompleted, TimeSpan.FromSeconds(25));
        Assert.IsFalse(cut.IsFaulted, "trop lent n'est pas une panne");
        Assert.IsNull(cut.Result);

        engine.Patience = TimeSpan.FromSeconds(20);
        var next = engine.DrawAsync("flowchart LR\n  c --> d", theme);
        UiThread.Until(() => next.IsCompleted, TimeSpan.FromSeconds(25));
        Assert.IsNotNull(next.Result);
    }

    [EngineTest]
    public void What_is_not_an_svg_is_not_a_picture()
    {
        using var engine = new MermaidEngine(FakeEnvironment.WebData);

        var broken = engine.DrawSvgAsync(System.Text.Encoding.UTF8.GetBytes("ceci n'est pas un dessin"));
        UiThread.Until(() => broken.IsCompleted, TimeSpan.FromSeconds(25));

        Assert.IsNull(broken.Result);
    }

    [TestMethod]
    public async Task An_svg_too_big_or_for_a_closed_engine_is_not_drawn()
    {
        var engine = new MermaidEngine(FakeEnvironment.WebData);

        Assert.IsNull(await engine.DrawSvgAsync(new byte[(5 * 1024 * 1024) + 1]));

        engine.Dispose();
        Assert.IsNull(await engine.DrawSvgAsync(new byte[10]));
    }

    [EngineTest]
    public void A_drawing_that_never_ends_stops_the_page_and_the_next_one_starts_it_again()
    {
        using var scene = new Scene();
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        var theme = MermaidEngine.ThemeOf(scene.SheetPage);
        var first = engine.DrawAsync("flowchart LR\n  a --> b", theme);
        UiThread.Until(() => first.IsCompleted, TimeSpan.FromSeconds(25));

        engine.Patience = TimeSpan.Zero;
        var cut = engine.DrawAsync("flowchart LR\n  c --> d", theme);
        UiThread.Until(() => cut.IsCompleted);
        Assert.IsNull(cut.Result);

        engine.Patience = TimeSpan.FromSeconds(20);
        var next = engine.DrawAsync("flowchart LR\n  e --> f", theme);
        UiThread.Until(() => next.IsCompleted, TimeSpan.FromSeconds(25));
        Assert.IsNotNull(next.Result);
    }

    [EngineTest]
    public void A_closed_engine_draws_nothing()
    {
        var engine = new MermaidEngine(FakeEnvironment.WebData);
        engine.Dispose();

        var drawing = engine.DrawAsync("flowchart LR\n  a --> b", new Dictionary<string, string>());

        Assert.IsTrue(drawing.IsCompleted);
        Assert.IsNull(drawing.Result);
    }

    [EngineTest]
    public void A_schema_shows_its_picture_in_the_text_and_its_code_when_it_cannot_be_drawn()
    {
        using var scene = new Scene("Début\n\n```mermaid\nflowchart LR\n  a --> b\n```\n\nFin\n");
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        scene.Window.Resources[MermaidEngine.ResourceKey] = engine;
        scene.Script("document.dispatchEvent(new Event('theme'))");

        UiThread.Until(() => scene.Script("document.querySelectorAll('.apercu-dessin img').length") == "1", TimeSpan.FromSeconds(25));
        Assert.AreEqual("\"none\"", scene.Script("getComputedStyle(document.querySelector('.bloc-dessin pre')).display"));
        Assert.StartsWith("\"data:image/svg+xml", scene.Script("document.querySelector('.apercu-dessin img').src"));
    }

    [EngineTest]
    public void Without_the_hidden_page_a_schema_is_read_as_code()
    {
        using var scene = new Scene("```mermaid\nflowchart LR\n  a --> b\n```\n");

        UiThread.Until(() => scene.Script("document.querySelectorAll('.dessin-impossible').length") == "1");
        Assert.AreNotEqual("\"none\"", scene.Script("getComputedStyle(document.querySelector('.bloc-dessin pre')).display"));
    }

    [EngineTest]
    public void The_reading_shows_the_picture_of_a_schema()
    {
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        var test = new TestShell();
        var viewer = new KyfRev.Presentation.Wpf.Reading.SheetDocumentViewer();
        var reading = SheetReading.Of(new OpenedSheet("f_1", "Mitose", "fiches/Biologie/Mitose.md", "Biologie", [], "```mermaid\nflowchart LR\n  a --> b\n```\n\n```mermaid\nceci n'est pas un schéma\n```\n", 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []), _ => Outcome.Refused<byte[]>("non"));
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 900, Height = 700, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Resources[MermaidEngine.ResourceKey] = engine;
        window.Show();
        try
        {
            viewer.Reading = reading;
            TestShell.Flush();
            var views = new List<KyfRev.Presentation.Wpf.Reading.MermaidView>();
            UiThread.Until(() =>
            {
                views = [.. LogicalDocumentViews(viewer)];
                return views.Count == 2 && views[0].ShowsPicture;
            }, TimeSpan.FromSeconds(25));
            UiThread.Until(() => views[1].IsLoaded, TimeSpan.FromSeconds(25));
            Pump(TimeSpan.FromSeconds(2));
            Assert.IsTrue(views[1].ShowsCode, "Ce qui ne se dessine pas se lit en code.");
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<KyfRev.Presentation.Wpf.Reading.MermaidView> LogicalDocumentViews(KyfRev.Presentation.Wpf.Reading.SheetDocumentViewer viewer) =>
        viewer.Document?.Blocks.OfType<System.Windows.Documents.BlockUIContainer>().Select(block => block.Child).OfType<KyfRev.Presentation.Wpf.Reading.MermaidView>() ?? [];

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

    private static readonly string[] ImageMenu = [EditingText.Cut, EditingText.Copy, EditingText.Paste, ImageMaskText.Action, EditingText.ImageCrop, EditingText.DeleteImage];

    [EngineTest]
    public void A_right_click_on_an_image_offers_to_mask_zones_and_the_title_is_written()
    {
        using var scene = new Scene("Une image.\n");
        UiThread.Until(() => scene.Shown.Contains("Une image.", StringComparison.Ordinal));
        scene.Script("document.querySelector('.ProseMirror').focus()");
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAEBgIApD5fRAAAAABJRU5ErkJggg==");
        var inserted = scene.Editor.InsertImageAsync("point.png", png);
        UiThread.Until(() => inserted.IsCompleted);
        UiThread.Until(() => scene.Script("document.querySelector('.ProseMirror .image img') !== null") == "true");
        var visual = scene.Visual!;
        ImageMasksQuestion? asked = null;
        visual.ImageMasksAsked += (_, question) => asked = question;

        // Le vrai clic droit, dans la page, sur l'image.
        scene.Script("document.querySelector('.ProseMirror .image img').dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 5, clientY: 5 }))");
        UiThread.Until(() => visual.LastMenuItems is { Count: > 0 });
        var menu = visual.LastMenuItems!;
        ((ContextMenu)menu[0].Parent).IsOpen = false;
        // Un menu court, comme celui d'une fiche : couper, copier, coller, puis ce qui règle l'image.
        CollectionAssert.AreEqual(
            ImageMenu,
            menu.OfType<MenuItem>().Select(item => (string)item.Header).ToList());
        Assert.IsInstanceOfType<Separator>(menu[3]);

        ((MenuItem)menu[4]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.IsNotNull(asked);
        Assert.IsNull(asked.Title);
        StringAssert.EndsWith(asked.Path, "point.png", StringComparison.Ordinal);
        Assert.IsTrue(scene.Editor.ReadResource(asked.Path).Succeeded, "le chemin rendu est celui du fichier dans le coffre");

        // Le titre rendu par la fenêtre des masques s'écrit dans la fiche, puis s'en retire.
        visual.SetImageTitle(asked.Position, "kyfrev-masques:10,20,30,15");
        UiThread.Until(() => scene.OnDisk.Contains("point.png \"kyfrev-masques:10,20,30,15\")", StringComparison.Ordinal));
        visual.SetImageTitle(asked.Position, null);
        UiThread.Until(() => !scene.OnDisk.Contains("kyfrev-masques", StringComparison.Ordinal) && scene.OnDisk.Contains("![point](", StringComparison.Ordinal));

        // « Supprimer l'image », au bas du menu : elle s'en va, le reste du texte ne bouge pas.
        ((MenuItem)menu[^1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        UiThread.Until(() => !scene.OnDisk.Contains("![point](", StringComparison.Ordinal) && scene.OnDisk.Contains("Une image.", StringComparison.Ordinal));

        // Le menu du texte montre la mise en forme déjà là, et propose de supprimer le bloc de code où est le curseur.
        var formatted = visual.MenuItems(new MenuEvent("mot", 0, true, false, false, false, Bold: true, Strike: true), null).OfType<MenuItem>().ToList();
        Assert.IsTrue(formatted.Single(item => (string)item.Header == EditingText.Bold).IsChecked);
        Assert.IsTrue(formatted.Single(item => (string)item.Header == EditingText.Strike).IsChecked);
        Assert.IsFalse(formatted.Single(item => (string)item.Header == EditingText.Italic).IsChecked);
        var inCode = visual.MenuItems(new MenuEvent(null, 0, false, true, false, false, CodePosition: 12), null).OfType<MenuItem>().Select(item => (string)item.Header).ToList();
        CollectionAssert.Contains(inCode, EditingText.DeleteCode);
        CollectionAssert.DoesNotContain(formatted.Select(item => (string)item.Header).ToList(), EditingText.DeleteCode);

        // Une image qui ne vient pas du coffre n'a pas de fichier à masquer.
        var elsewhere = visual.MenuItems(new MenuEvent(null, 0, false, false, false, false, ImagePosition: 3, ImageAddress: "data:image/png;base64,AAAA"), null).OfType<MenuItem>().Select(item => (string)item.Header).ToList();
        CollectionAssert.DoesNotContain(elsewhere, ImageMaskText.Action);
    }

    [EngineTest]
    public void The_titles_have_the_sizes_of_a_word_processor_and_the_text_stays_a_centred_column()
    {
        using var scene = new Scene("# Un\n\nTexte.\n\n## Deux\n\n### Trois\n\n#### Quatre\n\n##### Cinq\n\nFin.\n");
        UiThread.Until(() => scene.Shown.Contains("Fin.", StringComparison.Ordinal));
        var visual = scene.Visual!;
        double Size(string selector) => double.Parse(scene.Script($"parseFloat(getComputedStyle(document.querySelector('.ProseMirror {selector}')).fontSize)"), System.Globalization.CultureInfo.InvariantCulture);
        string Style(string selector) => scene.Script($"getComputedStyle(document.querySelector('.ProseMirror {selector}')).fontStyle").Trim('"');

        // Le corps vaut 12 points (16 px), les titres 20, 16 et 14, puis la taille du corps : le titre 4 en italique.
        UiThread.Until(() => Math.Abs(Size("p") - 16) < 0.05);
        Assert.AreEqual(ReadingComfort.Pixels(20), Size("h1"), 0.1);
        Assert.AreEqual(ReadingComfort.Pixels(16), Size("h2"), 0.1);
        Assert.AreEqual(ReadingComfort.Pixels(14), Size("h3"), 0.1);
        Assert.AreEqual(16, Size("h4"), 0.1);
        Assert.AreEqual(16, Size("h5"), 0.1);
        Assert.AreEqual("italic", Style("h4"));
        Assert.AreEqual("normal", Style("h5"));
        Assert.AreEqual("normal", Style("h1"));

        // Le ruban dit la taille du titre où est le curseur, en points.
        scene.Script("(() => { const titre = document.querySelector('.ProseMirror h1'); const selection = getSelection(); selection.selectAllChildren(titre); selection.collapseToEnd(); document.querySelector('.ProseMirror').focus(); })()");
        UiThread.Until(() => scene.Editor.Tools?.Heading == 1);
        Assert.AreEqual("20", scene.Editor.SizeLabel);

        // La vue est plus large que le texte : la feuille sans fin reste une colonne, au milieu.
        visual.TextWidth = 320;
        UiThread.Until(() => scene.Script("getComputedStyle(document.documentElement).getPropertyValue('--largeur-fiche').trim()").Trim('"') == "320px");
        UiThread.Until(() => double.Parse(scene.Script("document.getElementById('fiche').getBoundingClientRect().width"), System.Globalization.CultureInfo.InvariantCulture) <= 320.5);
        var view = double.Parse(scene.Script("document.documentElement.clientWidth"), System.Globalization.CultureInfo.InvariantCulture);
        var left = double.Parse(scene.Script("document.getElementById('fiche').getBoundingClientRect().left"), System.Globalization.CultureInfo.InvariantCulture);
        var width = double.Parse(scene.Script("document.getElementById('fiche').getBoundingClientRect().width"), System.Globalization.CultureInfo.InvariantCulture);
        if (view > 360)
        {
            Assert.AreEqual((view - width) / 2, left, 12, "centrée dans la vue");
        }
        Assert.AreEqual("false", scene.Script("document.documentElement.scrollWidth > document.documentElement.clientWidth + 1"), "sans défilement horizontal");

        // Sans largeur donnée, la feuille reprend toute la vue.
        visual.TextWidth = double.PositiveInfinity;
        UiThread.Until(() => scene.Script("getComputedStyle(document.documentElement).getPropertyValue('--largeur-fiche').trim()").Trim('"') == "none");
    }

    [EngineTest]
    public void A_section_folds_under_its_title_and_the_file_does_not_change()
    {
        using var scene = new Scene("# Un\n\nSous un.\n\n## Deux\n\nSous deux.\n\n# Trois\n\nSous trois.\n");
        UiThread.Until(() => scene.Shown.Contains("Sous trois.", StringComparison.Ordinal));
        scene.Script("document.querySelector('.ProseMirror').focus()");
        UiThread.Until(() => scene.Script("document.querySelectorAll('.ProseMirror button.repli').length") == "3");
        var before = scene.OnDisk;
        string Hidden() => scene.Script("[...document.querySelectorAll('.ProseMirror > *')].filter((bloc) => getComputedStyle(bloc).display === 'none').map((bloc) => bloc.textContent).join('|')").Trim('"');
        Assert.AreEqual(string.Empty, Hidden());
        Assert.AreEqual("false", scene.Script("document.querySelector('.ProseMirror h1 button.repli').getAttribute('aria-expanded') === 'false'"));

        // Un clic sur la flèche du premier titre : tout ce qui est sous lui se replie, le titre 2 et son texte compris,
        // jusqu'au titre de même niveau qui suit.
        scene.Script("document.querySelector('.ProseMirror h1 button.repli').click()");
        UiThread.Until(() => Hidden().Contains("Sous deux.", StringComparison.Ordinal));
        StringAssert.Contains(Hidden(), "Sous un.", StringComparison.Ordinal);
        StringAssert.Contains(Hidden(), "Deux", StringComparison.Ordinal);
        Assert.IsFalse(Hidden().Contains("Trois", StringComparison.Ordinal), "le titre suivant de même niveau reste");
        Assert.AreEqual("true", scene.Script("document.querySelector('.ProseMirror h1 button.repli').getAttribute('aria-expanded') === 'false'"));
        StringAssert.Contains(scene.Script("document.querySelector('.ProseMirror h1 button.repli').getAttribute('aria-label')"), "Déplier", StringComparison.Ordinal);

        // Rien n'est écrit dans la fiche : replier ne change pas le texte.
        TestShell.Flush();
        Assert.AreEqual(before, scene.OnDisk);

        // Un second clic déplie.
        scene.Script("document.querySelector('.ProseMirror h1 button.repli').click()");
        UiThread.Until(() => Hidden().Length == 0);
        Assert.AreEqual(before, scene.OnDisk);
    }

    [EngineTest]
    public void A_right_click_on_a_page_break_or_a_blank_page_offers_to_delete_it()
    {
        using var scene = new Scene("Avant.\n\n<!-- kyfrev saut-de-page -->\n\nMilieu.\n\n<!-- kyfrev page-blanche -->\n\nAprès.\n");
        UiThread.Until(() => scene.Shown.Contains("Milieu.", StringComparison.Ordinal));
        UiThread.Until(() => scene.Script("document.querySelectorAll('.ProseMirror .marque-de-page').length") == "2");
        scene.Script("document.querySelector('.ProseMirror').focus()");
        var visual = scene.Visual!;

        // Le vrai clic droit, dans la page, sur la marque du saut : le menu ne propose que de la supprimer.
        void RightClick(string kind) =>
            scene.Script($"document.querySelector('.ProseMirror .marque-de-page[data-page=\"{kind}\"]').dispatchEvent(new MouseEvent('contextmenu', {{ bubbles: true, cancelable: true, clientX: 5, clientY: 5 }}))");
        RightClick("saut");
        UiThread.Until(() => visual.LastMenuItems is { Count: > 0 });
        var menu = visual.LastMenuItems!;
        ((ContextMenu)menu[0].Parent).IsOpen = false;
        CollectionAssert.AreEqual(new[] { EditingText.DeletePageBreak }, menu.OfType<MenuItem>().Select(item => (string)item.Header).ToList());
        ((MenuItem)menu[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        UiThread.Until(() => !scene.OnDisk.Contains("saut-de-page", StringComparison.Ordinal));
        StringAssert.Contains(scene.OnDisk, "page-blanche", StringComparison.Ordinal, "l'autre marque reste");
        StringAssert.Contains(scene.OnDisk, "Avant.", StringComparison.Ordinal);
        StringAssert.Contains(scene.OnDisk, "Milieu.", StringComparison.Ordinal);

        // La page blanche, de même, sous son nom.
        RightClick("blanche");
        UiThread.Until(() => visual.LastMenuItems is { Count: 1 } items && (string)((MenuItem)items[0]).Header == EditingText.DeleteBlankPage);
        menu = visual.LastMenuItems!;
        ((ContextMenu)menu[0].Parent).IsOpen = false;
        ((MenuItem)menu[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        UiThread.Until(() => !scene.OnDisk.Contains("kyfrev", StringComparison.Ordinal));
        StringAssert.Contains(scene.OnDisk, "Après.", StringComparison.Ordinal, "le texte autour ne bouge pas");

        // Annuler remet la marque d'un coup.
        scene.Editor.UndoCommand.Execute(null);
        UiThread.Until(() => scene.OnDisk.Contains("page-blanche", StringComparison.Ordinal));

        // Ailleurs dans le texte, le menu reste celui du texte.
        var text = visual.MenuItems(new MenuEvent("mot", 0, true, false, false, false), null).OfType<MenuItem>().Select(item => (string)item.Header).ToList();
        CollectionAssert.DoesNotContain(text, EditingText.DeletePageBreak);
        CollectionAssert.Contains(text, EditingText.Cut);
    }

    [EngineTest]
    public void An_image_chosen_in_the_text_is_sized_placed_and_framed_from_its_tab()
    {
        using var scene = new Scene("Une image.\n");
        UiThread.Until(() => scene.Shown.Contains("Une image.", StringComparison.Ordinal));
        scene.Script("document.querySelector('.ProseMirror').focus()");
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAEBgIApD5fRAAAAABJRU5ErkJggg==");
        var inserted = scene.Editor.InsertImageAsync("point.png", png);
        UiThread.Until(() => inserted.IsCompleted);
        UiThread.Until(() => scene.Script("document.querySelector('.ProseMirror .image img') !== null") == "true");
        Assert.IsFalse(scene.Editor.IsOnImage, "le curseur dans le texte : pas d'onglet Image");

        // Le clic droit choisit l'image : l'onglet Image la décrit.
        scene.Script("document.querySelector('.ProseMirror .image img').dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 5, clientY: 5 }))");
        UiThread.Until(() => scene.Visual!.LastMenuItems is { Count: > 0 });
        ((ContextMenu)scene.Visual!.LastMenuItems![0].Parent).IsOpen = false;
        UiThread.Until(() => scene.Editor.IsOnImage);
        var image = scene.Editor.Tools!.Image!;
        Assert.IsTrue(image.Editable);
        Assert.IsTrue(image.IsNatural);
        Assert.AreEqual("fil", image.Align);
        Assert.AreEqual("point", image.Alt);
        Assert.IsTrue(scene.Editor.CanChangeImage);
        StringAssert.EndsWith(VisualEditor.VaultPathOf(image.Address), "point.png", StringComparison.Ordinal);

        // Choisie, elle a ses poignées : quatre coins et deux côtés.
        Assert.AreEqual("6", scene.Script("document.querySelectorAll('.ProseMirror .image.ProseMirror-selectednode .poignee').length"));
        Assert.AreEqual("\"block\"", scene.Script("getComputedStyle(document.querySelector('.ProseMirror .image .poignee')).display"));

        scene.Editor.ChangeImageCommand.Execute("width=50");
        UiThread.Until(() => scene.Editor.Tools?.Image?.Width == 50);
        // Sa taille à l'écran arrive avec elle : le ruban la dit en centimètres, et une largeur tapée la change.
        UiThread.Until(() => scene.Editor.Tools?.Image is { ShownWidth: > 0, Room: > 0 });
        StringAssert.EndsWith(scene.Editor.ImageWidthCm, " cm", StringComparison.Ordinal);
        var room = scene.Editor.Tools!.Image!.Room;
        scene.Editor.ImageWidthCm = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{room * 0.3 / KyfRev.Presentation.Wpf.Editing.ImageState.PixelsPerCentimetre:0.00}");
        UiThread.Until(() => scene.Editor.Tools?.Image?.Width == 30);
        scene.Editor.ChangeImageCommand.Execute("width=50");
        UiThread.Until(() => scene.Editor.Tools?.Image?.Width == 50);
        UiThread.Until(() => scene.OnDisk.Contains("point.png \"kyfrev-largeur:50\")", StringComparison.Ordinal));
        Assert.AreEqual("\"50%\"", scene.Script("document.querySelector('.ProseMirror .image').style.width"));
        Assert.IsTrue(scene.Editor.IsOnImage, "l'image reste choisie après un réglage");

        scene.Editor.NudgeImageWidthCommand.Execute("5");
        UiThread.Until(() => scene.Editor.Tools?.Image?.Width == 55);
        scene.Editor.ChangeImageCommand.Execute("align=centre");
        UiThread.Until(() => scene.Editor.Tools?.Image?.Align == "centre");
        Assert.AreEqual("\"block\"", scene.Script("document.querySelector('.ProseMirror .image').style.display"));
        scene.Editor.ChangeImageCommand.Execute("crop=10,10,50,50");
        UiThread.Until(() => scene.Editor.Tools?.Image?.Crop == "10,10,50,50");
        UiThread.Until(() => scene.OnDisk.Contains("\"kyfrev-largeur:55 kyfrev-position:centre kyfrev-cadre:10,10,50,50\"", StringComparison.Ordinal));
        Assert.AreEqual("\"hidden\"", scene.Script("document.querySelector('.ProseMirror .image').style.overflow"));

        // Les masques posés ensuite gardent ces réglages ; « Rétablir » ne retire pas les masques.
        scene.Visual!.SetImageTitle(image.Position, KyfRev.Rendering.ImageLooks.Change(scene.Editor.Tools!.Image!.Title, look => look with { Masks = [new KyfRev.Rendering.ImageMask(0.1, 0.1, 0.2, 0.2)] }).Title);
        UiThread.Until(() => scene.Editor.Tools?.Image?.Masks == 1);
        Assert.AreEqual("1 zone masquée", scene.Editor.Tools!.Image!.MasksLabel);
        scene.Editor.ChangeImageCommand.Execute("reset");
        UiThread.Until(() => scene.OnDisk.Contains("point.png \"kyfrev-masques:10,10,20,20\")", StringComparison.Ordinal));

        // Sa description.
        scene.Editor.ImageAlt = "Un point rouge";
        UiThread.Until(() => scene.OnDisk.Contains("![Un point rouge](", StringComparison.Ordinal));

        // « Masquer des zones… » et « Rogner… » demandent leur fenêtre, avec le fichier de l'image.
        var asked = new List<ImageMasksQuestion>();
        scene.Editor.ImageDialogAsked += (_, question) => asked.Add(question);
        scene.Editor.MaskImageCommand.Execute(null);
        scene.Editor.CropImageCommand.Execute(null);
        Assert.HasCount(2, asked);
        Assert.IsFalse(asked[0].Crop);
        Assert.IsTrue(asked[1].Crop);
        StringAssert.EndsWith(asked[1].Path, "point.png", StringComparison.Ordinal);
    }

    [EngineTest]
    public void A_right_click_on_a_drawing_offers_to_remove_it_and_ctrl_z_brings_it_back()
    {
        using var scene = new Scene("Avant\n\n```smartart cycle\nUne\nDeux\n```\n\n```mermaid\nflowchart TD\n  A --> B\n```\n\n```mermaid\npie\n  \"a\" : 1\n```\n\nAprès\n");
        UiThread.Until(() => scene.Script("document.querySelectorAll('.bloc-dessin').length") == "3");
        var visual = scene.Visual!;

        // Le vrai clic droit, dans la page : il vise le dessin, sans entrer dans son code.
        scene.Script("document.querySelector('.bloc-dessin').dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 5, clientY: 5 }))");
        UiThread.Until(() => visual.LastMenuItems is { Count: > 0 });
        var menu = visual.LastMenuItems!;
        ((ContextMenu)menu[0].Parent).IsOpen = false;
        var headers = menu.OfType<MenuItem>().Select(item => (string)item.Header).ToList();
        CollectionAssert.AreEqual(new[] { EditingText.EditDrawing, EditingText.ShowDrawingCode, EditingText.DeleteSmartArt }, headers);
        Assert.AreEqual("\"none\"", scene.Script("getComputedStyle(document.querySelector('.bloc-dessin pre')).display"), "Le code reste caché.");

        // Modifier : l'éditeur, sur ce bloc.
        menu.OfType<MenuItem>().First().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        UiThread.Until(() => scene.Editor.IsDiagramEditorOpen);
        scene.Editor.AnswerDiagramEditorCommand.Execute(false);
        UiThread.Until(() => !scene.Editor.IsDiagramEditorOpen);

        // Supprimer : le bloc s'en va, le reste ne bouge pas.
        menu.OfType<MenuItem>().Last().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        UiThread.Until(() => !scene.OnDisk.Contains("smartart", StringComparison.Ordinal) && scene.Editor.State == SaveState.Saved);
        Assert.Contains("Avant", scene.OnDisk);
        Assert.Contains("flowchart", scene.OnDisk);
        Assert.Contains("Après", scene.OnDisk);

        scene.Editor.UndoCommand.Execute(null);
        UiThread.Until(() => scene.OnDisk.Contains("```smartart cycle\nUne\nDeux\n```", StringComparison.Ordinal));

        // Un schéma et un graphique disent ce qu'ils sont ; écrits à la main, ils ne se modifient qu'en code.
        var schema = visual.MenuItems(new MenuEvent(null, 0, false, false, false, false, 7, "mermaid", "flowchart TD"), null).OfType<MenuItem>().Select(item => (string)item.Header).ToList();
        CollectionAssert.AreEqual(new[] { EditingText.ShowDrawingCode, EditingText.DeleteSchema }, schema);
        foreach (var chart in new[] { "pie\n\"a\": 1", " xychart-beta", "treemap-beta" })
        {
            var labels = visual.MenuItems(new MenuEvent(null, 0, false, false, false, false, 7, "mermaid", chart), null).OfType<MenuItem>().Select(item => (string)item.Header).ToList();
            CollectionAssert.Contains(labels, EditingText.DeleteChart);
        }

        // Venus de la galerie, ils se rouvrent, et disent toujours ce qu'ils sont.
        foreach (var (model, kind) in new[] { ("etapes", EditingText.DeleteSchema), ("secteurs", EditingText.DeleteChart) })
        {
            var source = KyfRev.Rendering.Diagrams.MermaidTemplates.Source(model, "a : 1\nb : 2")!.Value;
            var labels = visual.MenuItems(new MenuEvent(null, 0, false, false, false, false, 7, source.Info, source.Code), null).OfType<MenuItem>().Select(item => (string)item.Header).ToList();
            CollectionAssert.AreEqual(new[] { EditingText.EditDrawing, EditingText.ShowDrawingCode, kind }, labels);
        }

        // Dans son code, le menu du texte garde la suppression.
        var inCode = visual.MenuItems(new MenuEvent(null, 0, false, true, false, false, 7, "mermaid", "flowchart TD"), null).OfType<MenuItem>().Select(item => (string)item.Header).ToList();
        CollectionAssert.Contains(inCode, EditingText.DeleteSchema);
        CollectionAssert.Contains(inCode, EditingText.Cut);
    }

    [EngineTest]
    public void Removing_the_only_block_leaves_a_paragraph_to_write_in()
    {
        using var scene = new Scene("```smartart cycle\nUne\nDeux\n```\n");
        UiThread.Until(() => scene.Script("document.querySelectorAll('.bloc-dessin').length") == "1");
        var visual = scene.Visual!;
        var items = visual.MenuItems(new MenuEvent(null, 0, false, false, false, false, 0, "smartart cycle", "Une\nDeux"), null).OfType<MenuItem>().ToList();

        items[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        UiThread.Until(() => scene.Script("getComputedStyle(document.querySelector('.bloc-dessin pre')).display") != "\"none\"");

        items[^1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        UiThread.Until(() => scene.Script("document.querySelectorAll('.bloc-dessin').length") == "0");
        Assert.AreEqual("1", scene.Script("document.querySelectorAll('.ProseMirror > p').length"));
    }

    [EngineTest]
    public void An_organization_chart_of_the_sheet_opens_in_its_own_editor_and_is_replaced()
    {
        using var scene = new Scene("Avant\n\n```smartart organigramme\nGrand-mère\nEnfant\n```\n");
        UiThread.Until(() => scene.Script("document.querySelectorAll('.apercu-dessin svg text').length") == "2");

        scene.Script("[...document.querySelectorAll('.action-dessin')].find((b) => b.textContent === 'Modifier…').click()");
        UiThread.Until(() => scene.Editor.IsDiagramEditorOpen);
        UiThread.Until(() => scene.Visual!.IsCovered);
        Assert.IsTrue(scene.SheetPage.DiagramEditorDialog.IsOpen);
        var editor = scene.Editor.DiagramEditor!;
        Assert.AreEqual("Enfant", editor.Nodes[1].Text);
        editor.Select(editor.Nodes[1]);
        editor.AddChildCommand.Execute(null);
        editor.Selected!.Text = "Petit-enfant";

        scene.Editor.AnswerDiagramEditorCommand.Execute(true);

        UiThread.Until(() => scene.OnDisk.Contains("```smartart organigramme\nGrand-mère\n  Enfant\n    Petit-enfant\n```", StringComparison.Ordinal));
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Count(scene.OnDisk, "smartart"), "Il est remplacé, pas ajouté.");
        UiThread.Until(() => scene.Script("document.querySelectorAll('.apercu-dessin svg text').length") == "3");
    }

    [EngineTest]
    public void The_page_stands_out_from_the_background_in_both_themes()
    {
        foreach (var theme in new[] { ThemeKind.Light, ThemeKind.Dark })
        {
            using var scene = new Scene("Un texte.\n", theme: theme);
            UiThread.Until(() => scene.Shown.Contains("Un texte", StringComparison.Ordinal));

            var page = scene.Script("getComputedStyle(document.getElementById('fiche')).backgroundColor");
            var ground = scene.Script("getComputedStyle(document.body).backgroundColor");
            Assert.AreNotEqual(ground, page);
            Assert.AreNotEqual("\"0px\"", scene.Script("getComputedStyle(document.getElementById('fiche')).borderTopWidth"));
            Assert.AreNotEqual("\"none\"", scene.Script("getComputedStyle(document.getElementById('fiche')).boxShadow"));
            Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "La feuille ne déborde pas.");
        }
    }

    [EngineTest]
    public void On_an_encrypted_vault_the_formatted_text_does_not_offer_to_accept_a_word()
    {
        using var scene = new Scene("Une ékole ici.\n");
        scene.Vault.Spelling.AddsUntilClosing = true;

        var items = scene.Visual!.MenuItems(new MenuEvent("ékole", 5, false, false, true, true), new KyfRev.Application.Ports.SpellingCorrection(0, 5, ["école"]));

        var headers = items.OfType<MenuItem>().Select(item => (string)item.Header).ToList();
        CollectionAssert.Contains(headers, "école");
        CollectionAssert.DoesNotContain(headers, EditingText.AddToDictionary);
        CollectionAssert.DoesNotContain(headers, EditingText.AcceptUntilClosing);

        var bare = scene.Visual!.MenuItems(new MenuEvent("ékole", 5, false, false, true, true), new KyfRev.Application.Ports.SpellingCorrection(0, 5, []));
        Assert.IsFalse(bare.Zip(bare.Skip(1)).Any(pair => pair.First is Separator && pair.Second is Separator), "Sans remplacement, pas deux séparateurs de suite.");
    }

    [EngineTest]
    public void A_menu_offers_the_replacements_of_a_wrong_word()
    {
        using var scene = new Scene("Une ékole ici.\n");
        var visual = scene.Visual!;

        var items = visual.MenuItems(new MenuEvent("ékole", 5, false, false, true, true), new KyfRev.Application.Ports.SpellingCorrection(0, 5, ["école"]));

        var headers = items.OfType<MenuItem>().Select(item => (string)item.Header).ToList();
        Assert.AreEqual(EditingText.Cut, headers[0]);
        Assert.AreEqual(EditingText.Bold, headers[3]);
        Assert.AreEqual("école", headers[7]);
        Assert.AreEqual(EditingText.AddToDictionary, headers[8]);
        CollectionAssert.Contains(headers, EditingText.PastePlain);
        CollectionAssert.Contains(headers, EditingText.RemoveLink);
        CollectionAssert.Contains(headers, EditingText.DeleteTable);

        items.OfType<MenuItem>().First(item => (string)item.Header == "école").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        UiThread.Until(() => scene.Shown.Contains("école", StringComparison.Ordinal) || scene.Shown.Contains("Une", StringComparison.Ordinal));

        var inCode = visual.MenuItems(new MenuEvent(null, 0, false, true, false, false), null);
        Assert.AreEqual(EditingText.Cut, ((MenuItem)inCode[0]).Header);
        Assert.HasCount(4, inCode.OfType<MenuItem>().ToList(), "Dans le code, seulement le presse-papiers.");
    }

    [EngineTest]
    public void The_table_tab_appears_in_a_table_and_writes_its_look_above_it()
    {
        using var scene = new Scene("Avant.\n\n| Notion | Auteur |\n|---|---|\n| Pouvoir | Arendt |\n| Domination | Weber |\n| Autorité | Arendt |\n");
        UiThread.Until(() => scene.Shown.Contains("Pouvoir", StringComparison.Ordinal));
        Assert.IsFalse(scene.Editor.IsInTable);
        Assert.AreEqual(Visibility.Collapsed, scene.SheetPage.Ribbon.TableTab.Visibility);

        scene.Select("const cellule = [...page.querySelectorAll('td')].find((td) => td.textContent === 'Pouvoir'); selection.selectAllChildren(cellule); selection.collapseToEnd();");
        UiThread.Until(() => scene.Editor.IsInTable);
        TestShell.Flush();
        Assert.AreEqual(Visibility.Visible, scene.SheetPage.Ribbon.TableTab.Visibility);
        Assert.AreEqual(1, scene.Editor.Tools!.Table!.Column);
        Assert.IsTrue(scene.Editor.Tools.Table.Sortable);

        scene.Editor.ChangeTableCommand.Execute("theme=foret");
        UiThread.Until(() => scene.Editor.Tools?.Table?.Theme == "foret");
        Assert.AreEqual("\"foret\"", scene.Script("document.querySelector('.ProseMirror table').dataset.theme"));
        scene.Editor.ChangeTableCommand.Execute("trier");
        scene.Editor.ChangeTableCommand.Execute("aligner=center");
        scene.Editor.ChangeTableCommand.Execute("total");
        UiThread.Until(() => scene.OnDisk.Contains("<!-- kyfrev tableau total theme=foret -->", StringComparison.Ordinal) && scene.OnDisk.Contains(":---:", StringComparison.Ordinal));

        var written = scene.OnDisk;
        Assert.StartsWith("Avant.\n\n<!-- kyfrev tableau total theme=foret -->\n| Notion | Auteur |", written);
        Assert.IsLessThan(written.IndexOf("Domination", StringComparison.Ordinal), written.IndexOf("Autorité", StringComparison.Ordinal), "les lignes sont rangées de A à Z");

        scene.PlaceCaretIn("Avant.");
        UiThread.Until(() => !scene.Editor.IsInTable);
        TestShell.Flush();
        Assert.AreEqual(Visibility.Collapsed, scene.SheetPage.Ribbon.TableTab.Visibility, "hors du tableau, son onglet disparaît");
    }
}
