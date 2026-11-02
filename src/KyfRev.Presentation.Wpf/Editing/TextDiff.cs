namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Ce qui distingue une ligne d'une version à l'autre.</summary>
public enum DiffKind
{
    /// <summary>La même des deux côtés.</summary>
    Same,

    /// <summary>Modifiée : les deux côtés diffèrent.</summary>
    Changed,

    /// <summary>Seulement à gauche.</summary>
    Removed,

    /// <summary>Seulement à droite.</summary>
    Added,
}

/// <summary>Une ligne de la comparaison, ses deux côtés en face l'un de l'autre.</summary>
/// <param name="Left">Le texte à gauche ; rien s'il n'y en a pas.</param>
/// <param name="Right">Le texte à droite ; rien s'il n'y en a pas.</param>
/// <param name="Kind">Ce qui les distingue.</param>
public sealed record DiffRow(string? Left, string? Right, DiffKind Kind)
{
    /// <summary>Le côté gauche diffère.</summary>
    public bool IsLeftMarked => Kind is DiffKind.Changed or DiffKind.Removed;

    /// <summary>Le côté droit diffère.</summary>
    public bool IsRightMarked => Kind is DiffKind.Changed or DiffKind.Added;

    /// <summary>Le signe du côté gauche : la couleur ne le dit pas seule.</summary>
    public string LeftSign => IsLeftMarked ? "−" : string.Empty;

    /// <summary>Le signe du côté droit : la couleur ne le dit pas seule.</summary>
    public string RightSign => IsRightMarked ? "+" : string.Empty;
}

/// <summary>Compare deux textes ligne à ligne : la plus longue suite commune, comme <c>diff</c>.</summary>
public static class TextDiff
{
    /// <summary>Au-delà, comparer coûterait trop : ce qui diffère est montré d'un bloc.</summary>
    private const long MaximumCells = 4_000_000;

    /// <summary>Les lignes de la comparaison.</summary>
    public static IReadOnlyList<DiffRow> Rows(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var (a, b) = (left.Split('\n'), right.Split('\n'));
        var start = 0;
        while (start < a.Length && start < b.Length && a[start] == b[start])
        {
            start++;
        }
        var end = 0;
        while (end < a.Length - start && end < b.Length - start && a[a.Length - 1 - end] == b[b.Length - 1 - end])
        {
            end++;
        }
        var rows = new List<DiffRow>(Math.Max(a.Length, b.Length));
        rows.AddRange(a.Take(start).Select(line => new DiffRow(line, line, DiffKind.Same)));
        var (middleLeft, middleRight) = (a[start..(a.Length - end)], b[start..(b.Length - end)]);
        if ((long)middleLeft.Length * middleRight.Length > MaximumCells)
        {
            AddChanges(rows, middleLeft, middleRight);
        }
        else
        {
            AddCompared(rows, middleLeft, middleRight);
        }
        rows.AddRange(a.Skip(a.Length - end).Select(line => new DiffRow(line, line, DiffKind.Same)));
        return rows;
    }

    private static void AddCompared(List<DiffRow> rows, string[] a, string[] b)
    {
        var lengths = new int[a.Length + 1][];
        for (var i = 0; i <= a.Length; i++)
        {
            lengths[i] = new int[b.Length + 1];
        }
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                lengths[i][j] = a[i] == b[j] ? lengths[i + 1][j + 1] + 1 : Math.Max(lengths[i + 1][j], lengths[i][j + 1]);
            }
        }
        var (x, y) = (0, 0);
        var (removed, added) = (new List<string>(), new List<string>());
        while (x < a.Length || y < b.Length)
        {
            if (x < a.Length && y < b.Length && a[x] == b[y])
            {
                AddChanges(rows, removed, added);
                removed.Clear();
                added.Clear();
                rows.Add(new DiffRow(a[x], b[y], DiffKind.Same));
                x++;
                y++;
            }
            else if (y == b.Length || (x < a.Length && lengths[x + 1][y] >= lengths[x][y + 1]))
            {
                removed.Add(a[x]);
                x++;
            }
            else
            {
                added.Add(b[y]);
                y++;
            }
        }
        AddChanges(rows, removed, added);
    }

    /// <summary>Ce qui a changé : d'abord en face à face, puis ce qui reste d'un seul côté.</summary>
    private static void AddChanges(List<DiffRow> rows, IReadOnlyList<string> removed, IReadOnlyList<string> added)
    {
        var both = Math.Min(removed.Count, added.Count);
        for (var i = 0; i < both; i++)
        {
            rows.Add(new DiffRow(removed[i], added[i], DiffKind.Changed));
        }
        for (var i = both; i < removed.Count; i++)
        {
            rows.Add(new DiffRow(removed[i], null, DiffKind.Removed));
        }
        for (var i = both; i < added.Count; i++)
        {
            rows.Add(new DiffRow(null, added[i], DiffKind.Added));
        }
    }
}
