using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Réunir des schémas (31C) et échanger des pages d'un schéma à l'autre (31D), dans l'éditeur : réunir le lycée avec deux
/// autres, ajouter des pages d'un autre schéma puis les retirer par Ctrl+Z, déplacer une page vers un autre schéma, faire
/// un nouveau schéma d'une page. L'accueil ne les ouvre pas encore : on y passe par l'éditeur.
/// </summary>
public sealed partial class SchemasPageTests
{
    /// <summary>La salle serveurs, d'une page : le même cœur que le lycée (même nom, même adresse), un serveur ; VLAN 20 « Invités ».</summary>
    private const string SalleServeurs = """
        {"format": 2, "titre": "Salle serveurs B012", "vue": "physique",
         "vlans": [{"id": "v_20", "numero": 20, "nom": "Invités", "sousReseaux": []}],
         "equipements": [
          {"id": "d_coeur", "type": "switch-coeur", "nom": "Cœur", "position": {"x": 0, "y": 0}, "gestion": {"adresse": "10.0.0.1/24"},
           "interfaces": [{"id": "i_c9", "nom": "1/9", "vlan": {"mode": "acces", "acces": 20}}]},
          {"id": "d_baie", "type": "serveur", "nom": "Serveur B012", "position": {"x": 240, "y": 0}, "interfaces": [{"id": "i_e", "nom": "eth0"}]}],
         "liens": [{"id": "l_s", "a": {"equipement": "d_coeur", "interface": "i_c9"}, "b": {"equipement": "d_baie", "interface": "i_e"}, "support": "cuivre", "points": []}]}
        """;

    /// <summary>Une maquette en deux pages (bâtiments A et B) ; VLAN 20 « Wi-Fi ».</summary>
    private const string MaquetteVlan = """
        {"format": 3, "titre": "Maquette VLAN", "vue": "physique",
         "pages": [{"id": "p_a", "nom": "Bâtiment A"}, {"id": "p_b", "nom": "Bâtiment B"}],
         "vlans": [{"id": "v_20", "numero": 20, "nom": "Wi-Fi", "sousReseaux": []}],
         "equipements": [
          {"id": "d_a", "page": "p_a", "type": "switch", "nom": "SW-A", "position": {"x": 0, "y": 0}, "interfaces": [{"id": "i_a", "nom": "1/1", "vlan": {"mode": "acces", "acces": 20}}]},
          {"id": "d_b", "page": "p_b", "type": "switch", "nom": "SW-B", "position": {"x": 0, "y": 0}}]}
        """;

    /// <summary>Ouvre l'éditeur du schéma de ce titre, en mode avancé.</summary>
    private static void OpenEditorOf(IslandScene scene, string title)
    {
        var card = "[...document.querySelectorAll('li.carte')].find((c) => c.textContent.includes(" + JsonSerializer.Serialize(title) + "))";
        scene.WaitFor("[...((" + card + ")?.querySelectorAll('button') ?? [])].some((b) => b.textContent.includes('Modifier'))");
        scene.Script("[...(" + card + ").querySelectorAll('button')].find((b) => b.textContent.includes('Modifier')).click()");
        scene.WaitFor("document.querySelector('dialog[open] svg.canevas') !== null");
        scene.Script("document.querySelector('.segments.mode button[data-mode=\"avance\"]').click()");
        scene.WaitFor("document.querySelector('.segments.mode button[data-mode=\"avance\"]').getAttribute('aria-pressed') === 'true'");
    }

    /// <summary>Le texte des éléments de <paramref name="selector"/>, joints par « | ».</summary>
    private static string Texts(IslandScene scene, string selector) =>
        TextOf(scene, "[...document.querySelectorAll('" + selector + "')].map((e) => e.textContent.trim()).join('|')");

    /// <summary>Le bouton de <paramref name="dialog"/> dont le texte est <paramref name="text"/>, cliqué.</summary>
    private static void DialogButton(IslandScene scene, string dialog, string text) =>
        scene.Script("[...document.querySelectorAll('" + dialog + " button')].find((b) => b.textContent.trim() === " + JsonSerializer.Serialize(text) + ").click()");

    /// <summary>La case (ou le bouton radio) de la ligne de <paramref name="dialog"/> qui contient <paramref name="text"/>, cochée.</summary>
    private static void Tick(IslandScene scene, string dialog, string row, string text) =>
        scene.Script("[...document.querySelectorAll('" + dialog + " " + row + "')].find((l) => l.textContent.includes(" + JsonSerializer.Serialize(text) + ")).querySelector('input').click()");

    /// <summary>Attend <paramref name="condition"/> ; sinon, l'échec dit ce que montre <paramref name="what"/>, pour qu'on le voie sur la CI.</summary>
    private static void WaitOrTell(IslandScene scene, string condition, string what)
    {
        try
        {
            scene.WaitFor(condition);
        }
        catch (AssertFailedException exception)
        {
            throw new AssertFailedException($"attendu : {condition} ; on voit : {TextOf(scene, what)}", exception);
        }
    }

    /// <summary>La fenêtre tient dans la page, à 16 pixels au moins de chaque bord, sans défilement de côté.</summary>
    private static void FitsTheWindow(IslandScene scene, string dialog)
    {
        Assert.AreEqual("true", scene.Script("(() => { const r = document.querySelector('" + dialog + "').getBoundingClientRect(); return r.left >= 15.5 && r.right <= innerWidth - 15.5 && r.width >= innerWidth - 33; })()"), "la fenêtre prend la largeur moins 16 pixels de chaque côté");
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"), "Pas de défilement horizontal.");
        Assert.AreEqual("0", scene.Script("(() => { const d = document.querySelector('" + dialog + "'); return d.scrollWidth - d.clientWidth; })()"), "Rien ne déborde de la fenêtre.");
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void Three_schemas_are_joined_into_a_new_one_and_the_originals_are_archived(ThemeKind theme)
    {
        using var scene = new IslandScene(Lycee, theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        var salle = scene.Schemas.Create(SalleServeurs).Value.Id;
        var maquette = scene.Schemas.Create(MaquetteVlan).Value.Id;
        const string dialog = "dialog.dialogue-reunir[open]";
        OpenEditorOf(scene, "Réseau du lycée");

        // « Plus », « Réunir avec d'autres schémas… » : le lycée est déjà choisi ; on coche la salle, puis la maquette.
        scene.Script("document.querySelector('.ancre.plus > button').click()");
        scene.WaitFor("document.querySelector('.menu[role=menu]') !== null");
        MenuEntry(scene, "Réunir avec d'autres schémas…");
        scene.WaitFor($"document.querySelectorAll('{dialog} .case-schema').length === 3");
        Assert.AreEqual("true", scene.Script($"[...document.querySelectorAll('{dialog} .case-schema')].find((l) => l.textContent.includes('Réseau du lycée')).querySelector('input').checked"));
        Tick(scene, dialog, ".case-schema", "Salle serveurs B012");
        Tick(scene, dialog, ".case-schema", "Maquette VLAN");
        DialogButton(scene, dialog, "Continuer avec 3 schémas");

        // Le nom proposé, l'ordre des pages, ce qui se retrouve des deux côtés, l'avertissement du VLAN 20.
        WaitOrTell(scene, $"document.querySelector('{dialog} #nom-reunion')?.value === 'Réseau du lycée — complet'", $"document.querySelector('{dialog}')?.textContent ?? 'pas de fenêtre'");
        Assert.AreEqual("Réunir 3 schémas en un", TextOf(scene, $"document.querySelector('{dialog} h2').textContent"));
        Assert.AreEqual("Réseau du lycée|Salle serveurs B012|Maquette VLAN", Texts(scene, $"{dialog} .ligne .nom"));
        Assert.AreEqual("4 pages, gardées|1 page|2 pages, gardées", Texts(scene, $"{dialog} .ligne .compte"));
        Assert.AreEqual("Étage 1 · Étage 2 · Salle serveurs · Site distant|Bâtiment A · Bâtiment B", Texts(scene, $"{dialog} .sous"));
        StringAssert.Contains(Texts(scene, $"{dialog} .choix"), "« Cœur » existe dans plusieurs schémas : deux équipements, un sur chaque page.", StringComparison.Ordinal);
        Assert.AreEqual("VLAN 20 : « Invités » d'un côté, « Wi-Fi » de l'autre. Le nom du premier schéma est gardé.", Texts(scene, $"{dialog} .avertissements li"));
        Assert.AreEqual("true", scene.Script($"document.querySelector('{dialog} .case input').checked"), "archiver est coché par défaut");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-reunir-{suffix}");

        // Au clavier : Alt+flèche bas descend le lycée d'un rang, il garde la main ; Alt+flèche haut le remonte.
        scene.Script($"document.querySelector('{dialog} .ligne').focus()");
        scene.Script("document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', altKey: true, bubbles: true, cancelable: true }))");
        scene.WaitFor($"document.querySelector('{dialog} .ligne .nom').textContent === 'Salle serveurs B012'");
        WaitForFocus(scene, $"{dialog} .ligne[data-schema=\"" + scene.Schemas.Schemas().Value.Single(s => s.Title == "Réseau du lycée").Id + "\"]");
        StringAssert.Contains(TextOf(scene, $"document.querySelector('{dialog} [role=status]').textContent"), "« Réseau du lycée » en position 2 sur 3.", StringComparison.Ordinal);
        Assert.AreEqual("VLAN 20 : « Invités » d'un côté, « Wi-Fi » de l'autre. Le nom du premier schéma est gardé.", Texts(scene, $"{dialog} .avertissements li"));
        scene.Script("document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', altKey: true, bubbles: true, cancelable: true }))");
        scene.WaitFor($"document.querySelector('{dialog} .ligne .nom').textContent === 'Réseau du lycée'");

        // À la souris : la maquette, glissée au-dessus de la salle.
        var from = GripOf(scene, $"{dialog} .ligne[data-schema=\"{maquette}\"] .poignee");
        var to = GripOf(scene, $"{dialog} .ligne[data-schema=\"{salle}\"] .nom");
        scene.Drag(from[0], from[1], to[0], to[1] - 8);
        scene.WaitFor($"[...document.querySelectorAll('{dialog} .ligne .nom')].map((n) => n.textContent).join('|') === 'Réseau du lycée|Maquette VLAN|Salle serveurs B012'");

        // Fenêtre étroite : la fenêtre prend la largeur moins 16 pixels de chaque côté.
        Narrow(scene);
        FitsTheWindow(scene, dialog);
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-reunir-etroit-{suffix}");

        // Un seul cœur ; « Réunir » : le nouveau schéma s'ouvre, les trois d'origine sont archivés.
        Tick(scene, dialog, ".choix", "Réunir ceux qui ont le même nom et la même adresse");
        DialogButton(scene, dialog, "Réunir");
        scene.WaitFor("document.querySelector('dialog.dialogue-reunir') === null && document.querySelectorAll('.pages .onglet').length === 7");
        Assert.AreEqual("Étage 1*|Étage 2|Salle serveurs|Site distant|Bâtiment A|Bâtiment B|Salle serveurs B012", Tabs(scene));
        UiThread.Until(() => scene.Schemas.Aside().Value.Count == 3, TimeSpan.FromSeconds(20));
        var nouveau = scene.Schemas.Schemas().Value.Single();
        Assert.AreEqual("Réseau du lycée — complet", nouveau.Title);
        var saved = JsonNode.Parse(scene.Schemas.Read(nouveau.Id).Value.Text)!;
        Assert.AreEqual(3, saved["format"]!.GetValue<int>());
        Assert.AreEqual(1, saved["equipements"]!.AsArray().Count(e => e!["nom"]!.GetValue<string>() == "Cœur"));
        Assert.AreEqual("Wi-Fi", saved["vlans"]!.AsArray().Single()!["nom"]!.GetValue<string>(), "le nom du premier venu : la maquette, montée au-dessus de la salle");
        Assert.IsFalse(saved["equipements"]!.AsArray().Any(e => e!["id"]!.GetValue<string>() == "d_coeur"), "tous les identifiants sont neufs");
        // Le câble du cœur vers le serveur de la salle passe d'une page à l'autre : un renvoi le montre.
        scene.Script("document.querySelectorAll('.pages .onglet')[6].click()");
        scene.WaitFor("[...document.querySelectorAll('svg.canevas g.renvoi text')].some((t) => t.textContent.startsWith('→ Étage 1 · Cœur · 1/9'))");
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void Pages_of_another_schema_come_into_the_open_editor_and_ctrl_z_takes_them_back(ThemeKind theme)
    {
        using var scene = new IslandScene(Lycee, theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        var maquette = scene.Schemas.Create(MaquetteVlan).Value.Id;
        var lycee = scene.Schemas.Schemas().Value.Single(s => s.Title == "Réseau du lycée").Id;
        JsonNode Saved(string id) => JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!;
        const string dialog = "dialog.dialogue-ajouter-pages[open]";
        OpenEditorOf(scene, "Réseau du lycée");

        // « + » sous le plan : un petit menu, une nouvelle page ou des pages d'un autre schéma.
        scene.Script("document.querySelector('.pages .plus').click()");
        scene.WaitFor("document.querySelector('.menu[role=menu]') !== null");
        Assert.AreEqual("Nouvelle page|Pages d'un autre schéma…|Pages depuis un modèle…", Texts(scene, ".menu[role=menu] button"));
        WaitForFocus(scene, ".menu[role=menu] button.premier");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-pages-plus-{suffix}");
        MenuEntry(scene, "Pages d'un autre schéma…");

        // La maquette, seul autre schéma ; ses deux pages en vignettes, cochées ; on décoche le bâtiment B.
        scene.WaitFor($"document.querySelectorAll('{dialog} .vignette').length === 2");
        Assert.AreEqual("Ajouter des pages d'un autre schéma", TextOf(scene, $"document.querySelector('{dialog} h2').textContent").Trim());
        Assert.AreEqual(maquette, TextOf(scene, $"document.querySelector('{dialog} select.source').value"));
        Assert.AreEqual(2, int.Parse(scene.Script($"document.querySelectorAll('{dialog} svg').length"), System.Globalization.CultureInfo.InvariantCulture), "chaque page a sa vignette");
        Tick(scene, dialog, ".vignette", "Bâtiment B");
        scene.WaitFor($"[...document.querySelectorAll('{dialog} button')].some((b) => b.textContent.trim() === 'Ajouter 1 page')");
        StringAssert.Contains(Texts(scene, $"{dialog} .choix"), "« Maquette VLAN » ne change pas.", StringComparison.Ordinal);
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-ajouter-pages-{suffix}");

        // Fenêtre étroite : elle tient.
        var width = scene.Page.Width;
        Narrow(scene);
        FitsTheWindow(scene, dialog);
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-ajouter-pages-etroit-{suffix}");
        scene.Page.Width = width;
        TestShell.Flush();
        UiThread.Until(() => scene.Script("document.documentElement.clientWidth") is not "470" and not "469", TimeSpan.FromSeconds(20));

        // « Ajouter 1 page » : le compte rendu ; le VLAN 20 de la maquette vient avec la page qui s'en sert.
        DialogButton(scene, dialog, "Ajouter 1 page");
        scene.WaitFor($"document.querySelector('{dialog} .compte-rendu') !== null");
        Assert.AreEqual("Page ajoutée dans « Réseau du lycée » : « Bâtiment A ».", Texts(scene, $"{dialog} .compte-rendu li"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-ajouter-pages-fait-{suffix}");
        DialogButton(scene, dialog, "Fermer");
        scene.WaitFor("document.querySelector('dialog.dialogue-ajouter-pages') === null");
        Assert.AreEqual("Étage 1|Étage 2|Salle serveurs|Site distant|Bâtiment A*", Tabs(scene));
        UiThread.Until(() => Saved(lycee)["pages"]!.AsArray().Count == 5, TimeSpan.FromSeconds(20));
        Assert.AreEqual(11, Saved(lycee)["equipements"]!.AsArray().Count);
        Assert.AreEqual("Wi-Fi", Saved(lycee)["vlans"]!.AsArray().Single()!["nom"]!.GetValue<string>());
        Assert.AreEqual(2, Saved(maquette)["pages"]!.AsArray().Count, "copier ne touche pas au schéma d'origine");

        // Ctrl+Z, le plan ayant repris la main : la page part en une fois.
        WaitForFocus(scene, "svg.canevas");
        Key(scene, "z", "KeyZ", 0x5A, modifiers: 2);
        scene.WaitFor("document.querySelectorAll('.pages .onglet').length === 4");
        UiThread.Until(() => Saved(lycee)["pages"]!.AsArray().Count == 4 && Saved(lycee)["equipements"]!.AsArray().Count == 10, TimeSpan.FromSeconds(20));
        Assert.AreEqual(0, Saved(lycee)["vlans"]?.AsArray().Count ?? 0);

        // « + », « Nouvelle page » : une page vide, comme avant.
        scene.Script("document.querySelector('.pages .plus').click()");
        scene.WaitFor("document.querySelector('.menu[role=menu]') !== null");
        MenuEntry(scene, "Nouvelle page");
        scene.WaitFor("document.querySelectorAll('.pages .onglet').length === 5");
        Assert.AreEqual("Page 5*", Tabs(scene).Split('|')[^1]);

        // « + », « Pages depuis un modèle… » : la fenêtre des modèles, sans nom à donner ; « Ajouter » pose la page du
        // modèle après les autres, et Ctrl+Z la retire.
        scene.Script("document.querySelector('.pages .plus').click()");
        scene.WaitFor("document.querySelector('.menu[role=menu]') !== null");
        MenuEntry(scene, "Pages depuis un modèle…");
        const string modeles = "dialog.dialogue-modeles[open]";
        scene.WaitFor($"document.querySelectorAll('{modeles} .carte').length >= 5");
        Assert.AreEqual("Ajouter les pages d'un modèle", TextOf(scene, $"document.querySelector('{modeles} h2').textContent").Trim());
        Assert.AreEqual(0, Count(scene, $"{modeles} input.champ"), "les pages gardent leur nom : rien à nommer");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-pages-modele-{suffix}");
        DialogButton(scene, modeles, "Ajouter");
        scene.WaitFor("document.querySelector('dialog.dialogue-modeles') === null");
        scene.WaitFor("document.querySelectorAll('.pages .onglet').length === 6");
        Assert.AreEqual("Petit bureau*", Tabs(scene).Split('|')[^1]);
        UiThread.Until(() => Saved(lycee)["pages"]!.AsArray().Count == 6 && Saved(lycee)["equipements"]!.AsArray().Count > 10, TimeSpan.FromSeconds(20));
        WaitForFocus(scene, "svg.canevas");
        Key(scene, "z", "KeyZ", 0x5A, modifiers: 2);
        scene.WaitFor("document.querySelectorAll('.pages .onglet').length === 5");
        UiThread.Until(() => Saved(lycee)["equipements"]!.AsArray().Count == 10, TimeSpan.FromSeconds(20));
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void A_page_moves_to_another_schema_and_another_becomes_a_new_schema(ThemeKind theme)
    {
        using var scene = new IslandScene(Lycee, theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        var salle = scene.Schemas.Create(SalleServeurs).Value.Id;
        var lycee = scene.Schemas.Schemas().Value.Single(s => s.Title == "Réseau du lycée").Id;
        JsonNode Saved(string id) => JsonNode.Parse(scene.Schemas.Read(id).Value.Text)!;
        const string dialog = "dialog.dialogue-envoyer-page[open]";
        OpenEditorOf(scene, "Réseau du lycée");

        // Clic droit sur « Étage 2 » : copier, déplacer vers un autre schéma, en faire un nouveau.
        TabMenu(scene, 1);
        StringAssert.Contains(Texts(scene, ".menu[role=menu] button"), "Déplacer à droite|Copier vers un autre schéma…|Déplacer vers un autre schéma…|Faire un nouveau schéma de cette page|Enregistrer cette page comme modèle…|Supprimer la page…", StringComparison.Ordinal);
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-onglet-echanges-{suffix}");
        MenuEntry(scene, "Déplacer vers un autre schéma…");

        // La fenêtre dit que le câble de la montée ne suivra pas ; la salle choisie, « Déplacer ».
        scene.WaitFor($"document.querySelectorAll('{dialog} .case-schema').length === 1");
        Assert.AreEqual("Déplacer « Étage 2 » vers un autre schéma", TextOf(scene, $"document.querySelector('{dialog} h2').textContent"));
        StringAssert.Contains(TextOf(scene, $"document.querySelector('{dialog} .aide-tete').textContent"), "1 câble la relie à d'autres pages : un câble ne va pas d'un schéma à l'autre, il sera retiré.", StringComparison.Ordinal);
        Tick(scene, dialog, ".case-schema", "Salle serveurs B012");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-envoyer-page-{suffix}");
        DialogButton(scene, dialog, "Déplacer");

        // L'étage 2 a quitté le lycée, avec son câble vers l'étage 1 ; la salle le reçoit, en deuxième page.
        scene.WaitFor("document.querySelector('dialog.dialogue-envoyer-page') === null && document.querySelectorAll('.pages .onglet').length === 3");
        Assert.AreEqual("Étage 1*|Salle serveurs|Site distant", Tabs(scene));
        StringAssert.Contains(TextOf(scene, Announce), "Page déplacée dans « Salle serveurs B012 » : « Étage 2 ».", StringComparison.Ordinal);
        StringAssert.Contains(TextOf(scene, Announce), "1 câble allait vers une page restée dans « Réseau du lycée » : il est retiré", StringComparison.Ordinal);
        var recue = Saved(salle);
        Assert.AreEqual("Page 1|Étage 2", string.Join('|', recue["pages"]!.AsArray().Select(p => p!["nom"]!.GetValue<string>())));
        Assert.AreEqual(4, recue["equipements"]!.AsArray().Count);
        Assert.AreEqual(2, recue["liens"]!.AsArray().Count);
        UiThread.Until(() => Saved(lycee)["equipements"]!.AsArray().Count == 8, TimeSpan.FromSeconds(20));
        Assert.IsFalse(Saved(lycee)["liens"]!.AsArray().Any(l => l!["id"]!.GetValue<string>() == "l_montee"));

        // Ctrl+Z la rend ici, câble compris ; la salle garde sa copie.
        WaitForFocus(scene, "svg.canevas");
        Key(scene, "z", "KeyZ", 0x5A, modifiers: 2);
        scene.WaitFor("document.querySelectorAll('.pages .onglet').length === 4");
        UiThread.Until(() => Saved(lycee)["liens"]!.AsArray().Any(l => l!["id"]!.GetValue<string>() == "l_montee"), TimeSpan.FromSeconds(20));

        // « Faire un nouveau schéma de cette page » sur la salle serveurs : un schéma de plus, à son nom ; le lycée ne change pas.
        TabMenu(scene, 2);
        MenuEntry(scene, "Faire un nouveau schéma de cette page");
        UiThread.Until(() => scene.Schemas.Schemas().Value.Count == 3, TimeSpan.FromSeconds(20));
        var page = scene.Schemas.Schemas().Value.Single(s => s.Title == "Salle serveurs");
        var nouveau = Saved(page.Id);
        Assert.AreEqual(2, nouveau["format"]!.GetValue<int>());
        Assert.AreEqual("Serveur|NAS", string.Join('|', nouveau["equipements"]!.AsArray().Select(e => e!["nom"]!.GetValue<string>())));
        scene.WaitFor("(document.querySelector('.annonce')?.textContent ?? '').includes('Nouveau schéma « Salle serveurs » créé')");
        Assert.AreEqual(4, Saved(lycee)["pages"]!.AsArray().Count);

        // Fenêtre étroite : la fenêtre « Copier vers… » tient.
        Narrow(scene);
        scene.Script("document.querySelector('.pages .choix').click()");
        scene.WaitFor("document.querySelector('.menu[role=menu]') !== null");
        MenuEntry(scene, "Copier vers un autre schéma…");
        scene.WaitFor($"document.querySelectorAll('{dialog} .case-schema').length === 2");
        Assert.AreEqual("Copier « Étage 1 » vers un autre schéma", TextOf(scene, $"document.querySelector('{dialog} h2').textContent"));
        FitsTheWindow(scene, dialog);
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-envoyer-page-etroit-{suffix}");
        DialogButton(scene, dialog, "Annuler");
        scene.WaitFor("document.querySelector('dialog.dialogue-envoyer-page') === null");
    }
}
