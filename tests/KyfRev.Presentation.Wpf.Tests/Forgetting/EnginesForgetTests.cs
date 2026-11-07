using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using KyfRev.Application;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Extensions;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Les moteurs web quand le coffre se verrouille : celui des schémas oublie ses dessins, dont les clés sont le texte des
/// fiches, et ferme sa page cachée ; celui des extensions oublie leurs erreurs, qui peuvent citer ce qu'elles avaient lu.
/// L'un et l'autre restent utilisables, pour le coffre suivant.
/// </summary>
[TestClass]
public sealed class EnginesForgetTests
{
    private const string Schema = "flowchart LR\n  mitose --> cytocinese";

    /// <summary>Le tout premier démarrage du moteur web sur une machine neuve est long.</summary>
    private static readonly TimeSpan Long = TimeSpan.FromMinutes(2);

    [EngineTest]
    public void Resetting_the_drawing_engine_forgets_its_drawings_and_its_page_and_the_next_schema_draws_again()
    {
        using var ui = new UiThread();
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        var theme = Theme();
        var first = Wait(engine.DrawAsync(Schema, theme));
        Assert.IsNotNull(first);
        Assert.AreEqual(1, engine.CachedCount);
        Assert.AreEqual("1", Run(engine, "window.vu = 1"));

        engine.Reset();

        Assert.AreEqual(0, engine.CachedCount);

        // Le même schéma ne sort plus de ce qui était gardé : il se redessine, et se garde à nouveau.
        var drawing = engine.DrawAsync(Schema, theme);
        Assert.IsFalse(drawing.IsCompleted, "rien n'est plus gardé");
        var again = Wait(drawing);
        Assert.IsNotNull(again);
        Assert.AreNotSame(first, again);
        Assert.AreEqual(1, engine.CachedCount);

        // La page cachée a été fermée avec ce qu'elle avait vu : celle qui a repris part de rien.
        Assert.AreEqual("\"undefined\"", Run(engine, "typeof window.vu"));
    }

    [EngineTest]
    public void A_drawing_asked_before_the_reset_gives_nothing_and_is_not_kept()
    {
        using var ui = new UiThread();
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        var theme = Theme();
        Assert.IsNotNull(Wait(engine.DrawAsync("flowchart LR\n  a --> b", theme)));
        engine.Reset();

        // Le moteur redémarre pour ce schéma quand le coffre se verrouille.
        var starting = engine.DrawAsync(Schema, theme);
        Assert.IsFalse(starting.IsCompleted);
        engine.Reset();

        Assert.IsNull(Wait(starting));
        Assert.AreEqual(0, engine.CachedCount);

        // Le moteur a démarré, le schéma se dessine quand le coffre se verrouille.
        Assert.IsNotNull(Wait(engine.DrawAsync("flowchart LR\n  c --> d", theme)));
        var drawing = engine.DrawAsync(Schema, theme);
        Assert.IsFalse(drawing.IsCompleted);
        engine.Reset();

        Assert.IsNull(Wait(drawing));
        Assert.AreEqual(0, engine.CachedCount);

        // Le moteur repart au schéma suivant.
        Assert.IsNotNull(Wait(engine.DrawAsync(Schema, theme)));
        Assert.AreEqual(1, engine.CachedCount);
    }

    [TestMethod]
    public void Resetting_an_engine_that_never_started_changes_nothing()
    {
        using var engine = new MermaidEngine(FakeEnvironment.WebData);

        engine.Reset();
        engine.Reset();

        Assert.AreEqual(0, engine.CachedCount);
    }

    [EngineTest]
    public void Forgetting_every_fault_empties_each_extension_and_says_so_once_for_each()
    {
        using var scene = new ExtensionScene();
        scene.Install("fautive", """kyfrev.vue(() => { throw new Error("boum : la fiche « Mitose »"); });""");
        scene.Install("autre", """kyfrev.vue(() => { throw new Error("boum : la fiche « Méiose »"); });""");
        scene.Install("saine", """kyfrev.vue(() => "saine");""");
        using var ui = new UiThread();
        var changed = new List<string>();
        scene.Engine.FaultsChanged += (_, e) => changed.Add(e.Extension);

        // Rien à oublier : rien n'est dit.
        scene.Engine.ForgetAllFaults();
        Assert.IsEmpty(changed);

        Assert.IsFalse(Wait(scene.Engine.RenderViewAsync("fautive", dark: false)).Succeeded);
        Assert.IsFalse(Wait(scene.Engine.RenderViewAsync("fautive", dark: false)).Succeeded);
        Assert.IsFalse(Wait(scene.Engine.RenderViewAsync("autre", dark: false)).Succeeded);
        Assert.IsTrue(Wait(scene.Engine.RenderViewAsync("saine", dark: false)).Succeeded);
        Assert.HasCount(2, scene.Engine.Faults("fautive"));
        Assert.Contains("Mitose", scene.Engine.Faults("fautive")[0].Message);
        Assert.HasCount(1, scene.Engine.Faults("autre"));
        Assert.IsEmpty(scene.Engine.Faults("saine"));
        changed.Clear();

        scene.Engine.ForgetAllFaults();

        Assert.IsEmpty(scene.Engine.Faults("fautive"));
        Assert.IsEmpty(scene.Engine.Faults("autre"));
        Assert.IsEmpty(scene.Engine.Faults("saine"));
        Assert.AreEqual("autre fautive", string.Join(' ', changed.Order(StringComparer.Ordinal)), "Chaque extension qui avait des erreurs l'apprend, une fois.");

        // Les oublier encore ne dit plus rien.
        scene.Engine.ForgetAllFaults();
        Assert.HasCount(2, changed);

        // Les extensions restent installées et le moteur répond : une nouvelle erreur est la seule gardée.
        Assert.IsFalse(Wait(scene.Engine.RenderViewAsync("autre", dark: false)).Succeeded);
        Assert.HasCount(1, scene.Engine.Faults("autre"));
        Assert.IsEmpty(scene.Engine.Faults("fautive"));
        Assert.AreEqual("autre", changed[^1]);
    }

    /// <summary>Les couleurs du thème clair, sous les noms de Mermaid.</summary>
    private static IReadOnlyDictionary<string, string> Theme()
    {
        var element = new Border();
        element.Resources.MergedDictionaries.Add(new DesignResources(ThemeKind.Light, reducedMotion: false).Dictionary);
        return MermaidEngine.ThemeOf(element);
    }

    private static T Wait<T>(Task<T> task)
    {
        UiThread.Until(() => task.IsCompleted, Long);
        return task.Result;
    }

    private static string Run(MermaidEngine engine, string script) => Wait(engine.RunScriptAsync(script));

    /// <summary>Une bibliothèque d'extensions d'essai et leur moteur.</summary>
    private sealed class ExtensionScene : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "kyfrev-oubli-moteurs-" + Guid.NewGuid().ToString("N"));

        public ExtensionScene()
        {
            Directory.CreateDirectory(_root);
            Library = new ExtensionLibrary(Path.Combine(_root, "bibliotheque"), TimeProvider.System);
            Engine = new ExtensionEngine(FakeEnvironment.ExtensionData, Library, new ExtensionRequests(Library, new NoData()));
        }

        public ExtensionLibrary Library { get; }

        public ExtensionEngine Engine { get; }

        /// <summary>Installe une extension d'essai, sans permission.</summary>
        public void Install(string id, string script)
        {
            var source = Path.Combine(_root, "sources", id);
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, ExtensionManifest.FileName), JsonSerializer.Serialize(new { id, nom = id, version = "1", vue = new { titre = id } }));
            File.WriteAllText(Path.Combine(source, "extension.js"), script);
            var installed = Library.Install(source);
            Assert.IsTrue(installed.Succeeded, installed.Error);
        }

        public void Dispose()
        {
            Engine.Dispose();
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Un coffre dont les extensions n'obtiennent rien : celles de ces essais n'en lisent rien.</summary>
    private sealed class NoData : IExtensionData
    {
        public Task<Outcome<IReadOnlyList<ExtensionSheet>>> SheetsAsync() => Task.FromResult(Outcome.Of<IReadOnlyList<ExtensionSheet>>([]));

        public Task<Outcome<ExtensionSheetText>> SheetAsync(string id) => Task.FromResult(Outcome.Refused<ExtensionSheetText>("non"));

        public Task<Outcome<string?>> StoredAsync(string extension) => Task.FromResult(Outcome.Of<string?>(null));

        public Task<Outcome<Done>> StoreAsync(string extension, string json) => Task.FromResult(Outcome.Refused<Done>("non"));

        public Task<Outcome<string>> CreateSheetAsync(string extension, ExtensionNewSheet sheet) => Task.FromResult(Outcome.Refused<string>("non"));

        public Task<Outcome<IReadOnlyList<string>>> SetTagsAsync(string extension, string id, IReadOnlyList<string> tags) => Task.FromResult(Outcome.Refused<IReadOnlyList<string>>("non"));

        public Task<Outcome<Done>> WriteSheetAsync(string extension, string id, string text) => Task.FromResult(Outcome.Refused<Done>("non"));

        public Task<Outcome<IReadOnlyList<ExtensionJournalEntry>>> JournalAsync(string extension) => Task.FromResult(Outcome.Of<IReadOnlyList<ExtensionJournalEntry>>([]));
    }
}
