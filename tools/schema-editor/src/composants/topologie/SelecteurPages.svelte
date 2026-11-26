<script lang="ts">
  import { tick } from "svelte";
  import type { Page } from "../../lib/topologie/modele";

  // Les pages d'un schéma (30A), là où il se lit sans se modifier (le détail de la vue Schémas) : de petits onglets
  // discrets, « Étage 1 · Étage 2 · … », comme le sélecteur posé sur l'aperçu d'une fiche. Au clavier, une seule
  // tabulation y entre ; les flèches, Début et Fin passent d'une page à l'autre.
  let {
    pages,
    montree,
    onchoisir,
  }: {
    pages: Page[];
    montree: string;
    onchoisir: (id: string) => void;
  } = $props();

  let barre = $state<HTMLElement>();

  async function aller(rang: number) {
    const page = pages[(rang + pages.length) % pages.length];
    if (!page) return;
    onchoisir(page.id);
    await tick();
    barre?.querySelector<HTMLElement>(`[data-page="${CSS.escape(page.id)}"]`)?.focus();
  }

  function touche(evenement: KeyboardEvent, rang: number) {
    const voulu =
      evenement.key === "ArrowRight"
        ? rang + 1
        : evenement.key === "ArrowLeft"
          ? rang - 1
          : evenement.key === "Home"
            ? 0
            : evenement.key === "End"
              ? pages.length - 1
              : null;
    if (voulu === null) return;
    evenement.preventDefault();
    void aller(voulu);
  }
</script>

{#if pages.length > 1}
  <div class="selecteur-pages" role="tablist" aria-label="Pages du schéma" bind:this={barre}>
    {#each pages as p, rang (p.id)}
      <button
        type="button"
        role="tab"
        data-page={p.id}
        aria-selected={p.id === montree}
        tabindex={p.id === montree ? 0 : -1}
        title="Page « {p.nom} »"
        onclick={() => onchoisir(p.id)}
        onkeydown={(e) => touche(e, rang)}
      >
        {p.nom}<span class="visuellement-cache">, page {rang + 1} sur {pages.length}</span>
      </button>
    {/each}
  </div>
{/if}

<style>
  /* Discret : il ne prend pas le pas sur le dessin. Trop de pages pour une ligne : elles passent à la suivante. */
  .selecteur-pages {
    display: flex;
    flex-wrap: wrap;
    gap: 2px;
    min-width: 0;
    padding: 2px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--surface);
    font-size: 12.5px;
  }

  button {
    overflow: hidden;
    max-width: 220px;
    height: 26px;
    padding: 0 10px;
    border: none;
    border-radius: 6px;
    background: transparent;
    color: var(--texte-secondaire);
    font: inherit;
    text-overflow: ellipsis;
    white-space: nowrap;
    cursor: pointer;
    transition: background var(--duree);
  }

  button:hover {
    background: var(--surface-2);
    color: var(--texte);
  }

  /* La page montrée : sur le fond, cernée, soulignée de la couleur principale, comme l'onglet de l'éditeur. */
  button[aria-selected="true"] {
    background: var(--fond);
    box-shadow:
      inset 0 0 0 1px var(--bordure),
      inset 0 -2px 0 var(--principale);
    color: var(--texte);
    font-weight: 600;
  }

  button:focus-visible {
    outline: 2px solid var(--focus);
    outline-offset: -2px;
  }
</style>
