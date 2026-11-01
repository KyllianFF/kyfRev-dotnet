// Où placer un bloc inséré par la barre d'outils (tableau, code, diagramme,
// ligne horizontale) dans l'éditeur visuel.

import type { Node } from "prosemirror-model";
import { type EditorState, TextSelection, type Transaction } from "prosemirror-state";
import { schema } from "./schema";

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
