// Point d'entrée exécuté dans Microsoft Edge : le rendu et l'ancrage sont ceux des sources de
// kyfRev 0.x (src/lib/rendu.ts et src/lib/ancrage.ts), appelés comme le fait la lecture d'une
// fiche (src/composants/Fiche.svelte, `afficher` puis `appliquerAnnotations`).

import { ancreDe, ancrer, carteDu, ligneSourceDe, rogner, sectionDe } from "../../../kyfRev/src/lib/ancrage";
import type { Ancre } from "../../../kyfRev/src/lib/generes/Ancre";
import { rendre } from "../../../kyfRev/src/lib/rendu";

/** Passages demandés : relevés automatiquement sur les mots, ou par leur texte exact. */
type Demande = { auto?: boolean; exacts?: string[] };

/** Empreinte fictive : le corpus ne dépend pas du fichier. */
const EMPREINTE = "sha256:corpus";

/** Rend le corps comme la lecture d'une fiche, avant le dessin des diagrammes. */
function afficher(corps: string, titre: string): HTMLElement {
  const article = document.createElement("article");
  article.className = "contenu-fiche";
  article.innerHTML = rendre(corps, { dossier: "fiches/Corpus" });
  document.body.replaceChildren(article);
  const premier = article.firstElementChild;
  premier?.classList.toggle(
    "titre-repete",
    premier.tagName === "H1" && premier.textContent?.trim() === titre.trim(),
  );
  return article;
}

/** HTML du rendu, formules réduites à leur source (le rendu KaTeX n'entre pas dans le texte). */
function htmlDe(article: HTMLElement): string {
  const copie = article.cloneNode(true) as HTMLElement;
  for (const formule of copie.querySelectorAll("[data-tex]")) formule.replaceChildren();
  return copie.innerHTML;
}

/**
 * Sélections automatiques, reproductibles : huit groupes de 1 à 4 mots répartis dans le texte,
 * débordant parfois d'une espace (que `rogner` retire), une sélection au milieu d'un mot, et
 * tout le texte.
 */
function selectionsAutomatiques(texte: string): [number, number][] {
  const mots = [...texte.matchAll(/\S+/g)].map((m) => ({ debut: m.index, fin: m.index + m[0].length }));
  const selections: [number, number][] = [];
  const n = mots.length;
  for (let k = 0; k < 8 && n > 0; k++) {
    const i = Math.floor((k * n) / 8);
    const j = Math.min(n - 1, i + (k % 4));
    const premier = mots[i] as { debut: number; fin: number };
    const dernier = mots[j] as { debut: number; fin: number };
    selections.push([Math.max(0, premier.debut - (k % 2)), Math.min(texte.length, dernier.fin + (k % 3 === 0 ? 1 : 0))]);
  }
  // Une sélection ne coupe jamais un caractère en deux (paire UTF-16).
  const demi = (position: number) => /[\uD800-\uDFFF]/.test(texte.charAt(position));
  const long = mots.find((m) => m.fin - m.debut >= 4 && !demi(m.debut + 1) && !demi(m.fin - 2));
  if (long) selections.push([long.debut + 1, long.fin - 1]);
  selections.push([0, texte.length]);
  return selections;
}

function selectionsDe(texte: string, demande: Demande): [number, number][] {
  const selections = demande.auto ? selectionsAutomatiques(texte) : [];
  const absents = (demande.exacts ?? []).filter((exact) => !texte.includes(exact));
  if (absents.length > 0) throw new Error(`absents du texte affiché : ${JSON.stringify(absents)}`);
  for (const exact of demande.exacts ?? []) {
    const debut = texte.indexOf(exact);
    selections.push([debut, debut + exact.length]);
  }
  return selections;
}

function projeterArticle(article: HTMLElement, premiereLigneCorps: number, demande: Demande) {
  const carte = carteDu(article);
  const passages = selectionsDe(carte.texte, demande).map(([debut, fin]) => {
    const position = rogner(carte.texte, debut, fin);
    if (!position) return { selection: [debut, fin], ancre: null };
    const ancre = ancreDe(carte.texte, position, {
      section: sectionDe(carte, position.debut),
      ligneSource: ligneSourceDe(carte, position.debut, premiereLigneCorps),
      empreinteFiche: EMPREINTE,
    });
    return { selection: [debut, fin], ancre };
  });
  return {
    html: htmlDe(article),
    texte: carte.texte,
    morceaux: carte.morceaux.map((m) => ({ genre: m.genre, debut: m.debut, fin: m.fin })),
    passages,
  };
}

/** Markdown → texte affiché → passages (ancres). */
export function projeter(corps: string, titre: string, premiereLigneCorps: number, demande: Demande) {
  return projeterArticle(afficher(corps, titre), premiereLigneCorps, demande);
}

/** Ancre les passages dans le texte d'avant, puis les retrouve dans celui d'après. */
export function reancrer(avant: string, apres: string, titre: string, demande: Demande) {
  const depart = projeter(avant, titre, 1, demande);
  const texteApres = carteDu(afficher(apres, titre)).texte;
  return {
    texteAvant: depart.texte,
    texteApres,
    passages: depart.passages
      .filter((p): p is { selection: number[]; ancre: Ancre } => p.ancre !== null)
      .map(({ ancre }) => ({
        ancre,
        // « a_verifier » se comporte comme « ancree » : seul « introuvable » change le résultat.
        depuisAncree: ancrer(texteApres, ancre, "ancree"),
        depuisIntrouvable: ancrer(texteApres, ancre, "introuvable"),
      })),
  };
}
