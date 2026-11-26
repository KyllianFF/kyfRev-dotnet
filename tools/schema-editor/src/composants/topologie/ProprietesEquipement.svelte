<script lang="ts" module>
  /** Les quatre onglets du volet d'un équipement (30C). */
  export type OngletEquipement = "general" | "ports" | "reseau" | "inventaire";
</script>

<script lang="ts">
  import { tick } from "svelte";
  import { definition } from "../../lib/topologie/catalogue";
  import { collerConfiguration, copierConfiguration } from "../../lib/topologie/configuration";
  import { couleurDuType } from "../../lib/topologie/dessin";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import { illustration } from "../../lib/topologie/illustrations";
  import { type Equipement, type Interface, STATUTS } from "../../lib/topologie/modele";
  import {
    auNombreDeMembres,
    changerProfil,
    dupliquerEquipements,
    retirerEquipements,
  } from "../../lib/topologie/operations";
  import { MEMBRES_MAX } from "../../lib/topologie/ports";
  import { configurationCopiee, retenirType } from "../../lib/topologie/volet.svelte";
  import ChoixType from "./ChoixType.svelte";
  import DialogueProfil from "./DialogueProfil.svelte";
  import PortsEquipement from "./PortsEquipement.svelte";
  import ReseauEquipement from "./ReseauEquipement.svelte";

  // Le volet d'un équipement en mode avancé (30C) : en tête, son icône, son nom, son type et son statut, et « ⋯ » ;
  // puis quatre onglets, Général, Ports, Réseau et Inventaire. L'onglet choisi reste choisi d'un équipement à l'autre.
  let {
    editeur,
    equipement,
    onconnecter,
    oncopier,
    onglet = $bindable("general"),
  }: {
    editeur: EditeurSchema;
    equipement: Equipement;
    onconnecter: (source: string) => void;
    /** Copier l'équipement (Ctrl+C), pour le coller ici ou dans un autre schéma. */
    oncopier: () => void;
    onglet?: OngletEquipement;
  } = $props();

  const schema = $derived(editeur.schema);
  const def = $derived(definition(equipement.type));
  /** « Switch de cœur » : le nom du type sans sa précision entre parenthèses. */
  const nomDuType = $derived(def.nom.replace(/\s*\(.*\)$/, ""));
  /** « Propres à un switch », « à une imprimante », « à cet équipement générique ». */
  const FEMININS = new Set(["imprimante", "camera-ip", "machine-virtuelle", "baie", "baie-stockage", "prise-reseau", "bibliotheque-bandes"]);
  const article = $derived(def.nom.startsWith("Équipement") ? "cet" : FEMININS.has(def.type) ? "une" : "un");
  const adresses = $derived(equipement.interfaces.reduce((n, i) => n + i.adresses.length, 0));
  const ONGLETS: { id: OngletEquipement; nom: string }[] = [
    { id: "general", nom: "Général" },
    { id: "ports", nom: "Ports" },
    { id: "reseau", nom: "Réseau" },
    { id: "inventaire", nom: "Inventaire" },
  ];
  const compte = (id: OngletEquipement) =>
    id === "ports" ? equipement.interfaces.length : id === "reseau" ? adresses : undefined;

  let choixType = $state(false);
  let menu = $state(false);
  let aRetirer = $state(false);
  let aColler = $state(false);
  let boutonType = $state<HTMLButtonElement>();
  let boutonMenu = $state<HTMLButtonElement>();
  let menuElement = $state<HTMLDivElement>();
  let profil = $state<{ modifier: (e: Equipement) => void; retirees: Interface[] } | null>(null);

  /** Modifie l'équipement (annulable). */
  function modifier(action: (e: Equipement) => void, annonce?: string) {
    const id = equipement.id;
    editeur.agir((s) => {
      const e = s.equipements.find((x) => x.id === id);
      if (e) action(e);
    }, annonce);
  }

  const texteOuRien = (valeur: string) => valeur.trim() || undefined;
  function ecrireTexte<K extends keyof Equipement>(cle: K, valeur: string) {
    if (cle === "nom" && !valeur.trim()) {
      editeur.annonce = "Un équipement doit avoir un nom.";
      return;
    }
    modifier((e) => {
      const propre = texteOuRien(valeur);
      if (propre === undefined) delete e[cle];
      else (e as Record<string, unknown>)[cle] = propre;
    });
  }

  function ecrirePropriete(cle: string, valeur: string | number | boolean | null) {
    modifier((e) => {
      if (valeur === null || valeur === "") delete e.proprietes[cle];
      else e.proprietes[cle] = valeur;
    });
  }

  /** Changement de profil (membres, groupes de ports) : appliqué, ou décisions demandées pour les liens touchés. */
  function changer(action: (e: Equipement) => void) {
    const avant = editeur.commencerGeste();
    const resultat = changerProfil(editeur.schema, equipement.id, action);
    if (resultat.applique) {
      editeur.finirGeste(avant);
      editeur.annonce = "Interfaces régénérées.";
      return;
    }
    profil = { modifier: action, retirees: resultat.retirees };
  }

  function choisirType(type: string) {
    choixType = false;
    void tick().then(() => boutonType?.focus());
    if (type === equipement.type) return;
    retenirType(equipement.type);
    const nom = definition(type).nom;
    modifier((e) => (e.type = type), `Type : ${nom}. Le nom, les ports et les connexions restent.`);
  }

  /** Les types posés sur le schéma, du dernier posé au premier. */
  const presents = $derived([...schema.equipements].reverse().map((e) => e.type));

  // --- « ⋯ » -------------------------------------------------------------------------------------------------------

  function ouvrirMenu() {
    menu = !menu;
    if (menu) void tick().then(() => menuElement?.querySelector<HTMLButtonElement>("button:not(:disabled)")?.focus());
  }

  function fermerMenu(rendreLaMain = true) {
    menu = false;
    if (rendreLaMain) void tick().then(() => boutonMenu?.focus());
  }

  function clavierDuMenu(evenement: KeyboardEvent) {
    const items = [...(menuElement?.querySelectorAll<HTMLButtonElement>("button:not(:disabled)") ?? [])];
    const rang = items.indexOf(document.activeElement as HTMLButtonElement);
    let vers: HTMLButtonElement | undefined;
    if (evenement.key === "ArrowDown") vers = items[(rang + 1) % items.length];
    else if (evenement.key === "ArrowUp") vers = items[(rang - 1 + items.length) % items.length];
    else if (evenement.key === "Home") vers = items[0];
    else if (evenement.key === "End") vers = items.at(-1);
    else if (evenement.key === "Escape") {
      evenement.preventDefault();
      evenement.stopPropagation();
      fermerMenu();
      return;
    } else if (evenement.key === "Tab") {
      fermerMenu(false);
      return;
    } else return;
    // Les flèches sont pour le menu : l'équipement ne bouge pas sur le plan.
    evenement.preventDefault();
    evenement.stopPropagation();
    vers?.focus();
  }

  $effect(() => {
    if (!menu) return;
    const dehors = (evenement: PointerEvent) => {
      const cible = evenement.target as Node | null;
      if (cible && !menuElement?.contains(cible) && !boutonMenu?.contains(cible)) fermerMenu(false);
    };
    window.addEventListener("pointerdown", dehors, true);
    return () => window.removeEventListener("pointerdown", dehors, true);
  });

  /** Une action du menu : le menu se ferme, puis l'action. */
  function action(faire: () => void) {
    fermerMenu(false);
    faire();
  }

  function copierLaConfiguration() {
    configurationCopiee.valeur = copierConfiguration($state.snapshot(editeur.schema), equipement.id);
    editeur.annonce = `Configuration de « ${equipement.nom} » copiée : « Coller sur… » la reprend sur d'autres équipements.`;
  }

  const copiee = $derived(configurationCopiee.valeur);
  const cablesDeLEquipement = $derived(
    schema.liens.filter((l) => l.a.equipement === equipement.id || l.b.equipement === equipement.id).length,
  );

  // --- Les onglets ----------------------------------------------------------------------------------------------------

  let listeOnglets = $state<HTMLDivElement>();
  let corps = $state<HTMLDivElement>();
  // Un autre onglet se lit depuis son début.
  $effect(() => {
    void onglet;
    if (corps) corps.scrollTop = 0;
  });
  function clavierDesOnglets(evenement: KeyboardEvent) {
    const rang = ONGLETS.findIndex((o) => o.id === onglet);
    const vers =
      evenement.key === "ArrowRight"
        ? (rang + 1) % ONGLETS.length
        : evenement.key === "ArrowLeft"
          ? (rang - 1 + ONGLETS.length) % ONGLETS.length
          : evenement.key === "Home"
            ? 0
            : evenement.key === "End"
              ? ONGLETS.length - 1
              : -1;
    const cible = ONGLETS[vers];
    if (!cible) return;
    evenement.preventDefault();
    evenement.stopPropagation();
    onglet = cible.id;
    listeOnglets?.querySelectorAll<HTMLElement>("[role=tab]")[vers]?.focus();
  }

  const INVENTAIRE: [keyof Equipement, string][] = [
    ["fabricant", "Fabricant"],
    ["reference", "Modèle"],
    ["inventaire", "Référence d'inventaire"],
    ["serie", "Numéro de série"],
    ["site", "Site"],
    ["salle", "Salle"],
    ["baie", "Baie"],
    ["positionRack", "Position dans la baie (U)"],
    ["garantie", "Garantie ou maintenance"],
    ["contact", "Responsable ou contact"],
    ["systeme", "Système, firmware ou version"],
    ["documentation", "Adresse de la documentation"],
    ["referenceSecret", "Référence dans le gestionnaire de secrets"],
  ];
</script>

<div class="proprietes">
  <header class="tete">
    <svg class="icone" viewBox="2 2 60 60" width="40" height="40" aria-hidden="true">
      <g filter="url(#ombre)">{@html illustration(def.icone, couleurDuType(equipement.type))}</g>
    </svg>
    <div class="identite">
      <h3 class="nom" title={equipement.nom}>{equipement.nom}</h3>
      <button
        bind:this={boutonType}
        type="button"
        class="pastille-type"
        data-choix-type
        aria-haspopup="dialog"
        aria-expanded={choixType}
        aria-label="Type : {def.nom}. Changer de type"
        title="Changer de type"
        onclick={() => (choixType = !choixType)}
      >
        <span>{nomDuType}</span><span class="chevron" aria-hidden="true">▾</span>
      </button>
    </div>
    <label class="statut statut-{equipement.statut}" title="Statut documenté">
      <span class="point" aria-hidden="true"></span>
      <select aria-label="Statut" value={equipement.statut} onchange={(e) => modifier((x) => (x.statut = e.currentTarget.value as Equipement["statut"]))}>
        {#each STATUTS as s (s.valeur)}<option value={s.valeur}>{s.nom}</option>{/each}
      </select>
    </label>
    <button
      bind:this={boutonMenu}
      type="button"
      class="plus"
      aria-label="Autres actions sur {equipement.nom}"
      aria-haspopup="menu"
      aria-expanded={menu}
      title="Connecter, dupliquer, copier la configuration, retirer…"
      onclick={ouvrirMenu}
    >⋯</button>
    {#if menu}
      <!-- svelte-ignore a11y_interactive_supports_focus -->
      <div bind:this={menuElement} class="menu" role="menu" aria-label="Actions sur {equipement.nom}" onkeydown={clavierDuMenu}>
        <button type="button" role="menuitem" onclick={() => action(() => onconnecter(equipement.id))}>Connecter…</button>
        <button type="button" role="menuitem" onclick={() => action(() => editeur.agir((s) => { editeur.selection = dupliquerEquipements(s, new Set([equipement.id])); }, "Équipement dupliqué."))}>
          Dupliquer <span class="touche">Ctrl+D</span>
        </button>
        <button type="button" role="menuitem" onclick={() => action(oncopier)}>Copier <span class="touche">Ctrl+C</span></button>
        <button type="button" role="menuitem" onclick={() => action(() => modifier((x) => { if (x.verrou) delete x.verrou; else x.verrou = true; }, equipement.verrou ? "Place déverrouillée." : "Place verrouillée : l'équipement ne bouge plus sur le plan."))}>
          {equipement.verrou ? "Déverrouiller la place" : "Verrouiller la place"}
        </button>
        {#if def.hebergeur}
          <button type="button" role="menuitem" onclick={() => action(() => modifier((x) => { if (x.replie) delete x.replie; else x.replie = true; }))}>
            {equipement.replie ? "Déplier ce qu'il héberge" : "Replier ce qu'il héberge"}
          </button>
        {/if}
        {#if equipement.pose}
          <button type="button" role="menuitem" data-action="remettre-nom" title="Oublie la position et l'angle de son nom : il retrouve sa place sous l'équipement" onclick={() => action(() => modifier((x) => delete x.pose, "Nom remis à sa place."))}>
            Remettre le nom à sa place
          </button>
        {/if}
        <hr />
        <button type="button" role="menuitem" data-action="copier-configuration" onclick={() => action(copierLaConfiguration)}>Copier la configuration</button>
        <button
          type="button"
          role="menuitem"
          data-action="coller-configuration"
          disabled={!copiee}
          title={copiee ? `Reprendre la configuration de « ${copiee.nom} » sur d'autres équipements` : "Copiez d'abord la configuration d'un équipement"}
          onclick={() => action(() => (aColler = true))}
        >
          Coller sur…
        </button>
        <hr />
        <button type="button" role="menuitem" class="danger" data-action="retirer" onclick={() => action(() => (aRetirer = true))}>Retirer…</button>
      </div>
    {/if}
    {#if choixType}
      <ChoixType
        actuel={equipement.type}
        {presents}
        onchoisir={choisirType}
        onfermer={() => {
          choixType = false;
          void tick().then(() => boutonType?.focus());
        }}
      />
    {/if}
  </header>

  <!-- svelte-ignore a11y_interactive_supports_focus -->
  <div bind:this={listeOnglets} class="onglets" role="tablist" aria-label="Configuration de {equipement.nom}" onkeydown={clavierDesOnglets}>
    {#each ONGLETS as o (o.id)}
      {@const n = compte(o.id)}
      <button
        type="button"
        role="tab"
        id="onglet-equipement-{o.id}"
        data-onglet={o.id}
        aria-selected={onglet === o.id}
        aria-controls="contenu-equipement"
        tabindex={onglet === o.id ? 0 : -1}
        onclick={() => (onglet = o.id)}
      >
        {o.nom}{#if n !== undefined}<span class="compte">{n}</span>{/if}
      </button>
    {/each}
  </div>

  <div bind:this={corps} class="corps" id="contenu-equipement" role="tabpanel" aria-labelledby="onglet-equipement-{onglet}" data-onglet={onglet}>
    {#if onglet === "general"}
      <div class="champs">
        <label>
          <span>Nom</span>
          <input class="champ" value={equipement.nom} onchange={(e) => ecrireTexte("nom", e.currentTarget.value)} />
        </label>
        <label>
          <span>Nom d'hôte</span>
          <input class="champ" value={equipement.nomHote ?? ""} spellcheck="false" onchange={(e) => ecrireTexte("nomHote", e.currentTarget.value)} />
        </label>
        <label class="large">
          <span>Rôle</span>
          <input class="champ" value={equipement.role ?? ""} placeholder="Cœur de réseau, accès étage 2…" onchange={(e) => ecrireTexte("role", e.currentTarget.value)} />
        </label>
      </div>
      {#if def.champs.length > 0 || def.empilable || equipement.interfaces.some((i) => i.poe?.fournit)}
        <h4 class="titre-section">Propres à {article} {nomDuType.toLowerCase()}</h4>
        <div class="champs">
          {#if def.empilable}
            <label>
              <span>Membres du stack</span>
              <input class="champ" type="number" min="1" max={MEMBRES_MAX} value={equipement.membres.length} onchange={(e) => changer(auNombreDeMembres(Number(e.currentTarget.value)))} />
            </label>
          {/if}
          {#each def.champs as c (c.cle)}
            {#if c.genre === "oui-non"}
              <label class="case">
                <input type="checkbox" checked={equipement.proprietes[c.cle] === true} onchange={(e) => ecrirePropriete(c.cle, e.currentTarget.checked || null)} />
                {c.nom}
              </label>
            {:else if c.genre === "choix"}
              <label>
                <span>{c.nom}</span>
                <select class="champ" value={String(equipement.proprietes[c.cle] ?? "")} onchange={(e) => ecrirePropriete(c.cle, e.currentTarget.value || null)}>
                  <option value="">—</option>
                  {#each c.options ?? [] as o (o)}<option value={o}>{o}</option>{/each}
                </select>
              </label>
            {:else}
              <label>
                <span>{c.nom}{c.unite ? ` (${c.unite})` : ""}</span>
                <input
                  class="champ"
                  type={c.genre === "nombre" ? "number" : "text"}
                  placeholder={c.aide ?? ""}
                  value={String(equipement.proprietes[c.cle] ?? "")}
                  onchange={(e) => ecrirePropriete(c.cle, c.genre === "nombre" ? (e.currentTarget.value === "" ? null : Number(e.currentTarget.value)) : e.currentTarget.value.trim() || null)}
                />
              </label>
            {/if}
          {/each}
          {#if equipement.interfaces.some((i) => i.poe?.fournit)}
            <label>
              <span>Budget PoE (W)</span>
              <input class="champ" type="number" min="0" value={equipement.poeBudgetW ?? ""} onchange={(e) => modifier((x) => { const n = Number(e.currentTarget.value); if (n > 0) x.poeBudgetW = n; else delete x.poeBudgetW; })} />
            </label>
          {/if}
        </div>
      {/if}
      <label class="seul">
        <span>Description</span>
        <textarea class="champ" rows="3" value={equipement.description ?? ""} placeholder="Notes libres…" onchange={(e) => ecrireTexte("description", e.currentTarget.value)}></textarea>
      </label>
    {:else if onglet === "ports"}
      <PortsEquipement {editeur} {equipement} {changer} />
    {:else if onglet === "reseau"}
      <ReseauEquipement {editeur} {equipement} />
    {:else}
      <div class="champs">
        {#each INVENTAIRE as [cle, nom] (cle)}
          <label class:large={cle === "documentation" || cle === "referenceSecret"}>
            <span>{nom}</span>
            <input class="champ" value={String(equipement[cle] ?? "")} onchange={(e) => ecrireTexte(cle, e.currentTarget.value)} />
          </label>
        {/each}
        <label class="large">
          <span>Étiquettes (séparées par des virgules)</span>
          <input class="champ" value={equipement.tags.join(", ")} onchange={(e) => modifier((x) => (x.tags = e.currentTarget.value.split(",").map((t) => t.trim()).filter(Boolean).slice(0, 32)))} />
        </label>
        <label class="large">
          <span>Notes</span>
          <textarea class="champ" rows="3" value={equipement.notes ?? ""} onchange={(e) => ecrireTexte("notes", e.currentTarget.value)}></textarea>
        </label>
        <p class="aide large">Aucun mot de passe ni secret ici : seulement la référence où le trouver.</p>
      </div>
    {/if}
  </div>
</div>

{#if profil}
  <DialogueProfil
    {editeur}
    equipementId={equipement.id}
    modifier={profil.modifier}
    retirees={profil.retirees}
    onfermer={() => (profil = null)}
  />
{/if}

{#if aRetirer}
  <dialog
    class="confirmer"
    aria-labelledby="titre-retirer"
    {@attach (d: HTMLDialogElement) => d.showModal()}
    onclose={() => {
      aRetirer = false;
      void tick().then(() => boutonMenu?.focus());
    }}
  >
    <form
      method="dialog"
      onsubmit={(e) => {
        e.preventDefault();
        const id = equipement.id;
        const dialogue = e.currentTarget.closest("dialog");
        editeur.agir((s) => retirerEquipements(s, new Set([id])), "Équipement retiré, avec ses liens : Ctrl+Z pour annuler.");
        editeur.selection = [];
        dialogue?.close();
      }}
    >
      <h2 id="titre-retirer">Retirer « {equipement.nom} » ?</h2>
      <p class="aide">
        {cablesDeLEquipement === 0
          ? "L'équipement quitte le schéma."
          : `L'équipement quitte le schéma avec ${cablesDeLEquipement === 1 ? "son câble" : `ses ${cablesDeLEquipement} câbles`}.`}
        Ctrl+Z annule.
      </p>
      <div class="boutons-dialogue">
        <button type="button" class="bouton" onclick={(e) => e.currentTarget.closest("dialog")?.close()}>Annuler</button>
        <button type="submit" class="bouton principal danger">Retirer</button>
      </div>
    </form>
  </dialog>
{/if}

{#if aColler && copiee}
  {@const autres = schema.equipements
    .filter((e) => e.id !== copiee.source)
    .sort((a, b) => Number(b.type === copiee.type) - Number(a.type === copiee.type) || a.nom.localeCompare(b.nom, "fr"))}
  <dialog
    class="confirmer"
    aria-labelledby="titre-coller"
    {@attach (d: HTMLDialogElement) => d.showModal()}
    onclose={() => {
      aColler = false;
      void tick().then(() => boutonMenu?.focus());
    }}
  >
    <form
      method="dialog"
      onsubmit={(e) => {
        e.preventDefault();
        const dialogue = e.currentTarget.closest("dialog");
        const cibles = [...new FormData(e.currentTarget).getAll("cible")].map(String);
        if (cibles.length === 0) {
          editeur.annonce = "Choisissez au moins un équipement.";
          return;
        }
        let regles = 0;
        const manques: string[] = [];
        const fait = editeur.agir((s) => {
          for (const id of cibles) {
            const r = collerConfiguration(s, id, copiee);
            regles += r.regles;
            if (r.absents.length > 0) manques.push(`${s.equipements.find((x) => x.id === id)?.nom ?? "?"} (${r.absents.length})`);
          }
        });
        if (fait) {
          editeur.annonce =
            `Configuration de « ${copiee.nom} » collée sur ${cibles.length} ${cibles.length > 1 ? "équipements" : "équipement"} : ${regles} ${regles > 1 ? "ports réglés" : "port réglé"}.` +
            (manques.length > 0 ? ` Ports absents, laissés de côté : ${manques.join(", ")}.` : "");
          dialogue?.close();
        }
      }}
    >
      <h2 id="titre-coller">Coller la configuration de « {copiee.nom} »</h2>
      <p class="aide">
        Chaque port de même nom reprend le mode, les VLAN, l'activation et le PoE ; le VLAN de gestion suit. Les noms,
        les adresses et les câbles ne changent pas. Ctrl+Z annule.
      </p>
      {#if autres.length === 0}
        <p class="aide">Aucun autre équipement dans ce schéma.</p>
      {:else}
        <fieldset class="cibles">
          <legend class="visuellement-cache">Équipements qui reçoivent la configuration</legend>
          {#each autres as e (e.id)}
            <label class="case">
              <input type="checkbox" name="cible" value={e.id} checked={e.id === equipement.id} />
              <span>{e.nom}</span>
              <span class="type-cible">{definition(e.type).nom}</span>
            </label>
          {/each}
        </fieldset>
      {/if}
      <div class="boutons-dialogue">
        <button type="button" class="bouton" onclick={(e) => e.currentTarget.closest("dialog")?.close()}>Annuler</button>
        <button type="submit" class="bouton principal" disabled={autres.length === 0}>Coller</button>
      </div>
    </form>
  </dialog>
{/if}

<style>
  .proprietes {
    display: flex;
    flex-direction: column;
    height: 100%;
    min-height: 0;
  }

  .tete {
    position: relative;
    display: flex;
    flex: none;
    align-items: center;
    gap: 10px;
    padding: 12px 12px 8px;
  }

  .icone {
    flex: none;
    overflow: visible;
  }

  .identite {
    display: flex;
    flex: 1;
    flex-direction: column;
    align-items: flex-start;
    gap: 3px;
    min-width: 0;
  }

  .nom {
    max-width: 100%;
    margin: 0;
    overflow: hidden;
    font-size: 15px;
    font-weight: 700;
    line-height: 19px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .pastille-type {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    max-width: 100%;
    min-height: 24px;
    padding: 0 9px;
    border: 1px solid var(--bordure);
    border-radius: 999px;
    background: var(--surface);
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .pastille-type span:first-child {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .pastille-type:hover,
  .pastille-type[aria-expanded="true"] {
    border-color: var(--texte-secondaire);
    background: var(--surface-2);
    color: var(--texte);
  }

  .chevron {
    font-size: 10px;
  }

  .statut {
    position: relative;
    display: inline-flex;
    flex: none;
    flex-direction: row;
    align-items: center;
    gap: 6px;
    min-height: 28px;
    padding: 0 6px;
    border-radius: var(--rayon);
    font-size: 12px;
  }

  .statut:hover,
  .statut:focus-within {
    background: var(--surface-2);
  }

  .statut select {
    max-width: 112px;
    field-sizing: content;
    padding: 0;
    border: none;
    background: transparent;
    color: var(--texte);
    font-size: 12px;
    appearance: none;
    cursor: pointer;
  }

  .statut select:focus-visible {
    outline: none;
  }

  .statut:has(select:focus-visible) {
    outline: 2px solid var(--focus);
  }

  .statut option {
    background: var(--surface);
    color: var(--texte);
  }

  .point {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--texte-secondaire);
  }

  .statut-actif .point {
    background: var(--succes);
  }

  .statut-maintenance .point {
    background: var(--avertissement);
  }

  .statut-hors-service .point {
    background: var(--retard);
  }

  .plus {
    display: grid;
    flex: none;
    width: 30px;
    height: 30px;
    padding: 0;
    border: 1px solid transparent;
    border-radius: var(--rayon);
    background: transparent;
    color: var(--texte-secondaire);
    font-size: 17px;
    letter-spacing: 1px;
    place-items: center;
  }

  .plus:hover,
  .plus[aria-expanded="true"] {
    background: var(--surface-2);
    color: var(--texte);
  }

  .menu {
    position: absolute;
    top: calc(100% - 6px);
    right: 10px;
    z-index: 20;
    display: flex;
    flex-direction: column;
    min-width: 220px;
    padding: 5px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    box-shadow: 0 10px 28px rgb(0 0 0 / 0.18);
  }

  .menu button {
    display: flex;
    align-items: center;
    gap: 8px;
    min-height: 32px;
    padding: 0 10px;
    border: none;
    border-radius: var(--rayon);
    background: transparent;
    color: var(--texte);
    font-size: 13px;
    text-align: left;
    white-space: nowrap;
  }

  .menu button:hover:not(:disabled),
  .menu button:focus-visible {
    background: var(--surface-2);
  }

  .menu button:disabled {
    color: var(--texte-secondaire);
    opacity: 0.6;
  }

  .menu .danger {
    color: var(--retard);
  }

  .menu hr {
    width: calc(100% - 12px);
    margin: 4px 6px;
    border: none;
    border-top: 1px solid var(--bordure);
  }

  .touche {
    margin-left: auto;
    padding-left: 16px;
    color: var(--texte-secondaire);
    font-size: 11px;
  }

  .onglets {
    display: flex;
    flex: none;
    gap: 2px;
    padding: 0 8px;
    overflow-x: auto;
    border-bottom: 1px solid var(--bordure);
    scrollbar-width: none;
  }

  .onglets button {
    display: inline-flex;
    flex: none;
    align-items: baseline;
    gap: 4px;
    min-height: 34px;
    padding: 0 8px;
    border: none;
    border-radius: var(--rayon) var(--rayon) 0 0;
    background: transparent;
    color: var(--texte-secondaire);
    font-size: 13px;
    white-space: nowrap;
  }

  .onglets button:hover {
    color: var(--texte);
  }

  .onglets button[aria-selected="true"] {
    color: var(--texte);
    font-weight: 600;
    box-shadow: inset 0 -2px 0 var(--principale);
  }

  .onglets button:focus-visible {
    outline-offset: -2px;
  }

  .compte {
    color: var(--texte-secondaire);
    font-size: 11px;
    font-weight: 400;
  }

  .corps {
    flex: 1;
    min-height: 0;
    padding: 12px;
    overflow-y: auto;
  }

  .titre-section {
    margin: 4px 0 6px;
    color: var(--texte-secondaire);
    font-size: 11px;
    font-weight: 700;
    letter-spacing: 0.02em;
    text-transform: uppercase;
  }

  .champs {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 8px;
    margin-bottom: 10px;
  }

  label {
    display: flex;
    flex-direction: column;
    gap: 3px;
    min-width: 0;
    font-size: 12px;
  }

  label > span {
    color: var(--texte-secondaire);
  }

  .champ {
    height: 32px;
    font-size: 13px;
  }

  textarea.champ {
    height: auto;
    padding: 5px 10px;
    resize: vertical;
  }

  .large {
    grid-column: 1 / -1;
  }

  .case {
    flex-direction: row;
    align-items: center;
    gap: 6px;
  }

  .aide {
    margin: 2px 0 6px;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  dialog.confirmer {
    width: min(460px, calc(100vw - 32px));
    padding: 18px 20px;
  }

  dialog.confirmer h2 {
    margin-bottom: 8px;
    font-size: 17px;
  }

  dialog.confirmer .aide {
    font-size: 13px;
  }

  .cibles {
    display: flex;
    flex-direction: column;
    gap: 2px;
    max-height: 260px;
    margin: 8px 0 0;
    padding: 4px;
    overflow-y: auto;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
  }

  .cibles .case {
    min-height: 30px;
    padding: 0 6px;
    border-radius: 6px;
    font-size: 13px;
  }

  .cibles .case:hover {
    background: var(--surface-2);
  }

  .cibles .case > span:first-of-type {
    color: var(--texte);
  }

  .type-cible {
    margin-left: auto;
    overflow: hidden;
    color: var(--texte-secondaire);
    font-size: 12px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .boutons-dialogue {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 14px;
  }

  .bouton.danger {
    border-color: var(--retard);
    background: var(--retard);
  }

  .bouton.danger:hover {
    border-color: var(--retard);
    background: var(--retard);
    filter: brightness(0.92);
  }
</style>
