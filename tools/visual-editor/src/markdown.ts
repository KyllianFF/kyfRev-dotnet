// Passage du Markdown d'une fiche au document de l'éditeur visuel, et retour.
//
// Fidélité avant tout :
// - un document qui n'a pas changé redonne exactement le texte d'origine ;
// - un bloc de premier niveau (paragraphe, liste, tableau…) que l'utilisateur
//   n'a pas touché est réécrit octet pour octet, avec les lignes vides qui
//   l'entourent : ProseMirror garde le même objet pour un nœud inchangé ;
// - seuls les blocs modifiés sont réécrits, dans une écriture Markdown
//   uniforme (« ** » pour le gras, « * » pour l'italique, « - » pour les
//   listes…). Le texte, lui, ne change jamais : tout caractère qui serait lu
//   comme une syntaxe est échappé ;
// - ce que l'éditeur ne sait pas présenter devient un « bloc Markdown »
//   modifiable tel quel (notes de bas de page, définitions de liens…).

import type { MarkdownIt, Token } from "markdown-it";
import {
  type Attrs,
  Fragment,
  Mark,
  type MarkType,
  type Node,
  type NodeType,
  Slice,
} from "prosemirror-model";
import { analyseurMarkdown, normaliserLiens } from "./analyse";
import { type Apparence, ecrireApparence } from "./apparence";
import { schema } from "./schema";

/** Ce que l'analyse retient du texte d'origine, pour le réécrire à l'identique. */
export type Origine = {
  texte: string;
  doc: Node;
  /** Bloc de premier niveau → son rang parmi les blocs d'origine. */
  rangs: Map<Node, number>;
  /** Texte exact de chaque bloc d'origine. */
  sources: string[];
  /** Texte entre un bloc et le suivant (lignes vides). */
  ecarts: string[];
  debut: string;
  fin: string;
};

let analyseur: MarkdownIt | null = null;

function md(): MarkdownIt {
  if (!analyseur) {
    analyseur = analyseurMarkdown();
    // Les adresses restent telles qu'écrites (pas de « %20 ») : l'éditeur les réécrit à l'identique.
    analyseur.normalizeLink = (adresse) => adresse;
  }
  return analyseur;
}

let analyseurCollage: MarkdownIt | null = null;

/**
 * Texte collé : lu comme une fiche, sauf le bloc de code par simple retrait
 * (quatre espaces ou une tabulation en début de ligne). Un texte copié d'un
 * PDF, d'un courriel ou d'un éditeur commence souvent ainsi : ce n'est pas
 * du code. Les blocs entre « ``` » restent du code.
 */
function mdCollage(): MarkdownIt {
  if (!analyseurCollage) {
    analyseurCollage = analyseurMarkdown();
    analyseurCollage.normalizeLink = (adresse) => adresse;
    analyseurCollage.disable("code");
  }
  return analyseurCollage;
}

/** Texte collé (Ctrl+V), lu comme du Markdown sans bloc de code par retrait : ce qu'il faut insérer. */
export function trancheCollee(texte: string): Slice {
  return Slice.maxOpen(depuisMarkdown(texte.replace(/\r\n?/g, "\n"), { collage: true }).doc.content);
}

/** Texte collé tel quel (Ctrl+Maj+V) : une ligne par paragraphe, aucune syntaxe lue. */
export function trancheBrute(texte: string): Slice {
  const lignes = texte.split(/(?:\r\n?|\n)+/);
  return Slice.maxOpen(
    Fragment.from(lignes.map((ligne) => n.paragraph.create(null, ligne ? schema.text(ligne) : null))),
  );
}

/** Un bloc que l'éditeur ne sait pas présenter : il reste en Markdown. */
class Inconvertible extends Error {}

const n = schema.nodes;
const m = schema.marks;

// --- Markdown → document -----------------------------------------------------

export function depuisMarkdown(texte: string, { collage = false } = {}): Origine {
  const lignes = texte.split("\n");
  const debuts: number[] = [];
  let decalage = 0;
  for (const ligne of lignes) {
    debuts.push(decalage);
    decalage += ligne.length + 1;
  }
  const vide = (ligne: number) => (lignes[ligne] ?? "").trim() === "";

  // Blocs de premier niveau, en lignes [debut, fin). Les lignes qu'aucun ne
  // couvre (notes, définitions de liens) deviennent des blocs Markdown.
  const jetons = (collage ? mdCollage() : md()).parse(normaliserLiens(texte), {});
  type Plage = { debut: number; fin: number; jetons?: Token[] };
  const plages: Plage[] = [];
  for (let i = 0; i < jetons.length; i++) {
    const jeton = jetons[i] as Token;
    if (jeton.level !== 0 || jeton.nesting === -1 || !jeton.map || jeton.type.startsWith("footnote"))
      continue;
    let fin = i;
    if (jeton.nesting === 1) {
      fin = i + 1;
      while (
        fin < jetons.length &&
        !((jetons[fin] as Token).level === 0 && (jetons[fin] as Token).nesting === -1)
      ) {
        fin++;
      }
    }
    plages.push({ debut: jeton.map[0], fin: jeton.map[1], jetons: jetons.slice(i, fin + 1) });
    i = fin;
  }
  plages.sort((a, b) => a.debut - b.debut);

  const blocs: Plage[] = [];
  let ligne = 0;
  const brut = (jusqua: number) => {
    let debut = ligne;
    let fin = jusqua;
    while (debut < fin && vide(debut)) debut++;
    while (fin > debut && vide(fin - 1)) fin--;
    if (fin > debut) blocs.push({ debut, fin });
  };
  for (const plage of plages) {
    let fin = plage.fin;
    while (fin > plage.debut + 1 && vide(fin - 1)) fin--;
    if (plage.debut < ligne) {
      // Chevauchement inattendu : les deux blocs restent en Markdown.
      const precedent = blocs.pop();
      if (precedent) ligne = precedent.debut;
      blocs.push({ debut: ligne, fin: Math.max(fin, precedent?.fin ?? fin) });
      ligne = blocs[blocs.length - 1]?.fin ?? fin;
      continue;
    }
    brut(plage.debut);
    blocs.push({ ...plage, fin });
    ligne = fin;
  }
  brut(lignes.length);

  const bornes = blocs.map((bloc) => {
    const derniere = bloc.fin - 1;
    return [debuts[bloc.debut] ?? 0, (debuts[derniere] ?? 0) + (lignes[derniere] ?? "").length] as const;
  });
  const sources = bornes.map(([a, b]) => texte.slice(a, b));
  const noeuds = blocs.map((bloc, i) => {
    if (bloc.jetons) {
      try {
        return convertir(bloc.jetons);
      } catch (erreur) {
        if (!(erreur instanceof Inconvertible || erreur instanceof RangeError)) throw erreur;
      }
    }
    return n.brut.create(null, sources[i] ? schema.text(sources[i]) : null);
  });

  const doc = n.doc.create(null, noeuds.length > 0 ? noeuds : [n.paragraph.create()]);
  const premier = bornes[0];
  const dernier = bornes[bornes.length - 1];
  return {
    texte,
    doc,
    rangs: new Map(noeuds.map((noeud, i) => [noeud, i])),
    sources,
    ecarts: bornes.slice(0, -1).map(([, fin], i) => texte.slice(fin, bornes[i + 1]?.[0])),
    debut: premier ? texte.slice(0, premier[0]) : texte,
    fin: dernier ? texte.slice(dernier[1]) : "",
  };
}

type Cadre = { type: NodeType; attrs: Attrs | null; contenu: Node[] };

/** Jetons markdown-it d'un bloc de premier niveau → un nœud. */
function convertir(jetons: Token[]): Node {
  const pile: Cadre[] = [{ type: n.doc, attrs: null, contenu: [] }];
  const haut = () => pile[pile.length - 1] as Cadre;
  const ouvrir = (type: NodeType, attrs: Attrs | null = null) => pile.push({ type, attrs, contenu: [] });
  const fermer = () => {
    const cadre = pile.pop() as Cadre;
    const noeud =
      cadre.type === n.list_item || cadre.type === n.alignement
        ? cadre.type.createAndFill(cadre.attrs, cadre.contenu)
        : cadre.type.createChecked(cadre.attrs, cadre.contenu);
    if (!noeud) throw new Inconvertible(cadre.type.name);
    haut().contenu.push(noeud);
  };
  const ajouter = (noeud: Node) => haut().contenu.push(noeud);
  const texteDe = (contenu: string) => (contenu ? schema.text(contenu) : null);
  const alignementDe = (jeton: Token) =>
    /text-align:\s*(left|center|right)/.exec(String(jeton.attrGet("style") ?? ""))?.[1] ?? null;

  for (const [rang, jeton] of jetons.entries()) {
    switch (jeton.type) {
      case "alignement_open":
        ouvrir(n.alignement, { align: attribut(jeton, "align") ?? "center" });
        break;
      case "paragraph_open":
        if (jeton.hidden) {
          // Paragraphe masqué : la liste qui le contient est « serrée ».
          const liste = [...pile]
            .reverse()
            .find((c) => c.type === n.bullet_list || c.type === n.ordered_list);
          if (liste) liste.attrs = { ...liste.attrs, tight: true };
        }
        ouvrir(n.paragraph);
        break;
      case "heading_open":
        ouvrir(n.heading, { level: Number(jeton.tag.slice(1)) });
        break;
      case "blockquote_open":
        ouvrir(n.blockquote);
        break;
      case "bullet_list_open":
        ouvrir(n.bullet_list, { bullet: jeton.markup || "-", tight: false });
        break;
      case "ordered_list_open":
        ouvrir(n.ordered_list, {
          order: Number(attribut(jeton, "start") ?? 1),
          delim: jeton.markup || ".",
          tight: false,
        });
        break;
      case "list_item_open":
        ouvrir(n.list_item, { tache: tacheDe(jetons, rang) });
        break;
      case "table_open":
        ouvrir(n.table, (jeton.meta?.apparence as Apparence | undefined) ?? null);
        break;
      case "tr_open":
        ouvrir(n.table_row);
        break;
      case "th_open":
        ouvrir(n.table_header, { align: alignementDe(jeton) });
        break;
      case "td_open":
        ouvrir(n.table_cell, { align: alignementDe(jeton) });
        break;
      case "thead_open":
      case "thead_close":
      case "tbody_open":
      case "tbody_close":
        break;
      case "alignement_close":
      case "paragraph_close":
      case "heading_close":
      case "blockquote_close":
      case "bullet_list_close":
      case "ordered_list_close":
      case "list_item_close":
      case "table_close":
      case "tr_close":
      case "th_close":
      case "td_close":
        fermer();
        break;
      case "inline":
        haut().contenu.push(...enLigne(jeton.children ?? []));
        break;
      case "fence":
      case "code_block":
        ajouter(
          n.code_block.create(
            { language: jeton.info.trim(), fence: jeton.type === "fence" ? jeton.markup : "```" },
            texteDe(jeton.content.replace(/\n$/, "")),
          ),
        );
        break;
      case "hr":
        ajouter(n.horizontal_rule.create({ markup: jeton.markup || "---" }));
        break;
      case "math_block":
        ajouter(n.math_block.create({ tex: jeton.content.trim() }));
        break;
      default:
        throw new Inconvertible(jeton.type);
    }
  }
  const [noeud] = (pile[0] as Cadre).contenu;
  if (pile.length !== 1 || !noeud) throw new Inconvertible("bloc incomplet");
  return noeud;
}

/**
 * Case d'une tâche au début d'un point de liste (« - [ ] », « - [x] ») : retirée du texte.
 * Se lit sur la source : « \\[ \\] » écrit des crochets, pas une case.
 */
function tacheDe(jetons: Token[], rang: number): boolean | null {
  const contenu = jetons[rang + 1]?.type === "paragraph_open" ? jetons[rang + 2] : undefined;
  const premier = contenu?.type === "inline" ? contenu.children?.[0] : undefined;
  if (!contenu || premier?.type !== "text") return null;
  const trouve = /^\[([ xX])\](?: |$)/.exec(contenu.content);
  if (!trouve) return null;
  premier.content = premier.content.slice(trouve[0].length);
  return trouve[1] !== " ";
}

/** Attribut d'un jeton, en texte ; `null` s'il est absent. */
function attribut(jeton: Token, nom: string): string | null {
  const valeur = jeton.attrGet(nom);
  return valeur === null ? null : String(valeur);
}

/** Jetons en ligne → texte et nœuds en ligne, avec leurs marques. */
function enLigne(jetons: Token[]): Node[] {
  const resultat: Node[] = [];
  let marques: readonly Mark[] = Mark.none;
  const ecrire = (texte: string, avec = marques) => {
    if (texte) resultat.push(schema.text(texte, avec));
  };
  const ouvrir = (marque: Mark) => {
    marques = marque.addToSet(marques);
  };
  const fermer = (type: MarkType) => {
    marques = type.removeFromSet(marques);
  };
  /** Les couleurs et les tailles ouvertes : une balise fermée rend celle qu'elle cachait. */
  const spans: Mark[] = [];
  const appliquerSpans = () => {
    marques = m.couleur.removeFromSet(m.taille.removeFromSet(marques));
    for (const marque of spans) marques = marque.addToSet(marques);
  };
  const paires: Record<string, MarkType> = {
    strong: m.strong,
    em: m.em,
    s: m.strike,
    u: m.underline,
    sup: m.sup,
    sub: m.sub,
  };

  for (const jeton of jetons) {
    const [, nom, sens] = /^(\w+?)_(open|close)$/.exec(jeton.type) ?? [];
    const paire = nom ? paires[nom] : undefined;
    if (paire) {
      if (sens === "open") ouvrir(paire.create());
      else fermer(paire);
      continue;
    }
    switch (jeton.type) {
      case "span_open": {
        const [propriete, valeur] = (attribut(jeton, "style") ?? "").split(":") as [string, string];
        spans.push((propriete === "color" ? m.couleur : m.taille).create({ valeur }));
        appliquerSpans();
        break;
      }
      case "span_close":
        spans.pop();
        appliquerSpans();
        break;
      case "text":
        ecrire(jeton.content);
        break;
      case "softbreak":
        ecrire("\n");
        break;
      case "hardbreak":
        resultat.push(n.hard_break.create(null, null, marques));
        break;
      case "link_open":
        ouvrir(
          m.link.create({
            href: attribut(jeton, "href") ?? "",
            title: attribut(jeton, "title"),
            auto: jeton.markup === "autolink" || jeton.markup === "linkify" ? jeton.markup : null,
          }),
        );
        break;
      case "link_close":
        fermer(m.link);
        break;
      case "code_inline":
        ecrire(jeton.content, m.code.create().addToSet(marques));
        break;
      case "image":
        resultat.push(
          n.image.create(
            {
              src: attribut(jeton, "src") ?? "",
              alt: (jeton.children ?? [])
                .map((enfant) => (enfant.type === "softbreak" ? " " : enfant.content))
                .join(""),
              title: attribut(jeton, "title"),
            },
            null,
            marques,
          ),
        );
        break;
      case "math_inline":
        resultat.push(n.math_inline.create({ tex: jeton.content }, null, marques));
        break;
      case "footnote_ref": {
        // « ^[note en ligne] » n'a pas d'étiquette : le paragraphe reste en Markdown.
        const etiquette = (jeton.meta as { label?: string } | null)?.label;
        if (etiquette === undefined) throw new Inconvertible("note en ligne");
        resultat.push(n.note.create({ label: etiquette }, null, marques));
        break;
      }
      default:
        throw new Inconvertible(jeton.type);
    }
  }
  return resultat;
}

// --- Document → Markdown -----------------------------------------------------

/** Markdown du document : les blocs d'origine inchangés sont repris tels quels. */
export function versMarkdown(doc: Node, origine: Origine | null): string {
  if (origine && doc.eq(origine.doc)) return origine.texte;
  const morceaux: string[] = [];
  let precedent: number | undefined;
  /** Dernier bloc d'origine retrouvé, même séparé du bloc courant par des blocs nouveaux. */
  let dernierRetrouve = -1;
  let premier = true;
  doc.forEach((noeud) => {
    let rang = origine?.rangs.get(noeud);
    // ProseMirror recrée parfois un bloc à l'identique (Entrée au début ou à la fin
    // d'un paragraphe le coupe en deux) : la copie est encore le bloc d'origine.
    if (rang === undefined && origine?.doc.maybeChild(dernierRetrouve + 1)?.eq(noeud))
      rang = dernierRetrouve + 1;
    if (rang !== undefined) dernierRetrouve = rang;
    const texte = rang !== undefined && origine ? (origine.sources[rang] ?? "") : bloc(noeud);
    if (rang === undefined && texte.trim() === "") return;
    if (premier) morceaux.push(rang === 0 && origine ? origine.debut : "");
    else if (origine && rang !== undefined && precedent !== undefined && rang === precedent + 1) {
      morceaux.push(origine.ecarts[precedent] ?? "\n\n");
    } else morceaux.push("\n\n");
    morceaux.push(texte);
    premier = false;
    precedent = rang;
  });
  if (premier) return "";
  const dernier = origine ? origine.sources.length - 1 : -1;
  morceaux.push(origine && precedent !== undefined && precedent === dernier ? origine.fin : "\n");
  return morceaux.join("");
}

const estListe = (noeud: Node) => noeud.type === n.bullet_list || noeud.type === n.ordered_list;

/** Blocs enfants, séparés par une ligne vide (pas avant une sous-liste dans une liste serrée). */
function blocs(parent: Node, serre = false): string {
  const parties: string[] = [];
  parent.forEach((enfant) => {
    const texte = bloc(enfant);
    if (texte === "" && parent.childCount > 1 && (enfant.type === n.paragraph || enfant.type === n.alignement))
      return;
    if (parties.length > 0) parties.push(serre && estListe(enfant) ? "\n" : "\n\n");
    parties.push(texte);
  });
  return parties.join("");
}

function bloc(noeud: Node): string {
  switch (noeud.type) {
    case n.paragraph:
      return enLigneMd(noeud);
    case n.alignement: {
      // Resté vide, il ne s'écrit pas.
      const contenu = blocs(noeud);
      return contenu.trim() === "" ? "" : `<div align="${noeud.attrs.align}">\n\n${contenu}\n\n</div>`;
    }
    case n.heading: {
      const contenu = enLigneMd(noeud, { uneLigne: true }).replace(/(\s)(#+)$/, "$1\\$2");
      return `${"#".repeat(noeud.attrs.level as number)}${contenu ? ` ${contenu}` : ""}`;
    }
    case n.blockquote:
      return blocs(noeud)
        .split("\n")
        .map((ligne) => (ligne === "" ? ">" : `> ${ligne}`))
        .join("\n");
    case n.horizontal_rule:
      return (noeud.attrs.markup as string) || "---";
    case n.code_block: {
      const contenu = noeud.textContent;
      const signe = ((noeud.attrs.fence as string) || "```")[0] === "~" ? "~" : "`";
      const plusLongue = Math.max(
        0,
        ...[...contenu.matchAll(signe === "~" ? /~+/g : /`+/g)].map((r) => r[0].length),
      );
      const barriere = signe.repeat(Math.max(3, plusLongue + 1));
      return `${barriere}${noeud.attrs.language}\n${contenu ? `${contenu}\n` : ""}${barriere}`;
    }
    case n.math_block:
      return `$$\n${noeud.attrs.tex}\n$$`;
    case n.bullet_list:
      return liste(noeud, () => `${noeud.attrs.bullet || "-"} `);
    case n.ordered_list:
      return liste(noeud, (i) => `${(noeud.attrs.order as number) + i}${noeud.attrs.delim || "."} `);
    case n.table:
      return tableau(noeud);
    default:
      // Bloc Markdown (et tout ce qui n'a pas d'écriture propre) : tel quel.
      return noeud.textContent;
  }
}

function liste(noeud: Node, marqueur: (rang: number) => string): string {
  const serre = noeud.attrs.tight === true;
  const elements: string[] = [];
  noeud.forEach((element, _, rang) => {
    const debut = marqueur(rang);
    const retrait = " ".repeat(debut.length);
    const lignes = blocs(element, serre).split("\n");
    const tache = element.attrs.tache as boolean | null;
    const premiere = debut + (tache === null ? "" : tache ? "[x] " : "[ ] ");
    elements.push(
      lignes
        .map((ligne, i) => {
          if (i === 0) return ligne === "" ? premiere.trimEnd() : premiere + ligne;
          return ligne === "" ? "" : retrait + ligne;
        })
        .join("\n"),
    );
  });
  return elements.join(serre ? "\n" : "\n\n");
}

function tableau(noeud: Node): string {
  const lignes: string[][] = [];
  const alignements: (string | null)[] = [];
  noeud.forEach((rangee, _, r) => {
    const cellules: string[] = [];
    rangee.forEach((cellule) => {
      cellules.push(enLigneMd(cellule, { uneLigne: true, cellule: true }));
      if (r === 0) alignements.push(cellule.attrs.align as string | null);
      // Fusion de cellules : sans équivalent en Markdown, la place est gardée.
      for (let i = 1; i < ((cellule.attrs.colspan as number) ?? 1); i++) {
        cellules.push("");
        if (r === 0) alignements.push(null);
      }
    });
    lignes.push(cellules);
  });
  const largeur = alignements.length;
  const rangee = (cellules: string[]) =>
    `| ${Array.from({ length: largeur }, (_, i) => cellules[i] ?? "").join(" | ")} |`;
  const separation = alignements.map((a) =>
    a === "center" ? ":---:" : a === "right" ? "---:" : a === "left" ? ":---" : "---",
  );
  const corps = [rangee(lignes[0] ?? []), rangee(separation), ...lignes.slice(1).map(rangee)].join("\n");
  const apparence = ecrireApparence(noeud.attrs as Apparence);
  return apparence ? `${apparence}\n${corps}` : corps;
}

type OptionsLigne = { uneLigne?: boolean; cellule?: boolean };

/** Échappe ce qui serait lu comme une syntaxe Markdown dans du texte ordinaire. */
export function echapper(texte: string, cellule = false): string {
  let resultat = texte
    .replace(/[\\`*$[\]]/g, "\\$&")
    .replace(/_/g, (souligne, position: number, tout: string) =>
      /[\p{L}\p{N}]/u.test(tout[position - 1] ?? "") && /[\p{L}\p{N}]/u.test(tout[position + 1] ?? "")
        ? souligne
        : "\\_",
    )
    .replace(/~(?=~)|(?<=~)~/g, "\\~")
    .replace(/<(?=[A-Za-z/!?])/g, "\\<")
    .replace(/&(?=#?[A-Za-z0-9]+;)/g, "\\&");
  if (cellule) resultat = resultat.replace(/\|/g, "\\|");
  return resultat;
}

/** Écrit un extrait de code en ligne, avec assez d'accents graves autour. */
function codeEnLigne(texte: string, cellule: boolean): string {
  // Un retour à la ligne dans un extrait de code se lit comme une espace.
  const contenu = (cellule ? texte.replace(/\|/g, "\\|") : texte).replace(/\n/g, " ");
  const plusLongue = Math.max(0, ...[...contenu.matchAll(/`+/g)].map((r) => r[0].length));
  const barriere = "`".repeat(plusLongue + 1);
  const espace =
    contenu.startsWith("`") || contenu.endsWith("`") || (/^ .* $/s.test(contenu) && contenu.trim() !== "")
      ? " "
      : "";
  return `${barriere}${espace}${contenu}${espace}${barriere}`;
}

/** Destination d'un lien ou d'une image : entre chevrons si elle contient une espace, < > ou ( ). */
export function destination(adresse: string, titre: unknown): string {
  const lien = adresse === "" || /[\s<>()]/.test(adresse) ? `<${adresse.replace(/[<>]/g, "\\$&")}>` : adresse;
  return typeof titre === "string" && titre ? `${lien} "${titre.replace(/["\\]/g, "\\$&")}"` : lien;
}

/** Rang d'une marque dans le schéma : à étendue égale, la première est la plus extérieure. */
const rangMarque = (marque: Mark) => Object.keys(schema.marks).indexOf(marque.type.name);

const delimiteurs: Record<string, [string, string]> = {
  strong: ["**", "**"],
  em: ["*", "*"],
  underline: ["<u>", "</u>"],
  strike: ["~~", "~~"],
  sup: ["<sup>", "</sup>"],
  sub: ["<sub>", "</sub>"],
};

/** Contenu en ligne d'un bloc → Markdown. */
function enLigneMd(parent: Node, options: OptionsLigne = {}): string {
  const cellule = options.cellule === true;
  // Les extraits de code voisins n'en font qu'un : « `a``b` » serait mal relu.
  const enfants: Node[] = [];
  parent.forEach((enfant) => {
    const avant = enfants[enfants.length - 1];
    if (avant?.isText && enfant.isText && m.code.isInSet(avant.marks) && m.code.isInSet(enfant.marks)) {
      enfants[enfants.length - 1] = schema.text((avant.text ?? "") + (enfant.text ?? ""), avant.marks);
    } else enfants.push(enfant);
  });

  let sortie = "";
  const ouvertes: Mark[] = [];
  const ouvrant = (marque: Mark) => {
    if (marque.type === m.link) return "[";
    if (marque.type === m.couleur) return `<span style="color:${marque.attrs.valeur}">`;
    if (marque.type === m.taille) return `<span style="font-size:${marque.attrs.valeur}">`;
    return delimiteurs[marque.type.name]?.[0] ?? "";
  };
  const fermant = (marque: Mark) => {
    if (marque.type === m.link) return `](${destination(marque.attrs.href as string, marque.attrs.title)})`;
    if (marque.type === m.couleur || marque.type === m.taille) return "</span>";
    return delimiteurs[marque.type.name]?.[1] ?? "";
  };
  /** Ferme les marques au-delà des `garder` premières ; les espaces finaux passent après le délimiteur. */
  const fermerJusqua = (garder: number) => {
    while (ouvertes.length > garder) {
      const marque = ouvertes.pop() as Mark;
      const espaces = /\s+$/.exec(sortie)?.[0] ?? "";
      sortie = sortie.slice(0, sortie.length - espaces.length) + fermant(marque) + espaces;
    }
  };
  /** Nombre d'enfants consécutifs, à partir de `depuis`, qui portent la marque. */
  const etendue = (marque: Mark, depuis: number) => {
    let fin = depuis;
    while (fin < enfants.length && marque.isInSet((enfants[fin] as Node).marks)) fin++;
    return fin - depuis;
  };

  for (let i = 0; i < enfants.length; i++) {
    const enfant = enfants[i] as Node;
    if (enfant.type === n.hard_break) {
      fermerJusqua(0);
      // Un saut de ligne n'a de sens qu'entre deux textes : en bord de paragraphe, il serait lu « \ ».
      const suite = enfants
        .slice(i + 1)
        .some((s) => s.type !== n.hard_break && (!s.isText || s.text?.trim()));
      if (sortie.trim() !== "" && suite) sortie += options.uneLigne ? " " : "\\\n";
      continue;
    }
    const marques = enfant.marks.filter((marque) => marque.type !== m.code);
    let garder = 0;
    while (garder < ouvertes.length && (ouvertes[garder] as Mark).isInSet(marques)) garder++;
    fermerJusqua(garder);

    let texte = enfant.isText ? (enfant.text ?? "") : null;
    if (texte !== null && options.uneLigne) texte = texte.replace(/\n/g, " ");
    const blanc = texte !== null && texte.trim() === "";
    const aOuvrir = blanc
      ? []
      : marques
          .filter((marque) => !marque.isInSet(ouvertes))
          .sort((a, b) => etendue(b, i) - etendue(a, i) || rangMarque(a) - rangMarque(b));

    // Lien automatique d'origine (« <https://…> » ou adresse nue) resté identique.
    const lien = aOuvrir.find((marque) => marque.type === m.link);
    if (lien?.attrs.auto && aOuvrir.length === 1 && texte !== null) {
      const fin = i + etendue(lien, i);
      const morceaux = enfants.slice(i, fin);
      const tout = morceaux.map((morceau) => morceau.text ?? "").join("");
      const href = lien.attrs.href as string;
      const simple = morceaux.every((morceau) => morceau.isText && morceau.marks.length === 1);
      if (simple && (tout === href || `mailto:${tout}` === href || `http://${tout}` === href)) {
        sortie += lien.attrs.auto === "autolink" ? `<${tout}>` : tout;
        i = fin - 1;
        continue;
      }
    }

    if (texte !== null && aOuvrir.length > 0) {
      const espaces = /^\s*/.exec(texte)?.[0] ?? "";
      sortie += espaces;
      texte = texte.slice(espaces.length);
    }
    for (const marque of aOuvrir) {
      sortie += ouvrant(marque);
      ouvertes.push(marque);
    }

    if (texte !== null) {
      sortie += m.code.isInSet(enfant.marks) ? codeEnLigne(texte, cellule) : echapper(texte, cellule);
    } else if (enfant.type === n.image) {
      sortie += `![${echapper(enfant.attrs.alt as string, cellule)}](${destination(enfant.attrs.src as string, enfant.attrs.title)})`;
    } else if (enfant.type === n.math_inline) {
      sortie += `$${enfant.attrs.tex}$`;
    } else if (enfant.type === n.note) {
      sortie += `[^${enfant.attrs.label}]`;
    }
  }
  fermerJusqua(0);

  // Débuts de ligne : rien ne doit y être lu comme un titre, une liste, une citation…
  // Une ligne vide terminerait le paragraphe : elle est retirée.
  return sortie
    .split("\n")
    .filter((ligne) => ligne.trim() !== "")
    .map((ligne) => {
      const nette = ligne.replace(/^[ \t]+/, "").replace(/[ \t]+$/, "");
      if (/^(#{1,6}|[-+])(\s|$)/.test(nette) || nette.startsWith(">") || /^(=+|-+)$/.test(nette)) {
        return `\\${nette}`;
      }
      return nette.replace(/^(\d{1,9})([.)])(?=\s|$)/, "$1\\$2");
    })
    .join("\n");
}
