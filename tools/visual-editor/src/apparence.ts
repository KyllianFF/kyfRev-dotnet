// L'apparence d'un tableau : une seule ligne juste au-dessus de lui,
// « <!-- kyfrev tableau … --> », avec ce qui diffère de l'apparence par défaut.
// GitHub, Obsidian ou VS Code n'affichent pas ce commentaire ; le texte du tableau ne
// change pas, et un tableau sans cette ligne garde l'apparence qu'il a toujours eue.
// La lecture native (KyfRev.Rendering, TableLook) lit et écrit la même ligne.

import type { MarkdownIt, StateCore, Token } from "markdown-it";

export type Entete = "ligne" | "colonne" | "ligne,colonne" | "aucune";
export type Bandes = "aucune" | "lignes" | "colonnes";
export type Traits = "tous" | "horizontaux" | "verticaux" | "contour" | "entete" | "aucun";
export type Theme = "sobre" | "foret" | "ambre" | "ardoise" | "prune" | "encre";
export type Largeur = "ajustee" | "page";

export type Apparence = {
  entete: Entete;
  total: boolean;
  bandes: Bandes;
  traits: Traits;
  theme: Theme;
  /** La colonne cachée en révision, à partir de 1 ; rien si aucune ne l'est. */
  reviser: number | null;
  largeur: Largeur;
};

export const APPARENCE: Apparence = {
  entete: "ligne",
  total: false,
  bandes: "aucune",
  traits: "tous",
  theme: "sobre",
  reviser: null,
  largeur: "ajustee",
};

const BANDES: Bandes[] = ["aucune", "lignes", "colonnes"];
const TRAITS: Traits[] = ["tous", "horizontaux", "verticaux", "contour", "entete", "aucun"];
const THEMES: Theme[] = ["sobre", "foret", "ambre", "ardoise", "prune", "encre"];

const LIGNE = /^\s{0,3}<!--\s*kyfrev tableau((?:\s+[a-z,=0-9-]+)*)\s*-->\s*$/;

const parmi = <T extends string>(liste: T[], valeur: string, defaut: T): T =>
  (liste as string[]).includes(valeur) ? (valeur as T) : defaut;

/** L'apparence écrite dans une ligne de la fiche ; rien si la ligne n'en est pas une. */
export function lireApparence(ligne: string): Apparence | null {
  const trouve = LIGNE.exec(ligne);
  if (!trouve) return null;
  const apparence: Apparence = { ...APPARENCE };
  for (const mot of (trouve[1] ?? "").split(" ").filter((mot) => mot !== "")) {
    const egal = mot.indexOf("=");
    const [cle, valeur] = egal > 0 ? [mot.slice(0, egal), mot.slice(egal + 1)] : [mot, ""];
    switch (cle) {
      case "entete":
        apparence.entete =
          valeur === "colonne,ligne" ? "ligne,colonne" : parmi(["ligne", "colonne", "ligne,colonne", "aucune"], valeur, "ligne");
        break;
      case "total":
        apparence.total = true;
        break;
      case "bandes":
        apparence.bandes = parmi(BANDES, valeur, "aucune");
        break;
      case "traits":
        apparence.traits = parmi(TRAITS, valeur, "tous");
        break;
      case "theme":
        apparence.theme = parmi(THEMES, valeur, "sobre");
        break;
      case "reviser-cache": {
        const colonne = /^[0-9]+$/.test(valeur) ? Number(valeur) : 0;
        apparence.reviser = colonne > 0 ? colonne : null;
        break;
      }
      case "largeur":
        apparence.largeur = valeur === "page" ? "page" : "ajustee";
        break;
    }
  }
  return apparence;
}

/** La ligne qui écrit une apparence ; rien pour l'apparence par défaut. Même ordre que la lecture native. */
export function ecrireApparence(apparence: Apparence): string | null {
  const mots: string[] = [];
  if (apparence.entete !== "ligne") mots.push(`entete=${apparence.entete}`);
  if (apparence.total) mots.push("total");
  if (apparence.bandes !== "aucune") mots.push(`bandes=${apparence.bandes}`);
  if (apparence.traits !== "tous") mots.push(`traits=${apparence.traits}`);
  if (apparence.theme !== "sobre") mots.push(`theme=${apparence.theme}`);
  if (apparence.reviser !== null) mots.push(`reviser-cache=${apparence.reviser}`);
  if (apparence.largeur !== "ajustee") mots.push("largeur=page");
  return mots.length === 0 ? null : `<!-- kyfrev tableau ${mots.join(" ")} -->`;
}

/** Les attributs d'un tableau dans la page : la feuille de style en tire son apparence. */
export function attributsApparence(apparence: Apparence): Record<string, string> {
  const attributs: Record<string, string> = {
    "data-entete": apparence.entete,
    "data-bandes": apparence.bandes,
    "data-traits": apparence.traits,
    "data-theme": apparence.theme,
    "data-largeur": apparence.largeur,
  };
  if (apparence.total) attributs["data-total"] = "";
  if (apparence.reviser !== null) attributs["data-reviser"] = String(apparence.reviser);
  return attributs;
}

/** L'apparence d'un tableau collé ou relu dans la page. */
export function apparenceDuDom(tableau: HTMLElement): Apparence {
  const lu = (nom: string) => tableau.getAttribute(`data-${nom}`) ?? "";
  const reviser = Number(lu("reviser"));
  return {
    entete: parmi(["ligne", "colonne", "ligne,colonne", "aucune"], lu("entete"), "ligne"),
    total: tableau.hasAttribute("data-total"),
    bandes: parmi(BANDES, lu("bandes"), "aucune"),
    traits: parmi(TRAITS, lu("traits"), "tous"),
    theme: parmi(THEMES, lu("theme"), "sobre"),
    reviser: Number.isInteger(reviser) && reviser > 0 ? reviser : null,
    largeur: lu("largeur") === "page" ? "page" : "ajustee",
  };
}

/**
 * La ligne d'apparence juste au-dessus d'un tableau quitte le texte et va au tableau : son bloc commence à cette ligne,
 * pour qu'un tableau qu'on ne touche pas se réécrive à l'identique. Ailleurs, la même ligne reste un paragraphe.
 */
export function apparenceDesTableaux(md: MarkdownIt): void {
  md.core.ruler.after("block", "apparence_tableau", (etat: StateCore) => {
    const jetons = etat.tokens;
    for (let i = jetons.length - 4; i >= 0; i--) {
      const [ouvre, ligne, ferme, tableau] = jetons.slice(i, i + 4) as [Token, Token, Token, Token];
      if (
        ouvre.type !== "paragraph_open" ||
        ligne.type !== "inline" ||
        ferme.type !== "paragraph_close" ||
        tableau.type !== "table_open" ||
        tableau.level !== ouvre.level
      )
        continue;
      const apparence = lireApparence(ligne.content);
      if (!apparence) continue;
      tableau.meta = { ...(tableau.meta ?? {}), apparence };
      if (ouvre.map && tableau.map) tableau.map = [ouvre.map[0], tableau.map[1]];
      jetons.splice(i, 3);
    }
    return true;
  });
}
