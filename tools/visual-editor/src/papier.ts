// Le papier derrière le texte : uni, à carreaux, ligné… L'application en donne le nom ; il devient un attribut de la
// page, et tout le dessin est dans page.css (des fonds répétés, aux couleurs du thème). Rien du document n'en dépend.
// Un papier qui a un dessin a aussi des lignes d'écriture, où le texte se pose : lignes.ts.

/** Les papiers connus. L'ordre est celui de la liste de l'application. */
export const PAPIERS = ["uni", "petits-carreaux", "grands-carreaux", "lignes", "points", "millimetre"] as const;

export type Papier = (typeof PAPIERS)[number];

/** L'attribut de la racine de la page qui porte le papier : `data-papier="lignes"`. */
export const ATTRIBUT_PAPIER = "data-papier";

/** Le papier demandé ; rien pour « pas de papier » (une chaîne vide) ou un nom inconnu. */
export function papierDe(style: string | null | undefined): Papier | null {
  const nom = (style ?? "").trim();
  return (PAPIERS as readonly string[]).includes(nom) ? (nom as Papier) : null;
}

/** Les papiers qui ont un dessin : le texte s'écrit sur leurs lignes, et les cases des tableaux le cachent. */
export const estRegle = (papier: Papier | null) => papier !== null && papier !== "uni";

/** Pose le papier sur la racine de la page, ou le retire : sans papier, il ne reste aucune trace. */
export function poserPapier(racine: Pick<Element, "setAttribute" | "removeAttribute">, style: string | null | undefined): Papier | null {
  const papier = papierDe(style);
  if (papier) racine.setAttribute(ATTRIBUT_PAPIER, papier);
  else racine.removeAttribute(ATTRIBUT_PAPIER);
  return papier;
}
