// @vitest-environment node
// Réunir des schémas en un seul (31C) : l'ordre et le nom des pages, des identifiants tous neufs, les VLAN et les
// modèles réunis, les doublons, et des originaux jamais modifiés.
import { describe, expect, it } from "vitest";
import { ecrireSchema, erreursStructurelles, lireSchema } from "./format";
import type { Equipement, Interface, Schema } from "./modele";
import { schemaVide } from "./modele";
import { connecter } from "./operations";
import { ajouterPage, renommerPage, renvoisDeLaPage, surLaPage } from "./pages";
import {
  cleEquipement,
  dansLaListe,
  doublonsEntre,
  estVierge,
  extrairePages,
  fusionner,
  nomDePageUnique,
  renumeroter,
  reunir,
  titrePropose,
} from "./reunion";

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

/** Un équipement posé à la main : son nom, ses ports, son adresse de gestion. */
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
 * Le lycée en deux pages : l'étage 1 (le cœur, SW-1 et sa zone, une note groupée avec SW-1), l'étage 2 (SW-2) ;
 * un câble de la montée entre les deux. VLAN 10 « Admin » et 20 « Wi-Fi », un modèle C9300, un agrégat, une relation.
 */
function lycee(): Schema {
  const schema: Schema = { ...schemaVide("Réseau du lycée"), id: "s_lycee" };
  const coeur = poser(schema, "d_coeur", "Cœur", ["1/1", "1/2", "1/3"], { gestion: { adresse: "10.0.0.1/24" } });
  const sw1 = poser(schema, "d_sw1", "SW-1", ["1/25", "1/26"], { membres: [{ numero: 1, modele: "m_c9300" }] });
  sw1.interfaces[0] = { ...port("1/25"), id: "i_s25", vlan: { mode: "trunk", tagues: "10,20" } };
  schema.modeles.push({ id: "m_c9300", type: "switch", fabricant: "Cisco", reference: "C9300-48P", groupes: [] });
  schema.vlans.push(
    { id: "v_10", numero: 10, nom: "Admin", sousReseaux: ["10.0.10.0/24"] },
    { id: "v_20", numero: 20, nom: "Wi-Fi", sousReseaux: ["10.0.20.0/24"] },
  );
  connecter(schema, bout(coeur, "1/1"), bout(sw1, "1/25"));
  schema.agregats.push({ id: "a_po1", equipement: coeur.id, nom: "Po1", mode: "lacp", membres: [coeur.interfaces[2]?.id ?? ""] });
  schema.relations.push({ id: "r_dep", genre: "dependance", de: sw1.id, vers: coeur.id, statut: "actif", proprietes: {}, points: [] });
  schema.groupes.push({ id: "g_hall", nom: "Hall", genre: "zone", membres: [sw1.id], position: { x: -50, y: -50 }, taille: { largeur: 300, hauteur: 200 } });
  schema.annotations.push({ id: "n_baie", texte: "Baie du hall", position: { x: 0, y: 200 } });
  schema.ensembles = [{ id: "e_lot", membres: [sw1.id, "n_baie"] }];
  renommerPage(schema, "", "Étage 1");
  const etage2 = ajouterPage(schema, "Étage 2");
  const sw2 = poser(schema, "d_sw2", "SW-2", ["1/48"], { page: etage2.id });
  connecter(schema, bout(coeur, "1/2"), bout(sw2, "1/48"));
  return lireSchema(ecrireSchema(schema)).schema;
}

/** La salle serveurs, d'une page : le même cœur (même nom, même adresse), un serveur ; VLAN 20 « Invités », 30 « Serveurs ». */
function salle(): Schema {
  const schema: Schema = { ...schemaVide("Salle serveurs B012"), id: "s_salle" };
  const coeur = poser(schema, "d_coeur", "Cœur", ["1/1", "1/2", "1/9"], { gestion: { adresse: "10.0.0.1/22" }, membres: [{ numero: 1, modele: "m_autre" }] });
  const srv = poser(schema, "d_srv", "Serveur", ["eth0"], { type: "serveur" });
  connecter(schema, bout(coeur, "1/9"), bout(srv, "eth0"));
  schema.modeles.push({ id: "m_autre", type: "switch", fabricant: "cisco", reference: "c9300-48p ", groupes: [] });
  schema.vlans.push(
    { id: "v_20", numero: 20, nom: "Invités", sousReseaux: ["10.0.21.0/24"] },
    { id: "v_30", numero: 30, nom: "Serveurs", sousReseaux: [] },
  );
  return lireSchema(ecrireSchema(schema)).schema;
}

/** Tous les identifiants d'un schéma, interfaces comprises. */
function identifiants(schema: Schema): string[] {
  return [
    ...(schema.pages ?? []).map((p) => p.id),
    ...schema.modeles.map((m) => m.id),
    ...schema.equipements.flatMap((e) => [e.id, ...e.interfaces.map((i) => i.id)]),
    ...schema.liens.map((l) => l.id),
    ...schema.relations.map((r) => r.id),
    ...schema.vlans.map((v) => v.id),
    ...schema.agregats.map((a) => a.id),
    ...schema.groupes.map((g) => g.id),
    ...schema.annotations.map((n) => n.id),
    ...(schema.ensembles ?? []).map((e) => e.id),
  ];
}

describe("réunir : les pages", () => {
  it("les pages viennent dans l'ordre choisi : un schéma d'une page devient une page à son titre, les autres gardent les leurs", () => {
    const { schema, pagesParSchema } = reunir([lycee(), salle()], { titre: "Réseau du lycée — complet" });
    expect(schema.titre).toBe("Réseau du lycée — complet");
    expect(schema.pages?.map((p) => p.nom)).toEqual(["Étage 1", "Étage 2", "Salle serveurs B012"]);
    expect(pagesParSchema.map((p) => p.length)).toEqual([2, 1]);
    expect(schema.format).toBe(3);

    const inverse = reunir([salle(), lycee()], { titre: "Autre ordre" }).schema;
    expect(inverse.pages?.map((p) => p.nom)).toEqual(["Salle serveurs B012", "Étage 1", "Étage 2"]);
    // Chaque équipement est sur la page de son schéma.
    const pageDe = (nom: string) => inverse.pages?.find((p) => p.id === inverse.equipements.find((e) => e.nom === nom)?.page)?.nom;
    expect(pageDe("Serveur")).toBe("Salle serveurs B012");
    expect(pageDe("SW-2")).toBe("Étage 2");
  });

  it("un nom de page déjà pris est préfixé du titre de son schéma, puis numéroté", () => {
    const annexe = lycee();
    annexe.titre = "Annexe";
    const { schema } = reunir([lycee(), annexe, { ...salle(), titre: "Étage 1" }, { ...salle(), titre: "Étage 1" }], { titre: "Tout" });
    expect(schema.pages?.map((p) => p.nom)).toEqual([
      "Étage 1",
      "Étage 2",
      "Annexe · Étage 1",
      "Annexe · Étage 2",
      "Étage 1 · Étage 1",
      "Étage 1 · Étage 1 (2)",
    ]);
    expect(nomDePageUnique(new Set(["a"]), "A")).toBe("A (2)");
    expect(nomDePageUnique(new Set(), "  ")).toBe("Page 1");
    expect(nomDePageUnique(new Set(["x".repeat(80).toLowerCase()]), "x".repeat(90), "P").length).toBeLessThanOrEqual(80);
  });

  it("un schéma vide donne une page vide à son titre ; un titre vide reprend celui du premier", () => {
    const vide = { ...schemaVide("Brouillon"), id: "s_vide" };
    const { schema } = reunir([vide, salle()], { titre: "  " });
    expect(schema.titre).toBe("Brouillon");
    expect(schema.pages?.map((p) => p.nom)).toEqual(["Brouillon", "Salle serveurs B012"]);
    expect(surLaPage(schema, schema.pages?.[0]?.id).equipements).toEqual([]);
    expect(reunir([], { titre: "" }).schema.titre).toBe("Schéma réseau");
  });

  it("la vue, l'affichage et le tracé des câbles sont ceux du premier", () => {
    const premier = { ...salle(), vue: "logique" as const, cablesCoudes: true, affichage: { ports: false } };
    const { schema } = reunir([premier, lycee()], { titre: "T" });
    expect(schema.vue).toBe("logique");
    expect(schema.cablesCoudes).toBe(true);
    expect(schema.affichage).toEqual({ ports: false });
  });
});

describe("réunir : les identifiants", () => {
  it("tous sont neufs, sans doublon, et aucun n'est celui d'un original ; le schéma s'enregistre et se relit", () => {
    const a = lycee();
    const b = salle();
    const { schema } = reunir([a, b, lycee()], { titre: "Trois" });
    const ids = identifiants(schema);
    // Les interfaces n'ont à être uniques que dans leur équipement ; elles le sont ici partout.
    expect(new Set(ids).size).toBe(ids.length);
    const anciens = new Set([...identifiants(a), ...identifiants(b)]);
    expect(ids.filter((id) => anciens.has(id))).toEqual([]);
    expect(erreursStructurelles(schema)).toEqual([]);
    const relu = lireSchema(ecrireSchema(schema));
    expect(relu.erreurs).toEqual([]);
    expect(relu.schema.format).toBe(3);
    expect(relu.schema.equipements).toHaveLength(a.equipements.length * 2 + b.equipements.length);
    expect(relu.schema.liens).toHaveLength(a.liens.length * 2 + b.liens.length);
  });

  it("chaque référence suit : bouts de câble, agrégat, relation, cadre, ensemble, modèle, page", () => {
    const { schema } = reunir([lycee(), salle()], { titre: "T" });
    const coeur = schema.equipements.find((e) => e.nom === "Cœur" && e.gestion?.adresse === "10.0.0.1/24");
    const sw1 = schema.equipements.find((e) => e.nom === "SW-1");
    const sw2 = schema.equipements.find((e) => e.nom === "SW-2");
    if (!coeur || !sw1 || !sw2) throw new Error("équipements manquants");
    const etage1 = schema.pages?.[0]?.id;
    expect(coeur.page).toBe(etage1);
    expect(schema.liens.some((l) => l.a.equipement === coeur.id && l.b.equipement === sw1.id && l.b.interface === sw1.interfaces[0]?.id)).toBe(true);
    expect(schema.agregats[0]).toMatchObject({ equipement: coeur.id, membres: [coeur.interfaces[2]?.id] });
    expect(schema.relations[0]).toMatchObject({ de: sw1.id, vers: coeur.id });
    expect(schema.groupes[0]).toMatchObject({ membres: [sw1.id], page: etage1 });
    const note = schema.annotations.find((n) => n.texte === "Baie du hall");
    expect(schema.ensembles).toEqual([{ id: expect.stringMatching(/^e_/), membres: [sw1.id, note?.id] }]);
    expect(sw1.membres[0]?.modele).toBe(schema.modeles[0]?.id);
    // Le câble de la montée va toujours d'une page à l'autre : un renvoi sur chacune.
    expect(renvoisDeLaPage(schema, schema.pages?.[1]?.id).map((r) => r.texte)).toEqual(["→ Étage 1 · Cœur · 1/2"]);
  });

  it("renuméroter suit aussi l'interface parente, l'interface de gestion et les relations entre ports", () => {
    const schema = lycee();
    const coeur = schema.equipements[0];
    if (!coeur) throw new Error("cœur manquant");
    coeur.interfaces.push(port("Vlan10", { id: "i_svi", nature: "vlan", parent: coeur.interfaces[0]?.id }));
    coeur.gestion = { ...coeur.gestion, interface: "i_svi" };
    schema.relations[0] = { ...schema.relations[0]!, deInterface: schema.equipements[1]?.interfaces[0]?.id, versInterface: "i_svi" };
    const copie = renumeroter(schema);
    const neuf = copie.equipements[0];
    const svi = neuf?.interfaces.find((i) => i.nom === "Vlan10");
    expect(svi?.id).not.toBe("i_svi");
    expect(svi?.parent).toBe(neuf?.interfaces[0]?.id);
    expect(neuf?.gestion?.interface).toBe(svi?.id);
    expect(copie.relations[0]?.versInterface).toBe(svi?.id);
    expect(copie.relations[0]?.deInterface).toBe(copie.equipements[1]?.interfaces[0]?.id);
    expect(erreursStructurelles(copie)).toEqual([]);
    // Une interface de gestion qui n'existe plus est oubliée.
    coeur.gestion = { interface: "i_absente" };
    expect(renumeroter(schema).equipements[0]?.gestion).toEqual({});
  });
});

describe("réunir : VLAN, modèles, originaux", () => {
  it("les VLAN se réunissent par numéro : le premier nom est gardé, l'écart se dit, les sous-réseaux s'ajoutent", () => {
    const { schema, avertissements } = reunir([lycee(), salle()], { titre: "T" });
    expect(schema.vlans.map((v) => `${v.numero} ${v.nom}`)).toEqual(["10 Admin", "20 Wi-Fi", "30 Serveurs"]);
    expect(schema.vlans.find((v) => v.numero === 20)?.sousReseaux).toEqual(["10.0.20.0/24", "10.0.21.0/24"]);
    expect(avertissements).toEqual(["VLAN 20 : « Wi-Fi » d'un côté, « Invités » de l'autre. Le nom du premier schéma est gardé."]);
    // Deux périmètres : deux VLAN distincts, sans avertissement.
    const site = salle();
    for (const v of site.vlans) v.perimetre = "Site B";
    const deux = reunir([lycee(), site], { titre: "T" });
    expect(deux.schema.vlans.filter((v) => v.numero === 20)).toHaveLength(2);
    expect(deux.avertissements).toEqual([]);
  });

  it("un VLAN sans couleur ni description les prend du suivant", () => {
    const b = salle();
    b.vlans[0] = { ...b.vlans[0]!, couleur: "#167a5a", description: "Invités du CDI" };
    const { schema } = reunir([lycee(), b], { titre: "T" });
    expect(schema.vlans.find((v) => v.numero === 20)).toMatchObject({ nom: "Wi-Fi", couleur: "#167a5a", description: "Invités du CDI" });
  });

  it("les modèles de matériel se réunissent par référence (et fabricant), sans tenir compte de la casse", () => {
    const { schema } = reunir([lycee(), salle()], { titre: "T" });
    expect(schema.modeles).toHaveLength(1);
    const coeurSalle = schema.equipements.find((e) => e.nom === "Cœur" && e.gestion?.adresse === "10.0.0.1/22");
    expect(coeurSalle?.membres[0]?.modele).toBe(schema.modeles[0]?.id);
  });

  it("les originaux ne sont jamais modifiés", () => {
    const a = lycee();
    const b = salle();
    const avant = [JSON.stringify(a), JSON.stringify(b)];
    reunir([a, b], { titre: "T", reunirDoublons: true });
    reunir([b, a], { titre: "T" });
    expect([JSON.stringify(a), JSON.stringify(b)]).toEqual(avant);
  });
});

describe("réunir : ce qui se retrouve des deux côtés", () => {
  it("par défaut, chacun garde les siens : deux « Cœur », un sur chaque page", () => {
    expect(doublonsEntre([lycee(), salle()])).toEqual(["Cœur"]);
    const { schema, reunis } = reunir([lycee(), salle()], { titre: "T" });
    expect(schema.equipements.filter((e) => e.nom === "Cœur")).toHaveLength(2);
    expect(reunis).toEqual([]);
  });

  it("sur demande, un seul « Cœur » : ses câbles vers l'autre page deviennent des renvois", () => {
    const { schema, reunis, renvois } = reunir([lycee(), salle()], { titre: "T", reunirDoublons: true });
    const coeurs = schema.equipements.filter((e) => e.nom === "Cœur");
    expect(coeurs).toHaveLength(1);
    expect(reunis).toEqual(["Cœur"]);
    expect(renvois).toBe(1);
    const salleServeurs = schema.pages?.[2]?.id;
    expect(renvoisDeLaPage(schema, salleServeurs).map((r) => r.texte)).toEqual(["→ Étage 1 · Cœur · 1/9"]);
    // Le port 1/9, qu'il n'avait pas, lui est ajouté ; le serveur y est branché.
    expect(coeurs[0]?.interfaces.map((i) => i.nom)).toEqual(["1/1", "1/2", "1/3", "1/9"]);
    expect(erreursStructurelles(schema)).toEqual([]);
  });

  it("un même câble des deux côtés ne se double pas ; un port pris des deux côtés en reçoit un autre, et cela se dit", () => {
    const a = lycee();
    const b: Schema = { ...schemaVide("Maquette VLAN"), id: "s_maquette" };
    const coeur = poser(b, "d_c", "Cœur", ["1/1", "1/2"], { gestion: { adresse: "10.0.0.1" } });
    const sw1 = poser(b, "d_s", "SW-1", ["1/25"]);
    const ap = poser(b, "d_ap", "Borne", ["eth0"]);
    connecter(b, bout(coeur, "1/1"), bout(sw1, "1/25"));
    connecter(b, bout(coeur, "1/2"), bout(ap, "eth0"));
    const { schema, avertissements } = reunir([a, b], { titre: "T", reunirDoublons: true });
    expect(schema.equipements.filter((e) => e.nom === "SW-1")).toHaveLength(1);
    // Cœur–SW-1 était dans les deux : un seul câble ; Cœur 1/2 allait à SW-2 d'un côté, à la borne de l'autre.
    const coeurGarde = schema.equipements.find((e) => e.nom === "Cœur");
    expect(schema.liens.filter((l) => l.a.equipement === coeurGarde?.id || l.b.equipement === coeurGarde?.id)).toHaveLength(3);
    expect(coeurGarde?.interfaces.map((i) => i.nom)).toContain("1/2 (2)");
    expect(avertissements).toContain("« Cœur » : le port « 1/2 » est câblé dans les deux schémas ; le second câble passe sur « 1/2 (2) ».");
    expect(erreursStructurelles(schema)).toEqual([]);
  });

  it("une relation ou un agrégat déjà là ne se doublent pas, une relation devenue d'un équipement à lui-même part", () => {
    const a = lycee();
    const b = lycee();
    b.titre = "Copie";
    const sw1 = b.equipements.find((e) => e.nom === "SW-1");
    const sw2 = b.equipements.find((e) => e.nom === "SW-2");
    if (!sw1 || !sw2) throw new Error("manquants");
    sw2.nom = "SW-1";
    b.relations.push({ id: "r_soi", genre: "cluster", de: sw1.id, vers: sw2.id, statut: "actif", proprietes: {}, points: [] });
    const { schema } = reunir([a, b], { titre: "T", reunirDoublons: true });
    expect(schema.relations).toHaveLength(1);
    expect(schema.agregats).toHaveLength(1);
    expect(erreursStructurelles(schema)).toEqual([]);
  });

  it("deux équipements sans adresse vont ensemble ; une adresse différente les sépare", () => {
    const e = (nom: string, adresse?: string) =>
      ({ nom, interfaces: [], ...(adresse ? { gestion: { adresse } } : {}) }) as unknown as Equipement;
    expect(cleEquipement(e(" Cœur "))).toBe(cleEquipement(e("cœur")));
    expect(cleEquipement(e("Cœur", "10.0.0.1/24"))).toBe(cleEquipement(e("CŒUR", "10.0.0.1")));
    expect(cleEquipement(e("Cœur", "10.0.0.1"))).not.toBe(cleEquipement(e("Cœur", "10.0.0.2")));
    const avecPort = { nom: "PC", interfaces: [port("eth0", { adresses: [{ adresse: "192.168.1.5", prefixe: 24 }] })] } as unknown as Equipement;
    expect(cleEquipement(avecPort)).toBe("pc|192.168.1.5");
  });
});

describe("réunir : outils", () => {
  it("un numéro dans une liste de VLAN", () => {
    expect(dansLaListe("10,20,30-40", 35)).toBe(true);
    expect(dansLaListe("10, 20", 20)).toBe(true);
    expect(dansLaListe("10,20,30-40", 41)).toBe(false);
    expect(dansLaListe(undefined, 1)).toBe(false);
    expect(dansLaListe("a,,", 1)).toBe(false);
  });

  it("le nom proposé, un schéma vierge, une fusion dans une cible vide", () => {
    expect(titrePropose([{ titre: "Réseau du lycée" }, { titre: "B" }])).toBe("Réseau du lycée — complet");
    expect(titrePropose([])).toBe("Schéma réseau");
    expect(estVierge(schemaVide())).toBe(true);
    expect(estVierge(salle())).toBe(false);
    const cible = schemaVide("Vide");
    const { morceau } = extrairePages(salle(), undefined, "Salle");
    const fusion = fusionner(cible, renumeroter(morceau));
    expect(cible.pages?.map((p) => p.nom)).toEqual(["Salle"]);
    expect(cible.format).toBe(2);
    expect(cible.equipements.every((e) => e.page === undefined)).toBe(true);
    expect(fusion.equipements).toBe(2);
  });
});
