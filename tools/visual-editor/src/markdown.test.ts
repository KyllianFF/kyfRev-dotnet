// Fidélité du passage Markdown ↔ éditeur visuel.

import { splitBlock } from "prosemirror-commands";
import type { Node } from "prosemirror-model";
import { EditorState, Selection } from "prosemirror-state";
import { describe, expect, it } from "vitest";
import { depuisMarkdown, echapper, trancheBrute, trancheCollee, versMarkdown } from "./markdown";
import { schema } from "./schema";

/** Réécrit tout le document, sans rien reprendre du texte d'origine. */
const reecrire = (texte: string) => versMarkdown(depuisMarkdown(texte).doc, null);
/** Relit le Markdown réécrit : le document doit être le même. */
const stable = (texte: string) => {
  const doc = depuisMarkdown(texte).doc;
  const relu = depuisMarkdown(versMarkdown(doc, null)).doc;
  expect(relu.toJSON()).toEqual(doc.toJSON());
};
const paragraphe = (texte: string) => schema.nodes.paragraph.create(null, schema.text(texte));
/** Remplace le bloc de premier niveau `rang` par `bloc`. */
const remplacer = (doc: Node, rang: number, bloc: Node) => {
  let position = 0;
  for (let i = 0; i < rang; i++) position += doc.child(i).nodeSize;
  return doc.replace(
    position,
    position + doc.child(rang).nodeSize,
    schema.nodes.doc.create(null, bloc).slice(0),
  );
};

const FICHE = [
  "Intro avec  deux espaces et une ligne",
  "coupée.  ",
  "",
  "Titre souligné",
  "==============",
  "",
  "* liste en étoiles",
  "* serrée",
  "",
  "",
  "1) ordonnée",
  "2) avec parenthèses",
  "",
  "Texte[^1] et $x^2$ et <u>souligné</u>.",
  "",
  "| a | b |",
  "|:-|-:|",
  "| 1 | 2 |",
  "",
  "[^1]: Une note",
  "    sur deux lignes.",
  "",
  "[site]: https://exemple.fr",
  "",
  "~~~js",
  "const a = 1;",
  "~~~",
  "",
].join("\n");

describe("aller-retour sans modification", () => {
  it("redonne exactement le texte d'origine", () => {
    for (const texte of [
      FICHE,
      "",
      "\n\n",
      "Une ligne",
      "# Titre\n\nTexte sans fin de ligne",
      "\tTabulation\n",
    ]) {
      const origine = depuisMarkdown(texte);
      expect(versMarkdown(origine.doc, origine)).toBe(texte);
    }
  });

  it("ne réécrit que le bloc modifié", () => {
    const origine = depuisMarkdown(FICHE);
    const doc = remplacer(origine.doc, 0, paragraphe("Nouvelle intro"));
    const resultat = versMarkdown(doc, origine);
    expect(resultat).toBe(
      FICHE.replace("Intro avec  deux espaces et une ligne\ncoupée.  ", "Nouvelle intro"),
    );
  });

  it("Entrée à la fin d'un paragraphe ne le réécrit pas (ni ses lignes vides)", () => {
    const texte = "Un\n\n\nDeux  \n";
    const origine = depuisMarkdown(texte);
    let etat = EditorState.create({ doc: origine.doc });
    etat = etat.apply(etat.tr.setSelection(Selection.atEnd(etat.doc)));
    splitBlock(etat, (tr) => {
      etat = etat.apply(tr);
    });
    etat = etat.apply(etat.tr.insertText("Trois"));
    expect(versMarkdown(etat.doc, origine)).toBe("Un\n\n\nDeux  \n\nTrois\n");
  });

  it("un paragraphe vide ajouté en fin de fiche ne s'écrit pas", () => {
    for (const texte of ["```py\nx = 1\n```\n", "| a |\n| --- |\n| 1 |", "Texte\n\n$$\nx\n$$\n"]) {
      const origine = depuisMarkdown(texte);
      const doc = origine.doc.copy(origine.doc.content.addToEnd(schema.nodes.paragraph.create()));
      expect(versMarkdown(doc, origine)).toBe(texte);
    }
  });

  it("les notes et définitions de liens restent du Markdown", () => {
    const { doc } = depuisMarkdown(FICHE);
    const bruts: string[] = [];
    doc.forEach((bloc) => {
      if (bloc.type.name === "brut") bruts.push(bloc.textContent);
    });
    // Lignes voisines (seulement séparées de lignes vides) : un seul bloc.
    expect(bruts).toEqual(["[^1]: Une note\n    sur deux lignes.\n\n[site]: https://exemple.fr"]);
  });

  it("une note en ligne garde son paragraphe en Markdown", () => {
    const { doc } = depuisMarkdown("Texte^[note en ligne].");
    expect(doc.firstChild?.type.name).toBe("brut");
  });
});

describe("réécriture", () => {
  it("marques", () => {
    expect(reecrire("**gras** *ital* <u>soul</u> ~~barré~~ x<sup>2</sup> H<sub>2</sub>O `code`")).toBe(
      "**gras** *ital* <u>soul</u> ~~barré~~ x<sup>2</sup> H<sub>2</sub>O `code`\n",
    );
    expect(reecrire("***les deux***")).toBe("***les deux***\n");
    expect(reecrire("**gras *et ital* gras**")).toBe("**gras *et ital* gras**\n");
    expect(reecrire("`` a`b ``")).toBe("``a`b``\n");
    expect(reecrire("`` `a ``")).toBe("`` `a ``\n");
  });

  it("liens et images, adresses avec espaces", () => {
    expect(reecrire('[fiche](<Ancien Régime.md>) et ![carte](<mon image.png> "Titre")')).toBe(
      '[fiche](<Ancien Régime.md>) et ![carte](<mon image.png> "Titre")\n',
    );
    expect(reecrire("Voir <https://exemple.fr> ou https://exemple.org/a_b")).toBe(
      "Voir <https://exemple.fr> ou https://exemple.org/a_b\n",
    );
    expect(reecrire("![[schéma.png]]")).toBe("![schéma](schéma.png)\n");
  });

  it("titres, citations, listes, code, formules, séparateurs", () => {
    expect(reecrire("Titre\n===")).toBe("# Titre\n");
    expect(reecrire("> a\n>\n> b")).toBe("> a\n>\n> b\n");
    expect(reecrire("- a\n  - b\n- c")).toBe("- a\n  - b\n- c\n");
    expect(reecrire("- a\n\n- b")).toBe("- a\n\n- b\n");
    expect(reecrire("3. trois\n4. quatre")).toBe("3. trois\n4. quatre\n");
    expect(reecrire("```py\nx = 1\n```")).toBe("```py\nx = 1\n```\n");
    expect(reecrire("````\n```\n````")).toBe("````\n```\n````\n");
    expect(reecrire("$$ a = b $$")).toBe("$$\na = b\n$$\n");
    expect(reecrire("***")).toBe("***\n");
  });

  it("tableaux : alignements et barres verticales", () => {
    expect(reecrire("| a | b \\| c | d |\n|:-:|--:|:--|\n| `x\\|y` | 2 |")).toBe(
      "| a | b \\| c | d |\n| :---: | ---: | :--- |\n| `x\\|y` | 2 |  |\n",
    );
  });

  it("documents stables à la relecture", () => {
    for (const texte of [
      FICHE,
      "- [ ] pas une case\n- **gras** suivi\n  de suite",
      "1. a\n\n   para\n\n   ```\n   code\n   ```\n2. b",
      "> - cité\n> - liste\n>\n> > imbriqué",
      "a  \nb\\\nc",
      "*a **b*** c",
      "Texte avec \\* et \\_ et \\` échappés",
      "[![img](a.png)](https://exemple.fr)",
    ]) {
      stable(texte);
    }
  });
});

describe("le texte saisi n'est jamais lu comme une syntaxe", () => {
  const relire = (texte: string) => {
    const doc = schema.nodes.doc.create(null, paragraphe(texte));
    const ecrit = versMarkdown(doc, null);
    const relu = depuisMarkdown(ecrit).doc;
    expect(relu.childCount, ecrit).toBe(1);
    expect(relu.firstChild?.type.name, ecrit).toBe("paragraph");
    expect(relu.firstChild?.textContent, ecrit).toBe(texte);
    expect(relu.firstChild?.firstChild?.marks ?? [], ecrit).toEqual([]);
  };

  it("caractères spéciaux", () => {
    for (const texte of [
      "2 * 3 * 4",
      "a_b_c et _x_",
      "~~pas barré~~",
      "5 $ et 6 $ ou $x$",
      "<u>littéral</u> et <sup>x</sup>",
      "[1] et [lien](adresse)",
      "&amp; et &#35;",
      "\\ et \\*",
      "`pas du code`",
      "<https://exemple.fr>",
      "![pas une image](x.png)",
      "Fin #",
    ]) {
      relire(texte);
    }
  });

  it("débuts de ligne", () => {
    for (const texte of [
      "# pas un titre",
      "- pas une liste",
      "+ non plus",
      "1. pas numéroté",
      "2) non plus",
      "> pas une citation",
      "---",
      "===",
      "| a | b |",
      "Première\n# seconde ligne",
      "Première\n---",
    ]) {
      relire(texte);
    }
  });

  it("un retrait en début de ligne ne fait pas un bloc de code", () => {
    const doc = schema.nodes.doc.create(null, paragraphe("    retrait"));
    expect(depuisMarkdown(versMarkdown(doc, null)).doc.firstChild?.type.name).toBe("paragraph");
  });

  it("texte et mises en forme au hasard : relus à l'identique", () => {
    // Générateur pseudo-aléatoire reproductible.
    let graine = 42;
    const hasard = (max: number) => {
      graine = (graine * 1103515245 + 12345) % 2 ** 31;
      return graine % max;
    };
    const caracteres = "ab é1 *_~`$[]<>&#|\\!-+.=:()^\n";
    const types = ["strong", "em", "underline", "strike", "sup", "sub", "code", "link"] as const;
    for (let essai = 0; essai < 3000; essai++) {
      const morceaux: Node[] = [];
      for (let k = 0; k < 1 + hasard(4); k++) {
        let texte = "";
        for (let c = 0; c < 1 + hasard(6); c++) texte += caracteres[hasard(caracteres.length)];
        let marques = schema.marks.strong.create().removeFromSet([]);
        for (const type of types) {
          if (hasard(4) !== 0) continue;
          const marque =
            type === "link"
              ? schema.marks.link.create({ href: hasard(2) ? "fiche.md" : "a b.md" })
              : schema.marks[type].create();
          marques = marque.addToSet(marques);
        }
        if (hasard(8) === 0) morceaux.push(schema.nodes.hard_break.create(null, null, marques));
        morceaux.push(schema.text(texte, marques));
      }
      const doc = schema.nodes.doc.create(null, schema.nodes.paragraph.create(null, morceaux));
      const ecrit = versMarkdown(doc, null);
      const relu = depuisMarkdown(ecrit).doc;
      // Comparaison sur le sens : espaces de bord et retours à la ligne en blanc ne comptent pas.
      const sens = (d: Node) => {
        const parties: string[] = [];
        d.descendants((noeud) => {
          if (noeud.isText) {
            const noms = noeud.marks
              .map((marque) => marque.type.name)
              .sort()
              .join("+");
            parties.push(
              ...[...(noeud.text ?? "")].filter((c) => c.trim() !== "").map((c) => `${c}:${noms}`),
            );
          }
          return true;
        });
        return parties;
      };
      expect(sens(relu), JSON.stringify(ecrit)).toEqual(sens(doc));
    }
  });

  it("échappements minimaux dans les mots", () => {
    expect(echapper("snake_case_nom")).toBe("snake_case_nom");
    expect(echapper("a|b", true)).toBe("a\\|b");
  });
});

describe("texte collé", () => {
  /** Types des blocs collés (premier niveau), et leur texte. */
  const blocs = (tranche: ReturnType<typeof trancheCollee>) => {
    const resultat: string[] = [];
    tranche.content.forEach((noeud) => {
      resultat.push(`${noeud.type.name}:${noeud.textContent}`);
    });
    return resultat;
  };

  it("un texte en retrait (PDF, courriel) reste du texte, pas un bloc de code", () => {
    const tranche = trancheCollee("Premier paragraphe.\n\n    Retrait de quatre espaces.\n\n\tTabulation.");
    expect(blocs(tranche)).toEqual([
      "paragraph:Premier paragraphe.",
      "paragraph:Retrait de quatre espaces.",
      "paragraph:Tabulation.",
    ]);
    // Fins de ligne Windows comprises.
    expect(blocs(trancheCollee("Un\r\n\r\n    deux"))).toEqual(["paragraph:Un", "paragraph:deux"]);
  });

  it("le Markdown reste reconnu : gras, listes imbriquées, blocs « ``` »", () => {
    const gras = trancheCollee("du **gras**");
    expect(gras.content.firstChild?.lastChild?.marks.map((m) => m.type.name)).toEqual(["strong"]);
    const liste = trancheCollee("- un\n    - deux");
    expect(liste.content.firstChild?.type.name).toBe("bullet_list");
    expect(liste.content.firstChild?.firstChild?.lastChild?.type.name).toBe("bullet_list");
    expect(blocs(trancheCollee("```js\nx = 1\n```"))).toEqual(["code_block:x = 1"]);
  });

  it("Ctrl+Maj+V : le texte tel quel, une ligne par paragraphe", () => {
    const tranche = trancheBrute("**pas gras**\n\n    # pas un titre");
    expect(blocs(tranche)).toEqual(["paragraph:**pas gras**", "paragraph:    # pas un titre"]);
    tranche.content.forEach((noeud) => {
      noeud.forEach((texte) => {
        expect(texte.marks).toEqual([]);
      });
    });
  });
});
