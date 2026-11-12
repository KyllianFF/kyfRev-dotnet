import { describe, expect, it } from "vitest";
import { changerTitre, ecrireTitre, IMAGE_ORDINAIRE, lireTitre } from "./image";

describe("le titre d'une image", () => {
  it("donne largeur, position, cadre et masques, et se réécrit pareil", () => {
    const titre = "kyfrev-largeur:60 kyfrev-position:centre kyfrev-cadre:10,5,80,90 kyfrev-masques:10,20,30,15;50,50,20,10";
    const apparence = lireTitre(titre);
    expect(apparence).toEqual({
      largeur: 60,
      position: "centre",
      cadre: { x: 0.1, y: 0.05, largeur: 0.8, hauteur: 0.9 },
      masques: "10,20,30,15;50,50,20,10",
    });
    expect(ecrireTitre(apparence!)).toBe(titre);
  });

  it("n'écrit que ce qui est réglé, toujours dans le même ordre", () => {
    expect(ecrireTitre(IMAGE_ORDINAIRE)).toBeNull();
    expect(ecrireTitre({ ...IMAGE_ORDINAIRE, largeur: 40 })).toBe("kyfrev-largeur:40");
    expect(ecrireTitre(lireTitre("kyfrev-masques:10,10,20,20 kyfrev-largeur:250")!)).toBe("kyfrev-largeur:100 kyfrev-masques:10,10,20,20");
    expect(ecrireTitre({ ...IMAGE_ORDINAIRE, cadre: { x: 0, y: 0, largeur: 1, hauteur: 1 } })).toBeNull();
    expect(ecrireTitre({ ...IMAGE_ORDINAIRE, largeur: 1 })).toBe("kyfrev-largeur:5");
  });

  it("laisse une légende à l'utilisateur, et ne perd rien pour un mot mal écrit", () => {
    expect(lireTitre(null)).toEqual(IMAGE_ORDINAIRE);
    expect(lireTitre("")).toEqual(IMAGE_ORDINAIRE);
    expect(lireTitre("Ma légende")).toBeNull();
    expect(lireTitre("kyfrev-largeur:60 et une légende")).toBeNull();
    expect(lireTitre("kyfrev-inconnu:1")).toBeNull();
    expect(lireTitre("kyfrev-largeur:abc kyfrev-position:ailleurs kyfrev-cadre:1,2 kyfrev-masques:10,10,20,20")).toEqual({
      ...IMAGE_ORDINAIRE,
      masques: "10,10,20,20",
    });
  });

  it("change un réglage en gardant les autres", () => {
    const titre = "kyfrev-largeur:60 kyfrev-masques:10,10,20,20";
    expect(changerTitre(titre, "align", "centre")).toBe("kyfrev-largeur:60 kyfrev-position:centre kyfrev-masques:10,10,20,20");
    expect(changerTitre(titre, "width", null)).toBe("kyfrev-masques:10,10,20,20");
    expect(changerTitre(titre, "width", "35")).toBe("kyfrev-largeur:35 kyfrev-masques:10,10,20,20");
    expect(changerTitre(null, "crop", "10,5,80,90")).toBe("kyfrev-cadre:10,5,80,90");
    expect(changerTitre("kyfrev-cadre:10,5,80,90", "crop", null)).toBeNull();
    expect(changerTitre("kyfrev-largeur:60 kyfrev-position:droite kyfrev-cadre:10,5,80,90 kyfrev-masques:1,1,5,5", "reset", null)).toBe("kyfrev-masques:1,1,5,5");
    expect(changerTitre("kyfrev-position:centre", "align", "fil")).toBeNull();
  });

  it("refuse ce qu'il ne comprend pas, et une légende", () => {
    expect(changerTitre("Ma légende", "width", "50")).toBeUndefined();
    expect(changerTitre(null, "width", "large")).toBeUndefined();
    expect(changerTitre(null, "align", "ailleurs")).toBeUndefined();
    expect(changerTitre(null, "crop", "1,2")).toBeUndefined();
    expect(changerTitre(null, "tourner", "90")).toBeUndefined();
  });
});
