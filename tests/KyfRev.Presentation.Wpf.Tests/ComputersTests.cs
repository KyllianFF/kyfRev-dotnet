using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Rangée 29A : un coffre qu'un autre poste a ouvert. La question avant de l'ouvrir, dans la fenêtre, ses trois boutons ;
/// la bande « Lecture seule » et « Passer en écriture » ; l'avis quand un autre poste l'ouvre ensuite.
/// </summary>
[TestClass]
public sealed class ComputersTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void The_question_says_who_and_since_when()
    {
        var question = new OccupiedVaultViewModel([new OtherComputer("PC-BUREAU", new DateTimeOffset(2026, 11, 2, 14, 5, 0, TimeSpan.Zero), Nine)], Nine.AddSeconds(40), TimeZoneInfo.Utc);
        Assert.AreEqual("PC-BUREAU", question.ComputerName);
        StringAssert.EndsWith(question.Seen, "· vu à l'instant", StringComparison.Ordinal);
        Assert.AreEqual("depuis 9 h 05 · vu il y a 1 minute", ComputersText.SeenOf(new DateTimeOffset(2026, 11, 2, 9, 5, 0, TimeSpan.Zero), TimeSpan.FromSeconds(90)));
        Assert.AreEqual("depuis 14 h 30 · vu il y a 2 minutes", ComputersText.SeenOf(new DateTimeOffset(2026, 11, 2, 14, 30, 0, TimeSpan.Zero), TimeSpan.FromMinutes(2.5)));
        Assert.AreEqual("PC-A, PC-B", new OccupiedVaultViewModel([new OtherComputer("PC-A", Nine, Nine), new OtherComputer("PC-B", Nine, Nine)], Nine).ComputerName);

        question.Choose(DialogChoice.None);
        Assert.AreEqual(OccupiedChoice.Cancel, question.Answer.Result, "Échap : ne pas l'ouvrir");
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void A_vault_open_elsewhere_asks_in_the_window_then_opens_for_reading_then_for_writing(bool dark)
    {
        using var ui = new UiThread();
        var time = new FakeTime(Nine);
        using var vaults = new TestVaults(AppMode.Development, time);
        var root = vaults.CreateVault("Coffre");
        Assert.IsTrue(ComputerPresence.Announce(root, DiskStorage.Instance, "PC-BUREAU", Nine.AddMinutes(-55), Nine, open: true));
        using var computers = new ComputersViewModel(vaults.App.Vaults, time);
        AppState? reopened = null;
        computers.Reopened = state => reopened = state;
        var test = new TestShell(dark);
        test.Shell.Computers = computers;
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 470, Height = 540, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            Assert.IsFalse(window.OccupiedDialog.IsVisible);
            Assert.IsFalse(window.ReadOnlyBanner.IsVisible);

            // La question : qui, depuis quand, et les trois réponses, dans la largeur.
            var held = vaults.App.Vaults.Open(root);
            Assert.AreEqual(root, held.Occupied);
            var settling = computers.SettleAsync(held, () => vaults.App.Vaults.Open(root));
            TestShell.Flush();
            Assert.IsTrue(window.OccupiedDialog.IsVisible);
            Assert.AreEqual("PC-BUREAU", window.OccupiedName.Text);
            Assert.AreEqual(ComputersText.SeenOf(Nine.AddMinutes(-55), TimeSpan.Zero), window.OccupiedSeen.Text);
            Assert.AreEqual(ComputersText.OccupiedReadOnly, Button(window.OccupiedDialog, Dialog.ExtraButtonPart).Content);
            Assert.IsTrue(Button(window.OccupiedDialog, Dialog.ExtraButtonPart).IsVisible);
            AssertFits(window, window.OccupiedDialog);
            Capture(window, $"autre-poste-question-{(dark ? "sombre" : "clair")}");

            // « Lecture seule » : le coffre s'ouvre sans rien écrire, la bande le dit.
            Press(Button(window.OccupiedDialog, Dialog.ExtraButtonPart));
            UiThread.Until(() => settling.IsCompleted);
            var reading = settling.Result!;
            Assert.IsTrue(reading.ReadOnly);
            computers.Show(reading);
            TestShell.Flush();
            Assert.IsFalse(window.OccupiedDialog.IsVisible);
            Assert.IsTrue(window.ReadOnlyBanner.IsVisible);
            Assert.AreEqual(ComputersText.ReadOnlyBannerOf("PC\u2011BUREAU"), window.ReadOnlyText.Text, "le nom ne se coupe pas à son tiret");
            AssertFits(window, window.ReadOnlyBanner);
            Capture(window, $"autre-poste-lecture-seule-{(dark ? "sombre" : "clair")}");

            // Un autre poste l'ouvre ensuite : l'avis paraît, et reste jusqu'à la croix.
            Assert.IsTrue(ComputerPresence.Announce(root, DiskStorage.Instance, "PC-SALON", Nine, Nine, open: true));
            computers.Listen();
            UiThread.Until(() => computers.HasArrival);
            Assert.AreEqual(ComputersText.ArrivedOf("PC-SALON"), computers.Arrival);
            computers.Listen();
            TestShell.Flush();
            Assert.AreEqual(ComputersText.ArrivedOf("PC-SALON"), computers.Arrival, "il reste, même sans nouveau poste");
            computers.CloseArrivalCommand.Execute(null);
            Assert.IsFalse(computers.HasArrival);

            // « Passer en écriture » : rouvert comme d'habitude.
            Press(window.WritableButton);
            UiThread.Until(() => reopened is not null);
            Assert.AreEqual(root, reopened!.Vault);
            Assert.IsFalse(reopened.ReadOnly);
            computers.Show(reopened);
            TestShell.Flush();
            Assert.IsFalse(window.ReadOnlyBanner.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Answering_cancel_opens_nothing_and_without_a_vault_nothing_beats()
    {
        using var ui = new UiThread();
        var time = new FakeTime(Nine);
        using var vaults = new TestVaults(AppMode.Development, time);
        var root = vaults.CreateVault("Coffre");
        Assert.IsTrue(ComputerPresence.Announce(root, DiskStorage.Instance, "PC-BUREAU", Nine, Nine, open: true));
        using var computers = new ComputersViewModel(vaults.App.Vaults, time);

        var settling = computers.SettleAsync(vaults.App.Vaults.Open(root), () => vaults.App.Vaults.Open(root));
        Assert.IsTrue(computers.IsAsking);
        computers.Answer(DialogChoice.Secondary);
        UiThread.Until(() => settling.IsCompleted);

        Assert.IsNull(settling.Result);
        Assert.IsFalse(computers.IsAsking);
        Assert.IsNull(vaults.App.Vaults.State().Vault);
        computers.Show(vaults.App.Vaults.State());
        Assert.IsFalse(computers.IsReadOnly);
        Assert.IsNull(computers.ReadOnlyText);
        computers.Listen();
        TestShell.Flush();
        Assert.IsFalse(computers.HasArrival, "sans coffre, aucun poste n'est regardé");
    }

    private static Button Button(Dialog dialog, string part) => (Button)dialog.Template.FindName(part, dialog);

    private static void Press(ButtonBase button) =>
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button).GetPattern(PatternInterface.Invoke)).Invoke();

    private static void AssertFits(Window window, FrameworkElement root)
    {
        var content = (FrameworkElement)window.Content;
        foreach (var element in Descendants(root).OfType<FrameworkElement>().Where(element => element.IsVisible && element is Control or TextBlock))
        {
            var right = element.TranslatePoint(new Point(element.ActualWidth, 0), content).X;
            Assert.IsLessThanOrEqualTo(content.ActualWidth + 0.5, right, $"{element.Name} dépasse à droite");
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void Capture(Window window, string name) => RuledPaperPixels.Capture((FrameworkElement)window.Content, name);
}
