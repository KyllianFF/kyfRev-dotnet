using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using KyfRev.Application.Model;
using Typography = KyfRev.DesignSystem.Tokens.Typography;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>Un échantillon de papier, pour le choisir : le même dessin que sous une fiche, vu de plus loin.</summary>
internal sealed class PaperSwatch : FrameworkElement
{
    /// <summary>À quelle échelle l'échantillon montre le papier.</summary>
    private const double Scale = 0.37;

    /// <summary>Le papier montré.</summary>
    public static readonly DependencyProperty PaperProperty = DependencyProperty.Register(
        nameof(Paper), typeof(PaperStyle), typeof(PaperSwatch), new FrameworkPropertyMetadata(PaperStyle.Plain, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Ce qui change avec le thème : l'échantillon se redessine alors.</summary>
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register(
        "ThemeProbe", typeof(Brush), typeof(PaperSwatch), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public PaperSwatch()
    {
        IsHitTestVisible = false;
        SetResourceReference(ThemeProbeProperty, "Kyf.Brush.Surface");
    }

    /// <inheritdoc cref="PaperProperty"/>
    public PaperStyle Paper
    {
        get => (PaperStyle)GetValue(PaperProperty);
        set => SetValue(PaperProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        var sheet = new Rect(0, 0, ActualWidth, ActualHeight);
        var step = PaperStyles.LineStepPixels(Paper) * Scale;
        PaperPainter.Draw(drawingContext, this, sheet, sheet, Paper, 14, step * 0.9, step);
    }
}

/// <summary>
/// L'aperçu d'une feuille dans les paramètres : sa forme, ses marges en pointillé, son papier, et quelques lignes de texte
/// posées dessus, dans la police choisie. Une feuille sans fin n'a pas de bas : elle sort de l'aperçu.
/// </summary>
internal sealed class SheetPaperPreview : FrameworkElement
{
    /// <summary>La mise en page montrée.</summary>
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
        nameof(Layout), typeof(PageLayout), typeof(SheetPaperPreview), new FrameworkPropertyMetadata(PageLayout.Default, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Le papier montré ; rien s'il est éteint.</summary>
    public static readonly DependencyProperty PaperProperty = DependencyProperty.Register(
        nameof(Paper), typeof(PaperStyle?), typeof(SheetPaperPreview), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Le confort de lecture : la police et la taille du texte montré.</summary>
    public static readonly DependencyProperty ComfortProperty = DependencyProperty.Register(
        nameof(Comfort), typeof(ReadingComfort), typeof(SheetPaperPreview), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Le titre de la fiche montrée.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SheetPaperPreview), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Son texte : des paragraphes séparés par une ligne vide.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SheetPaperPreview), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register(
        "ThemeProbe", typeof(Brush), typeof(SheetPaperPreview), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Une feuille sans fin est montrée large comme une A4.</summary>
    private const double EndlessWidthCm = 21;

    public SheetPaperPreview()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SetResourceReference(ThemeProbeProperty, "Kyf.Brush.Surface");
    }

    /// <inheritdoc cref="LayoutProperty"/>
    public PageLayout Layout
    {
        get => (PageLayout)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <inheritdoc cref="PaperProperty"/>
    public PaperStyle? Paper
    {
        get => (PaperStyle?)GetValue(PaperProperty);
        set => SetValue(PaperProperty, value);
    }

    /// <inheritdoc cref="ComfortProperty"/>
    public ReadingComfort? Comfort
    {
        get => (ReadingComfort?)GetValue(ComfortProperty);
        set => SetValue(ComfortProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>La feuille dessinée, pour les essais.</summary>
    internal Rect Sheet { get; private set; }

    /// <summary>Le pas d'écriture dessiné, à l'échelle de l'aperçu ; zéro sans papier réglé.</summary>
    internal double Step { get; private set; }

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        var layout = Layout ?? PageLayout.Default;
        var comfort = Comfort ?? ReadingComfort.Default;
        const double around = 12;
        var (wide, tall) = (Math.Max(0, ActualWidth - (2 * around)), Math.Max(0, ActualHeight - (2 * around)));
        if (wide <= 0 || tall <= 0)
        {
            return;
        }
        var paged = layout.IsPaged;
        var pageWidth = (paged ? layout.WidthCm : EndlessWidthCm) * PageLayout.PixelsPerCm;
        var pageHeight = paged ? layout.HeightCm * PageLayout.PixelsPerCm : double.PositiveInfinity;
        var scale = Math.Min(wide / pageWidth, paged ? tall / pageHeight : double.PositiveInfinity);
        // Une feuille sans fin part du haut et sort par le bas ; une page tient entière, centrée.
        var sheet = paged
            ? new Rect(Math.Round((ActualWidth - (pageWidth * scale)) / 2), Math.Round((ActualHeight - (pageHeight * scale)) / 2), Math.Round(pageWidth * scale), Math.Round(pageHeight * scale))
            : new Rect(around, around, Math.Round(wide), ActualHeight);
        Sheet = sheet;
        var margin = (paged ? layout.MarginPixels : 28) * scale;
        var family = comfort.Family ?? (comfort.FamilyKey is { } key && TryFindResource(key) is FontFamily found ? found : TextElement.GetFontFamily(this));
        var size = comfort.Size * scale;
        var step = Paper is { } ruled ? PaperStyles.LineStepPixels(ruled) * scale : 0;
        Step = step;
        var pitch = step > 0 ? ReadingComfort.Slots(comfort.Size, step / scale) * step : size * comfort.LineSpacing;
        var baseline = pitch * (family.LineSpacing > 0 ? family.Baseline / family.LineSpacing : 0.8);
        PaperPainter.Draw(drawingContext, this, sheet, sheet, Paper, margin, margin + baseline, step);
        if (TryFindResource("Kyf.Brush.Divider") is Brush divider)
        {
            drawingContext.DrawRectangle(null, new Pen(divider, 1), new Rect(sheet.X + 0.5, sheet.Y + 0.5, Math.Max(0, sheet.Width - 1), Math.Max(0, sheet.Height - 1)));
        }
        var inside = new Rect(sheet.X + margin, sheet.Y + margin, Math.Max(0, sheet.Width - (2 * margin)), Math.Max(0, sheet.Height - (paged ? 2 * margin : margin)));
        if (TryFindResource("Kyf.Brush.ControlBorder") is Brush guide && inside.Width > 4)
        {
            // Les marges, en pointillé : ce qui est dedans est la zone du texte.
            var dashed = new Pen(guide, 1) { DashStyle = new DashStyle([2, 2], 0) };
            drawingContext.DrawRectangle(null, dashed, new Rect(Math.Round(inside.X) + 0.5, Math.Round(inside.Y) + 0.5, inside.Width, paged ? inside.Height : ActualHeight));
        }
        if (TryFindResource("Kyf.Brush.Text") is not Brush ink || inside.Width <= 8 || size < 1)
        {
            return;
        }
        drawingContext.PushClip(new RectangleGeometry(new Rect(inside.X, sheet.Y, inside.Width, paged ? margin + inside.Height : ActualHeight)));
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var culture = CultureInfo.CurrentUICulture;
        var top = inside.Y;
        if (Title.Length > 0)
        {
            var title = new FormattedText(Title, culture, FlowDirection.LeftToRight, new Typeface(family, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), Typography.Subtitle.Size * scale, ink, dip)
            {
                MaxTextWidth = inside.Width,
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            // Le titre sur la première ligne ; le texte reprend deux lignes plus bas, comme après un titre.
            drawingContext.DrawText(title, new Point(inside.X, top + baseline - title.Baseline));
            top += 2 * pitch;
        }
        if (Text.Length > 0)
        {
            var body = new FormattedText(Text.ReplaceLineEndings("\n"), culture, FlowDirection.LeftToRight, new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, ink, dip)
            {
                MaxTextWidth = inside.Width,
                LineHeight = pitch,
            };
            drawingContext.DrawText(body, new Point(inside.X, top + baseline - body.Baseline));
        }
        drawingContext.Pop();
    }
}
