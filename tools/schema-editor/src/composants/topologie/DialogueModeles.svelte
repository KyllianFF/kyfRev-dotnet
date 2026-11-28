<script lang="ts">
  import Ellipsis from "@lucide/svelte/icons/ellipsis";
  import { tick } from "svelte";
  import { api, messageErreur } from "../../lib/api";
  import { type Ancre, placerMenu } from "../../lib/placement";
  import { apercuSchema } from "../../lib/topologie/apercu";
  import { ecrireSchema, lireSchema } from "../../lib/topologie/format";
  import { type ModeleDeSchema, modelesIntegres, schemaDepuisModele } from "../../lib/topologie/modelesDeSchema";

  // « Nouveau schéma depuis un modèle » (31E) : les modèles de kyfRev, puis ceux de l'utilisateur, chacun en vignette
  // dessinée comme les aperçus de la vue Schémas. Le nom proposé est celui du modèle ; « Créer » fait un nouveau schéma,
  // copie du modèle aux identifiants refaits. Un modèle de l'utilisateur se retire au clic droit sur sa vignette, ou
  // par « ⋯ », après une question.
  let { ouvert, onFermer, onCree }: { ouvert: boolean; onFermer: () => void; onCree: (id: string) => void } = $props();

  let integres = $state.raw<ModeleDeSchema[]>([]);
  let miens = $state.raw<ModeleDeSchema[] | null>(null);
  let choisi = $state<string>("");
  let nom = $state("");
  /** Le nom a été tapé : changer de modèle ne le remplace plus. */
  let nomTape = false;
  let occupe = $state(false);
  let erreur = $state<string | null>(null);
  let menu = $state<{ modele: ModeleDeSchema; ancre: Ancre; origine: HTMLElement | null } | null>(null);
  let aRetirer = $state<ModeleDeSchema | null>(null);
  let dialogue = $state<HTMLDialogElement>();

  const montrer = (d: HTMLDialogElement) => d.showModal();
  const tous = $derived([...integres, ...(miens ?? [])]);
  const modeleChoisi = $derived(tous.find((m) => m.id === choisi));

  async function chargerMiens() {
    try {
      const lus = await api.modelesSchema();
      miens = lus.flatMap((lu) => {
        try {
          const schema = lireSchema(lu.texte).schema;
          return [{ id: lu.id, nom: schema.titre, description: schema.description ?? "", schema, integre: false }];
        } catch {
          // Un modèle qui ne se relit pas n'est pas proposé.
          return [];
        }
      });
    } catch (e) {
      miens = [];
      erreur = `Vos modèles ne se lisent pas : ${messageErreur(e)}`;
    }
  }

  function preparer(dialog: HTMLDialogElement) {
    integres = modelesIntegres();
    miens = null;
    erreur = null;
    nomTape = false;
    choisir(integres[0]?.id ?? "");
    dialog.showModal();
    void chargerMiens();
  }

  function choisir(id: string) {
    choisi = id;
    const m = tous.find((x) => x.id === id) ?? integres.find((x) => x.id === id);
    if (m && !nomTape) nom = m.nom;
  }

  /** Les flèches passent d'une vignette à l'autre ; la vignette qui a la main est celle choisie. */
  function clavier(e: KeyboardEvent, rang: number) {
    const colonnes = Math.max(1, Math.round((e.currentTarget as HTMLElement).parentElement!.clientWidth / (e.currentTarget as HTMLElement).offsetWidth));
    const pas = { ArrowRight: 1, ArrowLeft: -1, ArrowDown: colonnes, ArrowUp: -colonnes, Home: -rang, End: tous.length - 1 - rang }[e.key];
    if (pas === undefined) return;
    e.preventDefault();
    const suivant = Math.max(0, Math.min(tous.length - 1, rang + pas));
    const cible = tous[suivant];
    if (!cible) return;
    choisir(cible.id);
    void tick().then(() => dialogue?.querySelector<HTMLElement>(`[data-modele="${cible.id}"]`)?.focus());
  }

  async function creer(e: SubmitEvent) {
    e.preventDefault();
    const m = modeleChoisi;
    if (!m || occupe || !nom.trim()) return;
    occupe = true;
    erreur = null;
    try {
      const cree = await api.creerSchema(ecrireSchema(schemaDepuisModele(m.schema, nom)));
      dialogue?.close();
      onCree(cree.id);
    } catch (err) {
      erreur = `Création impossible : ${messageErreur(err)}`;
    } finally {
      occupe = false;
    }
  }

  function ouvrirMenu(e: MouseEvent, m: ModeleDeSchema) {
    if (m.integre) return;
    e.preventDefault();
    const origine = e.currentTarget as HTMLElement;
    const cadre = origine.getBoundingClientRect();
    const auClavier = e.clientX === 0 && e.clientY === 0;
    menu = { modele: m, ancre: auClavier ? { x: cadre.right - 8, y: cadre.top + 8 } : { x: e.clientX, y: e.clientY }, origine };
  }

  function fermerMenu(rendre = true) {
    const origine = menu?.origine;
    menu = null;
    if (rendre && origine?.isConnected) origine.focus();
  }

  function placer(liste: HTMLElement, ancre: Ancre) {
    const cadre = liste.getBoundingClientRect();
    const place = placerMenu(ancre, { largeur: cadre.width, hauteur: cadre.height }, { largeur: innerWidth, hauteur: innerHeight });
    liste.style.left = `${place.x}px`;
    liste.style.top = `${place.y}px`;
    liste.querySelector<HTMLButtonElement>("button")?.focus();
  }

  async function retirer() {
    const m = aRetirer;
    aRetirer = null;
    if (!m) return;
    occupe = true;
    erreur = null;
    try {
      await api.retirerModeleSchema(m.id);
      if (choisi === m.id) choisir(integres[0]?.id ?? "");
      await chargerMiens();
    } catch (err) {
      erreur = `Le modèle n'a pas été retiré : ${messageErreur(err)}`;
    } finally {
      occupe = false;
    }
  }
</script>

{#snippet vignette(m: ModeleDeSchema, rang: number)}
  <div class="modele" class:choisi={m.id === choisi}>
    <button
      type="button"
      class="carte"
      role="radio"
      aria-checked={m.id === choisi}
      tabindex={m.id === choisi ? 0 : -1}
      data-modele={m.id}
      onclick={() => choisir(m.id)}
      ondblclick={() => dialogue?.querySelector<HTMLFormElement>("form")?.requestSubmit()}
      onkeydown={(e) => clavier(e, rang)}
      oncontextmenu={(e) => ouvrirMenu(e, m)}
    >
      <span class="vignette" aria-hidden="true">
        {#if m.schema.equipements.length > 0}{@html apercuSchema(m.schema)}{:else}<span class="vide">Modèle vide</span>{/if}
      </span>
      <span class="infos">
        <span class="nom">{m.nom}</span>
        {#if m.description}<span class="description">{m.description}</span>{/if}
      </span>
    </button>
    {#if !m.integre}
      <button
        type="button"
        class="bouton discret actions"
        aria-label="Actions sur le modèle « {m.nom} »"
        aria-haspopup="menu"
        aria-expanded={menu?.modele.id === m.id}
        title="Actions sur ce modèle"
        onclick={(e) => ouvrirMenu(e, m)}
      ><Ellipsis size={16} aria-hidden="true" /></button>
    {/if}
  </div>
{/snippet}

{#if ouvert}
  <dialog class="dialogue-modeles" aria-labelledby="titre-modeles" bind:this={dialogue} use:preparer onclose={onFermer}>
    <form onsubmit={creer}>
      <h2 id="titre-modeles">Nouveau schéma depuis un modèle</h2>
      <div class="defile">
        <div class="grille" role="radiogroup" aria-label="Modèles">
          {#each integres as m, rang (m.id)}{@render vignette(m, rang)}{/each}
        </div>
        <h3 id="titre-mes-modeles">Mes modèles</h3>
        {#if miens === null}
          <p class="aide" aria-busy="true">Lecture de vos modèles…</p>
        {:else if miens.length === 0}
          <p class="aide">
            Aucun pour l'instant. « Enregistrer comme modèle… » sur un schéma, ou au clic droit sur l'onglet d'une page,
            l'ajoute ici.
          </p>
        {:else}
          <div class="grille" role="radiogroup" aria-labelledby="titre-mes-modeles">
            {#each miens as m, rang (m.id)}{@render vignette(m, integres.length + rang)}{/each}
          </div>
        {/if}
      </div>
      <label class="champ-nom">
        Nom du nouveau schéma
        <input class="champ" bind:value={nom} oninput={() => (nomTape = true)} maxlength="200" required />
      </label>
      {#if erreur}<p class="erreur" role="alert">{erreur}</p>{/if}
      <div class="actions">
        <button type="button" class="bouton" onclick={() => dialogue?.close()}>Annuler</button>
        <button type="submit" class="bouton principal" disabled={occupe || !modeleChoisi || !nom.trim()}>Créer</button>
      </div>
    </form>

    {#if menu}
      {@const m = menu.modele}
      <!-- svelte-ignore a11y_no_static_element_interactions, a11y_click_events_have_key_events -->
      <div class="voile-menu" onpointerdown={() => fermerMenu()} oncontextmenu={(e) => { e.preventDefault(); fermerMenu(); }}></div>
      <div
        class="menu"
        role="menu"
        tabindex="-1"
        aria-label="Modèle « {m.nom} »"
        use:placer={menu.ancre}
        onkeydown={(e) => {
          if (e.key === "Escape" || e.key === "Tab") {
            e.preventDefault();
            e.stopPropagation();
            fermerMenu();
          }
        }}
      >
        <button
          type="button"
          role="menuitem"
          class="danger"
          onclick={() => {
            // Le modèle se lit avant de fermer le menu : fermé, il n'en a plus.
            const cible = menu?.modele ?? null;
            fermerMenu(false);
            aRetirer = cible;
          }}>Supprimer le modèle…</button>
      </div>
    {/if}

    {#if aRetirer}
      {@const m = aRetirer}
      <dialog class="confirmation" aria-labelledby="titre-retrait-modele" use:montrer onclose={() => (aRetirer = null)}>
        <h2 id="titre-retrait-modele">Supprimer le modèle « {m.nom} » ?</h2>
        <p>Il ne sera plus proposé. Les schémas déjà créés depuis lui ne changent pas.</p>
        <div class="actions">
          <!-- svelte-ignore a11y_autofocus -->
          <button type="button" class="bouton" autofocus onclick={() => (aRetirer = null)}>Annuler</button>
          <button type="button" class="bouton principal" onclick={() => void retirer()}>Supprimer</button>
        </div>
      </dialog>
    {/if}
  </dialog>
{/if}

<style>
  .dialogue-modeles {
    width: min(760px, calc(100vw - 32px));
    max-height: calc(100vh - 32px);
    padding: 0;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 16px 48px rgb(0 0 0 / 0.2);
  }

  .dialogue-modeles::backdrop,
  .confirmation::backdrop {
    background: var(--voile);
  }

  form {
    display: flex;
    flex-direction: column;
    gap: 12px;
    max-height: calc(100vh - 34px);
    padding: 20px 22px;
  }

  h2 {
    font-size: 18px;
  }

  h3 {
    margin: 14px 0 8px;
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .defile {
    min-height: 0;
    margin: 0 -6px;
    padding: 2px 6px 4px;
    overflow-y: auto;
  }

  .grille {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(190px, 1fr));
    gap: 10px;
  }

  .modele {
    position: relative;
    min-width: 0;
  }

  .carte {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    padding: 0;
    overflow: hidden;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--surface);
    color: var(--texte);
    text-align: left;
  }

  .carte:hover {
    border-color: var(--texte-secondaire);
  }

  .choisi .carte {
    border-color: var(--principale);
    box-shadow: 0 0 0 1px var(--principale);
  }

  .vignette {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 112px;
    padding: 8px;
    border-bottom: 1px solid var(--bordure);
    background: var(--fond);
  }

  /* Le dessin occupe la vignette, à ses proportions. */
  .vignette > :global(svg) {
    width: 100%;
    height: 100%;
  }

  .vide {
    color: var(--texte-secondaire);
    font-size: 12.5px;
  }

  .infos {
    display: flex;
    flex-direction: column;
    gap: 2px;
    padding: 8px 10px 10px;
  }

  .nom {
    font-weight: 600;
    overflow-wrap: anywhere;
  }

  .description {
    color: var(--texte-secondaire);
    font-size: 12.5px;
    overflow-wrap: anywhere;
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
  }

  .modele > .actions {
    position: absolute;
    top: 6px;
    right: 6px;
    width: 30px;
    height: 30px;
    padding: 0;
    justify-content: center;
    background: var(--surface);
  }

  .aide {
    margin: 0;
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .champ-nom {
    display: grid;
    gap: 4px;
    font-weight: 600;
  }

  .champ-nom .champ {
    font-weight: 400;
  }

  .erreur {
    margin: 0;
    color: var(--retard);
    font-size: 13px;
  }

  .voile-menu {
    position: fixed;
    inset: 0;
    z-index: 10;
  }

  .menu {
    position: fixed;
    z-index: 11;
    display: flex;
    flex-direction: column;
    min-width: 200px;
    padding: 6px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--surface);
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.18);
  }

  .menu button {
    padding: 7px 10px;
    border: none;
    border-radius: 6px;
    background: transparent;
    color: var(--texte);
    text-align: left;
  }

  .menu button:hover,
  .menu button:focus-visible {
    background: var(--surface-2);
  }

  .menu .danger {
    color: var(--retard);
  }

  .confirmation {
    width: min(440px, calc(100vw - 32px));
    padding: 22px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 16px 48px rgb(0 0 0 / 0.2);
  }

  .confirmation p {
    margin: 10px 0 0;
  }

  .confirmation .actions {
    margin-top: 16px;
  }

  @media (max-width: 520px) {
    form {
      padding: 16px;
    }

    .grille {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    .vignette {
      height: 92px;
    }
  }
</style>
