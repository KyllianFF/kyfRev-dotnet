// Dessin d'un schéma réseau en SVG (D39) : aperçu dans les fiches, export
// SVG et PNG. Géométrie et styles sont partagés avec le canevas de l'éditeur.
// Tout texte venant du schéma est échappé : rien n'y est interprété.
//
// Les couleurs suivent le thème (variables CSS de kyfRev) avec des valeurs
// claires de repli pour un fichier exporté. Les différences ne reposent
// jamais sur la seule couleur : épaisseur, tirets et libellés distinguent
// supports et relations.

import { definition } from "./catalogue";
import { DEFINITIONS_ILLUSTRATIONS, emprise, illustration } from "./illustrations";
import type {
  Affichage,
  Annotation,
  Equipement,
  Groupe,
  Lien,
  Point,
  Relation,
  Schema,
  Support,
  Vue,
} from "./modele";
import { interfaceDe, RELATIONS_SYMETRIQUES } from "./modele";
import {
  agregatDe,
  configDuBout,
  enAgregat,
  estTrunk,
  texteAcces,
  texteAgregat,
  texteTrunk,
  texteVlan,
  vlanDAcces,
} from "./vlans";
import { liensVisibles, relationVisible, vue as vueDe } from "./vues";

export { icone } from "./pictogrammes";

/**
 * Familles d'équipements et couleur de leur tuile (pictogramme blanc dessus,
 * comme dans les schémas réseau professionnels). Palette vérifiée (écart entre
 * familles, daltonisme, contraste du blanc) ; postes et infrastructure en gris
 * neutre, que leur pictogramme distingue. Mêmes couleurs en thème clair et
 * sombre : un schéma se lit pareil partout, exporté compris.
 */
export type Famille = "reseau" | "securite" | "serveurs" | "stockage" | "exterieur" | "neutre";
export const FAMILLES: { valeur: Famille; nom: string; couleur: string }[] = [
  { valeur: "reseau", nom: "Réseau", couleur: "#1f62b0" },
  { valeur: "securite", nom: "Sécurité", couleur: "#c0392b" },
  { valeur: "serveurs", nom: "Serveurs et virtualisation", couleur: "#6a3fb0" },
  { valeur: "stockage", nom: "Stockage", couleur: "#a35a00" },
  { valeur: "exterieur", nom: "Réseaux extérieurs", couleur: "#167a5a" },
  { valeur: "neutre", nom: "Postes et infrastructure", couleur: "#4b5563" },
];
const SECURITE = new Set(["pare-feu", "passerelle-vpn", "cato-socket"]);

export function familleDe(type: string): Famille {
  if (SECURITE.has(type)) return "securite";
  const categorie = definition(type).categorie;
  if (categorie === "reseau") return "reseau";
  if (categorie === "serveurs") return "serveurs";
  if (categorie === "stockage") return "stockage";
  if (categorie === "contexte") return "exterieur";
  return "neutre";
}

export const couleurDuType = (type: string): string =>
  FAMILLES.find((f) => f.valeur === familleDe(type))?.couleur ?? "#4b5563";

export const couleurDe = (equipement: Pick<Equipement, "type">): string => couleurDuType(equipement.type);

/** Repère de statut : prévu (tirets), maintenance (pastille orange), hors service (grisé). */
export const COULEUR_MAINTENANCE = "#d97706";

/** Côté du pavé d'un équipement ; sa position est son centre. */
export const COTE = 56;
const POLICE = "Segoe UI, system-ui, sans-serif";
const TEXTE = "var(--texte, #221d2e)";
const SECONDAIRE = "var(--texte-secondaire, #5d566d)";
const SURFACE = "var(--surface, #ffffff)";
const BORDURE = "var(--bordure, #dedae8)";
const FOND = "var(--fond, #ffffff)";

/** Échappe un texte pour du SVG (contenu ou attribut). */
export const echapper = (texte: string): string =>
  texte.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;");

export type Trait = { couleur: string; epaisseur: number; tirets?: string; nom: string };

/** Trait d'un support : épaisseur et tirets distinguent, la couleur s'ajoute. */
export function traitDuSupport(support: Support): Trait {
  switch (support) {
    case "fibre-monomode":
      return { couleur: "var(--graphique-3, #c27c0e)", epaisseur: 3.5, nom: "Fibre monomode" };
    case "fibre-multimode":
      return {
        couleur: "var(--graphique-3, #c27c0e)",
        epaisseur: 3.5,
        tirets: "14 3",
        nom: "Fibre multimode",
      };
    case "dac":
    case "aoc":
      return { couleur: "var(--graphique-4, #5b6fc4)", epaisseur: 3, nom: support === "dac" ? "DAC" : "AOC" };
    case "fibre-channel":
      return {
        couleur: "var(--graphique-5, #c4553a)",
        epaisseur: 3.5,
        tirets: "2 2 10 2",
        nom: "Fibre Channel",
      };
    case "radio":
    case "cellulaire":
      return {
        couleur: SECONDAIRE,
        epaisseur: 2,
        tirets: "1 6",
        nom: support === "radio" ? "Radio" : "Cellulaire",
      };
    case "virtuel":
      return { couleur: "var(--graphique-2, #2a8c80)", epaisseur: 2, tirets: "6 4", nom: "Virtuel" };
    case "operateur":
      return {
        couleur: "var(--graphique-7, #4f7a2e)",
        epaisseur: 2.5,
        tirets: "12 6",
        nom: "Opérateur / WAN",
      };
    case "console":
    case "usb":
      return {
        couleur: SECONDAIRE,
        epaisseur: 1.5,
        tirets: "3 3",
        nom: support === "usb" ? "USB" : "Console",
      };
    default:
      return {
        couleur: SECONDAIRE,
        epaisseur: 2,
        nom: support === "personnalise" ? "Personnalisé" : "Cuivre",
      };
  }
}

export function traitDuLien(lien: Lien): Trait {
  const base = traitDuSupport(lien.support);
  const tirets = lien.trait === "tirets" ? "8 5" : lien.trait === "points" ? "1 5" : base.tirets;
  return { ...base, ...(lien.couleur ? { couleur: lien.couleur } : {}), ...(tirets ? { tirets } : {}) };
}

/** Relations : traits fins et fléchés, jamais confondus avec un câble. */
export function traitDeRelation(relation: Relation): Trait {
  switch (relation.genre) {
    case "tunnel":
      return { couleur: "var(--graphique-2, #2a8c80)", epaisseur: 2, tirets: "10 4 2 4", nom: "Tunnel VPN" };
    case "association-wifi":
      return { couleur: SECONDAIRE, epaisseur: 1.5, tirets: "1 4", nom: "Association Wi-Fi" };
    case "stockage":
      return {
        couleur: "var(--graphique-3, #c27c0e)",
        epaisseur: 1.5,
        tirets: "6 3",
        nom: "Accès au stockage",
      };
    case "hebergement":
      return { couleur: "var(--graphique-4, #5b6fc4)", epaisseur: 1.5, tirets: "2 3", nom: "Hébergement" };
    case "haute-disponibilite":
      return {
        couleur: "var(--graphique-5, #c4553a)",
        epaisseur: 2.5,
        tirets: "1 4",
        nom: "Haute disponibilité",
      };
    default:
      return {
        couleur: "var(--graphique-6, #8b5d8f)",
        epaisseur: 1.5,
        tirets: "4 4",
        nom: "Relation logique",
      };
  }
}

/** Point du bord du pavé de centre `de`, sur la droite vers `vers`. */
export function bordVers(de: Point, vers: Point, demi = COTE / 2 + 2): Point {
  const dx = vers.x - de.x;
  const dy = vers.y - de.y;
  if (dx === 0 && dy === 0) return de;
  const echelle = demi / Math.max(Math.abs(dx), Math.abs(dy));
  return { x: de.x + dx * Math.min(1, echelle), y: de.y + dy * Math.min(1, echelle) };
}

/** Rectangle du plan (bornes). */
export type Boite = { x0: number; y0: number; x1: number; y1: number };

/** Ce qu'occupe l'illustration d'un équipement dans le plan, à `marge` près (D50). */
export function boiteDe(e: Pick<Equipement, "type" | "position">, marge = 2): Boite {
  const [x0, y0, x1, y1] = emprise(definition(e.type).icone);
  const k = COTE / 64;
  const ox = e.position.x - COTE / 2;
  const oy = e.position.y - COTE / 2;
  return {
    x0: ox + x0 * k - marge,
    y0: oy + y0 * k - marge,
    x1: ox + x1 * k + marge,
    y1: oy + y1 * k + marge,
  };
}

/** Point où la demi-droite de `de` vers `vers` sort de la boîte (`de` dedans, sinon `de` lui-même). */
function bordDe(b: Boite, de: Point, vers: Point): Point {
  const dx = vers.x - de.x;
  const dy = vers.y - de.y;
  if ((dx === 0 && dy === 0) || de.x < b.x0 || de.x > b.x1 || de.y < b.y0 || de.y > b.y1) return de;
  let t = Number.POSITIVE_INFINITY;
  if (dx > 0) t = Math.min(t, (b.x1 - de.x) / dx);
  if (dx < 0) t = Math.min(t, (b.x0 - de.x) / dx);
  if (dy > 0) t = Math.min(t, (b.y1 - de.y) / dy);
  if (dy < 0) t = Math.min(t, (b.y0 - de.y) / dy);
  t = Math.min(1, t);
  return { x: de.x + dx * t, y: de.y + dy * t };
}

/** Bornes des deux bouts d'un tracé ; sans boîte, le carré de l'équipement. */
export type Bornes = [Boite | undefined, Boite | undefined];
const borne = (b: Boite | undefined, de: Point, vers: Point) =>
  b ? bordDe(b, de, vers) : bordVers(de, vers);

/**
 * Tracé d'un lien (ou d'une relation) entre deux pavés, par ses points de
 * passage. `ecart` décale un tracé droit de côté : les câbles parallèles
 * entre deux mêmes équipements restent distincts.
 */
export function traceEntre(
  de: Point,
  vers: Point,
  passages: Point[],
  ecart = 0,
  bornes: Bornes = [undefined, undefined],
): Point[] {
  if (ecart !== 0 && passages.length === 0) {
    const longueur = Math.hypot(vers.x - de.x, vers.y - de.y) || 1;
    const nx = (-(vers.y - de.y) / longueur) * ecart;
    const ny = ((vers.x - de.x) / longueur) * ecart;
    const de2 = { x: de.x + nx, y: de.y + ny };
    const vers2 = { x: vers.x + nx, y: vers.y + ny };
    return [borne(bornes[0], de2, vers2), borne(bornes[1], vers2, de2)];
  }
  if (ecart !== 0) {
    // Avec des points de passage : chaque segment se décale, comme des rails (les traits d'un faisceau).
    const rail = decaler([de, ...passages, vers], ecart);
    return [
      borne(bornes[0], rail[0] as Point, rail[1] as Point),
      ...rail.slice(1, -1),
      borne(bornes[1], rail.at(-1) as Point, rail.at(-2) as Point),
    ];
  }
  const premier = passages[0] ?? vers;
  const dernier = passages.at(-1) ?? de;
  return [borne(bornes[0], de, premier), ...passages, borne(bornes[1], vers, dernier)];
}

/**
 * Route d'un câble coudé sans point de passage (D44, D52), d'un centre à
 * l'autre : en Z (vertical, horizontal au milieu, vertical ; ou l'inverse)
 * ou en L (un seul coude). `depart` et `arrivee` décalent le premier et le
 * dernier segment (en pixels, sur le côté de l'équipement), `milieu` le
 * palier d'un Z : deux câbles ne partagent ainsi aucun segment.
 */
export type FormeRoute = "z-vertical" | "z-horizontal" | "l-vertical" | "l-horizontal";
export type Route = {
  forme: FormeRoute;
  depart?: number;
  arrivee?: number;
  milieu?: number;
  /** Position du segment du milieu d'un Z, placée à la main (D53) : remplace le milieu calculé. */
  palier?: number;
};
const FORMES_ROUTE: FormeRoute[] = ["z-vertical", "z-horizontal", "l-vertical", "l-horizontal"];

/** Route habituelle : verticale entre deux rangées (schémas hiérarchiques), horizontale sur une même rangée. */
export const routeParDefaut = (p: Point, q: Point): Route => ({
  forme: Math.abs(q.y - p.y) < COTE ? "z-horizontal" : "z-vertical",
});

/** La même route, vue de l'autre bout (un L vertical d'un côté est horizontal de l'autre). */
export const routeInverse = (r: Route): Route => ({
  forme: r.forme === "l-vertical" ? "l-horizontal" : r.forme === "l-horizontal" ? "l-vertical" : r.forme,
  depart: r.arrivee ?? 0,
  arrivee: r.depart ?? 0,
  milieu: r.milieu ?? 0,
});

/** Le premier (puis le dernier) segment d'une forme est-il vertical ? */
const departVertical = (f: FormeRoute) => f === "z-vertical" || f === "l-vertical";
const arriveeVerticale = (f: FormeRoute) => f === "z-vertical" || f === "l-horizontal";

function polyligne(p: Point, q: Point, route: Route): Point[] {
  const d = route.depart ?? 0;
  const a = route.arrivee ?? 0;
  const mx = route.palier ?? Math.round((p.x + q.x) / 2) + (route.milieu ?? 0);
  const my = route.palier ?? Math.round((p.y + q.y) / 2) + (route.milieu ?? 0);
  if (route.forme === "z-vertical")
    return [
      { x: p.x + d, y: p.y },
      { x: p.x + d, y: my },
      { x: q.x + a, y: my },
      { x: q.x + a, y: q.y },
    ];
  if (route.forme === "z-horizontal")
    return [
      { x: p.x, y: p.y + d },
      { x: mx, y: p.y + d },
      { x: mx, y: q.y + a },
      { x: q.x, y: q.y + a },
    ];
  if (route.forme === "l-vertical")
    return [
      { x: p.x + d, y: p.y },
      { x: p.x + d, y: q.y + a },
      { x: q.x, y: q.y + a },
    ];
  return [
    { x: p.x, y: p.y + d },
    { x: q.x + a, y: p.y + d },
    { x: q.x + a, y: q.y },
  ];
}

/** Sans doublons ni points alignés au milieu d'un segment (un demi-tour, lui, reste). */
function nettoyer(brut: Point[]): Point[] {
  const net: Point[] = [];
  for (const p of brut) {
    const a = net.at(-2);
    const b = net.at(-1);
    if (b && b.x === p.x && b.y === p.y) continue;
    const dansLaLancee =
      a &&
      b &&
      ((a.x === b.x && b.x === p.x && (b.y - a.y) * (p.y - b.y) > 0) ||
        (a.y === b.y && b.y === p.y && (b.x - a.x) * (p.x - b.x) > 0));
    if (dansLaLancee) net.pop();
    net.push(p);
  }
  return net;
}

/**
 * Polyligne décalée de `ecart` sur la normale de chaque segment (comme des
 * rails) : des câbles parallèles restent côte à côte et ne se croisent pas
 * aux coudes (D52). Même normale que le décalage d'un câble droit.
 */
function decaler(points: Point[], ecart: number): Point[] {
  if (ecart === 0 || points.length < 2) return points;
  const normales = points.slice(1).map((q, i) => {
    const p = points[i] as Point;
    const l = Math.hypot(q.x - p.x, q.y - p.y) || 1;
    return { x: -(q.y - p.y) / l, y: (q.x - p.x) / l };
  });
  return points.map((p, i) => {
    const avant = normales[i - 1];
    const apres = normales[i];
    if (!avant || !apres) {
      const n = (avant ?? apres) as Point;
      return { x: p.x + n.x * ecart, y: p.y + n.y * ecart };
    }
    // Coin : là où se rejoignent les deux segments décalés.
    // (Un demi-tour n'a pas de coin : le point se décale comme un bout.)
    const produit = avant.x * apres.x + avant.y * apres.y;
    if (produit < -0.999) return { x: p.x + avant.x * ecart, y: p.y + avant.y * ecart };
    const k = ecart / (1 + produit);
    return { x: p.x + (avant.x + apres.x) * k, y: p.y + (avant.y + apres.y) * k };
  });
}

/**
 * Tracé coudé à angle droit (D44) : d'un équipement à l'autre selon `route`
 * (par défaut en Z, vertical d'abord entre deux rangées, horizontal sur une
 * même rangée) ; avec des points de passage, en L entre chacun. `ecart`
 * décale le tracé côte à côte, pour les câbles parallèles (D52).
 */
export function traceCoudee(
  de: Point,
  vers: Point,
  passages: Point[],
  ecart = 0,
  bornes: Bornes = [undefined, undefined],
  route?: Route,
): Point[] {
  let brut: Point[];
  if (passages.length === 0) brut = polyligne(de, vers, route ?? routeParDefaut(de, vers));
  else {
    // Entre deux points non alignés, un coude (D55) : au point de passage, le
    // câble tourne (s'il revenait sur ses pas, le point ne serait plus sur le
    // tracé) ; au départ d'un équipement, le petit côté d'abord, pour que le
    // décalage reste contre l'équipement.
    const centres = [de, ...passages, vers];
    brut = [de];
    let precedent: "h" | "v" | null = null;
    for (let i = 1; i < centres.length; i++) {
      const p = centres[i - 1] as Point;
      const q = centres[i] as Point;
      if (p.x !== q.x && p.y !== q.y) {
        const horizontalDAbord: boolean = precedent
          ? precedent === "v"
          : Math.abs(q.x - p.x) < Math.abs(q.y - p.y);
        brut.push(horizontalDAbord ? { x: q.x, y: p.y } : { x: p.x, y: q.y });
        precedent = horizontalDAbord ? "v" : "h";
      } else if (p.x !== q.x || p.y !== q.y) precedent = p.x === q.x ? "v" : "h";
      brut.push(q);
    }
  }
  const net = nettoyer(brut);
  if (net.length < 2) return traceEntre(de, vers, passages, ecart, bornes);
  const decale = decaler(net, ecart);
  const premier = decale[1] as Point;
  const dernier = decale.at(-2) as Point;
  const trace = [
    borne(bornes[0], decale[0] as Point, premier),
    ...decale.slice(1, -1),
    borne(bornes[1], decale.at(-1) as Point, dernier),
  ];
  // Un coin caché sous l'équipement : le tracé part de lui (pas de segment nul).
  return trace.filter((p, i) => i === 0 || p.x !== trace[i - 1]?.x || p.y !== trace[i - 1]?.y);
}

/** Le tracé d'un câble : coudé ou droit (le sien, sinon celui du schéma). */
export function traceDuLien(
  schema: Pick<Schema, "cablesCoudes">,
  lien: Lien,
  de: Pick<Equipement, "type" | "position">,
  vers: Pick<Equipement, "type" | "position">,
  ecart = 0,
  route?: Route,
): Point[] {
  const bornes: Bornes = [boiteDe(de), boiteDe(vers)];
  return estCoude(schema, lien)
    ? traceCoudee(de.position, vers.position, lien.points, ecart, bornes, routeDuPalier(lien) ?? route)
    : traceEntre(de.position, vers.position, lien.points, ecart, bornes);
}

/** Route d'un câble dont le palier a été coulissé à la main (D53). */
export const routeDuPalier = (lien: Lien): Route | undefined =>
  lien.palier && lien.points.length === 0
    ? { forme: lien.palier.axe === "y" ? "z-vertical" : "z-horizontal", palier: lien.palier.position }
    : undefined;

export const estCoude = (schema: Pick<Schema, "cablesCoudes">, lien: Lien) =>
  lien.forme ? lien.forme === "coudee" : schema.cablesCoudes === true;

type Segment = [Point, Point];

/** Longueur sur laquelle un segment horizontal ou vertical longe un autre, à moins de `tolerance`. */
function longe(s: Segment, o: Segment, tolerance: number): number {
  const [a, b] = s;
  const [c, d] = o;
  if (a.y === b.y && c.y === d.y && Math.abs(a.y - c.y) < tolerance)
    return Math.max(
      0,
      Math.min(Math.max(a.x, b.x), Math.max(c.x, d.x)) - Math.max(Math.min(a.x, b.x), Math.min(c.x, d.x)),
    );
  if (a.x === b.x && c.x === d.x && Math.abs(a.x - c.x) < tolerance)
    return Math.max(
      0,
      Math.min(Math.max(a.y, b.y), Math.max(c.y, d.y)) - Math.max(Math.min(a.y, b.y), Math.min(c.y, d.y)),
    );
  return 0;
}

/** Deux segments se croisent-ils (hors de leurs bouts) ? */
function croisent(s: Segment, o: Segment): boolean {
  const cote = (p: Point, q: Point, r: Point) =>
    Math.sign((q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x));
  const [a, b] = s;
  const [c, d] = o;
  return cote(a, b, c) * cote(a, b, d) < 0 && cote(c, d, a) * cote(c, d, b) < 0;
}

/** Un segment horizontal ou vertical traverse-t-il une boîte ? */
function traverse([a, b]: Segment, boite: Boite): boolean {
  return (
    Math.min(a.x, b.x) < boite.x1 &&
    Math.max(a.x, b.x) > boite.x0 &&
    Math.min(a.y, b.y) < boite.y1 &&
    Math.max(a.y, b.y) > boite.y0
  );
}

const segmentsDe = (trace: Point[]): Segment[] =>
  trace.slice(1).map((q, i) => [trace[i] as Point, q] as Segment);

/**
 * Routes des câbles coudés sans point de passage (D52) : pour chaque paire
 * d'équipements, la route en Z ou en L qui ne longe pas un autre câble (deux
 * câbles fondus en un ne se lisent plus), ne traverse pas un équipement et
 * croise le moins de câbles ; à égalité, la route habituelle en Z. Les câbles
 * parallèles d'une paire partagent la route, chacun sur sa voie.
 */
export function choisirRoutes(
  schema: Pick<Schema, "cablesCoudes">,
  liens: Lien[],
  presents: Equipement[],
  ecarts: Map<string, number>,
  faisceaux?: Map<string, Faisceau>,
): Map<string, Route> {
  const parId = new Map(presents.map((e) => [e.id, e]));
  const routes = new Map<string, Route>();
  // Câbles déjà tracés et équipements, rangés par cases : on ne compare qu'aux voisins.
  const occupes = new Repartition<Segment>();
  const boites = new Repartition<{ id: string; boite: Boite }>();
  for (const e of presents) {
    const boite = boiteDe(e, 6);
    boites.ajouter(boite, { id: e.id, boite });
  }
  const occuper = (trace: Point[]) => {
    for (const s of segmentsDe(trace)) occupes.ajouter(boiteSegment(s, 40), s);
  };
  const groupes = new Map<string, Lien[]>();
  // Les tracés imposés d'abord (droits, à points de passage, alignés) : les autres les évitent.
  for (const lien of liens) {
    const a = parId.get(lien.a.equipement);
    const b = parId.get(lien.b.equipement);
    if (!a || !b) continue;
    const libre =
      estCoude(schema, lien) &&
      lien.points.length === 0 &&
      !lien.palier &&
      a.position.x !== b.position.x &&
      a.position.y !== b.position.y;
    if (!libre) {
      occuper(traceDuLien(schema, lien, a, b, ecarts.get(lien.id) ?? 0));
      continue;
    }
    const cle = [lien.a.equipement, lien.b.equipement].sort().join("|");
    groupes.set(cle, [...(groupes.get(cle) ?? []), lien]);
  }
  for (const [cle, groupe] of groupes) {
    const [idP = "", idQ = ""] = cle.split("|");
    const eqP = parId.get(idP);
    const eqQ = parId.get(idQ);
    if (!eqP || !eqQ) continue;
    const [p, q] = [eqP.position, eqQ.position];
    // Demi-largeur de la paire : du câble le plus écarté, avec les traits de son faisceau.
    const voies = Math.max(
      0,
      ...groupe.map(
        (l) =>
          Math.abs(ecarts.get(l.id) ?? 0) + (((faisceaux?.get(l.id)?.brins ?? 1) - 1) * PAS_FAISCEAU) / 2,
      ),
    );
    const tolerance = voies + 9;
    const habituelle = routeParDefaut(p, q).forme;
    // Toutes les routes possibles, avec leur coût propre : un coude coûte ; un L,
    // moins habituel qu'un Z, un peu plus ; un décalage (sortie, arrivée ou
    // palier, tant que le câble reste sur le côté de l'équipement), un peu.
    const [bp, bq] = [boiteDe(eqP, 0), boiteDe(eqQ, 0)];
    const tient = (b: Boite, vertical: boolean, decalage: number) =>
      decalage === 0 || Math.abs(decalage) + voies <= (vertical ? b.x1 - b.x0 : b.y1 - b.y0) / 2 - 2;
    const candidates: { route: Route; segments: Segment[]; propre: number }[] = [];
    for (const forme of [habituelle, ...FORMES_ROUTE.filter((f) => f !== habituelle)])
      for (const depart of [0, 12, -12])
        for (const arrivee of [0, 12, -12])
          for (const milieu of forme.startsWith("z") ? [0, 16, -16] : [0]) {
            if (!tient(bp, departVertical(forme), depart) || !tient(bq, arriveeVerticale(forme), arrivee))
              continue;
            const route: Route = { forme, depart, arrivee, milieu };
            const segments = segmentsDe(nettoyer(polyligne(p, q, route)));
            const propre =
              (segments.length - 1) * 40 +
              (forme.startsWith("l") ? 60 : 0) +
              (forme === habituelle ? 0 : 30) +
              Math.abs(depart) +
              Math.abs(arrivee) +
              Math.abs(milieu);
            candidates.push({ route, segments, propre });
          }
    candidates.sort((a, b) => a.propre - b.propre);
    // La moins coûteuse, gênes comprises (longer un câble, traverser un équipement,
    // croiser un câble) ; une gêne ne fait qu'ajouter : on s'arrête dès qu'aucune
    // candidate restante ne peut faire mieux.
    let meilleure: Route = { forme: habituelle };
    let moindre = Number.POSITIVE_INFINITY;
    for (const c of candidates) {
      if (c.propre >= moindre) break;
      let cout = c.propre;
      for (const s of c.segments) {
        const zone = boiteSegment(s, tolerance);
        boites.parcourir(zone, ({ id, boite }) => {
          if (id !== idP && id !== idQ && traverse(s, boite)) cout += 500;
        });
        if (cout >= moindre) break;
        occupes.parcourir(zone, (o) => {
          cout += longe(s, o, tolerance) * 4 + (croisent(s, o) ? 25 : 0);
        });
        if (cout >= moindre) break;
      }
      if (cout < moindre) {
        moindre = cout;
        meilleure = c.route;
      }
    }
    for (const lien of groupe) {
      const route = lien.a.equipement === idP ? meilleure : routeInverse(meilleure);
      routes.set(lien.id, route);
      const a = parId.get(lien.a.equipement);
      const b = parId.get(lien.b.equipement);
      if (a && b) occuper(traceDuLien(schema, lien, a, b, ecarts.get(lien.id) ?? 0, route));
    }
  }
  return routes;
}

/** Boîte d'un segment, élargie de `marge`. */
const boiteSegment = ([a, b]: Segment, marge: number): Boite => ({
  x0: Math.min(a.x, b.x) - marge,
  y0: Math.min(a.y, b.y) - marge,
  x1: Math.max(a.x, b.x) + marge,
  y1: Math.max(a.y, b.y) + marge,
});

/** Objets rangés par cases de 120 px, parcourus d'après une zone. */
class Repartition<T> {
  private cases = new Map<number, { objet: T; vu: number }[]>();
  private passage = 0;

  /** Appelle `action` pour chaque case de la boîte (clé numérique : x, y décalés). */
  private static cases(b: Boite, action: (cle: number) => void): void {
    const c = 120;
    const [x0, x1] = [Math.floor(b.x0 / c), Math.floor(b.x1 / c)];
    const [y0, y1] = [Math.floor(b.y0 / c), Math.floor(b.y1 / c)];
    for (let x = x0; x <= x1; x++)
      for (let y = y0; y <= y1; y++) action((x + 50_000) * 100_000 + (y + 50_000));
  }

  ajouter(b: Boite, objet: T): void {
    const entree = { objet, vu: 0 };
    Repartition.cases(b, (cle) => {
      const liste = this.cases.get(cle);
      if (liste) liste.push(entree);
      else this.cases.set(cle, [entree]);
    });
  }

  /** Chaque objet dont une case touche la zone, une seule fois. */
  parcourir(zone: Boite, action: (objet: T) => void): void {
    const passage = ++this.passage;
    Repartition.cases(zone, (cle) => {
      for (const entree of this.cases.get(cle) ?? []) {
        if (entree.vu === passage) continue;
        entree.vu = passage;
        action(entree.objet);
      }
    });
  }
}

/** Écart entre deux câbles ordinaires, et entre deux traits d'un même faisceau (les ports d'un agrégat). */
const PAS_CABLES = 16;
export const PAS_FAISCEAU = 8;
/** Traits dessinés au plus pour un seul câble (un agrégat de nombreux ports reste lisible). */
const BRINS_MAX = 8;

/**
 * Faisceau d'un câble d'agrégat de liens : les câbles de même `cle` entre deux mêmes équipements
 * partagent un cercle ; `brins` est le nombre de traits du câble (un par port de son agrégat).
 */
export type Faisceau = { cle: string; brins: number };

/**
 * Les faisceaux des agrégats de liens. Un agrégat n'a qu'un câble au modèle, qui vaut pour tous ses
 * ports : il se dessine en autant de traits, sous un cercle. Un trunk n'est pas un agrégat : sans
 * agrégat, des câbles en trunk restent des câbles ordinaires, annotés. Des câbles au tracé différent
 * (droit ou coudé, palier, points de passage) ne partagent pas de cercle.
 */
export function faisceauxDe(schema: Schema, liens: Lien[]): Map<string, Faisceau> {
  const agregatDuLien = (lien: Lien) => {
    const [a, b] = [agregatDe(schema, lien.a), agregatDe(schema, lien.b)];
    if (!a && !b) return null;
    return {
      cle: `a:${a?.id ?? ""}:${b?.id ?? ""}`,
      ports: Math.max(a?.membres.length ?? 0, b?.membres.length ?? 0),
    };
  };
  const cables = new Map<string, number>();
  for (const lien of liens) {
    const agregat = agregatDuLien(lien);
    if (agregat) cables.set(agregat.cle, (cables.get(agregat.cle) ?? 0) + 1);
  }
  const faisceaux = new Map<string, Faisceau>();
  for (const lien of liens) {
    const agregat = agregatDuLien(lien);
    if (!agregat) continue;
    const trace = [
      estCoude(schema, lien) ? "c" : "d",
      lien.palier ? `${lien.palier.axe}${lien.palier.position}` : "",
      lien.points.length > 0 ? lien.id : "",
    ].join("|");
    faisceaux.set(lien.id, {
      cle: `${agregat.cle}|${trace}`,
      brins: cables.get(agregat.cle) === 1 ? Math.max(1, Math.min(BRINS_MAX, agregat.ports)) : 1,
    });
  }
  return faisceaux;
}

/**
 * Écart de chaque câble droit parmi ceux qui relient deux mêmes équipements : 16 px entre deux câbles,
 * 8 px entre deux traits d'un même faisceau (`faisceaux` : les agrégats, dont les traits se serrent
 * sous leur cercle ; chaque faisceau est regroupé à la place de son premier câble).
 */
export function ecartsParalleles(liens: Lien[], faisceaux?: Map<string, Faisceau>): Map<string, number> {
  const paires = new Map<string, Lien[]>();
  for (const lien of liens) {
    if (lien.points.length > 0) continue;
    const [x, y] = [lien.a.equipement, lien.b.equipement].sort();
    const cle = `${x}|${y}`;
    paires.set(cle, [...(paires.get(cle) ?? []), lien]);
  }
  const ecarts = new Map<string, number>();
  for (const groupe of paires.values()) {
    if (groupe.length < 2) continue;
    const cleDe = (lien: Lien) => faisceaux?.get(lien.id)?.cle;
    const ordonnes = groupe.flatMap((lien, i) => {
      const cle = cleDe(lien);
      if (cle === undefined) return [lien];
      return groupe.findIndex((l) => cleDe(l) === cle) === i ? groupe.filter((l) => cleDe(l) === cle) : [];
    });
    // Le centre de chaque câble, de proche en proche ; le tout est ensuite centré sur l'axe de la paire.
    const centres: number[] = [];
    let curseur = 0;
    ordonnes.forEach((lien, i) => {
      const avant = ordonnes[i - 1];
      if (avant)
        curseur += cleDe(lien) !== undefined && cleDe(lien) === cleDe(avant) ? PAS_FAISCEAU : PAS_CABLES;
      const largeur = ((faisceaux?.get(lien.id)?.brins ?? 1) - 1) * PAS_FAISCEAU;
      centres.push(curseur + largeur / 2);
      curseur += largeur;
    });
    ordonnes.forEach((lien, i) => {
      // Même sens pour tous (sinon la normale s'inverse avec le sens du lien).
      const sens = lien.a.equipement <= lien.b.equipement ? 1 : -1;
      ecarts.set(lien.id, ((centres[i] as number) - curseur / 2) * sens);
    });
  }
  return ecarts;
}

export const cheminDe = (points: Point[]): string =>
  points.map((p, i) => `${i === 0 ? "M" : "L"}${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(" ");

/** Position d'un libellé de port, près du bout `de` d'un tracé. */
export function prochePorte(trace: Point[], depuisDebut: boolean): Point {
  const a = depuisDebut ? trace[0] : trace.at(-1);
  const b = depuisDebut ? trace[1] : trace.at(-2);
  if (!a || !b) return a ?? { x: 0, y: 0 };
  const longueur = Math.hypot(b.x - a.x, b.y - a.y) || 1;
  // Vers le bas, le nom et l'adresse de l'équipement occupent la place : la pastille s'écarte.
  const versLeBas = b.y - a.y > Math.abs(b.x - a.x);
  const t = Math.min(0.45, (versLeBas ? 44 : 20) / longueur);
  return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t };
}

/** Milieu d'un tracé (pour le nom d'un lien). */
export function milieu(trace: Point[]): Point {
  if (trace.length === 0) return { x: 0, y: 0 };
  const longueurs = trace
    .slice(1)
    .map((p, i) => Math.hypot(p.x - (trace[i] as Point).x, p.y - (trace[i] as Point).y));
  let reste = longueurs.reduce((a, b) => a + b, 0) / 2;
  for (let i = 0; i < longueurs.length; i++) {
    const l = longueurs[i] as number;
    if (reste <= l) {
      const a = trace[i] as Point;
      const b = trace[i + 1] as Point;
      const t = l === 0 ? 0 : reste / l;
      return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t };
    }
    reste -= l;
  }
  return trace.at(-1) as Point;
}

/** Équipements masqués : ceux hébergés par un hôte replié. */
export function masques(schema: Schema): Set<string> {
  const replies = new Set(schema.equipements.filter((e) => e.replie).map((e) => e.id));
  const caches = new Set<string>();
  const groupesReplies = schema.groupes.filter((g) => g.replie);
  for (const g of groupesReplies) for (const m of g.membres) caches.add(m);
  let change = true;
  while (change) {
    change = false;
    for (const r of schema.relations) {
      if (r.genre !== "hebergement") continue;
      if ((replies.has(r.de) || caches.has(r.de)) && !caches.has(r.vers)) {
        caches.add(r.vers);
        change = true;
      }
    }
  }
  return caches;
}

/** Flèche des relations, ombre et reflet des tuiles (partagés par l'aperçu et le canevas). */
export const DEFINITIONS = [
  '<marker id="fleche" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 10 5 0 10z" fill="#6b6578"/></marker>',
  '<filter id="ombre" x="-20%" y="-20%" width="140%" height="150%"><feDropShadow dx="0" dy="1.5" stdDeviation="1.6" flood-color="#000000" flood-opacity="0.28"/></filter>',
  DEFINITIONS_ILLUSTRATIONS,
].join("");

export type Cadre = { x: number; y: number; largeur: number; hauteur: number };

/** Cadre de tout ce qui est dessiné (pavés, noms, groupes, annotations, points de passage). */
export function cadreDuSchema(schema: Schema, marge = 40): Cadre {
  const xs: number[] = [];
  const ys: number[] = [];
  for (const e of schema.equipements) {
    xs.push(e.position.x - COTE, e.position.x + COTE);
    ys.push(e.position.y - COTE / 2, e.position.y + COTE / 2 + 34);
  }
  for (const g of schema.groupes) {
    xs.push(g.position.x, g.position.x + g.taille.largeur);
    ys.push(g.position.y - 6, g.position.y + g.taille.hauteur);
  }
  for (const n of schema.annotations) {
    const c = cadreAnnotation(n);
    xs.push(c.x, c.x + c.largeur);
    ys.push(c.y, c.y + c.hauteur);
  }
  for (const l of [...schema.liens, ...schema.relations]) {
    for (const p of l.points) {
      xs.push(p.x);
      ys.push(p.y);
    }
  }
  if (xs.length === 0) return { x: 0, y: 0, largeur: 400, hauteur: 200 };
  const x = Math.min(...xs) - marge;
  const y = Math.min(...ys) - marge;
  return { x, y, largeur: Math.max(...xs) + marge - x, hauteur: Math.max(...ys) + marge - y };
}

/** Lignes d'une annotation, coupées à sa largeur. */
export function lignes(texte: string, largeur: number): string[] {
  const parLigne = Math.max(8, Math.floor(largeur / 7));
  const resultat: string[] = [];
  for (const paragraphe of texte.split("\n")) {
    let ligne = "";
    for (const mot of paragraphe.split(/\s+/).filter(Boolean)) {
      if (ligne && `${ligne} ${mot}`.length > parLigne) {
        resultat.push(ligne);
        ligne = mot;
      } else ligne = ligne ? `${ligne} ${mot}` : mot;
    }
    resultat.push(ligne);
  }
  return resultat;
}

/** Adresse affichée sous un équipement : sa gestion, sinon sa première adresse. */
export function adresseAffichee(equipement: Equipement): string {
  if (equipement.gestion?.adresse) return equipement.gestion.adresse;
  const premiere = equipement.interfaces.flatMap((i) => i.adresses)[0];
  return premiere ? `${premiere.adresse}${premiere.prefixe !== undefined ? `/${premiere.prefixe}` : ""}` : "";
}

const texteSvg = (x: number, y: number, contenu: string, attributs = ""): string =>
  `<text x="${x.toFixed(1)}" y="${y.toFixed(1)}" ${attributs}>${echapper(contenu)}</text>`;

function dessinerGroupe(g: Groupe): string {
  const couleur = g.couleur ?? "var(--graphique-1, #7f4ea8)";
  return [
    `<rect x="${g.position.x}" y="${g.position.y}" width="${g.taille.largeur}" height="${g.taille.hauteur}" rx="10" fill="${couleur}" fill-opacity="0.06" stroke="${couleur}" stroke-dasharray="6 4"/>`,
    texteSvg(g.position.x + 10, g.position.y + 18, g.nom, `fill="${TEXTE}" font-size="13" font-weight="600"`),
  ].join("");
}

export const estZone = (n: Annotation): boolean => n.forme === "rectangle" || n.forme === "ellipse";

/** Taille du texte libre. */
const tailleTexte = (n: Annotation) => n.taille ?? 16;

/** Cadre d'une annotation (sélection, cadrage). */
export function cadreAnnotation(n: Annotation): Cadre {
  const forme = n.forme ?? "note";
  if (forme === "fleche") {
    const vers = n.vers ?? { x: n.position.x + 120, y: n.position.y };
    const x = Math.min(n.position.x, vers.x);
    const y = Math.min(n.position.y, vers.y);
    return {
      x: x - 8,
      y: y - 8,
      largeur: Math.abs(vers.x - n.position.x) + 16,
      hauteur: Math.abs(vers.y - n.position.y) + 16,
    };
  }
  if (forme === "rectangle" || forme === "ellipse") {
    return { x: n.position.x, y: n.position.y, largeur: n.largeur ?? 240, hauteur: n.hauteur ?? 160 };
  }
  if (forme === "texte") {
    const t = tailleTexte(n);
    const contenu = (n.texte || "Texte").split("\n");
    const plusLongue = Math.max(...contenu.map((l) => l.length));
    return {
      x: n.position.x,
      y: n.position.y,
      largeur: Math.max(40, plusLongue * t * 0.58),
      hauteur: contenu.length * t * 1.3 + 4,
    };
  }
  const largeur = n.largeur ?? 200;
  return { x: n.position.x, y: n.position.y, largeur, hauteur: 16 + lignes(n.texte, largeur).length * 17 };
}

/** Dessin SVG d'une annotation ; une zone se dessine sous les câbles, le reste au-dessus. */
export function dessinerAnnotation(n: Annotation): string {
  const forme = n.forme ?? "note";
  const couleur = n.couleur ?? (forme === "note" ? "" : "#4b5563");
  if (forme === "rectangle" || forme === "ellipse") {
    const l = n.largeur ?? 240;
    const h = n.hauteur ?? 160;
    const figure =
      forme === "rectangle"
        ? `<rect x="${n.position.x}" y="${n.position.y}" width="${l}" height="${h}" rx="8" fill="${couleur}" fill-opacity="0.1" stroke="${couleur}" stroke-width="1.5"/>`
        : `<ellipse cx="${n.position.x + l / 2}" cy="${n.position.y + h / 2}" rx="${l / 2}" ry="${h / 2}" fill="${couleur}" fill-opacity="0.1" stroke="${couleur}" stroke-width="1.5"/>`;
    const legende = n.texte
      ? texteSvg(
          forme === "rectangle" ? n.position.x + 10 : n.position.x + l / 2,
          n.position.y + 18,
          n.texte.split("\n")[0] ?? "",
          `fill="${couleur}" font-size="13" font-weight="700"${forme === "ellipse" ? ' text-anchor="middle"' : ""}`,
        )
      : "";
    return figure + legende;
  }
  if (forme === "fleche") {
    const de = n.position;
    const vers = n.vers ?? { x: de.x + 120, y: de.y };
    const l = Math.hypot(vers.x - de.x, vers.y - de.y) || 1;
    const [ux, uy] = [(vers.x - de.x) / l, (vers.y - de.y) / l];
    const base = { x: vers.x - ux * 12, y: vers.y - uy * 12 };
    const pointe = [
      [vers.x, vers.y],
      [base.x - uy * 6, base.y + ux * 6],
      [base.x + uy * 6, base.y - ux * 6],
    ]
      .map(([x, y]) => `${(x ?? 0).toFixed(1)},${(y ?? 0).toFixed(1)}`)
      .join(" ");
    const m = { x: (de.x + vers.x) / 2, y: (de.y + vers.y) / 2 };
    return [
      `<line x1="${de.x}" y1="${de.y}" x2="${base.x.toFixed(1)}" y2="${base.y.toFixed(1)}" stroke="${couleur}" stroke-width="2.5" stroke-linecap="round"/>`,
      `<polygon points="${pointe}" fill="${couleur}"/>`,
      n.texte ? etiquette({ x: m.x, y: m.y - 4 }, n.texte.split("\n")[0] ?? "", 11) : "",
    ].join("");
  }
  if (forme === "texte") {
    const t = tailleTexte(n);
    return (n.texte || "Texte")
      .split("\n")
      .map((ligne, i) =>
        texteSvg(
          n.position.x,
          n.position.y + t + i * t * 1.3,
          ligne,
          `fill="${n.couleur ?? TEXTE}" font-size="${t}" font-weight="600"`,
        ),
      )
      .join("");
  }
  const largeur = n.largeur ?? 200;
  const contenu = lignes(n.texte, largeur);
  return [
    `<rect x="${n.position.x}" y="${n.position.y}" width="${largeur}" height="${16 + contenu.length * 17}" rx="6" fill="${n.couleur ? n.couleur : "var(--avertissement-fond, #fff4e0)"}"${n.couleur ? ' fill-opacity="0.14"' : ""} stroke="${n.couleur ?? BORDURE}"/>`,
    ...contenu.map((ligne, i) =>
      texteSvg(n.position.x + 8, n.position.y + 20 + i * 17, ligne, `fill="${TEXTE}" font-size="12"`),
    ),
  ].join("");
}

/** Badge « ×2 » d'un stack, en haut à droite de la tuile. */
export const nombreDeMembres = (e: Equipement): number => e.membres.length;

/** Liseré de la couleur du fond autour d'un texte : lisible même sur un câble. */
const LISERE = `stroke="${FOND}" stroke-width="4" stroke-linejoin="round" paint-order="stroke"`;

/** Position du nom (puis de l'adresse) sous l'illustration. */
export const ligneDuNom = (b: Boite, rang: 0 | 1): number => b.y1 + 13 + rang * 14;

function dessinerEquipement(e: Equipement, adresses = true): string {
  const def = definition(e.type);
  const couleur = couleurDe(e);
  const x = e.position.x - COTE / 2;
  const y = e.position.y - COTE / 2;
  const b = boiteDe(e, 0);
  const adresse = adresses ? adresseAffichee(e) : "";
  const membres = nombreDeMembres(e);
  const statut =
    e.statut !== "actif"
      ? ` (${e.statut === "prevu" ? "prévu" : e.statut === "maintenance" ? "maintenance" : "hors service"})`
      : "";
  return [
    `<g${e.statut === "hors-service" ? ' opacity="0.45"' : ""}>`,
    `<title>${echapper(`${e.nom} — ${def.nom}${statut}`)}</title>`,
    `<g filter="url(#ombre)"${e.statut === "prevu" ? ' opacity="0.55"' : ""}><g transform="translate(${x} ${y}) scale(${COTE / 64})">${illustration(def.icone, couleur)}</g></g>`,
    e.statut === "prevu"
      ? `<rect x="${(b.x0 - 5).toFixed(1)}" y="${(b.y0 - 5).toFixed(1)}" width="${(b.x1 - b.x0 + 10).toFixed(1)}" height="${(b.y1 - b.y0 + 10).toFixed(1)}" rx="8" fill="none" stroke="${SECONDAIRE}" stroke-width="1.5" stroke-dasharray="5 3"/>`
      : "",
    membres > 1
      ? `<rect x="${(b.x1 - 16).toFixed(1)}" y="${(b.y0 - 10).toFixed(1)}" width="24" height="15" rx="7.5" fill="${SURFACE}" stroke="${couleur}" stroke-width="1.5"/>${texteSvg(b.x1 - 4, b.y0 + 1, `×${membres}`, `text-anchor="middle" fill="${couleur}" font-size="10" font-weight="700"`)}`
      : "",
    e.statut === "maintenance"
      ? `<circle cx="${b.x0.toFixed(1)}" cy="${b.y0.toFixed(1)}" r="6" fill="${COULEUR_MAINTENANCE}" stroke="${SURFACE}" stroke-width="2"/>`
      : "",
    texteSvg(
      e.position.x,
      ligneDuNom(b, 0),
      e.nom + statut,
      `text-anchor="middle" fill="${TEXTE}" font-size="13" font-weight="600" ${LISERE}`,
    ),
    adresse
      ? texteSvg(
          e.position.x,
          ligneDuNom(b, 1),
          adresse,
          `text-anchor="middle" fill="${SECONDAIRE}" font-size="11" font-family="Cascadia Mono, Consolas, monospace" ${LISERE}`,
        )
      : "",
    `</g>`,
  ].join("");
}

function dessinerTrace(trace: Point[], trait: Trait, fleche = false): string {
  return `<path d="${cheminDe(trace)}" fill="none" stroke="${trait.couleur}" stroke-width="${trait.epaisseur}"${trait.tirets ? ` stroke-dasharray="${trait.tirets}"` : ""} stroke-linecap="round" stroke-linejoin="round"${fleche ? ' marker-end="url(#fleche)"' : ""}/>`;
}

function etiquette(p: Point, contenu: string, taille = 11): string {
  return dessinerEtiquette({ x: p.x, y: p.y, texte: contenu, taille, genre: "nom" });
}

/**
 * Une étiquette posée sur un câble (D50) : nom de port, nom du câble, VLAN.
 * `x`, `y` : son centre.
 */
export type Etiquette = {
  x: number;
  y: number;
  texte: string;
  taille: number;
  genre: "port" | "nom" | "vlan";
  couleur?: string;
  /** Le texte entier, quand l'étiquette l'abrège (une longue liste de VLAN) : il se lit au survol. */
  titre?: string;
};

const largeurDe = (texte: string, taille: number) => texte.length * taille * 0.58 + 10;

export function boiteEtiquette(e: Etiquette, marge = 1): Boite {
  const l = largeurDe(e.texte, e.taille) / 2 + marge;
  const h = (e.taille + 6) / 2 + marge;
  return { x0: e.x - l, y0: e.y - h, x1: e.x + l, y1: e.y + h };
}

export function dessinerEtiquette(e: Etiquette): string {
  const b = boiteEtiquette(e, 0);
  const contour = e.couleur ?? BORDURE;
  return [
    e.titre ? `<g><title>${echapper(e.titre)}</title>` : "",
    `<rect x="${b.x0.toFixed(1)}" y="${b.y0.toFixed(1)}" width="${(b.x1 - b.x0).toFixed(1)}" height="${(b.y1 - b.y0).toFixed(1)}" rx="${((b.y1 - b.y0) / 2).toFixed(1)}" fill="${SURFACE}" stroke="${contour}"${e.couleur ? ' stroke-width="1.5"' : ""}/>`,
    texteSvg(
      e.x,
      e.y + e.taille * 0.36,
      e.texte,
      `text-anchor="middle" fill="${e.genre === "vlan" && e.couleur ? e.couleur : SECONDAIRE}" font-size="${e.taille}"${e.genre === "port" ? "" : ' font-weight="600"'}`,
    ),
    e.titre ? "</g>" : "",
  ].join("");
}

const chevauche = (a: Boite, b: Boite) => a.x0 < b.x1 && b.x0 < a.x1 && a.y0 < b.y1 && b.y0 < a.y1;
/** Surface commune de deux boîtes. */
const recouvrement = (a: Boite, b: Boite) =>
  Math.max(0, Math.min(a.x1, b.x1) - Math.max(a.x0, b.x0)) *
  Math.max(0, Math.min(a.y1, b.y1) - Math.max(a.y0, b.y0));
const abreger = (texte: string, maximum = 28) =>
  texte.length > maximum ? `${texte.slice(0, maximum - 1)}…` : texte;

/** Largeur d'une étiquette de VLAN, en caractères : au-delà, la liste est coupée. */
const VLAN_MAX = 32;

/**
 * Une liste de VLAN trop longue pour son étiquette, coupée après un numéro entier :
 * « Trunk · U 1 · T 10,20,30-40,… ». Le texte entier reste dans le titre de l'étiquette.
 */
export function abregerVlans(texte: string, maximum = VLAN_MAX): string {
  if (texte.length <= maximum) return texte;
  const debut = texte.slice(0, maximum - 1);
  const virgule = debut.lastIndexOf(",");
  return virgule > 0 ? `${debut.slice(0, virgule + 1)}…` : `${debut}…`;
}

/** Ce qu'il faut étiqueter sur un tracé. */
export type ATaguer = {
  id: string;
  trace: Point[];
  ports?: [string | undefined, string | undefined];
  nom?: string;
  vlan?: { texte: string; couleur?: string };
};

type Candidat = { e: Etiquette; cout: number };

/**
 * Places possibles d'une étiquette le long d'un tracé, chacune avec son coût :
 * sur le câble, juste à côté (au-dessus d'un câble horizontal, à droite d'un
 * câble vertical d'abord), puis un cran plus loin. Les moins coûteuses d'abord.
 */
function autourDuTrace(
  parcours: ReturnType<typeof parcourir>,
  distances: { d: number; cout: number }[],
  base: Omit<Etiquette, "x" | "y">,
): Candidat[] {
  const demi = boiteEtiquette({ ...base, x: 0, y: 0 }, 0);
  const candidats: Candidat[] = [];
  for (const { d, cout } of distances) {
    const { p, n } = parcours.a(d);
    candidats.push({ e: { ...base, x: p.x, y: p.y }, cout });
    const etendue = Math.abs(n.x) * demi.x1 + Math.abs(n.y) * demi.y1;
    const prefere = n.y < -0.1 || (Math.abs(n.y) <= 0.1 && n.x > 0) ? 1 : -1;
    for (const sens of [prefere, -prefere]) {
      for (const [ecart, surcout] of [
        [etendue + 3, 14],
        [3 * etendue + 10, 120],
      ] as const) {
        candidats.push({
          e: { ...base, x: p.x + n.x * sens * ecart, y: p.y + n.y * sens * ecart },
          cout: cout + surcout + (sens === prefere ? 0 : 2),
        });
      }
    }
  }
  return candidats.sort((a, b) => a.cout - b.cout);
}

/**
 * Place les étiquettes (D50, D51) sans qu'elles se chevauchent ni couvrent un
 * équipement ou son nom. Une pastille de port part du bout de son câble et
 * glisse le long du tracé ; faute de place, elle se met à côté du câble, puis
 * un peu plus loin. Nom et VLAN : autour du milieu, puis de côté. Faute de
 * place libre, la moins recouverte (couvrir un nom, qui la cacherait, coûte
 * plus que frôler une autre étiquette).
 */
export function placerEtiquettes(elements: ATaguer[], obstacles: Boite[]): Map<string, Etiquette[]> {
  const poses: Boite[] = [];
  const resultat = new Map<string, Etiquette[]>();
  const choisir = (candidats: Candidat[]): Etiquette | undefined => {
    if (candidats.length === 0) return undefined;
    // Seuls comptent les obstacles et étiquettes proches des candidates.
    const boites = candidats.map((c) => boiteEtiquette(c.e, 2));
    const zone = boites.reduce((z, b) => ({
      x0: Math.min(z.x0, b.x0),
      y0: Math.min(z.y0, b.y0),
      x1: Math.max(z.x1, b.x1),
      y1: Math.max(z.y1, b.y1),
    }));
    const proches = obstacles.filter((o) => chevauche(o, zone));
    const posees = poses.filter((o) => chevauche(o, zone));
    let meilleur: Etiquette | undefined;
    let moindre = Number.POSITIVE_INFINITY;
    for (const [k, c] of candidats.entries()) {
      const b = boites[k] as Boite;
      let gene = 0;
      for (const o of proches) gene += 3 * recouvrement(b, o);
      for (const o of posees) gene += 2 * recouvrement(b, o);
      if (gene === 0) return c.e;
      if (gene < moindre) {
        moindre = gene;
        meilleur = c.e;
      }
    }
    return meilleur;
  };
  const poser = (id: string, e: Etiquette | undefined) => {
    if (!e) return;
    poses.push(boiteEtiquette(e, 2));
    resultat.set(id, [...(resultat.get(id) ?? []), e]);
  };
  // Les ports d'abord (au plus près des équipements, en suivant le tracé même
  // coudé), puis noms et VLAN autour du milieu.
  for (const el of elements) {
    if (!el.ports) continue;
    const parcours = parcourir(el.trace);
    const fin = Math.max(10, Math.min(parcours.total * 0.45, 160));
    for (const [rang, texte] of el.ports.entries()) {
      if (!texte) continue;
      const distances: { d: number; cout: number }[] = [];
      // Passé la moitié du câble, la pastille se rapprocherait de l'autre bout : plus coûteux.
      // Au-delà, une pastille n'appartiendrait plus visiblement à son bout.
      const loin = Math.max(8, Math.min(parcours.total - 8, fin + 140));
      for (let d = 8; d <= loin; d += 4)
        distances.push({ d: rang === 0 ? d : parcours.total - d, cout: d + Math.max(0, d - fin) * 2 });
      const base = { texte: abreger(texte, 22), taille: 10, genre: "port" as const };
      poser(el.id, choisir(autourDuTrace(parcours, distances, base)));
    }
  }
  for (const el of elements) {
    const parcours = parcourir(el.trace);
    for (const [genre, texte, couleur] of [
      ["nom", el.nom, undefined],
      ["vlan", el.vlan?.texte, el.vlan?.couleur],
    ] as const) {
      if (!texte) continue;
      const taille = genre === "nom" ? 11 : 10;
      const court = genre === "vlan" ? abregerVlans(texte) : abreger(texte);
      const base = {
        texte: court,
        taille,
        genre,
        ...(couleur ? { couleur } : {}),
        ...(genre === "vlan" && court !== texte ? { titre: texte } : {}),
      };
      const distances = autourDuMilieu(parcours.total).map((d) => ({
        d,
        cout: Math.abs(d - parcours.total / 2),
      }));
      poser(el.id, choisir(autourDuTrace(parcours, distances, base)));
    }
  }
  return resultat;
}

/** Distances du milieu vers les bouts (8 px de pas, 240 px au plus de part et d'autre). */
function autourDuMilieu(total: number): number[] {
  const distances = [total / 2];
  for (let pas = 8; pas < Math.min(total / 2 - 12, 240); pas += 8)
    distances.push(total / 2 - pas, total / 2 + pas);
  return distances;
}

/** Un tracé parcouru à distance donnée de son début : le point et la normale du segment. */
function parcourir(trace: Point[]) {
  const longueurs = trace
    .slice(1)
    .map((p, i) => Math.hypot(p.x - (trace[i]?.x ?? 0), p.y - (trace[i]?.y ?? 0)));
  const total = longueurs.reduce((a, b) => a + b, 0);
  const a = (d: number): { p: Point; n: Point } => {
    let reste = Math.max(0, Math.min(total, d));
    for (let i = 0; i < longueurs.length; i++) {
      const l = longueurs[i] ?? 0;
      const debut = trace[i] as Point;
      const fin = trace[i + 1] as Point;
      if (reste <= l || i === longueurs.length - 1) {
        const t = l ? reste / l : 0;
        const n = l ? { x: -(fin.y - debut.y) / l, y: (fin.x - debut.x) / l } : { x: 0, y: -1 };
        return { p: { x: debut.x + (fin.x - debut.x) * t, y: debut.y + (fin.y - debut.y) * t }, n };
      }
      reste -= l;
    }
    return { p: trace[0] ?? { x: 0, y: 0 }, n: { x: 0, y: -1 } };
  };
  return { total, a };
}

/** L'annotation VLAN d'un câble : son texte, et la couleur du VLAN déclaré pour un câble en accès. */
export type AnnotationVlan = { texte: string; couleur?: string };

/** Un câble qui s'annote de son mode VLAN : en trunk, ou en accès avec son VLAN. */
export const sAnnoteDeSonVlan = (schema: Schema, lien: Lien): boolean =>
  estTrunk(schema, lien) || vlanDAcces(schema, lien) !== undefined;

/**
 * Les annotations VLAN des câbles, par câble : « Trunk · U 1 · T 10,20 » pour un trunk, « Access · U 30 »
 * pour un câble en accès qui a son VLAN. Des câbles parallèles aux mêmes VLAN n'en portent qu'une, sur le
 * premier ; s'ils diffèrent, chacun a la sienne. Un agrégat a la sienne (un seul câble au modèle, un seul
 * réglage VLAN).
 */
export function annotationsDeVlan(schema: Schema, liens: Lien[]): Map<string, AnnotationVlan> {
  const paires = new Map<string, { lien: Lien; annotation: AnnotationVlan }[]>();
  const annotations = new Map<string, AnnotationVlan>();
  for (const lien of liens) {
    const acces = estTrunk(schema, lien) ? undefined : vlanDAcces(schema, lien);
    if (!estTrunk(schema, lien) && acces === undefined) continue;
    const couleur = acces !== undefined ? schema.vlans.find((v) => v.numero === acces)?.couleur : undefined;
    const annotation: AnnotationVlan =
      acces !== undefined
        ? { texte: texteAcces(acces), ...(couleur ? { couleur } : {}) }
        : { texte: texteTrunk(schema, lien) };
    if (enAgregat(schema, lien)) {
      annotations.set(lien.id, annotation);
      continue;
    }
    const cle = [lien.a.equipement, lien.b.equipement].sort().join("|");
    paires.set(cle, [...(paires.get(cle) ?? []), { lien, annotation }]);
  }
  for (const groupe of paires.values()) {
    const memes = groupe.every((g) => g.annotation.texte === groupe[0]?.annotation.texte);
    for (const [rang, g] of groupe.entries()) if (!memes || rang === 0) annotations.set(g.lien.id, g.annotation);
  }
  return annotations;
}

/** VLAN d'un câble, d'après la configuration de l'un de ses bouts (son agrégat, sinon son port). */
export function vlanDuLien(schema: Schema, lien: Lien): { texte: string; couleur?: string } | undefined {
  for (const bout of [lien.a, lien.b]) {
    const texte = texteVlan(schema, configDuBout(schema, bout));
    if (texte) return texte;
  }
  return undefined;
}

/**
 * Repère d'un agrégat de liens (D51) : l'ellipse qui enserre ses câbles, en travers du tracé. `rx` est son
 * demi-axe en travers des câbles, `ry` le long. Le cercle n'a pas de libellé ; un trunk, lui, s'annote.
 */
export type Marque = { x: number; y: number; angle: number; rx: number; ry: number };

/**
 * Le cercle d'un faisceau, posé sur le tracé de son axe : `etendue` est la distance entre ses deux traits
 * extrêmes. Il se met au milieu du tracé si c'est sur un segment droit, loin d'un coude ; sinon au milieu
 * du plus long segment.
 */
export function marqueDeFaisceau(trace: Point[], etendue = 0): Marque {
  const rx = Math.max(12, etendue / 2 + 8);
  const ry = 5;
  const segments = segmentsDe(trace).map(([a, b]) => ({ a, b, l: Math.hypot(b.x - a.x, b.y - a.y) }));
  const poser = (s: (typeof segments)[number], d: number): Marque => {
    const t = s.l ? d / s.l : 0;
    // L'angle de la normale du segment : le grand axe de l'ellipse est en travers du câble.
    const angle = s.l ? (Math.atan2(s.b.x - s.a.x, -(s.b.y - s.a.y)) * 180) / Math.PI : -90;
    return { x: s.a.x + (s.b.x - s.a.x) * t, y: s.a.y + (s.b.y - s.a.y) * t, angle, rx, ry };
  };
  const premier = segments[0];
  if (!premier) return { x: trace[0]?.x ?? 0, y: trace[0]?.y ?? 0, angle: -90, rx, ry };
  // Place qu'il faut de part et d'autre : le cercle, et un peu d'air avant le coude.
  const garde = ry + 9;
  let reste = segments.reduce((n, s) => n + s.l, 0) / 2;
  for (const s of segments) {
    if (reste <= s.l) {
      if (segments.length === 1 || (reste >= garde && s.l - reste >= garde)) return poser(s, reste);
      break;
    }
    reste -= s.l;
  }
  const long = segments.reduce((m, s) => (s.l > m.l ? s : m), premier);
  return poser(long, long.l / 2);
}

export const dessinerMarque = (m: Marque, couleur: string): string =>
  `<ellipse cx="${m.x.toFixed(1)}" cy="${m.y.toFixed(1)}" rx="${m.rx}" ry="${m.ry}" transform="rotate(${m.angle.toFixed(1)} ${m.x.toFixed(1)} ${m.y.toFixed(1)})" fill="none" stroke="${couleur}" stroke-width="1.6"/>`;

const boiteMarque = (m: Marque): Boite => ({
  x0: m.x - m.rx - 1,
  y0: m.y - m.rx - 1,
  x1: m.x + m.rx + 1,
  y1: m.y + m.rx + 1,
});

/** Épaisseur au plus des traits d'un faisceau qui en compte plusieurs : serrés, ils restent distincts. */
const EPAISSEUR_FAISCEAU = 2.4;
/** Ce que la légende dit du cercle. */
export const LEGENDE_CERCLE = "Cercle : agrégat de liens";

/** Ce qui est affiché : les choix du schéma, sinon ceux de la vue. */
export function affichageDe(schema: Pick<Schema, "affichage">, v: Vue): Affichage {
  const choix = schema.affichage ?? {};
  return {
    ports: choix.ports ?? v.ports,
    vlans: choix.vlans ?? v.vlans,
    adresses: choix.adresses ?? true,
    nomsCables: choix.nomsCables ?? true,
    annotations: choix.annotations ?? true,
    zones: choix.zones ?? true,
  };
}

/** Ce qu'occupent un équipement et son nom (obstacles des étiquettes). */
export function occupationDe(e: Equipement, adresses: boolean): Boite[] {
  const b = boiteDe(e, 1);
  const adresse = adresses ? adresseAffichee(e) : "";
  const demi = Math.max(e.nom.length * 7.6, adresse.length * 6.8) / 2 + 3;
  const badge: Boite[] =
    e.membres.length > 1 ? [{ x0: b.x1 - 17, y0: b.y0 - 11, x1: b.x1 + 9, y1: b.y0 + 6 }] : [];
  return [
    ...badge,
    b,
    { x0: e.position.x - demi, y0: b.y1, x1: e.position.x + demi, y1: ligneDuNom(b, adresse ? 1 : 0) + 6 },
  ];
}

export type MiseEnPage = {
  /**
   * `trace` : l'axe du câble (étiquettes, prise du pointeur) ; `brins` : ses traits, s'il en a plusieurs
   * (un par port de son agrégat) ; `cercle` : le repère de l'agrégat, porté par le premier câble du
   * faisceau ; `coude` : tracé à angle droit ; `route` : sa route, s'il n'a pas de point de passage.
   */
  liens: {
    lien: Lien;
    trace: Point[];
    trait: Trait;
    coude: boolean;
    route?: Route;
    brins?: Point[][];
    cercle?: Marque;
  }[];
  /** `fleche` : relation orientée (une paire haute disponibilité n'en a pas). */
  relations: { relation: Relation; trace: Point[]; trait: Trait; nom: string; fleche: boolean }[];
  etiquettes: Map<string, Etiquette[]>;
  affichage: Affichage;
};

/** Tracés et étiquettes d'un schéma : la même mise en page à l'écran et à l'export (D50). */
export function mettreEnPage(schema: Schema, v: Vue, caches: Set<string>, sauf?: string): MiseEnPage {
  const affichage = affichageDe(schema, v);
  const parId = new Map(schema.equipements.map((e) => [e.id, e]));
  const visibles = liensVisibles(schema, v).filter(
    (l) => !caches.has(l.a.equipement) && !caches.has(l.b.equipement) && l.id !== sauf,
  );
  const faisceaux = faisceauxDe(schema, visibles);
  const ecarts = ecartsParalleles(visibles, faisceaux);
  const routes = choisirRoutes(
    schema,
    visibles,
    schema.equipements.filter((e) => !caches.has(e.id)),
    ecarts,
    faisceaux,
  );
  const liens: MiseEnPage["liens"] = [];
  // Une interface partagée par plusieurs câbles (un nuage) n'a qu'une pastille.
  const nommees = new Set<string>();
  const portUnique = (bout: Lien["a"]) => {
    const cle = `${bout.equipement}/${bout.interface}`;
    if (nommees.has(cle)) return undefined;
    nommees.add(cle);
    // Bout d'un agrégat : son nom et ses ports (« Trk1 · 1/1,2/1 »).
    return texteAgregat(schema, bout) ?? interfaceDe(schema, bout)?.nom;
  };
  const aTaguer: ATaguer[] = [];
  const marques: Boite[] = [];
  // Le trunk, et le câble en accès qui a son VLAN, s'annotent sur le câble dans toutes les vues, sauf si le
  // schéma masque les VLAN des câbles.
  const annotationsVlan =
    schema.affichage?.vlans !== false ? annotationsDeVlan(schema, visibles) : new Map<string, AnnotationVlan>();
  // Les câbles d'un même agrégat : ils partagent un cercle.
  const grappes = new Map<string, { rang: number; lien: Lien; ecart: number; brins: number }[]>();
  for (const lien of visibles) {
    const a = parId.get(lien.a.equipement);
    const b = parId.get(lien.b.equipement);
    if (!a || !b) continue;
    const ecart = ecarts.get(lien.id) ?? 0;
    const faisceau = faisceaux.get(lien.id);
    const trace = traceDuLien(schema, lien, a, b, ecart, routes.get(lien.id));
    // Un agrégat : un trait par port, de part et d'autre de l'axe du câble.
    const brins =
      faisceau && faisceau.brins > 1
        ? Array.from({ length: faisceau.brins }, (_, k) =>
            traceDuLien(
              schema,
              lien,
              a,
              b,
              ecart + (k - (faisceau.brins - 1) / 2) * PAS_FAISCEAU,
              routes.get(lien.id),
            ),
          )
        : undefined;
    if (faisceau) {
      const cle = `${[lien.a.equipement, lien.b.equipement].sort().join("|")}|${faisceau.cle}`;
      grappes.set(cle, [
        ...(grappes.get(cle) ?? []),
        { rang: liens.length, lien, ecart, brins: faisceau.brins },
      ]);
    }
    // Route d'un câble coudé sans point de passage : le canevas en coulisse le palier (D53).
    const coude = estCoude(schema, lien);
    const route =
      coude && lien.points.length === 0
        ? (routeDuPalier(lien) ?? routes.get(lien.id) ?? routeParDefaut(a.position, b.position))
        : undefined;
    liens.push({
      lien,
      trace,
      trait: traitDuLien(lien),
      coude,
      ...(route ? { route } : {}),
      ...(brins ? { brins } : {}),
    });
    const libelles = lien.libelles;
    const ports = libelles ? libelles === "ports" || libelles === "tout" : affichage.ports;
    const nom = libelles ? libelles === "nom" || libelles === "tout" : affichage.nomsCables;
    aTaguer.push({
      id: lien.id,
      trace,
      ...(ports
        ? {
            ports: [portUnique(lien.a), portUnique(lien.b)] as [string | undefined, string | undefined],
          }
        : {}),
      ...(nom && lien.nom ? { nom: lien.nom } : {}),
      // Une seule étiquette par câble : celle du mode (trunk, accès) tient lieu d'étiquette de VLAN.
      ...(sAnnoteDeSonVlan(schema, lien)
        ? annotationsVlan.has(lien.id)
          ? { vlan: annotationsVlan.get(lien.id) as AnnotationVlan }
          : {}
        : affichage.vlans
          ? { vlan: vlanDuLien(schema, lien) }
          : {}),
    });
  }
  // Le cercle de chaque faisceau, sur son axe, assez large pour entourer tous ses traits.
  for (const grappe of grappes.values()) {
    const premier = grappe[0];
    const entree = premier ? liens[premier.rang] : undefined;
    const a = premier ? parId.get(premier.lien.a.equipement) : undefined;
    const b = premier ? parId.get(premier.lien.b.equipement) : undefined;
    if (!premier || !entree || !a || !b) continue;
    // Dans le sens du premier câble (l'écart d'un câble tracé dans l'autre sens est de signe opposé).
    const direct = premier.lien.a.equipement <= premier.lien.b.equipement;
    const sens = (l: Lien) => (l.a.equipement <= l.b.equipement === direct ? 1 : -1);
    const bords = grappe.flatMap((g) => {
      const demi = ((g.brins - 1) * PAS_FAISCEAU) / 2;
      return [g.ecart * sens(g.lien) - demi, g.ecart * sens(g.lien) + demi];
    });
    const [bas, haut] = [Math.min(...bords), Math.max(...bords)];
    const axe =
      grappe.length === 1
        ? entree.trace
        : traceDuLien(schema, premier.lien, a, b, (bas + haut) / 2, routes.get(premier.lien.id));
    entree.cercle = marqueDeFaisceau(axe, haut - bas);
    marques.push(boiteMarque(entree.cercle));
    // Plusieurs traits sous un même cercle : affinés, pour rester distincts.
    if (haut - bas > 0)
      for (const g of grappe) {
        const e = liens[g.rang];
        if (e) e.trait = { ...e.trait, epaisseur: Math.min(e.trait.epaisseur, EPAISSEUR_FAISCEAU) };
      }
  }
  // Une relation entre deux équipements déjà reliés par des câbles droits passe à côté d'eux.
  const paires = new Map<string, number>();
  for (const l of visibles) {
    if (l.points.length > 0) continue;
    const cle = [l.a.equipement, l.b.equipement].sort().join("|");
    paires.set(cle, (paires.get(cle) ?? 0) + 1);
  }
  const relations: MiseEnPage["relations"] = [];
  for (const relation of schema.relations) {
    if (!relationVisible(relation, v) || caches.has(relation.de) || caches.has(relation.vers)) continue;
    const de = parId.get(relation.de);
    const vers = parId.get(relation.vers);
    if (!de || !vers) continue;
    const cables = paires.get([relation.de, relation.vers].sort().join("|")) ?? 0;
    const ecart =
      cables > 0 && relation.points.length === 0
        ? (((cables - 1) / 2) * 16 + 14) * (relation.de <= relation.vers ? 1 : -1)
        : 0;
    const trace = traceEntre(de.position, vers.position, relation.points, ecart, [
      boiteDe(de),
      boiteDe(vers),
    ]);
    const nom =
      relation.nom || (typeof relation.proprietes.ssid === "string" ? relation.proprietes.ssid : "");
    const fleche = !RELATIONS_SYMETRIQUES.has(relation.genre);
    relations.push({ relation, trace, trait: traitDeRelation(relation), nom, fleche });
    if (nom && affichage.nomsCables) aTaguer.push({ id: relation.id, trace, nom });
  }
  // Légendes des zones et noms des groupes : aucune étiquette ne les couvre.
  const legendes: Boite[] = [
    ...(affichage.zones ? schema.annotations.filter((n) => estZone(n) && n.texte) : []).map((n) => {
      const largeur = (n.texte.split("\n")[0] ?? "").length * 8 + 4;
      const x =
        n.forme === "ellipse" ? n.position.x + (n.largeur ?? 240) / 2 - largeur / 2 : n.position.x + 8;
      return { x0: x, y0: n.position.y + 4, x1: x + largeur, y1: n.position.y + 23 };
    }),
    ...schema.groupes.map((g) => ({
      x0: g.position.x + 8,
      y0: g.position.y + 4,
      x1: g.position.x + 12 + (g.nom || "Groupe").length * 8,
      y1: g.position.y + 23,
    })),
  ];
  const obstacles = [
    ...schema.equipements
      .filter((e) => !caches.has(e.id))
      .flatMap((e) => occupationDe(e, affichage.adresses)),
    ...marques,
    ...legendes,
  ];
  return { liens, relations, etiquettes: placerEtiquettes(aTaguer, obstacles), affichage };
}

/** Le SVG complet d'un schéma, cadré sur son contenu. */
export function dessinerSchema(schema: Schema, options: { vue?: Vue; legende?: boolean } = {}): string {
  const v = options.vue ?? vueDe(schema.vue);
  const caches = masques(schema);
  const cadre = cadreDuSchema(schema);
  const parties: string[] = [];
  for (const g of schema.groupes) parties.push(dessinerGroupe(g));
  if (affichageDe(schema, v).zones)
    for (const n of schema.annotations) if (estZone(n)) parties.push(dessinerAnnotation(n));
  const legende = new Map<string, Trait>();
  const page = mettreEnPage(schema, v, caches);
  let cercles = false;
  for (const { lien, trace, trait, brins, cercle } of page.liens) {
    legende.set(trait.nom, traitDuLien(lien));
    for (const brin of brins ?? [trace]) parties.push(dessinerTrace(brin, trait));
    if (cercle) parties.push(dessinerMarque(cercle, trait.couleur));
    cercles ||= cercle !== undefined;
  }
  for (const { trace, trait, fleche } of page.relations) {
    legende.set(trait.nom, trait);
    parties.push(dessinerTrace(trace, trait, fleche));
  }
  for (const etiquettes of page.etiquettes.values())
    for (const e of etiquettes) parties.push(dessinerEtiquette(e));

  for (const e of schema.equipements)
    if (!caches.has(e.id)) parties.push(dessinerEquipement(e, page.affichage.adresses));
  if (page.affichage.annotations)
    for (const n of schema.annotations) if (!estZone(n)) parties.push(dessinerAnnotation(n));

  let hauteur = cadre.hauteur;
  if (options.legende) {
    const presentes = FAMILLES.filter((f) => schema.equipements.some((e) => familleDe(e.type) === f.valeur));
    presentes.forEach((f, i) => {
      const y = cadre.y + hauteur + 14 + i * 18;
      parties.push(
        `<rect x="${cadre.x + 12}" y="${y - 6}" width="12" height="12" rx="3" fill="${f.couleur}"/>`,
        texteSvg(cadre.x + 30, y + 4, f.nom, `fill="${SECONDAIRE}" font-size="11"`),
      );
    });
    hauteur += presentes.length * 18 + (presentes.length ? 8 : 0);
  }
  if (options.legende && legende.size > 0) {
    const y0 = cadre.y + hauteur;
    [...legende.values()].forEach((trait, i) => {
      const y = y0 + 14 + i * 18;
      parties.push(
        dessinerTrace(
          [
            { x: cadre.x + 12, y },
            { x: cadre.x + 52, y },
          ],
          trait,
        ),
        texteSvg(cadre.x + 60, y + 4, trait.nom, `fill="${SECONDAIRE}" font-size="11"`),
      );
    });
    hauteur += 20 + legende.size * 18;
  }
  if (options.legende && cercles) {
    // Le cercle n'a pas de libellé sur le dessin : la légende dit ce qu'il veut dire.
    const y = cadre.y + hauteur + (legende.size > 0 ? -6 : 14);
    const trait: Trait = { couleur: SECONDAIRE, epaisseur: 2, nom: LEGENDE_CERCLE };
    parties.push(
      dessinerTrace(
        [
          { x: cadre.x + 12, y },
          { x: cadre.x + 52, y },
        ],
        trait,
      ),
      dessinerMarque({ x: cadre.x + 32, y, angle: 90, rx: 9, ry: 4 }, SECONDAIRE),
      texteSvg(cadre.x + 60, y + 4, LEGENDE_CERCLE, `fill="${SECONDAIRE}" font-size="11"`),
    );
    hauteur += legende.size > 0 ? 18 : 34;
  }

  return [
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${cadre.x} ${cadre.y} ${cadre.largeur} ${hauteur}" width="${cadre.largeur}" height="${hauteur}" font-family="${POLICE}">`,
    `<defs>${DEFINITIONS}</defs>`,
    ...parties,
    "</svg>",
  ].join("");
}

/** Description accessible d'un schéma (lecteurs d'écran). */
export function descriptionDuSchema(schema: Schema): string {
  const noms = schema.equipements.slice(0, 12).map((e) => e.nom);
  const reste = schema.equipements.length - noms.length;
  return `Schéma réseau « ${schema.titre} » : ${schema.equipements.length} équipement(s), ${schema.liens.length} lien(s)${noms.length ? ` — ${noms.join(", ")}${reste > 0 ? `, et ${reste} autre(s)` : ""}` : ""}.`;
}
