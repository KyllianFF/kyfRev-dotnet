// Le découpage d'une fiche en pages, sans la page : des boîtes mesurées (leur place naturelle, c'est-à-dire sans aucun
// espace de saut de page), la géométrie de la feuille, et il en sort les espaces à ouvrir et le nombre de pages.
// Un bloc n'est jamais coupé : celui qui franchirait le bas de sa page commence la suivante.
// Sur un papier réglé (lignes.ts), le même passage dit aussi les cales : ce qu'il faut ajouter sous un bloc pour que la
// suite reprenne sur une ligne d'écriture, comptée depuis le haut du texte de sa page.

import { type Cale, cale } from "./lignes";

/** Ce qu'est une boîte : un bloc ordinaire, une ligne de tableau (elle se pousse par ses cases), une figure qui ne se coupe pas. */
export type Genre = "bloc" | "ligne" | "figure";

/** Une boîte mesurée : son haut compté depuis le haut du texte de la première page, espaces de saut retirés. */
export type Boite = {
  haut: number;
  hauteur: number;
  genre: Genre;
  /** La marque d'un saut de page : ce qui la suit commence la page suivante, ou celle d'après une page laissée blanche. */
  saut?: "page" | "blanche";
  /**
   * La place à garder au-dessus d'elle si elle commence une page : la ligne d'en-tête de son tableau, remontrée en haut
   * de chaque page où il continue.
   */
  rappel?: number;
  /**
   * Là où finit le bloc que cette boîte termine, compté comme `haut` : sur un papier réglé, ce qui le suit reprend sur
   * une ligne d'écriture. Sans pied, la boîte ne finit aucun bloc (une ligne au milieu d'un tableau).
   */
  pied?: number;
};

/**
 * La feuille, en pixels CSS : sa hauteur, ses marges du haut et du bas, l'écart visible entre deux pages ; et, sur un
 * papier réglé, le pas de ses lignes d'écriture, qui repartent du haut du texte de chaque page.
 */
export type Geometrie = { hauteur: number; marge: number; ecart: number; ligne?: number };

/** Un espace à ouvrir avant une boîte, et la page (à partir de 0) où elle commence alors. */
export type Saut = { indice: number; decalage: number; page: number };

export type Pagination = {
  sauts: Saut[];
  /** Les figures plus hautes que le texte d'une page : à réduire à cette hauteur. */
  aReduire: number[];
  pages: number;
  /** Les pages qui commencent par un saut : l'écart au-dessus d'elles n'est traversé par aucun texte. */
  coupees: number[];
  /** Sur un papier réglé : les blocs à caler, pour que la suite reprenne sur une ligne. */
  cales: Cale[];
};

/** L'écart entre deux pages, sur le bureau. */
export const ECART = 16;

/** Les mesures du navigateur sont des fractions de pixel : en dessous, deux places sont les mêmes. */
const TOLERANCE = 0.5;

/** La hauteur du texte d'une page. */
export const hauteurUtile = (geometrie: Geometrie) => geometrie.hauteur - 2 * geometrie.marge;

/** D'un haut de texte au suivant : la page et l'écart. Entre deux textes, il y a donc marge + écart + marge. */
export const pasDePage = (geometrie: Geometrie) => geometrie.hauteur + geometrie.ecart;

/**
 * Une seule passe, de haut en bas, sur les places naturelles : le résultat ne dépend pas des espaces déjà ouverts, et
 * le recalculer ne le change pas.
 */
export function paginer(boites: readonly Boite[], geometrie: Geometrie): Pagination {
  const utile = hauteurUtile(geometrie);
  const pas = pasDePage(geometrie);
  const sauts: Saut[] = [];
  const aReduire: number[] = [];
  const coupees = new Set<number>();
  const cales: Cale[] = [];
  const ligne = geometrie.ligne ?? 0;
  let cumul = 0;
  let bas = 0;
  /** Après une marque de saut : la page où commence la boîte suivante. */
  let forcee: number | null = null;
  const couper = (de: number, a: number) => {
    for (let page = de + 1; page <= a; page++) coupees.add(page);
  };
  boites.forEach((boite, indice) => {
    let hauteur = boite.hauteur;
    const reduite = boite.genre === "figure" && hauteur > utile + TOLERANCE;
    if (reduite) {
      aReduire.push(indice);
      hauteur = utile;
    }
    let haut = boite.haut + cumul;
    let page = Math.max(0, Math.floor((haut + TOLERANCE) / pas));
    if (forcee !== null) {
      // Un saut demandé : même s'il restait de la place, même en haut d'une page.
      const decalage = forcee * pas - haut;
      if (decalage > TOLERANCE) {
        sauts.push({ indice, decalage, page: forcee });
        couper(page, forcee);
        cumul += decalage;
        haut += decalage;
        page = forcee;
      }
      forcee = null;
    }
    const debut = page * pas;
    const fin = debut + utile;
    const depasse = haut + hauteur > fin + TOLERANCE;
    const enTete = haut <= debut + TOLERANCE;
    // Plus haute qu'une page, elle suit son cours là où elle est ; sauf si elle commençait dans la marge du bas.
    const troisPetitsPoints = hauteur > utile + TOLERANCE && haut < fin - TOLERANCE;
    // La marque d'un saut finit sa page : elle n'est jamais poussée, quitte à tenir dans la marge du bas.
    if (depasse && !enTete && !troisPetitsPoints && !boite.saut) {
      const decalage = debut + pas - haut + (boite.rappel ?? 0);
      sauts.push({ indice, decalage, page: page + 1 });
      couper(page, page + 1);
      cumul += decalage;
      haut += decalage;
    }
    if (boite.saut) forcee = page + (boite.saut === "blanche" ? 2 : 1);
    bas = Math.max(bas, haut + hauteur);
    if (ligne > 0 && boite.pied !== undefined && !boite.saut) {
      // Les lignes se comptent depuis le haut du texte de la page où le bloc finit.
      const pied = reduite ? haut + hauteur : boite.pied + cumul;
      const dans = pied - Math.max(0, Math.floor((pied + TOLERANCE) / pas)) * pas;
      const manque = cale(dans, ligne);
      // Sous la dernière ligne de la page, rien ne reprend : la cale n'irait que dans la marge.
      if (manque > 0 && dans + manque <= utile + TOLERANCE) {
        cales.push({ indice, hauteur: manque });
        cumul += manque;
      }
    }
  });
  let pages = Math.max(1, Math.floor((bas - TOLERANCE) / pas) + 1);
  // Une marque en dernier : ses pages sont là quand même.
  if (forcee !== null) {
    couper(pages - 1, forcee);
    pages = Math.max(pages, forcee + 1);
  }
  return { sauts, aReduire, pages, coupees: [...coupees], cales };
}

/**
 * Les boîtes telles qu'elles se présentent une fois la pagination appliquée : un bloc ou une figure descend, une ligne
 * de tableau grandit par le haut (ses cases reçoivent l'espace), une figure trop haute est réduite ; une cale s'ajoute
 * sous son bloc, et descend ce qui le suit.
 */
export function appliquer(boites: readonly Boite[], pagination: Pagination, geometrie: Geometrie): Boite[] {
  const decalages = new Map(pagination.sauts.map((saut) => [saut.indice, saut.decalage]));
  const ajouts = new Map(pagination.cales.map((ajout) => [ajout.indice, ajout.hauteur]));
  const reduites = new Set(pagination.aReduire);
  let cumul = 0;
  return boites.map((boite, indice) => {
    const decalage = decalages.get(indice) ?? 0;
    const hauteur = reduites.has(indice) ? hauteurUtile(geometrie) : boite.hauteur;
    const haut = boite.haut + cumul;
    cumul += decalage;
    const pied = boite.pied === undefined ? {} : { pied: boite.pied + cumul };
    cumul += ajouts.get(indice) ?? 0;
    return boite.genre === "ligne"
      ? { ...boite, haut, hauteur: hauteur + decalage, ...pied }
      : { ...boite, haut: haut + decalage, hauteur, ...pied };
  });
}

/** L'inverse d'`appliquer` pour les places : ce que la page mesure, espaces retirés. Les figures réduites le restent. */
export function naturelles(placees: readonly Boite[], sauts: readonly Saut[], cales: readonly Cale[] = []): Boite[] {
  const decalages = new Map(sauts.map((saut) => [saut.indice, saut.decalage]));
  const ajouts = new Map(cales.map((ajout) => [ajout.indice, ajout.hauteur]));
  let cumul = 0;
  return placees.map((boite, indice) => {
    const decalage = decalages.get(indice) ?? 0;
    const avant = cumul;
    cumul += decalage;
    const pied = boite.pied === undefined ? {} : { pied: boite.pied - cumul };
    const naturelle =
      boite.genre === "ligne"
        ? { ...boite, haut: boite.haut - avant, hauteur: boite.hauteur - decalage, ...pied }
        : { ...boite, haut: boite.haut - cumul, ...pied };
    cumul += ajouts.get(indice) ?? 0;
    return naturelle;
  });
}
