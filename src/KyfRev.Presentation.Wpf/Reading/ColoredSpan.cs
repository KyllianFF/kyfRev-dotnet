using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Un passage coloré par la fiche : sa teinte reste, sa clarté s'adapte au fond du thème, et le suit quand il change.
/// En contraste élevé, il garde la couleur du thème.
/// </summary>
internal sealed class ColoredSpan : Span
{
    private static readonly DependencyProperty GroundProperty = DependencyProperty.Register(
        "Ground", typeof(Color), typeof(ColoredSpan), new PropertyMetadata(default(Color), OnThemeChanged));

    private static readonly DependencyProperty IsHighContrastProperty = DependencyProperty.Register(
        "IsHighContrast", typeof(bool), typeof(ColoredSpan), new PropertyMetadata(false, OnThemeChanged));

    private readonly TokenColor _color;

    public ColoredSpan(TokenColor color)
    {
        _color = color;
        SetResourceReference(GroundProperty, DesignKeys.Color(ColorRole.Background));
        SetResourceReference(IsHighContrastProperty, DesignKeys.IsHighContrast);
    }

    private static void OnThemeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var span = (ColoredSpan)sender;
        var ground = (Color)span.GetValue(GroundProperty);
        if ((bool)span.GetValue(IsHighContrastProperty) || ground.A != byte.MaxValue)
        {
            span.ClearValue(TextElement.ForegroundProperty);
            return;
        }
        var readable = span._color.ReadableOn(new TokenColor(ground.A, ground.R, ground.G, ground.B));
        var brush = new SolidColorBrush(readable.ToColor());
        brush.Freeze();
        span.Foreground = brush;
    }
}
