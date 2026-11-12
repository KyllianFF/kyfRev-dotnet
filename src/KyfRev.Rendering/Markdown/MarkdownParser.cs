using System.Text.RegularExpressions;

namespace KyfRev.Rendering.Markdown;

/// <summary>
/// L'analyse d'un document : la suite de markdown-it, réglée comme la 0.x (<c>html: false</c>,
/// <c>linkify: true</c>, <c>typographer: false</c>), avec ses extensions. Une autre analyse ferait glisser
/// les positions des passages déjà enregistrés : c'est pourquoi elle est reprise règle par règle.
/// </summary>
internal static partial class MarkdownParser
{
    public static List<Token> Parse(string source)
    {
        var text = LineBreaks().Replace(source, "\n").Replace('\0', '\uFFFD');
        var environment = new ParseEnvironment();
        var tokens = new List<Token>();
        BlockParser.Parse(text, environment, tokens);
        tokens.RemoveAll(token => token.Kind == TokenKind.ReferenceDefinition);
        AttachTableLooks(tokens);
        ReadPageMarks(tokens);
        foreach (var token in tokens.Where(token => token.Kind == TokenKind.Inline))
        {
            InlineParser.Parse(token.Content, environment, token.Children);
        }
        tokens = AddFootnotes(tokens, environment);
        foreach (var token in tokens.Where(token => token.Kind == TokenKind.Inline))
        {
            if (Linkifier.Test(token.Content))
            {
                LinkifyText(token);
            }
            JoinText(token.Children);
            foreach (var child in token.Children.Where(child => child.Kind == TokenKind.Image))
            {
                JoinText(child.Children);
            }
        }
        return tokens;
    }

    /// <summary>
    /// La ligne d'apparence posée juste au-dessus d'un tableau quitte le texte et va au tableau : elle n'est ni affichée ni
    /// comptée dans les positions. Ailleurs, la même ligne reste un paragraphe comme un autre.
    /// </summary>
    private static void AttachTableLooks(List<Token> tokens)
    {
        for (var i = tokens.Count - 4; i >= 0; i--)
        {
            if (tokens[i].Kind == TokenKind.ParagraphOpen
                && tokens[i + 1].Kind == TokenKind.Inline
                && tokens[i + 2].Kind == TokenKind.ParagraphClose
                && tokens[i + 3] is { Kind: TokenKind.Element, Tag: "table", Nesting: 1 } table
                && table.Level == tokens[i].Level
                && TableLook.FromLine(tokens[i + 1].Content) is { } look)
            {
                table.Look = look;
                tokens.RemoveRange(i, 3);
            }
        }
    }

    /// <summary>
    /// Une ligne de saut de page ou de page blanche, seule dans son paragraphe et au premier niveau de la fiche, quitte le
    /// texte : il en reste un filet qui porte la demande, que la mise en pages lit et que rien d'autre n'affiche comme tel.
    /// </summary>
    private static void ReadPageMarks(List<Token> tokens)
    {
        for (var i = tokens.Count - 3; i >= 0; i--)
        {
            if (tokens[i] is { Kind: TokenKind.ParagraphOpen, Level: 0 } open
                && tokens[i + 1].Kind == TokenKind.Inline
                && tokens[i + 2].Kind == TokenKind.ParagraphClose
                && PageMarks.FromLine(tokens[i + 1].Content) is var mark and not PageMark.None)
            {
                tokens[i] = new Token(TokenKind.Element, "hr", 0) { Block = true, Level = 0, Line = open.Line, PageMark = mark };
                tokens.RemoveRange(i + 1, 2);
            }
        }
    }

    /// <summary>Range les définitions de notes appelées à la fin du document, dans l'ordre de leur premier appel.</summary>
    private static List<Token> AddFootnotes(List<Token> tokens, ParseEnvironment environment)
    {
        var definitions = new Dictionary<string, List<Token>>(StringComparer.Ordinal);
        var result = new List<Token>(tokens.Count);
        List<Token>? current = null;
        string? currentLabel = null;
        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case TokenKind.FootnoteReferenceOpen:
                    current = [];
                    currentLabel = token.Label;
                    break;
                case TokenKind.FootnoteReferenceClose:
                    definitions[currentLabel!] = current!;
                    current = null;
                    break;
                default:
                    (current ?? result).Add(token);
                    break;
            }
        }
        if (environment.Footnotes.Count == 0)
        {
            return result;
        }
        result.Add(new Token(TokenKind.FootnoteBlockOpen, string.Empty, 1));
        List<Token>? content = null;
        for (var i = 0; i < environment.Footnotes.Count; i++)
        {
            var footnote = environment.Footnotes[i];
            result.Add(new Token(TokenKind.FootnoteOpen, string.Empty, 1) { FootnoteId = i });
            if (footnote.Tokens is not null)
            {
                content =
                [
                    new Token(TokenKind.ParagraphOpen, "p", 1) { Block = true },
                    new Token(TokenKind.Inline, string.Empty, 0) { Children = footnote.Tokens },
                    new Token(TokenKind.ParagraphClose, "p", -1) { Block = true },
                ];
            }
            else
            {
                content = definitions.GetValueOrDefault(footnote.Label!);
            }
            if (content is not null)
            {
                result.AddRange(content);
            }
            Token? lastParagraph = null;
            if (result[^1].Kind == TokenKind.ParagraphClose)
            {
                lastParagraph = result[^1];
                result.RemoveAt(result.Count - 1);
            }
            for (var j = 0; j < Math.Max(footnote.Count, 1); j++)
            {
                result.Add(new Token(TokenKind.FootnoteAnchor, string.Empty, 0) { FootnoteId = i, FootnoteSubId = j });
            }
            if (lastParagraph is not null)
            {
                result.Add(lastParagraph);
            }
            result.Add(new Token(TokenKind.FootnoteClose, string.Empty, -1));
        }
        result.Add(new Token(TokenKind.FootnoteBlockClose, string.Empty, -1));
        return result;
    }

    /// <summary>Remplace par des liens les adresses écrites dans les textes d'un paragraphe, hors des liens existants.</summary>
    private static void LinkifyText(Token inline)
    {
        var tokens = inline.Children;
        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            var current = tokens[i];
            if (current.Kind == TokenKind.LinkClose)
            {
                i--;
                while (tokens[i].Level != current.Level && tokens[i].Kind != TokenKind.LinkOpen)
                {
                    i--;
                }
                continue;
            }
            if (current.Kind != TokenKind.Text || !Linkifier.Test(current.Content))
            {
                continue;
            }
            var links = Linkifier.Match(current.Content);
            if (links.Count > 0 && links[0].Index == 0 && i > 0 && tokens[i - 1].Kind == TokenKind.TextSpecial)
            {
                links.RemoveAt(0);
            }
            var nodes = BuildLinks(current, links);
            tokens.RemoveAt(i);
            tokens.InsertRange(i, nodes);
        }
    }

    private static List<Token> BuildLinks(Token source, List<LinkMatch> links)
    {
        var text = source.Content;
        var nodes = new List<Token>();
        var level = source.Level;
        var lastPosition = 0;
        foreach (var link in links)
        {
            var fullUrl = Url.NormalizeLink(link.Url);
            if (!Url.IsValidLink(fullUrl))
            {
                continue;
            }
            var linkText = link.Schema == "mailto:" && !link.Text.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                ? Url.NormalizeLinkText("mailto:" + link.Text)["mailto:".Length..]
                : Url.NormalizeLinkText(link.Text);
            if (link.Index > lastPosition)
            {
                nodes.Add(new Token(TokenKind.Text, string.Empty, 0) { Content = text[lastPosition..link.Index], Level = level });
            }
            nodes.Add(new Token(TokenKind.LinkOpen, "a", 1) { Href = fullUrl, Level = level });
            nodes.Add(new Token(TokenKind.Text, string.Empty, 0) { Content = linkText, Level = level + 1 });
            nodes.Add(new Token(TokenKind.LinkClose, "a", -1) { Level = level });
            lastPosition = link.LastIndex;
        }
        if (lastPosition < text.Length)
        {
            nodes.Add(new Token(TokenKind.Text, string.Empty, 0) { Content = text[lastPosition..], Level = level });
        }
        return nodes;
    }

    /// <summary>Les échappements et les entités redeviennent du texte, joint à ses voisins.</summary>
    private static void JoinText(List<Token> tokens)
    {
        var last = 0;
        for (var current = 0; current < tokens.Count; current++)
        {
            if (tokens[current].Kind == TokenKind.TextSpecial)
            {
                tokens[current].Kind = TokenKind.Text;
            }
            if (current + 1 < tokens.Count && tokens[current].Kind == TokenKind.Text && tokens[current + 1].Kind is TokenKind.Text or TokenKind.TextSpecial)
            {
                tokens[current + 1].Kind = TokenKind.Text;
                tokens[current + 1].Content = tokens[current].Content + tokens[current + 1].Content;
            }
            else
            {
                tokens[last] = tokens[current];
                last++;
            }
        }
        tokens.RemoveRange(last, tokens.Count - last);
    }

    [GeneratedRegex(@"\r\n?", RegexOptions.CultureInvariant)]
    private static partial Regex LineBreaks();
}
