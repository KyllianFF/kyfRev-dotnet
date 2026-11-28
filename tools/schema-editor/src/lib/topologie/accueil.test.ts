import { describe, expect, it } from "vitest";
import {
  basculer,
  type CarteSchema,
  compterEtiquettes,
  etendre,
  etiqueter,
  etiquetteNette,
  filtrer,
  garderPresents,
  quand,
  sansAccents,
  sections,
  sensNaturel,
  trier,
} from "./accueil";
import { ecrireSchema, lireSchema } from "./format";
import { schemaVide } from "./modele";

const carte = (id: string, titre: string, plus: Partial<CarteSchema> = {}): CarteSchema => ({
  id,
  titre,
  equipements: 0,
  pages: 1,
  points: 0,
  fiches: 0,
  modifieLe: null,
  etiquettes: [],
  epingle: false,
  texte: sansAccents(titre),
  ...plus,
});

const lycee = carte("a", "Réseau du lycée", {
  equipements: 24,
  pages: 4,
  fiches: 3,
  modifieLe: "2026-11-27T10:00:00Z",
  etiquettes: ["Lycée"],
  epingle: true,
});
const sames = carte("b", "SAMES Meylan", { equipements: 9, pages: 3, points: 1, modifieLe: "2026-11-26T10:00:00Z", etiquettes: ["Client"] });
const labo = carte("c", "Labo réseau (TP 3)", { equipements: 7, modifieLe: "2026-11-20T10:00:00Z", etiquettes: ["TP", "lycée"] });
const vide = carte("d", "Brouillon");
const toutes = [lycee, sames, labo, vide];

describe("accueil des schémas (31A, 31B)", () => {
  it("compte les étiquettes, à la casse près, de la plus portée à la moins portée", () => {
    expect(compterEtiquettes(toutes)).toEqual([
      { nom: "Lycée", nombre: 2 },
      { nom: "Client", nombre: 1 },
      { nom: "TP", nombre: 1 },
    ]);
    // L'écriture gardée ne dépend pas de l'ordre : la plus courante, sinon celle qui commence par une majuscule.
    expect(compterEtiquettes([labo, lycee, sames, { ...sames, id: "e" }])).toEqual([
      { nom: "Client", nombre: 2 },
      { nom: "Lycée", nombre: 2 },
      { nom: "TP", nombre: 1 },
    ]);
    expect(compterEtiquettes([labo, { ...labo, id: "f" }, lycee])[0]).toEqual({ nom: "lycée", nombre: 3 });
  });

  it("filtre par la recherche, sans accents, par étiquette (l'une ou l'autre) et par le diagnostic", () => {
    const sans = { recherche: "", etiquettes: [], aVerifier: false };
    expect(filtrer(toutes, { ...sans, recherche: "RESEAU lycee" }).map((c) => c.id)).toEqual(["a"]);
    expect(filtrer(toutes, { ...sans, etiquettes: ["lycée"] }).map((c) => c.id)).toEqual(["a", "c"]);
    expect(filtrer(toutes, { ...sans, etiquettes: ["Client", "TP"] }).map((c) => c.id)).toEqual(["b", "c"]);
    expect(filtrer(toutes, { ...sans, aVerifier: true }).map((c) => c.id)).toEqual(["b"]);
    expect(filtrer(toutes, { ...sans, etiquettes: ["Lycée"], aVerifier: true })).toEqual([]);
    expect(filtrer(toutes, sans)).toHaveLength(4);
  });

  it("trie par date, nom, taille et chaque colonne, sans date et sans étiquette à la fin", () => {
    const ids = (cle: Parameters<typeof trier>[1]["cle"], sens: "asc" | "desc" = sensNaturel(cle)) =>
      trier(toutes, { cle, sens }).map((c) => c.id).join("");
    expect(ids("modifie")).toBe("abcd");
    expect(ids("modifie", "asc")).toBe("cbad");
    expect(ids("nom")).toBe("dcab");
    expect(ids("nom", "desc")).toBe("bacd");
    expect(ids("equipements")).toBe("abcd");
    expect(ids("pages")).toBe("abdc");
    expect(ids("points")).toBe("bdca");
    expect(ids("fiches")).toBe("adcb");
    expect(ids("etiquettes")).toBe("bacd");
    expect(ids("etiquettes", "desc")).toBe("cabd");
    expect(sensNaturel("nom")).toBe("asc");
    expect(sensNaturel("modifie")).toBe("desc");
  });

  it("met les épinglés en tête, une seule fois", () => {
    const { epingles, autres } = sections(toutes, { cle: "nom", sens: "asc" });
    expect(epingles.map((c) => c.id)).toEqual(["a"]);
    expect(autres.map((c) => c.id)).toEqual(["d", "c", "b"]);
  });

  it("choisit au Ctrl+clic, prend une suite au Maj+clic, et oublie ce qui n'est plus là", () => {
    const ordre = ["a", "b", "c", "d"];
    expect(basculer([], "b")).toEqual(["b"]);
    expect(basculer(["b", "c"], "b")).toEqual(["c"]);
    expect(etendre(ordre, ["b"], "b", "d")).toEqual(["b", "c", "d"]);
    expect(etendre(ordre, ["d"], "d", "a")).toEqual(["d", "a", "b", "c"]);
    expect(etendre(ordre, [], null, "c")).toEqual(["c"]);
    expect(etendre(ordre, ["c"], "z", "c")).toEqual(["c"]);
    expect(etendre(ordre, ["a"], "a", "z")).toEqual(["a"]);
    expect(garderPresents(["a", "z", "c"], ordre)).toEqual(["a", "c"]);
  });

  it("dit quand un schéma a changé, en mots courts", () => {
    const maintenant = new Date(2026, 10, 28, 9, 30);
    expect(quand(new Date(2026, 10, 28, 8).toISOString(), maintenant)).toBe("aujourd'hui");
    expect(quand(new Date(2026, 10, 27, 23).toISOString(), maintenant)).toBe("hier");
    expect(quand(new Date(2026, 10, 26, 12).toISOString(), maintenant)).toBe("le 26 nov.");
    expect(quand(new Date(2025, 10, 26, 12).toISOString(), maintenant)).toBe("le 26 nov. 2025");
    expect(quand("pas une date", maintenant)).toBe("");
  });

  it("nettoie une étiquette saisie et en ajoute ou retire sans doublon", () => {
    expect(etiquetteNette("  Salle   B012 ")).toBe("Salle B012");
    expect(etiquetteNette("   ")).toBe("");
    expect(etiquetteNette("x".repeat(60))).toHaveLength(40);
    expect(etiqueter(["Lycée", "TP"], ["client", "lycée"], ["tp"])).toEqual(["Lycée", "client"]);
    expect(etiqueter(["TP"], [], ["TP"])).toBeUndefined();
    expect(etiqueter(undefined, ["Client"], [])).toEqual(["Client"]);
  });
});

describe("étiquettes et épingle dans le schéma (31E)", () => {
  it("se lisent, bornées et sans doublon, et s'écrivent à la fin", () => {
    const { schema } = lireSchema({
      ...schemaVide("Réseau"),
      etiquettes: [" Lycée ", "lycée", 3, "", "Client\u0007", "x".repeat(50)],
      epingle: true,
    });
    expect(schema.etiquettes).toEqual(["Lycée", "Client", "x".repeat(40)]);
    expect(schema.epingle).toBe(true);
    const ecrit = ecrireSchema(schema);
    expect(ecrit.indexOf('"etiquettes"')).toBeGreaterThan(ecrit.indexOf('"vue"'));
    expect(lireSchema(ecrit).schema).toEqual(schema);
  });

  it("un schéma sans étiquette ni épingle ne change pas d'un octet", () => {
    const avant = ecrireSchema(schemaVide("Réseau"));
    expect(avant).not.toContain("etiquettes");
    expect(avant).not.toContain("epingle");
    expect(ecrireSchema({ ...schemaVide("Réseau"), etiquettes: [] })).toBe(avant);
    expect(ecrireSchema(lireSchema({ ...schemaVide("Réseau"), etiquettes: "Lycée", epingle: "oui" }).schema)).toBe(avant);
    expect(ecrireSchema(lireSchema(avant).schema)).toBe(avant);
  });

  it("garde au plus vingt étiquettes", () => {
    const { schema } = lireSchema({ ...schemaVide(), etiquettes: Array.from({ length: 30 }, (_, i) => `É${i}`) });
    expect(schema.etiquettes).toHaveLength(20);
  });
});
