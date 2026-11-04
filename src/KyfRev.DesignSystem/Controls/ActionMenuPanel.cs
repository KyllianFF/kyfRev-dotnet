using System.Windows;
using System.Windows.Controls;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// La disposition d'un menu contextuel : les actions rapides côte à côte, à parts égales, en tête ;
/// tout le reste empilé dessous. Sans action rapide, c'est une simple pile.
/// </summary>
public sealed class ActionMenuPanel : Panel
{
    /// <summary>L'entrée est une action rapide.</summary>
    public static readonly DependencyProperty IsQuickProperty = DependencyProperty.RegisterAttached(
        "IsQuick", typeof(bool), typeof(ActionMenuPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>Vrai si <paramref name="element"/> est une action rapide.</summary>
    public static bool GetIsQuick(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsQuickProperty);
    }

    /// <summary>Fait de <paramref name="element"/> une action rapide, ou non.</summary>
    public static void SetIsQuick(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsQuickProperty, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var (quickCount, quickWidth, quickHeight, width, height) = (0, 0d, 0d, 0d, 0d);
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            if (child.Visibility != Visibility.Collapsed && GetIsQuick(child))
            {
                quickCount++;
                quickWidth = Math.Max(quickWidth, child.DesiredSize.Width);
                quickHeight = Math.Max(quickHeight, child.DesiredSize.Height);
            }
            else
            {
                width = Math.Max(width, child.DesiredSize.Width);
                height += child.DesiredSize.Height;
            }
        }
        return new Size(Math.Max(width, quickWidth * quickCount), height + quickHeight);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var quick = InternalChildren.Cast<UIElement>().Where(child => child.Visibility != Visibility.Collapsed && GetIsQuick(child)).ToList();
        var quickHeight = quick.Count == 0 ? 0 : quick.Max(child => child.DesiredSize.Height);
        var left = 0d;
        foreach (var child in quick)
        {
            var share = finalSize.Width / quick.Count;
            child.Arrange(new Rect(left, 0, share, quickHeight));
            left += share;
        }
        var top = quickHeight;
        foreach (UIElement child in InternalChildren)
        {
            if (!quick.Contains(child))
            {
                child.Arrange(new Rect(0, top, finalSize.Width, child.DesiredSize.Height));
                top += child.DesiredSize.Height;
            }
        }
        return finalSize;
    }
}
