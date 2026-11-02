// Correction d'orthographe au clic droit (D31) : le mot cliqué, tel que le
// correcteur de Windows le lit (apostrophes et traits d'union compris :
// « l'éléfant », « peut-être »).

const LETTRE = /[\p{L}\p{M}\p{N}]/u;
const LIAISON = /['’-]/;

const dansUnMot = (c: string | undefined) => c !== undefined && (LETTRE.test(c) || LIAISON.test(c));

/**
 * Étendue (`debut` inclus, `fin` exclue) du mot de `texte` qui touche la
 * position `position` (un clic juste après la dernière lettre compte) ;
 * `null` hors d'un mot.
 */
export function motAutour(texte: string, position: number): { debut: number; fin: number } | null {
  let debut = Math.max(0, Math.min(position, texte.length));
  if (!dansUnMot(texte[debut]) && dansUnMot(texte[debut - 1])) debut--;
  if (!dansUnMot(texte[debut])) return null;
  let fin = debut;
  while (dansUnMot(texte[debut - 1])) debut--;
  while (dansUnMot(texte[fin])) fin++;
  // Pas d'apostrophe ni de trait d'union aux bords (« 'mot' », « -mot »).
  while (debut < fin && LIAISON.test(texte[debut] ?? "")) debut++;
  while (fin > debut && LIAISON.test(texte[fin - 1] ?? "")) fin--;
  return LETTRE.test(texte.slice(debut, fin)) ? { debut, fin } : null;
}
