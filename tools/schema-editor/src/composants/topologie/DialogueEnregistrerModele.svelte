<script lang="ts">
  // « Enregistrer comme modèle » (31E) : la place de la fenêtre, ouverte par l'accueil ; son contenu vient à part.
  const props: { schemaId: string; ouvert: boolean; onFermer: () => void } = $props();
  const montrer = (dialogue: HTMLDialogElement) => dialogue.showModal();
</script>

{#if props.ouvert}
  <dialog class="attente" aria-labelledby="titre-enregistrer-modele" use:montrer onclose={() => props.onFermer()}>
    <h2 id="titre-enregistrer-modele">Enregistrer comme modèle</h2>
    <div class="actions">
      <button type="button" class="bouton" onclick={(e) => e.currentTarget.closest("dialog")?.close()}>Fermer</button>
    </div>
  </dialog>
{/if}

<style>
  .attente {
    width: min(480px, calc(100vw - 32px));
    padding: 24px;
  }

  h2 {
    font-size: 18px;
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    margin-top: 16px;
  }
</style>
