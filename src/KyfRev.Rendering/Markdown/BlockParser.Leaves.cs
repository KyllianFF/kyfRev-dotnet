namespace KyfRev.Rendering.Markdown;

/// <summary>Les blocs sans bloc enfant : tableaux, code, formules, titres, paragraphes et définitions de liens.</summary>
internal static partial class BlockParser
{
    private const int MaxAutocompletedCells = 65536;

    private const int MaxLabelLength = 999;

    private static string GetLine(BlockState state, int line) =>
        state.Source[(state.LineStarts[line] + state.TextShift[line])..state.LineEnds[line]];

    private static List<string> EscapedSplit(string text)
    {
        var result = new List<string>();
        var isEscaped = false;
        var lastPos = 0;
        var current = string.Empty;
        for (var pos = 0; pos < text.Length; pos++)
        {
            var ch = text[pos];
            if (ch == '|')
            {
                if (!isEscaped)
                {
                    result.Add(current + text[lastPos..pos]);
                    current = string.Empty;
                    lastPos = pos + 1;
                }
                else
                {
                    current += text[lastPos..(pos - 1)];
                    lastPos = pos;
                }
            }
            isEscaped = ch == '\\';
        }
        result.Add(current + text[lastPos..]);
        return result;
    }

    private static void DropEmptyEdges(List<string> columns)
    {
        if (columns.Count > 0 && columns[0].Length == 0)
        {
            columns.RemoveAt(0);
        }
        if (columns.Count > 0 && columns[^1].Length == 0)
        {
            columns.RemoveAt(columns.Count - 1);
        }
    }

    /// <summary>Lit la ligne de séparation d'un tableau : l'alignement de ses colonnes, ou rien si elle n'en est pas une.</summary>
    private static List<string?>? TableAlignments(BlockState state, int line)
    {
        if (state.SpaceCount[line] < state.BlockIndent || state.SpaceCount[line] - state.BlockIndent >= 4)
        {
            return null;
        }
        var pos = state.LineStarts[line] + state.TextShift[line];
        if (pos >= state.LineEnds[line])
        {
            return null;
        }
        var first = state.At(pos++);
        if (first is not ('|' or '-' or ':') || pos >= state.LineEnds[line])
        {
            return null;
        }
        var second = state.At(pos++);
        if ((second is not ('|' or '-' or ':') && !BlockState.IsSpace(second)) || (first == '-' && BlockState.IsSpace(second)))
        {
            return null;
        }
        while (pos < state.LineEnds[line])
        {
            var ch = state.At(pos);
            if (ch is not ('|' or '-' or ':') && !BlockState.IsSpace(ch))
            {
                return null;
            }
            pos++;
        }
        var columns = GetLine(state, line).Split('|');
        var alignments = new List<string?>();
        for (var i = 0; i < columns.Length; i++)
        {
            var column = JsText.Trim(columns[i]);
            if (column.Length == 0)
            {
                if (i == 0 || i == columns.Length - 1)
                {
                    continue;
                }
                return null;
            }
            var dashes = column.Trim(':');
            if (dashes.Length == 0 || dashes.Any(c => c != '-') || column.Length - dashes.Length > (column[0] == ':' ? 1 : 0) + (column[^1] == ':' ? 1 : 0))
            {
                return null;
            }
            alignments.Add(column[^1] == ':' ? (column[0] == ':' ? "center" : "right") : column[0] == ':' ? "left" : null);
        }
        return alignments;
    }

    private static bool Table(BlockState state, int startLine, int endLine, bool silent)
    {
        if (startLine + 2 > endLine || TableAlignments(state, startLine + 1) is not { } alignments)
        {
            return false;
        }
        var lineText = JsText.Trim(GetLine(state, startLine));
        if (!lineText.Contains('|', StringComparison.Ordinal) || state.SpaceCount[startLine] - state.BlockIndent >= 4)
        {
            return false;
        }
        var columns = EscapedSplit(lineText);
        DropEmptyEdges(columns);
        var columnCount = columns.Count;
        if (columnCount == 0 || columnCount != alignments.Count)
        {
            return false;
        }
        if (silent)
        {
            return true;
        }
        var oldParent = state.Parent;
        state.Parent = ParentBlock.Table;
        state.Push(TokenKind.Element, "table", 1).Line = startLine;
        state.Push(TokenKind.Element, "thead", 1).Line = startLine;
        state.Push(TokenKind.Element, "tr", 1).Line = startLine;
        for (var i = 0; i < columns.Count; i++)
        {
            PushCell(state, "th", alignments[i], JsText.Trim(columns[i]));
        }
        state.Push(TokenKind.Element, "tr", -1);
        state.Push(TokenKind.Element, "thead", -1);
        var hasBody = false;
        var autocompletedCells = 0;
        int nextLine;
        for (nextLine = startLine + 2; nextLine < endLine; nextLine++)
        {
            if (state.SpaceCount[nextLine] < state.BlockIndent || Terminates(BlockquoteTerminators, state, nextLine, endLine))
            {
                break;
            }
            lineText = JsText.Trim(GetLine(state, nextLine));
            if (lineText.Length == 0 || state.SpaceCount[nextLine] - state.BlockIndent >= 4)
            {
                break;
            }
            columns = EscapedSplit(lineText);
            DropEmptyEdges(columns);
            autocompletedCells += columnCount - columns.Count;
            if (autocompletedCells > MaxAutocompletedCells)
            {
                break;
            }
            if (nextLine == startLine + 2)
            {
                state.Push(TokenKind.Element, "tbody", 1).Line = startLine + 2;
                hasBody = true;
            }
            state.Push(TokenKind.Element, "tr", 1).Line = nextLine;
            for (var i = 0; i < columnCount; i++)
            {
                PushCell(state, "td", alignments[i], i < columns.Count ? JsText.Trim(columns[i]) : string.Empty);
            }
            state.Push(TokenKind.Element, "tr", -1);
        }
        if (hasBody)
        {
            state.Push(TokenKind.Element, "tbody", -1);
        }
        state.Push(TokenKind.Element, "table", -1);
        state.Parent = oldParent;
        state.Line = nextLine;
        return true;
    }

    private static void PushCell(BlockState state, string tag, string? alignment, string content)
    {
        state.Push(TokenKind.Element, tag, 1).Alignment = alignment;
        state.Push(TokenKind.Inline, string.Empty, 0).Content = content;
        state.Push(TokenKind.Element, tag, -1);
    }

    private static bool Code(BlockState state, int startLine, int endLine, bool silent)
    {
        if (state.SpaceCount[startLine] - state.BlockIndent < 4)
        {
            return false;
        }
        var nextLine = startLine + 1;
        var last = nextLine;
        while (nextLine < endLine)
        {
            if (state.IsEmpty(nextLine))
            {
                nextLine++;
                continue;
            }
            if (state.SpaceCount[nextLine] - state.BlockIndent >= 4)
            {
                nextLine++;
                last = nextLine;
                continue;
            }
            break;
        }
        state.Line = last;
        state.Push(TokenKind.CodeBlock, "code", 0).Content = state.GetLines(startLine, last, 4 + state.BlockIndent, false) + "\n";
        return true;
    }

    private static bool Fence(BlockState state, int startLine, int endLine, bool silent)
    {
        var pos = state.LineStarts[startLine] + state.TextShift[startLine];
        var max = state.LineEnds[startLine];
        if (state.SpaceCount[startLine] - state.BlockIndent >= 4 || pos + 3 > max)
        {
            return false;
        }
        var marker = state.At(pos);
        if (marker is not ('~' or '`'))
        {
            return false;
        }
        var mem = pos;
        pos = state.SkipChars(pos, marker);
        var length = pos - mem;
        if (length < 3)
        {
            return false;
        }
        var parameters = state.Source[pos..max];
        if (marker == '`' && parameters.Contains('`', StringComparison.Ordinal))
        {
            return false;
        }
        if (silent)
        {
            return true;
        }
        var nextLine = startLine;
        var haveEndMarker = false;
        while (true)
        {
            nextLine++;
            if (nextLine >= endLine)
            {
                break;
            }
            pos = mem = state.LineStarts[nextLine] + state.TextShift[nextLine];
            max = state.LineEnds[nextLine];
            if (pos < max && state.SpaceCount[nextLine] < state.BlockIndent)
            {
                break;
            }
            if (state.At(pos) != marker || state.SpaceCount[nextLine] - state.BlockIndent >= 4)
            {
                continue;
            }
            pos = state.SkipChars(pos, marker);
            if (pos - mem < length)
            {
                continue;
            }
            pos = state.SkipSpaces(pos);
            if (pos < max)
            {
                continue;
            }
            haveEndMarker = true;
            break;
        }
        length = state.SpaceCount[startLine];
        state.Line = nextLine + (haveEndMarker ? 1 : 0);
        var token = state.Push(TokenKind.Fence, "code", 0);
        token.Info = parameters;
        token.Content = state.GetLines(startLine + 1, nextLine, length, true);
        return true;
    }

    private static bool IsWhiteSpace(int code) => code is >= 0x2000 and <= 0x200A
        or '\t' or '\n' or '\v' or '\f' or '\r' or ' ' or 0xA0 or 0x1680 or 0x202F or 0x205F or 0x3000;

    private static int SkipWhiteSpaceBack(BlockState state, int max, int min)
    {
        var pos = max;
        while (pos > min && IsWhiteSpace(state.At(pos - 1)))
        {
            pos--;
        }
        return pos;
    }

    private static bool HasDollarPairWithTrailingContent(BlockState state, int pos, int end)
    {
        for (var i = pos; i < end - 1; i++)
        {
            if (state.At(i) != '$' || state.At(i + 1) != '$')
            {
                continue;
            }
            for (var j = i + 2; j < end; j++)
            {
                if (!IsWhiteSpace(state.At(j)))
                {
                    return true;
                }
            }
            break;
        }
        return false;
    }

    private static bool EndsWithDollars(BlockState state, int pos, int contentEnd) =>
        contentEnd - pos >= 2 && state.At(contentEnd - 1) == '$' && state.At(contentEnd - 2) == '$';

    private static bool MathBlock(BlockState state, int startLine, int endLine, bool silent)
    {
        var start = state.LineStarts[startLine] + state.TextShift[startLine];
        var end = state.LineEnds[startLine];
        if (state.At(start) != '$' || state.At(start + 1) != '$' || HasDollarPairWithTrailingContent(state, start + 2, end))
        {
            return false;
        }
        if (silent)
        {
            return true;
        }
        var contentEnd = SkipWhiteSpaceBack(state, end, start);
        var pos = start + 2;
        string firstLine;
        var found = false;
        if (EndsWithDollars(state, pos, contentEnd))
        {
            firstLine = state.Source[pos..(contentEnd - 2)];
            found = true;
        }
        else
        {
            firstLine = state.Source[pos..end];
        }
        var current = startLine;
        var lastLine = string.Empty;
        while (!found)
        {
            current++;
            if (current >= endLine)
            {
                break;
            }
            pos = state.LineStarts[current] + state.TextShift[current];
            end = state.LineEnds[current];
            if (pos < end && state.TextShift[current] < state.BlockIndent)
            {
                break;
            }
            contentEnd = SkipWhiteSpaceBack(state, end, pos);
            if (EndsWithDollars(state, pos, contentEnd))
            {
                lastLine = state.Source[pos..(contentEnd - 2)];
                found = true;
            }
        }
        state.Line = found ? current + 1 : current;
        state.Push(TokenKind.MathBlock, "math", 0).Content =
            (firstLine.Length > 0 ? firstLine + "\n" : string.Empty)
            + state.GetLines(startLine + 1, current, state.TextShift[startLine], true)
            + (lastLine.Length > 0 ? lastLine + "\n" : string.Empty);
        return true;
    }

    private static bool HorizontalRule(BlockState state, int startLine, int endLine, bool silent)
    {
        var max = state.LineEnds[startLine];
        if (state.SpaceCount[startLine] - state.BlockIndent >= 4)
        {
            return false;
        }
        var pos = state.LineStarts[startLine] + state.TextShift[startLine];
        var marker = state.At(pos++);
        if (marker is not ('*' or '-' or '_'))
        {
            return false;
        }
        var count = 1;
        while (pos < max)
        {
            var ch = state.At(pos++);
            if (ch != marker && !BlockState.IsSpace(ch))
            {
                return false;
            }
            if (ch == marker)
            {
                count++;
            }
        }
        if (count < 3)
        {
            return false;
        }
        if (silent)
        {
            return true;
        }
        state.Line = startLine + 1;
        state.Push(TokenKind.Element, "hr", 0);
        return true;
    }

    private static bool Heading(BlockState state, int startLine, int endLine, bool silent)
    {
        var pos = state.LineStarts[startLine] + state.TextShift[startLine];
        var max = state.LineEnds[startLine];
        if (state.SpaceCount[startLine] - state.BlockIndent >= 4)
        {
            return false;
        }
        var ch = state.At(pos);
        if (ch != '#' || pos >= max)
        {
            return false;
        }
        var level = 1;
        ch = state.At(++pos);
        while (ch == '#' && pos < max && level <= 6)
        {
            level++;
            ch = state.At(++pos);
        }
        if (level > 6 || (pos < max && !BlockState.IsSpace(ch)))
        {
            return false;
        }
        if (silent)
        {
            return true;
        }
        max = state.SkipSpacesBack(max, pos);
        var closing = state.SkipCharsBack(max, '#', pos);
        if (closing > pos && BlockState.IsSpace(state.At(closing - 1)))
        {
            max = closing;
        }
        state.Line = startLine + 1;
        PushHeading(state, level, startLine, AsciiTrim(state.Source[pos..max]));
        return true;
    }

    private static void PushHeading(BlockState state, int level, int startLine, string content)
    {
        var tag = "h" + level.ToString(System.Globalization.CultureInfo.InvariantCulture);
        state.Push(TokenKind.Element, tag, 1).Line = startLine;
        state.Push(TokenKind.Inline, string.Empty, 0).Content = content;
        state.Push(TokenKind.Element, tag, -1);
    }

    private static string AsciiTrim(string text) => text.Trim(' ', '\t', '\n', '\r');

    private static bool SetextHeading(BlockState state, int startLine, int endLine, bool silent)
    {
        if (state.SpaceCount[startLine] - state.BlockIndent >= 4)
        {
            return false;
        }
        var oldParent = state.Parent;
        state.Parent = ParentBlock.Paragraph;
        var level = 0;
        var nextLine = startLine + 1;
        for (; nextLine < endLine && !state.IsEmpty(nextLine); nextLine++)
        {
            if (state.SpaceCount[nextLine] - state.BlockIndent > 3)
            {
                continue;
            }
            if (state.SpaceCount[nextLine] >= state.BlockIndent)
            {
                var pos = state.LineStarts[nextLine] + state.TextShift[nextLine];
                var max = state.LineEnds[nextLine];
                if (pos < max)
                {
                    var marker = state.At(pos);
                    if (marker is '-' or '=')
                    {
                        pos = state.SkipChars(pos, marker);
                        pos = state.SkipSpaces(pos);
                        if (pos >= max)
                        {
                            level = marker == '=' ? 1 : 2;
                            break;
                        }
                    }
                }
            }
            if (state.SpaceCount[nextLine] < 0)
            {
                continue;
            }
            if (Terminates(ParagraphTerminators, state, nextLine, endLine))
            {
                break;
            }
        }
        if (level == 0)
        {
            state.Parent = oldParent;
            return false;
        }
        var content = AsciiTrim(state.GetLines(startLine, nextLine, state.BlockIndent, false));
        state.Line = nextLine + 1;
        PushHeading(state, level, startLine, content);
        state.Parent = oldParent;
        return true;
    }

    private static bool Paragraph(BlockState state, int startLine, int endLine, bool silent)
    {
        var oldParent = state.Parent;
        var nextLine = startLine + 1;
        state.Parent = ParentBlock.Paragraph;
        for (; nextLine < endLine && !state.IsEmpty(nextLine); nextLine++)
        {
            if (state.SpaceCount[nextLine] - state.BlockIndent > 3 || state.SpaceCount[nextLine] < 0)
            {
                continue;
            }
            if (Terminates(ParagraphTerminators, state, nextLine, endLine))
            {
                break;
            }
        }
        var content = AsciiTrim(state.GetLines(startLine, nextLine, state.BlockIndent, false));
        state.Line = nextLine;
        state.Push(TokenKind.ParagraphOpen, "p", 1).Line = startLine;
        state.Push(TokenKind.Inline, string.Empty, 0).Content = content;
        state.Push(TokenKind.ParagraphClose, "p", -1);
        state.Parent = oldParent;
        return true;
    }

    private static int CountCodePoints(string text)
    {
        var length = text.Length;
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i + 1]))
            {
                length--;
                i++;
            }
        }
        return length;
    }

    private static int LineEnd(BlockState state, int line) => Math.Min(state.LineEnds[line] + 1, state.Source.Length);

    private static int NextLinePosition(BlockState state, int nextLine)
    {
        if (nextLine >= state.LineMax || state.IsEmpty(nextLine))
        {
            return -1;
        }
        var isContinuation = state.SpaceCount[nextLine] - state.BlockIndent > 3 || state.SpaceCount[nextLine] < 0;
        if (!isContinuation)
        {
            var oldParent = state.Parent;
            state.Parent = ParentBlock.Reference;
            var terminate = Terminates(ParagraphTerminators, state, nextLine, state.LineMax);
            state.Parent = oldParent;
            if (terminate)
            {
                return -1;
            }
        }
        return state.LineStarts[nextLine] + state.TextShift[nextLine];
    }

    /// <summary>Saute les espaces et les sauts de ligne autorisés d'une définition de lien.</summary>
    private static void SkipDefinitionSpaces(BlockState state, ref int pos, ref int max, ref int nextLine)
    {
        while (pos < max)
        {
            var ch = state.At(pos);
            if (ch == '\n')
            {
                var nextPos = NextLinePosition(state, nextLine);
                if (nextPos < 0)
                {
                    break;
                }
                pos = nextPos;
                max = LineEnd(state, nextLine++);
            }
            else if (BlockState.IsSpace(ch))
            {
                pos++;
            }
            else
            {
                break;
            }
        }
    }

    private static bool Reference(BlockState state, int startLine, int endLine, bool silent)
    {
        var source = state.Source;
        var pos = state.LineStarts[startLine] + state.TextShift[startLine];
        var nextLine = startLine + 1;
        if (state.SpaceCount[startLine] - state.BlockIndent >= 4 || state.At(pos) != '[')
        {
            return false;
        }
        var max = LineEnd(state, startLine);
        var rawLabel = string.Empty;
        var labelEnd = -1;
        var segmentStart = ++pos;
        while (pos < max)
        {
            if (rawLabel.Length + pos - segmentStart > MaxLabelLength * 2)
            {
                return false;
            }
            var ch = state.At(pos);
            if (ch == '[')
            {
                return false;
            }
            if (ch == ']')
            {
                labelEnd = pos;
                break;
            }
            if (ch == '\\')
            {
                ch = state.At(++pos);
            }
            if (ch == '\n' && pos < max)
            {
                var nextPos = NextLinePosition(state, nextLine);
                if (nextPos < 0)
                {
                    return false;
                }
                rawLabel += source[segmentStart..max];
                pos = segmentStart = nextPos;
                max = LineEnd(state, nextLine++);
                continue;
            }
            pos++;
        }
        if (labelEnd < 0 || state.At(labelEnd + 1) != ':')
        {
            return false;
        }
        rawLabel += source[segmentStart..labelEnd];
        if (rawLabel.Length > MaxLabelLength && CountCodePoints(rawLabel) > MaxLabelLength)
        {
            return false;
        }
        pos = labelEnd + 2;
        SkipDefinitionSpaces(state, ref pos, ref max, ref nextLine);
        var destination = LinkScanner.ParseDestination(source, pos, max);
        if (!destination.Ok)
        {
            return false;
        }
        var href = Url.NormalizeLink(destination.Text);
        if (!Url.IsValidLink(href))
        {
            return false;
        }
        pos = destination.Position;
        var destinationEnd = pos;
        var destinationMax = max;
        var destinationLine = nextLine;
        SkipDefinitionSpaces(state, ref pos, ref max, ref nextLine);
        var titleStart = pos;
        var title = LinkScanner.ParseTitle(source, pos, max, null);
        while (title.CanContinue)
        {
            var nextPos = NextLinePosition(state, nextLine);
            if (nextPos < 0)
            {
                break;
            }
            pos = nextPos;
            max = LineEnd(state, nextLine++);
            title = LinkScanner.ParseTitle(source, pos, max, title);
        }
        string titleText;
        if (titleStart != destinationEnd && title.Ok)
        {
            titleText = title.Text;
            pos = title.Position;
        }
        else
        {
            titleText = string.Empty;
            pos = destinationEnd;
            max = destinationMax;
            nextLine = destinationLine;
        }
        while (pos < max && BlockState.IsSpace(state.At(pos)))
        {
            pos++;
        }
        if (pos < max && state.At(pos) != '\n' && titleText.Length > 0)
        {
            titleText = string.Empty;
            pos = destinationEnd;
            max = destinationMax;
            nextLine = destinationLine;
            while (pos < max && BlockState.IsSpace(state.At(pos)))
            {
                pos++;
            }
        }
        if (pos < max && state.At(pos) != '\n')
        {
            return false;
        }
        var label = LinkScanner.NormalizeReference(rawLabel);
        if (label.Length == 0)
        {
            return false;
        }
        if (silent)
        {
            return true;
        }
        state.Environment.References.TryAdd(label, (href, titleText));
        state.Push(TokenKind.ReferenceDefinition, string.Empty, 0).Hidden = true;
        state.Line = nextLine;
        return true;
    }
}
