// Le dessin d'un schéma réseau, pour la page cachée qui fait les images des fiches : elle n'a pas besoin de l'éditeur.

import { dessinerSchema } from "./lib/topologie/dessin";
import { lireSchema } from "./lib/topologie/format";

/** Le SVG d'un schéma, aux couleurs données : une image n'a pas accès aux variables de la page. */
export function dessinerReseau(texte: string, couleurs: Record<string, string>): string {
  const svg = dessinerSchema(lireSchema(texte).schema);
  return svg.replace(/var\((--[a-z0-9-]+),\s*([^)]+)\)/g, (_, nom: string, defaut: string) => couleurs[nom] ?? defaut);
}

(window as unknown as { kyfRevReseau: typeof dessinerReseau }).kyfRevReseau = dessinerReseau;
