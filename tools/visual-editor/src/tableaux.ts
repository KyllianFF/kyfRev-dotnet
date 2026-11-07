// Ce que l'onglet Tableau fait d'un tableau : changer son apparence, trier ses lignes.
// Des fonctions sur le modèle du document, sans la page : l'éditeur les applique.

import { Fragment, type Node } from "prosemirror-model";
import { type Command, Selection, TextSelection } from "prosemirror-state";
import { nextCell } from "prosemirror-tables";
import type { Apparence, Bandes, Theme, Traits } from "./apparence";

const BANDES: Bandes[] = ["aucune", "lignes", "colonnes"];
const TRAITS: Traits[] = ["tous", "horizontaux", "verticaux", "contour", "entete", "aucun"];
const THEMES: Theme[] = ["sobre", "foret", "ambre", "ardoise", "prune", "encre"];

/**
 * ↑ et ↓ dans un tableau : la case du dessus ou du dessous, à la même place dans son texte autant qu'elle le permet ; au bord
 * du tableau, le bloc d'avant ou d'après. Nos cases sont des blocs de texte, que les flèches de prosemirror-tables ne savent
 * traverser qu'en largeur : en hauteur, le navigateur filait à la case d'à côté.
 */
export const caseVoisine =
  (sens: 1 | -1): Command =>
  (etat, dispatch, vue) => {
    const { selection } = etat;
    if (!(selection instanceof TextSelection) || !selection.empty) return false;
    const $tete = selection.$head;
    const role = $tete.parent.type.spec.tableRole;
    if (role !== "cell" && role !== "header_cell") return false;
    // Une case de plusieurs lignes : on y circule d'abord.
    if (vue && !vue.endOfTextblock(sens > 0 ? "down" : "up")) return false;
    const $case = etat.doc.resolve($tete.before());
    const $voisine = nextCell($case, "vert", sens);
    const voisine = $voisine?.nodeAfter;
    const cible =
      $voisine && voisine
        ? TextSelection.create(etat.doc, $voisine.pos + 1 + Math.min($tete.parentOffset, voisine.content.size))
        : Selection.near(etat.doc.resolve(sens > 0 ? $case.after(-1) : $case.before(-1)), sens);
    dispatch?.(etat.tr.setSelection(cible).scrollIntoView());
    return true;
  };

/** Un tri de mots français, les nombres dans leur ordre. */
const ORDRE = new Intl.Collator("fr", { numeric: true, sensitivity: "base" });

const aLigne = (apparence: Apparence) => apparence.entete === "ligne" || apparence.entete === "ligne,colonne";
const aColonne = (apparence: Apparence) => apparence.entete === "colonne" || apparence.entete === "ligne,colonne";

/**
 * L'apparence après un changement de l'onglet Tableau ; rien pour un changement inconnu ou une valeur refusée.
 * Les en-têtes, la dernière ligne et la colonne cachée en révision s'inversent ; les bandes suivent l'en-tête.
 * @param colonne La colonne du curseur, à partir de 1.
 */
export function changerApparence(apparence: Apparence, changement: string, valeur: string | null, colonne: number): Apparence | null {
  const nouvelle = { ...apparence };
  switch (changement) {
    case "enteteLigne":
    case "enteteColonne": {
      const ligne = changement === "enteteLigne" ? !aLigne(apparence) : aLigne(apparence);
      const premiere = changement === "enteteColonne" ? !aColonne(apparence) : aColonne(apparence);
      nouvelle.entete = ligne && premiere ? "ligne,colonne" : ligne ? "ligne" : premiere ? "colonne" : "aucune";
      if (nouvelle.bandes !== "aucune") nouvelle.bandes = premiere && !ligne ? "colonnes" : "lignes";
      return nouvelle;
    }
    case "total":
      return { ...nouvelle, total: !apparence.total };
    case "reviser":
      return { ...nouvelle, reviser: apparence.reviser === colonne ? null : colonne };
    case "bandes":
      return BANDES.includes(valeur as Bandes) ? { ...nouvelle, bandes: valeur as Bandes } : null;
    case "traits":
      return TRAITS.includes(valeur as Traits) ? { ...nouvelle, traits: valeur as Traits } : null;
    case "theme":
      return THEMES.includes(valeur as Theme) ? { ...nouvelle, theme: valeur as Theme } : null;
    case "largeur":
      return { ...nouvelle, largeur: valeur === "ajustee" ? "ajustee" : "page" };
    default:
      return null;
  }
}

/** Les lignes d'un tableau se trient : aucune case n'en couvre plusieurs. */
export function triable(tableau: Node): boolean {
  let simple = true;
  tableau.descendants((noeud) => {
    if ((noeud.attrs.colspan ?? 1) !== 1 || (noeud.attrs.rowspan ?? 1) !== 1) simple = false;
    return noeud.type.name === "table" || noeud.type.name === "table_row";
  });
  return simple;
}

/**
 * Le tableau aux lignes rangées selon une colonne, de A à Z, ou de Z à A si elles y sont déjà ; rien s'il ne se trie pas.
 * L'en-tête et la dernière ligne qui ressort restent à leur place.
 * @param colonne La colonne, à partir de 0.
 */
export function trierTableau(tableau: Node, colonne: number): Node | null {
  if (!triable(tableau)) return null;
  const apparence = tableau.attrs as Apparence;
  const lignes: Node[] = [];
  tableau.forEach((ligne) => lignes.push(ligne));
  const debut = aLigne(apparence) ? 1 : 0;
  const fin = apparence.total && lignes.length - debut > 1 ? lignes.length - 1 : lignes.length;
  const corps = lignes.slice(debut, fin);
  const cle = (ligne: Node) => ligne.maybeChild(colonne)?.textContent ?? "";
  if (corps.every((ligne) => ORDRE.compare(cle(ligne), cle(corps[0] as Node)) === 0)) return null;
  const montant = [...corps].sort((a, b) => ORDRE.compare(cle(a), cle(b)));
  const range = montant.every((ligne, i) => ligne === corps[i]) ? montant.reverse() : montant;
  return tableau.copy(Fragment.from([...lignes.slice(0, debut), ...range, ...lignes.slice(fin)]));
}
