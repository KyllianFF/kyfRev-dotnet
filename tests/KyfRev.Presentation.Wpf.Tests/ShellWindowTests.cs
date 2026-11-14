using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Pages;
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
            Assert.AreSame(shell.LockCommand, Binding(window, Key.L, ModifierKeys.Control | ModifierKeys.Shift).Command);
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

            // La largeur vue par la coquille, sans dépendre de la taille de l'écran.
            shell.WindowWidth = 1600;
            window.UpdateLayout();
            Assert.AreEqual(248, sidebar.ActualWidth);
            Assert.AreEqual(HorizontalAlignment.Stretch, search.HorizontalContentAlignment);
            Assert.IsTrue(Named<StackPanel>(main, "Row").All(row => row.Visibility == Visibility.Visible));
            Assert.AreEqual(Visibility.Collapsed, tabBar.Visibility);

            shell.WindowWidth = 1024;
            window.UpdateLayout();
            Assert.AreEqual(77, sidebar.ActualWidth);
            Assert.AreEqual(HorizontalAlignment.Stretch, search.HorizontalContentAlignment);
            var squares = Named<Grid>(main, "Stacked").ToList();
            Assert.IsTrue(squares.All(stacked => stacked.Visibility == Visibility.Visible));
            // Des boutons carrés, tous pareils : l'icône seule, et le nom au survol.
            var items = squares.Select(square => (FrameworkElement)ItemsControl.ContainerFromElement(main, square)).ToList();
            Assert.IsTrue(items.All(item => Math.Abs(item.ActualWidth - item.ActualHeight) < 1), "carrés : " + string.Join(" ", items.Select(item => $"{item.ActualWidth}x{item.ActualHeight}")));
            Assert.AreEqual(1, items.Select(item => Math.Round(item.ActualWidth)).Distinct().Count(), "tous de la même taille");
            Assert.IsTrue(squares.All(square => square.ToolTip is string { Length: > 0 }));
            Assert.IsTrue(Named<StackPanel>(main, "Row").All(row => row.Visibility == Visibility.Collapsed), "plus de texte sous l'icône");

            // La vraie fenêtre, rétrécie : la coquille suit.
            window.Width = 420;
            window.UpdateLayout();
            TestShell.Flush();
            Assert.AreEqual(NavigationLayout.Tabs, shell.Layout);
            Assert.AreEqual(ShellLayout.MinimumWidth, window.ActualWidth, 1, "la fenêtre ne descend pas sous son minimum");
            Assert.AreEqual(Visibility.Collapsed, sidebar.Visibility);
            Assert.AreEqual(Visibility.Visible, tabBar.Visibility);
            Assert.AreEqual(HorizontalAlignment.Right, ((Border)window.FindName("SearchHost")).HorizontalAlignment, "La recherche se réduit à son icône, à droite.");
            Assert.AreEqual(Visibility.Collapsed, ((TextBlock)window.FindName("SearchLabel")).Visibility);
            window.FocusNavigation();
            Assert.AreSame(window.FindName("TabNavigation"), ItemsControl.ItemsControlFromItemContainer((DependencyObject)FocusManager.GetFocusedElement(window)));

            // Les onglets du bas ne se masquent pas : Ctrl+Maj+F n'a pas de menu latéral à retirer ici.
            shell.ToggleFocusMode();
            Assert.AreEqual(Visibility.Visible, tabBar.Visibility);
            Assert.IsFalse(shell.IsFocusMode);

            // Masqué en grand, puis la fenêtre rétrécit : les onglets paraissent quand même, sans quoi on resterait bloqué sur la page ;
            // élargie, elle retrouve son menu masqué et le bouton qui le rend.
            window.Width = 1100;
            window.UpdateLayout();
            TestShell.Flush();
            shell.ToggleFocusMode();
            TestShell.Flush();
            Assert.IsTrue(shell.IsFocusMode);
            Assert.AreEqual(Visibility.Collapsed, sidebar.Visibility);
            window.Width = 420;
            window.UpdateLayout();
            TestShell.Flush();
            Assert.AreEqual(NavigationLayout.Tabs, shell.Layout);
            Assert.AreEqual(Visibility.Visible, tabBar.Visibility, "le menu masqué revient en onglets");
            Assert.IsTrue(tabBar.ActualHeight > 0);
            Assert.AreEqual(Visibility.Collapsed, ((FrameworkElement)window.FindName("MenuButtonHost")).Visibility);
            window.Width = 1100;
            window.UpdateLayout();
            TestShell.Flush();
            Assert.AreEqual(Visibility.Collapsed, tabBar.Visibility);
            Assert.AreEqual(Visibility.Collapsed, sidebar.Visibility, "élargie, elle retrouve le menu masqué");
            Assert.IsTrue(((Button)window.FindName("ExitFocusButton")).IsVisible, "et le bouton qui le rend");
            shell.ToggleFocusMode();
            window.Width = 420;
            window.UpdateLayout();
            TestShell.Flush();

            // Le zoom grandit la fenêtre minimale avec lui.
            shell.SetZoom(1.2);
            window.UpdateLayout();
            Assert.AreEqual(564, window.MinWidth, 1e-9);
            Assert.AreEqual(648, window.MinHeight, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_window_draws_its_own_title_bar()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var chrome = WindowChrome.GetWindowChrome(window);
            var bar = (Border)window.FindName("TitleBar");
            Assert.IsNotNull(chrome);
            Assert.AreEqual(bar.Height, chrome.CaptionHeight, "Windows déplace la fenêtre sur toute la hauteur de la barre.");
            foreach (var name in new[] { "SearchButton", "ThemeButton", "HelpButton", "MinimizeButton", "MaximizeButton", "CloseButton" })
            {
                var button = (Button)window.FindName(name);
                Assert.IsTrue(button.IsVisible, name);
                Assert.IsTrue(WindowChrome.GetIsHitTestVisibleInChrome(button), name);
                Assert.IsFalse(string.IsNullOrEmpty(AutomationProperties.GetName(button)), name);
                Assert.IsLessThanOrEqualTo(bar.ActualHeight, button.ActualHeight, name);
            }
            Assert.AreSame(test.Shell.OpenPaletteCommand, ((Button)window.FindName("SearchButton")).Command);
            Assert.AreEqual(test.Design.Dictionary[DesignSystem.DesignKeys.Brush(DesignSystem.Tokens.ColorRole.Surface)], ((Button)window.FindName("SearchButton")).Background, "Le bouton prend le style de l'application.");
            Assert.AreSame(test.Shell.ShowHelpCommand, ((Button)window.FindName("HelpButton")).Command);
            Assert.IsFalse(((Button)window.FindName("MinimizeButton")).Focusable, "Comme ceux de Windows : Alt+Espace les remplace au clavier.");
            Assert.AreEqual(bar.ActualHeight + ((Grid)window.FindName("Root")).ActualHeight, ((Grid)window.FindName("Frame")).ActualHeight, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_buttons_of_the_title_bar_act_on_the_window()
    {
        var test = new TestShell();
        var window = Open(test);
        var closed = false;
        window.Closed += (_, _) => closed = true;
        try
        {
            var maximize = (Button)window.FindName("MaximizeButton");
            var icon = (Icon)window.FindName("MaximizeIcon");
            Assert.AreEqual(Icons.WindowMaximize, icon.Text);
            Assert.AreEqual(ShellText.WindowMaximize, AutomationProperties.GetName(maximize));

            Click(maximize);
            Assert.AreEqual(WindowState.Maximized, window.WindowState);
            Assert.AreEqual(Icons.WindowRestore, icon.Text);
            Assert.AreEqual(ShellText.WindowRestore, AutomationProperties.GetName(maximize));
            Assert.AreEqual(ShellText.WindowRestore, maximize.ToolTip);

            Click(maximize);
            Assert.AreEqual(WindowState.Normal, window.WindowState);
            Assert.AreEqual(Icons.WindowMaximize, icon.Text);

            Click((Button)window.FindName("MinimizeButton"));
            Assert.AreEqual(WindowState.Minimized, window.WindowState);
            window.WindowState = WindowState.Normal;
            TestShell.Flush();

            Click((Button)window.FindName("CloseButton"));
            Assert.IsTrue(closed);
        }
        finally
        {
            if (!closed)
            {
                window.Close();
            }
        }
    }

    [STATestMethod]
    public void The_maximize_button_is_the_real_one_for_windows()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            var maximize = (Button)window.FindName("MaximizeButton");
            var center = maximize.PointToScreen(new Point(maximize.ActualWidth / 2, maximize.ActualHeight / 2));
            var elsewhere = ((Button)window.FindName("CloseButton")).PointToScreen(new Point(4, 12));
            nint Send(int message, nint wParam, nint lParam, out bool handled)
            {
                var taken = false;
                var answer = window.OnWindowMessage(handle, message, wParam, lParam, ref taken);
                handled = taken;
                return answer;
            }

            Assert.AreEqual(9, Send(0x0084, 0, ScreenPosition(center), out var handled), "HTMAXBUTTON : Windows 11 y ouvre ses dispositions.");
            Assert.IsTrue(handled);
            Assert.AreEqual(bool.TrueString, maximize.Tag, "Le survol se montre, puisque WPF ne le voit plus.");

            Assert.AreEqual(0, Send(0x0084, 0, ScreenPosition(elsewhere), out handled));
            Assert.IsFalse(handled);
            Assert.IsNull(maximize.Tag);

            Send(0x0084, 0, ScreenPosition(center), out _);
            Send(0x02A2, 0, 0, out _);
            Assert.IsNull(maximize.Tag, "La souris quitte la fenêtre.");

            Send(0x00A1, 9, 0, out handled);
            Assert.IsTrue(handled);
            Assert.AreEqual(WindowState.Normal, window.WindowState, "Comme un bouton, il agit au relâchement.");
            Send(0x00A2, 9, 0, out handled);
            Assert.IsTrue(handled);
            TestShell.Flush();
            Assert.AreEqual(WindowState.Maximized, window.WindowState);
            window.WindowState = WindowState.Normal;
            TestShell.Flush();

            Send(0x00A1, 2, 0, out handled);
            Assert.IsFalse(handled, "Sur le reste de la barre, c'est Windows qui déplace la fenêtre.");
            Send(0x00A2, 2, 0, out handled);
            Assert.IsFalse(handled);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_maximized_window_keeps_its_edges_on_the_screen()
    {
        var test = new TestShell();
        test.Chrome.Overhang = new WindowOverhang(8, 8, 8, 8);
        var window = Open(test);
        try
        {
            var frame = (Grid)window.FindName("Frame");
            var dpi = VisualTreeHelper.GetDpi(window);
            var inside = new Thickness(8 / dpi.DpiScaleX, 8 / dpi.DpiScaleY, 8 / dpi.DpiScaleX, 8 / dpi.DpiScaleY);
            Assert.AreEqual(default, frame.Margin);

            window.WindowState = WindowState.Maximized;
            TestShell.Flush();
            Assert.AreEqual(inside, frame.Margin, "Ce qui dépasse de l'écran est rendu au contenu.");

            test.Shell.ToggleFullScreen();
            TestShell.Flush();
            Assert.AreEqual(default, frame.Margin, "En plein écran, rien ne dépasse.");
            Assert.AreEqual(ResizeMode.NoResize, window.ResizeMode, "Sans cadre épais, rien ne déborde de l'écran.");

            test.Shell.ToggleFullScreen();
            TestShell.Flush();
            Assert.AreEqual(ResizeMode.CanResize, window.ResizeMode);
            Assert.AreEqual(WindowState.Maximized, window.WindowState);
            Assert.AreEqual(inside, frame.Margin, "De retour, agrandie comme avant.");

            window.WindowState = WindowState.Normal;
            TestShell.Flush();
            Assert.AreEqual(default, frame.Margin);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Full_screen_hides_the_title_bar_and_the_welcome_keeps_only_the_window()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var bar = (Border)window.FindName("TitleBar");
            var search = (Button)window.FindName("SearchButton");
            var actions = (StackPanel)window.FindName("TitleActions");
            var buttons = (StackPanel)window.FindName("WindowButtons");
            Assert.AreEqual(Visibility.Visible, bar.Visibility);

            test.Shell.ToggleFullScreen();
            TestShell.Flush();
            Assert.AreEqual(Visibility.Collapsed, bar.Visibility);
            var maximize = (Button)window.FindName("MaximizeButton");
            var taken = false;
            Assert.AreEqual(0, window.OnWindowMessage(new WindowInteropHelper(window).Handle, 0x0084, 0, 0, ref taken), "Un bouton caché ne répond pas à Windows.");
            Assert.IsFalse(maximize.IsVisible);
            test.Shell.ToggleFullScreen();
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, bar.Visibility);

            test.Shell.ToggleFocusMode();
            TestShell.Flush();
            Assert.IsTrue(search.IsVisible, "En mode concentration, la barre reste : la fenêtre se déplace toujours.");
            test.Shell.ToggleFocusMode();

            test.Shell.ToggleFullScreen();
            test.Shell.OpenPalette();
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, bar.Visibility, "En plein écran, la barre revient le temps d'une recherche.");
            test.Shell.ClosePalette();
            TestShell.Flush();
            Assert.AreEqual(Visibility.Collapsed, bar.Visibility);
            test.Shell.ToggleFullScreen();

            test.Shell.ShowWelcome(new object());
            TestShell.Flush();
            Assert.IsFalse(search.IsVisible);
            Assert.AreEqual(Visibility.Collapsed, actions.Visibility);
            Assert.AreEqual(Visibility.Visible, buttons.Visibility);
            test.Shell.OpenPalette();
            TestShell.Flush();
            Assert.IsTrue(((TextBox)window.FindName("PaletteQuery")).IsVisible, "Sur l'accueil aussi, on tape dans la barre.");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_theme_changes_in_one_click_and_is_remembered()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var shell = test.Shell;
            var machine = new MachineSettings(new MemoryMachinePreferences());
            machine.Follow(shell.Appearance, shell);
            var general = new GeneralSettingsViewModel(machine, new FakeVoices("Hortense"));
            var button = (Button)window.FindName("ThemeButton");
            Assert.AreEqual(ThemeKind.Light, shell.Appearance.Theme);
            Assert.AreEqual(Icons.Moon, button.Content);
            Assert.AreEqual(ShellText.ThemeToDark, AutomationProperties.GetName(button));

            shell.ToggleThemeCommand.Execute(null);
            TestShell.Flush();

            Assert.AreEqual(ThemeKind.Dark, shell.Appearance.Theme);
            Assert.AreEqual(ThemePreference.Dark, machine.Current.Theme);
            Assert.AreEqual(ThemePreference.Dark, general.Theme.Value, "L'onglet ouvert suit.");
            Assert.AreEqual(Icons.Sun, button.Content);
            Assert.AreEqual(ShellText.ThemeToLight, AutomationProperties.GetName(button));

            shell.ToggleThemeCommand.Execute(null);
            TestShell.Flush();
            Assert.AreEqual(ThemePreference.Light, machine.Current.Theme);

            shell.Appearance.ForcesHighContrast = true;
            TestShell.Flush();
            Assert.AreEqual(Visibility.Collapsed, button.Visibility, "Le contraste élevé passe avant.");
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
            Assert.AreEqual("Carnet", shell.Current.Label);
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

    [STATestMethod]
    public void The_list_follows_the_field_and_any_click_elsewhere_closes_it()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var host = (Border)window.FindName("SearchHost");
            var card = (FrameworkElement)window.FindName("PaletteCard");
            var query = (TextBox)window.FindName("PaletteQuery");
            window.Width = 1600;
            TestShell.Flush();
            test.Shell.OpenPalette();
            TestShell.Flush();
            Assert.AreEqual(host.TranslatePoint(default, window).X, card.TranslatePoint(default, window).X, 1);

            window.Width = 1200;
            TestShell.Flush();
            Assert.AreEqual(host.TranslatePoint(default, window).X, card.TranslatePoint(default, window).X, 1, "Redimensionnée, la fenêtre garde la liste sous le champ.");

            query.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            Assert.IsTrue(test.Shell.IsPaletteOpen, "Un clic dans le champ le garde ouvert.");
            var theme = (Button)window.FindName("ThemeButton");
            theme.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            Assert.IsFalse(test.Shell.IsPaletteOpen, "Un clic ailleurs, même dans la barre de titre, le ferme.");

            test.Shell.OpenPalette();
            TestShell.Flush();
            var taken = false;
            window.OnWindowMessage(new WindowInteropHelper(window).Handle, 0x00A1, 2, 0, ref taken);
            Assert.IsFalse(test.Shell.IsPaletteOpen, "Un clic sur le vide de la barre de titre le ferme.");
            Assert.IsFalse(taken, "Windows déplace toujours la fenêtre.");
            test.Shell.OpenPalette();
            window.OnWindowMessage(new WindowInteropHelper(window).Handle, 0x00A4, 2, 0, ref taken);
            Assert.IsFalse(test.Shell.IsPaletteOpen, "Le clic droit aussi.");
            test.Shell.OpenPalette();
            window.OnWindowMessage(new WindowInteropHelper(window).Handle, 0x00A1, 11, 0, ref taken);
            Assert.IsTrue(test.Shell.IsPaletteOpen, "Un bord tiré pour redimensionner la garde ouverte.");
            test.Shell.ClosePalette();

            var icon = (Image)window.FindName("AppIcon");
            Assert.IsInstanceOfType<BitmapSource>(icon.Source);
            Assert.IsGreaterThanOrEqualTo(16, ((BitmapSource)icon.Source).PixelWidth, "L'icône vient de l'image du .ico à la taille de l'écran.");
            Assert.IsLessThanOrEqualTo(48, ((BitmapSource)icon.Source).PixelWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_search_is_typed_in_the_title_bar_and_its_list_opens_below_it()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var bar = (Border)window.FindName("TitleBar");
            var host = (Border)window.FindName("SearchHost");
            var button = (Button)window.FindName("SearchButton");
            var query = (TextBox)window.FindName("PaletteQuery");
            var card = (FrameworkElement)window.FindName("PaletteCard");
            Assert.IsTrue(bar.IsAncestorOf(query), "On tape dans la barre de titre, pas dans une seconde barre.");
            Assert.IsFalse(query.IsVisible);

            button.Command.Execute(null);
            TestShell.Flush();
            Assert.IsTrue(test.Shell.IsPaletteOpen);
            Assert.IsTrue(query.IsVisible);
            Assert.IsFalse(button.IsVisible, "Le champ prend la place du bouton.");
            Assert.IsTrue(query.IsKeyboardFocused);
            Assert.AreEqual(host.TranslatePoint(default, window).X, card.TranslatePoint(default, window).X, 1, "La liste s'ouvre à l'aplomb de la recherche.");
            Assert.AreEqual(bar.ActualHeight + 2, card.TranslatePoint(default, window).Y, 1, "Juste sous la barre.");
            Assert.IsGreaterThanOrEqualTo(360, card.ActualWidth);

            query.Text = "parametres";
            Assert.AreEqual("Aller à : Paramètres", test.Shell.PaletteResults.Single().Label);
            query.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(query)!, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
            TestShell.Flush();
            Assert.IsFalse(test.Shell.IsPaletteOpen);
            Assert.IsTrue(button.IsVisible);

            test.Shell.OpenPalette();
            TestShell.Flush();
            var navigation = (ListBox)window.FindName("MainNavigation");
            var item = (ListBoxItem)navigation.ItemContainerGenerator.ContainerFromIndex(1);
            item.Focus();
            TestShell.Flush();
            Assert.IsFalse(test.Shell.IsPaletteOpen, "Un clic ailleurs la ferme, comme un menu.");
            Assert.IsTrue(item.IsKeyboardFocused, "Le focus reste où l'on a cliqué.");

            window.Width = 520;
            test.Shell.OpenPalette();
            TestShell.Flush();
            var right = card.TranslatePoint(new Point(card.ActualWidth, 0), window).X;
            Assert.IsLessThanOrEqualTo(((FrameworkElement)window.Content).ActualWidth - 15, right, "Étroite, la fenêtre garde la liste entière.");
            Assert.IsTrue(query.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static string Round(Rect bounds) =>
        new Rect(Math.Round(bounds.X, 4), Math.Round(bounds.Y, 4), Math.Round(bounds.Width, 4), Math.Round(bounds.Height, 4)).ToString(CultureInfo.InvariantCulture);

    private static void Click(ButtonBase button)
    {
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        TestShell.Flush();
    }

    private static nint ScreenPosition(Point point) => (((int)point.Y & 0xFFFF) << 16) | ((int)point.X & 0xFFFF);

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

    [STATestMethod]
    public void The_tabs_of_the_ribbon_all_have_the_height_of_the_first()
    {
        var test = new TestShell();
        var ribbon = new KyfRev.Presentation.Wpf.Editing.EditorRibbon();
        var window = new Window { Content = ribbon, Left = -10000, Top = -10000, Width = 1200, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            // Les onglets du tableau et de l'image n'existent que le curseur dans un tableau ou une image choisie : montrés ici.
            ribbon.TableTab.Visibility = Visibility.Visible;
            ribbon.ImageTab.Visibility = Visibility.Visible;
            TestShell.Flush();

            var home = ribbon.HomeTab;
            Assert.IsGreaterThan(0, home.ActualHeight);
            foreach (var tab in new FrameworkElement[] { ribbon.InsertTab, ribbon.TableTab, ribbon.ImageTab })
            {
                Assert.AreEqual(home.ActualHeight, tab.ActualHeight, 0.01, tab.Name);
                Assert.AreEqual(home.TranslatePoint(default, ribbon).Y, tab.TranslatePoint(default, ribbon).Y, 0.01, tab.Name);
            }

            // Leurs titres sont du texte seul, comme Accueil et Insertion.
            Assert.IsInstanceOfType<string>(ribbon.TableTab.Content);
            Assert.IsInstanceOfType<string>(ribbon.ImageTab.Content);

            // Un clic sur un onglet montre son ruban ; Entrée, dans un champ de taille, applique la valeur sans quitter le champ.
            ribbon.PageTab.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, ribbon.PagePanel.Visibility);
            var typed = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(ribbon.MarginBox)!, 0, System.Windows.Input.Key.Enter) { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent };
            ribbon.MarginBox.RaiseEvent(typed);
            Assert.IsTrue(typed.Handled);
            ribbon.ImageTab.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, ribbon.ImagePanel.Visibility);
            Assert.AreEqual(Visibility.Collapsed, ribbon.PagePanel.Visibility);

            // Et le ruban garde la hauteur de l'Accueil, quel que soit l'onglet : le texte de la fiche ne bouge pas.
            window.Width = 1900;
            ribbon.ShowTab(ribbon.HomePanel);
            TestShell.Flush();
            var height = ribbon.ActualHeight;
            Assert.IsGreaterThan(0, height);
            foreach (var panel in new Panel[] { ribbon.InsertPanel, ribbon.TablePanel, ribbon.ImagePanel, ribbon.PagePanel })
            {
                ribbon.ShowTab(panel);
                TestShell.Flush();
                Assert.AreEqual(Visibility.Visible, panel.Visibility, panel.Name);
                Assert.AreEqual(height, ribbon.ActualHeight, 0.5, panel.Name);
            }

            // Les onglets Tableau et Image sont en icônes : chaque bouton garde son nom, pour le lecteur d'écran, et le dit au survol.
            foreach (var panel in new Panel[] { ribbon.TablePanel, ribbon.ImagePanel, ribbon.PagePanel })
            {
                ribbon.ShowTab(panel);
                TestShell.Flush();
                var tools = Descendants(panel).OfType<System.Windows.Controls.Primitives.ButtonBase>().Where(tool => tool.IsVisible).ToList();
                Assert.IsGreaterThan(8, tools.Count, panel.Name);
                foreach (var tool in tools)
                {
                    var name = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(tool).GetName();
                    Assert.IsFalse(string.IsNullOrWhiteSpace(name), tool.Name);
                    Assert.IsTrue(tool.ToolTip is string { Length: > 0 }, $"{tool.Name} dit son nom au survol");
                }
                var glyphs = Descendants(panel).OfType<KyfRev.Presentation.Wpf.Editing.RibbonGlyph>().ToList();
                Assert.IsGreaterThan(6, glyphs.Count, panel.Name);
                Assert.IsTrue(glyphs.All(glyph => glyph.Icon is not null && glyph.Foreground is not null), "chaque icône a son dessin et la couleur du texte");
            }
            // Dans une fenêtre de taille courante, l'onglet Tableau tient sur une seule ligne de groupes.
            window.Width = 1100;
            ribbon.ShowTab(ribbon.TablePanel);
            TestShell.Flush();
            var groups = ribbon.TablePanel.Children.OfType<FrameworkElement>().Where(group => group.IsVisible).ToList();
            Assert.IsGreaterThan(5, groups.Count);
            Assert.AreEqual(1, groups.Select(group => Math.Round(group.TranslatePoint(default, ribbon.TablePanel).Y)).Distinct().Count(), "une seule ligne de groupes");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_tool_of_the_ribbon_shows_that_its_formatting_is_active()
    {
        var test = new TestShell();
        var tool = new Button { Content = "G" };
        var window = new Window { Content = tool, Left = -10000, Top = -10000, Width = 200, Height = 100, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        tool.SetResourceReference(FrameworkElement.StyleProperty, "Kyf.Button.Tool");
        window.Show();
        try
        {
            TestShell.Flush();
            var active = window.FindResource("Kyf.Brush.NavigationActiveBackground");
            Assert.AreNotEqual(active, tool.Background);

            // La valeur liée par le ruban est un booléen : « le texte sous le curseur est en gras ».
            tool.Tag = true;
            TestShell.Flush();
            Assert.AreEqual(active, tool.Background, "le bouton d'une mise en forme active ressort");

            tool.Tag = false;
            TestShell.Flush();
            Assert.AreNotEqual(active, tool.Background);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void Every_edge_and_corner_of_the_window_resizes_it()
    {
        var size = new Size(900, 600);
        // Les quatre coins, sur l'un ou l'autre de leurs bords.
        Assert.AreEqual(13, ShellWindow.ResizeEdge(new Point(2, 2), size), "HTTOPLEFT");
        Assert.AreEqual(14, ShellWindow.ResizeEdge(new Point(898, 2), size), "HTTOPRIGHT");
        Assert.AreEqual(14, ShellWindow.ResizeEdge(new Point(890, 3), size), "le coin se prend aussi par le bord du haut");
        Assert.AreEqual(14, ShellWindow.ResizeEdge(new Point(897, 12), size), "et par le bord de droite");
        Assert.AreEqual(16, ShellWindow.ResizeEdge(new Point(1, 598), size), "HTBOTTOMLEFT");
        Assert.AreEqual(17, ShellWindow.ResizeEdge(new Point(899, 599), size), "HTBOTTOMRIGHT");
        // Les quatre bords.
        Assert.AreEqual(10, ShellWindow.ResizeEdge(new Point(3, 300), size), "HTLEFT");
        Assert.AreEqual(11, ShellWindow.ResizeEdge(new Point(896, 300), size), "HTRIGHT");
        Assert.AreEqual(12, ShellWindow.ResizeEdge(new Point(450, 5), size), "HTTOP");
        Assert.AreEqual(15, ShellWindow.ResizeEdge(new Point(450, 595), size), "HTBOTTOM");
        // Ailleurs, rien : le bouton « Fermer » se clique à quelques pixels du coin.
        Assert.AreEqual(0, ShellWindow.ResizeEdge(new Point(880, 20), size));
        Assert.AreEqual(0, ShellWindow.ResizeEdge(new Point(890, 12), size), "près du coin, mais sur aucun bord");
        Assert.AreEqual(0, ShellWindow.ResizeEdge(new Point(450, 300), size));
        Assert.AreEqual(0, ShellWindow.ResizeEdge(new Point(-4, 300), size));
        Assert.AreEqual(0, ShellWindow.ResizeEdge(new Point(450, 700), size));
    }

    [STATestMethod]
    public void The_corner_under_the_close_button_resizes_the_window()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            var close = (Button)window.FindName("CloseButton");
            var maximize = (Button)window.FindName("MaximizeButton");
            int At(Point point)
            {
                var taken = false;
                var answer = (int)window.OnWindowMessage(handle, 0x0084, 0, ScreenPosition(window.PointToScreen(point)), ref taken);
                Assert.AreEqual(answer != 0, taken);
                return answer;
            }

            var corner = new Point(window.ActualWidth - 2, 2);
            Assert.IsTrue(close.TranslatePoint(new Point(close.ActualWidth, 0), window).X >= window.ActualWidth - 8, "« Fermer » touche le coin : sans rien faire, il l'emporterait sur le cadre");
            Assert.AreEqual(14, At(corner), "HTTOPRIGHT : la flèche en diagonale, comme dans les autres applications");
            Assert.AreEqual(13, At(new Point(2, 2)), "le coin du bouton du menu aussi");
            Assert.AreEqual(12, At(maximize.TranslatePoint(new Point(maximize.ActualWidth / 2, 2), window)), "le haut des boutons redimensionne");
            Assert.IsNull(maximize.Tag, "sans montrer « Agrandir » survolé");
            Assert.AreEqual(0, At(close.TranslatePoint(new Point(close.ActualWidth / 2, close.ActualHeight / 2), window)), "le bouton se clique toujours");

            // Agrandie ou en plein écran, la fenêtre ne se redimensionne pas : ses bords ne répondent plus.
            test.Shell.ToggleFullScreen();
            TestShell.Flush();
            Assert.AreEqual(0, At(new Point(window.ActualWidth - 2, 2)));
            test.Shell.ToggleFullScreen();
            TestShell.Flush();
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_title_bar_hides_the_menu_and_brings_it_back()
    {
        var test = new TestShell();
        var window = Open(test);
        try
        {
            var toggle = (Button)window.FindName("ExitFocusButton");
            var host = (FrameworkElement)window.FindName("MenuButtonHost");
            Assert.IsTrue(toggle.IsVisible, "toujours là : le menu se masque d'un clic");
            Assert.IsTrue(toggle.TranslatePoint(default, window).X < 60, "à gauche, au-dessus du menu");
            Assert.AreEqual("Masquer le menu (Ctrl+Maj+F)", (string)toggle.ToolTip);
            Assert.IsTrue(test.Shell.IsMenuVisible);

            toggle.Command.Execute(null);
            TestShell.Flush();

            Assert.IsTrue(test.Shell.IsFocusMode);
            Assert.IsFalse(test.Shell.IsMenuVisible);
            Assert.AreEqual("Afficher le menu (Ctrl+Maj+F)", (string)toggle.ToolTip);
            Assert.AreEqual("Afficher le menu (Ctrl+Maj+F)", System.Windows.Automation.AutomationProperties.GetName(toggle));

            toggle.Command.Execute(null);
            TestShell.Flush();

            Assert.IsFalse(test.Shell.IsFocusMode);
            Assert.IsTrue(test.Shell.IsMenuVisible);

            // En plein écran, il n'y a pas de menu à rendre : le bouton s'efface.
            test.Shell.ToggleFullScreen();
            TestShell.Flush();
            Assert.AreEqual(Visibility.Collapsed, host.Visibility);
            test.Shell.ToggleFullScreen();
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, host.Visibility);

            // Menu masqué, survoler le bouton le montre par-dessus la page ; un clic le rend pour de bon et referme l'aperçu.
            toggle.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            Assert.IsFalse(window.IsMenuPeekOpen, "menu visible : rien à montrer au survol");
            test.Shell.ToggleFocusMode();
            TestShell.Flush();
            toggle.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            TestShell.Flush();
            Assert.IsTrue(window.IsMenuPeekOpen);
            var card = (FrameworkElement)window.FindName("MenuPeekCard");
            var workspace = (FrameworkElement)window.FindName("Workspace");
            Assert.AreEqual(workspace.ActualHeight, card.ActualHeight, 2, "sur toute la hauteur de la page, jusqu'en bas de la fenêtre");
            Assert.AreEqual(test.Shell.MainNavigation.Count, ((ListBox)window.FindName("PeekMainNavigation")).Items.Count);
            test.Shell.ToggleFocusMode();
            TestShell.Flush();
            Assert.IsFalse(window.IsMenuPeekOpen);
        }
        finally
        {
            window.Close();
        }
    }

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

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }
}
