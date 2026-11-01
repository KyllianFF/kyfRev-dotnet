import { describe, expect, it } from "vitest";
import { colorer, langageConnu, langages } from "./coloration";

describe("coloration du code", () => {
  it("les morceaux colorés retombent exactement sur le texte d'origine", () => {
    const code = "def aire(r):\n    return 3.14 * r ** 2  # « disque » & <cercle>\n";
    const morceaux = colorer(code, "python");
    expect(morceaux.length).toBeGreaterThan(2);
    const motCle = morceaux.find((m) => code.slice(m.debut, m.fin) === "def");
    expect(motCle?.classes).toContain("hljs-keyword");
    const commentaire = morceaux.find((m) => m.classes.includes("hljs-comment"));
    expect(code.slice(commentaire?.debut, commentaire?.fin)).toBe("# « disque » & <cercle>");
    for (const m of morceaux) expect(m.fin).toBeLessThanOrEqual(code.length);
  });

  it("langage inconnu, alias et liste proposée", () => {
    expect(colorer("show running-config", "cisco")).toEqual([]);
    expect(langageConnu("js")).toBe(true);
    expect(langageConnu("")).toBe(false);
    const noms = langages().map((l) => l.nom);
    expect(noms).toContain("Python");
    expect(noms).toContain("C#");
    expect(noms).not.toContain("Texte");
  });
});
