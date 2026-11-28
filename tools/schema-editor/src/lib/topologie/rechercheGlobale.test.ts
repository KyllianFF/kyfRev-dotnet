// @vitest-environment node
// La recherche dans tous les schémas (31E) : noms, noms d'hôte, adresses (entières, débuts, réseaux), VLAN, adresses en
// double signalées sans bloquer, groupes plafonnés ; et la lecture des schémas du coffre, une fois.
import { afterEach, describe, expect, it, vi } from "vitest";
import { api } from "../api";
import { oublierSchemasLus, schemasDuCoffre } from "./coffreSchemas";
import { ecrireSchema, lireSchema } from "./format";
import type { Schema } from "./modele";
import { chercherPartout, conseilDe, indexer, PLAFOND, type SchemaCharge } from "./rechercheGlobale";

const lycee = lireSchema({
  format: 3,
  titre: "Réseau du lycée",
  pages: [
    { id: "p_1", nom: "Étage 1" },
    { id: "p_2", nom: "Étage 2" },
  ],
  vlans: [
    { id: "v_1", numero: 30, nom: "Administration", couleur: "#1f62b0", sousReseaux: ["10.0.30.0/24"] },
    { id: "v_2", numero: 99, nom: "Gestion", sousReseaux: [] },
  ],
  equipements: [
    { id: "d_1", page: "p_1", type: "switch", nom: "SW-Étage1", nomHote: "sw-e1.lycee.lan", position: { x: 0, y: 0 }, gestion: { adresse: "10.0.30.11/24" }, interfaces: [{ id: "i_1", nom: "1/1", adresses: [{ adresse: "10.0.30.11", prefixe: 24 }] }] },
    { id: "d_2", page: "p_2", type: "switch", nom: "SW-Étage2", position: { x: 0, y: 0 }, gestion: { adresse: "10.0.30.12/24" } },
    { id: "d_3", page: "p_1", type: "switch-coeur", nom: "Cœur", position: { x: 200, y: 0 }, interfaces: [{ id: "i_1", nom: "Vlan30", adresses: [{ adresse: "10.0.30.1", prefixe: 24 }] }, { id: "i_2", nom: "Vlan99", adresses: [{ adresse: "10.0.99.1", prefixe: 24 }] }] },
  ],
}).schema;

const meylan = lireSchema({
  format: 2,
  titre: "SAMES Meylan",
  vlans: [{ id: "v_1", numero: 30, nom: "Bureaux", sousReseaux: ["10.0.30.0/24"] }],
  equipements: [
    { id: "d_1", type: "routeur", nom: "LASOTEL 1", position: { x: 0, y: 0 }, interfaces: [{ id: "i_1", nom: "LAN1", adresses: [{ adresse: "10.0.30.5", prefixe: 24 }] }] },
    { id: "d_2", type: "pc", nom: "Poste accueil", position: { x: 200, y: 0 }, interfaces: [{ id: "i_1", nom: "eth0", adresses: [{ adresse: "192.168.1.20", prefixe: 24 }] }] },
  ],
}).schema;

const stains = lireSchema({
  format: 2,
  titre: "SAMES Stains",
  equipements: [
    { id: "d_1", type: "routeur", nom: "LASOTEL 2", position: { x: 0, y: 0 }, interfaces: [{ id: "i_1", nom: "LAN1", adresses: [{ adresse: "10.0.30.5", prefixe: 24 }] }] },
    // Deux pare-feux en haute disponibilité partagent leur adresse virtuelle : ce n'est pas un doublon.
    { id: "d_2", type: "pare-feu", nom: "FW-A", position: { x: 200, y: 0 }, interfaces: [{ id: "i_1", nom: "port1", adresses: [{ adresse: "10.9.0.254", prefixe: 24 }] }] },
    { id: "d_3", type: "pare-feu", nom: "FW-B", position: { x: 400, y: 0 }, interfaces: [{ id: "i_1", nom: "port1", adresses: [{ adresse: "10.9.0.254", prefixe: 24 }] }] },
    // Deux postes du même schéma à la même adresse : c'est à vérifier.
    { id: "d_4", type: "pc", nom: "PC-A", position: { x: 0, y: 200 }, interfaces: [{ id: "i_1", nom: "eth0", adresses: [{ adresse: "172.16.0.9", prefixe: 24 }] }] },
    { id: "d_5", type: "pc", nom: "PC-B", position: { x: 200, y: 200 }, interfaces: [{ id: "i_1", nom: "eth0", adresses: [{ adresse: "172.16.0.9", prefixe: 24 }, { adresse: "fe80::1" }] }] },
    { id: "d_6", type: "pc", nom: "PC-C", position: { x: 400, y: 200 }, interfaces: [{ id: "i_1", nom: "eth0", adresses: [{ adresse: "fe80::1" }] }] },
  ],
  relations: [{ id: "r_1", genre: "haute-disponibilite", de: "d_2", vers: "d_3" }],
}).schema;

const charges: SchemaCharge[] = [
  { id: "s_lycee", titre: "Réseau du lycée", schema: lycee },
  { id: "s_meylan", titre: "SAMES Meylan", schema: meylan },
  { id: "s_stains", titre: "SAMES Stains", schema: stains, archive: true },
];
const index = indexer(charges);
const noms = (texte: string, genre?: string) =>
  chercherPartout(index, texte)
    .groupes.filter((g) => !genre || g.genre === genre)
    .flatMap((g) => g.resultats.map((r) => r.nom));

describe("recherche dans tous les schémas", () => {
  it("commence à deux caractères", () => {
    expect(chercherPartout(index, "")).toMatchObject({ groupes: [], total: 0, schemas: 3 });
    expect(chercherPartout(index, " s ").groupes).toEqual([]);
    expect(chercherPartout(index, "sw").total).toBeGreaterThan(0);
  });

  it("trouve un équipement par son nom, sans accents ni casse, ou par son nom d'hôte", () => {
    expect(noms("etage", "equipement")).toEqual(["SW-Étage1", "SW-Étage2"]);
    expect(noms("COEUR", "equipement")).toEqual(["Cœur"]);
    const parHote = chercherPartout(index, "lycee.lan").groupes[0]?.resultats[0];
    expect(parHote).toMatchObject({ nom: "SW-Étage1", detail: "nom d'hôte sw-e1.lycee.lan", genre: "equipement" });
    // Ceux dont le nom commence par le texte passent devant.
    expect(noms("pc", "equipement")).toEqual(["PC-A", "PC-B", "PC-C", "Poste accueil"].filter((n) => n.toLowerCase().includes("pc")));
  });

  it("dit le schéma et la page de chaque résultat, et ce qu'il faut ouvrir", () => {
    const [premier, second] = chercherPartout(index, "SW-Étage").groupes[0]?.resultats ?? [];
    expect(premier).toMatchObject({ schemaId: "s_lycee", schemaTitre: "Réseau du lycée", pageId: "p_1", pageNom: "Étage 1", objetId: "d_1", type: "switch", adresse: "10.0.30.11/24", archive: false });
    expect(second).toMatchObject({ pageId: "p_2", pageNom: "Étage 2", objetId: "d_2" });
    // Un schéma d'une seule page n'en dit pas ; un schéma archivé le dit.
    const lasotel = chercherPartout(index, "LASOTEL 2").groupes[0]?.resultats[0];
    expect(lasotel?.pageId).toBeUndefined();
    expect(lasotel?.archive).toBe(true);
  });

  it("trouve une adresse entière, son début ou son réseau", () => {
    expect(noms("10.0.30.", "adresse")).toEqual(["Cœur", "LASOTEL 1", "LASOTEL 2", "SW-Étage1", "SW-Étage2"]);
    expect(chercherPartout(index, "10.0.30.").groupes.map((g) => g.genre)).toEqual(["adresse"]);
    const entiere = chercherPartout(index, "10.0.30.12").groupes.find((g) => g.genre === "adresse")?.resultats;
    expect(entiere?.map((r) => [r.nom, r.adresse])).toEqual([["SW-Étage2", "10.0.30.12/24"]]);
    // Un réseau : ses adresses, et elles seules.
    expect(noms("10.0.0.0/16", "adresse")).toEqual(["Cœur", "LASOTEL 1", "LASOTEL 2", "SW-Étage1", "SW-Étage2", "Cœur"]);
    expect(noms("10.0.99.0/24", "adresse")).toEqual(["Cœur"]);
    expect(noms("10.0.30.0/", "adresse")).toEqual(["Cœur", "LASOTEL 1", "LASOTEL 2", "SW-Étage1", "SW-Étage2"]);
    // L'adresse de gestion et celle de l'interface d'un même équipement ne font qu'une ligne.
    expect(noms("10.0.30.11", "adresse")).toEqual(["SW-Étage1"]);
    // Une adresse IPv6.
    expect(noms("fe80:", "adresse")).toEqual(["PC-B", "PC-C"]);
  });

  it("trouve un VLAN par son numéro ou son nom", () => {
    const vlans = (texte: string) =>
      chercherPartout(index, texte).groupes.find((g) => g.genre === "vlan")?.resultats.map((r) => `${r.nom} ${r.detail} · ${r.schemaTitre}`) ?? [];
    expect(vlans("30")).toEqual(["VLAN 30 Administration · Réseau du lycée", "VLAN 30 Bureaux · SAMES Meylan"]);
    expect(vlans("vlan 99")).toEqual(["VLAN 99 Gestion · Réseau du lycée"]);
    expect(vlans("VLAN99")).toEqual(["VLAN 99 Gestion · Réseau du lycée"]);
    expect(vlans("bureaux")).toEqual(["VLAN 30 Bureaux · SAMES Meylan"]);
    expect(vlans("vlan")).toHaveLength(3);
    const trente = chercherPartout(index, "30").groupes.find((g) => g.genre === "vlan")?.resultats[0];
    expect(trente).toMatchObject({ adresse: "10.0.30.0/24", couleur: "#1f62b0", schemaId: "s_lycee" });
    expect(trente?.objetId).toBeUndefined();
  });

  it("signale une même adresse dans deux schémas, sans rien bloquer", () => {
    const reponse = chercherPartout(index, "10.0.30.5");
    const lignes = reponse.groupes.flatMap((g) => g.resultats);
    expect(lignes.map((r) => [r.nom, r.memeAdresse ?? false])).toEqual([
      ["LASOTEL 1", true],
      ["LASOTEL 2", true],
    ]);
    expect(reponse.conseils).toEqual([{ adresse: "10.0.30.5", texte: "10.0.30.5 sert dans deux schémas : à vérifier s'ils sont sur le même réseau." }]);
    // Le nom aussi : son adresse principale est en double.
    expect(chercherPartout(index, "lasotel").groupes[0]?.resultats.every((r) => r.memeAdresse)).toBe(true);
  });

  it("signale deux équipements d'un même schéma, mais pas une paire haute disponibilité ni une adresse locale", () => {
    expect(chercherPartout(index, "172.16.0.9").conseils).toEqual([
      { adresse: "172.16.0.9", texte: "172.16.0.9 sert à deux équipements de « SAMES Stains » : à vérifier." },
    ]);
    const vip = chercherPartout(index, "10.9.0.254");
    expect(vip.groupes.flatMap((g) => g.resultats).some((r) => r.memeAdresse)).toBe(false);
    expect(vip.conseils).toEqual([]);
    expect(chercherPartout(index, "fe80::1").conseils).toEqual([]);
    expect(conseilDe(index, "192.168.1.20")).toBeNull();
    expect(conseilDe(index, "")).toBeNull();
  });

  it("plafonne chaque groupe, et montre tout une fois « N de plus » choisi", () => {
    const beaucoup: Schema = lireSchema({
      format: 2,
      titre: "Salle",
      equipements: Array.from({ length: 20 }, (_, n) => ({
        id: `d_${n}`,
        type: "pc",
        nom: `Poste ${String(n + 1).padStart(2, "0")}`,
        position: { x: n * 200, y: 0 },
        interfaces: [{ id: "i_1", nom: "eth0", adresses: [{ adresse: `10.5.0.${n + 10}`, prefixe: 24 }] }],
      })),
    }).schema;
    const ici = indexer([{ id: "s_salle", titre: "Salle", schema: beaucoup }]);
    const plafonnee = chercherPartout(ici, "poste");
    expect(plafonnee.groupes[0]?.resultats).toHaveLength(PLAFOND);
    expect(plafonnee.groupes[0]?.restants).toBe(20 - PLAFOND);
    expect(plafonnee.total).toBe(20);
    const ouverte = chercherPartout(ici, "poste", new Set(["equipement"]));
    expect(ouverte.groupes[0]?.resultats).toHaveLength(20);
    expect(ouverte.groupes[0]?.restants).toBe(0);
    // Les adresses, dans leur ordre : 10.5.0.10 avant 10.5.0.9… n'existe pas, mais 10.5.0.10 avant 10.5.0.11.
    const adresses = chercherPartout(ici, "10.5.0.", new Set(["adresse"])).groupes[0]?.resultats.map((r) => r.adresse);
    expect(adresses?.slice(0, 3)).toEqual(["10.5.0.10/24", "10.5.0.11/24", "10.5.0.12/24"]);
    // Rien ne correspond : aucun groupe.
    expect(chercherPartout(ici, "imprimante")).toMatchObject({ groupes: [], total: 0 });
  });
});

describe("les schémas du coffre, lus une fois", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    oublierSchemasLus();
  });

  it("relit seulement ce qui a changé, garde les archives et laisse la corbeille", async () => {
    const textes: Record<string, string> = {
      s_lycee: ecrireSchema({ ...lycee, id: "s_lycee" }),
      s_meylan: ecrireSchema({ ...meylan, id: "s_meylan" }),
      s_stains: ecrireSchema({ ...stains, id: "s_stains" }),
      s_casse: "{ cassé",
    };
    let dates: Record<string, string | null> = { s_lycee: "2026-11-01", s_meylan: "2026-11-02", s_casse: "2026-11-03" };
    vi.spyOn(api, "schemas").mockImplementation(async () =>
      Object.entries(dates).map(([id, modifieLe]) => ({ id, titre: id, equipements: 0, modifieLe, fiches: [] })),
    );
    vi.spyOn(api, "schemasRanges").mockResolvedValue([
      { id: "s_stains", titre: "SAMES Stains", equipements: 6, modifieLe: "2026-10-01", fiches: [], etat: "archive" },
      { id: "s_jete", titre: "Jeté", equipements: 0, modifieLe: "2026-10-01", fiches: [], etat: "corbeille" },
    ]);
    const lire = vi.spyOn(api, "lireSchema").mockImplementation(async (id: string) => ({ id, texte: textes[id] ?? "", empreinte: "x" }));

    const premiers = await schemasDuCoffre();
    expect(premiers.map((c) => [c.id, c.archive])).toEqual([
      ["s_lycee", false],
      ["s_meylan", false],
      ["s_stains", true],
    ]);
    expect(lire.mock.calls.map(([id]) => id)).toEqual(["s_lycee", "s_meylan", "s_casse", "s_stains"]);

    // Rien n'a changé : rien n'est relu (sauf le schéma illisible). Une date changée : lui seul.
    lire.mockClear();
    await schemasDuCoffre();
    expect(lire.mock.calls.map(([id]) => id)).toEqual(["s_casse"]);
    lire.mockClear();
    dates = { ...dates, s_meylan: "2026-11-20" };
    await schemasDuCoffre();
    expect(lire.mock.calls.map(([id]) => id)).toEqual(["s_meylan", "s_casse"]);
    // Sans date, un schéma se relit à chaque fois ; un schéma parti est oublié.
    lire.mockClear();
    dates = { s_lycee: null };
    const restants = await schemasDuCoffre();
    expect(restants.map((c) => c.id)).toEqual(["s_lycee", "s_stains"]);
    expect(lire.mock.calls.map(([id]) => id)).toEqual(["s_lycee"]);
    lire.mockClear();
    await schemasDuCoffre();
    expect(lire.mock.calls.map(([id]) => id)).toEqual(["s_lycee"]);
  });
});
