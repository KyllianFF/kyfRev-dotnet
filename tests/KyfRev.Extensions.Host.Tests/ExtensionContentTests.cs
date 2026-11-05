using System.Text.Json;
using KyfRev.Application.Model;
using KyfRev.Application.Tests.Support;

namespace KyfRev.Extensions.Host.Tests;

/// <summary>La liste blanche : ce qu'une extension affiche, tel que le moteur web le lit, et ce qui en reste.</summary>
[TestClass]
public sealed class ExtensionContentTests
{
    [TestMethod]
    public void What_is_not_a_tree_is_refused()
    {
        using var document = JsonDocument.Parse("{}");

        Assert.AreEqual("rendu illisible", ExtensionContent.Read(document.RootElement).Error);
    }

    [TestMethod]
    public void Text_keeps_its_words_and_collapses_its_spaces()
    {
        var content = Read("""[{"texte":"  un\n   deux\t"},{"balise":"p","enfants":[{"texte":"trois"}]},1]""");

        Assert.AreEqual(" un deux trois", content.Text);
        Assert.AreEqual(ExtensionNode.TextTag, content.Nodes[0].Tag);
        Assert.AreEqual("p", content.Nodes[1].Tag);
    }

    [TestMethod]
    public void Preformatted_text_keeps_its_spaces()
    {
        var content = Read("""[{"balise":"pre","enfants":[{"texte":"a\n  b"},{"balise":"code","enfants":[{"texte":"  c  "}]}]}]""");

        Assert.AreEqual("a\n  b  c  ", content.Text);
    }

    [TestMethod]
    [DataRow("h1", "h1")]
    [DataRow("h6", "h6")]
    [DataRow("section", "div")]
    [DataRow("blockquote", "div")]
    [DataRow("tfoot", "tbody")]
    [DataRow("a", "span")]
    [DataRow("abbr", "span")]
    [DataRow("label", "label")]
    [DataRow("form", "form")]
    [DataRow("fieldset", "div")]
    [DataRow("legend", "p")]
    [DataRow("progress", "progress")]
    [DataRow("meter", "progress")]
    [DataRow("b", "strong")]
    [DataRow("i", "em")]
    [DataRow("kbd", "code")]
    [DataRow("time", "time")]
    [DataRow("details", "details")]
    [DataRow("select", "select")]
    public void A_known_tag_becomes_what_the_list_says(string tag, string kept)
    {
        var content = Read($$"""[{"balise":"{{tag}}","enfants":[{"texte":"x"}]}]""");

        Assert.AreEqual(kept, content.Nodes.Single().Tag);
        Assert.AreEqual("x", content.Text);
    }

    [TestMethod]
    [DataRow("script")]
    [DataRow("style")]
    [DataRow("iframe")]
    [DataRow("object")]
    [DataRow("embed")]
    [DataRow("link")]
    [DataRow("meta")]
    [DataRow("base")]
    [DataRow("template")]
    [DataRow("noscript")]
    [DataRow("svg")]
    [DataRow("math")]
    [DataRow("audio")]
    [DataRow("video")]
    [DataRow("canvas")]
    [DataRow("frameset")]
    public void A_dangerous_tag_goes_with_all_it_holds(string tag)
    {
        var content = Read($$"""[{"balise":"{{tag}}","enfants":[{"texte":"secret"},{"balise":"p","enfants":[{"texte":"secret"}]}]}]""");

        Assert.IsEmpty(content.Nodes);
    }

    [TestMethod]
    [DataRow("font")]
    [DataRow("center")]
    [DataRow("mon-element")]
    public void An_unknown_tag_goes_and_what_it_holds_stays(string tag)
    {
        var content = Read($$"""[{"balise":"{{tag}}","enfants":[{"balise":"p","enfants":[{"texte":"reste"}]}]}]""");

        Assert.AreEqual("p", content.Nodes.Single().Tag);
        Assert.AreEqual("reste", content.Text);
    }

    [TestMethod]
    public void An_image_leaves_only_its_text()
    {
        Assert.AreEqual("une carte", Read("""[{"balise":"img","alt":"une carte","attributs":{}}]""").Text);
        Assert.IsEmpty(Read("""[{"balise":"img","alt":null}]""").Nodes);
        Assert.IsEmpty(Read("""[{"balise":"img","alt":""}]""").Nodes);
    }

    [TestMethod]
    public void Nothing_but_the_known_classes_and_attributes_stays()
    {
        var node = Read("""
            [{"balise":"button","classes":["bouton","principal","faux-dialogue","bouton",3],
              "donnees":{"action":"suivante","valeur":"12","fooBar":"x","nombre":3},
              "attributs":{"type":"button","disabled":"","aria-label":"Suivante","style":"position:fixed","onclick":"x()","href":"https://fuite.invalid","src":"x","nombre":1},
              "enfants":[{"texte":"Suite"}]}]
            """).Nodes.Single();

        Assert.AreEqual("bouton principal", string.Join(' ', node.Classes));
        Assert.IsTrue(node.Has("principal"));
        Assert.IsFalse(node.Has("faux-dialogue"));
        Assert.AreEqual("suivante", node.Action);
        Assert.AreEqual("12", node.Data["valeur"]);
        Assert.AreEqual("x", node.Data["fooBar"]);
        Assert.IsFalse(node.Data.ContainsKey("nombre"));
        Assert.AreEqual("Suivante", node.Attribute("aria-label"));
        Assert.AreEqual(string.Empty, node.Attribute("disabled"));
        Assert.AreEqual("button", node.Attribute("type"));
        foreach (var dropped in new[] { "style", "onclick", "href", "src", "nombre" })
        {
            Assert.IsNull(node.Attribute(dropped), dropped);
        }
    }

    [TestMethod]
    public void Hidden_elements_are_not_shown()
    {
        Assert.IsEmpty(Read("""[{"balise":"p","attributs":{"hidden":""},"enfants":[{"texte":"caché"}]}]""").Nodes);
    }

    [TestMethod]
    [DataRow("password", false)]
    [DataRow("file", false)]
    [DataRow("hidden", false)]
    [DataRow("submit", false)]
    [DataRow("image", false)]
    [DataRow("text", true)]
    [DataRow("SEARCH", true)]
    [DataRow("number", true)]
    [DataRow("checkbox", true)]
    [DataRow("radio", true)]
    public void Only_harmless_fields_stay(string type, bool stays)
    {
        var content = Read($$$"""[{"balise":"input","attributs":{"type":"{{{type}}}"}}]""");

        Assert.AreEqual(stays, !content.Nodes.IsEmpty, type);
    }

    [TestMethod]
    public void A_field_without_type_is_a_text_field()
    {
        Assert.AreEqual("input", Read("""[{"balise":"input"}]""").Nodes.Single().Tag);
    }

    [TestMethod]
    public void Too_much_data_is_left_out()
    {
        var entries = string.Join(',', Enumerable.Range(0, 40).Select(i => $"\"c{i}\":\"v\""));
        var long_ = new string('x', 4097);
        var node = Read($$$"""[{"balise":"button","donnees":{"long":"{{{long_}}}",{{{entries}}}}}]""").Nodes.Single();

        Assert.HasCount(32, node.Data);
        Assert.IsFalse(node.Data.ContainsKey("long"));
    }

    [TestMethod]
    public void Too_deep_is_refused()
    {
        var json = string.Concat(Enumerable.Repeat("""[{"balise":"div","enfants":""", 70)) + "[]" + string.Concat(Enumerable.Repeat("}]", 70));
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 512 });

        Assert.AreEqual("rendu trop profond", ExtensionContent.Read(document.RootElement).Error);
    }

    [TestMethod]
    public void A_broken_element_is_read_as_empty()
    {
        var node = Read("""[{"balise":"p","classes":"x","donnees":1,"attributs":[],"enfants":"y"}]""").Nodes.Single();

        Assert.IsTrue(node.Classes.IsEmpty);
        Assert.IsTrue(node.Data.IsEmpty);
        Assert.IsTrue(node.Attributes.IsEmpty);
        Assert.IsTrue(node.Children.IsEmpty);
        Assert.IsEmpty(Read("""[{"sansBalise":1}]""").Nodes);
    }

    [TestMethod]
    public void All_the_elements_can_be_walked()
    {
        var content = Read("""[{"balise":"ul","enfants":[{"balise":"li","enfants":[{"texte":"a"}]},{"balise":"li"}]},{"balise":"hr"}]""");

        Assert.AreEqual("ul,li,#texte,li,hr", string.Join(',', content.Descendants().Select(node => node.Tag)));
        Assert.IsEmpty(ExtensionContent.Empty.Nodes);
    }

    [TestMethod]
    public void A_form_keeps_the_names_of_its_fields_and_a_progress_its_maximum()
    {
        var content = Read("""
            [{"balise":"form","donnees":{"action":"creer"},"enfants":[
              {"balise":"input","attributs":{"name":"nom","required":"","onfocus":"x()"}},
              {"balise":"progress","attributs":{"value":"3","max":"12","style":"width:9999px"}}]}]
            """);

        var form = content.Nodes.Single();
        Assert.AreEqual("creer", form.Action);
        Assert.AreEqual("nom", form.Children[0].Attribute("name"));
        Assert.AreEqual(string.Empty, form.Children[0].Attribute("required"));
        Assert.IsNull(form.Children[0].Attribute("onfocus"));
        Assert.AreEqual("12", form.Children[1].Attribute("max"));
        Assert.IsNull(form.Children[1].Attribute("style"));
    }

    private static ExtensionContent Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var content = ExtensionContent.Read(document.RootElement);
        Assert.IsTrue(content.Succeeded, content.Error);
        return content.Value;
    }
}

/// <summary>Le coffre tel que les extensions le voient, sur un vrai coffre.</summary>
[TestClass]
public sealed class VaultExtensionDataTests
{
    [TestMethod]
    public async Task An_extension_sees_the_sheets_that_exist_and_their_text()
    {
        using var test = new DataTest();
        var sheet = test.App.App.Sheets.Create("Biologie", "Mitose", null).Value;
        var opened = test.App.App.Sheets.Open(sheet).Value;
        Assert.IsTrue(test.App.App.Editing.Save(sheet, "Le texte de la fiche.", opened.Fingerprint, 1).Succeeded);
        var archived = test.App.App.Sheets.Create("Biologie", "Méiose", null).Value;
        Assert.IsTrue(test.App.App.Reviews.Archive(archived).Succeeded);
        var gone = test.App.App.Sheets.Create("Biologie", "Disparue", null).Value;
        File.Delete(Path.Combine(test.Vault, test.App.App.Sheets.Open(gone).Value.Path));
        test.Reopen();

        var list = await test.Data.SheetsAsync();

        Assert.IsTrue(list.Succeeded, list.Error);
        Assert.IsFalse(list.Value.Any(item => item.Id == gone), "Une fiche sans fichier ne se lit pas.");
        Assert.IsFalse(list.Value.Single(item => item.Id == sheet).IsArchived);
        Assert.IsTrue(list.Value.Single(item => item.Id == archived).IsArchived);
        Assert.AreEqual("Biologie", list.Value.Single(item => item.Id == sheet).Folder);

        var text = await test.Data.SheetAsync(sheet);
        Assert.AreEqual("Mitose", text.Value.Title);
        Assert.AreEqual("Le texte de la fiche.", text.Value.Text);
        Assert.IsFalse((await test.Data.SheetAsync("f_absente")).Succeeded);
    }

    [TestMethod]
    public async Task What_an_extension_keeps_is_in_the_vault_as_the_old_version_wrote_it()
    {
        using var test = new DataTest();
        Assert.IsNull((await test.Data.StoredAsync("quiz")).Value);

        Assert.IsTrue((await test.Data.StoreAsync("quiz", """{"sues":3}""")).Succeeded);

        Assert.AreEqual("""{"sues":3}""", (await test.Data.StoredAsync("quiz")).Value);
        Assert.AreEqual("""{"sues":3}""", await File.ReadAllTextAsync(Path.Combine(test.Vault, ".suivi", "extensions", "quiz.json")));
        Assert.IsFalse((await test.Data.StoreAsync("quiz", "pas du json")).Succeeded);
        Assert.IsFalse((await test.Data.StoreAsync("../dehors", "{}")).Succeeded);
    }

    [TestMethod]
    public async Task Without_a_vault_it_is_said()
    {
        using var test = new DataTest(open: false);

        Assert.IsFalse((await test.Data.SheetsAsync()).Succeeded);
        Assert.IsFalse((await test.Data.StoredAsync("quiz")).Succeeded);
        Assert.IsFalse((await test.Data.CreateSheetAsync("quiz", new ExtensionNewSheet("Titre", null, null, []))).Succeeded);
        Assert.IsFalse((await test.Data.SetTagsAsync("quiz", "f_1", [])).Succeeded);
        Assert.IsFalse((await test.Data.WriteSheetAsync("quiz", "f_1", "texte")).Succeeded);
        Assert.IsFalse((await test.Data.JournalAsync("quiz")).Succeeded);
    }

    [TestMethod]
    public async Task An_extension_creates_a_sheet_with_its_text_and_tags_and_it_is_noted()
    {
        using var test = new DataTest();

        var created = await test.Data.CreateSheetAsync("quiz", new ExtensionNewSheet("Le quiz", "Philosophie", "Une question ?", ["quiz", "#philo"]));

        Assert.IsTrue(created.Succeeded, created.Error);
        var sheet = test.App.App.Sheets.Open(created.Value).Value;
        Assert.AreEqual("Le quiz", sheet.Title);
        Assert.AreEqual("Philosophie", sheet.Folder);
        Assert.AreEqual("Une question ?", sheet.Body);
        Assert.AreEqual("quiz philo", string.Join(' ', sheet.Tags));
        var noted = (await test.Data.JournalAsync("quiz")).Value.Single();
        Assert.AreEqual(new ExtensionJournalEntry(test.App.Time.GetUtcNow(), ExtensionJournalEntry.Creation, created.Value, "Le quiz", "Philosophie"), noted);
        Assert.IsTrue(File.Exists(Path.Combine(test.Vault, ".suivi", "extensions", "journaux", "quiz.jsonl")));

        var empty = await test.Data.CreateSheetAsync("quiz", new ExtensionNewSheet("Vide", null, null, []));
        Assert.AreEqual(string.Empty, test.App.App.Sheets.Open(empty.Value).Value.Body.Trim());
        Assert.IsNull((await test.Data.JournalAsync("quiz")).Value[0].Detail);
        Assert.IsFalse((await test.Data.CreateSheetAsync("quiz", new ExtensionNewSheet("Dehors", "../dehors", null, []))).Succeeded);
        Assert.HasCount(2, (await test.Data.JournalAsync("quiz")).Value);
    }

    [TestMethod]
    public async Task Each_writing_keeps_the_text_it_replaces_in_the_versions_and_is_noted()
    {
        using var test = new DataTest();
        var id = test.App.App.Sheets.Create("Biologie", "Mitose", null).Value;
        var opened = test.App.App.Sheets.Open(id).Value;
        Assert.IsTrue(test.App.App.Editing.Save(id, "Premier texte.", opened.Fingerprint, 1).Succeeded);
        var editorFingerprint = test.App.App.Sheets.Open(id).Value.Fingerprint;
        var before = test.App.App.Editing.Versions(id).Value.Count;

        Assert.IsTrue((await test.Data.WriteSheetAsync("correcteur", id, "Deuxième texte.")).Succeeded);
        test.App.Time.Advance(TimeSpan.FromMinutes(1));
        Assert.IsTrue((await test.Data.WriteSheetAsync("correcteur", id, "Troisième texte.")).Succeeded);

        Assert.AreEqual("Troisième texte.", test.App.App.Sheets.Open(id).Value.Body);
        var versions = test.App.App.Editing.Versions(id).Value;
        Assert.HasCount(before + 2, versions);
        var kept = versions.Select(version => test.App.App.Editing.ReadVersion(id, version.Name).Value).ToList();
        Assert.IsTrue(kept.Any(text => text.EndsWith("Premier texte.", StringComparison.Ordinal)), "Le texte d'avant la première écriture est gardé.");
        Assert.IsTrue(kept.Any(text => text.EndsWith("Deuxième texte.", StringComparison.Ordinal)), "Celui d'avant la seconde aussi.");
        var journal = (await test.Data.JournalAsync("correcteur")).Value;
        Assert.AreEqual("15 → 16 caractères", journal[0].Detail);
        Assert.AreEqual("14 → 15 caractères", journal[1].Detail);
        Assert.IsTrue(journal.All(entry => entry.Action == ExtensionJournalEntry.Writing && entry.Title == "Mitose" && entry.Sheet == id));

        // L'éditeur qui tenait l'ancien texte ne l'écrase pas : il voit le conflit.
        Assert.IsInstanceOfType<SaveConflict>(test.App.App.Editing.Save(id, "Texte de l'éditeur.", editorFingerprint, 7).Value);
        Assert.IsFalse((await test.Data.WriteSheetAsync("correcteur", "f_absente", "x")).Succeeded);
        Assert.HasCount(2, (await test.Data.JournalAsync("correcteur")).Value);
    }

    [TestMethod]
    public async Task Tags_set_by_an_extension_are_noted()
    {
        using var test = new DataTest();
        var id = test.App.App.Sheets.Create(null, "Mitose", null).Value;

        var tagged = await test.Data.SetTagsAsync("quiz", id, ["bio", "#cours", "bio"]);

        Assert.AreEqual("bio cours", string.Join(' ', tagged.Value));
        Assert.AreEqual("bio, cours", (await test.Data.JournalAsync("quiz")).Value.Single().Detail);
        Assert.IsFalse((await test.Data.SetTagsAsync("quiz", "f_absente", ["a"])).Succeeded);
    }

    [TestMethod]
    public async Task A_writing_that_cannot_be_noted_says_so_and_a_damaged_line_hides_no_other()
    {
        using var test = new DataTest();
        var id = test.App.App.Sheets.Create(null, "Mitose", null).Value;
        Directory.CreateDirectory(Path.Combine(test.Vault, ".suivi", "extensions", "journaux", "quiz.jsonl"));

        var tagged = await test.Data.SetTagsAsync("quiz", id, ["bio"]);

        StringAssert.Contains(tagged.Error, "pas noté au journal", StringComparison.Ordinal);
        Directory.Delete(Path.Combine(test.Vault, ".suivi", "extensions", "journaux", "quiz.jsonl"));
        await File.WriteAllTextAsync(
            Path.Combine(test.Vault, ".suivi", "extensions", "journaux", "quiz.jsonl"),
            "{ cassé\n{\"quand\":\"2026-11-05T18:00:00+00:00\",\"action\":\"ecriture\",\"fiche\":\"f_1\",\"titre\":\"Mitose\",\"detail\":null}\n{\"autre\":1}\n");
        var journal = (await test.Data.JournalAsync("quiz")).Value;
        Assert.AreEqual("Mitose", journal.Single(entry => entry.Action == ExtensionJournalEntry.Writing).Title);
        Assert.AreEqual("{\"autre\":1} | { cassé", string.Join(" | ", journal.Where(entry => entry.Action == ExtensionJournalEntry.Unreadable).Select(entry => entry.Detail)));
        Assert.HasCount(ExtensionJournalEntry.UnreadableShown, ExtensionJournalEntry.UnreadableLine(new string('x', 200)).Detail!);
    }

    private sealed class DataTest : IDisposable
    {
        private readonly TestFolder _folder = new();

        public DataTest(bool open = true)
        {
            if (open)
            {
                Assert.IsNull(App.App.Vaults.Create(_folder.Vault).OpenError);
            }
            Data = Of(App);
        }

        /// <summary>Rouvre le coffre : c'est à l'ouverture qu'il voit les fichiers qui manquent.</summary>
        public void Reopen()
        {
            App.Dispose();
            App = new TestApplication(FakeTime.AtLocal(2026, 11, 5, 18, 0));
            Assert.IsNull(App.App.Vaults.Open(_folder.Vault).OpenError);
            Data = Of(App);
        }

        private static VaultExtensionData Of(TestApplication app) =>
            new(app.App.Sheets, app.App.Editing, new ExtensionDataUseCases(app.App.Host), app.Time);

        public TestApplication App { get; private set; } = new(FakeTime.AtLocal(2026, 11, 5, 18, 0));

        public string Vault => _folder.Vault;

        public VaultExtensionData Data { get; private set; }

        public void Dispose()
        {
            App.Dispose();
            _folder.Dispose();
        }
    }
}
