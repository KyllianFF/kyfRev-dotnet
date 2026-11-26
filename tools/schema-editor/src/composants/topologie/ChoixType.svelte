<script lang="ts">
  import { onMount, tick } from "svelte";
  import { CATEGORIES, type DefinitionType, definition, typeConnu } from "../../lib/topologie/catalogue";
  import { couleurDuType } from "../../lib/topologie/dessin";
  import { illustration } from "../../lib/topologie/illustrations";
  import { nomCourt, typesTrouves } from "../../lib/topologie/plateau";
  import { typesRecents } from "../../lib/topologie/volet.svelte";

  // Le type d'un équipement, choisi dans une grille d'icônes (30C) : la recherche, les types récents d'abord, puis
  // chaque famille du catalogue. Les flèches vont d'une icône à l'autre, Entrée choisit, Échap referme.
  let {
    actuel,
    presents,
    onchoisir,
    onfermer,
  }: {
    /** Le type de l'équipement. */
    actuel: string;
    /** Les types posés sur le schéma, du dernier posé au premier : ils complètent les récents. */
    presents: string[];
    onchoisir: (type: string) => void;
    onfermer: () => void;
  } = $props();

  let recherche = $state("");
  let fenetre: HTMLDivElement;
  let champ: HTMLInputElement;

  const trouves = $derived(typesTrouves(recherche));
  const recents = $derived(
    [...new Set([...typesRecents.liste, ...presents])]
      .filter((t) => t !== actuel && typeConnu(t))
      .slice(0, 5)
      .map((t) => definition(t)),
  );
  const sections = $derived([
    ...(recherche.trim() === "" && recents.length > 0 ? [{ cle: "recents", nom: "Récents", types: recents }] : []),
    ...CATEGORIES.map((c) => ({ cle: c.valeur, nom: c.nom, types: trouves.filter((t) => t.categorie === c.valeur) })).filter(
      (s) => s.types.length > 0,
    ),
  ]);
  /** La tuile qui reçoit la tabulation : le type actuel, sinon la première. */
  let courante = $state<string | null>(null);
  const tabulee = $derived.by(() => {
    const tous = sections.flatMap((s) => s.types.map((t) => `${s.cle}:${t.type}`));
    if (courante && tous.includes(courante)) return courante;
    return tous.find((c) => c.endsWith(`:${actuel}`) && !c.startsWith("recents:")) ?? tous[0] ?? null;
  });

  onMount(() => {
    champ.focus();
    const dehors = (evenement: PointerEvent) => {
      const cible = evenement.target as Node | null;
      if (cible && !fenetre.contains(cible) && !(cible as Element).closest?.("[data-choix-type]")) onfermer();
    };
    window.addEventListener("pointerdown", dehors, true);
    return () => window.removeEventListener("pointerdown", dehors, true);
  });

  function tuiles(): HTMLButtonElement[] {
    return [...fenetre.querySelectorAll<HTMLButtonElement>("button.tuile-type")];
  }

  /** Les flèches : à gauche, à droite, et en haut ou en bas vers la tuile la plus proche de la rangée voisine. */
  function clavier(evenement: KeyboardEvent) {
    if (evenement.key === "Escape") {
      evenement.preventDefault();
      evenement.stopPropagation();
      onfermer();
      return;
    }
    const cible = evenement.target as HTMLElement;
    if (!cible.classList.contains("tuile-type")) return;
    const liste = tuiles();
    const rang = liste.indexOf(cible as HTMLButtonElement);
    let vers: HTMLButtonElement | undefined;
    if (evenement.key === "ArrowRight") vers = liste[Math.min(liste.length - 1, rang + 1)];
    else if (evenement.key === "ArrowLeft") vers = liste[Math.max(0, rang - 1)];
    else if (evenement.key === "Home") vers = liste[0];
    else if (evenement.key === "End") vers = liste.at(-1);
    else if (evenement.key === "ArrowDown" || evenement.key === "ArrowUp") {
      const ici = cible.getBoundingClientRect();
      const bas = evenement.key === "ArrowDown";
      const rangee = liste.filter((t) => {
        const r = t.getBoundingClientRect();
        return bas ? r.top > ici.top + 2 : r.top < ici.top - 2;
      });
      const voisine = bas
        ? Math.min(...rangee.map((t) => t.getBoundingClientRect().top))
        : Math.max(...rangee.map((t) => t.getBoundingClientRect().top));
      vers = rangee
        .filter((t) => Math.abs(t.getBoundingClientRect().top - voisine) < 2)
        .sort((a, b) => Math.abs(a.getBoundingClientRect().left - ici.left) - Math.abs(b.getBoundingClientRect().left - ici.left))[0];
      if (!vers && !bas) {
        evenement.preventDefault();
        evenement.stopPropagation();
        champ.focus();
        return;
      }
    } else return;
    // Les flèches sont pour la grille : l'équipement ne bouge pas sur le plan.
    evenement.preventDefault();
    evenement.stopPropagation();
    if (vers) {
      courante = vers.dataset.cle ?? null;
      vers.focus();
      vers.scrollIntoView({ block: "nearest" });
    }
  }

  function clavierDuChamp(evenement: KeyboardEvent) {
    if (evenement.key === "ArrowDown") {
      evenement.preventDefault();
      evenement.stopPropagation();
      void tick().then(() => tuiles()[0]?.focus());
    } else if (evenement.key === "Enter") {
      evenement.preventDefault();
      const premier = sections.find((s) => s.cle !== "recents")?.types[0];
      if (premier) onchoisir(premier.type);
    }
  }

  const nombre = $derived(trouves.length);
  const titre = (t: DefinitionType) => (t.type === actuel ? `${t.nom} (type actuel)` : t.nom);
</script>

<!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
<div
  bind:this={fenetre}
  class="choix-type"
  role="dialog"
  aria-label="Type de l'équipement"
  tabindex="-1"
  onkeydown={clavier}
>
  <div class="recherche">
    <input
      bind:this={champ}
      bind:value={recherche}
      class="champ"
      type="search"
      aria-label="Chercher un type"
      aria-describedby="nombre-de-types"
      placeholder="Chercher un type"
      autocomplete="off"
      onkeydown={clavierDuChamp}
    />
    <span id="nombre-de-types" class="nombre" aria-live="polite">{nombre} {nombre > 1 ? "types" : "type"}</span>
  </div>
  <div class="sections">
    {#each sections as s (s.cle)}
      <div class="section" role="group" aria-labelledby="section-type-{s.cle}">
        <h4 id="section-type-{s.cle}">{s.nom}</h4>
        <div class="grille">
          {#each s.types as t (t.type)}
            {@const cle = `${s.cle}:${t.type}`}
            <button
              type="button"
              class="tuile-type"
              class:actuel={t.type === actuel}
              data-type={t.type}
              data-cle={cle}
              aria-pressed={t.type === actuel}
              title={titre(t)}
              tabindex={tabulee === cle ? 0 : -1}
              onfocus={() => (courante = cle)}
              onclick={() => onchoisir(t.type)}
            >
              <svg viewBox="2 2 60 60" width="34" height="34" aria-hidden="true">
                <g filter="url(#ombre)">{@html illustration(t.icone, couleurDuType(t.type))}</g>
              </svg>
              <span class="nom">{nomCourt(t)}</span>
            </button>
          {/each}
        </div>
      </div>
    {:else}
      <p class="vide">Aucun type ne correspond. « Générique » convient à tout le reste.</p>
    {/each}
  </div>
  <p class="aide">Changer de type garde le nom, les ports et les connexions ; les propriétés propres au type changent.</p>
</div>

<style>
  .choix-type {
    position: absolute;
    top: calc(100% - 4px);
    right: 8px;
    left: 8px;
    z-index: 20;
    display: flex;
    flex-direction: column;
    gap: 8px;
    max-height: min(460px, 70vh);
    padding: 10px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 10px 28px rgb(0 0 0 / 0.18);
  }

  .recherche {
    position: relative;
  }

  .recherche .champ {
    height: 32px;
    padding-right: 72px;
    font-size: 13px;
  }

  .nombre {
    position: absolute;
    top: 50%;
    right: 10px;
    transform: translateY(-50%);
    color: var(--texte-secondaire);
    font-size: 12px;
    pointer-events: none;
  }

  .sections {
    display: flex;
    flex-direction: column;
    gap: 8px;
    min-height: 0;
    overflow-y: auto;
    padding: 2px;
  }

  h4 {
    margin: 0 0 4px;
    color: var(--texte-secondaire);
    font-size: 11px;
    font-weight: 700;
    letter-spacing: 0.02em;
    text-transform: uppercase;
  }

  .grille {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(60px, 1fr));
    gap: 4px;
  }

  .tuile-type {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 2px;
    min-width: 0;
    padding: 5px 2px 4px;
    border: 1px solid transparent;
    border-radius: var(--rayon);
    background: transparent;
    color: var(--texte);
    font-size: 11px;
    line-height: 13px;
  }

  .tuile-type:hover {
    background: var(--surface-2);
  }

  .tuile-type.actuel {
    border-color: var(--principale);
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
    font-weight: 600;
  }

  .tuile-type svg {
    flex: none;
    overflow: visible;
  }

  /* Deux lignes au plus : « Switch simple » se lit en entier. */
  .tuile-type .nom {
    display: -webkit-box;
    max-width: 100%;
    overflow: hidden;
    text-align: center;
    overflow-wrap: anywhere;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
    line-clamp: 2;
  }

  .vide,
  .aide {
    margin: 0;
    color: var(--texte-secondaire);
    font-size: 12px;
  }
</style>
