// Le plateau d'équipements de l'éditeur (11A, 12) : les familles du catalogue en onglets, la recherche,
// et pour chaque tuile son nom court. Les tuiles viennent du catalogue : un type ajouté là apparaît ici.

import { CATEGORIES, type Categorie, chercherTypes, type DefinitionType, TYPES } from "./catalogue";
import { GABARITS } from "./gabarits";
import { GENRES_GROUPE } from "./modele";
import { icone } from "./pictogrammes";

/** Un onglet du plateau : tout, une catégorie du catalogue, ou (mode avancé) cadres et modèles. */
export type FamillePlateau = "tous" | Categorie | "cadres" | "modeles";

const NOMS_DES_FAMILLES: Record<Categorie, string> = {
  reseau: "Réseau",
  serveurs: "Serveurs",
  terminaux: "Postes",
  stockage: "Stockage",
  infrastructure: "Infrastructure",
  contexte: "Contexte",
};
const ORDRE: Categorie[] = ["reseau", "serveurs", "terminaux", "stockage", "infrastructure", "contexte"];

/**
 * Les onglets : « Tous », puis les catégories du catalogue (`titre` : leur nom entier). En mode avancé
 * s'ajoutent les cadres et notes, et les modèles de topologie, que la bibliothèque proposait.
 */
export function famillesDuPlateau(avance: boolean): { valeur: FamillePlateau; nom: string; titre: string }[] {
  return [
    { valeur: "tous", nom: "Tous", titre: "Tous les équipements" },
    ...ORDRE.map((valeur) => ({
      valeur,
      nom: NOMS_DES_FAMILLES[valeur],
      titre: CATEGORIES.find((c) => c.valeur === valeur)?.nom ?? NOMS_DES_FAMILLES[valeur],
    })),
    ...(avance
      ? [
          { valeur: "cadres" as const, nom: "Cadres et notes", titre: "Cadres, zones, notes et flèches" },
          { valeur: "modeles" as const, nom: "Modèles", titre: "Modèles de topologie" },
        ]
      : []),
  ];
}

/** Nom court de chaque type, sous son icône ; un type sans nom court garde son nom entier. */
const NOMS_COURTS: Record<string, string> = {
  switch: "Switch",
  "switch-non-administrable": "Switch simple",
  "switch-coeur": "Cœur",
  "switch-distribution": "Distribution",
  routeur: "Routeur",
  "pare-feu": "Pare-feu",
  cpe: "CPE",
  modem: "Modem",
  "sd-wan": "SD-WAN",
  "cato-socket": "Cato Socket",
  "passerelle-vpn": "VPN",
  "point-acces": "Borne Wi-Fi",
  "controleur-wifi": "Ctrl Wi-Fi",
  repartiteur: "Répartiteur",
  "equipement-reseau": "Réseau",
  serveur: "Serveur",
  esxi: "ESXi",
  proxmox: "Proxmox",
  "hyper-v": "Hyper-V",
  hyperviseur: "Hyperviseur",
  "cluster-virtualisation": "Cluster",
  "machine-virtuelle": "VM",
  "hote-docker": "Docker",
  conteneur: "Conteneur",
  compose: "Compose",
  "cluster-kubernetes": "Kubernetes",
  "noeud-kubernetes": "Nœud K8s",
  nas: "NAS",
  san: "SAN",
  "baie-stockage": "Baie disques",
  "controleur-stockage": "Contrôleur",
  "switch-stockage": "Switch SAN",
  sauvegarde: "Sauvegarde",
  "bibliotheque-bandes": "Bandes",
  pc: "PC",
  portable: "Portable",
  "client-leger": "Client léger",
  "telephone-ip": "Téléphone",
  mobile: "Mobile",
  imprimante: "Imprimante",
  "camera-ip": "Caméra",
  iot: "IoT",
  generique: "Générique",
  baie: "Baie",
  "panneau-brassage": "Panneau",
  "prise-reseau": "Prise",
  onduleur: "Onduleur",
  pdu: "PDU",
  internet: "Internet",
  "reseau-operateur": "Opérateur",
  "reseau-distant": "Site distant",
  cloud: "Cloud",
};

export const nomCourt = (type: DefinitionType): string => NOMS_COURTS[type.type] ?? type.nom;

/** Les équipements les plus posés : en tête de l'onglet « Tous », dans cet ordre. */
const COURANTS = [
  "switch",
  "switch-coeur",
  "routeur",
  "pare-feu",
  "point-acces",
  "serveur",
  "nas",
  "pc",
  "portable",
  "imprimante",
  "telephone-ip",
  "internet",
  "cloud",
];

/**
 * Une tuile du plateau. `genre` est ce que le canevas reçoit (« type:switch », « groupe:site »,
 * « annotation:fleche », « gabarit:dmz ») ; `type` : l'équipement du catalogue, dessiné par son
 * illustration ; `icone` : le pictogramme au trait des autres tuiles.
 */
export type Tuile = { genre: string; nom: string; titre: string; type?: DefinitionType; icone?: string };

const sansAccents = (s: string) => s.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase();

/** Les types d'équipements trouvés : par leur nom, leur type (`chercherTypes`) ou leur nom court. */
function typesTrouves(recherche: string): DefinitionType[] {
  const mots = sansAccents(recherche).split(/\s+/).filter(Boolean);
  if (mots.length === 0) return TYPES;
  const trouves = new Set(chercherTypes(recherche));
  return TYPES.filter((t) => trouves.has(t) || mots.every((m) => sansAccents(nomCourt(t)).includes(m)));
}

const CADRES: Tuile[] = [
  ...GENRES_GROUPE.map((g) => ({
    genre: `groupe:${g.valeur}`,
    nom: g.nom,
    titre: `Cadre : ${g.nom}`,
    icone: icone(g.valeur === "site" ? "map-pin" : g.valeur === "batiment" ? "warehouse" : "square-dashed"),
  })),
  { genre: "annotation", nom: "Note", titre: "Note", icone: icone("sticky-note") },
  {
    genre: "annotation:texte",
    nom: "Texte",
    titre: "Texte libre",
    icone: '<path d="M4 7V4h16v3"/><path d="M9 20h6"/><path d="M12 4v16"/>',
  },
  {
    genre: "annotation:rectangle",
    nom: "Zone",
    titre: "Zone rectangulaire",
    icone: '<rect x="3" y="5" width="18" height="14" rx="2"/>',
  },
  {
    genre: "annotation:ellipse",
    nom: "Zone ronde",
    titre: "Zone ronde",
    icone: '<ellipse cx="12" cy="12" rx="9" ry="7"/>',
  },
  {
    genre: "annotation:fleche",
    nom: "Flèche",
    titre: "Flèche",
    icone: '<path d="M5 19 19 5"/><path d="M9 5h10v10"/>',
  },
];

/**
 * Les tuiles d'un onglet, filtrées par la recherche (sans casse ni accents). « Tous » : les équipements
 * courants d'abord, puis le reste dans l'ordre du catalogue. La recherche porte sur les équipements.
 */
export function tuilesDuPlateau(famille: FamillePlateau, recherche = ""): Tuile[] {
  if (famille === "cadres") return CADRES;
  if (famille === "modeles") {
    return GABARITS.map((g) => ({
      genre: `gabarit:${g.id}`,
      nom: g.nom,
      titre: `${g.nom}. ${g.description}`,
      icone: icone("network"),
    }));
  }
  const types = typesTrouves(recherche).filter((t) => famille === "tous" || t.categorie === famille);
  const rang = (t: DefinitionType) => {
    const k = COURANTS.indexOf(t.type);
    return k === -1 ? COURANTS.length : k;
  };
  const tries = famille === "tous" ? [...types].sort((a, b) => rang(a) - rang(b)) : types;
  return tries.map((t) => ({ genre: `type:${t.type}`, nom: nomCourt(t), titre: t.nom, type: t }));
}
