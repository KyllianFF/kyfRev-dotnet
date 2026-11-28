<script lang="ts">
  import { tick, untrack } from "svelte";
  import { api, messageErreur } from "../../lib/api";
  import type { SchemaResume } from "../../lib/generes/SchemaResume";
  import { creerEchange, lireEchange, type SchemaEchange } from "../../lib/topologie/echanges";
  import { pagesDe } from "../../lib/topologie/pages";
  import { doublonsEntre, reunir, titrePropose } from "../../lib/topologie/reunion";

  // « Réunir des schémas » (31C) : un nouveau schéma fait des pages des schémas choisis, dans l'ordre choisi (à glisser,
  // ou Alt+flèches). Les originaux ne changent pas ; par défaut, ils sont archivés, et les fiches qui les insèrent les
  // montrent toujours. Sans schémas donnés (ou un seul), la fenêtre les fait d'abord choisir.
  let { ids, ouvert, onFermer, onFait }: { ids: string[]; ouvert: boolean; onFermer: () => void; onFait: (nouvelId: string) => void } = $props();

  let dialogue = $state<HTMLDialogElement>();
  let etape = $state<"choix" | "reglages">("reglages");
  /** Les schémas du coffre, pour le choix ; `null` tant qu'ils ne sont pas lus. */
  let liste = $state<SchemaResume[] | null>(null);
  let coches = $state<string[]>([]);
  let filtre = $state("");
  /** Les schémas lus, dans l'ordre de leurs pages dans le nouveau. */
  let lus = $state.raw<SchemaEchange[]>([]);
  let chargement = $state(false);
  let erreur = $state<string | null>(null);
  let titre = $state("");
  let reunirDoublons = $state(false);
  let archiver = $state(true);
  let enCours = $state(false);
  /** Ce que la liste de l'ordre vient de faire, pour un lecteur d'écran. */
  let annonceOrdre = $state("");
  let ordre = $state<HTMLOListElement>();

  $effect(() => {
    if (ouvert) untrack(() => void ouvrir());
    else untrack(() => dialogue?.open && dialogue.close());
  });

  async function ouvrir() {
    erreur = null;
    enCours = false;
    reunirDoublons = false;
    archiver = true;
    lus = [];
    filtre = "";
    await tick();
    if (dialogue && !dialogue.open) dialogue.showModal();
    if (ids.length >= 2) {
      etape = "reglages";
      await charger(ids);
      return;
    }
    etape = "choix";
    coches = [...ids];
    liste = null;
    try {
      liste = await api.schemas();
    } catch (e) {
      erreur = `La liste des schémas est illisible : ${messageErreur(e)}`;
      liste = [];
    }
  }

  async function charger(choisis: string[]) {
    chargement = true;
    erreur = null;
    try {
      lus = await Promise.all(choisis.map((id) => lireEchange(id)));
      titre = titrePropose(lus.map((l) => l.schema));
    } catch (e) {
      erreur = `Lecture impossible : ${messageErreur(e)}`;
      lus = [];
    } finally {
      chargement = false;
    }
  }

  async function continuer() {
    etape = "reglages";
    await charger(coches);
    await tick();
    dialogue?.querySelector<HTMLInputElement>("#nom-reunion")?.focus();
  }

  const visibles = $derived(
    (liste ?? []).filter((s) => !filtre.trim() || s.titre.toLocaleLowerCase("fr").includes(filtre.trim().toLocaleLowerCase("fr"))),
  );

  // L'aperçu de la réunion : le nom des pages, les avertissements. Le titre n'y change rien : il n'en dépend pas.
  const apercu = $derived(lus.length >= 2 ? reunir(lus.map((l) => l.schema), { titre: "", reunirDoublons }) : null);
  const doublons = $derived(doublonsEntre(lus.map((l) => l.schema)));

  const pluriel = (n: number, un: string, plusieurs: string) => `${n} ${n > 1 ? plusieurs : un}`;
  const enGuillemets = (noms: string[]) => {
    const premiers = noms.slice(0, 3).map((n) => `« ${n} »`).join(", ");
    return noms.length > 3 ? `${premiers} et ${pluriel(noms.length - 3, "autre", "autres")}` : premiers;
  };
  const ouDeux = $derived(lus.length > 2 ? "plusieurs schémas" : "deux schémas");
  const aideGarder = $derived(
    doublons.length === 1
      ? `${enGuillemets(doublons)} existe dans ${ouDeux} : deux équipements, un sur chaque page.`
      : `${enGuillemets(doublons)} existent dans ${ouDeux} : chacun reste en double, un sur chaque page.`,
  );
  const aideReunir = $derived(
    doublons.length === 1
      ? `Un seul ${enGuillemets(doublons)} ; ses câbles vers l'autre page deviennent des renvois.`
      : "Un seul de chacun ; leurs câbles vers les autres pages deviennent des renvois.",
  );

  // --- L'ordre : à glisser, ou Alt+flèches ---

  function deplacer(depart: number, arrivee: number) {
    const rang = Math.max(0, Math.min(lus.length - 1, arrivee));
    if (rang === depart) return;
    const suite = [...lus];
    const [pris] = suite.splice(depart, 1);
    if (!pris) return;
    suite.splice(rang, 0, pris);
    lus = suite;
    annonceOrdre = `« ${pris.schema.titre} » en position ${rang + 1} sur ${suite.length}.`;
  }

  function clavierOrdre(evenement: KeyboardEvent, rang: number) {
    if (!evenement.altKey || (evenement.key !== "ArrowUp" && evenement.key !== "ArrowDown")) return;
    evenement.preventDefault();
    const id = lus[rang]?.id;
    deplacer(rang, rang + (evenement.key === "ArrowUp" ? -1 : 1));
    void tick().then(() => ordre?.querySelector<HTMLElement>(`[data-schema="${id}"]`)?.focus());
  }

  let glisse = $state<{ rang: number; depart: number; bouge: boolean; cible: number } | null>(null);

  function appui(evenement: PointerEvent, rang: number) {
    if (evenement.button !== 0) return;
    glisse = { rang, depart: evenement.clientY, bouge: false, cible: rang };
  }

  function mouvement(evenement: PointerEvent) {
    if (!glisse || !ordre) return;
    if (!glisse.bouge && Math.abs(evenement.clientY - glisse.depart) < 5) return;
    glisse.bouge = true;
    const lignes = [...ordre.querySelectorAll<HTMLElement>("[data-schema]")];
    // Le rang d'arrivée : le nombre de lignes (autres qu'elle) dont le milieu est au-dessus du pointeur.
    glisse.cible = lignes.filter((ligne, rang) => {
      if (rang === glisse?.rang) return false;
      const cadre = ligne.getBoundingClientRect();
      return cadre.top + cadre.height / 2 < evenement.clientY;
    }).length;
  }

  function relachement() {
    const fini = glisse;
    glisse = null;
    if (fini?.bouge) deplacer(fini.rang, fini.cible);
  }

  /** Où se pose la marque d'insertion pendant un glissement. */
  const marque = $derived.by(() => {
    if (!glisse?.bouge || !ordre) return null;
    const autres = [...ordre.querySelectorAll<HTMLElement>("[data-schema]")].filter((_, rang) => rang !== glisse?.rang);
    const fond = ordre.getBoundingClientRect();
    const reference = autres[glisse.cible];
    const derniere = autres[autres.length - 1];
    const y = reference ? reference.getBoundingClientRect().top - 3 : derniere ? derniere.getBoundingClientRect().bottom + 2 : 0;
    return y - fond.top;
  });

  // --- Réunir ---

  async function executer(evenement: SubmitEvent) {
    evenement.preventDefault();
    if (lus.length < 2 || !titre.trim() || enCours) return;
    enCours = true;
    erreur = null;
    let cree: string;
    try {
      // Un éditeur ouvert sur l'un d'eux enregistre d'abord : le schéma archivé est celui qu'on y voit.
      for (const l of lus) await l.editeur?.enregistrer();
      cree = await creerEchange(reunir(lus.map((l) => l.schema), { titre: titre.trim(), reunirDoublons }).schema);
    } catch (e) {
      erreur = `Réunion impossible : ${messageErreur(e)}. Rien n'a été créé.`;
      enCours = false;
      return;
    }
    if (archiver) {
      const restes: string[] = [];
      for (const l of lus) {
        try {
          await api.rangerSchema(l.id, "archive");
        } catch {
          restes.push(l.schema.titre);
        }
      }
      if (restes.length > 0) {
        erreur = `Le schéma réuni est créé, mais ${enGuillemets(restes)} n'${restes.length > 1 ? "ont" : "a"} pas pu être archivé${restes.length > 1 ? "s" : ""}.`;
        enCours = false;
        onFait(cree);
        return;
      }
    }
    enCours = false;
    onFait(cree);
    dialogue?.close();
  }
</script>

<svelte:window onpointermove={mouvement} onpointerup={relachement} onpointercancel={() => (glisse = null)} />

{#if ouvert}
  <dialog
    bind:this={dialogue}
    class="dialogue-reunir"
    aria-labelledby="titre-reunir"
    onclose={() => {
      if (ouvert) onFermer();
    }}
  >
    {#if etape === "choix"}
      <form
        onsubmit={(e) => {
          e.preventDefault();
          if (coches.length >= 2) void continuer();
        }}
      >
        <h2 id="titre-reunir">Réunir des schémas</h2>
        <p class="aide-tete">Choisissez au moins deux schémas : chacun devient une ou plusieurs pages du nouveau.</p>
        {#if (liste?.length ?? 0) > 6}
          <input class="champ" type="search" placeholder="Chercher un schéma" aria-label="Chercher un schéma" bind:value={filtre} />
        {/if}
        {#if liste === null}
          <p class="aide-tete">Lecture des schémas…</p>
        {:else if liste.length < 2}
          <p class="aide-tete">Il faut au moins deux schémas dans la liste pour les réunir.</p>
        {:else}
          <ul class="choix-schemas" aria-label="Schémas">
            {#each visibles as s (s.id)}
              <li>
                <label class="case-schema">
                  <input type="checkbox" value={s.id} bind:group={coches} />
                  <span class="nom">{s.titre}</span>
                  <span class="compte">{pluriel(s.equipements, "équipement", "équipements")}</span>
                </label>
              </li>
            {/each}
          </ul>
        {/if}
        {#if erreur}<p class="erreur" role="alert">{erreur}</p>{/if}
        <div class="actions">
          <button type="button" class="bouton" onclick={() => dialogue?.close()}>Annuler</button>
          <button type="submit" class="bouton principal" disabled={coches.length < 2}>
            {coches.length >= 2 ? `Continuer avec ${coches.length} schémas` : "Continuer"}
          </button>
        </div>
      </form>
    {:else}
      <form onsubmit={executer}>
        <h2 id="titre-reunir">{lus.length >= 2 ? `Réunir ${lus.length} schémas en un` : "Réunir des schémas"}</h2>
        {#if chargement}
          <p class="aide-tete">Lecture des schémas…</p>
        {:else if lus.length >= 2 && apercu}
          <label class="etiquette" for="nom-reunion">Nom du nouveau schéma</label>
          <input id="nom-reunion" class="champ" maxlength="200" bind:value={titre} />

          <h3 id="titre-ordre">L'ordre des pages (à glisser)</h3>
          <p id="aide-ordre" class="visuellement-cache">Alt+flèche haut ou bas déplace le schéma choisi.</p>
          <div class="cadre-ordre">
            <ol class="ordre" bind:this={ordre} aria-labelledby="titre-ordre">
              {#each lus as l, rang (l.id)}
                {@const pages = apercu.pagesParSchema[rang] ?? []}
                {@const plusieurs = pagesDe(l.schema).length > 1}
                <li>
                  <button
                    type="button"
                    class="ligne"
                    class:glisse={glisse?.bouge && glisse.rang === rang}
                    data-schema={l.id}
                    aria-describedby="aide-ordre"
                    title="Glisser pour changer l'ordre ; Alt+flèches au clavier"
                    onpointerdown={(e) => appui(e, rang)}
                    onkeydown={(e) => clavierOrdre(e, rang)}
                  >
                    <span class="poignee" aria-hidden="true">⋮</span>
                    <span class="nom">{l.schema.titre}</span>
                    <span class="compte">{plusieurs ? `${pluriel(pages.length, "page", "pages")}, gardées` : "1 page"}</span>
                  </button>
                  {#if plusieurs}
                    <p class="sous">{pages.map((p) => p.nom).join(" · ")}</p>
                  {/if}
                </li>
              {/each}
            </ol>
            {#if marque !== null}<span class="insertion" style="top: {marque}px" aria-hidden="true"></span>{/if}
          </div>
          <p class="visuellement-cache" role="status">{annonceOrdre}</p>

          {#if doublons.length > 0}
            <fieldset>
              <legend><h3>Ce qui se retrouve des deux côtés</h3></legend>
              <label class="choix">
                <input type="radio" name="doublons" value={false} bind:group={reunirDoublons} />
                <span class="nom">Garder chaque équipement à sa page</span>
                <span class="aide">{aideGarder}</span>
              </label>
              <label class="choix">
                <input type="radio" name="doublons" value={true} bind:group={reunirDoublons} />
                <span class="nom">Réunir ceux qui ont le même nom et la même adresse</span>
                <span class="aide">{aideReunir}</span>
              </label>
            </fieldset>
          {/if}

          {#if apercu.avertissements.length > 0}
            <ul class="avertissements" aria-label="Avertissements">
              {#each apercu.avertissements as a (a)}
                <li>{a}</li>
              {/each}
            </ul>
          {/if}

          <label class="case">
            <input type="checkbox" bind:checked={archiver} />
            <span>Archiver les {lus.length} schémas d'origine (les fiches qui les insèrent les montrent toujours)</span>
          </label>
        {/if}
        {#if erreur}<p class="erreur" role="alert">{erreur}</p>{/if}
        <div class="actions">
          {#if ids.length < 2}
            <button type="button" class="bouton discret retour" disabled={enCours} onclick={() => (etape = "choix")}>Changer le choix</button>
          {/if}
          <button type="button" class="bouton" onclick={() => dialogue?.close()}>Annuler</button>
          <button type="submit" class="bouton principal" disabled={lus.length < 2 || !titre.trim() || enCours || chargement}>
            {enCours ? "Réunion…" : "Réunir"}
          </button>
        </div>
      </form>
    {/if}
  </dialog>
{/if}

<style>
  dialog {
    /* La largeur de la fenêtre moins 16 pixels de chaque côté, au plus 560. */
    width: min(560px, calc(100vw - 32px));
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

  h3 {
    margin: 6px 0 0;
    color: var(--texte-secondaire);
    font-size: 11.5px;
    font-weight: 700;
    letter-spacing: 0.02em;
    text-transform: uppercase;
  }

  .aide-tete {
    margin: 0;
    color: var(--texte-secondaire);
  }

  .etiquette {
    margin-bottom: -6px;
    color: var(--texte-secondaire);
    font-size: 12.5px;
  }

  .choix-schemas {
    display: flex;
    flex-direction: column;
    gap: 4px;
    max-height: min(360px, 50vh);
    margin: 0;
    padding: 2px;
    overflow-y: auto;
    list-style: none;
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
  .case input {
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

  /* L'ordre des pages : une ligne par schéma, à glisser ; ses pages en dessous, en retrait. */
  .cadre-ordre {
    position: relative;
  }

  .ordre {
    display: flex;
    flex-direction: column;
    gap: 4px;
    margin: 0;
    padding: 0;
    list-style: none;
    user-select: none;
  }

  .ordre li {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  .ligne {
    display: flex;
    align-items: center;
    gap: 8px;
    width: 100%;
    min-height: 36px;
    padding: 4px 10px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--fond);
    color: var(--texte);
    font: inherit;
    text-align: left;
    cursor: grab;
    touch-action: none;
  }

  .ligne:hover {
    border-color: var(--texte-secondaire);
  }

  .ligne:focus-visible {
    outline: 2px solid var(--focus);
    outline-offset: 1px;
  }

  .ligne.glisse {
    opacity: 0.55;
  }

  .poignee {
    flex: none;
    color: var(--texte-secondaire);
    font-weight: 700;
  }

  .sous {
    margin: 0 0 0 24px;
    padding: 4px 10px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--fond);
    font-size: 12.5px;
    overflow-wrap: anywhere;
  }

  .insertion {
    position: absolute;
    left: 0;
    right: 0;
    height: 2px;
    border-radius: 1px;
    background: var(--principale);
    pointer-events: none;
  }

  fieldset {
    display: grid;
    gap: 6px;
    min-width: 0;
    margin: 0;
    padding: 0;
    border: none;
  }

  legend {
    padding: 0;
    margin-bottom: 6px;
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

  .avertissements {
    display: flex;
    flex-direction: column;
    gap: 4px;
    margin: 0;
    padding: 8px 10px 8px 26px;
    border-radius: var(--rayon);
    background: var(--avertissement-fond);
    font-size: 12.5px;
  }

  .avertissements li::marker {
    color: var(--avertissement);
  }

  .case {
    display: flex;
    align-items: flex-start;
    gap: 8px;
    cursor: pointer;
  }

  .case input {
    margin-top: 2px;
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
