// Les pages d'un schéma (30A) : comme dans draw.io, un schéma peut avoir plusieurs plans (Étage 1, Étage 2,
// Salle serveurs…), en onglets sous le plan. Chaque équipement, cadre et annotation est sur une page ; un câble
// suit ses équipements, et un câble dont les deux bouts sont sur deux pages se dessine sur chacune en renvoi
// (« → Étage 2 · SW-Étage2 · 1/48 ») : c'est la bonne pratique des plans en plusieurs feuilles, chaque bout
// garde son port.
//
// Un schéma sans pages en a une, implicite, d'identifiant vide : il ne change pas d'un octet tant qu'on n'en
// ajoute pas une deuxième. Les vues (Physique, Logique…) valent pour la page montrée ; le plan IP et le
// diagnostic regardent tout le schéma.

import {
  equipementDe,
  FORMAT,
  FORMAT_PAGES,
  type Groupe,
  interfaceDe,
  type Lien,
  nouvelId,
  type Page,
  type Point,
  type Schema,
} from "./modele";
import { collerEquipements, copierEquipements, Refus, retirerEquipements } from "./operations";

/** Le nom de la page d'un schéma qui n'en déclare pas. */
export const NOM_PREMIERE_PAGE = "Page 1";
/** Un nom de page, au plus. */
export const NOM_PAGE_MAX = 80;

type SurUnePage = { page?: string };

/** Le schéma a-t-il plusieurs pages (des onglets à montrer, un renvoi possible) ? */
export const aPlusieursPages = (schema: Pick<Schema, "pages">): boolean => (schema.pages?.length ?? 0) >= 2;

/** Les pages, dans l'ordre des onglets ; un schéma qui n'en déclare pas en a une, d'identifiant vide. */
export function pagesDe(schema: Pick<Schema, "pages">): Page[] {
  return schema.pages && schema.pages.length > 0 ? schema.pages : [{ id: "", nom: NOM_PREMIERE_PAGE }];
}

const premiere = (schema: Pick<Schema, "pages">): string => pagesDe(schema)[0]?.id ?? "";

/** `page` si le schéma l'a, sinon sa première page. */
export function pageValide(schema: Pick<Schema, "pages">, page: string | undefined): string {
  return pagesDe(schema).some((p) => p.id === page) ? (page ?? "") : premiere(schema);
}

/** La page d'un équipement, d'un cadre ou d'une annotation. */
export function pageDeLElement(schema: Pick<Schema, "pages">, element: SurUnePage): string {
  return aPlusieursPages(schema) ? pageValide(schema, element.page) : premiere(schema);
}

/** La page d'un objet du schéma (équipement, cadre, annotation, câble ou relation) ; rien s'il n'existe pas. */
export function pageDeLObjet(schema: Schema, id: string): string | undefined {
  const element =
    schema.equipements.find((e) => e.id === id) ??
    schema.groupes.find((g) => g.id === id) ??
    schema.annotations.find((n) => n.id === id);
  if (element) return pageDeLElement(schema, element);
  const lien = schema.liens.find((l) => l.id === id);
  const relation = schema.relations.find((r) => r.id === id);
  const bout = lien?.a.equipement ?? relation?.de;
  const equipement = bout ? equipementDe(schema, bout) : undefined;
  return equipement ? pageDeLElement(schema, equipement) : undefined;
}

/** Le nom d'une page. */
export const nomDeLaPage = (schema: Pick<Schema, "pages">, id: string): string =>
  pagesDe(schema).find((p) => p.id === id)?.nom ?? NOM_PREMIERE_PAGE;

/** Le nombre d'équipements de chaque page (pour les onglets). */
export function equipementsParPage(schema: Schema): Map<string, number> {
  const compte = new Map(pagesDe(schema).map((p) => [p.id, 0]));
  for (const e of schema.equipements) {
    const page = pageDeLElement(schema, e);
    compte.set(page, (compte.get(page) ?? 0) + 1);
  }
  return compte;
}

/**
 * Le schéma vu d'une page : ses équipements, cadres et annotations, les câbles et relations entre eux, leurs
 * agrégats. Les objets sont ceux du schéma (pas des copies) : les déplacer déplace ceux du schéma. Un schéma
 * d'une seule page est rendu tel quel.
 */
export function surLaPage(schema: Schema, page?: string): Schema {
  if (!aPlusieursPages(schema)) return schema;
  const ici = pageValide(schema, page);
  const surCettePage = (element: SurUnePage) => pageDeLElement(schema, element) === ici;
  const equipements = schema.equipements.filter(surCettePage);
  const ids = new Set(equipements.map((e) => e.id));
  const groupes = schema.groupes.filter(surCettePage);
  const annotations = schema.annotations.filter(surCettePage);
  const presents = new Set([...ids, ...groupes.map((g) => g.id), ...annotations.map((n) => n.id)]);
  const ensembles = schema.ensembles?.filter((e) => e.membres.some((m) => presents.has(m)));
  return {
    ...schema,
    equipements,
    liens: schema.liens.filter((l) => ids.has(l.a.equipement) && ids.has(l.b.equipement)),
    relations: schema.relations.filter((r) => ids.has(r.de) && ids.has(r.vers)),
    agregats: schema.agregats.filter((a) => ids.has(a.equipement)),
    groupes,
    annotations,
    ...(ensembles ? { ensembles } : {}),
  };
}

// --- Renvois entre pages ---------------------------------------------------------------------------------

/** Un câble dont l'autre bout est sur une autre page, tel qu'il se dessine sur celle-ci. */
export type Renvoi = {
  lien: Lien;
  /** L'équipement de cette page. */
  equipement: string;
  /** La page, l'équipement et le port d'en face. */
  page: Page;
  versEquipement: string;
  nomEquipement: string;
  port: string;
  /** « → Étage 2 · SW-Étage2 · 1/48 ». */
  texte: string;
  /** D'où part le trait (le centre de l'équipement) et où est la pastille (son centre, sa taille). */
  depart: Point;
  centre: Point;
  largeur: number;
  hauteur: number;
};

const DEMI_COTE = 28;
const TAILLE_RENVOI = 10.5;
const HAUTEUR_RENVOI = 20;
const ECART_RENVOIS = 26;

/** La largeur d'une pastille de renvoi, d'après son texte. */
export const largeurDuRenvoi = (texte: string): number => Math.round(texte.length * TAILLE_RENVOI * 0.58 + 22);

/**
 * Les renvois d'une page : un par câble qui la quitte. La pastille se pose à côté de l'équipement, du côté où il
 * est sur la page (vers le bord le plus proche), un peu au-dessus de son centre : assez bas pour ne pas passer
 * sous la barre d'actions d'un équipement choisi. Plusieurs renvois d'un même équipement s'empilent vers le bas.
 */
export function renvoisDeLaPage(schema: Schema, page?: string, caches: Set<string> = new Set()): Renvoi[] {
  if (!aPlusieursPages(schema)) return [];
  const ici = pageValide(schema, page);
  const parId = new Map(schema.equipements.map((e) => [e.id, e]));
  const surCettePage = schema.equipements.filter((e) => pageDeLElement(schema, e) === ici);
  if (surCettePage.length === 0) return [];
  const milieuX =
    surCettePage.reduce((somme, e) => somme + e.position.x, 0) / surCettePage.length;
  const rangs = new Map<string, number>();
  const renvois: Renvoi[] = [];
  for (const lien of schema.liens) {
    for (const [bout, autre] of [
      [lien.a, lien.b],
      [lien.b, lien.a],
    ] as const) {
      const local = parId.get(bout.equipement);
      const distant = parId.get(autre.equipement);
      if (!local || !distant || caches.has(local.id)) continue;
      const pageLocale = pageDeLElement(schema, local);
      const pageDistante = pageDeLElement(schema, distant);
      if (pageLocale !== ici || pageDistante === ici) continue;
      const port = interfaceDe(schema, autre)?.nom ?? "";
      const nomPage = nomDeLaPage(schema, pageDistante);
      const texte = `→ ${[nomPage, distant.nom, port].filter(Boolean).join(" · ")}`;
      const largeur = largeurDuRenvoi(texte);
      const rang = rangs.get(local.id) ?? 0;
      rangs.set(local.id, rang + 1);
      const cote = surCettePage.length > 1 && local.position.x < milieuX ? -1 : 1;
      renvois.push({
        lien,
        equipement: local.id,
        page: { id: pageDistante, nom: nomPage },
        versEquipement: distant.id,
        nomEquipement: distant.nom,
        port,
        texte,
        depart: { ...local.position },
        centre: {
          x: local.position.x + cote * (DEMI_COTE + 44 + largeur / 2),
          y: local.position.y - 20 + rang * ECART_RENVOIS,
        },
        largeur,
        hauteur: HAUTEUR_RENVOI,
      });
    }
  }
  return renvois;
}

// --- Les pages elles-mêmes : ajouter, renommer, dupliquer, déplacer, supprimer ----------------------------

/** « Page 3 » : le premier numéro libre. */
export function nomDePageLibre(schema: Pick<Schema, "pages">): string {
  const pris = new Set(pagesDe(schema).map((p) => p.nom));
  let n = pagesDe(schema).length + 1;
  while (pris.has(`Page ${n}`)) n++;
  return `Page ${n}`;
}

/** Un nom de page saisi : sans espaces autour, borné ; vide, il est refusé. */
function nomPropre(nom: string): string {
  const propre = nom.replace(/\s+/g, " ").trim().slice(0, NOM_PAGE_MAX);
  if (!propre) throw new Refus("Une page doit avoir un nom.");
  return propre;
}

/** Le schéma déclare ses pages (au moins la première) ; avec deux pages ou plus, chaque élément porte la sienne. */
function declarerPages(schema: Schema): Page[] {
  if (!schema.pages || schema.pages.length === 0) schema.pages = [{ id: nouvelId("p"), nom: NOM_PREMIERE_PAGE }];
  return schema.pages;
}

/** Passe le schéma en plusieurs pages : chaque élément sans page valable rejoint la première (format 3). */
function rangerSurLesPages(schema: Schema): void {
  const pages = schema.pages ?? [];
  if (pages.length < 2) return;
  const connues = new Set(pages.map((p) => p.id));
  const premiereId = pages[0]?.id ?? "";
  for (const element of [...schema.equipements, ...schema.groupes, ...schema.annotations]) {
    if (!element.page || !connues.has(element.page)) element.page = premiereId;
  }
  schema.format = FORMAT_PAGES;
}

/** Ajoute une page à la fin (ou après `apres`) ; rend la page créée. */
export function ajouterPage(schema: Schema, nom?: string, apres?: string): Page {
  const pages = declarerPages(schema);
  const page: Page = { id: nouvelId("p"), nom: nom === undefined ? nomDePageLibre(schema) : nomPropre(nom) };
  const rang = apres === undefined ? -1 : pages.findIndex((p) => p.id === apres);
  if (rang < 0) pages.push(page);
  else pages.splice(rang + 1, 0, page);
  rangerSurLesPages(schema);
  return page;
}

/** Renomme une page (la page implicite d'un schéma qui n'en déclare pas devient déclarée). */
export function renommerPage(schema: Schema, id: string, nom: string): void {
  const propre = nomPropre(nom);
  const page = id === "" && !schema.pages?.length ? declarerPages(schema)[0] : schema.pages?.find((p) => p.id === id);
  if (!page) throw new Refus("Cette page n'existe plus.");
  page.nom = propre;
}

/** Déplace une page au rang `rang` (0 : la première). */
export function deplacerPage(schema: Schema, id: string, rang: number): void {
  const pages = schema.pages ?? [];
  const depart = pages.findIndex((p) => p.id === id);
  if (depart < 0) throw new Refus("Cette page n'existe plus.");
  const arrivee = Math.max(0, Math.min(pages.length - 1, Math.round(rang)));
  if (arrivee === depart) return;
  const [page] = pages.splice(depart, 1);
  if (page) pages.splice(arrivee, 0, page);
}

/**
 * Duplique une page juste après elle : ses équipements (noms suivis de « copie », sans adresses ni numéros
 * de série, comme Ctrl+D), les câbles et relations entre eux, ses cadres, ses annotations et ses ensembles
 * groupés. Un câble vers une autre page ne se copie pas : son port, à l'autre bout, est déjà pris.
 */
export function dupliquerPage(schema: Schema, id: string): Page {
  const pages = declarerPages(schema);
  const source = pages.find((p) => p.id === id) ?? (id === "" ? pages[0] : undefined);
  if (!source) throw new Refus("Cette page n'existe plus.");
  const vue = surLaPage(schema, source.id);
  const equipements = aPlusieursPages(schema) ? vue.equipements : schema.equipements;
  const annotations = aPlusieursPages(schema) ? vue.annotations : schema.annotations;
  const groupes = aPlusieursPages(schema) ? vue.groupes : schema.groupes;
  const copie = ajouterPage(schema, `${source.nom} (copie)`.slice(0, NOM_PAGE_MAX), source.id);

  const correspondance = new Map<string, string>();
  const avant = schema.annotations.length;
  const contenu = copierEquipements(schema, new Set([...equipements, ...annotations].map((x) => x.id)));
  const colles = collerEquipements(schema, contenu, { x: 0, y: 0 });
  contenu.equipements.forEach((e, rang) => {
    const nouveau = colles[rang];
    if (nouveau) correspondance.set(e.id, nouveau);
  });
  (contenu.annotations ?? []).forEach((n, rang) => {
    const nouvelle = schema.annotations[avant + rang];
    if (nouvelle) correspondance.set(n.id, nouvelle.id);
  });
  for (const e of schema.equipements) if (colles.includes(e.id)) e.page = copie.id;
  for (const n of schema.annotations.slice(avant)) n.page = copie.id;

  for (const g of groupes) correspondance.set(g.id, nouvelId("g"));
  for (const g of groupes) {
    const cadre: Groupe = {
      ...(JSON.parse(JSON.stringify(g)) as Groupe),
      id: correspondance.get(g.id) ?? nouvelId("g"),
      page: copie.id,
      membres: g.membres.map((m) => correspondance.get(m)).filter((m): m is string => Boolean(m)),
    };
    const parent = g.parent ? correspondance.get(g.parent) : undefined;
    if (parent) cadre.parent = parent;
    else delete cadre.parent;
    delete cadre.verrou;
    schema.groupes.push(cadre);
  }
  const ensembles = (schema.ensembles ?? [])
    .filter((e) => e.membres.every((m) => correspondance.has(m)))
    .map((e) => ({ id: nouvelId("e"), membres: e.membres.map((m) => correspondance.get(m) ?? m) }));
  if (ensembles.length > 0) schema.ensembles = [...(schema.ensembles ?? []), ...ensembles];
  return copie;
}

/**
 * Supprime une page et tout ce qui y est (câbles vers les autres pages compris). Un schéma garde au moins une
 * page ; revenu à une seule, il repasse au format 2 et ne garde que le nom de sa page.
 */
export function supprimerPage(schema: Schema, id: string): void {
  const pages = schema.pages ?? [];
  if (pages.length < 2) throw new Refus("Un schéma garde au moins une page.");
  if (!pages.some((p) => p.id === id)) throw new Refus("Cette page n'existe plus.");
  const ici = (element: SurUnePage) => pageDeLElement(schema, element) === id;
  const equipements = new Set(schema.equipements.filter(ici).map((e) => e.id));
  const autres = new Set([...schema.groupes.filter(ici).map((g) => g.id), ...schema.annotations.filter(ici).map((n) => n.id)]);
  retirerEquipements(schema, equipements);
  schema.groupes = schema.groupes.filter((g) => !autres.has(g.id));
  for (const g of schema.groupes) if (g.parent && autres.has(g.parent)) delete g.parent;
  schema.annotations = schema.annotations.filter((n) => !autres.has(n.id));
  const ensembles = (schema.ensembles ?? [])
    .map((e) => ({ ...e, membres: e.membres.filter((m) => !equipements.has(m) && !autres.has(m)) }))
    .filter((e) => e.membres.length >= 2);
  if (ensembles.length > 0) schema.ensembles = ensembles;
  else delete schema.ensembles;
  schema.pages = pages.filter((p) => p.id !== id);
  if (schema.pages.length < 2) {
    for (const element of [...schema.equipements, ...schema.groupes, ...schema.annotations]) delete element.page;
    schema.format = FORMAT;
    // La page qui reste n'a que le nom par défaut : le schéma redevient celui d'avant les pages.
    if (schema.pages[0]?.nom === NOM_PREMIERE_PAGE) delete schema.pages;
  }
}

/**
 * Après une opération : ce qui vient d'apparaître (`connus` : les identifiants d'avant) sans page valable va
 * sur la page montrée. Ainsi un équipement posé, collé ou importé d'un modèle arrive où l'on regarde.
 */
export function rattacherALaPage(schema: Schema, page: string, connus: Set<string>): void {
  if (!aPlusieursPages(schema)) return;
  const ici = pageValide(schema, page);
  const connues = new Set(pagesDe(schema).map((p) => p.id));
  for (const element of [...schema.equipements, ...schema.groupes, ...schema.annotations]) {
    if (!connus.has(element.id) && (!element.page || !connues.has(element.page))) element.page = ici;
  }
}

/** Les identifiants des éléments posés sur une page (équipements, cadres, annotations). */
export const elementsDuSchema = (schema: Schema): Set<string> =>
  new Set([...schema.equipements, ...schema.groupes, ...schema.annotations].map((x) => x.id));
