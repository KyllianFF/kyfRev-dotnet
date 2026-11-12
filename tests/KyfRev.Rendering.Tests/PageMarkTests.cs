namespace KyfRev.Rendering.Tests;

/// <summary>Le saut de page et la page blanche : une ligne de commentaire, seule, au premier niveau de la fiche.</summary>
[TestClass]
public sealed class PageMarkTests
{
    private static readonly string[] Tags = ["p", "hr", "p", "hr", "p"];
    private static readonly PageMark[] Marks = [PageMark.None, PageMark.Break, PageMark.None, PageMark.BlankPage, PageMark.None];

    [TestMethod]
    public void The_two_lines_are_read_with_or_without_spaces()
    {
        Assert.AreEqual(PageMark.Break, PageMarks.FromLine(PageMarks.BreakLine));
        Assert.AreEqual(PageMark.BlankPage, PageMarks.FromLine(PageMarks.BlankPageLine));
        Assert.AreEqual(PageMark.Break, PageMarks.FromLine("   <!--kyfrev saut-de-page-->  "));

        foreach (var line in new[] { "", "<!-- kyfrev saut -->", "<!-- kyfrev saut-de-page --> texte", "texte <!-- kyfrev page-blanche -->", "    <!-- kyfrev saut-de-page -->", "<!-- kyfrev tableau -->" })
        {
            Assert.AreEqual(PageMark.None, PageMarks.FromLine(line), line);
        }
    }

    [TestMethod]
    public void A_mark_leaves_the_text_and_stays_as_a_rule_that_carries_it()
    {
        var root = SheetRenderer.Render("Avant.\n\n<!-- kyfrev saut-de-page -->\n\nAprès.\n\n<!-- kyfrev page-blanche -->\n\nFin.\n", "f_1");

        var blocks = root.Children.OfType<RenderElement>().ToList();
        CollectionAssert.AreEqual(Tags, blocks.Select(block => block.Tag).ToArray());
        CollectionAssert.AreEqual(Marks, blocks.Select(block => block.PageMark).ToArray());
    }

    [TestMethod]
    public void Elsewhere_the_same_line_is_ordinary_text_and_a_real_rule_carries_nothing()
    {
        var root = SheetRenderer.Render("- <!-- kyfrev saut-de-page -->\n\n> <!-- kyfrev page-blanche -->\n\n---\n\nUn <!-- kyfrev saut-de-page --> dans une phrase.\n", "f_1");

        var all = Descendants(root).ToList();
        Assert.IsTrue(all.All(element => element.PageMark == PageMark.None));
        Assert.AreEqual(1, all.Count(element => element.Tag == "hr"));
    }

    private static IEnumerable<RenderElement> Descendants(RenderElement element)
    {
        foreach (var child in element.Children.OfType<RenderElement>())
        {
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }
}
