using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>
/// Les préférences d'affichage lues dans Windows, comparées à celles que lit WPF, et la barre de
/// titre d'une vraie fenêtre (non affichée).
/// </summary>
[TestClass]
public sealed class WindowsAppearanceTests
{
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    [TestMethod]
    public void High_contrast_and_animations_match_what_wpf_reads()
    {
        using var appearance = new WindowsAppearance();

        Assert.AreEqual(SystemParameters.HighContrast, appearance.HighContrast);
        Assert.AreEqual(!SystemParameters.ClientAreaAnimation, appearance.ReducedMotion);
        Assert.AreEqual(WindowsAppearance.ReadPrefersDark(Registry.CurrentUser), appearance.PrefersDarkTheme);
    }

    [TestMethod]
    public void The_app_theme_is_read_from_the_personalization_key()
    {
        var name = $@"Software\kyfRev-essais\{Guid.NewGuid():N}";
        using var root = Registry.CurrentUser.CreateSubKey(name);
        try
        {
            Assert.IsFalse(WindowsAppearance.ReadPrefersDark(root), "clé absente : thème clair");
            using (var key = root.CreateSubKey(Personalize))
            {
                key.SetValue("AppsUseLightTheme", 0, RegistryValueKind.DWord);
            }
            Assert.IsTrue(WindowsAppearance.ReadPrefersDark(root));
            using (var key = root.CreateSubKey(Personalize))
            {
                key.SetValue("AppsUseLightTheme", 1, RegistryValueKind.DWord);
            }
            Assert.IsFalse(WindowsAppearance.ReadPrefersDark(root));
            using (var key = root.CreateSubKey(Personalize))
            {
                key.SetValue("AppsUseLightTheme", "0", RegistryValueKind.String);
            }
            Assert.IsFalse(WindowsAppearance.ReadPrefersDark(root), "valeur d'un autre type : ignorée");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
        }
    }

    [TestMethod]
    public void Only_display_preferences_are_reported_as_changes()
    {
        using var appearance = new WindowsAppearance();
        var changes = 0;
        appearance.Changed += (sender, _) =>
        {
            Assert.AreSame(appearance, sender);
            changes++;
        };

        foreach (var category in Enum.GetValues<UserPreferenceCategory>())
        {
            appearance.OnUserPreferenceChanged(this, new UserPreferenceChangedEventArgs(category));
        }

        Assert.AreEqual(4, changes);
        appearance.Dispose();
        appearance.Dispose();
    }

    [STATestMethod]
    public void The_title_bar_follows_the_theme()
    {
        var chrome = new WindowChrome();
        Assert.IsFalse(chrome.UseDarkTitleBar(0, dark: true));

        using var window = new HwndSource(new HwndSourceParameters("kyfRev essai") { WindowStyle = unchecked((int)0x00CF0000) });
        Assert.IsTrue(chrome.UseDarkTitleBar(window.Handle, dark: true));
        Assert.IsTrue(chrome.UseDarkTitleBar(window.Handle, dark: false));
    }
}
