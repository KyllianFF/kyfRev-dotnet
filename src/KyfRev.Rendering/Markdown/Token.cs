namespace KyfRev.Rendering.Markdown;

/// <summary>Ce qu'un jeton représente, quand son traitement ne se résume pas à sa balise.</summary>
internal enum TokenKind
{
    /// <summary>Une balise ouvrante, fermante ou seule.</summary>
    Element,
    Inline,
    Text,

    /// <summary>Un texte issu d'un échappement ou d'une entité : la détection des liens ne le lit pas.</summary>
    TextSpecial,
    CodeInline,
    CodeBlock,
    Fence,
    MathInline,
    MathBlock,
    Softbreak,
    Hardbreak,
    Image,
    LinkOpen,
    LinkClose,
    ParagraphOpen,
    ParagraphClose,
    ReferenceDefinition,
    FootnoteReferenceOpen,
    FootnoteReferenceClose,
    FootnoteBlockOpen,
    FootnoteBlockClose,
    FootnoteOpen,
    FootnoteClose,
    FootnoteAnchor,
    FootnoteRef,
}

/// <summary>Un jeton de l'analyse, comme ceux de markdown-it.</summary>
internal sealed class Token(TokenKind kind, string tag, int nesting)
{
    public TokenKind Kind { get; set; } = kind;

    public string Tag { get; set; } = tag;

    /// <summary>1 pour une balise ouvrante, -1 pour une fermante, 0 pour un jeton seul.</summary>
    public int Nesting { get; set; } = nesting;

    public int Level { get; set; }

    /// <summary>Le jeton commence un bloc (retours à la ligne de mise en forme autour de sa balise).</summary>
    public bool Block { get; set; }

    /// <summary>Paragraphe d'une liste serrée : sa balise n'est pas écrite, son contenu l'est.</summary>
    public bool Hidden { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>Texte qui suit la clôture d'ouverture d'un bloc de code.</summary>
    public string Info { get; set; } = string.Empty;

    /// <summary>Première ligne du bloc, à partir de 0, sur les balises ouvrantes qui en ont une.</summary>
    public int? Line { get; set; }

    /// <summary>Contenu d'un jeton <see cref="TokenKind.Inline"/> ou d'une image.</summary>
    public List<Token> Children { get; set; } = [];

    /// <summary>Destination d'un lien ou d'une image.</summary>
    public string? Href { get; set; }

    /// <summary>Numéro de départ d'une liste qui ne commence pas à 1.</summary>
    public int? Start { get; set; }

    /// <summary>Alignement d'une cellule de tableau.</summary>
    public string? Alignment { get; set; }

    /// <summary>Étiquette d'une définition de note.</summary>
    public string? Label { get; set; }

    /// <summary>Numéro d'une note, à partir de 0.</summary>
    public int FootnoteId { get; set; }

    /// <summary>Rang d'un renvoi vers cette note, à partir de 0.</summary>
    public int FootnoteSubId { get; set; }
}

/// <summary>Une note appelée dans le texte.</summary>
internal sealed class FootnoteEntry
{
    public string? Label { get; init; }

    /// <summary>Le contenu d'une note écrite sur place (<c>^[…]</c>) ; rien pour une définition.</summary>
    public List<Token>? Tokens { get; init; }

    public int Count { get; set; }
}

/// <summary>Ce que l'analyse d'un document retient d'un bloc à l'autre.</summary>
internal sealed class ParseEnvironment
{
    public Dictionary<string, (string Href, string Title)> References { get; } = new(StringComparer.Ordinal);

    /// <summary>Numéro de chaque note définie ; -1 tant qu'elle n'est pas appelée.</summary>
    public Dictionary<string, int> FootnoteReferences { get; } = new(StringComparer.Ordinal);

    public List<FootnoteEntry> Footnotes { get; } = [];
}
