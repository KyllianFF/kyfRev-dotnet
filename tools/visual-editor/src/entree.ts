// La feuille paraît en fondu à chaque texte chargé. La page a sa propre fenêtre, que l'application ne peut ni
// estomper ni faire glisser : c'est donc la page qui s'anime, sur la durée que l'application lui donne
// (« --duree-entree », avec le thème). Durée nulle : la feuille est là d'un coup.

/** La même décélération que les transitions de l'application. */
export const DECELERATION = "cubic-bezier(0.215, 0.61, 0.355, 1)";

/** Une durée CSS (« 220ms », « 0.22s ») en millisecondes ; 0 si elle est vide, illisible ou négative. */
export function dureeEnMs(valeur: string): number {
  const lue = /^\s*(\d+(?:\.\d+)?)(ms|s)\s*$/.exec(valeur);
  if (lue === null) {
    return 0;
  }
  return Number(lue[1]) * (lue[2] === "s" ? 1000 : 1);
}

/**
 * Fait paraître la feuille en fondu. Seule l'opacité change : rien n'est déplacé, les mesures de la page restent
 * justes. Si la page n'est pas encore à l'écran, le fondu attend qu'elle le soit.
 */
export function paraitre(feuille: HTMLElement, page: Document = document): void {
  const duree = dureeEnMs(getComputedStyle(page.documentElement).getPropertyValue("--duree-entree"));
  if (duree <= 0) {
    return;
  }
  const fondre = (): void => {
    for (const enCours of feuille.getAnimations()) {
      enCours.cancel();
    }
    feuille.animate([{ opacity: 0 }, { opacity: 1 }], { duration: duree, easing: DECELERATION });
  };
  if (page.visibilityState === "hidden") {
    page.addEventListener("visibilitychange", fondre, { once: true });
  } else {
    fondre();
  }
}
