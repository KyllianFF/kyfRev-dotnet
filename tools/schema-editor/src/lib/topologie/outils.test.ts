import { describe, expect, it } from "vitest";
import { capaciteDe, ipv4, planEnCsv, planIp, reseauDe } from "./adressage";
import { affichageDe, boiteDe, boiteEtiquette, mettreEnPage, traceDuLien, vlanDuLien } from "./dessin";
import { ecrireSchema, lireSchema } from "./format";
import { GABARITS } from "./gabarits";
import { schemaVide } from "./modele";
import { collerEquipements, connecter, creerEquipement, nomDisponible } from "./operations";
import { chercherDansSchema } from "./recherche";
import { vue } from "./vues";

describe("plan d'adressage IP (D47)", () => {
  it("calculs IPv4 : réseau, capacité, adresses invalides", () => {
    expect(ipv4("192.168.1.10")).toBe(3232235786);
    expect(ipv4("192.168.1.256")).toBeNull();
    expect(ipv4("fe80::1")).toBeNull();
    expect(reseauDe("10.1.2.3", 16)).toBe("10.1.0.0/16");
    expect(reseauDe("192.168.1.130", 25)).toBe("192.168.1.128/25");
    expect(capaciteDe(24)).toBe(254);
    expect(capaciteDe(31)).toBe(2);
    expect(capaciteDe(32)).toBe(1);
  });

  it("adresses rangées par sous-réseau, VLAN déclaré, passerelle, doublons, sans masque", () => {
    const schema = schemaVide();
    const a = creerEquipement(schema, "pc", { x: 0, y: 0 });
    const b = creerEquipement(schema, "pc", { x: 100, y: 0 });
    const c = creerEquipement(schema, "serveur", { x: 200, y: 0 });
    const r = creerEquipement(schema, "routeur", { x: 300, y: 0 });
    (a.interfaces[0] as (typeof a.interfaces)[number]).adresses = [{ adresse: "192.168.1.20", prefixe: 24 }];
    (b.interfaces[0] as (typeof b.interfaces)[number]).adresses = [{ adresse: "192.168.1.20", prefixe: 24 }];
    (c.interfaces[0] as (typeof c.interfaces)[number]).adresses = [{ adresse: "192.168.1.5" }];
    a.passerelle = "192.168.1.1";
    r.gestion = { adresse: "10.0.0.1/30" };
    schema.vlans.push({ id: "v_1", numero: 10, nom: "Postes", sousReseaux: ["192.168.1.0/24"] });
    const plan = planIp(schema);
    expect(plan.total).toBe(4);
    expect(plan.doublons).toBe(2);
    expect(plan.sousReseaux.map((s) => s.reseau)).toEqual(["10.0.0.0/30", "192.168.1.0/24"]);
    const lan = plan.sousReseaux[1];
    expect(lan?.lignes.map((l) => l.adresse)).toEqual(["192.168.1.5", "192.168.1.20", "192.168.1.20"]);
    expect(lan?.vlans).toEqual(["10 Postes"]);
    expect(lan?.passerelles).toEqual(["192.168.1.1"]);
    expect(lan?.capacite).toBe(254);
    expect(plan.sousReseaux[0]?.lignes[0]?.gestion).toBe(true);
    // CSV : en-tête, une ligne par adresse, champs protégés.
    const csv = planEnCsv(plan).trim().split("\r\n");
    expect(csv).toHaveLength(5);
    expect(csv[0]).toContain("Sous-réseau;Adresse");
    expect(csv.some((l) => l.endsWith(";oui"))).toBe(true);
  });
});

describe("modèles de topologie (D48)", () => {
  it("chaque modèle se colle sans erreur, garde ses noms, ses adresses et ses zones", () => {
    for (const g of GABARITS) {
      const schema = schemaVide();
      const contenu = g.construire();
      const ids = collerEquipements(schema, contenu, { x: 0, y: 0 }, { suffixe: "", adresses: true });
      expect(ids.length, g.id).toBeGreaterThan(3);
      expect(schema.liens.length, g.id).toBeGreaterThan(2);
      expect(schema.annotations.length, g.id).toBeGreaterThan(0);
      expect(planIp(schema).total, g.id).toBeGreaterThan(2);
      expect(planIp(schema).doublons, g.id).toBe(0);
      expect(lireSchema(ecrireSchema(schema)).erreurs, g.id).toEqual([]);
      expect(
        schema.equipements.some((e) => e.nom.endsWith("copie")),
        g.id,
      ).toBe(false);
    }
    // Le VPN porte son tunnel, le lab ses relations d'hébergement.
    const vpn = schemaVide();
    collerEquipements(
      vpn,
      GABARITS.find((g) => g.id === "vpn-deux-sites")?.construire() ?? {
        equipements: [],
        liens: [],
        modeles: [],
      },
    );
    expect(vpn.relations.map((r) => r.genre)).toEqual(["tunnel"]);
  });

  it("collé deux fois : noms renumérotés, jamais en double", () => {
    const schema = schemaVide();
    const g = GABARITS[0];
    if (!g) throw new Error("aucun modèle");
    collerEquipements(schema, g.construire(), { x: 0, y: 0 }, { suffixe: "", adresses: true });
    collerEquipements(schema, g.construire(), { x: 0, y: 600 }, { suffixe: "", adresses: true });
    const noms = schema.equipements.map((e) => e.nom);
    expect(new Set(noms).size).toBe(noms.length);
    expect(noms).toContain("PC 3");
    expect(noms).toContain("Box 2");
  });

  it("nom disponible : le numéro suivant", () => {
    const schema = schemaVide();
    for (const nom of ["PC 1", "PC 2", "Pare-feu"]) {
      const e = creerEquipement(schema, "pc", { x: 0, y: 0 });
      e.nom = nom;
    }
    expect(nomDisponible(schema, "PC 1")).toBe("PC 3");
    expect(nomDisponible(schema, "Pare-feu")).toBe("Pare-feu 2");
    expect(nomDisponible(schema, "Serveur")).toBe("Serveur");
  });
});

describe("recherche dans le schéma (D49)", () => {
  it("par nom (sans accents), adresse, port, câble, annotation", () => {
    const schema = schemaVide();
    collerEquipements(
      schema,
      GABARITS.find((g) => g.id === "dmz")?.construire() ?? { equipements: [], liens: [], modeles: [] },
      { x: 0, y: 0 },
      { suffixe: "", adresses: true },
    );
    const premier = schema.liens[0];
    if (premier) premier.nom = "Fibre du local technique";
    const libelles = (t: string) =>
      chercherDansSchema(schema, t).map((x) => `${x.libelle}|${x.detail ?? ""}`);
    expect(libelles("pare feu")).toEqual([]);
    expect(libelles("PARE-FEU")[0]).toMatch(/^Pare-feu\|/);
    expect(libelles("serveur web")).toEqual(["Serveur web|Serveur physique"]);
    expect(libelles("10.0.0.101")).toEqual(["PC 1|adresse 10.0.0.101/24 (Ethernet)"]);
    expect(libelles("technique")[0]).toMatch(/^Fibre du local technique\|câble/);
    expect(libelles("dmz")[0]).toMatch(/^SW DMZ\|/);
    expect(libelles("dmz").some((l) => l.endsWith("|annotation"))).toBe(true);
    expect(chercherDansSchema(schema, "   ")).toEqual([]);
    const coeur = schema.equipements[0];
    if (coeur) coeur.nom = "Cœur";
    expect(libelles("coeur")[0]).toMatch(/^Cœur\|/);
  });
});

describe("lisibilité des schémas (D50)", () => {
  const site = () => {
    const schema = schemaVide();
    const sw = creerEquipement(schema, "switch", { x: 0, y: 0 });
    const fw = creerEquipement(schema, "pare-feu", { x: 240, y: 0 });
    const cpe = creerEquipement(schema, "cpe", { x: 0, y: -200 });
    connecter(
      schema,
      { equipement: cpe.id, interface: cpe.interfaces[1]?.id ?? "" },
      { equipement: sw.id, interface: sw.interfaces[0]?.id ?? "" },
    );
    for (const n of [1, 2]) {
      connecter(
        schema,
        { equipement: sw.id, interface: sw.interfaces[n]?.id ?? "" },
        { equipement: fw.id, interface: fw.interfaces[n]?.id ?? "" },
      );
    }
    const port = sw.interfaces[1];
    if (port) port.vlan = { mode: "acces", acces: 10 };
    schema.vlans.push({ id: "v_1", numero: 10, nom: "Serveurs", couleur: "#167a5a", sousReseaux: [] });
    return { schema, sw, fw, cpe };
  };

  it("un câble s'arrête au bord de l'illustration, pas du carré", () => {
    const { schema, sw, cpe } = site();
    const lien = schema.liens[0];
    if (!lien) throw new Error("lien absent");
    const trace = traceDuLien(schema, lien, cpe, sw);
    const boite = boiteDe(sw);
    // Le switch est plat : le câble venu d'en haut touche son dessus, bien en deçà du carré de 56.
    expect(trace.at(-1)?.y).toBeCloseTo(boite.y0, 5);
    expect(boite.y0).toBeGreaterThan(sw.position.y - 28 + 10);
  });

  it("étiquettes sans chevauchement entre elles ni sur un équipement, VLAN des câbles", () => {
    const { schema } = site();
    schema.affichage = { ports: true, vlans: true };
    const page = mettreEnPage(schema, vue("physique"), new Set());
    const toutes = [...page.etiquettes.values()].flat();
    expect(toutes.some((e) => e.genre === "vlan" && e.texte === "Access · U 10" && e.couleur === "#167a5a")).toBe(
      true,
    );
    expect(toutes.filter((e) => e.genre === "port").length).toBe(6);
    const boites = toutes.map((e) => boiteEtiquette(e));
    const croisent = (a: (typeof boites)[number], b: (typeof boites)[number]) =>
      a.x0 < b.x1 && b.x0 < a.x1 && a.y0 < b.y1 && b.y0 < a.y1;
    for (let i = 0; i < boites.length; i++)
      for (let j = i + 1; j < boites.length; j++)
        expect(
          croisent(boites[i] as never, boites[j] as never),
          `${toutes[i]?.texte} / ${toutes[j]?.texte}`,
        ).toBe(false);
    for (const e of schema.equipements)
      for (const b of boites) expect(croisent(boiteDe(e, 0), b)).toBe(false);
  });

  it("affichage : réglages du schéma relus, sinon ceux de la vue ; masqués, ils disparaissent de l'export", () => {
    const { schema } = site();
    expect(affichageDe(schema, vue("physique")).ports).toBe(true);
    expect(affichageDe(schema, vue("physique")).vlans).toBe(false);
    schema.affichage = { ports: false, adresses: false };
    const relu = lireSchema(ecrireSchema(schema)).schema;
    expect(relu.affichage).toEqual({ ports: false, adresses: false });
    const page = mettreEnPage(relu, vue("physique"), new Set());
    expect([...page.etiquettes.values()].flat().filter((e) => e.genre === "port")).toEqual([]);
    expect(vlanDuLien(relu, relu.liens[1] as (typeof relu.liens)[number])).toEqual({
      texte: "VLAN 10",
      couleur: "#167a5a",
    });
  });
});
