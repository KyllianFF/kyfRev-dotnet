using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KyfRev.Application.Model;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// Liens vers une autre fiche : taper « [[ » propose les fiches du coffre, et en choisir une écrit un lien
/// Markdown ordinaire, au chemin relatif (« [Mitose](../Bio/Mitose.md) »), lisible partout.
/// </summary>
internal static partial class SheetLinks
{
    /// <summary>Fiches proposées au plus.</summary>
    public const int MaximumProposals = 8;

    private static readonly char[] WordSeparators = [' ', '\t', '\n', '\r', '\'', '’', '-'];

    private static readonly StringComparer FrenchOrder = CultureInfo.GetCultureInfo("fr-FR").CompareInfo.GetStringComparer(CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);

    /// <summary>« [[ » suivi de ce qui est tapé, juste avant le curseur : la recherche et la longueur à remplacer.</summary>
    public static (string Query, int Length)? InputBefore(string lineBefore)
    {
        var match = Input().Match(lineBefore);
        return match.Success ? (match.Groups[1].Value, match.Length) : null;
    }

    /// <summary>
    /// Les fiches qui correspondent, sans casse ni accents, sauf celle qu'on écrit : celles dont le titre
    /// commence par la recherche, puis celles qui ont un mot qui commence par elle, puis les autres.
    /// </summary>
    public static IReadOnlyList<VaultSheet> Propose(IEnumerable<VaultSheet> sheets, string query, string? excluded, int maximum = MaximumProposals)
    {
        var sought = Fold(query.Trim());
        return
        [
            .. sheets
                .Where(sheet => sheet.Id != excluded)
                .Select(sheet => (Sheet: sheet, Rank: Rank(sheet, sought)))
                .Where(entry => entry.Rank >= 0)
                .OrderBy(entry => entry.Rank)
                .ThenBy(entry => entry.Sheet.Title, FrenchOrder)
                .Take(maximum)
                .Select(entry => entry.Sheet),
        ];
    }

    /// <summary>Le chemin de <paramref name="target"/> vu depuis <paramref name="folder"/>, comme le cœur.</summary>
    public static string RelativeFrom(string folder, string target)
    {
        var from = folder.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var to = target.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var common = 0;
        while (common < from.Length && common < to.Length && from[common] == to[common])
        {
            common++;
        }
        return string.Join('/', Enumerable.Repeat("..", from.Length - common).Concat(to.Skip(common)));
    }

    /// <summary>L'adresse du lien : le chemin relatif, avec « # », « ? » et « % » encodés, qu'ils n'ouvrent pas un suffixe.</summary>
    public static string AddressOf(string folder, string target) =>
        ToEncode().Replace(RelativeFrom(folder, target), match => string.Create(CultureInfo.InvariantCulture, $"%{(int)match.Value[0]:X2}"));

    /// <summary>Un lien Markdown : « [Titre](&lt;chemin avec espaces.md&gt;) ».</summary>
    public static string MarkdownLink(string label, string address) =>
        $"[{LabelSigns().Replace(label, @"\$&")}]({Destination(address)})";

    /// <summary>La destination d'un lien, entre chevrons si elle en a besoin.</summary>
    public static string Destination(string address) =>
        address.Length == 0 || NeedsBrackets().IsMatch(address) ? $"<{Brackets().Replace(address, @"\$&")}>" : address;

    private static int Rank(VaultSheet sheet, string sought)
    {
        var title = Fold(sheet.Title);
        if (sought.Length == 0 || title.StartsWith(sought, StringComparison.Ordinal))
        {
            return 0;
        }
        if (title.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries).Any(word => word.StartsWith(sought, StringComparison.Ordinal)))
        {
            return 1;
        }
        return title.Contains(sought, StringComparison.Ordinal) || Fold(sheet.Folder).Contains(sought, StringComparison.Ordinal) ? 2 : -1;
    }

    private static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }
        return builder.ToString();
    }

    [GeneratedRegex(@"\[\[([^\[\]\n]{0,80})$")]
    private static partial Regex Input();

    [GeneratedRegex("[#?%]")]
    private static partial Regex ToEncode();

    [GeneratedRegex(@"[\\\[\]*_`]")]
    private static partial Regex LabelSigns();

    [GeneratedRegex(@"[\s<>()]")]
    private static partial Regex NeedsBrackets();

    [GeneratedRegex("[<>]")]
    private static partial Regex Brackets();
}
