// Les types de l'objet `kyfrev`, tel qu'une extension le reçoit dans son cadre (contrat 2).
//
// Copiez ce fichier à côté de votre extension.js : un éditeur comme VS Code complète alors les noms et vérifie les appels, sans
// rien installer ni rien compiler. L'extension reste du JavaScript ordinaire ; ces types ne changent rien à son exécution.
//
// Tout appel à kyfRev rend une promesse. Une permission non demandée dans extension.json, ou retirée dans les Paramètres, la rejette
// avec une erreur qui dit laquelle manque.

/** Ce que reçoit le rendu d'un bloc ou de la vue. */
interface KyfrevContexteRendu {
  /** Le thème de l'application : le HTML rendu en suit les couleurs de lui-même. */
  readonly theme: "clair" | "sombre";
}

/**
 * Ce sur quoi porte une commande. Vide depuis la recherche (Ctrl+K) ; depuis le clic droit d'une fiche : son `id`, son `titre`, son
 * `dossier` ; depuis le clic droit d'un dossier : son `dossier` ; depuis le clic droit d'un texte sélectionné en lecture (permission
 * « selection ») : la fiche, le `texte`, son `debut` et sa `fin` dans le texte affiché.
 */
interface KyfrevContexteCommande {
  readonly id?: string;
  readonly titre?: string;
  readonly dossier?: string;
  readonly texte?: string;
  readonly debut?: string;
  readonly fin?: string;
}

/** Une fiche, dans la liste. */
interface KyfrevFiche {
  readonly id: string;
  readonly titre: string;
  /** Son dossier, chemin relatif au coffre ; vide à la racine. */
  readonly dossier: string;
  readonly etiquettes: readonly string[];
  readonly archivee: boolean;
}

/** Le texte d'une fiche. */
interface KyfrevTexteFiche {
  readonly id: string;
  readonly titre: string;
  /** Son texte Markdown, tel qu'il est enregistré. */
  readonly texte: string;
}

/** Ce que rend une sélection guidée. */
interface KyfrevSelection {
  /** « selection » : un texte pris dans une fiche ; « saisie » : écrit à la main ; « passer » ; « terminer » : la série s'arrête. */
  readonly choix: "selection" | "saisie" | "passer" | "terminer";
  /** Le texte pris ou écrit ; null pour « passer » et « terminer ». */
  readonly texte: string | null;
  /** La fiche d'où vient le texte pris, son titre, et sa place dans le texte affiché (pour la rouvrir là avec fiches.ouvrir). */
  readonly fiche: string | null;
  readonly titre: string | null;
  readonly debut: number | null;
  readonly fin: number | null;
}

/** L'encart sur « Aujourd'hui » : un texte court, et un détail facultatif (300 caractères au plus chacun). */
interface KyfrevEncart {
  texte: string;
  detail?: string | null;
}

/**
 * La valeur d'une action. Un bouton envoie son `data-valeur` ; une case à cocher, son état ; un champ ou une liste, son texte ; un
 * formulaire envoyé, ses champs nommés (`name`) : le texte des champs, l'état des cases, la valeur du bouton radio coché.
 */
type KyfrevValeur = string | boolean | null | { readonly [nom: string]: string | boolean | null };

/** Une donnée enregistrable : ce que JSON sait écrire. */
type KyfrevJson = string | number | boolean | null | readonly KyfrevJson[] | { readonly [cle: string]: KyfrevJson };

interface Kyfrev {
  /**
   * Dessine les blocs ```langage des fiches. Le HTML rendu passe par une liste blanche : ni style, ni lien, ni image, ni script ;
   * kyfRev en fait des éléments natifs.
   */
  bloc(langage: string, fonction: (source: string, contexte: KyfrevContexteRendu) => string | Promise<string>): void;

  /** L'écran de la vue de l'extension, déclarée dans extension.json (`vue.titre`). */
  vue(fonction: (contexte: KyfrevContexteRendu) => string | Promise<string>): void;

  /** Réagit à un clic, à un changement ou à l'envoi d'un formulaire sur un élément `data-action="nom"` de la vue. */
  action(nom: string, fonction: (valeur: KyfrevValeur, donnees: { readonly [nom: string]: string }) => void | Promise<void>): void;

  /** Lance une commande déclarée dans extension.json (`commandes`), depuis la recherche (Ctrl+K) ou le clic droit (`menus`). */
  commande(nom: string, fonction: (contexte: KyfrevContexteCommande) => void | Promise<void>): void;

  /** Le nombre affiché près de l'entrée du menu (`vue.compteur`) : 0 ou null pour n'en rien afficher, 999 au plus. */
  compteur(fonction: () => number | null | Promise<number | null>): void;

  /** L'encart de l'extension sur « Aujourd'hui » (`encart.titre`) ; null pour n'en rien afficher ce jour-là. */
  encart(fonction: () => KyfrevEncart | null | Promise<KyfrevEncart | null>): void;

  /** Ouvre la vue de l'extension, par exemple à la fin d'une commande. */
  ouvrirVue(): Promise<null>;

  /** Redessine la vue, après un changement survenu hors d'une action. */
  rafraichir(): void;

  readonly fiches: {
    /** La liste des fiches (permission « fiches:lecture »). */
    lister(): Promise<KyfrevFiche[]>;
    /** Le texte d'une fiche (permission « fiches:lecture »). */
    lire(id: string): Promise<KyfrevTexteFiche>;
    /** Ouvre une fiche en lecture, et y sélectionne un passage s'il est donné (permission « fiches:lecture »). */
    ouvrir(id: string, passage?: { debut?: number; fin?: number }): Promise<null>;
  };

  readonly selection: {
    /**
     * Demande à l'utilisateur de sélectionner un texte dans une fiche, sous une consigne (permission « selection »). La fiche donnée
     * s'ouvre en lecture ; sans elle, c'est la fiche ouverte. Une barre montre la consigne : « Prendre la sélection », un champ pour
     * écrire ce qui manque (sauf `saisie: false`), « Passer » et « Terminer ». Le temps ne court pas pendant que l'utilisateur répond.
     */
    demander(consigne: string, options?: { fiche?: string; saisie?: boolean }): Promise<KyfrevSelection>;
  };

  readonly stockage: {
    /** Les données de l'extension dans le coffre, ou null (permission « stockage »). */
    lire(): Promise<KyfrevJson>;
    /** Remplace ces données : un objet JSON, 1 Mo au plus (permission « stockage »). */
    ecrire(donnees: KyfrevJson): Promise<null>;
  };
}

declare const kyfrev: Kyfrev;
