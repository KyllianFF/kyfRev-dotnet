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

    [TestMethod]
    public async Task A_folder_or_a_tag_restricts_the_list_and_the_session()
    {
        using var vault = new LateVault(sheets: 2, daysAway: 2);
        var history = vault.App.Sheets.Create("Histoire/Révolution", "1789", null).Value;
        Assert.IsTrue(vault.App.Sheets.SetTags(history, ["partiel"]).Succeeded);
        vault.Time.AdvanceDays(2);
        var opener = new FakeOpener();
        var page = new DashboardPageViewModel(vault.App.Reminders, vault.App.Sheets, opener, null, vault.App.Reviews, vault.App.Exams, vault.App.Folders, vault.Time);
        await page.RefreshCommand.ExecuteAsync(null);

        Assert.AreEqual(
            "Tous les dossiers | Dossier : Biologie | Dossier : Histoire | Dossier : Histoire › Révolution | Étiquette : partiel",
            string.Join(" | ", page.Scopes.Select(scope => scope.Label)));
        Assert.AreEqual("Commencer (3)", page.StartLabel);

        page.SelectedScope = page.Scopes[2];
        Assert.AreEqual("Commencer (1)", page.StartLabel);
        Assert.AreEqual("1789", page.Rows.OfType<DueItemRow>().Single().Item.Title);

        page.SelectedScope = page.Scopes[4];
        Assert.AreEqual("Commencer (1)", page.StartLabel);

        page.SelectedScope = page.Scopes[1];
        Assert.AreEqual("Commencer (2)", page.StartLabel);

        // Le choix tient quand le tableau se relit ; il revient à « Tout » si son dossier n'a plus rien à réviser.
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual("Dossier : Biologie", page.SelectedScope!.Label);
        page.SelectedScope = page.Scopes[2];
        Assert.IsTrue(vault.App.Reviews.Archive(history).Succeeded);
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual("Tous les dossiers", page.SelectedScope!.Label);
        Assert.AreEqual("Commencer (2)", page.StartLabel);
    }

    [TestMethod]
    public async Task The_written_filter_reads_a_kind_a_folder_a_tag_or_words()
    {
        using var vault = new LateVault(sheets: 2, daysAway: 2);
        var history = vault.App.Sheets.Create("Histoire/Révolution", "1789", null).Value;
        Assert.IsTrue(vault.App.Sheets.SetTags(history, ["partiel"]).Succeeded);
        vault.Time.AdvanceDays(2);
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual("Commencer (3)", page.StartLabel);

        // Un dossier par son nom, sans accents ni majuscules : la liste s'y restreint, la séance et la nouvelle fiche aussi.
        page.Query = "REVOLUTION";
        Assert.AreEqual("Dossier : Histoire › Révolution", page.SelectedScope!.Label);
        Assert.AreEqual("Commencer (1)", page.StartLabel);
        Assert.AreEqual("Elle sera créée dans « Histoire › Révolution », et entre en révision aujourd'hui (J0).", page.NewSheetPlace);
        page.Query = "histoire/revolution";
        Assert.AreEqual("Dossier : Histoire › Révolution", page.SelectedScope!.Label, "le chemin aussi");

        // Une étiquette, avec ou sans #.
        page.Query = "#Partiel";
        Assert.AreEqual("Étiquette : partiel", page.SelectedScope!.Label);
        Assert.AreEqual("Commencer (1)", page.StartLabel);

        // Le genre : aucun passage n'est dû, « Commencer » reste, grisé.
        page.Query = "passages";
        Assert.AreEqual(DashboardFilter.Passages, page.SelectedFilter.Value);
        Assert.AreEqual("Tous les dossiers", page.SelectedScope!.Label);
        Assert.IsFalse(page.StartCommand.CanExecute(null));
        Assert.AreEqual(SessionText.StartNothing, page.StartLabel);
        Assert.IsTrue(page.HasTiles, "les tuiles restent, à zéro : la journée a quelque chose");
        Assert.AreEqual(0, page.Tiles[0].Count);
        Assert.AreEqual(DashboardText.NothingMatches, page.Rows.OfType<EmptyDashboardRow>().Single().Description, "l'état vide dit le filtre, pas un coffre vide");

        // Le reste cherche dans les titres, les dossiers et les étiquettes ; « fiche » choisit le genre.
        page.Query = "fiche 178";
        Assert.AreEqual(DashboardFilter.Sheets, page.SelectedFilter.Value);
        Assert.AreEqual("1789", page.Rows.OfType<DueItemRow>().Single().Item.Title);
        page.Query = "bio 2";
        Assert.AreEqual("Fiche 2", page.Rows.OfType<DueItemRow>().Single().Item.Title);
        Assert.AreEqual("Tous les dossiers", page.SelectedScope!.Label, "des mots ne choisissent pas de dossier");
        Assert.AreEqual(DashboardText.NewSheetAtRoot, page.NewSheetPlace);

        page.Query = string.Empty;
        Assert.AreEqual(DashboardFilter.All, page.SelectedFilter.Value);
        Assert.AreEqual("Commencer (3)", page.StartLabel);

        // Oublier le coffre oublie le filtre : il peut nommer un dossier.
        page.Query = "histoire";
        page.Forget();
        Assert.AreEqual(string.Empty, page.Query);
    }

    [TestMethod]
    public async Task A_tile_keeps_only_its_own_and_a_second_click_gives_all_back()
    {
        using var vault = new LateVault(sheets: 2, daysAway: 2);
        Assert.IsTrue(vault.App.Sheets.Create("Chimie", "Atome", null).Succeeded);
        vault.Time.AdvanceDays(1);
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);

        var (overdue, today, reminders) = (page.Tiles[0], page.Tiles[1], page.Tiles[2]);
        Assert.AreEqual(DashboardText.Overdue, overdue.Title);
        Assert.AreEqual(2, overdue.Count);
        Assert.AreEqual(DashboardText.RemindersShort, reminders.Title);
        Assert.AreEqual(0, reminders.Count);
        Assert.AreEqual($"{DashboardText.Overdue} : 2", overdue.Spoken);
        Assert.AreEqual(1, today.Count, "l'atome, créé hier, est dû aujourd'hui (J1)");

        // La tuile ne garde que les retards : dans la liste, la séance et le résumé ; ni examens ni « à venir ».
        page.ChooseTileCommand.Execute(overdue);
        Assert.IsTrue(overdue.IsChosen);
        Assert.AreEqual("Commencer (2)", page.StartLabel);
        Assert.AreEqual(2, page.ReviewCount);
        CollectionAssert.AreEqual(new[] { DashboardText.Overdue }, page.Rows.OfType<DashboardGroup>().Select(group => group.Title).ToArray());

        // Une autre tuile prend la place ; la même, rechoisie, rend tout.
        page.ChooseTileCommand.Execute(today);
        Assert.IsFalse(overdue.IsChosen);
        Assert.AreEqual("Commencer (1)", page.StartLabel);
        page.ChooseTileCommand.Execute(today);
        Assert.IsFalse(today.IsChosen);
        Assert.AreEqual("Commencer (3)", page.StartLabel);

        // Une tuile vide ne se choisit pas ; une tuile choisie qui se vide se relâche.
        page.ChooseTileCommand.Execute(reminders);
        Assert.IsFalse(reminders.IsChosen);
        page.ChooseTileCommand.Execute(today);
        page.Query = "biologie";
        Assert.IsFalse(today.IsChosen);
        Assert.AreEqual("Commencer (2)", page.StartLabel);
    }

    [TestMethod]
    public async Task Each_action_sits_in_its_section_and_the_day_says_how_far_it_has_come()
    {
        using var vault = new LateVault(sheets: 2, daysAway: 2);
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);

        // Les examens se montrent sans examen, avec de quoi en fixer un ; « Étaler… » va aux retards.
        var groups = page.Rows.OfType<DashboardGroup>().ToList();
        var exams = groups.Single(group => group.Title == ExamText.Exams);
        Assert.AreEqual(0, exams.Count);
        Assert.AreSame(page.Exams!.NewCommand, exams.Action!.Command);
        Assert.AreEqual(ExamText.NewExamName, exams.Action.Name);
        var overdue = groups.Single(group => group.Title == DashboardText.Overdue);
        Assert.AreSame(page.Spread!.OpenCommand, overdue.Action!.Command);
        Assert.AreEqual(SpreadText.Action, overdue.Action.Label);
        Assert.IsTrue(overdue.HasAction);

        // Rien de fait encore : 0 sur 2, et ce que la séance ouvrira d'abord.
        StringAssert.StartsWith(page.Progress, "0 sur 2 fait · prochain : Fiche ", StringComparison.Ordinal);
        Assert.AreEqual(0, page.DoneShare);

        // Une révision faite : la journée avance, et sans retard, « Étaler… » passe à la section suivante.
        var first = page.Rows.OfType<DueItemRow>().First().Item;
        Assert.IsNull(vault.App.Reviews.Validate(first.Kind, first.Id).Error);
        await page.RefreshCommand.ExecuteAsync(null);
        StringAssert.StartsWith(page.Progress, "1 sur 2 fait · prochain : Fiche ", StringComparison.Ordinal);
        Assert.AreEqual(0.5, page.DoneShare);
        var second = page.Rows.OfType<DueItemRow>().First().Item;
        Assert.IsNull(vault.App.Reviews.Validate(second.Kind, second.Id).Error);
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual($"2 sur 2 faits · {DashboardText.DayDone}", page.Progress);
        Assert.IsFalse(page.HasTiles);
        Assert.IsFalse(page.Rows.OfType<DashboardGroup>().Any(group => group.Title == DashboardText.Overdue));
    }

    [TestMethod]
    public async Task A_day_of_the_forecast_opens_the_workload_on_it()
    {
        using var vault = new LateVault(sheets: 1, daysAway: 0);
        var opener = new FakeOpener();
        var page = new DashboardPageViewModel(vault.App.Reminders, vault.App.Sheets, opener, null, vault.App.Reviews, vault.App.Exams, vault.App.Folders, vault.Time);
        await page.RefreshCommand.ExecuteAsync(null);

        page.OpenWorkloadCommand.Execute(null);
        var day = page.Forecast[1];
        page.OpenWorkloadOnCommand.Execute(day);
        page.OpenWorkloadOnCommand.Execute(null);
        CollectionAssert.AreEqual(new object[] { ("charge", DashboardFilter.All), ("charge", DashboardFilter.All, day.Day) }, opener.Opened);
        Assert.AreEqual(vault.Time.GetLocalNow().Date.AddDays(2), day.Day.ToDateTime(TimeOnly.MinValue));
    }

    [TestMethod]
    public async Task A_new_sheet_is_created_from_today_and_opens_for_writing()
    {
        using var vault = new LateVault(sheets: 1, daysAway: 2);
        var opener = new FakeOpener();
        var page = new DashboardPageViewModel(vault.App.Reminders, vault.App.Sheets, opener, null, vault.App.Reviews, vault.App.Exams, vault.App.Folders, vault.Time);
        await page.RefreshCommand.ExecuteAsync(null);

        page.NewSheetCommand.Execute(null);
        Assert.IsTrue(page.IsCreatingSheet);
        Assert.AreEqual(DashboardText.NewSheetAtRoot, page.NewSheetPlace);

        await page.AnswerNewSheetCommand.ExecuteAsync(true);
        Assert.IsTrue(page.IsCreatingSheet, "sans titre, la fenêtre reste ouverte");
        Assert.AreEqual(SheetsText.TitleRequired, page.NewSheetError);

        await page.AnswerNewSheetCommand.ExecuteAsync(false);
        Assert.IsFalse(page.IsCreatingSheet);
        Assert.IsFalse(page.HasNewSheetError);
        Assert.HasCount(1, vault.App.Sheets.List().Value);

        // Avec un dossier choisi dans la liste, la fiche y est créée.
        page.SelectedScope = page.Scopes.Single(scope => scope.Label == "Dossier : Biologie");
        page.NewSheetCommand.Execute(null);
        Assert.AreEqual("Elle sera créée dans « Biologie », et entre en révision aujourd'hui (J0).", page.NewSheetPlace);
        page.NewSheetTitle = "  Méiose ";
        await page.AnswerNewSheetCommand.ExecuteAsync(true);

        Assert.IsFalse(page.IsCreatingSheet);
        var created = vault.App.Sheets.List().Value.Single(sheet => sheet.Title == "Méiose");
        Assert.AreEqual("Biologie", created.Folder);
        Assert.IsTrue(opener.Opened.Count > 0, "la fiche s'ouvre");
    }

    [TestMethod]
    public async Task Suspended_items_wait_in_their_own_folded_group()
    {
        using var vault = new LateVault(sheets: 2, daysAway: 2);
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var first = page.Dashboard!.Overdue[0];

        Assert.IsTrue(vault.App.Reviews.Suspend(first.Kind, first.Id).Succeeded);
        await page.RefreshCommand.ExecuteAsync(null);

        Assert.AreEqual("Commencer (1)", page.StartLabel);
        var group = page.Rows.OfType<DashboardGroup>().Single(candidate => candidate.Title == "Suspendues");
        Assert.AreEqual(1, group.Count);
        Assert.IsFalse(group.IsExpanded, "replié : ils ne sont pas à réviser");
        Assert.AreEqual(1, page.Rows.OfType<DueItemRow>().Count());

        group.ToggleCommand.Execute(null);
        Assert.IsTrue(page.Rows.OfType<DueItemRow>().Any(row => row.Item.Id == first.Id));

        Assert.IsTrue(vault.App.Reviews.Resume(first.Kind, first.Id).Succeeded);
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsFalse(page.Rows.OfType<DashboardGroup>().Any(candidate => candidate.Title == "Suspendues"));
        Assert.AreEqual("Commencer (2)", page.StartLabel);
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
