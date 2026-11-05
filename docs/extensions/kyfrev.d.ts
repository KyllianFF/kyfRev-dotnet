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
 * `dossier` ; depuis le clic droit d'un dossier : son `dossier`.
 */
interface KyfrevContexteCommande {
  readonly id?: string;
  readonly titre?: string;
  readonly dossier?: string;
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

/** Une fiche à créer : tout est facultatif, sauf son titre, donné à part. */
interface KyfrevNouvelleFiche {
  /** Son dossier, relatif au dossier des fiches ; vide ou absent : la racine. */
  dossier?: string;
  /** Son texte Markdown ; absent : une fiche vide. */
  texte?: string;
  /** Ses étiquettes, sans « # » (50 au plus). */
  etiquettes?: readonly string[];
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
    /**
     * Crée une fiche, et rend son identifiant (permission « fiches:creation »). Le titre fait 200 caractères au plus, le texte
     * 1 Mo. La création est notée au journal de l'extension, que l'utilisateur voit dans les Paramètres.
     */
    creer(titre: string, fiche?: KyfrevNouvelleFiche): Promise<string>;
    /** Remplace les étiquettes d'une fiche, et rend celles enregistrées : sans « # », sans doublon (permission « fiches:creation »). */
    etiqueter(id: string, etiquettes: readonly string[]): Promise<string[]>;
    /**
     * Remplace le texte Markdown d'une fiche, son en-tête gardé (permission « fiches:ecriture »). Le texte d'avant reste dans les
     * versions de la fiche, d'où l'utilisateur peut le remettre ; l'écriture est notée au journal de l'extension. Aucune
     * extension ne peut supprimer une fiche. Au plus 60 écritures par minute, créations et étiquettes comprises.
     */
    ecrire(id: string, texte: string): Promise<null>;
  };

  readonly stockage: {
    /** Les données de l'extension dans le coffre, ou null (permission « stockage »). */
    lire(): Promise<KyfrevJson>;
    /** Remplace ces données : un objet JSON, 1 Mo au plus (permission « stockage »). */
    ecrire(donnees: KyfrevJson): Promise<null>;
  };
}

declare const kyfrev: Kyfrev;
