using System.Diagnostics;
using System.Text;
using KyfRev.Cli;
using KyfRev.Infrastructure.Vault.Analysis;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>Analyse en lecture seule du coffre et outil <c>kyfrev-coffre verifier</c>.</summary>
[TestClass]
public sealed class InspectorTests
{
    private const string Revolution = "f_01M69JFV2X5VM53EY3DR1BY61M";
    private const string Mitose = "f_01M69JFV54D3MA7W49Z91KQBM7";

    [TestMethod]
    public void The_reference_vault_is_healthy()
    {
        var inspection = VaultInspector.Inspect(TemporaryVault.Reference);

        Assert.IsTrue(inspection.IsVault);
        Assert.IsFalse(inspection.HasErrors);
        Assert.IsEmpty(inspection.Findings.Where(f => f.Severity != FindingSeverity.Information));
        var s = inspection.Summary;
        Assert.AreEqual("v_01M69JFV2A3WD8KH55EHWC1G7N", s.VaultId);
        Assert.AreEqual((6, 3, 3, 0), (s.TrackedSheets, s.ActiveSheets, s.ArchivedSheets, s.UntrackedFiles));
        Assert.AreEqual((5, 1, 1, 1), (s.Annotations, s.Definitions, s.TrashedAnnotations, s.TrashedSheets));
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
        vault.Write("fiches/Sans suivi.md", text.Replace(Revolution, "f_01M69JFV2X5VM53EY3DR1BY61Z", StringComparison.Ordinal));
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
        vault.Write($".suivi/versions/{Mitose}/notes.txt", "?");

        var inspection = VaultInspector.Inspect(vault.Root);
        var codes = Codes(inspection);

        Assert.AreEqual(2, codes.Count(c => c == "schema-illisible"));
        Assert.Contains("donnees-extension-illisibles", codes);
        Assert.Contains("fichier-inattendu", codes);
        Assert.Contains("version-inconnue", codes);
        Assert.AreEqual(3, inspection.Summary.Schemas);
    }

    [TestMethod]
    public void A_vault_reduced_to_its_settings_is_healthy()
    {
        using var vault = TemporaryVault.Empty();
        vault.Write(".suivi/coffre.json", """{ "format": 1, "id": "v_01M69JFV2A3WD8KH55EHWC1G7N", "creeLe": "2026-10-09" }""");
        vault.Write(".suivi/citation-en-ligne.json", """{ "format": 1 }""");

        var inspection = VaultInspector.Inspect(vault.Root);

        Assert.IsTrue(inspection.IsVault);
        Assert.IsEmpty(inspection.Findings);
        var s = inspection.Summary;
        Assert.AreEqual((0, 0, 0, 0), (s.TrackedSheets, s.Annotations, s.Versions, s.Schemas));
    }

    [TestMethod]
    public void Files_under_a_wrong_name_or_too_large_are_reported()
    {
        using var vault = TemporaryVault.CopyOfReference();
        File.Copy(vault.PathOf($".suivi/fiches/{Revolution}.json"), vault.PathOf(".suivi/fiches/f_01M69JFV2X5VM53EY3DR1BY61Z.json"));
        File.Copy(vault.PathOf($".suivi/fiches/{Revolution}.json"), vault.PathOf(".suivi/fiches/f_01M69JFV2X5VM53EY3DR1BY6-M.json"));
        File.Copy(vault.PathOf($".suivi/annotations/{Revolution}.json"), vault.PathOf($".suivi/annotations/{Mitose}.json"));
        vault.Write($"fiches/{new string('é', 130)}.md", "# Nom de plus de 255 octets\n");
        vault.Write("schemas/s_00000000000000000000000000.json", new string(' ', (8 * 1024 * 1024) + 1));
        vault.Write(".suivi/extensions/lourde.json", new string(' ', (1024 * 1024) + 1));

        var findings = VaultInspector.Inspect(vault.Root).Findings;

        Assert.Contains(f => f.Code == "nom-refuse" && f.Message.Contains("trop long", StringComparison.Ordinal), findings);
        Assert.Contains(f => f.Code == "suivi-illisible" && f.Message.Contains("ne correspond pas au nom", StringComparison.Ordinal), findings);
        Assert.Contains(f => f.Code == "fichier-inattendu" && f.Path == ".suivi/fiches/f_01M69JFV2X5VM53EY3DR1BY6-M.json", findings);
        Assert.Contains(f => f.Code == "annotations-illisibles" && f.Path == $".suivi/annotations/{Mitose}.json", findings);
        Assert.Contains(f => f.Code == "schema-illisible" && f.Message.Contains("8 Mio", StringComparison.Ordinal), findings);
        Assert.Contains(f => f.Code == "donnees-extension-illisibles" && f.Message.Contains("1 Mio", StringComparison.Ordinal), findings);
    }

    [TestMethod]
    public void A_file_locked_by_another_program_is_reported_and_the_rest_is_inspected()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var locked = new FileStream(vault.PathOf(".suivi/rappels.json"), FileMode.Open, FileAccess.Read, FileShare.None);

        var inspection = VaultInspector.Inspect(vault.Root);

        Assert.AreEqual(".suivi/rappels.json", inspection.Findings.Single(f => f.Code == "lecture-impossible").Path);
        Assert.AreEqual(6, inspection.Summary.TrackedSheets);
    }

    [TestMethod]
    public void The_command_line_report_is_readable_and_machine_readable()
    {
        var (exitCode, text) = Run("verifier", TemporaryVault.Reference);
        Assert.AreEqual(VaultCommands.Success, exitCode);
        Assert.Contains("Aucun problème.", text);
        Assert.Contains("Fiches : 6 suivie(s) présente(s)", text);
        Assert.Contains("rien n'a été écrit dans le coffre", text);

        var (_, json) = Run("verifier", TemporaryVault.Reference, "--json");
        var report = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        Assert.IsTrue((bool?)report["estUnCoffre"]);
        Assert.IsFalse((bool?)report["erreurs"]);
        Assert.AreEqual(6, (int?)report["resume"]!["fichesSuivies"]);
        Assert.AreEqual("info", (string?)report["constats"]![0]!["gravite"]);
    }

    [TestMethod]
    public void The_json_report_names_each_severity()
    {
        using var vault = TemporaryVault.CopyOfReference();
        vault.Write(".suivi/rappels.json", "[]");
        File.Move(vault.PathOf($".suivi/fiches/{Mitose}.json"), vault.PathOf($".suivi/{Mitose}.json.bak"));

        var (exitCode, json) = Run("verifier", vault.Root, "--json");

        Assert.AreEqual(VaultCommands.ErrorsFound, exitCode);
        var severities = System.Text.Json.Nodes.JsonNode.Parse(json)!["constats"]!.AsArray().Select(f => (string?)f!["gravite"]).ToHashSet();
        Assert.IsTrue(severities.SetEquals(["erreur", "attention", "info"]), string.Join(", ", severities));
    }

    [TestMethod]
    public void The_tool_run_as_a_program_prints_accents_and_returns_its_exit_code()
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "kyfrev-coffre.exe"))
        {
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("verifier");
        start.ArgumentList.Add(TemporaryVault.Reference);

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Assert.AreEqual(VaultCommands.Success, process.ExitCode);
        Assert.Contains("rien n'a été écrit dans le coffre", output);
    }

    private static List<string> Codes(VaultInspection inspection) => [.. inspection.Findings.Select(f => f.Code)];

    private static (int ExitCode, string Output) Run(params string[] args)
    {
        using var output = new StringWriter();
        var exitCode = VaultCommands.Build(output).Parse(args).Invoke();
        return (exitCode, output.ToString());
    }
}
