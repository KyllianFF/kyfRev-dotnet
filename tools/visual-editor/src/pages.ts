// Les pages de la fiche, comme dans un traitement de texte : la feuille a une largeur, une hauteur et des marges, et le
// texte s'y range page après page. Rien n'est écrit dans le document : la page rendue est mesurée, et des décorations
// ouvrent les espaces (pagination.ts dit lesquels). Le Markdown est le même avec ou sans pages.
//
// Sur un papier réglé, le texte s'écrit sur les lignes (lignes.ts) : la même mesure, avec ou sans pages, dit d'où part
// le dessin du papier (le pied des lettres, mesuré dans la vraie police), de combien décaler les titres, et cale les
// blocs qui gardent leur hauteur (tableau, image, code…) pour que la suite reprenne sur une ligne.

import type { Node } from "prosemirror-model";
import { type EditorState, Plugin, PluginKey } from "prosemirror-state";
import { Decoration, DecorationSet, type EditorView } from "prosemirror-view";
import { caler, GRAIN, leveeDeTitre, lignesDeTitre, phaseDuPapier } from "./lignes";
import { type Boite, ECART, type Geometrie, type Genre, hauteurUtile, type Pagination, paginer } from "./pagination";

/** La feuille demandée par l'application, en pixels CSS au zoom 1. */
export type Feuille = { largeur: number; hauteur: number; marge: number };

/** En dessous, une page n'aurait pas de quoi tenir une ligne : la feuille reste sans fin. */
const UTILE_MINIMAL = 48;

/** Après un espace ouvert, la page est remesurée : les marges des voisins s'y ajoutent, et il se corrige. */
const TOURS = 4;

/** Sans image à dessiner (fenêtre cachée), la mesure n'attend pas plus que cela. */
const DELAI_SANS_ECRAN = 250;

/** Deux mesures à moins de cela sont les mêmes : pas de nouvelle décoration pour une fraction de pixel. */
const MEME = 0.5;

/** Le nombre de fois où une autre largeur de texte peut relancer une mesure qui ne s'accordait plus, entre deux frappes. */
const RELANCES = 3;

/** Une mesure ramenée au grain du moteur, qui range la page au soixante-quatrième de pixel. */
const auGrain = (mesure: number) => Math.floor(mesure * GRAIN) / GRAIN;

/** La feuille tenue par l'éditeur : elle survit au chargement d'une autre fiche, et dit à l'hôte le nombre de pages. */
export class MiseEnPage {
  feuille: Feuille | null = null;
  /** Le pas des lignes d'écriture du papier, en pixels ; 0 : le texte ne suit aucune ligne. */
  ligne = 0;
  private annonce = -1;

  constructor(private readonly onpages: (pages: number) => void) {}

  /** Vrai si la feuille change. Une largeur nulle : la feuille sans fin. */
  regler(large: number, haute: number, bord: number): boolean {
    // Au grain du moteur, comme le pas des lignes : d'une page à l'autre, le texte et sa feuille ne s'écartent pas.
    const [largeur, hauteur, marge] = [auGrain(large), auGrain(haute), auGrain(bord)];
    const valable = largeur > 2 * marge && hauteur - 2 * marge >= UTILE_MINIMAL && marge >= 0;
    const feuille = valable ? { largeur, hauteur, marge } : null;
    const avant = this.feuille;
    if (
      (feuille === null && avant === null) ||
      (feuille && avant && feuille.largeur === avant.largeur && feuille.hauteur === avant.hauteur && feuille.marge === avant.marge)
    ) {
      return false;
    }
    this.feuille = feuille;
    return true;
  }

  /** Vrai si le pas des lignes d'écriture change. Un pas nul : plus de lignes à suivre. */
  reglerLigne(pas: number): boolean {
    const ligne = Number.isFinite(pas) && pas > 0 ? pas : 0;
    if (ligne === this.ligne) return false;
    this.ligne = ligne;
    return true;
  }

  /** Le nombre de pages n'est dit que lorsqu'il change. */
  signaler(pages: number): void {
    if (pages === this.annonce) return;
    this.annonce = pages;
    this.onpages(pages);
  }
}

/** Ce que porte une décoration : de quoi la comparer à celle qu'il faudrait. */
type Marque =
  | { genre: "saut"; position: number; hauteur: number }
  | { genre: "code"; position: number; hauteur: number }
  | { genre: "ligne"; position: number; fin: number; hauteur: number }
  | { genre: "figure"; position: number; fin: number; haute: number; large: number }
  /** Sous un bloc, ce qu'il lui manque pour finir sur une ligne d'écriture ; `libre` : une image seule, sans ligne vide après elle. */
  | { genre: "cale"; position: number; fin: number; hauteur: number; libre: boolean };

type Meta = { marques: Marque[] };

const cle = new PluginKey<DecorationSet>("pages");

/** Un nœud qui en contient d'autres, à paginer un à un : une liste, un point, une citation. */
const CONTENANTS = new Set(["bullet_list", "ordered_list", "list_item", "blockquote"]);

function decoration(marque: Marque): Decoration {
  switch (marque.genre) {
    case "saut":
      return Decoration.widget(
        marque.position,
        () => {
          const espace = document.createElement("div");
          espace.className = "saut-de-page";
          espace.style.height = `${marque.hauteur}px`;
          return espace;
        },
        { side: -1, key: `saut-de-page-${marque.hauteur}`, ignoreSelection: true, marque },
      );
    case "code":
      // Dans un bloc de code plus haut qu'une page : l'espace s'ouvre au début d'une ligne du code.
      return Decoration.widget(
        marque.position,
        () => {
          const espace = document.createElement("span");
          espace.className = "saut-de-code";
          espace.style.height = `${marque.hauteur}px`;
          return espace;
        },
        { side: -1, key: `saut-de-code-${marque.hauteur}`, ignoreSelection: true, marque },
      );
    case "ligne":
      return Decoration.node(
        marque.position,
        marque.fin,
        { class: "saut-de-ligne", style: `--saut: ${marque.hauteur}px` },
        { marque },
      );
    case "figure":
      return Decoration.node(
        marque.position,
        marque.fin,
        { class: "figure-de-page", style: `--figure-haute: ${marque.haute}px; --figure-large: ${marque.large}px` },
        { marque },
      );
    case "cale":
      return Decoration.node(
        marque.position,
        marque.fin,
        { class: marque.libre ? "cale-de-ligne bloc-libre" : "cale-de-ligne", style: `--cale: ${marque.hauteur}px` },
        { marque },
      );
  }
}

/** Les décorations en place, à leurs positions d'aujourd'hui. */
function marquesEnPlace(etat: EditorState): Marque[] {
  return (cle.getState(etat) ?? DecorationSet.empty).find().map((trouvee) => {
    const marque = (trouvee.spec as { marque: Marque }).marque;
    return marque.genre === "saut" || marque.genre === "code"
      ? { ...marque, position: trouvee.from }
      : { ...marque, position: trouvee.from, fin: trouvee.to };
  });
}

const valeurs = (marque: Marque) => (marque.genre === "figure" ? [marque.haute, marque.large] : [marque.hauteur]);

function memesMarques(unes: Marque[], autres: Marque[]): boolean {
  if (unes.length !== autres.length) return false;
  const rang = (a: Marque, b: Marque) => a.position - b.position || a.genre.localeCompare(b.genre);
  const a = [...unes].sort(rang);
  const b = [...autres].sort(rang);
  return a.every((marque, i) => {
    const autre = b[i] as Marque;
    if (marque.genre !== autre.genre || marque.position !== autre.position) return false;
    if (marque.genre === "cale" && autre.genre === "cale" && marque.libre !== autre.libre) return false;
    const [v, w] = [valeurs(marque), valeurs(autre)];
    return v.every((valeur, j) => Math.abs(valeur - (w[j] ?? 0)) <= MEME);
  });
}

// --- La mesure ------------------------------------------------------------------

/** Une boîte mesurée, et le nœud à décorer pour la pousser ou la réduire. */
type Mesuree = Boite & {
  /** Où ouvrir l'espace : avant le nœud lui-même, ou avant le contenant qu'il commence. */
  cible: number;
  /** Le nœud de la boîte et sa fin : une ligne de tableau, une figure. */
  position: number;
  fin: number;
  element: HTMLElement;
  /** Comment la pousser : un espace avant son bloc, en haut des cases de sa ligne, ou au début de sa ligne de code. */
  mode: "espace" | "ligne" | "code";
  /** Où commence et où finit ce qu'elle montre, sur l'écran, au moment de la mesure. */
  hautReel: number;
  basReel: number;
  /** La ligne d'en-tête de son tableau, à remontrer au-dessus d'elle si elle commence une page. */
  entete?: HTMLElement;
  /** Le bloc qu'elle termine, à caler sur une ligne d'écriture ; `libre` : une image seule dans son paragraphe. */
  cale?: { position: number; fin: number; libre: boolean };
};

const estElement = (noeud: unknown): noeud is HTMLElement => noeud instanceof HTMLElement;

/** Le voisin qui compte pour la mise en page : pas un autre ajout de l'éditeur (curseur de bloc, espace). */
function voisin(element: Element, sens: "previousElementSibling" | "nextElementSibling"): Element | null {
  let autre = element[sens];
  while (autre?.classList.contains("ProseMirror-widget") || autre?.classList.contains("ProseMirror-gapcursor")) {
    autre = autre[sens];
  }
  return autre;
}

/**
 * Ce qu'un espace ouvert décale vraiment. Sans lui, les marges des deux voisins se confondent (la plus grande) ; avec
 * lui, elles s'ajoutent : le décalage est sa hauteur plus la plus petite des deux.
 */
function decalageDeLEspace(espace: Element): { decalage: number; marges: number } {
  const boite = espace.getBoundingClientRect();
  const avant = voisin(espace, "previousElementSibling")?.getBoundingClientRect();
  const apres = voisin(espace, "nextElementSibling")?.getBoundingClientRect();
  if (!avant || !apres) return { decalage: boite.height, marges: 0 };
  const dessus = Math.max(0, boite.top - avant.bottom);
  const dessous = Math.max(0, apres.top - boite.bottom);
  return { decalage: apres.top - avant.bottom - Math.max(dessus, dessous), marges: Math.min(dessus, dessous) };
}

/** L'espace ouvert juste avant ce bloc, s'il y en a un. */
function espaceAvant(element: Element): Element | null {
  let autre = element.previousElementSibling;
  while (autre?.classList.contains("ProseMirror-widget")) {
    if (autre.classList.contains("saut-de-page")) return autre;
    autre = autre.previousElementSibling;
  }
  return null;
}

const sautDeLaLigne = (ligne: HTMLElement) => Number.parseFloat(ligne.style.getPropertyValue("--saut")) || 0;

/** La cale déjà posée sous ce bloc. */
const caleDe = (bloc: HTMLElement) => Number.parseFloat(bloc.style.getPropertyValue("--cale")) || 0;

/** Le dessin d'une figure : l'image dans son cadre, ou le schéma du bloc. */
function dessinDe(figure: HTMLElement): Element | null {
  const boite = figure.querySelector<HTMLElement>(".apercu-dessin > :is(svg, img), .image");
  // Une image entière : ses proportions sont les siennes, pas celles de sa boîte, qui compte aussi le bas de la ligne.
  // Une image rognée : celles de son cadre.
  if (!boite?.classList.contains("image") || boite.style.aspectRatio) return boite;
  return boite.querySelector("img") ?? boite;
}

/**
 * Les boîtes de la fiche, de haut en bas, à leur place naturelle : les espaces déjà ouverts et les cales déjà posées
 * sont retirés, pour que le calcul ne dépende pas de ce qu'il a produit la fois d'avant. `levees` : de combien chaque
 * niveau de titre est décalé à l'œil sur un papier réglé ; sa place dans la page est celle d'avant ce décalage.
 */
function relever(vue: EditorView, marge: number, utile: number, levees: readonly number[] = []): Mesuree[] {
  const racine = vue.dom;
  const origine = racine.getBoundingClientRect().top + marge;
  const espaces = [...racine.querySelectorAll<HTMLElement>(".saut-de-page, .saut-de-code, tr.saut-de-ligne")];
  let prochain = 0;
  let retire = 0;
  /** Compte les espaces ouverts avant cet élément. */
  const retirerAvant = (element: HTMLElement) => {
    for (; prochain < espaces.length; prochain++) {
      const espace = espaces[prochain] as HTMLElement;
      if (espace === element || !(espace.compareDocumentPosition(element) & globalThis.Node.DOCUMENT_POSITION_FOLLOWING)) break;
      // Une ligne poussée contient ses cases : elle n'est pas « avant » elles.
      if (espace.contains(element)) break;
      retire += decalageDe(espace);
    }
  };
  const decalageDe = (espace: HTMLElement) =>
    espace.tagName === "TR"
      ? sautDeLaLigne(espace)
      : espace.classList.contains("saut-de-code")
        ? espace.getBoundingClientRect().height
        : decalageDeLEspace(espace).decalage;

  const boites: Mesuree[] = [];
  const poser = (
    genre: Genre,
    noeud: Node,
    position: number,
    cible: number,
    element: HTMLElement,
    depuis: HTMLElement,
    propre = 0,
    entete: HTMLElement | null = null,
  ) => {
    retirerAvant(depuis);
    const levee = noeud.type.name === "heading" ? (levees[noeud.attrs.level as number] ?? 0) : 0;
    const haut = depuis.getBoundingClientRect().top - (depuis === element ? levee : 0);
    const bas = element.getBoundingClientRect().bottom - levee;
    boites.push({
      genre,
      ...(noeud.type.name === "saut_de_page" ? { saut: noeud.attrs.blanche ? ("blanche" as const) : ("page" as const) } : {}),
      ...(entete ? { entete, rappel: Math.max(0, entete.getBoundingClientRect().height - sautDeLaLigne(entete)) } : {}),
      haut: haut - origine - retire,
      hauteur: Math.max(0, bas - haut - propre),
      cible,
      position,
      fin: position + noeud.nodeSize,
      element,
      mode: genre === "ligne" ? "ligne" : "espace",
      // Le texte d'une ligne poussée commence sous l'espace ouvert dans ses cases.
      hautReel: genre === "ligne" ? haut + propre : haut,
      basReel: bas,
    });
  };

  /**
   * Un bloc de code plus haut qu'une page ne peut pas descendre entier : il se range ligne par ligne. Plus court, il
   * reste un bloc, poussé d'une pièce.
   */
  const poserCode = (noeud: Node, position: number, cible: number, element: HTMLElement, depuis: HTMLElement) => {
    const ouverts = [...element.querySelectorAll<HTMLElement>(".saut-de-code")];
    const dedans = ouverts.reduce((somme, espace) => somme + espace.getBoundingClientRect().height, 0);
    const cadre = element.getBoundingClientRect();
    const texte = noeud.textContent;
    if (cadre.height - dedans <= utile + MEME || !texte.includes("\n")) {
      poser("bloc", noeud, position, cible, element, depuis, dedans);
      return;
    }
    retirerAvant(depuis);
    let debut = position + 1;
    const lignes = texte.split("\n");
    lignes.forEach((ligne, rang) => {
      const fin = debut + ligne.length;
      let haut: number;
      let bas: number;
      try {
        const tete = vue.coordsAtPos(debut, 1);
        haut = tete.top;
        bas = ligne.length > 0 ? vue.coordsAtPos(fin, -1).bottom : tete.bottom;
      } catch {
        debut = fin + 1;
        return;
      }
      if (rang === 0) haut = depuis.getBoundingClientRect().top;
      else {
        // Les espaces ouverts plus haut dans ce bloc.
        for (; prochain < espaces.length; prochain++) {
          const espace = espaces[prochain] as HTMLElement;
          if (!element.contains(espace) || espace.getBoundingClientRect().bottom > haut + MEME) break;
          retire += decalageDe(espace);
        }
      }
      if (rang === lignes.length - 1) bas = Math.max(bas, cadre.bottom);
      boites.push({
        genre: "bloc",
        haut: haut - origine - retire,
        hauteur: Math.max(0, bas - haut),
        cible: rang === 0 ? cible : debut,
        position: debut,
        fin,
        element,
        mode: rang === 0 ? "espace" : "code",
        hautReel: haut,
        basReel: bas,
      });
      debut = fin + 1;
    });
  };
  /** Le bas d'un contenant (son trait, son retrait) appartient à sa dernière boîte. */
  const finir = (premiere: number, contenant: HTMLElement) => {
    const derniere = boites[boites.length - 1];
    if (!derniere || boites.length <= premiere) return;
    const bas = contenant.getBoundingClientRect().bottom - origine - retire;
    // « retire » compte déjà l'espace d'une dernière ligne poussée… sauf le sien, encore dans sa hauteur.
    const propre = derniere.element.tagName === "TR" ? sautDeLaLigne(derniere.element) : 0;
    derniere.hauteur = Math.max(derniere.hauteur, bas - propre - derniere.haut);
    derniere.basReel = Math.max(derniere.basReel, contenant.getBoundingClientRect().bottom);
  };
  /**
   * La dernière boîte d'un bloc porte son pied : là où il finit sans sa cale, qui est une place ajoutée sous lui, comme
   * un espace ouvert. La cale se pose sur le bloc entier (le tableau, pas sa dernière ligne).
   */
  const terminer = (premiere: number, noeud: Node, position: number, element: HTMLElement) => {
    const derniere = boites[boites.length - 1];
    if (!derniere || boites.length <= premiere) return;
    const posee = caleDe(element);
    derniere.hauteur = Math.max(0, derniere.hauteur - posee);
    derniere.basReel -= posee;
    derniere.pied = derniere.haut + derniere.hauteur;
    derniere.cale = { position, fin: position + noeud.nodeSize, libre: derniere.genre === "figure" && noeud.type.name === "paragraph" };
    retire += posee;
  };

  const visiter = (noeud: Node, position: number, cible: number) => {
    const element = vue.nodeDOM(position);
    const depuis = vue.nodeDOM(cible);
    if (!estElement(element) || !estElement(depuis)) return;
    const premiere = boites.length;
    if (CONTENANTS.has(noeud.type.name)) {
      noeud.forEach((enfant, decalage, rang) => {
        const ici = position + 1 + decalage;
        visiter(enfant, ici, rang === 0 ? cible : ici);
      });
      finir(premiere, element);
      return;
    }
    if (noeud.type.spec.tableRole === "table") {
      // La ligne d'en-tête ne reste pas seule en bas d'une page : elle fait bloc avec la ligne qui la suit, et se
      // remontre en haut de chaque page où le tableau continue.
      const avecEntete = /ligne/.test(String(noeud.attrs.entete ?? "ligne")) && noeud.childCount > 1;
      const tete = vue.nodeDOM(position + 1);
      const entete = avecEntete && estElement(tete) ? tete : null;
      noeud.forEach((ligne, decalage, rang) => {
        const ici = position + 1 + decalage;
        const rangee = vue.nodeDOM(ici);
        if (!estElement(rangee)) return;
        if (rang === 0 && entete) return;
        const propre = sautDeLaLigne(rangee) + (rang === 1 && entete ? sautDeLaLigne(entete) : 0);
        // La première ligne, c'est le tableau : il descend entier, sans laisser ses traits sur la page d'avant.
        if (rang === 0 || (rang === 1 && entete)) poser("bloc", noeud, position, cible, rangee, depuis, propre);
        else poser("ligne", ligne, ici, ici, rangee, rangee, propre, entete);
      });
      finir(premiere, element);
    } else if (noeud.type.name === "code_block" && !estFigure(noeud, element)) {
      poserCode(noeud, position, cible, element, depuis);
    } else {
      poser(estFigure(noeud, element) ? "figure" : "bloc", noeud, position, cible, element, depuis);
    }
    terminer(premiere, noeud, position, element);
  };
  vue.state.doc.forEach((noeud, position) => visiter(noeud, position, position));
  return boites;
}

/** Une image seule dans son paragraphe, ou un bloc qui montre son dessin : cela ne se coupe pas, et se réduit. */
function estFigure(noeud: Node, element: HTMLElement): boolean {
  if (noeud.type.name === "paragraph") return noeud.childCount === 1 && noeud.firstChild?.type.name === "image";
  return (
    noeud.type.name === "code_block" &&
    element.classList.contains("bloc-dessin") &&
    !element.classList.contains("en-edition") &&
    !element.classList.contains("dessin-impossible")
  );
}

/** La taille à laquelle une figure tient dans le texte d'une page, à ses proportions ; rien tant qu'elle n'a pas de taille. */
function reduction(figure: Mesuree, utile: number): Marque | null {
  const dessin = dessinDe(figure.element)?.getBoundingClientRect();
  if (!dessin || dessin.height < 1 || dessin.width < 1) return null;
  // Ce que le bloc ajoute autour du dessin (marges, ligne de texte) compte dans la page.
  const autour = Math.max(0, figure.element.getBoundingClientRect().height - dessin.height);
  let haute = Math.max(UTILE_MINIMAL / 2, Math.floor(utile - autour));
  let large = Math.floor((haute * dessin.width) / dessin.height);
  // Déjà réduite à un pixel près : la même, pour ne pas la reprendre à chaque mesure.
  const deja = Number.parseFloat(figure.element.style.getPropertyValue("--figure-haute"));
  const dejaLarge = Number.parseFloat(figure.element.style.getPropertyValue("--figure-large"));
  if (Math.abs(deja - haute) <= 1.5 && Math.abs(dejaLarge - large) <= 1.5) [haute, large] = [deja, dejaLarge];
  return { genre: "figure", position: figure.position, fin: figure.fin, haute, large };
}

// --- Les coupes ------------------------------------------------------------------

/** Ce qui se dessine devant le texte, là où un bloc continue d'une page à l'autre. */
type Piece = { classe: string; style: Record<string, string>; texte?: string; contenu?: string; clone?: HTMLElement };

const demi = (valeur: number) => Math.round(valeur * 2) / 2;
const px = (valeur: number) => `${demi(valeur)}px`;

/** La ligne d'en-tête d'un tableau, recopiée pour être remontrée : aux mêmes largeurs, hors du document. */
function copieDEntete(entete: HTMLElement): HTMLElement | null {
  const tableau = entete.closest("table");
  if (!tableau) return null;
  const copie = tableau.cloneNode(false) as HTMLElement;
  copie.removeAttribute("class");
  copie.setAttribute("style", `width: ${px(tableau.getBoundingClientRect().width)}; margin: 0; table-layout: fixed;`);
  const ligne = entete.cloneNode(true) as HTMLElement;
  ligne.removeAttribute("class");
  ligne.removeAttribute("style");
  [...ligne.children].forEach((cellule, rang) => {
    const largeur = entete.children[rang]?.getBoundingClientRect().width ?? 0;
    cellule.removeAttribute("class");
    cellule.setAttribute("style", `${cellule.getAttribute("style") ?? ""}; box-sizing: border-box; width: ${px(largeur)}; min-width: 0;`);
  });
  for (const ajout of ligne.querySelectorAll(".ProseMirror-widget, .ProseMirror-trailingBreak, .ProseMirror-separator")) ajout.remove();
  const corps = document.createElement("tbody");
  corps.append(ligne);
  copie.append(corps);
  return copie;
}

/**
 * Un bloc coupé se ferme à sa dernière ligne de la page et se rouvre à la première de la suivante : entre les deux, le
 * papier et le bureau sont redessinés par-dessus ce qui traverse (fond d'une citation, traits d'un tableau, fond d'un
 * code). Un tableau à en-tête la remontre en haut de la page ; les autres disent « suite », dans la marge, au ras du texte.
 */
function coupes(vue: EditorView, parent: HTMLElement, boites: Mesuree[], resultat: Pagination, feuille: Feuille): Piece[] {
  const pieces: Piece[] = [];
  const base = parent.getBoundingClientRect();
  const pas = feuille.hauteur + ECART;
  for (const saut of resultat.sauts) {
    const boite = boites[saut.indice] as Mesuree;
    const avant = boites[saut.indice - 1];
    if (!avant) continue;
    let citation = false;
    if (boite.mode === "espace") {
      const cible = vue.nodeDOM(boite.cible);
      citation = estElement(cible) && cible.parentElement?.closest("blockquote") != null && vue.dom.contains(cible.parentElement);
      if (!citation) continue;
    }
    const pageDAvant = (saut.page - 1) * pas;
    const pageDApres = saut.page * pas;
    const finDuTexte = pageDAvant + feuille.hauteur - feuille.marge;
    let fin = avant.basReel - base.top;
    // Un tableau garde son trait du bas ; une citation, un code gardent un peu de fond sous leur dernière ligne, sans
    // jamais entrer dans la marge.
    fin += boite.mode === "ligne" ? 1 : Math.min(4, Math.max(0, finDuTexte - fin));
    const debut = boite.hautReel - base.top;
    const bas = pageDAvant + feuille.hauteur;
    // « --dy » : d'où part le dessin du papier, pour qu'il continue celui de la feuille sous le masque.
    // Compté depuis la place arrondie du masque, sans arrondir à son tour : ses lignes sont celles de la feuille.
    if (fin < bas) pieces.push({ classe: "masque bas", style: { top: px(fin), height: px(bas - fin), "--dy": `${pageDAvant - demi(fin)}px` } });
    if (debut > pageDApres) {
      pieces.push({ classe: "masque haut", style: { top: px(pageDApres), height: px(debut - pageDApres), "--dy": `${pageDApres - demi(pageDApres)}px` } });
    }
    if (boite.mode === "ligne") {
      const tableau = boite.element.closest("table")?.getBoundingClientRect();
      const place = tableau ? { left: px(tableau.left - base.left), width: px(tableau.width) } : null;
      const copie = boite.entete ? copieDEntete(boite.entete) : null;
      if (copie && place && boite.entete) {
        const hauteur = boite.entete.getBoundingClientRect().height - sautDeLaLigne(boite.entete);
        pieces.push({
          classe: "rappel-entete contenu-fiche editeur-visuel",
          style: { top: px(debut - hauteur), ...place },
          contenu: copie.outerHTML,
          clone: copie,
        });
        continue;
      }
      // Sans en-tête à remontrer, la ligne retrouve au moins son trait du haut, celui de ses cases.
      const cellule = boite.element.firstElementChild;
      const trait = cellule ? getComputedStyle(cellule) : null;
      if (trait && place && Number.parseFloat(trait.borderBottomWidth) > 0) {
        pieces.push({
          classe: "trait-de-tableau",
          style: { top: px(debut - 1), ...place, "border-top": `${trait.borderBottomWidth} ${trait.borderBottomStyle} ${trait.borderBottomColor}` },
        });
      }
    }
    pieces.push({ classe: "suite fin", style: { top: px(fin + 2) }, texte: "suite page suivante" });
    pieces.push({ classe: "suite debut", style: { top: px(debut - 20) }, texte: "suite" });
  }
  return pieces;
}

// --- Les lignes d'écriture ---------------------------------------------------------

/** Les niveaux de titre. */
const NIVEAUX = [1, 2, 3, 4, 5, 6] as const;

/**
 * La sonde : une ligne de texte et un titre de chaque niveau, hors du document, dans la police et aux tailles de la
 * fiche. Elle dit où tombe le pied des lettres dans une ligne, plutôt que de le deviner d'après la police.
 */
function creerSonde(): HTMLElement {
  const sonde = document.createElement("div");
  sonde.className = "contenu-fiche sonde-de-ligne";
  sonde.setAttribute("aria-hidden", "true");
  for (const balise of ["p", ...NIVEAUX.map((niveau) => `h${niveau}`)]) {
    const ligne = document.createElement(balise);
    // Une boîte vide posée sur la ligne de base : son bas est le pied des lettres.
    ligne.append(document.createElement("span"), "x");
    sonde.append(ligne);
  }
  return sonde;
}

/** Du haut de la ligne au pied de ses lettres. */
function piedDe(ligne: Element): number {
  const repere = ligne.firstElementChild;
  return repere ? repere.getBoundingClientRect().bottom - ligne.getBoundingClientRect().top : 0;
}

// --- Le greffon ------------------------------------------------------------------

/**
 * Range la fiche en pages. La feuille de papier et l'écart entre deux pages sont dessinés autour de l'éditeur, dans son
 * parent ; dans le texte, seuls des espaces s'ouvrent : un bloc vide avant le bloc poussé, un espace en haut des cases
 * d'une ligne de tableau poussée, une taille maximale sur une figure trop haute.
 */
export function pages(mise: MiseEnPage): Plugin<DecorationSet> {
  return new Plugin<DecorationSet>({
    key: cle,
    state: {
      init: () => DecorationSet.empty,
      apply(tr, decorations) {
        const meta = tr.getMeta(cle) as Meta | undefined;
        if (meta) return DecorationSet.create(tr.doc, meta.marques.map(decoration));
        // Entre deux mesures, les espaces suivent leur bloc.
        return tr.docChanged ? decorations.map(tr.mapping, tr.doc) : decorations;
      },
    },
    props: {
      decorations: (etat) => cle.getState(etat),
    },
    view(vue) {
      const parent = vue.dom.parentElement as HTMLElement;
      const papiers = document.createElement("div");
      papiers.className = "papiers";
      const coupures = document.createElement("div");
      coupures.className = "coupures";
      let posee: Feuille | null = null;
      /** Le pas des lignes d'écriture en place, sa sonde, ce qu'elle a mesuré et les variables qui le disent à la page. */
      let lignePosee = 0;
      const sonde = creerSonde();
      let levees: number[] = [];
      const reglages = new Map<string, string>();
      let attente = 0;
      let secours: ReturnType<typeof setTimeout> | undefined;
      let detruit = false;
      /** Des mesures qui ne s'accordent pas d'une fois sur l'autre : on s'en tient là jusqu'au prochain changement. */
      let desaccords = 0;
      /** Ce qui est dessiné devant le texte, pour ne le refaire que s'il change. */
      let dessine = "";
      let dernieresPieces: Piece[] = [];

      const planifier = () => {
        if (attente || detruit) return;
        const faire = () => {
          cancelAnimationFrame(attente);
          clearTimeout(secours);
          attente = 0;
          mesurer();
        };
        attente = requestAnimationFrame(faire);
        // Une page qui n'est pas à l'écran ne reçoit pas d'image à dessiner : la mesure se fait quand même, un peu plus tard.
        secours = setTimeout(faire, DELAI_SANS_ECRAN);
      };

      /** Une variable de la fiche, écrite seulement si elle change. */
      const regler = (nom: string, valeur: string) => {
        if (reglages.get(nom) === valeur) return;
        reglages.set(nom, valeur);
        parent.style.setProperty(nom, valeur);
      };

      /** Les lignes d'écriture sur la page : le pas en variable, la sonde à côté du texte ; sans lignes, plus rien. */
      const poserLignes = () => {
        const ligne = mise.ligne;
        if (ligne === lignePosee) return;
        lignePosee = ligne;
        const racine = document.documentElement;
        racine.classList.toggle("sur-lignes", ligne > 0);
        if (ligne > 0) {
          racine.style.setProperty("--pas", `${ligne}px`);
          parent.append(sonde);
          return;
        }
        racine.style.removeProperty("--pas");
        sonde.remove();
        for (const nom of reglages.keys()) parent.style.removeProperty(nom);
        reglages.clear();
        levees = [];
        if (parent.getAttribute("style") === "") parent.removeAttribute("style");
        if (racine.getAttribute("style") === "") racine.removeAttribute("style");
        if (racine.getAttribute("class") === "") racine.removeAttribute("class");
      };

      /**
       * Ce que la police demande pour écrire sur les lignes : combien de lignes prend chaque niveau de titre, de combien
       * le décaler pour que ses lettres s'y posent, et d'où part le dessin du papier pour que sa ligne d'écriture passe
       * au pied des lettres du texte. Mesuré à chaque fois : la police, sa taille ou le thème ont pu changer.
       */
      const sonder = (ligne: number, marge: number) => {
        const [texte, ...titres] = [...sonde.children];
        if (!texte) return;
        const lignes = titres.map((titre) => lignesDeTitre(Number.parseFloat(getComputedStyle(titre).fontSize), ligne));
        lignes.forEach((nombre, rang) => regler(`--lignes-${rang + 1}`, String(nombre)));
        const pied = piedDe(texte);
        levees = [0, ...titres.map((titre, rang) => leveeDeTitre(pied, piedDe(titre), lignes[rang] ?? 1, ligne))];
        NIVEAUX.forEach((niveau) => regler(`--levee-${niveau}`, `${(levees[niveau] ?? 0).toFixed(3)}px`));
        // Le dessin part du coin de la feuille : en pages, le texte commence une marge plus bas ; sans pages, là où il est.
        const haut = vue.dom.getBoundingClientRect().top + marge - parent.getBoundingClientRect().top;
        regler("--phase", `${phaseDuPapier(haut, pied, ligne).toFixed(3)}px`);
      };

      /** La feuille sur la page : ses mesures en variables, le papier derrière le texte, les coupures devant. */
      const poser = () => {
        poserLignes();
        const feuille = mise.feuille;
        if (feuille === posee) return;
        posee = feuille;
        const variables = { "--page-largeur": feuille?.largeur, "--page-hauteur": feuille?.hauteur, "--page-marge": feuille?.marge };
        for (const [nom, valeur] of Object.entries(variables)) {
          if (valeur === undefined) parent.style.removeProperty(nom);
          else parent.style.setProperty(nom, `${valeur}px`);
        }
        parent.classList.toggle("en-pages", feuille !== null);
        document.documentElement.classList.toggle("en-pages", feuille !== null);
        // Une autre feuille : les papiers ne sont plus aux mêmes places.
        papiers.replaceChildren();
        coupures.replaceChildren();
        dessine = "";
        dernieresPieces = [];
        if (feuille) {
          parent.insertBefore(papiers, vue.dom);
          parent.append(coupures);
          dessiner(1, [], []);
        } else {
          papiers.remove();
          coupures.remove();
          parent.style.removeProperty("--pages");
          if (parent.getAttribute("style") === "") parent.removeAttribute("style");
          if (parent.getAttribute("class") === "") parent.removeAttribute("class");
          if (document.documentElement.getAttribute("class") === "") document.documentElement.removeAttribute("class");
          mise.signaler(0);
        }
      };

      /**
       * Une feuille par page ; sur l'écart où un bloc a été poussé, une bande cache ce qui le traverse ; là où un bloc
       * continue d'une page à l'autre, ses coupes. Rien n'est refait si rien n'a changé.
       */
      const dessiner = (nombre: number, coupees: number[], pieces: Piece[] | null = []) => {
        const feuille = mise.feuille;
        if (!feuille) return;
        const pas = feuille.hauteur + ECART;
        parent.style.setProperty("--pages", String(nombre));
        while (papiers.childElementCount > nombre) papiers.lastElementChild?.remove();
        while (papiers.childElementCount < nombre) {
          const papier = document.createElement("div");
          papier.className = "papier";
          papier.style.top = `${papiers.childElementCount * pas}px`;
          papiers.append(papier);
        }
        if (pieces) dernieresPieces = pieces;
        const empreinte = `${coupees.join(",")}|${JSON.stringify(dernieresPieces, (nom, valeur: unknown) => (nom === "clone" ? undefined : valeur))}`;
        if (empreinte === dessine) return;
        dessine = empreinte;
        coupures.replaceChildren(
          ...dernieresPieces
            .filter((piece) => piece.classe.startsWith("masque"))
            .map(element),
          ...coupees.map((page) => {
            const bande = document.createElement("div");
            bande.className = "coupure";
            // Un pixel de chaque côté : le bord des deux feuilles, redessiné par-dessus.
            bande.style.top = `${page * pas - ECART - 1}px`;
            return bande;
          }),
          ...dernieresPieces.filter((piece) => !piece.classe.startsWith("masque")).map(element),
        );
      };

      const element = (piece: Piece) => {
        const boite = document.createElement("div");
        boite.className = piece.classe;
        boite.setAttribute("aria-hidden", "true");
        for (const [nom, valeur] of Object.entries(piece.style)) boite.style.setProperty(nom, valeur);
        if (piece.texte) boite.textContent = piece.texte;
        if (piece.clone) boite.append(piece.clone);
        return boite;
      };

      const mesurer = () => {
        const feuille = mise.feuille;
        const ligne = mise.ligne;
        // Sans pages ni lignes d'écriture, rien ne se mesure : la fiche suit son cours.
        if (detruit || (!feuille && ligne <= 0)) return;
        // Une saisie en cours de composition ne supporte pas qu'on redessine sous elle : la mesure attend sa fin.
        if (vue.composing) return;
        if (ligne > 0) sonder(ligne, feuille?.marge ?? 0);
        const geometrie: Geometrie | null = feuille ? { hauteur: feuille.hauteur, marge: feuille.marge, ecart: ECART, ligne } : null;
        const utile = geometrie ? hauteurUtile(geometrie) : Number.POSITIVE_INFINITY;
        let nombre = 1;
        let coupees: number[] = [];
        let pieces: Piece[] | null = null;
        let accord = false;
        for (let tour = 0; tour < TOURS && desaccords < 2; tour++) {
          const boites = relever(vue, feuille?.marge ?? 0, utile, levees);
          // Sans pages, il n'y a que les cales : les lignes se comptent d'un bout à l'autre de la fiche.
          const resultat: Pagination = geometrie
            ? paginer(boites, geometrie)
            : { sauts: [], aReduire: [], pages: 1, coupees: [], cales: caler(boites, ligne) };
          nombre = resultat.pages;
          coupees = resultat.coupees;
          const marques: Marque[] = [];
          for (const saut of resultat.sauts) {
            const boite = boites[saut.indice] as Mesuree;
            if (boite.mode === "ligne") {
              marques.push({ genre: "ligne", position: boite.position, fin: boite.fin, hauteur: saut.decalage });
              continue;
            }
            if (boite.mode === "code") {
              marques.push({ genre: "code", position: boite.cible, hauteur: saut.decalage });
              continue;
            }
            // Les marges des voisins s'ajoutent à l'espace : elles ne se mesurent qu'une fois l'espace en place.
            const cible = vue.nodeDOM(boite.cible);
            const espace = estElement(cible) ? espaceAvant(cible) : null;
            const marges = espace ? decalageDeLEspace(espace).marges : 0;
            marques.push({ genre: "saut", position: boite.cible, hauteur: Math.max(1, saut.decalage - marges) });
          }
          if (ligne > 0) {
            const manques = new Map(resultat.cales.map((ajout) => [ajout.indice, ajout.hauteur]));
            boites.forEach((boite, indice) => {
              const hauteur = manques.get(indice) ?? 0;
              // Une image seule reste marquée même sans cale : elle n'a pas la ligne vide d'un paragraphe après elle.
              if (boite.cale && (hauteur > 0 || boite.cale.libre)) marques.push({ genre: "cale", ...boite.cale, hauteur });
            });
          }
          const aReduire = new Set(resultat.aReduire);
          boites.forEach((boite, indice) => {
            if (boite.genre !== "figure") return;
            // Une figure réduite le reste : sa mesure ne dit plus qu'elle était trop haute.
            if (!aReduire.has(indice) && !boite.element.style.getPropertyValue("--figure-haute")) return;
            const marque = reduction(boite, utile);
            if (marque) marques.push(marque);
          });
          accord = memesMarques(marques, marquesEnPlace(vue.state));
          // La page est telle que mesurée : les coupes se dessinent d'après elle.
          if (accord && feuille) pieces = coupes(vue, parent, boites, resultat, feuille);
          if (accord) break;
          vue.dispatch(vue.state.tr.setMeta(cle, { marques } satisfies Meta).setMeta("addToHistory", false));
        }
        desaccords = accord ? 0 : desaccords + 1;
        if (!feuille || (!accord && desaccords > 2)) return;
        dessiner(nombre, coupees, pieces);
        mise.signaler(nombre);
      };

      // Une image qui arrive, un schéma dessiné, une police chargée : la page change sans que le document change.
      const observateur = new MutationObserver(planifier);
      observateur.observe(vue.dom, { subtree: true, childList: true, characterData: true, attributes: true });
      /** Un changement venu d'ailleurs que de la mesure (thème, police) : elle repart, même si elle s'en tenait là. */
      const repartir = () => {
        desaccords = 0;
        relances = RELANCES;
        planifier();
      };
      // Une autre largeur range le texte autrement : la mesure repart aussi, mais pas sans fin, car ses propres cales
      // peuvent faire paraître ou disparaître la barre de défilement, et changer la largeur à leur tour.
      let largeur = -1;
      let relances = RELANCES;
      const tailles = new ResizeObserver((entrees) => {
        const nouvelle = entrees[0]?.contentRect.width ?? largeur;
        if (Math.abs(nouvelle - largeur) > MEME && relances > 0) {
          relances--;
          desaccords = 0;
        }
        largeur = nouvelle;
        planifier();
      });
      tailles.observe(vue.dom);
      vue.dom.addEventListener("load", planifier, true);
      vue.dom.addEventListener("compositionend", planifier);
      // Le trait d'un tableau coupé est recopié de ses cases : un autre thème, d'autres couleurs. Et sur un papier réglé,
      // une autre police ou une autre taille déplacent le pied des lettres.
      document.addEventListener("theme", repartir);
      document.fonts.addEventListener("loadingdone", repartir);
      void document.fonts.ready.then(planifier);

      poser();
      planifier();
      return {
        update(_vue, avant) {
          const change = mise.feuille !== posee || mise.ligne !== lignePosee;
          if (change) poser();
          if (!change && avant.doc === vue.state.doc) return;
          desaccords = 0;
          relances = RELANCES;
          planifier();
        },
        destroy() {
          detruit = true;
          cancelAnimationFrame(attente);
          clearTimeout(secours);
          observateur.disconnect();
          tailles.disconnect();
          vue.dom.removeEventListener("load", planifier, true);
          vue.dom.removeEventListener("compositionend", planifier);
          document.removeEventListener("theme", repartir);
          document.fonts.removeEventListener("loadingdone", repartir);
          sonde.remove();
          papiers.remove();
          coupures.remove();
        },
      };
    },
  });
}

/**
 * Demande aux pages de se refaire : la feuille ou le pas des lignes d'écriture a changé. Les espaces et les cales
 * d'avant s'en vont tout de suite.
 */
export function refairePages(vue: EditorView): void {
  vue.dispatch(vue.state.tr.setMeta(cle, { marques: [] } satisfies Meta).setMeta("addToHistory", false));
}
