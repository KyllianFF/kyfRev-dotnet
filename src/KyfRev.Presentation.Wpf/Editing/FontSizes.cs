using System.Globalization;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// Les tailles proposées pour le texte, en pixels comme dans un traitement de texte. Le fichier les écrit par rapport au
/// texte normal (<c>1.18em</c>), pour qu'elles suivent un titre ou un autre affichage.
/// </summary>
internal static class FontSizes
{
    /// <summary>Le texte normal, sans taille écrite.</summary>
    public static int Normal { get; } = (int)Typography.BodyLarge.Size;

    /// <summary>Les tailles de la liste.</summary>
    public static IReadOnlyList<int> Steps { get; } = [10, 12, 14, Normal, 20, 24, 28, 32, 40];

    /// <summary>L'écriture d'une taille, sans <c>font-size:</c> ; rien pour le texte normal.</summary>
    public static string? Style(int pixels) =>
        pixels == Normal ? null : string.Create(CultureInfo.InvariantCulture, $"{Math.Round((double)pixels / Normal, 2)}em");

    /// <summary>La taille en pixels d'une écriture ; rien : le texte normal.</summary>
    public static int Pixels(string? style) =>
        style is null ? Normal : (int)Math.Round(double.Parse(style.AsSpan(0, style.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture) * Normal);

    /// <summary>La taille suivante de la liste, vers le haut ou vers le bas ; aux bouts, on y reste.</summary>
    public static int Step(string? style, int direction)
    {
        var current = Pixels(style);
        return direction > 0
            ? Steps.Where(step => step > current).DefaultIfEmpty(Steps[^1]).First()
            : Steps.Where(step => step < current).DefaultIfEmpty(Steps[0]).Last();
    }
}
