using System.Windows;
using System.Windows.Controls;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue « Archives ».</summary>
internal sealed partial class ArchivesPage : UserControl
{
    public ArchivesPage()
    {
        InitializeComponent();
        // La disposition des lignes suit la largeur de la page. La page quittée n'a plus de contexte.
        SizeChanged += (_, e) =>
        {
            if (DataContext is ArchivesPageViewModel page)
            {
                page.PageWidth = e.NewSize.Width;
            }
        };
    }

    /// <summary>Le menu d'une fiche archivée : l'ouvrir, la remettre en révision, l'inclure ou non dans les rappels.</summary>
    private void OnRowMenu(object sender, ContextMenuEventArgs e)
    {
        var border = (FrameworkElement)sender;
        ActionMenus.Fill(border.ContextMenu!, border.DataContext is ArchiveRow row ? RowMenuItems(row) : [], e);
    }

    /// <summary>« ⋯ » : le menu de la fiche, sous le bouton.</summary>
    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ArchiveRow row } button)
        {
            var menu = new ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            foreach (var item in RowMenuItems(row))
            {
                menu.Items.Add(item);
            }
            menu.IsOpen = menu.Items.Count > 0;
            e.Handled = true;
        }
    }

    /// <summary>Un clic sur la ligne, hors de ses boutons qui gardent le leur, ouvre la fiche.</summary>
    private void OnRowClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is ArchivesPageViewModel page && ((FrameworkElement)sender).DataContext is ArchiveRow row)
        {
            page.OpenCommand.Execute(row);
        }
    }

    /// <summary>Les entrées du menu de cette fiche.</summary>
    internal IReadOnlyList<Control> RowMenuItems(ArchiveRow row)
    {
        if (DataContext is not ArchivesPageViewModel page)
        {
            return [];
        }
        // Le menu d'une fiche, le même que partout (rangée 23A) ; la remise en révision et les rappels gardent ceux de la vue.
        var target = new SheetTarget(row.Entry.Id, row.Title) { IsArchived = true, IsExcludedFromReminders = row.IsExcluded, Folder = row.Entry.Folder };
        return SheetMenu.Items(target, new ArchiveSheetMenu(page, row));
    }

    /// <summary>La réponse à la confirmation part vers la vue.</summary>
    private void OnConfirmationClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is ArchivesPageViewModel page)
        {
            page.AnswerRestartCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    /// <summary>Le menu d'une fiche archivée : ce que la vue fait elle-même, le reste par les actions communes.</summary>
    private sealed class ArchiveSheetMenu(ArchivesPageViewModel page, ArchiveRow row) : ISheetMenuHost
    {
        private readonly ISheetMenuHost _shared = page.SheetMenuHost(read: _ => page.OpenCommand.Execute(row));

        public bool CanManage => _shared.CanManage;

        public bool CanShare => _shared.CanShare;

        public void Read(SheetTarget sheet) => _shared.Read(sheet);

        public void Revise(SheetTarget sheet) => _shared.Revise(sheet);

        public void Write(SheetTarget sheet) => _shared.Write(sheet);

        public void PostponeTomorrow(SheetTarget sheet) => _shared.PostponeTomorrow(sheet);

        public void Archive(SheetTarget sheet) => _shared.Archive(sheet);

        public void Restart(SheetTarget sheet) => page.RequestRestartCommand.Execute(row);

        public void ToggleReminders(SheetTarget sheet) => page.ToggleRemindersCommand.Execute(row);

        public void Rename(SheetTarget sheet) => _shared.Rename(sheet);

        public void Tags(SheetTarget sheet) => _shared.Tags(sheet);

        public void Move(SheetTarget sheet) => _shared.Move(sheet);

        public void Print(SheetTarget sheet) => _shared.Print(sheet);

        public void Share(SheetTarget sheet) => _shared.Share(sheet);

        public void SaveAsTemplate(SheetTarget sheet) => _shared.SaveAsTemplate(sheet);

        public void ToggleNote(SheetTarget sheet) => _shared.ToggleNote(sheet);

        public void Trash(SheetTarget sheet) => _shared.Trash(sheet);

        public IReadOnlyList<MenuContribution> Extensions(SheetTarget sheet) => _shared.Extensions(sheet);
    }
}
