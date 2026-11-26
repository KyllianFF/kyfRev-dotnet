// Ce que le volet d'un équipement retient d'un équipement à l'autre (30C, 30D), le temps que l'îlot est ouvert :
// la configuration copiée, en attente d'être collée, et les types choisis récemment dans la grille.

import type { ConfigurationCopiee } from "./configuration";

/** La configuration copiée par « ⋯ » puis « Copier la configuration ». */
export const configurationCopiee = $state<{ valeur: ConfigurationCopiee | null }>({ valeur: null });

/** Les types choisis récemment dans la grille des types, le plus récent d'abord. */
export const typesRecents = $state<{ liste: string[] }>({ liste: [] });

export function retenirType(type: string): void {
  typesRecents.liste = [type, ...typesRecents.liste.filter((t) => t !== type)].slice(0, 10);
}
