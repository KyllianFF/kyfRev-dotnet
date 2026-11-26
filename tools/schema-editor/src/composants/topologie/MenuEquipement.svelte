<script lang="ts">
  import { type Ancre, placerMenu } from "../../lib/placement";
  import type { ActionEquipement, EntreeEquipement } from "../../lib/topologie/menuEquipement";

  // Le menu des actions sur un équipement : sous « ⋯ » dans son volet, ou au pointeur après un clic droit sur le plan.
  // Il prend le clavier en s'ouvrant ; les flèches passent d'une entrée à l'autre, Début et Fin vont aux bouts, Échap
  // le ferme et rend la main à ce qui l'a ouvert, Tab le ferme aussi. Un appui ailleurs le ferme.
  let {
    entrees,
    libelle,
    ancre = null,
    exclu = null,
    onchoisir,
    onfermer,
  }: {
    entrees: EntreeEquipement[];
    libelle: string;
    /** Au pointeur (clic droit, touche Menu) : le menu flotte là, entier dans la fenêtre. Sans ancre : sous « ⋯ ». */
    ancre?: Ancre | null;
    /** Le bouton qui l'ouvre et le ferme : un appui sur lui n'est pas « ailleurs ». */
    exclu?: Element | null;
    onchoisir: (action: ActionEquipement) => void;
    /** `rendreLaMain` : le clavier revient à ce qui a ouvert le menu (Échap), sinon il va où l'on a cliqué. */
    onfermer: (rendreLaMain: boolean) => void;
  } = $props();

  let liste = $state<HTMLDivElement>();

  const entreesActives = (menu: HTMLElement | undefined) =>
    [...(menu?.querySelectorAll<HTMLButtonElement>("button:not(:disabled)") ?? [])];

  function ouvert(element: HTMLDivElement) {
    if (ancre) {
      const cadre = element.getBoundingClientRect();
      const { documentElement: racine, body } = document;
      const place = placerMenu(
        ancre,
        { largeur: cadre.width, hauteur: cadre.height },
        {
          largeur: Math.min(racine.clientWidth, body.clientWidth || racine.clientWidth),
          hauteur: Math.min(racine.clientHeight, body.clientHeight || racine.clientHeight),
        },
      );
      element.style.left = `${place.x}px`;
      element.style.top = `${place.y}px`;
    }
    entreesActives(element)[0]?.focus();
    const dehors = (evenement: Event) => {
      const cible = evenement.target as Node | null;
      if (cible && !element.contains(cible) && !exclu?.contains(cible)) onfermer(false);
    };
    const fenetre = () => onfermer(false);
    window.addEventListener("pointerdown", dehors, true);
    window.addEventListener("wheel", dehors, { capture: true, passive: true });
    window.addEventListener("resize", fenetre);
    return () => {
      window.removeEventListener("pointerdown", dehors, true);
      window.removeEventListener("wheel", dehors, { capture: true });
      window.removeEventListener("resize", fenetre);
    };
  }

  function clavier(evenement: KeyboardEvent) {
    const items = entreesActives(liste);
    const rang = items.indexOf(document.activeElement as HTMLButtonElement);
    let vers: HTMLButtonElement | undefined;
    if (evenement.key === "ArrowDown") vers = items[(rang + 1) % items.length];
    else if (evenement.key === "ArrowUp") vers = items[(rang - 1 + items.length) % items.length];
    else if (evenement.key === "Home") vers = items[0];
    else if (evenement.key === "End") vers = items.at(-1);
    else if (evenement.key === "Escape" || evenement.key === "ContextMenu" || (evenement.key === "F10" && evenement.shiftKey)) {
      evenement.preventDefault();
      evenement.stopPropagation();
      onfermer(true);
      return;
    } else if (evenement.key === "Tab") {
      onfermer(false);
      return;
    } else return;
    // Les flèches sont pour le menu : l'équipement ne bouge pas sur le plan.
    evenement.preventDefault();
    evenement.stopPropagation();
    vers?.focus();
  }
</script>

<!-- svelte-ignore a11y_interactive_supports_focus -->
<div
  bind:this={liste}
  class="menu"
  class:flottant={ancre !== null}
  role="menu"
  aria-label={libelle}
  tabindex="-1"
  onkeydown={clavier}
  oncontextmenu={(e) => e.preventDefault()}
  {@attach ouvert}
>
  {#each entrees as e (e.action)}
    {#if e.apresUnTrait}<hr />{/if}
    <button
      type="button"
      role="menuitem"
      class:danger={e.danger}
      data-action={e.action}
      disabled={e.desactivee}
      title={e.titre}
      onclick={() => onchoisir(e.action)}
    >
      <span class="nom-entree">{e.nom}</span>{#if e.touche}<span class="touche">{e.touche}</span>{/if}
    </button>
  {/each}
</div>

<style>
  .menu {
    position: absolute;
    top: calc(100% - 6px);
    right: 10px;
    z-index: 20;
    display: flex;
    flex-direction: column;
    min-width: 220px;
    max-width: calc(100vw - 16px);
    padding: 5px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    box-shadow: 0 10px 28px rgb(0 0 0 / 0.18);
  }

  /* Au pointeur : posé dans la fenêtre, au-dessus du plan et du volet ; il défile s'il est plus haut qu'elle. */
  .menu.flottant {
    position: fixed;
    top: 0;
    right: auto;
    left: 0;
    z-index: 30;
    max-height: calc(100vh - 16px);
    overflow-y: auto;
  }

  .menu:focus {
    outline: none;
  }

  .menu button {
    display: flex;
    flex: none;
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

  .nom-entree {
    overflow: hidden;
    text-overflow: ellipsis;
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
    flex: none;
    width: calc(100% - 12px);
    margin: 4px 6px;
    border: none;
    border-top: 1px solid var(--bordure);
  }

  .touche {
    flex: none;
    margin-left: auto;
    padding-left: 16px;
    color: var(--texte-secondaire);
    font-size: 11px;
  }
</style>
