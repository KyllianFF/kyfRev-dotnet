// @vitest-environment node
import { describe, expect, it, vi } from "vitest";
import { EditeurSchema } from "./etat.svelte";
import { ecrireSchema, lireSchema } from "./format";
import { type Schema, schemaVide } from "./modele";
import { avecEnsembles, creerEquipement, degrouper, grouper, Refus, retirerEquipements } from "./operations";

vi.mock("../api", () => ({
  api: { enregistrerSchema: vi.fn().mockResolvedValue({ statut: "enregistre", empreinte: "e2" }) },
  messageErreur: (erreur: unknown) => (erreur instanceof Error ? erreur.message : String(erreur)),
}));

/** Trois postes, une note et un cadre. */
function plan() {
  const schema: Schema = { ...schemaVide("Réseau"), id: "s_1" };
  const a = creerEquipement(schema, "pc", { x: 0, y: 0 });
  const b = creerEquipement(schema, "pc", { x: 200, y: 0 });
  const c = creerEquipement(schema, "pc", { x: 400, y: 0 });
  schema.annotations.push({ id: "n_note", texte: "Salle 12", position: { x: 0, y: 200 } });
  schema.groupes.push({ id: "g_cadre", nom: "Salle", genre: "salle", membres: [], position: { x: -80, y: -80 }, taille: { largeur: 600, hauteur: 400 } });
  return { schema, a: a.id, b: b.id, c: c.id };
}

describe("grouper et dégrouper", () => {
  it("des éléments de toute sorte se groupent, et se choisissent alors ensemble", () => {
    const { schema, a, b, c } = plan();
    const ensemble = grouper(schema, new Set([a, "n_note", "g_cadre"]));

    expect(ensemble.membres).toEqual([a, "n_note", "g_cadre"]);
    expect(avecEnsembles(schema, [a]).sort()).toEqual([a, "n_note", "g_cadre"].sort());
    expect(avecEnsembles(schema, ["n_note", b]).sort()).toEqual([a, b, "n_note", "g_cadre"].sort());
    expect(avecEnsembles(schema, [c])).toEqual([c]);
  });

  it("il faut deux éléments ; un câble ou un inconnu ne compte pas", () => {
    const { schema, a } = plan();
    expect(() => grouper(schema, new Set([a]))).toThrow(Refus);
    expect(() => grouper(schema, new Set([a, "l_cable", "absent"]))).toThrow(Refus);
    expect(schema.ensembles).toBeUndefined();
  });

  it("un élément n'est que dans un ensemble : regrouper fond les anciens", () => {
    const { schema, a, b, c } = plan();
    grouper(schema, new Set([a, b]));
    grouper(schema, new Set([a, b, c]));
    expect(schema.ensembles).toHaveLength(1);
    expect(schema.ensembles?.[0]?.membres).toEqual([a, b, c]);

    // b part dans un autre ensemble : le premier garde a et c.
    grouper(schema, new Set([b, "n_note"]));
    expect(schema.ensembles?.map((e) => e.membres)).toEqual([[a, c], [b, "n_note"]]);
    // a le rejoint : c resterait seul, son ensemble disparaît.
    grouper(schema, new Set([a, b, "n_note"]));
    expect(schema.ensembles?.map((e) => e.membres)).toEqual([[a, b, "n_note"]]);
  });

  it("dégrouper laisse les éléments où ils sont, et la clé disparaît du fichier", () => {
    const { schema, a, b } = plan();
    grouper(schema, new Set([a, b]));
    const avant = schema.equipements.map((e) => ({ ...e.position }));

    expect(degrouper(schema, new Set([b]))).toBe(1);
    expect(schema.ensembles).toBeUndefined();
    expect(schema.equipements.map((e) => e.position)).toEqual(avant);
    expect(ecrireSchema(schema)).not.toContain("ensembles");
    expect(() => degrouper(schema, new Set([a]))).toThrow(Refus);
  });

  it("le fichier garde les ensembles, et oublie ce qui n'existe plus", () => {
    const { schema, a, b, c } = plan();
    grouper(schema, new Set([a, b, c]));
    grouper(schema, new Set(["n_note", "g_cadre"]));

    const relu = lireSchema(ecrireSchema(schema));
    expect(relu.erreurs).toEqual([]);
    expect(relu.schema.ensembles?.map((e) => e.membres)).toEqual([[a, b, c], ["n_note", "g_cadre"]]);

    // Un équipement retiré quitte son ensemble ; une note retirée défait le sien, réduit à un cadre.
    retirerEquipements(schema, new Set([c]));
    schema.annotations = [];
    const apres = lireSchema(ecrireSchema(schema)).schema;
    expect(apres.ensembles?.map((e) => e.membres)).toEqual([[a, b]]);
    expect(avecEnsembles(schema, ["g_cadre"])).toEqual(["g_cadre"]);

    // Un fichier écrit à la main : doublons et membres déjà pris sont écartés.
    const brut = JSON.parse(ecrireSchema(schema));
    brut.ensembles = [{ id: "e_1", membres: [a, a, b] }, { id: "e_2", membres: [a, "g_cadre"] }, "n'importe quoi"];
    expect(lireSchema(brut).schema.ensembles).toEqual([{ id: "e_1", membres: [a, b] }]);
  });
});

describe("la sélection suit les ensembles", () => {
  function editeurAvec() {
    const { schema, a, b, c } = plan();
    const editeur = new EditeurSchema();
    editeur.schema = schema;
    editeur.id = "s_1";
    return { editeur, a, b, c };
  }

  it("Ctrl+G groupe la sélection, un clic sur un membre choisit tout, Ctrl+Maj+G défait", () => {
    const { editeur, a, b, c } = editeurAvec();
    expect(editeur.groupement.peutGrouper).toBe(false);

    editeur.selection = [a, "n_note"];
    expect(editeur.groupement).toMatchObject({ peutGrouper: true, peutDegrouper: false, unSeul: false });
    editeur.grouper();
    expect(editeur.selection).toEqual([a, "n_note"]);
    expect(editeur.groupement).toMatchObject({ peutGrouper: false, peutDegrouper: true, unSeul: true });

    editeur.choisir(c);
    expect(editeur.selection).toEqual([c]);
    editeur.choisir("n_note", true);
    expect([...editeur.selection].sort()).toEqual([a, c, "n_note"].sort());
    expect(editeur.groupement).toMatchObject({ peutGrouper: true, peutDegrouper: true, unSeul: false });
    // Ctrl+clic sur un membre déjà choisi retire tout son ensemble.
    editeur.choisir(a, true);
    expect(editeur.selection).toEqual([c]);

    editeur.choisir(a);
    expect(editeur.estGroupe(a)).toBe(true);
    expect(editeur.estGroupe(b)).toBe(false);
    editeur.degrouper();
    expect(editeur.schema.ensembles).toBeUndefined();
    editeur.choisir(a);
    expect(editeur.selection).toEqual([a]);
  });

  it("grouper s'annule, et un refus ne laisse aucune trace", () => {
    const { editeur, a, b } = editeurAvec();
    editeur.selection = [a];
    editeur.grouper();
    expect(editeur.peutAnnuler).toBe(false);
    expect(editeur.annonce).toContain("au moins deux");

    editeur.selection = [a, b];
    editeur.grouper();
    expect(editeur.schema.ensembles).toHaveLength(1);
    editeur.annuler();
    expect(editeur.schema.ensembles).toBeUndefined();
    editeur.retablir();
    expect(editeur.schema.ensembles?.[0]?.membres).toEqual([a, b]);
  });
});
