<script lang="ts">
  import { onMount, type Snippet, tick } from "svelte";
  import { placerBulle, type Zone } from "../../lib/placement";

  // Bulle du mode simplifié (11A) : posée près de l'objet choisi, sans jamais sortir du plan. Si la place
  // manque autour de l'objet (fenêtre étroite), elle devient un panneau en bas du plan.
  let {
    objet,
    cotes,
    libelle,
    auClavier = false,
    onfermer,
    children,
  }: {
    /** Ce qu'occupe l'objet choisi, en pixels du plan. */
    objet: Zone;
    /** Côtés essayés, dans l'ordre. */
    cotes?: ("droite" | "gauche" | "dessous" | "dessus")[];
    libelle: string;
    /** Ouverte au clavier : son premier champ prend la main. */
    auClavier?: boolean;
    /** Échap : la bulle se ferme, et l'objet reprend la main. */
    onfermer: () => void;
    children: Snippet;
  } = $props();

  let bulle: HTMLDivElement;
  /** Taille de la bulle flottante : celle du panneau du bas ne compte pas (sinon elle hésiterait entre les deux). */
  let taille = $state({ largeur: 0, hauteur: 0 });
  let cadre = $state({ largeur: 0, hauteur: 0 });
  const place = $derived(
    taille.largeur > 0 && cadre.largeur > 0 ? placerBulle(objet, taille, cadre, cotes) : undefined,
  );

  onMount(() => {
    const parent = bulle.offsetParent ?? bulle.parentElement;
    const mesurer = () => {
      if (!bulle.classList.contains("en-bas")) {
        taille = { largeur: bulle.offsetWidth, hauteur: bulle.offsetHeight };
      }
      if (parent) cadre = { largeur: parent.clientWidth, hauteur: parent.clientHeight };
    };
    const observateur = new ResizeObserver(mesurer);
    observateur.observe(bulle);
    if (parent) observateur.observe(parent);
    mesurer();
    return () => observateur.disconnect();
  });

  // Ouverte au clavier : son premier champ prend la main, une fois la bulle en place (cachée, elle ne le peut pas).
  let mainPrise = false;
  $effect(() => {
    if (!auClavier || mainPrise || place === undefined) return;
    mainPrise = true;
    void tick().then(() => bulle.querySelector<HTMLElement>('[tabindex="0"], input, select')?.focus());
  });

  function clavier(evenement: KeyboardEvent) {
    if (evenement.key === "Escape") {
      evenement.preventDefault();
      evenement.stopPropagation();
      onfermer();
      return;
    }
    // Les touches de la bulle sont les siennes : Suppr ou une flèche n'y touchent pas le schéma.
    if (!evenement.ctrlKey && !evenement.metaKey) evenement.stopPropagation();
  }
</script>

<!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
<div
  bind:this={bulle}
  class="bulle"
  class:en-bas={place === null}
  class:mesure={place === undefined}
  role="dialog"
  tabindex="-1"
  aria-label={libelle}
  style={place ? `left: ${place.x}px; top: ${place.y}px` : undefined}
  onkeydown={clavier}
>
  {@render children()}
</div>

<style>
  .bulle {
    position: absolute;
    z-index: 4;
    display: flex;
    flex-direction: column;
    gap: 8px;
    width: 276px;
    max-height: calc(100% - 16px);
    padding: 12px;
    border: 1px solid var(--trait-fort, var(--bordure));
    border-radius: 12px;
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.16);
    overflow-y: auto;
  }

  /* Le temps de la mesurer : en place, mais pas encore montrée. */
  .bulle.mesure {
    top: 0;
    left: 0;
    visibility: hidden;
  }

  /* Pas de place autour de l'objet : un panneau en bas du plan, sur toute sa largeur. */
  .bulle.en-bas {
    right: 8px;
    bottom: 8px;
    left: 8px;
    width: auto;
  }

  .bulle :global(h2) {
    margin: 0;
    font-size: 15px;
    line-height: 22px;
    overflow-wrap: anywhere;
  }

  .bulle :global(label),
  .bulle :global(.libelle) {
    display: block;
    margin-bottom: 2px;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .bulle :global(.champ) {
    border-color: var(--trait-fort, var(--bordure));
  }

  .bulle :global(.aide) {
    margin: 0;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .bulle :global(.actions) {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
  }

  .bulle :global(.actions .bouton) {
    height: 32px;
    padding: 0 8px;
    font-size: 13px;
  }

  .bulle :global(.bouton.danger:hover) {
    color: var(--retard);
  }

  .bulle :global(.vers-avance) {
    padding: 4px 0 0;
    border: none;
    border-top: 1px solid var(--bordure);
    background: none;
    color: var(--lien);
    font-size: 12px;
    text-align: left;
    text-decoration: underline;
  }
</style>
