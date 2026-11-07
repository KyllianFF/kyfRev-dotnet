using System.Windows;
using System.Windows.Controls;

namespace KyfRev.DesignSystem.Controls;

/// <summary>La place d'un élément dans une <see cref="AdaptiveRow"/>.</summary>
public enum AdaptiveRowPart
{
    /// <summary>Le texte : il prend la place qui reste sur la ligne, et décide de la disposition.</summary>
    Main,

    /// <summary>Avant le texte sur la ligne ; sous lui une fois empilé.</summary>
    Leading,

    /// <summary>Après le texte sur la ligne, calé à droite ; en dernier une fois empilé.</summary>
    Trailing,
}

/// <summary>
/// Une ligne en trois parts (ce qui précède, le texte, ce qui suit) tant que le texte y garde <see cref="MainMinWidth"/>
/// pixels. Sinon les parts s'empilent, le texte d'abord : un texte n'est jamais réduit à quelques lettres par ligne entre
/// deux voisins qui gardent toute leur largeur.
/// </summary>
public sealed class AdaptiveRow : Panel
{
    /// <summary>La part de l'élément : le texte (par défaut), ce qui le précède ou ce qui le suit.</summary>
    public static readonly DependencyProperty PartProperty = DependencyProperty.RegisterAttached(
        "Part", typeof(AdaptiveRowPart), typeof(AdaptiveRow), new FrameworkPropertyMetadata(AdaptiveRowPart.Main, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>La largeur que le texte doit garder sur la ligne ; en deçà, les parts s'empilent.</summary>
    public static readonly DependencyProperty MainMinWidthProperty = DependencyProperty.Register(
        nameof(MainMinWidth), typeof(double), typeof(AdaptiveRow), new FrameworkPropertyMetadata(240d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>L'écart entre les parts, sur la ligne.</summary>
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(AdaptiveRow), new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>L'écart entre les parts, une fois empilées.</summary>
    public static readonly DependencyProperty StackedSpacingProperty = DependencyProperty.Register(
        nameof(StackedSpacing), typeof(double), typeof(AdaptiveRow), new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private static readonly DependencyPropertyKey IsStackedKey = DependencyProperty.RegisterReadOnly(
        nameof(IsStacked), typeof(bool), typeof(AdaptiveRow), new PropertyMetadata(false));

    /// <summary>Les parts sont empilées : la ligne laissait trop peu de place au texte.</summary>
    public static readonly DependencyProperty IsStackedProperty = IsStackedKey.DependencyProperty;

    /// <summary>La largeur que le texte doit garder sur la ligne.</summary>
    public double MainMinWidth
    {
        get => (double)GetValue(MainMinWidthProperty);
        set => SetValue(MainMinWidthProperty, value);
    }

    /// <summary>L'écart entre les parts, sur la ligne.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>L'écart entre les parts, une fois empilées.</summary>
    public double StackedSpacing
    {
        get => (double)GetValue(StackedSpacingProperty);
        set => SetValue(StackedSpacingProperty, value);
    }

    /// <summary>Les parts sont empilées.</summary>
    public bool IsStacked => (bool)GetValue(IsStackedProperty);

    /// <summary>La part de <paramref name="element"/>.</summary>
    public static AdaptiveRowPart GetPart(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (AdaptiveRowPart)element.GetValue(PartProperty);
    }

    /// <summary>Donne sa part à <paramref name="element"/>.</summary>
    public static void SetPart(DependencyObject element, AdaptiveRowPart value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(PartProperty, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var sides = Shown().Where(child => GetPart(child) != AdaptiveRowPart.Main).ToList();
        var mains = Shown().Where(child => GetPart(child) == AdaptiveRowPart.Main).ToList();
        foreach (var side in sides)
        {
            side.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        }
        var gaps = Spacing * Math.Max(0, sides.Count + mains.Count - 1);
        var room = availableSize.Width - sides.Sum(side => side.DesiredSize.Width) - gaps;
        var stacked = !double.IsPositiveInfinity(availableSize.Width) && mains.Count > 0 && room < MainMinWidth;
        SetValue(IsStackedKey, stacked);
        if (!stacked)
        {
            foreach (var main in mains)
            {
                main.Measure(new Size(Math.Max(0, room / mains.Count), availableSize.Height));
            }
            var all = Shown().ToList();
            return new Size(all.Sum(child => child.DesiredSize.Width) + gaps, all.Count == 0 ? 0 : all.Max(child => child.DesiredSize.Height));
        }
        var ordered = Ordered();
        foreach (var child in ordered)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }
        return new Size(
            ordered.Max(child => child.DesiredSize.Width),
            ordered.Sum(child => child.DesiredSize.Height) + (StackedSpacing * (ordered.Count - 1)));
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (IsStacked)
        {
            var top = 0d;
            foreach (var child in Ordered())
            {
                child.Arrange(new Rect(0, top, finalSize.Width, child.DesiredSize.Height));
                top += child.DesiredSize.Height + StackedSpacing;
            }
            return finalSize;
        }
        var leading = Shown().Where(child => GetPart(child) == AdaptiveRowPart.Leading).ToList();
        var mains = Shown().Where(child => GetPart(child) == AdaptiveRowPart.Main).ToList();
        var trailing = Shown().Where(child => GetPart(child) == AdaptiveRowPart.Trailing).ToList();
        var parts = leading.Count + mains.Count + trailing.Count;
        var room = finalSize.Width - leading.Concat(trailing).Sum(child => child.DesiredSize.Width) - (Spacing * Math.Max(0, parts - 1));
        var left = 0d;
        foreach (var child in leading)
        {
            child.Arrange(new Rect(left, 0, child.DesiredSize.Width, finalSize.Height));
            left += child.DesiredSize.Width + Spacing;
        }
        foreach (var child in mains)
        {
            var width = Math.Max(0, room / mains.Count);
            child.Arrange(new Rect(left, 0, width, finalSize.Height));
            left += width + Spacing;
        }
        var right = finalSize.Width;
        foreach (var child in Enumerable.Reverse(trailing))
        {
            right -= child.DesiredSize.Width;
            child.Arrange(new Rect(right, 0, child.DesiredSize.Width, finalSize.Height));
            right -= Spacing;
        }
        return finalSize;
    }

    private IEnumerable<UIElement> Shown() => InternalChildren.Cast<UIElement>().Where(child => child.Visibility != Visibility.Collapsed);

    /// <summary>L'ordre empilé : le texte, puis ce qui le précède, puis ce qui le suit.</summary>
    private List<UIElement> Ordered() => [.. Shown().OrderBy(child => GetPart(child))];
}
