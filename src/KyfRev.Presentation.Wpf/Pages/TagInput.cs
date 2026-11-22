using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Une saisie d'étiquettes, séparées par des virgules : ce qu'elle contient, et ce qu'on propose pour celle en cours. La même
/// dans la fenêtre « Étiquettes… » et dans le volet « Nouvelle fiche ».
/// </summary>
internal static class TagInput
{
    /// <summary>Le plus d'étiquettes proposées à la fois.</summary>
    internal const int MaxSuggestions = 8;

    /// <summary>Les étiquettes écrites, sans les vides ; un « # » devant est retiré par le coffre.</summary>
    public static IReadOnlyList<string> Parse(string input) =>
        [.. (input ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    /// <summary>Prend une étiquette proposée : elle remplace celle en cours de saisie, et la suivante peut se taper.</summary>
    public static string Take(string input, string tag)
    {
        var text = input ?? string.Empty;
        var cut = text.LastIndexOf(',');
        var kept = cut < 0 ? string.Empty : text[..(cut + 1)].TrimEnd() + " ";
        return $"{kept}{tag}, ";
    }

    /// <summary>
    /// Les étiquettes du coffre qui commencent comme celle en cours de saisie, sans celles déjà écrites, les plus employées
    /// d'abord : reprendre une étiquette existante évite d'en créer deux pour la même chose.
    /// </summary>
    public static IReadOnlyList<string> Suggest(string input, IEnumerable<string> known)
    {
        var typed = (input ?? string.Empty).Split(',').Select(tag => tag.Trim().TrimStart('#')).ToList();
        var current = ShellViewModel.Fold(typed.Count > 0 ? typed[^1] : string.Empty);
        var written = typed.Take(Math.Max(typed.Count - 1, 0)).Select(ShellViewModel.Fold).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. known
                .GroupBy(tag => tag, StringComparer.Ordinal)
                .Where(group => !written.Contains(ShellViewModel.Fold(group.Key))
                    && ShellViewModel.Fold(group.Key).StartsWith(current, StringComparison.Ordinal)
                    && ShellViewModel.Fold(group.Key) != current)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.CurrentCulture)
                .Take(MaxSuggestions)
                .Select(group => group.Key),
        ];
    }
}
