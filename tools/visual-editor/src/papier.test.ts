import { describe, expect, it } from "vitest";
import { ATTRIBUT_PAPIER, estRegle, PAPIERS, papierDe, poserPapier } from "./papier";

describe("le papier de la fiche", () => {
  it("reconnaît les six papiers de l'application, et rien d'autre", () => {
    expect([...PAPIERS]).toEqual(["uni", "petits-carreaux", "grands-carreaux", "lignes", "points", "millimetre"]);
    for (const nom of PAPIERS) expect(papierDe(nom)).toBe(nom);
    expect(papierDe(" lignes ")).toBe("lignes");
    for (const nom of ["", "  ", "Lignes", "seyes", "carreaux", 'lignes"] body', null, undefined]) expect(papierDe(nom)).toBeNull();
  });

  it("ne laisse voir le dessin à travers les blocs que s'il y en a un", () => {
    expect(estRegle(null)).toBe(false);
    expect(estRegle("uni")).toBe(false);
    for (const nom of PAPIERS.filter((papier) => papier !== "uni")) expect(estRegle(nom)).toBe(true);
  });

  it("pose l'attribut sur la racine, et le retire sans laisser de trace", () => {
    const attributs = new Map<string, string>();
    const racine = {
      setAttribute: (nom: string, valeur: string) => void attributs.set(nom, valeur),
      removeAttribute: (nom: string) => void attributs.delete(nom),
    };
    expect(poserPapier(racine, "grands-carreaux")).toBe("grands-carreaux");
    expect(attributs.get(ATTRIBUT_PAPIER)).toBe("grands-carreaux");
    poserPapier(racine, "points");
    expect(attributs.get(ATTRIBUT_PAPIER)).toBe("points");
    expect(poserPapier(racine, "inconnu")).toBeNull();
    expect(attributs.size).toBe(0);
    poserPapier(racine, "uni");
    poserPapier(racine, "");
    expect(attributs.size).toBe(0);
  });
});
