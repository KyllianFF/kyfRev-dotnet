// Souligné, exposant et indice : Markdown n'a pas d'écriture pour eux, on
// utilise donc les balises HTML <u>, <sup> et <sub>, comme le font la plupart
// des outils.
//
// Le HTML brut reste désactivé (`html: false`, section 8.4) : seules ces trois
// balises, exactement, sans attribut, sont reconnues. Une balise ouvrante
// n'est retenue que si sa fermante suit dans le même paragraphe, sinon elle
// reste du texte.

import type { MarkdownIt, StateInline } from "markdown-it";

const BALISE = /^<(\/?)(u|sup|sub)>/;

/** Nombre de balises ouvertes, par analyse d'un passage en ligne. */
const ouvertes = new WeakMap<StateInline, Record<string, number>>();

function regle(etat: StateInline, silencieux: boolean): boolean {
  if (etat.src.charCodeAt(etat.pos) !== 0x3c /* < */) return false;
  const trouve = BALISE.exec(etat.src.slice(etat.pos, etat.pos + 6));
  if (!trouve) return false;
  const [tout, barre, nom] = trouve as unknown as [string, string, string];
  const compte = ouvertes.get(etat) ?? {};
  if (barre) {
    if (!compte[nom]) return false;
  } else if (!etat.src.includes(`</${nom}>`, etat.pos + tout.length)) {
    return false;
  }
  if (!silencieux) {
    const jeton = etat.push(`${nom}_${barre ? "close" : "open"}`, nom, barre ? -1 : 1);
    jeton.markup = tout;
    compte[nom] = (compte[nom] ?? 0) + (barre ? -1 : 1);
    ouvertes.set(etat, compte);
  }
  etat.pos += tout.length;
  return true;
}

/** Extension markdown-it : `<u>`, `<sup>` et `<sub>`, et rien d'autre. */
export function balises(md: MarkdownIt): void {
  md.inline.ruler.before("autolink", "balises", regle);
}
