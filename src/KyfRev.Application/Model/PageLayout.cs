using System.Globalization;

namespace KyfRev.Application.Model;

/// <summary>Le format de la feuille d'une fiche.</summary>
public enum PageFormat
{
    /// <summary>Une feuille sans fin, sans pages : la fiche telle qu'elle a toujours été.</summary>
    Endless,

    /// <summary>21 × 29,7 cm.</summary>
    A4,

    /// <summary>14,8 × 21 cm.</summary>
    A5,

    /// <summary>10,5 × 14,8 cm.</summary>
    A6,
}

/// <summary>
/// La mise en page d'une fiche : le format de sa feuille, son orientation et ses marges. Elle règle l'affichage (lire,
/// réviser, écrire) et ne change rien au texte de la fiche.
/// </summary>
/// <param name="Format">Le format de la feuille.</param>
/// <param name="Landscape">En paysage ; sinon en portrait. Sans effet sur une feuille sans fin.</param>
/// <param name="MarginCm">Les marges, en centimètres, les mêmes sur les quatre côtés.</param>
public sealed record PageLayout(PageFormat Format = PageFormat.Endless, bool Landscape = false, double MarginCm = PageLayout.NormalMargin)
{
    /// <summary>Marges étroites, en centimètres.</summary>
    public const double NarrowMargin = 1.27;

    /// <summary>Marges normales, en centimètres.</summary>
    public const double NormalMargin = 2;

    /// <summary>Marges larges, en centimètres.</summary>
    public const double WideMargin = 3;

    /// <summary>La plus petite marge acceptée, en centimètres.</summary>
    public const double MinimumMargin = 0.5;

    /// <summary>Un centimètre, en points d'écran (96 par pouce).</summary>
    public const double PixelsPerCm = 96 / 2.54;

    /// <summary>La feuille sans fin : la mise en page de toute fiche qui n'en a pas choisi d'autre.</summary>
    public static PageLayout Default { get; } = new();

    /// <summary>La feuille a des pages.</summary>
    public bool IsPaged => Format != PageFormat.Endless;

    /// <summary>Largeur de la feuille, en centimètres ; zéro pour une feuille sans fin.</summary>
    public double WidthCm => Landscape ? Long : Short;

    /// <summary>Hauteur de la feuille, en centimètres ; zéro pour une feuille sans fin.</summary>
    public double HeightCm => Landscape ? Short : Long;

    /// <summary>La plus grande marge que la feuille accepte : il reste toujours quatre centimètres de texte.</summary>
    public double MaximumMarginCm => IsPaged ? Math.Max(MinimumMargin, (Short - 4) / 2) : 5;

    /// <summary>Les marges réellement appliquées : celles choisies, ramenées à ce que la feuille accepte.</summary>
    public double EffectiveMarginCm => Math.Clamp(double.IsFinite(MarginCm) ? MarginCm : NormalMargin, MinimumMargin, MaximumMarginCm);

    /// <summary>Largeur de la feuille, en points d'écran.</summary>
    public double WidthPixels => Math.Round(WidthCm * PixelsPerCm);

    /// <summary>Hauteur de la feuille, en points d'écran.</summary>
    public double HeightPixels => Math.Round(HeightCm * PixelsPerCm);

    /// <summary>Les marges, en points d'écran.</summary>
    public double MarginPixels => Math.Round(EffectiveMarginCm * PixelsPerCm);

    private double Short => Format switch
    {
        PageFormat.A4 => 21,
        PageFormat.A5 => 14.8,
        PageFormat.A6 => 10.5,
        _ => 0,
    };

    private double Long => Format switch
    {
        PageFormat.A4 => 29.7,
        PageFormat.A5 => 21,
        PageFormat.A6 => 14.8,
        _ => 0,
    };

    /// <summary>La mise en page en un mot court : « a5 paysage 1.5 », ou « sans-fin ».</summary>
    public string Written() =>
        IsPaged
            ? string.Create(CultureInfo.InvariantCulture, $"{Name(Format)} {(Landscape ? "paysage" : "portrait")} {EffectiveMarginCm:0.##}")
            : "sans-fin";

    private static string Name(PageFormat format) => format switch
    {
        PageFormat.A4 => "a4",
        PageFormat.A5 => "a5",
        PageFormat.A6 => "a6",
        _ => "sans-fin",
    };

    /// <summary>Lit ce que <see cref="Written"/> écrit ; tout ce qui ne se lit pas donne la feuille sans fin.</summary>
    public static PageLayout Parse(string? text)
    {
        var words = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0 || !Enum.TryParse<PageFormat>(words[0], ignoreCase: true, out var format) || !Enum.IsDefined(format) || format == PageFormat.Endless)
        {
            return Default;
        }
        var landscape = words.Length > 1 && words[1] == "paysage";
        var margin = words.Length > 2 && double.TryParse(words[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var read) ? read : NormalMargin;
        var layout = new PageLayout(format, landscape, margin);
        return layout with { MarginCm = layout.EffectiveMarginCm };
    }
}

/// <summary>
/// Les mises en page choisies « pour cette fiche », par identifiant de fiche. Deux listes de même contenu sont égales, pour
/// que les préférences qui les portent se comparent toujours par leur valeur.
/// </summary>
public sealed class SheetPageLayouts : IEquatable<SheetPageLayouts>
{
    /// <summary>Au-delà, les plus anciennes entrées ne sont plus retenues.</summary>
    public const int Maximum = 2000;

    private readonly SortedDictionary<string, PageLayout> _layouts;

    private SheetPageLayouts(SortedDictionary<string, PageLayout> layouts)
    {
        _layouts = layouts;
    }

    /// <summary>Aucune fiche n'a sa propre mise en page.</summary>
    public static SheetPageLayouts None { get; } = new(new SortedDictionary<string, PageLayout>(StringComparer.Ordinal));

    /// <summary>Les fiches qui ont leur propre mise en page, dans l'ordre de leurs identifiants.</summary>
    public IReadOnlyCollection<KeyValuePair<string, PageLayout>> Items => _layouts;

    /// <summary>La mise en page propre à cette fiche ; rien si elle suit celle de toutes les fiches.</summary>
    public PageLayout? Of(string sheetId) => _layouts.GetValueOrDefault(sheetId);

    /// <summary>Donne sa mise en page à une fiche ; sans mise en page, la fiche suit de nouveau celle de toutes les fiches.</summary>
    public SheetPageLayouts With(string sheetId, PageLayout? layout)
    {
        ArgumentException.ThrowIfNullOrEmpty(sheetId);
        var layouts = new SortedDictionary<string, PageLayout>(_layouts, StringComparer.Ordinal);
        if (layout is null)
        {
            layouts.Remove(sheetId);
        }
        else if (layouts.ContainsKey(sheetId) || layouts.Count < Maximum)
        {
            layouts[sheetId] = layout;
        }
        return new SheetPageLayouts(layouts);
    }

    /// <inheritdoc/>
    public bool Equals(SheetPageLayouts? other) => other is not null && _layouts.SequenceEqual(other._layouts);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SheetPageLayouts);

    /// <inheritdoc/>
    public override int GetHashCode() => _layouts.Aggregate(_layouts.Count, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value));
}

/// <summary>Le papier d'une feuille : ce qui se dessine derrière le texte, à l'écran seulement. Rien de cela ne s'imprime.</summary>
public enum PaperStyle
{
    /// <summary>Une feuille unie.</summary>
    Plain,

    /// <summary>Petits carreaux : un quadrillage de 5 mm.</summary>
    SmallSquares,

    /// <summary>Grands carreaux, la réglure des cahiers d'écolier : des lignes tous les 8 mm, trois interlignes fins, une marge rouge.</summary>
    LargeSquares,

    /// <summary>Lignes : des lignes horizontales tous les 8 mm, comme un cahier ligné, et une marge rouge.</summary>
    Lines,

    /// <summary>Points : un point tous les 5 mm.</summary>
    Dots,

    /// <summary>Millimétré : un quadrillage au millimètre, renforcé tous les 5 et 10 mm.</summary>
    Graph,
}

/// <summary>Les noms sous lesquels les papiers s'écrivent, dans les préférences comme pour l'éditeur.</summary>
public static class PaperStyles
{
    /// <summary>Tous les papiers, dans l'ordre où on les propose.</summary>
    public static IReadOnlyList<PaperStyle> All { get; } = [PaperStyle.Plain, PaperStyle.SmallSquares, PaperStyle.LargeSquares, PaperStyle.Lines, PaperStyle.Dots, PaperStyle.Graph];

    /// <summary>Tous les types de feuille, dans l'ordre où on les propose.</summary>
    public static IReadOnlyList<PageFormat> Formats { get; } = [PageFormat.Endless, PageFormat.A4, PageFormat.A5, PageFormat.A6];

    /// <summary>Le nom écrit d'un papier.</summary>
    public static string Name(PaperStyle style) => style switch
    {
        PaperStyle.SmallSquares => "petits-carreaux",
        PaperStyle.LargeSquares => "grands-carreaux",
        PaperStyle.Lines => "lignes",
        PaperStyle.Dots => "points",
        PaperStyle.Graph => "millimetre",
        _ => "uni",
    };

    /// <summary>
    /// Le pas d'écriture d'un papier, en centimètres : l'écart entre deux lignes où le texte se pose. Les grands carreaux
    /// et les lignes en offrent une tous les 8 mm ; sur les petits carreaux, les points et le millimétré, on écrit une
    /// ligne sur deux (tous les 10 mm), faute de quoi le texte ne tiendrait pas dans un carreau de 5 mm. Zéro pour la
    /// feuille unie : rien n'y cale le texte.
    /// </summary>
    public static double LineStepCm(PaperStyle style) => style switch
    {
        PaperStyle.LargeSquares or PaperStyle.Lines => 0.8,
        PaperStyle.SmallSquares or PaperStyle.Dots or PaperStyle.Graph => 1,
        _ => 0,
    };

    /// <summary>
    /// Le pas d'écriture d'un papier, en points d'écran ; zéro pour la feuille unie. Il est arrondi au trois-centième de
    /// pouce, l'unité dans laquelle la lecture pose ses lignes : sans cela, le texte glisserait d'un sixième de point par
    /// ligne par rapport à la réglure. Le décor y perd un demi pour cent sur de vrais millimètres.
    /// </summary>
    public static double LineStepPixels(PaperStyle style) => Math.Round(LineStepCm(style) * PageLayout.PixelsPerCm * 300 / 96) * 96 / 300;

    /// <summary>
    /// La police conseillée pour un papier : celle de l'application sur une feuille unie ou à petits carreaux, une
    /// manuscrite sur les grands carreaux d'un cahier, des empattements sur un papier ligné, le feutre sur des points,
    /// la chasse fixe sur le millimétré.
    /// </summary>
    public static ReadingFont RecommendedFont(PaperStyle style) => style switch
    {
        PaperStyle.LargeSquares => ReadingFont.Handwriting,
        PaperStyle.Lines => ReadingFont.Serif,
        PaperStyle.Dots => ReadingFont.Marker,
        PaperStyle.Graph => ReadingFont.Typewriter,
        _ => ReadingFont.Sans,
    };

    /// <summary>Le papier que dit un nom ; tout autre texte donne la feuille unie.</summary>
    public static PaperStyle Parse(string? name) => All.FirstOrDefault(style => Name(style) == name);

    /// <summary>Le nom écrit d'un type de feuille : « sans-fin », « a4 », « a5 », « a6 ».</summary>
    public static string FormatName(PageFormat format) => format switch
    {
        PageFormat.A4 => "a4",
        PageFormat.A5 => "a5",
        PageFormat.A6 => "a6",
        _ => "sans-fin",
    };
}

/// <summary>
/// Le papier choisi pour chaque type de feuille (sans fin, A4, A5, A6), et s'il est montré. C'est un réglage de
/// l'ordinateur, le même pour toutes les fiches.
/// </summary>
/// <param name="IsOn">Le papier est montré.</param>
/// <param name="Endless">Celui de la feuille sans fin.</param>
/// <param name="A4">Celui des feuilles A4.</param>
/// <param name="A5">Celui des feuilles A5.</param>
/// <param name="A6">Celui des feuilles A6.</param>
public sealed record PaperChoices(bool IsOn = false, PaperStyle Endless = PaperStyle.Plain, PaperStyle A4 = PaperStyle.Plain, PaperStyle A5 = PaperStyle.Plain, PaperStyle A6 = PaperStyle.Plain)
{
    /// <summary>Rien n'est choisi : pas de papier, et des feuilles unies.</summary>
    public static PaperChoices Default { get; } = new();

    /// <summary>Le papier d'un type de feuille.</summary>
    public PaperStyle Of(PageFormat format) => format switch
    {
        PageFormat.A4 => A4,
        PageFormat.A5 => A5,
        PageFormat.A6 => A6,
        _ => Endless,
    };

    /// <summary>Change le papier d'un type de feuille.</summary>
    public PaperChoices With(PageFormat format, PaperStyle style) => format switch
    {
        PageFormat.A4 => this with { A4 = style },
        PageFormat.A5 => this with { A5 = style },
        PageFormat.A6 => this with { A6 = style },
        _ => this with { Endless = style },
    };
}
