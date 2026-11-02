import { EditorState, TextSelection } from "prosemirror-state";
import { describe, expect, it } from "vitest";
import {
  type Alignement,
  alignementCourant,
  alignerSelection,
  basculerTaches,
  insertionDeBloc,
  insertionDeLigne,
  sansTaches,
} from "./blocs";
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

describe("aligner", () => {
  /** Sélection de la position `de` à la position `a`, puis alignement ; rend le Markdown. */
  const aligner = (texte: string, valeur: Alignement, de: number, a = de) => {
    const origine = depuisMarkdown(texte);
    const depart = EditorState.create({ doc: origine.doc });
    const etat = depart.apply(depart.tr.setSelection(TextSelection.create(depart.doc, de, a)));
    const tr = alignerSelection(etat, valeur);
    return tr ? { apres: etat.apply(tr), markdown: versMarkdown(tr.doc, origine) } : null;
  };

  it("enveloppe le paragraphe du curseur, sans toucher aux autres", () => {
    const resultat = aligner("Un  \n\n\nDeux\n\n\nTrois\n", "center", 7);
    expect(resultat?.markdown).toBe('Un  \n\n<div align="center">\n\nDeux\n\n</div>\n\nTrois\n');
    expect(resultat && alignementCourant(resultat.apres)).toBe("center");
  });

  it("change la valeur d'un passage entier", () => {
    const texte = '<div align="center">\n\nUn\n\n</div>\n';
    expect(aligner(texte, "right", 3)?.markdown).toBe('<div align="right">\n\nUn\n\n</div>\n');
  });

  it("à gauche, sort du passage", () => {
    expect(aligner('<div align="center">\n\nUn\n\n</div>\n', "left", 3)?.markdown).toBe("Un\n");
  });

  it("dans un passage de plusieurs blocs, seul le bloc du curseur change", () => {
    const texte = '<div align="center">\n\nUn\n\nDeux\n\nTrois\n\n</div>\n';
    expect(aligner(texte, "right", 7)?.markdown).toBe(
      '<div align="center">\n\nUn\n\n</div>\n\n<div align="right">\n\nDeux\n\n</div>\n\n<div align="center">\n\nTrois\n\n</div>\n',
    );
    expect(aligner(texte, "left", 7)?.markdown).toBe(
      '<div align="center">\n\nUn\n\n</div>\n\nDeux\n\n<div align="center">\n\nTrois\n\n</div>\n',
    );
  });

  it("une sélection qui englobe un passage aligné n'en imbrique pas un autre", () => {
    const texte = 'Avant\n\n<div align="center">\n\nDedans\n\n</div>\n\nAprès\n';
    const fin = depuisMarkdown(texte).doc.content.size - 1;
    expect(aligner(texte, "right", 1, fin)?.markdown).toBe(
      '<div align="right">\n\nAvant\n\nDedans\n\nAprès\n\n</div>\n',
    );
    expect(aligner(texte, "left", 1, fin)?.markdown).toBe("Avant\n\nDedans\n\nAprès\n");
  });

  it("une liste s'aligne entière", () => {
    expect(aligner("- a\n- b\n", "center", 3)?.markdown).toBe('<div align="center">\n\n- a\n- b\n\n</div>\n');
  });

  it("dans un tableau, la colonne s'aligne", () => {
    const texte = "| a | b |\n| --- | --- |\n| c | d |\n";
    expect(aligner(texte, "center", 3)?.markdown).toBe("| a | b |\n| :---: | --- |\n| c | d |\n");
    expect(aligner("| a |\n| :---: |\n| c |\n", "left", 3)?.markdown).toBe("| a |\n| --- |\n| c |\n");
  });

  it("le code ne s'aligne pas", () => {
    expect(aligner("```\ncode\n```\n", "center", 2)).toBeNull();
  });

  it("l'alignement sous le curseur est celui du passage ; à gauche par défaut", () => {
    const etat = EditorState.create({ doc: depuisMarkdown("Un\n").doc });
    expect(alignementCourant(etat)).toBe("left");
  });

  it("justifie aussi", () => {
    expect(aligner("Un\n", "justify", 2)?.markdown).toBe('<div align="justify">\n\nUn\n\n</div>\n');
  });
});

describe("tâches", () => {
  const appliquer = (texte: string, de: number, a = de) => {
    const origine = depuisMarkdown(texte);
    const depart = EditorState.create({ doc: origine.doc });
    const etat = depart.apply(depart.tr.setSelection(TextSelection.create(depart.doc, de, a)));
    const tr = basculerTaches(etat);
    return tr ? versMarkdown(tr.doc, origine) : null;
  };

  it("d'un paragraphe, fait une tâche", () => {
    expect(appliquer("Texte\n", 2)).toBe("- [ ] Texte\n");
  });

  it("d'un point de liste, fait une tâche, et ne change que celui-là", () => {
    expect(appliquer("- a\n- b\n", 3)).toBe("- [ ] a\n- b\n");
  });

  it("de toute la sélection, fait des tâches ; une case déjà cochée le reste", () => {
    expect(appliquer("- [x] a\n- b\n", 3, 9)).toBe("- [x] a\n- [ ] b\n");
  });

  it("d'une tâche, refait un point ordinaire", () => {
    expect(appliquer("- [x] a\n- [ ] b\n", 3)).toBe("- a\n- [ ] b\n");
  });

  it("sans tâche, rien à retirer", () => {
    const etat = EditorState.create({ doc: depuisMarkdown("- a\n").doc });
    expect(sansTaches(etat.apply(etat.tr.setSelection(TextSelection.create(etat.doc, 3))))).toBeNull();
  });
});
