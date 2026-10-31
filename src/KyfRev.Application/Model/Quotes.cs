using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using KyfRev.Application.Text;

namespace KyfRev.Application.Model;

/// <summary>Nature de la source d'une citation.</summary>
public enum SourceKind
{
    /// <summary>Livre.</summary>
    Book,

    /// <summary>Journal.</summary>
    Newspaper,

    /// <summary>Article.</summary>
    Article,

    /// <summary>Discours.</summary>
    Speech,

    /// <summary>Site.</summary>
    Website,

    /// <summary>Film.</summary>
    Film,

    /// <summary>Chanson.</summary>
    Song,

    /// <summary>Émission.</summary>
    Broadcast,

    /// <summary>Autre.</summary>
    Other,

    /// <summary>Source inconnue, dite explicitement.</summary>
    Unknown,
}

/// <summary>Source d'une citation.</summary>
/// <param name="Kind">Nature de la source.</param>
/// <param name="Title">Titre de l'œuvre.</param>
/// <param name="Page">Page, telle qu'écrite (« 12 », « 12-14 »).</param>
/// <param name="Address">Adresse en ligne, telle que saisie (pas forcément une URL valide).</param>
public sealed record QuoteSource(SourceKind? Kind, string? Title, string? Page, string? Address)
{
    /// <summary>Aucune source.</summary>
    public static QuoteSource None { get; } = new(null, null, null, null);
}

/// <summary>Ce que l'utilisateur saisit ou modifie ; seul le texte est obligatoire.</summary>
/// <param name="Text">Texte de la citation.</param>
/// <param name="Author">Auteur.</param>
/// <param name="Date">Date libre : « 1670 », « mai 1968 », « vers 1650 », « XVIIe siècle ».</param>
/// <param name="Source">Source.</param>
/// <param name="Context">Contexte.</param>
/// <param name="Tags">Étiquettes.</param>
public sealed record QuoteContent(string Text, string? Author, string? Date, QuoteSource Source, string? Context, ImmutableArray<string> Tags);

/// <summary>Une citation du coffre : un fichier <c>.md</c> de <c>citations/</c>.</summary>
/// <param name="Path">Fichier de la citation, relatif au coffre : c'est ce qui la désigne.</param>
/// <param name="Id">Identifiant, absent pour une citation ajoutée à la main.</param>
/// <param name="Content">Contenu.</param>
/// <param name="Year">Année déduite de la date, pour le tri (négative avant notre ère).</param>
/// <param name="AddedOn">Jour d'ajout.</param>
/// <param name="InTrash">Dans <c>citations/.supprimees/</c>.</param>
/// <param name="Origin">D'où vient la citation si kyfRev l'a trouvée en ligne : « wikiquote ».</param>
public sealed record Quote(string Path, string? Id, QuoteContent Content, int? Year, DateOnly? AddedOn, bool InTrash, string? Origin);

/// <summary>Toutes les citations, celles de la corbeille à part, et les fichiers illisibles.</summary>
public sealed record QuoteList(IReadOnlyList<Quote> Quotes, IReadOnlyList<Quote> Trash, IReadOnlyList<UnreadableFile> Unreadable);

/// <summary>Dates libres des citations.</summary>
public static partial class QuoteDates
{
    /// <summary>
    /// Année d'une date libre, pour le tri : « mai 1968 » → 1968, « vers 1650 » → 1650,
    /// « XVIIe siècle » → 1650, « IVe siècle av. J.-C. » → -350.
    /// </summary>
    public static int? YearOf(string date)
    {
        ArgumentNullException.ThrowIfNull(date);
        var lower = RustText.ToLowercase(date);
        var sign = lower.Contains("av. j", StringComparison.Ordinal) || lower.Contains("avant j", StringComparison.Ordinal) ? -1 : 1;
        var numbers = AsciiDigits().Matches(date).Select(m => m.Value).Where(n => n.Length <= 4).ToList();
        // Un nombre de 3 ou 4 chiffres est l'année ; sinon, le premier nombre.
        if ((numbers.FirstOrDefault(n => n.Length >= 3) ?? numbers.FirstOrDefault()) is { } digits)
        {
            return sign * int.Parse(digits, CultureInfo.InvariantCulture);
        }
        // Siècle en chiffres romains, suivi de « e » : « XVIIe ».
        var roman = date.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => TrimEndRepeated(word.TrimEnd('e', 'è'), "er"))
            .FirstOrDefault(word => word.Length > 0 && word.All(c => "IVXLC".Contains(c, StringComparison.Ordinal)));
        if (roman is null)
        {
            return null;
        }
        var total = 0;
        var previous = 0;
        foreach (var c in roman.Reverse())
        {
            var value = c switch
            {
                'I' => 1,
                'V' => 5,
                'X' => 10,
                'L' => 50,
                _ => 100,
            };
            if (value < previous)
            {
                total -= value;
            }
            else
            {
                total += value;
                previous = value;
            }
        }
        return total > 0 ? sign * (((total - 1) * 100) + 50) : null;
    }

    private static string TrimEndRepeated(string text, string suffix)
    {
        while (text.EndsWith(suffix, StringComparison.Ordinal))
        {
            text = text[..^suffix.Length];
        }
        return text;
    }

    [GeneratedRegex("[0-9]+")]
    private static partial Regex AsciiDigits();
}
