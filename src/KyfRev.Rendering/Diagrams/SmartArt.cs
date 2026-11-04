using System.Collections.Immutable;
using System.Globalization;

namespace KyfRev.Rendering.Diagrams;

/// <summary>Un modèle de SmartArt.</summary>
/// <param name="Id">Son nom dans le fichier.</param>
/// <param name="Name">Son nom à l'écran.</param>
/// <param name="Category">Sa catégorie.</param>
/// <param name="Description">À quoi il sert.</param>
/// <param name="Sample">Un exemple, un élément par ligne.</param>
/// <param name="Maximum">Le nombre d'éléments qu'il sait montrer.</param>
public sealed record SmartArtLayout(string Id, string Name, string Category, string Description, string Sample, int Maximum);

/// <summary>
/// Les SmartArt : kyfRev les dessine lui-même, car Mermaid ne sait faire ni un vrai cycle ni une vraie pyramide. Ils s'écrivent
/// en un bloc de code lisible partout : <c>```smartart cycle</c>, puis un élément par ligne.
/// </summary>
public static class SmartArt
{
    /// <summary>Le modèle de la hiérarchie de haut en bas.</summary>
    public const string OrganizationChartId = "organigramme";

    /// <summary>Le modèle de la hiérarchie de gauche à droite.</summary>
    public const string HorizontalHierarchyId = "horizontal";

    private const int MaximumInHierarchy = 64;

    /// <summary>Le langage de ces blocs.</summary>
    public const string Language = "smartart";

    private const double Width = 480;
    private const double Height = 300;

    /// <summary>Plus d'éléments que cela, dans un modèle sans limite propre, ne se lirait plus.</summary>
    private const int ManyItems = 16;

    /// <summary>Les modèles, dans l'ordre de la galerie.</summary>
    public static ImmutableArray<SmartArtLayout> Layouts { get; } =
    [
        new("blocs", "Blocs", "Liste", "Des idées sans ordre, en grille.", "Prophase\nMétaphase\nAnaphase\nTélophase", ManyItems),
        new("liste", "Liste verticale", "Liste", "Une colonne de points égaux.", "Prophase\nMétaphase\nAnaphase\nTélophase", ManyItems),
        new("hexagones", "Hexagones", "Liste", "Un thème entouré de ses idées.", "Cellule\nNoyau\nADN\nMembrane\nCytoplasme", 7),
        new("processus", "Processus", "Processus", "Des étapes reliées par des flèches.", "Prophase\nMétaphase\nAnaphase\nTélophase", ManyItems),
        new("chevrons", "Chevrons", "Processus", "Une progression continue.", "Prophase\nMétaphase\nAnaphase\nTélophase", 8),
        new("vertical", "Processus vertical", "Processus", "Des étapes de haut en bas.", "Prophase\nMétaphase\nAnaphase", 8),
        new("escalier", "Escalier", "Processus", "Une montée, marche après marche.", "Base\nNiveau 2\nNiveau 3\nSommet", 8),
        new("cycle", "Cycle", "Cycle", "Des étapes qui reviennent en boucle.", "G1\nS\nG2\nM", 10),
        new("radial", "Radial", "Cycle", "Un centre et ce qui en dépend.", "Cellule\nNoyau\nADN\nMembrane\nCytoplasme", 11),
        new(OrganizationChartId, "Organigramme", "Hiérarchie", "Un sommet et ce qui en dépend, sur autant de niveaux qu'il faut.", "Cellule\nNoyau\nCytoplasme\nMembrane", MaximumInHierarchy),
        new(HorizontalHierarchyId, "Hiérarchie horizontale", "Hiérarchie", "La même, de gauche à droite.", "Cellule\nNoyau\nCytoplasme\nMembrane", MaximumInHierarchy),
        new("ensembles", "Ensembles", "Relation", "Ce que des idées ont en commun.", "Mitose\nMéiose", 3),
        new("convergence", "Convergence", "Relation", "Plusieurs causes pour un résultat.", "Chaleur\nEnzyme\nSubstrat\nRéaction", 9),
        new("matrice", "Matrice 2 × 2", "Matrice", "Quatre cas selon deux critères.", "Urgent\nImportant\nLes deux\nAucun", 4),
        new("pyramide", "Pyramide", "Pyramide", "Des niveaux, du large au rare.", "Sommet\nMilieu\nBase", 7),
        new("entonnoir", "Entonnoir", "Pyramide", "Du général vers le particulier.", "Lire\nTrier\nRetenir", 7),
        new("chronologie", "Chronologie", "Chronologie", "Des événements dans le temps.", "1789\n1792\n1799\n1804", 10),
    ];

    /// <summary>Les éléments d'un bloc : une ligne, un élément ; les lignes vides ne comptent pas.</summary>
    public static ImmutableArray<string> Items(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0)];
    }

    /// <summary>Le dessin d'un bloc ; rien si le modèle n'existe pas : ce n'est alors qu'un bloc de code.</summary>
    /// <param name="layout">Le nom du modèle dans le fichier.</param>
    /// <param name="text">Le contenu du bloc.</param>
    public static DiagramScene? Draw(string? layout, string text)
    {
        var model = Layouts.FirstOrDefault(candidate => candidate.Id == layout);
        if (model is null)
        {
            return null;
        }
        if (IsHierarchy(model.Id))
        {
            var outline = DiagramOutline.Parse(text).Take(model.Maximum).ToList();
            var tree = new SceneBuilder();
            var (treeWidth, treeHeight) = outline.Count > 0 ? DiagramHierarchy.Lay(outline, tree, model.Id == HorizontalHierarchyId) : (Width, Height);
            var labels = string.Create(CultureInfo.InvariantCulture, $"{model.Name} : {string.Join(", ", outline.Select(item => item.Label))}");
            return new DiagramScene(treeWidth, treeHeight, [.. tree.Shapes], labels);
        }
        var items = Items(text);
        var shown = items.Take(model.Maximum).ToList();
        var scene = new SceneBuilder();
        if (shown.Count > 0)
        {
            Lay(model.Id, shown, scene);
        }
        var description = string.Create(CultureInfo.InvariantCulture, $"{model.Name} : {string.Join(", ", shown)}");
        return new DiagramScene(Width, Height, [.. scene.Shapes], description);
    }

    /// <summary>Ce qui s'écrit dans le fichier.</summary>
    /// <param name="layout">Le nom du modèle dans le fichier.</param>
    /// <param name="text">Les éléments, un par ligne.</param>
    /// <returns>Le texte après les accents graves, et le contenu du bloc.</returns>
    public static (string Info, string Code) Source(string layout, string text) =>
        ($"{Language} {layout}", IsHierarchy(layout) ? DiagramOutline.Write(DiagramOutline.Parse(text)) : string.Join('\n', Items(text)));

    /// <summary>Ce modèle montre des niveaux : ses lignes se décalent, et il a son propre éditeur.</summary>
    /// <param name="layout">Le nom du modèle dans le fichier.</param>
    public static bool IsHierarchy(string? layout) => layout is OrganizationChartId or HorizontalHierarchyId;

    private static void Lay(string id, List<string> items, SceneBuilder scene)
    {
        switch (id)
        {
            case "blocs":
                Blocks(items, scene);
                break;
            case "liste":
                VerticalList(items, scene);
                break;
            case "hexagones":
                Hexagons(items, scene);
                break;
            case "processus":
                Process(items, scene);
                break;
            case "chevrons":
                Chevrons(items, scene);
                break;
            case "vertical":
                VerticalProcess(items, scene);
                break;
            case "escalier":
                Stairs(items, scene);
                break;
            case "cycle":
                Cycle(items, scene);
                break;
            case "radial":
                Radial(items, scene);
                break;
            case "ensembles":
                Sets(items, scene);
                break;
            case "convergence":
                Convergence(items, scene);
                break;
            case "matrice":
                Matrix(items, scene);
                break;
            case "pyramide":
                Pyramid(items, scene, inverted: false);
                break;
            case "entonnoir":
                Pyramid(items, scene, inverted: true);
                break;
            default:
                Timeline(items, scene);
                break;
        }
    }

    private static DiagramTone Alternate(int rank) => rank % 2 == 0 ? DiagramTone.Main : DiagramTone.Accent;

    private static void Blocks(List<string> items, SceneBuilder scene)
    {
        var columns = items.Count switch
        {
            <= 2 => items.Count,
            <= 4 => 2,
            <= 9 => 3,
            _ => 4,
        };
        var rows = (int)Math.Ceiling((double)items.Count / columns);
        const double gap = 12;
        var width = (Width - 24 - ((columns - 1) * gap)) / columns;
        var height = (Height - 24 - ((rows - 1) * gap)) / rows;
        for (var i = 0; i < items.Count; i++)
        {
            scene.Box(12 + ((i % columns) * (width + gap)), 12 + ((i / columns) * (height + gap)), width, height, items[i], DiagramTone.Main);
        }
    }

    private static void VerticalList(List<string> items, SceneBuilder scene)
    {
        const double gap = 10;
        var height = (Height - 24 - ((items.Count - 1) * gap)) / items.Count;
        for (var i = 0; i < items.Count; i++)
        {
            scene.Box(60, 12 + (i * (height + gap)), 360, height, items[i], DiagramTone.Main);
        }
    }

    private static void Hexagons(List<string> items, SceneBuilder scene)
    {
        scene.Hexagon(Width / 2, Height / 2, 56, items[0], DiagramTone.Accent);
        var others = items.Count - 1;
        for (var i = 0; i < others; i++)
        {
            var angle = (-Math.PI / 2) + (i * 2 * Math.PI / others);
            scene.Hexagon((Width / 2) + (130 * Math.Cos(angle)), (Height / 2) + (98 * Math.Sin(angle)), 48, items[i + 1], DiagramTone.Main);
        }
    }

    private static void Process(List<string> items, SceneBuilder scene)
    {
        const int perRow = 5;
        const double arrow = 26;
        var rows = (int)Math.Ceiling((double)items.Count / perRow);
        var height = Math.Min(100, (Height - 24 - ((rows - 1) * 28)) / rows);
        var top = (Height - (rows * height) - ((rows - 1) * 28)) / 2;
        var centers = new List<(double X, double Y, double Width)>();
        for (var row = 0; row < rows; row++)
        {
            var count = Math.Min(perRow, items.Count - (row * perRow));
            var width = (Width - 16 - ((count - 1) * arrow)) / count;
            var y = top + (row * (height + 28));
            for (var i = 0; i < count; i++)
            {
                var x = 8 + (i * (width + arrow));
                scene.Box(x, y, width, height, items[(row * perRow) + i], DiagramTone.Main);
                if (i > 0)
                {
                    scene.Arrow(x - arrow, y + (height / 2), x, y + (height / 2));
                }
                centers.Add((x + (width / 2), y, width));
            }
            if (row > 0)
            {
                // D'une rangée à la suivante : un coude, du dernier élément au premier.
                var last = centers[(row * perRow) - 1];
                var first = centers[row * perRow];
                var middle = y - 14;
                scene.Line(last.X, last.Y + height, last.X, middle);
                scene.Line(last.X, middle, first.X, middle);
                scene.Arrow(first.X, middle, first.X, y);
            }
        }
    }

    private static void Chevrons(List<string> items, SceneBuilder scene)
    {
        const double notch = 24;
        var step = (Width - 8 - notch) / items.Count;
        for (var i = 0; i < items.Count; i++)
        {
            var x = 4 + (i * step);
            scene.Polygon(
                [new(x, 100), new(x + step, 100), new(x + step + notch, 150), new(x + step, 200), new(x, 200), new(x + notch, 150)],
                x + (step / 2) + notch,
                150,
                step - notch,
                90,
                items[i],
                Alternate(i));
        }
    }

    private static void VerticalProcess(List<string> items, SceneBuilder scene)
    {
        const double gap = 22;
        var height = (Height - 24 - ((items.Count - 1) * gap)) / items.Count;
        for (var i = 0; i < items.Count; i++)
        {
            var y = 12 + (i * (height + gap));
            scene.Box(100, y, 280, height, items[i], DiagramTone.Main);
            if (i > 0)
            {
                scene.Arrow(240, y - gap, 240, y);
            }
        }
    }

    private static void Stairs(List<string> items, SceneBuilder scene)
    {
        var width = (Width - 16) / items.Count;
        var step = (Height - 24) / items.Count;
        for (var i = 0; i < items.Count; i++)
        {
            var height = (i + 1) * step;
            scene.Bar(8 + (i * width), Height - 12 - height, width, height, step, items[i], Alternate(i));
        }
    }

    private static void Cycle(List<string> items, SceneBuilder scene)
    {
        var radius = Math.Clamp(260.0 / items.Count, 28, 50);
        var points = Around(items.Count, 180, 100);
        for (var i = 0; i < items.Count && items.Count > 1; i++)
        {
            var (from, to) = (points[i], points[(i + 1) % items.Count]);
            var angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
            scene.Arrow(
                from.X + ((radius + 3) * Math.Cos(angle)),
                from.Y + ((radius + 3) * Math.Sin(angle)),
                to.X - ((radius + 3) * Math.Cos(angle)),
                to.Y - ((radius + 3) * Math.Sin(angle)));
        }
        for (var i = 0; i < items.Count; i++)
        {
            scene.Circle(points[i].X, points[i].Y, radius, items[i], DiagramTone.Main);
        }
    }

    private static void Radial(List<string> items, SceneBuilder scene)
    {
        var others = items.Count - 1;
        if (others > 0)
        {
            var radius = Math.Clamp(250.0 / others, 28, 44);
            var points = Around(others, 180, 104);
            foreach (var point in points)
            {
                scene.Line(Width / 2, Height / 2, point.X, point.Y);
            }
            for (var i = 0; i < others; i++)
            {
                scene.Circle(points[i].X, points[i].Y, radius, items[i + 1], DiagramTone.Main);
            }
        }
        scene.Circle(Width / 2, Height / 2, 54, items[0], DiagramTone.Accent);
    }

    private static List<DiagramPoint> Around(int count, double radiusX, double radiusY)
    {
        var points = new List<DiagramPoint>();
        for (var i = 0; i < count; i++)
        {
            var angle = (-Math.PI / 2) + (i * 2 * Math.PI / count);
            points.Add(new DiagramPoint((Width / 2) + (radiusX * Math.Cos(angle)), (Height / 2) + (radiusY * Math.Sin(angle))));
        }
        return points;
    }

    private static void Sets(List<string> items, SceneBuilder scene)
    {
        switch (items.Count)
        {
            case 1:
                scene.Circle(240, 150, 130, items[0], DiagramTone.Main);
                break;
            case 2:
                scene.Translucent(176, 150, 124, DiagramTone.Main);
                scene.Translucent(304, 150, 124, DiagramTone.Accent);
                scene.Label(108, 150, 110, 100, items[0], null);
                scene.Label(372, 150, 110, 100, items[1], null);
                break;
            default:
                scene.Translucent(240, 104, 94, DiagramTone.Main);
                scene.Translucent(182, 198, 94, DiagramTone.Accent);
                scene.Translucent(298, 198, 94, DiagramTone.Quiet);
                scene.Label(240, 62, 120, 50, items[0], null);
                scene.Label(144, 228, 110, 50, items[1], null);
                scene.Label(336, 228, 110, 50, items[2], null);
                break;
        }
    }

    private static void Convergence(List<string> items, SceneBuilder scene)
    {
        var causes = items.Count - 1;
        if (causes == 0)
        {
            scene.Box(160, 100, 160, 100, items[0], DiagramTone.Accent);
            return;
        }
        const double gap = 10;
        var height = (Height - 24 - ((causes - 1) * gap)) / causes;
        for (var i = 0; i < causes; i++)
        {
            var y = 12 + (i * (height + gap));
            scene.Arrow(184, y + (height / 2), 292, 150);
            scene.Box(12, y, 172, height, items[i], DiagramTone.Main);
        }
        scene.Box(292, 100, 176, 100, items[^1], DiagramTone.Accent);
    }

    private static void Matrix(List<string> items, SceneBuilder scene)
    {
        for (var i = 0; i < 4; i++)
        {
            var text = i < items.Count ? items[i] : string.Empty;
            scene.Box(28 + ((i % 2) * 216), 16 + ((i / 2) * 136), 208, 128, text, i is 0 or 3 ? DiagramTone.Main : DiagramTone.Accent);
        }
    }

    private static void Pyramid(List<string> items, SceneBuilder scene, bool inverted)
    {
        const double top = 12;
        const double side = 224;
        var band = (Height - 24) / items.Count;
        double HalfWidth(double rank) => side * (inverted ? 1 - (rank / items.Count) : rank / items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var (upper, lower) = (top + (i * band), top + ((i + 1) * band));
            var (a, b) = (HalfWidth(i), HalfWidth(i + 1));
            var pointed = inverted ? i == items.Count - 1 : i == 0;
            // La pointe n'a de la place que vers sa base.
            var offset = pointed ? (inverted ? -band * 0.18 : band * 0.18) : 0;
            scene.Polygon(
                [new(Width / 2 - a, upper), new(Width / 2 + a, upper), new(Width / 2 + b, lower), new(Width / 2 - b, lower)],
                Width / 2,
                ((upper + lower) / 2) + offset,
                Math.Max(a, b) * (pointed ? 1 : 1.6),
                band * (pointed ? 0.6 : 0.9),
                items[i],
                Alternate(i));
        }
    }

    private static void Timeline(List<string> items, SceneBuilder scene)
    {
        scene.Arrow(12, 150, 468, 150);
        var width = Math.Min(150, 800.0 / items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var x = items.Count == 1 ? 240 : 60 + (i * (360.0 / (items.Count - 1)));
            var above = i % 2 == 0;
            scene.Line(x, 150, x, above ? 110 : 190);
            scene.Circle(x, 150, 7, string.Empty, DiagramTone.Accent);
            scene.Label(x, above ? 76 : 224, width, 64, items[i], null);
        }
    }
}
