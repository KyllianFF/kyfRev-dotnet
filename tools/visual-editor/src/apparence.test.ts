// L'apparence d'un tableau : sa ligne, sa fidélité dans le Markdown, l'onglet Tableau.

import { describe, expect, it } from "vitest";
import { APPARENCE, type Apparence, apparenceDuDom, attributsApparence, ecrireApparence, lireApparence } from "./apparence";
import { depuisMarkdown, versMarkdown } from "./markdown";
import { schema } from "./schema";
import { changerApparence, triable, trierTableau } from "./tableaux";

const TABLEAU = ["| Notion | Auteur |", "| --- | --- |", "| Pouvoir | Arendt |", "| Domination | Weber |", "| Autorité | Arendt |"];

describe("la ligne d'apparence", () => {
  it("ne s'écrit pas pour l'apparence par défaut, et se relit à l'identique sinon", () => {
    expect(ecrireApparence(APPARENCE)).toBeNull();
    const toutes: Apparence = {
      entete: "ligne,colonne",
      total: true,
      bandes: "colonnes",
      traits: "entete",
      theme: "ardoise",
      reviser: 2,
      largeur: "page",
    };
    const ligne = ecrireApparence(toutes) as string;
    expect(ligne).toBe(
      "<!-- kyfrev tableau entete=ligne,colonne total bandes=colonnes traits=entete theme=ardoise reviser-cache=2 largeur=page -->",
    );
    expect(lireApparence(ligne)).toEqual(toutes);
    expect(lireApparence("   <!--kyfrev tableau theme=prune-->  ")).toEqual({ ...APPARENCE, theme: "prune" });
  });

  it("garde le défaut d'une valeur inconnue et ignore un mot inconnu", () => {
    expect(lireApparence("<!-- kyfrev tableau theme=rose traits=x bandes=y entete=z reviser-cache=0 futur=1 -->")).toEqual(APPARENCE);
    expect(lireApparence("<!-- kyfrev tableau entete=colonne,ligne -->")?.entete).toBe("ligne,colonne");
    expect(lireApparence("<!-- autre chose -->")).toBeNull();
    expect(lireApparence("texte <!-- kyfrev tableau -->")).toBeNull();
  });

  it("passe par les attributs de la page, dans les deux sens", () => {
    const apparence: Apparence = { ...APPARENCE, entete: "colonne", total: true, reviser: 3, theme: "encre" };
    // Les tests tournent sans page : un élément réduit à ses attributs suffit.
    const attributs = new Map(Object.entries(attributsApparence(apparence)));
    const tableau = { getAttribute: (nom: string) => attributs.get(nom) ?? null, hasAttribute: (nom: string) => attributs.has(nom) };
    expect(apparenceDuDom(tableau as unknown as HTMLElement)).toEqual(apparence);
    expect(attributsApparence(APPARENCE)).not.toHaveProperty("data-total");
  });
});

describe("le Markdown d'un tableau", () => {
  it("lit la ligne au-dessus du tableau et la garde à l'identique tant qu'on n'y touche pas", () => {
    const texte = ["Avant.", "", "<!-- kyfrev tableau theme=foret bandes=lignes -->", ...TABLEAU, "", "Après.", ""].join("\n");
    const origine = depuisMarkdown(texte);
    const tableau = origine.doc.child(1);
    expect(tableau.type).toBe(schema.nodes.table);
    expect(tableau.attrs.theme).toBe("foret");
    expect(tableau.attrs.bandes).toBe("lignes");
    expect(origine.doc.childCount).toBe(3);
    expect(versMarkdown(origine.doc, origine)).toBe(texte);
  });

  it("réécrit la ligne avec le tableau, et l'efface quand l'apparence redevient celle par défaut", () => {
    const origine = depuisMarkdown(TABLEAU.join("\n"));
    const tableau = origine.doc.child(0);
    const change = (attrs: Partial<Apparence>) =>
      versMarkdown(schema.nodes.doc.create(null, tableau.type.create({ ...tableau.attrs, ...attrs }, tableau.content)), origine);
    expect(change({ total: true }).split("\n")[0]).toBe("<!-- kyfrev tableau total -->");
    expect(change({ ...APPARENCE }).split("\n")[0]).toBe("| Notion | Auteur |");
  });

  it("laisse en paragraphe une ligne d'apparence qui n'est pas suivie d'un tableau", () => {
    const doc = depuisMarkdown("<!-- kyfrev tableau theme=foret -->\n\nPas de tableau.").doc;
    expect(doc.child(0).type).toBe(schema.nodes.paragraph);
    expect(doc.child(0).textContent).toBe("<!-- kyfrev tableau theme=foret -->");
  });
});

describe("l'onglet Tableau", () => {
  it("inverse les en-têtes, et les bandes suivent l'en-tête", () => {
    const bandes: Apparence = { ...APPARENCE, bandes: "lignes" };
    const colonne = changerApparence(changerApparence(bandes, "enteteColonne", null, 1) as Apparence, "enteteLigne", null, 1);
    expect(colonne).toMatchObject({ entete: "colonne", bandes: "colonnes" });
    expect(changerApparence(colonne as Apparence, "enteteColonne", null, 1)).toMatchObject({ entete: "aucune", bandes: "lignes" });
    expect(changerApparence(APPARENCE, "enteteColonne", null, 1)).toMatchObject({ entete: "ligne,colonne", bandes: "aucune" });
  });

  it("inverse la dernière ligne et la colonne cachée en révision, et refuse une valeur inconnue", () => {
    expect(changerApparence(APPARENCE, "total", null, 1)?.total).toBe(true);
    const cachee = changerApparence(APPARENCE, "reviser", null, 2) as Apparence;
    expect(cachee.reviser).toBe(2);
    expect(changerApparence(cachee, "reviser", null, 2)?.reviser).toBeNull();
    expect(changerApparence(cachee, "reviser", null, 1)?.reviser).toBe(1);
    expect(changerApparence(APPARENCE, "theme", "prune", 1)?.theme).toBe("prune");
    expect(changerApparence(APPARENCE, "traits", "contour", 1)?.traits).toBe("contour");
    expect(changerApparence(APPARENCE, "bandes", "colonnes", 1)?.bandes).toBe("colonnes");
    expect(changerApparence(APPARENCE, "largeur", "page", 1)?.largeur).toBe("page");
    expect(changerApparence(APPARENCE, "theme", "rose", 1)).toBeNull();
    expect(changerApparence(APPARENCE, "inconnu", null, 1)).toBeNull();
  });

  it("trie les lignes de A à Z, puis de Z à A, l'en-tête et la dernière ligne qui ressort à leur place", () => {
    const lignes = [...TABLEAU, "| Synthèse | — |"].join("\n");
    const tableau = depuisMarkdown(lignes).doc.child(0);
    const avecTotal = tableau.type.create({ ...tableau.attrs, total: true }, tableau.content);
    const premiere = (noeud: ReturnType<typeof trierTableau>) => {
      const textes: string[] = [];
      noeud?.forEach((ligne) => textes.push(ligne.child(0).textContent));
      return textes;
    };
    const montant = trierTableau(avecTotal, 0);
    expect(premiere(montant)).toEqual(["Notion", "Autorité", "Domination", "Pouvoir", "Synthèse"]);
    expect(premiere(trierTableau(montant as NonNullable<typeof montant>, 0))).toEqual(["Notion", "Pouvoir", "Domination", "Autorité", "Synthèse"]);
    expect(trierTableau(avecTotal, 5)).toBeNull();
    expect(triable(tableau)).toBe(true);
  });
});
