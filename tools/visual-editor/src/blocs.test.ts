import { EditorState, TextSelection } from "prosemirror-state";
import { describe, expect, it } from "vitest";
import { insertionDeBloc, insertionDeLigne } from "./blocs";
import { depuisMarkdown, versMarkdown } from "./markdown";
import { schema } from "./schema";

const n = schema.nodes;

/** Fiche `texte` (relue), curseur à la position que rend `ou`. */
function fiche(texte: string, ou: (etat: EditorState) => number) {
  const origine = depuisMarkdown(texte);
  const etat = EditorState.create({ doc: origine.doc });
  return { origine, etat: etat.apply(etat.tr.setSelection(TextSelection.create(etat.doc, ou(etat)))) };
}

/** Première position (dans un nœud de type `type`, vide si `vide`). */
function dans(etat: EditorState, type: (typeof n)[string], vide = false): number {
  let trouvee = -1;
  etat.doc.descendants((noeud, position) => {
    if (trouvee < 0 && noeud.type === type && (!vide || noeud.content.size === 0)) trouvee = position + 1;
  });
  return trouvee;
}

describe("ligne horizontale (éditeur visuel)", () => {
  it("sous le paragraphe du curseur ; l'écriture reprend dans un paragraphe neuf, dessous", () => {
    const { origine, etat } = fiche("Un  \n\n\n> Deux\n", () => 2);
    let apres = etat.apply(insertionDeLigne(etat));
    expect(apres.doc.child(1).type).toBe(n.horizontal_rule);
    expect(apres.selection.$from.parent).toBe(apres.doc.child(2));
    expect(apres.doc.child(2).content.size).toBe(0);
    // Resté vide, le paragraphe ne s'écrit pas ; le reste de la fiche est intact.
    expect(versMarkdown(apres.doc, origine)).toBe("Un  \n\n---\n\n> Deux\n");
    apres = apres.apply(apres.tr.insertText("Trois"));
    expect(versMarkdown(apres.doc, origine)).toBe("Un  \n\n---\n\nTrois\n\n> Deux\n");
  });

  it("à la place d'un paragraphe vide, comme « --- » puis Espace", () => {
    const origine = depuisMarkdown("Un\n");
    const doc = origine.doc.copy(origine.doc.content.addToEnd(n.paragraph.create()));
    let etat = EditorState.create({ doc });
    etat = etat.apply(etat.tr.setSelection(TextSelection.atEnd(etat.doc)));
    const apres = etat.apply(insertionDeLigne(etat));
    expect(apres.doc.childCount).toBe(3);
    expect(apres.doc.child(1).type).toBe(n.horizontal_rule);
    expect(apres.selection.$from.parent).toBe(apres.doc.lastChild);
    expect(versMarkdown(apres.doc, origine)).toBe("Un\n\n---\n");
  });

  it("dans une liste ou un tableau : sous la liste ou le tableau entiers", () => {
    // Élément de liste vide : son premier paragraphe ne peut pas devenir un trait.
    let { etat } = fiche("- un\n-\n", (e) => dans(e, n.paragraph, true));
    let apres = etat.apply(insertionDeLigne(etat));
    expect(apres.doc.child(0).type).toBe(n.bullet_list);
    expect(apres.doc.child(1).type).toBe(n.horizontal_rule);

    // Cellule de tableau vide.
    ({ etat } = fiche("| a | b |\n| --- | --- |\n|  | x |\n", (e) => dans(e, n.table_cell, true)));
    apres = etat.apply(insertionDeLigne(etat));
    expect(apres.doc.child(0).type).toBe(n.table);
    expect(apres.doc.child(0).textContent).toBe("abx");
    expect(apres.doc.child(1).type).toBe(n.horizontal_rule);
  });
});

describe("autres blocs de la barre d'outils", () => {
  it("bloc de code à la place d'un paragraphe vide, curseur dedans", () => {
    const origine = depuisMarkdown("Un\n");
    const doc = origine.doc.copy(origine.doc.content.addToEnd(n.paragraph.create()));
    let etat = EditorState.create({ doc });
    etat = etat.apply(etat.tr.setSelection(TextSelection.atEnd(etat.doc)));
    const apres = etat.apply(insertionDeBloc(etat, n.code_block.create(), 1));
    expect(apres.doc.child(1).type).toBe(n.code_block);
    expect(apres.selection.$from.parent.type).toBe(n.code_block);
    expect(apres.doc.lastChild?.type).toBe(n.paragraph);
  });
});
