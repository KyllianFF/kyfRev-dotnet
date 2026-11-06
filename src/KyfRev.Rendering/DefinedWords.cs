using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KyfRev.Rendering;

/// <summary>Un terme défini quelque part dans le coffre, à chercher dans une fiche.</summary>
/// <param name="Id">La définition qui le porte.</param>
/// <param name="Term">Le terme, tel qu'il a été défini.</param>
public sealed record DefinedTerm(string Id, string Term);

/// <summary>Un terme défini, trouvé dans le texte affiché d'une fiche.</summary>
/// <param name="Id">La définition.</param>
/// <param name="Start">Son début dans le texte affiché.</param>
/// <param name="End">Sa fin.</param>
public sealed record TermOccurrence(string Id, int Start, int End);

/// <summary>
/// Les mots définis dans une fiche : ils se cherchent à l'affichage, sans jamais rien écrire dans la fiche. Un terme se
/// trouve en mot entier, sans tenir compte des majuscules, au singulier ou au pluriel en « s » ou « x ». Il n'est marqué
/// qu'à sa première apparition dans chaque partie de la fiche, ni dans un titre, ni dans du code, ni dans un lien, ni sur
/// un passage déjà surligné : sinon un mot courant couvrirait la page.
/// </summary>
public static class DefinedWords
{
    /// <summary>Un terme plus court ne se cherche pas : il se trouverait partout.</summary>
    public const int MinimumLength = 2;

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(2);

    private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal) { "h1", "h2", "h3", "h4", "h5", "h6", "code", "pre", "a" };

    /// <summary>Les apparitions des termes dans le texte affiché, dans l'ordre du texte.</summary>
    /// <param name="map">Le texte affiché de la fiche.</param>
    /// <param name="terms">Les termes du coffre ; pour un même terme défini deux fois, le premier l'emporte.</param>
    /// <param name="taken">Ce qui est déjà surligné dans la fiche : un terme n'y est pas marqué une seconde fois.</param>
    public static IReadOnlyList<TermOccurrence> Find(TextMap map, IReadOnlyList<DefinedTerm> terms, IReadOnlyList<(int Start, int End)> taken)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(taken);
        var kept = new List<(string Key, string Id, string Term)>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var term in terms)
        {
            var clean = Clean(term.Term);
            var key = clean.ToUpperInvariant();
            if (clean.Length >= MinimumLength && clean.Any(char.IsLetter) && keys.Add(key))
            {
                kept.Add((key, term.Id, clean));
            }
        }
        if (kept.Count == 0 || map.Text.Length == 0)
        {
            return [];
        }
        // Le plus long d'abord : « état de droit » passe avant « état » quand les deux commencent au même endroit.
        kept.Sort((left, right) => right.Term.Length.CompareTo(left.Term.Length));
        var pattern = new StringBuilder(@"(?<![\p{L}\p{N}_])(?:");
        for (var i = 0; i < kept.Count; i++)
        {
            pattern.Append(i == 0 ? string.Empty : "|").Append("(?<t").Append(i.ToString(CultureInfo.InvariantCulture)).Append('>').Append(Pattern(kept[i].Term)).Append(')');
        }
        pattern.Append(@")(?![\p{L}\p{N}_])");
        var regex = new Regex(pattern.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, Patience);
        var sections = SectionStarts(map);
        var seen = new HashSet<(int Term, int Section)>();
        var found = new List<TermOccurrence>();
        foreach (Match match in regex.Matches(map.Text))
        {
            var index = Enumerable.Range(0, kept.Count).First(i => match.Groups["t" + i.ToString(CultureInfo.InvariantCulture)].Success);
            var (start, end) = (match.Index, match.Index + match.Length);
            if (taken.Any(range => range.Start < end && range.End > start) || !IsPlainText(map, start, end))
            {
                continue;
            }
            if (seen.Add((index, SectionOf(sections, start))))
            {
                found.Add(new TermOccurrence(kept[index].Id, start, end));
            }
        }
        return found;
    }

    /// <summary>Le terme sans espaces de bord ni espaces doublés.</summary>
    private static string Clean(string term) => string.Join(' ', term.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Le motif d'un terme : ses espaces valent toute espace d'une ligne, ses apostrophes les deux formes, et le pluriel est admis.</summary>
    private static string Pattern(string term)
    {
        var pattern = new StringBuilder();
        foreach (var character in term)
        {
            pattern.Append(character switch
            {
                ' ' => @"[\p{Zs}\t]+",
                '\'' or '’' => "['’]",
                _ => Regex.Escape(character.ToString()),
            });
        }
        var last = char.ToLowerInvariant(term[^1]);
        if (char.IsLetter(last) && last is not ('s' or 'x' or 'z'))
        {
            pattern.Append("[sx]?");
        }
        return pattern.ToString();
    }

    /// <summary>Le texte visé n'est fait que de texte courant : ni titre, ni code, ni lien, ni formule.</summary>
    private static bool IsPlainText(TextMap map, int start, int end)
    {
        var any = false;
        foreach (var piece in map.Pieces)
        {
            if (piece.End <= start)
            {
                continue;
            }
            if (piece.Start >= end)
            {
                break;
            }
            any = true;
            if (piece.IsFormula || Ancestors(piece.Node).Any(element => Skipped.Contains(element.Tag)))
            {
                return false;
            }
        }
        return any;
    }

    private static IEnumerable<RenderElement> Ancestors(RenderNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            yield return parent;
        }
    }

    /// <summary>Le début de chaque partie : le premier texte de chaque titre.</summary>
    private static List<int> SectionStarts(TextMap map)
    {
        var headings = map.Headings.ToHashSet();
        var starts = new List<int>();
        RenderElement? current = null;
        foreach (var piece in map.Pieces)
        {
            var heading = Ancestors(piece.Node).FirstOrDefault(headings.Contains);
            if (heading is not null && heading != current)
            {
                starts.Add(piece.Start);
            }
            current = heading;
        }
        return starts;
    }

    private static int SectionOf(List<int> starts, int position)
    {
        var index = starts.BinarySearch(position);
        return index >= 0 ? index + 1 : ~index;
    }
}
