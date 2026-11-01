using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Le bouton d'un filtre et son menu.</summary>
internal sealed partial class ListFilterButton : UserControl
{
    public ListFilterButton()
    {
        InitializeComponent();
    }

    private void OnFilterClick(object sender, RoutedEventArgs e)
    {
        var menu = FilterButton.ContextMenu;
        menu.PlacementTarget = FilterButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnFilterChosen(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu { DataContext: ListFilter filter } && e.OriginalSource is MenuItem { DataContext: Choice<object?> choice })
        {
            filter.Selected = choice;
        }
    }
}
