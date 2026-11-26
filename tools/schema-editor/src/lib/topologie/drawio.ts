// Export d'un schéma au format draw.io (29D) : un fichier .drawio non compressé (XML mxGraph), à retoucher
// dans draw.io, ou dans Visio en passant par draw.io (qui ouvre et enregistre le .vsdx lui-même).
//
// Ce qui sort :
// - un équipement est un sommet de 56 × 56 (la position de kyfRev est son centre, celle de draw.io le coin
//   en haut à gauche), avec l'illustration de kyfRev ou une forme réseau de draw.io, et ses données
//   (nom, type, adresse, VLAN, modèle, site, salle) que draw.io montre dans « Modifier les données » ;
// - un câble est un connecteur entre deux équipements, ses ports écrits à chaque bout ;
// - une relation, un connecteur en tirets fléché ; une zone (groupe), un conteneur ; une annotation, un
//   texte, un cadre ou une flèche libre ;
// - tout le modèle de kyfRev (le JSON du « Modèle complet ») est gardé dans l'attribut `kyfrev` du calque :
//   l'import le reprend et n'applique que ce qui a changé dans draw.io. Le calque est un `<object>` parce que
//   draw.io garde, en réenregistrant, les attributs inconnus des cellules qui portent des données, et qu'une
//   cellule nue (`<mxCell id="1"/>`) n'en a pas ; l'en-tête `<diagram>` ou `<mxfile>`, lui, est réécrit.

import { definition } from "./catalogue";
import {
  adresseAffichee,
  COTE,
  cadreAnnotation,
  couleurDuType,
  estCoude,
  traitDeRelation,
  traitDuLien,
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
} from "./modele";
import { aPlusieursPages, pagesDe, type Renvoi, renvoisDeLaPage, surLaPage } from "./pages";
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

const geometrie = (x: number, y: number, largeur: number, hauteur: number) =>
  `<mxGeometry x="${nombre(x)}" y="${nombre(y)}" width="${nombre(largeur)}" height="${nombre(hauteur)}" as="geometry"/>`;

const pointXml = (p: Point, role?: string) =>
  `<mxPoint x="${nombre(p.x)}" y="${nombre(p.y)}"${role ? ` as="${role}"` : ""}/>`;

const passages = (points: Point[]) =>
  points.length > 0 ? `<Array as="points">${points.map((p) => pointXml(p)).join("")}</Array>` : "";

/** L'illustration de kyfRev d'un type, en SVG autonome (data URI en base 64, comme draw.io les écrit). */
export function imageDuType(type: string): string {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${COTE}" height="${COTE}" viewBox="0 0 64 64"><defs>${DEFINITIONS_ILLUSTRATIONS}</defs>${illustration(definition(type).icone, couleurDuType(type))}</svg>`;
  const octets = new TextEncoder().encode(svg);
  let binaire = "";
  for (const octet of octets) binaire += String.fromCharCode(octet);
  // Pas de « ;base64 » : le point-virgule sépare les clés d'un style draw.io, qui lit ce data URI tel quel.
  return `data:image/svg+xml,${btoa(binaire)}`;
}

const LIBELLE_DESSOUS = { verticalLabelPosition: "bottom", verticalAlign: "top", labelPosition: "center", align: "center" };

function styleEquipement(e: Equipement, icones: IconesDrawio, images: Map<string, string>): string {
  const commun = { ...LIBELLE_DESSOUS, html: 0, whiteSpace: "wrap", opacity: e.statut === "prevu" ? 55 : undefined };
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
    return style({ rounded: 1, fillColor: couleurDuType(e.type), strokeColor: "#ffffff", fontColor: "#221d2e", ...commun });
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

/** Le groupe le plus intérieur qui contient un équipement : il en est l'enfant dans draw.io. */
function groupeDe(schema: Schema, id: string, profondeur: Map<string, number>): Groupe | undefined {
  return schema.groupes
    .filter((g) => g.membres.includes(id))
    .sort((a, b) => (profondeur.get(b.id) ?? 0) - (profondeur.get(a.id) ?? 0))[0];
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

function celluleGroupe(g: Groupe, parent: Groupe | undefined): string {
  const teinte = g.couleur ?? "#7f4ea8";
  const origine = parent?.position ?? { x: 0, y: 0 };
  return `<object id="${echapperXml(g.id)}" label="${echapperXml(g.nom)}"${attributs({ genre: g.genre })}><mxCell style="${echapperXml(
    style({
      rounded: 1,
      arcSize: 4,
      dashed: 1,
      dashPattern: "6 4",
      container: 1,
      collapsible: 0,
      whiteSpace: "wrap",
      html: 0,
      fillColor: "none",
      strokeColor: teinte,
      verticalAlign: "top",
      align: "left",
      spacingLeft: 10,
      fontStyle: 1,
    }),
  )}" vertex="1" parent="${echapperXml(parent?.id ?? "1")}">${geometrie(
    g.position.x - origine.x,
    g.position.y - origine.y,
    g.taille.largeur,
    g.taille.hauteur,
  )}</mxCell></object>`;
}

function celluleEquipement(schema: Schema, e: Equipement, parent: Groupe | undefined, icones: IconesDrawio, images: Map<string, string>): string {
  const origine = parent?.position ?? { x: 0, y: 0 };
  const donnees = {
    nom: e.nom,
    type: e.type,
    ip: adresseAffichee(e),
    vlan: e.gestion?.vlan,
    modele: modeleDe(schema, e),
    site: e.site,
    salle: e.salle,
  };
  return `<object id="${echapperXml(e.id)}" label="${echapperXml(e.nom)}"${attributs(donnees)}><mxCell style="${echapperXml(
    styleEquipement(e, icones, images),
  )}" vertex="1" parent="${echapperXml(parent?.id ?? "1")}">${geometrie(
    e.position.x - COTE / 2 - origine.x,
    e.position.y - COTE / 2 - origine.y,
    COTE,
    COTE,
  )}</mxCell></object>`;
}

/** Les tirets d'un trait de kyfRev, en clés de style draw.io. */
const tirets = (motif: string | undefined) => (motif ? { dashed: 1, dashPattern: motif } : {});

/** Le nom d'un port, en étiquette près d'un bout du connecteur (−1 : le départ, 1 : l'arrivée). */
const etiquettePort = (lien: Lien, bout: "a" | "b", nom: string) =>
  `<mxCell id="${echapperXml(`${lien.id}-${bout}`)}" value="${echapperXml(nom)}" style="${echapperXml(
    style({ html: 0, align: "center", verticalAlign: "middle", resizable: 0, points: "[]", fontSize: 10, labelBackgroundColor: "#ffffff" }, "edgeLabel;"),
  )}" vertex="1" connectable="0" parent="${echapperXml(lien.id)}"><mxGeometry x="${bout === "a" ? -0.75 : 0.75}" relative="1" as="geometry"><mxPoint as="offset"/></mxGeometry></mxCell>`;

function celluleLien(schema: Schema, l: Lien): string {
  const trait = traitDuLien(l);
  const portA = interfaceDe(schema, l.a)?.nom;
  const portB = interfaceDe(schema, l.b)?.nom;
  const donnees = {
    support: l.support,
    technologie: l.technologie,
    portA,
    portB,
    etiquette: l.etiquette,
  };
  const corps = style({
    endArrow: "none",
    html: 0,
    rounded: 0,
    strokeColor: couleur(trait.couleur),
    strokeWidth: trait.epaisseur,
    ...tirets(trait.tirets),
    edgeStyle: estCoude(schema, l) ? "orthogonalEdgeStyle" : undefined,
  });
  return [
    `<object id="${echapperXml(l.id)}" label="${echapperXml(l.nom ?? "")}"${attributs(donnees)}>`,
    `<mxCell style="${echapperXml(corps)}" edge="1" parent="1" source="${echapperXml(l.a.equipement)}" target="${echapperXml(l.b.equipement)}">`,
    `<mxGeometry relative="1" as="geometry">${passages(l.points)}</mxGeometry></mxCell></object>`,
    portA ? etiquettePort(l, "a", portA) : "",
    portB ? etiquettePort(l, "b", portB) : "",
  ].join("");
}

function celluleRelation(r: Relation): string {
  const trait = traitDeRelation(r);
  const corps = style({
    endArrow: RELATIONS_SYMETRIQUES.has(r.genre) ? "none" : "block",
    endFill: 1,
    html: 0,
    rounded: 0,
    dashed: 1,
    dashPattern: trait.tirets,
    strokeColor: couleur(trait.couleur),
    strokeWidth: trait.epaisseur,
  });
  return `<object id="${echapperXml(r.id)}" label="${echapperXml(r.nom ?? "")}"${attributs({ genre: r.genre })}><mxCell style="${echapperXml(
    corps,
  )}" edge="1" parent="1" source="${echapperXml(r.de)}" target="${echapperXml(r.vers)}"><mxGeometry relative="1" as="geometry">${passages(
    r.points,
  )}</mxGeometry></mxCell></object>`;
}

function styleAnnotation(n: Annotation): string {
  const teinte = n.couleur ?? "#4b5563";
  switch (n.forme) {
    case "texte":
      return style(
        { html: 0, align: "left", verticalAlign: "top", whiteSpace: "wrap", fontSize: n.taille ?? 16, fontStyle: 1, fontColor: n.couleur ?? "#221d2e" },
        "text;",
      );
    case "rectangle":
      return style({ rounded: 1, arcSize: 4, whiteSpace: "wrap", html: 0, fillColor: "none", strokeColor: teinte, strokeWidth: 1.5, verticalAlign: "top", align: "left", spacingLeft: 10, fontStyle: 1, fontColor: teinte });
    case "ellipse":
      return style({ whiteSpace: "wrap", html: 0, fillColor: "none", strokeColor: teinte, strokeWidth: 1.5, verticalAlign: "top", fontStyle: 1, fontColor: teinte }, "ellipse;");
    default:
      return style({
        rounded: 1,
        arcSize: 6,
        whiteSpace: "wrap",
        html: 0,
        align: "left",
        verticalAlign: "top",
        spacingLeft: 8,
        spacingTop: 4,
        fontSize: 12,
        fillColor: n.couleur ?? "#fff4e0",
        strokeColor: n.couleur ?? "#dedae8",
      });
  }
}

function celluleAnnotation(n: Annotation): string {
  if (n.forme === "fleche") {
    const vers = n.vers ?? { x: n.position.x + 120, y: n.position.y };
    const corps = style({ endArrow: "classic", html: 0, rounded: 0, strokeWidth: 2.5, strokeColor: n.couleur ?? "#4b5563" });
    return `<mxCell id="${echapperXml(n.id)}" value="${echapperXml(n.texte)}" style="${echapperXml(corps)}" edge="1" parent="1"><mxGeometry relative="1" as="geometry">${pointXml(
      n.position,
      "sourcePoint",
    )}${pointXml(vers, "targetPoint")}</mxGeometry></mxCell>`;
  }
  const cadre = cadreAnnotation(n);
  return `<mxCell id="${echapperXml(n.id)}" value="${echapperXml(n.texte)}" style="${echapperXml(styleAnnotation(n))}" vertex="1" parent="1">${geometrie(
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
function cellulesRenvoi(r: Renvoi): string {
  const id = `${r.lien.id}-renvoi-${r.equipement}`;
  const teinte = "#2e6a4e";
  const pastille = style({
    rounded: 1,
    arcSize: 50,
    dashed: 1,
    dashPattern: "4 3",
    html: 0,
    whiteSpace: "nowrap",
    fontSize: 10,
    fontStyle: 1,
    fontColor: teinte,
    strokeColor: teinte,
    fillColor: "#ffffff",
  });
  const trait = style({ endArrow: "none", html: 0, rounded: 0, dashed: 1, dashPattern: "5 4", strokeColor: teinte, strokeWidth: 1.5 });
  return [
    `<object id="${echapperXml(id)}" label="${echapperXml(r.texte)}"${attributs({ renvoi: r.lien.id, page: r.page.nom, equipement: r.nomEquipement, port: r.port, link: `data:page/id,${idDuDiagramme(r.page.id)}` })}>`,
    `<mxCell style="${echapperXml(pastille)}" vertex="1" parent="1">${geometrie(r.centre.x - r.largeur / 2, r.centre.y - r.hauteur / 2, r.largeur, r.hauteur)}</mxCell></object>`,
    `<mxCell id="${echapperXml(`${id}-trait`)}" style="${echapperXml(trait)}" edge="1" parent="1" source="${echapperXml(r.equipement)}" target="${echapperXml(id)}"><mxGeometry relative="1" as="geometry"/></mxCell>`,
  ].join("");
}

/** Une page draw.io : les cellules d'une page de kyfRev (le schéma entier s'il n'en a qu'une). */
function diagramme(
  schema: Schema,
  page: { nom: string; id: string; kyfrevPage?: string },
  modele: string | undefined,
  renvois: Renvoi[],
  icones: IconesDrawio,
  images: Map<string, string>,
): string {
  const profondeur = profondeurs(schema);
  const parId = new Map(schema.groupes.map((g) => [g.id, g]));
  const groupes = [...schema.groupes].sort((a, b) => (profondeur.get(a.id) ?? 0) - (profondeur.get(b.id) ?? 0));
  const equipements = new Set(schema.equipements.map((e) => e.id));
  const zones = schema.annotations.filter((n) => n.forme === "rectangle" || n.forme === "ellipse");
  const autres = schema.annotations.filter((n) => n.forme !== "rectangle" && n.forme !== "ellipse");
  const calque = attributs({ kyfrev: modele, kyfrevPage: page.kyfrevPage });
  const cellules = [
    '<mxCell id="0"/>',
    `<object id="1" label="kyfRev"${calque}><mxCell parent="0"/></object>`,
    ...groupes.map((g) => celluleGroupe(g, g.parent ? parId.get(g.parent) : undefined)),
    ...zones.map(celluleAnnotation),
    ...schema.liens.filter((l) => equipements.has(l.a.equipement) && equipements.has(l.b.equipement)).map((l) => celluleLien(schema, l)),
    ...schema.relations.filter((r) => equipements.has(r.de) && equipements.has(r.vers)).map(celluleRelation),
    ...renvois.map(cellulesRenvoi),
    ...schema.equipements.map((e) => celluleEquipement(schema, e, groupeDe(schema, e.id, profondeur), icones, images)),
    ...autres.map(celluleAnnotation),
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
 */
export function ecrireDrawio(schema: Schema, icones: IconesDrawio = "kyfrev"): string {
  const modele = JSON.stringify(JSON.parse(ecrireSchema(schema)));
  const images = new Map<string, string>();
  const titre = schema.titre || "Schéma réseau";
  const diagrammes = aPlusieursPages(schema)
    ? pagesDe(schema).map((p, rang) =>
        diagramme(
          surLaPage(schema, p.id),
          { nom: p.nom, id: idDuDiagramme(p.id), kyfrevPage: p.id },
          rang === 0 ? modele : undefined,
          renvoisDeLaPage(schema, p.id),
          icones,
          images,
        ),
      )
    : [diagramme(schema, { nom: titre, id: idDuDiagramme(schema.id || "schema") }, modele, [], icones, images)];
  return ['<mxfile host="kyfRev" type="device">', ...diagrammes, "</mxfile>", ""].join("\n");
}
