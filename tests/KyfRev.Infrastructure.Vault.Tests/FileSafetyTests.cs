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

        Assert.IsFalse(AtomicFile.TryCreateNew(path, "nouveau"u8, () => File.WriteAllText(path, "créé ailleurs")));

        Assert.AreEqual("créé ailleurs", File.ReadAllText(path));
        Assert.HasCount(1, Directory.GetFiles(vault.Root), "le temporaire est retiré");
    }

    [TestMethod]
    public void A_file_held_open_for_a_moment_is_replaced_once_released()
    {
        using var vault = TemporaryVault.Empty();
        var path = vault.PathOf("fiche.md");
        AtomicFile.Replace(path, "ancienne version"u8);
        // Comme un antivirus ou un indexeur : le fichier est ouvert un instant, sans partage en suppression.
        using var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var release = new Timer(_ => holder.Dispose(), state: null, dueTime: 100, period: Timeout.Infinite);

        AtomicFile.Replace(path, "nouvelle version"u8);

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

    /// <summary>T15 : une coupure pendant l'écriture laisse l'ancienne version, jamais un fichier tronqué.</summary>
    [TestMethod]
    public void A_crash_during_a_write_leaves_the_previous_version_intact()
    {
        using var vault = TemporaryVault.Empty();
        var path = vault.PathOf("fiche.md");
        AtomicFile.Replace(path, "ancienne version"u8);
        var bigger = Encoding.UTF8.GetBytes(new string('x', 1_000_000));

        Assert.ThrowsExactly<IOException>(() => AtomicFile.Replace(path, bigger, () => throw new IOException("coupure simulée")));

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
