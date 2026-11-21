// L'entrée de la feuille : la durée que l'application donne à la page.

import { describe, expect, it } from "vitest";
import { dureeEnMs } from "./entree";

describe("la durée d'entrée de la feuille", () => {
  it("se lit en millisecondes ou en secondes", () => {
    expect(dureeEnMs("220ms")).toBe(220);
    expect(dureeEnMs(" 220ms ")).toBe(220);
    expect(dureeEnMs("0.22s")).toBeCloseTo(220);
  });

  it("est nulle quand les animations sont coupées, ou quand elle manque", () => {
    expect(dureeEnMs("0ms")).toBe(0);
    expect(dureeEnMs("")).toBe(0);
    expect(dureeEnMs("vite")).toBe(0);
    expect(dureeEnMs("-220ms")).toBe(0);
  });
});
