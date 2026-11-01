using System.Text.RegularExpressions;
using KyfRev.Rendering.Markdown;

namespace KyfRev.Rendering;

/// <summary>Le rendu du corps d'une fiche : du Markdown à l'arbre de contenu dont on tire le texte affiché.</summary>
public static partial class SheetRenderer
{
    private const string JsSpace = @"[\f\n\r\t\v \u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF]";

    /// <summary>Le contenu d'une fiche.</summary>
    /// <param name="body">Le corps de la fiche, en Markdown.</param>
    /// <param name="title">Son titre : un premier titre qui le répète est marqué, car il sera masqué.</param>
    public static RenderElement Render(string body, string title)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(title);
        var root = TreeBuilder.Build(MarkdownParser.Parse(NormalizeLinks(body)));
        if (root.Children.OfType<RenderElement>().FirstOrDefault() is { Tag: "h1" } first
            && JsText.Trim(first.TextContent()) == JsText.Trim(title))
        {
            first.IsRepeatedTitle = true;
        }
        return root;
    }

    /// <summary>
    /// Deux écritures courantes que CommonMark refuse, rendues lisibles : un chemin avec des espaces sans
    /// chevrons (<c>![a](mon image.png)</c>) et l'intégration d'Obsidian (<c>![[mon image.png]]</c>). Le
    /// fichier n'est pas modifié ; les blocs et les extraits de code ne sont pas touchés.
    /// </summary>
    internal static string NormalizeLinks(string body)
    {
        var fence = (char?)null;
        var lines = body.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var marker = FenceLine().Match(lines[i]);
            if (marker.Success)
            {
                var character = marker.Groups[1].Value[0];
                if (fence is null)
                {
                    fence = character;
                }
                else if (fence == character)
                {
                    fence = null;
                }
            }
            else if (fence is null)
            {
                lines[i] = NormalizeOutsideCode(lines[i]);
            }
        }
        return string.Join('\n', lines);
    }

    private static string NormalizeOutsideCode(string line)
    {
        var parts = CodeSpan().Split(line);
        for (var i = 0; i < parts.Length; i += 2)
        {
            parts[i] = NormalizeSpacedLinks(EmbeddedFile().Replace(parts[i], ReplaceEmbeddedFile));
        }
        return string.Concat(parts);
    }

    private static string ReplaceEmbeddedFile(Match match)
    {
        var name = JsText.Trim(match.Groups[1].Value);
        return $"![{Extension().Replace(name, string.Empty)}](<{name}>)";
    }

    private static string NormalizeSpacedLinks(string text) => SpacedLink().Replace(text, match =>
    {
        var destination = JsText.Trim(match.Groups[2].Value);
        return destination.Any(JsText.IsSpace) ? $"{match.Groups[1].Value}(<{destination}>)" : match.Value;
    });

    [GeneratedRegex(@"\A" + JsSpace + "{0,3}(`{3,}|~{3,})", RegexOptions.CultureInvariant)]
    private static partial Regex FenceLine();

    [GeneratedRegex("(`+[^`]*`+)", RegexOptions.CultureInvariant)]
    private static partial Regex CodeSpan();

    [GeneratedRegex(@"!\[\[([^\]|\n]+?)(?:\|[^\]\n]*)?\]\]", RegexOptions.CultureInvariant)]
    private static partial Regex EmbeddedFile();

    [GeneratedRegex(@"\.[^./]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex Extension();

    [GeneratedRegex(@"(!?\[[^\]\n]*\])\(([^()<>""'\n]*" + JsSpace + @"[^()<>""'\n]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex SpacedLink();
}
