using System.Collections.Immutable;
using System.Text;

namespace KyfRev.Rendering;

/// <summary>Un morceau du texte affiché : un nœud de texte, ou la source d'une formule.</summary>
/// <param name="Node">Le nœud.</param>
/// <param name="Start">Début, en unités UTF-16.</param>
/// <param name="End">Fin, en unités UTF-16.</param>
public sealed record TextPiece(RenderNode Node, int Start, int End)
{
    /// <summary>Le morceau est une formule.</summary>
    public bool IsFormula => Node is RenderElement;
}

/// <summary>
/// Le texte affiché d'une fiche : ses nœuds de texte, un saut de ligne entre deux blocs, chaque formule
/// remplacée par sa source ; les diagrammes en sont exclus. C'est le <c>carteDu</c> de la 0.x : les
/// positions de tous les passages déjà enregistrés sont comptées sur ce texte.
/// </summary>
public sealed class TextMap
{
    private static readonly FrozenTags Blocks = new(
        "p", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "dl", "dt", "dd", "pre", "blockquote",
        "table", "thead", "tbody", "tr", "td", "th", "figure", "hr", "div", "section", "details", "summary");

    private static readonly FrozenTags Headings = new("h1", "h2", "h3", "h4", "h5", "h6");

    private readonly RenderElement _root;
    private readonly List<TextPiece> _pieces = [];
    private readonly StringBuilder _text = new();

    private TextMap(RenderElement root)
    {
        _root = root;
        Walk(root);
        Text = _text.ToString();
    }

    /// <summary>Le texte affiché.</summary>
    public string Text { get; }

    /// <summary>Ses morceaux, dans l'ordre.</summary>
    public IReadOnlyList<TextPiece> Pieces => _pieces;

    /// <summary>Le texte affiché d'un contenu.</summary>
    public static TextMap Of(RenderElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return new TextMap(root);
    }

    /// <summary>Les titres qui mènent à une position (repère lisible du passage).</summary>
    public ImmutableArray<string> SectionAt(int position)
    {
        var path = new List<(int Level, string Title)>();
        foreach (var heading in Descendants(_root).Where(element => Headings.Contains(element.Tag) && !element.IsRepeatedTitle))
        {
            var first = _pieces.Find(piece => IsWithin(piece.Node, heading));
            if (first is null || first.Start > position)
            {
                break;
            }
            var level = heading.Tag[1] - '0';
            while (path.Count > 0 && path[^1].Level >= level)
            {
                path.RemoveAt(path.Count - 1);
            }
            path.Add((level, JsText.Trim(heading.TextContent())));
        }
        return [.. path.Select(entry => entry.Title)];
    }

    /// <summary>Ligne du fichier où se trouve le bloc d'une position (repère approximatif) ; rien hors de tout bloc.</summary>
    /// <param name="position">Position dans le texte affiché.</param>
    /// <param name="firstBodyLine">Ligne du fichier où commence le corps de la fiche, à partir de 1.</param>
    public int? SourceLineAt(int position, int firstBodyLine)
    {
        var piece = _pieces.Find(candidate => candidate.End > position) ?? (_pieces.Count > 0 ? _pieces[^1] : null);
        for (var element = piece?.Node as RenderElement ?? piece?.Node.Parent; element is not null; element = element.Parent)
        {
            if (element.Line is { } line)
            {
                return firstBodyLine + line - 1;
            }
        }
        return null;
    }

    private static bool IsWithin(RenderNode node, RenderElement ancestor)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<RenderElement> Descendants(RenderElement root)
    {
        foreach (var child in root.Children.OfType<RenderElement>())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Hors du texte affiché : un bloc de diagramme, avant ou après son dessin.</summary>
    private static bool IsExcluded(RenderElement element) =>
        element.Tag == "pre" && element.Children.OfType<RenderElement>().Any(child => child.Language is "mermaid" or "reseau");

    private void Separate()
    {
        if (_text.Length > 0 && _text[^1] != '\n')
        {
            _text.Append('\n');
        }
    }

    private void Walk(RenderElement parent)
    {
        foreach (var node in parent.Children)
        {
            if (node is RenderText text)
            {
                if (text.Value.Length > 0)
                {
                    _pieces.Add(new TextPiece(text, _text.Length, _text.Length + text.Value.Length));
                    _text.Append(text.Value);
                }
                continue;
            }
            var element = (RenderElement)node;
            if (IsExcluded(element))
            {
                continue;
            }
            if (element.Formula is { } formula)
            {
                var source = element.IsFormulaBlock ? $"$${formula}$$" : $"${formula}$";
                if (element.IsFormulaBlock)
                {
                    Separate();
                }
                _pieces.Add(new TextPiece(element, _text.Length, _text.Length + source.Length));
                _text.Append(source);
                if (element.IsFormulaBlock)
                {
                    Separate();
                }
                continue;
            }
            if (element.Tag == "br")
            {
                _text.Append('\n');
                continue;
            }
            var block = Blocks.Contains(element.Tag);
            if (block)
            {
                Separate();
            }
            Walk(element);
            if (block)
            {
                Separate();
            }
        }
    }

    private sealed class FrozenTags(params string[] tags)
    {
        private readonly HashSet<string> _tags = new(tags, StringComparer.Ordinal);

        public bool Contains(string tag) => _tags.Contains(tag);
    }
}
