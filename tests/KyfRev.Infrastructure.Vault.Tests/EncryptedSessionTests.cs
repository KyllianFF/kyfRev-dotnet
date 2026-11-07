using System.IO.Compression;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// Ce que la session d'un coffre chiffré fait de plus qu'un disque : elle relit l'index une fois le verrou pris, et dit
/// quand un effacement n'a pas pu retirer un objet, qu'aucune sauvegarde ne recopie alors.
/// </summary>
[TestClass]
public sealed class EncryptedSessionTests
{
    private static readonly Moment Now = new(new DateOnly(2026, 11, 7), new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
    private static readonly DateTime Stamp = new(2026, 11, 7, 12, 0, 0);

    [TestMethod]
    public void A_session_reads_the_index_once_it_holds_the_lock()
    {
        using var folder = TemporaryVault.Empty(StorageKind.Encrypted);
        folder.Initialize(Now).Dispose();

        // Ce stockage a lu l'index avant qu'un autre processus écrive dans le coffre, puis le ferme.
        using var early = TemporaryVault.OpenEncrypted(folder.Root);
        using (var other = folder.Open(Now))
        {
            other.CreateSheet(null, "Mitose", "La prophase.\n", Now);
        }

        using (var vault = VaultSession.Open(folder.Root, Now, storage: early))
        {
            Assert.AreEqual("Mitose", vault.Sheets.Single().Sheet.Title, "la session voit ce que l'autre processus a écrit");
            vault.CreateSheet(null, "Révolution", "1789.\n", Now);
        }

        using var after = TemporaryVault.OpenEncrypted(folder.Root);
        Assert.IsTrue(after.FileExists(folder.PathOf("fiches/Mitose.md")), "l'écriture de l'autre processus n'est pas perdue");
        Assert.IsTrue(after.FileExists(folder.PathOf("fiches/Révolution.md")));
    }

    [TestMethod]
    public void A_session_whose_index_no_longer_opens_gives_its_lock_back()
    {
        using var folder = TemporaryVault.Empty(StorageKind.Encrypted);
        folder.Initialize(Now).Dispose();
        using var early = TemporaryVault.OpenEncrypted(folder.Root);
        var index = Path.Combine(folder.Root, EncryptedVault.IndexFile);
        var sealedIndex = File.ReadAllBytes(index);
        File.WriteAllBytes(index, [.. sealedIndex[..^1], (byte)(sealedIndex[^1] ^ 1)]);

        Assert.AreEqual(VaultErrorKind.Io, Assert.ThrowsExactly<VaultException>(() => VaultSession.Open(folder.Root, Now, storage: early)).Kind);
        Assert.IsTrue(early.FileExists(folder.PathOf(VaultLayout.SettingsFile)), "l'index en mémoire reste celui qu'il était");

        File.WriteAllBytes(index, sealedIndex);
        using var reopened = VaultSession.Open(folder.Root, Now, storage: early);
        Assert.IsEmpty(reopened.Sheets, "le verrou a été rendu");
    }

    [TestMethod]
    public void A_purge_that_cannot_erase_an_object_says_so_and_no_backup_keeps_it()
    {
        using var folder = TemporaryVault.Empty(StorageKind.Encrypted);
        using var outside = TemporaryVault.Empty();
        using var vault = folder.Initialize(Now);
        var id = vault.CreateSheet(null, "Mitose", "La prophase.\n", Now);
        vault.CreateSheet(null, "Révolution", "1789.\n", Now);
        vault.TrashSheet(id, Now);
        var trashed = $"fiches/.supprimees/{id}/Mitose.md";
        var objectFile = ((EncryptedStorage)folder.Storage).ObjectFileOf(folder.PathOf(trashed));

        // Un autre programme tient l'objet ouvert : il ne s'efface pas.
        using (folder.Hold(trashed))
        {
            var error = Assert.ThrowsExactly<VaultException>(() => vault.PurgeSheet(id));
            Assert.AreEqual(VaultErrorKind.Io, error.Kind);
            Assert.Contains(Path.GetFileName(objectFile), error.Message, "l'objet resté est nommé");
        }
        Assert.IsFalse(folder.DirectoryExists($"fiches/.supprimees/{id}"), "la fiche a quitté le coffre");
        Assert.IsTrue(File.Exists(objectFile), "son objet est resté sur le disque");

        var backup = vault.Backup(outside.PathOf("sauvegardes"), Stamp);

        using var archive = ZipFile.OpenRead(backup.Path);
        var objects = archive.Entries.Where(entry => entry.FullName.StartsWith(EncryptedVault.Objects + "/", StringComparison.Ordinal)).Select(entry => entry.Name).ToList();
        Assert.DoesNotContain(Path.GetFileName(objectFile), objects, "l'objet que l'index ne nomme plus n'est pas sauvegardé");
        Assert.HasCount(backup.FileCount, objects, "un objet par fichier du coffre, et rien d'autre");
    }

    [TestMethod]
    public void A_purge_goes_on_past_an_object_that_stays_and_says_so_at_the_end()
    {
        foreach (var held in new[] { ".json", ".annotations.json" })
        {
            using var folder = TemporaryVault.Empty(StorageKind.Encrypted);
            using var vault = folder.Initialize(Now);
            var id = vault.CreateSheet(null, "Mitose", "Le corps.", Now);
            vault.CreateAnnotation(id, new Format.StoredAnchor("Le", string.Empty, " corps.", 0, 2, [], null, "sha256:x", Format.UnknownProperties.None), null, Now);
            vault.TrashSheet(id, Now);
            var tracking = $".suivi/corbeille/{id}.json";
            var annotations = $".suivi/corbeille/{id}.annotations.json";
            var relative = $".suivi/corbeille/{id}{held}";
            var objectFile = ObjectFileOf(folder, relative);

            // Un antivirus tient l'objet : il se lit, mais ne s'efface pas.
            VaultException error;
            using (folder.Hold(relative, FileShare.Read))
            {
                error = Assert.ThrowsExactly<VaultException>(() => vault.PurgeSheet(id));
            }

            Assert.AreEqual(VaultErrorKind.Io, error.Kind, held);
            Assert.Contains(Path.GetFileName(objectFile), error.Message, held);
            Assert.IsFalse(folder.FileExists(tracking), held);
            Assert.IsFalse(folder.FileExists(annotations), held);
            Assert.IsFalse(folder.DirectoryExists($"fiches/.supprimees/{id}"), $"{held} : l'effacement est allé jusqu'au bout");
            Assert.IsEmpty(vault.TrashedSheets(), held);
            Assert.IsTrue(File.Exists(objectFile), held);
        }
    }

    [TestMethod]
    public void A_folder_purge_goes_on_past_an_object_that_stays()
    {
        using var folder = TemporaryVault.Empty(StorageKind.Encrypted);
        using var vault = folder.Initialize(Now);
        vault.CreateSheet("Cours", "A", "Texte.\n", Now);
        vault.CreateSheet("Cours", "B", "Texte.\n", Now);
        var held = ObjectFileOf(folder, "fiches/Cours/A.md");
        var other = ObjectFileOf(folder, "fiches/Cours/B.md");

        VaultException error;
        using (folder.Hold("fiches/Cours/A.md", FileShare.Read))
        {
            error = Assert.ThrowsExactly<VaultException>(() => vault.PurgeFolder("Cours", Now));
        }

        Assert.Contains(Path.GetFileName(held), error.Message);
        Assert.IsTrue(File.Exists(held));
        Assert.IsFalse(File.Exists(other), "l'autre fiche est effacée");
        Assert.IsEmpty(vault.TrashedSheets());
        Assert.IsEmpty(folder.Storage.Entries(folder.PathOf("fiches/.supprimees/dossiers")), "ce qui restait du dossier est effacé");
    }

    private static string ObjectFileOf(TemporaryVault folder, string relative) =>
        ((EncryptedStorage)folder.Storage).ObjectFileOf(folder.PathOf(relative));
}
