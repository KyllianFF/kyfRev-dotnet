import { GapCursor } from "prosemirror-gapcursor";
import type { Node, ResolvedPos } from "prosemirror-model";
import { describe, expect, it } from "vitest";
import { depuisMarkdown } from "./markdown";

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
