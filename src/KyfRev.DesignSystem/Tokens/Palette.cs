using System.Collections.Immutable;

namespace KyfRev.DesignSystem.Tokens;

/// <summary>Une couleur et son nom, pour la choisir.</summary>
/// <param name="Name">Son nom.</param>
/// <param name="Color">La couleur.</param>
public sealed record NamedColor(string Name, TokenColor Color);

/// <summary>
/// Toutes les couleurs de kyfRev, et le seul endroit où elles s'écrivent : l'identité « Veillée »,
/// choisie en novembre 2026. Deux gammes, le vert forêt (l'action, ce qui est fait) et l'ambre
/// (le retard, les passages surlignés), puis les neutres de chaque thème : « Jour », clair et
/// teinté de sauge, et « Veillée », sombre et chaud. Les composants n'emploient que des rôles
/// (<see cref="ColorRole"/>), jamais ces valeurs.
/// </summary>
public static class Palette
{
    /// <summary>Vert forêt : la couleur principale (700 en thème clair, 300 en thème sombre).</summary>
    public static ColorRamp Forest { get; } = new(
        "forêt", "#f1f6f1", "#dde8df", "#c1daca", "#86c7a1", "#5eaa80", "#418c64", "#357a57", "#2e6a4e", "#234f3b", "#2a3a30");

    /// <summary>Ambre : le retard, l'avertissement et les passages surlignés.</summary>
    public static ColorRamp Amber { get; } = new(
        "ambre", "#fbf5ec", "#f4e8d6", "#f0d3a8", "#eba458", "#d88a33", "#b9701c", "#a36114", "#93540f", "#6b3d0c", "#3a2e1f");

    /// <summary>
    /// Les couleurs proposées pour le texte des fiches : des teintes franches, de clarté moyenne. Le texte les prend
    /// par <see cref="TokenColor.ReadableOn"/>, qui en fait une couleur lisible sur le fond du thème.
    /// </summary>
    public static ImmutableArray<NamedColor> TextColors { get; } =
    [
        new("Rouge", TokenColor.FromHex("#d0021b")),
        new("Orange", TokenColor.FromHex("#b85c00")),
        new("Or", TokenColor.FromHex("#8a7300")),
        new("Vert", TokenColor.FromHex("#2e7d32")),
        new("Émeraude", TokenColor.FromHex("#00796b")),
        new("Cyan", TokenColor.FromHex("#00708c")),
        new("Bleu", TokenColor.FromHex("#1565c0")),
        new("Indigo", TokenColor.FromHex("#4338ca")),
        new("Violet", TokenColor.FromHex("#7b1fa2")),
        new("Rose", TokenColor.FromHex("#c2185b")),
        new("Brun", TokenColor.FromHex("#6d4c41")),
        new("Gris", TokenColor.FromHex("#616161")),
    ];

    /// <summary>Couleur des ombres portées, dans les deux thèmes (leur opacité vient de l'élévation).</summary>
    public static TokenColor Shadow { get; } = TokenColor.FromHex("#000000");

    /// <summary>Neutres et couleurs fonctionnelles du thème clair, « Jour ».</summary>
    public static ThemeNeutrals Light { get; } = new(
        Background: TokenColor.FromHex("#edefe9"),
        Surface: TokenColor.FromHex("#fafbf7"),
        SurfaceAlt: TokenColor.FromHex("#e3e8e0"),
        Divider: TokenColor.FromHex("#d3d9d0"),
        ControlBorder: TokenColor.FromHex("#77837a"),
        Text: TokenColor.FromHex("#1d2822"),
        TextSecondary: TokenColor.FromHex("#4f5c54"),
        OnPrimary: TokenColor.FromHex("#ffffff"),
        Critical: TokenColor.FromHex("#a33a2b"),
        CriticalBackground: TokenColor.FromHex("#f8e4df"),
        Warning: TokenColor.FromHex("#93540f"),
        WarningBackground: TokenColor.FromHex("#f4e8d6"),
        Success: TokenColor.FromHex("#2e6a4e"),
        SuccessBackground: TokenColor.FromHex("#dde8df"),
        Scrim: TokenColor.FromHex("#731d2822"));

    /// <summary>Neutres et couleurs fonctionnelles du thème sombre, « Veillée » : chaud plutôt que bleuté.</summary>
    public static ThemeNeutrals Dark { get; } = new(
        Background: TokenColor.FromHex("#191714"),
        Surface: TokenColor.FromHex("#24211c"),
        SurfaceAlt: TokenColor.FromHex("#2e2a24"),
        Divider: TokenColor.FromHex("#38332b"),
        ControlBorder: TokenColor.FromHex("#877e70"),
        Text: TokenColor.FromHex("#efe9dc"),
        TextSecondary: TokenColor.FromHex("#b4ab9b"),
        OnPrimary: TokenColor.FromHex("#13201a"),
        Critical: TokenColor.FromHex("#f0a090"),
        CriticalBackground: TokenColor.FromHex("#3b2420"),
        Warning: TokenColor.FromHex("#eba458"),
        WarningBackground: TokenColor.FromHex("#3a2e1f"),
        Success: TokenColor.FromHex("#86c7a1"),
        SuccessBackground: TokenColor.FromHex("#2a3a30"),
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

    /// <summary>Nom de la gamme (« forêt », « ambre »).</summary>
    public string Name { get; }

    /// <summary>La nuance <paramref name="shade"/> (50, 100… 900).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Ce n'est pas l'une des dix nuances.</exception>
    public TokenColor this[int shade] => Shades.IndexOf(shade) is var index and >= 0
        ? _colors[index]
        : throw new ArgumentOutOfRangeException(nameof(shade), shade, "Nuance attendue : 50, 100, 200… 900.");
}

/// <summary>Neutres et couleurs fonctionnelles d'un thème.</summary>
/// <param name="Background">Fond de la fenêtre.</param>
/// <param name="Surface">Fond des cartes et des champs.</param>
/// <param name="SurfaceAlt">Fond secondaire : survol, blocs de code, squelettes.</param>
/// <param name="Divider">Séparateurs décoratifs.</param>
/// <param name="ControlBorder">Contour des contrôles (au moins 3:1 sur tous les fonds).</param>
/// <param name="Text">Texte.</param>
/// <param name="TextSecondary">Texte secondaire.</param>
/// <param name="OnPrimary">Texte sur la couleur principale.</param>
/// <param name="Critical">Erreur.</param>
/// <param name="CriticalBackground">Fond des messages d'erreur.</param>
/// <param name="Warning">Retard et avertissement.</param>
/// <param name="WarningBackground">Fond des retards et des avertissements.</param>
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
