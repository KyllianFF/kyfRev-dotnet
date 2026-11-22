using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Les menus du clic droit : des actions rapides en tête, puis la liste.</summary>
internal static class ActionMenus
{
    private const string QuickStyle = "Kyf.MenuItem.Quick";
    private const string ToolStyle = "Kyf.MenuItem.Tool";

    /// <summary>
    /// Une action rapide : icône au-dessus du libellé. <paramref name="current"/> la marque comme l'état en cours (le mode
    /// dans lequel la fiche est ouverte) : elle ressort, et le lecteur d'écran la dit cochée.
    /// </summary>
    public static MenuItem Quick(string label, string glyph, Action action, bool isEnabled = true, bool current = false)
    {
        var item = NewQuick(label, glyph);
        item.IsEnabled = isEnabled;
        item.IsChecked = current;
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Une action rapide portée par une commande, comme « Copier ».</summary>
    public static MenuItem Quick(string label, string glyph, ICommand command, IInputElement target)
    {
        var item = NewQuick(label, glyph);
        item.Command = command;
        item.CommandTarget = target;
        return item;
    }

    /// <summary>Un outil de la seconde rangée : une icône seule, son nom dans l'infobulle.</summary>
    public static MenuItem Tool(string label, string glyph, Action action, bool active = false)
    {
        // « Actif » : la mise en forme est déjà là sous le curseur ; l'outil ressort, et le dit au lecteur d'écran.
        var item = new MenuItem { Header = label, Icon = new Icon { Text = glyph }, ToolTip = label, IsChecked = active };
        item.SetResourceReference(FrameworkElement.StyleProperty, ToolStyle);
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Une entrée de la liste.</summary>
    public static MenuItem Item(string label, Action action, string? shortcut = null)
    {
        var item = new MenuItem { Header = label, InputGestureText = shortcut ?? string.Empty };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Une entrée de la liste portée par une commande, comme « Tout sélectionner ».</summary>
    public static MenuItem Item(string label, ICommand command, IInputElement target, string? shortcut = null) =>
        new() { Header = label, Command = command, CommandTarget = target, InputGestureText = shortcut ?? string.Empty };

    /// <summary>
    /// Un sous-menu (« Fiche › ») dans le même dessin que le menu : ses actions rapides en tête, puis la liste par blocs.
    /// Sans entrée, il n'est pas proposé.
    /// </summary>
    public static MenuItem Submenu(string label, IReadOnlyList<Control> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var submenu = new MenuItem { Header = label, ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(ActionMenuPanel))), IsEnabled = items.Count > 0 };
        foreach (var item in items)
        {
            submenu.Items.Add(item);
        }
        return submenu;
    }

    /// <summary>Remplit le menu qui s'ouvre ; sans entrée, il ne s'ouvre pas.</summary>
    public static void Fill(ContextMenu menu, IReadOnlyList<Control> items, ContextMenuEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(e);
        menu.Items.Clear();
        foreach (var item in items)
        {
            menu.Items.Add(item);
        }
        e.Handled = items.Count == 0;
    }

    private static MenuItem NewQuick(string label, string glyph)
    {
        var item = new MenuItem { Header = label, Icon = new Icon { Text = glyph } };
        item.SetResourceReference(FrameworkElement.StyleProperty, QuickStyle);
        return item;
    }
}
