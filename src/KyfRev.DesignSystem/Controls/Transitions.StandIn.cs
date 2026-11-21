using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Le squelette qui tient la place d'un contenu en cours de lecture (rangée 20D des maquettes) : quand le contenu est là, il
/// ne disparaît plus d'un coup. Il rend sa place tout de suite, mais s'efface encore un instant sous le contenu, qui paraît
/// en fondu par-dessus lui.
/// </summary>
public static partial class Transitions
{
    /// <summary>
    /// Sur un squelette : vrai tant qu'il tient la place du contenu (« IsLoading »). Quand la valeur devient fausse, il rend
    /// sa place et s'efface, pendant que ce qui le suit dans le même panneau paraît en fondu. Remplace une liaison sur
    /// <see cref="UIElement.Visibility"/>.
    /// </summary>
    public static readonly DependencyProperty StandsInProperty =
        DependencyProperty.RegisterAttached("StandsIn", typeof(bool), typeof(Transitions), new PropertyMetadata(true, OnStandsIn));

    /// <summary>Lit <see cref="StandsInProperty"/>.</summary>
    public static bool GetStandsIn(DependencyObject element) => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(StandsInProperty);

    /// <summary>Écrit <see cref="StandsInProperty"/>.</summary>
    public static void SetStandsIn(DependencyObject element, bool value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(StandsInProperty, value);

    // La marge du squelette avant qu'il ne rende sa place ; rien s'il la tient.
    private static readonly DependencyProperty StandInMarginProperty =
        DependencyProperty.RegisterAttached("StandInMargin", typeof(Thickness?), typeof(Transitions), new PropertyMetadata(null));

    private static void OnStandsIn(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not FrameworkElement element)
        {
            return;
        }
        // Un départ en cours s'arrête : le squelette retrouve sa marge et son opacité.
        TakePlaceBack(element);
        if ((bool)e.NewValue)
        {
            element.Visibility = Visibility.Visible;
            return;
        }
        if (!element.IsLoaded || !element.IsVisible || element.ActualHeight <= 0 || !Moves(element, DesignKeys.FastDuration, out _, MotionKinds.Views))
        {
            element.Visibility = Visibility.Collapsed;
            return;
        }
        // Il rend sa place sans quitter l'écran : sa marge du bas annule sa hauteur, et ce qui le suit vient se poser sur lui.
        var margin = element.Margin;
        element.SetValue(StandInMarginProperty, margin);
        element.Margin = new Thickness(margin.Left, margin.Top, margin.Right, -(element.ActualHeight + margin.Top));
        element.IsHitTestVisible = false;
        Exit(
            element,
            () =>
            {
                if (element.GetValue(StandInMarginProperty) is Thickness)
                {
                    element.Visibility = Visibility.Collapsed;
                    TakePlaceBack(element);
                }
            },
            DesignKeys.FastDuration,
            kind: MotionKinds.Views);
        // Une fois les liaisons du contenu relues et la mise en page faite : ce qui suit le squelette paraît en fondu.
        element.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => Reveal(element));
    }

    /// <summary>Ce qui suit le squelette dans son panneau, et se voit, paraît en fondu.</summary>
    /// <returns>Combien d'éléments paraissent.</returns>
    internal static int Reveal(FrameworkElement standIn)
    {
        if (GetStandsIn(standIn) || System.Windows.Media.VisualTreeHelper.GetParent(standIn) is not Panel panel)
        {
            return 0;
        }
        var revealed = 0;
        for (var index = panel.Children.IndexOf(standIn) + 1; index < panel.Children.Count; index++)
        {
            if (panel.Children[index] is FrameworkElement { IsVisible: true } next && Enter(next, DesignKeys.NormalDuration, kind: MotionKinds.Views))
            {
                revealed++;
            }
        }
        return revealed;
    }

    /// <summary>Le squelette retrouve sa marge, son opacité et ses clics : il tient de nouveau sa place, ou l'a quittée pour de bon.</summary>
    private static void TakePlaceBack(FrameworkElement element)
    {
        if (element.GetValue(StandInMarginProperty) is not Thickness margin)
        {
            return;
        }
        element.ClearValue(StandInMarginProperty);
        element.Margin = margin;
        element.ClearValue(UIElement.IsHitTestVisibleProperty);
        StopExit(element);
    }
}
