using System.Globalization;
using System.Text;

namespace KyfRev.Application.Text;

/// <summary>
/// Opérations de texte aux règles exactes de Rust, celles de la version 0.x : les caractères y
/// sont des scalaires Unicode (et non des unités UTF-16), et la mise en minuscules suit les
/// correspondances complètes d'Unicode, sigma final compris.
/// </summary>
public static class RustText
{
    /// <summary>Nombre de caractères (<c>chars().count()</c>).</summary>
    public static int CharCount(string text) => text.EnumerateRunes().Count();

    /// <summary>Les <paramref name="count"/> premiers caractères (<c>chars().take(n)</c>).</summary>
    public static string Take(string text, int count)
    {
        var builder = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (count-- == 0)
            {
                break;
            }
            builder.Append(rune.ToString());
        }
        return builder.ToString();
    }

    /// <summary>Mots séparés par des espaces Unicode, rejoints par une espace (<c>split_whitespace().join(" ")</c>).</summary>
    public static string CollapseWhitespace(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Mise en minuscules de <c>str::to_lowercase</c>.</summary>
    public static string ToLowercase(string text)
    {
        var builder = new StringBuilder(text.Length);
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case 0x03A3:
                    builder.Append(IsFinalSigma(text, index) ? 'ς' : 'σ');
                    break;
                case 0x0130:
                    // Seule correspondance complète vers plusieurs caractères (SpecialCasing.txt).
                    builder.Append("i\u0307");
                    break;
                default:
                    builder.Append(Rune.ToLowerInvariant(rune).ToString());
                    break;
            }
            index += rune.Utf16SequenceLength;
        }
        return builder.ToString();
    }

    /// <summary>Égalité après <see cref="ToLowercase"/>.</summary>
    public static bool EqualsIgnoringCase(string left, string right) =>
        string.Equals(ToLowercase(left), ToLowercase(right), StringComparison.Ordinal);

    /// <summary>Sigma final (Unicode, chapitre 3) : précédé d'une lettre à casse, et non suivi d'une.</summary>
    private static bool IsFinalSigma(string text, int index)
    {
        var before = Runes(text[..index]).Reverse();
        var after = Runes(text[(index + 1)..]);
        return CasedAfterIgnorable(before) && !CasedAfterIgnorable(after);
    }

    private static bool CasedAfterIgnorable(IEnumerable<Rune> runes) =>
        runes.SkipWhile(IsCaseIgnorable).Select(rune => (Rune?)rune).FirstOrDefault() is { } first && IsCased(first);

    private static IEnumerable<Rune> Runes(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            yield return rune;
        }
    }

    /// <summary>Propriété Unicode <c>Cased</c> : lettres à casse et caractères « Other_Lowercase » ou « Other_Uppercase ».</summary>
    private static bool IsCased(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
        || Rune.ToLowerInvariant(rune) != rune
        || Rune.ToUpperInvariant(rune) != rune
        || rune.Value is 0xAA or 0xBA or (>= 0x02B0 and <= 0x02B8) or 0x02C0 or 0x02C1 or (>= 0x02E0 and <= 0x02E4)
            or 0x0345 or 0x037A or 0x10FC or (>= 0x1D2C and <= 0x1D6A) or 0x1D78 or (>= 0x1D9B and <= 0x1DBF)
            or 0x2071 or 0x207F or (>= 0x2090 and <= 0x209C) or 0x2C7C or 0x2C7D or 0xA69C or 0xA69D or 0xA770
            or 0xA7F8 or 0xA7F9 or (>= 0xAB5C and <= 0xAB5F);

    /// <summary>Propriété Unicode <c>Case_Ignorable</c>.</summary>
    private static bool IsCaseIgnorable(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format
            or UnicodeCategory.ModifierLetter or UnicodeCategory.ModifierSymbol
        || rune.Value is '\'' or '.' or ':' or '^' or '`' or 0xB7 or 0x0387 or 0x055F or 0x05F4 or 0x2018 or 0x2019
            or 0x2024 or 0x2027 or 0xFE13 or 0xFE52 or 0xFE55 or 0xFF07 or 0xFF0E or 0xFF1A;
}
