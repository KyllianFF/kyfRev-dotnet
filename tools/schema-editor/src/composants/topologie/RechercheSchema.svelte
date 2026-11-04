<script lang="ts">
  import Search from "@lucide/svelte/icons/search";
  import X from "@lucide/svelte/icons/x";
  import { onMount } from "svelte";
  import type { Schema } from "../../lib/topologie/modele";
  import { chercherDansSchema, type Trouve } from "../../lib/topologie/recherche";

  // Recherche dans le schéma (D49, Ctrl+F) : la liste se met à jour à la frappe ;
  // Entrée (ou un clic) amène l'objet au centre et le choisit ; Échap ferme.
  let {
    schema,
    texte = $bindable(""),
    onchoisir,
    onfermer,
  }: {
    schema: Schema;
    texte?: string;
    onchoisir: (trouve: Trouve) => void;
    onfermer: () => void;
  } = $props();

  let champ: HTMLInputElement;
  let actif = $state(0);
  const trouves = $derived(chercherDansSchema(schema, texte));

  $effect(() => {
    void trouves;
    actif = 0;
  });

  onMount(() => champ.select());

  function clavier(e: KeyboardEvent) {
    if (e.key === "Escape") {
      e.preventDefault();
      e.stopPropagation();
      onfermer();
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

<div class="recherche-schema" role="search">
  <div class="saisie">
    <Search size={16} aria-hidden="true" />
    <input
      bind:this={champ}
      bind:value={texte}
      class="champ"
      type="search"
      placeholder="Équipement, adresse IP, port, câble…"
      aria-label="Chercher dans le schéma"
      aria-controls="trouves-schema"
      aria-activedescendant={trouves[actif] ? `trouve-${actif}` : undefined}
      onkeydown={clavier}
    />
    <button type="button" class="bouton discret petit" aria-label="Fermer la recherche" title="Fermer (Échap)" onclick={onfermer}>
      <X size={14} aria-hidden="true" />
    </button>
  </div>
  {#if texte.trim()}
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
