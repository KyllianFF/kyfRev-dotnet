<script lang="ts" module>
  import type { PressePapiers } from "../../lib/topologie/operations";

  // Presse-papiers des schémas (D42) : il survit à la fermeture de l'éditeur,
  // pour coller dans un autre schéma.
  let pressePapiers: PressePapiers | null = null;
  let collages = 0;
</script>

<script lang="ts">
  import Cable from "@lucide/svelte/icons/cable";
  import Download from "@lucide/svelte/icons/download";
  import Grid3x3 from "@lucide/svelte/icons/grid-3x3";
  import Eye from "@lucide/svelte/icons/eye";
  import Hand from "@lucide/svelte/icons/hand";
  import Search from "@lucide/svelte/icons/search";
  import Table2 from "@lucide/svelte/icons/table-2";
  import Spline from "@lucide/svelte/icons/spline";
  import MapIcone from "@lucide/svelte/icons/map";
  import Maximize from "@lucide/svelte/icons/maximize";
  import Redo2 from "@lucide/svelte/icons/redo-2";
  import Undo2 from "@lucide/svelte/icons/undo-2";
  import Upload from "@lucide/svelte/icons/upload";
  import X from "@lucide/svelte/icons/x";
  import ZoomIn from "@lucide/svelte/icons/zoom-in";
  import ZoomOut from "@lucide/svelte/icons/zoom-out";
  import { onMount } from "svelte";
  import { api, messageErreur } from "../../lib/api";
  import { inscrireEditeur } from "../../lib/fin";
  import { affichageDe, COTE, dessinerSchema, traitDuSupport } from "../../lib/topologie/dessin";
  import { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import { ecrireSchema } from "../../lib/topologie/format";
  import {
    AFFICHAGES,
    type Affichage,
    type IdVue,
    nouvelId,
    type Point,
    SUPPORTS,
    type Support,
  } from "../../lib/topologie/modele";
  import {
    collerEquipements,
    copierEquipements,
    creerEquipement,
    dupliquerEquipements,
    retirerEquipements,
  } from "../../lib/topologie/operations";
  import { VUES, vue as vueDe } from "../../lib/topologie/vues";
  import Bibliotheque from "./Bibliotheque.svelte";
  import Canevas from "./Canevas.svelte";
  import { GABARITS } from "../../lib/topologie/gabarits";
  import { chercherDansSchema } from "../../lib/topologie/recherche";
  import DialogueConnexion from "./DialogueConnexion.svelte";
  import PlanIp from "./PlanIp.svelte";
  import RechercheSchema from "./RechercheSchema.svelte";
  import PanneauProprietes from "./PanneauProprietes.svelte";

  // Éditeur de schémas réseau (D39), en plein écran au-dessus de la fiche.
  // Le schéma vit dans schemas/<id>.json : il s'enregistre seul, et la fiche
  // n'en garde qu'une référence (rien à y écrire pour le modifier).
  let {
    id = null,
    ancien = null,
    onconverti,
    onfermer,
  }: {
    /** Schéma du coffre à ouvrir. */
    id?: string | null;
    /** Schéma au format 1 (bloc ```reseau de la fiche), à convertir. */
    ancien?: string | null;
    /** Le schéma ancien format vient d'être créé dans le coffre : la fiche doit le citer. */
    onconverti?: (id: string) => void;
    onfermer: () => void;
  } = $props();

  const editeur = new EditeurSchema();
  let dialogue: HTMLDialogElement;
  let canevas = $state<Canevas>();
  let grille = $state(true);
  let main = $state(false);
  let miniCarte = $state(true);
  let zoom = $state(1);
  let erreur = $state<string | null>(null);
  let pret = $state(false);
  let connexion = $state<{ source: string | null; destination: string | null } | null>(null);
  let exportOuvert = $state(false);
  let typesCableOuvert = $state(false);
  let affichageOuvert = $state(false);

  /** Une information affichée ou masquée (D50) : réglage du schéma, aperçus et exports compris. */
  function basculerAffichage(cle: keyof Affichage, valeur: boolean) {
    editeur.agir((s) => {
      s.affichage = { ...(s.affichage ?? {}), [cle]: valeur };
    });
  }
  let planIpOuvert = $state(false);
  let rechercheOuverte = $state(false);
  let texteRecherche = $state("");
  const trouves = $derived(
    rechercheOuverte && texteRecherche.trim()
      ? new Set(chercherDansSchema(editeur.schema, texteRecherche, 500).map((t) => t.id))
      : null,
  );

  /** Choisit un objet et l'amène au centre du plan. */
  function montrer(id: string) {
    editeur.selection = [id];
    canevas?.montrer(id);
  }

  async function exporterPlanIp(csv: string) {
    const dossier = await api.choisirDossier("Dossier où enregistrer le plan d'adressage");
    if (!dossier) return;
    try {
      // Marque UTF-8 : un tableur lit alors les accents.
      const octets = new TextEncoder().encode(`\ufeff${csv}`);
      const chemin = await api.exporterSchema(dossier, `${schema.titre || "Schéma réseau"} - plan IP.csv`, octets);
      editeur.annonce = `Plan IP exporté : ${chemin}`;
    } catch (err) {
      editeur.annonce = `Export impossible : ${messageErreur(err)}`;
    }
  }

  /** Câbles proposés au bouton « Ajouter un lien » (comme la boîte des simulateurs). */
  const TYPES_CABLE: { valeur: Support | "auto"; nom: string }[] = [
    { valeur: "auto", nom: "Automatique (d'après les ports)" },
    ...SUPPORTS.filter((x) => x.valeur !== "personnalise"),
  ];

  function prendreCable(support: Support | "auto") {
    typesCableOuvert = false;
    editeur.selection = [];
    editeur.cable = { support, depart: null };
  }

  /** Équipements de la sélection, hors verrouillés quand on les retire. */
  const equipementsChoisis = () =>
    new Set(editeur.selection.filter((x) => schema.equipements.some((e) => e.id === x)));

  function retirerSelection() {
    if (editeur.selection.length === 0) return;
    const choisis = new Set(editeur.selection);
    editeur.agir((s) => {
      retirerEquipements(
        s,
        new Set(s.equipements.filter((e) => choisis.has(e.id) && !e.verrou).map((e) => e.id)),
      );
      s.liens = s.liens.filter((l) => !choisis.has(l.id));
      s.relations = s.relations.filter((r) => !choisis.has(r.id));
      s.groupes = s.groupes.filter((g) => !choisis.has(g.id) || g.verrou);
      s.annotations = s.annotations.filter((n) => !choisis.has(n.id));
    }, "Retiré : Ctrl+Z pour annuler.");
    editeur.selection = [];
  }

  function dupliquerSelection() {
    const ids = equipementsChoisis();
    if (ids.size) editeur.agir((s) => (editeur.selection = dupliquerEquipements(s, ids)), "Dupliqué.");
  }

  function copierSelection() {
    const ids = equipementsChoisis();
    if (ids.size === 0) return;
    pressePapiers = copierEquipements(schema, ids);
    collages = 0;
    editeur.annonce = `Copié (${ids.size}) : Ctrl+V pour coller, ici ou dans un autre schéma.`;
  }

  /** Colle au centre du champ visible ; chaque collage suivant se décale un peu. */
  function coller() {
    const contenu = pressePapiers;
    if (!contenu || contenu.equipements.length === 0) return;
    const centre = canevas?.centre() ?? { x: 0, y: 0 };
    const xs = contenu.equipements.map((e) => e.position.x);
    const ys = contenu.equipements.map((e) => e.position.y);
    const milieu = { x: (Math.min(...xs) + Math.max(...xs)) / 2, y: (Math.min(...ys) + Math.max(...ys)) / 2 };
    collages++;
    const pas = (collages - 1) * (COTE / 2);
    const decalage = { x: Math.round(centre.x - milieu.x + pas), y: Math.round(centre.y - milieu.y + pas) };
    editeur.agir((s) => (editeur.selection = collerEquipements(s, contenu, decalage)), "Collé.");
  }
  let confirmationImport = $state<string | null>(null);

  const schema = $derived(editeur.schema);
  const nombreAvertissements = $derived(editeur.constats.filter((c) => c.niveau !== "manque").length);

  onMount(() => {
    dialogue.showModal();
    (async () => {
      try {
        if (id) await editeur.ouvrir(id);
        else if (ancien !== null) editeur.ouvrirAncien(ancien, (nouveau) => onconverti?.(nouveau));
        pret = true;
        requestAnimationFrame(() => canevas?.toutVoir());
      } catch (e) {
        erreur = `Ouverture impossible : ${messageErreur(e)}`;
      }
    })();
  });

  // Avant le verrouillage du coffre, l'application demande de finir : le schéma ouvert s'enregistre, et dit ce qu'il en reste.
  onMount(() => inscrireEditeur(() => editeur.finir()));

  // L'annonce disparaît d'elle-même.
  $effect(() => {
    if (!editeur.annonce) return;
    const minuterie = setTimeout(() => (editeur.annonce = null), 5000);
    return () => clearTimeout(minuterie);
  });

  async function fermer() {
    await editeur.enregistrer();
    if (editeur.sauvegarde === "erreur" || editeur.sauvegarde === "conflit") {
      editeur.annonce =
        "Le schéma n'est pas enregistré : réglez le problème indiqué en haut avant de fermer.";
      return;
    }
    dialogue.close();
  }

  /** Une place libre près de `centre` (en spirale, de 120 en 120), pour ne recouvrir aucun équipement. */
  function placeLibre(centre: Point): Point {
    const occupee = (p: Point) =>
      schema.equipements.some((e) => Math.abs(e.position.x - p.x) < 100 && Math.abs(e.position.y - p.y) < 90);
    for (let rayon = 0; rayon <= 6; rayon++) {
      for (let dx = -rayon; dx <= rayon; dx++) {
        for (let dy = -rayon; dy <= rayon; dy++) {
          if (Math.max(Math.abs(dx), Math.abs(dy)) !== rayon) continue;
          const p = { x: centre.x + dx * 120, y: centre.y + dy * 120 };
          if (!occupee(p)) return p;
        }
      }
    }
    return centre;
  }

  function ajouter(genre: string, ou?: Point) {
    const centre = canevas?.centre() ?? { x: 0, y: 0 };
    const point = ou ?? (genre.startsWith("type:") ? placeLibre(centre) : centre);
    let nouveau = "";
    let colles: string[] = [];
    editeur.agir((s) => {
      if (genre.startsWith("type:")) {
        nouveau = creerEquipement(s, genre.slice(5), point).id;
      } else if (genre.startsWith("groupe:")) {
        nouveau = nouvelId("g");
        const genreGroupe = genre.slice(7) as "site";
        s.groupes.push({
          id: nouveau,
          nom:
            { site: "Site", batiment: "Bâtiment", salle: "Salle", zone: "Zone", groupe: "Groupe" }[
              genreGroupe
            ] ?? "Groupe",
          genre: genreGroupe,
          membres: [],
          position: { x: point.x - 160, y: point.y - 100 },
          taille: { largeur: 320, hauteur: 200 },
        });
      } else if (genre.startsWith("gabarit:")) {
        // Modèle de topologie (D48) : collé centré sur le point, noms et adresses gardés.
        const contenu = GABARITS.find((g) => g.id === genre.slice(8))?.construire();
        if (!contenu) return;
        const xs = contenu.equipements.map((x) => x.position.x);
        const ys = contenu.equipements.map((x) => x.position.y);
        const decalage = {
          x: Math.round(point.x - (Math.min(...xs) + Math.max(...xs)) / 2),
          y: Math.round(point.y - (Math.min(...ys) + Math.max(...ys)) / 2),
        };
        colles = collerEquipements(s, contenu, decalage, { suffixe: "", adresses: true });
      } else if (genre.startsWith("annotation")) {
        nouveau = nouvelId("n");
        const forme = genre.split(":")[1] ?? "note";
        if (forme === "rectangle" || forme === "ellipse") {
          s.annotations.push({
            id: nouveau,
            texte: "Zone",
            forme,
            position: { x: point.x - 120, y: point.y - 80 },
            largeur: 240,
            hauteur: 160,
            couleur: "#1f62b0",
          });
        } else if (forme === "fleche") {
          s.annotations.push({
            id: nouveau,
            texte: "",
            forme,
            position: { x: point.x - 80, y: point.y },
            vers: { x: point.x + 80, y: point.y },
            couleur: "#c0392b",
          });
        } else if (forme === "texte") {
          s.annotations.push({ id: nouveau, texte: "Texte", forme, position: point, taille: 18 });
        } else s.annotations.push({ id: nouveau, texte: "Annotation", position: point });
      }
    });
    if (nouveau) editeur.selection = [nouveau];
    if (colles.length) {
      editeur.selection = colles;
      editeur.annonce = "Modèle inséré : Ctrl+Z pour le retirer.";
    }
  }

  function clavier(evenement: KeyboardEvent) {
    const cible = evenement.target as HTMLElement;
    const saisie = cible.closest("input, textarea, select, [contenteditable]");
    const ctrl = evenement.ctrlKey || evenement.metaKey;
    if (ctrl && evenement.key.toLowerCase() === "s") {
      evenement.preventDefault();
      void editeur.enregistrer();
      return;
    }
    if (ctrl && evenement.key.toLowerCase() === "f") {
      evenement.preventDefault();
      rechercheOuverte = true;
      return;
    }
    if (saisie) return;
    if (ctrl && !evenement.shiftKey && evenement.key.toLowerCase() === "z") {
      evenement.preventDefault();
      editeur.annuler();
    } else if (
      ctrl &&
      (evenement.key.toLowerCase() === "y" || (evenement.shiftKey && evenement.key.toLowerCase() === "z"))
    ) {
      evenement.preventDefault();
      editeur.retablir();
    } else if (ctrl && evenement.key.toLowerCase() === "a") {
      evenement.preventDefault();
      editeur.toutChoisir();
    } else if (ctrl && evenement.key.toLowerCase() === "g") {
      // Ctrl+G groupe la sélection, Ctrl+Maj+G la dégroupe.
      evenement.preventDefault();
      if (evenement.shiftKey) editeur.degrouper();
      else editeur.grouper();
    } else if (ctrl && evenement.key.toLowerCase() === "d") {
      evenement.preventDefault();
      dupliquerSelection();
    } else if (ctrl && evenement.key.toLowerCase() === "c") {
      if (equipementsChoisis().size === 0) return;
      evenement.preventDefault();
      copierSelection();
    } else if (ctrl && evenement.key.toLowerCase() === "v") {
      if (!pressePapiers) return;
      evenement.preventDefault();
      coller();
    } else if (evenement.key === "Delete" || evenement.key === "Backspace") {
      if (editeur.selection.length === 0) return;
      evenement.preventDefault();
      retirerSelection();
    } else if (evenement.key === "Escape" && editeur.selection.length > 0) {
      evenement.preventDefault();
      editeur.selection = [];
    } else if (evenement.key.startsWith("Arrow") && editeur.selection.length > 0) {
      evenement.preventDefault();
      const pas = evenement.shiftKey ? 1 : 20;
      const dx = evenement.key === "ArrowLeft" ? -pas : evenement.key === "ArrowRight" ? pas : 0;
      const dy = evenement.key === "ArrowUp" ? -pas : evenement.key === "ArrowDown" ? pas : 0;
      const choisis = new Set(editeur.selection);
      editeur.agir((s) => {
        for (const e of s.equipements)
          if (choisis.has(e.id) && !e.verrou) e.position = { x: e.position.x + dx, y: e.position.y + dy };
        for (const n of s.annotations)
          if (choisis.has(n.id)) n.position = { x: n.position.x + dx, y: n.position.y + dy };
      });
    } else if (
      evenement.key.toLowerCase() === "c" &&
      !ctrl &&
      editeur.selection.length === 1 &&
      schema.equipements.some((e) => e.id === editeur.selection[0])
    ) {
      evenement.preventDefault();
      connexion = { source: editeur.selection[0] ?? null, destination: null };
    }
  }

  /** SVG autonome : couleurs du thème clair, légende. */
  const svgAExporter = () =>
    dessinerSchema($state.snapshot(schema), { legende: true }).replace(/var\(--[\w-]+,\s*([^)]+)\)/g, "$1");

  async function exporter(format: "json" | "svg" | "png") {
    exportOuvert = false;
    const dossier = await api.choisirDossier("Dossier où enregistrer l'export du schéma");
    if (!dossier) return;
    try {
      let octets: Uint8Array;
      if (format === "json") octets = new TextEncoder().encode(ecrireSchema($state.snapshot(schema)));
      else if (format === "svg") octets = new TextEncoder().encode(svgAExporter());
      else octets = await enPng(svgAExporter());
      const chemin = await api.exporterSchema(
        dossier,
        `${schema.titre || "Schéma réseau"}.${format}`,
        octets,
      );
      editeur.annonce = `Exporté : ${chemin}`;
    } catch (e) {
      editeur.annonce = `Export impossible : ${messageErreur(e)}`;
    }
  }

  /** Image PNG (fond blanc, deux fois la taille) d'un SVG. */
  async function enPng(svg: string): Promise<Uint8Array> {
    const image = new Image();
    image.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
    await image.decode();
    const toile = document.createElement("canvas");
    const echelle = Math.min(2, 8000 / Math.max(image.width, image.height, 1));
    toile.width = Math.round(image.width * echelle);
    toile.height = Math.round(image.height * echelle);
    const contexte = toile.getContext("2d");
    if (!contexte) throw new Error("dessin impossible");
    contexte.fillStyle = "#ffffff";
    contexte.fillRect(0, 0, toile.width, toile.height);
    contexte.drawImage(image, 0, 0, toile.width, toile.height);
    const blob = await new Promise<Blob | null>((ok) => toile.toBlob(ok, "image/png"));
    if (!blob) throw new Error("image vide");
    return new Uint8Array(await blob.arrayBuffer());
  }

  async function importer() {
    const chemin = await api.choisirFichierSchema();
    if (!chemin) return;
    try {
      confirmationImport = await api.lireImportSchema(chemin);
    } catch (e) {
      editeur.annonce = `Import impossible : ${messageErreur(e)}`;
    }
  }

  function confirmerImport() {
    if (confirmationImport === null) return;
    const refus = editeur.importer(confirmationImport);
    confirmationImport = null;
    editeur.annonce = refus ?? "Schéma importé : le contenu a été remplacé (Ctrl+Z pour annuler).";
    if (!refus) requestAnimationFrame(() => canevas?.toutVoir());
  }

  const libelleSauvegarde = $derived(
    {
      enregistre: "Enregistré",
      modifie: "Modifications en attente…",
      enregistrement: "Enregistrement…",
      erreur: "Non enregistré",
      conflit: "Conflit",
    }[editeur.sauvegarde],
  );
</script>

<dialog
  bind:this={dialogue}
  class="editeur-topologie"
  aria-labelledby="titre-topologie"
  oncancel={(e) => {
    e.preventDefault();
    if (editeur.cable) {
      editeur.cable = null;
      return;
    }
    if (rechercheOuverte) {
      rechercheOuverte = false;
      return;
    }
    if (typesCableOuvert || affichageOuvert || exportOuvert) {
      typesCableOuvert = false;
      affichageOuvert = false;
      exportOuvert = false;
      return;
    }
    if (connexion || confirmationImport) return;
    void fermer();
  }}
  onclose={onfermer}
  onkeydown={clavier}
>
  <header>
    <div class="intitule">
      <h2 id="titre-topologie">Schéma réseau</h2>
      <input
        class="champ titre"
        aria-label="Titre du schéma"
        value={schema.titre}
        onchange={(e) => {
          const nom = e.currentTarget.value.trim();
          if (!nom) {
            e.currentTarget.value = schema.titre;
            editeur.annonce = "Un schéma doit avoir un nom.";
          } else editeur.agir((s) => (s.titre = nom));
        }}
      />
    </div>
    <span
      class="sauvegarde {editeur.sauvegarde}"
      role="status"
      title={editeur.messageSauvegarde ?? "Le schéma s'enregistre seul dans le dossier « schemas » du coffre."}
    >
      {libelleSauvegarde}
    </span>
    <label class="vue">
      <span class="visuellement-cache">Vue</span>
      <select class="champ" value={schema.vue} onchange={(e) => editeur.changerVue(e.currentTarget.value as IdVue)}>
        {#each VUES as v (v.id)}<option value={v.id}>Vue {v.nom.toLowerCase()}</option>{/each}
      </select>
    </label>
    <div class="outils">
      <button type="button" class="bouton discret" title="Annuler (Ctrl+Z)" aria-label="Annuler" disabled={!editeur.peutAnnuler} onclick={() => editeur.annuler()}>
        <Undo2 size={16} aria-hidden="true" />
      </button>
      <button type="button" class="bouton discret" title="Rétablir (Ctrl+Y)" aria-label="Rétablir" disabled={!editeur.peutRetablir} onclick={() => editeur.retablir()}>
        <Redo2 size={16} aria-hidden="true" />
      </button>
      <span class="ancre-export">
        <button type="button" class="bouton" aria-expanded={typesCableOuvert} aria-haspopup="menu" title="Prendre un câble en main, puis cliquer les deux équipements" onclick={() => (typesCableOuvert = !typesCableOuvert)}>
          <Cable size={16} aria-hidden="true" /> <span class="libelle">Ajouter un lien</span>
        </button>
        {#if typesCableOuvert}
          <div class="menu-export menu-cables" role="menu" aria-label="Type de câble">
            {#each TYPES_CABLE as t (t.valeur)}
              {@const trait = traitDuSupport(t.valeur === "auto" ? "cuivre" : t.valeur)}
              <button type="button" role="menuitem" onclick={() => prendreCable(t.valeur)}>
                <svg width="28" height="10" aria-hidden="true">
                  <line x1="2" y1="5" x2="26" y2="5" stroke={trait.couleur} stroke-width={Math.min(trait.epaisseur, 4)} stroke-dasharray={t.valeur === "auto" ? "3 3" : trait.tirets} stroke-linecap="round" />
                </svg>
                {t.nom}
              </button>
            {/each}
            <hr />
            <button
              type="button"
              role="menuitem"
              title="Choisir les deux interfaces et les propriétés du lien dans une boîte (C sur un équipement choisi)"
              onclick={() => {
                typesCableOuvert = false;
                connexion = { source: schema.equipements.find((e) => editeur.selection.includes(e.id))?.id ?? null, destination: null };
              }}
            >
              Boîte de connexion détaillée…
            </button>
          </div>
        {/if}
      </span>
      <button type="button" class="bouton discret" aria-pressed={main} aria-label="Déplacer le plan" title="Déplacer le plan : glisser fait défiler (H, ou maintenir Espace)" onclick={() => (main = !main)}><Hand size={16} aria-hidden="true" /></button>
      <button type="button" class="bouton discret" aria-label="Zoom arrière" title="Zoom arrière (−, Ctrl+molette ou pincer)" onclick={() => canevas?.zoomer(0.8)}><ZoomOut size={16} aria-hidden="true" /></button>
      <button type="button" class="bouton discret niveau-zoom" aria-label="Zoom {Math.round(zoom * 100)} % : revenir à la taille réelle" title="Taille réelle (1)" onclick={() => canevas?.tailleReelle()}>{Math.round(zoom * 100)} %</button>
      <button type="button" class="bouton discret" aria-label="Zoom avant" title="Zoom avant (+, Ctrl+molette ou pincer)" onclick={() => canevas?.zoomer(1.25)}><ZoomIn size={16} aria-hidden="true" /></button>
      <button type="button" class="bouton discret" aria-label="Tout voir" title="Tout voir (0)" onclick={() => canevas?.toutVoir()}><Maximize size={16} aria-hidden="true" /></button>
      <button type="button" class="bouton discret" aria-label="Chercher dans le schéma" aria-pressed={rechercheOuverte} title="Chercher un équipement, une adresse, un port (Ctrl+F)" onclick={() => (rechercheOuverte = !rechercheOuverte)}><Search size={16} aria-hidden="true" /></button>
      <button type="button" class="bouton" title="Plan d'adressage IP du schéma" onclick={() => (planIpOuvert = true)}><Table2 size={16} aria-hidden="true" /> <span class="libelle">Plan IP</span></button>
      <button type="button" class="bouton discret" aria-pressed={miniCarte} aria-label="Mini-carte" title="Mini-carte" onclick={() => (miniCarte = !miniCarte)}><MapIcone size={16} aria-hidden="true" /></button>
      <span class="ancre-export">
        <button type="button" class="bouton discret" aria-expanded={affichageOuvert} aria-haspopup="true" aria-label="Affichage" title="Ce qui est affiché : ports, VLAN, adresses, noms, annotations, zones" onclick={() => (affichageOuvert = !affichageOuvert)}>
          <Eye size={16} aria-hidden="true" />
        </button>
        {#if affichageOuvert}
          {@const actuel = affichageDe(schema, vueDe(schema.vue))}
          <div class="menu-export menu-affichage" role="group" aria-label="Affichage">
            {#each AFFICHAGES as a (a.valeur)}
              <label>
                <input type="checkbox" checked={actuel[a.valeur]} onchange={(ev) => basculerAffichage(a.valeur, ev.currentTarget.checked)} />
                {a.nom}
              </label>
            {/each}
            {#if schema.affichage}
              <hr />
              <button type="button" onclick={() => editeur.agir((s) => delete s.affichage)}>Selon la vue</button>
            {/if}
          </div>
        {/if}
      </span>
      <button
        type="button"
        class="bouton discret"
        aria-pressed={schema.cablesCoudes === true}
        aria-label="Câbles coudés à angle droit"
        title="Câbles coudés à angle droit (pour tout le schéma ; un câble peut avoir le sien)"
        onclick={() => editeur.agir((s) => { if (s.cablesCoudes) delete s.cablesCoudes; else s.cablesCoudes = true; })}
      >
        <Spline size={16} aria-hidden="true" />
      </button>
      <button type="button" class="bouton discret" aria-pressed={grille} title="Grille et aimantation (Alt maintenu pendant un déplacement : au pixel près, sans grille ni aimantation)" aria-label="Grille et aimantation" onclick={() => (grille = !grille)}>
        <Grid3x3 size={16} aria-hidden="true" />
      </button>
      <span class="ancre-export">
        <button type="button" class="bouton" aria-expanded={exportOuvert} aria-haspopup="menu" title="Exporter (SVG, PNG, JSON)" onclick={() => (exportOuvert = !exportOuvert)}>
          <Download size={16} aria-hidden="true" /> <span class="libelle">Exporter</span>
        </button>
        {#if exportOuvert}
          <div class="menu-export" role="menu">
            <button type="button" role="menuitem" onclick={() => exporter("svg")}>Image SVG</button>
            <button type="button" role="menuitem" onclick={() => exporter("png")}>Image PNG</button>
            <button type="button" role="menuitem" onclick={() => exporter("json")}>Modèle complet (JSON)</button>
          </div>
        {/if}
      </span>
      <button type="button" class="bouton" title="Importer : remplacer le contenu par un fichier JSON exporté" onclick={importer}>
        <Upload size={16} aria-hidden="true" /> <span class="libelle">Importer…</span>
      </button>
      <button type="button" class="bouton principal" onclick={fermer}>
        <X size={16} aria-hidden="true" /> Fermer
      </button>
    </div>
  </header>

  {#if editeur.conflit}
    <div class="bandeau avertissement" role="alert">
      Ce schéma a été modifié ailleurs depuis son ouverture.
      <button type="button" class="bouton petit" onclick={() => editeur.garderMaVersion()}>Garder ma version</button>
      <button type="button" class="bouton petit" onclick={() => editeur.reprendreLeDisque()}>Reprendre l'autre version</button>
    </div>
  {:else if editeur.messageSauvegarde && editeur.sauvegarde !== "enregistre"}
    <div class="bandeau {editeur.sauvegarde === 'erreur' ? 'erreur' : 'info'}" role="status">
      {editeur.messageSauvegarde}
      {#if editeur.sauvegarde === "erreur"}<button type="button" class="bouton petit" onclick={() => { editeur.sauvegarde = "modifie"; void editeur.enregistrer(); }}>Réessayer</button>{/if}
    </div>
  {/if}

  {#if erreur}
    <p class="bandeau erreur" role="alert">{erreur}</p>
  {:else if !pret}
    <p class="chargement" aria-busy="true">Ouverture du schéma…</p>
  {:else}
    <div class="corps">
      <Bibliotheque onajouter={(genre) => ajouter(genre)} />
      <div class="plan">
        <Canevas
          bind:this={canevas}
          bind:grille
          bind:main
          bind:miniCarte
          bind:zoom
          {trouves}
          {editeur}
          onretirer={retirerSelection}
          oncopier={copierSelection}
          ondupliquer={dupliquerSelection}
          onconnecter={(source, destination) => (connexion = { source, destination: destination ?? null })}
          ondeposer={(genre, point) => ajouter(genre, point)}
        />
        {#if rechercheOuverte}
          <RechercheSchema
            {schema}
            bind:texte={texteRecherche}
            onchoisir={(t) => montrer(t.id)}
            onfermer={() => {
              rechercheOuverte = false;
              canevas?.focus();
            }}
          />
        {/if}
        {#if schema.equipements.length === 0}
          <p class="invite">Commencez par un équipement de la bibliothèque (clic, ou glisser sur le plan).</p>
        {/if}
        <p class="etat-diagnostic">
          {schema.equipements.length} équipement(s) · {schema.liens.length} lien(s)
          {#if nombreAvertissements > 0}· <button type="button" class="lien-texte" onclick={() => (editeur.selection = [])}>{nombreAvertissements} point(s) à vérifier</button>{/if}
        </p>
      </div>
      <PanneauProprietes {editeur} onconnecter={(source) => (connexion = { source, destination: null })} onmontrer={(x) => canevas?.montrer(x)} />
    </div>
  {/if}

  {#if editeur.annonce}
    <p class="annonce" role="status">{editeur.annonce}</p>
  {/if}

  {#if confirmationImport !== null}
    <div class="confirmation" role="alertdialog" aria-modal="true" aria-labelledby="titre-import">
      <h2 id="titre-import">Remplacer le contenu du schéma ?</h2>
      <p>Le fichier choisi remplacera tout le contenu de ce schéma (Ctrl+Z annule). Un fichier invalide est refusé sans rien modifier.</p>
      <div class="actions">
        <button type="button" class="bouton" onclick={() => (confirmationImport = null)}>Annuler</button>
        <button type="button" class="bouton principal" onclick={confirmerImport}>Remplacer</button>
      </div>
    </div>
  {/if}
</dialog>

{#if planIpOuvert}
  <PlanIp
    {schema}
    onfermer={() => (planIpOuvert = false)}
    onexporter={exporterPlanIp}
    onmontrer={(id) => {
      planIpOuvert = false;
      montrer(id);
    }}
  />
{/if}

{#if connexion}
  <DialogueConnexion
    {editeur}
    source={connexion.source}
    destination={connexion.destination}
    onfermer={(lien) => {
      connexion = null;
      if (lien) editeur.selection = [lien];
    }}
  />
{/if}



<style>
  .editeur-topologie {
    display: flex;
    flex-direction: column;
    width: calc(100vw - 32px);
    max-width: none;
    height: calc(100vh - 32px);
    max-height: none;
    padding: 0;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 16px 48px rgb(0 0 0 / 0.25);
    overflow: hidden;
  }

  .editeur-topologie:not([open]) {
    display: none;
  }

  .editeur-topologie::backdrop {
    background: var(--voile);
  }

  header {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px 12px;
    padding: 8px 14px;
    border-bottom: 1px solid var(--bordure);
  }

  .intitule {
    display: flex;
    align-items: center;
    gap: 10px;
    min-width: 0;
  }

  h2 {
    font-size: 16px;
    white-space: nowrap;
  }

  .champ.titre {
    width: 240px;
    height: 32px;
    font-weight: 600;
  }

  .vue .champ {
    width: auto;
    height: 32px;
  }

  .sauvegarde {
    padding: 2px 8px;
    border-radius: 999px;
    background: var(--surface-2);
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .sauvegarde.enregistre {
    background: var(--succes-fond);
    color: var(--succes);
  }

  .sauvegarde.erreur,
  .sauvegarde.conflit {
    background: var(--retard-fond);
    color: var(--retard);
  }

  /* Fenêtre étroite : ces boutons gardent leur icône (et leur nom pour les lecteurs d'écran). */
  @media (max-width: 1200px) {
    .outils .libelle {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
  }

  .outils {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 4px;
    margin-left: auto;
  }

  .outils .bouton {
    height: 32px;
    padding: 0 10px;
  }

  .bouton.discret {
    border-color: transparent;
    background: transparent;
  }

  .bouton.discret[aria-pressed="true"] {
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
  }

  .ancre-export {
    position: relative;
  }

  .menu-export {
    position: absolute;
    top: calc(100% + 4px);
    right: 0;
    z-index: 5;
    display: flex;
    flex-direction: column;
    min-width: 200px;
    padding: 4px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--surface);
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.15);
  }

  .menu-export button {
    padding: 7px 10px;
    border: none;
    border-radius: 4px;
    background: transparent;
    color: var(--texte);
    text-align: left;
    cursor: pointer;
  }

  .menu-export button:hover {
    background: var(--surface-2);
  }

  .menu-affichage {
    min-width: 250px;
  }

  .menu-affichage label {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 6px 10px;
    border-radius: 4px;
    cursor: pointer;
  }

  .menu-affichage label:hover {
    background: var(--surface-2);
  }

  .menu-affichage hr {
    width: 100%;
    margin: 4px 0;
    border: none;
    border-top: 1px solid var(--bordure);
  }

  .menu-cables {
    right: auto;
    left: 0;
    min-width: 260px;
  }

  .menu-cables button {
    display: flex;
    align-items: center;
    gap: 10px;
  }

  .menu-cables hr {
    width: 100%;
    margin: 4px 0;
    border: none;
    border-top: 1px solid var(--bordure);
  }

  .corps {
    display: grid;
    flex: 1;
    grid-template-columns: 230px minmax(0, 1fr) 340px;
    min-height: 0;
  }

  .plan {
    position: relative;
    min-width: 0;
    min-height: 0;
  }

  .invite {
    position: absolute;
    top: 40%;
    left: 50%;
    transform: translate(-50%, -50%);
    margin: 0;
    color: var(--texte-secondaire);
    pointer-events: none;
  }

  .niveau-zoom {
    min-width: 54px;
    justify-content: center;
    font-variant-numeric: tabular-nums;
    font-size: 12px;
  }

  .etat-diagnostic {
    position: absolute;
    bottom: 8px;
    left: 10px;
    margin: 0;
    padding: 2px 8px;
    border-radius: var(--rayon);
    background: var(--surface);
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .lien-texte {
    padding: 0;
    border: none;
    background: none;
    color: var(--avertissement);
    font-size: 12px;
    text-decoration: underline;
    cursor: pointer;
  }

  .bandeau {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
    margin: 0;
    padding: 6px 14px;
    font-size: 13px;
  }

  .bandeau.avertissement,
  .bandeau.info {
    background: var(--avertissement-fond);
    color: var(--avertissement);
  }

  .bandeau.erreur {
    background: var(--retard-fond);
    color: var(--retard);
  }

  .bouton.petit {
    height: 26px;
    padding: 0 10px;
    font-size: 12px;
  }

  .chargement {
    padding: 32px;
    color: var(--texte-secondaire);
  }

  .annonce {
    position: absolute;
    bottom: 16px;
    left: 50%;
    transform: translateX(-50%);
    max-width: 70%;
    margin: 0;
    padding: 8px 14px;
    border-radius: var(--rayon);
    background: var(--texte);
    color: var(--surface);
    font-size: 13px;
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.2);
  }

  .confirmation {
    position: fixed;
    inset: 0;
    z-index: 20;
    width: min(460px, calc(100vw - 32px));
    margin: auto;
    padding: 20px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 16px 48px rgb(0 0 0 / 0.3);
  }

  .confirmation h2 {
    margin-bottom: 8px;
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
  }
</style>
