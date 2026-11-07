using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.DesignSystem.Styles;

/// <summary>Styles des champs et des listes de choix.</summary>
internal sealed partial class Fields : ResourceDictionary
{
    public Fields()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Un champ reçoit son menu quand il se charge : déclaré dans le dictionnaire, un menu serait un contrôle créé
    /// dès le chargement du design system, hors du fil de l'interface s'il le faut.
    /// </summary>
    private void OnFieldLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { ContextMenu: null } field)
        {
            field.ContextMenu = FieldMenu();
        }
    }

    /// <summary>Un mot de passe reçoit le sien, qui ne fait que coller : rien ne sort d'un mot de passe.</summary>
    private void OnPasswordLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox { ContextMenu: null } field)
        {
            var menu = new ContextMenu();
            menu.Items.Add(QuickCommand(ApplicationCommands.Paste, DesignText.Paste, Icons.Paste));
            field.ContextMenu = menu;
        }
    }

    private static ContextMenu FieldMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(QuickCommand(ApplicationCommands.Cut, DesignText.Cut, Icons.Cut));
        menu.Items.Add(QuickCommand(ApplicationCommands.Copy, DesignText.Copy, Icons.Copy));
        menu.Items.Add(QuickCommand(ApplicationCommands.Paste, DesignText.Paste, Icons.Paste));
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Command = ApplicationCommands.SelectAll, Header = DesignText.SelectAll });
        return menu;
    }

    private static MenuItem QuickCommand(ICommand command, string label, string glyph)
    {
        var item = new MenuItem { Command = command, Header = label, Icon = new Icon { Text = glyph } };
        item.SetResourceReference(FrameworkElement.StyleProperty, "Kyf.MenuItem.Quick");
        return item;
    }
}
