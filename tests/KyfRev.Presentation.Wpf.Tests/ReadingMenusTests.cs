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

    [STATestMethod]
    public void The_opened_sheet_changes_its_own_mode_and_hands_the_rest_to_the_shared_actions()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        var target = new SheetTarget(vault.SheetId, "Mitose");

        // Sans les actions communes (un essai, une page seule) : ni rangement ni partage, et rien ne lève.
        var bare = new OpenedSheetMenu(vault.Page());
        Assert.IsFalse(bare.CanManage);
        Assert.IsFalse(bare.CanShare);
        Assert.IsEmpty(bare.Extensions(target));
        foreach (var action in new Action<SheetTarget>[] { bare.PostponeTomorrow, bare.Archive, bare.Restart, bare.ToggleReminders, bare.Rename, bare.Tags, bare.Move, bare.ToggleNote, bare.Trash })
        {
            action(target);
        }

        // Avec elles : la fiche change de mode elle-même, et « Renommer » ouvre la fenêtre commune.
        var actions = new SheetActionsViewModel(vault.App.Sheets, vault.App.Folders, vault.App.Reviews, vault.App.Reminders, vault.App.Trash, new FakeOpener(), TimeProvider.System);
        var page = vault.Page(actions: actions);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => page.Reading is not null);
        var menu = new OpenedSheetMenu(page);
        Assert.IsTrue(menu.CanManage);
        menu.Revise(target);
        Assert.IsTrue(page.IsReviseOn);
        menu.Read(target);
        Assert.IsTrue(page.IsReadOn);
        menu.Print(target);
        menu.Share(target);
        menu.SaveAsTemplate(target);
        Assert.IsFalse(actions.IsActing, "imprimer, partager : rien ne s'ouvre sans leur service");
        menu.Rename(target);
        UiThread.Until(() => actions.IsActing);
        Assert.AreEqual(SheetActionsText.RenameTitle, actions.ActionTitle);
    }

    private static TableRow Row(Run first, Run second)
    {
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(first)));
        row.Cells.Add(new TableCell(new Paragraph(second)));
        return row;
    }
}
