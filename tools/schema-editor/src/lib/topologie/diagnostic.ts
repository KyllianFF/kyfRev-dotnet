// Diagnostic d'un schéma (D39), en trois niveaux :
// - erreur : structure impossible (le schéma ne s'enregistre pas tel quel) ;
// - avertissement : incohérence réseau possible, signalée sans bloquer (on
//   peut documenter une anomalie réelle) ;
// - manque : information utile absente.

import { definition } from "./catalogue";
import { erreursStructurelles } from "./format";
import type { Interface, Schema } from "./modele";
import { interfaceDe, vitesseEnClair } from "./modele";
import { champsDe, configDuBout, memesVlans } from "./vlans";

export type Niveau = "erreur" | "avertissement" | "manque";
export type Constat = {
  niveau: Niveau;
  message: string;
  /** Objets concernés (équipements, liens), pour les montrer. */
  objets: string[];
  /** Adresse partagée par deux équipements : peut-être une paire haute disponibilité (D51). */
  paire?: [string, string];
};

/**
 * Paires haute disponibilité (D51) : les équipements reliés par une relation
 * « haute disponibilité », de proche en proche, forment une grappe ; ses
 * membres partagent légitimement une adresse (VIP) ou une MAC.
 */
function grappesHauteDispo(schema: Schema): Map<string, string> {
  const parent = new Map<string, string>();
  const racine = (id: string): string => {
    let r = id;
    while (parent.has(r) && parent.get(r) !== r) r = parent.get(r) as string;
    return r;
  };
  for (const r of schema.relations) {
    if (r.genre !== "haute-disponibilite") continue;
    const [a, b] = [racine(r.de), racine(r.vers)];
    if (a !== b) parent.set(a, b);
  }
  const grappes = new Map<string, string>();
  for (const e of schema.equipements) grappes.set(e.id, racine(e.id));
  return grappes;
}

const FIBRE = new Set(["fibre-monomode", "fibre-multimode", "aoc"]);

/** Un support ne va pas sur ce port : de la fibre sur du cuivre, du cuivre sur de la fibre ou du stockage. */
export function supportIncompatible(support: string, i: Interface): boolean {
  if (FIBRE.has(support)) return i.genre === "cuivre";
  if (support === "cuivre") return i.genre === "fibre" || i.genre === "stockage";
  if (support === "fibre-channel") return i.genre === "cuivre";
  return false;
}

export function diagnostiquer(schema: Schema): Constat[] {
  const constats: Constat[] = erreursStructurelles(schema).map((message) => ({
    niveau: "erreur",
    message,
    objets: [],
  }));

  for (const lien of schema.liens) {
    const a = interfaceDe(schema, lien.a);
    const b = interfaceDe(schema, lien.b);
    if (!a || !b) continue;
    const nom = lien.nom || `${a.nom} ↔ ${b.nom}`;
    const objets = [lien.id, lien.a.equipement, lien.b.equipement];
    if (a.vitesse && b.vitesse && a.vitesse !== b.vitesse) {
      constats.push({
        niveau: "avertissement",
        message: `Lien « ${nom} » : vitesses différentes aux extrémités (${vitesseEnClair(a.vitesse)} et ${vitesseEnClair(b.vitesse)}).`,
        objets,
      });
    }
    if (!memesVlans(schema, lien)) {
      const texte = (bout: typeof lien.a) => {
        const { nonTague, tagues } = champsDe(configDuBout(schema, bout));
        return (
          [nonTague !== undefined ? `non tagué ${nonTague}` : "", tagues ? `tagués ${tagues}` : ""]
            .filter(Boolean)
            .join(", ") || "aucun VLAN"
        );
      };
      constats.push({
        niveau: "avertissement",
        message: `Lien « ${nom} » : VLAN différents aux extrémités (${texte(lien.a)} ; ${texte(lien.b)}).`,
        objets,
      });
    }
    for (const iface of [a, b]) {
      if (supportIncompatible(lien.support, iface)) {
        constats.push({
          niveau: "avertissement",
          message: `Lien « ${nom} » : support ${lien.support.replace("-", " ")} sur l'interface ${iface.genre} « ${iface.nom} ».`,
          objets,
        });
      }
      if (iface.admin === "desactive") {
        constats.push({
          niveau: "avertissement",
          message: `Lien « ${nom} » : l'interface « ${iface.nom} » est désactivée.`,
          objets,
        });
      }
    }
  }

  // Adresses IP et MAC en double.
  const adresses = new Map<string, string[]>();
  const macs = new Map<string, string[]>();
  for (const e of schema.equipements) {
    const vues = new Set<string>();
    const noter = (carte: Map<string, string[]>, cle: string) => {
      if (!cle || vues.has(`${carte === macs ? "m" : "a"}${cle}`)) return;
      vues.add(`${carte === macs ? "m" : "a"}${cle}`);
      carte.set(cle, [...(carte.get(cle) ?? []), e.id]);
    };
    if (e.gestion?.adresse) noter(adresses, e.gestion.adresse.trim());
    for (const i of e.interfaces) {
      for (const a of i.adresses) noter(adresses, a.adresse.trim());
      for (const m of i.macs) noter(macs, m.trim().toLowerCase().replaceAll("-", ":"));
    }
  }
  const nomDe = (id: string) => schema.equipements.find((e) => e.id === id)?.nom ?? id;
  // Une adresse partagée au sein d'une paire haute disponibilité n'est pas un doublon.
  const grappes = grappesHauteDispo(schema);
  const partagee = (ids: string[]) => new Set(ids.map((id) => grappes.get(id) ?? id)).size === 1;
  for (const [adresse, ids] of adresses) {
    if (ids.length > 1 && !partagee(ids)) {
      constats.push({
        niveau: "avertissement",
        message: `Adresse ${adresse} utilisée par ${ids.map(nomDe).join(", ")}.`,
        objets: ids,
        ...(ids.length === 2 ? { paire: [ids[0], ids[1]] as [string, string] } : {}),
      });
    }
  }
  for (const [mac, ids] of macs) {
    if (ids.length > 1 && !partagee(ids)) {
      constats.push({
        niveau: "avertissement",
        message: `Adresse MAC ${mac} en double (${ids.map(nomDe).join(", ")}).`,
        objets: ids,
      });
    }
  }

  // Informations manquantes : l'adresse de gestion d'un équipement administrable.
  for (const e of schema.equipements) {
    const def = definition(e.type);
    const administrable =
      (def.categorie === "reseau" && e.type !== "switch-non-administrable") ||
      ["serveur", "esxi", "proxmox", "hyper-v", "nas", "san", "baie-stockage", "onduleur", "pdu"].includes(
        e.type,
      );
    if (administrable && e.statut !== "hors-service" && !e.gestion?.adresse) {
      constats.push({
        niveau: "manque",
        message: `« ${e.nom} » : adresse de gestion non renseignée.`,
        objets: [e.id],
      });
    }
  }
  return constats;
}
