// Ce que Markdown ne sait pas écrire s'écrit en balises HTML, comme le font la plupart
// des outils : souligné, exposant, indice (<u>, <sup>, <sub>), couleur et taille
// (<span style="…">), alignement (<div align="…">).
//
// Le HTML brut reste désactivé (`html: false`, section 8.4) : seules ces écritures,
// exactement, sont reconnues ; toute autre reste du texte, jamais interprété. Une balise
// en ligne ouvrante n'est retenue que si sa fermante suit dans le même paragraphe. La
// lecture native (KyfRev.Rendering) applique les mêmes règles.

import type { MarkdownIt, StateBlock, StateInline } from "markdown-it";

const BALISE = /^<(\/?)(u|sup|sub)>/;

/** Une couleur à six chiffres ; une taille de 0,5 à 3 fois le texte, au centième. */
const SPAN = /^<span style="(color:#[0-9a-fA-F]{6}|font-size:(?:0\.[5-9][0-9]?|[12](?:\.[0-9]{1,2})?|3(?:\.0{1,2})?)em)">/;

const FERME_SPAN = "</span>";

/** La plus longue écriture reconnue tient dans cette longueur. */
const LONGUEUR_MAX = 40;

/** Nombre de balises ouvertes, par analyse d'un passage en ligne. */
const ouvertes = new WeakMap<StateInline, Record<string, number>>();

type Balise = { tout: string; ferme: boolean; nom: string; style?: string };

function lire(debut: string): Balise | null {
  const span = SPAN.exec(debut);
  if (span) return { tout: span[0], ferme: false, nom: "span", style: span[1] as string };
  if (debut.startsWith(FERME_SPAN)) return { tout: FERME_SPAN, ferme: true, nom: "span" };
  const balise = BALISE.exec(debut);
  return balise ? { tout: balise[0], ferme: balise[1] === "/", nom: balise[2] as string } : null;
}

function regle(etat: StateInline, silencieux: boolean): boolean {
  if (etat.src.charCodeAt(etat.pos) !== 0x3c /* < */) return false;
  const balise = lire(etat.src.slice(etat.pos, etat.pos + LONGUEUR_MAX));
  if (!balise) return false;
  const { tout, ferme, nom, style } = balise;
  const compte = ouvertes.get(etat) ?? {};
  if (ferme) {
    if (!compte[nom]) return false;
  } else if (!etat.src.includes(`</${nom}>`, etat.pos + tout.length)) {
    return false;
  }
  if (!silencieux) {
    const jeton = etat.push(`${nom}_${ferme ? "close" : "open"}`, nom, ferme ? -1 : 1);
    jeton.markup = tout;
    if (style) jeton.attrSet("style", style);
    compte[nom] = (compte[nom] ?? 0) + (ferme ? -1 : 1);
    ouvertes.set(etat, compte);
  }
  etat.pos += tout.length;
  return true;
}

const OUVRE_ALIGNEMENT = /^<div align="(center|right|justify)">[ \t]*$/;
const FERME_ALIGNEMENT = /^<\/div>[ \t]*$/;
const BARRIERE = /^(`{3,}|~{3,})/;

/**
 * Lignes que la recherche des fermetures peut lire, par analyse. Une fiche piégée ouvre des
 * passages sans fin : au-delà, ce n'est plus que du texte. La lecture native a la même limite.
 */
const BUDGET_ALIGNEMENT = 1_000_000;

/**
 * Un passage aligné : `<div align="center">` seul sur sa ligne, des blocs, `</div>` seul sur
 * sa ligne. Sans fermeture, ou collé sous un paragraphe, ce n'est que du texte.
 */
function regleAlignement(etat: StateBlock, debut: number, fin: number, silencieux: boolean): boolean {
  const texte = (ligne: number) =>
    etat.src.slice((etat.bMarks[ligne] ?? 0) + (etat.tShift[ligne] ?? 0), etat.eMarks[ligne]);
  const retrait = (ligne: number) => (etat.sCount[ligne] ?? 0) - etat.blkIndent;
  if (retrait(debut) >= 4) return false;
  const ouvre = OUVRE_ALIGNEMENT.exec(texte(debut));
  if (!ouvre) return false;

  let profondeur = 1;
  let barriere: string | null = null;
  let ligne = debut + 1;
  const env = etat.env as { budgetAlignement?: number };
  env.budgetAlignement ??= BUDGET_ALIGNEMENT;
  for (; ligne < fin; ligne++) {
    if (--env.budgetAlignement < 0) return false;
    const contenu = texte(ligne);
    if (retrait(ligne) < 0 && contenu.trim() !== "") return false;
    if (retrait(ligne) >= 4) continue;
    const signe = BARRIERE.exec(contenu)?.[1];
    if (barriere) {
      if (signe && signe[0] === barriere[0] && signe.length >= barriere.length && contenu.trim() === signe) {
        barriere = null;
      }
    } else if (signe) barriere = signe;
    else if (OUVRE_ALIGNEMENT.test(contenu)) profondeur++;
    else if (FERME_ALIGNEMENT.test(contenu) && --profondeur === 0) break;
  }
  if (ligne >= fin) return false;
  if (silencieux) return true;

  const ouverture = etat.push("alignement_open", "div", 1);
  ouverture.attrSet("align", ouvre[1] as string);
  ouverture.block = true;
  ouverture.map = [debut, ligne + 1];
  const limite = etat.lineMax;
  etat.lineMax = ligne;
  etat.md.block.tokenize(etat, debut + 1, ligne);
  etat.lineMax = limite;
  etat.push("alignement_close", "div", -1).block = true;
  etat.line = ligne + 1;
  return true;
}

/** Extension markdown-it : les balises ci-dessus, et rien d'autre. */
export function balises(md: MarkdownIt): void {
  md.inline.ruler.before("autolink", "balises", regle);
  md.block.ruler.before("fence", "alignement", regleAlignement);
}
