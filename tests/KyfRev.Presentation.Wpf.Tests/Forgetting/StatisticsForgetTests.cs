using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Statistiques » quand le coffre se verrouille : elle oublie ce qu'elle en a lu, et le relit ensuite.</summary>
[TestClass]
public sealed class StatisticsForgetTests
{
    private static readonly DateTimeOffset Morning = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Forgetting_drops_what_was_read_and_a_new_reading_brings_it_back()
    {
        var time = new FakeTime(Morning);
        using var vaults = new TestVaults(AppMode.Development, time);
        var page = Page(vaults, time);
        page.PageWidth = 800;
        page.CalendarWidth = 600;
        await page.RefreshCommand.ExecuteAsync(null);
        Loaded(page);

        page.Forget();

        Forgotten(page);
        // La largeur de la page ne vient pas du coffre : elle reste.
        Assert.AreEqual(800.0, page.PageWidth);
        Assert.AreEqual(600.0, page.CalendarWidth);

        await page.RefreshCommand.ExecuteAsync(null);
        Loaded(page);
    }

    [STATestMethod]
    public void A_reading_started_before_forgetting_puts_nothing_back()
    {
        using var ui = new UiThread();
        var time = new FakeTime(Morning);
        using var vaults = new TestVaults(AppMode.Development, time);
        var page = Page(vaults, time);

        // La suite de la lecture revient au fil de l'interface : elle n'y passe qu'une fois l'oubli fait.
        var reading = page.RefreshCommand.ExecuteAsync(null);
        page.Forget();
        UiThread.Until(() => reading.IsCompleted);

        Forgotten(page);

        page.OnNavigatedTo();
        UiThread.Until(() => page.IsLoaded);
        Loaded(page);
    }

    /// <summary>La page, sur un coffre ouvert où « Mitose », dans « Biologie », a été révisée le lendemain de sa création.</summary>
    private static StatisticsPageViewModel Page(TestVaults vaults, FakeTime time)
    {
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        var mitosis = vaults.App.Sheets.Create("Biologie", "Mitose", null).Value;
        time.AdvanceDays(1);
        Assert.IsTrue(vaults.App.Reviews.Validate(ItemKind.Sheet, mitosis).Succeeded);
        return new StatisticsPageViewModel(vaults.App.Reminders);
    }

    /// <summary>Les statistiques lues dans le coffre : une révision, le 2 octobre, dans « Biologie ».</summary>
    private static void Loaded(StatisticsPageViewModel page)
    {
        Assert.IsNull(page.Error);
        Assert.IsTrue(page.IsLoaded);
        Assert.IsFalse(page.IsLoading);
        StringAssert.EndsWith(page.Summary, "depuis le ven. 2 oct.", StringComparison.Ordinal);
        Assert.HasCount(4, page.Tiles);
        Assert.AreEqual("1", page.Tiles[1].Value);
        Assert.HasCount(53, page.Weeks);
        Assert.HasCount(53, page.MonthLabels);
        Assert.AreEqual("Calendrier d'activité : 1 jour actif sur 52 semaines", page.CalendarName);
        Assert.HasCount(1, page.Months);
        Assert.AreEqual("octobre 2026", page.Months[0].Label);
        Assert.HasCount(1, page.Folders);
        Assert.AreEqual("Biologie", page.Folders[0].Folder);
        Assert.AreEqual(1, page.Folders[0].Reviews);
    }

    /// <summary>Rien de ce qui a été lu dans le coffre ne reste : la page est comme à sa construction.</summary>
    private static void Forgotten(StatisticsPageViewModel page)
    {
        Assert.IsFalse(page.IsLoaded);
        Assert.IsFalse(page.IsLoading);
        Assert.IsNull(page.Error);
        Assert.IsFalse(page.HasError);
        Assert.AreEqual(StatisticsText.Summary, page.Summary);
        Assert.IsEmpty(page.Tiles);
        Assert.IsEmpty(page.Weeks);
        Assert.IsEmpty(page.MonthLabels);
        Assert.AreEqual(string.Empty, page.CalendarName);
        Assert.IsEmpty(page.Months);
        Assert.IsEmpty(page.Folders);
        Assert.IsTrue(page.HasNoMonth);
        Assert.IsTrue(page.HasNoFolder);
    }
}
