// Avant que le coffre se verrouille, l'application demande à l'îlot de finir ce qui est en cours. Le champ où l'on écrivait
// s'applique : les champs de l'éditeur ne s'appliquent qu'à leur « change », quand on les quitte. L'enregistrement qui
// attendait part tout de suite, et l'îlot dit à l'application ce qu'il en reste : un schéma qui n'a pas pu s'enregistrer
// (refusé, ou modifié ailleurs) garde le coffre ouvert, et rien ne se perd.

/** Ce qu'il reste après avoir fini ; « aucun » : aucun schéma n'est ouvert dans l'éditeur. */
export type EtatFin = "enregistre" | "aucun" | "erreur" | "conflit" | "modifie";

let finirEditeur: (() => Promise<EtatFin>) | null = null;
/** Le champ où l'on a écrit sans que ce soit encore appliqué. */
let enAttente: EventTarget | null = null;

/**
 * Suit ce qui s'écrit dans les champs. Quitter un champ l'applique ; mais quand le moteur n'a plus la main (la fenêtre
 * de l'application ne l'a plus, la session s'est verrouillée), le champ la garde, et ce qu'on y a écrit attend.
 */
export function suivreLesChamps(page: Document): void {
  page.addEventListener("input", (evenement) => {
    enAttente = evenement.target;
  });
  page.addEventListener("change", (evenement) => {
    if (evenement.target === enAttente) enAttente = null;
  });
  page.addEventListener("focusin", (evenement) => {
    if (evenement.target !== enAttente) enAttente = null;
  });
}

/** L'éditeur qui s'ouvre dit comment le finir ; la fonction rendue l'oublie, quand il se ferme. */
export function inscrireEditeur(finir: () => Promise<EtatFin>): () => void {
  finirEditeur = finir;
  return () => {
    if (finirEditeur === finir) finirEditeur = null;
  };
}

/** Finit ce qui est en cours : le champ où l'on écrivait s'applique, puis l'éditeur ouvert enregistre ce qui attendait. */
export async function finir(page: Document = document): Promise<EtatFin> {
  appliquerLeChamp(page);
  if (!finirEditeur) return "aucun";
  try {
    return await finirEditeur();
  } catch {
    return "erreur";
  }
}

/** Le champ qui a encore la main la quitte, et s'applique ; si le moteur ne l'a pas, le quitter ne suffit pas. */
function appliquerLeChamp(page: Document): void {
  const champ = enAttente;
  if (!champ) return;
  if (champ === page.activeElement) (champ as HTMLElement).blur();
  if (enAttente === champ) champ.dispatchEvent(new Event("change", { bubbles: true }));
  enAttente = null;
}
