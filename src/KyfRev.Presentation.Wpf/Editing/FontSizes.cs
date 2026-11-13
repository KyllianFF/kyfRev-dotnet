using System.Globalization;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// Les tailles proposées pour le texte, en points comme dans un traitement de texte : 12 pour le corps, 20, 16 et 14 pour
/// les trois premiers titres. Le fichier les écrit par rapport à la taille de son bloc (<c>1.17em</c>), pour qu'elles
/// suivent un titre ou un autre réglage de la taille du texte.
/// </summary>
internal static class FontSizes
{
    /// <summary>Le texte normal, sans taille écrite.</summary>
    public static int Normal => ReadingComfort.BodyPoints;

    /// <summary>Les tailles de la liste.</summary>
    public static IReadOnlyList<int> Steps { get; } = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 36];

    /// <summary>La taille d'un bloc sans taille écrite : celle du corps, ou celle d'un titre de niveau <paramref name="heading"/>.</summary>
    public static int Base(int heading) => heading > 0 ? ReadingComfort.HeadingPoints(heading) : Normal;

    /// <summary>L'écriture d'une taille, sans <c>font-size:</c> ; rien pour la taille que le bloc a déjà.</summary>
    public static string? Style(int points, int heading = 0) =>
        points == Base(heading) ? null : string.Create(CultureInfo.InvariantCulture, $"{Math.Round((double)points / Base(heading), 2)}em");

    /// <summary>La taille en points d'une écriture ; rien : celle du bloc.</summary>
    public static int Points(string? style, int heading = 0) =>
        style is null ? Base(heading) : (int)Math.Round(double.Parse(style.AsSpan(0, style.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture) * Base(heading));

    /// <summary>La taille suivante de la liste, vers le haut ou vers le bas ; aux bouts, on y reste.</summary>
    public static int Step(string? style, int direction, int heading = 0)
    {
        var current = Points(style, heading);
        return direction > 0
            ? Steps.Where(step => step > current).DefaultIfEmpty(Steps[^1]).First()
            : Steps.Where(step => step < current).DefaultIfEmpty(Steps[0]).Last();
    }
}
