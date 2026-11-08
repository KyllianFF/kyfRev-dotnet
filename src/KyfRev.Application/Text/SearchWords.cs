using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using KyfRev.Application.Model;

namespace KyfRev.Application.Text;

/// <summary>
/// Les mots d'une recherche, comme dans la 0.x (<c>recherche.rs</c>) : un élément est retenu si chacun d'eux se trouve dans
/// son titre ou dans son texte. La casse, les accents, les ligatures « œ » et « æ » et la forme de l'apostrophe ne
/// comptent pas : « coeur » trouve « Cœur », « l'etat » trouve « l’État ».
/// </summary>
public sealed class SearchWords
{
    /// <summary>Caractères gardés avant le premier mot trouvé, dans l'extrait.</summary>
    private const int CharactersBefore = 40;

    /// <summary>Longueur de l'extrait, en caractères.</summary>
    private const int ExcerptLength = 140;

    private readonly string[] _words;

    /// <summary>Les mots de <paramref name="query"/>, sous leur forme comparable.</summary>
    public SearchWords(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        _words = FoldedText.Of(query).Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>La recherche ne contient aucun mot.</summary>
    public bool IsEmpty => _words.Length == 0;

    /// <summary>
    /// Cherche les mots dans <paramref name="title"/> et <paramref name="body"/>.
    /// </summary>
    /// <returns>
    /// Rien si un mot ne se trouve ni dans l'un ni dans l'autre ; sinon l'extrait de <paramref name="body"/> autour des
    /// mots absents du titre, vide si le titre les contient tous.
    /// </returns>
    public ImmutableArray<ExcerptPiece>? Match(string title, string body)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(body);
        var inTitle = FoldedText.Of(title).Text;
        var folded = FoldedText.Of(body);
        if (!_words.All(word => inTitle.Contains(word, StringComparison.Ordinal) || folded.Text.Contains(word, StringComparison.Ordinal)))
        {
            return null;
        }
        return Excerpt(body, folded, _words.Where(word => !inTitle.Contains(word, StringComparison.Ordinal)));
    }

    /// <summary>Le texte d'un extrait, sur une ligne : blancs réduits à une espace, marques d'emphase et de code Markdown (« * », « ` ») retirées.</summary>
    internal static string Readable(string text)
    {
        var clean = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.Where(c => c is not ('*' or '`')))
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }
            if (space)
            {
                clean.Append(' ');
                space = false;
            }
            clean.Append(c);
        }
        if (space)
        {
            clean.Append(' ');
        }
        return clean.ToString();
    }

    /// <summary>L'extrait de <paramref name="body"/> autour de la première occurrence d'un mot de <paramref name="toShow"/>, où tous les mots sont repérés.</summary>
    private ImmutableArray<ExcerptPiece> Excerpt(string body, FoldedText folded, IEnumerable<string> toShow)
    {
        var first = toShow.Select(word => folded.Text.IndexOf(word, StringComparison.Ordinal)).DefaultIfEmpty(-1).Min();
        if (first < 0)
        {
            return [];
        }
        var wordStart = folded.Origins[first];
        var start = WindowStart(body, wordStart);
        var end = WindowEnd(body, start, wordStart);

        var pieces = new List<ExcerptPiece>();
        void Add(string text, bool found)
        {
            if (Readable(text) is { Length: > 0 } clean)
            {
                pieces.Add(new ExcerptPiece(clean, found));
            }
        }

        var position = start;
        if (start > 0 && body[start - 1] != '\n')
        {
            Add("… ", found: false);
        }
        foreach (var (from, to) in Marks(body, folded, start, end))
        {
            Add(body[position..from], found: false);
            Add(body[from..to], found: true);
            position = to;
        }
        Add(body[position..end], found: false);
        if (end < body.Length)
        {
            Add("…", found: false);
        }
        return Trimmed(pieces);
    }

    /// <summary>Les mots trouvés dans la fenêtre, fusionnés quand ils se chevauchent.</summary>
    private List<(int From, int To)> Marks(string body, FoldedText folded, int start, int end)
    {
        var marks = new List<(int From, int To)>();
        foreach (var word in _words)
        {
            for (var index = folded.Text.IndexOf(word, StringComparison.Ordinal); index >= 0; index = folded.Text.IndexOf(word, index + word.Length, StringComparison.Ordinal))
            {
                var mark = folded.InOriginal(body, index, index + word.Length);
                if (mark.From >= start && mark.To <= end)
                {
                    marks.Add(mark);
                }
            }
        }
        marks.Sort();
        var merged = new List<(int From, int To)>();
        foreach (var (from, to) in marks)
        {
            if (merged.Count > 0 && from <= merged[^1].To)
            {
                merged[^1] = (merged[^1].From, Math.Max(merged[^1].To, to));
            }
            else
            {
                merged.Add((from, to));
            }
        }
        return merged;
    }

    /// <summary>Le début de la fenêtre : un peu avant le mot, sans remonter au-delà du début de sa ligne ni couper de mot.</summary>
    private static int WindowStart(string body, int wordStart)
    {
        var position = wordStart;
        for (var taken = 0; taken < CharactersBefore && position > 0; taken++)
        {
            position -= position >= 2 && char.IsSurrogatePair(body[position - 2], body[position - 1]) ? 2 : 1;
            if (body[position] == '\n')
            {
                return position + 1;
            }
        }
        if (position == 0 || position == wordStart)
        {
            return position;
        }
        // Le début du premier mot entier : après la première espace, s'il y en a une avant le mot cherché.
        for (var index = position; index < wordStart; index++)
        {
            if (char.IsWhiteSpace(body[index]))
            {
                return index + 1;
            }
        }
        return position;
    }

    /// <summary>La fin de la fenêtre : au plus la longueur de l'extrait depuis son début, sans couper de mot.</summary>
    private static int WindowEnd(string body, int start, int wordStart)
    {
        var limit = start;
        for (var taken = 0; taken < ExcerptLength && limit < body.Length; taken++)
        {
            limit += limit + 1 < body.Length && char.IsSurrogatePair(body[limit], body[limit + 1]) ? 2 : 1;
        }
        if (limit >= body.Length)
        {
            return body.Length;
        }
        for (var index = limit - 1; index >= wordStart; index--)
        {
            if (char.IsWhiteSpace(body[index]))
            {
                return index;
            }
        }
        return limit;
    }

    /// <summary>Pas d'espace en tête ni en fin d'extrait.</summary>
    private static ImmutableArray<ExcerptPiece> Trimmed(List<ExcerptPiece> pieces)
    {
        if (pieces.Count > 0)
        {
            pieces[0] = pieces[0] with { Text = pieces[0].Text.TrimStart() };
            pieces[^1] = pieces[^1] with { Text = pieces[^1].Text.TrimEnd() };
        }
        return [.. pieces.Where(piece => piece.Text.Length > 0)];
    }
}

/// <summary>Un texte ramené à une forme comparable, avec pour chacune de ses unités la position du caractère d'origine : de quoi retrouver l'extrait.</summary>
internal sealed class FoldedText
{
    private FoldedText(string text, int[] origins)
    {
        Text = text;
        Origins = origins;
    }

    /// <summary>Le texte en minuscules, sans accents, sans ligatures, l'apostrophe droite.</summary>
    public string Text { get; }

    /// <summary>Pour chaque unité de <see cref="Text"/>, la position dans le texte d'origine du caractère dont elle vient.</summary>
    public int[] Origins { get; }

    /// <summary>La forme comparable de <paramref name="text"/>.</summary>
    public static FoldedText Of(string text)
    {
        var folded = new StringBuilder(text.Length);
        var origins = new List<int>(text.Length);
        var position = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var before = folded.Length;
            Append(folded, rune);
            for (var unit = before; unit < folded.Length; unit++)
            {
                origins.Add(position);
            }
            position += rune.Utf16SequenceLength;
        }
        return new FoldedText(folded.ToString(), [.. origins]);
    }

    /// <summary>Ce que couvre <paramref name="start"/>..<paramref name="end"/> du texte comparable, dans le texte d'origine.</summary>
    public (int From, int To) InOriginal(string original, int start, int end)
    {
        var last = Origins[end - 1];
        var length = last + 1 < original.Length && char.IsSurrogatePair(original[last], original[last + 1]) ? 2 : 1;
        return (Origins[start], last + length);
    }

    private static void Append(StringBuilder folded, Rune rune)
    {
        switch (rune.Value)
        {
            case < 0x80:
                folded.Append(char.ToLowerInvariant((char)rune.Value));
                break;
            case 'œ' or 'Œ':
                folded.Append("oe");
                break;
            case 'æ' or 'Æ':
                folded.Append("ae");
                break;
            case '’' or '‘' or 'ʼ':
                folded.Append('\'');
                break;
            case 0xFFFE or 0xFFFF:
                // Des non-caractères, permis dans un fichier : ils n'ont pas de décomposition, et la normalisation les refuse.
                folded.Append((char)rune.Value);
                break;
            default:
                foreach (var part in rune.ToString().Normalize(NormalizationForm.FormD).EnumerateRunes())
                {
                    if (Rune.GetUnicodeCategory(part) is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark))
                    {
                        folded.Append(Rune.ToLowerInvariant(part).ToString());
                    }
                }
                break;
        }
    }
}
