using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue de la barre de révision.</summary>
internal sealed partial class ReviewBar : UserControl
{
    public ReviewBar()
    {
        InitializeComponent();
    }

    /// <summary>« Reporter » et « ⋯ » ouvrent leur menu au-dessus du bouton, la barre étant en bas ; le clavier y entre.</summary>
    private void OnMenuButton(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Top;
        button.ContextMenu.IsOpen = true;
    }

    /// <summary>Le champ de date prend le focus dès qu'il paraît.</summary>
    private void OnDayFormVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => ChosenDay.Focus());
        }
    }
}
