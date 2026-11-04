// Référence d'une fiche vers un schéma réseau (D39) : un bloc
//
//   ```reseau
//   schema: s_01JB3…
//   ```
//
// Retirer ce bloc d'une fiche ne touche pas au schéma (schemas/<id>.json),
// que d'autres fiches peuvent insérer. Un bloc ```reseau qui contient un
// objet JSON est un schéma au format 1 (D21), encore rangé dans la fiche.

const MOTIF = /^\s*schema\s*:\s*(s_[0-9A-Za-z]{26})\s*$/;

/** Identifiant du schéma cité par un bloc ```reseau, ou `null` (ancien format, vide…). */
export function referenceDe(source: string): string | null {
  return MOTIF.exec(source.trim())?.[1] ?? null;
}

/** Contenu d'un bloc ```reseau qui cite le schéma `id`. */
export const contenuDeReference = (id: string): string => `schema: ${id}`;

/** Le bloc contient-il un schéma au format 1 (JSON dans la fiche) ? */
export const estAncienFormat = (source: string): boolean => source.trim().startsWith("{");
