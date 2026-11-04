<script lang="ts">
  import { onMount, untrack } from "svelte";

  // Petite boîte à un seul champ (« Renommer… », « Nouveau dossier… »).
  let {
    titre,
    libelle,
    valeur,
    libelleValider,
    toujoursValider = false,
    verifier,
    onvalider,
    onfermer,
  }: {
    titre: string;
    libelle: string;
    valeur: string;
    libelleValider: string;
    /** Valider même si la saisie n'a pas changé (un nom proposé, à confirmer). */
    toujoursValider?: boolean;
    /** Raison de refuser la saisie (affichée sous le champ), ou `null`. */
    verifier?: (valeur: string) => string | null;
    onvalider: (valeur: string) => void;
    onfermer: () => void;
  } = $props();

  let dialogue: HTMLDialogElement;
  let saisie = $state(untrack(() => valeur));
  let erreur = $state<string | null>(null);
  let valide = false;

  onMount(() => dialogue.showModal());

  function valider(evenement: SubmitEvent) {
    evenement.preventDefault();
    if (saisie.trim() === "") return;
    erreur = verifier?.(saisie.trim()) ?? null;
    if (erreur) return;
    valide = true;
    dialogue.close();
  }
</script>

<dialog
  bind:this={dialogue}
  aria-labelledby="titre-saisie"
  onclose={() => {
    if (valide && (toujoursValider || saisie.trim() !== valeur.trim())) onvalider(saisie.trim());
    onfermer();
  }}
>
  <form onsubmit={valider}>
    <h2 id="titre-saisie">{titre}</h2>
    <label>
      {libelle}
      <!-- svelte-ignore a11y_autofocus -->
      <input
        class="champ"
        bind:value={saisie}
        autofocus
        onfocus={(e) => e.currentTarget.select()}
        oninput={() => (erreur = null)}
        aria-invalid={erreur !== null}
        aria-describedby={erreur ? "erreur-saisie" : undefined}
      />
    </label>
    {#if erreur}
      <p id="erreur-saisie" class="erreur" role="alert">{erreur}</p>
    {/if}
    <div class="actions">
      <button type="button" class="bouton" onclick={() => dialogue.close()}>Annuler</button>
      <button type="submit" class="bouton principal" disabled={saisie.trim() === ""}>{libelleValider}</button>
    </div>
  </form>
</dialog>

<style>
  dialog {
    width: min(460px, calc(100vw - 32px));
    padding: 24px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 16px 48px rgb(0 0 0 / 0.2);
  }

  dialog::backdrop {
    background: var(--voile);
  }

  h2 {
    margin-bottom: 12px;
    font-size: 18px;
  }

  label {
    display: grid;
    gap: 4px;
    font-weight: 600;
  }

  label .champ {
    font-weight: 400;
  }

  .erreur {
    margin: 6px 0 0;
    color: var(--retard);
    font-size: 13px;
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 16px;
  }
</style>
