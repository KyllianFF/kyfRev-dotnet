import { describe, expect, it } from "vitest";
import { boiteEtiquette, mettreEnPage, occupationDe, vlanDuLien } from "./dessin";
import { diagnostiquer } from "./diagnostic";
import { ecrireSchema, lireSchema } from "./format";
import { illustration } from "./illustrations";
import { type Equipement, type Schema, schemaVide } from "./modele";
import {
  collerEquipements,
  connecter,
  copierEquipements,
  creerEquipement,
  interfaceIndisponible,
  Refus,
  relier,
} from "./operations";
import {
  agregatDe,
  basculerMembre,
  champsDe,
  configDepuis,
  configDuBout,
  creerAgregat,
  ecrireConfigDuBout,
  lireListeVlan,
  lireNumeroVlan,
  retirerAgregat,
  texteVlan,
} from "./vlans";
import { vue } from "./vues";

const port = (e: Equipement, nom: string) => {
  const i = e.interfaces.find((x) => x.nom === nom);
  if (!i) throw new Error(`port ${nom} absent`);
  return { equipement: e.id, interface: i.id };
};

/** Un switch relié à un pare-feu (1/1 ↔ port1), et un PC sur 1/5. */
function site() {
  const schema = schemaVide("Essai D51");
  const sw = creerEquipement(schema, "switch", { x: 0, y: 0 });
  const fw = creerEquipement(schema, "pare-feu", { x: 300, y: 0 });
  const pc = creerEquipement(schema, "pc", { x: 0, y: 200 });
  const lien = connecter(schema, port(sw, "1/1"), port(fw, "port1"));
  connecter(schema, port(sw, "1/5"), port(pc, "Ethernet"));
  return { schema, sw, fw, pc, lien };
}

const croisent = (
  a: { x0: number; y0: number; x1: number; y1: number },
  b: { x0: number; y0: number; x1: number; y1: number },
) => a.x0 < b.x1 && b.x0 < a.x1 && a.y0 < b.y1 && b.y0 < a.y1;

describe("VLAN des câbles (D51)", () => {
  it("saisie : numéro de 1 à 4094, liste remise au propre, invalide refusée", () => {
    expect(lireNumeroVlan("")).toBeUndefined();
    expect(lireNumeroVlan(" 20 ")).toBe(20);
    expect(lireNumeroVlan("0")).toBeNull();
    expect(lireNumeroVlan("4095")).toBeNull();
    expect(lireListeVlan("10, 20 30-40;50")).toBe("10,20,30-40,50");
    expect(lireListeVlan("  ")).toBeUndefined();
    expect(lireListeVlan("10,abc")).toBeNull();
    expect(lireListeVlan("40-30")).toBeNull();
    expect(lireListeVlan("10-20-30")).toBeNull();
  });

  it("non tagué et tagués : accès, trunk avec natif, texte du câble", () => {
    const schema = schemaVide();
    schema.vlans.push({ id: "v_1", numero: 10, nom: "Postes", couleur: "#167a5a", sousReseaux: [] });
    expect(configDepuis({ nonTague: 10 })).toEqual({ mode: "acces", acces: 10 });
    expect(configDepuis({ nonTague: 1, tagues: "10,20" })).toEqual({
      mode: "trunk",
      tagues: "10,20",
      natif: 1,
    });
    expect(configDepuis({})).toBeUndefined();
    // La voix et le PVID déjà notés sont gardés.
    expect(configDepuis({ nonTague: 10 }, { mode: "acces", voix: 50 })).toEqual({
      mode: "acces",
      acces: 10,
      voix: 50,
    });
    expect(champsDe({ mode: "trunk", natif: 1, tagues: "10,20" })).toEqual({ nonTague: 1, tagues: "10,20" });
    expect(texteVlan(schema, { mode: "acces", acces: 10 })).toEqual({ texte: "VLAN 10", couleur: "#167a5a" });
    expect(texteVlan(schema, { mode: "trunk", tagues: "10,20" })).toEqual({ texte: "T 10,20" });
    expect(texteVlan(schema, { mode: "trunk", natif: 1, tagues: "10,20" })).toEqual({
      texte: "U 1 · T 10,20",
    });
    expect(texteVlan(schema, undefined)).toBeUndefined();
  });

  it("deux bouts différents : signalé au diagnostic ; alignés, plus rien", () => {
    const { schema, lien } = site();
    ecrireConfigDuBout(schema, lien.a, { mode: "acces", acces: 10 });
    expect(diagnostiquer(schema).some((c) => c.message.includes("VLAN différents"))).toBe(true);
    ecrireConfigDuBout(schema, lien.b, { mode: "acces", acces: 10 });
    expect(diagnostiquer(schema).some((c) => c.message.includes("VLAN différents"))).toBe(false);
    expect(vlanDuLien(schema, lien)?.texte).toBe("VLAN 10");
  });
});

describe("agrégats de liens, trunks (D51)", () => {
  it("un câble devient un agrégat ; ses ports se choisissent de chaque côté", () => {
    const { schema, sw, fw, lien } = site();
    ecrireConfigDuBout(schema, lien.a, { mode: "trunk", natif: 1, tagues: "10,20" });
    creerAgregat(schema, lien.id);
    const agregat = agregatDe(schema, lien.a);
    expect(agregat?.nom).toBe("Trk1");
    expect(agregat?.mode).toBe("lacp");
    // La configuration VLAN du port passe à l'agrégat.
    expect(champsDe(configDuBout(schema, lien.a))).toEqual({ nonTague: 1, tagues: "10,20" });
    basculerMembre(schema, lien.id, "a", port(sw, "1/2").interface);
    basculerMembre(schema, lien.id, "b", port(fw, "port4").interface);
    expect(agregatDe(schema, port(sw, "1/2"))?.id).toBe(agregat?.id);
    // Membre d'un agrégat câblé : il ne prend pas d'autre câble ; le port du câble reste membre.
    expect(interfaceIndisponible(schema, port(sw, "1/2"))).toContain("Trk1");
    expect(() => basculerMembre(schema, lien.id, "a", lien.a.interface)).toThrow(Refus);
    // Un port câblé vers un autre équipement : refusé.
    expect(() => basculerMembre(schema, lien.id, "a", port(sw, "1/5").interface)).toThrow(/câblé vers/);
    // Dessin : un seul trait, son repère, les ports de l'agrégat aux bouts.
    const page = mettreEnPage(schema, vue("physique"), new Set());
    expect(page.liens.find((l) => l.lien.id === lien.id)?.agregat).toBeDefined();
    const ports = [...page.etiquettes.values()].flat().map((e) => e.texte);
    expect(ports).toContain("Trk1 · 1/1,1/2");
    expect(ports).toContain("Trk1 · port1,port4");
    // Relu sans perte, puis défait : la configuration VLAN revient au port.
    const relu = lireSchema(ecrireSchema(schema)).schema;
    expect(relu.agregats).toEqual(schema.agregats);
    retirerAgregat(schema, lien.id);
    expect(schema.agregats).toEqual([]);
    expect(champsDe(configDuBout(schema, lien.a))).toEqual({ nonTague: 1, tagues: "10,20" });
  });

  it("un câble parallèle vers le même équipement rejoint l'agrégat", () => {
    const { schema, sw, fw, lien } = site();
    const second = connecter(schema, port(sw, "1/2"), port(fw, "port2"));
    creerAgregat(schema, lien.id);
    basculerMembre(schema, lien.id, "a", port(sw, "1/2").interface);
    expect(schema.liens.some((l) => l.id === second.id)).toBe(false);
    expect(agregatDe(schema, lien.b)?.membres).toEqual([
      port(fw, "port1").interface,
      port(fw, "port2").interface,
    ]);
  });

  it("copier-coller : les agrégats suivent ; interface disparue : rangée à la lecture", () => {
    const { schema, sw, fw, lien } = site();
    creerAgregat(schema, lien.id);
    basculerMembre(schema, lien.id, "a", port(sw, "1/2").interface);
    const autre = schemaVide();
    collerEquipements(autre, copierEquipements(schema, new Set([sw.id, fw.id])));
    expect(autre.agregats.length).toBe(2);
    const copie = autre.liens[0];
    if (!copie) throw new Error("câble non copié");
    expect(agregatDe(autre, copie.a)?.membres.length).toBe(2);
    // Une interface membre retirée à la main : la relecture la retire de l'agrégat.
    const brut = JSON.parse(ecrireSchema(schema)) as Schema;
    const switchBrut = brut.equipements.find((e) => e.id === sw.id) as Equipement;
    switchBrut.interfaces = switchBrut.interfaces.filter((i) => i.nom !== "1/2");
    const lecture = lireSchema(brut);
    expect(lecture.erreurs).toEqual([]);
    expect(lecture.schema.agregats.find((a) => a.equipement === sw.id)?.membres).toEqual([lien.a.interface]);
  });
});

describe("paire haute disponibilité (D51)", () => {
  it("une adresse partagée par deux pare-feu actif/passif n'est plus signalée une fois la paire déclarée", () => {
    const schema = schemaVide();
    const fw1 = creerEquipement(schema, "pare-feu", { x: 0, y: 0 });
    const fw2 = creerEquipement(schema, "pare-feu", { x: 200, y: 0 });
    fw1.gestion = { adresse: "10.210.130.140" };
    fw2.gestion = { adresse: "10.210.130.140" };
    connecter(schema, port(fw1, "port2"), port(fw2, "port2"));
    const avant = diagnostiquer(schema).find((c) => c.message.includes("10.210.130.140"));
    expect(avant?.paire).toEqual([fw1.id, fw2.id]);
    relier(schema, { genre: "haute-disponibilite", de: fw1.id, vers: fw2.id, nom: "HA actif/passif" });
    expect(diagnostiquer(schema).some((c) => c.message.includes("10.210.130.140"))).toBe(false);
    // Dessinée sans flèche, à côté du câble qui relie déjà les deux pare-feu.
    const page = mettreEnPage(schema, vue("physique"), new Set());
    const relation = page.relations[0];
    expect(relation?.fleche).toBe(false);
    const cable = page.liens[0];
    expect(Math.abs((relation?.trace[0]?.y ?? 0) - (cable?.trace[0]?.y ?? 0))).toBeGreaterThan(10);
  });
});

describe("placement des étiquettes (D51)", () => {
  it("câbles courts : les pastilles de ports ne passent ni sous un nom ni l'une sur l'autre", () => {
    const schema = schemaVide();
    const cpe = creerEquipement(schema, "cpe", { x: 0, y: 0 });
    cpe.nom = "LASOTEL 2";
    cpe.gestion = { adresse: "10.107.30.6" };
    const fw = creerEquipement(schema, "pare-feu", { x: 0, y: 150 });
    fw.nom = "FW_Backup";
    fw.gestion = { adresse: "10.210.130.140" };
    const cs = creerEquipement(schema, "switch", { x: -160, y: 150 });
    cs.nom = "SKFRMEY-CA00A-CS";
    const sw = creerEquipement(schema, "switch", { x: 0, y: 300 });
    connecter(schema, port(cpe, "LAN1"), port(fw, "port1"));
    connecter(schema, port(cs, "1/1"), port(fw, "port2"));
    connecter(schema, port(fw, "port3"), port(sw, "1/1"));
    const trunk = schema.liens[1];
    if (trunk) ecrireConfigDuBout(schema, trunk.a, { mode: "trunk", tagues: "10,20,30-40" });
    schema.affichage = { ports: true, vlans: true };
    const page = mettreEnPage(schema, vue("physique"), new Set());
    const toutes = [...page.etiquettes.values()].flat();
    expect(toutes.filter((e) => e.genre === "port").length).toBe(6);
    const boites = toutes.map((e) => boiteEtiquette(e));
    for (let i = 0; i < boites.length; i++)
      for (let j = i + 1; j < boites.length; j++)
        expect(
          croisent(boites[i] as never, boites[j] as never),
          `${toutes[i]?.texte} / ${toutes[j]?.texte}`,
        ).toBe(false);
    for (const e of schema.equipements)
      for (const o of occupationDe(e, true))
        for (const [k, b] of boites.entries())
          expect(croisent(o, b), `${toutes[k]?.texte} sur ${e.nom}`).toBe(false);
  });

  it("le pare-feu porte sa flamme", () => {
    const dessin = illustration("brick-wall", "#c0392b");
    expect(dessin).toContain("#e8590c");
    expect(dessin).toContain("#ffd43b");
    expect(dessin).not.toContain("transform");
  });
});
