// L'accueil des schémas (31A, 31B) : ce que montre une carte, les filtres, le tri, les sections et la sélection.
// Rien ici ne touche au coffre ni à la page : la vue donne les schémas lus, et reçoit ce qu'elle doit montrer.

import { ETIQUETTE_MAX, lireEtiquettes } from "./format";

/** Ce que l'accueil sait d'un schéma : de quoi le montrer, le chercher, le filtrer et le trier. */
export type CarteSchema = {
  id: string;
  titre: string;
  equipements: number;
  /** Une page seule (ou aucune) compte pour une. */
  pages: number;
  /** Les points à vérifier du diagnostic (les informations manquantes n'y sont pas). */
  points: number;
  /** Le nombre de fiches qui l'insèrent. */
  fiches: number;
  modifieLe: string | null;
  etiquettes: string[];
  epingle: boolean;
  /** Le texte où chercher, sans accents ni majuscules : titre, description, fiches, équipements et adresses, étiquettes. */
  texte: string;
};

/** Ce qui se trie : les trois choix de la barre d'outils, et les colonnes de la liste. */
export type CleTri = "modifie" | "nom" | "equipements" | "pages" | "points" | "fiches" | "etiquettes";
export type Sens = "asc" | "desc";
export type Tri = { cle: CleTri; sens: Sens };

/** Les tris de la barre d'outils, chacun dans son sens naturel. */
export const TRIS: { cle: CleTri; nom: string; sens: Sens; section: string }[] = [
  { cle: "modifie", nom: "Modifiés récemment", sens: "desc", section: "modifiés récemment" },
  { cle: "nom", nom: "Nom", sens: "asc", section: "par nom" },
  { cle: "equipements", nom: "Taille", sens: "desc", section: "par taille" },
];

/** Le sens naturel d'une colonne, au premier clic sur son en-tête : les noms de A à Z, les nombres et les dates du plus grand. */
export const sensNaturel = (cle: CleTri): Sens => (cle === "nom" || cle === "etiquettes" ? "asc" : "desc");

/** Un texte sans accents ni majuscules, pour chercher « reseau » dans « Réseau ». */
export const sansAccents = (t: string): string => t.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase();

/**
 * Les étiquettes en usage, de la plus portée à la moins portée (puis par nom), avec le nombre de schémas de chacune.
 * Une même étiquette écrite de deux façons (« Lycée », « lycée ») compte pour une, sous l'écriture la plus courante
 * (à égalité, celle qui commence par une majuscule).
 */
export function compterEtiquettes(cartes: readonly CarteSchema[]): { nom: string; nombre: number }[] {
  const comptes = new Map<string, { nombre: number; ecritures: Map<string, number> }>();
  for (const carte of cartes) {
    for (const nom of carte.etiquettes) {
      const cle = nom.toLocaleLowerCase("fr-FR");
      const compte = comptes.get(cle) ?? { nombre: 0, ecritures: new Map<string, number>() };
      compte.nombre++;
      compte.ecritures.set(nom, (compte.ecritures.get(nom) ?? 0) + 1);
      comptes.set(cle, compte);
    }
  }
  return [...comptes.values()]
    .map(({ nombre, ecritures }) => ({
      nom:
        [...ecritures].sort(([a, na], [b, nb]) => nb - na || a.localeCompare(b, "fr", { caseFirst: "upper" }))[0]?.[0] ?? "",
      nombre,
    }))
    .sort((a, b) => b.nombre - a.nombre || a.nom.localeCompare(b.nom, "fr"));
}

export type Filtres = {
  recherche: string;
  /** Les étiquettes choisies : un schéma en porte au moins une. Aucune : pas de filtre. */
  etiquettes: readonly string[];
  /** Seulement les schémas où le diagnostic trouve un point à vérifier. */
  aVerifier: boolean;
};

const memeEtiquette = (a: string, b: string) => a.toLocaleLowerCase("fr-FR") === b.toLocaleLowerCase("fr-FR");

/** Les schémas qui répondent à la recherche (tous ses mots) et aux filtres. */
export function filtrer(cartes: readonly CarteSchema[], filtres: Filtres): CarteSchema[] {
  const mots = sansAccents(filtres.recherche).split(/\s+/).filter(Boolean);
  return cartes.filter(
    (c) =>
      mots.every((m) => c.texte.includes(m)) &&
      (filtres.etiquettes.length === 0 || filtres.etiquettes.some((e) => c.etiquettes.some((x) => memeEtiquette(e, x)))) &&
      (!filtres.aVerifier || c.points > 0),
  );
}

const parNom = (a: CarteSchema, b: CarteSchema) => a.titre.localeCompare(b.titre, "fr", { sensitivity: "base" }) || a.id.localeCompare(b.id);

/** Trie les schémas ; à égalité, par nom. Un schéma sans date de modification va à la fin, dans les deux sens. */
export function trier(cartes: readonly CarteSchema[], tri: Tri): CarteSchema[] {
  const signe = tri.sens === "asc" ? 1 : -1;
  const valeur = (c: CarteSchema): number | string => {
    switch (tri.cle) {
      case "nom":
        return 0;
      case "modifie":
        return c.modifieLe ? Date.parse(c.modifieLe) : Number.NaN;
      case "etiquettes":
        return c.etiquettes.join(", ");
      default:
        return c[tri.cle];
    }
  };
  return [...cartes].sort((a, b) => {
    if (tri.cle === "nom") return signe * parNom(a, b);
    const [va, vb] = [valeur(a), valeur(b)];
    if (typeof va === "number" && typeof vb === "number") {
      if (Number.isNaN(va) !== Number.isNaN(vb)) return Number.isNaN(va) ? 1 : -1;
      if (!Number.isNaN(va) && va !== vb) return signe * (va - vb);
    } else if (typeof va === "string" && typeof vb === "string" && va !== vb) {
      // Sans étiquette : à la fin, dans les deux sens.
      if (!va || !vb) return va ? -1 : 1;
      return signe * va.localeCompare(vb, "fr", { sensitivity: "base" });
    }
    return parNom(a, b);
  });
}

/** Les deux sections de la grille : les épinglés en tête, puis tous les autres, chacune triée. */
export function sections(cartes: readonly CarteSchema[], tri: Tri): { epingles: CarteSchema[]; autres: CarteSchema[] } {
  const tries = trier(cartes, tri);
  return { epingles: tries.filter((c) => c.epingle), autres: tries.filter((c) => !c.epingle) };
}

/** Ctrl+clic : le schéma entre dans la sélection, ou en sort. */
export const basculer = (choisis: readonly string[], id: string): string[] =>
  choisis.includes(id) ? choisis.filter((x) => x !== id) : [...choisis, id];

/**
 * Maj+clic : la suite affichée, de l'ancre (le dernier schéma choisi ou lâché) jusqu'à celui-ci, rejoint la sélection.
 * Sans ancre encore affichée, le schéma seul.
 */
export function etendre(ordre: readonly string[], choisis: readonly string[], ancre: string | null, id: string): string[] {
  const debut = ancre === null ? -1 : ordre.indexOf(ancre);
  const fin = ordre.indexOf(id);
  if (fin < 0) return [...choisis];
  if (debut < 0) return choisis.includes(id) ? [...choisis] : [...choisis, id];
  const suite = ordre.slice(Math.min(debut, fin), Math.max(debut, fin) + 1);
  return [...choisis, ...suite.filter((x) => !choisis.includes(x))];
}

/** Ne garde de la sélection que les schémas encore là (un schéma archivé ou supprimé en sort). */
export const garderPresents = (choisis: readonly string[], presents: readonly string[]): string[] =>
  choisis.filter((id) => presents.includes(id));

const formatJour = new Intl.DateTimeFormat("fr-FR", { day: "numeric", month: "short" });
const formatJourAnnee = new Intl.DateTimeFormat("fr-FR", { day: "numeric", month: "short", year: "numeric" });

/** « aujourd'hui », « hier », « le 26 nov. », « le 26 nov. 2025 » : quand un schéma a changé, en heure locale. */
export function quand(horodatage: string, maintenant = new Date()): string {
  const date = new Date(horodatage);
  if (Number.isNaN(date.getTime())) return "";
  const jour = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
  const ecart = Math.round((jour(maintenant) - jour(date)) / 86_400_000);
  if (ecart === 0) return "aujourd'hui";
  if (ecart === 1) return "hier";
  return `le ${(date.getFullYear() === maintenant.getFullYear() ? formatJour : formatJourAnnee).format(date)}`;
}

/** Une étiquette saisie, nettoyée comme le schéma la gardera ; vide si rien ne reste. */
export const etiquetteNette = (saisie: string): string => lireEtiquettes([saisie])?.[0] ?? "";

/** Longueur d'une étiquette, au plus (le champ de saisie s'y arrête). */
export { ETIQUETTE_MAX };

/**
 * Les étiquettes d'un schéma après « Étiqueter… » : celles retirées partent, celles ajoutées arrivent à la fin (sans
 * doublon, à la casse près). Rend `undefined` s'il n'en reste aucune : la clé ne s'écrit pas.
 */
export function etiqueter(
  actuelles: readonly string[] | undefined,
  ajouts: readonly string[],
  retraits: readonly string[],
): string[] | undefined {
  const gardees = (actuelles ?? []).filter((e) => !retraits.some((r) => memeEtiquette(e, r)));
  return lireEtiquettes([...gardees, ...ajouts]);
}
