// Un lecteur XML minimal, pour les fichiers draw.io (mxGraph) : des éléments, leurs attributs et leur texte.
// Il refuse les déclarations de type (pas d'entités à déplier) et tout ce qui est mal formé : un fichier qui
// ne se lit pas entièrement n'est pas importé. Il marche pareil dans l'îlot et dans les essais, sans navigateur.

export type ElementXml = {
  nom: string;
  attributs: Record<string, string>;
  enfants: ElementXml[];
  /** Le texte direct de l'élément (sans celui de ses enfants), entités décodées. */
  texte: string;
};

const ENTITES: Record<string, string> = { amp: "&", lt: "<", gt: ">", quot: '"', apos: "'" };
/** Au-delà, le fichier est refusé : un schéma n'a pas besoin d'autant d'éléments. */
const ELEMENTS_MAX = 400_000;
const NOM = /[A-Za-z_:][\w:.-]*/y;
const ATTRIBUT = /\s+([A-Za-z_:][\w:.-]*)\s*=\s*("([^"]*)"|'([^']*)')/y;

/** Décode les entités prédéfinies et numériques ; une entité inconnue est une erreur. */
export function decoderEntites(texte: string): string {
  return texte.replace(/&([^;&\s]*)(;?)/g, (_, entite: string, pointVirgule: string) => {
    if (!pointVirgule) throw new Error(`« & » seul (écrire « &amp; »)`);
    if (entite.startsWith("#x") || entite.startsWith("#X")) return caractere(Number.parseInt(entite.slice(2), 16));
    if (entite.startsWith("#")) return caractere(Number.parseInt(entite.slice(1), 10));
    const valeur = ENTITES[entite];
    if (valeur === undefined) throw new Error(`entité « &${entite}; » inconnue`);
    return valeur;
  });
}

function caractere(code: number): string {
  if (!Number.isInteger(code) || code < 0 || code > 0x10ffff) throw new Error("caractère numérique invalide");
  return String.fromCodePoint(code);
}

/**
 * Échappe un texte pour un attribut ou un contenu XML. Les sauts de ligne et tabulations sont écrits en
 * références numériques (un attribut les garde ainsi) ; les caractères qu'XML 1.0 interdit sont retirés.
 */
export function echapperXml(texte: string): string {
  return (
    texte
      // biome-ignore lint/suspicious/noControlCharactersInRegex: on retire justement ce qu'XML refuse.
      .replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f￾￿]/g, "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("\n", "&#10;")
      .replaceAll("\r", "&#13;")
      .replaceAll("\t", "&#9;")
  );
}

/** Lit un document XML ; lève une erreur qui dit où il est mal formé. */
export function lireXml(source: string): ElementXml {
  let i = source.charCodeAt(0) === 0xfeff ? 1 : 0;
  const pile: ElementXml[] = [];
  let racine: ElementXml | null = null;
  let elements = 0;
  const erreur = (quoi: string) => new Error(`XML mal formé (${quoi}, caractère ${i})`);
  const fin = (marque: string, quoi: string) => {
    const j = source.indexOf(marque, i);
    if (j < 0) throw erreur(`${quoi} sans fin`);
    return j;
  };

  while (i < source.length) {
    const ouvrant = source.indexOf("<", i);
    const texte = source.slice(i, ouvrant < 0 ? source.length : ouvrant);
    const courant = pile[pile.length - 1];
    if (courant) courant.texte += decoderEntites(texte);
    else if (texte.trim()) throw erreur("texte hors de l'élément racine");
    if (ouvrant < 0) break;
    i = ouvrant;

    if (source.startsWith("<!--", i)) {
      i = fin("-->", "commentaire") + 3;
    } else if (source.startsWith("<![CDATA[", i)) {
      const j = fin("]]>", "section CDATA");
      if (!courant) throw erreur("section CDATA hors de l'élément racine");
      courant.texte += source.slice(i + 9, j);
      i = j + 3;
    } else if (source.startsWith("<?", i)) {
      i = fin("?>", "instruction") + 2;
    } else if (source.startsWith("<!", i)) {
      throw erreur("déclaration de type refusée");
    } else if (source.startsWith("</", i)) {
      NOM.lastIndex = i + 2;
      const nom = NOM.exec(source)?.[0];
      const ferme = pile.pop();
      if (!nom || !ferme || ferme.nom !== nom) throw erreur(`balise fermante « ${nom ?? ""} » inattendue`);
      i = NOM.lastIndex;
      while (/\s/.test(source[i] ?? "")) i++;
      if (source[i] !== ">") throw erreur("balise fermante mal close");
      i++;
    } else {
      NOM.lastIndex = i + 1;
      const nom = NOM.exec(source)?.[0];
      if (!nom) throw erreur("nom d'élément attendu");
      if (++elements > ELEMENTS_MAX) throw erreur("trop d'éléments");
      const element: ElementXml = { nom, attributs: {}, enfants: [], texte: "" };
      i = NOM.lastIndex;
      for (;;) {
        ATTRIBUT.lastIndex = i;
        const attribut = ATTRIBUT.exec(source);
        if (!attribut) break;
        const cle = attribut[1] ?? "";
        if (cle in element.attributs) throw erreur(`attribut « ${cle} » en double`);
        const brut = attribut[3] ?? attribut[4] ?? "";
        if (brut.includes("<")) throw erreur("« < » dans un attribut");
        element.attributs[cle] = decoderEntites(brut.replace(/[\t\n\r]/g, " "));
        i = ATTRIBUT.lastIndex;
      }
      while (/\s/.test(source[i] ?? "")) i++;
      const vide = source.startsWith("/>", i);
      if (!vide && source[i] !== ">") throw erreur(`balise « ${nom} » mal close`);
      i += vide ? 2 : 1;
      if (courant) courant.enfants.push(element);
      else if (racine) throw erreur("plusieurs éléments racines");
      else racine = element;
      if (!vide) pile.push(element);
    }
  }
  if (pile.length > 0) throw erreur(`élément « ${pile[pile.length - 1]?.nom} » non fermé`);
  if (!racine) throw erreur("aucun élément");
  return racine;
}

/** Les enfants d'un élément qui portent ce nom. */
export const enfantsNommes = (element: ElementXml, nom: string): ElementXml[] =>
  element.enfants.filter((e) => e.nom === nom);
