using System.Windows;
using System.Windows.Interop;
using KyfRev.Application.Model;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>L'apparence suit Windows et le choix de l'utilisateur ; le contraste élevé passe avant tout.</summary>
[TestClass]
public sealed class AppearanceTests
{
    [STATestMethod]
    public void The_theme_follows_windows_unless_chosen()
    {
        var test = new TestShell(dark: true);
        Assert.AreEqual(ThemeKind.Dark, test.Appearance.Theme);

        test.Appearance.Preference = ThemePreference.Light;
        Assert.AreEqual(ThemeKind.Light, test.Design.Theme);
        test.System.PrefersDarkTheme = false;
        test.Appearance.Preference = ThemePreference.Dark;
        Assert.AreEqual(ThemeKind.Dark, test.Design.Theme);
        test.Appearance.Preference = ThemePreference.System;
        Assert.AreEqual(ThemeKind.Light, test.Design.Theme);
        Assert.AreSame(test.Design.Dictionary, test.Appearance.Resources);
    }

    [STATestMethod]
    public void High_contrast_and_reduced_motion_follow_windows_as_they_change()
    {
        var test = new TestShell();
        var changes = new List<string?>();
        test.Appearance.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        test.Appearance.Preference = ThemePreference.Dark;

        test.System.HighContrast = true;
        test.System.ReducedMotion = true;
        test.System.RaiseChanged();
        TestShell.Flush();

        Assert.AreEqual(ThemeKind.HighContrast, test.Design.Theme);
        Assert.IsTrue(test.Design.ReducedMotion);
        CollectionAssert.Contains(changes, nameof(AppearanceController.Theme));
        test.Appearance.Dispose();
        test.System.HighContrast = false;
        test.System.RaiseChanged();
        TestShell.Flush();
        Assert.AreEqual(ThemeKind.HighContrast, test.Design.Theme, "plus suivi après Dispose");
    }

    [STATestMethod]
    public void The_title_bar_matches_the_theme()
    {
        var test = new TestShell(dark: true);
        var window = new Window();
        test.Appearance.Attach(window);
        Assert.IsEmpty(test.Chrome.Calls);

        var handle = new WindowInteropHelper(window).EnsureHandle();
        Assert.AreEqual((handle, true), test.Chrome.Calls.Last());
        test.Appearance.Preference = ThemePreference.Light;
        Assert.AreEqual((handle, false), test.Chrome.Calls.Last());

        window.Close();
        var calls = test.Chrome.Calls.Count;
        test.Appearance.Preference = ThemePreference.Dark;
        Assert.HasCount(calls, test.Chrome.Calls);
        Assert.ThrowsExactly<ArgumentNullException>(() => test.Appearance.Attach(null!));
    }

    [STATestMethod]
    public void The_gallery_changes_the_theme_of_the_whole_application()
    {
        var test = new TestShell();
        var gallery = test.Gallery;
        var changes = 0;
        gallery.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(GalleryPageViewModel.SelectedTheme) ? 1 : 0;
        Assert.AreEqual(ThemePreference.System, gallery.SelectedTheme.Preference);
        Assert.AreEqual("Comme Windows", gallery.SelectedTheme.ToString());

        gallery.SelectedTheme = gallery.Themes[2];

        Assert.AreEqual(ThemeKind.Dark, test.Design.Theme);
        Assert.AreEqual(1, changes);
        test.Appearance.Preference = ThemePreference.Light;
        Assert.AreEqual("Clair", gallery.SelectedTheme.Label);
        Assert.ThrowsExactly<ArgumentNullException>(() => gallery.SelectedTheme = null!);
        Assert.ThrowsExactly<ArgumentNullException>(() => new GalleryPageViewModel(null!));
    }

    [STATestMethod]
    public void The_controller_needs_all_its_parts()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        Assert.ThrowsExactly<ArgumentNullException>(() => new AppearanceController(null!, new FakeAppearance(), new FakeChrome()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AppearanceController(design, null!, new FakeChrome()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AppearanceController(design, new FakeAppearance(), null!));
    }
}
