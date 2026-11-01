using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Visible dans une seule disposition de la navigation, nommée en paramètre (« Sidebar »…) ;
/// masqué dans les autres : les libellés de la barre latérale disparaissent de la colonne d'icônes.
/// </summary>
public sealed class LayoutVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is NavigationLayout layout && Enum.TryParse<NavigationLayout>(parameter as string, out var shown) && layout == shown
            ? Visibility.Visible
            : Visibility.Collapsed;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Alignement du contenu d'un bouton du menu : centré dans la colonne d'icônes, sinon celui donné
/// en paramètre (« Left », « Stretch »).
/// </summary>
public sealed class LayoutAlignmentConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is NavigationLayout.Rail
            ? HorizontalAlignment.Center
            : Enum.TryParse<HorizontalAlignment>(parameter as string, out var alignment) ? alignment : HorizontalAlignment.Left;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
