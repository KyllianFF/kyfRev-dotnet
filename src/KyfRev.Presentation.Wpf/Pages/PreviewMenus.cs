using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Le clic droit dans l'aperçu d'une fiche (Carnet, Aujourd'hui), rangée 23B des maquettes : sur un texte choisi, Copier et
/// Tout sélectionner, puis « Fiche › » ; ailleurs, le menu de la fiche, le même que sur sa ligne.
/// </summary>
internal static class PreviewMenus
{
    /// <summary>Remplit le menu de l'aperçu qui s'ouvre ; <paramref name="sheet"/> donne celui de la fiche.</summary>
    public static void Open(ContextMenuEventArgs e, Func<IReadOnlyList<Control>> sheet)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(sheet);
        if (ViewerOf(e.OriginalSource as DependencyObject) is not { ContextMenu: { } menu } viewer)
        {
            return;
        }
        ActionMenus.Fill(menu, Items(viewer, sheet()), e);
    }

    /// <summary>Les entrées : celles du texte choisi, s'il y en a, au-dessus de la fiche ; sinon la fiche seule.</summary>
    internal static IReadOnlyList<Control> Items(SheetDocumentViewer viewer, IReadOnlyList<Control> sheet) =>
        viewer.Selection is { IsEmpty: false }
            ?
            [
                ActionMenus.Quick(ReadingText.Copy, Icons.Copy, ApplicationCommands.Copy, viewer),
                ActionMenus.Item(DesignText.SelectAll, ApplicationCommands.SelectAll, viewer, "Ctrl+A"),
                new Separator(),
                ActionMenus.Submenu(ReadingText.SheetSubmenu, sheet),
            ]
            : sheet;

    private static SheetDocumentViewer? ViewerOf(DependencyObject? element)
    {
        for (; element is not null; element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
        {
            if (element is SheetDocumentViewer viewer)
            {
                return viewer;
            }
        }
        return null;
    }
}
