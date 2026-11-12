// Les lignes d'écriture d'un papier réglé : le texte s'y pose comme sur un cahier. L'application donne le pas (la
// hauteur d'une ligne d'écriture, en pixels) ; ici, seulement les comptes, sans la page : combien de lignes prend un
// titre, où reprend le texte après un bloc qui garde sa hauteur, d'où part le dessin du papier pour que sa ligne passe
// sous le pied des lettres. La lecture native compte de la même façon, avec les mêmes nombres.

import { estRegle, type Papier } from "./papier";

/** Un bloc qui finit à moins de cela d'une ligne y est : le texte reprend là, sans ligne de plus. */
export const TOLERANCE_DE_LIGNE = 0.5;

/** La place qu'un titre demande, en parts de la taille de ses lettres : au-delà d'une ligne, il en prend une de plus. */
export const PART_DU_TITRE = 1.1;

/** Le moteur range la page au soixante-quatrième de pixel : un pas plus fin se perdrait de ligne en ligne. */
export const GRAIN = 64;

/** Un bloc qui garde sa hauteur, et la hauteur à lui ajouter pour que la suite reprenne sur une ligne. */
export type Cale = { indice: number; hauteur: number };

/**
 * Le pas des lignes d'écriture : celui que donne l'application, pour un papier qui a un dessin ; 0 sinon (papier uni,
 * pas de papier, pas inconnu), et le texte ne suit alors aucune ligne. Il est arrondi au grain du moteur, comme le moteur
 * arrondit lui-même la hauteur d'une ligne de texte : cent lignes de texte font alors exactement cent pas du dessin et
 * de la mesure. L'écart avec le pas reçu est de moins d'un centième de pixel par ligne.
 */
export function pasDEcriture(papier: Papier | null, pas: number | null | undefined): number {
  if (!estRegle(papier) || typeof pas !== "number" || !Number.isFinite(pas) || pas < 1) return 0;
  return Math.round(pas * GRAIN) / GRAIN;
}

/** Le nombre de lignes d'écriture que prend chaque ligne d'un titre : une, ou plus si ses lettres n'y tiennent pas. */
export function lignesDeTitre(taille: number, pas: number): number {
  if (!(pas > 0) || !(taille > 0)) return 1;
  // Une erreur d'arrondi ne coûte pas une ligne : 1,1 × 20 vaut 22,000000000000004.
  return Math.max(1, Math.ceil((taille * PART_DU_TITRE) / pas - 1e-9));
}

/** La première ligne à cette hauteur ou plus bas ; une hauteur à moins de la tolérance sous une ligne y est encore. */
export function ligneSuivante(hauteur: number, pas: number, tolerance = TOLERANCE_DE_LIGNE): number {
  if (!(pas > 0)) return hauteur;
  // « + 0 » : jamais de zéro négatif.
  return Math.ceil((hauteur - tolerance) / pas) * pas + 0;
}

/**
 * Ce qu'il manque à un bloc qui finit à cette hauteur pour que la suite reprenne sur une ligne. Rien s'il y finit, ou
 * s'il la dépasse de moins que la tolérance : la suite ne remonte pas, et ne saute pas une ligne pour si peu. Un manque
 * plus fin que le grain du moteur n'est pas une cale.
 */
export function cale(hauteur: number, pas: number, tolerance = TOLERANCE_DE_LIGNE): number {
  if (!(pas > 0)) return 0;
  const manque = ligneSuivante(hauteur, pas, tolerance) - hauteur;
  return manque >= 1 / GRAIN ? manque : 0;
}

/**
 * D'où part le dessin du papier, compté depuis le haut de la feuille : la ligne d'écriture passe au pied des lettres de
 * chaque ligne de texte. `hautDuTexte` : le haut de la première ligne ; `pied` : du haut d'une ligne au pied de ses lettres.
 */
export function phaseDuPapier(hautDuTexte: number, pied: number, pas: number): number {
  if (!(pas > 0)) return 0;
  const phase = (hautDuTexte + pied) % pas;
  return phase < 0 ? phase + pas : phase;
}

/**
 * De combien descendre un titre (ou le monter, si c'est négatif) pour que ses lettres se posent sur la dernière des
 * lignes qu'il prend : `pied` et `piedDuTitre` sont mesurés du haut d'une ligne au pied des lettres, dans le texte et
 * dans le titre.
 */
export function leveeDeTitre(pied: number, piedDuTitre: number, lignes: number, pas: number): number {
  return (Math.max(1, lignes) - 1) * pas + pied - piedDuTitre;
}

/**
 * Sur la feuille sans fin : les cales à poser, de haut en bas. `pied` : là où finit un bloc à sa place naturelle (sans
 * aucune cale), compté depuis le haut du texte ; les blocs sans pied ne finissent rien.
 */
export function caler(boites: readonly { pied?: number }[], pas: number): Cale[] {
  const cales: Cale[] = [];
  let cumul = 0;
  boites.forEach((boite, indice) => {
    if (boite.pied === undefined) return;
    const hauteur = cale(boite.pied + cumul, pas);
    if (hauteur <= 0) return;
    cales.push({ indice, hauteur });
    cumul += hauteur;
  });
  return cales;
}
