using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KyfRev.DesignSystem.Controls;

/// <summary>Menus (rangée 18C des maquettes) : un menu grandit depuis l'endroit d'où il s'ouvre.</summary>
public static partial class Transitions
{
    /// <summary>L'échelle d'où part un menu qui s'ouvre (96 %).</summary>
    public const double MenuFrom = 0.96;

    /// <summary>Sur un menu contextuel : à l'ouverture, il grandit de 96 % à sa taille depuis son coin, en fondu.</summary>
    public static readonly DependencyProperty GrowsOnOpenProperty =
        DependencyProperty.RegisterAttached("GrowsOnOpen", typeof(bool), typeof(Transitions), new PropertyMetadata(false, OnGrowsOnOpen));

    /// <summary>Lit <see cref="GrowsOnOpenProperty"/>.</summary>
    public static bool GetGrowsOnOpen(DependencyObject element) => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(GrowsOnOpenProperty);

    /// <summary>Écrit <see cref="GrowsOnOpenProperty"/>.</summary>
    public static void SetGrowsOnOpen(DependencyObject element, bool value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(GrowsOnOpenProperty, value);

    private static void OnGrowsOnOpen(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is ContextMenu menu)
        {
            menu.Opened -= OnMenuOpened;
            if (e.NewValue is true)
            {
                menu.Opened += OnMenuOpened;
            }
        }
    }

    /// <summary>Le menu vient de s'ouvrir : son cadre grandit depuis son coin haut gauche, là où il s'accroche.</summary>
    private static void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        var menu = (ContextMenu)sender;
        if (VisualTreeHelper.GetChildrenCount(menu) > 0 && VisualTreeHelper.GetChild(menu, 0) is FrameworkElement frame)
        {
            Grow(frame, MenuFrom, new Point(0, 0));
        }
    }
}
