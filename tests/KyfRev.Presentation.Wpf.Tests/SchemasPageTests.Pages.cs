using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Les retours sur les pages d'un schéma (30A) : les parcourir en lecture, un câble d'une page à l'autre, le renvoi déplacé.</summary>
public sealed partial class SchemasPageTests
{
    /// <summary>Les onglets du sélecteur de la vue Schémas, la page montrée suivie d'une étoile.</summary>
    private static string ReadTabs(IslandScene scene) =>
        JsonSerializer.Deserialize<string>(scene.Script("[...document.querySelectorAll('.selecteur-pages [role=tab]')].map((o) => o.childNodes[0].textContent.trim() + (o.getAttribute('aria-selected') === 'true' ? '*' : '')).join('|')"))!;

    private static string Figure(IslandScene scene) =>
        JsonSerializer.Deserialize<string>(scene.Script("document.querySelector('figure.grand-apercu').textContent"))!;

    /// <summary>Un point de l'élément où lui seul est sous le pointeur.</summary>
    private static double[] Grip(IslandScene scene, string selector) =>
        JsonSerializer.Deserialize<double[]?>(JsonSerializer.Deserialize<string>(scene.Script(
            "(() => { const e = document.querySelector('" + selector + "'); const r = e.getBoundingClientRect(); for (const fy of [0.5, 0.35, 0.65]) for (const fx of [0.5, 0.35, 0.65, 0.2, 0.8]) { const x = r.x + r.width * fx, y = r.y + r.height * fy; const s = document.elementFromPoint(x, y); if (s && (s === e || e.contains(s))) return JSON.stringify([x, y]); } return JSON.stringify(null); })()"))!)
        ?? throw new AssertFailedException($"aucun point de « {selector} » sous le pointeur");

    private static void KeyOn(IslandScene scene, string key, bool shift = false) =>
        scene.Script("document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: '" + key + "', shiftKey: " + (shift ? "true" : "false") + ", bubbles: true, cancelable: true }))");

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void The_detail_of_a_schema_reads_each_of_its_pages(ThemeKind theme)
    {
        using var scene = new IslandScene(Lycee, theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        var model = (SchemasPageViewModel)scene.Page.DataContext;
        // La carte le dit, une fois le schéma lu : « 10 équipements · 6 liens · 4 pages ».
        scene.WaitFor("document.querySelector('li.carte .meta')?.textContent.includes('4 pages') === true");

        // Le détail montre la première page, et le sélecteur des quatre, juste au-dessus de l'aperçu, à droite.
        scene.Script("document.querySelector('li.carte button.apercu').click()");
        scene.WaitFor("document.querySelector('.selecteur-pages') !== null");
        Assert.AreEqual("Étage 1*|Étage 2|Salle serveurs|Site distant", ReadTabs(scene));
        StringAssert.Contains(Figure(scene), "PC-Accueil", StringComparison.Ordinal);
        Assert.IsFalse(Figure(scene).Contains("PC-Salle 201", StringComparison.Ordinal));
        Assert.AreEqual("true", scene.Script("(() => { const s = document.querySelector('.selecteur-pages').getBoundingClientRect(), f = document.querySelector('figure.grand-apercu').getBoundingClientRect(); return s.bottom <= f.top + 1 && Math.abs(s.right - f.right) < 2; })()"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-detail-pages-{suffix}");

        // Un clic : l'étage 2, et l'aperçu le dit.
        scene.Script("document.querySelectorAll('.selecteur-pages [role=tab]')[1].click()");
        scene.WaitFor("document.querySelector('figure.grand-apercu').textContent.includes('PC-Salle 201')");
        Assert.AreEqual("Étage 1|Étage 2*|Salle serveurs|Site distant", ReadTabs(scene));
        Assert.IsFalse(Figure(scene).Contains("PC-Accueil", StringComparison.Ordinal));
        StringAssert.Contains(JsonSerializer.Deserialize<string>(scene.Script("document.querySelector('figure.grand-apercu').getAttribute('aria-label')")), "page « Étage 2 »", StringComparison.Ordinal);
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-detail-pages-etage2-{suffix}");

        // Le renvoi de l'aperçu, cliqué, ramène à la page d'en face.
        scene.Script("document.querySelector('figure.grand-apercu [data-renvoi] rect').dispatchEvent(new MouseEvent('click', { bubbles: true }))");
        scene.WaitFor("document.querySelector('figure.grand-apercu').textContent.includes('PC-Accueil')");
        Assert.AreEqual("Étage 1*|Étage 2|Salle serveurs|Site distant", ReadTabs(scene));

        // Au clavier : une seule tabulation entre dans le sélecteur ; les flèches, Début et Fin changent de page, la page montrée garde la main.
        Assert.AreEqual(1, int.Parse(scene.Script("document.querySelectorAll('.selecteur-pages [tabindex=\"0\"]').length"), CultureInfo.InvariantCulture));
        scene.Script("document.querySelector('.selecteur-pages [aria-selected=true]').focus()");
        KeyOn(scene, "ArrowRight");
        scene.WaitFor("document.activeElement.getAttribute('aria-selected') === 'true' && document.activeElement.textContent.includes('Étage 2')");
        KeyOn(scene, "End");
        scene.WaitFor("document.activeElement.textContent.includes('Site distant') && document.querySelector('figure.grand-apercu').textContent.includes('Routeur du site')");
        KeyOn(scene, "ArrowRight");
        scene.WaitFor("document.activeElement.textContent.includes('Étage 1')");
        KeyOn(scene, "ArrowLeft");
        scene.WaitFor("document.activeElement.textContent.includes('Site distant')");
        KeyOn(scene, "Home");
        scene.WaitFor("document.activeElement.textContent.includes('Étage 1')");
        scene.Script("document.querySelectorAll('.selecteur-pages [role=tab]')[2].click()");
        scene.WaitFor("document.querySelector('figure.grand-apercu').textContent.includes('NAS')");

        // L'impression part de la page montrée, son nom dans le titre.
        scene.Script("[...document.querySelectorAll('.entete-lecture button')].find((b) => b.textContent.includes('Imprimer')).click()");
        UiThread.Until(() => model.Print.IsOpen, TimeSpan.FromSeconds(30));
        Assert.AreEqual("Imprimer « Réseau du lycée - Salle serveurs »", model.Print.Title);
        model.Print.Close();

        // Fenêtre étroite : le sélecteur tient, rien ne défile de côté.
        Narrow(scene);
        scene.WaitFor("document.querySelector('.selecteur-pages') !== null");
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Pas de défilement horizontal à 470 pixels.");
        Assert.AreEqual("true", scene.Script("(() => { const r = document.querySelector('.selecteur-pages').getBoundingClientRect(); return r.left >= 0 && r.right <= innerWidth; })()"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-detail-pages-etroit-{suffix}");

        // Très large : il reste collé au coin de l'aperçu.
        var before = scene.Script("document.documentElement.clientWidth");
        scene.Page.Width = 2400;
        TestShell.Flush();
        UiThread.Until(() => scene.Script("document.documentElement.clientWidth") != before, TimeSpan.FromSeconds(20));
        Assert.AreEqual("true", scene.Script("(() => { const s = document.querySelector('.selecteur-pages').getBoundingClientRect(), f = document.querySelector('figure.grand-apercu').getBoundingClientRect(); return Math.abs(s.right - f.right) < 2; })()"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-detail-pages-tres-large-{suffix}");
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void A_cable_taken_from_the_quick_bar_is_plugged_on_another_page(ThemeKind theme)
    {
        using var scene = new IslandScene(Lycee, theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        var id = scene.Schemas.Schemas().Value.Single().Id;
        JsonNode Saved() => JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!;
        string Hint() => JsonSerializer.Deserialize<string>(scene.Script("document.querySelector('.consigne-cable span')?.textContent ?? ''"))!;
        OpenEditor(scene);

        // Le cœur choisi au clavier, « Brancher un câble » dans sa petite barre : ses ports sont pris, un port de plus.
        scene.Script("(() => { const e = document.querySelector('svg.canevas g.equipement[data-id=\"d_coeur\"]'); e.focus(); e.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true })); })()");
        scene.WaitFor("document.querySelector('.barre-rapide button[data-action=\"connecter\"], .barre-rapide button[aria-label=\"Brancher un câble\"]') !== null");
        scene.Script("document.querySelector('.barre-rapide button[data-action=\"connecter\"], .barre-rapide button[aria-label=\"Brancher un câble\"]').click()");
        scene.WaitFor("document.querySelector('.menu-ports .ajout') !== null");
        scene.Script("document.querySelector('.menu-ports .ajout').click()");
        scene.WaitFor("document.querySelector('.consigne-cable') !== null && document.querySelector('.menu-ports') === null");
        Assert.AreEqual("Câble en main depuis Cœur : choisissez un équipement, sur cette page ou une autre.", Hint());

        // Un onglet cliqué : l'autre page, le câble toujours en main ; la consigne dit d'où il vient.
        scene.Script("document.querySelectorAll('.pages .onglet')[2].click()");
        scene.WaitFor("document.querySelector('.pages .onglet[aria-selected=true] .nom').textContent === 'Salle serveurs'");
        Assert.AreEqual("Câble en main depuis Cœur (Étage 1) : choisissez un équipement, sur cette page ou une autre.", Hint());
        Assert.AreEqual("true", scene.Script("document.querySelector('svg.canevas').classList.contains('cable')"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-cable-entre-pages-{suffix}");

        // Le serveur choisi : le menu de ses ports, comme d'habitude ; un port de plus, et le câble est branché d'une page à l'autre.
        scene.Script("(() => { const e = document.querySelector('svg.canevas g.equipement[data-id=\"d_srv\"]'); e.focus(); e.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true })); })()");
        scene.WaitFor("document.querySelector('.menu-ports .ajout') !== null");
        StringAssert.Contains(scene.Script("document.querySelector('.menu-ports').textContent"), "Serveur", StringComparison.Ordinal);
        scene.Script("document.querySelector('.menu-ports .ajout').click()");
        scene.WaitFor("document.querySelector('.consigne-cable') === null");
        UiThread.Until(() => Saved()["liens"]!.AsArray().Any(link => link!["a"]!["equipement"]!.GetValue<string>() == "d_coeur" && link["b"]!["equipement"]!.GetValue<string>() == "d_srv"), TimeSpan.FromSeconds(20));
        StringAssert.Contains(scene.Script("document.querySelector('.annonce')?.textContent ?? ''"), "Câble branché vers « Étage 1 » : un renvoi le montre sur chaque page.", StringComparison.Ordinal);
        scene.WaitFor("[...document.querySelectorAll('svg.canevas g.renvoi text')].some((t) => t.textContent.startsWith('→ Étage 1 · Cœur'))");
        scene.Script("document.querySelectorAll('.pages .onglet')[0].click()");
        scene.WaitFor("[...document.querySelectorAll('svg.canevas g.renvoi text')].some((t) => t.textContent.startsWith('→ Salle serveurs · Serveur'))");

        // Échap repose le câble en main, même après un changement de page.
        scene.Script("(() => { const e = document.querySelector('svg.canevas g.equipement[data-id=\"d_sw1\"]'); e.focus(); e.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true })); })()");
        scene.WaitFor("document.querySelector('.barre-rapide button[data-action=\"connecter\"], .barre-rapide button[aria-label=\"Brancher un câble\"]') !== null");
        scene.Script("document.querySelector('.barre-rapide button[data-action=\"connecter\"], .barre-rapide button[aria-label=\"Brancher un câble\"]').click()");
        scene.WaitFor("document.querySelector('.menu-ports .ajout') !== null");
        scene.Script("document.querySelector('.menu-ports .ajout').click()");
        scene.WaitFor("document.querySelector('.consigne-cable') !== null");
        scene.Script("document.querySelectorAll('.pages .onglet')[1].click()");
        scene.WaitFor("document.querySelector('.pages .onglet[aria-selected=true] .nom').textContent === 'Étage 2'");
        Assert.AreEqual("Câble en main depuis SW-Étage1 (Étage 1) : choisissez un équipement, sur cette page ou une autre.", Hint());
        scene.Script("document.body.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }))");
        scene.WaitFor("document.querySelector('.consigne-cable') === null");

        // Mode simplifié, outil Câble : commencé sur une page, il se termine sur une autre.
        scene.Script("document.querySelector('.segments.mode button[data-mode=\"simplifie\"]').click()");
        scene.WaitFor("document.querySelector('.segments.mode button[data-mode=\"simplifie\"]').getAttribute('aria-pressed') === 'true'");
        scene.Script("document.querySelector('button.outil[data-outil=\"cable\"]').click()");
        scene.Script("(() => { const e = document.querySelector('svg.canevas g.equipement[data-id=\"d_sw2\"]'); e.focus(); e.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true })); })()");
        scene.Script("document.querySelectorAll('.pages .onglet')[2].click()");
        scene.WaitFor("document.querySelector('.consigne-outil')?.textContent === 'Câble depuis SW-Étage2 (Étage 2) : choisissez l\\'autre équipement, sur cette page ou une autre.'");
        scene.Script("(() => { const e = document.querySelector('svg.canevas g.equipement[data-id=\"d_nas\"]'); e.focus(); e.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true })); })()");
        UiThread.Until(() => Saved()["liens"]!.AsArray().Any(link => link!["a"]!["equipement"]!.GetValue<string>() == "d_sw2" && link["b"]!["equipement"]!.GetValue<string>() == "d_nas"), TimeSpan.FromSeconds(20));
        StringAssert.Contains(scene.Script("document.querySelector('.annonce')?.textContent ?? ''"), "Câble tiré entre SW-Étage2 et NAS.", StringComparison.Ordinal);
        scene.Script("document.querySelector('button.outil[data-outil=\"choisir\"]').click()");
        scene.Script("document.querySelector('.segments.mode button[data-mode=\"avance\"]').click()");
        scene.WaitFor("document.querySelector('.segments.mode button[data-mode=\"avance\"]').getAttribute('aria-pressed') === 'true'");
        scene.Script("document.querySelectorAll('.pages .onglet')[1].click()");
        scene.WaitFor("document.querySelector('.pages .onglet[aria-selected=true] .nom').textContent === 'Étage 2'");

        // Fenêtre étroite : le menu des pages, câble en main, garde le câble ; la consigne tient dans la fenêtre.
        Narrow(scene);
        scene.WaitFor("getComputedStyle(document.querySelector('.pages .replie')).display === 'flex'");
        scene.Script("(() => { const e = document.querySelector('svg.canevas g.equipement[data-id=\"d_sw2\"]'); e.focus(); e.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true })); })()");
        scene.WaitFor("document.querySelector('.barre-rapide button[data-action=\"connecter\"], .barre-rapide button[aria-label=\"Brancher un câble\"]') !== null");
        scene.Script("document.querySelector('.barre-rapide button[data-action=\"connecter\"], .barre-rapide button[aria-label=\"Brancher un câble\"]').click()");
        scene.WaitFor("document.querySelector('.menu-ports .ajout') !== null");
        scene.Script("document.querySelector('.menu-ports .ajout').click()");
        scene.WaitFor("document.querySelector('.consigne-cable') !== null");
        scene.Script("document.querySelector('.pages .choix').click()");
        scene.WaitFor("document.querySelector('.menu[role=menu]') !== null");
        scene.Script("[...document.querySelectorAll('.menu[role=menu] [role=menuitemradio]')].find((b) => b.textContent.includes('Site distant')).click()");
        scene.WaitFor("document.querySelector('.pages .choix .nom').textContent === 'Site distant'");
        Assert.AreEqual("Câble en main depuis SW-Étage2 (Étage 2) : choisissez un équipement, sur cette page ou une autre.", Hint());
        Assert.AreEqual("true", scene.Script("(() => { const r = document.querySelector('.consigne-cable').getBoundingClientRect(); return r.left >= 0 && r.right <= innerWidth; })()"));
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-cable-entre-pages-etroit-{suffix}");
        scene.Script("document.body.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }))");
        scene.WaitFor("document.querySelector('.consigne-cable') === null");
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void The_cable_to_another_page_has_a_badge_that_moves_by_hand(ThemeKind theme)
    {
        using var scene = new IslandScene(Lycee, theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        var id = scene.Schemas.Schemas().Value.Single().Id;
        JsonNode Saved() => JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!;
        JsonNode? Pose(string key) => Saved()["liens"]!.AsArray().Single(link => link!["id"]!.GetValue<string>() == "l_montee")!["poses"]?[key];
        int Count(string selector) => int.Parse(scene.Script($"document.querySelectorAll('{selector}').length"), CultureInfo.InvariantCulture);
        const string badge = "svg.canevas g.renvoi[data-renvoi=\"l_montee\"]";
        OpenEditor(scene);
        // Le plan se cadre à sa première image, que cette fenêtre hors de l'écran ne dessine qu'au premier mouvement.
        scene.Mouse("mouseMoved", 4, 4);
        scene.WaitFor("document.querySelector('svg.canevas').getAttribute('viewBox') !== '-400 -300 800 600'");

        // Tirée, la pastille se déplace et reste sur sa page ; elle est choisie, « Remettre à sa place » sous elle, sans poignée de rotation.
        var grip = Grip(scene, badge);
        scene.Drag(grip[0], grip[1], grip[0] + 40, grip[1] + 90);
        UiThread.Until(() => Pose("renvoi-a")?["place"] is not null, TimeSpan.FromSeconds(20));
        Assert.IsGreaterThan(40d, Pose("renvoi-a")!["place"]!["y"]!.GetValue<double>());
        Assert.IsNull(Pose("renvoi-b"), "l'autre bout, sur l'autre page, n'a pas bougé");
        Assert.AreEqual(3, Saved()["format"]!.GetValue<int>());
        Assert.AreEqual("Étage 1", JsonSerializer.Deserialize<string>(scene.Script("document.querySelector('.pages .onglet[aria-selected=true] .nom').textContent")), "tirer n'est pas suivre");
        Assert.AreEqual(1, Count(".barre-etiquette button[data-action=\"remettre\"]:not([disabled])"));
        Assert.AreEqual(0, Count(".poignee-rotation"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-renvoi-deplace-{suffix}");

        // Un simple clic mène toujours à l'autre page ; la pastille d'en face est à sa place calculée.
        grip = Grip(scene, badge);
        scene.Click(grip[0], grip[1]);
        scene.WaitFor("document.querySelector('.pages .onglet[aria-selected=true] .nom').textContent === 'Étage 2'");
        scene.WaitFor("document.querySelector('svg.canevas g.equipement.choisi')?.dataset.id === 'd_sw2'");

        // De retour : au clavier, une flèche choisit la pastille et la déplace (Maj : 10 pixels).
        scene.Script("document.querySelectorAll('.pages .onglet')[0].click()");
        scene.WaitFor("document.querySelector('.pages .onglet[aria-selected=true] .nom').textContent === 'Étage 1'");
        var before = Pose("renvoi-a")!["place"]!["x"]!.GetValue<double>();
        scene.Script($"document.querySelector('{badge}').focus()");
        KeyOn(scene, "ArrowRight", shift: true);
        UiThread.Until(() => Pose("renvoi-a")?["place"]?["x"]?.GetValue<double>() == before + 10, TimeSpan.FromSeconds(20));
        scene.WaitFor("document.querySelector('.barre-etiquette button[data-action=\"remettre\"]') !== null");

        // « Remettre à sa place » : la pose oubliée, le fichier n'en garde rien.
        scene.Script("document.querySelector('.barre-etiquette button[data-action=\"remettre\"]').click()");
        UiThread.Until(() => Pose("renvoi-a") is null, TimeSpan.FromSeconds(20));

        // Fenêtre étroite : la pastille se tire de même.
        Narrow(scene);
        scene.Mouse("mouseMoved", 6, 6);
        scene.Script("document.querySelector('svg.canevas').focus()");
        KeyOn(scene, "0");
        scene.WaitFor($"document.querySelector('{badge}') !== null");
        grip = Grip(scene, badge);
        scene.Drag(grip[0], grip[1], grip[0] - 30, grip[1] + 60);
        UiThread.Until(() => Pose("renvoi-a")?["place"] is not null, TimeSpan.FromSeconds(20));
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-renvoi-deplace-etroit-{suffix}");
    }
}
