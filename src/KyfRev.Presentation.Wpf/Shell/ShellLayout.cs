namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Comment la coquille présente la navigation, selon la largeur de la fenêtre.</summary>
public enum NavigationLayout
{
    /// <summary>Fenêtre moyenne : une colonne d'icônes, le libellé sous chacune.</summary>
    Rail,

    /// <summary>Fenêtre large : la barre latérale complète.</summary>
    Sidebar,
}

/// <summary>
/// Les paliers de la coquille. La largeur comptée est celle que voit l'interface, zoom compris :
/// agrandir l'interface rapproche du palier inférieur, comme une fenêtre plus étroite. La fenêtre
/// ne descend jamais sous <see cref="MinimumWidth"/> pixels vus : la colonne d'icônes est la plus
/// petite disposition, et aucune vue ne défile jamais horizontalement.
/// </summary>
public static class ShellLayout
{
    /// <summary>La plus petite largeur vue de la fenêtre, quel que soit le zoom.</summary>
    public const double MinimumWidth = 600;

    /// <summary>La plus petite hauteur vue de la fenêtre, tant que l'écran la permet.</summary>
    public const double MinimumHeight = 540;

    /// <summary>Largeur à partir de laquelle la barre latérale complète remplace la colonne d'icônes.</summary>
    public const double SidebarMinimum = 1400;

    /// <summary>La disposition pour une fenêtre de <paramref name="width"/> pixels et un zoom de <paramref name="zoom"/>.</summary>
    public static NavigationLayout For(double width, double zoom)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoom);
        return width / zoom < SidebarMinimum ? NavigationLayout.Rail : NavigationLayout.Sidebar;
    }
}
