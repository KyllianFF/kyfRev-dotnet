<script lang="ts">
  import { onMount } from "svelte";
  import { messageErreur } from "../../lib/api";
  import { definition } from "../../lib/topologie/catalogue";
  import { schemasDuCoffre } from "../../lib/topologie/coffreSchemas";
  import { couleurDuType } from "../../lib/topologie/dessin";
  import { DEFINITIONS_ILLUSTRATIONS, illustration } from "../../lib/topologie/illustrations";
  import {
    chercherPartout,
    type GenreResultat,
    type Index,
    indexer,
    RECHERCHE_MIN,
    type Resultat,
  } from "../../lib/topologie/rechercheGlobale";

  // Recherche dans tous les schémas du coffre (31E) : un équipement, une adresse IP ou un VLAN. Le champ est celui de
  // qui la montre (la vue Schémas, la recherche de l'éditeur) ; ici, les résultats, groupés et plafonnés, chacun avec
  // son schéma et sa page. Un clic (ou Entrée) l'ouvre. Flèche bas depuis le champ entre dans les résultats ; les
  // flèches s'y déplacent, flèche haut sur le premier rend la main au champ. Une même adresse portée par deux
  // équipements est signalée, avec un conseil discret qui ne bloque rien.
  let { texte, onOuvrir }: { texte: string; onOuvrir: (schemaId: string, pageId?: string, objetId?: string) => void } = $props();

  let index = $state.raw<Index | null>(null);
  let erreur = $state<string | null>(null);
  /** Les groupes dont « N de plus » a été choisi : une nouvelle recherche les referme. */
  let ouverts = $state.raw<ReadonlySet<GenreResultat>>(new Set());
  let racine = $state<HTMLElement>();
  /** Le champ d'où l'on est entré dans les résultats, pour y revenir. */
  let champ: HTMLInputElement | null = null;

  const actif = $derived(texte.trim().length >= RECHERCHE_MIN);
  const reponse = $derived(index && actif ? chercherPartout(index, texte, ouverts) : null);

  let precedent = "";
  $effect.pre(() => {
    const t = texte.trim();
    if (t !== precedent) {
      precedent = t;
      ouverts = new Set();
    }
  });

  async function charger() {
    try {
      index = indexer(await schemasDuCoffre());
      erreur = null;
    } catch (e) {
      erreur = messageErreur(e);
    }
  }

  onMount(() => {
    void charger();
    // De retour sur la vue : un schéma a pu changer entre-temps. Seul ce qui a changé est relu.
    const revenus = () => void charger();
    document.addEventListener("schemas-revenus", revenus);
    // Flèche bas dans le champ qui porte ce texte : la main passe au premier résultat.
    const depuisLeChamp = (e: KeyboardEvent) => {
      if (e.key !== "ArrowDown" || e.defaultPrevented || !actif) return;
      const cible = e.target;
      if (!(cible instanceof HTMLInputElement) || cible.value.trim() !== texte.trim()) return;
      const premier = racine?.querySelector<HTMLButtonElement>("button.trouve");
      if (!premier) return;
      e.preventDefault();
      champ = cible;
      premier.focus();
    };
    document.addEventListener("keydown", depuisLeChamp);
    return () => {
      document.removeEventListener("schemas-revenus", revenus);
      document.removeEventListener("keydown", depuisLeChamp);
    };
  });

  function naviguer(e: KeyboardEvent) {
    const tous = [...(racine?.querySelectorAll<HTMLButtonElement>("button") ?? [])];
    const ici = tous.indexOf(document.activeElement as HTMLButtonElement);
    if (ici < 0) return;
    if (e.key === "ArrowDown") tous[Math.min(ici + 1, tous.length - 1)]?.focus();
    else if (e.key === "ArrowUp") {
      if (ici === 0 && champ?.isConnected) champ.focus();
      else tous[Math.max(ici - 1, 0)]?.focus();
    } else if (e.key === "Home") tous[0]?.focus();
    else if (e.key === "End") tous[tous.length - 1]?.focus();
    else return;
    e.preventDefault();
    e.stopPropagation();
  }

  const ouvrir = (r: Resultat) => onOuvrir(r.schemaId, r.pageId, r.objetId);

  function plus(genre: GenreResultat) {
    // La main reste dans le groupe : sur le premier résultat qui n'était pas montré.
    const avant = racine?.querySelectorAll(`[data-groupe="${genre}"] button.trouve`).length ?? 0;
    ouverts = new Set([...ouverts, genre]);
    requestAnimationFrame(() => racine?.querySelectorAll<HTMLButtonElement>(`[data-groupe="${genre}"] button.trouve`)[avant]?.focus());
  }

  const typeDe = (r: Resultat) => r.type ?? "generique";
  const pluriel = (n: number, un: string, plusieurs: string) => `${n} ${n > 1 ? plusieurs : un}`;
</script>

{#if texte.trim().length > 0}
  <!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
  <section class="recherche-globale" aria-label="Dans tous les schémas" bind:this={racine} onkeydown={naviguer}>
    <svg class="definitions" width="0" height="0" aria-hidden="true"><defs>{@html DEFINITIONS_ILLUSTRATIONS}</defs></svg>
    {#if !actif}
      <p class="etat">La recherche dans tous les schémas commence à deux caractères.</p>
    {:else if erreur}
      <p class="etat erreur" role="alert">Les schémas ne se lisent pas : {erreur}</p>
    {:else if !reponse}
      <p class="etat" aria-busy="true">Lecture des schémas du coffre…</p>
    {:else}
      <p class="visuellement-cache" role="status">
        {reponse.total === 0
          ? "Aucun résultat dans les schémas du coffre."
          : `${pluriel(reponse.total, "résultat", "résultats")} dans les schémas du coffre.`}
      </p>
      {#each reponse.groupes as g (g.genre)}
        <div class="groupe" data-groupe={g.genre}>
          <h3>{g.titre} <span class="compte">{g.resultats.length + g.restants}</span></h3>
          <ul>
            {#each g.resultats as r (r.cle)}
              <li>
                <button type="button" class="trouve" data-genre={r.genre} onclick={() => ouvrir(r)}>
                  {#if r.genre === "vlan"}
                    <span class="vlan" style:--couleur-vlan={r.couleur ?? null} aria-hidden="true"></span>
                  {:else}
                    <svg class="icone" viewBox="2 2 60 60" width="28" height="28" aria-hidden="true">
                      {@html illustration(definition(typeDe(r)).icone, couleurDuType(typeDe(r)))}
                    </svg>
                  {/if}
                  <span class="quoi">
                    <span class="nom">{r.nom}</span>
                    {#if r.adresse}<span class="adresse">{r.adresse}</span>{/if}
                    {#if r.memeAdresse}<span class="meme">même adresse</span>{/if}
                    {#if r.detail}<span class="detail">{r.detail}</span>{/if}
                  </span>
                  <span class="ou">
                    {r.schemaTitre}{#if r.pageNom}<span aria-hidden="true">{" › "}</span><span class="visuellement-cache">{", page "}</span>{r.pageNom}{/if}{#if r.archive}<span class="archive">{" · archivé"}</span>{/if}
                  </span>
                </button>
              </li>
            {/each}
          </ul>
          {#if g.restants > 0}
            <button type="button" class="plus" onclick={() => plus(g.genre)}>{g.restants} de plus</button>
          {/if}
        </div>
      {:else}
        <p class="etat">Rien ne correspond dans les schémas du coffre.</p>
      {/each}
      {#each reponse.conseils as c (c.adresse)}
        <p class="conseil"><span class="point" aria-hidden="true"></span>{c.texte}</p>
      {/each}
    {/if}
  </section>
{/if}

<style>
  .recherche-globale {
    container-type: inline-size;
    display: flex;
    flex-direction: column;
    gap: 10px;
    min-width: 0;
  }

  .definitions {
    position: absolute;
    width: 0;
    height: 0;
  }

  .etat {
    margin: 0;
    padding: 6px 2px;
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .etat.erreur {
    color: var(--retard);
  }

  h3 {
    margin: 0 0 4px;
    padding: 0 2px;
    color: var(--texte-secondaire);
    font-size: 12.5px;
    font-weight: 600;
  }

  .compte {
    font-weight: 400;
  }

  ul {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  li + li {
    border-top: 1px solid var(--bordure);
  }

  .trouve {
    display: grid;
    grid-template-columns: 28px minmax(0, 1fr) auto;
    align-items: center;
    gap: 4px 10px;
    width: 100%;
    min-height: 44px;
    padding: 6px 8px;
    border: none;
    border-radius: var(--rayon);
    background: transparent;
    color: var(--texte);
    text-align: left;
  }

  .trouve:hover,
  .trouve:focus-visible {
    background: var(--navigation-active-fond);
  }

  .icone {
    display: block;
  }

  .vlan {
    justify-self: center;
    width: 14px;
    height: 14px;
    border-radius: 4px;
    background: var(--couleur-vlan, var(--texte-secondaire));
  }

  .quoi {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: 2px 10px;
    min-width: 0;
  }

  .nom {
    font-weight: 600;
    overflow-wrap: anywhere;
  }

  .adresse {
    font-family: var(--police-code);
    font-size: 13px;
    overflow-wrap: anywhere;
  }

  .meme {
    align-self: center;
    padding: 0 8px;
    border-radius: 999px;
    background: var(--avertissement-fond);
    color: var(--texte);
    font-size: 11.5px;
    line-height: 19px;
    white-space: nowrap;
  }

  .detail {
    color: var(--texte-secondaire);
    font-size: 12.5px;
  }

  .ou {
    color: var(--texte-secondaire);
    font-size: 12.5px;
    text-align: right;
  }

  .archive {
    font-style: italic;
  }

  .plus {
    margin: 2px 0 0 38px;
    padding: 2px 8px;
    border: none;
    border-radius: var(--rayon);
    background: transparent;
    color: var(--lien);
    font-size: 13px;
  }

  .plus:hover {
    background: var(--surface-2);
  }

  .conseil {
    display: flex;
    align-items: flex-start;
    gap: 8px;
    margin: 0;
    padding: 7px 10px;
    border-radius: var(--rayon);
    background: var(--avertissement-fond);
    color: var(--texte);
    font-size: 12.5px;
    line-height: 17px;
  }

  .point {
    flex: none;
    width: 7px;
    height: 7px;
    margin-top: 5px;
    border-radius: 50%;
    background: var(--avertissement);
  }

  /* Étroit : le schéma et la page passent sous le nom. */
  @container (max-width: 440px) {
    .trouve {
      grid-template-columns: 28px minmax(0, 1fr);
    }

    .ou {
      grid-column: 2;
      text-align: left;
    }
  }
</style>
