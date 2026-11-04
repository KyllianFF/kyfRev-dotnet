<script lang="ts">
  import ArrowLeft from "@lucide/svelte/icons/arrow-left";
  import Copy from "@lucide/svelte/icons/copy";
  import Eye from "@lucide/svelte/icons/eye";
  import FileText from "@lucide/svelte/icons/file-text";
  import PenLine from "@lucide/svelte/icons/pen-line";
  import Plus from "@lucide/svelte/icons/plus";
  import Search from "@lucide/svelte/icons/search";
  import Upload from "@lucide/svelte/icons/upload";
  import { onMount } from "svelte";
  import { api, messageErreur } from "../../lib/api";
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
  // l'éditeur ; « Modifier » l'ouvre.
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

  async function charger() {
    try {
      liste = await api.schemas();
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

  onMount(charger);

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
        Aucun schéma réseau pour l'instant. Créez-en un ici, ou depuis une fiche (Écrire › Insérer › Diagramme ›
        Schéma réseau).
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
          <li class="carte">
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
              </div>
            </div>
          </li>
        {/each}
      </ul>
      {#if filtres.length === 0}<p class="vide">Aucun schéma ne correspond.</p>{/if}
    {/if}
  {/if}
</div>

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
