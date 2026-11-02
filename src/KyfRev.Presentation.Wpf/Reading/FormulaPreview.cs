using System.Windows;
using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>L'aperçu d'une formule en cours de saisie, dessinée comme en lecture.</summary>
internal sealed class FormulaPreview : ContentControl
{
    /// <summary>La formule, en LaTeX.</summary>
    public static readonly DependencyProperty TexProperty = DependencyProperty.Register(
        nameof(Tex), typeof(string), typeof(FormulaPreview), new PropertyMetadata(string.Empty, OnChanged));

    private const double Size = 22;

    /// <inheritdoc cref="TexProperty"/>
    public string Tex
    {
        get => (string)GetValue(TexProperty);
        set => SetValue(TexProperty, value);
    }

    private static void OnChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var preview = (FormulaPreview)sender;
        var tex = preview.Tex.Trim();
        preview.Content = tex.Length == 0 ? null : DocumentBuilder.FormulaOrText(tex, Size, block: true);
    }
}
