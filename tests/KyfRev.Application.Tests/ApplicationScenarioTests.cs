using KyfRev.Application.Model;
using KyfRev.Application.Tests.Support;
using KyfRev.Application.UseCases;
using KyfRev.Infrastructure.Vault;
using Microsoft.Extensions.Logging;

namespace KyfRev.Application.Tests;

/// <summary>
/// Scénarios applicatifs avec une horloge simulée (fuseau UTC+2), passage de minuit compris :
/// ouverture du coffre (préparation de la journée, sauvegarde quotidienne, rotation), et la date
/// du jour toujours prise dans le fuseau local.
/// </summary>
[TestClass]
public sealed class ApplicationScenarioTests
{
    [TestMethod]
    public void The_day_is_the_local_one()
    {
        var present = Present.Of(new FakeTime(new DateTimeOffset(2026, 10, 31, 22, 30, 0, TimeSpan.Zero), FakeTime.PlusTwo));

        Assert.AreEqual(new DateOnly(2026, 11, 1), present.Now.Day);
        Assert.AreEqual(new DateTimeOffset(2026, 10, 31, 22, 30, 0, TimeSpan.Zero), present.Now.Instant);
        Assert.AreEqual(new DateTime(2026, 11, 1, 0, 30, 0), present.Local);
    }

    [TestMethod]
    public void Opening_prepares_the_day_backs_up_once_a_day_and_remembers_the_vault()
    {
        using var folder = new TestFolder();
        using (var first = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0)))
        {
            var state = first.App.Vaults.Create(folder.Vault);

            Assert.IsNull(state.OpenError);
            Assert.AreEqual(folder.Vault, state.Vault);
            Assert.AreEqual("1.0.0-essai", state.Version);
            Assert.AreEqual(folder.Vault, first.Locations.Remembered.Single());
            Assert.AreEqual("coffre-2026-10-01T09-00-00.zip", BackupNames(folder).Single());
            Assert.IsTrue(first.Logs.Entries.Any(e => e.Message.StartsWith("Journée du 2026-10-01 préparée", StringComparison.Ordinal)));

            // Rouvrir le coffre ouvert ne fait rien : son verrou est le nôtre.
            first.Time.Advance(TimeSpan.FromHours(2));
            Assert.IsNull(first.App.Vaults.Open(folder.Vault).OpenError);
            Assert.HasCount(1, first.Locations.Remembered);
        }

        using (var sameDay = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 18, 0)))
        {
            Assert.IsNull(sameDay.App.Vaults.Open(folder.Vault).OpenError);
            Assert.HasCount(1, BackupNames(folder));
        }

        using var nextDay = new TestApplication(FakeTime.AtLocal(2026, 10, 2, 0, 5));
        Assert.IsNull(nextDay.App.Vaults.Open(folder.Vault).OpenError);
        Assert.AreEqual("coffre-2026-10-01T09-00-00.zip coffre-2026-10-02T00-05-00.zip", string.Join(' ', BackupNames(folder)));
    }

    [TestMethod]
    public void Midnight_starts_a_new_day_for_every_use_case()
    {
        using var folder = new TestFolder();
        using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 23, 59, 30));
        var app = test.App;
        app.Vaults.Create(folder.Vault);
        var id = app.Sheets.Create(null, "Mitose", "Cours").Value;

        // J0 est une révision : une seule par jour, jusqu'à minuit.
        Assert.IsFalse(app.Reviews.Validate(ItemKind.Sheet, id).Succeeded);
        Assert.IsTrue(app.Reminders.PrepareDay().Value.AlreadyPrepared);

        test.Time.Advance(TimeSpan.FromMinutes(1));
        var state = app.Reviews.Validate(ItemKind.Sheet, id).Value;

        Assert.AreEqual("J3", state.Step);
        Assert.AreEqual(new DateOnly(2026, 10, 2), state.LastReview);
        Assert.IsTrue(state.ReviewedToday);
        Assert.AreEqual(new CycleStep("J1", new DateOnly(2026, 10, 2), 0), state.Steps[1]);
        var day = app.Reminders.PrepareDay().Value;
        Assert.AreEqual(new DateOnly(2026, 10, 2), day.Day);
        Assert.IsFalse(day.AlreadyPrepared);
        Assert.IsTrue(app.Reminders.PrepareDay().Value.AlreadyPrepared);
        var sheet = app.Sheets.Open(id).Value;
        Assert.StartsWith("# Mitose\n\n## Objectifs", sheet.Body.TrimStart('\n'));
        Assert.IsTrue(sheet.Review.ReviewedToday);

        var exports = app.Exports.Export(ExportKind.Tracking, folder.Root).Value;
        Assert.IsTrue(exports.All(file => file.Contains("2026-10-02", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void A_failed_opening_keeps_the_open_vault()
    {
        using var folder = new TestFolder();
        using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0));
        Directory.CreateDirectory(folder.PathOf("Pas un coffre"));

        var refused = test.App.Vaults.Open(folder.PathOf("Pas un coffre"));
        Assert.IsNull(refused.Vault);
        Assert.IsNotNull(refused.OpenError);

        test.App.Vaults.Create(folder.Vault);
        var again = test.App.Vaults.Open(folder.PathOf("Pas un coffre"));
        Assert.AreEqual(refused.OpenError, again.OpenError);
        Assert.AreEqual(folder.Vault, again.Vault);
        Assert.AreEqual(new AppState("1.0.0-essai", AppMode.Development, folder.Vault, null, null), test.App.Vaults.State());
    }

    [TestMethod]
    public void A_vault_that_cannot_be_remembered_stays_open_and_is_logged()
    {
        using var folder = new TestFolder();
        using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0));
        test.Locations.Unwritable = true;

        var state = test.App.Vaults.Create(folder.Vault);

        Assert.AreEqual(folder.Vault, state.Vault);
        Assert.IsTrue(test.Logs.Warnings.Any(message => message.Contains("pas retenu", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void At_startup_the_last_vault_is_reopened()
    {
        using var folder = new TestFolder();
        using (var first = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0)))
        {
            Assert.AreEqual(new AppState("1.0.0-essai", AppMode.Development, null, null, null), first.App.Vaults.OpenAtStartup());
            first.App.Vaults.Create(folder.Vault);
        }
        using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 2, 9, 0));
        test.Locations.Remembered.Add(folder.Vault);

        Assert.AreEqual(new AppState("1.0.0-essai", AppMode.Development, folder.Vault, null, null), test.App.Vaults.OpenAtStartup());
    }

    [TestMethod]
    public void At_startup_a_planned_vault_still_to_create_is_left_to_the_welcome_page()
    {
        using var folder = new TestFolder();
        using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0));
        test.Locations.Mode = AppMode.Installed;
        test.Locations.Planned = folder.Vault;

        var state = test.App.Vaults.OpenAtStartup();

        Assert.AreEqual(new AppState("1.0.0-essai", AppMode.Installed, null, folder.Vault, null), state);
        Assert.IsFalse(Directory.Exists(folder.Vault), "rien n'est créé sans action de l'utilisateur");

        var created = test.App.Vaults.Create(folder.Vault);
        test.Locations.PlannedExists = true;
        Assert.AreEqual(new AppState("1.0.0-essai", AppMode.Installed, folder.Vault, null, null), created);
    }

    [TestMethod]
    public void At_startup_a_vault_that_cannot_be_opened_is_reported_to_the_welcome_page()
    {
        using var folder = new TestFolder();
        using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0));
        test.Locations.Mode = AppMode.Portable;
        test.Locations.Planned = folder.Vault;
        test.Locations.PlannedExists = true;
        Directory.CreateDirectory(folder.Vault);

        var state = test.App.Vaults.OpenAtStartup();

        Assert.IsNull(state.Vault);
        Assert.IsNull(state.VaultToCreate);
        Assert.IsNotNull(state.OpenError);
        Assert.AreEqual(AppMode.Portable, state.Mode);
    }

    [TestMethod]
    public void The_previous_vault_is_closed_once_the_new_one_is_open()
    {
        using var folder = new TestFolder();
        using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0));
        test.App.Vaults.Create(folder.PathOf("A", "Coffre"));

        var state = test.App.Vaults.Create(folder.PathOf("B", "Coffre"));

        Assert.AreEqual(folder.PathOf("B", "Coffre"), state.Vault);
        using var reopened = VaultSession.Open(folder.PathOf("A", "Coffre"), Present.Of(test.Time).Now);
        Assert.AreEqual(VaultErrorKind.AlreadyOpen, Assert.ThrowsExactly<VaultException>(() => VaultSession.Open(folder.PathOf("B", "Coffre"), Present.Of(test.Time).Now)).Kind);
    }

    [TestMethod]
    public void A_programming_error_while_opening_leaves_no_vault_locked()
    {
        using var folder = new TestFolder();
        using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0));
        test.Locations.Failure = new InvalidOperationException("mémoire indisponible");

        Assert.ThrowsExactly<InvalidOperationException>(() => test.App.Vaults.Create(folder.Vault));

        Assert.IsNull(test.App.Vaults.State().Vault);
        using var reopened = VaultSession.Open(folder.Vault, Present.Of(test.Time).Now);
    }

    [TestMethod]
    public void Failures_while_opening_are_logged_and_never_block()
    {
        using var folder = new TestFolder();
        using (var first = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0)))
        {
            first.App.Vaults.Create(folder.Vault);
        }
        File.WriteAllText(Path.Combine(folder.Vault, ".suivi", "rappels.json"), "{ abîmé");
        Directory.CreateDirectory(Path.Combine(folder.Backups, ".suivi"));

        using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 2, 9, 0));
        Assert.IsNull(test.App.Vaults.Open(folder.Vault).OpenError);

        var warnings = test.Logs.Warnings.ToList();
        Assert.IsTrue(warnings.Any(w => w.StartsWith("Préparation de la journée impossible", StringComparison.Ordinal)), string.Join('\n', warnings));
        Assert.IsTrue(warnings.Any(w => w.StartsWith("Rotation impossible", StringComparison.Ordinal)), string.Join('\n', warnings));
        Assert.HasCount(2, BackupNames(folder));
    }

    [TestMethod]
    public void An_unreadable_backup_folder_or_vault_file_is_logged()
    {
        using var folder = new TestFolder();
        using (var first = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0)))
        {
            first.App.Vaults.Create(folder.Vault);
        }
        Directory.Delete(folder.Backups, recursive: true);
        File.WriteAllText(folder.Backups, "un fichier, pas un dossier");
        using (var test = new TestApplication(FakeTime.AtLocal(2026, 10, 2, 9, 0)))
        {
            Assert.IsNull(test.App.Vaults.Open(folder.Vault).OpenError);
            Assert.IsTrue(test.Logs.Warnings.Any(w => w.StartsWith("Dossier des sauvegardes illisible", StringComparison.Ordinal)));
        }

        File.Delete(folder.Backups);
        File.WriteAllText(Path.Combine(folder.Vault, "fiches", "image.png"), "x");
        using var locked = new FileStream(Path.Combine(folder.Vault, "fiches", "image.png"), FileMode.Open, FileAccess.Read, FileShare.None);
        using var blocked = new TestApplication(FakeTime.AtLocal(2026, 10, 3, 9, 0));
        Assert.IsNull(blocked.App.Vaults.Open(folder.Vault).OpenError);
        Assert.IsTrue(blocked.Logs.Warnings.Any(w => w.StartsWith("Sauvegarde impossible", StringComparison.Ordinal)));
        Assert.AreEqual(LogLevel.Warning, blocked.Logs.Entries.Single(e => e.Message.StartsWith("Sauvegarde", StringComparison.Ordinal)).Level);
    }

    private static string[] BackupNames(TestFolder folder) =>
        [.. Directory.GetFiles(folder.Backups).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
}
