import { describe, expect, it } from "vitest";
import { CASSES, casseSuivante, changerCasse } from "./casse";

describe("changer la casse, comme dans Word", () => {
  it("minuscules et CAPITALES, accents compris", () => {
    expect(changerCasse("Été À L'ÉCOLE", "minuscules")).toBe("été à l'école");
    expect(changerCasse("été à l'école, œuvre", "capitales")).toBe("ÉTÉ À L'ÉCOLE, ŒUVRE");
  });

  it("la longueur ne change jamais : « ß » reste tel quel en CAPITALES", () => {
    expect(changerCasse("straße", "capitales")).toBe("STRAßE");
    for (const { casse } of CASSES) {
      const texte = "Une phrase. Puis « l'autre » ! 3.14 kg\nstraße";
      expect(changerCasse(texte, casse)).toHaveLength(texte.length);
    }
  });

  it("Majuscule en début de phrase : après « . ! ? … » et une espace, et à chaque paragraphe", () => {
    expect(changerCasse("bONJOUR. comment ÇA va ? très BIEN… merci", "phrase")).toBe(
      "Bonjour. Comment ça va ? Très bien… Merci",
    );
    expect(changerCasse("premier paragraphe\nSECOND", "phrase")).toBe("Premier paragraphe\nSecond");
    // Guillemet fermant après le point ; pas de fin de phrase sans espace.
    expect(changerCasse("il a dit « non. » puis il est parti", "phrase")).toBe(
      "Il a dit « non. » Puis il est parti",
    );
    expect(changerCasse("le fichier notes.md fait 3.14 ko", "phrase")).toBe(
      "Le fichier notes.md fait 3.14 ko",
    );
  });

  it("Première Lettre Des Mots : après une espace ou un trait d'union, pas après une apostrophe", () => {
    expect(changerCasse("JEAN-PIERRE va à l'école aujourd'hui", "mots")).toBe(
      "Jean-Pierre Va À L'école Aujourd'hui",
    );
  });

  it("une lettre sur deux, en commençant par une majuscule, sans compter les espaces", () => {
    expect(changerCasse("une lettre sur deux", "alternee")).toBe("UnE lEtTrE sUr DeUx");
    expect(changerCasse("Une lettre sur deux en majuscule et en minuscule", "alternee")).toBe(
      "UnE lEtTrE sUr DeUx En MaJuScUlE eT eN mInUsCuLe",
    );
  });

  it("inverser la casse : chaque lettre change", () => {
    expect(changerCasse("Inverser La Casse 3.14 é", "inversee")).toBe("iNVERSER lA cASSE 3.14 É");
  });

  it("Maj+F3 : minuscules → CAPITALES → Première Lettre Des Mots → Majuscule en début de phrase", () => {
    let texte = "mon titre de fiche";
    const suite: string[] = [];
    for (let i = 0; i < 5; i++) {
      texte = changerCasse(texte, casseSuivante(texte));
      suite.push(texte);
    }
    expect(suite).toEqual([
      "MON TITRE DE FICHE",
      "Mon Titre De Fiche",
      "Mon titre de fiche",
      "mon titre de fiche",
      "MON TITRE DE FICHE",
    ]);
    // Casse mêlée : on commence par « Majuscule en début de phrase ».
    expect(casseSuivante("mOn TiTrE")).toBe("phrase");
  });
});
