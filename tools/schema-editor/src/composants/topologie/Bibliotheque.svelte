<script lang="ts">
  import Search from "@lucide/svelte/icons/search";
  import { CATEGORIES, chercherTypes } from "../../lib/topologie/catalogue";
  import { couleurDuType, icone } from "../../lib/topologie/dessin";
  import { GABARITS } from "../../lib/topologie/gabarits";
  import { illustration } from "../../lib/topologie/illustrations";
  import { GENRES_GROUPE } from "../../lib/topologie/modele";

  /** Annotations libres (D46). */
  const DESSINS = [
    { genre: "annotation", nom: "Note", icone: icone("sticky-note") },
    {
      genre: "annotation:texte",
      nom: "Texte libre",
      icone: '<path d="M4 7V4h16v3"/><path d="M9 20h6"/><path d="M12 4v16"/>',
    },
    {
      genre: "annotation:rectangle",
      nom: "Zone rectangulaire",
      icone: '<rect x="3" y="5" width="18" height="14" rx="2"/>',
    },
    { genre: "annotation:ellipse", nom: "Zone ronde", icone: '<ellipse cx="12" cy="12" rx="9" ry="7"/>' },
    { genre: "annotation:fleche", nom: "Flèche", icone: '<path d="M5 19 19 5"/><path d="M9 5h10v10"/>' },
  ];

  // Bibliothèque de l'éditeur (D39) : types d'équipements par catégorie,
  // groupes (site, bâtiment, salle, zone) et annotations. Un clic pose
  // l'élément au centre du plan ; on peut aussi le faire glisser.
  let { onajouter }: { onajouter: (genre: string) => void } = $props();

  let recherche = $state("");
  const trouves = $derived(chercherTypes(recherche));
  const parCategorie = $derived(
    CATEGORIES.map((c) => ({ ...c, types: trouves.filter((t) => t.categorie === c.valeur) })).filter(
      (c) => c.types.length > 0,
    ),
  );

  function glisser(evenement: DragEvent, genre: string) {
    evenement.dataTransfer?.setData("application/x-kyfrev-topologie", genre);
    if (evenement.dataTransfer) evenement.dataTransfer.effectAllowed = "copy";
  }
</script>

<aside class="bibliotheque" aria-label="Bibliothèque d'équipements">
  <label class="recherche">
    <Search size={15} aria-hidden="true" />
    <span class="visuellement-cache">Chercher un équipement</span>
    <input class="champ" type="search" placeholder="Switch, ESXi, NAS…" bind:value={recherche} />
  </label>
  <div class="liste">
    {#each parCategorie as categorie (categorie.valeur)}
      <details open={recherche.trim() !== "" || categorie.valeur === "reseau"}>
        <summary>{categorie.nom} <span class="nombre">{categorie.types.length}</span></summary>
        <ul>
          {#each categorie.types as t (t.type)}
            <li>
              <button
                type="button"
                class="element"
                draggable="true"
                title="Cliquer pour ajouter au centre, ou glisser sur le plan"
                ondragstart={(e) => glisser(e, `type:${t.type}`)}
                onclick={() => onajouter(`type:${t.type}`)}
              >
                <svg viewBox="0 0 64 64" width="28" height="28" aria-hidden="true">
                  {@html illustration(t.icone, couleurDuType(t.type))}
                </svg>
                <span>{t.nom}</span>
              </button>
            </li>
          {/each}
        </ul>
      </details>
    {/each}
    {#if recherche.trim() === ""}
      <details open>
        <summary>Contexte et annotations</summary>
        <ul>
          {#each GENRES_GROUPE as g (g.valeur)}
            <li>
              <button
                type="button"
                class="element"
                draggable="true"
                ondragstart={(e) => glisser(e, `groupe:${g.valeur}`)}
                onclick={() => onajouter(`groupe:${g.valeur}`)}
              >
                <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  {@html icone(g.valeur === "site" ? "map-pin" : g.valeur === "batiment" ? "warehouse" : "square-dashed")}
                </svg>
                <span>{g.nom}</span>
              </button>
            </li>
          {/each}
          {#each DESSINS as d (d.genre)}
            <li>
              <button
                type="button"
                class="element"
                draggable="true"
                ondragstart={(e) => glisser(e, d.genre)}
                onclick={() => onajouter(d.genre)}
              >
                <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  {@html d.icone}
                </svg>
                <span>{d.nom}</span>
              </button>
            </li>
          {/each}
        </ul>
      </details>
    {/if}
    {#if recherche.trim() === ""}
      <details>
        <summary>Modèles de topologie</summary>
        <ul>
          {#each GABARITS as g (g.id)}
            <li>
              <button
                type="button"
                class="element gabarit"
                draggable="true"
                title={`${g.description} Cliquer pour l'insérer au centre, ou glisser sur le plan.`}
                ondragstart={(e) => glisser(e, `gabarit:${g.id}`)}
                onclick={() => onajouter(`gabarit:${g.id}`)}
              >
                <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  {@html icone("network")}
                </svg>
                <span>{g.nom}</span>
              </button>
            </li>
          {/each}
        </ul>
      </details>
    {/if}
    {#if parCategorie.length === 0 && recherche.trim()}
      <p class="vide">Aucun type ne correspond. L'équipement générique convient à tout le reste.</p>
    {/if}
  </div>
</aside>

<style>
  .bibliotheque {
    display: flex;
    flex-direction: column;
    min-height: 0;
    border-right: 1px solid var(--bordure);
    background: var(--surface);
  }

  .recherche {
    position: relative;
    display: flex;
    align-items: center;
    margin: 10px;
  }

  .recherche :global(svg) {
    position: absolute;
    left: 9px;
    color: var(--texte-secondaire);
  }

  .recherche input {
    height: 32px;
    padding-left: 30px;
  }

  .liste {
    flex: 1;
    overflow-y: auto;
    padding: 0 6px 12px;
  }

  details {
    margin-bottom: 4px;
  }

  summary {
    padding: 6px 6px;
    border-radius: var(--rayon);
    color: var(--texte-secondaire);
    font-size: 12px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.03em;
    cursor: pointer;
  }

  summary:hover {
    background: var(--surface-2);
  }

  .nombre {
    font-weight: 400;
  }

  ul {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .element {
    display: flex;
    align-items: center;
    gap: 8px;
    width: 100%;
    padding: 5px 8px;
    border: none;
    border-radius: var(--rayon);
    background: transparent;
    color: var(--texte);
    font-size: 13px;
    text-align: left;
    cursor: grab;
  }

  .element:hover,
  .element:focus-visible {
    background: var(--surface-2);
  }

  .element svg {
    flex: none;
    color: var(--principale);
  }

  .vide {
    margin: 8px;
    color: var(--texte-secondaire);
    font-size: 13px;
  }
</style>
