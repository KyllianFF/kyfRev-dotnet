// Lecture et écriture des schémas (D39). Tout ce qui est lu (fichier du
// coffre, import) passe par ici : champs inconnus écartés, textes bornés et
// sans caractères de contrôle, nombres bornés, énumérations vérifiées. Les
// erreurs structurelles (identifiant en double, extrémité absente, deux
// câbles sur une interface point à point…) sont rendues à part : un import
// qui en contient est refusé tel quel, rien n'est enregistré.

import { migrerFormat1 } from "./migration";
import {
  type Adresse,
  AFFICHAGES,
  type Agregat,
  type Annotation,
  type ConfigVlan,
  type Equipement,
  type Extremite,
  FORMAT,
  FORMES_ANNOTATION,
  GENRES_GROUPE,
  GENRES_INTERFACE,
  GENRES_RELATION,
  type Groupe,
  type GroupePorts,
  type IdVue,
  type Interface,
  type Lien,
  type Membre,
  type ModeleMateriel,
  NATURES_INTERFACE,
  type Point,
  type Relation,
  ROLES_INTERFACE,
  type Schema,
  STATUTS,
  STATUTS_LIEN,
  SUPPORTS,
  type Valeur,
  type Vlan,
} from "./modele";
import { MEMBRES_MAX, PORTS_MAX } from "./ports";

const TEXTE_MAX = 300;
const LONG_MAX = 4000;
const COORDONNEE_MAX = 100_000;
const ELEMENTS_MAX = 20_000;

type Brut = Record<string, unknown>;

const objets = (valeur: unknown): Brut[] =>
  Array.isArray(valeur)
    ? valeur
        .slice(0, ELEMENTS_MAX)
        .filter((v): v is Brut => typeof v === "object" && v !== null && !Array.isArray(v))
    : [];

/** Texte borné, sans caractères de contrôle (sauf saut de ligne et tabulation). */
function texte(valeur: unknown, maximum = TEXTE_MAX): string {
  if (typeof valeur !== "string") return "";
  // biome-ignore lint/suspicious/noControlCharactersInRegex: on retire justement les caractères de contrôle.
  return valeur.replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f]/g, "").slice(0, maximum);
}
const facultatif = (valeur: unknown, maximum = TEXTE_MAX): string | undefined =>
  texte(valeur, maximum) || undefined;
function nombre(valeur: unknown, min: number, max: number): number | undefined {
  if (typeof valeur !== "number" || !Number.isFinite(valeur)) return undefined;
  return Math.max(min, Math.min(max, valeur));
}
const entier = (valeur: unknown, min: number, max: number): number | undefined => {
  const n = nombre(valeur, min, max);
  return n === undefined ? undefined : Math.round(n);
};
const parmi = <T extends string>(valeur: unknown, permis: { valeur: T }[]): T | undefined =>
  permis.find((p) => p.valeur === valeur)?.valeur;
const booleen = (valeur: unknown): true | undefined => (valeur === true ? true : undefined);
const point = (valeur: unknown): Point => {
  const brut = (typeof valeur === "object" && valeur !== null ? valeur : {}) as Brut;
  return {
    x: Math.round(nombre(brut.x, -COORDONNEE_MAX, COORDONNEE_MAX) ?? 0),
    y: Math.round(nombre(brut.y, -COORDONNEE_MAX, COORDONNEE_MAX) ?? 0),
  };
};
const points = (valeur: unknown): Point[] => (Array.isArray(valeur) ? valeur.slice(0, 64).map(point) : []);
const textes = (valeur: unknown, maximum = 64): string[] =>
  Array.isArray(valeur)
    ? valeur
        .slice(0, maximum)
        .map((v) => texte(v))
        .filter(Boolean)
    : [];
const identifiant = (valeur: unknown): string => texte(valeur, 64).replace(/[^\w-]/g, "");

/** Supprime les clés à `undefined` (le JSON écrit reste court et stable). */
function propre<T extends object>(objet: T): T {
  for (const cle of Object.keys(objet) as (keyof T)[]) {
    if (objet[cle] === undefined) delete objet[cle];
  }
  return objet;
}

function proprietes(valeur: unknown): Record<string, Valeur> {
  const resultat: Record<string, Valeur> = {};
  if (typeof valeur !== "object" || valeur === null) return resultat;
  for (const [cle, v] of Object.entries(valeur).slice(0, 100)) {
    const nom = identifiant(cle);
    if (!nom) continue;
    if (typeof v === "string") resultat[nom] = texte(v, LONG_MAX);
    else if (typeof v === "number" && Number.isFinite(v)) resultat[nom] = v;
    else if (typeof v === "boolean") resultat[nom] = v;
  }
  return resultat;
}

const lireGroupePorts = (g: Brut): GroupePorts =>
  propre({
    nom: texte(g.nom) || "Ports",
    motif: texte(g.motif, 60) || "{p}",
    nombre: entier(g.nombre, 0, PORTS_MAX) ?? 0,
    premier: entier(g.premier, 0, 100_000) ?? 1,
    genre: parmi(g.genre, GENRES_INTERFACE) ?? "cuivre",
    connecteur: facultatif(g.connecteur, 40),
    vitesse: entier(g.vitesse, 1, 10_000_000),
    role: parmi(g.role, ROLES_INTERFACE),
    poe: booleen(g.poe),
  });

function lireConfigVlan(valeur: unknown): ConfigVlan | undefined {
  if (typeof valeur !== "object" || valeur === null) return undefined;
  const v = valeur as Brut;
  const mode: ConfigVlan["mode"] = v.mode === "trunk" || v.mode === "hybride" ? v.mode : "acces";
  return propre({
    mode,
    acces: entier(v.acces, 1, 4094),
    natif: entier(v.natif, 1, 4094),
    tagues: facultatif(v.tagues, 1000),
    voix: entier(v.voix, 1, 4094),
    pvid: entier(v.pvid, 1, 4094),
  });
}

function lireInterface(i: Brut): Interface {
  const poe = typeof i.poe === "object" && i.poe !== null ? (i.poe as Brut) : undefined;
  return propre({
    id: identifiant(i.id),
    nom: texte(i.nom, 80) || "?",
    membre: entier(i.membre, 0, MEMBRES_MAX),
    module: facultatif(i.module, 40),
    numero: entier(i.numero, 0, 100_000),
    origine: facultatif(i.origine, 40),
    nomAuto: i.nomAuto === true ? true : i.nomAuto === false ? false : undefined,
    genre: parmi(i.genre, GENRES_INTERFACE) ?? "cuivre",
    nature: parmi(i.nature, NATURES_INTERFACE) ?? "physique",
    connecteur: facultatif(i.connecteur, 40),
    vitesse: entier(i.vitesse, 1, 10_000_000),
    role: parmi(i.role, ROLES_INTERFACE),
    parent: identifiant(i.parent) || undefined,
    admin: i.admin === "desactive" ? "desactive" : "actif",
    oper: i.oper === "up" || i.oper === "down" || i.oper === "inconnu" ? i.oper : undefined,
    description: facultatif(i.description, LONG_MAX),
    macs: textes(i.macs, 16).map((m) => m.slice(0, 40)),
    adresses: objets(i.adresses)
      .slice(0, 32)
      .map((a): Adresse => propre({ adresse: texte(a.adresse, 64), prefixe: entier(a.prefixe, 0, 128) }))
      .filter((a) => a.adresse),
    vlan: lireConfigVlan(i.vlan),
    poe: poe
      ? propre({
          fournit: booleen(poe.fournit),
          recoit: booleen(poe.recoit),
          norme: facultatif(poe.norme, 40),
          puissanceW: nombre(poe.puissanceW, 0, 10_000),
        })
      : undefined,
    multiConnexion: booleen(i.multiConnexion),
  });
}

function lireEquipement(e: Brut): Equipement {
  const gestion = typeof e.gestion === "object" && e.gestion !== null ? (e.gestion as Brut) : undefined;
  const membres = objets(e.membres)
    .slice(0, MEMBRES_MAX)
    .map(
      (m): Membre =>
        propre({
          numero: entier(m.numero, 1, MEMBRES_MAX) ?? 1,
          nom: facultatif(m.nom),
          modele: identifiant(m.modele) || undefined,
          groupes: Array.isArray(m.groupes) ? objets(m.groupes).slice(0, 32).map(lireGroupePorts) : undefined,
          serie: facultatif(m.serie, 80),
        }),
    );
  return propre({
    id: identifiant(e.id),
    type: identifiant(e.type) || "generique",
    nom: texte(e.nom, 120) || "Équipement",
    nomHote: facultatif(e.nomHote, 120),
    fabricant: facultatif(e.fabricant),
    reference: facultatif(e.reference),
    description: facultatif(e.description, LONG_MAX),
    role: facultatif(e.role),
    inventaire: facultatif(e.inventaire, 80),
    serie: facultatif(e.serie, 80),
    site: facultatif(e.site),
    salle: facultatif(e.salle),
    baie: facultatif(e.baie),
    positionRack: facultatif(e.positionRack, 40),
    statut: parmi(e.statut, STATUTS) ?? "actif",
    systeme: facultatif(e.systeme),
    contact: facultatif(e.contact),
    tags: textes(e.tags, 32).map((t) => t.slice(0, 40)),
    notes: facultatif(e.notes, LONG_MAX),
    documentation: facultatif(e.documentation, 1000),
    garantie: facultatif(e.garantie),
    referenceSecret: facultatif(e.referenceSecret),
    gestion: gestion
      ? propre({
          interface: identifiant(gestion.interface) || undefined,
          adresse: facultatif(gestion.adresse, 64),
          vlan: entier(gestion.vlan, 1, 4094),
        })
      : undefined,
    passerelle: facultatif(e.passerelle, 64),
    dns: facultatif(e.dns),
    vrf: facultatif(e.vrf, 80),
    poeBudgetW: nombre(e.poeBudgetW, 0, 100_000),
    proprietes: proprietes(e.proprietes),
    membres: membres.length > 0 ? membres : [{ numero: 1, groupes: [] }],
    interfaces: objets(e.interfaces)
      .slice(0, PORTS_MAX * MEMBRES_MAX)
      .map(lireInterface),
    position: point(e.position),
    verrou: booleen(e.verrou),
    replie: booleen(e.replie),
  });
}

const lireExtremite = (valeur: unknown): Extremite => {
  const brut = (typeof valeur === "object" && valeur !== null ? valeur : {}) as Brut;
  return { equipement: identifiant(brut.equipement), interface: identifiant(brut.interface) };
};

function lirePalier(valeur: unknown): Lien["palier"] {
  if (typeof valeur !== "object" || valeur === null) return undefined;
  const p = valeur as Brut;
  const position = nombre(p.position, -1_000_000, 1_000_000);
  return (p.axe === "x" || p.axe === "y") && position !== undefined ? { axe: p.axe, position } : undefined;
}

function lireLien(l: Brut): Lien {
  const optique = typeof l.optique === "object" && l.optique !== null ? (l.optique as Brut) : undefined;
  const mesure = typeof l.mesure === "object" && l.mesure !== null ? (l.mesure as Brut) : undefined;
  return propre({
    id: identifiant(l.id),
    a: lireExtremite(l.a),
    b: lireExtremite(l.b),
    support: parmi(l.support, SUPPORTS) ?? "cuivre",
    supportPerso: facultatif(l.supportPerso, 80),
    categorie: facultatif(l.categorie, 40),
    technologie: facultatif(l.technologie, 80),
    debit: entier(l.debit, 1, 10_000_000),
    longueurM: nombre(l.longueurM, 0, 1_000_000),
    etiquette: facultatif(l.etiquette, 80),
    nom: facultatif(l.nom),
    description: facultatif(l.description, LONG_MAX),
    statut: parmi(l.statut, STATUTS_LIEN) ?? "actif",
    optique: optique
      ? propre({
          connecteurA: facultatif(optique.connecteurA, 40),
          connecteurB: facultatif(optique.connecteurB, 40),
          moduleA: facultatif(optique.moduleA, 80),
          moduleB: facultatif(optique.moduleB, 80),
        })
      : undefined,
    mesure: mesure
      ? propre({
          debit: entier(mesure.debit, 0, 10_000_000),
          date: facultatif(mesure.date, 40),
          source: facultatif(mesure.source),
        })
      : undefined,
    couleur: /^#[0-9a-f]{6}$/i.test(texte(l.couleur, 7)) ? texte(l.couleur, 7) : undefined,
    trait: l.trait === "tirets" || l.trait === "points" ? l.trait : undefined,
    libelles:
      l.libelles === "aucun" || l.libelles === "nom" || l.libelles === "tout" ? l.libelles : undefined,
    forme: l.forme === "droite" || l.forme === "coudee" ? l.forme : undefined,
    points: points(l.points),
    palier: lirePalier(l.palier),
    notes: facultatif(l.notes, LONG_MAX),
  });
}

const lireRelation = (r: Brut): Relation =>
  propre({
    id: identifiant(r.id),
    genre: parmi(r.genre, GENRES_RELATION) ?? "dependance",
    de: identifiant(r.de),
    vers: identifiant(r.vers),
    deInterface: identifiant(r.deInterface) || undefined,
    versInterface: identifiant(r.versInterface) || undefined,
    nom: facultatif(r.nom),
    description: facultatif(r.description, LONG_MAX),
    statut: parmi(r.statut, STATUTS_LIEN) ?? "actif",
    proprietes: proprietes(r.proprietes),
    points: points(r.points),
  });

const lireVlan = (v: Brut): Vlan =>
  propre({
    id: identifiant(v.id),
    numero: entier(v.numero, 1, 4094) ?? 1,
    nom: texte(v.nom, 80),
    description: facultatif(v.description, LONG_MAX),
    couleur: /^#[0-9a-f]{6}$/i.test(texte(v.couleur, 7)) ? texte(v.couleur, 7) : undefined,
    sousReseaux: textes(v.sousReseaux, 32).map((s) => s.slice(0, 64)),
    perimetre: facultatif(v.perimetre),
  });

const lireAgregat = (a: Brut): Agregat =>
  propre({
    id: identifiant(a.id),
    equipement: identifiant(a.equipement),
    nom: texte(a.nom, 80) || "Agrégat",
    mode: a.mode === "lacp" ? "lacp" : "statique",
    lacp: a.lacp === "actif" || a.lacp === "passif" ? a.lacp : undefined,
    membres: textes(a.membres, 64).map(identifiant),
    vlan: lireConfigVlan(a.vlan),
    condense: booleen(a.condense),
  });

const lireGroupe = (g: Brut): Groupe => {
  const taille = (typeof g.taille === "object" && g.taille !== null ? g.taille : {}) as Brut;
  return propre({
    id: identifiant(g.id),
    nom: texte(g.nom, 120),
    genre: parmi(g.genre, GENRES_GROUPE) ?? "groupe",
    parent: identifiant(g.parent) || undefined,
    membres: textes(g.membres, ELEMENTS_MAX).map(identifiant),
    position: point(g.position),
    taille: {
      largeur: Math.round(nombre(taille.largeur, 40, COORDONNEE_MAX) ?? 240),
      hauteur: Math.round(nombre(taille.hauteur, 40, COORDONNEE_MAX) ?? 160),
    },
    couleur: /^#[0-9a-f]{6}$/i.test(texte(g.couleur, 7)) ? texte(g.couleur, 7) : undefined,
    replie: booleen(g.replie),
    verrou: booleen(g.verrou),
  });
};

/** Options d'affichage : seules celles qui ont été réglées (vrai ou faux). */
function lireAffichage(valeur: unknown): Schema["affichage"] {
  if (typeof valeur !== "object" || valeur === null) return undefined;
  const brut = valeur as Brut;
  const resultat: NonNullable<Schema["affichage"]> = {};
  for (const { valeur: cle } of AFFICHAGES) {
    if (typeof brut[cle] === "boolean") resultat[cle] = brut[cle] as boolean;
  }
  return Object.keys(resultat).length ? resultat : undefined;
}

const lireAnnotation = (n: Brut): Annotation => {
  const forme = parmi(n.forme, FORMES_ANNOTATION);
  return propre({
    id: identifiant(n.id),
    texte: texte(n.texte, LONG_MAX),
    position: point(n.position),
    largeur: entier(n.largeur, forme === "texte" ? 20 : 40, COORDONNEE_MAX),
    forme: forme === "note" ? undefined : forme,
    hauteur: entier(n.hauteur, 20, COORDONNEE_MAX),
    couleur: /^#[0-9a-f]{6}$/i.test(texte(n.couleur, 7)) ? texte(n.couleur, 7) : undefined,
    vers: forme === "fleche" ? point(n.vers) : undefined,
    taille: entier(n.taille, 10, 72),
  });
};

const lireModele = (m: Brut): ModeleMateriel =>
  propre({
    id: identifiant(m.id),
    type: identifiant(m.type) || "generique",
    fabricant: facultatif(m.fabricant),
    reference: texte(m.reference) || "Modèle",
    description: facultatif(m.description, LONG_MAX),
    groupes: objets(m.groupes).slice(0, 32).map(lireGroupePorts),
  });

const VUES: IdVue[] = ["physique", "logique", "virtualisation", "stockage", "simplifiee"];

/** Erreurs structurelles : l'enregistrement (ou l'import) est refusé. */
export function erreursStructurelles(schema: Schema): string[] {
  const erreurs: string[] = [];
  const ids = new Set<string>();
  const unique = (id: string, quoi: string) => {
    if (!id) erreurs.push(`${quoi} sans identifiant.`);
    else if (ids.has(id)) erreurs.push(`Identifiant « ${id} » en double (${quoi}).`);
    else ids.add(id);
  };
  for (const m of schema.modeles) unique(m.id, `modèle « ${m.reference} »`);
  const interfaces = new Map<string, Interface>();
  for (const e of schema.equipements) {
    unique(e.id, `équipement « ${e.nom} »`);
    const locaux = new Set<string>();
    for (const i of e.interfaces) {
      if (!i.id || locaux.has(i.id))
        erreurs.push(`Interface « ${i.nom} » de « ${e.nom} » : identifiant absent ou en double.`);
      locaux.add(i.id);
      interfaces.set(`${e.id}/${i.id}`, i);
    }
    for (const i of e.interfaces) {
      if (i.parent && !locaux.has(i.parent))
        erreurs.push(`Interface « ${i.nom} » de « ${e.nom} » : interface parente absente.`);
    }
  }
  const occupees = new Map<string, number>();
  for (const l of schema.liens) {
    unique(l.id, "lien");
    for (const bout of [l.a, l.b]) {
      const cle = `${bout.equipement}/${bout.interface}`;
      const iface = interfaces.get(cle);
      if (!iface) {
        erreurs.push(`Lien « ${l.nom || l.id} » : extrémité absente (${cle}).`);
        continue;
      }
      occupees.set(cle, (occupees.get(cle) ?? 0) + 1);
      if (!iface.multiConnexion && (occupees.get(cle) ?? 0) > 1) {
        erreurs.push(`Interface « ${iface.nom} » : plusieurs câbles sur une interface point à point.`);
      }
    }
    if (l.a.equipement === l.b.equipement && l.a.interface === l.b.interface) {
      erreurs.push(`Lien « ${l.nom || l.id} » : une interface reliée à elle-même.`);
    }
  }
  const equipements = new Set(schema.equipements.map((e) => e.id));
  for (const r of schema.relations) {
    unique(r.id, "relation");
    if (!equipements.has(r.de) || !equipements.has(r.vers))
      erreurs.push(`Relation « ${r.nom || r.id} » : extrémité absente.`);
  }
  for (const v of schema.vlans) unique(v.id, `VLAN ${v.numero}`);
  for (const a of schema.agregats) {
    unique(a.id, `agrégat « ${a.nom} »`);
    if (!equipements.has(a.equipement)) erreurs.push(`Agrégat « ${a.nom} » : équipement absent.`);
    for (const m of a.membres) {
      if (!interfaces.has(`${a.equipement}/${m}`))
        erreurs.push(`Agrégat « ${a.nom} » : interface membre absente.`);
    }
  }
  const groupes = new Set(schema.groupes.map((g) => g.id));
  for (const g of schema.groupes) {
    unique(g.id, `groupe « ${g.nom} »`);
    if (g.parent && (!groupes.has(g.parent) || g.parent === g.id))
      erreurs.push(`Groupe « ${g.nom} » : groupe parent absent.`);
  }
  for (const n of schema.annotations) unique(n.id, "annotation");
  return erreurs;
}

/** Retire les références orphelines qui ne sont pas des erreurs (membre de groupe disparu…). */
function ranger(schema: Schema): void {
  const equipements = new Set(schema.equipements.map((e) => e.id));
  for (const g of schema.groupes) g.membres = g.membres.filter((m) => equipements.has(m));
  const interfaces = new Set(schema.equipements.flatMap((e) => e.interfaces.map((i) => `${e.id}/${i.id}`)));
  for (const a of schema.agregats)
    a.membres = a.membres.filter((m) => interfaces.has(`${a.equipement}/${m}`));
  schema.agregats = schema.agregats.filter((a) => equipements.has(a.equipement) && a.membres.length > 0);
  for (const e of schema.equipements) {
    if (e.gestion?.interface && !e.interfaces.some((i) => i.id === e.gestion?.interface))
      delete e.gestion.interface;
  }
}

export type Lecture = {
  schema: Schema;
  /** Erreurs structurelles : le schéma ne peut pas être enregistré ni importé tel quel. */
  erreurs: string[];
  /** Le texte était au format 1 (bloc ```reseau d'une fiche) : il a été converti. */
  migre: boolean;
};

/** Lit un schéma (format 2, ou format 1 converti). Lève une erreur si le texte n'est pas un schéma. */
export function lireSchema(source: string | unknown): Lecture {
  let donnees: unknown = source;
  if (typeof source === "string") {
    try {
      donnees = JSON.parse(source);
    } catch (erreur) {
      throw new Error(`JSON illisible (${erreur instanceof Error ? erreur.message : String(erreur)})`);
    }
  }
  if (typeof donnees !== "object" || donnees === null || Array.isArray(donnees)) {
    throw new Error("un objet JSON est attendu");
  }
  const brut = donnees as Brut;
  if (brut.version === 1 && brut.format === undefined) {
    const schema = migrerFormat1(brut);
    return { schema, erreurs: erreursStructurelles(schema), migre: true };
  }
  if (brut.format !== FORMAT) {
    throw new Error(
      typeof brut.format === "number" && brut.format > FORMAT
        ? `format ${brut.format} plus récent que cette version de kyfRev (format ${FORMAT})`
        : "ce n'est pas un schéma réseau de kyfRev (numéro de format absent)",
    );
  }
  const schema: Schema = propre({
    format: FORMAT,
    id: identifiant(brut.id),
    titre: texte(brut.titre, 200) || "Schéma réseau",
    description: facultatif(brut.description, LONG_MAX),
    modeles: objets(brut.modeles).map(lireModele),
    equipements: objets(brut.equipements).map(lireEquipement),
    liens: objets(brut.liens).map(lireLien),
    relations: objets(brut.relations).map(lireRelation),
    vlans: objets(brut.vlans).map(lireVlan),
    agregats: objets(brut.agregats).map(lireAgregat),
    groupes: objets(brut.groupes).map(lireGroupe),
    annotations: objets(brut.annotations).map(lireAnnotation),
    vue: VUES.find((v) => v === brut.vue) ?? "physique",
    cablesCoudes: booleen(brut.cablesCoudes),
    affichage: lireAffichage(brut.affichage),
  });
  ranger(schema);
  return { schema, erreurs: erreursStructurelles(schema), migre: false };
}

/** JSON du schéma : stable (champs dans l'ordre du modèle), indenté, relu par `lireSchema`. */
export function ecrireSchema(schema: Schema): string {
  const { schema: relu } = lireSchema(JSON.parse(JSON.stringify(schema)));
  return `${JSON.stringify(relu, null, 2)}\n`;
}
