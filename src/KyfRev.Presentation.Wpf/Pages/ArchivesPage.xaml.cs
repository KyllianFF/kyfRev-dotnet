using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

    /// <summary>Un filtre : ses choix, dans un menu ouvert sous le bouton.</summary>
    private void OnFilterClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var menu = button.ContextMenu;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnFilterChosen(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu { DataContext: ArchiveFilter filter } && e.OriginalSource is MenuItem { DataContext: Choice<object?> choice })
        {
            filter.Selected = choice;
        }
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
