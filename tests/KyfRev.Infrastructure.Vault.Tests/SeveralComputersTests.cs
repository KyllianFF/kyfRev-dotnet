using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// Un coffre ouvert sur deux postes, dans un dossier synchronisé. L'autre poste est une copie du coffre, ouverte de son
/// côté ; ce qu'il écrit revient dans le coffre de ce poste comme la synchronisation le ramènerait, fichier par fichier,
/// pendant que ce poste a le coffre ouvert. Ce qu'il y a écrit n'est jamais effacé par ce que ce poste écrit ensuite.
/// </summary>
[TestClass]
public sealed class SeveralComputersTests
{
    private static readonly Moment Morning = On(new DateOnly(2026, 11, 2));
    private static readonly string[] Exam = ["examen"];
    private static readonly string[] BothExams = ["Partiel de biologie", "Partiel d'histoire"];

    [TestMethod]
    public void A_review_done_on_the_other_computer_survives_what_this_one_writes_next()
    {
        using var pair = new Pair();
        var id = pair.Before(vault => vault.CreateSheet("Biologie", "Mitose", "# Mitose\n", Morning));
        using var here = pair.OpenHere();

        // Sur l'autre poste, le J1 de la fiche est validé ; son suivi arrive ici.
        var nextDay = On(new DateOnly(2026, 11, 3));
        pair.OnTheOther(vault => vault.ApplyReview(id, tracking => tracking.ValidateReview(nextDay, null, vault.Settings.Reminders)));
        pair.Sync($"{VaultLayout.SheetTracking}/{id}.json");
        var reviewed = pair.Here.ReadText($"{VaultLayout.SheetTracking}/{id}.json");

        // Ici, la fiche est écrite, puis ses étiquettes changent : son suivi est réécrit, avec la révision de l'autre poste.
        var read = here.ReadSheet(id);
        here.SaveSheet(id, read.Text + "Ajouté ici.\n", read.Fingerprint, 1, nextDay);
        here.SetTags(id, ["examen"], nextDay);

        var history = here.Sheet(id).Sheet.Tracking.History;
        Assert.AreEqual(1, here.Dashboard(nextDay.Day).RecentReviews.Sum(day => day.Reviews), "la révision faite là-bas compte ici");
        Assert.AreNotEqual(reviewed, pair.Here.ReadText($"{VaultLayout.SheetTracking}/{id}.json"), "le suivi a bien été réécrit ici");
        var onDisk = TrackedSheetFile.Read(pair.Here.ReadBytes($"{VaultLayout.SheetTracking}/{id}.json"));
        Assert.HasCount(history.Length, onDisk.Tracking.History, "et sa révision y est encore");
        CollectionAssert.AreEqual(Exam, onDisk.Tags.ToArray());

        // Une seconde révision ici s'ajoute à celle de là-bas, au lieu de repartir du suivi d'avant.
        var later = On(new DateOnly(2026, 11, 6));
        here.ApplyReview(id, tracking => tracking.ValidateReview(later, null, here.Settings.Reminders));
        Assert.AreEqual(2, here.Dashboard(later.Day).RecentReviews.Sum(day => day.Reviews));
    }

    [TestMethod]
    public void Passages_written_on_both_computers_are_all_kept()
    {
        using var pair = new Pair();
        var id = pair.Before(vault => vault.CreateSheet("Biologie", "Mitose", "La prophase précède la métaphase.\n", Morning));
        using var here = pair.OpenHere();
        _ = here.Annotations(id);

        var there = pair.OnTheOther(vault => vault.CreateAnnotation(id, Anchor("prophase"), "vu là-bas", Morning));
        pair.Sync($"{VaultLayout.Annotations}/{id}.json");

        var mine = here.CreateAnnotation(id, Anchor("métaphase"), "vu ici", Morning);

        CollectionAssert.AreEquivalent(new[] { there, mine }, here.Annotations(id).Select(annotation => annotation.Id).ToArray());
        var onDisk = AnnotationsFile.Read(pair.Here.ReadBytes($"{VaultLayout.Annotations}/{id}.json"));
        CollectionAssert.AreEquivalent(new[] { there, mine }, onDisk.Annotations.Select(annotation => annotation.Id).ToArray());
    }

    [TestMethod]
    public void An_exam_set_on_the_other_computer_stays_when_this_one_sets_another()
    {
        using var pair = new Pair();
        pair.Before(vault => vault.CreateSheet("Biologie", "Mitose", "# Mitose\n", Morning));
        using var here = pair.OpenHere();

        pair.OnTheOther(vault => vault.DefineExam("Biologie", "Partiel de biologie", new DateOnly(2026, 12, 15)));
        pair.Sync(VaultLayout.SettingsFile);

        here.DefineExam("Histoire", "Partiel d'histoire", new DateOnly(2026, 12, 18));
        here.WithdrawExam("Géographie");

        CollectionAssert.AreEqual(BothExams, here.Settings.Exams.Select(exam => exam.Name).ToArray());
        CollectionAssert.AreEqual(
            BothExams,
            VaultSettingsFile.Read(pair.Here.ReadBytes(VaultLayout.SettingsFile)).Exams.Select(exam => exam.Name).ToArray());
    }

    [TestMethod]
    public void A_file_that_cannot_be_read_now_leaves_this_computer_as_it_was()
    {
        using var pair = new Pair();
        var id = pair.Before(vault => vault.CreateSheet("Biologie", "Mitose", "La prophase.\n", Morning));
        using var here = pair.OpenHere();
        var first = here.CreateAnnotation(id, Anchor("prophase"), null, Morning);

        // Arrivé abîmé (une synchronisation pas finie) : ce poste garde ce qu'il sait, et l'écrit.
        pair.Here.Write($"{VaultLayout.Annotations}/{id}.json", "{ abîmé");
        pair.Here.Write($"{VaultLayout.SheetTracking}/{id}.json", "{ abîmé");
        pair.Here.Write(VaultLayout.SettingsFile, "{ abîmé");
        var second = here.CreateAnnotation(id, Anchor("La"), null, Morning);
        here.ApplyToTracking(id, tracking => tracking.Postpone(Morning, new DateOnly(2026, 11, 5)));
        here.DefineExam("Biologie", "Partiel", new DateOnly(2026, 12, 15));

        CollectionAssert.AreEquivalent(new[] { first, second }, AnnotationsFile.Read(pair.Here.ReadBytes($"{VaultLayout.Annotations}/{id}.json")).Annotations.Select(annotation => annotation.Id).ToArray());
        Assert.AreEqual(id, TrackedSheetFile.Read(pair.Here.ReadBytes($"{VaultLayout.SheetTracking}/{id}.json")).Id);
        Assert.HasCount(1, VaultSettingsFile.Read(pair.Here.ReadBytes(VaultLayout.SettingsFile)).Exams);
    }

    private static Moment On(DateOnly day) => new(day, new DateTimeOffset(day, new TimeOnly(8, 0), TimeSpan.Zero));

    private static StoredAnchor Anchor(string exact) =>
        new(exact, string.Empty, string.Empty, 0, exact.Length, [], null, "sha256:…", UnknownProperties.None);

    /// <summary>Le coffre de ce poste, et sa copie sur l'autre poste, faite pendant que le coffre était fermé.</summary>
    private sealed class Pair : IDisposable
    {
        public TemporaryVault Here { get; } = TemporaryVault.Empty();

        public TemporaryVault There { get; } = TemporaryVault.Empty();

        /// <summary>Prépare le coffre, puis le copie sur l'autre poste.</summary>
        public T Before<T>(Func<VaultSession, T> prepare)
        {
            T result;
            using (var vault = Here.Initialize(Morning))
            {
                result = prepare(vault);
            }
            foreach (var file in Directory.EnumerateFiles(Here.Root, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(There.Root, Path.GetRelativePath(Here.Root, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
            return result;
        }

        public VaultSession OpenHere() => Here.Open(Morning);

        /// <summary>Ouvre le coffre de l'autre poste, y fait quelque chose, puis le ferme.</summary>
        public T OnTheOther<T>(Func<VaultSession, T> act)
        {
            using var vault = There.Open(Morning);
            return act(vault);
        }

        public void OnTheOther(Action<VaultSession> act) => OnTheOther(vault =>
        {
            act(vault);
            return true;
        });

        /// <summary>La synchronisation ramène ici un fichier écrit sur l'autre poste.</summary>
        public void Sync(string relative) => File.Copy(There.PathOf(relative), Here.PathOf(relative), overwrite: true);

        public void Dispose()
        {
            Here.Dispose();
            There.Dispose();
        }
    }
}
