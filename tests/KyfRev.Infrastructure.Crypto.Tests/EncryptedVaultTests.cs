using System.Security.Cryptography;
using System.Text;

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
        using (var vault = EncryptedVault.Create(folder.Root, header, key))
        {
            vault.Write("fiches/Histoire/" + Title + ".md", Encoding.UTF8.GetBytes("# " + Title + "\n"));
            vault.Write(".suivi/coffre.json", "{}"u8);
            vault.AddFolder("fiches/Vide");
            vault.Flush();
        }

        using var reopened = EncryptedVault.Open(folder.Root, key);
        Assert.AreEqual(VaultId, reopened.VaultId);
        CollectionAssert.AreEqual(ExpectedFiles, reopened.Files.ToArray());
        CollectionAssert.AreEqual(ExpectedFolders, reopened.Folders.ToArray());
        Assert.AreEqual("# " + Title + "\n", Encoding.UTF8.GetString(reopened.Read("fiches/Histoire/" + Title + ".md")));
        Assert.AreEqual(2, reopened.LengthOf(".suivi/coffre.json"));
        Assert.ThrowsExactly<VaultCryptoException>(() => reopened.Read("fiches/Absente.md"));
    }

    [TestMethod]
    public void Nothing_on_the_disk_names_or_shows_a_sheet()
    {
        using var folder = new TemporaryFolder();
        var (header, key) = Shared.Value;
        using (var vault = EncryptedVault.Create(folder.Root, header, key))
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
        using var vault = EncryptedVault.Create(folder.Root, header, key);
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
        using (var vault = EncryptedVault.Create(folder.Root, header, key))
        {
            vault.Write("a.md", "premier"u8);
            vault.Write("b.md", "second"u8);
            vault.Flush();
        }

        Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.Open(folder.Root, RandomNumberGenerator.GetBytes(32)));

        var objects = Directory.GetFiles(Path.Combine(folder.Root, EncryptedVault.Objects));
        using (var vault = EncryptedVault.Open(folder.Root, key))
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
        EncryptedVault.Create(first.Root, header, key).Dispose();
        File.WriteAllBytes(Path.Combine(second.Root, EncryptedVault.HeaderFile), otherHeader.Write());
        File.Copy(Path.Combine(first.Root, EncryptedVault.IndexFile), Path.Combine(second.Root, EncryptedVault.IndexFile));

        Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.Open(second.Root, key), "l'index est lié à son coffre");
        Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.Create(first.Root, header, key), "jamais par-dessus un dossier qui n'est pas vide");
        Assert.IsTrue(EncryptedVault.IsEncrypted(first.Root));
        Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.ReadHeader(Path.Combine(first.Root, "absent")));
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
