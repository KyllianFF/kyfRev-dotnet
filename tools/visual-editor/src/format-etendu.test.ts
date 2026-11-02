// Le format étendu : l'éditeur et la lecture native (KyfRev.Rendering) lisent les mêmes
// cas, tirés du même fichier. Une écriture reconnue d'un côté l'est aussi de l'autre.

import { describe, expect, it } from "vitest";
import cas from "../../../tests/fixtures/format-etendu/cas.json";
import { depuisMarkdown, versMarkdown } from "./markdown";

type Cas = {
  source: string;
  colors?: string[];
  sizes?: number[];
  aligns?: string[];
  tasks?: (boolean | null)[];
};

function lire(source: string) {
  const vu = {
    colors: [] as string[],
    sizes: [] as number[],
    aligns: [] as string[],
    tasks: [] as (boolean | null)[],
  };
  depuisMarkdown(`${source}\n`).doc.descendants((noeud) => {
    if (noeud.type.name === "alignement") vu.aligns.push(noeud.attrs.align as string);
    if (noeud.type.name === "list_item") vu.tasks.push(noeud.attrs.tache as boolean | null);
    for (const marque of noeud.marks) {
      if (marque.type.name === "couleur") vu.colors.push(marque.attrs.valeur as string);
      if (marque.type.name === "taille") vu.sizes.push(Number.parseFloat(marque.attrs.valeur as string));
    }
  });
  return vu;
}

describe("le format étendu, comme la lecture native", () => {
  it.each((cas as Cas[]).map((un) => [un.source, un] as const))("%s", (source, attendu) => {
    expect(lire(source)).toEqual({
      colors: attendu.colors ?? [],
      sizes: attendu.sizes ?? [],
      aligns: attendu.aligns ?? [],
      tasks: attendu.tasks ?? [],
    });
  });

  it.each((cas as Cas[]).map((un) => [un.source] as const))("%s : réécrit, il se relit pareil", (source) => {
    const doc = depuisMarkdown(`${source}\n`).doc;
    expect(depuisMarkdown(versMarkdown(doc, null)).doc.toJSON()).toEqual(doc.toJSON());
  });

  it("une fiche piégée qui ouvre des passages sans fin reste du texte, et ne bloque pas", () => {
    const piege = '<div align="center">\n\n'.repeat(200_000);
    expect(depuisMarkdown(piege).doc.childCount).toBeGreaterThan(0);
    const emboite = `${'<div align="center">\n\n'.repeat(300)}a\n\n${"</div>\n\n".repeat(300)}`;
    expect(depuisMarkdown(emboite).doc.childCount).toBeGreaterThan(0);
  });
});
