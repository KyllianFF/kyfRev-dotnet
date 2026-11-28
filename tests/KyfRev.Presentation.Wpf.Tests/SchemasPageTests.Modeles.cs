using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La 31E dans l'éditeur : une page enregistrée comme modèle, un nouveau schéma tiré d'un modèle, et la recherche dans tous
/// les schémas (Ctrl+F, « Tous les schémas »). La vue Schémas les montre aussi ; ici, ce sont les mêmes composants.
/// </summary>
public sealed partial class SchemasPageTests
{
    /// <summary>Les équipements de la page « Étage 2 » du lycée.</summary>
    private static readonly string[] SecondFloor = ["SW-Étage2", "PC-Salle 201"];

    /// <summary>Un second schéma, à Meylan : une adresse qu'il partage avec le lycée, et un VLAN.</summary>
    private const string Meylan = """
        {"format": 2, "titre": "SAMES Meylan", "vue": "physique",
         "vlans": [{"id": "v_1", "numero": 30, "nom": "Bureaux", "sousReseaux": ["10.0.30.0/24"]}],
         "equipements": [
          {"id": "d_las", "type": "routeur", "nom": "LASOTEL 1", "position": {"x": 0, "y": 0}, "gestion": {"adresse": "10.0.0.11/24"},
           "interfaces": [{"id": "i_1", "nom": "LAN1", "adresses": [{"adresse": "10.0.30.5", "prefixe": 24}]}]},
          {"id": "d_poste", "type": "pc", "nom": "Poste accueil", "position": {"x": 240, "y": 0}}]}
        """;

    /// <summary>Tape un texte dans un champ, comme au clavier : la valeur, puis l'évènement que Svelte écoute.</summary>
    private static void Type(IslandScene scene, string selector, string text) =>
        scene.Script("(() => { const c = document.querySelector('" + selector + "'); c.focus(); c.value = " + JsonSerializer.Serialize(text) + "; c.dispatchEvent(new Event('input', { bubbles: true })); })()");

    private static void Wide(IslandScene scene)
    {
        var before = scene.Script("document.documentElement.clientWidth");
        scene.Page.Width = 2400;
        TestShell.Flush();
        UiThread.Until(() => scene.Script("document.documentElement.clientWidth") != before, TimeSpan.FromSeconds(20));
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void A_page_becomes_a_template_and_a_template_becomes_a_new_schema(ThemeKind theme)
    {
        using var scene = new IslandScene(Lycee, theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        OpenEditor(scene);

        // Clic droit sur l'onglet « Étage 2 » : la page, et elle seule, devient un modèle ; son nom est proposé.
        TabMenu(scene, 1);
        MenuEntry(scene, "Enregistrer cette page comme modèle…");
        scene.WaitFor("document.querySelector('dialog.dialogue-modele[open] .vignette svg') !== null");
        Assert.AreEqual("Étage 2", TextOf(scene, "document.querySelector('dialog.dialogue-modele input').value"));
        Assert.AreEqual("Enregistrer cette page comme modèle", TextOf(scene, "document.querySelector('#titre-enregistrer-modele').textContent"));
        Type(scene, "dialog.dialogue-modele input", "Étage du lycée");
        Type(scene, "dialog.dialogue-modele label:nth-of-type(2) input", "Switch d'étage et une salle");
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-modele-enregistrer-page-{suffix}");
        scene.Script("[...document.querySelectorAll('dialog.dialogue-modele button')].find((b) => b.textContent.trim() === 'Enregistrer').click()");
        scene.WaitFor("document.querySelector('dialog.dialogue-modele .fait') !== null");
        StringAssert.Contains(TextOf(scene, "document.querySelector('dialog.dialogue-modele .fait').textContent"), "« Étage du lycée » est proposé dans « Nouveau schéma depuis un modèle… »", StringComparison.Ordinal);
        scene.WaitFor("document.activeElement?.classList.contains('fermer') === true");
        var template = JsonNode.Parse(scene.Schemas.Templates().Value.Single().Text)!;
        Assert.AreEqual("Étage du lycée", template["titre"]!.GetValue<string>());
        Assert.AreEqual("Switch d'étage et une salle", template["description"]!.GetValue<string>());
        CollectionAssert.AreEquivalent(SecondFloor, template["equipements"]!.AsArray().Select(device => device!["nom"]!.GetValue<string>()).ToList());
        Assert.IsNull(template["pages"], "Un modèle n'a qu'une page.");
        Assert.HasCount(1, scene.Schemas.Schemas().Value, "Un modèle n'est pas un schéma.");
        scene.Script("document.querySelector('dialog.dialogue-modele button.fermer').click()");
        scene.WaitFor("document.querySelector('dialog.dialogue-modele') === null");

        // « Plus » (une fenêtre de taille moyenne le montre) › « Nouveau schéma depuis un modèle… » : les cinq modèles de
        // kyfRev, dessinés, puis le sien.
        scene.Page.Width = 900;
        TestShell.Flush();
        UiThread.Until(() => scene.Script("getComputedStyle(document.querySelector('.ancre.plus')).display !== 'none'") == "true", TimeSpan.FromSeconds(20));
        scene.Script("document.querySelector('.ancre.plus > button').click()");
        scene.WaitFor("document.querySelector('.menu[role=menu]') !== null");
        MenuEntry(scene, "Nouveau schéma depuis un modèle…");
        scene.WaitFor("document.querySelectorAll('dialog.dialogue-modeles[open] .carte').length === 6");
        Assert.AreEqual("Petit bureau|Étage type|Site distant|Lab TP|Datacenter|Étage du lycée", TextOf(scene, "[...document.querySelectorAll('dialog.dialogue-modeles .carte .nom')].map((n) => n.textContent).join('|')"));
        Assert.AreEqual(5, Count(scene, "dialog.dialogue-modeles .grille:first-of-type .vignette > svg"));
        Assert.AreEqual("Petit bureau", TextOf(scene, "document.querySelector('dialog.dialogue-modeles input.champ').value"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-modeles-{suffix}");

        // Au clavier : une seule vignette prend la tabulation ; les flèches choisissent, et le nom proposé suit.
        Assert.AreEqual(1, Count(scene, "dialog.dialogue-modeles .carte[tabindex=\"0\"]"));
        scene.Script("document.querySelector('dialog.dialogue-modeles .carte[aria-checked=true]').focus()");
        KeyOn(scene, "ArrowRight");
        scene.WaitFor("document.activeElement?.dataset.modele === 'etage-type' && document.activeElement.getAttribute('aria-checked') === 'true'");
        Assert.AreEqual("Étage type", TextOf(scene, "document.querySelector('dialog.dialogue-modeles input.champ').value"));
        KeyOn(scene, "End");
        scene.WaitFor("document.activeElement?.querySelector('.nom')?.textContent === 'Étage du lycée'");

        // Le sien se supprime au clic droit, après une question ; il n'est plus proposé.
        scene.Script("document.activeElement.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 0, clientY: 0 }))");
        scene.WaitFor("document.querySelector('dialog.dialogue-modeles .menu[role=menu]') !== null");
        MenuEntry(scene, "Supprimer le modèle…");
        scene.WaitFor("document.querySelector('dialog.confirmation[open]') !== null");
        StringAssert.Contains(TextOf(scene, "document.querySelector('dialog.confirmation[open]').textContent"), "Supprimer le modèle « Étage du lycée » ?", StringComparison.Ordinal);
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-modele-supprimer-{suffix}");
        scene.Script("[...document.querySelectorAll('dialog.confirmation[open] button')].find((b) => b.textContent.trim() === 'Supprimer').click()");
        scene.WaitFor("document.querySelectorAll('dialog.dialogue-modeles .carte').length === 5 && document.querySelector('dialog.dialogue-modeles .aide') !== null");
        Assert.IsEmpty(scene.Schemas.Templates().Value);
        Assert.AreEqual("Petit bureau", TextOf(scene, "document.querySelector('dialog.dialogue-modeles .carte[aria-checked=true] .nom').textContent"));

        // Fenêtre étroite : deux vignettes par ligne, rien ne déborde.
        Narrow(scene);
        scene.WaitFor("(() => { const c = [...document.querySelectorAll('dialog.dialogue-modeles .carte')].map((x) => x.getBoundingClientRect()); return c.length === 5 && Math.abs(c[0].top - c[1].top) < 2 && c[2].top > c[1].bottom; })()");
        Assert.AreEqual("true", scene.Script("(() => { const r = document.querySelector('dialog.dialogue-modeles').getBoundingClientRect(); return r.left >= 0 && r.right <= innerWidth && r.bottom <= innerHeight; })()"));
        Assert.AreEqual("0", scene.Script("document.querySelector('dialog.dialogue-modeles .defile').scrollWidth - document.querySelector('dialog.dialogue-modeles .defile').clientWidth"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-modeles-etroit-{suffix}");

        // Très large : le dialogue garde sa largeur, ses vignettes sur une ligne ou deux.
        Wide(scene);
        Assert.AreEqual("true", scene.Script("document.querySelector('dialog.dialogue-modeles').getBoundingClientRect().width <= 762"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-modeles-tres-large-{suffix}");

        // Datacenter, sous un autre nom : un nouveau schéma, ouvert à la place de celui-ci, aux identifiants neufs.
        scene.Script("document.querySelector('dialog.dialogue-modeles .carte[data-modele=\"datacenter\"]').click()");
        Assert.AreEqual("Datacenter", TextOf(scene, "document.querySelector('dialog.dialogue-modeles input.champ').value"));
        Type(scene, "dialog.dialogue-modeles input.champ", "Salle machines");
        scene.Script("[...document.querySelectorAll('dialog.dialogue-modeles button')].find((b) => b.textContent.trim() === 'Créer').click()");
        scene.WaitFor("document.querySelector('dialog.dialogue-modeles') === null && document.querySelector('.champ.titre')?.value === 'Salle machines'");
        scene.WaitFor("document.querySelector('svg.canevas')?.textContent.includes('FW-1') === true");
        var created = scene.Schemas.Schemas().Value.Single(schema => schema.Title == "Salle machines");
        var saved = JsonNode.Parse(scene.Schemas.Read(created.Id).Value.Text)!;
        Assert.AreEqual(10, saved["equipements"]!.AsArray().Count);
        Assert.IsNull(saved["description"]);
        Assert.AreEqual(3, saved["vlans"]!.AsArray().Count);
    }

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void The_search_of_the_editor_looks_in_every_schema_and_leads_there(ThemeKind theme)
    {
        using var scene = new IslandScene(Lycee, theme: theme);
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        scene.Schemas.Create(Meylan).Value.ToString();
        OpenEditor(scene);
        string Rows() => TextOf(scene, "[...document.querySelectorAll('.recherche-globale button.trouve')].map((b) => b.querySelector('.nom').textContent + (b.querySelector('.meme') ? '!' : '') + ' @ ' + b.querySelector('.ou').textContent.replace(', page ', '').trim()).join('|')");

        // Ctrl+F, puis « Tous les schémas » : la même recherche, dans tout le coffre.
        scene.Script("document.querySelector('button[aria-label=\"Chercher dans le schéma\"]').click()");
        scene.WaitFor("document.querySelector('.recherche-schema .portee') !== null");
        Type(scene, ".recherche-schema input", "10.0.0.1");
        scene.Script("[...document.querySelectorAll('.recherche-schema .portee button')].find((b) => b.textContent === 'Tous les schémas').click()");
        scene.WaitFor("document.querySelectorAll('.recherche-globale button.trouve').length === 3");
        Assert.AreEqual("Cœur @ Réseau du lycée › Étage 1|LASOTEL 1! @ SAMES Meylan|SW-Étage1! @ Réseau du lycée › Étage 1", Rows());
        Assert.AreEqual("10.0.0.11 sert dans deux schémas : à vérifier s'ils sont sur le même réseau.", TextOf(scene, "document.querySelector('.recherche-globale .conseil').textContent"));
        Assert.AreEqual("Chercher dans tous les schémas", TextOf(scene, "document.querySelector('.recherche-schema input').getAttribute('aria-label')"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-recherche-partout-{suffix}");

        // Un VLAN, par son numéro.
        Type(scene, ".recherche-schema input", "vlan 30");
        scene.WaitFor("document.querySelector('.recherche-globale [data-groupe=vlan] button.trouve') !== null");
        Assert.AreEqual("VLAN 30 @ SAMES Meylan", Rows());

        // Au clavier : flèche bas entre dans les résultats, les flèches s'y déplacent, flèche haut rend la main au champ.
        Type(scene, ".recherche-schema input", "10.0.0.1");
        scene.WaitFor("document.querySelectorAll('.recherche-globale button.trouve').length === 3");
        Key(scene, "ArrowDown", "ArrowDown", 0x28);
        scene.WaitFor("document.activeElement?.matches('.recherche-globale button.trouve') && document.activeElement.textContent.includes('Cœur')");
        Key(scene, "ArrowDown", "ArrowDown", 0x28);
        scene.WaitFor("document.activeElement?.textContent.includes('LASOTEL 1') === true");
        Key(scene, "ArrowUp", "ArrowUp", 0x26);
        Key(scene, "ArrowUp", "ArrowUp", 0x26);
        scene.WaitFor("document.activeElement?.matches('.recherche-schema input') === true");
        Assert.AreEqual("10.0.0.1", TextOf(scene, "document.querySelector('.recherche-schema input').value"));

        // Un résultat de ce schéma, sur une autre page : la page s'ouvre, l'équipement est choisi.
        Type(scene, ".recherche-schema input", "PC-Salle");
        scene.WaitFor("document.querySelectorAll('.recherche-globale button.trouve').length === 1");
        Press(scene, ".recherche-globale button.trouve");
        scene.WaitFor("document.querySelector('.pages .onglet[aria-selected=true] .nom')?.textContent === 'Étage 2'");
        scene.WaitFor("document.querySelector('.proprietes .tete .nom')?.textContent === 'PC-Salle 201'");

        // Un résultat d'un autre schéma : il s'ouvre à la place de celui-ci, l'équipement choisi.
        scene.Script("document.querySelector('button[aria-label=\"Chercher dans le schéma\"]').click()");
        scene.WaitFor("document.querySelector('.recherche-schema .portee') !== null");
        scene.Script("[...document.querySelectorAll('.recherche-schema .portee button')].find((b) => b.textContent === 'Tous les schémas').click()");
        Type(scene, ".recherche-schema input", "lasotel");
        scene.WaitFor("document.querySelectorAll('.recherche-globale button.trouve').length === 1");
        Press(scene, ".recherche-globale button.trouve");
        scene.WaitFor("document.querySelector('.champ.titre')?.value === 'SAMES Meylan'");
        scene.WaitFor("document.querySelector('.proprietes .tete .nom')?.textContent === 'LASOTEL 1'");
        Assert.AreEqual(0, Count(scene, ".recherche-schema"));

        // Fenêtre étroite : le schéma et la page passent sous le nom ; rien ne déborde.
        scene.Script("document.querySelector('svg.canevas').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }))");
        Narrow(scene);
        scene.Script("document.querySelector('button[aria-label=\"Chercher dans le schéma\"]').click()");
        scene.WaitFor("document.querySelector('.recherche-schema .portee') !== null");
        scene.Script("[...document.querySelectorAll('.recherche-schema .portee button')].find((b) => b.textContent === 'Tous les schémas').click()");
        Type(scene, ".recherche-schema input", "10.0.");
        scene.WaitFor("document.querySelectorAll('.recherche-globale button.trouve').length === 4");
        Assert.AreEqual("true", scene.Script("(() => { const r = document.querySelector('.recherche-schema').getBoundingClientRect(); return r.left >= 0 && r.right <= innerWidth; })()"));
        Assert.AreEqual("true", scene.Script("[...document.querySelectorAll('.recherche-globale button.trouve')].every((b) => b.scrollWidth <= b.clientWidth + 1)"));
        Assert.AreEqual("true", scene.Script("(() => { const b = document.querySelector('.recherche-globale button.trouve'); return b.querySelector('.ou').getBoundingClientRect().top > b.querySelector('.nom').getBoundingClientRect().bottom - 1; })()"), "Étroit, le schéma passe sous le nom.");
        Assert.AreEqual("0", scene.Script("document.documentElement.scrollWidth - document.documentElement.clientWidth"));
        scene.WaitFor("document.getAnimations().length === 0");
        scene.Picture($"schemas-recherche-partout-etroit-{suffix}");
    }
}
