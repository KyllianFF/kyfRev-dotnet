import { describe, expect, it } from "vitest";
import { placerBulle, placerMenu } from "./placement";

describe("place d'un menu flottant dans la fenêtre", () => {
  const menu = { largeur: 240, hauteur: 290 };
  // La plus petite fenêtre de l'application.
  const petite = { largeur: 470, hauteur: 540 };

  it("reste où on l'a demandé quand il tient", () => {
    expect(placerMenu({ x: 100, y: 120 }, menu, { largeur: 1600, hauteur: 900 })).toEqual({ x: 100, y: 120 });
  });

  it("revient dans la fenêtre à droite et en bas, avec la marge", () => {
    expect(placerMenu({ x: 460, y: 530 }, menu, petite)).toEqual({ x: 222, y: 242 });
    expect(placerMenu({ x: 460, y: 530 }, menu, petite, 0)).toEqual({ x: 230, y: 250 });
  });

  it("ne sort ni à gauche ni en haut", () => {
    expect(placerMenu({ x: -30, y: -5 }, menu, petite)).toEqual({ x: 8, y: 8 });
  });

  it("aligné sur la droite d'un bouton, il finit à l'ancre", () => {
    expect(placerMenu({ x: 440, y: 100, fin: true }, menu, petite)).toEqual({ x: 200, y: 100 });
    // Trop près du bord gauche : il se décale plutôt que de sortir.
    expect(placerMenu({ x: 120, y: 100, fin: true }, menu, petite)).toEqual({ x: 8, y: 100 });
  });

  it("plus grand que la fenêtre, il se cale en haut à gauche", () => {
    expect(placerMenu({ x: 300, y: 300 }, { largeur: 600, hauteur: 700 }, petite)).toEqual({ x: 8, y: 8 });
  });

  it("le menu entier est toujours dans la fenêtre, à toutes les positions", () => {
    for (const fenetre of [petite, { largeur: 900, hauteur: 600 }, { largeur: 3440, hauteur: 1440 }]) {
      for (let x = 0; x <= fenetre.largeur; x += 47) {
        for (let y = 0; y <= fenetre.hauteur; y += 53) {
          const place = placerMenu({ x, y }, menu, fenetre);
          expect(place.x).toBeGreaterThanOrEqual(8);
          expect(place.y).toBeGreaterThanOrEqual(8);
          expect(place.x + menu.largeur).toBeLessThanOrEqual(fenetre.largeur - 8);
          expect(place.y + menu.hauteur).toBeLessThanOrEqual(fenetre.hauteur - 8);
        }
      }
    }
  });
});

describe("place d'une bulle près d'un objet du plan", () => {
  const bulle = { largeur: 276, hauteur: 230 };
  const plan = { largeur: 1440, hauteur: 580 };

  it("à droite de l'objet, à sa hauteur, quand elle y tient", () => {
    expect(placerBulle({ x0: 800, y0: 200, x1: 884, y1: 280 }, bulle, plan)).toEqual({ x: 896, y: 200 });
  });

  it("à gauche si la droite manque de place, puis dessous, puis dessus", () => {
    expect(placerBulle({ x0: 1300, y0: 200, x1: 1384, y1: 280 }, bulle, plan)).toEqual({ x: 1012, y: 200 });
    const etroit = { largeur: 420, hauteur: 700 };
    expect(placerBulle({ x0: 170, y0: 100, x1: 254, y1: 180 }, bulle, etroit)).toEqual({ x: 74, y: 192 });
    expect(placerBulle({ x0: 170, y0: 500, x1: 254, y1: 580 }, bulle, etroit)).toEqual({ x: 74, y: 258 });
  });

  it("reste dans le cadre quand l'objet touche un bord, ou en est sorti", () => {
    expect(placerBulle({ x0: 600, y0: 500, x1: 684, y1: 580 }, bulle, plan)).toEqual({ x: 696, y: 342 });
    expect(placerBulle({ x0: -300, y0: -90, x1: -216, y1: -10 }, bulle, plan)).toEqual({ x: 8, y: 8 });
    expect(placerBulle({ x0: 2000, y0: 100, x1: 2084, y1: 180 }, bulle, plan)).toEqual({ x: 1156, y: 100 });
  });

  it("l'ordre des côtés se choisit : sous un câble d'abord", () => {
    const sousLeCable = ["dessous" as const, "dessus" as const, "droite" as const, "gauche" as const];
    expect(placerBulle({ x0: 640, y0: 240, x1: 686, y1: 280 }, bulle, plan, sousLeCable)).toEqual({ x: 525, y: 292 });
    expect(placerBulle({ x0: 640, y0: 440, x1: 686, y1: 480 }, bulle, plan, sousLeCable)).toEqual({ x: 525, y: 198 });
  });

  it("aucun côté ne suffit (fenêtre étroite) : rien, la bulle devient un panneau en bas", () => {
    expect(placerBulle({ x0: 180, y0: 100, x1: 264, y1: 180 }, bulle, { largeur: 450, hauteur: 280 })).toBeNull();
    // Plus grande que le cadre : pareil.
    expect(placerBulle({ x0: 10, y0: 10, x1: 50, y1: 50 }, bulle, { largeur: 260, hauteur: 900 })).toBeNull();
  });

  it("la bulle entière est toujours dans le cadre, où que soit l'objet", () => {
    for (const cadre of [{ largeur: 470, hauteur: 300 }, plan, { largeur: 3440, hauteur: 1200 }]) {
      for (let x = -200; x <= cadre.largeur + 200; x += 61) {
        for (let y = -200; y <= cadre.hauteur + 200; y += 67) {
          const place = placerBulle({ x0: x, y0: y, x1: x + 84, y1: y + 80 }, bulle, cadre);
          if (place === null) continue;
          expect(place.x).toBeGreaterThanOrEqual(8);
          expect(place.y).toBeGreaterThanOrEqual(8);
          expect(place.x + bulle.largeur).toBeLessThanOrEqual(cadre.largeur - 8);
          expect(place.y + bulle.hauteur).toBeLessThanOrEqual(cadre.hauteur - 8);
        }
      }
    }
  });
});
