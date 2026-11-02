using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Le pinceau d'une couleur écrite <c>#rrggbb</c> ; rien, sans couleur.</summary>
internal sealed class ColorBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string written)
        {
            return null;
        }
        var brush = new SolidColorBrush(TokenColor.FromHex(written).ToColor());
        brush.Freeze();
        return brush;
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
