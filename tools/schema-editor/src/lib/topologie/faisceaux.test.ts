import { describe, expect, it } from "vitest";
import {
  abregerVlans,
  annotationsDeTrunk,
  dessinerSchema,
  ecartsParalleles,
  type Etiquette,
  faisceauxDe,
  LEGENDE_CERCLE,
  type Marque,
  marqueDeFaisceau,
  mettreEnPage,
  milieu,
  PAS_FAISCEAU,
} from "./dessin";
import { type Equipement, type IdVue, type Point, type Schema, schemaVide } from "./modele";
import { connecter, creerEquipement } from "./operations";
import {
  agreger,
  changerTypeDeCable,
  ecrireVlansDuTrunk,
  epurer,
  mettreEnTrunk,
  tirerCable,
} from "./simplifie";
import { basculerMembre, creerAgregat, ecrireConfigDuBout, enAgregat, estTrunk, texteTrunk } from "./vlans";
import { vue } from "./vues";

// Deux choses qui ne se confondent pas. L'agrégat de liens : autant de traits que de câbles, sous un
// cercle, sans libellé. Le trunk : une annotation sur le câble, « Trunk » et ses VLAN, sans cercle.

const port = (e: Equipement, nom: string) => {
  const i = e.interfaces.find((x) => x.nom === nom);
  if (!i) throw new Error(`port ${nom} absent`);
  return { equipement: e.id, interface: i.id };
};

/** Deux switches côte à côte, reliés par `nombre` câbles ordinaires. */
function paire(nombre: number, position: Point = { x: 400, y: 0 }) {
  const schema = schemaVide("Liaison");
  const a = creerEquipement(schema, "switch", { x: 0, y: 0 });
  const b = creerEquipement(schema, "switch", position);
  const cables = Array.from({ length: nombre }, () => tirerCable(schema, a.id, b.id, { trunk: false }));
  return { schema, a, b, cables, ids: cables.map((l) => l.id) };
}

/** Les mêmes, réunis en un agrégat de `nombre` câbles. */
function agregat(nombre: number, position?: Point) {
  const liaison = paire(nombre, position);
  const lien = agreger(liaison.schema, liaison.a.id, liaison.b.id);
  return { ...liaison, lien };
}

const pageDe = (schema: Schema, vueVoulue: IdVue = "simplifiee") =>
  mettreEnPage(schema, vue(vueVoulue), new Set());
/** Tous les traits dessinés entre les équipements. */
const traitsDe = (schema: Schema) => pageDe(schema).liens.flatMap((l) => l.brins ?? [l.trace]);
const cerclesDe = (schema: Schema) =>
  pageDe(schema)
    .liens.map((l) => l.cercle)
    .filter((c): c is Marque => c !== undefined);
const etiquettesDe = (schema: Schema, vueVoulue: IdVue = "simplifiee"): Etiquette[] =>
  [...pageDe(schema, vueVoulue).etiquettes.values()].flat();
const textesDe = (schema: Schema, vueVoulue: IdVue = "simplifiee") =>
  etiquettesDe(schema, vueVoulue).map((e) => e.texte);
/** Le milieu des câbles (l'illustration d'un switch n'est pas tout à fait centrée : ce n'est pas x = 200). */
const milieuDe = (schema: Schema) => milieu(pageDe(schema).liens[0]?.trace ?? []).x;
/** L'ordonnée de chaque trait, de haut en bas (les équipements sont sur une même ligne). */
const hauteurs = (traits: Point[][]) => traits.map((t) => t[0]?.y ?? 0).sort((x, y) => x - y);

describe("agrégat de liens : autant de traits que de câbles, sous un cercle", () => {
  it("un agrégat de deux câbles : deux traits côte à côte, un cercle, aucun libellé", () => {
    const { schema, lien } = agregat(2);
    // Le modèle n'a qu'un câble pour l'agrégat : c'est le dessin qui montre ses ports.
    expect(schema.liens.length).toBe(1);
    expect(enAgregat(schema, lien)).toBe(true);
    expect(hauteurs(traitsDe(schema))).toEqual([-PAS_FAISCEAU / 2, PAS_FAISCEAU / 2]);
    const [cercle, ...autres] = cerclesDe(schema);
    expect(autres).toEqual([]);
    // Au milieu des câbles, en travers (le grand axe est vertical pour des câbles horizontaux).
    expect(cercle).toMatchObject({ x: milieuDe(schema), y: 0, rx: 12, ry: 5 });
    expect(Math.abs(cercle?.angle ?? 0)).toBe(90);
    // Un agrégat n'est pas un trunk : rien n'est écrit sur les câbles.
    expect(estTrunk(schema, lien)).toBe(false);
    expect(textesDe(schema)).toEqual([]);
  });

  it("trois câbles : trois traits, et un cercle assez grand pour les trois", () => {
    const { schema } = agregat(3);
    expect(hauteurs(traitsDe(schema))).toEqual([-8, 0, 8]);
    const cercles = cerclesDe(schema);
    expect(cercles.length).toBe(1);
    expect(cercles[0]?.rx).toBeGreaterThan(8 + 2);
    expect(cercles[0]).toMatchObject({ x: milieuDe(schema), y: 0 });
  });

  it("serrés, des traits épais (la fibre : 3,5) sont affinés pour rester distincts", () => {
    const { schema, lien } = agregat(2);
    changerTypeDeCable(schema, lien.id, "fibre-multimode");
    expect(pageDe(schema).liens.map((l) => l.trait.epaisseur)).toEqual([2.4]);
    expect(traitsDe(schema).length).toBe(2);
  });

  it("un agrégat fait en avancé, port par port : même dessin ; un seul port, un seul trait cerclé", () => {
    const schema = schemaVide("Agrégat");
    const a = creerEquipement(schema, "switch", { x: 0, y: 0 });
    const b = creerEquipement(schema, "switch", { x: 400, y: 0 });
    const lien = connecter(schema, port(a, "1/1"), port(b, "1/1"));
    creerAgregat(schema, lien.id);
    expect(traitsDe(schema).length).toBe(1);
    expect(cerclesDe(schema).length).toBe(1);
    for (const n of [2, 3]) {
      basculerMembre(schema, lien.id, "a", port(a, `1/${n}`).interface);
      basculerMembre(schema, lien.id, "b", port(b, `1/${n}`).interface);
    }
    expect(hauteurs(traitsDe(schema))).toEqual([-8, 0, 8]);
    expect(cerclesDe(schema).length).toBe(1);
    // En vue physique, les étiquettes de l'agrégat restent ce qu'elles étaient.
    expect(textesDe(schema, "physique")).toEqual(["Trk1 · 1/1,1/2,1/3", "Trk1 · 1/1,1/2,1/3"]);
  });

  it("un câble ordinaire à côté d'un agrégat reste hors du cercle", () => {
    const { schema, a, b, lien } = agregat(2);
    const simple = connecter(schema, port(a, "1/5"), port(b, "1/5"));
    const page = pageDe(schema);
    const [cercle, ...autres] = cerclesDe(schema);
    expect(autres).toEqual([]);
    const y = (id: string) => page.liens.find((l) => l.lien.id === id)?.trace[0]?.y ?? Number.NaN;
    // L'agrégat occupe 8 px ; le câble ordinaire est à un écart de câble de son trait le plus proche.
    expect(Math.abs(y(simple.id) - y(lien.id))).toBe(PAS_FAISCEAU / 2 + 16);
    expect(cercle?.y).toBe(y(lien.id));
    expect(Math.abs(y(simple.id) - (cercle?.y ?? 0))).toBeGreaterThan(cercle?.rx ?? 0);
  });
});

describe("le cercle en tracé à angle", () => {
  it("se pose sur un segment droit, loin des coudes et des équipements", () => {
    for (const nombre of [2, 3]) {
      const { schema } = agregat(nombre, { x: 300, y: 240 });
      schema.cablesCoudes = true;
      const traits = traitsDe(schema);
      expect(traits.length, `${nombre} câbles`).toBe(nombre);
      // Chaque trait ne fait que des segments horizontaux ou verticaux.
      for (const trait of traits) {
        for (let i = 1; i < trait.length; i++) {
          const [p, q] = [trait[i - 1] as Point, trait[i] as Point];
          expect(Math.abs(p.x - q.x) < 0.01 || Math.abs(p.y - q.y) < 0.01).toBe(true);
        }
      }
      const cercles = cerclesDe(schema);
      expect(cercles.length).toBe(1);
      const cercle = cercles[0] as Marque;
      // En travers d'un segment horizontal ou vertical, jamais de biais.
      expect(Math.abs(cercle.angle) % 90).toBe(0);
      // Aucun coude à moins de 14 px du cercle.
      for (const trait of traits) {
        for (const coude of trait.slice(1, -1)) {
          expect(Math.hypot(coude.x - cercle.x, coude.y - cercle.y)).toBeGreaterThan(14);
        }
      }
      // Et loin des deux équipements.
      for (const e of schema.equipements) {
        expect(Math.hypot(e.position.x - cercle.x, e.position.y - cercle.y)).toBeGreaterThan(60);
      }
    }
  });

  it("le milieu du tracé tombe sur un coude : le cercle va au milieu du plus long segment", () => {
    // Un L : 100 px à l'horizontale, puis 100 px à la verticale. Le milieu est le coude.
    const marque = marqueDeFaisceau(
      [
        { x: 0, y: 0 },
        { x: 100, y: 0 },
        { x: 100, y: 100 },
      ],
      8,
    );
    expect(marque).toMatchObject({ x: 50, y: 0, rx: 12 });
    // Un tracé droit : toujours son milieu.
    expect(marqueDeFaisceau([{ x: 0, y: 0 }, { x: 0, y: 80 }])).toMatchObject({ x: 0, y: 40 });
  });
});

describe("trunk : une annotation sur le câble, pas de cercle", () => {
  it("un câble en trunk : un trait, aucun cercle, l'étiquette « Trunk » même en vue simplifiée", () => {
    const { schema, ids, cables } = paire(1);
    mettreEnTrunk(schema, ids, true);
    expect(traitsDe(schema).length).toBe(1);
    expect(cerclesDe(schema)).toEqual([]);
    expect(faisceauxDe(schema, schema.liens).size).toBe(0);
    expect(enAgregat(schema, cables[0] as (typeof cables)[number])).toBe(false);
    const etiquettes = etiquettesDe(schema);
    expect(etiquettes.map((e) => [e.genre, e.texte])).toEqual([["vlan", "Trunk"]]);
    // L'épuration du mode simplifié garde cette annotation, même si le schéma affiche les VLAN en avancé.
    schema.affichage = { vlans: true, ports: true };
    const epure = epurer(schema);
    expect([...mettreEnPage(epure, vue("simplifiee"), new Set()).etiquettes.values()].flat().map((e) => e.texte)).toEqual([
      "Trunk",
    ]);
  });

  it("l'étiquette dit les VLAN : non tagué (U) et tagués (T)", () => {
    const { schema, ids, cables } = paire(1);
    const lien = cables[0] as (typeof cables)[number];
    ecrireVlansDuTrunk(schema, ids, { nonTague: 1, tagues: "10,20,30" });
    expect(texteTrunk(schema, lien)).toBe("Trunk · U 1 · T 10,20,30");
    expect(textesDe(schema)).toEqual(["Trunk · U 1 · T 10,20,30"]);
    ecrireVlansDuTrunk(schema, ids, { tagues: "10,20" });
    expect(textesDe(schema)).toEqual(["Trunk · T 10,20"]);
    ecrireVlansDuTrunk(schema, ids, { nonTague: 99 });
    expect(textesDe(schema)).toEqual(["Trunk · U 99"]);
    ecrireVlansDuTrunk(schema, ids, {});
    expect(textesDe(schema)).toEqual(["Trunk"]);
  });

  it("une longue liste est coupée après un numéro entier ; la liste entière reste dans le titre", () => {
    const longue = "Trunk · U 1 · T 10,20,30-40,50,60,70,80,90,100";
    expect(abregerVlans("Trunk · U 1 · T 10,20,30")).toBe("Trunk · U 1 · T 10,20,30");
    expect(abregerVlans(longue)).toBe("Trunk · U 1 · T 10,20,30-40,50,…");
    expect(abregerVlans(longue).length).toBeLessThanOrEqual(32);
    const { schema, ids } = paire(1);
    ecrireVlansDuTrunk(schema, ids, { nonTague: 1, tagues: "10,20,30-40,50,60,70,80,90,100" });
    const [etiquette] = etiquettesDe(schema);
    expect(etiquette).toMatchObject({ texte: "Trunk · U 1 · T 10,20,30-40,50,…", titre: longue });
    const svg = dessinerSchema(schema, { vue: vue("simplifiee") });
    expect(svg).toContain(`<title>${longue}</title>`);
    expect(svg).toContain(">Trunk · U 1 · T 10,20,30-40,50,…<");
  });

  it("des câbles parallèles en trunk sans agrégat : chacun son trait, pas de cercle, une seule étiquette s'ils ont les mêmes VLAN", () => {
    const { schema, ids, cables } = paire(2);
    ecrireVlansDuTrunk(schema, ids, { nonTague: 1, tagues: "10,20" });
    // Des câbles ordinaires : 16 px entre eux, comme sans trunk.
    expect(hauteurs(traitsDe(schema))).toEqual([-8, 8]);
    expect(cerclesDe(schema)).toEqual([]);
    expect(textesDe(schema)).toEqual(["Trunk · U 1 · T 10,20"]);
    expect([...annotationsDeTrunk(schema, schema.liens).keys()]).toEqual([cables[0]?.id]);
    // Des VLAN différents d'un câble à l'autre : chacun dit les siens.
    ecrireVlansDuTrunk(schema, [ids[1] as string], { tagues: "30" });
    expect(textesDe(schema).sort()).toEqual(["Trunk · T 30", "Trunk · U 1 · T 10,20"]);
    // L'écart ne dépend pas du trunk.
    expect([...ecartsParalleles(schema.liens, faisceauxDe(schema, schema.liens)).values()].map(Math.abs)).toEqual([8, 8]);
  });

  it("un agrégat qui est aussi un trunk : les traits sous le cercle, et une seule étiquette", () => {
    const { schema, lien } = agregat(3);
    ecrireVlansDuTrunk(schema, [lien.id], { nonTague: 1, tagues: "10,20,30" });
    expect(traitsDe(schema).length).toBe(3);
    expect(cerclesDe(schema).length).toBe(1);
    expect(textesDe(schema)).toEqual(["Trunk · U 1 · T 10,20,30"]);
    // L'étiquette ne recouvre pas le cercle.
    const [etiquette] = etiquettesDe(schema);
    const [cercle] = cerclesDe(schema);
    expect(Math.hypot((etiquette?.x ?? 0) - (cercle?.x ?? 0), (etiquette?.y ?? 0) - (cercle?.y ?? 0))).toBeGreaterThan(12);
  });

  it("un port d'accès n'est pas un trunk : rien en vue simplifiée ; un seul bout en trunk non plus", () => {
    const { schema, cables } = paire(1);
    const lien = cables[0] as (typeof cables)[number];
    ecrireConfigDuBout(schema, lien.a, { mode: "acces", acces: 20 });
    ecrireConfigDuBout(schema, lien.b, { mode: "acces", acces: 20 });
    expect(textesDe(schema)).toEqual([]);
    expect(textesDe(schema, "logique")).toEqual(["VLAN 20"]);
    ecrireConfigDuBout(schema, lien.a, { mode: "trunk", tagues: "10" });
    expect(estTrunk(schema, lien)).toBe(false);
    expect(textesDe(schema)).toEqual([]);
  });

  it("en vue « Logique et VLAN », une seule étiquette par câble : celle du trunk tient lieu d'étiquette de VLAN", () => {
    const { schema, ids } = paire(1);
    ecrireVlansDuTrunk(schema, ids, { nonTague: 1, tagues: "10,20" });
    expect(textesDe(schema, "logique")).toEqual(["Trunk · U 1 · T 10,20"]);
    // « VLAN des câbles » décoché dans le schéma : plus aucune étiquette de VLAN, trunk compris.
    schema.affichage = { vlans: false };
    expect(textesDe(schema, "logique")).toEqual([]);
    expect(textesDe(schema, "physique").filter((t) => t.startsWith("Trunk"))).toEqual([]);
  });
});

describe("le dessin exporté", () => {
  it("agrégat : autant de traits que de câbles, un cercle sans libellé, et la légende qui le nomme", () => {
    const { schema } = agregat(2);
    const svg = dessinerSchema(schema, { vue: vue("simplifiee"), legende: true });
    // Les câbles sont dessinés avant les équipements : deux câbles, deux traits, un cercle.
    const cables = svg.slice(svg.indexOf("</defs>"), svg.indexOf("<title>"));
    expect(cables.match(/<path /g)?.length).toBe(2);
    expect(cables.match(/<ellipse /g)?.length).toBe(1);
    // La légende, après le dernier équipement, montre le cercle et dit ce qu'il veut dire.
    const legende = svg.slice(svg.lastIndexOf("</g>"));
    expect(legende.match(/<ellipse /g)?.length).toBe(1);
    expect(LEGENDE_CERCLE).toBe("Cercle : agrégat de liens");
    expect(legende).toContain(`>${LEGENDE_CERCLE}<`);
    expect(svg).not.toMatch(/>[^<]*[Tt]runk[^<]*</);
    // Sans légende demandée (aperçu des listes), aucun texte ne parle du cercle.
    expect(dessinerSchema(schema)).not.toContain("Cercle");
  });

  it("trunk sans agrégat : l'étiquette est dans l'image, sans cercle ni mention du cercle en légende", () => {
    const { schema, ids } = paire(2);
    ecrireVlansDuTrunk(schema, ids, { nonTague: 1, tagues: "10,20" });
    const svg = dessinerSchema(schema, { vue: vue("simplifiee"), legende: true });
    const cables = svg.slice(svg.indexOf("</defs>"), svg.indexOf("<title>"));
    expect(cables.match(/<path /g)?.length).toBe(2);
    expect(cables.match(/<ellipse /g)).toBeNull();
    expect(svg.match(/>Trunk · U 1 · T 10,20</g)?.length).toBe(1);
    expect(svg).not.toContain("Cercle");
  });
});
