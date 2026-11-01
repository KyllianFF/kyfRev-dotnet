// Changer la casse de la sélection, comme dans Word (bouton « Casse » de
// l'onglet Mise en forme, Maj+F3, clic droit). Le texte garde sa longueur
// caractère pour caractère : un caractère qui changerait de longueur (« ß »
// → « SS ») reste tel quel, et la mise en forme ne bouge pas.

export type Casse = "phrase" | "minuscules" | "capitales" | "mots" | "inversee" | "alternee";

/** Les casses, dans l'ordre du menu, avec leur libellé écrit dans leur casse. */
export const CASSES: { casse: Casse; libelle: string }[] = [
  { casse: "phrase", libelle: "Majuscule en début de phrase" },
  { casse: "minuscules", libelle: "minuscules" },
  { casse: "capitales", libelle: "CAPITALES" },
  { casse: "mots", libelle: "Première Lettre Des Mots" },
  { casse: "inversee", libelle: "iNVERSER lA cASSE" },
  { casse: "alternee", libelle: "UnE lEtTrE sUr DeUx" },
];

/** Ordre de Maj+F3 (la casse alternée n'y est pas : elle se choisit au menu). */
const CYCLE: Casse[] = ["phrase", "minuscules", "capitales", "mots"];

const LETTRE = /\p{L}/u;
/** Dans un mot : lettre, chiffre, accent combiné ou apostrophe (« l'école », « aujourd'hui »). */
const DANS_UN_MOT = /[\p{L}\p{N}\p{M}'’]/u;
const FIN_DE_PHRASE = /[.!?…]/;
/** Après la ponctuation finale, avant l'espace : guillemets et parenthèses fermants. */
const FERMANT = /[»"”’')\]]/;

const majuscule = (c: string) => {
  const m = c.toUpperCase();
  return m.length === c.length ? m : c;
};
const minuscule = (c: string) => {
  const m = c.toLowerCase();
  return m.length === c.length ? m : c;
};

/**
 * `texte` dans la casse `casse`. Une nouvelle ligne (un autre paragraphe)
 * commence une phrase ; le début du texte aussi.
 */
export function changerCasse(texte: string, casse: Casse): string {
  const caracteres = [...texte];
  switch (casse) {
    case "minuscules":
      return caracteres.map(minuscule).join("");
    case "capitales":
      return caracteres.map(majuscule).join("");
    case "mots":
      return caracteres
        .map((c, i) => {
          if (!LETTRE.test(c)) return c;
          const precedent = caracteres[i - 1];
          return precedent !== undefined && DANS_UN_MOT.test(precedent) ? minuscule(c) : majuscule(c);
        })
        .join("");
    case "inversee":
      return caracteres.map((c) => (c === majuscule(c) ? minuscule(c) : majuscule(c))).join("");
    case "alternee": {
      let rang = 0;
      return caracteres
        .map((c) => (LETTRE.test(c) ? (rang++ % 2 === 0 ? majuscule(c) : minuscule(c)) : c))
        .join("");
    }
    case "phrase": {
      let debut = true;
      let finProbable = false;
      return caracteres
        .map((c) => {
          if (LETTRE.test(c)) {
            const resultat = debut ? majuscule(c) : minuscule(c);
            debut = false;
            finProbable = false;
            return resultat;
          }
          if (c === "\n") {
            debut = true;
          } else if (FIN_DE_PHRASE.test(c)) {
            finProbable = true;
          } else if (/\s/.test(c)) {
            if (finProbable) debut = true;
            finProbable = false;
          } else if (!(finProbable && FERMANT.test(c))) {
            // « 3.14 », « fichier.md » : pas de fin de phrase sans espace.
            finProbable = false;
          }
          return c;
        })
        .join("");
    }
  }
}

/**
 * Casse suivante pour Maj+F3 : celle que le texte a déjà (dans l'ordre du
 * cycle), puis la suivante ; « Majuscule en début de phrase » si le texte
 * n'en a aucune.
 */
export function casseSuivante(texte: string): Casse {
  const actuelle = CYCLE.findIndex((casse) => changerCasse(texte, casse) === texte);
  return actuelle < 0 ? "phrase" : (CYCLE[(actuelle + 1) % CYCLE.length] as Casse);
}
