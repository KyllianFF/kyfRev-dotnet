import { describe, expect, it } from "vitest";
import { motAutour } from "./correcteur";

const mot = (texte: string, position: number) => {
  const etendue = motAutour(texte, position);
  return etendue && texte.slice(etendue.debut, etendue.fin);
};

describe("mot cliqué pour le correcteur (D31)", () => {
  it("le mot qui touche la position, du début à la fin", () => {
    expect(mot("Le prinsipe fondamantal", 3)).toBe("prinsipe");
    expect(mot("Le prinsipe fondamantal", 7)).toBe("prinsipe");
    // Juste après la dernière lettre : le mot compte encore.
    expect(mot("Le prinsipe fondamantal", 11)).toBe("prinsipe");
    expect(mot("Le prinsipe fondamantal", 23)).toBe("fondamantal");
    expect(motAutour("Le prinsipe", 3)).toEqual({ debut: 3, fin: 11 });
  });

  it("apostrophes et traits d'union dans le mot, pas à ses bords", () => {
    expect(mot("de l'éléfant.", 7)).toBe("l'éléfant");
    expect(mot("C’est peut-être ça", 9)).toBe("peut-être");
    expect(mot("« 'mot' »", 4)).toBe("mot");
    expect(mot("un -tiret", 5)).toBe("tiret");
  });

  it("hors d'un mot : rien", () => {
    expect(motAutour("deux  espaces", 5)).toBeNull();
    expect(motAutour("« ? »", 2)).toBeNull();
    expect(motAutour("' - '", 2)).toBeNull();
    expect(motAutour("", 0)).toBeNull();
  });
});
