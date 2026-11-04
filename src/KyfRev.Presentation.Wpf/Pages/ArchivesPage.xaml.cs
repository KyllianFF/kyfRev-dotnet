using System.Windows;
using System.Windows.Controls;
using KyfRev.DesignSystem.Controls;

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

    /// <summary>Les entrées du menu de cette fiche.</summary>
    internal IReadOnlyList<Control> RowMenuItems(ArchiveRow row)
    {
        if (DataContext is not ArchivesPageViewModel page)
        {
            return [];
        }
        return
        [
            ActionMenus.Quick(ArchivesText.Open, Icons.Document, () => page.OpenCommand.Execute(row)),
            ActionMenus.Quick(ArchivesText.Restart, Icons.Undo, () => page.RequestRestartCommand.Execute(row)),
            new Separator(),
            ActionMenus.Item(row.ToggleLabel, () => page.ToggleRemindersCommand.Execute(row)),
        ];
    }

    /// <summary>La réponse à la confirmation part vers la vue.</summary>
    private void OnConfirmationClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is ArchivesPageViewModel page)
        {
            page.AnswerRestartCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }
}
