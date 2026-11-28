using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// L'accueil des schémas (31A, 31B, 31E, 31F) : des cartes allégées, les épinglés en tête, les filtres par étiquette et par
/// diagnostic, le tri, la liste ; plusieurs schémas choisis et traités ensemble ; épingler, étiqueter, renommer ; de
/// 470 pixels à un très grand écran, en clair et en sombre.
/// </summary>
public sealed partial class SchemasPageTests
{
    private const string Affichage = "kyfrev.schemas.affichage";

    /// <summary>Un petit schéma de deux équipements ; « doublon » leur donne la même adresse : un point à vérifier.</summary>
    private static string Petit(string titre, string etiquettes, bool doublon = false) =>
        "{\"format\": 2, \"titre\": " + JsonSerializer.Serialize(titre) + ", \"vue\": \"physique\", \"etiquettes\": [" + etiquettes + "], \"equipements\": ["
        + "{\"id\": \"d_a\", \"type\": \"switch\", \"nom\": \"SW-A\", \"position\": {\"x\": 0, \"y\": 0}, \"gestion\": {\"adresse\": \"10.0.30.5/24\"}},"
        + "{\"id\": \"d_b\", \"type\": \"pc\", \"nom\": \"PC-B\", \"position\": {\"x\": 200, \"y\": 120}, \"gestion\": {\"adresse\": \"" + (doublon ? "10.0.30.5/24" : "10.0.30.6/24") + "\"}}]}";

    /// <summary>
    /// L'accueil de l'essai : le lycée épinglé (quatre pages, étiqueté « Lycée »), et quatre autres schémas, dont un à vérifier.
    /// La liste est relue comme au retour sur la vue, et l'essai attend que chaque carte soit lue.
    /// </summary>
    private static IslandScene Accueil(ThemeKind theme)
    {
        var scene = new IslandScene(Lycee.Replace("\"vue\": \"physique\",", "\"vue\": \"physique\", \"etiquettes\": [\"Lycée\"], \"epingle\": true,", StringComparison.Ordinal), theme: theme);
        scene.Script($"localStorage.removeItem('{Affichage}')");
        // Les erreurs de la page sont gardées : un échec les dit.
        scene.Script("window.erreursDeLEssai = []; addEventListener('error', (e) => erreursDeLEssai.push(e.message)); addEventListener('unhandledrejection', (e) => erreursDeLEssai.push(String(e.reason)))");
        scene.Schemas.Create(Petit("SAMES Meylan", "\"Client\"", doublon: true)).Value.ToString();
        scene.Schemas.Create(Petit("Labo réseau (TP 3)", "\"TP\", \"lycée\"")).Value.ToString();
        scene.Schemas.Create(Petit("Site distant Grenoble", "\"Client\"")).Value.ToString();
        scene.Schemas.Create(Petit("Salle serveurs B012", string.Empty)).Value.ToString();
        scene.Script("document.dispatchEvent(new Event('schemas-revenus'))");
        // Un essai précédent a pu laisser la liste retenue : l'accueil revient en grille.
        Expect(scene, "document.querySelector('.bascule button') !== null");
        scene.Script("document.querySelector('.bascule button').click()");
        Expect(scene, "document.querySelectorAll('li.carte').length === 5 && [...document.querySelectorAll('li.carte .meta')].every((m) => m.textContent.includes('équipement'))");
        Expect(scene, "document.querySelector('li.carte .meta')?.textContent.includes('4 pages') === true");
        // Chaque schéma lu (sa vignette dessinée, ses étiquettes avec elle) : sans quoi les rangées bougent encore, et un clic
        // visé avant tombe à côté.
        Expect(scene, "document.querySelectorAll('li.carte .apercu > svg').length === 5 && document.getAnimations().length === 0");
        return scene;
    }

    /// <summary>Attend qu'une condition soit vraie ; sinon, l'échec dit ce que l'accueil montre (avis, menu, fenêtre), pour la CI.</summary>
    private static void Expect(IslandScene scene, string condition)
    {
        try
        {
            scene.WaitFor(condition);
        }
        catch (AssertFailedException)
        {
            throw new AssertFailedException($"jamais vrai : {condition} ; avis : {TextOf(scene, "document.querySelector('.annonces')?.textContent.trim() ?? ''")} ; menu : {TextOf(scene, "document.querySelector('.menu[role=menu]')?.textContent.trim() ?? 'aucun'")} ; fenêtre : {TextOf(scene, "document.querySelector('dialog[open]')?.textContent.trim().slice(0, 200) ?? 'aucune'")} ; erreurs de la page : {TextOf(scene, "JSON.stringify(window.erreursDeLEssai ?? [])")}");
        }
    }

    private static string IdOf(IslandScene scene, string title) => scene.Schemas.Schemas().Value.Single(schema => schema.Title == title).Id;

    private static string CardOf(IslandScene scene, string title) => $"li.carte[data-id=\"{IdOf(scene, title)}\"]";

    private static string Titles(IslandScene scene, string within = "") =>
        TextOf(scene, "[...document.querySelectorAll('" + within + " li.carte .nom')].map((n) => n.textContent.trim()).join('|')");

    private static string Chips(IslandScene scene) =>
        TextOf(scene, "[...document.querySelectorAll('.outils .puce')].map((p) => p.textContent.trim().replace(/\\s+/g, ' ') + (p.getAttribute('aria-pressed') === 'true' ? '*' : '')).join('|')");

    private static void Chip(IslandScene scene, string name) =>
        scene.Script("[...document.querySelectorAll('.outils .puce')].find((p) => p.textContent.trim().startsWith(" + JsonSerializer.Serialize(name) + ")).click()");

    private static string Written(IslandScene scene, string title) => scene.Schemas.Read(IdOf(scene, title)).Value.Text;

    /// <summary>Le clic droit sur une carte, et une entrée de son menu ; le clic droit se redonne s'il s'est perdu (CI).</summary>
    private static void CardMenu(IslandScene scene, string title, string entry)
    {
        var card = CardOf(scene, title);
        UiThread.Until(() =>
        {
            scene.Script("document.querySelector('" + card + "').dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 80, clientY: 80 }))");
            TestShell.Flush();
            return scene.Script("document.querySelector('.menu[role=menu]') !== null") == "true";
        }, TimeSpan.FromSeconds(20));
        var find = "[...document.querySelectorAll('.menu[role=menu] [role=menuitem]')].find((b) => b.textContent.trim().startsWith(" + JsonSerializer.Serialize(entry) + "))";
        Expect(scene, find + "?.disabled === false");
        scene.Script(find + ".click()");
    }

    /// <summary>Un vrai double clic, au milieu de l'élément.</summary>
    private static void DoubleClick(IslandScene scene, string selector)
    {
        var point = GripOf(scene, selector);
        MouseEvent(scene, "mouseMoved", point[0], point[1], "left");
        foreach (var count in new[] { 1, 2 })
        {
            foreach (var type in new[] { "mousePressed", "mouseReleased" })
            {
                var run = scene.Page.Island!.CallDevToolsAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new { type, x = point[0], y = point[1], button = "left", buttons = type == "mousePressed" ? 1 : 0, clickCount = count }));
                UiThread.Until(() => run.IsCompleted);
            }
        }
        TestShell.Flush();
    }

    private static void Resize(IslandScene scene, double width)
    {
        var before = scene.Script("document.documentElement.clientWidth");
        scene.Page.Width = width;
        TestShell.Flush();
        UiThread.Until(() => scene.Script("document.documentElement.clientWidth") != before, TimeSpan.FromSeconds(20));
    }

    private static void NoSideScroll(IslandScene scene, string where) =>
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), $"pas de défilement de côté {where}");

    /// <summary>« Nouveau schéma ▾ › Schéma vide » : la fenêtre du nom s'ouvre.</summary>
    private static void NewEmptySchema(IslandScene scene)
    {
        Expect(scene, "document.querySelector('.entete-accueil .bouton.principal:not(:disabled)') !== null");
        scene.Script("document.querySelector('.entete-accueil .bouton.principal').click()");
        Expect(scene, "document.querySelector('.menu[role=menu]') !== null");
        scene.Script("[...document.querySelectorAll('.menu [role=menuitem]')].find((e) => e.textContent.trim() === 'Schéma vide').click()");
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void The_home_puts_pinned_schemas_first_and_filters_them_by_label_and_diagnosis(ThemeKind theme)
    {
        using var scene = Accueil(theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";

        // En tête, l'épinglé ; puis tous les autres, du plus récent au plus ancien. Une carte : un nom, une ligne, des étiquettes.
        Assert.AreEqual("Épinglés|Tous · modifiés récemment", TextOf(scene, "[...document.querySelectorAll('.titre-section')].map((t) => t.textContent.trim()).join('|')"));
        Assert.AreEqual("Réseau du lycée", Titles(scene, "ul.grille:first-of-type"));
        Assert.AreEqual("Salle serveurs B012|Site distant Grenoble|Labo réseau (TP 3)|SAMES Meylan", Titles(scene, "h2 + ul.grille ~ ul.grille"));
        Assert.AreEqual("5 schémas · 1 épinglé", TextOf(scene, "document.querySelector('.entete-accueil .resume').textContent.trim()"));
        StringAssert.StartsWith(TextOf(scene, "document.querySelector('" + CardOf(scene, "Réseau du lycée") + " .meta').textContent"), "10 équipements · 4 pages · modifié aujourd'hui", StringComparison.Ordinal);
        Assert.AreEqual("Client|1 point à vérifier", TextOf(scene, "[...document.querySelectorAll('" + CardOf(scene, "SAMES Meylan") + " .petite')].map((p) => p.textContent.trim()).join('|')"));
        Assert.AreEqual(4, Count(scene, CardOf(scene, "Réseau du lycée") + " .traits i"), "un trait par page");
        Assert.AreEqual(0, Count(scene, "li.carte .boutons"), "plus de rangée de boutons sous les cartes");
        Assert.AreEqual("Client 2|Lycée 2|TP 1|À vérifier 1", Chips(scene));
        NoSideScroll(scene, "à 1300");
        Expect(scene, "document.getAnimations().length === 0");
        scene.Picture($"schemas-accueil-1300-{suffix}");

        // Au survol : « Modifier » et « ⋯ » paraissent, et la vignette fait défiler les pages.
        var lycee = CardOf(scene, "Réseau du lycée");
        Assert.AreEqual("0", TextOf(scene, "getComputedStyle(document.querySelector('" + lycee + " .survol')).opacity"));
        var point = GripOf(scene, lycee + " .apercu");
        scene.Mouse("mouseMoved", point[0], point[1]);
        Expect(scene, "getComputedStyle(document.querySelector('" + lycee + " .survol')).opacity === '1'");
        Expect(scene, "[...document.querySelectorAll('" + lycee + " .traits i')].findIndex((i) => i.classList.contains('allume')) > 0");
        scene.Picture($"schemas-accueil-survol-{suffix}");
        scene.Mouse("mouseMoved", 5, 5);
        Expect(scene, "[...document.querySelectorAll('" + lycee + " .traits i')].findIndex((i) => i.classList.contains('allume')) === 0");

        // Au clavier aussi : la vignette a la main, « Modifier » et « ⋯ » se montrent.
        scene.Script("document.querySelector('" + CardOf(scene, "SAMES Meylan") + " .apercu').focus()");
        Expect(scene, "getComputedStyle(document.querySelector('" + CardOf(scene, "SAMES Meylan") + " .survol')).opacity === '1'");

        // Les puces filtrent : une étiquette, l'une ou l'autre de deux, puis « À vérifier » ; la recherche trouve un équipement.
        Chip(scene, "Client");
        Expect(scene, "document.querySelectorAll('li.carte').length === 2");
        Assert.AreEqual("Site distant Grenoble|SAMES Meylan", Titles(scene));
        Chip(scene, "TP");
        Expect(scene, "document.querySelectorAll('li.carte').length === 3");
        Assert.AreEqual("Client 2*|Lycée 2|TP 1*|À vérifier 1", Chips(scene));
        Chip(scene, "Client");
        Chip(scene, "TP");
        Chip(scene, "À vérifier");
        Expect(scene, "document.querySelectorAll('li.carte').length === 1");
        Assert.AreEqual("SAMES Meylan", Titles(scene));
        Chip(scene, "À vérifier");
        Fill(scene, ".outils input[type=search]", "salle 201");
        Expect(scene, "document.querySelectorAll('li.carte').length === 1");
        Assert.AreEqual("Réseau du lycée", Titles(scene));
        Fill(scene, ".outils input[type=search]", "rien de tel");
        Expect(scene, "document.body.innerText.includes('Aucun schéma ne correspond')");
        scene.Script("[...document.querySelectorAll('.vide button')].find((b) => b.textContent.includes('Effacer')).click()");
        Expect(scene, "document.querySelectorAll('li.carte').length === 5");

        // Le tri : par nom, puis par taille ; l'épinglé reste en tête.
        Fill(scene, ".outils .tri select", "nom");
        Expect(scene, "document.querySelectorAll('.titre-section')[1]?.textContent.includes('par nom') === true");
        Assert.AreEqual("Réseau du lycée|Labo réseau (TP 3)|Salle serveurs B012|SAMES Meylan|Site distant Grenoble", Titles(scene));
        Fill(scene, ".outils .tri select", "equipements");
        Expect(scene, "document.querySelectorAll('.titre-section')[1]?.textContent.includes('par taille') === true");

        // « Nouveau schéma ▾ » : vide, depuis un modèle, depuis un fichier draw.io, ou en réunissant des schémas.
        scene.Script("document.querySelector('.entete-accueil .bouton.principal').click()");
        Expect(scene, "document.querySelector('.menu[role=menu]') !== null");
        Assert.AreEqual("Schéma vide|Depuis un modèle… Petit bureau, étage…|Depuis un fichier draw.io…|Réunir des schémas…", TextOf(scene, "[...document.querySelectorAll('.menu [role=menuitem]')].map((b) => b.textContent.trim().replace(/\\s+/g, ' ')).join('|')"));
        Assert.AreEqual("true", scene.Script("(() => { const m = document.querySelector('.menu[role=menu]').getBoundingClientRect(); return m.left >= 0 && m.right <= innerWidth; })()"));
        Expect(scene, "document.getAnimations().length === 0");
        scene.Picture($"schemas-accueil-nouveau-{suffix}");
        scene.Script("[...document.querySelectorAll('.menu [role=menuitem]')].find((b) => b.textContent.includes('modèle')).click()");
        Expect(scene, "document.querySelector('dialog[open] h2')?.textContent.includes('modèle') === true");
        scene.Script("document.querySelector('dialog[open]').close()");
        Expect(scene, "document.querySelector('dialog[open]') === null");
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void The_home_lists_the_schemas_in_a_table_sorted_by_its_headers(ThemeKind theme)
    {
        using var scene = Accueil(theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        try
        {
            scene.Script("[...document.querySelectorAll('.bascule button')].find((b) => b.textContent.includes('Liste')).click()");
            Expect(scene, "document.querySelectorAll('.tableau tbody tr').length === 5");
            Assert.AreEqual("liste", TextOf(scene, $"localStorage.getItem('{Affichage}')"), "retenu");
            Assert.AreEqual("Schéma|Pages|Équipements|Étiquettes|Diagnostic|Cité dans|Modifié|Actions", TextOf(scene, "[...document.querySelectorAll('.tableau thead th')].slice(1).map((t) => t.textContent.trim().replace(/[▲▼]/g, '').trim()).join('|')"));
            string Rows() => TextOf(scene, "[...document.querySelectorAll('.tableau tbody th .nom')].map((n) => n.textContent.trim()).join('|')");
            string Sort() => TextOf(scene, "[...document.querySelectorAll('.tableau thead th[aria-sort]')].map((t) => t.textContent.trim().replace(/[▲▼]/g, '').trim() + ' ' + t.getAttribute('aria-sort')).join('|')");
            Assert.AreEqual("Modifié descending", Sort());
            Assert.AreEqual("4 | 10 | Lycée | — | — | aujourd'hui", TextOf(scene, "[...document.querySelector('" + "tr[data-id=\"" + IdOf(scene, "Réseau du lycée") + "\"]" + "').querySelectorAll('td')].slice(1, 7).map((t) => t.textContent.trim()).join(' | ')"));
            Assert.AreEqual("1 à vérifier", TextOf(scene, "document.querySelector('tr[data-id=\"" + IdOf(scene, "SAMES Meylan") + "\"] .a-verifier').textContent.trim()"));

            // Un clic sur « Pages » : du plus grand au plus petit ; un second, l'inverse.
            scene.Script("[...document.querySelectorAll('.tableau thead .entete')].find((b) => b.textContent.includes('Pages')).click()");
            Expect(scene, "document.querySelector('.tableau thead th[aria-sort]')?.textContent.includes('Pages') === true");
            Assert.AreEqual("Pages descending", Sort());
            StringAssert.StartsWith(Rows(), "Réseau du lycée|", StringComparison.Ordinal);
            scene.Script("[...document.querySelectorAll('.tableau thead .entete')].find((b) => b.textContent.includes('Pages')).click()");
            Expect(scene, "document.querySelector('.tableau thead th[aria-sort]')?.getAttribute('aria-sort') === 'ascending'");
            StringAssert.EndsWith(Rows(), "|Réseau du lycée", StringComparison.Ordinal);
            scene.Script("[...document.querySelectorAll('.tableau thead .entete')].find((b) => b.textContent.includes('Schéma')).click()");
            Expect(scene, "document.querySelector('.tableau thead th[aria-sort]')?.textContent.includes('Schéma') === true");
            Assert.AreEqual("Labo réseau (TP 3)|Réseau du lycée|Salle serveurs B012|SAMES Meylan|Site distant Grenoble", Rows());

            // Les mêmes choix que la grille : la case choisit, la case de l'en-tête prend tout, le clic sur un nom ouvre le détail.
            scene.Script("document.querySelector('tr[data-id=\"" + IdOf(scene, "SAMES Meylan") + "\"] td.case input').click()");
            Expect(scene, "document.querySelector('.barre-selection')?.textContent.includes('1 schéma choisi') === true");
            scene.Script("document.querySelector('.tableau thead th.case input').click()");
            Expect(scene, "document.querySelector('.barre-selection')?.textContent.includes('5 schémas choisis') === true");
            Assert.AreEqual(5, Count(scene, ".tableau tbody tr.choisie"));
            NoSideScroll(scene, "en liste");
            Expect(scene, "document.getAnimations().length === 0");
            scene.Picture($"schemas-liste-{suffix}");
            scene.Script("document.querySelector('.tableau thead th.case input').click()");
            Expect(scene, "document.querySelector('.barre-selection') === null");
            scene.Script("document.querySelector('tr[data-id=\"" + IdOf(scene, "SAMES Meylan") + "\"] button.nom').click()");
            Expect(scene, "document.querySelector('.grand-apercu svg') !== null");
            scene.Script("document.querySelector('.entete-lecture .bouton.discret').click()");

            // De retour du détail : toujours en liste.
            Expect(scene, "document.querySelector('.tableau') !== null");
        }
        finally
        {
            scene.Script($"localStorage.removeItem('{Affichage}')");
        }
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void Several_schemas_are_chosen_and_handled_together_from_the_bar_at_the_bottom(ThemeKind theme)
    {
        using var scene = Accueil(theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        string Chosen() => TextOf(scene, "[...document.querySelectorAll('li.carte.choisie .nom')].map((n) => n.textContent.trim()).join('|')");

        // Ctrl+clic ajoute, Maj+clic prend la suite ; la barre le dit, par-dessus la liste.
        Press(scene, CardOf(scene, "Salle serveurs B012") + " .apercu", modifiers: 2);
        Expect(scene, "document.querySelector('.barre-selection') !== null");
        Assert.AreEqual("Salle serveurs B012", Chosen());
        Assert.AreEqual("true", scene.Script("document.querySelector('.grand-apercu') === null"), "Ctrl+clic n'ouvre pas le détail");
        Press(scene, CardOf(scene, "Labo réseau (TP 3)") + " .apercu", modifiers: 8);
        Expect(scene, "document.querySelectorAll('li.carte.choisie').length === 3");
        Assert.AreEqual("Salle serveurs B012|Site distant Grenoble|Labo réseau (TP 3)", Chosen());
        Assert.AreEqual("3 schémas choisis|Réunir en un schéma…|Ajouter à un schéma…|Étiqueter…|Exporter…|Archiver|Supprimer…|", TextOf(scene, "[...document.querySelectorAll('.barre-selection > *')].map((e) => e.textContent.trim()).join('|')"));
        Expect(scene, "[...document.querySelectorAll('li.carte .case')].every((c) => getComputedStyle(c).opacity === '1')");
        scene.Mouse("mouseMoved", 5, 5);
        Expect(scene, "document.getAnimations().length === 0");
        scene.Picture($"schemas-selection-{suffix}");

        // La case de la carte : elle retire ; Ctrl+A prend tout ce qui est affiché, Échap lâche.
        scene.Script("document.querySelector('" + CardOf(scene, "Site distant Grenoble") + " .case').click()");
        Expect(scene, "document.querySelectorAll('li.carte.choisie').length === 2");
        Key(scene, "a", "KeyA", 0x41, modifiers: 2);
        Expect(scene, "document.querySelectorAll('li.carte.choisie').length === 5");
        Key(scene, "Escape", "Escape", 0x1B);
        Expect(scene, "document.querySelector('.barre-selection') === null");

        // Le clic droit sur l'un des schémas choisis : le menu de la sélection.
        scene.Script("document.querySelector('" + CardOf(scene, "SAMES Meylan") + " .case').click()");
        scene.Script("document.querySelector('" + CardOf(scene, "Site distant Grenoble") + " .case').click()");
        Expect(scene, "document.querySelectorAll('li.carte.choisie').length === 2");
        scene.Script("document.querySelector('" + CardOf(scene, "SAMES Meylan") + "').dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 80, clientY: 80 }))");
        Expect(scene, "document.querySelector('.menu[role=menu]') !== null");
        Assert.AreEqual("Réunir en un schéma…|Ajouter à un schéma…|Étiqueter…|Exporter…|Archiver|Supprimer…", TextOf(scene, "[...document.querySelectorAll('.menu [role=menuitem]')].map((b) => b.textContent.trim()).join('|')"));
        Key(scene, "Escape", "Escape", 0x1B);
        Expect(scene, "document.querySelector('.menu[role=menu]') === null");

        // Étiqueter… les deux : « Grenoble » s'ajoute à chacun, « Client » part des deux.
        scene.Script("[...document.querySelectorAll('.barre-selection button')].find((b) => b.textContent.includes('Étiqueter')).click()");
        Expect(scene, "document.querySelector('dialog[open] h2')?.textContent === 'Étiqueter 2 schémas'");
        Fill(scene, "dialog[open] input.champ", "Isère");
        scene.Script("document.querySelector('dialog[open] input.champ').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }))");
        Expect(scene, "[...document.querySelectorAll('dialog[open] .posees .puce')].some((p) => p.textContent.includes('Isère'))");
        scene.Script("document.querySelector('dialog[open] button[aria-label=\"Retirer « Client »\"]').click()");
        scene.Script("document.querySelector('dialog[open] form').requestSubmit()");
        UiThread.Until(() => Written(scene, "SAMES Meylan").Contains("Isère", StringComparison.Ordinal) && Written(scene, "Site distant Grenoble").Contains("Isère", StringComparison.Ordinal), TimeSpan.FromSeconds(20));
        Assert.AreEqual("Isère", string.Join('|', JsonNode.Parse(Written(scene, "SAMES Meylan"))!["etiquettes"]!.AsArray().Select(e => e!.GetValue<string>())));
        Expect(scene, "[...document.querySelectorAll('.outils .puce')].some((p) => p.textContent.includes('Isère'))");

        // Exporter… : un fichier par schéma, dans le dossier choisi une fois.
        Expect(scene, "document.querySelectorAll('li.carte.choisie').length === 2");
        var folder = scene.Outside("Plusieurs");
        Directory.CreateDirectory(folder);
        scene.Picker.Answers.Enqueue(folder);
        // L'étiquetage fini d'écrire : la barre n'est plus occupée.
        Expect(scene, "[...document.querySelectorAll('.barre-selection button')].find((b) => b.textContent.includes('Exporter'))?.disabled === false");
        scene.Script("[...document.querySelectorAll('.barre-selection button')].find((b) => b.textContent.includes('Exporter')).click()");
        Expect(scene, "document.querySelector('dialog[open] h2')?.textContent === 'Exporter 2 schémas'");
        scene.Script("(() => { [...document.querySelectorAll('dialog[open] input[type=radio]')].find((c) => c.value === 'json').click(); document.querySelector('dialog[open] form').requestSubmit(); })()");
        UiThread.Until(() => Directory.GetFiles(folder, "*.json").Length == 2, TimeSpan.FromSeconds(30));
        var files = string.Join('|', Directory.GetFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        StringAssert.Contains(files, "SAMES Meylan", StringComparison.Ordinal);
        StringAssert.Contains(files, "Site distant Grenoble", StringComparison.Ordinal);
        Expect(scene, "document.querySelector('.annonces [role=status]').textContent.includes('2 fichiers écrits')");

        // « Réunir en un schéma… » et « Ajouter à un schéma… » ouvrent leur fenêtre.
        scene.Script("[...document.querySelectorAll('.barre-selection button')].find((b) => b.textContent.includes('Réunir')).click()");
        Expect(scene, "document.querySelector('dialog[open] h2')?.textContent.includes('Réunir') === true");
        scene.Script("document.querySelector('dialog[open]').close()");
        scene.Script("[...document.querySelectorAll('.barre-selection button')].find((b) => b.textContent.includes('Ajouter')).click()");
        Expect(scene, "document.querySelector('dialog[open] h2')?.textContent.includes('Ajouter') === true");
        scene.Script("document.querySelector('dialog[open]').close()");
        Expect(scene, "document.querySelector('dialog[open]') === null");

        // Archiver : les deux quittent la liste, sans question ; ils sont aux archives.
        scene.Script("[...document.querySelectorAll('.barre-selection button')].find((b) => b.textContent.trim() === 'Archiver').click()");
        Expect(scene, "document.querySelectorAll('li.carte').length === 3 && document.querySelector('.barre-selection') === null");
        Assert.AreEqual(3, scene.Schemas.Schemas().Value.Count);
        Expect(scene, "document.querySelector('.entete-accueil .resume').textContent.includes('2 archivés')");

        // Supprimer… : une question d'abord, puis la corbeille.
        Press(scene, CardOf(scene, "Labo réseau (TP 3)") + " .apercu", modifiers: 2);
        Press(scene, CardOf(scene, "Salle serveurs B012") + " .apercu", modifiers: 2);
        Expect(scene, "document.querySelectorAll('li.carte.choisie').length === 2");
        scene.Script("[...document.querySelectorAll('.barre-selection button')].find((b) => b.textContent.includes('Supprimer')).click()");
        Expect(scene, "document.querySelector('dialog[open] h2')?.textContent === 'Supprimer 2 schémas ?'");
        Expect(scene, "document.getAnimations().length === 0");
        scene.Picture($"schemas-selection-supprimer-{suffix}");
        scene.Script("[...document.querySelectorAll('dialog[open] button')].find((b) => b.textContent.trim() === 'Supprimer').click()");
        Expect(scene, "document.querySelectorAll('li.carte').length === 1");
        Assert.AreEqual(1, scene.Schemas.Schemas().Value.Count);
        Expect(scene, "document.body.innerText.includes('Corbeille')");
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void A_schema_is_pinned_labelled_and_renamed_from_its_card(ThemeKind theme)
    {
        using var scene = Accueil(theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";

        // Épingler : la carte passe en tête, et le schéma le garde ; désépingler retire la clé.
        CardMenu(scene, "Site distant Grenoble", "Épingler");
        UiThread.Until(() => Written(scene, "Site distant Grenoble").Contains("epingle", StringComparison.Ordinal), TimeSpan.FromSeconds(20));
        Expect(scene, "document.querySelector('ul.grille:first-of-type')?.textContent.includes('Site distant Grenoble') === true");
        StringAssert.Contains(Written(scene, "Site distant Grenoble"), "\"epingle\": true", StringComparison.Ordinal);
        Assert.AreEqual(1, Count(scene, CardOf(scene, "Site distant Grenoble") + " .epingle"));
        CardMenu(scene, "Site distant Grenoble", "Désépingler");
        UiThread.Until(() => !Written(scene, "Site distant Grenoble").Contains("epingle", StringComparison.Ordinal), TimeSpan.FromSeconds(20));
        Expect(scene, "document.querySelector('ul.grille:first-of-type')?.textContent.includes('Site distant Grenoble') === false");

        // Étiqueter… : celles en usage se proposent ; une nouvelle s'ajoute, une ancienne se retire.
        CardMenu(scene, "Salle serveurs B012", "Étiqueter");
        Expect(scene, "document.querySelector('dialog[open] h2')?.textContent === 'Étiqueter « Salle serveurs B012 »'");
        Assert.AreEqual("Client|Lycée|TP", TextOf(scene, "[...document.querySelectorAll('dialog[open] .suggestions button')].map((b) => b.textContent.trim()).join('|')"));
        Fill(scene, "dialog[open] input.champ", "cli");
        Expect(scene, "[...document.querySelectorAll('dialog[open] .suggestions button')].map((b) => b.textContent.trim()).join('|') === 'Client'");
        scene.Script("document.querySelector('dialog[open] .suggestions button').click()");
        Fill(scene, "dialog[open] input.champ", "  Salle   B012 ");
        scene.Script("document.querySelector('dialog[open] input.champ').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }))");
        Expect(scene, "document.querySelectorAll('dialog[open] .posees .puce').length === 2");
        Expect(scene, "document.getAnimations().length === 0");
        scene.Picture($"schemas-etiqueter-{suffix}");
        scene.Script("document.querySelector('dialog[open] form').requestSubmit()");
        UiThread.Until(() => Written(scene, "Salle serveurs B012").Contains("Salle B012", StringComparison.Ordinal), TimeSpan.FromSeconds(20));
        Assert.AreEqual("Client|Salle B012", string.Join('|', JsonNode.Parse(Written(scene, "Salle serveurs B012"))!["etiquettes"]!.AsArray().Select(e => e!.GetValue<string>())));
        Expect(scene, "document.querySelector('" + CardOf(scene, "Salle serveurs B012") + " .petites')?.textContent.includes('Salle B012') === true");
        CardMenu(scene, "Salle serveurs B012", "Étiqueter");
        Expect(scene, "document.querySelectorAll('dialog[open] .posees .puce').length === 2");
        scene.Script("document.querySelector('dialog[open] button[aria-label=\"Retirer « Salle B012 »\"]').click()");
        scene.Script("document.querySelector('dialog[open] button[aria-label=\"Retirer « Client »\"]').click()");
        scene.Script("document.querySelector('dialog[open] form').requestSubmit()");
        UiThread.Until(() => !Written(scene, "Salle serveurs B012").Contains("etiquettes", StringComparison.Ordinal), TimeSpan.FromSeconds(20));

        // Renommer au double clic sur le nom : Entrée garde.
        var id = IdOf(scene, "Labo réseau (TP 3)");
        var name = $"li.carte[data-id=\"{id}\"] .nom";
        DoubleClick(scene, name);
        Expect(scene, $"document.activeElement?.matches('li.carte[data-id=\"{id}\"] input.renommer') === true");
        Assert.AreEqual("true", scene.Script("document.querySelector('.grand-apercu') === null"), "le double clic n'ouvre pas le détail");
        scene.Script($"document.querySelector('li.carte[data-id=\"{id}\"] input.renommer').value = 'Labo réseau (TP 4)'");
        Expect(scene, "document.getAnimations().length === 0");
        scene.Picture($"schemas-renommer-{suffix}");
        Key(scene, "Enter", "Enter", 0x0D);
        UiThread.Until(() => scene.Schemas.Read(id).Value.Text.Contains("Labo réseau (TP 4)", StringComparison.Ordinal), TimeSpan.FromSeconds(20));
        Expect(scene, $"document.querySelector('li.carte[data-id=\"{id}\"] .nom')?.textContent === 'Labo réseau (TP 4)'");

        // F2 sur la vignette, puis Échap : rien ne change.
        scene.Script($"document.querySelector('li.carte[data-id=\"{id}\"] .apercu').focus()");
        Key(scene, "F2", "F2", 0x71);
        Expect(scene, $"document.activeElement?.matches('li.carte[data-id=\"{id}\"] input.renommer') === true");
        scene.Script($"document.querySelector('li.carte[data-id=\"{id}\"] input.renommer').value = 'Autre nom'");
        Key(scene, "Escape", "Escape", 0x1B);
        Expect(scene, $"document.querySelector('li.carte[data-id=\"{id}\"] input.renommer') === null");
        StringAssert.Contains(scene.Schemas.Read(id).Value.Text, "Labo réseau (TP 4)", StringComparison.Ordinal);

        // Un clic simple sur le nom ouvre le détail, un instant plus tard.
        Press(scene, name);
        Expect(scene, "document.querySelector('.grand-apercu svg') !== null");
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void The_home_fits_a_narrow_window_and_a_very_wide_one(ThemeKind theme)
    {
        using var scene = Accueil(theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        scene.Schemas.Create(Petit("Maquette VLAN", "\"TP\"")).Value.ToString();
        scene.Script("document.dispatchEvent(new Event('schemas-revenus'))");
        Expect(scene, "document.querySelectorAll('li.carte').length === 6");
        int Columns() => int.Parse(scene.Script("getComputedStyle(document.querySelector('ul.grille')).gridTemplateColumns.split(' ').length"), CultureInfo.InvariantCulture);

        // À 470 pixels : deux cartes par ligne, « Importer » sous « ⋯ », les puces utiles et « Filtres ».
        Narrow(scene);
        Expect(scene, "document.querySelector('.entete-accueil button[aria-label^=\"Plus d\\'actions\"]') !== null");
        Assert.AreEqual(2, Columns());
        // L'en-tête suit la largeur qu'il mesure, un peu après la grille : on l'attend.
        Expect(scene, "![...document.querySelectorAll('.entete-accueil button')].some((b) => b.textContent.trim() === 'Importer…')");
        Assert.AreEqual("Client 2|Lycée 2|TP 2|Filtres", Chips(scene));
        Assert.AreEqual("true", scene.Script("[...document.querySelectorAll('li.carte .meta')].every((m) => m.scrollHeight <= 20)"), "une seule ligne d'infos");
        NoSideScroll(scene, "à 470");
        Expect(scene, "document.getAnimations().length === 0");
        scene.Picture($"schemas-accueil-470-{suffix}");

        // « ⋯ » de l'en-tête : Importer… ; « Filtres » : À vérifier, le tri et l'affichage.
        scene.Script("document.querySelector('.entete-accueil button[aria-haspopup=menu]:not(.principal)').click()");
        Expect(scene, "document.querySelector('.menu[role=menu]')?.textContent.includes('Importer') === true");
        Key(scene, "Escape", "Escape", 0x1B);
        Expect(scene, "document.querySelector('.menu[role=menu]') === null");
        scene.Script("[...document.querySelectorAll('.outils .puce')].find((p) => p.textContent.includes('Filtres')).click()");
        Expect(scene, "document.querySelector('.menu[role=menu]') !== null");
        Assert.AreEqual("À vérifier 1|Modifiés récemment|Nom|Taille|Grille|Liste", TextOf(scene, "[...document.querySelectorAll('.menu [role^=menuitem]')].map((b) => b.textContent.replace(/[✓•]/g, '').trim().replace(/\\s+/g, ' ')).join('|')"));
        Assert.AreEqual("true", scene.Script("document.querySelector('.menu [role=menuitemradio][aria-checked=true]')?.textContent.includes('Modifiés') === true"));
        Assert.AreEqual("true", scene.Script("(() => { const m = document.querySelector('.menu[role=menu]').getBoundingClientRect(); return m.left >= 0 && m.right <= innerWidth && m.bottom <= innerHeight; })()"));
        Expect(scene, "document.getAnimations().length === 0");
        scene.Picture($"schemas-accueil-470-filtres-{suffix}");
        scene.Script("[...document.querySelectorAll('.menu [role^=menuitem]')].find((b) => b.textContent.includes('À vérifier')).click()");
        Expect(scene, "document.querySelectorAll('li.carte').length === 1");
        Assert.AreEqual("true", scene.Script("[...document.querySelectorAll('.outils .puce')].find((p) => p.textContent.includes('Filtres')).classList.contains('active')"));
        scene.Script("[...document.querySelectorAll('.outils .puce')].find((p) => p.textContent.includes('Filtres')).click()");
        Expect(scene, "document.querySelector('.menu[role=menu]') !== null");
        scene.Script("[...document.querySelectorAll('.menu [role^=menuitem]')].find((b) => b.textContent.includes('À vérifier')).click()");
        Expect(scene, "document.querySelectorAll('li.carte').length === 6");

        // Deux choisis : la barre tient dans la fenêtre, passe à la ligne s'il le faut, et garde Réunir et Ajouter à… en premier.
        scene.Script("document.querySelector('" + CardOf(scene, "Réseau du lycée") + " .case').click()");
        scene.Script("document.querySelector('" + CardOf(scene, "SAMES Meylan") + " .case').click()");
        Expect(scene, "document.querySelector('.barre-selection') !== null");
        Assert.AreEqual("2 choisis|Réunir…|Ajouter à…||", TextOf(scene, "[...document.querySelectorAll('.barre-selection > *')].map((e) => e.textContent.trim()).join('|')"));
        Assert.AreEqual("true", scene.Script("(() => { const b = document.querySelector('.barre-selection').getBoundingClientRect(); return b.left >= 0 && b.right <= innerWidth; })()"));
        Expect(scene, "document.getAnimations().length === 0");
        scene.Picture($"schemas-accueil-470-selection-{suffix}");
        scene.Script("document.querySelector('.barre-selection button[aria-haspopup=menu]').click()");
        Expect(scene, "document.querySelector('.menu[role=menu]') !== null");
        Assert.AreEqual("Étiqueter…|Exporter…|Archiver|Supprimer…", TextOf(scene, "[...document.querySelectorAll('.menu [role=menuitem]')].map((b) => b.textContent.trim()).join('|')"));
        // Mesuré une fois ouvert : pendant son animation, le menu glisse encore vers sa place.
        Expect(scene, "document.getAnimations().length === 0");
        Assert.AreEqual("true", scene.Script("(() => { const m = document.querySelector('.menu[role=menu]').getBoundingClientRect(), b = document.querySelector('.barre-selection').getBoundingClientRect(); return m.bottom <= b.top + 1 && m.top >= 0; })()"), "le menu s'ouvre au-dessus de la barre");
        Key(scene, "Escape", "Escape", 0x1B);
        Key(scene, "Escape", "Escape", 0x1B);
        Expect(scene, "document.querySelector('.barre-selection') === null");

        // Très large : six cartes par ligne, au plus.
        Resize(scene, 2560);
        Expect(scene, "getComputedStyle(document.querySelector('ul.grille')).gridTemplateColumns.split(' ').length > 2");
        Expect(scene, "[...document.querySelectorAll('.entete-accueil button')].some((b) => b.textContent.trim() === 'Importer…')");
        Assert.IsLessThanOrEqualTo(6, Columns());
        Assert.IsGreaterThanOrEqualTo(5, Columns(), "la place est prise");
        NoSideScroll(scene, "à 2560");
        Expect(scene, "document.getAnimations().length === 0");
        scene.Picture($"schemas-accueil-2560-{suffix}");
    }
}
