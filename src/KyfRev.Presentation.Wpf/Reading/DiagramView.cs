using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Rendering.Diagrams;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>Un dessin de fiche, à la largeur qu'on lui laisse ; ses couleurs sont celles du thème, et le suivent.</summary>
internal sealed class DiagramView : FrameworkElement
{
    /// <summary>Le dessin.</summary>
    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene),
        typeof(DiagramScene),
        typeof(DiagramView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnSceneChanged));

    /// <summary>Une vignette : trop petite pour se lire, son texte est figuré par des traits.</summary>
    public static readonly DependencyProperty IsThumbnailProperty = DependencyProperty.Register(
        nameof(IsThumbnail),
        typeof(bool),
        typeof(DiagramView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register(
        "ThemeProbe",
        typeof(Brush),
        typeof(DiagramView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double ArrowLength = 10;
    private const double LineSpacing = 1.2;
    private const double ThumbnailBarHeight = 12;
    private const double ThumbnailBarWidth = 56;

    public DiagramView()
    {
        SetResourceReference(ThemeProbeProperty, DesignKeys.Brush(ColorRole.Text));
    }

    /// <inheritdoc cref="SceneProperty"/>
    public DiagramScene? Scene
    {
        get => (DiagramScene?)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <inheritdoc cref="IsThumbnailProperty"/>
    public bool IsThumbnail
    {
        get => (bool)GetValue(IsThumbnailProperty);
        set => SetValue(IsThumbnailProperty, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Scene is not { } scene)
        {
            return default;
        }
        var width = double.IsInfinity(availableSize.Width) ? scene.Width : availableSize.Width;
        return new Size(width, width * scene.Height / scene.Width);
    }

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (Scene is not { } scene)
        {
            return;
        }
        var scale = ActualWidth / scene.Width;
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        foreach (var shape in scene.Shapes)
        {
            Draw(drawingContext, shape);
        }
        drawingContext.Pop();
    }

    private static void OnSceneChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is DiagramScene scene)
        {
            AutomationProperties.SetName(sender, scene.Description);
        }
    }

    private Brush Find(ColorRole role) => TryFindResource(DesignKeys.Brush(role)) as Brush ?? (Brush)GetValue(ThemeProbeProperty);

    private (Brush Fill, Pen Outline) Paint(DiagramTone tone) => tone switch
    {
        DiagramTone.Main => (Find(ColorRole.NavigationActiveBackground), new Pen(Find(ColorRole.Primary), 1)),
        DiagramTone.Accent => (Find(ColorRole.PillPassageBackground), new Pen(Find(ColorRole.PillPassageText), 1)),
        _ => (Find(ColorRole.SurfaceAlt), new Pen(Find(ColorRole.TextSecondary), 1)),
    };

    private Brush TextBrush(DiagramTone? tone) => tone switch
    {
        DiagramTone.Main => Find(ColorRole.NavigationActiveText),
        DiagramTone.Accent => Find(ColorRole.PillPassageText),
        _ => Find(ColorRole.Text),
    };

    private void Draw(DrawingContext context, DiagramShape shape)
    {
        switch (shape)
        {
            case DiagramRectangle box:
                var (boxFill, boxOutline) = Paint(box.Tone);
                context.DrawRoundedRectangle(boxFill, boxOutline, new Rect(box.X, box.Y, box.Width, box.Height), box.Radius, box.Radius);
                break;
            case DiagramEllipse ellipse:
                var (ellipseFill, ellipseOutline) = Paint(ellipse.Tone);
                context.PushOpacity(ellipse.Opacity);
                context.DrawEllipse(ellipseFill, ellipseOutline, new Point(ellipse.CenterX, ellipse.CenterY), ellipse.RadiusX, ellipse.RadiusY);
                context.Pop();
                break;
            case DiagramPolygon polygon:
                var (polygonFill, polygonOutline) = Paint(polygon.Tone);
                context.DrawGeometry(polygonFill, polygonOutline, Outline([.. polygon.Points.Select(point => new Point(point.X, point.Y))]));
                break;
            case DiagramLine line:
                DrawLine(context, line);
                break;
            case DiagramText text:
                DrawText(context, text);
                break;
        }
    }

    private void DrawLine(DrawingContext context, DiagramLine line)
    {
        var brush = Find(ColorRole.TextSecondary);
        var (from, to) = (new Point(line.From.X, line.From.Y), new Point(line.To.X, line.To.Y));
        context.DrawLine(new Pen(brush, 1.5), from, to);
        if (line.HasArrow)
        {
            var angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
            Point Barb(double side) => new(to.X - (ArrowLength * Math.Cos(angle + side)), to.Y - (ArrowLength * Math.Sin(angle + side)));
            context.DrawGeometry(brush, null, Outline([to, Barb(-0.5), Barb(0.5)]));
        }
    }

    private void DrawText(DrawingContext context, DiagramText text)
    {
        var brush = TextBrush(text.Tone);
        if (IsThumbnail)
        {
            var bar = new Rect(text.CenterX - (ThumbnailBarWidth / 2), text.CenterY - (ThumbnailBarHeight / 2), ThumbnailBarWidth, ThumbnailBarHeight);
            context.PushOpacity(0.75);
            context.DrawRoundedRectangle(brush, null, bar, 4, 4);
            context.Pop();
            return;
        }
        var family = TryFindResource("Kyf.Font.Ui") as FontFamily ?? SystemFonts.MessageFontFamily;
        var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var spacing = text.Size * LineSpacing;
        var top = text.CenterY - ((text.Lines.Length - 1) * spacing / 2);
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        for (var i = 0; i < text.Lines.Length; i++)
        {
            var line = new FormattedText(text.Lines[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, text.Size, brush, dip);
            context.DrawText(line, new Point(text.CenterX - (line.Width / 2), top + (i * spacing) - (line.Height / 2)));
        }
    }

    private static StreamGeometry Outline(List<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], isFilled: true, isClosed: true);
            for (var i = 1; i < points.Count; i++)
            {
                context.LineTo(points[i], isStroked: true, isSmoothJoin: false);
            }
        }
        geometry.Freeze();
        return geometry;
    }
}
