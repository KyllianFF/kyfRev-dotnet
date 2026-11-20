using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using KyfRev.Application.Ports;
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

    [STATestMethod]
    public void What_a_window_puts_off_the_screen_is_measured()
    {
        var chrome = new WindowChrome();
        Assert.AreEqual(default, chrome.MaximizedOverhang(0));

        var work = SystemParameters.WorkArea;
        using var inside = new HwndSource(new HwndSourceParameters("kyfRev essai") { WindowStyle = unchecked((int)0x00CF0000), PositionX = (int)work.Left + 50, PositionY = (int)work.Top + 50, Width = 300, Height = 200 });
        Assert.AreEqual(default, chrome.MaximizedOverhang(inside.Handle), "Une fenêtre dans l'écran n'en dépasse pas.");
        Assert.AreEqual(default, chrome.WorkArea(0));
        var area = chrome.WorkArea(inside.Handle);
        Assert.IsGreaterThan(0, area.Width, "L'écran de la fenêtre a une largeur.");
        Assert.IsGreaterThan(0, area.Height, "Et une hauteur.");

        using var outside = new HwndSource(new HwndSourceParameters("kyfRev essai") { WindowStyle = unchecked((int)0x00CF0000), PositionX = -10000, PositionY = -10000, Width = 300, Height = 200 });
        var overhang = chrome.MaximizedOverhang(outside.Handle);
        Assert.IsGreaterThan(0, overhang.Left + overhang.Top, "Hors de l'écran, à gauche et en haut.");
        Assert.AreEqual(0, overhang.Right);
        Assert.AreEqual(0, overhang.Bottom);
    }

    [TestMethod]
    public void A_maximized_window_stops_short_of_each_edge_where_the_taskbar_hides()
    {
        var monitor = new ScreenBounds(0, 0, 1920, 1080);
        var proposed = new ScreenBounds(-8, -8, 1928, 1088);

        Assert.AreEqual(proposed, WindowChrome.Trimmed(proposed, monitor, ScreenEdges.None));
        Assert.AreEqual(proposed with { Bottom = 1078 }, WindowChrome.Trimmed(proposed, monitor, ScreenEdges.Bottom), "Deux pixels pour la souris, en bas.");
        Assert.AreEqual(
            new ScreenBounds(2, 2, 1918, 1078),
            WindowChrome.Trimmed(proposed, monitor, ScreenEdges.Left | ScreenEdges.Top | ScreenEdges.Right | ScreenEdges.Bottom));
        var second = new ScreenBounds(1920, -200, 4480, 1240);
        Assert.AreEqual(
            new ScreenBounds(1922, -208, 4488, 1248),
            WindowChrome.Trimmed(new ScreenBounds(1912, -208, 4488, 1248), second, ScreenEdges.Left),
            "Un second écran, à droite et plus haut : ses propres bords.");
        var inside = new ScreenBounds(100, 100, 500, 400);
        Assert.AreEqual(inside, WindowChrome.Trimmed(inside, monitor, ScreenEdges.Bottom | ScreenEdges.Left), "Une zone loin du bord ne bouge pas.");
    }

    [STATestMethod]
    public void Only_a_maximized_window_makes_room_for_a_taskbar_that_hides()
    {
        var chrome = new WindowChrome();
        Assert.IsFalse(chrome.LeaveRoomForHiddenTaskbar(0, 0));
        // NCCALCSIZE_PARAMS : trois rectangles, puis un pointeur.
        var parameters = Marshal.AllocHGlobal((3 * 16) + nint.Size);
        void Propose(ScreenBounds bounds)
        {
            foreach (var (offset, value) in new[] { (0, bounds.Left), (4, bounds.Top), (8, bounds.Right), (12, bounds.Bottom) })
            {
                Marshal.WriteInt32(parameters, offset, value);
            }
        }
        ScreenBounds Proposed() => new(Marshal.ReadInt32(parameters, 0), Marshal.ReadInt32(parameters, 4), Marshal.ReadInt32(parameters, 8), Marshal.ReadInt32(parameters, 12));
        try
        {
            var work = SystemParameters.WorkArea;
            using var normal = new HwndSource(new HwndSourceParameters("kyfRev essai") { WindowStyle = unchecked((int)0x00CF0000), PositionX = (int)work.Left + 50, PositionY = (int)work.Top + 50, Width = 300, Height = 200 });
            var everywhere = new ScreenBounds(-100_000, -100_000, 100_000, 100_000);
            Propose(everywhere);
            Assert.IsFalse(chrome.LeaveRoomForHiddenTaskbar(normal.Handle, parameters), "Une fenêtre qui n'est pas agrandie dessine où elle veut.");
            Assert.AreEqual(everywhere, Proposed());
            Assert.IsFalse(chrome.LeaveRoomForHiddenTaskbar(normal.Handle, 0));

            // WS_OVERLAPPEDWINDOW | WS_MAXIMIZE, jamais affichée. Ce poste masque ou non sa barre : la réponse le suit.
            using var maximized = new HwndSource(new HwndSourceParameters("kyfRev essai") { WindowStyle = unchecked((int)0x01CF0000), PositionX = (int)work.Left + 50, PositionY = (int)work.Top + 50, Width = 300, Height = 200 });
            var trimmed = chrome.LeaveRoomForHiddenTaskbar(maximized.Handle, parameters);
            var room = Proposed();
            Assert.AreEqual(trimmed, room != everywhere, "Rognée si, et seulement si, une barre se masque sur un bord de son écran.");
            Assert.IsTrue(room.Left >= everywhere.Left && room.Top >= everywhere.Top && room.Right <= everywhere.Right && room.Bottom <= everywhere.Bottom, "Toujours vers l'intérieur.");
        }
        finally
        {
            Marshal.FreeHGlobal(parameters);
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
    public void A_window_frame_is_recalculated_without_moving_or_resizing_the_window()
    {
        var chrome = new WindowChrome();
        chrome.RefreshFrame(0);

        // Le cadre recalculé, Windows renvoie le calcul de la zone cliente ; la fenêtre ne bouge ni ne change de taille.
        var asked = 0;
        using var window = new HwndSource(new HwndSourceParameters("kyfRev essai") { WindowStyle = unchecked((int)0x00CF0000), PositionX = -10000, PositionY = -10000, Width = 300, Height = 200 });
        window.AddHook((nint _, int message, nint _, nint _, ref bool _) =>
        {
            if (message == 0x0083)
            {
                asked++;
            }
            return 0;
        });
        var before = chrome.VisibleBounds(window.Handle);
        chrome.RefreshFrame(window.Handle);
        Assert.IsGreaterThan(0, asked, "WM_NCCALCSIZE renvoyé");
        Assert.AreEqual(before, chrome.VisibleBounds(window.Handle));
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
