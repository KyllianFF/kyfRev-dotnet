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

describe("format étendu : couleur et taille", () => {
  const marquesDe = (texte: string) => {
    const paragraphe = depuisMarkdown(texte).doc.firstChild as Node;
    const trouvees: string[] = [];
    paragraphe.forEach((enfant) => {
      for (const marque of enfant.marks) {
        trouvees.push(`${enfant.text}:${marque.type.name}=${marque.attrs.valeur ?? ""}`);
      }
    });
    return { paragraphe, trouvees };
  };

  it("lit la couleur et la taille, et les réécrit à l'identique", () => {
    const rouge = '<span style="color:#ff0000">rouge</span>\n';
    expect(marquesDe(rouge).trouvees).toEqual(["rouge:couleur=#ff0000"]);
    expect(reecrire(rouge)).toBe(rouge);
    const grand = 'Un <span style="font-size:1.25em">grand</span> mot\n';
    expect(marquesDe(grand).trouvees).toEqual(["grand:taille=1.25em"]);
    expect(reecrire(grand)).toBe(grand);
  });

  it("garde les deux ensemble, et ce qu'elles entourent", () => {
    const texte = '<span style="color:#00ff00"><span style="font-size:2em">**gras**</span></span>\n';
    expect(reecrire(texte)).toBe(texte);
    stable(texte);
    stable('<span style="color:#00ff00">[lien](https://exemple.org) et `code`</span> suite\n');
  });

  it("une balise fermée rend la couleur qu'elle cachait", () => {
    const { trouvees } = marquesDe(
      '<span style="color:#111111">a<span style="color:#222222">b</span>c</span>\n',
    );
    expect(trouvees).toEqual(["a:couleur=#111111", "b:couleur=#222222", "c:couleur=#111111"]);
  });

  it.each([
    '<span style="color:red">nom de couleur</span>',
    '<span style="color:#ff00">quatre chiffres</span>',
    '<span style="font-size:9em">trop grand</span>',
    '<span style="font-size:0.4em">trop petit</span>',
    '<span style="font-size:12px">autre unité</span>',
    '<span onclick="x">attribut</span>',
    '<span style="color:#ff0000;background:url(x)">deux propriétés</span>',
    '<span style="color:#ff0000">sans fermeture',
    "fermeture seule</span>",
  ])("%s reste du texte", (ecrit) => {
    const paragraphe = depuisMarkdown(`${ecrit}\n`).doc.firstChild as Node;
    expect(paragraphe.textContent).toBe(ecrit);
    paragraphe.forEach((enfant) => expect(enfant.marks).toHaveLength(0));
  });

  it("le texte d'une balise n'est jamais relu comme une balise", () => {
    const doc = schema.nodes.doc.create(null, paragraphe('<span style="color:#ff0000">x</span>'));
    const ecrit = versMarkdown(doc, null);
    expect(ecrit).toBe('\\<span style="color:#ff0000">x\\</span>\n');
    expect((depuisMarkdown(ecrit).doc.firstChild as Node).textContent).toBe('<span style="color:#ff0000">x</span>');
  });
});

describe("format étendu : alignement", () => {
  const aligne = '<div align="center">\n\nAu centre\n\n</div>\n';

  it("lit un passage aligné, et le réécrit à l'identique", () => {
    const { doc } = depuisMarkdown(aligne);
    expect(doc.firstChild?.type.name).toBe("alignement");
    expect(doc.firstChild?.attrs.align).toBe("center");
    expect(doc.firstChild?.textContent).toBe("Au centre");
    expect(reecrire(aligne)).toBe(aligne);
    stable(aligne);
  });

  it("ne réécrit que le bloc modifié", () => {
    const texte = `Un  \n\n\n${aligne}\n\n* Deux\n`;
    const origine = depuisMarkdown(texte);
    expect(versMarkdown(origine.doc, origine)).toBe(texte);
    const doc = remplacer(origine.doc, 0, paragraphe("Modifié"));
    expect(versMarkdown(doc, origine)).toBe(`Modifié\n\n${aligne}\n\n* Deux\n`);
  });

  it("accepte des blocs variés et se relit pareil", () => {
    stable('<div align="right">\n\n# Titre\n\n- a\n- b\n\n> citation\n\n</div>\n');
    stable('<div align="justify">\n\n<div align="center">\n\nDedans\n\n</div>\n\nAprès\n\n</div>\n');
    stable('> <div align="center">\n>\n> Dans une citation\n>\n> </div>\n');
    stable('- point\n\n  <div align="center">\n\n  Dans un point\n\n  </div>\n');
  });

  it("une ligne de fermeture dans un bloc de code ne ferme rien", () => {
    const texte = '<div align="center">\n\n```html\n</div>\n```\n\nSuite\n\n</div>\n';
    const { doc } = depuisMarkdown(texte);
    expect(doc.childCount).toBe(1);
    expect(doc.firstChild?.childCount).toBe(2);
    expect(reecrire(texte)).toBe(texte);
  });

  it("collé sans lignes vides, il est lu aussi", () => {
    const { doc } = depuisMarkdown('<div align="right">\nTexte\n</div>\n');
    expect(doc.firstChild?.type.name).toBe("alignement");
    expect(doc.firstChild?.textContent).toBe("Texte");
  });

  it.each([
    ['sans fermeture', '<div align="center">\n\nTexte\n'],
    ["autre valeur", '<div align="left">\n\nTexte\n\n</div>\n'],
    ["autre attribut", '<div class="x">\n\nTexte\n\n</div>\n'],
    ["collée sous un paragraphe", 'Texte\n<div align="center">\n\nSuite\n\n</div>\n'],
  ])("%s : ce n'est que du texte", (_nom, texte) => {
    const { doc } = depuisMarkdown(texte);
    doc.forEach((bloc) => expect(bloc.type.name).not.toBe("alignement"));
  });

  it("resté vide, il ne s'écrit pas", () => {
    const vide = schema.nodes.alignement.create({ align: "center" }, schema.nodes.paragraph.create());
    expect(versMarkdown(schema.nodes.doc.create(null, [paragraphe("Un"), vide]), null)).toBe("Un\n");
  });
});

describe("format étendu : tâches", () => {
  const taches = (texte: string) => {
    const resultat: (boolean | null)[] = [];
    depuisMarkdown(texte).doc.descendants((noeud) => {
      if (noeud.type === schema.nodes.list_item) resultat.push(noeud.attrs.tache as boolean | null);
    });
    return resultat;
  };

  it("lit les cases, à faire ou faites, et les réécrit à l'identique", () => {
    const texte = "- [ ] à faire\n- [x] fait\n- ordinaire\n";
    expect(taches(texte)).toEqual([false, true, null]);
    expect(reecrire(texte)).toBe(texte);
    stable(texte);
    stable("1. [x] numérotée\n2. [ ] aussi\n");
    stable("- [ ] parent\n  - [x] enfant\n");
  });

  it("retire la case du texte", () => {
    const point = depuisMarkdown("- [X] Texte\n").doc.firstChild?.firstChild;
    expect(point?.textContent).toBe("Texte");
    expect(point?.attrs.tache).toBe(true);
  });

  it("une tâche sans texte reste une tâche", () => {
    expect(taches("- [ ]\n")).toEqual([false]);
    expect(reecrire("- [ ]\n")).toBe("- [ ]\n");
  });

  it.each([
    ["des crochets échappés", "- \\[ \\] texte\n"],
    ["sans espace après", "- [ ]texte\n"],
    ["une autre lettre", "- [y] texte\n"],
    ["un lien", "- [x](https://exemple.org) texte\n"],
    ["en plein texte", "- texte [ ] suite\n"],
  ])("%s : pas une tâche", (_nom, texte) => {
    expect(taches(texte)).toEqual([null]);
  });

  it("des crochets écrits dans un point restent des crochets", () => {
    const point = schema.nodes.list_item.create(null, paragraphe("[ ] littéral"));
    const doc = schema.nodes.doc.create(null, schema.nodes.bullet_list.create(null, point));
    const ecrit = versMarkdown(doc, null);
    expect(ecrit).toBe("- \\[ \\] littéral\n");
    expect(taches(ecrit)).toEqual([null]);
  });
});
