using KyfRev.Application.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Quotes;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>Limites et défenses des annotations, réglages, citations, schémas et modèles, hors scénarios.</summary>
[TestClass]
public sealed class VaultComplementsTests
{
    private static readonly Moment Now = new(new DateOnly(2026, 9, 1), new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void Annotation_lengths_are_limited()
    {
        using var folder = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        var sheet = vault.CreateSheet(null, "Fiche", "Texte.\n", Now);
        var anchor = new StoredAnchor("Texte", "", "", 0, 5, [], null, "sha256:x", UnknownProperties.None);
        var tooLong = new string('a', VaultSession.MaxPassageChars + 1);

        Assert.Contains("commentaire trop long", Refused(() => vault.CreateAnnotation(sheet, anchor, tooLong, Now)));
        Assert.Contains("passage trop long", Refused(() => vault.CreateAnnotation(sheet, anchor with { Exact = tooLong }, null, Now)));
        Assert.Contains("terme trop long", Refused(() => vault.CreateDefinition(sheet, anchor, new StoredDefinition(new string('t', VaultSession.MaxTermChars + 1), "x"), Now)));
        Assert.Contains("explication trop longue", Refused(() => vault.CreateDefinition(sheet, anchor, new StoredDefinition("t", tooLong), Now)));
        Assert.IsEmpty(vault.Annotations(sheet));
    }

    [TestMethod]
    public void An_annotations_file_named_after_another_sheet_is_unreadable()
    {
        using var folder = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        var sheet = vault.CreateSheet(null, "Fiche", "Texte.\n", Now);
        var other = vault.CreateSheet(null, "Autre", "Texte.\n", Now);
        vault.CreateAnnotation(sheet, new StoredAnchor("Texte", "", "", 0, 5, [], null, "sha256:x", UnknownProperties.None), null, Now);
        File.Move(folder.PathOf($".suivi/annotations/{sheet}.json"), folder.PathOf($".suivi/annotations/{other}.json"));

        vault.Analyze(Now);

        Assert.AreEqual($".suivi/annotations/{other}.json", vault.Health.Unreadable.Single().Path);
        Assert.AreEqual(VaultErrorKind.UnreadableAnnotations, Assert.ThrowsExactly<VaultException>(() =>
            vault.CreateAnnotation(other, new StoredAnchor("Texte", "", "", 0, 5, [], null, "sha256:x", UnknownProperties.None), null, Now)).Kind);
    }

    [TestMethod]
    public void Extension_data_must_be_small_valid_json()
    {
        using var folder = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        Assert.Contains("illisibles", Refused(() => vault.SaveExtensionData("quiz", "{ pas du JSON")));
        Assert.Contains("plus de 1 Mo", Refused(() => vault.SaveExtensionData("quiz", $"\"{new string('x', VaultSession.MaxExtensionDataBytes)}\"")));
        Assert.IsNull(vault.ExtensionData("quiz"));

        folder.Write(".suivi/extensions/latin.json", "x");
        File.WriteAllBytes(folder.PathOf(".suivi/extensions/latin.json"), [0x22, 0xE9, 0x22]);
        Assert.AreEqual(VaultErrorKind.Io, Assert.ThrowsExactly<VaultException>(() => vault.ExtensionData("latin")).Kind);
    }

    [TestMethod]
    public void A_damaged_draw_journal_stops_the_daily_draw()
    {
        using var folder = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        folder.Write(".suivi/rappels.json", "{ cassé");
        Assert.AreEqual(VaultErrorKind.UnreadableTracking, Assert.ThrowsExactly<VaultException>(() => vault.PrepareDay(Now)).Kind);
    }

    [TestMethod]
    public void Schemas_are_checked_before_being_written()
    {
        using var folder = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        Assert.Contains("plus de 8 Mo", Refused(() => vault.CreateSchema($"{{\"format\": 2, \"x\": \"{new string('x', VaultSession.MaxSchemaBytes)}\"}}")));
        Assert.Contains("JSON illisible", Refused(() => vault.CreateSchema("{ cassé")));

        var schema = vault.CreateSchema("{\"format\": 1}");
        var copy = vault.DuplicateSchema(schema.Id);
        Assert.Contains("\"titre\": \"Schéma réseau (copie)\"", copy.Text);

        File.WriteAllBytes(folder.PathOf($"schemas/{schema.Id}.json"), [0x7B, 0xE9, 0x7D]);
        Assert.Contains("non UTF-8", Refused(() => vault.ReadSchema(schema.Id)));
    }

    [TestMethod]
    public void Quote_fields_are_limited()
    {
        using var folder = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        var quote = new QuoteContent("Texte.", null, null, QuoteSource.None, null, []);
        Assert.Contains("texte trop long", Refused(() => vault.AddQuote(quote with { Text = new string('a', QuoteFile.MaxTextChars + 1) }, Now)));
        Assert.Contains("2 000 caractères", Refused(() => vault.AddQuote(quote with { Source = QuoteSource.None with { Address = new string('u', QuoteFile.MaxFieldChars + 1) } }, Now)));
        Assert.IsEmpty(vault.Quotes().Quotes);
    }

    [TestMethod]
    [DataRow("date: true", "un booléen n'est ni un texte ni un nombre")]
    [DataRow("source: Pensées", "la source doit être une suite de champs")]
    [DataRow("etiquettes: philosophie", "les étiquettes doivent être une liste")]
    [DataRow("auteur: X\nauteur: Y", "champ en double")]
    [DataRow("auteur: [cassé", "YAML invalide")]
    public void Unusual_quote_headers_are_reported_unreadable(string header, string reason)
    {
        using var folder = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        folder.Write("citations/Étrange.md", $"---\n{header}\n---\nTexte.\n");
        File.WriteAllBytes(folder.PathOf("citations/Latin.md"), [0x65, 0xE9, 0x0A]);

        var list = vault.Quotes();

        Assert.IsEmpty(list.Quotes, reason);
        Assert.AreEqual("citations/Latin.md|citations/Étrange.md", string.Join('|', list.Unreadable.Select(u => u.Path).Order(StringComparer.Ordinal)));
        Assert.AreEqual("encodage non UTF-8", list.Unreadable.Single(u => u.Path == "citations/Latin.md").Message);
    }

    [TestMethod]
    public void A_quote_whose_header_cannot_be_read_gets_a_new_identifier_when_modified()
    {
        using var folder = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        folder.Write("citations/Cassée.md", "---\nauteur: [cassé\n---\nTexte.\n");

        var quote = vault.ModifyQuote("citations/Cassée.md", new QuoteContent("Réparée.", null, null, QuoteSource.None, null, []), Now);

        Assert.StartsWith("c_", quote.Id!);
        Assert.AreEqual(Now.Day, quote.AddedOn);
        File.WriteAllBytes(folder.PathOf("citations/Cassée.md"), [0x65, 0xE9, 0x0A]);
        Assert.AreEqual(VaultErrorKind.Io, Assert.ThrowsExactly<VaultException>(() =>
            vault.ModifyQuote("citations/Cassée.md", new QuoteContent("x", null, null, QuoteSource.None, null, []), Now)).Kind);
    }

    [TestMethod]
    [DataRow("vers 1650", 1650)]
    [DataRow("XVIIe siècle", 1650)]
    [DataRow("Ier siècle", 50)]
    [DataRow("IVe siècle avant J.-C.", -350)]
    [DataRow("12/05/68", 12)]
    [DataRow("30 juillet 1903", 1903)]
    public void The_year_of_a_free_date_is_found(string date, int year) => Assert.AreEqual(year, QuoteDates.YearOf(date));

    [TestMethod]
    [DataRow("sans date")]
    [DataRow("")]
    public void A_date_without_year_has_none(string date) => Assert.IsNull(QuoteDates.YearOf(date));

    [TestMethod]
    [DataRow(0.0, "0.0")]
    [DataRow(-0.0, "-0.0")]
    [DataRow(100.0, "100.0")]
    [DataRow(1e15, "1000000000000000.0")]
    [DataRow(1e16, "1e16")]
    [DataRow(0.1, "0.1")]
    [DataRow(1e-5, "0.00001")]
    [DataRow(1e-6, "1e-6")]
    [DataRow(1.5e-7, "1.5e-7")]
    [DataRow(-2.5, "-2.5")]
    [DataRow(1.2345678901234568e17, "1.2345678901234568e17")]
    public void Real_numbers_are_written_like_serde_json(double value, string expected) => Assert.AreEqual(expected, SerdeJson.Float(value));

    [TestMethod]
    public void Template_descriptions_fall_back_on_titles()
    {
        using var folder = TemporaryVault.Empty();
        using var vault = VaultSession.Initialize(folder.Root, Now);
        folder.Write("modeles/Vide.md", "<!-- -->\n## Titre\n");
        folder.Write("modeles/Ouvert.md", "<!-->\n## Autre\n");

        var templates = vault.Templates();

        Assert.AreEqual("Autre.", templates.Single(t => t.Name == "Ouvert").Description);
        Assert.AreEqual("Titre.", templates.Single(t => t.Name == "Vide").Description);
        Assert.AreEqual(string.Empty, vault.TemplateBody("Inconnu", "x", "", Now.Day));
    }

    [TestMethod]
    [DataRow("ΟΔΟΣ", "οδος")]
    [DataRow("Σ", "σ")]
    [DataRow("ΣΑ", "σα")]
    [DataRow("ΑΣ.", "ας.")]
    [DataRow("İ", "i̇")]
    public void Lowercase_follows_rust_and_its_final_sigma(string text, string expected) =>
        Assert.AreEqual(expected, RustText.ToLowercase(text));

    private static string Refused(Action action) => Assert.ThrowsExactly<VaultException>(action).Message;
}
