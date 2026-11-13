// Le repli des sections, comme dans un traitement de texte : une flèche dans la marge, à gauche de chaque titre qui a
// quelque chose sous lui. Repliée, la section ne montre plus que son titre : tout ce qui le suit, jusqu'au prochain titre
// de niveau égal ou supérieur, est caché (les titres de niveau inférieur et leur contenu avec).
//
// Rien n'est écrit dans le document ni dans le Markdown : les titres repliés vivent dans l'état de ce greffon, suivent
// les changements du texte, et repartent de zéro quand une autre fiche est chargée. Les blocs cachés le sont par une
// décoration (page.css) ; la mise en page ne les mesure pas (pages.ts).
//
// On n'écrit jamais dans du texte invisible : dès que la sélection entre dans un bloc caché, ou qu'un changement le
// touche, la section qui le cache se déplie.

import type { Node } from "prosemirror-model";
import { type Command, type EditorState, NodeSelection, Plugin, PluginKey, type Selection, TextSelection, type Transaction } from "prosemirror-state";
import { Decoration, DecorationSet, type EditorView } from "prosemirror-view";

/** La classe d'un bloc caché par une section repliée, et celle du titre qui le cache. */
export const CLASSE_CACHE = "bloc-replie";
export const CLASSE_REPLIE = "titre-replie";
/** La classe du titre où est le curseur : sa flèche se montre. */
export const CLASSE_COURANT = "titre-courant";
/** La classe de la flèche. */
export const CLASSE_FLECHE = "repli";

/** Une section : un titre de premier niveau du document, et ce qu'il gouverne. */
export type Section = {
  /** Où commence le bloc de premier niveau qui porte le titre : c'est par là qu'une section se désigne. */
  position: number;
  /** Où commence le titre lui-même : au même endroit, ou un cran plus loin s'il est seul dans un passage aligné. */
  titre: number;
  /** La fin du texte du titre : là où va le curseur quand ce qu'il gouverne se cache. */
  finDuTitre: number;
  niveau: number;
  texte: string;
  /** Ce que le titre gouverne : de `debut` à `fin`, jusqu'au prochain titre de niveau égal ou supérieur. */
  debut: number;
  fin: number;
  /** Rien à replier : aucun bloc sous le titre, ou seulement des paragraphes vides. */
  vide: boolean;
};

/** Un bloc de premier niveau, de `position` à `fin`. */
export type Bloc = { position: number; fin: number };

/**
 * Le titre que porte un bloc de premier niveau : le bloc lui-même, ou le seul bloc d'un passage aligné (un titre
 * centré). Un titre dans une citation, une liste ou un passage de plusieurs blocs n'ouvre pas de section.
 */
function titreDe(bloc: Node): { noeud: Node; decalage: number } | null {
  if (bloc.type.name === "heading") return { noeud: bloc, decalage: 0 };
  const seul = bloc.type.name === "alignement" && bloc.childCount === 1 ? bloc.firstChild : null;
  return seul?.type.name === "heading" ? { noeud: seul, decalage: 1 } : null;
}

const estVide = (bloc: Node) => bloc.type.name === "paragraph" && bloc.content.size === 0;

/** Les sections de chaque document, gardées tant qu'il ne change pas. */
const connues = new WeakMap<Node, Section[]>();

/** Les sections du document, dans l'ordre de leurs titres. */
export function sections(doc: Node): Section[] {
  const deja = connues.get(doc);
  if (deja) return deja;
  const toutes: Section[] = [];
  /** Celles qui ne sont pas finies : leurs niveaux vont croissant. */
  const ouvertes: Section[] = [];
  doc.forEach((bloc, position) => {
    const titre = titreDe(bloc);
    if (titre) {
      const niveau = titre.noeud.attrs.level as number;
      // Un titre de niveau égal ou supérieur finit les sections ouvertes avant lui.
      while (ouvertes.length > 0 && (ouvertes[ouvertes.length - 1] as Section).niveau >= niveau) (ouvertes.pop() as Section).fin = position;
    }
    if (titre || !estVide(bloc)) for (const ouverte of ouvertes) ouverte.vide = false;
    if (!titre) return;
    const debut = position + bloc.nodeSize;
    const section: Section = {
      position,
      titre: position + titre.decalage,
      finDuTitre: position + titre.decalage + titre.noeud.nodeSize - 1,
      niveau: titre.noeud.attrs.level as number,
      texte: titre.noeud.textContent,
      debut,
      fin: debut,
      vide: true,
    };
    toutes.push(section);
    ouvertes.push(section);
  });
  for (const ouverte of ouvertes) ouverte.fin = doc.content.size;
  connues.set(doc, toutes);
  return toutes;
}

/** La section du titre qui commence à `position`, si elle a quelque chose à replier. */
export function sectionA(doc: Node, position: number): Section | null {
  return sections(doc).find((section) => section.position === position && !section.vide) ?? null;
}

/** Les blocs de premier niveau que cachent les sections repliées, de haut en bas, chacun une fois. */
export function blocsCaches(doc: Node, replies: readonly number[]): Bloc[] {
  const plages = replies.map((position) => sectionA(doc, position)).filter((section) => section !== null);
  if (plages.length === 0) return [];
  const caches: Bloc[] = [];
  doc.forEach((bloc, position) => {
    if (plages.some((plage) => position >= plage.debut && position < plage.fin)) caches.push({ position, fin: position + bloc.nodeSize });
  });
  return caches;
}

/** La sélection a un bout dans ce que cette section cache, ou choisit un de ses blocs. */
function selectionCachee(selection: Selection, section: Section): boolean {
  if (selection instanceof NodeSelection) return selection.from >= section.debut && selection.to <= section.fin;
  const dedans = (position: number) => position > section.debut && position < section.fin;
  return selection.ranges.some((plage) => dedans(plage.$from.pos) || dedans(plage.$to.pos));
}

/** Ce que la transaction a changé, dans le document d'après : une plage par changement. */
function plagesChangees(tr: Transaction): [number, number][] {
  const plages: [number, number][] = [];
  tr.mapping.maps.forEach((carte, rang) => {
    const suite = tr.mapping.slice(rang + 1);
    carte.forEach((_debut, _fin, debut, fin) => plages.push([suite.map(debut, -1), suite.map(fin, 1)]));
  });
  return plages;
}

// --- L'état ---------------------------------------------------------------------

/** Les titres repliés : la position du bloc de premier niveau de chacun, en ordre croissant. */
type Etat = { replies: readonly number[] };

/** Ce qu'une transaction demande : replier ou déplier la section du titre à cette position. */
type Geste = { position: number; replie: boolean };

const cle = new PluginKey<Etat>("repli");

const AUCUN: Etat = { replies: [] };

/** Les titres repliés de cet état ; aucun sans le greffon. */
export function titresReplies(etat: EditorState): readonly number[] {
  return (cle.getState(etat) ?? AUCUN).replies;
}

/** Les mêmes sections sont repliées dans les deux états : sinon, la mise en page est à refaire. */
export function memeRepli(un: EditorState, autre: EditorState): boolean {
  return cle.getState(un) === cle.getState(autre);
}

function suivre(tr: Transaction, etat: Etat, avant: EditorState, apres: EditorState): Etat {
  const geste = tr.getMeta(cle) as Geste | undefined;
  if (etat.replies.length === 0 && !geste?.replie) return etat;
  let replies = [...etat.replies];
  // Un titre retiré, fondu dans un autre bloc (son début ou sa fin s'en vont) ou qui change de niveau n'est plus
  // replié : ce qu'il cachait se remontre, et rien d'autre ne se cache à sa place.
  if (tr.docChanged) {
    replies = replies.flatMap((position) => {
      const section = sectionA(avant.doc, position);
      const suivie = tr.mapping.mapResult(position, 1);
      if (!section || suivie.deletedAfter || tr.mapping.mapResult(section.finDuTitre, 1).deletedAfter) return [];
      return [suivie.pos];
    });
  }
  if (geste) {
    replies = replies.filter((position) => position !== geste.position);
    if (geste.replie) replies.push(geste.position);
  }
  const changees = tr.docChanged ? plagesChangees(tr) : [];
  const gardes = replies
    .filter((position) => {
      const section = sectionA(tr.doc, position);
      if (!section || selectionCachee(apres.selection, section)) return false;
      // Un changement dans ce qui est caché (annulation, mot remplacé, bloc déposé) : on le montre.
      return !changees.some(([debut, fin]) => debut < section.fin && fin > section.debut);
    })
    .sort((a, b) => a - b);
  const memes = gardes.length === etat.replies.length && gardes.every((position, rang) => position === etat.replies[rang]);
  return memes ? etat : { replies: gardes };
}

// --- Les gestes -----------------------------------------------------------------

/**
 * Replie ou déplie la section du titre à `position` ; `replier` absent : l'inverse de ce qu'elle est. Rien s'il n'y a
 * pas de section à cet endroit, ou si elle est déjà ainsi. Un curseur qui se retrouverait caché va à la fin du titre.
 */
export function bascule(etat: EditorState, position: number, replier?: boolean): Transaction | null {
  const section = sectionA(etat.doc, position);
  if (!section) return null;
  const deja = titresReplies(etat).includes(position);
  const replie = replier ?? !deja;
  if (replie === deja) return null;
  const tr = etat.tr.setMeta(cle, { position, replie } satisfies Geste).setMeta("addToHistory", false);
  if (replie && selectionCachee(etat.selection, section)) tr.setSelection(TextSelection.create(etat.doc, section.finDuTitre));
  return tr;
}

/** La section du titre où est le curseur, ou du titre le plus proche au-dessus de lui : celui qui le gouverne. */
export function sectionDuCurseur(etat: EditorState): Section | null {
  const { $from } = etat.selection;
  const ici = $from.depth > 0 ? $from.before(1) : $from.pos;
  let trouvee: Section | null = null;
  for (const section of sections(etat.doc)) {
    if (section.position > ici) break;
    trouvee = section;
  }
  return trouvee && !trouvee.vide ? trouvee : null;
}

const auCurseur =
  (replier: boolean): Command =>
  (etat, dispatch) => {
    const section = sectionDuCurseur(etat);
    if (!section) return false;
    const tr = bascule(etat, section.position, replier);
    if (tr) dispatch?.(tr.scrollIntoView());
    return true;
  };

/** Replie la section du titre qui gouverne le curseur (Alt+Maj+« - »). */
export const replierIci: Command = auCurseur(true);

/** Déplie la section du titre où est le curseur (Alt+Maj+« + »). */
export const deplierIci: Command = auCurseur(false);

/** Ce qu'il faut d'une touche pour reconnaître le geste. */
export type Touche = { key: string; code: string; altKey: boolean; shiftKey: boolean; ctrlKey: boolean; metaKey: boolean };

/**
 * Comme dans Word : Alt+Maj+« - » replie, Alt+Maj+« + » (ou « = ») déplie. Avec Maj, la touche du « - » ne dit plus
 * « - » sur tous les claviers (« 6 » sur un clavier français, « _ » sur un clavier américain) : `moins` donne les
 * touches qui l'écrivent sur ce clavier, quand le navigateur sait le dire. Le pavé numérique le dit toujours.
 */
export function gesteDuClavier(touche: Touche, moins: ReadonlySet<string> = new Set()): "replier" | "deplier" | null {
  if (!touche.altKey || !touche.shiftKey || touche.ctrlKey || touche.metaKey) return null;
  if (touche.key === "-" || moins.has(touche.code) || (touche.key === "_" && touche.code === "Minus")) return "replier";
  return touche.key === "+" || touche.key === "=" ? "deplier" : null;
}

/** Le nom de la flèche, pour les lecteurs d'écran et l'infobulle. */
export function libelle(texte: string, replie: boolean): string {
  const titre = texte.trim();
  const section = titre ? `la section « ${titre} »` : "la section";
  return `${replie ? "Déplier" : "Replier"} ${section}`;
}

// --- La page --------------------------------------------------------------------

/** La carte du clavier, là où le navigateur la donne : la touche que tape chaque emplacement, sans Maj. */
type Clavier = { getLayoutMap?: () => Promise<Iterable<[string, string]>> };

const SVG = "http://www.w3.org/2000/svg";

/** Un chevron, tracé dans la couleur du texte de la flèche : vers la droite, la page le tourne vers le bas (page.css). */
function chevron(): SVGElement {
  const dessin = document.createElementNS(SVG, "svg");
  for (const [nom, valeur] of Object.entries({ viewBox: "0 0 16 16", width: "16", height: "16", "aria-hidden": "true", focusable: "false" })) {
    dessin.setAttribute(nom, valeur);
  }
  const trait = document.createElementNS(SVG, "path");
  const attributs = { d: "M6 3.5 10.5 8 6 12.5", fill: "none", stroke: "currentColor", "stroke-width": "1.75", "stroke-linecap": "round", "stroke-linejoin": "round" };
  for (const [nom, valeur] of Object.entries(attributs)) trait.setAttribute(nom, valeur);
  dessin.append(trait);
  return dessin;
}

const nommer = (bouton: Element, texte: string, replie: boolean) => {
  const nom = libelle(texte, replie);
  if (bouton.getAttribute("aria-label") === nom) return;
  bouton.setAttribute("aria-label", nom);
  bouton.setAttribute("title", nom);
};

const flecheDe = (vue: EditorView, section: Section): Element | null => {
  const titre = vue.nodeDOM(section.titre);
  return titre instanceof Element ? titre.querySelector(`:scope > .${CLASSE_FLECHE}`) : null;
};

/** La flèche d'un titre : un vrai bouton, dans le titre mais hors de son texte, que la page pose dans la marge. */
function fleche(replie: boolean) {
  return (vue: EditorView, position: () => number | undefined): HTMLElement => {
    const bouton = document.createElement("button");
    bouton.type = "button";
    bouton.className = CLASSE_FLECHE;
    bouton.contentEditable = "false";
    bouton.setAttribute("aria-expanded", String(!replie));
    bouton.append(chevron());
    /** Le bloc de premier niveau du titre de cette flèche. */
    const bloc = () => {
      const ici = position();
      return ici === undefined ? null : vue.state.doc.resolve(ici).before(1);
    };
    const depart = bloc();
    const section = depart === null ? null : sectionA(vue.state.doc, depart);
    nommer(bouton, section?.texte ?? "", replie);
    // Le clic ne déplace pas le curseur et ne prend pas le focus à l'éditeur.
    bouton.addEventListener("mousedown", (appui) => appui.preventDefault());
    bouton.addEventListener("contextmenu", (menu) => menu.preventDefault());
    bouton.addEventListener("click", (clic) => {
      clic.preventDefault();
      const ici = bloc();
      const tr = ici === null ? null : bascule(vue.state, ici);
      if (!tr || ici === null) return;
      // Au clavier, le focus était sur la flèche : il passe à celle qui la remplace.
      const auClavier = document.activeElement === bouton;
      vue.dispatch(tr);
      const apres = auClavier ? sectionA(vue.state.doc, ici) : null;
      const suivante = apres ? flecheDe(vue, apres) : null;
      if (suivante instanceof HTMLElement) suivante.focus();
    });
    return bouton;
  };
}

/** Où commence le titre qui contient le curseur ; -1 s'il n'est pas dans un titre. */
function titreCourant(etat: EditorState): number {
  const { $head } = etat.selection;
  return $head.parent.type.name === "heading" ? $head.before() : -1;
}

function decorer(doc: Node, replies: readonly number[], courant: number): DecorationSet {
  const liste: Decoration[] = [];
  for (const section of sections(doc)) {
    if (section.vide) continue;
    const replie = replies.includes(section.position);
    liste.push(
      Decoration.widget(section.titre + 1, fleche(replie), {
        side: -1,
        // La même flèche tant que la section reste comme elle est : son nom suit le titre sans qu'elle soit refaite.
        key: replie ? "repli-ferme" : "repli-ouvert",
        ignoreSelection: true,
        // Ses clics et ses touches sont à elle : le moteur n'a pas à y voir un clic dans le texte.
        stopEvent: () => true,
      }),
    );
    const classes = [replie ? CLASSE_REPLIE : "", section.titre === courant ? CLASSE_COURANT : ""].filter((classe) => classe !== "");
    if (classes.length > 0) liste.push(Decoration.node(section.titre, section.finDuTitre + 1, { class: classes.join(" ") }));
  }
  for (const bloc of blocsCaches(doc, replies)) liste.push(Decoration.node(bloc.position, bloc.fin, { class: CLASSE_CACHE }));
  return DecorationSet.create(doc, liste);
}

/** Le repli des sections : l'état, les flèches, les blocs cachés et les deux gestes du clavier. */
export function repli(): Plugin<Etat> {
  let dernieres: { doc: Node; replies: readonly number[]; courant: number; decorations: DecorationSet } | null = null;
  /** Les touches qui écrivent « - » sur ce clavier. */
  const moins = new Set<string>();
  const lireLeClavier = () => {
    const clavier = (navigator as Navigator & { keyboard?: Clavier }).keyboard;
    void clavier?.getLayoutMap?.().then(
      (carte) => {
        moins.clear();
        for (const [code, touche] of carte) if (touche === "-") moins.add(code);
      },
      // Sans carte du clavier, seules les touches qui disent « - » comptent.
      () => moins.clear(),
    );
  };
  return new Plugin<Etat>({
    key: cle,
    state: {
      init: () => AUCUN,
      apply: (tr, etat, avant, apres) => suivre(tr, etat, avant, apres),
    },
    props: {
      decorations(etat) {
        const replies = titresReplies(etat);
        const courant = titreCourant(etat);
        if (dernieres?.doc !== etat.doc || dernieres.replies !== replies || dernieres.courant !== courant) {
          dernieres = { doc: etat.doc, replies, courant, decorations: decorer(etat.doc, replies, courant) };
        }
        return dernieres.decorations;
      },
      handleKeyDown(vue, touche) {
        const geste = gesteDuClavier(touche, moins);
        if (!geste) return false;
        return (geste === "replier" ? replierIci : deplierIci)(vue.state, vue.dispatch, vue);
      },
    },
    view(vue) {
      lireLeClavier();
      // Un autre clavier se choisit souvent hors de la fenêtre : au retour, sa carte est relue.
      window.addEventListener("focus", lireLeClavier);
      return {
        update(_vue, avant) {
          if (avant.doc === vue.state.doc) return;
          // Le titre a peut-être changé : sa flèche dit toujours son nom.
          const replies = titresReplies(vue.state);
          for (const section of sections(vue.state.doc)) {
            const bouton = section.vide ? null : flecheDe(vue, section);
            if (bouton) nommer(bouton, section.texte, replies.includes(section.position));
          }
        },
        destroy() {
          window.removeEventListener("focus", lireLeClavier);
        },
      };
    },
  });
}
