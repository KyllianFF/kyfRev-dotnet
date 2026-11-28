<script lang="ts">
  import ArchiveRestore from "@lucide/svelte/icons/archive-restore";
  import ArrowLeft from "@lucide/svelte/icons/arrow-left";
  import ChevronDown from "@lucide/svelte/icons/chevron-down";
  import Download from "@lucide/svelte/icons/download";
  import Ellipsis from "@lucide/svelte/icons/ellipsis";
  import Eye from "@lucide/svelte/icons/eye";
  import FileText from "@lucide/svelte/icons/file-text";
  import LayoutGrid from "@lucide/svelte/icons/layout-grid";
  import List from "@lucide/svelte/icons/list";
  import PenLine from "@lucide/svelte/icons/pen-line";
  import Plus from "@lucide/svelte/icons/plus";
  import Printer from "@lucide/svelte/icons/printer";
  import Search from "@lucide/svelte/icons/search";
  import Share2 from "@lucide/svelte/icons/share-2";
  import Trash2 from "@lucide/svelte/icons/trash-2";
  import Undo2 from "@lucide/svelte/icons/undo-2";
  import Upload from "@lucide/svelte/icons/upload";
  import X from "@lucide/svelte/icons/x";
  import { onMount } from "svelte";
  import { api, messageErreur, type SchemaRange } from "../../lib/api";
  import { momentCourt, pluriel } from "../../lib/format";
  import type { SchemaResume } from "../../lib/generes/SchemaResume";
  import { type Ancre, placerMenu } from "../../lib/placement";
  import {
    basculer,
    type CarteSchema,
    type CleTri,
    compterEtiquettes,
    etendre,
    etiqueter,
    filtrer,
    garderPresents,
    sansAccents,
    sections,
    sensNaturel,
    type Tri,
    TRIS,
    trier,
  } from "../../lib/topologie/accueil";
  import { apercuSchema } from "../../lib/topologie/apercu";
  import { definition } from "../../lib/topologie/catalogue";
  import { adresseAffichee, couleurDe, FAMILLES, familleDe } from "../../lib/topologie/dessin";
  import { diagnostiquer } from "../../lib/topologie/diagnostic";
  import type { IconesDrawio } from "../../lib/topologie/drawio";
  import { ecrireSchema, lireSchema } from "../../lib/topologie/format";
  import { type CompteRendu, lireImport } from "../../lib/topologie/importation";
  import { type IdVue, type Schema, STATUTS, schemaVide } from "../../lib/topologie/modele";
  import {
    avisEnClair,
    coffreChiffre,
    dureeAnnonce,
    estImage,
    estVide,
    exporterSchema,
    exporterSchemas,
    type FormatExport,
    imprimerSchema,
    partagerSchema,
  } from "../../lib/topologie/sorties";
  import { VUES, vue as vueDe } from "../../lib/topologie/vues";
  import DialogueSaisie from "../DialogueSaisie.svelte";
  import CarteAccueil from "./CarteAccueil.svelte";
  import CompteRenduImport from "./CompteRenduImport.svelte";
  import DialogueAjouterPages from "./DialogueAjouterPages.svelte";
  import DialogueEnregistrerModele from "./DialogueEnregistrerModele.svelte";
  import DialogueEtiqueter from "./DialogueEtiqueter.svelte";
  import DialogueExport from "./DialogueExport.svelte";
  import DialogueImport from "./DialogueImport.svelte";
  import DialogueModeles from "./DialogueModeles.svelte";
  import DialogueReunir from "./DialogueReunir.svelte";
  import EditeurTopologie from "./EditeurTopologie.svelte";
  import ListeSchemas from "./ListeSchemas.svelte";
  import RechercheGlobale from "./RechercheGlobale.svelte";
  import SelecteurPages from "./SelecteurPages.svelte";
  import { aPlusieursPages, nomDeLaPage, pageDeLObjet, pagesDe, pageValide } from "../../lib/topologie/pages";

  // Vue « Schémas réseau » (D40, 31A à 31F) : tous les schémas du coffre (schemas/*.json), en cartes ou en liste,
  // les épinglés en tête ; une recherche, des filtres par étiquette et par diagnostic, un tri. Un clic montre le détail
  // (grand aperçu dans la vue choisie, inventaire, constats, fiches qui le citent) sans ouvrir l'éditeur ;
  // « Modifier » l'ouvre. Le reste est au clic droit et sous « ⋯ » : renommer, épingler, étiqueter, dupliquer,
  // exporter, copier l'image, imprimer, archiver, supprimer. Plusieurs schémas se choisissent (Ctrl+clic, Maj+clic,
  // Ctrl+A) et se traitent ensemble depuis la barre du bas. Rien ne s'efface : les archives et la corbeille se lisent
  // sous la liste, et tout s'y restaure.
  let { onouvrirfiche }: { onouvrirfiche: (id: string) => void } = $props();

  type Detail = { schema: Schema; erreur?: undefined } | { erreur: string; schema?: undefined };
  /**
   * Menu flottant : celui d'une carte (clic droit, « ⋯ »), « Plus » dans le détail, « Nouveau schéma », « ⋯ » de
   * l'en-tête en fenêtre étroite, celui des schémas choisis, et « Filtres » en fenêtre étroite.
   */
  type Menu = {
    genre: "carte" | "plus" | "nouveau" | "entete" | "selection" | "filtres";
    schema?: SchemaResume;
    ancre: Ancre & { dessus?: boolean };
    origine: HTMLElement | null;
    /** Le menu des schémas choisis ouvert depuis la barre étroite : sans ce que la barre montre déjà. */
    reduit?: boolean;
  };
  /** Largeur sous laquelle l'en-tête du détail range aussi exporter, partager et imprimer sous « Plus ». */
  const LARGEUR_ETROITE = 780;
  /** Largeur sous laquelle l'accueil se resserre (31F) : deux cartes par ligne, « Importer » et le tri sous des menus. */
  const LARGEUR_ACCUEIL_ETROITE = 640;
  /** Les étiquettes montrées en puces dans une fenêtre étroite ; les autres sont sous « Filtres ». */
  const PUCES_ETROITES = 3;
  const CLE_AFFICHAGE = "kyfrev.schemas.affichage";
  let liste = $state<SchemaResume[] | null>(null);
  let details = $state<Record<string, Detail>>({});
  let erreur = $state<string | null>(null);
  let message = $state<string | null>(null);
  let recherche = $state("");
  let lu = $state<string | null>(null);
  let vueLue = $state<IdVue>("physique");
  /** La page montrée dans le détail d'un schéma à plusieurs pages (30A) ; vide : la première. */
  let pageLue = $state("");
  let edite = $state<string | null>(null);
  /** L'éditeur a été ouvert par l'application, depuis une fiche : le fermer y ramène, pas à cette liste. */
  let depuisFiche = $state(false);
  let creation = $state(false);
  let occupe = $state(false);
  let ranges = $state<SchemaRange[]>([]);
  let aSupprimer = $state<SchemaResume | null>(null);
  let aExporter = $state<SchemaResume | null>(null);
  let importOuvert = $state(false);
  /** Un fichier draw.io lu, en attente de son compte rendu : rien n'est encore dans le coffre. */
  let aGarder = $state<{ schema: Schema; compteRendu: CompteRendu } | null>(null);
  /** Les éléments à choisir à l'ouverture de l'éditeur (les formes inconnues d'un import). */
  let choisisAOuvrir = $state<string[]>([]);
  let menu = $state<Menu | null>(null);
  let largeur = $state(window.innerWidth);
  const etroit = $derived(largeur < LARGEUR_ETROITE);
  const accueilEtroit = $derived(largeur < LARGEUR_ACCUEIL_ETROITE);
  const archives = $derived(ranges.filter((s) => s.etat === "archive"));
  const corbeille = $derived(ranges.filter((s) => s.etat === "corbeille"));

  // --- L'accueil (31A, 31B) : filtres, tri, affichage, sélection ---

  let filtreEtiquettes = $state<string[]>([]);
  let filtreAVerifier = $state(false);
  let tri = $state<Tri>({ cle: "modifie", sens: "desc" });
  /** Grille ou liste : retenu d'une fois sur l'autre, sur ce poste. */
  let affichage = $state<"grille" | "liste">(affichageRetenu());
  let choisis = $state<string[]>([]);
  /** Le dernier schéma choisi ou lâché un par un : Maj+clic prend la suite depuis lui. */
  let ancre: string | null = null;
  /** Le schéma dont le nom se saisit sur place. */
  let enEdition = $state<string | null>(null);
  let aEtiqueter = $state<string[] | null>(null);
  let aExporterPlusieurs = $state<string[] | null>(null);
  let aSupprimerPlusieurs = $state<string[] | null>(null);
  let aReunir = $state<string[] | null>(null);
  let aAjouter = $state<string[] | null>(null);
  let modelesOuverts = $state(false);
  let enModele = $state<string | null>(null);

  function affichageRetenu(): "grille" | "liste" {
    try {
      return localStorage.getItem(CLE_AFFICHAGE) === "liste" ? "liste" : "grille";
    } catch {
      return "grille";
    }
  }

  function afficher(choix: "grille" | "liste") {
    affichage = choix;
    try {
      localStorage.setItem(CLE_AFFICHAGE, choix);
    } catch {
      // Le choix vaut pour cette fois : rien d'autre ne dépend de ce qui est retenu.
      return;
    }
  }

  /** Ce que l'accueil montre de chaque schéma, une fois lu : pages, diagnostic, étiquettes, de quoi chercher. */
  const cartes = $derived(
    (liste ?? []).map((s): CarteSchema => {
      const schema = details[s.id]?.schema;
      const etiquettes = schema?.etiquettes ?? [];
      return {
        id: s.id,
        titre: s.titre,
        equipements: schema?.equipements.length ?? s.equipements,
        pages: schema && aPlusieursPages(schema) ? pagesDe(schema).length : 1,
        points: schema ? diagnostiquer(schema).filter((c) => c.niveau !== "manque").length : 0,
        fiches: s.fiches.length,
        modifieLe: s.modifieLe,
        etiquettes,
        epingle: schema?.epingle === true,
        texte: sansAccents(
          `${s.titre} ${schema?.description ?? ""} ${s.fiches.map((f) => f.titre).join(" ")} ${schema?.equipements.map((e) => `${e.nom} ${e.gestion?.adresse ?? ""}`).join(" ") ?? ""} ${etiquettes.join(" ")}`,
        ),
      };
    }),
  );
  const resumes = $derived(new Map((liste ?? []).map((s) => [s.id, s])));
  const enUsage = $derived(compterEtiquettes(cartes));
  const nombreAVerifier = $derived(cartes.filter((c) => c.points > 0).length);
  const nombreEpingles = $derived(cartes.filter((c) => c.epingle).length);
  const filtres = $derived(
    filtrer(cartes, { recherche, etiquettes: filtreEtiquettes, aVerifier: filtreAVerifier }),
  );
  const rangees = $derived(sections(filtres, tri));
  /** Dans l'ordre affiché : Maj+clic et Ctrl+A s'y tiennent. */
  const affiches = $derived(affichage === "grille" ? [...rangees.epingles, ...rangees.autres] : trier(filtres, tri));
  const ordre = $derived(affiches.map((c) => c.id));
  const filtresActifs = $derived(filtreEtiquettes.length > 0 || filtreAVerifier || recherche.trim() !== "");
  const triConnu = $derived(TRIS.find((t) => t.cle === tri.cle && t.sens === tri.sens));
  /** Les puces : toutes les étiquettes ; en fenêtre étroite, les plus portées et celles choisies, le reste sous « Filtres ». */
  const puces = $derived(
    accueilEtroit
      ? enUsage.filter((e, rang) => rang < PUCES_ETROITES || filtreEtiquettes.includes(e.nom))
      : enUsage,
  );
  const horsPuces = $derived(enUsage.filter((e) => !puces.includes(e)));

  // Un schéma qui n'est plus affiché (filtré, archivé, supprimé) sort de la sélection : rien n'agit sur ce qu'on ne voit pas.
  $effect(() => {
    const restants = garderPresents(choisis, ordre);
    if (restants.length !== choisis.length) choisis = restants;
  });

  // Une étiquette choisie qui n'existe plus ne filtre plus.
  $effect(() => {
    const encore = filtreEtiquettes.filter((e) => enUsage.some((u) => u.nom === e));
    if (encore.length !== filtreEtiquettes.length && liste !== null) filtreEtiquettes = encore;
  });

  const basculerEtiquette = (nom: string) =>
    (filtreEtiquettes = filtreEtiquettes.includes(nom) ? filtreEtiquettes.filter((e) => e !== nom) : [...filtreEtiquettes, nom]);

  function effacerFiltres() {
    recherche = "";
    filtreEtiquettes = [];
    filtreAVerifier = false;
  }

  /** Un tri de la barre d'outils, ou un en-tête de la liste : un second clic sur le même en-tête renverse le sens. */
  function trierPar(cle: CleTri, depuisEntete = false) {
    tri =
      depuisEntete && tri.cle === cle
        ? { cle, sens: tri.sens === "asc" ? "desc" : "asc" }
        : { cle, sens: TRIS.find((t) => t.cle === cle)?.sens ?? sensNaturel(cle) };
  }

  /** Un clic sur une carte : Ctrl ajoute ou retire, Maj prend une suite ; seul, il ouvre le détail. */
  function clicCarte(id: string, evenement: MouseEvent) {
    if (evenement.ctrlKey || evenement.metaKey) {
      choisis = basculer(choisis, id);
      ancre = id;
    } else if (evenement.shiftKey) {
      choisis = etendre(ordre, choisis, ancre, id);
    } else {
      lire(id);
    }
  }

  /** La case d'une carte : elle ajoute ou retire ; avec Maj, elle prend la suite depuis le dernier choisi. */
  function clicCase(id: string, evenement: MouseEvent) {
    if (evenement.shiftKey && ancre !== null && !choisis.includes(id)) {
      choisis = etendre(ordre, choisis, ancre, id);
    } else {
      choisis = basculer(choisis, id);
      ancre = id;
    }
  }

  function lacher() {
    choisis = [];
    ancre = null;
  }

  /** Ctrl+A choisit tout ce qui est affiché, Échap lâche ; ni dans un champ, ni sous une fenêtre, ni dans l'éditeur. */
  function toucheAccueil(evenement: KeyboardEvent) {
    // Une touche déjà prise (Échap qui ferme un menu, un champ qui la garde) ne vaut pas pour l'accueil.
    if (evenement.defaultPrevented || lu || edite || menu || enEdition || document.querySelector("dialog[open]")) return;
    const cible = evenement.target instanceof Element ? evenement.target : null;
    const dansUnChamp = cible?.closest("input:not([type=checkbox]), textarea, select, [contenteditable=true]") != null;
    if ((evenement.ctrlKey || evenement.metaKey) && evenement.key.toLowerCase() === "a" && !dansUnChamp) {
      evenement.preventDefault();
      choisis = [...ordre];
    } else if (evenement.key === "Escape" && choisis.length > 0) {
      evenement.preventDefault();
      lacher();
    }
  }

  // Un message de réussite disparaît de lui-même ; une erreur reste, jusqu'à l'action suivante ou sa fermeture.
  $effect(() => {
    const texte = message;
    if (!texte) return;
    const minuterie = setTimeout(() => {
      if (message === texte) message = null;
    }, dureeAnnonce(texte));
    return () => clearTimeout(minuterie);
  });

  async function charger() {
    try {
      liste = await api.schemas();
      ranges = await api.schemasRanges();
      erreur = null;
      for (const s of liste) {
        try {
          details[s.id] = { schema: lireSchema((await api.lireSchema(s.id)).texte).schema };
        } catch (e) {
          details[s.id] = { erreur: messageErreur(e) };
        }
      }
    } catch (e) {
      erreur = messageErreur(e);
    }
  }

  onMount(() => {
    void charger();
    // L'application ouvre un schéma depuis une fiche.
    const ouvrir = (evenement: Event) => {
      depuisFiche = true;
      edite = (evenement as CustomEvent<string>).detail;
    };
    document.addEventListener("ouvrir-schema", ouvrir);
    // De retour sur la vue : un schéma a pu être importé, cité ou retiré entre-temps. L'éditeur ouvert, lui, reste tel quel.
    const revenus = () => {
      if (!edite) void charger();
    };
    document.addEventListener("schemas-revenus", revenus);
    return () => {
      document.removeEventListener("ouvrir-schema", ouvrir);
      document.removeEventListener("schemas-revenus", revenus);
    };
  });

  const resumeLu = $derived(lu ? (liste ?? []).find((s) => s.id === lu) : undefined);
  const schemaLu = $derived(lu ? details[lu]?.schema : undefined);
  const constats = $derived(schemaLu ? diagnostiquer(schemaLu) : []);
  const inventaire = $derived(
    schemaLu
      ? [...schemaLu.equipements].sort(
          (a, b) =>
            FAMILLES.findIndex((f) => f.valeur === familleDe(a.type)) -
              FAMILLES.findIndex((f) => f.valeur === familleDe(b.type)) || a.nom.localeCompare(b.nom, "fr"),
        )
      : [],
  );

  function lire(id: string) {
    lu = id;
    vueLue = details[id]?.schema?.vue ?? "physique";
    pageLue = "";
  }

  // --- Les pages du schéma lu (30A) : on les parcourt sans ouvrir l'éditeur ---

  const pagesLues = $derived(schemaLu && aPlusieursPages(schemaLu) ? pagesDe(schemaLu) : []);
  /** La page montrée : celle choisie si le schéma l'a toujours, sinon la première. */
  const pageMontree = $derived(schemaLu ? pageValide(schemaLu, pageLue) : "");

  /** Un clic sur la pastille d'un renvoi, dans l'aperçu : la page d'en face. */
  function suivreRenvoi(evenement: MouseEvent) {
    const id = (evenement.target as Element | null)?.closest("[data-renvoi]")?.getAttribute("data-renvoi");
    const schema = schemaLu;
    const lien = id && schema ? schema.liens.find((l) => l.id === id) : undefined;
    if (!lien || !schema) return;
    const bouts = [lien.a.equipement, lien.b.equipement].map((e) => pageDeLObjet(schema, e));
    const enFace = bouts.find((p) => p !== undefined && p !== pageMontree);
    if (enFace !== undefined) pageLue = enFace;
  }

  async function agir(action: () => Promise<string>) {
    occupe = true;
    message = null;
    erreur = null;
    try {
      message = await action();
      await charger();
    } catch (e) {
      erreur = messageErreur(e);
    } finally {
      occupe = false;
    }
  }

  const creer = (titre: string) =>
    agir(async () => {
      const cree = await api.creerSchema(ecrireSchema(schemaVide(titre)));
      edite = cree.id;
      return `Schéma « ${titre} » créé.`;
    });

  const dupliquer = (id: string) =>
    agir(async () => {
      const copie = await api.dupliquerSchema(id);
      const schema = lireSchema(copie.texte).schema;
      // La copie garde les étiquettes, pas l'épingle : l'original reste seul en tête.
      if (schema.epingle) await api.enregistrerSchema(copie.id, ecrireSchema({ ...schema, epingle: undefined }), copie.empreinte);
      return `Copie créée : « ${schema.titre} ».`;
    });

  /**
   * Retouche des schémas sans ouvrir l'éditeur (nom, épingle, étiquettes) : chacun est relu tel qu'il est dans le coffre,
   * changé, puis réécrit sur la version lue ; si elle a changé entre-temps, rien n'est écrit pour lui.
   */
  async function retoucher(ids: readonly string[], changer: (schema: Schema) => void): Promise<void> {
    for (const id of ids) {
      const lecture = await api.lireSchema(id);
      const { schema } = lireSchema(lecture.texte);
      changer(schema);
      const issue = await api.enregistrerSchema(id, ecrireSchema(schema), lecture.empreinte);
      if (issue.statut === "conflit") {
        throw new Error(`« ${schema.titre} » a changé entre-temps : rien n'y a été écrit. Réessayez.`);
      }
    }
  }

  function renommer(id: string, nom: string) {
    enEdition = null;
    const avant = resumes.get(id)?.titre ?? "";
    void agir(async () => {
      await retoucher([id], (s) => (s.titre = nom.slice(0, 200)));
      return `Schéma « ${avant} » renommé « ${nom} ».`;
    });
  }

  /** Renommer depuis le menu, F2 ou un double clic : le nom devient un champ, sur la carte ou dans la liste. */
  function demanderRenommer(id: string) {
    lacher();
    enEdition = id;
  }

  const epingler = (s: SchemaResume, epingle: boolean) =>
    agir(async () => {
      await retoucher([s.id], (schema) => (schema.epingle = epingle ? true : undefined));
      return epingle ? `« ${s.titre} » épinglé : il reste en tête des schémas.` : `« ${s.titre} » n'est plus épinglé.`;
    });

  const estEpingle = (id: string) => details[id]?.schema?.epingle === true;

  /** Les étiquettes portées par ces schémas, et combien chacune en porte : le point de départ d'« Étiqueter… ». */
  const etiquettesDe = (ids: readonly string[]) =>
    compterEtiquettes(cartes.filter((c) => ids.includes(c.id)));

  const poserEtiquettes = (ids: readonly string[], ajouts: string[], retraits: string[]) =>
    agir(async () => {
      await retoucher(ids, (s) => (s.etiquettes = etiqueter(s.etiquettes, ajouts, retraits)));
      return ids.length > 1 ? `Étiquettes de ${pluriel(ids.length, "schéma")} enregistrées.` : "Étiquettes enregistrées.";
    });

  /** Archive ou met à la corbeille les schémas choisis, et lâche la sélection. */
  const rangerPlusieurs = (ids: readonly string[], etat: "archive" | "corbeille") =>
    agir(async () => {
      for (const id of ids) await api.rangerSchema(id, etat);
      lacher();
      return etat === "archive"
        ? `${pluriel(ids.length, "schéma archivé", "schémas archivés")}. Les fiches qui les insèrent les montrent toujours.`
        : `${pluriel(ids.length, "schéma mis", "schémas mis")} à la corbeille : ils s'y restaurent.`;
    });

  const exporterPlusieurs = (ids: readonly string[], format: FormatExport, icones: IconesDrawio) =>
    sortir(async () => {
      const schemas = [];
      for (const id of ids) schemas.push(await schemaDuCoffre(id));
      const fait = await exporterSchemas(schemas, format, icones);
      if (!fait) return null;
      const sautes = fait.sautes > 0 ? ` ${pluriel(fait.sautes, "schéma vide n'a", "schémas vides n'ont")} pas d'image.` : "";
      return `${pluriel(fait.ecrits, "fichier écrit", "fichiers écrits")} dans ${fait.dossier}.${sautes}`;
    }, "Export impossible : ");

  /** Un résultat de la recherche dans tous les schémas : l'éditeur s'ouvre, l'objet trouvé choisi, sur sa page. */
  function ouvrirTrouve(schemaId: string, _pageId?: string, objetId?: string) {
    choisisAOuvrir = objetId ? [objetId] : [];
    edite = schemaId;
  }

  /** Un schéma créé ailleurs (modèle, réunion) : la liste le montre, et l'éditeur s'ouvre dessus. */
  function ouvrirCree(id: string) {
    lacher();
    void charger();
    edite = id;
  }

  /** Range un schéma (archives, corbeille, ou retour dans la liste) et le dit. */
  const ranger = (s: SchemaResume, etat: "actif" | "archive" | "corbeille", fait: string) =>
    agir(async () => {
      await api.rangerSchema(s.id, etat);
      if (lu === s.id) lu = null;
      return `Schéma « ${s.titre} » ${fait}`;
    });
  const archiver = (s: SchemaResume) => ranger(s, "archive", "archivé. Les fiches qui l'insèrent le montrent toujours.");
  const supprimer = (s: SchemaResume) => ranger(s, "corbeille", "mis à la corbeille : il s'y restaure.");
  const restaurer = (s: SchemaResume) => ranger(s, "actif", "remis dans la liste.");

  /**
   * Une sortie d'un schéma (export, partage, impression) : rien ne change dans le coffre, la liste n'est pas relue.
   * L'action rend le message de réussite, ou `null` s'il n'y a rien à dire (choix du dossier annulé).
   */
  async function sortir(action: () => Promise<string | null>, echec = "") {
    if (occupe) return;
    occupe = true;
    message = null;
    erreur = null;
    try {
      message = await action();
    } catch (e) {
      erreur = `${echec}${messageErreur(e)}`;
    } finally {
      occupe = false;
    }
  }

  /** Le schéma tel qu'il est dans le coffre à cet instant : la liste peut dater. */
  const schemaDuCoffre = async (id: string) => lireSchema((await api.lireSchema(id)).texte).schema;
  /** Depuis le détail, ce qui sort est la vue montrée ; depuis la liste, celle du schéma. */
  const vueDeSortie = (s: SchemaResume) => (lu === s.id ? vueDe(vueLue) : undefined);
  /** Depuis le détail, une image sort de la page montrée ; depuis la liste, de la première. */
  const pageDeSortie = (s: SchemaResume) => (lu === s.id && pageMontree ? pageMontree : undefined);

  const exporter = (s: SchemaResume, format: FormatExport, icones: IconesDrawio) =>
    sortir(async () => {
      const schema = await schemaDuCoffre(s.id);
      if (estImage(format) && estVide(schema)) return "Ce schéma est vide : rien à exporter en image.";
      const chemin = await exporterSchema(schema, format, vueDeSortie(s), icones, pageDeSortie(s));
      return chemin ? `Schéma exporté vers ${chemin}` : null;
    }, "Export impossible : ");

  const partager = (s: SchemaResume) =>
    sortir(async () => {
      const schema = await schemaDuCoffre(s.id);
      if (estVide(schema)) return "Ce schéma est vide : rien à partager.";
      await partagerSchema(schema, vueDeSortie(s), pageDeSortie(s));
      return `Image du schéma copiée : collez-la dans un message, un document ou une fiche.${avisEnClair(await coffreChiffre(), "image")}`;
    });

  const imprimer = (s: SchemaResume) =>
    sortir(async () => {
      const schema = await schemaDuCoffre(s.id);
      if (estVide(schema)) return "Ce schéma est vide : rien à imprimer.";
      return imprimerSchema(schema, vueDeSortie(s), pageDeSortie(s));
    }, "Impression impossible : ");

  /**
   * Le clic droit sur une carte ou une ligne : son menu ; sur l'un de plusieurs schémas choisis, celui de la sélection.
   */
  function ouvrirMenu(evenement: MouseEvent, schema: SchemaResume | undefined) {
    evenement.preventDefault();
    if (!schema || enEdition === schema.id) return;
    // Au clavier (touche Menu), l'évènement n'a pas de position : le menu s'ouvre sur la carte.
    const carte = evenement.currentTarget as HTMLElement;
    const cadre = carte.getBoundingClientRect();
    const auClavier = evenement.clientX === 0 && evenement.clientY === 0;
    // À la fermeture, le clavier revient d'où il vient : le bouton de la carte qui l'avait, sinon son aperçu.
    const actif = document.activeElement;
    menu = {
      schema,
      genre: choisis.length > 1 && choisis.includes(schema.id) ? "selection" : "carte",
      ancre: { x: auClavier ? cadre.left + 24 : evenement.clientX, y: auClavier ? cadre.top + 24 : evenement.clientY },
      origine: actif instanceof HTMLElement && carte.contains(actif) ? actif : carte.querySelector<HTMLElement>("button"),
    };
  }

  /**
   * Un bouton qui ouvre un menu (« Plus » du détail, « ⋯ » d'une carte, « Nouveau schéma », « Filtres »…) : le menu
   * s'ouvre sous lui (au-dessus pour la barre du bas), aligné sur sa droite ou sa gauche ; un second clic le ferme.
   */
  function ouvrirSous(
    evenement: MouseEvent,
    genre: Menu["genre"],
    options: { schema?: SchemaResume; gauche?: boolean; dessus?: boolean; reduit?: boolean } = {},
  ) {
    // Le clic ne remonte pas à la fenêtre, qui fermerait le menu aussitôt.
    evenement.stopPropagation();
    const bouton = evenement.currentTarget as HTMLElement;
    if (menu?.origine === bouton) {
      menu = null;
      return;
    }
    const cadre = bouton.getBoundingClientRect();
    menu = {
      genre,
      ...(options.schema ? { schema: options.schema } : {}),
      ancre: {
        x: options.gauche ? cadre.left : cadre.right,
        y: options.dessus ? cadre.top - 4 : cadre.bottom + 4,
        fin: !options.gauche,
        dessus: options.dessus === true,
      },
      origine: bouton,
      reduit: options.reduit === true,
    };
  }

  /** « Plus », dans l'en-tête du détail : le menu s'ouvre sous le bouton, aligné sur sa droite. */
  const ouvrirPlus = (evenement: MouseEvent, schema: SchemaResume) => ouvrirSous(evenement, "plus", { schema });

  /** Ferme le menu ; le clavier revient à l'élément d'où il a été ouvert. */
  function fermerMenu() {
    const origine = menu?.origine;
    menu = null;
    if (origine?.isConnected) origine.focus();
  }

  function choisir(action: (s: SchemaResume) => unknown) {
    const schema = menu?.schema;
    // Le focus revient d'abord à l'origine : un dialogue ouvert par l'action le lui rendra en se fermant.
    fermerMenu();
    if (schema) action(schema);
  }

  /** Une entrée d'un menu qui n'agit pas sur un schéma en particulier. */
  function faire(action: () => unknown) {
    fermerMenu();
    action();
  }

  /** Le menu mesuré une fois rendu : il ne sort jamais de la fenêtre, même étroite ou basse. */
  function dansLaFenetre(liste: HTMLElement, voulu: Menu["ancre"]) {
    // Sa taille sans l'animation d'ouverture, qui le montre un instant un peu plus petit.
    const cadre = { width: liste.offsetWidth, height: liste.offsetHeight };
    const ancre = voulu.dessus ? { ...voulu, y: voulu.y - cadre.height } : voulu;
    // La page défile dans `body` : sa largeur utile s'arrête avant la barre de défilement.
    const { documentElement: racine, body } = document;
    const place = placerMenu(
      ancre,
      { largeur: cadre.width, hauteur: cadre.height },
      {
        largeur: Math.min(racine.clientWidth, body.clientWidth),
        hauteur: Math.min(racine.clientHeight, body.clientHeight),
      },
    );
    liste.style.left = `${place.x}px`;
    liste.style.top = `${place.y}px`;
  }

  /**
   * Le menu prend le clavier à l'ouverture ; les flèches passent d'une entrée à l'autre (Début et Fin : la
   * première, la dernière), Échap le ferme, Tab aussi.
   */
  function clavierMenu(liste: HTMLElement) {
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
    };
    liste.addEventListener("keydown", touche);
    return { destroy: () => liste.removeEventListener("keydown", touche) };
  }

  const ouvrirDialogue = (dialogue: HTMLDialogElement) => dialogue.showModal();

  /**
   * Le fichier choisi : un modèle de kyfRev entre tout de suite ; un fichier draw.io passe d'abord par son compte
   * rendu, et n'entre que s'il est gardé.
   */
  async function importer() {
    const chemin = await api.choisirFichierSchema();
    if (!chemin) return;
    await agir(async () => {
      const importe = await lireImport(await api.lireImportSchema(chemin), chemin);
      if (importe.erreurs.length > 0) {
        throw new Error(
          `Import refusé : ${importe.erreurs.length} erreur(s) de structure, dont « ${importe.erreurs[0]} ». Rien n'a été créé.`,
        );
      }
      if (importe.compteRendu) {
        aGarder = { schema: importe.schema, compteRendu: importe.compteRendu };
        return "";
      }
      await api.creerSchema(ecrireSchema({ ...importe.schema, id: "", epingle: undefined }));
      return `Schéma « ${importe.schema.titre} » importé.`;
    });
  }

  /** Le compte rendu d'un import draw.io : annulé, rien n'entre ; gardé, le schéma entre ; montré, il s'ouvre, ses formes inconnues choisies. */
  function conclureImport(issue: "annuler" | "garder" | "montrer") {
    const importe = aGarder;
    aGarder = null;
    if (!importe) return;
    if (issue === "annuler") message = "Import annulé : rien n'a été créé.";
    else void garder(importe, issue === "montrer");
  }

  const garder = (importe: { schema: Schema; compteRendu: CompteRendu }, montrer: boolean) =>
    agir(async () => {
      const cree = await api.creerSchema(ecrireSchema({ ...importe.schema, id: "", epingle: undefined }));
      if (montrer) {
        choisisAOuvrir = importe.compteRendu.inconnus;
        edite = cree.id;
      }
      return `Schéma « ${importe.schema.titre} » importé.`;
    });

  const nombre = (schema: Schema | undefined, quoi: "equipements" | "liens" | "relations") =>
    schema?.[quoi].length ?? 0;
</script>

<div class="vue" class:accueil={!lu || !resumeLu}>
  {#if lu && resumeLu}
    <header class="entete-lecture">
      <button type="button" class="bouton discret" onclick={() => (lu = null)}>
        <ArrowLeft size={16} aria-hidden="true" /> Tous les schémas
      </button>
      <h1>{resumeLu.titre}</h1>
      <div class="actions">
        <label>
          <span class="visuellement-cache">Vue</span>
          <select class="champ" bind:value={vueLue}>
            {#each VUES as v (v.id)}<option value={v.id}>Vue {v.nom.toLowerCase()}</option>{/each}
          </select>
        </label>
        <!-- Fenêtre étroite : exporter, partager et imprimer rejoignent « Plus », l'en-tête tient en deux lignes. -->
        {#if !etroit}
          <button type="button" class="bouton" onclick={() => (aExporter = resumeLu)}>
            <Download size={16} aria-hidden="true" /> Exporter…
          </button>
          <button
            type="button"
            class="bouton"
            aria-label="Copier l'image"
            title="Copie l'image du schéma, à coller dans un message, un document ou une fiche"
            onclick={() => partager(resumeLu)}
          >
            <Share2 size={16} aria-hidden="true" /> Partager
          </button>
          <button type="button" class="bouton" onclick={() => imprimer(resumeLu)}>
            <Printer size={16} aria-hidden="true" /> Imprimer…
          </button>
        {/if}
        <button
          type="button"
          class="bouton"
          aria-haspopup="menu"
          aria-expanded={menu?.genre === "plus"}
          aria-label={etroit ? "Plus d'actions : exporter, partager, imprimer, dupliquer, archiver, supprimer" : "Plus d'actions : dupliquer, archiver, supprimer"}
          onclick={(e) => ouvrirPlus(e, resumeLu)}
        >
          <Ellipsis size={16} aria-hidden="true" /> Plus
        </button>
        <button type="button" class="bouton principal" onclick={() => (edite = resumeLu.id)}>
          <PenLine size={16} aria-hidden="true" /> Modifier
        </button>
      </div>
    </header>
    {#if schemaLu}
      {#if schemaLu.description}<p class="description">{schemaLu.description}</p>{/if}
      {#if pagesLues.length > 1}
        <!-- Un schéma à plusieurs pages (30A) : on les parcourt ici ; un renvoi cliqué dans l'aperçu mène aussi à la sienne. -->
        <div class="pages-lues">
          <SelecteurPages pages={pagesLues} montree={pageMontree} onchoisir={(id) => (pageLue = id)} />
        </div>
      {/if}
      <!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_noninteractive_element_interactions -->
      <figure
        class="grand-apercu"
        class:plusieurs-pages={pagesLues.length > 1}
        aria-label={pagesLues.length > 1
          ? `Aperçu du schéma « ${resumeLu.titre} », page « ${nomDeLaPage(schemaLu, pageMontree)} »`
          : `Aperçu du schéma « ${resumeLu.titre} »`}
        onclick={suivreRenvoi}
      >
        {@html apercuSchema(schemaLu, vueDe(vueLue), true, pagesLues.length > 1 ? pageMontree : undefined)}
      </figure>
      <div class="colonnes">
        <section class="inventaire" aria-labelledby="titre-inventaire">
          <h2 id="titre-inventaire">Inventaire <span class="compte">{schemaLu.equipements.length}</span></h2>
          <table>
            <thead>
              <tr><th scope="col">Équipement</th><th scope="col">Type</th><th scope="col">Adresse</th><th scope="col">Statut</th><th scope="col" class="nombre">Ports</th></tr>
            </thead>
            <tbody>
              {#each inventaire as e (e.id)}
                <tr>
                  <th scope="row"><span class="pastille-famille" style:background={couleurDe(e)}></span>{e.nom}</th>
                  <td>{definition(e.type).nom}{e.membres.length > 1 ? ` (stack ×${e.membres.length})` : ""}</td>
                  <td class="code">{adresseAffichee(e)}</td>
                  <td>{STATUTS.find((s) => s.valeur === e.statut)?.nom}</td>
                  <td class="nombre">{e.interfaces.length}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </section>
        <section aria-labelledby="titre-synthese">
          <h2 id="titre-synthese">Synthèse</h2>
          <ul class="synthese">
            <li>{pluriel(nombre(schemaLu, "liens"), "lien physique", "liens physiques")}</li>
            <li>{pluriel(nombre(schemaLu, "relations"), "relation logique", "relations logiques")}</li>
            <li>{pluriel(schemaLu.vlans.length, "VLAN")}, {pluriel(schemaLu.groupes.length, "groupe")}</li>
            <li class:alerte={constats.some((c) => c.niveau !== "manque")}>
              {pluriel(constats.filter((c) => c.niveau !== "manque").length, "point à vérifier", "points à vérifier")},
              {pluriel(constats.filter((c) => c.niveau === "manque").length, "information manquante", "informations manquantes")}
            </li>
            {#if resumeLu.modifieLe}<li>Modifié {momentCourt(resumeLu.modifieLe)}</li>{/if}
          </ul>
          <h2>Cité dans</h2>
          {#if resumeLu.fiches.length === 0}
            <p class="aide">Aucune fiche : insérez-le depuis l'écriture d'une fiche (Insérer › Diagramme › Schéma réseau).</p>
          {:else}
            <ul class="fiches">
              {#each resumeLu.fiches as f (f.id)}
                <li><button type="button" class="lien-fiche" onclick={() => onouvrirfiche(f.id)}><FileText size={14} aria-hidden="true" /> {f.titre}</button></li>
              {/each}
            </ul>
          {/if}
        </section>
      </div>
    {:else}
      <p class="bandeau erreur" role="alert">{details[resumeLu.id]?.erreur ?? "Chargement…"}</p>
    {/if}
  {:else}
    <header class="entete-accueil">
      <div>
        <h1>Schémas réseau</h1>
        {#if liste}
          <p class="resume">
            {[
              pluriel(liste.length, "schéma"),
              nombreEpingles > 0 ? pluriel(nombreEpingles, "épinglé") : "",
              archives.length > 0 ? pluriel(archives.length, "archivé") : "",
            ]
              .filter(Boolean)
              .join(" · ")}
          </p>
        {/if}
      </div>
      <div class="actions">
        {#if !accueilEtroit}
          <button type="button" class="bouton" disabled={occupe} onclick={() => (importOuvert = true)}><Upload size={16} aria-hidden="true" /> Importer…</button>
        {/if}
        <button
          type="button"
          class="bouton principal"
          disabled={occupe}
          aria-haspopup="menu"
          aria-expanded={menu?.genre === "nouveau"}
          onclick={(e) => ouvrirSous(e, "nouveau")}
        >
          <Plus size={16} aria-hidden="true" />
          {accueilEtroit ? "Nouveau" : "Nouveau schéma"}
          <ChevronDown size={14} aria-hidden="true" />
        </button>
        {#if accueilEtroit}
          <button
            type="button"
            class="bouton carre"
            disabled={occupe}
            aria-haspopup="menu"
            aria-expanded={menu?.genre === "entete"}
            aria-label="Plus d'actions : importer"
            title="Plus d'actions"
            onclick={(e) => ouvrirSous(e, "entete")}
          >
            <Ellipsis size={16} aria-hidden="true" />
          </button>
        {/if}
      </div>
    </header>
    {#if liste === null}
      <p class="vide" aria-busy="true">Chargement des schémas…</p>
    {:else if liste.length === 0}
      <p class="vide">
        {ranges.length === 0
          ? "Aucun schéma réseau pour l'instant. Créez-en un ici, ou depuis une fiche (Écrire › Insertion › Réseau)."
          : "Aucun schéma dans la liste : ceux que vous avez archivés ou supprimés sont plus bas."}
      </p>
    {:else}
      <div class="outils" role="search">
        <label class="recherche">
          <Search size={16} aria-hidden="true" />
          <span class="visuellement-cache">Chercher un schéma, un équipement</span>
          <input class="champ" type="search" placeholder={accueilEtroit ? "Chercher…" : "Chercher un schéma, un équipement…"} bind:value={recherche} />
        </label>
        {#if puces.length > 0 || nombreAVerifier > 0 || filtreAVerifier || accueilEtroit}
          <div class="puces" role="group" aria-label="Filtres">
            {#each puces as e (e.nom)}
              <button
                type="button"
                class="puce"
                aria-pressed={filtreEtiquettes.includes(e.nom)}
                onclick={() => basculerEtiquette(e.nom)}
              >
                {e.nom} <span class="n">{e.nombre}</span>
              </button>
            {/each}
            {#if !accueilEtroit && (nombreAVerifier > 0 || filtreAVerifier)}
              <button type="button" class="puce" aria-pressed={filtreAVerifier} onclick={() => (filtreAVerifier = !filtreAVerifier)}>
                À vérifier <span class="n">{nombreAVerifier}</span>
              </button>
            {/if}
            {#if accueilEtroit}
              <button
                type="button"
                class="puce"
                class:active={filtreAVerifier || horsPuces.some((e) => filtreEtiquettes.includes(e.nom))}
                aria-haspopup="menu"
                aria-expanded={menu?.genre === "filtres"}
                onclick={(e) => ouvrirSous(e, "filtres", { gauche: true })}
              >
                Filtres <ChevronDown size={12} aria-hidden="true" />
              </button>
            {/if}
          </div>
        {/if}
        {#if !accueilEtroit}
          <div class="ordre">
            <label class="tri">
              <span>Trier</span>
              <select
                class="champ"
                value={triConnu ? triConnu.cle : ""}
                onchange={(e) => e.currentTarget.value && trierPar(e.currentTarget.value as CleTri)}
              >
                {#each TRIS as t (t.cle)}<option value={t.cle}>{t.nom}</option>{/each}
                {#if !triConnu}<option value="" disabled>Par la liste</option>{/if}
              </select>
            </label>
            <div class="bascule" role="group" aria-label="Affichage">
              <button type="button" aria-pressed={affichage === "grille"} onclick={() => afficher("grille")}>
                <LayoutGrid size={14} aria-hidden="true" /> Grille
              </button>
              <button type="button" aria-pressed={affichage === "liste"} onclick={() => afficher("liste")}>
                <List size={14} aria-hidden="true" /> Liste
              </button>
            </div>
          </div>
        {/if}
      </div>
      {#if recherche.trim().length >= 2}
        <RechercheGlobale texte={recherche.trim()} onOuvrir={ouvrirTrouve} />
      {/if}
      {#if affiches.length === 0}
        <p class="vide">
          Aucun schéma ne correspond.
          {#if filtresActifs}<button type="button" class="bouton discret petit" onclick={effacerFiltres}>Effacer la recherche et les filtres</button>{/if}
        </p>
      {:else if affichage === "liste"}
        <ListeSchemas
          cartes={affiches}
          {choisis}
          {tri}
          edition={enEdition}
          ontrier={(cle) => trierPar(cle, true)}
          onclic={clicCarte}
          oncase={clicCase}
          ontout={(tout) => (tout ? (choisis = [...ordre]) : lacher())}
          onplus={(id, e) => ouvrirSous(e, "carte", { schema: resumes.get(id) })}
          onmenu={(id, e) => ouvrirMenu(e, resumes.get(id))}
          onrenommer={demanderRenommer}
          onvalider={renommer}
          onannuler={() => (enEdition = null)}
        />
      {:else}
        {#if rangees.epingles.length > 0}
          <h2 class="titre-section">Épinglés</h2>
          <ul class="grille" aria-label="Schémas épinglés">
            {#each rangees.epingles as c (c.id)}{@render carte(c)}{/each}
          </ul>
          {#if rangees.autres.length > 0}
            <h2 class="titre-section">Tous · {triConnu?.section ?? "triés"}</h2>
          {/if}
        {/if}
        {#if rangees.autres.length > 0}
          <ul class="grille" aria-label={rangees.epingles.length > 0 ? "Les autres schémas" : "Schémas"}>
            {#each rangees.autres as c (c.id)}{@render carte(c)}{/each}
          </ul>
        {/if}
      {/if}
    {/if}
    {#if archives.length > 0}
      <details class="ranges">
        <summary>Archives <span class="compte">{archives.length}</span></summary>
        <p class="aide">Hors de la liste, mais les fiches qui les insèrent les montrent toujours.</p>
        <ul>
          {#each archives as s (s.id)}
            <li>
              <span class="nom">{s.titre}</span>
              <span class="meta">{pluriel(s.equipements, "équipement")}{s.fiches.length > 0 ? ` · cité dans ${s.fiches.map((f) => f.titre).join(", ")}` : ""}</span>
              <button type="button" class="bouton petit" disabled={occupe} onclick={() => restaurer(s)}><ArchiveRestore size={14} aria-hidden="true" /> Désarchiver</button>
              <button type="button" class="bouton petit" disabled={occupe} onclick={() => (aSupprimer = s)}><Trash2 size={14} aria-hidden="true" /> Supprimer</button>
            </li>
          {/each}
        </ul>
      </details>
    {/if}
    {#if corbeille.length > 0}
      <details class="ranges">
        <summary>Corbeille <span class="compte">{corbeille.length}</span></summary>
        <p class="aide">Rien n'est effacé : un schéma supprimé reste ici, et se restaure tel qu'il était.</p>
        <ul>
          {#each corbeille as s (s.id)}
            <li>
              <span class="nom">{s.titre}</span>
              <span class="meta">{pluriel(s.equipements, "équipement")}{s.fiches.length > 0 ? ` · manque à ${s.fiches.map((f) => f.titre).join(", ")}` : ""}</span>
              <button type="button" class="bouton petit" disabled={occupe} onclick={() => restaurer(s)}><Undo2 size={14} aria-hidden="true" /> Restaurer</button>
            </li>
          {/each}
        </ul>
      </details>
    {/if}
  {/if}
</div>

<svelte:window
  bind:innerWidth={largeur}
  onclick={() => (menu = null)}
  onblur={() => (menu = null)}
  onresize={() => (menu = null)}
  onkeydown={toucheAccueil}
/>

{#snippet carte(c: CarteSchema)}
  {@const s = resumes.get(c.id)}
  <CarteAccueil
    carte={c}
    schema={details[c.id]?.schema}
    erreur={details[c.id]?.erreur}
    fiches={s?.fiches.map((f) => f.titre) ?? []}
    choisie={choisis.includes(c.id)}
    selection={choisis.length > 0}
    edition={enEdition === c.id}
    onclic={(e) => clicCarte(c.id, e)}
    oncase={(e) => clicCase(c.id, e)}
    onmodifier={() => (edite = c.id)}
    onplus={(e) => ouvrirSous(e, "carte", s ? { schema: s } : {})}
    onmenu={(e) => ouvrirMenu(e, s)}
    onrenommer={() => demanderRenommer(c.id)}
    onvalider={(nom) => renommer(c.id, nom)}
    onannuler={() => (enEdition = null)}
  />
{/snippet}

<!-- Les schémas choisis (31B) : une barre en bas, par-dessus la liste ; elle passe à la ligne dans une fenêtre étroite. -->
{#if choisis.length > 0 && !lu && !edite}
  <div class="barre-selection" role="toolbar" aria-label="Schémas choisis">
    <span class="compte-choisis" aria-live="polite">
      {accueilEtroit ? pluriel(choisis.length, "choisi") : pluriel(choisis.length, "schéma choisi", "schémas choisis")}
    </span>
    <button
      type="button"
      class="bouton principal petit"
      disabled={occupe || choisis.length < 2}
      title={choisis.length < 2 ? "Choisissez au moins deux schémas" : undefined}
      onclick={() => (aReunir = [...choisis])}
    >
      {accueilEtroit ? "Réunir…" : "Réunir en un schéma…"}
    </button>
    <button type="button" class="bouton petit" disabled={occupe} onclick={() => (aAjouter = [...choisis])}>
      {accueilEtroit ? "Ajouter à…" : "Ajouter à un schéma…"}
    </button>
    {#if accueilEtroit}
      <button
        type="button"
        class="bouton petit carre"
        aria-haspopup="menu"
        aria-expanded={menu?.genre === "selection"}
        aria-label="Plus d'actions sur les schémas choisis : étiqueter, exporter, archiver, supprimer"
        title="Plus d'actions"
        onclick={(e) => ouvrirSous(e, "selection", { dessus: true, reduit: true })}
      >
        <Ellipsis size={16} aria-hidden="true" />
      </button>
    {:else}
      <button type="button" class="bouton petit" disabled={occupe} onclick={() => (aEtiqueter = [...choisis])}>Étiqueter…</button>
      <button type="button" class="bouton petit" disabled={occupe} onclick={() => (aExporterPlusieurs = [...choisis])}>Exporter…</button>
      <button type="button" class="bouton petit" disabled={occupe} onclick={() => rangerPlusieurs([...choisis], "archive")}>Archiver</button>
      <button type="button" class="bouton petit" disabled={occupe} onclick={() => (aSupprimerPlusieurs = [...choisis])}>Supprimer…</button>
    {/if}
    <button type="button" class="bouton petit carre" aria-label="Lâcher la sélection (Échap)" title="Lâcher (Échap)" onclick={lacher}>
      <X size={16} aria-hidden="true" />
    </button>
  </div>
{/if}

<!-- Les messages, au bas de la fenêtre dans la liste comme dans le détail : la réussite s'efface seule, l'erreur se ferme. -->
<div class="annonces" class:au-dessus={choisis.length > 0 && !lu && !edite}>
  <div role="status">
    {#if message}<p class="annonce succes">{message}</p>{/if}
  </div>
  {#if erreur}
    <div class="annonce erreur" role="alert">
      <p>{erreur}</p>
      <button type="button" class="fermer" aria-label="Fermer le message d'erreur" title="Fermer" onclick={() => (erreur = null)}>
        <X size={16} aria-hidden="true" />
      </button>
    </div>
  {/if}
</div>

<!-- Les sorties, dans les mots du reste de kyfRev (rangée 23D) : copier l'image, l'exporter, l'imprimer. -->
{#snippet entreesSorties()}
  <button type="button" role="menuitem" onclick={() => choisir(partager)}>Copier l'image</button>
  <button type="button" role="menuitem" onclick={() => choisir((s) => (aExporter = s))}>Exporter…</button>
  <button type="button" role="menuitem" onclick={() => choisir(imprimer)}>Imprimer…</button>
{/snippet}

{#if menu}
  {@const m = menu}
  {#key m}
    <div
      class="menu"
      role="menu"
      aria-label={m.genre === "nouveau"
        ? "Nouveau schéma"
        : m.genre === "entete"
          ? "Plus d'actions"
          : m.genre === "filtres"
            ? "Filtres, tri et affichage"
            : m.genre === "selection"
              ? `Actions sur ${pluriel(choisis.length, "schéma choisi", "schémas choisis")}`
              : m.genre === "carte"
                ? `Actions sur « ${m.schema?.titre ?? ""} »`
                : `Plus d'actions sur « ${m.schema?.titre ?? ""} »`}
      use:dansLaFenetre={m.ancre}
      use:clavierMenu
    >
      <!-- Le même dessin que les menus de kyfRev (rangée 23D) : les actions principales en tête, l'icône au-dessus du mot ;
           puis la liste, par blocs. -->
      {#if m.genre === "nouveau"}
        <button type="button" role="menuitem" class="fort" onclick={() => faire(() => (creation = true))}>Schéma vide</button>
        <button type="button" role="menuitem" onclick={() => faire(() => (modelesOuverts = true))}>
          Depuis un modèle… <span class="indice">Petit bureau, étage…</span>
        </button>
        <button type="button" role="menuitem" onclick={() => faire(() => (importOuvert = true))}>Depuis un fichier draw.io…</button>
        <hr />
        <button type="button" role="menuitem" onclick={() => faire(() => (aReunir = []))}>Réunir des schémas…</button>
      {:else if m.genre === "entete"}
        <button type="button" role="menuitem" onclick={() => faire(() => (importOuvert = true))}><Upload size={16} aria-hidden="true" /> Importer…</button>
      {:else if m.genre === "filtres"}
        <button type="button" role="menuitemcheckbox" aria-checked={filtreAVerifier} onclick={() => faire(() => (filtreAVerifier = !filtreAVerifier))}>
          <span class="coche" aria-hidden="true">{filtreAVerifier ? "✓" : ""}</span> À vérifier <span class="indice">{nombreAVerifier}</span>
        </button>
        {#each horsPuces as e (e.nom)}
          {@const pris = filtreEtiquettes.includes(e.nom)}
          <button type="button" role="menuitemcheckbox" aria-checked={pris} onclick={() => faire(() => basculerEtiquette(e.nom))}>
            <span class="coche" aria-hidden="true">{pris ? "✓" : ""}</span> {e.nom} <span class="indice">{e.nombre}</span>
          </button>
        {/each}
        <hr />
        <p class="groupe" aria-hidden="true">Trier</p>
        {#each TRIS as t (t.cle)}
          {@const pris = triConnu?.cle === t.cle}
          <button type="button" role="menuitemradio" aria-checked={pris} onclick={() => faire(() => trierPar(t.cle))}>
            <span class="coche" aria-hidden="true">{pris ? "•" : ""}</span> {t.nom}
          </button>
        {/each}
        <hr />
        <p class="groupe" aria-hidden="true">Affichage</p>
        <button type="button" role="menuitemradio" aria-checked={affichage === "grille"} onclick={() => faire(() => afficher("grille"))}>
          <span class="coche" aria-hidden="true">{affichage === "grille" ? "•" : ""}</span> Grille
        </button>
        <button type="button" role="menuitemradio" aria-checked={affichage === "liste"} onclick={() => faire(() => afficher("liste"))}>
          <span class="coche" aria-hidden="true">{affichage === "liste" ? "•" : ""}</span> Liste
        </button>
      {:else if m.genre === "selection"}
        {#if !m.reduit}
          <button type="button" role="menuitem" disabled={occupe} onclick={() => faire(() => (aReunir = [...choisis]))}>Réunir en un schéma…</button>
          <button type="button" role="menuitem" disabled={occupe} onclick={() => faire(() => (aAjouter = [...choisis]))}>Ajouter à un schéma…</button>
          <hr />
        {/if}
        <button type="button" role="menuitem" disabled={occupe} onclick={() => faire(() => (aEtiqueter = [...choisis]))}>Étiqueter…</button>
        <button type="button" role="menuitem" disabled={occupe} onclick={() => faire(() => (aExporterPlusieurs = [...choisis]))}>Exporter…</button>
        <hr />
        <button type="button" role="menuitem" disabled={occupe} onclick={() => faire(() => rangerPlusieurs([...choisis], "archive"))}>Archiver</button>
        <button type="button" role="menuitem" disabled={occupe} onclick={() => faire(() => (aSupprimerPlusieurs = [...choisis]))}>Supprimer…</button>
      {:else if m.genre === "carte" && m.schema}
        {@const epingle = estEpingle(m.schema.id)}
        <div class="vite">
          <button type="button" role="menuitem" onclick={() => choisir((s) => lire(s.id))}><Eye size={18} aria-hidden="true" /> Détail</button>
          <button type="button" role="menuitem" onclick={() => choisir((s) => (edite = s.id))}><PenLine size={18} aria-hidden="true" /> Modifier</button>
        </div>
        <hr />
        <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir((s) => demanderRenommer(s.id))}>Renommer <span class="indice">F2</span></button>
        <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir((s) => epingler(s, !estEpingle(s.id)))}>{epingle ? "Désépingler" : "Épingler"}</button>
        <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir((s) => (aEtiqueter = [s.id]))}>Étiqueter…</button>
        <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir((s) => dupliquer(s.id))}>Dupliquer</button>
        <hr />
        <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir((s) => (aAjouter = [s.id]))}>Ajouter à un schéma…</button>
        <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir((s) => (enModele = s.id))}>Enregistrer comme modèle…</button>
        <hr />
        {@render entreesSorties()}
        <hr />
        <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir(archiver)}>Archiver</button>
        <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir((s) => (aSupprimer = s))}>Supprimer…</button>
      {:else}
        {#if etroit}
          {@render entreesSorties()}
          <hr />
        {/if}
        <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir((s) => dupliquer(s.id))}>Dupliquer</button>
        <hr />
        <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir(archiver)}>Archiver</button>
        <button type="button" role="menuitem" disabled={occupe} onclick={() => choisir((s) => (aSupprimer = s))}>Supprimer…</button>
      {/if}
    </div>
  {/key}
{/if}

{#if aEtiqueter}
  {@const ids = aEtiqueter}
  <DialogueEtiqueter
    titre={resumes.get(ids[0] ?? "")?.titre ?? ""}
    nombre={ids.length}
    presentes={etiquettesDe(ids)}
    connues={enUsage.map((e) => e.nom)}
    onvalider={(ajouts, retraits) => poserEtiquettes(ids, ajouts, retraits)}
    onfermer={() => (aEtiqueter = null)}
  />
{/if}

{#if aExporterPlusieurs}
  {@const ids = aExporterPlusieurs}
  <DialogueExport
    titre=""
    nombre={ids.length}
    onvalider={(format, icones) => exporterPlusieurs(ids, format, icones)}
    onfermer={() => (aExporterPlusieurs = null)}
  />
{/if}

{#if aSupprimerPlusieurs}
  {@const ids = aSupprimerPlusieurs}
  {@const cites = ids.map((id) => resumes.get(id)).filter((s) => s !== undefined && s.fiches.length > 0)}
  <dialog class="confirmation" aria-labelledby="titre-suppression-plusieurs" use:ouvrirDialogue onclose={() => (aSupprimerPlusieurs = null)}>
    <h2 id="titre-suppression-plusieurs">Supprimer {pluriel(ids.length, "schéma")} ?</h2>
    <p>Ils vont à la corbeille des schémas, en bas de cette page : ils s'y restaurent tels qu'ils sont.</p>
    {#if cites.length > 0}
      <p class="alerte">
        Des fiches en insèrent ({cites.map((s) => `« ${s?.titre} »`).join(", ")}) : ces schémas y manqueront tant qu'ils ne
        seront pas restaurés. Pour les retirer de la liste sans les retirer des fiches, archivez-les.
      </p>
    {/if}
    <div class="actions fin">
      <!-- svelte-ignore a11y_autofocus -->
      <button type="button" class="bouton" autofocus onclick={(e) => e.currentTarget.closest("dialog")?.close()}>Annuler</button>
      <button
        type="button"
        class="bouton principal"
        onclick={(e) => {
          e.currentTarget.closest("dialog")?.close();
          void rangerPlusieurs(ids, "corbeille");
        }}
      >
        Supprimer
      </button>
    </div>
  </dialog>
{/if}

<!-- Les fenêtres des autres outils de l'accueil (31C, 31D, 31E) : elles se montrent quand on les ouvre. -->
<DialogueReunir
  ids={aReunir ?? []}
  ouvert={aReunir !== null}
  onFermer={() => (aReunir = null)}
  onFait={() => {
    aReunir = null;
    lacher();
    message = "Schémas réunis dans un nouveau schéma.";
    void charger();
  }}
/>
<DialogueAjouterPages
  cible={null}
  sources={aAjouter ?? []}
  ouvert={aAjouter !== null}
  onFermer={() => (aAjouter = null)}
  onFait={() => {
    aAjouter = null;
    lacher();
    message = "Pages ajoutées au schéma.";
    void charger();
  }}
/>
<DialogueModeles
  ouvert={modelesOuverts}
  onFermer={() => (modelesOuverts = false)}
  onCree={(id) => {
    modelesOuverts = false;
    ouvrirCree(id);
  }}
/>
<DialogueEnregistrerModele schemaId={enModele ?? ""} ouvert={enModele !== null} onFermer={() => (enModele = null)} />

{#if aExporter}
  {@const s = aExporter}
  {@const connu = details[s.id]?.schema}
  <DialogueExport
    titre={s.titre}
    vide={connu !== undefined && estVide(connu)}
    onvalider={(format, icones) => exporter(s, format, icones)}
    onfermer={() => (aExporter = null)}
  />
{/if}

{#if importOuvert}
  <DialogueImport onvalider={() => void importer()} onfermer={() => (importOuvert = false)} />
{/if}

{#if aGarder}
  <CompteRenduImport
    titre={aGarder.schema.titre}
    compteRendu={aGarder.compteRendu}
    onannuler={() => conclureImport("annuler")}
    ongarder={() => conclureImport("garder")}
    onmontrer={() => conclureImport("montrer")}
  />
{/if}

{#if aSupprimer}
  {@const s = aSupprimer}
  <dialog class="confirmation" aria-labelledby="titre-suppression" use:ouvrirDialogue onclose={() => (aSupprimer = null)}>
    <h2 id="titre-suppression">Supprimer « {s.titre} » ?</h2>
    <p>Le schéma va à la corbeille des schémas, en bas de cette page : il s'y restaure tel qu'il est.</p>
    {#if s.fiches.length > 0}
      <p class="alerte">
        {pluriel(s.fiches.length, "fiche l'insère", "fiches l'insèrent")} ({s.fiches.map((f) => f.titre).join(", ")}) : le
        schéma y manquera tant qu'il ne sera pas restauré. Pour le retirer de la liste sans le retirer des fiches, archivez-le.
      </p>
    {/if}
    <div class="actions fin">
      <!-- svelte-ignore a11y_autofocus -->
      <button type="button" class="bouton" autofocus onclick={(e) => e.currentTarget.closest("dialog")?.close()}>Annuler</button>
      <button
        type="button"
        class="bouton principal"
        onclick={(e) => {
          e.currentTarget.closest("dialog")?.close();
          void supprimer(s);
        }}
      >
        Supprimer
      </button>
    </div>
  </dialog>
{/if}

{#if creation}
  <DialogueSaisie
    titre="Nouveau schéma réseau"
    libelle="Nom du schéma"
    valeur=""
    libelleValider="Créer"
    verifier={(nom) => (nom.trim() ? null : "Un schéma doit avoir un nom.")}
    onvalider={(nom) => creer(nom.trim())}
    onfermer={() => (creation = false)}
  />
{/if}

{#if edite}
  {#key edite}
    <EditeurTopologie
      id={edite}
      retour={depuisFiche ? "Fermer" : "Schémas"}
      choisis={choisisAOuvrir}
      onouvrir={(id, choisis) => {
        choisisAOuvrir = choisis;
        edite = id;
      }}
      onfermer={() => {
        choisisAOuvrir = [];
        edite = null;
        depuisFiche = false;
        void charger();
        void api.editeurFerme();
      }}
    />
  {/key}
{/if}

<style>
  /* Centrée, comme les autres pages : la liste et la lecture d'un schéma ne restent pas collées à gauche d'une grande fenêtre. */
  .vue {
    max-width: 1200px;
    margin: 0 auto;
    padding: 24px 32px 48px;
  }

  header {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-start;
    justify-content: space-between;
    gap: 12px;
  }

  h1 {
    margin: 0;
    font-size: 26px;
  }

  .resume,
  .vide,
  .aide,
  .description {
    color: var(--texte-secondaire);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
  }

  .actions .champ {
    width: auto;
  }

  .bandeau {
    padding: 10px 14px;
    border-radius: var(--rayon);
  }

  .erreur {
    background: var(--retard-fond);
    color: var(--retard);
  }

  .succes {
    background: var(--succes-fond);
    color: var(--succes);
  }

  /* L'accueil prend plus de place que la lecture : jusqu'à six cartes par ligne sur un très grand écran (31F). */
  .vue.accueil {
    max-width: 2240px;
  }

  .entete-accueil .resume {
    margin: 4px 0 0;
    font-size: 13px;
  }

  .bouton.carre {
    width: 34px;
    justify-content: center;
    padding: 0;
  }

  .bouton.petit.carre {
    width: 28px;
    padding: 0;
  }

  /* La barre d'outils (31A) : la recherche, les puces de filtre, puis le tri et l'affichage, poussés à droite. */
  .outils {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px 12px;
    margin: 16px 0;
  }

  .recherche {
    position: relative;
    display: flex;
    flex: 0 1 340px;
    align-items: center;
    min-width: 200px;
  }

  .recherche :global(svg) {
    position: absolute;
    left: 10px;
    color: var(--texte-secondaire);
  }

  .recherche input {
    padding-left: 32px;
  }

  .puces {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
  }

  .puce {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    min-height: 30px;
    padding: 0 12px;
    border: 1px solid var(--bordure);
    border-radius: 999px;
    background: var(--surface);
    color: var(--texte);
    font-size: 13px;
    white-space: nowrap;
  }

  .puce:hover {
    background: var(--surface-2);
  }

  .puce[aria-pressed="true"],
  .puce.active {
    border-color: var(--principale);
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
    font-weight: 600;
  }

  .puce .n {
    color: var(--texte-secondaire);
    font-size: 12px;
    font-weight: 400;
  }

  .ordre {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
    margin-left: auto;
  }

  .tri {
    display: flex;
    align-items: center;
    gap: 8px;
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .tri .champ {
    width: auto;
    height: 32px;
    color: var(--texte);
  }

  .bascule {
    display: inline-flex;
    overflow: hidden;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
  }

  .bascule button {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    height: 30px;
    padding: 0 12px;
    border: none;
    background: var(--surface);
    color: var(--texte);
    font-size: 13px;
  }

  .bascule button + button {
    border-left: 1px solid var(--bordure);
  }

  .bascule button:hover {
    background: var(--surface-2);
  }

  .bascule button[aria-pressed="true"] {
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
    font-weight: 600;
  }

  .titre-section {
    margin: 4px 0 10px;
    color: var(--texte-secondaire);
    font-size: 12px;
    font-weight: 700;
    letter-spacing: 0.03em;
    text-transform: uppercase;
  }

  .grille + .titre-section {
    margin-top: 24px;
  }

  .grille {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
    gap: 16px;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .vide .bouton {
    margin-left: 6px;
  }

  /* Les schémas choisis : la barre flotte au bas de la fenêtre, comme les avis, et passe à la ligne si elle manque de place. */
  .barre-selection {
    position: fixed;
    bottom: 16px;
    left: 50%;
    z-index: 25;
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: center;
    gap: 6px;
    width: max-content;
    max-width: calc(100vw - 32px);
    padding: 8px 8px 8px 14px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    box-shadow: 0 10px 28px rgb(0 0 0 / 0.18);
    transform: translateX(-50%);
    animation: kyfrev-monter var(--duree-ouverture) var(--decelere);
  }

  @keyframes kyfrev-monter {
    from {
      opacity: 0;
      transform: translate(-50%, 8px);
    }
  }

  .compte-choisis {
    margin-right: 4px;
    font-size: 13px;
    font-weight: 600;
    white-space: nowrap;
  }

  .annonces.au-dessus {
    bottom: 76px;
  }

  .menu .fort {
    font-weight: 600;
  }

  .menu .indice {
    margin-left: auto;
    padding-left: 16px;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .menu .coche {
    display: inline-block;
    width: 12px;
    text-align: center;
  }

  .menu .groupe {
    margin: 2px 10px 2px;
    color: var(--texte-secondaire);
    font-size: 12px;
    font-weight: 600;
  }

  /* Fenêtre étroite (31F) : deux cartes par ligne, la recherche sur toute la largeur. */
  @media (max-width: 640px) {
    .vue.accueil {
      padding: 16px 16px 96px;
    }

    .entete-accueil {
      flex-wrap: nowrap;
      align-items: center;
    }

    .entete-accueil h1 {
      font-size: 22px;
    }

    .entete-accueil .actions {
      flex-wrap: nowrap;
    }

    .outils {
      margin: 12px 0;
    }

    .recherche {
      flex: 1 1 100%;
    }

    .grille {
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 10px;
    }

    .barre-selection {
      bottom: 10px;
      max-width: calc(100vw - 20px);
    }
  }

  .bouton.petit {
    height: 28px;
    padding: 0 10px;
    font-size: 12px;
  }

  .entete-lecture {
    align-items: center;
  }

  .ranges {
    margin-top: 24px;
    padding: 10px 14px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
  }

  .ranges summary {
    font-weight: 600;
    cursor: pointer;
  }

  .ranges .aide {
    margin: 6px 0 8px;
    font-size: 13px;
  }

  .ranges ul {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .ranges li {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
    padding: 6px 0;
    border-top: 1px solid var(--bordure);
  }

  .ranges .nom {
    font-weight: 600;
  }

  .ranges .meta {
    flex: 1 1 160px;
  }

  .annonces {
    position: fixed;
    right: 16px;
    bottom: 16px;
    left: 16px;
    z-index: 20;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 8px;
    pointer-events: none;
  }

  .annonces [role="status"] {
    display: flex;
    justify-content: center;
    max-width: 100%;
  }

  .annonce {
    display: flex;
    align-items: center;
    gap: 8px;
    max-width: min(640px, 100%);
    margin: 0;
    padding: 8px 14px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.18);
    font-size: 13px;
    overflow-wrap: anywhere;
    pointer-events: auto;
  }

  .annonce.erreur {
    padding: 4px 4px 4px 14px;
  }

  .annonce p {
    margin: 0;
  }

  .fermer {
    display: inline-flex;
    flex: none;
    align-items: center;
    justify-content: center;
    width: 32px;
    height: 32px;
    border: none;
    border-radius: var(--rayon);
    background: none;
    color: inherit;
  }

  .fermer:hover {
    background: var(--surface);
  }

  .menu {
    position: fixed;
    z-index: 30;
    display: flex;
    flex-direction: column;
    min-width: 220px;
    max-width: calc(100vw - 16px);
    max-height: calc(100vh - 16px);
    overflow-y: auto;
    padding: 4px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--surface);
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.18);
  }

  .menu button {
    display: flex;
    flex: none;
    align-items: center;
    gap: 8px;
    height: 32px;
    padding: 0 10px;
    border: none;
    border-radius: var(--rayon);
    background: none;
    color: var(--texte);
    font: inherit;
    text-align: left;
    cursor: pointer;
  }

  .menu button:hover:not(:disabled),
  .menu button:focus-visible {
    background: var(--surface-2);
  }

  /* Les actions principales : l'icône au-dessus du mot, côte à côte, comme les menus de kyfRev. */
  .menu .vite {
    display: flex;
    gap: 4px;
  }

  .menu .vite button {
    flex: 1;
    flex-direction: column;
    justify-content: center;
    gap: 4px;
    height: auto;
    padding: 8px 6px;
    text-align: center;
  }

  .menu button:disabled {
    opacity: 0.5;
    cursor: default;
  }

  .menu hr {
    width: 100%;
    margin: 4px 0;
    border: none;
    border-top: 1px solid var(--bordure);
  }

  .confirmation {
    width: min(480px, calc(100vw - 32px));
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

  .actions.fin {
    justify-content: flex-end;
    margin-top: 16px;
  }

  .entete-lecture h1 {
    flex: 1 1 200px;
  }

  .grand-apercu {
    display: flex;
    justify-content: center;
    margin: 16px 0;
    padding: 16px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    overflow: auto;
  }

  .grand-apercu :global(svg) {
    max-width: 100%;
    height: auto;
  }

  /* Les pages, juste au-dessus de l'aperçu, à droite : le sélecteur colle au dessin qu'il change. */
  .pages-lues {
    display: flex;
    justify-content: flex-end;
    margin: 16px 0 -8px;
  }

  .grand-apercu.plusieurs-pages :global([data-renvoi]) {
    cursor: pointer;
  }

  .colonnes {
    display: grid;
    grid-template-columns: minmax(0, 2fr) minmax(240px, 1fr);
    gap: 24px;
  }

  /* Fenêtre étroite : le tableau défile dans sa section au lieu de sortir de la page. */
  .inventaire {
    min-width: 0;
    overflow-x: auto;
  }

  h2 {
    margin: 0 0 8px;
    font-size: 16px;
  }

  .compte {
    color: var(--texte-secondaire);
    font-weight: 400;
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 13px;
  }

  th,
  td {
    padding: 6px 8px;
    border-bottom: 1px solid var(--bordure);
    text-align: left;
  }

  thead th {
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  tbody th {
    font-weight: 600;
    white-space: nowrap;
  }

  .code {
    font-family: var(--police-code);
    font-size: 12px;
  }

  .nombre {
    text-align: right;
  }

  .pastille-famille {
    display: inline-block;
    width: 10px;
    height: 10px;
    margin-right: 6px;
    border-radius: 3px;
  }

  .synthese,
  .fiches {
    margin: 0 0 16px;
    padding-left: 18px;
    font-size: 13px;
  }

  .synthese li,
  .fiches li {
    margin-bottom: 4px;
  }

  .alerte {
    color: var(--avertissement);
  }

  .fiches {
    padding: 0;
    list-style: none;
  }

  .lien-fiche {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 0;
    border: none;
    background: none;
    color: var(--lien);
    cursor: pointer;
  }

  @media (max-width: 900px) {
    .colonnes {
      grid-template-columns: minmax(0, 1fr);
    }
  }
</style>
