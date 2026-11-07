using System.Globalization;
using System.Windows.Data;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Un choix de l'onglet Tableau est celui du tableau : sa valeur, écrite comme dans la fiche, est le paramètre.</summary>
internal sealed class TableChoiceConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && string.Equals(System.Convert.ToString(value, CultureInfo.InvariantCulture), parameter as string, StringComparison.Ordinal);

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
