// Un schéma d'essai proche d'un vrai (export draw.io) : un cœur en stack ×2, deux pare-feux en haute
// disponibilité, deux routeurs LASOTEL chacun dans sa zone, un nuage LAN2LAN, des câbles en accès (VLAN 2131),
// deux câbles parallèles, deux agrégats (Trk1 en trunk, Trk9), un switch ×3 au nom long, et trois pages reliées
// par des renvois. Seuls les essais s'en servent.

import { type Equipement, type Lien, type Schema, schemaVide } from "./modele";
import { creerEquipement, renommerInterface } from "./operations";
import { agreger, ecrireVlanDAcces, ecrireVlansDuTrunk, mettreEnTrunk, tirerCable } from "./simplifie";

export function commeUnVrai(options: { coudes?: boolean } = {}): Schema {
  const schema = schemaVide("SKFRMEY");
  schema.id = "s_vrai";
  if (options.coudes) schema.cablesCoudes = true;
  schema.pages = [
    { id: "p_siege", nom: "Siège" },
    { id: "p_ca03", nom: "CA03" },
    { id: "p_ca06", nom: "CA06" },
  ];
  schema.vlans.push({ id: "v_2131", numero: 2131, nom: "Utilisateurs", couleur: "#c2185b", sousReseaux: [] });
  const poser = (type: string, nom: string, x: number, y: number, adresse?: string, page = "p_siege"): Equipement => {
    const e = creerEquipement(schema, type, { x, y });
    e.nom = nom;
    e.page = page;
    if (adresse) e.gestion = { adresse };
    return e;
  };
  const coeur = poser("switch-coeur", "SKFRMEY-CA06A-CORE", 420, 320, "10.21.31.1/24");
  coeur.membres = [{ numero: 1 }, { numero: 2 }];
  const fwMaitre = poser("pare-feu", "FW_Master", 220, 110, "10.21.31.2");
  const fwSecours = poser("pare-feu", "FW_Backup", 620, 110, "10.21.31.3");
  const ca03 = poser("routeur", "LASOTEL", 140, 560, "192.168.3.1");
  const ca06 = poser("routeur", "LASOTEL", 700, 560, "192.168.6.1");
  const nuage = poser("reseau-distant", "LAN2LAN", 420, 760);
  const distant = poser("switch", "SKFRMEY-CA06A-SW", 900, 320, "10.21.31.20");
  distant.membres = [{ numero: 1 }, { numero: 2 }, { numero: 3 }];
  const salle = poser("switch", "SW-SALLE", -80, 320, "10.21.31.30");

  const cable = (a: Equipement, b: Equipement, portA?: string, portB?: string, support?: Lien["support"]): Lien => {
    const l = tirerCable(schema, a.id, b.id, support ? { support } : {});
    if (portA) renommerInterface(schema, l.a.equipement === a.id ? l.a : l.b, portA);
    if (portB) renommerInterface(schema, l.a.equipement === a.id ? l.b : l.a, portB);
    return l;
  };
  const acces = [cable(coeur, fwMaitre, "1/1", "port3"), cable(coeur, fwSecours, "1/2", "port3"), cable(coeur, fwSecours, "2/2", "port4")];
  ecrireVlanDAcces(
    schema,
    acces.map((l) => l.id),
    2131,
  );
  cable(fwMaitre, ca03, "port1", "LAN1");
  cable(fwSecours, ca06, "port1", "LAN1");
  cable(ca03, nuage, "WAN", undefined, "operateur");
  cable(ca06, nuage, "WAN", undefined, "operateur");

  const agregat = (a: Equipement, b: Equipement, nom: string, portsA: string[], portsB: string[]) => {
    const cables = portsA.map((p, i) => cable(a, b, p, portsB[i]));
    const garde = agreger(
      schema,
      cables.map((l) => l.id),
    );
    for (const ag of schema.agregats) if (ag.membres.includes(garde.a.interface) || ag.membres.includes(garde.b.interface)) ag.nom = nom;
    return garde;
  };
  const trk1 = agregat(coeur, distant, "Trk1", ["1/9", "3/9"], ["1/49", "2/49"]);
  mettreEnTrunk(schema, [trk1.id], true);
  ecrireVlansDuTrunk(schema, [trk1.id], { nonTague: 1, tagues: "10,20,30" });
  agregat(coeur, salle, "Trk9", ["1/10", "2/10"], ["49", "50"]);

  schema.relations.push({ id: "r_ha", genre: "haute-disponibilite", de: fwMaitre.id, vers: fwSecours.id, statut: "actif", proprietes: {}, points: [] });
  schema.groupes.push(
    { id: "g_ca03", nom: "CA03", genre: "site", membres: [ca03.id], position: { x: 40, y: 480 }, taille: { largeur: 220, hauteur: 170 }, page: "p_siege" },
    { id: "g_ca06", nom: "CA06", genre: "site", membres: [ca06.id], position: { x: 590, y: 480 }, taille: { largeur: 220, hauteur: 170 }, page: "p_siege" },
  );

  // Deux autres pages : un switch sur chacune, relié au routeur de sa zone (un renvoi de chaque côté).
  const sw03 = poser("switch", "SW-CA03", 200, 200, "192.168.3.10", "p_ca03");
  const sw06 = poser("switch", "SW-CA06", 200, 200, "192.168.6.10", "p_ca06");
  cable(ca03, sw03, "LAN2", "1/48");
  cable(ca06, sw06, "LAN2", "1/48");
  return schema;
}
