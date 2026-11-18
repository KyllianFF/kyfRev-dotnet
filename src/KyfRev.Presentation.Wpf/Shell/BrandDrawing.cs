using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Ce qu'est une pièce de l'icône de kyfRev.</summary>
internal enum BrandPart
{
    /// <summary>Une fiche de l'éventail, derrière la fiche du dessus.</summary>
    Card,

    /// <summary>La fiche du dessus.</summary>
    Sheet,

    /// <summary>Une ligne écrite sur la fiche : son titre, puis son texte.</summary>
    Line,

    /// <summary>Le passage surligné.</summary>
    Band,

    /// <summary>Le trait de la frise des J.</summary>
    Rail,

    /// <summary>Une étape de la frise des J.</summary>
    Dot,
}

/// <summary>Une pièce de l'icône, telle que son dessin la donne, dans un carré de <see cref="BrandDrawing.Size"/> de côté.</summary>
/// <param name="Part">Ce qu'elle est.</param>
/// <param name="Bounds">Sa place, trait non compris.</param>
/// <param name="Radius">L'arrondi de ses coins ; rien pour un rond, qui l'est tout entier.</param>
/// <param name="Outline">Son tracé, pour une pièce qui n'est ni un rectangle ni un rond.</param>
/// <param name="Fill">Sa couleur.</param>
/// <param name="Stroke">La couleur de son trait, si elle en a un.</param>
/// <param name="StrokeWidth">L'épaisseur de ce trait.</param>
/// <param name="Angle">L'angle dont elle est tournée, en degrés, autour de <paramref name="Pivot"/>.</param>
/// <param name="Pivot">Le point autour duquel elle tourne.</param>
internal sealed record BrandShape(BrandPart Part, Rect Bounds, double Radius, string? Outline, Color Fill, Color? Stroke, double StrokeWidth, double Angle, Point Pivot);

/// <summary>
/// Le dessin de l'icône de kyfRev, pièce par pièce, lu dans le fichier qui en est la source unique
/// (<c>assets/icon/kyfrev.svg</c>) : l'animation d'ouverture déplace ces pièces, elle ne redessine rien.
/// </summary>
internal static class BrandDrawing
{
    /// <summary>Le nom du dessin dans les ressources du programme.</summary>
    internal const string Resource = "kyfrev-icone.svg";

    /// <summary>Le côté du carré où l'icône est dessinée.</summary>
    public const double Size = 256;

    private static readonly Lazy<IReadOnlyList<BrandShape>> Drawn = new(() => Read(Text()));

    /// <summary>Les pièces du dessin principal, dans l'ordre où elles se peignent ; aucune si le programme n'a pas son dessin.</summary>
    public static IReadOnlyList<BrandShape> Shapes => Drawn.Value;

    /// <summary>
    /// Lit le dessin principal d'un SVG de l'icône : ses rectangles, ses ronds et ses tracés, hors de <c>defs</c>. Les
    /// rectangles tournés sont les fiches de l'éventail ; le premier des autres est la fiche du dessus, le dernier le trait
    /// de la frise, ceux d'entre les deux ses lignes.
    /// </summary>
    internal static IReadOnlyList<BrandShape> Read(string svg)
    {
        if (string.IsNullOrWhiteSpace(svg))
        {
            return [];
        }
        var shapes = new List<BrandShape>();
        foreach (var element in XDocument.Parse(svg).Root!.Elements())
        {
            var fill = Colour(element, "fill") ?? Colors.Transparent;
            var stroke = Colour(element, "stroke");
            var width = Number(element, "stroke-width");
            switch (element.Name.LocalName)
            {
                case "rect":
                    var (angle, pivot) = Rotation((string?)element.Attribute("transform"));
                    var bounds = new Rect(Number(element, "x"), Number(element, "y"), Number(element, "width"), Number(element, "height"));
                    shapes.Add(new BrandShape(angle == 0 ? BrandPart.Line : BrandPart.Card, bounds, Number(element, "rx"), null, fill, stroke, width, angle, pivot));
                    break;
                case "circle":
                    var radius = Number(element, "r");
                    shapes.Add(new BrandShape(BrandPart.Dot, new Rect(Number(element, "cx") - radius, Number(element, "cy") - radius, 2 * radius, 2 * radius), radius, null, fill, stroke, width, 0, default));
                    break;
                case "path":
                    var outline = (string)element.Attribute("d")!;
                    shapes.Add(new BrandShape(BrandPart.Band, Geometry.Parse(outline).Bounds, 0, outline, fill, stroke, width, 0, default));
                    break;
                default:
                    break;
            }
        }
        // Parmi les rectangles droits : la fiche d'abord, le trait de la frise en dernier.
        var straight = shapes.Select((shape, index) => (shape, index)).Where(entry => entry.shape.Part == BrandPart.Line).Select(entry => entry.index).ToList();
        if (straight.Count > 0)
        {
            shapes[straight[0]] = shapes[straight[0]] with { Part = BrandPart.Sheet };
        }
        if (straight.Count > 1)
        {
            shapes[straight[^1]] = shapes[straight[^1]] with { Part = BrandPart.Rail };
        }
        return shapes;
    }

    private static double Number(XElement element, string name) =>
        (string?)element.Attribute(name) is { Length: > 0 } text ? double.Parse(text, CultureInfo.InvariantCulture) : 0;

    private static Color? Colour(XElement element, string name) =>
        (string?)element.Attribute(name) is { Length: > 0 } text ? (Color)ColorConverter.ConvertFromString(text) : null;

    /// <summary>« rotate(-15 128 256) » : l'angle, puis le point autour duquel la pièce tourne.</summary>
    private static (double Angle, Point Pivot) Rotation(string? transform)
    {
        const string start = "rotate(";
        if (transform is null || !transform.StartsWith(start, StringComparison.Ordinal))
        {
            return (0, default);
        }
        var values = transform[start.Length..].TrimEnd(')').Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToList();
        return (values[0], values.Count >= 3 ? new Point(values[1], values[2]) : default);
    }

    private static string Text()
    {
        using var stream = typeof(BrandDrawing).Assembly.GetManifestResourceStream(Resource);
        if (stream is null)
        {
            return string.Empty;
        }
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
