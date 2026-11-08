using System.Reflection;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.Tests.Support;
using KyfRev.Application.UseCases;
using KyfRev.Domain.Reviews;

namespace KyfRev.Application.Tests;

/// <summary>Chaque cas d'usage, essayé sur un vrai coffre : réussite, refus avec le message de la 0.x.</summary>
[TestClass]
public sealed class UseCaseTests
{
    [TestMethod]
    public void The_statistics_read_the_history_of_the_vault()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        var app = test.App;
        var id = app.Sheets.Create("Biologie", "Mitose", string.Empty).Value;
        test.Time.Advance(TimeSpan.FromDays(1));
        Assert.IsTrue(app.Reviews.Validate(ItemKind.Sheet, id).Succeeded);

        var statistics = app.Reminders.Statistics().Value;

        Assert.AreEqual(1, statistics.Reviews);
        Assert.AreEqual(1, statistics.OnTime);
        var day = statistics.Activity.Single();
        Assert.AreEqual(1, day.Reviews);
        Assert.AreEqual(1, day.Total);
        Assert.AreEqual("Biologie", statistics.ByFolder.Single().Folder);
    }

    [TestMethod]
    public void A_day_of_activity_adds_up_what_was_done()
    {
        var day = new ActivityDay(new DateOnly(2026, 10, 13), 2, 3, 4);

        Assert.AreEqual(5, day.Reviews, "les fiches, les passages et les définitions");
        Assert.AreEqual(9, day.Total, "et les rappels traités");
    }

    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose"], 5, "sha256:aa");

    private static readonly string[] Checks =
    [
        "Choisissez un dossier où enregistrer la fiche.",
        "Choisissez un dossier pour l'export.",
        "« x » n'est pas une image reconnue (PNG, JPEG, GIF, WebP, AVIF, BMP ou SVG).",
    ];

    [TestMethod]
    public void Without_a_vault_every_use_case_is_refused()
    {
        using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0));
        var tried = 0;
        foreach (var group in typeof(KyfRevApplication).GetProperties().Select(p => p.GetValue(test.App)!).Where(g => g is not VaultUseCases and not VaultHost))
        {
            // Les opérations, pas les propriétés (« une clé est retenue ») : elles rendent un résultat.
            foreach (var method in group.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(method => !method.IsSpecialName))
            {
                var outcome = method.Invoke(group, [.. method.GetParameters().Select(p => Sample(p.ParameterType))])!;
                var error = (string?)outcome.GetType().GetProperty(nameof(Outcome<Done>.Error))!.GetValue(outcome);
                Assert.IsTrue(error == VaultHost.NoVaultOpen || Checks.Contains(error), $"{group.GetType().Name}.{method.Name} : {error}");
                tried++;
            }
        }
        Assert.IsGreaterThanOrEqualTo(54, tried);
        Assert.ThrowsExactly<InvalidOperationException>(() => test.App.Sheets.Open("x").Value);
    }

    [TestMethod]
    public void The_glossary_gives_each_definition_and_where_it_comes_from()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        var app = test.App;
        var sheet = app.Sheets.Create("Biologie/Cellule", "Mitose", string.Empty).Value;
        var noyau = app.Annotations.CreateDefinition(sheet, Passage, new Definition("Noyau", "Il renferme l'ADN.")).Value;
        var ecologie = app.Annotations.CreateDefinition(sheet, Passage, new Definition("Écologie", "L'étude des milieux.")).Value;
        app.Annotations.Create(sheet, Passage, "Un simple commentaire.");
        app.Annotations.Trash(app.Annotations.CreateDefinition(sheet, Passage, new Definition("Jetée", "À la corbeille.")).Value);

        var glossary = app.Annotations.Definitions().Value;

        Assert.AreEqual(ecologie + "|" + noyau, string.Join('|', glossary.Select(definition => definition.Id)), "É se range avec E");
        var first = glossary[0];
        Assert.AreEqual(sheet, first.SheetId);
        Assert.AreEqual("Mitose", first.SheetTitle);
        Assert.AreEqual("Biologie/Cellule", first.Folder);
        Assert.AreEqual("Écologie", first.Term);
        Assert.AreEqual("L'étude des milieux.", first.Explanation);
        Assert.AreEqual("Mitose", first.Section.Single());
        Assert.AreEqual(AnchorState.Anchored, first.AnchorState);
        Assert.AreEqual("J1", first.Step);
        Assert.IsNotNull(first.DueDate);
    }

    [TestMethod]
    public void A_setting_changes_from_the_current_ones_and_the_rest_stays()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        var settings = test.App.Settings;
        var before = settings.Get().Value;

        var saved = settings.Update(current => current with { MaxPerDay = before.MaxPerDay + 1 }).Value;

        Assert.AreEqual(before.MaxPerDay + 1, saved.MaxPerDay);
        Assert.AreEqual(before.BackupRotation, saved.BackupRotation);
        Assert.AreEqual(saved.MaxPerDay, settings.Get().Value.MaxPerDay);

        var refused = settings.Update(current => current with { MaxPerDay = 0 });
        Assert.IsFalse(refused.Succeeded);
        Assert.AreEqual(saved.MaxPerDay, settings.Get().Value.MaxPerDay);
    }

    [TestMethod]
    public void The_dashboard_prepares_the_day_and_lists_what_to_review()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        var app = test.App;
        var id = app.Sheets.Create("Biologie", "Mitose", string.Empty).Value;
        var created = app.Reminders.Dashboard().Value;
        Assert.IsEmpty(created.Today, "la création vaut J0");
        Assert.AreEqual(id, created.Upcoming.Single().Id);
        Assert.AreEqual(new DateOnly(2026, 10, 2), created.NextDueDate);

        test.Time.Advance(TimeSpan.FromDays(3));
        var dashboard = app.Reminders.Dashboard().Value;
        Assert.AreEqual(new DateOnly(2026, 10, 4), dashboard.Day);
        Assert.AreEqual(2, dashboard.Overdue.Single().DaysLate);
        Assert.AreEqual(1, dashboard.ActiveSheets);
        Assert.IsEmpty(app.Reminders.Archives().Value);
        Assert.AreEqual(id, app.Sheets.List().Value.Single().Id);

        Assert.IsTrue(app.Reviews.Archive(id).Succeeded);
        Assert.AreEqual(id, app.Reminders.Archives().Value.Single().Id);
        Assert.AreEqual(1, app.Reminders.Dashboard().Value.ArchivedSheets);
    }

    [TestMethod]
    public void Sheets_folders_and_templates()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        var app = test.App;

        var id = app.Sheets.Create("  ", "Mitose", "").Value;
        var templated = app.Sheets.Create("Biologie", "Méiose", "Cours").Value;
        Assert.AreEqual("fiches/Mitose.md", app.Sheets.Open(id).Value.Path);
        Assert.AreEqual("Biologie", app.Sheets.Open(templated).Value.Folder);
        Assert.Contains("## Objectifs", app.Sheets.Open(templated).Value.Body);
        Assert.AreEqual(id, app.Sheets.IdAt("fiches/Mitose.md").Value);
        Assert.IsNull(app.Sheets.IdAt("fiches/Absente.md").Value);

        Assert.IsTrue(app.Sheets.ChangeTitle(id, "Mitose cellulaire").Succeeded);
        Assert.AreEqual("bio,cellule", string.Join(',', app.Sheets.SetTags(id, ["#bio", "bio", "cellule"]).Value));
        Assert.AreEqual("Biologie/Cellule", app.Folders.Create("Biologie/Cellule").Value);
        Assert.IsTrue(app.Sheets.Move(id, " Biologie/Cellule ").Succeeded);
        Assert.AreEqual("Biologie/Cellule", app.Sheets.Open(id).Value.Folder);
        Assert.IsTrue(app.Sheets.Move(id, "").Succeeded);
        Assert.AreEqual(string.Empty, app.Sheets.Open(id).Value.Folder);
        Assert.AreEqual("Cellule", app.Folders.Move("Biologie/Cellule", "Cellule").Value);
        CollectionAssert.Contains(app.Folders.Folders().Value.ToList(), "Cellule");

        Assert.IsTrue(app.Templates.Templates().Value.Any(t => t is { Name: "Cours", BuiltIn: true }));
        Assert.AreEqual("Mon modèle", app.Templates.SaveAsTemplate(templated, "Mon modèle").Value);

        File.WriteAllText(Path.Combine(folder.Vault, "fiches", "Libre.md"), "Texte libre.\n");
        Assert.StartsWith("f_", app.Sheets.Track("fiches/Libre.md").Value);
        Assert.Contains("refusé", app.Sheets.Track("../hors.md").Error!);
        Assert.Contains("Aucune fiche ne porte l'identifiant f_inconnue", app.Sheets.ChangeTitle("f_inconnue", "x").Error!);
    }

    [TestMethod]
    public void Editing_and_versions()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        var app = test.App;
        var id = app.Sheets.Create(null, "Mitose", null).Value;
        var read = app.Sheets.Open(id).Value;

        Assert.IsInstanceOfType<SheetSaved>(app.Editing.Save(id, "Nouveau corps.\n", read.Fingerprint, 1).Value);
        var conflict = app.Editing.Save(id, "Autre corps.\n", read.Fingerprint, 1).Value;
        Assert.IsInstanceOfType<SaveConflict>(conflict);
        Assert.IsTrue(app.Editing.KeepAsVersion(id, "Brouillon.\n").Succeeded);
        var versions = app.Editing.Versions(id).Value;
        Assert.IsNotEmpty(versions);
        Assert.IsNotNull(app.Editing.ReadVersion(id, versions[0].Name).Value);
        Assert.IsInstanceOfType<SheetSaved>(app.Editing.RestoreVersion(id, versions[0].Name, app.Sheets.Open(id).Value.Fingerprint).Value);

        Assert.Contains("carte.png", app.Editing.AddImage(id, "Carte.PNG", new byte[] { 1, 2, 3 }).Value);
        Assert.Contains("n'est pas une image reconnue", app.Editing.AddImage(id, "outil.exe", new byte[] { 1 }).Error!);
        Assert.Contains("n'est pas une image reconnue", app.Editing.AddImage(id, "sans-extension", new byte[] { 1 }).Error!);
    }

    [TestMethod]
    public void Reviews_annotations_reminders_exams_and_settings()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        var app = test.App;
        var id = app.Sheets.Create(null, "Mitose", null).Value;
        test.Time.Advance(TimeSpan.FromDays(1));

        Assert.AreEqual("J3", app.Reviews.Validate(ItemKind.Sheet, id).Value.Step);
        Assert.AreEqual(new DateOnly(2026, 10, 12), app.Reviews.Postpone(ItemKind.Sheet, id, new DateOnly(2026, 10, 12)).Value.DueDate);
        Assert.AreEqual(2, app.Reviews.Restart(ItemKind.Sheet, id).Value.Cycle);

        var passage = app.Annotations.Create(id, Passage, "Commentaire").Value;
        var definition = app.Annotations.CreateDefinition(id, Passage with { Exact = "prophase", Start = 3 }, new Definition("Prophase", "Première phase.")).Value;
        Assert.IsTrue(app.Annotations.ModifyDefinition(definition, new Definition("Prophase", "Condensation.")).Succeeded);
        Assert.IsTrue(app.Annotations.ModifyComment(passage, null).Succeeded);
        Assert.IsTrue(app.Annotations.Reanchor(passage, Passage with { Start = 1, End = 12 }, AnchorState.NeedsReview).Succeeded);
        Assert.IsTrue(app.Annotations.ModifyPassage(passage, Passage with { Exact = "La métaphase" }, restart: true).Succeeded);
        test.Time.Advance(TimeSpan.FromDays(1));
        Assert.AreEqual("J3", app.Reviews.Validate(ItemKind.Passage, passage).Value.Step);
        Assert.IsTrue(app.Annotations.Trash(definition).Succeeded);
        Assert.AreEqual(definition, app.Trash.TrashedAnnotations().Value.Single().Id);
        Assert.IsTrue(app.Annotations.Restore(definition).Succeeded);
        Assert.IsTrue(app.Sheets.Open(id).Value.Annotations.All(a => !a.InTrash));

        Assert.AreEqual(ReviewStatus.Archived, app.Reviews.Archive(id).Value.Status);
        Assert.IsTrue(app.Reminders.Include(id, include: false).Succeeded);
        Assert.IsFalse(app.Sheets.Open(id).Value.Review.IncludedInReminders);
        Assert.IsFalse(app.Reminders.Handle(id, ReminderOutcome.Known).Succeeded);

        Assert.IsTrue(app.Exams.Define(string.Empty, "Partiels", new DateOnly(2026, 11, 30)).Succeeded);
        Assert.AreEqual(0, app.Exams.Tighten(string.Empty).Value);
        Assert.IsTrue(app.Exams.Withdraw(string.Empty).Succeeded);

        var preferences = app.Settings.Get().Value;
        Assert.IsTrue(app.Settings.Change(preferences with { MaxPerDay = 3 }).Succeeded);
        Assert.AreEqual(3, app.Settings.Get().Value.MaxPerDay);
        Assert.IsFalse(app.Settings.Change(preferences with { Steps = [] }).Succeeded);
    }

    [TestMethod]
    public void Trash_health_backups_exports_sharing_and_import()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        var app = test.App;
        var id = app.Sheets.Create(null, "Mitose", null).Value;

        Assert.IsTrue(app.Trash.TrashSheet(id).Succeeded);
        Assert.AreEqual(id, app.Trash.TrashedSheets().Value.Single().Id);
        Assert.IsTrue(app.Trash.RestoreSheet(id).Succeeded);
        app.Sheets.Create("Vieux", "Ancienne", null);
        Assert.AreEqual(1, app.Trash.TrashFolder("Vieux").Value);
        Assert.IsFalse(app.Trash.PurgeSheet(id).Succeeded, "elle n'est plus à la corbeille");
        var erased = app.Sheets.Create("Encore", "Effacée", null).Value;
        Assert.IsTrue(app.Trash.TrashSheet(erased).Succeeded);
        Assert.IsTrue(app.Trash.PurgeSheet(erased).Succeeded);
        Assert.IsFalse(app.Sheets.Open(erased).Succeeded);
        app.Sheets.Create("Encore", "Autre", null);
        Assert.AreEqual(1, app.Trash.PurgeFolder("Encore").Value);

        File.WriteAllText(Path.Combine(folder.Vault, "fiches", "Libre.md"), "Texte.\n");
        File.WriteAllText(Path.Combine(folder.Vault, "fiches", "Orpheline.md"), "---\nid: f_01JQZ8S7ZK4V3H2N8M6X5W9PQR\n---\nTexte.\n");
        File.Copy(Path.Combine(folder.Vault, "fiches", "Mitose.md"), Path.Combine(folder.Vault, "fiches", "Copie.md"));
        var health = app.Maintenance.Analyze().Value;
        CollectionAssert.Contains(health.Untracked.ToList(), "fiches/Libre.md");
        Assert.AreEqual(health.Problems, app.Maintenance.Health().Value.Problems);
        Assert.AreEqual("f_01JQZ8S7ZK4V3H2N8M6X5W9PQR", app.Maintenance.RecreateTracking("fiches/Orpheline.md").Value);
        Assert.HasCount(1, app.Maintenance.KeepOriginal(id, "fiches/Mitose.md").Value);

        var backups = app.Backups.List().Value;
        Assert.AreEqual(folder.Backups, backups.Folder);
        Assert.IsTrue(backups.Rotation);
        Assert.AreEqual(new DateTime(2026, 10, 1, 9, 0, 0), backups.Backups.Single().Timestamp);
        test.Time.Advance(TimeSpan.FromMinutes(5));
        var now = app.Backups.BackupNow().Value;
        Assert.AreEqual(("coffre-2026-10-01T09-05-00.zip", new DateTime(2026, 10, 1, 9, 5, 0)), (now.Name, now.Timestamp));
        Assert.IsGreaterThan(0, now.Size);
        // Rotation : une seule copie par jour, la plus récente.
        Assert.AreEqual(now, app.Backups.List().Value.Backups.Single());
        Assert.AreEqual(folder.PathOf("Coffre (restauré le 2026-10-01)"), app.Backups.Restore(now.Name).Value);
        Assert.AreEqual("La sauvegarde « inconnue.zip » est introuvable.", app.Backups.Restore("inconnue.zip").Error);
        Assert.AreEqual("La sauvegarde « coffre-2026-01-01T00-00-00.zip » est introuvable.", app.Backups.Restore("coffre-2026-01-01T00-00-00.zip").Error);

        File.Delete(Path.Combine(folder.Vault, "fiches", "Mitose.md"));
        app.Maintenance.Analyze();
        Assert.AreEqual("fiches/Mitose.md", app.Maintenance.RestoreFromBackup(id, now.Name).Value);

        var outside = folder.PathOf("Dehors");
        Directory.CreateDirectory(outside);
        Assert.AreEqual("Choisissez un dossier pour l'export.", app.Exports.Export(ExportKind.Annotations, "relatif").Error);
        Assert.HasCount(1, app.Exports.Export(ExportKind.Annotations, outside).Value);
        Assert.AreEqual("Choisissez un dossier où enregistrer la fiche.", app.Sheets.Share(id, ShareFormat.Markdown, "relatif", null).Error);
        Assert.IsTrue(File.Exists(app.Sheets.Share(id, ShareFormat.Markdown, outside, null).Value));
        Assert.AreEqual("Choisissez un dossier où enregistrer la fiche.", app.Sheets.SharePdf(id, "relatif", new byte[] { 37 }).Error);
        Assert.IsTrue(File.Exists(app.Sheets.SharePdf(id, outside, new byte[] { 37 }).Value));
        Assert.AreEqual("<p>Sans image.</p>", app.Sheets.EmbedSharedImages("<p>Sans image.</p>").Value);

        var source = folder.PathOf("Notes");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Note.md"), "Une note.\n");
        Assert.IsFalse(app.Sheets.Import(folder.PathOf("Absent"), track: false).Succeeded);
        var report = app.Sheets.Import(source + Path.DirectorySeparatorChar, track: true).Value;
        Assert.AreEqual(("Notes", 1, 1), (report.Folder, report.Sheets, report.Tracked));
        Assert.HasCount(1, test.Logs.Entries.Where(e => e.Message.StartsWith("Dossier importé : 1 fiche(s), 1 suivie(s)", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void The_open_vault_is_known_without_waiting_for_a_long_operation()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holding = Task.Run(() => test.App.Host.Use(_ =>
        {
            started.Set();
            release.Wait(TimeSpan.FromSeconds(30));
        }));
        Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(10)));

        var reading = Task.Run(() => test.App.Vaults.State());
        var answered = reading.Wait(TimeSpan.FromSeconds(5));
        release.Set();
        Assert.IsTrue(holding.Wait(TimeSpan.FromSeconds(10)));

        Assert.IsTrue(answered, "l'état ne prend pas la file derrière l'opération en cours");
        Assert.AreEqual(folder.Vault, reading.Result.Vault);
    }

    private static TestApplication Opened(TestFolder folder)
    {
        var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0));
        Assert.IsNull(test.App.Vaults.Create(folder.Vault).OpenError);
        return test;
    }

    private static object? Sample(Type type) => type switch
    {
        _ when type == typeof(string) => "x",
        _ when type == typeof(bool) => false,
        _ when type == typeof(ulong) => 0UL,
        _ when type == typeof(DateOnly) => new DateOnly(2026, 10, 1),
        _ when type == typeof(IReadOnlyList<string>) => Array.Empty<string>(),
        _ when type == typeof(ReadOnlyMemory<byte>) => ReadOnlyMemory<byte>.Empty,
        _ when type == typeof(Anchor) => Passage,
        _ when type == typeof(Definition) => new Definition("Terme", "Explication."),
        _ when type == typeof(VaultSecret) => VaultSecret.Password("un mot de passe d'essai"),
        _ when type == typeof(RecoveryKeyDraft) => new RecoveryKeyDraft(@"C:\Coffre", "clé", default, default),
        _ when type == typeof(VaultPreferences) => new VaultPreferences([1, 3], true, 2, 30, [2, 4], false, true, true, false, [], false),
        _ when type == typeof(Func<VaultPreferences, VaultPreferences>) => (Func<VaultPreferences, VaultPreferences>)(preferences => preferences),
        _ when type.IsEnum => Enum.GetValues(type).GetValue(0),
        _ => throw new InvalidOperationException($"Pas d'exemple pour {type}"),
    };
}
