// Aperçu d'un schéma réseau en SVG nettoyé (vue Schémas réseau, D40). Rangé
// avec le module (D61) : le reste de l'application ne le charge pas.

import DOMPurify from "dompurify";
import { dessinerSchema } from "./dessin";
import type { Schema, Vue } from "./modele";

export function apercuSchema(schema: Schema, vue?: Vue, legende = false): string {
  return DOMPurify.sanitize(dessinerSchema(schema, { ...(vue ? { vue } : {}), legende }), {
    USE_PROFILES: { svg: true, svgFilters: true },
  });
}
