using System.Windows;
using System.Windows.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// kyfRev dans la barre d'état (rangée 25D) : le menu de son icône, la croix qui le cache au lieu de le fermer, et les
/// réglages de démarrage.
/// </summary>
[TestClass]
public sealed class TrayTests
{
    private static readonly string[] Done = ["ouvrir", "capture", "réviser", "paramètres", "quitter"];
    private static readonly string[] Entries = [TrayText.Open, TrayText.Capture, TrayText.Revise, TrayText.Settings, TrayText.Quit];

    [STATestMethod]
    public void The_menu_of_the_icon_opens_captures_revises_sets_and_quits()
    {
        var done = new List<string>();
        var actions = new TrayActions(() => done.Add("ouvrir"), () => done.Add("capture"), () => done.Add("réviser"), () => done.Add("paramètres"), () => done.Add("quitter"));

        var items = TrayMenu.Items(actions, "Ctrl+Alt+N");

        CollectionAssert.AreEqual(Entries, items.OfType<MenuItem>().Select(item => (string)item.Header).ToArray());
        Assert.HasCount(2, items.OfType<Separator>(), "trois blocs : aller, paramètres, quitter");
        Assert.AreEqual(FontWeights.SemiBold, ((MenuItem)items[0]).FontWeight, "ouvrir : l'entrée principale");
        Assert.AreEqual("Ctrl+Alt+N", ((MenuItem)items[1]).InputGestureText, "le raccourci global, quand il est demandé à Windows");
        Assert.IsTrue(string.IsNullOrEmpty(((MenuItem)TrayMenu.Items(actions, null)[1]).InputGestureText));
        foreach (var item in items.OfType<MenuItem>())
        {
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
        CollectionAssert.AreEqual(Done, done);
    }

    [STATestMethod]
    public void The_cross_hides_kyfrev_in_the_tray_when_asked_and_quit_closes_it()
    {
        var test = new TestShell();
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var inTray = true;
        var hidden = 0;
        var closed = false;
        window.ClosesToTray = () => inTray;
        window.HiddenToTray += (_, _) => hidden++;
        window.Closed += (_, _) => closed = true;
        window.Show();
        try
        {
            window.Close();
            Assert.IsFalse(closed, "la croix le cache");
            Assert.IsFalse(window.IsVisible);
            Assert.AreEqual(1, hidden);

            window.Show();
            window.Quit();
            Assert.IsTrue(closed, "« Quitter kyfRev » ferme pour de bon");
        }
        finally
        {
            inTray = false;
            window.Close();
        }
    }

    [TestMethod]
    public void The_startup_settings_are_kept_on_this_computer()
    {
        var store = new MemoryMachinePreferences();
        var page = new ShortcutsSettingsViewModel(new MachineSettings(store));
        page.OnNavigatedTo();
        Assert.IsFalse(page.CloseToTray);
        Assert.IsFalse(page.StartWithSession);
        Assert.IsTrue(page.StartInTray, "lancé par Windows, il reste discret");
        Assert.IsFalse(page.StartsOpen);

        page.CloseToTray = true;
        page.StartWithSession = true;
        page.StartsOpen = true;
        Assert.IsTrue(store.Stored.CloseToTray);
        Assert.IsTrue(store.Stored.StartWithSession);
        Assert.IsFalse(store.Stored.StartInTray);
        Assert.IsFalse(page.StartInTray);
        page.StartsOpen = false;
        Assert.IsFalse(store.Stored.StartInTray, "décocher un côté du choix ne choisit pas l'autre");
        page.StartInTray = true;
        Assert.IsTrue(store.Stored.StartInTray);

        // Relus tels qu'ils sont gardés.
        var again = new ShortcutsSettingsViewModel(new MachineSettings(store));
        again.OnNavigatedTo();
        Assert.IsTrue(again.CloseToTray && again.StartWithSession && again.StartInTray);
    }
}
