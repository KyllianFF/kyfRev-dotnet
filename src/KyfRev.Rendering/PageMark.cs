using System.Text.RegularExpressions;

namespace KyfRev.Rendering;

/// <summary>Ce qu'une ligne de mise en pages demande.</summary>
public enum PageMark
{
    /// <summary>Rien : un bloc ordinaire.</summary>
    None,

    /// <summary>Un saut de page : ce qui suit commence la page suivante.</summary>
    Break,

    /// <summary>Une page blanche : une page entière reste vide, et ce qui suit commence celle d'après.</summary>
    BlankPage,
}

/// <summary>
/// Le saut de page et la page blanche d'une fiche. Chacun s'écrit dans une ligne à part, un commentaire que les autres
/// outils Markdown n'affichent pas : <c>&lt;!-- kyfrev saut-de-page --&gt;</c> et <c>&lt;!-- kyfrev page-blanche --&gt;</c>.
/// Ils n'ont d'effet que si la fiche a une feuille (A4, A5, A6) ; sur la feuille sans fin, ils ne montrent rien.
/// </summary>
public static partial class PageMarks
{
    /// <summary>La ligne d'un saut de page, telle qu'elle s'écrit.</summary>
    public const string BreakLine = "<!-- kyfrev saut-de-page -->";

    /// <summary>La ligne d'une page blanche, telle qu'elle s'écrit.</summary>
    public const string BlankPageLine = "<!-- kyfrev page-blanche -->";

    /// <summary>Ce que demande une ligne ; rien si elle n'est pas une ligne de mise en pages.</summary>
    public static PageMark FromLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var match = Line().Match(line);
        return !match.Success ? PageMark.None : match.Groups["mot"].Value == "saut-de-page" ? PageMark.Break : PageMark.BlankPage;
    }

    [GeneratedRegex(@"^\s{0,3}<!--\s*kyfrev (?<mot>saut-de-page|page-blanche)\s*-->\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Line();
}
