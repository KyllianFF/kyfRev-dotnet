using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Le cadre qui désigne, dans sa section, le réglage que « Chercher un réglage » vient d'ouvrir : un trait de la couleur
/// principale autour de lui, sans rien déplacer. Il ne reçoit ni la souris ni le clavier.
/// </summary>
internal sealed class SettingPointer : Adorner
{
    /// <summary>L'écart entre le réglage et son cadre.</summary>
    internal const double Gap = 4;

    public SettingPointer(FrameworkElement setting)
        : base(setting)
    {
        IsHitTestVisible = false;
        SetResourceReference(StrokeProperty, "Kyf.Brush.Primary");
    }

    /// <summary>La couleur du trait, prise au thème.</summary>
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(SettingPointer), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>La couleur du trait.</summary>
    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (Stroke is not { } stroke)
        {
            return;
        }
        var frame = new Rect(AdornedElement.RenderSize);
        frame.Inflate(Gap, Gap);
        drawingContext.DrawRoundedRectangle(null, new Pen(stroke, 2), frame, 6, 6);
    }
}
