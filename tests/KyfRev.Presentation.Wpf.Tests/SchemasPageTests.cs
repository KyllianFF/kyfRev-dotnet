using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Modules.Schemas;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Printing;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Presentation.Wpf.Shell;

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
    public async Task The_bridge_copies_the_picture_of_a_schema_and_only_a_picture()
    {
        using var scene = new BridgeScene();
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAEBgIApD5fRAAAAABJRU5ErkJggg==");

        // « Partager » : l'image du schéma, telle que l'îlot l'a dessinée, va à ce qui la copie.
        Assert.IsNull(await scene.RawValueAsync("copierImageSchema", Convert.ToBase64String(png)));
        CollectionAssert.AreEqual(png, scene.Copied.Single());

        // Autre chose qu'une image PNG, rien du tout, ou du texte qui n'est pas du base64 : refusé, rien n'est copié.
        foreach (var bad in new[] { Convert.ToBase64String("<svg/>"u8.ToArray()), string.Empty, "pas du base64 !" })
        {
            var answer = await scene.AnswerAsync(JsonSerializer.Serialize(new { kind = "call", id = 2, method = "copierImageSchema", args = new[] { bad } }));
            Assert.IsFalse(answer["ok"]!.GetValue<bool>(), bad);
        }
        Assert.HasCount(1, scene.Copied);

        // Sans rien pour copier, l'appel est refusé comme un appel inconnu.
        var bare = new SchemaBridge(new SchemaUseCases(scene.Vaults.App.Host), scene.Picker, () => null, _ => { });
        var refused = JsonNode.Parse((await bare.ReceiveAsync(JsonSerializer.Serialize(new { kind = "call", id = 3, method = "copierImageSchema", args = new[] { Convert.ToBase64String(png) } })))!)!;
        Assert.IsFalse(refused["ok"]!.GetValue<bool>());
        Assert.AreEqual(SchemaText.BadCall, refused["error"]!.GetValue<string>());
    }

    [STATestMethod]
    public void The_page_copies_a_picture_to_the_clipboard_and_says_when_it_cannot()
    {
        using var scene = new BridgeScene();
        using var page = scene.Page();
        // Une image illisible : un refus dit en clair, pas une exception qui remonte.
        var refusal = Assert.ThrowsExactly<InvalidOperationException>(() => page.CopyImage([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3]));
        Assert.AreEqual(SchemaText.ClipboardBusy, refusal.Message);
    }

    [TestMethod]
    public async Task The_bridge_says_whether_the_open_vault_is_encrypted()
    {
        using var scene = new BridgeScene();
        Assert.IsFalse((await scene.CallAsync("coffreChiffre")).GetValue<bool>(), "sans rien pour le dire, le coffre est tenu pour en clair");

        var encrypted = false;
        var bridge = new SchemaBridge(new SchemaUseCases(scene.Vaults.App.Host), scene.Picker, () => null, _ => { }, isEncrypted: () => encrypted);
        async Task<bool> SaysAsync() =>
            JsonNode.Parse((await bridge.ReceiveAsync(JsonSerializer.Serialize(new { kind = "call", id = 1, method = "coffreChiffre", args = Array.Empty<string>() })))!)!["value"]!.GetValue<bool>();
        Assert.IsFalse(await SaysAsync());
        encrypted = true;
        Assert.IsTrue(await SaysAsync(), "demandé à chaque fois : le coffre ouvert peut avoir changé");

        // La page donne à son pont ce qu'on lui a donné.
        using var page = new SchemasPageViewModel(new SchemaUseCases(scene.Vaults.App.Host), scene.Picker, new FakeOpener(), FakeEnvironment.WebData) { IsEncrypted = () => true };
        var answer = JsonNode.Parse((await page.Bridge().ReceiveAsync(JsonSerializer.Serialize(new { kind = "call", id = 2, method = "coffreChiffre", args = Array.Empty<string>() })))!)!;
        Assert.IsTrue(answer["value"]!.GetValue<bool>());
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
        using var page = scene.Page();
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
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void The_simple_mode_places_devices_and_cables_without_ports_circles_aggregates_and_labels_trunks(ThemeKind theme)
    {
        using var scene = new IslandScene(theme: theme);
        scene.WaitFor("document.querySelector('.bouton.principal') !== null");
        scene.Script("document.querySelector('.bouton.principal').click()");
        scene.WaitFor("document.querySelector('dialog[open] input') !== null");
        scene.Script("const champ = document.querySelector('dialog[open] input'); champ.value = 'Site de Meylan'; champ.dispatchEvent(new Event('input', { bubbles: true })); document.querySelector('dialog[open] form').requestSubmit()");
        scene.WaitFor("document.querySelector('dialog[open] svg.canevas') !== null");
        int Count(string selector) => int.Parse(scene.Script($"document.querySelectorAll('{selector}').length"), System.Globalization.CultureInfo.InvariantCulture);
        string Text(string expression) => JsonSerializer.Deserialize<string>(scene.Script(expression)) ?? string.Empty;

        // Les deux modes, et le même plateau d'équipements en bas dans l'un comme dans l'autre.
        scene.Script("document.querySelector('.segments.mode button[data-mode=\"simplifie\"]').click()");
        scene.WaitFor("document.querySelector('.segments.mode button[data-mode=\"simplifie\"]').getAttribute('aria-pressed') === 'true'");
        Assert.AreEqual(1, Count("footer.plateau"));
        Assert.IsGreaterThanOrEqualTo(5, Count("footer.plateau .tuile"));
        Assert.IsGreaterThanOrEqualTo(Count("footer.plateau .tuile"), Count("footer.plateau .tuile svg"), "chaque tuile montre l'icône de l'équipement");
        StringAssert.StartsWith(Text("document.querySelector('.tuile[data-genre=\"type:switch\"]').getAttribute('aria-label')"), "Ajouter", StringComparison.Ordinal);

        // Deux équipements posés depuis le plateau, au clavier : la tuile, puis Entrée.
        foreach (var genre in new[] { "type:switch-coeur", "type:switch" })
        {
            var before = Count("svg.canevas g.equipement");
            scene.Script($"(() => {{ const tuile = document.querySelector('.tuile[data-genre=\"{genre}\"]'); tuile.focus(); tuile.click(); }})()");
            UiThread.Until(() => Count("svg.canevas g.equipement") == before + 1, TimeSpan.FromSeconds(15));
        }

        // Un équipement sans adresse n'écrit rien sous son nom : ni « sans adresse », ni rien d'autre.
        var drawn = Text("document.querySelector('svg.canevas').textContent");
        Assert.IsFalse(drawn.Contains("adresse", StringComparison.OrdinalIgnoreCase), drawn);

        // Deux câbles tirés entre eux, sans choisir de port : l'outil Câble, un équipement, puis l'autre ; et on recommence.
        void Cable()
        {
            var before = Count("svg.canevas g.lien .trait");
            scene.Script("document.querySelector('nav.outils .outil[data-outil=\"cable\"]').click()");
            scene.Script("(() => { for (const equipement of document.querySelectorAll('svg.canevas g.equipement')) { equipement.focus(); equipement.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', bubbles: true, cancelable: true })); } })()");
            UiThread.Until(() => Count("svg.canevas g.lien .trait") == before + 1, TimeSpan.FromSeconds(15));
        }
        Cable();

        // Le second à la souris, en deux clics sans glisser : le pointillé qui suit le pointeur ne prend pas le second clic.
        var centres = JsonSerializer.Deserialize<double[][]>(Text("JSON.stringify([...document.querySelectorAll('svg.canevas g.equipement')].map((g) => { const r = g.getBoundingClientRect(); return [r.x + r.width / 2, r.y + r.height / 2]; }))"))!;
        var cables = Count("svg.canevas g.lien .trait");
        scene.Script("document.querySelector('nav.outils .outil[data-outil=\"cable\"]').click()");
        scene.Click(centres[0][0], centres[0][1]);
        scene.WaitFor("document.body.innerText.includes('cliquez l\\'autre équipement')");
        scene.Mouse("mouseMoved", centres[1][0], centres[1][1]);
        var there = string.Join(',', centres[1].Select(c => c.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.AreEqual("true", scene.Script("document.querySelector('line.fil') !== null"), "le pointillé suit le pointeur jusqu'à l'autre équipement");
        Assert.AreEqual("true", scene.Script($"document.elementFromPoint({there}).closest('g.equipement') !== null"), "sous le pointeur, c'est l'équipement qui reçoit le clic, pas le pointillé");
        scene.Click(centres[1][0], centres[1][1]);
        UiThread.Until(() => Count("svg.canevas g.lien .trait") == cables + 1, TimeSpan.FromSeconds(15));
        Assert.AreEqual(0, Count("dialog[open] dialog[open]"), "aucune fenêtre de connexion ne s'ouvre");
        scene.Picture($"schemas-simplifie-{(theme == ThemeKind.Dark ? "sombre" : "clair")}-cables");

        // Le fichier a de vrais ports et deux vrais liens : le mode avancé les retrouvera.
        var id = scene.Schemas.Schemas().Value.Single().Id;
        UiThread.Until(() => (JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!["liens"] as JsonArray)?.Count == 2, TimeSpan.FromSeconds(20));
        var links = (JsonArray)JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!["liens"]!;
        foreach (var link in links)
        {
            Assert.IsFalse(string.IsNullOrEmpty(link!["a"]!["interface"]!.GetValue<string>()));
            Assert.IsFalse(string.IsNullOrEmpty(link["b"]!["interface"]!.GetValue<string>()));
        }
        Assert.AreNotEqual(links[0]!["a"]!["interface"]!.GetValue<string>(), links[1]!["a"]!["interface"]!.GetValue<string>(), "chaque câble a son port");

        // Le câble choisi : sa bulle offre deux options distinctes, « Agrégat de liens » et « Trunk ».
        Assert.AreEqual(0, Count("svg.canevas g.lien ellipse"));
        scene.Script("document.querySelector('nav.outils .outil[data-outil=\"choisir\"]').click()");
        scene.Script("(() => { const lien = document.querySelector('svg.canevas g.lien'); lien.focus(); lien.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', bubbles: true, cancelable: true })); })()");
        scene.WaitFor("document.querySelector('.bulle button[role=switch][data-option=\"agregat\"]') !== null && document.querySelector('.bulle button[role=switch][data-option=\"trunk\"]') !== null");
        // Un câble se choisit seul, même si un autre relie les deux mêmes équipements : lui seul est en surbrillance, sans cadre.
        Assert.AreEqual(1, Count("svg.canevas g.lien.choisi"));
        Assert.AreEqual(1, Count("svg.canevas path.halo"));
        Assert.AreEqual(0, Count("svg.canevas rect.cadre-objet"));
        string Labels() => Text("[...document.querySelectorAll('svg.canevas .etiquettes text')].map((texte) => texte.textContent.trim()).join(' | ')");
        void Type(string field, string value) =>
            scene.Script($"(() => {{ const champ = document.getElementById('{field}'); champ.value = '{value}'; champ.dispatchEvent(new Event('input', {{ bubbles: true }})); champ.dispatchEvent(new Event('change', {{ bubbles: true }})); }})()");
        // Deux câbles entre deux commutateurs, sans agrégat : la bulle conseille, sans rien bloquer.
        StringAssert.Contains(Text("[...document.querySelectorAll('.bulle [role=note]')].map((note) => note.textContent).join(' ')"), "boucle", StringComparison.Ordinal);
        // Sans trunk, les ports sont en accès : un seul champ, le VLAN non tagué ; le câble s'annote « Access · U 30 ».
        Assert.AreEqual(0, Count("#bulle-vlan-non-tague"), "sans trunk, pas les champs du trunk");
        Assert.AreEqual(1, Count("#bulle-vlan-acces"));
        Type("bulle-vlan-acces", "4095");
        scene.WaitFor("document.querySelector('.bulle [role=alert]') !== null");
        Type("bulle-vlan-acces", "30");
        UiThread.Until(() => Labels().Contains("Access · U 30", StringComparison.Ordinal), TimeSpan.FromSeconds(15));
        Assert.AreEqual(0, Count(".bulle [role=alert]"), "corrigé, plus d'erreur");
        Assert.AreEqual(1, Labels().Split(" | ").Count(label => label.StartsWith("Access", StringComparison.Ordinal)), "seul le câble choisi est en accès sur ce VLAN");
        scene.Picture($"schemas-simplifie-{(theme == ThemeKind.Dark ? "sombre" : "clair")}-acces");

        // Le câble choisi se tire à la souris, comme en mode avancé : son tracé change et s'enregistre, il reste choisi,
        // et sa bulle revient où il s'arrête. On le saisit là où lui seul est sous le pointeur, l'autre câble passant à côté.
        bool Routed() => ((JsonArray)JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!["liens"]!)
            .Any(link => (link!["points"] as JsonArray)?.Count > 0 || link["palier"] is not null);
        Assert.IsFalse(Routed(), "aucun câble n'a encore de tracé à lui");
        var grip = JsonSerializer.Deserialize<double[]?>(Text("""
            (() => {
              const lien = document.querySelector('svg.canevas g.lien.choisi');
              const prise = lien.querySelector('path.prise');
              const m = prise.getScreenCTM();
              const total = prise.getTotalLength();
              for (let f = 0.15; f < 0.9; f += 0.05) {
                const p = prise.getPointAtLength(total * f);
                for (const d of [0, -5, 5, -3, 3]) {
                  for (const [ox, oy] of [[d, 0], [0, d]]) {
                    const x = p.x * m.a + p.y * m.c + m.e + ox;
                    const y = p.x * m.b + p.y * m.d + m.f + oy;
                    const sous = document.elementFromPoint(x, y);
                    if (sous && sous.classList.contains('prise') && sous.closest('g.lien') === lien) {
                      // Le pointeur part en travers du câble : le nouveau tracé se voit.
                      const q = prise.getPointAtLength(Math.min(total, total * f + 10));
                      const tx = (q.x - p.x) * m.a + (q.y - p.y) * m.c;
                      const ty = (q.x - p.x) * m.b + (q.y - p.y) * m.d;
                      const n = Math.hypot(tx, ty) || 1;
                      return JSON.stringify([x, y, -ty / n, tx / n]);
                    }
                  }
                }
              }
              return JSON.stringify(null);
            })()
            """));
        Assert.IsNotNull(grip, "un point du câble choisi, sous le pointeur");
        scene.Drag(grip[0], grip[1], grip[0] + (60 * grip[2]), grip[1] + (60 * grip[3]));
        UiThread.Until(Routed, TimeSpan.FromSeconds(15));
        scene.WaitFor("document.querySelectorAll('svg.canevas g.lien.choisi').length === 1 && document.querySelector('.bulle') !== null");
        var bends = ((JsonArray)JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!["liens"]!).Max(link => (link!["points"] as JsonArray)?.Count ?? 0);
        Assert.AreEqual(bends, Count("svg.canevas circle.passage"), "les points de passage du câble choisi se montrent, pour les tirer ou les retirer");

        // Échap : sans la bulle, le nouveau tracé se voit. Puis le câble est repris au clavier pour la suite.
        scene.Script("document.querySelector('svg.canevas').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', code: 'Escape', bubbles: true, cancelable: true }))");
        scene.WaitFor("document.querySelectorAll('svg.canevas g.lien.choisi').length === 0 && document.querySelector('.bulle') === null");
        scene.Picture($"schemas-simplifie-{(theme == ThemeKind.Dark ? "sombre" : "clair")}-trace");
        scene.Script("(() => { const lien = document.querySelector('svg.canevas g.lien'); lien.focus(); lien.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', bubbles: true, cancelable: true })); })()");
        scene.WaitFor("document.getElementById('bulle-vlan-acces') !== null");

        // « Trunk » : il libère deux champs, et le câble est annoté de ses VLAN. Pas de cercle : le cercle est pour l'agrégat.
        scene.Script("document.querySelector('.bulle button[role=switch][data-option=\"trunk\"]').click()");
        scene.WaitFor("document.getElementById('bulle-vlan-non-tague') !== null && document.getElementById('bulle-vlans-tagues') !== null");
        UiThread.Until(() => Labels().Contains("Trunk", StringComparison.Ordinal), TimeSpan.FromSeconds(15));
        Type("bulle-vlan-non-tague", "99");
        Type("bulle-vlans-tagues", "10,20,30");
        UiThread.Until(() => Labels().Contains("Trunk · U 99 · T 10,20,30", StringComparison.Ordinal), TimeSpan.FromSeconds(15));
        Assert.AreEqual(0, Count("svg.canevas g.lien ellipse"), "un trunk seul n'a pas de cercle");
        Assert.AreEqual(2, Count("svg.canevas g.lien .trait"));
        scene.Picture($"schemas-simplifie-{(theme == ThemeKind.Dark ? "sombre" : "clair")}-trunk");

        // Le VLAN non tagué ne se répète pas dans les tagués : dit en clair, et rien n'est écrit.
        Type("bulle-vlans-tagues", "10,99");
        scene.WaitFor("document.querySelector('.bulle [role=alert]') !== null");
        StringAssert.Contains(Text("document.querySelector('.bulle [role=alert]').textContent"), "99", StringComparison.Ordinal);
        Assert.IsTrue(Labels().Contains("T 10,20,30", StringComparison.Ordinal), "le dessin garde les VLAN valides");
        Type("bulle-vlans-tagues", "10,20,30");

        // Ctrl+Entrée, comme Ctrl+clic, choisit l'autre câble avec le premier : la bulle les règle ensemble.
        scene.Script("(() => { const lien = document.querySelectorAll('svg.canevas g.lien')[1]; lien.focus(); lien.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', ctrlKey: true, bubbles: true, cancelable: true })); })()");
        scene.WaitFor("document.querySelectorAll('svg.canevas g.lien.choisi').length === 2 && document.getElementById('bulle-choix') !== null");
        StringAssert.Contains(Text("document.getElementById('bulle-choix').textContent"), "2 câbles choisis", StringComparison.Ordinal);
        Assert.AreEqual(2, Count("svg.canevas path.halo"));
        scene.Picture($"schemas-simplifie-{(theme == ThemeKind.Dark ? "sombre" : "clair")}-deux-choisis");

        // « Agrégat de liens » : les deux câbles sous un cercle, autant de traits que de câbles, et une seule annotation de trunk.
        scene.Script("document.querySelector('.bulle button[role=switch][data-option=\"agregat\"]').click()");
        UiThread.Until(() => Count("svg.canevas g.lien ellipse") == 1, TimeSpan.FromSeconds(15));
        Assert.AreEqual(2, Count("svg.canevas g.lien .trait"), "le nombre de câbles se voit toujours");
        Assert.AreEqual(1, Labels().Split(" | ").Count(label => label.StartsWith("Trunk", StringComparison.Ordinal)), "une seule annotation pour le groupe");
        StringAssert.Contains(Labels(), "Trunk · U 99 · T 10,20,30", StringComparison.Ordinal, "le trunk suit l'agrégat");
        scene.Picture($"schemas-simplifie-{(theme == ThemeKind.Dark ? "sombre" : "clair")}-agregat");

        // Les autres types de câble, dont « Haute disponibilité » : son nom tient dans son bouton.
        scene.Script("document.querySelector('.bulle .type.plus').click()");
        scene.WaitFor("document.querySelector('.bulle .type[data-support=\"haute-disponibilite\"]') !== null");
        Assert.AreEqual("true", scene.Script("(() => { const bouton = document.querySelector('.bulle .type[data-support=\"haute-disponibilite\"]'); return bouton.scrollWidth <= bouton.clientWidth + 1; })()"));
        scene.Picture($"schemas-simplifie-{(theme == ThemeKind.Dark ? "sombre" : "clair")}-types");

        // Le fichier : un vrai agrégat, qui porte le trunk et ses VLAN.
        UiThread.Until(() => (JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!["agregats"] as JsonArray)?.Count >= 1, TimeSpan.FromSeconds(20));
        var written = scene.Schemas.Read(id).Value.Text;
        StringAssert.Contains(written, "\"trunk\"", StringComparison.Ordinal);
        StringAssert.Contains(written, "10,20,30", StringComparison.Ordinal);

        // En avancé, le même schéma, ses ports, et le même plateau.
        scene.Script("document.querySelector('.segments.mode button[data-mode=\"avance\"]').click()");
        scene.WaitFor("document.querySelector('.segments.mode button[data-mode=\"avance\"]').getAttribute('aria-pressed') === 'true'");
        Assert.AreEqual(1, Count("footer.plateau"));
        Assert.AreEqual(2, Count("svg.canevas g.equipement"));
        Assert.AreEqual(2, Count("svg.canevas g.lien .trait"));
        Assert.AreEqual(1, Count("svg.canevas g.lien ellipse"));
        scene.Picture($"schemas-avance-{(theme == ThemeKind.Dark ? "sombre" : "clair")}");
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Pas de défilement horizontal.");
    }

    /// <summary>Un cœur et un switch reliés par un trunk : des ports, une annotation de VLAN, une adresse.</summary>
    private const string Trunk = """
        {"format": 2, "titre": "Étiquettes", "vue": "physique",
         "equipements": [
          {"id": "d_coeur", "type": "switch-coeur", "nom": "Cœur", "position": {"x": 0, "y": 0}, "gestion": {"adresse": "10.0.0.1/24"},
           "interfaces": [{"id": "i_c1", "nom": "1/1", "genre": "ethernet", "vlan": {"mode": "trunk", "natif": 1, "tagues": "10,20,30"}}]},
          {"id": "d_sw", "type": "switch", "nom": "SW-Étage1", "position": {"x": 360, "y": 0},
           "interfaces": [{"id": "i_s1", "nom": "1/25", "genre": "ethernet", "vlan": {"mode": "trunk", "natif": 1, "tagues": "10,20,30"}}]}],
         "liens": [{"id": "l_trunk", "a": {"equipement": "d_coeur", "interface": "i_c1"}, "b": {"equipement": "d_sw", "interface": "i_s1"}, "support": "cuivre", "points": []}]}
        """;

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void Labels_are_moved_and_turned_by_hand_in_both_modes_and_kept_with_the_schema(ThemeKind theme)
    {
        using var scene = new IslandScene(Trunk, theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        scene.WaitFor("[...document.querySelectorAll('li.carte button')].some((b) => b.textContent.includes('Modifier'))");
        scene.Script("[...document.querySelectorAll('li.carte button')].find((b) => b.textContent.includes('Modifier')).click()");
        scene.WaitFor("document.querySelector('dialog[open] svg.canevas') !== null");
        scene.Script("document.querySelector('.segments.mode button[data-mode=\"simplifie\"]').click()");
        scene.WaitFor("document.querySelector('.segments.mode button[data-mode=\"simplifie\"]').getAttribute('aria-pressed') === 'true'");
        var id = scene.Schemas.Schemas().Value.Single().Id;
        JsonNode Saved() => JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!;
        JsonNode? Pose(string key) => Saved()["liens"]![0]!["poses"]?[key];
        int Count(string selector) => int.Parse(scene.Script($"document.querySelectorAll('{selector}').length"), CultureInfo.InvariantCulture);
        double[] Centre(string selector) => JsonSerializer.Deserialize<double[]>(JsonSerializer.Deserialize<string>(scene.Script(
            "(() => { const r = document.querySelector('" + selector + "').getBoundingClientRect(); return JSON.stringify([r.x + r.width / 2, r.y + r.height / 2]); })()"))!)!;
        // Un point de l'élément où lui seul est sous le pointeur : une pastille collée à un équipement est en partie sous
        // son icône, et selon les polices son centre peut l'être.
        double[] Grip(string selector) => JsonSerializer.Deserialize<double[]?>(JsonSerializer.Deserialize<string>(scene.Script(
            "(() => { const e = document.querySelector('" + selector + "'); const r = e.getBoundingClientRect(); for (const fy of [0.5, 0.35, 0.65]) for (const fx of [0.5, 0.35, 0.65, 0.2, 0.8]) { const x = r.x + r.width * fx, y = r.y + r.height * fy; const s = document.elementFromPoint(x, y); if (s && (s === e || e.contains(s))) return JSON.stringify([x, y]); } return JSON.stringify(null); })()"))!)
            ?? throw new AssertFailedException($"aucun point de « {selector} » sous le pointeur");
        const string vlanLabel = "g.etiquette[data-etiquette=\"l_trunk|vlan\"]";

        // Mode simplifié : l'étiquette du trunk se prend seule (pas son câble), et se tire sous le câble.
        Assert.AreEqual(1, Count(vlanLabel));
        // Le plan se cadre sur le schéma à sa première image, que cette fenêtre hors de l'écran ne dessine qu'au premier mouvement.
        scene.Mouse("mouseMoved", 4, 4);
        scene.WaitFor("document.querySelector('svg.canevas').getAttribute('viewBox') !== '-400 -300 800 600'");
        var label = Grip(vlanLabel);
        scene.Drag(label[0], label[1], label[0], label[1] + 50);
        UiThread.Until(() => Pose("vlan")?["place"] is not null, TimeSpan.FromSeconds(20));
        Assert.IsGreaterThan(10d, Math.Abs(Pose("vlan")!["place"]!["y"]!.GetValue<double>()), "écartée du câble, en travers");
        Assert.AreEqual(1, Count("g.etiquette.choisie"));
        Assert.AreEqual(0, Count("svg.canevas g.lien.choisi"), "le câble n'est pas choisi : seulement son étiquette");
        Assert.AreEqual(0, Count(".bulle"), "pas de bulle de câble");
        Assert.AreEqual(1, Count(".poignee-rotation"), "la poignée de rotation, au-dessus de l'étiquette choisie");
        Assert.AreEqual(1, Count("line.attache"), "un fil relie l'étiquette déplacée à son câble");
        Assert.AreEqual(1, Count(".barre-etiquette button[data-action=\"remettre\"]:not([disabled])"));
        scene.Picture($"schemas-etiquette-simplifie-{suffix}-deplacee");

        // La poignée : par crans de 15°. Tirée à 38° de la verticale, l'étiquette se pose à 45°. Le geste en cours montre les crans et l'angle.
        double[] Around(double[] centre, double degrees, double radius) =>
            [centre[0] + (radius * Math.Sin(degrees * Math.PI / 180)), centre[1] - (radius * Math.Cos(degrees * Math.PI / 180))];
        var middle = Centre(".etiquette-choisie rect.cadre-choisi");
        var handle = Centre(".poignee-rotation circle");
        var target = Around(middle, 38, 70);
        scene.Drag(handle[0], handle[1], target[0], target[1], release: false);
        scene.WaitFor("document.querySelectorAll('.crans line.atteint').length === 1 && document.querySelector('.bulle-angle text').textContent === '45°'");
        scene.Picture($"schemas-etiquette-simplifie-{suffix}-rotation");
        scene.Mouse("mouseReleased", target[0], target[1]);
        UiThread.Until(() => Pose("vlan")?["angle"]?.GetValue<double>() == 45, TimeSpan.FromSeconds(20));
        Assert.IsNotNull(Pose("vlan")!["place"], "tournée, elle garde sa place");

        // Alt maintenu : au degré près.
        middle = Centre(".etiquette-choisie rect.cadre-choisi");
        handle = Centre(".poignee-rotation circle");
        target = Around(middle, -27, 70);
        scene.Drag(handle[0], handle[1], target[0], target[1], modifiers: 1);
        UiThread.Until(() => Pose("vlan")?["angle"]?.GetValue<double>() is { } angle && angle is > -32 and < -22 && angle % 15 != 0, TimeSpan.FromSeconds(20));

        // Au clavier : R, un cran de plus ; Échap lâche l'étiquette.
        Assert.AreEqual("true", scene.Script("document.querySelector('svg.canevas').contains(document.activeElement)"), "le plan a la main");
        scene.Script("document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'r', code: 'KeyR', bubbles: true, cancelable: true }))");
        UiThread.Until(() => Pose("vlan")?["angle"]?.GetValue<double>() == -15, TimeSpan.FromSeconds(20));
        scene.Script("document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', code: 'Escape', bubbles: true, cancelable: true }))");
        scene.WaitFor("document.querySelectorAll('g.etiquette.choisie').length === 0 && document.querySelector('.poignee-rotation') === null");

        // « Remettre à sa place » : position et angle oubliés, l'étiquette revient sur son câble.
        label = Grip(vlanLabel);
        scene.Click(label[0], label[1]);
        scene.WaitFor("document.querySelector('.barre-etiquette button[data-action=\"remettre\"]') !== null");
        scene.Script("document.querySelector('.barre-etiquette button[data-action=\"remettre\"]').click()");
        UiThread.Until(() => Saved()["liens"]![0]!["poses"] is null, TimeSpan.FromSeconds(20));
        scene.WaitFor("document.querySelector('line.attache') === null");

        // Reposée à la main, pour la suite : elle doit tenir après la fermeture.
        label = Grip(vlanLabel);
        scene.Drag(label[0], label[1], label[0] + 30, label[1] - 40);
        UiThread.Until(() => Pose("vlan")?["place"] is not null, TimeSpan.FromSeconds(20));

        // Mode avancé : les pastilles des ports se prennent de même, ici au clavier. Collée à son switch, celle-ci peut être
        // sous son icône (selon les polices, toute entière sur la machine de la CI) : le clavier l'atteint quand même.
        scene.Script("document.querySelector('.segments.mode button[data-mode=\"avance\"]').click()");
        scene.WaitFor("document.querySelector('.segments.mode button[data-mode=\"avance\"]').getAttribute('aria-pressed') === 'true'");
        const string portLabel = "g.etiquette[data-etiquette=\"l_trunk|port-b\"]";
        scene.WaitFor($"document.querySelector('{portLabel}') !== null");
        void Key(string key, bool shift = false) =>
            scene.Script("document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: '" + key + "', shiftKey: " + (shift ? "true" : "false") + ", bubbles: true, cancelable: true }))");
        // Le câble choisi, ses étiquettes se prennent au clavier : Entrée sur l'une la choisit, les flèches la déplacent.
        scene.Script("(() => { const lien = document.querySelector('svg.canevas g.lien'); lien.focus(); lien.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', bubbles: true, cancelable: true })); })()");
        scene.WaitFor($"document.querySelector('{portLabel}').getAttribute('tabindex') === '0'");
        scene.Script($"document.querySelector('{portLabel}').focus()");
        Key("Enter");
        scene.WaitFor("document.querySelectorAll('g.etiquette.choisie').length === 1");
        for (var step = 0; step < 4; step++)
        {
            Key("ArrowDown", shift: true);
        }
        UiThread.Until(() => Pose("port-b")?["place"] is not null, TimeSpan.FromSeconds(20));

        // Le nom et l'adresse d'un équipement : l'équipement choisi d'abord, puis son nom se tire, d'un seul bloc.
        scene.Script("(() => { const e = document.querySelector('svg.canevas g.equipement[data-id=\"d_coeur\"]'); e.focus(); e.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', bubbles: true, cancelable: true })); })()");
        scene.WaitFor("document.querySelectorAll('svg.canevas g.equipement.choisi').length === 1");
        var name = Grip("[data-bloc=\"d_coeur\"]");
        scene.Drag(name[0], name[1], name[0] - 150, name[1] - 20);
        UiThread.Until(() => Saved()["equipements"]![0]!["pose"]?["place"] is not null, TimeSpan.FromSeconds(20));
        Assert.AreEqual(0, Saved()["equipements"]![0]!["position"]!["x"]!.GetValue<double>(), "l'équipement n'a pas bougé : seulement son nom");
        Assert.AreEqual(1, Count("line.attache"));
        scene.Script("document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'r', code: 'KeyR', bubbles: true, cancelable: true }))");
        UiThread.Until(() => Saved()["equipements"]![0]!["pose"]?["angle"]?.GetValue<double>() == 15, TimeSpan.FromSeconds(20));
        scene.Picture($"schemas-etiquette-avance-{suffix}");
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Pas de défilement horizontal.");

        // Fermé puis rouvert : tout est relu. L'aperçu dessine les étiquettes là où on les a mises.
        scene.Script("document.querySelector('dialog[open] button.retour').click()");
        var written = scene.Schemas.Read(id).Value.Text;
        StringAssert.Contains(written, "\"poses\"", StringComparison.Ordinal);
        StringAssert.Contains(written, "\"pose\"", StringComparison.Ordinal);
        scene.WaitFor("document.querySelector('dialog[open] svg.canevas') === null");
        scene.WaitFor("document.querySelector('li.carte .apercu') !== null");
        scene.Script("document.querySelector('li.carte .apercu').click()");
        scene.WaitFor("document.querySelector('.grand-apercu svg')?.outerHTML.includes('rotate(15 ') === true");
        scene.Picture($"schemas-etiquette-apercu-{suffix}");
    }

    [EngineTest]
    public void A_right_click_on_a_schema_exports_shares_and_prints_it()
    {
        using var scene = new IslandScene(Text);
        scene.WaitFor("document.querySelector('li.carte') !== null");

        // Le vrai clic droit, dans l'îlot, sur la carte : le menu propose les trois sorties, entre « Dupliquer » et « Archiver ».
        scene.Script("document.querySelector('li.carte').dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 60, clientY: 60 }))");
        scene.WaitFor("document.querySelector('.menu[role=menu]') !== null");
        var order = JsonSerializer.Deserialize<string>(scene.Script("[...document.querySelectorAll('.menu [role=menuitem]')].map((entree) => entree.textContent.trim()).join(' | ')"))!;
        StringAssert.Contains(order, "Dupliquer | Exporter… | Partager (copier l'image) | Imprimer… | Archiver", StringComparison.Ordinal);
        Assert.AreEqual("true", scene.Script("(() => { const m = document.querySelector('.menu[role=menu]').getBoundingClientRect(); return m.left >= 0 && m.top >= 0 && m.right <= innerWidth && m.bottom <= innerHeight; })()"), "le menu tient dans la fenêtre");

        // « Partager » : l'îlot dessine le schéma, le pont reçoit une vraie image PNG, et l'îlot le dit.
        scene.Script("[...document.querySelectorAll('.menu [role=menuitem]')].find((entree) => entree.textContent.includes('Partager')).click()");
        UiThread.Until(() => scene.Copied.Count == 1, TimeSpan.FromSeconds(30));
        CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, scene.Copied[0].Take(4).ToArray(), "une image PNG");
        using (var stream = new MemoryStream(scene.Copied[0]))
        {
            var picture = System.Windows.Media.Imaging.BitmapFrame.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            Assert.IsGreaterThan(100, picture.PixelWidth, "qui se lit, avec le dessin dedans");
        }
        scene.WaitFor("document.querySelector('.annonces [role=status]').textContent.includes('copiée')");
        Assert.AreEqual("false", scene.Script("document.querySelector('.annonces [role=status]').textContent.includes('en clair')"));

        // « Exporter… » : la fenêtre des formats, puis le dossier choisi, et le fichier est écrit hors du coffre.
        var folder = scene.Outside("Exports");
        Directory.CreateDirectory(folder);
        scene.Picker.Answers.Enqueue(folder);
        scene.Script("document.querySelector('li.carte').dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 60, clientY: 60 }))");
        scene.WaitFor("document.querySelector('.menu[role=menu]') !== null");
        scene.Script("[...document.querySelectorAll('.menu [role=menuitem]')].find((entree) => entree.textContent.includes('Exporter')).click()");
        scene.WaitFor("document.querySelector('dialog[open] input[type=radio]') !== null");
        Assert.AreEqual("3", scene.Script("document.querySelectorAll('dialog[open] input[type=radio]').length"));
        Assert.AreEqual("true", scene.Script("document.querySelector('dialog[open] .en-clair') === null"), "un coffre en clair : rien à dire de l'export");
        scene.Script("(() => { const svg = [...document.querySelectorAll('dialog[open] input[type=radio]')].find((choix) => choix.value === 'svg'); svg.click(); document.querySelector('dialog[open] form').requestSubmit(); })()");
        UiThread.Until(() => Directory.Exists(folder) && Directory.GetFiles(folder, "*.svg").Length == 1, TimeSpan.FromSeconds(30));
        StringAssert.Contains(File.ReadAllText(Directory.GetFiles(folder, "*.svg").Single()), "<svg", StringComparison.Ordinal);

        // « Imprimer… » : la fenêtre « Imprimer » de l'application, sur une image fine du schéma ; l'îlot est recouvert le temps qu'elle est ouverte.
        var model = (SchemasPageViewModel)scene.Page.DataContext;
        scene.Script("document.querySelector('li.carte').dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 60, clientY: 60 }))");
        scene.WaitFor("document.querySelector('.menu[role=menu]') !== null");
        scene.Script("[...document.querySelectorAll('.menu [role=menuitem]')].find((entree) => entree.textContent.includes('Imprimer')).click()");
        UiThread.Until(() => model.Print.IsOpen, TimeSpan.FromSeconds(30));
        Assert.AreEqual("Imprimer « Réseau »", model.Print.Title);
        Assert.IsGreaterThanOrEqualTo(3000, Math.Max(model.Print.Picture!.PixelWidth, model.Print.Picture.PixelHeight), "assez fine pour le papier");
        UiThread.Until(() => scene.Page.Island!.IsCovered, TimeSpan.FromSeconds(10));
        model.Print.Close();
        UiThread.Until(() => !scene.Page.Island!.IsCovered, TimeSpan.FromSeconds(10));
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void What_leaves_a_schema_of_an_encrypted_vault_is_said_to_be_in_clear(ThemeKind theme)
    {
        using var scene = new IslandScene(Text, theme: theme, encrypted: true);
        scene.WaitFor("document.querySelector('li.carte') !== null");
        void Choose(string entry)
        {
            scene.Script("document.querySelector('li.carte').dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 60, clientY: 60 }))");
            scene.WaitFor("document.querySelector('.menu[role=menu]') !== null");
            scene.Script($"[...document.querySelectorAll('.menu [role=menuitem]')].find((entree) => entree.textContent.includes('{entry}')).click()");
        }

        // « Exporter… » : la fenêtre le dit avant le choix du dossier, sans rien empêcher, et tient dans la fenêtre.
        Choose("Exporter");
        scene.WaitFor("document.querySelector('dialog[open] .en-clair') !== null");
        var warning = JsonSerializer.Deserialize<string>(scene.Script("document.querySelector('dialog[open] .en-clair').textContent"))!;
        Assert.AreEqual("Ce coffre est chiffré, mais pas ce que vous exportez : le fichier créé est en clair, lisible par quiconque y accède.", warning);
        Assert.AreEqual("note", JsonSerializer.Deserialize<string>(scene.Script("document.querySelector('dialog[open] .en-clair').getAttribute('role')")));
        Assert.AreEqual("true", scene.Script("(() => { const d = document.querySelector('dialog[open]').getBoundingClientRect(); const a = document.querySelector('dialog[open] .en-clair').getBoundingClientRect(); return a.left >= d.left && a.right <= d.right && a.bottom <= d.bottom && d.bottom <= innerHeight; })()"), "l'avis tient dans la fenêtre d'export");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-export-chiffre-{(theme == ThemeKind.Dark ? "sombre" : "clair")}");
        var folder = scene.Outside("Exports");
        Directory.CreateDirectory(folder);
        scene.Picker.Answers.Enqueue(folder);
        scene.Script("document.querySelector('dialog[open] form').requestSubmit()");
        UiThread.Until(() => Directory.GetFiles(folder, "*.png").Length == 1, TimeSpan.FromSeconds(30));

        // « Partager » : l'image est dans le presse-papiers, et l'annonce dit qu'elle y est en clair.
        Choose("Partager");
        UiThread.Until(() => scene.Copied.Count == 1, TimeSpan.FromSeconds(30));
        scene.WaitFor("document.querySelector('.annonces [role=status]').textContent.includes('Ce coffre est chiffré, pas cette image')");
    }

    [EngineTest]
    public void A_schema_is_read_in_a_centred_column_at_every_width()
    {
        using var scene = new IslandScene(Text);
        scene.WaitFor("document.querySelector('li.carte .apercu') !== null");
        scene.Script("document.querySelector('li.carte .apercu').click()");
        scene.WaitFor("document.querySelector('.grand-apercu svg') !== null");

        // Du plus large au plus étroit : la colonne de lecture se tient au milieu, sans défilement de côté.
        foreach (var (width, theme) in new[] { (2400d, ThemeKind.Light), (1300d, ThemeKind.Dark), (480d, ThemeKind.Light) })
        {
            var before = scene.Script("document.documentElement.clientWidth");
            scene.Page.Width = width;
            scene.Test.Design.Theme = theme;
            TestShell.Flush();
            UiThread.Until(() => scene.Script("document.documentElement.clientWidth") != before, TimeSpan.FromSeconds(20));
            var sides = scene.Script("(() => { const v = document.querySelector('.vue').getBoundingClientRect(); return [Math.round(v.left), Math.round(document.documentElement.clientWidth - v.right)].join(' '); })()");
            var (left, right) = (int.Parse(sides.Trim('"').Split(' ')[0], CultureInfo.InvariantCulture), int.Parse(sides.Trim('"').Split(' ')[1], CultureInfo.InvariantCulture));
            Assert.IsLessThanOrEqualTo(1, Math.Abs(left - right), $"au milieu à {width} : {sides}");
            if (width > 1300)
            {
                Assert.IsGreaterThan(100, left, "une grande fenêtre laisse de la place des deux côtés");
            }
            Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), $"pas de défilement de côté à {width}");
            scene.WaitFor("document.getAnimations().length === 0");
            scene.Picture($"schemas-lecture-{width}-{theme}");
        }
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

    [EngineTest]
    public void Leaving_the_view_keeps_its_island_and_coming_back_rereads_the_list_at_once()
    {
        using var scene = new IslandScene(Text);
        scene.WaitFor("document.querySelectorAll('li.carte').length === 1");
        var model = (SchemasPageViewModel)scene.Page.DataContext;
        var island = scene.Page.Island!;

        // La vue part : l'îlot quitte l'affichage, mais reste ouvert avec sa vue-modèle.
        scene.Page.DataContext = null;
        TestShell.Flush();
        Assert.IsNull(scene.Page.Island);
        Assert.AreSame(island, model.Island);
        Assert.IsFalse(island.IsDisposed);

        // Pendant ce temps, un schéma arrive dans le coffre ; de retour, le même îlot, déjà prêt, relit la liste.
        scene.Schemas.Create(Text.Replace("Réseau", "Autre réseau", StringComparison.Ordinal)).Value.ToString();
        scene.Page.DataContext = model;
        TestShell.Flush();
        Assert.AreSame(island, scene.Page.Island);
        Assert.IsTrue(island.IsReady, "rien à recharger : la page est déjà là");
        scene.WaitFor("document.querySelectorAll('li.carte').length === 2");

        // La vue-modèle remplacée (un coffre verrouillé, un autre coffre) ferme son îlot ; une nouvelle en ouvre un autre.
        scene.Page.DataContext = null;
        model.Dispose();
        Assert.IsTrue(island.IsDisposed);
        Assert.IsNull(model.Island);
        Assert.ThrowsExactly<ObjectDisposedException>(() => model.TakeIsland());
    }

    [EngineTest]
    public void The_island_warms_up_hidden_and_the_view_then_shows_it_at_once()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        var schemas = new SchemaUseCases(vaults.App.Host);
        schemas.Create(Text).Value.ToString();
        var test = new TestShell();
        using var model = new SchemasPageViewModel(schemas, vaults.Picker, new FakeOpener(), FakeEnvironment.WebData);
        var hidden = new ContentControl { Width = 320, Height = 240, Visibility = Visibility.Hidden };
        var place = new Canvas();
        var window = new Window { Content = new Grid { Children = { place, hidden } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            SchemasPageViewModel? offered = null;
            var warmUp = new SchemaWarmUp(hidden, () => offered) { Delay = TimeSpan.FromMilliseconds(50) };
            warmUp.Warm();
            Assert.IsNull(hidden.Content, "sans module des schémas, rien à préparer");

            // Le coffre ouvert : après le délai, l'îlot démarre caché, puis quitte sa place, prêt.
            offered = model;
            warmUp.Schedule();
            warmUp.Schedule();
            UiThread.Until(() => model.Island is { IsReady: true } && hidden.Content is null, TimeSpan.FromMinutes(2));
            var island = model.Island!;
            warmUp.Warm();
            Assert.IsNull(hidden.Content, "déjà prêt : rien de plus");

            // La vue s'affiche : le même îlot, tout de suite.
            var page = new SchemasPage { DataContext = model, Width = 1300, Height = 860 };
            place.Children.Add(page);
            TestShell.Flush();
            Assert.AreSame(island, page.Island);
            Assert.IsTrue(island.IsReady);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void The_engine_keeps_off_the_frame_of_the_window_where_it_touches_it()
    {
        var window = new Size(1200, 800);
        Assert.AreEqual(default, ShellWindow.FrameInset(new Rect(220, 40, 900, 700), window), "loin du cadre : rien");
        Assert.AreEqual(new Thickness(0, 0, 6, 6), ShellWindow.FrameInset(new Rect(220, 40, 980, 760), window), "posé sur les bords droit et bas");
        Assert.AreEqual(new Thickness(6, 0, 4, 0), ShellWindow.FrameInset(new Rect(0, 40, 1198, 700), window), "menu replié : le bord gauche aussi");
    }

    [EngineTest]
    public void The_island_leaves_the_frame_to_the_window_only_while_it_resizes()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        var test = new TestShell();
        using var model = new SchemasPageViewModel(new SchemaUseCases(vaults.App.Host), vaults.Picker, new FakeOpener(), FakeEnvironment.WebData);
        var page = new SchemasPage { DataContext = model };
        // Une fenêtre qui dessine son cadre, comme celle de kyfRev : son bord de redimensionnement est dans ce qu'elle affiche.
        var window = new Window { Content = page, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        System.Windows.Shell.WindowChrome.SetWindowChrome(window, new System.Windows.Shell.WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = default });
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            var island = page.Island!;
            UiThread.Until(() => island.EngineMargin == new Thickness(6));
            window.ResizeMode = ResizeMode.NoResize;
            window.InvalidateMeasure();
            UiThread.Until(() => island.EngineMargin == default);

            // Sans cadre dessiné, c'est Windows qui tient le bord : rien à laisser.
            window.ResizeMode = ResizeMode.CanResize;
            System.Windows.Shell.WindowChrome.SetWindowChrome(window, null);
            window.InvalidateMeasure();
            UiThread.Until(() => island.EngineMargin == default);
            island.KeepOffTheFrame();
            Assert.AreEqual(default, island.EngineMargin);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Le pont seul, sur un vrai coffre.</summary>
    [TestMethod]
    public async Task The_bridge_prints_a_picture_through_the_window_of_the_app_and_only_a_picture()
    {
        using var scene = new BridgeScene();
        var png = Convert.FromBase64String(OnePixel);
        var asked = new List<(string Title, byte[] Picture)>();
        var outcomes = new Queue<string?>(["Envoyé à l'imprimante « Bureau ».", null]);
        var bridge = new SchemaBridge(new SchemaUseCases(scene.Vaults.App.Host), scene.Picker, () => null, _ => { }, print: (title, picture) =>
        {
            asked.Add((title, picture));
            return Task.FromResult(outcomes.Dequeue());
        });
        async Task<JsonObject> Print(params string[] args) =>
            JsonNode.Parse((await bridge.ReceiveAsync(JsonSerializer.Serialize(new { kind = "call", id = 4, method = "imprimerSchema", args })))!)!.AsObject();

        // La fenêtre s'ouvre sur l'image, sous son titre ; la réponse vient quand elle se ferme : le travail parti, ou rien.
        var sent = await Print("Réseau du lycée", Convert.ToBase64String(png));
        Assert.IsTrue(sent["ok"]!.GetValue<bool>());
        Assert.AreEqual("Envoyé à l'imprimante « Bureau ».", sent["value"]!.GetValue<string>());
        Assert.AreEqual("Réseau du lycée", asked.Single().Title);
        CollectionAssert.AreEqual(png, asked.Single().Picture);
        var closed = await Print("Réseau du lycée", Convert.ToBase64String(png));
        Assert.IsTrue(closed["ok"]!.GetValue<bool>());
        Assert.IsNull(closed["value"]);

        // Autre chose qu'une image PNG, pas de titre, ou du texte qui n'est pas du base64 : refusé, rien ne s'ouvre.
        foreach (var bad in new[] { new[] { "Réseau", Convert.ToBase64String("<svg/>"u8.ToArray()) }, ["Réseau", string.Empty], ["Réseau", "pas du base64 !"], [Convert.ToBase64String(png)] })
        {
            Assert.IsFalse((await Print(bad))["ok"]!.GetValue<bool>(), string.Join(" ", bad));
        }
        Assert.HasCount(2, asked);

        // Sans fenêtre pour imprimer, l'appel est refusé comme un appel inconnu.
        var refused = await scene.AnswerAsync(JsonSerializer.Serialize(new { kind = "call", id = 5, method = "imprimerSchema", args = new[] { "Réseau", Convert.ToBase64String(png) } }));
        Assert.IsFalse(refused["ok"]!.GetValue<bool>());
        Assert.AreEqual(SchemaText.BadCall, refused["error"]!.GetValue<string>());
    }

    [STATestMethod]
    public void The_page_opens_the_print_window_on_the_picture_and_says_what_became_of_it()
    {
        using var ui = new UiThread();
        using var scene = new BridgeScene();
        using var page = new SchemasPageViewModel(new SchemaUseCases(scene.Vaults.App.Host), scene.Picker, new FakeOpener(), FakeEnvironment.WebData, new OnePrinter());
        var png = Convert.FromBase64String(OnePixel);

        // Fermée sans imprimer : l'îlot n'a rien à dire.
        var closed = page.PrintPictureAsync("Réseau du lycée", png);
        Assert.IsTrue(page.Print.IsOpen);
        Assert.AreEqual("Réseau du lycée", page.Print.PictureTitle);
        Assert.AreEqual(1, page.Print.Picture!.PixelWidth);
        page.Print.Close();
        Assert.IsTrue(closed.IsCompletedSuccessfully);
        Assert.IsNull(closed.Result);

        // Une autre demande remplace celle d'une fenêtre restée ouverte, qui n'imprimera pas.
        var forgotten = page.PrintPictureAsync("Premier", png);
        var printed = page.PrintPictureAsync("Second", png);
        Assert.IsTrue(forgotten.IsCompletedSuccessfully);
        Assert.IsNull(forgotten.Result);
        UiThread.Until(() => !page.Print.IsLookingForPrinters);
        page.Print.Paginator = (_, _) => new System.Windows.Documents.FixedDocument().DocumentPaginator;
        Assert.IsTrue(page.Print.Print());
        Assert.AreEqual("Envoyé à l'imprimante « Bureau ».", printed.Result);
        Assert.IsFalse(page.Print.IsOpen);

        // Une image illisible : refusée comme un appel mal formé, et la fenêtre ne s'ouvre pas.
        var refusal = Assert.ThrowsExactly<ArgumentException>(() => page.PrintPictureAsync("Réseau", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3]));
        Assert.AreEqual(SchemaText.BadCall, refusal.Message);
        Assert.IsFalse(page.Print.IsOpen);
    }

    [STATestMethod]
    public void The_page_shows_the_print_window_of_the_app_over_the_schemas()
    {
        using var ui = new UiThread();
        using var scene = new BridgeScene();
        var printer = new OnePrinter();
        using var model = new SchemasPageViewModel(new SchemaUseCases(scene.Vaults.App.Host), scene.Picker, new FakeOpener(), FakeEnvironment.WebData, printer);
        var page = new SchemasPage { DataContext = model };
        var dialog = (KyfRev.DesignSystem.Controls.Dialog)page.FindName("PrintDialog");
        TestShell.Flush();
        Assert.AreSame(model.Print, dialog.DataContext);
        Assert.IsFalse(dialog.IsOpen);
        _ = model.PrintPictureAsync("Réseau du lycée", Convert.FromBase64String(OnePixel));
        TestShell.Flush();
        Assert.IsTrue(dialog.IsOpen);
        Assert.AreEqual("Imprimer « Réseau du lycée »", dialog.Title);
        Assert.IsInstanceOfType<PrintView>(dialog.Content);
        model.Print.Close();
        TestShell.Flush();
        Assert.IsFalse(dialog.IsOpen);

        // « Annuler » ferme sans imprimer ; « Imprimer » refusé par l'imprimante laisse la fenêtre ouverte, qui dit pourquoi ; accepté, il la ferme.
        var cancelled = model.PrintPictureAsync("Réseau du lycée", Convert.FromBase64String(OnePixel));
        TestShell.Flush();
        dialog.Close(KyfRev.DesignSystem.Controls.DialogChoice.None);
        TestShell.Flush();
        Assert.IsFalse(model.Print.IsOpen);
        Assert.IsNull(cancelled.Result);
        var printed = model.PrintPictureAsync("Réseau du lycée", Convert.FromBase64String(OnePixel));
        UiThread.Until(() => model.Print.CanPrint);
        printer.Refusal = "Plus de papier.";
        dialog.Close(KyfRev.DesignSystem.Controls.DialogChoice.Primary);
        TestShell.Flush();
        Assert.IsTrue(dialog.IsOpen);
        StringAssert.Contains(model.Print.Error, "Plus de papier.", StringComparison.Ordinal);
        Assert.IsFalse(printed.IsCompleted);
        printer.Refusal = null;
        dialog.Close(KyfRev.DesignSystem.Controls.DialogChoice.Primary);
        TestShell.Flush();
        Assert.IsFalse(dialog.IsOpen);
        Assert.AreEqual("Envoyé à l'imprimante « Bureau ».", printed.Result);
    }

    /// <summary>Une image PNG d'un point.</summary>
    private const string OnePixel = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAEBgIApD5fRAAAAABJRU5ErkJggg==";

    /// <summary>Une imprimante, qui accepte tout, sauf quand on lui donne de quoi refuser.</summary>
    private sealed class OnePrinter : IPrintService
    {
        public string? Refusal { get; set; }

        public IReadOnlyList<PrinterInfo> Printers() => [new("Bureau", true)];

        public string? Print(PrintJob job, System.Windows.Documents.DocumentPaginator pages) => Refusal;
    }

    private sealed class BridgeScene : IDisposable
    {
        public BridgeScene()
        {
            Vaults = new TestVaults(AppMode.Development);
            Vaults.App.Vaults.Create(Vaults.PathOf("Coffre"));
            Bridge = new SchemaBridge(new SchemaUseCases(Vaults.App.Host), Picker, () => PickedFile, Opened.Add, copyImage: Copied.Add);
        }

        public TestVaults Vaults { get; }

        public FakePicker Picker => Vaults.Picker;

        public SchemaBridge Bridge { get; }

        public string? PickedFile { get; set; }

        public List<string> Opened { get; } = [];

        public List<byte[]> Copied { get; } = [];

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

        public IslandScene(string? schema = null, bool waitsForTheIsland = true, ThemeKind theme = ThemeKind.Light, bool encrypted = false)
        {
            _vaults.App.Vaults.Create(_vaults.PathOf("Coffre"));
            Schemas = new SchemaUseCases(_vaults.App.Host);
            if (schema is not null)
            {
                Schemas.Create(schema).Value.ToString();
            }
            Test = new TestShell();
            Test.Design.Theme = theme;
            Page = new SchemasPage { DataContext = new SchemasPageViewModel(Schemas, _vaults.Picker, new FakeOpener(), FakeEnvironment.WebData) { CopyImage = Copied.Add, IsEncrypted = () => encrypted }, Width = 1300, Height = 860 };
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

        /// <summary>Les images que l'îlot a demandé de copier : le presse-papiers du poste n'est pas touché.</summary>
        public List<byte[]> Copied { get; } = [];

        /// <summary>Le sélecteur de dossier de l'essai.</summary>
        public FakePicker Picker => _vaults.Picker;

        /// <summary>
        /// L'image de l'îlot, telle que le moteur web la rend : écrite dans le dossier des captures s'il est donné. Le moteur a
        /// sa propre fenêtre, que l'image d'un élément WPF ne voit pas : on passe par celle qu'il donne quand un volet le recouvre.
        /// </summary>
        public void Picture(string name)
        {
            var covered = Page.Island!.SetCoveredAsync(true);
            UiThread.Until(() => covered.IsCompleted);
            TestShell.Flush();
            Page.Capture(name);
            var shown = Page.Island.SetCoveredAsync(false);
            UiThread.Until(() => shown.IsCompleted);
            TestShell.Flush();
        }

        /// <summary>Un dossier hors du coffre, où exporter.</summary>
        public string Outside(string name) => _vaults.PathOf(name);

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

        /// <summary>Un vrai geste de la souris, donné au moteur comme s'il venait de Windows, à ce point de la page.</summary>
        public void Mouse(string type, double x, double y) =>
            Dispatch(new { type, x, y, button = type == "mouseMoved" ? "none" : "left", clickCount = type == "mouseMoved" ? 0 : 1 });

        /// <summary>
        /// Un glissement, bouton gauche tenu : appuyer, cinq pas, relâcher (ou garder le bouton, pour voir le geste en cours).
        /// <paramref name="modifiers"/> : les touches tenues, comme le moteur les compte (1 : Alt).
        /// </summary>
        public void Drag(double fromX, double fromY, double toX, double toY, int modifiers = 0, bool release = true)
        {
            Mouse("mouseMoved", fromX, fromY);
            Dispatch(new { type = "mousePressed", x = fromX, y = fromY, button = "left", clickCount = 1, modifiers });
            for (var step = 1; step <= 5; step++)
            {
                Dispatch(new { type = "mouseMoved", x = fromX + ((toX - fromX) * step / 5), y = fromY + ((toY - fromY) * step / 5), button = "left", buttons = 1, modifiers });
            }
            if (release)
            {
                Dispatch(new { type = "mouseReleased", x = toX, y = toY, button = "left", clickCount = 1, modifiers });
            }
        }

        private void Dispatch(object parameters)
        {
            var run = Page.Island!.CallDevToolsAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(parameters));
            UiThread.Until(() => run.IsCompleted);
            TestShell.Flush();
        }

        /// <summary>Un clic de la souris : appuyer, relâcher, au même point.</summary>
        public void Click(double x, double y)
        {
            Mouse("mouseMoved", x, y);
            Mouse("mousePressed", x, y);
            Mouse("mouseReleased", x, y);
        }

        public void Dispose()
        {
            _window.Close();
            (Page.DataContext as IDisposable)?.Dispose();
            _vaults.Dispose();
            _ui.Dispose();
        }
    }
}
