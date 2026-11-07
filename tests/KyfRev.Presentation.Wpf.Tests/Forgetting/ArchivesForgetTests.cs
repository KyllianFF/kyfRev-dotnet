using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Archives » oublie ce qu'elle a lu du coffre quand il se verrouille, et le relit à sa prochaine ouverture.</summary>
[TestClass]
public sealed class ArchivesForgetTests
{
    [TestMethod]
    public async Task Forget_drops_the_archives_the_typed_search_and_the_pending_restart()
    {
        var time = Time();
        using var vaults = new TestVaults(AppMode.Development, time);
        var sheet = ArchivedSheet(vaults);
        var page = Page(vaults, time);
        page.OnNavigatedTo();
        await page.RefreshCommand.ExecutionTask!;
        await page.ToggleRemindersCommand.ExecuteAsync(page.Rows.Single());
        var folder = page.Bar.Filters[0];
        folder.Selected = folder.Choices.Single(choice => choice.Label == "Biologie");
        var sort = page.Bar.Filters[4];
        sort.Selected = sort.Choices[1];
        page.Bar.Search = "mito";
        page.PageWidth = ArchivesPageViewModel.RoomMinimum - 1;
        page.RequestRestartCommand.Execute(page.Rows.Single());
        Assert.AreEqual("« Mitose » est exclue des rappels de consolidation.", page.Message);
        Assert.IsTrue(page.IsConfirming);

        page.Forget();

        // Rien de ce qui a été lu ne reste : ni ligne, ni compte, ni message, ni dossier, ni confirmation.
        Assert.IsEmpty(page.Rows);
        Assert.IsFalse(page.HasRows);
        Assert.IsFalse(page.HasArchives);
        Assert.IsFalse(page.IsEmpty, "Le coffre n'a pas été relu : on ne dit pas qu'il est vide.");
        Assert.IsFalse(page.HasNoMatch);
        Assert.IsFalse(page.IsLoading);
        Assert.AreEqual(string.Empty, page.Summary);
        Assert.IsNull(page.Error);
        Assert.IsNull(page.Message);
        Assert.IsFalse(page.IsConfirming);
        Assert.AreEqual(string.Empty, page.ConfirmMessage);
        Assert.AreEqual(string.Empty, page.Bar.Search);
        Assert.HasCount(1, folder.Choices);
        Assert.AreEqual("Tous", folder.Selected.Label);

        // Le tri et la largeur sont des réglages de la page, pas du coffre : ils restent.
        Assert.AreSame(sort.Choices[1], sort.Selected);
        Assert.IsFalse(page.HasRoom);

        // La confirmation oubliée ne remet rien en révision.
        await page.AnswerRestartCommand.ExecuteAsync(true);
        Assert.AreEqual(ReviewStatus.Archived, vaults.App.Sheets.Open(sheet).Value.Review.Status);
        Assert.IsNull(page.Message);

        // La prochaine ouverture relit le coffre.
        page.OnNavigatedTo();
        await page.RefreshCommand.ExecutionTask!;
        Assert.AreEqual("Mitose", page.Rows.Single().Title);
        Assert.IsTrue(page.HasArchives);
        Assert.Contains(choice => choice.Label == "Biologie", folder.Choices);
        StringAssert.StartsWith(page.Summary, "1 fiche archivée", StringComparison.Ordinal);
    }

    [STATestMethod]
    public void A_reading_started_before_Forget_puts_nothing_back()
    {
        using var ui = new UiThread();
        var time = Time();
        using var vaults = new TestVaults(AppMode.Development, time);
        ArchivedSheet(vaults);
        var page = Page(vaults, time);

        // La lecture part ; sa suite attend le fil de l'interface, qui oublie le coffre avant de la reprendre.
        page.OnNavigatedTo();
        Assert.IsTrue(page.RefreshCommand.IsRunning);
        page.Forget();
        UiThread.Until(() => !page.RefreshCommand.IsRunning);

        Assert.IsEmpty(page.Rows);
        Assert.IsFalse(page.HasArchives);
        Assert.IsFalse(page.IsLoading);
        Assert.AreEqual(string.Empty, page.Summary);
        Assert.HasCount(1, page.Bar.Filters[0].Choices);

        page.OnNavigatedTo();
        UiThread.Until(() => !page.RefreshCommand.IsRunning);
        Assert.AreEqual("Mitose", page.Rows.Single().Title);
    }

    [STATestMethod]
    public void An_action_finished_after_Forget_names_no_sheet()
    {
        using var ui = new UiThread();
        var time = Time();
        using var vaults = new TestVaults(AppMode.Development, time);
        ArchivedSheet(vaults);
        var page = Page(vaults, time);
        page.OnNavigatedTo();
        UiThread.Until(() => !page.RefreshCommand.IsRunning);

        // L'exclusion part ; le coffre est oublié avant que son message ne revienne.
        page.ToggleRemindersCommand.Execute(page.Rows.Single());
        Assert.IsTrue(page.ToggleRemindersCommand.IsRunning);
        page.Forget();
        UiThread.Until(() => !page.ToggleRemindersCommand.IsRunning);

        Assert.IsNull(page.Message);
        Assert.IsNull(page.Error);
        Assert.IsEmpty(page.Rows);
        Assert.AreEqual(string.Empty, page.Summary);

        // L'action, elle, a eu lieu dans le coffre : la prochaine lecture la montre.
        page.OnNavigatedTo();
        UiThread.Until(() => !page.RefreshCommand.IsRunning);
        Assert.IsTrue(page.Rows.Single().IsExcluded);
    }

    private static FakeTime Time() => new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));

    private static string ArchivedSheet(TestVaults vaults)
    {
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        var sheet = vaults.App.Sheets.Create("Biologie", "Mitose", null).Value;
        Assert.IsTrue(vaults.App.Reviews.Archive(sheet).Succeeded);
        return sheet;
    }

    private static ArchivesPageViewModel Page(TestVaults vaults, TimeProvider time) =>
        new(vaults.App.Reminders, vaults.App.Reviews, new FakeOpener(), time);
}
