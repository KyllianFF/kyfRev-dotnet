using System.Text.RegularExpressions;

namespace KyfRev.Rendering.Markdown;

/// <summary>La lecture des morceaux d'un lien : son texte, sa destination, son titre et son libellé de référence.</summary>
internal static partial class LinkScanner
{
    /// <summary>La fin du texte d'un lien, ou -1. Le texte peut contenir d'autres éléments, qui sont sautés.</summary>
    public static int ParseLabel(InlineState state, int start, bool disableNested)
    {
        var oldPosition = state.Position;
        state.Position = start + 1;
        var level = 1;
        var found = false;
        while (state.Position < state.PositionMax)
        {
            var marker = state.At(state.Position);
            if (marker == ']')
            {
                level--;
                if (level == 0)
                {
                    found = true;
                    break;
                }
            }
            var previous = state.Position;
            InlineParser.SkipToken(state);
            if (marker == '[')
            {
                if (previous == state.Position - 1)
                {
                    level++;
                }
                else if (disableNested)
                {
                    state.Position = oldPosition;
                    return -1;
                }
            }
        }
        var labelEnd = found ? state.Position : -1;
        state.Position = oldPosition;
        return labelEnd;
    }

    public static (bool Ok, int Position, string Text) ParseDestination(string source, int start, int max)
    {
        var pos = start;
        if (pos < source.Length && source[pos] == '<')
        {
            pos++;
            while (pos < max)
            {
                var ch = source[pos];
                if (ch is '\n' or '<')
                {
                    return default;
                }
                if (ch == '>')
                {
                    return (true, pos + 1, UnescapeAll(source[(start + 1)..pos]));
                }
                pos += ch == '\\' && pos + 1 < max ? 2 : 1;
            }
            return default;
        }
        var level = 0;
        while (pos < max)
        {
            var ch = source[pos];
            if (ch is <= ' ' or '\u007F')
            {
                break;
            }
            if (ch == '\\' && pos + 1 < max)
            {
                pos += source[pos + 1] == ' ' ? 1 : 2;
                continue;
            }
            if (ch == '(')
            {
                level++;
                if (level > 32)
                {
                    return default;
                }
            }
            if (ch == ')')
            {
                if (level == 0)
                {
                    break;
                }
                level--;
            }
            pos++;
        }
        return start == pos || level != 0 ? default : (true, pos, UnescapeAll(source[start..pos]));
    }

    public static LinkTitle ParseTitle(string source, int start, int max, LinkTitle? previous)
    {
        var pos = start;
        var text = string.Empty;
        int marker;
        if (previous is not null)
        {
            text = previous.Text;
            marker = previous.Marker;
        }
        else
        {
            if (pos >= max)
            {
                return new LinkTitle();
            }
            marker = source[pos];
            if (marker is not ('"' or '\'' or '('))
            {
                return new LinkTitle();
            }
            start++;
            pos++;
            if (marker == '(')
            {
                marker = ')';
            }
        }
        while (pos < max)
        {
            var ch = source[pos];
            if (ch == marker)
            {
                return new LinkTitle { Ok = true, Position = pos + 1, Text = text + UnescapeAll(source[start..pos]), Marker = marker };
            }
            if (ch == '(' && marker == ')')
            {
                return new LinkTitle { Text = text, Marker = marker };
            }
            if (ch == '\\' && pos + 1 < max)
            {
                pos++;
            }
            pos++;
        }
        return new LinkTitle { CanContinue = true, Text = text + UnescapeAll(source[start..pos]), Marker = marker };
    }

    /// <summary>Le libellé d'une référence : ni la casse ni les espaces n'y comptent.</summary>
    public static string NormalizeReference(string label)
    {
        var compact = JsSpaces().Replace(JsText.Trim(label), " ");
        var lower = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToLower(compact);
        return lower.Replace("ß", "SS", StringComparison.Ordinal).ToUpperInvariant();
    }

    /// <summary>Résout les échappements et les entités d'une destination, d'un titre ou d'une langue.</summary>
    public static string UnescapeAll(string text)
    {
        if (!text.Contains('\\', StringComparison.Ordinal) && !text.Contains('&', StringComparison.Ordinal))
        {
            return text;
        }
        return EscapeOrEntity().Replace(text, match =>
            match.Groups[1].Success ? match.Groups[1].Value : Entities.DecodeReference(match.Value, match.Groups[2].Value));
    }

    [GeneratedRegex(@"\\([!""#$%&'()*+,\-./:;<=>?@[\\\]^_`{|}~])|&([a-z#][a-z0-9]{1,31});", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EscapeOrEntity();

    [GeneratedRegex(@"[\f\n\r\t\v \u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000\ufeff]+", RegexOptions.CultureInvariant)]
    private static partial Regex JsSpaces();
}

/// <summary>Le titre d'un lien, lu en une ou plusieurs lignes.</summary>
internal sealed record LinkTitle
{
    public bool Ok { get; init; }

    public bool CanContinue { get; init; }

    public int Position { get; init; }

    public string Text { get; init; } = string.Empty;

    public int Marker { get; init; }
}
