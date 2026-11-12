using System.Globalization;
using System.Windows.Documents;
using KyfRev.Application.Model;

namespace KyfRev.Presentation.Wpf.Printing;

/// <summary>Sur combien de faces du papier on imprime.</summary>
public enum PrintSides
{
    /// <summary>Recto seul : une feuille de papier par page.</summary>
    OneSided,

    /// <summary>Recto verso, retourné sur le bord long : les pages se tournent comme celles d'un livre.</summary>
    LongEdge,

    /// <summary>Recto verso, retourné sur le bord court : les pages se tournent comme celles d'un bloc-notes.</summary>
    ShortEdge,
}

/// <summary>Une imprimante installée.</summary>
/// <param name="Name">Son nom, tel que Windows le montre.</param>
/// <param name="IsDefault">C'est l'imprimante par défaut de Windows.</param>
public sealed record PrinterInfo(string Name, bool IsDefault);

/// <summary>Ce qui part à l'imprimante.</summary>
/// <param name="Printer">Le nom de l'imprimante.</param>
/// <param name="Title">Le nom du travail d'impression : le titre de la fiche.</param>
/// <param name="Layout">La feuille : son format et son orientation.</param>
/// <param name="Sides">Recto seul ou recto verso.</param>
/// <param name="Copies">Le nombre d'exemplaires.</param>
public sealed record PrintJob(string Printer, string Title, PageLayout Layout, PrintSides Sides, int Copies);

/// <summary>Les imprimantes de l'ordinateur, et l'envoi d'un travail à l'une d'elles.</summary>
public interface IPrintService
{
    /// <summary>Les imprimantes installées, par nom ; aucune si Windows n'en connaît pas, ou ne répond pas.</summary>
    IReadOnlyList<PrinterInfo> Printers();

    /// <summary>Envoie les pages à l'imprimante. Rend ce qui l'a empêché, ou rien si le travail est parti.</summary>
    /// <param name="job">L'imprimante et ses réglages.</param>
    /// <param name="pages">Les pages, déjà mises en page à la taille de la feuille.</param>
    string? Print(PrintJob job, DocumentPaginator pages);
}

/// <summary>Les feuilles à imprimer, quand on n'imprime pas tout : « 1-2, 4 ».</summary>
public static class PageSelection
{
    /// <summary>
    /// Lit des numéros et des plages séparés par des virgules, des points-virgules ou des espaces. Rend les feuilles
    /// demandées, dans l'ordre et sans doublon ; rien si la saisie est vide, illisible, ou sort de la fiche.
    /// </summary>
    /// <param name="text">La saisie.</param>
    /// <param name="total">Le nombre de feuilles de la fiche.</param>
    public static IReadOnlyList<int>? Parse(string? text, int total)
    {
        var pages = new SortedSet<int>();
        var parts = (text ?? string.Empty).Replace('–', '-').Replace(" - ", "-", StringComparison.Ordinal)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var bounds = part.Split('-');
            if (bounds.Length is < 1 or > 2 || !Number(bounds[0], out var first) || !Number(bounds[^1], out var last) || first < 1 || last > total || first > last)
            {
                return null;
            }
            for (var page = first; page <= last; page++)
            {
                pages.Add(page);
            }
        }
        return pages.Count == 0 ? null : [.. pages];
    }

    /// <summary>Le nombre de feuilles de papier que demande un travail.</summary>
    /// <param name="pages">Le nombre de feuilles de la fiche à imprimer.</param>
    /// <param name="sides">Recto seul ou recto verso.</param>
    /// <param name="copies">Le nombre d'exemplaires.</param>
    public static int PaperSheets(int pages, PrintSides sides, int copies) =>
        Math.Max(0, copies) * (sides == PrintSides.OneSided ? Math.Max(0, pages) : (Math.Max(0, pages) + 1) / 2);

    private static bool Number(string text, out int number) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
}
