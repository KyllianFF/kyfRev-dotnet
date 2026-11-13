import { describe, expect, it } from "vitest";
import { CATEGORIES, TYPES } from "./catalogue";
import { GABARITS } from "./gabarits";
import { famillesDuPlateau, nomCourt, tuilesDuPlateau } from "./plateau";

describe("plateau d'équipements : familles", () => {
  it("« Tous », puis chaque catégorie du catalogue, sous un nom court", () => {
    const familles = famillesDuPlateau(false);
    expect(familles.map((f) => f.nom)).toEqual([
      "Tous",
      "Réseau",
      "Serveurs",
      "Postes",
      "Stockage",
      "Infrastructure",
      "Contexte",
    ]);
    // Aucune catégorie du catalogue n'est oubliée, et chacune garde son nom entier en titre.
    for (const c of CATEGORIES) {
      expect(familles.find((f) => f.valeur === c.valeur)?.titre).toBe(c.nom);
    }
  });

  it("le mode avancé y ajoute ce que la bibliothèque proposait : cadres et notes, modèles", () => {
    expect(
      famillesDuPlateau(true)
        .slice(-2)
        .map((f) => f.valeur),
    ).toEqual(["cadres", "modeles"]);
    expect(tuilesDuPlateau("cadres").map((t) => t.genre)).toEqual([
      "groupe:site",
      "groupe:batiment",
      "groupe:salle",
      "groupe:zone",
      "groupe:groupe",
      "annotation",
      "annotation:texte",
      "annotation:rectangle",
      "annotation:ellipse",
      "annotation:fleche",
    ]);
    expect(tuilesDuPlateau("modeles").map((t) => t.genre)).toEqual(GABARITS.map((g) => `gabarit:${g.id}`));
    for (const t of [...tuilesDuPlateau("cadres"), ...tuilesDuPlateau("modeles")]) expect(t.icone).toBeTruthy();
  });
});

describe("plateau d'équipements : tuiles", () => {
  it("« Tous » montre tout le catalogue, les équipements courants d'abord", () => {
    const tuiles = tuilesDuPlateau("tous");
    expect(tuiles.length).toBe(TYPES.length);
    expect(new Set(tuiles.map((t) => t.genre)).size).toBe(TYPES.length);
    expect(tuiles.slice(0, 13).map((t) => t.nom)).toEqual([
      "Switch",
      "Cœur",
      "Routeur",
      "Pare-feu",
      "Borne Wi-Fi",
      "Serveur",
      "NAS",
      "PC",
      "Portable",
      "Imprimante",
      "Téléphone",
      "Internet",
      "Cloud",
    ]);
  });

  it("chaque tuile porte l'équipement du catalogue, un nom court et son nom entier", () => {
    for (const t of tuilesDuPlateau("tous")) {
      expect(t.type, t.genre).toBeDefined();
      expect(t.genre).toBe(`type:${t.type?.type}`);
      expect(t.titre).toBe(t.type?.nom);
      // Le nom tient sous l'icône d'une tuile de 86 px.
      expect(t.nom.length, t.nom).toBeLessThanOrEqual(13);
    }
    // Un type que le plateau ne connaît pas encore garde son nom du catalogue.
    const inconnu = { ...(TYPES[0] as (typeof TYPES)[number]), type: "nouveau", nom: "Nouveau type" };
    expect(nomCourt(inconnu)).toBe("Nouveau type");
  });

  it("une famille ne montre que ses équipements, dans l'ordre du catalogue", () => {
    for (const c of CATEGORIES) {
      expect(tuilesDuPlateau(c.valeur).map((t) => t.type?.type)).toEqual(
        TYPES.filter((t) => t.categorie === c.valeur).map((t) => t.type),
      );
    }
    expect(tuilesDuPlateau("stockage").map((t) => t.nom)).toContain("NAS");
    expect(tuilesDuPlateau("stockage").map((t) => t.nom)).not.toContain("Switch");
  });

  it("la recherche ignore la casse et les accents, et connaît le nom court comme le nom entier", () => {
    expect(tuilesDuPlateau("tous", "COEUR").map((t) => t.type?.type)).toEqual(["switch-coeur"]);
    // « Borne » n'est que dans le nom court ; « accès » que dans le nom entier.
    expect(tuilesDuPlateau("tous", "borne").map((t) => t.type?.type)).toEqual(["point-acces"]);
    expect(tuilesDuPlateau("tous", "point d'acces").map((t) => t.type?.type)).toEqual(["point-acces"]);
    expect(tuilesDuPlateau("tous", "  ").length).toBe(TYPES.length);
    expect(tuilesDuPlateau("tous", "zzz")).toEqual([]);
  });

  it("la recherche se combine avec la famille", () => {
    const partout = tuilesDuPlateau("tous", "switch").map((t) => t.type?.type);
    expect(partout).toContain("switch-stockage");
    expect(partout[0]).toBe("switch");
    expect(tuilesDuPlateau("stockage", "switch").map((t) => t.type?.type)).toEqual(["switch-stockage"]);
    expect(tuilesDuPlateau("contexte", "switch")).toEqual([]);
  });
});
