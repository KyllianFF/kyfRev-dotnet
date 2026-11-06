import { describe, expect, it } from "vitest";
import { largeursMinimales } from "./mots-entiers";

describe("les mots entiers dans les tableaux", () => {
  it("donnent à chaque colonne la largeur de son plus long mot, quand le tableau tient", () => {
    expect(largeursMinimales([[80, 60.2], [70, 90.4]], 200)).toEqual([80, 91]);
    expect(largeursMinimales([[80, 60], [70]], 140)).toEqual([80, 60]);
  });

  it("n'imposent rien quand le tableau ne tiendrait pas : les mots se coupent plutôt que la page ne défile", () => {
    expect(largeursMinimales([[120, 100]], 200)).toBeNull();
    expect(largeursMinimales([], 200)).toBeNull();
  });
});
