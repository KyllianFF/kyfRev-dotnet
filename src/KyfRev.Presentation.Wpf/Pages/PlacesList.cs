using System.Windows;
using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La liste du volet des dossiers (rangée 21B des maquettes) : le style commun des listes, avec des entrées plus serrées
/// que les lignes de fiches, et un écart avant le premier dossier et avant « Non rangées ». Les dimensions sont posées sur
/// chaque entrée quand elle est créée : changer le style d'une liste déjà à l'écran referait toutes ses lignes.
/// </summary>
internal sealed class PlacesList : ListBox
{
    /// <summary>La hauteur d'une entrée.</summary>
    internal const double EntryHeight = 34;

    /// <summary>L'écart au-dessus du premier dossier, et au-dessus de « Non rangées ».</summary>
    internal const double FoldersGap = 14;

    /// <summary>L'écart au-dessus de « Non rangées ».</summary>
    internal const double UnfiledGap = 10;

    public PlacesList()
    {
        // Le style de toutes les listes : un style implicite ne s'applique pas tout seul à une classe dérivée.
        SetResourceReference(StyleProperty, typeof(ListBox));
    }

    /// <summary>L'écart au-dessus de cette entrée.</summary>
    internal static double GapAbove(object? item) => item switch
    {
        SheetFolderRow { Place: SheetPlace.Unfiled } => UnfiledGap,
        SheetFolderRow { StartsFolders: true } => FoldersGap,
        _ => 0,
    };

    /// <inheritdoc/>
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ListBoxItem entry)
        {
            entry.MinHeight = EntryHeight;
            entry.Padding = new Thickness(4, 0, 10, 0);
            entry.Margin = new Thickness(0, GapAbove(item), 0, 1);
        }
    }
}
