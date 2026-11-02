using System.Globalization;
using System.Text.Json.Nodes;

namespace KyfRev.Rendering.Tests;

/// <summary>
/// Le format étendu : couleur et taille, alignement, tâches. Les cas viennent du fichier que l'éditeur lit aussi : une
/// écriture reconnue d'un côté l'est de l'autre.
/// </summary>
[TestClass]
public sealed class ExtendedFormatTests
{
    public static IEnumerable<object[]> Cases =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "format-etendu", "cas.json")))!
            .AsArray()
            .Select(item => new object[] { item!["source"]!.GetValue<string>(), item.ToJsonString() });

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void Reads_like_the_editor(string source, string expected)
    {
        var wanted = JsonNode.Parse(expected)!;
        var elements = Elements(SheetRenderer.Render(source, string.Empty)).ToList();

        CollectionAssert.AreEqual(Strings(wanted["colors"]), elements.Where(element => element.TextColor is not null).Select(element => element.TextColor).ToList());
        CollectionAssert.AreEqual(
            (wanted["sizes"]?.AsArray() ?? []).Select(size => size!.GetValue<double>()).ToList(),
            elements.Where(element => element.FontScale is not null).Select(element => element.FontScale!.Value).ToList());
        CollectionAssert.AreEqual(Strings(wanted["aligns"]), elements.Where(element => element is { Tag: "div", Alignment: not null }).Select(element => element.Alignment).ToList());
        CollectionAssert.AreEqual(
            (wanted["tasks"]?.AsArray() ?? []).Select(task => task?.GetValue<bool>()).ToList(),
            elements.Where(element => element.Tag == "li").Select(element => element.Task).ToList());
    }

    [TestMethod]
    public void A_task_keeps_its_box_in_the_text_so_that_positions_do_not_move()
    {
        var map = TextMap.Of(SheetRenderer.Render("- [ ] Acheter\n- [x] Fait", string.Empty));

        Assert.Contains("[ ] Acheter", map.Text);
        Assert.Contains("[x] Fait", map.Text);
    }

    [TestMethod]
    public void The_tags_of_a_style_or_of_an_alignment_are_not_in_the_text()
    {
        var map = TextMap.Of(SheetRenderer.Render("<div align=\"center\">\n\nUn <span style=\"color:#ff0000\">mot</span>\n\n</div>", string.Empty));

        Assert.AreEqual("Un mot", map.Text.Trim());
    }

    [TestMethod]
    public void A_formula_block_is_not_an_alignment()
    {
        var division = Elements(SheetRenderer.Render("$$\nx\n$$", string.Empty)).Single(element => element.Tag == "div");

        Assert.IsNull(division.Alignment);
        Assert.AreEqual("x", division.Formula);
    }

    [TestMethod]
    public void A_scale_reads_the_same_in_any_culture()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            var span = Elements(SheetRenderer.Render("<span style=\"font-size:1.25em\">a</span>", string.Empty)).Single(element => element.Tag == "span");

            Assert.AreEqual(1.25, span.FontScale);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public void A_trapped_sheet_that_opens_passages_without_end_stays_text_and_does_not_block()
    {
        var opening = string.Concat(Enumerable.Repeat("<div align=\"center\">\n\n", 200_000));

        var elements = Elements(SheetRenderer.Render(opening, string.Empty)).ToList();

        Assert.IsFalse(elements.Exists(element => element is { Tag: "div", Alignment: not null }));
    }

    [TestMethod]
    public void Passages_nested_beyond_the_limit_do_not_overflow()
    {
        var nested = string.Concat(Enumerable.Repeat("<div align=\"center\">\n\n", 300)) + "a\n\n" + string.Concat(Enumerable.Repeat("</div>\n\n", 300));

        var elements = Elements(SheetRenderer.Render(nested, string.Empty)).ToList();

        Assert.IsTrue(elements.Exists(element => element is { Tag: "div", Alignment: "center" }));
    }

    private static List<string> Strings(JsonNode? node) =>
        [.. (node?.AsArray() ?? []).Select(item => item!.GetValue<string>())];

    private static IEnumerable<RenderElement> Elements(RenderElement root)
    {
        var stack = new Stack<RenderElement>();
        stack.Push(root);
        while (stack.TryPop(out var element))
        {
            yield return element;
            foreach (var child in element.Children.OfType<RenderElement>().Reverse())
            {
                stack.Push(child);
            }
        }
    }
}
