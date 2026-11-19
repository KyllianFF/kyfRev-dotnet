import { describe, expect, it } from "vitest";
import {
  blocDuNom,
  boiteEtiquette,
  dessinerSchema,
  type Etiquette,
  mettreEnPage,
  occupationDe,
  placerEtiquettes,
  placeSurLeTrace,
  pointDeLaPlace,
  transformeDuBloc,
} from "./dessin";
import {
  angleDeLaPoignee,
  type CibleEtiquette,
  cranSuivant,
  ecrirePose,
  memeCible,
  placer,
  poseDe,
  remettre,
  remettreToutes,
  tourner,
} from "./etiquettes";
import { ecrireSchema, lireSchema } from "./format";
import { angleLisible, type Point, schemaVide } from "./modele";
import { creerEquipement, relier } from "./operations";
import { aDesReglages, ecrireVlansDuTrunk, epurer, mettreEnTrunk, tirerCable } from "./simplifie";
import { vue } from "./vues";

/** Un cœur et un switch reliés par un trunk, et une borne sous le switch. */
function site() {
  const schema = schemaVide("Étiquettes");
  schema.cablesCoudes = false;
  const coeur = creerEquipement(schema, "switch-coeur", { x: 0, y: 0 });
  const sw = creerEquipement(schema, "switch", { x: 360, y: 0 });
  const borne = creerEquipement(schema, "point-acces", { x: 360, y: 220 });
  const trunk = tirerCable(schema, coeur.id, sw.id);
  mettreEnTrunk(schema, [trunk.id], true);
  ecrireVlansDuTrunk(schema, [trunk.id], { nonTague: 1, tagues: "10,20,30" });
  const acces = tirerCable(schema, sw.id, borne.id);
  return { schema, coeur, sw, borne, trunk, acces };
}

const physique = vue("physique");
const etiquettesDe = (schema: ReturnType<typeof site>["schema"], id: string) =>
  mettreEnPage(schema, physique, new Set()).etiquettes.get(id) ?? [];
const vlanDe = (schema: ReturnType<typeof site>["schema"], id: string) =>
  etiquettesDe(schema, id).find((e) => e.cle === "vlan") as Etiquette;
const chevauchent = (a: Etiquette, b: Etiquette) => {
  const [p, q] = [boiteEtiquette(a, 0), boiteEtiquette(b, 0)];
  return p.x0 < q.x1 && q.x0 < p.x1 && p.y0 < q.y1 && q.y0 < p.y1;
};

describe("angles des étiquettes", () => {
  it("jamais à l'envers : ramenés entre −90 et 90", () => {
    expect(angleLisible(0)).toBe(0);
    expect(angleLisible(-0)).toBe(0);
    expect(angleLisible(30)).toBe(30);
    expect(angleLisible(-90)).toBe(-90);
    expect(angleLisible(90)).toBe(-90);
    expect(angleLisible(135)).toBe(-45);
    expect(angleLisible(180)).toBe(0);
    expect(angleLisible(-100)).toBe(80);
    expect(angleLisible(405)).toBe(45);
  });

  it("la poignée tourne par crans de 15°, au degré près avec Alt", () => {
    const centre = { x: 100, y: 100 };
    // Juste au-dessus : à plat.
    expect(angleDeLaPoignee(centre, { x: 100, y: 40 }, false)).toBe(0);
    // À droite, un peu au-dessus de l'horizontale (80°) : le cran le plus proche, 75°.
    const a80 = (80 * Math.PI) / 180;
    const vers = (a: number) => ({ x: 100 + Math.sin(a) * 50, y: 100 - Math.cos(a) * 50 });
    expect(angleDeLaPoignee(centre, vers(a80), false)).toBe(75);
    expect(angleDeLaPoignee(centre, vers(a80), true)).toBe(80);
    expect(angleDeLaPoignee(centre, vers((-27 * Math.PI) / 180), false)).toBe(-30);
    expect(angleDeLaPoignee(centre, vers((-27 * Math.PI) / 180), true)).toBe(-27);
    // Tirée vers le bas : le texte se retourne plutôt que de se lire à l'envers.
    expect(angleDeLaPoignee(centre, vers((150 * Math.PI) / 180), false)).toBe(-30);
  });

  it("au clavier : un cran de plus ou de moins, en boucle", () => {
    expect(cranSuivant(0, 1)).toBe(15);
    expect(cranSuivant(0, -1)).toBe(-15);
    expect(cranSuivant(22, 1)).toBe(30);
    expect(cranSuivant(75, 1)).toBe(-90);
    expect(cranSuivant(-90, -1)).toBe(75);
  });
});

describe("accroche au tracé", () => {
  it("une place mesurée sur le tracé se relit au même point, droit ou coudé", () => {
    const traces: Point[][] = [
      [
        { x: 0, y: 0 },
        { x: 300, y: 0 },
      ],
      [
        { x: 0, y: 0 },
        { x: 0, y: 120 },
        { x: 240, y: 120 },
      ],
    ];
    for (const trace of traces) {
      for (const centre of [
        { x: 40, y: -30 },
        { x: 180, y: 140 },
        { x: -25, y: 60 },
        { x: 400, y: 10 },
      ]) {
        const place = placeSurLeTrace(trace, centre);
        expect(place.t).toBeGreaterThanOrEqual(0);
        expect(place.t).toBeLessThanOrEqual(1);
        const relu = pointDeLaPlace(trace, place).centre;
        expect(relu.x).toBeCloseTo(centre.x, 0);
        expect(relu.y).toBeCloseTo(centre.y, 0);
      }
    }
  });

  it("au-dessus d'un câble horizontal : accrochée au point le plus proche, écart en travers", () => {
    const trace = [
      { x: 0, y: 0 },
      { x: 200, y: 0 },
    ];
    const place = placeSurLeTrace(trace, { x: 50, y: -30 });
    expect(place.t).toBeCloseTo(0.25, 4);
    expect(place.x).toBe(0);
    expect(Math.abs(place.y)).toBe(30);
    expect(pointDeLaPlace(trace, place).ancre).toEqual({ x: 50, y: 0 });
  });
});

describe("placer et tourner une étiquette", () => {
  it("une étiquette jamais touchée garde sa place calculée", () => {
    const { schema, trunk } = site();
    const avant = etiquettesDe(schema, trunk.id).map(({ x, y, texte }) => ({ x, y, texte }));
    const relu = lireSchema(ecrireSchema(schema)).schema;
    expect(relu.liens.every((l) => l.poses === undefined)).toBe(true);
    expect(etiquettesDe(relu, trunk.id).map(({ x, y, texte }) => ({ x, y, texte }))).toEqual(avant);
    expect(dessinerSchema(relu)).not.toContain("rotate(");
  });

  it("posée à la main : là où on l'a mise, accrochée à son câble, et les autres s'en écartent", () => {
    const { schema, trunk } = site();
    const vlan = vlanDe(schema, trunk.id);
    expect(vlan.ancre).toBeUndefined();
    const cible: CibleEtiquette = { genre: "lien", id: trunk.id, cle: "vlan" };
    // Posée juste sur la pastille du premier port.
    const port = etiquettesDe(schema, trunk.id).find((e) => e.cle === "port-a") as Etiquette;
    const trace = mettreEnPage(schema, physique, new Set()).liens.find((l) => l.lien.id === trunk.id)?.trace ?? [];
    placer(schema, cible, placeSurLeTrace(trace, { x: port.x, y: port.y }));
    const apres = etiquettesDe(schema, trunk.id);
    const posee = apres.find((e) => e.cle === "vlan") as Etiquette;
    expect(posee.x).toBeCloseTo(port.x, 0);
    expect(posee.y).toBeCloseTo(port.y, 0);
    expect(posee.ancre).toBeDefined();
    // La pastille du port, calculée, lui cède la place.
    const portApres = apres.find((e) => e.cle === "port-a") as Etiquette;
    expect(chevauchent(posee, portApres)).toBe(false);
  });

  it("elle suit son câble quand un équipement bouge", () => {
    const { schema, trunk, sw } = site();
    const cible: CibleEtiquette = { genre: "lien", id: trunk.id, cle: "vlan" };
    const trace = () => mettreEnPage(schema, physique, new Set()).liens.find((l) => l.lien.id === trunk.id)?.trace ?? [];
    placer(schema, cible, placeSurLeTrace(trace(), { x: 180, y: -40 }));
    // Le switch descend : le câble penche, l'étiquette reste au-dessus de lui, au même écart.
    sw.position = { x: 360, y: 200 };
    const posee = vlanDe(schema, trunk.id);
    const ici = placeSurLeTrace(trace(), { x: posee.x, y: posee.y });
    expect(ici.t).toBeCloseTo(poseDe(schema, cible)?.place?.t ?? -1, 2);
    expect(ici.y).toBeCloseTo(poseDe(schema, cible)?.place?.y ?? 0, 0);
    expect(posee.y).toBeGreaterThan(-40);
  });

  it("tournée seulement : sa place calculée, et son angle", () => {
    const { schema, trunk } = site();
    const cible: CibleEtiquette = { genre: "lien", id: trunk.id, cle: "vlan" };
    tourner(schema, cible, -30);
    const tournee = vlanDe(schema, trunk.id);
    expect(tournee.angle).toBe(-30);
    expect(tournee.ancre).toBeUndefined();
    expect(poseDe(schema, cible)).toEqual({ angle: -30 });
    expect(dessinerSchema(schema)).toContain("rotate(-30 ");
    // Ce qu'elle occupe tient compte de la rotation : plus haute que droite.
    const droite = boiteEtiquette({ ...tournee, angle: 0 }, 0);
    const penchee = boiteEtiquette(tournee, 0);
    expect(penchee.y1 - penchee.y0).toBeGreaterThan(droite.y1 - droite.y0);
  });

  it("« Remettre à sa place » oublie position et angle ; rien de vide ne reste dans le fichier", () => {
    const { schema, trunk } = site();
    const cible: CibleEtiquette = { genre: "lien", id: trunk.id, cle: "port-b" };
    placer(schema, cible, { t: 0.5, x: 0, y: 20 });
    tourner(schema, cible, 45);
    expect(poseDe(schema, cible)).toEqual({ place: { t: 0.5, x: 0, y: 20 }, angle: 45 });
    tourner(schema, cible, 0);
    expect(poseDe(schema, cible)).toEqual({ place: { t: 0.5, x: 0, y: 20 } });
    remettre(schema, cible);
    expect(poseDe(schema, cible)).toBeUndefined();
    expect(schema.liens.find((l) => l.id === trunk.id)?.poses).toBeUndefined();
    // Un angle de 0 sans place n'est rien.
    ecrirePose(schema, cible, { angle: 0 });
    expect(schema.liens.find((l) => l.id === trunk.id)?.poses).toBeUndefined();
    placer(schema, { genre: "lien", id: trunk.id, cle: "nom" }, { t: 0.2, x: 0, y: 0 });
    tourner(schema, { genre: "lien", id: trunk.id, cle: "vlan" }, 15);
    remettreToutes(schema, trunk.id);
    expect(schema.liens.find((l) => l.id === trunk.id)?.poses).toBeUndefined();
    // Une cible absente ne change rien.
    placer(schema, { genre: "lien", id: "l_absent", cle: "nom" }, { t: 0, x: 0, y: 0 });
    placer(schema, { genre: "equipement", id: "d_absent" }, { x: 1, y: 1 });
    expect(poseDe(schema, { genre: "relation", id: "r_absent", cle: "nom" })).toBeUndefined();
  });

  it("deux cibles sont les mêmes si elles désignent la même étiquette", () => {
    expect(memeCible({ genre: "lien", id: "l", cle: "nom" }, { genre: "lien", id: "l", cle: "nom" })).toBe(true);
    expect(memeCible({ genre: "lien", id: "l", cle: "nom" }, { genre: "lien", id: "l", cle: "vlan" })).toBe(false);
    expect(memeCible({ genre: "equipement", id: "d" }, { genre: "equipement", id: "d" })).toBe(true);
    expect(memeCible({ genre: "equipement", id: "d" }, null)).toBe(false);
    expect(memeCible(null, null)).toBe(false);
  });

  it("le nom d'une relation se place et se tourne comme celui d'un câble", () => {
    const { schema, coeur, sw } = site();
    const relation = relier(schema, { genre: "dependance", de: coeur.id, vers: sw.id });
    relation.nom = "Supervision";
    const logique = vue("logique");
    const page = () => mettreEnPage(schema, logique, new Set());
    const trace = page().relations.find((r) => r.relation.id === relation.id)?.trace ?? [];
    const cible: CibleEtiquette = { genre: "relation", id: relation.id, cle: "nom" };
    placer(schema, cible, placeSurLeTrace(trace, { x: 120, y: 80 }));
    tourner(schema, cible, 90);
    const nom = page().etiquettes.get(relation.id)?.[0] as Etiquette;
    expect(nom.x).toBeCloseTo(120, 0);
    expect(nom.y).toBeCloseTo(80, 0);
    expect(nom.angle).toBe(-90);
    const relue = lireSchema(ecrireSchema(schema)).schema.relations.find((r) => r.id === relation.id);
    expect(relue?.poses?.nom?.angle).toBe(-90);
  });
});

describe("le nom et l'adresse d'un équipement : un seul bloc", () => {
  it("décalé et tourné ensemble, dans le dessin, ses obstacles et son cadre", () => {
    const { schema, coeur } = site();
    const avant = dessinerSchema(schema);
    expect(transformeDuBloc(coeur, true)).toBe("");
    const cible: CibleEtiquette = { genre: "equipement", id: coeur.id };
    placer(schema, cible, { x: -120, y: -90 });
    tourner(schema, cible, -45);
    const { cx, cy } = blocDuNom(coeur, true);
    expect(transformeDuBloc(coeur, true)).toBe(`translate(-120 -90) rotate(-45 ${cx.toFixed(1)} ${cy.toFixed(1)})`);
    const dessin = dessinerSchema(schema);
    expect(dessin).toContain(`<g transform="translate(-120 -90) rotate(-45`);
    // Le cadre du dessin s'agrandit pour garder le nom posé loin, en haut à gauche.
    const cadre = (svg: string) => (svg.match(/viewBox="([^"]+)"/)?.[1] ?? "").split(" ").map(Number);
    expect(cadre(dessin)[0]).toBeLessThan(cadre(avant)[0] as number);
    expect(cadre(dessin)[1]).toBeLessThan(cadre(avant)[1] as number);
    // Les étiquettes calculées évitent le nom là où il est, pas là où il était.
    const occupe = occupationDe(coeur, true).at(-1);
    expect(occupe?.y1).toBeLessThan(cy);
    // Seulement décalé, sans angle.
    tourner(schema, cible, 0);
    expect(transformeDuBloc(coeur, true)).toBe("translate(-120 -90)");
    // Seulement tourné, à sa place.
    ecrirePose(schema, cible, { angle: 30 });
    expect(transformeDuBloc(coeur, true)).toBe(`rotate(30 ${cx.toFixed(1)} ${cy.toFixed(1)})`);
    remettre(schema, cible);
    expect(coeur.pose).toBeUndefined();
    expect(dessinerSchema(schema)).toBe(avant);
  });
});

describe("enregistrées avec le schéma", () => {
  it("les poses se relisent ; ce qui est faux ou vide est écarté", () => {
    const { schema, trunk, coeur } = site();
    placer(schema, { genre: "lien", id: trunk.id, cle: "vlan" }, { t: 0.4567, x: 3.21, y: -24.56 });
    tourner(schema, { genre: "lien", id: trunk.id, cle: "port-a" }, 30);
    placer(schema, { genre: "equipement", id: coeur.id }, { x: 12, y: -8 });
    const relu = lireSchema(ecrireSchema(schema)).schema;
    expect(relu.liens.find((l) => l.id === trunk.id)?.poses).toEqual({
      "port-a": { angle: 30 },
      vlan: { place: { t: 0.4567, x: 3.2, y: -24.6 } },
    });
    expect(relu.equipements.find((e) => e.id === coeur.id)?.pose).toEqual({ place: { x: 12, y: -8 } });

    const brut = JSON.parse(ecrireSchema(schema));
    const lien = brut.liens.find((l: { id: string }) => l.id === trunk.id);
    lien.poses = {
      vlan: { place: { x: 4, y: 4 } }, // sans fraction : pas de place sur un câble
      nom: { angle: 200 }, // ramené à 20°
      "port-b": { place: { t: 7, x: 1, y: 2 } }, // fraction bornée à 1
      inconnu: { angle: 10 },
      "port-a": "n'importe quoi",
    };
    brut.equipements[0].pose = { place: { t: 0.5, x: "a", y: 3 }, angle: 0 };
    const lu = lireSchema(brut).schema;
    expect(lu.liens.find((l) => l.id === trunk.id)?.poses).toEqual({
      nom: { angle: 20 },
      "port-b": { place: { t: 1, x: 1, y: 2 } },
    });
    expect(lu.equipements[0]?.pose).toBeUndefined();
  });

  it("un câble aux étiquettes placées a des réglages ; le mode simplifié les garde", () => {
    const { schema, trunk } = site();
    const lien = schema.liens.find((l) => l.id === trunk.id);
    if (!lien) throw new Error("câble absent");
    const avant = aDesReglages(schema, lien);
    tourner(schema, { genre: "lien", id: trunk.id, cle: "port-a" }, 15);
    expect(aDesReglages(schema, lien) || avant).toBe(true);
    tourner(schema, { genre: "lien", id: trunk.id, cle: "vlan" }, 15);
    expect(epurer(schema).liens.find((l) => l.id === trunk.id)?.poses?.vlan).toEqual({ angle: 15 });
    const simple = mettreEnPage(epurer(schema), vue("simplifiee"), new Set()).etiquettes.get(trunk.id) ?? [];
    expect(simple.find((e) => e.cle === "vlan")?.angle).toBe(15);
  });

  it("placerEtiquettes seul : une étiquette posée à la main garde son angle et son point d'accroche", () => {
    const trace = [
      { x: 0, y: 0 },
      { x: 200, y: 0 },
    ];
    const resultat = placerEtiquettes(
      [{ id: "l", trace, nom: "Uplink", poses: { nom: { place: { t: 0.5, x: 0, y: -20 }, angle: -15 } } }],
      [],
    );
    expect(resultat.get("l")).toEqual([
      expect.objectContaining({ texte: "Uplink", x: 100, y: -20, angle: -15, id: "l", cle: "nom", ancre: { x: 100, y: 0 } }),
    ]);
  });
});
