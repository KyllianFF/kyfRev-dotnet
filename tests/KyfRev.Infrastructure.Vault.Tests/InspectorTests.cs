using KyfRev.Cli;
using KyfRev.Infrastructure.Vault.Analysis;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>Analyse en lecture seule du coffre et outil <c>kyfrev-coffre verifier</c>.</summary>
[TestClass]
public sealed class InspectorTests
{
    private const string Revolution = "f_01M69GHG4NJ7P3M0J2ZMPHF7YG";

    [TestMethod]
    public void The_reference_vault_is_healthy()
    {
        var inspection = VaultInspector.Inspect(TemporaryVault.Reference);

        Assert.IsTrue(inspection.IsVault);
        Assert.IsFalse(inspection.HasErrors);
        Assert.IsEmpty(inspection.Findings.Where(f => f.Severity != FindingSeverity.Information));
        var s = inspection.Summary;
        Assert.AreEqual("v_01M69GHG41N8QHVEVEPY13FDPX", s.VaultId);
        Assert.AreEqual((4, 2, 2, 0), (s.TrackedSheets, s.ActiveSheets, s.ArchivedSheets, s.UntrackedFiles));
        Assert.AreEqual((2, 1, 1, 1), (s.Annotations, s.Definitions, s.TrashedAnnotations, s.TrashedSheets));
        Assert.AreEqual((2, 2, 1, 1), (s.Versions, s.Quotes, s.Schemas, s.ExtensionData));
    }

    [TestMethod]
    public void Inspecting_writes_nothing_in_the_vault()
    {
        using var vault = TemporaryVault.CopyOfReference();
        vault.Write("fiches/Ajoutée à la main.md", "# Sans en-tête\n");
        var before = vault.Snapshot();

        _ = VaultInspector.Inspect(vault.Root);
        _ = Run("verifier", vault.Root, "--json");

        CollectionAssert.AreEqual(before, vault.Snapshot());
    }

    [TestMethod]
    public void A_folder_that_is_not_a_vault_is_reported()
    {
        using var folder = TemporaryVault.Empty();
        var inspection = VaultInspector.Inspect(folder.Root);
        Assert.IsFalse(inspection.IsVault);
        Assert.AreEqual("pas-un-coffre", Assert.ContainsSingle(inspection.Findings).Code);
        Assert.AreEqual(VaultCommands.NotAVault, Run("verifier", folder.Root).ExitCode);
    }

    [TestMethod]
    public void Sheets_moved_modified_missing_duplicated_or_untracked_are_reported()
    {
        using var vault = TemporaryVault.CopyOfReference();
        var revolution = vault.PathOf("fiches/Histoire/Révolution française - les causes.md");
        var text = File.ReadAllText(revolution);
        File.Move(revolution, vault.PathOf("fiches/Révolution.md"));
        File.AppendAllText(vault.PathOf("fiches/Biologie/Mitose.md"), "\nAjout fait ailleurs.\n");
        File.Delete(vault.PathOf("fiches/Mathématiques/Théorème de Pythagore.md"));
        File.Copy(vault.PathOf("fiches/Nouveau titre.md"), vault.PathOf("fiches/Copie.md"));
        vault.Write("fiches/Sans suivi.md", text.Replace(Revolution, "f_01M69GHG4NJ7P3M0J2ZMPHF7YZ", StringComparison.Ordinal));
        vault.Write("fiches/Libre.md", "# Note libre\n");

        var codes = Codes(VaultInspector.Inspect(vault.Root));

        Assert.Contains("fiche-deplacee", codes);
        Assert.Contains("fiche-modifiee", codes);
        Assert.Contains("fiche-manquante", codes);
        Assert.Contains("doublon", codes);
        Assert.Contains("sans-suivi", codes);
        Assert.AreEqual(1, VaultInspector.Inspect(vault.Root).Summary.UntrackedFiles);
    }

    [TestMethod]
    public void Unreadable_files_are_errors_and_are_left_untouched()
    {
        using var vault = TemporaryVault.CopyOfReference();
        var tracking = vault.PathOf($".suivi/fiches/{Revolution}.json");
        File.WriteAllText(tracking, File.ReadAllText(tracking).Replace("\"cyclesCommences\": 1", "\"cyclesCommences\": 7", StringComparison.Ordinal));
        vault.Write("fiches/En-tête cassé.md", "---\nid: [cassé\n---\n");
        vault.Write("fiches/Mauvais identifiant.md", "---\nid: f_court\n---\n");
        File.WriteAllBytes(vault.PathOf("fiches/Latin1.md"), [0x65, 0xE9, 0x0A]);
        vault.Write($".suivi/annotations/{Revolution}.json", "{ \"format\": 1, ");
        vault.Write(".suivi/rappels.json", "[]");
        var before = vault.Snapshot();

        var inspection = VaultInspector.Inspect(vault.Root);
        var codes = Codes(inspection);

        Assert.IsTrue(inspection.HasErrors);
        foreach (var code in new[] { "suivi-illisible", "en-tete-illisible", "identifiant-invalide", "fiche-illisible", "annotations-illisibles", "tirages-illisibles" })
        {
            Assert.Contains(code, codes);
        }
        Assert.Contains("compteurs ne correspondent pas", inspection.Findings.First(f => f.Code == "suivi-illisible").Message);
        CollectionAssert.AreEqual(before, vault.Snapshot());
        Assert.AreEqual(VaultCommands.ErrorsFound, Run("verifier", vault.Root).ExitCode);
    }

    [TestMethod]
    public void A_newer_format_an_interrupted_operation_and_orphan_annotations_are_reported()
    {
        using var vault = TemporaryVault.CopyOfReference();
        var settings = vault.PathOf(".suivi/coffre.json");
        File.WriteAllText(settings, File.ReadAllText(settings).Replace("\"format\": 1", "\"format\": 2", StringComparison.Ordinal));
        var operation = vault.PathOf(".suivi/operation.json");
        File.WriteAllText(operation, File.ReadAllText(operation).Replace("\"terminee\"", "\"en_cours\"", StringComparison.Ordinal));
        File.Move(vault.PathOf($".suivi/fiches/{Revolution}.json"), vault.PathOf($".suivi/{Revolution}.json.bak"));

        var codes = Codes(VaultInspector.Inspect(vault.Root));

        Assert.Contains("format-plus-recent", codes);
        Assert.Contains("operation-interrompue", codes);
        Assert.Contains("annotations-orphelines", codes);
        Assert.Contains("sans-suivi", codes);
    }

    [TestMethod]
    public void Schemas_and_extension_data_are_checked()
    {
        using var vault = TemporaryVault.CopyOfReference();
        vault.Write("schemas/s_00000000000000000000000000.json", "{ \"format\": 3 }");
        vault.Write("schemas/s_11111111111111111111111111.json", "{ \"format\": 2, \"id\": \"s_22222222222222222222222222\" }");
        vault.Write(".suivi/extensions/vocabulaire.json", "pas du JSON");
        vault.Write(".suivi/extensions/Majuscule.json", "{}");
        vault.Write(".suivi/versions/f_01M69GHG5QARE6JHS9156AWPN7/notes.txt", "?");

        var inspection = VaultInspector.Inspect(vault.Root);
        var codes = Codes(inspection);

        Assert.AreEqual(2, codes.Count(c => c == "schema-illisible"));
        Assert.Contains("donnees-extension-illisibles", codes);
        Assert.Contains("fichier-inattendu", codes);
        Assert.Contains("version-inconnue", codes);
        Assert.AreEqual(3, inspection.Summary.Schemas);
    }

    [TestMethod]
    public void The_command_line_report_is_readable_and_machine_readable()
    {
        var (exitCode, text) = Run("verifier", TemporaryVault.Reference);
        Assert.AreEqual(VaultCommands.Success, exitCode);
        Assert.Contains("Aucun problème.", text);
        Assert.Contains("Fiches : 4 suivie(s) présente(s)", text);
        Assert.Contains("rien n'a été écrit dans le coffre", text);

        var (_, json) = Run("verifier", TemporaryVault.Reference, "--json");
        var report = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        Assert.IsTrue((bool?)report["estUnCoffre"]);
        Assert.IsFalse((bool?)report["erreurs"]);
        Assert.AreEqual(4, (int?)report["resume"]!["fichesSuivies"]);
        Assert.AreEqual("info", (string?)report["constats"]![0]!["gravite"]);
    }

    private static List<string> Codes(VaultInspection inspection) => [.. inspection.Findings.Select(f => f.Code)];

    private static (int ExitCode, string Output) Run(params string[] args)
    {
        using var output = new StringWriter();
        var exitCode = VaultCommands.Build(output).Parse(args).Invoke();
        return (exitCode, output.ToString());
    }
}
