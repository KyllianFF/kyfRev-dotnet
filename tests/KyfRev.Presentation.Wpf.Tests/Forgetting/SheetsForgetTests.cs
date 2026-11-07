using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Fiches » oublie ce qu'elle a lu du coffre quand il se verrouille, et le relit à sa prochaine ouverture.</summary>
[TestClass]
public sealed class SheetsForgetTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Forget_drops_the_sheets_the_preview_what_was_typed_and_the_pending_action()
    {
        var time = new FakeTime(Now);
        using var vaults = new TestVaults(AppMode.Development, time);
        var mitosis = Mitosis(vaults);
        var page = Page(vaults, time);
        page.OnNavigatedTo();
        await page.RefreshCommand.ExecutionTask!;
        await page.LoadPreviewCommand.ExecutionTask!;
        Assert.AreEqual("Mitose", page.Preview!.Title);

        // La page garde ce qu'elle a lu : un dossier filtré, une recherche, une fiche à créer, une fiche à supprimer.
        var folder = page.Bar.Filters[0];
        var state = page.Bar.Filters[1];
        folder.Selected = folder.Choices.Single(choice => choice.Label == "Biologie");
        state.Selected = state.Choices[1];
        page.Bar.Search = "mito";
        await page.LoadPreviewCommand.ExecutionTask!;
        await page.RequestCreateCommand.ExecuteAsync(null);
        page.NewTitle = "Méiose";
        CollectionAssert.Contains(page.NewFolder.Choices.Select(choice => choice.Label).ToList(), "Biologie");
        page.RequestTrash(page.Rows.OfType<SheetRow>().Single());
        page.PageWidth = SheetsPageViewModel.PreviewMinimum;
        Assert.IsTrue(page.IsCreating);
        Assert.IsTrue(page.IsActing);
        StringAssert.Contains(page.ActionMessage, "Mitose", StringComparison.Ordinal);

        page.Forget();

        // Rien de ce qui a été lu ne reste : ni ligne, ni aperçu, ni compte, ni dossier, ni fenêtre, ni message.
        Assert.IsEmpty(page.Rows);
        Assert.IsFalse(page.HasRows);
        Assert.IsNull(page.SelectedRow);
        Assert.IsNull(page.Preview);
        Assert.IsFalse(page.ShowsPreview);
        Assert.AreEqual(string.Empty, page.Summary);
        Assert.IsFalse(page.HasSheets);
        Assert.IsFalse(page.IsEmpty, "Le coffre n'a pas été relu : on ne dit pas qu'il est vide.");
        Assert.IsFalse(page.HasNoMatch);
        Assert.IsFalse(page.IsLoading);
        Assert.IsNull(page.Error);
        Assert.AreEqual(string.Empty, page.Bar.Search);
        Assert.HasCount(1, folder.Choices);
        Assert.AreEqual("Tous", folder.Selected.Label);
        Assert.IsFalse(page.IsCreating);
        Assert.AreEqual(string.Empty, page.NewTitle);
        Assert.AreEqual(string.Empty, page.NewFolderName);
        Assert.HasCount(1, page.NewFolder.Choices);
        Assert.HasCount(1, page.NewTemplate.Choices);
        Assert.IsNull(page.CreateError);
        Assert.IsFalse(page.IsActing);
        Assert.AreEqual(string.Empty, page.ActionTitle);
        Assert.IsNull(page.ActionMessage);
        Assert.IsNull(page.ActionInputLabel);
        Assert.AreEqual(string.Empty, page.ActionInput);
        Assert.IsEmpty(page.ActionDestinations);
        Assert.IsFalse(page.CanErase);
        Assert.IsNull(page.ActionError);
        Assert.IsNull(page.Done);
        Assert.IsNull(page.DropHint);

        // L'état choisi et la largeur sont des réglages de la page, pas du coffre : ils restent.
        Assert.AreEqual(state.Choices[1], state.Selected);
        Assert.AreEqual(SheetsPageViewModel.PreviewMinimum, page.PageWidth);

        // La suppression oubliée ne met rien à la corbeille.
        await page.AnswerActionCommand.ExecuteAsync(true);
        Assert.IsEmpty(vaults.App.Trash.TrashedSheets().Value);

        // La prochaine ouverture relit le coffre.
        page.OnNavigatedTo();
        await page.RefreshCommand.ExecutionTask!;
        await page.LoadPreviewCommand.ExecutionTask!;
        Assert.AreEqual(mitosis, ((SheetRow)page.SelectedRow!).Sheet.Id);
        Assert.AreEqual("Mitose", page.Preview!.Title);
        Assert.IsTrue(page.HasSheets);
        Assert.AreEqual("1 fiche · 1 en révision · 0 archivée", page.Summary);
        CollectionAssert.Contains(folder.Choices.Select(choice => choice.Label).ToList(), "Biologie");
    }

    [STATestMethod]
    public void A_read_or_an_action_started_before_forget_puts_nothing_back()
    {
        using var ui = new UiThread();
        var time = new FakeTime(Now);
        using var vaults = new TestVaults(AppMode.Development, time);
        var mitosis = Mitosis(vaults);
        var page = Page(vaults, time);

        // La lecture des fiches revient après l'oubli : elle n'y remet rien.
        page.OnNavigatedTo();
        var listing = page.RefreshCommand.ExecutionTask!;
        page.Forget();
        UiThread.Until(() => listing.IsCompleted);
        Assert.IsEmpty(page.Rows);
        Assert.AreEqual(string.Empty, page.Summary);
        Assert.IsFalse(page.IsLoading);

        // L'aperçu de la fiche choisie non plus.
        page.OnNavigatedTo();
        UiThread.Until(() => page.HasRows);
        var previewing = page.LoadPreviewCommand.ExecutionTask!;
        page.Forget();
        UiThread.Until(() => previewing.IsCompleted);
        Assert.IsNull(page.Preview);

        // Ni les dossiers et les modèles de la fenêtre « Nouvelle fiche ».
        page.RequestCreateCommand.Execute(null);
        var asking = page.RequestCreateCommand.ExecutionTask!;
        page.Forget();
        UiThread.Until(() => asking.IsCompleted);
        Assert.IsFalse(page.IsCreating);
        Assert.HasCount(1, page.NewFolder.Choices);

        // Une action confirmée a lieu, mais ni sa réponse ni la relecture qui la suit ne reviennent.
        page.OnNavigatedTo();
        UiThread.Until(() => page.Preview is not null);
        page.RequestRename(page.Rows.OfType<SheetRow>().Single());
        page.ActionInput = "Mitose et méiose";
        page.AnswerActionCommand.Execute(true);
        var answering = page.AnswerActionCommand.ExecutionTask!;
        page.Forget();
        UiThread.Until(() => answering.IsCompleted);
        Assert.IsNull(page.Done);
        Assert.IsEmpty(page.Rows);
        Assert.AreEqual("Mitose et méiose", vaults.App.Sheets.Open(mitosis).Value.Title);

        // La prochaine ouverture relit le coffre.
        page.OnNavigatedTo();
        UiThread.Until(() => page.Preview is not null);
        Assert.AreEqual("Mitose et méiose", page.Preview!.Title);
    }

    /// <summary>Un coffre avec une fiche « Mitose » dans « Biologie » ; rend son identifiant.</summary>
    private static string Mitosis(TestVaults vaults)
    {
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        return vaults.App.Sheets.Create("Biologie", "Mitose", null).Value;
    }

    private static SheetsPageViewModel Page(TestVaults vaults, TimeProvider time) =>
        new(vaults.App.Sheets, vaults.App.Folders, vaults.App.Templates, new FakeOpener(), time, vaults.App.Reviews, vaults.App.Trash);
}
