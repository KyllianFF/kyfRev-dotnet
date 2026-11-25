using System.Text;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>Empreintes, écriture atomique (T15) et verrou du coffre.</summary>
[TestClass]
public sealed class FileSafetyTests
{
    [TestMethod]
    public void Fingerprint_is_sha256_of_the_raw_bytes() =>
        Assert.AreEqual("sha256:b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", Fingerprint.Of("hello world"u8));

    [TestMethod]
    public void Replacing_writes_the_new_content_and_leaves_no_temporary_file()
    {
        using var vault = TemporaryVault.Empty();
        var path = vault.PathOf("fiche.md");
        AtomicFile.Replace(path, "première"u8);
        AtomicFile.Replace(path, "seconde"u8);
        Assert.AreEqual("seconde", File.ReadAllText(path));
        Assert.HasCount(1, Directory.GetFiles(vault.Root));
    }

    [TestMethod]
    public void Creating_never_overwrites()
    {
        using var vault = TemporaryVault.Empty();
        var path = vault.PathOf("fiche.md");
        Assert.IsTrue(AtomicFile.TryCreateNew(path, "original"u8));
        Assert.IsFalse(AtomicFile.TryCreateNew(path, "écrasement"u8));
        Assert.AreEqual("original", File.ReadAllText(path));
        Assert.HasCount(1, Directory.GetFiles(vault.Root));
    }

    [TestMethod]
    public void A_file_created_meanwhile_by_another_program_is_never_overwritten()
    {
        using var vault = TemporaryVault.Empty();
        var path = vault.PathOf("fiche.md");

        Assert.IsFalse(AtomicFile.TryCreateNew(path, "nouveau"u8, new(BeforeCommit: () => File.WriteAllText(path, "créé ailleurs"))));

        Assert.AreEqual("créé ailleurs", File.ReadAllText(path));
        Assert.HasCount(1, Directory.GetFiles(vault.Root), "le temporaire est retiré");
    }

    [TestMethod]
    public void A_file_held_open_for_a_moment_is_replaced_once_released()
    {
        using var vault = TemporaryVault.Empty();
        var path = vault.PathOf("fiche.md");
        AtomicFile.Replace(path, "ancienne version"u8);
        // Comme un antivirus ou un indexeur : le fichier est ouvert, sans partage en suppression,
        // puis libéré pendant l'attente qui précède la nouvelle tentative.
        using var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var retries = 0;

        AtomicFile.Replace(path, "nouvelle version"u8, new(BeforeRetry: () =>
        {
            retries++;
            holder.Dispose();
        }));

        Assert.AreEqual(1, retries);
        Assert.AreEqual("nouvelle version", File.ReadAllText(path));
        Assert.HasCount(1, Directory.GetFiles(vault.Root));
    }

    [TestMethod]
    public void A_file_that_stays_held_is_left_unchanged_and_no_temporary_remains()
    {
        using var vault = TemporaryVault.Empty();
        var path = vault.PathOf("fiche.md");
        AtomicFile.Replace(path, "ancienne version"u8);

        Exception error;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            error = Assert.Throws<Exception>(() => AtomicFile.Replace(path, "nouvelle version"u8));
        }

        Assert.IsTrue(error is IOException or UnauthorizedAccessException, error.GetType().Name);
        Assert.AreEqual("ancienne version", File.ReadAllText(path));
        Assert.HasCount(1, Directory.GetFiles(vault.Root), "le temporaire est retiré");
    }

    [TestMethod]
    public void A_written_file_does_not_carry_the_temporary_mark_that_sync_services_skip()
    {
        using var vault = TemporaryVault.Empty();
        var replaced = vault.PathOf("fiche.md");
        var created = vault.PathOf("autre.md");

        AtomicFile.Replace(replaced, "contenu"u8);
        Assert.IsTrue(AtomicFile.TryCreateNew(created, "contenu"u8));

        foreach (var path in new[] { replaced, created })
        {
            Assert.IsFalse(File.GetAttributes(path).HasFlag(FileAttributes.Temporary), path);
        }
    }

    [TestMethod]
    public void Files_marked_temporary_by_an_earlier_version_are_released_without_changing_them()
    {
        using var vault = TemporaryVault.Empty();
        using var outside = TemporaryVault.Empty();
        var when = new DateTime(2026, 11, 8, 14, 0, 0, DateTimeKind.Utc);
        string Marked(string relative, FileAttributes more = 0)
        {
            var path = vault.PathOf(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, relative);
            File.SetLastWriteTimeUtc(path, when);
            File.SetAttributes(path, FileAttributes.Archive | FileAttributes.Temporary | more);
            return path;
        }
        var settings = Marked(".suivi/coffre.json");
        var sheet = Marked("fiches/Réseau/Câblage.md", FileAttributes.ReadOnly);
        var hidden = Marked("fiches/Réseau/note.md", FileAttributes.Hidden);
        var writing = Marked($".suivi/fiches/{AtomicFile.TemporaryPrefix}ABC123.tmp");
        var plain = vault.PathOf("fiches/sans marque.md");
        File.WriteAllText(plain, "x");
        var elsewhere = outside.PathOf("hors du coffre.md");
        File.WriteAllText(elsewhere, "x");
        File.SetAttributes(elsewhere, FileAttributes.Temporary);
        var link = vault.PathOf("fiches/Lien");
        FileLinks.CreateDirectoryLink(link, outside.Root);

        var (released, refused) = SyncedFiles.Release(vault.Root);
        Directory.Delete(link);

        Assert.AreEqual((3, 0), (released, refused));
        foreach (var path in new[] { settings, sheet, hidden, plain })
        {
            Assert.IsFalse(File.GetAttributes(path).HasFlag(FileAttributes.Temporary), path);
        }
        Assert.IsTrue(File.GetAttributes(sheet).HasFlag(FileAttributes.ReadOnly), "les autres attributs restent");
        Assert.IsTrue(File.GetAttributes(hidden).HasFlag(FileAttributes.Hidden));
        Assert.AreEqual(when, File.GetLastWriteTimeUtc(sheet), "la date ne change pas");
        Assert.AreEqual("fiches/Réseau/Câblage.md", File.ReadAllText(sheet));
        Assert.IsTrue(File.GetAttributes(writing).HasFlag(FileAttributes.Temporary), "une écriture en cours garde le sien");
        Assert.IsTrue(File.GetAttributes(elsewhere).HasFlag(FileAttributes.Temporary), "rien hors du coffre, même par un lien");
        Assert.AreEqual((0, 0), SyncedFiles.Release(vault.Root));
        File.SetAttributes(sheet, FileAttributes.Normal);
    }

    [TestMethod]
    public void A_walk_of_the_disk_enters_folders_but_not_links()
    {
        using var vault = TemporaryVault.Empty();
        using var outside = TemporaryVault.Empty();
        vault.Write("fiches/Réseau/Câblage.md", "x");
        outside.Write("hors du coffre.md", "x");
        var link = vault.PathOf("fiches/Lien");
        FileLinks.CreateDirectoryLink(link, outside.Root);

        var entries = DiskStorage.Instance.AllEntries(vault.Root);
        var listed = DiskStorage.Instance.Entries(vault.PathOf("fiches")).ToDictionary(entry => entry.Name, entry => entry.IsLink);
        Directory.Delete(link);

        Assert.IsFalse(entries.Single(entry => entry.Name == "Câblage.md").IsLink);
        Assert.IsFalse(entries.Single(entry => entry.Name == "Réseau").IsLink);
        Assert.IsTrue(entries.Single(entry => entry.Name == "Lien").IsLink);
        Assert.DoesNotContain(entry => entry.Name == "hors du coffre.md", entries);
        Assert.IsTrue(listed["Lien"]);
        Assert.IsFalse(listed["Réseau"]);
    }

    /// <summary>T15 : une coupure pendant l'écriture laisse l'ancienne version, jamais un fichier tronqué.</summary>
    [TestMethod]
    public void A_crash_during_a_write_leaves_the_previous_version_intact()
    {
        using var vault = TemporaryVault.Empty();
        var path = vault.PathOf("fiche.md");
        AtomicFile.Replace(path, "ancienne version"u8);
        var bigger = Encoding.UTF8.GetBytes(new string('x', 1_000_000));

        Assert.ThrowsExactly<IOException>(() => AtomicFile.Replace(path, bigger, new(BeforeCommit: () => throw new IOException("coupure simulée"))));

        Assert.AreEqual("ancienne version", File.ReadAllText(path));
        Assert.HasCount(1, Directory.GetFiles(vault.Root), "le temporaire est retiré");
    }

    [TestMethod]
    public void A_temporary_left_by_a_real_crash_is_ignored_and_never_replaces_the_file()
    {
        using var vault = TemporaryVault.CopyOfReference();
        // Un temporaire orphelin, comme après un arrêt brutal entre l'écriture et la mise en place.
        File.WriteAllText(vault.PathOf($".suivi/fiches/{AtomicFile.TemporaryPrefix}ABC123.tmp"), "{ tronqué");
        var inspection = Analysis.VaultInspector.Inspect(vault.Root);
        Assert.IsFalse(inspection.HasErrors);
        Assert.DoesNotContain(f => f.Path?.Contains(AtomicFile.TemporaryPrefix, StringComparison.Ordinal) == true, inspection.Findings);
    }

    [TestMethod]
    public void The_lock_excludes_a_second_opening_and_names_the_holder()
    {
        using var vault = TemporaryVault.Empty();
        Directory.CreateDirectory(vault.PathOf(".suivi"));
        var since = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

        using (var first = VaultLock.TryAcquire(vault.Root, since, out var noHolder))
        {
            Assert.IsNotNull(first);
            Assert.IsNull(noHolder);

            using var second = VaultLock.TryAcquire(vault.Root, since, out var holder);
            Assert.IsNull(second);
            Assert.IsNotNull(holder);
            Assert.AreEqual(Environment.ProcessId, holder.Process);
            Assert.AreEqual(since, holder.Since);
        }

        using var again = VaultLock.TryAcquire(vault.Root, since, out _);
        Assert.IsNotNull(again, "le verrou est libéré à la fermeture");
        Assert.IsFalse(File.ReadAllText(vault.PathOf(".suivi/verrou-info.json")).EndsWith('\n'));
    }

    [TestMethod]
    public void A_lock_whose_info_cannot_be_written_is_given_back()
    {
        using var vault = TemporaryVault.Empty();
        Directory.CreateDirectory(vault.PathOf(".suivi"));
        var since = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        // Un dossier à la place de verrou-info.json : il ne peut pas être remplacé.
        Directory.CreateDirectory(vault.PathOf(".suivi/verrou-info.json"));

        // L'écriture est refusée : le coffre ne s'ouvre pas.
        Assert.Throws<SystemException>(() => VaultLock.TryAcquire(vault.Root, since, out _));

        Directory.Delete(vault.PathOf(".suivi/verrou-info.json"));
        using var again = VaultLock.TryAcquire(vault.Root, since, out var holder);
        Assert.IsNotNull(again, "le verrou de l'essai manqué a été rendu : le coffre ne se dit pas ouvert par cette fenêtre");
        Assert.IsNull(holder);
    }

    [TestMethod]
    public void An_unreadable_lock_info_does_not_hide_that_the_vault_is_held()
    {
        using var vault = TemporaryVault.Empty();
        Directory.CreateDirectory(vault.PathOf(".suivi"));
        var since = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        using var first = VaultLock.TryAcquire(vault.Root, since, out _);
        Assert.IsNotNull(first);
        vault.Write(".suivi/verrou-info.json", "{ illisible");

        using var second = VaultLock.TryAcquire(vault.Root, since, out var holder);

        Assert.IsNull(second);
        Assert.IsNull(holder, "détenteur inconnu, mais le coffre reste refusé");
    }
}
