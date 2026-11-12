// Modèle métier des schémas réseau (D39, format 2). Il ne dépend d'aucun
// dessin : le canevas et l'aperçu le lisent, ils ne le définissent pas.
//
// Un schéma est rangé dans `schemas/<id>.json` du coffre ; une fiche n'en
// garde qu'une référence. Les objets ont des identifiants internes stables
// (« d_… », « i_… »…), indépendants des noms affichés : renommer un
// équipement ou une interface ne casse aucun lien.

export const FORMAT = 2;

export type Point = { x: number; y: number };

/** Statut documenté d'un équipement (rien n'est supervisé). */
export type Statut = "prevu" | "actif" | "maintenance" | "hors-service";
export const STATUTS: { valeur: Statut; nom: string }[] = [
  { valeur: "prevu", nom: "Prévu" },
  { valeur: "actif", nom: "Actif" },
  { valeur: "maintenance", nom: "En maintenance" },
  { valeur: "hors-service", nom: "Hors service" },
];

/** Statut documenté d'un lien ou d'une relation. */
export type StatutLien = "prevu" | "actif" | "indisponible";
export const STATUTS_LIEN: { valeur: StatutLien; nom: string }[] = [
  { valeur: "prevu", nom: "Prévu" },
  { valeur: "actif", nom: "Actif" },
  { valeur: "indisponible", nom: "Indisponible" },
];

// --- Interfaces ---------------------------------------------------------------

/** Nature physique d'une interface (support du port). */
export type GenreInterface =
  | "cuivre"
  | "fibre"
  | "radio"
  | "virtuel"
  | "console"
  | "stockage"
  | "usb"
  | "autre";
export const GENRES_INTERFACE: { valeur: GenreInterface; nom: string }[] = [
  { valeur: "cuivre", nom: "Cuivre (RJ45)" },
  { valeur: "fibre", nom: "Fibre / logement optique" },
  { valeur: "radio", nom: "Radio (Wi-Fi, cellulaire)" },
  { valeur: "virtuel", nom: "Virtuelle" },
  { valeur: "console", nom: "Console" },
  { valeur: "stockage", nom: "Stockage (Fibre Channel, SAS)" },
  { valeur: "usb", nom: "USB" },
  { valeur: "autre", nom: "Autre" },
];

/** Place de l'interface dans l'équipement. */
export type NatureInterface = "physique" | "sous-interface" | "vlan" | "virtuelle" | "boucle";
export const NATURES_INTERFACE: { valeur: NatureInterface; nom: string }[] = [
  { valeur: "physique", nom: "Physique" },
  { valeur: "sous-interface", nom: "Sous-interface" },
  { valeur: "vlan", nom: "Interface VLAN (SVI)" },
  { valeur: "virtuelle", nom: "Virtuelle" },
  { valeur: "boucle", nom: "Bouclage (loopback)" },
];

export type RoleInterface = "acces" | "uplink" | "gestion" | "wan" | "lan" | "stockage" | "console";
export const ROLES_INTERFACE: { valeur: RoleInterface; nom: string }[] = [
  { valeur: "acces", nom: "Accès" },
  { valeur: "uplink", nom: "Uplink" },
  { valeur: "gestion", nom: "Gestion" },
  { valeur: "wan", nom: "WAN" },
  { valeur: "lan", nom: "LAN" },
  { valeur: "stockage", nom: "Stockage" },
  { valeur: "console", nom: "Console" },
];

/** Adresse IP (v4 ou v6, reconnue à son écriture) et longueur de préfixe. */
export type Adresse = { adresse: string; prefixe?: number };

export type ModeVlan = "acces" | "trunk" | "hybride";
/**
 * Configuration VLAN d'une extrémité (une interface ou un agrégat). Les
 * listes s'écrivent comme on les lit : « 10,20,30-40 ».
 */
export type ConfigVlan = {
  mode: ModeVlan;
  /** VLAN non tagué d'un port d'accès (ou d'un port hybride). */
  acces?: number;
  /** VLAN natif d'un trunk. */
  natif?: number;
  /** VLAN tagués autorisés. */
  tagues?: string;
  voix?: number;
  pvid?: number;
};

export type Poe = { fournit?: boolean; recoit?: boolean; norme?: string; puissanceW?: number };

export type Interface = {
  id: string;
  nom: string;
  /** Membre de stack (1, 2…) ; absent pour un équipement seul. */
  membre?: number;
  module?: string;
  numero?: number;
  /**
   * Clé de génération (« 1:0:5 » : membre, groupe de ports, numéro) d'une
   * interface créée d'après un profil ; sert à la régénération. Absente pour
   * une interface ajoutée à la main.
   */
  origine?: string;
  /** Nom tiré du motif du profil (il suit le motif) ; faux une fois renommé à la main. */
  nomAuto?: boolean;
  genre: GenreInterface;
  nature: NatureInterface;
  connecteur?: string;
  /** Vitesse nominale, en Mbit/s. */
  vitesse?: number;
  role?: RoleInterface;
  /** Interface parente (sous-interface, interface VLAN). */
  parent?: string;
  admin: "actif" | "desactive";
  /** État documenté (pas mesuré). */
  oper?: "up" | "down" | "inconnu";
  description?: string;
  macs: string[];
  adresses: Adresse[];
  vlan?: ConfigVlan;
  poe?: Poe;
  /**
   * Exception explicite : accepte plusieurs câbles (concentrateur, bus,
   * nuage opérateur). Une interface physique point à point n'en prend qu'un.
   */
  multiConnexion?: boolean;
};

// --- Modèles matériels et membres ------------------------------------------------

/**
 * Un groupe de ports d'un profil : `motif` donne le nom affiché, avec
 * `{m}` pour le membre et `{p}` pour le numéro (« {m}/{p}», « Gi{m}/0/{p} »,
 * « LAN{p} », « vmnic{p} »).
 */
export type GroupePorts = {
  nom: string;
  motif: string;
  nombre: number;
  premier: number;
  genre: GenreInterface;
  connecteur?: string;
  vitesse?: number;
  role?: RoleInterface;
  poe?: boolean;
};

/** Caractéristiques communes, partagées par plusieurs équipements. */
export type ModeleMateriel = {
  id: string;
  /** Type d'équipement du catalogue (« switch »…). */
  type: string;
  fabricant?: string;
  reference: string;
  description?: string;
  groupes: GroupePorts[];
};

/** Membre d'un stack, ou l'équipement lui-même (membre 1). */
export type Membre = {
  numero: number;
  nom?: string;
  /** Modèle matériel du membre ; sinon, ses propres groupes de ports. */
  modele?: string;
  groupes?: GroupePorts[];
  serie?: string;
};

// --- Équipements -----------------------------------------------------------------

export type Valeur = string | number | boolean;

export type Equipement = {
  id: string;
  /** Type du catalogue (catalogue.ts) ; un type inconnu reste lisible (« générique »). */
  type: string;
  nom: string;
  nomHote?: string;
  fabricant?: string;
  reference?: string;
  description?: string;
  role?: string;
  inventaire?: string;
  serie?: string;
  site?: string;
  salle?: string;
  baie?: string;
  positionRack?: string;
  statut: Statut;
  systeme?: string;
  contact?: string;
  tags: string[];
  notes?: string;
  documentation?: string;
  garantie?: string;
  /** Référence dans un gestionnaire de secrets (jamais le secret lui-même). */
  referenceSecret?: string;
  gestion?: { interface?: string; adresse?: string; vlan?: number };
  passerelle?: string;
  dns?: string;
  vrf?: string;
  poeBudgetW?: number;
  /** Propriétés propres au type (champs du catalogue). */
  proprietes: Record<string, Valeur>;
  membres: Membre[];
  interfaces: Interface[];
  position: Point;
  verrou?: boolean;
  /** Hôte replié : ce qu'il héberge est masqué. */
  replie?: boolean;
};

// --- Liens physiques et relations logiques ---------------------------------------

export type Extremite = { equipement: string; interface: string };

/** Support physique d'un lien (distinct du protocole et de la configuration). */
export type Support =
  | "cuivre"
  | "fibre-monomode"
  | "fibre-multimode"
  | "dac"
  | "aoc"
  | "radio"
  | "cellulaire"
  | "fibre-channel"
  | "usb"
  | "console"
  | "virtuel"
  | "operateur"
  | "personnalise";
export const SUPPORTS: { valeur: Support; nom: string }[] = [
  { valeur: "cuivre", nom: "Cuivre (Ethernet)" },
  { valeur: "fibre-monomode", nom: "Fibre monomode" },
  { valeur: "fibre-multimode", nom: "Fibre multimode" },
  { valeur: "dac", nom: "Câble DAC" },
  { valeur: "aoc", nom: "Câble AOC" },
  { valeur: "radio", nom: "Liaison radio point à point" },
  { valeur: "cellulaire", nom: "Cellulaire" },
  { valeur: "fibre-channel", nom: "Fibre Channel" },
  { valeur: "usb", nom: "USB" },
  { valeur: "console", nom: "Console" },
  { valeur: "virtuel", nom: "Connexion virtuelle" },
  { valeur: "operateur", nom: "Interconnexion opérateur / WAN" },
  { valeur: "personnalise", nom: "Personnalisé" },
];

export type StyleTrait = "plein" | "tirets" | "points";
/** Tracé d'un câble : droit, ou coudé à angle droit (D44). */
export type FormeLien = "droite" | "coudee";
export type AffichageLibelles = "aucun" | "ports" | "nom" | "tout";

export type Lien = {
  id: string;
  a: Extremite;
  b: Extremite;
  support: Support;
  supportPerso?: string;
  /** Catégorie d'un câble cuivre (« Cat6a »). */
  categorie?: string;
  /** Technologie ou protocole (« 1000BASE-T », « 10GBASE-SR »). */
  technologie?: string;
  /** Débit nominal documenté, en Mbit/s. */
  debit?: number;
  longueurM?: number;
  /** Référence ou étiquette du câble. */
  etiquette?: string;
  nom?: string;
  description?: string;
  statut: StatutLien;
  /** Fibre : connecteurs et modules optiques à chaque extrémité. */
  optique?: { connecteurA?: string; connecteurB?: string; moduleA?: string; moduleB?: string };
  /** Données mesurées (distinctes du documenté) : valeur, date, source. */
  mesure?: { debit?: number; date?: string; source?: string };
  couleur?: string;
  trait?: StyleTrait;
  libelles?: AffichageLibelles;
  /** Tracé propre à ce câble ; sinon, celui du schéma. */
  forme?: FormeLien;
  /** Points de passage du tracé. */
  points: Point[];
  /**
   * Palier d'un câble coudé en Z, coulissé à la main (D53) : la position de
   * son segment du milieu (une ordonnée pour un Z vertical, une abscisse pour
   * un Z horizontal). Il reste en place quand les équipements bougent.
   */
  palier?: { axe: "x" | "y"; position: number };
  notes?: string;
};

/** Relations logiques : jamais confondues avec un câble. */
export type GenreRelation =
  | "hebergement"
  | "cluster"
  | "association-wifi"
  | "tunnel"
  | "stockage"
  | "reseau-docker"
  | "volume"
  | "dependance"
  | "operateur"
  | "haute-disponibilite";
export const GENRES_RELATION: { valeur: GenreRelation; nom: string }[] = [
  { valeur: "hebergement", nom: "Hébergement" },
  { valeur: "cluster", nom: "Appartenance à un cluster" },
  { valeur: "association-wifi", nom: "Association Wi-Fi" },
  { valeur: "tunnel", nom: "Tunnel VPN" },
  { valeur: "stockage", nom: "Accès au stockage" },
  { valeur: "reseau-docker", nom: "Réseau Docker" },
  { valeur: "volume", nom: "Volume" },
  { valeur: "dependance", nom: "Dépendance applicative" },
  { valeur: "operateur", nom: "Service opérateur" },
  { valeur: "haute-disponibilite", nom: "Haute disponibilité (paire actif/passif)" },
];
/** Relations sans sens : dessinées sans flèche. */
export const RELATIONS_SYMETRIQUES = new Set<GenreRelation>(["haute-disponibilite"]);

export type Relation = {
  id: string;
  genre: GenreRelation;
  de: string;
  vers: string;
  deInterface?: string;
  versInterface?: string;
  nom?: string;
  description?: string;
  statut: StatutLien;
  proprietes: Record<string, Valeur>;
  points: Point[];
};

// --- VLAN, agrégats, groupes, annotations, vues ----------------------------------

export type Vlan = {
  id: string;
  numero: number;
  nom: string;
  description?: string;
  couleur?: string;
  sousReseaux: string[];
  /** Site ou périmètre : deux VLAN de même numéro dans deux périmètres sont distincts. */
  perimetre?: string;
};

export type Agregat = {
  id: string;
  equipement: string;
  nom: string;
  mode: "statique" | "lacp";
  lacp?: "actif" | "passif";
  membres: string[];
  vlan?: ConfigVlan;
  /** Représentation condensée : un seul trait pour les liens membres. */
  condense?: boolean;
};

export type GenreGroupe = "site" | "batiment" | "salle" | "zone" | "groupe";
export const GENRES_GROUPE: { valeur: GenreGroupe; nom: string }[] = [
  { valeur: "site", nom: "Site" },
  { valeur: "batiment", nom: "Bâtiment" },
  { valeur: "salle", nom: "Salle" },
  { valeur: "zone", nom: "Zone" },
  { valeur: "groupe", nom: "Groupe visuel" },
];

export type Groupe = {
  id: string;
  nom: string;
  genre: GenreGroupe;
  /** Groupe englobant (groupes imbriqués). */
  parent?: string;
  /** Équipements du groupe (l'appartenance ne dépend pas de la géométrie). */
  membres: string[];
  position: Point;
  taille: { largeur: number; hauteur: number };
  couleur?: string;
  replie?: boolean;
  verrou?: boolean;
};

/**
 * Éléments groupés : ils se choisissent, se déplacent, se copient et se retirent ensemble, et se dégroupent sans rien
 * changer d'autre. Rien n'est dessiné. Un élément (équipement, annotation ou cadre) n'est que dans un ensemble.
 */
export type Ensemble = {
  id: string;
  membres: string[];
};

/**
 * Annotations libres (D46) : note (cadre et texte), texte seul, zone
 * rectangulaire ou elliptique (largeur × hauteur, texte en légende), flèche
 * (de `position` à `vers`, texte au milieu).
 */
export type FormeAnnotation = "note" | "texte" | "rectangle" | "ellipse" | "fleche";
export const FORMES_ANNOTATION: { valeur: FormeAnnotation; nom: string }[] = [
  { valeur: "note", nom: "Note" },
  { valeur: "texte", nom: "Texte libre" },
  { valeur: "rectangle", nom: "Zone rectangulaire" },
  { valeur: "ellipse", nom: "Zone ronde" },
  { valeur: "fleche", nom: "Flèche" },
];
/** Couleurs proposées pour les annotations (celles des familles, et un jaune). */
export const COULEURS_ANNOTATION: { valeur: string; nom: string }[] = [
  { valeur: "#1f62b0", nom: "Bleu" },
  { valeur: "#c0392b", nom: "Rouge" },
  { valeur: "#167a5a", nom: "Vert" },
  { valeur: "#a35a00", nom: "Orange" },
  { valeur: "#6a3fb0", nom: "Violet" },
  { valeur: "#b7950b", nom: "Jaune" },
  { valeur: "#4b5563", nom: "Gris" },
];
export type Annotation = {
  id: string;
  texte: string;
  position: Point;
  largeur?: number;
  forme?: FormeAnnotation;
  hauteur?: number;
  couleur?: string;
  /** Pointe d'une flèche. */
  vers?: Point;
  /** Taille du texte (texte libre). */
  taille?: number;
};

/** Une vue filtre un même modèle : rien n'est copié. */
export type IdVue = "physique" | "logique" | "virtualisation" | "stockage" | "simplifiee";
export type Vue = {
  id: IdVue;
  nom: string;
  /** Liens physiques montrés. */
  liens: boolean;
  /** Genres de relations logiques montrés. */
  relations: GenreRelation[];
  /** Noms d'interfaces aux extrémités des liens. */
  ports: boolean;
  /** Couleurs de VLAN sur les liens. */
  vlans: boolean;
};

export type Schema = {
  format: typeof FORMAT;
  id: string;
  titre: string;
  description?: string;
  modeles: ModeleMateriel[];
  equipements: Equipement[];
  liens: Lien[];
  relations: Relation[];
  vlans: Vlan[];
  agregats: Agregat[];
  groupes: Groupe[];
  annotations: Annotation[];
  /** Éléments groupés ; la clé n'est écrite que s'il y en a. */
  ensembles?: Ensemble[];
  /** Vue affichée à l'ouverture et dans l'aperçu des fiches. */
  vue: IdVue;
  /** Câbles coudés à angle droit par défaut (D44). */
  cablesCoudes?: boolean;
  /** Ce qui est affiché (D50) ; sinon, ce que prévoit la vue. */
  affichage?: Partial<Affichage>;
};

/** Informations affichées sur le schéma (D50), à l'écran comme à l'export. */
export type Affichage = {
  ports: boolean;
  vlans: boolean;
  adresses: boolean;
  nomsCables: boolean;
  annotations: boolean;
  zones: boolean;
};
export const AFFICHAGES: { valeur: keyof Affichage; nom: string }[] = [
  { valeur: "ports", nom: "Noms des ports" },
  { valeur: "vlans", nom: "VLAN des câbles" },
  { valeur: "adresses", nom: "Adresses IP" },
  { valeur: "nomsCables", nom: "Noms des câbles et relations" },
  { valeur: "annotations", nom: "Notes, textes et flèches" },
  { valeur: "zones", nom: "Zones" },
];

export const schemaVide = (titre = "Schéma réseau"): Schema => ({
  format: FORMAT,
  id: "",
  titre,
  modeles: [],
  equipements: [],
  liens: [],
  relations: [],
  vlans: [],
  agregats: [],
  groupes: [],
  annotations: [],
  vue: "physique",
});

// --- Identifiants -------------------------------------------------------------------

const ALPHABET = "0123456789abcdefghijklmnopqrstuvwxyz";

/** Identifiant interne neuf (« d_k3x9… ») ; jamais réutilisé ni recalculé. */
export function nouvelId(prefixe: "d" | "i" | "l" | "r" | "v" | "a" | "g" | "n" | "m" | "e"): string {
  const octets = new Uint8Array(10);
  crypto.getRandomValues(octets);
  let suite = "";
  for (const octet of octets) suite += ALPHABET[octet % ALPHABET.length];
  return `${prefixe}_${suite}`;
}

/** L'équipement d'identifiant `id`. */
export const equipementDe = (schema: Schema, id: string): Equipement | undefined =>
  schema.equipements.find((e) => e.id === id);

/** L'interface `id` de l'équipement `equipement`. */
export function interfaceDe(schema: Schema, extremite: Extremite): Interface | undefined {
  return equipementDe(schema, extremite.equipement)?.interfaces.find((i) => i.id === extremite.interface);
}

/** Liens physiques branchés sur une interface. */
export const liensDeLInterface = (schema: Schema, equipement: string, interfaceId: string): Lien[] =>
  schema.liens.filter(
    (l) =>
      (l.a.equipement === equipement && l.a.interface === interfaceId) ||
      (l.b.equipement === equipement && l.b.interface === interfaceId),
  );

/** Une interface est-elle occupée (un câble y est branché) ? */
export const interfaceOccupee = (schema: Schema, equipement: string, interfaceId: string): boolean =>
  liensDeLInterface(schema, equipement, interfaceId).length > 0;

/** Vitesse lisible : « 1 Gbit/s », « 100 Mbit/s ». */
export function vitesseEnClair(mbits: number | undefined): string {
  if (!mbits) return "";
  if (mbits >= 1000) return `${(mbits / 1000).toLocaleString("fr-FR", { maximumFractionDigits: 1 })} Gbit/s`;
  return `${mbits} Mbit/s`;
}
