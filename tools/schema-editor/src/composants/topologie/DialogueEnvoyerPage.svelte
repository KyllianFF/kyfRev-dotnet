<script lang="ts">
  import { onMount } from "svelte";
  import { api, messageErreur } from "../../lib/api";
  import type { SchemaResume } from "../../lib/generes/SchemaResume";
  import { ecrireEchange, lireEchange } from "../../lib/topologie/echanges";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import type { Schema } from "../../lib/topologie/modele";
  import { nomDeLaPage } from "../../lib/topologie/pages";
  import { extrairePages } from "../../lib/topologie/reunion";
  import { compteRendu, copierPages } from "../../lib/topologie/transfert";

  // Le chemin inverse de « Pages d'un autre schéma… » (31D), au clic droit sur un onglet : copier ou déplacer la page
  // vers un autre schéma. Le schéma qui la reçoit s'écrit dans le coffre ; celui-ci, ouvert, ne change que par
  // l'éditeur : une page déplacée en est retirée en une opération que Ctrl+Z annule.
  let { editeur, page, deplacer, onfermer }: { editeur: EditeurSchema; page: string; deplacer: boolean; onfermer: () => void } = $props();

  let dialogue: HTMLDialogElement;
  let liste = $state<SchemaResume[] | null>(null);
  let filtre = $state("");
  let cible = $state<string | null>(null);
  let enCours = $state(false);
  let erreur = $state<string | null>(null);

  const nom = $derived(nomDeLaPage(editeur.schema, page));
  const cables = $derived(extrairePages(editeur.schema, [page]).cablesHors);
  const possibles = $derived(
    (liste ?? []).filter(
      (s) => s.id !== editeur.id && (!filtre.trim() || s.titre.toLocaleLowerCase("fr").includes(filtre.trim().toLocaleLowerCase("fr"))),
    ),
  );
  const pluriel = (n: number, un: string, plusieurs: string) => `${n} ${n > 1 ? plusieurs : un}`;

  onMount(() => {
    dialogue.showModal();
    void api
      .schemas()
      .then((schemas) => (liste = schemas))
      .catch((e: unknown) => {
        erreur = `La liste des schémas est illisible : ${messageErreur(e)}`;
        liste = [];
      });
  });

  async function envoyer(evenement: SubmitEvent) {
    evenement.preventDefault();
    if (!cible || enCours) return;
    enCours = true;
    erreur = null;
    try {
      const recue = await lireEchange(cible);
      const schema = JSON.parse(JSON.stringify(recue.schema)) as Schema;
      const source = JSON.parse(JSON.stringify(editeur.schema)) as Schema;
      const transfert = copierPages(source, [page], schema);
      await ecrireEchange(recue, schema);
      const phrases = compteRendu(transfert, { source: source.titre, cible: recue.schema.titre, deplacer });
      if (deplacer) editeur.retirerPages([page], `${phrases.join(" ")} Ctrl+Z la rend ici.`);
      else editeur.annonce = phrases.join(" ");
      dialogue.close();
    } catch (e) {
      erreur = `${deplacer ? "Déplacement" : "Copie"} impossible : ${messageErreur(e)}. Rien n'a été modifié.`;
      enCours = false;
    }
  }
</script>

<dialog bind:this={dialogue} class="dialogue-envoyer-page" aria-labelledby="titre-envoyer-page" onclose={onfermer}>
  <form onsubmit={envoyer}>
    <h2 id="titre-envoyer-page">{deplacer ? "Déplacer" : "Copier"} « {nom} » vers un autre schéma</h2>
    <p class="aide-tete">
      {deplacer
        ? "Elle quitte ce schéma et va après les pages de celui choisi ; Ctrl+Z la rend ici."
        : "Une copie va après les pages du schéma choisi ; celle-ci ne change pas."}
      {#if cables > 0}
        {deplacer
          ? `${pluriel(cables, "câble la relie", "câbles la relient")} à d'autres pages : un câble ne va pas d'un schéma à l'autre, ${cables > 1 ? "ils seront retirés" : "il sera retiré"}.`
          : `${pluriel(cables, "câble la relie", "câbles la relient")} à d'autres pages : ${cables > 1 ? "ils ne seront pas copiés" : "il ne sera pas copié"}.`}
      {/if}
    </p>
    {#if (liste?.length ?? 0) > 6}
      <input class="champ" type="search" placeholder="Chercher un schéma" aria-label="Chercher un schéma" bind:value={filtre} />
    {/if}
    {#if liste === null}
      <p class="aide-tete">Lecture des schémas…</p>
    {:else if possibles.length === 0}
      <p class="aide-tete">Il n'y a pas d'autre schéma où {deplacer ? "la déplacer" : "la copier"}.</p>
    {:else}
      <div class="liste-schemas" role="radiogroup" aria-label="Schéma qui la reçoit">
        {#each possibles as s (s.id)}
          <label class="case-schema">
            <input type="radio" name="cible-page" value={s.id} bind:group={cible} />
            <span class="nom">{s.titre}</span>
            <span class="compte">{pluriel(s.equipements, "équipement", "équipements")}</span>
          </label>
        {/each}
      </div>
    {/if}
    {#if erreur}<p class="erreur" role="alert">{erreur}</p>{/if}
    <div class="actions">
      <button type="button" class="bouton" onclick={() => dialogue.close()}>Annuler</button>
      <button type="submit" class="bouton principal" disabled={!cible || enCours}>
        {enCours ? (deplacer ? "Déplacement…" : "Copie…") : deplacer ? "Déplacer" : "Copier"}
      </button>
    </div>
  </form>
</dialog>

<style>
  dialog {
    width: min(480px, calc(100vw - 32px));
    max-width: none;
    max-height: calc(100vh - 32px);
    padding: 20px 22px;
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

  form {
    display: flex;
    flex-direction: column;
    gap: 10px;
    min-width: 0;
  }

  h2 {
    font-size: 18px;
    overflow-wrap: anywhere;
  }

  .aide-tete {
    margin: 0;
    color: var(--texte-secondaire);
  }

  .liste-schemas {
    display: flex;
    flex-direction: column;
    gap: 4px;
    max-height: min(320px, 50vh);
    padding: 2px;
    overflow-y: auto;
  }

  .case-schema {
    display: flex;
    align-items: center;
    gap: 10px;
    min-height: 40px;
    padding: 0 12px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    cursor: pointer;
  }

  .case-schema:hover {
    background: var(--surface-2);
  }

  .case-schema:has(input:checked) {
    border-color: var(--principale);
  }

  .case-schema input {
    flex: none;
    width: 16px;
    height: 16px;
    margin: 0;
    accent-color: var(--principale);
  }

  .nom {
    min-width: 0;
    overflow-wrap: anywhere;
    font-weight: 600;
  }

  .compte {
    flex: none;
    margin-left: auto;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .erreur {
    margin: 0;
    padding: 8px 10px;
    border-left: 2px solid var(--retard);
    border-radius: var(--rayon);
    background: var(--retard-fond);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 6px;
  }
</style>
