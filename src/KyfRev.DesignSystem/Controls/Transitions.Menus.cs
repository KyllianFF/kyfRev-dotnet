using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Menus (rangée 18C des maquettes) : un menu contextuel, une liste déroulante, un sous-menu grandissent depuis leur coin
/// en s'ouvrant, en fondu, sur la durée d'ouverture.
/// </summary>
public static partial class Transitions
{
    /// <summary>L'échelle d'où part un menu qui s'ouvre (92 %) : assez pour qu'on le voie naître de son bouton.</summary>
    public const double MenuFrom = 0.92;

    /// <summary>Sur un menu contextuel ou une fenêtre surgissante : à l'ouverture, il grandit depuis son coin, en fondu.</summary>
    public static readonly DependencyProperty GrowsOnOpenProperty =
        DependencyProperty.RegisterAttached("GrowsOnOpen", typeof(bool), typeof(Transitions), new PropertyMetadata(false, OnGrowsOnOpen));

    /// <summary>Lit <see cref="GrowsOnOpenProperty"/>.</summary>
    public static bool GetGrowsOnOpen(DependencyObject element) => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(GrowsOnOpenProperty);

    /// <summary>Écrit <see cref="GrowsOnOpenProperty"/>.</summary>
    public static void SetGrowsOnOpen(DependencyObject element, bool value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(GrowsOnOpenProperty, value);

    private static void OnGrowsOnOpen(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        switch (element)
        {
            case ContextMenu menu:
                menu.Opened -= OnMenuOpened;
                if (e.NewValue is true)
                {
                    menu.Opened += OnMenuOpened;
                }
                break;
            case Popup popup:
                popup.Opened -= OnPopupOpened;
                if (e.NewValue is true)
                {
                    popup.Opened += OnPopupOpened;
                }
                break;
        }
    }

    /// <summary>
    /// Le menu vient de s'ouvrir : son cadre grandit depuis son coin haut gauche, là où il s'accroche. Un menu fait à la
    /// volée n'a pas encore son modèle à la première ouverture : il le reçoit d'abord.
    /// </summary>
    private static void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        var menu = (ContextMenu)sender;
        menu.ApplyTemplate();
        GrowFrame(menu);
    }

    /// <summary>Une fenêtre surgissante (une liste déroulante, un sous-menu) vient de s'ouvrir : son contenu grandit.</summary>
    private static void OnPopupOpened(object? sender, EventArgs e)
    {
        if (sender is Popup { Child: FrameworkElement child })
        {
            Grow(child, MenuFrom, new Point(0, 0), DesignKeys.NormalDuration, MotionKinds.Surfaces);
        }
    }

    /// <summary>Le cadre du menu (la racine de son modèle) grandit ; s'il n'est pas encore là, au prochain tour, une fois posé.</summary>
    internal static bool GrowFrame(Control menu)
    {
        if (VisualTreeHelper.GetChildrenCount(menu) > 0 && VisualTreeHelper.GetChild(menu, 0) is FrameworkElement frame)
        {
            return Grow(frame, MenuFrom, new Point(0, 0), DesignKeys.NormalDuration, MotionKinds.Surfaces);
        }
        menu.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (VisualTreeHelper.GetChildrenCount(menu) > 0 && VisualTreeHelper.GetChild(menu, 0) is FrameworkElement later)
            {
                Grow(later, MenuFrom, new Point(0, 0), DesignKeys.NormalDuration, MotionKinds.Surfaces);
            }
        });
        return false;
    }
}
