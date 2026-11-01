using System.Globalization;
using System.Windows.Data;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Vrai si une largeur atteint le seuil donné en paramètre (« 720 ») : une vue range ses éléments
/// autrement quand la place manque, sans jamais défiler horizontalement.
/// </summary>
public sealed class WidthAtLeastConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double width && parameter is string threshold && width >= double.Parse(threshold, CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
