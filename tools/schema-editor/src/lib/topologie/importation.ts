// Import d'un schéma (29E) : le modèle de kyfRev (.json), ou un fichier draw.io (.drawio, .xml).
//
// Un fichier draw.io exporté par kyfRev garde le modèle complet dans son calque (drawio.ts) : on repart de
// lui et on n'applique que ce qui a changé dans draw.io (places, noms, formes et connecteurs ajoutés ou
// retirés). Sinon, on traduit : une forme réseau devient un équipement (type deviné à sa forme, sinon
// « Générique »), un connecteur entre deux équipements un câble (avec ses ports, créés au besoin, comme la
// migration du format 1), un conteneur une zone, un texte ou un cadre une annotation. Chaque page du fichier
// devient une page du schéma (30A) ; un câble entre deux pages, que draw.io montre en renvoi, se garde par le
// modèle. Le résultat passe par la même lecture que tout schéma (format.ts) : ce qui n'est pas valide est
// refusé, rien n'est enregistré.

import { typeConnu } from "./catalogue";
import { cadreAnnotation, estCoude } from "./dessin";
import { typeDevine } from "./drawio";
import { ecrireSchema, lireSchema, PAGES_MAX } from "./format";
import { boutDeLien } from "./migration";
import {
  type Annotation,
  type FormeAnnotation,
  GENRES_RELATION,
  type Groupe,
  type Lien,
  nouvelId,
  type Point,
  type Schema,
  SUPPORTS,
  schemaVide,
} from "./modele";
import { creerEquipement, retirerEquipements } from "./operations";
import { aPlusieursPages, elementsDuSchema, NOM_PAGE_MAX, pageDeLElement, pagesDe } from "./pages";
import { type ElementXml, enfantsNommes, lireXml } from "./xml";

/** Un schéma importé tient en 8 Mo, comme tout schéma du coffre. */
export const TAILLE_MAX_IMPORT = 8 * 1024 * 1024;
/** Les pages compressées ne se décompressent pas au-delà, toutes ensemble : un fichier piégé ne remplit pas la mémoire. */
const DECOMPRESSE_MAX = 64 * 1024 * 1024;
const TITRE_PAR_DEFAUT = "Schéma importé";

/** Ce que l'import dit après coup : ce qui est entré, et ce qu'il vaut la peine de regarder. */
export type CompteRendu = {
  equipements: number;
  cables: number;
  zones: number;
  textes: number;
  /** Équipements posés en « Générique » faute de reconnaître leur forme. */
  inconnus: string[];
  /** Connecteurs courbes, redressés en coudés. */
  courbes: number;
  /** Connecteurs sans extrémité, laissés de côté. */
  sansExtremite: number;
  /** Les pages du fichier, chacune devenue une page du schéma. */
  pages: number;
};

export type Importe = {
  schema: Schema;
  /** Erreurs structurelles : l'import est refusé. */
  erreurs: string[];
  /** Pour un fichier draw.io ; rien pour un modèle de kyfRev. */
  compteRendu: CompteRendu | null;
};

/** Un fichier draw.io est du XML ; un modèle de kyfRev, du JSON. */
export const estFichierDrawio = (texte: string): boolean => texte.replace(/^﻿/, "").trimStart().startsWith("<");

/** Le nom d'un fichier, sans dossier ni extension. */
export const nomDuFichier = (chemin: string): string =>
  (chemin.split(/[\\/]/).pop() ?? "").replace(/\.[^.]*$/, "").trim();

/** Lit un fichier choisi pour l'import ; lève une erreur s'il n'est ni un schéma de kyfRev ni un fichier draw.io. */
export async function lireImport(texte: string, chemin = ""): Promise<Importe> {
  if (!estFichierDrawio(texte)) {
    const lecture = lireSchema(texte);
    return { schema: lecture.schema, erreurs: lecture.erreurs, compteRendu: null };
  }
  const { schema, remarques } = await lireDrawio(texte, nomDuFichier(chemin));
  const lecture = lireSchema(JSON.parse(JSON.stringify(schema)));
  if (new TextEncoder().encode(ecrireSchema(lecture.schema)).length > TAILLE_MAX_IMPORT) {
    throw new Error("le schéma importé dépasserait 8 Mo");
  }
  const zones = lecture.schema.annotations.filter((n) => n.forme === "rectangle" || n.forme === "ellipse").length;
  return {
    schema: lecture.schema,
    erreurs: lecture.erreurs,
    compteRendu: {
      equipements: lecture.schema.equipements.length,
      cables: lecture.schema.liens.length,
      zones: lecture.schema.groupes.length + zones,
      textes: lecture.schema.annotations.length - zones,
      ...remarques,
    },
  };
}

// --- Le compte rendu, en phrases ---------------------------------------------------------------

const accorde = (n: number, singulier: string, pluriel: string) => `${n} ${n > 1 ? pluriel : singulier}`;

/** Ce qui est entré : équipements, câbles, zones et textes. */
export function bilanImport(c: CompteRendu): string[] {
  return [
    ...(c.pages > 1 ? [`${c.pages} pages`] : []),
    accorde(c.equipements, "équipement", "équipements"),
    c.cables > 1 ? `${c.cables} câbles, avec leurs ports` : c.cables === 1 ? "1 câble, avec ses ports" : "0 câble",
    `${accorde(c.zones, "zone", "zones")} et ${accorde(c.textes, "texte", "textes")}`,
  ];
}

export type Remarque = { texte: string; montrer?: boolean };

/** Ce qu'il vaut la peine de regarder : seulement ce qui n'est pas nul. */
export function remarquesImport(c: CompteRendu): Remarque[] {
  const n = c.inconnus.length;
  return [
    ...(n > 0
      ? [{ texte: `${accorde(n, "forme de type inconnu, posée", "formes de type inconnu, posées")} en « Générique »`, montrer: true }]
      : []),
    ...(c.courbes > 0
      ? [{ texte: accorde(c.courbes, "connecteur courbe, redressé en coudé", "connecteurs courbes, redressés en coudés") }]
      : []),
    ...(c.sansExtremite > 0
      ? [{ texte: accorde(c.sansExtremite, "connecteur sans extrémité, laissé de côté", "connecteurs sans extrémité, laissés de côté") }]
      : []),
  ];
}

// --- Lecture du fichier draw.io ------------------------------------------------------------------

type Cellule = {
  id: string;
  parent: string;
  /** Le libellé, tel qu'écrit (HTML compris si `html=1`). */
  valeur: string;
  /** Les données de la cellule (« Modifier les données » de draw.io). */
  donnees: Record<string, string>;
  style: Record<string, string>;
  /** Les mots du style sans valeur (« text », « ellipse », « edgeLabel »…). */
  jetons: string[];
  sommet: boolean;
  arete: boolean;
  source: string;
  cible: string;
  x: number;
  y: number;
  largeur: number;
  hauteur: number;
  points: Point[];
  depart?: Point;
  arrivee?: Point;
};

type Remarques = Pick<CompteRendu, "inconnus" | "courbes" | "sansExtremite" | "pages">;

const nombreXml = (valeur: string | undefined): number => {
  const n = Number(valeur);
  return Number.isFinite(n) ? n : 0;
};
const pointDe = (e: ElementXml): Point => ({ x: nombreXml(e.attributs.x), y: nombreXml(e.attributs.y) });

function lireStyle(texte: string): { style: Record<string, string>; jetons: string[] } {
  const style: Record<string, string> = {};
  const jetons: string[] = [];
  for (const partie of texte.split(";")) {
    if (!partie) continue;
    const egal = partie.indexOf("=");
    if (egal < 0) jetons.push(partie.trim());
    else style[partie.slice(0, egal).trim()] = partie.slice(egal + 1).trim();
  }
  return { style, jetons };
}

function lireCellules(racine: ElementXml): Cellule[] {
  const cellules: Cellule[] = [];
  for (const enfant of racine.enfants) {
    const objet = enfant.nom === "object" || enfant.nom === "UserObject" ? enfant : undefined;
    const cellule = objet ? enfantsNommes(objet, "mxCell")[0] : enfant.nom === "mxCell" ? enfant : undefined;
    if (!cellule) continue;
    const id = objet?.attributs.id ?? cellule.attributs.id ?? "";
    if (!id) continue;
    const donnees = { ...(objet?.attributs ?? {}) };
    delete donnees.id;
    delete donnees.label;
    delete donnees.placeholders;
    const geometrie = cellule.enfants.find((e) => e.nom === "mxGeometry");
    const points = geometrie?.enfants.find((e) => e.nom === "Array" && e.attributs.as === "points");
    const role = (nom: string) => geometrie?.enfants.find((e) => e.nom === "mxPoint" && e.attributs.as === nom);
    const depart = role("sourcePoint");
    const arrivee = role("targetPoint");
    cellules.push({
      id,
      parent: cellule.attributs.parent ?? "",
      valeur: objet ? (objet.attributs.label ?? "") : (cellule.attributs.value ?? ""),
      donnees,
      ...lireStyle(cellule.attributs.style ?? ""),
      sommet: cellule.attributs.vertex === "1",
      arete: cellule.attributs.edge === "1",
      source: cellule.attributs.source ?? "",
      cible: cellule.attributs.target ?? "",
      x: nombreXml(geometrie?.attributs.x),
      y: nombreXml(geometrie?.attributs.y),
      largeur: nombreXml(geometrie?.attributs.width),
      hauteur: nombreXml(geometrie?.attributs.height),
      points: points ? enfantsNommes(points, "mxPoint").map(pointDe) : [],
      ...(depart ? { depart: pointDe(depart) } : {}),
      ...(arrivee ? { arrivee: pointDe(arrivee) } : {}),
    });
  }
  return cellules;
}

const ENTITES_HTML: Record<string, string> = { amp: "&", lt: "<", gt: ">", quot: '"', apos: "'", nbsp: " " };

/** Le texte d'un libellé : sans balises s'il est en HTML, sauts de ligne gardés. */
export function texteDuLibelle(valeur: string, html: boolean): string {
  const brut = html
    ? valeur
        .replace(/<br\s*\/?>/gi, "\n")
        .replace(/<\/(div|p|li)>/gi, "\n")
        .replace(/<[^>]*>/g, "")
        .replace(/&(#x[0-9a-f]+|#\d+|[a-z]+);/gi, (tout, e: string) => {
          if (e[0] !== "#") return ENTITES_HTML[e.toLowerCase()] ?? tout;
          const code = e[1] === "x" || e[1] === "X" ? Number.parseInt(e.slice(2), 16) : Number.parseInt(e.slice(1), 10);
          return Number.isInteger(code) && code > 0 && code <= 0x10ffff ? String.fromCodePoint(code) : tout;
        })
    : valeur;
  return brut
    .replace(/ /g, " ")
    .split("\n")
    .map((ligne) => ligne.trim())
    .filter(Boolean)
    .join("\n");
}

const libelle = (c: Cellule) => texteDuLibelle(c.valeur, c.style.html === "1");
const premiereLigne = (c: Cellule) => libelle(c).split("\n")[0] ?? "";
const arrondi = (p: Point): Point => ({ x: Math.round(p.x), y: Math.round(p.y) });

/**
 * Décompresse une page : base 64, puis deflate brut, puis texte encodé comme une adresse (ainsi l'écrit draw.io).
 * `budget` est ce qui reste à décompresser pour tout le fichier : beaucoup de pages ne remplissent pas la mémoire.
 */
async function pageCompressee(texte: string, nom: string, budget: { reste: number }): Promise<ElementXml> {
  const brut = texte.replace(/\s+/g, "");
  if (!brut) throw new Error(`la page « ${nom} » du fichier draw.io est vide`);
  let octets: Uint8Array;
  try {
    octets = Uint8Array.from(atob(brut), (c) => c.charCodeAt(0));
  } catch {
    throw new Error(`la page « ${nom} » du fichier draw.io est illisible`);
  }
  const infle = await inflerBrut(octets, budget.reste);
  budget.reste -= infle.length;
  let xml = "";
  for (let debut = 0; debut < infle.length; debut += 0x8000) {
    xml += String.fromCharCode(...infle.subarray(debut, debut + 0x8000));
  }
  const page = lireXml(decoderAdresse(xml));
  if (page.nom !== "mxGraphModel") throw new Error(`la page « ${nom} » du fichier draw.io n'est pas un dessin`);
  return page;
}

/** Les anciens fichiers ne sont pas encodés : un texte qui ne se décode pas est pris tel quel. */
function decoderAdresse(texte: string): string {
  try {
    return decodeURIComponent(texte);
  } catch {
    return texte;
  }
}

async function inflerBrut(octets: Uint8Array, maximum = DECOMPRESSE_MAX): Promise<Uint8Array> {
  if (typeof DecompressionStream === "undefined") {
    throw new Error("cette page draw.io est compressée, et ce moteur ne sait pas la décompresser");
  }
  const lecteur = new Blob([octets as BlobPart]).stream().pipeThrough(new DecompressionStream("deflate-raw")).getReader();
  const morceaux: Uint8Array[] = [];
  let total = 0;
  try {
    for (;;) {
      const { done, value } = await lecteur.read();
      if (done) break;
      total += value.length;
      if (total > maximum) {
        await lecteur.cancel();
        throw new Error("la page compressée du fichier draw.io est trop grande");
      }
      morceaux.push(value);
    }
  } catch (erreur) {
    throw erreur instanceof Error && erreur.message.includes("trop grande")
      ? erreur
      : new Error("la page compressée du fichier draw.io est illisible");
  }
  const resultat = new Uint8Array(total);
  let position = 0;
  for (const morceau of morceaux) {
    resultat.set(morceau, position);
    position += morceau.length;
  }
  return resultat;
}

/** Les formes de base de draw.io : un cadre ou un texte, pas un équipement. */
const BASIQUES = new Set([
  "",
  "rect",
  "rectangle",
  "label",
  "ellipse",
  "doubleEllipse",
  "text",
  "rhombus",
  "triangle",
  "hexagon",
  "process",
  "parallelogram",
  "trapezoid",
  "note",
  "card",
  "document",
  "step",
  "callout",
  "cylinder",
  "cylinder3",
  "swimlane",
  "partialRectangle",
  "line",
  "actor",
  "umlActor",
  "tape",
  "datastore",
  "dataStorage",
  "internalStorage",
  "mxgraph.basic.rect",
]);

type Nature =
  | { genre: "equipement"; type: string; inconnu: boolean }
  | { genre: "annotation"; forme: Exclude<FormeAnnotation, "fleche" | "note"> };

/** Ce que devient une forme : un équipement (type des données, sinon deviné à sa forme), ou une annotation. */
function natureDe(c: Cellule): Nature {
  if (typeConnu(c.donnees.type ?? "")) return { genre: "equipement", type: c.donnees.type ?? "", inconnu: false };
  const forme = c.style.shape ?? c.jetons.find((j) => j.startsWith("mxgraph.") || j === "image") ?? "";
  if (forme === "image" || !BASIQUES.has(forme)) {
    const image = c.style.image ?? "";
    const indices = forme === "image" ? (image.startsWith("data:") ? "" : image) : [forme, c.style.resIcon, c.style.prIcon, c.style.grIcon].filter(Boolean).join(" ");
    const type = indices ? typeDevine(indices) : undefined;
    return type ? { genre: "equipement", type, inconnu: false } : { genre: "equipement", type: "generique", inconnu: true };
  }
  const sansTrait = c.style.strokeColor === "none" && (c.style.fillColor ?? "none") === "none";
  if (c.jetons.includes("text") || forme === "text" || sansTrait) return { genre: "annotation", forme: "texte" };
  if (c.jetons.includes("ellipse") || forme === "ellipse" || forme === "doubleEllipse") return { genre: "annotation", forme: "ellipse" };
  return { genre: "annotation", forme: "rectangle" };
}

/** Un câble coudé dans draw.io : connecteur orthogonal ou en équerre, ou courbe (redressé). */
const estCoudeDrawio = (c: Cellule) => /orthogonal|elbow|entityRelation|isometric/i.test(c.style.edgeStyle ?? "") || c.style.curved === "1";
const couleurHex = (valeur: string | undefined) => (valeur && /^#[0-9a-f]{6}$/i.test(valeur) ? valeur.toLowerCase() : undefined);

/** La page : ses cellules, et de quoi situer chacune (draw.io place un enfant par rapport à son conteneur). */
class Page {
  readonly parId: Map<string, Cellule>;
  readonly #origines = new Map<string, Point>();

  constructor(readonly cellules: Cellule[]) {
    this.parId = new Map(cellules.map((c) => [c.id, c]));
  }

  /** Une couche (ou la racine) : ce qui n'est dans aucune forme. */
  estCouche(c: Cellule): boolean {
    return !c.parent || c.parent === "0" || (!c.sommet && !c.arete && !this.parId.get(c.parent)?.sommet);
  }

  /** L'étiquette d'un connecteur (un nom de port, par exemple). */
  estEtiquette(c: Cellule): boolean {
    return this.parId.get(c.parent)?.arete === true;
  }

  /** Le point d'où se comptent les coordonnées des enfants de `id`. */
  origine(id: string, vus = new Set<string>()): Point {
    const connue = this.#origines.get(id);
    if (connue) return connue;
    const c = this.parId.get(id);
    if (!c?.sommet || vus.has(id)) return { x: 0, y: 0 };
    vus.add(id);
    const dessus = this.origine(c.parent, vus);
    const resultat = { x: dessus.x + c.x, y: dessus.y + c.y };
    this.#origines.set(id, resultat);
    return resultat;
  }

  coin(c: Cellule): Point {
    const o = this.origine(c.parent);
    return { x: o.x + c.x, y: o.y + c.y };
  }

  centre(c: Cellule): Point {
    const coin = this.coin(c);
    return { x: coin.x + c.largeur / 2, y: coin.y + c.hauteur / 2 };
  }

  points(c: Cellule): Point[] {
    const o = this.origine(c.parent);
    return c.points.map((p) => arrondi({ x: p.x + o.x, y: p.y + o.y }));
  }

  /** Les ancêtres d'une cellule, du plus proche au plus lointain. */
  ancetres(c: Cellule): Cellule[] {
    const resultat: Cellule[] = [];
    let courant = this.parId.get(c.parent);
    while (courant?.sommet && !resultat.includes(courant)) {
      resultat.push(courant);
      courant = this.parId.get(courant.parent);
    }
    return resultat;
  }
}

/** Une page du fichier draw.io : son nom, ses cellules, et la page de kyfRev qu'elle porte (export de kyfRev). */
type Feuille = { nom: string; page: Page; pageKyfrev?: string };

/** Un nom de page que draw.io donne de lui-même (« Page-1 ») ne dit rien du schéma. */
const nomParDefaut = (nom: string) => /^(page|seite|página)[-\s]?\d+$/i.test(nom);

async function lireDrawio(texte: string, nomFichier: string): Promise<{ schema: Schema; remarques: Remarques }> {
  const racine = lireXml(texte);
  const dessins: { nom: string; dessin: ElementXml }[] = [];
  if (racine.nom === "mxGraphModel") {
    dessins.push({ nom: "", dessin: racine });
  } else if (racine.nom === "mxfile") {
    const diagrammes = enfantsNommes(racine, "diagram");
    if (diagrammes.length === 0) throw new Error("ce fichier draw.io n'a aucune page");
    if (diagrammes.length > PAGES_MAX) throw new Error(`ce fichier draw.io a plus de ${PAGES_MAX} pages`);
    const budget = { reste: DECOMPRESSE_MAX };
    for (const [rang, d] of diagrammes.entries()) {
      const nom = d.attributs.name?.trim() ?? "";
      const dessin = enfantsNommes(d, "mxGraphModel")[0] ?? (await pageCompressee(d.texte, nom || `Page ${rang + 1}`, budget));
      dessins.push({ nom, dessin });
    }
  } else {
    throw new Error("ce n'est pas un fichier draw.io (élément « mxfile » attendu)");
  }
  const feuilles: Feuille[] = dessins.map(({ nom, dessin }) => {
    const contenu = enfantsNommes(dessin, "root")[0];
    if (!contenu) throw new Error(nom ? `la page « ${nom} » du fichier draw.io n'a pas de contenu` : "la page du fichier draw.io n'a pas de contenu");
    const page = new Page(lireCellules(contenu));
    const pageKyfrev = page.cellules.find((c) => page.estCouche(c) && c.donnees.kyfrevPage)?.donnees.kyfrevPage;
    return { nom, page, ...(pageKyfrev ? { pageKyfrev } : {}) };
  });
  const remarques: Remarques = { inconnus: [], courbes: 0, sansExtremite: 0, pages: feuilles.length };
  // Une seule page, qui n'est pas une page d'un schéma de kyfRev : son nom est celui du schéma (comme à l'export).
  const seule = feuilles.length === 1 && feuilles[0]?.pageKyfrev === undefined ? feuilles[0] : undefined;
  const nomVoulu = seule && !nomParDefaut(seule.nom) ? seule.nom.slice(0, 200) : "";

  const modele = feuilles.flatMap((f) => f.page.cellules).find((c) => c.donnees.kyfrev)?.donnees.kyfrev;
  const repris = modele ? reprendreLeModele(modele, feuilles, remarques) : null;
  if (repris) {
    if (nomVoulu) repris.schema.titre = nomVoulu;
    traduireLesFeuilles(repris.schema, feuilles, repris.pages, repris.vues, remarques);
    return { schema: repris.schema, remarques };
  }
  const schema = schemaVide(nomVoulu || nomFichier.slice(0, 200) || TITRE_PAR_DEFAUT);
  traduireLesFeuilles(
    schema,
    feuilles,
    feuilles.map(() => ({ id: nouvelId("p") })),
    feuilles.map(() => new Set<string>()),
    remarques,
  );
  return { schema, remarques };
}

/**
 * Chaque page du fichier devient une page du schéma (une seule : le schéma n'en déclare pas plus que le modèle) :
 * ses cellules neuves sont traduites et posées sur elle.
 */
function traduireLesFeuilles(
  schema: Schema,
  feuilles: Feuille[],
  pages: { id: string; nom?: string }[],
  vues: Set<string>[],
  remarques: Remarques,
): void {
  const plusieurs = feuilles.length > 1;
  if (plusieurs) {
    schema.pages = feuilles.map((f, rang) => ({
      id: pages[rang]?.id ?? nouvelId("p"),
      nom: (f.nom.trim() || pages[rang]?.nom || `Page ${rang + 1}`).slice(0, NOM_PAGE_MAX),
    }));
  } else if (feuilles[0]?.pageKyfrev !== undefined && schema.pages && pages[0]) {
    // Une page d'un schéma qui en avait plusieurs, seule restée dans draw.io : elle garde son nom.
    schema.pages = [{ id: pages[0].id, nom: (feuilles[0].nom.trim() || pages[0].nom || "Page 1").slice(0, NOM_PAGE_MAX) }];
  }
  feuilles.forEach((f, rang) => {
    const avant = elementsDuSchema(schema);
    traduire(schema, f.page, vues[rang] ?? new Set(), remarques);
    if (!plusieurs) return;
    const page = schema.pages?.[rang]?.id;
    for (const element of [...schema.equipements, ...schema.groupes, ...schema.annotations]) {
      if (!avant.has(element.id)) element.page = page;
    }
  });
}

/**
 * Le modèle gardé dans le fichier, avec ce qui a changé dans draw.io : places et noms, formes, connecteurs et
 * pages retirés. Chaque page du modèle se retrouve dans la page du fichier qui la porte (la première, pour un
 * schéma d'une page) ; un câble entre deux pages se garde tant que ses deux équipements restent. Rend les
 * cellules ainsi reprises, page par page (le reste est neuf), et la page de chaque feuille ; rien si le modèle
 * est illisible.
 */
function reprendreLeModele(
  json: string,
  feuilles: Feuille[],
  remarques: Remarques,
): { schema: Schema; vues: Set<string>[]; pages: { id: string; nom?: string }[] } | null {
  let schema: Schema;
  try {
    const lecture = lireSchema(json);
    if (lecture.erreurs.length > 0) return null;
    schema = lecture.schema;
  } catch {
    return null;
  }
  const pagesDuModele = pagesDe(schema);
  // La feuille qui porte chaque page du modèle : la première qui la nomme (une page dupliquée dans draw.io garde
  // ce nom, mais ses cellules sont neuves) ; sans nom, la première feuille porte la première page.
  const rangDeLaPage = new Map<string, number>();
  feuilles.forEach((f, rang) => {
    const voulue = f.pageKyfrev ?? (rang === 0 && !aPlusieursPages(schema) ? pagesDuModele[0]?.id : undefined);
    if (voulue !== undefined && pagesDuModele.some((p) => p.id === voulue) && !rangDeLaPage.has(voulue)) rangDeLaPage.set(voulue, rang);
  });
  const pages = feuilles.map((_, rang) => {
    const portee = pagesDuModele.find((p) => rangDeLaPage.get(p.id) === rang);
    return portee && portee.id !== "" ? { id: portee.id, nom: portee.nom } : { id: nouvelId("p"), ...(portee ? { nom: portee.nom } : {}) };
  });
  const vues = feuilles.map(() => new Set<string>());
  /** La feuille d'un élément du modèle ; rien si sa page a été retirée dans draw.io. */
  const rangDe = (element: { page?: string }) => rangDeLaPage.get(pageDeLElement(schema, element));
  const prise = (rang: number | undefined, id: string, quoi: "sommet" | "arete") => {
    if (rang === undefined) return undefined;
    const c = feuilles[rang]?.page.parId.get(id);
    if (!c?.[quoi]) return undefined;
    vues[rang]?.add(id);
    return c;
  };

  const retires = new Set<string>();
  const rangDeLEquipement = new Map<string, number>();
  for (const e of schema.equipements) {
    const rang = rangDe(e);
    const c = prise(rang, e.id, "sommet");
    const page = rang === undefined ? undefined : feuilles[rang]?.page;
    if (!c || !page || rang === undefined) {
      retires.add(e.id);
      continue;
    }
    rangDeLEquipement.set(e.id, rang);
    e.position = arrondi(page.centre(c));
    const nom = premiereLigne(c);
    if (nom) e.nom = nom.slice(0, 120);
  }
  retirerEquipements(schema, retires);

  const memesBouts = (c: Cellule, de: string, vers: string) =>
    (c.source === de && c.cible === vers) || (c.source === vers && c.cible === de);
  /** La feuille commune à deux équipements ; `null` s'ils sont sur deux pages (un renvoi). */
  const feuilleCommune = (de: string, vers: string) => {
    const a = rangDeLEquipement.get(de);
    return a !== undefined && a === rangDeLEquipement.get(vers) ? a : null;
  };
  schema.liens = schema.liens.filter((l) => {
    const rang = feuilleCommune(l.a.equipement, l.b.equipement);
    // Un câble entre deux pages : draw.io n'en montre que les renvois ; il reste tant que ses deux bouts restent.
    if (rang === null) return true;
    const page = feuilles[rang]?.page;
    const c = page?.parId.get(l.id);
    // Rebranché ailleurs dans draw.io : l'ancien câble part, le connecteur revient comme un câble neuf.
    if (!page || !c?.arete || !memesBouts(c, l.a.equipement, l.b.equipement)) return false;
    vues[rang]?.add(c.id);
    l.points = page.points(c);
    if (c.style.curved === "1") remarques.courbes++;
    const coude = estCoudeDrawio(c);
    if (coude !== estCoude(schema, l)) l.forme = coude ? "coudee" : "droite";
    const nom = premiereLigne(c);
    if (nom) l.nom = nom;
    else delete l.nom;
    return true;
  });
  schema.relations = schema.relations.filter((r) => {
    const rang = feuilleCommune(r.de, r.vers);
    if (rang === null) return true;
    const page = feuilles[rang]?.page;
    const c = page?.parId.get(r.id);
    if (!page || !c?.arete || !memesBouts(c, r.de, r.vers)) return false;
    vues[rang]?.add(c.id);
    r.points = page.points(c);
    const nom = premiereLigne(c);
    if (nom) r.nom = nom;
    else delete r.nom;
    return true;
  });

  schema.groupes = schema.groupes.filter((g) => {
    const rang = rangDe(g);
    const c = prise(rang, g.id, "sommet");
    const page = rang === undefined ? undefined : feuilles[rang]?.page;
    if (!c || !page) return false;
    g.position = arrondi(page.coin(c));
    g.taille = { largeur: Math.max(40, Math.round(c.largeur)), hauteur: Math.max(40, Math.round(c.hauteur)) };
    g.nom = premiereLigne(c).slice(0, 120);
    return true;
  });
  const groupes = new Set(schema.groupes.map((g) => g.id));
  for (const g of schema.groupes) if (g.parent && !groupes.has(g.parent)) delete g.parent;

  schema.annotations = schema.annotations.filter((n) => {
    const rang = rangDe(n);
    const page = rang === undefined ? undefined : feuilles[rang]?.page;
    const c = page?.parId.get(n.id);
    if (!c || !page || rang === undefined) return false;
    if (n.forme === "fleche") {
      if (!c.arete || !c.depart || !c.arrivee) return false;
      const o = page.origine(c.parent);
      n.position = arrondi({ x: c.depart.x + o.x, y: c.depart.y + o.y });
      n.vers = arrondi({ x: c.arrivee.x + o.x, y: c.arrivee.y + o.y });
    } else {
      if (!c.sommet) return false;
      const cadre = cadreAnnotation(n);
      const coin = page.coin(c);
      n.position = arrondi({ x: n.position.x + coin.x - cadre.x, y: n.position.y + coin.y - cadre.y });
      // Une taille ne s'écrit que si elle a changé : une note sans largeur garde la sienne par défaut.
      const largeur = Math.max(40, Math.round(c.largeur));
      const hauteur = Math.max(20, Math.round(c.hauteur));
      if (n.forme !== "texte" && largeur !== Math.round(cadre.largeur)) n.largeur = largeur;
      if ((n.forme === "rectangle" || n.forme === "ellipse") && hauteur !== Math.round(cadre.hauteur)) n.hauteur = hauteur;
    }
    vues[rang]?.add(c.id);
    n.texte = libelle(c);
    return true;
  });

  // Chaque élément repris est sur la page de sa feuille (une page neuve, si la sienne n'avait pas d'identifiant).
  if (feuilles.length > 1) {
    for (const element of [...schema.equipements, ...schema.groupes, ...schema.annotations]) {
      const rang = rangDe(element);
      if (rang !== undefined) element.page = pages[rang]?.id;
    }
  }
  return { schema, vues, pages };
}

/** Traduit les cellules qui ne sont pas encore reprises : formes, conteneurs, puis connecteurs. */
function traduire(schema: Schema, page: Page, vues: Set<string>, remarques: Remarques): void {
  // Les renvois vers une autre page (pastille et trait), écrits par kyfRev : le câble qu'ils montrent vient du modèle.
  const renvois = new Set(page.cellules.filter((c) => c.donnees.renvoi).map((c) => c.id));
  const neuves = page.cellules.filter(
    (c) =>
      !vues.has(c.id) &&
      !page.estCouche(c) &&
      !page.estEtiquette(c) &&
      !renvois.has(c.id) &&
      !renvois.has(c.source) &&
      !renvois.has(c.cible),
  );
  const parents = new Set(page.cellules.filter((c) => c.sommet).map((c) => c.parent));
  const estConteneur = (c: Cellule) =>
    c.sommet &&
    !typeConnu(c.donnees.type ?? "") &&
    (c.style.container === "1" || c.jetons.includes("swimlane") || c.jetons.includes("group") || c.style.shape === "swimlane" || parents.has(c.id));

  // Les zones d'abord : les équipements s'y rangent.
  const groupes = new Map<string, Groupe>(schema.groupes.map((g) => [g.id, g]));
  const conteneurs = neuves.filter(estConteneur);
  for (const c of conteneurs) {
    const g: Groupe = {
      id: nouvelId("g"),
      nom: premiereLigne(c).slice(0, 120),
      genre: "zone",
      membres: [],
      position: arrondi(page.coin(c)),
      taille: { largeur: Math.max(40, Math.round(c.largeur)), hauteur: Math.max(40, Math.round(c.hauteur)) },
      ...(couleurHex(c.style.strokeColor) ? { couleur: couleurHex(c.style.strokeColor) } : {}),
    };
    groupes.set(c.id, g);
    schema.groupes.push(g);
  }
  const zoneDe = (c: Cellule) => page.ancetres(c).map((a) => groupes.get(a.id)).find(Boolean);
  for (const c of conteneurs) {
    const g = groupes.get(c.id);
    const dessus = zoneDe(c);
    if (g && dessus) g.parent = dessus.id;
  }

  // Les formes : équipements et annotations.
  const equipements = new Map<string, string>(schema.equipements.map((e) => [e.id, e.id]));
  for (const c of neuves) {
    if (!c.sommet || estConteneur(c)) continue;
    const nature = natureDe(c);
    if (nature.genre === "equipement") {
      const e = creerEquipement(schema, nature.type, arrondi(page.centre(c)));
      const nom = premiereLigne(c) || c.donnees.nom;
      if (nom) e.nom = nom.slice(0, 120);
      const adresse = c.donnees.ip?.trim();
      const vlan = Number(c.donnees.vlan);
      if (adresse || (Number.isInteger(vlan) && vlan >= 1 && vlan <= 4094)) {
        e.gestion = { ...(adresse ? { adresse } : {}), ...(Number.isInteger(vlan) && vlan >= 1 && vlan <= 4094 ? { vlan } : {}) };
      }
      if (c.donnees.modele?.trim()) e.reference = c.donnees.modele.trim();
      if (c.donnees.site?.trim()) e.site = c.donnees.site.trim();
      if (c.donnees.salle?.trim()) e.salle = c.donnees.salle.trim();
      equipements.set(c.id, e.id);
      if (nature.inconnu) remarques.inconnus.push(e.id);
      zoneDe(c)?.membres.push(e.id);
      continue;
    }
    const coin = arrondi(page.coin(c));
    const teinte = couleurHex(nature.forme === "texte" ? c.style.fontColor : c.style.strokeColor);
    const annotation: Annotation = {
      id: nouvelId("n"),
      texte: libelle(c),
      position: coin,
      forme: nature.forme,
      ...(teinte ? { couleur: teinte } : {}),
    };
    if (nature.forme === "texte") {
      const taille = Number(c.style.fontSize);
      if (Number.isFinite(taille) && taille > 0) annotation.taille = Math.min(72, Math.max(10, Math.round(taille)));
    } else {
      annotation.largeur = Math.max(40, Math.round(c.largeur));
      annotation.hauteur = Math.max(20, Math.round(c.hauteur));
    }
    schema.annotations.push(annotation);
  }

  // Les connecteurs : un câble entre deux équipements, une flèche entre deux points, rien sans extrémité.
  let numero = schema.liens.length;
  for (const c of neuves) {
    if (!c.arete) continue;
    const bout = (id: string, libre: Point | undefined) => {
      const forme = page.parId.get(id);
      const o = page.origine(c.parent);
      return {
        equipement: equipements.get(id),
        point: forme?.sommet ? page.centre(forme) : libre ? { x: libre.x + o.x, y: libre.y + o.y } : undefined,
      };
    };
    const de = bout(c.source, c.depart);
    const vers = bout(c.cible, c.arrivee);
    if (de.equipement && vers.equipement && de.equipement !== vers.equipement) {
      relier(schema, page, c, de.equipement, vers.equipement, ++numero, remarques);
    } else if (de.point && vers.point && !(de.equipement && de.equipement === vers.equipement)) {
      schema.annotations.push({
        id: nouvelId("n"),
        texte: libelle(c),
        position: arrondi(de.point),
        forme: "fleche",
        vers: arrondi(vers.point),
        ...(couleurHex(c.style.strokeColor) ? { couleur: couleurHex(c.style.strokeColor) } : {}),
      });
    } else {
      remarques.sansExtremite++;
    }
  }
}

/** Un connecteur entre deux équipements : une relation si ses données le disent, sinon un câble. */
function relier(schema: Schema, page: Page, c: Cellule, de: string, vers: string, numero: number, remarques: Remarques): void {
  const nom = premiereLigne(c);
  const genre = GENRES_RELATION.find((g) => g.valeur === c.donnees.genre)?.valeur;
  if (genre) {
    schema.relations.push({
      id: nouvelId("r"),
      genre,
      de,
      vers,
      ...(nom ? { nom } : {}),
      statut: "actif",
      proprietes: {},
      points: page.points(c),
    });
    return;
  }
  // Les ports : les étiquettes du connecteur, celle du départ avant le milieu, celle de l'arrivée après.
  const etiquettes = page.cellules.filter((e) => e.parent === c.id && e.sommet);
  const port = (cote: number) => {
    const etiquette = etiquettes.find((e) => Math.sign(e.x) === cote);
    return etiquette ? premiereLigne(etiquette).slice(0, 80) : "";
  };
  const support = SUPPORTS.find((s) => s.valeur === c.donnees.support)?.valeur ?? "cuivre";
  const genreInterface = support.startsWith("fibre") ? "fibre" : "cuivre";
  const equipementA = schema.equipements.find((e) => e.id === de);
  const equipementB = schema.equipements.find((e) => e.id === vers);
  if (!equipementA || !equipementB) return;
  if (c.style.curved === "1") remarques.courbes++;
  const coude = estCoudeDrawio(c);
  const trait = c.style.dashed === "1" ? (/^1\s/.test(c.style.dashPattern ?? "") ? "points" : "tirets") : undefined;
  const couleur = couleurHex(c.style.strokeColor);
  const lien: Lien = {
    id: nouvelId("l"),
    a: boutDeLien(schema, equipementA, port(-1), numero, genreInterface),
    b: boutDeLien(schema, equipementB, port(1), numero, genreInterface),
    support,
    statut: "actif",
    ...(nom ? { nom } : {}),
    ...(couleur ? { couleur } : {}),
    ...(trait ? { trait } : {}),
    ...(coude ? { forme: "coudee" as const } : schema.cablesCoudes ? { forme: "droite" as const } : {}),
    points: page.points(c),
  };
  schema.liens.push(lien);
}
