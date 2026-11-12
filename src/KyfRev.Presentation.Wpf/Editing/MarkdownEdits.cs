using System.Globalization;
using System.Text.RegularExpressions;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Une mise en forme du texte, marquée de part et d'autre de la sélection.</summary>
public enum InlineMark
{
    /// <summary>Gras.</summary>
    Bold,

    /// <summary>Italique.</summary>
    Italic,

    /// <summary>Souligné : Markdown n'a pas d'écriture pour lui, comme la 0.x on garde la balise.</summary>
    Underline,

    /// <summary>Barré.</summary>
    Strike,

    /// <summary>Exposant.</summary>
    Superscript,

    /// <summary>Indice.</summary>
    Subscript,

    /// <summary>Code dans le texte.</summary>
    Code,

    /// <summary>Formule dans le texte.</summary>
    Formula,
}

/// <summary>Un genre de liste.</summary>
public enum ListKind
{
    /// <summary>Liste à puces.</summary>
    Bullets,

    /// <summary>Liste numérotée.</summary>
    Numbers,

    /// <summary>Liste de tâches, à faire ou faites.</summary>
    Tasks,
}

/// <summary>Un alignement du texte.</summary>
public enum TextAlign
{
    /// <summary>À gauche : le texte ordinaire.</summary>
    Left,

    /// <summary>Au centre.</summary>
    Center,

    /// <summary>À droite.</summary>
    Right,

    /// <summary>Justifié.</summary>
    Justify,
}

/// <summary>Un remplacement dans le texte, et la sélection qui le suit.</summary>
/// <param name="Start">Où il commence.</param>
/// <param name="Length">Ce qu'il remplace.</param>
/// <param name="Text">Ce qu'il écrit.</param>
/// <param name="SelectionStart">Le début de la sélection, après.</param>
/// <param name="SelectionLength">La longueur de la sélection, après.</param>
internal readonly record struct TextChange(int Start, int Length, string Text, int SelectionStart, int SelectionLength);

/// <summary>
/// Les gestes du ruban sur le Markdown : ils rendent un remplacement, sans rien reformater d'autre. Les lignes
/// finissent par « \n » : l'éditeur ne présente jamais d'autre fin de ligne.
/// </summary>
internal static partial class MarkdownEdits
{
    private const string SpanClosing = "</span>";

    private const string AlignmentStart = "<div align=";

    /// <summary>Marque la sélection, ou retire la marque qui l'entoure déjà.</summary>
    public static TextChange Toggle(string text, int start, int end, InlineMark mark)
    {
        (start, end) = WithoutEdgeSpaces(text, start, end);
        if (mark is InlineMark.Bold or InlineMark.Italic)
        {
            return ToggleStars(text, start, end, mark == InlineMark.Bold ? 2 : 1);
        }
        var (open, close) = mark switch
        {
            InlineMark.Underline => ("<u>", "</u>"),
            InlineMark.Strike => ("~~", "~~"),
            InlineMark.Superscript => ("<sup>", "</sup>"),
            InlineMark.Subscript => ("<sub>", "</sub>"),
            InlineMark.Code => ("`", "`"),
            _ => ("$", "$"),
        };
        var selected = text[start..end];
        return IsAt(text, start - open.Length, open) && IsAt(text, end, close)
            ? new TextChange(start - open.Length, open.Length + selected.Length + close.Length, selected, start - open.Length, selected.Length)
            : new TextChange(start, selected.Length, open + selected + close, start + open.Length, selected.Length);
    }

    /// <summary>Donne le niveau de titre aux lignes de la sélection ; 0 en refait du texte.</summary>
    public static TextChange Heading(string text, int start, int end, int level)
    {
        var prefix = level > 0 ? new string('#', level) + " " : string.Empty;
        return RewriteLines(text, start, end, line => HeadingPrefix().Replace(line, string.Empty, 1).Insert(0, prefix));
    }

    /// <summary>Le niveau de titre de la ligne ; 0 pour du texte.</summary>
    public static int HeadingLevel(string text, int position)
    {
        var (lineStart, lineEnd) = LineAround(text, position);
        var match = HeadingPrefix().Match(text, lineStart, lineEnd - lineStart);
        return match.Success ? match.Value.Count(c => c == '#') : 0;
    }

    /// <summary>La ligne fait partie d'une citation.</summary>
    public static bool IsQuoted(string line) => QuotePrefix().IsMatch(line);

    /// <summary>Met les lignes de la sélection en citation, d'un seul bloc, ou les en sort si elles y sont déjà.</summary>
    public static TextChange Quote(string text, int start, int end)
    {
        var (from, to) = LinesOf(text, start, end);
        var written = text[from..to].Split('\n').Where(line => line.Trim().Length > 0).ToList();
        var removing = written.Count > 0 && written.All(IsQuoted);
        return RewriteLines(text, start, end, line =>
        {
            if (removing)
            {
                return QuotePrefix().Replace(line, string.Empty, 1);
            }
            return line.Trim().Length == 0 ? ">" : "> " + line;
        });
    }

    /// <summary>Met les lignes de la sélection en liste, ou les en sort si elles y sont déjà.</summary>
    public static TextChange List(string text, int start, int end, ListKind kind)
    {
        var (from, to) = LinesOf(text, start, end);
        var lines = text[from..to].Split('\n');
        var marker = kind switch
        {
            ListKind.Bullets => BulletMarker(),
            ListKind.Tasks => TaskMarker(),
            _ => NumberMarker(),
        };
        var removing = lines.Where(line => line.Trim().Length > 0).All(line => marker.IsMatch(line));
        var rank = 0;
        return RewriteLines(text, start, end, line =>
        {
            if (line.Trim().Length == 0)
            {
                return line;
            }
            var match = AnyMarker().Match(line);
            var indent = match.Groups[1].Value;
            var content = line[match.Length..];
            rank++;
            return (kind, removing) switch
            {
                // Une tâche redevient un point ordinaire : elle reste dans sa liste.
                (ListKind.Tasks, true) => $"{indent}- {content}",
                (_, true) => indent + content,
                (ListKind.Bullets, _) => $"{indent}- {content}",
                (ListKind.Tasks, _) => $"{indent}- [ ] {content}",
                _ => string.Create(CultureInfo.InvariantCulture, $"{indent}{rank}. {content}"),
            };
        });
    }

    /// <summary>
    /// Pose une couleur ou une taille sur la sélection : elle remplace celle qui l'entoure déjà, et, sans valeur, la
    /// retire ; rien à faire si elle n'y est pas.
    /// </summary>
    /// <param name="text">Le texte.</param>
    /// <param name="start">Le début de la sélection.</param>
    /// <param name="end">La fin de la sélection.</param>
    /// <param name="property">La propriété : <c>color</c> ou <c>font-size</c>.</param>
    /// <param name="value">Sa valeur, telle qu'elle s'écrit.</param>
    public static TextChange? Style(string text, int start, int end, string property, string? value)
    {
        (start, end) = WithoutEdgeSpaces(text, start, end);
        var prefix = $"<span style=\"{property}:";
        var selected = text[start..end];
        if (OpeningBefore(text, start, prefix) is { } opening && IsAt(text, end, SpanClosing))
        {
            if (value is null)
            {
                return new TextChange(opening, end + SpanClosing.Length - opening, selected, opening, selected.Length);
            }
            var replaced = $"{prefix}{value}\">";
            return new TextChange(opening, start - opening, replaced, opening + replaced.Length, selected.Length);
        }
        if (value is null)
        {
            return null;
        }
        var open = $"{prefix}{value}\">";
        return new TextChange(start, selected.Length, open + selected + SpanClosing, start + open.Length, selected.Length);
    }

    /// <summary>La couleur ou la taille qui entoure exactement la sélection.</summary>
    public static string? StyleAround(string text, int start, int end, string property)
    {
        (start, end) = WithoutEdgeSpaces(text, start, end);
        var prefix = $"<span style=\"{property}:";
        return OpeningBefore(text, start, prefix) is { } opening && IsAt(text, end, SpanClosing)
            ? text[(opening + prefix.Length)..(start - 2)]
            : null;
    }

    /// <summary>
    /// Aligne les lignes de la sélection : un passage aligné les entoure déjà, il change de valeur, et « à gauche » le
    /// retire ; sinon, il se crée autour d'elles, isolé par des lignes vides.
    /// </summary>
    public static TextChange? Align(string text, int start, int end, TextAlign align)
    {
        var (from, to) = LinesOf(text, start, end);
        var value = align switch
        {
            TextAlign.Center => "center",
            TextAlign.Right => "right",
            TextAlign.Justify => "justify",
            _ => null,
        };
        var opening = $"<div align=\"{value}\">";
        if (AlignedPassageAround(text, from, to) is { } passage)
        {
            if (value is not null)
            {
                var delta = opening.Length - (passage.OpeningEnd - passage.Start);
                return new TextChange(passage.Start, passage.OpeningEnd - passage.Start, opening, start + delta, end - start);
            }
            var content = text[passage.OpeningEnd..passage.ClosingStart].Trim('\n');
            var removed = text.IndexOf(content, passage.OpeningEnd, StringComparison.Ordinal) - passage.Start;
            return new TextChange(passage.Start, passage.End - passage.Start, content, Math.Max(passage.Start, start - removed), end - start);
        }
        if (value is null)
        {
            return null;
        }
        var before = from == 0 || (from >= 2 && text[from - 2] == '\n') ? string.Empty : "\n";
        var after = to == text.Length || text.AsSpan(to).StartsWith("\n\n", StringComparison.Ordinal) ? string.Empty : "\n";
        var head = $"{before}{opening}\n\n";
        return new TextChange(from, to - from, $"{head}{text[from..to]}\n\n</div>{after}", start + head.Length, end - start);
    }

    /// <summary>L'alignement à cet endroit.</summary>
    public static TextAlign AlignAt(string text, int position)
    {
        if (!text.Contains(AlignmentStart, StringComparison.Ordinal))
        {
            return TextAlign.Left;
        }
        var (from, to) = LineAround(text, position);
        if (AlignedPassageAround(text, from, to) is not { } passage)
        {
            return TextAlign.Left;
        }
        return AlignmentOpening().Match(text[passage.Start..passage.OpeningEnd]).Groups[1].Value switch
        {
            "center" => TextAlign.Center,
            "right" => TextAlign.Right,
            _ => TextAlign.Justify,
        };
    }

    /// <summary>Insère un bloc sur ses propres lignes ; le curseur reprend après lui.</summary>
    public static TextChange Block(string text, int position, string block)
    {
        var (lineStart, lineEnd) = LineAround(text, position);
        var empty = text.AsSpan(lineStart, lineEnd - lineStart).IsWhiteSpace();
        var before = empty || position == lineStart ? string.Empty : "\n\n";
        var after = empty || position == lineEnd ? "\n" : "\n\n";
        var from = empty ? lineStart : position;
        var to = empty ? lineEnd : position;
        var written = before + block + after;
        return new TextChange(from, to - from, written, from + written.Length, 0);
    }

    /// <summary>Un tableau vide : une ligne d'en-tête, puis les autres.</summary>
    public static string Table(int rows, int columns)
    {
        string Row(Func<int, string> cell) => "| " + string.Join(" | ", Enumerable.Range(0, columns).Select(cell)) + " |";
        return string.Join('\n', new[]
        {
            Row(i => string.Create(CultureInfo.InvariantCulture, $"Colonne {i + 1}")),
            Row(_ => "---"),
        }.Concat(Enumerable.Repeat(Row(_ => " "), Math.Max(1, rows - 1))));
    }

    /// <summary>
    /// Une ligne horizontale, isolée par des lignes vides : collée sous un paragraphe, elle en ferait un titre.
    /// Le curseur reprend au début du texte qui suit.
    /// </summary>
    public static TextChange Rule(string text, int position) => Line(text, position, "---");

    /// <summary>Insère un saut de page, ou une page blanche, seul sur sa ligne ; le curseur passe après lui.</summary>
    public static TextChange PageBreak(string text, int position, bool blank) =>
        Line(text, position, blank ? KyfRev.Rendering.PageMarks.BlankPageLine : KyfRev.Rendering.PageMarks.BreakLine);

    private static TextChange Line(string text, int position, string line)
    {
        var endsBefore = position == 0 ? 2 : position >= 2 && text[position - 1] == '\n' && text[position - 2] == '\n' ? 2 : text[position - 1] == '\n' ? 1 : 0;
        var endsAfter = position == text.Length ? 1 : text.AsSpan(position).StartsWith("\n\n", StringComparison.Ordinal) ? 2 : text[position] == '\n' ? 1 : 0;
        var written = new string('\n', 2 - endsBefore) + line + new string('\n', 2 - endsAfter);
        var caret = written.Length + (position == text.Length ? 0 : endsAfter);
        return new TextChange(position, 0, written, position + caret, 0);
    }

    /// <summary>
    /// Change le langage du bloc de code où se trouve le curseur ; sinon, crée un bloc, autour de la sélection
    /// s'il y en a une, et le curseur s'y place.
    /// </summary>
    public static TextChange Code(string text, int start, int end, string language)
    {
        if (MarkdownLines.FenceAt(text, start) is { } fence)
        {
            var opening = text[fence.Start..fence.OpeningEnd];
            var written = OpeningFence().Replace(opening, match => match.Groups[1].Value + language, 1);
            return new TextChange(fence.Start, opening.Length, written, Math.Max(fence.Start, start + written.Length - opening.Length), end - start);
        }
        var head = "```" + language + "\n";
        if (end > start)
        {
            var selected = text[start..end];
            var change = Block(text.Remove(start, selected.Length), start, head + selected + "\n```");
            return new TextChange(change.Start, change.Length + selected.Length, change.Text, change.SelectionStart, 0);
        }
        var block = Block(text, start, head + "\n```");
        return block with { SelectionStart = block.Start + block.Text.IndexOf(head, StringComparison.Ordinal) + head.Length };
    }

    /// <summary>Remplace la sélection par un lien.</summary>
    public static TextChange Link(int start, int end, string label, string address)
    {
        var written = SheetLinks.MarkdownLink(label, address);
        return new TextChange(start, end - start, written, start + written.Length, 0);
    }

    /// <summary>Remplace une étendue du texte par le même texte dans une autre casse ; la sélection reste.</summary>
    public static TextChange? Case(string text, int start, int end, CaseStyle? style)
    {
        if (start == end)
        {
            var (lineStart, lineEnd) = LineAround(text, start);
            if (TextCase.WordAround(text[lineStart..lineEnd], start - lineStart) is not { } word)
            {
                return null;
            }
            var (from, to) = (lineStart + word.Start, lineStart + word.End);
            var written = TextCase.Apply(text[from..to], style ?? TextCase.Next(text[from..to]));
            return new TextChange(from, to - from, written, start, 0);
        }
        var selected = text[start..end];
        return new TextChange(start, selected.Length, TextCase.Apply(selected, style ?? TextCase.Next(selected)), start, selected.Length);
    }

    /// <summary>La ligne qui contient <paramref name="position"/>, sans sa fin de ligne.</summary>
    public static (int Start, int End) LineAround(string text, int position)
    {
        var start = position == 0 ? 0 : text.LastIndexOf('\n', position - 1) + 1;
        var end = text.IndexOf('\n', position);
        return (start, end < 0 ? text.Length : end);
    }

    /// <summary>« **gras** » et « *italique* » partagent leur signe : on compte ceux qui entourent la sélection.</summary>
    private static TextChange ToggleStars(string text, int start, int end, int stars)
    {
        var before = 0;
        while (start - before > 0 && text[start - before - 1] == '*')
        {
            before++;
        }
        var after = 0;
        while (end + after < text.Length && text[end + after] == '*')
        {
            after++;
        }
        var around = Math.Min(before, after);
        var present = stars == 2 ? around >= 2 : around % 2 == 1;
        var selected = text[start..end];
        if (present)
        {
            return new TextChange(start - stars, stars + selected.Length + stars, selected, start - stars, selected.Length);
        }
        var mark = new string('*', stars);
        return new TextChange(start, selected.Length, mark + selected + mark, start + stars, selected.Length);
    }

    /// <summary>« **mot ** » n'est pas du gras : les espaces de bord restent hors de la marque.</summary>
    private static (int Start, int End) WithoutEdgeSpaces(string text, int start, int end)
    {
        var (from, to) = (start, end);
        while (from < to && char.IsWhiteSpace(text[from]))
        {
            from++;
        }
        while (to > from && char.IsWhiteSpace(text[to - 1]))
        {
            to--;
        }
        return from == to ? (start, start) : (from, to);
    }

    /// <summary>Le début de la balise ouvrante qui finit juste avant <paramref name="start"/>.</summary>
    private static int? OpeningBefore(string text, int start, string prefix)
    {
        if (start < prefix.Length + 2 || text[start - 1] != '>' || text[start - 2] != '"')
        {
            return null;
        }
        var opening = text.LastIndexOf(prefix, start - 1, StringComparison.Ordinal);
        return opening >= 0 && text.IndexOf('>', opening) == start - 1 ? opening : null;
    }

    /// <summary>Le plus petit passage aligné qui contient ces lignes, hors des blocs de code.</summary>
    private static AlignedPassage? AlignedPassageAround(string text, int from, int to)
    {
        if (!text.Contains(AlignmentStart, StringComparison.Ordinal))
        {
            return null;
        }
        var fences = MarkdownLines.Fences(text);
        var opened = new Stack<(int Start, int End)>();
        AlignedPassage? found = null;
        var position = 0;
        while (position <= text.Length)
        {
            var (lineStart, lineEnd) = LineAround(text, position);
            var line = text[lineStart..lineEnd];
            if (!fences.Exists(fence => fence.Start <= lineStart && lineStart <= fence.End))
            {
                if (AlignmentOpening().IsMatch(line))
                {
                    opened.Push((lineStart, lineEnd));
                }
                else if (line.TrimEnd() == "</div>" && opened.TryPop(out var open) && open.Start <= from && to <= lineEnd
                    && (found is null || open.Start > found.Value.Start))
                {
                    found = new AlignedPassage(open.Start, open.End, lineStart, lineEnd);
                }
            }
            position = lineEnd + 1;
        }
        return found;
    }

    private static bool IsAt(string text, int index, string expected) =>
        index >= 0 && string.CompareOrdinal(text, index, expected, 0, expected.Length) == 0;

    /// <summary>Les lignes que touche la sélection ; une sélection qui s'arrête en début de ligne n'y entre pas.</summary>
    private static (int Start, int End) LinesOf(string text, int start, int end)
    {
        var last = end > start && text[end - 1] == '\n' ? end - 1 : end;
        return (LineAround(text, start).Start, LineAround(text, last).End);
    }

    private static TextChange RewriteLines(string text, int start, int end, Func<string, string> rewrite)
    {
        var (from, to) = LinesOf(text, start, end);
        var lines = text[from..to].Split('\n');
        var written = lines.Select(rewrite).ToList();
        var result = string.Join('\n', written);
        var firstDelta = written[0].Length - lines[0].Length;
        var delta = result.Length - (to - from);
        // Une sélection de lignes entières le reste ; un curseur suit son texte.
        var selectionStart = end > start && start == from ? from : Math.Max(from, Math.Min(start + firstDelta, from + result.Length));
        var selectionEnd = Math.Max(selectionStart, Math.Min(end + delta, from + result.Length));
        return new TextChange(from, to - from, result, selectionStart, selectionEnd - selectionStart);
    }

    [GeneratedRegex(@"^ {0,3}> ?")]
    private static partial Regex QuotePrefix();

    [GeneratedRegex(@"^ {0,3}#{1,6}(?:[ \t]+|$)")]
    private static partial Regex HeadingPrefix();

    [GeneratedRegex(@"^(\s*)(?:[-*+]\s+|\d+[.)]\s+)?(?:\[[ xX]\](?:\s+|$))?")]
    private static partial Regex AnyMarker();

    [GeneratedRegex(@"^\s*[-*+]\s(?!\[[ xX]\](?:\s|$))")]
    private static partial Regex BulletMarker();

    [GeneratedRegex(@"^\s*[-*+]\s\[[ xX]\](?:\s|$)")]
    private static partial Regex TaskMarker();

    [GeneratedRegex(@"^<div align=""(center|right|justify)"">[ \t]*$")]
    private static partial Regex AlignmentOpening();

    [GeneratedRegex(@"^\s*\d+[.)]\s")]
    private static partial Regex NumberMarker();

    [GeneratedRegex(@"^( {0,3}(?:`{3,}|~{3,})).*$")]
    private static partial Regex OpeningFence();
}

/// <summary>Un passage aligné, dans le texte.</summary>
/// <param name="Start">Le début de sa ligne d'ouverture.</param>
/// <param name="OpeningEnd">La fin de sa ligne d'ouverture.</param>
/// <param name="ClosingStart">Le début de sa ligne de fermeture.</param>
/// <param name="End">La fin de sa ligne de fermeture.</param>
internal readonly record struct AlignedPassage(int Start, int OpeningEnd, int ClosingStart, int End);

/// <summary>Ce que l'éditeur doit savoir des lignes du Markdown : les blocs de code et ce qui ne se corrige pas.</summary>
internal static partial class MarkdownLines
{
    /// <summary>Un bloc de code clôturé.</summary>
    /// <param name="Start">Le début de sa ligne d'ouverture.</param>
    /// <param name="OpeningEnd">La fin de sa ligne d'ouverture.</param>
    /// <param name="End">La fin de sa ligne de fermeture, ou du texte s'il n'est pas fermé.</param>
    public readonly record struct Fence(int Start, int OpeningEnd, int End);

    /// <summary>Les blocs de code, comme CommonMark : la fermeture a le même signe, au moins aussi long.</summary>
    public static List<Fence> Fences(string text)
    {
        var fences = new List<Fence>();
        var position = 0;
        Match? opening = null;
        var openingStart = 0;
        var openingEnd = 0;
        while (position <= text.Length)
        {
            var end = text.IndexOf('\n', position);
            end = end < 0 ? text.Length : end;
            var line = text[position..end];
            if (opening is null)
            {
                var match = FenceLine().Match(line);
                if (match.Success && !(match.Groups[1].Value[0] == '`' && match.Groups[2].Value.Contains('`', StringComparison.Ordinal)))
                {
                    opening = match;
                    openingStart = position;
                    openingEnd = end;
                }
            }
            else
            {
                var match = FenceLine().Match(line);
                var marker = opening.Groups[1].Value;
                if (match.Success && match.Groups[1].Value[0] == marker[0] && match.Groups[1].Length >= marker.Length && match.Groups[2].Value.Trim().Length == 0)
                {
                    fences.Add(new Fence(openingStart, openingEnd, end));
                    opening = null;
                }
            }
            position = end + 1;
        }
        if (opening is not null)
        {
            fences.Add(new Fence(openingStart, openingEnd, text.Length));
        }
        return fences;
    }

    /// <summary>Le bloc de code qui contient <paramref name="position"/>.</summary>
    public static Fence? FenceAt(string text, int position)
    {
        foreach (var fence in Fences(text))
        {
            if (position >= fence.Start && position <= fence.End)
            {
                return fence;
            }
        }
        return null;
    }

    /// <summary>La position est dans un extrait de code, sur sa ligne.</summary>
    public static bool IsInInlineCode(string lineBefore)
    {
        var position = 0;
        while (position < lineBefore.Length)
        {
            var open = lineBefore.IndexOf('`', position);
            if (open < 0)
            {
                return false;
            }
            var length = 1;
            while (open + length < lineBefore.Length && lineBefore[open + length] == '`')
            {
                length++;
            }
            var close = ClosingRun(lineBefore, open + length, length);
            if (close < 0)
            {
                return true;
            }
            position = close + length;
        }
        return false;
    }

    /// <summary>Ce qui, sur une ligne, n'est pas du français : extraits de code, adresses, balises, formules.</summary>
    public static IEnumerable<(int Start, int End)> Unchecked(string line)
    {
        foreach (Match match in UncheckedText().Matches(line))
        {
            yield return (match.Index, match.Index + match.Length);
        }
    }

    private static int ClosingRun(string line, int from, int length)
    {
        var position = from;
        while (position < line.Length)
        {
            var next = line.IndexOf('`', position);
            if (next < 0)
            {
                return -1;
            }
            var run = 1;
            while (next + run < line.Length && line[next + run] == '`')
            {
                run++;
            }
            if (run == length)
            {
                return next;
            }
            position = next + run;
        }
        return -1;
    }

    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,})(.*)$")]
    private static partial Regex FenceLine();

    [GeneratedRegex(@"`[^`]*`|\]\([^)]*\)|</?[a-zA-Z][^>]*>|[a-z][a-z0-9+.-]*://\S+|\$[^$]+\$")]
    private static partial Regex UncheckedText();
}
