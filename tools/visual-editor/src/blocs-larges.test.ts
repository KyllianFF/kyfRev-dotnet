import { describe, expect, it } from "vitest";
import { debordDuBloc, debordPermis, LARGEUR_DESSIN, largeurDesImages, lireLargeur } from "./blocs-larges";

describe("ce qui est plus large que la colonne de texte, en écriture", () => {
  it("lit les largeurs que donne l'application, et rien d'autre", () => {
    expect(lireLargeur("680px")).toBe(680);
    expect(lireLargeur(" 820px ")).toBe(820);
    expect(lireLargeur("none")).toBeNull();
    expect(lireLargeur("")).toBeNull();
    expect(lireLargeur(null)).toBeNull();
    expect(lireLargeur("0px")).toBeNull();
    expect(lireLargeur("50%")).toBeNull();
  });

  it("laisse déborder jusqu'à la feuille de la colonne « Large », de chaque côté, dans une vue assez large", () => {
    // Confortable (680) dans une vue de 1600 : 70 pixels de chaque côté, la feuille « Large » fait 820.
    expect(debordPermis(1600, 680, 820)).toBe(70);
    // Étroite (570) : 125 de chaque côté.
    expect(debordPermis(1600, 570, 820)).toBe(125);
    // Large : rien de plus.
    expect(debordPermis(1600, 820, 820)).toBe(0);
  });

  it("ne sort jamais de la vue, ses marges comprises, et rien quand la vue n'a que la place du texte", () => {
    // 760 pixels de vue : la feuille ne passe pas 736 (12 de marge de chaque côté).
    expect(debordPermis(760, 680, 820)).toBe(28);
    expect(debordPermis(700, 680, 820)).toBe(0);
    expect(debordPermis(470, 680, 820)).toBe(0);
  });

  it("ne déborde pas sans largeur de colonne, ou sans celle de la colonne « Large »", () => {
    expect(debordPermis(1600, null, 820)).toBe(0);
    expect(debordPermis(1600, 680, null)).toBe(0);
  });

  it("donne à un bloc la moitié de ce qu'il demande de plus que le texte, dans ce qui est permis", () => {
    expect(debordDuBloc(700, 598, 70)).toBe(51);
    expect(debordDuBloc(701, 598, 70)).toBe(52);
    expect(debordDuBloc(2000, 598, 70)).toBe(70);
    // Un bloc qui tient dans la colonne, ou nulle part où déborder : il reste dans le texte.
    expect(debordDuBloc(598, 598, 70)).toBe(0);
    expect(debordDuBloc(300, 598, 70)).toBe(0);
    expect(debordDuBloc(700, 598, 0)).toBe(0);
  });

  it("un dessin demande la largeur qu'il a en lecture", () => {
    expect(LARGEUR_DESSIN).toBe(640);
    expect(debordDuBloc(LARGEUR_DESSIN, 488, 125)).toBe(76);
  });

  it("un paragraphe qui n'a que des images demande la somme de leurs largeurs", () => {
    expect(largeurDesImages([{ image: 800 }])).toBe(800);
    expect(largeurDesImages(["blanc", { image: 400 }, "blanc", { image: 380 }])).toBe(780);
  });

  it("un paragraphe avec du texte, une image à la largeur choisie ou pas encore lue reste dans la colonne", () => {
    expect(largeurDesImages([{ image: 800 }, "autre"])).toBe(0);
    expect(largeurDesImages([{ image: 800 }, { image: null }])).toBe(0);
    expect(largeurDesImages([{ image: 0 }])).toBe(0);
    expect(largeurDesImages(["blanc"])).toBe(0);
    expect(largeurDesImages([])).toBe(0);
  });
});
