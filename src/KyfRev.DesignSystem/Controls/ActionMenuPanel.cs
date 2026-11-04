using System.Windows;
using System.Windows.Controls;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// La disposition d'un menu contextuel : les actions rapides côte à côte, à parts égales, en tête ; puis une
/// rangée d'outils, s'il y en a ; tout le reste empilé dessous. Sans eux, c'est une simple pile.
/// </summary>
public sealed class ActionMenuPanel : Panel
{
    /// <summary>L'entrée est une action rapide.</summary>
    public static readonly DependencyProperty IsQuickProperty = DependencyProperty.RegisterAttached(
        "IsQuick", typeof(bool), typeof(ActionMenuPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>L'entrée est un outil de la seconde rangée.</summary>
    public static readonly DependencyProperty IsToolProperty = DependencyProperty.RegisterAttached(
        "IsTool", typeof(bool), typeof(ActionMenuPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

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

    /// <summary>Vrai si <paramref name="element"/> est un outil.</summary>
    public static bool GetIsTool(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsToolProperty);
    }

    /// <summary>Fait de <paramref name="element"/> un outil, ou non.</summary>
    public static void SetIsTool(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsToolProperty, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }
        var (quick, tools, rest) = Sorted();
        var width = rest.Count == 0 ? 0 : rest.Max(child => child.DesiredSize.Width);
        var height = rest.Sum(child => child.DesiredSize.Height);
        foreach (var row in new[] { quick, tools })
        {
            if (row.Count > 0)
            {
                width = Math.Max(width, row.Max(child => child.DesiredSize.Width) * row.Count);
                height += row.Max(child => child.DesiredSize.Height);
            }
        }
        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var (quick, tools, rest) = Sorted();
        var top = 0d;
        foreach (var row in new[] { quick, tools })
        {
            if (row.Count > 0)
            {
                var height = row.Max(child => child.DesiredSize.Height);
                var share = finalSize.Width / row.Count;
                for (var i = 0; i < row.Count; i++)
                {
                    row[i].Arrange(new Rect(i * share, top, share, height));
                }
                top += height;
            }
        }
        foreach (var child in rest)
        {
            child.Arrange(new Rect(0, top, finalSize.Width, child.DesiredSize.Height));
            top += child.DesiredSize.Height;
        }
        return finalSize;
    }

    private (List<UIElement> Quick, List<UIElement> Tools, List<UIElement> Others) Sorted()
    {
        var (quick, tools, rest) = (new List<UIElement>(), new List<UIElement>(), new List<UIElement>());
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }
            (GetIsQuick(child) ? quick : GetIsTool(child) ? tools : rest).Add(child);
        }
        return (quick, tools, rest);
    }
}
