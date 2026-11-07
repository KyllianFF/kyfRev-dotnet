// @vitest-environment node
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ResultatEnregistrement } from "../generes/ResultatEnregistrement";
import { EditeurSchema } from "./etat.svelte";
import { schemaVide } from "./modele";

const { enregistrerSchema } = vi.hoisted(() => ({
  enregistrerSchema: vi.fn<(id: string, texte: string, empreinte: string) => Promise<ResultatEnregistrement>>(),
}));

vi.mock("../api", () => ({
  api: { enregistrerSchema },
  messageErreur: (erreur: unknown) => (erreur instanceof Error ? erreur.message : String(erreur)),
}));

/** Un schéma ouvert, déjà enregistré. */
function ouvert() {
  const editeur = new EditeurSchema();
  editeur.schema = { ...schemaVide("Réseau"), id: "s_1" };
  editeur.id = "s_1";
  editeur.empreinte = "e1";
  return editeur;
}

const ENREGISTRE: ResultatEnregistrement = { statut: "enregistre", empreinte: "e2" };

beforeEach(() => {
  enregistrerSchema.mockReset();
});

describe("finir avant le verrouillage", () => {
  it("rien n'attendait : rien ne s'écrit", async () => {
    expect(await ouvert().finir()).toBe("enregistre");
    expect(enregistrerSchema).not.toHaveBeenCalled();
  });

  it("ce qui attendait s'enregistre tout de suite, sans attendre le délai", async () => {
    enregistrerSchema.mockResolvedValue(ENREGISTRE);
    const editeur = ouvert();
    editeur.agir((s) => {
      s.titre = "Lycée";
    });
    expect(editeur.sauvegarde).toBe("modifie");

    expect(await editeur.finir()).toBe("enregistre");

    expect(enregistrerSchema).toHaveBeenCalledOnce();
    expect(enregistrerSchema.mock.calls[0]?.[1]).toContain("Lycée");
    expect(editeur.empreinte).toBe("e2");
  });

  it("une modification arrivée pendant l'écriture part à son tour", async () => {
    const editeur = ouvert();
    enregistrerSchema.mockImplementationOnce(async () => {
      editeur.agir((s) => {
        s.titre = "Lycée, bâtiment B";
      });
      return ENREGISTRE;
    });
    enregistrerSchema.mockResolvedValueOnce({ statut: "enregistre", empreinte: "e3" });
    editeur.agir((s) => {
      s.titre = "Lycée";
    });

    expect(await editeur.finir()).toBe("enregistre");

    expect(enregistrerSchema).toHaveBeenCalledTimes(2);
    expect(enregistrerSchema.mock.calls[1]?.[1]).toContain("bâtiment B");
  });

  it("un enregistrement refusé est retenté ; refusé encore, il le dit", async () => {
    const editeur = ouvert();
    enregistrerSchema.mockRejectedValueOnce(new Error("disque plein"));
    editeur.agir((s) => {
      s.titre = "Lycée";
    });
    await editeur.enregistrer();
    expect(editeur.sauvegarde).toBe("erreur");

    enregistrerSchema.mockResolvedValueOnce(ENREGISTRE);
    expect(await editeur.finir()).toBe("enregistre");

    editeur.agir((s) => {
      s.titre = "Lycée, bâtiment B";
    });
    enregistrerSchema.mockRejectedValue(new Error("disque plein"));
    expect(await editeur.finir()).toBe("erreur");
  });

  it("un schéma modifié ailleurs attend le choix de l'utilisateur", async () => {
    const editeur = ouvert();
    enregistrerSchema.mockResolvedValue({ statut: "conflit", empreinteDisque: "e9", corpsDisque: "{}" });
    editeur.agir((s) => {
      s.titre = "Lycée";
    });

    expect(await editeur.finir()).toBe("conflit");
    expect(await editeur.finir()).toBe("conflit");
    expect(enregistrerSchema).toHaveBeenCalledOnce();
  });
});
