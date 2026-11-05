import { describe, expect, it } from "vitest";
import { chercherTypes, definition, TYPES } from "./catalogue";
import {
  cadreAnnotation,
  descriptionDuSchema,
  dessinerAnnotation,
  dessinerSchema,
  ecartsParalleles,
  mettreEnPage,
  traceCoudee,
  traceDuLien,
  traceEntre,
} from "./dessin";
import { diagnostiquer } from "./diagnostic";
import { ecrireSchema, lireSchema } from "./format";
import { ILLUSTRES, emprise, illustration } from "./illustrations";
import { icone } from "./pictogrammes";
import { interfaceOccupee, type Schema, schemaVide } from "./modele";
import {
  aligner,
  appliquerProfil,
  auNombreDeMembres,
  changerProfil,
  collerEquipements,
  connecter,
  copierEquipements,
  creerEquipement,
  dupliquerEquipements,
  Refus,
  rebrancher,
  renommerInterface,
  repartir,
  retirerEquipements,
} from "./operations";
import { interfacesParMembre } from "./ports";
import { vue } from "./vues";

const stackDeDeuxSwitches = (schema: Schema) => {
  const sw = creerEquipement(schema, "switch", { x: 0, y: 0 });
  // 24 ports d'accès seulement, pour le scénario.
  sw.membres = [
    { numero: 1, groupes: [{ nom: "Accès", motif: "{m}/{p}", nombre: 24, premier: 1, genre: "cuivre" }] },
  ];
  const resultat = changerProfil(schema, sw.id, auNombreDeMembres(2));
  expect(resultat.applique).toBe(true);
  return sw;
};

describe("catalogue (D39)", () => {
  it("chaque type a sa catégorie, son pictogramme et des propriétés propres", () => {
    expect(TYPES.length).toBeGreaterThanOrEqual(45);
    expect(new Set(TYPES.map((t) => t.type)).size).toBe(TYPES.length);
    // Un NAS n'a pas les champs d'un switch.
    const champs = (type: string) => definition(type).champs.map((c) => c.cle);
    expect(champs("nas")).toContain("capaciteTo");
    expect(champs("switch")).not.toContain("capaciteTo");
    // Un type inconnu (schéma plus récent) se lit comme un équipement générique.
    expect(definition("inconnu").type).toBe("generique");
  });
});

describe("ports et stack (scénario 1)", () => {
  it("stack de deux switches de 24 ports : 1/1 à 1/24 puis 2/1 à 2/24, regroupés par membre", () => {
    const schema = schemaVide();
    const sw = stackDeDeuxSwitches(schema);
    const groupes = interfacesParMembre(sw);
    expect(groupes.map((g) => g.membre)).toEqual([1, 2]);
    expect(groupes[0]?.interfaces.map((i) => i.nom)).toEqual(
      Array.from({ length: 24 }, (_, k) => `1/${k + 1}`),
    );
    expect(groupes[1]?.interfaces.map((i) => i.nom)).toEqual(
      Array.from({ length: 24 }, (_, k) => `2/${k + 1}`),
    );
    // Membres de tailles différentes.
    const membre2 = sw.membres[1];
    if (membre2?.groupes?.[0]) membre2.groupes[0].nombre = 48;
    expect(changerProfil(schema, sw.id, () => {}).applique).toBe(true);
    expect(sw.interfaces.filter((i) => i.membre === 2)).toHaveLength(48);
  });
});

describe("connexions (scénarios 2, 3, 4)", () => {
  it("un PC sur un port libre : le port devient occupé, un second câble est refusé", () => {
    const schema = schemaVide();
    const sw = stackDeDeuxSwitches(schema);
    const pc = creerEquipement(schema, "pc", { x: 200, y: 0 });
    const port = sw.interfaces.find((i) => i.nom === "1/5");
    const carte = pc.interfaces.find((i) => i.genre === "cuivre");
    if (!port || !carte) throw new Error("ports absents");
    const lien = connecter(
      schema,
      { equipement: pc.id, interface: carte.id },
      { equipement: sw.id, interface: port.id },
    );
    expect(lien.support).toBe("cuivre");
    expect(interfaceOccupee(schema, sw.id, port.id)).toBe(true);
    const autre = creerEquipement(schema, "pc", { x: 400, y: 0 });
    const carte2 = autre.interfaces.find((i) => i.genre === "cuivre");
    if (!carte2) throw new Error("port absent");
    expect(() =>
      connecter(
        schema,
        { equipement: autre.id, interface: carte2.id },
        { equipement: sw.id, interface: port.id },
      ),
    ).toThrow(Refus);
    // Une interface radio ne prend pas de câble.
    const wifi = pc.interfaces.find((i) => i.genre === "radio");
    const libre = sw.interfaces.find((i) => i.nom === "1/6");
    if (!wifi || !libre) throw new Error("ports absents");
    expect(() =>
      connecter(
        schema,
        { equipement: pc.id, interface: wifi.id },
        { equipement: sw.id, interface: libre.id },
      ),
    ).toThrow(/association/);
  });

  it("renommer un équipement ou une interface ne casse pas ses liens", () => {
    const schema = schemaVide();
    const sw = stackDeDeuxSwitches(schema);
    const pc = creerEquipement(schema, "pc", { x: 200, y: 0 });
    const port = sw.interfaces[0];
    const carte = pc.interfaces[0];
    if (!port || !carte) throw new Error("ports absents");
    const lien = connecter(
      schema,
      { equipement: pc.id, interface: carte.id },
      { equipement: sw.id, interface: port.id },
    );
    sw.nom = "Cœur de réseau";
    renommerInterface(schema, { equipement: sw.id, interface: port.id }, "Gi1/0/1");
    expect(lireSchema(ecrireSchema(schema)).erreurs).toEqual([]);
    expect(schema.liens[0]?.b).toEqual({ equipement: sw.id, interface: port.id });
    expect(sw.interfaces[0]?.nom).toBe("Gi1/0/1");
    // Régénérer garde le nom donné à la main.
    changerProfil(schema, sw.id, () => {});
    expect(sw.interfaces[0]?.nom).toBe("Gi1/0/1");
    expect(schema.liens.find((l) => l.id === lien.id)).toBeDefined();
  });

  it("réduire le nombre de ports : les liens concernés sont rendus, puis traités un par un", () => {
    const schema = schemaVide();
    const sw = stackDeDeuxSwitches(schema);
    const pc1 = creerEquipement(schema, "pc", { x: 0, y: 200 });
    const pc2 = creerEquipement(schema, "pc", { x: 100, y: 200 });
    const p20 = sw.interfaces.find((i) => i.nom === "2/20");
    const p22 = sw.interfaces.find((i) => i.nom === "2/22");
    const p3 = sw.interfaces.find((i) => i.nom === "2/3");
    if (!p20 || !p22 || !p3) throw new Error("ports absents");
    const l1 = connecter(
      schema,
      { equipement: pc1.id, interface: pc1.interfaces[0]?.id ?? "" },
      { equipement: sw.id, interface: p20.id },
    );
    const l2 = connecter(
      schema,
      { equipement: pc2.id, interface: pc2.interfaces[0]?.id ?? "" },
      { equipement: sw.id, interface: p22.id },
    );
    const reduire = (e: (typeof schema.equipements)[number]) => {
      const groupe = e.membres[1]?.groupes?.[0];
      if (groupe) groupe.nombre = 16;
    };
    const essai = changerProfil(schema, sw.id, reduire);
    expect(essai.applique).toBe(false);
    if (essai.applique) return;
    expect(essai.retirees.map((i) => i.nom)).toEqual([
      "2/17",
      "2/18",
      "2/19",
      "2/20",
      "2/21",
      "2/22",
      "2/23",
      "2/24",
    ]);
    expect(essai.effets.liens.map((l) => l.id).sort()).toEqual([l1.id, l2.id].sort());
    // Rien n'a changé tant qu'aucune décision n'est prise.
    expect(sw.interfaces.filter((i) => i.membre === 2)).toHaveLength(24);
    expect(() => appliquerProfil(schema, sw.id, reduire, new Map())).toThrow(Refus);
    // Décisions explicites : l1 réaffecté au port 2/3, l2 retiré.
    appliquerProfil(
      schema,
      sw.id,
      reduire,
      new Map<string, { reaffecter: string } | "retirer">([
        [l1.id, { reaffecter: p3.id }],
        [l2.id, "retirer"],
      ]),
    );
    expect(sw.interfaces.filter((i) => i.membre === 2)).toHaveLength(16);
    expect(schema.liens.map((l) => l.id)).toEqual([l1.id]);
    expect(schema.liens[0]?.b.interface).toBe(p3.id);
  });

  it("supprimer un équipement retire ses liens ; la copie d'avant permet de tout rétablir (scénario 14)", () => {
    const schema = schemaVide();
    const sw = stackDeDeuxSwitches(schema);
    const pc = creerEquipement(schema, "pc", { x: 200, y: 0 });
    connecter(
      schema,
      { equipement: pc.id, interface: pc.interfaces[0]?.id ?? "" },
      { equipement: sw.id, interface: sw.interfaces[0]?.id ?? "" },
    );
    const avant = ecrireSchema(schema);
    retirerEquipements(schema, new Set([sw.id]));
    expect(schema.equipements).toHaveLength(1);
    expect(schema.liens).toHaveLength(0);
    const retabli = lireSchema(avant).schema;
    expect(retabli.equipements).toHaveLength(2);
    expect(retabli.liens).toHaveLength(1);
    expect(ecrireSchema(retabli)).toBe(avant);
  });

  it("dupliquer : nouveaux identifiants, liens internes recopiés, adresses non reprises", () => {
    const schema = schemaVide();
    const sw = stackDeDeuxSwitches(schema);
    const pc = creerEquipement(schema, "pc", { x: 200, y: 0 });
    connecter(
      schema,
      { equipement: pc.id, interface: pc.interfaces[0]?.id ?? "" },
      { equipement: sw.id, interface: sw.interfaces[0]?.id ?? "" },
    );
    sw.gestion = { adresse: "10.0.0.2" };
    const nouveaux = dupliquerEquipements(schema, new Set([sw.id, pc.id]));
    expect(nouveaux).toHaveLength(2);
    expect(nouveaux.some((id) => id === sw.id || id === pc.id)).toBe(false);
    expect(schema.liens).toHaveLength(2);
    expect(lireSchema(ecrireSchema(schema)).erreurs).toEqual([]);
    expect(schema.equipements.find((e) => e.id === nouveaux[0])?.gestion?.adresse).toBeUndefined();
  });
});

describe("câbles coudés, alignement, annotations (D44, D45, D46)", () => {
  it("tracé coudé : segments horizontaux et verticaux seulement, du bord d'un équipement à l'autre", () => {
    const droitOuPlat = (trace: { x: number; y: number }[]) =>
      trace.slice(1).every((p, i) => p.x === trace[i]?.x || p.y === trace[i]?.y);
    // Deux rangées : départ vertical (par le bas), arrivée par le haut.
    const rangees = traceCoudee({ x: 0, y: 0 }, { x: 200, y: 300 }, []);
    expect(droitOuPlat(rangees)).toBe(true);
    expect(rangees[0]).toEqual({ x: 0, y: 30 });
    expect(rangees.at(-1)).toEqual({ x: 200, y: 270 });
    // Même rangée : départ horizontal.
    const rangee = traceCoudee({ x: 0, y: 0 }, { x: 300, y: 20 }, []);
    expect(droitOuPlat(rangee)).toBe(true);
    expect(rangee[0]?.x).toBe(30);
    // Alignés : un seul segment, sans coude inutile.
    expect(traceCoudee({ x: 0, y: 0 }, { x: 0, y: 300 }, [])).toHaveLength(2);
    // Points de passage : en L entre chacun.
    expect(droitOuPlat(traceCoudee({ x: 0, y: 0 }, { x: 400, y: 400 }, [{ x: 200, y: 100 }]))).toBe(true);
    // Le tracé d'un câble : le sien, sinon celui du schéma.
    const schema = schemaVide();
    const sw = creerEquipement(schema, "switch", { x: 0, y: 0 });
    const pc = creerEquipement(schema, "pc", { x: 200, y: 300 });
    const lien = connecter(
      schema,
      { equipement: sw.id, interface: sw.interfaces[0]?.id ?? "" },
      { equipement: pc.id, interface: pc.interfaces[0]?.id ?? "" },
    );
    expect(traceDuLien(schema, lien, sw, pc)).toHaveLength(2);
    schema.cablesCoudes = true;
    expect(traceDuLien(schema, lien, sw, pc).length).toBeGreaterThan(2);
    lien.forme = "droite";
    expect(traceDuLien(schema, lien, sw, pc)).toHaveLength(2);
    const relu = lireSchema(ecrireSchema(schema)).schema;
    expect(relu.cablesCoudes).toBe(true);
    expect(relu.liens[0]?.forme).toBe("droite");
  });

  it("aligner et répartir : les verrouillés ne bougent pas ; trop peu d'équipements, refusé", () => {
    const schema = schemaVide();
    const a = creerEquipement(schema, "pc", { x: 0, y: 10 });
    const b = creerEquipement(schema, "pc", { x: 100, y: 50 });
    const c = creerEquipement(schema, "pc", { x: 400, y: 30 });
    const verrouille = creerEquipement(schema, "pc", { x: 900, y: 999 });
    verrouille.verrou = true;
    const tous = new Set([a.id, b.id, c.id, verrouille.id]);
    aligner(schema, tous, "haut");
    expect([a, b, c].map((e) => e.position.y)).toEqual([10, 10, 10]);
    expect(verrouille.position.y).toBe(999);
    aligner(schema, new Set([a.id, b.id]), "centre-h");
    expect([a.position.x, b.position.x]).toEqual([50, 50]);
    repartir(schema, new Set([a.id, b.id, c.id]), "x");
    expect([a, b, c].map((e) => e.position.x).sort((m, n) => m - n)).toEqual([50, 225, 400]);
    expect(() => aligner(schema, new Set([a.id]), "gauche")).toThrow(Refus);
    expect(() => repartir(schema, new Set([a.id, b.id]), "y")).toThrow(Refus);
  });

  it("annotations libres : chaque forme se relit sans perte et se dessine dans son cadre", () => {
    const schema = schemaVide();
    schema.annotations.push(
      { id: "n_1", texte: "Note", position: { x: 0, y: 0 } },
      {
        id: "n_2",
        texte: "Grand titre",
        forme: "texte",
        position: { x: 0, y: 100 },
        taille: 24,
        couleur: "#c0392b",
      },
      {
        id: "n_3",
        texte: "DMZ",
        forme: "rectangle",
        position: { x: 0, y: 200 },
        largeur: 300,
        hauteur: 180,
        couleur: "#1f62b0",
      },
      {
        id: "n_4",
        texte: "",
        forme: "ellipse",
        position: { x: 0, y: 400 },
        largeur: 200,
        hauteur: 100,
        couleur: "#167a5a",
      },
      {
        id: "n_5",
        texte: "flux",
        forme: "fleche",
        position: { x: 0, y: 600 },
        vers: { x: 200, y: 650 },
        couleur: "#a35a00",
      },
    );
    const relu = lireSchema(ecrireSchema(schema)).schema;
    expect(relu.annotations).toEqual(schema.annotations);
    for (const n of relu.annotations) {
      expect(dessinerAnnotation(n).length).toBeGreaterThan(20);
      const c = cadreAnnotation(n);
      expect(c.largeur).toBeGreaterThan(0);
      expect(c.hauteur).toBeGreaterThan(0);
    }
    expect(cadreAnnotation(relu.annotations[4] as (typeof relu.annotations)[number])).toEqual({
      x: -8,
      y: 592,
      largeur: 216,
      hauteur: 66,
    });
    // Une zone se dessine sous les câbles et équipements, dans l'export aussi.
    const svg = dessinerSchema(relu);
    expect(svg.indexOf("DMZ")).toBeLessThan(svg.indexOf("Grand titre"));
    // Un texte injecté reste du texte.
    schema.annotations[0] = {
      id: "n_1",
      texte: "<script>x</script>",
      forme: "texte",
      position: { x: 0, y: 0 },
    };
    expect(dessinerAnnotation(schema.annotations[0] as (typeof schema.annotations)[number])).not.toContain(
      "<script>",
    );
  });
});

describe("câbles coudés côte à côte (D52)", () => {
  type P = { x: number; y: number };
  const segments = (t: P[]) => t.slice(1).map((q, i) => [t[i] as P, q] as const);
  /** Longueur commune de deux segments horizontaux ou verticaux confondus (à 4 px près). */
  const commun = ([a, b]: readonly [P, P], [c, d]: readonly [P, P]) => {
    if (a.y === b.y && c.y === d.y && Math.abs(a.y - c.y) < 4)
      return (
        Math.min(Math.max(a.x, b.x), Math.max(c.x, d.x)) - Math.max(Math.min(a.x, b.x), Math.min(c.x, d.x))
      );
    if (a.x === b.x && c.x === d.x && Math.abs(a.x - c.x) < 4)
      return (
        Math.min(Math.max(a.y, b.y), Math.max(c.y, d.y)) - Math.max(Math.min(a.y, b.y), Math.min(c.y, d.y))
      );
    return 0;
  };
  const croisent = ([a, b]: readonly [P, P], [c, d]: readonly [P, P]) => {
    const cote = (p: P, q: P, r: P) => Math.sign((q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x));
    return cote(a, b, c) * cote(a, b, d) < 0 && cote(c, d, a) * cote(c, d, b) < 0;
  };
  const port = (e: { id: string; interfaces: { id: string; nom: string }[] }, nom: string) => ({
    equipement: e.id,
    interface: e.interfaces.find((i) => i.nom === nom)?.id ?? "",
  });

  it("deux câbles parallèles entre deux rangées : côte à côte, sans se croiser ni longer un autre câble", () => {
    const schema = schemaVide();
    schema.cablesCoudes = true;
    const cs = creerEquipement(schema, "switch", { x: 180, y: 100 });
    const fw = creerEquipement(schema, "pare-feu", { x: 1280, y: 100 });
    const sw = creerEquipement(schema, "switch", { x: 1280, y: 400 });
    const l1 = connecter(schema, port(cs, "1/10"), port(sw, "1/9"));
    const l2 = connecter(schema, port(cs, "1/11"), port(sw, "1/10"));
    const l3 = connecter(schema, port(fw, "port3"), port(sw, "1/1"));
    const page = mettreEnPage(schema, vue("physique"), new Set());
    const trace = (id: string) => page.liens.find((l) => l.lien.id === id)?.trace ?? [];
    const [t1, t2, t3] = [trace(l1.id), trace(l2.id), trace(l3.id)];
    for (const t of [t1, t2])
      expect(t.slice(1).every((p, i) => p.x === t[i]?.x || p.y === t[i]?.y)).toBe(true);
    for (const s of segments(t1))
      for (const o of segments(t2)) {
        expect(croisent(s, o)).toBe(false);
        expect(commun(s, o)).toBeLessThanOrEqual(0);
      }
    // Le câble du pare-feu arrive par le haut du switch : les deux autres ne le longent pas.
    for (const t of [t1, t2])
      for (const s of segments(t)) for (const o of segments(t3)) expect(commun(s, o)).toBeLessThanOrEqual(0);
  });

  it("schéma en arbre : un câble coudé ne longe pas le câble droit qui part du même côté", () => {
    const schema = schemaVide();
    schema.cablesCoudes = true;
    const sw = creerEquipement(schema, "switch", { x: 0, y: 0 });
    const pc = creerEquipement(schema, "pc", { x: 0, y: 150 });
    const imp = creerEquipement(schema, "imprimante", { x: 180, y: 150 });
    const droit = connecter(schema, port(sw, "1/1"), port(pc, "Ethernet"));
    const coude = connecter(schema, port(sw, "1/2"), port(imp, "Ethernet"));
    const page = mettreEnPage(schema, vue("physique"), new Set());
    const trace = (id: string) => page.liens.find((l) => l.lien.id === id)?.trace ?? [];
    for (const s of segments(trace(coude.id)))
      for (const o of segments(trace(droit.id))) expect(commun(s, o)).toBeLessThanOrEqual(0);
  });
});

describe("coulisser un câble (D53)", () => {
  it("palier coulissé : le Z passe à sa hauteur, se relit, suit un collage, et reste quand un équipement bouge", () => {
    const schema = schemaVide();
    schema.cablesCoudes = true;
    const cs = creerEquipement(schema, "switch", { x: 0, y: 0 });
    const sw = creerEquipement(schema, "switch", { x: 600, y: 260 });
    const lien = connecter(
      schema,
      { equipement: cs.id, interface: cs.interfaces[0]?.id ?? "" },
      { equipement: sw.id, interface: sw.interfaces[0]?.id ?? "" },
    );
    const milieu = () => mettreEnPage(schema, vue("physique"), new Set()).liens[0]?.trace[1]?.y;
    expect(milieu()).toBe(130);
    lien.palier = { axe: "y", position: 220 };
    expect(milieu()).toBe(220);
    sw.position = { x: 700, y: 300 };
    expect(milieu()).toBe(220);
    expect(lireSchema(ecrireSchema(schema)).schema.liens[0]?.palier).toEqual({ axe: "y", position: 220 });
    const autre = schemaVide();
    collerEquipements(autre, copierEquipements(schema, new Set([cs.id, sw.id])), { x: 40, y: 40 });
    expect(autre.liens[0]?.palier).toEqual({ axe: "y", position: 260 });
  });
});

describe("points de passage d'un câble coudé (D55)", () => {
  const surLeTrace = (trace: { x: number; y: number }[], c: { x: number; y: number }) =>
    trace.slice(1).some((q, i) => {
      const p = trace[i] as { x: number; y: number };
      const entre = (v: number, a: number, b: number) => v >= Math.min(a, b) && v <= Math.max(a, b);
      return (
        (p.x === q.x && c.x === p.x && entre(c.y, p.y, q.y)) ||
        (p.y === q.y && c.y === p.y && entre(c.x, p.x, q.x))
      );
    });

  it("chaque point de passage reste sur le tracé, même tiré en arrière", () => {
    const passages = [
      { x: 300, y: 0 },
      { x: 200, y: 600 },
    ];
    const trace = traceCoudee({ x: 0, y: 0 }, { x: 600, y: 500 }, passages);
    for (const c of passages) expect(surLeTrace(trace, c), JSON.stringify({ c, trace })).toBe(true);
    expect(trace.slice(1).every((p, i) => p.x === trace[i]?.x || p.y === trace[i]?.y)).toBe(true);
  });

  it("un segment décalé près d'un équipement : le petit côté contre l'équipement", () => {
    // Le câble descendait de l'équipement ; son premier segment, décalé de 20 vers la droite.
    const vertical = traceCoudee({ x: 0, y: 0 }, { x: 400, y: 400 }, [
      { x: 20, y: 300 },
      { x: 400, y: 300 },
    ]);
    // Le petit côté reste caché sous l'équipement : le tracé part de lui, déjà décalé.
    expect(vertical.slice(0, 2)).toEqual([
      { x: 20, y: 0 },
      { x: 20, y: 300 },
    ]);
    // Il partait sur le côté ; son premier segment, décalé de 20 vers le bas.
    const horizontal = traceCoudee({ x: 0, y: 0 }, { x: 400, y: 400 }, [
      { x: 300, y: 20 },
      { x: 300, y: 400 },
    ]);
    expect(horizontal.slice(0, 2)).toEqual([
      { x: 0, y: 20 },
      { x: 300, y: 20 },
    ]);
  });
});

describe("illustrations (D43)", () => {
  it("chaque type du catalogue a son illustration, dessinée sans transformation ni script", () => {
    const sansIllustration = TYPES.filter((t) => !ILLUSTRES.has(t.icone)).map((t) => t.type);
    expect(sansIllustration).toEqual([]);
    for (const t of TYPES) {
      const svg = illustration(t.icone, "#1f62b0");
      expect(svg.length).toBeGreaterThan(50);
      expect(svg).not.toMatch(/<script|on[a-z]+=|NaN|undefined/);
    }
  });
});

describe("cœur et distribution", () => {
  it("ils partagent une illustration propre, et se trouvent par leurs deux noms", () => {
    const coeur = definition("switch-coeur");
    const distribution = definition("switch-distribution");
    expect(coeur.icone).toBe(distribution.icone);
    expect(illustration(coeur.icone, "#1f62b0")).not.toBe(illustration(definition("switch").icone, "#1f62b0"));
    expect(emprise(coeur.icone)).toEqual([10, 12, 60, 55]);
    expect(icone(coeur.icone)).not.toBe(icone("box"));
    for (const mot of ["core", "cœur", "coeur"]) {
      expect(chercherTypes(mot).map((t) => t.type)).toContain("switch-coeur");
    }
    expect(chercherTypes("distribution").map((t) => t.type)).toEqual(["switch-distribution"]);
  });

  it("ils se posent en stack, avec des liens rapides : le cœur en 10 et 100 G, la distribution en 10 et 40 G", () => {
    const schema = schemaVide();
    const coeur = creerEquipement(schema, "switch-coeur", { x: 0, y: 0 });
    const distribution = creerEquipement(schema, "switch-distribution", { x: 0, y: 200 });

    expect(coeur.nom).toMatch(/^CORE/);
    expect(distribution.nom).toMatch(/^DIST/);
    expect(coeur.interfaces.map((i) => i.nom)).toContain("1/28");
    expect(coeur.interfaces.find((i) => i.nom === "1/28")?.vitesse).toBe(100000);
    expect(distribution.interfaces.find((i) => i.nom === "1/28")?.vitesse).toBe(40000);
    expect(definition("switch-coeur").empilable).toBe(true);
  });
});

describe("câble en main, copier-coller (D42)", () => {
  it("rebrancher un bout : il change de port ; port pris ou même interface refusés", () => {
    const schema = schemaVide();
    const sw = stackDeDeuxSwitches(schema);
    const pc = creerEquipement(schema, "pc", { x: 200, y: 0 });
    const carte = pc.interfaces.find((i) => i.genre === "cuivre");
    const p1 = sw.interfaces.find((i) => i.nom === "1/1");
    const p2 = sw.interfaces.find((i) => i.nom === "1/2");
    const p3 = sw.interfaces.find((i) => i.nom === "1/3");
    if (!carte || !p1 || !p2 || !p3) throw new Error("ports absents");
    const lien = connecter(
      schema,
      { equipement: pc.id, interface: carte.id },
      { equipement: sw.id, interface: p1.id },
    );
    const autre = creerEquipement(schema, "pc", { x: 400, y: 0 });
    const carte2 = autre.interfaces.find((i) => i.genre === "cuivre");
    if (!carte2) throw new Error("port absent");
    connecter(
      schema,
      { equipement: autre.id, interface: carte2.id },
      { equipement: sw.id, interface: p2.id },
    );

    rebrancher(schema, lien.id, "b", { equipement: sw.id, interface: p3.id });
    expect(lien.b.interface).toBe(p3.id);
    expect(interfaceOccupee(schema, sw.id, p1.id)).toBe(false);
    // Son propre port reste disponible pour lui-même.
    rebrancher(schema, lien.id, "b", { equipement: sw.id, interface: p3.id });
    expect(() => rebrancher(schema, lien.id, "b", { equipement: sw.id, interface: p2.id })).toThrow(Refus);
    expect(() => rebrancher(schema, lien.id, "b", { equipement: pc.id, interface: carte.id })).toThrow(Refus);
    expect(lien.b.interface).toBe(p3.id);
  });

  it("copier puis coller dans un autre schéma : copies indépendantes, câble interne et modèles suivent", () => {
    const source = schemaVide();
    const sw = stackDeDeuxSwitches(source);
    const pc = creerEquipement(source, "pc", { x: 200, y: 0 });
    connecter(
      source,
      { equipement: pc.id, interface: pc.interfaces[0]?.id ?? "" },
      { equipement: sw.id, interface: sw.interfaces[0]?.id ?? "" },
    );
    source.modeles.push({ id: "m_test", type: "switch", reference: "Modèle test", groupes: [] });
    sw.membres[0] = { ...(sw.membres[0] ?? { numero: 1 }), modele: "m_test" };
    const copie = copierEquipements(source, new Set([sw.id, pc.id]));
    // La copie ne bouge plus avec l'original.
    sw.nom = "Renommé après la copie";

    const destination = schemaVide();
    const colles = collerEquipements(destination, copie, { x: 100, y: 100 });
    expect(colles).toHaveLength(2);
    expect(destination.liens).toHaveLength(1);
    expect(destination.modeles.map((m) => m.id)).toEqual(["m_test"]);
    expect(destination.equipements.map((e) => e.nom)).not.toContain("Renommé après la copie");
    expect(destination.equipements.find((e) => e.type === "pc")?.position).toEqual({ x: 300, y: 100 });
    expect(lireSchema(ecrireSchema(destination)).erreurs).toEqual([]);
    // Coller deux fois : de nouveaux identifiants à chaque fois.
    collerEquipements(destination, copie);
    expect(new Set(destination.equipements.map((e) => e.id)).size).toBe(4);
    expect(destination.modeles).toHaveLength(1);
  });

  it("câbles parallèles entre deux mêmes équipements : écartés, et symétriques", () => {
    const schema = schemaVide();
    const sw = creerEquipement(schema, "switch", { x: 0, y: 0 });
    const routeur = creerEquipement(schema, "routeur", { x: 300, y: 0 });
    const lien1 = connecter(
      schema,
      { equipement: sw.id, interface: sw.interfaces[0]?.id ?? "" },
      { equipement: routeur.id, interface: routeur.interfaces[0]?.id ?? "" },
    );
    // Le second dans l'autre sens : l'écart ne doit pas les superposer pour autant.
    const lien2 = connecter(
      schema,
      { equipement: routeur.id, interface: routeur.interfaces[1]?.id ?? "" },
      { equipement: sw.id, interface: sw.interfaces[1]?.id ?? "" },
    );
    const ecarts = ecartsParalleles(schema.liens);
    expect(Math.abs(ecarts.get(lien1.id) ?? 0)).toBe(8);
    const trace1 = traceEntre(sw.position, routeur.position, [], ecarts.get(lien1.id));
    const trace2 = traceEntre(routeur.position, sw.position, [], ecarts.get(lien2.id));
    expect(Math.abs((trace1[0]?.y ?? 0) - (trace2.at(-1)?.y ?? 0))).toBe(16);
  });
});

describe("format, import et migration (scénarios 13, 15)", () => {
  it("aller-retour JSON sans perte", () => {
    const schema = schemaVide("Siège");
    schema.id = "s_01JA8Z3K5Q6WXYZABCDEFGHJKM";
    const sw = stackDeDeuxSwitches(schema);
    sw.gestion = { adresse: "10.0.0.2", vlan: 99 };
    sw.proprietes = { couche: "Niveau 3", poe: true };
    sw.tags = ["cœur"];
    const port = sw.interfaces[0];
    if (port) {
      port.macs = ["00:11:22:33:44:55"];
      port.adresses = [
        { adresse: "10.0.0.2", prefixe: 24 },
        { adresse: "fe80::1", prefixe: 64 },
      ];
      port.vlan = { mode: "trunk", natif: 1, tagues: "10,20,30-40" };
    }
    const pc = creerEquipement(schema, "pc", { x: 200, y: 0 });
    const lien = connecter(
      schema,
      { equipement: pc.id, interface: pc.interfaces[0]?.id ?? "" },
      { equipement: sw.id, interface: sw.interfaces[1]?.id ?? "" },
    );
    lien.longueurM = 3;
    lien.etiquette = "C-042";
    lien.mesure = { debit: 940, date: "2026-10-28", source: "iperf" };
    lien.points = [{ x: 100, y: 50 }];
    schema.vlans.push({
      id: "v_1",
      numero: 10,
      nom: "Utilisateurs",
      sousReseaux: ["10.10.0.0/24"],
      perimetre: "Siège",
    });
    schema.groupes.push({
      id: "g_1",
      nom: "Salle serveurs",
      genre: "salle",
      membres: [sw.id],
      position: { x: -100, y: -100 },
      taille: { largeur: 300, hauteur: 200 },
    });
    schema.annotations.push({ id: "n_1", texte: "À recâbler", position: { x: 0, y: 300 } });
    const texte = ecrireSchema(schema);
    const relu = lireSchema(texte);
    expect(relu.erreurs).toEqual([]);
    expect(relu.schema).toEqual(JSON.parse(texte));
    expect(ecrireSchema(relu.schema)).toBe(texte);
    expect(relu.schema.equipements[0]?.interfaces[0]?.vlan?.tagues).toBe("10,20,30-40");
  });

  it("import invalide : refusé avec un message, rien n'est retenu", () => {
    expect(() => lireSchema("{pas du json")).toThrow(/JSON illisible/);
    expect(() => lireSchema("[]")).toThrow();
    expect(() => lireSchema('{"format":99}')).toThrow(/plus récent/);
    expect(() => lireSchema('{"titre":"x"}')).toThrow(/format/);
    // Un lien vers une interface absente, et deux câbles sur une même interface.
    const schema = schemaVide();
    const sw = creerEquipement(schema, "switch", { x: 0, y: 0 });
    const a = creerEquipement(schema, "pc", { x: 0, y: 100 });
    const b = creerEquipement(schema, "pc", { x: 0, y: 200 });
    const brut = JSON.parse(JSON.stringify(schema));
    brut.liens = [
      {
        id: "l_1",
        a: { equipement: a.id, interface: a.interfaces[0]?.id },
        b: { equipement: sw.id, interface: sw.interfaces[0]?.id },
        support: "cuivre",
        statut: "actif",
        points: [],
      },
      {
        id: "l_2",
        a: { equipement: b.id, interface: b.interfaces[0]?.id },
        b: { equipement: sw.id, interface: sw.interfaces[0]?.id },
        support: "cuivre",
        statut: "actif",
        points: [],
      },
      {
        id: "l_3",
        a: { equipement: b.id, interface: "i_absente" },
        b: { equipement: sw.id, interface: sw.interfaces[1]?.id },
        support: "cuivre",
        statut: "actif",
        points: [],
      },
    ];
    const { erreurs } = lireSchema(brut);
    expect(erreurs.some((e) => e.includes("plusieurs câbles"))).toBe(true);
    expect(erreurs.some((e) => e.includes("extrémité absente"))).toBe(true);
  });

  it("textes injectés : bornés, sans caractères de contrôle, échappés au dessin", () => {
    const schema = schemaVide('<script>alert("x")</script>');
    const e = creerEquipement(schema, "routeur", { x: 0, y: 0 });
    e.nom = '<img src=x onerror="alert(1)">\u0007';
    const relu = lireSchema(ecrireSchema(schema)).schema;
    expect(relu.equipements[0]?.nom).not.toContain("\u0007");
    const svg = dessinerSchema(relu);
    expect(svg).not.toContain("<img");
    expect(svg).not.toContain("<script");
    expect(svg).toContain("&lt;img src=x onerror=&quot;alert(1)&quot;&gt;");
    expect(descriptionDuSchema(relu)).toContain("1 équipement");
  });

  it("migration du format 1 : équipements, ports notés, sans fil, VPN, zones et notes", () => {
    const ancien = {
      version: 1,
      titre: "Maison",
      equipements: [
        { id: "e1", type: "routeur", nom: "Box", detail: "Fibre", x: 0, y: 0 },
        { id: "e2", type: "switch", nom: "SW salon", x: 200, y: 0 },
        { id: "e3", type: "point-acces", nom: "AP", x: 200, y: 200 },
        { id: "e4", type: "ordinateur", nom: "PC", x: 400, y: 0 },
        { id: "e5", type: "mobile", nom: "Téléphone", x: 400, y: 200 },
        { id: "e6", type: "television", nom: "TV", x: 400, y: 300 },
      ],
      liens: [
        {
          id: "l1",
          de: "e1",
          vers: "e2",
          style: "filaire",
          portDe: "LAN1",
          portVers: "1/1",
          libelle: "uplink",
        },
        { id: "l2", de: "e2", vers: "e4", style: "fibre", portDe: "Gi0/9" },
        { id: "l3", de: "e5", vers: "e3", style: "sans-fil" },
        { id: "l4", de: "e1", vers: "e6", style: "vpn" },
      ],
      zones: [{ id: "z1", nom: "Salon", x: 150, y: -50, largeur: 300, hauteur: 120, couleur: "bleu" }],
      notes: [{ id: "n1", texte: "Câble à changer", x: 0, y: 300 }],
    };
    const { schema, erreurs, migre } = lireSchema(JSON.stringify(ancien));
    expect(migre).toBe(true);
    expect(erreurs).toEqual([]);
    expect(schema.titre).toBe("Maison");
    expect(schema.equipements.map((e) => e.nom)).toEqual(["Box", "SW salon", "AP", "PC", "Téléphone", "TV"]);
    expect(schema.equipements[0]?.description).toBe("Fibre");
    expect(schema.equipements[5]?.role).toBe("Télévision");
    // l1 : les ports notés existent dans les profils (LAN1 du routeur, 1/1 du switch).
    const [l1, l2] = schema.liens;
    const nomDe = (bout: { equipement: string; interface: string }) =>
      schema.equipements
        .find((e) => e.id === bout.equipement)
        ?.interfaces.find((i) => i.id === bout.interface)?.nom;
    expect(l1 && [nomDe(l1.a), nomDe(l1.b), l1.nom]).toEqual(["LAN1", "1/1", "uplink"]);
    // l2 : « Gi0/9 » n'existe pas : interface ajoutée à ce nom ; support fibre.
    expect(l2 && [nomDe(l2.a), l2.support]).toEqual(["Gi0/9", "fibre-multimode"]);
    expect(schema.relations.map((r) => r.genre).sort()).toEqual(["association-wifi", "tunnel"]);
    expect(schema.groupes[0]?.membres).toHaveLength(2);
    expect(schema.annotations[0]?.texte).toBe("Câble à changer");
  });
});

describe("diagnostic", () => {
  it("vitesses différentes, support incompatible, adresses en double, adresse de gestion manquante", () => {
    const schema = schemaVide();
    const sw = creerEquipement(schema, "switch", { x: 0, y: 0 });
    const srv = creerEquipement(schema, "serveur", { x: 200, y: 0 });
    const uplink = sw.interfaces.find((i) => i.role === "uplink");
    const nic = srv.interfaces[0];
    if (!uplink || !nic) throw new Error("ports absents");
    const lien = connecter(
      schema,
      { equipement: sw.id, interface: uplink.id },
      { equipement: srv.id, interface: nic.id },
    );
    lien.support = "fibre-multimode";
    sw.gestion = { adresse: "10.0.0.1" };
    nic.adresses = [{ adresse: "10.0.0.1", prefixe: 24 }];
    const constats = diagnostiquer(schema);
    const messages = constats.map((c) => `${c.niveau}: ${c.message}`);
    expect(messages.some((m) => m.startsWith("avertissement") && m.includes("vitesses différentes"))).toBe(
      true,
    );
    expect(messages.some((m) => m.includes("support fibre multimode sur l'interface cuivre"))).toBe(true);
    expect(messages.some((m) => m.includes("Adresse 10.0.0.1 utilisée par"))).toBe(true);
    expect(messages.some((m) => m.startsWith("manque") && m.includes(srv.nom))).toBe(true);
    expect(constats.some((c) => c.niveau === "erreur")).toBe(false);
  });
});
