using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using KyfRev.DesignSystem.Controls;
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
