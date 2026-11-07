// Les colonnes d'un tableau gardent la largeur de leur plus long mot, tant que le tableau tient dans la page. Laissé seul, le
// navigateur partage la place selon les plus longues lignes (un en-tête de deux mots) et coupe les mots des colonnes courtes :
// « commence / r ». Quand le tableau ne tiendrait pas, rien n'est imposé : les mots se coupent plutôt que la page ne défile.

import { Plugin, PluginKey } from "prosemirror-state";
import { Decoration, DecorationSet, type EditorView } from "prosemirror-view";

/**
 * Les largeurs minimales des colonnes, d'après celles des cases (leur plus long mot, marges comprises) : pour chaque colonne,
 * la plus grande. Rien si leur somme dépasse `disponible`.
 */
export function largeursMinimales(cases: number[][], disponible: number): number[] | null {
  const colonnes: number[] = [];
  for (const ligne of cases) {
    ligne.forEach((largeur, colonne) => {
      colonnes[colonne] = Math.max(colonnes[colonne] ?? 0, largeur);
    });
  }
  const arrondies = colonnes.map((largeur) => Math.ceil(largeur));
  const total = arrondies.reduce((somme, largeur) => somme + largeur, 0);
  return arrondies.length > 0 && total <= disponible ? arrondies : null;
}

interface Mesure {
  /** Les largeurs retenues, tableau par tableau : une mesure identique ne change rien. */
  empreinte: string;
  decorations: DecorationSet;
}

const cle = new PluginKey<Mesure>("motsEntiers");

/** La largeur du plus long mot d'une case, avec ses marges et ses traits, dans la police de la case. */
function largeurDuPlusLongMot(cellule: HTMLTableCellElement, contexte: CanvasRenderingContext2D): number {
  const style = getComputedStyle(cellule);
  contexte.font = `${style.fontStyle} ${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
  const mots = (cellule.textContent ?? "").split(/\s+/).filter((mot) => mot.length > 0);
  const plusLong = mots.reduce((largeur, mot) => Math.max(largeur, contexte.measureText(mot).width), 0);
  const bords = ["paddingLeft", "paddingRight", "borderLeftWidth", "borderRightWidth"] as const;
  // Deux pixels de plus : un mot en gras dans une case ordinaire est un peu plus large que sa mesure.
  return plusLong + bords.reduce((somme, bord) => somme + (Number.parseFloat(style[bord]) || 0), 0) + 2;
}

function mesurer(vue: EditorView): Mesure {
  const contexte = document.createElement("canvas").getContext("2d");
  const decorations: Decoration[] = [];
  const empreintes: string[] = [];
  let rang = 0;
  vue.state.doc.descendants((noeud, position) => {
    if (noeud.type.name !== "table") return true;
    rang += 1;
    const tableau = vue.nodeDOM(position);
    if (!contexte || !(tableau instanceof HTMLTableElement)) return false;
    const cases = Array.from(tableau.rows, (ligne) => Array.from(ligne.cells, (cellule) => largeurDuPlusLongMot(cellule, contexte)));
    const largeurs = largeursMinimales(cases, (tableau.parentElement ?? vue.dom).clientWidth);
    if (largeurs) {
      // La première ligne porte les largeurs : la colonne entière les suit.
      const debut = position + 2;
      noeud.firstChild?.forEach((cellule, decalage, colonne) => {
        decorations.push(Decoration.node(debut + decalage, debut + decalage + cellule.nodeSize, { style: `min-width: ${largeurs[colonne]}px` }));
      });
      empreintes.push(`${rang}:${largeurs.join(",")}`);
    }
    return false;
  });
  return { empreinte: empreintes.join(";"), decorations: DecorationSet.create(vue.state.doc, decorations) };
}

/** Mesure les tableaux après chaque changement du texte, et quand la page change de largeur. */
export function motsEntiers(): Plugin<Mesure> {
  return new Plugin<Mesure>({
    key: cle,
    state: {
      init: () => ({ empreinte: "", decorations: DecorationSet.empty }),
      apply: (tr, avant) =>
        (tr.getMeta(cle) as Mesure | undefined) ?? { empreinte: avant.empreinte, decorations: avant.decorations.map(tr.mapping, tr.doc) },
    },
    props: {
      decorations: (etat) => cle.getState(etat)?.decorations,
    },
    view: (vue) => {
      let attente = 0;
      const ajuster = () => {
        attente = 0;
        const mesure = mesurer(vue);
        if (mesure.empreinte !== cle.getState(vue.state)?.empreinte) vue.dispatch(vue.state.tr.setMeta(cle, mesure));
      };
      const planifier = () => {
        if (!attente) attente = requestAnimationFrame(ajuster);
      };
      window.addEventListener("resize", planifier);
      void document.fonts.ready.then(planifier);
      planifier();
      return {
        update: (apres, avant) => {
          if (!apres.state.doc.eq(avant.doc)) planifier();
        },
        destroy: () => {
          cancelAnimationFrame(attente);
          window.removeEventListener("resize", planifier);
        },
      };
    },
  });
}
