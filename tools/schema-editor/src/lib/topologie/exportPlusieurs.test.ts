import { beforeEach, describe, expect, it, vi } from "vitest";

const appels = vi.hoisted(() => ({ dossier: "C:/Exports" as string | null, ecrits: [] as string[] }));
vi.mock("../api", () => ({
  api: {
    choisirDossier: async () => appels.dossier,
    exporterSchema: async (dossier: string, nom: string) => {
      appels.ecrits.push(`${dossier}/${nom}`);
      return `${dossier}/${nom}`;
    },
  },
}));

const { exporterSchemas } = await import("./sorties");
const { schemaVide } = await import("./modele");

describe("exporter plusieurs schémas (31B)", () => {
  beforeEach(() => {
    appels.dossier = "C:/Exports";
    appels.ecrits = [];
  });

  it("écrit un fichier par schéma dans le dossier choisi une fois", async () => {
    const resultat = await exporterSchemas([schemaVide("Lycée"), schemaVide("Labo")], "json");
    expect(resultat).toEqual({ dossier: "C:/Exports", ecrits: 2, sautes: 0 });
    expect(appels.ecrits).toEqual(["C:/Exports/Lycée.json", "C:/Exports/Labo.json"]);
  });

  it("saute un schéma vide en image, et n'écrit rien si le dossier n'est pas choisi", async () => {
    expect(await exporterSchemas([schemaVide("Vide")], "png")).toEqual({ dossier: "C:/Exports", ecrits: 0, sautes: 1 });
    appels.dossier = null;
    expect(await exporterSchemas([schemaVide("Lycée")], "json")).toBeNull();
    expect(appels.ecrits).toEqual([]);
  });
});
