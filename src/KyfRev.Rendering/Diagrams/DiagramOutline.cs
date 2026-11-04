using System.Collections.Immutable;
using System.Text;

namespace KyfRev.Rendering.Diagrams;

/// <summary>Un élément d'une hiérarchie.</summary>
/// <param name="Level">Son niveau : 0 au sommet.</param>
/// <param name="Label">Son texte.</param>
public readonly record struct OutlineItem(int Level, string Label);

/// <summary>
/// Une hiérarchie s'écrit en texte, lisible et modifiable partout : une ligne par élément, décalée de deux espaces par
/// niveau. Une liste sans aucun décalage se lit comme avant : la première ligne au sommet, les autres juste en dessous.
/// </summary>
public static class DiagramOutline
{
    private const int SpacesPerLevel = 2;

    /// <summary>Les éléments d'un bloc, dans l'ordre : aucun ne saute de niveau, quel que soit le décalage écrit.</summary>
    /// <param name="text">Le contenu du bloc.</param>
    public static ImmutableArray<OutlineItem> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Split('\n')
            .Select(line => (Indent: IndentOf(line), Label: line.Trim()))
            .Where(line => line.Label.Length > 0)
            .ToList();
        if (lines.All(line => line.Indent == lines[0].Indent))
        {
            return [.. lines.Select((line, rank) => new OutlineItem(rank == 0 ? 0 : 1, line.Label))];
        }
        var open = new Stack<int>();
        var items = ImmutableArray.CreateBuilder<OutlineItem>(lines.Count);
        foreach (var (indent, label) in lines)
        {
            while (open.Count > 0 && open.Peek() >= indent)
            {
                open.Pop();
            }
            items.Add(new OutlineItem(open.Count, label));
            open.Push(indent);
        }
        return items.ToImmutable();
    }

    /// <summary>Le contenu d'un bloc : les éléments vides n'y sont pas.</summary>
    /// <param name="items">Les éléments, dans l'ordre.</param>
    public static string Write(IEnumerable<OutlineItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var text = new StringBuilder();
        foreach (var item in items.Where(item => item.Label.Trim().Length > 0))
        {
            text.Append(' ', item.Level * SpacesPerLevel).Append(item.Label.Trim()).Append('\n');
        }
        return text.ToString().TrimEnd('\n');
    }

    private static int IndentOf(string line)
    {
        var indent = 0;
        foreach (var character in line)
        {
            if (character == ' ')
            {
                indent++;
            }
            else if (character == '\t')
            {
                indent += SpacesPerLevel;
            }
            else
            {
                break;
            }
        }
        return indent;
    }
}
