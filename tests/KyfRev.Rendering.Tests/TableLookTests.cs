namespace KyfRev.Rendering.Tests;

/// <summary>L'apparence d'un tableau : la ligne écrite au-dessus de lui.</summary>
[TestClass]
public sealed class TableLookTests
{
    private const string Table = "| Notion | Auteur |\n|---|---|\n| Pouvoir | Arendt |\n";

    [TestMethod]
    public void The_default_look_writes_nothing_and_any_other_is_read_back_the_same()
    {
        Assert.IsNull(TableLook.Default.ToLine());
        var all = new TableLook
        {
            HeaderRow = true,
            HeaderColumn = true,
            TotalRow = true,
            Bands = TableBands.Columns,
            Lines = TableLines.Header,
            Theme = TableTheme.Slate,
            HiddenInReview = 2,
            FullWidth = true,
        };

        var line = all.ToLine();

        Assert.AreEqual("<!-- kyfrev tableau entete=ligne,colonne total bandes=colonnes traits=entete theme=ardoise reviser-cache=2 largeur=page -->", line);
        Assert.AreEqual(all, TableLook.FromLine(line!));
        Assert.AreEqual(TableLook.Default with { Theme = TableTheme.Plum }, TableLook.FromLine("   <!--kyfrev tableau theme=prune-->  "));
        Assert.AreEqual("<!-- kyfrev tableau entete=colonne -->", (TableLook.Default with { HeaderRow = false, HeaderColumn = true }).ToLine());
        Assert.AreEqual("<!-- kyfrev tableau entete=aucune bandes=lignes traits=horizontaux -->", (TableLook.Default with { HeaderRow = false, Bands = TableBands.Rows, Lines = TableLines.Horizontal }).ToLine());
    }

    [TestMethod]
    [DataRow("<!-- kyfrev tableau theme=rose traits=x bandes=y entete=z reviser-cache=0 futur=1 -->")]
    [DataRow("<!-- kyfrev tableau reviser-cache=-1 largeur=autre -->")]
    public void An_unknown_value_keeps_its_default_and_an_unknown_word_is_ignored(string line) =>
        Assert.AreEqual(TableLook.Default, TableLook.FromLine(line));

    [TestMethod]
    [DataRow("<!-- autre chose -->")]
    [DataRow("texte <!-- kyfrev tableau -->")]
    [DataRow("<!-- kyfrev tableau theme=foret")]
    [DataRow("    <!-- kyfrev tableau -->")]
    public void Another_line_is_not_a_look(string line) => Assert.IsNull(TableLook.FromLine(line));

    [TestMethod]
    public void The_entete_values_read_both_ways()
    {
        Assert.IsTrue(TableLook.FromLine("<!-- kyfrev tableau entete=colonne,ligne -->") is { HeaderRow: true, HeaderColumn: true });
        Assert.IsTrue(TableLook.FromLine("<!-- kyfrev tableau entete=ligne -->") is { HeaderRow: true, HeaderColumn: false });
        Assert.IsTrue(TableLook.FromLine("<!-- kyfrev tableau entete=aucune -->") is { HeaderRow: false, HeaderColumn: false });
    }

    [TestMethod]
    [DataRow("<!-- kyfrev tableau theme=foret total -->\n")]
    [DataRow("<!-- kyfrev tableau theme=foret total -->\n\n")]
    public void The_line_above_a_table_goes_to_the_table_and_leaves_the_text(string line)
    {
        var withLook = SheetRenderer.Render("Avant.\n\n" + line + Table, "Fiche");
        var without = SheetRenderer.Render("Avant.\n\n" + Table, "Fiche");

        var table = Find(withLook, "table");
        Assert.AreEqual(TableLook.Default with { Theme = TableTheme.Forest, TotalRow = true }, table.Look);
        Assert.IsNull(Find(without, "table").Look);
        Assert.AreEqual(TextMap.Of(without).Text, TextMap.Of(withLook).Text, "la ligne ne s'affiche pas, et les positions des passages ne bougent pas");
    }

    [TestMethod]
    public void The_same_line_elsewhere_stays_a_paragraph()
    {
        var root = SheetRenderer.Render("<!-- kyfrev tableau theme=foret -->\n\nPas de tableau.\n\n- <!-- kyfrev tableau -->\n\n" + Table, "Fiche");

        StringAssert.StartsWith(TextMap.Of(root).Text, "<!-- kyfrev tableau theme=foret -->", StringComparison.Ordinal);
        Assert.IsNull(Find(root, "table").Look, "une ligne dans une liste ne va pas au tableau qui la suit");
    }

    private static RenderElement Find(RenderElement root, string tag) =>
        root.Tag == tag ? root : root.Children.OfType<RenderElement>().Select(child => Find(child, tag)).FirstOrDefault(found => found.Tag == tag) ?? root;
}
