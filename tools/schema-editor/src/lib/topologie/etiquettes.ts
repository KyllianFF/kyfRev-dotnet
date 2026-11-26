// Étiquettes placées et tournées à la main (rangée 17 des maquettes) : un clic choisit une étiquette, on la
// glisse où l'on veut, sa poignée la fait pivoter par crans de 15° (Alt maintenu : au degré près). Ce qui est
// choisi à la main s'enregistre avec le schéma ; « Remettre à sa place » l'oublie, et l'étiquette retrouve
// la place que le dessin lui calcule.

import { angleLisible, type CleEtiquette, type CleRenvoi, type Point, type PoseEtiquette, type Schema } from "./modele";

/**
 * Une étiquette du schéma : l'une de celles d'un câble, le nom d'une relation, le bloc du nom d'un équipement, ou
 * la pastille d'un câble vers une autre page (30A), à l'un de ses bouts : celle-ci se déplace, elle ne pivote pas.
 */
export type CibleEtiquette =
  | { genre: "lien"; id: string; cle: CleEtiquette }
  | { genre: "renvoi"; id: string; cle: CleRenvoi }
  | { genre: "relation"; id: string; cle: "nom" }
  | { genre: "equipement"; id: string };

/** Le pas de la poignée de rotation, en degrés. */
export const CRAN = 15;

export const memeCible = (a: CibleEtiquette | null, b: CibleEtiquette | null): boolean =>
  a !== null &&
  b !== null &&
  a.genre === b.genre &&
  a.id === b.id &&
  (a.genre === "equipement" || a.cle === (b as { cle?: CleEtiquette | CleRenvoi }).cle);

/** La pose d'une étiquette, si elle a été placée ou tournée à la main. */
export function poseDe(schema: Schema, cible: CibleEtiquette): PoseEtiquette | undefined {
  if (cible.genre === "equipement") return schema.equipements.find((e) => e.id === cible.id)?.pose;
  if (cible.genre === "relation") return schema.relations.find((r) => r.id === cible.id)?.poses?.nom;
  return schema.liens.find((l) => l.id === cible.id)?.poses?.[cible.cle];
}

/** Écrit la pose d'une étiquette ; une pose vide (ni place ni angle) est retirée, et avec elle ce qui la portait. */
export function ecrirePose(schema: Schema, cible: CibleEtiquette, pose: PoseEtiquette | undefined): void {
  const utile = pose && (pose.place || pose.angle) ? nettoyer(pose) : undefined;
  if (cible.genre === "equipement") {
    const e = schema.equipements.find((x) => x.id === cible.id);
    if (!e) return;
    if (utile) e.pose = utile;
    else delete e.pose;
    return;
  }
  const objet =
    cible.genre === "relation"
      ? schema.relations.find((r) => r.id === cible.id)
      : schema.liens.find((l) => l.id === cible.id);
  if (!objet) return;
  const poses: Record<string, PoseEtiquette> = { ...(objet.poses ?? {}) };
  if (utile) poses[cible.cle] = utile;
  else delete poses[cible.cle];
  if (Object.keys(poses).length > 0) objet.poses = poses;
  else delete objet.poses;
}

function nettoyer(pose: PoseEtiquette): PoseEtiquette {
  const angle = pose.angle ? angleLisible(pose.angle) : 0;
  return { ...(pose.place ? { place: { ...pose.place } } : {}), ...(angle ? { angle } : {}) };
}

/** L'étiquette posée en un autre endroit, son angle gardé. */
export function placer(schema: Schema, cible: CibleEtiquette, place: NonNullable<PoseEtiquette["place"]>): void {
  ecrirePose(schema, cible, { ...poseDe(schema, cible), place });
}

/** L'étiquette tournée de `angle` degrés, sa place gardée. */
export function tourner(schema: Schema, cible: CibleEtiquette, angle: number): void {
  const { angle: _ancien, ...reste } = poseDe(schema, cible) ?? {};
  ecrirePose(schema, cible, { ...reste, angle });
}

/** « Remettre à sa place » : position et angle oubliés. */
export const remettre = (schema: Schema, cible: CibleEtiquette): void => ecrirePose(schema, cible, undefined);

/** Toutes les étiquettes d'un câble ou d'une relation remises à leur place. */
export function remettreToutes(schema: Schema, id: string): void {
  const objet = schema.liens.find((l) => l.id === id) ?? schema.relations.find((r) => r.id === id);
  if (objet) delete objet.poses;
}

/**
 * L'angle que donne la poignée tirée jusqu'à `pointeur` autour du centre de l'étiquette : 0° quand elle est
 * juste au-dessus. Par crans de 15°, ou au degré près (`libre`, Alt maintenu) ; jamais à l'envers.
 */
export function angleDeLaPoignee(centre: Point, pointeur: Point, libre: boolean): number {
  const brut = (Math.atan2(pointeur.x - centre.x, centre.y - pointeur.y) * 180) / Math.PI;
  return angleLisible(libre ? Math.round(brut) : Math.round(brut / CRAN) * CRAN);
}

/** Un cran de plus (ou de moins) au clavier, depuis l'angle actuel ramené au cran le plus proche. */
export const cranSuivant = (angle: number, sens: 1 | -1): number =>
  angleLisible(Math.round((angle ?? 0) / CRAN) * CRAN + sens * CRAN);
