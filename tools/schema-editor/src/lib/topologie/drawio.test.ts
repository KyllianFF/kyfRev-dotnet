import { describe, expect, it } from "vitest";
import { typeConnu } from "./catalogue";
import { boiteDe, COTE, cadreEtiquette, masques, mettreEnPage } from "./dessin";
import {
  cadreDeLaForme,
  ecrireDrawio,
  FORMES_DRAWIO,
  type IconesDrawio,
  positionDeLaForme,
  texteDuTrace,
  typeDeLaForme,
  typeDevine,
} from "./drawio";
import { commeUnVrai } from "./drawio.exemple";
import { ecrireSchema, erreursStructurelles, lireSchema } from "./format";
import { GABARITS } from "./gabarits";
import { bilanImport, type CompteRendu, estFichierDrawio, lireImport, nomDuFichier, remarquesImport, texteDuLibelle } from "./importation";
import { type Point, type Schema, schemaVide } from "./modele";
import { collerEquipements, connecter, creerEquipement } from "./operations";
import { surLaPage } from "./pages";
import { estImage, FORMATS_EXPORT } from "./sorties";
import { vue } from "./vues";
import { type ElementXml, echapperXml, lireXml } from "./xml";

/** Un schéma de démonstration : le premier modèle de topologie, une zone, des annotations, une relation. */
function demonstration(): Schema {
  const schema = schemaVide('Réseau « siège » & <labo>\n"bis"');
  schema.id = "s_demo";
  const gabarit = GABARITS[0];
  if (!gabarit) throw new Error("aucun modèle de topologie");
  collerEquipements(schema, gabarit.construire(), { x: 0, y: 0 }, { suffixe: "", adresses: true });
  const [premier, second] = schema.equipements;
  if (!premier || !second) throw new Error("modèle trop petit");
  premier.site = "Lyon";
  premier.salle = "";
  premier.gestion = { adresse: "10.0.0.1", vlan: 10 };
  schema.groupes.push({
    id: "g_zone",
    nom: "Salle serveurs",
    genre: "salle",
    membres: [premier.id],
    position: { x: premier.position.x - 100, y: premier.position.y - 100 },
    taille: { largeur: 300, hauteur: 260 },
  });
  schema.relations.push({ id: "r_dep", genre: "dependance", de: premier.id, vers: second.id, statut: "actif", proprietes: {}, points: [] });
  schema.annotations.push(
    { id: "n_note", texte: "À vérifier\ndeux lignes", position: { x: 500, y: 40 } },
    { id: "n_texte", texte: "Titre", forme: "texte", position: { x: 20, y: 20 }, taille: 20 },
    { id: "n_zone", texte: "DMZ", forme: "rectangle", position: { x: 600, y: 300 }, largeur: 200, hauteur: 120 },
    { id: "n_fleche", texte: "flux", forme: "fleche", position: { x: 10, y: 400 }, vers: { x: 200, y: 420 } },
  );
  const lien = schema.liens[0];
  if (lien) {
    lien.forme = "coudee";
    lien.trait = "points";
    lien.couleur = "#c0392b";
    lien.points = [{ x: 120, y: 80 }];
  }
  return schema;
}

const cellules = (xml: string): ElementXml[] => {
  const racine = lireXml(xml);
  const root = racine.enfants[0]?.enfants[0]?.enfants[0];
  if (racine.nom !== "mxfile" || root?.nom !== "root") throw new Error("structure inattendue");
  return root.enfants;
};
const cellule = (xml: string, id: string) => cellules(xml).find((c) => c.attributs.id === id);
const mxCell = (c: ElementXml | undefined) => (c?.nom === "mxCell" ? c : c?.enfants.find((e) => e.nom === "mxCell"));
const geometrie = (c: ElementXml | undefined) => mxCell(c)?.enfants.find((e) => e.nom === "mxGeometry");
/** Les cellules de toutes les pages du fichier. */
const toutes = (xml: string): ElementXml[] => lireXml(xml).enfants.flatMap((d) => d.enfants[0]?.enfants[0]?.enfants ?? []);
const dans = (xml: string, id: string) => toutes(xml).find((c) => c.attributs.id === id);
const pointsDe = (xml: string, id: string): Point[] =>
  geometrie(dans(xml, id))
    ?.enfants.find((e) => e.nom === "Array")
    ?.enfants.map((p) => ({ x: Number(p.attributs.x), y: Number(p.attributs.y) })) ?? [];
/** Un nombre tel que l'export l'écrit (au centième). */
const ecrit = (n: number) => String(Math.round(n * 100) / 100);
const styleDe = (c: ElementXml | undefined): Record<string, string> =>
  Object.fromEntries(
    (mxCell(c)?.attributs.style ?? "")
      .split(";")
      .filter(Boolean)
      .map((p) => (p.includes("=") ? [p.slice(0, p.indexOf("=")), p.slice(p.indexOf("=") + 1)] : [p, ""])),
  );
type Rect = { x0: number; y0: number; x1: number; y1: number };
const rectDe = (c: ElementXml | undefined): Rect => {
  const g = geometrie(c)?.attributs ?? {};
  const [x, y] = [Number(g.x), Number(g.y)];
  return { x0: x, y0: y, x1: x + Number(g.width), y1: y + Number(g.height) };
};
const proche = (a: number, b: number) => Math.abs(a - b) < 0.02;
const seChevauchent = (a: Rect, b: Rect) => a.x0 < b.x1 - 0.5 && b.x0 < a.x1 - 0.5 && a.y0 < b.y1 - 0.5 && b.y0 < a.y1 - 0.5;
/** Le point où draw.io fixe le bout d'un connecteur (`exit` : départ, `entry` : arrivée) d'après sa contrainte. */
function boutFixe(xml: string, id: string, role: "exit" | "entry"): Point {
  const c = dans(xml, id);
  const s = styleDe(c);
  const forme = dans(xml, mxCell(c)?.attributs[role === "exit" ? "source" : "target"] ?? "");
  const r = rectDe(forme);
  expect(s[`${role}Perimeter`]).toBe("0");
  return { x: r.x0 + Number(s[`${role}X`]) * (r.x1 - r.x0), y: r.y0 + Number(s[`${role}Y`]) * (r.y1 - r.y0) };
}
/** La mise en page de kyfRev de la page Siège du schéma d'essai. */
const miseDuSiege = (schema: Schema) => {
  const page = surLaPage(schema, "p_siege");
  return mettreEnPage(page, vue(schema.vue), masques(page));
};
const equipementNomme = (schema: Schema, nom: string) => {
  const e = schema.equipements.find((x) => x.nom === nom);
  if (!e) throw new Error(`${nom} absent`);
  return e;
};

describe("draw.io : la table des formes", () => {
  it("ne cite que des types du catalogue et des formes réseau de draw.io", () => {
    for (const [type, forme] of Object.entries(FORMES_DRAWIO)) {
      expect(typeConnu(type), type).toBe(true);
      expect(forme).toMatch(/^mxgraph\.(cisco|networks)\.[a-z0-9_]+(\.[a-z0-9_]+)?$/);
    }
  });

  it("se lit à l'envers : chaque forme redonne un type, le premier écrit ou le préféré", () => {
    for (const forme of new Set(Object.values(FORMES_DRAWIO))) expect(typeConnu(typeDeLaForme(forme) ?? "")).toBe(true);
    expect(typeDeLaForme("mxgraph.cisco.routers.router")).toBe("routeur");
    expect(typeDeLaForme("mxgraph.cisco.switches.layer_3_switch")).toBe("switch-coeur");
    expect(typeDeLaForme("mxgraph.networks.server")).toBe("serveur");
    expect(typeDeLaForme("mxgraph.inconnue")).toBeUndefined();
  });

  it("devine le type d'une forme hors de la table à ses mots", () => {
    expect(typeDevine("mxgraph.cisco.routers.atm_router")).toBe("routeur");
    expect(typeDevine("mxgraph.networks.desktop_pc")).toBe("pc");
    expect(typeDevine("mxgraph.cisco.security.ios_firewall")).toBe("pare-feu");
    expect(typeDevine("cloud")).toBe("cloud");
    expect(typeDevine("img/lib/clip_art/networking/Wireless_Router_128x128.png")).toBe("routeur");
    expect(typeDevine("mxgraph.aws4.lambda_function")).toBeUndefined();
  });
});

describe("draw.io : l'export", () => {
  it("est un format à part, entre l'image SVG et le modèle complet, qui n'est pas une image", () => {
    expect(FORMATS_EXPORT.map((f) => f.valeur)).toEqual(["png", "svg", "drawio", "json"]);
    expect(estImage("drawio")).toBe(false);
    expect(estImage("svg")).toBe(true);
  });

  it("écrit un fichier bien formé : mxfile, une page au titre du schéma, la racine et un calque", () => {
    const schema = demonstration();
    const xml = ecrireDrawio(schema);
    const racine = lireXml(xml);
    expect(racine.nom).toBe("mxfile");
    expect(racine.enfants[0]?.attributs.name).toBe(schema.titre);
    expect(racine.enfants[0]?.enfants[0]?.nom).toBe("mxGraphModel");
    const toutes = cellules(xml);
    expect(toutes[0]?.attributs).toEqual({ id: "0" });
    expect(toutes[1]?.nom).toBe("object");
    expect(mxCell(toutes[1])?.attributs.parent).toBe("0");
  });

  it("garde tout le modèle de kyfRev dans le calque", () => {
    const schema = demonstration();
    const calque = cellule(ecrireDrawio(schema), "1");
    expect(ecrireSchema(lireSchema(calque?.attributs.kyfrev ?? "").schema)).toBe(ecrireSchema(schema));
  });

  it("pose chaque équipement par son coin, ses données seulement non vides", () => {
    const schema = demonstration();
    const xml = ecrireDrawio(schema);
    const premier = schema.equipements[0];
    if (!premier) throw new Error("vide");
    const c = cellule(xml, premier.id);
    expect(c?.attributs).toMatchObject({ nom: premier.nom, type: premier.type, ip: "10.0.0.1", vlan: "10", site: "Lyon" });
    expect(texteDuLibelle(c?.attributs.label ?? "", true)).toBe(`${premier.nom}\n10.0.0.1`);
    expect(c?.attributs).not.toHaveProperty("salle");
    // Pas enfant de sa zone : un enfant se dessine avec son conteneur, sous les câbles qui couvriraient son nom.
    expect(mxCell(c)?.attributs.parent).toBe("1");
    expect(geometrie(c)?.attributs).toMatchObject({
      x: String(premier.position.x - COTE / 2),
      y: String(premier.position.y - COTE / 2),
      width: String(COTE),
      height: String(COTE),
    });
    expect(mxCell(c)?.attributs.style).toMatch(/^shape=image;image=data:image\/svg\+xml,[A-Za-z0-9+/=]+;/);
    const svg = atob((mxCell(c)?.attributs.style ?? "").split("image=data:image/svg+xml,")[1]?.split(";")[0] ?? "");
    expect(lireXml(svg).nom).toBe("svg");
    const second = schema.equipements[1];
    if (!second) throw new Error("vide");
    expect(geometrie(cellule(xml, second.id))?.attributs.x).toBe(String(second.position.x - COTE / 2));
  });

  it("avec les formes de draw.io, un type de la table prend sa forme, les autres un rectangle arrondi", () => {
    const schema = schemaVide();
    const routeur = creerEquipement(schema, "routeur", { x: 100, y: 100 });
    const iot = creerEquipement(schema, "iot", { x: 300, y: 100 });
    const xml = ecrireDrawio(schema, "drawio");
    expect(mxCell(cellule(xml, routeur.id))?.attributs.style).toContain("shape=mxgraph.cisco.routers.router;");
    expect(mxCell(cellule(xml, iot.id))?.attributs.style).toMatch(/^rounded=1;/);
  });

  it("écrit un câble en connecteur sur le tracé de kyfRev : bouts fixés, coudes en points, pointillé, couleur, ports", () => {
    const schema = demonstration();
    const lien = schema.liens[0];
    if (!lien) throw new Error("pas de câble");
    const xml = ecrireDrawio(schema);
    const c = mxCell(cellule(xml, lien.id));
    expect(c?.attributs).toMatchObject({ edge: "1", source: lien.a.equipement, target: lien.b.equipement });
    // Les coudes sont ceux de kyfRev, en points de passage : draw.io ne refait pas la route.
    expect(c?.attributs.style).not.toContain("edgeStyle");
    expect(c?.attributs.style).toContain("dashed=1;dashPattern=1 5;fixDash=1;");
    expect(c?.attributs.style).toContain("strokeColor=#c0392b;");
    expect(c?.attributs.style).toMatch(/exitX=[-\d.]+;exitY=[-\d.]+;exitPerimeter=0;entryX=[-\d.]+;entryY=[-\d.]+;entryPerimeter=0;/);
    const trace = mettreEnPage(schema, vue("physique"), new Set()).liens.find((m) => m.lien.id === lien.id)?.trace ?? [];
    expect(trace.length).toBeGreaterThan(2);
    expect(pointsDe(xml, lien.id)).toEqual(trace.slice(1, -1).map((p) => ({ x: Math.round(p.x * 100) / 100, y: Math.round(p.y * 100) / 100 })));
    expect(cellule(xml, lien.id)?.attributs.trace).toBe(texteDuTrace(trace.slice(1, -1)));
    expect(cellule(xml, `${lien.id}-port-a`)?.attributs).toMatchObject({ decor: "port-a", pour: lien.id });
    expect(cellule(xml, `${lien.id}-port-b`)?.attributs.label).toBeTruthy();
  });

  it("écrit relations, zones et annotations ; une flèche n'a que ses deux points", () => {
    const xml = ecrireDrawio(demonstration());
    expect(mxCell(cellule(xml, "r_dep"))?.attributs.style).toContain("endArrow=block;");
    expect(cellule(xml, "r_dep")?.attributs.genre).toBe("dependance");
    expect(mxCell(cellule(xml, "g_zone"))?.attributs.style).toContain("container=1;");
    expect(cellule(xml, "n_texte")?.attributs.style).toMatch(/^text;/);
    expect(cellule(xml, "n_note")?.attributs.value).toBe("À vérifier\ndeux lignes");
    const fleche = cellule(xml, "n_fleche");
    expect(fleche?.attributs.edge).toBe("1");
    expect(fleche?.attributs.source).toBeUndefined();
    expect(geometrie(fleche)?.enfants.map((p) => p.attributs.as)).toEqual(["sourcePoint", "targetPoint"]);
  });

  it("échappe tout ce qu'il écrit, et retire ce qu'XML refuse", () => {
    expect(echapperXml('a<b>&"c"\n\td\u0001')).toBe("a&lt;b&gt;&amp;&quot;c&quot;&#10;&#9;d");
  });
});

describe("le lecteur XML", () => {
  it("lit attributs, texte, entités et CDATA", () => {
    const e = lireXml('<?xml version="1.0"?><!-- note --><a x="1 &amp; 2" y=\'&#10;\'>t&lt;<b/><![CDATA[<brut>]]></a>');
    expect(e.attributs).toEqual({ x: "1 & 2", y: "\n" });
    expect(e.texte).toBe("t<<brut>");
    expect(e.enfants[0]?.nom).toBe("b");
  });

  it("refuse ce qui est mal formé", () => {
    for (const mauvais of ["<a>", "<a></b>", "<a/><b/>", '<!DOCTYPE a [<!ENTITY x "y">]><a/>', "<a>&</a>", "<a>&bof;</a>", '<a x="1" x="2"/>', "texte", "<a"]) {
      expect(() => lireXml(mauvais), mauvais).toThrow(/XML mal formé|entité|« & »|caractère/);
    }
  });
});

/** Un fichier draw.io écrit à la main, comme d'autres outils le font. */
const ETRANGER = `<mxfile host="app.diagrams.net">
  <diagram name="Réseau du labo" id="p1"><mxGraphModel><root>
    <mxCell id="0"/><mxCell id="1" parent="0"/>
    <mxCell id="z" value="Bâtiment A" style="swimlane;container=1;strokeColor=#167a5a;" vertex="1" parent="1"><mxGeometry x="0" y="0" width="400" height="300" as="geometry"/></mxCell>
    <mxCell id="r" value="Routeur&lt;br&gt;10.0.0.1" style="shape=mxgraph.cisco.routers.router;html=1;" vertex="1" parent="z"><mxGeometry x="50" y="60" width="60" height="40" as="geometry"/></mxCell>
    <mxCell id="s" value="Switch" style="shape=mxgraph.cisco.switches.workgroup_switch;" vertex="1" parent="1"><mxGeometry x="500" y="100" width="64" height="32" as="geometry"/></mxCell>
    <mxCell id="f" value="Fonction" style="shape=mxgraph.aws4.lambda_function;" vertex="1" parent="1"><mxGeometry x="700" y="100" width="40" height="40" as="geometry"/></mxCell>
    <mxCell id="t" value="Note libre" style="text;html=1;fontSize=18;" vertex="1" parent="1"><mxGeometry x="10" y="400" width="100" height="30" as="geometry"/></mxCell>
    <mxCell id="e" value="Wi-Fi" style="ellipse;whiteSpace=wrap;" vertex="1" parent="1"><mxGeometry x="200" y="400" width="120" height="80" as="geometry"/></mxCell>
    <mxCell id="c1" value="uplink" style="curved=1;strokeColor=#1f62b0;" edge="1" parent="1" source="r" target="s"><mxGeometry relative="1" as="geometry"><Array as="points"><mxPoint x="300" y="200"/></Array></mxGeometry></mxCell>
    <mxCell id="c1a" value="Gi0/1" style="edgeLabel;" vertex="1" connectable="0" parent="c1"><mxGeometry x="-0.8" relative="1" as="geometry"/></mxCell>
    <mxCell id="c2" style="edgeStyle=orthogonalEdgeStyle;dashed=1;" edge="1" parent="1" source="s" target="f"><mxGeometry relative="1" as="geometry"/></mxCell>
    <mxCell id="c3" style="" edge="1" parent="1" source="s"><mxGeometry relative="1" as="geometry"/></mxCell>
    <mxCell id="c4" value="vers la note" style="" edge="1" parent="1" source="t" target="e"><mxGeometry relative="1" as="geometry"/></mxCell>
  </root></mxGraphModel></diagram>
  <diagram name="Étage 2" id="p2"><mxGraphModel><root><mxCell id="0"/></root></mxGraphModel></diagram>
</mxfile>`;

const compteRendu = (importe: { compteRendu: CompteRendu | null }) => {
  if (!importe.compteRendu) throw new Error("pas de compte rendu");
  return importe.compteRendu;
};

describe("draw.io : l'import d'un fichier d'un autre outil", () => {
  it("traduit formes, zones, textes et connecteurs, et dit ce qu'il faut regarder", async () => {
    const importe = await lireImport(ETRANGER, "C:\\dossier\\labo.drawio");
    const { schema } = importe;
    expect(importe.erreurs).toEqual([]);
    expect(erreursStructurelles(schema)).toEqual([]);
    // Deux pages : le schéma prend le nom du fichier, chaque page du fichier devient une page du schéma (30A).
    expect(schema.titre).toBe("labo");
    expect(schema.format).toBe(3);
    expect(schema.pages?.map((p) => p.nom)).toEqual(["Réseau du labo", "Étage 2"]);
    expect(new Set(schema.equipements.map((e) => e.page))).toEqual(new Set([schema.pages?.[0]?.id]));
    const parNom = (nom: string) => schema.equipements.find((e) => e.nom === nom);
    expect(parNom("Routeur")?.type).toBe("routeur");
    // Le centre, depuis le coin de la zone qui le contient.
    expect(parNom("Routeur")?.position).toEqual({ x: 80, y: 80 });
    expect(parNom("Switch")?.type).toBe("switch");
    expect(parNom("Fonction")?.type).toBe("generique");
    expect(schema.groupes).toHaveLength(1);
    expect(schema.groupes[0]).toMatchObject({ nom: "Bâtiment A", genre: "zone", couleur: "#167a5a", membres: [parNom("Routeur")?.id] });
    expect(schema.annotations.map((n) => n.forme).sort()).toEqual(["ellipse", "fleche", "texte"]);
    expect(schema.annotations.find((n) => n.forme === "texte")).toMatchObject({ texte: "Note libre", taille: 18 });

    const uplink = schema.liens.find((l) => l.nom === "uplink");
    expect(uplink).toMatchObject({ forme: "coudee", couleur: "#1f62b0", points: [{ x: 300, y: 200 }] });
    const routeur = parNom("Routeur");
    expect(routeur?.interfaces.find((i) => i.id === uplink?.a.interface)?.nom).toBe("Gi0/1");
    expect(schema.liens.find((l) => l.nom !== "uplink")).toMatchObject({ forme: "coudee", trait: "tirets" });

    const c = compteRendu(importe);
    expect(c).toMatchObject({ equipements: 3, cables: 2, zones: 2, textes: 2, courbes: 1, sansExtremite: 1, pages: 2 });
    expect(c.inconnus).toEqual([parNom("Fonction")?.id]);
    expect(remarquesImport(c).map((r) => r.texte)).toEqual([
      "1 forme de type inconnu, posée en « Générique »",
      "1 connecteur courbe, redressé en coudé",
      "1 connecteur sans extrémité, laissé de côté",
    ]);
    expect(remarquesImport(c)[0]?.montrer).toBe(true);
    expect(bilanImport(c)).toEqual(["2 pages", "3 équipements", "2 câbles, avec leurs ports", "2 zones et 2 textes"]);
  });

  it("prend le nom du fichier quand la page porte le nom par défaut", async () => {
    const xml = '<mxfile><diagram name="Page-1"><mxGraphModel><root><mxCell id="0"/><mxCell id="1" parent="0"/></root></mxGraphModel></diagram></mxfile>';
    const { schema, compteRendu } = await lireImport(xml, "C:/a/Mon réseau.drawio");
    expect(schema.titre).toBe("Mon réseau");
    expect(compteRendu && remarquesImport(compteRendu)).toEqual([]);
    expect(compteRendu && bilanImport(compteRendu)).toEqual(["0 équipement", "0 câble", "0 zone et 0 texte"]);
  });

  it("lit une page compressée (deflate et base 64, comme draw.io l'écrit par défaut)", async () => {
    const dessin = ETRANGER.slice(ETRANGER.indexOf("<mxGraphModel>"), ETRANGER.indexOf("</mxGraphModel>") + 15);
    const flux = new Blob([encodeURIComponent(dessin)]).stream().pipeThrough(new CompressionStream("deflate-raw"));
    const octets = new Uint8Array(await new Response(flux).arrayBuffer());
    const compresse = `<mxfile><diagram name="Réseau du labo" id="p1">${btoa(String.fromCharCode(...octets))}</diagram></mxfile>`;
    const lu = await lireImport(compresse);
    const clair = await lireImport(ETRANGER);
    expect(lu.schema.equipements.map((e) => [e.nom, e.type, e.position])).toEqual(clair.schema.equipements.map((e) => [e.nom, e.type, e.position]));
    expect(lu.compteRendu?.pages).toBe(1);
    expect(lu.schema.titre).toBe("Réseau du labo");
    expect(lu.schema.pages).toBeUndefined();
  });

  it("refuse ce qui n'est pas un fichier draw.io, et une page compressée illisible", async () => {
    await expect(lireImport("<svg/>")).rejects.toThrow("ce n'est pas un fichier draw.io");
    await expect(lireImport("<mxfile></mxfile>")).rejects.toThrow("aucune page");
    await expect(lireImport("<mxfile><diagram>AAAA</diagram></mxfile>")).rejects.toThrow("illisible");
    await expect(lireImport("<mxfile><diagram>%%%</diagram></mxfile>")).rejects.toThrow("illisible");
  });

  it("garde le chemin JSON pour un modèle de kyfRev, sans compte rendu", async () => {
    const schema = demonstration();
    const importe = await lireImport(ecrireSchema(schema));
    expect(importe.compteRendu).toBeNull();
    expect(estFichierDrawio(ecrireSchema(schema))).toBe(false);
    expect(estFichierDrawio("\ufeff  <mxfile/>")).toBe(true);
  });

  it("lit un libellé HTML en texte, et un nom de fichier sans dossier ni extension", () => {
    expect(texteDuLibelle("<div><b>SW&nbsp;1</b></div><div>10.0.0.2 &amp; &#233;</div>", true)).toBe("SW 1\n10.0.0.2 & é");
    expect(texteDuLibelle("a<b>", false)).toBe("a<b>");
    expect(nomDuFichier("C:\\x\\y\\réseau.v2.drawio")).toBe("réseau.v2");
  });
});

describe("draw.io : l'aller et retour d'un schéma de kyfRev", () => {
  it("redonne le même schéma quand rien n'a changé dans draw.io", async () => {
    const schema = demonstration();
    const importe = await lireImport(ecrireDrawio(schema));
    expect(importe.erreurs).toEqual([]);
    expect(ecrireSchema({ ...importe.schema, id: schema.id })).toBe(ecrireSchema(schema));
    expect(compteRendu(importe)).toMatchObject({ inconnus: [], courbes: 0, sansExtremite: 0 });
  });

  it("applique ce qui a changé : une place, un nom, une forme retirée, une forme et un connecteur ajoutés", async () => {
    const schema = demonstration();
    const [premier, second] = schema.equipements;
    if (!premier || !second) throw new Error("vide");
    let xml = ecrireDrawio(schema, "drawio");
    // Le second équipement : déplacé de 100 vers la droite et renommé. Avec les formes de draw.io, son cadre est
    // l'emprise de son illustration.
    const cadre = cadreDeLaForme(second, "drawio");
    const ancien = `<mxGeometry x="${ecrit(cadre.x)}" y="${ecrit(cadre.y)}" width="${ecrit(cadre.largeur)}"`;
    expect(xml).toContain(ancien);
    xml = xml.replace(ancien, `<mxGeometry x="${ecrit(cadre.x + 100)}" y="${ecrit(cadre.y)}" width="${ecrit(cadre.largeur)}"`);
    xml = xml.replace(new RegExp(`<object id="${second.id}" label="[^"]*"`), `<object id="${second.id}" label="Cœur B"`);
    // La note retirée ; un routeur ajouté, relié au premier équipement.
    xml = xml.replace(/<mxCell id="n_note"[^]*?<\/mxCell>/, "");
    xml = xml.replace(
      "</root>",
      `<mxCell id="neuf" value="R9" style="shape=mxgraph.cisco.routers.router;" vertex="1" parent="1"><mxGeometry x="900" y="900" width="56" height="56" as="geometry"/></mxCell><mxCell id="neuf-c" edge="1" parent="1" source="neuf" target="${premier.id}"><mxGeometry relative="1" as="geometry"/></mxCell></root>`,
    );
    const importe = await lireImport(xml);
    expect(importe.erreurs).toEqual([]);
    const lu = importe.schema;
    const deplace = lu.equipements.find((e) => e.id === second.id);
    expect(deplace).toMatchObject({ nom: "Cœur B", position: { x: second.position.x + 100, y: second.position.y } });
    expect(lu.annotations.some((n) => n.id === "n_note")).toBe(false);
    const neuf = lu.equipements.find((e) => e.nom === "R9");
    expect(neuf).toMatchObject({ type: "routeur", position: { x: 928, y: 928 } });
    expect(lu.liens.filter((l) => l.a.equipement === neuf?.id || l.b.equipement === neuf?.id)).toHaveLength(1);
    expect(lu.liens).toHaveLength(schema.liens.length + 1);
    // Les données du modèle que draw.io ne montre pas (interfaces, VLAN…) sont gardées.
    expect(lu.equipements.find((e) => e.id === premier.id)?.interfaces.slice(0, premier.interfaces.length)).toEqual(premier.interfaces);
  });

  it("retire un équipement et ses câbles quand sa forme a disparu", async () => {
    const schema = schemaVide("Deux");
    const a = creerEquipement(schema, "switch", { x: 100, y: 100 });
    const b = creerEquipement(schema, "pc", { x: 300, y: 100 });
    const [ia, ib] = [a.interfaces[0], b.interfaces[0]];
    if (!ia || !ib) throw new Error("pas d'interface");
    connecter(schema, { equipement: a.id, interface: ia.id }, { equipement: b.id, interface: ib.id });
    const xml = ecrireDrawio(schema).replace(new RegExp(`<object id="${b.id}"[^]*?</object>`), "");
    const { schema: lu } = await lireImport(xml);
    expect(lu.equipements.map((e) => e.id)).toEqual([a.id]);
    expect(lu.liens).toEqual([]);
  });
});

const ICONES: IconesDrawio[] = ["kyfrev", "drawio"];

describe("draw.io : ce que montre kyfRev (schéma d'essai : cœur ×2, pare-feux en HA, LASOTEL, agrégats, trois pages)", () => {
  for (const icones of ICONES) {
    it(`${icones} : le nom en gras, l'adresse dessous en chasse fixe, sur une ligne, juste sous l'illustration`, () => {
      const schema = commeUnVrai();
      const xml = ecrireDrawio(schema, icones);
      const coeur = equipementNomme(schema, "SKFRMEY-CA06A-CORE");
      const c = dans(xml, coeur.id);
      const label = c?.attributs.label ?? "";
      expect(label).toContain('<span style="font-weight:600">SKFRMEY-CA06A-CORE</span><br>');
      expect(label).toContain("font-family:Cascadia Mono,Consolas,monospace");
      expect(texteDuLibelle(label, true)).toBe("SKFRMEY-CA06A-CORE\n10.21.31.1/24");
      expect(styleDe(c)).toMatchObject({ html: "1", whiteSpace: "nowrap", verticalLabelPosition: "bottom", verticalAlign: "top" });
      // Le haut du nom est au bas de l'illustration, comme dans kyfRev (le carré de 56 laisse du blanc dessous).
      const cadre = cadreDeLaForme(coeur, icones);
      const decalage = geometrie(c)?.enfants.find((e) => e.attributs.as === "offset");
      expect(cadre.y + cadre.hauteur + Number(decalage?.attributs.y ?? 0)).toBeCloseTo(boiteDe(coeur, 0).y1, 1);
      expect(geometrie(c)?.attributs).toMatchObject({ x: ecrit(cadre.x), y: ecrit(cadre.y), width: ecrit(cadre.largeur), height: ecrit(cadre.hauteur) });
    });

    it(`${icones} : la pastille « ×N » d'un stack, posée sur l'équipement comme dans kyfRev`, () => {
      const schema = commeUnVrai();
      const xml = ecrireDrawio(schema, icones);
      for (const [nom, membres] of [
        ["SKFRMEY-CA06A-CORE", "×2"],
        ["SKFRMEY-CA06A-SW", "×3"],
      ] as const) {
        const e = equipementNomme(schema, nom);
        const pastille = dans(xml, `${e.id}-membres`);
        expect(pastille?.attributs).toMatchObject({ label: membres, decor: "membres" });
        expect(mxCell(pastille)?.attributs.parent).toBe(e.id);
        const g = geometrie(pastille);
        expect(g?.attributs).toMatchObject({ relative: "1", width: "24", height: "15" });
        const decalage = g?.enfants.find((x) => x.attributs.as === "offset")?.attributs;
        const [cadre, b] = [cadreDeLaForme(e, icones), boiteDe(e, 0)];
        expect(cadre.x + Number(decalage?.x)).toBeCloseTo(b.x1 - 16, 1);
        expect(cadre.y + Number(decalage?.y)).toBeCloseTo(b.y0 - 10, 1);
      }
      expect(dans(xml, `${equipementNomme(schema, "FW_Master").id}-membres`)).toBeUndefined();
    });

    for (const coudes of [false, true]) {
      it(`${icones}, câbles ${coudes ? "coudés" : "droits"} : chaque câble a son connecteur, au tracé de kyfRev, les parallèles décalés`, () => {
        const schema = commeUnVrai({ coudes });
        const xml = ecrireDrawio(schema, icones);
        const mise = miseDuSiege(schema);
        const [coeur, secours] = [equipementNomme(schema, "SKFRMEY-CA06A-CORE"), equipementNomme(schema, "FW_Backup")];
        const paire = [coeur.id, secours.id].sort().join();
        const paralleles = mise.liens.filter((m) => [m.lien.a.equipement, m.lien.b.equipement].sort().join() === paire);
        expect(paralleles).toHaveLength(2);
        for (const { lien, trace, brins } of mise.liens) {
          const premier = (brins ?? [trace])[0] ?? [];
          const [debut, fin] = [boutFixe(xml, lien.id, "exit"), boutFixe(xml, lien.id, "entry")];
          expect(proche(debut.x, premier[0]?.x ?? Number.NaN) && proche(debut.y, premier[0]?.y ?? Number.NaN), `${lien.id} départ`).toBe(true);
          expect(proche(fin.x, premier.at(-1)?.x ?? Number.NaN) && proche(fin.y, premier.at(-1)?.y ?? Number.NaN), `${lien.id} arrivée`).toBe(true);
          expect(pointsDe(xml, lien.id)).toEqual(premier.slice(1, -1).map((p) => ({ x: Number(ecrit(p.x)), y: Number(ecrit(p.y)) })));
        }
        // Les deux câbles parallèles ne se superposent pas : décalés comme dans kyfRev.
        const [a, b] = paralleles.map((m) => boutFixe(xml, m.lien.id, "exit"));
        expect(Math.hypot((a?.x ?? 0) - (b?.x ?? 0), (a?.y ?? 0) - (b?.y ?? 0))).toBeGreaterThan(10);
      });
    }

    it(`${icones} : un agrégat a un connecteur par trait et son cercle, là où kyfRev le pose`, () => {
      const schema = commeUnVrai();
      const xml = ecrireDrawio(schema, icones);
      const agregats = miseDuSiege(schema).liens.filter((m) => m.brins && m.cercle);
      expect(agregats).toHaveLength(2);
      for (const { lien, brins, cercle } of agregats) {
        if (!brins || !cercle) throw new Error("agrégat incomplet");
        expect(brins).toHaveLength(2);
        const second = dans(xml, `${lien.id}-brin-2`);
        expect(second?.attributs).toMatchObject({ decor: "brin", pour: lien.id });
        expect(mxCell(second)?.attributs).toMatchObject({ edge: "1", source: lien.a.equipement, target: lien.b.equipement });
        expect(boutFixe(xml, `${lien.id}-brin-2`, "exit").y).toBeCloseTo(brins[1]?.[0]?.y ?? Number.NaN, 1);
        expect(boutFixe(xml, lien.id, "exit").y).toBeCloseTo(brins[0]?.[0]?.y ?? Number.NaN, 1);
        const rond = dans(xml, `${lien.id}-cercle`);
        expect(rond?.attributs.decor).toBe("cercle");
        expect(styleDe(rond)).toMatchObject({ ellipse: "", rotation: ecrit(cercle.angle), fillColor: "none" });
        const r = rectDe(rond);
        expect((r.x0 + r.x1) / 2).toBeCloseTo(cercle.x, 1);
        expect((r.y0 + r.y1) / 2).toBeCloseTo(cercle.y, 1);
        expect(r.x1 - r.x0).toBeCloseTo(cercle.rx * 2, 1);
      }
    });

    it(`${icones} : les étiquettes de kyfRev (ports, agrégats, accès, trunk), à leur place, sans rien chevaucher`, () => {
      const schema = commeUnVrai();
      const xml = ecrireDrawio(schema, icones);
      const mise = miseDuSiege(schema);
      const attendues = [...mise.etiquettes.entries()].flatMap(([pour, liste]) => liste.map((e) => ({ pour, e })));
      const pastilles = toutes(xml).filter(
        (c) => ["port-a", "port-b", "nom", "vlan"].includes(c.attributs.decor ?? "") && attendues.some((a) => a.pour === c.attributs.pour),
      );
      expect(pastilles.map((c) => c.attributs.label).sort()).toEqual(attendues.map((a) => a.e.texte).sort());
      for (const texte of ["Access · U 2131", "Trk1 · 1/9,3/9", "Trk9 · 1/10,2/10", "Trunk · U 1 · T 10,20,30", "LAN1", "port3"]) {
        expect(pastilles.map((c) => c.attributs.label)).toContain(texte);
      }
      for (const { pour, e } of attendues) {
        const r = rectDe(dans(xml, `${pour}-${e.cle}`));
        const b = cadreEtiquette(e, 0);
        expect(r.x0).toBeCloseTo(b.x0, 1);
        expect(r.y0).toBeCloseTo(b.y0, 1);
      }
      // La couleur du VLAN d'accès, comme kyfRev.
      const acces = pastilles.find((c) => c.attributs.label === "Access · U 2131");
      expect(styleDe(acces)).toMatchObject({ strokeColor: "#c2185b", fontColor: "#c2185b" });
      // Rien ne se superpose : ni deux pastilles, ni une pastille et une illustration.
      const rects = pastilles.map(rectDe);
      for (const [i, a] of rects.entries()) for (const b of rects.slice(i + 1)) expect(seChevauchent(a, b)).toBe(false);
      for (const e of surLaPage(schema, "p_siege").equipements) {
        const cadre = cadreDeLaForme(e, "drawio");
        const icone = { x0: cadre.x, y0: cadre.y, x1: cadre.x + cadre.largeur, y1: cadre.y + cadre.hauteur };
        for (const r of rects) expect(seChevauchent(r, icone), e.nom).toBe(false);
      }
    });
  }

  it("suit l'affichage du schéma : sans ports, VLAN, adresses, zones ni notes, rien de cela ne se voit", () => {
    const schema = commeUnVrai();
    schema.annotations.push(
      { id: "n_zone", texte: "DMZ", forme: "rectangle", position: { x: 0, y: 700 }, largeur: 200, hauteur: 100, page: "p_siege" },
      { id: "n_note", texte: "À voir", position: { x: 300, y: 900 }, page: "p_siege" },
    );
    const avant = ecrireDrawio(schema);
    expect(toutes(avant).some((c) => c.attributs.decor === "port-a")).toBe(true);
    expect(toutes(avant).some((c) => c.attributs.decor === "vlan")).toBe(true);
    expect(mxCell(dans(avant, "n_zone"))?.attributs.visible).toBeUndefined();
    schema.affichage = { ports: false, vlans: false, adresses: false, zones: false, annotations: false };
    const apres = ecrireDrawio(schema);
    expect(toutes(apres).some((c) => c.attributs.decor === "port-a" || c.attributs.decor === "port-b")).toBe(false);
    expect(toutes(apres).some((c) => c.attributs.decor === "vlan")).toBe(false);
    expect(texteDuLibelle(dans(apres, equipementNomme(schema, "FW_Master").id)?.attributs.label ?? "", true)).toBe("FW_Master");
    expect(mxCell(dans(apres, "n_zone"))?.attributs.visible).toBe("0");
    expect(dans(apres, "n_note")?.attributs.visible).toBe("0");
  });

  it("montre le nom d'un câble au milieu, et ce que la vue cache reste dans le fichier, caché", () => {
    const schema = commeUnVrai();
    const [coeur, maitre] = [equipementNomme(schema, "SKFRMEY-CA06A-CORE"), equipementNomme(schema, "FW_Master")];
    const lien = schema.liens.find((l) => l.a.equipement === coeur.id && l.b.equipement === maitre.id);
    if (!lien) throw new Error("câble absent");
    lien.nom = "Uplink FW";
    const xml = ecrireDrawio(schema);
    expect(dans(xml, `${lien.id}-nom`)?.attributs).toMatchObject({ label: "Uplink FW", decor: "nom", pour: lien.id });
    // Le libellé du connecteur garde le nom (l'import le lit), caché derrière la pastille.
    expect(dans(xml, lien.id)?.attributs.label).toBe("Uplink FW");
    expect(styleDe(dans(xml, lien.id)).noLabel).toBe("1");
    // En vue Virtualisation, la relation de haute disponibilité ne se voit pas : elle est là, cachée.
    const virtualisation = ecrireDrawio(schema, "kyfrev", vue("virtualisation"));
    expect(mxCell(dans(virtualisation, "r_ha"))?.attributs.visible).toBe("0");
    expect(mxCell(dans(xml, "r_ha"))?.attributs.visible).toBeUndefined();
    expect(styleDe(dans(xml, "r_ha"))).toMatchObject({ dashed: "1", fixDash: "1", endArrow: "none" });
  });

  it("dessine les zones en pointillé de la couleur principale (vert), teintées, comme à l'écran", () => {
    const xml = ecrireDrawio(commeUnVrai());
    expect(styleDe(dans(xml, "g_ca03"))).toMatchObject({ strokeColor: "#2e6a4e", fillColor: "#2e6a4e", fillOpacity: "6", dashed: "1", dashPattern: "6 4", fixDash: "1" });
  });
});

describe("draw.io : l'aller-retour du schéma d'essai", () => {
  for (const icones of ICONES) {
    for (const coudes of [false, true]) {
      it(`${icones}, câbles ${coudes ? "coudés" : "droits"} : rien de neuf, rien de perdu, aucun décor pris pour un objet`, async () => {
        const schema = commeUnVrai({ coudes });
        const importe = await lireImport(ecrireDrawio(schema, icones), "C:/x/SKFRMEY.drawio");
        expect(importe.erreurs).toEqual([]);
        expect(ecrireSchema({ ...importe.schema, id: schema.id })).toBe(ecrireSchema(lireSchema(ecrireSchema(schema)).schema));
        expect(compteRendu(importe)).toMatchObject({ equipements: 10, cables: schema.liens.length, zones: 2, textes: 0, inconnus: [], courbes: 0, sansExtremite: 0, pages: 3 });
      });
    }
  }

  it("sans le modèle, les décors ne deviennent ni équipements, ni câbles, ni zones, ni textes", async () => {
    const schema = commeUnVrai();
    const xml = ecrireDrawio(schema).replace(/ kyfrev="[^"]*"/, "");
    const { schema: lu } = await lireImport(xml);
    expect(lu.equipements).toHaveLength(10);
    // Les câbles d'une page ; les deux câbles entre deux pages ne se voient qu'en renvois.
    expect(lu.liens).toHaveLength(schema.liens.length - 2);
    expect(lu.groupes).toHaveLength(2);
    expect(lu.annotations).toEqual([]);
    expect(lu.relations).toHaveLength(1);
  });

  it("un point de passage ajouté dans draw.io devient celui du câble ; une pastille de nom récrite le renomme", async () => {
    const schema = commeUnVrai();
    const [coeur, maitre] = [equipementNomme(schema, "SKFRMEY-CA06A-CORE"), equipementNomme(schema, "FW_Master")];
    const lien = schema.liens.find((l) => l.a.equipement === coeur.id && l.b.equipement === maitre.id);
    if (!lien) throw new Error("câble absent");
    lien.nom = "Uplink FW";
    let xml = ecrireDrawio(schema);
    xml = xml.replace(
      new RegExp(`(<object id="${lien.id}"[^]*?)<mxGeometry relative="1" as="geometry"></mxGeometry>`),
      '$1<mxGeometry relative="1" as="geometry"><Array as="points"><mxPoint x="300" y="200"/></Array></mxGeometry>',
    );
    xml = xml.replace(`id="${lien.id}-nom" label="Uplink FW"`, `id="${lien.id}-nom" label="Lien du pare-feu"`);
    const { schema: lu } = await lireImport(xml);
    expect(lu.liens.find((l) => l.id === lien.id)).toMatchObject({ nom: "Lien du pare-feu", points: [{ x: 300, y: 200 }] });
    expect(lu.liens.filter((l) => l.id !== lien.id).every((l) => l.points.length === 0)).toBe(true);
    expect(lu.liens).toHaveLength(schema.liens.length);
  });

  it("retrouve le centre de kyfRev d'après le cadre d'une forme : emprise de l'illustration, ou carré de 56", () => {
    const e = { type: "switch", position: { x: 101, y: 57 } };
    for (const icones of ICONES) expect(positionDeLaForme(e.type, cadreDeLaForme(e, icones))).toEqual(e.position);
    expect(positionDeLaForme("switch", { x: 0, y: 0, largeur: 80, hauteur: 40 })).toEqual({ x: 40, y: 20 });
  });
});
