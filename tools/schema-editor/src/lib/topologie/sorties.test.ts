import { describe, expect, it } from "vitest";
import { dessinerSchema } from "./dessin";
import { GABARITS } from "./gabarits";
import { schemaVide } from "./modele";
import { collerEquipements, creerEquipement } from "./operations";
import {
  couleursClaires,
  dimensionsSvg,
  dureeAnnonce,
  echellePng,
  estVide,
  FORMATS_EXPORT,
  nomDeFichier,
  orientationPage,
  svgAutonome,
} from "./sorties";
import { vue } from "./vues";

describe("sorties d'un schéma : nom du fichier", () => {
  it("garde le titre, accents compris, et ajoute l'extension", () => {
    expect(nomDeFichier("Réseau du siège", "png")).toBe("Réseau du siège.png");
    expect(nomDeFichier("Cœur v1.2", "json")).toBe("Cœur v1.2.json");
  });

  it("retire ou remplace ce que Windows interdit dans un nom de fichier", () => {
    expect(nomDeFichier('Site A/B : "cœur" <prod>?*', "svg")).toBe("Site A-B - 'cœur' prod.svg");
    expect(nomDeFichier("a\\b|c", "png")).toBe("a-b-c.png");
    expect(nomDeFichier("ligne\n\tsuivante\u0007 fin", "png")).toBe("ligne suivante fin.png");
  });

  it("n'a ni espace autour, ni point ou espace à la fin", () => {
    expect(nomDeFichier("  Agence  de   Lyon . . ", "png")).toBe("Agence de Lyon.png");
  });

  it("un titre vide, ou qui ne laisse rien, prend le nom par défaut", () => {
    expect(nomDeFichier("", "png")).toBe("Schéma réseau.png");
    expect(nomDeFichier(" ?*<> ... ", "json")).toBe("Schéma réseau.json");
  });

  it("un nom réservé par Windows est précédé du nom par défaut", () => {
    expect(nomDeFichier("CON", "png")).toBe("Schéma réseau CON.png");
    expect(nomDeFichier("lpt1", "svg")).toBe("Schéma réseau lpt1.svg");
    expect(nomDeFichier("Console", "png")).toBe("Console.png");
  });

  it("coupe à 80 caractères, sans couper un caractère en deux ni finir par un espace", () => {
    expect(nomDeFichier("x".repeat(200), "png")).toBe(`${"x".repeat(80)}.png`);
    expect(nomDeFichier(`${"é".repeat(79)} suite`, "png")).toBe(`${"é".repeat(79)}.png`);
    expect([...nomDeFichier("🖧".repeat(100), "png")]).toHaveLength(84);
  });
});

describe("sorties d'un schéma : image", () => {
  it("échelle PNG : deux fois la taille, moins si un côté dépasserait 8000 pixels", () => {
    expect(echellePng(800, 600)).toBe(2);
    expect(echellePng(4000, 1000)).toBe(2);
    expect(echellePng(5000, 1000)).toBe(1.6);
    expect(echellePng(1000, 16000)).toBe(0.5);
    // Une image sans taille ne divise pas par zéro.
    expect(echellePng(0, 0)).toBe(2);
  });

  it("couleurs claires : chaque variable du thème laisse sa valeur par défaut", () => {
    expect(couleursClaires('<text fill="var(--texte, #221d2e)" stroke="var(--graphique-3,#c27c0e)">a</text>')).toBe(
      '<text fill="#221d2e" stroke="#c27c0e">a</text>',
    );
    expect(couleursClaires('<rect fill="#ffffff"/>')).toBe('<rect fill="#ffffff"/>');
  });

  it("SVG autonome : aucune variable du thème, la légende, la taille du dessin", () => {
    const schema = schemaVide("Agence");
    const gabarit = GABARITS[0];
    if (!gabarit) throw new Error("aucun modèle de topologie");
    collerEquipements(schema, gabarit.construire(), { x: 0, y: 0 }, { suffixe: "", adresses: true });
    const svg = svgAutonome(schema);
    expect(svg).not.toContain("var(--");
    expect(svg.startsWith('<svg xmlns="http://www.w3.org/2000/svg"')).toBe(true);
    // La légende allonge le dessin : il est plus haut que sans elle.
    const sans = dimensionsSvg(dessinerSchema(schema));
    const avec = dimensionsSvg(svg);
    expect(avec.largeur).toBe(sans.largeur);
    expect(avec.hauteur).toBeGreaterThan(sans.hauteur);
    expect(svg).toContain(">Réseau<");
  });

  it("SVG autonome : la vue demandée remplace celle du schéma", () => {
    const schema = schemaVide("Agence");
    const gabarit = GABARITS[0];
    if (!gabarit) throw new Error("aucun modèle de topologie");
    collerEquipements(schema, gabarit.construire(), { x: 0, y: 0 }, { suffixe: "", adresses: true });
    expect(svgAutonome(schema, vue(schema.vue))).toBe(svgAutonome(schema));
    expect(svgAutonome(schema, vue("physique"))).toBe(
      couleursClaires(dessinerSchema(schema, { vue: vue("physique"), legende: true })),
    );
  });

  it("taille d'un SVG : les attributs de sa racine, pas ceux de son contenu", () => {
    expect(dimensionsSvg('<svg xmlns="x" viewBox="0 0 10 20" width="640" height="372.5"><rect width="5" height="6"/></svg>')).toEqual({
      largeur: 640,
      hauteur: 372.5,
    });
    expect(dimensionsSvg('<svg stroke-width="3"><rect width="5" height="6"/></svg>')).toEqual({ largeur: 0, hauteur: 0 });
    expect(dimensionsSvg("pas un dessin")).toEqual({ largeur: 0, hauteur: 0 });
  });
});

describe("sorties d'un schéma : impression, gardes et messages", () => {
  it("page en paysage si le dessin est plus large que haut, en portrait sinon", () => {
    expect(orientationPage(1200, 600)).toBe("landscape");
    expect(orientationPage(600, 1200)).toBe("portrait");
    expect(orientationPage(500, 500)).toBe("portrait");
  });

  it("un schéma sans équipement est vide, même annoté", () => {
    const schema = schemaVide("Vide");
    expect(estVide(schema)).toBe(true);
    schema.annotations.push({ id: "n_1", texte: "Note", position: { x: 0, y: 0 } });
    expect(estVide(schema)).toBe(true);
    creerEquipement(schema, "pc", { x: 0, y: 0 });
    expect(estVide(schema)).toBe(false);
  });

  it("trois formats : deux images, et le modèle qui s'exporte toujours", () => {
    expect(FORMATS_EXPORT.map((f) => f.valeur)).toEqual(["png", "svg", "json"]);
    expect(FORMATS_EXPORT.filter((f) => f.image).map((f) => f.valeur)).toEqual(["png", "svg"]);
  });

  it("un message de réussite reste 5 s, plus selon sa longueur, 12 s au plus", () => {
    expect(dureeAnnonce("")).toBe(5000);
    expect(dureeAnnonce("x".repeat(40))).toBe(7000);
    expect(dureeAnnonce("x".repeat(140))).toBe(12000);
    expect(dureeAnnonce("x".repeat(2000))).toBe(12000);
  });
});
