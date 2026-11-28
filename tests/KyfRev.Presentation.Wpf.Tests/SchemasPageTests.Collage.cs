using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La configuration copiée d'un équipement, collée sur un autre comme l'utilisateur le fait, à la souris et au clavier :
/// ports de même nom, ports aux noms différents trouvés par leur place, rien en commun ; et le clic droit sur un
/// équipement, qui ouvre les mêmes actions que « ⋯ ».
/// </summary>
public sealed partial class SchemasPageTests
{
    /// <summary>Le lycée, et un troisième switch d'étage du même modèle dont les ports s'appellent « Gi1/0/1 ».</summary>
    private static string LyceeEtTroisiemeEtage()
    {
        var schema = JsonNode.Parse(LyceeEnVolet())!;
        var equipements = schema["equipements"]!.AsArray();
        var troisieme = equipements.Single(e => e!["id"]!.GetValue<string>() == "d_sw2")!.DeepClone();
        troisieme["id"] = "d_sw3";
        troisieme["nom"] = "SW-Étage3";
        troisieme["position"] = new JsonObject { ["x"] = 640, ["y"] = 0 };
        foreach (var groupe in troisieme["membres"]![0]!["groupes"]!.AsArray())
        {
            groupe!["motif"] = "Gi{m}/0/{p}";
        }
        foreach (var port in troisieme["interfaces"]!.AsArray())
        {
            var numero = port!["numero"]!.GetValue<int>().ToString(CultureInfo.InvariantCulture);
            port["id"] = "i_u" + numero;
            port["nom"] = "Gi1/0/" + numero;
            port.AsObject().Remove("vlan");
        }
        equipements.Add(troisieme);
        return schema.ToJsonString();
    }

    /// <summary>
    /// Un point de l'élément où lui seul est sous le pointeur. Une animation ou un avis qui s'en va peut le couvrir un
    /// instant (la CI est plus lente que le poste) : on attend qu'il soit libre, dix secondes au plus.
    /// </summary>
    private static double[] GripOf(IslandScene scene, string selector)
    {
        double[]? point = null;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while ((point = TryGripOf(scene, selector)) is null && DateTime.UtcNow < deadline)
        {
            TestShell.Flush();
        }
        // Couvert jusqu'au bout : l'échec dit ce qui le couvre, pour qu'on le voie sur la CI.
        return point ?? throw new AssertFailedException($"aucun point de « {selector} » sous le pointeur ; au centre : {TextOf(scene, "(() => { const e = document.querySelector('" + selector + "'); if (!e) return 'absent'; const r = e.getBoundingClientRect(); const s = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2); const nom = (n) => n ? n.tagName.toLowerCase() + (n.id ? '#' + n.id : '') + (typeof n.className === 'string' && n.className ? '.' + n.className.trim().replaceAll(' ', '.') : '') : 'rien'; return 'rect ' + [r.x, r.y, r.width, r.height].map(Math.round).join(',') + ' ; ' + nom(s) + ' < ' + nom(s?.parentElement) + ' < ' + nom(s?.parentElement?.parentElement) + ' ; fenêtre ' + innerWidth + 'x' + innerHeight + ' ; dialogue ' + (document.querySelector('dialog[open]')?.className ?? 'aucun') + ' ; volet ' + Math.round(document.querySelector('.volet, .proprietes')?.getBoundingClientRect().width ?? 0); })()")}");
    }

    private static double[]? TryGripOf(IslandScene scene, string selector) =>
        JsonSerializer.Deserialize<double[]?>(TextOf(scene,
            "(() => { const e = document.querySelector('" + selector + "'); if (!e) return JSON.stringify(null); const r = e.getBoundingClientRect(); for (const fy of [0.5, 0.35, 0.65]) for (const fx of [0.5, 0.35, 0.65, 0.2, 0.8]) { const x = r.x + r.width * fx, y = r.y + r.height * fy; const s = document.elementFromPoint(x, y); if (s && (s === e || e.contains(s))) return JSON.stringify([x, y]); } return JSON.stringify(null); })()"));

    /// <summary>Un geste de la souris donné au moteur comme s'il venait de Windows (modificateurs : 2 Ctrl, 8 Maj).</summary>
    private static void MouseEvent(IslandScene scene, string type, double x, double y, string button, int modifiers = 0)
    {
        var run = scene.Page.Island!.CallDevToolsAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new
        {
            type,
            x,
            y,
            button = type == "mouseMoved" ? "none" : button,
            buttons = type == "mousePressed" ? (button == "right" ? 2 : 1) : 0,
            clickCount = type == "mouseMoved" ? 0 : 1,
            modifiers,
        }));
        UiThread.Until(() => run.IsCompleted);
        TestShell.Flush();
    }

    /// <summary>Un vrai clic de la souris sur un élément de la page (bouton gauche, ou droit).</summary>
    private static void Press(IslandScene scene, string selector, string button = "left", int modifiers = 0)
    {
        var point = GripOf(scene, selector);
        MouseEvent(scene, "mouseMoved", point[0], point[1], button);
        MouseEvent(scene, "mousePressed", point[0], point[1], button, modifiers);
        MouseEvent(scene, "mouseReleased", point[0], point[1], button, modifiers);
    }

    /// <summary>Une vraie touche du clavier, donnée au moteur comme si elle venait de Windows.</summary>
    private static void Key(IslandScene scene, string key, string code, int virtualKey, int modifiers = 0)
    {
        // Entrée porte son caractère : c'est lui qui active le bouton qui a la main.
        var text = key == "Enter" ? "\r" : string.Empty;
        foreach (var type in new[] { text.Length > 0 ? "keyDown" : "rawKeyDown", "keyUp" })
        {
            var run = scene.Page.Island!.CallDevToolsAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(new { type, key, code, text = type == "keyUp" ? string.Empty : text, windowsVirtualKeyCode = virtualKey, nativeVirtualKeyCode = virtualKey, modifiers }));
            UiThread.Until(() => run.IsCompleted);
            TestShell.Flush();
        }
    }

    private static string Device(string id) => "svg.canevas g.equipement[data-id=\"" + id + "\"]";

    /// <summary>Un équipement choisi d'un clic sur le plan : son volet s'ouvre.</summary>
    private static void PressDevice(IslandScene scene, string id, string name)
    {
        // Le plan ne se recadre qu'à sa première mesure : le volet, ouvert ensuite à côté, peut cacher la droite du
        // schéma (vu sur la CI). « 0 » (« Tout voir ») le recadre dans la place qui reste.
        if (TryGripOf(scene, Device(id) + " rect.pave") is null)
        {
            scene.Script("(() => { const plan = document.querySelector('svg.canevas'); plan.focus(); plan.dispatchEvent(new KeyboardEvent('keydown', { key: '0', bubbles: true, cancelable: true })); })()");
            TestShell.Flush();
        }
        Press(scene, Device(id) + " rect.pave");
        scene.WaitFor("document.querySelector('.proprietes .tete .nom')?.textContent === " + JsonSerializer.Serialize(name));
    }

    /// <summary>Le plan se cadre sur le schéma à sa première image, que cette fenêtre hors de l'écran ne dessine qu'au premier mouvement.</summary>
    private static void FramePlan(IslandScene scene)
    {
        scene.Mouse("mouseMoved", 4, 4);
        scene.WaitFor("document.querySelector('svg.canevas').getAttribute('viewBox') !== '-400 -300 800 600'");
    }

    private const string Announce = "document.querySelector('.annonce')?.textContent ?? ''";

    /// <summary>Attend que l'élément qui a la main réponde à <paramref name="selector"/> ; sinon, dit lequel l'a.</summary>
    private static void WaitForFocus(IslandScene scene, string selector)
    {
        try
        {
            scene.WaitFor("document.activeElement?.matches('" + selector + "') === true");
        }
        catch (AssertFailedException)
        {
            throw new AssertFailedException($"« {selector} » n'a pas la main : {TextOf(scene, "(document.activeElement?.outerHTML ?? 'rien').slice(0, 160)")}");
        }
    }

    /// <summary>Les entrées d'un menu ouvert, séparées par « | ».</summary>
    private static string Entries(IslandScene scene, string menu) =>
        TextOf(scene, "[...document.querySelectorAll('" + menu + " [role=menuitem]')].map((b) => b.querySelector('.nom-entree').textContent.trim()).join('|')");

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void A_configuration_is_pasted_by_name_or_by_place_says_what_it_did_and_is_undone_at_once(ThemeKind theme)
    {
        using var scene = new IslandScene(LyceeEtTroisiemeEtage(), theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        var id = OpenInAdvancedMode(scene);
        JsonNode Saved(string device) => JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!["equipements"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == device)!;
        JsonNode Port(string device, string name) => Saved(device)["interfaces"]!.AsArray().Single(i => i!["nom"]!.GetValue<string>() == name)!;
        int? Access(string device, string name) => Port(device, name)["vlan"]?["acces"]?.GetValue<int>();
        void OpenPaste(string device, string name)
        {
            PressDevice(scene, device, name);
            Press(scene, ".proprietes .tete .plus");
            scene.WaitFor("document.querySelector('.proprietes .menu [data-action=coller-configuration]:not(:disabled)') !== null");
            Press(scene, ".proprietes .menu [data-action=coller-configuration]");
            scene.WaitFor("document.querySelector('dialog.confirmer[open] input[name=cible][value=" + device + "]')?.checked === true");
        }
        string Detail(string device) => TextOf(scene, "document.querySelector('dialog.confirmer[open] input[value=" + device + "]').closest('label').querySelector('.detail-cible').textContent");
        FramePlan(scene);

        // Copier, à la souris, depuis « ⋯ ».
        PressDevice(scene, "d_sw1", "SW-Étage1");
        Press(scene, ".proprietes .tete .plus");
        scene.WaitFor("document.querySelector('.proprietes .menu [data-action=copier-configuration]') !== null");
        Press(scene, ".proprietes .menu [data-action=copier-configuration]");
        scene.WaitFor("document.querySelector('.annonce')?.textContent.includes('copiée') === true");
        WaitForFocus(scene, ".proprietes .tete .plus");

        // Depuis le switch copié lui-même : rien de coché, « Coller » attend un équipement.
        Press(scene, ".proprietes .tete .plus");
        scene.WaitFor("document.querySelector('.proprietes .menu [data-action=coller-configuration]:not(:disabled)') !== null");
        Press(scene, ".proprietes .menu [data-action=coller-configuration]");
        scene.WaitFor("document.querySelector('dialog.confirmer[open] #titre-coller') !== null");
        Assert.AreEqual(0, Count(scene, "dialog.confirmer[open] input[name=cible]:checked"));
        Assert.AreEqual("true", scene.Script("document.querySelector('dialog.confirmer[open] button[type=submit]').disabled"), "rien à coller tant que rien n'est coché");
        Press(scene, "dialog.confirmer[open] input[name=cible][value=d_ap]");
        Assert.AreEqual("Point d'accès Wi-Fi · aucun port en commun", Detail("d_ap"));
        Press(scene, "dialog.confirmer[open] .boutons-dialogue .bouton:not(.principal)");
        scene.WaitFor("document.querySelector('dialog.confirmer[open]') === null");

        // Ports de même nom : « 1/5 » reprend « 1/5 », et l'annonce le dit.
        OpenPaste("d_sw2", "SW-Étage2");
        Assert.AreEqual("Switch administrable · 28 ports de même nom", Detail("d_sw2"));
        Assert.AreEqual(0, Count(scene, "dialog.confirmer[open] fieldset.par"), "rien à proposer : les noms se correspondent");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-coller-nom-{suffix}");
        Press(scene, "dialog.confirmer[open] button[type=submit]");
        UiThread.Until(() => Access("d_sw2", "1/5") == 10, TimeSpan.FromSeconds(20));
        Assert.AreEqual(20, Access("d_sw2", "1/15"));
        Assert.AreEqual("Configuration de « SW-Étage1 » collée sur « SW-Étage2 » : 28 ports repris. Ctrl+Z annule.", TextOf(scene, Announce));
        WaitForFocus(scene, ".proprietes .tete .plus");

        // Ctrl+Z annule le collage en une fois : tous les ports reviennent.
        Key(scene, "z", "KeyZ", 0x5A, modifiers: 2);
        UiThread.Until(() => Access("d_sw2", "1/5") is null, TimeSpan.FromSeconds(20));
        Assert.IsNull(Access("d_sw2", "1/15"));
        Assert.IsNull(Port("d_sw2", "1/3")["poe"], "le PoE aussi");
        Key(scene, "y", "KeyY", 0x59, modifiers: 2);
        UiThread.Until(() => Access("d_sw2", "1/5") == 10, TimeSpan.FromSeconds(20));

        // Des noms différents (« 1/1 », « Gi1/0/1 ») : la place des ports est proposée, avec un exemple.
        OpenPaste("d_sw3", "SW-Étage3");
        Assert.AreEqual("Switch administrable · 28 ports, par leur place", Detail("d_sw3"));
        StringAssert.Contains(TextOf(scene, "document.querySelector('dialog.confirmer[open] fieldset.par').textContent.replaceAll(String.fromCharCode(160), ' ')"), "Par leur place : même membre, même numéro (« 1/1 » sur « Gi1/0/1 »)", StringComparison.Ordinal);
        Assert.AreEqual("true", scene.Script("document.querySelector('dialog.confirmer[open] input[name=par][value=place]').checked"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-coller-place-{suffix}");

        // Par le nom seulement : rien en commun, rien ne change, et l'annonce le dit.
        Press(scene, "dialog.confirmer[open] input[name=par][value=nom]");
        Assert.AreEqual("Switch administrable · aucun port en commun", Detail("d_sw3"));
        Press(scene, "dialog.confirmer[open] button[type=submit]");
        scene.WaitFor("document.querySelector('dialog.confirmer[open]') === null");
        Assert.AreEqual("Aucun port de même nom sur « SW-Étage3 » : rien n'a changé.", TextOf(scene, Announce));
        Assert.IsNull(Access("d_sw3", "Gi1/0/5"));

        // Par leur place : les 28 ports.
        Press(scene, ".proprietes .tete .plus");
        scene.WaitFor("document.querySelector('.proprietes .menu [data-action=coller-configuration]:not(:disabled)') !== null");
        Press(scene, ".proprietes .menu [data-action=coller-configuration]");
        scene.WaitFor("document.querySelector('dialog.confirmer[open] input[name=par][value=place]')?.checked === true");
        Press(scene, "dialog.confirmer[open] button[type=submit]");
        UiThread.Until(() => Access("d_sw3", "Gi1/0/5") == 10, TimeSpan.FromSeconds(20));
        Assert.AreEqual(20, Access("d_sw3", "Gi1/0/15"));
        Assert.AreEqual("trunk", Port("d_sw3", "Gi1/0/25")["vlan"]!["mode"]!.GetValue<string>());
        Assert.AreEqual("Configuration de « SW-Étage1 » collée sur « SW-Étage3 » : 28 ports repris, par leur place. Ctrl+Z annule.", TextOf(scene, Announce));

        // Un autre type, un autre nombre de ports : ce qui se correspond est annoncé avant.
        OpenPaste("d_coeur", "Cœur");
        Assert.AreEqual("Switch de cœur (core) · 24 ports, par leur place", Detail("d_coeur"));
        Press(scene, "dialog.confirmer[open] .boutons-dialogue .bouton:not(.principal)");
        scene.WaitFor("document.querySelector('dialog.confirmer[open]') === null");

        // À 470 pixels, dans le tiroir des propriétés : la fenêtre tient dans la largeur.
        Narrow(scene);
        Press(scene, ".bouton-volet");
        scene.WaitFor("document.querySelector('.volet.ouvert') !== null");
        scene.WaitFor("document.getAnimations().length === 0");
        Press(scene, ".proprietes .tete .plus");
        scene.WaitFor("document.querySelector('.proprietes .menu [data-action=coller-configuration]:not(:disabled)') !== null");
        Press(scene, ".proprietes .menu [data-action=coller-configuration]");
        scene.WaitFor("document.querySelector('dialog.confirmer[open] fieldset.par') !== null");
        Assert.AreEqual("true", scene.Script("(() => { const d = document.querySelector('dialog.confirmer[open]').getBoundingClientRect(); return d.left >= 0 && d.right <= innerWidth; })()"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-coller-470-{suffix}");
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Pas de défilement horizontal.");
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void A_right_click_on_a_device_opens_the_actions_of_its_dots_for_it_or_for_the_selection(ThemeKind theme)
    {
        using var scene = new IslandScene(LyceeEtTroisiemeEtage(), theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        var id = OpenInAdvancedMode(scene);
        JsonNode Saved(string device) => JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!["equipements"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == device)!;
        int? Access(string device, string name) => Saved(device)["interfaces"]!.AsArray().Single(i => i!["nom"]!.GetValue<string>() == name)!["vlan"]?["acces"]?.GetValue<int>();
        const string onPlan = ".menu.flottant[role=menu]";
        FramePlan(scene);

        // Les entrées de « ⋯ », pour comparer.
        PressDevice(scene, "d_sw1", "SW-Étage1");
        Press(scene, ".proprietes .tete .plus");
        scene.WaitFor("document.querySelector('.proprietes .menu[role=menu]') !== null");
        var dots = Entries(scene, ".proprietes .menu");
        Key(scene, "Escape", "Escape", 0x1B);
        scene.WaitFor("document.querySelector('.proprietes .menu') === null");

        // Clic droit sur un équipement qui n'était pas choisi : il est choisi, et le menu a les mêmes entrées, dans le même ordre.
        Press(scene, Device("d_sw2") + " rect.pave", "right");
        scene.WaitFor($"document.querySelector('{onPlan}') !== null");
        Assert.AreEqual("SW-Étage2", TextOf(scene, "document.querySelector('.proprietes .tete .nom').textContent"), "le clic droit choisit l'équipement");
        Assert.AreEqual(dots, Entries(scene, onPlan));
        Assert.AreEqual("Connecter…|Dupliquer|Copier|Verrouiller la place|Copier la configuration|Coller sur…|Retirer…", Entries(scene, onPlan));
        Assert.AreEqual("true", scene.Script($"document.activeElement?.closest('{onPlan}') !== null"), "le menu prend le clavier");
        Assert.AreEqual("true", scene.Script($"getComputedStyle(document.querySelector('{onPlan} [data-action=retirer]')).color !== getComputedStyle(document.querySelector('{onPlan} [data-action=dupliquer]')).color"), "Retirer, en couleur d'alerte");
        Assert.AreEqual("true", scene.Script($"(() => {{ const m = document.querySelector('{onPlan}').getBoundingClientRect(); return m.left >= 0 && m.top >= 0 && m.right <= innerWidth && m.bottom <= innerHeight; }})()"), "le menu tient dans la fenêtre");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-menu-clic-droit-{suffix}");

        // Échap le ferme et rend la main à l'équipement.
        Key(scene, "Escape", "Escape", 0x1B);
        scene.WaitFor($"document.querySelector('{onPlan}') === null");
        WaitForFocus(scene, Device("d_sw2"));
        Assert.AreEqual(1, Count(scene, "svg.canevas g.equipement.choisi"), "Échap ferme le menu sans lâcher l'équipement");

        // Au clavier : la touche Menu sur l'équipement qui a la main ; les flèches, puis Entrée.
        scene.Script("document.querySelector('" + Device("d_sw1") + "').focus()");
        Key(scene, "ContextMenu", "ContextMenu", 0x5D);
        scene.WaitFor($"document.querySelector('{onPlan}') !== null");
        Assert.AreEqual("SW-Étage1", TextOf(scene, "document.querySelector('.proprietes .tete .nom').textContent"));
        for (var i = 0; i < 4; i++)
        {
            Key(scene, "ArrowDown", "ArrowDown", 0x28);
        }
        Assert.AreEqual("copier-configuration", TextOf(scene, "document.activeElement.dataset.action"));
        Key(scene, "Enter", "Enter", 0x0D);
        scene.WaitFor("document.querySelector('.annonce')?.textContent.includes('copiée') === true");
        scene.WaitFor($"document.querySelector('{onPlan}') === null");
        WaitForFocus(scene, Device("d_sw1"));
        Assert.AreEqual(320d, Saved("d_sw1")["position"]!["x"]!.GetValue<double>(), "les flèches n'ont pas déplacé l'équipement");

        // Maj+F10 aussi, puis « Coller sur… » : la même fenêtre que depuis « ⋯ ».
        scene.Script("document.querySelector('" + Device("d_sw2") + "').focus()");
        Key(scene, "F10", "F10", 0x79, modifiers: 8);
        scene.WaitFor($"document.querySelector('{onPlan} [data-action=coller-configuration]:not(:disabled)') !== null");
        Press(scene, $"{onPlan} [data-action=coller-configuration]");
        scene.WaitFor("document.querySelector('dialog.confirmer[open] input[name=cible][value=d_sw2]')?.checked === true");
        Press(scene, "dialog.confirmer[open] button[type=submit]");
        UiThread.Until(() => Access("d_sw2", "1/5") == 10, TimeSpan.FromSeconds(20));
        WaitForFocus(scene, Device("d_sw2"));

        // Plusieurs équipements choisis : le menu agit sur eux tous, quand l'action a un sens.
        Press(scene, Device("d_sw3") + " rect.pave", modifiers: 2);
        Assert.AreEqual(2, Count(scene, "svg.canevas g.equipement.choisi"));
        Press(scene, Device("d_sw3") + " rect.pave", "right");
        scene.WaitFor($"document.querySelector('{onPlan}') !== null");
        Assert.AreEqual("Dupliquer|Copier|Coller la configuration de « SW-Étage1 »|Retirer…", Entries(scene, onPlan));
        Assert.AreEqual("Actions sur 2 équipements", TextOf(scene, $"document.querySelector('{onPlan}').getAttribute('aria-label')"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-menu-clic-droit-plusieurs-{suffix}");
        Press(scene, $"{onPlan} [data-action=coller-sur-la-selection]");
        UiThread.Until(() => Access("d_sw3", "Gi1/0/5") == 10, TimeSpan.FromSeconds(20));
        Assert.AreEqual("Configuration de « SW-Étage1 » collée sur 2 équipements : 56 ports repris (dont 28 par leur place). Ctrl+Z annule.", TextOf(scene, Announce));

        // « Retirer… » demande d'abord, pour les deux ; Annuler ne retire rien.
        Press(scene, Device("d_sw2") + " rect.pave", "right");
        scene.WaitFor($"document.querySelector('{onPlan} [data-action=retirer]') !== null");
        Press(scene, $"{onPlan} [data-action=retirer]");
        scene.WaitFor("document.querySelector('dialog.confirmer[open]')?.textContent.includes('Retirer 2 équipements ?') === true");
        StringAssert.Contains(TextOf(scene, "document.querySelector('dialog.confirmer[open] .aide').textContent"), "Ils quittent le schéma avec leur câble.", StringComparison.Ordinal);
        Press(scene, "dialog.confirmer[open] .boutons-dialogue .bouton:not(.principal)");
        scene.WaitFor("document.querySelector('dialog.confirmer[open]') === null");
        Assert.AreEqual(5, Count(scene, "svg.canevas g.equipement"));

        // Un clic droit sur un équipement hors de la sélection la remplace.
        Press(scene, Device("d_ap") + " rect.pave", "right");
        scene.WaitFor($"document.querySelector('{onPlan}') !== null");
        Assert.AreEqual(1, Count(scene, "svg.canevas g.equipement.choisi"));
        Key(scene, "Escape", "Escape", 0x1B);
        scene.WaitFor($"document.querySelector('{onPlan}') === null");

        // Mode simplifié : ce qui y a un sens, sans les ports ni la connexion par port.
        scene.Script("document.querySelector('.segments.mode button[data-mode=\"simplifie\"]').click()");
        scene.WaitFor("document.querySelector('.segments.mode button[data-mode=\"simplifie\"]').getAttribute('aria-pressed') === 'true'");
        Press(scene, Device("d_ap") + " rect.pave", "right");
        scene.WaitFor($"document.querySelector('{onPlan}') !== null");
        Assert.AreEqual("Dupliquer|Copier|Verrouiller la place|Retirer…", Entries(scene, onPlan));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-menu-clic-droit-simplifie-{suffix}");
        Press(scene, $"{onPlan} [data-action=verrouiller]");
        UiThread.Until(() => Saved("d_ap")["verrou"]?.GetValue<bool>() == true, TimeSpan.FromSeconds(20));
        Assert.AreEqual("Place verrouillée : l'équipement ne bouge plus sur le plan.", TextOf(scene, Announce));

        // À 470 pixels, en mode avancé : le menu reste entier dans la fenêtre.
        scene.Script("document.querySelector('.segments.mode button[data-mode=\"avance\"]').click()");
        scene.WaitFor("document.querySelector('.segments.mode button[data-mode=\"avance\"]').getAttribute('aria-pressed') === 'true'");
        Narrow(scene);
        scene.Script("document.querySelector('svg.canevas').focus()");
        Key(scene, "0", "Digit0", 0x30);
        scene.WaitFor("document.getAnimations().length === 0");
        if (Count(scene, ".volet.ouvert") > 0)
        {
            Press(scene, ".bouton-volet");
            scene.WaitFor("document.querySelector('.volet.ouvert') === null");
        }
        scene.Script("document.querySelector('svg.canevas').focus()");
        Key(scene, "0", "Digit0", 0x30);
        scene.WaitFor("document.getAnimations().length === 0");
        // Le premier équipement que l'on voit en entier sous le pointeur : à cette largeur, le plan en montre une partie.
        var seen = TextOf(scene, "[...document.querySelectorAll('svg.canevas g.equipement')].find((g) => { const r = g.querySelector('rect.pave').getBoundingClientRect(); const s = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2); return s && g.contains(s); })?.dataset.id ?? ''");
        Assert.AreNotEqual(string.Empty, seen, "un équipement au moins se voit sur le plan");
        Press(scene, Device(seen) + " rect.pave", "right");
        scene.WaitFor($"document.querySelector('{onPlan}') !== null");
        Assert.AreEqual("true", scene.Script($"(() => {{ const m = document.querySelector('{onPlan}').getBoundingClientRect(); return m.left >= 0 && m.top >= 0 && m.right <= innerWidth && m.bottom <= innerHeight; }})()"), "le menu tient dans la fenêtre étroite");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-menu-clic-droit-470-{suffix}");
        Key(scene, "Escape", "Escape", 0x1B);
        scene.WaitFor($"document.querySelector('{onPlan}') === null");
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Pas de défilement horizontal.");
    }
}
