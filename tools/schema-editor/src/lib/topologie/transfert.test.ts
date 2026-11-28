// @vitest-environment node
// Ajouter des pages à un autre schéma (31D) : copier, déplacer, un nouveau schéma d'une page ; les câbles qui ne
// peuvent pas suivre ; l'ajout dans l'éditeur ouvert, que Ctrl+Z annule en une fois.
import { describe, expect, it, vi } from "vitest";
import { EditeurSchema } from "./etat.svelte";
import { ecrireSchema, erreursStructurelles, lireSchema } from "./format";
import type { Equipement, Interface, Schema } from "./modele";
import { schemaVide } from "./modele";
import { connecter, Refus } from "./operations";
import { ajouterPage, pagesDe, renommerPage, renvoisDeLaPage, surLaPage } from "./pages";
import { compte, compteRendu, copierPages, deplacerPages, retirerPages, schemaDeLaPage } from "./transfert";

vi.mock("../api", () => ({
  api: { enregistrerSchema: vi.fn().mockResolvedValue({ statut: "enregistre", empreinte: "e2" }) },
  messageErreur: (erreur: unknown) => (erreur instanceof Error ? erreur.message : String(erreur)),
}));

const port = (nom: string, plus: Partial<Interface> = {}): Interface => ({
  id: `i_${nom.replace(/\W/g, "")}`,
  nom,
  genre: "cuivre",
  nature: "physique",
  admin: "actif",
  macs: [],
  adresses: [],
  ...plus,
});

function poser(schema: Schema, id: string, nom: string, ports: string[], plus: Partial<Equipement> = {}): Equipement {
  const e: Equipement = {
    id,
    type: "switch",
    nom,
    statut: "actif",
    tags: [],
    proprietes: {},
    membres: [{ numero: 1 }],
    interfaces: ports.map((p) => port(p)),
    position: { x: schema.equipements.length * 160, y: 0 },
    ...plus,
  };
  schema.equipements.push(e);
  return e;
}

const bout = (e: Equipement, nom: string) => ({ equipement: e.id, interface: e.interfaces.find((i) => i.nom === nom)?.id ?? "" });

/**
 * SAMES Meylan, en trois pages : Meylan (routeur, switch d'accès sur le VLAN 20 « Invités »), Stains (son switch),
 * Datacenter (un serveur). Un câble de Meylan à Stains, un de Stains au datacenter, une relation de Meylan au datacenter.
 */
function sames(): Schema {
  const schema: Schema = { ...schemaVide("SAMES Meylan"), id: "s_sames" };
  const rt = poser(schema, "d_rt", "Routeur", ["wan", "lan"]);
  const acc = poser(schema, "d_acc", "SW-Accès", ["1/1"]);
  acc.interfaces[0] = { ...port("1/1"), vlan: { mode: "acces", acces: 20 } };
  connecter(schema, bout(rt, "lan"), bout(acc, "1/1"));
  schema.vlans.push(
    { id: "v_20", numero: 20, nom: "Invités", sousReseaux: [] },
    { id: "v_99", numero: 99, nom: "Inutilisé", sousReseaux: [] },
  );
  schema.groupes.push({ id: "g_baie", nom: "Baie", genre: "zone", membres: [rt.id, acc.id], position: { x: 0, y: 0 }, taille: { largeur: 400, hauteur: 200 } });
  renommerPage(schema, "", "Meylan");
  const stains = ajouterPage(schema, "Stains");
  const sw = poser(schema, "d_sw", "SW-Stains", ["1/1", "1/2"], { page: stains.id });
  connecter(schema, bout(rt, "wan"), bout(sw, "1/1"));
  const dc = ajouterPage(schema, "Datacenter");
  const srv = poser(schema, "d_srv", "Serveur", ["eth0"], { page: dc.id, type: "serveur" });
  connecter(schema, bout(sw, "1/2"), bout(srv, "eth0"));
  schema.relations.push({ id: "r_dep", genre: "dependance", de: rt.id, vers: srv.id, statut: "actif", proprietes: {}, points: [] });
  schema.annotations.push({ id: "n_dc", page: dc.id, texte: "Salle B", position: { x: 0, y: 100 } });
  return lireSchema(ecrireSchema(schema)).schema;
}

/** Le lycée, d'une page : son cœur ; VLAN 20 « Wi-Fi ». */
function lycee(): Schema {
  const schema: Schema = { ...schemaVide("Réseau du lycée"), id: "s_lycee" };
  poser(schema, "d_coeur", "Cœur", ["1/1"]);
  schema.vlans.push({ id: "v_20", numero: 20, nom: "Wi-Fi", sousReseaux: [] });
  return lireSchema(ecrireSchema(schema)).schema;
}

const page = (schema: Schema, nom: string) => pagesDe(schema).find((p) => p.nom === nom)?.id ?? "?";
const noms = (schema: Schema) => pagesDe(schema).map((p) => p.nom);

describe("copier des pages", () => {
  it("les pages arrivent après celles de la cible ; la source ne change pas ; un câble vers une page laissée est compté", () => {
    const source = sames();
    const avant = JSON.stringify(source);
    const cible = lycee();
    const t = copierPages(source, [page(source, "Stains")], cible);
    expect(JSON.stringify(source)).toBe(avant);
    expect(noms(cible)).toEqual(["Page 1", "Stains"]);
    expect(t.pages.map((p) => p.nom)).toEqual(["Stains"]);
    expect(t.equipements).toBe(1);
    // Stains–Meylan et Stains–Datacenter ne viennent pas.
    expect(t.cablesRetires).toBe(2);
    expect(t.relationsRetirees).toBe(0);
    expect(surLaPage(cible, t.pages[0]?.id).equipements.map((e) => e.nom)).toEqual(["SW-Stains"]);
    expect(cible.liens).toHaveLength(0);
    expect(cible.format).toBe(3);
    expect(erreursStructurelles(cible)).toEqual([]);
    expect(cible.equipements.find((e) => e.nom === "SW-Stains")?.id).not.toBe("d_sw");
  });

  it("un câble entre deux pages copiées est gardé : il reste un renvoi d'une page à l'autre", () => {
    const source = sames();
    const cible = lycee();
    const t = copierPages(source, [page(source, "Stains"), page(source, "Meylan")], cible);
    // L'ordre est celui des onglets de la source.
    expect(t.pages.map((p) => p.nom)).toEqual(["Meylan", "Stains"]);
    expect(t.cablesRetires).toBe(1);
    expect(t.relationsRetirees).toBe(1);
    expect(cible.liens).toHaveLength(2);
    expect(renvoisDeLaPage(cible, t.pages[1]?.id).map((r) => r.texte)).toEqual(["→ Meylan · Routeur · wan"]);
    // Le cadre suit ses membres, sur sa page.
    expect(cible.groupes[0]).toMatchObject({ page: t.pages[0]?.id });
    expect(cible.groupes[0]?.membres).toHaveLength(2);
  });

  it("les VLAN : seuls ceux dont les pages se servent viennent ; un conflit de nom garde celui de la cible et le dit", () => {
    const source = sames();
    const cible = lycee();
    const t = copierPages(source, [page(source, "Meylan")], cible);
    expect(cible.vlans.map((v) => `${v.numero} ${v.nom}`)).toEqual(["20 Wi-Fi"]);
    expect(t.avertissements).toEqual([
      "VLAN 20 : « Wi-Fi » dans « Réseau du lycée », « Invités » dans « SAMES Meylan ». Le nom de « Réseau du lycée » est gardé.",
    ]);
  });

  it("un nom de page déjà pris est préfixé du titre de la source", () => {
    const source = sames();
    const cible = lycee();
    renommerPage(cible, "", "Stains");
    copierPages(source, [page(source, "Stains")], cible);
    expect(noms(cible)).toEqual(["Stains", "SAMES Meylan · Stains"]);
  });

  it("une cible vide prend les pages à la place de la sienne ; sans page voulue, c'est refusé", () => {
    const source = sames();
    const vide = schemaVide("Neuf");
    copierPages(source, [page(source, "Datacenter")], vide);
    expect(vide.pages?.map((p) => p.nom)).toEqual(["Datacenter"]);
    expect(vide.format).toBe(2);
    expect(vide.annotations.map((n) => n.texte)).toEqual(["Salle B"]);
    expect(() => copierPages(source, ["p_inconnue"], lycee())).toThrow(Refus);
  });

  it("une source d'une page implicite se copie par l'identifiant vide, à son nom « Page 1 »", () => {
    const cible = sames();
    const t = copierPages(lycee(), [""], cible);
    expect(t.pages.map((p) => p.nom)).toEqual(["Réseau du lycée"]);
    expect(noms(cible)).toEqual(["Meylan", "Stains", "Datacenter", "Réseau du lycée"]);
  });
});

describe("déplacer des pages", () => {
  it("elles quittent la source avec leurs câbles vers les pages qui y restent ; la cible les reçoit", () => {
    const source = sames();
    const cible = lycee();
    const t = deplacerPages(source, [page(source, "Stains")], cible);
    expect(t.cablesRetires).toBe(2);
    expect(noms(source)).toEqual(["Meylan", "Datacenter"]);
    expect(source.equipements.map((e) => e.nom)).toEqual(["Routeur", "SW-Accès", "Serveur"]);
    expect(source.liens).toHaveLength(1);
    expect(source.relations).toHaveLength(1);
    expect(noms(cible)).toEqual(["Page 1", "Stains"]);
    expect(erreursStructurelles(source)).toEqual([]);
    expect(erreursStructurelles(cible)).toEqual([]);
  });

  it("toutes les pages parties, la source garde une page vide", () => {
    const source = sames();
    deplacerPages(source, pagesDe(source).map((p) => p.id), lycee());
    expect(source.equipements).toEqual([]);
    expect(source.liens).toEqual([]);
    expect(source.relations).toEqual([]);
    expect(source.groupes).toEqual([]);
    expect(source.annotations).toEqual([]);
    expect(source.pages).toBeUndefined();
    expect(source.format).toBe(2);
    const seule = lycee();
    retirerPages(seule, [""]);
    expect(seule.equipements).toEqual([]);
  });

  it("une page retirée parmi trois, puis deux : le schéma reste cohérent", () => {
    const source = sames();
    retirerPages(source, [page(source, "Meylan"), page(source, "Datacenter")]);
    expect(noms(source)).toEqual(["Stains"]);
    expect(source.equipements.map((e) => e.nom)).toEqual(["SW-Stains"]);
    expect(source.format).toBe(2);
    expect(erreursStructurelles(source)).toEqual([]);
  });
});

describe("un nouveau schéma d'une page", () => {
  it("il prend le nom de la page, ses équipements et ses câbles internes ; la source ne change pas", () => {
    const source = { ...sames(), vue: "logique" as const, cablesCoudes: true, affichage: { vlans: false } };
    const avant = JSON.stringify(source);
    const { schema, transfert } = schemaDeLaPage(source, page(source, "Meylan"));
    expect(JSON.stringify(source)).toBe(avant);
    expect(schema.titre).toBe("Meylan");
    expect(schema.pages?.map((p) => p.nom)).toEqual(["Meylan"]);
    expect(schema.equipements.map((e) => e.nom)).toEqual(["Routeur", "SW-Accès"]);
    expect(schema.liens).toHaveLength(1);
    expect(schema).toMatchObject({ vue: "logique", cablesCoudes: true, affichage: { vlans: false } });
    expect(transfert.cablesRetires).toBe(1);
    const texte = ecrireSchema({ ...schema, id: "" });
    expect(JSON.parse(texte).format).toBe(2);
    expect(lireSchema(texte).erreurs).toEqual([]);
    expect(schemaDeLaPage(lycee(), "").schema.titre).toBe("Page 1");
    expect(() => schemaDeLaPage(lycee(), "p_inconnue")).toThrow(Refus);
  });
});

describe("le compte rendu", () => {
  it("dit ce qui a été fait et ce qui n'a pas suivi", () => {
    const source = sames();
    const t = copierPages(source, [page(source, "Stains"), page(source, "Meylan")], lycee());
    expect(compteRendu(t, { source: "SAMES Meylan", cible: "Réseau du lycée", deplacer: false })).toEqual([
      "2 pages ajoutées dans « Réseau du lycée » : « Meylan », « Stains ».",
      "1 câble va vers une page qui n'est pas copiée : il reste dans « SAMES Meylan » seulement.",
      "1 relation vers une page laissée n'est pas copiée.",
      "VLAN 20 : « Wi-Fi » dans « Réseau du lycée », « Invités » dans « SAMES Meylan ». Le nom de « Réseau du lycée » est gardé.",
    ]);
    const autre = sames();
    const d = deplacerPages(autre, [page(autre, "Stains")], lycee());
    expect(compteRendu(d, { source: "SAMES Meylan", cible: "Réseau du lycée", deplacer: true }).slice(0, 2)).toEqual([
      "Page déplacée dans « Réseau du lycée » : « Stains ».",
      "2 câbles allaient vers une page restée dans « SAMES Meylan » : ils sont retirés, un câble ne va pas d'un schéma à l'autre.",
    ]);
    const un = { ...d, cablesRetires: 1, relationsRetirees: 2, avertissements: [] };
    expect(compteRendu(un, { source: "A", cible: "B", deplacer: true }).slice(1)).toEqual([
      "1 câble allait vers une page restée dans « A » : il est retiré, un câble ne va pas d'un schéma à l'autre.",
      "2 relations vers une page laissée sont retirées.",
    ]);
    expect(compteRendu({ ...un, cablesRetires: 2, relationsRetirees: 1 }, { source: "A", cible: "B", deplacer: false }).slice(1)).toEqual([
      "2 câbles vont vers une page qui n'est pas copiée : ils restent dans « A » seulement.",
      "1 relation vers une page laissée n'est pas copiée.",
    ]);
    expect(compteRendu({ ...un, cablesRetires: 0, relationsRetirees: 1 }, { source: "A", cible: "B", deplacer: true })[1]).toBe(
      "1 relation vers une page laissée est retirée.",
    );
    expect(compteRendu({ ...un, cablesRetires: 0, relationsRetirees: 2 }, { source: "A", cible: "B", deplacer: false })[1]).toBe(
      "2 relations vers une page laissée ne sont pas copiées.",
    );
    expect(compte(1, "page", "pages")).toBe("1 page");
  });
});

describe("dans l'éditeur ouvert", () => {
  it("les pages arrivent en une opération, la première montrée ; Ctrl+Z les retire toutes", () => {
    const editeur = new EditeurSchema();
    editeur.schema = lycee();
    editeur.id = "s_lycee";
    const avant = JSON.stringify(editeur.schema);
    const source = sames();
    const t = editeur.recevoirPages(source, [page(source, "Meylan"), page(source, "Stains")]);
    expect(t?.pages).toHaveLength(2);
    expect(editeur.pages.map((p) => p.nom)).toEqual(["Page 1", "Meylan", "Stains"]);
    expect(editeur.pageMontree).toBe(t?.pages[0]?.id);
    editeur.annuler();
    expect(JSON.stringify(editeur.schema)).toBe(avant);
    expect(editeur.recevoirPages(source, [])).toBeNull();
    expect(editeur.annonce).toBe("Aucune page à ajouter.");
  });

  it("une page qui part est retirée en une opération : la sélection et le câble en main ne la suivent pas ; Ctrl+Z la rend", () => {
    const editeur = new EditeurSchema();
    editeur.schema = sames();
    const avant = JSON.stringify(editeur.schema);
    const stains = page(editeur.schema, "Stains");
    editeur.montrerPage(stains);
    editeur.selection = ["d_sw"];
    editeur.cable = { support: "auto", depart: { equipement: "d_sw", interface: "i_11" } };
    expect(editeur.retirerPages([stains], "Page « Stains » déplacée.")).toBe(true);
    expect(editeur.pages.map((p) => p.nom)).toEqual(["Meylan", "Datacenter"]);
    expect(editeur.pageMontree).toBe(page(editeur.schema, "Meylan"));
    expect(editeur.selection).toEqual([]);
    expect(editeur.cable).toBeNull();
    expect(editeur.annonce).toBe("Page « Stains » déplacée.");
    editeur.annuler();
    expect(JSON.stringify(editeur.schema)).toBe(avant);
  });
});
