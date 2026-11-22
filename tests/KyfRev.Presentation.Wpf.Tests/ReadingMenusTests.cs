using System.Windows.Controls;
using System.Windows.Documents;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Ce que le clic droit vise dans une fiche ouverte (rangées 23B et 23D) : un tableau, un schéma.</summary>
[TestClass]
public sealed class ReadingMenusTests
{
    [STATestMethod]
    public void A_table_is_found_from_its_text_and_copied_as_rows_of_tabs()
    {
        using var ui = new UiThread();
        var inside = new Run("Mitose");
        var table = new Table();
        var group = new TableRowGroup();
        group.Rows.Add(Row(new Run("Phase"), new Run("Durée")));
        group.Rows.Add(Row(inside, new Run("1 h\ntout au plus")));
        table.RowGroups.Add(group);
        _ = new FlowDocument(table);

        Assert.AreSame(table, SheetPage.TableAt(inside));
        Assert.IsNull(SheetPage.TableAt(new Run("ailleurs")));
        Assert.IsNull(SheetPage.TableAt(null));
        Assert.AreEqual($"Phase\tDurée{Environment.NewLine}Mitose\t1 h tout au plus", SheetPage.TableText(table), "une ligne par rangée, une case par tabulation");
    }

    [STATestMethod]
    public void A_drawing_is_found_from_what_it_shows()
    {
        using var ui = new UiThread();
        var inner = new Border();
        var network = new NetworkView { Child = inner };

        Assert.AreSame(network, SheetPage.DrawingAt(inner));
        Assert.AreSame(network, SheetPage.DrawingAt(network));
        var diagram = new DiagramView();
        Assert.AreSame(diagram, SheetPage.DrawingAt(diagram));
        var mermaid = new MermaidView();
        Assert.AreSame(mermaid, SheetPage.DrawingAt(mermaid));
        Assert.IsNull(SheetPage.DrawingAt(new Border()));
        Assert.IsNull(SheetPage.DrawingAt("du texte"));
    }

    private static TableRow Row(Run first, Run second)
    {
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(first)));
        row.Cells.Add(new TableCell(new Paragraph(second)));
        return row;
    }
}
