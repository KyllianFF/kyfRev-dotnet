<script lang="ts">
  import { onMount, untrack } from "svelte";
  import { FORMATS_EXPORT, type FormatExport } from "../../lib/topologie/sorties";

  // « Exporter le schéma » : le format d'abord ; le dossier se choisit ensuite, dans la fenêtre de l'application.
  let {
    titre,
    vide = false,
    onvalider,
    onfermer,
  }: {
    /** Titre du schéma à exporter. */
    titre: string;
    /** Schéma sans équipement : pas d'image, seul le modèle s'exporte. */
    vide?: boolean;
    onvalider: (format: FormatExport) => void;
    onfermer: () => void;
  } = $props();

  let dialogue: HTMLDialogElement;
  let format = $state<FormatExport>(untrack(() => (vide ? "json" : "png")));
  let valide = false;

  onMount(() => dialogue.showModal());

  function valider(evenement: SubmitEvent) {
    evenement.preventDefault();
    valide = true;
    dialogue.close();
  }
</script>

<dialog
  bind:this={dialogue}
  aria-labelledby="titre-export"
  aria-describedby="schema-export"
  onclose={() => {
    if (valide) onvalider(format);
    onfermer();
  }}
>
  <form onsubmit={valider}>
    <h2 id="titre-export">Exporter le schéma</h2>
    <p id="schema-export" class="schema">« {titre} »</p>
    <fieldset>
      <legend class="visuellement-cache">Format de l'export</legend>
      {#each FORMATS_EXPORT as f (f.valeur)}
        <!-- svelte-ignore a11y_autofocus -->
        <label class="choix">
          <input
            type="radio"
            name="format-export"
            value={f.valeur}
            bind:group={format}
            disabled={vide && f.image}
            autofocus={f.valeur === format}
          />
          <span class="nom">{f.nom}</span>
          <span class="aide">{f.aide}</span>
        </label>
      {/each}
    </fieldset>
    {#if vide}<p class="note">Ce schéma est vide : il n'a pas d'image, seul son modèle s'exporte.</p>{/if}
    <div class="actions">
      <button type="button" class="bouton" onclick={() => dialogue.close()}>Annuler</button>
      <button type="submit" class="bouton principal">Choisir le dossier…</button>
    </div>
  </form>
</dialog>

<style>
  dialog {
    width: min(460px, calc(100vw - 32px));
    max-height: calc(100vh - 32px);
    padding: 24px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 16px 48px rgb(0 0 0 / 0.2);
    overflow: auto;
  }

  dialog::backdrop {
    background: var(--voile);
  }

  h2 {
    font-size: 18px;
  }

  .schema {
    margin: 4px 0 14px;
    color: var(--texte-secondaire);
    overflow-wrap: anywhere;
  }

  fieldset {
    display: grid;
    gap: 6px;
    min-width: 0;
    margin: 0;
    padding: 0;
    border: none;
  }

  .choix {
    display: grid;
    grid-template-columns: auto minmax(0, 1fr);
    align-items: center;
    gap: 0 10px;
    min-height: 48px;
    padding: 6px 12px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    cursor: pointer;
  }

  .choix:hover {
    background: var(--surface-2);
  }

  .choix:has(input:checked) {
    border-color: var(--principale);
    background: var(--surface-2);
  }

  .choix:has(input:disabled) {
    opacity: 0.5;
    cursor: not-allowed;
  }

  .choix:has(input:disabled):hover {
    background: none;
  }

  .choix input {
    grid-row: span 2;
    width: 16px;
    height: 16px;
    margin: 0;
    accent-color: var(--principale);
  }

  .nom {
    font-weight: 600;
  }

  .aide {
    grid-column: 2;
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .note {
    margin: 10px 0 0;
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 16px;
  }
</style>
