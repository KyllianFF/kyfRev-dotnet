namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Une entrée du menu latéral et la page qu'elle affiche.</summary>
/// <param name="Id">Identifiant stable (« aujourdhui », « citations »…).</param>
/// <param name="Label">Libellé.</param>
/// <param name="Glyph">Icône (police d'icônes de Windows).</param>
/// <param name="Order">Rang dans le menu ; 900 et plus : en bas du menu.</param>
/// <param name="Page">Modèle de vue de la page.</param>
public sealed record NavigationItem(string Id, string Label, string Glyph, int Order, object Page)
{
    /// <summary>Rang à partir duquel une entrée va en bas du menu (santé du coffre, paramètres).</summary>
    public const int SecondaryOrder = 900;

    /// <summary>En bas du menu.</summary>
    public bool IsSecondary => Order >= SecondaryOrder;

    /// <summary>Le libellé : c'est aussi le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => Label;
}

/// <summary>Une commande de la recherche (Ctrl+K).</summary>
/// <param name="Label">Libellé, cherché sans tenir compte de la casse ni des accents.</param>
/// <param name="Detail">Raccourci clavier, ou rien.</param>
/// <param name="Glyph">Icône.</param>
/// <param name="Execute">Ce que fait la commande.</param>
public sealed record PaletteEntry(string Label, string? Detail, string Glyph, Action Execute)
{
    /// <summary>Le libellé : c'est aussi le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => Label;
}

/// <summary>Un raccourci clavier, tel que l'aide le présente.</summary>
/// <param name="Keys">Touches (« Ctrl+K »).</param>
/// <param name="Description">Ce qu'il fait.</param>
public sealed record Shortcut(string Keys, string Description);
