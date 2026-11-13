// Sorties d'un schéma réseau (D40, D39) : export (PNG, SVG, JSON), partage (l'image va dans le
// presse-papiers) et impression. L'éditeur, la liste des schémas et le détail d'un schéma s'en servent.
// Ce qui sort est toujours clair, avec sa légende, quel que soit le thème de l'écran.

import { api } from "../api";
import { descriptionDuSchema, dessinerSchema } from "./dessin";
import { ecrireSchema } from "./format";
import type { Schema, Vue } from "./modele";

export type FormatExport = "png" | "svg" | "json";

/** Les formats proposés à l'export ; une image demande un schéma qui a au moins un équipement. */
export const FORMATS_EXPORT: { valeur: FormatExport; nom: string; aide: string; image: boolean }[] = [
  { valeur: "png", nom: "Image PNG", aide: "Pour un message, un document ou une fiche.", image: true },
  { valeur: "svg", nom: "Image SVG", aide: "Vectorielle : nette à toute taille.", image: true },
  { valeur: "json", nom: "Modèle complet", aide: "JSON, à réimporter dans kyfRev.", image: false },
];

const NOM_PAR_DEFAUT = "Schéma réseau";
const NOMS_RESERVES = /^(con|prn|aux|nul|com\d|lpt\d)$/i;
/** Côté le plus long d'une image PNG, en pixels. */
const COTE_MAX_PNG = 8000;

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

/** SVG autonome d'un schéma : couleurs claires, légende. Sans `vue`, celle du schéma. */
export function svgAutonome(schema: Schema, vue?: Vue): string {
  return couleursClaires(dessinerSchema(schema, { ...(vue ? { vue } : {}), legende: true }));
}

/** Taille déclarée par un SVG (attributs `width` et `height` de sa racine). */
export function dimensionsSvg(svg: string): { largeur: number; hauteur: number } {
  const racine = /^\s*<svg\b[^>]*>/.exec(svg)?.[0] ?? "";
  const nombre = (attribut: string) => Number(new RegExp(`\\s${attribut}="([\\d.]+)"`).exec(racine)?.[1] ?? 0) || 0;
  return { largeur: nombre("width"), hauteur: nombre("height") };
}

/** Sens de la page à l'impression : paysage si le dessin est plus large que haut. */
export function orientationPage(largeur: number, hauteur: number): "landscape" | "portrait" {
  return largeur > hauteur ? "landscape" : "portrait";
}

/** Durée d'affichage d'un message de réussite : 5 s, plus selon sa longueur, 12 s au plus. */
export function dureeAnnonce(texte: string): number {
  return Math.min(12000, 5000 + texte.length * 50);
}

/** Image PNG (fond blanc, deux fois la taille) d'un SVG. */
export async function enPng(svg: string): Promise<Uint8Array> {
  const image = new Image();
  image.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
  await image.decode();
  const toile = document.createElement("canvas");
  const echelle = echellePng(image.width, image.height);
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

/** Les octets d'un export, dans le format voulu. */
async function octetsDe(schema: Schema, format: FormatExport, vue?: Vue): Promise<Uint8Array> {
  if (format === "json") return new TextEncoder().encode(ecrireSchema(schema));
  if (format === "svg") return new TextEncoder().encode(svgAutonome(schema, vue));
  return enPng(svgAutonome(schema, vue));
}

/**
 * Demande un dossier, y écrit l'export, et rend le chemin du fichier écrit (l'application n'écrase rien).
 * Rend `null` si le choix du dossier a été annulé : rien n'est écrit.
 */
export async function exporterSchema(schema: Schema, format: FormatExport, vue?: Vue): Promise<string | null> {
  const dossier = await api.choisirDossier("Dossier où enregistrer l'export du schéma");
  if (!dossier) return null;
  return api.exporterSchema(dossier, nomDeFichier(schema.titre, format), await octetsDe(schema, format, vue));
}

/** Copie l'image PNG du schéma dans le presse-papiers, prête à être collée ailleurs. */
export async function partagerSchema(schema: Schema, vue?: Vue): Promise<void> {
  await api.copierImageSchema(await enPng(svgAutonome(schema, vue)));
}

const CLASSE_IMPRESSION = "impression-schema";

/**
 * Pose dans la page ce qui s'imprime : le titre et le dessin, seuls (la feuille `@media print` de page.css
 * cache tout le reste tant que ce conteneur existe). Rend de quoi le retirer.
 *
 * Le dessin est une image, comme à l'export : un SVG posé tel quel dans la page chercherait ses ombres et
 * ses dégradés (`url(#…)`) dans les aperçus de la liste, cachés à l'impression, et perdrait ses équipements.
 */
export async function preparerImpression(schema: Schema, vue?: Vue): Promise<() => void> {
  for (const ancien of document.querySelectorAll(`.${CLASSE_IMPRESSION}`)) ancien.remove();
  const svg = svgAutonome(schema, vue);
  const { largeur, hauteur } = dimensionsSvg(svg);
  const zone = document.createElement("div");
  zone.className = CLASSE_IMPRESSION;
  // Le sens de la page suit la forme du dessin.
  const page = document.createElement("style");
  page.textContent = `@page { size: ${orientationPage(largeur, hauteur)}; }`;
  const titre = document.createElement("h1");
  titre.textContent = schema.titre || NOM_PAR_DEFAUT;
  const dessin = new Image();
  dessin.alt = descriptionDuSchema(schema);
  dessin.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
  // L'image est prête avant que l'impression s'ouvre : sinon la page partirait sans son dessin.
  await dessin.decode();
  zone.append(page, titre, dessin);
  document.body.append(zone);
  return () => zone.remove();
}

/** Imprime le schéma seul : son titre et son dessin, ajustés à la page. */
export async function imprimerSchema(schema: Schema, vue?: Vue): Promise<void> {
  const retirer = await preparerImpression(schema, vue);
  window.addEventListener("afterprint", retirer, { once: true });
  try {
    window.print();
  } catch (erreur) {
    window.removeEventListener("afterprint", retirer);
    retirer();
    throw erreur;
  }
}
