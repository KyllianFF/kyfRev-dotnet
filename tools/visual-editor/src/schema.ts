// Schéma du document de l'éditeur visuel : exactement ce que le Markdown des
// fiches sait écrire (section 8.5), pas davantage. Ce qui n'y entre pas (notes
// de bas de page, définitions de liens…) reste un « bloc Markdown » modifiable
// tel quel, jamais perdu.

import { type DOMOutputSpec, type MarkSpec, type Node, type NodeSpec, Schema } from "prosemirror-model";
import { tableNodes } from "prosemirror-tables";
import { APPARENCE, type Apparence, apparenceDuDom, attributsApparence } from "./apparence";

type Alignement = "left" | "center" | "right" | null;

const alignement = {
  default: null,
  getFromDOM: (dom: HTMLElement): Alignement => {
    const valeur = dom.style.textAlign;
    return valeur === "left" || valeur === "center" || valeur === "right" ? valeur : null;
  },
  setDOMAttr: (valeur: unknown, attrs: Record<string, unknown>) => {
    if (valeur) attrs.style = `text-align: ${valeur}`;
  },
};

const tableaux = tableNodes({
  tableGroup: "block",
  cellContent: "inline*",
  cellAttributes: { align: alignement },
}) as Record<"table" | "table_row" | "table_cell" | "table_header", NodeSpec>;

type NomNoeud =
  | "doc"
  | "paragraph"
  | "alignement"
  | "heading"
  | "blockquote"
  | "horizontal_rule"
  | "code_block"
  | "math_block"
  | "brut"
  | "bullet_list"
  | "ordered_list"
  | "list_item"
  | "table"
  | "table_row"
  | "table_cell"
  | "table_header"
  | "text"
  | "image"
  | "hard_break"
  | "math_inline"
  | "note";

type NomMarque = "link" | "couleur" | "taille" | "strong" | "em" | "underline" | "strike" | "sup" | "sub" | "code";

/** Liste « serrée » (sans ligne vide entre ses points) : présentée sans espace entre eux, comme en lecture. */
const serree = (noeud: Node): Record<string, string> => (noeud.attrs.tight ? { class: "serree" } : {});

const noeuds: Record<NomNoeud, NodeSpec> = {
  doc: { content: "block+" },

  paragraph: {
    content: "inline*",
    group: "block",
    parseDOM: [{ tag: "p" }],
    toDOM: () => ["p", 0],
  },

  heading: {
    attrs: { level: { default: 1 } },
    content: "inline*",
    group: "block",
    defining: true,
    parseDOM: [1, 2, 3, 4, 5, 6].map((niveau) => ({ tag: `h${niveau}`, attrs: { level: niveau } })),
    toDOM: (noeud) => [`h${noeud.attrs.level}`, 0],
  },

  blockquote: {
    content: "block+",
    group: "block",
    defining: true,
    parseDOM: [{ tag: "blockquote" }],
    toDOM: () => ["blockquote", 0],
  },

  /** Un passage aligné : `<div align="…">`, en Markdown. */
  alignement: {
    attrs: { align: { default: "center" } },
    content: "block+",
    group: "block",
    parseDOM: [
      {
        tag: "div[data-align]",
        getAttrs: (dom) => ({ align: dom.getAttribute("data-align") ?? "center" }),
      },
    ],
    toDOM: (noeud) => [
      "div",
      { class: "aligne", "data-align": noeud.attrs.align, style: `text-align:${noeud.attrs.align}` },
      0,
    ],
  },

  horizontal_rule: {
    attrs: { markup: { default: "---" } },
    group: "block",
    parseDOM: [{ tag: "hr" }],
    toDOM: () => ["hr"],
  },

  code_block: {
    attrs: { language: { default: "" }, fence: { default: "```" } },
    content: "text*",
    marks: "",
    group: "block",
    code: true,
    defining: true,
    parseDOM: [
      {
        tag: "pre",
        preserveWhitespace: "full",
        getAttrs: (dom) => ({
          language: /language-(\S+)/.exec(dom.querySelector("code")?.className ?? "")?.[1] ?? "",
        }),
      },
    ],
    toDOM: (noeud) => [
      "pre",
      ["code", noeud.attrs.language ? { class: `language-${noeud.attrs.language}` } : {}, 0],
    ],
  },

  math_block: {
    attrs: { tex: { default: "" } },
    group: "block",
    atom: true,
    parseDOM: [
      { tag: "div.formule[data-tex]", getAttrs: (dom) => ({ tex: dom.getAttribute("data-tex") ?? "" }) },
    ],
    toDOM: (noeud) => ["div", { class: "formule bloc", "data-tex": noeud.attrs.tex }, noeud.attrs.tex],
  },

  /** Markdown que l'éditeur visuel ne sait pas présenter : gardé et modifiable tel quel. */
  brut: {
    content: "text*",
    marks: "",
    group: "block",
    code: true,
    defining: true,
    parseDOM: [{ tag: "pre.bloc-brut", preserveWhitespace: "full" }],
    toDOM: () => ["pre", { class: "bloc-brut" }, ["code", 0]],
  },

  bullet_list: {
    attrs: { bullet: { default: "-" }, tight: { default: true } },
    content: "list_item+",
    group: "block",
    parseDOM: [{ tag: "ul" }],
    toDOM: (noeud) => ["ul", serree(noeud), 0],
  },

  ordered_list: {
    attrs: { order: { default: 1 }, delim: { default: "." }, tight: { default: true } },
    content: "list_item+",
    group: "block",
    parseDOM: [
      {
        tag: "ol",
        getAttrs: (dom) => ({
          order: dom.hasAttribute("start") ? Number(dom.getAttribute("start")) || 1 : 1,
        }),
      },
    ],
    toDOM: (noeud): DOMOutputSpec => [
      "ol",
      { ...serree(noeud), ...(noeud.attrs.order === 1 ? {} : { start: String(noeud.attrs.order) }) },
      0,
    ],
  },

  /** `tache` : `null` pour un point ordinaire, sinon une case, cochée ou non. */
  list_item: {
    attrs: { tache: { default: null } },
    content: "paragraph block*",
    defining: true,
    parseDOM: [
      {
        tag: "li",
        getAttrs: (dom) => ({
          tache: dom.hasAttribute("data-tache") ? dom.getAttribute("data-tache") === "fait" : null,
        }),
      },
    ],
    toDOM: (noeud) => [
      "li",
      noeud.attrs.tache === null ? {} : { "data-tache": noeud.attrs.tache ? "fait" : "a-faire" },
      0,
    ],
  },

  ...tableaux,

  // L'apparence du tableau vit sur lui : la ligne au-dessus de lui la donne, et la page la montre.
  table: {
    ...tableaux.table,
    attrs: Object.fromEntries(Object.entries(APPARENCE).map(([nom, valeur]) => [nom, { default: valeur }])),
    parseDOM: [{ tag: "table", getAttrs: (dom) => apparenceDuDom(dom as HTMLElement) }],
    toDOM: (noeud) => ["table", attributsApparence(noeud.attrs as Apparence), ["tbody", 0]],
  },

  text: { group: "inline" },

  image: {
    attrs: { src: {}, alt: { default: "" }, title: { default: null } },
    inline: true,
    group: "inline",
    draggable: true,
    parseDOM: [
      {
        tag: "img[src]",
        getAttrs: (dom) => ({
          src: dom.getAttribute("src"),
          alt: dom.getAttribute("alt") ?? "",
          title: dom.getAttribute("title"),
        }),
      },
    ],
    toDOM: (noeud) => ["img", { src: noeud.attrs.src, alt: noeud.attrs.alt, title: noeud.attrs.title }],
  },

  hard_break: {
    inline: true,
    group: "inline",
    selectable: false,
    parseDOM: [{ tag: "br" }],
    toDOM: () => ["br"],
  },

  math_inline: {
    attrs: { tex: { default: "" } },
    inline: true,
    group: "inline",
    atom: true,
    parseDOM: [
      { tag: "span.formule[data-tex]", getAttrs: (dom) => ({ tex: dom.getAttribute("data-tex") ?? "" }) },
    ],
    toDOM: (noeud) => ["span", { class: "formule", "data-tex": noeud.attrs.tex }, noeud.attrs.tex],
  },

  /** Appel de note de bas de page « [^1] ». */
  note: {
    attrs: { label: {} },
    inline: true,
    group: "inline",
    atom: true,
    toDOM: (noeud) => [
      "sup",
      { class: "appel-note", title: "Note de bas de page" },
      `[${noeud.attrs.label}]`,
    ],
  },
};

/** L'ordre des marques est celui de leur imbrication en Markdown (la première est la plus extérieure). */
const marques: Record<NomMarque, MarkSpec> = {
  link: {
    attrs: { href: {}, title: { default: null }, auto: { default: null } },
    inclusive: false,
    parseDOM: [
      {
        tag: "a[href]",
        getAttrs: (dom) => ({ href: dom.getAttribute("href"), title: dom.getAttribute("title") }),
      },
    ],
    toDOM: (marque) => ["a", { href: marque.attrs.href, title: marque.attrs.title ?? marque.attrs.href }, 0],
  },

  // Couleur et taille : seulement les nôtres. Celles d'un texte collé d'ailleurs (une page
  // web fixe la couleur de tout) n'entrent pas dans la fiche.
  couleur: {
    attrs: { valeur: {} },
    parseDOM: [
      { tag: "span[data-couleur]", getAttrs: (dom) => ({ valeur: dom.getAttribute("data-couleur") }) },
    ],
    toDOM: (marque) => [
      "span",
      { class: "couleur", "data-couleur": marque.attrs.valeur, style: `--couleur:${marque.attrs.valeur}` },
      0,
    ],
  },

  taille: {
    attrs: { valeur: {} },
    parseDOM: [
      { tag: "span[data-taille]", getAttrs: (dom) => ({ valeur: dom.getAttribute("data-taille") }) },
    ],
    toDOM: (marque) => [
      "span",
      { "data-taille": marque.attrs.valeur, style: `font-size:${marque.attrs.valeur}` },
      0,
    ],
  },

  strong: {
    parseDOM: [
      { tag: "strong" },
      // Word et Google Docs : <b style="font-weight: normal"> enveloppe parfois tout le texte collé.
      { tag: "b", getAttrs: (dom) => dom.style.fontWeight !== "normal" && null },
      { style: "font-weight=400", clearMark: (m) => m.type.name === "strong" },
      { style: "font-weight", getAttrs: (valeur) => /^(bold(er)?|[6-9]\d{2})$/.test(valeur) && null },
    ],
    toDOM: () => ["strong", 0],
  },

  em: {
    parseDOM: [
      { tag: "i" },
      { tag: "em" },
      { style: "font-style=italic" },
      { style: "font-style=normal", clearMark: (m) => m.type.name === "em" },
    ],
    toDOM: () => ["em", 0],
  },

  underline: {
    parseDOM: [{ tag: "u" }, { style: "text-decoration=underline" }],
    toDOM: () => ["u", 0],
  },

  strike: {
    parseDOM: [{ tag: "s" }, { tag: "del" }, { tag: "strike" }, { style: "text-decoration=line-through" }],
    toDOM: () => ["s", 0],
  },

  sup: {
    excludes: "sup sub",
    parseDOM: [{ tag: "sup" }, { style: "vertical-align=super" }],
    toDOM: () => ["sup", 0],
  },

  sub: {
    excludes: "sup sub",
    parseDOM: [{ tag: "sub" }, { style: "vertical-align=sub" }],
    toDOM: () => ["sub", 0],
  },

  code: {
    parseDOM: [{ tag: "code" }],
    toDOM: () => ["code", 0],
  },
};

export const schema = new Schema<NomNoeud, NomMarque>({ nodes: noeuds, marks: marques });
