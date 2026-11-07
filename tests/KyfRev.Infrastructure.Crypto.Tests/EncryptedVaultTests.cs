using System.Security.Cryptography;
using System.Text;
using KyfRev.Infrastructure.Vault.Files;

namespace KyfRev.Infrastructure.Crypto.Tests;

/// <summary>Le magasin d'un coffre chiffré : des objets opaques et un index chiffré, rien de lisible sur le disque.</summary>
[TestClass]
public sealed class EncryptedVaultTests
{
    private const string VaultId = "v_01M69JFV2A3WD8KH55EHWC1G7N";
    private const string Title = "Révolution française";

    private static readonly string[] ExpectedFiles = [".suivi/coffre.json", "fiches/Histoire/" + Title + ".md"];
    private static readonly string[] ExpectedFolders = [".suivi", "fiches", "fiches/Histoire", "fiches/Vide"];

    /// <summary>Un en-tête et sa clé, créés une fois : la dérivation d'un vrai réglage prend une fraction de seconde.</summary>
    private static readonly Lazy<(VaultHeader Header, byte[] Key)> Shared = new(() =>
    {
        var (header, key, _) = VaultHeader.Create(VaultId, "un mot de passe d'essai", PasswordKeys.NewPbkdf2());
        return (header, key);
    });

    [TestMethod]
    public void Files_folders_and_contents_come_back_as_written()
    {
        using var folder = new TemporaryFolder();
        var (header, key) = Shared.Value;
        using (var vault = EncryptedVault.Create(folder.Root, header, key, DiskStorage.Instance))
        {
            vault.Write("fiches/Histoire/" + Title + ".md", Encoding.UTF8.GetBytes("# " + Title + "\n"));
            vault.Write(".suivi/coffre.json", "{}"u8);
            vault.AddFolder("fiches/Vide");
            vault.Flush();
        }

        using var reopened = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);
        Assert.AreEqual(VaultId, reopened.VaultId);
        CollectionAssert.AreEqual(ExpectedFiles, reopened.Files.ToArray());
        CollectionAssert.AreEqual(ExpectedFolders, reopened.Folders.ToArray());
        Assert.AreEqual("# " + Title + "\n", Encoding.UTF8.GetString(reopened.Read("fiches/Histoire/" + Title + ".md")));
        Assert.AreEqual(2, reopened.LengthOf(".suivi/coffre.json"));
        Assert.ThrowsExactly<VaultCryptoException>(() => reopened.Read("fiches/Absente.md"));
    }

    [TestMethod]
    public void A_released_vault_forgets_its_paths_and_its_key()
    {
        using var folder = new TemporaryFolder();
        var (header, key) = Shared.Value;
        using (var written = EncryptedVault.Create(folder.Root, header, key, DiskStorage.Instance))
        {
            written.Write("fiches/Histoire/" + Title + ".md", Encoding.UTF8.GetBytes("# " + Title + "\n"));
            written.Flush();
        }
        using var vault = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);
        Assert.IsNotEmpty(vault.Files);

        vault.Dispose();

        // Les titres et les dossiers de l'index en clair ne restent pas en mémoire, et plus rien ne se lit.
        Assert.IsEmpty(vault.Files);
        Assert.IsEmpty(vault.Folders);
        Assert.ThrowsExactly<ObjectDisposedException>(() => vault.ContainsFile("fiches/Histoire/" + Title + ".md"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => vault.ContainsFolder("fiches/Histoire"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => vault.Read("fiches/Histoire/" + Title + ".md"));
        vault.Dispose();
    }

    [TestMethod]
    public void Nothing_on_the_disk_names_or_shows_a_sheet()
    {
        using var folder = new TemporaryFolder();
        var (header, key) = Shared.Value;
        using (var vault = EncryptedVault.Create(folder.Root, header, key, DiskStorage.Instance))
        {
            vault.Write("fiches/" + Title + ".md", Encoding.UTF8.GetBytes(Title));
            vault.Flush();
        }

        var entries = Directory.EnumerateFileSystemEntries(folder.Root, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(folder.Root, path)).ToList();
        Assert.HasCount(4, entries, string.Join(", ", entries));
        Assert.Contains(EncryptedVault.HeaderFile, entries);
        Assert.Contains(EncryptedVault.IndexFile, entries);
        Assert.Contains(EncryptedVault.Objects, entries);
        Assert.IsTrue(entries.All(entry => !entry.Contains("Révolution", StringComparison.Ordinal) && !entry.Contains("fiches", StringComparison.Ordinal)));
        foreach (var file in Directory.EnumerateFiles(folder.Root, "*", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(file);
            Assert.AreEqual(-1, bytes.AsSpan().IndexOf("volution"u8), file);
            Assert.AreEqual(-1, bytes.AsSpan().IndexOf("fiches"u8), file);
        }
    }

    [TestMethod]
    public void Rewriting_a_file_keeps_its_object_with_a_fresh_nonce()
    {
        using var folder = new TemporaryFolder();
        var (header, key) = Shared.Value;
        using var vault = EncryptedVault.Create(folder.Root, header, key, DiskStorage.Instance);
        vault.Write("fiches/a.md", "même contenu"u8);
        var objects = Directory.GetFiles(Path.Combine(folder.Root, EncryptedVault.Objects));
        var before = File.ReadAllBytes(Assert.ContainsSingle(objects));

        vault.Write("fiches/a.md", "même contenu"u8);

        var after = File.ReadAllBytes(Assert.ContainsSingle(Directory.GetFiles(Path.Combine(folder.Root, EncryptedVault.Objects))));
        Assert.IsFalse(before.AsSpan().SequenceEqual(after), "un nonce neuf à chaque écriture");
        Assert.AreEqual("même contenu", Encoding.UTF8.GetString(vault.Read("fiches/a.md")));
    }

    [TestMethod]
    public void A_wrong_key_an_altered_object_or_a_swapped_one_is_refused()
    {
        using var folder = new TemporaryFolder();
        var (header, key) = Shared.Value;
        using (var vault = EncryptedVault.Create(folder.Root, header, key, DiskStorage.Instance))
        {
            vault.Write("a.md", "premier"u8);
            vault.Write("b.md", "second"u8);
            vault.Flush();
        }

        Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.Open(folder.Root, RandomNumberGenerator.GetBytes(32), DiskStorage.Instance));

        var objects = Directory.GetFiles(Path.Combine(folder.Root, EncryptedVault.Objects));
        using (var vault = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance))
        {
            var first = objects.Single(path => Path.GetFileNameWithoutExtension(path) == ObjectOf(vault, "a.md", objects));
            var second = objects.Single(path => path != first);
            File.Copy(first, second, overwrite: true);
            Assert.ThrowsExactly<VaultCryptoException>(() => vault.Read("b.md"), "un objet recopié sous un autre identifiant ne s'ouvre pas");
            var altered = File.ReadAllBytes(first);
            altered[^1] ^= 1;
            File.WriteAllBytes(first, altered);
            Assert.ThrowsExactly<VaultCryptoException>(() => vault.Read("a.md"));
        }
    }

    [TestMethod]
    public void An_index_from_another_vault_or_a_misplaced_creation_is_refused()
    {
        using var first = new TemporaryFolder();
        using var second = new TemporaryFolder();
        var (header, key) = Shared.Value;
        var (otherHeader, _, _) = VaultHeader.Create("v_01M69JFV2A3WD8KH55EHWC1G7P", "un mot de passe d'essai", PasswordKeys.NewPbkdf2());
        EncryptedVault.Create(first.Root, header, key, DiskStorage.Instance).Dispose();
        File.WriteAllBytes(Path.Combine(second.Root, EncryptedVault.HeaderFile), otherHeader.Write());
        File.Copy(Path.Combine(first.Root, EncryptedVault.IndexFile), Path.Combine(second.Root, EncryptedVault.IndexFile));

        Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.Open(second.Root, key, DiskStorage.Instance), "l'index est lié à son coffre");
        Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.Create(first.Root, header, key, DiskStorage.Instance), "jamais par-dessus un dossier qui n'est pas vide");
        Assert.IsTrue(EncryptedVault.IsEncrypted(first.Root));
        Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.ReadHeader(Path.Combine(first.Root, "absent")));
    }

    [TestMethod]
    public void Paths_ignore_case_but_keep_the_one_written_first()
    {
        using var folder = new TemporaryFolder();
        var (header, key) = Shared.Value;
        using (var vault = EncryptedVault.Create(folder.Root, header, key, DiskStorage.Instance))
        {
            vault.AddFolder("fiches/Cours");
            vault.Write("FICHES/cours/Leçon.md", "un"u8);
            vault.Write("fiches/cours/LEÇON.md", "deux"u8);
            vault.Flush();
            Assert.IsTrue(vault.ContainsFile("fiches/COURS/leçon.md"));
            Assert.IsTrue(vault.ContainsFolder("Fiches"));
        }

        using var reopened = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);
        Assert.AreEqual("fiches/Cours/Leçon.md", Assert.ContainsSingle(reopened.Files));
        Assert.AreEqual("deux", Encoding.UTF8.GetString(reopened.Read("fiches/cours/leçon.md")));
        Assert.AreEqual("fiches|fiches/Cours", string.Join('|', reopened.Folders));
    }

    [TestMethod]
    [DataRow("""{"format":2,"fichiers":{},"dossiers":[]}""", "format inconnu")]
    [DataRow("""{"format":1,"fichiers":{"a.md":{"objet":"court"}},"dossiers":[]}""", "objet invalide")]
    [DataRow("""{"format":1,"fichiers":{"a.md":{}},"dossiers":[]}""", "illisible")]
    [DataRow("""{"format":1,"fichiers":{"a.md":{"objet":"0123456789ABCDEFGHJKMNPQRS"},"A.MD":{"objet":"0123456789ABCDEFGHJKMNPQRT"}},"dossiers":[]}""", "deux fois")]
    [DataRow("""{"format":"un"}""", "illisible")]
    [DataRow("""[1]""", "illisible")]
    [DataRow("""pas du JSON""", "illisible")]
    public void An_index_that_does_not_hold_together_is_refused(string json, string reason)
    {
        using var folder = new TemporaryFolder();
        var (header, key) = Shared.Value;
        EncryptedVault.Create(folder.Root, header, key, DiskStorage.Instance).Dispose();
        File.WriteAllBytes(Path.Combine(folder.Root, EncryptedVault.IndexFile), SealedFile.Seal(key, VaultId, EncryptedVault.IndexId, Encoding.UTF8.GetBytes(json)));

        Assert.Contains(reason, Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.Open(folder.Root, key, DiskStorage.Instance)).Message);
    }

    [TestMethod]
    public void A_missing_index_or_object_and_a_cut_creation_are_refused()
    {
        using var folder = new TemporaryFolder();
        var (header, key) = Shared.Value;
        using (var vault = EncryptedVault.Create(folder.Root, header, key, DiskStorage.Instance))
        {
            vault.Write("a.md", "un"u8);
            vault.Flush();
            File.Delete(vault.ObjectPath(Path.GetFileNameWithoutExtension(Directory.GetFiles(Path.Combine(folder.Root, EncryptedVault.Objects)).Single())));
            Assert.Contains("introuvable", Assert.ThrowsExactly<VaultCryptoException>(() => vault.Read("a.md")).Message);
            Assert.ThrowsExactly<VaultCryptoException>(() => vault.MoveFolder("absent", "ailleurs"));
        }
        File.Delete(Path.Combine(folder.Root, EncryptedVault.IndexFile));
        Assert.Contains("index", Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.Open(folder.Root, key, DiskStorage.Instance)).Message);

        using var cut = new TemporaryFolder();
        var disk = new KyfRev.Tests.Support.FailingDisk { FailAt = 3 };
        Assert.ThrowsExactly<IOException>(() => EncryptedVault.Create(cut.Root, header, key, disk), "l'index ne s'écrit pas");
    }

    [TestMethod]
    [DataRow("../hors.md")]
    [DataRow("fiches//a.md")]
    [DataRow("C:/a.md")]
    [DataRow("fiches/./a.md")]
    [DataRow("")]
    public void A_path_outside_the_vault_is_refused(string path) =>
        Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.Normalized(path));

    [TestMethod]
    public void A_windows_path_becomes_a_vault_path() =>
        Assert.AreEqual("fiches/Histoire/a.md", EncryptedVault.Normalized("fiches\\Histoire\\a.md"));

    /// <summary>L'objet d'un fichier, retrouvé en essayant chacun : seul le bon s'ouvre sous ce chemin.</summary>
    private static string ObjectOf(EncryptedVault vault, string path, string[] objects)
    {
        var content = vault.Read(path);
        foreach (var candidate in objects)
        {
            var saved = File.ReadAllBytes(candidate);
            try
            {
                File.WriteAllBytes(candidate, []);
                if (Throws(() => vault.Read(path)))
                {
                    return Path.GetFileNameWithoutExtension(candidate);
                }
            }
            finally
            {
                File.WriteAllBytes(candidate, saved);
            }
        }
        Assert.Fail($"aucun objet pour {path} ({content.Length} octets)");
        return string.Empty;
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (VaultCryptoException)
        {
            return true;
        }
    }

    private sealed class TemporaryFolder : IDisposable
    {
        public string Root { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kyfrev-tests", Guid.NewGuid().ToString("N"))).FullName;

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
