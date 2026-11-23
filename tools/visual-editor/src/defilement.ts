// Le défilement accompagné (rangée 20D), comme en lecture : un clic dans le plan fait glisser la fiche jusqu'au titre
// au lieu de la faire sauter. La page a sa propre fenêtre : c'est elle qui glisse, sur la durée que l'application lui
// donne (« --duree-defilement », avec le thème). Durée nulle (animations coupées) : d'un coup.

import { dureeEnMs } from "./entree";

/** Au-delà d'un écran et demi, le trajet commence plus près : un long chemin ne défile pas devant les yeux. */
export const ECRANS_AU_PLUS = 1.5;

/** L'accélération puis la décélération du défilement de la lecture (CubicEase EaseInOut). */
export function adoucir(t: number): number {
  const u = Math.min(1, Math.max(0, t));
  return u < 0.5 ? 4 * u * u * u : 1 - Math.pow(-2 * u + 2, 3) / 2;
}

/** D'où part le trajet vers `cible` : d'où l'on est, ou d'un écran et demi avant elle si elle est plus loin. */
export function depart(depuis: number, cible: number, hauteurVue: number): number {
  const auPlus = Math.max(1, hauteurVue) * ECRANS_AU_PLUS;
  return Math.abs(cible - depuis) > auPlus ? cible - Math.sign(cible - depuis) * auPlus : depuis;
}

/** Ce qui défile autour de l'élément : le premier parent qui a une barre, sinon la page. */
function ascenseur(element: Element): HTMLElement {
  for (let parent = element.parentElement; parent !== null; parent = parent.parentElement) {
    const style = getComputedStyle(parent);
    if (/(auto|scroll)/.test(style.overflowY) && parent.scrollHeight > parent.clientHeight) {
      return parent;
    }
  }
  return (element.ownerDocument.scrollingElement as HTMLElement | null) ?? element.ownerDocument.documentElement;
}

/** Le trajet en cours, que le suivant ou la molette arrête là où il en est. */
let enCours = 0;

/**
 * Amène `element` en haut de ce qui le fait défiler, en glissant comme la lecture ; d'un coup si la durée est nulle.
 * La molette, une touche ou un clic arrêtent le trajet là où il en est.
 */
export function glisserJusqua(element: Element, page: Document = document): void {
  const zone = ascenseur(element);
  const marge = parseFloat(getComputedStyle(element).scrollMarginTop) || 0;
  const haut = zone === page.scrollingElement ? 0 : zone.getBoundingClientRect().top;
  const max = Math.max(0, zone.scrollHeight - zone.clientHeight);
  const cible = Math.min(max, Math.max(0, zone.scrollTop + element.getBoundingClientRect().top - haut - marge));
  const duree = dureeEnMs(getComputedStyle(page.documentElement).getPropertyValue("--duree-defilement"));
  const vue = page.defaultView;
  const trajet = ++enCours;
  if (duree <= 0 || vue === null || Math.abs(cible - zone.scrollTop) < 1) {
    zone.scrollTop = cible;
    return;
  }
  const depuis = depart(zone.scrollTop, cible, zone.clientHeight);
  const arreter = () => {
    enCours++;
  };
  for (const evenement of ["wheel", "keydown", "mousedown"]) {
    page.addEventListener(evenement, arreter, { once: true, capture: true });
  }
  const debut = vue.performance.now();
  const pas = (maintenant: number) => {
    if (trajet !== enCours) {
      return;
    }
    const t = (maintenant - debut) / duree;
    zone.scrollTop = depuis + (cible - depuis) * adoucir(t);
    if (t < 1) {
      vue.requestAnimationFrame(pas);
    } else {
      for (const evenement of ["wheel", "keydown", "mousedown"]) {
        page.removeEventListener(evenement, arreter, { capture: true });
      }
    }
  };
  zone.scrollTop = depuis;
  vue.requestAnimationFrame(pas);
}
