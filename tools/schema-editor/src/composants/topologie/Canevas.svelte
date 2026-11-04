<script lang="ts">
  import AlignHorizontalDistributeCenter from "@lucide/svelte/icons/align-horizontal-distribute-center";
  import AlignStartVertical from "@lucide/svelte/icons/align-start-vertical";
  import AlignVerticalDistributeCenter from "@lucide/svelte/icons/align-vertical-distribute-center";
  import Cable from "@lucide/svelte/icons/cable";
  import Copy from "@lucide/svelte/icons/copy";
  import CopyPlus from "@lucide/svelte/icons/copy-plus";
  import Spline from "@lucide/svelte/icons/spline";
  import Trash2 from "@lucide/svelte/icons/trash-2";
  import { onMount } from "svelte";
  import { definition } from "../../lib/topologie/catalogue";
  import {
    adresseAffichee,
    boiteDe,
    COTE,
    COULEUR_MAINTENANCE,
    cadreAnnotation,
    cheminDe,
    couleurDe,
    dessinerAnnotation,
    dessinerEtiquette,
    dessinerMarque,
    estZone,
    ligneDuNom,
    masques,
    mettreEnPage,
    milieu,
    traceEntre,
    traitDuSupport,
  } from "../../lib/topologie/dessin";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import { illustration } from "../../lib/topologie/illustrations";
  import {
    type Annotation,
    COULEURS_ANNOTATION,
    type Equipement,
    type Lien,
    type Point,
    SUPPORTS,
    type Support,
  } from "../../lib/topologie/modele";
  import {
    ALIGNEMENTS,
    type Alignement,
    ajouterInterface,
    aligner,
    connecter,
    rebrancher,
    repartir,
  } from "../../lib/topologie/operations";
  import { vue as vueDe } from "../../lib/topologie/vues";
  import MenuPorts from "./MenuPorts.svelte";

  // Canevas de l'éditeur de schémas réseau (D39). Le modèle est la seule
  // source : le canevas le lit et ne fait qu'y appliquer des gestes.
  let {
    editeur,
    grille = $bindable(true),
    main = $bindable(false),
    miniCarte = $bindable(true),
    zoom = $bindable(1),
    trouves = null,
    onconnecter,
    ondeposer,
    onretirer,
    oncopier,
    ondupliquer,
  }: {
    editeur: EditeurSchema;
    /** Aimantation à la grille. */
    grille?: boolean;
    /** Outil « main » : tout glissement déplace le plan. */
    main?: boolean;
    /** Mini-carte affichée en bas à droite. */
    miniCarte?: boolean;
    /** Niveau de zoom (lecture seule pour le parent). */
    zoom?: number;
    /** Recherche en cours (D49) : les autres équipements sont estompés. */
    trouves?: Set<string> | null;
    /** Geste de connexion : de `source`, vers `destination` si lâché sur un équipement. */
    onconnecter: (source: string, destination?: string) => void;
    /** Un élément de la bibliothèque est déposé sur le plan. */
    ondeposer: (genre: string, point: Point) => void;
    /** Actions rapides sur la sélection (barre au-dessus de l'objet choisi). */
    onretirer: () => void;
    oncopier: () => void;
    ondupliquer: () => void;
  } = $props();

  const PAS = 20;
  const schema = $derived(editeur.schema);
  const v = $derived(vueDe(schema.vue));
  const caches = $derived(masques(schema));
  const parId = $derived(new Map(schema.equipements.map((e) => [e.id, e])));

  let svg = $state<SVGSVGElement>();
  let largeur = $state(800);
  let hauteur = $state(600);
  let camera = $state({ x: -400, y: -300, zoom: 1 });
  let espace = $state(false);
  const plan = $derived(espace || main);

  $effect(() => {
    zoom = camera.zoom;
  });

  type Geste =
    | { genre: "plan"; depart: Point; camera: { x: number; y: number } }
    | { genre: "rectangle"; depart: Point; courant: Point; ajouter: boolean }
    | {
        genre: "deplacement";
        depart: Point;
        origines: Map<string, Point>;
        avant: string;
        bouge: boolean;
        /** Objet saisi : c'est lui qui s'aligne sur les autres. */
        meneur: string;
      }
    | { genre: "pointe"; annotation: string; bout: "position" | "vers"; avant: string }
    | {
        genre: "coulisse";
        lien: string;
        depart: Point;
        avant: string;
        bouge: boolean;
        /** Ce que le geste fait glisser, décidé au premier mouvement (D53). */
        mode: Coulisse | null;
      }
    | { genre: "connexion"; source: string; courant: Point }
    | { genre: "passage"; lien: string; indice: number; avant: string; relation: boolean }
    | {
        genre: "taille";
        groupe: string;
        /** Poignée saisie : un coin ou un côté (D51). */
        poignee: Poignee;
        depart: Point;
        position: Point;
        taille: { largeur: number; hauteur: number };
        avant: string;
        /** Une zone (annotation) plutôt qu'un groupe. */
        annotation?: boolean;
      };
  let geste = $state<Geste | null>(null);

  /**
   * Coulisser un câble (D53) : le palier d'un Z (son segment du milieu) ; un
   * autre segment d'un câble coudé (ses coins deviennent des points de
   * passage) ; un câble droit (un point de passage naît sous le pointeur).
   */
  type Coulisse =
    | { genre: "palier"; axe: "x" | "y"; base: number }
    | { genre: "coins"; coins: Point[]; decales: number[]; axe: "x" | "y" }
    | { genre: "passage"; indice: number };

  /** Poignées d'une zone ou d'un groupe choisi : quatre coins, quatre côtés (D51). */
  type Poignee = "nw" | "n" | "ne" | "e" | "se" | "s" | "sw" | "w";
  const POIGNEES: { poignee: Poignee; u: number; v: number; curseur: string }[] = [
    { poignee: "nw", u: 0, v: 0, curseur: "nwse-resize" },
    { poignee: "n", u: 0.5, v: 0, curseur: "ns-resize" },
    { poignee: "ne", u: 1, v: 0, curseur: "nesw-resize" },
    { poignee: "e", u: 1, v: 0.5, curseur: "ew-resize" },
    { poignee: "se", u: 1, v: 1, curseur: "nwse-resize" },
    { poignee: "s", u: 0.5, v: 1, curseur: "ns-resize" },
    { poignee: "sw", u: 0, v: 1, curseur: "nesw-resize" },
    { poignee: "w", u: 0, v: 0.5, curseur: "ew-resize" },
  ];

  const visibles = $derived.by(() => {
    // Au-delà de quelques centaines d'équipements, seuls ceux du champ sont dessinés.
    const tous = schema.equipements.filter((e) => !caches.has(e.id));
    if (tous.length < 250) return tous;
    const marge = 120 / camera.zoom;
    const x0 = camera.x - marge;
    const y0 = camera.y - marge;
    const x1 = camera.x + largeur / camera.zoom + marge;
    const y1 = camera.y + hauteur / camera.zoom + marge;
    return tous.filter(
      (e) => e.position.x >= x0 && e.position.x <= x1 && e.position.y >= y0 && e.position.y <= y1,
    );
  });

  // Tracés et étiquettes : la même mise en page que les aperçus et les exports (D50).
  const page = $derived(mettreEnPage(schema, v, caches, editeur.cable?.lien));
  const affichage = $derived(page.affichage);
  const traces = $derived(page.liens);
  const tracesRelations = $derived(page.relations);
  const etiquettes = $derived([...page.etiquettes.values()].flat());
  /** Équipement survolé : ses câbles ressortent. */
  let survole = $state<string | null>(null);

  const groupesTries = $derived(
    [...schema.groupes].sort(
      (a, b) => b.taille.largeur * b.taille.hauteur - a.taille.largeur * a.taille.hauteur,
    ),
  );

  onMount(() => {
    if (!svg) return;
    const observateur = new ResizeObserver(([entree]) => {
      if (!entree) return;
      largeur = entree.contentRect.width;
      hauteur = entree.contentRect.height;
    });
    observateur.observe(svg);
    requestAnimationFrame(() => toutVoir());
    return () => observateur.disconnect();
  });

  /** Point du plan sous le pointeur. */
  function dansLePlan(evenement: { clientX: number; clientY: number }): Point {
    const cadre = svg?.getBoundingClientRect();
    return {
      x: camera.x + (evenement.clientX - (cadre?.left ?? 0)) / camera.zoom,
      y: camera.y + (evenement.clientY - (cadre?.top ?? 0)) / camera.zoom,
    };
  }

  /** Sur la grille, sauf Alt maintenu (déplacement libre, D54). */
  const aimanter = (n: number) => (grille && !sansAimant ? Math.round(n / PAS) * PAS : Math.round(n));

  /** Centre du champ visible (où poser un nouvel élément). */
  export function centre(): Point {
    return {
      x: aimanter(camera.x + largeur / camera.zoom / 2),
      y: aimanter(camera.y + hauteur / camera.zoom / 2),
    };
  }

  /** Cadre le schéma entier. */
  export function toutVoir(): void {
    const xs = schema.equipements.flatMap((e) => [e.position.x - COTE, e.position.x + COTE]);
    const ys = schema.equipements.flatMap((e) => [e.position.y - COTE, e.position.y + COTE + 30]);
    for (const g of schema.groupes) {
      xs.push(g.position.x, g.position.x + g.taille.largeur);
      ys.push(g.position.y, g.position.y + g.taille.hauteur);
    }
    if (xs.length === 0) {
      camera = { x: -largeur / 2, y: -hauteur / 2, zoom: 1 };
      return;
    }
    const x0 = Math.min(...xs) - 60;
    const y0 = Math.min(...ys) - 60;
    const l = Math.max(...xs) + 60 - x0;
    const h = Math.max(...ys) + 60 - y0;
    const zoom = Math.max(0.15, Math.min(1.5, largeur / l, hauteur / h));
    camera = { x: x0 - (largeur / zoom - l) / 2, y: y0 - (hauteur / zoom - h) / 2, zoom };
  }

  /** Amène un équipement au centre du champ. */
  /** Rend la main au plan (clavier). */
  export function focus(): void {
    svg?.focus();
  }

  /** Amène un objet (équipement, câble, annotation) au centre du champ. */
  export function montrer(id: string): void {
    const trace = traces.find((t) => t.lien.id === id)?.trace;
    const note = schema.annotations.find((n) => n.id === id);
    const cadre = note ? cadreAnnotation(note) : null;
    const p =
      parId.get(id)?.position ??
      (trace
        ? milieu(trace)
        : cadre
          ? { x: cadre.x + cadre.largeur / 2, y: cadre.y + cadre.hauteur / 2 }
          : null);
    if (!p) return;
    camera = { ...camera, x: p.x - largeur / camera.zoom / 2, y: p.y - hauteur / camera.zoom / 2 };
  }

  export function zoomer(facteur: number, autour?: Point): void {
    const nouveau = Math.max(0.1, Math.min(3, camera.zoom * facteur));
    const pivot = autour ?? {
      x: camera.x + largeur / camera.zoom / 2,
      y: camera.y + hauteur / camera.zoom / 2,
    };
    camera = {
      zoom: nouveau,
      x: pivot.x - ((pivot.x - camera.x) * camera.zoom) / nouveau,
      y: pivot.y - ((pivot.y - camera.y) * camera.zoom) / nouveau,
    };
  }

  /** Taille réelle (100 %), autour du centre du champ. */
  export function tailleReelle(): void {
    zoomer(1 / camera.zoom);
  }

  /** Fait glisser le champ de `dx`, `dy` pixels d'écran. */
  export function deplacer(dx: number, dy: number): void {
    camera = { ...camera, x: camera.x + dx / camera.zoom, y: camera.y + dy / camera.zoom };
  }

  /**
   * Molette et pavé tactile, comme dans les logiciels de dessin : défiler
   * (deux doigts, molette, Maj+molette à l'horizontale) fait glisser le plan ;
   * pincer ou Ctrl+molette zoome autour du pointeur, d'autant plus que le geste
   * est ample (un pincement suit les doigts, un cran de molette vaut 25 %).
   */
  function molette(evenement: WheelEvent) {
    evenement.preventDefault();
    const echelle = evenement.deltaMode === 1 ? 16 : evenement.deltaMode === 2 ? hauteur : 1;
    const dx = evenement.deltaX * echelle;
    const dy = evenement.deltaY * echelle;
    if (evenement.ctrlKey || evenement.metaKey) {
      zoomer(Math.min(1.25, Math.max(0.8, Math.exp(-dy / 100))), dansLePlan(evenement));
    } else if (evenement.shiftKey && dx === 0) {
      deplacer(dy, 0);
    } else {
      deplacer(dx, dy);
    }
  }

  /** Clavier du plan, quand rien n'est choisi : flèches pour le parcourir, + − 0 1 pour le zoom, H pour la main. */
  function clavierDuPlan(evenement: KeyboardEvent) {
    if (evenement.defaultPrevented || evenement.ctrlKey || evenement.metaKey || evenement.altKey) return;
    const pas = evenement.shiftKey ? 400 : 80;
    if (evenement.key.startsWith("Arrow") && editeur.selection.length === 0) {
      evenement.preventDefault();
      deplacer(
        evenement.key === "ArrowLeft" ? -pas : evenement.key === "ArrowRight" ? pas : 0,
        evenement.key === "ArrowUp" ? -pas : evenement.key === "ArrowDown" ? pas : 0,
      );
    } else if (evenement.key === "+" || evenement.key === "=") {
      evenement.preventDefault();
      zoomer(1.25);
    } else if (evenement.key === "-") {
      evenement.preventDefault();
      zoomer(0.8);
    } else if (evenement.key === "0") {
      evenement.preventDefault();
      toutVoir();
    } else if (evenement.key === "1") {
      evenement.preventDefault();
      tailleReelle();
    } else if (evenement.key.toLowerCase() === "h") {
      evenement.preventDefault();
      main = !main;
    }
  }

  // Défilement automatique : un objet (ou un fil, un rectangle de sélection)
  // amené près du bord du plan le fait glisser, sans lâcher.
  const BORD = 40;
  let dernierPointeur: { clientX: number; clientY: number } | null = null;
  /**
   * Alt maintenu (D54), comme dans les logiciels de dessin : tout ce qui se
   * déplace, se redimensionne ou coulisse (équipements, zones, annotations,
   * points de passage, paliers, bouts de flèche) va au pixel près, sans grille
   * ni aimantation. Réglé à chaque mouvement du pointeur et à chaque appui
   * ou relâchement de la touche.
   */
  let sansAimant = false;
  /** Guides d'alignement affichés pendant un déplacement. */
  let guides = $state<{ x: number | null; y: number | null } | null>(null);
  let defilement = 0;

  function vitesseAuBord(): Point {
    const cadre = svg?.getBoundingClientRect();
    if (!cadre || !dernierPointeur) return { x: 0, y: 0 };
    const axe = (p: number, debut: number, fin: number) =>
      Math.max(
        -BORD,
        Math.min(BORD, p < debut + BORD ? p - debut - BORD : p > fin - BORD ? p - fin + BORD : 0),
      );
    return {
      x: axe(dernierPointeur.clientX, cadre.left, cadre.right),
      y: axe(dernierPointeur.clientY, cadre.top, cadre.bottom),
    };
  }

  function avancerAuBord() {
    defilement = 0;
    if (!geste || geste.genre === "plan" || !dernierPointeur) return;
    const vitesse = vitesseAuBord();
    if (vitesse.x === 0 && vitesse.y === 0) return;
    deplacer(vitesse.x * 0.3, vitesse.y * 0.3);
    suivre(dernierPointeur);
    defilement = requestAnimationFrame(avancerAuBord);
  }

  function arreterDefilement() {
    if (defilement) cancelAnimationFrame(defilement);
    defilement = 0;
    dernierPointeur = null;
  }

  function capturer(evenement: PointerEvent) {
    (evenement.currentTarget as Element).setPointerCapture?.(evenement.pointerId);
  }

  function appuiSurLePlan(evenement: PointerEvent) {
    if (evenement.button === 2) return;
    svg?.focus();
    capturer(evenement);
    menuPorts = null;
    // Câble en main : le plan se fait glisser, on ne sélectionne pas.
    if (evenement.button === 1 || plan || editeur.cable) {
      geste = {
        genre: "plan",
        depart: { x: evenement.clientX, y: evenement.clientY },
        camera: { x: camera.x, y: camera.y },
      };
      return;
    }
    const point = dansLePlan(evenement);
    const zone = zoneSous(point);
    if (zone && editeur.estChoisi(zone) && !evenement.shiftKey && !evenement.ctrlKey) {
      geste = {
        genre: "deplacement",
        depart: point,
        origines: origines(),
        avant: editeur.commencerGeste(),
        bouge: false,
        meneur: zone,
      };
      return;
    }
    if (!evenement.shiftKey && !evenement.ctrlKey) editeur.selection = [];
    geste = {
      genre: "rectangle",
      depart: point,
      courant: point,
      ajouter: evenement.shiftKey || evenement.ctrlKey,
    };
  }

  /**
   * La zone (ou le groupe) sous un point du plan, sans équipement ni câble
   * dessus : la plus petite qui le contient (D51).
   */
  function zoneSous(point: Point): string | null {
    let trouvee: string | null = null;
    let plusPetite = Number.POSITIVE_INFINITY;
    const essayer = (id: string, x: number, y: number, l: number, h: number, ellipse: boolean) => {
      const [u, v] = [(point.x - x) / l - 0.5, (point.y - y) / h - 0.5];
      const dedans = ellipse ? u * u + v * v <= 0.25 : Math.abs(u) <= 0.5 && Math.abs(v) <= 0.5;
      if (dedans && l * h < plusPetite) {
        plusPetite = l * h;
        trouvee = id;
      }
    };
    for (const n of zones)
      essayer(n.id, n.position.x, n.position.y, n.largeur ?? 240, n.hauteur ?? 160, n.forme === "ellipse");
    for (const g of schema.groupes)
      if (!g.verrou) essayer(g.id, g.position.x, g.position.y, g.taille.largeur, g.taille.hauteur, false);
    return trouvee;
  }

  /** Ce qui bouge avec la sélection : équipements non verrouillés, groupes (et leurs membres), annotations. */
  function origines(): Map<string, Point> {
    const choisis = new Set(editeur.selection);
    const resultat = new Map<string, Point>();
    for (const g of schema.groupes) {
      if (!choisis.has(g.id) || g.verrou) continue;
      resultat.set(g.id, { ...g.position });
      for (const m of g.membres) choisis.add(m);
    }
    for (const e of schema.equipements)
      if (choisis.has(e.id) && !e.verrou) resultat.set(e.id, { ...e.position });
    for (const n of schema.annotations) {
      if (!choisis.has(n.id)) continue;
      resultat.set(n.id, { ...n.position });
      if (n.vers) resultat.set(`${n.id}>vers`, { ...n.vers });
    }
    for (const l of schema.liens) {
      if (!resultat.has(l.a.equipement) || !resultat.has(l.b.equipement)) continue;
      l.points.forEach((p, i) => {
        resultat.set(`${l.id}#${i}`, { ...p });
      });
      if (l.palier) resultat.set(`${l.id}>palier`, { x: l.palier.position, y: l.palier.position });
    }
    return resultat;
  }

  function appuiSurObjet(evenement: PointerEvent, id: string) {
    if (evenement.button !== 0 || plan) return;
    if (editeur.cable) {
      // Câble en main : un équipement cliqué ouvre le menu de ses ports.
      if (!parId.has(id)) return;
      evenement.stopPropagation();
      ouvrirPorts(id, evenement);
      return;
    }
    evenement.stopPropagation();
    svg?.focus();
    capturer(evenement);
    if (evenement.shiftKey || evenement.ctrlKey) {
      editeur.choisir(id, true);
      return;
    }
    if (!editeur.estChoisi(id)) editeur.choisir(id);
    geste = {
      genre: "deplacement",
      depart: dansLePlan(evenement),
      origines: origines(),
      avant: editeur.commencerGeste(),
      bouge: false,
      meneur: id,
    };
  }

  function appuiSurPoignee(evenement: PointerEvent, equipement: Equipement) {
    if (evenement.button !== 0 || plan || editeur.cable) return;
    evenement.stopPropagation();
    capturer(evenement);
    geste = { genre: "connexion", source: equipement.id, courant: dansLePlan(evenement) };
  }

  function appuiSurLien(evenement: PointerEvent, id: string) {
    if (evenement.button !== 0 || plan || editeur.cable) return;
    evenement.stopPropagation();
    svg?.focus();
    const ajouter = evenement.shiftKey || evenement.ctrlKey;
    editeur.choisir(id, ajouter);
    // Un câble saisi puis tiré coulisse (D53).
    if (ajouter || !schema.liens.some((l) => l.id === id)) return;
    capturer(evenement);
    geste = {
      genre: "coulisse",
      lien: id,
      depart: dansLePlan(evenement),
      avant: editeur.commencerGeste(),
      bouge: false,
      mode: null,
    };
  }

  /** Indice du segment d'un tracé le plus proche d'un point. */
  function segmentProche(trace: Point[], p: Point): number {
    let meilleur = 0;
    let distance = Number.POSITIVE_INFINITY;
    for (let i = 0; i < trace.length - 1; i++) {
      const a = trace[i] as Point;
      const b = trace[i + 1] as Point;
      const l2 = (b.x - a.x) ** 2 + (b.y - a.y) ** 2 || 1;
      const t = Math.max(0, Math.min(1, ((p.x - a.x) * (b.x - a.x) + (p.y - a.y) * (b.y - a.y)) / l2));
      const d = Math.hypot(a.x + (b.x - a.x) * t - p.x, a.y + (b.y - a.y) * t - p.y);
      if (d < distance) {
        distance = d;
        meilleur = i;
      }
    }
    return meilleur;
  }

  /** Ce que fait glisser un câble saisi en `point` (voir `Coulisse`). */
  function modeDeCoulisse(lien: Lien, point: Point): Coulisse | null {
    const t = traces.find((x) => x.lien.id === lien.id);
    const a = parId.get(lien.a.equipement);
    const b = parId.get(lien.b.equipement);
    if (!t || !a || !b) return null;
    const k = segmentProche(t.trace, point);
    const debut = t.trace[k] as Point;
    const fin = t.trace[k + 1] as Point;
    // Le segment du milieu d'un Z : son palier, qui reste en place quand les équipements bougent.
    if (t.route?.forme.startsWith("z") && t.trace.length === 4 && k === 1) {
      const axe = t.route.forme === "z-vertical" ? "y" : "x";
      const base =
        t.route.palier ?? Math.round((a.position[axe] + b.position[axe]) / 2) + (t.route.milieu ?? 0);
      return { genre: "palier", axe, base };
    }
    // Un autre segment d'un câble coudé : ses coins deviennent des points de passage.
    const vertical = Math.abs(debut.x - fin.x) < 0.5;
    if (t.coude && (vertical || Math.abs(debut.y - fin.y) < 0.5)) {
      const coins = (t.trace.length === 2 ? t.trace : t.trace.slice(1, -1)).map((p) => ({
        x: Math.round(p.x),
        y: Math.round(p.y),
      }));
      const decales = t.trace.length === 2 ? [0, 1] : [k - 1, k].filter((i) => i >= 0 && i < coins.length);
      return { genre: "coins", coins, decales, axe: vertical ? "x" : "y" };
    }
    // Un câble droit : un point de passage là où on l'a saisi, dans le segment le plus proche.
    const centres = [a.position, ...lien.points, b.position];
    const indice = segmentProche(centres, point);
    lien.points.splice(indice, 0, { x: aimanter(point.x), y: aimanter(point.y) });
    return { genre: "passage", indice };
  }

  function appuiSurPassage(evenement: PointerEvent, lien: string, indice: number, relation: boolean) {
    if (evenement.button !== 0 || plan) return;
    evenement.stopPropagation();
    capturer(evenement);
    geste = { genre: "passage", lien, indice, avant: editeur.commencerGeste(), relation };
  }

  /** Double-clic sur un tracé : un point de passage de plus ; sur un point : il est retiré. */
  function ajouterPassage(evenement: MouseEvent, id: string, relation: boolean) {
    evenement.stopPropagation();
    const point = dansLePlan(evenement);
    sansAimant = evenement.altKey;
    editeur.agir((s) => {
      const objet = relation ? s.relations.find((r) => r.id === id) : s.liens.find((l) => l.id === id);
      if (!objet) return;
      const de = parId.get(
        relation ? (objet as { de: string }).de : (objet as { a: { equipement: string } }).a.equipement,
      );
      const vers = parId.get(
        relation ? (objet as { vers: string }).vers : (objet as { b: { equipement: string } }).b.equipement,
      );
      if (!de || !vers) return;
      const trace = [de.position, ...objet.points, vers.position];
      // Le point s'insère dans le segment le plus proche.
      let meilleur = 0;
      let distance = Number.POSITIVE_INFINITY;
      for (let i = 0; i < trace.length - 1; i++) {
        const a = trace[i] as Point;
        const b = trace[i + 1] as Point;
        const d = Math.hypot((a.x + b.x) / 2 - point.x, (a.y + b.y) / 2 - point.y);
        if (d < distance) {
          distance = d;
          meilleur = i;
        }
      }
      objet.points.splice(meilleur, 0, { x: aimanter(point.x), y: aimanter(point.y) });
    });
  }

  function retirerPassage(evenement: MouseEvent, id: string, indice: number, relation: boolean) {
    evenement.stopPropagation();
    editeur.agir((s) => {
      const objet = relation ? s.relations.find((r) => r.id === id) : s.liens.find((l) => l.id === id);
      objet?.points.splice(indice, 1);
    });
  }

  function appuiSurTaille(evenement: PointerEvent, groupe: string, poignee: Poignee, annotation = false) {
    if (evenement.button !== 0 || plan) return;
    evenement.stopPropagation();
    capturer(evenement);
    const n = annotation ? schema.annotations.find((x) => x.id === groupe) : undefined;
    const g = annotation ? undefined : schema.groupes.find((x) => x.id === groupe);
    const taille = n ? { largeur: n.largeur ?? 240, hauteur: n.hauteur ?? 160 } : g?.taille;
    const position = n?.position ?? g?.position;
    if (!taille || !position) return;
    geste = {
      genre: "taille",
      groupe,
      poignee,
      depart: dansLePlan(evenement),
      position: { ...position },
      taille: { ...taille },
      avant: editeur.commencerGeste(),
      annotation,
    };
  }

  /** Cadre d'une zone ou d'un groupe tiré par une poignée : le côté opposé ne bouge pas. */
  function cadreTire(
    g: Extract<Geste, { genre: "taille" }>,
    point: Point,
    minimum: { largeur: number; hauteur: number },
  ) {
    const dx = point.x - g.depart.x;
    const dy = point.y - g.depart.y;
    let { x, y } = g.position;
    let { largeur, hauteur } = g.taille;
    if (g.poignee.includes("e"))
      largeur = Math.max(minimum.largeur, aimanter(g.position.x + largeur + dx) - x);
    if (g.poignee.includes("s"))
      hauteur = Math.max(minimum.hauteur, aimanter(g.position.y + hauteur + dy) - y);
    if (g.poignee.includes("w")) {
      const droite = g.position.x + g.taille.largeur;
      x = Math.min(aimanter(g.position.x + dx), droite - minimum.largeur);
      largeur = droite - x;
    }
    if (g.poignee.includes("n")) {
      const bas = g.position.y + g.taille.hauteur;
      y = Math.min(aimanter(g.position.y + dy), bas - minimum.hauteur);
      hauteur = bas - y;
    }
    return { position: { x, y }, taille: { largeur, hauteur } };
  }

  function appuiSurPointe(evenement: PointerEvent, annotation: string, bout: "position" | "vers") {
    if (evenement.button !== 0 || plan) return;
    evenement.stopPropagation();
    capturer(evenement);
    geste = { genre: "pointe", annotation, bout, avant: editeur.commencerGeste() };
  }

  function mouvement(evenement: PointerEvent) {
    if (editeur.cable) pointeur = dansLePlan(evenement);
    if (!geste) return;
    if (geste.genre === "plan") {
      camera = {
        ...camera,
        x: geste.camera.x - (evenement.clientX - geste.depart.x) / camera.zoom,
        y: geste.camera.y - (evenement.clientY - geste.depart.y) / camera.zoom,
      };
      return;
    }
    dernierPointeur = { clientX: evenement.clientX, clientY: evenement.clientY };
    sansAimant = evenement.altKey;
    suivre(evenement);
    const vitesse = vitesseAuBord();
    if (!defilement && (vitesse.x !== 0 || vitesse.y !== 0))
      defilement = requestAnimationFrame(avancerAuBord);
  }

  /** Le geste en cours suit le pointeur (aussi pendant le défilement automatique). */
  function suivre(pointeur: { clientX: number; clientY: number }) {
    if (!geste || geste.genre === "plan") return;
    const point = dansLePlan(pointeur);
    if (geste.genre === "rectangle") geste.courant = point;
    else if (geste.genre === "connexion") geste.courant = point;
    else if (geste.genre === "deplacement") {
      const dx = point.x - geste.depart.x;
      const dy = point.y - geste.depart.y;
      if (!geste.bouge && Math.hypot(dx, dy) < 3 / camera.zoom) return;
      geste.bouge = true;
      // L'équipement saisi s'aligne sur le centre d'un autre (à 8 px d'écran
      // près) ; sinon sur la grille. Tout le reste suit du même décalage.
      // Alt maintenu : ni aimantation, ni grille.
      const meneur = parId.has(geste.meneur) ? geste.origines.get(geste.meneur) : undefined;
      let decalage: Point | null = null;
      guides = null;
      if (meneur) {
        const voulu = { x: meneur.x + dx, y: meneur.y + dy };
        const seuil = 8 / camera.zoom;
        let gx: number | null = null;
        let gy: number | null = null;
        if (!sansAimant) {
          for (const e of schema.equipements) {
            if (geste.origines.has(e.id) || caches.has(e.id)) continue;
            if (
              Math.abs(e.position.x - voulu.x) < seuil &&
              (gx === null || Math.abs(e.position.x - voulu.x) < Math.abs(gx - voulu.x))
            )
              gx = e.position.x;
            if (
              Math.abs(e.position.y - voulu.y) < seuil &&
              (gy === null || Math.abs(e.position.y - voulu.y) < Math.abs(gy - voulu.y))
            )
              gy = e.position.y;
          }
        }
        decalage = { x: (gx ?? aimanter(voulu.x)) - meneur.x, y: (gy ?? aimanter(voulu.y)) - meneur.y };
        if (gx !== null || gy !== null) guides = { x: gx, y: gy };
      }
      for (const [id, origine] of geste.origines) {
        const cible = decalage
          ? { x: origine.x + decalage.x, y: origine.y + decalage.y }
          : { x: aimanter(origine.x + dx), y: aimanter(origine.y + dy) };
        if (id.endsWith(">vers")) {
          const n = schema.annotations.find((x) => x.id === id.slice(0, -5));
          if (n) n.vers = cible;
          continue;
        }
        if (id.endsWith(">palier")) {
          const l = schema.liens.find((x) => x.id === id.slice(0, -7));
          if (l?.palier) l.palier = { ...l.palier, position: l.palier.axe === "y" ? cible.y : cible.x };
          continue;
        }
        if (id.includes("#")) {
          const [lienId, indice] = id.split("#");
          const lien = schema.liens.find((l) => l.id === lienId);
          if (lien) lien.points[Number(indice)] = cible;
          continue;
        }
        const objet =
          parId.get(id) ??
          schema.groupes.find((g) => g.id === id) ??
          schema.annotations.find((n) => n.id === id);
        if (objet) objet.position = cible;
      }
    } else if (geste.genre === "coulisse") {
      const g = geste;
      const dx = point.x - g.depart.x;
      const dy = point.y - g.depart.y;
      if (!g.bouge && Math.hypot(dx, dy) < 3 / camera.zoom) return;
      const lien = schema.liens.find((l) => l.id === g.lien);
      if (!lien) return;
      if (!g.bouge) {
        g.bouge = true;
        g.mode = modeDeCoulisse(lien, g.depart);
      }
      const mode = g.mode;
      if (mode?.genre === "palier") {
        lien.palier = { axe: mode.axe, position: aimanter(mode.base + (mode.axe === "y" ? dy : dx)) };
      } else if (mode?.genre === "coins") {
        lien.points = mode.coins.map((c, i) =>
          !mode.decales.includes(i)
            ? c
            : mode.axe === "x"
              ? { x: aimanter(c.x + dx), y: c.y }
              : { x: c.x, y: aimanter(c.y + dy) },
        );
        delete lien.palier;
      } else if (mode?.genre === "passage") {
        lien.points[mode.indice] = { x: aimanter(point.x), y: aimanter(point.y) };
      }
    } else if (geste.genre === "passage") {
      const g = geste;
      const objet = g.relation
        ? schema.relations.find((r) => r.id === g.lien)
        : schema.liens.find((l) => l.id === g.lien);
      if (objet) objet.points[g.indice] = { x: aimanter(point.x), y: aimanter(point.y) };
    } else if (geste.genre === "pointe") {
      const g = geste;
      const n = schema.annotations.find((x) => x.id === g.annotation);
      if (n) n[g.bout] = { x: aimanter(point.x), y: aimanter(point.y) };
    } else if (geste.genre === "taille" && geste.annotation) {
      const g = geste;
      const n = schema.annotations.find((x) => x.id === g.groupe);
      if (n) {
        const cadre = cadreTire(g, point, { largeur: 40, hauteur: 30 });
        n.position = cadre.position;
        n.largeur = cadre.taille.largeur;
        n.hauteur = cadre.taille.hauteur;
      }
    } else if (geste.genre === "taille") {
      const g = geste;
      const groupe = schema.groupes.find((x) => x.id === g.groupe);
      if (groupe) {
        const cadre = cadreTire(g, point, { largeur: 80, hauteur: 60 });
        groupe.position = cadre.position;
        groupe.taille = cadre.taille;
      }
    }
  }

  function relachement(evenement: PointerEvent) {
    arreterDefilement();
    guides = null;
    sansAimant = false;
    const fini = geste;
    geste = null;
    if (!fini) return;
    if (fini.genre === "rectangle") {
      const x0 = Math.min(fini.depart.x, fini.courant.x);
      const x1 = Math.max(fini.depart.x, fini.courant.x);
      const y0 = Math.min(fini.depart.y, fini.courant.y);
      const y1 = Math.max(fini.depart.y, fini.courant.y);
      if (x1 - x0 < 4 && y1 - y0 < 4) {
        const zone = zoneSous(fini.depart);
        if (zone) editeur.selection = fini.ajouter ? [...new Set([...editeur.selection, zone])] : [zone];
        return;
      }
      const dedans = visibles
        .filter((e) => e.position.x >= x0 && e.position.x <= x1 && e.position.y >= y0 && e.position.y <= y1)
        .map((e) => e.id);
      editeur.selection = fini.ajouter ? [...new Set([...editeur.selection, ...dedans])] : dedans;
    } else if (
      fini.genre === "deplacement" ||
      fini.genre === "passage" ||
      fini.genre === "taille" ||
      fini.genre === "pointe" ||
      fini.genre === "coulisse"
    ) {
      editeur.finirGeste(fini.avant);
    } else if (fini.genre === "connexion") {
      const point = dansLePlan(evenement);
      const cible = visibles.find(
        (e) =>
          e.id !== fini.source &&
          Math.abs(e.position.x - point.x) <= COTE / 2 + 6 &&
          Math.abs(e.position.y - point.y) <= COTE / 2 + 6,
      );
      onconnecter(fini.source, cible?.id);
    }
  }

  function deposer(evenement: DragEvent) {
    const genre = evenement.dataTransfer?.getData("application/x-kyfrev-topologie");
    if (!genre) return;
    evenement.preventDefault();
    const point = dansLePlan(evenement);
    sansAimant = evenement.altKey;
    ondeposer(genre, { x: aimanter(point.x), y: aimanter(point.y) });
    sansAimant = false;
  }

  const rectangle = $derived(
    geste?.genre === "rectangle"
      ? {
          x: Math.min(geste.depart.x, geste.courant.x),
          y: Math.min(geste.depart.y, geste.courant.y),
          l: Math.abs(geste.courant.x - geste.depart.x),
          h: Math.abs(geste.courant.y - geste.depart.y),
        }
      : null,
  );
  const fil = $derived.by(() => {
    if (geste?.genre !== "connexion") return null;
    const source = parId.get(geste.source);
    return source ? { de: source.position, vers: geste.courant } : null;
  });

  // Mini-carte : tout le schéma et le champ visible ; un clic ou un glissement
  // y amène le champ. L'étendue est figée pendant le glissement (sinon elle
  // suivrait le champ qui bouge).
  let carte = $state<SVGSVGElement>();
  type Etendue = { x: number; y: number; largeur: number; hauteur: number };
  let carteFigee = $state<Etendue | null>(null);
  const etendueCalculee = $derived.by((): Etendue | null => {
    const presents = schema.equipements.filter((e) => !caches.has(e.id));
    if (presents.length === 0) return null;
    const xs = [camera.x, camera.x + largeur / camera.zoom];
    const ys = [camera.y, camera.y + hauteur / camera.zoom];
    for (const e of presents) xs.push(e.position.x - COTE / 2, e.position.x + COTE / 2);
    for (const e of presents) ys.push(e.position.y - COTE / 2, e.position.y + COTE / 2);
    for (const g of schema.groupes) {
      xs.push(g.position.x, g.position.x + g.taille.largeur);
      ys.push(g.position.y, g.position.y + g.taille.hauteur);
    }
    const x = Math.min(...xs) - 40;
    const y = Math.min(...ys) - 40;
    return { x, y, largeur: Math.max(...xs) + 40 - x, hauteur: Math.max(...ys) + 40 - y };
  });
  const etendue = $derived(carteFigee ?? etendueCalculee);

  function centrerSur(evenement: PointerEvent) {
    const matrice = carte?.getScreenCTM();
    if (!carte || !matrice) return;
    const p = new DOMPoint(evenement.clientX, evenement.clientY).matrixTransform(matrice.inverse());
    camera = { ...camera, x: p.x - largeur / camera.zoom / 2, y: p.y - hauteur / camera.zoom / 2 };
  }

  function appuiSurCarte(evenement: PointerEvent) {
    if (evenement.button !== 0) return;
    (evenement.currentTarget as Element).setPointerCapture?.(evenement.pointerId);
    carteFigee = etendueCalculee;
    centrerSur(evenement);
  }

  // Câble en main (D42) : il suit le pointeur ; un équipement cliqué ouvre le
  // menu de ses ports ; le second port choisi branche le câble.
  let pointeur = $state<Point | null>(null);
  let menuPorts = $state<{ equipement: string; x: number; y: number } | null>(null);

  $effect(() => {
    if (!editeur.cable) {
      menuPorts = null;
      pointeur = null;
    }
  });

  const versEcran = (p: Point) => ({ x: (p.x - camera.x) * camera.zoom, y: (p.y - camera.y) * camera.zoom });

  function ouvrirPorts(equipement: string, evenement?: { clientX: number; clientY: number }) {
    const cadre = svg?.getBoundingClientRect();
    const e = parId.get(equipement);
    if (!cadre || !e) return;
    const ecran = evenement
      ? { x: evenement.clientX - cadre.left, y: evenement.clientY - cadre.top }
      : versEcran({ x: e.position.x + COTE / 2, y: e.position.y });
    menuPorts = {
      equipement,
      x: Math.max(8, Math.min(ecran.x + 12, cadre.width - 308)),
      y: Math.max(8, Math.min(ecran.y - 20, cadre.height - 380)),
    };
  }

  /** Prend un câble en main depuis un équipement (barre rapide) : son menu de ports s'ouvre. */
  function cableDepuis(equipement: string) {
    editeur.cable = { support: "auto", depart: null };
    editeur.selection = [];
    ouvrirPorts(equipement);
  }

  /** Bout d'un câble existant pris en main : l'autre bout reste branché. */
  function detacher(evenement: PointerEvent, lien: string, bout: "a" | "b") {
    if (evenement.button !== 0 || plan) return;
    evenement.stopPropagation();
    const l = schema.liens.find((x) => x.id === lien);
    if (!l) return;
    editeur.selection = [];
    editeur.cable = { support: l.support, depart: { ...(bout === "a" ? l.b : l.a) }, lien, bout };
    pointeur = dansLePlan(evenement);
  }

  function brancher(interfaceId: string) {
    const menu = menuPorts;
    const cable = editeur.cable;
    if (!menu || !cable) return;
    const vers = { equipement: menu.equipement, interface: interfaceId };
    menuPorts = null;
    if (!cable.depart) {
      editeur.cable = { ...cable, depart: vers };
      return;
    }
    const depart = cable.depart;
    if (cable.lien && cable.bout) {
      const { lien, bout } = cable;
      if (editeur.agir((s) => rebrancher(s, lien, bout, vers), "Câble rebranché.")) {
        editeur.cable = null;
        editeur.selection = [lien];
      }
      return;
    }
    let cree = "";
    const ok = editeur.agir((s) => {
      cree = connecter(s, depart, vers, cable.support === "auto" ? {} : { support: cable.support }).id;
    }, "Câble branché.");
    if (ok) {
      editeur.cable = null;
      editeur.selection = [cree];
    }
  }

  /** Aucun port libre (un PC n'en a qu'un) : un port de plus, où le câble se branche aussitôt. */
  function ajouterPortEtBrancher() {
    const menu = menuPorts;
    if (!menu) return;
    let nouveau = "";
    const fibre = editeur.cable?.support.startsWith("fibre") ?? false;
    const ok = editeur.agir((s) => {
      nouveau = ajouterInterface(s, menu.equipement, fibre ? { genre: "fibre" } : {}).id;
    });
    if (ok && nouveau) brancher(nouveau);
  }

  const nomDuSupport = (support: Support | "auto") =>
    support === "auto"
      ? "automatique"
      : (SUPPORTS.find((x) => x.valeur === support)?.nom ?? support).toLowerCase();

  const consigneCable = $derived.by(() => {
    const cable = editeur.cable;
    if (!cable) return null;
    if (cable.lien) {
      const fixe = cable.depart ? parId.get(cable.depart.equipement)?.nom : "";
      return `Bout de câble en main (l'autre reste sur ${fixe}) : cliquez l'équipement où le brancher.`;
    }
    return cable.depart
      ? `Câble ${nomDuSupport(cable.support)} branché sur ${parId.get(cable.depart.equipement)?.nom ?? "?"} : cliquez le second équipement.`
      : `Câble ${nomDuSupport(cable.support)} en main : cliquez le premier équipement.`;
  });

  /** Le câble en main : de son bout branché jusqu'au pointeur. */
  const cableEnMain = $derived.by(() => {
    const cable = editeur.cable;
    if (!cable || !pointeur) return null;
    const trait = traitDuSupport(cable.support === "auto" ? "cuivre" : cable.support);
    const de = cable.depart ? parId.get(cable.depart.equipement) : undefined;
    return {
      trait,
      depart: de
        ? (traceEntre(de.position, pointeur, [], 0, [boiteDe(de), undefined])[0] ?? de.position)
        : null,
      bout: pointeur,
    };
  });

  /** Bouts d'un câble (pour le détacher) : juste au bord de chaque équipement. */
  function boutsDe(trace: Point[]): [Point, Point] {
    const pres = (a: Point | undefined, b: Point | undefined): Point => {
      if (!a) return { x: 0, y: 0 };
      if (!b) return a;
      const l = Math.hypot(b.x - a.x, b.y - a.y) || 1;
      const t = Math.min(0.3, 8 / l);
      return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t };
    };
    return [pres(trace[0], trace[1]), pres(trace.at(-1), trace.at(-2))];
  }

  // Barre d'actions rapides, au-dessus de l'objet choisi.
  const barre = $derived.by(() => {
    if (geste || editeur.cable || editeur.selection.length === 0) return null;
    const equipements = editeur.selection.map((id) => parId.get(id)).filter((e) => e !== undefined);
    if (editeur.selection.length === 1 && equipements.length === 1) {
      const e = equipements[0] as Equipement;
      const haut = versEcran({ x: e.position.x, y: e.position.y - COTE / 2 });
      return { genre: "equipement" as const, equipement: e, x: haut.x, y: haut.y - 10 };
    }
    const note =
      editeur.selection.length === 1
        ? schema.annotations.find((n) => n.id === editeur.selection[0])
        : undefined;
    if (note) {
      const c = cadreAnnotation(note);
      const haut = versEcran({ x: c.x + c.largeur / 2, y: c.y });
      return { genre: "annotation" as const, annotation: note, x: haut.x, y: haut.y - 10 };
    }
    if (editeur.selection.length === 1) {
      const t = traces.find((x) => x.lien.id === editeur.selection[0]);
      if (!t) return null;
      const m = versEcran(milieu(t.trace));
      return { genre: "lien" as const, lien: t.lien, x: m.x, y: m.y + 16 };
    }
    if (equipements.length === 0) return null;
    const x = equipements.reduce((n, e) => n + e.position.x, 0) / equipements.length;
    const y = Math.min(...equipements.map((e) => e.position.y)) - COTE / 2;
    const haut = versEcran({ x, y });
    return {
      genre: "plusieurs" as const,
      nombre: editeur.selection.length,
      equipements: equipements.length,
      x: haut.x,
      y: haut.y - 10,
    };
  });

  let menuAligner = $state(false);
  $effect(() => {
    if (barre?.genre !== "plusieurs") menuAligner = false;
  });

  function alignerSelection(mode: Alignement | "repartir-x" | "repartir-y") {
    menuAligner = false;
    const ids = new Set(editeur.selection);
    editeur.agir(
      (s) => {
        if (mode === "repartir-x") repartir(s, ids, "x");
        else if (mode === "repartir-y") repartir(s, ids, "y");
        else aligner(s, ids, mode);
      },
      mode.startsWith("repartir") ? "Répartis à intervalles égaux." : "Alignés.",
    );
  }

  function modifierAnnotation(id: string, changer: (n: Annotation) => void) {
    editeur.agir((s) => {
      const n = s.annotations.find((x) => x.id === id);
      if (n) changer(n);
    });
  }

  const zones = $derived(affichage.zones ? schema.annotations.filter(estZone) : []);
  const autresAnnotations = $derived(
    affichage.annotations ? schema.annotations.filter((n) => !estZone(n)) : [],
  );

  function renommer(equipement: Equipement, champ: HTMLInputElement) {
    const nom = champ.value.trim();
    if (!nom) {
      champ.value = equipement.nom;
      editeur.annonce = "Un équipement doit avoir un nom.";
      return;
    }
    const id = equipement.id;
    editeur.agir((s) => {
      const e = s.equipements.find((x) => x.id === id);
      if (e) e.nom = nom;
    });
  }

  const choisis = $derived(new Set(editeur.selection));
</script>

<svelte:window
  onkeydown={(e) => {
    // Alt pendant un geste : libre tout de suite, sans attendre un mouvement (et sans réveiller le menu de la fenêtre).
    if (e.key === "Alt" && geste && geste.genre !== "plan") {
      e.preventDefault();
      sansAimant = true;
      if (dernierPointeur) suivre(dernierPointeur);
      return;
    }
    if (e.key === "Escape" && editeur.cable) {
      e.preventDefault();
      editeur.cable = null;
      return;
    }
    if (document.activeElement !== svg) return;
    if (e.key === " ") {
      espace = true;
      e.preventDefault();
    } else clavierDuPlan(e);
  }}
  onkeyup={(e) => {
    if (e.key === " ") espace = false;
    if (e.key === "Alt" && geste && geste.genre !== "plan") {
      e.preventDefault();
      sansAimant = false;
      if (dernierPointeur) suivre(dernierPointeur);
    }
  }}
/>

<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
<svg
  bind:this={svg}
  class="canevas"
  class:deplacement={geste?.genre === "plan"}
  class:main={plan}
  class:cable={editeur.cable !== null}
  role="application"
  aria-label="Plan du schéma réseau : faites glisser un équipement de la bibliothèque, Suppr retire la sélection, Ctrl+Z annule. Se déplacer : molette ou deux doigts, flèches, Espace ou H et glisser ; zoom : Ctrl+molette ou pincer, + et −, 0 pour tout voir ; Alt maintenu pendant un déplacement : sans grille ni aimantation"
  tabindex="0"
  viewBox="{camera.x} {camera.y} {largeur / camera.zoom} {hauteur / camera.zoom}"
  onpointerdown={appuiSurLePlan}
  onpointermove={mouvement}
  onpointerup={relachement}
  onpointercancel={() => {
    arreterDefilement();
    geste = null;
  }}
  onwheel={molette}
  ondragover={(e) => {
    if (e.dataTransfer?.types.includes("application/x-kyfrev-topologie")) e.preventDefault();
  }}
  ondrop={deposer}
>
  <defs>
    <pattern id="grille-topologie" width={PAS} height={PAS} patternUnits="userSpaceOnUse">
      <circle cx="0" cy="0" r={1 / camera.zoom} class="point-grille" />
    </pattern>
    <marker id="fleche-topologie" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
      <path d="M0 0 10 5 0 10z" class="pointe" />
    </marker>
    <filter id="ombre" x="-20%" y="-20%" width="140%" height="150%">
      <feDropShadow dx="0" dy="1.5" stdDeviation="1.6" flood-color="#000000" flood-opacity="0.28" />
    </filter>
    <linearGradient id="dalle" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#7cc0f0" />
      <stop offset="1" stop-color="#2f6fb7" />
    </linearGradient>
  </defs>
  {#if grille}
    <rect x={camera.x} y={camera.y} width={largeur / camera.zoom} height={hauteur / camera.zoom} fill="url(#grille-topologie)" />
  {/if}

  {#each groupesTries as g (g.id)}
    <g class="groupe" class:choisi={choisis.has(g.id)}>
      <rect
        x={g.position.x}
        y={g.position.y}
        width={g.taille.largeur}
        height={g.taille.hauteur}
        rx="10"
        fill={g.couleur ?? "var(--graphique-1)"}
        fill-opacity="0.06"
        stroke={g.couleur ?? "var(--graphique-1)"}
        stroke-dasharray="6 4"
        class="cadre-groupe"
      />
      <!-- svelte-ignore a11y_no_static_element_interactions -->
      <text
        x={g.position.x + 10}
        y={g.position.y + 18}
        class="nom-groupe"
        onpointerdown={(e) => appuiSurObjet(e, g.id)}
      >{g.nom || "Groupe"}{g.replie ? " (replié)" : ""}{g.verrou ? " 🔒" : ""}</text>
      {#if choisis.has(g.id) && !g.verrou}
        {@const cote = 11 / camera.zoom}
        {#each POIGNEES as p (p.poignee)}
          <!-- svelte-ignore a11y_no_static_element_interactions -->
          <rect
            x={g.position.x + p.u * g.taille.largeur - cote / 2}
            y={g.position.y + p.v * g.taille.hauteur - cote / 2}
            width={cote}
            height={cote}
            class="poignee-taille"
            style="cursor: {p.curseur}"
            onpointerdown={(e) => appuiSurTaille(e, g.id, p.poignee)}
          />
        {/each}
      {/if}
    </g>
  {/each}

  {#each zones as n (n.id)}
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <g class="annotation zone" class:choisi={choisis.has(n.id)} onpointerdown={(ev) => appuiSurObjet(ev, n.id)}>
      {@html dessinerAnnotation(n)}
      {#if n.forme === "ellipse"}
        <ellipse
          cx={n.position.x + (n.largeur ?? 240) / 2}
          cy={n.position.y + (n.hauteur ?? 160) / 2}
          rx={(n.largeur ?? 240) / 2}
          ry={(n.hauteur ?? 160) / 2}
          class="prise-zone"
        />
      {:else}
        <rect x={n.position.x} y={n.position.y} width={n.largeur ?? 240} height={n.hauteur ?? 160} rx="8" class="prise-zone" />
      {/if}
    </g>
  {/each}

  {#each traces as t (t.lien.id)}
    <g
      class="lien"
      class:choisi={choisis.has(t.lien.id)}
      class:prevu={t.lien.statut === "prevu"}
      class:relie={survole !== null && (t.lien.a.equipement === survole || t.lien.b.equipement === survole)}
    >
      <path
        d={cheminDe(t.trace)}
        fill="none"
        stroke={t.trait.couleur}
        stroke-width={t.trait.epaisseur}
        stroke-dasharray={t.trait.tirets}
        stroke-linecap="round"
        stroke-linejoin="round"
      />
      {#if t.agregat}
        <g pointer-events="none">{@html dessinerMarque(t.agregat, t.trait.couleur)}</g>
      {/if}
      <!-- svelte-ignore a11y_no_static_element_interactions -->
      <path
        d={cheminDe(t.trace)}
        class="prise"
        onpointerdown={(e) => appuiSurLien(e, t.lien.id)}
        ondblclick={(e) => ajouterPassage(e, t.lien.id, false)}
      />
      {#each boutsDe(t.trace) as p, i (i)}
        <!-- svelte-ignore a11y_no_static_element_interactions -->
        <g class="bout" onpointerdown={(e) => detacher(e, t.lien.id, i === 0 ? "a" : "b")}>
          <title>Détacher ce bout du câble (puis cliquer l'équipement où le brancher)</title>
          <circle cx={p.x} cy={p.y} r="11" class="prise-bout" />
          <circle cx={p.x} cy={p.y} r="5" class="pastille-bout" stroke={t.trait.couleur} />
        </g>
      {/each}
      {#if choisis.has(t.lien.id)}
        {#each t.lien.points as p, i (i)}
          <!-- svelte-ignore a11y_no_static_element_interactions -->
          <circle
            cx={p.x}
            cy={p.y}
            r="6"
            class="passage"
            onpointerdown={(e) => appuiSurPassage(e, t.lien.id, i, false)}
            ondblclick={(e) => retirerPassage(e, t.lien.id, i, false)}
          />
        {/each}
      {/if}
    </g>
  {/each}

  {#each tracesRelations as t (t.relation.id)}
    <g class="relation" class:choisi={choisis.has(t.relation.id)}>
      <path
        d={cheminDe(t.trace)}
        fill="none"
        stroke={t.trait.couleur}
        stroke-width={t.trait.epaisseur}
        stroke-dasharray={t.trait.tirets}
        marker-end={t.fleche ? "url(#fleche-topologie)" : undefined}
      />
      <!-- svelte-ignore a11y_no_static_element_interactions -->
      <path
        d={cheminDe(t.trace)}
        class="prise"
        onpointerdown={(e) => appuiSurLien(e, t.relation.id)}
        ondblclick={(e) => ajouterPassage(e, t.relation.id, true)}
      />
      {#if choisis.has(t.relation.id)}
        {#each t.relation.points as p, i (i)}
          <!-- svelte-ignore a11y_no_static_element_interactions -->
          <circle
            cx={p.x}
            cy={p.y}
            r="6"
            class="passage"
            onpointerdown={(e) => appuiSurPassage(e, t.relation.id, i, true)}
            ondblclick={(e) => retirerPassage(e, t.relation.id, i, true)}
          />
        {/each}
      {/if}
    </g>
  {/each}

  <g class="etiquettes">
    {#each etiquettes as etiquette, i (i)}
      {@html dessinerEtiquette(etiquette)}
    {/each}
  </g>

  {#each visibles as e (e.id)}
    {@const def = definition(e.type)}
    {@const couleur = couleurDe(e)}
    {@const adresse = affichage.adresses ? adresseAffichee(e) : ""}
    {@const b = boiteDe(e, 0)}
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <g
      class="equipement"
      class:choisi={choisis.has(e.id)}
      class:verrouille={e.verrou}
      class:estompe={trouves !== null && !trouves.has(e.id)}
      data-id={e.id}
      onpointerdown={(ev) => appuiSurObjet(ev, e.id)}
      onpointerenter={() => (survole = e.id)}
      onpointerleave={() => (survole = null)}
    >
      <title>{e.nom} — {def.nom}{e.verrou ? " (verrouillé)" : ""}</title>
      <rect
        x={b.x0 - 6}
        y={b.y0 - 6}
        width={b.x1 - b.x0 + 12}
        height={b.y1 - b.y0 + 12}
        rx="8"
        class="pave"
        class:prevu={e.statut === "prevu"}
      />
      <g
        filter="url(#ombre)"
        opacity={e.statut === "hors-service" ? 0.45 : e.statut === "prevu" ? 0.55 : 1}
        pointer-events="none"
      >
        <g transform="translate({e.position.x - COTE / 2} {e.position.y - COTE / 2}) scale({COTE / 64})">
          {@html illustration(def.icone, couleur)}
        </g>
      </g>
      {#if e.membres.length > 1}
        <rect x={b.x1 - 16} y={b.y0 - 10} width="24" height="15" rx="7.5" class="badge" stroke={couleur} />
        <text x={b.x1 - 4} y={b.y0 + 1} class="texte-badge" fill={couleur}>×{e.membres.length}</text>
      {/if}
      {#if e.statut === "maintenance"}
        <circle cx={b.x0} cy={b.y0} r="6" fill={COULEUR_MAINTENANCE} class="pastille-statut" />
      {/if}
      <text x={e.position.x} y={ligneDuNom(b, 0)} class="nom">{e.nom}{e.statut === "hors-service" ? " (hors service)" : e.statut === "prevu" ? " (prévu)" : ""}</text>
      {#if adresse}
        <text x={e.position.x} y={ligneDuNom(b, 1)} class="adresse">{adresse}</text>
      {/if}
      {#if e.replie}
        <text x={b.x1 + 2} y={b.y0 + 8} class="replie">+</text>
      {/if}
      {#if !e.verrou && def.ports.length + e.interfaces.length > 0}
        <!-- svelte-ignore a11y_no_static_element_interactions -->
        <circle
          cx={b.x1 + 4}
          cy={(b.y0 + b.y1) / 2}
          r="7"
          class="poignee"
          onpointerdown={(ev) => appuiSurPoignee(ev, e)}
        >
          <title>Glisser vers un autre équipement pour le connecter</title>
        </circle>
      {/if}
    </g>
  {/each}

  {#each autresAnnotations as n (n.id)}
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <g class="annotation" class:choisi={choisis.has(n.id)} onpointerdown={(ev) => appuiSurObjet(ev, n.id)}>
      {@html dessinerAnnotation(n.texte || n.forme ? n : { ...n, texte: "Annotation" })}
      {#if n.forme === "fleche"}
        {@const vers = n.vers ?? { x: n.position.x + 120, y: n.position.y }}
        <line x1={n.position.x} y1={n.position.y} x2={vers.x} y2={vers.y} class="prise-fleche" />
      {:else}
        {@const c = cadreAnnotation(n)}
        <rect x={c.x} y={c.y} width={c.largeur} height={c.hauteur} class="prise-note" />
      {/if}
    </g>
  {/each}

  {#each schema.annotations.filter((n) => choisis.has(n.id)) as n (n.id)}
    {#if n.forme === "fleche"}
      {@const vers = n.vers ?? { x: n.position.x + 120, y: n.position.y }}
      {#each [["position", n.position], ["vers", vers]] as const as [bout, p] (bout)}
        <!-- svelte-ignore a11y_no_static_element_interactions -->
        <circle cx={p.x} cy={p.y} r="6" class="passage" onpointerdown={(e) => appuiSurPointe(e, n.id, bout)}>
          <title>Faire glisser pour déplacer {bout === "vers" ? "la pointe" : "le départ"} de la flèche</title>
        </circle>
      {/each}
    {:else}
      {@const c = cadreAnnotation(n)}
      <rect x={c.x - 4} y={c.y - 4} width={c.largeur + 8} height={c.hauteur + 8} rx="6" class="cadre-choisi" />
      {#if estZone(n)}
        {@const cote = 11 / camera.zoom}
        {#each POIGNEES as p (p.poignee)}
          <!-- svelte-ignore a11y_no_static_element_interactions -->
          <rect
            x={c.x + p.u * c.largeur - cote / 2}
            y={c.y + p.v * c.hauteur - cote / 2}
            width={cote}
            height={cote}
            class="poignee-taille"
            style="cursor: {p.curseur}"
            onpointerdown={(e) => appuiSurTaille(e, n.id, p.poignee, true)}
          >
            <title>Faire glisser pour redimensionner la zone</title>
          </rect>
        {/each}
      {/if}
    {/if}
  {/each}

  {#if guides}
    {#if guides.x !== null}
      <line x1={guides.x} y1={camera.y} x2={guides.x} y2={camera.y + hauteur / camera.zoom} class="guide" />
    {/if}
    {#if guides.y !== null}
      <line x1={camera.x} y1={guides.y} x2={camera.x + largeur / camera.zoom} y2={guides.y} class="guide" />
    {/if}
  {/if}

  {#if rectangle}
    <rect x={rectangle.x} y={rectangle.y} width={rectangle.l} height={rectangle.h} class="rectangle" />
  {/if}
  {#if fil}
    <line x1={fil.de.x} y1={fil.de.y} x2={fil.vers.x} y2={fil.vers.y} class="fil" />
  {/if}
  {#if cableEnMain}
    <g class="cable-en-main" pointer-events="none">
      {#if cableEnMain.depart}
        <line
          x1={cableEnMain.depart.x}
          y1={cableEnMain.depart.y}
          x2={cableEnMain.bout.x}
          y2={cableEnMain.bout.y}
          stroke={cableEnMain.trait.couleur}
          stroke-width={cableEnMain.trait.epaisseur}
          stroke-dasharray={cableEnMain.trait.tirets}
          stroke-linecap="round"
        />
      {/if}
      <rect x={cableEnMain.bout.x - 5} y={cableEnMain.bout.y - 7} width="10" height="14" rx="2" class="prise-cable" stroke={cableEnMain.trait.couleur} />
    </g>
  {/if}
</svg>

{#if consigneCable}
  <div class="consigne-cable" role="status">
    <Cable size={16} aria-hidden="true" />
    <span>{consigneCable}</span>
    <button type="button" class="bouton petit" onclick={() => (editeur.cable = null)}>Reposer (Échap)</button>
  </div>
{/if}

{#if menuPorts && editeur.cable}
  {@const cible = parId.get(menuPorts.equipement)}
  {#if cible}
    <MenuPorts
      {schema}
      equipement={cible}
      x={menuPorts.x}
      y={menuPorts.y}
      sauf={editeur.cable.lien}
      titre={`Brancher sur ${cible.nom}`}
      onchoisir={brancher}
      onajouter={ajouterPortEtBrancher}
      onfermer={() => (menuPorts = null)}
    />
  {/if}
{/if}

{#if barre}
  <div
    class="barre-rapide"
    class:a-cote={barre.genre === "lien"}
    style="left: {barre.x}px; top: {barre.y}px"
    role="toolbar"
    aria-label="Actions rapides"
  >
    {#if barre.genre === "equipement"}
      {@const choisi = barre.equipement}
      {#key choisi.id}
        <input
          class="champ nom-rapide"
          aria-label="Nom de l'équipement"
          value={choisi.nom}
          onchange={(e) => renommer(choisi, e.currentTarget)}
          onkeydown={(e) => {
            // Entrée valide (le changement part au départ du champ), Échap rétablit ;
            // dans les deux cas le plan reprend la main (Ctrl+Z y annule).
            if (e.key === "Escape") {
              e.preventDefault();
              e.currentTarget.value = choisi.nom;
            }
            if (e.key === "Enter" || e.key === "Escape") svg?.focus();
          }}
        />
      {/key}
      <button type="button" class="bouton discret" title="Brancher un câble (puis choisir le port)" aria-label="Brancher un câble" onclick={() => cableDepuis(choisi.id)}>
        <Cable size={16} aria-hidden="true" />
      </button>
    {:else if barre.genre === "lien"}
      {@const lien = barre.lien}
      <select
        class="champ"
        aria-label="Type de câble"
        value={lien.support}
        onchange={(e) => {
          const id = lien.id;
          const support = e.currentTarget.value as Support;
          editeur.agir((s) => {
            const l = s.liens.find((x) => x.id === id);
            if (l) l.support = support;
          });
        }}
      >
        {#each SUPPORTS as x (x.valeur)}<option value={x.valeur}>{x.nom}</option>{/each}
      </select>
      {@const coude = lien.forme ? lien.forme === "coudee" : schema.cablesCoudes === true}
      <button
        type="button"
        class="bouton discret"
        aria-pressed={coude}
        aria-label="Câble coudé à angle droit"
        title={coude ? "Câble coudé : cliquer pour le tracer droit" : "Câble droit : cliquer pour le couder à angle droit"}
        onclick={() => {
          const id = lien.id;
          editeur.agir((s) => {
            const l = s.liens.find((x) => x.id === id);
            if (l) l.forme = coude ? "droite" : "coudee";
          });
        }}
      >
        <Spline size={16} aria-hidden="true" />
      </button>
    {:else if barre.genre === "annotation"}
      {@const note = barre.annotation}
      <span class="couleurs-rapides" role="group" aria-label="Couleur">
        {#each COULEURS_ANNOTATION as c (c.valeur)}
          <button
            type="button"
            class="pastille-couleur"
            class:choisie={note.couleur === c.valeur}
            style="background: {c.valeur}"
            aria-label={c.nom}
            aria-pressed={note.couleur === c.valeur}
            title={c.nom}
            onclick={() => modifierAnnotation(note.id, (n) => (n.couleur = c.valeur))}
          ></button>
        {/each}
      </span>
    {:else}
      <span class="nombre-rapide">{barre.nombre} éléments</span>
      {#if barre.equipements >= 2}
        <span class="ancre-aligner">
          <button
            type="button"
            class="bouton discret"
            aria-label="Aligner ou répartir"
            aria-haspopup="menu"
            aria-expanded={menuAligner}
            title="Aligner ou répartir les équipements choisis"
            onclick={() => (menuAligner = !menuAligner)}
          >
            <AlignStartVertical size={16} aria-hidden="true" />
          </button>
          {#if menuAligner}
            <div class="menu-aligner" role="menu" aria-label="Aligner">
              {#each ALIGNEMENTS as a (a.valeur)}
                <button type="button" role="menuitem" onclick={() => alignerSelection(a.valeur)}>{a.nom}</button>
              {/each}
              <button type="button" role="menuitem" disabled={barre.equipements < 3} onclick={() => alignerSelection("repartir-x")}>
                <AlignHorizontalDistributeCenter size={14} aria-hidden="true" /> Répartir en largeur
              </button>
              <button type="button" role="menuitem" disabled={barre.equipements < 3} onclick={() => alignerSelection("repartir-y")}>
                <AlignVerticalDistributeCenter size={14} aria-hidden="true" /> Répartir en hauteur
              </button>
            </div>
          {/if}
        </span>
      {/if}
    {/if}
    {#if barre.genre === "equipement" || barre.genre === "plusieurs"}
      <button type="button" class="bouton discret" title="Copier (Ctrl+C), puis Ctrl+V pour coller, ici ou dans un autre schéma" aria-label="Copier" onclick={oncopier}>
        <Copy size={16} aria-hidden="true" />
      </button>
      <button type="button" class="bouton discret" title="Dupliquer (Ctrl+D)" aria-label="Dupliquer" onclick={ondupliquer}>
        <CopyPlus size={16} aria-hidden="true" />
      </button>
    {/if}
    <button type="button" class="bouton discret danger" title="Supprimer (Suppr) ; Ctrl+Z annule" aria-label="Supprimer" onclick={onretirer}>
      <Trash2 size={16} aria-hidden="true" />
    </button>
  </div>
{/if}

{#if miniCarte && etendue}
  <!-- Aide au pointeur : le clavier a les flèches, + − et 0. -->
  <svg
    bind:this={carte}
    class="mini-carte"
    aria-hidden="true"
    viewBox="{etendue.x} {etendue.y} {etendue.largeur} {etendue.hauteur}"
    preserveAspectRatio="xMidYMid meet"
    onpointerdown={appuiSurCarte}
    onpointermove={(e) => {
      if (carteFigee) centrerSur(e);
    }}
    onpointerup={() => (carteFigee = null)}
    onpointercancel={() => (carteFigee = null)}
    onwheel={molette}
  >
    <title>Mini-carte : cliquez ou faites glisser pour vous déplacer</title>
    {#each schema.groupes as g (g.id)}
      <rect x={g.position.x} y={g.position.y} width={g.taille.largeur} height={g.taille.hauteur} class="carte-groupe" vector-effect="non-scaling-stroke" />
    {/each}
    {#each schema.equipements as e (e.id)}
      {#if !caches.has(e.id)}
        <rect x={e.position.x - COTE / 2} y={e.position.y - COTE / 2} width={COTE} height={COTE} rx="8" fill={couleurDe(e)} />
      {/if}
    {/each}
    <rect
      x={camera.x}
      y={camera.y}
      width={largeur / camera.zoom}
      height={hauteur / camera.zoom}
      class="carte-champ"
      vector-effect="non-scaling-stroke"
    />
  </svg>
{/if}

<style>
  .canevas {
    display: block;
    width: 100%;
    height: 100%;
    background: var(--fond);
    font-family: var(--police);
    touch-action: none;
    user-select: none;
    cursor: default;
  }

  .canevas:focus-visible {
    outline: 2px solid var(--focus);
    outline-offset: -2px;
  }

  .canevas.main,
  .canevas.main * {
    cursor: grab;
  }

  .canevas.deplacement,
  .canevas.deplacement * {
    cursor: grabbing;
  }

  .canevas.cable {
    cursor: crosshair;
  }

  .equipement.estompe {
    opacity: 0.25;
  }

  .canevas.cable .equipement {
    cursor: copy;
  }

  .canevas.cable .equipement:hover .pave {
    fill: var(--focus);
    fill-opacity: 0.15;
    stroke: var(--focus);
    stroke-width: 2.5;
  }

  .bout {
    cursor: grab;
    opacity: 0;
  }

  .lien:hover .bout,
  .lien.choisi .bout {
    opacity: 1;
  }

  .canevas.cable .bout {
    display: none;
  }

  .prise-bout {
    fill: transparent;
  }

  .pastille-bout {
    fill: var(--surface);
    stroke-width: 2.5;
  }

  .prise-cable {
    fill: var(--surface);
    stroke-width: 2;
  }

  .consigne-cable {
    position: absolute;
    top: 10px;
    left: 50%;
    z-index: 3;
    display: flex;
    align-items: center;
    gap: 8px;
    max-width: calc(100% - 24px);
    padding: 6px 8px 6px 12px;
    transform: translateX(-50%);
    border: 1px solid var(--focus);
    border-radius: var(--rayon);
    background: var(--surface);
    box-shadow: 0 4px 12px rgb(0 0 0 / 0.15);
    font-size: 13px;
  }

  .barre-rapide {
    position: absolute;
    z-index: 3;
    display: flex;
    align-items: center;
    gap: 2px;
    padding: 4px;
    transform: translate(-50%, -100%);
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--surface);
    box-shadow: 0 4px 12px rgb(0 0 0 / 0.15);
    white-space: nowrap;
  }

  /* Sous le milieu d'un câble : le câble et ses bouts restent visibles. */
  .barre-rapide.a-cote {
    transform: translateX(-50%);
  }

  .barre-rapide .champ {
    width: auto;
    height: 30px;
  }

  .nom-rapide {
    width: 140px;
  }

  .nombre-rapide {
    padding: 0 6px;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .barre-rapide .danger:hover {
    color: var(--retard);
  }

  .mini-carte {
    position: absolute;
    right: 10px;
    bottom: 10px;
    width: min(200px, 40%);
    height: min(130px, 30%);
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--surface);
    box-shadow: 0 2px 8px rgb(0 0 0 / 0.12);
    cursor: pointer;
    touch-action: none;
  }

  .carte-groupe {
    fill: none;
    stroke: var(--bordure);
    stroke-width: 1;
  }

  .carte-champ {
    fill: var(--focus);
    fill-opacity: 0.1;
    stroke: var(--focus);
    stroke-width: 1.5;
  }

  .point-grille {
    fill: var(--bordure);
  }

  .pointe {
    fill: var(--texte-secondaire);
  }

  /* Cadre de l'équipement : invisible (il reçoit le pointeur), visible choisi ou prévu. */
  .pave {
    fill: transparent;
    stroke: none;
  }

  .equipement:hover .pave {
    fill: var(--surface-2);
    fill-opacity: 0.5;
  }

  .pave.prevu {
    stroke: var(--texte-secondaire);
    stroke-width: 1.5;
    stroke-dasharray: 5 3;
  }

  .badge {
    fill: var(--surface);
    stroke-width: 1.5;
  }

  .texte-badge {
    font-size: 10px;
    font-weight: 700;
    text-anchor: middle;
    pointer-events: none;
  }

  .pastille-statut {
    stroke: var(--surface);
    stroke-width: 2;
  }

  .equipement {
    cursor: move;
  }

  .equipement.verrouille {
    cursor: default;
  }

  .equipement.choisi .pave {
    fill: var(--focus);
    fill-opacity: 0.12;
    stroke: var(--focus);
    stroke-width: 2;
    stroke-dasharray: none;
  }

  /* Liseré de la couleur du fond : un nom reste lisible sur un câble (D50). */
  .nom,
  .adresse {
    paint-order: stroke;
    stroke: var(--fond);
    stroke-width: 4px;
    stroke-linejoin: round;
  }

  .nom {
    fill: var(--texte);
    font-size: 13px;
    font-weight: 600;
    text-anchor: middle;
  }

  .etiquettes {
    pointer-events: none;
  }

  .lien.relie > path:first-child {
    filter: drop-shadow(0 0 2.5px var(--focus));
  }

  .adresse {
    fill: var(--texte-secondaire);
    font-family: var(--police-code);
    font-size: 11px;
    text-anchor: middle;
  }

  .replie {
    fill: var(--texte-secondaire);
    font-size: 14px;
    font-weight: 700;
    text-anchor: middle;
  }

  .poignee {
    fill: var(--surface);
    stroke: var(--principale);
    stroke-width: 2;
    cursor: crosshair;
    opacity: 0;
  }

  .equipement:hover .poignee,
  .equipement.choisi .poignee {
    opacity: 1;
  }

  .prise {
    fill: none;
    stroke: transparent;
    stroke-width: 14;
    cursor: pointer;
    pointer-events: stroke;
  }

  /* Un câble choisi se tire pour coulisser (D53). */
  .lien.choisi .prise {
    cursor: move;
  }

  .lien.choisi path:first-child,
  .relation.choisi path:first-child {
    filter: drop-shadow(0 0 3px var(--focus));
  }

  .lien.prevu path:first-child {
    opacity: 0.6;
  }

  .passage {
    fill: var(--surface);
    stroke: var(--focus);
    stroke-width: 2;
    cursor: move;
  }




  .nom-groupe {
    fill: var(--texte);
    font-size: 13px;
    font-weight: 600;
    cursor: move;
  }

  .groupe.choisi .cadre-groupe {
    stroke-width: 2.5;
    stroke-dasharray: none;
  }

  .poignee-taille {
    fill: var(--surface);
    stroke: var(--focus);
    stroke-width: 2;
    vector-effect: non-scaling-stroke;
  }

  .annotation {
    cursor: move;
  }

  /* Le dessin d'une annotation ne prend pas le pointeur : sa prise (invisible) le fait. */
  .annotation :global(:not(.prise-zone, .prise-fleche, .prise-note)) {
    pointer-events: none;
  }

  .prise-note {
    fill: transparent;
  }

  /* Une zone se saisit par son bord ; un clic à l'intérieur la choisit, puis l'intérieur la fait glisser (D51). */
  .prise-zone {
    fill: none;
    stroke: transparent;
    stroke-width: 14;
    pointer-events: stroke;
  }

  .prise-fleche {
    stroke: transparent;
    stroke-width: 14;
    pointer-events: stroke;
  }

  .cadre-choisi {
    fill: none;
    stroke: var(--focus);
    stroke-width: 1.5;
    stroke-dasharray: 4 3;
    pointer-events: none;
  }

  .guide {
    stroke: var(--focus);
    stroke-width: 1;
    stroke-dasharray: 5 4;
    vector-effect: non-scaling-stroke;
    pointer-events: none;
  }

  .couleurs-rapides {
    display: flex;
    gap: 3px;
    padding: 0 4px;
  }

  .pastille-couleur {
    width: 20px;
    height: 20px;
    padding: 0;
    border: 2px solid var(--surface);
    border-radius: 50%;
    box-shadow: 0 0 0 1px var(--bordure);
    cursor: pointer;
  }

  .pastille-couleur.choisie {
    box-shadow: 0 0 0 2px var(--focus);
  }

  .ancre-aligner {
    position: relative;
  }

  .menu-aligner {
    position: absolute;
    top: calc(100% + 6px);
    left: 50%;
    z-index: 5;
    display: flex;
    flex-direction: column;
    min-width: 210px;
    padding: 4px;
    transform: translateX(-50%);
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--surface);
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.15);
  }

  .menu-aligner button {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 6px 10px;
    border: none;
    border-radius: 4px;
    background: transparent;
    color: var(--texte);
    text-align: left;
    cursor: pointer;
  }

  .menu-aligner button:hover:not(:disabled) {
    background: var(--surface-2);
  }

  .menu-aligner button:disabled {
    color: var(--texte-secondaire);
    cursor: not-allowed;
  }

  .rectangle {
    fill: var(--focus);
    fill-opacity: 0.08;
    stroke: var(--focus);
    stroke-dasharray: 4 3;
  }

  .fil {
    stroke: var(--principale);
    stroke-width: 2;
    stroke-dasharray: 6 4;
  }
</style>
