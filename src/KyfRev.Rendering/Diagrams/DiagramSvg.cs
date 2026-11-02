using System.Globalization;
using System.Text;

namespace KyfRev.Rendering.Diagrams;

/// <summary>
/// Un dessin en SVG, pour l'éditeur. Ses couleurs sont des variables CSS : il suit le thème de la page. Tout le texte est
/// échappé : un contenu de fiche n'y devient jamais une balise.
/// </summary>
public static class DiagramSvg
{
    private const double ArrowLength = 10;

    /// <summary>Le SVG d'un dessin.</summary>
    public static string Write(DiagramScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var svg = new StringBuilder();
        svg.Append(Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {scene.Width} {scene.Height}\" role=\"img\" aria-label=\"{Escape(scene.Description)}\">"));
        foreach (var shape in scene.Shapes)
        {
            switch (shape)
            {
                case DiagramRectangle box:
                    svg.Append(Invariant($"<rect x=\"{box.X:0.##}\" y=\"{box.Y:0.##}\" width=\"{box.Width:0.##}\" height=\"{box.Height:0.##}\" rx=\"{box.Radius:0.##}\" {Fill(box.Tone)}/>"));
                    break;
                case DiagramEllipse ellipse:
                    svg.Append(Invariant($"<ellipse cx=\"{ellipse.CenterX:0.##}\" cy=\"{ellipse.CenterY:0.##}\" rx=\"{ellipse.RadiusX:0.##}\" ry=\"{ellipse.RadiusY:0.##}\" fill-opacity=\"{ellipse.Opacity:0.##}\" {Fill(ellipse.Tone)}/>"));
                    break;
                case DiagramPolygon polygon:
                    var points = string.Join(' ', polygon.Points.Select(point => Invariant($"{point.X:0.##},{point.Y:0.##}")));
                    svg.Append(Invariant($"<polygon points=\"{points}\" {Fill(polygon.Tone)}/>"));
                    break;
                case DiagramLine line:
                    WriteLine(svg, line);
                    break;
                case DiagramText text:
                    WriteText(svg, text);
                    break;
            }
        }
        svg.Append("</svg>");
        return svg.ToString();
    }

    private static void WriteLine(StringBuilder svg, DiagramLine line)
    {
        svg.Append(Invariant($"<line x1=\"{line.From.X:0.##}\" y1=\"{line.From.Y:0.##}\" x2=\"{line.To.X:0.##}\" y2=\"{line.To.Y:0.##}\" stroke=\"var(--texte-secondaire)\" stroke-width=\"1.5\"/>"));
        if (line.HasArrow)
        {
            var angle = Math.Atan2(line.To.Y - line.From.Y, line.To.X - line.From.X);
            var (x, y) = (line.To.X, line.To.Y);
            svg.Append(Invariant($"<polygon points=\"{x:0.##},{y:0.##} {x - (ArrowLength * Math.Cos(angle - 0.5)):0.##},{y - (ArrowLength * Math.Sin(angle - 0.5)):0.##} {x - (ArrowLength * Math.Cos(angle + 0.5)):0.##},{y - (ArrowLength * Math.Sin(angle + 0.5)):0.##}\" fill=\"var(--texte-secondaire)\"/>"));
        }
    }

    private static void WriteText(StringBuilder svg, DiagramText text)
    {
        var color = text.Tone switch
        {
            DiagramTone.Main => "var(--actif-texte)",
            DiagramTone.Accent => "var(--pastille-texte)",
            _ => "var(--texte)",
        };
        var spacing = text.Size * 1.2;
        var first = text.CenterY - ((text.Lines.Length - 1) * spacing / 2);
        svg.Append(Invariant($"<text text-anchor=\"middle\" dominant-baseline=\"central\" font-size=\"{text.Size:0.##}\" fill=\"{color}\">"));
        for (var i = 0; i < text.Lines.Length; i++)
        {
            svg.Append(Invariant($"<tspan x=\"{text.CenterX:0.##}\" y=\"{first + (i * spacing):0.##}\">{Escape(text.Lines[i])}</tspan>"));
        }
        svg.Append("</text>");
    }

    private static string Fill(DiagramTone tone) => tone switch
    {
        DiagramTone.Main => "fill=\"var(--actif-fond)\" stroke=\"var(--principale)\"",
        DiagramTone.Accent => "fill=\"var(--pastille-fond)\" stroke=\"var(--pastille-texte)\"",
        _ => "fill=\"var(--surface-2)\" stroke=\"var(--texte-secondaire)\"",
    };

    private static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
