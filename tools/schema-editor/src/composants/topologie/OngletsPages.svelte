<script lang="ts">
  import ChevronDown from "@lucide/svelte/icons/chevron-down";
  import Plus from "@lucide/svelte/icons/plus";
  import { tick } from "svelte";
  import { placerMenu } from "../../lib/placement";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import { NOM_PAGE_MAX } from "../../lib/topologie/pages";

  // Les pages d'un schéma (30A), en onglets sous le plan, comme dans draw.io : un clic montre une page, « + » en
  // ajoute une ; clic droit (ou la touche Menu) : renommer, dupliquer, déplacer, supprimer ; double clic : renommer ;
  // glisser un onglet le déplace. Quand les onglets ne tiennent plus (fenêtre étroite), ils se replient en un menu
  // « Étage 1 ▾ » suivi de « 1 / 4 » et de « + ». Avec les échanges entre schémas (31D), « + » ouvre un petit menu
  // (une nouvelle page, ou des pages d'un autre schéma), et le clic droit copie ou déplace la page vers un autre schéma,
  // ou en fait un nouveau.
  let {
    editeur,
    onchanger,
    onautresPages,
    onenvoyer,
    onnouveauSchema,
  }: {
    editeur: EditeurSchema;
    onchanger?: () => void;
    /** « Pages d'un autre schéma… » : ajouter ici des pages venues d'ailleurs. */
    onautresPages?: () => void;
    /** « Copier vers un autre schéma… », « Déplacer vers un autre schéma… ». */
    onenvoyer?: (page: string, deplacer: boolean) => void;
    /** « Faire un nouveau schéma de cette page ». */
    onnouveauSchema?: (page: string) => void;
  } = $props();

  const pages = $derived(editeur.pages);
  const montree = $derived(editeur.pageMontree);
  const rangMontre = $derived(Math.max(0, pages.findIndex((p) => p.id === montree)));
  const plusieurs = $derived(pages.length > 1);
  const total = $derived(editeur.schema.equipements.length);

  /** Le menu d'une page (au clic droit, ou replié en fenêtre étroite), ou celui de « + » (`ajout`). */
  type Menu = { page: string; x: number; y: number; origine: HTMLElement | null; liste?: boolean; ajout?: boolean };
  let menu = $state<Menu | null>(null);
  /** La page dont le nom est en cours de saisie. */
  let renommage = $state<string | null>(null);
  let saisie = $state("");
  /** La page à supprimer, une fois la question posée. */
  let aSupprimer = $state<string | null>(null);
  let barre: HTMLElement;

  const pluriel = (n: number, un: string, plusieursMots: string) => `${n} ${n > 1 ? plusieursMots : un}`;

  function montrer(id: string) {
    if (glisse?.bouge) return;
    editeur.montrerPage(id);
    onchanger?.();
  }

  function ajouter() {
    menu = null;
    editeur.ajouterPage();
    onchanger?.();
  }

  /** « + » : une page vide tout de suite ; s'il y a d'autres façons d'en ajouter, un petit menu les propose. */
  function plus(evenement: MouseEvent) {
    if (!onautresPages) {
      ajouter();
      return;
    }
    const origine = evenement.currentTarget as HTMLElement;
    const cadre = origine.getBoundingClientRect();
    menu = { page: montree, x: cadre.left, y: cadre.top, origine, ajout: true };
  }

  // --- Menu d'un onglet ---

  function ouvrirMenu(evenement: MouseEvent | KeyboardEvent, id: string, liste = false) {
    evenement.preventDefault();
    const origine = evenement.currentTarget as HTMLElement | null;
    const cadre = origine?.getBoundingClientRect();
    const auClavier = !(evenement instanceof MouseEvent) || (evenement.clientX === 0 && evenement.clientY === 0);
    menu = {
      page: id,
      x: auClavier || !cadre ? (cadre?.left ?? 0) : evenement.clientX,
      y: cadre?.top ?? 0,
      origine,
      liste,
    };
  }

  function fermerMenu(rendre = true) {
    const origine = menu?.origine;
    menu = null;
    if (rendre && origine?.isConnected) origine.focus();
  }

  /** Le menu s'ouvre au-dessus de l'onglet (la barre est en bas) et reste dans la fenêtre. */
  function placer(liste: HTMLElement, voulu: Menu) {
    const cadre = liste.getBoundingClientRect();
    const place = placerMenu(
      { x: voulu.x, y: voulu.y - cadre.height - 6 },
      { largeur: cadre.width, hauteur: cadre.height },
      { largeur: document.documentElement.clientWidth, hauteur: document.documentElement.clientHeight },
    );
    liste.style.left = `${place.x}px`;
    liste.style.top = `${place.y}px`;
    const entrees = () => [...liste.querySelectorAll<HTMLButtonElement>("button:not(:disabled)")];
    entrees()[0]?.focus();
    const touche = (evenement: KeyboardEvent) => {
      const tous = entrees();
      const ici = tous.indexOf(document.activeElement as HTMLButtonElement);
      if (evenement.key === "Escape" || evenement.key === "Tab") fermerMenu();
      else if (evenement.key === "ArrowDown") tous[(ici + 1) % tous.length]?.focus();
      else if (evenement.key === "ArrowUp") tous[(ici - 1 + tous.length) % tous.length]?.focus();
      else if (evenement.key === "Home") tous[0]?.focus();
      else if (evenement.key === "End") tous[tous.length - 1]?.focus();
      else return;
      evenement.preventDefault();
      evenement.stopPropagation();
    };
    liste.addEventListener("keydown", touche);
    return { destroy: () => liste.removeEventListener("keydown", touche) };
  }

  function choisir(action: (id: string) => void) {
    const id = menu?.page;
    fermerMenu(false);
    if (id !== undefined) action(id);
  }

  /** Déplace la page du menu au rang `rang`, et le dit. */
  function deplacer(rang: number) {
    choisir((id) => {
      editeur.deplacerPage(id, rang);
      editeur.annonce = `Page « ${pages.find((p) => p.id === id)?.nom ?? ""} » déplacée.`;
    });
  }

  // --- Renommer ---

  async function renommer(id: string) {
    const page = pages.find((p) => p.id === id);
    if (!page) return;
    if (id !== montree) editeur.montrerPage(id);
    renommage = id;
    saisie = page.nom;
    await tick();
    // Le champ visible : dans l'onglet, ou à la place du menu replié d'une fenêtre étroite.
    const champ = [...(barre?.querySelectorAll<HTMLInputElement>("input.nom-page") ?? [])].find((c) => c.offsetParent !== null);
    champ?.focus();
    champ?.select();
  }

  const ouvrirDialogue = (dialogue: HTMLDialogElement) => dialogue.showModal();

  function finirRenommage(garder: boolean) {
    const id = renommage;
    if (id === null) return;
    renommage = null;
    const nom = saisie.trim();
    const avant = pages.find((p) => p.id === id)?.nom;
    if (garder && nom && nom !== avant) editeur.renommerPage(id, nom);
    else if (garder && !nom) editeur.annonce = "Une page doit avoir un nom : il n'a pas changé.";
    void tick().then(() => barre?.querySelector<HTMLElement>('[aria-selected="true"], .choix')?.focus());
  }

  // --- Glisser un onglet ---

  let glisse = $state<{ id: string; depart: number; x: number; bouge: boolean; cible: number } | null>(null);

  function appui(evenement: PointerEvent, id: string) {
    if (evenement.button !== 0 || renommage !== null) return;
    glisse = { id, depart: evenement.clientX, x: evenement.clientX, bouge: false, cible: pages.findIndex((p) => p.id === id) };
  }

  function mouvement(evenement: PointerEvent) {
    if (!glisse) return;
    glisse.x = evenement.clientX;
    if (!glisse.bouge && Math.abs(glisse.x - glisse.depart) < 6) return;
    glisse.bouge = true;
    const onglets = [...barre.querySelectorAll<HTMLElement>("[data-page]")];
    // Le rang d'arrivée : le nombre d'onglets (autres que lui) dont le milieu est à gauche du pointeur.
    glisse.cible = onglets.filter((o) => {
      if (o.dataset.page === glisse?.id) return false;
      const cadre = o.getBoundingClientRect();
      return cadre.left + cadre.width / 2 < evenement.clientX;
    }).length;
  }

  function relachement() {
    const fini = glisse;
    if (!fini) return;
    // Le clic qui suit le relâchement ne doit pas changer de page : on attend qu'il soit passé.
    setTimeout(() => (glisse = null));
    if (!fini.bouge) return;
    const depart = pages.findIndex((p) => p.id === fini.id);
    if (fini.cible !== depart) {
      editeur.deplacerPage(fini.id, fini.cible);
      editeur.annonce = `Page « ${pages.find((p) => p.id === fini.id)?.nom ?? ""} » déplacée.`;
    }
  }

  /** Où se pose la marque d'insertion pendant un glissement : avant l'onglet de ce rang (ou après le dernier). */
  const marque = $derived.by(() => {
    if (!glisse?.bouge || !barre) return null;
    const autres = [...barre.querySelectorAll<HTMLElement>("[data-page]")].filter((o) => o.dataset.page !== glisse?.id);
    const fond = barre.getBoundingClientRect();
    const reference = autres[glisse.cible];
    const derniere = autres[autres.length - 1];
    const x = reference ? reference.getBoundingClientRect().left - 2 : derniere ? derniere.getBoundingClientRect().right + 1 : 0;
    return x - fond.left;
  });

  function clavierOnglet(evenement: KeyboardEvent, id: string) {
    const rang = pages.findIndex((p) => p.id === id);
    if (evenement.key === "ContextMenu" || (evenement.shiftKey && evenement.key === "F10")) {
      ouvrirMenu(evenement, id);
    } else if (evenement.key === "F2") {
      evenement.preventDefault();
      void renommer(id);
    } else if (evenement.key === "ArrowRight" || evenement.key === "ArrowLeft") {
      evenement.preventDefault();
      const voisine = pages[(rang + (evenement.key === "ArrowRight" ? 1 : pages.length - 1)) % pages.length];
      if (!voisine) return;
      if (evenement.altKey && evenement.shiftKey) {
        editeur.deplacerPage(id, rang + (evenement.key === "ArrowRight" ? 1 : -1));
        void tick().then(() => barre?.querySelector<HTMLElement>(`[data-page="${id}"]`)?.focus());
        return;
      }
      editeur.montrerPage(voisine.id);
      void tick().then(() => barre?.querySelector<HTMLElement>(`[data-page="${voisine.id}"]`)?.focus());
    }
  }

  const aSupprimerPage = $derived(aSupprimer === null ? undefined : pages.find((p) => p.id === aSupprimer));
  const nombreASupprimer = $derived(aSupprimer === null ? 0 : (editeur.equipementsParPage.get(aSupprimer) ?? 0));
</script>

<svelte:window onpointermove={mouvement} onpointerup={relachement} onpointercancel={() => (glisse = null)} />

<div class="pages" class:plusieurs bind:this={barre} aria-label="Pages du schéma" role="toolbar">
  <div class="onglets" role="tablist" aria-label="Pages">
    {#each pages as p, rang (p.id)}
      {#if renommage === p.id}
        <input
          class="champ nom-page"
          aria-label="Nom de la page"
          maxlength={NOM_PAGE_MAX}
          bind:value={saisie}
          onkeydown={(e) => {
            e.stopPropagation();
            if (e.key === "Enter") finirRenommage(true);
            else if (e.key === "Escape") {
              e.preventDefault();
              finirRenommage(false);
            }
          }}
          onblur={() => finirRenommage(true)}
        />
      {:else}
        <button
          type="button"
          class="onglet"
          class:glisse={glisse?.bouge && glisse.id === p.id}
          role="tab"
          data-page={p.id}
          aria-selected={p.id === montree}
          tabindex={p.id === montree ? 0 : -1}
          title="{p.nom} : clic droit pour renommer, dupliquer, déplacer ou supprimer ; double clic pour renommer"
          onclick={() => montrer(p.id)}
          ondblclick={() => void renommer(p.id)}
          oncontextmenu={(e) => ouvrirMenu(e, p.id)}
          onpointerdown={(e) => appui(e, p.id)}
          onkeydown={(e) => clavierOnglet(e, p.id)}
        >
          <span class="nom">{p.nom}</span>
          {#if plusieurs}<span class="nombre" aria-label="{pluriel(editeur.equipementsParPage.get(p.id) ?? 0, 'équipement', 'équipements')}">{editeur.equipementsParPage.get(p.id) ?? 0}</span>{/if}
          <span class="visuellement-cache">, page {rang + 1} sur {pages.length}</span>
        </button>
      {/if}
    {/each}
    {#if marque !== null}<span class="insertion" style="left: {marque}px" aria-hidden="true"></span>{/if}
  </div>

  <!-- Fenêtre étroite : un menu à la place des onglets. -->
  <div class="replie">
    {#if renommage !== null}
      <input
        class="champ nom-page"
        aria-label="Nom de la page"
        maxlength={NOM_PAGE_MAX}
        bind:value={saisie}
        onkeydown={(e) => {
          e.stopPropagation();
          if (e.key === "Enter") finirRenommage(true);
          else if (e.key === "Escape") {
            e.preventDefault();
            finirRenommage(false);
          }
        }}
        onblur={() => finirRenommage(true)}
      />
    {:else}
      <button
        type="button"
        class="choix"
        aria-haspopup="menu"
        aria-expanded={menu?.liste === true}
        title="Changer de page ; renommer, dupliquer, déplacer ou supprimer celle-ci"
        onclick={(e) => ouvrirMenu(e, montree, true)}
        oncontextmenu={(e) => ouvrirMenu(e, montree, true)}
      >
        <span class="nom">{pages[rangMontre]?.nom}</span>
        <ChevronDown size={14} aria-hidden="true" />
      </button>
    {/if}
    <span class="position" aria-label="Page {rangMontre + 1} sur {pages.length}">{rangMontre + 1} / {pages.length}</span>
  </div>

  <button
    type="button"
    class="plus"
    title={onautresPages ? "Ajouter une page" : "Nouvelle page"}
    aria-label={onautresPages ? "Ajouter une page" : "Nouvelle page"}
    aria-haspopup={onautresPages ? "menu" : undefined}
    aria-expanded={onautresPages ? menu?.ajout === true : undefined}
    onclick={plus}
  >
    <Plus size={16} aria-hidden="true" />
  </button>
  {#if plusieurs}
    <span class="resume">{pluriel(pages.length, "page", "pages")} · {pluriel(total, "équipement", "équipements")}</span>
  {/if}
</div>

{#if menu}
  {@const voulu = menu}
  {@const rang = pages.findIndex((p) => p.id === voulu.page)}
  <!-- svelte-ignore a11y_no_static_element_interactions, a11y_click_events_have_key_events -->
  <div class="voile-menu" onpointerdown={() => fermerMenu()} oncontextmenu={(e) => { e.preventDefault(); fermerMenu(); }}></div>
  {#key voulu}
    <div class="menu" role="menu" aria-label={voulu.ajout ? "Ajouter une page" : `Page « ${pages[rang]?.nom ?? ""} »`} use:placer={voulu}>
      {#if voulu.ajout}
        <button type="button" role="menuitem" class="premier" onclick={ajouter}>Nouvelle page</button>
        <button type="button" role="menuitem" onclick={() => choisir(() => onautresPages?.())}>Pages d'un autre schéma…</button>
      {:else}
      {#if voulu.liste}
        {#each pages as p (p.id)}
          <button type="button" role="menuitemradio" aria-checked={p.id === montree} onclick={() => choisir(() => montrer(p.id))}>
            <span class="nom">{p.nom}</span>
            {#if plusieurs}<span class="nombre">{editeur.equipementsParPage.get(p.id) ?? 0}</span>{/if}
          </button>
        {/each}
        <hr />
      {/if}
      <button type="button" role="menuitem" onclick={() => choisir((id) => void renommer(id))}>Renommer</button>
      <button type="button" role="menuitem" onclick={() => choisir((id) => editeur.dupliquerPage(id))}>Dupliquer la page</button>
      <!-- Le rang se lit avant de fermer le menu : fermé, il n'a plus de page. -->
      <button type="button" role="menuitem" disabled={rang <= 0} onclick={() => deplacer(rang - 1)}>Déplacer à gauche</button>
      <button type="button" role="menuitem" disabled={rang < 0 || rang >= pages.length - 1} onclick={() => deplacer(rang + 1)}>Déplacer à droite</button>
      {#if onenvoyer || onnouveauSchema}
        <hr />
        {#if onenvoyer}
          <button type="button" role="menuitem" onclick={() => choisir((id) => onenvoyer?.(id, false))}>Copier vers un autre schéma…</button>
          <button type="button" role="menuitem" onclick={() => choisir((id) => onenvoyer?.(id, true))}>Déplacer vers un autre schéma…</button>
        {/if}
        {#if onnouveauSchema}
          <button type="button" role="menuitem" onclick={() => choisir((id) => onnouveauSchema?.(id))}>Faire un nouveau schéma de cette page</button>
        {/if}
      {/if}
      <hr />
      <button type="button" role="menuitem" class="danger" disabled={!plusieurs} title={plusieurs ? undefined : "Un schéma garde au moins une page."} onclick={() => choisir((id) => (aSupprimer = id))}>Supprimer la page…</button>
      {/if}
    </div>
  {/key}
{/if}

{#if aSupprimerPage}
  {@const page = aSupprimerPage}
  <dialog class="confirmation" aria-labelledby="titre-suppression-page" use:ouvrirDialogue onclose={() => (aSupprimer = null)}>
    <h2 id="titre-suppression-page">Supprimer la page « {page.nom} » ?</h2>
    <p>
      {nombreASupprimer > 0
        ? `Ses ${pluriel(nombreASupprimer, "équipement", "équipements")} partent avec elle, avec leurs câbles, y compris vers les autres pages.`
        : "Elle n'a aucun équipement."}
      Ctrl+Z l'annule, et elle reste dans les versions du schéma.
    </p>
    <div class="actions">
      <!-- svelte-ignore a11y_autofocus -->
      <button type="button" class="bouton" autofocus onclick={(e) => e.currentTarget.closest("dialog")?.close()}>Annuler</button>
      <button
        type="button"
        class="bouton principal"
        onclick={(e) => {
          e.currentTarget.closest("dialog")?.close();
          editeur.supprimerPage(page.id);
          onchanger?.();
        }}
      >
        Supprimer la page
      </button>
    </div>
  </dialog>
{/if}

<style>
  /* La barre des pages, entre le plan et le plateau (30A). */
  .pages {
    position: relative;
    display: flex;
    flex: none;
    align-items: center;
    gap: 2px;
    min-height: 34px;
    padding: 0 8px;
    border-top: 1px solid var(--bordure);
    background: var(--surface);
    font-size: 12.5px;
    /* Glisser un onglet le déplace : rien ne se sélectionne au passage. */
    user-select: none;
  }

  .onglets {
    position: relative;
    display: flex;
    flex: 0 1 auto;
    align-items: center;
    gap: 2px;
    min-width: 0;
    overflow-x: auto;
    scrollbar-width: thin;
  }

  .onglet,
  .choix {
    display: inline-flex;
    flex: none;
    align-items: center;
    gap: 6px;
    height: 26px;
    padding: 0 10px;
    border: none;
    border-radius: 7px;
    background: transparent;
    color: var(--texte);
    font: inherit;
    white-space: nowrap;
    cursor: pointer;
    touch-action: none;
    transition: background var(--duree);
  }

  .onglet:hover,
  .choix:hover {
    background: var(--surface-2);
  }

  /* La page montrée : sur le fond du plan, cernée, soulignée de la couleur principale. */
  .onglet[aria-selected="true"],
  .choix {
    background: var(--fond);
    box-shadow:
      inset 0 0 0 1px var(--bordure),
      inset 0 -2px 0 var(--principale);
    font-weight: 600;
  }

  .onglet:focus-visible,
  .choix:focus-visible,
  .plus:focus-visible {
    outline: 2px solid var(--focus);
    outline-offset: -2px;
  }

  .onglet.glisse {
    opacity: 0.55;
  }

  .nom {
    overflow: hidden;
    max-width: 220px;
    text-overflow: ellipsis;
  }

  .nombre {
    color: var(--texte-secondaire);
    font-size: 11px;
    font-weight: 400;
  }

  .insertion {
    position: absolute;
    top: 4px;
    bottom: 4px;
    width: 2px;
    border-radius: 1px;
    background: var(--principale);
    pointer-events: none;
  }

  .champ.nom-page {
    width: 160px;
    height: 26px;
    padding: 0 8px;
    font-size: 12.5px;
  }

  .plus {
    display: inline-flex;
    flex: none;
    align-items: center;
    justify-content: center;
    width: 28px;
    height: 26px;
    border: none;
    border-radius: 7px;
    background: transparent;
    color: var(--texte-secondaire);
    cursor: pointer;
  }

  .plus:hover {
    background: var(--surface-2);
    color: var(--texte);
  }

  .resume {
    flex: none;
    margin-left: auto;
    padding-left: 12px;
    color: var(--texte-secondaire);
    font-size: 11.5px;
    white-space: nowrap;
  }

  .replie {
    display: none;
    align-items: center;
    gap: 6px;
    min-width: 0;
  }

  .replie .choix {
    min-width: 0;
  }

  .position {
    color: var(--texte-secondaire);
    font-size: 11.5px;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .voile-menu {
    position: fixed;
    inset: 0;
    z-index: 29;
  }

  .menu {
    position: fixed;
    z-index: 30;
    display: flex;
    flex-direction: column;
    min-width: 200px;
    max-width: calc(100vw - 16px);
    max-height: calc(100vh - 16px);
    overflow-y: auto;
    padding: 4px;
    border: 1px solid var(--bordure);
    border-radius: 12px;
    background: var(--surface);
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.18);
  }

  .menu button {
    display: flex;
    flex: none;
    align-items: center;
    gap: 8px;
    min-height: 32px;
    padding: 0 10px;
    border: none;
    border-radius: 8px;
    background: none;
    color: var(--texte);
    font: inherit;
    text-align: left;
    cursor: pointer;
  }

  .menu button .nombre {
    margin-left: auto;
  }

  .menu button:hover:not(:disabled),
  .menu button:focus-visible {
    background: var(--surface-2);
  }

  .menu button:disabled {
    opacity: 0.5;
    cursor: default;
  }

  .menu button[aria-checked="true"] {
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
    font-weight: 600;
  }

  .menu button.premier {
    font-weight: 600;
  }

  .menu button.danger {
    color: var(--retard);
  }

  .menu hr {
    width: 100%;
    margin: 4px 0;
    border: none;
    border-top: 1px solid var(--bordure);
  }

  .confirmation {
    width: min(460px, calc(100vw - 32px));
    padding: 24px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 16px 48px rgb(0 0 0 / 0.2);
  }

  .confirmation::backdrop {
    background: var(--voile);
  }

  .confirmation h2 {
    margin-bottom: 12px;
    font-size: 18px;
  }

  .confirmation p {
    margin: 0 0 10px;
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 16px;
  }

  /* Fenêtre étroite : les onglets se replient en un menu, « + » reste à côté (30F). */
  @media (max-width: 620px) {
    .onglets,
    .resume {
      display: none;
    }

    .replie {
      display: flex;
    }
  }
</style>
