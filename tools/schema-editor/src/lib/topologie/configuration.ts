// La configuration d'un équipement dans le volet du mode avancé (30C, 30D) : les ports réglés plusieurs à la
// fois (accès, trunk, PoE, activation), une plage de ports ajoutée d'un coup, la configuration copiée d'un
// switch sur d'autres, et les conseils de bonnes pratiques de l'onglet Réseau. Tout passe par le schéma reçu ;
// l'annulation reste celle de l'éditeur.

import { definition } from "./catalogue";
import type { ConfigVlan, Equipement, GenreInterface, GroupePorts, Interface, ModeleMateriel, Schema } from "./modele";
import { equipementDe } from "./modele";
import { ajouterInterface, changerProfil, Refus } from "./operations";
import { groupesDuMembre, nomDePort, PORTS_MAX } from "./ports";
import { agregatDe, champsDe, configDepuis, configDuBout, ecrireConfigDuBout, lireListeVlan } from "./vlans";

// --- Lire un port ----------------------------------------------------------------------------------------------------

/** Le VLAN d'un port tel que le tableau l'écrit : « 20 » en accès, « U 1 · T 10,20,30 » en trunk. */
export function texteVlanDuPort(config: ConfigVlan | undefined): string {
  const { nonTague, tagues } = champsDe(config);
  if (!tagues) return nonTague !== undefined ? String(nonTague) : "";
  return [nonTague !== undefined ? `U ${nonTague}` : "", `T ${tagues}`].filter(Boolean).join(" · ");
}

/** Le numéro d'un port sur la façade : son numéro, sinon les chiffres qui finissent son nom. */
export function numeroAffiche(iface: Interface): string {
  if (iface.numero !== undefined) return String(iface.numero);
  return /(\d+)\D*$/.exec(iface.nom)?.[1] ?? iface.nom.slice(0, 3);
}

/** Les ports entre deux ports d'une liste ordonnée, les deux compris (Maj+clic). */
export function suiteEntre(ordre: string[], de: string, a: string): string[] {
  const i = ordre.indexOf(de);
  const j = ordre.indexOf(a);
  if (i < 0 || j < 0) return j < 0 ? [] : [a];
  return ordre.slice(Math.min(i, j), Math.max(i, j) + 1);
}

// --- Plusieurs ports d'un coup -----------------------------------------------------------------------------------------

function portsDe(schema: Schema, equipementId: string, ids: Iterable<string>): Interface[] {
  const equipement = equipementDe(schema, equipementId);
  if (!equipement) throw new Refus("Équipement introuvable.");
  const voulus = new Set(ids);
  const ports = equipement.interfaces.filter((i) => voulus.has(i.id));
  if (ports.length === 0) throw new Refus("Choisissez au moins un port.");
  return ports;
}

function verifierVlan(n: number | undefined, quoi: string): void {
  if (n !== undefined && !(Number.isInteger(n) && n >= 1 && n <= 4094))
    throw new Refus(`${quoi} : un numéro de 1 à 4094.`);
}

/** Les ports choisis passent en accès sur `vlan` (la voix et le PVID déjà notés restent). Rend leur nombre. */
export function mettreEnAcces(schema: Schema, equipementId: string, ids: Iterable<string>, vlan: number): number {
  verifierVlan(vlan, "VLAN d'accès");
  const ports = portsDe(schema, equipementId, ids);
  for (const p of ports) {
    const bout = { equipement: equipementId, interface: p.id };
    ecrireConfigDuBout(schema, bout, configDepuis({ nonTague: vlan }, configDuBout(schema, bout)));
  }
  return ports.length;
}

/** Les ports choisis passent en trunk : VLAN tagués (« 10,20,30-40 ») et VLAN natif facultatif. Rend leur nombre. */
export function mettreEnTrunk(
  schema: Schema,
  equipementId: string,
  ids: Iterable<string>,
  reglage: { natif?: number; tagues: string },
): number {
  verifierVlan(reglage.natif, "VLAN natif");
  const tagues = lireListeVlan(reglage.tagues);
  if (!tagues) throw new Refus("VLAN tagués : des numéros de 1 à 4094, séparés par des virgules (« 10,20,30-40 »).");
  const ports = portsDe(schema, equipementId, ids);
  for (const p of ports) {
    const bout = { equipement: equipementId, interface: p.id };
    const champs = { tagues, ...(reglage.natif !== undefined ? { nonTague: reglage.natif } : {}) };
    ecrireConfigDuBout(schema, bout, configDepuis(champs, configDuBout(schema, bout)));
  }
  return ports.length;
}

/** Le PoE des ports choisis : fourni à tous, ou retiré à tous s'ils le fournissaient déjà tous. Rend le nouvel état. */
export function basculerPoe(schema: Schema, equipementId: string, ids: Iterable<string>): boolean {
  const ports = portsDe(schema, equipementId, ids);
  const fournir = !ports.every((p) => p.poe?.fournit);
  for (const p of ports) {
    if (fournir) p.poe = { ...(p.poe ?? {}), fournit: true };
    else if (p.poe) {
      delete p.poe.fournit;
      if (Object.keys(p.poe).length === 0) delete p.poe;
    }
  }
  return fournir;
}

/** Les ports choisis désactivés, ou réactivés s'ils l'étaient déjà tous. Rend le nouvel état. */
export function basculerActivation(schema: Schema, equipementId: string, ids: Iterable<string>): Interface["admin"] {
  const ports = portsDe(schema, equipementId, ids);
  const etat = ports.every((p) => p.admin === "desactive") ? "actif" : "desactive";
  for (const p of ports) p.admin = etat;
  return etat;
}

// --- Une plage de ports ------------------------------------------------------------------------------------------------

/**
 * Une plage de ports à ajouter : de `de` à `a`, nommés par `motif` (`{m}` le membre, `{p}` le numéro, comme les
 * profils de ports.ts), avec le genre, le connecteur et la vitesse des ports qu'elle prolonge.
 */
export type Plage = {
  /** Membre du stack qui reçoit les ports (1 pour un équipement seul). */
  membre: number;
  motif: string;
  de: number;
  a: number;
  genre: GenreInterface;
  connecteur?: string;
  vitesse?: number;
  poe?: boolean;
};

/** Le motif d'un nom de port : les derniers chiffres deviennent `{p}` (« Gi1/0/24 » : « Gi1/0/{p} »). */
function motifDuNom(nom: string): { motif: string; numero: number } | null {
  const trouve = /(\d+)(\D*)$/.exec(nom);
  if (!trouve?.[1]) return null;
  return { motif: `${nom.slice(0, trouve.index)}{p}${trouve[2] ?? ""}`, numero: Number(trouve[1]) };
}

/** Le motif d'un port : celui de son groupe de ports s'il vient d'un profil, sinon celui de son nom. */
function motifDuPort(
  equipement: Equipement,
  modeles: ModeleMateriel[],
  iface: Interface,
): { motif: string; numero: number } | null {
  if (iface.origine && iface.nomAuto !== false && iface.numero !== undefined) {
    const [m, g] = iface.origine.split(":").map(Number);
    const membre = equipement.membres.find((x) => x.numero === m);
    const groupe = membre && g !== undefined ? groupesDuMembre(membre, modeles)[g] : undefined;
    if (groupe) return { motif: groupe.motif, numero: iface.numero };
  }
  return motifDuNom(iface.nom);
}

const membreDuPort = (equipement: Equipement, i: Interface) =>
  equipement.membres.length > 1 ? (i.membre ?? null) : (equipement.membres[0]?.numero ?? 1);

/**
 * La plage proposée sur un membre : la suite de son dernier port physique, autant de ports qu'il en a déjà sous
 * ce nommage (24 ports « 1/0/1 » à « 1/0/24 » : « 1/0/25 » à « 1/0/48 »). Rien de physique : `null`.
 */
export function plageProposee(equipement: Equipement, modeles: ModeleMateriel[], membre: number): Plage | null {
  const physiques = equipement.interfaces.filter(
    (i) => i.nature === "physique" && i.genre !== "radio" && membreDuPort(equipement, i) === membre,
  );
  const dernier = [...physiques].reverse().find((i) => motifDuPort(equipement, modeles, i) !== null);
  const lu = dernier ? motifDuPort(equipement, modeles, dernier) : null;
  if (!lu) return null;
  const memes = physiques.filter((i) => motifDuPort(equipement, modeles, i)?.motif === lu.motif);
  // Le réglage le plus courant parmi ces ports : celui des ports d'accès, pas celui des deux uplinks du bout.
  const comptes = new Map<string, { n: number; port: Interface }>();
  for (const i of memes) {
    const cle = `${i.genre}|${i.connecteur ?? ""}|${i.vitesse ?? ""}|${i.poe?.fournit ? 1 : 0}`;
    const c = comptes.get(cle);
    comptes.set(cle, { n: (c?.n ?? 0) + 1, port: c?.port ?? i });
  }
  const modele = [...comptes.values()].sort((x, y) => y.n - x.n)[0]?.port ?? dernier;
  const plus = Math.max(...memes.map((i) => motifDuPort(equipement, modeles, i)?.numero ?? 0));
  const de = plus + 1;
  return {
    membre,
    motif: lu.motif,
    de,
    a: de + Math.min(PORTS_MAX, memes.length) - 1,
    genre: modele?.genre ?? "cuivre",
    ...(modele?.connecteur ? { connecteur: modele.connecteur } : {}),
    ...(modele?.vitesse ? { vitesse: modele.vitesse } : {}),
    ...(modele?.poe?.fournit ? { poe: true } : {}),
  };
}

/** Le nom du premier et du dernier port de la plage. */
export const bornesDeLaPlage = (plage: Plage): [string, string] => [
  nomDePort(plage.motif, plage.membre, plage.de),
  nomDePort(plage.motif, plage.membre, plage.a),
];

/**
 * Ajoute une plage de ports. Le membre a son propre profil de ports : la plage y devient un groupe de ports (elle
 * suit alors le profil, comme les autres) ; sinon, des interfaces ajoutées à la main. Un nom déjà pris est refusé.
 * Rend le nombre de ports ajoutés.
 */
export function ajouterPlage(schema: Schema, equipementId: string, plage: Plage): number {
  const equipement = equipementDe(schema, equipementId);
  if (!equipement) throw new Refus("Équipement introuvable.");
  const { de, a } = plage;
  if (!Number.isInteger(de) || !Number.isInteger(a) || de < 0 || a < de)
    throw new Refus("La plage va d'un numéro au même ou à un plus grand.");
  const nombre = a - de + 1;
  if (nombre > PORTS_MAX) throw new Refus(`Une plage compte au plus ${PORTS_MAX} ports.`);
  if (!plage.motif.includes("{p}")) throw new Refus("Le nom des ports doit contenir {p}, le numéro.");
  const pris = new Set(equipement.interfaces.map((i) => i.nom));
  for (let n = de; n <= a; n++) {
    const nom = nomDePort(plage.motif, plage.membre, n);
    if (pris.has(nom)) throw new Refus(`Le port « ${nom} » existe déjà.`);
  }
  const membre = equipement.membres.find((m) => m.numero === plage.membre);
  if (membre?.groupes) {
    const groupe: GroupePorts = {
      nom: `Ports ${de} à ${a}`,
      motif: plage.motif,
      nombre,
      premier: de,
      genre: plage.genre,
      ...(plage.connecteur ? { connecteur: plage.connecteur } : {}),
      ...(plage.vitesse ? { vitesse: plage.vitesse } : {}),
      ...(plage.poe ? { poe: true } : {}),
    };
    const resultat = changerProfil(schema, equipementId, (e) => {
      const m = e.membres.find((x) => x.numero === plage.membre);
      if (m) m.groupes = [...(m.groupes ?? []), groupe];
    });
    if (!resultat.applique) throw new Refus("Le profil des ports ne peut pas recevoir cette plage.");
    return nombre;
  }
  const empile = equipement.membres.length > 1 || plage.motif.includes("{m}");
  for (let n = de; n <= a; n++) {
    ajouterInterface(schema, equipementId, {
      nom: nomDePort(plage.motif, plage.membre, n),
      numero: n,
      ...(empile ? { membre: plage.membre } : {}),
      genre: plage.genre,
      nature: "physique",
      ...(plage.connecteur ? { connecteur: plage.connecteur } : {}),
      ...(plage.vitesse ? { vitesse: plage.vitesse } : {}),
      ...(plage.poe ? { poe: { fournit: true } } : {}),
    });
  }
  return nombre;
}

// --- Copier la configuration -------------------------------------------------------------------------------------------

/** Un port copié : son nom, sa place sur la façade (membre et numéro) s'il en a une, et ce qui se reprend. */
export type PortCopie = {
  nom: string;
  membre?: number;
  numero?: number;
  vlan?: ConfigVlan;
  admin: Interface["admin"];
  poe: boolean;
};

/** La configuration copiée d'un équipement : le réglage de chaque port et le VLAN de gestion. */
export type ConfigurationCopiee = {
  source: string;
  nom: string;
  type: string;
  ports: PortCopie[];
  gestionVlan?: number;
};

/**
 * La place d'un port sur la façade : son membre et son numéro (« 1:7 »). « 1/7 » d'un switch et « Gi1/0/7 » d'un
 * autre switch du même modèle ont la même place. Seuls les ports physiques numérotés en ont une, hors radio.
 */
function placeDuPort(port: { membre?: number; numero?: number }): string | undefined {
  return port.numero === undefined ? undefined : `${port.membre ?? 1}:${port.numero}`;
}

const aUnePlace = (i: Interface) => i.nature === "physique" && i.genre !== "radio";

export function copierConfiguration(schema: Schema, equipementId: string): ConfigurationCopiee {
  const equipement = equipementDe(schema, equipementId);
  if (!equipement) throw new Refus("Équipement introuvable.");
  return {
    source: equipement.id,
    nom: equipement.nom,
    type: equipement.type,
    ports: equipement.interfaces.map((i) => {
      const vlan = configDuBout(schema, { equipement: equipement.id, interface: i.id });
      return {
        nom: i.nom,
        ...(aUnePlace(i) && i.numero !== undefined ? { membre: i.membre ?? 1, numero: i.numero } : {}),
        ...(vlan ? { vlan: JSON.parse(JSON.stringify(vlan)) as ConfigVlan } : {}),
        admin: i.admin,
        poe: i.poe?.fournit === true,
      };
    }),
    ...(equipement.gestion?.vlan !== undefined ? { gestionVlan: equipement.gestion.vlan } : {}),
  };
}

/**
 * Comment les ports copiés trouvent ceux de la cible : par leur nom seulement (« nom »), ou par leur nom puis, pour
 * ceux qui n'en trouvent pas, par leur place, même membre et même numéro (« place »).
 */
export type Correspondance = "nom" | "place";

/** Le port de la cible que reprend chaque port copié, et comment il l'a trouvé. */
export type Appariement = { source: PortCopie; cible: Interface; par: "nom" | "place" }[];

export function apparier(cible: Equipement, configuration: ConfigurationCopiee, par: Correspondance): Appariement {
  const parNom = new Map(cible.interfaces.map((i) => [i.nom, i]));
  const pris = new Set<string>();
  const paires: Appariement = [];
  const restants: PortCopie[] = [];
  for (const p of configuration.ports) {
    const iface = parNom.get(p.nom);
    if (iface && !pris.has(iface.id)) {
      pris.add(iface.id);
      paires.push({ source: p, cible: iface, par: "nom" });
    } else restants.push(p);
  }
  if (par === "nom") return paires;
  const parPlace = new Map<string, Interface>();
  for (const i of cible.interfaces) {
    const place = aUnePlace(i) ? placeDuPort(i) : undefined;
    if (place !== undefined && !pris.has(i.id) && !parPlace.has(place)) parPlace.set(place, i);
  }
  for (const p of restants) {
    const place = placeDuPort(p);
    const iface = place === undefined ? undefined : parPlace.get(place);
    if (!iface || place === undefined) continue;
    parPlace.delete(place);
    paires.push({ source: p, cible: iface, par: "place" });
  }
  return paires;
}

/** Ce qu'un collage reprendrait sur une cible, pour le dire avant : par le nom, puis par la place, et un exemple. */
export function apercuDuCollage(
  cible: Equipement,
  configuration: ConfigurationCopiee,
): { parNom: number; parPlace: number; exemple?: { de: string; vers: string } } {
  const paires = apparier(cible, configuration, "place");
  const parNom = paires.filter((p) => p.par === "nom").length;
  const premiere = paires.find((p) => p.par === "place");
  return {
    parNom,
    parPlace: paires.length - parNom,
    ...(premiere ? { exemple: { de: premiere.source.nom, vers: premiere.cible.nom } } : {}),
  };
}

/**
 * Colle une configuration sur un équipement : chaque port apparié (par son nom, ou par sa place si `par` le permet)
 * reprend le mode, les VLAN, l'activation et le PoE ; le VLAN de gestion suit. Les noms, les adresses et les câbles
 * ne changent pas. Sans aucun port apparié, rien ne change, pas même la gestion. Rend le nombre de ports repris (et
 * ceux trouvés par leur place), les noms des ports copiés restés sans port sur la cible, et si la gestion a suivi.
 */
export function collerConfiguration(
  schema: Schema,
  cibleId: string,
  configuration: ConfigurationCopiee,
  par: Correspondance = "nom",
): { regles: number; parPlace: number; absents: string[]; gestion: boolean } {
  const cible = equipementDe(schema, cibleId);
  if (!cible) throw new Refus("Équipement introuvable.");
  if (cible.id === configuration.source) throw new Refus("Un équipement a déjà sa propre configuration.");
  const paires = apparier(cible, configuration, par);
  const repris = new Set(paires.map((p) => p.source));
  const absents = configuration.ports.filter((p) => !repris.has(p)).map((p) => p.nom);
  // Sans port en commun, la configuration ne touche à rien : la gestion seule ne suit pas.
  if (paires.length === 0 && configuration.ports.length > 0) return { regles: 0, parPlace: 0, absents, gestion: false };
  for (const { source: p, cible: iface } of paires) {
    ecrireConfigDuBout(
      schema,
      { equipement: cible.id, interface: iface.id },
      p.vlan ? (JSON.parse(JSON.stringify(p.vlan)) as ConfigVlan) : undefined,
    );
    iface.admin = p.admin;
    if (p.poe) iface.poe = { ...(iface.poe ?? {}), fournit: true };
    else if (iface.poe?.fournit) {
      delete iface.poe.fournit;
      if (Object.keys(iface.poe).length === 0) delete iface.poe;
    }
  }
  const gestion = configuration.gestionVlan;
  if (gestion !== undefined) cible.gestion = { ...(cible.gestion ?? {}), vlan: gestion };
  return {
    regles: paires.length,
    parPlace: paires.filter((p) => p.par === "place").length,
    absents,
    gestion: gestion !== undefined,
  };
}

const nomsCites = (noms: string[]) =>
  noms.length === 1 ? `« ${noms[0]} »` : `${noms.slice(0, -1).map((n) => `« ${n} »`).join(", ")} ni « ${noms.at(-1)} »`;

/**
 * Colle une configuration sur plusieurs équipements d'un coup et rend ce qu'il faut en dire : « Configuration de
 * « SW-Étage1 » collée sur « SW-Étage2 » : 28 ports repris, VLAN de gestion 99. » Si aucune cible n'a de port en
 * commun, rien ne change et le refus le dit : « Aucun port de même nom sur « Cœur » : rien n'a changé. » Appelé
 * dans `editeur.agir`, le collage s'annule en une fois.
 */
export function collerSurLesCibles(
  schema: Schema,
  cibles: Iterable<string>,
  configuration: ConfigurationCopiee,
  par: Correspondance = "place",
): string {
  const voulues = [...cibles].filter((id) => id !== configuration.source && equipementDe(schema, id));
  if (voulues.length === 0) throw new Refus("Choisissez au moins un autre équipement que celui copié.");
  const nomDe = (id: string) => equipementDe(schema, id)?.nom ?? "?";
  const laisses: string[] = [];
  const touches: string[] = [];
  let regles = 0;
  let parPlace = 0;
  let gestion = false;
  for (const id of voulues) {
    const r = collerConfiguration(schema, id, configuration, par);
    if (r.regles === 0 && !r.gestion) {
      laisses.push(nomDe(id));
      continue;
    }
    touches.push(nomDe(id));
    regles += r.regles;
    parPlace += r.parPlace;
    gestion ||= r.gestion;
  }
  const commun = par === "place" ? "de même nom ni à la même place" : "de même nom";
  if (touches.length === 0) throw new Refus(`Aucun port ${commun} sur ${nomsCites(laisses)} : rien n'a changé.`);
  const sur = touches.length === 1 ? `« ${touches[0]} »` : `${touches.length} équipements`;
  const place = parPlace === 0 ? "" : parPlace === regles ? ", par leur place" : ` (dont ${parPlace} par leur place)`;
  const repris = `${regles} ${regles > 1 ? "ports repris" : "port repris"}${place}`;
  const detail = gestion ? `${repris}, VLAN de gestion ${configuration.gestionVlan}` : repris;
  const reste =
    laisses.length === 0
      ? ""
      : ` Aucun port ${commun} sur ${nomsCites(laisses)} : ${laisses.length > 1 ? "laissés tels quels" : "laissé tel quel"}.`;
  return `Configuration de « ${configuration.nom} » collée sur ${sur} : ${detail}.${reste} Ctrl+Z annule.`;
}


// --- L'onglet Réseau -----------------------------------------------------------------------------------------------------

const TYPES_QUI_ROUTENT = new Set([
  "switch-coeur",
  "switch-distribution",
  "routeur",
  "pare-feu",
  "sd-wan",
  "cato-socket",
  "passerelle-vpn",
  "cpe",
]);

/** L'équipement route-t-il entre VLAN (routeur, pare-feu, switch de niveau 3) ? Ou en a-t-il déjà les interfaces ? */
export function routeEntreVlan(equipement: Equipement): boolean {
  return (
    TYPES_QUI_ROUTENT.has(equipement.type) ||
    equipement.proprietes.couche === "Niveau 3" ||
    equipement.interfaces.some((i) => i.nature === "vlan")
  );
}

/** Les interfaces de routage par VLAN (SVI) : leur VLAN est celui de leur accès (adressage.ts le lit ainsi). */
export const interfacesDeRoutage = (equipement: Equipement): Interface[] =>
  equipement.interfaces.filter((i) => i.nature === "vlan");

/** Une interface de routage de plus sur le VLAN `numero` (« Vlan10 »). */
export function ajouterInterfaceVlan(schema: Schema, equipementId: string, numero: number): Interface {
  verifierVlan(numero, "VLAN");
  const equipement = equipementDe(schema, equipementId);
  if (!equipement) throw new Refus("Équipement introuvable.");
  if (interfacesDeRoutage(equipement).some((i) => i.vlan?.acces === numero))
    throw new Refus(`Le VLAN ${numero} a déjà son interface de routage.`);
  const pris = new Set(equipement.interfaces.map((i) => i.nom));
  let nom = `Vlan${numero}`;
  for (let n = 2; pris.has(nom); n++) nom = `Vlan${numero}-${n}`;
  return ajouterInterface(schema, equipementId, {
    nom,
    genre: "virtuel",
    nature: "vlan",
    vlan: { mode: "acces", acces: numero },
  });
}

/** Une proposition de numéro pour la prochaine interface de routage : le premier VLAN du schéma qui n'en a pas. */
export function vlanSansInterface(schema: Schema, equipement: Equipement): number {
  const pris = new Set(interfacesDeRoutage(equipement).map((i) => i.vlan?.acces));
  const declares = [...schema.vlans].sort((a, b) => a.numero - b.numero).map((v) => v.numero);
  const libre = declares.find((n) => !pris.has(n));
  if (libre !== undefined) return libre;
  let n = Math.max(0, ...[...pris].filter((x): x is number => x !== undefined)) + 10;
  n -= n % 10;
  return Math.min(4094, Math.max(10, n));
}

const pluriel = (n: number, un: string, plusieurs: string) => (n === 1 ? un : plusieurs);

/**
 * Les conseils de bonnes pratiques d'un équipement, jamais bloquants : un VLAN natif 1 sur ses trunks, une gestion
 * sur le VLAN 1, ou sur un VLAN que des ports d'accès partagent. Le diagnostic du schéma ne les compte pas.
 */
export function conseilsReseau(schema: Schema, equipement: Equipement): string[] {
  const conseils: string[] = [];
  // Un agrégat ne compte qu'une fois : ses ports partagent sa configuration.
  const vus = new Set<string>();
  const configs: { mode: ConfigVlan["mode"]; natif?: number; acces?: number }[] = [];
  for (const i of equipement.interfaces) {
    if (i.nature !== "physique") continue;
    const bout = { equipement: equipement.id, interface: i.id };
    const agregat = agregatDe(schema, bout);
    const cle = agregat ? `a:${agregat.id}` : `i:${i.id}`;
    if (vus.has(cle)) continue;
    vus.add(cle);
    const config = configDuBout(schema, bout);
    if (!config) continue;
    configs.push({
      mode: config.mode,
      ...(config.natif !== undefined ? { natif: config.natif } : {}),
      ...(config.acces !== undefined ? { acces: config.acces } : {}),
    });
  }
  const natifs = configs.filter((c) => c.mode !== "acces" && (c.natif ?? c.acces) === 1).length;
  if (natifs > 0) {
    const quoi = natifs === 1 ? "du trunk" : natifs === 2 ? "des deux trunks" : `des ${natifs} trunks`;
    conseils.push(
      `Le VLAN 1 est le VLAN natif ${quoi} : mieux vaut un VLAN natif inutilisé (par ex. 999). Conseil, rien n'est bloqué.`,
    );
  }
  const gestion = equipement.gestion?.vlan;
  if (gestion === 1) {
    conseils.push(
      "La gestion passe par le VLAN 1 : mieux vaut un VLAN de gestion dédié (par ex. 99). Conseil, rien n'est bloqué.",
    );
  } else if (gestion !== undefined) {
    const partages = configs.filter((c) => c.mode === "acces" && c.acces === gestion).length;
    if (partages > 0 && definition(equipement.type).categorie === "reseau") {
      conseils.push(
        `Le VLAN de gestion ${gestion} sert aussi à ${partages} ${pluriel(partages, "port", "ports")} d'accès : mieux vaut le réserver à la gestion. Conseil, rien n'est bloqué.`,
      );
    }
  }
  return conseils;
}
