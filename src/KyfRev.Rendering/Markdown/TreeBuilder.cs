using System.Globalization;

namespace KyfRev.Rendering.Markdown;

/// <summary>
/// Écrit les jetons sous forme d'arbre, avec les retours à la ligne que le rendu de markdown-it met dans son
/// HTML : le navigateur en faisait des nœuds de texte, qui comptent dans les positions de la 0.x.
/// </summary>
internal sealed class TreeBuilder
{
    private const string BackReference = "\u21A9\uFE0E";

    /// <summary>
    /// Profondeur maximale, comme dans le navigateur de la 0.x : au-delà, un élément devient le voisin de
    /// son parent. Le texte n'en dépend pas, et une fiche piégée ne peut pas faire déborder la pile.
    /// </summary>
    private const int MaxDepth = 512;

    private readonly RenderElement _root = new("article");
    private RenderElement _current;

    private TreeBuilder()
    {
        _current = _root;
    }

    public static RenderElement Build(List<Token> tokens)
    {
        var builder = new TreeBuilder();
        builder.WriteTokens(tokens);
        return builder._root;
    }

    private static string FootnoteNumber(Token token) => (token.FootnoteId + 1).ToString(CultureInfo.InvariantCulture);

    private static string FootnoteSuffix(Token token) =>
        token.FootnoteSubId > 0 ? ":" + token.FootnoteSubId.ToString(CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>Le texte de remplacement d'une image.</summary>
    private static string AlternativeText(List<Token> tokens) => string.Concat(tokens.Select(token => token.Kind switch
    {
        TokenKind.Text or TokenKind.CodeInline => token.Content,
        TokenKind.Image => AlternativeText(token.Children),
        TokenKind.Softbreak or TokenKind.Hardbreak => "\n",
        _ => string.Empty,
    }));

    /// <summary>La langue d'un bloc de code : le premier mot de ce qui suit sa clôture d'ouverture.</summary>
    private static string? FenceLanguage(string info)
    {
        var text = JsText.Trim(LinkScanner.UnescapeAll(info));
        if (text.Length == 0)
        {
            return null;
        }
        var end = 0;
        while (end < text.Length && !JsText.IsSpace(text[end]))
        {
            end++;
        }
        return text[..end];
    }

    private void WriteTokens(List<Token> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind == TokenKind.Inline)
            {
                WriteTokens(token.Children);
            }
            else
            {
                WriteToken(tokens, i);
            }
        }
    }

    private void WriteToken(List<Token> tokens, int index)
    {
        var token = tokens[index];
        switch (token.Kind)
        {
            case TokenKind.Text:
                WriteText(token.Content);
                break;
            case TokenKind.Softbreak:
                WriteText("\n");
                break;
            case TokenKind.Hardbreak:
                Leaf("br");
                WriteText("\n");
                break;
            case TokenKind.CodeInline:
                Open("code");
                WriteText(token.Content);
                Close("code");
                break;
            case TokenKind.CodeBlock:
                WriteCodeBlock(token.Content, null);
                break;
            case TokenKind.Fence:
                WriteCodeBlock(token.Content, FenceLanguage(token.Info));
                break;
            case TokenKind.MathInline:
                Leaf("span").Formula = JsText.Trim(token.Content);
                break;
            case TokenKind.MathBlock:
                var block = Leaf("div");
                block.Formula = JsText.Trim(token.Content);
                block.IsFormulaBlock = true;
                break;
            case TokenKind.Image:
                var image = Leaf("img");
                image.Target = token.Href;
                image.AlternativeText = AlternativeText(token.Children);
                break;
            case TokenKind.FootnoteRef:
                WriteFootnoteReference(token);
                break;
            case TokenKind.FootnoteAnchor:
                WriteFootnoteBackReference(token);
                break;
            case TokenKind.FootnoteBlockOpen:
                Leaf("hr");
                WriteText("\n");
                Open("section");
                WriteText("\n");
                Open("ol");
                WriteText("\n");
                break;
            case TokenKind.FootnoteBlockClose:
                Close("ol");
                WriteText("\n");
                Close("section");
                WriteText("\n");
                break;
            case TokenKind.FootnoteOpen:
                Open("li").Identifier = "fn" + FootnoteNumber(token);
                break;
            case TokenKind.FootnoteClose:
                Close("li");
                WriteText("\n");
                break;
            default:
                WriteTag(tokens, index);
                break;
        }
    }

    /// <summary>Une balise, avec les retours à la ligne que <c>renderToken</c> met autour.</summary>
    private void WriteTag(List<Token> tokens, int index)
    {
        var token = tokens[index];
        if (token.Hidden)
        {
            return;
        }
        if (token.Block && token.Nesting != -1 && index > 0 && tokens[index - 1] is { Hidden: true, Nesting: -1 })
        {
            WriteText("\n");
        }
        if (token.Nesting == -1)
        {
            Close(token.Tag);
        }
        else
        {
            var element = token.Nesting == 1 ? Open(token.Tag) : Leaf(token.Tag);
            element.Line = token.Line + 1;
            element.ListStart = token.Start;
            element.Alignment = token.Alignment;
            element.Target = token.Href;
        }
        var lineFeed = token.Block;
        if (token.Block && token.Nesting == 1 && index + 1 < tokens.Count)
        {
            var next = tokens[index + 1];
            lineFeed = !(next.Kind == TokenKind.Inline || next.Hidden || (next.Nesting == -1 && next.Tag == token.Tag));
        }
        if (lineFeed)
        {
            WriteText("\n");
        }
    }

    private void WriteCodeBlock(string content, string? language)
    {
        Open("pre");
        Open("code").Language = language;
        WriteText(content);
        Close("code");
        Close("pre");
        WriteText("\n");
    }

    private void WriteFootnoteReference(Token token)
    {
        var number = FootnoteNumber(token);
        var suffix = FootnoteSuffix(token);
        Open("sup");
        var link = Open("a");
        link.Target = "#fn" + number;
        link.Identifier = "fnref" + number + suffix;
        WriteText("[" + number + suffix + "]");
        Close("a");
        Close("sup");
    }

    private void WriteFootnoteBackReference(Token token)
    {
        WriteText(" ");
        Open("a").Target = "#fnref" + FootnoteNumber(token) + FootnoteSuffix(token);
        WriteText(BackReference);
        Close("a");
    }

    private void WriteText(string text)
    {
        // Le navigateur lit un retour chariot comme un saut de ligne ; une entité peut en écrire un.
        var value = text.Contains('\r', StringComparison.Ordinal)
            ? text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            : text;
        if (value.Length > 0)
        {
            _current.Add(new RenderText(value));
        }
    }

    private RenderElement Open(string tag)
    {
        var element = new RenderElement(tag);
        var parent = _current.Depth < MaxDepth ? _current : _current.Parent!;
        parent.Add(element);
        element.Depth = parent.Depth + 1;
        _current = element;
        return element;
    }

    private RenderElement Leaf(string tag)
    {
        var element = new RenderElement(tag);
        _current.Add(element);
        return element;
    }

    /// <summary>Ferme une balise. Des balises mal imbriquées (<c>&lt;u&gt;*a&lt;/u&gt;*</c>) se ferment avec elle, sans perdre de texte.</summary>
    private void Close(string tag)
    {
        for (var element = _current; element is not null; element = element.Parent)
        {
            if (element.Tag == tag)
            {
                _current = element.Parent ?? _root;
                return;
            }
        }
    }
}
