using System.Globalization;
using System.Text;

namespace KyfRev.Infrastructure.Vault.Links;

/// <summary>Destination d'un lien dans le texte : <c>text[Start..End]</c> (indices en caractères).</summary>
/// <param name="Start">Début de la destination.</param>
/// <param name="End">Fin de la destination (exclue).</param>
/// <param name="AngleBrackets">Forme <c>[texte](&lt;destination avec espaces.md&gt;)</c>.</param>
public readonly record struct MarkdownLink(int Start, int End, bool AngleBrackets);

/// <summary>
/// Liens Markdown relatifs : repérage, résolution et réécriture, comme dans la version 0.x.
/// Seule la destination d'un lien est réécrite : son texte, son titre et le reste du fichier
/// restent intacts, et les liens situés dans du code ne sont jamais touchés.
/// </summary>
public static class MarkdownLinks
{
    /// <summary>
    /// Destinations des liens et images en ligne (<c>[t](d)</c>, <c>![t](d)</c>) et des
    /// définitions de référence (<c>[ref]: d</c>), hors blocs de code et code en ligne.
    /// </summary>
    public static IReadOnlyList<MarkdownLink> Find(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var links = new List<MarkdownLink>();
        var i = 0;
        var lineStart = true;
        (char Mark, int Length)? fence = null;

        while (i < text.Length)
        {
            if (lineStart)
            {
                var newline = text.IndexOf('\n', i);
                var lineEnd = newline < 0 ? text.Length : newline;
                var line = text[i..lineEnd];
                var indent = line.Length - line.TrimStart(' ').Length;
                var rest = line[indent..];
                var next = Math.Min(lineEnd + 1, text.Length);

                if (fence is { } open)
                {
                    var run = Run(rest, 0, open.Mark);
                    if (indent <= 3 && run >= open.Length && rest[run..].Trim().Length == 0)
                    {
                        fence = null;
                    }
                    i = next;
                    continue;
                }
                if (indent <= 3)
                {
                    if (rest.Length > 0 && rest[0] is '`' or '~')
                    {
                        var mark = rest[0];
                        var run = Run(rest, 0, mark);
                        if (run >= 3 && !(mark == '`' && rest[run..].Contains('`', StringComparison.Ordinal)))
                        {
                            fence = (mark, run);
                            i = next;
                            continue;
                        }
                    }
                    if (rest.StartsWith('[') && ReferenceDefinition(text, i + indent, lineEnd) is { } definition)
                    {
                        links.Add(definition);
                        i = next;
                        continue;
                    }
                }
                lineStart = false;
            }

            switch (text[i])
            {
                case '\n':
                    lineStart = true;
                    i++;
                    break;
                case '\\':
                    i += 2;
                    break;
                case '`':
                    var ticks = Run(text, i, '`');
                    i = InlineCodeEnd(text, i + ticks, ticks) ?? i + ticks;
                    break;
                case ']' when i + 1 < text.Length && text[i + 1] == '(':
                    if (Destination(text, i + 2, text.Length) is { } link)
                    {
                        links.Add(link);
                        i = link.End;
                    }
                    else
                    {
                        i += 2;
                    }
                    break;
                default:
                    i++;
                    break;
            }
        }
        return links;
    }

    /// <summary>
    /// Destination telle que le système de fichiers la comprend : échappements « \x » retirés et
    /// pourcentages décodés (« %20 » devient une espace).
    /// </summary>
    public static string Decode(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var source = Encoding.UTF8.GetBytes(raw);
        var bytes = new List<byte>(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var current = source[i];
            if (current == '\\' && i + 1 < source.Length && IsAsciiPunctuation(source[i + 1]))
            {
                bytes.Add(source[i + 1]);
                i += 2;
            }
            else if (current == '%' && i + 2 < source.Length)
            {
                if (HexDigit(source[i + 1]) is { } high && HexDigit(source[i + 2]) is { } low)
                {
                    bytes.Add((byte)((high << 4) | low));
                    i += 3;
                }
                else
                {
                    bytes.Add(current);
                    i++;
                }
            }
            else
            {
                bytes.Add(current);
                i++;
            }
        }
        return Encoding.UTF8.GetString([.. bytes]);
    }

    /// <summary>
    /// Destination à écrire dans le Markdown pour <paramref name="path"/> (relatif). Entre
    /// chevrons aussi, « # » et « ? » (qui ouvriraient un suffixe) et « % » (qui serait décodé)
    /// sont encodés : « C#.md » ne devient pas « C » suivi de « #.md ».
    /// </summary>
    public static string Encode(string path, bool angleBrackets)
    {
        ArgumentNullException.ThrowIfNull(path);
        var result = new StringBuilder(path.Length);
        foreach (var rune in path.EnumerateRunes())
        {
            var value = rune.Value;
            if (angleBrackets)
            {
                if (value is '#' or '?' or '%')
                {
                    result.Append(CultureInfo.InvariantCulture, $"%{value:X2}");
                }
                else if (value is '<' or '>' or '\\')
                {
                    result.Append('\\').Append((char)value);
                }
                else
                {
                    result.Append(rune.ToString());
                }
            }
            else if (value is ' ' or '%' or '(' or ')' or '<' or '>' or '#' or '?' or '"' or '\\' or '[' or ']' or '`')
            {
                result.Append(CultureInfo.InvariantCulture, $"%{value:X2}");
            }
            else if (Rune.IsControl(rune))
            {
                foreach (var b in Encoding.UTF8.GetBytes(rune.ToString()))
                {
                    result.Append(CultureInfo.InvariantCulture, $"%{b:X2}");
                }
            }
            else
            {
                result.Append(rune.ToString());
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// Chemin (relatif au coffre) désigné par <paramref name="relative"/> depuis le dossier
    /// <paramref name="folder"/> ; <see langword="null"/> si le lien remonte au-dessus de la racine.
    /// </summary>
    public static string? Resolve(string folder, string relative)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(relative);
        var parts = folder.Split('/').Where(part => part.Length > 0).ToList();
        foreach (var part in relative.Split('/'))
        {
            switch (part)
            {
                case "" or ".":
                    break;
                case "..":
                    if (parts.Count == 0)
                    {
                        return null;
                    }
                    parts.RemoveAt(parts.Count - 1);
                    break;
                default:
                    parts.Add(part);
                    break;
            }
        }
        return string.Join('/', parts);
    }

    /// <summary>Chemin relatif qui mène du dossier <paramref name="folder"/> à <paramref name="target"/>.</summary>
    public static string RelativeFrom(string folder, string target)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(target);
        var from = folder.Split('/').Where(part => part.Length > 0).ToList();
        var to = target.Split('/').Where(part => part.Length > 0).ToList();
        var common = from.Zip(to).TakeWhile(pair => pair.First == pair.Second).Count();
        return string.Join('/', Enumerable.Repeat("..", from.Count - common).Concat(to.Skip(common)));
    }

    /// <summary>
    /// Réécrit les liens relatifs d'un texte Markdown.
    /// </summary>
    /// <param name="text">Texte de la fiche.</param>
    /// <param name="folder">Dossier actuel de la fiche (« fiches/Histoire »).</param>
    /// <param name="newFolder">Dossier où elle se trouvera.</param>
    /// <param name="move">Reçoit chaque cible résolue (relative au coffre) et rend sa nouvelle place si elle change.</param>
    /// <returns>Le texte réécrit, ou <see langword="null"/> si aucun lien ne change.</returns>
    public static string? Rewrite(string text, string folder, string newFolder, Func<string, string?> move)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(move);
        var result = new StringBuilder(text.Length);
        var copiedUpTo = 0;
        var changed = false;
        foreach (var link in Find(text))
        {
            var raw = text[link.Start..link.End];
            var cut = raw.IndexOfAny(['#', '?']);
            var (rawPath, suffix) = cut < 0 ? (raw, string.Empty) : (raw[..cut], raw[cut..]);
            if (IsExternal(rawPath))
            {
                continue;
            }
            var decoded = Decode(rawPath);
            if (Resolve(folder, decoded) is not { } target)
            {
                continue;
            }
            var newTarget = move(target) ?? target;
            if (newTarget == target && newFolder == folder)
            {
                continue;
            }
            var updated = RelativeFrom(newFolder, newTarget);
            if (updated == decoded)
            {
                continue;
            }
            result.Append(text, copiedUpTo, link.Start - copiedUpTo)
                .Append(Encode(updated, link.AngleBrackets))
                .Append(suffix);
            copiedUpTo = link.End;
            changed = true;
        }
        return changed ? result.Append(text, copiedUpTo, text.Length - copiedUpTo).ToString() : null;
    }

    /// <summary>
    /// Vrai pour une adresse externe (<c>https:</c>, <c>mailto:</c>), un chemin absolu (<c>/x</c>,
    /// <c>C:/x</c>) ou une simple ancre : ces liens ne sont jamais réécrits.
    /// </summary>
    internal static bool IsExternal(string path) =>
        path.Length == 0 || path.StartsWith('/') || path.StartsWith('\\') || path.Split('/')[0].Contains(':', StringComparison.Ordinal);

    private static int Run(string text, int from, char mark)
    {
        var end = from;
        while (end < text.Length && text[end] == mark)
        {
            end++;
        }
        return end - from;
    }

    /// <summary>Position juste après la série de <paramref name="ticks"/> accents graves qui ferme un code en ligne.</summary>
    private static int? InlineCodeEnd(string text, int from, int ticks)
    {
        var j = from;
        while (j < text.Length)
        {
            if (text[j] == '`')
            {
                var other = Run(text, j, '`');
                if (other == ticks)
                {
                    return j + other;
                }
                j += other;
            }
            else
            {
                j++;
            }
        }
        return null;
    }

    /// <summary>Destination qui commence à <paramref name="from"/> (après « ]( » ou « ]: »).</summary>
    private static MarkdownLink? Destination(string text, int from, int limit)
    {
        var j = from;
        while (j < limit && text[j] is ' ' or '\t')
        {
            j++;
        }
        if (j < limit && text[j] == '<')
        {
            var start = j + 1;
            var k = start;
            while (k < limit)
            {
                switch (text[k])
                {
                    case '\\':
                        k += 2;
                        break;
                    case '>':
                        return new MarkdownLink(start, k, AngleBrackets: true);
                    case '<' or '\n':
                        return null;
                    default:
                        k++;
                        break;
                }
            }
            return null;
        }

        var begin = j;
        var depth = 0;
        while (j < limit)
        {
            var c = text[j];
            if (c == '\\')
            {
                j++;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                if (depth == 0)
                {
                    break;
                }
                depth--;
            }
            else if (c is ' ' or '\t' or '\r' or '\n')
            {
                break;
            }
            j++;
        }
        var end = Math.Min(j, limit);
        return end > begin ? new MarkdownLink(begin, end, AngleBrackets: false) : null;
    }

    /// <summary><c>[étiquette]: destination</c> sur une seule ligne.</summary>
    private static MarkdownLink? ReferenceDefinition(string text, int start, int lineEnd)
    {
        var line = text[start..lineEnd];
        var close = line.IndexOf("]:", StringComparison.Ordinal);
        if (close < 2 || line.AsSpan(1, close - 1).IndexOfAny('[', ']') >= 0)
        {
            return null;
        }
        return Destination(text, start + close + 2, lineEnd);
    }

    private static bool IsAsciiPunctuation(byte b) => b is (>= 0x21 and <= 0x2F) or (>= 0x3A and <= 0x40) or (>= 0x5B and <= 0x60) or (>= 0x7B and <= 0x7E);

    private static int? HexDigit(byte b) => b switch
    {
        >= (byte)'0' and <= (byte)'9' => b - '0',
        >= (byte)'a' and <= (byte)'f' => b - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => b - 'A' + 10,
        _ => null,
    };
}
