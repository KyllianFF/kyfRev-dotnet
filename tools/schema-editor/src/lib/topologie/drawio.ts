// Export d'un schéma au format draw.io (29D) : un fichier .drawio non compressé (XML mxGraph), à retoucher
// dans draw.io, ou dans Visio en passant par draw.io (qui ouvre et enregistre le .vsdx lui-même).
//
// draw.io montre ce que montre kyfRev, dans la vue exportée et avec l'affichage choisi du schéma : la mise en
// page vient de `mettreEnPage` (dessin.ts), la même qu'à l'écran. Ce qui sort :
// - un équipement est une forme, avec l'illustration de kyfRev (dans son carré de 56, la position de kyfRev
//   étant son centre) ou une forme réseau de draw.io (posée sur l'emprise de l'illustration, là où kyfRev la
//   dessine) ; son nom en gras, son adresse dessous en chasse fixe, sur une ligne ; ses données (nom, type,
//   adresse, VLAN, modèle, site, salle) dans « Modifier les données » ; la pastille « ×2 » d'un stack ;
// - un câble est un connecteur entre deux équipements, au tracé de kyfRev : ses bouts fixés là où kyfRev les
//   arrête, ses coudes et son décalage (câbles parallèles) en points de passage ; un agrégat a un connecteur
//   par trait et son cercle ; ses étiquettes (ports, nom, VLAN) sont des pastilles posées où kyfRev les pose ;
// - une relation, un connecteur en tirets ; une zone (groupe), un conteneur ; une annotation, un texte, un
//   cadre ou une flèche libre ; ce que la vue ou l'affichage cache est dans le fichier, caché ;
// - tout le modèle de kyfRev (le JSON du « Modèle complet ») est gardé dans l'attribut `kyfrev` du calque :
//   l'import le reprend et n'applique que ce qui a changé dans draw.io. Le calque est un `<object>` parce que
//   draw.io garde, en réenregistrant, les attributs inconnus des cellules qui portent des données, et qu'une
//   cellule nue (`<mxCell id="1"/>`) n'en a pas ; l'en-tête `<diagram>` ou `<mxfile>`, lui, est réécrit.
//   Ce qui n'est là que pour l'œil (pastilles, traits d'un agrégat, cercle, pastille d'un stack) porte la
//   donnée `decor` : l'import ne le prend jamais pour un objet du schéma.

import { definition } from "./catalogue";
import {
  adresseAffichee,
  boiteDe,
  type Cadre,
  COTE,
  COULEUR_MAINTENANCE,
  cadreAnnotation,
  cadreEtiquette,
  couleurDuType,
  type Etiquette,
  estCoude,
  estZone,
  type MiseEnPage,
  masques,
  mettreEnPage,
  nombreDeMembres,
  type Trait,
  traitDeRelation,
  traitDuLien,
  traitDuRenvoi,
} from "./dessin";
import { ecrireSchema } from "./format";
import { DEFINITIONS_ILLUSTRATIONS, illustration } from "./illustrations";
import {
  type Annotation,
  type Equipement,
  type Groupe,
  interfaceDe,
  type Lien,
  type Point,
  type Relation,
  RELATIONS_SYMETRIQUES,
  type Schema,
  type Vue,
} from "./modele";
import { aPlusieursPages, pagesDe, type Renvoi, renvoisDeLaPage, surLaPage } from "./pages";
import { vue as vueDe } from "./vues";
import { echapperXml } from "./xml";

/** Les icônes d'un export draw.io : les illustrations de kyfRev, ou les formes réseau de draw.io. */
export type IconesDrawio = "kyfrev" | "drawio";
export const ICONES_DRAWIO: { valeur: IconesDrawio; nom: string }[] = [
  { valeur: "kyfrev", nom: "Icônes de kyfRev" },
  { valeur: "drawio", nom: "Formes réseau de draw.io" },
];

/**
 * Type du catalogue → forme réseau de draw.io (bibliothèques « Cisco » et « Networks »). Un type absent se
 * dessine en rectangle arrondi. Quand deux types partagent une forme, le premier écrit est celui que l'import
 * reconnaît (sauf préférence ci-dessous). C'est la seule table : l'export la lit, l'import la lit à l'envers.
 */
export const FORMES_DRAWIO: Record<string, string> = {
  switch: "mxgraph.cisco.switches.workgroup_switch",
  "switch-non-administrable": "mxgraph.cisco.hubs_and_gateways.small_hub",
  "switch-coeur": "mxgraph.cisco.switches.layer_3_switch",
  "switch-distribution": "mxgraph.cisco.switches.layer_3_switch",
  routeur: "mxgraph.cisco.routers.router",
  "pare-feu": "mxgraph.cisco.security.firewall",
  cpe: "mxgraph.cisco.modems_and_phones.cable_modem",
  modem: "mxgraph.networks.modem",
  "sd-wan": "mxgraph.cisco.hubs_and_gateways.universal_gateway",
  "passerelle-vpn": "mxgraph.cisco.hubs_and_gateways.vpn_gateway",
  "cato-socket": "mxgraph.cisco.misc.vpn_concentrator",
  "point-acces": "mxgraph.cisco.misc.access_point",
  "controleur-wifi": "mxgraph.networks.wireless_hub",
  repartiteur: "mxgraph.networks.load_balancer",
  "equipement-reseau": "mxgraph.cisco.hubs_and_gateways.generic_gateway",
  serveur: "mxgraph.cisco.servers.fileserver",
  hyperviseur: "mxgraph.networks.server",
  esxi: "mxgraph.networks.server",
  proxmox: "mxgraph.networks.server",
  "hyper-v": "mxgraph.networks.server",
  "cluster-virtualisation": "mxgraph.networks.server",
  "hote-docker": "mxgraph.networks.server",
  "machine-virtuelle": "mxgraph.networks.virtual_server",
  nas: "mxgraph.networks.nas_filer",
  "baie-stockage": "mxgraph.networks.storage",
  san: "mxgraph.networks.storage",
  "controleur-stockage": "mxgraph.networks.server_storage",
  "switch-stockage": "mxgraph.networks.switch",
  sauvegarde: "mxgraph.networks.external_storage",
  "bibliotheque-bandes": "mxgraph.networks.tape_storage",
  pc: "mxgraph.cisco.computers_and_peripherals.pc",
  portable: "mxgraph.cisco.computers_and_peripherals.laptop",
  "client-leger": "mxgraph.cisco.computers_and_peripherals.terminal",
  "telephone-ip": "mxgraph.cisco.modems_and_phones.ip_phone",
  mobile: "mxgraph.cisco.modems_and_phones.cell_phone",
  imprimante: "mxgraph.cisco.computers_and_peripherals.printer",
  "camera-ip": "mxgraph.networks.security_camera",
  baie: "mxgraph.networks.rack",
  "panneau-brassage": "mxgraph.networks.patch_panel",
  onduleur: "mxgraph.networks.ups_enterprise",
  cloud: "mxgraph.cisco.storage.cloud",
  "reseau-operateur": "mxgraph.cisco.storage.cloud",
  internet: "mxgraph.networks.cloud",
  "reseau-distant": "mxgraph.networks.cloud",
};

/** À l'import, une forme partagée par plusieurs types donne celui-ci (le plus courant). */
const PREFERENCES: Record<string, string> = {
  "mxgraph.networks.server": "serveur",
  "mxgraph.networks.storage": "baie-stockage",
};

/** Le type que donne une forme de draw.io (nom exact de la table), ou rien. */
export function typeDeLaForme(forme: string): string | undefined {
  const cle = forme.trim().toLowerCase();
  if (PREFERENCES[cle]) return PREFERENCES[cle];
  return Object.entries(FORMES_DRAWIO).find(([, f]) => f === cle)?.[0];
}

/** Une forme hors de la table : le type se devine aux mots de son nom (« router », « firewall »…). */
const MOTS: [RegExp, string][] = [
  [/firewall|pare.?feu/, "pare-feu"],
  [/router|routeur/, "routeur"],
  [/layer_?3|multilayer|core_switch/, "switch-coeur"],
  [/switch|commutateur/, "switch"],
  [/access_point|wireless|wifi|wi-fi/, "point-acces"],
  [/modem/, "modem"],
  [/load_?balancer/, "repartiteur"],
  [/vpn/, "passerelle-vpn"],
  [/laptop|notebook/, "portable"],
  [/cell_?phone|mobile|smartphone|tablet/, "mobile"],
  [/phone/, "telephone-ip"],
  [/printer|imprimante/, "imprimante"],
  [/camera/, "camera-ip"],
  [/(^|[._])(pc|desktop_pc|desktop|workstation|computer|terminal)($|[._])/, "pc"],
  [/nas/, "nas"],
  [/tape/, "bibliotheque-bandes"],
  [/storage|disk|san\b/, "baie-stockage"],
  [/virtual/, "machine-virtuelle"],
  [/server|serveur|mainframe/, "serveur"],
  [/docker|container/, "conteneur"],
  [/internet|globe/, "internet"],
  [/cloud|nuage/, "cloud"],
  [/ups|onduleur/, "onduleur"],
  [/patch_?panel/, "panneau-brassage"],
  [/rack/, "baie"],
  [/hub/, "switch-non-administrable"],
];

/** Le type deviné aux mots d'une forme, ou rien. */
export function typeDevine(forme: string): string | undefined {
  const nom = forme.toLowerCase();
  return typeDeLaForme(nom) ?? MOTS.find(([mot]) => mot.test(nom))?.[1];
}


// --- Écriture --------------------------------------------------------------------------------

const nombre = (n: number) => String(Math.round(n * 100) / 100);
const fraction = (n: number) => String(Math.round(n * 10_000) / 10_000);
/** Une couleur de dessin (« var(--x, #abc) ») ramenée à sa valeur claire, seule lisible par draw.io. */
const couleur = (valeur: string) => valeur.replace(/var\(--[\w-]+,\s*([^)]+)\)/, "$1").trim();
const style = (parties: Record<string, string | number | undefined>, debut = "") =>
  debut +
  Object.entries(parties)
    .filter(([, v]) => v !== undefined && v !== "")
    .map(([cle, v]) => `${cle}=${v};`)
    .join("");

/** Des données de cellule, en attributs : seulement les valeurs non vides. */
function attributs(donnees: Record<string, string | number | undefined>): string {
  return Object.entries(donnees)
    .filter(([, v]) => v !== undefined && String(v).trim() !== "")
    .map(([cle, v]) => ` ${cle}="${echapperXml(String(v))}"`)
    .join("");
}

const geometrie = (x: number, y: number, largeur: number, hauteur: number, dedans = "") =>
  `<mxGeometry x="${nombre(x)}" y="${nombre(y)}" width="${nombre(largeur)}" height="${nombre(hauteur)}" as="geometry"${
    dedans ? `>${dedans}</mxGeometry>` : "/>"
  }`;

const pointXml = (p: Point, role?: string) =>
  `<mxPoint x="${nombre(p.x)}" y="${nombre(p.y)}"${role ? ` as="${role}"` : ""}/>`;

const passages = (points: Point[]) =>
  points.length > 0 ? `<Array as="points">${points.map((p) => pointXml(p)).join("")}</Array>` : "";

const cache = (visible: boolean) => (visible ? "" : ' visible="0"');

/**
 * Les points de passage d'un connecteur tels qu'ils s'écrivent, au pixel près (« 120,80 300,80 ») : l'export en
 * garde la trace dans la donnée `trace`, l'import y compare ce qu'il lit pour savoir s'ils ont bougé dans draw.io.
 */
export const texteDuTrace = (points: Point[]): string =>
  points.map((p) => `${Math.round(Number(nombre(p.x)))},${Math.round(Number(nombre(p.y)))}`).join(" ");

/** Les couleurs claires de kyfRev (un fichier draw.io n'a pas de thème) : les valeurs de repli de ses variables. */
const POLICE = "Segoe UI";
const TEXTE = "#221d2e";
const SECONDAIRE = "#5d566d";
const BORDURE = "#dedae8";
const SURFACE = "#ffffff";
/** La couleur principale de kyfRev (vert forêt) : celle d'une zone qui n'a pas la sienne, comme à l'écran. */
const PRINCIPALE = "#2e6a4e";

/** L'illustration de kyfRev d'un type, en SVG autonome (data URI en base 64, comme draw.io les écrit). */
export function imageDuType(type: string): string {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${COTE}" height="${COTE}" viewBox="0 0 64 64"><defs>${DEFINITIONS_ILLUSTRATIONS}</defs>${illustration(definition(type).icone, couleurDuType(type))}</svg>`;
  const octets = new TextEncoder().encode(svg);
  let binaire = "";
  for (const octet of octets) binaire += String.fromCharCode(octet);
  // Pas de « ;base64 » : le point-virgule sépare les clés d'un style draw.io, qui lit ce data URI tel quel.
  return `data:image/svg+xml,${btoa(binaire)}`;
}

/**
 * Le cadre de la forme d'un équipement dans draw.io. L'illustration de kyfRev a son carré de 56 (elle y laisse du
 * blanc, comme à l'écran) ; une forme de draw.io remplit tout son cadre : elle prend l'emprise de l'illustration,
 * là où kyfRev arrête les câbles et pose le nom dessous.
 */
export function cadreDeLaForme(e: Pick<Equipement, "type" | "position">, icones: IconesDrawio): Cadre {
  if (icones === "kyfrev") return { x: e.position.x - COTE / 2, y: e.position.y - COTE / 2, largeur: COTE, hauteur: COTE };
  const b = boiteDe(e, 0);
  return { x: b.x0, y: b.y0, largeur: b.x1 - b.x0, hauteur: b.y1 - b.y0 };
}

/**
 * La position de kyfRev (le centre du carré) d'un équipement d'après le cadre de sa forme dans draw.io : l'inverse
 * de `cadreDeLaForme`. Un cadre à la taille de l'emprise de l'illustration en est l'emprise ; un autre (le carré
 * de 56, une forme agrandie dans draw.io), son centre.
 */
export function positionDeLaForme(type: string, cadre: Cadre): Point {
  const b = boiteDe({ type, position: { x: 0, y: 0 } }, 0);
  const emprise = Math.abs(cadre.largeur - (b.x1 - b.x0)) < 0.5 && Math.abs(cadre.hauteur - (b.y1 - b.y0)) < 0.5;
  return emprise
    ? { x: cadre.x - b.x0, y: cadre.y - b.y0 }
    : { x: cadre.x + cadre.largeur / 2, y: cadre.y + cadre.hauteur / 2 };
}

/** Échappe un texte pour un libellé HTML de draw.io (l'attribut, lui, est échappé ensuite pour XML). */
const html = (texte: string) => texte.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;");

/**
 * Le libellé d'un équipement, comme kyfRev l'écrit sous l'illustration : le nom en gras, l'adresse dessous en
 * chasse fixe (si le schéma montre les adresses). L'import en lit la première ligne : le nom.
 */
function libelleEquipement(e: Equipement, adresses: boolean): string {
  const adresse = adresses ? adresseAffichee(e) : "";
  return [
    `<span style="font-weight:600">${html(e.nom)}</span>`,
    adresse
      ? `<br><span style="font-family:Cascadia Mono,Consolas,monospace;font-size:11px;font-weight:normal;color:${SECONDAIRE}">${html(adresse)}</span>`
      : "",
  ].join("");
}

/** Le nom sous la forme, sur une ligne (un nom long ne se coupe pas sous l'icône), sur un fond qui cache les câbles. */
const LIBELLE_DESSOUS = {
  html: 1,
  whiteSpace: "nowrap",
  verticalLabelPosition: "bottom",
  verticalAlign: "top",
  labelPosition: "center",
  align: "center",
  spacing: 0,
  fontSize: 13,
  fontColor: TEXTE,
  fontFamily: POLICE,
  labelBackgroundColor: SURFACE,
};

function styleEquipement(e: Equipement, icones: IconesDrawio, images: Map<string, string>): string {
  const commun = {
    ...LIBELLE_DESSOUS,
    opacity: e.statut === "prevu" ? 55 : e.statut === "hors-service" ? 45 : undefined,
    textOpacity: e.statut === "hors-service" ? 45 : undefined,
  };
  const forme = FORMES_DRAWIO[e.type];
  if (icones === "drawio" && forme) {
    return style({
      shape: forme,
      sketch: 0,
      pointerEvents: 1,
      dashed: 0,
      fillColor: "#036897",
      strokeColor: "#ffffff",
      strokeWidth: 2,
      outlineConnect: 0,
      ...commun,
    });
  }
  if (icones === "drawio") {
    return style({ rounded: 1, fillColor: couleurDuType(e.type), strokeColor: "#ffffff", ...commun });
  }
  let image = images.get(e.type);
  if (!image) {
    image = imageDuType(e.type);
    images.set(e.type, image);
  }
  return style({ shape: "image", image, aspect: "fixed", imageAspect: 0, ...commun });
}

/** Le modèle matériel d'un équipement : sa référence, sinon celle du modèle de son premier membre. */
function modeleDe(schema: Schema, e: Equipement): string | undefined {
  if (e.reference) return e.reference;
  const id = e.membres[0]?.modele;
  return id ? schema.modeles.find((m) => m.id === id)?.reference : undefined;
}

/** Une forme posée sur un équipement (elle le suit dans draw.io) : son coin, compté depuis le coin de la forme. */
function decorDeLEquipement(e: Equipement, cadre: Cadre, decor: string, texte: string, corps: string, boite: Cadre): string {
  return `<object id="${echapperXml(`${e.id}-${decor}`)}" label="${echapperXml(texte)}" decor="${decor}"><mxCell style="${echapperXml(
    corps,
  )}" vertex="1" connectable="0" parent="${echapperXml(e.id)}"><mxGeometry width="${nombre(boite.largeur)}" height="${nombre(
    boite.hauteur,
  )}" relative="1" as="geometry">${pointXml({ x: boite.x - cadre.x, y: boite.y - cadre.y }, "offset")}</mxGeometry></mxCell></object>`;
}

/** Les repères que kyfRev pose sur un équipement : la pastille « ×2 » d'un stack, la maintenance, le cadre d'un prévu. */
function decorsDeLEquipement(e: Equipement, cadre: Cadre): string[] {
  const b = boiteDe(e, 0);
  const teinte = couleurDuType(e.type);
  const terni = e.statut === "hors-service" ? 45 : undefined;
  const decors: string[] = [];
  if (e.statut === "prevu") {
    decors.push(
      decorDeLEquipement(
        e,
        cadre,
        "prevu",
        "",
        style({ rounded: 1, absoluteArcSize: 1, arcSize: 16, fillColor: "none", strokeColor: SECONDAIRE, strokeWidth: 1.5, dashed: 1, dashPattern: "5 3", fixDash: 1, pointerEvents: 0, resizable: 0 }),
        { x: b.x0 - 5, y: b.y0 - 5, largeur: b.x1 - b.x0 + 10, hauteur: b.y1 - b.y0 + 10 },
      ),
    );
  }
  const membres = nombreDeMembres(e);
  if (membres > 1) {
    decors.push(
      decorDeLEquipement(
        e,
        cadre,
        "membres",
        `×${membres}`,
        style({
          rounded: 1,
          absoluteArcSize: 1,
          arcSize: 15,
          html: 0,
          whiteSpace: "nowrap",
          fillColor: SURFACE,
          strokeColor: teinte,
          strokeWidth: 1.5,
          fontColor: teinte,
          fontSize: 10,
          fontStyle: 1,
          fontFamily: POLICE,
          align: "center",
          verticalAlign: "middle",
          spacing: 0,
          resizable: 0,
          opacity: terni,
          textOpacity: terni,
        }),
        { x: b.x1 - 16, y: b.y0 - 10, largeur: 24, hauteur: 15 },
      ),
    );
  }
  if (e.statut === "maintenance") {
    decors.push(
      decorDeLEquipement(
        e,
        cadre,
        "maintenance",
        "",
        style({ fillColor: COULEUR_MAINTENANCE, strokeColor: SURFACE, strokeWidth: 2, resizable: 0 }, "ellipse;"),
        { x: b.x0 - 6, y: b.y0 - 6, largeur: 12, hauteur: 12 },
      ),
    );
  }
  return decors;
}

function celluleEquipement(
  schema: Schema,
  e: Equipement,
  cadre: Cadre,
  options: { icones: IconesDrawio; images: Map<string, string>; adresses: boolean; visible: boolean },
): string {
  const donnees = {
    nom: e.nom,
    type: e.type,
    ip: adresseAffichee(e),
    vlan: e.gestion?.vlan,
    modele: modeleDe(schema, e),
    site: e.site,
    salle: e.salle,
  };
  // Le nom se met juste sous l'illustration, comme dans kyfRev (le carré de 56 laisse du blanc dessous), ou là où
  // il a été placé à la main.
  const b = boiteDe(e, 0);
  const decalage = { x: e.pose?.place?.x ?? 0, y: b.y1 - (cadre.y + cadre.hauteur) + (e.pose?.place?.y ?? 0) };
  const offset = Math.abs(decalage.x) > 0.005 || Math.abs(decalage.y) > 0.005 ? pointXml(decalage, "offset") : "";
  return [
    `<object id="${echapperXml(e.id)}" label="${echapperXml(libelleEquipement(e, options.adresses))}"${attributs(donnees)}>`,
    `<mxCell style="${echapperXml(styleEquipement(e, options.icones, options.images))}" vertex="1" parent="1"${cache(options.visible)}>`,
    `${geometrie(cadre.x, cadre.y, cadre.largeur, cadre.hauteur, offset)}</mxCell></object>`,
    ...decorsDeLEquipement(e, cadre),
  ].join("");
}

function profondeurs(schema: Schema): Map<string, number> {
  const parId = new Map(schema.groupes.map((g) => [g.id, g]));
  const resultat = new Map<string, number>();
  for (const g of schema.groupes) {
    let n = 0;
    let courant = g.parent ? parId.get(g.parent) : undefined;
    const vus = new Set([g.id]);
    while (courant && !vus.has(courant.id)) {
      vus.add(courant.id);
      n++;
      courant = courant.parent ? parId.get(courant.parent) : undefined;
    }
    resultat.set(g.id, n);
  }
  return resultat;
}

/**
 * Une zone (groupe) : un conteneur en pointillé, teinté de sa couleur, sinon de la couleur principale, comme à
 * l'écran. Ses équipements ne sont pas ses enfants dans draw.io : un enfant se dessine avec son conteneur, sous
 * les câbles, et les câbles couvriraient leurs noms (kyfRev les dessine dessus).
 */
function celluleGroupe(g: Groupe, parent: Groupe | undefined): string {
  const teinte = g.couleur ?? PRINCIPALE;
  const origine = parent?.position ?? { x: 0, y: 0 };
  return `<object id="${echapperXml(g.id)}" label="${echapperXml(g.nom)}"${attributs({ genre: g.genre })}><mxCell style="${echapperXml(
    style({
      rounded: 1,
      absoluteArcSize: 1,
      arcSize: 20,
      dashed: 1,
      dashPattern: "6 4",
      fixDash: 1,
      container: 1,
      collapsible: 0,
      whiteSpace: "wrap",
      html: 0,
      fillColor: teinte,
      fillOpacity: 6,
      strokeColor: teinte,
      verticalAlign: "top",
      align: "left",
      spacing: 0,
      spacingLeft: 10,
      spacingTop: 4,
      fontStyle: 1,
      fontSize: 13,
      fontColor: TEXTE,
      fontFamily: POLICE,
    }),
  )}" vertex="1" parent="${echapperXml(parent?.id ?? "1")}">${geometrie(
    g.position.x - origine.x,
    g.position.y - origine.y,
    g.taille.largeur,
    g.taille.hauteur,
  )}</mxCell></object>`;
}

/** Les tirets d'un trait de kyfRev, en clés de style draw.io (en pixels, comme kyfRev : sans `fixDash`, draw.io les multiplie par l'épaisseur). */
const tirets = (motif: string | undefined) => (motif ? { dashed: 1, dashPattern: motif, fixDash: 1 } : {});

const styleDuTrait = (trait: Trait) => ({
  strokeColor: couleur(trait.couleur),
  strokeWidth: trait.epaisseur,
  ...tirets(trait.tirets),
});

/** Un bout de connecteur fixé au point voulu du cadre de sa forme : draw.io ne le recalcule pas. */
function prise(role: "exit" | "entry", p: Point | undefined, cadre: Cadre | undefined) {
  if (!p || !cadre || cadre.largeur === 0 || cadre.hauteur === 0) return {};
  return {
    [`${role}X`]: fraction((p.x - cadre.x) / cadre.largeur),
    [`${role}Y`]: fraction((p.y - cadre.y) / cadre.hauteur),
    [`${role}Perimeter`]: 0,
  };
}

type Cadres = Map<string, Cadre>;

/**
 * Un connecteur qui suit un tracé de kyfRev : ses bouts fixés aux siens, ses autres points en points de passage,
 * en segments droits (draw.io ne refait pas la route : les coudes sont ceux de kyfRev). Sans étiquette : elles
 * sont posées à part, au-dessus de tous les câbles, comme dans kyfRev.
 */
function connecteurSurLeTrace(o: {
  id: string;
  libelle: string;
  donnees: Record<string, string | number | undefined>;
  corps: Record<string, string | number | undefined>;
  source: string;
  cible: string;
  trace: Point[];
  cadres: Cadres;
  /** Le tracé vaut pour le modèle : sa trace va dans les données (l'import voit s'il a bougé). */
  tracer: boolean;
}): string {
  const corps = style({
    endArrow: "none",
    startArrow: "none",
    html: 0,
    rounded: 0,
    ...o.corps,
    ...prise("exit", o.trace[0], o.cadres.get(o.source)),
    ...prise("entry", o.trace.at(-1), o.cadres.get(o.cible)),
    noLabel: 1,
  });
  const milieu = o.trace.slice(1, -1);
  return [
    `<object id="${echapperXml(o.id)}" label="${echapperXml(o.libelle)}"${attributs(o.donnees)}${o.tracer ? ` trace="${texteDuTrace(milieu)}"` : ""}>`,
    `<mxCell style="${echapperXml(corps)}" edge="1" parent="1" source="${echapperXml(o.source)}" target="${echapperXml(o.cible)}">`,
    `<mxGeometry relative="1" as="geometry">${passages(milieu)}</mxGeometry></mxCell></object>`,
  ].join("");
}

const donneesDuLien = (schema: Schema, l: Lien) => ({
  support: l.support,
  technologie: l.technologie,
  portA: interfaceDe(schema, l.a)?.nom,
  portB: interfaceDe(schema, l.b)?.nom,
  etiquette: l.etiquette,
});

/** Un câble montré par kyfRev : un connecteur par trait (un agrégat en a plusieurs), et le cercle d'un agrégat. */
function cellulesDuLien(schema: Schema, entree: MiseEnPage["liens"][number], cadres: Cadres): string[] {
  const { lien: l, trace, trait, brins, cercle } = entree;
  const traits = brins ?? [trace];
  const corps = styleDuTrait(trait);
  const [source, cible] = [l.a.equipement, l.b.equipement];
  const cellules = traits.map((t, rang) =>
    rang === 0
      ? connecteurSurLeTrace({ id: l.id, libelle: l.nom ?? "", donnees: donneesDuLien(schema, l), corps, source, cible, trace: t, cadres, tracer: true })
      : connecteurSurLeTrace({ id: `${l.id}-brin-${rang + 1}`, libelle: "", donnees: { decor: "brin", pour: l.id }, corps, source, cible, trace: t, cadres, tracer: false }),
  );
  if (cercle) {
    const corpsCercle = style({ fillColor: "none", strokeColor: couleur(trait.couleur), strokeWidth: 1.6, rotation: nombre(cercle.angle), pointerEvents: 0, resizable: 0 }, "ellipse;");
    cellules.push(
      `<object id="${echapperXml(`${l.id}-cercle`)}" label="" decor="cercle" pour="${echapperXml(l.id)}"><mxCell style="${echapperXml(
        corpsCercle,
      )}" vertex="1" connectable="0" parent="1">${geometrie(cercle.x - cercle.rx, cercle.y - cercle.ry, cercle.rx * 2, cercle.ry * 2)}</mxCell></object>`,
    );
  }
  return cellules;
}

/** Un câble que kyfRev ne montre pas dans cette vue (ou d'un équipement replié) : dans le fichier, caché. */
function celluleLienCachee(schema: Schema, l: Lien): string {
  const corps = style({
    endArrow: "none",
    html: 0,
    rounded: 0,
    ...styleDuTrait(traitDuLien(l)),
    edgeStyle: estCoude(schema, l) ? "orthogonalEdgeStyle" : undefined,
  });
  return [
    `<object id="${echapperXml(l.id)}" label="${echapperXml(l.nom ?? "")}"${attributs(donneesDuLien(schema, l))}>`,
    `<mxCell style="${echapperXml(corps)}" edge="1" parent="1" source="${echapperXml(l.a.equipement)}" target="${echapperXml(l.b.equipement)}" visible="0">`,
    `<mxGeometry relative="1" as="geometry">${passages(l.points)}</mxGeometry></mxCell></object>`,
  ].join("");
}

const corpsDeRelation = (trait: Trait, fleche: boolean) => ({
  endArrow: fleche ? "block" : "none",
  endFill: fleche ? 1 : undefined,
  endSize: fleche ? 7 : undefined,
  ...styleDuTrait(trait),
});

function celluleRelationCachee(r: Relation): string {
  const corps = style({ html: 0, rounded: 0, ...corpsDeRelation(traitDeRelation(r), !RELATIONS_SYMETRIQUES.has(r.genre)) });
  return `<object id="${echapperXml(r.id)}" label="${echapperXml(r.nom ?? "")}"${attributs({ genre: r.genre })}><mxCell style="${echapperXml(
    corps,
  )}" edge="1" parent="1" source="${echapperXml(r.de)}" target="${echapperXml(r.vers)}" visible="0"><mxGeometry relative="1" as="geometry">${passages(
    r.points,
  )}</mxGeometry></mxCell></object>`;
}

/**
 * Une étiquette de câble ou de relation (port, nom, VLAN) : la pastille de kyfRev, à sa place et à sa taille,
 * au-dessus des câbles. `pour` dit à quoi elle est ; l'import ne lit que celle du nom, si on l'a changée.
 */
function cellulePastille(pour: string, e: Etiquette): string {
  const b = cadreEtiquette(e, 0);
  const teinte = e.couleur ? couleur(e.couleur) : undefined;
  const corps = style({
    rounded: 1,
    arcSize: 50,
    html: 0,
    whiteSpace: "nowrap",
    fillColor: SURFACE,
    strokeColor: teinte ?? BORDURE,
    strokeWidth: teinte ? 1.5 : 1,
    fontColor: e.genre === "vlan" && teinte ? teinte : SECONDAIRE,
    fontSize: e.taille,
    fontStyle: e.genre === "port" ? 0 : 1,
    fontFamily: POLICE,
    align: "center",
    verticalAlign: "middle",
    spacing: 0,
    rotation: e.angle ? nombre(e.angle) : undefined,
    resizable: 0,
  });
  const cle = e.cle ?? e.genre;
  return `<object id="${echapperXml(`${pour}-${cle}`)}" label="${echapperXml(e.texte)}"${attributs({ decor: cle, pour, tooltip: e.titre })}><mxCell style="${echapperXml(
    corps,
  )}" vertex="1" connectable="0" parent="1">${geometrie(b.x0, b.y0, b.x1 - b.x0, b.y1 - b.y0)}</mxCell></object>`;
}

function styleAnnotation(n: Annotation): string {
  const teinte = n.couleur ?? "#4b5563";
  switch (n.forme) {
    case "texte":
      return style(
        { html: 0, align: "left", verticalAlign: "top", whiteSpace: "nowrap", spacing: 0, fontSize: n.taille ?? 16, fontStyle: 1, fontColor: n.couleur ?? TEXTE, fontFamily: POLICE },
        "text;",
      );
    case "rectangle":
    case "ellipse":
      return style(
        {
          ...(n.forme === "rectangle" ? { rounded: 1, absoluteArcSize: 1, arcSize: 16, align: "left", spacingLeft: 10 } : { align: "center" }),
          whiteSpace: "wrap",
          html: 0,
          fillColor: teinte,
          fillOpacity: 10,
          strokeColor: teinte,
          strokeWidth: 1.5,
          verticalAlign: "top",
          spacing: 0,
          spacingTop: 4,
          fontStyle: 1,
          fontSize: 13,
          fontColor: teinte,
          fontFamily: POLICE,
        },
        n.forme === "ellipse" ? "ellipse;" : "",
      );
    default:
      return style({
        rounded: 1,
        absoluteArcSize: 1,
        arcSize: 12,
        whiteSpace: "wrap",
        html: 0,
        align: "left",
        verticalAlign: "top",
        spacing: 0,
        spacingLeft: 8,
        spacingTop: 6,
        fontSize: 12,
        fontColor: TEXTE,
        fontFamily: POLICE,
        fillColor: n.couleur ?? "#fff4e0",
        fillOpacity: n.couleur ? 14 : undefined,
        strokeColor: n.couleur ?? BORDURE,
      });
  }
}

function celluleAnnotation(n: Annotation, visible: boolean): string {
  if (n.forme === "fleche") {
    const vers = n.vers ?? { x: n.position.x + 120, y: n.position.y };
    const corps = style({
      endArrow: "classic",
      html: 0,
      rounded: 0,
      strokeWidth: 2.5,
      strokeColor: n.couleur ?? "#4b5563",
      fontSize: 11,
      fontStyle: 1,
      fontColor: SECONDAIRE,
      fontFamily: POLICE,
      labelBackgroundColor: SURFACE,
      labelBorderColor: BORDURE,
    });
    return `<mxCell id="${echapperXml(n.id)}" value="${echapperXml(n.texte)}" style="${echapperXml(corps)}" edge="1" parent="1"${cache(visible)}><mxGeometry relative="1" as="geometry">${pointXml(
      n.position,
      "sourcePoint",
    )}${pointXml(vers, "targetPoint")}</mxGeometry></mxCell>`;
  }
  const cadre = cadreAnnotation(n);
  return `<mxCell id="${echapperXml(n.id)}" value="${echapperXml(n.texte)}" style="${echapperXml(styleAnnotation(n))}" vertex="1" parent="1"${cache(visible)}>${geometrie(
    cadre.x,
    cadre.y,
    cadre.largeur,
    cadre.hauteur,
  )}</mxCell>`;
}

/** L'identifiant draw.io de la page qui porte une page de kyfRev : un renvoi y mène par un lien de page. */
const idDuDiagramme = (page: string) => `kyfrev-${page}`;

/**
 * Un câble vers une autre page (30A) : une pastille en pointillé (« → Étage 2 · SW-Étage2 · 1/48 ») qui mène à
 * l'autre page dans draw.io (lien de page), et un trait en pointillé de l'équipement à elle. L'import les
 * reconnaît à leur donnée `renvoi` : le câble lui-même vient du modèle.
 */
function cellulesRenvoi(r: Renvoi, cadres: Cadres): string {
  const id = `${r.lien.id}-renvoi-${r.equipement}`;
  const teinte = PRINCIPALE;
  const boite = { x: r.centre.x - r.largeur / 2, y: r.centre.y - r.hauteur / 2, largeur: r.largeur, hauteur: r.hauteur };
  const pastille = style({
    rounded: 1,
    arcSize: 50,
    dashed: 1,
    dashPattern: "4 3",
    fixDash: 1,
    html: 0,
    whiteSpace: "nowrap",
    fontSize: 10.5,
    fontStyle: 1,
    fontColor: teinte,
    fontFamily: POLICE,
    strokeColor: teinte,
    strokeWidth: 1.4,
    fillColor: SURFACE,
    spacing: 0,
  });
  const [debut, fin] = traitDuRenvoi(r);
  const trait = style({
    endArrow: "none",
    html: 0,
    rounded: 0,
    dashed: 1,
    dashPattern: "5 4",
    fixDash: 1,
    strokeColor: teinte,
    strokeWidth: 1.6,
    ...prise("exit", debut, cadres.get(r.equipement)),
    ...prise("entry", fin, boite),
  });
  return [
    `<object id="${echapperXml(id)}" label="${echapperXml(r.texte)}"${attributs({ renvoi: r.lien.id, page: r.page.nom, equipement: r.nomEquipement, port: r.port, link: `data:page/id,${idDuDiagramme(r.page.id)}` })}>`,
    `<mxCell style="${echapperXml(pastille)}" vertex="1" parent="1">${geometrie(boite.x, boite.y, boite.largeur, boite.hauteur)}</mxCell></object>`,
    `<mxCell id="${echapperXml(`${id}-trait`)}" style="${echapperXml(trait)}" edge="1" parent="1" source="${echapperXml(r.equipement)}" target="${echapperXml(id)}"><mxGeometry relative="1" as="geometry"/></mxCell>`,
  ].join("");
}

/**
 * Une page draw.io : les cellules d'une page de kyfRev (le schéma entier s'il n'en a qu'une), dans l'ordre où
 * kyfRev les dessine (zones, câbles, étiquettes, renvois, équipements, annotations).
 */
function diagramme(
  schema: Schema,
  page: { nom: string; id: string; kyfrevPage?: string },
  modele: string | undefined,
  options: { vue: Vue; icones: IconesDrawio; images: Map<string, string>; renvois: (caches: Set<string>) => Renvoi[] },
): string {
  const caches = masques(schema);
  const mise = mettreEnPage(schema, options.vue, caches);
  const { affichage } = mise;
  const profondeur = profondeurs(schema);
  const parId = new Map(schema.groupes.map((g) => [g.id, g]));
  const groupes = [...schema.groupes].sort((a, b) => (profondeur.get(a.id) ?? 0) - (profondeur.get(b.id) ?? 0));
  const cadres: Cadres = new Map(schema.equipements.map((e) => [e.id, cadreDeLaForme(e, options.icones)]));
  const montres = new Set([...mise.liens.map((m) => m.lien.id), ...mise.relations.map((m) => m.relation.id)]);
  const presents = (de: string, vers: string) => cadres.has(de) && cadres.has(vers);
  const calque = attributs({ kyfrev: modele, kyfrevPage: page.kyfrevPage });
  const cellules = [
    '<mxCell id="0"/>',
    `<object id="1" label="kyfRev"${calque}><mxCell parent="0"/></object>`,
    ...groupes.map((g) => celluleGroupe(g, g.parent ? parId.get(g.parent) : undefined)),
    ...schema.annotations.filter(estZone).map((n) => celluleAnnotation(n, affichage.zones)),
    ...mise.liens.flatMap((m) => cellulesDuLien(schema, m, cadres)),
    ...schema.liens.filter((l) => !montres.has(l.id) && presents(l.a.equipement, l.b.equipement)).map((l) => celluleLienCachee(schema, l)),
    ...mise.relations.map(({ relation: r, trace, trait, fleche }) =>
      connecteurSurLeTrace({
        id: r.id,
        libelle: r.nom ?? "",
        donnees: { genre: r.genre },
        corps: corpsDeRelation(trait, fleche),
        source: r.de,
        cible: r.vers,
        trace,
        cadres,
        tracer: true,
      }),
    ),
    ...schema.relations.filter((r) => !montres.has(r.id) && presents(r.de, r.vers)).map(celluleRelationCachee),
    ...[...mise.etiquettes.entries()].flatMap(([pour, etiquettes]) => etiquettes.map((e) => cellulePastille(pour, e))),
    ...options.renvois(caches).map((r) => cellulesRenvoi(r, cadres)),
    ...schema.equipements.map((e) =>
      celluleEquipement(schema, e, cadres.get(e.id) as Cadre, {
        icones: options.icones,
        images: options.images,
        adresses: affichage.adresses,
        visible: !caches.has(e.id),
      }),
    ),
    ...schema.annotations.filter((n) => !estZone(n)).map((n) => celluleAnnotation(n, affichage.annotations)),
  ];
  return [
    `<diagram name="${echapperXml(page.nom)}" id="${echapperXml(page.id)}">`,
    '<mxGraphModel dx="1200" dy="800" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="0" pageScale="1" math="0" shadow="0">',
    `<root>${cellules.join("\n")}</root>`,
    "</mxGraphModel></diagram>",
  ].join("\n");
}

/**
 * Le fichier .drawio d'un schéma : XML mxGraph non compressé, avec le modèle de kyfRev dans le calque. Une page
 * de kyfRev est une page de draw.io, du même nom (30A) ; le modèle est dans le calque de la première, et le
 * calque de chacune dit la page qu'elle porte. Un schéma d'une page sort comme avant : une page au nom du schéma.
 * Chaque page se dessine comme kyfRev la montre dans `vue` (sinon la vue du schéma).
 */
export function ecrireDrawio(schema: Schema, icones: IconesDrawio = "kyfrev", vue?: Vue): string {
  const modele = JSON.stringify(JSON.parse(ecrireSchema(schema)));
  const images = new Map<string, string>();
  const titre = schema.titre || "Schéma réseau";
  const v = vue ?? vueDe(schema.vue);
  const diagrammes = aPlusieursPages(schema)
    ? pagesDe(schema).map((p, rang) =>
        diagramme(surLaPage(schema, p.id), { nom: p.nom, id: idDuDiagramme(p.id), kyfrevPage: p.id }, rang === 0 ? modele : undefined, {
          vue: v,
          icones,
          images,
          renvois: (caches) => renvoisDeLaPage(schema, p.id, caches),
        }),
      )
    : [diagramme(schema, { nom: titre, id: idDuDiagramme(schema.id || "schema") }, modele, { vue: v, icones, images, renvois: () => [] })];
  return ['<mxfile host="kyfRev" type="device">', ...diagrammes, "</mxfile>", ""].join("\n");
}
