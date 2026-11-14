using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La section « Feuilles » des paramètres : les réglages à gauche, ce qu'ils donnent à droite ; en fenêtre étroite, tout
/// se suit sur une colonne.
/// </summary>
internal sealed partial class SheetsSettingsPage : UserControl
{
    /// <summary>En deçà, l'aperçu ne tient plus à côté des réglages.</summary>
    internal const double TwoColumns = 900;

    public SheetsSettingsPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width);
    }

    private void Arrange(double width)
    {
        var wide = width >= TwoColumns;
        Grid.SetColumn(Side, wide ? 1 : 0);
        Grid.SetRow(Side, wide ? 0 : 1);
        Grid.SetColumnSpan(Main, wide ? 1 : 2);
        Grid.SetColumnSpan(Side, wide ? 1 : 2);
        Main.Margin = wide ? new Thickness(0, 0, 12, 0) : default;
    }

    /// <summary>Entrée, dans le champ des marges : la valeur tapée s'applique, sans quitter le champ.</summary>
    private void OnMarginKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CustomMarginBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            CustomMarginBox.SelectAll();
            e.Handled = true;
        }
    }
}

/// <summary>
/// L'onglet « Raccourcis et menu » des paramètres : il prend les touches tapées pendant qu'un raccourci les attend. Dans l'ordre du
/// menu, une vue se fait glisser à sa place (elle y va à mesure, l'ordre s'écrit au dépôt) ; choisie d'un clic, ↑ et ↓ la
/// déplacent, Début et Fin l'envoient en tête ou en queue.
/// </summary>
internal sealed partial class ShortcutsSettingsPage : UserControl
{
    private MenuRow? _pressed;
    private Point _pressedAt;
    private bool _dragging;

    public ShortcutsSettingsPage()
    {
        InitializeComponent();
    }

    /// <summary>Une vue du menu est en train de glisser.</summary>
    internal bool IsDragging => _dragging;

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (DataContext is ShortcutsSettingsViewModel page && page.IsWaiting)
        {
            // Alt fait de la touche une touche « système » : c'est elle qu'on veut, pas « System ».
            e.Handled = page.Take(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
        }
    }

    /// <summary>
    /// Les flèches haut et bas déplacent la vue choisie, Début et Fin l'envoient en tête ou en queue ; la vue garde le focus. Avec Ctrl,
    /// les flèches restent celles de la liste : elles passent d'une vue à l'autre sans rien déplacer.
    /// </summary>
    private void OnMenuKey(object sender, KeyEventArgs e) => e.Handled = MoveChosen(e.Key, Keyboard.Modifiers);

    /// <summary>Déplace la vue choisie selon la touche ; vrai si la touche était pour elle.</summary>
    internal bool MoveChosen(Key key, ModifierKeys modifiers)
    {
        if (modifiers != ModifierKeys.None || DataContext is not ShortcutsSettingsViewModel page || page.ChosenMenuRow is not { } row)
        {
            return false;
        }
        var index = page.Menu.IndexOf(row);
        int? to = key switch
        {
            Key.Up => index - 1,
            Key.Down => index + 1,
            Key.Home => 0,
            Key.End => page.Menu.Count - 1,
            _ => null,
        };
        if (to is not { } target)
        {
            return false;
        }
        if (target >= 0 && target < page.Menu.Count && page.MoveTo(row, target))
        {
            MenuList.UpdateLayout();
            if (MenuList.ItemContainerGenerator.ContainerFromItem(row) is ListBoxItem moved)
            {
                moved.Focus();
                moved.BringIntoView();
            }
        }
        return true;
    }

    private void OnMenuPressed(object sender, MouseButtonEventArgs e) =>
        Pressed(e.OriginalSource as DependencyObject, e.GetPosition(MenuList));

    private void OnMenuDragged(object sender, MouseEventArgs e) => Dragged(e.GetPosition(MenuList), e.LeftButton == MouseButtonState.Pressed);

    private void OnMenuReleased(object sender, MouseButtonEventArgs e) => Released();

    private void OnMenuCaptureLost(object sender, MouseEventArgs e) => Released();

    /// <summary>Un appui sur une ligne la prend, prête à glisser ; un appui sur ses boutons ↑ ↓ reste à eux.</summary>
    internal void Pressed(DependencyObject? source, Point position)
    {
        _pressed = null;
        for (var element = source; element is not null and not ListBoxItem; element = VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element))
        {
            if (element is System.Windows.Controls.Primitives.ButtonBase)
            {
                return;
            }
        }
        _pressed = ItemsControl.ContainerFromElement(MenuList, source) is ListBoxItem { DataContext: MenuRow row } ? row : null;
        _pressedAt = position;
    }

    /// <summary>Le pointeur bouge, bouton tenu : passé le seuil d'un glissement, la vue prise va sous lui, à mesure.</summary>
    internal void Dragged(Point position, bool pressed)
    {
        if (!pressed || _pressed is not { } row || DataContext is not ShortcutsSettingsViewModel page)
        {
            return;
        }
        if (!_dragging)
        {
            if (Math.Abs(position.Y - _pressedAt.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
            _dragging = true;
            page.ChosenMenuRow = row;
            // La liste garde le pointeur tant que le bouton est tenu, même hors d'elle ; perdu (Alt+Tab), le glissement s'arrête là.
            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                MenuList.CaptureMouse();
            }
        }
        page.MoveTo(row, IndexAt(position.Y), save: false);
    }

    /// <summary>Le bouton relâché : une vue qui a glissé reste où elle est, et l'ordre s'écrit.</summary>
    internal void Released()
    {
        var dropped = _dragging;
        _dragging = false;
        _pressed = null;
        if (MenuList.IsMouseCaptured)
        {
            MenuList.ReleaseMouseCapture();
        }
        if (dropped && DataContext is ShortcutsSettingsViewModel page)
        {
            page.SaveMenuOrder();
        }
    }

    /// <summary>Le rang de la ligne sous cette hauteur de la liste ; au-delà de la dernière, la dernière.</summary>
    private int IndexAt(double y)
    {
        MenuList.UpdateLayout();
        for (var index = 0; index < MenuList.Items.Count; index++)
        {
            if (MenuList.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement row
                && y < row.TranslatePoint(default, MenuList).Y + row.ActualHeight)
            {
                return index;
            }
        }
        return MenuList.Items.Count - 1;
    }
}
