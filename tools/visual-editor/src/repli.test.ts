import { history, redo, undo } from "prosemirror-history";
import { type Command, EditorState, NodeSelection, TextSelection, type Transaction } from "prosemirror-state";
import { describe, expect, it } from "vitest";
import { depuisMarkdown, versMarkdown } from "./markdown";
import {
  bascule,
  blocsCaches,
  deplierIci,
  gesteDuClavier,
  libelle,
  memeRepli,
  repli,
  replierIci,
  sectionA,
  sectionDuCurseur,
  sections,
  titresReplies,
  type Touche,
} from "./repli";
import { schema } from "./schema";

const FICHE = `# A

un

## B

deux

### C

trois

## D

quatre

# E

cinq

six
`;

/** La fiche `texte`, avec le repli et l'historique. */
function fiche(texte = FICHE): EditorState {
  return EditorState.create({ doc: depuisMarkdown(texte).doc, plugins: [history(), repli()] });
}

/** Où commence le bloc de premier niveau qui dit `texte`. */
function bloc(etat: EditorState, texte: string): number {
  let trouve = -1;
  etat.doc.forEach((noeud, position) => {
    if (trouve < 0 && noeud.textContent === texte) trouve = position;
  });
  expect(trouve).toBeGreaterThanOrEqual(0);
  return trouve;
}

/** Les blocs de premier niveau entre deux positions, par leur texte. */
function textes(etat: EditorState, debut: number, fin: number): string[] {
  const dits: string[] = [];
  etat.doc.forEach((noeud, position) => {
    if (position >= debut && position < fin) dits.push(noeud.textContent);
  });
  return dits;
}

/** Ce que gouverne le titre `titre`. */
function gouverne(etat: EditorState, titre: string): string[] {
  const section = sections(etat.doc).find((une) => une.texte === titre);
  expect(section).toBeDefined();
  return section ? textes(etat, section.debut, section.fin) : [];
}

/** Les blocs cachés, par leur texte. */
const caches = (etat: EditorState) => blocsCaches(etat.doc, titresReplies(etat)).map(({ position }) => etat.doc.nodeAt(position)?.textContent);

/** Les titres repliés, par leur texte. */
const replies = (etat: EditorState) => titresReplies(etat).map((position) => etat.doc.nodeAt(position)?.textContent);

function appliquer(etat: EditorState, tr: Transaction | null): EditorState {
  expect(tr).not.toBeNull();
  return tr ? etat.apply(tr) : etat;
}

const replier = (etat: EditorState, titre: string) => appliquer(etat, bascule(etat, bloc(etat, titre), true));

/** Le curseur dans le bloc qui dit `texte`, à `decalage` de son début. */
const curseur = (etat: EditorState, texte: string, decalage = 0) =>
  etat.apply(etat.tr.setSelection(TextSelection.create(etat.doc, bloc(etat, texte) + 1 + decalage)));

function executer(etat: EditorState, commande: Command): { etat: EditorState; faite: boolean } {
  let apres = etat;
  const faite = commande(etat, (tr) => {
    apres = etat.apply(tr);
  });
  return { etat: apres, faite };
}

describe("l'étendue d'une section", () => {
  it("va jusqu'au prochain titre de niveau égal ou supérieur : les sous-titres et leur contenu en font partie", () => {
    const etat = fiche();
    expect(gouverne(etat, "A")).toEqual(["un", "B", "deux", "C", "trois", "D", "quatre"]);
    expect(gouverne(etat, "B")).toEqual(["deux", "C", "trois"]);
    expect(gouverne(etat, "C")).toEqual(["trois"]);
    expect(gouverne(etat, "D")).toEqual(["quatre"]);
  });

  it("va jusqu'à la fin de la fiche pour la dernière section", () => {
    const etat = fiche();
    expect(gouverne(etat, "E")).toEqual(["cinq", "six"]);
    expect(sections(etat.doc).at(-1)?.fin).toBe(etat.doc.content.size);
    // Un sous-titre en dernier : sa section et celle qui la contient finissent ensemble.
    const autre = fiche("# A\n\nun\n\n## B\n\ndeux\n");
    expect(gouverne(autre, "A")).toEqual(["un", "B", "deux"]);
    expect(gouverne(autre, "B")).toEqual(["deux"]);
  });

  it("est vide pour un titre sans rien sous lui : pas de flèche, rien à replier", () => {
    const etat = fiche("# A\n\n## B\n\n## C\n\ntexte\n\n# D\n");
    const vides = sections(etat.doc).map((section) => [section.texte, section.vide]);
    expect(vides).toEqual([["A", false], ["B", true], ["C", false], ["D", true]]);
    expect(sectionA(etat.doc, bloc(etat, "B"))).toBeNull();
    expect(bascule(etat, bloc(etat, "B"))).toBeNull();
    expect(bascule(etat, bloc(etat, "D"))).toBeNull();
    expect(bascule(etat, bloc(etat, "texte"))).toBeNull();
  });

  it("est vide quand il n'y a sous le titre que des paragraphes vides", () => {
    const { doc } = depuisMarkdown("# A\n");
    const vide = schema.nodes.paragraph.create();
    const etat = EditorState.create({ doc: doc.copy(doc.content.addToEnd(vide).addToEnd(vide)), plugins: [repli()] });
    expect(sections(etat.doc)[0]?.vide).toBe(true);
    expect(bascule(etat, 0)).toBeNull();
  });

  it("ne compte que les titres de premier niveau du document ; un titre centré, seul dans son passage, en est un", () => {
    const etat = fiche('# A\n\nun\n\n> ## Cité\n>\n> dedans\n\n- point\n\n<div align="center">\n\n## Centré\n\n</div>\n\ndeux\n');
    expect(sections(etat.doc).map((section) => section.texte)).toEqual(["A", "Centré"]);
    // La citation et son titre sont un bloc comme un autre de la section ; le titre centré en ouvre une.
    expect(gouverne(etat, "A")).toEqual(["un", "Citédedans", "point", "Centré", "deux"]);
    expect(gouverne(etat, "Centré")).toEqual(["deux"]);
    const centre = sections(etat.doc)[1];
    expect(centre && etat.doc.nodeAt(centre.position)?.type.name).toBe("alignement");
    expect(centre && etat.doc.nodeAt(centre.titre)?.type.name).toBe("heading");
    expect(centre && etat.doc.resolve(centre.finDuTitre).parent.textContent).toBe("Centré");
  });
});

describe("replier et déplier", () => {
  it("cache tout ce que le titre gouverne, sans toucher au texte ni au Markdown", () => {
    const depart = fiche();
    const etat = replier(depart, "B");
    expect(replies(etat)).toEqual(["B"]);
    expect(caches(etat)).toEqual(["deux", "C", "trois"]);
    expect(etat.doc).toBe(depart.doc);
    expect(versMarkdown(etat.doc, depuisMarkdown(FICHE))).toBe(FICHE);
    expect(memeRepli(depart, etat)).toBe(false);
    const rouvert = appliquer(etat, bascule(etat, bloc(etat, "B")));
    expect(replies(rouvert)).toEqual([]);
    expect(caches(rouvert)).toEqual([]);
  });

  it("ne fait rien pour une section déjà comme on la veut", () => {
    const etat = replier(fiche(), "B");
    expect(bascule(etat, bloc(etat, "B"), true)).toBeNull();
    expect(bascule(etat, bloc(etat, "D"), false)).toBeNull();
  });

  it("tient deux sections repliées à la fois, chaque bloc caché une seule fois", () => {
    let etat = replier(fiche(), "B");
    etat = replier(etat, "E");
    expect(replies(etat)).toEqual(["B", "E"]);
    expect(caches(etat)).toEqual(["deux", "C", "trois", "cinq", "six"]);
    // Une section repliée dans une autre : dépliée, celle du dehors laisse celle du dedans comme elle était.
    etat = replier(etat, "A");
    expect(caches(etat)).toEqual(["un", "B", "deux", "C", "trois", "D", "quatre", "cinq", "six"]);
    etat = appliquer(etat, bascule(etat, bloc(etat, "A"), false));
    expect(replies(etat)).toEqual(["B", "E"]);
    expect(caches(etat)).toEqual(["deux", "C", "trois", "cinq", "six"]);
  });

  it("repart de zéro pour une autre fiche", () => {
    const etat = replier(fiche(), "B");
    expect(titresReplies(fiche())).toEqual([]);
    expect(titresReplies(EditorState.create({ doc: etat.doc }))).toEqual([]);
  });

  it("emmène à la fin du titre un curseur qui se retrouverait caché, et laisse les autres où ils sont", () => {
    let etat = curseur(fiche(), "trois", 2);
    etat = replier(etat, "B");
    expect(replies(etat)).toEqual(["B"]);
    expect(etat.selection.$head.parent.textContent).toBe("B");
    expect(etat.selection.$head.parentOffset).toBe(1);
    etat = curseur(fiche(), "quatre", 3);
    const avant = etat.selection;
    etat = replier(etat, "B");
    expect(etat.selection.eq(avant)).toBe(true);
  });
});

describe("le repli suit le texte", () => {
  it("reste sur son titre quand on écrit avant lui, dans lui ou après lui", () => {
    let etat = replier(fiche(), "B");
    etat = etat.apply(etat.tr.insertText("Du texte avant. ", bloc(etat, "un") + 1));
    expect(replies(etat)).toEqual(["B"]);
    expect(caches(etat)).toEqual(["deux", "C", "trois"]);
    // Un bloc entier juste avant le titre.
    etat = etat.apply(etat.tr.insert(bloc(etat, "B"), schema.nodes.paragraph.create(null, schema.text("nouveau"))));
    expect(replies(etat)).toEqual(["B"]);
    // Dans le titre lui-même : il reste replié sous son nouveau nom.
    etat = etat.apply(etat.tr.insertText("is", bloc(etat, "B") + 2));
    expect(replies(etat)).toEqual(["Bis"]);
    etat = etat.apply(etat.tr.insertText("Et après. ", bloc(etat, "quatre") + 1));
    expect(replies(etat)).toEqual(["Bis"]);
    expect(caches(etat)).toEqual(["deux", "C", "trois"]);
  });

  it("survit à annuler et rétablir", () => {
    let etat = replier(fiche(), "B");
    etat = curseur(etat, "un", 2);
    etat = etat.apply(etat.tr.insertText("Du texte avant."));
    ({ etat } = executer(etat, undo));
    expect(etat.doc.firstChild?.textContent).toBe("A");
    expect(replies(etat)).toEqual(["B"]);
    expect(caches(etat)).toEqual(["deux", "C", "trois"]);
    ({ etat } = executer(etat, redo));
    expect(replies(etat)).toEqual(["B"]);
    expect(caches(etat)).toEqual(["deux", "C", "trois"]);
  });

  it("n'entre pas dans l'historique : annuler ne déplie ni ne replie", () => {
    let etat = curseur(fiche(), "un", 2);
    etat = etat.apply(etat.tr.insertText("x"));
    etat = replier(etat, "D");
    ({ etat } = executer(etat, undo));
    expect(etat.doc.child(1).textContent).toBe("un");
    expect(replies(etat)).toEqual(["D"]);
  });
});

describe("on n'écrit jamais dans du texte caché", () => {
  it("déplie la section quand la sélection entre dans un de ses blocs", () => {
    let etat = replier(fiche(), "B");
    etat = curseur(etat, "trois", 1);
    expect(replies(etat)).toEqual([]);
    expect(caches(etat)).toEqual([]);
  });

  it("déplie toutes celles qui cachent la sélection, et seulement elles", () => {
    let etat = replier(fiche(), "C");
    etat = replier(etat, "E");
    etat = replier(etat, "A");
    expect(replies(etat)).toEqual(["A", "C", "E"]);
    // Le titre C est caché par A, sans l'être par sa propre section : le Plan y mène, A se déplie, C reste replié.
    etat = curseur(etat, "C");
    expect(replies(etat)).toEqual(["C", "E"]);
    etat = curseur(etat, "trois");
    expect(replies(etat)).toEqual(["E"]);
  });

  it("laisse repliée une section que la sélection enjambe, ou dont le curseur est dans le titre", () => {
    let etat = replier(fiche(), "B");
    etat = curseur(etat, "B", 1);
    expect(replies(etat)).toEqual(["B"]);
    etat = etat.apply(etat.tr.setSelection(TextSelection.create(etat.doc, bloc(etat, "un") + 1, bloc(etat, "quatre") + 2)));
    expect(replies(etat)).toEqual(["B"]);
  });

  it("déplie pour un bloc caché que l'on choisit", () => {
    let etat = replier(fiche("# A\n\nun\n\n---\n\ndeux\n\n# B\n\ntrois\n"), "A");
    let trait = -1;
    etat.doc.forEach((noeud, position) => {
      if (noeud.type === schema.nodes.horizontal_rule) trait = position;
    });
    etat = etat.apply(etat.tr.setSelection(NodeSelection.create(etat.doc, trait)));
    expect(replies(etat)).toEqual([]);
  });

  it("déplie quand Entrée, à la fin du titre replié, ouvre un paragraphe sous lui", () => {
    let etat = replier(fiche(), "B");
    const fin = bloc(etat, "B") + 2;
    const tr = etat.tr.split(fin, 1, [{ type: schema.nodes.paragraph }]);
    etat = etat.apply(tr.setSelection(TextSelection.create(tr.doc, fin + 2)));
    expect(etat.selection.$head.parent.type).toBe(schema.nodes.paragraph);
    expect(replies(etat)).toEqual([]);
  });

  it("déplie quand un changement touche ce qui est caché, même sans y mettre le curseur", () => {
    let etat = replier(fiche(), "B");
    etat = curseur(etat, "quatre", 1);
    etat = etat.apply(etat.tr.insertText("x", bloc(etat, "trois") + 2));
    expect(replies(etat)).toEqual([]);
    // Annuler une frappe faite dans la section avant de la replier : elle se déplie, on voit ce qui change.
    etat = curseur(fiche(), "deux", 2);
    etat = etat.apply(etat.tr.insertText("xyz"));
    etat = curseur(etat, "quatre", 1);
    etat = replier(etat, "B");
    expect(caches(etat)).toEqual(["dexyzux", "C", "trois"]);
    ({ etat } = executer(etat, undo));
    expect(etat.doc.textContent).toContain("deux");
    expect(replies(etat)).toEqual([]);
  });
});

describe("le titre replié disparaît ou change", () => {
  it("déplie son contenu quand on le supprime : rien ne reste caché sans flèche", () => {
    let etat = replier(fiche(), "B");
    const debut = bloc(etat, "B");
    etat = etat.apply(etat.tr.delete(debut, debut + (etat.doc.nodeAt(debut)?.nodeSize ?? 0)));
    expect(titresReplies(etat)).toEqual([]);
    expect(caches(etat)).toEqual([]);
    // Le titre suivant, arrivé à sa place, n'hérite pas du repli.
    etat = replier(fiche("# A\n\nun\n\n# B\n\ndeux\n"), "A");
    etat = etat.apply(etat.tr.delete(0, bloc(etat, "B")));
    expect(etat.doc.firstChild?.textContent).toBe("B");
    expect(titresReplies(etat)).toEqual([]);
  });

  it("déplie quand il devient un paragraphe, change de niveau ou entre dans une citation", () => {
    for (const changer of [
      (etat: EditorState, position: number) => etat.tr.setNodeMarkup(position, schema.nodes.paragraph),
      (etat: EditorState, position: number) => etat.tr.setNodeMarkup(position, null, { level: 3 }),
      (etat: EditorState, position: number) => {
        const $debut = etat.doc.resolve(position + 1);
        const plage = $debut.blockRange();
        return plage ? etat.tr.wrap(plage, [{ type: schema.nodes.blockquote }]) : etat.tr;
      },
    ]) {
      let etat = replier(fiche(), "B");
      etat = curseur(etat, "quatre");
      const tr = changer(etat, bloc(etat, "B"));
      expect(tr.docChanged).toBe(true);
      etat = etat.apply(tr);
      expect(titresReplies(etat)).toEqual([]);
    }
  });

  it("ne cache rien d'autre quand il se fond dans le titre suivant", () => {
    // « B » replié, sélection du milieu de « B » jusque dans « D », puis Suppr : le titre qui reste gouverne « quatre ».
    let etat = replier(fiche("# A\n\n## Bon\n\ndeux\n\n## Dur\n\nquatre\n"), "Bon");
    etat = etat.apply(etat.tr.delete(bloc(etat, "Bon") + 2, bloc(etat, "Dur") + 2));
    expect(etat.doc.child(1).textContent).toBe("Bur");
    expect(titresReplies(etat)).toEqual([]);
    expect(caches(etat)).toEqual([]);
  });

  it("déplie quand il ne gouverne plus rien", () => {
    let etat = replier(fiche("# A\n\nun\n\n# B\n\ndeux\n"), "A");
    etat = curseur(etat, "deux");
    const debut = bloc(etat, "un");
    etat = etat.apply(etat.tr.delete(debut, bloc(etat, "B")));
    expect(titresReplies(etat)).toEqual([]);
  });
});

describe("au clavier, comme dans Word", () => {
  const touche = (key: string, code: string, autres: Partial<Touche> = {}): Touche => ({
    key,
    code,
    altKey: true,
    shiftKey: true,
    ctrlKey: false,
    metaKey: false,
    ...autres,
  });

  it("Alt+Maj+« - » replie, Alt+Maj+« + » ou « = » déplie", () => {
    expect(gesteDuClavier(touche("-", "NumpadSubtract"))).toBe("replier");
    expect(gesteDuClavier(touche("_", "Minus"))).toBe("replier");
    expect(gesteDuClavier(touche("+", "NumpadAdd"))).toBe("deplier");
    expect(gesteDuClavier(touche("+", "Equal"))).toBe("deplier");
    expect(gesteDuClavier(touche("=", "Equal"))).toBe("deplier");
  });

  it("reconnaît la touche du « - » d'un clavier français, qui dit « 6 » avec Maj, quand le navigateur donne sa carte", () => {
    expect(gesteDuClavier(touche("6", "Digit6"))).toBeNull();
    expect(gesteDuClavier(touche("6", "Digit6"), new Set(["Digit6"]))).toBe("replier");
    expect(gesteDuClavier(touche("°", "Minus"), new Set(["Digit6"]))).toBeNull();
  });

  it("ne prend rien sans Alt et Maj, ni avec Ctrl (AltGr) ou la touche Windows", () => {
    expect(gesteDuClavier(touche("-", "Minus", { shiftKey: false }))).toBeNull();
    expect(gesteDuClavier(touche("-", "Minus", { altKey: false }))).toBeNull();
    expect(gesteDuClavier(touche("-", "Minus", { ctrlKey: true }))).toBeNull();
    expect(gesteDuClavier(touche("+", "Equal", { metaKey: true }))).toBeNull();
    expect(gesteDuClavier(touche("a", "KeyQ"))).toBeNull();
  });

  it("replie la section du titre où est le curseur, puis la déplie", () => {
    let etat = curseur(fiche(), "B", 1);
    expect(sectionDuCurseur(etat)?.texte).toBe("B");
    let faite: boolean;
    ({ etat, faite } = executer(etat, replierIci));
    expect(faite).toBe(true);
    expect(replies(etat)).toEqual(["B"]);
    ({ etat, faite } = executer(etat, deplierIci));
    expect(faite).toBe(true);
    expect(replies(etat)).toEqual([]);
  });

  it("depuis le texte, replie la section du titre qui le gouverne, et le curseur va dans ce titre", () => {
    let etat = curseur(fiche(), "trois", 2);
    expect(sectionDuCurseur(etat)?.texte).toBe("C");
    ({ etat } = executer(etat, replierIci));
    expect(replies(etat)).toEqual(["C"]);
    expect(etat.selection.$head.parent.textContent).toBe("C");
  });

  it("laisse passer la touche quand aucun titre ne gouverne le curseur, ou qu'il n'a rien à replier", () => {
    let etat = curseur(fiche("avant\n\n# A\n\nun\n"), "avant");
    expect(executer(etat, replierIci).faite).toBe(false);
    etat = curseur(fiche("# A\n\n# B\n\nun\n"), "A");
    expect(executer(etat, replierIci).faite).toBe(false);
    expect(executer(etat, deplierIci).faite).toBe(false);
  });
});

describe("le nom de la flèche", () => {
  it("dit ce que fera le clic, et sur quelle section", () => {
    expect(libelle("Les étapes", false)).toBe("Replier la section « Les étapes »");
    expect(libelle("Les étapes", true)).toBe("Déplier la section « Les étapes »");
    expect(libelle("  ", false)).toBe("Replier la section");
  });
});
