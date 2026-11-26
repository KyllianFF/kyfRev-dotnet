import { describe, expect, it } from "vitest";
import { copierConfiguration } from "./configuration";
import { entreesEquipement } from "./menuEquipement";
import { schemaVide } from "./modele";
import { creerEquipement } from "./operations";

function lycee() {
  const schema = schemaVide("Lycée");
  const sw1 = creerEquipement(schema, "switch", { x: 0, y: 0 });
  const sw2 = creerEquipement(schema, "switch", { x: 300, y: 0 });
  const hote = creerEquipement(schema, "hyperviseur", { x: 600, y: 0 });
  return { schema, sw1, sw2, hote };
}

const noms = (entrees: ReturnType<typeof entreesEquipement>) =>
  entrees.map((e) => `${e.apresUnTrait ? "— " : ""}${e.nom}${e.desactivee ? " (grisé)" : ""}`);

describe("Le menu d'un équipement", () => {
  it("donne les entrées de « ⋯ » dans l'ordre : Connecter, Dupliquer, Copier, la place, la configuration, puis Retirer à part", () => {
    const { schema, sw1 } = lycee();
    expect(noms(entreesEquipement(schema, [sw1.id], { simplifie: false, copiee: null }))).toEqual([
      "Connecter…",
      "Dupliquer",
      "Copier",
      "Verrouiller la place",
      "— Copier la configuration",
      "Coller sur… (grisé)",
      "— Retirer…",
    ]);
    const retirer = entreesEquipement(schema, [sw1.id], { simplifie: false, copiee: null }).at(-1);
    expect(retirer).toMatchObject({ action: "retirer", danger: true });
  });

  it("ajoute Replier et Remettre le nom quand ils s'appliquent, et suit l'état de l'équipement", () => {
    const { schema, hote } = lycee();
    hote.verrou = true;
    hote.replie = true;
    hote.pose = { angle: 30 };
    const entrees = noms(entreesEquipement(schema, [hote.id], { simplifie: false, copiee: null }));
    expect(entrees.slice(3, 6)).toEqual(["Déverrouiller la place", "Déplier ce qu'il héberge", "Remettre le nom à sa place"]);
  });

  it("propose « Coller sur… » dès qu'une configuration est copiée", () => {
    const { schema, sw1, sw2 } = lycee();
    const copiee = copierConfiguration(schema, sw1.id);
    const coller = entreesEquipement(schema, [sw2.id], { simplifie: false, copiee }).find((e) => e.action === "coller-configuration");
    expect(coller?.desactivee).toBe(false);
    expect(coller?.titre).toContain(sw1.nom);
  });

  it("en mode simplifié, garde ce qui y a un sens : ni connexion par port, ni configuration des ports", () => {
    const { schema, sw1 } = lycee();
    expect(noms(entreesEquipement(schema, [sw1.id], { simplifie: true, copiee: copierConfiguration(schema, sw1.id) }))).toEqual([
      "Dupliquer",
      "Copier",
      "Verrouiller la place",
      "— Retirer…",
    ]);
  });

  it("avec plusieurs équipements, n'agit que sur ce qui a un sens pour tous, la configuration copiée comprise", () => {
    const { schema, sw1, sw2, hote } = lycee();
    const ids = [sw1.id, sw2.id, hote.id];
    expect(noms(entreesEquipement(schema, ids, { simplifie: false, copiee: null }))).toEqual(["Dupliquer", "Copier", "— Retirer…"]);
    const copiee = copierConfiguration(schema, sw1.id);
    expect(noms(entreesEquipement(schema, ids, { simplifie: false, copiee }))).toEqual([
      "Dupliquer",
      "Copier",
      `— Coller la configuration de « ${sw1.nom} »`,
      "— Retirer…",
    ]);
    expect(noms(entreesEquipement(schema, ids, { simplifie: true, copiee }))).toEqual(["Dupliquer", "Copier", "— Retirer…"]);
    // Ce qui n'est plus dans le schéma ne compte pas.
    expect(entreesEquipement(schema, ["absent"], { simplifie: false, copiee })).toEqual([]);
  });
});
