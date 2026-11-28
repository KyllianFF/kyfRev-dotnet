// Ce qui est plus large que la colonne de texte (15A), comme en lecture : sur une feuille sans fin et sans papier, le texte
// garde la colonne choisie (« Largeur du texte »), et un tableau, une image seule sur sa ligne ou un dessin plus large
// qu'elle garde sa largeur, jusqu'à celle de la colonne « Large ». Il déborde autant de chaque côté, centré sur le texte ;
// la feuille s'élargit d'autant, pour qu'il reste dessus. Sur un papier, sur une feuille A4, A5 ou A6, ou dans une fenêtre
// qui n'a que la place du texte, rien ne déborde.
//
// Le CSS seul ne sait pas centrer un bloc plus large que sa colonne sans décaler les autres : chaque bloc est mesuré, et
// celui qui déborde reçoit une décoration (sa classe et son débord de chaque côté). Le texte ne bouge pas : le curseur, la
// sélection et le défilement restent où ils sont.

import type { Node } from "prosemirror-model";
import { type EditorState, Plugin, PluginKey } from "prosemirror-state";
import { Decoration, DecorationSet, type EditorView } from "prosemirror-view";
import { lireTitre } from "./image";

/** La largeur d'un dessin (SmartArt, Mermaid, schéma réseau, bloc d'extension), comme en lecture. */
export const LARGEUR_DESSIN = 640;

/** Ce que la feuille laisse au moins de chaque côté dans la vue : ses marges (8 et 12 pixels), arrondies à la plus grande. */
const MARGE_DE_LA_VUE = 12;

/** La classe d'un bloc qui déborde ; son débord de chaque côté est dans « --debord-bloc ». */
export const CLASSE_LARGE = "bloc-large";

/** Une largeur donnée par l'application (« 680px ») ; rien pour « none », une valeur vide ou illisible. */
export function lireLargeur(valeur: string | null | undefined): number | null {
  const lue = /^\s*([\d.]+)px\s*$/.exec(valeur ?? "");
  const nombre = lue ? Number(lue[1]) : Number.NaN;
  return Number.isFinite(nombre) && nombre > 0 ? nombre : null;
}

/**
 * Ce qu'un bloc peut prendre au plus de chaque côté du texte : de quoi atteindre la feuille de la colonne « Large », sans
 * sortir de la vue (ses marges comprises). Zéro quand l'une des largeurs manque, ou que la vue n'a que la place du texte.
 * @param vue La largeur de la vue.
 * @param fiche La largeur de la feuille, bords compris (« --largeur-fiche »).
 * @param large Celle de la feuille de la colonne « Large » (« --largeur-large »).
 */
export function debordPermis(vue: number, fiche: number | null, large: number | null): number {
  if (fiche === null || large === null) return 0;
  return Math.max(0, Math.floor((Math.min(large, vue - 2 * MARGE_DE_LA_VUE) - fiche) / 2));
}

/** Le débord d'un bloc de chaque côté : la moitié de ce qu'il demande de plus que le texte, dans ce qui est permis. */
export function debordDuBloc(voulue: number, colonne: number, permis: number): number {
  return voulue > colonne && permis > 0 ? Math.min(permis, Math.ceil((voulue - colonne) / 2)) : 0;
}

/** Un morceau d'un paragraphe : une image (sa largeur à l'écran, ou rien si elle a une largeur choisie), un blanc, ou autre chose. */
export type Morceau = { image: number | null } | "blanc" | "autre";

/**
 * La largeur que demande un paragraphe qui n'a que des images : la somme des leurs. Zéro s'il a du texte, une image à la
 * largeur choisie (comptée sur celle du texte) ou pas encore lue : il passe à la ligne dans la colonne, comme du texte.
 */
export function largeurDesImages(morceaux: readonly Morceau[]): number {
  let largeur = 0;
  for (const morceau of morceaux) {
    if (morceau === "blanc") continue;
    if (morceau === "autre" || morceau.image === null || morceau.image <= 0) return 0;
    largeur += morceau.image;
  }
  return largeur;
}

/**
 * La largeur du texte autour d'un élément : celle de son bloc, moins ce qu'il déborde quand c'est un bloc large (une image
 * seule sur sa ligne se règle toujours en pourcentage de la colonne de texte).
 */
export function largeurAutour(bloc: HTMLElement | null): number {
  if (!bloc) return 0;
  const largeur = bloc.getBoundingClientRect().width;
  if (!bloc.classList.contains(CLASSE_LARGE)) return largeur;
  return largeur - 2 * (Number.parseFloat(bloc.style.getPropertyValue("--debord-bloc")) || 0);
}

interface Mesure {
  /** La place permise et les débords retenus, bloc par bloc : une mesure identique ne change rien. */
  empreinte: string;
  /** Ce qu'un bloc peut prendre de chaque côté du texte. */
  permis: number;
  /** Le plus grand débord : la feuille s'élargit d'autant de chaque côté. */
  debord: number;
  decorations: DecorationSet;
}

const cle = new PluginKey<Mesure>("blocsLarges");

/** La place qu'un bloc avait de chaque côté du texte à la dernière mesure : les mots entiers des tableaux la suivent. */
export const permisDe = (etat: EditorState): number => cle.getState(etat)?.permis ?? 0;

const variable = (nom: string) => getComputedStyle(document.documentElement).getPropertyValue(nom);

/** Ce qu'un bloc peut prendre de chaque côté du texte, dans la page telle qu'elle est ; zéro en pages ou sur un papier. */
export function placeDeChaqueCote(): number {
  const racine = document.documentElement;
  if (racine.classList.contains("en-pages") || racine.hasAttribute("data-papier")) return 0;
  return debordPermis(racine.clientWidth, lireLargeur(variable("--largeur-fiche")), lireLargeur(variable("--largeur-large")));
}

/** La largeur de la colonne de texte : celle de l'éditeur, sans ses marges intérieures. */
export function largeurDuTexte(vue: EditorView): number {
  const style = getComputedStyle(vue.dom);
  return vue.dom.clientWidth - (Number.parseFloat(style.paddingLeft) || 0) - (Number.parseFloat(style.paddingRight) || 0);
}

/** La largeur d'un tableau sur une ligne : chaque case à sa plus longue ligne, comme en lecture. */
function largeurNaturelle(tableau: HTMLTableElement, sonde: HTMLElement): number {
  const copie = tableau.cloneNode(true) as HTMLTableElement;
  copie.classList.remove(CLASSE_LARGE);
  copie.style.cssText = "width: max-content; max-width: none; margin: 0";
  sonde.replaceChildren(copie);
  return copie.getBoundingClientRect().width;
}

/** La largeur d'une image à l'écran sans réglage : la boîte d'une image rognée, ou les pixels du fichier. */
function largeurDeLImage(noeud: Node, dom: globalThis.Node | null): number | null {
  if (lireTitre(noeud.attrs.title as string | null)?.largeur != null) return null;
  if (!(dom instanceof HTMLElement)) return null;
  const boite = Number.parseFloat(dom.style.width);
  if (dom.style.width.endsWith("px") && boite > 0) return boite;
  const image = dom.querySelector("img");
  return image?.complete ? image.naturalWidth : null;
}

/** La largeur que demande un bloc de la fiche ; zéro pour du texte, qui passe à la ligne dans sa colonne. */
function largeurVoulue(vue: EditorView, noeud: Node, position: number, sonde: HTMLElement): number {
  const dom = vue.nodeDOM(position);
  switch (noeud.type.name) {
    case "table":
      return dom instanceof HTMLTableElement ? largeurNaturelle(dom, sonde) : 0;
    case "code_block":
      return dom instanceof HTMLElement && dom.classList.contains("bloc-dessin") && !dom.classList.contains("dessin-impossible")
        ? LARGEUR_DESSIN
        : 0;
    case "paragraph": {
      if (noeud.childCount === 0) return 0;
      const morceaux: Morceau[] = [];
      noeud.forEach((enfant, decalage) => {
        if (enfant.type.name === "image") morceaux.push({ image: largeurDeLImage(enfant, vue.nodeDOM(position + 1 + decalage)) });
        else if (enfant.type.name === "hard_break" || (enfant.isText && (enfant.text ?? "").trim() === "")) morceaux.push("blanc");
        else morceaux.push("autre");
      });
      return largeurDesImages(morceaux);
    }
    default:
      return 0;
  }
}

function mesurer(vue: EditorView): Mesure {
  const permis = placeDeChaqueCote();
  const decorations: Decoration[] = [];
  const empreintes: string[] = [];
  let debord = 0;
  if (permis > 0) {
    const colonne = largeurDuTexte(vue);
    // La sonde : hors du flux (rien ne bouge, rien ne défile), avec les styles du texte de la fiche.
    const sonde = document.createElement("div");
    sonde.className = "contenu-fiche editeur-visuel sonde-des-blocs";
    sonde.setAttribute("aria-hidden", "true");
    vue.dom.parentElement?.append(sonde);
    vue.state.doc.forEach((noeud, position) => {
      const cote = debordDuBloc(largeurVoulue(vue, noeud, position, sonde), colonne, permis);
      if (cote <= 0) return;
      debord = Math.max(debord, cote);
      decorations.push(Decoration.node(position, position + noeud.nodeSize, { class: CLASSE_LARGE, style: `--debord-bloc: ${cote}px` }));
      empreintes.push(`${position}:${cote}`);
    });
    sonde.remove();
  }
  return { empreinte: `${permis}|${empreintes.join(";")}`, permis, debord, decorations: DecorationSet.create(vue.state.doc, decorations) };
}

/** La feuille s'élargit du plus grand débord, de chaque côté : le texte garde sa place, au milieu. */
function elargirLaFeuille(vue: EditorView, debord: number): void {
  const feuille = vue.dom.parentElement;
  if (!feuille) return;
  feuille.classList.toggle("avec-debord", debord > 0);
  if (debord > 0) feuille.style.setProperty("--debord", `${debord}px`);
  else feuille.style.removeProperty("--debord");
}

/**
 * Mesure les blocs après chaque changement du texte, quand la page ou la colonne change de largeur, quand une image arrive
 * ou qu'un dessin paraît, et quand la page passe en pages ou sur un papier.
 */
export function blocsLarges(): Plugin<Mesure> {
  return new Plugin<Mesure>({
    key: cle,
    state: {
      init: () => ({ empreinte: "", permis: 0, debord: 0, decorations: DecorationSet.empty }),
      apply: (tr, avant) =>
        (tr.getMeta(cle) as Mesure | undefined) ?? { ...avant, decorations: avant.decorations.map(tr.mapping, tr.doc) },
    },
    props: {
      decorations: (etat) => cle.getState(etat)?.decorations,
    },
    view: (vue) => {
      let attente = 0;
      const ajuster = () => {
        attente = 0;
        const mesure = mesurer(vue);
        elargirLaFeuille(vue, mesure.debord);
        if (mesure.empreinte !== cle.getState(vue.state)?.empreinte) vue.dispatch(vue.state.tr.setMeta(cle, mesure));
      };
      const planifier = () => {
        if (!attente) attente = requestAnimationFrame(ajuster);
      };
      // Une image ne dit sa largeur qu'une fois lue : son chargement ne remonte pas, il se capte en descendant.
      const chargee = (evenement: Event) => {
        if (evenement.target instanceof HTMLImageElement) planifier();
      };
      const colonne = new ResizeObserver(planifier);
      colonne.observe(vue.dom);
      const racine = new MutationObserver(planifier);
      racine.observe(document.documentElement, { attributes: true, attributeFilter: ["class", "data-papier"] });
      window.addEventListener("resize", planifier);
      document.addEventListener("theme", planifier);
      vue.dom.addEventListener("load", chargee, true);
      vue.dom.addEventListener("dessin", planifier);
      void document.fonts.ready.then(planifier);
      planifier();
      return {
        update: (apres, avant) => {
          if (!apres.state.doc.eq(avant.doc)) planifier();
        },
        destroy: () => {
          cancelAnimationFrame(attente);
          colonne.disconnect();
          racine.disconnect();
          window.removeEventListener("resize", planifier);
          document.removeEventListener("theme", planifier);
          vue.dom.removeEventListener("load", chargee, true);
          vue.dom.removeEventListener("dessin", planifier);
          elargirLaFeuille(vue, 0);
        },
      };
    },
  });
}
