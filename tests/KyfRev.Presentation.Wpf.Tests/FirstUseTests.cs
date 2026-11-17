using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La première utilisation : la carte « Premiers pas » de la vue « Aujourd'hui », dont les pas se cochent seuls, et ce que
/// l'ordinateur en retient.
/// </summary>
[TestClass]
public sealed class FirstUseTests
{
    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose", "Étapes"], 5, "sha256:aa");

    [TestMethod]
    public async Task The_first_steps_tick_themselves_as_the_vault_fills()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
        using var test = new TestVaults(AppMode.Development, time);
        var app = test.App;
        Assert.IsNotNull(app.Vaults.Create(test.PathOf("Coffre")).Vault);
        var guides = 0;
        var memory = new FirstUse();
        var steps = new FirstSteps(memory, () => guides++);
        var page = new DashboardPageViewModel(app.Reminders, app.Sheets, new FakeOpener()) { FirstSteps = steps };

        // Un coffre neuf : la carte tient la place de l'état vide, le premier pas est le prochain.
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.AreSame(steps, page.Rows.Single(row => row is FirstSteps));
        Assert.IsFalse(page.Rows.Any(row => row is EmptyDashboardRow));
        Assert.AreEqual("0 sur 3", steps.Progress);
        Assert.AreEqual("Premiers pas, 0 sur 3", steps.ToString());
        Assert.IsTrue(steps.Steps[0] is { IsCurrent: true, IsDone: false, OffersNewSheet: true, Number: "1" });
        Assert.IsTrue(steps.Steps.Skip(1).All(step => step is { IsCurrent: false, IsDone: false, OffersNewSheet: false }));
        StringAssert.StartsWith(steps.Steps[0].Spoken, "Écrire votre première fiche. ", StringComparison.Ordinal);

        // « Nouvelle fiche » ouvre la fenêtre de la vue ; « Lire le guide » ouvre le guide.
        steps.StartSheetCommand.Execute(null);
        Assert.IsTrue(page.IsCreatingSheet);
        await page.AnswerNewSheetCommand.ExecuteAsync(false);
        Assert.IsTrue(steps.CanOpenGuide);
        steps.OpenGuideCommand.Execute(null);
        Assert.AreEqual(1, guides);

        // Une fiche écrite : le premier pas est fait. Elle n'est due que demain, la carte reste là.
        var sheet = app.Sheets.Create("Biologie", "Mitose", null).Value;
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsTrue(page.Rows.Contains(steps));
        Assert.AreEqual("1 sur 3", steps.Progress);
        Assert.IsTrue(steps.Steps[0] is { IsDone: true, IsCurrent: false, OffersNewSheet: false });
        Assert.AreEqual("Écrire votre première fiche : fait", steps.Steps[0].ToString());
        Assert.IsTrue(steps.Steps[1].IsCurrent);

        // Le lendemain, la fiche est due : la liste reprend sa place.
        time.AdvanceDays(1);
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsFalse(page.Rows.Contains(steps), "une révision attend : la liste, pas la carte");
        Assert.IsTrue(memory.Has(FirstUse.SheetWritten));
        Assert.IsFalse(memory.Has(FirstUse.SheetReviewed));

        // Validée : le deuxième pas est fait, et la carte revient avec le troisième.
        Assert.IsTrue(app.Reviews.Validate(ItemKind.Sheet, sheet).Succeeded);
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsTrue(page.Rows.Contains(steps));
        Assert.AreEqual("2 sur 3", steps.Progress);
        Assert.IsTrue(steps.Steps[2].IsCurrent);

        // Un passage surligné : les trois pas sont faits, la carte ne revient plus.
        app.Annotations.Create(sheet, Passage, null).Value.ToString();
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsTrue(memory.Has(FirstUse.PassageMarked));
        Assert.AreEqual("3 sur 3", steps.Progress);
        Assert.IsFalse(page.Rows.Contains(steps));
        time.AdvanceDays(40);
        Assert.IsTrue(app.Reviews.Validate(ItemKind.Passage, app.Sheets.Open(sheet).Value.Annotations.Single().Id).Succeeded);
        Assert.IsFalse(new FirstSteps(memory).Follow(app.Reminders.Dashboard().Value), "retenu : une autre séance ne la remontre pas");
    }

    [TestMethod]
    public async Task The_first_steps_can_be_hidden_and_stay_out_of_a_vault_that_is_no_longer_new()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
        using var test = new TestVaults(AppMode.Development, time);
        var app = test.App;
        Assert.IsNotNull(app.Vaults.Create(test.PathOf("Coffre")).Vault);
        var memory = new FirstUse();
        var steps = new FirstSteps(memory);
        Assert.IsFalse(steps.CanOpenGuide);
        steps.OpenGuideCommand.Execute(null);
        steps.StartSheetCommand.Execute(null);
        var page = new DashboardPageViewModel(app.Reminders, app.Sheets, new FakeOpener()) { FirstSteps = steps };
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsTrue(page.Rows.Contains(steps));

        // « Masquer les premiers pas » : l'état vide revient aussitôt, et pour de bon.
        steps.HideCommand.Execute(null);
        Assert.IsFalse(page.Rows.Contains(steps));
        Assert.IsTrue(page.Rows.Any(row => row is EmptyDashboardRow));
        Assert.IsTrue(memory.Has(FirstUse.StepsHidden));
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsFalse(page.Rows.Contains(steps));

        // Un coffre qui a déjà plusieurs fiches ne débute plus : même sans rien masquer, la carte ne s'y montre pas.
        var fresh = new FirstSteps(new FirstUse());
        Assert.IsTrue(fresh.Follow(app.Reminders.Dashboard().Value));
        for (var index = 0; index <= FirstSteps.YoungVault; index++)
        {
            app.Sheets.Create(null, $"Fiche {index}", null).Value.ToString();
        }
        Assert.IsFalse(fresh.Follow(app.Reminders.Dashboard().Value));
        Assert.AreEqual("1 sur 3", fresh.Progress, "ce qu'il montre est coché quand même");
        Assert.ThrowsExactly<ArgumentNullException>(() => new FirstSteps(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => fresh.Follow(null!));
    }

    [TestMethod]
    public void What_the_first_use_has_shown_is_kept_with_the_machine_preferences()
    {
        var store = new MemoryMachinePreferences();
        var machine = new MachineSettings(store);
        var memory = new FirstUse(machine);
        var changes = 0;
        memory.Changed += (_, _) => changes++;

        Assert.IsFalse(memory.Has(FirstUse.StepsHidden));
        memory.Remember(FirstUse.StepsHidden);
        memory.Remember(FirstUse.ReviewTipRead);
        memory.Remember(FirstUse.StepsHidden);

        Assert.AreEqual(2, changes, "une seule fois chacun");
        Assert.AreEqual("aide-revision;pas-masques", store.Stored.FirstUse);
        Assert.IsTrue(new FirstUse(new MachineSettings(store)).Has(FirstUse.ReviewTipRead), "relu au démarrage suivant");
        Assert.IsFalse(memory.Has(FirstUse.NoTips));

        // Sans préférences (un essai, un poste où elles ne s'écrivent pas) : le temps de la séance.
        var session = new FirstUse();
        session.Remember(FirstUse.WritingTipRead);
        Assert.IsTrue(session.Has(FirstUse.WritingTipRead));
        Assert.IsFalse(new FirstUse().Has(FirstUse.WritingTipRead));
    }

    [STATestMethod]
    [DataRow(470, true)]
    [DataRow(900, false)]
    [DataRow(1700, true)]
    public void The_first_steps_card_holds_at_every_width_in_both_themes(int width, bool dark)
    {
        using var ui = new UiThread();
        var time = new FakeTime(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
        using var vaults = new TestVaults(AppMode.Development, time);
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        vaults.App.Sheets.Create("Biologie", "Mitose", null).Value.ToString();
        var test = new TestShell();
        test.Design.Theme = dark ? DesignSystem.Tokens.ThemeKind.Dark : DesignSystem.Tokens.ThemeKind.Light;
        var steps = new FirstSteps(new FirstUse(), () => { });
        var model = new DashboardPageViewModel(vaults.App.Reminders, vaults.App.Sheets, new FakeOpener()) { FirstSteps = steps };
        var page = new DashboardPage { DataContext = model };
        var host = new Border { Width = width, Height = 820, Child = page, Background = (Brush)test.Design.Dictionary["Kyf.Brush.Background"] };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var window = new Window { Content = new Canvas { Children = { host } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            model.OnNavigatedTo();
            UiThread.Until(() => model.Rows.Contains(steps));
            TestShell.Flush();
            var card = Descendants(page).OfType<Border>().Single(border => border.Name == "FirstStepsCard");
            var place = new Rect(card.TranslatePoint(default, page), card.RenderSize);
            Assert.IsTrue(place.Left >= 0 && place.Right <= width + 0.5, $"la carte tient dans la fenêtre : de {place.Left:0} à {place.Right:0} sur {width}");
            Assert.AreEqual("Premiers pas, 1 sur 3", AutomationProperties.GetName(card));
            var texts = Descendants(card).OfType<TextBlock>().Where(text => text.IsVisible).ToList();
            Assert.IsTrue(texts.All(text => text.TranslatePoint(new Point(text.ActualWidth, 0), page).X <= width + 0.5), "aucun texte ne dépasse");
            Assert.IsTrue(texts.Any(text => text.Text == "1 sur 3"));
            var links = Descendants(card).OfType<Button>().Where(button => button.IsVisible).ToList();
            Assert.HasCount(2, links, "le premier pas est fait : « Nouvelle fiche » n'y est plus, restent le guide et « Masquer »");
            Assert.IsTrue(links.All(link => link.ActualHeight >= 44));
            // Le pas fait est coché, le suivant est désigné.
            var dots = Descendants(card).OfType<Border>().Where(border => border.Name == "Dot").ToList();
            Assert.HasCount(3, dots);
            Assert.AreSame(page.FindResource("Kyf.Brush.Primary"), dots[0].Background);
            Assert.AreSame(page.FindResource("Kyf.Brush.NavigationActiveBackground"), dots[1].Background);
            Assert.IsNull(dots[2].Background);
            host.Capture($"premiers-pas-{width}-{(dark ? "sombre" : "clair")}");
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
