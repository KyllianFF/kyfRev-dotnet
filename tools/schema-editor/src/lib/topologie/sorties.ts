// Sorties d'un schéma réseau (D40, D39) : export (PNG, SVG, draw.io, JSON), partage (l'image va dans le
// presse-papiers) et impression. L'éditeur, la liste des schémas et le détail d'un schéma s'en servent.
// Ce qui sort est toujours clair, avec sa légende, quel que soit le thème de l'écran.

import { api } from "../api";
import { dessinerSchema } from "./dessin";
import { ecrireDrawio, type IconesDrawio } from "./drawio";
import { ecrireSchema } from "./format";
import type { Schema, Vue } from "./modele";
import { aPlusieursPages, nomDeLaPage, pageValide } from "./pages";

export type FormatExport = "png" | "svg" | "drawio" | "json";

/** Les formats proposés à l'export ; une image demande un schéma qui a au moins un équipement. */
export const FORMATS_EXPORT: { valeur: FormatExport; nom: string; aide: string; image: boolean }[] = [
  { valeur: "png", nom: "Image PNG", aide: "Pour un message, un document ou une fiche.", image: true },
  { valeur: "svg", nom: "Image SVG", aide: "Vectorielle : nette à toute taille.", image: true },
  { valeur: "drawio", nom: "draw.io", aide: "À retoucher dans draw.io, ou dans Visio en passant par draw.io.", image: false },
  { valeur: "json", nom: "Modèle complet", aide: "JSON, à réimporter dans kyfRev.", image: false },
];

/** Un format d'image : il demande un schéma qui a au moins un équipement, et sort tel qu'on le voit. */
export const estImage = (format: FormatExport): boolean => FORMATS_EXPORT.some((f) => f.valeur === format && f.image);

const NOM_PAR_DEFAUT = "Schéma réseau";
const NOMS_RESERVES = /^(con|prn|aux|nul|com\d|lpt\d)$/i;
/** Côté le plus long d'une image PNG, en pixels. */
const COTE_MAX_PNG = 8000;
/** Côté le plus long de l'image à imprimer, au moins : de quoi rester net sur une feuille A4 à 300 points par pouce. */
const COTE_IMPRESSION = 3600;

/** Dans la fenêtre d'export, quand le coffre est chiffré : dit avant que le fichier ne soit écrit. */
export const AVIS_EXPORT_EN_CLAIR =
  "Ce coffre est chiffré, mais pas ce que vous exportez : le fichier créé est en clair, lisible par quiconque y accède.";

/** Ce qui vient de sortir du coffre sans passer par la fenêtre d'export. */
export type Sortie = "fichier" | "image";

/** La phrase qui suit l'annonce d'une sortie quand le coffre est chiffré ; rien sinon. */
export const avisEnClair = (chiffre: boolean, sortie: Sortie): string =>
  !chiffre
    ? ""
    : sortie === "image"
      ? " Ce coffre est chiffré, pas cette image : dans le presse-papiers, elle est en clair."
      : " Ce coffre est chiffré, pas ce fichier : il est en clair.";

/**
 * Le coffre ouvert est-il chiffré ? Demandé à l'application à chaque sortie : le coffre peut avoir changé depuis l'ouverture
 * de la vue. Sans réponse, rien n'est annoncé : l'export, lui, ne doit pas échouer pour un avis.
 */
export async function coffreChiffre(): Promise<boolean> {
  try {
    return (await api.coffreChiffre()) === true;
  } catch {
    return false;
  }
}

/** Un schéma sans équipement n'a rien à montrer : ni image, ni page à imprimer. */
export const estVide = (schema: Schema): boolean => schema.equipements.length === 0;

/**
 * Nom de fichier sûr depuis le titre : sans caractère interdit par Windows, sans point ni espace à la fin,
 * 80 caractères au plus. L'application refait ce ménage de son côté ; ici, le nom montré est déjà le bon.
 */
export function nomDeFichier(titre: string, extension: string): string {
  const propre = titre
    .replace(/:/g, " -")
    .replace(/"/g, "'")
    .replace(/[/\\|]/g, "-")
    .replace(/[<>?*]/g, "")
    .replace(/[\p{Cc}\s]+/gu, " ")
    .trim();
  const coupe = [...propre]
    .slice(0, 80)
    .join("")
    .replace(/[. ]+$/, "");
  const nom = coupe === "" ? NOM_PAR_DEFAUT : NOMS_RESERVES.test(coupe) ? `${NOM_PAR_DEFAUT} ${coupe}` : coupe;
  return `${nom}.${extension}`;
}

/** Échelle d'une image PNG : deux fois la taille du dessin, moins si un côté dépasserait 8000 pixels. */
export function echellePng(largeur: number, hauteur: number): number {
  return Math.min(2, COTE_MAX_PNG / Math.max(largeur, hauteur, 1));
}

/** Les couleurs du thème remplacées par leur valeur claire par défaut : le SVG ne dépend plus de la page. */
export const couleursClaires = (svg: string): string => svg.replace(/var\(--[\w-]+,\s*([^)]+)\)/g, "$1");

/** SVG autonome d'un schéma : couleurs claires, légende. Sans `vue`, celle du schéma ; sans `page`, sa première page. */
export function svgAutonome(schema: Schema, vue?: Vue, page?: string): string {
  return couleursClaires(dessinerSchema(schema, { ...(vue ? { vue } : {}), legende: true, ...(page !== undefined ? { page } : {}) }));
}

/** Le titre d'une image sortie d'un schéma à plusieurs pages porte le nom de sa page (« Réseau - Étage 2 »). */
export function titreDeLaSortie(schema: Schema, page?: string): string {
  const titre = schema.titre || NOM_PAR_DEFAUT;
  return aPlusieursPages(schema) ? `${titre} - ${nomDeLaPage(schema, pageValide(schema, page))}` : titre;
}

/** Taille déclarée par un SVG (attributs `width` et `height` de sa racine). */
export function dimensionsSvg(svg: string): { largeur: number; hauteur: number } {
  const racine = /^\s*<svg\b[^>]*>/.exec(svg)?.[0] ?? "";
  const nombre = (attribut: string) => Number(new RegExp(`\\s${attribut}="([\\d.]+)"`).exec(racine)?.[1] ?? 0) || 0;
  return { largeur: nombre("width"), hauteur: nombre("height") };
}

/**
 * Échelle de l'image à imprimer : assez grande pour que le côté le plus long fasse 3600 pixels (deux fois la
 * taille du dessin au moins), sans dépasser 8000 pixels.
 */
export function echelleImpression(largeur: number, hauteur: number): number {
  const cote = Math.max(largeur, hauteur, 1);
  return Math.min(COTE_MAX_PNG / cote, Math.max(2, COTE_IMPRESSION / cote));
}

/** Durée d'affichage d'un message de réussite : 5 s, plus selon sa longueur, 12 s au plus. */
export function dureeAnnonce(texte: string): number {
  return Math.min(12000, 5000 + texte.length * 50);
}

/** Image PNG (fond blanc, deux fois la taille par défaut) d'un SVG. */
export async function enPng(svg: string, echelleDe = echellePng): Promise<Uint8Array> {
  const image = new Image();
  image.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
  await image.decode();
  const toile = document.createElement("canvas");
  const echelle = echelleDe(image.width, image.height);
  toile.width = Math.round(image.width * echelle);
  toile.height = Math.round(image.height * echelle);
  const contexte = toile.getContext("2d");
  if (!contexte) throw new Error("dessin impossible");
  contexte.fillStyle = "#ffffff";
  contexte.fillRect(0, 0, toile.width, toile.height);
  contexte.drawImage(image, 0, 0, toile.width, toile.height);
  const blob = await new Promise<Blob | null>((ok) => toile.toBlob(ok, "image/png"));
  if (!blob) throw new Error("image vide");
  return new Uint8Array(await blob.arrayBuffer());
}

/** Les octets d'un export, dans le format voulu : une image montre une page, draw.io et le modèle les gardent toutes. */
async function octetsDe(schema: Schema, format: FormatExport, vue?: Vue, icones?: IconesDrawio, page?: string): Promise<Uint8Array> {
  if (format === "json") return new TextEncoder().encode(ecrireSchema(schema));
  if (format === "drawio") return new TextEncoder().encode(ecrireDrawio(schema, icones, vue));
  if (format === "svg") return new TextEncoder().encode(svgAutonome(schema, vue, page));
  return enPng(svgAutonome(schema, vue, page));
}

/**
 * Demande un dossier, y écrit l'export, et rend le chemin du fichier écrit (l'application n'écrase rien).
 * Rend `null` si le choix du dossier a été annulé : rien n'est écrit. Une image d'un schéma à plusieurs pages
 * montre `page` (sinon la première) et porte son nom.
 */
export async function exporterSchema(
  schema: Schema,
  format: FormatExport,
  vue?: Vue,
  icones?: IconesDrawio,
  page?: string,
): Promise<string | null> {
  const dossier = await api.choisirDossier("Dossier où enregistrer l'export du schéma");
  if (!dossier) return null;
  const titre = estImage(format) ? titreDeLaSortie(schema, page) : schema.titre;
  return api.exporterSchema(dossier, nomDeFichier(titre, format), await octetsDe(schema, format, vue, icones, page));
}

/**
 * Plusieurs schémas d'un coup (31B) : le dossier se demande une fois, puis chaque schéma y devient son propre fichier,
 * sous son titre (l'application n'écrase rien : deux titres pareils donnent deux fichiers). Une image montre la première
 * page, et ne sort pas d'un schéma vide, qui est sauté. Rend le dossier, les fichiers écrits et les schémas sautés, ou
 * `null` si le choix du dossier a été annulé : rien n'est écrit.
 */
export async function exporterSchemas(
  schemas: readonly Schema[],
  format: FormatExport,
  icones?: IconesDrawio,
): Promise<{ dossier: string; ecrits: number; sautes: number } | null> {
  const dossier = await api.choisirDossier("Dossier où enregistrer l'export des schémas");
  if (!dossier) return null;
  let ecrits = 0;
  let sautes = 0;
  for (const schema of schemas) {
    if (estImage(format) && estVide(schema)) {
      sautes++;
      continue;
    }
    const titre = estImage(format) ? titreDeLaSortie(schema) : schema.titre;
    await api.exporterSchema(dossier, nomDeFichier(titre, format), await octetsDe(schema, format, undefined, icones));
    ecrits++;
  }
  return { dossier, ecrits, sautes };
}

/** Copie l'image PNG du schéma (sa première page, ou `page`) dans le presse-papiers, prête à être collée ailleurs. */
export async function partagerSchema(schema: Schema, vue?: Vue, page?: string): Promise<void> {
  await api.copierImageSchema(await enPng(svgAutonome(schema, vue, page)));
}

/**
 * Imprime le schéma seul par la fenêtre « Imprimer » de l'application, comme une fiche : son titre et son
 * dessin, en image assez fine pour le papier, ajustés à la feuille. L'imprimante, le papier, les marges et
 * la couleur (ou le noir et blanc) s'y choisissent, avec l'aperçu. Rend ce qu'il faut dire du travail parti, ou rien.
 * Un schéma à plusieurs pages imprime `page` (sinon la première), son nom dans le titre.
 */
export async function imprimerSchema(schema: Schema, vue?: Vue, page?: string): Promise<string | null> {
  return api.imprimerSchema(titreDeLaSortie(schema, page), await enPng(svgAutonome(schema, vue, page), echelleImpression));
}
