// Génération des interfaces d'un équipement d'après ses membres (stack,
// modules) et leurs profils de ports (D39). Chaque interface générée garde
// une clé d'origine (membre, groupe, numéro) : régénérer reprend la même
// interface — même identifiant, donc mêmes liens — et ne fait que signaler
// celles qui disparaissent, sans rien retirer d'office.

import type { Equipement, GroupePorts, Interface, Membre, ModeleMateriel, Schema } from "./modele";
import { liensDeLInterface, nouvelId } from "./modele";

/** Nombre maximal de ports d'un groupe, et de membres d'un stack. */
export const PORTS_MAX = 256;
export const MEMBRES_MAX = 16;

/** Nom affiché d'un port : `{m}` devient le membre, `{p}` le numéro. */
export const nomDePort = (motif: string, membre: number, numero: number): string =>
  motif.replaceAll("{m}", String(membre)).replaceAll("{p}", String(numero));

export const cleDOrigine = (membre: number, groupe: number, numero: number): string =>
  `${membre}:${groupe}:${numero}`;

/** Groupes de ports d'un membre : les siens, sinon ceux de son modèle matériel. */
export function groupesDuMembre(membre: Membre, modeles: ModeleMateriel[]): GroupePorts[] {
  if (membre.groupes) return membre.groupes;
  return modeles.find((m) => m.id === membre.modele)?.groupes ?? [];
}

type Attendue = Omit<Interface, "id" | "macs" | "adresses" | "admin">;

/** Interfaces que les membres et leurs profils font attendre, dans l'ordre. */
export function interfacesAttendues(equipement: Equipement, modeles: ModeleMateriel[]): Attendue[] {
  const empile = equipement.membres.length > 1;
  const attendues: Attendue[] = [];
  for (const membre of equipement.membres) {
    groupesDuMembre(membre, modeles).forEach((groupe, indice) => {
      const nombre = Math.max(0, Math.min(PORTS_MAX, Math.round(groupe.nombre)));
      for (let k = 0; k < nombre; k++) {
        const numero = groupe.premier + k;
        attendues.push({
          nom: nomDePort(groupe.motif, membre.numero, numero),
          ...(empile || groupe.motif.includes("{m}") ? { membre: membre.numero } : {}),
          numero,
          origine: cleDOrigine(membre.numero, indice, numero),
          nomAuto: true,
          genre: groupe.genre,
          nature: groupe.genre === "virtuel" ? "virtuelle" : "physique",
          ...(groupe.connecteur ? { connecteur: groupe.connecteur } : {}),
          ...(groupe.vitesse ? { vitesse: groupe.vitesse } : {}),
          ...(groupe.role ? { role: groupe.role } : {}),
          ...(groupe.poe ? { poe: { fournit: true } } : {}),
        });
      }
    });
  }
  return attendues;
}

export type Regeneration = {
  /** Les interfaces après régénération : générées dans l'ordre, puis celles ajoutées à la main. */
  interfaces: Interface[];
  /** Interfaces générées qui ne sont plus attendues (moins de ports, membre retiré). */
  retirees: Interface[];
};

/**
 * Régénère les interfaces d'un équipement. Une interface déjà là garde son
 * identifiant et ses réglages ; son nom suit le motif tant qu'il n'a pas été
 * changé à la main. Celles qui ne sont plus attendues sont rendues à part :
 * c'est à l'appelant de traiter leurs liens.
 */
export function regenerer(
  equipement: Equipement,
  modeles: ModeleMateriel[],
  multiConnexion = false,
): Regeneration {
  const parOrigine = new Map(
    equipement.interfaces.filter((i) => i.origine).map((i) => [i.origine as string, i]),
  );
  const attendues = interfacesAttendues(equipement, modeles);
  const vues = new Set<string>();
  const interfaces: Interface[] = attendues.map((attendue) => {
    const cle = attendue.origine as string;
    vues.add(cle);
    const existante = parOrigine.get(cle);
    if (existante) {
      return existante.nomAuto === false ? existante : { ...existante, nom: attendue.nom };
    }
    return {
      id: nouvelId("i"),
      ...attendue,
      admin: "actif",
      macs: [],
      adresses: [],
      ...(multiConnexion ? { multiConnexion: true } : {}),
    };
  });
  const manuelles = equipement.interfaces.filter((i) => !i.origine);
  const retirees = equipement.interfaces.filter((i) => i.origine && !vues.has(i.origine));
  return { interfaces: [...interfaces, ...manuelles], retirees };
}

/** Ce que retirer des interfaces toucherait : liens, relations, agrégats, gestion. */
export function effetsDuRetrait(schema: Schema, equipementId: string, retirees: Interface[]) {
  const ids = new Set(retirees.map((i) => i.id));
  const liens = retirees.flatMap((i) => liensDeLInterface(schema, equipementId, i.id));
  const relations = schema.relations.filter(
    (r) =>
      (r.de === equipementId && r.deInterface && ids.has(r.deInterface)) ||
      (r.vers === equipementId && r.versInterface && ids.has(r.versInterface)),
  );
  const agregats = schema.agregats.filter(
    (a) => a.equipement === equipementId && a.membres.some((m) => ids.has(m)),
  );
  return { liens: [...new Set(liens)], relations, agregats };
}

/** Membres 1 à `nombre`, chacun avec le profil du premier (ou son propre profil s'il existe déjà). */
export function membresAuNombre(equipement: Equipement, nombre: number): Membre[] {
  const voulu = Math.max(1, Math.min(MEMBRES_MAX, Math.round(nombre)));
  const premier = equipement.membres[0] ?? { numero: 1, groupes: [] };
  return Array.from({ length: voulu }, (_, k) => {
    const existant = equipement.membres.find((m) => m.numero === k + 1);
    if (existant) return existant;
    return {
      numero: k + 1,
      ...(premier.modele ? { modele: premier.modele } : {}),
      ...(premier.groupes ? { groupes: premier.groupes.map((g) => ({ ...g })) } : {}),
    };
  });
}

/** Interfaces regroupées par membre (stack), pour le sélecteur. */
export function interfacesParMembre(
  equipement: Equipement,
): { membre: number | null; interfaces: Interface[] }[] {
  const groupes = new Map<number | null, Interface[]>();
  for (const i of equipement.interfaces) {
    const cle = equipement.membres.length > 1 ? (i.membre ?? null) : null;
    const liste = groupes.get(cle) ?? [];
    liste.push(i);
    groupes.set(cle, liste);
  }
  return [...groupes.entries()].map(([membre, interfaces]) => ({ membre, interfaces }));
}
