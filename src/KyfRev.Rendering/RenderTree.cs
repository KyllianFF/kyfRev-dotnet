namespace KyfRev.Rendering;

/// <summary>
/// Un nœud du contenu d'une fiche. L'arbre reprend le HTML que kyfRev 0.x tirait de markdown-it,
/// sauts de ligne de mise en forme compris : le texte affiché, donc la position des passages, en dépend.
/// </summary>
public abstract class RenderNode
{
    /// <summary>L'élément qui contient ce nœud ; rien pour la racine.</summary>
    public RenderElement? Parent { get; internal set; }
}

/// <summary>Un nœud de texte. Deux textes qui se suivent n'en font qu'un, comme dans un document HTML.</summary>
public sealed class RenderText : RenderNode
{
    internal RenderText(string value)
    {
        Value = value;
    }

    /// <summary>Le texte.</summary>
    public string Value { get; internal set; }
}

/// <summary>Un élément, nommé comme sa balise HTML (<c>p</c>, <c>h2</c>, <c>li</c>…).</summary>
public sealed class RenderElement : RenderNode
{
    private readonly List<RenderNode> _children = [];

    internal RenderElement(string tag)
    {
        Tag = tag;
    }

    /// <summary>Nom de la balise.</summary>
    public string Tag { get; }

    /// <summary>Le contenu, dans l'ordre.</summary>
    public IReadOnlyList<RenderNode> Children => _children;

    /// <summary>Première ligne du bloc dans le corps de la fiche, à partir de 1 ; rien si le bloc n'en a pas.</summary>
    public int? Line { get; internal set; }

    /// <summary>Source TeX d'une formule, sans ses espaces de bord ; rien pour un autre élément.</summary>
    public string? Formula { get; internal set; }

    /// <summary>La formule occupe tout un bloc.</summary>
    public bool IsFormulaBlock { get; internal set; }

    /// <summary>Langue d'un bloc de code (<c>python</c>, <c>mermaid</c>…), sur son élément <c>code</c>.</summary>
    public string? Language { get; internal set; }

    /// <summary>Destination d'un lien, ou source d'une image, telle qu'elle est écrite.</summary>
    public string? Target { get; internal set; }

    /// <summary>Identifiant d'une note ou d'un renvoi, vers lequel mène une destination en <c>#</c>.</summary>
    public string? Identifier { get; internal set; }

    /// <summary>Texte de remplacement d'une image.</summary>
    public string? AlternativeText { get; internal set; }

    /// <summary>Numéro de départ d'une liste numérotée qui ne commence pas à 1.</summary>
    public int? ListStart { get; internal set; }

    /// <summary>
    /// Alignement d'une colonne de tableau (<c>left</c>, <c>center</c> ou <c>right</c>), ou d'un passage
    /// (<c>center</c>, <c>right</c> ou <c>justify</c>).
    /// </summary>
    public string? Alignment { get; internal set; }

    /// <summary>Couleur d'un passage, écrite <c>#rrggbb</c>.</summary>
    public string? TextColor { get; internal set; }

    /// <summary>Taille d'un passage, par rapport au texte qui l'entoure.</summary>
    public double? FontScale { get; internal set; }

    /// <summary>Un point de liste qui est une tâche : faite, ou à faire ; rien pour un point ordinaire.</summary>
    public bool? Task { get; internal set; }

    /// <summary>Ce titre répète celui de la fiche : masqué, mais gardé dans le texte, où les ancres s'y réfèrent.</summary>
    public bool IsRepeatedTitle { get; internal set; }

    /// <summary>Le niveau d'un titre, de 1 à 6 ; 0 pour un autre élément.</summary>
    public int HeadingLevel => Tag is ['h', >= '1' and <= '6'] ? Tag[1] - '0' : 0;

    /// <summary>Nombre d'éléments au-dessus de celui-ci.</summary>
    internal int Depth { get; set; }

    /// <summary>Ajoute un enfant ; un texte rejoint le texte qui le précède.</summary>
    internal void Add(RenderNode node)
    {
        if (node is RenderText text && _children.Count > 0 && _children[^1] is RenderText previous)
        {
            previous.Value += text.Value;
            return;
        }
        node.Parent = this;
        _children.Add(node);
    }

    /// <summary>Le texte de l'élément et de tout ce qu'il contient ; une formule compte pour sa source.</summary>
    public string TextContent()
    {
        if (Formula is not null)
        {
            return Formula;
        }
        return string.Concat(_children.Select(child => child is RenderElement element ? element.TextContent() : ((RenderText)child).Value));
    }
}
