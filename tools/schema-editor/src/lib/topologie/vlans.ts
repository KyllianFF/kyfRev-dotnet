// VLAN et agrégats de liens des câbles (D51). Un bout de câble porte une
// configuration VLAN : celle de son agrégat s'il en est membre, sinon celle
// de son interface. Elle se saisit comme on la lit sur un switch : un VLAN
// non tagué (untagged, natif d'un trunk) et des VLAN tagués (« 10,20,30-40 »).
//
// Un agrégat (trunk au sens HPE/Aruba, port-channel, LACP) regroupe des
// ports d'un équipement ; son câble les représente tous au modèle (ses autres
// membres ne prennent donc pas d'autre câble) et se dessine en autant de
// traits que de ports, sous un cercle.

import type { Agregat, ConfigVlan, Extremite, Lien, Schema } from "./modele";
import { equipementDe, interfaceDe, liensDeLInterface, nouvelId } from "./modele";
import { Refus } from "./operations";

/** L'agrégat de l'équipement dont l'interface du bout est membre. */
export const agregatDe = (schema: Pick<Schema, "agregats">, bout: Extremite): Agregat | undefined =>
  schema.agregats.find((a) => a.equipement === bout.equipement && a.membres.includes(bout.interface));

/** Configuration VLAN d'un bout : celle de son agrégat, sinon celle de son interface. */
export function configDuBout(schema: Schema, bout: Extremite): ConfigVlan | undefined {
  const agregat = agregatDe(schema, bout);
  return agregat ? agregat.vlan : interfaceDe(schema, bout)?.vlan;
}

/**
 * Un câble d'agrégat de liens : un de ses bouts au moins est membre d'un agrégat. Le dessin montre
 * autant de traits que de ports, sous un cercle. Ce n'est pas un trunk : l'un va sans l'autre.
 */
export const enAgregat = (schema: Pick<Schema, "agregats">, lien: Lien): boolean =>
  Boolean(agregatDe(schema, lien.a) || agregatDe(schema, lien.b));

/**
 * Un câble en trunk : ses deux bouts sont en mode VLAN trunk (le réglage de leur agrégat, s'ils en
 * ont un). Le dessin l'annote « Trunk », avec ses VLAN.
 */
export const estTrunk = (schema: Schema, lien: Lien): boolean =>
  configDuBout(schema, lien.a)?.mode === "trunk" && configDuBout(schema, lien.b)?.mode === "trunk";

/**
 * L'annotation d'un câble en trunk : « Trunk », « Trunk · T 10,20 », « Trunk · U 1 · T 10,20,30 »
 * (U : VLAN non tagué, T : VLAN tagués), d'après le premier bout qui a des VLAN.
 */
export function texteTrunk(schema: Schema, lien: Lien): string {
  for (const bout of [lien.a, lien.b]) {
    const { nonTague, tagues } = champsDe(configDuBout(schema, bout));
    if (nonTague === undefined && !tagues) continue;
    return ["Trunk", nonTague !== undefined ? `U ${nonTague}` : "", tagues ? `T ${tagues}` : ""]
      .filter(Boolean)
      .join(" · ");
  }
  return "Trunk";
}

/** Les deux champs d'une configuration : VLAN non tagué (ou natif) et VLAN tagués. */
export type ChampsVlan = { nonTague?: number; tagues?: string };

export function champsDe(config: ConfigVlan | undefined): ChampsVlan {
  if (!config) return {};
  const nonTague = config.mode === "acces" ? config.acces : (config.natif ?? config.acces);
  const tagues = config.mode === "acces" ? undefined : config.tagues;
  return { ...(nonTague !== undefined ? { nonTague } : {}), ...(tagues ? { tagues } : {}) };
}

/** Configuration d'après les deux champs ; rien si les deux sont vides (voix et PVID gardés). */
export function configDepuis(champs: ChampsVlan, avant?: ConfigVlan): ConfigVlan | undefined {
  const reste = {
    ...(avant?.voix !== undefined ? { voix: avant.voix } : {}),
    ...(avant?.pvid !== undefined ? { pvid: avant.pvid } : {}),
  };
  if (champs.tagues) {
    return {
      mode: "trunk",
      tagues: champs.tagues,
      ...(champs.nonTague !== undefined ? { natif: champs.nonTague } : {}),
      ...reste,
    };
  }
  if (champs.nonTague !== undefined) return { mode: "acces", acces: champs.nonTague, ...reste };
  return Object.keys(reste).length ? { mode: "acces", ...reste } : undefined;
}

/** Numéro de VLAN saisi (1 à 4094) ; vide : `undefined` ; invalide : `null`. */
export function lireNumeroVlan(texte: string): number | undefined | null {
  const propre = texte.trim();
  if (!propre) return undefined;
  const n = Number(propre);
  return Number.isInteger(n) && n >= 1 && n <= 4094 ? n : null;
}

/** Liste de VLAN saisie (« 10, 20,30-40 ») remise au propre (« 10,20,30-40 ») ; invalide : `null`. */
export function lireListeVlan(texte: string): string | undefined | null {
  const morceaux = texte
    .split(/[\s,;]+/)
    .map((m) => m.trim())
    .filter(Boolean);
  if (morceaux.length === 0) return undefined;
  const valide = (n: number) => Number.isInteger(n) && n >= 1 && n <= 4094;
  for (const m of morceaux) {
    const [debut, fin, ...trop] = m.split("-").map(Number);
    if (trop.length > 0 || debut === undefined || !valide(debut)) return null;
    if (fin !== undefined && (!valide(fin) || fin < debut)) return null;
  }
  return morceaux.join(",");
}

/** Écrit la configuration d'un bout (sur son agrégat s'il en a un). */
export function ecrireConfigDuBout(schema: Schema, bout: Extremite, config: ConfigVlan | undefined): void {
  const cible = agregatDe(schema, bout) ?? interfaceDe(schema, bout);
  if (!cible) return;
  if (config) cible.vlan = config;
  else delete cible.vlan;
}

/** Texte d'un câble : « VLAN 10 », « T 20,30 », « U 10 · T 20,30 » ; couleur du VLAN non tagué déclaré. */
export function texteVlan(
  schema: Pick<Schema, "vlans">,
  config: ConfigVlan | undefined,
): { texte: string; couleur?: string } | undefined {
  const { nonTague, tagues } = champsDe(config);
  if (nonTague === undefined && !tagues) return undefined;
  const declare = nonTague !== undefined ? schema.vlans.find((v) => v.numero === nonTague) : undefined;
  const couleur = !tagues && declare?.couleur ? { couleur: declare.couleur } : {};
  if (!tagues) return { texte: `VLAN ${nonTague}`, ...couleur };
  return { texte: nonTague !== undefined ? `U ${nonTague} · T ${tagues}` : `T ${tagues}` };
}

/** Les deux bouts ont-ils la même configuration VLAN ? */
export function memesVlans(schema: Schema, lien: Lien): boolean {
  const [a, b] = [champsDe(configDuBout(schema, lien.a)), champsDe(configDuBout(schema, lien.b))];
  return a.nonTague === b.nonTague && (a.tagues ?? "") === (b.tagues ?? "");
}

// --- Agrégats (trunks) -------------------------------------------------------------

/** Nom libre « Trk1 », « Trk2 »… sur un équipement. */
function nomDAgregat(schema: Schema, equipement: string): string {
  const pris = new Set(schema.agregats.filter((a) => a.equipement === equipement).map((a) => a.nom));
  let n = 1;
  while (pris.has(`Trk${n}`)) n++;
  return `Trk${n}`;
}

/**
 * Fait d'un câble un agrégat : un agrégat de chaque côté, avec le port du
 * câble pour premier membre (et la configuration VLAN de ce port).
 */
export function creerAgregat(schema: Schema, lienId: string, mode: Agregat["mode"] = "lacp"): void {
  const lien = schema.liens.find((l) => l.id === lienId);
  if (!lien) throw new Refus("Câble introuvable.");
  for (const bout of [lien.a, lien.b]) {
    if (agregatDe(schema, bout)) continue;
    const iface = interfaceDe(schema, bout);
    if (!iface) throw new Refus("Interface introuvable.");
    schema.agregats.push({
      id: nouvelId("a"),
      equipement: bout.equipement,
      nom: nomDAgregat(schema, bout.equipement),
      mode,
      membres: [bout.interface],
      ...(iface.vlan ? { vlan: { ...iface.vlan } } : {}),
      condense: true,
    });
  }
}

/** Le câble n'est plus un agrégat : ses agrégats disparaissent, la configuration VLAN revient au port. */
export function retirerAgregat(schema: Schema, lienId: string): void {
  const lien = schema.liens.find((l) => l.id === lienId);
  if (!lien) return;
  for (const bout of [lien.a, lien.b]) {
    const agregat = agregatDe(schema, bout);
    if (!agregat) continue;
    const iface = interfaceDe(schema, bout);
    if (iface && agregat.vlan) iface.vlan = { ...agregat.vlan };
    schema.agregats = schema.agregats.filter((a) => a.id !== agregat.id);
  }
}

/**
 * Ajoute ou retire un port d'un agrégat, du côté `cote` du câble. Un port qui
 * porte déjà un câble vers le même équipement que l'agrégat : ce câble rejoint
 * l'agrégat (il disparaît, son autre bout devient membre en face). Vers un
 * autre équipement : refusé. Le port du câble lui-même reste membre.
 */
export function basculerMembre(schema: Schema, lienId: string, cote: "a" | "b", interfaceId: string): void {
  const lien = schema.liens.find((l) => l.id === lienId);
  if (!lien) throw new Refus("Câble introuvable.");
  const bout = lien[cote];
  const enFace = cote === "a" ? lien.b : lien.a;
  const agregat = agregatDe(schema, bout);
  if (!agregat) throw new Refus("Ce câble n'est pas un agrégat.");
  if (interfaceId === bout.interface) throw new Refus("Le port du câble reste dans l'agrégat.");
  const iface = interfaceDe(schema, { equipement: bout.equipement, interface: interfaceId });
  if (!iface) throw new Refus("Interface introuvable.");
  if (agregat.membres.includes(interfaceId)) {
    agregat.membres = agregat.membres.filter((m) => m !== interfaceId);
    return;
  }
  const autre = schema.agregats.find(
    (a) => a.id !== agregat.id && a.equipement === bout.equipement && a.membres.includes(interfaceId),
  );
  if (autre) throw new Refus(`« ${iface.nom} » est déjà membre de l'agrégat « ${autre.nom} ».`);
  if (iface.nature !== "physique") throw new Refus(`« ${iface.nom} » n'est pas un port physique.`);
  const cables = liensDeLInterface(schema, bout.equipement, interfaceId);
  for (const cable of cables) {
    const loin =
      cable.a.equipement === bout.equipement && cable.a.interface === interfaceId ? cable.b : cable.a;
    if (loin.equipement !== enFace.equipement) {
      const nom = equipementDe(schema, loin.equipement)?.nom ?? "?";
      throw new Refus(`« ${iface.nom} » est câblé vers ${nom}, pas vers l'autre bout de l'agrégat.`);
    }
  }
  for (const cable of cables) {
    const loin =
      cable.a.equipement === bout.equipement && cable.a.interface === interfaceId ? cable.b : cable.a;
    schema.liens = schema.liens.filter((l) => l.id !== cable.id);
    const agregatEnFace = agregatDe(schema, enFace);
    if (agregatEnFace && !agregatEnFace.membres.includes(loin.interface)) {
      const dejaAilleurs = schema.agregats.some(
        (a) => a.equipement === loin.equipement && a.membres.includes(loin.interface),
      );
      if (!dejaAilleurs) agregatEnFace.membres.push(loin.interface);
    }
  }
  agregat.membres.push(interfaceId);
}

/** Texte du bout d'un câble agrégé : « Trk1 · 1/1,2/1 ». */
export function texteAgregat(schema: Schema, bout: Extremite): string | undefined {
  const agregat = agregatDe(schema, bout);
  if (!agregat) return undefined;
  const equipement = equipementDe(schema, bout.equipement);
  const noms = agregat.membres
    .map((m) => equipement?.interfaces.find((i) => i.id === m)?.nom)
    .filter((n): n is string => Boolean(n));
  return noms.length ? `${agregat.nom} · ${noms.join(",")}` : agregat.nom;
}
