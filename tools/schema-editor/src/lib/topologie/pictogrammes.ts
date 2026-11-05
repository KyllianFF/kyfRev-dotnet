// Pictogrammes au trait (24 × 24) : la bibliothèque de l'éditeur, et les signes
// posés sur les illustrations des équipements.

import { ICONES_LUCIDE } from "./icones";

/** Pictogrammes dessinés pour kyfRev (24 × 24, trait). */
const ICONES_KYFREV: Record<string, string> = {
  "kyfrev-switch":
    '<rect x="2" y="6" width="20" height="12" rx="2"/><path d="M6 10.5h11M15 8.5l2 2-2 2M18 13.5H7M9 11.5l-2 2 2 2"/>',
  "kyfrev-switch-coeur":
    '<rect x="3" y="2" width="18" height="20" rx="2"/><path d="M3 8h18"/><path d="M7 5h3.5M6.5 5l1-1M6.5 5l1 1M17 5h-3.5M17.5 5l-1-1M17.5 5l-1 1"/><circle cx="12" cy="15" r="1.5"/><path d="M12 10.5v2M12 17.5v2M7.5 15h2M14.5 15h2M8.8 11.8l1.4 1.4M15.2 11.8l-1.4 1.4M8.8 18.2l1.4-1.4M15.2 18.2l-1.4-1.4"/>',
  "kyfrev-routeur":
    '<circle cx="12" cy="12" r="10"/><path d="M12 6v4M10.5 7.5 12 6l1.5 1.5M12 18v-4M10.5 16.5 12 18l1.5-1.5M6 12h4M7.5 10.5 6 12l1.5 1.5M18 12h-4M16.5 10.5 18 12l-1.5 1.5"/>',
  "kyfrev-modem":
    '<rect x="2" y="12" width="20" height="8" rx="2"/><path d="M6 16h.01"/><path d="M10 16h.01"/><path d="M3 7c1.5-2 3-2 4.5 0s3 2 4.5 0 3-2 4.5 0 3 2 4.5 0"/>',
  "kyfrev-point-acces":
    '<rect width="20" height="8" x="2" y="14" rx="2"/><path d="M6.01 18H6"/><path d="M10.01 18H10"/><path d="M15 10v4"/><path d="M17.84 7.17a4 4 0 0 0-5.66 0"/><path d="M20.66 4.34a8 8 0 0 0-11.31 0"/>',
  "kyfrev-vm":
    '<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/><rect x="7" y="7" width="10" height="6" rx="1" stroke-dasharray="2 2"/>',
  "kyfrev-telephone":
    '<path d="M13.832 16.568a1 1 0 0 0 1.213-.303l.355-.465A2 2 0 0 1 17 15h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2A18 18 0 0 1 2 4a2 2 0 0 1 2-2h3a2 2 0 0 1 2 2v3a2 2 0 0 1-.8 1.6l-.468.351a1 1 0 0 0-.292 1.233 14 14 0 0 0 6.392 6.384"/>',
  "kyfrev-panneau":
    '<rect x="2" y="7" width="20" height="10" rx="1"/><path d="M5 10h2v2H5zM9 10h2v2H9zM13 10h2v2h-2zM17 10h2v2h-2z"/><path d="M2 15h20"/>',
};

export const icone = (nom: string): string =>
  ICONES_KYFREV[nom] ?? ICONES_LUCIDE[nom] ?? ICONES_LUCIDE.box ?? "";
