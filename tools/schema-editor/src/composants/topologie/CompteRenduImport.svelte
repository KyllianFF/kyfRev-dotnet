<script lang="ts">
  import { onMount } from "svelte";
  import { bilanImport, type CompteRendu, remarquesImport } from "../../lib/topologie/importation";

  // « Le schéma est importé » (29E) : ce qui est entré, et ce qu'il vaut la peine de regarder. « Annuler
  // l'import » ne garde rien ; « Garder » le garde ; « Les montrer » le garde et choisit, sur le plan, les
  // formes posées en « Générique ».
  let {
    titre,
    compteRendu,
    onannuler,
    ongarder,
    onmontrer,
  }: {
    titre: string;
    compteRendu: CompteRendu;
    onannuler: () => void;
    ongarder: () => void;
    /** Choisir les formes inconnues sur le plan ; sans lui, le lien n'est pas proposé. */
    onmontrer?: () => void;
  } = $props();

  let dialogue: HTMLDialogElement;
  /** Ce que la fenêtre fait en se fermant : Échap annule, comme le bouton. */
  let issue: "annuler" | "garder" | "montrer" = "annuler";
  const bilan = $derived(bilanImport(compteRendu));
  const remarques = $derived(remarquesImport(compteRendu));

  onMount(() => dialogue.showModal());

  function fermer(choix: typeof issue) {
    issue = choix;
    dialogue.close();
  }
</script>

<dialog
  bind:this={dialogue}
  class="compte-rendu-import"
  aria-labelledby="titre-compte-rendu"
  aria-describedby="schema-compte-rendu"
  onclose={() => {
    if (issue === "garder") ongarder();
    else if (issue === "montrer") onmontrer?.();
    else onannuler();
  }}
>
  <h2 id="titre-compte-rendu">Le schéma est importé</h2>
  <p id="schema-compte-rendu" class="schema">« {titre} »</p>
  <ul class="bilan">
    {#each bilan as ligne (ligne)}<li>{ligne}</li>{/each}
  </ul>
  {#if remarques.length > 0}
    <h3>À regarder</h3>
    <ul class="remarques">
      {#each remarques as remarque (remarque.texte)}
        <li>
          {remarque.texte}{#if remarque.montrer && onmontrer}{" · "}<button type="button" class="lien-texte" onclick={() => fermer("montrer")}>Les montrer</button>{/if}
        </li>
      {/each}
    </ul>
  {/if}
  <div class="actions">
    <button type="button" class="bouton" onclick={() => fermer("annuler")}>Annuler l'import</button>
    <!-- svelte-ignore a11y_autofocus -->
    <button type="button" class="bouton principal" autofocus onclick={() => fermer("garder")}>Garder</button>
  </div>
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

  h3 {
    margin: 16px 0 6px;
    font-size: 14px;
  }

  .schema {
    margin: 4px 0 12px;
    color: var(--texte-secondaire);
    overflow-wrap: anywhere;
  }

  ul {
    display: grid;
    gap: 4px;
    margin: 0;
    padding-left: 20px;
  }

  .remarques {
    padding: 8px 10px 8px 28px;
    border-left: 2px solid var(--avertissement);
    border-radius: var(--rayon);
    background: var(--avertissement-fond);
    font-size: 13px;
  }

  li {
    overflow-wrap: anywhere;
  }

  .lien-texte {
    padding: 0;
    border: none;
    background: none;
    color: var(--lien);
    font: inherit;
    font-weight: 600;
    text-decoration: underline;
    cursor: pointer;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 16px;
  }
</style>
