import { describe, expect, it } from "vitest";
import {
  ajouterInterfaceVlan,
  ajouterPlage,
  apercuDuCollage,
  apparier,
  basculerActivation,
  basculerPoe,
  bornesDeLaPlage,
  collerConfiguration,
  collerSurLesCibles,
  conseilsReseau,
  copierConfiguration,
  interfacesDeRoutage,
  mettreEnAcces,
  mettreEnTrunk,
  numeroAffiche,
  plageProposee,
  routeEntreVlan,
  suiteEntre,
  texteVlanDuPort,
  vlanSansInterface,
} from "./configuration";
import { lireSchema } from "./format";
import { type Equipement, schemaVide } from "./modele";
import { auNombreDeMembres, changerProfil, connecter, creerEquipement, Refus, renommerInterface } from "./operations";
import { configDuBout, creerAgregat } from "./vlans";

const ids = (e: Equipement, ...noms: string[]) =>
  noms.map((nom) => {
    const i = e.interfaces.find((x) => x.nom === nom);
    if (!i) throw new Error(`port ${nom} absent`);
    return i.id;
  });
const bout = (e: Equipement, nom: string) => ({ equipement: e.id, interface: ids(e, nom)[0] as string });

/** Deux switchs d'étage pareils et un cœur, reliés par 1/25. */
function etage() {
  const schema = schemaVide("Lycée");
  const coeur = creerEquipement(schema, "switch-coeur", { x: 0, y: 0 });
  const sw1 = creerEquipement(schema, "switch", { x: 300, y: 0 });
  const sw2 = creerEquipement(schema, "switch", { x: 600, y: 0 });
  connecter(schema, bout(coeur, "1/1"), bout(sw1, "1/25"));
  return { schema, coeur, sw1, sw2 };
}

describe("Le tableau des ports", () => {
  it("écrit le VLAN d'un port d'accès et celui d'un trunk comme sur l'annotation du câble", () => {
    expect(texteVlanDuPort(undefined)).toBe("");
    expect(texteVlanDuPort({ mode: "acces", acces: 20 })).toBe("20");
    expect(texteVlanDuPort({ mode: "trunk", natif: 1, tagues: "10,20,30" })).toBe("U 1 · T 10,20,30");
    expect(texteVlanDuPort({ mode: "trunk", tagues: "10" })).toBe("T 10");
  });

  it("numérote la façade d'après le numéro du port, sinon la fin de son nom", () => {
    const { sw1 } = etage();
    expect(numeroAffiche(sw1.interfaces[3] as Equipement["interfaces"][number])).toBe("4");
    expect(numeroAffiche({ id: "i", nom: "Gi1/0/17", genre: "cuivre", nature: "physique", admin: "actif", macs: [], adresses: [] })).toBe("17");
  });

  it("choisit une suite de ports avec Maj, dans un sens comme dans l'autre", () => {
    const ordre = ["a", "b", "c", "d", "e"];
    expect(suiteEntre(ordre, "b", "d")).toEqual(["b", "c", "d"]);
    expect(suiteEntre(ordre, "d", "b")).toEqual(["b", "c", "d"]);
    expect(suiteEntre(ordre, "x", "c")).toEqual(["c"]);
  });
});

describe("Plusieurs ports d'un coup", () => {
  it("met des ports en accès sur un VLAN, en gardant la voix déjà notée", () => {
    const { schema, sw1 } = etage();
    const choisis = ids(sw1, "1/4", "1/5", "1/6");
    const iface = sw1.interfaces.find((i) => i.id === choisis[0]);
    if (iface) iface.vlan = { mode: "acces", acces: 30, voix: 40 };
    expect(mettreEnAcces(schema, sw1.id, choisis, 10)).toBe(3);
    expect(configDuBout(schema, bout(sw1, "1/4"))).toEqual({ mode: "acces", acces: 10, voix: 40 });
    expect(configDuBout(schema, bout(sw1, "1/6"))).toEqual({ mode: "acces", acces: 10 });
    expect(() => mettreEnAcces(schema, sw1.id, choisis, 5000)).toThrow(Refus);
    expect(() => mettreEnAcces(schema, sw1.id, [], 10)).toThrow(Refus);
  });

  it("met des ports en trunk, VLAN natif facultatif, et refuse une liste illisible", () => {
    const { schema, sw1 } = etage();
    expect(mettreEnTrunk(schema, sw1.id, ids(sw1, "1/25", "1/26"), { natif: 1, tagues: "10, 20,30" })).toBe(2);
    expect(configDuBout(schema, bout(sw1, "1/26"))).toEqual({ mode: "trunk", natif: 1, tagues: "10,20,30" });
    mettreEnTrunk(schema, sw1.id, ids(sw1, "1/27"), { tagues: "99" });
    expect(configDuBout(schema, bout(sw1, "1/27"))).toEqual({ mode: "trunk", tagues: "99" });
    expect(() => mettreEnTrunk(schema, sw1.id, ids(sw1, "1/27"), { tagues: "" })).toThrow(Refus);
    expect(() => mettreEnTrunk(schema, sw1.id, ids(sw1, "1/27"), { tagues: "10-5" })).toThrow(Refus);
  });

  it("règle le trunk d'un port d'agrégat sur l'agrégat, que ses autres ports partagent", () => {
    const { schema, coeur, sw1 } = etage();
    const lien = schema.liens[0];
    if (!lien) throw new Error("lien absent");
    creerAgregat(schema, lien.id);
    mettreEnTrunk(schema, sw1.id, ids(sw1, "1/25"), { natif: 1, tagues: "10" });
    expect(schema.agregats.find((a) => a.equipement === sw1.id)?.vlan).toEqual({ mode: "trunk", natif: 1, tagues: "10" });
    expect(configDuBout(schema, bout(coeur, "1/1"))).toBeUndefined();
  });

  it("donne le PoE à tous, puis le retire à tous ; désactive, puis réactive", () => {
    const { schema, sw1 } = etage();
    const choisis = ids(sw1, "1/1", "1/2");
    const premier = sw1.interfaces.find((i) => i.id === choisis[0]);
    if (premier) premier.poe = { fournit: true, norme: "802.3at" };
    expect(basculerPoe(schema, sw1.id, choisis)).toBe(true);
    expect(sw1.interfaces.filter((i) => choisis.includes(i.id)).every((i) => i.poe?.fournit)).toBe(true);
    expect(basculerPoe(schema, sw1.id, choisis)).toBe(false);
    expect(premier?.poe).toEqual({ norme: "802.3at" });
    expect(sw1.interfaces.find((i) => i.id === choisis[1])?.poe).toBeUndefined();
    expect(basculerActivation(schema, sw1.id, choisis)).toBe("desactive");
    expect(basculerActivation(schema, sw1.id, choisis)).toBe("actif");
  });
});

describe("Une plage de ports", () => {
  it("prolonge le nommage d'un profil : la suite des ports, autant qu'il y en a, au réglage des ports d'accès", () => {
    const { schema, sw2 } = etage();
    const plage = plageProposee(sw2, schema.modeles, 1);
    expect(plage).toMatchObject({ motif: "{m}/{p}", de: 29, a: 56, genre: "cuivre", connecteur: "RJ45", vitesse: 1000 });
    if (!plage) return;
    expect(bornesDeLaPlage(plage)).toEqual(["1/29", "1/56"]);
    const avant = sw2.interfaces.length;
    const premier = sw2.interfaces[0]?.id;
    expect(ajouterPlage(schema, sw2.id, { ...plage, a: 32 })).toBe(4);
    const apres = schema.equipements.find((e) => e.id === sw2.id) as Equipement;
    expect(apres.interfaces.length).toBe(avant + 4);
    expect(apres.interfaces.map((i) => i.nom)).toContain("1/32");
    expect(apres.interfaces[0]?.id).toBe(premier);
    // Les ports ajoutés suivent le profil : un groupe de plus, régénéré comme les autres.
    expect(apres.membres[0]?.groupes?.at(-1)).toMatchObject({ motif: "{m}/{p}", premier: 29, nombre: 4 });
    expect(apres.interfaces.find((i) => i.nom === "1/30")?.origine).toBeDefined();
  });

  it("garde le nommage « Gi1/0/{p} » de ports ajoutés à la main, sur le bon membre d'un stack", () => {
    const schema = lireSchema(
      JSON.stringify({
        format: 2,
        titre: "Stack",
        vue: "physique",
        equipements: [
          {
            id: "d_sw",
            type: "switch",
            nom: "SW",
            position: { x: 0, y: 0 },
            membres: [{ numero: 1 }, { numero: 2 }],
            interfaces: [1, 2, 3].flatMap((p) => [
              { id: `i_a${p}`, nom: `Gi1/0/${p}`, membre: 1, genre: "cuivre", vitesse: 1000 },
              { id: `i_b${p}`, nom: `Gi2/0/${p}`, membre: 2, genre: "cuivre", vitesse: 1000 },
            ]),
          },
        ],
      }),
    ).schema;
    const sw = schema.equipements[0] as Equipement;
    const plage = plageProposee(sw, schema.modeles, 2);
    expect(plage).toMatchObject({ motif: "Gi2/0/{p}", de: 4, a: 6, membre: 2 });
    if (!plage) return;
    expect(ajouterPlage(schema, sw.id, plage)).toBe(3);
    expect(sw.interfaces.filter((i) => i.membre === 2).map((i) => i.nom)).toEqual([
      "Gi2/0/1",
      "Gi2/0/2",
      "Gi2/0/3",
      "Gi2/0/4",
      "Gi2/0/5",
      "Gi2/0/6",
    ]);
    expect(sw.interfaces.find((i) => i.nom === "Gi2/0/6")).toMatchObject({ numero: 6, vitesse: 1000, nature: "physique" });
    expect(() => ajouterPlage(schema, sw.id, { ...plage, de: 6, a: 8 })).toThrow("Gi2/0/6");
    expect(() => ajouterPlage(schema, sw.id, { ...plage, de: 9, a: 8 })).toThrow(Refus);
    expect(() => ajouterPlage(schema, sw.id, { ...plage, motif: "Gi2/0/x", de: 9, a: 10 })).toThrow(Refus);
  });

  it("ne propose rien à un équipement sans port physique", () => {
    const schema = schemaVide();
    const internet = creerEquipement(schema, "internet", { x: 0, y: 0 });
    expect(plageProposee(internet, schema.modeles, 1)).toBeNull();
  });

  it("ajoute sur un membre du stack sans toucher aux autres", () => {
    const { schema, sw2 } = etage();
    expect(changerProfil(schema, sw2.id, auNombreDeMembres(2)).applique).toBe(true);
    const plage = plageProposee(sw2, schema.modeles, 2);
    expect(plage).toMatchObject({ membre: 2, de: 29 });
    if (!plage) return;
    ajouterPlage(schema, sw2.id, { ...plage, a: 30 });
    const noms = sw2.interfaces.map((i) => i.nom);
    expect(noms).toContain("2/30");
    expect(noms).not.toContain("1/30");
  });
});

describe("Copier la configuration", () => {
  it("recopie le mode, les VLAN, l'activation et le PoE des ports de même nom, et le VLAN de gestion", () => {
    const { schema, sw1, sw2, coeur } = etage();
    mettreEnAcces(schema, sw1.id, ids(sw1, "1/1", "1/2"), 10);
    mettreEnTrunk(schema, sw1.id, ids(sw1, "1/25"), { natif: 999, tagues: "10,20" });
    basculerPoe(schema, sw1.id, ids(sw1, "1/1"));
    basculerActivation(schema, sw1.id, ids(sw1, "1/24"));
    sw1.gestion = { adresse: "10.0.99.11/24", vlan: 99 };
    basculerPoe(schema, sw2.id, ids(sw2, "1/3"));
    const copie = copierConfiguration(schema, sw1.id);
    expect(copie).toMatchObject({ source: sw1.id, nom: sw1.nom, gestionVlan: 99 });

    expect(collerConfiguration(schema, sw2.id, copie)).toEqual({ regles: 28, parPlace: 0, absents: [], gestion: true });
    expect(configDuBout(schema, bout(sw2, "1/2"))).toEqual({ mode: "acces", acces: 10 });
    expect(configDuBout(schema, bout(sw2, "1/25"))).toEqual({ mode: "trunk", natif: 999, tagues: "10,20" });
    expect(sw2.interfaces.find((i) => i.nom === "1/1")?.poe?.fournit).toBe(true);
    expect(sw2.interfaces.find((i) => i.nom === "1/3")?.poe).toBeUndefined();
    expect(sw2.interfaces.find((i) => i.nom === "1/24")?.admin).toBe("desactive");
    // L'adresse de gestion, les noms et les câbles restent ceux de la cible.
    expect(sw2.gestion).toEqual({ vlan: 99 });
    expect(sw2.nom).not.toBe(sw1.nom);

    // Un cœur porte les mêmes noms de ports : il suit. Un petit switch ne les a pas : ils sont nommés, rien n'est créé,
    // et sans port en commun la gestion ne suit pas non plus.
    const { regles, absents } = collerConfiguration(schema, coeur.id, copie);
    expect(regles).toBe(28);
    expect(absents).toEqual([]);
    const petit = creerEquipement(schema, "switch-non-administrable", { x: 0, y: 300 });
    const resultat = collerConfiguration(schema, petit.id, copie);
    expect(resultat.regles).toBe(0);
    expect(resultat.gestion).toBe(false);
    expect(resultat.absents).toHaveLength(28);
    expect(petit.interfaces).toHaveLength(8);
    expect(petit.gestion).toBeUndefined();
    expect(() => collerConfiguration(schema, sw1.id, copie)).toThrow(Refus);
  });

  /** Un troisième switch du même modèle, dont les ports s'appellent « Gi1/0/1 » : même membre, même numéro. */
  function autreNommage(schema: ReturnType<typeof etage>["schema"]) {
    const sw3 = creerEquipement(schema, "switch", { x: 900, y: 0 });
    for (const i of sw3.interfaces) renommerInterface(schema, { equipement: sw3.id, interface: i.id }, `Gi1/0/${i.numero}`);
    return sw3;
  }

  it("propose la place des ports quand leurs noms diffèrent, et la reprend : même membre, même numéro", () => {
    const { schema, sw1 } = etage();
    const sw3 = autreNommage(schema);
    mettreEnAcces(schema, sw1.id, ids(sw1, "1/1", "1/2"), 10);
    mettreEnTrunk(schema, sw1.id, ids(sw1, "1/25"), { natif: 999, tagues: "10,20" });
    sw1.gestion = { vlan: 99 };
    const copie = copierConfiguration(schema, sw1.id);

    expect(apercuDuCollage(sw3, copie)).toEqual({ parNom: 0, parPlace: 28, exemple: { de: "1/1", vers: "Gi1/0/1" } });
    // Par le nom seulement : rien en commun, rien ne change.
    expect(apparier(sw3, copie, "nom")).toEqual([]);
    expect(collerConfiguration(schema, sw3.id, copie, "nom")).toMatchObject({ regles: 0, gestion: false });
    expect(sw3.gestion).toBeUndefined();
    // Par la place : les 28 ports.
    expect(collerConfiguration(schema, sw3.id, copie, "place")).toMatchObject({ regles: 28, parPlace: 28, absents: [] });
    expect(configDuBout(schema, bout(sw3, "Gi1/0/2"))).toEqual({ mode: "acces", acces: 10 });
    expect(configDuBout(schema, bout(sw3, "Gi1/0/25"))).toEqual({ mode: "trunk", natif: 999, tagues: "10,20" });
    expect(sw3.gestion).toEqual({ vlan: 99 });
  });

  it("garde le nom d'abord : la place ne sert qu'aux ports qui n'ont pas trouvé le leur", () => {
    const { schema, sw1, sw2 } = etage();
    // Deux ports de la cible renommés : les autres gardent leur nom, eux sont trouvés par leur place.
    renommerInterface(schema, bout(sw2, "1/3"), "Bureau 3");
    renommerInterface(schema, bout(sw2, "1/4"), "Bureau 4");
    mettreEnAcces(schema, sw1.id, ids(sw1, "1/3", "1/4", "1/5"), 30);
    const copie = copierConfiguration(schema, sw1.id);
    expect(apercuDuCollage(sw2, copie)).toMatchObject({ parNom: 26, parPlace: 2, exemple: { de: "1/3", vers: "Bureau 3" } });
    expect(collerConfiguration(schema, sw2.id, copie, "place")).toMatchObject({ regles: 28, parPlace: 2 });
    expect(configDuBout(schema, bout(sw2, "Bureau 4"))).toEqual({ mode: "acces", acces: 30 });
    expect(configDuBout(schema, bout(sw2, "1/5"))).toEqual({ mode: "acces", acces: 30 });
  });

  it("d'un switch vers un autre type ou un autre nombre de ports : ce qui se correspond, le reste laissé de côté", () => {
    const { schema, sw1 } = etage();
    mettreEnAcces(schema, sw1.id, ids(sw1, "1/1"), 10);
    const petit = creerEquipement(schema, "switch-non-administrable", { x: 0, y: 300 });
    // « 1 » à « 8 » : membre 1, numéros 1 à 8, comme « 1/1 » à « 1/8 ».
    expect(apercuDuCollage(petit, copierConfiguration(schema, sw1.id))).toMatchObject({ parNom: 0, parPlace: 8 });
    const r = collerConfiguration(schema, petit.id, copierConfiguration(schema, sw1.id), "place");
    expect(r).toMatchObject({ regles: 8, parPlace: 8 });
    expect(r.absents).toHaveLength(20);
    expect(configDuBout(schema, bout(petit, "1"))).toEqual({ mode: "acces", acces: 10 });
    // Une borne Wi-Fi n'a aucun port numéroté comme eux : rien en commun.
    const borne = creerEquipement(schema, "point-acces", { x: 300, y: 300 });
    expect(apercuDuCollage(borne, copierConfiguration(schema, sw1.id))).toEqual({ parNom: 0, parPlace: 0 });
  });

  it("dit toujours ce qui s'est passé, et refuse quand rien n'aurait changé", () => {
    const { schema, sw1, sw2, coeur } = etage();
    const sw3 = autreNommage(schema);
    sw1.gestion = { vlan: 99 };
    const copie = copierConfiguration(schema, sw1.id);
    expect(collerSurLesCibles(schema, [sw2.id], copie)).toBe(
      `Configuration de « ${sw1.nom} » collée sur « ${sw2.nom} » : 28 ports repris, VLAN de gestion 99. Ctrl+Z annule.`,
    );
    expect(collerSurLesCibles(schema, [sw3.id], copie)).toBe(
      `Configuration de « ${sw1.nom} » collée sur « ${sw3.nom} » : 28 ports repris, par leur place, VLAN de gestion 99. Ctrl+Z annule.`,
    );
    expect(collerSurLesCibles(schema, [sw2.id, coeur.id], copie)).toBe(
      `Configuration de « ${sw1.nom} » collée sur 2 équipements : 56 ports repris, VLAN de gestion 99. Ctrl+Z annule.`,
    );
    expect(() => collerSurLesCibles(schema, [sw3.id], copie, "nom")).toThrow(
      `Aucun port de même nom sur « ${sw3.nom} » : rien n'a changé.`,
    );
    const borne = creerEquipement(schema, "routeur", { x: 0, y: 600 });
    const r = (() => {
      try {
        return collerSurLesCibles(schema, [sw2.id, borne.id], copie, "nom");
      } catch (e) {
        return (e as Error).message;
      }
    })();
    expect(r).toContain(`collée sur « ${sw2.nom} »`);
    expect(r).toContain(`Aucun port de même nom sur « ${borne.nom} » : laissé tel quel.`);
    expect(() => collerSurLesCibles(schema, [sw1.id], copie)).toThrow(Refus);
  });

  it("garde sa copie intacte quand la source change ensuite", () => {
    const { schema, sw1, sw2 } = etage();
    mettreEnAcces(schema, sw1.id, ids(sw1, "1/1"), 10);
    const copie = copierConfiguration(schema, sw1.id);
    mettreEnAcces(schema, sw1.id, ids(sw1, "1/1"), 20);
    collerConfiguration(schema, sw2.id, copie);
    expect(configDuBout(schema, bout(sw2, "1/1"))).toEqual({ mode: "acces", acces: 10 });
  });
});

describe("L'onglet Réseau", () => {
  it("sait quels équipements routent entre VLAN", () => {
    const { schema, coeur, sw1 } = etage();
    expect(routeEntreVlan(coeur)).toBe(true);
    expect(routeEntreVlan(sw1)).toBe(false);
    sw1.proprietes.couche = "Niveau 3";
    expect(routeEntreVlan(sw1)).toBe(true);
    const pc = creerEquipement(schema, "pc", { x: 0, y: 0 });
    expect(routeEntreVlan(pc)).toBe(false);
  });

  it("ajoute les interfaces de routage par VLAN, une par VLAN, en proposant les VLAN du schéma", () => {
    const { schema, coeur } = etage();
    schema.vlans.push(
      { id: "v_10", numero: 10, nom: "Élèves", sousReseaux: [] },
      { id: "v_20", numero: 20, nom: "Wi-Fi", sousReseaux: [] },
    );
    expect(vlanSansInterface(schema, coeur)).toBe(10);
    const svi = ajouterInterfaceVlan(schema, coeur.id, 10);
    expect(svi).toMatchObject({ nom: "Vlan10", nature: "vlan", genre: "virtuel", vlan: { mode: "acces", acces: 10 } });
    expect(vlanSansInterface(schema, coeur)).toBe(20);
    expect(() => ajouterInterfaceVlan(schema, coeur.id, 10)).toThrow(Refus);
    ajouterInterfaceVlan(schema, coeur.id, 20);
    expect(interfacesDeRoutage(coeur)).toHaveLength(2);
    expect(vlanSansInterface(schema, coeur)).toBe(30);
  });

  it("conseille un VLAN natif inutilisé et une gestion à part, sans rien bloquer", () => {
    const { schema, coeur, sw1 } = etage();
    expect(conseilsReseau(schema, coeur)).toEqual([]);
    mettreEnTrunk(schema, coeur.id, ids(coeur, "1/1", "1/2"), { natif: 1, tagues: "10,20,30" });
    expect(conseilsReseau(schema, coeur)).toEqual([
      "Le VLAN 1 est le VLAN natif des deux trunks : mieux vaut un VLAN natif inutilisé (par ex. 999). Conseil, rien n'est bloqué.",
    ]);
    mettreEnTrunk(schema, coeur.id, ids(coeur, "1/2"), { natif: 999, tagues: "10" });
    expect(conseilsReseau(schema, coeur)[0]).toContain("natif du trunk");
    sw1.gestion = { vlan: 1 };
    expect(conseilsReseau(schema, sw1)).toEqual([
      "La gestion passe par le VLAN 1 : mieux vaut un VLAN de gestion dédié (par ex. 99). Conseil, rien n'est bloqué.",
    ]);
    sw1.gestion = { vlan: 99 };
    mettreEnAcces(schema, sw1.id, ids(sw1, "1/3", "1/4"), 99);
    expect(conseilsReseau(schema, sw1)).toEqual([
      "Le VLAN de gestion 99 sert aussi à 2 ports d'accès : mieux vaut le réserver à la gestion. Conseil, rien n'est bloqué.",
    ]);
  });
});
