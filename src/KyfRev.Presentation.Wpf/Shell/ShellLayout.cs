namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Comment la coquille présente la navigation, selon la largeur de la fenêtre.</summary>
public enum NavigationLayout
{
    /// <summary>Fenêtre étroite : onglets en bas, « Plus » pour les autres vues.</summary>
    Tabs,

    /// <summary>Fenêtre moyenne : une colonne d'icônes, le libellé sous chacune.</summary>
    Rail,

    /// <summary>Fenêtre large : la barre latérale complète.</summary>
    Sidebar,
}

/// <summary>
/// Les paliers de la coquille. La largeur comptée est celle que voit l'interface, zoom compris :
/// agrandir l'interface rapproche du palier inférieur, comme une fenêtre plus étroite. La
/// navigation se resserre avant que le contenu ne manque de place, pour qu'aucune vue ne défile
/// jamais horizontalement.
/// </summary>
public static class ShellLayout
{
    /// <summary>Largeur à partir de laquelle la colonne d'icônes remplace les onglets.</summary>
    public const double RailMinimum = 600;

    /// <summary>Largeur à partir de laquelle la barre latérale complète remplace la colonne d'icônes.</summary>
    public const double SidebarMinimum = 1400;

    /// <summary>Nombre de vues en onglets, « Plus » en sus.</summary>
    public const int TabCount = 3;

    /// <summary>La disposition pour une fenêtre de <paramref name="width"/> pixels et un zoom de <paramref name="zoom"/>.</summary>
    public static NavigationLayout For(double width, double zoom)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoom);
        var seen = width / zoom;
        return seen < RailMinimum ? NavigationLayout.Tabs : seen < SidebarMinimum ? NavigationLayout.Rail : NavigationLayout.Sidebar;
    }
}
