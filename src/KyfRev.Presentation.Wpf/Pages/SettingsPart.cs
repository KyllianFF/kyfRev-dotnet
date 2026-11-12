using System.Windows;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Une page de paramètres sert parfois plusieurs sections (« Apparence » et « Lecture », par exemple) : chacune de ses
/// cartes dit à quelle section elle appartient (<see cref="OfProperty"/>), et la vue « Paramètres » dit laquelle est
/// ouverte (<see cref="CurrentProperty"/>). Une carte d'une autre section ne se montre pas. Hors de cette vue, où aucune
/// section n'est dite, tout se montre.
/// </summary>
public static class SettingsPart
{
    /// <summary>La section ouverte : elle se transmet à tout ce que la vue contient.</summary>
    public static readonly DependencyProperty CurrentProperty = DependencyProperty.RegisterAttached(
        "Current", typeof(string), typeof(SettingsPart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (element, _) => Apply(element)));

    /// <summary>La section à laquelle appartient un élément.</summary>
    public static readonly DependencyProperty OfProperty = DependencyProperty.RegisterAttached(
        "Of", typeof(string), typeof(SettingsPart), new PropertyMetadata(null, (element, _) => Apply(element)));

    /// <summary>La section ouverte.</summary>
    public static string? GetCurrent(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string?)element.GetValue(CurrentProperty);
    }

    /// <summary>Dit quelle section est ouverte.</summary>
    public static void SetCurrent(DependencyObject element, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(CurrentProperty, value);
    }

    /// <summary>La section d'un élément.</summary>
    public static string? GetOf(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string?)element.GetValue(OfProperty);
    }

    /// <summary>Dit à quelle section appartient un élément.</summary>
    public static void SetOf(DependencyObject element, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(OfProperty, value);
    }

    private static void Apply(DependencyObject element)
    {
        if (element is UIElement shown && GetOf(shown) is { Length: > 0 } part)
        {
            var current = GetCurrent(shown);
            shown.Visibility = string.IsNullOrEmpty(current) || current == part ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
