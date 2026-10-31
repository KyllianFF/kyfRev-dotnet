using System.Collections.Immutable;

namespace KyfRev.DesignSystem.Tokens;

/// <summary>
/// Toutes les couleurs de kyfRev, et le seul endroit où elles s'écrivent (conception de la 0.x,
/// §12). Trois gammes construites dans l'espace OKLCH autour des quatre couleurs fournies
/// (violet 600, pervenche 400, mauve 400 et 300), puis les neutres teintés de violet et les
/// couleurs fonctionnelles de chaque thème. Les composants n'emploient que des rôles
/// (<see cref="ColorRole"/>), jamais ces valeurs.
/// </summary>
public static class Palette
{
    /// <summary>Violet : la couleur principale (600 : <c>#7f4ea8</c>, fournie).</summary>
    public static ColorRamp Violet { get; } = new(
        "violet", "#f9f4fe", "#f2e8fe", "#e6cdfe", "#d5adfb", "#bd8ee8", "#a270ce", "#7f4ea8", "#6d4192", "#542f72", "#3a1e51");

    /// <summary>Pervenche : la couleur secondaire, celle des passages et de la sélection (400 : <c>#9c93e5</c>, fournie).</summary>
    public static ColorRamp Periwinkle { get; } = new(
        "pervenche", "#f6f6fe", "#ebebfe", "#d6d4ff", "#bdb7fe", "#9c93e5", "#887dd2", "#6e62b4", "#574d95", "#423975", "#2d2554");

    /// <summary>Mauve : l'accent, celui des rappels et du surlignage (400 : <c>#ba8cbe</c> et 300 : <c>#c7a3d2</c>, fournies).</summary>
    public static ColorRamp Mauve { get; } = new(
        "mauve", "#fdf3fd", "#f7e7f8", "#ebceed", "#c7a3d2", "#ba8cbe", "#a677ab", "#8b5d8f", "#714874", "#57355a", "#3c233f");

    /// <summary>Couleur des ombres portées, dans les deux thèmes (leur opacité vient de l'élévation).</summary>
    public static TokenColor Shadow { get; } = TokenColor.FromHex("#000000");

    /// <summary>Neutres et couleurs fonctionnelles du thème clair.</summary>
    public static ThemeNeutrals Light { get; } = new(
        Background: TokenColor.FromHex("#f8f7fb"),
        Surface: TokenColor.FromHex("#ffffff"),
        SurfaceAlt: TokenColor.FromHex("#f1eff6"),
        Divider: TokenColor.FromHex("#dedae8"),
        ControlBorder: TokenColor.FromHex("#8a8498"),
        Text: TokenColor.FromHex("#221d2e"),
        TextSecondary: TokenColor.FromHex("#5d566d"),
        OnPrimary: TokenColor.FromHex("#ffffff"),
        Critical: TokenColor.FromHex("#b3261e"),
        CriticalBackground: TokenColor.FromHex("#fdecea"),
        Warning: TokenColor.FromHex("#8a5300"),
        WarningBackground: TokenColor.FromHex("#fff4e0"),
        Success: TokenColor.FromHex("#1e7a46"),
        SuccessBackground: TokenColor.FromHex("#e7f5ec"),
        Scrim: TokenColor.FromHex("#73221d2e"));

    /// <summary>Neutres et couleurs fonctionnelles du thème sombre.</summary>
    public static ThemeNeutrals Dark { get; } = new(
        Background: TokenColor.FromHex("#15121c"),
        Surface: TokenColor.FromHex("#1e1a28"),
        SurfaceAlt: TokenColor.FromHex("#2a2536"),
        Divider: TokenColor.FromHex("#3a3448"),
        ControlBorder: TokenColor.FromHex("#777087"),
        Text: TokenColor.FromHex("#ece8f4"),
        TextSecondary: TokenColor.FromHex("#a9a1bb"),
        OnPrimary: TokenColor.FromHex("#15121c"),
        Critical: TokenColor.FromHex("#ff8a80"),
        CriticalBackground: TokenColor.FromHex("#3d1f22"),
        Warning: TokenColor.FromHex("#f0b45a"),
        WarningBackground: TokenColor.FromHex("#3a2a12"),
        Success: TokenColor.FromHex("#6fcf97"),
        SuccessBackground: TokenColor.FromHex("#173326"),
        Scrim: TokenColor.FromHex("#99000000"));
}

/// <summary>Une gamme de dix nuances, de 50 (la plus claire) à 900 (la plus foncée).</summary>
public sealed class ColorRamp
{
    /// <summary>Les nuances, dans l'ordre.</summary>
    public static ImmutableArray<int> Shades { get; } = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900];

    private readonly ImmutableArray<TokenColor> _colors;

    internal ColorRamp(string name, params string[] hex)
    {
        Name = name;
        _colors = [.. hex.Select(TokenColor.FromHex)];
    }

    /// <summary>Nom de la gamme (« violet », « pervenche », « mauve »).</summary>
    public string Name { get; }

    /// <summary>La nuance <paramref name="shade"/> (50, 100… 900).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Ce n'est pas l'une des dix nuances.</exception>
    public TokenColor this[int shade] => Shades.IndexOf(shade) is var index and >= 0
        ? _colors[index]
        : throw new ArgumentOutOfRangeException(nameof(shade), shade, "Nuance attendue : 50, 100, 200… 900.");
}

/// <summary>Neutres teintés de violet et couleurs fonctionnelles d'un thème.</summary>
/// <param name="Background">Fond de la fenêtre.</param>
/// <param name="Surface">Fond des cartes et des champs.</param>
/// <param name="SurfaceAlt">Fond secondaire : survol, blocs de code, squelettes.</param>
/// <param name="Divider">Séparateurs décoratifs.</param>
/// <param name="ControlBorder">Contour des contrôles (au moins 3:1 sur tous les fonds).</param>
/// <param name="Text">Texte.</param>
/// <param name="TextSecondary">Texte secondaire.</param>
/// <param name="OnPrimary">Texte sur la couleur principale.</param>
/// <param name="Critical">Retard et erreur.</param>
/// <param name="CriticalBackground">Fond des messages d'erreur.</param>
/// <param name="Warning">Avertissement.</param>
/// <param name="WarningBackground">Fond des avertissements.</param>
/// <param name="Success">Réussite.</param>
/// <param name="SuccessBackground">Fond des réussites.</param>
/// <param name="Scrim">Voile sous les dialogues (transparent).</param>
public sealed record ThemeNeutrals(
    TokenColor Background,
    TokenColor Surface,
    TokenColor SurfaceAlt,
    TokenColor Divider,
    TokenColor ControlBorder,
    TokenColor Text,
    TokenColor TextSecondary,
    TokenColor OnPrimary,
    TokenColor Critical,
    TokenColor CriticalBackground,
    TokenColor Warning,
    TokenColor WarningBackground,
    TokenColor Success,
    TokenColor SuccessBackground,
    TokenColor Scrim);
