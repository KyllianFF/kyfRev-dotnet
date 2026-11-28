<script lang="ts">
  import { tick, untrack } from "svelte";
  import { api, messageErreur } from "../../lib/api";
  import type { SchemaResume } from "../../lib/generes/SchemaResume";
  import { apercuSchema } from "../../lib/topologie/apercu";
  import { ecrireEchange, lireEchange, type SchemaEchange } from "../../lib/topologie/echanges";
  import type { Schema } from "../../lib/topologie/modele";
  import { equipementsParPage, pagesDe } from "../../lib/topologie/pages";
  import { compteRendu, copierPages, retirerPages, type Transfert } from "../../lib/topologie/transfert";

  // « Ajouter des pages d'un autre schéma » (31D) : on choisit le schéma qui reçoit (s'il n'est pas donné), le schéma
  // d'où elles viennent (s'il n'est pas donné), puis ses pages sur leurs vignettes. « Les copier » ne touche pas au
  // schéma d'origine ; « Les déplacer » les en retire. Un câble ne va pas d'un schéma à l'autre : entre une page prise
  // et une page laissée, il ne suit pas, et le compte rendu le dit. Dans l'éditeur ouvert, l'ajout passe par l'éditeur :
  // Ctrl+Z l'annule en une fois.
  let {
    cible,
    sources,
    ouvert,
    onFermer,
    onFait,
  }: { cible: string | null; sources: string[]; ouvert: boolean; onFermer: () => void; onFait: (cibleId: string) => void } = $props();

  type Groupe = { lu: SchemaEchange; cochees: string[] };

  let dialogue = $state<HTMLDialogElement>();
  let etape = $state<"cible" | "pages" | "fait">("pages");
  let liste = $state<SchemaResume[] | null>(null);
  let filtre = $state("");
  let cibleId = $state<string | null>(null);
  /** Le schéma source choisi dans la liste, quand aucun n'est donné. */
  let sourceChoisie = $state("");
  let groupes = $state<Groupe[]>([]);
  let deplacer = $state(false);
  let chargement = $state(false);
  let enCours = $state(false);
  let erreur = $state<string | null>(null);
  let phrases = $state<string[]>([]);

  $effect(() => {
    if (ouvert) untrack(() => void ouvrir());
    else untrack(() => dialogue?.open && dialogue.close());
  });

  const titreDe = (id: string | null) => liste?.find((s) => s.id === id)?.titre ?? "";
  const pluriel = (n: number, un: string, plusieurs: string) => `${n} ${n > 1 ? plusieurs : un}`;

  async function ouvrir() {
    erreur = null;
    enCours = false;
    deplacer = false;
    groupes = [];
    phrases = [];
    filtre = "";
    sourceChoisie = "";
    cibleId = cible;
    etape = cible === null ? "cible" : "pages";
    await tick();
    if (dialogue && !dialogue.open) dialogue.showModal();
    liste = null;
    try {
      liste = await api.schemas();
    } catch (e) {
      erreur = `La liste des schémas est illisible : ${messageErreur(e)}`;
      liste = [];
    }
    if (etape === "pages") await preparerPages();
  }

  /** Les schémas d'où les pages peuvent venir : ceux qui sont donnés, sinon tous, sauf celui qui les reçoit. */
  const sourcesPossibles = $derived((liste ?? []).filter((s) => s.id !== cibleId && (sources.length === 0 || sources.includes(s.id))));
  const ciblesPossibles = $derived(
    (liste ?? []).filter(
      (s) => !sources.includes(s.id) && (!filtre.trim() || s.titre.toLocaleLowerCase("fr").includes(filtre.trim().toLocaleLowerCase("fr"))),
    ),
  );

  async function preparerPages() {
    etape = "pages";
    if (sources.length > 0) await charger(sources.filter((id) => id !== cibleId));
    else {
      sourceChoisie = sourcesPossibles[0]?.id ?? "";
      if (sourceChoisie) await charger([sourceChoisie]);
    }
  }

  /** Lit les schémas sources ; toutes leurs pages sont cochées. */
  async function charger(ids: string[]) {
    chargement = true;
    erreur = null;
    try {
      const lus = await Promise.all(ids.map((id) => lireEchange(id)));
      groupes = lus.map((lu) => ({ lu, cochees: pagesDe(lu.schema).map((p) => p.id) }));
    } catch (e) {
      erreur = `Lecture impossible : ${messageErreur(e)}`;
      groupes = [];
    } finally {
      chargement = false;
    }
  }

  const nombre = $derived(groupes.reduce((n, g) => n + g.cochees.length, 0));
  const nomSources = $derived(groupes.length === 1 ? `« ${groupes[0]?.lu.schema.titre ?? ""} »` : "leurs schémas d'origine");

  /** Vignette recadrée sur le dessin, comme sur les cartes de la liste. */
  function recadrer(cadre: HTMLElement, _cle: string) {
    const appliquer = () => {
      const svg = cadre.querySelector("svg");
      if (!svg) return;
      const boite = svg.getBBox();
      if (boite.width <= 0 || boite.height <= 0) return;
      const marge = 12;
      svg.setAttribute("viewBox", `${boite.x - marge} ${boite.y - marge} ${boite.width + 2 * marge} ${boite.height + 2 * marge}`);
      svg.removeAttribute("width");
      svg.removeAttribute("height");
    };
    appliquer();
    return { update: appliquer };
  }

  const vignette = (schema: Schema, page: string) => apercuSchema(schema, undefined, false, pagesDe(schema).length > 1 ? page : undefined);

  // --- Ajouter ---

  async function executer(evenement: SubmitEvent) {
    evenement.preventDefault();
    if (!cibleId || nombre === 0 || enCours) return;
    enCours = true;
    erreur = null;
    const envois = groupes.filter((g) => g.cochees.length > 0);
    let recue: SchemaEchange;
    try {
      recue = await lireEchange(cibleId);
    } catch (e) {
      erreur = `Lecture impossible : ${messageErreur(e)}`;
      enCours = false;
      return;
    }
    const titreCible = recue.schema.titre;
    const transferts: { g: Groupe; t: Transfert }[] = [];
    try {
      if (recue.editeur) {
        // Le schéma qui reçoit est ouvert : l'ajout passe par l'éditeur, et Ctrl+Z l'annule.
        for (const g of envois) {
          const t = recue.editeur.recevoirPages(g.lu.schema, g.cochees);
          if (!t) throw new Error(recue.editeur.annonce ?? "ajout refusé");
          transferts.push({ g, t });
        }
      } else {
        const schema = JSON.parse(JSON.stringify(recue.schema)) as Schema;
        for (const g of envois) transferts.push({ g, t: copierPages(g.lu.schema, g.cochees, schema) });
        await ecrireEchange(recue, schema);
      }
    } catch (e) {
      // Rien n'est resté à moitié : l'éditeur revient où il en était.
      transferts.forEach(() => recue.editeur?.annuler());
      erreur = `Ajout impossible : ${messageErreur(e)}. Rien n'a été modifié.`;
      enCours = false;
      return;
    }
    const restes: string[] = [];
    if (deplacer) {
      for (const { g } of transferts) {
        try {
          if (g.lu.editeur) {
            g.lu.editeur.retirerPages(g.cochees, `${pluriel(g.cochees.length, "page déplacée", "pages déplacées")} vers « ${titreCible} » : Ctrl+Z ${g.cochees.length > 1 ? "les rend" : "la rend"} ici.`);
          } else {
            const schema = JSON.parse(JSON.stringify(g.lu.schema)) as Schema;
            retirerPages(schema, g.cochees);
            await ecrireEchange(g.lu, schema);
          }
        } catch (e) {
          restes.push(`${g.lu.schema.titre} (${messageErreur(e)})`);
        }
      }
    }
    phrases = transferts.flatMap(({ g, t }) => compteRendu(t, { source: g.lu.schema.titre, cible: titreCible, deplacer }));
    if (restes.length > 0) {
      phrases.push(`Les pages n'ont pas pu être retirées de ${restes.map((r) => `« ${r} »`).join(", ")} : elles y sont toujours.`);
    }
    if (recue.editeur) recue.editeur.annonce = `${phrases[0] ?? ""} Ctrl+Z pour annuler.`;
    enCours = false;
    etape = "fait";
    await tick();
    dialogue?.querySelector<HTMLButtonElement>(".fermer-compte-rendu")?.focus();
  }

  function finir() {
    const id = cibleId;
    // Oublié avant de fermer : la fermeture ne le redonne pas une seconde fois.
    cibleId = null;
    if (id) onFait(id);
    dialogue?.close();
  }
</script>

{#if ouvert}
  <dialog
    bind:this={dialogue}
    class="dialogue-ajouter-pages"
    aria-labelledby="titre-ajouter-pages"
    onclose={() => {
      if (etape === "fait" && cibleId) {
        // Fermée par Échap après coup : ce qui est fait est fait, la liste doit le savoir.
        const id = cibleId;
        cibleId = null;
        onFait(id);
      }
      if (ouvert) onFermer();
    }}
  >
    {#if etape === "cible"}
      <form
        onsubmit={(e) => {
          e.preventDefault();
          if (cibleId) void preparerPages();
        }}
      >
        <h2 id="titre-ajouter-pages">Ajouter à quel schéma ?</h2>
        <p class="aide-tete">
          {sources.length === 1 ? `Les pages de « ${titreDe(sources[0] ?? null)} » iront dans le schéma choisi.` : "Les pages choisies iront dans ce schéma, après les siennes."}
        </p>
        {#if (liste?.length ?? 0) > 6}
          <input class="champ" type="search" placeholder="Chercher un schéma" aria-label="Chercher un schéma" bind:value={filtre} />
        {/if}
        {#if liste === null}
          <p class="aide-tete">Lecture des schémas…</p>
        {:else if ciblesPossibles.length === 0}
          <p class="aide-tete">Aucun autre schéma ne peut les recevoir.</p>
        {:else}
          <div class="liste-schemas" role="radiogroup" aria-label="Schéma qui reçoit">
            {#each ciblesPossibles as s (s.id)}
              <label class="case-schema">
                <input type="radio" name="cible-pages" value={s.id} bind:group={cibleId} />
                <span class="nom">{s.titre}</span>
                <span class="compte">{pluriel(s.equipements, "équipement", "équipements")}</span>
              </label>
            {/each}
          </div>
        {/if}
        {#if erreur}<p class="erreur" role="alert">{erreur}</p>{/if}
        <div class="actions">
          <button type="button" class="bouton" onclick={() => dialogue?.close()}>Annuler</button>
          <button type="submit" class="bouton principal" disabled={!cibleId}>Continuer</button>
        </div>
      </form>
    {:else if etape === "pages"}
      <form onsubmit={executer}>
        <h2 id="titre-ajouter-pages">
          {cible === null ? `Ajouter à « ${titreDe(cibleId)} »` : "Ajouter des pages d'un autre schéma"}
        </h2>
        {#if sources.length === 0}
          <select
            class="champ source"
            aria-label="Schéma d'où viennent les pages"
            bind:value={sourceChoisie}
            disabled={enCours || sourcesPossibles.length === 0}
            onchange={() => void charger([sourceChoisie])}
          >
            {#each sourcesPossibles as s (s.id)}
              <option value={s.id}>{s.titre}</option>
            {/each}
          </select>
          {#if liste !== null && sourcesPossibles.length === 0}
            <p class="aide-tete">Il n'y a pas d'autre schéma d'où prendre des pages.</p>
          {/if}
        {/if}
        {#if chargement || liste === null}
          <p class="aide-tete">Lecture du schéma…</p>
        {:else}
          {#each groupes as g (g.lu.id)}
            {@const pages = pagesDe(g.lu.schema)}
            {@const compte = equipementsParPage(g.lu.schema)}
            <fieldset class="pages">
              <legend class={sources.length === 0 ? "visuellement-cache" : "titre-source"}>Pages de « {g.lu.schema.titre} »</legend>
              <div class="vignettes">
                {#each pages as p (p.id)}
                  <label class="vignette">
                    <span class="dessin" aria-hidden="true" use:recadrer={`${g.lu.id}/${p.id}`}>
                      {@html vignette(g.lu.schema, p.id)}
                    </span>
                    <span class="legende">
                      <input type="checkbox" value={p.id} bind:group={g.cochees} />
                      <span class="nom-page">{p.nom}</span>
                      <span class="compte">{compte.get(p.id) ?? 0}</span>
                    </span>
                  </label>
                {/each}
              </div>
            </fieldset>
          {/each}

          {#if groupes.length > 0}
            <fieldset class="mode">
              <legend class="visuellement-cache">Copier ou déplacer</legend>
              <label class="choix">
                <input type="radio" name="mode-pages" value={false} bind:group={deplacer} />
                <span class="nom">Les copier</span>
                <span class="aide">{groupes.length === 1 ? `${nomSources} ne change pas.` : "Les schémas d'origine ne changent pas."}</span>
              </label>
              <label class="choix">
                <input type="radio" name="mode-pages" value={true} bind:group={deplacer} />
                <span class="nom">Les déplacer</span>
                <span class="aide">Elles quittent {nomSources}. Un câble vers une page qui y reste ne peut pas aller d'un schéma à l'autre : il est retiré, et le compte rendu le dit.</span>
              </label>
            </fieldset>
          {/if}
        {/if}
        {#if erreur}<p class="erreur" role="alert">{erreur}</p>{/if}
        <div class="actions">
          {#if cible === null}
            <button type="button" class="bouton discret retour" disabled={enCours} onclick={() => { etape = "cible"; groupes = []; }}>Changer de schéma</button>
          {/if}
          <button type="button" class="bouton" onclick={() => dialogue?.close()}>Annuler</button>
          <button type="submit" class="bouton principal" disabled={nombre === 0 || enCours || chargement}>
            {enCours ? "Ajout…" : nombre > 0 ? `Ajouter ${pluriel(nombre, "page", "pages")}` : "Ajouter"}
          </button>
        </div>
      </form>
    {:else}
      <div class="fait">
        <h2 id="titre-ajouter-pages">{deplacer ? "Pages déplacées" : "Pages ajoutées"}</h2>
        <ul class="compte-rendu" role="status">
          {#each phrases as phrase (phrase)}
            <li>{phrase}</li>
          {/each}
        </ul>
        <div class="actions">
          <button type="button" class="bouton principal fermer-compte-rendu" onclick={finir}>Fermer</button>
        </div>
      </div>
    {/if}
  </dialog>
{/if}

<style>
  dialog {
    /* La largeur de la fenêtre moins 16 pixels de chaque côté, au plus 600. */
    width: min(600px, calc(100vw - 32px));
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

  form,
  .fait {
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
    max-height: min(360px, 50vh);
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

  .case-schema input,
  .legende input {
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

  fieldset {
    display: grid;
    gap: 6px;
    min-width: 0;
    margin: 0;
    padding: 0;
    border: none;
  }

  .titre-source {
    margin-bottom: 4px;
    padding: 0;
    color: var(--texte-secondaire);
    font-size: 12.5px;
    font-weight: 600;
  }

  /* Les pages, sur leurs vignettes : trois par ligne, deux dans une fenêtre étroite. */
  .vignettes {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
    gap: 8px;
  }

  .vignette {
    display: flex;
    flex-direction: column;
    min-width: 0;
    overflow: hidden;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--fond);
    cursor: pointer;
  }

  .vignette:hover {
    border-color: var(--texte-secondaire);
  }

  .vignette:has(input:checked) {
    border-color: var(--principale);
    box-shadow: 0 0 0 1px var(--principale);
  }

  .vignette:has(input:focus-visible) {
    outline: 2px solid var(--focus);
    outline-offset: 1px;
  }

  .dessin {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 76px;
    padding: 6px;
    border-bottom: 1px solid var(--bordure);
    overflow: hidden;
  }

  .dessin :global(svg) {
    width: 100%;
    height: 100%;
  }

  .legende {
    display: flex;
    align-items: center;
    gap: 6px;
    min-height: 32px;
    padding: 4px 8px;
    background: var(--surface);
  }

  .nom-page {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .choix {
    display: grid;
    grid-template-columns: auto minmax(0, 1fr);
    align-items: center;
    gap: 2px 10px;
    min-height: 48px;
    padding: 8px 12px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    cursor: pointer;
  }

  .choix:hover {
    background: var(--surface-2);
  }

  .choix:has(input:checked) {
    border-color: var(--principale);
    background: color-mix(in srgb, var(--principale) 7%, var(--surface));
  }

  .choix input {
    grid-row: span 2;
    width: 16px;
    height: 16px;
    margin: 0;
    accent-color: var(--principale);
  }

  .aide {
    grid-column: 2;
    color: var(--texte-secondaire);
    font-size: 12.5px;
  }

  .compte-rendu {
    display: flex;
    flex-direction: column;
    gap: 6px;
    margin: 0;
    padding-left: 20px;
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

  .actions .retour {
    margin-right: auto;
  }
</style>
