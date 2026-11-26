using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Le volet d'un équipement en mode avancé (maquettes 30C et 30D) : l'en-tête, les quatre onglets, le type dans une grille
/// d'icônes, « ⋯ », les ports en tableau, plusieurs ports d'un coup, une plage, la configuration copiée, l'onglet Réseau ;
/// et le tiroir par le bas d'une fenêtre étroite.
/// </summary>
public sealed partial class SchemasPageTests
{
    /// <summary>
    /// Le réseau d'un lycée : un cœur en stack de deux membres (ports « 1/0/1 »), trois interfaces de routage, une gestion à
    /// part ; deux switchs d'étage, reliés au cœur par des trunks au VLAN natif 1 ; une borne Wi-Fi en accès.
    /// </summary>
    private static string Lycee()
    {
        static JsonObject Trunk() => new() { ["mode"] = "trunk", ["natif"] = 1, ["tagues"] = "10,20,30" };
        static JsonObject Access(int vlan) => new() { ["mode"] = "acces", ["acces"] = vlan };
        static JsonObject Group(string nom, string motif, int nombre, int premier, string genre, int vitesse) =>
            new() { ["nom"] = nom, ["motif"] = motif, ["nombre"] = nombre, ["premier"] = premier, ["genre"] = genre, ["vitesse"] = vitesse };
        static JsonObject Port(string id, string nom, int membre, int groupe, int numero, string genre, int vitesse, JsonObject? vlan = null, string? description = null, bool poe = false)
        {
            var port = new JsonObject
            {
                ["id"] = id,
                ["nom"] = nom,
                ["membre"] = membre,
                ["numero"] = numero,
                ["origine"] = string.Create(CultureInfo.InvariantCulture, $"{membre}:{groupe}:{numero}"),
                ["nomAuto"] = true,
                ["genre"] = genre,
                ["nature"] = "physique",
                ["vitesse"] = vitesse,
            };
            if (vlan is not null)
            {
                port["vlan"] = vlan;
            }
            if (description is not null)
            {
                port["description"] = description;
            }
            if (poe)
            {
                port["poe"] = new JsonObject { ["fournit"] = true };
            }
            return port;
        }
        static JsonObject Svi(int vlan, string adresse) => new()
        {
            ["id"] = string.Create(CultureInfo.InvariantCulture, $"i_v{vlan}"),
            ["nom"] = string.Create(CultureInfo.InvariantCulture, $"Vlan{vlan}"),
            ["genre"] = "virtuel",
            ["nature"] = "vlan",
            ["vlan"] = Access(vlan),
            ["adresses"] = new JsonArray(new JsonObject { ["adresse"] = adresse, ["prefixe"] = 24 }),
        };
        static JsonObject Floor(string id, string nom, string prefixe, double x, double y, bool configured)
        {
            var ports = new JsonArray();
            for (var p = 1; p <= 28; p++)
            {
                var vlan = p == 25 ? Trunk() : configured && p <= 12 ? Access(10) : configured && p <= 20 ? Access(20) : null;
                ports.Add(Port(string.Create(CultureInfo.InvariantCulture, $"{prefixe}{p}"), string.Create(CultureInfo.InvariantCulture, $"1/{p}"), 1, p <= 24 ? 0 : 1, p, p <= 24 ? "cuivre" : "fibre", p <= 24 ? 1000 : 10000, vlan, poe: configured && p <= 12));
            }
            return new JsonObject
            {
                ["id"] = id,
                ["type"] = "switch",
                ["nom"] = nom,
                ["position"] = new JsonObject { ["x"] = x, ["y"] = y },
                ["membres"] = new JsonArray(new JsonObject { ["numero"] = 1, ["groupes"] = new JsonArray(Group("Accès", "{m}/{p}", 24, 1, "cuivre", 1000), Group("Uplinks", "{m}/{p}", 4, 25, "fibre", 10000)) }),
                ["interfaces"] = ports,
            };
        }

        var coeur = new JsonArray();
        for (var m = 1; m <= 2; m++)
        {
            for (var p = 1; p <= 24; p++)
            {
                var (vlan, description) = (m, p) switch
                {
                    (1, 1) => (Trunk(), "Uplink SW-Étage1"),
                    (1, 2) => (Trunk(), "Uplink SW-Étage2"),
                    (1, 3) => (Access(20), "Borne hall"),
                    _ => ((JsonObject?)null, (string?)null),
                };
                coeur.Add(Port(string.Create(CultureInfo.InvariantCulture, $"i_c{m}_{p}"), string.Create(CultureInfo.InvariantCulture, $"{m}/0/{p}"), m, 0, p, "cuivre", 1000, vlan, description));
            }
        }
        coeur.Add(Svi(10, "10.0.10.1"));
        coeur.Add(Svi(20, "10.0.20.1"));
        coeur.Add(Svi(30, "10.0.30.1"));
        JsonObject Member(int numero) => new() { ["numero"] = numero, ["groupes"] = new JsonArray(Group("Accès", "{m}/0/{p}", 24, 1, "cuivre", 1000)) };
        JsonObject Link(string id, string a, string ia, string b, string ib) => new()
        {
            ["id"] = id,
            ["a"] = new JsonObject { ["equipement"] = a, ["interface"] = ia },
            ["b"] = new JsonObject { ["equipement"] = b, ["interface"] = ib },
            ["support"] = "cuivre",
            ["points"] = new JsonArray(),
        };
        JsonObject Vlan(int numero, string nom) => new() { ["id"] = string.Create(CultureInfo.InvariantCulture, $"v_{numero}"), ["numero"] = numero, ["nom"] = nom, ["sousReseaux"] = new JsonArray() };

        return new JsonObject
        {
            ["format"] = 2,
            ["titre"] = "Réseau du lycée",
            ["vue"] = "physique",
            ["vlans"] = new JsonArray(Vlan(10, "Élèves"), Vlan(20, "Wi-Fi"), Vlan(30, "Profs"), Vlan(99, "Gestion")),
            ["equipements"] = new JsonArray(
                new JsonObject
                {
                    ["id"] = "d_coeur",
                    ["type"] = "switch-coeur",
                    ["nom"] = "Cœur",
                    ["nomHote"] = "sw-coeur-01",
                    ["role"] = "Cœur du réseau, routage inter-VLAN",
                    ["reference"] = "C9300-48P",
                    ["position"] = new JsonObject { ["x"] = 0, ["y"] = 0 },
                    ["gestion"] = new JsonObject { ["adresse"] = "10.0.99.2/24", ["vlan"] = 99 },
                    ["proprietes"] = new JsonObject { ["routage"] = "OSPF" },
                    ["membres"] = new JsonArray(Member(1), Member(2)),
                    ["interfaces"] = coeur,
                },
                Floor("d_sw1", "SW-Étage1", "i_s", 320, 0, configured: true),
                Floor("d_sw2", "SW-Étage2", "i_t", 320, 220, configured: false),
                new JsonObject
                {
                    ["id"] = "d_ap",
                    ["type"] = "point-acces",
                    ["nom"] = "Wi-Fi Hall",
                    ["position"] = new JsonObject { ["x"] = 0, ["y"] = 220 },
                    ["interfaces"] = new JsonArray(new JsonObject { ["id"] = "i_ap0", ["nom"] = "eth0", ["numero"] = 0, ["genre"] = "cuivre", ["nature"] = "physique", ["vlan"] = Access(20) }),
                }),
            ["liens"] = new JsonArray(
                Link("l_1", "d_coeur", "i_c1_1", "d_sw1", "i_s25"),
                Link("l_2", "d_coeur", "i_c1_2", "d_sw2", "i_t25"),
                Link("l_3", "d_coeur", "i_c1_3", "d_ap", "i_ap0")),
        }.ToJsonString();
    }

    /// <summary>Le schéma du lycée ouvert dans l'éditeur, en mode avancé.</summary>
    private static string OpenInAdvancedMode(IslandScene scene)
    {
        scene.WaitFor("[...document.querySelectorAll('li.carte button')].some((b) => b.textContent.includes('Modifier'))");
        scene.Script("[...document.querySelectorAll('li.carte button')].find((b) => b.textContent.includes('Modifier')).click()");
        scene.WaitFor("document.querySelector('dialog[open] svg.canevas') !== null");
        scene.Script("document.querySelector('.segments.mode button[data-mode=\"avance\"]').click()");
        scene.WaitFor("document.querySelector('.segments.mode button[data-mode=\"avance\"]').getAttribute('aria-pressed') === 'true'");
        return scene.Schemas.Schemas().Value.Single().Id;
    }

    /// <summary>Un équipement choisi au clavier sur le plan : son volet s'ouvre.</summary>
    private static void SelectDevice(IslandScene scene, string id, string name)
    {
        scene.Script("(() => { const e = document.querySelector('svg.canevas g.equipement[data-id=\"" + id + "\"]'); e.focus(); e.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', bubbles: true, cancelable: true })); })()");
        scene.WaitFor("document.querySelector('.proprietes .tete .nom')?.textContent === " + JsonSerializer.Serialize(name));
    }

    private static string TextOf(IslandScene scene, string expression) => JsonSerializer.Deserialize<string>(scene.Script(expression)) ?? string.Empty;

    private static int Count(IslandScene scene, string selector) =>
        int.Parse(scene.Script($"document.querySelectorAll('{selector}').length"), CultureInfo.InvariantCulture);

    private static readonly string[] ChosenPorts = ["i_c1_4", "i_c1_5", "i_c1_6", "i_c1_8"];

    /// <summary>Les en-têtes de colonnes que l'on voit, séparés par « | ».</summary>
    private const string VisibleHeaders = "[...document.querySelectorAll('.ports .tableau thead th')].filter((t) => getComputedStyle(t).display !== 'none').map((t) => t.textContent.trim()).join('|')";

    /// <summary>Un clic sur un élément, avec les touches tenues (Maj, Ctrl) comme le moteur les donne.</summary>
    private static void ClickWith(IslandScene scene, string selector, bool shift = false, bool ctrl = false) =>
        scene.Script("document.querySelector('" + selector + "').dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, shiftKey: " + (shift ? "true" : "false") + ", ctrlKey: " + (ctrl ? "true" : "false") + " }))");

    private static void Fill(IslandScene scene, string selector, string value) =>
        scene.Script("(() => { const c = document.querySelector('" + selector + "'); c.value = " + JsonSerializer.Serialize(value) + "; c.dispatchEvent(new Event('input', { bubbles: true })); c.dispatchEvent(new Event('change', { bubbles: true })); })()");

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void The_device_pane_has_four_tabs_a_grid_of_types_and_its_actions_under_the_dots(ThemeKind theme)
    {
        using var scene = new IslandScene(Lycee(), theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        var id = OpenInAdvancedMode(scene);
        JsonNode Saved() => JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!;
        JsonNode Device(string device) => Saved()["equipements"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == device)!;

        // En tête : la vraie icône, le nom, le type en pastille, le statut ; puis quatre onglets et leur compte.
        SelectDevice(scene, "d_coeur", "Cœur");
        Assert.AreEqual(1, Count(scene, ".proprietes .tete svg.icone"));
        StringAssert.Contains(TextOf(scene, "document.querySelector('.proprietes .pastille-type').textContent"), "Switch de cœur", StringComparison.Ordinal);
        Assert.AreEqual("actif", TextOf(scene, "document.querySelector('.proprietes .statut select').value"));
        Assert.AreEqual("Général|Ports51|Réseau3|Inventaire", TextOf(scene, "[...document.querySelectorAll('.proprietes [role=tab]')].map((t) => t.textContent.trim()).join('|')"));
        Assert.AreEqual("general", TextOf(scene, "document.querySelector('.proprietes [role=tab][aria-selected=true]').dataset.onglet"));
        Assert.AreEqual("Propres à un switch de cœur", TextOf(scene, "document.querySelector('.proprietes .titre-section').textContent"));
        Assert.AreEqual(0, Count(scene, ".proprietes details"), "plus de sections repliables pour l'équipement");

        // La petite barre posée sur le plan ne garde que le nom et Connecter.
        Assert.AreEqual(1, Count(scene, ".barre-rapide .nom-rapide"));
        Assert.AreEqual("Connecter", TextOf(scene, "document.querySelector('.barre-rapide [data-action=connecter]').textContent.trim()"));
        Assert.AreEqual(2, Count(scene, ".barre-rapide > *"), "le nom et Connecter, rien d'autre");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-volet-general-{suffix}");

        // Les onglets au clavier : la flèche passe au suivant, qui prend la main ; le volet s'élargit sur les ports.
        scene.Script("document.querySelector('.proprietes [role=tab][aria-selected=true]').focus()");
        scene.Script("document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }))");
        scene.WaitFor("document.activeElement?.dataset.onglet === 'ports' && document.activeElement.getAttribute('aria-selected') === 'true'");
        Assert.AreEqual(0d, Device("d_coeur")["position"]!["x"]!.GetValue<double>(), "la flèche n'a pas déplacé l'équipement");
        scene.WaitFor("Math.round(document.querySelector('.volet').getBoundingClientRect().width) === 460");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-volet-ports-{suffix}");

        // Le dernier onglet reste choisi d'un équipement à l'autre.
        SelectDevice(scene, "d_sw1", "SW-Étage1");
        Assert.AreEqual("ports", TextOf(scene, "document.querySelector('.proprietes [role=tab][aria-selected=true]').dataset.onglet"));

        // Le type : une grille d'icônes, par famille, avec une recherche et les récents d'abord.
        SelectDevice(scene, "d_sw2", "SW-Étage2");
        scene.Script("document.querySelector('.proprietes [data-onglet=general][role=tab]').click()");
        scene.Script("document.querySelector('.proprietes .pastille-type').click()");
        scene.WaitFor("document.querySelector('.choix-type') !== null && document.activeElement?.matches('.choix-type input')");
        Assert.AreEqual("Récents", TextOf(scene, "document.querySelector('.choix-type h4').textContent"));
        StringAssert.Contains(TextOf(scene, "document.querySelector('.choix-type').textContent"), "Changer de type garde le nom, les ports et les connexions", StringComparison.Ordinal);
        Assert.IsGreaterThan(40, Count(scene, ".choix-type .tuile-type svg"), "chaque type montre sa vraie icône");
        Fill(scene, ".choix-type input", "switch");
        scene.WaitFor("!document.querySelector('.choix-type').textContent.includes('Récents')");
        Assert.AreEqual("Réseau et sécurité", TextOf(scene, "document.querySelector('.choix-type h4').textContent"));
        StringAssert.EndsWith(TextOf(scene, "document.querySelector('.choix-type .nombre').textContent"), "types", StringComparison.Ordinal);
        Assert.AreEqual("true", scene.Script("document.querySelector('.choix-type .tuile-type[data-type=switch]').getAttribute('aria-pressed') === 'true'"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-volet-type-{suffix}");
        scene.Script("document.querySelector('.choix-type .tuile-type[data-type=\"switch-distribution\"]').click()");
        UiThread.Until(() => Device("d_sw2")["type"]!.GetValue<string>() == "switch-distribution", TimeSpan.FromSeconds(20));
        Assert.AreEqual("SW-Étage2", Device("d_sw2")["nom"]!.GetValue<string>());
        Assert.AreEqual(28, Device("d_sw2")["interfaces"]!.AsArray().Count, "les ports restent");
        Assert.AreEqual(3, Saved()["liens"]!.AsArray().Count, "les connexions restent");
        scene.WaitFor("document.querySelector('.choix-type') === null && document.activeElement?.matches('.pastille-type')");

        // « ⋯ » : Connecter, Dupliquer, Verrouiller la place, puis la configuration, puis Retirer, à part.
        scene.Script("document.querySelector('.proprietes .tete .plus').click()");
        scene.WaitFor("document.querySelector('.proprietes .menu[role=menu]') !== null && document.activeElement?.getAttribute('role') === 'menuitem'");
        var items = TextOf(scene, "[...document.querySelectorAll('.proprietes .menu [role=menuitem]')].map((b) => b.textContent.replace(/\\s+/g, ' ').trim()).join('|')").Split('|');
        int At(string start) => Array.FindIndex(items, item => item.StartsWith(start, StringComparison.Ordinal));
        Assert.IsTrue(At("Connecter") == 0 && At("Dupliquer") > 0 && At("Verrouiller la place") > At("Dupliquer"), string.Join(" | ", items));
        Assert.IsTrue(At("Copier la configuration") > At("Verrouiller la place") && At("Coller sur") > At("Copier la configuration") && At("Retirer") == items.Length - 1, string.Join(" | ", items));
        Assert.AreEqual("true", scene.Script("document.querySelector('.proprietes .menu [data-action=coller-configuration]').disabled"), "rien de copié pour l'instant");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-volet-menu-{suffix}");
        scene.Script("document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }))");
        scene.WaitFor("document.querySelector('.proprietes .menu') === null && document.activeElement?.matches('.proprietes .tete .plus')");
        Assert.AreEqual(4, Count(scene, "svg.canevas g.equipement"), "Échap ferme le menu sans lâcher l'équipement");

        // Copier la configuration d'un switch d'étage, puis la coller sur l'autre : ports et VLAN.
        SelectDevice(scene, "d_sw1", "SW-Étage1");
        scene.Script("document.querySelector('.proprietes .tete .plus').click()");
        scene.WaitFor("document.querySelector('.proprietes .menu [data-action=copier-configuration]') !== null");
        scene.Script("document.querySelector('.proprietes .menu [data-action=copier-configuration]').click()");
        scene.WaitFor("document.querySelector('.annonce')?.textContent.includes('copiée') === true");
        SelectDevice(scene, "d_sw2", "SW-Étage2");
        scene.Script("document.querySelector('.proprietes .tete .plus').click()");
        scene.WaitFor("document.querySelector('.proprietes .menu [data-action=coller-configuration]:not(:disabled)') !== null");
        scene.Script("document.querySelector('.proprietes .menu [data-action=coller-configuration]').click()");
        scene.WaitFor("document.querySelector('dialog.confirmer[open] input[name=cible][value=d_sw2]')?.checked === true");
        Assert.AreEqual(0, Count(scene, "dialog.confirmer[open] input[name=cible][value=d_sw1]"), "pas sur lui-même");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-volet-coller-{suffix}");
        scene.Script("document.querySelector('dialog.confirmer[open] form').requestSubmit()");
        JsonNode Port(string name) => Device("d_sw2")["interfaces"]!.AsArray().Single(i => i!["nom"]!.GetValue<string>() == name)!;
        UiThread.Until(() => Port("1/5")["vlan"]?["acces"]?.GetValue<int>() == 10, TimeSpan.FromSeconds(20));
        Assert.AreEqual(20, Port("1/15")["vlan"]!["acces"]!.GetValue<int>());
        Assert.IsTrue(Port("1/3")["poe"]!["fournit"]!.GetValue<bool>());
        Assert.AreEqual("SW-Étage2", Device("d_sw2")["nom"]!.GetValue<string>(), "le nom ne suit pas");
        scene.WaitFor("document.querySelector('.annonce')?.textContent.includes('28 ports réglés') === true");

        // L'onglet Réseau reprend ce que le diagnostic dit de l'équipement ; ce qu'il dit des câbles reste au schéma.
        scene.Script("document.querySelector('.proprietes [data-onglet=reseau][role=tab]').click()");
        scene.WaitFor("document.querySelector('.reseau .conseil') !== null");
        Assert.AreEqual("Le VLAN 1 est le VLAN natif du trunk : mieux vaut un VLAN natif inutilisé (par ex. 999). Conseil, rien n'est bloqué.|« SW-Étage2 » : adresse de gestion non renseignée.", TextOf(scene, "[...document.querySelectorAll('.reseau .conseil')].map((c) => c.textContent.trim()).join('|')"));
        Assert.AreEqual(1, Count(scene, ".reseau [data-action=ajouter-svi]"), "devenu switch de distribution, il route entre VLAN");

        // « Retirer… » demande d'abord ; « Annuler » ne retire rien.
        scene.Script("document.querySelector('.proprietes .tete .plus').click()");
        scene.WaitFor("document.querySelector('.proprietes .menu [data-action=retirer]') !== null");
        scene.Script("document.querySelector('.proprietes .menu [data-action=retirer]').click()");
        scene.WaitFor("document.querySelector('dialog.confirmer[open]')?.textContent.includes('Retirer « SW-Étage2 »') === true");
        scene.Script("[...document.querySelectorAll('dialog.confirmer[open] button')].find((b) => b.textContent === 'Annuler').click()");
        scene.WaitFor("document.querySelector('dialog.confirmer[open]') === null");
        Assert.AreEqual(4, Count(scene, "svg.canevas g.equipement"));
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Pas de défilement horizontal.");
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void The_ports_tab_sets_several_ports_at_once_adds_a_range_and_the_network_tab_advises(ThemeKind theme)
    {
        using var scene = new IslandScene(Lycee(), theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        var id = OpenInAdvancedMode(scene);
        JsonNode Coeur() => JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!["equipements"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == "d_coeur")!;
        JsonNode Port(string portId) => Coeur()["interfaces"]!.AsArray().Single(i => i!["id"]!.GetValue<string>() == portId)!;

        SelectDevice(scene, "d_coeur", "Cœur");
        scene.Script("document.querySelector('.proprietes [data-onglet=ports][role=tab]').click()");
        scene.WaitFor("document.querySelector('.ports .facade') !== null");

        // Les membres du stack, et ce que le membre regardé a de libre.
        Assert.AreEqual("Membre 1|Membre 2|Autres", TextOf(scene, "[...document.querySelectorAll('.ports .membres button')].map((b) => b.textContent.trim()).join('|')"));
        Assert.AreEqual("21 libres · 2 trunks", TextOf(scene, "document.querySelector('.ports .resume').textContent.trim()"));
        Assert.AreEqual(24, Count(scene, ".ports .facade [role=option]"));
        Assert.AreEqual(3, Count(scene, ".ports .facade .carre.relie"), "un carré plein par port relié");

        // Le tableau : à 460 pixels, la vitesse cède sa place ; un trunk se lit « U 1 · T 10,20,30 ».
        Assert.AreEqual("|Port|Nom|Mode|VLAN|Relié à", TextOf(scene, VisibleHeaders));
        Assert.AreEqual("1/0/1|Uplink SW-Étage1|Trunk|U 1 · T 10,20,30|SW-Étage1 · 1/25", TextOf(scene, "[...document.querySelector('.ports .tableau tbody tr').cells].filter((c) => getComputedStyle(c).display !== 'none').slice(1).map((c) => c.textContent.trim()).join('|')"));

        // La façade : un clic choisit, Maj+clic une suite, Ctrl+clic en ajoute un.
        ClickWith(scene, ".ports .facade [data-port=i_c1_4]");
        ClickWith(scene, ".ports .facade [data-port=i_c1_6]", shift: true);
        ClickWith(scene, ".ports .facade [data-port=i_c1_8]", ctrl: true);
        scene.WaitFor("document.querySelectorAll('.ports .facade [aria-selected=true]').length === 4");
        Assert.AreEqual(4, Count(scene, ".ports .tableau tbody tr.choisi"));
        StringAssert.StartsWith(TextOf(scene, "document.querySelector('.ports .lot').textContent.trim()"), "4 ports choisis", StringComparison.Ordinal);

        // « Accès VLAN… » : un VLAN pour les quatre.
        scene.Script("document.querySelector('.ports .lot [data-action=acces]').click()");
        scene.WaitFor("document.activeElement?.matches('.ports .reglage input') === true");
        Fill(scene, ".ports .reglage input", "10");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-ports-choisis-{suffix}");
        scene.Script("document.querySelector('.ports .reglage').requestSubmit()");
        UiThread.Until(() => ChosenPorts.All(p => Port(p)["vlan"]?["acces"]?.GetValue<int>() == 10), TimeSpan.FromSeconds(20));

        // PoE, puis Désactiver, sur les mêmes ports.
        scene.Script("document.querySelector('.ports .lot [data-action=poe]').click()");
        UiThread.Until(() => Port("i_c1_5")["poe"]?["fournit"]?.GetValue<bool>() == true, TimeSpan.FromSeconds(20));
        scene.Script("document.querySelector('.ports .lot [data-action=activation]').click()");
        UiThread.Until(() => Port("i_c1_8")["admin"]?.GetValue<string>() == "desactive", TimeSpan.FromSeconds(20));
        Assert.AreEqual("actif", Port("i_c1_7")["admin"]!.GetValue<string>(), "le port 7 n'était pas choisi");

        // « Trunk… » sur un seul port, choisi par sa case : VLAN natif inutilisé et VLAN tagués.
        scene.Script("document.querySelector('.ports .lot button[aria-label=\"Ne plus choisir ces ports\"]').click()");
        ClickWith(scene, ".ports .tableau tr[data-port=i_c1_9] input[type=checkbox]");
        scene.WaitFor("document.querySelectorAll('.ports .tableau tbody tr.choisi').length === 1");
        scene.Script("document.querySelector('.ports .lot [data-action=trunk]').click()");
        scene.WaitFor("document.querySelectorAll('.ports .reglage input').length === 2");
        Fill(scene, ".ports .reglage input", "999");
        Fill(scene, ".ports .reglage label.long input", "10, 20");
        scene.Script("document.querySelector('.ports .reglage').requestSubmit()");
        UiThread.Until(() => Port("i_c1_9")["vlan"]?["mode"]?.GetValue<string>() == "trunk", TimeSpan.FromSeconds(20));
        Assert.AreEqual("10,20", Port("i_c1_9")["vlan"]!["tagues"]!.GetValue<string>());
        Assert.AreEqual(999, Port("i_c1_9")["vlan"]!["natif"]!.GetValue<int>());

        // Une ligne touchée ouvre le détail du port.
        scene.Script("document.querySelector('.ports .tableau tr[data-port=i_c1_1] .nom-port').click()");
        scene.WaitFor("document.querySelector('.ports .interface legend')?.textContent === 'Port « 1/0/1 »'");

        // Une plage sur le membre 2 : « 2/0/25 à 2/0/48 », d'un coup, au nommage des autres ports.
        scene.Script("[...document.querySelectorAll('.ports .membres button')].find((b) => b.textContent.trim() === 'Membre 2').click()");
        scene.WaitFor("document.querySelector('.ports [data-action=plage]')?.textContent.trim() === 'Ajouter des ports : 2/0/25 à 2/0/48…'");
        scene.Script("document.querySelector('.ports [data-action=plage]').click()");
        scene.WaitFor("document.querySelector('.ports .reglage.plage .apercu')?.textContent.trim() === '24 ports : 2/0/25 à 2/0/48'");
        scene.Script("document.querySelector('.ports .reglage.plage').requestSubmit()");
        UiThread.Until(() => Coeur()["interfaces"]!.AsArray().Count == 75, TimeSpan.FromSeconds(20));
        Assert.IsTrue(Coeur()["interfaces"]!.AsArray().Any(i => i!["nom"]!.GetValue<string>() == "2/0/48" && i["membre"]!.GetValue<int>() == 2));
        scene.WaitFor("document.querySelectorAll('.ports .facade [role=option]').length === 48");
        Assert.AreEqual("Ports75", TextOf(scene, "document.querySelector('.proprietes [data-onglet=ports][role=tab]').textContent.trim()"));

        // L'onglet Réseau : la gestion à part, les interfaces de routage par VLAN, et un conseil, sans rien bloquer.
        scene.Script("document.querySelector('.proprietes [data-onglet=reseau][role=tab]').click()");
        scene.WaitFor("document.querySelector('.reseau') !== null");
        Assert.IsLessThan(460, double.Parse(scene.Script("document.querySelector('.volet').getBoundingClientRect().width"), CultureInfo.InvariantCulture), "hors des ports, le volet reprend sa largeur");
        Assert.AreEqual("10.0.99.2/24|99", TextOf(scene, "[...document.querySelectorAll('.reseau .champs')[0].querySelectorAll('input')].map((c) => c.value).join('|')"));
        Assert.AreEqual("Élèves|Wi-Fi|Profs", TextOf(scene, "[...document.querySelectorAll('.reseau .tableau .col-nom')].map((c) => c.textContent.trim()).join('|')"));
        Assert.AreEqual("10.0.10.1/24", TextOf(scene, "document.querySelector('.reseau .tableau tbody tr input[aria-label^=Adresse]').value"));
        Assert.AreEqual(
            "Le VLAN 1 est le VLAN natif des deux trunks : mieux vaut un VLAN natif inutilisé (par ex. 999). Conseil, rien n'est bloqué.",
            TextOf(scene, "document.querySelector('.reseau .conseil').textContent.trim()"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-reseau-{suffix}");
        scene.Script("document.querySelector('.reseau [data-action=ajouter-svi]').click()");
        UiThread.Until(() => Coeur()["interfaces"]!.AsArray().Any(i => i!["nom"]!.GetValue<string>() == "Vlan99"), TimeSpan.FromSeconds(20));
        scene.WaitFor("document.querySelectorAll('.reseau .tableau tbody tr').length === 4");

        // « Relié à » : le câble se choisit depuis le tableau, et son volet ne change pas.
        scene.Script("document.querySelector('.proprietes [data-onglet=ports][role=tab]').click()");
        scene.Script("[...document.querySelectorAll('.ports .membres button')].find((b) => b.textContent.trim() === 'Membre 1').click()");
        scene.WaitFor("document.querySelector('.ports .tableau tr[data-port=i_c1_1] .col-relie button') !== null");
        scene.Script("document.querySelector('.ports .tableau tr[data-port=i_c1_1] .col-relie button').click()");
        scene.WaitFor("document.querySelector('.proprietes .onglets') === null && document.querySelector('.panneau').textContent.includes('Extrémités')");
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Pas de défilement horizontal.");
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void At_470_pixels_the_device_pane_is_a_drawer_with_its_four_tabs_and_a_shorter_table(ThemeKind theme)
    {
        using var scene = new IslandScene(Lycee(), theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        OpenInAdvancedMode(scene);
        Narrow(scene);

        // Le volet s'ouvre par « Propriétés » : un tiroir par le bas, toute la largeur, le plan visible au-dessus.
        SelectDevice(scene, "d_coeur", "Cœur");
        scene.Script("document.querySelector('.bouton-volet').click()");
        scene.WaitFor("document.querySelector('.volet.ouvert') !== null");
        var box = TextOf(scene, "(() => { const v = document.querySelector('.volet').getBoundingClientRect(); const c = document.querySelector('.corps').getBoundingClientRect(); return [Math.round(v.left), Math.round(document.documentElement.clientWidth - v.right), Math.round(v.top - c.top), Math.round(c.bottom - v.bottom)].join(' '); })()")
            .Split(' ').Select(n => int.Parse(n, CultureInfo.InvariantCulture)).ToArray();
        Assert.AreEqual(0, box[0], "collé à gauche");
        Assert.AreEqual(0, box[1], "collé à droite");
        Assert.IsGreaterThan(80, box[2], "le plan reste visible au-dessus");
        Assert.AreEqual(0, box[3], "posé en bas");
        Assert.AreEqual(4, Count(scene, ".proprietes [role=tab]"));
        Assert.AreEqual("true", scene.Script("(() => { const o = document.querySelector('.proprietes .onglets'); return o.scrollWidth <= o.clientWidth + 1; })()"), "les quatre onglets tiennent");
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Pas de défilement horizontal.");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-volet-470-general-{suffix}");

        // Les ports : la façade, et un tableau réduit à port, mode, VLAN et relié à.
        scene.Script("document.querySelector('.proprietes [data-onglet=ports][role=tab]').click()");
        scene.WaitFor("document.querySelector('.ports .facade') !== null");
        Assert.AreEqual("Port|Mode|VLAN|Relié à", TextOf(scene, VisibleHeaders));
        ClickWith(scene, ".ports .facade [data-port=i_c1_4]");
        ClickWith(scene, ".ports .facade [data-port=i_c1_6]", shift: true);
        scene.WaitFor("document.querySelectorAll('.ports .facade [aria-selected=true]').length === 3");
        StringAssert.StartsWith(TextOf(scene, "document.querySelector('.ports .lot b').innerText.trim()"), "3 choisis", StringComparison.Ordinal);
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Pas de défilement horizontal.");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-volet-470-ports-{suffix}");

        // Le Réseau aussi tient dans le tiroir.
        scene.Script("document.querySelector('.proprietes [data-onglet=reseau][role=tab]').click()");
        scene.WaitFor("document.querySelector('.reseau .conseil') !== null");
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Pas de défilement horizontal.");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-volet-470-reseau-{suffix}");
        scene.Script("document.querySelector('.proprietes [data-onglet=ports][role=tab]').click()");
        scene.WaitFor("document.querySelector('.ports .facade') !== null");
        ClickWith(scene, ".ports .facade [data-port=i_c1_4]");
        scene.WaitFor("document.querySelectorAll('.ports .facade [aria-selected=true]').length === 1");

        // La façade au clavier : Échap lâche les ports, l'équipement reste choisi.
        scene.Script("document.querySelector('.ports .facade [data-port=i_c1_4]').focus()");
        scene.Script("document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }))");
        scene.WaitFor("document.querySelectorAll('.ports .facade [aria-selected=true]').length === 0");
        Assert.AreEqual(1, Count(scene, "svg.canevas g.equipement.choisi"));

        // La poignée : Entrée agrandit le tiroir presque jusqu'en haut, et le rabaisse.
        var before = double.Parse(scene.Script("document.querySelector('.volet').getBoundingClientRect().height"), CultureInfo.InvariantCulture);
        scene.Script("document.querySelector('.poignee-tiroir').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }))");
        scene.WaitFor("document.querySelector('.poignee-tiroir').getAttribute('aria-expanded') === 'true'");
        Assert.IsGreaterThan(before + 50, double.Parse(scene.Script("document.querySelector('.volet').getBoundingClientRect().height"), CultureInfo.InvariantCulture));
        scene.Script("document.querySelector('.poignee-tiroir').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }))");
        scene.WaitFor("document.querySelector('.poignee-tiroir').getAttribute('aria-expanded') === 'false'");
    }

    [EngineTest]
    public void The_ports_tab_widens_the_pane_only_where_the_plan_keeps_its_room()
    {
        using var scene = new IslandScene(Lycee());
        OpenInAdvancedMode(scene);
        SelectDevice(scene, "d_coeur", "Cœur");
        scene.Script("document.querySelector('.proprietes [data-onglet=ports][role=tab]').click()");
        scene.WaitFor("document.querySelector('.ports .facade') !== null");

        // Très large : 460 pixels sur les ports, le plan prend le reste ; moyenne : 340, le tableau défile de côté ;
        // étroite : le volet s'ouvre par-dessus le plan, à 460 s'il tient.
        foreach (var (width, theme, expected) in new[] { (2560d, ThemeKind.Light, 460), (940d, ThemeKind.Dark, 340), (760d, ThemeKind.Light, 460) })
        {
            var before = scene.Script("document.documentElement.clientWidth");
            scene.Page.Width = width;
            scene.Test.Design.Theme = theme;
            TestShell.Flush();
            UiThread.Until(() => scene.Script("document.documentElement.clientWidth") != before, TimeSpan.FromSeconds(20));
            if (width < 860)
            {
                scene.WaitFor("document.querySelector('.bouton-volet') !== null && getComputedStyle(document.querySelector('.bouton-volet')).display !== 'none'");
                scene.Script("if (!document.querySelector('.volet.ouvert')) document.querySelector('.bouton-volet').click()");
                scene.WaitFor("document.querySelector('.volet.ouvert') !== null");
            }
            scene.WaitFor(string.Create(CultureInfo.InvariantCulture, $"Math.round(document.querySelector('.volet').getBoundingClientRect().width) === {expected}"));
            Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), $"pas de défilement de côté à {width}");
            if (expected == 340)
            {
                Assert.AreEqual("true", scene.Script("(() => { const d = document.querySelector('.ports .defile'); return d.scrollWidth > d.clientWidth; })()"), "le tableau défile de côté");
            }
            scene.WaitFor("document.getAnimations().length === 0");
            scene.Picture(string.Create(CultureInfo.InvariantCulture, $"schemas-volet-ports-{width}-{theme}"));
        }
    }
}
