// Changer la casse dans l'éditeur visuel (règles : src/lib/casse.ts) : la
// sélection, ou à défaut le mot du curseur, comme dans Word.

import type { Mark, Node } from "prosemirror-model";
import { type EditorState, TextSelection, type Transaction } from "prosemirror-state";
import { type Casse, casseSuivante, changerCasse } from "./casse";
import { motAutour } from "./correcteur";
import { schema } from "./schema";

/** Étendues à transformer : celles de la sélection, ou le mot du curseur. */
function etendues(etat: EditorState): { from: number; to: number }[] {
  const { selection } = etat;
  if (!selection.empty) return selection.ranges.map(({ $from, $to }) => ({ from: $from.pos, to: $to.pos }));
  const { $from } = selection;
  if (!$from.parent.isTextblock) return [];
  const texte = $from.parent.textBetween(0, $from.parent.content.size, undefined, "￼");
  const mot = motAutour(texte, $from.parentOffset);
  return mot ? [{ from: $from.start() + mot.debut, to: $from.start() + mot.fin }] : [];
}

/**
 * Transaction qui met la sélection dans la casse `casse` (Maj+F3 : la
 * suivante du cycle). Le texte est transformé d'un bloc (une phrase, un mot
 * peuvent chevaucher plusieurs mises en forme), puis rendu morceau par
 * morceau avec ses marques. `null` si rien ne change.
 */
export function changementDeCasse(etat: EditorState, casse?: Casse): Transaction | null {
  const morceaux: { debut: number; texte: string; marques: readonly Mark[]; rang: number }[] = [];
  let tout = "";
  let blocPrecedent: Node | null = null;
  for (const { from, to } of etendues(etat)) {
    etat.doc.nodesBetween(from, to, (noeud, position, parent) => {
      if (!noeud.isInline) return true;
      // Un autre bloc (paragraphe, cellule) commence une phrase.
      if (blocPrecedent && parent !== blocPrecedent) tout += "\n";
      blocPrecedent = parent;
      if (!noeud.isText) {
        tout += "￼";
        return false;
      }
      const debut = Math.max(from, position);
      const fin = Math.min(to, position + noeud.nodeSize);
      const texte = (noeud.text ?? "").slice(debut - position, fin - position);
      morceaux.push({ debut, texte, marques: noeud.marks, rang: tout.length });
      tout += texte;
      return false;
    });
  }
  if (morceaux.length === 0) return null;
  const transforme = changerCasse(tout, casse ?? casseSuivante(tout));
  const tr = etat.tr;
  for (const { debut, texte, marques, rang } of morceaux) {
    const nouveau = transforme.slice(rang, rang + texte.length);
    if (nouveau !== texte) tr.replaceWith(debut, debut + texte.length, schema.text(nouveau, marques));
  }
  if (!tr.docChanged) return null;
  // Longueurs inchangées : la sélection d'origine reste valable telle quelle.
  const { selection } = etat;
  return tr.setSelection(
    selection instanceof TextSelection
      ? TextSelection.create(tr.doc, selection.anchor, selection.head)
      : selection.map(tr.doc, tr.mapping),
  );
}
