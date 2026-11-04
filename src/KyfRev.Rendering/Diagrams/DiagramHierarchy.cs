namespace KyfRev.Rendering.Diagrams;

/// <summary>
/// Pose une hiérarchie : chaque feuille a sa place, chaque parent se centre sur ses enfants. Le dessin grandit avec ce
/// qu'il montre : c'est l'affichage qui le met à sa taille. Les boîtes sont posées dans l'ordre des éléments, après les traits.
/// </summary>
internal static class DiagramHierarchy
{
    private const double SmallestWidth = 480;
    private const double SmallestHeight = 300;
    private const double Margin = 12;
    private const double BranchGap = 44;
    private const double LeafGap = 12;
    private const double NarrowestLeaf = 132;
    private const double WidestLeaf = 200;
    private const double LowestLeaf = 56;
    private const double HighestLeaf = 96;
    private const double ColumnWidth = 170;
    private const double WidestColumn = 220;
    private const double RowHeight = 76;
    private const double HighestRow = 92;

    /// <summary>Pose les éléments, de haut en bas ou de gauche à droite.</summary>
    /// <returns>La taille du dessin.</returns>
    public static (double Width, double Height) Lay(IReadOnlyList<OutlineItem> items, SceneBuilder scene, bool horizontal)
    {
        var nodes = Place(items);
        var leaves = nodes.Count(node => node.Children.Count == 0);
        var depth = nodes.Max(node => node.Item.Level) + 1;
        return horizontal ? LayAcross(nodes, leaves, depth, scene) : LayDown(nodes, leaves, depth, scene);
    }

    private static (double Width, double Height) LayDown(List<Node> nodes, int leaves, int depth, SceneBuilder scene)
    {
        var width = Math.Max(SmallestWidth, (2 * Margin) + (leaves * NarrowestLeaf) + ((leaves - 1) * LeafGap));
        var box = Math.Min(WidestLeaf, (width - (2 * Margin) - ((leaves - 1) * LeafGap)) / leaves);
        var used = (leaves * box) + ((leaves - 1) * LeafGap);
        var height = depth <= 2 ? HighestRow : RowHeight;
        var total = Math.Max(SmallestHeight, (2 * Margin) + (depth * height) + ((depth - 1) * BranchGap));
        var left = (width - used) / 2;
        var top = (total - (depth * height) - ((depth - 1) * BranchGap)) / 2;
        double X(Node node) => left + (node.Place * (box + LeafGap));
        double Y(Node node) => top + (node.Item.Level * (height + BranchGap));
        foreach (var parent in nodes.Where(node => node.Children.Count > 0))
        {
            var center = X(parent) + (box / 2);
            var bottom = Y(parent) + height;
            var bus = bottom + (BranchGap / 2);
            scene.Line(center, bottom, center, bus);
            foreach (var child in parent.Children)
            {
                var childCenter = X(child) + (box / 2);
                scene.Line(center, bus, childCenter, bus);
                scene.Line(childCenter, bus, childCenter, Y(child));
            }
        }
        foreach (var node in nodes)
        {
            scene.Box(X(node), Y(node), box, height, node.Item.Label, ToneOf(node.Item.Level));
        }
        return (width, total);
    }

    private static (double Width, double Height) LayAcross(List<Node> nodes, int leaves, int depth, SceneBuilder scene)
    {
        var total = Math.Max(SmallestHeight, (2 * Margin) + (leaves * LowestLeaf) + ((leaves - 1) * LeafGap));
        var box = Math.Min(HighestLeaf, (total - (2 * Margin) - ((leaves - 1) * LeafGap)) / leaves);
        var used = (leaves * box) + ((leaves - 1) * LeafGap);
        var width = Math.Max(SmallestWidth, (2 * Margin) + (depth * ColumnWidth) + ((depth - 1) * BranchGap));
        var column = Math.Min(WidestColumn, (width - (2 * Margin) - ((depth - 1) * BranchGap)) / depth);
        var left = (width - (depth * column) - ((depth - 1) * BranchGap)) / 2;
        var top = (total - used) / 2;
        double X(Node node) => left + (node.Item.Level * (column + BranchGap));
        double Y(Node node) => top + (node.Place * (box + LeafGap));
        foreach (var parent in nodes.Where(node => node.Children.Count > 0))
        {
            var center = Y(parent) + (box / 2);
            var right = X(parent) + column;
            var bus = right + (BranchGap / 2);
            scene.Line(right, center, bus, center);
            foreach (var child in parent.Children)
            {
                var childCenter = Y(child) + (box / 2);
                scene.Line(bus, center, bus, childCenter);
                scene.Line(bus, childCenter, X(child), childCenter);
            }
        }
        foreach (var node in nodes)
        {
            scene.Box(X(node), Y(node), column, box, node.Item.Label, ToneOf(node.Item.Level));
        }
        return (width, total);
    }

    /// <summary>Le sommet se distingue, et les niveaux suivants alternent pour se lire d'un coup d'œil.</summary>
    private static DiagramTone ToneOf(int level) => level switch
    {
        0 => DiagramTone.Accent,
        _ when level % 2 == 1 => DiagramTone.Main,
        _ => DiagramTone.Quiet,
    };

    /// <summary>Relie chaque élément à son parent, puis donne leur place aux feuilles et aux parents.</summary>
    private static List<Node> Place(IReadOnlyList<OutlineItem> items)
    {
        var nodes = items.Select(item => new Node(item)).ToList();
        var open = new Stack<Node>();
        var roots = new List<Node>();
        foreach (var node in nodes)
        {
            while (open.Count > node.Item.Level)
            {
                open.Pop();
            }
            (open.Count > 0 ? open.Peek().Children : roots).Add(node);
            open.Push(node);
        }
        var nextLeaf = 0d;
        foreach (var root in roots)
        {
            nextLeaf = Place(root, nextLeaf);
        }
        return nodes;
    }

    private static double Place(Node node, double nextLeaf)
    {
        if (node.Children.Count == 0)
        {
            node.Place = nextLeaf;
            return nextLeaf + 1;
        }
        foreach (var child in node.Children)
        {
            nextLeaf = Place(child, nextLeaf);
        }
        node.Place = (node.Children[0].Place + node.Children[^1].Place) / 2;
        return nextLeaf;
    }

    private sealed class Node(OutlineItem item)
    {
        public OutlineItem Item { get; } = item;

        public List<Node> Children { get; } = [];

        public double Place { get; set; }
    }
}
