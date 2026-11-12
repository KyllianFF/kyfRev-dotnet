<script lang="ts">
  import Archive from "@lucide/svelte/icons/archive";
  import ArchiveRestore from "@lucide/svelte/icons/archive-restore";
  import ArrowLeft from "@lucide/svelte/icons/arrow-left";
  import Copy from "@lucide/svelte/icons/copy";
  import Eye from "@lucide/svelte/icons/eye";
  import FileText from "@lucide/svelte/icons/file-text";
  import PenLine from "@lucide/svelte/icons/pen-line";
  import Plus from "@lucide/svelte/icons/plus";
  import Search from "@lucide/svelte/icons/search";
  import Trash2 from "@lucide/svelte/icons/trash-2";
  import Undo2 from "@lucide/svelte/icons/undo-2";
  import Upload from "@lucide/svelte/icons/upload";
  import { onMount } from "svelte";
  import { api, messageErreur, type SchemaRange } from "../../lib/api";
  import { momentCourt, pluriel } from "../../lib/format";
  import type { SchemaResume } from "../../lib/generes/SchemaResume";
  import { apercuSchema } from "../../lib/topologie/apercu";
  import { definition } from "../../lib/topologie/catalogue";
  import { adresseAffichee, couleurDe, FAMILLES, familleDe } from "../../lib/topologie/dessin";
  import { diagnostiquer } from "../../lib/topologie/diagnostic";
  import { ecrireSchema, lireSchema } from "../../lib/topologie/format";
  import { type IdVue, type Schema, STATUTS, schemaVide } from "../../lib/topologie/modele";
  import { VUES, vue as vueDe } from "../../lib/topologie/vues";
  import DialogueSaisie from "../DialogueSaisie.svelte";
  import EditeurTopologie from "./EditeurTopologie.svelte";

  // Vue « Schémas réseau » (D40) : tous les schémas du coffre (schemas/*.json),
  // chacun avec son aperçu ; un clic montre le détail (grand aperçu dans la
  // vue choisie, inventaire, constats, fiches qui le citent) sans ouvrir
  // l'éditeur ; « Modifier » l'ouvre. Un schéma s'archive ou se supprime par
  // ses boutons comme au clic droit ; rien ne s'efface : les archives et la
  // corbeille se lisent sous la liste, et tout s'y restaure.
  let { onouvrirfiche }: { onouvrirfiche: (id: string) => void } = $props();

  type Detail = { schema: Schema; erreur?: undefined } | { erreur: string; schema?: undefined };
  let liste = $state<SchemaResume[] | null>(null);
  let details = $state<Record<string, Detail>>({});
  let erreur = $state<string | null>(null);
  let message = $state<string | null>(null);
  let recherche = $state("");
  let lu = $state<string | null>(null);
  let vueLue = $state<IdVue>("physique");
  let edite = $state<string | null>(null);
  let creation = $state(false);
  let occupe = $state(false);
  let ranges = $state<SchemaRange[]>([]);
  let aSupprimer = $state<SchemaResume | null>(null);
  let menu = $state<{ schema: SchemaResume; x: number; y: number } | null>(null);
  const archives = $derived(ranges.filter((s) => s.etat === "archive"));
  const corbeille = $derived(ranges.filter((s) => s.etat === "corbeille"));

  async function charger() {
    try {
      liste = await api.schemas();
      ranges = await api.schemasRanges();
      erreur = null;
      for (const s of liste) {
        try {
          details[s.id] = { schema: lireSchema((await api.lireSchema(s.id)).texte).schema };
        } catch (e) {
          details[s.id] = { erreur: messageErreur(e) };
        }
      }
    } catch (e) {
      erreur = messageErreur(e);
    }
  }

  onMount(() => {
    void charger();
    // L'application ouvre un schéma depuis une fiche.
    const ouvrir = (evenement: Event) => {
      edite = (evenement as CustomEvent<string>).detail;
    };
    document.addEventListener("ouvrir-schema", ouvrir);
    return () => document.removeEventListener("ouvrir-schema", ouvrir);
  });

  const sansAccents = (t: string) => t.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase();
  const filtres = $derived.by(() => {
    const mots = sansAccents(recherche).split(/\s+/).filter(Boolean);
    return (liste ?? []).filter((s) => {
      const schema = details[s.id]?.schema;
      const texte = sansAccents(
        `${s.titre} ${schema?.description ?? ""} ${s.fiches.map((f) => f.titre).join(" ")} ${schema?.equipements.map((e) => `${e.nom} ${e.gestion?.adresse ?? ""}`).join(" ") ?? ""}`,
      );
      return mots.every((m) => texte.includes(m));
    });
  });

  const resumeLu = $derived(lu ? (liste ?? []).find((s) => s.id === lu) : undefined);
  const schemaLu = $derived(lu ? details[lu]?.schema : undefined);
  const constats = $derived(schemaLu ? diagnostiquer(schemaLu) : []);
  const inventaire = $derived(
    schemaLu
      ? [...schemaLu.equipements].sort(
          (a, b) =>
            FAMILLES.findIndex((f) => f.valeur === familleDe(a.type)) -
              FAMILLES.findIndex((f) => f.valeur === familleDe(b.type)) || a.nom.localeCompare(b.nom, "fr"),
        )
      : [],
  );

  function lire(id: string) {
    lu = id;
    vueLue = details[id]?.schema?.vue ?? "physique";
  }

  async function agir(action: () => Promise<string>) {
    occupe = true;
    message = null;
    try {
      message = await action();
      await charger();
    } catch (e) {
      erreur = messageErreur(e);
    } finally {
      occupe = false;
    }
  }

  const creer = (titre: string) =>
    agir(async () => {
      const cree = await api.creerSchema(ecrireSchema(schemaVide(titre)));
      edite = cree.id;
      return `Schéma « ${titre} » créé.`;
    });

  const dupliquer = (id: string) =>
    agir(async () => {
      const copie = await api.dupliquerSchema(id);
      return `Copie créée : « ${lireSchema(copie.texte).schema.titre} ».`;
    });

  /** Range un schéma (archives, corbeille, ou retour dans la liste) et le dit. */
  const ranger = (s: SchemaResume, etat: "actif" | "archive" | "corbeille", fait: string) =>
    agir(async () => {
      await api.rangerSchema(s.id, etat);
      if (lu === s.id) lu = null;
      return `Schéma « ${s.titre} » ${fait}`;
    });
  const archiver = (s: SchemaResume) => ranger(s, "archive", "archivé. Les fiches qui l'insèrent le montrent toujours.");
  const supprimer = (s: SchemaResume) => ranger(s, "corbeille", "mis à la corbeille : il s'y restaure.");
  const restaurer = (s: SchemaResume) => ranger(s, "actif", "remis dans la liste.");

  function ouvrirMenu(evenement: MouseEvent, schema: SchemaResume) {
    evenement.preventDefault();
    // Au clavier (touche Menu), l'évènement n'a pas de position : le menu s'ouvre sur la carte.
    const cadre = (evenement.currentTarget as HTMLElement).getBoundingClientRect();
    const auClavier = evenement.clientX === 0 && evenement.clientY === 0;
    menu = {
      schema,
      x: Math.min(auClavier ? cadre.left + 24 : evenement.clientX, window.innerWidth - 220),
      y: Math.min(auClavier ? cadre.top + 24 : evenement.clientY, window.innerHeight - 230),
    };
  }

  function choisir(action: (s: SchemaResume) => unknown) {
    const schema = menu?.schema;
    menu = null;
    if (schema) action(schema);
  }

  /** Le menu prend le clavier à l'ouverture ; les flèches passent d'une entrée à l'autre, Échap le ferme. */
  function clavierMenu(liste: HTMLElement) {
    const entrees = () => [...liste.querySelectorAll<HTMLButtonElement>("button")];
    entrees()[0]?.focus();
    const touche = (evenement: KeyboardEvent) => {
      const tous = entrees();
      const ici = tous.indexOf(document.activeElement as HTMLButtonElement);
      if (evenement.key === "Escape") menu = null;
      else if (evenement.key === "ArrowDown") tous[(ici + 1) % tous.length]?.focus();
      else if (evenement.key === "ArrowUp") tous[(ici - 1 + tous.length) % tous.length]?.focus();
      else return;
      evenement.preventDefault();
    };
    liste.addEventListener("keydown", touche);
    return { destroy: () => liste.removeEventListener("keydown", touche) };
  }

  const ouvrirDialogue = (dialogue: HTMLDialogElement) => dialogue.showModal();

  async function importer() {
    const chemin = await api.choisirFichierSchema();
    if (!chemin) return;
    await agir(async () => {
      const lecture = lireSchema(await api.lireImportSchema(chemin));
      if (lecture.erreurs.length > 0) {
        throw new Error(
          `Import refusé : ${lecture.erreurs.length} erreur(s) de structure, dont « ${lecture.erreurs[0]} ». Rien n'a été créé.`,
        );
      }
      await api.creerSchema(ecrireSchema({ ...lecture.schema, id: "" }));
      return `Schéma « ${lecture.schema.titre} » importé.`;
    });
  }

  const nombre = (schema: Schema | undefined, quoi: "equipements" | "liens" | "relations") =>
    schema?.[quoi].length ?? 0;

  /**
   * Vignette recadrée sur le dessin : le cadre du schéma garde de larges
   * marges (place prévue pour les étiquettes), qui rapetissaient les
   * équipements dans une vignette plus large que haute.
   */
  function recadrer(bouton: HTMLElement, _schema: Schema | undefined) {
    const appliquer = () => {
      const svg = bouton.querySelector("svg");
      if (!svg) return;
      const boite = svg.getBBox();
      if (boite.width <= 0 || boite.height <= 0) return;
      const marge = 12;
      svg.setAttribute(
        "viewBox",
        `${boite.x - marge} ${boite.y - marge} ${boite.width + 2 * marge} ${boite.height + 2 * marge}`,
      );
      svg.setAttribute("width", String(Math.round(boite.width + 2 * marge)));
      svg.setAttribute("height", String(Math.round(boite.height + 2 * marge)));
    };
    appliquer();
    return { update: appliquer };
  }
</script>

<div class="vue">
  {#if lu && resumeLu}
    <header class="entete-lecture">
      <button type="button" class="bouton discret" onclick={() => (lu = null)}>
        <ArrowLeft size={16} aria-hidden="true" /> Tous les schémas
      </button>
      <h1>{resumeLu.titre}</h1>
      <div class="actions">
        <label>
          <span class="visuellement-cache">Vue</span>
          <select class="champ" bind:value={vueLue}>
            {#each VUES as v (v.id)}<option value={v.id}>Vue {v.nom.toLowerCase()}</option>{/each}
          </select>
        </label>
        <button type="button" class="bouton" disabled={occupe} onclick={() => dupliquer(resumeLu.id)}>
          <Copy size={16} aria-hidden="true" /> Dupliquer
        </button>
        <button type="button" class="bouton" disabled={occupe} onclick={() => archiver(resumeLu)}>
          <Archive size={16} aria-hidden="true" /> Archiver
        </button>
        <button type="button" class="bouton" disabled={occupe} onclick={() => (aSupprimer = resumeLu)}>
          <Trash2 size={16} aria-hidden="true" /> Supprimer
        </button>
        <button type="button" class="bouton principal" onclick={() => (edite = resumeLu.id)}>
          <PenLine size={16} aria-hidden="true" /> Modifier
        </button>
      </div>
    </header>
    {#if schemaLu}
      {#if schemaLu.description}<p class="description">{schemaLu.description}</p>{/if}
      <figure class="grand-apercu" aria-label={`Aperçu du schéma « ${resumeLu.titre} »`}>
        {@html apercuSchema(schemaLu, vueDe(vueLue), true)}
      </figure>
      <div class="colonnes">
        <section aria-labelledby="titre-inventaire">
          <h2 id="titre-inventaire">Inventaire <span class="compte">{schemaLu.equipements.length}</span></h2>
          <table>
            <thead>
              <tr><th scope="col">Équipement</th><th scope="col">Type</th><th scope="col">Adresse</th><th scope="col">Statut</th><th scope="col" class="nombre">Ports</th></tr>
            </thead>
            <tbody>
              {#each inventaire as e (e.id)}
                <tr>
                  <th scope="row"><span class="pastille-famille" style:background={couleurDe(e)}></span>{e.nom}</th>
                  <td>{definition(e.type).nom}{e.membres.length > 1 ? ` (stack ×${e.membres.length})` : ""}</td>
                  <td class="code">{adresseAffichee(e)}</td>
                  <td>{STATUTS.find((s) => s.valeur === e.statut)?.nom}</td>
                  <td class="nombre">{e.interfaces.length}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </section>
        <section aria-labelledby="titre-synthese">
          <h2 id="titre-synthese">Synthèse</h2>
          <ul class="synthese">
            <li>{pluriel(nombre(schemaLu, "liens"), "lien physique", "liens physiques")}</li>
            <li>{pluriel(nombre(schemaLu, "relations"), "relation logique", "relations logiques")}</li>
            <li>{pluriel(schemaLu.vlans.length, "VLAN")}, {pluriel(schemaLu.groupes.length, "groupe")}</li>
            <li class:alerte={constats.some((c) => c.niveau !== "manque")}>
              {pluriel(constats.filter((c) => c.niveau !== "manque").length, "point à vérifier", "points à vérifier")},
              {pluriel(constats.filter((c) => c.niveau === "manque").length, "information manquante", "informations manquantes")}
            </li>
            {#if resumeLu.modifieLe}<li>Modifié {momentCourt(resumeLu.modifieLe)}</li>{/if}
          </ul>
          <h2>Cité dans</h2>
          {#if resumeLu.fiches.length === 0}
            <p class="aide">Aucune fiche : insérez-le depuis l'écriture d'une fiche (Insérer › Diagramme › Schéma réseau).</p>
          {:else}
            <ul class="fiches">
              {#each resumeLu.fiches as f (f.id)}
                <li><button type="button" class="lien-fiche" onclick={() => onouvrirfiche(f.id)}><FileText size={14} aria-hidden="true" /> {f.titre}</button></li>
              {/each}
            </ul>
          {/if}
        </section>
      </div>
    {:else}
      <p class="bandeau erreur" role="alert">{details[resumeLu.id]?.erreur ?? "Chargement…"}</p>
    {/if}
  {:else}
    <header>
      <div>
        <h1>Schémas réseau</h1>
        {#if liste}
          <p class="resume">
            {pluriel(liste.length, "schéma")} dans le coffre (dossier « schemas »). Cliquez sur un aperçu pour le détail,
            « Modifier » pour l'éditeur.
          </p>
        {/if}
      </div>
      <div class="actions">
        <button type="button" class="bouton" disabled={occupe} onclick={importer}><Upload size={16} aria-hidden="true" /> Importer…</button>
        <button type="button" class="bouton principal" disabled={occupe} onclick={() => (creation = true)}>
          <Plus size={16} aria-hidden="true" /> Nouveau schéma
        </button>
      </div>
    </header>
    {#if erreur}<p class="bandeau erreur" role="alert">{erreur}</p>{/if}
    {#if message}<p class="bandeau succes" role="status">{message}</p>{/if}
    {#if liste === null}
      <p class="vide" aria-busy="true">Chargement des schémas…</p>
    {:else if liste.length === 0}
      <p class="vide">
        {ranges.length === 0
          ? "Aucun schéma réseau pour l'instant. Créez-en un ici, ou depuis une fiche (Écrire › Insertion › Réseau)."
          : "Aucun schéma dans la liste : ceux que vous avez archivés ou supprimés sont plus bas."}
      </p>
    {:else}
      <label class="recherche">
        <Search size={16} aria-hidden="true" />
        <span class="visuellement-cache">Chercher un schéma</span>
        <input class="champ" type="search" placeholder="Titre, équipement, adresse, fiche…" bind:value={recherche} />
      </label>
      <ul class="grille">
        {#each filtres as s (s.id)}
          {@const schema = details[s.id]?.schema}
          <!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
          <li class="carte" oncontextmenu={(e) => ouvrirMenu(e, s)}>
            <button
              type="button"
              class="apercu"
              aria-label={`Voir le détail de « ${s.titre} »`}
              onclick={() => lire(s.id)}
              use:recadrer={schema}
            >
              {#if schema && schema.equipements.length > 0}
                {@html apercuSchema(schema)}
              {:else if schema}
                <span class="vide-apercu">Schéma vide</span>
              {:else if details[s.id]?.erreur}
                <span class="vide-apercu erreur">{details[s.id]?.erreur}</span>
              {:else}
                <span class="vide-apercu">…</span>
              {/if}
            </button>
            <div class="infos">
              <h2 class="titre">{s.titre}</h2>
              <p class="meta">
                {pluriel(nombre(schema, "equipements"), "équipement")} · {pluriel(nombre(schema, "liens"), "lien")}
                {#if s.modifieLe}· modifié {momentCourt(s.modifieLe)}{/if}
              </p>
              <p class="meta">
                {s.fiches.length === 0 ? "Cité dans aucune fiche" : `Cité dans ${s.fiches.map((f) => f.titre).join(", ")}`}
              </p>
              <div class="boutons">
                <button type="button" class="bouton petit" onclick={() => lire(s.id)}><Eye size={14} aria-hidden="true" /> Détail</button>
                <button type="button" class="bouton petit" onclick={() => (edite = s.id)}><PenLine size={14} aria-hidden="true" /> Modifier</button>
                <button type="button" class="bouton petit" disabled={occupe} onclick={() => dupliquer(s.id)}><Copy size={14} aria-hidden="true" /> Dupliquer</button>
                <button type="button" class="bouton petit" disabled={occupe} onclick={() => archiver(s)}><Archive size={14} aria-hidden="true" /> Archiver</button>
                <button type="button" class="bouton petit" disabled={occupe} onclick={() => (aSupprimer = s)}><Trash2 size={14} aria-hidden="true" /> Supprimer</button>
              </div>
            </div>
          </li>
        {/each}
      </ul>
      {#if filtres.length === 0}<p class="vide">Aucun schéma ne correspond.</p>{/if}
    {/if}
    {#if archives.length > 0}
      <details class="ranges">
        <summary>Archives <span class="compte">{archives.length}</span></summary>
        <p class="aide">Hors de la liste, mais les fiches qui les insèrent les montrent toujours.</p>
        <ul>
          {#each archives as s (s.id)}
            <li>
              <span class="nom">{s.titre}</span>
              <span class="meta">{pluriel(s.equipements, "équipement")}{s.fiches.length > 0 ? ` · cité dans ${s.fiches.map((f) => f.titre).join(", ")}` : ""}</span>
              <button type="button" class="bouton petit" disabled={occupe} onclick={() => restaurer(s)}><ArchiveRestore size={14} aria-hidden="true" /> Désarchiver</button>
              <button type="button" class="bouton petit" disabled={occupe} onclick={() => (aSupprimer = s)}><Trash2 size={14} aria-hidden="true" /> Supprimer</button>
            </li>
          {/each}
        </ul>
      </details>
    {/if}
    {#if corbeille.length > 0}
      <details class="ranges">
        <summary>Corbeille <span class="compte">{corbeille.length}</span></summary>
        <p class="aide">Rien n'est effacé : un schéma supprimé reste ici, et se restaure tel qu'il était.</p>
        <ul>
          {#each corbeille as s (s.id)}
            <li>
              <span class="nom">{s.titre}</span>
              <span class="meta">{pluriel(s.equipements, "équipement")}{s.fiches.length > 0 ? ` · manque à ${s.fiches.map((f) => f.titre).join(", ")}` : ""}</span>
              <button type="button" class="bouton petit" disabled={occupe} onclick={() => restaurer(s)}><Undo2 size={14} aria-hidden="true" /> Restaurer</button>
            </li>
          {/each}
        </ul>
      </details>
    {/if}
  {/if}
</div>

<svelte:window onclick={() => (menu = null)} onblur={() => (menu = null)} onresize={() => (menu = null)} />

{#if menu}
  <div class="menu" role="menu" aria-label={`Actions sur « ${menu.schema.titre} »`} style:left={`${menu.x}px`} style:top={`${menu.y}px`} use:clavierMenu>
    <button type="button" role="menuitem" onclick={() => choisir((s) => lire(s.id))}><Eye size={16} aria-hidden="true" /> Détail</button>
    <button type="button" role="menuitem" onclick={() => choisir((s) => (edite = s.id))}><PenLine size={16} aria-hidden="true" /> Modifier</button>
    <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir((s) => dupliquer(s.id))}><Copy size={16} aria-hidden="true" /> Dupliquer</button>
    <hr />
    <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir(archiver)}><Archive size={16} aria-hidden="true" /> Archiver</button>
    <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir((s) => (aSupprimer = s))}><Trash2 size={16} aria-hidden="true" /> Supprimer</button>
  </div>
{/if}

{#if aSupprimer}
  {@const s = aSupprimer}
  <dialog class="confirmation" aria-labelledby="titre-suppression" use:ouvrirDialogue onclose={() => (aSupprimer = null)}>
    <h2 id="titre-suppression">Supprimer « {s.titre} » ?</h2>
    <p>Le schéma va à la corbeille des schémas, en bas de cette page : il s'y restaure tel qu'il est.</p>
    {#if s.fiches.length > 0}
      <p class="alerte">
        {pluriel(s.fiches.length, "fiche l'insère", "fiches l'insèrent")} ({s.fiches.map((f) => f.titre).join(", ")}) : le
        schéma y manquera tant qu'il ne sera pas restauré. Pour le retirer de la liste sans le retirer des fiches, archivez-le.
      </p>
    {/if}
    <div class="actions fin">
      <!-- svelte-ignore a11y_autofocus -->
      <button type="button" class="bouton" autofocus onclick={(e) => e.currentTarget.closest("dialog")?.close()}>Annuler</button>
      <button
        type="button"
        class="bouton principal"
        onclick={(e) => {
          e.currentTarget.closest("dialog")?.close();
          void supprimer(s);
        }}
      >
        Supprimer
      </button>
    </div>
  </dialog>
{/if}

{#if creation}
  <DialogueSaisie
    titre="Nouveau schéma réseau"
    libelle="Nom du schéma"
    valeur=""
    libelleValider="Créer"
    verifier={(nom) => (nom.trim() ? null : "Un schéma doit avoir un nom.")}
    onvalider={(nom) => creer(nom.trim())}
    onfermer={() => (creation = false)}
  />
{/if}

{#if edite}
  {#key edite}
    <EditeurTopologie
      id={edite}
      onfermer={() => {
        edite = null;
        void charger();
        void api.editeurFerme();
      }}
    />
  {/key}
{/if}

<style>
  .vue {
    max-width: 1200px;
    padding: 24px 32px 48px;
  }

  header {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-start;
    justify-content: space-between;
    gap: 12px;
  }

  h1 {
    margin: 0;
    font-size: 26px;
  }

  .resume,
  .vide,
  .aide,
  .description {
    color: var(--texte-secondaire);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
  }

  .actions .champ {
    width: auto;
  }

  .bandeau {
    padding: 10px 14px;
    border-radius: var(--rayon);
  }

  .erreur {
    background: var(--retard-fond);
    color: var(--retard);
  }

  .succes {
    background: var(--succes-fond);
    color: var(--succes);
  }

  .recherche {
    position: relative;
    display: flex;
    align-items: center;
    max-width: 420px;
    margin: 16px 0;
  }

  .recherche :global(svg) {
    position: absolute;
    left: 10px;
    color: var(--texte-secondaire);
  }

  .recherche input {
    padding-left: 32px;
  }

  .grille {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
    gap: 16px;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .carte {
    display: flex;
    flex-direction: column;
    overflow: hidden;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
  }

  .apercu {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 220px;
    padding: 12px;
    border: none;
    border-bottom: 1px solid var(--bordure);
    background: var(--fond);
    cursor: zoom-in;
  }

  .apercu :global(svg) {
    max-width: 100%;
    max-height: 100%;
    width: auto;
    height: auto;
  }

  .apercu:hover,
  .apercu:focus-visible {
    background: var(--surface-2);
  }

  .vide-apercu {
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .infos {
    display: flex;
    flex-direction: column;
    gap: 2px;
    padding: 10px 12px 12px;
  }

  .titre {
    margin: 0;
    font-size: 15px;
  }

  .meta {
    margin: 0;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .boutons {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    margin-top: 8px;
  }

  .bouton.petit {
    height: 28px;
    padding: 0 10px;
    font-size: 12px;
  }

  .entete-lecture {
    align-items: center;
  }

  .ranges {
    margin-top: 24px;
    padding: 10px 14px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
  }

  .ranges summary {
    font-weight: 600;
    cursor: pointer;
  }

  .ranges .aide {
    margin: 6px 0 8px;
    font-size: 13px;
  }

  .ranges ul {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .ranges li {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
    padding: 6px 0;
    border-top: 1px solid var(--bordure);
  }

  .ranges .nom {
    font-weight: 600;
  }

  .ranges .meta {
    flex: 1 1 160px;
  }

  .menu {
    position: fixed;
    z-index: 30;
    display: flex;
    flex-direction: column;
    min-width: 200px;
    padding: 4px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--surface);
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.18);
  }

  .menu button {
    display: flex;
    align-items: center;
    gap: 8px;
    height: 32px;
    padding: 0 10px;
    border: none;
    border-radius: var(--rayon);
    background: none;
    color: var(--texte);
    font: inherit;
    text-align: left;
    cursor: pointer;
  }

  .menu button:hover:not(:disabled),
  .menu button:focus-visible {
    background: var(--surface-2);
  }

  .menu button:disabled {
    opacity: 0.5;
    cursor: default;
  }

  .menu hr {
    width: 100%;
    margin: 4px 0;
    border: none;
    border-top: 1px solid var(--bordure);
  }

  .confirmation {
    width: min(480px, calc(100vw - 32px));
    padding: 24px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 16px 48px rgb(0 0 0 / 0.2);
  }

  .confirmation::backdrop {
    background: var(--voile);
  }

  .confirmation h2 {
    margin-bottom: 12px;
    font-size: 18px;
  }

  .confirmation p {
    margin: 0 0 10px;
  }

  .actions.fin {
    justify-content: flex-end;
    margin-top: 16px;
  }

  .entete-lecture h1 {
    flex: 1 1 200px;
  }

  .grand-apercu {
    display: flex;
    justify-content: center;
    margin: 16px 0;
    padding: 16px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    overflow: auto;
  }

  .grand-apercu :global(svg) {
    max-width: 100%;
    height: auto;
  }

  .colonnes {
    display: grid;
    grid-template-columns: minmax(0, 2fr) minmax(240px, 1fr);
    gap: 24px;
  }

  h2 {
    margin: 0 0 8px;
    font-size: 16px;
  }

  .compte {
    color: var(--texte-secondaire);
    font-weight: 400;
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 13px;
  }

  th,
  td {
    padding: 6px 8px;
    border-bottom: 1px solid var(--bordure);
    text-align: left;
  }

  thead th {
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  tbody th {
    font-weight: 600;
    white-space: nowrap;
  }

  .code {
    font-family: var(--police-code);
    font-size: 12px;
  }

  .nombre {
    text-align: right;
  }

  .pastille-famille {
    display: inline-block;
    width: 10px;
    height: 10px;
    margin-right: 6px;
    border-radius: 3px;
  }

  .synthese,
  .fiches {
    margin: 0 0 16px;
    padding-left: 18px;
    font-size: 13px;
  }

  .synthese li,
  .fiches li {
    margin-bottom: 4px;
  }

  .alerte {
    color: var(--avertissement);
  }

  .fiches {
    padding: 0;
    list-style: none;
  }

  .lien-fiche {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 0;
    border: none;
    background: none;
    color: var(--lien);
    cursor: pointer;
  }

  @media (max-width: 900px) {
    .colonnes {
      grid-template-columns: minmax(0, 1fr);
    }
  }
</style>
