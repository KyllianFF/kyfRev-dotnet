using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Modules.Schemas;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Schemas;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Schémas réseau » : le pont vers le coffre, puis le vrai îlot dans WebView2.</summary>
[TestClass]
[DoNotParallelize]
public sealed class SchemasPageTests
{
    private const string Text = "{\"format\": 2, \"titre\": \"Réseau\", \"equipements\": [{\"id\": \"e1\"}]}";

    private static readonly byte[] ThreeBytes = [1, 2, 3];

    private static readonly string[] OpenedSheet = ["f_une"];

    [TestMethod]
    public async Task The_bridge_lists_reads_saves_and_copies()
    {
        using var scene = new BridgeScene();

        var created = await scene.CallAsync("creerSchema", Text);
        var id = created["id"]!.GetValue<string>();
        Assert.AreEqual(id, (await scene.CallAsync("lireSchema", id))["id"]!.GetValue<string>());

        var sheet = scene.Vaults.App.Sheets.Create(null, "Une fiche", null).Value;
        var opened = scene.Vaults.App.Sheets.Open(sheet).Value;
        scene.Vaults.App.Editing.Save(sheet, $"```reseau\nschema: {id}\n```\n", opened.Fingerprint, 1).Value.ToString();
        var list = (await scene.CallAsync("schemas")).AsArray();
        var row = list.Single()!;
        Assert.AreEqual("Réseau", row["titre"]!.GetValue<string>());
        Assert.AreEqual(1, row["equipements"]!.GetValue<int>());
        Assert.IsNotNull(row["modifieLe"]);
        Assert.AreEqual("Une fiche", row["fiches"]![0]!["titre"]!.GetValue<string>());
        Assert.AreEqual(sheet, row["fiches"]![0]!["id"]!.GetValue<string>());

        var text = created["texte"]!.GetValue<string>();
        var print = created["empreinte"]!.GetValue<string>();
        var saved = await scene.CallAsync("enregistrerSchema", id, text.Replace("Réseau", "Lycée", StringComparison.Ordinal), print);
        Assert.AreEqual("enregistre", saved["statut"]!.GetValue<string>());

        var conflict = await scene.CallAsync("enregistrerSchema", id, text, print);
        Assert.AreEqual("conflit", conflict["statut"]!.GetValue<string>());
        StringAssert.Contains(conflict["corpsDisque"]!.GetValue<string>(), "Lycée", StringComparison.Ordinal);
        Assert.AreEqual(saved["empreinte"]!.GetValue<string>(), conflict["empreinteDisque"]!.GetValue<string>());

        var copy = await scene.CallAsync("dupliquerSchema", id);
        Assert.AreNotEqual(id, copy["id"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task The_bridge_exports_and_imports_through_the_pickers()
    {
        using var scene = new BridgeScene();
        var outside = scene.Vaults.PathOf("Exports");
        Directory.CreateDirectory(outside);
        scene.Picker.Answers.Enqueue(outside);

        Assert.AreEqual(outside, (await scene.CallAsync("choisirDossier", "Un titre")).GetValue<string>());
        CollectionAssert.Contains(scene.Picker.Titles, "Un titre");

        var exported = (await scene.CallAsync("exporterSchema", outside, "Réseau.png", Convert.ToBase64String(ThreeBytes))).GetValue<string>();
        CollectionAssert.AreEqual(ThreeBytes, await File.ReadAllBytesAsync(exported));

        Assert.IsNull(await scene.RawValueAsync("choisirFichierSchema"));
        var file = Path.Combine(outside, "reseau.json");
        await File.WriteAllTextAsync(file, Text);
        scene.PickedFile = file;
        Assert.AreEqual(file, (await scene.CallAsync("choisirFichierSchema")).GetValue<string>());
        Assert.AreEqual(Text, (await scene.CallAsync("lireImportSchema", file)).GetValue<string>());

        Assert.IsNull(await scene.RawValueAsync("ouvrirFiche", "f_une"));
        CollectionAssert.AreEqual(OpenedSheet, scene.Opened);
    }

    [TestMethod]
    public async Task The_bridge_archives_trashes_and_restores_a_schema()
    {
        using var scene = new BridgeScene();
        var id = (await scene.CallAsync("creerSchema", Text))["id"]!.GetValue<string>();
        Assert.AreEqual("actif", (await scene.CallAsync("schemas")).AsArray().Single()!["etat"]!.GetValue<string>());
        Assert.IsEmpty((await scene.CallAsync("schemasRanges")).AsArray());

        foreach (var place in new[] { "archive", "corbeille" })
        {
            Assert.IsNull(await scene.RawValueAsync("rangerSchema", id, place));
            Assert.IsEmpty((await scene.CallAsync("schemas")).AsArray());
            var aside = (await scene.CallAsync("schemasRanges")).AsArray().Single()!;
            Assert.AreEqual((id, place), (aside["id"]!.GetValue<string>(), aside["etat"]!.GetValue<string>()));
        }

        Assert.IsNull(await scene.RawValueAsync("rangerSchema", id, "actif"));
        Assert.AreEqual(id, (await scene.CallAsync("schemas")).AsArray().Single()!["id"]!.GetValue<string>());

        // Un rangement inconnu ou un schéma absent est refusé, sans rien déplacer.
        foreach (var args in new[] { $"[\"{id}\",\"ailleurs\"]", "[\"s_00000000000000000000000000\",\"corbeille\"]", "[]" })
        {
            var answer = await scene.AnswerAsync($"{{\"kind\":\"call\",\"id\":3,\"method\":\"rangerSchema\",\"args\":{args}}}");
            Assert.IsFalse(answer["ok"]!.GetValue<bool>(), args);
        }
        Assert.HasCount(1, (await scene.CallAsync("schemas")).AsArray());
    }

    [TestMethod]
    public async Task The_bridge_refuses_what_it_does_not_know()
    {
        using var scene = new BridgeScene();

        Assert.IsNull(await scene.Bridge.ReceiveAsync("{ cassé"));
        Assert.IsNull(await scene.Bridge.ReceiveAsync("[1]"));
        Assert.IsNull(await scene.Bridge.ReceiveAsync("{\"kind\":\"ready\"}"));
        Assert.IsNull(await scene.Bridge.ReceiveAsync("{\"kind\":\"call\",\"id\":\"pas un nombre\",\"method\":\"schemas\"}"));

        foreach (var (method, args) in new[] { ("inconnu", "[]"), ("lireSchema", "[]"), ("lireSchema", "[1]"), ("exporterSchema", "[\"d\",\"n.png\",\"pas du base 64 !\"]") })
        {
            var answer = await scene.AnswerAsync($"{{\"kind\":\"call\",\"id\":7,\"method\":\"{method}\",\"args\":{args}}}");
            Assert.AreEqual(7, answer["id"]!.GetValue<int>());
            Assert.IsFalse(answer["ok"]!.GetValue<bool>(), method);
            Assert.IsFalse(string.IsNullOrEmpty(answer["error"]!.GetValue<string>()));
        }
        var unknown = await scene.AnswerAsync("{\"kind\":\"call\",\"id\":8,\"method\":\"lireSchema\",\"args\":[\"s_inconnu\"]}");
        Assert.IsFalse(unknown["ok"]!.GetValue<bool>());
        var noArgs = await scene.AnswerAsync("{\"kind\":\"call\",\"id\":9,\"method\":\"schemas\"}");
        Assert.IsTrue(noArgs["ok"]!.GetValue<bool>());
    }

    [TestMethod]
    public void The_files_of_the_island_are_served_and_nothing_else()
    {
        Assert.IsNotNull(SchemaIsland.PageContent("/index.html"));
        Assert.IsNotNull(SchemaIsland.PageContent("/schemas.js"));
        Assert.IsNotNull(SchemaIsland.PageContent("/schemas.css"));
        Assert.IsNull(SchemaIsland.PageContent("/editor.js"));
        Assert.IsNull(SchemaIsland.PageContent("/absent"));
    }

    [TestMethod]
    public void The_page_says_when_the_engine_could_not_start()
    {
        using var scene = new BridgeScene();
        var page = scene.Page();
        Assert.IsFalse(page.HasProblem);

        page.Problem = SchemaText.EngineFailedOf("absent");

        Assert.IsTrue(page.HasProblem);
        StringAssert.Contains(page.Problem, "absent", StringComparison.Ordinal);
        Assert.AreEqual(FakeEnvironment.WebData, page.WebDataFolder);
        Assert.IsFalse(string.IsNullOrEmpty(SchemaText.ChooseFile));
        Assert.IsFalse(string.IsNullOrEmpty(SchemaText.FileFilter));
        Assert.IsFalse(string.IsNullOrEmpty(SchemaText.IslandName));
    }

    [EngineTest]
    public void The_real_island_lists_creates_and_edits_a_schema()
    {
        using var scene = new IslandScene();
        scene.WaitFor("document.querySelector('.bouton.principal') !== null");
        StringAssert.Contains(scene.Script("document.body.innerText"), "Aucun schéma réseau", StringComparison.Ordinal);

        // Nouveau schéma : un nom, puis l'éditeur, et le fichier est écrit dans le coffre.
        scene.Script("document.querySelector('.bouton.principal').click()");
        scene.WaitFor("document.querySelector('dialog[open] input') !== null");
        scene.Script("const champ = document.querySelector('dialog[open] input'); champ.value = 'Réseau du lycée'; champ.dispatchEvent(new Event('input', { bubbles: true })); document.querySelector('dialog[open] form').requestSubmit()");
        scene.WaitFor("document.querySelector('dialog[open] svg') !== null");
        UiThread.Until(() => scene.Schemas.Schemas().Value.Count == 1);
        Assert.AreEqual("Réseau du lycée", scene.Schemas.Schemas().Value.Single().Title);

        // Les couleurs viennent du thème de l'application.
        Assert.AreEqual("\"#" + scene.Ground + "\"", scene.Script("getComputedStyle(document.documentElement).getPropertyValue('--fond').trim()"));
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Pas de défilement horizontal.");
    }

    [EngineTest]
    public void The_island_shows_the_schemas_of_the_vault_and_follows_the_theme()
    {
        using var scene = new IslandScene(Text);
        scene.WaitFor("document.body.innerText.includes('Réseau')");

        var light = scene.Script("getComputedStyle(document.documentElement).getPropertyValue('--fond')");
        scene.Test.Design.Theme = ThemeKind.Dark;
        UiThread.Until(() => scene.Script("getComputedStyle(document.documentElement).getPropertyValue('--fond')") != light);
        Assert.AreEqual(scene.Background, scene.Page.Island!.Ground, "Le fond du moteur suit le thème.");
    }

    [EngineTest]
    public void The_engine_takes_the_background_of_the_theme_before_its_first_page()
    {
        using var scene = new IslandScene(waitsForTheIsland: false, theme: ThemeKind.Dark);
        UiThread.Until(() => scene.Page.Island is { IsLoaded: true }, TimeSpan.FromMinutes(1));

        Assert.AreEqual(scene.Background, scene.Page.Island!.Ground, "Pas d'éclair blanc pendant que la page se charge.");
        UiThread.Until(() => scene.Page.Island.IsReady, TimeSpan.FromMinutes(2));
    }

    [EngineTest]
    public void A_panel_of_the_shell_covers_the_island_with_its_image()
    {
        using var scene = new IslandScene();
        var island = scene.Page.Island!;

        var covered = island.SetCoveredAsync(true);
        UiThread.Until(() => covered.IsCompleted);
        Assert.IsTrue(island.IsCovered);

        var again = island.SetCoveredAsync(true);
        Assert.IsTrue(again.IsCompleted, "Déjà recouvert.");

        var shown = island.SetCoveredAsync(false);
        UiThread.Until(() => shown.IsCompleted);
        Assert.IsFalse(island.IsCovered);
        island.SetZoom(1.25);

        scene.Page.DataContext = null;
        Assert.IsNull(scene.Page.Island, "Sans vue, plus d'îlot.");
    }

    /// <summary>Le pont seul, sur un vrai coffre.</summary>
    private sealed class BridgeScene : IDisposable
    {
        public BridgeScene()
        {
            Vaults = new TestVaults(AppMode.Development);
            Vaults.App.Vaults.Create(Vaults.PathOf("Coffre"));
            Bridge = new SchemaBridge(new SchemaUseCases(Vaults.App.Host), Picker, () => PickedFile, Opened.Add);
        }

        public TestVaults Vaults { get; }

        public FakePicker Picker => Vaults.Picker;

        public SchemaBridge Bridge { get; }

        public string? PickedFile { get; set; }

        public List<string> Opened { get; } = [];

        public SchemasPageViewModel Page() => new(new SchemaUseCases(Vaults.App.Host), Picker, new FakeOpener(), FakeEnvironment.WebData);

        public async Task<JsonObject> AnswerAsync(string message) => (JsonNode.Parse((await Bridge.ReceiveAsync(message))!) as JsonObject)!;

        public async Task<JsonNode> CallAsync(string method, params string[] args) => (await RawValueAsync(method, args))!;

        public async Task<JsonNode?> RawValueAsync(string method, params string[] args)
        {
            var message = JsonSerializer.Serialize(new { kind = "call", id = 1, method, args });
            var answer = await AnswerAsync(message);
            Assert.IsTrue(answer["ok"]!.GetValue<bool>(), answer["error"]?.GetValue<string>());
            return answer["value"];
        }

        public void Dispose() => Vaults.Dispose();
    }

    /// <summary>La vue et son îlot, dans le vrai moteur web.</summary>
    private sealed class IslandScene : IDisposable
    {
        private readonly UiThread _ui = new();
        private readonly TestVaults _vaults = new(AppMode.Development);
        private readonly Window _window;

        public IslandScene(string? schema = null, bool waitsForTheIsland = true, ThemeKind theme = ThemeKind.Light)
        {
            _vaults.App.Vaults.Create(_vaults.PathOf("Coffre"));
            Schemas = new SchemaUseCases(_vaults.App.Host);
            if (schema is not null)
            {
                Schemas.Create(schema).Value.ToString();
            }
            Test = new TestShell();
            Test.Design.Theme = theme;
            Page = new SchemasPage { DataContext = new SchemasPageViewModel(Schemas, _vaults.Picker, new FakeOpener(), FakeEnvironment.WebData), Width = 1300, Height = 860 };
            _window = new Window { Content = new Canvas { Children = { Page } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
            _window.Resources.MergedDictionaries.Add(Test.Design.Dictionary);
            _window.Show();
            TestShell.Flush();
            if (waitsForTheIsland)
            {
                UiThread.Until(() => Page.Island?.IsReady == true, TimeSpan.FromMinutes(2));
            }
        }

        public TestShell Test { get; }

        /// <summary>Le fond du thème, tel que le moteur le reçoit.</summary>
        public System.Drawing.Color Background =>
            Test.Design.Dictionary["Kyf.Brush.Background"] is System.Windows.Media.SolidColorBrush { Color: var color }
                ? System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B)
                : System.Drawing.Color.Empty;

        public SchemasPage Page { get; }

        public SchemaUseCases Schemas { get; }

        public string Ground
        {
            get
            {
                var color = ((System.Windows.Media.SolidColorBrush)Test.Design.Dictionary["Kyf.Brush.Background"]).Color;
                return FormattableString.Invariant($"{color.R:X2}{color.G:X2}{color.B:X2}");
            }
        }

        public string Script(string expression)
        {
            var run = Page.Island!.RunScriptAsync(expression);
            UiThread.Until(() => run.IsCompleted);
            return run.Result;
        }

        public void WaitFor(string condition) => UiThread.Until(() => Script(condition) == "true", TimeSpan.FromSeconds(20));

        public void Dispose()
        {
            _window.Close();
            _vaults.Dispose();
            _ui.Dispose();
        }
    }
}
