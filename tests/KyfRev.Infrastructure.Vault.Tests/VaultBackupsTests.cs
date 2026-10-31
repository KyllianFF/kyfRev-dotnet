using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Backups;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>Défenses des sauvegardes, des exports et du partage que les scénarios n'exercent pas.</summary>
[TestClass]
public sealed class VaultBackupsTests
{
    private static readonly Moment Now = new(new DateOnly(2026, 10, 5), new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
    private static readonly DateTime Stamp = new(2026, 10, 5, 10, 0, 0);
    private static readonly byte[] Settings = Encoding.UTF8.GetBytes("{}");
    private static readonly string[] TrackingExports = ["kyfRev - suivi 2026-10-05.csv", "kyfRev - historique 2026-10-05.csv"];
    private static readonly string[] SharedEntries = ["Carte.md", "images/carte.png", "images/2-carte.png"];

    [TestMethod]
    [DataRow("coffre-2026-10-09T18-30-05.zip", "2026-10-09T18:30:05")]
    [DataRow("coffre-2026-10-09T18-30-05-12.zip", "2026-10-09T18:30:05")]
    [DataRow("coffre-2026-10-09T+1-30-05.zip", "2026-10-09T01:30:05")]
    [DataRow("coffre-2026-10-09T18-30-05-.zip", null)]
    [DataRow("coffre-2026-10-09T18-30-05-1a.zip", null)]
    [DataRow("coffre-2026-13-09T18-30-05.zip", null)]
    [DataRow("coffre-2026-10-09T24-00-00.zip", null)]
    [DataRow("coffre-2026-10-09T18-60-00.zip", null)]
    [DataRow("coffre-2026-10-09T-1-30-05.zip", null)]
    [DataRow("coffre-2026-10-09 18-30-05.zip", null)]
    [DataRow("coffre-2026-10-09T18-30.zip", null)]
    [DataRow("coffre-2026-10-09T18-30-0é.zip", null)]
    [DataRow("coffre-2026-10-09T18-30-05.ZIP", null)]
    [DataRow("Coffre-2026-10-09T18-30-05.zip", null)]
    [DataRow("coffre-.zip", null)]
    public void Only_exact_backup_names_carry_a_timestamp(string name, string? expected) =>
        Assert.AreEqual(expected, VaultBackups.TimestampOf(name)?.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));

    [TestMethod]
    public void A_backup_is_verified_and_leaves_no_temporary_file()
    {
        using var folder = TemporaryVault.Empty();
        using var outside = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        vault.CreateSheet("Biologie", "Mitose", "Texte.\n", Now);

        var backup = vault.Backup(outside.PathOf("sauvegardes"), Stamp);

        Assert.AreEqual("coffre-2026-10-05T10-00-00.zip", Path.GetFileName(backup.Path));
        Assert.AreEqual(Stamp, backup.Timestamp);
        CollectionAssert.AreEqual(new[] { Path.GetFileName(backup.Path) }, Directory.GetFiles(outside.PathOf("sauvegardes")).Select(Path.GetFileName).ToArray());
        using var archive = ZipFile.OpenRead(backup.Path);
        Assert.HasCount(backup.FileCount, archive.Entries);
        Assert.IsFalse(archive.Entries.Any(e => e.FullName.StartsWith(".suivi/verrou", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void A_backup_needs_a_folder_it_can_create()
    {
        using var folder = TemporaryVault.Empty();
        using var outside = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        outside.Write("fichier", "x");
        Assert.AreEqual(VaultErrorKind.Io, Assert.ThrowsExactly<VaultException>(() => vault.Backup(outside.PathOf("fichier"), Stamp)).Kind);
    }

    [TestMethod]
    public void Rotation_keeps_everything_when_nothing_is_outdated()
    {
        Assert.IsEmpty(VaultBackups.ToKeep([], RotationPolicy.Default));
        using var outside = TemporaryVault.Empty();
        outside.Write(VaultBackups.FileName(Stamp), "x");
        outside.Write(VaultBackups.FileName(Stamp.AddDays(-1)), "x");
        Assert.IsEmpty(VaultBackups.ApplyRotation(outside.Root, RotationPolicy.Default));
        Assert.IsTrue(VaultBackups.DailyBackupNeeded(outside.Root, new DateOnly(2026, 10, 6)));
        Assert.IsFalse(VaultBackups.DailyBackupNeeded(outside.Root, new DateOnly(2026, 10, 4)));
    }

    [TestMethod]
    public void A_restore_refuses_names_that_leave_the_vault()
    {
        using var outside = TemporaryVault.Empty();
        foreach (var name in new[] { "../hors.txt", "/racine.txt", "C:/Windows/x.txt", "fiches/CON.md" })
        {
            var backup = Zip(outside.PathOf($"{Guid.NewGuid():N}.zip"), (".suivi/coffre.json", Settings), (name, [1]));
            var error = Assert.ThrowsExactly<VaultException>(() => VaultBackups.Restore(backup, outside.PathOf("restaurations"), Now.Day));
            Assert.AreEqual(VaultErrorKind.InvalidBackup, error.Kind, name);
            Assert.Contains("chemin refusé", error.Message);
        }
        Assert.IsFalse(Directory.Exists(outside.PathOf("restaurations")), "rien n'est écrit avant d'avoir tout vérifié");
    }

    [TestMethod]
    public void A_restore_refuses_what_is_not_a_vault_backup()
    {
        using var outside = TemporaryVault.Empty();
        outside.Write("texte.zip", "pas une archive");
        var notZip = Assert.ThrowsExactly<VaultException>(() => VaultBackups.Restore(outside.PathOf("texte.zip"), outside.Root, Now.Day));
        Assert.AreEqual(VaultErrorKind.InvalidBackup, notZip.Kind);

        var withoutSettings = Zip(outside.PathOf("autre.zip"), ("fiches/Note.md", [1]));
        Assert.Contains("coffre.json absent", Assert.ThrowsExactly<VaultException>(() => VaultBackups.Restore(withoutSettings, outside.Root, Now.Day)).Message);

        Assert.AreEqual(VaultErrorKind.Io, Assert.ThrowsExactly<VaultException>(() => VaultBackups.Restore(outside.PathOf("absente.zip"), outside.Root, Now.Day)).Kind);
    }

    [TestMethod]
    public void A_restore_refuses_announced_sizes_that_are_too_large()
    {
        using var outside = TemporaryVault.Empty();
        var backup = Zip(
            outside.PathOf("bombe.zip"),
            [(".suivi/coffre.json", Settings), .. Enumerable.Range(1, 6).Select(n => ($"fiches/{n}.md", new byte[] { 1 }))]);
        Lie(backup, entry => entry.StartsWith("fiches/", StringComparison.Ordinal) ? 0xF0000000 : null);

        var error = Assert.ThrowsExactly<VaultException>(() => VaultBackups.Restore(backup, outside.PathOf("restaurations"), Now.Day));
        Assert.Contains("trop volumineux", error.Message);
        Assert.IsFalse(Directory.Exists(outside.PathOf("restaurations")));
    }

    [TestMethod]
    public void A_restore_refuses_an_entry_larger_than_announced()
    {
        using var outside = TemporaryVault.Empty();
        var backup = Zip(outside.PathOf("menteuse.zip"), (".suivi/coffre.json", Settings), ("fiches/Note.md", Encoding.UTF8.GetBytes("0123456789")));
        Lie(backup, entry => entry == "fiches/Note.md" ? 3u : null);

        var error = Assert.ThrowsExactly<VaultException>(() => VaultBackups.Restore(backup, outside.PathOf("restaurations"), Now.Day));
        Assert.Contains("contenu altéré pour « fiches/Note.md »", error.Message);

        // Annoncée plus longue qu'elle n'est : la taille ne correspond pas.
        var shorter = Zip(outside.PathOf("courte.zip"), (".suivi/coffre.json", Settings), ("fiches/Note.md", Encoding.UTF8.GetBytes("012")));
        Lie(shorter, entry => entry == "fiches/Note.md" ? 10u : null);
        Assert.Contains("taille incohérente", Assert.ThrowsExactly<VaultException>(() => VaultBackups.Restore(shorter, outside.PathOf("restaurations"), Now.Day)).Message);
    }

    [TestMethod]
    [DataRow("", 0x00000000u)]
    [DataRow("a", 0xE8B7BE43u)]
    [DataRow("123456789", 0xCBF43926u)]
    [DataRow("The quick brown fox jumps over the lazy dog", 0x414FA339u)]
    public void Crc32_is_the_zip_checksum(string text, uint expected) =>
        Assert.AreEqual(expected, VaultBackups.Crc32(Encoding.ASCII.GetBytes(text)));

    [TestMethod]
    public void A_restore_skips_folder_entries_and_never_overwrites()
    {
        using var outside = TemporaryVault.Empty();
        var backup = Zip(outside.PathOf("coffre.zip"), (".suivi/coffre.json", Settings), ("fiches/Vide/", []), ("fiches/Note.md", [1]));

        var first = VaultBackups.Restore(backup, outside.PathOf("restaurations"), Now.Day);
        var second = VaultBackups.Restore(backup, outside.PathOf("restaurations"), Now.Day);

        Assert.AreEqual("Coffre (restauré le 2026-10-05)", Path.GetFileName(first));
        Assert.AreEqual("Coffre (restauré le 2026-10-05) (2)", Path.GetFileName(second));
        Assert.IsTrue(File.Exists(Path.Combine(second, "fiches", "Note.md")));
    }

    [TestMethod]
    public void A_missing_sheet_comes_back_from_a_backup_despite_odd_entries()
    {
        using var folder = TemporaryVault.Empty();
        using var outside = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        var id = vault.CreateSheet("Biologie", "Mitose", "Texte.\n", Now);
        vault.AddResource(id, "cellule.png", [1, 2, 3]);
        var backup = vault.Backup(outside.Root, Stamp).Path;
        using (var archive = ZipFile.Open(backup, ZipArchiveMode.Update))
        {
            archive.CreateEntry("fiches/Dossier/");
            archive.CreateEntry("../hors.md");
            using (var latin = archive.CreateEntry("fiches/Latin.md").Open())
            {
                latin.Write([0xE9]);
            }
            using var untracked = archive.CreateEntry("fiches/Sans en-tête.md").Open();
            untracked.Write("Texte."u8);
        }
        Directory.Delete(folder.PathOf("fiches/Biologie"), recursive: true);
        vault.Analyze(Now);

        var restored = vault.RestoreSheetFromBackup(id, backup, Now);

        Assert.AreEqual("fiches/Biologie/Mitose.md", restored.Value);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(folder.PathOf("fiches/Biologie/Mitose.ressources/cellule.png")));
        Assert.AreEqual(FilePresence.Present, vault.Sheet(id).Presence);
        Assert.AreEqual(VaultErrorKind.UnknownSheet, Assert.ThrowsExactly<VaultException>(() => vault.RestoreSheetFromBackup("f_inconnue", backup, Now)).Kind);
    }

    [TestMethod]
    public void Exports_go_to_an_existing_folder_outside_the_vault()
    {
        using var folder = TemporaryVault.Empty();
        using var outside = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        vault.CreateSheet(null, "Fiche", "Texte.\n", Now);
        outside.Write("fichier.txt", "x");

        Assert.Contains("ce n'est pas un dossier", Assert.ThrowsExactly<VaultException>(() => vault.Export(ExportKind.Tracking, outside.PathOf("fichier.txt"), Stamp)).Message);
        Assert.AreEqual(VaultErrorKind.Io, Assert.ThrowsExactly<VaultException>(() => vault.Export(ExportKind.Tracking, outside.PathOf("absent"), Stamp)).Kind);

        // Sans fuseau précisé, les jours sont ceux du fuseau local.
        var files = vault.Export(ExportKind.Tracking, outside.Root, Stamp);
        CollectionAssert.AreEqual(TrackingExports, files.Select(Path.GetFileName).ToArray());
        Assert.StartsWith("Type;", File.ReadAllText(files[1]));
    }

    /// <summary>
    /// Le coffre de référence contient chaque sorte d'événement de suivi, de rappel et d'état
    /// d'ancrage : ses exports par la 0.x (<c>reference-vectors --exports</c>, en UTC) sont
    /// reproduits à l'octet.
    /// </summary>
    [TestMethod]
    public void Exports_of_the_reference_vault_are_those_of_version_0x()
    {
        using var folder = TemporaryVault.CopyOfReference();
        using var outside = TemporaryVault.Empty();
        var day = new DateOnly(2026, 10, 31);
        using var vault = VaultSession.Open(folder.Root, new Moment(day, new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)));
        var stamp = day.ToDateTime(new TimeOnly(12, 0));

        foreach (var kind in new[] { ExportKind.Tracking, ExportKind.Annotations, ExportKind.Quotes })
        {
            vault.Export(kind, outside.Root, stamp, TimeZoneInfo.Utc);
        }

        var expected = Path.Combine(AppContext.BaseDirectory, "fixtures", "exports-reference");
        CollectionAssert.AreEqual(
            Directory.GetFiles(expected).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray(),
            Directory.GetFiles(outside.Root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray());
        foreach (var file in Directory.GetFiles(expected))
        {
            Assert.AreEqual(File.ReadAllText(file), File.ReadAllText(outside.PathOf(Path.GetFileName(file))), Path.GetFileName(file));
            CollectionAssert.AreEqual(File.ReadAllBytes(file), File.ReadAllBytes(outside.PathOf(Path.GetFileName(file))), Path.GetFileName(file));
        }
    }

    [TestMethod]
    public void A_shared_archive_holds_only_the_files_the_sheet_shows()
    {
        using var folder = TemporaryVault.Empty();
        using var outside = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        folder.Write("fiches/A/carte.png", "a");
        folder.Write("fiches/B/carte.png", "b");
        folder.Write("fiches/Grande/vue.png", "g");
        using (var big = File.OpenWrite(folder.PathOf("fiches/Grande/vue.png")))
        {
            big.SetLength(VaultSession.MaxSharedBytes + 1L);
        }
        var id = vault.CreateSheet("Cours", "Carte", "![a](../A/carte.png) ![b](../B/carte.png) [autre](Autre.md) ![absente](absente.png) ![vue](../Grande/vue.png)\n", Now);

        var archive = vault.ShareSheet(id, ShareFormat.MarkdownWithImages, outside.Root, content: null);

        using var zip = ZipFile.OpenRead(archive);
        CollectionAssert.AreEqual(SharedEntries, zip.Entries.Select(e => e.FullName).ToArray());
        using var reader = new StreamReader(zip.Entries[0].Open());
        Assert.Contains("![a](images/carte.png) ![b](images/2-carte.png)", reader.ReadToEnd());
    }

    private static string Zip(string path, params (string Name, byte[] Content)[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
            stream.Write(content);
        }
        return path;
    }

    /// <summary>Remplace la taille décompressée annoncée par le répertoire central, entrée par entrée.</summary>
    private static void Lie(string path, Func<string, uint?> size)
    {
        var bytes = File.ReadAllBytes(path);
        for (var i = 0; i + 46 <= bytes.Length; i++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i)) != 0x02014b50)
            {
                continue;
            }
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i + 28));
            if (size(Encoding.UTF8.GetString(bytes, i + 46, nameLength)) is { } announced)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i + 24), announced);
            }
        }
        File.WriteAllBytes(path, bytes);
    }
}
