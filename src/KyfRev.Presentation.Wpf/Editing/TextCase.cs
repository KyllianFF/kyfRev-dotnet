using System.Globalization;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Les casses du menu, comme dans Word.</summary>
public enum CaseStyle
{
    /// <summary>Majuscule en début de phrase.</summary>
    Sentence,

    /// <summary>minuscules.</summary>
    Lower,

    /// <summary>CAPITALES.</summary>
    Upper,

    /// <summary>Première Lettre Des Mots En Majuscule.</summary>
    Words,

    /// <summary>iNVERSER lA cASSE.</summary>
    Invert,

    /// <summary>uNe LeTtRe SuR dEuX.</summary>
    Alternate,
}

/// <summary>
/// Change la casse d'un texte. Le texte garde sa longueur caractère pour caractère : un caractère qui
/// changerait de longueur (« ß » en capitales) reste tel quel, et la mise en forme ne bouge pas.
/// </summary>
internal static class TextCase
{
    private const string SentenceEnd = ".!?…";
    private const string Closing = "»\"”’')]";
    private const string Link = "'’-";

    /// <summary>L'ordre de Maj+F3 ; l'inversion et une lettre sur deux se choisissent au menu.</summary>
    private static readonly CaseStyle[] Cycle = [CaseStyle.Sentence, CaseStyle.Lower, CaseStyle.Upper, CaseStyle.Words];

    /// <summary>Le texte dans la casse <paramref name="style"/> ; une nouvelle ligne commence une phrase.</summary>
    public static string Apply(string text, CaseStyle style) => style switch
    {
        CaseStyle.Lower => string.Create(text.Length, text, (result, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = char.ToLowerInvariant(source[i]);
            }
        }),
        CaseStyle.Upper => text.ToUpperInvariant(),
        CaseStyle.Words => Words(text),
        CaseStyle.Invert => Invert(text),
        CaseStyle.Alternate => Alternate(text),
        _ => Sentence(text),
    };

    /// <summary>La casse suivante pour Maj+F3 : après celle que le texte a déjà ; sinon, le début de phrase.</summary>
    public static CaseStyle Next(string text)
    {
        var current = Array.FindIndex(Cycle, style => Apply(text, style) == text);
        return current < 0 ? CaseStyle.Sentence : Cycle[(current + 1) % Cycle.Length];
    }

    /// <summary>L'étendue du mot qui touche <paramref name="position"/> ; un clic juste après sa dernière lettre compte.</summary>
    public static (int Start, int End)? WordAround(string text, int position)
    {
        var start = Math.Clamp(position, 0, text.Length);
        if (!IsWordPart(text, start) && IsWordPart(text, start - 1))
        {
            start--;
        }
        if (!IsWordPart(text, start))
        {
            return null;
        }
        var end = start;
        while (IsWordPart(text, start - 1))
        {
            start--;
        }
        while (IsWordPart(text, end))
        {
            end++;
        }
        // Pas d'apostrophe ni de trait d'union aux bords (« 'mot' », « -mot »).
        while (start < end && Link.Contains(text[start], StringComparison.Ordinal))
        {
            start++;
        }
        while (end > start && Link.Contains(text[end - 1], StringComparison.Ordinal))
        {
            end--;
        }
        return HasLetter(text, start, end) ? (start, end) : null;
    }

    private static bool HasLetter(string text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (char.IsLetter(text[i]))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsWordPart(string text, int index)
    {
        if (index < 0 || index >= text.Length)
        {
            return false;
        }
        var c = text[index];
        return IsWithinWord(c) || c == '-';
    }

    /// <summary>Dans un mot : lettre, chiffre, accent combiné ou apostrophe (« l'école », « aujourd'hui »).</summary>
    private static bool IsWithinWord(char c) =>
        char.IsLetterOrDigit(c) || char.IsNumber(c) || c is '\'' or '’' || char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    private static string Words(string text) => string.Create(text.Length, text, (result, source) =>
    {
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            result[i] = !char.IsLetter(c) ? c : i > 0 && IsWithinWord(source[i - 1]) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c);
        }
    });

    private static string Invert(string text) => string.Create(text.Length, text, (result, source) =>
    {
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            result[i] = char.IsUpper(c) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c);
        }
    });

    private static string Alternate(string text) => string.Create(text.Length, text, (result, source) =>
    {
        var rank = 0;
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            result[i] = !char.IsLetter(c) ? c : rank++ % 2 == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
        }
    });

    private static string Sentence(string text) => string.Create(text.Length, text, (result, source) =>
    {
        var start = true;
        var probableEnd = false;
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            result[i] = c;
            if (char.IsLetter(c))
            {
                result[i] = start ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
                start = false;
                probableEnd = false;
            }
            else if (c == '\n')
            {
                start = true;
            }
            else if (SentenceEnd.Contains(c, StringComparison.Ordinal))
            {
                probableEnd = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                start |= probableEnd;
                probableEnd = false;
            }
            else if (!(probableEnd && Closing.Contains(c, StringComparison.Ordinal)))
            {
                // « 3.14 », « fichier.md » : pas de fin de phrase sans espace.
                probableEnd = false;
            }
        }
    });
}
