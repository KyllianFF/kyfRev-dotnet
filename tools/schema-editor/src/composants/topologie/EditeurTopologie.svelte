<script lang="ts" module>
  import type { PressePapiers } from "../../lib/topologie/operations";

  // Presse-papiers des schémas (D42) : il survit à la fermeture de l'éditeur,
  // pour coller dans un autre schéma.
  let pressePapiers: PressePapiers | null = null;
  let collages = 0;
</script>

<script lang="ts">
  import ArrowLeft from "@lucide/svelte/icons/arrow-left";
  import ChevronDown from "@lucide/svelte/icons/chevron-down";
  import Grid3x3 from "@lucide/svelte/icons/grid-3x3";
  import Eye from "@lucide/svelte/icons/eye";
  import Hand from "@lucide/svelte/icons/hand";
  import Search from "@lucide/svelte/icons/search";
  import MapIcone from "@lucide/svelte/icons/map";
  import Maximize from "@lucide/svelte/icons/maximize";
  import Redo2 from "@lucide/svelte/icons/redo-2";
  import Undo2 from "@lucide/svelte/icons/undo-2";
  import ZoomIn from "@lucide/svelte/icons/zoom-in";
  import ZoomOut from "@lucide/svelte/icons/zoom-out";
  import { onMount } from "svelte";
  import { api, messageErreur } from "../../lib/api";
  import { inscrireEditeur } from "../../lib/fin";
  import { definition } from "../../lib/topologie/catalogue";
  import { affichageDe, COTE, traitDuSupport } from "../../lib/topologie/dessin";
  import { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import {
    AFFICHAGES,
    type Affichage,
    GENRES_RELATION,
    type GenreRelation,
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
  import {
    effacer,
    epurer,
    lireMode,
    MODES,
    type ModeEdition,
    type Outil,
    retenirMode,
  } from "../../lib/topologie/simplifie";
  import { estVide, exporterSchema, type FormatExport, imprimerSchema } from "../../lib/topologie/sorties";
  import { VUES, vue as vueDe } from "../../lib/topologie/vues";
  import Canevas from "./Canevas.svelte";
  import { GABARITS } from "../../lib/topologie/gabarits";
  import { chercherDansSchema } from "../../lib/topologie/recherche";
  import DialogueConnexion from "./DialogueConnexion.svelte";
  import DialogueExport from "./DialogueExport.svelte";
  import PlanIp from "./PlanIp.svelte";
  import Plateau from "./Plateau.svelte";
  import RechercheSchema from "./RechercheSchema.svelte";
  import PanneauProprietes from "./PanneauProprietes.svelte";

  // Éditeur de schémas réseau (D39), en plein écran au-dessus de la fiche.
  // Le schéma vit dans schemas/<id>.json : il s'enregistre seul, et la fiche
  // n'en garde qu'une référence (rien à y écrire pour le modifier).
  //
  // Deux modes de création (11A, 12) sur le même modèle : simplifié (poser, relier, nommer) et avancé
  // (interfaces, VLAN, vues, diagnostic). Passer de l'un à l'autre ne change rien au schéma.
  let {
    id = null,
    ancien = null,
    retour = "Fermer",
    onconverti,
    onfermer,
  }: {
    /** Schéma du coffre à ouvrir. */
    id?: string | null;
    /** Schéma au format 1 (bloc ```reseau de la fiche), à convertir. */
    ancien?: string | null;
    /** Où mène le bouton de retour : « Schémas » depuis la liste, « Fermer » quand on vient d'ailleurs. */
    retour?: string;
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
  /** Le menu ouvert de la barre d'outils, un seul à la fois. */
  let menu = $state<"cables" | "relations" | "affichage" | "plus" | null>(null);

  // --- Mode de création, outil en cours, tuile en attente ---

  let mode = $state<ModeEdition>(lireMode());
  const simplifie = $derived(mode === "simplifie");
  let outil = $state<Outil>("choisir");
  /** Tuile du plateau cliquée : le prochain clic sur le plan la pose. */
  let pose = $state<string | null>(null);
  let genreRelation = $state<GenreRelation>("dependance");
  /** Fenêtre étroite, mode avancé : le volet des propriétés s'ouvre par-dessus le plan. */
  let voletOuvert = $state(false);

  /** Change de mode : le schéma et la sélection restent, l'outil en cours se repose. */
  function changerMode(nouveau: ModeEdition) {
    if (nouveau === mode) return;
    mode = nouveau;
    retenirMode(nouveau);
    reposer();
    main = false;
    voletOuvert = nouveau === "avance" && editeur.selection.length > 0;
    editeur.annonce =
      nouveau === "simplifie"
        ? "Mode simplifié : poser, relier, nommer. Le schéma n'a pas changé."
        : "Mode avancé : interfaces, VLAN, vues et diagnostic. Le schéma n'a pas changé.";
  }

  /** Repose tout outil : câble en main, tuile en attente, menus. */
  function reposer() {
    outil = "choisir";
    pose = null;
    editeur.cable = null;
    menu = null;
  }

  function prendreOutil(nouveau: Outil) {
    reposer();
    outil = nouveau;
    if (nouveau !== "choisir") editeur.selection = [];
  }

  const OUTILS_SIMPLES: { valeur: Outil; nom: string; aide: string }[] = [
    { valeur: "choisir", nom: "Choisir", aide: "Choisir, déplacer, régler" },
    { valeur: "cable", nom: "Câble", aide: "Tirer un câble : cliquer un équipement, puis un autre" },
    { valeur: "texte", nom: "Texte", aide: "Écrire un texte libre sur le plan" },
    { valeur: "zone", nom: "Zone", aide: "Dessiner une zone : cliquer, ou tirer un rectangle" },
    { valeur: "effacer", nom: "Effacer", aide: "Effacer ce sur quoi on clique (Ctrl+Z annule)" },
  ];

  /** Tracé des câbles du schéma : droits, ou à angle droit. Le réglage est celui du schéma, pas du mode. */
  function reglerCoudes(coudes: boolean) {
    if ((editeur.schema.cablesCoudes === true) === coudes) return;
    editeur.agir(
      (s) => {
        if (coudes) s.cablesCoudes = true;
        else delete s.cablesCoudes;
      },
      coudes ? "Câbles à angle droit." : "Câbles droits.",
    );
  }

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
      const octets = new TextEncoder().encode(`﻿${csv}`);
      const chemin = await api.exporterSchema(dossier, `${schema.titre || "Schéma réseau"} - plan IP.csv`, octets);
      editeur.annonce = `Plan IP exporté : ${chemin}`;
    } catch (err) {
      editeur.annonce = `Export impossible : ${messageErreur(err)}`;
    }
  }

  /** Câbles proposés à l'outil Câble du mode avancé (comme la boîte des simulateurs). */
  const TYPES_CABLE: { valeur: Support | "auto"; nom: string }[] = [
    { valeur: "auto", nom: "Automatique (d'après les ports)" },
    ...SUPPORTS.filter((x) => x.valeur !== "personnalise"),
  ];

  function prendreCable(support: Support | "auto") {
    reposer();
    editeur.selection = [];
    editeur.cable = { support, depart: null };
  }

  function prendreRelation(genre: GenreRelation) {
    prendreOutil("relation");
    genreRelation = genre;
  }

  /** Équipements de la sélection, hors verrouillés quand on les retire. */
  const equipementsChoisis = () =>
    new Set(editeur.selection.filter((x) => schema.equipements.some((e) => e.id === x)));

  function retirerSelection() {
    if (editeur.selection.length === 0) return;
    const choisis = new Set(editeur.selection);
    editeur.agir((s) => {
      // Mode simplifié : un port libéré ne garde pas le trunk posé par l'interrupteur de la bulle.
      if (simplifie) {
        effacer(s, choisis);
        return;
      }
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
    canevas?.focus();
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
        // Le plan prend la main : les raccourcis du clavier valent tout de suite.
        requestAnimationFrame(() => canevas?.focus());
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
    // Posé depuis le plateau au clavier : on dit ce qui est arrivé, et où.
    if (nouveau && !ou && genre.startsWith("type:")) {
      const nom = schema.equipements.find((e) => e.id === nouveau)?.nom ?? "";
      editeur.annonce = `${definition(genre.slice(5)).nom} posé au centre du plan : ${nom}.`;
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
    } else if (evenement.key === "Escape" && (menu || pose || outil !== "choisir")) {
      // Échap repose d'abord ce qu'on tenait : un menu, une tuile, un outil.
      evenement.preventDefault();
      if (menu) menu = null;
      else reposer();
    } else if (evenement.key === "Escape" && editeur.selection.length > 0) {
      evenement.preventDefault();
      editeur.selection = [];
    } else if (evenement.key.startsWith("Arrow") && editeur.selection.length > 0) {
      evenement.preventDefault();
      const choisis = new Set(editeur.selection);
      // Des textes et des zones seuls : petits pas, Maj pour les grands. Avec des équipements : la grille, Maj au pixel.
      const libres = schema.annotations.filter((n) => choisis.has(n.id)).length === choisis.size;
      const pas = libres ? (evenement.shiftKey ? 50 : 10) : evenement.shiftKey ? 1 : 20;
      const dx = evenement.key === "ArrowLeft" ? -pas : evenement.key === "ArrowRight" ? pas : 0;
      const dy = evenement.key === "ArrowUp" ? -pas : evenement.key === "ArrowDown" ? pas : 0;
      editeur.agir((s) => {
        for (const e of s.equipements)
          if (choisis.has(e.id) && !e.verrou) e.position = { x: e.position.x + dx, y: e.position.y + dy };
        for (const n of s.annotations) {
          if (!choisis.has(n.id)) continue;
          n.position = { x: n.position.x + dx, y: n.position.y + dy };
          if (n.vers) n.vers = { x: n.vers.x + dx, y: n.vers.y + dy };
        }
      });
    } else if (
      !simplifie &&
      evenement.key.toLowerCase() === "c" &&
      !ctrl &&
      editeur.selection.length === 1 &&
      schema.equipements.some((e) => e.id === editeur.selection[0])
    ) {
      evenement.preventDefault();
      connexion = { source: editeur.selection[0] ?? null, destination: null };
    }
  }

  /** Ce qui sort (image, impression) est ce qu'on voit : en mode simplifié, la vue simplifiée, sans ports ni VLAN. */
  function sortie(image: boolean) {
    const instantane = $state.snapshot(schema);
    return simplifie && image
      ? { schema: epurer(instantane), vue: vueDe("simplifiee") }
      : { schema: instantane, vue: undefined };
  }

  /** Exporte le schéma tel qu'il est à l'écran (SVG et PNG : couleurs claires, légende). */
  async function exporter(format: FormatExport) {
    try {
      const { schema: aExporter, vue } = sortie(format !== "json");
      const chemin = await exporterSchema(aExporter, format, vue);
      if (chemin) editeur.annonce = `Exporté : ${chemin}`;
    } catch (e) {
      editeur.annonce = `Export impossible : ${messageErreur(e)}`;
    }
  }

  async function imprimer() {
    menu = null;
    if (estVide(schema)) {
      editeur.annonce = "Ce schéma est vide : rien à imprimer.";
      return;
    }
    try {
      const { schema: aImprimer, vue } = sortie(true);
      await imprimerSchema(aImprimer, vue);
    } catch (e) {
      editeur.annonce = `Impression impossible : ${messageErreur(e)}`;
    }
  }

  async function importer() {
    menu = null;
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
    if (menu || pose || outil !== "choisir") {
      if (menu) menu = null;
      else reposer();
      return;
    }
    if (connexion || confirmationImport || exportOuvert) return;
    void fermer();
  }}
  onclose={onfermer}
  onkeydown={clavier}
>
  <header class="entete">
    <button type="button" class="outil retour" title="Enregistrer et quitter l'éditeur" onclick={fermer}>
      <ArrowLeft size={18} aria-hidden="true" /><span class="nom-retour">{retour}</span>
    </button>
    <h2 id="titre-topologie" class="visuellement-cache">Schéma réseau</h2>
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
    <div class="segments mode" role="group" aria-label="Mode de création">
      {#each MODES as m (m.valeur)}
        <button type="button" aria-pressed={mode === m.valeur} data-mode={m.valeur} onclick={() => changerMode(m.valeur)}>
          {m.nom}
        </button>
      {/each}
    </div>
    <span class="ressort"></span>
    <span
      class="sauvegarde {editeur.sauvegarde}"
      role="status"
      title={editeur.messageSauvegarde ?? "Le schéma s'enregistre seul dans le dossier « schemas » du coffre."}
    >
      {libelleSauvegarde}
    </span>
    <button type="button" class="outil large" title="Annuler (Ctrl+Z)" aria-label="Annuler" disabled={!editeur.peutAnnuler} onclick={() => editeur.annuler()}>
      <span class="icone-etroite"><Undo2 size={16} aria-hidden="true" /></span><span class="nom-large">Annuler</span>
    </button>
    <button type="button" class="outil large" title="Rétablir (Ctrl+Y)" aria-label="Rétablir" disabled={!editeur.peutRetablir} onclick={() => editeur.retablir()}>
      <span class="icone-etroite"><Redo2 size={16} aria-hidden="true" /></span><span class="nom-large">Rétablir</span>
    </button>
    {#if !simplifie}
      <button type="button" class="outil cadre large tres-large" title="Importer : remplacer le contenu par un fichier JSON exporté" onclick={importer}>
        Importer…
      </button>
    {/if}
    <button type="button" class="outil cadre large" title="Exporter : image PNG ou SVG, ou modèle complet" onclick={() => (exportOuvert = true)}>
      Exporter…
    </button>
    <button type="button" class="outil cadre large" title="Imprimer le schéma seul, ajusté à la page" onclick={imprimer}>
      Imprimer…
    </button>
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
    <nav class="outils" class:avance={!simplifie} aria-label="Outils">
      {#if simplifie}
        {#each OUTILS_SIMPLES as o (o.valeur)}
          <button
            type="button"
            class="outil"
            class:large={o.valeur === "zone" || o.valeur === "effacer"}
            aria-pressed={outil === o.valeur}
            data-outil={o.valeur}
            title={o.aide}
            onclick={() => prendreOutil(o.valeur)}
          >
            {o.nom}
          </button>
        {/each}
      {:else}
        <button type="button" class="outil" aria-pressed={outil === "choisir" && !editeur.cable} data-outil="choisir" title="Choisir, déplacer, régler" onclick={() => prendreOutil("choisir")}>
          Choisir
        </button>
        <span class="ancre">
          <button type="button" class="outil" aria-pressed={editeur.cable !== null} aria-expanded={menu === "cables"} aria-haspopup="menu" data-outil="cable" title="Prendre un câble en main, puis cliquer les deux équipements et choisir leurs ports" onclick={() => (menu = menu === "cables" ? null : "cables")}>
            Câble <ChevronDown size={14} aria-hidden="true" />
          </button>
          {#if menu === "cables"}
            <div class="menu menu-cables" role="menu" aria-label="Type de câble">
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
                  menu = null;
                  connexion = { source: schema.equipements.find((e) => editeur.selection.includes(e.id))?.id ?? null, destination: null };
                }}
              >
                Boîte de connexion détaillée…
              </button>
            </div>
          {/if}
        </span>
        <span class="ancre">
          <button type="button" class="outil" aria-pressed={outil === "relation"} aria-expanded={menu === "relations"} aria-haspopup="menu" data-outil="relation" title="Tracer une relation logique entre deux équipements (elle n'occupe aucun port)" onclick={() => (menu = menu === "relations" ? null : "relations")}>
            Relation <ChevronDown size={14} aria-hidden="true" />
          </button>
          {#if menu === "relations"}
            <div class="menu" role="menu" aria-label="Genre de relation">
              {#each GENRES_RELATION as g (g.valeur)}
                <button type="button" role="menuitem" onclick={() => prendreRelation(g.valeur)}>{g.nom}</button>
              {/each}
            </div>
          {/if}
        </span>
        <button type="button" class="outil" aria-pressed={outil === "texte"} data-outil="texte" title="Écrire un texte libre sur le plan" onclick={() => prendreOutil("texte")}>
          Texte
        </button>
        <button type="button" class="outil" aria-pressed={outil === "zone"} data-outil="zone" title="Dessiner une zone : cliquer, ou tirer un rectangle" onclick={() => prendreOutil("zone")}>
          Zone
        </button>
        <span class="groupe separe">
          <span class="etiquette large" id="etiquette-vue">Vue</span>
          <div class="segments vues" role="group" aria-labelledby="etiquette-vue">
            {#each VUES as v (v.id)}
              <button type="button" aria-pressed={schema.vue === v.id} onclick={() => editeur.changerVue(v.id)}>{v.nom}</button>
            {/each}
          </div>
          <select class="champ vue-liste" aria-label="Vue" value={schema.vue} onchange={(e) => editeur.changerVue(e.currentTarget.value as IdVue)}>
            {#each VUES as v (v.id)}<option value={v.id}>{v.nom}</option>{/each}
          </select>
        </span>
        <button type="button" class="outil large" title="Plan d'adressage IP du schéma" onclick={() => (planIpOuvert = true)}>Plan IP</button>
      {/if}
      <span class="groupe separe large">
        <span class="etiquette" id="etiquette-cables">Câbles</span>
        <div class="segments" role="group" aria-labelledby="etiquette-cables">
          <button type="button" aria-pressed={schema.cablesCoudes !== true} title="Câbles droits, pour tout le schéma" onclick={() => reglerCoudes(false)}>Droits</button>
          <button type="button" aria-pressed={schema.cablesCoudes === true} title="Câbles coudés à angle droit, pour tout le schéma" onclick={() => reglerCoudes(true)}>À angle</button>
        </div>
      </span>
      <span class="ressort"></span>
      {#if simplifie}
        <div class="zoom large" role="group" aria-label="Zoom">
          <button type="button" class="outil carre" aria-label="Réduire" title="Zoom arrière (−, Ctrl+molette ou pincer)" onclick={() => canevas?.zoomer(0.8)}>−</button>
          <button type="button" class="outil niveau" aria-label="Zoom {Math.round(zoom * 100)} % : revenir à la taille réelle" title="Taille réelle (1)" onclick={() => canevas?.tailleReelle()}>{Math.round(zoom * 100)} %</button>
          <button type="button" class="outil carre" aria-label="Agrandir" title="Zoom avant (+, Ctrl+molette ou pincer)" onclick={() => canevas?.zoomer(1.25)}>+</button>
          <button type="button" class="outil" title="Tout voir (0)" onclick={() => canevas?.toutVoir()}>Ajuster</button>
        </div>
      {:else}
        <button
          type="button"
          class="outil"
          class:alerte={nombreAvertissements > 0}
          title="Diagnostic du schéma : les points à vérifier, dans le volet"
          onclick={() => {
            editeur.selection = [];
            voletOuvert = true;
          }}
        >
          Diagnostic · {nombreAvertissements}
        </button>
        <button type="button" class="outil cadre bouton-volet" aria-pressed={voletOuvert} title="Propriétés de l'objet choisi, VLAN et diagnostic du schéma" onclick={() => (voletOuvert = !voletOuvert)}>
          Propriétés
        </button>
      {/if}
      <span class="ancre plus" class:avance={!simplifie}>
        <button type="button" class="outil" aria-expanded={menu === "plus"} aria-haspopup="menu" title="Les autres outils et actions" onclick={() => (menu = menu === "plus" ? null : "plus")}>
          Plus
        </button>
        {#if menu === "plus"}
          <div class="menu a-droite" role="menu" aria-label="Autres outils et actions">
            {#if simplifie}
              <button type="button" role="menuitemradio" aria-checked={outil === "zone"} onclick={() => prendreOutil("zone")}>Zone</button>
              <button type="button" role="menuitemradio" aria-checked={outil === "effacer"} onclick={() => prendreOutil("effacer")}>Effacer</button>
              <hr />
            {/if}
            <button type="button" role="menuitemradio" aria-checked={schema.cablesCoudes !== true} onclick={() => { menu = null; reglerCoudes(false); }}>Câbles droits</button>
            <button type="button" role="menuitemradio" aria-checked={schema.cablesCoudes === true} onclick={() => { menu = null; reglerCoudes(true); }}>Câbles à angle</button>
            <hr />
            {#if simplifie}
              <button type="button" role="menuitem" onclick={() => canevas?.zoomer(0.8)}>Réduire</button>
              <button type="button" role="menuitem" onclick={() => canevas?.zoomer(1.25)}>Agrandir</button>
              <button type="button" role="menuitem" onclick={() => { menu = null; canevas?.toutVoir(); }}>Ajuster</button>
              <hr />
            {/if}
            <button type="button" role="menuitem" disabled={!editeur.peutAnnuler} onclick={() => editeur.annuler()}>Annuler</button>
            <button type="button" role="menuitem" disabled={!editeur.peutRetablir} onclick={() => editeur.retablir()}>Rétablir</button>
            <hr />
            {#if !simplifie}
              <button type="button" role="menuitem" onclick={() => { menu = null; planIpOuvert = true; }}>Plan IP</button>
              <button type="button" role="menuitem" onclick={importer}>Importer…</button>
            {/if}
            <button type="button" role="menuitem" onclick={() => { menu = null; exportOuvert = true; }}>Exporter…</button>
            <button type="button" role="menuitem" onclick={imprimer}>Imprimer…</button>
          </div>
        {/if}
      </span>
    </nav>

    <div class="corps" class:avance={!simplifie}>
      <div class="plan" class:avance={!simplifie}>
        <Canevas
          bind:this={canevas}
          bind:grille
          bind:main
          bind:miniCarte
          bind:zoom
          bind:outil
          bind:pose
          {simplifie}
          {genreRelation}
          {trouves}
          {editeur}
          onretirer={retirerSelection}
          oncopier={copierSelection}
          ondupliquer={dupliquerSelection}
          onconnecter={(source, destination) => (connexion = { source, destination: destination ?? null })}
          ondeposer={(genre, point) => ajouter(genre, point)}
          onavance={() => changerMode("avance")}
        />
        {#if !simplifie}
          <!-- Mode avancé : l'affichage du plan, sur le plan (la barre d'outils garde les outils et les vues). -->
          <div class="pied">
          <div class="commandes" role="toolbar" aria-label="Affichage du plan">
            <button type="button" class="outil carre" aria-label="Chercher dans le schéma" aria-pressed={rechercheOuverte} title="Chercher un équipement, une adresse, un port (Ctrl+F)" onclick={() => (rechercheOuverte = !rechercheOuverte)}><Search size={16} aria-hidden="true" /></button>
            <button type="button" class="outil carre" aria-pressed={main} aria-label="Déplacer le plan" title="Déplacer le plan : glisser fait défiler (H, ou maintenir Espace)" onclick={() => (main = !main)}><Hand size={16} aria-hidden="true" /></button>
            <button type="button" class="outil carre" aria-label="Zoom arrière" title="Zoom arrière (−, Ctrl+molette ou pincer)" onclick={() => canevas?.zoomer(0.8)}><ZoomOut size={16} aria-hidden="true" /></button>
            <button type="button" class="outil niveau" aria-label="Zoom {Math.round(zoom * 100)} % : revenir à la taille réelle" title="Taille réelle (1)" onclick={() => canevas?.tailleReelle()}>{Math.round(zoom * 100)} %</button>
            <button type="button" class="outil carre" aria-label="Zoom avant" title="Zoom avant (+, Ctrl+molette ou pincer)" onclick={() => canevas?.zoomer(1.25)}><ZoomIn size={16} aria-hidden="true" /></button>
            <button type="button" class="outil carre" aria-label="Tout voir" title="Tout voir (0)" onclick={() => canevas?.toutVoir()}><Maximize size={16} aria-hidden="true" /></button>
            <span class="ancre">
              <button type="button" class="outil carre" aria-expanded={menu === "affichage"} aria-haspopup="true" aria-label="Affichage" title="Ce qui est affiché : ports, VLAN, adresses, noms, annotations, zones" onclick={() => (menu = menu === "affichage" ? null : "affichage")}>
                <Eye size={16} aria-hidden="true" />
              </button>
              {#if menu === "affichage"}
                {@const actuel = affichageDe(schema, vueDe(schema.vue))}
                <div class="menu menu-affichage" role="group" aria-label="Affichage">
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
            <button type="button" class="outil carre" aria-pressed={miniCarte} aria-label="Mini-carte" title="Mini-carte" onclick={() => (miniCarte = !miniCarte)}><MapIcone size={16} aria-hidden="true" /></button>
            <button type="button" class="outil carre" aria-pressed={grille} title="Grille et aimantation (Alt maintenu pendant un déplacement : au pixel près, sans grille ni aimantation)" aria-label="Grille et aimantation" onclick={() => (grille = !grille)}><Grid3x3 size={16} aria-hidden="true" /></button>
          </div>
          <p class="etat-diagnostic">
            {schema.equipements.length} équipement(s) · {schema.liens.length} lien(s)
            {#if nombreAvertissements > 0}· <button type="button" class="lien-texte" onclick={() => { editeur.selection = []; voletOuvert = true; }}>{nombreAvertissements} point(s) à vérifier</button>{/if}
          </p>
          </div>
        {/if}
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
        {#if schema.equipements.length === 0 && !pose}
          <p class="invite">Commencez par un équipement du plateau, en bas : glissez-le ici, ou cliquez-le puis cliquez sur le plan.</p>
        {/if}
      </div>
      {#if !simplifie}
        <div class="volet" class:ouvert={voletOuvert}>
          <PanneauProprietes {editeur} onconnecter={(source) => (connexion = { source, destination: null })} onmontrer={(x) => canevas?.montrer(x)} />
        </div>
      {/if}
    </div>

    <Plateau avance={!simplifie} bind:pose onposer={(genre) => ajouter(genre)} />
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

{#if exportOuvert}
  <DialogueExport
    titre={schema.titre}
    vide={estVide(schema)}
    onvalider={(format) => void exporter(format)}
    onfermer={() => (exportOuvert = false)}
  />
{/if}

<style>
  .editeur-topologie {
    /* Bord des boutons et des champs, plus marqué que les filets : tiré des couleurs du thème. */
    --trait-fort: color-mix(in srgb, var(--texte-secondaire) 75%, var(--surface));
    display: flex;
    flex-direction: column;
    width: 100vw;
    max-width: none;
    height: 100vh;
    max-height: none;
    margin: 0;
    padding: 0;
    border: none;
    border-radius: 0;
    background: var(--fond);
    color: var(--texte);
    line-height: 20px;
    overflow: hidden;
  }

  .editeur-topologie:not([open]) {
    display: none;
  }

  .editeur-topologie::backdrop {
    background: var(--voile);
  }

  .entete,
  .outils {
    display: flex;
    flex: none;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
    padding: 8px 14px;
    border-bottom: 1px solid var(--bordure);
    background: var(--surface);
  }

  .ressort {
    flex: 1;
  }

  /* Boutons de l'en-tête et de la barre d'outils (11A) : discrets, encadrés quand ils sont actifs. */
  .outil {
    display: inline-flex;
    flex: none;
    align-items: center;
    justify-content: center;
    gap: 6px;
    min-height: 36px;
    padding: 0 12px;
    border: 1px solid transparent;
    border-radius: 10px;
    background: transparent;
    color: var(--texte);
    white-space: nowrap;
    transition: background var(--duree);
  }

  .outil:hover:not(:disabled) {
    background: var(--surface-2);
  }

  .outil:disabled {
    opacity: 0.5;
  }

  .outil.cadre {
    border-color: var(--trait-fort);
    background: var(--surface);
  }

  .outil[aria-pressed="true"] {
    border-color: var(--principale);
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
    font-weight: 600;
  }

  .outil.carre {
    width: 36px;
    padding: 0;
  }

  .outil.alerte {
    color: var(--avertissement);
  }

  .retour {
    padding: 0 10px;
  }

  .champ.titre {
    flex: 0 1 auto;
    width: auto;
    min-width: 90px;
    max-width: 340px;
    height: 36px;
    padding: 0 6px;
    border-color: transparent;
    background: transparent;
    font-size: 18px;
    font-weight: 700;
    field-sizing: content;
    text-overflow: ellipsis;
  }

  .champ.titre:hover,
  .champ.titre:focus-visible {
    border-color: var(--trait-fort);
    background: var(--surface);
  }

  /* Groupe de deux boutons ou plus dont un seul est actif (mode, tracé des câbles, vue). */
  .segments {
    display: flex;
    flex: none;
    border: 1px solid var(--trait-fort);
    border-radius: 10px;
    overflow: hidden;
  }

  .segments button {
    min-height: 34px;
    padding: 0 14px;
    border: none;
    background: var(--surface);
    color: var(--texte);
    white-space: nowrap;
  }

  .segments button:hover {
    background: var(--surface-2);
  }

  .segments button[aria-pressed="true"] {
    background: var(--principale);
    color: var(--sur-principale);
    font-weight: 600;
  }

  .segments button:focus-visible {
    outline-offset: -3px;
  }

  .etiquette,
  .sauvegarde {
    color: var(--texte-secondaire);
    font-size: 12px;
    white-space: nowrap;
  }

  .sauvegarde.erreur,
  .sauvegarde.conflit {
    padding: 2px 8px;
    border-radius: 999px;
    background: var(--retard-fond);
    color: var(--retard);
  }

  /* Le filet qui sépare un groupe de ce qui le précède : il passe à la ligne avec lui. */
  .groupe.separe::before {
    flex: none;
    width: 1px;
    height: 24px;
    margin: 0 4px;
    background: var(--bordure);
    content: "";
  }

  .zoom {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .niveau {
    min-width: 54px;
    padding: 0 4px;
    color: var(--texte-secondaire);
    font-size: 12px;
    font-variant-numeric: tabular-nums;
  }

  .icone-etroite {
    display: none;
  }

  .vue-liste {
    display: none;
    flex: none;
    width: auto;
    border-color: var(--trait-fort);
    border-radius: 10px;
  }

  .ancre {
    position: relative;
    display: inline-flex;
    flex: none;
  }

  /* Un libellé et sa commande : ils passent à la ligne ensemble. */
  .groupe {
    display: inline-flex;
    flex: none;
    align-items: center;
    gap: 8px;
  }

  /* « Plus » et « Propriétés » : pour les fenêtres étroites seulement. */
  .ancre.plus,
  .bouton-volet {
    display: none;
  }

  .menu {
    position: absolute;
    top: calc(100% + 4px);
    left: 0;
    z-index: 8;
    display: flex;
    flex-direction: column;
    min-width: 220px;
    max-height: min(calc(100vh - 116px), 520px);
    padding: 4px;
    border: 1px solid var(--trait-fort);
    border-radius: 12px;
    background: var(--surface);
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.16);
    overflow-y: auto;
  }

  .menu.a-droite {
    right: 0;
    left: auto;
  }

  .menu button {
    display: flex;
    align-items: center;
    gap: 10px;
    flex: none;
    min-height: 34px;
    padding: 0 10px;
    border: none;
    border-radius: 8px;
    background: transparent;
    color: var(--texte);
    text-align: left;
  }

  .menu button:hover:not(:disabled) {
    background: var(--surface-2);
  }

  .menu button:disabled {
    opacity: 0.5;
  }

  .menu button[aria-checked="true"] {
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
    font-weight: 600;
  }

  .menu hr {
    width: 100%;
    margin: 4px 0;
    border: none;
    border-top: 1px solid var(--bordure);
  }

  .menu-cables {
    min-width: 270px;
  }

  .menu-affichage {
    min-width: 250px;
  }

  .menu-affichage label {
    display: flex;
    align-items: center;
    gap: 8px;
    min-height: 34px;
    padding: 0 10px;
    border-radius: 8px;
    cursor: pointer;
  }

  .menu-affichage label:hover {
    background: var(--surface-2);
  }

  .corps {
    display: grid;
    flex: 1;
    grid-template-rows: minmax(0, 1fr);
    grid-template-columns: minmax(0, 1fr);
    min-height: 0;
    position: relative;
  }

  .corps.avance {
    grid-template-columns: minmax(0, 1fr) 340px;
  }

  .plan {
    position: relative;
    min-width: 0;
    min-height: 0;
    overflow: hidden;
  }

  .volet {
    display: grid;
    grid-template-rows: minmax(0, 1fr);
    min-height: 0;
    overflow: hidden;
  }

  /* Mode avancé : l'affichage du plan et l'état du schéma, en bas à gauche du plan. */
  .pied {
    position: absolute;
    bottom: 8px;
    left: 8px;
    z-index: 3;
    display: flex;
    flex-wrap: wrap-reverse;
    align-items: center;
    gap: 6px 10px;
    max-width: calc(100% - 16px);
    pointer-events: none;
  }

  .pied > :global(*) {
    pointer-events: auto;
  }

  .commandes {
    display: flex;
    flex-wrap: wrap;
    padding: 3px;
    border: 1px solid var(--bordure);
    border-radius: 12px;
    background: var(--surface);
    box-shadow: 0 2px 8px rgb(0 0 0 / 0.12);
  }

  /* Leur menu s'ouvre vers le haut. */
  .commandes .menu {
    top: auto;
    bottom: calc(100% + 6px);
  }

  .commandes .outil {
    min-height: 32px;
  }

  .commandes .outil.carre {
    width: 32px;
  }

  .invite {
    position: absolute;
    top: 40%;
    left: 50%;
    width: min(420px, calc(100% - 32px));
    transform: translate(-50%, -50%);
    margin: 0;
    color: var(--texte-secondaire);
    text-align: center;
    pointer-events: none;
  }

  .etat-diagnostic {
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

  /* Au-dessus du plateau, sur le bas du plan. */
  .annonce {
    position: absolute;
    bottom: 158px;
    left: 50%;
    z-index: 9;
    width: max-content;
    max-width: min(70%, calc(100% - 32px));
    transform: translateX(-50%);
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
    height: fit-content;
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

  /* Les cinq vues tiennent côte à côte dans une fenêtre large ; sinon, une liste. */
  @media (max-width: 1400px) {
    .segments.vues {
      display: none;
    }

    .vue-liste {
      display: block;
    }
  }

  /* Fenêtre moyenne : Annuler et Rétablir gardent leur icône (et leur nom pour les lecteurs d'écran) ;
     en mode avancé, « Importer… » rejoint « Plus ». */
  @media (max-width: 1000px) {
    .nom-large {
      display: none;
    }

    .icone-etroite {
      display: inline-flex;
    }

    .tres-large {
      display: none;
    }

    .ancre.plus.avance {
      display: inline-flex;
    }
  }

  /* Mode avancé, fenêtre moyenne : le volet des propriétés s'ouvre par-dessus le plan. */
  @media (max-width: 860px) {
    .corps.avance {
      grid-template-columns: minmax(0, 1fr);
    }

    .bouton-volet {
      display: inline-flex;
    }

    .volet {
      position: absolute;
      top: 0;
      right: 0;
      bottom: 0;
      z-index: 6;
      display: none;
      width: min(340px, 100%);
      box-shadow: -8px 0 24px rgb(0 0 0 / 0.16);
    }

    .volet.ouvert {
      display: grid;
    }
  }

  /* Fenêtre étroite (11D) : l'en-tête garde le retour, le titre et le mode ; le reste passe sous « Plus ». */
  @media (max-width: 620px) {
    .entete,
    .outils {
      padding: 8px 10px;
    }

    /* La mini-carte prendrait la place des commandes du plan. */
    .plan :global(.mini-carte) {
      display: none;
    }

    .large {
      display: none;
    }

    .ancre.plus {
      display: inline-flex;
    }

    .nom-retour {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }

    .sauvegarde.enregistre,
    .sauvegarde.modifie,
    .sauvegarde.enregistrement {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
    }

    .champ.titre {
      flex: 1 1 0;
      min-width: 60px;
    }

    /* Le titre prend la place ; « Plus » suit les trois outils gardés (11D). */
    .ressort {
      display: none;
    }

    .annonce {
      max-width: calc(100% - 24px);
    }
  }
</style>
