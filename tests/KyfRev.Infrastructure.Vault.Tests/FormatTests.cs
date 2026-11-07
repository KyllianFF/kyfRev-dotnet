using System.Text;
using System.Text.Json.Nodes;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>Lecture tolérante, conservation des champs inconnus et refus des fichiers incohérents.</summary>
[TestClass]
public sealed class FormatTests
{
    private const string Settings = """
        {
          "format": 1,
          "id": "v_01JA8Z3K5Q6WXYZABCDEFGHJKM",
          "creeLe": "2026-10-09"
        }
        """;

    private const string Sheet = """
        {
          "historique": [
            { "le": "2026-10-09T14:00:00+02:00", "evt": "revision", "jour": "2026-10-09", "cycle": 1, "etape": "J0", "prevuLe": "2026-10-09", "retardJours": 0, "origine": "creation" }
          ],
          "compteurs": { "cyclesCommences": 1, "cyclesTermines": 0, "cyclesInterrompus": 0 },
          "cycle": { "numero": 1, "sequence": [0, 1, 3], "demarreLe": "2026-10-09", "etape": 1, "echeance": "2026-10-10", "derniereRevision": "2026-10-09" },
          "statut": "active",
          "format": 1,
          "id": "f_01JA8Z3K5Q6WXYZABCDEFGHJKM",
          "chemin": "fiches\\Histoire\\Révolution.md",
          "titre": "Révolution",
          "etiquettes": null,
          "empreinte": "sha256:00",
          "modifieeLe": "2026-10-09T12:00:00.123456789Z"
        }
        """;

    [TestMethod]
    public void Missing_settings_take_the_same_defaults_as_version_0x()
    {
        var settings = VaultSettingsFile.Read(Encoding.UTF8.GetBytes(Settings));
        Assert.AreEqual(ReviewSequence.Default, settings.Sequence);
        Assert.IsTrue(settings.ActiveRecall);
        Assert.AreEqual(2, settings.Reminders.MinimumDelayMonths);
        Assert.IsFalse(settings.Quotes.Active);
        Assert.IsTrue(settings.Quotes.Online);
        Assert.HasCount(4, settings.Quotes.Domains);
        Assert.IsTrue(settings.BackupRotation);
        Assert.IsNull(settings.SchemasModule);
        Assert.IsEmpty(settings.Exams);

        var written = Encoding.UTF8.GetString(settings.Write());
        Assert.Contains("\"rappelActif\": true", written);
        Assert.DoesNotContain("modules", written);
        Assert.DoesNotContain("examens", written);
    }

    [TestMethod]
    public void Unknown_settings_are_kept_top_level_sorted_and_sections_in_order()
    {
        const string json = """
            {
              "format": 1, "id": "v_X", "creeLe": "2026-10-09",
              "zeta": 1, "affichage": { "theme": "sombre" },
              "sauvegardes": { "rotation": false, "quotidienne": true }
            }
            """;
        var settings = VaultSettingsFile.Read(Encoding.UTF8.GetBytes(json));
        var written = JsonNode.Parse(settings.Write())!.AsObject();

        string[] keys = ["format", "id", "creeLe", "revision", "rappels", "citations", "sauvegardes", "affichage", "zeta"];
        Assert.AreSequenceEqual(keys, written.Select(pair => pair.Key));
        Assert.AreEqual("sombre", (string?)written["affichage"]!["theme"]);
        Assert.IsTrue((bool?)written["sauvegardes"]!["quotidienne"]);
        Assert.IsFalse((bool?)written["sauvegardes"]!["rotation"]);
    }

    [TestMethod]
    public void A_settings_section_present_without_its_required_value_is_refused()
    {
        const string json = """{ "format": 1, "id": "v_X", "creeLe": "2026-10-09", "sauvegardes": { "quotidienne": true } }""";
        var error = Assert.ThrowsExactly<VaultFormatException>(() => VaultSettingsFile.Read(Encoding.UTF8.GetBytes(json)));
        Assert.Contains("rotation", error.Message);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(""", "verrouillage": {}""")]
    [DataRow(""", "verrouillage": null""")]
    [DataRow(""", "verrouillage": { "apresMinutes": null }""")]
    [DataRow(""", "verrouillage": { "apresMinutes": 15 }""")]
    public void The_lock_delay_is_15_minutes_by_default_and_then_not_written(string section)
    {
        var json = $$"""{ "format": 1, "id": "v_X", "creeLe": "2026-10-09"{{section}} }""";
        var settings = VaultSettingsFile.Read(Encoding.UTF8.GetBytes(json));

        Assert.AreEqual(VaultPreferences.DefaultLockAfterMinutes, settings.LockAfterMinutes);
        Assert.DoesNotContain("verrouillage", Encoding.UTF8.GetString(settings.Write()), "un coffre de la 0.x se réécrit tel quel");
        Assert.DoesNotContain("verrouillage", Encoding.UTF8.GetString(VaultSettingsFile.New("v_X", new DateOnly(2026, 10, 9)).Write()));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(5)]
    [DataRow(60)]
    [DataRow(VaultSettingsFile.MaxLockAfterMinutes)]
    public void A_lock_delay_other_than_the_default_is_written_after_the_known_sections_and_read_back(int minutes)
    {
        var written = (VaultSettingsFile.Read(Encoding.UTF8.GetBytes(Settings)) with { LockAfterMinutes = minutes }).Write();
        var root = JsonNode.Parse(written)!.AsObject();

        Assert.AreEqual("verrouillage", root.Last().Key);
        Assert.AreEqual(minutes, (int?)root["verrouillage"]!["apresMinutes"]);
        var read = VaultSettingsFile.Read(written);
        Assert.AreEqual(minutes, read.LockAfterMinutes);
        CollectionAssert.AreEqual(written, read.Write());
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("1441")]
    [DataRow("15.5")]
    [DataRow("\"15\"")]
    [DataRow("true")]
    public void A_lock_delay_that_is_not_a_whole_number_of_minutes_up_to_a_day_is_refused(string minutes)
    {
        var json = $$"""{ "format": 1, "id": "v_X", "creeLe": "2026-10-09", "verrouillage": { "apresMinutes": {{minutes}} } }""";
        var error = Assert.ThrowsExactly<VaultFormatException>(() => VaultSettingsFile.Read(Encoding.UTF8.GetBytes(json)));
        Assert.Contains("apresMinutes", error.Message);
    }

    [TestMethod]
    public void Unknown_properties_of_the_lock_section_are_kept_even_with_the_default_delay()
    {
        const string json = """
            {
              "format": 1, "id": "v_X", "creeLe": "2026-10-09",
              "zeta": 1,
              "verrouillage": { "enVeille": true, "apresMinutes": 60 }
            }
            """;
        var settings = VaultSettingsFile.Read(Encoding.UTF8.GetBytes(json));
        var written = JsonNode.Parse(settings.Write())!.AsObject();

        Assert.AreEqual(60, settings.LockAfterMinutes);
        string[] keys = ["format", "id", "creeLe", "revision", "rappels", "citations", "sauvegardes", "verrouillage", "zeta"];
        Assert.AreSequenceEqual(keys, written.Select(pair => pair.Key));
        string[] section = ["apresMinutes", "enVeille"];
        Assert.AreSequenceEqual(section, written["verrouillage"]!.AsObject().Select(pair => pair.Key));

        var byDefault = JsonNode.Parse((settings with { LockAfterMinutes = VaultPreferences.DefaultLockAfterMinutes }).Write())!.AsObject();
        Assert.IsFalse(byDefault["verrouillage"]!.AsObject().ContainsKey("apresMinutes"));
        Assert.IsTrue((bool?)byDefault["verrouillage"]!["enVeille"], "la section reste pour ce qu'elle a d'inconnu");
    }

    [TestMethod]
    public void A_tracking_is_read_whatever_the_key_order_offsets_and_precision()
    {
        var sheet = TrackedSheetFile.Read(Encoding.UTF8.GetBytes(Sheet));

        Assert.AreEqual("fiches/Histoire/Révolution.md", sheet.Path.Value);
        Assert.IsEmpty(sheet.Tags);
        Assert.AreEqual(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero), sheet.Tracking.History[0].At);
        Assert.AreEqual(1_234_567, sheet.ModifiedAt.Ticks % TimeSpan.TicksPerSecond, "fraction tronquée à la précision de .NET");

        var written = Encoding.UTF8.GetString(sheet.Write());
        Assert.Contains("\"le\": \"2026-10-09T12:00:00Z\"", written);
        Assert.Contains("\"modifieeLe\": \"2026-10-09T12:00:00.1234567Z\"", written);
        Assert.IsLessThan(written.IndexOf("\"statut\"", StringComparison.Ordinal), written.IndexOf("\"modifieeLe\"", StringComparison.Ordinal));
        Assert.DoesNotContain("etiquettes", written);
    }

    [TestMethod]
    public void Unknown_fields_of_a_tracking_file_are_kept_after_the_known_ones()
    {
        var json = Sheet.Replace("\"format\": 1,", "\"format\": 1, \"couleur\": \"vert\",", StringComparison.Ordinal);
        var written = JsonNode.Parse(TrackedSheetFile.Read(Encoding.UTF8.GetBytes(json)).Write())!.AsObject();
        Assert.AreEqual("couleur", written.Last().Key);
        Assert.AreEqual("vert", (string?)written["couleur"]);
    }

    [TestMethod]
    [DataRow("\"cyclesTermines\": 0", "\"cyclesTermines\": 3", "compteurs")]
    [DataRow("\"sequence\": [0, 1, 3]", "\"sequence\": [0, 3, 1]", "strictement croissantes")]
    [DataRow("\"evt\": \"revision\"", "\"evt\": \"inconnu\"", "inconnu")]
    [DataRow("\"statut\": \"active\"", "\"statut\": \"en_pause\"", "en_pause")]
    [DataRow("\"cycle\": 1,", "\"cycle\": -1,", "entier")]
    [DataRow("\"jour\": \"2026-10-09\"", "\"jour\": \"09/10/2026\"", "AAAA-MM-JJ")]
    [DataRow("\"chemin\": \"fiches\\\\Histoire\\\\Révolution.md\"", "\"chemin\": \"../secret.md\"", "refusé")]
    [DataRow("\"modifieeLe\": \"2026-10-09T12:00:00.123456789Z\"", "\"modifieeLe\": \"2026-10-09 midi\"", "RFC 3339")]
    public void Inconsistent_or_malformed_trackings_are_refused(string original, string replacement, string expected)
    {
        var json = Sheet.Replace(original, replacement, StringComparison.Ordinal);
        Assert.AreNotEqual(Sheet, json, "le remplacement doit modifier le fichier");
        var error = Assert.ThrowsExactly<VaultFormatException>(() => TrackedSheetFile.Read(Encoding.UTF8.GetBytes(json)));
        Assert.Contains(expected, error.Message);
    }

    [TestMethod]
    [DataRow("{ \"format\": 1 ")]
    [DataRow("[]")]
    [DataRow("{ \"format\": 1, }")]
    public void Invalid_json_is_refused(string json) =>
        Assert.ThrowsExactly<VaultFormatException>(() => DrawJournalFile.Read(Encoding.UTF8.GetBytes(json)));

    [TestMethod]
    public void An_annotation_of_another_sheet_is_refused()
    {
        var json = JsonNode.Parse(File.ReadAllBytes(ReferenceFiles(".suivi/annotations")[0]))!.AsObject();
        json["ficheId"] = "f_00000000000000000000000000";
        var error = Assert.ThrowsExactly<VaultFormatException>(() => AnnotationsFile.Read(Encoding.UTF8.GetBytes(json.ToJsonString())));
        Assert.Contains("appartient à la fiche", error.Message);
    }

    /// <summary>Valeurs qu'une version plus récente pourrait écrire : le fichier est refusé, donc jamais réécrit.</summary>
    [TestMethod]
    [DataRow(".suivi/fiches", "\"origine\": \"adoption\"", "\"origine\": \"import\"", "origine « import »")]
    [DataRow(".suivi/fiches", "\"raison\": \"manuelle\"", "\"raison\": \"oubli\"", "raison d'archivage")]
    [DataRow(".suivi/fiches", "\"resultat\": \"connue\"", "\"resultat\": \"bof\"", "résultat de rappel")]
    [DataRow(".suivi/fiches", "\"inclure\": false", "\"inclure\": \"non\"", "vrai ou faux")]
    [DataRow(".suivi/fiches", "\"historique\": [", "\"historique\": [\n    1,", "doit être un objet")]
    [DataRow(".suivi/annotations", "\"objet\": \"commentaire\"", "\"objet\": \"couleur\"", "objet modifié")]
    [DataRow(".suivi/annotations", "\"etatAncrage\": \"a_verifier\"", "\"etatAncrage\": \"perdue\"", "état d'ancrage")]
    [DataRow(".suivi", "\"etat\": \"terminee\"", "\"etat\": \"annulee\"", "état d'opération")]
    public void Values_unknown_to_this_version_are_refused(string folder, string original, string replacement, string expected)
    {
        var file = ReferenceFiles(folder).First(f => File.ReadAllText(f).Contains(original, StringComparison.Ordinal));
        var json = Encoding.UTF8.GetBytes(File.ReadAllText(file).Replace(original, replacement, StringComparison.Ordinal));
        Action read = folder switch
        {
            ".suivi/fiches" => () => TrackedSheetFile.Read(json),
            ".suivi/annotations" => () => AnnotationsFile.Read(json),
            _ => () => OperationJournalFile.Read(json),
        };
        var error = Assert.ThrowsExactly<VaultFormatException>(read);
        Assert.Contains(expected, error.Message);
    }

    [TestMethod]
    public void A_format_error_without_details_still_has_a_message() =>
        Assert.AreEqual("Fichier du coffre illisible.", new VaultFormatException().Message);

    [TestMethod]
    public void Online_quote_writes_explicit_nulls_and_lock_info_has_no_final_newline()
    {
        var quote = OnlineQuoteFile.Read("""{ "format": 1 }"""u8);
        Assert.AreEqual("{\n  \"format\": 1,\n  \"jour\": null,\n  \"chemin\": null,\n  \"page\": null\n}\n", Encoding.UTF8.GetString(quote.Write()));

        var info = new LockInfoFile("POSTE", 42, new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero), UnknownProperties.None);
        Assert.AreEqual("{\n  \"machine\": \"POSTE\",\n  \"processus\": 42,\n  \"depuis\": \"2026-10-09T12:00:00Z\"\n}", Encoding.UTF8.GetString(info.Write()));
    }

    [TestMethod]
    public void Json_strings_are_escaped_like_serde_json()
    {
        var node = new JsonObject
        {
            ["texte"] = "« guillemets \" et \\ » é\n\r\t\b\f\u0001\u001f\u007f\u2028",
            ["vide"] = new JsonArray(),
            ["objet"] = new JsonObject(),
            ["liste"] = new JsonArray(1, 2),
        };
        Assert.AreEqual(
            "{\n  \"texte\": \"« guillemets \\\" et \\\\ » é\\n\\r\\t\\b\\f\\u0001\\u001f\u007f\u2028\",\n  \"vide\": [],\n  \"objet\": {},\n  \"liste\": [\n    1,\n    2\n  ]\n}",
            SerdeJson.Pretty(node));
        Assert.AreEqual("{\"a\":[1,true,null,\"x\"],\"b\":{}}", SerdeJson.Compact(JsonNode.Parse("""{"a":[1,true,null,"x"],"b":{}}""")));
        Assert.AreEqual("null", SerdeJson.Pretty(null));
        Assert.AreEqual("1.5e3", SerdeJson.Compact(JsonNode.Parse("1.5e3")), "un nombre lu est réécrit tel quel");
    }

    [TestMethod]
    [DataRow("2026-10-09T12:00:00Z", "2026-10-09T12:00:00Z")]
    [DataRow("2026-10-09T14:00:00+02:00", "2026-10-09T12:00:00Z")]
    [DataRow("2026-10-09T11:30:00-0030", "2026-10-09T12:00:00Z")]
    [DataRow("2026-10-31T14:10:13.9204629Z", "2026-10-31T14:10:13.9204629Z")]
    [DataRow("2026-10-31t14:10:13.5z", "2026-10-31T14:10:13.5Z")]
    [DataRow("2026-10-31 14:10:13,25Z", "2026-10-31T14:10:13.25Z")]
    [DataRow("2026-10-31T14:10:13.000000100Z", "2026-10-31T14:10:13.0000001Z")]
    public void Timestamps_are_read_with_any_offset_and_written_in_utc(string text, string expected) =>
        Assert.AreEqual(expected, InstantText.Format(InstantText.Parse(text, "le")));

    [TestMethod]
    [DataRow("2026-10-09T12:00:00")]
    [DataRow("2026-13-09T12:00:00Z")]
    [DataRow("2026-10-09T12:00:00.1234567890Z")]
    public void Malformed_timestamps_are_refused(string text) =>
        Assert.ThrowsExactly<VaultFormatException>(() => InstantText.Parse(text, "le"));

    private static string[] ReferenceFiles(string folder) =>
        Directory.GetFiles(Path.Combine([TemporaryVault.Reference, .. folder.Split('/')]), "*.json");
}
