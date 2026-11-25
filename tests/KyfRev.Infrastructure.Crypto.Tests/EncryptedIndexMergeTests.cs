using System.Text;
using KyfRev.Infrastructure.Vault.Files;

namespace KyfRev.Infrastructure.Crypto.Tests;

/// <summary>
/// Un coffre chiffré ouvert sur deux postes, dans un dossier synchronisé : chacun a lu l'index à l'ouverture, et chacun
/// l'écrit en entier à chaque création, déplacement ou effacement. Ce que l'un y a ajouté n'est jamais effacé par
/// l'écriture de l'autre. Les deux postes sont ici deux coffres ouverts sur le même dossier.
/// </summary>
[TestClass]
public sealed class EncryptedIndexMergeTests
{
    private const string VaultId = "v_01M69JFV2A3WD8KH55EHWC1G7N";

    private static readonly string[] AllFour = ["fiches/Biologie/Cytokinèse.md", "fiches/Biologie/Mitose.md", "fiches/Biologie/Méiose.md", "fiches/Histoire/Révolution.md"];
    private static readonly string[] MovedOnly = ["fiches/Biologie/Division/Mitose.md"];

    private static readonly Lazy<(VaultHeader Header, byte[] Key)> Shared = new(() =>
    {
        var (header, key, _) = VaultHeader.Create(VaultId, "un mot de passe d'essai", PasswordKeys.NewPbkdf2());
        return (header, key);
    });

    [TestMethod]
    public void Sheets_created_on_both_computers_are_all_kept()
    {
        using var folder = Prepared(out var key);
        using var here = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);
        using var there = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);

        there.Save("fiches/Biologie/Méiose.md", "La méiose."u8);
        here.Save("fiches/Biologie/Cytokinèse.md", "La cytokinèse."u8);

        using var reopened = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);
        CollectionAssert.AreEqual(AllFour, reopened.Files.ToArray());
        Assert.AreEqual("La méiose.", Encoding.UTF8.GetString(reopened.Read("fiches/Biologie/Méiose.md")));
        Assert.IsTrue(here.ContainsFile("fiches/Biologie/Méiose.md"), "ce poste voit aussi ce que l'autre a ajouté");
    }

    [TestMethod]
    public void A_move_here_and_a_purge_there_both_happen()
    {
        using var folder = Prepared(out var key);
        using var here = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);
        using var there = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);

        there.Purge("fiches/Histoire/Révolution.md");
        there.CreateFolder("fiches/Géographie");
        here.MoveFile("fiches/Biologie/Mitose.md", "fiches/Biologie/Division/Mitose.md");

        using var reopened = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);
        CollectionAssert.AreEqual(MovedOnly, reopened.Files.ToArray());
        Assert.AreEqual("La mitose.", Encoding.UTF8.GetString(reopened.Read("fiches/Biologie/Division/Mitose.md")));
        Assert.IsTrue(reopened.ContainsFolder("fiches/Géographie"));
        Assert.IsTrue(reopened.ContainsFolder("fiches/Biologie/Division"));
    }

    [TestMethod]
    public void The_same_path_changed_on_both_keeps_what_was_written_last_and_a_case_change_survives()
    {
        using var folder = Prepared(out var key);
        using var here = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);
        using var there = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);

        there.Save("fiches/Nouvelle.md", "là-bas"u8);
        here.Save("fiches/Nouvelle.md", "ici"u8);
        here.MoveFile("fiches/Biologie/Mitose.md", "fiches/Biologie/MITOSE.md");

        using var reopened = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);
        Assert.AreEqual("ici", Encoding.UTF8.GetString(reopened.Read("fiches/Nouvelle.md")));
        Assert.Contains("fiches/Biologie/MITOSE.md", reopened.Files);
    }

    [TestMethod]
    public void An_index_that_cannot_be_read_on_the_disk_is_written_over_as_before()
    {
        using var folder = Prepared(out var key);
        using var here = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);
        File.WriteAllText(Path.Combine(folder.Root, EncryptedVault.IndexFile), "abîmé par une synchronisation pas finie");

        here.Save("fiches/Nouvelle.md", "ici"u8);

        using var reopened = EncryptedVault.Open(folder.Root, key, DiskStorage.Instance);
        Assert.HasCount(3, reopened.Files);
    }

    /// <summary>Un coffre chiffré de deux fiches, fermé.</summary>
    private static TemporaryFolder Prepared(out byte[] key)
    {
        var folder = new TemporaryFolder();
        var (header, dataKey) = Shared.Value;
        key = dataKey;
        using var vault = EncryptedVault.Create(folder.Root, header, dataKey, DiskStorage.Instance);
        vault.Save("fiches/Biologie/Mitose.md", "La mitose."u8);
        vault.Save("fiches/Histoire/Révolution.md", "La Révolution."u8);
        return folder;
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
