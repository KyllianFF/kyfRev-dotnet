using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue de la barre de révision.</summary>
internal sealed partial class ReviewBar : UserControl
{
    public ReviewBar()
    {
        InitializeComponent();
    }

    /// <summary>« ⋯ » ouvre les autres actions de révision sous le bouton ; le clavier y entre.</summary>
    private void OnMore(object sender, RoutedEventArgs e)
    {
        MoreMenu.PlacementTarget = MoreButton;
        MoreMenu.Placement = PlacementMode.Bottom;
        MoreMenu.IsOpen = true;
    }
}
