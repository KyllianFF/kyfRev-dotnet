using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// Une icône du ruban, dessinée sur une grille de 24 : des traits, et au besoin des aplats pleins, des aplats légers et
/// des traits estompés. Elle n'a pas de couleur à elle : elle prend celle du texte qui l'entoure.
/// </summary>
/// <param name="Lines">Ses traits.</param>
/// <param name="Solid">Ce qui est rempli, pleinement.</param>
/// <param name="Soft">Ce qui est rempli, légèrement : une bande, une colonne désignée.</param>
/// <param name="Faint">Ses traits estompés : ce que l'action retire, ou ce qui n'est là que pour situer.</param>
public sealed record RibbonIcon(Geometry? Lines, Geometry? Solid = null, Geometry? Soft = null, Geometry? Faint = null);

/// <summary>
/// Les icônes des onglets Tableau et Image du ruban. Chacune montre ce que fait son bouton : un tableau dont la première
/// ligne est pleine pour « Première ligne », dont seul le contour est tracé pour « Contour »… Le nom de l'action reste
/// dans l'infobulle et pour le lecteur d'écran.
/// </summary>
public static class RibbonIcons
{
    private const string Grid = "M9 4v16M15 4v16M3 9.3h18M3 14.7h18";

    private static readonly Geometry Frame = Box(3, 4, 18, 16, 2);

    private static readonly Geometry Table = All(Frame, Lines(Grid));

    private static readonly Geometry Picture = All(Box(3, 5, 18, 14, 2), Lines("M3 16l5-5 4 4 3-3 6 6"));

    /// <summary>La première ligne est l'en-tête.</summary>
    public static RibbonIcon HeaderRow { get; } = new(Table, Solid: Box(3, 4, 18, 5.3));

    /// <summary>La première colonne est un en-tête.</summary>
    public static RibbonIcon HeaderColumn { get; } = new(Table, Solid: Box(3, 4, 6, 16));

    /// <summary>La dernière ligne ressort.</summary>
    public static RibbonIcon TotalRow { get; } = new(Table, Solid: Box(3, 14.7, 18, 5.3));

    /// <summary>Sans bandes.</summary>
    public static RibbonIcon BandsNone { get; } = new(Table);

    /// <summary>Une ligne sur deux.</summary>
    public static RibbonIcon BandsRows { get; } = new(Table, Soft: Box(3, 9.3, 18, 5.4));

    /// <summary>Une colonne sur deux.</summary>
    public static RibbonIcon BandsColumns { get; } = new(Table, Soft: Box(9, 4, 6, 16));

    /// <summary>Tous les traits.</summary>
    public static RibbonIcon LinesAll { get; } = new(Table);

    /// <summary>Les traits horizontaux.</summary>
    public static RibbonIcon LinesHorizontal { get; } = new(Lines("M3 4h18M3 9.3h18M3 14.7h18M3 20h18"), Faint: Lines("M3 4v16M9 4v16M15 4v16M21 4v16"));

    /// <summary>Les traits verticaux.</summary>
    public static RibbonIcon LinesVertical { get; } = new(Lines("M3 4v16M9 4v16M15 4v16M21 4v16"), Faint: Lines("M3 4h18M3 9.3h18M3 14.7h18M3 20h18"));

    /// <summary>Le contour seul.</summary>
    public static RibbonIcon LinesOutline { get; } = new(Frame, Faint: Lines(Grid));

    /// <summary>Un trait sous l'en-tête.</summary>
    public static RibbonIcon LinesHeader { get; } = new(Lines("M3 9.3h18M3 9.9h18"), Faint: Lines("M3 4h18v16H3zM9 4v16M15 4v16M3 14.7h18"));

    /// <summary>Aucun trait.</summary>
    public static RibbonIcon LinesNone { get; } = new(null, Faint: Lines("M3 4h18v16H3zM9 4v16M15 4v16M3 9.3h18M3 14.7h18"));

    /// <summary>La colonne cachée en révision.</summary>
    public static RibbonIcon HideColumn { get; } = new(All(Frame, Lines("M9 4v16M15 4v16M5 21 19 3")), Soft: Box(9, 4, 6, 16));

    /// <summary>Trier les lignes de A à Z.</summary>
    public static RibbonIcon Sort { get; } = new(Lines("M5 10 7.5 4l2.5 6M5.8 8h3.4M5 14h5l-5 6h5M17 4v16M14 17l3 3 3-3"));

    /// <summary>Une ligne au-dessus.</summary>
    public static RibbonIcon RowAbove { get; } = new(All(Box(3, 11, 18, 9, 2), Lines("M9 11v9M15 11v9M12 2v6M9 5h6")));

    /// <summary>Une ligne en dessous.</summary>
    public static RibbonIcon RowBelow { get; } = new(All(Box(3, 4, 18, 9, 2), Lines("M9 4v9M15 4v9M12 16v6M9 19h6")));

    /// <summary>Retirer la ligne.</summary>
    public static RibbonIcon RowDelete { get; } = new(All(Box(3, 5, 18, 9, 2), Lines("M9 5v9M15 5v9M9 19h6")));

    /// <summary>Une colonne à gauche.</summary>
    public static RibbonIcon ColumnLeft { get; } = new(All(Box(11, 4, 10, 16, 2), Lines("M11 9.3h10M11 14.7h10M2 12h6M5 9v6")));

    /// <summary>Une colonne à droite.</summary>
    public static RibbonIcon ColumnRight { get; } = new(All(Box(3, 4, 10, 16, 2), Lines("M3 9.3h10M3 14.7h10M16 12h6M19 9v6")));

    /// <summary>Retirer la colonne.</summary>
    public static RibbonIcon ColumnDelete { get; } = new(All(Box(5, 4, 9, 16, 2), Lines("M5 9.3h9M5 14.7h9M16.5 12h6")));

    /// <summary>Retirer le tableau.</summary>
    public static RibbonIcon TableDelete { get; } = new(Lines("M7 7l10 10M17 7 7 17"), Faint: Table);

    /// <summary>Le tableau prend toute la largeur de la page.</summary>
    public static RibbonIcon WidthPage { get; } = new(All(Box(2, 8, 20, 10, 2), Lines("M9 8v10M15 8v10M2 2.5v3M22 2.5v3M2 4h20")));

    /// <summary>Le tableau s'ajuste à son contenu.</summary>
    public static RibbonIcon WidthFit { get; } = new(All(Box(6, 7, 12, 10, 2), Lines("M10 7v10M14 7v10M1.5 12h3M19.5 12h3M3 10.5 4.5 12 3 13.5M21 10.5 19.5 12 21 13.5")));

    /// <summary>Rogner l'image.</summary>
    public static RibbonIcon Crop { get; } = new(Lines("M7 2v15h15M2 7h15v15"));

    /// <summary>Sa hauteur.</summary>
    public static RibbonIcon Height { get; } = new(All(Box(8, 4, 12, 16, 2), Lines("M4 4v16M2.5 6 4 4l1.5 2M2.5 18 4 20l1.5-2")));

    /// <summary>Sa largeur.</summary>
    public static RibbonIcon Width { get; } = new(All(Box(4, 8, 16, 12, 2), Lines("M4 4h16M6 2.5 4 4l2 1.5M18 2.5 20 4l-2 1.5")));

    /// <summary>Dans le fil du texte.</summary>
    public static RibbonIcon PlaceInline { get; } = new(All(Box(3, 8.5, 8, 7, 1.5), Lines("M3 5h18M3 19h18M14 9.5h7M14 14.5h7")));

    /// <summary>Seule sur sa ligne, à gauche.</summary>
    public static RibbonIcon PlaceLeft { get; } = new(All(Box(3, 7, 10, 10, 1.5), Lines("M3 3.5h18M3 20.5h18")));

    /// <summary>Seule sur sa ligne, au centre.</summary>
    public static RibbonIcon PlaceCenter { get; } = new(All(Box(7, 7, 10, 10, 1.5), Lines("M3 3.5h18M3 20.5h18")));

    /// <summary>Seule sur sa ligne, à droite.</summary>
    public static RibbonIcon PlaceRight { get; } = new(All(Box(11, 7, 10, 10, 1.5), Lines("M3 3.5h18M3 20.5h18")));

    /// <summary>Masquer des zones de l'image.</summary>
    public static RibbonIcon Masks { get; } = new(Picture, Solid: All(Box(6, 8, 6, 3.5), Box(13, 13, 5, 3)));

    /// <summary>La taille d'origine de l'image.</summary>
    public static RibbonIcon Natural { get; } = new(
        All(Box(3, 5, 18, 14, 2), Lines("M8.5 9.5v5M8.5 9.5 7 10.5M17.5 9.5v5M17.5 9.5 16 10.5")),
        Solid: All(Dot(12.5, 10.7, 0.8), Dot(12.5, 13.5, 0.8)));

    /// <summary>L'image entière, sans rognage.</summary>
    public static RibbonIcon Whole { get; } = new(All(Picture, Circle(16, 9.5, 1.4)));

    /// <summary>Retirer les réglages de l'image.</summary>
    public static RibbonIcon Reset { get; } = new(Lines("M5 12a7 7 0 1 0 2.3-5.2M5 4v4h4"));

    /// <summary>La feuille sans fin : ses bords, et un bas déchiré.</summary>
    public static RibbonIcon PageEndless { get; } = new(Lines("M6 2h12v20M6 2v20M6 22l2-1.5 2 1.5 2-1.5 2 1.5 2-1.5 2 1.5"));

    /// <summary>La feuille A4, la plus grande.</summary>
    public static RibbonIcon PageA4 { get; } = new(All(Box(5, 2, 14, 20, 1.5), Lines("M9 15l1.5-5 1.5 5M9.5 13.5h2M14.5 10v5M14.5 10l-2 3.5h3")));

    /// <summary>La feuille A5.</summary>
    public static RibbonIcon PageA5 { get; } = new(All(Box(6, 4, 12, 16, 1.5), Lines("M8.5 15l1.5-5 1.5 5M9 13.5h2M15.5 10h-2v2.3c1.5-.6 2.3 0 2.3 1.2 0 1.3-1.3 1.8-2.5 1.2")));

    /// <summary>La feuille A6, la plus petite.</summary>
    public static RibbonIcon PageA6 { get; } = new(Box(7.5, 6, 9, 12, 1.5));

    /// <summary>En hauteur : un portrait.</summary>
    public static RibbonIcon PagePortrait { get; } = new(All(Box(6, 3, 12, 18, 1.5), Circle(12, 9.5, 2), Lines("M8.5 17c.5-2.5 6.5-2.5 7 0")));

    /// <summary>En largeur : un paysage.</summary>
    public static RibbonIcon PageLandscape { get; } = new(All(Box(3, 6, 18, 12, 1.5), Lines("M3 15l5-4 4 3 3-2 6 4")));

    /// <summary>Des marges étroites.</summary>
    public static RibbonIcon MarginsNarrow { get; } = new(Box(4, 3, 16, 18, 1.5), Faint: Lines("M6 3v18M18 3v18M4 5h16M4 19h16"));

    /// <summary>Des marges normales.</summary>
    public static RibbonIcon MarginsNormal { get; } = new(Box(4, 3, 16, 18, 1.5), Faint: Lines("M7.5 3v18M16.5 3v18M4 6.5h16M4 17.5h16"));

    /// <summary>Des marges larges.</summary>
    public static RibbonIcon MarginsWide { get; } = new(Box(4, 3, 16, 18, 1.5), Faint: Lines("M9.5 3v18M14.5 3v18M4 8h16M4 16h16"));

    /// <summary>Des marges sur mesure.</summary>
    public static RibbonIcon MarginsCustom { get; } = new(All(Box(4, 3, 16, 18, 1.5), Lines("M13 13h5M15.5 10.5v5")), Faint: Lines("M8 3v18M4 7h16"));

    /// <summary>Le saut de page : la fin d'une feuille, un trait de coupe, le début de la suivante.</summary>
    public static RibbonIcon PageBreak { get; } = new(Lines("M5 2v6h14V2M5 22v-6h14v6M9 5h6M9 19h6"), Faint: Lines("M2 12h2.5M6.5 12h2.5M11 12h2M15 12h2.5M19.5 12H22"));

    /// <summary>La page blanche : une feuille vide, et le signe plus de ce qu'on ajoute.</summary>
    public static RibbonIcon BlankPage { get; } = new(Lines("M14 2H6v20h12V6zM14 2v4h4M12 11v6M9 14h6"));

    /// <summary>Le papier montré ou caché : une feuille, coin plié, et ses lignes.</summary>
    public static RibbonIcon PaperToggle { get; } = new(Lines("M14 2H6v20h12V6zM14 2v4h4"), Faint: Lines("M9 10h6M9 13.5h6M9 17h6"));

    /// <summary>Papier uni.</summary>
    public static RibbonIcon PaperPlain { get; } = new(Box(4, 3, 16, 18, 1.5));

    /// <summary>Petits carreaux.</summary>
    public static RibbonIcon PaperSmallSquares { get; } = new(Box(4, 3, 16, 18, 1.5), Faint: Lines("M8 3v18M12 3v18M16 3v18M4 7.5h16M4 12h16M4 16.5h16"));

    /// <summary>Grands carreaux : des lignes fortes, des interlignes fins, la marge.</summary>
    public static RibbonIcon PaperLargeSquares { get; } = new(All(Box(4, 3, 16, 18, 1.5), Lines("M4 9h16M4 15h16M8 3v18")), Faint: Lines("M4 6h16M4 12h16M4 18h16M14 3v18"));

    /// <summary>Lignes : un cahier ligné et sa marge.</summary>
    public static RibbonIcon PaperLines { get; } = new(All(Box(4, 3, 16, 18, 1.5), Lines("M8 3v18")), Faint: Lines("M4 7.5h16M4 12h16M4 16.5h16"));

    /// <summary>Points.</summary>
    public static RibbonIcon PaperDots { get; } = new(
        Box(4, 3, 16, 18, 1.5),
        Solid: All(Dot(8, 7.5, 0.9), Dot(12, 7.5, 0.9), Dot(16, 7.5, 0.9), Dot(8, 12, 0.9), Dot(12, 12, 0.9), Dot(16, 12, 0.9), Dot(8, 16.5, 0.9), Dot(12, 16.5, 0.9), Dot(16, 16.5, 0.9)));

    /// <summary>Millimétré : un quadrillage serré, renforcé au milieu.</summary>
    public static RibbonIcon PaperGraph { get; } = new(All(Box(4, 3, 16, 18, 1.5), Lines("M12 3v18M4 12h16")), Faint: Lines("M6.7 3v18M9.3 3v18M14.7 3v18M17.3 3v18M4 6h16M4 9h16M4 15h16M4 18h16"));

    /// <summary>Recto seul : une feuille écrite d'un côté.</summary>
    public static RibbonIcon PrintOneSided { get; } = new(All(Box(6, 3, 12, 18, 1.5), Lines("M9 8h6M9 12h6M9 16h4")));

    /// <summary>Recto verso, bord long : la reliure est sur le grand côté.</summary>
    public static RibbonIcon PrintLongEdge { get; } = new(All(Box(8, 3, 12, 18, 1.5), Lines("M11 8h6M11 12h6M11 16h4")), Solid: Box(4, 3, 2, 18, 1));

    /// <summary>Recto verso, bord court : la reliure est sur le petit côté.</summary>
    public static RibbonIcon PrintShortEdge { get; } = new(All(Box(6, 7, 12, 14, 1.5), Lines("M9 12h6M9 16h6")), Solid: Box(6, 3, 12, 2, 1));

    private static Geometry Lines(string data) => Frozen(Geometry.Parse(data));

    private static Geometry Box(double x, double y, double width, double height, double radius = 0) =>
        Frozen(new RectangleGeometry(new Rect(x, y, width, height), radius, radius));

    private static Geometry Circle(double x, double y, double radius) => Frozen(new EllipseGeometry(new Point(x, y), radius, radius));

    private static Geometry Dot(double x, double y, double radius) => Circle(x, y, radius);

    private static Geometry All(params Geometry[] parts)
    {
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (var part in parts)
        {
            group.Children.Add(part);
        }
        return Frozen(group);
    }

    private static Geometry Frozen(Geometry geometry)
    {
        geometry.Freeze();
        return geometry;
    }
}

/// <summary>
/// Dessine une <see cref="RibbonIcon"/>, à la taille qu'on lui donne et de la couleur du texte autour d'elle : le même
/// bouton va au thème clair, au thème sombre et au contraste élevé sans rien y changer.
/// </summary>
public sealed class RibbonGlyph : FrameworkElement
{
    /// <summary>Le côté de la grille sur laquelle les icônes sont dessinées.</summary>
    private const double Grid = 24;

    /// <summary>L'icône à dessiner.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(RibbonIcon), typeof(RibbonGlyph), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>La couleur des traits : celle du texte, héritée.</summary>
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(RibbonGlyph), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    static RibbonGlyph()
    {
        WidthProperty.OverrideMetadata(typeof(RibbonGlyph), new FrameworkPropertyMetadata(20d));
        HeightProperty.OverrideMetadata(typeof(RibbonGlyph), new FrameworkPropertyMetadata(20d));
        IsHitTestVisibleProperty.OverrideMetadata(typeof(RibbonGlyph), new FrameworkPropertyMetadata(false));
    }

    /// <summary>L'icône à dessiner.</summary>
    public RibbonIcon? Icon
    {
        get => (RibbonIcon?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>La couleur des traits.</summary>
    public Brush? Foreground
    {
        get => (Brush?)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Icon is not { } icon || Foreground is not { } brush || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }
        var pen = new Pen(brush, 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        drawingContext.PushTransform(new ScaleTransform(ActualWidth / Grid, ActualHeight / Grid));
        if (icon.Soft is { } soft)
        {
            drawingContext.PushOpacity(0.45);
            drawingContext.DrawGeometry(brush, null, soft);
            drawingContext.Pop();
        }
        if (icon.Solid is { } solid)
        {
            drawingContext.DrawGeometry(brush, null, solid);
        }
        if (icon.Faint is { } faint)
        {
            drawingContext.PushOpacity(0.35);
            drawingContext.DrawGeometry(null, pen, faint);
            drawingContext.Pop();
        }
        if (icon.Lines is { } lines)
        {
            drawingContext.DrawGeometry(null, pen, lines);
        }
        drawingContext.Pop();
    }
}
