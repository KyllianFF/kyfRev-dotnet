// Catalogue des types d'équipements (D39). Ajouter un type : une entrée ici,
// rien d'autre à changer. Chaque type a sa catégorie, son pictogramme, un
// profil de ports par défaut et ses propriétés propres (un NAS n'a pas les
// champs d'un switch).

import type { GenreInterface, GroupePorts, RoleInterface } from "./modele";

export type Categorie = "reseau" | "serveurs" | "stockage" | "terminaux" | "infrastructure" | "contexte";
export const CATEGORIES: { valeur: Categorie; nom: string }[] = [
  { valeur: "reseau", nom: "Réseau et sécurité" },
  { valeur: "serveurs", nom: "Serveurs, virtualisation, conteneurs" },
  { valeur: "stockage", nom: "Stockage" },
  { valeur: "terminaux", nom: "Terminaux et périphériques" },
  { valeur: "infrastructure", nom: "Infrastructure" },
  { valeur: "contexte", nom: "Réseaux extérieurs" },
];

/** Propriété propre à un type d'équipement. */
export type Champ = {
  cle: string;
  nom: string;
  genre: "texte" | "nombre" | "choix" | "oui-non";
  options?: string[];
  unite?: string;
  aide?: string;
};

export type DefinitionType = {
  type: string;
  nom: string;
  /** Nom court des nouveaux équipements (« SW » donne « SW 1 »). */
  court: string;
  categorie: Categorie;
  /** Pictogramme : une icône Lucide (icones.ts) ou un dessin de kyfRev (dessin.ts). */
  icone: string;
  /** Profil de ports d'un nouvel équipement (membre 1). */
  ports: GroupePorts[];
  champs: Champ[];
  /** Peut former un stack (plusieurs membres). */
  empilable?: boolean;
  /** Héberge des machines, conteneurs ou nœuds (relation d'hébergement). */
  hebergeur?: boolean;
  /** Se place dans un hôte (machine virtuelle, conteneur…). */
  heberge?: boolean;
  /** Ses interfaces acceptent plusieurs connexions (nuage, panneau de brassage…). */
  multiConnexion?: boolean;
};

const groupe = (
  nom: string,
  motif: string,
  nombre: number,
  genre: GenreInterface,
  options: {
    premier?: number;
    connecteur?: string;
    vitesse?: number;
    role?: RoleInterface;
    poe?: boolean;
  } = {},
): GroupePorts => ({ nom, motif, nombre, premier: options.premier ?? 1, genre, ...options });

const cuivre = (nom: string, motif: string, nombre: number, options: Parameters<typeof groupe>[4] = {}) =>
  groupe(nom, motif, nombre, "cuivre", { connecteur: "RJ45", vitesse: 1000, ...options });

const texte = (cle: string, nom: string, aide?: string): Champ => ({
  cle,
  nom,
  genre: "texte",
  ...(aide ? { aide } : {}),
});
const nombre = (cle: string, nom: string, unite?: string): Champ => ({
  cle,
  nom,
  genre: "nombre",
  ...(unite ? { unite } : {}),
});
const choix = (cle: string, nom: string, options: string[]): Champ => ({ cle, nom, genre: "choix", options });
const ouiNon = (cle: string, nom: string): Champ => ({ cle, nom, genre: "oui-non" });

const PORTS_SERVEUR = [
  cuivre("Réseau", "eno{p}", 4),
  cuivre("Gestion hors bande", "BMC", 1, { role: "gestion" }),
];
const CHAMPS_MACHINE = [texte("cpu", "Processeur"), nombre("memoire", "Mémoire", "Go")];

export const TYPES: DefinitionType[] = [
  // --- Réseau et sécurité ---
  {
    type: "switch",
    nom: "Switch administrable",
    court: "SW",
    categorie: "reseau",
    icone: "kyfrev-switch",
    empilable: true,
    ports: [
      cuivre("Accès", "{m}/{p}", 24, { role: "acces" }),
      groupe("Uplinks", "{m}/{p}", 4, "fibre", {
        premier: 25,
        connecteur: "SFP+",
        vitesse: 10000,
        role: "uplink",
      }),
    ],
    champs: [
      choix("couche", "Couche", ["Niveau 2", "Niveau 3"]),
      ouiNon("poe", "PoE"),
      texte("stp", "Spanning tree"),
    ],
  },
  {
    type: "switch-non-administrable",
    nom: "Switch non administrable",
    court: "SW",
    categorie: "reseau",
    icone: "kyfrev-switch",
    ports: [cuivre("Ports", "{p}", 8)],
    champs: [ouiNon("poe", "PoE")],
  },
  {
    type: "routeur",
    nom: "Routeur",
    court: "RT",
    categorie: "reseau",
    icone: "kyfrev-routeur",
    ports: [cuivre("WAN", "WAN{p}", 1, { role: "wan" }), cuivre("LAN", "LAN{p}", 4, { role: "lan" })],
    champs: [texte("routage", "Protocoles de routage", "OSPF, BGP, statique…"), texte("nat", "NAT")],
  },
  {
    type: "pare-feu",
    nom: "Pare-feu",
    court: "FW",
    categorie: "reseau",
    icone: "brick-wall",
    ports: [cuivre("Ports", "port{p}", 8), cuivre("Gestion", "MGMT", 1, { role: "gestion" })],
    champs: [
      texte("zones", "Zones de sécurité", "LAN, DMZ, WAN…"),
      choix("hauteDispo", "Haute disponibilité", ["Aucune", "Actif / passif", "Actif / actif"]),
      texte("licences", "Services ou licences"),
    ],
  },
  {
    type: "cpe",
    nom: "CPE opérateur",
    court: "CPE",
    categorie: "reseau",
    icone: "router",
    ports: [cuivre("WAN", "WAN", 1, { role: "wan" }), cuivre("LAN", "LAN{p}", 4, { role: "lan" })],
    champs: [
      texte("operateur", "Opérateur"),
      texte("ligne", "Référence de la ligne"),
      nombre("debitContractuel", "Débit contractuel", "Mbit/s"),
    ],
  },
  {
    type: "modem",
    nom: "Modem ou ONT",
    court: "ONT",
    categorie: "reseau",
    icone: "kyfrev-modem",
    ports: [
      groupe("Ligne", "PON", 1, "fibre", { connecteur: "SC/APC", role: "wan" }),
      cuivre("LAN", "LAN{p}", 4, { role: "lan" }),
    ],
    champs: [
      choix("technologie", "Technologie", ["FTTH (ONT)", "VDSL", "ADSL", "Câble", "4G / 5G"]),
      texte("operateur", "Opérateur"),
      texte("ligne", "Référence de la ligne"),
    ],
  },
  {
    type: "sd-wan",
    nom: "Équipement SD-WAN",
    court: "SDWAN",
    categorie: "reseau",
    icone: "waypoints",
    ports: [cuivre("WAN", "WAN{p}", 2, { role: "wan" }), cuivre("LAN", "LAN{p}", 4, { role: "lan" })],
    champs: [texte("editeur", "Éditeur"), texte("orchestrateur", "Orchestrateur")],
  },
  {
    type: "cato-socket",
    nom: "Cato Socket",
    court: "Socket",
    categorie: "reseau",
    icone: "network",
    ports: [
      cuivre("WAN", "WAN{p}", 2, { role: "wan" }),
      cuivre("LAN", "LAN{p}", 1, { role: "lan" }),
      cuivre("Gestion", "MGMT", 1, { role: "gestion" }),
    ],
    champs: [
      choix("modele", "Modèle", ["X1500", "X1600", "X1700", "vSocket"]),
      texte("pop", "PoP Cato"),
      texte("site", "Site Cato"),
    ],
  },
  {
    type: "passerelle-vpn",
    nom: "Concentrateur ou passerelle VPN",
    court: "VPN",
    categorie: "reseau",
    icone: "lock-keyhole",
    ports: [cuivre("Ports", "eth{p}", 2, { premier: 0 })],
    champs: [
      choix("protocole", "Protocole", ["IPsec", "SSL / TLS", "WireGuard", "OpenVPN"]),
      texte("pairs", "Pairs ou sites"),
    ],
  },
  {
    type: "point-acces",
    nom: "Point d'accès Wi-Fi",
    court: "AP",
    categorie: "reseau",
    icone: "kyfrev-point-acces",
    ports: [
      cuivre("Ethernet", "eth{p}", 1, { premier: 0, role: "uplink" }),
      groupe("Radios", "radio{p}", 2, "radio", { premier: 0 }),
    ],
    champs: [
      texte("ssid", "SSID diffusés", "séparés par des virgules"),
      texte("bandes", "Bandes", "2,4 GHz, 5 GHz, 6 GHz"),
      texte("controleur", "Contrôleur"),
    ],
  },
  {
    type: "controleur-wifi",
    nom: "Contrôleur Wi-Fi",
    court: "WLC",
    categorie: "reseau",
    icone: "wifi-cog",
    ports: [cuivre("Ports", "port{p}", 2)],
    champs: [nombre("pointsAcces", "Points d'accès gérés")],
  },
  {
    type: "repartiteur",
    nom: "Répartiteur de charge",
    court: "LB",
    categorie: "reseau",
    icone: "split",
    ports: [cuivre("Ports", "port{p}", 4)],
    champs: [texte("vip", "Adresses virtuelles (VIP)"), texte("algorithme", "Algorithme")],
  },
  {
    type: "equipement-reseau",
    nom: "Équipement réseau générique",
    court: "Réseau",
    categorie: "reseau",
    icone: "network",
    ports: [cuivre("Ports", "port{p}", 4)],
    champs: [texte("fonction", "Fonction")],
  },

  // --- Serveurs, virtualisation, conteneurs ---
  {
    type: "serveur",
    nom: "Serveur physique",
    court: "SRV",
    categorie: "serveurs",
    icone: "server",
    hebergeur: true,
    ports: PORTS_SERVEUR,
    champs: [...CHAMPS_MACHINE, choix("format", "Format", ["Tour", "Rack", "Lame"])],
  },
  {
    type: "esxi",
    nom: "Hôte VMware ESXi",
    court: "ESXi",
    categorie: "serveurs",
    icone: "server-cog",
    hebergeur: true,
    ports: [
      cuivre("Cartes réseau", "vmnic{p}", 4, { premier: 0 }),
      cuivre("Gestion hors bande", "BMC", 1, { role: "gestion" }),
    ],
    champs: [...CHAMPS_MACHINE, texte("version", "Version ESXi"), texte("vcenter", "vCenter")],
  },
  {
    type: "proxmox",
    nom: "Hôte Proxmox",
    court: "PVE",
    categorie: "serveurs",
    icone: "server-cog",
    hebergeur: true,
    ports: PORTS_SERVEUR,
    champs: [...CHAMPS_MACHINE, texte("version", "Version"), texte("cluster", "Cluster Proxmox")],
  },
  {
    type: "hyper-v",
    nom: "Hôte Hyper-V",
    court: "HV",
    categorie: "serveurs",
    icone: "server-cog",
    hebergeur: true,
    ports: [
      cuivre("Cartes réseau", "NIC{p}", 4),
      cuivre("Gestion hors bande", "BMC", 1, { role: "gestion" }),
    ],
    champs: [...CHAMPS_MACHINE, texte("version", "Version de Windows Server")],
  },
  {
    type: "hyperviseur",
    nom: "Hyperviseur générique",
    court: "HV",
    categorie: "serveurs",
    icone: "server-cog",
    hebergeur: true,
    ports: [cuivre("Cartes réseau", "nic{p}", 2, { premier: 0 })],
    champs: [...CHAMPS_MACHINE, texte("logiciel", "Logiciel")],
  },
  {
    type: "cluster-virtualisation",
    nom: "Cluster de virtualisation",
    court: "Cluster",
    categorie: "serveurs",
    icone: "boxes",
    hebergeur: true,
    ports: [],
    champs: [
      ouiNon("ha", "Haute disponibilité"),
      ouiNon("drs", "Répartition automatique (DRS)"),
      texte("gestion", "Gestion (vCenter…)"),
    ],
  },
  {
    type: "machine-virtuelle",
    nom: "Machine virtuelle",
    court: "VM",
    categorie: "serveurs",
    icone: "kyfrev-vm",
    heberge: true,
    ports: [groupe("Cartes virtuelles", "Carte réseau {p}", 1, "virtuel")],
    champs: [
      nombre("vcpu", "vCPU"),
      nombre("memoire", "Mémoire", "Go"),
      nombre("disque", "Disque", "Go"),
      texte("invite", "Système invité"),
    ],
  },
  {
    type: "hote-docker",
    nom: "Hôte Docker",
    court: "Docker",
    categorie: "serveurs",
    icone: "container",
    hebergeur: true,
    ports: [cuivre("Réseau", "eth{p}", 1, { premier: 0 })],
    champs: [texte("version", "Version de Docker"), ouiNon("swarm", "Mode Swarm")],
  },
  {
    type: "conteneur",
    nom: "Conteneur Docker",
    court: "Conteneur",
    categorie: "serveurs",
    icone: "box",
    heberge: true,
    ports: [groupe("Interfaces", "eth{p}", 1, "virtuel", { premier: 0 })],
    champs: [
      texte("image", "Image", "nginx:1.27"),
      texte("portsPublies", "Ports publiés", "8080:80/tcp, 8443:443/tcp"),
      choix("redemarrage", "Redémarrage", ["no", "always", "unless-stopped", "on-failure"]),
    ],
  },
  {
    type: "compose",
    nom: "Groupe Docker Compose",
    court: "Compose",
    categorie: "serveurs",
    icone: "boxes",
    heberge: true,
    hebergeur: true,
    ports: [],
    champs: [texte("projet", "Projet"), texte("fichier", "Fichier", "compose.yaml")],
  },
  {
    type: "cluster-kubernetes",
    nom: "Cluster Kubernetes",
    court: "K8s",
    categorie: "serveurs",
    icone: "ship-wheel",
    hebergeur: true,
    ports: [],
    champs: [texte("distribution", "Distribution"), texte("version", "Version")],
  },
  {
    type: "noeud-kubernetes",
    nom: "Nœud Kubernetes",
    court: "Nœud",
    categorie: "serveurs",
    icone: "hexagon",
    heberge: true,
    hebergeur: true,
    ports: [cuivre("Réseau", "eth{p}", 1, { premier: 0 })],
    champs: [choix("roleK8s", "Rôle", ["Plan de contrôle", "Travail"])],
  },

  // --- Stockage ---
  {
    type: "nas",
    nom: "NAS",
    court: "NAS",
    categorie: "stockage",
    icone: "hard-drive",
    ports: [
      cuivre("LAN", "LAN{p}", 2),
      groupe("10 GbE", "10G-{p}", 2, "fibre", { connecteur: "SFP+", vitesse: 10000 }),
    ],
    champs: [
      nombre("capaciteTo", "Capacité totale", "To"),
      nombre("utiliseTo", "Capacité utilisée", "To"),
      texte("raid", "Groupes de disques et RAID", "RAID 6, 8 × 12 To"),
      texte("protocoles", "Protocoles", "SMB, NFS, iSCSI"),
    ],
  },
  {
    type: "san",
    nom: "SAN",
    court: "SAN",
    categorie: "stockage",
    icone: "database",
    ports: [
      groupe("Fibre Channel", "FC{p}", 4, "stockage", { connecteur: "LC", vitesse: 32000, role: "stockage" }),
      cuivre("Gestion", "MGMT", 1, { role: "gestion" }),
    ],
    champs: [
      nombre("capaciteTo", "Capacité totale", "To"),
      nombre("utiliseTo", "Capacité utilisée", "To"),
      texte("protocoles", "Protocoles", "FC, iSCSI"),
    ],
  },
  {
    type: "baie-stockage",
    nom: "Baie de stockage",
    court: "Baie",
    categorie: "stockage",
    icone: "database",
    ports: [
      groupe("Contrôleur A", "A{p}", 2, "fibre", { connecteur: "SFP+", vitesse: 10000, role: "stockage" }),
      groupe("Contrôleur B", "B{p}", 2, "fibre", { connecteur: "SFP+", vitesse: 10000, role: "stockage" }),
      cuivre("Gestion", "MGMT", 1, { role: "gestion" }),
    ],
    champs: [
      nombre("capaciteTo", "Capacité totale", "To"),
      nombre("utiliseTo", "Capacité utilisée", "To"),
      texte("raid", "Groupes de disques et RAID"),
    ],
  },
  {
    type: "controleur-stockage",
    nom: "Contrôleur de stockage",
    court: "CTRL",
    categorie: "stockage",
    icone: "cpu",
    ports: [groupe("Ports", "port{p}", 2, "stockage", { role: "stockage" })],
    champs: [texte("protocole", "Protocole", "SAS, FC, iSCSI")],
  },
  {
    type: "switch-stockage",
    nom: "Switch de stockage",
    court: "SAN-SW",
    categorie: "stockage",
    icone: "kyfrev-switch",
    empilable: true,
    ports: [groupe("Ports", "{p}", 24, "stockage", { connecteur: "SFP", vitesse: 32000, role: "stockage" })],
    champs: [choix("fabric", "Fabric", ["A", "B", "Unique"])],
  },
  {
    type: "sauvegarde",
    nom: "Serveur ou appliance de sauvegarde",
    court: "BKP",
    categorie: "stockage",
    icone: "archive-restore",
    ports: [cuivre("Réseau", "eth{p}", 2, { premier: 0 })],
    champs: [
      texte("logiciel", "Logiciel"),
      texte("retention", "Rétention"),
      nombre("capaciteTo", "Capacité", "To"),
    ],
  },
  {
    type: "bibliotheque-bandes",
    nom: "Bibliothèque de bandes",
    court: "LTO",
    categorie: "stockage",
    icone: "cassette-tape",
    ports: [groupe("Fibre Channel", "FC{p}", 2, "stockage", { role: "stockage" })],
    champs: [
      texte("format", "Format", "LTO-9"),
      nombre("lecteurs", "Lecteurs"),
      nombre("emplacements", "Emplacements"),
    ],
  },

  // --- Terminaux et périphériques ---
  {
    type: "pc",
    nom: "PC fixe",
    court: "PC",
    categorie: "terminaux",
    icone: "monitor",
    ports: [cuivre("Ethernet", "Ethernet", 1), groupe("Wi-Fi", "Wi-Fi", 1, "radio")],
    champs: [texte("utilisateur", "Utilisateur")],
  },
  {
    type: "portable",
    nom: "Ordinateur portable",
    court: "Portable",
    categorie: "terminaux",
    icone: "laptop",
    ports: [groupe("Wi-Fi", "Wi-Fi", 1, "radio"), cuivre("Ethernet", "Ethernet", 1)],
    champs: [texte("utilisateur", "Utilisateur")],
  },
  {
    type: "client-leger",
    nom: "Client léger",
    court: "CL",
    categorie: "terminaux",
    icone: "tv-minimal",
    ports: [cuivre("Ethernet", "Ethernet", 1)],
    champs: [texte("utilisateur", "Utilisateur"), texte("serveur", "Serveur de sessions")],
  },
  {
    type: "telephone-ip",
    nom: "Téléphone IP",
    court: "Tél",
    categorie: "terminaux",
    icone: "kyfrev-telephone",
    ports: [cuivre("Réseau", "LAN", 1), cuivre("Poste de travail", "PC", 1)],
    champs: [texte("extension", "Numéro interne"), texte("sip", "Serveur SIP")],
  },
  {
    type: "mobile",
    nom: "Téléphone mobile ou tablette",
    court: "Mobile",
    categorie: "terminaux",
    icone: "tablet-smartphone",
    ports: [groupe("Wi-Fi", "Wi-Fi", 1, "radio"), groupe("Cellulaire", "Cellulaire", 1, "radio")],
    champs: [texte("utilisateur", "Utilisateur")],
  },
  {
    type: "imprimante",
    nom: "Imprimante ou multifonction",
    court: "IMP",
    categorie: "terminaux",
    icone: "printer",
    ports: [cuivre("Ethernet", "Ethernet", 1), groupe("Wi-Fi", "Wi-Fi", 1, "radio")],
    champs: [ouiNon("couleur", "Couleur"), texte("file", "File d'impression")],
  },
  {
    type: "camera-ip",
    nom: "Caméra IP",
    court: "CAM",
    categorie: "terminaux",
    icone: "cctv",
    ports: [cuivre("Ethernet", "eth{p}", 1, { premier: 0 })],
    champs: [texte("resolution", "Résolution"), texte("enregistreur", "Enregistreur (NVR)")],
  },
  {
    type: "iot",
    nom: "Équipement IoT",
    court: "IoT",
    categorie: "terminaux",
    icone: "thermometer",
    ports: [groupe("Radio", "radio{p}", 1, "radio", { premier: 0 })],
    champs: [texte("protocole", "Protocole", "Wi-Fi, Zigbee, LoRa…")],
  },
  {
    type: "generique",
    nom: "Équipement générique",
    court: "Équipement",
    categorie: "terminaux",
    icone: "box",
    ports: [cuivre("Ports", "eth{p}", 1, { premier: 0 })],
    champs: [texte("fonction", "Fonction")],
  },

  // --- Infrastructure ---
  {
    type: "baie",
    nom: "Baie ou rack",
    court: "Baie",
    categorie: "infrastructure",
    icone: "rows-3",
    ports: [],
    champs: [nombre("hauteurU", "Hauteur", "U"), texte("emplacement", "Emplacement")],
  },
  {
    type: "panneau-brassage",
    nom: "Panneau de brassage",
    court: "PB",
    categorie: "infrastructure",
    icone: "kyfrev-panneau",
    multiConnexion: true,
    ports: [cuivre("Ports", "{p}", 24, { vitesse: undefined })],
    champs: [texte("categorieCablage", "Catégorie du câblage", "Cat6a"), texte("reperage", "Repérage")],
  },
  {
    type: "prise-reseau",
    nom: "Prise réseau",
    court: "Prise",
    categorie: "infrastructure",
    icone: "ethernet-port",
    multiConnexion: true,
    ports: [cuivre("Prise", "Prise", 1, { vitesse: undefined })],
    champs: [texte("repere", "Repère"), texte("piece", "Pièce")],
  },
  {
    type: "onduleur",
    nom: "Onduleur",
    court: "UPS",
    categorie: "infrastructure",
    icone: "battery-charging",
    ports: [cuivre("Gestion", "Réseau", 1, { role: "gestion", vitesse: 100 })],
    champs: [nombre("puissanceVA", "Puissance", "VA"), nombre("autonomie", "Autonomie", "min")],
  },
  {
    type: "pdu",
    nom: "PDU",
    court: "PDU",
    categorie: "infrastructure",
    icone: "plug-zap",
    ports: [cuivre("Gestion", "Réseau", 1, { role: "gestion", vitesse: 100 })],
    champs: [nombre("prises", "Prises"), ouiNon("commutable", "Prises commutables")],
  },

  // --- Réseaux extérieurs ---
  {
    type: "internet",
    nom: "Internet",
    court: "Internet",
    categorie: "contexte",
    icone: "globe",
    multiConnexion: true,
    ports: [groupe("Accès", "Internet", 1, "virtuel", { role: "wan" })],
    champs: [],
  },
  {
    type: "reseau-operateur",
    nom: "Réseau opérateur",
    court: "Opérateur",
    categorie: "contexte",
    icone: "radio-tower",
    multiConnexion: true,
    ports: [groupe("Accès", "Réseau", 1, "virtuel", { role: "wan" })],
    champs: [texte("operateur", "Opérateur"), texte("service", "Service", "MPLS, fibre dédiée…")],
  },
  {
    type: "reseau-distant",
    nom: "Réseau distant",
    court: "Site distant",
    categorie: "contexte",
    icone: "house",
    multiConnexion: true,
    ports: [groupe("Accès", "Réseau", 1, "virtuel", { role: "wan" })],
    champs: [texte("sousReseaux", "Sous-réseaux")],
  },
  {
    type: "cloud",
    nom: "Service cloud",
    court: "Cloud",
    categorie: "contexte",
    icone: "cloud",
    multiConnexion: true,
    ports: [groupe("Accès", "Réseau", 1, "virtuel", { role: "wan" })],
    champs: [texte("fournisseur", "Fournisseur"), texte("region", "Région")],
  },
];

const PAR_TYPE = new Map(TYPES.map((d) => [d.type, d]));
const GENERIQUE = PAR_TYPE.get("generique") as DefinitionType;

/** Définition d'un type ; un type inconnu (schéma plus récent) se lit comme un équipement générique. */
export function definition(type: string): DefinitionType {
  return PAR_TYPE.get(type) ?? GENERIQUE;
}

export const typeConnu = (type: string): boolean => PAR_TYPE.has(type);

/** Types d'une catégorie, dans l'ordre du catalogue. */
export const typesDe = (categorie: Categorie): DefinitionType[] =>
  TYPES.filter((d) => d.categorie === categorie);

/** Recherche dans la bibliothèque, sans casse ni accents. */
export function chercherTypes(recherche: string): DefinitionType[] {
  const plat = (s: string) => s.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase();
  const mots = plat(recherche).split(/\s+/).filter(Boolean);
  if (mots.length === 0) return TYPES;
  return TYPES.filter((d) => {
    const texte = plat(`${d.nom} ${d.court} ${d.type}`);
    return mots.every((m) => texte.includes(m));
  });
}
