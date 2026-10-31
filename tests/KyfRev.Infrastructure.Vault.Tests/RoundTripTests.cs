using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// Chaque fichier JSON du coffre de référence, écrit par la version 0.x, est relu puis réécrit
/// identique à l'octet près.
/// </summary>
[TestClass]
public sealed class RoundTripTests
{
    public static IEnumerable<object[]> ReferenceFiles =>
        Directory.EnumerateFiles(TemporaryVault.Reference, "*.json", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(TemporaryVault.Reference, file).Replace('\\', '/'))
            .Where(relative => !relative.StartsWith("schemas/", StringComparison.Ordinal) && !relative.StartsWith(".suivi/extensions/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(relative => new object[] { relative });

    [TestMethod]
    public void The_reference_vault_covers_every_kind_of_json_file()
    {
        var files = ReferenceFiles.Select(f => (string)f[0]).ToList();
        string[] expected =
        [
            ".suivi/annotations/", ".suivi/coffre.json", ".suivi/corbeille/", ".suivi/fiches/",
            ".suivi/operation.json", ".suivi/rappels.json", ".suivi/verrou-info.json", "fiches/.supprimees/",
        ];
        foreach (var prefix in expected)
        {
            Assert.Contains(f => f.StartsWith(prefix, StringComparison.Ordinal), files, $"aucun fichier « {prefix} » dans le corpus");
        }
    }

    [TestMethod]
    [DynamicData(nameof(ReferenceFiles))]
    public void Reference_file_is_rewritten_byte_for_byte(string relative)
    {
        var original = File.ReadAllBytes(Path.Combine([TemporaryVault.Reference, .. relative.Split('/')]));
        var rewritten = relative switch
        {
            ".suivi/coffre.json" => VaultSettingsFile.Read(original).Write(),
            ".suivi/rappels.json" => DrawJournalFile.Read(original).Write(),
            ".suivi/citation-en-ligne.json" => OnlineQuoteFile.Read(original).Write(),
            ".suivi/verrou-info.json" => LockInfoFile.Read(original).Write(),
            ".suivi/operation.json" => OperationJournalFile.Read(original).Write(),
            _ when relative.EndsWith("/corbeille.json", StringComparison.Ordinal) => TrashInfoFile.Read(original).Write(),
            _ when relative.EndsWith(".annotations.json", StringComparison.Ordinal) || relative.StartsWith(".suivi/annotations/", StringComparison.Ordinal) => AnnotationsFile.Read(original).Write(),
            _ => TrackedSheetFile.Read(original).Write(),
        };
        Assert.AreEqual(System.Text.Encoding.UTF8.GetString(original), System.Text.Encoding.UTF8.GetString(rewritten));
        Assert.AreSequenceEqual(original, rewritten);
    }
}
