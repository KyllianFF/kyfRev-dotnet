import { describe, expect, it } from "vitest";
import { typeConnu } from "./catalogue";
import { COTE } from "./dessin";
import { ecrireDrawio, FORMES_DRAWIO, typeDeLaForme, typeDevine } from "./drawio";
import { ecrireSchema, erreursStructurelles, lireSchema } from "./format";
import { GABARITS } from "./gabarits";
import { bilanImport, type CompteRendu, estFichierDrawio, lireImport, nomDuFichier, remarquesImport, texteDuLibelle } from "./importation";
import { type Schema, schemaVide } from "./modele";
import { collerEquipements, connecter, creerEquipement } from "./operations";
import { estImage, FORMATS_EXPORT } from "./sorties";
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
    expect(c?.attributs).toMatchObject({ label: premier.nom, nom: premier.nom, type: premier.type, ip: "10.0.0.1", vlan: "10", site: "Lyon" });
    expect(c?.attributs).not.toHaveProperty("salle");
    // Dans la zone : ses coordonnées comptent depuis le coin de la zone.
    expect(mxCell(c)?.attributs.parent).toBe("g_zone");
    expect(geometrie(c)?.attributs).toMatchObject({ x: "72", y: "72", width: String(COTE), height: String(COTE) });
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

  it("écrit un câble en connecteur : extrémités, coudé, pointillé, couleur, points, ports aux deux bouts", () => {
    const schema = demonstration();
    const lien = schema.liens[0];
    if (!lien) throw new Error("pas de câble");
    const xml = ecrireDrawio(schema);
    const c = mxCell(cellule(xml, lien.id));
    expect(c?.attributs).toMatchObject({ edge: "1", source: lien.a.equipement, target: lien.b.equipement });
    expect(c?.attributs.style).toContain("edgeStyle=orthogonalEdgeStyle;");
    expect(c?.attributs.style).toContain("dashed=1;dashPattern=1 5;");
    expect(c?.attributs.style).toContain("strokeColor=#c0392b;");
    expect(geometrie(cellule(xml, lien.id))?.enfants[0]?.enfants[0]?.attributs).toMatchObject({ x: "120", y: "80" });
    expect(cellule(xml, `${lien.id}-a`)?.attributs.parent).toBe(lien.id);
    expect(cellule(xml, `${lien.id}-b`)?.attributs.value).toBeTruthy();
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
    expect(schema.titre).toBe("Réseau du labo");
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
    expect(c).toMatchObject({ equipements: 3, cables: 2, zones: 2, textes: 2, courbes: 1, sansExtremite: 1, pagesIgnorees: ["Étage 2"] });
    expect(c.inconnus).toEqual([parNom("Fonction")?.id]);
    expect(remarquesImport(c).map((r) => r.texte)).toEqual([
      "1 forme de type inconnu, posée en « Générique »",
      "1 connecteur courbe, redressé en coudé",
      "1 connecteur sans extrémité, laissé de côté",
      "page « Étage 2 » non importée : un schéma, une page",
    ]);
    expect(remarquesImport(c)[0]?.montrer).toBe(true);
    expect(bilanImport(c)).toEqual(["3 équipements", "2 câbles, avec leurs ports", "2 zones et 2 textes"]);
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
    expect(lu.compteRendu?.pagesIgnorees).toEqual([]);
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
    // Le second équipement : déplacé de 100 vers la droite et renommé.
    const ancien = `<mxGeometry x="${second.position.x - COTE / 2}" y="${second.position.y - COTE / 2}" width="56" height="56" as="geometry"/>`;
    expect(xml).toContain(ancien);
    xml = xml.replace(ancien, `<mxGeometry x="${second.position.x - COTE / 2 + 100}" y="${second.position.y - COTE / 2}" width="56" height="56" as="geometry"/>`);
    xml = xml.replace(`label="${echapperXml(second.nom)}"`, 'label="Cœur B"');
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
