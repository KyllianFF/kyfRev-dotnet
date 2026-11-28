using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Les vues masquées du menu (maquette 8A) : décochées dans Paramètres › Menu, elles quittent le menu de gauche et ses
/// onglets, mais s'ouvrent encore par la recherche et leur raccourci ; Aujourd'hui reste.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class HiddenMenuTests
{
    [STATestMethod]
    public void A_hidden_view_leaves_the_menu_but_keeps_the_search_and_a_shortcut()
    {
        var shell = new TestShell().Shell;
        var store = new MemoryMachinePreferences();
        var machine = new MachineSettings(store);
        var page = new ShortcutsSettingsViewModel(machine, () => shell.OrderedNavigation);
        page.OnNavigatedTo();
        MenuRow Row(string id) => page.Menu.Single(row => row.Id == id);

        Assert.IsTrue(page.Menu.All(row => row.IsShown), "au départ, tout est montré");
        var today = Row(CorePages.Today);
        Assert.IsFalse(today.CanHide, "Aujourd'hui ne quitte pas le menu");
        Assert.AreEqual(MoreSettingsText.AlwaysInMenu(today.Label), today.ShowLabel);
        Assert.ThrowsExactly<InvalidOperationException>(() => today.IsShown = false);
        Assert.IsTrue(new MenuRow(CorePages.Today, today.Label, isShown: false).IsShown, "même relue masquée, elle se montre");

        var definitions = Row(CorePages.Definitions);
        Assert.AreEqual(MoreSettingsText.ShowInMenu(definitions.Label), definitions.ShowLabel);
        definitions.IsShown = false;
        Assert.AreEqual(CorePages.Definitions, store.Stored.HiddenMenu, "écrit aussitôt");
        Assert.AreEqual(MoreSettingsText.ShownOrHidden(definitions.Label, shown: false), page.MenuMoved, "dit au lecteur d'écran");
        Row(CorePages.Archives).IsShown = false;
        Assert.AreEqual($"{CorePages.Definitions},{CorePages.Archives}", store.Stored.HiddenMenu, "dans l'ordre de la liste");

        shell.SetMenuOrder(store.Stored.MenuOrder, store.Stored.HiddenMenu);
        CollectionAssert.AreEqual(new[] { CorePages.Today, CorePages.Sheets, CorePages.Statistics }, shell.MainNavigation.Select(item => item.Id).ToList());
        CollectionAssert.AreEqual(new[] { CorePages.Definitions, CorePages.Archives }, shell.HiddenNavigation.Select(item => item.Id).ToList());
        Assert.HasCount(5, shell.OrderedNavigation, "les paramètres les voient toutes");
        Assert.IsFalse(shell.TabNavigation.Concat(shell.OverflowNavigation).Any(shell.HiddenNavigation.Contains), "ni onglet ni « Plus » en fenêtre étroite");
        Assert.HasCount(7, shell.AllNavigation);

        // La recherche les ouvre encore, avec le raccourci qui suit ceux des vues montrées.
        shell.OpenPalette();
        shell.PaletteQuery = definitions.Label;
        var found = shell.PaletteResults.First(entry => entry.Label == ShellText.GoTo(definitions.Label));
        Assert.AreEqual("Ctrl+4", found.Detail);
        shell.NavigateToPosition("5");
        Assert.AreEqual(CorePages.Archives, shell.Current.Id);
        Assert.IsNull(shell.SelectedMain, "rien de choisi dans le menu : la vue n'y est pas");
        Assert.IsNull(shell.SelectedTab);

        // Remontrée pendant qu'elle est ouverte : le menu la choisit.
        Row(CorePages.Archives).IsShown = true;
        shell.SetMenuOrder(store.Stored.MenuOrder, store.Stored.HiddenMenu);
        Assert.AreEqual(CorePages.Archives, shell.SelectedMain?.Id);

        // Aujourd'hui ne se masque pas, même écrit à la main dans les préférences.
        shell.SetMenuOrder(null, $"{CorePages.Today},{CorePages.Sheets}");
        Assert.AreEqual(CorePages.Today, shell.MainNavigation[0].Id);
        Assert.AreEqual(CorePages.Sheets, shell.HiddenNavigation.Single().Id);

        // Relu au prochain affichage ; « Menu d'origine » remet l'ordre et remontre tout.
        var again = new ShortcutsSettingsViewModel(machine, () => shell.OrderedNavigation);
        again.OnNavigatedTo();
        Assert.IsFalse(again.Menu.Single(row => row.Id == CorePages.Definitions).IsShown);
        Assert.IsTrue(again.Menu.Single(row => row.Id == CorePages.Archives).IsShown);
        again.RestoreOrderCommand.Execute(null);
        Assert.AreEqual((string.Empty, string.Empty), (store.Stored.MenuOrder, store.Stored.HiddenMenu));
        Assert.IsTrue(again.Menu.All(row => row.IsShown));
        page.OnNavigatedTo();
        Assert.IsTrue(page.Menu.All(row => row.IsShown), "l'autre vue des réglages se relit sans rien écrire");
        Assert.AreEqual(string.Empty, store.Stored.HiddenMenu);
    }

    [STATestMethod]
    public void The_menu_list_shows_the_boxes_and_says_which_view_is_hidden_at_every_width_and_theme()
    {
        using var ui = new UiThread();
        var test = new TestShell();
        var store = new MemoryMachinePreferences { Stored = new MachinePreferences { HiddenMenu = CorePages.Archives } };
        var page = new ShortcutsSettingsViewModel(new MachineSettings(store), () => test.Shell.OrderedNavigation);
        page.OnNavigatedTo();
        var view = new ShortcutsSettingsPage { DataContext = page };
        var frame = new Border { Child = view, Padding = new Thickness(16) };
        frame.SetResourceReference(Border.BackgroundProperty, "Kyf.Brush.Background");
        var window = new Window { Content = new ScrollViewer { Content = frame }, Left = -10000, Top = -10000, Width = 470, Height = 900, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            var list = (ListBox)view.FindName("MenuList");
            ListBoxItem Item(string id) => (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(page.Menu.Single(row => row.Id == id));
            CheckBox Box(string id) => Descendants<CheckBox>(Item(id)).Single(box => box.Name == "ShownBox");
            TextBlock Label(string id) => Descendants<TextBlock>(Item(id)).Single(text => text.Name == "RowLabel");

            Assert.IsFalse(Box(CorePages.Today).IsEnabled, "Aujourd'hui ne se décoche pas");
            Assert.IsTrue(Box(CorePages.Today).IsChecked);
            Assert.IsFalse(Box(CorePages.Archives).IsChecked);
            Assert.AreEqual(ShellText.Archives + MoreSettingsText.HiddenSuffix, Label(CorePages.Archives).Text, "le nom dit qu'elle est masquée");
            Assert.AreEqual(ShellText.Statistics, Label(CorePages.Statistics).Text);
            Assert.AreEqual(MoreSettingsText.ShowInMenu(ShellText.Archives), System.Windows.Automation.AutomationProperties.GetName(Box(CorePages.Archives)));

            // La case décochée d'un clic : la vue est masquée, et le nom suit.
            Box(CorePages.Statistics).IsChecked = false;
            TestShell.Flush();
            Assert.AreEqual($"{CorePages.Archives},{CorePages.Statistics}", store.Stored.HiddenMenu);
            Assert.AreEqual(ShellText.Statistics + MoreSettingsText.HiddenSuffix, Label(CorePages.Statistics).Text);

            // Un appui sur la case reste à elle : la ligne ne glisse pas.
            view.Pressed(Box(CorePages.Sheets), default);
            view.Dragged(new Point(4, list.ActualHeight), pressed: true);
            Assert.IsFalse(view.IsDragging);
            view.Released();

            foreach (var (width, name) in new[] { (470.0, "470"), (1280.0, "1280"), (2560.0, "2560") })
            {
                window.Width = width;
                foreach (var theme in new[] { ThemeKind.Light, ThemeKind.Dark, ThemeKind.HighContrast })
                {
                    test.Design.Theme = theme;
                    var card = (FrameworkElement)view.FindName("MenuCard");
                    TestShell.Flush();
                    Assert.IsLessThanOrEqualTo(window.ActualWidth, frame.ActualWidth, "rien ne déborde à " + name);
                    Assert.IsGreaterThan(0, card.ActualWidth);
                    frame.Capture($"menu-masque-{name}-{theme}");
                }
            }
            test.Design.Theme = ThemeKind.Light;
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T found)
            {
                yield return found;
            }
            foreach (var deeper in Descendants<T>(child))
            {
                yield return deeper;
            }
        }
    }
}
