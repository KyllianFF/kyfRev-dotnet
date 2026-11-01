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
    /// <summary>Les entrées du cœur.</summary>
    public static IReadOnlyList<NavigationItem> All() =>
    [
        Placeholder("aujourdhui", ShellText.Today, Icons.Calendar, 10),
        Placeholder("fiches", ShellText.Sheets, Icons.Document, 20),
        Placeholder("definitions", ShellText.Definitions, Icons.Dictionary, 30),
        Placeholder("archives", ShellText.Archives, Icons.Archive, 40),
        Placeholder("statistiques", ShellText.Statistics, Icons.Chart, 50),
        Placeholder("sante", ShellText.Health, Icons.Health, NavigationItem.SecondaryOrder),
        Placeholder("parametres", ShellText.Settings, Icons.Settings, NavigationItem.SecondaryOrder + 10),
    ];

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
