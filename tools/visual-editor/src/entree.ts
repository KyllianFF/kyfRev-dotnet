// L'entrée de la feuille, à chaque texte chargé. Le texte, lui, existait déjà à la lecture : il est là tout de suite,
// et c'est sa feuille (son fond, son bord, son ombre) qui vient l'habiller, en fondu derrière lui. La page a sa propre
// fenêtre, que l'application ne peut ni estomper ni faire glisser : c'est donc la page qui s'anime, sur la durée que
// l'application lui donne (« --duree-entree », avec le thème). Durée nulle : la feuille est là d'un coup.

/** La même décélération que les transitions de l'application. */
export const DECELERATION = "cubic-bezier(0.215, 0.61, 0.355, 1)";

/** La classe de la fiche tant que sa feuille n'est pas dessinée (page.css). */
export const CLASSE_NUE = "nue";

/** La feuille sans fin, avant d'être dessinée : ni fond, ni bord, ni ombre. */
const NUE = { backgroundColor: "transparent", borderColor: "transparent", boxShadow: "0 2px 12px transparent" };

/** Une durée CSS (« 220ms », « 0.22s ») en millisecondes ; 0 si elle est vide, illisible ou négative. */
export function dureeEnMs(valeur: string): number {
  const lue = /^\s*(\d+(?:\.\d+)?)(ms|s)\s*$/.exec(valeur);
  if (lue === null) {
    return 0;
  }
  return Number(lue[1]) * (lue[2] === "s" ? 1000 : 1);
}

/**
 * Montre le texte seul, puis fait paraître sa feuille derrière lui. Le texte ne bouge pas et ne s'estompe pas : les
 * mesures de la page restent justes. Une page cachée ne dessine aucune image : l'habillage attend qu'elle soit vue.
 */
export function paraitre(fiche: HTMLElement, page: Document = document): void {
  const duree = dureeEnMs(getComputedStyle(page.documentElement).getPropertyValue("--duree-entree"));
  const vue = page.defaultView;
  if (duree <= 0 || vue === null) {
    return;
  }
  fiche.classList.add(CLASSE_NUE);
  // Deux images : la première dessine le texte seul, la seconde lance l'habillage.
  vue.requestAnimationFrame(() =>
    vue.requestAnimationFrame(() => {
      fiche.classList.remove(CLASSE_NUE);
      const rythme = { duration: duree, easing: DECELERATION };
      const papiers = fiche.querySelector<HTMLElement>(":scope > .papiers");
      if (fiche.classList.contains("en-pages") && papiers !== null) {
        // En pages, les feuilles sont des éléments à part, derrière le texte.
        papiers.animate([{ opacity: 0 }, { opacity: 1 }], rythme);
      } else {
        fiche.animate([NUE, {}], rythme);
      }
    }),
  );
}

/**
 * Le haut de la première ligne du texte, depuis le haut de la vue, en pixels CSS ; 0 sans texte. L'application y amène
 * la fiche lue avant de montrer celle-ci : le texte ne change pas de place au relais.
 */
export function hautDuTexte(fiche: HTMLElement): number {
  const premier = fiche.querySelector(".ProseMirror")?.firstElementChild;
  if (!premier) {
    return 0;
  }
  const plage = fiche.ownerDocument.createRange();
  plage.selectNodeContents(premier);
  return (plage.getClientRects()[0] ?? premier.getBoundingClientRect()).top;
}
