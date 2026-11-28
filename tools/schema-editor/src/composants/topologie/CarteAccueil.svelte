<script lang="ts">
  import Ellipsis from "@lucide/svelte/icons/ellipsis";
  import PenLine from "@lucide/svelte/icons/pen-line";
  import Pin from "@lucide/svelte/icons/pin";
  import { pluriel } from "../../lib/format";
  import { type CarteSchema, quand } from "../../lib/topologie/accueil";
  import { apercuSchema } from "../../lib/topologie/apercu";
  import type { Schema } from "../../lib/topologie/modele";
  import { pagesDe } from "../../lib/topologie/pages";
  import NomEditable from "./NomEditable.svelte";

  // Une carte de l'accueil (31A) : la vignette, le nom, une ligne, de petites étiquettes. Un clic ouvre le détail ;
  // « Modifier » et « ⋯ » paraissent au survol et quand la carte a le clavier ; la case (31B) aussi, et reste dès qu'un
  // schéma est choisi. Les pages se voient en petits traits sur la vignette, qui les fait défiler au survol.
  let {
    carte,
    schema,
    erreur,
    fiches,
    choisie,
    selection,
    edition,
    onclic,
    oncase,
    onmodifier,
    onplus,
    onmenu,
    onrenommer,
    onvalider,
    onannuler,
  }: {
    carte: CarteSchema;
    schema: Schema | undefined;
    erreur: string | undefined;
    /** Les titres des fiches qui l'insèrent. */
    fiches: string[];
    choisie: boolean;
    /** Au moins un schéma est choisi : les cases restent visibles. */
    selection: boolean;
    edition: boolean;
    /** Un clic sur la carte, avec ses touches tenues. */
    onclic: (evenement: MouseEvent) => void;
    /** La case cochée ou décochée, avec ses touches tenues (Maj prend une suite). */
    oncase: (evenement: MouseEvent) => void;
    onmodifier: () => void;
    onplus: (evenement: MouseEvent) => void;
    onmenu: (evenement: MouseEvent) => void;
    onrenommer: () => void;
    onvalider: (nom: string) => void;
    onannuler: () => void;
  } = $props();

  /** Défilement des pages au survol : une page de plus toutes les 900 ms. */
  const PAS = 900;
  let rang = $state(0);
  let minuterie: ReturnType<typeof setInterval> | undefined;
  const pages = $derived(schema && carte.pages > 1 ? pagesDe(schema) : []);
  const page = $derived(pages.length > 1 ? pages[rang % pages.length]?.id : undefined);
  const dessin = $derived(schema && schema.equipements.length > 0 ? apercuSchema(schema, undefined, false, page) : "");

  function survoler() {
    clearInterval(minuterie);
    if (pages.length < 2) return;
    minuterie = setInterval(() => (rang = (rang + 1) % pages.length), PAS);
  }

  function quitter() {
    clearInterval(minuterie);
    rang = 0;
  }

  $effect(() => () => clearInterval(minuterie));

  const meta = $derived(
    [
      pluriel(carte.equipements, "équipement"),
      carte.pages > 1 ? pluriel(carte.pages, "page") : "",
      carte.modifieLe ? `modifié ${quand(carte.modifieLe)}` : "",
    ]
      .filter(Boolean)
      .join(" · "),
  );

  /** Un clic sur la carte, hors de ses boutons, de sa case et de son nom (qui attend de savoir s'il est double). */
  function cliquer(evenement: MouseEvent) {
    const cible = evenement.target as Element | null;
    if (cible?.closest(".survol, .case, .nom, .renommer")) return;
    onclic(evenement);
  }

  /** F2 renomme, depuis la vignette qui a le clavier. */
  function touche(evenement: KeyboardEvent) {
    if (evenement.key === "F2") {
      evenement.preventDefault();
      onrenommer();
    }
  }

  /**
   * Vignette recadrée sur le dessin : le cadre du schéma garde de larges marges (place prévue pour les étiquettes), qui
   * rapetissaient les équipements dans une vignette plus large que haute.
   */
  function recadrer(bouton: HTMLElement, _dessin: string) {
    const appliquer = () => {
      const svg = bouton.querySelector("svg");
      if (!svg) return;
      const boite = svg.getBBox();
      if (boite.width <= 0 || boite.height <= 0) return;
      const marge = 12;
      svg.setAttribute("viewBox", `${boite.x - marge} ${boite.y - marge} ${boite.width + 2 * marge} ${boite.height + 2 * marge}`);
      svg.setAttribute("width", String(Math.round(boite.width + 2 * marge)));
      svg.setAttribute("height", String(Math.round(boite.height + 2 * marge)));
    };
    appliquer();
    return { update: appliquer };
  }
</script>

<!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_noninteractive_element_interactions -->
<li class="carte" class:choisie class:selection data-id={carte.id} oncontextmenu={onmenu} onclick={cliquer}>
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="vignette" onmouseenter={survoler} onmouseleave={quitter}>
    <input
      type="checkbox"
      class="case"
      checked={choisie}
      aria-label={`Choisir « ${carte.titre} »`}
      onclick={(e) => {
        e.stopPropagation();
        oncase(e);
      }}
    />
    <button
      type="button"
      class="apercu"
      aria-label={`Voir le détail de « ${carte.titre} »`}
      aria-describedby={`meta-${carte.id}`}
      onkeydown={touche}
      use:recadrer={dessin}
    >
      {#if dessin}
        {@html dessin}
      {:else if schema}
        <span class="vide-apercu">Schéma vide</span>
      {:else if erreur}
        <span class="vide-apercu erreur">{erreur}</span>
      {:else}
        <span class="vide-apercu">…</span>
      {/if}
    </button>
    {#if carte.epingle}
      <span class="epingle" title="Épinglé"><Pin size={14} aria-hidden="true" /><span class="visuellement-cache">Épinglé</span></span>
    {/if}
    {#if pages.length > 1}
      <span class="traits" aria-hidden="true">
        {#each pages as p, i (p.id)}<i class:allume={i === rang % pages.length}></i>{/each}
      </span>
    {/if}
    <div class="survol">
      <button type="button" class="bouton principal petit" onclick={onmodifier}>
        <PenLine size={14} aria-hidden="true" /> Modifier
      </button>
      <button
        type="button"
        class="bouton petit plus"
        aria-haspopup="menu"
        aria-label={`Plus d'actions sur « ${carte.titre} »`}
        title="Plus d'actions"
        onclick={onplus}
      >
        <Ellipsis size={16} aria-hidden="true" />
      </button>
    </div>
  </div>
  <div class="infos">
    <NomEditable titre={carte.titre} {edition} onouvrir={onclic} {onrenommer} {onvalider} {onannuler} />
    <p class="meta" id={`meta-${carte.id}`} title={meta}>{meta}</p>
    {#if carte.etiquettes.length > 0 || carte.fiches > 0 || carte.points > 0}
      <ul class="petites" aria-label="Étiquettes">
        {#each carte.etiquettes as e (e)}<li class="petite">{e}</li>{/each}
        {#if carte.fiches > 0}<li class="petite" title={fiches.join(", ")}>Cité dans {pluriel(carte.fiches, "fiche")}</li>{/if}
        {#if carte.points > 0}<li class="petite a-verifier">{pluriel(carte.points, "point à vérifier", "points à vérifier")}</li>{/if}
      </ul>
    {/if}
  </div>
</li>

<style>
  .carte {
    position: relative;
    display: flex;
    flex-direction: column;
    min-width: 0;
    overflow: hidden;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    cursor: pointer;
    transition: box-shadow var(--duree);
  }

  .carte.choisie {
    border-color: var(--principale);
    box-shadow: 0 0 0 1px var(--principale);
  }

  .vignette {
    position: relative;
    border-bottom: 1px solid var(--bordure);
  }

  .apercu {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 100%;
    height: 150px;
    padding: 10px 12px 14px;
    border: none;
    background: var(--fond);
    cursor: pointer;
  }

  .apercu :global(svg) {
    /* Le dessin est refait quand les pages défilent : un clic commencé sur l'ancien se perdrait. Le bouton, lui, reste. */
    pointer-events: none;
    max-width: 100%;
    max-height: 100%;
    width: auto;
    height: auto;
  }

  .carte:hover .apercu,
  .apercu:focus-visible {
    background: var(--surface-2);
  }

  .apercu:focus-visible {
    outline-offset: -2px;
  }

  .vide-apercu {
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .vide-apercu.erreur {
    color: var(--retard);
  }

  /* La case, au coin : au survol, au clavier, et dès qu'un schéma est choisi. */
  .case {
    position: absolute;
    top: 8px;
    left: 8px;
    width: 18px;
    height: 18px;
    margin: 0;
    accent-color: var(--principale);
    cursor: pointer;
    opacity: 0;
    transition: opacity var(--duree);
  }

  .carte:hover .case,
  .carte:focus-within .case,
  .carte.selection .case,
  .carte.choisie .case {
    opacity: 1;
  }

  .epingle {
    position: absolute;
    top: 8px;
    right: 8px;
    display: inline-flex;
    color: var(--principale);
  }

  /* Les pages : un trait chacune, celui de la page montrée allumé. */
  .traits {
    position: absolute;
    bottom: 6px;
    left: 8px;
    display: flex;
    gap: 3px;
  }

  .traits i {
    width: 14px;
    height: 4px;
    border-radius: 2px;
    background: var(--bordure);
  }

  .traits i.allume {
    background: var(--principale);
  }

  /* « Modifier » et « ⋯ » : au survol, et quand la carte a le clavier. */
  .survol {
    position: absolute;
    right: 8px;
    bottom: 8px;
    display: flex;
    gap: 4px;
    opacity: 0;
    pointer-events: none;
    transition: opacity var(--duree);
  }

  .carte:hover .survol,
  .carte:focus-within .survol {
    opacity: 1;
    pointer-events: auto;
  }

  .bouton.petit {
    height: 28px;
    padding: 0 10px;
    font-size: 12px;
  }

  .bouton.plus {
    width: 30px;
    justify-content: center;
    padding: 0;
  }

  .infos {
    display: flex;
    flex-direction: column;
    gap: 3px;
    min-width: 0;
    padding: 9px 12px 11px;
  }

  .meta {
    margin: 0;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .petites {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
    margin: 3px 0 0;
    padding: 0;
    list-style: none;
  }

  .petite {
    max-width: 100%;
    overflow: hidden;
    padding: 0 8px;
    border-radius: 999px;
    background: var(--surface-2);
    font-size: 11px;
    line-height: 19px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .petite.a-verifier {
    background: var(--avertissement-fond);
    color: var(--avertissement);
  }

  /* Fenêtre étroite (31F) : deux cartes par ligne, une vignette plus basse, une seule ligne d'infos. */
  @media (max-width: 640px) {
    .apercu {
      height: 104px;
      padding: 8px 8px 12px;
    }

    .infos {
      padding: 7px 9px 9px;
    }

    .meta {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .survol {
      right: 6px;
      bottom: 6px;
    }
  }

  /* Très large : des vignettes un peu plus hautes. */
  @media (min-width: 1800px) {
    .apercu {
      height: 180px;
    }
  }
</style>
