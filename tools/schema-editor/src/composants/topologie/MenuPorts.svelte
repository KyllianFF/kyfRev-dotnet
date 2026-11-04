<script lang="ts">
  import Plus from "@lucide/svelte/icons/plus";
  import X from "@lucide/svelte/icons/x";
  import { onMount } from "svelte";
  import type { Equipement, Schema } from "../../lib/topologie/modele";
  import { interfaceIndisponible } from "../../lib/topologie/operations";
  import SelecteurInterface from "./SelecteurInterface.svelte";

  // Petit menu des ports (D42) : ouvert sur l'équipement cliqué, câble en
  // main ; les ports pris sont grisés et disent vers quoi ils vont.
  let {
    schema,
    equipement,
    x,
    y,
    sauf,
    titre,
    onchoisir,
    onajouter,
    onfermer,
  }: {
    schema: Schema;
    equipement: Equipement;
    /** Position dans le plan, en pixels d'écran (le menu se garde dans le cadre). */
    x: number;
    y: number;
    /** Câble dont on déplace un bout : ses propres ports restent libres. */
    sauf?: string;
    titre: string;
    onchoisir: (interfaceId: string) => void;
    /** Aucun port libre : en ajouter un et y brancher le câble. */
    onajouter: () => void;
    onfermer: () => void;
  } = $props();

  let menu: HTMLDivElement;
  let haut = $state(0);
  $effect.pre(() => {
    haut = y;
  });

  const libres = $derived(
    equipement.interfaces.filter(
      (i) => !interfaceIndisponible(schema, { equipement: equipement.id, interface: i.id }, sauf),
    ).length,
  );

  onMount(() => {
    // Le premier port libre prend le focus : Entrée suffit pour un équipement à un port.
    const cadre = menu.offsetParent?.clientHeight ?? 0;
    if (cadre && haut + menu.offsetHeight > cadre - 8) haut = Math.max(8, cadre - menu.offsetHeight - 8);
    const premier = menu.querySelector<HTMLButtonElement>(".port:not(:disabled)");
    (premier ?? menu).focus();
  });
</script>

<div
  bind:this={menu}
  class="menu-ports"
  style="left: {x}px; top: {haut}px"
  role="dialog"
  aria-label={titre}
  tabindex="-1"
>
  <header>
    <strong>{titre}</strong>
    <button type="button" class="bouton discret petit" aria-label="Fermer le menu des ports" title="Fermer (Échap)" onclick={onfermer}>
      <X size={14} aria-hidden="true" />
    </button>
  </header>
  <SelecteurInterface {schema} {equipement} {sauf} etiquette={`Port de ${equipement.nom}`} {onchoisir} />
  <button type="button" class="bouton petit ajout" class:principal={libres === 0} onclick={onajouter}>
    <Plus size={14} aria-hidden="true" />
    {libres === 0 ? "Aucun port libre : ajouter un port et y brancher" : "Ajouter un port et y brancher"}
  </button>
</div>

<style>
  .menu-ports {
    position: absolute;
    z-index: 4;
    width: 300px;
    max-width: calc(100% - 16px);
    max-height: calc(100% - 16px);
    overflow-y: auto;
    padding: 8px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--surface);
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.2);
  }

  .menu-ports:focus {
    outline: none;
  }

  header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    margin-bottom: 6px;
    font-size: 13px;
  }

  .ajout {
    width: 100%;
    margin-top: 6px;
    justify-content: center;
  }

  .menu-ports :global(.groupes) {
    max-height: 200px;
  }
</style>
