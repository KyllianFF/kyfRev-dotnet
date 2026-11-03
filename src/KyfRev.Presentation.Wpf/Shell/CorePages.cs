using KyfRev.Application.Modules;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Les entrées du menu, dans l'ordre de la 0.x : Aujourd'hui, Fiches, Définitions, Archives,
/// Statistiques, puis celles des modules actifs (Citations…) ; en bas, Santé du coffre et
/// Paramètres. Les vues arrivent jalon après jalon ; en attendant, chacune présente ce qu'elle fera.
/// </summary>
public static class CorePages
{
    /// <summary>Identifiant de la vue « Aujourd'hui » (le tableau de bord).</summary>
    public const string Today = "aujourdhui";

    /// <summary>Identifiant de la vue « Fiches ».</summary>
    public const string Sheets = "fiches";

    /// <summary>Identifiant de la vue « Définitions ».</summary>
    public const string Definitions = "definitions";

    /// <summary>Identifiant de la vue « Paramètres ».</summary>
    public const string Settings = "parametres";

    /// <summary>Identifiant de la vue « Archives ».</summary>
    public const string Archives = "archives";

    /// <summary>Identifiant de la vue « Statistiques ».</summary>
    public const string Statistics = "statistiques";

    /// <summary>Identifiant de la vue « Santé du coffre ».</summary>
    public const string Health = "sante";

    /// <summary>Les entrées du cœur, toutes encore à porter.</summary>
    public static IReadOnlyList<NavigationItem> All() => All(new Dictionary<string, object>());

    /// <summary>Les entrées du cœur ; <paramref name="pages"/> donne, par identifiant, les vues déjà portées.</summary>
    public static IReadOnlyList<NavigationItem> All(IReadOnlyDictionary<string, object> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        NavigationItem Entry(string id, string label, string glyph, int order) =>
            pages.TryGetValue(id, out var page) ? new(id, label, glyph, order, page) : Placeholder(id, label, glyph, order);
        return
        [
            Entry(Today, ShellText.Today, Icons.Calendar, 10),
            Entry(Sheets, ShellText.Sheets, Icons.Document, 20),
            Entry(Definitions, ShellText.Definitions, Icons.Dictionary, 30),
            Entry(Archives, ShellText.Archives, Icons.Archive, 40),
            Entry(Statistics, ShellText.Statistics, Icons.Chart, 50),
            Entry(Health, ShellText.Health, Icons.Health, NavigationItem.SecondaryOrder),
            Entry(Settings, ShellText.Settings, Icons.Settings, NavigationItem.SecondaryOrder + 10),
        ];
    }

    /// <summary>L'entrée qu'un module apporte (<see cref="INavigationContributor"/>).</summary>
    public static NavigationItem FromModule(NavigationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var glyph = entry.Id switch
        {
            "citations" => Icons.Quote,
            _ => Icons.Puzzle,
        };
        return Placeholder(entry.Id, entry.Label, glyph, entry.Order);
    }

    private static NavigationItem Placeholder(string id, string label, string glyph, int order) =>
        new(id, label, glyph, order, new PlaceholderPageViewModel(label, glyph, ShellText.ComingSoon));
}
