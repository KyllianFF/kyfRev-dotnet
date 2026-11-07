using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using KyfRev.Application.Ports;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Backups;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// Les sauvegardes d'un coffre chiffré restent chiffrées : l'archive recopie l'en-tête, l'index et les objets tels qu'ils
/// sont sur le disque, se restaure en un coffre chiffré, et ne rend une fiche qu'au coffre dont elle vient.
/// </summary>
[TestClass]
public sealed class EncryptedBackupTests
{
    private static readonly Moment Now = new(new DateOnly(2026, 10, 31), new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
    private static readonly DateTime Stamp = new(2026, 10, 31, 12, 0, 0);

    /// <summary>Des mots du coffre de référence : titres, dossiers, texte d'une fiche, nom d'un suivi.</summary>
    private static readonly string[] Secrets = ["Révolution", "Mitose", "Pythagore", "Histoire", "fiches", "suivi", "f_01M69JFV2X5VM53EY3DR1BY61M", "prophase"];

    [TestMethod]
    public void A_backup_of_an_encrypted_vault_holds_nothing_in_clear_and_restores_encrypted()
    {
        using var folder = TemporaryVault.CopyOfReference(StorageKind.Encrypted);
        using var outside = TemporaryVault.Empty();
        BackupFile backup;
        using (var vault = folder.Open(Now))
        {
            backup = vault.Backup(outside.PathOf("sauvegardes"), Stamp);
        }

        using (var archive = ZipFile.OpenRead(backup.Path))
        {
            var names = archive.Entries.Select(entry => entry.FullName).ToList();
            Assert.IsTrue(names.All(name => name is EncryptedVault.HeaderFile or EncryptedVault.IndexFile || name.StartsWith(EncryptedVault.Objects + "/", StringComparison.Ordinal)), string.Join(", ", names));
            Assert.AreEqual(names.Count - 2, backup.FileCount, "le compte est celui des fichiers du coffre, pas de son index ni de son en-tête");
            foreach (var entry in archive.Entries)
            {
                var bytes = Read(entry);
                foreach (var secret in Secrets)
                {
                    Assert.AreEqual(-1, bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(secret)), $"« {secret} » se lit dans {entry.FullName}");
                }
            }
        }

        var restored = VaultBackups.Restore(backup.Path, outside.PathOf("restaurations"), Now.Day);

        Assert.IsTrue(EncryptedVault.IsEncrypted(restored));
        using var storage = TemporaryVault.OpenEncrypted(restored);
        CollectionAssert.AreEqual(Logical(folder.Storage, folder.Root), Logical(storage, restored), "chaque fichier revient, déchiffré à l'identique");
        using var reopened = VaultSession.Open(restored, Now, storage: storage);
        Assert.AreEqual(6, reopened.Sheets.Count(entry => entry.Presence == FilePresence.Present));
    }

    [TestMethod]
    public void A_missing_sheet_comes_back_from_an_encrypted_backup_or_from_one_made_before_encryption()
    {
        using var folder = TemporaryVault.Empty(StorageKind.Encrypted);
        using var outside = TemporaryVault.Empty();
        using var vault = folder.Initialize(Now);
        var id = vault.CreateSheet("Biologie", "Mitose", "La prophase.\n", Now);
        vault.AddResource(id, "cellule.png", [1, 2, 3]);
        var encrypted = vault.Backup(outside.PathOf("chiffrees"), Stamp).Path;
        var sheet = folder.ReadBytes("fiches/Biologie/Mitose.md");
        var plain = Zip(outside.PathOf("en-clair.zip"), ("fiches/Biologie/Mitose.md", sheet), ("fiches/Biologie/Mitose.ressources/cellule.png", [4, 5, 6]));

        foreach (var (backup, image) in new[] { (encrypted, new byte[] { 1, 2, 3 }), (plain, new byte[] { 4, 5, 6 }) })
        {
            folder.Delete("fiches/Biologie");
            vault.Analyze(Now);
            Assert.AreEqual(FilePresence.Missing, vault.Sheet(id).Presence);

            Assert.AreEqual("fiches/Biologie/Mitose.md", vault.RestoreSheetFromBackup(id, backup, Now).Value);

            Assert.AreEqual(FilePresence.Present, vault.Sheet(id).Presence);
            CollectionAssert.AreEqual(sheet, folder.ReadBytes("fiches/Biologie/Mitose.md"));
            CollectionAssert.AreEqual(image, folder.ReadBytes("fiches/Biologie/Mitose.ressources/cellule.png"));
        }
        Assert.IsFalse(Directory.Exists(folder.PathOf("fiches")), "rien n'est restauré en clair sur le disque");
    }

    [TestMethod]
    public void A_backup_of_another_vault_or_an_altered_one_gives_nothing_back()
    {
        using var folder = TemporaryVault.Empty(StorageKind.Encrypted);
        using var outside = TemporaryVault.Empty();
        using var vault = folder.Initialize(Now);
        var id = vault.CreateSheet(null, "Mitose", "La prophase.\n", Now);
        var backup = vault.Backup(outside.PathOf("sauvegardes"), Stamp).Path;
        folder.Delete("fiches/Mitose.md");
        vault.Analyze(Now);

        // Le même coffre, déchiffré avec une autre clé : son index ne s'ouvre pas.
        using (var other = TemporaryVault.Empty())
        {
            using var otherStorage = EncryptedStorage.Create(other.Root, EncryptedVault.ReadHeader(folder.Root), RandomNumberGenerator.GetBytes(32), DiskStorage.Instance);
            using var otherVault = VaultSession.Initialize(other.Root, Now, storage: otherStorage);
            var foreign = otherVault.Backup(outside.PathOf("autres"), Stamp).Path;
            var error = Assert.ThrowsExactly<VaultException>(() => vault.RestoreSheetFromBackup(id, foreign, Now));
            Assert.AreEqual(VaultErrorKind.InvalidBackup, error.Kind);
            Assert.Contains("autre coffre", error.Message);
        }

        // Chaque objet altéré, et la fiche ne revient pas : ni elle, ni rien d'autre n'est écrit.
        using (var archive = ZipFile.Open(backup, ZipArchiveMode.Update))
        {
            foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith(EncryptedVault.Objects + "/", StringComparison.Ordinal)).ToList())
            {
                var bytes = Read(entry);
                bytes[^1] ^= 1;
                var name = entry.FullName;
                entry.Delete();
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(bytes);
            }
        }
        var altered = Assert.ThrowsExactly<VaultException>(() => vault.RestoreSheetFromBackup(id, backup, Now));
        Assert.AreEqual(VaultErrorKind.InvalidBackup, altered.Kind);
        Assert.AreEqual(FilePresence.Missing, vault.Sheet(id).Presence);
        Assert.IsFalse(folder.FileExists("fiches/Mitose.md"));
    }

    [TestMethod]
    public void A_vault_missing_an_object_backs_up_and_restores_the_rest()
    {
        using var folder = TemporaryVault.Empty(StorageKind.Encrypted);
        using var outside = TemporaryVault.Empty();
        using var vault = folder.Initialize(Now);
        var kept = vault.CreateSheet(null, "Mitose", "La prophase.\n", Now);
        var lost = vault.CreateSheet(null, "Révolution", "1789.\n", Now);
        var files = folder.VaultFiles().Count;

        // L'objet d'une fiche a disparu du disque : la sauvegarde recopie le reste, comme pour un coffre en clair.
        File.Delete(((EncryptedStorage)folder.Storage).ObjectFileOf(folder.PathOf("fiches/Révolution.md")));
        var backup = vault.Backup(outside.PathOf("sauvegardes"), Stamp);
        Assert.AreEqual(files - 1, backup.FileCount, "tout, sauf la fiche dont l'objet manque");

        folder.Delete("fiches/Mitose.md");
        folder.Delete("fiches/Révolution.md");
        vault.Analyze(Now);
        Assert.AreEqual("fiches/Mitose.md", vault.RestoreSheetFromBackup(kept, backup.Path, Now).Value);
        Assert.AreEqual(VaultErrorKind.NotInBackup, Assert.ThrowsExactly<VaultException>(() => vault.RestoreSheetFromBackup(lost, backup.Path, Now)).Kind);
    }

    [TestMethod]
    public void A_storage_serves_only_its_own_folder()
    {
        using var folder = TemporaryVault.Empty(StorageKind.Encrypted);
        using var other = TemporaryVault.Empty();
        folder.Initialize(Now).Dispose();

        Assert.ThrowsExactly<ArgumentException>(() => VaultSession.Open(other.Root, Now, storage: folder.Storage));
        Assert.ThrowsExactly<ArgumentException>(() => VaultSession.Initialize(other.Root, Now, storage: folder.Storage));
        using var reopened = VaultSession.Open(folder.Root.ToUpperInvariant() + Path.DirectorySeparatorChar, Now, storage: folder.Storage);
        Assert.IsEmpty(reopened.Sheets, "le même dossier, quelle que soit la casse");
    }

    [TestMethod]
    public void The_vault_recognizes_the_files_of_an_encrypted_vault_by_their_names()
    {
        string[] names = [EncryptedVault.HeaderFile, EncryptedVault.IndexFile];
        CollectionAssert.AreEqual(names, new[] { VaultLayout.EncryptedHeaderFile, VaultLayout.EncryptedIndexFile });
    }

    /// <summary>Chaque fichier du coffre et son empreinte, tels que la session les lit.</summary>
    private static List<string> Logical(VaultStorage storage, string root) =>
    [
        .. storage.AllEntries(root)
            .Where(entry => !entry.IsDirectory)
            .Select(entry => $"{Path.GetRelativePath(root, entry.FullName)}:{Convert.ToHexString(SHA256.HashData(storage.ReadAllBytes(entry.FullName)))}")
            .Order(StringComparer.Ordinal),
    ];

    private static byte[] Read(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string Zip(string path, params (string Name, byte[] Content)[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var stream = archive.CreateEntry(name).Open();
            stream.Write(content);
        }
        return path;
    }
}
