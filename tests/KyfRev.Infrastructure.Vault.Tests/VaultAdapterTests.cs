using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// Les adaptateurs des ports de l'application (<see cref="VaultStore"/>, <see cref="VaultAdapter"/>,
/// <see cref="BackupFiles"/>) : chaque opération atteint la session du coffre, avec les chemins
/// validés et les repères convertis.
/// </summary>
public abstract class VaultAdapterTests(StorageKind storage)
{
    protected static readonly Moment Now = new(new DateOnly(2026, 10, 1), new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    protected static readonly Moment Tomorrow = new(new DateOnly(2026, 10, 2), new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose"], 5, "sha256:aa");

    [TestMethod]
    public void The_store_opens_creates_and_compares_vault_folders()
    {
        using var folder = TemporaryVault.Empty(storage);
        var store = new VaultStore(storage: folder.Storage);
        using (var created = store.Initialize(folder.Root, Now))
        {
            Assert.AreEqual(folder.Root, created.Root);
            Assert.AreEqual(VaultErrorKind.AlreadyOpen, Assert.ThrowsExactly<VaultException>(() => store.Open(folder.Root, Now)).Kind);
        }
        using var opened = store.Open(folder.Root, Now);

        Assert.IsTrue(store.SameLocation(folder.Root, folder.Root + Path.DirectorySeparatorChar));
        Assert.IsFalse(store.SameLocation(folder.Root, folder.PathOf("fiches")));
        Assert.IsTrue(store.SameLocation(folder.PathOf("absent"), folder.PathOf("absent")));
        Assert.IsFalse(store.SameLocation(folder.PathOf("absent"), folder.Root));
        Assert.AreEqual(VaultErrorKind.FolderNotEmpty, Assert.ThrowsExactly<VaultException>(() => store.Initialize(folder.Root, Now)).Kind);
    }

    [TestMethod]
    public void A_definition_kept_to_its_sheet_says_so_in_its_file_only_then_and_keeps_it_when_edited()
    {
        using var folder = TemporaryVault.Empty();
        using var vault = new VaultStore().Initialize(folder.Root, Now);
        var id = vault.CreateSheet(null, "Mitose", "La prophase précède la métaphase.\n", Now);
        var definition = vault.CreateDefinition(id, Passage with { Exact = "prophase" }, new Definition("Prophase", "Première phase."), Now);
        var file = Directory.GetFiles(folder.PathOf(".suivi"), "*.json", SearchOption.AllDirectories)
            .Single(path => File.ReadAllText(path).Contains("\"terme\"", StringComparison.Ordinal));
        var written = File.ReadAllText(file);
        Assert.DoesNotContain("partout", written, "la 0.x n'a pas ce champ : il ne s'écrit que s'il vaut faux");
        Assert.IsTrue(vault.Definitions(Now.Day).Single().Everywhere);

        vault.MarkDefinitionEverywhere(definition, everywhere: false);
        vault.MarkDefinitionEverywhere(definition, everywhere: false);
        vault.ModifyDefinition(definition, new Definition("Prophase", "Condensation."), Now);

        Assert.Contains("\"partout\": false", File.ReadAllText(file));
        Assert.IsFalse(vault.Definitions(Now.Day).Single().Everywhere, "modifier la définition garde le choix");
        Assert.IsFalse(vault.OpenSheet(id, Now.Day).Annotations.Single().Definition!.Everywhere);

        vault.ModifyDefinition(definition, new Definition("Prophase", "Première phase."), Now);
        var edited = File.ReadAllText(file);
        vault.MarkDefinitionEverywhere(definition, everywhere: true);
        Assert.DoesNotContain("partout", File.ReadAllText(file));
        vault.MarkDefinitionEverywhere(definition, everywhere: false);
        Assert.AreEqual(edited, File.ReadAllText(file), "un choix d'affichage ne change ni la date ni l'historique");
        Assert.AreEqual(VaultErrorKind.NotADefinition, Assert.ThrowsExactly<VaultException>(() => vault.MarkDefinitionEverywhere(vault.CreateAnnotation(id, Passage, null, Now), everywhere: false)).Kind);
    }

    [TestMethod]
    public void Schemas_reach_the_session_and_a_conflict_says_what_the_disk_holds()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now);
        Assert.IsEmpty(vault.Schemas());

        var created = vault.CreateSchema("{\"format\": 2, \"titre\": \"Réseau\", \"equipements\": [{}, {}]}");
        var other = vault.DuplicateSchema(created.Id);
        vault.CreateSheet(null, "Citante", $"```reseau\nschema: {created.Id}\n```\n", Now);
        vault.CreateSheet(null, "Trompeuse", "schema: pas_un_identifiant\n", Now);
        var saved = (SheetSaved)vault.SaveSchema(other.Id, other.Text, other.Fingerprint);

        Assert.AreEqual(saved.Fingerprint, vault.ReadSchema(other.Id).Fingerprint);
        var summaries = vault.Schemas();
        Assert.HasCount(2, summaries);
        var cited = summaries.Single(summary => summary.Id == created.Id);
        Assert.AreEqual("Réseau", cited.Title);
        Assert.AreEqual(2, cited.Devices);
        Assert.AreEqual("Citante", cited.Sheets.Single().Title);
        Assert.IsEmpty(summaries.Single(summary => summary.Id == other.Id).Sheets);

        var changed = created.Text.Replace("Réseau", "Lycée", StringComparison.Ordinal);
        vault.SaveSchema(created.Id, changed, created.Fingerprint);
        var conflict = (SaveConflict)vault.SaveSchema(created.Id, created.Text, created.Fingerprint);
        Assert.AreEqual(changed, conflict.DiskBody);

        folder.Write("schemas/XXX.json", "{}");
        folder.Write($"schemas/s_{new string('1', 26)}.json", "{ cassé");
        Assert.HasCount(2, vault.Schemas(), "Un fichier illisible ne casse pas la liste.");

        var outside = Path.Combine(Path.GetTempPath(), "kyfrev-schemas-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            var written = vault.ExportSchema(outside, "   ", "svg", [1]);
            Assert.AreEqual(".svg", Path.GetExtension(written));
            Assert.AreEqual(VaultErrorKind.InvalidPath, Assert.ThrowsExactly<VaultException>(() => vault.ExportSchema(outside, "n", "exe", [1])).Kind);
            Assert.AreEqual(VaultErrorKind.InvalidSchema, Assert.ThrowsExactly<VaultException>(() => vault.ExportSchema(outside, "n", "png", new byte[(32 * 1024 * 1024) + 1])).Kind);
            Assert.ThrowsExactly<ArgumentNullException>(() => vault.ExportSchema(outside, null!, "png", [1]));

            var file = Path.Combine(outside, "import.json");
            File.WriteAllText(file, "{}");
            Assert.AreEqual("{}", vault.ReadSchemaToImport(file));
            Assert.AreEqual(VaultErrorKind.InvalidPath, Assert.ThrowsExactly<VaultException>(() => vault.ReadSchemaToImport(Path.Combine(outside, "import.txt"))).Kind);
            Assert.AreEqual(VaultErrorKind.Io, Assert.ThrowsExactly<VaultException>(() => vault.ReadSchemaToImport(Path.Combine(outside, "absent.json"))).Kind);
            Assert.ThrowsExactly<ArgumentNullException>(() => vault.ReadSchemaToImport(null!));
            File.WriteAllBytes(file, [0x7B, 0xE9, 0x7D]);
            Assert.AreEqual(VaultErrorKind.InvalidSchema, Assert.ThrowsExactly<VaultException>(() => vault.ReadSchemaToImport(file)).Kind);
            File.WriteAllBytes(file, new byte[VaultSession.MaxSchemaBytes + 1]);
            Assert.AreEqual(VaultErrorKind.InvalidSchema, Assert.ThrowsExactly<VaultException>(() => vault.ReadSchemaToImport(file)).Kind);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [TestMethod]
    public void Sheets_editing_folders_and_trash_reach_the_session()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now);
        var id = vault.CreateSheet("Biologie", "Mitose", vault.TemplateBody("Cours", "Mitose", "Biologie", Now.Day), Now);

        var sheet = vault.OpenSheet(id, Now.Day);
        Assert.AreEqual(("fiches/Biologie/Mitose.md", "Biologie", Now.Day), (sheet.Path, sheet.Folder, sheet.CreatedOn));
        Assert.AreEqual(id, vault.SheetIdAt("fiches/Biologie/Mitose.md"));
        Assert.IsNull(vault.SheetIdAt("fiches/Absente.md"));
        vault.ChangeTitle(id, "Mitose cellulaire", Now);
        Assert.AreEqual("bio", vault.SetTags(id, ["bio"], Now).Single());
        vault.MoveSheet(id, null, Now);
        Assert.AreEqual(string.Empty, vault.OpenSheet(id, Now.Day).Folder);

        Assert.IsInstanceOfType<SheetSaved>(vault.SaveBody(id, "Corps.\n", vault.OpenSheet(id, Now.Day).Fingerprint, 1, Now));
        vault.KeepAsVersion(id, "Brouillon.\n", Now);
        var version = vault.Versions(id)[0].Name;
        Assert.IsNotNull(vault.ReadVersion(id, version));
        Assert.IsInstanceOfType<SheetSaved>(vault.RestoreVersion(id, version, vault.OpenSheet(id, Now.Day).Fingerprint, Now));
        Assert.Contains("carte.png", vault.AddResource(id, "carte.png", [1, 2]));

        Assert.AreEqual("Physique", vault.CreateFolder("Physique"));
        Assert.AreEqual("Sciences", vault.MoveFolder("Physique", "Sciences", Now));
        CollectionAssert.Contains(vault.Folders().ToList(), "Sciences");
        vault.TrashSheet(id, Now);
        Assert.AreEqual(id, vault.TrashedSheets().Single().Id);
        vault.RestoreSheet(id, Now);
        vault.CreateSheet("Vieux", "Ancienne", string.Empty, Now);
        Assert.AreEqual(1, vault.TrashFolder("Vieux", Now));
        vault.CreateSheet("Encore", "Ancienne", string.Empty, Now);
        Assert.AreEqual(1, vault.PurgeFolder("Encore", Now));
        var effacee = vault.CreateSheet(null, "Effacée", string.Empty, Now);
        vault.TrashSheet(effacee, Now);
        vault.PurgeSheet(effacee);
        Assert.IsFalse(vault.TrashedSheets().Any(sheet => sheet.Id == effacee));

        folder.Write("fiches/Libre.md", "---\ncreee: pas une date\n---\nTexte.\n");
        var tracked = vault.TrackFile("fiches/Libre.md", Now);
        Assert.IsNull(vault.OpenSheet(tracked, Now.Day).CreatedOn);
        Assert.AreEqual(VaultErrorKind.InvalidPath, Assert.ThrowsExactly<VaultException>(() => vault.TrackFile("../hors.md", Now)).Kind);
        Assert.AreEqual("Mon modèle", vault.SaveAsTemplate(id, "Mon modèle"));
        Assert.IsTrue(vault.Templates().Any(t => t.Name == "Mon modèle"));
    }

    [TestMethod]
    public void A_forgotten_review_is_written_read_back_and_steps_back()
    {
        using var folder = TemporaryVault.Empty(storage);
        var store = new VaultStore(storage: folder.Storage);
        string id;
        var third = new Moment(new DateOnly(2026, 10, 5), new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
        using (var vault = store.Initialize(folder.Root, Now))
        {
            id = vault.CreateSheet(null, "Mitose", "La prophase précède la métaphase.\n", Now);
            vault.ValidateReview(ItemKind.Sheet, id, Tomorrow);
            Assert.AreEqual(new NextStepScheduled("J1", new DateOnly(2026, 10, 6)), vault.ValidateReview(ItemKind.Sheet, id, third, recall: ReviewRecall.Forgotten));
        }

        using var reopened = store.Open(folder.Root, third);
        var state = reopened.ReviewStateOf(ItemKind.Sheet, id, third.Day);
        Assert.AreEqual("J1", state.Step);
        Assert.IsTrue(state.ReviewedToday);
        // La frise : J0 fait, J1 de nouveau à faire, bien qu'il ait déjà été validé une fois.
        Assert.AreEqual("J0 fait, J1 à faire, J3 à faire", string.Join(", ", state.Steps.Take(3).Select(step => $"{step.Label} {(step.DoneOn is null ? "à faire" : "fait")}")));
        var statistics = reopened.Statistics(third.Day);
        Assert.AreEqual((2, 1), (statistics.Reviews, statistics.Forgotten));
    }

    [TestMethod]
    public void Smoothing_is_off_by_default_written_only_when_on_and_picks_a_lighter_day()
    {
        using var folder = TemporaryVault.Empty(storage);
        var store = new VaultStore(storage: folder.Storage);
        var day = new DateOnly(2026, 10, 1);
        Moment On(int offset) => new(day.AddDays(offset), new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero).AddDays(offset));
        string first;
        string second;
        using (var vault = store.Initialize(folder.Root, Now))
        {
            Assert.IsFalse(vault.Preferences.SmoothDueDates);
            first = vault.CreateSheet(null, "Une", "Texte.\n", Now);
            second = vault.CreateSheet(null, "Deux", "Texte.\n", Now);
            // J1 le 2, J3 le 5 : J7 tombe exactement le 12 pour les deux, sans lissage.
            foreach (var id in new[] { first, second })
            {
                vault.ValidateReview(ItemKind.Sheet, id, On(1));
            }
            vault.ValidateReview(ItemKind.Sheet, first, On(4));
            Assert.AreEqual(day.AddDays(11), vault.ReviewStateOf(ItemKind.Sheet, first, On(4).Day).DueDate);

            vault.ChangePreferences(vault.Preferences with { SmoothDueDates = true });
            // Le 12 est déjà pris par la première : la seconde se pose la veille.
            vault.ValidateReview(ItemKind.Sheet, second, On(4));
            Assert.AreEqual(day.AddDays(10), vault.ReviewStateOf(ItemKind.Sheet, second, On(4).Day).DueDate);
        }

        using var reopened = store.Open(folder.Root, On(4));
        Assert.IsTrue(reopened.Preferences.SmoothDueDates, "le réglage est relu");
        reopened.ChangePreferences(reopened.Preferences with { SmoothDueDates = false });
        Assert.IsFalse(reopened.Preferences.SmoothDueDates);
    }

    [TestMethod]
    public void A_suspended_item_leaves_the_day_and_the_load_and_comes_back_when_resumed()
    {
        using var folder = TemporaryVault.Empty(storage);
        var store = new VaultStore(storage: folder.Storage);
        string kept;
        string paused;
        using (var vault = store.Initialize(folder.Root, Now))
        {
            kept = vault.CreateSheet(null, "Gardée", "Texte.\n", Now);
            paused = vault.CreateSheet(null, "Suspendue", "Texte.\n", Now);
            vault.Suspend(ItemKind.Sheet, paused, Now);
            Assert.AreEqual(VaultErrorKind.Tracking, Assert.ThrowsExactly<VaultException>(() => vault.ValidateReview(ItemKind.Sheet, paused, Tomorrow)).Kind);
        }

        using var reopened = store.Open(folder.Root, Tomorrow);
        Assert.AreEqual(Now.Day, reopened.ReviewStateOf(ItemKind.Sheet, paused, Tomorrow.Day).SuspendedOn, "la suspension est relue");
        var dashboard = reopened.Dashboard(Tomorrow.Day);
        Assert.AreEqual(kept, dashboard.Today.Single().Id);
        Assert.AreEqual(paused, dashboard.Suspended.Single().Id);
        Assert.IsFalse(dashboard.Workload.SelectMany(day => day.Due).Any(item => item.Id == paused));
        Assert.AreEqual(new SpreadPlan(1, 0, null), reopened.SpreadDueDates(new SpreadRequest(1), Tomorrow, apply: false), "elle ne compte pas dans un étalement");

        reopened.ResumeReview(ItemKind.Sheet, paused);
        var resumed = reopened.Dashboard(Tomorrow.Day);
        Assert.IsEmpty(resumed.Suspended);
        Assert.HasCount(2, resumed.Today);
        Assert.IsNull(reopened.ReviewStateOf(ItemKind.Sheet, paused, Tomorrow.Day).SuspendedOn);
    }

    [TestMethod]
    public void Spreading_shows_its_plan_first_then_postpones_without_restarting()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now);
        var ids = Enumerable.Range(1, 5).Select(rank => vault.CreateSheet(null, $"Fiche {rank}", "Texte.\n", Now)).ToList();
        var later = new Moment(new DateOnly(2026, 10, 9), new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));

        // Créées le 1er, toutes dues le 2 : le 9, cinq J1 en retard.
        var plan = vault.SpreadDueDates(new SpreadRequest(2), later, apply: false);
        Assert.AreEqual(new SpreadPlan(2, 3, new DateOnly(2026, 10, 11)), plan);
        Assert.IsTrue(ids.All(id => vault.ReviewStateOf(ItemKind.Sheet, id, later.Day).DueDate == new DateOnly(2026, 10, 2)), "Le plan n'écrit rien.");

        Assert.AreEqual(plan, vault.SpreadDueDates(new SpreadRequest(2), later, apply: true));
        var states = ids.Select(id => vault.ReviewStateOf(ItemKind.Sheet, id, later.Day)).ToList();
        Assert.AreEqual("2,2,1", string.Join(',', states.GroupBy(state => state.DueDate).OrderBy(group => group.Key).Select(group => group.Count())));
        Assert.IsTrue(states.All(state => state is { Step: "J1", Cycle: 1 }), "Aucun cycle ne recommence.");
        Assert.AreEqual(new SpreadPlan(2, 0, null), vault.SpreadDueDates(new SpreadRequest(2), later, apply: false), "Le retard restant tient dans la journée.");

        // Une pause du 10 au 11 : ce qui y tombait part après le retour.
        var pause = vault.SpreadDueDates(new SpreadRequest(5, new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 11)), later, apply: true);
        Assert.AreEqual(new SpreadPlan(0, 3, new DateOnly(2026, 10, 12)), pause);

        Assert.AreEqual(VaultErrorKind.InvalidSettings, Assert.ThrowsExactly<VaultException>(() => vault.SpreadDueDates(new SpreadRequest(0), later, apply: false)).Kind);
        Assert.AreEqual(VaultErrorKind.InvalidSettings, Assert.ThrowsExactly<VaultException>(() => vault.SpreadDueDates(new SpreadRequest(2, later.Day), later, apply: false)).Kind);
        Assert.AreEqual(VaultErrorKind.InvalidSettings, Assert.ThrowsExactly<VaultException>(() => vault.SpreadDueDates(new SpreadRequest(2, later.Day.AddDays(3), later.Day.AddDays(2)), later, apply: false)).Kind);
    }

    [TestMethod]
    public void Reviews_annotations_reminders_exams_and_settings_reach_the_session()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now);
        var id = vault.CreateSheet(null, "Mitose", "La prophase précède la métaphase.\n", Now);

        Assert.IsInstanceOfType<NextStepScheduled>(vault.ValidateReview(ItemKind.Sheet, id, Tomorrow));
        vault.Postpone(ItemKind.Sheet, id, Tomorrow, new DateOnly(2026, 10, 9));
        Assert.AreEqual(new DateOnly(2026, 10, 9), vault.ReviewStateOf(ItemKind.Sheet, id, Tomorrow.Day).DueDate);
        vault.Restart(ItemKind.Sheet, id, Tomorrow);
        Assert.AreEqual(2, vault.ReviewStateOf(ItemKind.Sheet, id, Tomorrow.Day).Cycle);

        var passage = vault.CreateAnnotation(id, Passage, "Commentaire", Now);
        var definition = vault.CreateDefinition(id, Passage with { Exact = "prophase" }, new Definition("Prophase", "Première phase."), Now);
        vault.ModifyDefinition(definition, new Definition("Prophase", "Condensation."), Now);
        vault.ModifyComment(passage, "Autre", Now);
        vault.Reanchor(passage, Passage with { Start = 1, End = 12 }, AnchorState.NeedsReview, Now);
        vault.ModifyPassage(passage, Passage with { Exact = "La métaphase" }, restart: false, Now);
        Assert.IsInstanceOfType<NextStepScheduled>(vault.ValidateReview(ItemKind.Passage, passage, Tomorrow));
        Assert.AreEqual("J3", vault.ReviewStateOf(ItemKind.Passage, passage, Tomorrow.Day).Step);
        vault.TrashAnnotation(definition, Now);
        Assert.AreEqual(definition, vault.TrashedAnnotations().Single().Id);
        vault.RestoreAnnotation(definition, Now);
        var opened = vault.OpenSheet(id, Tomorrow.Day);
        Assert.AreEqual(("Autre", "Prophase"), (opened.Annotations[0].Comment, opened.Annotations[1].Definition!.Term));
        Assert.AreEqual(Passage with { Exact = "La métaphase" }, opened.Annotations[0].Anchor);

        vault.ArchiveSheet(id, Tomorrow);
        vault.IncludeInReminders(id, include: false, Tomorrow);
        Assert.IsFalse(vault.ReviewStateOf(ItemKind.Sheet, id, Tomorrow.Day).IncludedInReminders);
        Assert.AreEqual(VaultErrorKind.Tracking, Assert.ThrowsExactly<VaultException>(() => vault.HandleReminder(id, ReminderOutcome.Known, Tomorrow)).Kind);
        Assert.AreEqual(Tomorrow.Day, vault.PrepareDay(Tomorrow).Day);

        vault.DefineExam(string.Empty, "Partiels", new DateOnly(2026, 11, 30));
        Assert.AreEqual(0, vault.TightenBeforeExam(string.Empty, Tomorrow));
        vault.WithdrawExam(string.Empty);
        vault.ChangePreferences(vault.Preferences with { MaxPerDay = 3 });
        Assert.AreEqual(3, vault.Preferences.MaxPerDay);
    }

    [TestMethod]
    public void A_review_dated_by_hand_reaches_the_session()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now);
        var id = vault.CreateSheet(null, "Mitose", "La prophase précède la métaphase.\n", Now);
        var passage = vault.CreateAnnotation(id, Passage, null, Now);
        var later = new Moment(Now.Day.AddDays(5), Now.Instant.AddDays(5));

        Assert.IsInstanceOfType<NextStepScheduled>(vault.ValidateReview(ItemKind.Sheet, id, later, Now.Day.AddDays(2)));
        Assert.IsInstanceOfType<NextStepScheduled>(vault.ValidateReview(ItemKind.Passage, passage, later, Now.Day.AddDays(3)));

        Assert.AreEqual(Now.Day.AddDays(2), vault.ReviewStateOf(ItemKind.Sheet, id, later.Day).LastReview);
        Assert.AreEqual(Now.Day.AddDays(3), vault.ReviewStateOf(ItemKind.Passage, passage, later.Day).LastReview);
    }

    [TestMethod]
    public void Dashboard_and_archives_reach_the_session()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now);
        var id = vault.CreateSheet(null, "Mitose", "La prophase.\n", Now);

        Assert.AreEqual(id, vault.Dashboard(Tomorrow.Day).Today.Single().Id);
        Assert.IsEmpty(vault.Archives(Tomorrow.Day));
        vault.ArchiveSheet(id, Tomorrow);
        Assert.AreEqual(id, vault.Archives(Tomorrow.Day).Single().Id);
    }

    [TestMethod]
    public void The_sheet_list_goes_by_folder_then_title_and_says_where_each_sheet_is()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now);
        var zebra = vault.CreateSheet("Histoire", "zèbre", "", Now);
        var atoms = vault.CreateSheet("Histoire", "Atomes", "", Now);
        var root = vault.CreateSheet(null, "À la racine", "", Now);
        var archived = vault.CreateSheet("Biologie", "Mitose", "", Now);
        vault.ArchiveSheet(archived, Tomorrow);

        var sheets = vault.SheetList();

        CollectionAssert.AreEqual(new[] { root, archived, atoms, zebra }, sheets.Select(sheet => sheet.Id).ToArray());
        Assert.AreEqual(string.Empty, sheets[0].Folder);
        Assert.AreEqual("J1", sheets[0].Step);
        Assert.AreEqual(Tomorrow.Day, sheets[0].DueDate);
        Assert.IsFalse(sheets[0].Archived);
        Assert.IsTrue(sheets[0].FilePresent);
        Assert.AreEqual("Biologie", sheets[1].Folder);
        Assert.IsTrue(sheets[1].Archived);
        Assert.IsNull(sheets[1].Step);
        Assert.IsNull(sheets[1].DueDate);
        StringAssert.EndsWith(sheets[1].Path, "Mitose.md", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Quotes_reach_the_session()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now);
        Assert.StartsWith("v_", vault.Id);

        var quote = vault.AddQuote(new QuoteContent("Que sais-je ?", "Montaigne", "1580", QuoteSource.None, null, []), Now);
        Assert.AreEqual("Que sais-je ?", vault.ModifyQuote(quote.Path, quote.Content with { Context = "Essais" }, Now).Content.Text);
        var trashed = vault.TrashQuote(quote.Path);
        Assert.AreEqual(trashed, vault.Quotes().Trash.Single().Path);
        Assert.AreEqual(quote.Path, vault.RestoreQuote(trashed));

        Assert.IsNull(vault.OnlineQuoteDay());
        var online = vault.KeepOnlineQuote("Sagesse", [new QuoteContent("La sagesse vient avec l'âge.", null, null, QuoteSource.None, null, [])], Now);
        Assert.AreEqual("wikiquote", online!.Origin);
        Assert.AreEqual(Now.Day, vault.OnlineQuoteDay());
        Assert.AreEqual(online.Path, vault.OnlineQuoteOf(Now.Day)!.Path);
        Assert.IsNull(vault.OnlineQuoteOf(Tomorrow.Day));
    }

    [TestMethod]
    public void Health_backups_exports_sharing_and_import_reach_the_session()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var outside = TemporaryVault.Empty();
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now);
        var id = vault.CreateSheet(null, "Mitose", "Texte.\n", Now);

        folder.Write("fiches/Orpheline.md", "---\nid: f_01JQZ8S7ZK4V3H2N8M6X5W9PQR\n---\nTexte.\n");
        folder.Copy("fiches/Mitose.md", "fiches/Copie.md");
        vault.Analyze(Now);
        Assert.IsGreaterThanOrEqualTo(2, vault.HealthDetails().Problems);
        Assert.AreEqual("f_01JQZ8S7ZK4V3H2N8M6X5W9PQR", vault.RecreateTracking("fiches/Orpheline.md", Now));
        Assert.HasCount(1, vault.KeepOriginal(id, "fiches/Mitose.md", Now));

        var backup = vault.Backup(outside.PathOf("Sauvegardes"), new DateTime(2026, 10, 1, 9, 0, 0));
        folder.Delete("fiches/Mitose.md");
        vault.Analyze(Now);
        Assert.AreEqual("fiches/Mitose.md", vault.RestoreSheetFromBackup(id, backup.Path, Now));

        Assert.HasCount(2, vault.Export(ExportKind.Tracking, outside.Root, new DateTime(2026, 10, 1, 9, 0, 0), TimeZoneInfo.Utc));
        Assert.IsTrue(File.Exists(vault.ShareSheet(id, ShareFormat.Markdown, outside.Root, null)));
        Assert.IsTrue(File.Exists(vault.ShareSheetAsPdf(id, outside.Root, new byte[] { 37, 80, 68, 70 })));
        Assert.AreEqual("<p>Rien à intégrer.</p>", vault.EmbedSharedImages("<p>Rien à intégrer.</p>"));
        outside.Write("Notes/Note.md", "Une note.\n");
        Assert.AreEqual(1, vault.ImportFolder(outside.PathOf("Notes"), "Notes", track: false, Now).Sheets);
    }

    [TestMethod]
    public void Backup_files_are_listed_described_rotated_and_restored()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var outside = TemporaryVault.Empty();
        var files = new BackupFiles();
        var backups = outside.PathOf("Sauvegardes");
        Assert.IsEmpty(files.List(backups));
        Assert.IsTrue(files.DailyBackupNeeded(backups, Now.Day));
        using (var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now))
        {
            vault.Backup(backups, new DateTime(2026, 10, 1, 9, 0, 0));
            vault.Backup(backups, new DateTime(2026, 10, 1, 18, 0, 0));
        }

        var listed = files.List(backups);
        Assert.AreEqual("coffre-2026-10-01T18-00-00.zip coffre-2026-10-01T09-00-00.zip", string.Join(' ', listed.Select(b => b.Name)));
        Assert.IsTrue(listed.All(b => b.Size > 0));
        Assert.IsFalse(files.DailyBackupNeeded(backups, Now.Day));
        Assert.AreEqual(new DateTime(2026, 10, 1, 9, 0, 0), files.TimestampOf("coffre-2026-10-01T09-00-00.zip"));
        Assert.IsTrue(files.IsFile(Path.Combine(backups, listed[0].Name)));
        Assert.IsFalse(files.IsFile(backups));
        Assert.AreEqual(new BackupEntry("coffre-perdue.zip", default, 0), files.Describe(Path.Combine(backups, "coffre-perdue.zip")));

        Assert.AreEqual(Path.Combine(backups, listed[1].Name), files.ApplyRotation(backups, RotationPolicy.Default).Single());
        var restored = files.Restore(Path.Combine(backups, listed[0].Name), outside.Root, Now.Day);
        Assert.IsTrue(TemporaryVault.HasVaultFile(restored, ".suivi/coffre.json"), "un coffre chiffré revient chiffré");
        Assert.AreEqual(storage == StorageKind.Encrypted, KyfRev.Infrastructure.Crypto.EncryptedVault.IsEncrypted(restored));
    }
}

/// <summary>Sur le disque tel quel.</summary>
[TestClass]
public sealed class VaultAdapterOnDiskTests() : VaultAdapterTests(StorageKind.Disk);

/// <summary>Sur un coffre chiffré.</summary>
[TestClass]
public sealed class VaultAdapterEncryptedTests() : VaultAdapterTests(StorageKind.Encrypted);
