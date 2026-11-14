// Où poser un menu flottant pour qu'il reste entier dans la fenêtre, d'après sa taille mesurée.

export type Ancre = {
  x: number;
  y: number;
  /** Le menu finit à `x` (aligné à droite d'un bouton) au lieu d'y commencer. */
  fin?: boolean;
};

type Taille = { largeur: number; hauteur: number };

/**
 * Coin haut gauche d'un menu de `taille` ancré à `ancre`, ramené dans `fenetre` avec une marge.
 * Un menu plus grand que la fenêtre se cale en haut à gauche : il défile alors.
 */
export function placerMenu(ancre: Ancre, taille: Taille, fenetre: Taille, marge = 8): { x: number; y: number } {
  const borner = (voulu: number, longueur: number, place: number) =>
    Math.round(Math.max(marge, Math.min(voulu, place - longueur - marge)));
  return {
    x: borner(ancre.fin ? ancre.x - taille.largeur : ancre.x, taille.largeur, fenetre.largeur),
    y: borner(ancre.y, taille.hauteur, fenetre.hauteur),
  };
}

/** Rectangle de l'écran qu'occupe l'objet auquel une bulle se rapporte. */
export type Zone = { x0: number; y0: number; x1: number; y1: number };

/**
 * Coin haut gauche d'une bulle posée près d'un objet, dans son cadre : du premier côté de `cotes` où elle
 * tient en entier (à droite, à gauche, dessous, dessus), ramenée dans le cadre sur l'autre axe. Rend `null`
 * si elle ne tient d'aucun côté : la bulle devient alors un panneau en bas du cadre.
 */
export function placerBulle(
  objet: Zone,
  taille: Taille,
  cadre: Taille,
  cotes: ("droite" | "gauche" | "dessous" | "dessus")[] = ["droite", "gauche", "dessous", "dessus"],
  ecart = 12,
  marge = 8,
): { x: number; y: number } | null {
  if (taille.largeur + 2 * marge > cadre.largeur || taille.hauteur + 2 * marge > cadre.hauteur) return null;
  const borner = (voulu: number, longueur: number, place: number) =>
    Math.round(Math.max(marge, Math.min(voulu, place - longueur - marge)));
  const milieu = (objet.x0 + objet.x1) / 2;
  for (const cote of cotes) {
    if (cote === "droite" && objet.x1 + ecart + taille.largeur + marge <= cadre.largeur) {
      return { x: Math.round(Math.max(marge, objet.x1 + ecart)), y: borner(objet.y0, taille.hauteur, cadre.hauteur) };
    }
    if (cote === "gauche" && objet.x0 - ecart - taille.largeur >= marge) {
      return {
        x: Math.round(Math.min(objet.x0 - ecart, cadre.largeur - marge) - taille.largeur),
        y: borner(objet.y0, taille.hauteur, cadre.hauteur),
      };
    }
    if (cote === "dessous" && objet.y1 + ecart + taille.hauteur + marge <= cadre.hauteur) {
      return {
        x: borner(milieu - taille.largeur / 2, taille.largeur, cadre.largeur),
        y: Math.round(Math.max(marge, objet.y1 + ecart)),
      };
    }
    if (cote === "dessus" && objet.y0 - ecart - taille.hauteur >= marge) {
      return {
        x: borner(milieu - taille.largeur / 2, taille.largeur, cadre.largeur),
        y: Math.round(Math.min(objet.y0 - ecart, cadre.hauteur - marge) - taille.hauteur),
      };
    }
  }
  return null;
}
