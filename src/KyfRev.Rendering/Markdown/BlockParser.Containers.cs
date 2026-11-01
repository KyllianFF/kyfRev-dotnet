namespace KyfRev.Rendering.Markdown;

/// <summary>Les blocs qui en contiennent d'autres : citations, listes et notes.</summary>
internal static partial class BlockParser
{
    private static bool Blockquote(BlockState state, int startLine, int endLine, bool silent)
    {
        var pos = state.LineStarts[startLine] + state.TextShift[startLine];
        var oldLineMax = state.LineMax;
        if (state.SpaceCount[startLine] - state.BlockIndent >= 4 || state.At(pos) != '>')
        {
            return false;
        }
        if (silent)
        {
            return true;
        }
        var oldLineStarts = new List<int>();
        var oldBaseSpaceCount = new List<int>();
        var oldSpaceCount = new List<int>();
        var oldTextShift = new List<int>();
        var oldParent = state.Parent;
        state.Parent = ParentBlock.Blockquote;
        var lastLineEmpty = false;
        int nextLine;
        for (nextLine = startLine; nextLine < endLine; nextLine++)
        {
            var isOutdented = state.SpaceCount[nextLine] < state.BlockIndent;
            pos = state.LineStarts[nextLine] + state.TextShift[nextLine];
            var max = state.LineEnds[nextLine];
            if (pos >= max)
            {
                break;
            }
            if (state.At(pos++) == '>' && !isOutdented)
            {
                var initial = state.SpaceCount[nextLine] + 1;
                bool spaceAfterMarker;
                var adjustTab = false;
                if (state.At(pos) == ' ')
                {
                    pos++;
                    initial++;
                    spaceAfterMarker = true;
                }
                else if (state.At(pos) == '\t')
                {
                    spaceAfterMarker = true;
                    if ((state.BaseSpaceCount[nextLine] + initial) % 4 == 3)
                    {
                        pos++;
                        initial++;
                    }
                    else
                    {
                        adjustTab = true;
                    }
                }
                else
                {
                    spaceAfterMarker = false;
                }
                var offset = initial;
                oldLineStarts.Add(state.LineStarts[nextLine]);
                state.LineStarts[nextLine] = pos;
                while (pos < max)
                {
                    var ch = state.At(pos);
                    if (!BlockState.IsSpace(ch))
                    {
                        break;
                    }
                    offset += ch == '\t' ? 4 - ((offset + state.BaseSpaceCount[nextLine] + (adjustTab ? 1 : 0)) % 4) : 1;
                    pos++;
                }
                lastLineEmpty = pos >= max;
                oldBaseSpaceCount.Add(state.BaseSpaceCount[nextLine]);
                state.BaseSpaceCount[nextLine] = state.SpaceCount[nextLine] + 1 + (spaceAfterMarker ? 1 : 0);
                oldSpaceCount.Add(state.SpaceCount[nextLine]);
                state.SpaceCount[nextLine] = offset - initial;
                oldTextShift.Add(state.TextShift[nextLine]);
                state.TextShift[nextLine] = pos - state.LineStarts[nextLine];
                continue;
            }
            if (lastLineEmpty)
            {
                break;
            }
            if (Terminates(BlockquoteTerminators, state, nextLine, endLine))
            {
                state.LineMax = nextLine;
                if (state.BlockIndent != 0)
                {
                    oldLineStarts.Add(state.LineStarts[nextLine]);
                    oldBaseSpaceCount.Add(state.BaseSpaceCount[nextLine]);
                    oldTextShift.Add(state.TextShift[nextLine]);
                    oldSpaceCount.Add(state.SpaceCount[nextLine]);
                    state.SpaceCount[nextLine] -= state.BlockIndent;
                }
                break;
            }
            oldLineStarts.Add(state.LineStarts[nextLine]);
            oldBaseSpaceCount.Add(state.BaseSpaceCount[nextLine]);
            oldTextShift.Add(state.TextShift[nextLine]);
            oldSpaceCount.Add(state.SpaceCount[nextLine]);
            state.SpaceCount[nextLine] = -1;
        }
        var oldIndent = state.BlockIndent;
        state.BlockIndent = 0;
        var open = state.Push(TokenKind.Element, "blockquote", 1);
        open.Line = startLine;
        Tokenize(state, startLine, nextLine);
        state.Push(TokenKind.Element, "blockquote", -1);
        state.LineMax = oldLineMax;
        state.Parent = oldParent;
        for (var i = 0; i < oldTextShift.Count; i++)
        {
            state.LineStarts[i + startLine] = oldLineStarts[i];
            state.TextShift[i + startLine] = oldTextShift[i];
            state.SpaceCount[i + startLine] = oldSpaceCount[i];
            state.BaseSpaceCount[i + startLine] = oldBaseSpaceCount[i];
        }
        state.BlockIndent = oldIndent;
        return true;
    }

    private static int SkipBulletListMarker(BlockState state, int startLine)
    {
        var max = state.LineEnds[startLine];
        var pos = state.LineStarts[startLine] + state.TextShift[startLine];
        var marker = state.At(pos++);
        if (marker is not ('*' or '-' or '+'))
        {
            return -1;
        }
        return pos < max && !BlockState.IsSpace(state.At(pos)) ? -1 : pos;
    }

    private static int SkipOrderedListMarker(BlockState state, int startLine)
    {
        var start = state.LineStarts[startLine] + state.TextShift[startLine];
        var max = state.LineEnds[startLine];
        var pos = start;
        if (pos + 1 >= max)
        {
            return -1;
        }
        var ch = state.At(pos++);
        if (ch is < '0' or > '9')
        {
            return -1;
        }
        while (true)
        {
            if (pos >= max)
            {
                return -1;
            }
            ch = state.At(pos++);
            if (ch is >= '0' and <= '9')
            {
                if (pos - start >= 10)
                {
                    return -1;
                }
                continue;
            }
            if (ch is ')' or '.')
            {
                break;
            }
            return -1;
        }
        return pos < max && !BlockState.IsSpace(state.At(pos)) ? -1 : pos;
    }

    private static void MarkTightParagraphs(BlockState state, int index)
    {
        var level = state.Level + 2;
        for (var i = index + 2; i < state.Tokens.Count - 2; i++)
        {
            if (state.Tokens[i].Level == level && state.Tokens[i].Kind == TokenKind.ParagraphOpen)
            {
                state.Tokens[i + 2].Hidden = true;
                state.Tokens[i].Hidden = true;
                i += 2;
            }
        }
    }

    private static bool List(BlockState state, int startLine, int endLine, bool silent)
    {
        var nextLine = startLine;
        var tight = true;
        if (state.SpaceCount[nextLine] - state.BlockIndent >= 4)
        {
            return false;
        }
        if (state.ListIndent >= 0 && state.SpaceCount[nextLine] - state.ListIndent >= 4 && state.SpaceCount[nextLine] < state.BlockIndent)
        {
            return false;
        }
        var isTerminatingParagraph = silent && state.Parent == ParentBlock.Paragraph && state.SpaceCount[nextLine] >= state.BlockIndent;
        var isOrdered = false;
        var markerValue = 0;
        var posAfterMarker = SkipOrderedListMarker(state, nextLine);
        if (posAfterMarker >= 0)
        {
            isOrdered = true;
            var start = state.LineStarts[nextLine] + state.TextShift[nextLine];
            markerValue = int.Parse(state.Source.AsSpan(start, posAfterMarker - 1 - start), provider: System.Globalization.CultureInfo.InvariantCulture);
            if (isTerminatingParagraph && markerValue != 1)
            {
                return false;
            }
        }
        else
        {
            posAfterMarker = SkipBulletListMarker(state, nextLine);
            if (posAfterMarker < 0)
            {
                return false;
            }
        }
        if (isTerminatingParagraph && state.SkipSpaces(posAfterMarker) >= state.LineEnds[nextLine])
        {
            return false;
        }
        if (silent)
        {
            return true;
        }
        var markerCharacter = state.At(posAfterMarker - 1);
        var listIndex = state.Tokens.Count;
        var tag = isOrdered ? "ol" : "ul";
        var listOpen = state.Push(TokenKind.Element, tag, 1);
        if (isOrdered && markerValue != 1)
        {
            listOpen.Start = markerValue;
        }
        listOpen.Line = nextLine;
        var previousEmptyEnd = false;
        var oldParent = state.Parent;
        state.Parent = ParentBlock.List;
        while (nextLine < endLine)
        {
            var pos = posAfterMarker;
            var max = state.LineEnds[nextLine];
            var initial = state.SpaceCount[nextLine] + posAfterMarker - (state.LineStarts[nextLine] + state.TextShift[nextLine]);
            var offset = initial;
            while (pos < max)
            {
                var ch = state.At(pos);
                if (ch == '\t')
                {
                    offset += 4 - ((offset + state.BaseSpaceCount[nextLine]) % 4);
                }
                else if (ch == ' ')
                {
                    offset++;
                }
                else
                {
                    break;
                }
                pos++;
            }
            var contentStart = pos;
            var indentAfterMarker = contentStart >= max ? 1 : offset - initial;
            if (indentAfterMarker > 4)
            {
                indentAfterMarker = 1;
            }
            var indent = initial + indentAfterMarker;
            var item = state.Push(TokenKind.Element, "li", 1);
            item.Line = nextLine;
            var oldTight = state.Tight;
            var oldTextShift = state.TextShift[nextLine];
            var oldSpaceCount = state.SpaceCount[nextLine];
            var oldListIndent = state.ListIndent;
            state.ListIndent = state.BlockIndent;
            state.BlockIndent = indent;
            state.Tight = true;
            state.TextShift[nextLine] = contentStart - state.LineStarts[nextLine];
            state.SpaceCount[nextLine] = offset;
            if (contentStart >= max && state.IsEmpty(nextLine + 1))
            {
                state.Line = Math.Min(state.Line + 2, endLine);
            }
            else
            {
                Tokenize(state, nextLine, endLine);
            }
            if (!state.Tight || previousEmptyEnd)
            {
                tight = false;
            }
            previousEmptyEnd = state.Line - nextLine > 1 && state.IsEmpty(state.Line - 1);
            state.BlockIndent = state.ListIndent;
            state.ListIndent = oldListIndent;
            state.TextShift[nextLine] = oldTextShift;
            state.SpaceCount[nextLine] = oldSpaceCount;
            state.Tight = oldTight;
            state.Push(TokenKind.Element, "li", -1);
            nextLine = state.Line;
            if (nextLine >= endLine
                || state.SpaceCount[nextLine] < state.BlockIndent
                || state.SpaceCount[nextLine] - state.BlockIndent >= 4
                || Terminates(ListTerminators, state, nextLine, endLine))
            {
                break;
            }
            posAfterMarker = isOrdered ? SkipOrderedListMarker(state, nextLine) : SkipBulletListMarker(state, nextLine);
            if (posAfterMarker < 0 || markerCharacter != state.At(posAfterMarker - 1))
            {
                break;
            }
        }
        state.Push(TokenKind.Element, tag, -1);
        state.Line = nextLine;
        state.Parent = oldParent;
        if (tight)
        {
            MarkTightParagraphs(state, listIndex);
        }
        return true;
    }

    private static bool FootnoteDefinition(BlockState state, int startLine, int endLine, bool silent)
    {
        var start = state.LineStarts[startLine] + state.TextShift[startLine];
        var max = state.LineEnds[startLine];
        if (start + 4 > max || state.At(start) != '[' || state.At(start + 1) != '^')
        {
            return false;
        }
        int pos;
        for (pos = start + 2; pos < max; pos++)
        {
            if (state.At(pos) == ' ')
            {
                return false;
            }
            if (state.At(pos) == ']')
            {
                break;
            }
        }
        if (pos == start + 2 || pos + 1 >= max || state.At(++pos) != ':')
        {
            return false;
        }
        if (silent)
        {
            return true;
        }
        pos++;
        var label = state.Source[(start + 2)..(pos - 2)];
        state.Environment.FootnoteReferences[label] = -1;
        state.Tokens.Add(new Token(TokenKind.FootnoteReferenceOpen, string.Empty, 1) { Label = label, Level = state.Level++ });
        var oldLineStart = state.LineStarts[startLine];
        var oldTextShift = state.TextShift[startLine];
        var oldSpaceCount = state.SpaceCount[startLine];
        var oldParent = state.Parent;
        var posAfterColon = pos;
        var initial = state.SpaceCount[startLine] + pos - (state.LineStarts[startLine] + state.TextShift[startLine]);
        var offset = initial;
        while (pos < max)
        {
            var ch = state.At(pos);
            if (!BlockState.IsSpace(ch))
            {
                break;
            }
            offset += ch == '\t' ? 4 - (offset % 4) : 1;
            pos++;
        }
        state.TextShift[startLine] = pos - posAfterColon;
        state.SpaceCount[startLine] = offset - initial;
        state.LineStarts[startLine] = posAfterColon;
        state.BlockIndent += 4;
        state.Parent = ParentBlock.Footnote;
        if (state.SpaceCount[startLine] < state.BlockIndent)
        {
            state.SpaceCount[startLine] += state.BlockIndent;
        }
        Tokenize(state, startLine, endLine);
        state.Parent = oldParent;
        state.BlockIndent -= 4;
        state.TextShift[startLine] = oldTextShift;
        state.SpaceCount[startLine] = oldSpaceCount;
        state.LineStarts[startLine] = oldLineStart;
        state.Tokens.Add(new Token(TokenKind.FootnoteReferenceClose, string.Empty, -1) { Level = --state.Level });
        return true;
    }
}
