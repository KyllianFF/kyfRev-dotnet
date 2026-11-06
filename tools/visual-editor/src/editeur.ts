// Éditeur visuel des fiches (ProseMirror) : on écrit comme dans un traitement de
// texte, la fiche reste un fichier Markdown (voir ./markdown.ts). Repris de la 0.x ;
// ses menus et ses dialogues sont natifs ici, et l'hôte parle par ./main.ts.

import "prosemirror-view/style/prosemirror.css";
import "prosemirror-tables/style/tables.css";
import "prosemirror-gapcursor/style/gapcursor.css";
import "katex/dist/katex.min.css";
import "./fiche.css";
import "./page.css";
import { baseKeymap, chainCommands, exitCode, setBlockType, toggleMark } from "prosemirror-commands";
import { dropCursor } from "prosemirror-dropcursor";
import { gapCursor } from "prosemirror-gapcursor";
import { history, redo, redoDepth, undo, undoDepth } from "prosemirror-history";
import {
  InputRule,
  inputRules,
  textblockTypeInputRule,
  undoInputRule,
  wrappingInputRule,
} from "prosemirror-inputrules";
import { keymap } from "prosemirror-keymap";
import type { Mark, MarkType, Node, NodeType } from "prosemirror-model";
import { liftListItem, sinkListItem, splitListItem, wrapInList } from "prosemirror-schema-list";
import {
  type Command,
  EditorState,
  NodeSelection,
  Plugin,
  Selection,
  TextSelection,
  type Transaction,
} from "prosemirror-state";
import {
  addColumnAfter,
  addColumnBefore,
  addRowAfter,
  addRowBefore,
  deleteColumn,
  deleteRow,
  deleteTable,
  goToNextCell,
  isInTable,
  selectedRect,
  type TableRect,
  tableEditing,
} from "prosemirror-tables";
import type { Apparence, Bandes, Largeur, Theme, Traits } from "./apparence";
import {
  Decoration,
  DecorationSet,
  type EditorView,
  type NodeView,
  type ViewMutationRecord,
  EditorView as Vue,
} from "prosemirror-view";
import { affichageImage, formuleHtml, svgNettoye } from "./analyse";
import {
  type Alignement,
  alignementCourant,
  alignerSelection,
  basculerTaches,
  insertionDeBloc,
  insertionDeLigne,
  sansTaches,
} from "./blocs";
import type { Casse } from "./casse";
import { changementDeCasse } from "./casse-visuelle";
import { colorer, langages, type Morceau } from "./coloration";
import { motAutour } from "./correcteur";
import { depuisMarkdown, type Origine, trancheBrute, trancheCollee, versMarkdown } from "./markdown";
import { motsEntiers } from "./mots-entiers";
import { schema } from "./schema";
import { caseVoisine, changerApparence, triable, trierTableau } from "./tableaux";

const n = schema.nodes;
const m = schema.marks;

export type NomMarque = "strong" | "em" | "underline" | "strike" | "sup" | "sub" | "code";
export type ActionTableau =
  | "rowBefore"
  | "rowAfter"
  | "columnBefore"
  | "columnAfter"
  | "deleteRow"
  | "deleteColumn"
  | "deleteTable";

const ACTIONS_TABLEAU: ActionTableau[] = ["rowBefore", "rowAfter", "columnBefore", "columnAfter", "deleteRow", "deleteColumn", "deleteTable"];

/** Ce que le ruban affiche : mises en forme actives, type de bloc… */
export type EtatOutils = {
  bold: boolean;
  italic: boolean;
  underline: boolean;
  strike: boolean;
  superscript: boolean;
  subscript: boolean;
  link: boolean;
  heading: number;
  inCode: boolean;
  list: "bullets" | "numbers" | "tasks" | null;
  table: boolean;
  /** Le tableau où est le curseur, pour l'onglet Tableau ; rien hors d'un tableau. */
  tableLook: EtatTableau | null;
  /** Couleur et taille du texte, écrites comme dans le fichier ; `null` : celles du thème. */
  color: string | null;
  size: string | null;
  align: Alignement;
  canUndo: boolean;
  canRedo: boolean;
};

export type Dessin = { svg: string | null; image: string | null; editable: boolean };

/** L'apparence du tableau où est le curseur, et sa colonne (à partir de 1). */
export type EtatTableau = {
  header: Apparence["entete"];
  total: boolean;
  bands: Bandes;
  lines: Traits;
  theme: Theme;
  hidden: number | null;
  width: Largeur;
  column: number;
  columnAlign: string | null;
  sortable: boolean;
};


export type Formule = { position: number | null; tex: string; block: boolean };

/** Ce que l'éditeur dit à l'hôte. */
export type Reactions = {
  /** Le texte a changé par une action de l'utilisateur. */
  onmodification: () => void;
  /** Ctrl+S ou perte du focus : enregistrer tout de suite. */
  onenregistrer: () => void;
  ondeposer: (fichiers: File[], position: number | null) => void;
  onetat: (etat: EtatOutils) => void;
  /** Ctrl+K : ouvrir la boîte « Lien ». */
  onlien: () => void;
  /** Double-clic sur une formule : la modifier. */
  onformule: (formule: Formule) => void;
  /** Saisie « [[… » au curseur, ou plus de saisie (`null`). */
  onsaisielien: (saisie: { requete: string; debut: number; x: number; y: number } | null) => void;
  /** Touche pendant une saisie : vrai si la liste la prend. */
  clavierlien: (touche: string) => boolean;
  /** Clic droit : le menu est natif. */
  onmenu: (dessin: DessinVise | null) => void;
  /** Le dessin d'un bloc : un SVG, ou une image ; ni l'un ni l'autre : il ne se dessine pas. */
  dessin: (langage: string, code: string) => Promise<Dessin>;
  /** Modifier ce dessin : c'est la galerie de l'application. */
  ondessin: (langage: string, code: string, position: number) => void;
};

/** Touches que la liste des fiches peut prendre à l'éditeur. */
const TOUCHES_LIEN = ["ArrowDown", "ArrowUp", "Enter", "Tab", "Escape"] as const;

/** « [[ » suivi de ce qui est tapé, juste avant le curseur. */
function saisieLienAvant(texteAvant: string): { requete: string; longueur: number } | null {
  const trouve = /\[\[([^[\]\n]{0,80})$/.exec(texteAvant);
  return trouve ? { requete: trouve[1] ?? "", longueur: trouve[0].length } : null;
}

// --- Clavier et saisie --------------------------------------------------------

const dansListe = (etat: EditorState) => {
  const { $from } = etat.selection;
  for (let profondeur = $from.depth; profondeur > 0; profondeur--) {
    if ($from.node(profondeur).type === n.list_item) return true;
  }
  return false;
};

/** Tab : cellule suivante dans un tableau, retrait dans une liste ; ailleurs, le focus avance. */
const tabulation =
  (sens: 1 | -1): Command =>
  (etat, dispatch) => {
    if (isInTable(etat)) return goToNextCell(sens)(etat, dispatch);
    if (!dansListe(etat)) return false;
    (sens === 1 ? sinkListItem : liftListItem)(n.list_item)(etat, dispatch);
    return true;
  };

/** Ctrl+Origine, Ctrl+Fin (avec Maj : en sélectionnant) : début ou fin de la fiche. */
const bord =
  (fin: boolean, etendre: boolean): Command =>
  (etat, dispatch) => {
    const cible = fin ? Selection.atEnd(etat.doc) : Selection.atStart(etat.doc);
    const selection = etendre ? TextSelection.between(etat.selection.$anchor, cible.$head) : cible;
    dispatch?.(etat.tr.setSelection(selection).scrollIntoView());
    return true;
  };

/** Le curseur est dans un bloc de code. */
const dansLeCode = (etat: EditorState) => etat.selection.$from.parent.type === n.code_block;

/** Retrait d'une ligne de code. */
const RETRAIT = "    ";

/**
 * Entrée dans un bloc de code : nouvelle ligne, au même retrait que la précédente.
 * Sur la ligne vide qui termine le bloc, Entrée sort du bloc et l'on continue dessous.
 */
const entreeDansLeCode: Command = (etat, dispatch) => {
  const { $from, $to, empty } = etat.selection;
  if (!dansLeCode(etat) || $to.parent !== $from.parent) return false;
  const texte = $from.parent.textContent;
  const avant = texte.slice(0, $from.parentOffset);
  const ligne = avant.slice(avant.lastIndexOf("\n") + 1);
  // Dernière ligne vide (ou seulement son retrait), et ce n'est pas la première.
  const finDeBloc = $from.parentOffset === texte.length && avant.length > ligne.length;
  if (empty && finDeBloc && ligne.trim() === "") {
    const finDuBloc = $from.after();
    let tr = etat.tr.delete($from.pos - ligne.length - 1, $from.pos);
    const apres = tr.mapping.map(finDuBloc);
    tr = tr.insert(apres, n.paragraph.create());
    dispatch?.(tr.setSelection(TextSelection.create(tr.doc, apres + 1)).scrollIntoView());
    return true;
  }
  const retrait = /^[ \t]*/.exec(ligne)?.[0] ?? "";
  dispatch?.(etat.tr.insertText(`\n${retrait}`, $from.pos, $to.pos).scrollIntoView());
  return true;
};

/** Tab, Maj+Tab dans un bloc de code : retrait des lignes de la sélection. */
const retraitDuCode =
  (sens: 1 | -1): Command =>
  (etat, dispatch) => {
    if (!dansLeCode(etat)) return false;
    const { $from, $to, empty } = etat.selection;
    if ($from.parent !== $to.parent) return true;
    if (sens === 1 && empty) {
      dispatch?.(etat.tr.insertText(RETRAIT));
      return true;
    }
    const texte = $from.parent.textContent;
    const debuts: number[] = [];
    for (let i = texte.lastIndexOf("\n", $from.parentOffset - 1) + 1; ; ) {
      debuts.push(i);
      const suivant = texte.indexOf("\n", i);
      if (suivant === -1 || suivant >= $to.parentOffset) break;
      i = suivant + 1;
    }
    const debutDuBloc = $from.start();
    let tr = etat.tr;
    for (const debut of debuts.reverse()) {
      const position = debutDuBloc + debut;
      if (sens === 1) {
        tr = tr.insertText(RETRAIT, position);
      } else {
        const reste = texte.slice(debut);
        const longueur = reste.startsWith("\t") ? 1 : (/^ {1,4}/.exec(reste)?.[0].length ?? 0);
        if (longueur > 0) tr = tr.delete(position, position + longueur);
      }
    }
    dispatch?.(tr);
    return true;
  };

/**
 * Flèche haut au tout début de la fiche, quand elle s'ouvre sur un bloc de code, un
 * tableau ou une formule : un paragraphe se crée au-dessus, pour écrire avant.
 */
const avantLePremierBloc: Command = (etat, dispatch, vue) => {
  const premier = etat.doc.firstChild;
  if (!premier || (premier.isTextblock && !premier.type.spec.code)) return false;
  const { selection } = etat;
  if (selection instanceof NodeSelection) {
    if (selection.from !== 0) return false;
  } else {
    const { $from, empty } = selection;
    if (!empty || $from.depth === 0 || $from.before(1) !== 0) return false;
    if (premier.type === n.table && $from.index(1) !== 0) return false;
    // Dans une liste, une citation, un passage aligné : seulement depuis leur tout premier bloc.
    if (premier.type !== n.table && $from.start() !== Selection.atStart(etat.doc).from) return false;
    if (vue && !vue.endOfTextblock("up")) return false;
  }
  const tr = etat.tr.insert(0, n.paragraph.create());
  dispatch?.(tr.setSelection(TextSelection.create(tr.doc, 1)).scrollIntoView());
  return true;
};

/** Un bloc après lequel on ne pourrait pas écrire sans un paragraphe qui le suive. */
const demandeUnParagraphe = (noeud: Node | null) =>
  noeud !== null && (!noeud.isTextblock || noeud.type.spec.code === true);

/** Ajoute un paragraphe vide en fin de document s'il le faut. */
function avecParagrapheFinal(doc: Node): Node {
  return demandeUnParagraphe(doc.lastChild) ? doc.copy(doc.content.addToEnd(n.paragraph.create())) : doc;
}

/** Un paragraphe termine toujours la fiche ; il ne s'écrit pas dans le fichier. */
function paragrapheFinal(): Plugin {
  return new Plugin({
    appendTransaction(_transactions, _ancien, etat) {
      if (!demandeUnParagraphe(etat.doc.lastChild)) return null;
      return etat.tr.insert(etat.doc.content.size, n.paragraph.create()).setMeta("addToHistory", false);
    },
  });
}

/** Morceaux colorés de chaque bloc de code, gardés tant que le bloc ne change pas. */
const morceauxDuCode = new WeakMap<Node, Morceau[]>();

/** Coloration du code selon son langage. */
function colorationDuCode(): Plugin {
  let dernier: { doc: Node; decorations: DecorationSet } | null = null;
  return new Plugin({
    props: {
      decorations(etat) {
        if (dernier?.doc === etat.doc) return dernier.decorations;
        const liste: Decoration[] = [];
        etat.doc.descendants((noeud, position) => {
          if (noeud.type !== n.code_block) return !noeud.isTextblock;
          const langue = noeud.attrs.language as string;
          if (!langue || langue === "mermaid") return false;
          let morceaux = morceauxDuCode.get(noeud);
          if (!morceaux) {
            morceaux = colorer(noeud.textContent, langue);
            morceauxDuCode.set(noeud, morceaux);
          }
          for (const morceau of morceaux) {
            liste.push(
              Decoration.inline(position + 1 + morceau.debut, position + 1 + morceau.fin, {
                class: morceau.classes,
              }),
            );
          }
          return false;
        });
        dernier = { doc: etat.doc, decorations: DecorationSet.create(etat.doc, liste) };
        return dernier.decorations;
      },
    },
  });
}

const sautDeLigne: Command = chainCommands(exitCode, (etat, dispatch) => {
  dispatch?.(etat.tr.replaceSelectionWith(n.hard_break.create()).scrollIntoView());
  return true;
});

/** Entrée dans une tâche : la suivante reste à faire, même si celle-ci est faite. */
const entreeDansListe: Command = (etat, dispatch, vue) => {
  const { $from } = etat.selection;
  const point = $from.depth >= 1 ? $from.node(-1) : null;
  const aFaire = point?.type === n.list_item && point.attrs.tache !== null ? { tache: false } : undefined;
  return splitListItem(n.list_item, aFaire)(etat, dispatch, vue);
};

const aligner =
  (valeur: Alignement): Command =>
  (etat, dispatch) => {
    const tr = alignerSelection(etat, valeur);
    if (tr) dispatch?.(tr.scrollIntoView());
    return true;
  };

/** Raccourcis Markdown pendant la frappe : « # », « - », « 1. », « > », « ``` », « [ ] ». */
function reglesDeSaisie(): Plugin {
  return inputRules({
    rules: [
      textblockTypeInputRule(/^(#{1,4})\s$/, n.heading, (r) => ({ level: r[1]?.length ?? 1 })),
      wrappingInputRule(/^\s*([-+*])\s$/, n.bullet_list),
      wrappingInputRule(
        /^(\d+)([.)])\s$/,
        n.ordered_list,
        (r) => ({ order: Number(r[1]), delim: r[2] }),
        (r, noeud) => noeud.childCount + (noeud.attrs.order as number) === Number(r[1]),
      ),
      wrappingInputRule(/^\s*>\s$/, n.blockquote),
      textblockTypeInputRule(/^```([\w-]*)\s$/, n.code_block, (r) => ({ language: r[1] ?? "" })),
      // « [ ] » puis espace : une tâche, dans un point de liste ou en en créant un.
      new InputRule(/^\[([ xX]?)\]\s$/, (etat, r, debut, fin) => {
        const fait = (r[1] ?? " ").toLowerCase() === "x";
        const avant = etat.apply(etat.tr.delete(debut, fin));
        const tr = etat.tr.delete(debut, fin);
        const taches = basculerTaches(avant);
        if (!taches) return null;
        for (const pas of taches.steps) tr.step(pas);
        const $ici = tr.doc.resolve(tr.mapping.map(debut));
        if (fait && $ici.node(-1).type === n.list_item) {
          tr.setNodeMarkup($ici.before(-1), null, { ...$ici.node(-1).attrs, tache: true });
        }
        return tr;
      }),
      // « --- » puis espace : un trait de séparation, et l'on continue d'écrire dessous.
      new InputRule(/^(?:---|\*\*\*|___)\s$/, (etat, _r, debut, fin) => {
        const $debut = etat.doc.resolve(debut);
        if ($debut.parent.type !== n.paragraph || $debut.parent.textContent.length !== fin - debut)
          return null;
        const avant = $debut.before();
        const trait = n.horizontal_rule.create();
        const tr = etat.tr.replaceWith(avant, $debut.after(), [trait, n.paragraph.create()]);
        return tr.setSelection(TextSelection.create(tr.doc, avant + trait.nodeSize + 1));
      }),
    ],
  });
}

// --- Vues des nœuds -------------------------------------------------------------

/** Point de liste : une tâche a sa case, que l'on coche sans quitter le texte. */
class VuePoint implements NodeView {
  dom: HTMLElement;
  contentDOM: HTMLElement;
  private readonly boite: HTMLInputElement | null = null;

  constructor(
    private noeud: Node,
    vue: EditorView,
    position: () => number | undefined,
  ) {
    this.dom = document.createElement("li");
    this.contentDOM = this.dom;
    if (noeud.attrs.tache === null) return;
    this.dom.className = "tache";
    const boite = document.createElement("input");
    boite.type = "checkbox";
    boite.contentEditable = "false";
    boite.setAttribute("aria-label", "Tâche faite");
    boite.addEventListener("mousedown", (evenement) => evenement.preventDefault());
    boite.addEventListener("click", (evenement) => {
      evenement.preventDefault();
      const ici = position();
      if (ici === undefined) return;
      const attrs = { ...this.noeud.attrs, tache: !this.noeud.attrs.tache };
      vue.dispatch(vue.state.tr.setNodeMarkup(ici, null, attrs));
    });
    this.boite = boite;
    this.contentDOM = document.createElement("div");
    this.contentDOM.className = "tache-texte";
    this.dom.append(boite, this.contentDOM);
    this.afficher();
  }

  private afficher(): void {
    if (!this.boite) return;
    const fait = this.noeud.attrs.tache === true;
    this.boite.checked = fait;
    this.dom.classList.toggle("faite", fait);
    this.dom.dataset.tache = fait ? "fait" : "a-faire";
  }

  update(noeud: Node): boolean {
    if (noeud.type !== this.noeud.type || (noeud.attrs.tache === null) !== (this.noeud.attrs.tache === null)) {
      return false;
    }
    this.noeud = noeud;
    this.afficher();
    return true;
  }

  ignoreMutation(mutation: ViewMutationRecord): boolean {
    if (mutation.type === "selection") return false;
    return !this.contentDOM.contains(mutation.target);
  }

  stopEvent(evenement: Event): boolean {
    return evenement.target === this.boite;
  }
}

/** Image du coffre, ou encadré qui explique pourquoi elle ne s'affiche pas. */
function vueImage(dossier: () => string) {
  return (noeud: Node): NodeView => {
    const dom = document.createElement("span");
    dom.className = "image";
    const bloquee = (message: string, detail: string) => {
      const encadre = document.createElement("span");
      encadre.className = "image-bloquee";
      encadre.textContent = `${message} : ${detail}`;
      encadre.title = detail;
      return encadre;
    };
    const affichage = affichageImage(noeud.attrs.src as string, dossier());
    if ("bloquee" in affichage) {
      dom.append(bloquee(affichage.bloquee, affichage.detail));
    } else {
      const image = document.createElement("img");
      image.src = affichage.adresse;
      image.alt = noeud.attrs.alt as string;
      if (noeud.attrs.title) image.title = noeud.attrs.title as string;
      image.addEventListener(
        "error",
        () => image.replaceWith(bloquee("Image introuvable", noeud.attrs.src as string)),
        { once: true },
      );
      dom.append(image);
    }
    return { dom, ignoreMutation: () => true };
  };
}

function vueFormule(bloc: boolean, onformule: (formule: Formule) => void) {
  return (noeud: Node, _vue: EditorView, position: () => number | undefined): NodeView => {
    // HTML de KaTeX déjà nettoyé par DOMPurify : son élément « .formule » sert de vue.
    const modele = document.createElement("template");
    modele.innerHTML = formuleHtml(noeud.attrs.tex as string, bloc);
    const rendu = modele.content.querySelector<HTMLElement>(".formule, .katex-display, .katex");
    const dom = document.createElement(bloc ? "div" : "span");
    dom.className = bloc ? "formule bloc" : "formule";
    if (rendu) dom.append(rendu);
    else dom.textContent = noeud.attrs.tex as string;
    dom.title = "Double-cliquez pour modifier la formule";
    dom.addEventListener("dblclick", () => {
      const ici = position();
      if (ici !== undefined) onformule({ position: ici, tex: noeud.attrs.tex as string, block: bloc });
    });
    return { dom, ignoreMutation: () => true };
  };
}

/** Le dessin visé par un clic droit. */
export type DessinVise = { position: number; language: string; code: string };

/** Où se trouve chaque bloc dessiné : son nœud le sait, son élément non. */
const positionsDesDessins = new WeakMap<Element, () => number | undefined>();

/** Le dessin sous la souris, tant qu'on n'est pas entré dans son code. */
function dessinSous(vue: EditorView, cible: EventTarget | null): DessinVise | null {
  const bloc = cible instanceof Element ? cible.closest(".bloc-dessin:not(.en-edition)") : null;
  const position = bloc ? positionsDesDessins.get(bloc)?.() : undefined;
  const noeud = position === undefined ? null : vue.state.doc.nodeAt(position);
  return noeud && position !== undefined
    ? { position, language: noeud.attrs.language as string, code: noeud.textContent }
    : null;
}

/** Les langages de bloc que dessinent les extensions actives : l'application les donne avec chaque fiche. */
let blocsDesExtensions: ReadonlySet<string> = new Set();

/** Un bloc qui se dessine : son code ne se montre que si l'on y entre. */
const seDessine = (langage: string) => /^(smartart\s|mermaid\s*$|reseau\s*$)/.test(langage) || blocsDesExtensions.has(langage.trim());

/** Le dessin suit la frappe, sans la gêner. */
const DELAI_DESSIN = 250;

/** Bloc de code : son langage se choisit dans son en-tête, directement dans le texte. */
class VueCode implements NodeView {
  dom: HTMLElement;
  contentDOM: HTMLElement;
  private entete: HTMLElement;
  private apercu: HTMLElement | null = null;
  private boutonModifier: HTMLButtonElement | null = null;
  private minuterie: ReturnType<typeof setTimeout> | undefined;

  constructor(
    private noeud: Node,
    vue: EditorView,
    position: () => number | undefined,
    decorations: readonly Decoration[],
    private reactions: Reactions,
  ) {
    this.dom = document.createElement("div");
    this.dom.className = "bloc-code";
    this.marquerEdition(decorations);
    this.entete = this.creerEntete(vue, position);
    this.dom.append(this.entete);
    if (seDessine(noeud.attrs.language as string)) this.preparerDessin(vue, position);
    const pre = document.createElement("pre");
    this.contentDOM = document.createElement("code");
    if (noeud.attrs.language) this.contentDOM.className = `language-${noeud.attrs.language}`;
    pre.append(this.contentDOM);
    this.dom.append(pre);
  }

  /** Langage du bloc (texte brut, langages colorés, diagramme) et rappel pour en sortir. */
  private creerEntete(vue: EditorView, position: () => number | undefined): HTMLElement {
    const entete = document.createElement("div");
    entete.className = "entete-code";
    entete.contentEditable = "false";
    const choix = document.createElement("select");
    choix.setAttribute("aria-label", "Langage du bloc de code");
    const langue = this.noeud.attrs.language as string;
    const options: [string, string][] = [
      ["", "Texte brut"],
      ...langages().map(({ id, nom }): [string, string] => [id, nom]),
      ["mermaid", "Diagramme (Mermaid)"],
      ["reseau", "Schéma réseau"],
      ...[...blocsDesExtensions].map((id): [string, string] => [id, `Bloc « ${id} » (extension)`]),
    ];
    if (!options.some(([id]) => id === langue)) options.splice(1, 0, [langue, langue]);
    for (const [id, nom] of options) choix.add(new Option(nom, id, false, id === langue));
    choix.addEventListener("change", () => {
      const ici = position();
      if (ici === undefined) return;
      vue.dispatch(vue.state.tr.setNodeMarkup(ici, null, { ...this.noeud.attrs, language: choix.value }));
      vue.focus();
    });
    const aide = document.createElement("span");
    aide.className = "aide-code";
    aide.textContent = "Pour sortir : Entrée sur une ligne vide à la fin, ou Ctrl+Entrée";
    entete.append(choix, aide);
    return entete;
  }

  /** Le dessin du bloc, et ce qu'il faut pour le modifier ou pour voir son code. */
  private preparerDessin(vue: EditorView, position: () => number | undefined): void {
    this.dom.classList.add("bloc-dessin");
    positionsDesDessins.set(this.dom, position);
    this.apercu = document.createElement("div");
    this.apercu.className = "apercu-dessin";
    this.apercu.contentEditable = "false";
    const modifier = () => {
      const ici = position();
      if (ici !== undefined) {
        this.reactions.ondessin(this.noeud.attrs.language as string, this.noeud.textContent, ici);
      }
    };
    this.apercu.addEventListener("dblclick", modifier);
    this.dom.append(this.apercu);
    const action = (texte: string, faire: () => void, classe = "") => {
      const bouton = document.createElement("button");
      bouton.type = "button";
      bouton.className = `action-dessin ${classe}`;
      bouton.textContent = texte;
      bouton.addEventListener("click", faire);
      return bouton;
    };
    const langage = this.noeud.attrs.language as string;
    // L'hôte dit avec le dessin si le bloc se rouvre dans un éditeur.
    this.boutonModifier = action("Modifier…", modifier);
    this.boutonModifier.hidden = true;
    this.entete.append(this.boutonModifier);
    if (!/^smartart/.test(langage)) document.addEventListener("theme", this.suivreTheme);
    this.entete.append(
      action(
        "Voir le code",
        () => {
          const ici = position();
          if (ici === undefined) return;
          vue.dispatch(vue.state.tr.setSelection(TextSelection.create(vue.state.doc, ici + 1)));
          vue.focus();
        },
        "action-code",
      ),
    );
    this.demanderDessin();
  }

  private demanderDessin(): void {
    clearTimeout(this.minuterie);
    this.minuterie = setTimeout(() => {
      const code = this.noeud.textContent;
      void this.reactions.dessin(this.noeud.attrs.language as string, code).then((dessin) => {
        if (this.apercu && this.noeud.textContent === code) this.montrerDessin(dessin);
      });
    }, DELAI_DESSIN);
  }

  /** Le dessin ; s'il n'y en a pas, c'est le code qui se lit. */
  private montrerDessin({ svg, image, editable }: Dessin): void {
    if (!this.apercu) return;
    if (this.boutonModifier) this.boutonModifier.hidden = !editable;
    this.dom.classList.toggle("dessin-impossible", !svg && !image);
    if (image) {
      const dessin = document.createElement("img");
      dessin.src = image;
      dessin.alt = this.noeud.textContent;
      this.apercu.replaceChildren(dessin);
    } else this.apercu.innerHTML = svg ? svgNettoye(svg) : "";
  }

  private readonly suivreTheme = () => this.demanderDessin();

  destroy(): void {
    clearTimeout(this.minuterie);
    document.removeEventListener("theme", this.suivreTheme);
  }

  private marquerEdition(decorations: readonly Decoration[]) {
    this.dom.classList.toggle(
      "en-edition",
      decorations.some((d) => (d.spec as { enEdition?: boolean }).enEdition),
    );
  }

  update(noeud: Node, decorations: readonly Decoration[]): boolean {
    if (noeud.type !== this.noeud.type || noeud.attrs.language !== this.noeud.attrs.language) return false;
    const change = noeud.textContent !== this.noeud.textContent;
    this.noeud = noeud;
    this.marquerEdition(decorations);
    if (change && this.apercu) this.demanderDessin();
    return true;
  }

  ignoreMutation(mutation: ViewMutationRecord): boolean {
    if (mutation.type === "selection") return false;
    return !this.contentDOM.contains(mutation.target);
  }

  stopEvent(evenement: Event): boolean {
    return this.entete.contains(evenement.target as globalThis.Node);
  }
}

/** Marque le bloc de code où se trouve le curseur, et l'invite d'un document vide. */
function decorations(): Plugin {
  return new Plugin({
    props: {
      decorations(etat) {
        const liste: Decoration[] = [];
        const { $from } = etat.selection;
        for (let profondeur = $from.depth; profondeur > 0; profondeur--) {
          if ($from.node(profondeur).type === n.code_block) {
            const debut = $from.before(profondeur);
            liste.push(
              Decoration.node(debut, debut + $from.node(profondeur).nodeSize, {}, { enEdition: true }),
            );
          }
        }
        const { doc } = etat;
        if (
          doc.childCount === 1 &&
          doc.firstChild?.type === n.paragraph &&
          doc.firstChild.content.size === 0
        ) {
          liste.push(
            Decoration.node(0, doc.firstChild.nodeSize, {
              class: "invite",
              "data-invite": "Écrivez votre fiche… (le ruban met en forme, comme dans un traitement de texte)",
            }),
          );
        }
        return DecorationSet.create(doc, liste);
      },
    },
  });
}

// --- L'éditeur ------------------------------------------------------------------

const images = (liste: FileList | undefined | null) =>
  [...(liste ?? [])].filter((fichier) => fichier.type.startsWith("image/"));

export class EditeurVisuel {
  readonly vue: EditorView;
  private origine: Origine;
  private cache: { doc: Node; texte: string } | null = null;
  private dossier = "";

  constructor(
    parent: HTMLElement,
    private reactions: Reactions,
  ) {
    this.origine = depuisMarkdown("");
    this.vue = new Vue(parent, {
      state: this.etatInitial(),
      attributes: {
        class: "contenu-fiche editeur-visuel",
        spellcheck: "true",
        lang: "fr",
        role: "textbox",
        "aria-multiline": "true",
        "aria-label": "Texte de la fiche",
      },
      nodeViews: {
        image: vueImage(() => this.dossier),
        math_inline: vueFormule(false, reactions.onformule),
        math_block: vueFormule(true, reactions.onformule),
        code_block: (noeud, vue, position, decorations) => new VueCode(noeud, vue, position, decorations, reactions),
        list_item: (noeud, vue, position) => new VuePoint(noeud, vue, position),
      },
      dispatchTransaction: (tr: Transaction) => {
        this.vue.updateState(this.vue.state.apply(tr));
        if (tr.docChanged) reactions.onmodification();
        reactions.onetat(this.etatOutils());
        this.signalerSaisieLien();
      },
      handleDOMEvents: {
        // Le menu est natif : ProseMirror n'a qu'à laisser le curseur à sa place.
        contextmenu: (vue, evenement) => {
          evenement.preventDefault();
          const dessin = dessinSous(vue, evenement.target);
          if (dessin) {
            reactions.onmenu(dessin);
            return true;
          }
          const lieu = vue.posAtCoords({ left: evenement.clientX, top: evenement.clientY });
          const { from, to, empty } = vue.state.selection;
          if (lieu && (empty || lieu.pos < from || lieu.pos > to)) {
            vue.dispatch(vue.state.tr.setSelection(TextSelection.near(vue.state.doc.resolve(lieu.pos))));
          }
          reactions.onmenu(null);
          return true;
        },
        blur: () => {
          reactions.onenregistrer();
          reactions.onsaisielien(null);
          return false;
        },
        focus: () => {
          setTimeout(() => this.signalerSaisieLien());
          return false;
        },
        // Les liens ne s'ouvrent pas depuis l'éditeur.
        click: (_vue, evenement) => {
          if ((evenement.target as HTMLElement).closest?.("a")) evenement.preventDefault();
          return false;
        },
      },
      handleDrop: (vue, evenement) => {
        const fichiers = images((evenement as DragEvent).dataTransfer?.files);
        if (fichiers.length === 0) return false;
        evenement.preventDefault();
        const position = vue.posAtCoords({ left: evenement.clientX, top: evenement.clientY })?.pos;
        reactions.ondeposer(fichiers, position ?? vue.state.selection.from);
        return true;
      },
      handlePaste: (vue, evenement) => {
        const fichiers = images(evenement.clipboardData?.files);
        if (fichiers.length === 0) return false;
        reactions.ondeposer(fichiers, vue.state.selection.from);
        return true;
      },
      // Texte collé (Ctrl+V) : lu comme du Markdown (« **gras** » devient gras), sans bloc
      // de code par simple retrait. Ctrl+Maj+V (`brut`) : tel quel, une ligne par paragraphe.
      clipboardTextParser: (texteColle, _contexte, brut) =>
        brut ? trancheBrute(texteColle) : trancheCollee(texteColle),
    });
    reactions.onetat(this.etatOutils());
  }

  private etatInitial(): EditorState {
    const r = this.reactions;
    return EditorState.create({
      doc: avecParagrapheFinal(this.origine.doc),
      plugins: [
        // Liste des fiches ouverte par « [[ » : elle prend d'abord ses touches.
        keymap(Object.fromEntries(TOUCHES_LIEN.map((touche) => [touche, () => r.clavierlien(touche)]))),
        reglesDeSaisie(),
        keymap({
          "Mod-z": undo,
          "Mod-y": redo,
          "Shift-Mod-z": redo,
          "Mod-b": toggleMark(m.strong),
          "Mod-i": toggleMark(m.em),
          "Mod-u": toggleMark(m.underline),
          "Mod-k": () => {
            setTimeout(r.onlien);
            return true;
          },
          "Mod-s": () => {
            r.onenregistrer();
            return true;
          },
          "Mod-Home": bord(false, false),
          "Mod-End": bord(true, false),
          "Shift-Mod-Home": bord(false, true),
          "Shift-Mod-End": bord(true, true),
          "Mod-Alt-0": setBlockType(n.paragraph),
          "Mod-Alt-1": setBlockType(n.heading, { level: 1 }),
          "Mod-Alt-2": setBlockType(n.heading, { level: 2 }),
          "Mod-Alt-3": setBlockType(n.heading, { level: 3 }),
          "Mod-Alt-4": setBlockType(n.heading, { level: 4 }),
          "Mod-l": aligner("left"),
          "Mod-r": aligner("right"),
          "Mod-j": aligner("justify"),
          Enter: chainCommands(entreeDansLeCode, entreeDansListe),
          // Au-dessus d'un tableau en tête de fiche, une ligne s'ouvre ; dans un tableau, ↑ et ↓ vont de case en case.
          ArrowUp: chainCommands(avantLePremierBloc, caseVoisine(-1)),
          ArrowDown: caseVoisine(1),
          "Shift-Enter": sautDeLigne,
          "Mod-Enter": sautDeLigne,
          Tab: chainCommands(retraitDuCode(1), tabulation(1)),
          "Shift-Tab": chainCommands(retraitDuCode(-1), tabulation(-1)),
          // Comme dans Word : la casse suivante (minuscules, CAPITALES…).
          "Shift-F3": (etat, dispatch) => {
            const tr = changementDeCasse(etat);
            if (tr && dispatch) dispatch(tr.scrollIntoView());
            return true;
          },
          Backspace: undoInputRule,
        }),
        keymap(baseKeymap),
        history(),
        dropCursor({ color: false, class: "curseur-depot" }),
        gapCursor(),
        tableEditing(),
        decorations(),
        motsEntiers(),
        colorationDuCode(),
        paragrapheFinal(),
      ],
    });
  }

  texte(): string {
    const { doc } = this.vue.state;
    if (this.cache?.doc !== doc) this.cache = { doc, texte: versMarkdown(doc, this.origine) };
    return this.cache.texte;
  }

  /** Contenu déjà connu de l'hôte : rien à enregistrer, historique remis à zéro. */
  remplacer(texte: string, dossier: string, dessines: readonly string[] = []): void {
    blocsDesExtensions = new Set(dessines);
    this.dossier = dossier;
    this.origine = depuisMarkdown(texte);
    this.cache = null;
    this.vue.updateState(this.etatInitial());
    this.reactions.onetat(this.etatOutils());
  }

  insererImage(lien: string, alt: string, position: number | null): void {
    const { state } = this.vue;
    const ici = position ?? state.selection.from;
    const image = n.image.create({ src: lien, alt });
    this.vue.dispatch(state.tr.replaceRangeWith(ici, position === null ? state.selection.to : ici, image));
    this.vue.focus();
  }

  insererTableau(lignes: number, colonnes: number): void {
    const rangee = (type: NodeType) =>
      n.table_row.create(
        null,
        Array.from({ length: colonnes }, () => type.create()),
      );
    const tableau = n.table.create(null, [
      rangee(n.table_header),
      ...Array.from({ length: Math.max(1, lignes - 1) }, () => rangee(n.table_cell)),
    ]);
    this.insererBloc(tableau, 3);
  }

  /** Ligne horizontale ; l'écriture reprend dans un paragraphe juste dessous. */
  insererLigne(): void {
    this.vue.dispatch(insertionDeLigne(this.vue.state));
    this.vue.focus();
  }

  /** Change le langage du bloc où est le curseur ; sinon, crée un bloc, curseur dedans. */
  insererCode(langage: string): void {
    const { $from } = this.vue.state.selection;
    if ($from.parent.type === n.code_block) {
      const position = $from.before();
      this.vue.dispatch(this.vue.state.tr.setNodeMarkup(position, null, { ...$from.parent.attrs, language: langage }));
      this.vue.focus();
      return;
    }
    this.insererBloc(n.code_block.create({ language: langage }), 1);
  }

  /** Insère un dessin après le paragraphe courant, ou remplace celui de cette position. */
  insererDessin(langage: string, code: string, position: number | null): void {
    const bloc = n.code_block.create({ language: langage }, code ? schema.text(code) : null);
    const actuel = position === null ? null : this.vue.state.doc.nodeAt(position);
    if (position !== null && actuel?.type === n.code_block) {
      this.vue.dispatch(this.vue.state.tr.replaceWith(position, position + actuel.nodeSize, bloc));
      this.vue.focus();
      return;
    }
    this.insererBloc(bloc, bloc.nodeSize);
  }

  /** Retire un bloc de la fiche ; Ctrl+Z le rend. Une fiche garde toujours un paragraphe où écrire. */
  supprimerBloc(position: number): void {
    const etat = this.vue.state;
    const noeud = etat.doc.nodeAt(position);
    if (noeud?.type !== n.code_block) return;
    const fin = position + noeud.nodeSize;
    const tr = etat.doc.childCount === 1 ? etat.tr.replaceWith(position, fin, n.paragraph.create()) : etat.tr.delete(position, fin);
    this.vue.dispatch(tr.scrollIntoView());
    this.vue.focus();
  }

  /** Entre dans le code d'un bloc. */
  voirCodeBloc(position: number): void {
    const etat = this.vue.state;
    if (etat.doc.nodeAt(position)?.type !== n.code_block) return;
    this.vue.dispatch(etat.tr.setSelection(TextSelection.create(etat.doc, position + 1)));
    this.vue.focus();
  }

  /** Le dessin dont le curseur est dans le code : il se retire aussi depuis là. */
  dessinSousLeCurseur(): DessinVise | null {
    const { $from } = this.vue.state.selection;
    if ($from.parent.type !== n.code_block || !seDessine($from.parent.attrs.language as string)) return null;
    return { position: $from.before(), language: $from.parent.attrs.language as string, code: $from.parent.textContent };
  }

  focus(): void {
    this.vue.focus();
  }

  /** Insère un bloc après le paragraphe courant (ou à sa place s'il est vide). */
  private insererBloc(bloc: Node, decalage: number) {
    this.vue.dispatch(insertionDeBloc(this.vue.state, bloc, decalage));
    this.vue.focus();
  }

  private executer(commande: Command): void {
    commande(this.vue.state, this.vue.dispatch);
    this.vue.focus();
  }

  marque(nom: NomMarque): void {
    this.executer(toggleMark(m[nom] as MarkType));
  }

  effacerMiseEnForme(): void {
    this.executer((etat, dispatch) => {
      const { from, to, empty } = etat.selection;
      if (empty) return false;
      let tr = etat.tr;
      for (const nom of ["couleur", "taille", "strong", "em", "underline", "strike", "sup", "sub", "code"] as const) {
        tr = tr.removeMark(from, to, m[nom] as MarkType);
      }
      dispatch?.(tr);
      return true;
    });
  }

  /** Pose une couleur (`#rrggbb`), ou rend celle du thème ; sans sélection, pour ce qui sera tapé. */
  couleur(valeur: string | null): void {
    this.poserValeur(m.couleur as MarkType, valeur);
  }

  /** Pose une taille (`1.25em`), ou rend la taille normale ; sans sélection, pour ce qui sera tapé. */
  taille(valeur: string | null): void {
    this.poserValeur(m.taille as MarkType, valeur);
  }

  private poserValeur(type: MarkType, valeur: string | null): void {
    this.executer((etat, dispatch) => {
      const tr = etat.tr;
      if (etat.selection.empty) {
        if (valeur) tr.addStoredMark(type.create({ valeur }));
        else tr.removeStoredMark(type);
      } else {
        for (const { $from, $to } of etat.selection.ranges) {
          if (valeur) tr.addMark($from.pos, $to.pos, type.create({ valeur }));
          else tr.removeMark($from.pos, $to.pos, type);
        }
      }
      dispatch?.(tr);
      return true;
    });
  }

  aligner(valeur: Alignement): void {
    this.executer(aligner(valeur));
  }

  titre(niveau: number): void {
    if (niveau === 0) this.executer(setBlockType(n.paragraph));
    else this.executer(setBlockType(n.heading, { level: niveau }));
  }

  liste(genre: "bullets" | "numbers" | "tasks"): void {
    if (genre === "tasks") {
      this.executer((etat, dispatch) => {
        const tr = basculerTaches(etat);
        if (tr) dispatch?.(tr.scrollIntoView());
        return true;
      });
      return;
    }
    const type = genre === "bullets" ? n.bullet_list : n.ordered_list;
    this.executer((etat, dispatch) => {
      const { $from } = etat.selection;
      const ordinaires = sansTaches(etat);
      for (let profondeur = $from.depth; profondeur > 0; profondeur--) {
        const noeud = $from.node(profondeur);
        if (noeud.type === type) {
          // Une tâche redevient un point de cette liste ; un point ordinaire en sort.
          if (ordinaires) {
            dispatch?.(ordinaires);
            return true;
          }
          return liftListItem(n.list_item)(etat, dispatch);
        }
        if (noeud.type === n.bullet_list || noeud.type === n.ordered_list) {
          dispatch?.((ordinaires ?? etat.tr).setNodeMarkup($from.before(profondeur), type));
          return true;
        }
      }
      return wrapInList(type)(etat, dispatch);
    });
  }

  /** Le tableau autour du curseur, et sa position ; rien hors d'un tableau. */
  private tableauCourant(etat: EditorState): { pos: number; noeud: Node } | null {
    const { $from } = etat.selection;
    for (let profondeur = $from.depth; profondeur > 0; profondeur--) {
      if ($from.node(profondeur).type === n.table) return { pos: $from.before(profondeur), noeud: $from.node(profondeur) };
    }
    return null;
  }

  /** Ce que l'onglet Tableau montre : l'apparence du tableau et la colonne du curseur. */
  private etatTableau(etat: EditorState): EtatTableau | null {
    const tableau = isInTable(etat) ? this.tableauCourant(etat) : null;
    if (!tableau) return null;
    const rect = selectedRect(etat);
    const apparence = tableau.noeud.attrs as Apparence;
    const cellule = rect.table.nodeAt(rect.map.map[rect.top * rect.map.width + rect.left] ?? 0);
    return {
      header: apparence.entete,
      total: apparence.total,
      bands: apparence.bandes,
      lines: apparence.traits,
      theme: apparence.theme,
      hidden: apparence.reviser,
      width: apparence.largeur,
      column: rect.left + 1,
      columnAlign: (cellule?.attrs.align as string | null | undefined) ?? null,
      sortable: triable(tableau.noeud),
    };
  }

  /**
   * Change l'apparence du tableau où est le curseur, ou sa colonne. Les en-têtes, la dernière ligne et la colonne cachée
   * en révision s'inversent ; les bandes suivent l'en-tête ; le reste prend la valeur donnée.
   */
  apparenceTableau(changement: string, valeur: string | null): void {
    const etat = this.vue.state;
    const tableau = isInTable(etat) ? this.tableauCourant(etat) : null;
    if (!tableau) return;
    const rect = selectedRect(etat);
    if (changement === "action") {
      if (ACTIONS_TABLEAU.includes(valeur as ActionTableau)) this.tableau(valeur as ActionTableau);
      return;
    }
    if (changement === "aligner") {
      this.alignerColonne(rect, valeur === "center" || valeur === "right" ? valeur : null);
      return;
    }
    if (changement === "trier") {
      const trie = trierTableau(tableau.noeud, rect.left);
      if (trie) {
        // Le tableau trié a la même taille : le curseur reste à la même place, dans le tableau.
        const tr = etat.tr.replaceWith(tableau.pos, tableau.pos + tableau.noeud.nodeSize, trie);
        tr.setSelection(TextSelection.near(tr.doc.resolve(etat.selection.from)));
        this.vue.dispatch(tr.scrollIntoView());
      }
      this.vue.focus();
      return;
    }
    const apparence = changerApparence(tableau.noeud.attrs as Apparence, changement, valeur, rect.left + 1);
    if (!apparence) return;
    this.vue.dispatch(etat.tr.setNodeMarkup(tableau.pos, null, apparence));
    this.vue.focus();
  }

  /** Aligne toute une colonne ; le Markdown l'écrit sous l'en-tête (« :---: »). */
  private alignerColonne(rect: TableRect, align: "center" | "right" | null): void {
    const tr = this.vue.state.tr;
    const vues = new Set<number>();
    for (let ligne = 0; ligne < rect.map.height; ligne++) {
      const position = rect.map.map[ligne * rect.map.width + rect.left];
      if (position === undefined || vues.has(position)) continue;
      vues.add(position);
      const cellule = rect.table.nodeAt(position);
      if (cellule) tr.setNodeMarkup(rect.tableStart + position, null, { ...cellule.attrs, align });
    }
    this.vue.dispatch(tr);
    this.vue.focus();
  }

  tableau(action: ActionTableau): void {
    const commandes: Record<ActionTableau, Command> = {
      rowBefore: addRowBefore,
      rowAfter: addRowAfter,
      columnBefore: addColumnBefore,
      columnAfter: addColumnAfter,
      deleteRow,
      deleteColumn,
      deleteTable,
    };
    this.executer(commandes[action]);
  }

  annuler(): void {
    this.executer(undo);
  }

  retablir(): void {
    this.executer(redo);
  }

  /** Lien sous le curseur (ou texte sélectionné), pour préremplir la boîte « Lien ». */
  lienCourant(): { texte: string; adresse: string } {
    const plage = this.plageLien();
    const { state } = this.vue;
    if (plage)
      return {
        texte: state.doc.textBetween(plage.from, plage.to),
        adresse: plage.marque.attrs.href as string,
      };
    const { from, to } = state.selection;
    // Sélection qui porte (en partie) un lien : c'est lui qu'on modifie.
    let adresse = "";
    state.doc.nodesBetween(from, to, (noeud) => {
      const marque = adresse === "" ? m.link?.isInSet(noeud.marks) : undefined;
      if (marque) adresse = marque.attrs.href as string;
    });
    return { texte: state.doc.textBetween(from, to, " "), adresse };
  }

  /** Saisie « [[… » au curseur, hors du code : la recherche, et l'étendue à remplacer. */
  private saisieLien(): { requete: string; debut: number; fin: number } | null {
    const { selection, doc } = this.vue.state;
    if (!selection.empty) return null;
    const { $from } = selection;
    if ($from.parent.type.spec.code || $from.marks().some((marque) => marque.type === m.code)) return null;
    // Un élément sans texte (image, formule) compte pour un caractère, comme dans le document.
    const trouve = saisieLienAvant($from.parent.textBetween(0, $from.parentOffset, undefined, "￼"));
    if (!trouve) return null;
    const suite = doc.textBetween(selection.from, Math.min(doc.content.size, selection.from + 2));
    const fin = suite === "]]" ? selection.from + 2 : selection.from;
    return { requete: trouve.requete, debut: selection.from - trouve.longueur, fin };
  }

  private signalerSaisieLien(): void {
    const saisie = this.vue.hasFocus() ? this.saisieLien() : null;
    if (!saisie) {
      this.reactions.onsaisielien(null);
      return;
    }
    const coordonnees = this.vue.coordsAtPos(saisie.debut);
    this.reactions.onsaisielien({ requete: saisie.requete, debut: saisie.debut, x: coordonnees.left, y: coordonnees.bottom });
  }

  /** Remplace la saisie « [[… » par un lien vers la fiche ; la frappe qui suit n'est plus un lien. */
  insererLienFiche(texte: string, adresse: string): void {
    const saisie = this.saisieLien();
    const { state } = this.vue;
    const { from, to } = saisie ? { from: saisie.debut, to: saisie.fin } : state.selection;
    const lien = m.link as MarkType;
    const tr = state.tr.replaceWith(from, to, schema.text(texte, [lien.create({ href: adresse })])).removeStoredMark(lien);
    tr.setSelection(TextSelection.create(tr.doc, from + texte.length));
    this.vue.dispatch(tr.scrollIntoView());
    this.vue.focus();
  }

  /** Crée ou modifie le lien : sur la sélection, sur le lien existant, ou insère le texte. */
  lien(texte: string, adresse: string): void {
    const lien = m.link as MarkType;
    const marque = lien.create({ href: adresse });
    this.executer((etat, dispatch) => {
      const plage = this.plageLien();
      const { from, to } = plage ?? etat.selection;
      let tr = etat.tr;
      const actuel = etat.doc.textBetween(from, to);
      if (texte && texte !== actuel) {
        tr = tr.insertText(texte, from, to);
        tr = tr.addMark(from, from + texte.length, marque);
      } else if (from === to) {
        tr = tr.insertText(adresse, from).addMark(from, from + adresse.length, marque);
      } else {
        tr = tr.removeMark(from, to, lien).addMark(from, to, marque);
      }
      dispatch?.(tr.scrollIntoView());
      return true;
    });
  }

  retirerLien(): void {
    this.executer((etat, dispatch) => {
      const plage = this.plageLien();
      const { from, to } = plage ?? etat.selection;
      dispatch?.(etat.tr.removeMark(from, to, m.link as MarkType));
      return true;
    });
  }

  /** Étendue du lien sous le curseur (sélection vide), s'il y en a un. */
  private plageLien(): { from: number; to: number; marque: Mark } | null {
    const { $from, empty } = this.vue.state.selection;
    if (!empty) return null;
    const marque = (m.link as MarkType).isInSet($from.marks());
    if (!marque) return null;
    const parent = $from.parent;
    const debutParent = $from.start();
    let from = $from.pos;
    let to = $from.pos;
    // Le lien couvre les morceaux voisins qui portent la même marque.
    const morceaux: { from: number; to: number; dans: boolean }[] = [];
    parent.forEach((enfant, decalage) => {
      morceaux.push({
        from: debutParent + decalage,
        to: debutParent + decalage + enfant.nodeSize,
        dans: marque.isInSet(enfant.marks) !== undefined,
      });
    });
    const ici = morceaux.findIndex((p) => p.dans && p.from <= $from.pos && $from.pos <= p.to);
    if (ici === -1) return null;
    let i = ici;
    while (i > 0 && morceaux[i - 1]?.dans) i--;
    from = morceaux[i]?.from ?? from;
    i = ici;
    while (i < morceaux.length - 1 && morceaux[i + 1]?.dans) i++;
    to = morceaux[i]?.to ?? to;
    return { from, to, marque };
  }

  /** Insère une formule, ou remplace celle qui est en cours de modification. */
  formule(tex: string, bloc: boolean, position: number | null): void {
    const type = bloc ? n.math_block : n.math_inline;
    const noeud = type.create({ tex });
    if (position !== null) {
      const actuel = this.vue.state.doc.nodeAt(position);
      if (actuel && actuel.type === type) {
        this.executer((etat, dispatch) => {
          dispatch?.(etat.tr.setNodeMarkup(position, null, { tex }));
          return true;
        });
        return;
      }
      if (actuel) {
        this.executer((etat, dispatch) => {
          dispatch?.(etat.tr.replaceRangeWith(position, position + actuel.nodeSize, noeud));
          return true;
        });
        return;
      }
    }
    if (bloc) this.insererBloc(noeud, noeud.nodeSize);
    else
      this.executer((etat, dispatch) => {
        dispatch?.(etat.tr.replaceSelectionWith(noeud).scrollIntoView());
        return true;
      });
  }

  /** Formule sélectionnée (clic simple), pour le bouton « Formule ». */
  formuleSelectionnee(): Formule | null {
    const { selection } = this.vue.state;
    if (!(selection instanceof NodeSelection)) return null;
    const { node } = selection;
    if (node.type !== n.math_inline && node.type !== n.math_block) return null;
    return { position: selection.from, tex: node.attrs.tex as string, block: node.type === n.math_block };
  }

  /** Colle le presse-papiers, comme Ctrl+V ; `brut` : comme Ctrl+Maj+V, le texte tel quel. */
  async coller(brut: boolean): Promise<void> {
    this.vue.focus();
    let html: string | null = null;
    let texte = "";
    try {
      for (const element of await navigator.clipboard.read()) {
        if (!html && element.types.includes("text/html"))
          html = await (await element.getType("text/html")).text();
        if (!texte && element.types.includes("text/plain"))
          texte = await (await element.getType("text/plain")).text();
      }
    } catch {
      texte = await navigator.clipboard.readText().catch(() => "");
    }
    if (html && !brut) {
      this.vue.pasteHTML(html);
    } else if (texte && (brut || this.vue.state.selection.$from.parent.type.spec.code)) {
      this.vue.pasteText(texte);
    } else if (texte) {
      const tr = this.vue.state.tr.replaceSelection(trancheCollee(texte));
      this.vue.dispatch(tr.scrollIntoView().setMeta("paste", true).setMeta("uiEvent", "paste"));
    }
  }

  /** Amène en haut le titre `titre`, à son occurrence `rang`, et y place le curseur. */
  allerAuTitre(titre: string, rang: number): void {
    let position = -1;
    let vus = 0;
    this.vue.state.doc.descendants((noeud, debut) => {
      if (position >= 0) return false;
      if (noeud.type === n.heading && noeud.textContent.trim() === titre && vus++ === rang) position = debut;
      return true;
    });
    if (position < 0) return;
    const { state } = this.vue;
    this.vue.dispatch(state.tr.setSelection(TextSelection.create(state.doc, position + 1)));
    this.vue.focus();
    const element = this.vue.nodeDOM(position);
    if (element instanceof Element) element.scrollIntoView({ block: "start" });
  }

  /** Casse de la sélection, ou du mot du curseur ; sans `casse`, la suivante (Maj+F3). */
  changerCasse(casse: Casse | null): void {
    const tr = changementDeCasse(this.vue.state, casse ?? undefined);
    if (tr) this.vue.dispatch(tr.scrollIntoView());
    this.vue.focus();
  }

  /** Remplace une étendue par un mot, comme une frappe : Ctrl+Z l'annule. */
  remplacerMot(debut: number, fin: number, texte: string): void {
    this.vue.dispatch(this.vue.state.tr.insertText(texte, debut, fin));
    this.vue.focus();
  }

  /** Le mot sous le curseur, pour le correcteur, et où se trouve le curseur. */
  contexteDuMenu(): { word: string | null; wordStart: number; hasSelection: boolean } {
    const { $from, empty } = this.vue.state.selection;
    const contexte = { word: null as string | null, wordStart: 0, hasSelection: !empty };
    if (!$from.parent.isTextblock || $from.parent.type.spec.code) return contexte;
    const texte = $from.parent.textBetween(0, $from.parent.content.size, undefined, "￼");
    const mot = motAutour(texte, $from.parentOffset);
    if (mot) {
      contexte.word = texte.slice(mot.debut, mot.fin);
      contexte.wordStart = $from.start() + mot.debut;
    }
    return contexte;
  }

  /** Mises en forme et blocs sous le curseur, pour le ruban. */
  etatOutils(): EtatOutils {
    const etat = this.vue.state;
    const { $from, from, to, empty } = etat.selection;
    const actif = (type: MarkType | undefined) =>
      type !== undefined &&
      (empty ? type.isInSet(etat.storedMarks ?? $from.marks()) !== undefined : etat.doc.rangeHasMark(from, to, type));
    /** La valeur d'une marque sous le curseur, ou au début de la sélection. */
    const valeur = (type: MarkType): string | null => {
      let marque = empty ? type.isInSet(etat.storedMarks ?? $from.marks()) : undefined;
      if (!empty) {
        etat.doc.nodesBetween(from, to, (noeud) => {
          marque ??= type.isInSet(noeud.marks);
          return !marque;
        });
      }
      return marque ? (marque.attrs.valeur as string) : null;
    };
    const parent = $from.parent;
    let list: EtatOutils["list"] = null;
    for (let profondeur = $from.depth; profondeur > 0 && !list; profondeur--) {
      const noeud = $from.node(profondeur);
      if (noeud.type === n.list_item && noeud.attrs.tache !== null) list = "tasks";
      if (noeud.type === n.bullet_list) list = "bullets";
      if (noeud.type === n.ordered_list) list = "numbers";
    }
    return {
      bold: actif(m.strong),
      italic: actif(m.em),
      underline: actif(m.underline),
      strike: actif(m.strike),
      superscript: actif(m.sup),
      subscript: actif(m.sub),
      link: actif(m.link),
      heading: parent.type === n.heading ? (parent.attrs.level as number) : 0,
      inCode: parent.type === n.code_block,
      list,
      table: isInTable(etat),
      tableLook: this.etatTableau(etat),
      color: valeur(m.couleur as MarkType),
      size: valeur(m.taille as MarkType),
      align: alignementCourant(etat),
      canUndo: undoDepth(etat) > 0,
      canRedo: redoDepth(etat) > 0,
    };
  }
}
