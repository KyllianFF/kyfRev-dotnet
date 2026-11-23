using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Vrai : montré ; faux : caché en gardant sa place ; rien : absent. Une bande qui ne doit pas bouger cache un bouton
/// sans rendre sa place (rangée 27A).
/// </summary>
public sealed class ShownConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        true => Visibility.Visible,
        false => Visibility.Hidden,
        _ => Visibility.Collapsed,
    };

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
