using System.Text.RegularExpressions;

namespace KyfRev.Rendering.Markdown;

/// <summary>Les liens, les images, les notes et les balises permises.</summary>
internal static partial class InlineParser
{
    private const string SpanName = "span";

    private static readonly string[] AllowedTagNames = ["u", "sup", "sub", SpanName];

    private static bool IsAsciiLetter(int code) => code is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

    private static bool IsSchemeCharacter(int code) => IsAsciiLetter(code) || code is (>= '0' and <= '9') or '+' or '-' or '.';

    private static bool Linkify(InlineState state, bool silent)
    {
        if (state.LinkLevel > 0)
        {
            return false;
        }
        var pos = state.Position;
        if (pos + 3 > state.PositionMax || state.At(pos) != ':' || state.At(pos + 1) != '/' || state.At(pos + 2) != '/')
        {
            return false;
        }
        var protocolMinimum = pos - Math.Min(10, Math.Min(state.Pending.Length, pos));
        var protocolStart = pos;
        while (protocolStart > protocolMinimum && IsSchemeCharacter(state.At(protocolStart - 1)))
        {
            protocolStart--;
        }
        if (protocolStart == pos || !IsAsciiLetter(state.At(protocolStart)))
        {
            return false;
        }
        var protocolLength = pos - protocolStart;
        if (Linkifier.MatchAtStart(state.Source[protocolStart..]) is not { } link)
        {
            return false;
        }
        var url = link.Url;
        if (url.Length <= protocolLength)
        {
            return false;
        }
        url = url.TrimEnd('*');
        var fullUrl = Url.NormalizeLink(url);
        if (!Url.IsValidLink(fullUrl))
        {
            return false;
        }
        if (!silent)
        {
            state.Pending.Length -= protocolLength;
            PushAutomaticLink(state, fullUrl, Url.NormalizeLinkText(url));
        }
        state.Position += url.Length - protocolLength;
        return true;
    }

    private static void PushAutomaticLink(InlineState state, string href, string text)
    {
        state.Push(TokenKind.LinkOpen, "a", 1).Href = href;
        state.Push(TokenKind.Text, string.Empty, 0).Content = text;
        state.Push(TokenKind.LinkClose, "a", -1);
    }

    /// <summary>Saute les espaces et les sauts de ligne, à l'intérieur des parenthèses d'un lien.</summary>
    private static int SkipLinkSpaces(InlineState state, int pos)
    {
        while (pos < state.PositionMax && (BlockState.IsSpace(state.At(pos)) || state.At(pos) == '\n'))
        {
            pos++;
        }
        return pos;
    }

    /// <summary>
    /// Lit le libellé de référence qui suit un texte de lien ; rend la position qui le suit, et ce libellé,
    /// ou rien s'il faut reprendre le texte du lien comme référence.
    /// </summary>
    private static (int Position, string? Label) ReferenceLabel(InlineState state, int pos, int labelEnd)
    {
        if (pos < state.PositionMax && state.At(pos) == '[')
        {
            var start = pos + 1;
            pos = LinkScanner.ParseLabel(state, pos, false);
            return pos >= 0 ? (pos + 1, state.Source[start..pos]) : (labelEnd + 1, null);
        }
        return (labelEnd + 1, null);
    }

    private static bool Link(InlineState state, bool silent)
    {
        var href = string.Empty;
        if (state.At(state.Position) != '[')
        {
            return false;
        }
        var oldPosition = state.Position;
        var max = state.PositionMax;
        var labelStart = state.Position + 1;
        var labelEnd = LinkScanner.ParseLabel(state, state.Position, true);
        if (labelEnd < 0)
        {
            return false;
        }
        var pos = labelEnd + 1;
        var parseReference = true;
        if (pos < max && state.At(pos) == '(')
        {
            parseReference = false;
            pos = SkipLinkSpaces(state, pos + 1);
            if (pos >= max)
            {
                return false;
            }
            var destination = LinkScanner.ParseDestination(state.Source, pos, state.PositionMax);
            if (destination.Ok)
            {
                href = Url.NormalizeLink(destination.Text);
                if (Url.IsValidLink(href))
                {
                    pos = destination.Position;
                }
                else
                {
                    href = string.Empty;
                }
                var start = pos;
                pos = SkipLinkSpaces(state, pos);
                var title = LinkScanner.ParseTitle(state.Source, pos, state.PositionMax, null);
                if (pos < max && start != pos && title.Ok)
                {
                    pos = SkipLinkSpaces(state, title.Position);
                }
            }
            if (pos >= max || state.At(pos) != ')')
            {
                parseReference = true;
            }
            pos++;
        }
        if (parseReference)
        {
            if (state.Environment.References.Count == 0)
            {
                return false;
            }
            (pos, var label) = ReferenceLabel(state, pos, labelEnd);
            if (string.IsNullOrEmpty(label))
            {
                label = state.Source[labelStart..labelEnd];
            }
            if (!state.Environment.References.TryGetValue(LinkScanner.NormalizeReference(label), out var reference))
            {
                state.Position = oldPosition;
                return false;
            }
            href = reference.Href;
        }
        if (!silent)
        {
            state.Position = labelStart;
            state.PositionMax = labelEnd;
            state.Push(TokenKind.LinkOpen, "a", 1).Href = href;
            state.LinkLevel++;
            Tokenize(state);
            state.LinkLevel--;
            state.Push(TokenKind.LinkClose, "a", -1);
        }
        state.Position = pos;
        state.PositionMax = max;
        return true;
    }

    private static bool Image(InlineState state, bool silent)
    {
        var href = string.Empty;
        string? imageTitle = null;
        var oldPosition = state.Position;
        var max = state.PositionMax;
        if (state.At(state.Position) != '!' || state.At(state.Position + 1) != '[')
        {
            return false;
        }
        var labelStart = state.Position + 2;
        var labelEnd = LinkScanner.ParseLabel(state, state.Position + 1, false);
        if (labelEnd < 0)
        {
            return false;
        }
        var pos = labelEnd + 1;
        if (pos < max && state.At(pos) == '(')
        {
            pos = SkipLinkSpaces(state, pos + 1);
            if (pos >= max)
            {
                return false;
            }
            var destination = LinkScanner.ParseDestination(state.Source, pos, state.PositionMax);
            if (destination.Ok)
            {
                href = Url.NormalizeLink(destination.Text);
                if (Url.IsValidLink(href))
                {
                    pos = destination.Position;
                }
                else
                {
                    href = string.Empty;
                }
            }
            var start = pos;
            pos = SkipLinkSpaces(state, pos);
            var title = LinkScanner.ParseTitle(state.Source, pos, state.PositionMax, null);
            if (pos < max && start != pos && title.Ok)
            {
                imageTitle = title.Text;
                pos = SkipLinkSpaces(state, title.Position);
            }
            if (pos >= max || state.At(pos) != ')')
            {
                state.Position = oldPosition;
                return false;
            }
            pos++;
        }
        else
        {
            if (state.Environment.References.Count == 0)
            {
                return false;
            }
            (pos, var label) = ReferenceLabel(state, pos, labelEnd);
            if (string.IsNullOrEmpty(label))
            {
                label = state.Source[labelStart..labelEnd];
            }
            if (!state.Environment.References.TryGetValue(LinkScanner.NormalizeReference(label), out var reference))
            {
                state.Position = oldPosition;
                return false;
            }
            href = reference.Href;
        }
        if (!silent)
        {
            var content = state.Source[labelStart..labelEnd];
            var children = new List<Token>();
            Parse(content, state.Environment, children);
            var token = state.Push(TokenKind.Image, "img", 0);
            token.Href = href;
            token.Title = imageTitle;
            token.Children = children;
            token.Content = content;
        }
        state.Position = pos;
        state.PositionMax = max;
        return true;
    }

    private static bool FootnoteInline(InlineState state, bool silent)
    {
        var max = state.PositionMax;
        var start = state.Position;
        if (start + 2 >= max || state.At(start) != '^' || state.At(start + 1) != '[')
        {
            return false;
        }
        var labelStart = start + 2;
        var labelEnd = LinkScanner.ParseLabel(state, start + 1, false);
        if (labelEnd < 0)
        {
            return false;
        }
        if (!silent)
        {
            var footnotes = state.Environment.Footnotes;
            var footnoteId = footnotes.Count;
            var tokens = new List<Token>();
            Parse(state.Source[labelStart..labelEnd], state.Environment, tokens);
            state.Push(TokenKind.FootnoteRef, string.Empty, 0).FootnoteId = footnoteId;
            // Une note écrite dans une autre prend sa place : son numéro a été choisi avant de lire son contenu.
            var entry = new FootnoteEntry { Tokens = tokens };
            if (footnoteId < footnotes.Count)
            {
                footnotes[footnoteId] = entry;
            }
            else
            {
                footnotes.Add(entry);
            }
        }
        state.Position = labelEnd + 1;
        state.PositionMax = max;
        return true;
    }

    private static bool FootnoteReference(InlineState state, bool silent)
    {
        var max = state.PositionMax;
        var start = state.Position;
        if (start + 3 > max || state.Environment.FootnoteReferences.Count == 0 || state.At(start) != '[' || state.At(start + 1) != '^')
        {
            return false;
        }
        int pos;
        for (pos = start + 2; pos < max; pos++)
        {
            var ch = state.At(pos);
            if (ch is ' ' or '\n')
            {
                return false;
            }
            if (ch == ']')
            {
                break;
            }
        }
        if (pos == start + 2 || pos >= max)
        {
            return false;
        }
        pos++;
        var label = state.Source[(start + 2)..(pos - 1)];
        if (!state.Environment.FootnoteReferences.TryGetValue(label, out var footnoteId))
        {
            return false;
        }
        if (!silent)
        {
            var footnotes = state.Environment.Footnotes;
            if (footnoteId < 0)
            {
                footnoteId = footnotes.Count;
                footnotes.Add(new FootnoteEntry { Label = label });
                state.Environment.FootnoteReferences[label] = footnoteId;
            }
            var token = state.Push(TokenKind.FootnoteRef, string.Empty, 0);
            token.FootnoteId = footnoteId;
            token.FootnoteSubId = footnotes[footnoteId].Count;
            footnotes[footnoteId].Count++;
        }
        state.Position = pos;
        state.PositionMax = max;
        return true;
    }

    /// <summary>
    /// <c>&lt;u&gt;</c>, <c>&lt;sup&gt;</c> et <c>&lt;sub&gt;</c>, sans attribut, et <c>&lt;span&gt;</c> avec exactement
    /// une couleur ou une taille : le reste du HTML reste du texte. Une balise ouvrante n'est retenue que si sa fermante
    /// suit, une fermante que si elle ferme quelque chose.
    /// </summary>
    private static bool AllowedTag(InlineState state, bool silent)
    {
        var start = state.Position;
        if (state.At(start) != '<')
        {
            return false;
        }
        var styled = StyledSpan().Match(state.Source, start);
        if (styled.Success)
        {
            if (state.Source.IndexOf("</span>", start + styled.Length, StringComparison.Ordinal) < 0)
            {
                return false;
            }
            if (!silent)
            {
                state.Push(TokenKind.Element, SpanName, 1).Style = styled.Groups[1].Value;
                state.OpenTags[SpanName] = state.OpenTags.GetValueOrDefault(SpanName) + 1;
            }
            state.Position += styled.Length;
            return true;
        }
        var closing = state.At(start + 1) == '/';
        var nameStart = start + (closing ? 2 : 1);
        var name = Array.Find(AllowedTagNames, candidate =>
            string.CompareOrdinal(state.Source, nameStart, candidate, 0, candidate.Length) == 0 && state.At(nameStart + candidate.Length) == '>');
        if (name is null || (name == SpanName && !closing))
        {
            return false;
        }
        var length = nameStart + name.Length + 1 - start;
        var open = state.OpenTags.GetValueOrDefault(name);
        if (closing)
        {
            if (open == 0)
            {
                return false;
            }
        }
        else if (state.Source.IndexOf($"</{name}>", start + length, StringComparison.Ordinal) < 0)
        {
            return false;
        }
        if (!silent)
        {
            state.Push(TokenKind.Element, name, closing ? -1 : 1);
            state.OpenTags[name] = open + (closing ? -1 : 1);
        }
        state.Position += length;
        return true;
    }

    /// <summary>Une couleur à six chiffres ; une taille de 0,5 à 3 fois le texte, au centième.</summary>
    [GeneratedRegex(@"\G<span style=""(color:#[0-9a-fA-F]{6}|font-size:(?:0\.[5-9][0-9]?|[12](?:\.[0-9]{1,2})?|3(?:\.0{1,2})?)em)"">")]
    private static partial Regex StyledSpan();

    private static bool Autolink(InlineState state, bool silent)
    {
        var pos = state.Position;
        if (state.At(pos) != '<')
        {
            return false;
        }
        var start = pos;
        while (true)
        {
            if (++pos >= state.PositionMax)
            {
                return false;
            }
            var ch = state.At(pos);
            if (ch == '<')
            {
                return false;
            }
            if (ch == '>')
            {
                break;
            }
        }
        var url = state.Source[(start + 1)..pos];
        string fullUrl;
        if (AutolinkAddress().IsMatch(url))
        {
            fullUrl = Url.NormalizeLink(url);
        }
        else if (EmailAddress().IsMatch(url))
        {
            fullUrl = Url.NormalizeLink("mailto:" + url);
        }
        else
        {
            return false;
        }
        if (!Url.IsValidLink(fullUrl))
        {
            return false;
        }
        if (!silent)
        {
            PushAutomaticLink(state, fullUrl, Url.NormalizeLinkText(url));
        }
        state.Position += url.Length + 2;
        return true;
    }

    [GeneratedRegex(@"\A[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex EmailAddress();

    [GeneratedRegex(@"\A[a-zA-Z][a-zA-Z0-9+.-]{1,31}:[^<>\x00-\x20]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex AutolinkAddress();
}
