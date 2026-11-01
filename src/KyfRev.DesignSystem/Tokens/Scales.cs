using System.Collections.Immutable;
using System.IO.Packaging;
using System.Windows;

namespace KyfRev.DesignSystem.Tokens;

/// <summary>Un style de texte : taille et interligne en pixels, graisse.</summary>
/// <param name="Name">Nom du style (« Body », « Title »…).</param>
/// <param name="Size">Taille de la police.</param>
/// <param name="LineHeight">Hauteur de ligne, multiple de 4.</param>
/// <param name="SemiBold">Graisse 600 plutôt que 400.</param>
public sealed record TypeStyle(string Name, double Size, double LineHeight, bool SemiBold)
{
    /// <summary>Graisse WPF.</summary>
    public FontWeight Weight => SemiBold ? FontWeights.SemiBold : FontWeights.Normal;
}

/// <summary>
/// Typographie : Atkinson Hyperlegible Next, conçue pour se lire sans effort, et sa version à
/// chasse fixe pour les étapes, les dates et les compteurs. Les deux familles sont livrées avec
/// kyfRev (licence SIL OFL 1.1, dossier <c>Fonts</c>) ; Segoe UI prend le relais pour un caractère
/// qu'elles n'auraient pas. Une échelle régulière dont chaque interligne tombe sur la grille de
/// 4 px, deux graisses (400 et 600).
/// </summary>
public static class Typography
{
    /// <summary>Emplacement des polices livrées, dans les ressources du design system.</summary>
    public static Uri FontsBase { get; } = PackUri("pack://application:,,,/KyfRev.DesignSystem;component/Fonts/");

    /// <summary>Police de l'interface et du texte des fiches (relative à <see cref="FontsBase"/>).</summary>
    public const string UiFamily = "./#Atkinson Hyperlegible Next, Segoe UI";

    /// <summary>Police des titres.</summary>
    public const string DisplayFamily = UiFamily;

    /// <summary>Police à chasse fixe : code, étapes (J7), dates et compteurs.</summary>
    public const string CodeFamily = "./#Atkinson Hyperlegible Mono, Cascadia Mono, Consolas";

    /// <summary>Icônes (Windows 11, puis Windows 10).</summary>
    public const string IconFamily = "Segoe Fluent Icons, Segoe MDL2 Assets";

    /// <summary>Légendes, pastilles.</summary>
    public static TypeStyle Caption { get; } = new("Caption", 13, 20, SemiBold: false);

    /// <summary>Texte courant de l'interface.</summary>
    public static TypeStyle Body { get; } = new("Body", 15, 24, SemiBold: false);

    /// <summary>Texte courant mis en avant.</summary>
    public static TypeStyle BodyStrong { get; } = new("BodyStrong", 15, 24, SemiBold: true);

    /// <summary>Texte courant agrandi, titre d'une ligne de liste.</summary>
    public static TypeStyle BodyLarge { get; } = new("BodyLarge", 17, 24, SemiBold: false);

    /// <summary>Sous-titre, titre de carte ou de dialogue.</summary>
    public static TypeStyle Subtitle { get; } = new("Subtitle", 21, 28, SemiBold: true);

    /// <summary>Titre de page.</summary>
    public static TypeStyle Title { get; } = new("Title", 34, 40, SemiBold: true);

    /// <summary>Grand titre, compteur du jour.</summary>
    public static TypeStyle Display { get; } = new("Display", 48, 56, SemiBold: true);

    /// <summary>L'échelle, de la plus petite taille à la plus grande.</summary>
    public static ImmutableArray<TypeStyle> Ramp { get; } = [Caption, Body, BodyStrong, BodyLarge, Subtitle, Title, Display];

    /// <summary>Une adresse « pack » : <see cref="Uri"/> ne connaît ce schéma qu'une fois <see cref="PackUriHelper"/> chargé.</summary>
    private static Uri PackUri(string address)
    {
        _ = PackUriHelper.UriSchemePack;
        return new Uri(address);
    }

    /// <summary>Tailles du texte des fiches (normale, grande, très grande), comme dans la 0.x.</summary>
    public static ImmutableArray<double> ReadingSizes { get; } = [17, 19, 21];

    /// <summary>Interligne du texte des fiches.</summary>
    public const double ReadingLineSpacing = 1.65;
}

/// <summary>Espacements : la grille de 4 px.</summary>
public static class Spacing
{
    /// <summary>Le pas de la grille.</summary>
    public const double Unit = 4;

    /// <summary>Les espacements employés, tous multiples du pas.</summary>
    public static ImmutableArray<double> Scale { get; } = [4, 8, 12, 16, 20, 24, 32, 40, 48];
}

/// <summary>Rayons des coins, sur la grille de 4 px : des formes douces, jamais des bulles.</summary>
public static class Radii
{
    /// <summary>Boutons, champs, infobulles.</summary>
    public const double Control = 12;

    /// <summary>Cartes, menus, notifications.</summary>
    public const double Card = 20;

    /// <summary>Dialogues.</summary>
    public const double Dialog = 24;
}

/// <summary>Une élévation : l'ombre portée d'un élément flottant.</summary>
/// <param name="Name">Nom (« Flyout », « Dialog »).</param>
/// <param name="BlurRadius">Flou de l'ombre.</param>
/// <param name="Depth">Décalage vers le bas.</param>
/// <param name="Opacity">Opacité de l'ombre.</param>
public sealed record Elevation(string Name, double BlurRadius, double Depth, double Opacity)
{
    /// <summary>Cartes posées sur le fond : une ombre douce et large, à peine visible.</summary>
    public static Elevation Card { get; } = new("Card", 20, 4, 0.08);

    /// <summary>Menus, infobulles, listes déroulantes.</summary>
    public static Elevation Flyout { get; } = new("Flyout", 16, 4, 0.16);

    /// <summary>Dialogues.</summary>
    public static Elevation Dialog { get; } = new("Dialog", 40, 12, 0.24);

    /// <summary>Les élévations : tout le reste est à plat ; en contraste élevé, aucune ombre, le contour suffit.</summary>
    public static ImmutableArray<Elevation> All { get; } = [Card, Flyout, Dialog];
}

/// <summary>
/// Durées des animations : des décélérations douces, 260 ms au plus, rien qui clignote ; aucune
/// si Windows demande de réduire les animations.
/// </summary>
public static class Motion
{
    /// <summary>Survol, apparition d'un repère.</summary>
    public static TimeSpan Fast { get; } = TimeSpan.FromMilliseconds(120);

    /// <summary>Ouverture et fermeture d'un élément.</summary>
    public static TimeSpan Normal { get; } = TimeSpan.FromMilliseconds(220);

    /// <summary>Durée réellement employée : nulle quand les animations sont réduites.</summary>
    public static TimeSpan Effective(TimeSpan duration, bool reducedMotion) => reducedMotion ? TimeSpan.Zero : duration;

    /// <summary>Vrai si Windows demande de réduire les animations (Paramètres › Accessibilité › Effets visuels).</summary>
    public static bool SystemPrefersReducedMotion => !SystemParameters.ClientAreaAnimation;
}
