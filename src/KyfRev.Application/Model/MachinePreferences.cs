namespace KyfRev.Application.Model;

/// <summary>Thème choisi par l'utilisateur.</summary>
public enum ThemePreference
{
    /// <summary>Comme Windows (clair ou sombre).</summary>
    System,

    /// <summary>Toujours clair.</summary>
    Light,

    /// <summary>Toujours sombre.</summary>
    Dark,
}

/// <summary>Largeur du texte d'une fiche.</summary>
public enum ReadingWidth
{
    /// <summary>Des lignes de longueur reposante.</summary>
    Comfortable,

    /// <summary>Toute la place disponible, tableaux larges compris.</summary>
    Full,
}

/// <summary>Taille du texte d'une fiche.</summary>
public enum ReadingTextSize
{
    /// <summary>12 points, comme le corps d'un traitement de texte.</summary>
    Normal,

    /// <summary>14 points.</summary>
    Large,

    /// <summary>16 points.</summary>
    ExtraLarge,
}

/// <summary>Interligne du texte d'une fiche.</summary>
public enum ReadingLineSpacing
{
    /// <summary>1,65 fois la taille du texte.</summary>
    Normal,

    /// <summary>1,9 fois la taille du texte.</summary>
    Airy,
}

/// <summary>Police du texte d'une fiche.</summary>
public enum ReadingFont
{
    /// <summary>Sans empattements : celle de l'application.</summary>
    Sans,

    /// <summary>Avec empattements : Georgia.</summary>
    Serif,

    /// <summary>Manuscrite, comme au stylo : Segoe Print.</summary>
    Handwriting,

    /// <summary>Au feutre, plus libre : Ink Free.</summary>
    Marker,

    /// <summary>À chasse fixe, comme à la machine : Consolas.</summary>
    Typewriter,
}

/// <summary>Ce qui est retenu sur cet ordinateur, jamais dans le coffre : l'apparence, le confort de lecture et la voix.</summary>
public sealed record MachinePreferences
{
    /// <summary>Mode concentration : le menu est masqué, et le texte des fiches garde la largeur de lecture.</summary>
    public bool FocusMode { get; init; }

    /// <summary>Thème d'affichage.</summary>
    public ThemePreference Theme { get; init; } = ThemePreference.System;

    /// <summary>Taille de l'interface (1 : taille normale).</summary>
    public double Zoom { get; init; } = 1;

    /// <summary>
    /// Zoom de la fiche (1 : taille normale), en lecture, en révision et en écriture. Il ne touche pas au reste de
    /// l'interface, qui a sa propre taille.
    /// </summary>
    public double SheetZoom { get; init; } = 1;

    /// <summary>La mise en page de toutes les fiches : la feuille sans fin, ou un format de page.</summary>
    public PageLayout Page { get; init; } = PageLayout.Default;

    /// <summary>Le papier montré derrière le texte, par type de feuille. Il ne s'imprime jamais.</summary>
    public PaperChoices Paper { get; init; } = PaperChoices.Default;

    /// <summary>Les fiches qui ont leur propre mise en page.</summary>
    public SheetPageLayouts SheetPages { get; init; } = SheetPageLayouts.None;

    /// <summary>L'ordre choisi des vues du menu : leurs identifiants séparés par des virgules ; vide pour l'ordre d'origine.</summary>
    public string MenuOrder { get; init; } = string.Empty;

    /// <summary>Les raccourcis changés : « imprimer=Ctrl+Shift+I;partager=Ctrl+J » ; vide si aucun.</summary>
    public string Shortcuts { get; init; } = string.Empty;

    /// <summary>Largeur du texte des fiches : gardée pour relire les anciennes préférences, elle n'a plus d'effet.</summary>
    public ReadingWidth Width { get; init; } = ReadingWidth.Comfortable;

    /// <summary>Taille du texte des fiches.</summary>
    public ReadingTextSize TextSize { get; init; } = ReadingTextSize.Normal;

    /// <summary>Interligne du texte des fiches.</summary>
    public ReadingLineSpacing LineSpacing { get; init; } = ReadingLineSpacing.Normal;

    /// <summary>Police du texte des fiches.</summary>
    public ReadingFont Font { get; init; } = ReadingFont.Sans;

    /// <summary>Un mot défini dans le coffre se marque dans toutes les fiches qui le contiennent, hors révision.</summary>
    public bool MarkDefinedWords { get; init; } = true;

    /// <summary>
    /// Le raccourci global de la capture rapide (Ctrl+Alt+N) est demandé à Windows : il ouvre la capture même quand une
    /// autre application est au premier plan. Coupé par défaut : un raccourci global se choisit.
    /// </summary>
    public bool GlobalCapture { get; init; }

    /// <summary>Animations réduites, même si Windows ne le demande pas.</summary>
    public bool ReducedMotion { get; init; }

    /// <summary>Contraste élevé, même si Windows ne l'active pas.</summary>
    public bool HighContrast { get; init; }

    /// <summary>Les petits boutons sont agrandis.</summary>
    public bool LargerTargets { get; init; }

    /// <summary>Voix de la lecture à voix haute ; rien pour la première installée.</summary>
    public string? Voice { get; init; }

    /// <summary>Vitesse de la lecture à voix haute (1 : normale).</summary>
    public double Rate { get; init; } = 1;
}
