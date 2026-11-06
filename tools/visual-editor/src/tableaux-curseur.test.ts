import { GapCursor } from "prosemirror-gapcursor";
import { EditorState, TextSelection } from "prosemirror-state";
import type { Node, ResolvedPos } from "prosemirror-model";
import { describe, expect, it } from "vitest";
import { depuisMarkdown } from "./markdown";
import { caseVoisine } from "./tableaux";

/** Les deux règles dont le curseur de bloc se sert pour les flèches : présentes à l'exécution, absentes de ses types. */
const regles = GapCursor as unknown as {
  valid($pos: ResolvedPos): boolean;
  findGapCursorFrom($pos: ResolvedPos, dir: number, mustMove?: boolean): ResolvedPos | null;
};

const TABLEAU = "| A | B |\n|---|---|\n| 1 | 2 |\n| 3 | |\n\nAprès.\n";

/** Les positions juste après chaque case, dans sa ligne. */
function apresLesCases(doc: Node): number[] {
  const positions: number[] = [];
  doc.descendants((noeud, position) => {
    if (noeud.type.name === "table_cell" || noeud.type.name === "table_header") positions.push(position + noeud.nodeSize);
  });
  return positions;
}

describe("les flèches dans un tableau", () => {
  it("ne posent jamais le curseur de bloc entre deux cases, ni à droite d'une ligne", () => {
    const { doc } = depuisMarkdown(TABLEAU);
    const positions = apresLesCases(doc);
    expect(positions).toHaveLength(6);
    for (const position of positions) {
      expect(regles.valid(doc.resolve(position))).toBe(false);
      // Depuis la fin d'une case, vers la droite comme vers le bas : rien pour le curseur de bloc, la case voisine prend la main.
      expect(regles.findGapCursorFrom(doc.resolve(position), 1)).toBeNull();
    }
  });
});

/** Le curseur au caractère `decalage` de la case qui contient `texte` (la première trouvée). */
function dansLaCase(doc: Node, texte: string, decalage: number): EditorState {
  let debut = -1;
  doc.descendants((noeud, position) => {
    if (debut < 0 && noeud.type.spec.tableRole && noeud.textContent === texte) debut = position + 1;
  });
  expect(debut).toBeGreaterThan(0);
  const etat = EditorState.create({ doc });
  return etat.apply(etat.tr.setSelection(TextSelection.create(doc, debut + decalage)));
}

/** Où va le curseur après ↑ (-1) ou ↓ (1) : le texte de son bloc, et sa place dans ce texte. */
function apres(etat: EditorState, sens: 1 | -1): [string, number] {
  let suivant = etat;
  expect(caseVoisine(sens)(etat, (tr) => (suivant = etat.apply(tr)))).toBe(true);
  const { $head } = suivant.selection;
  return [$head.parent.textContent, $head.parentOffset];
}

describe("↑ et ↓ dans un tableau", () => {
  const { doc } = depuisMarkdown("| Nom | Prénom |\n|---|---|\n| Arendt | Hannah |\n| Weber | |\n\nAprès.\n");

  it("vont à la case du dessous ou du dessus, à la même place dans son texte autant qu'elle le permet", () => {
    expect(apres(dansLaCase(doc, "Arendt", 4), 1)).toEqual(["Weber", 4]);
    expect(apres(dansLaCase(doc, "Arendt", 6), 1)).toEqual(["Weber", 5]);
    expect(apres(dansLaCase(doc, "Hannah", 3), -1)).toEqual(["Prénom", 3]);
    expect(apres(dansLaCase(doc, "Hannah", 3), 1)).toEqual(["", 0]);
  });

  it("sortent du tableau par le bas, vers le bloc d'après", () => {
    expect(apres(dansLaCase(doc, "Weber", 2), 1)).toEqual(["Après.", 0]);
  });

  it("laissent faire hors d'un tableau, et quand du texte est choisi", () => {
    const etat = EditorState.create({ doc });
    const ailleurs = etat.apply(etat.tr.setSelection(TextSelection.atEnd(doc)));
    expect(caseVoisine(1)(ailleurs)).toBe(false);
    const choisi = dansLaCase(doc, "Arendt", 0);
    const etendu = choisi.apply(choisi.tr.setSelection(TextSelection.create(doc, choisi.selection.from, choisi.selection.from + 3)));
    expect(caseVoisine(1)(etendu)).toBe(false);
  });
});
