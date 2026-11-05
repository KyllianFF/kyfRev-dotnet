using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La fenêtre de la coquille, ouverte pour de vrai : raccourcis, focus, mode concentration, zoom, pages.</summary>
[TestClass]
public sealed class ShellWindowTests
{
    [STATestMethod]
    public void Every_shortcut_reaches_its_command()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var shell = test.Shell;
            Assert.AreSame(shell.OpenPaletteCommand, Binding(window, Key.K, ModifierKeys.Control).Command);
            Assert.AreSame(shell.ShowHelpCommand, Binding(window, Key.F1, ModifierKeys.None).Command);
            Assert.AreSame(shell.ToggleFocusModeCommand, Binding(window, Key.F, ModifierKeys.Control | ModifierKeys.Shift).Command);
            Assert.AreSame(shell.ZoomInCommand, Binding(window, Key.OemPlus, ModifierKeys.Control).Command);
            Assert.AreSame(shell.ZoomInCommand, Binding(window, Key.Add, ModifierKeys.Control).Command);
            Assert.AreSame(shell.ZoomOutCommand, Binding(window, Key.OemMinus, ModifierKeys.Control).Command);
            Assert.AreSame(shell.ZoomOutCommand, Binding(window, Key.Subtract, ModifierKeys.Control).Command);
            Assert.AreSame(shell.ResetZoomCommand, Binding(window, Key.D0, ModifierKeys.Control).Command);
            Assert.AreSame(shell.ResetZoomCommand, Binding(window, Key.NumPad0, ModifierKeys.Control).Command);
            for (var rank = 1; rank <= 9; rank++)
            {
                var binding = Binding(window, Key.D0 + rank, ModifierKeys.Control);
                Assert.AreSame(shell.NavigateToPositionCommand, binding.Command);
                Assert.AreEqual(rank.ToString(System.Globalization.CultureInfo.InvariantCulture), binding.CommandParameter);
            }

            Press(window, Key.F1);
            Assert.IsTrue(shell.IsHelpOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Focus_goes_into_the_search_and_comes_back()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var navigation = (ListBox)window.FindName("MainNavigation");
            Assert.IsInstanceOfType<ListBoxItem>(FocusManager.GetFocusedElement(window));
            Assert.AreSame(test.Shell.SelectedMain, ((ListBoxItem)FocusManager.GetFocusedElement(window)).DataContext);

            test.Shell.OpenPalette();
            TestShell.Flush();
            var query = (TextBox)window.FindName("PaletteQuery");
            Assert.AreSame(query, FocusManager.GetFocusedElement(window));
            Assert.AreEqual(Visibility.Visible, ((Grid)window.FindName("Palette")).Visibility);

            test.Shell.ClosePalette();
            TestShell.Flush();
            Assert.AreEqual(Visibility.Collapsed, ((Grid)window.FindName("Palette")).Visibility);
            Assert.AreSame(navigation, ItemsControl.ItemsControlFromItemContainer((DependencyObject)FocusManager.GetFocusedElement(window)));

            test.Shell.Navigate(test.Shell.SecondaryNavigation[0]);
            test.Shell.ToggleFocusMode();
            test.Shell.OpenPalette();
            TestShell.Flush();
            test.Shell.ClosePalette();
            TestShell.Flush();
            Assert.IsNotNull(FocusManager.GetFocusedElement(window));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_click_on_a_result_runs_it()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            test.Shell.OpenPalette();
            test.Shell.PaletteQuery = "parametres";
            window.UpdateLayout();
            var results = (ListBox)window.FindName("PaletteResults");
            var item = (ListBoxItem)results.ItemContainerGenerator.ContainerFromIndex(0);

            results.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseUpEvent });
            Assert.IsTrue(test.Shell.IsPaletteOpen, "un clic hors d'un résultat ne fait rien");
            item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.MouseUpEvent });
            Assert.IsTrue(test.Shell.IsPaletteOpen, "le bouton droit ne lance rien");
            item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseUpEvent });

            Assert.IsFalse(test.Shell.IsPaletteOpen);
            Assert.AreEqual("Paramètres", test.Shell.Current.Label);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Focus_mode_hides_the_menu_and_zoom_scales_the_workspace()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var sidebar = (Border)window.FindName("Sidebar");
            var scale = (ScaleTransform)((Grid)window.FindName("Workspace")).LayoutTransform;
            Assert.AreEqual(Visibility.Visible, sidebar.Visibility);

            test.Shell.ToggleFocusMode();
            test.Shell.ZoomIn();
            test.Shell.ZoomIn();

            Assert.AreEqual(Visibility.Collapsed, sidebar.Visibility);
            Assert.AreEqual(1.2, scale.ScaleX, 1e-9);
            Assert.AreEqual(1.2, scale.ScaleY, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Each_entry_shows_its_page()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            foreach (var item in test.Shell.MainNavigation.Concat(test.Shell.SecondaryNavigation))
            {
                test.Shell.Navigate(item);
                window.UpdateLayout();
                var empty = Descendants<EmptyState>(window).Single();
                Assert.AreEqual(item.Label, empty.Title);
            }
            test.Shell.Navigate(test.Shell.Gallery);
            window.UpdateLayout();
            Assert.IsGreaterThanOrEqualTo(6, Descendants<Pill>(window).Count());
            var themes = (ListBox)Descendants<ListBox>(window).Single(list => list.Name == "ThemeChoices");
            Assert.AreSame(test.Gallery.SelectedTheme, themes.SelectedItem);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_menu_follows_the_width_of_the_window()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var shell = test.Shell;
            var sidebar = (Border)window.FindName("Sidebar");
            var search = (Button)window.FindName("SearchButton");
            var main = (ListBox)window.FindName("MainNavigation");
            var tabBar = (Border)window.FindName("TabBar");
            var topBar = (Border)window.FindName("TopBar");

            // La largeur vue par la coquille, sans dépendre de la taille de l'écran.
            shell.WindowWidth = 1600;
            window.UpdateLayout();
            Assert.AreEqual(248, sidebar.ActualWidth);
            Assert.AreEqual(HorizontalAlignment.Stretch, search.HorizontalContentAlignment);
            Assert.IsTrue(Named<StackPanel>(main, "Row").All(row => row.Visibility == Visibility.Visible));
            Assert.AreEqual(Visibility.Collapsed, tabBar.Visibility);
            Assert.AreEqual(Visibility.Collapsed, topBar.Visibility);

            shell.WindowWidth = 1024;
            window.UpdateLayout();
            Assert.AreEqual(112, sidebar.ActualWidth);
            Assert.AreEqual(HorizontalAlignment.Center, search.HorizontalContentAlignment);
            Assert.IsTrue(Named<StackPanel>(main, "Stacked").All(stacked => stacked.Visibility == Visibility.Visible));

            // La vraie fenêtre, rétrécie : la coquille suit.
            window.Width = 420;
            window.UpdateLayout();
            TestShell.Flush();
            Assert.AreEqual(NavigationLayout.Tabs, shell.Layout);
            Assert.AreEqual(Visibility.Collapsed, sidebar.Visibility);
            Assert.AreEqual(Visibility.Visible, tabBar.Visibility);
            Assert.AreEqual(Visibility.Visible, topBar.Visibility);
            window.FocusNavigation();
            Assert.AreSame(window.FindName("TabNavigation"), ItemsControl.ItemsControlFromItemContainer((DependencyObject)FocusManager.GetFocusedElement(window)));

            shell.ToggleFocusMode();
            Assert.AreEqual(Visibility.Collapsed, tabBar.Visibility);
            Assert.AreEqual(Visibility.Collapsed, topBar.Visibility);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void More_opens_the_other_views()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var shell = test.Shell;
            window.Width = 420;
            window.UpdateLayout();
            TestShell.Flush();
            var more = (Button)window.FindName("MoreButton");
            var menu = more.ContextMenu;
            var background = (Border)window.FindName("MoreBackground");

            more.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            TestShell.Flush();
            Assert.IsTrue(menu.IsOpen);
            Assert.AreSame(more, menu.PlacementTarget);
            Assert.AreEqual(PlacementMode.Top, menu.Placement);
            Assert.AreEqual(shell.OverflowNavigation.Count, menu.Items.Count);

            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.AreSame(shell.Home, shell.Current, "un clic hors d'une vue ne fait rien");
            var settings = (MenuItem)menu.ItemContainerGenerator.ContainerFromItem(shell.OverflowNavigation[^1]);
            settings.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.AreEqual("Paramètres", shell.Current.Label);
            Assert.AreEqual(FontWeights.SemiBold, TextElement.GetFontWeight(background));

            // L'état actif couvre tout le bouton, comme son survol.
            window.UpdateLayout();
            Assert.AreEqual(more.ActualWidth - more.BorderThickness.Left - more.BorderThickness.Right, background.ActualWidth, 0.5);
            Assert.AreEqual(more.ActualHeight - more.BorderThickness.Top - more.BorderThickness.Bottom, background.ActualHeight, 0.5);

            var tabs = (ListBox)window.FindName("TabNavigation");
            Assert.AreEqual(-1, tabs.SelectedIndex);
            tabs.SelectedIndex = 1;
            Assert.AreEqual("Fiches", shell.Current.Label);
            Assert.AreEqual(FontWeights.Normal, TextElement.GetFontWeight(background));
            menu.IsOpen = false;
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void An_extension_entry_shows_its_drawn_icon_and_its_counter()
    {
        var test = new TestShell();
        var badge = new NavigationBadge();
        var lines = ExtensionPages.PictureOf(new ExtensionIcon(0, 0, 24, 24, ["M4 4h16v16H4Z"], Stroked: true))!;
        var shapes = ExtensionPages.PictureOf(new ExtensionIcon(0, 0, 32, 16, ["M0 0h32v16H0Z"], Stroked: false))!;
        test.Shell.SetModuleEntries(
        [
            new NavigationItem("extension:quiz", "Quiz", Icons.Puzzle, 70, new object(), lines, badge),
            new NavigationItem("extension:plein", "Plein", Icons.Puzzle, 71, new object(), shapes),
        ]);
        var window = Open(test);
        try
        {
            var navigation = (ListBox)window.FindName("MainNavigation");
            DependencyObject Container(string id) => navigation.ItemContainerGenerator.ContainerFromItem(test.Shell.MainNavigation.Single(item => item.Id == id));
            var quiz = Container("extension:quiz");
            var drawn = Descendants<System.Windows.Shapes.Path>(quiz).Single(path => path.IsVisible);
            Assert.AreSame(lines.Geometry, drawn.Data);
            Assert.AreEqual(1.5, drawn.StrokeThickness);
            Assert.IsNull(drawn.Fill);
            Assert.AreEqual(new Rect(2.6667, 2.6667, 10.6667, 10.6667).ToString(CultureInfo.InvariantCulture), Round(lines.Geometry.Bounds), "La boîte de vue devient le carré du menu.");
            Assert.IsFalse(Descendants<Icon>(quiz).Any(icon => icon.IsVisible), "L'icône dessinée remplace le glyphe.");
            Assert.IsFalse(Descendants<TextBlock>(quiz).Any(text => text.IsVisible && text.Text == "3"));

            badge.Text = "3";
            TestShell.Flush();
            Assert.IsTrue(Descendants<TextBlock>(quiz).Any(text => text.IsVisible && text.Text == "3"));

            var plein = Descendants<System.Windows.Shapes.Path>(Container("extension:plein")).Single(path => path.IsVisible);
            Assert.AreEqual(0, plein.StrokeThickness);
            Assert.IsNotNull(plein.Fill);
            Assert.AreEqual(new Rect(0, 4, 16, 8).ToString(CultureInfo.InvariantCulture), Round(shapes.Geometry.Bounds), "Plus large que haute, elle se centre.");
        }
        finally
        {
            window.Close();
        }
    }

    private static string Round(Rect bounds) =>
        new Rect(Math.Round(bounds.X, 4), Math.Round(bounds.Y, 4), Math.Round(bounds.Width, 4), Math.Round(bounds.Height, 4)).ToString(CultureInfo.InvariantCulture);

    private static ShellWindow Open(TestShell test)
    {
        var window = new ShellWindow(test.Shell) { ShowActivated = true, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        window.Activate();
        TestShell.Flush();
        Assert.AreNotEqual(0, new WindowInteropHelper(window).Handle);
        return window;
    }

    private static KeyBinding Binding(Window window, Key key, ModifierKeys modifiers) =>
        window.InputBindings.OfType<KeyBinding>().Single(binding => binding.Key == key && binding.Modifiers == modifiers);

    private static void Press(Window window, Key key) =>
        window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.KeyDownEvent });

    private static IEnumerable<T> Named<T>(DependencyObject root, string name)
        where T : FrameworkElement => Descendants<T>(root).Where(element => element.Name == name);

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found)
            {
                yield return found;
            }
            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
