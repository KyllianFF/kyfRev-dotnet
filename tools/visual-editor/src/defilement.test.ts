// Le défilement accompagné : la même courbe et le même trajet au plus qu'en lecture.

import { describe, expect, it } from "vitest";
import { adoucir, depart, ECRANS_AU_PLUS } from "./defilement";

describe("le défilement accompagné", () => {
  it("accélère puis ralentit, de 0 à 1", () => {
    expect(adoucir(0)).toBe(0);
    expect(adoucir(1)).toBe(1);
    expect(adoucir(0.5)).toBeCloseTo(0.5);
    expect(adoucir(0.25)).toBeLessThan(0.25);
    expect(adoucir(0.75)).toBeGreaterThan(0.75);
    expect(adoucir(-1)).toBe(0);
    expect(adoucir(2)).toBe(1);
  });

  it("part d'où l'on est, ou d'un écran et demi avant la cible", () => {
    expect(depart(100, 500, 600)).toBe(100);
    expect(depart(0, 5000, 600)).toBe(5000 - 600 * ECRANS_AU_PLUS);
    expect(depart(5000, 0, 600)).toBe(600 * ECRANS_AU_PLUS);
    expect(depart(0, 10, 0)).toBe(10 - ECRANS_AU_PLUS); // une vue sans hauteur compte pour un pixel, comme en lecture
  });
});
