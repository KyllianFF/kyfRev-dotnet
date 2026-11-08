using System.Collections.Immutable;
using System.Text;

namespace KyfRev.Infrastructure.Vault.Links;

/// <summary>
/// Extraits des rétroliens et des aperçus, comme dans la version 0.x (<c>retroliens.rs</c>) : le texte d'une fiche sans
/// sa mise en forme, coupé après un mot entier. Les longueurs se comptent en caractères Unicode.
/// </summary>
internal static class SheetExcerpts
{
    /// <summary>Longueur de l'aperçu d'une fiche, en caractères.</summary>
    internal const int PreviewLength = 320;

    /// <summary>Caractères gardés avant et après un lien dans l'extrait d'un rétrolien.</summary>
    private const int Around = 90;

    private const string Ellipsis = "…";

    /// <summary>Sans astérisques ni accents graves, et chaque suite d'espaces réduite à une seule.</summary>
    internal static string Readable(string text)
    {
        var clean = new StringBuilder(text.Length);
        var space = false;
        foreach (var character in text)
        {
            if (character is '*' or '`')
            {
                continue;
            }
            if (char.IsWhiteSpace(character))
            {
                space = true;
                continue;
            }
            if (space)
            {
                clean.Append(' ');
                space = false;
            }
            clean.Append(character);
        }
        return (space ? clean.Append(' ') : clean).ToString();
    }

    /// <summary><c>[texte](destination)</c> devient « texte », une image de même ; le reste ne change pas.</summary>
    internal static string WithoutLinks(string text)
    {
        var clean = new StringBuilder(text.Length);
        var rest = text.AsSpan();
        int open;
        while ((open = rest.IndexOf('[')) >= 0)
        {
            var before = rest[..open];
            clean.Append(before.EndsWith("!", StringComparison.Ordinal) ? before[..^1] : before);
            var after = rest[(open + 1)..];
            var close = after.IndexOf("](", StringComparison.Ordinal);
            var end = close < 0 ? -1 : after[(close + 2)..].IndexOf(')');
            if (end < 0)
            {
                clean.Append('[');
                rest = after;
            }
            else
            {
                clean.Append(after[..close]);
                rest = after[(close + 2 + end + 1)..];
            }
        }
        return clean.Append(rest).ToString();
    }

    /// <summary>Les <paramref name="count"/> premiers caractères, coupés après un mot entier, avec « … » si le texte continue.</summary>
    internal static string Shorten(string text, int count)
    {
        var kept = text.TrimEnd();
        if (IndexOfCharacter(kept, count) is not { } limit)
        {
            return kept;
        }
        var cut = limit;
        while (cut > 0 && !char.IsWhiteSpace(kept[cut - 1]))
        {
            cut--;
        }
        return string.Concat(kept.AsSpan(0, cut > 0 ? cut - 1 : limit).TrimEnd(), Ellipsis);
    }

    /// <summary>
    /// Le texte d'une fiche sans sa mise en forme Markdown : titres, listes, citations, liens, emphase, code. Les blocs de
    /// code, les tableaux et le titre répété en tête sont omis.
    /// </summary>
    internal static string PlainText(string body, string title)
    {
        var lines = new List<string>();
        var inBlock = false;
        foreach (var line in body.Split('\n'))
        {
            var clean = line.Trim();
            if (clean.StartsWith("```", StringComparison.Ordinal) || clean.StartsWith("~~~", StringComparison.Ordinal))
            {
                inBlock = !inBlock;
                continue;
            }
            if (inBlock || clean.Length == 0 || clean.StartsWith('|') || clean.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }
            var text = Readable(WithoutLinks(WithoutNumber(WithoutBullet(clean.TrimStart('#').TrimStart('>').TrimStart())))).Trim();
            if (lines.Count > 0 || text != title)
            {
                lines.Add(text);
            }
        }
        return string.Join(' ', lines);
    }

    /// <summary>La ligne d'un lien, abrégée autour de lui : le texte du lien en est le morceau qui ressort.</summary>
    internal static ImmutableArray<ExcerptPart> AroundLink(string text, MarkdownLink link)
    {
        var lineStart = text.AsSpan(0, link.Start).LastIndexOf('\n') + 1;
        var newline = text.AsSpan(link.End).IndexOf('\n');
        var lineEnd = newline < 0 ? text.Length : link.End + newline;
        var opening = link.AngleBrackets ? "](<" : "](";
        var bracket = text.AsSpan(lineStart, link.Start - lineStart).LastIndexOf('[') + lineStart;
        string before, label, after;
        if (bracket >= lineStart && text.AsSpan(bracket, link.Start - bracket).EndsWith(opening, StringComparison.Ordinal))
        {
            var close = text.AsSpan(link.End, lineEnd - link.End).IndexOf(')');
            before = text[lineStart..bracket];
            label = text[(bracket + 1)..(link.Start - opening.Length)];
            after = text[(close < 0 ? lineEnd : link.End + close + 1)..lineEnd];
        }
        else
        {
            // Une définition de référence, ou une destination précédée d'espaces : pas de texte de lien à reprendre.
            before = text[lineStart..link.Start];
            label = text[link.Start..link.End];
            after = text[link.End..lineEnd];
        }

        var parts = ImmutableArray.CreateBuilder<ExcerptPart>(3);
        var shortBefore = ShortenBefore(Readable(WithoutLinks(before)));
        if (shortBefore.Length > 0)
        {
            parts.Add(new ExcerptPart(shortBefore, IsLink: false));
        }
        parts.Add(new ExcerptPart(Readable(label).Trim(), IsLink: true));
        var shortAfter = Shorten(Readable(WithoutLinks(after)).TrimEnd(), Around);
        if (shortAfter.Length > 0)
        {
            parts.Add(new ExcerptPart(shortAfter, IsLink: false));
        }
        return parts.DrainToImmutable();
    }

    private static string WithoutBullet(string text) =>
        text.StartsWith("- ", StringComparison.Ordinal) || text.StartsWith("* ", StringComparison.Ordinal) || text.StartsWith("+ ", StringComparison.Ordinal)
            ? text[2..]
            : text;

    private static string WithoutNumber(string text)
    {
        var dot = text.IndexOf(". ", StringComparison.Ordinal);
        return dot > 0 && !text.AsSpan(0, dot).ContainsAnyExceptInRange('0', '9') ? text[(dot + 2)..] : text;
    }

    /// <summary>Ce qui précède un lien, réduit à ses derniers caractères.</summary>
    private static string ShortenBefore(string text)
    {
        var index = text.Length;
        for (var remaining = Around; index > 0; remaining--)
        {
            index -= index >= 2 && char.IsSurrogatePair(text[index - 2], text[index - 1]) ? 2 : 1;
            if (remaining == 0)
            {
                return string.Concat(Ellipsis, text.AsSpan(index).TrimStart());
            }
        }
        return text.TrimStart();
    }

    /// <summary>Où commence le caractère de rang <paramref name="rank"/> ; rien si le texte est plus court.</summary>
    private static int? IndexOfCharacter(string text, int rank)
    {
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rank-- == 0)
            {
                return index;
            }
            index += rune.Utf16SequenceLength;
        }
        return null;
    }
}
