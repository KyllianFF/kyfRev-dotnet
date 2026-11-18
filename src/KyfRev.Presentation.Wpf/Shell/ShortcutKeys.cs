using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Les touches en vigueur, telles qu'une vue les lit : une infobulle ou un nom d'accessibilité qui s'y lie nomme la
/// touche choisie sur cet ordinateur, et la suit quand elle change.
/// </summary>
public sealed class ShortcutKeys : INotifyPropertyChanged
{
    private ShortcutKeys()
    {
        ShortcutMap.Changed += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
    }

    /// <summary>Les touches en vigueur.</summary>
    public static ShortcutKeys Current { get; } = new();

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>La touche d'une action, telle qu'on la lit : « Ctrl+Maj+P ».</summary>
    public string this[string id] => ShortcutMap.Current.DisplayOf(id);

    /// <summary>Un texte qui nomme la touche d'une action : « Imprimer la fiche ({0}) ».</summary>
    public static string In(string format, string id) =>
        string.Format(CultureInfo.CurrentCulture, format, ShortcutMap.Current.DisplayOf(id));
}

/// <summary>
/// Dans une vue, la touche en vigueur d'une action : <c>{shell:Shortcut imprimer}</c>, ou un texte qui la nomme avec
/// <see cref="Format"/>.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class ShortcutExtension : MarkupExtension
{
    /// <summary>Sans action : elle se donne par <see cref="Id"/>.</summary>
    public ShortcutExtension()
    {
    }

    /// <summary>La touche de cette action.</summary>
    public ShortcutExtension(string id)
    {
        Id = id;
    }

    /// <summary>Le nom écrit de l'action (voir <see cref="ShortcutMap.Actions"/>).</summary>
    [ConstructorArgument("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Le texte où la touche prend la place de « {0} » ; sans lui, la touche seule.</summary>
    public string? Format { get; set; }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Id}]")
        {
            Source = ShortcutKeys.Current,
            Mode = BindingMode.OneWay,
            Converter = Format is null ? null : new Named(Format),
        }.ProvideValue(serviceProvider);

    private sealed class Named(string format) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            string.Format(culture, format, value);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
