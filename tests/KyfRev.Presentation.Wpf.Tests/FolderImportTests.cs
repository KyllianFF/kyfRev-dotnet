using System.IO;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La fenêtre « Importer un dossier de notes » : le choix, le bilan, et ce qu'elle tait une fois le coffre verrouillé.</summary>
[TestClass]
public sealed class FolderImportTests
{
    [STATestMethod]
    public void The_window_starts_with_nothing_chosen_and_copies_only_by_default()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;

        import.Request();

        Assert.IsTrue(import.IsOpen);
        Assert.IsFalse(import.HasSource);
        Assert.AreEqual(SharingText.ImportChoose, import.ChooseLabel);
        Assert.AreEqual(string.Empty, import.SourceName);
        Assert.IsTrue(import.CopiesOnly);
        Assert.IsFalse(import.Tracks);
        Assert.IsFalse(import.CanConfirm, "il faut un dossier");
        Assert.AreEqual(SharingText.ImportConfirm, import.ConfirmLabel);
        Assert.AreEqual(SharingText.Cancel, import.CancelLabel);
        Assert.IsTrue(import.IsChoosing);
        Assert.IsFalse(import.HasReport);
        Assert.AreEqual(string.Empty, import.Summary);
        Assert.AreEqual(string.Empty, import.Follow);
    }

    [STATestMethod]
    public void Giving_up_on_the_folder_keeps_the_one_chosen_before()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;
        import.Request();

        scene.Picker.Answers.Enqueue(null);
        import.ChooseCommand.Execute(null);
        Assert.IsFalse(import.HasSource);

        scene.Picker.Answers.Enqueue(scene.Source);
        import.ChooseCommand.Execute(null);
        scene.Picker.Answers.Enqueue(null);
        import.ChooseCommand.Execute(null);

        Assert.AreEqual(scene.Source, import.Source);
        Assert.AreEqual("Mes notes", import.SourceName);
        Assert.AreEqual(SharingText.ImportChange, import.ChooseLabel);
        Assert.IsTrue(import.CanConfirm);
        Assert.AreEqual(SharingText.ImportPick, scene.Picker.Titles[0]);
    }

    [STATestMethod]
    public void A_vault_locked_while_the_folder_is_asked_keeps_no_folder()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        FolderImportViewModel? import = null;
        import = new FolderImportViewModel(scene.App.Sheets, new CallingPicker(() =>
        {
            import!.Forget();
            return scene.Source;
        }));
        import.Request();

        import.ChooseCommand.Execute(null);

        Assert.IsNull(import.Source);
    }

    [STATestMethod]
    public void A_drive_keeps_its_whole_name()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        scene.Import.Request();
        scene.Picker.Answers.Enqueue(@"D:\");

        scene.Import.ChooseCommand.Execute(null);

        Assert.AreEqual(@"D:\", scene.Import.SourceName);
    }

    [STATestMethod]
    public void Confirming_without_a_folder_stays_put()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        scene.Import.Request();

        scene.Answer(true);

        Assert.IsTrue(scene.Import.IsOpen);
        Assert.IsFalse(scene.Import.HasReport);
        Assert.IsEmpty(scene.Reports);
    }

    [STATestMethod]
    public void Copied_only_the_report_says_what_came_and_how_to_follow_it()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;
        scene.Fill();
        import.Request();
        scene.Choose();

        scene.Answer(true);

        Assert.IsTrue(import.IsOpen, "le bilan se lit dans la fenêtre");
        Assert.IsTrue(import.HasReport);
        Assert.IsFalse(import.IsChoosing);
        Assert.IsFalse(import.IsBusy);
        Assert.AreEqual("2 fiches importées dans « Mes notes ». 2 liens [[…]] convertis. 1 image ou pièce jointe copiée.", import.Summary);
        Assert.AreEqual(SharingText.ReportFollow, import.Follow);
        Assert.IsTrue(import.HasFollow);
        Assert.IsTrue(import.HasIgnored);
        Assert.AreEqual("1 élément non repris", import.IgnoredLabel);
        StringAssert.Contains(import.Ignored.Single(), "UTF-8", StringComparison.Ordinal);
        Assert.AreEqual(SharingText.Close, import.ConfirmLabel);
        Assert.AreEqual(string.Empty, import.CancelLabel, "plus rien à annuler");
        Assert.IsTrue(import.CanConfirm);
        Assert.AreEqual("Mes notes", scene.Reports.Single().Folder);
        Assert.HasCount(2, Directory.GetFiles(Path.Combine(scene.Root, "fiches", "Mes notes"), "*.md"));

        scene.Answer(true);
        Assert.IsFalse(import.IsOpen, "« Fermer » ferme");
    }

    [STATestMethod]
    public void Entering_review_is_said_and_nothing_is_left_to_follow()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;
        File.WriteAllText(Path.Combine(scene.Source, "Note.md"), "Une note.\n");
        File.WriteAllText(Path.Combine(scene.Source, "Autre.md"), "[[Note]] et [[Note|encore]]\n");
        import.Request();
        scene.Choose();
        import.Tracks = true;
        Assert.IsFalse(import.CopiesOnly);

        scene.Answer(true);

        Assert.AreEqual("2 fiches importées dans « Mes notes », dont 2 en révision (J1 demain). 2 liens [[…]] convertis.", import.Summary);
        Assert.IsFalse(import.HasFollow);
        Assert.IsFalse(import.HasIgnored);

        import.CopiesOnly = true;
        Assert.IsFalse(import.Tracks);
    }

    [STATestMethod]
    public void The_report_agrees_with_its_numbers()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;
        File.WriteAllText(Path.Combine(scene.Source, "Note.md"), "[[Autre]]\n\n![[carte.png]]\n\n![[plan.png]]\n");
        File.WriteAllText(Path.Combine(scene.Source, "Autre.md"), "Rien.\n");
        File.WriteAllBytes(Path.Combine(scene.Source, "carte.png"), [137, 80, 78, 71]);
        File.WriteAllBytes(Path.Combine(scene.Source, "plan.png"), [137, 80, 78, 71]);
        File.WriteAllBytes(Path.Combine(scene.Source, "Abimée.md"), [(byte)'a', 0xFF, 0xFE]);
        File.WriteAllBytes(Path.Combine(scene.Source, "Abimée aussi.md"), [(byte)'b', 0xFF, 0xFE]);
        import.Request();
        scene.Choose();

        scene.Answer(true);

        StringAssert.Contains(import.Summary, "2 images ou pièces jointes copiées.", StringComparison.Ordinal);
        Assert.AreEqual("2 éléments non repris", import.IgnoredLabel);

        File.WriteAllText(Path.Combine(scene.Source, "Seule.md"), "[[Autre]]\n");
        foreach (var other in new[] { "Note.md", "Autre.md", "Abimée.md", "Abimée aussi.md" })
        {
            File.Delete(Path.Combine(scene.Source, other));
        }
        import.Request();
        scene.Choose();
        scene.Answer(true);

        StringAssert.Contains(import.Summary, "1 fiche importée dans", StringComparison.Ordinal);
        Assert.AreEqual(1, import.Report!.Sheets);
    }

    [STATestMethod]
    public void A_refused_import_says_why_and_stays_open()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;
        import.Request();
        scene.Choose();

        scene.Answer(true);

        Assert.IsTrue(import.IsOpen);
        Assert.IsTrue(import.HasError);
        Assert.IsFalse(import.HasReport);
        Assert.IsTrue(import.IsIdle);
        Assert.IsEmpty(scene.Reports);

        scene.Picker.Answers.Enqueue(scene.Source);
        import.ChooseCommand.Execute(null);
        Assert.IsFalse(import.HasError, "choisir un autre dossier efface le refus");
    }

    [STATestMethod]
    public void Closing_during_the_import_lets_it_finish_and_reopening_shows_it()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;
        scene.Fill();
        import.Request();
        scene.Choose();

        var running = import.AnswerCommand.ExecuteAsync(true);
        Assert.IsTrue(import.IsBusy);
        Assert.IsFalse(import.IsIdle);
        Assert.AreEqual(SharingText.ImportBusy, import.ConfirmLabel);
        Assert.IsFalse(import.CanConfirm);
        Assert.AreEqual(SharingText.Close, import.CancelLabel, "l'import ne s'annule pas : le bouton ferme seulement la fenêtre");

        import.AnswerCommand.Execute(false);
        Assert.IsFalse(import.IsOpen);

        import.Request();
        Assert.IsTrue(import.IsOpen);
        Assert.IsTrue(import.IsBusy, "il ne repart pas à vide");
        Assert.AreEqual(scene.Source, import.Source);

        import.AnswerCommand.Execute(true);
        UiThread.Until(() => running.IsCompleted);
        Assert.HasCount(1, scene.Reports, "un seul import");
        Assert.IsTrue(import.HasReport);
    }

    [STATestMethod]
    public void A_report_that_ends_with_the_window_closed_waits_for_the_next_opening_only()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;
        scene.Fill();
        import.Request();
        scene.Choose();
        using var hold = new VaultHold(scene.App);
        var running = import.AnswerCommand.ExecuteAsync(true);
        import.AnswerCommand.Execute(false);

        hold.Release();
        UiThread.Until(() => running.IsCompleted);
        Assert.IsFalse(import.IsOpen);
        Assert.IsNull(import.UnreadRefusal, "un bilan n'est pas un refus");

        import.Request();
        Assert.IsTrue(import.IsOpen);
        Assert.IsTrue(import.HasReport, "le bilan se lit encore");
        StringAssert.Contains(import.Summary, "1 image ou pièce jointe copiée.", StringComparison.Ordinal);
        Assert.IsTrue(import.HasIgnored);

        import.AnswerCommand.Execute(true);
        import.Request();
        Assert.IsFalse(import.HasReport, "lu une fois, il n'est plus montré");
        Assert.IsFalse(import.HasSource);
    }

    [STATestMethod]
    public void A_refusal_that_ends_with_the_window_closed_is_said_by_the_list_and_kept_by_the_window()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var page = new SheetsPageViewModel(scene.App.Sheets, scene.App.Folders, scene.App.Templates, new FakeOpener(), TimeProvider.System, scene.App.Reviews, scene.App.Trash, null, scene.Import);
        var import = scene.Import;
        page.RequestImportCommand.Execute(null);
        scene.Choose();
        using var hold = new VaultHold(scene.App);
        var running = import.AnswerCommand.ExecuteAsync(true);
        import.Dismiss();

        hold.Release();
        UiThread.Until(() => running.IsCompleted);
        Assert.IsFalse(import.IsOpen);
        Assert.IsTrue(import.HasError);
        StringAssert.StartsWith(page.Error, SharingText.ImportRefusedOf(string.Empty), StringComparison.Ordinal);
        StringAssert.EndsWith(page.Error, import.Error, StringComparison.Ordinal);

        page.Show([]);
        Assert.IsTrue(page.HasError, "relire la liste ne le tait pas");

        page.RequestImportCommand.Execute(null);
        Assert.IsTrue(import.IsOpen);
        Assert.IsTrue(import.HasError, "le refus se lit dans la fenêtre");
        Assert.AreEqual(scene.Source, import.Source);
        Assert.IsNull(import.UnreadRefusal);
        Assert.IsFalse(page.HasError, "il n'est plus à dire deux fois");

        import.AnswerCommand.Execute(false);
        import.Request();
        Assert.IsFalse(import.HasError, "lu une fois, il n'est plus montré");
    }

    [STATestMethod]
    public void A_refusal_seen_in_the_open_window_stays_out_of_the_list()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var page = new SheetsPageViewModel(scene.App.Sheets, scene.App.Folders, scene.App.Templates, new FakeOpener(), TimeProvider.System, scene.App.Reviews, scene.App.Trash, null, scene.Import);
        page.RequestImportCommand.Execute(null);
        scene.Choose();

        scene.Answer(true);

        Assert.IsTrue(scene.Import.HasError);
        Assert.IsFalse(page.HasError);
    }

    [STATestMethod]
    public void Forgetting_the_vault_drops_what_was_waiting_to_be_read()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;
        import.Request();
        scene.Choose();
        using var hold = new VaultHold(scene.App);
        var running = import.AnswerCommand.ExecuteAsync(true);
        import.Dismiss();
        hold.Release();
        UiThread.Until(() => running.IsCompleted);
        Assert.IsNotNull(import.UnreadRefusal);

        import.Forget();

        Assert.IsNull(import.UnreadRefusal);
        import.Request();
        Assert.IsFalse(import.HasError);
        Assert.IsFalse(import.HasSource);
    }

    [STATestMethod]
    public void A_new_request_after_the_report_starts_empty()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;
        scene.Fill();
        import.Request();
        scene.Choose();
        import.Tracks = true;
        scene.Answer(true);

        import.Request();

        Assert.IsFalse(import.HasReport);
        Assert.IsFalse(import.HasSource);
        Assert.IsFalse(import.Tracks);
    }

    [STATestMethod]
    public void Leaving_the_page_closes_the_window_and_the_import_goes_on()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;
        scene.Fill();
        import.Request();
        scene.Choose();
        var running = import.AnswerCommand.ExecuteAsync(true);

        import.Dismiss();

        Assert.IsFalse(import.IsOpen);
        UiThread.Until(() => running.IsCompleted);
        Assert.HasCount(1, scene.Reports);
    }

    [STATestMethod]
    public void Forgetting_the_vault_drops_the_report_and_what_was_chosen()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;
        scene.Fill();
        import.Request();
        scene.Choose();
        scene.Answer(true);
        Assert.IsTrue(import.HasReport);

        import.Forget();

        Assert.IsFalse(import.IsOpen);
        Assert.IsNull(import.Source);
        Assert.IsNull(import.Report);
        Assert.IsEmpty(import.Ignored);
        Assert.IsFalse(import.Tracks);
    }

    [STATestMethod]
    public void An_import_finished_after_the_vault_was_forgotten_shows_nothing()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        var import = scene.Import;
        scene.Fill();
        import.Request();
        scene.Choose();

        var running = import.AnswerCommand.ExecuteAsync(true);
        import.Forget();
        UiThread.Until(() => running.IsCompleted);
        TestShell.Flush();

        Assert.IsFalse(import.HasReport);
        Assert.IsFalse(import.IsBusy);
        Assert.IsFalse(import.IsOpen);
        Assert.IsEmpty(scene.Reports);
    }

    [STATestMethod]
    public void The_list_is_read_again_and_says_where_the_notes_went()
    {
        using var ui = new UiThread();
        using var scene = new ImportScene();
        scene.Fill();
        var page = new SheetsPageViewModel(scene.App.Sheets, scene.App.Folders, scene.App.Templates, new FakeOpener(), TimeProvider.System, scene.App.Reviews, scene.App.Trash, null, scene.Import);
        Assert.IsTrue(page.CanImport);
        page.RequestImportCommand.Execute(null);
        scene.Choose();
        scene.Import.Tracks = true;

        scene.Answer(true);
        UiThread.Until(() => page.Rows.OfType<SheetRow>().Count() == 2);

        Assert.AreEqual(SharingText.ImportedOf("Mes notes"), page.Done);
        Assert.IsFalse(new SheetsPageViewModel(scene.App.Sheets, scene.App.Folders, scene.App.Templates, new FakeOpener(), TimeProvider.System).CanImport);
    }

    [TestMethod]
    public void The_window_needs_all_it_works_with()
    {
        using var scene = new ImportScene();
        Assert.ThrowsExactly<ArgumentNullException>(() => new FolderImportViewModel(null!, scene.Picker));
        Assert.ThrowsExactly<ArgumentNullException>(() => new FolderImportViewModel(scene.App.Sheets, null!));
    }

    /// <summary>Un coffre en clair, un dossier de notes à côté, et la fenêtre dessus.</summary>
    private sealed class ImportScene : IDisposable
    {
        public ImportScene()
        {
            Vaults = new TestVaults(AppMode.Development);
            Root = Vaults.PathOf("Coffre");
            Assert.AreEqual(Root, App.Vaults.Create(Root).Vault);
            Source = Vaults.Folder("Mes notes");
            Import = new FolderImportViewModel(App.Sheets, Picker) { Imported = Reports.Add };
        }

        public TestVaults Vaults { get; }

        public KyfRevApplication App => Vaults.App;

        public FakePicker Picker => Vaults.Picker;

        public string Root { get; }

        public string Source { get; }

        public FolderImportViewModel Import { get; }

        public List<ImportReport> Reports { get; } = [];

        /// <summary>Deux notes, un lien, une image citée et une note illisible.</summary>
        public void Fill()
        {
            File.WriteAllText(Path.Combine(Source, "Note.md"), "Une note.\n");
            File.WriteAllText(Path.Combine(Source, "Autre.md"), "Voir [[Note]].\n\n![[carte.png]]\n");
            File.WriteAllBytes(Path.Combine(Source, "carte.png"), [137, 80, 78, 71]);
            File.WriteAllBytes(Path.Combine(Source, "Abimée.md"), [(byte)'a', 0xFF, 0xFE, (byte)'b']);
        }

        public void Choose()
        {
            Picker.Answers.Enqueue(Source);
            Import.ChooseCommand.Execute(null);
        }

        public void Answer(bool confirmed)
        {
            var running = Import.AnswerCommand.ExecuteAsync(confirmed);
            UiThread.Until(() => running.IsCompleted);
            running.GetAwaiter().GetResult();
        }

        public void Dispose() => Vaults.Dispose();
    }
}
