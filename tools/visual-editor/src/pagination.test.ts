import { describe, expect, it } from "vitest";
import { appliquer, type Boite, type Geometrie, hauteurUtile, naturelles, paginer, pasDePage } from "./pagination";

// Une petite feuille : 300 de haut, 50 de marge, donc 200 de texte par page, et 316 d'un haut de texte au suivant.
const feuille: Geometrie = { hauteur: 300, marge: 50, ecart: 16 };
const A4: Geometrie = { hauteur: 1123, marge: 76, ecart: 16 };

/** Des boîtes qui se suivent, séparées de `entre` pixels. */
function suite(hauteurs: number[], genre: Boite["genre"] = "bloc", entre = 0): Boite[] {
  let haut = 0;
  return hauteurs.map((hauteur) => {
    const boite = { haut, hauteur, genre };
    haut += hauteur + entre;
    return boite;
  });
}

/** Aucune boîte placée ne franchit le bas du texte de sa page, sauf celles plus hautes qu'une page. */
function toutTient(placees: Boite[], contenus: Boite[], geometrie: Geometrie): boolean {
  const utile = hauteurUtile(geometrie);
  const pas = pasDePage(geometrie);
  return placees.every((boite, indice) => {
    const contenu = contenus[indice] as Boite;
    if (contenu.hauteur > utile) return true;
    // Le contenu d'une ligne poussée est en bas de sa boîte.
    const bas = boite.haut + boite.hauteur;
    const haut = bas - Math.min(contenu.hauteur, utile);
    const page = Math.floor((haut + 0.5) / pas);
    return haut >= page * pas - 0.5 && bas <= page * pas + utile + 0.5;
  });
}

describe("paginer", () => {
  it("ne pousse rien quand tout tient sur la page", () => {
    const resultat = paginer(suite([40, 60, 100]), feuille);
    expect(resultat).toEqual({ sauts: [], aReduire: [], pages: 1, coupees: [], cales: [] });
  });

  it("compte une page pour une fiche vide", () => {
    expect(paginer([], feuille).pages).toBe(1);
    expect(paginer([{ haut: 0, hauteur: 0, genre: "bloc" }], feuille).pages).toBe(1);
  });

  it("laisse sur la page le bloc qui finit juste à son bas", () => {
    expect(paginer(suite([120, 80]), feuille).sauts).toEqual([]);
    // Une fraction de pixel de trop n'est pas un dépassement.
    expect(paginer(suite([120, 80.3]), feuille).sauts).toEqual([]);
  });

  it("pousse entier à la page suivante le bloc qui franchirait le bas", () => {
    const boites = suite([150, 80, 30]);
    const resultat = paginer(boites, feuille);
    // Le deuxième bloc commençait à 150 : il commence la page 2, à 316.
    expect(resultat.sauts).toEqual([{ indice: 1, decalage: 166, page: 1 }]);
    expect(resultat.pages).toBe(2);
    const placees = appliquer(boites, resultat, feuille);
    expect(placees.map((boite) => boite.haut)).toEqual([0, 316, 396]);
  });

  it("garde entre deux textes les deux marges et l'écart, en plus de la place restée libre", () => {
    const boites = suite([150, 80]);
    const [saut] = paginer(boites, feuille).sauts;
    const libre = 200 - 150;
    expect(saut?.decalage).toBe(libre + feuille.marge + feuille.ecart + feuille.marge);
  });

  it("laisse suivre son cours le bloc plus haut qu'une page", () => {
    const boites = suite([100, 500, 40]);
    const resultat = paginer(boites, feuille);
    // Le grand bloc reste à 100 et finit à 600, dans la page 2 (316 à 516)… au-delà : le suivant commence la page 3.
    expect(resultat.sauts).toEqual([{ indice: 2, decalage: 32, page: 2 }]);
    expect(resultat.pages).toBe(3);
  });

  it("ne pousse pas le bloc trop haut qui commence une page", () => {
    const resultat = paginer(suite([450, 20]), feuille);
    // 450 finit dans la page 2 (316 à 516) : le petit bloc y tient encore.
    expect(resultat.sauts).toEqual([]);
    expect(resultat.pages).toBe(2);
  });

  it("pousse le bloc qui commencerait dans la marge du bas, même trop haut", () => {
    // Le premier, trop haut, finit à 250 : dans la marge du bas de la page 1.
    const resultat = paginer(suite([250, 300]), feuille);
    expect(resultat.sauts).toEqual([{ indice: 1, decalage: 66, page: 1 }]);
  });

  it("pousse les lignes d'un tableau une à une, sur trois pages", () => {
    // Sept lignes de 60 : trois par page de 200.
    const lignes = suite([60, 60, 60, 60, 60, 60, 60], "ligne");
    const resultat = paginer(lignes, feuille);
    expect(resultat.sauts).toEqual([
      { indice: 3, decalage: 136, page: 1 },
      { indice: 6, decalage: 136, page: 2 },
    ]);
    expect(resultat.pages).toBe(3);
    const placees = appliquer(lignes, resultat, feuille);
    // Une ligne poussée ne descend pas : elle grandit par le haut, et son texte commence la page.
    expect(placees[3]).toEqual({ haut: 180, hauteur: 196, genre: "ligne" });
    expect(placees[4]?.haut).toBe(376);
    expect(toutTient(placees, lignes, feuille)).toBe(true);
  });

  it("signale la figure plus haute qu'une page, et la compte à la hauteur du texte", () => {
    const boites: Boite[] = [
      { haut: 0, hauteur: 50, genre: "bloc" },
      { haut: 50, hauteur: 700, genre: "figure" },
      { haut: 750, hauteur: 30, genre: "bloc" },
    ];
    const resultat = paginer(boites, feuille);
    expect(resultat.aReduire).toEqual([1]);
    // Réduite à 200, elle ne tient pas après les 50 du premier bloc : elle prend toute la page 2.
    expect(resultat.sauts[0]).toEqual({ indice: 1, decalage: 266, page: 1 });
    // Une fois réduite dans la page, ce qui la suit remonte : la mesure suivante le dira.
    const reduites = boites.map((boite, indice) => (indice === 2 ? { ...boite, haut: 250 } : boite));
    expect(paginer(reduites, feuille)).toMatchObject({ aReduire: [1], pages: 3 });
  });

  it("ne coupe pas une figure qui tient sur une page, et ne la réduit pas", () => {
    const boites: Boite[] = [
      { haut: 0, hauteur: 120, genre: "bloc" },
      { haut: 120, hauteur: 150, genre: "figure" },
    ];
    const resultat = paginer(boites, feuille);
    expect(resultat.aReduire).toEqual([]);
    expect(resultat.sauts).toEqual([{ indice: 1, decalage: 196, page: 1 }]);
  });

  it("redonne les mêmes sauts quand on le relance sur son propre résultat", () => {
    const boites: Boite[] = [
      ...suite([300, 420, 80, 610, 55, 2000, 40, 333], "bloc", 17),
      ...suite([90, 90, 400, 90, 1200, 90], "ligne").map((boite) => ({ ...boite, haut: boite.haut + 4000 })),
      { haut: 6000, hauteur: 1500, genre: "figure" },
      { haut: 7500, hauteur: 300, genre: "figure" },
      { haut: 7817, hauteur: 28, genre: "bloc" },
    ];
    const premier = paginer(boites, A4);
    expect(premier.sauts.length).toBeGreaterThanOrEqual(3);
    const placees = appliquer(boites, premier, A4);
    expect(toutTient(placees, boites, A4)).toBe(true);
    // Ce que la page mesurerait alors, espaces retirés : les mêmes places, la grande figure réduite.
    const mesurees = naturelles(placees, premier.sauts);
    const second = paginer(mesurees, A4);
    expect(second.sauts).toEqual(premier.sauts);
    expect(second.pages).toBe(premier.pages);
    expect(second.aReduire).toEqual([]);
    expect(appliquer(mesurees, second, A4)).toEqual(placees);
  });

  it("ne dépend pas des fractions de pixel d'une mesure à l'autre", () => {
    const boites = suite([150.2, 80.1, 30.4, 190.3, 12.25]);
    const premier = paginer(boites, feuille);
    const bruit = boites.map((boite, indice) => ({ ...boite, haut: boite.haut + (indice % 2 === 0 ? 0.2 : -0.2) }));
    expect(paginer(bruit, feuille).sauts.map((saut) => saut.indice)).toEqual(premier.sauts.map((saut) => saut.indice));
  });
});

describe("sauts de page demandés", () => {
  const marque = (haut: number, saut: "page" | "blanche" = "page"): Boite => ({ haut, hauteur: 10, genre: "bloc", saut });

  it("commence une nouvelle page après la marque, même s'il restait de la place", () => {
    const boites: Boite[] = [{ haut: 0, hauteur: 40, genre: "bloc" }, marque(40), { haut: 50, hauteur: 30, genre: "bloc" }];
    const resultat = paginer(boites, feuille);
    expect(resultat.sauts).toEqual([{ indice: 2, decalage: 266, page: 1 }]);
    expect(resultat.pages).toBe(2);
    expect(resultat.coupees).toEqual([1]);
    expect(appliquer(boites, resultat, feuille)[2]?.haut).toBe(316);
  });

  it("saute aussi quand la marque est tout en haut d'une page", () => {
    const boites: Boite[] = [marque(0), { haut: 10, hauteur: 30, genre: "bloc" }];
    const resultat = paginer(boites, feuille);
    expect(resultat.sauts).toEqual([{ indice: 1, decalage: 306, page: 1 }]);
    expect(resultat.pages).toBe(2);
  });

  it("ne pousse pas la marque : elle finit sa page, quitte à tenir dans la marge du bas", () => {
    const boites: Boite[] = [{ haut: 0, hauteur: 195, genre: "bloc" }, marque(195), { haut: 205, hauteur: 30, genre: "bloc" }];
    const resultat = paginer(boites, feuille);
    expect(resultat.sauts).toEqual([{ indice: 2, decalage: 111, page: 1 }]);
    expect(resultat.pages).toBe(2);
  });

  it("laisse exactement une page vide après une page blanche", () => {
    const boites: Boite[] = [{ haut: 0, hauteur: 40, genre: "bloc" }, marque(40, "blanche"), { haut: 50, hauteur: 30, genre: "bloc" }];
    const resultat = paginer(boites, feuille);
    expect(resultat.sauts).toEqual([{ indice: 2, decalage: 2 * 316 - 50, page: 2 }]);
    expect(resultat.pages).toBe(3);
    // Les deux écarts, avant et après la page vide, sont nets.
    expect(resultat.coupees).toEqual([1, 2]);
  });

  it("compte les pages d'une marque en dernier", () => {
    expect(paginer([{ haut: 0, hauteur: 40, genre: "bloc" }, marque(40)], feuille)).toMatchObject({ sauts: [], pages: 2, coupees: [1] });
    expect(paginer([{ haut: 0, hauteur: 40, genre: "bloc" }, marque(40, "blanche")], feuille)).toMatchObject({ pages: 3, coupees: [1, 2] });
  });

  it("deux marques de suite laissent une page vide", () => {
    const boites: Boite[] = [marque(0), marque(10), { haut: 20, hauteur: 30, genre: "bloc" }];
    const resultat = paginer(boites, feuille);
    expect(resultat.sauts.map((saut) => saut.page)).toEqual([1, 2]);
    expect(resultat.pages).toBe(3);
  });

  it("redonne les mêmes sauts de page demandés quand on le relance sur son propre résultat", () => {
    const boites: Boite[] = [
      { haut: 0, hauteur: 120, genre: "bloc" },
      marque(120),
      { haut: 130, hauteur: 150, genre: "bloc" },
      { haut: 280, hauteur: 90, genre: "bloc" },
      marque(370, "blanche"),
      { haut: 380, hauteur: 60, genre: "ligne" },
      { haut: 440, hauteur: 190, genre: "ligne" },
    ];
    const premier = paginer(boites, feuille);
    const placees = appliquer(boites, premier, feuille);
    const second = paginer(naturelles(placees, premier.sauts), feuille);
    expect(second).toEqual(premier);
    expect(premier.pages).toBe(6);
  });
});

describe("en-tête de tableau remontrée", () => {
  it("garde sa hauteur en haut de la page où une ligne est poussée", () => {
    // Le début du tableau (en-tête de 30 et première ligne, 90 en tout), puis des lignes de 60.
    const lignes: Boite[] = [
      { haut: 0, hauteur: 90, genre: "bloc" },
      { haut: 90, hauteur: 60, genre: "ligne", rappel: 30 },
      { haut: 150, hauteur: 60, genre: "ligne", rappel: 30 },
      { haut: 210, hauteur: 60, genre: "ligne", rappel: 30 },
      { haut: 270, hauteur: 60, genre: "ligne", rappel: 30 },
    ];
    const resultat = paginer(lignes, feuille);
    // La troisième boîte franchirait 200 : son texte commence la page 2 (316) sous l'en-tête remontrée, à 346.
    // Sous l'en-tête il reste 170 : deux lignes, et la suivante commence la page 3 de la même façon.
    expect(resultat.sauts).toEqual([
      { indice: 2, decalage: 166 + 30, page: 1 },
      { indice: 4, decalage: 632 + 30 - 466, page: 2 },
    ]);
    const placees = appliquer(lignes, resultat, feuille);
    expect(placees[2]).toEqual({ haut: 150, hauteur: 196 + 60, genre: "ligne", rappel: 30 });
    expect(placees[3]?.haut).toBe(406);
    expect((placees[4]?.haut ?? 0) + (placees[4]?.hauteur ?? 0)).toBe(632 + 30 + 60);
    expect(resultat.pages).toBe(3);
  });

  it("ne garde rien pour un tableau sans en-tête", () => {
    const lignes: Boite[] = [
      { haut: 0, hauteur: 90, genre: "bloc" },
      { haut: 90, hauteur: 60, genre: "ligne" },
      { haut: 150, hauteur: 60, genre: "ligne" },
    ];
    expect(paginer(lignes, feuille).sauts).toEqual([{ indice: 2, decalage: 166, page: 1 }]);
  });

  it("reste stable avec des en-têtes remontrées", () => {
    const lignes: Boite[] = Array.from({ length: 12 }, (_, rang) => ({ haut: rang * 70, hauteur: 70, genre: "ligne" as const, rappel: 25 }));
    const premier = paginer(lignes, feuille);
    const second = paginer(naturelles(appliquer(lignes, premier, feuille), premier.sauts), feuille);
    expect(second).toEqual(premier);
  });
});

describe("papier réglé : les cales", () => {
  // Des lignes d'écriture tous les 30 : six lignes entières dans les 200 de texte d'une page.
  const reglee: Geometrie = { ...feuille, ligne: 30 };

  it("cale un bloc qui garde sa hauteur, pour que la suite reprenne sur une ligne", () => {
    const boites: Boite[] = [
      { haut: 0, hauteur: 60, genre: "bloc", pied: 60 },
      // Un tableau de 55, une ligne vide après le paragraphe : il finit à 145, cinq pixels avant une ligne.
      { haut: 90, hauteur: 55, genre: "bloc", pied: 145 },
      { haut: 145, hauteur: 30, genre: "bloc", pied: 175 },
    ];
    const resultat = paginer(boites, reglee);
    expect(resultat.cales).toEqual([{ indice: 1, hauteur: 5 }]);
    expect(resultat.sauts).toEqual([]);
    expect(appliquer(boites, resultat, reglee).map((boite) => boite.haut)).toEqual([0, 90, 150]);
  });

  it("ne cale rien sans lignes d'écriture, ni sous un bloc qui finit sur une ligne ou la dépasse à peine", () => {
    const boites: Boite[] = [
      { haut: 0, hauteur: 55, genre: "bloc", pied: 55 },
      { haut: 55, hauteur: 30, genre: "bloc", pied: 85 },
    ];
    expect(paginer(boites, feuille).cales).toEqual([]);
    const justes: Boite[] = [
      { haut: 0, hauteur: 60.3, genre: "bloc", pied: 60.3 },
      { haut: 60.3, hauteur: 29.9, genre: "bloc", pied: 90.2 },
    ];
    expect(paginer(justes, reglee).cales).toEqual([]);
  });

  it("compte les lignes depuis le haut du texte de chaque page", () => {
    const boites: Boite[] = [
      { haut: 0, hauteur: 150, genre: "bloc", pied: 150 },
      // Poussé en haut de la page 2 (316), il y finit à 80 : la ligne suivante de cette page est à 90.
      { haut: 150, hauteur: 80, genre: "bloc", pied: 230 },
      { haut: 230, hauteur: 30, genre: "bloc", pied: 260 },
    ];
    const resultat = paginer(boites, reglee);
    expect(resultat.sauts).toEqual([{ indice: 1, decalage: 166, page: 1 }]);
    expect(resultat.cales).toEqual([{ indice: 1, hauteur: 10 }]);
    expect(appliquer(boites, resultat, reglee)[2]?.haut).toBe(316 + 90);
  });

  it("ne cale pas sous la dernière ligne d'une page, ni après la marque d'un saut de page", () => {
    // 195 : la ligne suivante serait à 210, dans la marge du bas.
    expect(paginer([{ haut: 0, hauteur: 195, genre: "bloc", pied: 195 }], reglee).cales).toEqual([]);
    const boites: Boite[] = [
      { haut: 0, hauteur: 60, genre: "bloc", pied: 60 },
      { haut: 60, hauteur: 10, genre: "bloc", saut: "page", pied: 70 },
      { haut: 70, hauteur: 30, genre: "bloc", pied: 100 },
    ];
    const resultat = paginer(boites, reglee);
    expect(resultat.cales).toEqual([]);
    expect(appliquer(boites, resultat, reglee)[2]?.haut).toBe(316);
  });

  it("cale un tableau sous sa dernière ligne, même poussée à la page suivante", () => {
    const lignes: Boite[] = [
      { haut: 0, hauteur: 90, genre: "bloc" },
      { haut: 90, hauteur: 70, genre: "ligne" },
      // Franchirait 200 : son texte commence la page 2, à 316, et le tableau y finit à 70.
      { haut: 160, hauteur: 70, genre: "ligne", pied: 230 },
      { haut: 230, hauteur: 30, genre: "bloc", pied: 260 },
    ];
    const resultat = paginer(lignes, reglee);
    expect(resultat.sauts).toEqual([{ indice: 2, decalage: 156, page: 1 }]);
    expect(resultat.cales).toEqual([{ indice: 2, hauteur: 20 }]);
    expect(appliquer(lignes, resultat, reglee)[3]?.haut).toBe(316 + 90);
  });

  it("reste stable : relancé sur son propre résultat, il redonne les mêmes sauts et les mêmes cales", () => {
    const hauteurs = [60, 47, 90, 133, 30, 75, 120, 60, 41, 150, 30, 99, 60];
    let haut = 0;
    const boites: Boite[] = hauteurs.map((hauteur, rang) => {
      const boite: Boite = { haut, hauteur, genre: rang % 4 === 3 ? "figure" : "bloc", pied: haut + hauteur };
      haut += hauteur + (rang % 2 === 0 ? 30 : 0);
      return boite;
    });
    const premier = paginer(boites, reglee);
    expect(premier.cales.length).toBeGreaterThan(3);
    const placees = appliquer(boites, premier, reglee);
    const second = paginer(naturelles(placees, premier.sauts, premier.cales), reglee);
    expect(second).toEqual(premier);
    expect(appliquer(naturelles(placees, premier.sauts, premier.cales), second, reglee)).toEqual(placees);
    // Après chaque cale, la boîte suivante de la même page commence sur une ligne de cette page.
    const pas = pasDePage(reglee);
    for (const { indice } of premier.cales) {
      const suivante = placees[indice + 1];
      if (!suivante) continue;
      const dans = suivante.haut - Math.floor((suivante.haut + 0.5) / pas) * pas;
      expect(Math.abs(dans - Math.round(dans / 30) * 30)).toBeLessThan(1e-9);
    }
  });
});
