// @vitest-environment node
// Les pages d'un schéma (30A) : le format, les opérations, la vue d'une page, les renvois, le dessin, draw.io.
import { describe, expect, it, vi } from "vitest";
import { dessinerSchema } from "./dessin";
import { ecrireDrawio } from "./drawio";
import { EditeurSchema } from "./etat.svelte";
import { placer, remettre } from "./etiquettes";
import { ecrireSchema, erreursStructurelles, lireSchema } from "./format";
import { lireImport, remarquesImport } from "./importation";
import { type Equipement, interfaceDe, type Schema, schemaVide } from "./modele";
import { collerEquipements, connecter, copierEquipements, creerEquipement, Refus } from "./operations";
import {
  ajouterPage,
  aPlusieursPages,
  deplacerPage,
  dupliquerPage,
  elementsDuSchema,
  equipementsParPage,
  nomDePageLibre,
  pageDeLElement,
  pageDeLObjet,
  pagesDe,
  pageValide,
  rattacherALaPage,
  renommerPage,
  renvoisDeLaPage,
  supprimerPage,
  surLaPage,
} from "./pages";
import { feuilleMontree, feuillesAImprimer, titreDeLaSortie } from "./sorties";

vi.mock("../api", () => ({
  api: { enregistrerSchema: vi.fn() },
  messageErreur: (erreur: unknown) => (erreur instanceof Error ? erreur.message : String(erreur)),
}));

const port = (e: Equipement, rang = 0) => ({ equipement: e.id, interface: e.interfaces[rang]?.id ?? "" });

/** Un schéma d'une page : le cœur, un poste, un câble entre eux. */
function unePage() {
  const schema: Schema = { ...schemaVide("Réseau du lycée"), id: "s_1" };
  const coeur = creerEquipement(schema, "switch-coeur", { x: 0, y: 0 });
  coeur.nom = "Cœur";
  const poste = creerEquipement(schema, "pc", { x: 0, y: 200 });
  poste.nom = "PC-Accueil";
  const lien = connecter(schema, port(coeur), port(poste));
  return { schema, coeur, poste, lien };
}

/** Le lycée en deux pages : l'étage 1 (cœur, poste, une zone, une note), l'étage 2 (son switch), un câble entre les deux. */
function deuxPages() {
  const { schema, coeur, poste, lien } = unePage();
  schema.groupes.push({ id: "g_zone", nom: "Hall", genre: "zone", membres: [coeur.id], position: { x: -100, y: -100 }, taille: { largeur: 300, hauteur: 400 } });
  schema.annotations.push({ id: "n_note", texte: "Baie du hall", position: { x: 200, y: 0 } });
  renommerPage(schema, "", "Étage 1");
  const etage2 = ajouterPage(schema, "Étage 2");
  const sw = creerEquipement(schema, "switch", { x: 400, y: 0 });
  sw.nom = "SW-Étage2";
  sw.page = etage2.id;
  const montee = connecter(schema, port(coeur, 1), port(sw, sw.interfaces.length - 1));
  const etage1 = pagesDe(schema)[0]?.id ?? "";
  return { schema, coeur, poste, lien, sw, montee, etage1, etage2: etage2.id };
}

describe("pages : le format", () => {
  it("un schéma sans pages ne change pas d'un octet : format 2, ni « pages » ni « page »", () => {
    const { schema } = unePage();
    const texte = ecrireSchema(schema);
    expect(JSON.parse(texte).format).toBe(2);
    expect(texte).not.toMatch(/"pages?"/);
    expect(ecrireSchema(lireSchema(texte).schema)).toBe(texte);
    expect(pagesDe(schema)).toEqual([{ id: "", nom: "Page 1" }]);
    expect(aPlusieursPages(schema)).toBe(false);
  });

  it("avec deux pages, le schéma passe au format 3, chaque élément porte sa page, et il se relit à l'identique", () => {
    const { schema, etage1, etage2, sw } = deuxPages();
    const texte = ecrireSchema(schema);
    const relu = lireSchema(texte);
    expect(relu.erreurs).toEqual([]);
    expect(relu.schema.format).toBe(3);
    expect(relu.schema.pages?.map((p) => p.nom)).toEqual(["Étage 1", "Étage 2"]);
    expect(relu.schema.equipements.find((e) => e.id === sw.id)?.page).toBe(etage2);
    expect(relu.schema.groupes[0]?.page).toBe(etage1);
    expect(relu.schema.annotations[0]?.page).toBe(etage1);
    expect(ecrireSchema(relu.schema)).toBe(texte);
  });

  it("une version qui ne connaît que le format 2 refuserait le format 3 ; le format 4 est refusé ici", () => {
    const { schema } = deuxPages();
    const brut = JSON.parse(ecrireSchema(schema));
    expect(() => lireSchema({ ...brut, format: 4 })).toThrow("format 4 plus récent que cette version de kyfRev (format 3)");
  });

  it("une page inconnue renvoie à la première ; un cadre ne garde que les membres de sa page ; les pages en double partent", () => {
    const { schema, etage1, etage2, sw, coeur } = deuxPages();
    const brut = JSON.parse(ecrireSchema(schema));
    brut.pages.push({ id: etage2, nom: "Doublon" }, { id: "p_vide", nom: "  " });
    brut.equipements.find((e: Equipement) => e.id === coeur.id).page = "p_inconnue";
    brut.groupes[0].membres.push(sw.id);
    const { schema: lu } = lireSchema(brut);
    expect(lu.pages?.map((p) => p.nom)).toEqual(["Étage 1", "Étage 2", "Page 4"]);
    expect(lu.equipements.find((e) => e.id === coeur.id)?.page).toBe(etage1);
    expect(lu.groupes[0]?.membres).toEqual([coeur.id]);
  });

  it("une seule page déclarée : format 2, son nom gardé, aucun élément ne porte de page", () => {
    const { schema } = unePage();
    const brut = JSON.parse(ecrireSchema(schema));
    const { schema: lu } = lireSchema({ ...brut, format: 3, pages: [{ id: "p_a", nom: "Rez-de-chaussée" }], equipements: brut.equipements.map((e: Equipement) => ({ ...e, page: "p_a" })) });
    expect(lu.format).toBe(2);
    expect(lu.pages).toEqual([{ id: "p_a", nom: "Rez-de-chaussée" }]);
    expect(lu.equipements.every((e) => e.page === undefined)).toBe(true);
  });

  it("migration : passer à deux pages range tout sur la première ; revenir à une page rend le schéma d'avant", () => {
    const { schema } = unePage();
    const avant = ecrireSchema(schema);
    const ajoutee = ajouterPage(schema);
    expect(ajoutee.nom).toBe("Page 2");
    expect(schema.format).toBe(3);
    const premiere = pagesDe(schema)[0]?.id;
    expect(schema.equipements.every((e) => e.page === premiere)).toBe(true);
    supprimerPage(schema, ajoutee.id);
    expect(ecrireSchema(schema)).toBe(avant);
  });
});

describe("pages : la vue d'une page, les renvois", () => {
  it("une page ne montre que ses équipements, cadres, notes et les câbles entre eux ; les objets sont ceux du schéma", () => {
    const { schema, coeur, sw, lien, montee, etage1, etage2 } = deuxPages();
    const ici = surLaPage(schema, etage1);
    expect(ici.equipements.map((e) => e.nom)).toEqual(["Cœur", "PC-Accueil"]);
    expect(ici.liens.map((l) => l.id)).toEqual([lien.id]);
    expect(ici.groupes).toHaveLength(1);
    expect(ici.annotations).toHaveLength(1);
    expect(ici.equipements[0]).toBe(coeur);
    const la = surLaPage(schema, etage2);
    expect(la.equipements).toEqual([sw]);
    expect(la.liens).toEqual([]);
    expect(la.groupes).toEqual([]);
    // Une page inconnue : la première.
    expect(surLaPage(schema, "p_rien").equipements).toHaveLength(2);
    expect(pageValide(schema, "p_rien")).toBe(etage1);
    expect(pageDeLObjet(schema, montee.id)).toBe(etage1);
    expect(pageDeLObjet(schema, sw.id)).toBe(etage2);
    expect(pageDeLObjet(schema, "n_note")).toBe(etage1);
    expect(pageDeLObjet(schema, "rien")).toBeUndefined();
    expect([...equipementsParPage(schema)]).toEqual([
      [etage1, 2],
      [etage2, 1],
    ]);
  });

  it("un câble entre deux pages se dessine en renvoi sur chacune : page, équipement et port d'en face", () => {
    const { schema, coeur, sw, montee, etage1, etage2 } = deuxPages();
    const ici = renvoisDeLaPage(schema, etage1);
    expect(ici).toHaveLength(1);
    const portSw = interfaceDe(schema, montee.b)?.nom;
    expect(ici[0]).toMatchObject({ equipement: coeur.id, versEquipement: sw.id, page: { id: etage2, nom: "Étage 2" }, texte: `→ Étage 2 · SW-Étage2 · ${portSw}` });
    // À côté de son équipement, hors de son icône, un peu au-dessus de son centre.
    expect(Math.abs((ici[0]?.centre.x ?? 0) - coeur.position.x) - (ici[0]?.largeur ?? 0) / 2).toBeGreaterThan(28);
    expect(ici[0]?.centre.y).toBeLessThan(coeur.position.y);
    const la = renvoisDeLaPage(schema, etage2);
    expect(la[0]).toMatchObject({ equipement: sw.id, versEquipement: coeur.id, page: { id: etage1, nom: "Étage 1" } });
    expect(la[0]?.texte).toBe(`→ Étage 1 · Cœur · ${interfaceDe(schema, montee.a)?.nom}`);
    // Un schéma d'une page n'en a pas ; un équipement masqué non plus.
    expect(renvoisDeLaPage(unePage().schema)).toEqual([]);
    expect(renvoisDeLaPage(schema, etage1, new Set([coeur.id]))).toEqual([]);
  });

  it("le dessin d'une page (aperçu, export, fiche) : ses équipements et ses renvois ; sans page, la première", () => {
    const { schema, etage2 } = deuxPages();
    const premiere = dessinerSchema(schema);
    expect(premiere).toContain("Cœur");
    expect(premiere).not.toContain(">SW-Étage2<");
    expect(premiere).toContain("→ Étage 2 · SW-Étage2");
    expect(premiere).toContain("data-renvoi=");
    const seconde = dessinerSchema(schema, { page: etage2 });
    expect(seconde).toContain(">SW-Étage2<");
    expect(seconde).toContain("→ Étage 1 · Cœur");
    expect(seconde).not.toContain(">PC-Accueil<");
  });
});

describe("pages : ajouter, renommer, déplacer, dupliquer, supprimer", () => {
  it("ajoute après une page, avec le premier nom libre", () => {
    const { schema, etage1 } = deuxPages();
    expect(nomDePageLibre(schema)).toBe("Page 3");
    const milieu = ajouterPage(schema, undefined, etage1);
    expect(pagesDe(schema).map((p) => p.nom)).toEqual(["Étage 1", "Page 3", "Étage 2"]);
    expect(milieu.nom).toBe("Page 3");
    expect(() => ajouterPage(schema, "   ")).toThrow(Refus);
  });

  it("renomme : un nom vide est refusé, un nom trop long coupé, la page implicite devient déclarée", () => {
    const { schema, etage2 } = deuxPages();
    renommerPage(schema, etage2, "  Salle   serveurs ");
    expect(pagesDe(schema)[1]?.nom).toBe("Salle serveurs");
    expect(() => renommerPage(schema, etage2, " ")).toThrow("Une page doit avoir un nom.");
    expect(() => renommerPage(schema, "p_rien", "X")).toThrow("Cette page n'existe plus.");
    renommerPage(schema, etage2, "x".repeat(200));
    expect(pagesDe(schema)[1]?.nom).toHaveLength(80);
    const seul = unePage().schema;
    renommerPage(seul, "", "Rez-de-chaussée");
    expect(seul.pages).toHaveLength(1);
    expect(seul.format).toBe(2);
  });

  it("déplace une page à gauche ou à droite, bornée", () => {
    const { schema, etage1, etage2 } = deuxPages();
    deplacerPage(schema, etage2, 0);
    expect(pagesDe(schema).map((p) => p.id)).toEqual([etage2, etage1]);
    deplacerPage(schema, etage2, 99);
    expect(pagesDe(schema).map((p) => p.id)).toEqual([etage1, etage2]);
    deplacerPage(schema, etage2, 1);
    expect(() => deplacerPage(schema, "p_rien", 0)).toThrow(Refus);
  });

  it("duplique une page juste après elle : équipements, câbles, cadres, notes et ensembles ; pas les câbles vers les autres pages", () => {
    const { schema, coeur, etage1 } = deuxPages();
    schema.ensembles = [{ id: "e_1", membres: [coeur.id, "n_note"] }];
    const avant = schema.liens.length;
    const copie = dupliquerPage(schema, etage1);
    expect(pagesDe(schema).map((p) => p.nom)).toEqual(["Étage 1", "Étage 1 (copie)", "Étage 2"]);
    const vue = surLaPage(schema, copie.id);
    expect(vue.equipements.map((e) => e.nom)).toEqual(["Cœur copie", "PC-Accueil copie"]);
    expect(vue.liens).toHaveLength(1);
    expect(schema.liens).toHaveLength(avant + 1);
    expect(vue.groupes[0]).toMatchObject({ nom: "Hall", page: copie.id, membres: [vue.equipements[0]?.id] });
    expect(vue.annotations[0]).toMatchObject({ texte: "Baie du hall", page: copie.id });
    expect(schema.ensembles?.[1]?.membres).toEqual([vue.equipements[0]?.id, vue.annotations[0]?.id]);
    expect(renvoisDeLaPage(schema, copie.id)).toEqual([]);
    expect(erreursStructurelles(schema)).toEqual([]);
    // La page implicite d'un schéma d'une page se duplique aussi.
    const seul = unePage().schema;
    const double = dupliquerPage(seul, "");
    expect(pagesDe(seul).map((p) => p.nom)).toEqual(["Page 1", "Page 1 (copie)"]);
    expect(surLaPage(seul, double.id).equipements).toHaveLength(2);
    expect(() => dupliquerPage(seul, "p_rien")).toThrow(Refus);
  });

  it("supprime une page, ce qu'elle porte et ses câbles vers les autres ; un schéma garde au moins une page", () => {
    const { schema, sw, etage1, etage2 } = deuxPages();
    supprimerPage(schema, etage2);
    expect(schema.equipements.some((e) => e.id === sw.id)).toBe(false);
    expect(schema.liens).toHaveLength(1);
    expect(schema.format).toBe(2);
    expect(schema.pages).toEqual([{ id: etage1, nom: "Étage 1" }]);
    expect(schema.equipements.every((e) => e.page === undefined)).toBe(true);
    expect(() => supprimerPage(schema, etage1)).toThrow("Un schéma garde au moins une page.");
    const autre = deuxPages();
    expect(() => supprimerPage(autre.schema, "p_rien")).toThrow(Refus);
    autre.schema.ensembles = [{ id: "e_1", membres: [autre.coeur.id, "n_note", autre.poste.id] }];
    supprimerPage(autre.schema, autre.etage1);
    expect(autre.schema.equipements.map((e) => e.nom)).toEqual(["SW-Étage2"]);
    expect(autre.schema.groupes).toEqual([]);
    expect(autre.schema.annotations).toEqual([]);
    expect(autre.schema.ensembles).toBeUndefined();
    expect(autre.schema.pages?.map((p) => p.nom)).toEqual(["Étage 2"]);
  });

  it("ce qui vient d'apparaître va sur la page montrée ; une copie collée oublie sa page d'origine", () => {
    const { schema, coeur, etage2 } = deuxPages();
    const connus = elementsDuSchema(schema);
    const nouveau = creerEquipement(schema, "pc", { x: 0, y: 0 });
    schema.annotations.push({ id: "n_2", texte: "x", position: { x: 0, y: 0 }, page: "p_perdue" });
    rattacherALaPage(schema, etage2, connus);
    expect(nouveau.page).toBe(etage2);
    expect(schema.annotations.find((n) => n.id === "n_2")?.page).toBe(etage2);
    const [colle] = collerEquipements(schema, copierEquipements(schema, new Set([coeur.id])));
    expect(schema.equipements.find((e) => e.id === colle)?.page).toBeUndefined();
    expect(pageDeLElement(schema, { page: "p_perdue" })).toBe(pagesDe(schema)[0]?.id);
  });
});

describe("pages : l'éditeur", () => {
  function editeur() {
    const e = new EditeurSchema();
    const { schema, ...reste } = deuxPages();
    e.schema = schema;
    e.id = "s_1";
    return { e, ...reste };
  }

  it("montre une page, choisit et pose sur elle ; Ctrl+A ne prend que ses équipements", () => {
    const { e, etage1, etage2, sw } = editeur();
    expect(e.pageMontree).toBe(etage1);
    e.selection = [e.schema.equipements[0]?.id ?? ""];
    e.montrerPage(etage2);
    expect(e.pageMontree).toBe(etage2);
    expect(e.selection).toEqual([]);
    expect(e.vuePage.equipements).toEqual([sw]);
    expect(e.renvois).toHaveLength(1);
    e.toutChoisir();
    expect(e.selection).toEqual([sw.id]);
    let pose = "";
    e.agir((s) => (pose = creerEquipement(s, "pc", { x: 0, y: 0 }).id));
    expect(e.pageDe(pose)).toBe(etage2);
    expect(e.equipementsParPage.get(etage2)).toBe(2);
  });

  it("ajoute, renomme, duplique, déplace et supprime des pages ; Ctrl+Z défait", () => {
    const { e, etage1, etage2 } = editeur();
    e.ajouterPage();
    expect(e.pages.map((p) => p.nom)).toEqual(["Étage 1", "Étage 2", "Page 3"]);
    expect(e.pageMontree).toBe(e.pages[2]?.id);
    e.annuler();
    expect(e.pages).toHaveLength(2);
    expect(e.pageMontree).toBe(etage1);
    expect(e.renommerPage(etage2, "Salle serveurs")).toBe(true);
    expect(e.renommerPage(etage2, "")).toBe(false);
    expect(e.annonce).toBe("Une page doit avoir un nom.");
    e.dupliquerPage(etage2);
    expect(e.pages.map((p) => p.nom)).toEqual(["Étage 1", "Salle serveurs", "Salle serveurs (copie)"]);
    e.deplacerPage(etage1, 2);
    expect(e.pages.map((p) => p.nom)).toEqual(["Salle serveurs", "Salle serveurs (copie)", "Étage 1"]);
    e.montrerPage(etage2);
    e.supprimerPage(etage2);
    expect(e.pages.map((p) => p.nom)).toEqual(["Salle serveurs (copie)", "Étage 1"]);
    expect(e.pageMontree).toBe(e.pages[0]?.id);
    expect(e.annonce).toBe("Page « Salle serveurs » supprimée : Ctrl+Z pour l'annuler.");
    e.annuler();
    expect(e.pages).toHaveLength(3);
  });

  it("renommer la page implicite d'un schéma d'une page la déclare et la garde montrée", () => {
    const e = new EditeurSchema();
    e.schema = unePage().schema;
    expect(e.pageMontree).toBe("");
    e.renommerPage("", "Rez-de-chaussée");
    expect(e.pages).toEqual([{ id: e.pageMontree, nom: "Rez-de-chaussée" }]);
  });
});

describe("pages : draw.io", () => {
  it("une page de kyfRev est une page draw.io ; le renvoi est une pastille qui mène à l'autre page", () => {
    const { schema, etage1, etage2 } = deuxPages();
    const xml = ecrireDrawio(schema);
    expect(xml.match(/<diagram /g)).toHaveLength(2);
    expect(xml).toContain(`<diagram name="Étage 1" id="kyfrev-${etage1}">`);
    expect(xml).toContain(`<diagram name="Étage 2" id="kyfrev-${etage2}">`);
    expect(xml.match(/ kyfrev="/g)).toHaveLength(1);
    expect(xml).toContain(`kyfrevPage="${etage2}"`);
    expect(xml).toContain(`link="data:page/id,kyfrev-${etage2}"`);
    expect(xml).toContain("→ Étage 2 · SW-Étage2");
  });

  it("un schéma d'une page sort en une page au nom du schéma, sans renvoi", () => {
    const xml = ecrireDrawio(unePage().schema);
    expect(xml.match(/<diagram /g)).toHaveLength(1);
    expect(xml).toContain('<diagram name="Réseau du lycée"');
    expect(xml).not.toContain("kyfrevPage");
  });

  it("l'aller-retour garde les pages, leurs éléments et le câble entre deux pages", async () => {
    const { schema, montee, sw, etage2 } = deuxPages();
    const importe = await lireImport(ecrireDrawio(schema), "C:/x/lycée.drawio");
    expect(importe.erreurs).toEqual([]);
    expect(importe.schema.titre).toBe("Réseau du lycée");
    expect(importe.schema.pages).toEqual(schema.pages);
    expect(importe.schema.liens.find((l) => l.id === montee.id)).toBeTruthy();
    expect(importe.schema.equipements.find((e) => e.id === sw.id)?.page).toBe(etage2);
    expect(importe.schema.groupes).toHaveLength(1);
    expect(importe.schema.annotations).toHaveLength(1);
    expect(importe.compteRendu?.pages).toBe(2);
    expect(importe.compteRendu && remarquesImport(importe.compteRendu)).toEqual([]);
    expect(ecrireSchema(importe.schema)).toBe(ecrireSchema(lireSchema(ecrireSchema(schema)).schema));
  });

  it("ce qui change dans draw.io : une page renommée, une page retirée (et son câble), une page ajoutée", async () => {
    const { schema, sw, montee, etage1 } = deuxPages();
    let xml = ecrireDrawio(schema).replace('<diagram name="Étage 2"', '<diagram name="Étage deux"');
    const renomme = await lireImport(xml);
    expect(renomme.schema.pages?.map((p) => p.nom)).toEqual(["Étage 1", "Étage deux"]);

    const sansEtage2 = xml.replace(/<diagram name="Étage deux"[\s\S]*?<\/diagram>\n/, "");
    const retire = await lireImport(sansEtage2);
    expect(retire.schema.equipements.some((e) => e.id === sw.id)).toBe(false);
    expect(retire.schema.liens.some((l) => l.id === montee.id)).toBe(false);
    expect(retire.schema.format).toBe(2);
    expect(retire.schema.pages).toEqual([{ id: etage1, nom: "Étage 1" }]);

    xml = xml.replace(
      "</mxfile>",
      '<diagram name="Salle serveurs" id="neuve"><mxGraphModel><root><mxCell id="0"/><mxCell id="1" parent="0"/><mxCell id="srv" value="NAS" style="shape=mxgraph.networks.nas_filer;" vertex="1" parent="1"><mxGeometry x="0" y="0" width="56" height="56" as="geometry"/></mxCell></root></mxGraphModel></diagram>\n</mxfile>',
    );
    const ajoute = await lireImport(xml);
    expect(ajoute.schema.pages?.map((p) => p.nom)).toEqual(["Étage 1", "Étage deux", "Salle serveurs"]);
    const nas = ajoute.schema.equipements.find((e) => e.nom === "NAS");
    expect(nas?.type).toBe("nas");
    expect(nas?.page).toBe(ajoute.schema.pages?.[2]?.id);
  });

  it("un modèle d'une page et une page ajoutée dans draw.io : deux pages, la première garde ses équipements", async () => {
    const { schema, coeur } = unePage();
    const xml = ecrireDrawio(schema).replace(
      "</mxfile>",
      '<diagram name="Annexe" id="a"><mxGraphModel><root><mxCell id="0"/><mxCell id="1" parent="0"/></root></mxGraphModel></diagram>\n</mxfile>',
    );
    const { schema: lu } = await lireImport(xml);
    expect(lu.pages?.map((p) => p.nom)).toEqual(["Réseau du lycée", "Annexe"]);
    expect(lu.titre).toBe("Réseau du lycée");
    expect(lu.equipements.find((e) => e.id === coeur.id)?.page).toBe(lu.pages?.[0]?.id);
  });
});

describe("pages : les sorties", () => {
  it("le titre d'une image d'un schéma à plusieurs pages porte le nom de sa page", () => {
    const { schema, etage2 } = deuxPages();
    expect(titreDeLaSortie(schema, etage2)).toBe("Réseau du lycée - Étage 2");
    expect(titreDeLaSortie(schema)).toBe("Réseau du lycée - Étage 1");
    expect(titreDeLaSortie(unePage().schema)).toBe("Réseau du lycée");
  });

  it("l'impression sort toutes les pages, une par feuille, dans l'ordre des onglets, le nom de chacune en tête", () => {
    const { schema, etage1, etage2 } = deuxPages();
    expect(feuillesAImprimer(schema)).toEqual([
      { page: etage1, titre: "Réseau du lycée - Étage 1" },
      { page: etage2, titre: "Réseau du lycée - Étage 2" },
    ]);
    // L'ordre des onglets change : celui des feuilles aussi.
    deplacerPage(schema, etage2, 0);
    expect(feuillesAImprimer(schema).map((f) => f.page)).toEqual([etage2, etage1]);
  });

  it("une page sans équipement ne s'imprime pas ; un schéma d'une page sort sous son titre seul", () => {
    const { schema, etage1, etage2 } = deuxPages();
    ajouterPage(schema, "Annexe");
    expect(feuillesAImprimer(schema).map((f) => f.page)).toEqual([etage1, etage2]);
    expect(feuillesAImprimer(unePage().schema)).toEqual([{ page: "", titre: "Réseau du lycée" }]);
  });

  it("l'aperçu s'ouvre sur la feuille de la page montrée, sinon sur la première", () => {
    const { schema, etage2 } = deuxPages();
    const vide = ajouterPage(schema, "Annexe").id;
    const feuilles = feuillesAImprimer(schema);
    expect(feuilleMontree(feuilles, etage2)).toBe(2);
    expect(feuilleMontree(feuilles)).toBe(1);
    expect(feuilleMontree(feuilles, vide)).toBe(1);
  });
});

describe("pages : retours sur la 30A", () => {
  it("un câble en main survit au changement de page ; la suppression d'une page le repose", () => {
    const e = new EditeurSchema();
    const { schema, coeur, etage1, etage2 } = deuxPages();
    e.schema = schema;
    e.id = "s_1";
    e.cable = { support: "auto", depart: port(coeur, 0) };
    e.montrerPage(etage2);
    expect(e.pageMontree).toBe(etage2);
    expect(e.cable?.depart?.equipement).toBe(coeur.id);
    e.montrerPage(etage1);
    expect(e.cable?.depart?.equipement).toBe(coeur.id);
    let troisieme = "";
    e.agir((s) => (troisieme = ajouterPage(s, "Annexe").id));
    e.supprimerPage(troisieme);
    expect(e.cable).toBeNull();
  });

  it("un câble branché d'une page à l'autre se dessine en renvoi sur chacune, chaque bout sous sa clé", () => {
    const { schema, coeur, sw, montee, etage1, etage2 } = deuxPages();
    const ici = renvoisDeLaPage(schema, etage1);
    const la = renvoisDeLaPage(schema, etage2);
    expect(ici.map((r) => [r.lien.id, r.cle, r.equipement])).toEqual([[montee.id, "renvoi-a", coeur.id]]);
    expect(la.map((r) => [r.lien.id, r.cle, r.equipement])).toEqual([[montee.id, "renvoi-b", sw.id]]);
    expect(ici[0]?.centre).toEqual(ici[0]?.base);
  });

  it("une pastille déplacée garde son écart à sa place calculée, par bout de câble, et suit son équipement", () => {
    const { schema, coeur, montee, etage1, etage2 } = deuxPages();
    const avant = renvoisDeLaPage(schema, etage1)[0];
    if (!avant) throw new Error("un renvoi attendu");
    placer(schema, { genre: "renvoi", id: montee.id, cle: "renvoi-a" }, { x: 40, y: -60 });
    const apres = renvoisDeLaPage(schema, etage1)[0];
    expect(apres?.base).toEqual(avant.base);
    expect(apres?.centre).toEqual({ x: avant.base.x + 40, y: avant.base.y - 60 });
    // L'autre bout, sur l'autre page, n'a pas bougé.
    const enFace = renvoisDeLaPage(schema, etage2)[0];
    expect(enFace?.centre).toEqual(enFace?.base);
    // L'équipement bouge : la pastille le suit, au même écart.
    coeur.position = { x: coeur.position.x + 100, y: coeur.position.y + 20 };
    const suivie = renvoisDeLaPage(schema, etage1)[0];
    if (!suivie) throw new Error("un renvoi attendu");
    expect(suivie.centre).toEqual({ x: suivie.base.x + 40, y: suivie.base.y - 60 });
    // Le dessin (aperçu, fiche, impression) la pose là aussi.
    expect(dessinerSchema(schema, { page: etage1 })).toContain(`x="${(suivie.centre.x - suivie.largeur / 2).toFixed(1)}"`);
    // « Remettre à sa place » l'oublie.
    remettre(schema, { genre: "renvoi", id: montee.id, cle: "renvoi-a" });
    expect(schema.liens.find((l) => l.id === montee.id)?.poses).toBeUndefined();
    expect(renvoisDeLaPage(schema, etage1)[0]?.centre).toEqual(suivie.base);
  });

  it("la place d'une pastille voyage dans le fichier au format 3 ; un schéma d'une page n'en garde rien", () => {
    const { schema, montee, etage2, lien } = deuxPages();
    placer(schema, { genre: "renvoi", id: montee.id, cle: "renvoi-b" }, { x: 12, y: 30 });
    placer(schema, { genre: "lien", id: montee.id, cle: "nom" }, { t: 0.5, x: 0, y: 8 });
    const texte = ecrireSchema(schema);
    const relu = lireSchema(texte).schema;
    expect(relu.format).toBe(3);
    expect(relu.liens.find((l) => l.id === montee.id)?.poses).toEqual({
      nom: { place: { t: 0.5, x: 0, y: 8 } },
      "renvoi-b": { place: { x: 12, y: 30 } },
    });
    expect(ecrireSchema(relu)).toBe(texte);
    // Un câble dont les deux bouts sont sur la même page n'a pas de renvoi : une place lue pour lui est oubliée.
    const brut = JSON.parse(texte);
    brut.liens.find((l: { id: string }) => l.id === lien.id).poses = { "renvoi-a": { place: { x: 5, y: 5 } } };
    expect(lireSchema(brut).schema.liens.find((l) => l.id === lien.id)?.poses).toBeUndefined();
    // La page supprimée, le schéma revient au format 2 sans trace des renvois.
    supprimerPage(schema, etage2);
    const seul = ecrireSchema(schema);
    expect(JSON.parse(seul).format).toBe(2);
    expect(seul).not.toContain("renvoi");
  });
});
