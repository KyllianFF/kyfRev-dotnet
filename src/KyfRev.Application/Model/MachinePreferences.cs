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
    /// <summary>17 pixels.</summary>
    Normal,

    /// <summary>19 pixels.</summary>
    Large,

    /// <summary>21 pixels.</summary>
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
}

/// <summary>Ce qui est retenu sur cet ordinateur, jamais dans le coffre : l'apparence, le confort de lecture et la voix.</summary>
public sealed record MachinePreferences
{
    /// <summary>Thème d'affichage.</summary>
    public ThemePreference Theme { get; init; } = ThemePreference.System;

    /// <summary>Taille de l'interface (1 : taille normale).</summary>
    public double Zoom { get; init; } = 1;

    /// <summary>Largeur du texte des fiches.</summary>
    public ReadingWidth Width { get; init; } = ReadingWidth.Comfortable;

    /// <summary>Taille du texte des fiches.</summary>
    public ReadingTextSize TextSize { get; init; } = ReadingTextSize.Normal;

    /// <summary>Interligne du texte des fiches.</summary>
    public ReadingLineSpacing LineSpacing { get; init; } = ReadingLineSpacing.Normal;

    /// <summary>Police du texte des fiches.</summary>
    public ReadingFont Font { get; init; } = ReadingFont.Sans;

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
