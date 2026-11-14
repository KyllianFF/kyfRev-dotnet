// Où placer un bloc inséré par la barre d'outils (tableau, code, diagramme,
// ligne horizontale) dans l'éditeur visuel, et ce que le ruban fait aux blocs existants :
// les aligner, en faire des tâches.

import { type Node, NodeRange } from "prosemirror-model";
import { wrapInList } from "prosemirror-schema-list";
import { type EditorState, Selection, TextSelection, type Transaction } from "prosemirror-state";
import { isInTable, selectedRect } from "prosemirror-tables";
import { liftTarget } from "prosemirror-transform";
import { schema } from "./schema";

const n = schema.nodes;

/**
 * Pose `bloc` : à la place du paragraphe vide (ou autre bloc de texte vide)
 * où se trouve le curseur, si le bloc peut y aller ; sinon après le bloc de
 * premier niveau du curseur (une liste, un tableau entiers).
 */
function poser(etat: EditorState, bloc: Node): { tr: Transaction; position: number } {
  const { $from } = etat.selection;
  const remplaceLeVide =
    $from.parent.isTextblock &&
    $from.parent.content.size === 0 &&
    $from.depth > 0 &&
    $from.node(-1).canReplaceWith($from.index(-1), $from.indexAfter(-1), bloc.type);
  if (remplaceLeVide) {
    const position = $from.before();
    return { tr: etat.tr.replaceWith(position, $from.after(), bloc), position };
  }
  const position = $from.depth > 0 ? $from.after(1) : etat.selection.to;
  return { tr: etat.tr.insert(position, bloc), position };
}

/**
 * Insère `bloc` (voir `poser`). Le curseur se place `decalage` positions
 * après son début (dedans, ou juste après) ; un paragraphe suit toujours un
 * bloc posé en fin de fiche.
 */
export function insertionDeBloc(etat: EditorState, bloc: Node, decalage: number): Transaction {
  let { tr, position } = poser(etat, bloc);
  const apres = position + bloc.nodeSize;
  if (apres === tr.doc.content.size) tr = tr.insert(apres, schema.nodes.paragraph.create());
  return tr.setSelection(TextSelection.near(tr.doc.resolve(position + decalage))).scrollIntoView();
}

/**
 * Saut de page ou page blanche : après le bloc de premier niveau où est le curseur (ou le bloc choisi), sans le couper.
 * Le curseur va au début de ce qui suit ; rien ne suit : un paragraphe vide, qui ne s'écrit pas tant qu'il le reste.
 */
export function insertionDeSaut(etat: EditorState, blanche: boolean): Transaction {
  const { $from, to } = etat.selection;
  const position = $from.depth > 0 ? $from.after(1) : to;
  const saut = schema.nodes.saut_de_page.create({ blanche });
  let tr = etat.tr.insert(position, saut);
  const apres = position + saut.nodeSize;
  if (apres === tr.doc.content.size) tr = tr.insert(apres, schema.nodes.paragraph.create());
  return tr.setSelection(Selection.near(tr.doc.resolve(apres), 1)).scrollIntoView();
}

/**
 * Ligne horizontale, posée comme un bloc. L'écriture reprend dans un
 * paragraphe vide juste dessous, comme après « --- » puis Espace (créé au
 * besoin : resté vide, il ne s'écrit pas dans le fichier).
 */
export function insertionDeLigne(etat: EditorState): Transaction {
  const ligne = schema.nodes.horizontal_rule.create();
  let { tr, position } = poser(etat, ligne);
  const apres = position + ligne.nodeSize;
  const suivant = tr.doc.nodeAt(apres);
  if (suivant?.type !== schema.nodes.paragraph || suivant.content.size > 0) {
    tr = tr.insert(apres, schema.nodes.paragraph.create());
  }
  return tr.setSelection(TextSelection.create(tr.doc, apres + 1)).scrollIntoView();
}

/**
 * Retire le bloc qui commence à `position` : une image, un bloc de code, la marque d'un saut de page ou d'une page
 * blanche. Rien pour un autre nœud. Une seule transaction : Ctrl+Z le rend d'un coup. Une fiche garde toujours un
 * paragraphe où écrire.
 */
export function suppressionDeBloc(etat: EditorState, position: number): Transaction | null {
  if (!Number.isInteger(position) || position < 0 || position >= etat.doc.content.size) return null;
  const noeud = etat.doc.nodeAt(position);
  if (!noeud) return null;
  const fin = position + noeud.nodeSize;
  // Une image s'en va seule : le paragraphe qui la porte reste, même vide.
  if (noeud.type === n.image) return etat.tr.delete(position, fin).scrollIntoView();
  if (noeud.type !== n.code_block && noeud.type !== n.saut_de_page) return null;
  const seul = etat.doc.resolve(position).parent.childCount === 1;
  const tr = seul ? etat.tr.replaceWith(position, fin, n.paragraph.create()) : etat.tr.delete(position, fin);
  // Une marque était choisie, pas traversée par un curseur : il va au début de ce qui la suivait, sinon à la fin de
  // ce qui la précédait.
  if (noeud.type === n.saut_de_page) tr.setSelection(Selection.near(tr.doc.resolve(Math.min(position, tr.doc.content.size)), 1));
  return tr.scrollIntoView();
}

export type Alignement = "left" | "center" | "right" | "justify";

/** L'alignement sous le curseur. */
export function alignementCourant(etat: EditorState): Alignement {
  const { $from } = etat.selection;
  for (let profondeur = $from.depth; profondeur > 0; profondeur--) {
    const noeud = $from.node(profondeur);
    if (noeud.type === n.table_cell || noeud.type === n.table_header) {
      return (noeud.attrs.align as Alignement | null) ?? "left";
    }
    if (noeud.type === n.alignement) return noeud.attrs.align as Alignement;
  }
  return "left";
}

/** Dans un tableau, Markdown n'aligne que des colonnes entières ; il ne sait pas justifier. */
function alignerColonnes(etat: EditorState, valeur: Alignement): Transaction {
  const { left, right, table, tableStart, map } = selectedRect(etat);
  const align = valeur === "left" || valeur === "justify" ? null : valeur;
  const tr = etat.tr;
  for (let colonne = left; colonne < right; colonne++) {
    const rectangle = { left: colonne, right: colonne + 1, top: 0, bottom: map.height };
    for (const position of map.cellsInRect(rectangle)) {
      tr.setNodeMarkup(tableStart + position, null, { ...table.nodeAt(position)?.attrs, align });
    }
  }
  return tr;
}

const estConteneurDeListe = (noeud: Node) =>
  noeud.type === n.list_item || noeud.type === n.bullet_list || noeud.type === n.ordered_list;

/**
 * Aligne les blocs de la sélection ; « à gauche » les sort de leur passage aligné. Une liste
 * s'aligne entière : son point commence par un paragraphe, rien ne peut s'y glisser.
 */
export function alignerSelection(etat: EditorState, valeur: Alignement): Transaction | null {
  const { $from, $to } = etat.selection;
  if ($from.parent.type.spec.code) return null;
  const memeTableau = $from.sharedDepth($to.pos) >= $from.depth - 2 && isInTable(etat);
  if (memeTableau && ($from.parent.type === n.table_cell || $from.parent.type === n.table_header)) {
    return alignerColonnes(etat, valeur);
  }
  let plage = $from.blockRange($to);
  while (plage && plage.depth > 0 && estConteneurDeListe(plage.parent)) {
    const profondeur = plage.depth;
    plage = new NodeRange(etat.doc.resolve($from.before(profondeur)), etat.doc.resolve($to.after(profondeur)), profondeur - 1);
  }
  if (!plage) return null;

  const { parent, depth, startIndex, endIndex } = plage;
  const tr = etat.tr;
  if (parent.type === n.alignement) {
    if (startIndex === 0 && endIndex === parent.childCount && valeur !== "left") {
      return tr.setNodeMarkup($from.before(depth), null, { align: valeur });
    }
    const cible = liftTarget(plage);
    if (cible === null) return null;
    tr.lift(plage, cible);
  } else {
    // Les passages alignés que la sélection contient se défont : un seul les remplace.
    let position = plage.end;
    for (let i = endIndex - 1; i >= startIndex; i--) {
      const enfant = parent.child(i);
      position -= enfant.nodeSize;
      if (enfant.type === n.alignement) {
        const interieur = new NodeRange(
          tr.doc.resolve(position + 1),
          tr.doc.resolve(position + enfant.nodeSize - 1),
          depth + 1,
        );
        tr.lift(interieur, depth);
      }
    }
  }
  if (valeur === "left") return tr;
  const nouvelle = tr.doc
    .resolve(tr.mapping.map(plage.start, 1))
    .blockRange(tr.doc.resolve(tr.mapping.map(plage.end, -1)));
  return nouvelle ? tr.wrap(nouvelle, [{ type: n.alignement, attrs: { align: valeur } }]) : null;
}

/** Les points de liste dont la sélection touche le premier paragraphe : leur position et eux-mêmes. */
function pointsDeLaSelection(doc: Node, from: number, to: number): { position: number; point: Node }[] {
  const points: { position: number; point: Node }[] = [];
  doc.nodesBetween(from, to, (noeud, position, parent, rang) => {
    if (noeud.type === n.paragraph && parent?.type === n.list_item && rang === 0) {
      points.push({ position: position - 1, point: parent });
    }
    return true;
  });
  return points;
}

/** Les points de la sélection sont-ils des tâches ? */
function sontDesTaches(points: { point: Node }[]): boolean {
  return points.length > 0 && points.every(({ point }) => point.attrs.tache !== null);
}

/** Refait des points ordinaires des tâches de la sélection ; `null` s'il n'y en a pas. */
export function sansTaches(etat: EditorState): Transaction | null {
  const { from, to } = etat.selection;
  const points = pointsDeLaSelection(etat.doc, from, to).filter(({ point }) => point.attrs.tache !== null);
  if (points.length === 0) return null;
  const tr = etat.tr;
  for (const { position, point } of points) tr.setNodeMarkup(position, null, { ...point.attrs, tache: null });
  return tr;
}

/** Fait de la sélection des tâches ; si elle en est déjà, en refait des points ordinaires. */
export function basculerTaches(etat: EditorState): Transaction | null {
  const { from, to } = etat.selection;
  const points = pointsDeLaSelection(etat.doc, from, to);
  if (sontDesTaches(points)) return sansTaches(etat);
  const enListe: Transaction[] = [];
  const tr = points.length > 0 || !wrapInList(n.bullet_list)(etat, (t) => enListe.push(t)) ? etat.tr : (enListe[0] as Transaction);
  const nouveaux = points.length > 0 ? points : pointsDeLaSelection(tr.doc, tr.selection.from, tr.selection.to);
  if (nouveaux.length === 0) return null;
  for (const { position, point } of nouveaux) {
    tr.setNodeMarkup(position, null, { ...point.attrs, tache: point.attrs.tache ?? false });
  }
  return tr;
}
