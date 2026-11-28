// @vitest-environment node
// Les modèles de schéma (31E) : les modèles intégrés sont de vrais schémas, et un schéma créé d'un modèle (ou un
// modèle tiré d'un schéma, ou d'une de ses pages) ne partage aucun identifiant avec sa source.
import { describe, expect, it } from "vitest";
import { typeConnu } from "./catalogue";
import { cadreDuSchema, dessinerSchema } from "./dessin";
import { diagnostiquer } from "./diagnostic";
import { ecrireSchema, erreursStructurelles, lireSchema } from "./format";
import { avecIdentifiantsNeufs, MODELES_INTEGRES, modeleDepuisSchema, modelesIntegres, schemaDepuisModele } from "./modelesDeSchema";
import { interfaceDe, type Schema } from "./modele";
import { ajouterPage } from "./pages";

/** Tous les identifiants d'un schéma (les interfaces, avec leur équipement). */
function identifiants(s: Schema): string[] {
  return [
    ...(s.pages ?? []).map((p) => p.id),
    ...s.modeles.map((m) => m.id),
    ...s.equipements.flatMap((e) => [e.id, ...e.interfaces.map((i) => i.id)]),
    ...s.liens.map((l) => l.id),
    ...s.relations.map((r) => r.id),
    ...s.vlans.map((v) => v.id),
    ...s.agregats.map((a) => a.id),
    ...s.groupes.map((g) => g.id),
    ...s.annotations.map((n) => n.id),
    ...(s.ensembles ?? []).map((e) => e.id),
  ];
}

describe("modèles intégrés", () => {
  it("sont les cinq de la maquette, dans l'ordre", () => {
    expect(MODELES_INTEGRES.map((m) => m.nom)).toEqual(["Petit bureau", "Étage type", "Site distant", "Lab TP", "Datacenter"]);
    expect(modelesIntegres().every((m) => m.integre && m.description.length > 0)).toBe(true);
  });

  for (const modele of MODELES_INTEGRES) {
    it(`« ${modele.nom} » est un schéma valide, câblé et sans avertissement`, () => {
      const schema = modele.construire();
      // Il se relit tel quel, sans erreur de structure ni avertissement : seuls des renseignements peuvent manquer.
      const relu = lireSchema(ecrireSchema(schema));
      expect(relu.erreurs).toEqual([]);
      expect(diagnostiquer(relu.schema).filter((c) => c.niveau !== "manque")).toEqual([]);
      // De vrais types du catalogue, des câbles de port à port, et chaque équipement relié.
      expect(schema.equipements.every((e) => typeConnu(e.type))).toBe(true);
      expect(schema.liens.length).toBeGreaterThanOrEqual(schema.equipements.length - 2);
      for (const l of schema.liens) {
        expect(interfaceDe(schema, l.a)).toBeDefined();
        expect(interfaceDe(schema, l.b)).toBeDefined();
      }
      const relies = new Set([...schema.liens.flatMap((l) => [l.a.equipement, l.b.equipement]), ...schema.relations.flatMap((r) => [r.de, r.vers])]);
      expect(schema.equipements.filter((e) => !relies.has(e.id)).map((e) => e.nom)).toEqual([]);
      // Les VLAN déclarés sont ceux des ports.
      const portes = new Set(
        schema.equipements.flatMap((e) =>
          e.interfaces.flatMap((i) => [i.vlan?.acces, i.vlan?.natif, ...(i.vlan?.tagues ?? "").split(",").map(Number)]),
        ),
      );
      for (const v of schema.vlans) expect(portes.has(v.numero), `VLAN ${v.numero}`).toBe(true);
      // Mise en page : deux équipements sont à 150 points l'un de l'autre en largeur, ou à 140 en hauteur (la place du nom et de l'adresse).
      for (const [rang, e] of schema.equipements.entries()) {
        for (const f of schema.equipements.slice(rang + 1)) {
          expect(Math.abs(e.position.x - f.position.x) >= 150 || Math.abs(e.position.y - f.position.y) >= 140, `${e.nom} et ${f.nom}`).toBe(true);
        }
      }
      const cadre = cadreDuSchema(schema);
      expect(cadre.largeur).toBeLessThan(1400);
      expect(cadre.hauteur).toBeLessThan(900);
      expect(dessinerSchema(schema)).toContain("<svg");
    });
  }

  it("ont des adresses et des VLAN plausibles", () => {
    const [bureau, etage, , lab, datacenter] = MODELES_INTEGRES.map((m) => m.construire()) as Schema[];
    expect(bureau?.vlans).toEqual([]);
    expect(etage?.vlans.map((v) => v.numero)).toEqual([10, 20, 30, 99]);
    expect(lab?.vlans.map((v) => `${v.numero} ${v.nom}`)).toEqual(["10 Postes", "20 Serveurs"]);
    expect(datacenter?.relations.some((r) => r.genre === "haute-disponibilite")).toBe(true);
    const adresses = bureau?.equipements.flatMap((e) => e.interfaces.flatMap((i) => i.adresses.map((a) => a.adresse))) ?? [];
    expect(adresses).toContain("192.168.1.11");
    expect(adresses.every((a) => a.startsWith("192.168.1."))).toBe(true);
  });
});

describe("créer depuis un modèle", () => {
  it("refait chaque identifiant et suit chaque référence", () => {
    const source = MODELES_INTEGRES[4]?.construire() as Schema;
    const cree = schemaDepuisModele(source, "  Salle machines  ");
    expect(cree.titre).toBe("Salle machines");
    expect(cree.id).toBe("");
    expect(cree.description).toBeUndefined();
    const avant = new Set(identifiants(source));
    expect(identifiants(cree).filter((id) => avant.has(id))).toEqual([]);
    expect(erreursStructurelles(cree)).toEqual([]);
    expect(diagnostiquer(cree).filter((c) => c.niveau !== "manque")).toEqual([]);
    // Les relations, les agrégats et les ports suivent.
    expect(cree.relations).toHaveLength(source.relations.length);
    expect(cree.liens.map((l) => `${interfaceDe(cree, l.a)?.nom}-${interfaceDe(cree, l.b)?.nom}`)).toEqual(
      source.liens.map((l) => `${interfaceDe(source, l.a)?.nom}-${interfaceDe(source, l.b)?.nom}`),
    );
    // Un titre vide garde celui du modèle ; deux créations ne se ressemblent pas.
    expect(schemaDepuisModele(source, " ").titre).toBe("Datacenter");
    expect(schemaDepuisModele(source, "a").equipements[0]?.id).not.toBe(cree.equipements[0]?.id);
  });

  it("refait aussi les pages, les cadres, les ensembles, les agrégats et les modèles matériels", () => {
    const s: Schema = lireSchema({
      format: 3,
      titre: "Deux pages",
      pages: [
        { id: "p_1", nom: "Étage 1" },
        { id: "p_2", nom: "Étage 2" },
      ],
      modeles: [{ id: "m_1", type: "switch", reference: "X", groupes: [] }],
      equipements: [
        { id: "d_1", page: "p_1", type: "switch", nom: "SW", membres: [{ numero: 1, modele: "m_1" }], position: { x: 0, y: 0 }, gestion: { interface: "i_1", adresse: "10.0.0.1/24" }, interfaces: [{ id: "i_1", nom: "1/1" }, { id: "i_2", nom: "1/1.10", parent: "i_1" }] },
        { id: "d_2", page: "p_2", type: "pc", nom: "PC", position: { x: 0, y: 200 }, interfaces: [{ id: "i_1", nom: "eth0" }] },
      ],
      liens: [{ id: "l_1", a: { equipement: "d_1", interface: "i_1" }, b: { equipement: "d_2", interface: "i_1" }, support: "cuivre", points: [] }],
      relations: [{ id: "r_1", genre: "dependance", de: "d_2", vers: "d_1", deInterface: "i_1", versInterface: "i_2" }],
      agregats: [{ id: "a_1", equipement: "d_1", nom: "Trk1", mode: "lacp", membres: ["i_1"] }],
      groupes: [{ id: "g_1", page: "p_1", nom: "Baie", genre: "groupe", membres: ["d_1"], position: { x: -50, y: -50 }, taille: { largeur: 100, hauteur: 100 } }],
      annotations: [{ id: "n_1", page: "p_1", texte: "Note", position: { x: 0, y: 0 } }],
      ensembles: [{ id: "e_1", membres: ["d_1", "n_1"] }],
    }).schema;
    const cree = schemaDepuisModele(s, "Copie");
    const avant = new Set(identifiants(s));
    expect(identifiants(cree).filter((id) => avant.has(id))).toEqual([]);
    expect(erreursStructurelles(cree)).toEqual([]);
    const [sw, pc] = cree.equipements;
    expect(sw?.page).toBe(cree.pages?.[0]?.id);
    expect(pc?.page).toBe(cree.pages?.[1]?.id);
    expect(sw?.membres[0]?.modele).toBe(cree.modeles[0]?.id);
    expect(sw?.gestion?.interface).toBe(sw?.interfaces[0]?.id);
    expect(sw?.interfaces[1]?.parent).toBe(sw?.interfaces[0]?.id);
    expect(cree.relations[0]).toMatchObject({ de: pc?.id, vers: sw?.id, deInterface: pc?.interfaces[0]?.id, versInterface: sw?.interfaces[1]?.id });
    expect(cree.agregats[0]).toMatchObject({ equipement: sw?.id, membres: [sw?.interfaces[0]?.id] });
    expect(cree.groupes[0]).toMatchObject({ page: cree.pages?.[0]?.id, membres: [sw?.id] });
    expect(cree.annotations[0]?.page).toBe(cree.pages?.[0]?.id);
    expect(cree.ensembles?.[0]?.membres).toEqual([sw?.id, cree.annotations[0]?.id]);
    // Le schéma reçu n'a pas bougé.
    expect(s.equipements[0]?.id).toBe("d_1");
    expect(avecIdentifiantsNeufs(s).id).toBe("");
  });
});

describe("enregistrer comme modèle", () => {
  it("garde un schéma entier, sous le nom et la description du modèle", () => {
    const source = MODELES_INTEGRES[0]?.construire() as Schema;
    source.description = "Le réseau de l'agence";
    const modele = modeleDepuisSchema(source, " Agence type ", " Pour les petites agences ");
    expect(modele).toMatchObject({ titre: "Agence type", description: "Pour les petites agences", id: "" });
    expect(modele.equipements.map((e) => e.nom)).toEqual(source.equipements.map((e) => e.nom));
    expect(modeleDepuisSchema(source, "", "").titre).toBe("Petit bureau");
    expect(modeleDepuisSchema(source, "X", "").description).toBeUndefined();
    // Une page demandée d'un schéma qui n'en a qu'une : le schéma entier.
    expect(modeleDepuisSchema(source, "X", "", "").equipements).toHaveLength(source.equipements.length);
  });

  it("ne garde d'une page que ce qui y est dessiné, sur une seule page", () => {
    const s = MODELES_INTEGRES[0]?.construire() as Schema;
    const premiere = ajouterPage(s, "Étage 1");
    const deuxieme = ajouterPage(s, "Étage 2");
    // La box et Internet passent sur la deuxième page : le câble box–switch devient un renvoi.
    for (const e of s.equipements) e.page = ["Internet", "Box"].includes(e.nom) ? deuxieme.id : premiere.id;
    const lien = s.liens.find((l) => [l.a, l.b].some((b) => s.equipements.find((e) => e.id === b.equipement)?.nom === "Box") && [l.a, l.b].some((b) => s.equipements.find((e) => e.id === b.equipement)?.nom === "Switch"));
    if (lien) lien.poses = { "renvoi-a": { place: { x: 4, y: 4 } }, nom: { angle: 10 } };
    const modele = modeleDepuisSchema(lireSchema(ecrireSchema(s)).schema, "Étage", "", premiere.id);
    expect(modele.pages).toBeUndefined();
    expect(modele.equipements.map((e) => e.nom)).not.toContain("Box");
    expect(modele.equipements.every((e) => e.page === undefined)).toBe(true);
    expect(modele.annotations.every((n) => n.page === undefined)).toBe(true);
    expect(erreursStructurelles(modele)).toEqual([]);
    expect(lireSchema(ecrireSchema(modele)).schema.format).toBe(2);
    const autre = modeleDepuisSchema(lireSchema(ecrireSchema(s)).schema, "Arrivée", "", deuxieme.id);
    expect(autre.equipements.map((e) => e.nom).sort()).toEqual(["Box", "Internet"]);
    expect(autre.liens).toHaveLength(1);
  });

  it("oublie la place des renvois d'un câble qui n'a plus d'autre page", () => {
    const s = lireSchema({
      format: 3,
      titre: "S",
      pages: [
        { id: "p_1", nom: "A" },
        { id: "p_2", nom: "B" },
      ],
      equipements: [
        { id: "d_1", page: "p_1", type: "pc", nom: "Un", position: { x: 0, y: 0 }, interfaces: [{ id: "i_1", nom: "eth0" }, { id: "i_2", nom: "eth1" }] },
        { id: "d_2", page: "p_1", type: "pc", nom: "Deux", position: { x: 200, y: 0 }, interfaces: [{ id: "i_1", nom: "eth0" }] },
      ],
      liens: [{ id: "l_1", a: { equipement: "d_1", interface: "i_1" }, b: { equipement: "d_2", interface: "i_1" }, support: "cuivre", points: [], poses: { nom: { angle: 10 } } }],
    }).schema;
    // Le format oublie déjà les renvois d'un câble sur une seule page : on les remet, comme un schéma plus ancien.
    const lien = s.liens[0];
    if (lien) lien.poses = { "renvoi-b": { place: { x: 1, y: 1 } } };
    const modele = modeleDepuisSchema(s, "M", "", "p_1");
    expect(modele.liens[0]?.poses).toBeUndefined();
  });
});
