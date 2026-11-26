// Aperçu d'un schéma réseau en SVG nettoyé (vue Schémas réseau, D40). Rangé
// avec le module (D61) : le reste de l'application ne le charge pas.

import DOMPurify from "dompurify";
import { dessinerSchema } from "./dessin";
import type { Schema, Vue } from "./modele";

/** L'aperçu d'un schéma ; un schéma à plusieurs pages montre `page` (sinon sa première), avec ses renvois. */
export function apercuSchema(schema: Schema, vue?: Vue, legende = false, page?: string): string {
  return DOMPurify.sanitize(
    dessinerSchema(schema, { ...(vue ? { vue } : {}), legende, ...(page !== undefined ? { page } : {}) }),
    { USE_PROFILES: { svg: true, svgFilters: true } },
  );
}
