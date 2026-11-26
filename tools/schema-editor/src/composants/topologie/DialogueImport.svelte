<script lang="ts">
  import { onMount } from "svelte";

  // « Importer un schéma » (29E) : où va le fichier, d'abord ; il se choisit ensuite, dans la fenêtre de l'application.
  // Depuis un schéma ouvert, il peut le remplacer ; depuis la liste, il en fait un nouveau.
  let {
    titre = null,
    onvalider,
    onfermer,
  }: {
    /** Le schéma ouvert, que l'import peut remplacer ; rien depuis la liste. */
    titre?: string | null;
    onvalider: (remplacer: boolean) => void;
    onfermer: () => void;
  } = $props();

  let dialogue: HTMLDialogElement;
  let destination = $state<"nouveau" | "remplacer">("nouveau");
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
  class="dialogue-import"
  aria-labelledby="titre-import-schema"
  aria-describedby="aide-import-schema"
  onclose={() => {
    if (valide) onvalider(titre !== null && destination === "remplacer");
    onfermer();
  }}
>
  <form onsubmit={valider}>
    <h2 id="titre-import-schema">Importer un schéma</h2>
    <p id="aide-import-schema" class="aide">Un modèle exporté de kyfRev (.json), ou un fichier draw.io (.drawio, .xml).</p>
    {#if titre !== null}
      <fieldset>
        <legend class="visuellement-cache">Où importer</legend>
        <!-- svelte-ignore a11y_autofocus -->
        <label class="choix">
          <input type="radio" name="destination-import" value="nouveau" bind:group={destination} autofocus />
          <span class="nom">Dans un nouveau schéma</span>
          <span class="aide">Celui-ci reste tel qu'il est.</span>
        </label>
        <label class="choix">
          <input type="radio" name="destination-import" value="remplacer" bind:group={destination} />
          <span class="nom">À la place de « {titre} »</span>
          <span class="aide">Son contenu est remplacé ; Ctrl+Z le rend.</span>
        </label>
      </fieldset>
    {/if}
    <p class="note">Un fichier Visio (.vsdx) : ouvrez-le dans draw.io, puis enregistrez-le en .drawio.</p>
    <div class="actions">
      <button type="button" class="bouton" onclick={() => dialogue.close()}>Annuler</button>
      <button type="submit" class="bouton principal">Choisir le fichier…</button>
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

  .aide {
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  form > .aide {
    margin: 4px 0 14px;
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

  .choix input {
    grid-row: span 2;
    width: 16px;
    height: 16px;
    margin: 0;
    accent-color: var(--principale);
  }

  .nom {
    font-weight: 600;
    overflow-wrap: anywhere;
  }

  .choix .aide {
    grid-column: 2;
  }

  .note {
    margin: 12px 0 0;
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
