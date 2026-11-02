using System.Collections.Immutable;

namespace KyfRev.Rendering.Diagrams;

/// <summary>Pose les formes d'un dessin, et coupe leur texte pour qu'il y tienne.</summary>
internal sealed class SceneBuilder
{
    /// <summary>Largeur moyenne d'un caractère, par rapport à la taille de la police.</summary>
    private const double CharacterWidth = 0.56;

    private const double LineSpacing = 1.2;
    private const double LargestText = 17;
    private const double SmallestText = 10;

    public List<DiagramShape> Shapes { get; } = [];

    public void Box(double x, double y, double width, double height, string text, DiagramTone tone)
    {
        Shapes.Add(new DiagramRectangle(x, y, width, height, 8, tone));
        Label(x + (width / 2), y + (height / 2), width - 10, height - 6, text, tone);
    }

    public void Circle(double x, double y, double radius, string text, DiagramTone tone)
    {
        Shapes.Add(new DiagramEllipse(x, y, radius, radius, tone));
        if (text.Length > 0)
        {
            Label(x, y, radius * 1.6, radius * 1.3, text, tone);
        }
    }

    public void Translucent(double x, double y, double radius, DiagramTone tone) =>
        Shapes.Add(new DiagramEllipse(x, y, radius, radius, tone, 0.65));

    public void Hexagon(double x, double y, double radius, string text, DiagramTone tone)
    {
        var points = Enumerable.Range(0, 6)
            .Select(k => new DiagramPoint(x + (radius * Math.Cos((Math.PI / 6) + (k * Math.PI / 3))), y + (radius * Math.Sin((Math.PI / 6) + (k * Math.PI / 3)))))
            .ToImmutableArray();
        Polygon(points, x, y, radius * 1.5, radius * 1.1, text, tone);
    }

    public void Polygon(ImmutableArray<DiagramPoint> points, double x, double y, double width, double height, string text, DiagramTone tone)
    {
        Shapes.Add(new DiagramPolygon(points, tone));
        Label(x, y, width, height, text, tone);
    }

    /// <summary>Une barre dont le texte se tient en haut, dans sa première marche.</summary>
    public void Bar(double x, double y, double width, double height, double step, string text, DiagramTone tone)
    {
        Shapes.Add(new DiagramRectangle(x, y, width, height, 3, tone));
        Label(x + (width / 2), y + (step / 2), width - 8, step - 4, text, tone);
    }

    public void Line(double x1, double y1, double x2, double y2) =>
        Shapes.Add(new DiagramLine(new DiagramPoint(x1, y1), new DiagramPoint(x2, y2), HasArrow: false));

    public void Arrow(double x1, double y1, double x2, double y2) =>
        Shapes.Add(new DiagramLine(new DiagramPoint(x1, y1), new DiagramPoint(x2, y2), HasArrow: true));

    /// <summary>
    /// Le plus grand texte qui tient dans la boîte, sans couper de mot si possible ; à la plus petite taille, ce qui dépasse
    /// finit par « … ».
    /// </summary>
    public void Label(double x, double y, double width, double height, string text, DiagramTone? tone)
    {
        if (text.Length == 0)
        {
            return;
        }
        foreach (var breakingWords in new[] { false, true })
        {
            for (var size = LargestText; size >= SmallestText; size--)
            {
                var fits = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => word.Length <= CharactersIn(width, size));
                var lines = Wrap(text, width, size);
                if ((fits || breakingWords) && lines.Count * size * LineSpacing <= height)
                {
                    Shapes.Add(new DiagramText(x, y, [.. lines], size, tone));
                    return;
                }
            }
        }
        var fitting = Math.Max(1, (int)(height / (SmallestText * LineSpacing)));
        var wrapped = Wrap(text, width, SmallestText);
        var kept = wrapped.Take(fitting).ToList();
        var limit = CharactersIn(width, SmallestText);
        var last = kept[^1];
        kept[^1] = last.Length >= limit ? string.Concat(last.AsSpan(0, Math.Max(1, limit - 1)), "…") : last + "…";
        Shapes.Add(new DiagramText(x, y, [.. kept], SmallestText, tone));
    }

    private static int CharactersIn(double width, double size) => Math.Max(1, (int)(width / (size * CharacterWidth)));

    /// <summary>Coupe aux espaces ; un mot trop long pour la ligne se coupe où il faut.</summary>
    private static List<string> Wrap(string text, double width, double size)
    {
        var limit = CharactersIn(width, size);
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var rest = word;
            if (current.Length > 0 && current.Length + 1 + rest.Length <= limit)
            {
                current += " " + rest;
                continue;
            }
            if (current.Length > 0)
            {
                lines.Add(current);
                current = string.Empty;
            }
            while (rest.Length > limit)
            {
                lines.Add(rest[..limit]);
                rest = rest[limit..];
            }
            current = rest;
        }
        if (current.Length > 0)
        {
            lines.Add(current);
        }
        return lines.Count > 0 ? lines : [string.Empty];
    }
}
