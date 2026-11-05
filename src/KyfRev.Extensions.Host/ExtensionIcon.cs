using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Xml;
using KyfRev.Application;

namespace KyfRev.Extensions.Host;

/// <summary>
/// L'icône d'une extension, lue dans son fichier SVG : seulement des tracés, d'une seule couleur, celle du thème. Ni couleur, ni texte,
/// ni image, ni lien, ni script : l'application ne garde que la géométrie, qu'elle dessine elle-même comme ses propres icônes.
/// </summary>
/// <param name="Left">Le bord gauche de la boîte de vue.</param>
/// <param name="Top">Le bord haut de la boîte de vue.</param>
/// <param name="Width">Sa largeur.</param>
/// <param name="Height">Sa hauteur.</param>
/// <param name="Paths">Les tracés, dans la syntaxe des chemins SVG.</param>
/// <param name="Stroked">Les tracés sont des traits (<c>fill="none"</c>) ; sinon, des formes pleines.</param>
public sealed record ExtensionIcon(double Left, double Top, double Width, double Height, ImmutableArray<string> Paths, bool Stroked)
{
    private const int MaxBytes = 32 * 1024;
    private const int MaxPaths = 32;
    private const int MaxPathLength = 4096;

    /// <summary>Lit une icône SVG ; refusée si elle n'a pas de tracé lisible.</summary>
    /// <param name="svg">Le contenu du fichier.</param>
    public static Outcome<ExtensionIcon> Read(ReadOnlyMemory<byte> svg)
    {
        if (svg.Length > MaxBytes)
        {
            return Outcome.Refused<ExtensionIcon>("icône de plus de 32 Ko");
        }
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxBytes, IgnoreComments = true };
            using var reader = XmlReader.Create(new StringReader(Encoding.UTF8.GetString(svg.Span)), settings);
            return ReadSvg(reader);
        }
        catch (XmlException exception)
        {
            return Outcome.Refused<ExtensionIcon>($"icône illisible : {exception.Message}");
        }
    }

    private static Outcome<ExtensionIcon> ReadSvg(XmlReader reader)
    {
        if (!reader.ReadToFollowing("svg") || ViewBox(reader.GetAttribute("viewBox")) is not { } box)
        {
            return Outcome.Refused<ExtensionIcon>("icône illisible : un élément « svg » avec sa « viewBox » est attendu");
        }
        var stroked = reader.GetAttribute("fill") == "none";
        var paths = ImmutableArray.CreateBuilder<string>();
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }
            if (Shape(reader) is { } path)
            {
                if (path.Length > MaxPathLength || !path.All(IsPathCharacter) || paths.Count == MaxPaths)
                {
                    return Outcome.Refused<ExtensionIcon>("icône illisible : tracé trop long, trop nombreux ou invalide");
                }
                paths.Add(path);
            }
        }
        return paths.Count == 0
            ? Outcome.Refused<ExtensionIcon>("icône illisible : aucun tracé")
            : Outcome.Of(new ExtensionIcon(box[0], box[1], box[2], box[3], paths.ToImmutable(), stroked));
    }

    private static double[]? ViewBox(string? text)
    {
        var parts = (text ?? string.Empty).Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        var numbers = parts.Select(Number).ToArray();
        return numbers.Length == 4 && numbers.All(number => number is not null) && numbers[2] > 0 && numbers[3] > 0 ? [.. numbers.Select(number => number!.Value)] : null;
    }

    private static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;

    /// <summary>Le tracé d'une forme simple ; rien pour ce qui n'en est pas une.</summary>
    private static string? Shape(XmlReader reader)
    {
        string N(double value) => value.ToString(CultureInfo.InvariantCulture);
        double A(string name) => Number(reader.GetAttribute(name)) ?? 0;
        switch (reader.LocalName)
        {
            case "path":
                return reader.GetAttribute("d");
            case "line":
                return $"M{N(A("x1"))} {N(A("y1"))}L{N(A("x2"))} {N(A("y2"))}";
            case "polyline":
            case "polygon":
                var points = (reader.GetAttribute("points") ?? string.Empty).Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
                if (points.Length < 4 || points.Length % 2 != 0 || points.Any(point => Number(point) is null))
                {
                    return null;
                }
                var pairs = Enumerable.Range(0, points.Length / 2).Select(i => $"{points[2 * i]} {points[(2 * i) + 1]}");
                return "M" + string.Join("L", pairs) + (reader.LocalName == "polygon" ? "Z" : string.Empty);
            case "rect":
                var (x, y, w, h) = (A("x"), A("y"), A("width"), A("height"));
                return w > 0 && h > 0 ? $"M{N(x)} {N(y)}h{N(w)}v{N(h)}h{N(-w)}Z" : null;
            case "circle":
                var (cx, cy, r) = (A("cx"), A("cy"), A("r"));
                return r > 0 ? $"M{N(cx - r)} {N(cy)}a{N(r)} {N(r)} 0 1 0 {N(2 * r)} 0a{N(r)} {N(r)} 0 1 0 {N(-2 * r)} 0" : null;
            default:
                return null;
        }
    }

    private static bool IsPathCharacter(char c) => char.IsAsciiDigit(c) || c is ' ' or ',' or '.' or '-' or '+' or 'e' or 'E' or '\n' or '\r' or '\t' || "MmLlHhVvCcSsQqTtAaZz".Contains(c, StringComparison.Ordinal);
}
