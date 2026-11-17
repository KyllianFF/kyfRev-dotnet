using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Samples;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// Le coffre d'exemple de l'accueil, et le coffre d'essai complet de <c>tools/coffre-essai</c> : créés par les vrais cas
/// d'usage, ils s'ouvrent comme n'importe quel coffre, sans rien à signaler dans « Santé du coffre ».
/// </summary>
[TestClass]
public sealed class SampleVaultTests
{
    private static readonly string[] Subjects = ["Anglais", "Biologie", "Histoire", "Mathématiques", "Philosophie", "Réseaux"];
    private static readonly string[] Mine = ["notes.txt"];

    [TestMethod]
    public void The_sample_of_the_welcome_has_ten_sheets_in_six_subjects_and_something_to_review_today()
    {
        using var place = new Place();
        var created = SampleVault.Create(place.Root, DateOnly.FromDateTime(DateTime.Now));

        Assert.IsTrue(created.Succeeded, created.Error);
        Assert.AreEqual(10, created.Value.Sheets);
        Assert.IsGreaterThan(0, created.Value.Marks, "des passages surlignés et des définitions");
        Assert.AreEqual(1, created.Value.Schemas, "le schéma que cite la fiche de réseaux");
        Assert.AreEqual(6, created.Value.Quotes);
        var subjects = Directory.GetDirectories(Path.Combine(place.Root, "fiches")).Select(Path.GetFileName).ToList();
        CollectionAssert.AreEquivalent(Subjects, subjects);

        using var app = Open(place.Root);
        var board = app.Reminders.Dashboard().Value;
        Assert.AreEqual(10, board.ActiveSheets + board.ArchivedSheets);
        Assert.IsGreaterThan(0, board.Overdue.Length, "du retard à rattraper");
        Assert.IsGreaterThan(0, board.Today.Length, "des révisions du jour");
        Assert.IsGreaterThan(0, board.Upcoming.Length, "et d'autres dans la semaine");
        Assert.IsGreaterThan(0, board.RecentReviews.Sum(day => day.Reviews), "une semaine déjà vécue");
        var health = app.Maintenance.Analyze().Value;
        Assert.AreEqual(0, health.Problems, $"{health.BrokenLinks.Count} lien(s) cassé(s), {health.AnnotationsToCheck.Count + health.OrphanAnnotations.Count} passage(s) à vérifier");
        var settings = app.Settings.Get().Value;
        Assert.IsTrue(settings.QuotesActive && settings.SchemasActive);
        Assert.IsFalse(settings.QuotesOnline, "aucune connexion : les citations sont dans le coffre");
    }

    [TestMethod]
    public void A_sample_is_never_written_over_something()
    {
        using var place = new Place();
        Assert.IsTrue(SampleVault.CanCreateIn(place.Root), "un dossier absent");
        Directory.CreateDirectory(place.Root);
        Assert.IsTrue(SampleVault.CanCreateIn(place.Root), "un dossier vide");
        File.WriteAllText(Path.Combine(place.Root, "notes.txt"), "à moi");

        Assert.IsFalse(SampleVault.CanCreateIn(place.Root));
        var refused = SampleVault.Create(place.Root, new DateOnly(2026, 11, 17));
        Assert.IsFalse(refused.Succeeded);
        Assert.AreEqual(SampleVault.FolderNotEmpty, refused.Error);
        CollectionAssert.AreEqual(Mine, Directory.GetFileSystemEntries(place.Root).Select(Path.GetFileName).ToArray(), "rien n'y est écrit");
        Assert.ThrowsExactly<ArgumentException>(() => SampleVault.Create(" ", new DateOnly(2026, 11, 17)));
    }

    [TestMethod]
    public void A_folder_that_cannot_be_written_says_why()
    {
        using var place = new Place();
        // Un fichier à la place du dossier parent : le coffre ne peut pas y être créé.
        Directory.CreateDirectory(Path.GetDirectoryName(place.Root)!);
        var blocked = Path.Combine(Path.GetDirectoryName(place.Root)!, "fichier");
        File.WriteAllText(blocked, "pas un dossier");

        var refused = SampleVault.Create(Path.Combine(blocked, "Coffre"), new DateOnly(2026, 11, 17));

        Assert.IsFalse(refused.Succeeded);
        Assert.IsFalse(string.IsNullOrWhiteSpace(refused.Error));
    }

    [TestMethod]
    public void The_full_trial_vault_lives_five_months_and_stays_healthy()
    {
        using var place = new Place();
        // Un autre jour que celui du poste : les dates suivent le jour donné.
        var day = new DateOnly(2026, 6, 15);
        var created = SampleVault.Create(place.Root, day, everything: true);

        Assert.IsTrue(created.Succeeded, created.Error);
        Assert.AreEqual(26, created.Value.Sheets);
        Assert.AreEqual(3, created.Value.Schemas);
        using var vault = VaultSession.Open(place.Root, new Moment(day, new DateTimeOffset(day, new TimeOnly(19, 0), TimeSpan.Zero)));
        var board = vault.Dashboard(day);
        Assert.IsGreaterThan(0, board.ArchivedSheets, "des cycles finis");
        Assert.IsGreaterThan(0, board.Overdue.Length + board.Today.Length);
        Assert.IsTrue(File.Exists(Path.Combine(place.Root, "fiches", "Droit", "La responsabilité civile.md")));
    }

    private static KyfRevApplication Open(string root)
    {
        var app = new KyfRevApplication(new VaultStore(), new BackupFiles(), new Nowhere(), TimeProvider.System, NullLoggerFactory.Instance, "essai");
        Assert.IsNotNull(app.Vaults.Open(root).Vault);
        return app;
    }

    /// <summary>Un dossier à part pour un coffre et les sauvegardes rangées à côté de lui ; tout est effacé à la fin.</summary>
    private sealed class Place : IDisposable
    {
        private readonly string _parent = Path.Combine(Path.GetTempPath(), "kyfrev-exemple-" + Guid.NewGuid().ToString("N"));

        public string Root => Path.Combine(_parent, "Coffre");

        public void Dispose()
        {
            if (Directory.Exists(_parent))
            {
                Directory.Delete(_parent, recursive: true);
            }
        }
    }

    private sealed class Nowhere : IVaultLocations
    {
        public AppMode Mode => AppMode.Portable;

        public string? VaultToOpen() => null;

        public string? VaultToCreate() => null;

        public bool Remember(string root) => true;
    }
}
