import { describe, expect, it } from "vitest";
import { cale, caler, leveeDeTitre, ligneSuivante, lignesDeTitre, pasDEcriture, phaseDuPapier } from "./lignes";
import { PAPIERS } from "./papier";

// Les pas que donne l'application : 8 mm et 10 mm, arrondis au trois-centième de pouce.
const HUIT = 30.08;
const DIX = 37.76;

describe("le pas des lignes d'écriture", () => {
  it("est celui de l'application sur un papier qui a un dessin, au grain du moteur", () => {
    for (const papier of PAPIERS.filter((nom) => nom !== "uni")) {
      const pas = pasDEcriture(papier, HUIT);
      expect(Math.abs(pas - HUIT)).toBeLessThan(1 / 128 + 1e-9);
      expect(Number.isInteger(pas * 64)).toBe(true);
    }
    expect(pasDEcriture("lignes", HUIT)).toBe(30.078125);
    expect(pasDEcriture("points", DIX)).toBe(37.765625);
    // Un pas déjà au grain reste le même.
    expect(pasDEcriture("lignes", 30.5)).toBe(30.5);
  });

  it("est nul sans papier, sur un papier uni, ou sans pas : le texte ne suit alors aucune ligne", () => {
    expect(pasDEcriture(null, HUIT)).toBe(0);
    expect(pasDEcriture("uni", HUIT)).toBe(0);
    for (const pas of [0, -8, 0.4, Number.NaN, Number.POSITIVE_INFINITY, null, undefined]) expect(pasDEcriture("lignes", pas)).toBe(0);
  });
});

describe("les lignes d'un titre", () => {
  it("en prend une tant que ses lettres y tiennent : 1,1 fois leur taille", () => {
    expect(lignesDeTitre(17, HUIT)).toBe(1);
    expect(lignesDeTitre(27.2, HUIT)).toBe(1);
    // 27,35 × 1,1 = 30,085 : juste au-dessus du pas.
    expect(lignesDeTitre(27.35, HUIT)).toBe(2);
    expect(lignesDeTitre(34, DIX)).toBe(1);
    expect(lignesDeTitre(35, DIX)).toBe(2);
  });

  it("tient les titres de la fiche sur une ligne de 8 mm, pour un texte de 16 px", () => {
    // Les tailles de fiche.css : 20, 16 et 14 points pour un corps de 12, puis la taille du texte.
    for (const part of [1.667, 1.333, 1.167, 1, 1, 1]) expect(lignesDeTitre(16 * part, HUIT)).toBe(1);
    // À 17 px, le premier niveau (28,3 px) n'y tenait plus.
    expect(lignesDeTitre(17 * 1.667, HUIT)).toBe(2);
    expect(lignesDeTitre(17 * 1.333, HUIT)).toBe(1);
  });

  it("en prend deux, trois… quand il est plus grand", () => {
    expect(lignesDeTitre(40, HUIT)).toBe(2);
    expect(lignesDeTitre(54, HUIT)).toBe(2);
    expect(lignesDeTitre(56, HUIT)).toBe(3);
  });

  it("ne compte pas une ligne de plus pour une erreur d'arrondi", () => {
    // 1,1 × 20 vaut 22,000000000000004.
    expect(lignesDeTitre(20, 22)).toBe(1);
    expect(lignesDeTitre(40, 22)).toBe(2);
  });

  it("en prend une sans pas ou sans taille", () => {
    expect(lignesDeTitre(40, 0)).toBe(1);
    expect(lignesDeTitre(0, HUIT)).toBe(1);
    expect(lignesDeTitre(Number.NaN, HUIT)).toBe(1);
  });
});

describe("la ligne où le texte reprend", () => {
  it("est la première ligne entière sous le bloc", () => {
    expect(ligneSuivante(1, 30)).toBe(30);
    expect(ligneSuivante(29, 30)).toBe(30);
    expect(ligneSuivante(31, 30)).toBe(60);
    expect(ligneSuivante(100, 30)).toBe(120);
  });

  it("est celle où finit le bloc, à un demi-pixel près : pas de ligne de plus", () => {
    expect(ligneSuivante(60, 30)).toBe(60);
    expect(ligneSuivante(60.5, 30)).toBe(60);
    expect(ligneSuivante(59.6, 30)).toBe(60);
    expect(ligneSuivante(60.6, 30)).toBe(90);
    expect(ligneSuivante(0, 30)).toBe(0);
  });

  it("donne la cale : ce qu'il manque au bloc, et rien s'il finit sur une ligne", () => {
    expect(cale(100, 30)).toBe(20);
    expect(cale(60, 30)).toBe(0);
    // Un peu après la ligne : la suite y reste. Un peu avant : elle y descend, de ce peu.
    expect(cale(60.4, 30)).toBe(0);
    expect(cale(59.7, 30)).toBeCloseTo(0.3, 10);
    expect(cale(59.995, 30)).toBe(0);
    expect(cale(60.6, 30)).toBeCloseTo(29.4, 10);
    expect(cale(59.4, 30)).toBeCloseTo(0.6, 10);
    expect(cale(100, 0)).toBe(0);
  });

  it("ne dépend pas du nombre de lignes déjà passées", () => {
    const pas = pasDEcriture("lignes", HUIT);
    for (const lignes of [0, 1, 7, 250]) expect(cale(lignes * pas + 12, pas)).toBeCloseTo(pas - 12, 9);
    for (const lignes of [1, 7, 250]) expect(cale(lignes * pas, pas)).toBe(0);
  });
});

describe("les cales de la feuille sans fin", () => {
  it("cale chaque bloc qui ne finit pas sur une ligne, en comptant les cales d'avant", () => {
    // Un paragraphe de deux lignes, un tableau qui finit à 100, un paragraphe (160 à 190), une image qui finit à 250.
    const boites = [{ pied: 60 }, { pied: 100 }, { pied: 190 }, { pied: 250 }];
    // Le tableau reçoit 20 : la suite descend d'autant, le paragraphe finit à 210, l'image à 270.
    expect(caler(boites, 30)).toEqual([{ indice: 1, hauteur: 20 }]);
    expect(caler([{ pied: 60 }, { pied: 100 }, { pied: 190 }, { pied: 255 }], 30)).toEqual([
      { indice: 1, hauteur: 20 },
      { indice: 3, hauteur: 25 },
    ]);
  });

  it("laisse les boîtes qui ne finissent aucun bloc, et ne fait rien sans pas", () => {
    expect(caler([{}, { pied: 45 }, {}], 30)).toEqual([{ indice: 1, hauteur: 15 }]);
    expect(caler([{ pied: 45 }], 0)).toEqual([]);
  });

  it("ne change plus rien une fois posées", () => {
    const boites = [{ pied: 47 }, { pied: 133 }, { pied: 190.2 }];
    const cales = caler(boites, 30);
    let cumul = 0;
    const calees = boites.map((boite, indice) => {
      cumul += cales.find((ajout) => ajout.indice === indice)?.hauteur ?? 0;
      return { pied: boite.pied + cumul };
    });
    expect(caler(calees, 30)).toEqual([]);
  });
});

describe("la phase du papier", () => {
  it("fait passer la ligne d'écriture au pied des lettres de la première ligne, et donc de toutes", () => {
    // Le texte commence 33 px sous le haut de la feuille, le pied des lettres est 22 px sous le haut de la ligne.
    const phase = phaseDuPapier(33, 22, 30);
    expect(phase).toBe(25);
    for (const ligne of [0, 1, 2, 40]) expect((33 + ligne * 30 + 22 - phase) % 30).toBe(0);
  });

  it("reste entre zéro et un pas", () => {
    expect(phaseDuPapier(0, 22, 30)).toBe(22);
    expect(phaseDuPapier(8, 22, 30)).toBe(0);
    expect(phaseDuPapier(56.6875, 20, 30.078125)).toBeCloseTo(76.6875 - 2 * 30.078125, 9);
    expect(phaseDuPapier(-10, 4, 30)).toBe(24);
    expect(phaseDuPapier(33, 22, 0)).toBe(0);
  });

  it("est la même sur chaque page : le texte y commence une marge sous le haut de la feuille", () => {
    const [marge, pied, pas] = [56.6875, 21, 37.765625];
    const phase = phaseDuPapier(marge, pied, pas);
    // Sur n'importe quelle page, la ligne k du texte est à marge + k × pas : son pied tombe sur une ligne du dessin.
    for (const ligne of [0, 5, 17]) {
      const tours = (marge + ligne * pas + pied - phase) / pas;
      expect(Math.abs(tours - Math.round(tours))).toBeLessThan(1e-9);
    }
  });
});

describe("le décalage d'un titre", () => {
  it("le pose sur la ligne du texte quand il tient dans une ligne", () => {
    // Le pied du texte est à 22 ; celui d'un titre plus gros tombe 4 px plus bas : il remonte d'autant.
    expect(leveeDeTitre(22, 26, 1, 30)).toBe(-4);
    expect(leveeDeTitre(22, 22, 1, 30)).toBe(0);
    expect(leveeDeTitre(22, 20, 1, 30)).toBe(2);
  });

  it("le pose sur la dernière de ses lignes quand il en prend plusieurs", () => {
    // Deux lignes : le pied doit tomber à 30 + 22 ; il est à 41 dans la boîte du titre.
    expect(leveeDeTitre(22, 41, 2, 30)).toBe(11);
    expect(leveeDeTitre(22, 60, 3, 30)).toBe(22);
  });
});
