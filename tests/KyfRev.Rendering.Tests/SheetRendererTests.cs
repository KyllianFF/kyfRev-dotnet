using CsCheck;
using KyfRev.Application.Model;

namespace KyfRev.Rendering.Tests;

/// <summary>Ce que le corpus ne dit pas : la forme de l'arbre, que l'affichage lira, et la solidité du moteur.</summary>
[TestClass]
public sealed class SheetRendererTests
{
    private static readonly string[] MarkdownPieces =
    [
        "*", "_", "**", "~~", "`", "``", "[", "]", "(", ")", "!", "![", "](", "$", "$$", "\n", "\n\n", "> ", "- ", "1. ", "# ", "---", "===", "|", "|---|",
        "<u>", "</u>", "<sup>", "&amp;", "&#65;", "&#xD800;", "\\", " ", "  ", "\t", "    ", "a", "é", "😀", "\ud83d", "\ude00", "http://a.b/c", "//a.b", "x@y.z", "mailto:",
        "^[", "[^a]", "[^a]: ", "```", "~~~", "[r]: u", "[r]", "\r\n", "\0", " ", "﻿", "![[a|b]]", "<http://xn--a>",
    ];

    private static readonly string?[] CellAlignments = ["left", "center", "right", null];

    private static readonly int?[] RowLines = [10, 12];

    [TestMethod]
    public void A_first_heading_that_repeats_the_title_is_marked_and_stays_in_the_text()
    {
        var root = SheetRenderer.Render("# Mon titre \n\nTexte\n\n## Partie\n\nSuite", " Mon titre");
        var map = TextMap.Of(root);

        Assert.IsTrue(Find(root, "h1").IsRepeatedTitle);
        Assert.AreEqual("Mon titre\n\nTexte\n\nPartie\n\nSuite\n\n", map.Text);
        Assert.IsEmpty(map.SectionAt(map.Text.IndexOf("Texte", StringComparison.Ordinal)));
        Assert.AreEqual("Partie", map.SectionAt(map.Text.IndexOf("Suite", StringComparison.Ordinal)).Single());
    }

    [TestMethod]
    [DataRow("# Autre\n\nTexte")]
    [DataRow("Texte\n\n# Mon titre")]
    [DataRow("## Mon titre")]
    public void Only_a_first_level_one_heading_can_repeat_the_title(string body)
    {
        var root = SheetRenderer.Render(body, "Mon titre");

        Assert.IsFalse(root.Children.OfType<RenderElement>().Any(element => element.IsRepeatedTitle));
    }

    [TestMethod]
    public void The_tree_keeps_what_the_display_needs()
    {
        var root = SheetRenderer.Render(
            "Un [lien](Autre%20fiche.md) et ![une *image*](dessin.png), `code`, $x^2$.\n\n"
            + "$$\n a + b \n$$\n\n"
            + "3. trois\n4. quatre\n\n"
            + "| A | B | C | D |\n|:--|:-:|--:|---|\n| 1 | 2 | 3 | 4 |\n\n"
            + "```python\nprint(1)\n```\n\n"
            + "Une note[^n].\n\n[^n]: Son texte.\n",
            "Titre");

        Assert.AreEqual(1, Find(root, "p").Line);
        Assert.AreEqual("Autre%20fiche.md", Find(root, "a").Target);
        var image = Find(root, "img");
        Assert.AreEqual("dessin.png", image.Target);
        Assert.AreEqual("une image", image.AlternativeText);
        var formula = Find(root, "span");
        Assert.AreEqual("x^2", formula.Formula);
        Assert.IsFalse(formula.IsFormulaBlock);
        var block = Find(root, "div");
        Assert.AreEqual("a + b", block.Formula);
        Assert.IsTrue(block.IsFormulaBlock);
        Assert.AreEqual(3, Find(root, "ol").ListStart);
        CollectionAssert.AreEqual(CellAlignments, All(root, "td").Select(cell => cell.Alignment).ToArray());
        CollectionAssert.AreEqual(RowLines, All(root, "tr").Select(row => row.Line).ToArray());
        Assert.AreEqual("python", All(root, "code").Single(code => code.Parent!.Tag == "pre").Language);
        var noteLink = All(root, "a").Single(link => link.Identifier == "fnref1");
        Assert.AreEqual("#fn1", noteLink.Target);
        Assert.IsTrue(All(root, "li").Any(item => item.Identifier == "fn1"));
        Assert.IsTrue(All(root, "a").Any(link => link.Target == "#fnref1"));
    }

    [TestMethod]
    [DataRow("mermaid")]
    [DataRow("reseau")]
    public void A_diagram_stays_in_the_tree_but_not_in_the_text(string language)
    {
        var root = SheetRenderer.Render($"Avant\n\n```{language}\nsource\n```\n\nAprès", "Titre");

        Assert.AreEqual(language, Find(root, "code").Language);
        Assert.AreEqual("Avant\n\n\nAprès\n\n", TextMap.Of(root).Text);
    }

    [TestMethod]
    [DataRow("[a](http://bücher.de/é)", "http://xn--bcher-kva.de/%C3%A9")]
    [DataRow("[a](http://😀.org)", "http://xn--e28h.org")]
    [DataRow("[a](http://é.xn--bcher-kva.de)", "http://xn--9ca.xn--bcher-kva.de")]
    [DataRow("[a](b😀)", "b%F0%9F%98%80")]
    [DataRow("[a](b%20c%zz)", "b%20c%25zz")]
    [DataRow("[a](mailto:é@é.org)", "mailto:%C3%A9@xn--9ca.org")]
    [DataRow("[a](x:é@é.org)", "x:%C3%A9@%C3%A9.org")]
    [DataRow("https://exemple.org/a b", "https://exemple.org/a")]
    public void A_link_target_is_encoded_like_the_old_version(string body, string expected)
    {
        Assert.AreEqual(expected, Find(SheetRenderer.Render(body, "Titre"), "a").Target);
    }

    [TestMethod]
    [DataRow("[a](javascript:alert)")]
    [DataRow("[a](vbscript:alert)")]
    [DataRow("[a](file:///c/x)")]
    [DataRow("[a](data:text/html,x)")]
    public void A_dangerous_link_stays_plain_text(string body)
    {
        var root = SheetRenderer.Render(body, "Titre");

        Assert.IsFalse(All(root, "a").Any());
        Assert.AreEqual(body + "\n\n", TextMap.Of(root).Text);
    }

    [TestMethod]
    public void Thousands_of_nested_emphases_keep_the_text_in_reading_order()
    {
        var root = SheetRenderer.Render(string.Concat(Enumerable.Repeat("*a ", 600)) + string.Concat(Enumerable.Repeat("b*", 600)), "Titre");

        Assert.AreEqual(string.Concat(Enumerable.Repeat("a ", 600)) + new string('b', 600) + "\n\n", TextMap.Of(root).Text);
    }

    [TestMethod]
    public void A_lone_surrogate_cannot_break_the_analysis()
    {
        Assert.AreEqual("\ud83da\ude00\n\n", TextMap.Of(SheetRenderer.Render("\ud83d*a*\ude00", "Titre")).Text);
        Assert.AreEqual("%EF%BF%BD", Find(SheetRenderer.Render("[a](\ud83d)", "Titre"), "a").Target);
        Assert.AreEqual("%EF%BF%BDb", Find(SheetRenderer.Render("[a](\ude00b)", "Titre"), "a").Target);
    }

    [TestMethod]
    [DataRow("> ")]
    [DataRow("- ")]
    [DataRow("[")]
    [DataRow("![")]
    [DataRow("^[")]
    [DataRow("<u>")]
    [DataRow("*a ")]
    [DataRow("**a ")]
    [DataRow("~~a ")]
    [DataRow("`")]
    public void A_sheet_nested_thousands_of_times_is_read_without_overflowing(string opening)
    {
        var closing = opening switch
        {
            "[" or "![" => "](b)",
            "^[" => "]",
            "<u>" => "</u>",
            "*a " => "b*",
            "**a " => "b**",
            "~~a " => "b~~",
            _ => string.Empty,
        };
        var root = SheetRenderer.Render(string.Concat(Enumerable.Repeat(opening, 5000)) + "a" + string.Concat(Enumerable.Repeat(closing, 5000)), "Titre");

        Assert.IsGreaterThan(0, TextMap.Of(root).Text.Length);
    }

    [TestMethod]
    public void An_address_too_long_for_a_link_stops_at_the_limit()
    {
        var link = Find(SheetRenderer.Render("https://exemple.org/" + new string('a', 20000), "Titre"), "a");

        // La recherche s'arrête 10 000 caractères après le protocole.
        Assert.AreEqual("https:".Length + 10000, link.Target!.Length);
    }

    [TestMethod]
    public void Any_text_is_read_and_its_passages_are_found_where_they_were()
    {
        Gen.OneOfConst(MarkdownPieces).Array[0, 40].Select(string.Concat).Sample(
            body =>
            {
                var map = TextMap.Of(SheetRenderer.Render(body, "Titre"));
                var previousEnd = 0;
                foreach (var piece in map.Pieces)
                {
                    Assert.IsGreaterThanOrEqualTo(previousEnd, piece.Start);
                    var text = map.Text[piece.Start..piece.End];
                    Assert.AreEqual(piece.Node is RenderText written ? written.Value : FormulaText((RenderElement)piece.Node), text);
                    previousEnd = piece.End;
                    if (Anchoring.Trim(map.Text, piece.Start, piece.End) is { } position)
                    {
                        var anchor = Anchoring.AnchorOf(map.Text, position, map.SectionAt(position.Start), map.SourceLineAt(position.Start, 1), "sha256:essai");
                        Assert.AreEqual(new AnchorResult(AnchorState.Anchored, position), Anchoring.Locate(map.Text, anchor, AnchorState.Anchored));
                    }
                }
            },
            iter: 4000);
    }

    [TestMethod]
    public void An_empty_quote_is_never_found()
    {
        var anchor = new Anchor(string.Empty, string.Empty, string.Empty, 0, 0, [], null, "sha256:essai");

        Assert.AreEqual(new AnchorResult(AnchorState.NotFound, null), Anchoring.Locate("Texte", anchor, AnchorState.Anchored));
    }

    [TestMethod]
    public void A_selection_of_spaces_gives_no_passage()
    {
        Assert.IsNull(Anchoring.Trim("a \n ﻿ b", 1, 6));
        Assert.AreEqual(new TextPosition(2, 3), Anchoring.Trim("  a  ", -4, 40));
    }

    [TestMethod]
    public void Two_anchors_differ_as_soon_as_one_field_differs()
    {
        var anchor = new Anchor("a", "b", "c", 1, 2, ["d"], 3, "e");

        Assert.IsTrue(Anchoring.SameAnchor(anchor, anchor with { Section = ["d"] }));
        foreach (var other in new[]
        {
            anchor with { Exact = "x" }, anchor with { Before = "x" }, anchor with { After = "x" }, anchor with { Start = 9 }, anchor with { End = 9 },
            anchor with { Section = ["x"] }, anchor with { Section = [] }, anchor with { SourceLine = null }, anchor with { SheetFingerprint = "x" },
        })
        {
            Assert.IsFalse(Anchoring.SameAnchor(anchor, other));
        }
    }

    private static string FormulaText(RenderElement formula) =>
        formula.IsFormulaBlock ? $"$${formula.Formula}$$" : $"${formula.Formula}$";

    private static RenderElement Find(RenderElement root, string tag) => All(root, tag).First();

    private static IEnumerable<RenderElement> All(RenderElement root, string tag)
    {
        foreach (var element in root.Children.OfType<RenderElement>())
        {
            if (element.Tag == tag)
            {
                yield return element;
            }
            foreach (var descendant in All(element, tag))
            {
                yield return descendant;
            }
        }
    }
}
