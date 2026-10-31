using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using KyfRev.Application.Model;
using KyfRev.Application.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Domain.Randomness;

namespace KyfRev.Modules.Citations;

/// <summary>Une page thématique du Wikiquote francophone (« Philosophie », « Science »…).</summary>
/// <param name="Domain">Domaine choisi dans les réglages, et étiquette des citations trouvées.</param>
/// <param name="Title">Titre de la page.</param>
public sealed record WikiquotePage(string Domain, string Title)
{
    /// <summary>Adresse de la page, lien de la source des citations qui en viennent.</summary>
    public string Address => $"https://fr.wikiquote.org/wiki/{Title.Replace(' ', '_')}";
}

/// <summary>
/// Citation du jour depuis le Wikiquote francophone, comme dans la version 0.x (<c>wikiquote.rs</c>) :
/// quelles pages lire, puis les citations sourcées d'un wikitexte. Aucune requête n'est faite ici.
/// <list type="bullet">
/// <item>Une page thématique donne chaque passage avec <c>{{citation|…}}</c>, suivi de sa référence :
/// <c>{{Réf Livre|…}}</c>, <c>{{Réf Article|…}}</c>…</item>
/// <item>Seules les citations complètes sont retenues : texte, auteur, type de source, titre de
/// l'œuvre et année de parution. Le domaine devient une étiquette, la page la source.</item>
/// <item>Un modèle inconnu dans le texte ou la référence écarte la citation : mieux vaut une
/// citation de moins qu'un texte déformé.</item>
/// </list>
/// </summary>
public static class Wikiquote
{
    /// <summary>Au plus autant de pages lues dans une journée (une seule, d'ordinaire).</summary>
    public const int PagesPerDay = 3;

    private const int MinTextChars = 10;
    private const int MaxTextChars = 1_500;

    private static readonly DateOnly Origin = new(2000, 1, 1);

    private static readonly string[] IgnoredLinkSpaces = ["fichier", "file", "image", "catégorie", "category"];

    private static readonly string[] EmptyTemplates = ["personnage", "pers", "nobr", "nowrap", "petit", "small"];

    /// <summary>Les domaines proposés, et les pages qui les couvrent (Wikiquote n'a pas de page « Informatique »).</summary>
    public static ImmutableArray<(string Name, ImmutableArray<string> Pages)> Domains { get; } =
    [
        ("philosophie", ["Philosophie", "Sagesse", "Connaissance", "Liberté"]),
        ("art", ["Art", "Peinture", "Musique", "Poésie", "Littérature"]),
        ("science", ["Science", "Mathématiques"]),
        ("informatique", ["Ordinateur", "Internet", "Technique"]),
    ];

    /// <summary>Noms de domaine connus, dans l'ordre des domaines, sans doublon.</summary>
    public static IReadOnlyList<string> ValidDomains(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var given = names.ToList();
        return [.. Domains.Where(d => given.Any(n => Ascii.EqualsIgnoreCase(n.Trim(), d.Name))).Select(d => d.Name)];
    }

    /// <summary>
    /// Pages à lire le jour donné, dans l'ordre. Les domaines choisis tournent d'un jour à l'autre ;
    /// dans le domaine du jour, la première page est tirée au hasard, la même toute la journée.
    /// </summary>
    public static IReadOnlyList<WikiquotePage> PagesOfTheDay(string vaultId, DateOnly day, IEnumerable<string> domains)
    {
        ArgumentNullException.ThrowIfNull(domains);
        var wanted = domains.ToList();
        var chosen = Domains.Where(d => wanted.Contains(d.Name, StringComparer.Ordinal)).ToList();
        if (chosen.Count == 0)
        {
            return [];
        }
        var rank = (int)(((long)CivilCalendar.DaysBetween(Origin, day) % chosen.Count + chosen.Count) % chosen.Count);
        var seed = DailySeed.For(vaultId, day);
        var pages = new List<WikiquotePage>();
        for (var shift = 0; shift < chosen.Count; shift++)
        {
            var (name, titles) = chosen[(rank + shift) % chosen.Count];
            var start = (int)(seed % (ulong)titles.Length);
            for (var i = 0; i < titles.Length; i++)
            {
                pages.Add(new WikiquotePage(name, titles[(start + i) % titles.Length]));
            }
        }
        return [.. pages.Take(PagesPerDay)];
    }

    /// <summary>Citations complètes d'une page, dans l'ordre de la page.</summary>
    public static IReadOnlyList<QuoteContent> QuotesOfPage(string wikitext, WikiquotePage page)
    {
        ArgumentNullException.ThrowIfNull(wikitext);
        ArgumentNullException.ThrowIfNull(page);
        var found = new List<QuoteContent>();
        string? sectionAuthor = null;
        var i = 0;
        while (i < wikitext.Length)
        {
            var atLineStart = i == 0 || wikitext[i - 1] == '\n';
            if (atLineStart && string.CompareOrdinal(wikitext, i, "==", 0, 2) == 0)
            {
                var line = FirstLine(wikitext, i);
                var level = line.TakeWhile(c => c == '=').Count();
                var link = FirstLink(line);
                if (link is not null || level <= 2)
                {
                    sectionAuthor = link;
                }
                i += line.Length;
                continue;
            }
            if (string.CompareOrdinal(wikitext, i, "{{", 0, 2) == 0)
            {
                if (TemplateEnd(wikitext, i) is not { } end)
                {
                    break;
                }
                var template = Template.Read(wikitext[(i + 2)..(end - 2)]);
                if (template.Name == "citation")
                {
                    var (reference, after) = ReferenceAfter(wikitext, end);
                    if (reference is not null && Compose(template, reference, sectionAuthor, page) is { } content)
                    {
                        found.Add(content);
                    }
                    i = after;
                }
                else
                {
                    i = end;
                }
                continue;
            }
            i += char.IsHighSurrogate(wikitext[i]) && i + 1 < wikitext.Length ? 2 : 1;
        }
        return found;
    }

    /// <summary>La référence qui suit une citation, en passant les espaces, une accolade en trop et « Choisie citation du jour ».</summary>
    private static (Template? Reference, int After) ReferenceAfter(string wikitext, int i)
    {
        while (true)
        {
            while (i < wikitext.Length && (char.IsWhiteSpace(wikitext[i]) || wikitext[i] == '}'))
            {
                i++;
            }
            if (string.CompareOrdinal(wikitext, i, "{{", 0, 2) != 0 || TemplateEnd(wikitext, i) is not { } end)
            {
                return (null, i);
            }
            var template = Template.Read(wikitext[(i + 2)..(end - 2)]);
            if (template.Name == "choisie citation du jour")
            {
                i = end;
                continue;
            }
            return ReferenceKind(template.Name) is not null ? (template, end) : (null, i);
        }
    }

    /// <summary>Type de source d'un modèle de référence (« réf livre » → livre).</summary>
    private static SourceKind? ReferenceKind(string name)
    {
        var kind = name.StartsWith("réf ", StringComparison.Ordinal) ? name[4..]
            : name.StartsWith("ref ", StringComparison.Ordinal) ? name[4..]
            : null;
        return kind?.Trim() switch
        {
            null => null,
            "livre" or "nouvelle" => SourceKind.Book,
            "article" => SourceKind.Article,
            "discours" => SourceKind.Speech,
            "film" => SourceKind.Film,
            "chanson" => SourceKind.Song,
            "émission" or "emission" => SourceKind.Broadcast,
            _ => SourceKind.Other,
        };
    }

    /// <summary>La citation complète, ou rien s'il manque un élément.</summary>
    private static QuoteContent? Compose(Template quote, Template reference, string? sectionAuthor, WikiquotePage page)
    {
        if ((quote.Value("citation") ?? quote.Positional(0)) is not { } raw || Clean(raw, Reading.Text) is not { } text)
        {
            return null;
        }
        var length = RustText.CharCount(text);
        if (length is < MinTextChars or > MaxTextChars)
        {
            return null;
        }
        // Un champ présent mais illisible écarte la citation.
        (bool Readable, string? Value) Field(string[] keys, Reading reading) =>
            keys.Select(reference.Value).FirstOrDefault(v => v is not null) is { } value
                ? Clean(value, reading) is { } clean ? (true, clean) : (false, null)
                : (true, null);

        var (authorOk, author) = Field(["auteur de la contribution", "auteur"], Reading.Name);
        author ??= sectionAuthor;
        var (workOk, work) = Field(["titre"], Reading.Text);
        var (contributionOk, contribution) = Field(["titre de la contribution"], Reading.Text);
        var (publicationOk, publication) = Field(["publication"], Reading.Text);
        if (!authorOk || author is null || !workOk || !contributionOk || !publicationOk)
        {
            return null;
        }
        var title = (contribution, work, publication) switch
        {
            ({ } c, { } w, _) => $"{c} (dans {w})",
            ({ } c, null, _) => c,
            (null, { } w, { } p) => $"{w} ({p})",
            (null, { } w, null) => w,
            _ => null,
        };
        var (dateOk, date) = Field(["année d'origine", "date", "année de la contribution", "année"], Reading.Text);
        if (title is null || !dateOk || date is null || QuoteDates.YearOf(date) is null)
        {
            return null;
        }
        var (pageOk, workPage) = Field(["page"], Reading.Text);
        if (!pageOk)
        {
            return null;
        }
        var context = quote.Value("précisions") is { } details ? Clean(details, Reading.Text) : null;
        return new QuoteContent(text, author, date, new QuoteSource(ReferenceKind(reference.Name), title, workPage, page.Address), context, [page.Domain]);
    }

    // --- Modèles --------------------------------------------------------------------------------

    /// <summary>Un modèle lu : son nom normalisé, ses paramètres nommés et positionnels.</summary>
    private sealed record Template(string Name, List<(string Name, string Value)> Named, List<string> Positionals)
    {
        /// <summary><paramref name="body"/> : le contenu entre <c>{{</c> et <c>}}</c>.</summary>
        public static Template Read(string body)
        {
            var pieces = SplitAtLevel(body, '|');
            var named = new List<(string, string)>();
            var positionals = new List<string>();
            foreach (var piece in pieces.Skip(1))
            {
                if (FirstAtLevel(piece, '=') is { } equal)
                {
                    named.Add((NormalizeName(piece[..equal]), piece[(equal + 1)..]));
                }
                else
                {
                    positionals.Add(piece);
                }
            }
            return new Template(NormalizeName(pieces[0]), named, positionals);
        }

        public string? Value(string key) =>
            Named.FirstOrDefault(p => p.Name == key && p.Value.Trim().Length > 0).Value;

        public string? Positional(int rank) => rank < Positionals.Count && Positionals[rank].Trim().Length > 0 ? Positionals[rank] : null;
    }

    /// <summary>« Réf_Livre » → « réf livre » ; « année d’origine » → « année d'origine ».</summary>
    private static string NormalizeName(string name)
    {
        var lower = RustText.ToLowercase(name.Trim()).Replace('_', ' ').Replace((char)0xA0, ' ').Replace((char)0x2019, '\'');
        if (lower.StartsWith("modèle:", StringComparison.Ordinal))
        {
            lower = lower["modèle:".Length..];
        }
        return RustText.CollapseWhitespace(lower);
    }

    /// <summary>Fin (exclue) du modèle qui commence à <paramref name="start"/> (<c>{{</c>), modèles imbriqués compris.</summary>
    private static int? TemplateEnd(string text, int start)
    {
        var depth = 0;
        var i = start;
        while (i + 1 < text.Length)
        {
            if (text[i] == '{' && text[i + 1] == '{')
            {
                depth++;
                i += 2;
            }
            else if (text[i] == '}' && text[i + 1] == '}')
            {
                if (depth == 0)
                {
                    return null;
                }
                depth--;
                i += 2;
                if (depth == 0)
                {
                    return i;
                }
            }
            else
            {
                i++;
            }
        }
        return null;
    }

    /// <summary>Découpe <paramref name="text"/> à chaque <paramref name="separator"/> hors des modèles et liens imbriqués.</summary>
    private static List<string> SplitAtLevel(string text, char separator)
    {
        var pieces = new List<string>();
        var start = 0;
        while (FirstAtLevel(text[start..], separator) is { } position)
        {
            pieces.Add(text[start..(start + position)]);
            start += position + 1;
        }
        pieces.Add(text[start..]);
        return pieces;
    }

    /// <summary>Position du premier <paramref name="separator"/> hors des <c>{{…}}</c> et <c>[[…]]</c>.</summary>
    private static int? FirstAtLevel(string text, char separator)
    {
        var templates = 0;
        var links = 0;
        var i = 0;
        while (i < text.Length)
        {
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            switch ((text[i], next))
            {
                case ('{', '{'):
                    templates++;
                    i += 2;
                    continue;
                case ('}', '}') when templates > 0:
                    templates--;
                    i += 2;
                    continue;
                case ('[', '['):
                    links++;
                    i += 2;
                    continue;
                case (']', ']') when links > 0:
                    links--;
                    i += 2;
                    continue;
            }
            if (templates == 0 && links == 0 && text[i] == separator)
            {
                return i;
            }
            i++;
        }
        return null;
    }

    /// <summary>Cible du premier lien <c>[[cible|texte]]</c> d'une ligne (titre de section).</summary>
    private static string? FirstLink(string line)
    {
        var open = line.IndexOf("[[", StringComparison.Ordinal);
        if (open < 0)
        {
            return null;
        }
        var start = open + 2;
        var close = line.IndexOf("]]", start, StringComparison.Ordinal);
        if (close < 0)
        {
            return null;
        }
        var target = line[start..close].Split('|')[0].Trim();
        target = target[(target.LastIndexOf(':') + 1)..].Trim();
        return target.Length > 0 ? target : null;
    }

    /// <summary>
    /// Première ligne à partir de <paramref name="start"/> (<c>lines().next()</c>) : sans son
    /// « \n », ni le « \r » qui le précède.
    /// </summary>
    private static string FirstLine(string text, int start)
    {
        var end = text.IndexOf('\n', start);
        if (end < 0)
        {
            return text[start..];
        }
        var line = text[start..end];
        return line.EndsWith('\r') ? line[..^1] : line;
    }

    // --- Texte ----------------------------------------------------------------------------------

    /// <summary>Pour un nom (auteur), la cible d'un lien est gardée ; pour un texte, ce qui est affiché.</summary>
    private enum Reading
    {
        Text,
        Name,
    }

    /// <summary>Wikitexte → texte simple ; rien si un modèle inconnu s'y trouve, ou s'il ne reste rien.</summary>
    private static string? Clean(string wikitext, Reading reading)
    {
        if (TemplatesAndLinks(Tags(wikitext), reading) is not { } expanded)
        {
            return null;
        }
        var text = Entities(expanded.Replace("'''", string.Empty, StringComparison.Ordinal).Replace("''", string.Empty, StringComparison.Ordinal));
        // Marques invisibles : sens de lecture, césure, espace sans largeur.
        foreach (var invisible in new[] { 0x200B, 0x200E, 0x200F, 0xAD, 0xFEFF })
        {
            text = text.Replace(((char)invisible).ToString(), string.Empty, StringComparison.Ordinal);
        }
        var lines = new List<string>();
        foreach (var line in RustLines(text))
        {
            var joined = string.Join(' ', line.Split(' ', '\t').Where(word => word.Length > 0));
            // Pas plus d'une ligne vide de suite.
            if (joined.Length == 0 && lines.Count > 0 && lines[^1].Length == 0)
            {
                continue;
            }
            lines.Add(joined);
        }
        var result = string.Join('\n', lines.SkipWhile(l => l.Length == 0).Reverse().SkipWhile(l => l.Length == 0).Reverse());
        return result.Length > 0 ? result : null;
    }

    /// <summary>Retire commentaires, notes <c>&lt;ref&gt;</c> et balises HTML ; <c>&lt;br&gt;</c> devient un retour à la ligne.</summary>
    private static string Tags(string text)
    {
        var output = new StringBuilder(text.Length);
        var rest = text;
        while (rest.IndexOf('<', StringComparison.Ordinal) is var position and >= 0)
        {
            output.Append(rest, 0, position);
            rest = rest[position..];
            // Minuscules ASCII seulement : les positions restent celles de « rest ».
            var lower = AsciiLower(rest);
            if (lower.StartsWith("<!--", StringComparison.Ordinal))
            {
                var close = rest.IndexOf("-->", StringComparison.Ordinal);
                rest = close < 0 ? string.Empty : rest[(close + 3)..];
                continue;
            }
            var tagEnd = rest.IndexOf('>', StringComparison.Ordinal);
            if (tagEnd < 0)
            {
                return output.Append(rest).ToString();
            }
            var tag = lower[..(tagEnd + 1)];
            if (!(tag.Length > 1 && (char.IsAsciiLetter(tag[1]) || tag[1] == '/')))
            {
                output.Append('<');
                rest = rest[1..];
                continue;
            }
            if (tag.StartsWith("<ref", StringComparison.Ordinal) && !tag.EndsWith("/>", StringComparison.Ordinal))
            {
                // Note de bas de page : retirée avec son contenu.
                var close = lower.IndexOf("</ref>", StringComparison.Ordinal);
                rest = close < 0 ? string.Empty : rest[(close + "</ref>".Length)..];
                continue;
            }
            if (tag.StartsWith("<br", StringComparison.Ordinal))
            {
                output.Append('\n');
            }
            rest = rest[(tagEnd + 1)..];
        }
        return output.Append(rest).ToString();
    }

    /// <summary>Développe les modèles connus et les liens ; rien si un modèle est inconnu.</summary>
    private static string? TemplatesAndLinks(string text, Reading reading)
    {
        var output = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            if (string.CompareOrdinal(text, i, "{{", 0, 2) == 0)
            {
                if (TemplateEnd(text, i) is not { } end || Expand(Template.Read(text[(i + 2)..(end - 2)]), reading) is not { } expanded)
                {
                    return null;
                }
                output.Append(expanded);
                i = end;
            }
            else if (string.CompareOrdinal(text, i, "[[", 0, 2) == 0)
            {
                var close = text.IndexOf("]]", i + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    return null;
                }
                var link = text[(i + 2)..close];
                var bar = link.IndexOf('|', StringComparison.Ordinal);
                var (target, shown) = bar < 0 ? (link.Trim(), null) : (link[..bar].Trim(), link[(bar + 1)..].Trim());
                var space = RustText.ToLowercase(target.Split(':')[0]);
                if (!IgnoredLinkSpaces.Contains(space, StringComparer.Ordinal))
                {
                    // « w:fr:Page#Section » → « Page ».
                    target = target[(target.LastIndexOf(':') + 1)..];
                    target = target.Split('#')[0].Trim();
                    output.Append((reading, shown) switch
                    {
                        // « Jean Racine (dramaturge) » → « Jean Racine ».
                        (Reading.Name, _) => target.IndexOf(" (", StringComparison.Ordinal) is var open and >= 0 && target[(open + 2)..].EndsWith(')')
                            ? target[..open].Trim()
                            : target,
                        (Reading.Text, { } s) => s,
                        _ => target,
                    });
                }
                i = close + 2;
            }
            else if (string.CompareOrdinal(text, i, "[http", 0, 5) == 0)
            {
                // Lien externe : seul son texte reste.
                var close = text.IndexOf(']', i);
                if (close < 0)
                {
                    return null;
                }
                var link = text[(i + 1)..close];
                var space = link.IndexOf(' ', StringComparison.Ordinal);
                if (space >= 0)
                {
                    output.Append(link[(space + 1)..].Trim());
                }
                i = close + 1;
            }
            else
            {
                var width = char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? 2 : 1;
                output.Append(text, i, width);
                i += width;
            }
        }
        return output.ToString();
    }

    /// <summary>Texte d'un modèle courant dans les citations ; rien s'il est inconnu.</summary>
    private static string? Expand(Template template, Reading reading)
    {
        var first = template.Positional(0)?.Trim();
        var expanded = template.Name switch
        {
            _ when EmptyTemplates.Contains(template.Name, StringComparer.Ordinal) => first,
            "lang" or "langue" => template.Positionals.Count > 0 ? template.Positionals[^1].Trim() : null,
            "w" or "lien" => (template.Positional(1) ?? template.Positional(0))?.Trim(),
            "siècle" or "s" => first is null ? null : $"{first}e siècle",
            "s-" => first is null ? null : $"{first}e",
            "e" or "ème" or "eme" => first is null ? "e" : $"{first}e",
            "er" or "re" => template.Name,
            "tab" => " ",
            _ => null,
        };
        return expanded is null ? null : TemplatesAndLinks(expanded, reading);
    }

    /// <summary>Entités HTML courantes : <c>&amp;nbsp;</c>, <c>&amp;amp;</c>, <c>&amp;#160;</c>, <c>&amp;#x2019;</c>…</summary>
    private static string Entities(string text)
    {
        var output = new StringBuilder(text.Length);
        var rest = text;
        while (rest.IndexOf('&', StringComparison.Ordinal) is var position and >= 0)
        {
            output.Append(rest, 0, position);
            rest = rest[position..];
            // Le « ; » est cherché dans les 12 premiers octets UTF-8.
            var semicolon = rest[..WithinUtf8Bytes(rest, 12)].IndexOf(';', StringComparison.Ordinal);
            if (semicolon >= 0 && Entity(rest[1..semicolon]) is { } replacement)
            {
                output.Append(replacement);
                rest = rest[(semicolon + 1)..];
            }
            else
            {
                output.Append('&');
                rest = rest[1..];
            }
        }
        return output.Append(rest).ToString();
    }

    private static string? Entity(string name) => name switch
    {
        "nbsp" => ((char)0xA0).ToString(),
        "thinsp" or "nnbsp" => ((char)0x202F).ToString(),
        "amp" => "&",
        "lt" => "<",
        "gt" => ">",
        "quot" => "\"",
        "apos" => "'",
        "laquo" => "«",
        "raquo" => "»",
        "hellip" => "…",
        "mdash" => "—",
        "ndash" => "–",
        _ when name.StartsWith("#x", StringComparison.Ordinal) || name.StartsWith("#X", StringComparison.Ordinal) => CodePoint(name[2..], 16),
        _ when name.StartsWith('#') => CodePoint(name[1..], 10),
        _ => null,
    };

    /// <summary>Caractère d'un code écrit en base <paramref name="radix"/> (<c>u32::from_str_radix</c>, puis <c>char::from_u32</c>).</summary>
    private static string? CodePoint(string digits, int radix)
    {
        var body = digits.StartsWith('+') ? digits[1..] : digits;
        if (body.Length == 0 || !body.All(c => radix == 16 ? char.IsAsciiHexDigit(c) : char.IsAsciiDigit(c)))
        {
            return null;
        }
        var style = radix == 16 ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
        return uint.TryParse(body, style, CultureInfo.InvariantCulture, out var value) && value <= 0x10FFFF && value is < 0xD800 or > 0xDFFF
            ? char.ConvertFromUtf32((int)value)
            : null;
    }

    /// <summary>Nombre de caractères UTF-16 au début de <paramref name="text"/> qui tiennent dans <paramref name="bytes"/> octets UTF-8.</summary>
    private static int WithinUtf8Bytes(string text, int bytes)
    {
        var used = 0;
        var i = 0;
        while (i < text.Length)
        {
            var rune = Rune.GetRuneAt(text, i);
            if (used + rune.Utf8SequenceLength > bytes)
            {
                break;
            }
            used += rune.Utf8SequenceLength;
            i += rune.Utf16SequenceLength;
        }
        return i;
    }

    private static string AsciiLower(string text) =>
        string.Create(text.Length, text, (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.IsAsciiLetterUpper(source[i]) ? (char)(source[i] | 0x20) : source[i];
            }
        });

    /// <summary>Lignes comme <c>str::lines</c> : séparées par « \n », le « \r » qui le précède retiré, pas de ligne vide finale.</summary>
    private static List<string> RustLines(string text)
    {
        var parts = text.Split('\n');
        var lines = new List<string>(parts.Length);
        for (var k = 0; k < parts.Length; k++)
        {
            var ended = k < parts.Length - 1;
            if (!ended && parts[k].Length == 0)
            {
                break;
            }
            lines.Add(ended && parts[k].EndsWith('\r') ? parts[k][..^1] : parts[k]);
        }
        return lines;
    }
}
