using System.Globalization;
using System.Windows;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La disposition du menu selon la largeur de la fenêtre, et la taille minimale de celle-ci selon le zoom et l'écran.</summary>
[TestClass]
public sealed class ShellLayoutTests
{
    [TestMethod]
    [DataRow(360.0, 1.0, NavigationLayout.Rail)]
    [DataRow(600.0, 1.0, NavigationLayout.Rail)]
    [DataRow(1399.0, 1.0, NavigationLayout.Rail)]
    [DataRow(1400.0, 1.0, NavigationLayout.Sidebar)]
    [DataRow(2560.0, 1.0, NavigationLayout.Sidebar)]
    [DataRow(1400.0, 1.5, NavigationLayout.Rail)]
    [DataRow(1000.0, 2.0, NavigationLayout.Rail)]
    [DataRow(1200.0, 0.8, NavigationLayout.Sidebar)]
    public void The_layout_follows_the_width_seen_at_the_current_zoom(double width, double zoom, NavigationLayout expected) =>
        Assert.AreEqual(expected, ShellLayout.For(width, zoom));

    [TestMethod]
    public void A_zoom_is_always_positive()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ShellLayout.For(800, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ShellLayout.For(800, -1));
    }

    [TestMethod]
    public void The_converters_show_and_align_the_parts_of_the_menu()
    {
        var culture = CultureInfo.InvariantCulture;
        var shown = new LayoutVisibilityConverter();
        Assert.AreEqual(Visibility.Visible, shown.Convert(NavigationLayout.Sidebar, typeof(Visibility), "Sidebar", culture));
        Assert.AreEqual(Visibility.Collapsed, shown.Convert(NavigationLayout.Rail, typeof(Visibility), "Sidebar", culture));
        Assert.AreEqual(Visibility.Collapsed, shown.Convert("Sidebar", typeof(Visibility), "Sidebar", culture));
        Assert.AreEqual(Visibility.Collapsed, shown.Convert(NavigationLayout.Sidebar, typeof(Visibility), null!, culture));
        Assert.ThrowsExactly<NotSupportedException>(() => shown.ConvertBack(Visibility.Visible, typeof(NavigationLayout), "Sidebar", culture));

    }

    [TestMethod]
    public void A_view_knows_when_a_width_reaches_its_threshold()
    {
        var culture = CultureInfo.InvariantCulture;
        var atLeast = new KyfRev.Presentation.Wpf.Pages.WidthAtLeastConverter();
        Assert.AreEqual(true, atLeast.Convert(720.0, typeof(bool), "720", culture));
        Assert.AreEqual(false, atLeast.Convert(719.5, typeof(bool), "720", culture));
        Assert.AreEqual(false, atLeast.Convert("800", typeof(bool), "720", culture));
        Assert.AreEqual(false, atLeast.Convert(800.0, typeof(bool), null!, culture));
        Assert.ThrowsExactly<NotSupportedException>(() => atLeast.ConvertBack(true, typeof(double), "720", culture));
    }

    [STATestMethod]
    public void The_shell_recomputes_its_layout_with_the_width_and_the_zoom()
    {
        var shell = new TestShell().Shell;
        var changes = new List<string?>();
        shell.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.AreEqual(NavigationLayout.Sidebar, shell.Layout);

        shell.WindowWidth = 1024;
        Assert.AreEqual(NavigationLayout.Rail, shell.Layout);
        Assert.Contains(nameof(ShellViewModel.Layout), changes);

        shell.WindowWidth = 1500;
        Assert.AreEqual(NavigationLayout.Sidebar, shell.Layout);
        changes.Clear();
        shell.ZoomIn();
        Assert.AreEqual(NavigationLayout.Rail, shell.Layout);
        Assert.Contains(nameof(ShellViewModel.Layout), changes);
    }

    [STATestMethod]
    public void The_window_keeps_six_hundred_pixels_seen_whatever_the_zoom()
    {
        var shell = new TestShell().Shell;
        Assert.AreEqual(600, shell.MinimumWindowWidth);
        Assert.AreEqual(540, shell.MinimumWindowHeight);

        shell.SetZoom(1.5);
        Assert.AreEqual(900, shell.MinimumWindowWidth, 1e-9);
        Assert.AreEqual(810, shell.MinimumWindowHeight, 1e-9);
    }

    [STATestMethod]
    public void A_small_screen_caps_the_zoom_without_forgetting_the_chosen_one()
    {
        var shell = new TestShell().Shell;
        var changes = new List<string?>();
        shell.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        shell.SetZoom(1.5);
        shell.FitScreen(0, 0);
        Assert.AreEqual(ShellViewModel.MaximumZoom, shell.ZoomLimit, "sans écran connu, rien ne change");

        // Un portable de 1366 × 768 à 125 % : 1093 × 574 pixels indépendants, barre des tâches ôtée.
        shell.FitScreen(1093, 574);
        Assert.AreEqual(1.8, shell.ZoomLimit, 1e-9);
        Assert.AreEqual(1.5, shell.Zoom);
        Assert.AreEqual(574, shell.MinimumWindowHeight, "jamais plus haute que l'écran");
        Assert.Contains(nameof(ShellViewModel.MinimumWindowHeight), changes);

        shell.FitScreen(800, 600);
        Assert.AreEqual(1.3, shell.ZoomLimit, 1e-9);
        Assert.AreEqual(1.3, shell.Zoom, 1e-9);
        Assert.AreEqual(780, shell.MinimumWindowWidth, 1e-9);
        shell.ZoomIn();
        Assert.AreEqual(1.3, shell.Zoom, 1e-9, "l'écran ne tient pas plus");
        Assert.AreEqual(1.5, shell.ChosenZoom, "le zoom choisi n'est pas oublié");

        shell.FitScreen(1920, 1040);
        Assert.AreEqual(1.5, shell.Zoom, "un écran assez grand rend le zoom choisi");
        shell.ZoomIn();
        Assert.AreEqual(1.6, shell.ChosenZoom, 1e-9);

        shell.FitScreen(400, 300);
        Assert.AreEqual(ShellViewModel.MinimumZoom, shell.ZoomLimit, "le zoom ne descend jamais sous son minimum");
        Assert.AreEqual(ShellViewModel.MinimumZoom, shell.Zoom);
        shell.ZoomOut();
        Assert.AreEqual(ShellViewModel.MinimumZoom, shell.ChosenZoom, "réduire, c'est choisir");
    }
}
