// La syntaxe des fiches et ce que l'éditeur en affiche, repris du rendu de la 0.x
// (src/lib/rendu.ts). La lecture, elle, est native (KyfRev.Rendering) : ici, seulement
// ce qu'il faut pour écrire. Le HTML brut reste désactivé : tout HTML d'une fiche est du
// texte, jamais interprété, et les formules passent par DOMPurify.

import { katex } from "@mdit/plugin-katex";
import DOMPurify from "dompurify";
import MarkdownIt, { type MarkdownIt as Analyseur } from "markdown-it";
import footnote from "markdown-it-footnote";
import { apparenceDesTableaux } from "./apparence";
import { balises } from "./balises";
import { adresseRessource, decoder, estExterne, resoudre, separerSuffixe } from "./chemins";

/** Syntaxe reconnue dans les fiches : la même que celle de la lecture. */
export function analyseurMarkdown(): Analyseur {
  const md = new MarkdownIt({ html: false, linkify: true, typographer: false });
  md.use(footnote);
  md.use(katex, { output: "htmlAndMathml", throwOnError: false, trust: false, strict: "ignore" });
  md.use(balises);
  md.use(apparenceDesTableaux);
  return md;
}

/** Le SVG d'un dessin, tel qu'il peut entrer dans la page. */
export function svgNettoye(svg: string): string {
  return DOMPurify.sanitize(svg, { USE_PROFILES: { svg: true } });
}

/** Ce qu'affiche une image : son adresse, ou la raison pour laquelle elle est bloquée. */
export type AffichageImage = { adresse: string } | { bloquee: string; detail: string };

/** Images relatives servies par l'hôte ; tout le reste est bloqué, sauf `data:image`. */
export function affichageImage(source: string, dossier: string): AffichageImage {
  const [chemin] = separerSuffixe(source);
  if (!estExterne(chemin)) {
    const cible = resoudre(dossier, decoder(chemin));
    if (!cible) return { bloquee: "Image hors du coffre", detail: decoder(chemin) };
    return { adresse: adresseRessource(cible) };
  }
  // Images distantes bloquées par défaut : jamais chargées.
  if (/^https?:/i.test(source)) return { bloquee: "Image distante bloquée", detail: source };
  if (/^data:image\//i.test(source)) return { adresse: source };
  // « C:\…\image.png », « file:… » : seul le contenu du coffre s'affiche.
  return { bloquee: "Image hors du coffre (copiez-la dans la fiche)", detail: decoder(source) };
}

/**
 * Liens que markdown-it ne lirait pas : « ![[image.png]] », et les destinations à
 * espaces, hors du code. Comme la 0.x, pour que les fiches qu'elle a écrites se relisent.
 */
export function normaliserLiens(corps: string): string {
  let cloture: string | null = null;
  const transformer = (texte: string) =>
    texte
      .replace(/!\[\[([^\]|\n]+?)(?:\|[^\]\n]*)?\]\]/g, (_tout, cible: string) => {
        const nom = cible.trim();
        return `![${nom.replace(/\.[^./]+$/, "")}](<${nom}>)`;
      })
      .replace(
        /(!?\[[^\]\n]*\])\(([^()<>"'\n]*\s[^()<>"'\n]*)\)/g,
        (tout, lien: string, destination: string) => {
          const nette = destination.trim();
          return /\s/.test(nette) ? `${lien}(<${nette}>)` : tout;
        },
      );
  return corps
    .split("\n")
    .map((ligne) => {
      const barriere = /^\s{0,3}(`{3,}|~{3,})/.exec(ligne)?.[1]?.[0];
      if (barriere) {
        if (cloture === null) cloture = barriere;
        else if (barriere === cloture) cloture = null;
        return ligne;
      }
      if (cloture !== null) return ligne;
      // Les extraits de code en ligne (entre accents graves) restent tels quels.
      return ligne
        .split(/(`+[^`]*`+)/)
        .map((morceau, i) => (i % 2 === 1 ? morceau : transformer(morceau)))
        .join("");
    })
    .join("\n");
}

const NETTOYAGE = {
  FORBID_TAGS: ["style", "form", "input", "button", "iframe", "object", "embed"],
};

let rendeurFormules: Analyseur | null = null;

/** HTML sûr d'une formule. */
export function formuleHtml(tex: string, bloc: boolean): string {
  rendeurFormules ??= analyseurMarkdown();
  const html = bloc ? rendeurFormules.render(`$$\n${tex}\n$$`) : rendeurFormules.renderInline(`$${tex}$`);
  return DOMPurify.sanitize(html, NETTOYAGE);
}
