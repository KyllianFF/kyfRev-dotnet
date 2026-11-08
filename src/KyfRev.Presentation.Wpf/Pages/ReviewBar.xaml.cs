using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue de la barre de révision.</summary>
internal sealed partial class ReviewBar : UserControl
{
    /// <summary>Le bouton dont le menu a ouvert le champ de date : le focus y revient.</summary>
    private Button? _menuButton;

    public ReviewBar()
    {
        InitializeComponent();
    }

    /// <summary>« Reporter » et « ⋯ » ouvrent leur menu au-dessus du bouton, la barre étant en bas ; le clavier y entre.</summary>
    private void OnMenuButton(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        _menuButton = button;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Top;
        button.ContextMenu.IsOpen = true;
    }

    /// <summary>Le champ de date prend le focus dès qu'il paraît, et le rend quand il se referme.</summary>
    private void OnDayFormVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => ChosenDay.Focus());
        }
        else
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, GiveFocusBack);
        }
    }

    /// <summary>Un refus laisse le champ ouvert : la date reprend le focus, que son bouton a perdu le temps de l'essai.</summary>
    private void OnErrorVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue && DayForm.IsVisible)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (KeyboardFocus.IsLost() || DayForm.IsKeyboardFocusWithin)
                {
                    ChosenDay.Focus();
                }
            });
        }
    }

    /// <summary>Le focus resté dans le champ revient au bouton qui l'a ouvert ; archivée par cette révision, la barre n'a plus que la remise en révision.</summary>
    private void GiveFocusBack()
    {
        if (!IsVisible || !KeyboardFocus.IsLost())
        {
            return;
        }
        var opener = _menuButton ?? MoreButton;
        (opener.IsVisible ? opener : RestartArchived).Focus();
    }
}
