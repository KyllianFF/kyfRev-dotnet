<script lang="ts">
  import Plus from "@lucide/svelte/icons/plus";
  import X from "@lucide/svelte/icons/x";
  import { onMount, untrack } from "svelte";
  import { ETIQUETTE_MAX, etiquetteNette, sansAccents } from "../../lib/topologie/accueil";
  import { ETIQUETTES_MAX } from "../../lib/topologie/format";

  // « Étiqueter… » (31E) : les étiquettes d'un schéma, ou de plusieurs choisis, posées et retirées à la main. Celles
  // déjà en usage dans le coffre se proposent en tapant. Rien ne s'écrit avant « Enregistrer ».
  let {
    titre,
    nombre,
    presentes,
    connues,
    onvalider,
    onfermer,
  }: {
    /** Le titre du schéma, s'il est seul. */
    titre: string;
    /** Les schémas à étiqueter. */
    nombre: number;
    /** Les étiquettes qu'ils portent déjà, avec le nombre de schémas qui portent chacune. */
    presentes: { nom: string; nombre: number }[];
    /** Les étiquettes en usage dans le coffre, à proposer. */
    connues: string[];
    onvalider: (ajouts: string[], retraits: string[]) => void;
    onfermer: () => void;
  } = $props();

  let dialogue: HTMLDialogElement;
  let champ: HTMLInputElement;
  let saisie = $state("");
  /** L'état voulu de chaque étiquette : combien de schémas la porteront (`nombre` : tous). */
  let voulues = $state(untrack(() => presentes.map((p) => ({ ...p }))));
  let valide = false;

  onMount(() => dialogue.showModal());

  const cle = (nom: string) => nom.toLocaleLowerCase("fr-FR");
  const suggestions = $derived.by(() => {
    const cherche = sansAccents(saisie.trim());
    return connues
      .filter((nom) => !voulues.some((v) => cle(v.nom) === cle(nom)))
      .filter((nom) => !cherche || sansAccents(nom).includes(cherche))
      .slice(0, 8);
  });
  const pleine = $derived(nombre === 1 && voulues.length >= ETIQUETTES_MAX);

  /** Pose une étiquette sur tous les schémas : celle tapée, ou une proposée. */
  function poser(texte: string) {
    const nom = etiquetteNette(texte);
    if (!nom || pleine) return;
    const deja = voulues.find((v) => cle(v.nom) === cle(nom));
    if (deja) deja.nombre = nombre;
    else voulues.push({ nom: connues.find((c) => cle(c) === cle(nom)) ?? nom, nombre });
    saisie = "";
    champ?.focus();
  }

  function retirer(nom: string) {
    voulues = voulues.filter((v) => v.nom !== nom);
    champ?.focus();
  }

  function valider(evenement: SubmitEvent) {
    evenement.preventDefault();
    // Une saisie en attente compte : « Enregistrer » la pose avant de fermer.
    if (saisie.trim()) poser(saisie);
    valide = true;
    dialogue.close();
  }

  function fermer() {
    if (valide) {
      const ajouts = voulues
        .filter((v) => v.nombre === nombre && (presentes.find((p) => cle(p.nom) === cle(v.nom))?.nombre ?? 0) < nombre)
        .map((v) => v.nom);
      const retraits = presentes.filter((p) => !voulues.some((v) => cle(v.nom) === cle(p.nom))).map((p) => p.nom);
      if (ajouts.length > 0 || retraits.length > 0) onvalider(ajouts, retraits);
    }
    onfermer();
  }
</script>

<dialog bind:this={dialogue} aria-labelledby="titre-etiqueter" onclose={fermer}>
  <form onsubmit={valider}>
    <h2 id="titre-etiqueter">{nombre > 1 ? `Étiqueter ${nombre} schémas` : `Étiqueter « ${titre} »`}</h2>
    <p class="aide">Les étiquettes rangent les schémas : elles servent de filtres sur l'accueil.</p>
    {#if voulues.length > 0}
      <ul class="posees" aria-label="Étiquettes posées">
        {#each voulues as v (v.nom)}
          <li class="puce" class:partielle={v.nombre < nombre}>
            <span>{v.nom}</span>
            {#if v.nombre < nombre}
              <span class="part">{v.nombre} sur {nombre}</span>
              <button type="button" class="mini" aria-label={`Poser « ${v.nom} » sur les ${nombre} schémas`} title="Sur tous" onclick={() => poser(v.nom)}>
                <Plus size={14} aria-hidden="true" />
              </button>
            {/if}
            <button type="button" class="mini" aria-label={`Retirer « ${v.nom} »`} title="Retirer" onclick={() => retirer(v.nom)}>
              <X size={14} aria-hidden="true" />
            </button>
          </li>
        {/each}
      </ul>
    {:else}
      <p class="aide">Aucune étiquette pour l'instant.</p>
    {/if}
    <label class="saisie">
      Nouvelle étiquette
      <!-- svelte-ignore a11y_autofocus -->
      <input
        bind:this={champ}
        class="champ"
        bind:value={saisie}
        maxlength={ETIQUETTE_MAX}
        placeholder="Lycée, Client, TP…"
        autocomplete="off"
        autofocus
        disabled={pleine}
        aria-describedby={pleine ? "etiquettes-pleines" : undefined}
        onkeydown={(e) => {
          if (e.key === "Enter" && saisie.trim()) {
            e.preventDefault();
            poser(saisie);
          }
        }}
      />
    </label>
    {#if pleine}<p id="etiquettes-pleines" class="aide">{ETIQUETTES_MAX} étiquettes au plus par schéma.</p>{/if}
    {#if suggestions.length > 0}
      <div class="suggestions" role="group" aria-label="Étiquettes déjà en usage">
        {#each suggestions as nom (nom)}
          <button type="button" class="puce proposee" disabled={pleine} onclick={() => poser(nom)}>
            <Plus size={12} aria-hidden="true" />{nom}
          </button>
        {/each}
      </div>
    {/if}
    <div class="actions">
      <button type="button" class="bouton" onclick={() => dialogue.close()}>Annuler</button>
      <button type="submit" class="bouton principal">Enregistrer</button>
    </div>
  </form>
</dialog>

<style>
  dialog {
    width: min(480px, calc(100vw - 32px));
    max-height: calc(100vh - 32px);
    padding: 24px;
    overflow: auto;
    box-shadow: 0 16px 48px rgb(0 0 0 / 0.2);
  }

  h2 {
    font-size: 18px;
    overflow-wrap: anywhere;
  }

  .aide {
    margin: 6px 0 12px;
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .posees,
  .suggestions {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    margin: 0 0 14px;
    padding: 0;
    list-style: none;
  }

  .suggestions {
    margin: 10px 0 0;
  }

  .puce {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    max-width: 100%;
    min-height: 30px;
    padding: 0 4px 0 12px;
    border: 1px solid var(--bordure);
    border-radius: 999px;
    background: var(--surface-2);
    font-size: 13px;
    overflow-wrap: anywhere;
  }

  .puce.partielle {
    background: var(--surface);
    border-style: dashed;
  }

  .part {
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .proposee {
    padding: 0 12px 0 8px;
    background: var(--surface);
    color: var(--texte);
  }

  .proposee:hover:not(:disabled) {
    background: var(--surface-2);
  }

  .mini {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 24px;
    height: 24px;
    padding: 0;
    border: none;
    border-radius: 999px;
    background: none;
    color: var(--texte-secondaire);
  }

  .mini:hover {
    background: var(--surface);
    color: var(--texte);
  }

  .saisie {
    display: grid;
    gap: 4px;
    font-size: 13px;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 20px;
  }
</style>
