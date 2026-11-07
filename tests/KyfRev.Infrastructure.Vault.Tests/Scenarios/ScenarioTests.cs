using System.Text.Json;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Ids;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests.Scenarios;

/// <summary>
/// Opérations sur un coffre comparées à la version 0.x : chaque scénario de
/// <c>fixtures/scenarios</c> a été joué par le crate <c>coffre</c> (voir
/// <c>tools/reference-vectors --scenarios</c>) ; rejoué ici avec les mêmes identifiants, il doit
/// donner les mêmes résultats et un coffre identique à l'octet près, dossiers vides compris.
/// </summary>
[TestClass]
public sealed class ScenarioTests
{
    private const string LockInfo = ".suivi/verrou-info.json";

    private static string Folder => Path.Combine(AppContext.BaseDirectory, "fixtures", "scenarios");

    /// <summary>Chaque scénario, joué sur le disque tel quel puis sur un coffre chiffré.</summary>
    public static IEnumerable<object[]> Scenarios =>
        Directory.GetDirectories(Folder).Order(StringComparer.Ordinal)
            .SelectMany(path => Enum.GetValues<StorageKind>().Select(storage => new object[] { Path.GetFileName(path), storage }));

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void Operations_give_the_same_vault_as_version_0x(string scenario, StorageKind storage)
    {
        var folder = Path.Combine(Folder, scenario);
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "scenario.json")));
        var ids = UlidGenerator.Replaying(File.ReadAllLines(Path.Combine(folder, "identifiants.txt")).Where(line => line.Length > 0));
        using var vault = TemporaryVault.Empty(storage);

        var results = new List<string>();
        using (var runner = new ScenarioRunner(vault, ids))
        {
            var n = 0;
            foreach (var operation in document.RootElement.GetProperty("operations").EnumerateArray())
            {
                results.Add(runner.Play(n++, operation));
            }
        }
        File.Delete(vault.PathOf(LockInfo));

        var expected = File.ReadAllLines(Path.Combine(folder, "resultats.txt"));
        var differences = expected.Zip(results).Where(pair => pair.First != pair.Second).Select(pair => $"0.x : {pair.First}\nC#  : {pair.Second}").ToList();
        Assert.IsEmpty(differences, $"Résultats différents :\n{string.Join("\n\n", differences.Take(5))}");
        Assert.HasCount(expected.Length, results);

        var expectedTree = Files(Path.Combine(folder, "attendu"));
        var actualTree = vault.Files();
        CollectionAssert.AreEqual(expectedTree.Keys.ToList(), actualTree.Keys.ToList(), $"Fichiers différents.\n0.x : {string.Join(", ", expectedTree.Keys.Except(actualTree.Keys))}\nC#  : {string.Join(", ", actualTree.Keys.Except(expectedTree.Keys))}");
        foreach (var (path, bytes) in expectedTree)
        {
            CollectionAssert.AreEqual(bytes, actualTree[path], $"Contenu différent : {path}\n0.x : {Show(bytes)}\nC#  : {Show(actualTree[path])}");
        }
        CollectionAssert.AreEqual(File.ReadAllLines(Path.Combine(folder, "dossiers.txt")), vault.Folders(), "Dossiers différents.");

        if (storage == StorageKind.Encrypted)
        {
            // Sur le disque, rien d'autre que l'en-tête, l'index, un objet par fichier du coffre et le verrou : aucun nom de
            // fiche, aucun suivi en clair.
            var onDisk = Directory.EnumerateFiles(vault.Root, "*", SearchOption.AllDirectories).Select(file => Relative(vault.Root, file)).ToList();
            var objects = onDisk.Count(path => path.StartsWith(EncryptedVault.Objects + "/", StringComparison.Ordinal));
            Assert.IsTrue(onDisk.All(path => path is EncryptedVault.HeaderFile or EncryptedVault.IndexFile or VaultLayout.LockFile || path.StartsWith(EncryptedVault.Objects + "/", StringComparison.Ordinal)), string.Join(", ", onDisk));
            Assert.AreEqual(vault.Storage.AllEntries(vault.Root).Count(entry => !entry.IsDirectory), objects, "un objet par fichier du coffre");
        }
    }

    private static SortedDictionary<string, byte[]> Files(string root)
    {
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            files[Relative(root, file)] = File.ReadAllBytes(file);
        }
        return files;
    }

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string Show(byte[] bytes) => System.Text.Encoding.UTF8.GetString(bytes);
}
