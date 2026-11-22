using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using KyfRev.Application.Model;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Reading;

/// <remarks>
/// La mise en pages de la lecture. Le document reste un seul texte qui défile ; quand la fiche a une feuille (A4, A5, A6),
/// un espace s'ouvre au-dessus de chaque bloc qui dépasserait le bas de sa page, et une bande dessine la fin d'une feuille,
/// l'écart, et le début de la suivante : un bloc coupé (citation, tableau) se ferme donc au bas de la page et se rouvre en
/// haut de l'autre. L'en-tête d'un tableau coupé est répété ; une citation coupée dit « suite ». Rien n'est coupé au milieu
/// d'un bloc : voir <see cref="PageBreaks"/>.
/// </remarks>
internal sealed partial class SheetDocumentViewer
{
    /// <summary>L'écart dessiné entre deux feuilles.</summary>
    internal const double PageGap = 16;

    /// <summary>Ce qui marque la ligne d'en-tête d'un tableau.</summary>
    internal static readonly object HeaderRowMark = new();

    /// <summary>Ce qui marque l'en-tête de la fiche : ses détails et son titre, avant le texte.</summary>
    internal static readonly object SheetHeaderMark = new();

    /// <summary>L'espace sous le titre de la fiche, quand il est dans le texte.</summary>
    internal const double HeaderSpacing = 16;

    /// <summary>L'espace entre l'en-tête posé au-dessus des feuilles et le bord de la première, et au-dessus de lui.</summary>
    internal const double HeaderGap = 12;

    /// <summary>Ce qui marque un en-tête répété : il n'est pas dans la fiche, la mise en pages l'ajoute et le retire.</summary>
    private static readonly object RepeatedRowMark = new();

    /// <summary>Les marges du document sans feuille : celles de toujours.</summary>
    private static readonly Thickness EndlessPadding = new(0, 4, 0, 24);

    /// <summary>La feuille de la fiche ; sans elle, ou sans fin, le texte défile comme toujours.</summary>
    public static readonly DependencyProperty PageLayoutProperty = DependencyProperty.Register(
        nameof(PageLayout), typeof(PageLayout), typeof(SheetDocumentViewer), new PropertyMetadata(null, (viewer, _) => ((SheetDocumentViewer)viewer).FollowSheet()));

    /// <summary>Le nombre de mesures de suite après lequel la mise en pages s'en tient à ce qu'elle a : elle ne tourne jamais sans fin.</summary>
    private const int MaximumPasses = 12;

    private readonly Dictionary<Block, double> _blockSpacers = [];

    /// <summary>Ce qui a été ajouté à la marge du haut de chaque bloc poussé, pour le retirer tel quel.</summary>
    private readonly Dictionary<Block, double> _blockMargins = [];
    private int _passes;
    private readonly Dictionary<TableRow, double> _rowSpacers = [];
    private readonly Dictionary<TableRow, TableRow> _repeats = [];
    private readonly Dictionary<FrameworkElement, double> _shrunk = [];
    private IReadOnlyList<PageBreak> _breaks = [];
    private List<(double Top, double End)> _sheets = [];
    private double _origin;
    private bool _paginates;

    /// <summary>Un bloc à placer : ce qu'il est, où il est, et ce que la mise en pages lui a déjà ajouté.</summary>
    /// <param name="Element">Le bloc, ou la ligne de tableau.</param>
    /// <param name="Top">Son haut mesuré.</param>
    /// <param name="Height">Sa hauteur mesurée.</param>
    /// <param name="Kind">Ce qu'il est.</param>
    /// <param name="Added">Tout ce qui a été ajouté au-dessus de lui : son espace, et l'en-tête répété.</param>
    /// <param name="Spacer">Son espace seul.</param>
    /// <param name="Continues">Il poursuit une citation ou un tableau.</param>
    /// <param name="Lead">La hauteur de l'en-tête de son tableau.</param>
    private readonly record struct Unit(object Element, double Top, double Height, PageBoxKind Kind, double Added, double Spacer, bool Continues = false, double Lead = 0)
    {
        /// <summary>Sur un papier réglé, où son haut doit tomber dans le pas d'écriture ; rien s'il ne s'aligne pas.</summary>
        public double? Snap { get; init; }
    }

    /// <inheritdoc cref="PageLayoutProperty"/>
    public PageLayout? PageLayout
    {
        get => (PageLayout?)GetValue(PageLayoutProperty);
        set => SetValue(PageLayoutProperty, value);
    }

    /// <summary>Le nombre de pages du texte affiché ; zéro sans feuille.</summary>
    public int PageCount { get; private set; }

    /// <summary>Le nombre de pages a changé.</summary>
    public event EventHandler? PageCountChanged;

    /// <summary>La couche qui dessine l'écart entre les feuilles, par-dessus le texte.</summary>
    internal PageBandsLayer PageBands { get; } = new();

    /// <summary>La couche qui dessine les feuilles, sous le texte.</summary>
    internal PagePaperLayer PagePaper { get; } = new();

    /// <summary>
    /// Ce que les préférences disent des feuilles : la vue le trouve dans les ressources, et suit ses changements.
    /// </summary>
    public static readonly DependencyProperty PaperProperty = DependencyProperty.Register(
        nameof(Paper), typeof(SheetPaper), typeof(SheetDocumentViewer), new PropertyMetadata(null, (viewer, _) => ((SheetDocumentViewer)viewer).FollowSheet()));

    /// <summary>
    /// La vue prend d'elle-même la mise en page de la fiche qu'elle montre (un aperçu, par exemple), tant qu'aucune ne lui
    /// est donnée par <see cref="PageLayout"/>.
    /// </summary>
    public static readonly DependencyProperty FollowsSheetLayoutProperty = DependencyProperty.Register(
        nameof(FollowsSheetLayout), typeof(bool), typeof(SheetDocumentViewer), new PropertyMetadata(false, (viewer, _) => ((SheetDocumentViewer)viewer).FollowSheet()));

    /// <summary>
    /// La largeur que le texte d'une feuille sans fin ne dépasse pas, barre de défilement non comprise : la vue peut alors
    /// être plus large que lui, sa barre au bord de la fenêtre, le texte restant une colonne centrée. Infinie par défaut :
    /// le texte prend toute la vue.
    /// </summary>
    public static readonly DependencyProperty TextMaxWidthProperty = DependencyProperty.Register(
        nameof(TextMaxWidth), typeof(double), typeof(SheetDocumentViewer), new PropertyMetadata(double.PositiveInfinity, (viewer, _) => ((SheetDocumentViewer)viewer).TextWidthChanged()));

    /// <summary>
    /// La largeur que peut prendre, sur une feuille sans fin et sans papier, ce qui est plus large que la colonne de texte :
    /// un tableau, une image seule sur sa ligne, un schéma. Le texte garde <see cref="TextMaxWidth"/> ; eux débordent de
    /// chaque côté, centrés sur lui, jusqu'à cette largeur. Zéro par défaut : rien ne déborde.
    /// </summary>
    public static readonly DependencyProperty WideMaxWidthProperty = DependencyProperty.Register(
        nameof(WideMaxWidth), typeof(double), typeof(SheetDocumentViewer), new PropertyMetadata(0d, (viewer, _) => ((SheetDocumentViewer)viewer).TextWidthChanged()));

    /// <summary>Les marges de côté qu'un bloc avait avant que la vue ne le resserre sur la colonne de texte.</summary>
    private static readonly DependencyProperty OwnMarginProperty = DependencyProperty.RegisterAttached(
        "OwnMargin", typeof(Thickness), typeof(SheetDocumentViewer), new PropertyMetadata(default(Thickness)));

    /// <summary>Les marges du texte sur une feuille sans fin dont le papier est montré : il ne touche pas le bord.</summary>
    private static readonly Thickness EndlessPaperPadding = new(28, 20, 28, 28);

    /// <inheritdoc cref="TextMaxWidthProperty"/>
    public double TextMaxWidth
    {
        get => (double)GetValue(TextMaxWidthProperty);
        set => SetValue(TextMaxWidthProperty, value);
    }

    /// <inheritdoc cref="WideMaxWidthProperty"/>
    public double WideMaxWidth
    {
        get => (double)GetValue(WideMaxWidthProperty);
        set => SetValue(WideMaxWidthProperty, value);
    }

    /// <summary>
    /// La largeur du document d'une feuille sans fin : celle du texte, ou celle des blocs larges quand ils peuvent déborder.
    /// Un papier montré garde la sienne, celle du texte : rien ne sort de la feuille dessinée.
    /// </summary>
    private double EndlessWidth =>
        PaperShown is null && Layout is not { IsPaged: true } && double.IsFinite(WideMaxWidth) && WideMaxWidth > TextMaxWidth ? WideMaxWidth : TextMaxWidth;

    /// <summary>Ce qu'une feuille sans fin laisse libre de chaque côté de son document, dans une vue plus large que lui.</summary>
    internal double EndlessSide =>
        double.IsFinite(TextMaxWidth) && TextMaxWidth > 0 ? Math.Max(0, Math.Floor((ActualWidth - SystemParameters.VerticalScrollBarWidth - EndlessWidth) / 2)) : 0;

    /// <summary>
    /// Ce que le texte laisse de chaque côté dans le document, quand celui-ci est plus large que la colonne de texte : la
    /// place où un bloc large déborde. Zéro quand rien ne peut déborder, ou dans une vue qui n'a que la place du texte.
    /// </summary>
    internal double TextInset =>
        double.IsFinite(TextMaxWidth) && TextMaxWidth > 0 && EndlessWidth > TextMaxWidth && ActualWidth > 0
            ? Math.Max(0, Math.Floor((ActualWidth - SystemParameters.VerticalScrollBarWidth - (2 * EndlessSide) - TextMaxWidth) / 2))
            : 0;

    /// <summary>
    /// Resserre chaque bloc du document sur la colonne de texte, et laisse à un bloc plus large qu'elle la place qu'il
    /// demande, autant de chaque côté. Seules les marges de côté changent : celles du haut sont à la mise en pages.
    /// </summary>
    /// <returns>Au moins un bloc a changé de largeur.</returns>
    internal bool FitBlocks()
    {
        if (Document is not { } document)
        {
            return false;
        }
        var inset = TextInset;
        var changed = false;
        foreach (var block in document.Blocks)
        {
            if (block.ReadLocalValue(OwnMarginProperty) == DependencyProperty.UnsetValue)
            {
                if (inset <= 0)
                {
                    // Rien ne déborde et ce bloc n'a jamais été resserré : il garde ses marges.
                    continue;
                }
                block.SetValue(OwnMarginProperty, block.Margin);
            }
            var own = (Thickness)block.GetValue(OwnMarginProperty);
            var side = inset <= 0 ? 0 : Math.Max(0, inset - Math.Ceiling(Math.Max(0, WidthWanted(block) - TextMaxWidth) / 2));
            var margin = block.Margin;
            var fitted = new Thickness(own.Left + side, margin.Top, own.Right + side, margin.Bottom);
            if (margin != fitted)
            {
                block.Margin = fitted;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// La largeur qu'un bloc demande quand elle dépasse celle du texte : un tableau, ce que ses colonnes demandent sur une
    /// ligne ; un schéma, sa plus grande largeur ; un paragraphe qui n'a que des images sans largeur choisie, la leur.
    /// Tout le reste est du texte, qui passe à la ligne dans sa colonne.
    /// </summary>
    private static double WidthWanted(Block block) => block switch
    {
        Table { Tag: TableSizing sizing } => sizing.Natural.Sum(),
        BlockUIContainer { Child: FrameworkElement drawing and (DiagramView or MermaidView or NetworkView) } when double.IsFinite(drawing.MaxWidth) => drawing.MaxWidth,
        Paragraph paragraph => PicturesWidth(paragraph),
        _ => 0,
    };

    private static double PicturesWidth(Paragraph paragraph)
    {
        var width = 0d;
        foreach (var inline in paragraph.Inlines)
        {
            switch (inline)
            {
                case InlineUIContainer { Child: var child } when PictureOf(child) is { Source: not null } picture && double.IsFinite(picture.MaxWidth):
                    width += picture.MaxWidth;
                    break;
                case Run run when string.IsNullOrWhiteSpace(run.Text):
                case LineBreak:
                    break;
                default:
                    // Du texte autour, une image à la largeur choisie (comptée sur celle du texte) ou pas encore lue.
                    return 0;
            }
        }
        return width;
    }

    /// <summary>L'image d'une ligne, seule ou sous ses masques.</summary>
    private static System.Windows.Controls.Image? PictureOf(UIElement? child) => child switch
    {
        System.Windows.Controls.Image image => image,
        System.Windows.Controls.Grid { Children: { Count: > 0 } children } when children[0] is System.Windows.Controls.Image image => image,
        _ => null,
    };

    /// <summary>
    /// La largeur du papier d'une feuille sans fin : toute la vue, ou, dans une vue plus large que le texte, sa colonne et
    /// ses marges, autant de chaque côté.
    /// </summary>
    private double EndlessSheetWidth(System.Windows.Controls.ScrollViewer scroller) =>
        EndlessSide > 0 ? Math.Max(0, ActualWidth - SystemParameters.VerticalScrollBarWidth - (2 * EndlessSide)) : scroller.ViewportWidth;

    private void TextWidthChanged()
    {
        ApplyPagePadding();
        FitTables();
        RestartPages();
    }

    private PageLayout? _shownLayout;
    private PaperStyle? _shownStyle;

    /// <summary>Le pas d'écriture du papier pour lequel le document a été construit ; zéro sans papier réglé.</summary>
    private double _step;

    /// <summary>Où tombe la ligne de base d'une ligne de texte, depuis son haut.</summary>
    private double _baseline;

    /// <summary>Le haut du texte a été mesuré : la réglure d'une feuille sans fin part de là.</summary>
    private bool _originKnown;

    /// <summary>Le pas d'écriture en vigueur, pour les essais.</summary>
    internal double WritingStep => _step;

    /// <summary>Où tombe la ligne de base d'une ligne de texte, pour les essais.</summary>
    internal double WritingBaseline => _baseline;

    /// <inheritdoc cref="PaperProperty"/>
    public SheetPaper? Paper
    {
        get => (SheetPaper?)GetValue(PaperProperty);
        set => SetValue(PaperProperty, value);
    }

    /// <inheritdoc cref="FollowsSheetLayoutProperty"/>
    public bool FollowsSheetLayout
    {
        get => (bool)GetValue(FollowsSheetLayoutProperty);
        set => SetValue(FollowsSheetLayoutProperty, value);
    }

    /// <summary>Les feuilles et leur papier se dessinent ; sans cela, il ne reste que le texte : c'est ce qui s'imprime.</summary>
    internal bool DrawsPaper
    {
        get;
        set
        {
            field = value;
            FollowSheet();
        }
    } = true;

    /// <summary>La mise en page en vigueur : celle qu'on lui donne, sinon celle de la fiche montrée si la vue la suit.</summary>
    internal PageLayout? Layout => PageLayout ?? (FollowsSheetLayout && Reading is { } reading ? Paper?.LayoutOf(reading.SheetId) : null);

    /// <summary>
    /// La vue montre toujours sa feuille : sans papier choisi, une feuille unie, posée sur le fond de la page. C'est l'aperçu
    /// du Carnet, où la fiche se voit comme une feuille même quand les papiers sont éteints.
    /// </summary>
    public static readonly DependencyProperty ShowsSheetProperty = DependencyProperty.Register(
        nameof(ShowsSheet), typeof(bool), typeof(SheetDocumentViewer), new PropertyMetadata(false, (viewer, _) => ((SheetDocumentViewer)viewer).FollowSheet()));

    /// <summary>L'écart entre deux feuilles dit quelle page commence : « Page 2 sur 3 ».</summary>
    public static readonly DependencyProperty NumbersPagesProperty = DependencyProperty.Register(
        nameof(NumbersPages), typeof(bool), typeof(SheetDocumentViewer), new PropertyMetadata(false, (viewer, _) => ((SheetDocumentViewer)viewer).UpdateBands()));

    /// <inheritdoc cref="ShowsSheetProperty"/>
    public bool ShowsSheet
    {
        get => (bool)GetValue(ShowsSheetProperty);
        set => SetValue(ShowsSheetProperty, value);
    }

    /// <inheritdoc cref="NumbersPagesProperty"/>
    public bool NumbersPages
    {
        get => (bool)GetValue(NumbersPagesProperty);
        set => SetValue(NumbersPagesProperty, value);
    }

    /// <summary>Le papier à dessiner ; rien s'il est éteint, sauf pour une vue qui montre toujours sa feuille : une feuille unie.</summary>
    internal PaperStyle? PaperShown => DrawsPaper ? Paper?.StyleOf(Layout?.Format ?? PageFormat.Endless) ?? (ShowsSheet ? PaperStyle.Plain : null) : null;

    /// <summary>La largeur qu'il faut à la vue pour montrer sa feuille entière ; zéro sans feuille.</summary>
    internal double FitWidth => Layout is { IsPaged: true } page ? page.WidthPixels + SystemParameters.VerticalScrollBarWidth + 8 : 0;

    /// <summary>La fiche, sa mise en page ou le papier ont pu changer : la vue s'y remet, si c'est le cas.</summary>
    private void FollowSheet()
    {
        var (layout, style) = (Layout, PaperShown);
        if (layout == _shownLayout && style == _shownStyle)
        {
            return;
        }
        if (Math.Abs(StepOf(style) - _step) > 0.001 && Document is not null)
        {
            // Le pas d'écriture change : les lignes du texte n'ont plus la même hauteur, le document se refait.
            Rebuild();
            if (!_rebuildsWhenShown)
            {
                return;
            }
        }
        var paged = layout != _shownLayout;
        (_shownLayout, _shownStyle) = (layout, style);
        if (paged)
        {
            PageLayoutChanged();
            (VisualTreeHelper.GetParent(this) as UIElement)?.InvalidateMeasure();
        }
        else
        {
            ApplyPagePadding();
            UpdateBands();
        }
        ApplyPaperToText();
    }

    /// <summary>
    /// Sur un papier réglé, les fonds des blocs (code, bandes) laissent voir la réglure : le texte paraît écrit sur la feuille.
    /// </summary>
    private void ApplyPaperToText()
    {
        if (Document is not { } document)
        {
            return;
        }
        const string surface = "Kyf.Brush.SurfaceAlt";
        if (PaperShown is { } style && style != PaperStyle.Plain && TryFindResource(surface) is SolidColorBrush solid)
        {
            var soft = new SolidColorBrush(solid.Color) { Opacity = 0.55 };
            soft.Freeze();
            document.Resources[surface] = soft;
        }
        else if (document.Resources.Contains(surface))
        {
            document.Resources.Remove(surface);
        }
    }

    /// <summary>Le pas d'écriture d'un papier ; zéro sans papier, ou sur une feuille unie.</summary>
    private static double StepOf(PaperStyle? style) => style is { } ruled ? PaperStyles.LineStepPixels(ruled) : 0;

    /// <summary>La part de la hauteur d'une ligne qui est au-dessus de sa ligne de base, pour cette police.</summary>
    private static double BaselineRatio(FontFamily family) => family.LineSpacing > 0 ? family.Baseline / family.LineSpacing : 0.8;

    /// <summary>
    /// Ce qu'il faut savoir d'un bloc qui écrit sur les lignes du papier. <c>Snap</c> : où le haut de sa première ligne doit
    /// tomber dans le pas pour que sa ligne de base soit sur une ligne du papier (zéro pour un paragraphe ordinaire ; autre
    /// chose pour un titre de deux pas, ou une autre police) ; rien pour ce qui n'écrit pas sur les lignes. <c>Lead</c> : ce
    /// qui sépare le haut de cette ligne du haut de ses lettres, car c'est le haut des lettres que le texte sait mesurer.
    /// </summary>
    private (double? Snap, double Lead) Writing(Block block)
    {
        if (_step <= 0)
        {
            return (null, 0);
        }
        switch (block)
        {
            case Paragraph { Tag: null } text when text.LineStackingStrategy == LineStackingStrategy.BlockLineHeight && text.Padding.Top == 0 && text.BorderThickness.Top == 0 && OnStep(text.LineHeight) && FirstAscent(text) is { } ascent:
                // Dans une ligne de hauteur fixée, la ligne de base est à une part constante de cette hauteur, propre à la police.
                var baseline = text.LineHeight * BaselineRatio(text.FontFamily);
                var phase = (_baseline - baseline) % _step;
                if (phase < 0)
                {
                    phase += _step;
                }
                return (phase < 0.25 || _step - phase < 0.25 ? 0 : phase, baseline - ascent);
            case List list when list.ListItems.FirstListItem?.Blocks.FirstBlock is { } head:
                return Writing(head);
            default:
                return (null, 0);
        }
    }

    /// <summary>
    /// La hauteur des premières lettres d'un paragraphe au-dessus de leur ligne de base ; rien s'il commence par autre chose
    /// que du texte (une case, une formule), qui ne se mesure pas ainsi.
    /// </summary>
    private static double? FirstAscent(Paragraph text)
    {
        var start = text.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        return start.GetAdjacentElement(LogicalDirection.Forward) is InlineUIContainer || start.Parent is not TextElement element
            ? null
            : element.FontFamily.Baseline * element.FontSize;
    }

    /// <summary>Cette hauteur de ligne est un nombre entier de pas.</summary>
    private bool OnStep(double height) => height >= _step - 0.01 && Math.Abs((height / _step) - Math.Round(height / _step)) < 0.01;

    private void ShowNoPages()
    {
        _sheets = [];
        _originKnown = false;
        PageBands.Show([], [], null, 0, 0, 0, 0, 0);
        PagePaper.Show([], null, 0);
    }

    private bool IsPaged => Layout is { IsPaged: true };

    /// <summary>
    /// En pages, l'en-tête de la fiche se pose au-dessus de la première feuille, sur le fond, au lieu d'y prendre la place du
    /// texte : la première feuille porte alors le même texte qu'à l'écriture, qui n'a pas cet en-tête, et se coupe au même
    /// endroit. Ailleurs (l'impression, l'aperçu de l'écriture), il reste en haut de la première page.
    /// </summary>
    public bool HeaderAbovePages { get; set; }

    /// <summary>
    /// Le haut de la première ligne du texte, sous l'en-tête de la fiche, depuis le haut de la vue ; rien si la vue a défilé,
    /// ou si le texte n'est pas encore mesuré.
    /// </summary>
    internal double? FirstTextTop()
    {
        if (_scroller is not { VerticalOffset: < 1 } scroller || Document?.Blocks.SkipWhile(block => ReferenceEquals(block.Tag, SheetHeaderMark)).FirstOrDefault() is not { } first)
        {
            return null;
        }
        var line = first.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        if (line.IsEmpty || double.IsInfinity(line.Top))
        {
            return null;
        }
        var scope = scroller.Content as UIElement ?? scroller;
        return line.Top + (scope.IsDescendantOf(this) ? scope.TranslatePoint(default, this).Y : 0);
    }

    /// <summary>Le dernier bloc de l'en-tête de la fiche : son titre ; rien si le texte n'en a pas.</summary>
    private Paragraph? HeaderEnd => Document?.Blocks.TakeWhile(block => ReferenceEquals(block.Tag, SheetHeaderMark)).LastOrDefault() as Paragraph;

    /// <summary>L'en-tête est posé au-dessus des feuilles : il ne compte pas dans leur mise en pages.</summary>
    internal bool HeaderIsAbove => HeaderAbovePages && IsPaged && HeaderEnd is not null;

    /// <summary>La marge du haut du document : celle de la feuille, ou un simple espace quand l'en-tête est au-dessus d'elle.</summary>
    private double TopPadding(PageLayout page) => HeaderIsAbove ? HeaderGap : page.MarginPixels;

    /// <summary>
    /// Met le titre à sa distance du texte : l'espace de toujours quand il est dans la feuille ; au-dessus d'elle, l'espace
    /// qui le sépare de son bord, puis la marge de la feuille, puisque le texte commence sous cette marge.
    /// </summary>
    private void PlaceHeader()
    {
        if (HeaderEnd is not { } title)
        {
            return;
        }
        var below = HeaderIsAbove && Layout is { } page ? HeaderGap + page.MarginPixels : HeaderSpacing;
        if (Math.Abs(title.Margin.Bottom - below) > 0.5)
        {
            title.Margin = new Thickness(title.Margin.Left, title.Margin.Top, title.Margin.Right, below);
        }
    }

    /// <summary>
    /// La vue montre le texte entier, sans défiler : elle prend la hauteur de tout le document. C'est ce qu'il faut à
    /// l'impression, qui découpe ensuite les feuilles dans cette longue bande.
    /// </summary>
    internal bool ShowsWhole
    {
        get;
        set
        {
            field = value;
            VerticalScrollBarVisibility = value ? System.Windows.Controls.ScrollBarVisibility.Hidden : System.Windows.Controls.ScrollBarVisibility.Auto;
        }
    }

    /// <summary>La mise en pages vient d'être refaite ou redessinée.</summary>
    internal event EventHandler? PagesShown;

    /// <summary>La hauteur de tout le texte.</summary>
    private void FollowWholeHeight()
    {
        if (ShowsWhole && _scroller is { ExtentHeight: > 0 } scroller && Math.Abs(Height - scroller.ExtentHeight) > 0.5)
        {
            Height = scroller.ExtentHeight;
        }
    }

    /// <summary>
    /// Les feuilles, là où elles sont dans la vue : une par page, marges comprises. Une feuille que déborde un texte plus haut
    /// qu'une page est découpée en autant de pages qu'il faut ; celles-là ne montrent que leur zone de texte.
    /// </summary>
    internal IReadOnlyList<(Rect Sheet, bool TextOnly)> PageRects()
    {
        if (Layout is not { IsPaged: true } page || _scroller is not { } scroller || _sheets.Count == 0)
        {
            return [];
        }
        var usable = page.HeightPixels - (2 * page.MarginPixels);
        var scope = scroller.Content as UIElement ?? scroller;
        var shift = scope.IsDescendantOf(this) ? scope.TranslatePoint(default, this) : default;
        var top = _origin - scroller.VerticalOffset + shift.Y;
        var left = shift.X + Side(page) - page.MarginPixels;
        var rects = new List<(Rect Sheet, bool TextOnly)>();
        foreach (var (sheetTop, sheetEnd) in _sheets)
        {
            var count = Math.Max(1, (int)Math.Ceiling((sheetEnd - sheetTop - 0.5) / usable));
            for (var slice = 0; slice < count; slice++)
            {
                rects.Add((new Rect(left, top + sheetTop + (slice * usable) - page.MarginPixels, page.WidthPixels, page.HeightPixels), count > 1));
            }
        }
        return rects;
    }

    /// <summary>Ajoute tout de suite ce qui reste du texte, sans attendre que la lecture y arrive.</summary>
    internal void CompleteDocument() => _built?.Complete();

    private void PageLayoutChanged()
    {
        ClearPages();
        ApplyPagePadding();
        SchedulePages();
    }

    /// <summary>Les marges de la feuille sont celles du document.</summary>
    private void ApplyPagePadding()
    {
        if (Document is { } document)
        {
            PlaceHeader();
            var padding = Layout is { IsPaged: true } page
                ? new Thickness(Side(page), TopPadding(page), Side(page), page.MarginPixels)
                : Beside(PaperShown is null ? EndlessPadding : EndlessPaperPadding, EndlessSide);
            if (document.PagePadding != padding)
            {
                document.PagePadding = padding;
            }
            // La colonne de texte se taille dans ces marges : elle suit tout ce qui les change.
            FitBlocks();
        }
    }

    private static Thickness Beside(Thickness padding, double side) =>
        new(padding.Left + side, padding.Top, padding.Right + side, padding.Bottom);

    /// <summary>La marge de côté : celle de la feuille, plus la moitié de ce que la vue a de plus large qu'elle.</summary>
    private double Side(PageLayout page) =>
        page.MarginPixels + Math.Max(0, Math.Floor((ActualWidth - SystemParameters.VerticalScrollBarWidth - page.WidthPixels) / 2));

    /// <summary>Retire tout ce que la mise en pages a ajouté au document.</summary>
    private void ClearPages()
    {
        foreach (var (block, margin) in _blockMargins)
        {
            block.Margin = new Thickness(block.Margin.Left, block.Margin.Top - margin, block.Margin.Right, block.Margin.Bottom);
        }
        foreach (var (row, spacer) in _rowSpacers.Where(pair => !_repeats.ContainsKey(pair.Key)))
        {
            PadRow(row, -spacer);
        }
        foreach (var repeat in _repeats.Values)
        {
            (repeat.Parent as TableRowGroup)?.Rows.Remove(repeat);
        }
        foreach (var (figure, height) in _shrunk)
        {
            figure.MaxHeight = height;
        }
        _blockSpacers.Clear();
        _blockMargins.Clear();
        _passes = 0;
        _rowSpacers.Clear();
        _repeats.Clear();
        _shrunk.Clear();
        _breaks = [];
        ShowNoPages();
        SetPageCount(0);
    }

    /// <summary>Un document neuf n'a rien gardé de la mise en pages du précédent.</summary>
    private void ForgetPages()
    {
        _blockSpacers.Clear();
        _blockMargins.Clear();
        _passes = 0;
        _rowSpacers.Clear();
        _repeats.Clear();
        _shrunk.Clear();
        _breaks = [];
        ShowNoPages();
        if (Layout != _shownLayout)
        {
            (VisualTreeHelper.GetParent(this) as UIElement)?.InvalidateMeasure();
        }
        (_shownLayout, _shownStyle) = (Layout, PaperShown);
        ApplyPagePadding();
        ApplyPaperToText();
        UpdateBands();
        SchedulePages();
    }

    /// <summary>La vue a changé de taille : la mise en pages repart, même si elle s'en était tenue à un état.</summary>
    private void RestartPages()
    {
        _passes = 0;
        SchedulePages();
    }

    /// <summary>La mise en pages se refait une fois le texte mesuré, et une seule fois pour plusieurs demandes.</summary>
    private void SchedulePages()
    {
        if (_paginates || !(IsPaged || _step > 0) || _passes > MaximumPasses)
        {
            return;
        }
        _paginates = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _paginates = false;
            Paginate();
        });
    }

    /// <summary>
    /// Mesure les blocs, calcule les sauts de page et ouvre les espaces qui manquent. Sur un papier réglé, les mêmes espaces
    /// calent le texte sur les lignes, que la feuille ait des pages ou qu'elle soit sans fin.
    /// </summary>
    internal void Paginate()
    {
        var page = IsPaged ? Layout : null;
        if ((page is null && _step <= 0) || Document is not { } document || _scroller is not { } scroller || !IsVisible || ActualHeight <= 0)
        {
            return;
        }
        _baseline = _step > 0 ? document.LineHeight * BaselineRatio(document.FontFamily) : 0;
        var usable = page is null ? double.PositiveInfinity : page.HeightPixels - (2 * page.MarginPixels);
        if (RemoveStrayRepeats(document))
        {
            // Le texte vient de changer : il se mesure de nouveau avant d'être mis en pages.
            SchedulePages();
            return;
        }
        var units = new List<Unit>();
        Collect(document.Blocks, scroller.VerticalOffset, units, continues: false);
        if (units.Count == 0)
        {
            _breaks = [];
            // Sans texte, la feuille commence sous sa marge ; sous l'en-tête, s'il est au-dessus d'elle.
            _origin = HeaderIsAbove && HeaderEnd is { } alone && Measure(alone.ContentStart, alone.ContentEnd, scroller.VerticalOffset) is (var aloneTop, var aloneHeight)
                ? aloneTop + aloneHeight + alone.Margin.Bottom
                : (page?.MarginPixels ?? document.PagePadding.Top) - scroller.VerticalOffset;
            _sheets = page is null ? [] : [(0, usable)];
            SetPageCount(page is null ? 0 : 1);
            UpdateBands();
            return;
        }
        // Les hauts naturels : là où serait chaque bloc sans rien d'ajouté, depuis le premier.
        var first = units[0].Top - units[0].Added;
        var boxes = new List<PageBox>(units.Count);
        var added = 0d;
        var shrunk = 0d;
        foreach (var unit in units)
        {
            added += unit.Added;
            var natural = unit.Height;
            if (unit.Element is BlockUIContainer { Child: FrameworkElement figure } && _shrunk.ContainsKey(figure))
            {
                // Une figure déjà rétrécie reste comptée plus haute qu'une page : le calcul la rétrécit de nouveau.
                natural = Math.Max(natural, usable + 1);
            }
            boxes.Add(new PageBox(unit.Top - added - first + shrunk, natural, unit.Kind, unit.Continues, unit.Lead, unit.Snap));
            shrunk += natural - unit.Height;
        }
        var result = PageBreaks.Compute(boxes, usable, page is null ? 0 : (2 * page.MarginPixels) + PageGap, _step);
        var changed = false;
        for (var index = 0; index < units.Count; index++)
        {
            changed |= Place(units[index], result.Placements[index]);
        }
        _origin = first;
        _originKnown = true;
        _breaks = result.Breaks;
        if (page is null)
        {
            _sheets = [];
        }
        else
        {
            // Les feuilles : chacune va du haut de son texte au bas de sa zone de texte ; les marges s'y ajoutent au dessin.
            var sheets = new List<(double Top, double End)>();
            var sheetTop = 0d;
            foreach (var pageBreak in result.Breaks)
            {
                sheets.Add((sheetTop, pageBreak.PageEnd));
                sheetTop = pageBreak.NextTop;
            }
            // Un texte plus haut qu'une page allonge la dernière feuille : elle sera découpée à l'impression.
            sheets.Add((sheetTop, Math.Max(sheetTop + usable, units[^1].Top + units[^1].Height - first)));
            _sheets = sheets;
            // La dernière feuille est entière : sa marge du bas vient après la place restée libre.
            var bottom = page.MarginPixels + (HasPendingWork ? 0 : result.Remaining);
            var side = Side(page);
            if (Math.Abs(document.PagePadding.Bottom - bottom) > 0.5 || Math.Abs(document.PagePadding.Left - side) > 0.5)
            {
                document.PagePadding = new Thickness(side, TopPadding(page), side, bottom);
            }
        }
        SetPageCount(page is null ? 0 : result.Pages);
        UpdateBands();
        if (changed)
        {
            // Ce qui vient d'être ajouté déplace le texte : une seconde mesure confirme, et ne change plus rien. Si des
            // mesures se contredisaient encore après bien des passes, on s'en tiendrait à la dernière.
            _passes++;
            SchedulePages();
        }
        else
        {
            _passes = 0;
        }
    }

    /// <summary>
    /// Retire les en-têtes répétés que la mise en pages ne connaît plus. Elle seule en pose, et sait pour quelle ligne ; un
    /// en-tête resté sans elle garderait l'espace ouvert au-dessus de lui, caché sous l'écart entre deux feuilles : la feuille
    /// d'avant resterait à moitié vide, et l'écart s'allongerait d'autant.
    /// </summary>
    /// <returns>Vrai si le texte a changé.</returns>
    private bool RemoveStrayRepeats(FlowDocument document)
    {
        var strays = document.Blocks.OfType<Table>()
            .SelectMany(table => table.RowGroups)
            .SelectMany(group => group.Rows)
            .Where(row => ReferenceEquals(row.Tag, RepeatedRowMark) && !_repeats.ContainsValue(row))
            .ToList();
        foreach (var stray in strays)
        {
            (stray.Parent as TableRowGroup)?.Rows.Remove(stray);
        }
        return strays.Count > 0;
    }

    private bool Place(Unit unit, PagePlacement placement)
    {
        var changed = false;
        switch (unit.Element)
        {
            case TableRow row:
                changed = PlaceRow(row, unit.Spacer, placement);
                break;
            case Block block when Math.Abs(placement.Spacer - unit.Spacer) > 0.5:
                // Deux marges qui se suivent se confondent : seule la plus grande compte. Pour descendre le bloc d'autant
                // que voulu, sa marge du haut doit donc d'abord rattraper celle du bas du bloc précédent.
                var own = block.Margin.Top - _blockMargins.GetValueOrDefault(block);
                var margin = placement.Spacer > 0.5 ? placement.Spacer + Math.Max(0, BottomMarginBefore(block) - own) : 0;
                block.Margin = new Thickness(block.Margin.Left, own + margin, block.Margin.Right, block.Margin.Bottom);
                Remember(_blockSpacers, block, placement.Spacer);
                Remember(_blockMargins, block, margin);
                changed = true;
                break;
        }
        if (placement.ShrinkTo is { } height && unit.Element is BlockUIContainer { Child: FrameworkElement figure } && !_shrunk.ContainsKey(figure))
        {
            _shrunk[figure] = figure.MaxHeight;
            figure.MaxHeight = height;
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// Une ligne de tableau n'a pas de marge : l'espace s'ouvre en haut de ses cellules, ou, quand l'en-tête est répété
    /// au-dessus d'elle, en haut de celles de l'en-tête répété.
    /// </summary>
    private bool PlaceRow(TableRow row, double applied, PagePlacement placement)
    {
        var repeat = _repeats.GetValueOrDefault(row);
        var wanted = placement.Repeats && placement.Spacer > 0.5 && HeaderOf(row) is not null;
        if (wanted == (repeat is not null) && Math.Abs(placement.Spacer - applied) <= 0.5)
        {
            return false;
        }
        // On repart de la ligne nue, puis on pose ce qui est voulu.
        if (repeat is not null)
        {
            (repeat.Parent as TableRowGroup)?.Rows.Remove(repeat);
            _repeats.Remove(row);
        }
        else if (applied > 0)
        {
            PadRow(row, -applied);
        }
        if (wanted && HeaderOf(row) is { } header && row.Parent is TableRowGroup group)
        {
            var copy = Repeated(header);
            PadRow(copy, placement.Spacer);
            group.Rows.Insert(group.Rows.IndexOf(row), copy);
            _repeats[row] = copy;
        }
        else if (placement.Spacer > 0.5)
        {
            PadRow(row, placement.Spacer);
        }
        Remember(_rowSpacers, row, placement.Spacer);
        return true;
    }

    /// <summary>La ligne d'en-tête du tableau d'une ligne ; rien s'il n'en a pas, ou si c'est elle.</summary>
    private static TableRow? HeaderOf(TableRow row) =>
        row.Parent is TableRowGroup { Parent: Table table }
        && table.RowGroups.SelectMany(group => group.Rows).FirstOrDefault() is { } first
        && ReferenceEquals(first.Tag, HeaderRowMark) && !ReferenceEquals(first, row)
            ? first
            : null;

    /// <summary>Une copie de la ligne d'en-tête, pour la répéter : même texte, mêmes couleurs, mêmes traits.</summary>
    private static TableRow Repeated(TableRow header)
    {
        var copy = new TableRow { Tag = RepeatedRowMark };
        foreach (var cell in header.Cells)
        {
            var range = new TextRange(cell.ContentStart, cell.ContentEnd);
            var text = new Paragraph(new Run(range.Text.Trim())) { Margin = new Thickness(0) };
            if (range.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight weight)
            {
                text.FontWeight = weight;
            }
            if (range.GetPropertyValue(TextElement.ForegroundProperty) is Brush ink)
            {
                text.Foreground = ink;
            }
            copy.Cells.Add(new TableCell(text)
            {
                ColumnSpan = cell.ColumnSpan,
                Padding = cell.Padding,
                BorderThickness = cell.BorderThickness,
                BorderBrush = cell.BorderBrush,
                Background = cell.Background,
                TextAlignment = cell.TextAlignment,
            });
        }
        return copy;
    }

    /// <summary>
    /// La marge du bas de ce qui précède un bloc : son voisin, ou le dernier bloc de l'élément de liste d'avant. Un tableau
    /// laisse passer la marge qui le précède : après lui, c'est la plus grande des deux qui compte (mesuré, sur un tableau
    /// venu après une liste : le texte qui le suit ne descendait pas tant que sa marge n'avait pas rattrapé celle de la liste).
    /// </summary>
    private static double BottomMarginBefore(Block block) => block.PreviousBlock switch
    {
        Table table => Math.Max(table.Margin.Bottom, Math.Max(table.Margin.Top, BottomMarginBefore(table))),
        { } before => before.Margin.Bottom,
        null => block.Parent is ListItem { PreviousListItem.Blocks.LastBlock: { } last } ? last.Margin.Bottom : 0,
    };

    private static void Remember<T>(Dictionary<T, double> spacers, T element, double spacer)
        where T : notnull
    {
        if (spacer > 0.5)
        {
            spacers[element] = spacer;
        }
        else
        {
            spacers.Remove(element);
        }
    }

    private static void PadRow(TableRow row, double change)
    {
        foreach (var cell in row.Cells)
        {
            cell.Padding = new Thickness(cell.Padding.Left, Math.Max(0, cell.Padding.Top + change), cell.Padding.Right, cell.Padding.Bottom);
        }
    }

    /// <summary>
    /// Ce qui se pose d'un seul tenant, de haut en bas : chaque bloc, mais les éléments d'une liste un à un, les lignes d'un
    /// tableau une à une, et le contenu d'une citation ou d'une section bloc par bloc.
    /// </summary>
    private void Collect(IEnumerable<Block> blocks, double offset, List<Unit> units, bool continues)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph header when ReferenceEquals(header.Tag, SheetHeaderMark) && HeaderIsAbove:
                    // Posé au-dessus de la première feuille : il n'est sur aucune page.
                    break;
                case Section section:
                    // Une citation (son trait à gauche la désigne) se poursuit d'un bloc au suivant.
                    var quote = section.BorderThickness.Left > 0;
                    var before = units.Count;
                    Collect(section.Blocks, offset, units, continues);
                    if (quote)
                    {
                        for (var index = before + 1; index < units.Count; index++)
                        {
                            units[index] = units[index] with { Continues = true };
                        }
                    }
                    break;
                case List list when list.ListItems.Count > 1:
                    foreach (var item in list.ListItems)
                    {
                        // L'espace d'un élément s'ouvre au-dessus de son premier bloc.
                        if (item.Blocks.FirstBlock is { } head && Measure(item.ContentStart, item.ContentEnd, offset) is (var top, var height))
                        {
                            var spacer = _blockSpacers.GetValueOrDefault(head);
                            var (snap, lead) = Writing(head);
                            units.Add(new Unit(head, top - lead, height + lead, PageBoxKind.Block, spacer, spacer, continues) { Snap = snap });
                        }
                    }
                    break;
                case Table table:
                    CollectRows(table, offset, units);
                    break;
                case Paragraph { Tag: PageMark mark } paragraph:
                    if (Measure(paragraph.ContentStart, paragraph.ContentEnd, offset) is (var markTop, var markHeight))
                    {
                        units.Add(new Unit(paragraph, markTop, markHeight, mark == PageMark.BlankPage ? PageBoxKind.BlankPage : PageBoxKind.Break, 0, 0));
                    }
                    break;
                case BlockUIContainer { Child: FrameworkElement figure } container:
                    if (Measure(container.ContentStart, container.ContentEnd, offset) is (var figureTop, _))
                    {
                        var spacer = _blockSpacers.GetValueOrDefault(container);
                        units.Add(new Unit(container, figureTop, Math.Max(figure.ActualHeight, 0), PageBoxKind.Figure, spacer, spacer, continues));
                    }
                    break;
                default:
                    if (Measure(block.ContentStart, block.ContentEnd, offset) is (var blockTop, var blockHeight))
                    {
                        var spacer = _blockSpacers.GetValueOrDefault(block);
                        var (snap, lead) = Writing(block);
                        units.Add(new Unit(block, blockTop - lead, blockHeight + lead, PageBoxKind.Block, spacer, spacer, continues) { Snap = snap });
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Les lignes d'un tableau, une à une. Un en-tête répété n'en est pas une : sa hauteur compte dans ce qui a été ajouté
    /// au-dessus de la ligne qu'il précède.
    /// </summary>
    private void CollectRows(Table table, double offset, List<Unit> units)
    {
        var rows = table.RowGroups.SelectMany(group => group.Rows).ToList();
        var tops = rows.Select(row => MeasureRow(row, offset)).ToList();
        var hasHeader = rows.Count > 1 && ReferenceEquals(rows[0].Tag, HeaderRowMark);
        var lead = hasHeader ? HeaderLead(rows, tops) : 0;
        var real = 0;
        for (var index = 0; index < rows.Count; index++)
        {
            if (ReferenceEquals(rows[index].Tag, RepeatedRowMark) || tops[index] is not (var top, var height))
            {
                continue;
            }
            var spacer = _rowSpacers.GetValueOrDefault(rows[index]);
            var added = spacer;
            if (index > 0 && ReferenceEquals(rows[index - 1].Tag, RepeatedRowMark) && tops[index - 1] is (var repeatTop, _))
            {
                // L'espace est en haut de l'en-tête répété : la ligne est plus bas de cet espace et de la hauteur de l'en-tête.
                added = spacer + Math.Max(0, top - repeatTop);
            }
            units.Add(new Unit(rows[index], top, height, PageBoxKind.Row, added, spacer, Continues: real > 0, Lead: real > 0 ? lead : 0));
            real++;
        }
    }

    /// <summary>
    /// La hauteur de l'en-tête d'un tableau : du haut de son texte au haut du texte de la première ligne. Si cette ligne a été
    /// poussée sous un en-tête répété, c'est lui qui se mesure : l'espace ouvert au-dessus de lui n'est pas de la hauteur de
    /// l'en-tête, et compté comme tel, il décalait toute la suite de la mise en pages.
    /// </summary>
    private double HeaderLead(List<TableRow> rows, List<(double Top, double Height)?> tops)
    {
        var first = rows.FindIndex(1, row => !ReferenceEquals(row.Tag, RepeatedRowMark));
        if (first < 0 || tops[first] is not (var firstTop, _))
        {
            return 0;
        }
        if (ReferenceEquals(rows[first - 1].Tag, RepeatedRowMark) && tops[first - 1] is (var repeatTop, _))
        {
            return Math.Max(0, firstTop - repeatTop);
        }
        return tops[0] is (var headerTop, _) ? Math.Max(0, firstTop - headerTop - _rowSpacers.GetValueOrDefault(rows[first])) : 0;
    }

    /// <summary>Le haut et la hauteur d'un passage du texte, dans le document entier ; rien s'il n'est pas encore mesuré.</summary>
    private static (double Top, double Height)? Measure(TextPointer start, TextPointer end, double offset)
    {
        var first = start.GetCharacterRect(LogicalDirection.Forward);
        var last = LastText(end).GetCharacterRect(LogicalDirection.Backward);
        if (first.IsEmpty || last.IsEmpty || double.IsInfinity(first.Top) || double.IsInfinity(last.Bottom))
        {
            return null;
        }
        return (first.Top + offset, Math.Max(0, last.Bottom - first.Top));
    }

    /// <summary>
    /// La fin du texte d'un élément. La fin d'une cellule ou d'un élément de liste est une position de structure, qui se
    /// mesure mal (une case de plusieurs lignes y paraissait n'en avoir qu'une) : on descend jusqu'à la fin de son dernier
    /// paragraphe.
    /// </summary>
    private static TextPointer LastText(TextPointer end)
    {
        if (end.Parent is not TextElement element)
        {
            return end;
        }
        var outer = element;
        while (true)
        {
            var last = element switch
            {
                TableCell cell => cell.Blocks.LastBlock,
                ListItem item => item.Blocks.LastBlock,
                Section section => section.Blocks.LastBlock,
                List list => list.ListItems.LastListItem as TextElement,
                Table table => table.RowGroups.LastOrDefault()?.Rows.LastOrDefault()?.Cells.LastOrDefault(),
                _ => null,
            };
            if (last is null)
            {
                return ReferenceEquals(element, outer) ? end : element.ContentEnd;
            }
            element = last;
        }
    }

    /// <summary>La hauteur mesurée du texte d'une ligne de tableau, pour les essais.</summary>
    internal static double? RowTextHeight(TableRow row) => MeasureRow(row, 0)?.Height;

    private static (double Top, double Height)? MeasureRow(TableRow row, double offset)
    {
        double? top = null;
        var bottom = 0d;
        foreach (var cell in row.Cells)
        {
            if (Measure(cell.ContentStart, cell.ContentEnd, offset) is (var cellTop, var cellHeight))
            {
                top = Math.Min(top ?? cellTop, cellTop);
                bottom = Math.Max(bottom, cellTop + cellHeight);
            }
        }
        return top is { } found ? (found, bottom - found) : null;
    }

    private void SetPageCount(int pages)
    {
        if (PageCount != pages)
        {
            PageCount = pages;
            PageCountChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Les bandes suivent le texte qui défile.</summary>
    private void UpdateBands()
    {
        if (_scroller is not { } scroller || !PagePaper.IsDescendantOf(this))
        {
            return;
        }
        // Le texte se mesure dans la zone qui défile : les couches sont posées exactement sur elle.
        var scope = scroller.Content as UIElement ?? scroller;
        var shift = scope.IsDescendantOf(this) ? scope.TranslatePoint(default, PagePaper) : default;
        var style = PaperShown;
        _baseline = _step > 0 && Document is { } shown ? shown.LineHeight * BaselineRatio(shown.FontFamily) : 0;
        if (Layout is not { IsPaged: true } page)
        {
            // Une feuille sans fin : rien, ou une seule longue feuille de papier sous tout le texte. Sa réglure part du haut
            // du texte, tel qu'il a été mesuré : la première ligne de base y tombe sur une ligne.
            PageBands.Show([], [], null, 0, 0, 0, 0, 0);
            PagePaper.Show(
                style is null || Document is null ? [] : [new Rect(shift.X + EndlessSide, shift.Y - scroller.VerticalOffset, EndlessSheetWidth(scroller), Math.Max(scroller.ExtentHeight, scroller.ViewportHeight))],
                style,
                EndlessPaperPadding.Left,
                (_originKnown ? _origin : Document?.PagePadding.Top ?? 0) + _baseline,
                _step);
            PagesShown?.Invoke(this, EventArgs.Empty);
            return;
        }
        var top = _origin - scroller.VerticalOffset + shift.Y;
        var left = shift.X + Side(page) - page.MarginPixels;
        var sheets = new List<Rect>(_sheets.Count);
        foreach (var (sheetTop, sheetEnd) in _sheets)
        {
            sheets.Add(new Rect(left, top + sheetTop - page.MarginPixels, page.WidthPixels, sheetEnd - sheetTop + (2 * page.MarginPixels)));
        }
        PageBands.NumbersPages = NumbersPages;
        PageBands.Show(_breaks, sheets, style, top, left, page.WidthPixels, page.MarginPixels, shift.X + scroller.ViewportWidth, page.MarginPixels + _baseline, _step);
        PagePaper.Show(DrawsPaper ? sheets : [], style, page.MarginPixels, page.MarginPixels + _baseline, _step);
        PagesShown?.Invoke(this, EventArgs.Empty);
    }
}
