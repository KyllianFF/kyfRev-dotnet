using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace KyfRev.Rendering.Diagrams;

/// <summary>Ce que propose un modèle Mermaid : un schéma, ou un graphique.</summary>
public enum MermaidFamily
{
    /// <summary>Un schéma : des éléments.</summary>
    Schema,

    /// <summary>Un graphique : des valeurs.</summary>
    Chart,
}

/// <summary>Un modèle de schéma ou de graphique.</summary>
/// <param name="Id">Son nom.</param>
/// <param name="Name">Son nom à l'écran.</param>
/// <param name="Family">Schéma ou graphique.</param>
/// <param name="Category">Sa catégorie.</param>
/// <param name="Description">À quoi il sert.</param>
/// <param name="Sample">Un exemple, un élément par ligne.</param>
public sealed record MermaidTemplate(string Id, string Name, MermaidFamily Family, string Category, string Description, string Sample);

/// <summary>
/// Les schémas et les graphiques : du code Mermaid, lisible et modifiable partout. La galerie l'écrit à partir d'une ligne par
/// élément ; chaque texte y est protégé, pour qu'il ne soit jamais lu comme de la syntaxe.
/// </summary>
public static class MermaidTemplates
{
    /// <summary>Le langage de ces blocs.</summary>
    public const string Language = "mermaid";

    private const double ThumbnailWidth = 480;
    private const double ThumbnailHeight = 300;

    /// <summary>Les modèles, dans l'ordre de la galerie.</summary>
    public static ImmutableArray<MermaidTemplate> All { get; } =
    [
        new("etapes", "Étapes", MermaidFamily.Schema, "Flux", "Un enchaînement de causes ou d'étapes.", "Crise\nÉtats généraux\nBastille"),
        new("echanges", "Échanges", MermaidFamily.Schema, "Flux", "Qui envoie quoi à qui.", "Client\nServeur"),
        new("etats", "États", MermaidFamily.Schema, "Flux", "Les états d'une chose.", "Nouvelle\nRevue\nArchivée"),
        new("classes", "Classes", MermaidFamily.Schema, "Données", "Des objets et leurs liens.", "Fiche\nPassage"),
        new("entites", "Entités", MermaidFamily.Schema, "Données", "Les tables d'une base.", "Fiche\nPassage"),
        new("planning", "Planning", MermaidFamily.Schema, "Organisation", "Des tâches sur un calendrier.", "Mitose\nMéiose\nRévision"),
        new("taches", "Tableau de tâches", MermaidFamily.Schema, "Organisation", "À faire, en cours, fait.", "À faire\nEn cours\nFait"),
        new("carte", "Carte mentale", MermaidFamily.Schema, "Idées", "Un thème et ses branches.", "Cellule\nNoyau\nADN\nMembrane\nCytoplasme"),
        new("causes", "Causes et effet", MermaidFamily.Schema, "Idées", "Ce qui mène à un problème : le résultat, puis ses causes.", "Mauvaise note\nMéthode\nTemps"),
        new("secteurs", "Secteurs", MermaidFamily.Chart, "Secteurs", "Des parts d'un total.", "Clergé : 10\nNoblesse : 20\nTiers état : 70"),
        new("colonnes", "Colonnes", MermaidFamily.Chart, "Colonnes", "Comparer des valeurs.", "1700 : 21\n1750 : 24\n1789 : 28"),
        new("barres", "Barres", MermaidFamily.Chart, "Barres", "Les mêmes, à l'horizontale.", "1700 : 21\n1750 : 24\n1789 : 28"),
        new("courbes", "Courbes", MermaidFamily.Chart, "Courbes", "Une évolution dans le temps.", "1700 : 21\n1750 : 24\n1789 : 28"),
        new("mixte", "Colonnes et courbe", MermaidFamily.Chart, "Courbes", "Deux mesures sur un seul axe : libellé, colonne, courbe.", "1700 : 21 : 18\n1750 : 24 : 22\n1789 : 28 : 30"),
        new("carte-proportionnelle", "Carte proportionnelle", MermaidFamily.Chart, "Autres", "Des surfaces selon les valeurs.", "Cytoplasme : 60\nNoyau : 30\nMembrane : 10"),
    ];

    /// <summary>Le code d'un modèle ; rien si le modèle n'existe pas, ou s'il n'y a rien à dessiner.</summary>
    /// <param name="template">Le nom du modèle.</param>
    /// <param name="text">Les éléments, un par ligne.</param>
    public static string? Code(string? template, string text)
    {
        var items = SmartArt.Items(text);
        if (items.IsEmpty)
        {
            return null;
        }
        return template switch
        {
            "etapes" => Chain("flowchart LR", items),
            "echanges" => Exchanges(items),
            "etats" => States(items),
            "classes" => Classes(items),
            "entites" => Entities(items),
            "planning" => Schedule(items),
            "taches" => Board(items),
            "carte" => MindMap(items),
            "causes" => Causes(items),
            "secteurs" => Pie(items),
            "colonnes" => Xy(items, "xychart-beta", bars: true, line: false),
            "barres" => Xy(items, "xychart-beta horizontal", bars: true, line: false),
            "courbes" => Xy(items, "xychart-beta", bars: false, line: true),
            "mixte" => Xy(items, "xychart-beta", bars: true, line: true),
            "carte-proportionnelle" => Treemap(items),
            _ => null,
        };
    }

    /// <summary>La vignette d'un modèle : un dessin de kyfRev, immédiat, sans moteur.</summary>
    public static DiagramScene Thumbnail(MermaidTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var scene = new SceneBuilder();
        var items = SmartArt.Items(template.Sample);
        switch (template.Id)
        {
            case "etapes":
                return SmartArt.Draw("processus", template.Sample)!;
            case "carte":
                return SmartArt.Draw("radial", template.Sample)!;
            case "entites":
                scene.Box(28, 88, 172, 124, items[0], DiagramTone.Main);
                scene.Box(280, 88, 172, 124, items[1], DiagramTone.Main);
                scene.Line(200, 148, 280, 148);
                scene.Line(256, 148, 280, 128);
                scene.Line(256, 148, 280, 168);
                break;
            case "classes":
                for (var i = 0; i < 2; i++)
                {
                    scene.Box(28 + (i * 244), 60, 180, 180, string.Empty, DiagramTone.Main);
                    scene.Label(118 + (i * 244), 88, 170, 40, items[i], DiagramTone.Main);
                    scene.Line(28 + (i * 244), 116, 208 + (i * 244), 116);
                }
                scene.Arrow(208, 150, 272, 150);
                break;
            case "echanges":
                scene.Box(32, 16, 176, 56, items[0], DiagramTone.Main);
                scene.Box(272, 16, 176, 56, items[1], DiagramTone.Main);
                scene.Line(120, 72, 120, 284);
                scene.Line(360, 72, 360, 284);
                scene.Arrow(120, 140, 360, 140);
                scene.Arrow(360, 216, 120, 216);
                break;
            case "etats":
                scene.Circle(240, 16, 10, string.Empty, DiagramTone.Accent);
                for (var i = 0; i < items.Length; i++)
                {
                    var y = 44 + (i * 86);
                    scene.Arrow(240, y - 18, 240, y);
                    scene.Box(120, y, 240, 62, items[i], DiagramTone.Main);
                }
                break;
            case "planning":
                scene.Line(132, 20, 132, 284);
                for (var i = 0; i < items.Length; i++)
                {
                    var y = 44 + (i * 76);
                    scene.Label(66, y + 24, 124, 40, items[i], null);
                    scene.Shapes.Add(new DiagramRectangle(140 + (i * 88), y, 160, 48, 6, i % 2 == 0 ? DiagramTone.Main : DiagramTone.Accent));
                }
                break;
            case "taches":
                for (var i = 0; i < items.Length; i++)
                {
                    var x = 16 + (i * 154);
                    scene.Shapes.Add(new DiagramRectangle(x, 16, 140, 268, 8, DiagramTone.Quiet));
                    scene.Label(x + 70, 44, 130, 36, items[i], DiagramTone.Quiet);
                    scene.Shapes.Add(new DiagramRectangle(x + 12, 72, 116, 52, 6, DiagramTone.Main));
                    if (i == 0)
                    {
                        scene.Shapes.Add(new DiagramRectangle(x + 12, 136, 116, 52, 6, DiagramTone.Main));
                    }
                }
                break;
            case "causes":
                scene.Arrow(20, 150, 340, 150);
                scene.Box(340, 104, 128, 92, items[0], DiagramTone.Accent);
                scene.Line(100, 40, 180, 150);
                scene.Line(220, 40, 300, 150);
                scene.Line(100, 260, 180, 150);
                scene.Line(220, 260, 300, 150);
                scene.Label(100, 24, 150, 30, items[1], null);
                scene.Label(100, 276, 150, 30, items[2], null);
                break;
            case "secteurs":
                Sectors(scene, Values(items));
                break;
            case "colonnes":
                Columns(scene, Values(items));
                break;
            case "barres":
                Bars(scene, Values(items));
                break;
            case "courbes":
                Curve(scene, Values(items), 1);
                break;
            case "mixte":
                Columns(scene, Values(items));
                Curve(scene, Values(items), 2);
                break;
            default:
                Proportions(scene, Values(items));
                break;
        }
        return new DiagramScene(ThumbnailWidth, ThumbnailHeight, [.. scene.Shapes], template.Name);
    }

    private static void Sectors(SceneBuilder scene, List<Measure> values)
    {
        var total = values.Sum(value => value.First);
        var angle = -Math.PI / 2;
        var rank = 0;
        foreach (var value in values)
        {
            var sweep = value.First / total * 2 * Math.PI;
            var steps = Math.Max(2, (int)(sweep * 12));
            var points = new List<DiagramPoint> { new(240, 150) };
            for (var i = 0; i <= steps; i++)
            {
                var a = angle + (sweep * i / steps);
                points.Add(new DiagramPoint(240 + (130 * Math.Cos(a)), 150 + (130 * Math.Sin(a))));
            }
            scene.Shapes.Add(new DiagramPolygon([.. points], (DiagramTone)(rank % 3)));
            angle += sweep;
            rank++;
        }
    }

    private static void Columns(SceneBuilder scene, List<Measure> values)
    {
        var largest = values.Max(value => value.First);
        var width = 400.0 / values.Count;
        scene.Line(40, 268, 452, 268);
        for (var i = 0; i < values.Count; i++)
        {
            var height = values[i].First / largest * 230;
            scene.Shapes.Add(new DiagramRectangle(52 + (i * width), 268 - height, width - 24, height, 3, DiagramTone.Main));
        }
    }

    private static void Bars(SceneBuilder scene, List<Measure> values)
    {
        var largest = values.Max(value => value.First);
        var height = 248.0 / values.Count;
        scene.Line(80, 20, 80, 280);
        for (var i = 0; i < values.Count; i++)
        {
            scene.Shapes.Add(new DiagramRectangle(80, 28 + (i * height), values[i].First / largest * 370, height - 18, 3, DiagramTone.Main));
        }
    }

    private static void Curve(SceneBuilder scene, List<Measure> values, int series)
    {
        double Of(Measure value) => series == 1 ? value.First : value.Second;
        var largest = values.Max(value => Math.Max(value.First, value.Second));
        var step = 380.0 / Math.Max(1, values.Count - 1);
        if (series == 1)
        {
            scene.Line(40, 268, 452, 268);
            scene.Line(40, 20, 40, 268);
        }
        for (var i = 1; i < values.Count; i++)
        {
            scene.Line(52 + ((i - 1) * step), 268 - (Of(values[i - 1]) / largest * 230), 52 + (i * step), 268 - (Of(values[i]) / largest * 230));
        }
    }

    private static void Proportions(SceneBuilder scene, List<Measure> values)
    {
        var total = values.Sum(value => value.First);
        var x = 12.0;
        for (var i = 0; i < values.Count; i++)
        {
            var width = values[i].First / total * 456;
            scene.Box(x, 20, width, 260, values[i].Label, (DiagramTone)(i % 3));
            x += width;
        }
    }

    /// <summary>Un texte entre guillemets, où rien n'est de la syntaxe.</summary>
    private static string Quoted(string text) =>
        "\"" + text.Replace("\"", "#quot;", StringComparison.Ordinal) + "\"";

    /// <summary>Un texte sans guillemets possibles : tout ce qui serait de la syntaxe disparaît.</summary>
    private static string Plain(string text)
    {
        var clean = new StringBuilder();
        foreach (var character in text)
        {
            clean.Append(char.IsLetterOrDigit(character) || character is ' ' or '-' or '\'' ? character : ' ');
        }
        var result = clean.ToString().Trim();
        return result.Length > 0 ? result : "?";
    }

    private static string Chain(string header, ImmutableArray<string> items)
    {
        var code = new StringBuilder(header).Append('\n');
        for (var i = 0; i < items.Length; i++)
        {
            code.Append(Invariant($"    n{i}[{Quoted(items[i])}]\n"));
        }
        for (var i = 1; i < items.Length; i++)
        {
            code.Append(Invariant($"    n{i - 1} --> n{i}\n"));
        }
        return code.ToString().TrimEnd('\n');
    }

    private static string Exchanges(ImmutableArray<string> items)
    {
        var code = new StringBuilder("sequenceDiagram\n");
        for (var i = 0; i < items.Length; i++)
        {
            code.Append(Invariant($"    participant p{i} as {Plain(items[i])}\n"));
        }
        for (var i = 1; i < items.Length; i++)
        {
            code.Append(Invariant($"    p{i - 1}->>p{i}: Demande\n"));
            code.Append(Invariant($"    p{i}-->>p{i - 1}: Réponse\n"));
        }
        return code.ToString().TrimEnd('\n');
    }

    private static string States(ImmutableArray<string> items)
    {
        var code = new StringBuilder("stateDiagram-v2\n");
        for (var i = 0; i < items.Length; i++)
        {
            code.Append(Invariant($"    state {Quoted(items[i])} as e{i}\n"));
        }
        code.Append("    [*] --> e0\n");
        for (var i = 1; i < items.Length; i++)
        {
            code.Append(Invariant($"    e{i - 1} --> e{i}\n"));
        }
        code.Append(Invariant($"    e{items.Length - 1} --> [*]"));
        return code.ToString();
    }

    private static string Classes(ImmutableArray<string> items)
    {
        var code = new StringBuilder("classDiagram\n");
        for (var i = 0; i < items.Length; i++)
        {
            code.Append(Invariant($"    class c{i}[{Quoted(items[i])}]\n"));
        }
        for (var i = 1; i < items.Length; i++)
        {
            code.Append(Invariant($"    c{i - 1} --> c{i}\n"));
        }
        return code.ToString().TrimEnd('\n');
    }

    private static string Entities(ImmutableArray<string> items)
    {
        var code = new StringBuilder("erDiagram\n");
        foreach (var item in items)
        {
            code.Append(Invariant($"    {Quoted(Plain(item))} {{\n    }}\n"));
        }
        for (var i = 1; i < items.Length; i++)
        {
            code.Append(Invariant($"    {Quoted(Plain(items[i - 1]))} ||--o{{ {Quoted(Plain(items[i]))} : \"lié à\"\n"));
        }
        return code.ToString().TrimEnd('\n');
    }

    private static string Schedule(ImmutableArray<string> items)
    {
        var code = new StringBuilder("gantt\n    dateFormat YYYY-MM-DD\n    axisFormat %d/%m\n");
        for (var i = 0; i < items.Length; i++)
        {
            var start = new DateOnly(2026, 1, 5).AddDays(i * 5);
            code.Append(Invariant($"    {Plain(items[i])} : t{i}, {start:yyyy-MM-dd}, 7d\n"));
        }
        return code.ToString().TrimEnd('\n');
    }

    private static string Board(ImmutableArray<string> items)
    {
        var code = new StringBuilder("kanban\n");
        for (var i = 0; i < items.Length; i++)
        {
            code.Append(Invariant($"    c{i}[{Plain(items[i])}]\n"));
            code.Append(Invariant($"        t{i}[Une tâche]\n"));
        }
        return code.ToString().TrimEnd('\n');
    }

    private static string MindMap(ImmutableArray<string> items)
    {
        var code = new StringBuilder("mindmap\n");
        code.Append(Invariant($"    root(({Plain(items[0])}))\n"));
        foreach (var item in items.Skip(1))
        {
            code.Append(Invariant($"        {Plain(item)}\n"));
        }
        return code.ToString().TrimEnd('\n');
    }

    private static string Causes(ImmutableArray<string> items)
    {
        var code = new StringBuilder("flowchart LR\n");
        code.Append(Invariant($"    effet[{Quoted(items[0])}]\n"));
        for (var i = 1; i < items.Length; i++)
        {
            code.Append(Invariant($"    c{i}[{Quoted(items[i])}] --> effet\n"));
        }
        return code.ToString().TrimEnd('\n');
    }

    private static string Pie(ImmutableArray<string> items)
    {
        var code = new StringBuilder("pie showData\n");
        foreach (var value in Values(items))
        {
            code.Append(Invariant($"    {Quoted(value.Label)} : {value.First}\n"));
        }
        return code.ToString().TrimEnd('\n');
    }

    private static string Xy(ImmutableArray<string> items, string header, bool bars, bool line)
    {
        var values = Values(items);
        var code = new StringBuilder(header).Append('\n');
        code.Append("    x-axis [").Append(string.Join(", ", values.Select(value => Quoted(value.Label)))).Append("]\n");
        // L'axe part de zéro : une différence de quelques unités ne doit pas paraître énorme.
        var largest = values.Max(value => Math.Max(value.First, value.Second));
        code.Append(Invariant($"    y-axis 0 --> {Math.Ceiling(largest * 1.1)}\n"));
        if (bars)
        {
            code.Append("    bar [").Append(string.Join(", ", values.Select(value => Invariant($"{value.First}")))).Append("]\n");
        }
        if (line)
        {
            code.Append("    line [").Append(string.Join(", ", values.Select(value => Invariant($"{(bars ? value.Second : value.First)}")))).Append("]\n");
        }
        return code.ToString().TrimEnd('\n');
    }

    private static string Treemap(ImmutableArray<string> items)
    {
        // Une section autour : Mermaid laisse transparent ce qui est à la racine.
        var code = new StringBuilder("treemap-beta\n    \"Répartition\"\n");
        foreach (var value in Values(items))
        {
            code.Append(Invariant($"        {Quoted(value.Label)}: {value.First}\n"));
        }
        return code.ToString().TrimEnd('\n');
    }

    /// <summary>« libellé : valeur : valeur » ; ce qui ne se lit pas vaut 1, pour que rien ne disparaisse.</summary>
    private static List<Measure> Values(ImmutableArray<string> items) =>
        [.. items.Select(item =>
        {
            var parts = item.Split(':', StringSplitOptions.TrimEntries);
            var first = parts.Length > 1 ? Number(parts[1]) : 1;
            return new Measure(parts[0], first, parts.Length > 2 ? Number(parts[2]) : first);
        })];

    private static double Number(string text)
    {
        var value = double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var read) ? read : 1;
        return double.IsFinite(value) && value > 0 ? value : 1;
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private readonly record struct Measure(string Label, double First, double Second);
}
