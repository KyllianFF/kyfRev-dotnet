// Cadre d'une extension. Ce script est celui de kyfRev : il donne l'objet `kyfrev`, puis charge le script de l'extension. Tout passe
// par des messages avec la page qui l'héberge, qui les attribue à ce cadre seul ; l'application vérifie ensuite les permissions et
// ne garde du HTML rendu qu'une liste blanche. Le contrat est celui de la 0.x, pour que les extensions déjà écrites fonctionnent.
"use strict";

(() => {
  const parent = window.parent;
  const blocs = new Map();
  const actions = new Map();
  const attentes = new Map();
  const commandes = new Map();
  let vue = null;
  let fonctionCompteur = null;
  let fonctionEncart = null;
  let compteur = 0;
  let jeton = null;

  const envoyer = (message) => parent.postMessage({ ...message, jeton }, "*");

  /** Appel à kyfRev (fiches, stockage…), qui répond par une promesse. */
  function demander(methode, args) {
    const n = ++compteur;
    return new Promise((resoudre, rejeter) => {
      attentes.set(n, { resoudre, rejeter });
      envoyer({ type: "requete", n, methode, args });
    });
  }

  const enTexte = async (valeur) => {
    const resultat = await valeur;
    if (typeof resultat !== "string") throw new Error("le rendu doit être une chaîne HTML");
    return resultat;
  };

  const kyfrev = Object.freeze({
    /** Dessine les blocs ```langage des fiches : fonction(source, contexte) → HTML. */
    bloc(langage, fonction) {
      if (typeof fonction !== "function") throw new TypeError("kyfrev.bloc : fonction attendue");
      blocs.set(String(langage), fonction);
    },
    /** Écran de la vue de l'extension : fonction(contexte) → HTML. */
    vue(fonction) {
      if (typeof fonction !== "function") throw new TypeError("kyfrev.vue : fonction attendue");
      vue = fonction;
    },
    /** Réagit à un clic (ou un changement) sur un élément data-action="nom" de la vue. */
    action(nom, fonction) {
      if (typeof fonction !== "function") throw new TypeError("kyfrev.action : fonction attendue");
      actions.set(String(nom), fonction);
    },
    /** Lance une commande déclarée dans extension.json (Ctrl+K, clic droit) : fonction(contexte). Contrat 2. */
    commande(nom, fonction) {
      if (typeof fonction !== "function") throw new TypeError("kyfrev.commande : fonction attendue");
      commandes.set(String(nom), fonction);
    },
    /** Le nombre affiché près de l'entrée du menu : fonction() → nombre, ou 0 pour rien. Contrat 2. */
    compteur(fonction) {
      if (typeof fonction !== "function") throw new TypeError("kyfrev.compteur : fonction attendue");
      fonctionCompteur = fonction;
    },
    /** L'encart sur « Aujourd'hui » : fonction() → { texte, detail } ou null. Contrat 2. */
    encart(fonction) {
      if (typeof fonction !== "function") throw new TypeError("kyfrev.encart : fonction attendue");
      fonctionEncart = fonction;
    },
    /** Ouvre la vue de l'extension. Contrat 2. */
    ouvrirVue: () => demander("vue.ouvrir", []),
    /** Redessine la vue (après un changement d'état hors d'une action). */
    rafraichir() {
      envoyer({ type: "rafraichir" });
    },
    fiches: Object.freeze({
      /** Liste des fiches : id, titre, dossier, étiquettes (permission « fiches:lecture »). */
      lister: () => demander("fiches.lister", []),
      /** Texte Markdown d'une fiche (permission « fiches:lecture »). */
      lire: (id) => demander("fiches.lire", [String(id)]),
      /** Crée une fiche : titre, puis { dossier, texte, etiquettes } ; rend son id (permission « fiches:creation »). Contrat 2. */
      creer: (titre, options) => demander("fiches.creer", [String(titre), options ?? {}]),
      /** Remplace les étiquettes d'une fiche ; rend celles enregistrées (permission « fiches:creation »). Contrat 2. */
      etiqueter: (id, etiquettes) => demander("fiches.etiqueter", [String(id), etiquettes]),
      /** Remplace le texte d'une fiche ; le précédent reste dans ses versions (permission « fiches:ecriture »). Contrat 2. */
      ecrire: (id, texte) => demander("fiches.ecrire", [String(id), String(texte)]),
    }),
    stockage: Object.freeze({
      /** Données de l'extension dans le coffre, ou null (permission « stockage »). */
      lire: () => demander("stockage.lire", []),
      /** Remplace les données (un objet JSON, 1 Mo au plus ; permission « stockage »). */
      ecrire: (donnees) => demander("stockage.ecrire", [donnees]),
    }),
  });
  Object.defineProperty(globalThis, "kyfrev", { value: kyfrev, writable: false, configurable: false });

  async function traiter(message) {
    switch (message.type) {
      case "charger": {
        const script = document.createElement("script");
        script.src = message.url;
        script.onload = () => envoyer({ type: "charge" });
        script.onerror = () => envoyer({ type: "erreur", erreur: "script de l'extension introuvable" });
        document.body.append(script);
        return undefined;
      }
      case "reponse": {
        const attente = attentes.get(message.n);
        if (!attente) return undefined;
        attentes.delete(message.n);
        if ("erreur" in message) attente.rejeter(new Error(message.erreur));
        else attente.resoudre(message.resultat);
        return undefined;
      }
      case "rendre-bloc": {
        const fonction = blocs.get(message.langage);
        if (!fonction) throw new Error(`aucun rendu pour « ${message.langage} »`);
        return { html: await enTexte(fonction(message.source, message.contexte)) };
      }
      case "rendre-vue": {
        if (!vue) throw new Error("cette extension n'a pas de vue");
        return { html: await enTexte(vue(message.contexte)) };
      }
      case "action": {
        const fonction = actions.get(message.nom);
        if (!fonction) throw new Error(`action « ${message.nom} » inconnue`);
        await fonction(message.valeur, message.donnees);
        return { ok: true };
      }
      case "commande": {
        const fonction = commandes.get(message.nom);
        if (!fonction) throw new Error(`commande « ${message.nom} » inconnue`);
        await fonction(message.contexte ?? {});
        return { ok: true };
      }
      case "compteur": {
        const nombre = fonctionCompteur ? await fonctionCompteur() : null;
        return { valeur: typeof nombre === "number" && Number.isFinite(nombre) ? nombre : null };
      }
      case "encart": {
        const contenu = fonctionEncart ? await fonctionEncart() : null;
        return {
          valeur: contenu && typeof contenu.texte === "string"
            ? { texte: contenu.texte, detail: typeof contenu.detail === "string" ? contenu.detail : null }
            : null,
        };
      }
      default:
        return undefined;
    }
  }

  window.addEventListener("message", (evenement) => {
    // Seule la page qui l'héberge parle à ce cadre : un autre cadre qui écrit ici n'obtient rien.
    if (evenement.source !== parent) return;
    const message = evenement.data ?? {};
    if (jeton === null && message.type === "charger" && typeof message.jeton === "string") jeton = message.jeton;
    if (jeton === null || message.jeton !== jeton) return;
    traiter(message).then(
      (resultat) => {
        if (resultat !== undefined && message.n !== undefined) envoyer({ type: "resultat", n: message.n, ...resultat });
      },
      (erreur) => {
        if (message.n !== undefined) envoyer({ type: "resultat", n: message.n, erreur: String(erreur?.message ?? erreur) });
      },
    );
  });

  envoyer({ type: "pret" });
})();
