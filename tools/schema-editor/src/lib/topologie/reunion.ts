// Réunir des schémas en un seul (31C), et le socle des échanges de pages entre schémas (31D, transfert.ts).
//
// Le nouveau schéma est fait des pages des schémas choisis, dans l'ordre choisi : un schéma d'une page y devient une
// page à son titre, un schéma à plusieurs pages y garde les siennes (leur nom, ou « Titre · Nom » si ce nom est déjà
// pris). Rien ne pointe vers les originaux : tous les identifiants sont refaits, et les originaux ne sont jamais
// modifiés. Les VLAN se réunissent par numéro (et périmètre) : en cas de noms différents, le premier est gardé et un
// avertissement le dit. Les modèles de matériel se réunissent par référence. Sur demande, deux équipements de même nom
// et de même adresse n'en font qu'un : ses câbles vers l'autre page deviennent des renvois.

import {
  type Agregat,
  type Equipement,
  FORMAT,
  FORMAT_PAGES,
  type Interface,
  type ModeleMateriel,
  nouvelId,
  type Page,
  type Relation,
  type Schema,
  schemaVide,
  type Vlan,
} from "./modele";
import { NOM_PAGE_MAX, NOM_PREMIERE_PAGE, pageDeLElement, pagesDe } from "./pages";

const cloner = <T>(valeur: T): T => JSON.parse(JSON.stringify(valeur)) as T;

// --- Refaire les identifiants ---------------------------------------------------------------------------------

/**
 * Une copie du schéma, tous ses identifiants refaits : pages, modèles, équipements et leurs interfaces, câbles,
 * relations, VLAN, agrégats, cadres, annotations, ensembles. Chaque référence suit (bouts de câble, interface
 * parente, interface de gestion, membres d'un agrégat, d'un cadre ou d'un ensemble, page de chaque élément, modèle
 * de chaque membre). Un élément sans page va sur la page d'identifiant vide, s'il y en a une.
 */
export function renumeroter(source: Schema): Schema {
  const schema = cloner(source);
  const pages = new Map<string, string>();
  for (const p of schema.pages ?? []) pages.set(p.id, nouvelId("p"));
  const page = (id: string | undefined) => pages.get(id ?? "");
  for (const p of schema.pages ?? []) p.id = pages.get(p.id) ?? p.id;

  const modeles = new Map(schema.modeles.map((m) => [m.id, nouvelId("m")]));
  for (const m of schema.modeles) m.id = modeles.get(m.id) ?? m.id;

  const ids = new Map<string, string>();
  const interfaces = new Map<string, string>();
  for (const e of schema.equipements) {
    const nouveau = nouvelId("d");
    ids.set(e.id, nouveau);
    for (const i of e.interfaces) interfaces.set(`${e.id}/${i.id}`, nouvelId("i"));
  }
  const iface = (equipement: string, id: string | undefined) =>
    id === undefined ? undefined : interfaces.get(`${equipement}/${id}`);
  for (const e of schema.equipements) {
    const ancien = e.id;
    e.id = ids.get(ancien) ?? e.id;
    const p = page(e.page);
    if (p) e.page = p;
    for (const i of e.interfaces) {
      const parent = iface(ancien, i.parent);
      i.id = iface(ancien, i.id) ?? i.id;
      if (parent) i.parent = parent;
    }
    if (e.gestion?.interface) {
      const gestion = iface(ancien, e.gestion.interface);
      if (gestion) e.gestion.interface = gestion;
      else delete e.gestion.interface;
    }
    for (const m of e.membres) if (m.modele) m.modele = modeles.get(m.modele) ?? m.modele;
  }
  const bout = (b: { equipement: string; interface: string }) => ({
    equipement: ids.get(b.equipement) ?? b.equipement,
    interface: iface(b.equipement, b.interface) ?? b.interface,
  });
  for (const l of schema.liens) {
    l.id = nouvelId("l");
    l.a = bout(l.a);
    l.b = bout(l.b);
  }
  for (const r of schema.relations) {
    r.id = nouvelId("r");
    if (r.deInterface) r.deInterface = iface(r.de, r.deInterface) ?? r.deInterface;
    if (r.versInterface) r.versInterface = iface(r.vers, r.versInterface) ?? r.versInterface;
    r.de = ids.get(r.de) ?? r.de;
    r.vers = ids.get(r.vers) ?? r.vers;
  }
  for (const v of schema.vlans) v.id = nouvelId("v");
  for (const a of schema.agregats) {
    a.id = nouvelId("a");
    a.membres = a.membres.map((m) => iface(a.equipement, m) ?? m);
    a.equipement = ids.get(a.equipement) ?? a.equipement;
  }
  const cadres = new Map(schema.groupes.map((g) => [g.id, nouvelId("g")]));
  for (const g of schema.groupes) {
    g.id = cadres.get(g.id) ?? g.id;
    const p = page(g.page);
    if (p) g.page = p;
    if (g.parent) g.parent = cadres.get(g.parent) ?? g.parent;
    g.membres = g.membres.map((m) => ids.get(m) ?? m);
  }
  const notes = new Map(schema.annotations.map((n) => [n.id, nouvelId("n")]));
  for (const n of schema.annotations) {
    n.id = notes.get(n.id) ?? n.id;
    const p = page(n.page);
    if (p) n.page = p;
  }
  for (const e of schema.ensembles ?? []) {
    e.id = nouvelId("e");
    e.membres = e.membres.map((m) => ids.get(m) ?? cadres.get(m) ?? notes.get(m) ?? m);
  }
  return schema;
}

// --- Extraire des pages ---------------------------------------------------------------------------------------

/** Les pages extraites d'un schéma, et ce qui n'a pas pu venir avec elles. */
export type Extrait = {
  /** Un schéma fait de ces pages seulement (identifiants d'origine) ; chaque élément porte sa page. */
  morceau: Schema;
  /** Câbles entre une page extraite et une page qui ne l'est pas : ils ne viennent pas. */
  cablesHors: number;
  /** Relations entre une page extraite et une page qui ne l'est pas : elles ne viennent pas. */
  relationsHors: number;
};

/** Un numéro de VLAN figure-t-il dans une liste écrite comme on la lit (« 10,20,30-40 ») ? */
export function dansLaListe(liste: string | undefined, numero: number): boolean {
  for (const morceau of (liste ?? "").split(",")) {
    const [debut, fin] = morceau.split("-").map((x) => Number(x.trim()));
    if (debut === undefined || !Number.isFinite(debut) || morceau.trim() === "") continue;
    if (fin === undefined ? numero === debut : Number.isFinite(fin) && numero >= debut && numero <= fin) return true;
  }
  return false;
}

/** Les VLAN dont un morceau se sert : ports, agrégats, gestion, sous-réseaux des équipements. */
function vlansCites(morceau: Schema, vlans: Vlan[]): Vlan[] {
  const configs = [
    ...morceau.equipements.flatMap((e) => e.interfaces.map((i) => i.vlan)),
    ...morceau.agregats.map((a) => a.vlan),
  ].filter((c) => c !== undefined);
  const gestion = new Set(morceau.equipements.map((e) => e.gestion?.vlan).filter((n) => n !== undefined));
  return vlans.filter(
    (v) =>
      gestion.has(v.numero) ||
      configs.some(
        (c) => c.acces === v.numero || c.natif === v.numero || c.voix === v.numero || c.pvid === v.numero || dansLaListe(c.tagues, v.numero),
      ),
  );
}

/**
 * Les pages `ids` d'un schéma (toutes, sans `ids`), en un schéma à part, sans rien modifier de l'original : leurs
 * équipements, cadres et annotations, les câbles et relations entre eux, leurs agrégats, leurs ensembles, les modèles
 * qu'ils citent ; les VLAN : tous pour le schéma entier, ceux dont ces pages se servent sinon. Les pages gardent leur
 * nom ; celle d'un schéma qui n'en déclare pas prend `nomPageUnique` (ou « Page 1 »).
 */
export function extrairePages(source: Schema, ids?: string[], nomPageUnique?: string): Extrait {
  const toutes = pagesDe(source);
  const voulues = new Set(ids ?? toutes.map((p) => p.id));
  const pages = toutes
    .filter((p) => voulues.has(p.id))
    .map((p) => ({ ...p, ...(toutes.length === 1 && nomPageUnique ? { nom: nomPageUnique } : {}) }));
  const garde = new Set(pages.map((p) => p.id));
  const ici = <T extends { page?: string }>(element: T) => garde.has(pageDeLElement(source, element));
  const avecPage = <T extends { page?: string }>(element: T): T => ({ ...cloner(element), page: pageDeLElement(source, element) });

  const equipements = source.equipements.filter(ici).map(avecPage);
  const dedans = new Set(equipements.map((e) => e.id));
  const groupes = source.groupes.filter(ici).map(avecPage);
  const annotations = source.annotations.filter(ici).map(avecPage);
  const presents = new Set([...dedans, ...groupes.map((g) => g.id), ...annotations.map((n) => n.id)]);
  const touche = (a: string, b: string) => dedans.has(a) || dedans.has(b);
  const entier = (a: string, b: string) => dedans.has(a) && dedans.has(b);
  const cites = new Set(equipements.flatMap((e) => e.membres.map((m) => m.modele)).filter(Boolean));

  const morceau: Schema = {
    ...cloner({ ...source, pages: undefined, equipements: [], liens: [], relations: [], agregats: [], groupes: [], annotations: [], modeles: [], vlans: [], ensembles: undefined }),
    pages,
    modeles: cloner(source.modeles.filter((m) => cites.has(m.id))),
    equipements,
    liens: cloner(source.liens.filter((l) => entier(l.a.equipement, l.b.equipement))),
    relations: cloner(source.relations.filter((r) => entier(r.de, r.vers))),
    agregats: cloner(source.agregats.filter((a) => dedans.has(a.equipement))),
    groupes: groupes.map((g) => ({ ...g, membres: g.membres.filter((m) => dedans.has(m)), ...(g.parent && presents.has(g.parent) ? {} : { parent: undefined }) })),
    annotations,
  };
  for (const g of morceau.groupes) if (g.parent === undefined) delete g.parent;
  const ensembles = (source.ensembles ?? [])
    .map((e) => ({ ...cloner(e), membres: e.membres.filter((m) => presents.has(m)) }))
    .filter((e) => e.membres.length >= 2);
  if (ensembles.length > 0) morceau.ensembles = ensembles;
  else delete morceau.ensembles;
  morceau.vlans = cloner(voulues.size === toutes.length ? source.vlans : vlansCites(morceau, source.vlans));
  return {
    morceau,
    cablesHors: source.liens.filter((l) => touche(l.a.equipement, l.b.equipement) && !entier(l.a.equipement, l.b.equipement)).length,
    relationsHors: source.relations.filter((r) => touche(r.de, r.vers) && !entier(r.de, r.vers)).length,
  };
}

// --- Fusionner un morceau dans un schéma -----------------------------------------------------------------------

/** Ce qu'a donné l'arrivée d'un morceau dans un schéma. */
export type Fusion = {
  /** Les pages ajoutées, dans l'ordre. */
  pages: Page[];
  /** Équipements arrivés (ceux réunis à un équipement déjà là ne comptent pas). */
  equipements: number;
  /** Équipements réunis à un équipement de même nom et de même adresse. */
  reunis: string[];
  /** Câbles qui vont désormais d'une page à une autre (renvois), du fait des équipements réunis. */
  renvois: number;
  /** Câbles en double (déjà là, entre les mêmes ports) laissés de côté. */
  doubles: number;
  avertissements: string[];
};

export type OptionsFusion = {
  /** Deux équipements de même nom et de même adresse n'en font qu'un. */
  reunirDoublons?: boolean;
  /** Ce qui suit « Titre · » quand le nom d'une page est déjà pris : le titre du schéma d'où elle vient. */
  prefixe?: string;
  /** La phrase qui conclut un conflit de nom de VLAN (« Le nom du premier schéma est gardé. »). */
  conflitVlan?: (vlan: number, garde: string, autre: string) => string;
  /** Une cible vide perd sa page vide au profit des nouvelles (par défaut, oui). */
  remplacerSiVide?: boolean;
};

/** Un nom de page libre : `voulu`, sinon « Préfixe · voulu », sinon suivi d'un numéro. */
export function nomDePageUnique(pris: Set<string>, voulu: string, prefixe?: string): string {
  const borne = (nom: string) => nom.slice(0, NOM_PAGE_MAX).trim();
  const libre = (nom: string) => !pris.has(nom.toLocaleLowerCase("fr"));
  const base = borne(voulu) || NOM_PREMIERE_PAGE;
  if (libre(base)) return base;
  const prefixee = prefixe ? borne(`${prefixe} · ${base}`) : base;
  if (libre(prefixee)) return prefixee;
  for (let n = 2; ; n++) {
    const suffixe = ` (${n})`;
    const numerotee = `${prefixee.slice(0, NOM_PAGE_MAX - suffixe.length).trim()}${suffixe}`;
    if (libre(numerotee)) return numerotee;
  }
}

const cleVlan = (v: Vlan) => `${v.numero}|${(v.perimetre ?? "").trim().toLocaleLowerCase("fr")}`;
const cleModele = (m: ModeleMateriel) =>
  `${(m.fabricant ?? "").trim().toLocaleLowerCase("fr")}|${m.reference.trim().toLocaleLowerCase("fr")}`;

/** L'adresse d'un équipement, pour le reconnaître : celle de gestion, sinon la première de ses interfaces ; sans le préfixe. */
function adresseDe(e: Equipement): string {
  const brute = e.gestion?.adresse ?? e.interfaces.flatMap((i) => i.adresses)[0]?.adresse ?? "";
  return brute.split("/")[0]?.trim().toLowerCase() ?? "";
}

/** Ce qui reconnaît un même équipement d'un schéma à l'autre : son nom et son adresse (deux sans adresse vont ensemble). */
export const cleEquipement = (e: Equipement): string => `${e.nom.trim().toLocaleLowerCase("fr")}|${adresseDe(e)}`;

/** Le schéma est-il vide (rien de posé, une page au plus) ? Des pages qui y arrivent prennent alors la place de la sienne. */
export const estVierge = (schema: Schema): boolean =>
  schema.equipements.length === 0 && schema.groupes.length === 0 && schema.annotations.length === 0 && (schema.pages?.length ?? 0) <= 1;

const conflitParDefaut = (vlan: number, garde: string, autre: string) =>
  `VLAN ${vlan} : « ${garde} » d'un côté, « ${autre} » de l'autre. Le nom du premier schéma est gardé.`;

/**
 * Fait entrer `morceau` (identifiants déjà refaits : voir `renumeroter`) dans `cible`, qui est modifié : ses pages
 * viennent après celles de la cible (un nom déjà pris est préfixé), ses VLAN et ses modèles rejoignent ceux de même
 * numéro ou de même référence. Une cible vide perd sa page vide au profit des nouvelles.
 */
export function fusionner(cible: Schema, morceau: Schema, options: OptionsFusion = {}): Fusion {
  const avertissements: string[] = [];
  const conflit = options.conflitVlan ?? conflitParDefaut;

  // Les VLAN, par numéro (et périmètre) : le premier nom est gardé ; les sous-réseaux s'ajoutent.
  for (const v of morceau.vlans) {
    const deja = cible.vlans.find((x) => cleVlan(x) === cleVlan(v));
    if (!deja) {
      cible.vlans.push(cloner(v));
      continue;
    }
    if (deja.nom.trim().toLocaleLowerCase("fr") !== v.nom.trim().toLocaleLowerCase("fr")) avertissements.push(conflit(v.numero, deja.nom, v.nom));
    for (const s of v.sousReseaux) if (!deja.sousReseaux.includes(s)) deja.sousReseaux.push(s);
    if (!deja.description && v.description) deja.description = v.description;
    if (!deja.couleur && v.couleur) deja.couleur = v.couleur;
  }

  // Les modèles de matériel, par référence (et fabricant) : un membre qui en citait un cite celui qui est gardé.
  const modeles = new Map<string, string>();
  for (const m of morceau.modeles) {
    const deja = cible.modeles.find((x) => cleModele(x) === cleModele(m));
    if (deja) modeles.set(m.id, deja.id);
    else cible.modeles.push(cloner(m));
  }
  const equipements = cloner(morceau.equipements);
  for (const e of equipements) for (const membre of e.membres) if (membre.modele) membre.modele = modeles.get(membre.modele) ?? membre.modele;

  // Les pages : celles de la cible d'abord (sa page implicite se déclare) ; une cible vide perd la sienne.
  const vierge = (options.remplacerSiVide ?? true) && estVierge(cible);
  if (vierge) {
    delete cible.pages;
  } else if (!cible.pages || cible.pages.length === 0) {
    cible.pages = [{ id: nouvelId("p"), nom: NOM_PREMIERE_PAGE }];
  }
  const premiere = cible.pages?.[0]?.id;
  if (premiere) for (const x of [...cible.equipements, ...cible.groupes, ...cible.annotations]) x.page ??= premiere;
  const pris = new Set((cible.pages ?? []).map((p) => p.nom.toLocaleLowerCase("fr")));
  const ajoutees: Page[] = [];
  for (const p of morceau.pages ?? []) {
    const page = { id: p.id, nom: nomDePageUnique(pris, p.nom, options.prefixe) };
    pris.add(page.nom.toLocaleLowerCase("fr"));
    ajoutees.push(page);
  }
  cible.pages = [...(cible.pages ?? []), ...ajoutees];

  // Les doublons : un équipement déjà là, de même nom et de même adresse, prend la place de celui qui arrive.
  const remplace = new Map<string, Equipement>();
  const interfaces = new Map<string, string>();
  const reunis: string[] = [];
  if (options.reunirDoublons) {
    const deja = new Map<string, Equipement>();
    for (const e of cible.equipements) if (!deja.has(cleEquipement(e))) deja.set(cleEquipement(e), e);
    for (const e of equipements) {
      const garde = deja.get(cleEquipement(e));
      if (!garde) continue;
      remplace.set(e.id, garde);
      reunis.push(garde.nom);
      const ajoutees: { copie: Interface; parent?: string }[] = [];
      for (const i of e.interfaces) {
        const meme = garde.interfaces.find((x) => x.nom === i.nom);
        if (meme) {
          interfaces.set(`${e.id}/${i.id}`, meme.id);
          continue;
        }
        const copie: Interface = { ...cloner(i), id: nouvelId("i") };
        delete copie.parent;
        garde.interfaces.push(copie);
        interfaces.set(`${e.id}/${i.id}`, copie.id);
        ajoutees.push({ copie, ...(i.parent ? { parent: i.parent } : {}) });
      }
      for (const { copie, parent } of ajoutees) {
        const nouveau = parent ? interfaces.get(`${e.id}/${parent}`) : undefined;
        if (nouveau) copie.parent = nouveau;
      }
    }
  }
  const versGarde = (id: string) => remplace.get(id)?.id ?? id;
  const versBout = (b: { equipement: string; interface: string }) =>
    remplace.has(b.equipement)
      ? { equipement: versGarde(b.equipement), interface: interfaces.get(`${b.equipement}/${b.interface}`) ?? b.interface }
      : b;

  const restants = equipements.filter((e) => !remplace.has(e.id));
  const pageDe = new Map<string, string | undefined>([
    ...cible.equipements.map((e) => [e.id, e.page] as const),
    ...restants.map((e) => [e.id, e.page] as const),
  ]);
  cible.equipements.push(...restants);

  // Les câbles : un bout réuni passe sur l'équipement gardé ; un câble déjà là (mêmes deux ports) n'est pas doublé ; un
  // port point à point déjà pris en reçoit un autre, à côté, et un avertissement le dit.
  const occupe = new Map<string, number>();
  const cle = (b: { equipement: string; interface: string }) => `${b.equipement}/${b.interface}`;
  for (const l of cible.liens) for (const b of [l.a, l.b]) occupe.set(cle(b), (occupe.get(cle(b)) ?? 0) + 1);
  const paires = new Set(cible.liens.map((l) => [cle(l.a), cle(l.b)].sort().join(" ")));
  let doubles = 0;
  let renvois = 0;
  for (const l of cloner(morceau.liens)) {
    const touche = remplace.has(l.a.equipement) || remplace.has(l.b.equipement);
    l.a = versBout(l.a);
    l.b = versBout(l.b);
    if (cle(l.a) === cle(l.b)) continue;
    const paire = [cle(l.a), cle(l.b)].sort().join(" ");
    if (paires.has(paire)) {
      doubles++;
      continue;
    }
    if (touche) {
      for (const bout of [l.a, l.b]) {
        const garde = cible.equipements.find((e) => e.id === bout.equipement);
        const port = garde?.interfaces.find((i) => i.id === bout.interface);
        if (!garde || !port || port.multiConnexion || (occupe.get(cle(bout)) ?? 0) === 0) continue;
        const noms = new Set(garde.interfaces.map((i) => i.nom));
        let n = 2;
        while (noms.has(`${port.nom} (${n})`)) n++;
        const autre: Interface = { ...cloner(port), id: nouvelId("i"), nom: `${port.nom} (${n})`, adresses: [], macs: [] };
        delete autre.parent;
        garde.interfaces.push(autre);
        avertissements.push(`« ${garde.nom} » : le port « ${port.nom} » est câblé dans les deux schémas ; le second câble passe sur « ${autre.nom} ».`);
        bout.interface = autre.id;
      }
      if (pageDe.get(l.a.equipement) !== pageDe.get(l.b.equipement)) renvois++;
    }
    for (const b of [l.a, l.b]) occupe.set(cle(b), (occupe.get(cle(b)) ?? 0) + 1);
    paires.add([cle(l.a), cle(l.b)].sort().join(" "));
    cible.liens.push(l);
  }

  for (const r of cloner(morceau.relations)) {
    const relation: Relation = { ...r, de: versGarde(r.de), vers: versGarde(r.vers) };
    if (r.deInterface && remplace.has(r.de)) relation.deInterface = interfaces.get(`${r.de}/${r.deInterface}`) ?? r.deInterface;
    if (r.versInterface && remplace.has(r.vers)) relation.versInterface = interfaces.get(`${r.vers}/${r.versInterface}`) ?? r.versInterface;
    if (relation.de === relation.vers) continue;
    if (cible.relations.some((x) => x.genre === relation.genre && x.de === relation.de && x.vers === relation.vers)) continue;
    cible.relations.push(relation);
  }
  for (const a of cloner(morceau.agregats)) {
    const garde = remplace.get(a.equipement);
    const agregat: Agregat = garde
      ? { ...a, equipement: garde.id, membres: a.membres.map((m) => interfaces.get(`${a.equipement}/${m}`) ?? m) }
      : a;
    if (garde && cible.agregats.some((x) => x.equipement === garde.id && x.nom === agregat.nom)) continue;
    cible.agregats.push(agregat);
  }
  cible.groupes.push(...cloner(morceau.groupes).map((g) => ({ ...g, membres: g.membres.filter((m) => !remplace.has(m)) })));
  cible.annotations.push(...cloner(morceau.annotations));
  const ensembles = cloner(morceau.ensembles ?? [])
    .map((e) => ({ ...e, membres: e.membres.filter((m) => !remplace.has(m)) }))
    .filter((e) => e.membres.length >= 2);
  if (ensembles.length > 0) cible.ensembles = [...(cible.ensembles ?? []), ...ensembles];

  rangerLesPages(cible);
  return { pages: ajoutees, equipements: restants.length, reunis, renvois, doubles, avertissements };
}

/**
 * Le rangement par pages, après un échange : avec deux pages ou plus, chaque élément porte la sienne (la première, s'il
 * n'en a pas de valable) et le schéma passe au format 3 ; avec une seule, rien ne porte de page (format 2), la page
 * gardant son nom.
 */
export function rangerLesPages(schema: Schema): void {
  const pages = schema.pages ?? [];
  const elements = [...schema.equipements, ...schema.groupes, ...schema.annotations];
  if (pages.length < 2) {
    for (const x of elements) delete x.page;
    schema.format = FORMAT;
    return;
  }
  const connues = new Set(pages.map((p) => p.id));
  const premiere = pages[0]?.id ?? "";
  for (const x of elements) if (!x.page || !connues.has(x.page)) x.page = premiere;
  schema.format = FORMAT_PAGES;
}

// --- Réunir ----------------------------------------------------------------------------------------------------

export type OptionsReunion = {
  /** Le titre du nouveau schéma. */
  titre: string;
  /** Deux équipements de même nom et de même adresse n'en font qu'un (par défaut, chacun garde les siens). */
  reunirDoublons?: boolean;
};

export type Reunion = {
  /** Le nouveau schéma, sans identifiant (le coffre lui en donne un). */
  schema: Schema;
  /** Les pages du nouveau schéma, pour chaque schéma d'origine, dans l'ordre. */
  pagesParSchema: Page[][];
  /** Équipements réunis à un équipement d'un schéma précédent. */
  reunis: string[];
  renvois: number;
  avertissements: string[];
};

/**
 * Un nouveau schéma fait des pages de `schemas`, dans cet ordre ; les originaux ne sont pas touchés. La vue,
 * l'affichage et le tracé des câbles sont ceux du premier.
 */
export function reunir(schemas: Schema[], options: OptionsReunion): Reunion {
  const premier = schemas[0];
  const schema: Schema = {
    ...schemaVide(options.titre.trim() || premier?.titre || "Schéma réseau"),
    ...(premier ? { vue: premier.vue } : {}),
    ...(premier?.cablesCoudes ? { cablesCoudes: true } : {}),
    ...(premier?.affichage ? { affichage: cloner(premier.affichage) } : {}),
  };
  const pagesParSchema: Page[][] = [];
  const reunis: string[] = [];
  const avertissements: string[] = [];
  let renvois = 0;
  schemas.forEach((source, rang) => {
    const { morceau } = extrairePages(source, undefined, source.titre);
    // Le nouveau schéma n'a pas de page à lui : la première venue prend la place de sa page implicite.
    const fusion = fusionner(schema, renumeroter(morceau), {
      ...(options.reunirDoublons ? { reunirDoublons: true } : {}),
      prefixe: source.titre,
      remplacerSiVide: rang === 0,
    });
    pagesParSchema.push(fusion.pages);
    reunis.push(...fusion.reunis.filter((nom) => !reunis.includes(nom)));
    renvois += fusion.renvois;
    for (const a of fusion.avertissements) if (!avertissements.includes(a)) avertissements.push(a);
  });
  return { schema, pagesParSchema, reunis, renvois, avertissements };
}

/** Les noms des équipements qu'on retrouve (même nom, même adresse) dans au moins deux des schémas. */
export function doublonsEntre(schemas: Schema[]): string[] {
  const vus = new Map<string, { nom: string; schemas: Set<number> }>();
  schemas.forEach((s, rang) => {
    for (const e of s.equipements) {
      const cle = cleEquipement(e);
      const deja = vus.get(cle) ?? { nom: e.nom, schemas: new Set<number>() };
      deja.schemas.add(rang);
      vus.set(cle, deja);
    }
  });
  return [...vus.values()].filter((v) => v.schemas.size >= 2).map((v) => v.nom);
}

/** Le nom proposé pour le schéma réuni : celui du premier, « — complet ». */
export const titrePropose = (schemas: Pick<Schema, "titre">[]): string =>
  schemas[0] ? `${schemas[0].titre} — complet`.slice(0, 200) : "Schéma réseau";
