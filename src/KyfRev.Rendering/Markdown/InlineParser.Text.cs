using System.Text.RegularExpressions;

namespace KyfRev.Rendering.Markdown;

/// <summary>Le texte, les échappements, le code, les formules, les marques et les entités.</summary>
internal static partial class InlineParser
{
    private static bool IsTerminator(int ch) => ch is '\n' or '!' or '#' or '$' or '%' or '&' or '*' or '+' or '-' or ':'
        or '<' or '=' or '>' or '@' or '[' or '\\' or ']' or '^' or '_' or '`' or '{' or '}' or '~';

    private static bool Text(InlineState state, bool silent)
    {
        var pos = state.Position;
        while (pos < state.PositionMax && !IsTerminator(state.At(pos)))
        {
            pos++;
        }
        if (pos == state.Position)
        {
            return false;
        }
        if (!silent)
        {
            state.Pending.Append(state.Source, state.Position, pos - state.Position);
        }
        state.Position = pos;
        return true;
    }

    private static bool Newline(InlineState state, bool silent)
    {
        var pos = state.Position;
        if (state.At(pos) != '\n')
        {
            return false;
        }
        if (!silent)
        {
            var pending = state.Pending;
            var pendingMax = pending.Length - 1;
            if (pendingMax >= 0 && pending[pendingMax] == ' ')
            {
                if (pendingMax >= 1 && pending[pendingMax - 1] == ' ')
                {
                    var spaces = pendingMax - 1;
                    while (spaces >= 1 && pending[spaces - 1] == ' ')
                    {
                        spaces--;
                    }
                    pending.Length = spaces;
                    state.Push(TokenKind.Hardbreak, "br", 0);
                }
                else
                {
                    pending.Length--;
                    state.Push(TokenKind.Softbreak, "br", 0);
                }
            }
            else
            {
                state.Push(TokenKind.Softbreak, "br", 0);
            }
        }
        pos++;
        while (pos < state.PositionMax && BlockState.IsSpace(state.At(pos)))
        {
            pos++;
        }
        state.Position = pos;
        return true;
    }

    private static bool Escape(InlineState state, bool silent)
    {
        var pos = state.Position;
        var max = state.PositionMax;
        if (state.At(pos) != '\\')
        {
            return false;
        }
        pos++;
        if (pos >= max)
        {
            return false;
        }
        var first = state.At(pos);
        if (first == '\n')
        {
            if (!silent)
            {
                state.Push(TokenKind.Hardbreak, "br", 0);
            }
            pos++;
            while (pos < max && BlockState.IsSpace(state.At(pos)))
            {
                pos++;
            }
            state.Position = pos;
            return true;
        }
        if (first == ' ')
        {
            if (!silent)
            {
                state.Push(TokenKind.TextSpecial, string.Empty, 0).Content = "\\";
            }
            state.Position = pos;
            return true;
        }
        var escaped = state.Source[pos].ToString();
        if (char.IsHighSurrogate((char)first) && pos + 1 < max && char.IsLowSurrogate(state.Source[pos + 1]))
        {
            escaped += state.Source[pos + 1];
            pos++;
        }
        if (!silent)
        {
            state.Push(TokenKind.TextSpecial, string.Empty, 0).Content = UnicodeClasses.IsAsciiPunctuation(first) ? escaped : "\\" + escaped;
        }
        state.Position = pos + 1;
        return true;
    }

    private static bool IsWordCharacter(int code) => code is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_';

    private static bool IsDollarOpen(InlineState state, int pos)
    {
        var previous = state.At(pos - 1);
        return previous != '$'
            && (pos == 0 || BlockState.IsSpace(previous) || !IsWordCharacter(previous))
            && !BlockState.IsSpace(state.At(pos + 1));
    }

    private static bool IsDollarClose(InlineState state, int pos)
    {
        var next = state.At(pos + 1);
        return next != '$'
            && (BlockState.IsSpace(next) || !IsWordCharacter(next))
            && !BlockState.IsSpace(state.At(pos - 1));
    }

    private static bool MathInline(InlineState state, bool silent)
    {
        if (state.At(state.Position) != '$')
        {
            return false;
        }
        if (!IsDollarOpen(state, state.Position))
        {
            return LiteralDollars(state, silent, "$");
        }
        var match = state.Position + 1;
        while ((match = match <= state.Source.Length ? state.Source.IndexOf('$', match) : -1) != -1)
        {
            if (match >= state.PositionMax)
            {
                match = -1;
                break;
            }
            var pos = match - 1;
            while (state.At(pos) == '\\')
            {
                pos--;
            }
            if ((match - pos) % 2 == 0)
            {
                match++;
                continue;
            }
            if (IsDollarClose(state, match))
            {
                break;
            }
            if (BlockState.IsSpace(state.At(match - 1)) && (match + 1 >= state.PositionMax || BlockState.IsSpace(state.At(match + 1))))
            {
                match++;
                continue;
            }
            match = -1;
            break;
        }
        if (match == -1)
        {
            return LiteralDollars(state, silent, "$");
        }
        if (match == state.Position + 1)
        {
            return LiteralDollars(state, silent, "$$");
        }
        if (!silent)
        {
            state.Push(TokenKind.MathInline, "math", 0).Content = state.Source[(state.Position + 1)..match];
        }
        state.Position = match + 1;
        return true;
    }

    private static bool LiteralDollars(InlineState state, bool silent, string dollars)
    {
        if (!silent)
        {
            state.Pending.Append(dollars);
        }
        state.Position += dollars.Length;
        return true;
    }

    private static Dictionary<int, int> LastBacktickRuns(string source)
    {
        var runs = new Dictionary<int, int>();
        var pos = 0;
        while ((pos = source.IndexOf('`', pos)) != -1)
        {
            var start = pos;
            do
            {
                pos++;
            }
            while (pos < source.Length && source[pos] == '`');
            runs[pos - start] = start;
        }
        return runs;
    }

    private static bool Backticks(InlineState state, bool silent)
    {
        var start = state.Position;
        if (state.At(start) != '`')
        {
            return false;
        }
        var max = state.PositionMax;
        var pos = start + 1;
        while (pos < max && state.At(pos) == '`')
        {
            pos++;
        }
        var openerLength = pos - start;
        state.LastBacktickRuns ??= LastBacktickRuns(state.Source);
        if (state.LastBacktickRuns.TryGetValue(openerLength, out var lastRun) && lastRun >= pos)
        {
            var matchEnd = pos;
            int matchStart;
            while ((matchStart = state.Source.IndexOf('`', matchEnd)) != -1 && matchStart < max)
            {
                matchEnd = matchStart + 1;
                while (state.At(matchEnd) == '`')
                {
                    matchEnd++;
                }
                if (matchEnd > max)
                {
                    break;
                }
                if (matchEnd - matchStart == openerLength)
                {
                    if (!silent)
                    {
                        var content = state.Source[pos..matchStart].Replace('\n', ' ');
                        if (content.StartsWith(' ') && content.EndsWith(' ') && content.Any(c => c != ' '))
                        {
                            content = content[1..^1];
                        }
                        state.Push(TokenKind.CodeInline, "code", 0).Content = content;
                    }
                    state.Position = matchEnd;
                    return true;
                }
            }
        }
        if (!silent)
        {
            state.Pending.Append('`', openerLength);
        }
        state.Position = pos;
        return true;
    }

    private static bool Strikethrough(InlineState state, bool silent)
    {
        if (silent || state.At(state.Position) != '~')
        {
            return false;
        }
        var (canOpen, canClose, scanned) = state.ScanDelimiters(state.Position, true);
        var length = scanned;
        if (length < 2)
        {
            return false;
        }
        if (length % 2 == 1)
        {
            state.Push(TokenKind.Text, string.Empty, 0).Content = "~";
            length--;
        }
        for (var i = 0; i < length; i += 2)
        {
            state.Push(TokenKind.Text, string.Empty, 0).Content = "~~";
            state.Delimiters.Add(new Delimiter { Marker = '~', Length = 0, Token = state.Tokens.Count - 1, Open = canOpen, Close = canClose });
        }
        state.Position += scanned;
        return true;
    }

    private static void ApplyStrikethrough(InlineState state, List<Delimiter> delimiters)
    {
        var loneMarkers = new Stack<int>();
        foreach (var start in delimiters)
        {
            if (start.Marker != '~' || start.End == -1)
            {
                continue;
            }
            var end = delimiters[start.End];
            MakeElement(state.Tokens[start.Token], "s", 1);
            MakeElement(state.Tokens[end.Token], "s", -1);
            if (state.Tokens[end.Token - 1].Kind == TokenKind.Text && state.Tokens[end.Token - 1].Content == "~")
            {
                loneMarkers.Push(end.Token - 1);
            }
        }
        while (loneMarkers.TryPop(out var i))
        {
            var j = i + 1;
            while (j < state.Tokens.Count && state.Tokens[j] is { Kind: TokenKind.Element, Tag: "s", Nesting: -1 })
            {
                j++;
            }
            j--;
            (state.Tokens[j], state.Tokens[i]) = (state.Tokens[i], state.Tokens[j]);
        }
    }

    private static void MakeElement(Token token, string tag, int nesting)
    {
        token.Kind = TokenKind.Element;
        token.Tag = tag;
        token.Nesting = nesting;
        token.Content = string.Empty;
    }

    private static bool Emphasis(InlineState state, bool silent)
    {
        var marker = state.At(state.Position);
        if (silent || marker is not ('_' or '*'))
        {
            return false;
        }
        var (canOpen, canClose, length) = state.ScanDelimiters(state.Position, marker == '*');
        for (var i = 0; i < length; i++)
        {
            state.Push(TokenKind.Text, string.Empty, 0).Content = ((char)marker).ToString();
            state.Delimiters.Add(new Delimiter { Marker = marker, Length = length, Token = state.Tokens.Count - 1, Open = canOpen, Close = canClose });
        }
        state.Position += length;
        return true;
    }

    private static void ApplyEmphasis(InlineState state, List<Delimiter> delimiters)
    {
        for (var i = delimiters.Count - 1; i >= 0; i--)
        {
            var start = delimiters[i];
            if (start.Marker is not ('_' or '*') || start.End == -1)
            {
                continue;
            }
            var end = delimiters[start.End];
            var isStrong = i > 0
                && delimiters[i - 1].End == start.End + 1
                && delimiters[i - 1].Marker == start.Marker
                && delimiters[i - 1].Token == start.Token - 1
                && delimiters[start.End + 1].Token == end.Token + 1;
            var tag = isStrong ? "strong" : "em";
            MakeElement(state.Tokens[start.Token], tag, 1);
            MakeElement(state.Tokens[end.Token], tag, -1);
            if (isStrong)
            {
                state.Tokens[delimiters[i - 1].Token].Content = string.Empty;
                state.Tokens[delimiters[start.End + 1].Token].Content = string.Empty;
                i--;
            }
        }
    }

    private static bool Entity(InlineState state, bool silent)
    {
        var pos = state.Position;
        if (state.At(pos) != '&' || pos + 1 >= state.PositionMax)
        {
            return false;
        }
        string decoded;
        Match match;
        if (state.At(pos + 1) == '#')
        {
            match = DigitalEntity().Match(state.Source, pos);
            if (!match.Success)
            {
                return false;
            }
            var value = match.Groups[1].Value;
            var code = value[0] is 'x' or 'X'
                ? int.Parse(value.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture)
                : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            decoded = Entities.IsValidCode(code) ? char.ConvertFromUtf32(code) : "\uFFFD";
        }
        else
        {
            match = NamedEntity().Match(state.Source, pos);
            if (!match.Success || Entities.Decode(match.Groups[1].Value) is not { } named)
            {
                return false;
            }
            decoded = named;
        }
        if (!silent)
        {
            state.Push(TokenKind.TextSpecial, string.Empty, 0).Content = decoded;
        }
        state.Position += match.Length;
        return true;
    }

    [GeneratedRegex(@"\G&#((?:x[a-f0-9]{1,6}|[0-9]{1,7}));", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DigitalEntity();

    [GeneratedRegex(@"\G&([a-z][a-z0-9]{1,31});", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NamedEntity();
}
