using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Les menus du clic droit : des actions rapides en tête, puis la liste.</summary>
internal static class ActionMenus
{
    private const string QuickStyle = "Kyf.MenuItem.Quick";

    /// <summary>Une action rapide : icône au-dessus du libellé.</summary>
    public static MenuItem Quick(string label, string glyph, Action action, bool isEnabled = true)
    {
        var item = NewQuick(label, glyph);
        item.IsEnabled = isEnabled;
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

    /// <summary>Une entrée de la liste.</summary>
    public static MenuItem Item(string label, Action action, string? shortcut = null)
    {
        var item = new MenuItem { Header = label, InputGestureText = shortcut ?? string.Empty };
        item.Click += (_, _) => action();
        return item;
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
