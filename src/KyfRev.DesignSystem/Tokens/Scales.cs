using System.Collections.Immutable;
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
/// Typographie : polices système (rien à télécharger), une échelle régulière dont chaque
/// interligne tombe sur la grille de 4 px, deux graisses (400 et 600).
/// </summary>
public static class Typography
{
    /// <summary>Police de l'interface.</summary>
    public const string UiFamily = "Segoe UI Variable Text, Segoe UI";

    /// <summary>Police des titres.</summary>
    public const string DisplayFamily = "Segoe UI Variable Display, Segoe UI";

    /// <summary>Police du code.</summary>
    public const string CodeFamily = "Cascadia Mono, Consolas";

    /// <summary>Icônes (Windows 11, puis Windows 10).</summary>
    public const string IconFamily = "Segoe Fluent Icons, Segoe MDL2 Assets";

    /// <summary>Légendes, pastilles.</summary>
    public static TypeStyle Caption { get; } = new("Caption", 12, 16, SemiBold: false);

    /// <summary>Texte courant de l'interface.</summary>
    public static TypeStyle Body { get; } = new("Body", 14, 20, SemiBold: false);

    /// <summary>Texte courant mis en avant.</summary>
    public static TypeStyle BodyStrong { get; } = new("BodyStrong", 14, 20, SemiBold: true);

    /// <summary>Texte courant agrandi.</summary>
    public static TypeStyle BodyLarge { get; } = new("BodyLarge", 16, 24, SemiBold: false);

    /// <summary>Sous-titre, titre de carte ou de dialogue.</summary>
    public static TypeStyle Subtitle { get; } = new("Subtitle", 20, 28, SemiBold: true);

    /// <summary>Titre de page.</summary>
    public static TypeStyle Title { get; } = new("Title", 24, 32, SemiBold: true);

    /// <summary>Grand titre (tableau de bord, état vide).</summary>
    public static TypeStyle Display { get; } = new("Display", 32, 40, SemiBold: true);

    /// <summary>L'échelle, de la plus petite taille à la plus grande.</summary>
    public static ImmutableArray<TypeStyle> Ramp { get; } = [Caption, Body, BodyStrong, BodyLarge, Subtitle, Title, Display];

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

/// <summary>Rayons des coins, sur la grille de 4 px.</summary>
public static class Radii
{
    /// <summary>Boutons, champs, infobulles.</summary>
    public const double Control = 4;

    /// <summary>Cartes, menus, notifications.</summary>
    public const double Card = 8;

    /// <summary>Dialogues.</summary>
    public const double Dialog = 12;
}

/// <summary>Une élévation : l'ombre portée d'un élément flottant.</summary>
/// <param name="Name">Nom (« Flyout », « Dialog »).</param>
/// <param name="BlurRadius">Flou de l'ombre.</param>
/// <param name="Depth">Décalage vers le bas.</param>
/// <param name="Opacity">Opacité de l'ombre.</param>
public sealed record Elevation(string Name, double BlurRadius, double Depth, double Opacity)
{
    /// <summary>Menus, infobulles, listes déroulantes.</summary>
    public static Elevation Flyout { get; } = new("Flyout", 8, 2, 0.18);

    /// <summary>Dialogues.</summary>
    public static Elevation Dialog { get; } = new("Dialog", 24, 8, 0.24);

    /// <summary>Les élévations : tout le reste est à plat, bordé d'un trait de 1 px.</summary>
    public static ImmutableArray<Elevation> All { get; } = [Flyout, Dialog];
}

/// <summary>Durées des animations : 150 ms au plus, aucune si Windows demande de réduire les animations.</summary>
public static class Motion
{
    /// <summary>Survol, apparition d'un repère.</summary>
    public static TimeSpan Fast { get; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Ouverture et fermeture d'un élément.</summary>
    public static TimeSpan Normal { get; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Durée réellement employée : nulle quand les animations sont réduites.</summary>
    public static TimeSpan Effective(TimeSpan duration, bool reducedMotion) => reducedMotion ? TimeSpan.Zero : duration;

    /// <summary>Vrai si Windows demande de réduire les animations (Paramètres › Accessibilité › Effets visuels).</summary>
    public static bool SystemPrefersReducedMotion => !SystemParameters.ClientAreaAnimation;
}
