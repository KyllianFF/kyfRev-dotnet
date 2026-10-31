using System.Windows;
using System.Windows.Automation;

namespace KyfRev.DesignSystem.Controls;

/// <summary>Propriétés jointes des champs de saisie.</summary>
public static class Field
{
    /// <summary>
    /// Indication affichée tant que le champ est vide (« Rechercher une fiche ») ; le lecteur
    /// d'écran la lit comme aide du champ.
    /// </summary>
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached("Placeholder", typeof(string), typeof(Field), new FrameworkPropertyMetadata(string.Empty, (field, e) => AutomationProperties.SetHelpText(field, (string)e.NewValue)));

    /// <summary>Indication du champ.</summary>
    public static string GetPlaceholder(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string)element.GetValue(PlaceholderProperty);
    }

    /// <summary>Change l'indication du champ.</summary>
    public static void SetPlaceholder(DependencyObject element, string value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(PlaceholderProperty, value);
    }
}
