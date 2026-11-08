using System.Globalization;
using System.Text;
using System.Windows;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Presentation.Wpf.Shell;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Ce qui sort d'une fiche partagée : la page d'un seul fichier, le texte, le presse-papiers et le PDF.</summary>
[TestClass]
public sealed class SharedSheetTests
{
    private static readonly SharedFacts Facts = new("Mitose", "fiches/Biologie", new DateOnly(2026, 11, 8));

    private static readonly byte[] Png = [137, 80, 78, 71];

    private static readonly string[] BothDrawings = ["graph TD", "{ \"titre\": \"Mon réseau\" }"];

    private static string Page(string body, SharedDrawings? drawings = null) => SharedSheet.Page(SheetRenderer.Render(body, "Mitose"), Facts, drawings ?? SharedDrawings.None);

    private static string Text(string body, SharedDrawings? drawings = null) => SharedSheet.PlainText(SheetRenderer.Render(body, "Mitose"), Facts, drawings ?? SharedDrawings.None);

    [TestMethod]
    public void The_page_stands_alone_and_loads_nothing_from_anywhere()
    {
        var page = Page("Un paragraphe.\n\n![loin](https://exemple.fr/image.png)\n");

        StringAssert.StartsWith(page, "<!doctype html>", StringComparison.Ordinal);
        StringAssert.Contains(page, "default-src 'none'; img-src data:", StringComparison.Ordinal);
        StringAssert.Contains(page, "<h1>Mitose</h1>", StringComparison.Ordinal);
        StringAssert.Contains(page, "<title>Mitose</title>", StringComparison.Ordinal);
        StringAssert.Contains(page, "Fiche partagée depuis kyfRev le 8 novembre 2026", StringComparison.Ordinal);
        StringAssert.Contains(page, "image-bloquee", StringComparison.Ordinal);
        Assert.IsFalse(page.Contains("src=\"http", StringComparison.Ordinal), "une image distante reste signalée");
        Assert.IsFalse(page.Contains("<script", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(page.Contains("<link", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(page, "--texte:", StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_title_written_in_the_sheet_is_not_repeated()
    {
        StringAssert.Contains(Text("# Mitose\n\nDu texte.\n").Trim(), "Du texte.", StringComparison.Ordinal);
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Count(Page("# Mitose\n\nDu texte.\n"), "<h1>"));
        Assert.AreEqual("Mitose\n\nDu texte.\n", Text("# Mitose\n\nDu texte.\n"));
        Assert.AreEqual("Mitose\n\nDu texte.\n", Text("Du texte.\n"));
    }

    [TestMethod]
    public void Everything_written_in_the_sheet_is_escaped()
    {
        var facts = Facts with { Title = "<b>&\"titre\"" };
        var page = SharedSheet.Page(SheetRenderer.Render("Un `<script>alert(1)</script>` et <b>gras</b>\n\n```\n<script>x</script>\n```\n", "Autre"), facts, SharedDrawings.None);

        StringAssert.Contains(page, "<title>&lt;b&gt;&amp;&quot;titre&quot;</title>", StringComparison.Ordinal);
        Assert.IsFalse(page.Contains("<script", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(page, "&lt;script&gt;", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Images_and_links_keep_only_what_has_a_sense_outside_the_vault()
    {
        var page = Page(
            "![carte](carte.png) ![point](data:image/png;base64,iVBORw0KGgo=)\n\n"
            + "[web](https://exemple.fr/a) [autre](Autre.md) [note](#plus-bas) [piege](javascript:alert)\n");

        StringAssert.Contains(page, "data-coffre=\"fiches/Biologie/carte.png\" alt=\"carte\"", StringComparison.Ordinal);
        StringAssert.Contains(page, "src=\"data:image/png;base64,iVBORw0KGgo=\" alt=\"point\"", StringComparison.Ordinal);
        StringAssert.Contains(page, "<a href=\"https://exemple.fr/a\">web</a>", StringComparison.Ordinal);
        StringAssert.Contains(page, "<span>autre</span>", StringComparison.Ordinal);
        StringAssert.Contains(page, "<a href=\"#plus-bas\">note</a>", StringComparison.Ordinal);
        Assert.IsFalse(page.Contains("href=\"javascript:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Tasks_lists_and_footnotes_read_as_in_the_sheet()
    {
        var page = Page("- [ ] à faire\n- [x] fait\n\n3. trois\n4. quatre\n\nUn renvoi[^1].\n\n[^1]: La note.\n\n---\n\nFin\nde ligne\n");

        StringAssert.Contains(page, "class=\"tache\"", StringComparison.Ordinal);
        StringAssert.Contains(page, "class=\"tache faite\"", StringComparison.Ordinal);
        StringAssert.Contains(page, "☐ à faire", StringComparison.Ordinal);
        StringAssert.Contains(page, "☑ fait", StringComparison.Ordinal);
        StringAssert.Contains(Page("- [ ] aéré\n\n  encore\n\n- [x] lui aussi\n"), "☐ aéré", StringComparison.Ordinal);
        StringAssert.Contains(page, "<ol start=\"3\">", StringComparison.Ordinal);
        StringAssert.Contains(page, "class=\"footnotes\"", StringComparison.Ordinal);
        StringAssert.Contains(page, "id=\"fn1\"", StringComparison.Ordinal);
        StringAssert.Contains(page, "<hr>", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Colours_sizes_and_alignment_are_kept_and_stay_readable_on_the_page()
    {
        var page = Page("<div align=\"center\">\n\nUn <span style=\"color:#ffff00\">mot</span> et <span style=\"font-size:1.25em\">grand</span>\n\n</div>\n");

        StringAssert.Contains(page, "text-align:center", StringComparison.Ordinal);
        StringAssert.Contains(page, "font-size:1.25em", StringComparison.Ordinal);
        Assert.IsFalse(page.Contains("color:#ffff00", StringComparison.Ordinal), "du jaune sur fond clair ne se lirait pas");
    }

    [TestMethod]
    public void A_table_keeps_its_look_or_takes_the_one_of_the_page()
    {
        const string table = "| A | B |\n|:--|--:|\n| 1 | 2 |\n| 3 | 4 |\n";
        StringAssert.Contains(Page("<!-- kyfrev tableau bandes=lignes -->\n" + table + "| 5 | 6 |\n| 7 | 8 |\n"), "background:#", StringComparison.Ordinal);

        var plain = Page(table);
        StringAssert.Contains(plain, "<table", StringComparison.Ordinal);
        Assert.IsFalse(plain.Contains("class=\"apparence\"", StringComparison.Ordinal), "sans ligne d'apparence, celle de la page");
        StringAssert.Contains(plain, "<thead>", StringComparison.Ordinal);
        StringAssert.Contains(plain, "text-align:left", StringComparison.Ordinal);
        StringAssert.Contains(plain, "text-align:right", StringComparison.Ordinal);

        var styled = Page("<!-- kyfrev tableau entete=ligne total bandes=lignes traits=horizontaux theme=foret largeur=page -->\n" + table);
        StringAssert.Contains(styled, "<table class=\"apparence\" style=\"width:100%\">", StringComparison.Ordinal);
        StringAssert.Contains(styled, "border-style:solid", StringComparison.Ordinal);
        StringAssert.Contains(styled, "font-weight:600", StringComparison.Ordinal);
        StringAssert.Contains(styled, "background:#", StringComparison.Ordinal);

        Assert.AreEqual("Mitose\n\nA\tB\n1\t2\n3\t4\n", Text(table));
    }

    [TestMethod]
    public void What_cannot_be_drawn_stays_readable()
    {
        var page = Page("Un $x^2$ et\n\n$$\ny\n$$\n\n```mermaid\ngraph TD\n```\n\n```python\nx = 1\n```\n");

        StringAssert.Contains(page, "<code>$x^2$</code>", StringComparison.Ordinal);
        StringAssert.Contains(page, "<code>$$y$$</code>", StringComparison.Ordinal);
        StringAssert.Contains(page, "class=\"schema-en-code\"", StringComparison.Ordinal);
        StringAssert.Contains(page, "<pre><code>graph TD</code></pre>", StringComparison.Ordinal);
        StringAssert.Contains(page, "<pre><code>x = 1</code></pre>", StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_smartart_is_drawn_by_the_rendering_itself()
    {
        var page = Page("```smartart cycle\nG1\nS\nG2\n```\n");

        StringAssert.Contains(page, "<figure class=\"diagramme\"><svg", StringComparison.Ordinal);
        StringAssert.Contains(Text("```smartart cycle\nG1\nS\n```\n"), SharingText.Diagram, StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_drawings_replace_their_code()
    {
        var root = SheetRenderer.Render("```mermaid\ngraph TD\n```\n\n```reseau\n{ \"schema\": \"s\" }\n```\n\nUn $x$.\n", "Mitose");
        var codes = Elements(root).Where(element => element.Tag == "code" && element.Language is not null).ToList();
        var formula = Elements(root).Single(element => element.Formula is not null);
        var drawings = new SharedDrawings
        {
            Pictures = new Dictionary<RenderElement, string> { [codes[0]] = "<img id=\"m\">", [codes[1]] = "<img id=\"r\">", [formula] = "<img id=\"f\">" },
            Titles = new Dictionary<RenderElement, string> { [codes[1]] = "Réseau <A>" },
        };

        var page = SharedSheet.Page(root, Facts, drawings);
        StringAssert.Contains(page, "<figure class=\"diagramme\"><img id=\"m\"></figure>", StringComparison.Ordinal);
        StringAssert.Contains(page, "<figure class=\"reseau\"><img id=\"r\"><figcaption>Réseau &lt;A&gt;</figcaption></figure>", StringComparison.Ordinal);
        StringAssert.Contains(page, "role=\"math\"", StringComparison.Ordinal);
        StringAssert.Contains(page, "<img id=\"f\">", StringComparison.Ordinal);

        var text = SharedSheet.PlainText(root, Facts, drawings);
        StringAssert.Contains(text, SharingText.Diagram, StringComparison.Ordinal);
        StringAssert.Contains(text, SharingText.NetworkNamedOf("Réseau <A>"), StringComparison.Ordinal);
        StringAssert.Contains(Text("```reseau\n{}\n```\n"), SharingText.Network, StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_plain_text_reads_like_what_a_browser_gives()
    {
        var text = Text(
            "## Partie\n\nUn paragraphe\nsur deux lignes  \net un saut.\n\n- un\n- deux\n\n> cité\n\n$$\nz\n$$\n\n```\ncode\n```\n\n"
            + "![carte](carte.png) ![](autre.png) ![loin](https://exemple.fr/a.png)\n\n---\n\nFin $x$.\n");

        StringAssert.Contains(text, "Un paragraphe sur deux lignes\net un saut.", StringComparison.Ordinal);
        StringAssert.Contains(text, "un\ndeux", StringComparison.Ordinal);
        StringAssert.Contains(text, "cité", StringComparison.Ordinal);
        StringAssert.Contains(text, "$$ z $$", StringComparison.Ordinal);
        StringAssert.Contains(text, "\ncode\n", StringComparison.Ordinal);
        StringAssert.Contains(text, "[Image : carte] [Image] Image distante bloquée", StringComparison.Ordinal);
        StringAssert.Contains(text, "Fin $x$.", StringComparison.Ordinal);
        Assert.IsFalse(text.Contains("\n\n\n", StringComparison.Ordinal));
    }

    [TestMethod]
    public void The_clipboard_format_says_in_bytes_where_the_page_and_the_fragment_are()
    {
        const string page = "<html><head><title>é</title></head><body><p>Déjà</p></body></html>";

        var format = SharedClipboard.HtmlFormat(page);
        var bytes = Encoding.UTF8.GetBytes(format);

        var startHtml = Offset(format, "StartHTML");
        var endHtml = Offset(format, "EndHTML");
        var start = Offset(format, "StartFragment");
        var end = Offset(format, "EndFragment");
        Assert.AreEqual(bytes.Length, endHtml);
        StringAssert.StartsWith(Encoding.UTF8.GetString(bytes[startHtml..]), "<html>", StringComparison.Ordinal);
        Assert.AreEqual("<p>Déjà</p>", Encoding.UTF8.GetString(bytes[start..end]));
    }

    [STATestMethod]
    public void The_clipboard_holds_both_forms_and_hides_what_comes_from_an_encrypted_vault()
    {
        const string page = "<html><body><p>a</p></body></html>";

        var open = SharedClipboard.DataOf(page, "a", isPrivate: false);
        Assert.IsTrue(open.GetDataPresent(DataFormats.Html));
        Assert.AreEqual("a", open.GetText(TextDataFormat.UnicodeText));
        Assert.IsFalse(open.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing"));

        var hidden = SharedClipboard.DataOf(page, "a", isPrivate: true);
        Assert.IsTrue(hidden.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing"));
    }

    [STATestMethod]
    public void A_formula_becomes_a_sharp_picture_named_by_its_source()
    {
        using var ui = new UiThread();

        var picture = SharedSheetPictures.Formula("\\tfrac{1}{2}", block: true);

        StringAssert.StartsWith(picture, "<img src=\"data:image/png;base64,", StringComparison.Ordinal);
        StringAssert.Contains(picture, "alt=\"\\tfrac{1}{2}\"", StringComparison.Ordinal);
        Assert.IsNull(SharedSheetPictures.Formula("\\inconnue{", block: false), "elle reste lisible en TeX");
    }

    [STATestMethod]
    public void The_pictures_are_drawn_in_the_light_theme_and_named_by_the_schema()
    {
        using var ui = new UiThread();
        var engine = new DrawingEngine();
        var schemas = new ReadingSchemas { Text = "{ \"titre\": \"Mon réseau\" }" };
        var pictures = new SharedSheetPictures(engine, () => schemas);
        var root = SheetRenderer.Render($"$x$\n\n```mermaid\ngraph TD\n```\n\n```reseau\n{Reference}\n```\n", "Mitose");

        var running = pictures.DrawAsync(root, drawing: true, CancellationToken.None);
        UiThread.Until(() => running.IsCompleted);

        var drawn = running.Result!;
        Assert.HasCount(3, drawn.Pictures);
        Assert.AreEqual("Mon réseau", drawn.Titles.Values.Single());
        CollectionAssert.AreEqual(BothDrawings, engine.Drawn);
        Assert.IsTrue(drawn.Pictures.Values.Any(picture => picture.Contains("width:120px", StringComparison.Ordinal)));
    }

    [STATestMethod]
    public void For_the_text_only_the_titles_of_the_schemas_are_read()
    {
        using var ui = new UiThread();
        var engine = new DrawingEngine();
        var schemas = new ReadingSchemas { Text = "{ \"titre\": \"Mon réseau\" }" };
        var root = SheetRenderer.Render($"$x$\n\n```mermaid\ngraph TD\n```\n\n```reseau\n{Reference}\n```\n", "Mitose");

        var running = new SharedSheetPictures(engine, () => schemas).DrawAsync(root, drawing: false, CancellationToken.None);
        UiThread.Until(() => running.IsCompleted);

        Assert.IsEmpty(running.Result!.Pictures);
        Assert.HasCount(1, running.Result.Titles);
        Assert.IsEmpty(engine.Drawn);
    }

    [STATestMethod]
    [DataRow("{ cassé")]
    [DataRow("{ \"titre\": \"\" }")]
    [DataRow("{ \"autre\": 1 }")]
    public void A_schema_without_a_readable_title_has_none(string schema)
    {
        using var ui = new UiThread();
        var pictures = new SharedSheetPictures(new DrawingEngine(), () => new ReadingSchemas { Text = schema });
        var root = SheetRenderer.Render($"```reseau\n{Reference}\n```\n", "Mitose");

        var running = pictures.DrawAsync(root, drawing: false, CancellationToken.None);
        UiThread.Until(() => running.IsCompleted);

        Assert.IsEmpty(running.Result!.Titles);
    }

    [STATestMethod]
    public void Without_the_engine_or_the_module_nothing_is_drawn_and_a_stale_request_returns_nothing()
    {
        using var ui = new UiThread();
        var root = SheetRenderer.Render($"```mermaid\ngraph TD\n```\n\n```reseau\n{Reference}\n```\n", "Mitose");

        var alone = new SharedSheetPictures(null).DrawAsync(root, drawing: true, CancellationToken.None);
        UiThread.Until(() => alone.IsCompleted);
        Assert.IsEmpty(alone.Result!.Pictures);

        var inactive = new SharedSheetPictures(new DrawingEngine(), () => new ReadingSchemas { Active = false }).DrawAsync(root, drawing: true, CancellationToken.None);
        UiThread.Until(() => inactive.IsCompleted);
        Assert.IsEmpty(inactive.Result!.Titles);

        var stale = new SharedSheetPictures(null).DrawAsync(root, drawing: true, new CancellationToken(canceled: true));
        UiThread.Until(() => stale.IsCompleted);
        Assert.IsNull(stale.Result);
    }

    [EngineTest]
    public void The_hidden_page_really_makes_a_pdf()
    {
        using var ui = new UiThread();
        var printer = new SheetPrinter(FakeEnvironment.WebData);

        var printed = printer.PrintAsync("<!doctype html><html><body><h1>Mitose</h1><img src=\"https://exemple.invalid/a.png\"></body></html>", CancellationToken.None);
        UiThread.Until(() => printed.IsCompleted, TimeSpan.FromMinutes(2));

        Assert.IsNotNull(printed.Result, "le moteur a répondu");
        Assert.AreEqual("%PDF", Encoding.ASCII.GetString(printed.Result, 0, 4));
    }

    [EngineTest]
    public void A_stale_or_too_slow_pdf_returns_nothing()
    {
        using var ui = new UiThread();

        var stale = new SheetPrinter(FakeEnvironment.WebData).PrintAsync("<html></html>", new CancellationToken(canceled: true));
        UiThread.Until(() => stale.IsCompleted, TimeSpan.FromMinutes(2));
        Assert.IsNull(stale.Result);

        var slow = new SheetPrinter(FakeEnvironment.WebData) { Patience = TimeSpan.Zero }.PrintAsync("<html></html>", CancellationToken.None);
        UiThread.Until(() => slow.IsCompleted, TimeSpan.FromMinutes(2));
        Assert.IsNull(slow.Result);

        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetPrinter(FakeEnvironment.WebData).PrintAsync(null!, CancellationToken.None).GetAwaiter().GetResult());
    }

    private const string Reference = "schema: s_01JQZ8S7ZK4V3H2N8M6X5W9PQR";

    private static int Offset(string format, string name)
    {
        var at = format.IndexOf(name + ":", StringComparison.Ordinal) + name.Length + 1;
        return int.Parse(format.AsSpan(at, 10), CultureInfo.InvariantCulture);
    }

    private static IEnumerable<RenderElement> Elements(RenderElement root)
    {
        yield return root;
        foreach (var child in root.Children.OfType<RenderElement>().SelectMany(Elements))
        {
            yield return child;
        }
    }

    private sealed class ReadingSchemas : INetworkSchemas
    {
        public string? Text { get; init; }

        public bool Active { get; init; } = true;

        public bool IsActive => Active;

        public Task<string?> ReadAsync(string id) => Task.FromResult(Text);

        public Task<IReadOnlyList<SchemaSummary>> ListAsync() => Task.FromResult<IReadOnlyList<SchemaSummary>>([]);

        public Task<string?> CreateAsync(string title) => Task.FromResult<string?>(null);

        public Task<string?> DuplicateAsync(string id) => Task.FromResult<string?>(null);

        public void Open(string id)
        {
        }
    }

    private sealed class DrawingEngine : IMermaidEngine
    {
        public List<string> Drawn { get; } = [];

        public Task<MermaidPicture?> DrawAsync(string code, IReadOnlyDictionary<string, string> theme)
        {
            Drawn.Add(code);
            return Task.FromResult<MermaidPicture?>(new MermaidPicture(Png, "image", 120, 60));
        }

        public Task<MermaidPicture?> DrawSvgAsync(ReadOnlyMemory<byte> svg) => Task.FromResult<MermaidPicture?>(null);

        public Task<MermaidPicture?> DrawNetworkAsync(string schema, IReadOnlyDictionary<string, string> colors)
        {
            Drawn.Add(schema);
            return Task.FromResult<MermaidPicture?>(new MermaidPicture(Png, "image", 80, 40));
        }
    }
}
