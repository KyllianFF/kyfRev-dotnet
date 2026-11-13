using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Ce que les préférences de l'ordinateur disent des feuilles : la mise en page de toutes les fiches, celles qui ont la
/// leur, et le papier montré derrière le texte. Les vues qui lisent une fiche le trouvent dans les ressources.
/// </summary>
/// <param name="Choices">Le papier de chaque type de feuille, et s'il est montré.</param>
/// <param name="Page">La mise en page de toutes les fiches.</param>
/// <param name="SheetPages">Les fiches qui ont leur propre mise en page.</param>
public sealed record SheetPaper(PaperChoices Choices, PageLayout Page, SheetPageLayouts SheetPages)
{
    /// <summary>Sa clé dans les ressources.</summary>
    public const string ResourceKey = "Kyf.Reading.Paper";

    /// <summary>Ce que disent ces préférences.</summary>
    public static SheetPaper Of(MachinePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return new SheetPaper(preferences.Paper, preferences.Page, preferences.SheetPages);
    }

    /// <summary>La mise en page d'une fiche : la sienne si elle en a une, sinon celle de toutes.</summary>
    public PageLayout LayoutOf(string sheetId) => SheetPages.Of(sheetId) ?? Page;

    /// <summary>Le papier à montrer pour ce type de feuille ; rien si le papier est éteint.</summary>
    public PaperStyle? StyleOf(PageFormat format) => Choices.IsOn ? Choices.Of(format) : null;
}

/// <summary>
/// Dessine une feuille de papier : son fond, son bord, et sa réglure (carreaux, lignes, points). Ce n'est qu'un décor de
/// l'écran : rien de cela ne part à l'imprimante. Les traits prennent les couleurs du thème.
/// </summary>
internal static class PaperPainter
{
    /// <summary>À quelle distance, à gauche du texte, passe la marge rouge.</summary>
    private const double MarginLineGap = 8;

    /// <summary>Dessine la part <paramref name="part"/> de la feuille <paramref name="sheet"/>.</summary>
    /// <param name="drawing">Où dessiner.</param>
    /// <param name="owner">Ce qui donne les couleurs du thème.</param>
    /// <param name="sheet">La feuille entière.</param>
    /// <param name="part">Ce qu'il faut en dessiner.</param>
    /// <param name="style">Le papier ; rien pour une feuille sans réglure.</param>
    /// <param name="textLeft">Où commence le texte, depuis le bord gauche de la feuille : la marge rouge passe juste avant.</param>
    /// <param name="line">
    /// Où passe une ligne d'écriture, depuis le haut de la feuille : la ligne de base de la première ligne de texte. Toute
    /// la réglure en part, pour que le texte soit posé dessus. Sans valeur, un pas sous le haut de la feuille.
    /// </param>
    /// <param name="step">Le pas d'écriture ; zéro pour celui du papier, en vraie grandeur.</param>
    public static void Draw(DrawingContext drawing, FrameworkElement owner, Rect sheet, Rect part, PaperStyle? style, double textLeft, double line = double.NaN, double step = 0)
    {
        part.Intersect(sheet);
        if (part.IsEmpty || part.Width <= 0 || part.Height <= 0 || owner.TryFindResource("Kyf.Brush.Surface") is not Brush paper)
        {
            return;
        }
        drawing.DrawRectangle(paper, null, part);
        if (style is not { } ruled || ruled == PaperStyle.Plain || owner.TryFindResource("Kyf.Brush.Divider") is not SolidColorBrush fine)
        {
            return;
        }
        var strong = owner.TryFindResource("Kyf.Brush.ControlBorder") as SolidColorBrush ?? fine;
        var pitch = step > 0 ? step : PaperStyles.LineStepPixels(ruled);
        var written = sheet.Y + (double.IsNaN(line) ? pitch : line);
        var faint = new Pen(Faded(fine, 0.55), 1);
        var thin = new Pen(fine, 1);
        var thick = new Pen(Faded(strong, 0.7), 1);
        drawing.PushClip(new RectangleGeometry(part));
        switch (ruled)
        {
            case PaperStyle.Lines:
                Rows(drawing, part, written, pitch, _ => thick);
                break;
            case PaperStyle.LargeSquares:
                // Seyès : la ligne d'écriture, trois interlignes plus légers au-dessus d'elle, une verticale par carreau.
                if (pitch / 4 >= FinestGap)
                {
                    Rows(drawing, part, written, pitch / 4, index => index % 4 == 0 ? null : faint);
                }
                Columns(drawing, part, sheet.X, pitch, _ => thin);
                Rows(drawing, part, written, pitch, _ => thick);
                break;
            case PaperStyle.SmallSquares:
                Rows(drawing, part, written, pitch / 2, _ => thin);
                Columns(drawing, part, sheet.X, pitch / 2, _ => thin);
                break;
            case PaperStyle.Dots:
                drawing.DrawRectangle(Dots(pitch / 2, sheet.X, written, Faded(strong, 0.8)), null, part);
                break;
            default:
                // Millimétré : le millimètre très léger, plus net tous les cinq, plus net encore tous les dix. Vu de trop
                // loin, le millimètre ne se dessine plus : il ne ferait qu'un voile.
                var millimetre = pitch / 10 >= FinestGap ? new Pen(Faded(fine, 0.3), 1) : null;
                var half = new Pen(Faded(fine, 0.75), 1);
                Rows(drawing, part, written, pitch / 10, index => index % 10 == 0 ? thick : index % 5 == 0 ? half : millimetre);
                Columns(drawing, part, sheet.X, pitch / 10, index => index % 10 == 0 ? thick : index % 5 == 0 ? half : millimetre);
                break;
        }
        if (ruled is PaperStyle.LargeSquares or PaperStyle.Lines && owner.TryFindResource("Kyf.Brush.Critical") is SolidColorBrush red && textLeft > MarginLineGap + 2)
        {
            var x = Math.Round(sheet.X + textLeft - MarginLineGap) + 0.5;
            drawing.DrawLine(new Pen(Faded(red, 0.45), 1), new Point(x, part.Top), new Point(x, part.Bottom));
        }
        drawing.Pop();
    }

    /// <summary>En deçà de cet écart, deux traits voisins se confondraient.</summary>
    private const double FinestGap = 2.5;

    /// <summary>Les traits horizontaux d'une part de feuille : un tous les <paramref name="gap"/>, comptés depuis <paramref name="origin"/>.</summary>
    private static void Rows(DrawingContext drawing, Rect part, double origin, double gap, Func<long, Pen?> pen)
    {
        for (var index = (long)Math.Ceiling((part.Top - origin) / gap); ; index++)
        {
            var y = origin + (index * gap);
            if (y > part.Bottom)
            {
                break;
            }
            if (pen(Math.Abs(index)) is { } stroke)
            {
                // Un trait net, juste sous la ligne de base.
                var at = Math.Round(y) + 0.5;
                drawing.DrawLine(stroke, new Point(part.Left, at), new Point(part.Right, at));
            }
        }
    }

    /// <summary>Les traits verticaux d'une part de feuille : un tous les <paramref name="gap"/>, comptés depuis <paramref name="origin"/>.</summary>
    private static void Columns(DrawingContext drawing, Rect part, double origin, double gap, Func<long, Pen?> pen)
    {
        for (var index = (long)Math.Ceiling((part.Left - origin) / gap); ; index++)
        {
            var x = origin + (index * gap);
            if (x > part.Right)
            {
                break;
            }
            if (pen(Math.Abs(index)) is { } stroke)
            {
                var at = Math.Round(x) + 0.5;
                drawing.DrawLine(stroke, new Point(at, part.Top), new Point(at, part.Bottom));
            }
        }
    }

    /// <summary>Des points, un tous les <paramref name="gap"/> : chaque rangée passe par une ligne d'écriture.</summary>
    private static DrawingBrush Dots(double gap, double left, double line, Brush ink)
    {
        var motif = new DrawingGroup();
        // Un fond transparent donne sa taille au motif : sans lui, il se resserrerait autour de son point.
        motif.Children.Add(new GeometryDrawing(Clear(), null, new RectangleGeometry(new Rect(0, 0, gap, gap))));
        motif.Children.Add(new GeometryDrawing(ink, null, new EllipseGeometry(new Point(gap / 2, gap / 2), 0.9, 0.9)));
        var brush = new DrawingBrush(motif)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(left, line - (gap / 2), gap, gap),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, gap, gap),
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Clear()
    {
        var clear = new SolidColorBrush(Colors.Transparent);
        clear.Freeze();
        return clear;
    }

    private static SolidColorBrush Faded(SolidColorBrush brush, double opacity)
    {
        var faded = new SolidColorBrush(brush.Color) { Opacity = opacity };
        faded.Freeze();
        return faded;
    }
}

/// <summary>
/// Les feuilles, dessinées sous le texte : le papier de chaque page, marges comprises, avec son bord et sa réglure. Ce qui
/// les entoure reste le fond de l'application. Rien ne s'y clique.
/// </summary>
internal sealed class PagePaperLayer : FrameworkElement
{
    private IReadOnlyList<Rect> _sheets = [];
    private PaperStyle? _style;
    private double _textLeft;
    private double _line = double.NaN;
    private double _step;

    public PagePaperLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    /// <summary>Le nombre de feuilles dessinées.</summary>
    internal int Count => _sheets.Count;

    /// <summary>La largeur des feuilles.</summary>
    internal double SheetWidth => _sheets.Count == 0 ? 0 : _sheets[0].Width;

    /// <summary>La hauteur de la première feuille, marges comprises.</summary>
    internal double FirstSheetHeight => _sheets.Count == 0 ? 0 : _sheets[0].Height;

    /// <summary>Le papier dessiné ; rien pour des feuilles sans réglure.</summary>
    internal PaperStyle? Ruling => _style;

    /// <summary>Montre les feuilles.</summary>
    /// <param name="sheets">Chaque feuille, là où elle est dans la couche, marges comprises.</param>
    /// <param name="style">Leur papier.</param>
    /// <param name="textLeft">Où commence le texte, depuis le bord gauche d'une feuille.</param>
    /// <param name="line">Où passe la première ligne d'écriture, depuis le haut d'une feuille ; sans valeur, un pas sous le haut.</param>
    /// <param name="step">Le pas d'écriture ; zéro pour celui du papier.</param>
    internal void Show(IReadOnlyList<Rect> sheets, PaperStyle? style, double textLeft, double line = double.NaN, double step = 0)
    {
        if (sheets.Count == 0 && _sheets.Count == 0)
        {
            return;
        }
        (_sheets, _style, _textLeft, _line, _step) = (sheets, style, textLeft, line, step);
        InvalidateVisual();
    }

    /// <summary>Où passe la première ligne d'écriture, depuis le haut d'une feuille.</summary>
    internal double WritingLine => _line;

    /// <summary>Les feuilles dessinées, là où elles sont dans la couche.</summary>
    internal IReadOnlyList<Rect> Sheets => _sheets;

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        var edge = TryFindResource("Kyf.Brush.Divider") is Brush divider ? new Pen(divider, 1) : null;
        var view = new Rect(0, 0, ActualWidth, ActualHeight);
        foreach (var sheet in _sheets)
        {
            if (!sheet.IntersectsWith(view))
            {
                continue;
            }
            // Seule la part visible est dessinée : une longue feuille sans fin ne coûte pas plus qu'une page.
            var part = new Rect(sheet.X, Math.Max(sheet.Y, -2), sheet.Width, Math.Min(sheet.Bottom, ActualHeight + 2) - Math.Max(sheet.Y, -2));
            PaperPainter.Draw(drawingContext, this, sheet, part, _style, _textLeft, _line, _step);
            if (edge is not null)
            {
                drawingContext.DrawRectangle(null, edge, new Rect(sheet.X + 0.5, Math.Round(sheet.Y) + 0.5, Math.Max(0, sheet.Width - 1), Math.Max(0, sheet.Height - 1)));
            }
        }
    }
}

/// <summary>
/// L'écart entre deux feuilles, dessiné par-dessus le texte là où la mise en pages a ouvert un espace : la fin de la feuille
/// du haut, la bande entre les deux, et la marge de la feuille du bas, chacune avec son papier. Un bloc coupé là y lit
/// « suite page suivante », puis « suite ». Rien ne s'y clique.
/// </summary>
internal sealed class PageBandsLayer : FrameworkElement
{
    private IReadOnlyList<PageBreak> _breaks = [];
    private IReadOnlyList<Rect> _sheets = [];
    private PaperStyle? _style;
    private double _origin;
    private double _left;
    private double _width;
    private double _margin;
    private double _clip;
    private double _line = double.NaN;
    private double _step;

    public PageBandsLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    /// <summary>Le nombre de bandes dessinées.</summary>
    internal int Count => _breaks.Count;

    /// <summary>Chaque écart dit la page qui commence sous lui : « Page 2 sur 3 ».</summary>
    internal bool NumbersPages
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>Ce que dit l'écart au-dessus de la page <paramref name="page"/> d'une fiche de <paramref name="pages"/> pages.</summary>
    internal static string PageCaption(int page, int pages) => string.Create(CultureInfo.CurrentCulture, $"Page {page} sur {pages}");

    /// <summary>Le nombre de coupures qui disent « suite ».</summary>
    internal int Continued => _breaks.Count(pageBreak => pageBreak.Continues);

    /// <summary>Montre les sauts de page.</summary>
    /// <param name="breaks">Les sauts, mesurés depuis le haut du texte.</param>
    /// <param name="sheets">Les feuilles, là où elles sont dans la couche : chaque bande reprend le papier des siennes.</param>
    /// <param name="style">Leur papier.</param>
    /// <param name="origin">Où est le haut du texte dans la couche, défilement compris.</param>
    /// <param name="left">Le bord gauche des feuilles.</param>
    /// <param name="width">La largeur des feuilles.</param>
    /// <param name="margin">La marge d'une feuille.</param>
    /// <param name="clip">Où s'arrête le texte à droite : la barre de défilement reste libre.</param>
    /// <param name="line">Où passe la première ligne d'écriture, depuis le haut d'une feuille.</param>
    /// <param name="step">Le pas d'écriture ; zéro pour celui du papier.</param>
    internal void Show(IReadOnlyList<PageBreak> breaks, IReadOnlyList<Rect> sheets, PaperStyle? style, double origin, double left, double width, double margin, double clip, double line = double.NaN, double step = 0)
    {
        if (breaks.Count == 0 && _breaks.Count == 0)
        {
            return;
        }
        (_breaks, _sheets, _style, _origin, _left, _width, _margin, _clip, _line, _step) = (breaks, sheets, style, origin, left, width, margin, clip, line, step);
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (_breaks.Count == 0 || TryFindResource("Kyf.Brush.Background") is not Brush desk)
        {
            return;
        }
        var line = TryFindResource("Kyf.Brush.Divider") is Brush divider ? new Pen(divider, 1) : null;
        // Le papier se redessine dans la largeur de la feuille ; l'écart, lui, efface tout ce qui le traverserait.
        var width = Math.Max(0, Math.Min(_left + _width, Math.Min(_clip, ActualWidth)) - _left);
        var side = _left + _margin;
        var number = 1;
        foreach (var pageBreak in _breaks)
        {
            number++;
            var contentEnd = _origin + pageBreak.ContentEnd;
            var sheetEnd = _origin + pageBreak.PageEnd + _margin;
            var nextSheet = _origin + pageBreak.NextTop - _margin;
            if (_origin + pageBreak.NextTop < 0 || contentEnd > ActualHeight)
            {
                continue;
            }
            // Ce qui reste de la feuille du haut, puis le haut de la feuille du bas : du papier, sans rien dessus.
            var above = new Rect(_left + 1, contentEnd, Math.Max(0, width - 2), Math.Max(0, sheetEnd - contentEnd - 1));
            var below = new Rect(_left + 1, nextSheet + 1, Math.Max(0, width - 2), Math.Max(0, _margin - 2));
            PaperPainter.Draw(drawingContext, this, SheetOf(above), above, _style, _margin, _line, _step);
            PaperPainter.Draw(drawingContext, this, SheetOf(below), below, _style, _margin, _line, _step);
            drawingContext.DrawRectangle(desk, null, new Rect(0, sheetEnd, Math.Min(_clip, ActualWidth), Math.Max(0, nextSheet - sheetEnd)));
            if (line is not null)
            {
                drawingContext.DrawLine(line, new Point(_left, sheetEnd - 0.5), new Point(_left + width, sheetEnd - 0.5));
                drawingContext.DrawLine(line, new Point(_left, nextSheet + 0.5), new Point(_left + width, nextSheet + 0.5));
            }
            if (NumbersPages && nextSheet - sheetEnd >= 12 && TryFindResource("Kyf.Brush.TextSecondary") is Brush quiet)
            {
                // Au milieu de l'écart, sur le fond de la page : la feuille qui suit dit son rang.
                var caption = Caption(PageCaption(number, _breaks.Count + 1), quiet, FontStyles.Normal, Math.Min(12, nextSheet - sheetEnd - 2));
                drawingContext.DrawText(caption, new Point(_left + ((width - caption.Width) / 2), sheetEnd + ((nextSheet - sheetEnd - caption.Height) / 2)));
            }
            if (pageBreak.Continues && TryFindResource("Kyf.Brush.TextSecondary") is Brush ink)
            {
                var under = Caption(ReviewText.PageContinues, ink);
                drawingContext.DrawText(under, new Point(Math.Max(side, _left + _width - _margin - under.Width), contentEnd + 2));
                var over = Caption(ReviewText.PageContinued, ink);
                drawingContext.DrawText(over, new Point(side, _origin + pageBreak.NextTop - over.Height - 2));
            }
        }
    }

    /// <summary>La feuille où se trouve une part de papier : sa réglure part du coin de cette feuille-là.</summary>
    private Rect SheetOf(Rect part)
    {
        var middle = part.Y + (part.Height / 2);
        foreach (var sheet in _sheets)
        {
            if (middle >= sheet.Y && middle <= sheet.Bottom)
            {
                return sheet;
            }
        }
        return new Rect(_left, part.Y, _width, part.Height);
    }

    private FormattedText Caption(string text, Brush ink) => Caption(text, ink, FontStyles.Italic, 12);

    private FormattedText Caption(string text, Brush ink, FontStyle style, double size) => new(
        text,
        CultureInfo.CurrentUICulture,
        FlowDirection.LeftToRight,
        new Typeface(TextElement.GetFontFamily(this), style, FontWeights.Normal, FontStretches.Normal),
        size,
        ink,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);
}

/// <summary>
/// Autour d'une vue de lecture : quand la fiche a une feuille plus large que la place offerte (l'aperçu d'une liste, par
/// exemple), la vue est rétrécie d'un seul tenant pour que la feuille tienne dans la largeur, comme une page vue de loin.
/// </summary>
internal sealed class PageFitHost : Decorator
{
    /// <summary>
    /// La largeur que la vue demande pour sa feuille : celle de la page et de la barre de défilement, ou, pour une feuille
    /// sans fin, <see cref="EndlessWidth"/>. Ce qui entoure la feuille (son titre, ses boutons) s'y aligne.
    /// </summary>
    public static readonly DependencyProperty SheetWidthProperty = DependencyProperty.Register(
        nameof(SheetWidth), typeof(double), typeof(PageFitHost), new PropertyMetadata(double.PositiveInfinity));

    /// <summary>
    /// Ce qui sépare les bords de la vue de ceux de sa feuille, à gauche et à droite (la barre de défilement est à droite de
    /// la feuille) : le titre et les boutons posés au-dessus d'elle prennent ces marges pour s'aligner sur elle.
    /// </summary>
    public static readonly DependencyProperty SheetInsetProperty = DependencyProperty.Register(
        nameof(SheetInset), typeof(Thickness), typeof(PageFitHost), new PropertyMetadata(default(Thickness)));

    private double _scale = 1;
    private SheetDocumentViewer? _watched;

    /// <inheritdoc cref="SheetInsetProperty"/>
    public Thickness SheetInset
    {
        get => (Thickness)GetValue(SheetInsetProperty);
        private set => SetValue(SheetInsetProperty, value);
    }

    /// <inheritdoc cref="SheetWidthProperty"/>
    public double SheetWidth
    {
        get => (double)GetValue(SheetWidthProperty);
        private set => SetValue(SheetWidthProperty, value);
    }

    /// <summary>La largeur donnée à une feuille sans fin ; infinie : toute la place.</summary>
    public double EndlessWidth { get; set; } = double.PositiveInfinity;

    /// <summary>L'échelle en cours ; 1 quand la feuille tient.</summary>
    internal double Scale => _scale;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is SheetDocumentViewer viewer)
        {
            if (!ReferenceEquals(viewer, _watched))
            {
                _watched?.PagesShown -= OnPagesShown;
                _watched = viewer;
                viewer.PagesShown += OnPagesShown;
            }
            var wanted = viewer.FitWidth;
            var sheet = wanted > 0 ? wanted : EndlessWidth;
            if (!sheet.Equals(SheetWidth))
            {
                SheetWidth = sheet;
            }
            var scale = wanted > 0 && !double.IsInfinity(constraint.Width) && constraint.Width > 0 && constraint.Width < wanted
                ? Math.Max(0.2, constraint.Width / wanted)
                : 1;
            if (Math.Abs(scale - _scale) > 0.001)
            {
                _scale = scale;
                viewer.LayoutTransform = scale == 1 ? Transform.Identity : new ScaleTransform(scale, scale);
            }
        }
        return base.MeasureOverride(constraint);
    }

    /// <summary>La vue vient de poser ses feuilles : leurs bords sont connus.</summary>
    private void OnPagesShown(object? sender, EventArgs e)
    {
        if (sender is not SheetDocumentViewer viewer || viewer.PagePaper.Sheets is not [var sheet, ..] || viewer.ActualWidth <= 0)
        {
            return;
        }
        var inset = new Thickness(Math.Max(0, Math.Round(sheet.X * _scale)), 0, Math.Max(0, Math.Round((viewer.ActualWidth - sheet.Right) * _scale)), 0);
        if (Math.Abs(inset.Left - SheetInset.Left) > 0.5 || Math.Abs(inset.Right - SheetInset.Right) > 0.5)
        {
            SheetInset = inset;
        }
    }
}
