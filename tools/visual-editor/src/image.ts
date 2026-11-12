// L'apparence d'une image, lue et écrite dans son titre Markdown, comme côté application
// (KyfRev.Rendering/ImageLook.cs) : « kyfrev-largeur:60 kyfrev-position:centre kyfrev-cadre:10,5,80,90 kyfrev-masques:… ».
// Les mêmes cas sont vérifiés des deux côtés (tests/fixtures/image/titres.json).

/** Une zone d'une image, en parts de l'image (de 0 à 1). */
export type Zone = { x: number; y: number; largeur: number; hauteur: number };

/** Où l'image se place : dans le fil du texte, ou seule sur sa ligne. */
export type Position = "fil" | "gauche" | "centre" | "droite";

/** L'apparence d'une image. Les masques sont gardés tels qu'ils sont écrits : ici, on ne les touche pas. */
export type ApparenceImage = { largeur: number | null; position: Position; cadre: Zone | null; masques: string | null };

export const IMAGE_ORDINAIRE: ApparenceImage = { largeur: null, position: "fil", cadre: null, masques: null };

const LARGEUR = "kyfrev-largeur:";
const POSITION = "kyfrev-position:";
const CADRE = "kyfrev-cadre:";
const MASQUES = "kyfrev-masques:";
const LARGEUR_MIN = 5;
const LARGEUR_MAX = 100;
const COTE_MIN = 0.01;

const borner = (valeur: number, min: number, max: number) => Math.min(Math.max(valeur, min), max);
const arrondi = (part: number) => Math.round(part * 10000) / 10000;

/** Ramène une zone dans l'image ; rien s'il n'en reste pas de quoi la voir. */
export function zoneNormale(zone: Zone): Zone | null {
  const gauche = borner(Math.min(zone.x, zone.x + zone.largeur), 0, 1);
  const droite = borner(Math.max(zone.x, zone.x + zone.largeur), 0, 1);
  const haut = borner(Math.min(zone.y, zone.y + zone.hauteur), 0, 1);
  const bas = borner(Math.max(zone.y, zone.y + zone.hauteur), 0, 1);
  return droite - gauche >= COTE_MIN && bas - haut >= COTE_MIN
    ? { x: arrondi(gauche), y: arrondi(haut), largeur: arrondi(droite - gauche), hauteur: arrondi(bas - haut) }
    : null;
}

const entiere = (zone: Zone) => zone.x === 0 && zone.y === 0 && zone.largeur === 1 && zone.hauteur === 1;

function lireZone(texte: string): Zone | null {
  const nombres = texte.split(",");
  if (nombres.length !== 4 || !nombres.every((nombre) => /^\d+(\.\d+)?$/.test(nombre))) return null;
  const parts = nombres.map((nombre) => Number(nombre) / 100);
  return zoneNormale({ x: parts[0] ?? 0, y: parts[1] ?? 0, largeur: parts[2] ?? 0, hauteur: parts[3] ?? 0 });
}

const pourcent = (part: number) => String(Math.round(part * 1000) / 10);

/**
 * L'apparence écrite dans un titre. Sans titre, l'image est ordinaire ; un titre qui n'est pas à nous (une légende)
 * rend null : on ne peut ni le lire ni le remplacer. Un mot à nous mais mal écrit est laissé de côté.
 */
export function lireTitre(titre: string | null | undefined): ApparenceImage | null {
  if (!titre) return IMAGE_ORDINAIRE;
  const mots = titre.split(" ").filter((mot) => mot !== "");
  const aNous = (mot: string) => [LARGEUR, POSITION, CADRE, MASQUES].some((debut) => mot.startsWith(debut));
  if (mots.length === 0 || !mots.every(aNous)) return null;
  let apparence = IMAGE_ORDINAIRE;
  for (const mot of mots) {
    if (mot.startsWith(LARGEUR)) {
      const valeur = mot.slice(LARGEUR.length);
      if (/^\d+$/.test(valeur)) apparence = { ...apparence, largeur: borner(Number(valeur), LARGEUR_MIN, LARGEUR_MAX) };
    } else if (mot.startsWith(POSITION)) {
      const valeur = mot.slice(POSITION.length);
      if (valeur === "gauche" || valeur === "centre" || valeur === "droite") apparence = { ...apparence, position: valeur };
    } else if (mot.startsWith(CADRE)) {
      const cadre = lireZone(mot.slice(CADRE.length));
      if (cadre && !entiere(cadre)) apparence = { ...apparence, cadre };
    } else {
      apparence = { ...apparence, masques: mot.slice(MASQUES.length) || null };
    }
  }
  return apparence;
}

/** Le titre qui écrit cette apparence ; null pour une image ordinaire : elle n'a alors pas de titre. */
export function ecrireTitre(apparence: ApparenceImage): string | null {
  const mots: string[] = [];
  if (apparence.largeur !== null) mots.push(LARGEUR + borner(Math.round(apparence.largeur), LARGEUR_MIN, LARGEUR_MAX));
  if (apparence.position !== "fil") mots.push(POSITION + apparence.position);
  const cadre = apparence.cadre ? zoneNormale(apparence.cadre) : null;
  if (cadre && !entiere(cadre)) {
    mots.push(CADRE + [cadre.x, cadre.y, cadre.largeur, cadre.hauteur].map(pourcent).join(","));
  }
  if (apparence.masques) mots.push(MASQUES + apparence.masques);
  return mots.length === 0 ? null : mots.join(" ");
}

/**
 * Applique une demande du ruban au titre d'une image : « width=60 », « width= » (taille d'origine), « align=centre »,
 * « align=fil », « crop=10,5,80,90 », « crop= » (image entière), « reset » (tout sauf les masques). Rend undefined si le
 * titre n'est pas à nous ou si la demande n'est pas comprise : rien ne change alors.
 */
export function changerTitre(titre: string | null, changement: string, valeur: string | null): string | null | undefined {
  const apparence = lireTitre(titre);
  if (!apparence) return undefined;
  switch (changement) {
    case "width": {
      if (!valeur) return ecrireTitre({ ...apparence, largeur: null });
      return /^\d+$/.test(valeur) ? ecrireTitre({ ...apparence, largeur: Number(valeur) }) : undefined;
    }
    case "align":
      return valeur === "fil" || valeur === "gauche" || valeur === "centre" || valeur === "droite"
        ? ecrireTitre({ ...apparence, position: valeur })
        : undefined;
    case "crop": {
      if (!valeur) return ecrireTitre({ ...apparence, cadre: null });
      const cadre = lireZone(valeur);
      return cadre ? ecrireTitre({ ...apparence, cadre }) : undefined;
    }
    case "reset":
      return ecrireTitre({ ...IMAGE_ORDINAIRE, masques: apparence.masques });
    default:
      return undefined;
  }
}
