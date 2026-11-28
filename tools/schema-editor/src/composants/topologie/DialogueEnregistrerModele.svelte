<script lang="ts">
  import { api, messageErreur } from "../../lib/api";
  import { apercuSchema } from "../../lib/topologie/apercu";
  import { ecrireSchema, lireSchema } from "../../lib/topologie/format";
  import type { Schema } from "../../lib/topologie/modele";
  import { modeleDepuisSchema } from "../../lib/topologie/modelesDeSchema";
  import { aPlusieursPages, nomDeLaPage, pageValide } from "../../lib/topologie/pages";

  // « Enregistrer comme modèle… » (31E) : le schéma, ou une seule de ses pages (`pageId`), devient un modèle de
  // l'utilisateur, rangé à part dans le coffre et proposé ensuite dans « Nouveau schéma depuis un modèle… ». Le
  // schéma est relu dans le coffre : c'est ce qui y est enregistré qui sert de modèle.
  let {
    schemaId,
    ouvert,
    onFermer,
    pageId,
  }: {
    schemaId: string;
    ouvert: boolean;
    onFermer: () => void;
    /** Une seule page du schéma ; sinon, le schéma entier. */
    pageId?: string;
  } = $props();

  let schema = $state.raw<Schema | null>(null);
  let nom = $state("");
  let description = $state("");
  let erreur = $state<string | null>(null);
  let occupe = $state(false);
  /** Le nom du modèle enregistré : le dialogue le dit, puis se ferme. */
  let enregistre = $state<string | null>(null);
  let dialogue = $state<HTMLDialogElement>();

  /** Une page d'un schéma qui en a plusieurs ; sinon, tout le schéma. */
  const page = $derived(schema && pageId !== undefined && aPlusieursPages(schema) ? pageValide(schema, pageId) : undefined);
  // Ce qui sera gardé, sans attendre le nom : la vignette ne se redessine pas à chaque lettre.
  const apercu = $derived(schema ? modeleDepuisSchema(schema, "", "", page) : null);

  const prendreLaMain = (bouton: HTMLButtonElement) => bouton.focus();

  function preparer(dialog: HTMLDialogElement) {
    schema = null;
    erreur = null;
    enregistre = null;
    nom = "";
    description = "";
    dialog.showModal();
    void lire();
  }

  async function lire() {
    try {
      const lu = lireSchema((await api.lireSchema(schemaId)).texte).schema;
      schema = lu;
      const unePage = pageId !== undefined && aPlusieursPages(lu);
      nom = unePage ? nomDeLaPage(lu, pageValide(lu, pageId)) : lu.titre;
    } catch (e) {
      erreur = `Le schéma ne se lit pas : ${messageErreur(e)}`;
    }
  }

  async function enregistrer(e: SubmitEvent) {
    e.preventDefault();
    if (!schema || occupe || !nom.trim()) return;
    occupe = true;
    erreur = null;
    try {
      await api.creerModeleSchema(ecrireSchema(modeleDepuisSchema(schema, nom, description, page)));
      enregistre = nom.trim();
    } catch (err) {
      erreur = `Le modèle n'a pas été enregistré : ${messageErreur(err)}`;
    } finally {
      occupe = false;
    }
  }
</script>

{#if ouvert}
  <dialog class="dialogue-modele" aria-labelledby="titre-enregistrer-modele" bind:this={dialogue} use:preparer onclose={onFermer}>
    {#if enregistre}
      <h2 id="titre-enregistrer-modele">Modèle enregistré</h2>
      <p class="fait" role="status">
        « {enregistre} » est proposé dans « Nouveau schéma depuis un modèle… », sous « Mes modèles ».
      </p>
      <div class="actions">
        <!-- Le formulaire a disparu avec le bouton qui avait la main : « Fermer » la prend. -->
        <button type="button" class="bouton principal fermer" use:prendreLaMain onclick={() => dialogue?.close()}>Fermer</button>
      </div>
    {:else}
      <form onsubmit={enregistrer}>
        <h2 id="titre-enregistrer-modele">{page !== undefined ? "Enregistrer cette page comme modèle" : "Enregistrer comme modèle"}</h2>
        <p class="aide">
          {#if schema && page !== undefined}
            La page « {nomDeLaPage(schema, page)} » de « {schema.titre} » : ses équipements, leurs câbles et les VLAN du schéma.
          {:else if schema}
            Le schéma « {schema.titre} » tel qu'il est enregistré : ses équipements, leurs câbles et ses VLAN{aPlusieursPages(schema) ? ", sur toutes ses pages" : ""}.
          {:else if !erreur}
            Lecture du schéma…
          {/if}
        </p>
        {#if apercu}
          <div class="vignette" aria-hidden="true">
            {#if apercu.equipements.length > 0}{@html apercuSchema(apercu)}{:else}<span class="vide">Rien n'est dessiné ici.</span>{/if}
          </div>
        {/if}
        <label>
          Nom du modèle
          <!-- svelte-ignore a11y_autofocus -->
          <input class="champ" bind:value={nom} maxlength="200" required autofocus onfocus={(e) => e.currentTarget.select()} />
        </label>
        <label>
          <span>Description <span class="facultatif">(facultative)</span></span>
          <input class="champ" bind:value={description} maxlength="200" placeholder="Ce qu'il contient : switch d'étage, bornes, prises…" />
        </label>
        {#if erreur}<p class="erreur" role="alert">{erreur}</p>{/if}
        <div class="actions">
          <button type="button" class="bouton" onclick={() => dialogue?.close()}>Annuler</button>
          <button type="submit" class="bouton principal" disabled={occupe || !schema || !nom.trim()}>Enregistrer</button>
        </div>
      </form>
    {/if}
  </dialog>
{/if}

<style>
  .dialogue-modele {
    width: min(480px, calc(100vw - 32px));
    max-height: calc(100vh - 32px);
    padding: 22px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 16px 48px rgb(0 0 0 / 0.2);
  }

  .dialogue-modele::backdrop {
    background: var(--voile);
  }

  form {
    display: flex;
    flex-direction: column;
    gap: 12px;
  }

  h2 {
    font-size: 18px;
  }

  .aide,
  .fait {
    margin: 0;
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .fait {
    margin: 12px 0 0;
    color: var(--texte);
    font-size: 14px;
  }

  .vignette {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 140px;
    padding: 8px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--fond);
  }

  /* Le dessin occupe la vignette, à ses proportions. */
  .vignette > :global(svg) {
    width: 100%;
    height: 100%;
  }

  .vide {
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  label {
    display: grid;
    gap: 4px;
    font-weight: 600;
  }

  label .champ {
    font-weight: 400;
  }

  .facultatif {
    color: var(--texte-secondaire);
    font-weight: 400;
  }

  .erreur {
    margin: 0;
    color: var(--retard);
    font-size: 13px;
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 4px;
  }

  .fait + .actions {
    margin-top: 16px;
  }
</style>
