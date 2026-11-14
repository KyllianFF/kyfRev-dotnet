// Mode simplifié de l'éditeur de schémas (11A). C'est une façon de travailler sur le même modèle que le
// mode avancé : un câble tiré ici branche de vrais ports, « Trunk » est le vrai réglage VLAN des deux
// bouts, l'adresse est l'adresse de gestion. Rien n'est propre au mode : passer de l'un à l'autre ne
// change pas le schéma.

import { ipv4 } from "./adressage";
import { definition } from "./catalogue";
import { adresseAffichee, faisceauxDe } from "./dessin";
import { supportIncompatible } from "./diagnostic";
import type {
  ConfigVlan,
  Equipement,
  Extremite,
  GenreInterface,
  Interface,
  Lien,
  Schema,
  Support,
} from "./modele";
import { equipementDe, interfaceDe, interfaceOccupee, vitesseEnClair } from "./modele";
import {
  ajouterInterface,
  connecter,
  interfaceIndisponible,
  rebrancher,
  Refus,
  retirerEquipements,
  supportPropose,
} from "./operations";
import {
  agregatDe,
  basculerMembre,
  type ChampsVlan,
  champsDe,
  configDepuis,
  configDuBout,
  creerAgregat,
  ecrireConfigDuBout,
  enAgregat,
  estTrunk,
  lireListeVlan,
  lireNumeroVlan,
  memesVlans,
  retirerAgregat,
  texteVlan,
} from "./vlans";

// --- Le mode, retenu sur le poste ---------------------------------------------------

export type ModeEdition = "simplifie" | "avance";
export const MODES: { valeur: ModeEdition; nom: string }[] = [
  { valeur: "simplifie", nom: "Simplifié" },
  { valeur: "avance", nom: "Avancé" },
];
export const CLE_MODE = "kyfrev.schemas.mode";

type Stockage = Pick<Storage, "getItem" | "setItem">;

/** Le stockage du poste, s'il est accessible (un moteur peut le refuser). */
function stockageDuPoste(): Stockage | null {
  try {
    return globalThis.localStorage ?? null;
  } catch {
    return null;
  }
}

/** Le mode retenu sur le poste ; la première fois, ou si rien ne se lit : simplifié. */
export function lireMode(stockage: Stockage | null = stockageDuPoste()): ModeEdition {
  try {
    return stockage?.getItem(CLE_MODE) === "avance" ? "avance" : "simplifie";
  } catch {
    return "simplifie";
  }
}

/** Retient le mode sur le poste ; rend faux si le poste ne retient rien (le mode vaut alors pour cette fois). */
export function retenirMode(mode: ModeEdition, stockage: Stockage | null = stockageDuPoste()): boolean {
  try {
    stockage?.setItem(CLE_MODE, mode);
    return stockage !== null;
  } catch {
    return false;
  }
}

// --- Câbles sans choisir de port ----------------------------------------------------

/** Câbles au plus entre deux mêmes équipements, en mode simplifié. */
export const CABLES_MAX = 8;

/** Les câbles entre deux équipements, dans un sens ou dans l'autre. */
export const cablesEntre = (schema: Pick<Schema, "liens">, a: string, b: string): Lien[] =>
  schema.liens.filter(
    (l) =>
      (l.a.equipement === a && l.b.equipement === b) || (l.a.equipement === b && l.b.equipement === a),
  );

/** Genre de port qu'il faut à un support, quand ce support ne va pas sur n'importe quel port. */
export function genrePour(support: Support): GenreInterface | null {
  if (support === "cuivre") return "cuivre";
  if (support === "fibre-monomode" || support === "fibre-multimode" || support === "aoc") return "fibre";
  if (support === "fibre-channel") return "stockage";
  return null;
}

const HORS_CABLE = new Set<GenreInterface>(["console", "usb", "radio"]);

/**
 * Les ports où un câble peut se brancher sans rien demander : physiques ou virtuels, actifs, libres
 * (`interfaceIndisponible`), hors console, USB et radio. Les ports ordinaires d'abord ; ceux de gestion
 * et ceux qui portent déjà un réglage VLAN en dernier.
 */
export function portsLibres(schema: Schema, equipement: Equipement, sauf?: string): Interface[] {
  const rang = (i: Interface) => (i.role === "gestion" ? 1 : 0) + (i.vlan ? 2 : 0);
  return equipement.interfaces
    .filter(
      (i) =>
        (i.nature === "physique" || i.nature === "virtuelle") &&
        i.admin !== "desactive" &&
        i.role !== "console" &&
        !HORS_CABLE.has(i.genre) &&
        interfaceIndisponible(schema, { equipement: equipement.id, interface: i.id }, sauf) === null,
    )
    .map((i, ordre) => ({ i, ordre }))
    .sort((x, y) => rang(x.i) - rang(y.i) || x.ordre - y.ordre)
    .map((x) => x.i);
}

/** Un bout choisi : un port libre, ou le genre du port à créer faute de port libre qui convienne. */
export type BoutChoisi = { interface: string } | { creer: GenreInterface };

/**
 * Choisit les deux ports d'un câble (11A) : de chaque côté le premier port libre qui convient, deux
 * ports de même genre de préférence. Faute de port libre, ou si les ports libres ne vont pas ensemble
 * (cuivre d'un côté, fibre de l'autre), un port est à créer : un équipement ne refuse jamais un câble.
 */
export function choisirBouts(
  schema: Schema,
  a: Equipement,
  b: Equipement,
  support?: Support,
): { a: BoutChoisi; b: BoutChoisi } {
  const convient = (i: Interface) => !support || !supportIncompatible(support, i);
  const libresA = portsLibres(schema, a).filter(convient);
  const libresB = portsLibres(schema, b).filter(convient);
  const ensemble = (x: Interface, y: Interface) => {
    const s = support ?? supportPropose(x, y);
    return !supportIncompatible(s, x) && !supportIncompatible(s, y);
  };
  for (const memeGenre of [true, false]) {
    for (const x of libresA) {
      const y = libresB.find((i) => (memeGenre ? i.genre === x.genre : ensemble(x, i)));
      if (y) return { a: { interface: x.id }, b: { interface: y.id } };
    }
  }
  const voulu = support ? genrePour(support) : null;
  const [premierA, premierB] = [libresA[0], libresB[0]];
  // Des ports libres des deux côtés, mais qui ne vont pas ensemble : on garde celui de l'équipement qui a
  // le moins de ports (un poste, un serveur), et l'autre reçoit un port du même genre.
  if (premierA && premierB) {
    return a.interfaces.length <= b.interfaces.length
      ? { a: { interface: premierA.id }, b: { creer: premierA.genre } }
      : { a: { creer: premierB.genre }, b: { interface: premierB.id } };
  }
  if (premierA) return { a: { interface: premierA.id }, b: { creer: premierA.genre } };
  if (premierB) return { a: { creer: premierB.genre }, b: { interface: premierB.id } };
  return { a: { creer: voulu ?? "cuivre" }, b: { creer: voulu ?? "cuivre" } };
}

/** Les deux bouts d'un câble sont-ils en mode VLAN trunk (réglage des ports, ou de leur agrégat) ? */
const boutsEnTrunk = (schema: Schema, lien: Lien): boolean =>
  configDuBout(schema, lien.a)?.mode === "trunk" && configDuBout(schema, lien.b)?.mode === "trunk";

/**
 * Tire un câble entre deux équipements, sans dialogue ni choix de port (11A). Entre deux équipements
 * déjà reliés, c'est un câble de plus : du même type que les autres, et en trunk s'ils le sont tous.
 */
export function tirerCable(
  schema: Schema,
  idA: string,
  idB: string,
  options: { support?: Support; trunk?: boolean } = {},
): Lien {
  const [a, b] = [equipementDe(schema, idA), equipementDe(schema, idB)];
  if (!a || !b) throw new Refus("Équipement introuvable.");
  if (a.id === b.id) throw new Refus("Un câble relie deux équipements différents.");
  const autres = cablesEntre(schema, a.id, b.id);
  // La liaison est un agrégat : un câble de plus y est un port de plus.
  const agregat =
    options.support === undefined && options.trunk === undefined ? agregatReglable(schema, autres) : null;
  if (agregat) {
    reglerMembres(schema, agregat.id, portsDeLAgregat(schema, agregat) + 1);
    return agregat;
  }
  if (autres.length >= CABLES_MAX) {
    throw new Refus(`Pas plus de ${CABLES_MAX} câbles entre deux mêmes équipements.`);
  }
  const premier = autres[0];
  const memeType =
    premier && autres.every((l) => l.support === premier.support) ? premier.support : undefined;
  const support = options.support ?? memeType;
  const trunk = options.trunk ?? (autres.length > 0 && autres.every((l) => boutsEnTrunk(schema, l)));
  const bouts = choisirBouts(schema, a, b, support);
  const port = (equipement: Equipement, bout: BoutChoisi) =>
    "interface" in bout ? bout.interface : ajouterInterface(schema, equipement.id, { genre: bout.creer }).id;
  const lien = connecter(
    schema,
    { equipement: a.id, interface: port(a, bouts.a) },
    { equipement: b.id, interface: port(b, bouts.b) },
    support ? { support } : {},
  );
  if (trunk) {
    mettreEnTrunk(schema, [lien.id], true);
    // Dans un groupe en trunk, il transporte les mêmes VLAN que les autres.
    if (premier && options.trunk === undefined) {
      for (const bout of [lien.a, lien.b]) {
        const modele = premier.a.equipement === bout.equipement ? premier.a : premier.b;
        const config = configDuBout(schema, modele);
        if (config) ecrireConfigDuBout(schema, bout, { ...config });
      }
    }
  }
  return lien;
}

/**
 * Un câble a-t-il des réglages que le mode simplifié ne montre pas (nom, étiquette, tracé, agrégat…) ?
 * Le bouton « − » ne retire pas un tel câble à l'aveugle.
 */
export function aDesReglages(schema: Schema, lien: Lien): boolean {
  return Boolean(
    lien.nom ||
      lien.description ||
      lien.etiquette ||
      lien.categorie ||
      lien.technologie ||
      lien.supportPerso ||
      lien.notes ||
      lien.longueurM !== undefined ||
      lien.optique ||
      lien.mesure ||
      lien.couleur ||
      lien.trait ||
      lien.libelles ||
      lien.forme ||
      lien.palier ||
      lien.points.length > 0 ||
      lien.statut !== "actif" ||
      agregatDe(schema, lien.a) ||
      agregatDe(schema, lien.b),
  );
}

/**
 * Retire un port créé pour un câble (« if1 »…) quand plus rien ne s'en sert : ni câble, ni adresse, ni
 * réglage. Les ports du profil de l'équipement, et tout port nommé ou réglé à la main, restent.
 */
function retirerPortInutile(schema: Schema, bout: Extremite): void {
  const equipement = equipementDe(schema, bout.equipement);
  const port = equipement?.interfaces.find((i) => i.id === bout.interface);
  if (!equipement || !port || port.origine || !/^if\d+$/.test(port.nom)) return;
  const sert =
    interfaceOccupee(schema, equipement.id, port.id) ||
    port.adresses.length > 0 ||
    port.macs.length > 0 ||
    port.vlan !== undefined ||
    port.description !== undefined ||
    port.poe !== undefined ||
    equipement.gestion?.interface === port.id ||
    equipement.interfaces.some((i) => i.parent === port.id) ||
    schema.agregats.some((a) => a.equipement === equipement.id && a.membres.includes(port.id)) ||
    schema.relations.some(
      (r) =>
        (r.de === equipement.id && r.deInterface === port.id) ||
        (r.vers === equipement.id && r.versInterface === port.id),
    );
  if (!sert) equipement.interfaces = equipement.interfaces.filter((i) => i.id !== port.id);
}

/** Un réglage VLAN qui ne dit que « trunk » : celui que pose l'interrupteur du mode simplifié. */
const trunkSeul = (config: ConfigVlan | undefined): boolean => config?.mode === "trunk" && !config.tagues;

/**
 * Retire un câble. Un port ainsi libéré ne reste pas « en trunk » par l'interrupteur du mode simplifié
 * (sinon le prochain câble posé là naîtrait en trunk) ; un trunk réglé en avancé, avec ses VLAN tagués,
 * est le réglage du port : il reste.
 */
export function retirerCable(schema: Schema, lienId: string): void {
  const lien = schema.liens.find((l) => l.id === lienId);
  if (!lien) return;
  // Un agrégat n'a que ce câble : il part avec lui, et ses ports sont rendus.
  const membres = [lien.a, lien.b].flatMap((bout) =>
    (agregatDe(schema, bout)?.membres ?? []).map((m) => ({ equipement: bout.equipement, interface: m })),
  );
  const estMembre = (x: Extremite) =>
    membres.some((m) => m.equipement === x.equipement && m.interface === x.interface);
  const seul = !schema.liens.some((l) => l.id !== lienId && (estMembre(l.a) || estMembre(l.b)));
  if (membres.length > 0 && seul) retirerAgregat(schema, lienId);
  schema.liens = schema.liens.filter((l) => l.id !== lienId);
  for (const bout of [lien.a, lien.b, ...(seul ? membres : [])]) {
    const port = interfaceDe(schema, bout);
    if (!port || agregatDe(schema, bout) || interfaceOccupee(schema, bout.equipement, bout.interface)) continue;
    if (trunkSeul(port.vlan)) remettreEnAcces(port);
    retirerPortInutile(schema, bout);
  }
}

/** Le port revient en accès, avec ce qu'il avait d'autre ; sans rien d'autre, il n'a plus de réglage. */
function remettreEnAcces(cible: { vlan?: ConfigVlan }): void {
  if (!cible.vlan) return;
  const { mode: _mode, ...reste } = cible.vlan;
  if (Object.keys(reste).length > 0) cible.vlan = { ...reste, mode: "acces" };
  else delete cible.vlan;
}

/**
 * Efface des objets en mode simplifié : équipements (non verrouillés) avec leurs câbles, câbles,
 * relations, annotations et cadres. Les câbles passent par `retirerCable`.
 */
export function effacer(schema: Schema, ids: Set<string>): void {
  const equipements = new Set(schema.equipements.filter((e) => ids.has(e.id) && !e.verrou).map((e) => e.id));
  const cables = schema.liens.filter(
    (l) => ids.has(l.id) || equipements.has(l.a.equipement) || equipements.has(l.b.equipement),
  );
  for (const cable of cables) retirerCable(schema, cable.id);
  retirerEquipements(schema, equipements);
  schema.relations = schema.relations.filter((r) => !ids.has(r.id));
  schema.groupes = schema.groupes.filter((g) => !ids.has(g.id) || g.verrou);
  schema.annotations = schema.annotations.filter((n) => !ids.has(n.id));
}

/**
 * Règle le nombre de câbles entre deux équipements (1 à 8). Un de plus : `tirerCable`. Un de moins : le
 * dernier posé parmi ceux qui n'ont aucun réglage du mode avancé ; s'ils en ont tous, c'est refusé (on
 * ne choisit pas à la place de l'utilisateur lequel perdre : l'outil Effacer retire celui qu'il désigne).
 */
export function reglerNombreDeCables(schema: Schema, idA: string, idB: string, nombre: number): void {
  const voulu = Math.max(1, Math.min(CABLES_MAX, Math.round(nombre)));
  const cables = cablesEntre(schema, idA, idB);
  const enTrop = cables.length - voulu;
  if (enTrop > 0) {
    const simples = cables.filter((l) => !aDesReglages(schema, l));
    if (simples.length < enTrop) {
      throw new Refus(
        "Les câbles qui restent ont des réglages du mode avancé (nom, étiquette, tracé…) : retirez celui que vous voulez avec l'outil Effacer.",
      );
    }
    for (const cable of simples.slice(-enTrop)) retirerCable(schema, cable.id);
  }
  for (let n = cables.length; n < voulu; n++) tirerCable(schema, idA, idB);
}

// --- Type de câble ------------------------------------------------------------------

/**
 * Change le type d'un câble. Si un de ses ports ne va pas avec ce type (de la fibre sur un port cuivre),
 * ce bout passe sur un port libre qui convient, créé au besoin, comme on le rebrancherait en avancé ;
 * le réglage VLAN posé pour ce câble le suit. Les câbles d'un agrégat ont tous le même type : ses ports
 * changent de la même façon, un par un.
 */
export function changerTypeDeCable(schema: Schema, lienId: string, support: Support): void {
  const lien = schema.liens.find((l) => l.id === lienId);
  if (!lien) throw new Refus("Câble introuvable.");
  let rebranche = false;
  for (const cote of ["a", "b"] as const) {
    const bout = lien[cote];
    const equipement = equipementDe(schema, bout.equipement);
    if (!equipement) continue;
    const agregat = agregatDe(schema, bout);
    for (const ancien of agregat ? [...agregat.membres] : [bout.interface]) {
      const port = equipement.interfaces.find((i) => i.id === ancien);
      if (!port || !supportIncompatible(support, port)) continue;
      const cible =
        portsLibres(schema, equipement).find(
          (i) => i.nature === "physique" && !supportIncompatible(support, i),
        ) ?? ajouterInterface(schema, equipement.id, { genre: genrePour(support) ?? "cuivre" });
      if (ancien === bout.interface) {
        rebrancher(schema, lien.id, cote, { equipement: equipement.id, interface: cible.id });
        if (!agregat && port.vlan && !cible.vlan) {
          cible.vlan = port.vlan;
          delete port.vlan;
        }
        rebranche = true;
      }
      if (agregat) agregat.membres = agregat.membres.map((m) => (m === ancien ? cible.id : m));
      retirerPortInutile(schema, { equipement: equipement.id, interface: ancien });
    }
  }
  lien.support = support;
  if (support !== "personnalise") delete lien.supportPerso;
  if (rebranche) {
    // Le débit documenté suit les nouveaux ports, comme à la pose du câble.
    const vitesses = [interfaceDe(schema, lien.a)?.vitesse, interfaceDe(schema, lien.b)?.vitesse];
    const debit = Math.min(...vitesses.map((v) => v ?? Number.POSITIVE_INFINITY));
    if (Number.isFinite(debit)) lien.debit = debit;
    else delete lien.debit;
  }
}

// --- Agrégat de liens ---------------------------------------------------------------
//
// Un agrégat réunit plusieurs câbles entre deux équipements en un seul lien logique. Au modèle, c'est un
// câble et, de chaque côté, la liste des ports de l'agrégat ; le dessin montre un trait par port, sous
// un cercle. Ce n'est pas un trunk : un agrégat peut porter un trunk, ou non.

/** Ports au moins et au plus d'un agrégat réglé en mode simplifié. */
export const MEMBRES_MIN = 2;

/** Pourquoi un agrégat ne se fait pas avec un seul câble (aide de l'interrupteur, et refus). */
export const AIDE_AGREGAT_SEUL = "Un agrégat regroupe au moins deux câbles : ajoutez-en un.";
/** Pourquoi « − » s'arrête à deux ports. */
export const AIDE_AGREGAT_MINIMUM =
  "Un agrégat garde au moins deux câbles : décochez « Agrégat de liens » pour n'en garder qu'un.";

/** Le nombre de câbles qu'un agrégat réunit : ses ports, du côté qui en a le plus. */
export const portsDeLAgregat = (schema: Schema, lien: Lien): number =>
  Math.max(agregatDe(schema, lien.a)?.membres.length ?? 0, agregatDe(schema, lien.b)?.membres.length ?? 0);

/**
 * Ce que le mode simplifié ne sait pas exprimer d'un agrégat fait ailleurs (ou `null`) : plusieurs
 * agrégats sur une même liaison, un agrégat d'un seul côté, un agrégat qui a un câble par port.
 */
function agregatHorsDePortee(schema: Schema, cables: Lien[]): string | null {
  const agreges = cables.filter((l) => enAgregat(schema, l));
  const cles = new Set(agreges.map((l) => `${agregatDe(schema, l.a)?.id}|${agregatDe(schema, l.b)?.id}`));
  if (cles.size > 1) {
    const noms = [...new Set(agreges.map((l) => agregatDe(schema, l.a)?.nom ?? agregatDe(schema, l.b)?.nom))];
    return (
      `Cette liaison a plusieurs agrégats (${noms.join(", ")}) : ` +
      "le mode simplifié n'en règle qu'un par liaison. Ils se règlent en mode avancé."
    );
  }
  if (agreges.length > 1) {
    return "Cet agrégat a un câble par port au modèle : son nombre de ports se règle en mode avancé.";
  }
  const lien = agreges[0];
  if (lien && (!agregatDe(schema, lien.a) || !agregatDe(schema, lien.b))) {
    const cote = equipementDe(schema, (agregatDe(schema, lien.a) ? lien.a : lien.b).equipement);
    return `L'agrégat n'existe que du côté de ${cote?.nom ?? "un équipement"} : il se complète en mode avancé.`;
  }
  return null;
}

/** Le câble de l'agrégat d'une liaison, quand le mode simplifié sait le régler. */
function agregatReglable(schema: Schema, cables: Lien[]): Lien | null {
  if (agregatHorsDePortee(schema, cables) !== null) return null;
  return cables.find((l) => enAgregat(schema, l)) ?? null;
}

/**
 * Coche « Agrégat de liens » : les câbles ordinaires d'une liaison deviennent un agrégat (LACP), un
 * port par câble de chaque côté. Il en faut au moins deux. Ils prennent tous le type du câble gardé
 * (celui qui a des réglages du mode avancé, sinon le premier), et l'agrégat reprend son réglage VLAN.
 */
export function agreger(schema: Schema, idA: string, idB: string): Lien {
  const cables = cablesEntre(schema, idA, idB);
  const existant = cables.find((l) => enAgregat(schema, l));
  const ordinaires = cables.filter((l) => !enAgregat(schema, l));
  const hors = agregatHorsDePortee(schema, cables);
  if (hors) throw new Refus(hors);
  if (!existant && ordinaires.length < MEMBRES_MIN) throw new Refus(AIDE_AGREGAT_SEUL);
  const garde = existant ?? ordinaires.find((l) => aDesReglages(schema, l)) ?? (ordinaires[0] as Lien);
  if (!existant) creerAgregat(schema, garde.id);
  for (const autre of ordinaires) {
    if (autre.id === garde.id) continue;
    if (autre.support !== garde.support) changerTypeDeCable(schema, autre.id, garde.support);
    // Le port de ce câble, du côté du premier bout du câble gardé, rejoint l'agrégat ; son câble
    // disparaît, et le port d'en face rejoint l'agrégat d'en face (`basculerMembre`).
    const port = autre.a.equipement === garde.a.equipement ? autre.a : autre.b;
    basculerMembre(schema, garde.id, "a", port.interface);
  }
  return garde;
}

/**
 * Décoche « Agrégat de liens » : chaque paire de ports redevient un câble ordinaire, du même type, et
 * le réglage VLAN de l'agrégat est reporté sur chacun de ses ports (un trunk reste un trunk, câble par
 * câble). Un port sans vis-à-vis (agrégat inégal, fait en avancé) est rendu, sans réglage.
 */
export function desagreger(schema: Schema, lienId: string): Lien[] {
  const lien = schema.liens.find((l) => l.id === lienId);
  if (!lien) throw new Refus("Câble introuvable.");
  const cotes = [lien.a, lien.b].map((bout) => {
    const agregat = agregatDe(schema, bout);
    // Le port du câble d'abord : il garde son câble.
    const ports = [bout.interface, ...(agregat?.membres ?? []).filter((m) => m !== bout.interface)];
    return { equipement: bout.equipement, ports, vlan: agregat?.vlan, agregat };
  });
  if (!cotes.some((c) => c.agregat)) throw new Refus("Ce câble n'est pas un agrégat.");
  schema.agregats = schema.agregats.filter((a) => !cotes.some((c) => c.agregat?.id === a.id));
  const [ca, cb] = cotes as [(typeof cotes)[number], (typeof cotes)[number]];
  const paires = Math.min(ca.ports.length, cb.ports.length);
  const cables = [lien];
  for (const cote of cotes) {
    cote.ports.forEach((id, rang) => {
      const port = interfaceDe(schema, { equipement: cote.equipement, interface: id });
      if (!port) return;
      if (rang < paires && cote.vlan) port.vlan = { ...cote.vlan };
      else delete port.vlan;
    });
  }
  for (let rang = 1; rang < paires; rang++) {
    cables.push(
      connecter(
        schema,
        { equipement: ca.equipement, interface: ca.ports[rang] as string },
        { equipement: cb.equipement, interface: cb.ports[rang] as string },
        { support: lien.support, statut: lien.statut },
      ),
    );
  }
  for (const cote of cotes) {
    for (const id of cote.ports.slice(paires)) {
      retirerPortInutile(schema, { equipement: cote.equipement, interface: id });
    }
  }
  return cables;
}

/**
 * « + » et « − » sur un agrégat : il gagne ou rend un port de chaque côté (2 à 8). Un port de plus est
 * le premier port libre du même genre que les autres, créé au besoin ; un port de moins est le dernier
 * ajouté (jamais celui du câble).
 */
export function reglerMembres(schema: Schema, lienId: string, nombre: number): void {
  const lien = schema.liens.find((l) => l.id === lienId);
  if (!lien) throw new Refus("Câble introuvable.");
  if (nombre < MEMBRES_MIN) throw new Refus(AIDE_AGREGAT_MINIMUM);
  if (nombre > CABLES_MAX) throw new Refus(`Pas plus de ${CABLES_MAX} câbles entre deux mêmes équipements.`);
  for (const bout of [lien.a, lien.b]) {
    const agregat = agregatDe(schema, bout);
    const equipement = equipementDe(schema, bout.equipement);
    if (!agregat || !equipement) throw new Refus("Ce câble n'est pas un agrégat des deux côtés.");
    const modele = interfaceDe(schema, bout);
    const genre = modele?.genre ?? "cuivre";
    while (agregat.membres.length < nombre) {
      const libres = portsLibres(schema, equipement).filter(
        (i) => i.nature === "physique" && !supportIncompatible(lien.support, i),
      );
      // Un agrégat veut des liens identiques : même genre et même débit que le port du câble, d'abord.
      const port =
        libres.find((i) => i.genre === genre && i.vitesse === modele?.vitesse) ??
        libres.find((i) => i.genre === genre) ??
        libres[0] ??
        ajouterInterface(schema, equipement.id, {
          genre,
          ...(modele?.vitesse ? { vitesse: modele.vitesse } : {}),
        });
      agregat.membres.push(port.id);
    }
    while (agregat.membres.length > nombre) {
      const dernier = agregat.membres.filter((m) => m !== bout.interface).at(-1);
      if (!dernier) break;
      agregat.membres = agregat.membres.filter((m) => m !== dernier);
      retirerPortInutile(schema, { equipement: equipement.id, interface: dernier });
    }
  }
}

// --- Trunk --------------------------------------------------------------------------

/**
 * L'interrupteur « Trunk » : met les deux bouts de chaque câble en mode VLAN trunk, ou les remet en
 * accès. Seul le mode change : VLAN natif, tagués, voix et PVID restent tels quels (recocher les
 * retrouve). Un port hybride n'est pas « en trunk » : le couper ne le touche pas.
 */
export function mettreEnTrunk(schema: Schema, lienIds: string[], actif: boolean): void {
  for (const id of lienIds) {
    const lien = schema.liens.find((l) => l.id === id);
    if (!lien) continue;
    for (const bout of [lien.a, lien.b]) {
      const avant = configDuBout(schema, bout);
      if (actif && avant?.mode !== "trunk") ecrireConfigDuBout(schema, bout, { ...avant, mode: "trunk" });
      else if (!actif && avant?.mode === "trunk") {
        const cible = agregatDe(schema, bout) ?? interfaceDe(schema, bout);
        if (cible) remettreEnAcces(cible);
      }
    }
  }
}

/** Les VLAN d'une liste (« 10,20,30-40 ») contiennent-ils ce numéro ? */
function listeContient(liste: string, numero: number): boolean {
  return liste.split(",").some((morceau) => {
    const [debut, fin = debut] = morceau.split("-").map(Number);
    return debut !== undefined && fin !== undefined && numero >= debut && numero <= fin;
  });
}

/**
 * Lit les deux champs d'un trunk : le VLAN non tagué (un numéro de 1 à 4094, facultatif) et les VLAN
 * tagués (une liste, « 10,20,30-40 »). Rend les champs au propre, ou le message qui dit quoi corriger
 * et dans quel champ.
 */
export function lireVlansDuTrunk(
  nonTague: string,
  tagues: string,
): { champs: ChampsVlan } | { erreur: string; champ: "non-tague" | "tagues" } {
  const numero = lireNumeroVlan(nonTague);
  if (numero === null) {
    return { erreur: "Le VLAN non tagué est un seul numéro, de 1 à 4094.", champ: "non-tague" };
  }
  const liste = lireListeVlan(tagues);
  if (liste === null) {
    return {
      erreur: "Les VLAN tagués sont des numéros de 1 à 4094, séparés par des virgules : 10,20,30-40.",
      champ: "tagues",
    };
  }
  if (numero !== undefined && liste && listeContient(liste, numero)) {
    return {
      erreur: `Le VLAN ${numero} est déjà le VLAN non tagué : retirez-le des VLAN tagués.`,
      champ: "tagues",
    };
  }
  return {
    champs: { ...(numero !== undefined ? { nonTague: numero } : {}), ...(liste ? { tagues: liste } : {}) },
  };
}

/**
 * Écrit les VLAN d'un trunk sur les deux bouts de chaque câble, à l'identique (sur l'agrégat de chaque
 * côté s'il y en a un). Les bouts sont en trunk ; VLAN voix et PVID restent.
 */
export function ecrireVlansDuTrunk(schema: Schema, lienIds: string[], champs: ChampsVlan): void {
  for (const id of lienIds) {
    const lien = schema.liens.find((l) => l.id === id);
    if (!lien) continue;
    for (const bout of [lien.a, lien.b]) {
      // `configDepuis` garde voix et PVID ; le mode trunk vaut même sans VLAN tagué encore saisi.
      const { mode: _mode, ...reste } = configDepuis({}, configDuBout(schema, bout)) ?? { mode: "acces" };
      ecrireConfigDuBout(schema, bout, {
        ...reste,
        mode: "trunk",
        ...(champs.nonTague !== undefined ? { natif: champs.nonTague } : {}),
        ...(champs.tagues ? { tagues: champs.tagues } : {}),
      });
    }
  }
}

/**
 * Le VLAN d'un câble en accès (un port d'accès : un seul VLAN, non tagué), écrit à l'identique sur les
 * deux bouts de chaque câble, sur l'agrégat de chaque côté s'il y en a un ; `undefined` le retire. Seul
 * ce numéro change : VLAN natif et tagués d'un ancien trunk, voix et PVID restent. Un bout en trunk ou
 * en hybride n'est pas touché : l'interrupteur « Trunk » le règle.
 */
export function ecrireVlanDAcces(schema: Schema, lienIds: string[], vlan: number | undefined): void {
  for (const id of lienIds) {
    const lien = schema.liens.find((l) => l.id === id);
    if (!lien) continue;
    for (const bout of [lien.a, lien.b]) {
      const avant = configDuBout(schema, bout);
      if (avant && avant.mode !== "acces") continue;
      const { acces: _acces, ...reste } = avant ?? { mode: "acces" as const };
      const apres: ConfigVlan = vlan !== undefined ? { ...reste, mode: "acces", acces: vlan } : { ...reste, mode: "acces" };
      ecrireConfigDuBout(schema, bout, Object.keys(apres).length > 1 ? apres : undefined);
    }
  }
}

/** Ce que dit le réglage VLAN d'un bout, en clair (« trunk, U 1 · T 10,20 », « accès, VLAN 10 »). */
function decrireVlans(schema: Schema, config: ConfigVlan | undefined): string {
  if (!config) return "aucun réglage";
  const vlans = texteVlan(schema, config)?.texte;
  const mode = config.mode === "trunk" ? "trunk" : config.mode === "hybride" ? "hybride" : "accès";
  return vlans ? `${mode}, ${vlans}` : mode;
}

/** Les deux bouts d'un câble ont-ils le même réglage VLAN (même mode, mêmes VLAN) ? */
const boutsAccordes = (schema: Schema, lien: Lien): boolean =>
  (configDuBout(schema, lien.a)?.mode ?? "acces") === (configDuBout(schema, lien.b)?.mode ?? "acces") &&
  memesVlans(schema, lien);

/** « Aligner sur ce bout » : l'autre bout de chaque câble reprend le réglage VLAN du bout de `equipementId`. */
export function alignerVlans(schema: Schema, lienIds: string[], equipementId: string): void {
  for (const id of lienIds) {
    const lien = schema.liens.find((l) => l.id === id);
    if (!lien) continue;
    const [source, cible] = lien.a.equipement === equipementId ? [lien.a, lien.b] : [lien.b, lien.a];
    const config = configDuBout(schema, source);
    ecrireConfigDuBout(schema, cible, config ? { ...config } : undefined);
  }
}

// --- Le groupe des câbles d'une liaison ---------------------------------------------

/** Ce que la bulle d'un groupe de câbles montre et permet. */
export type GroupeDeCables = {
  /** Les deux équipements, dans le sens du premier câble. */
  a: Equipement;
  b: Equipement;
  cables: Lien[];
  /** Le nombre de câbles que le dessin montre : un agrégat compte pour autant de câbles que de ports. */
  nombre: number;
  /** Le type commun, ou `null` s'ils diffèrent. */
  support: Support | null;
  /** L'agrégat de la liaison : son câble, son nombre de ports, son nom (« Trk1 »). */
  agregat: { lien: Lien; ports: number; nom: string } | null;
  /** Les câbles ordinaires, hors de l'agrégat. */
  ordinaires: Lien[];
  /** Ce que le mode simplifié ne sait pas régler de cet agrégat, dit précisément (ou `null`). */
  horsDePortee: string | null;
  /** Tous les câbles sont en trunk (le dessin les annote). */
  trunk: boolean;
  /** Les VLAN du trunk, d'après le premier bout qui en a. */
  vlans: ChampsVlan;
  /** Un câble dont les deux bouts n'ont pas le même réglage VLAN : ce que dit chacun. */
  boutsDifferents: { a: string; b: string } | null;
  /** Des câbles parallèles n'ont pas les mêmes VLAN entre eux. */
  cablesDifferents: boolean;
};

/** Le groupe des câbles qui relient les deux mêmes équipements que `lienId`. */
export function groupeDeCables(schema: Schema, lienId: string): GroupeDeCables | null {
  const lien = schema.liens.find((l) => l.id === lienId);
  if (!lien) return null;
  const [a, b] = [equipementDe(schema, lien.a.equipement), equipementDe(schema, lien.b.equipement)];
  if (!a || !b) return null;
  const cables = cablesEntre(schema, a.id, b.id);
  const faisceaux = faisceauxDe(schema, cables);
  const agrege = cables.find((l) => enAgregat(schema, l));
  // Le bout de chaque câble du côté de `a`, puis du côté de `b`.
  const bout = (l: Lien, equipement: Equipement) => (l.a.equipement === equipement.id ? l.a : l.b);
  const desaccorde = cables.find((l) => !boutsAccordes(schema, l));
  const champsDuCable = (l: Lien): ChampsVlan => {
    for (const x of [bout(l, a), bout(l, b)]) {
      const champs = champsDe(configDuBout(schema, x));
      if (champs.nonTague !== undefined || champs.tagues) return champs;
    }
    return {};
  };
  const vlans = cables.map(champsDuCable);
  const premiers = vlans.find((c) => c.nonTague !== undefined || c.tagues) ?? {};
  return {
    a,
    b,
    cables,
    nombre: cables.reduce((n, l) => n + (faisceaux.get(l.id)?.brins ?? 1), 0),
    support: cables.every((l) => l.support === lien.support) ? lien.support : null,
    agregat: agrege
      ? {
          lien: agrege,
          ports: portsDeLAgregat(schema, agrege),
          nom: agregatDe(schema, agrege.a)?.nom ?? agregatDe(schema, agrege.b)?.nom ?? "",
        }
      : null,
    ordinaires: cables.filter((l) => !enAgregat(schema, l)),
    horsDePortee: agregatHorsDePortee(schema, cables),
    trunk: cables.every((l) => estTrunk(schema, l)),
    vlans: premiers,
    boutsDifferents: desaccorde
      ? {
          a: decrireVlans(schema, configDuBout(schema, bout(desaccorde, a))),
          b: decrireVlans(schema, configDuBout(schema, bout(desaccorde, b))),
        }
      : null,
    cablesDifferents: vlans.some(
      (c) => c.nonTague !== vlans[0]?.nonTague || (c.tagues ?? "") !== (vlans[0]?.tagues ?? ""),
    ),
  };
}

// --- Bonnes pratiques : des conseils, jamais des blocages ------------------------------

const estCommutateur = (e: Equipement): boolean => definition(e.type).type.startsWith("switch");
/** Un terminal qui ne lit pas les VLAN : poste, imprimante, caméra… (un téléphone IP, lui, les lit). */
const estTerminal = (e: Equipement): boolean =>
  definition(e.type).categorie === "terminaux" && e.type !== "telephone-ip";

/**
 * Les conseils que la bulle d'une liaison donne, quand c'est le cas. Aucun n'empêche rien : on
 * documente aussi ce qui n'est pas idéal.
 */
export function conseilsDeLiaison(schema: Schema, groupe: GroupeDeCables): string[] {
  const conseils: string[] = [];
  const { a, b, agregat, ordinaires } = groupe;
  if (estCommutateur(a) && estCommutateur(b)) {
    if (!agregat && ordinaires.length >= 2) {
      conseils.push(
        "Deux câbles entre deux commutateurs sans agrégat forment une boucle : le spanning tree n'en laissera passer qu'un. Cochez « Agrégat de liens ».",
      );
    } else if (agregat && ordinaires.length >= 1) {
      conseils.push(
        "Un câble de cette liaison n'est pas dans l'agrégat : entre deux commutateurs, il forme une boucle avec lui. Décochez puis recochez « Agrégat de liens » pour l'y mettre.",
      );
    }
  }
  if (groupe.trunk) {
    if (groupe.vlans.nonTague === undefined && !groupe.vlans.tagues) {
      conseils.push("Indiquez les VLAN que ce trunk transporte.");
    }
    if (groupe.vlans.nonTague === 1) {
      conseils.push(
        "Le VLAN 1 comme VLAN non tagué d'un trunk est déconseillé : préférez un VLAN réservé à cet usage.",
      );
    }
    if (estTerminal(a) || estTerminal(b)) {
      conseils.push("Un poste se branche d'ordinaire sur un port d'accès, pas sur un trunk.");
    }
  } else if (groupe.vlans.nonTague === 1) {
    conseils.push(
      "Le VLAN 1 est celui de tous les ports tant qu'on n'en a rien dit : mieux vaut un VLAN propre à cet usage.",
    );
  }
  if (agregat) {
    // Comme le diagnostic le dit des deux bouts d'un lien : des ports qui ne vont pas ensemble.
    const ports = [agregat.lien.a, agregat.lien.b].flatMap((x) => {
      const equipement = equipementDe(schema, x.equipement);
      const membres = agregatDe(schema, x)?.membres ?? [x.interface];
      return (equipement?.interfaces ?? []).filter((i) => membres.includes(i.id));
    });
    const genres = [...new Set(ports.map((i) => i.genre))];
    const vitesses = [...new Set(ports.map((i) => i.vitesse).filter((v): v is number => Boolean(v)))];
    if (genres.length > 1) {
      conseils.push(
        `Agrégat « ${agregat.nom} » : ports de genres différents (${genres.join(" et ")}). Un agrégat veut des liens identiques.`,
      );
    }
    if (vitesses.length > 1) {
      conseils.push(
        `Agrégat « ${agregat.nom} » : vitesses différentes entre ses ports (${vitesses.map(vitesseEnClair).join(" et ")}). Un agrégat veut des liens identiques.`,
      );
    }
  }
  return conseils;
}

// --- Adresse IP ---------------------------------------------------------------------

/** Une adresse IPv6 écrite en clair (groupes hexadécimaux, « :: » une fois au plus). */
function estIpv6(texte: string): boolean {
  if (!/^[0-9a-f:]+$/i.test(texte) || !texte.includes(":")) return false;
  const moities = texte.split("::");
  if (moities.length > 2) return false;
  const groupes = moities.flatMap((m) => (m === "" ? [] : m.split(":")));
  if (groupes.some((g) => !/^[0-9a-f]{1,4}$/i.test(g))) return false;
  return moities.length === 2 ? groupes.length <= 7 : groupes.length === 8;
}

/**
 * Lit une adresse saisie : IPv4 ou IPv6, préfixe facultatif (« 10.0.0.2 », « 10.0.0.2/24 »,
 * « fd00::1/64 »). Vide : pas d'adresse. Sinon l'adresse au propre, ou le message qui dit quoi corriger.
 */
export function lireAdresse(texte: string): { adresse: string | null } | { erreur: string } {
  const propre = texte.trim();
  if (propre === "") return { adresse: null };
  const [adresse = "", prefixe, ...trop] = propre.split("/");
  const v4 = ipv4(adresse) !== null;
  if (/\s/.test(propre) || (!v4 && !estIpv6(adresse))) {
    return { erreur: "Ce n'est pas une adresse IP. Exemples : 10.0.0.2, 10.0.0.2/24, fd00::1." };
  }
  if (prefixe === undefined) return { adresse };
  const maximum = v4 ? 32 : 128;
  if (trop.length > 0 || !/^\d{1,3}$/.test(prefixe) || Number(prefixe) > maximum) {
    const exemple = v4 ? "10.0.0.2/24" : "fd00::1/64";
    return { erreur: `Le préfixe, après « / », va de 0 à ${maximum}. Exemple : ${exemple}.` };
  }
  return { adresse: `${adresse}/${Number(prefixe)}` };
}

/**
 * Écrit l'adresse d'un équipement là où le mode avancé la lit : son adresse de gestion. Une saisie vide
 * retire l'adresse que le dessin montre (la gestion ; sinon la première adresse d'interface). Rend
 * l'adresse que le dessin montre ensuite : vide, sauf s'il reste une adresse d'interface posée en avancé.
 */
export function ecrireAdresse(schema: Schema, equipementId: string, texte: string): string {
  const equipement = equipementDe(schema, equipementId);
  if (!equipement) throw new Refus("Équipement introuvable.");
  const lue = lireAdresse(texte);
  if ("erreur" in lue) throw new Refus(lue.erreur);
  if (lue.adresse !== null) {
    equipement.gestion = { ...(equipement.gestion ?? {}), adresse: lue.adresse };
  } else if (equipement.gestion?.adresse) {
    const { adresse: _adresse, ...reste } = equipement.gestion;
    if (Object.keys(reste).length > 0) equipement.gestion = reste;
    else delete equipement.gestion;
  } else {
    equipement.interfaces.find((i) => i.adresses.length > 0)?.adresses.shift();
  }
  return adresseAffichee(equipement);
}

// --- Ce que le mode simplifié montre -------------------------------------------------

/**
 * Le schéma tel que le mode simplifié le dessine, à l'écran comme à l'export : ni nom de port, ni VLAN
 * d'un port d'accès, même si le schéma ou un câble en demande en mode avancé (le choix du schéma pour
 * les VLAN est mis de côté : c'est la vue simplifiée qui décide). Un trunk, lui, reste annoté. Rien
 * n'est modifié.
 */
export function epurer(schema: Schema): Schema {
  const { vlans: _vlans, ...affichage } = schema.affichage ?? {};
  return {
    ...schema,
    affichage: { ...affichage, ports: false },
    liens: schema.liens.map((l) =>
      l.libelles === "tout" ? { ...l, libelles: "nom" } : l.libelles === "ports" ? { ...l, libelles: "aucun" } : l,
    ),
  };
}

// --- Outils -------------------------------------------------------------------------

/**
 * L'outil en cours dans la barre d'outils. Simplifié : Choisir, Câble, Texte, Zone, Effacer. Avancé :
 * Choisir, Relation, Texte, Zone (son câble passe par le câble en main et le choix des ports).
 */
export type Outil = "choisir" | "cable" | "relation" | "texte" | "zone" | "effacer";
