using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>L'étalement des échéances depuis « Aujourd'hui » : un retard à rattraper, une pause à prévoir.</summary>
[TestClass]
public sealed class SpreadTests
{
    [TestMethod]
    public async Task Catching_up_shows_its_plan_then_postpones_what_does_not_fit()
    {
        using var vault = new LateVault(sheets: 5, daysAway: 8);
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var spread = page.Spread!;
        Assert.IsTrue(page.CanSpread);
        Assert.AreEqual(5, page.Dashboard!.Overdue.Length);

        await spread.OpenCommand.ExecuteAsync(null);

        Assert.IsTrue(spread.IsOpen);
        Assert.IsTrue(spread.IsCatchUp);
        Assert.AreEqual("10", spread.PerDay);
        Assert.AreEqual("Rien à étaler : ce qui est dû aujourd'hui (5) tient dans ce plafond.", spread.Plan);

        spread.PerDay = "2";
        await spread.ShowPlanAsync();
        Assert.AreEqual("2 révisions restent aujourd'hui ; 3 échéances partent sur les jours suivants, jusqu'au dimanche 11 octobre.", spread.Plan);

        spread.PerDay = "zéro";
        await spread.ShowPlanAsync();
        Assert.AreEqual(SpreadText.PerDayInvalid, spread.Plan);

        spread.PerDay = "2";
        await spread.AnswerCommand.ExecuteAsync(true);

        Assert.IsFalse(spread.IsOpen);
        Assert.AreEqual("3 échéances reportées, jusqu'au dimanche 11 octobre.", spread.Message);
        Assert.IsFalse(spread.HasError);
        Assert.AreEqual(2, page.Dashboard!.Overdue.Length, "le tableau de bord est relu");
        Assert.IsTrue(page.Dashboard.Upcoming.All(item => item.Step == "J1"), "rien ne recommence, rien n'avance");
    }

    [TestMethod]
    public async Task Closing_the_window_changes_nothing()
    {
        using var vault = new LateVault(sheets: 3, daysAway: 4);
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var spread = page.Spread!;
        await spread.OpenCommand.ExecuteAsync(null);
        spread.PerDay = "1";

        await spread.AnswerCommand.ExecuteAsync(false);

        Assert.IsFalse(spread.IsOpen);
        Assert.IsFalse(spread.HasMessage);
        Assert.AreEqual(3, vault.App.Reminders.Dashboard().Value.Overdue.Length);
    }

    [TestMethod]
    public async Task A_pause_moves_what_falls_in_it_after_the_return()
    {
        // Créées le 1er, dues le 2 : la veille, rien n'est en retard.
        using var vault = new LateVault(sheets: 3, daysAway: 0);
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var spread = page.Spread!;

        await spread.OpenCommand.ExecuteAsync(null);
        Assert.AreEqual(new DateOnly(2026, 10, 2), spread.Earliest);
        Assert.AreEqual((new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 8)), (spread.PauseFrom!.Value, spread.PauseTo!.Value));
        Assert.AreEqual("Rien n'est dû aujourd'hui : il n'y a rien à étaler.", spread.Plan);

        spread.IsPause = true;
        spread.PauseTo = new DateOnly(2026, 10, 4);
        spread.PerDay = "2";
        await spread.ShowPlanAsync();
        Assert.AreEqual("3 échéances partent après votre retour, jusqu'au mardi 6 octobre.", spread.Plan);

        spread.PauseTo = null;
        await spread.ShowPlanAsync();
        Assert.AreEqual(SpreadText.DaysRequired, spread.Plan);

        spread.PauseTo = new DateOnly(2026, 10, 4);
        await spread.AnswerCommand.ExecuteAsync(true);

        Assert.AreEqual("3 échéances reportées, jusqu'au mardi 6 octobre.", spread.Message);
        var due = vault.App.Reminders.Dashboard().Value.Upcoming.Select(item => item.DueDate.Day).Order().ToList();
        Assert.AreEqual("5,5,6", string.Join(',', due));
    }

    [TestMethod]
    public async Task Without_its_use_cases_spreading_is_not_offered_and_forgetting_closes_it()
    {
        using var vault = new LateVault(sheets: 1, daysAway: 3);
        var bare = new DashboardPageViewModel(vault.App.Reminders, vault.App.Sheets, new FakeOpener());
        Assert.IsNull(bare.Spread);
        Assert.IsFalse(bare.CanSpread);

        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        await page.Spread!.OpenCommand.ExecuteAsync(null);
        page.Forget();

        Assert.IsFalse(page.Spread.IsOpen);
        Assert.AreEqual(string.Empty, page.Spread.Plan);
    }

    /// <summary>Un coffre d'essai : des fiches créées le jeudi 1er octobre 2026, et l'horloge avancée de quelques jours.</summary>
    private sealed class LateVault : IDisposable
    {
        private readonly TestVaults _vaults;

        public LateVault(int sheets, int daysAway)
        {
            Time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
            _vaults = new TestVaults(AppMode.Development, Time);
            Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Coffre")).Vault);
            for (var rank = 1; rank <= sheets; rank++)
            {
                Assert.IsTrue(App.Sheets.Create("Biologie", $"Fiche {rank}", null).Succeeded);
            }
            Time.AdvanceDays(daysAway);
        }

        public FakeTime Time { get; }

        public KyfRev.Application.UseCases.KyfRevApplication App => _vaults.App;

        public DashboardPageViewModel Page() => new(App.Reminders, App.Sheets, new FakeOpener(), null, App.Reviews, App.Exams, App.Folders, Time);

        public void Dispose() => _vaults.Dispose();
    }
}
