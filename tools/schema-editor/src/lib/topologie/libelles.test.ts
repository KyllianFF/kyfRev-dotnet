import { describe, expect, it } from "vitest";
import { mettreEnPage } from "./dessin";
import { ecrireSchema, lireSchema } from "./format";
import { type AffichageLibelles, schemaVide } from "./modele";
import { creerEquipement } from "./operations";
import { tirerCable } from "./simplifie";
import { vue } from "./vues";

/** Deux switches reliés par un câble nommé ; le schéma ne montre ni ports ni noms de câbles. */
function site(libelles?: AffichageLibelles) {
  const schema = schemaVide("Libellés");
  schema.affichage = { ports: false, nomsCables: false };
  const a = creerEquipement(schema, "switch", { x: 0, y: 0 });
  const b = creerEquipement(schema, "switch", { x: 400, y: 0 });
  const lien = tirerCable(schema, a.id, b.id);
  lien.nom = "Uplink";
  if (libelles) lien.libelles = libelles;
  return { schema, lien };
}

const genres = (schema: ReturnType<typeof site>["schema"], id: string) =>
  (mettreEnPage(schema, vue("physique"), new Set()).etiquettes.get(id) ?? []).map((e) => e.genre);

describe("ce qu'un câble écrit sur lui-même", () => {
  it("chaque choix du volet survit à l'enregistrement, « Ports » compris", () => {
    for (const choix of ["aucun", "ports", "nom", "tout"] as const) {
      const { schema, lien } = site(choix);
      const relu = lireSchema(ecrireSchema(schema)).schema;
      expect(relu.liens.find((l) => l.id === lien.id)?.libelles).toBe(choix);
    }
  });

  it("« Ports » relu : les pastilles des ports, sans le nom, même si le schéma ne montre pas les ports", () => {
    const { schema, lien } = site("ports");
    const relu = lireSchema(ecrireSchema(schema)).schema;
    expect(genres(relu, lien.id)).toEqual(["port", "port"]);
  });

  it("une valeur inconnue est écartée : le câble suit le schéma", () => {
    const { schema, lien } = site();
    const brut = JSON.parse(ecrireSchema(schema));
    brut.liens[0].libelles = "partout";
    const lu = lireSchema(brut).schema;
    expect(lu.liens[0]?.libelles).toBeUndefined();
    expect(genres(lu, lien.id)).toEqual([]);
  });
});
