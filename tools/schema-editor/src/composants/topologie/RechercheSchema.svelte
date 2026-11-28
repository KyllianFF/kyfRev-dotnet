<script lang="ts">
  import Search from "@lucide/svelte/icons/search";
  import X from "@lucide/svelte/icons/x";
  import { onMount } from "svelte";
  import type { Schema } from "../../lib/topologie/modele";
  import { chercherDansSchema, type Trouve } from "../../lib/topologie/recherche";
  import RechercheGlobale from "./RechercheGlobale.svelte";

  // Recherche dans le schéma (D49, Ctrl+F) : la liste se met à jour à la frappe ;
  // Entrée (ou un clic) amène l'objet au centre et le choisit ; Échap ferme.
  // « Tous les schémas » (31E) cherche le même texte dans tout le coffre : un résultat d'un autre schéma l'ouvre.
  let {
    schema,
    texte = $bindable(""),
    onchoisir,
    onfermer,
    onouvrir,
  }: {
    schema: Schema;
    texte?: string;
    onchoisir: (trouve: Trouve) => void;
    onfermer: () => void;
    /** Un résultat de la recherche dans tous les schémas ; sans lui, la recherche reste dans ce schéma. */
    onouvrir?: (schemaId: string, pageId?: string, objetId?: string) => void;
  } = $props();

  let champ: HTMLInputElement;
  let actif = $state(0);
  let partout = $state(false);
  const trouves = $derived(chercherDansSchema(schema, texte));

  $effect(() => {
    void trouves;
    actif = 0;
  });

  onMount(() => champ.select());

  function changerPortee(tous: boolean) {
    partout = tous;
    champ.focus();
  }

  function clavier(e: KeyboardEvent) {
    if (e.key === "Escape") {
      e.preventDefault();
      e.stopPropagation();
      onfermer();
    } else if (partout) {
      // Flèche bas : la recherche dans tous les schémas prend la main sur ses résultats. Entrée ouvre le premier.
      if (e.key === "Enter") {
        e.preventDefault();
        champ.closest(".recherche-schema")?.querySelector<HTMLButtonElement>(".recherche-globale button.trouve")?.click();
      }
    } else if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      if (trouves.length) actif = (actif + (e.key === "ArrowDown" ? 1 : trouves.length - 1)) % trouves.length;
    } else if (e.key === "Enter") {
      e.preventDefault();
      const t = trouves[actif];
      if (t) onchoisir(t);
    }
  }
</script>

<!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
<div
  class="recherche-schema"
  class:partout
  role="search"
  onkeydown={(e) => {
    if (e.target === champ) return;
    // Échap depuis un résultat ferme aussi ; les autres touches ne vont pas au plan (elles y déplaceraient ou
    // retireraient ce qui est choisi), sauf les raccourcis avec Ctrl.
    if (e.key === "Escape") {
      e.preventDefault();
      onfermer();
    }
    if (e.key === "Escape" || !(e.ctrlKey || e.metaKey)) e.stopPropagation();
  }}
>
  <div class="saisie">
    <Search size={16} aria-hidden="true" />
    <input
      bind:this={champ}
      bind:value={texte}
      class="champ"
      type="search"
      placeholder={partout ? "Équipement, adresse IP, VLAN…" : "Équipement, adresse IP, port, câble…"}
      aria-label={partout ? "Chercher dans tous les schémas" : "Chercher dans le schéma"}
      aria-controls={partout ? undefined : "trouves-schema"}
      aria-activedescendant={!partout && trouves[actif] ? `trouve-${actif}` : undefined}
      onkeydown={clavier}
    />
    <button type="button" class="bouton discret petit" aria-label="Fermer la recherche" title="Fermer (Échap)" onclick={onfermer}>
      <X size={14} aria-hidden="true" />
    </button>
  </div>
  {#if onouvrir}
    <div class="portee" role="group" aria-label="Où chercher">
      <button type="button" aria-pressed={!partout} onclick={() => changerPortee(false)}>Ce schéma</button>
      <button type="button" aria-pressed={partout} onclick={() => changerPortee(true)}>Tous les schémas</button>
    </div>
  {/if}
  {#if partout && onouvrir}
    <div class="partout-resultats">
      <RechercheGlobale {texte} onOuvrir={onouvrir} />
    </div>
  {:else if texte.trim()}
    <ul id="trouves-schema" role="listbox" aria-label="Résultats">
      {#each trouves as t, i (`${t.genre}-${t.id}-${i}`)}
        <li id="trouve-{i}" role="option" aria-selected={i === actif}>
          <button type="button" class:actif={i === actif} onclick={() => onchoisir(t)} onmouseenter={() => (actif = i)}>
            <strong>{t.libelle}</strong>
            {#if t.detail}<span>{t.detail}</span>{/if}
          </button>
        </li>
      {:else}
        <li class="vide">Rien ne correspond.</li>
      {/each}
    </ul>
  {/if}
</div>

<style>
  .recherche-schema {
    position: absolute;
    top: 10px;
    right: 10px;
    z-index: 4;
    width: min(340px, calc(100% - 20px));
    padding: 6px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--surface);
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.18);
  }

  .saisie {
    display: flex;
    align-items: center;
    gap: 6px;
    padding-left: 4px;
    color: var(--texte-secondaire);
  }

  .saisie .champ {
    flex: 1;
    height: 30px;
  }

  .recherche-schema.partout {
    width: min(460px, calc(100% - 20px));
  }

  .portee {
    display: flex;
    margin: 6px 0 0;
    border: 1px solid var(--trait-fort);
    border-radius: var(--rayon);
    overflow: hidden;
  }

  .portee button {
    flex: 1;
    min-height: 28px;
    padding: 0 10px;
    border: none;
    background: var(--surface);
    color: var(--texte);
    font-size: 13px;
    white-space: nowrap;
  }

  .portee button:hover {
    background: var(--surface-2);
  }

  .portee button[aria-pressed="true"] {
    background: var(--principale);
    color: var(--sur-principale);
    font-weight: 600;
  }

  .portee button:focus-visible {
    outline-offset: -3px;
  }

  .partout-resultats {
    max-height: min(420px, calc(100vh - 260px));
    margin-top: 8px;
    padding: 0 2px;
    overflow-y: auto;
  }

  ul {
    max-height: 280px;
    margin: 6px 0 0;
    padding: 0;
    overflow-y: auto;
    list-style: none;
  }

  li button {
    display: flex;
    flex-direction: column;
    width: 100%;
    padding: 5px 8px;
    border: none;
    border-radius: 4px;
    background: transparent;
    color: var(--texte);
    text-align: left;
    cursor: pointer;
  }

  li button.actif {
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
  }

  li span {
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .vide {
    padding: 6px 8px;
    color: var(--texte-secondaire);
    font-size: 13px;
  }
</style>
