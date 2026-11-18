using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KyfRev.DesignSystem;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Le logotype de kyfRev : son icône, puis son nom en deux tons dans la police de l'application, « kyf » de la couleur du
/// texte et « Rev » de la couleur principale. Il suit le thème, et se règle par la taille de son icône.
/// </summary>
internal sealed class Logotype : StackPanel
{
    /// <summary>La taille de l'icône ; le nom et l'écart qui les sépare suivent.</summary>
    public static readonly DependencyProperty MarkSizeProperty = DependencyProperty.Register(
        nameof(MarkSize), typeof(double), typeof(Logotype), new PropertyMetadata(48d, (element, _) => ((Logotype)element).Size()));

    private readonly BrandMark _mark = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _name = new() { VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold };

    public Logotype()
    {
        Orientation = Orientation.Horizontal;
        var rev = new Run("Rev");
        rev.SetResourceReference(TextElement.ForegroundProperty, "Kyf.Brush.Primary");
        _name.Inlines.Add(new Run("kyf"));
        _name.Inlines.Add(rev);
        _name.SetResourceReference(TextBlock.FontFamilyProperty, DesignKeys.DisplayFont);
        _name.SetResourceReference(TextBlock.ForegroundProperty, "Kyf.Brush.Text");
        Children.Add(_mark);
        Children.Add(_name);
        Size();
    }

    /// <inheritdoc cref="MarkSizeProperty"/>
    public double MarkSize
    {
        get => (double)GetValue(MarkSizeProperty);
        set => SetValue(MarkSizeProperty, value);
    }

    /// <summary>Le nom, tel qu'il s'écrit.</summary>
    internal string Wordmark => string.Concat(_name.Inlines.OfType<Run>().Select(run => run.Text));

    private void Size()
    {
        _mark.Width = _mark.Height = MarkSize;
        _mark.Margin = new Thickness(0, 0, Math.Round(MarkSize / 4), 0);
        _name.FontSize = Math.Round(MarkSize * 0.8);
    }
}
