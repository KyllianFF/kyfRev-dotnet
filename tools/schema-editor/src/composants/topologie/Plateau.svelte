<script lang="ts">
  import ChevronLeft from "@lucide/svelte/icons/chevron-left";
  import ChevronRight from "@lucide/svelte/icons/chevron-right";
  import { onMount, tick } from "svelte";
  import { couleurDuType } from "../../lib/topologie/dessin";
  import { illustration } from "../../lib/topologie/illustrations";
  import { type FamillePlateau, famillesDuPlateau, tuilesDuPlateau } from "../../lib/topologie/plateau";

  // Plateau d'équipements (11A, 12), en bas de l'éditeur dans les deux modes : les familles en onglets,
  // la recherche, et une rangée de tuiles qui défile de côté. Une tuile se fait glisser sur le plan ;
  // cliquée, elle se pose au prochain clic sur le plan ; au clavier, Entrée la pose au centre de la vue.
  let {
    avance = false,
    pose = $bindable(null),
    onposer,
  }: {
    /** Mode avancé : les cadres, notes et modèles de topologie s'ajoutent aux familles. */
    avance?: boolean;
    /** La tuile cliquée, qui attend un clic sur le plan (« type:switch »…). */
    pose?: string | null;
    /** Pose tout de suite, au centre de la vue (clavier). */
    onposer: (genre: string) => void;
  } = $props();

  let famille = $state<FamillePlateau>("tous");
  let recherche = $state("");
  const familles = $derived(famillesDuPlateau(avance));
  const tuiles = $derived(tuilesDuPlateau(famille, recherche));
  /** La tuile qui reçoit la tabulation : une seule, les flèches passent aux autres. */
  let courante = $state(0);

  // Une famille qui n'existe plus (retour au mode simplifié) : tout.
  $effect(() => {
    if (!familles.some((f) => f.valeur === famille)) famille = "tous";
  });

  let rangee = $state<HTMLDivElement>();
  let depasse = $state({ gauche: false, droite: false });

  function mesurer() {
    if (!rangee) return;
    depasse = {
      gauche: rangee.scrollLeft > 1,
      droite: rangee.scrollLeft + rangee.clientWidth < rangee.scrollWidth - 1,
    };
  }

  onMount(() => {
    if (!rangee) return;
    const observateur = new ResizeObserver(mesurer);
    observateur.observe(rangee);
    return () => observateur.disconnect();
  });

  // Les tuiles changent (famille, recherche) : la rangée repart du début.
  $effect(() => {
    void tuiles;
    courante = 0;
    if (rangee) rangee.scrollLeft = 0;
    void tick().then(mesurer);
  });

  function choisirFamille(valeur: FamillePlateau) {
    famille = valeur;
    recherche = "";
  }

  /** Flèches, Début et Fin dans les onglets : l'onglet atteint est choisi. */
  function clavierDesFamilles(evenement: KeyboardEvent) {
    const rang = familles.findIndex((f) => f.valeur === famille);
    const vers =
      evenement.key === "ArrowRight"
        ? (rang + 1) % familles.length
        : evenement.key === "ArrowLeft"
          ? (rang - 1 + familles.length) % familles.length
          : evenement.key === "Home"
            ? 0
            : evenement.key === "End"
              ? familles.length - 1
              : -1;
    const cible = familles[vers];
    if (!cible) return;
    evenement.preventDefault();
    evenement.stopPropagation();
    choisirFamille(cible.valeur);
    const onglets = (evenement.currentTarget as HTMLElement).querySelectorAll<HTMLElement>("[role=tab]");
    onglets[vers]?.focus();
  }

  /** Flèches, Début et Fin dans les tuiles : la tuile atteinte vient dans le champ. */
  function clavierDesTuiles(evenement: KeyboardEvent) {
    const vers =
      evenement.key === "ArrowRight"
        ? Math.min(tuiles.length - 1, courante + 1)
        : evenement.key === "ArrowLeft"
          ? Math.max(0, courante - 1)
          : evenement.key === "Home"
            ? 0
            : evenement.key === "End"
              ? tuiles.length - 1
              : -1;
    if (vers < 0) return;
    evenement.preventDefault();
    evenement.stopPropagation();
    courante = vers;
    const tuile = rangee?.querySelectorAll<HTMLElement>(".tuile")[vers];
    tuile?.focus();
    tuile?.scrollIntoView({ block: "nearest", inline: "nearest" });
  }

  /** La molette fait défiler de côté la rangée (les tuiles, ou les familles quand elles dépassent). */
  function molette(evenement: WheelEvent, rangee: HTMLElement) {
    if (rangee.scrollWidth <= rangee.clientWidth) return;
    const pas = Math.abs(evenement.deltaX) > Math.abs(evenement.deltaY) ? evenement.deltaX : evenement.deltaY;
    evenement.preventDefault();
    rangee.scrollLeft += evenement.deltaMode === 1 ? pas * 32 : pas;
  }

  function defiler(sens: 1 | -1) {
    rangee?.scrollBy({ left: sens * Math.max(120, rangee.clientWidth * 0.8), behavior: "smooth" });
  }

  function glisser(evenement: DragEvent, genre: string) {
    evenement.dataTransfer?.setData("application/x-kyfrev-topologie", genre);
    if (evenement.dataTransfer) evenement.dataTransfer.effectAllowed = "copy";
    pose = null;
  }

  /** Un clic de souris arme la tuile (le prochain clic sur le plan la pose) ; Entrée la pose au centre. */
  function activer(evenement: MouseEvent, genre: string) {
    if (evenement.detail === 0) {
      pose = null;
      onposer(genre);
    } else pose = pose === genre ? null : genre;
  }
</script>

<footer class="plateau" class:avance aria-label="Équipements à poser">
  <div class="tete">
    <!-- svelte-ignore a11y_interactive_supports_focus -->
    <div
      class="familles"
      role="tablist"
      aria-label="Familles d'équipements"
      onkeydown={clavierDesFamilles}
      onwheel={(e) => molette(e, e.currentTarget)}
    >
      {#each familles as f (f.valeur)}
        <button
          type="button"
          role="tab"
          id="famille-{f.valeur}"
          aria-selected={famille === f.valeur}
          aria-controls="tuiles-du-plateau"
          tabindex={famille === f.valeur ? 0 : -1}
          title={f.titre}
          onclick={() => choisirFamille(f.valeur)}
        >
          {f.nom}
        </button>
      {/each}
    </div>
    <!-- Fenêtre étroite : les familles tiennent dans une liste, à côté de la recherche. -->
    <select
      class="champ famille-etroite"
      aria-label="Famille d'équipements"
      value={famille}
      onchange={(e) => choisirFamille(e.currentTarget.value as FamillePlateau)}
    >
      {#each familles as f (f.valeur)}<option value={f.valeur}>{f.nom}</option>{/each}
    </select>
    <input
      class="champ recherche"
      type="search"
      aria-label="Chercher un équipement"
      placeholder="Chercher un équipement"
      bind:value={recherche}
      oninput={() => {
        // On cherche dans tout le catalogue, pas dans la seule famille affichée.
        if (recherche.trim() !== "") famille = "tous";
      }}
      onkeydown={(e) => {
        // Échap vide d'abord la recherche (sans fermer l'éditeur).
        if (e.key !== "Escape" || recherche === "") return;
        e.preventDefault();
        e.stopPropagation();
        recherche = "";
      }}
    />
  </div>
  <div class="defilement">
    {#if depasse.gauche}
      <button type="button" class="fleche gauche" aria-label="Équipements précédents" onclick={() => defiler(-1)}>
        <ChevronLeft size={18} aria-hidden="true" />
      </button>
    {/if}
    <div
      bind:this={rangee}
      class="tuiles"
      id="tuiles-du-plateau"
      role="tabpanel"
      aria-labelledby="famille-{famille}"
      tabindex="-1"
      onscroll={mesurer}
      onwheel={(e) => molette(e, e.currentTarget)}
      onkeydown={clavierDesTuiles}
    >
      {#each tuiles as t, i (t.genre)}
        <button
          type="button"
          class="tuile"
          class:armee={pose === t.genre}
          draggable="true"
          data-genre={t.genre}
          aria-label="Ajouter : {t.titre}"
          aria-pressed={pose === t.genre}
          title="{t.titre} : glisser sur le plan, ou cliquer puis cliquer sur le plan"
          tabindex={i === courante ? 0 : -1}
          ondragstart={(e) => glisser(e, t.genre)}
          onfocus={() => (courante = i)}
          onclick={(e) => activer(e, t.genre)}
        >
          {#if t.type}
            <svg viewBox="2 2 60 60" width="44" height="44" aria-hidden="true">
              <g filter="url(#ombre)">{@html illustration(t.type.icone, couleurDuType(t.type.type))}</g>
            </svg>
          {:else}
            <svg class="trait" viewBox="0 0 24 24" width="30" height="30" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
              {@html t.icone}
            </svg>
          {/if}
          <span class="nom">{t.nom}</span>
        </button>
      {:else}
        <p class="vide">Aucun équipement ne correspond. L'équipement « Générique » convient à tout le reste.</p>
      {/each}
    </div>
    {#if depasse.droite}
      <button type="button" class="fleche droite" aria-label="Équipements suivants" onclick={() => defiler(1)}>
        <ChevronRight size={18} aria-hidden="true" />
      </button>
    {/if}
  </div>
</footer>

<style>
  .plateau {
    display: flex;
    flex: none;
    flex-direction: column;
    gap: 8px;
    padding: 8px 14px 10px;
    border-top: 1px solid var(--bordure);
    background: var(--surface-2);
  }

  .tete {
    display: flex;
    align-items: center;
    gap: 10px;
    min-width: 0;
  }

  .familles {
    display: flex;
    flex: 1;
    gap: 4px;
    min-width: 0;
    padding: 2px;
    overflow-x: auto;
    scrollbar-width: none;
  }

  .familles button {
    flex: none;
    min-height: 32px;
    padding: 0 12px;
    border: 1px solid transparent;
    border-radius: 16px;
    background: transparent;
    color: var(--texte);
    white-space: nowrap;
  }

  .familles button:hover {
    background: var(--surface);
  }

  .familles button[aria-selected="true"] {
    border-color: var(--principale);
    background: var(--surface);
    font-weight: 600;
  }

  .famille-etroite {
    display: none;
    flex: 1;
    min-width: 0;
    height: 32px;
    border-color: var(--trait-fort, var(--bordure));
    border-radius: 10px;
  }

  .recherche {
    flex: none;
    width: 220px;
    height: 32px;
    margin-left: auto;
    padding: 0 12px;
    border-color: var(--trait-fort, var(--bordure));
    border-radius: 10px;
  }

  .recherche::placeholder {
    color: var(--texte-secondaire);
    opacity: 1;
  }

  .defilement {
    position: relative;
    min-width: 0;
  }

  /* Une ligne, qui défile de côté : sa hauteur ne change jamais, le plan garde sa place. */
  .tuiles {
    display: flex;
    gap: 8px;
    height: 78px;
    overflow-x: auto;
    overflow-y: hidden;
    scrollbar-width: none;
    scroll-padding: 0 40px;
  }

  .tuile {
    display: flex;
    flex: 0 0 86px;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 2px;
    height: 78px;
    padding: 0 4px;
    border: 1px solid var(--bordure);
    border-radius: 10px;
    background: var(--surface);
    color: var(--texte);
    font-size: 12px;
    cursor: grab;
  }

  .tuile:hover {
    border-color: var(--texte-secondaire);
  }

  .tuile:focus-visible {
    outline-offset: -2px;
  }

  .tuile.armee {
    border-color: var(--principale);
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
    font-weight: 600;
  }

  .tuile svg {
    flex: none;
    overflow: visible;
    pointer-events: none;
  }

  .tuile svg.trait {
    height: 44px;
    color: var(--principale);
  }

  .nom {
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    pointer-events: none;
  }

  .vide {
    align-self: center;
    margin: 0;
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .fleche {
    position: absolute;
    top: 50%;
    z-index: 1;
    display: grid;
    width: 32px;
    height: 44px;
    padding: 0;
    transform: translateY(-50%);
    border: 1px solid var(--trait-fort, var(--bordure));
    border-radius: 10px;
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 2px 8px rgb(0 0 0 / 0.15);
    place-items: center;
  }

  .fleche:hover {
    background: var(--surface-2);
  }

  .fleche.gauche {
    left: 0;
  }

  .fleche.droite {
    right: 0;
  }

  /* Mode avancé : neuf familles ne tiennent pas à côté de la recherche dans une fenêtre moyenne. */
  @media (max-width: 1120px) {
    .plateau.avance .familles {
      display: none;
    }

    .plateau.avance .famille-etroite {
      display: block;
      flex: 0 1 240px;
    }
  }

  /* Fenêtre étroite (11D) : la recherche d'abord, les familles dans une liste. */
  @media (max-width: 620px) {
    .familles {
      display: none;
    }

    .famille-etroite,
    .plateau.avance .famille-etroite {
      display: block;
      flex: 1;
    }

    .recherche {
      flex: 1.4;
      width: auto;
      min-width: 0;
    }
  }
</style>
