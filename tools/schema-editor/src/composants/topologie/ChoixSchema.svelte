<script lang="ts">
  import Copy from "@lucide/svelte/icons/copy";
  import Plus from "@lucide/svelte/icons/plus";
  import Upload from "@lucide/svelte/icons/upload";
  import { onMount } from "svelte";
  import { api, messageErreur } from "../../lib/api";
  import type { SchemaResume } from "../../lib/generes/SchemaResume";
  import { ecrireSchema, lireSchema } from "../../lib/topologie/format";
  import { schemaVide } from "../../lib/topologie/modele";

  // Insérer un schéma réseau dans une fiche (D39) : un nouveau, un schéma du
  // coffre (la même référence peut figurer dans plusieurs fiches), une copie
  // indépendante, ou un fichier importé. La fiche ne reçoit qu'une référence.
  let {
    oninserer,
    onfermer,
  }: {
    /** Le schéma à citer ; `ouvrir` : l'ouvrir aussitôt dans l'éditeur. */
    oninserer: (id: string, ouvrir: boolean) => void;
    onfermer: () => void;
  } = $props();

  let dialogue: HTMLDialogElement;
  let schemas = $state<SchemaResume[] | null>(null);
  let titre = $state("");
  let erreur = $state<string | null>(null);
  let occupe = $state(false);

  onMount(() => {
    dialogue.showModal();
    api
      .schemas()
      .then((liste) => (schemas = liste))
      .catch((e) => (erreur = messageErreur(e)));
  });

  async function agir(action: () => Promise<{ id: string; ouvrir: boolean }>) {
    occupe = true;
    erreur = null;
    try {
      const { id, ouvrir } = await action();
      dialogue.close();
      oninserer(id, ouvrir);
    } catch (e) {
      erreur = messageErreur(e);
    } finally {
      occupe = false;
    }
  }

  /** Un schéma a toujours un nom : on le retrouve ainsi dans « Schémas réseau ». */
  const creer = () => {
    if (!titre.trim()) {
      erreur = "Donnez un nom au schéma : on le retrouve ainsi dans « Schémas réseau ».";
      return;
    }
    return agir(async () => ({
      id: (await api.creerSchema(ecrireSchema(schemaVide(titre.trim())))).id,
      ouvrir: true,
    }));
  };
  const copier = (id: string) => agir(async () => ({ id: (await api.dupliquerSchema(id)).id, ouvrir: true }));
  const citer = (id: string) => agir(async () => ({ id, ouvrir: false }));

  async function importer() {
    const chemin = await api.choisirFichierSchema();
    if (!chemin) return;
    await agir(async () => {
      const lecture = lireSchema(await api.lireImportSchema(chemin));
      if (lecture.erreurs.length > 0) {
        throw new Error(
          `fichier refusé : ${lecture.erreurs.length} erreur(s) de structure, dont « ${lecture.erreurs[0]} »`,
        );
      }
      return { id: (await api.creerSchema(ecrireSchema({ ...lecture.schema, id: "" }))).id, ouvrir: true };
    });
  }
</script>

<dialog bind:this={dialogue} aria-labelledby="titre-choix-schema" onclose={onfermer}>
  <h2 id="titre-choix-schema">Insérer un schéma réseau</h2>
  <p class="aide">
    Le schéma est rangé dans le coffre ; la fiche n'en garde qu'une référence. Le même schéma peut figurer dans
    plusieurs fiches : le modifier les met toutes à jour. Retirer le bloc d'une fiche ne supprime pas le schéma.
  </p>

  <form class="nouveau" onsubmit={(e) => { e.preventDefault(); void creer(); }}>
    <label>
      <span>Nouveau schéma (nom obligatoire)</span>
      <input class="champ" bind:value={titre} placeholder="Réseau du siège" required aria-required="true" />
    </label>
    <button type="submit" class="bouton principal" disabled={occupe || !titre.trim()}><Plus size={16} aria-hidden="true" /> Créer</button>
    <button type="button" class="bouton" disabled={occupe} onclick={importer}><Upload size={16} aria-hidden="true" /> Importer un fichier…</button>
  </form>

  <h3>Schémas du coffre</h3>
  {#if erreur}
    <p class="erreur" role="alert">{erreur}</p>
  {/if}
  {#if schemas === null}
    <p class="aide" aria-busy="true">Chargement…</p>
  {:else if schemas.length === 0}
    <p class="aide">Aucun schéma pour l'instant.</p>
  {:else}
    <ul class="liste">
      {#each schemas as s (s.id)}
        <li>
          <div class="texte">
            <strong>{s.titre || "Sans titre"}</strong>
            <span class="aide">
              {s.equipements} équipement(s) ·
              {s.fiches.length === 0 ? "dans aucune fiche" : `dans ${s.fiches.map((f) => f.titre).join(", ")}`}
            </span>
          </div>
          <button type="button" class="bouton" disabled={occupe} onclick={() => citer(s.id)}>Insérer</button>
          <button type="button" class="bouton" disabled={occupe} title="Copie indépendante : la modifier ne touche pas l'original" onclick={() => copier(s.id)}>
            <Copy size={15} aria-hidden="true" /> Insérer une copie
          </button>
        </li>
      {/each}
    </ul>
  {/if}
  <div class="actions">
    <button type="button" class="bouton" onclick={() => dialogue.close()}>Annuler</button>
  </div>
</dialog>

<style>
  dialog {
    width: min(720px, calc(100vw - 32px));
    max-height: calc(100vh - 48px);
    padding: 22px;
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
    font-size: 20px;
  }

  h3 {
    margin: 18px 0 6px;
    font-size: 15px;
  }

  .aide {
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .nouveau {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    gap: 8px;
  }

  .nouveau label {
    display: flex;
    flex: 1 1 240px;
    flex-direction: column;
    gap: 4px;
    font-weight: 600;
  }

  .liste {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .liste li {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
    padding: 8px 0;
    border-bottom: 1px solid var(--bordure);
  }

  .texte {
    display: flex;
    flex: 1 1 260px;
    flex-direction: column;
  }

  .erreur {
    color: var(--retard);
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    margin-top: 14px;
  }
</style>
