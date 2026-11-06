using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KyfRev.Rendering;

/// <summary>Les bandes d'un tableau : une ligne ou une colonne sur deux un peu plus foncée.</summary>
public enum TableBands
{
    /// <summary>Aucune bande.</summary>
    None,

    /// <summary>Une ligne sur deux.</summary>
    Rows,

    /// <summary>Une colonne sur deux.</summary>
    Columns,
}

/// <summary>Les traits d'un tableau.</summary>
public enum TableLines
{
    /// <summary>Tous les traits.</summary>
    All,

    /// <summary>Entre les lignes, et au-dessus et au-dessous du tableau.</summary>
    Horizontal,

    /// <summary>Entre les colonnes, et sur ses côtés.</summary>
    Vertical,

    /// <summary>Le seul contour du tableau.</summary>
    Outline,

    /// <summary>Le seul trait sous l'en-tête (et à droite de la colonne d'en-tête).</summary>
    Header,

    /// <summary>Aucun trait.</summary>
    None,
}

/// <summary>Les thèmes d'un tableau, chacun lisible dans les deux thèmes de l'application.</summary>
public enum TableTheme
{
    /// <summary>Les neutres de l'application : l'apparence d'avant les thèmes.</summary>
    Sober,

    /// <summary>Vert forêt.</summary>
    Forest,

    /// <summary>Ambre.</summary>
    Amber,

    /// <summary>Bleu ardoise.</summary>
    Slate,

    /// <summary>Prune.</summary>
    Plum,

    /// <summary>Encre : le plus contrasté.</summary>
    Ink,
}

/// <summary>
/// L'apparence d'un tableau. Elle s'écrit dans une seule ligne juste au-dessus de lui, <c>&lt;!-- kyfrev tableau … --&gt;</c>,
/// avec ce qui diffère de l'apparence par défaut : GitHub, Obsidian ou VS Code n'affichent pas ce commentaire, le texte du
/// tableau ne change pas, et un tableau sans cette ligne garde l'apparence qu'il a toujours eue. L'alignement des colonnes
/// reste celui du Markdown (<c>:---:</c>).
/// </summary>
public sealed partial record TableLook
{
    /// <summary>Le début de la ligne qui porte l'apparence.</summary>
    public const string Marker = "kyfrev tableau";

    private static readonly Dictionary<TableTheme, string> ThemeNames = new()
    {
        [TableTheme.Sober] = "sobre",
        [TableTheme.Forest] = "foret",
        [TableTheme.Amber] = "ambre",
        [TableTheme.Slate] = "ardoise",
        [TableTheme.Plum] = "prune",
        [TableTheme.Ink] = "encre",
    };

    private static readonly Dictionary<TableLines, string> LineNames = new()
    {
        [TableLines.All] = "tous",
        [TableLines.Horizontal] = "horizontaux",
        [TableLines.Vertical] = "verticaux",
        [TableLines.Outline] = "contour",
        [TableLines.Header] = "entete",
        [TableLines.None] = "aucun",
    };

    /// <summary>L'apparence d'un tableau sans ligne d'apparence.</summary>
    public static TableLook Default { get; } = new();

    /// <summary>La première ligne est l'en-tête (le Markdown en a toujours une ; ici, elle peut se lire comme les autres).</summary>
    public bool HeaderRow { get; init; } = true;

    /// <summary>La première colonne est un en-tête.</summary>
    public bool HeaderColumn { get; init; }

    /// <summary>La dernière ligne ressort : une synthèse, un total.</summary>
    public bool TotalRow { get; init; }

    /// <summary>Les bandes.</summary>
    public TableBands Bands { get; init; }

    /// <summary>Les traits.</summary>
    public TableLines Lines { get; init; }

    /// <summary>Le thème.</summary>
    public TableTheme Theme { get; init; }

    /// <summary>La colonne cachée en révision, à partir de 1 : ses cases deviennent des trous ; rien si aucune ne l'est.</summary>
    public int? HiddenInReview { get; init; }

    /// <summary>Le tableau prend toute la largeur du texte, au lieu de celle de son contenu.</summary>
    public bool FullWidth { get; init; }

    /// <summary>L'apparence écrite dans une ligne de la fiche ; rien si la ligne n'en est pas une.</summary>
    public static TableLook? FromLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var match = Line().Match(line);
        if (!match.Success)
        {
            return null;
        }
        var look = Default;
        foreach (var word in match.Groups["mots"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var (key, value) = word.IndexOf('=', StringComparison.Ordinal) is var equal and > 0 ? (word[..equal], word[(equal + 1)..]) : (word, string.Empty);
            look = key switch
            {
                "entete" => value switch
                {
                    "aucune" => look with { HeaderRow = false, HeaderColumn = false },
                    "colonne" => look with { HeaderRow = false, HeaderColumn = true },
                    "ligne,colonne" or "colonne,ligne" => look with { HeaderRow = true, HeaderColumn = true },
                    _ => look with { HeaderRow = true, HeaderColumn = false },
                },
                "total" => look with { TotalRow = true },
                "bandes" => look with { Bands = value switch { "lignes" => TableBands.Rows, "colonnes" => TableBands.Columns, _ => TableBands.None } },
                "traits" => look with { Lines = LineNames.FirstOrDefault(pair => pair.Value == value, new(TableLines.All, "tous")).Key },
                "theme" => look with { Theme = ThemeNames.FirstOrDefault(pair => pair.Value == value, new(TableTheme.Sober, "sobre")).Key },
                "reviser-cache" => look with { HiddenInReview = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var column) && column > 0 ? column : null },
                "largeur" => look with { FullWidth = value == "page" },
                _ => look,
            };
        }
        return look;
    }

    /// <summary>La ligne qui écrit cette apparence ; rien pour l'apparence par défaut, qui n'en a pas besoin.</summary>
    public string? ToLine()
    {
        var words = new List<string>();
        if (!HeaderRow || HeaderColumn)
        {
            words.Add((HeaderRow, HeaderColumn) switch
            {
                (true, true) => "entete=ligne,colonne",
                (false, true) => "entete=colonne",
                _ => "entete=aucune",
            });
        }
        if (TotalRow)
        {
            words.Add("total");
        }
        if (Bands != TableBands.None)
        {
            words.Add(Bands == TableBands.Rows ? "bandes=lignes" : "bandes=colonnes");
        }
        if (Lines != TableLines.All)
        {
            words.Add("traits=" + LineNames[Lines]);
        }
        if (Theme != TableTheme.Sober)
        {
            words.Add("theme=" + ThemeNames[Theme]);
        }
        if (HiddenInReview is { } column)
        {
            words.Add("reviser-cache=" + column.ToString(CultureInfo.InvariantCulture));
        }
        if (FullWidth)
        {
            words.Add("largeur=page");
        }
        return words.Count == 0 ? null : new StringBuilder("<!-- ").Append(Marker).Append(' ').AppendJoin(' ', words).Append(" -->").ToString();
    }

    [GeneratedRegex(@"^\s{0,3}<!--\s*kyfrev tableau(?<mots>(?:\s+[a-z,=0-9-]+)*)\s*-->\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Line();
}
