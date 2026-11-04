// Opérations sur un schéma (D39). Elles modifient le schéma reçu et
// refusent, avec un message clair, ce qui le rendrait incohérent (deux câbles
// sur une même interface point à point, extrémité absente…). L'annulation est
// assurée par l'éditeur (copie du schéma avant chaque opération).

import { definition } from "./catalogue";
import type {
  Agregat,
  Annotation,
  Equipement,
  Extremite,
  Interface,
  Lien,
  ModeleMateriel,
  Point,
  Relation,
  Schema,
  Support,
} from "./modele";
import { equipementDe, interfaceDe, liensDeLInterface, nouvelId } from "./modele";
import { effetsDuRetrait, membresAuNombre, regenerer } from "./ports";

export class Refus extends Error {}

/** « SW 3 » : le premier nom libre pour ce préfixe. */
export function nomLibre(schema: Schema, prefixe: string): string {
  const pris = new Set(schema.equipements.map((e) => e.nom));
  let n = 1;
  while (pris.has(`${prefixe} ${n}`)) n++;
  return `${prefixe} ${n}`;
}

/**
 * `voulu` s'il est libre ; sinon le numéro suivant (« PC 1 » pris : « PC 2 »,
 * « Pare-feu » pris : « Pare-feu 2 »).
 */
export function nomDisponible(schema: Pick<Schema, "equipements">, voulu: string): string {
  const pris = new Set(schema.equipements.map((e) => e.nom));
  if (!pris.has(voulu)) return voulu;
  const [, prefixe = voulu, numero] = /^(.*?)\s*(\d+)$/.exec(voulu) ?? [];
  let n = numero ? Number(numero) + 1 : 2;
  while (pris.has(`${prefixe} ${n}`)) n++;
  return `${prefixe} ${n}`;
}

/** Nouvel équipement du type demandé, avec ses interfaces par défaut. */
export function creerEquipement(schema: Schema, type: string, position: Point): Equipement {
  const def = definition(type);
  const equipement: Equipement = {
    id: nouvelId("d"),
    type: def.type,
    nom: nomLibre(schema, def.court),
    statut: "actif",
    tags: [],
    proprietes: {},
    membres: [{ numero: 1, groupes: def.ports.map((g) => ({ ...g })) }],
    interfaces: [],
    position: { x: Math.round(position.x), y: Math.round(position.y) },
  };
  equipement.interfaces = regenerer(equipement, schema.modeles, def.multiConnexion).interfaces;
  schema.equipements.push(equipement);
  return equipement;
}

/**
 * Changement du nombre de membres ou des groupes de ports : les interfaces
 * qui disparaîtraient sont rendues avec leurs liens, sans rien changer. Si
 * aucune n'a de lien, relation ni agrégat, le changement est appliqué.
 */
export function changerProfil(
  schema: Schema,
  equipementId: string,
  modifier: (equipement: Equipement) => void,
):
  | { applique: true }
  | { applique: false; retirees: Interface[]; effets: ReturnType<typeof effetsDuRetrait> } {
  const equipement = equipementDe(schema, equipementId);
  if (!equipement) throw new Refus("Équipement introuvable.");
  const essai: Equipement = cloner(equipement);
  modifier(essai);
  const { interfaces, retirees } = regenerer(essai, schema.modeles, definition(essai.type).multiConnexion);
  const effets = effetsDuRetrait(schema, equipementId, retirees);
  if (effets.liens.length + effets.relations.length + effets.agregats.length > 0) {
    return { applique: false, retirees, effets };
  }
  modifier(equipement);
  equipement.interfaces = interfaces;
  if (equipement.gestion?.interface && !interfaces.some((i) => i.id === equipement.gestion?.interface)) {
    delete equipement.gestion.interface;
  }
  return { applique: true };
}

/**
 * Décision explicite pour chaque lien touché par un changement de profil :
 * le réaffecter à une interface qui reste, ou le retirer. Les relations et
 * agrégats touchés perdent seulement la référence à l'interface retirée.
 */
export function appliquerProfil(
  schema: Schema,
  equipementId: string,
  modifier: (equipement: Equipement) => void,
  decisions: Map<string, { reaffecter: string } | "retirer">,
): void {
  const equipement = equipementDe(schema, equipementId);
  if (!equipement) throw new Refus("Équipement introuvable.");
  const essai: Equipement = cloner(equipement);
  modifier(essai);
  const { interfaces, retirees } = regenerer(essai, schema.modeles, definition(essai.type).multiConnexion);
  const restantes = new Set(interfaces.map((i) => i.id));
  const retireesIds = new Set(retirees.map((i) => i.id));
  const effets = effetsDuRetrait(schema, equipementId, retirees);
  for (const lien of effets.liens) {
    const decision = decisions.get(lien.id);
    if (!decision) throw new Refus(`Décision manquante pour le lien « ${lien.nom || lien.id} ».`);
    if (decision === "retirer") continue;
    if (!restantes.has(decision.reaffecter)) throw new Refus("L'interface de réaffectation n'existe plus.");
  }
  // Toutes les décisions sont valides : application.
  for (const lien of effets.liens) {
    const decision = decisions.get(lien.id);
    if (decision === "retirer") {
      schema.liens = schema.liens.filter((l) => l.id !== lien.id);
    } else if (decision) {
      const cote: "a" | "b" =
        lien.a.equipement === equipementId && retireesIds.has(lien.a.interface) ? "a" : "b";
      lien[cote] = { equipement: equipementId, interface: decision.reaffecter };
    }
  }
  for (const relation of effets.relations) {
    if (relation.de === equipementId && relation.deInterface && retireesIds.has(relation.deInterface))
      delete relation.deInterface;
    if (relation.vers === equipementId && relation.versInterface && retireesIds.has(relation.versInterface))
      delete relation.versInterface;
  }
  for (const agregat of effets.agregats) agregat.membres = agregat.membres.filter((m) => !retireesIds.has(m));
  modifier(equipement);
  equipement.interfaces = interfaces;
  if (equipement.gestion?.interface && !restantes.has(equipement.gestion.interface))
    delete equipement.gestion.interface;
}

/** Stack de `nombre` membres (chacun reprend le profil du premier). */
export const auNombreDeMembres = (nombre: number) => (equipement: Equipement) => {
  equipement.membres = membresAuNombre(equipement, nombre);
};

/** Pourquoi une interface ne peut pas recevoir un câble de plus (ou `null`). */
export function interfaceIndisponible(schema: Schema, extremite: Extremite, sauf?: string): string | null {
  const iface = interfaceDe(schema, extremite);
  if (!iface) return "interface introuvable";
  if (iface.multiConnexion) return null;
  if (iface.genre === "radio") return "une interface radio se relie par une association, pas par un câble";
  if (iface.nature === "vlan" || iface.nature === "boucle") return "interface logique : pas de câble";
  const occupe = liensDeLInterface(schema, extremite.equipement, extremite.interface).some(
    (l) => l.id !== sauf,
  );
  if (occupe) return "déjà occupée par un câble";
  // Membre d'un agrégat déjà câblé par un autre de ses ports : ce câble le représente (D51).
  const agregat = schema.agregats.find(
    (a) => a.equipement === extremite.equipement && a.membres.includes(extremite.interface),
  );
  const represente = agregat?.membres.some(
    (m) =>
      m !== extremite.interface &&
      liensDeLInterface(schema, extremite.equipement, m).some((l) => l.id !== sauf),
  );
  return agregat && represente ? `membre de l'agrégat « ${agregat.nom} », déjà câblé` : null;
}

/** Support proposé d'après les interfaces (fibre, Fibre Channel, virtuel…). */
export function supportPropose(a: Interface | undefined, b: Interface | undefined): Support {
  const genres = new Set([a?.genre, b?.genre]);
  if (genres.has("stockage")) return "fibre-channel";
  if (genres.has("fibre")) return "fibre-multimode";
  if (genres.has("virtuel"))
    return a?.genre === "virtuel" && b?.genre === "virtuel" ? "virtuel" : "operateur";
  if (genres.has("console")) return "console";
  if (genres.has("usb")) return "usb";
  return "cuivre";
}

/** Relie deux interfaces par un câble ; refuse une interface déjà occupée. */
export function connecter(schema: Schema, a: Extremite, b: Extremite, proprietes: Partial<Lien> = {}): Lien {
  if (a.equipement === b.equipement && a.interface === b.interface) {
    throw new Refus("Une interface ne peut pas être reliée à elle-même.");
  }
  for (const [extremite, cote] of [
    [a, "source"],
    [b, "destination"],
  ] as const) {
    const raison = interfaceIndisponible(schema, extremite);
    if (raison) {
      const nom = interfaceDe(schema, extremite)?.nom ?? "?";
      throw new Refus(`Interface ${cote} « ${nom} » : ${raison}.`);
    }
  }
  const ia = interfaceDe(schema, a);
  const ib = interfaceDe(schema, b);
  const vitesse = Math.min(ia?.vitesse ?? Number.POSITIVE_INFINITY, ib?.vitesse ?? Number.POSITIVE_INFINITY);
  const lien: Lien = {
    id: nouvelId("l"),
    a,
    b,
    support: supportPropose(ia, ib),
    statut: "actif",
    points: [],
    ...(Number.isFinite(vitesse) ? { debit: vitesse } : {}),
    ...proprietes,
  };
  schema.liens.push(lien);
  return lien;
}

/** Retire des équipements avec tout ce qui en dépend (liens, relations, agrégats, appartenances). */
export function retirerEquipements(schema: Schema, ids: Set<string>): void {
  schema.equipements = schema.equipements.filter((e) => !ids.has(e.id));
  schema.liens = schema.liens.filter((l) => !ids.has(l.a.equipement) && !ids.has(l.b.equipement));
  schema.relations = schema.relations.filter((r) => !ids.has(r.de) && !ids.has(r.vers));
  schema.agregats = schema.agregats.filter((a) => !ids.has(a.equipement));
  for (const groupe of schema.groupes) groupe.membres = groupe.membres.filter((m) => !ids.has(m));
}

/**
 * Contenu copié (Ctrl+C) : des équipements, les câbles entre eux et les
 * modèles matériels qu'ils citent. Il se colle dans le même schéma ou dans
 * un autre.
 */
export type PressePapiers = {
  equipements: Equipement[];
  liens: Lien[];
  modeles: ModeleMateriel[];
  /** Relations entre les équipements copiés (tunnel, hébergement…). */
  relations?: Relation[];
  /** Annotations (zones, notes) : celles des modèles de topologie. */
  annotations?: Annotation[];
  /** Agrégats (trunks) des équipements copiés. */
  agregats?: Agregat[];
};

/** Copie des équipements `ids` (et de ce qu'il faut pour les recoller ailleurs). */
export function copierEquipements(schema: Schema, ids: Set<string>): PressePapiers {
  const equipements = schema.equipements.filter((e) => ids.has(e.id));
  const cites = new Set(equipements.flatMap((e) => e.membres.map((m) => m.modele)).filter(Boolean));
  return cloner({
    equipements,
    liens: schema.liens.filter((l) => ids.has(l.a.equipement) && ids.has(l.b.equipement)),
    modeles: schema.modeles.filter((m) => cites.has(m.id)),
    relations: schema.relations.filter((r) => ids.has(r.de) && ids.has(r.vers)),
    annotations: schema.annotations.filter((n) => ids.has(n.id)),
    agregats: schema.agregats.filter((a) => ids.has(a.equipement)),
  });
}

/**
 * Colle une copie : nouveaux identifiants, noms libres (« SW 1 copie »),
 * décalée de `decalage`. Rend les identifiants des équipements collés.
 * Un modèle de topologie (D48) garde ses noms (s'ils sont libres) et ses
 * adresses : `{ suffixe: "", adresses: true }`.
 */
export function collerEquipements(
  schema: Schema,
  contenu: PressePapiers,
  decalage: Point = { x: 40, y: 40 },
  options: { suffixe?: string; adresses?: boolean } = {},
): string[] {
  const suffixe = options.suffixe ?? " copie";
  for (const modele of contenu.modeles)
    if (!schema.modeles.some((m) => m.id === modele.id)) schema.modeles.push(cloner(modele));
  const correspondance = new Map<string, string>();
  const copies: Equipement[] = [];
  for (const source of contenu.equipements) {
    const copie: Equipement = cloner(source);
    copie.id = nouvelId("d");
    copie.nom = nomDisponible(
      { equipements: [...schema.equipements, ...copies] },
      `${source.nom}${suffixe}`.trim(),
    );
    copie.position = { x: source.position.x + decalage.x, y: source.position.y + decalage.y };
    delete copie.verrou;
    correspondance.set(source.id, copie.id);
    copie.interfaces = copie.interfaces.map((i) => {
      const nouvelle = nouvelId("i");
      correspondance.set(`${source.id}/${i.id}`, nouvelle);
      return options.adresses
        ? { ...i, id: nouvelle, macs: [] }
        : { ...i, id: nouvelle, macs: [], adresses: [] };
    });
    for (const i of copie.interfaces) {
      if (i.parent) i.parent = correspondance.get(`${source.id}/${i.parent}`) ?? i.parent;
    }
    if (copie.gestion?.interface) {
      copie.gestion.interface = correspondance.get(`${source.id}/${copie.gestion.interface}`);
    }
    if (!options.adresses) delete copie.gestion?.adresse;
    delete copie.serie;
    delete copie.inventaire;
    copies.push(copie);
  }
  schema.equipements.push(...copies);
  const extremite = (e: Extremite): Extremite | null => {
    const equipement = correspondance.get(e.equipement);
    const iface = correspondance.get(`${e.equipement}/${e.interface}`);
    return equipement && iface ? { equipement, interface: iface } : null;
  };
  for (const lien of contenu.liens) {
    const a = extremite(lien.a);
    const b = extremite(lien.b);
    if (a && b) {
      schema.liens.push({
        ...cloner(lien),
        id: nouvelId("l"),
        a,
        b,
        points: lien.points.map((p) => ({ x: p.x + decalage.x, y: p.y + decalage.y })),
        ...(lien.palier
          ? {
              palier: {
                axe: lien.palier.axe,
                position: lien.palier.position + (lien.palier.axe === "y" ? decalage.y : decalage.x),
              },
            }
          : {}),
      });
    }
  }
  const decale = (p: Point): Point => ({ x: p.x + decalage.x, y: p.y + decalage.y });
  for (const r of contenu.relations ?? []) {
    const de = correspondance.get(r.de);
    const vers = correspondance.get(r.vers);
    if (!de || !vers) continue;
    const copie: Relation = { ...cloner(r), id: nouvelId("r"), de, vers, points: r.points.map(decale) };
    if (r.deInterface) copie.deInterface = correspondance.get(`${r.de}/${r.deInterface}`);
    if (r.versInterface) copie.versInterface = correspondance.get(`${r.vers}/${r.versInterface}`);
    if (!copie.deInterface) delete copie.deInterface;
    if (!copie.versInterface) delete copie.versInterface;
    schema.relations.push(copie);
  }
  for (const a of contenu.agregats ?? []) {
    const equipement = correspondance.get(a.equipement);
    if (!equipement) continue;
    const membres = a.membres
      .map((m) => correspondance.get(`${a.equipement}/${m}`))
      .filter((m): m is string => Boolean(m));
    if (membres.length > 0) schema.agregats.push({ ...cloner(a), id: nouvelId("a"), equipement, membres });
  }
  for (const n of contenu.annotations ?? []) {
    schema.annotations.push({
      ...cloner(n),
      id: nouvelId("n"),
      position: decale(n.position),
      ...(n.vers ? { vers: decale(n.vers) } : {}),
    });
  }
  return copies.map((c) => c.id);
}

/** Copie des équipements choisis (et des liens entre eux), décalée de `decalage`. */
export function dupliquerEquipements(
  schema: Schema,
  ids: Set<string>,
  decalage: Point = { x: 40, y: 40 },
): string[] {
  return collerEquipements(schema, copierEquipements(schema, ids), decalage);
}

/**
 * Rebranche un bout de câble (geste « câble en main ») : l'extrémité `bout`
 * du lien passe sur `vers`. Refusé si l'interface est prise ou si le câble
 * reviendrait sur sa propre autre extrémité.
 */
export function rebrancher(schema: Schema, lienId: string, bout: "a" | "b", vers: Extremite): void {
  const lien = schema.liens.find((l) => l.id === lienId);
  if (!lien) throw new Refus("Câble introuvable.");
  const autre = bout === "a" ? lien.b : lien.a;
  if (autre.equipement === vers.equipement && autre.interface === vers.interface) {
    throw new Refus("Une interface ne peut pas être reliée à elle-même.");
  }
  const raison = interfaceIndisponible(schema, vers, lien.id);
  if (raison) throw new Refus(`Interface « ${interfaceDe(schema, vers)?.nom ?? "?"} » : ${raison}.`);
  lien[bout] = { ...vers };
}

/** Alignement d'équipements (D45) : sur un bord ou au centre de la sélection. */
export type Alignement = "gauche" | "centre-h" | "droite" | "haut" | "milieu" | "bas";
export const ALIGNEMENTS: { valeur: Alignement; nom: string }[] = [
  { valeur: "gauche", nom: "Aligner à gauche" },
  { valeur: "centre-h", nom: "Centrer horizontalement" },
  { valeur: "droite", nom: "Aligner à droite" },
  { valeur: "haut", nom: "Aligner en haut" },
  { valeur: "milieu", nom: "Centrer verticalement" },
  { valeur: "bas", nom: "Aligner en bas" },
];

const deplacables = (schema: Schema, ids: Set<string>) =>
  schema.equipements.filter((e) => ids.has(e.id) && !e.verrou);

/** Aligne les équipements `ids` (les verrouillés ne bougent pas). */
export function aligner(schema: Schema, ids: Set<string>, mode: Alignement): void {
  const cibles = deplacables(schema, ids);
  if (cibles.length < 2) throw new Refus("Choisissez au moins deux équipements non verrouillés.");
  const axe = mode === "gauche" || mode === "centre-h" || mode === "droite" ? "x" : "y";
  const valeurs = cibles.map((e) => e.position[axe]);
  const min = Math.min(...valeurs);
  const max = Math.max(...valeurs);
  const valeur =
    mode === "gauche" || mode === "haut"
      ? min
      : mode === "droite" || mode === "bas"
        ? max
        : Math.round((min + max) / 2);
  for (const e of cibles) e.position = { ...e.position, [axe]: valeur };
}

/** Répartit les équipements `ids` à intervalles égaux entre les deux extrêmes. */
export function repartir(schema: Schema, ids: Set<string>, axe: "x" | "y"): void {
  const tries = deplacables(schema, ids).sort((a, b) => a.position[axe] - b.position[axe]);
  if (tries.length < 3) throw new Refus("Choisissez au moins trois équipements non verrouillés.");
  const premier = tries[0]?.position[axe] ?? 0;
  const pas = ((tries.at(-1)?.position[axe] ?? 0) - premier) / (tries.length - 1);
  tries.forEach((e, i) => {
    e.position = { ...e.position, [axe]: Math.round(premier + i * pas) };
  });
}

/** Renomme une interface : son identifiant, donc ses liens, ne changent pas. */
export function renommerInterface(schema: Schema, extremite: Extremite, nom: string): void {
  const iface = interfaceDe(schema, extremite);
  if (!iface) throw new Refus("Interface introuvable.");
  const propre = nom.trim();
  if (!propre) throw new Refus("Une interface doit avoir un nom.");
  iface.nom = propre;
  iface.nomAuto = false;
}

/** Ajoute une interface à la main (sous-interface, VLAN, virtuelle…). */
export function ajouterInterface(
  schema: Schema,
  equipementId: string,
  modele: Partial<Interface> = {},
): Interface {
  const equipement = equipementDe(schema, equipementId);
  if (!equipement) throw new Refus("Équipement introuvable.");
  const pris = new Set(equipement.interfaces.map((i) => i.nom));
  let n = 1;
  while (pris.has(`if${n}`)) n++;
  const iface: Interface = {
    id: nouvelId("i"),
    nom: `if${n}`,
    genre: "cuivre",
    nature: "physique",
    admin: "actif",
    macs: [],
    adresses: [],
    ...modele,
  };
  equipement.interfaces.push(iface);
  return iface;
}

/** Relation logique entre deux équipements (jamais un câble). */
export function relier(
  schema: Schema,
  relation: Omit<Relation, "id" | "points" | "proprietes" | "statut"> & Partial<Relation>,
): Relation {
  if (!equipementDe(schema, relation.de) || !equipementDe(schema, relation.vers)) {
    throw new Refus("Extrémité de relation introuvable.");
  }
  const nouvelle: Relation = { statut: "actif", proprietes: {}, points: [], ...relation, id: nouvelId("r") };
  schema.relations.push(nouvelle);
  return nouvelle;
}

/** Copie profonde d'un objet du modèle (un proxy de Svelte compris). */
function cloner<T>(valeur: T): T {
  return JSON.parse(JSON.stringify(valeur)) as T;
}
