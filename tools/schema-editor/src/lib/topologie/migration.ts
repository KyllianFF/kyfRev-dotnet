// Migration des schémas au format 1 (D21 : bloc ```reseau dans la fiche,
// équipements sans interfaces, liens d'équipement à équipement avec une note
// de port à chaque bout) vers le format 2 (D39). Rien n'est perdu :
// - chaque équipement garde son nom, son détail (en description) et sa place ;
// - chaque bout de lien devient une interface : celle du profil qui porte le
//   nom noté si elle est libre, sinon une interface ajoutée à ce nom ;
// - les liens « sans fil » vers un point d'accès deviennent des associations
//   Wi-Fi, les « VPN » des tunnels (des relations, pas des câbles) ;
// - les zones deviennent des groupes (membres : les équipements qu'elles
//   contiennent), les notes des annotations.

import type { Equipement, Extremite, Groupe, Interface, Schema, Support } from "./modele";
import { FORMAT, nouvelId, schemaVide } from "./modele";
import { creerEquipement, interfaceIndisponible } from "./operations";

type Brut = Record<string, unknown>;

/** Types du format 1 → types du catalogue (et rôle, quand le type devient générique). */
const TYPES_FORMAT_1: Record<string, { type: string; role?: string }> = {
  routeur: { type: "routeur" },
  switch: { type: "switch" },
  concentrateur: { type: "equipement-reseau", role: "Concentrateur (hub)" },
  "point-acces": { type: "point-acces" },
  modem: { type: "modem" },
  "pare-feu": { type: "pare-feu" },
  ips: { type: "equipement-reseau", role: "IPS" },
  ids: { type: "equipement-reseau", role: "IDS" },
  serveur: { type: "serveur" },
  nas: { type: "nas" },
  san: { type: "san" },
  ordinateur: { type: "pc" },
  mobile: { type: "mobile" },
  imprimante: { type: "imprimante" },
  "telephone-ip": { type: "telephone-ip" },
  television: { type: "generique", role: "Télévision" },
  "camera-ip": { type: "camera-ip" },
  "objet-connecte": { type: "iot" },
  internet: { type: "internet" },
  cloud: { type: "cloud" },
  "machine-virtuelle": { type: "machine-virtuelle" },
  conteneur: { type: "conteneur" },
  service: { type: "generique", role: "Service" },
};

const COULEURS_ZONE: Record<string, string> = {
  violet: "#7f4ea8",
  bleu: "#5b6fc4",
  vert: "#4f7a2e",
  orange: "#c27c0e",
  rouge: "#c4553a",
  gris: "#6b6578",
};

const texte = (v: unknown, max = 300) => (typeof v === "string" ? v.slice(0, max) : "");
const coord = (v: unknown) => (typeof v === "number" && Number.isFinite(v) ? Math.round(v) : 0);
const liste = (v: unknown): Brut[] =>
  Array.isArray(v) ? v.filter((x): x is Brut => typeof x === "object" && x !== null) : [];

export function migrerFormat1(brut: Brut): Schema {
  const schema = schemaVide(texte(brut.titre) || "Schéma réseau");
  schema.format = FORMAT;
  const parAncienId = new Map<string, Equipement>();

  for (const e of liste(brut.equipements)) {
    const ancien = TYPES_FORMAT_1[texte(e.type)] ?? { type: "generique" };
    const equipement = creerEquipement(schema, ancien.type, { x: coord(e.x), y: coord(e.y) });
    const nom = texte(e.nom, 120);
    if (nom) equipement.nom = nom;
    const detail = texte(e.detail, 4000);
    if (detail) equipement.description = detail;
    if (ancien.role) equipement.role = ancien.role;
    if (typeof e.id === "string") parAncienId.set(e.id, equipement);
  }

  /** Interface pour un bout de lien : celle du profil qui porte ce nom si elle est libre, sinon une nouvelle. */
  const bout = (
    equipement: Equipement,
    note: string,
    numero: number,
    genre: Interface["genre"],
  ): Extremite => {
    const nom = note.trim();
    const libre = (i: Interface) =>
      !interfaceIndisponible(schema, { equipement: equipement.id, interface: i.id });
    const trouvee = nom ? equipement.interfaces.find((i) => i.nom === nom && libre(i)) : undefined;
    if (trouvee) return { equipement: equipement.id, interface: trouvee.id };
    if (!nom && genre !== "radio") {
      const premiere = equipement.interfaces.find(
        (i) => i.genre === genre && i.nature === "physique" && libre(i),
      );
      if (premiere) return { equipement: equipement.id, interface: premiere.id };
    }
    const iface: Interface = {
      id: nouvelId("i"),
      nom: nom || `port ${numero}`,
      genre,
      nature: genre === "virtuel" ? "virtuelle" : "physique",
      admin: "actif",
      macs: [],
      adresses: [],
      ...(equipement.interfaces.some((i) => i.multiConnexion) ? { multiConnexion: true } : {}),
    };
    equipement.interfaces.push(iface);
    return { equipement: equipement.id, interface: iface.id };
  };

  let numero = 0;
  for (const l of liste(brut.liens)) {
    const de = parAncienId.get(texte(l.de));
    const vers = parAncienId.get(texte(l.vers));
    if (!de || !vers || de === vers) continue;
    numero++;
    const style = texte(l.style) || "filaire";
    const libelle = texte(l.libelle);
    const coude =
      typeof l.coude === "object" && l.coude !== null && texte(l.forme) && texte(l.forme) !== "ligne"
        ? { x: coord((l.coude as Brut).x), y: coord((l.coude as Brut).y) }
        : null;
    const points = coude
      ? [
          {
            x: Math.round((de.position.x + vers.position.x) / 2 + coude.x),
            y: Math.round((de.position.y + vers.position.y) / 2 + coude.y),
          },
        ]
      : [];
    if (
      style === "vpn" ||
      (style === "sans-fil" && (de.type === "point-acces" || vers.type === "point-acces"))
    ) {
      const ap = vers.type === "point-acces" ? vers : de;
      const client = ap === vers ? de : vers;
      schema.relations.push({
        id: nouvelId("r"),
        genre: style === "vpn" ? "tunnel" : "association-wifi",
        de: style === "vpn" ? de.id : client.id,
        vers: style === "vpn" ? vers.id : ap.id,
        ...(libelle ? { nom: libelle } : {}),
        statut: "actif",
        proprietes: {},
        points,
      });
      continue;
    }
    const genre: Interface["genre"] =
      style === "fibre" ? "fibre" : style === "sans-fil" ? "virtuel" : "cuivre";
    const support: Support =
      style === "fibre" ? "fibre-multimode" : style === "sans-fil" ? "radio" : "cuivre";
    const a = bout(de, texte(l.portDe, 40), numero, genre);
    const b = bout(vers, texte(l.portVers, 40), numero, genre);
    schema.liens.push({
      id: nouvelId("l"),
      a,
      b,
      support,
      statut: "actif",
      ...(libelle ? { nom: libelle } : {}),
      points,
    });
  }

  for (const z of liste(brut.zones)) {
    const x = coord(z.x);
    const y = coord(z.y);
    const largeur = Math.max(60, coord(z.largeur) || 240);
    const hauteur = Math.max(60, coord(z.hauteur) || 160);
    const membres = schema.equipements
      .filter(
        (e) =>
          e.position.x >= x &&
          e.position.x <= x + largeur &&
          e.position.y >= y &&
          e.position.y <= y + hauteur,
      )
      .map((e) => e.id);
    const groupe: Groupe = {
      id: nouvelId("g"),
      nom: texte(z.nom, 120) || (z.genre === "hote" ? "Hôte" : "Zone"),
      genre: "zone",
      membres,
      position: { x, y },
      taille: { largeur, hauteur },
      ...(COULEURS_ZONE[texte(z.couleur)] ? { couleur: COULEURS_ZONE[texte(z.couleur)] } : {}),
    };
    schema.groupes.push(groupe);
  }

  for (const n of liste(brut.notes)) {
    const contenu = texte(n.texte, 4000);
    if (contenu)
      schema.annotations.push({
        id: nouvelId("n"),
        texte: contenu,
        position: { x: coord(n.x), y: coord(n.y) },
      });
  }
  return schema;
}
