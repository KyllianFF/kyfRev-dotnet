<script lang="ts">
  import { definition } from "../../lib/topologie/catalogue";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import {
    type Annotation,
    COULEURS_ANNOTATION,
    FORMES_ANNOTATION,
    GENRES_GROUPE,
    GENRES_RELATION,
    type Groupe,
    nouvelId,
    type Relation,
    STATUTS,
    STATUTS_LIEN,
    type Vlan,
  } from "../../lib/topologie/modele";
  import { dupliquerEquipements, relier, retirerEquipements } from "../../lib/topologie/operations";
  import { VUES } from "../../lib/topologie/vues";
  import ProprietesEquipement from "./ProprietesEquipement.svelte";
  import ProprietesLien from "./ProprietesLien.svelte";

  // Panneau de propriétés contextuel (D39) : selon la sélection, l'équipement,
  // le lien, la relation, le groupe ou l'annotation ; plusieurs équipements :
  // les actions communes ; rien : le schéma et son diagnostic.
  let {
    editeur,
    onconnecter,
    onmontrer,
  }: {
    editeur: EditeurSchema;
    onconnecter: (source: string) => void;
    onmontrer: (id: string) => void;
  } = $props();

  const schema = $derived(editeur.schema);
  const seul = $derived(editeur.selection.length === 1 ? editeur.selection[0] : undefined);
  const equipement = $derived(seul ? schema.equipements.find((e) => e.id === seul) : undefined);
  const lien = $derived(seul ? schema.liens.find((l) => l.id === seul) : undefined);
  const relation = $derived(seul ? schema.relations.find((r) => r.id === seul) : undefined);
  const groupe = $derived(seul ? schema.groupes.find((g) => g.id === seul) : undefined);
  const annotation = $derived(seul ? schema.annotations.find((n) => n.id === seul) : undefined);
  const equipementsChoisis = $derived(schema.equipements.filter((e) => editeur.selection.includes(e.id)));

  const erreurs = $derived(editeur.constats.filter((c) => c.niveau === "erreur"));
  const avertissements = $derived(editeur.constats.filter((c) => c.niveau === "avertissement"));
  const manques = $derived(editeur.constats.filter((c) => c.niveau === "manque"));

  function modifierRelation(action: (r: Relation) => void) {
    editeur.agir((s) => {
      const r = s.relations.find((x) => x.id === seul);
      if (r) action(r);
    });
  }

  function modifierGroupe(action: (g: Groupe) => void) {
    editeur.agir((s) => {
      const g = s.groupes.find((x) => x.id === seul);
      if (g) action(g);
    });
  }

  function modifierAnnotation(changer: (n: Annotation) => void) {
    const id = annotation?.id;
    editeur.agir((s) => {
      const n = s.annotations.find((x) => x.id === id);
      if (n) changer(n);
    });
  }

  /** Une annotation change de forme : ses dimensions suivent. */
  function changerForme(forme: NonNullable<Annotation["forme"]>) {
    modifierAnnotation((n) => {
      if (forme === "note") delete n.forme;
      else n.forme = forme;
      if (forme === "rectangle" || forme === "ellipse") {
        n.largeur ??= 240;
        n.hauteur ??= 160;
        n.couleur ??= "#1f62b0";
      }
      if (forme === "fleche") {
        n.vers ??= { x: n.position.x + 160, y: n.position.y };
        n.couleur ??= "#c0392b";
      } else delete n.vers;
      if (forme !== "rectangle" && forme !== "ellipse") delete n.hauteur;
    });
  }

  /**
   * Deux équipements qui partagent une adresse forment une paire haute
   * disponibilité (pare-feu actif/passif, VIP) : une relation le dit, le
   * diagnostic ne la signale plus (D51).
   */
  function declarerPaire([de, vers]: [string, string]) {
    editeur.agir((s) => {
      relier(s, { genre: "haute-disponibilite", de, vers, nom: "HA actif/passif" });
      for (const e of s.equipements) {
        if ((e.id === de || e.id === vers) && definition(e.type).champs.some((c) => c.cle === "hauteDispo"))
          e.proprietes.hauteDispo ??= "Actif / passif";
      }
    }, "Paire haute disponibilité déclarée : l'adresse partagée n'est plus signalée.");
  }

  // VLAN du schéma (D51) : numéro, nom et couleur, repris sur les câbles.
  function modifierVlan(id: string, action: (v: Vlan) => void) {
    editeur.agir((s) => {
      const v = s.vlans.find((x) => x.id === id);
      if (v) action(v);
    });
  }

  function ajouterVlan() {
    const pris = new Set(schema.vlans.map((v) => v.numero));
    let numero = Math.min(4094, Math.max(0, ...pris) + 10 - (Math.max(0, ...pris) % 10));
    while (pris.has(numero) && numero < 4094) numero++;
    const couleur = COULEURS_ANNOTATION[schema.vlans.length % COULEURS_ANNOTATION.length]?.valeur;
    editeur.agir((s) => {
      s.vlans.push({ id: nouvelId("v"), numero, nom: "", sousReseaux: [], ...(couleur ? { couleur } : {}) });
    });
  }

  function aligner(axe: "x" | "y") {
    const ids = new Set(equipementsChoisis.filter((e) => !e.verrou).map((e) => e.id));
    const valeurs = equipementsChoisis.filter((e) => ids.has(e.id)).map((e) => e.position[axe]);
    const moyenne = Math.round(valeurs.reduce((a, b) => a + b, 0) / Math.max(1, valeurs.length) / 20) * 20;
    editeur.agir(
      (s) => {
        for (const e of s.equipements) if (ids.has(e.id)) e.position[axe] = moyenne;
      },
      axe === "x" ? "Alignés verticalement." : "Alignés horizontalement.",
    );
  }

  function repartir(axe: "x" | "y") {
    const tries = equipementsChoisis
      .filter((e) => !e.verrou)
      .sort((a, b) => a.position[axe] - b.position[axe]);
    if (tries.length < 3) return;
    const premier = tries[0]?.position[axe] ?? 0;
    const pas = ((tries.at(-1)?.position[axe] ?? 0) - premier) / (tries.length - 1);
    const rangs = new Map(tries.map((e, i) => [e.id, Math.round(premier + i * pas)]));
    editeur.agir((s) => {
      for (const e of s.equipements) {
        const v = rangs.get(e.id);
        if (v !== undefined) e.position[axe] = v;
      }
    }, "Répartis à intervalles égaux.");
  }

  function grouper() {
    const xs = equipementsChoisis.map((e) => e.position.x);
    const ys = equipementsChoisis.map((e) => e.position.y);
    const position = { x: Math.min(...xs) - 70, y: Math.min(...ys) - 60 };
    const taille = {
      largeur: Math.max(...xs) - Math.min(...xs) + 140,
      hauteur: Math.max(...ys) - Math.min(...ys) + 140,
    };
    let id = "";
    editeur.agir((s) => {
      id = nouvelId("g");
      s.groupes.push({
        id,
        nom: "Groupe",
        genre: "groupe",
        membres: equipementsChoisis.map((e) => e.id),
        position,
        taille,
      });
    }, "Groupe créé.");
    editeur.selection = [id];
  }
</script>

<aside class="panneau" aria-label="Propriétés">
  {#if equipement}
    {#key equipement.id}
      <ProprietesEquipement {editeur} {equipement} {onconnecter} />
    {/key}
  {:else if lien}
    {#key lien.id}
      <ProprietesLien {editeur} {lien} />
    {/key}
  {:else if relation}
    <h3 class="titre">Relation : {GENRES_RELATION.find((g) => g.valeur === relation.genre)?.nom}</h3>
    <p class="aide">
      {schema.equipements.find((e) => e.id === relation.de)?.nom} → {schema.equipements.find((e) => e.id === relation.vers)?.nom}.
      Une relation logique n'occupe aucun port.
    </p>
    <div class="champs">
      <label>
        <span>Genre</span>
        <select class="champ" value={relation.genre} onchange={(e) => modifierRelation((r) => (r.genre = e.currentTarget.value as Relation["genre"]))}>
          {#each GENRES_RELATION as g (g.valeur)}<option value={g.valeur}>{g.nom}</option>{/each}
        </select>
      </label>
      <label>
        <span>Statut</span>
        <select class="champ" value={relation.statut} onchange={(e) => modifierRelation((r) => (r.statut = e.currentTarget.value as Relation["statut"]))}>
          {#each STATUTS_LIEN as s (s.valeur)}<option value={s.valeur}>{s.nom}</option>{/each}
        </select>
      </label>
      <label class="large">
        <span>Nom</span>
        <input class="champ" value={relation.nom ?? ""} onchange={(e) => modifierRelation((r) => { if (e.currentTarget.value.trim()) r.nom = e.currentTarget.value.trim(); else delete r.nom; })} />
      </label>
      {#if relation.genre === "association-wifi"}
        <label class="large">
          <span>SSID</span>
          <input class="champ" value={String(relation.proprietes.ssid ?? "")} onchange={(e) => modifierRelation((r) => { if (e.currentTarget.value.trim()) r.proprietes.ssid = e.currentTarget.value.trim(); else delete r.proprietes.ssid; })} />
        </label>
      {/if}
      <label class="large">
        <span>Description</span>
        <textarea class="champ" rows="2" value={relation.description ?? ""} onchange={(e) => modifierRelation((r) => { if (e.currentTarget.value.trim()) r.description = e.currentTarget.value.trim(); else delete r.description; })}></textarea>
      </label>
    </div>
    <button type="button" class="bouton petit attention" onclick={() => { editeur.agir((s) => (s.relations = s.relations.filter((r) => r.id !== relation.id)), "Relation retirée."); editeur.selection = []; }}>Retirer la relation</button>
  {:else if groupe}
    <h3 class="titre">{GENRES_GROUPE.find((g) => g.valeur === groupe.genre)?.nom} « {groupe.nom} »</h3>
    <div class="champs">
      <label class="large">
        <span>Nom</span>
        <input class="champ" value={groupe.nom} onchange={(e) => modifierGroupe((g) => (g.nom = e.currentTarget.value.trim()))} />
      </label>
      <label>
        <span>Genre</span>
        <select class="champ" value={groupe.genre} onchange={(e) => modifierGroupe((g) => (g.genre = e.currentTarget.value as Groupe["genre"]))}>
          {#each GENRES_GROUPE as g (g.valeur)}<option value={g.valeur}>{g.nom}</option>{/each}
        </select>
      </label>
      <label>
        <span>Couleur</span>
        <input class="champ" type="color" value={groupe.couleur ?? "#7f4ea8"} onchange={(e) => modifierGroupe((g) => (g.couleur = e.currentTarget.value))} />
      </label>
      <label class="large">
        <span>Dans le groupe</span>
        <select class="champ" value={groupe.parent ?? ""} onchange={(e) => modifierGroupe((g) => { if (e.currentTarget.value) g.parent = e.currentTarget.value; else delete g.parent; })}>
          <option value="">— (premier niveau)</option>
          {#each schema.groupes.filter((g) => g.id !== groupe.id) as g (g.id)}<option value={g.id}>{g.nom}</option>{/each}
        </select>
      </label>
    </div>
    <p class="aide">{groupe.membres.length} équipement(s). Déplacer le groupe déplace ses membres.</p>
    <div class="actions">
      <button type="button" class="bouton petit" onclick={() => modifierGroupe((g) => { if (g.replie) delete g.replie; else g.replie = true; })}>{groupe.replie ? "Déplier" : "Replier"}</button>
      <button type="button" class="bouton petit" onclick={() => modifierGroupe((g) => { if (g.verrou) delete g.verrou; else g.verrou = true; })}>{groupe.verrou ? "Déverrouiller" : "Verrouiller"}</button>
      <button type="button" class="bouton petit attention" onclick={() => { editeur.agir((s) => (s.groupes = s.groupes.filter((g) => g.id !== groupe.id).map((g) => (g.parent === groupe.id ? { ...g, parent: undefined } : g))), "Groupe retiré ; ses équipements restent."); editeur.selection = []; }}>Retirer le groupe</button>
    </div>
  {:else if annotation}
    {@const forme = annotation.forme ?? "note"}
    <h3 class="titre">{FORMES_ANNOTATION.find((f) => f.valeur === forme)?.nom ?? "Annotation"}</h3>
    <div class="champs">
      <label>
        <span>Forme</span>
        <select class="champ" value={forme} onchange={(e) => changerForme(e.currentTarget.value as Annotation["forme"] & string)}>
          {#each FORMES_ANNOTATION as f (f.valeur)}<option value={f.valeur}>{f.nom}</option>{/each}
        </select>
      </label>
      {#if forme === "texte"}
        <label>
          <span>Taille du texte</span>
          <input class="champ" type="number" min="10" max="72" value={annotation.taille ?? 16} onchange={(e) => modifierAnnotation((n) => (n.taille = Math.max(10, Math.min(72, Number(e.currentTarget.value) || 16))))} />
        </label>
      {/if}
      <div class="large">
        <span class="etiquette-champ">Couleur</span>
        <div class="couleurs" role="group" aria-label="Couleur">
          {#if forme === "note" || forme === "texte"}
            <button type="button" class="pastille" class:choisie={!annotation.couleur} aria-pressed={!annotation.couleur} aria-label="Par défaut" title="Par défaut" onclick={() => modifierAnnotation((n) => delete n.couleur)}>×</button>
          {/if}
          {#each COULEURS_ANNOTATION as c (c.valeur)}
            <button type="button" class="pastille" class:choisie={annotation.couleur === c.valeur} style="background: {c.valeur}" aria-pressed={annotation.couleur === c.valeur} aria-label={c.nom} title={c.nom} onclick={() => modifierAnnotation((n) => (n.couleur = c.valeur))}></button>
          {/each}
        </div>
      </div>
    </div>
    <label class="large">
      <span>{forme === "rectangle" || forme === "ellipse" ? "Légende" : forme === "fleche" ? "Libellé (facultatif)" : "Texte"}</span>
      <textarea class="champ" rows={forme === "note" || forme === "texte" ? 5 : 2} value={annotation.texte} onchange={(e) => modifierAnnotation((n) => (n.texte = e.currentTarget.value))}></textarea>
    </label>
    <button type="button" class="bouton petit attention" onclick={() => { editeur.agir((s) => (s.annotations = s.annotations.filter((n) => n.id !== annotation.id)), "Annotation retirée."); editeur.selection = []; }}>Retirer l'annotation</button>
  {:else if equipementsChoisis.length > 1}
    <h3 class="titre">{equipementsChoisis.length} équipements</h3>
    <div class="actions">
      <button type="button" class="bouton petit" onclick={() => aligner("y")}>Aligner horizontalement</button>
      <button type="button" class="bouton petit" onclick={() => aligner("x")}>Aligner verticalement</button>
      <button type="button" class="bouton petit" onclick={() => repartir("x")}>Répartir en largeur</button>
      <button type="button" class="bouton petit" onclick={() => repartir("y")}>Répartir en hauteur</button>
      <button type="button" class="bouton petit" disabled={!editeur.groupement.peutGrouper} title="Ctrl+G : les éléments choisis se manipulent ensemble" onclick={() => editeur.grouper()}>Grouper</button>
      <button type="button" class="bouton petit" disabled={!editeur.groupement.peutDegrouper} title="Ctrl+Maj+G" onclick={() => editeur.degrouper()}>Dégrouper</button>
      <button type="button" class="bouton petit" title="Dessine un cadre nommé autour des équipements choisis" onclick={grouper}>Encadrer</button>
      <button type="button" class="bouton petit" onclick={() => editeur.agir((s) => { editeur.selection = dupliquerEquipements(s, new Set(editeur.selection)); }, "Équipements dupliqués.")}>Dupliquer</button>
    </div>
    <label class="large">
      <span>Statut de tous</span>
      <select class="champ" onchange={(e) => { const statut = e.currentTarget.value as (typeof STATUTS)[number]["valeur"]; editeur.agir((s) => { for (const x of s.equipements) if (editeur.selection.includes(x.id)) x.statut = statut; }); }}>
        <option value="" selected disabled>Choisir…</option>
        {#each STATUTS as s (s.valeur)}<option value={s.valeur}>{s.nom}</option>{/each}
      </select>
    </label>
    <div class="actions">
      <button type="button" class="bouton petit" onclick={() => editeur.agir((s) => { const tous = equipementsChoisis.every((e) => e.verrou); for (const x of s.equipements) if (editeur.selection.includes(x.id)) { if (tous) delete x.verrou; else x.verrou = true; } })}>Verrouiller / déverrouiller</button>
      <button type="button" class="bouton petit attention" onclick={() => { editeur.agir((s) => retirerEquipements(s, new Set(editeur.selection)), "Équipements retirés, avec leurs liens : Ctrl+Z pour annuler."); editeur.selection = []; }}>Retirer</button>
    </div>
  {:else}
    <h3 class="titre">Schéma</h3>
    <div class="champs">
      <label class="large">
        <span>Titre</span>
        <input class="champ" value={schema.titre} onchange={(e) => {
          const nom = e.currentTarget.value.trim();
          if (!nom) {
            e.currentTarget.value = schema.titre;
            editeur.annonce = "Un schéma doit avoir un nom.";
          } else editeur.agir((s) => (s.titre = nom));
        }} />
      </label>
      <label class="large">
        <span>Description</span>
        <textarea class="champ" rows="2" value={schema.description ?? ""} onchange={(e) => editeur.agir((s) => { if (e.currentTarget.value.trim()) s.description = e.currentTarget.value.trim(); else delete s.description; })}></textarea>
      </label>
      <label class="large">
        <span>Vue des aperçus dans les fiches</span>
        <select class="champ" value={schema.vue} onchange={(e) => editeur.changerVue(e.currentTarget.value as typeof schema.vue)}>
          {#each VUES as v (v.id)}<option value={v.id}>{v.nom}</option>{/each}
        </select>
      </label>
      <label class="large case-schema">
        <input type="checkbox" checked={schema.cablesCoudes === true} onchange={(e) => { const coudes = e.currentTarget.checked; editeur.agir((s) => { if (coudes) s.cablesCoudes = true; else delete s.cablesCoudes; }); }} />
        <span>Câbles coudés à angle droit</span>
      </label>
    </div>
    <details open={schema.vlans.length > 0}>
      <summary>VLAN du schéma ({schema.vlans.length})</summary>
      <p class="aide">Leur nom et leur couleur se retrouvent sur les câbles qui les portent (non tagués).</p>
      {#if schema.vlans.length > 0}
        <ul class="vlans">
          {#each [...schema.vlans].sort((a, b) => a.numero - b.numero) as v (v.id)}
            <li>
              <input
                class="champ numero"
                type="number"
                min="1"
                max="4094"
                aria-label="Numéro du VLAN"
                value={v.numero}
                onchange={(e) => {
                  const n = Number(e.currentTarget.value);
                  if (Number.isInteger(n) && n >= 1 && n <= 4094) modifierVlan(v.id, (x) => (x.numero = n));
                  else {
                    e.currentTarget.value = String(v.numero);
                    editeur.annonce = "Un numéro de VLAN va de 1 à 4094.";
                  }
                }}
              />
              <input class="champ" aria-label={`Nom du VLAN ${v.numero}`} placeholder="Utilisateurs, téléphonie…" value={v.nom} onchange={(e) => { const nom = e.currentTarget.value.trim(); modifierVlan(v.id, (x) => (x.nom = nom)); }} />
              <input class="champ couleur" type="color" aria-label={`Couleur du VLAN ${v.numero}`} value={v.couleur ?? "#4b5563"} onchange={(e) => { const c = e.currentTarget.value; modifierVlan(v.id, (x) => (x.couleur = c)); }} />
              <button type="button" class="bouton discret petit" aria-label={`Retirer le VLAN ${v.numero}`} title="Retirer" onclick={() => editeur.agir((s) => (s.vlans = s.vlans.filter((x) => x.id !== v.id)))}>✕</button>
            </li>
          {/each}
        </ul>
      {/if}
      <button type="button" class="bouton petit" onclick={ajouterVlan}>Ajouter un VLAN</button>
    </details>
    <p class="aide">
      {schema.equipements.length} équipement(s), {schema.liens.length} lien(s), {schema.relations.length} relation(s),
      {schema.modeles.length} modèle(s) matériel(s).
    </p>
    {#if schema.modeles.length > 0}
      <details>
        <summary>Modèles matériels</summary>
        <ul class="liste">
          {#each schema.modeles as m (m.id)}
            {@const utilise = schema.equipements.some((e) => e.membres.some((x) => x.modele === m.id))}
            <li>
              {m.fabricant ? `${m.fabricant} ` : ""}{m.reference} <span class="aide">({definition(m.type).nom}, {m.groupes.reduce((n, g) => n + g.nombre, 0)} ports)</span>
              {#if !utilise}
                <button type="button" class="bouton discret petit" onclick={() => editeur.agir((s) => (s.modeles = s.modeles.filter((x) => x.id !== m.id)))}>Retirer</button>
              {/if}
            </li>
          {/each}
        </ul>
      </details>
    {/if}
    <section class="diagnostic" aria-label="Diagnostic">
      <h4>Diagnostic</h4>
      {#if editeur.constats.length === 0}
        <p class="ok">Rien à signaler.</p>
      {/if}
      {#each [["erreur", "Erreurs (bloquent l'enregistrement)", erreurs], ["avertissement", "Incohérences possibles", avertissements], ["manque", "Informations manquantes", manques]] as const as [niveau, titre, liste] (niveau)}
        {#if liste.length > 0}
          <details open={niveau !== "manque"}>
            <summary class={niveau}>{titre} ({liste.length})</summary>
            <ul class="liste">
              {#each liste as c, i (i)}
                <li>
                  {#if c.objets[0] && schema.equipements.some((e) => e.id === c.objets[0])}
                    <button type="button" class="lien-texte" onclick={() => { editeur.selection = [c.objets[0] as string]; onmontrer(c.objets[0] as string); }}>{c.message}</button>
                  {:else if c.objets[0]}
                    <button type="button" class="lien-texte" onclick={() => (editeur.selection = [c.objets[0] as string])}>{c.message}</button>
                  {:else}
                    {c.message}
                  {/if}
                  {#if c.paire}
                    {@const paire = c.paire}
                    <button type="button" class="bouton petit action-constat" title="Les deux équipements partagent cette adresse à dessein (pare-feu actif/passif, adresse virtuelle)" onclick={() => declarerPaire(paire)}>
                      C'est une paire haute disponibilité
                    </button>
                  {/if}
                </li>
              {/each}
            </ul>
          </details>
        {/if}
      {/each}
    </section>
  {/if}
</aside>

<style>
  .panneau {
    min-height: 0;
    overflow-y: auto;
    padding: 12px;
    border-left: 1px solid var(--bordure);
    background: var(--surface);
  }

  .titre {
    margin: 0 0 8px;
    font-size: 15px;
  }

  h4 {
    margin: 12px 0 4px;
    font-size: 13px;
  }

  .champs {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 6px 8px;
    margin-bottom: 8px;
  }

  label {
    display: flex;
    flex-direction: column;
    gap: 2px;
    min-width: 0;
    font-size: 12px;
  }

  label > span {
    color: var(--texte-secondaire);
  }

  .champ {
    height: 30px;
    font-size: 13px;
  }

  textarea.champ {
    height: auto;
    padding: 4px 8px;
  }

  .large {
    grid-column: 1 / -1;
  }

  .etiquette-champ {
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .couleurs {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    margin-top: 4px;
  }

  .pastille {
    width: 24px;
    height: 24px;
    padding: 0;
    border: 2px solid var(--surface);
    border-radius: 50%;
    background: var(--surface-2);
    box-shadow: 0 0 0 1px var(--bordure);
    color: var(--texte-secondaire);
    font-size: 12px;
    line-height: 1;
    cursor: pointer;
  }

  .pastille.choisie {
    box-shadow: 0 0 0 2px var(--focus);
  }

  .case-schema {
    flex-direction: row;
    align-items: center;
    gap: 6px;
  }

  .aide {
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    margin: 8px 0;
  }

  .bouton.petit {
    height: 28px;
    padding: 0 10px;
    font-size: 12px;
  }

  .attention {
    color: var(--retard);
  }

  .liste {
    margin: 4px 0;
    padding-left: 16px;
    font-size: 12px;
  }

  .liste li {
    margin-bottom: 3px;
  }

  summary {
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
  }

  summary.erreur {
    color: var(--retard);
  }

  summary.avertissement {
    color: var(--avertissement);
  }

  .ok {
    color: var(--succes);
    font-size: 13px;
  }

  .action-constat {
    display: flex;
    margin: 4px 0 6px;
  }

  .vlans {
    display: flex;
    flex-direction: column;
    gap: 4px;
    margin: 4px 0 6px;
    padding: 0;
    list-style: none;
  }

  .vlans li {
    display: grid;
    grid-template-columns: 64px 1fr 34px auto;
    gap: 4px;
    align-items: center;
  }

  .vlans .champ {
    min-width: 0;
    height: 28px;
    padding: 0 6px;
  }

  .vlans .couleur {
    padding: 2px;
  }

  .lien-texte {
    padding: 0;
    border: none;
    background: none;
    color: var(--texte);
    font-size: 12px;
    text-align: left;
    /* La puce de la liste reste sur la première ligne d'un message long. */
    vertical-align: top;
    text-decoration: underline dotted;
    cursor: pointer;
  }
</style>
