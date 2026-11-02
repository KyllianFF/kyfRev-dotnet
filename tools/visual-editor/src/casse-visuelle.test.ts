import { EditorState, TextSelection } from "prosemirror-state";
import { describe, expect, it } from "vitest";
import type { Casse } from "./casse";
import { changementDeCasse } from "./casse-visuelle";
import { depuisMarkdown, versMarkdown } from "./markdown";

/** Applique `casse` à la fiche `texte`, sélection de `de` à `a` (tout par défaut). */
function appliquer(
  texte: string,
  casse: Casse | undefined,
  choisir?: (etat: EditorState) => [number, number],
) {
  const origine = depuisMarkdown(texte);
  let etat = EditorState.create({ doc: origine.doc });
  const [de, a] = choisir ? choisir(etat) : [1, etat.doc.content.size - 1];
  etat = etat.apply(etat.tr.setSelection(TextSelection.create(etat.doc, de, a)));
  const tr = changementDeCasse(etat, casse);
  const apres = tr ? etat.apply(tr) : etat;
  return {
    markdown: versMarkdown(apres.doc, origine),
    selection: [apres.selection.from, apres.selection.to],
  };
}

/** Position du texte `cherche` (+ `decalage`) dans le document. */
const position = (etat: EditorState, cherche: string, decalage = 0) => {
  let trouvee = -1;
  etat.doc.descendants((noeud, debut) => {
    const i = noeud.isText ? (noeud.text ?? "").indexOf(cherche) : -1;
    if (trouvee < 0 && i >= 0) trouvee = debut + i + decalage;
  });
  return trouvee;
};

describe("changer la casse dans l'éditeur visuel", () => {
  it("la mise en forme reste, à cheval sur la sélection", () => {
    const { markdown, selection } = appliquer("bonjour le **monde** entier\n", "capitales");
    expect(markdown).toBe("BONJOUR LE **MONDE** ENTIER\n");
    // La sélection reste la même : tout le paragraphe (23 caractères).
    expect(selection).toEqual([1, 24]);
  });

  it("sans sélection : le mot du curseur seulement", () => {
    const { markdown } = appliquer("un mot ici\n", "capitales", (etat) => {
      const p = position(etat, "mot", 1);
      return [p, p];
    });
    expect(markdown).toBe("un MOT ici\n");
  });

  it("chaque paragraphe commence une phrase ; l'adresse d'un lien ne change pas", () => {
    expect(appliquer("UN PREMIER.\n\nUN [SECOND](https://exemple.fr/Page).\n", "phrase").markdown).toBe(
      "Un premier.\n\nUn [second](https://exemple.fr/Page).\n",
    );
  });

  it("Maj+F3 sans casse donnée : la suivante du cycle", () => {
    expect(appliquer("mon titre\n", undefined).markdown).toBe("MON TITRE\n");
  });
});
