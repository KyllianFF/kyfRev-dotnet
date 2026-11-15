using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using KyfRev.Application.Model;
using KyfRev.Modules.Schemas;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Schemas;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Avant le verrouillage du coffre, la vue « Schémas réseau » finit ce qui est en cours dans son îlot : le champ où l'on
/// écrivait s'applique, le schéma s'enregistre, et ce qui n'a pas pu l'être (refusé, modifié ailleurs) garde le coffre ouvert.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class SchemasLeaveTests
{
    private const string Text = "{\"format\": 2, \"titre\": \"Réseau\", \"equipements\": []}";

    private static readonly string[] States = ["enregistre", "aucun", "erreur", "conflit", "modifie"];

    private static readonly FinishReport[] Reports = [new(4, true), new(4, true), new(4, false), new(4, false), new(4, false)];

    [TestMethod]
    public async Task Leaving_waits_for_the_island_and_keeps_the_vault_open_when_it_could_not_save()
    {
        using var vaults = new TestVaults(AppMode.Development);
        using var page = new SchemasPageViewModel(new SchemaUseCases(vaults.App.Host), vaults.Picker, new FakeOpener(), FakeEnvironment.WebData);
        Assert.IsTrue(await page.LeaveAsync(), "sans îlot ouvert, rien à finir");

        var finishing = new TaskCompletionSource<bool>();
        var calls = 0;
        page.FinishIsland = () =>
        {
            calls++;
            return finishing.Task;
        };
        var leaving = page.LeaveAsync();
        Assert.IsFalse(leaving.IsCompleted, "le verrouillage attend que l'îlot ait fini");
        finishing.SetResult(false);
        Assert.IsFalse(await leaving, "ce qui n'a pas pu s'enregistrer garde le coffre ouvert");

        page.FinishIsland = () =>
        {
            calls++;
            return Task.FromResult(true);
        };
        Assert.IsTrue(await page.LeaveAsync());
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task The_bridge_passes_on_what_the_island_has_finished_and_refuses_the_rest()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var bridge = new SchemaBridge(new SchemaUseCases(vaults.App.Host), vaults.Picker, () => null, _ => { });
        var reports = new List<FinishReport>();
        bridge.Finished += (_, report) => reports.Add(report);

        foreach (var state in States)
        {
            Assert.IsTrue(await AnsweredAsync(bridge, $"[4,\"{state}\"]"), state);
        }
        CollectionAssert.AreEqual(Reports, reports);

        // Un numéro qui n'en est pas un, un état inconnu : refusés, et rien n'est tenu pour fini.
        foreach (var args in new[] { "[\"4\",\"enregistre\"]", "[4,\"inconnu\"]", "[4]", "[]" })
        {
            Assert.IsFalse(await AnsweredAsync(bridge, args), args);
        }
        Assert.HasCount(States.Length, reports);
    }

    [EngineTest]
    public void Before_the_lock_the_field_being_written_is_applied_and_the_schema_saved()
    {
        using var scene = new IslandScene();
        Assert.IsTrue(scene.Leave(), "aucun schéma ouvert dans l'éditeur : rien à finir");

        scene.Model.OpenFrom(scene.Id, () => { });
        scene.WaitFor("document.querySelector('dialog[open] input.titre')?.value === 'Réseau'");

        // Le titre s'applique quand on quitte son champ : on y écrit sans le quitter.
        scene.Write("Lycée");
        Assert.IsTrue(scene.Leave());
        StringAssert.Contains(scene.Schemas.Read(scene.Id).Value.Text, "Lycée", StringComparison.Ordinal);

        // Modifié ailleurs pendant qu'on l'écrivait : rien n'écrase l'autre version, et le coffre reste ouvert.
        var disk = scene.Schemas.Read(scene.Id).Value;
        scene.Schemas.Save(scene.Id, disk.Text.Replace("Lycée", "Ailleurs", StringComparison.Ordinal), disk.Fingerprint).Value.ToString();
        scene.Write("Lycée, bâtiment B");
        Assert.IsFalse(scene.Leave());
        StringAssert.Contains(scene.Schemas.Read(scene.Id).Value.Text, "Ailleurs", StringComparison.Ordinal);

        // Quitter la vue garde l'îlot ouvert, et ce qui n'a pas pu s'enregistrer avec lui : le verrouillage le sait encore.
        scene.Page.DataContext = null;
        Assert.IsFalse(scene.Leave(), "l'îlot reste ouvert avec sa vue-modèle");
        scene.Model.Dispose();
        Assert.IsTrue(scene.Leave(), "sans îlot, rien à finir");
    }

    private static async Task<bool> AnsweredAsync(SchemaBridge bridge, string args)
    {
        var answer = JsonNode.Parse((await bridge.ReceiveAsync($"{{\"kind\":\"call\",\"id\":7,\"method\":\"etatSauvegarde\",\"args\":{args}}}"))!)!;
        Assert.AreEqual(7, answer["id"]!.GetValue<int>());
        return answer["ok"]!.GetValue<bool>();
    }

    /// <summary>La vue et son îlot, dans le vrai moteur web, sur un coffre qui a un schéma.</summary>
    private sealed class IslandScene : IDisposable
    {
        private readonly UiThread _ui = new();
        private readonly TestVaults _vaults = new(AppMode.Development);
        private readonly Window _window;

        public IslandScene()
        {
            _vaults.App.Vaults.Create(_vaults.PathOf("Coffre"));
            Schemas = new SchemaUseCases(_vaults.App.Host);
            Id = Schemas.Create(Text).Value.Id;
            var test = new TestShell();
            Model = new SchemasPageViewModel(Schemas, _vaults.Picker, new FakeOpener(), FakeEnvironment.WebData);
            Page = new SchemasPage { DataContext = Model, Width = 1300, Height = 860 };
            _window = new Window { Content = new Canvas { Children = { Page } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
            _window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
            _window.Show();
            TestShell.Flush();
            UiThread.Until(() => Page.Island?.IsReady == true, TimeSpan.FromMinutes(2));
        }

        public SchemaUseCases Schemas { get; }

        public string Id { get; }

        public SchemasPageViewModel Model { get; }

        public SchemasPage Page { get; }

        /// <summary>Ce que la vue a fini avant le verrouillage.</summary>
        public bool Leave()
        {
            var leaving = Model.LeaveAsync();
            UiThread.Until(() => leaving.IsCompleted, TimeSpan.FromSeconds(20));
            return leaving.Result;
        }

        /// <summary>Écrit le titre du schéma, sans quitter son champ.</summary>
        public void Write(string title) =>
            Script($"(() => {{ const champ = document.querySelector('dialog[open] input.titre'); champ.focus(); champ.value = '{title}'; champ.dispatchEvent(new Event('input', {{ bubbles: true }})); return true; }})()");

        public void WaitFor(string condition) => UiThread.Until(() => Script(condition) == "true", TimeSpan.FromSeconds(20));

        public void Dispose()
        {
            _window.Close();
            Model.Dispose();
            _vaults.Dispose();
            _ui.Dispose();
        }

        private string Script(string expression)
        {
            var run = Page.Island!.RunScriptAsync(expression);
            UiThread.Until(() => run.IsCompleted);
            return run.Result;
        }
    }
}
