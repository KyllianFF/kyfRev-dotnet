// Le bus entre l'éditeur et l'application : messages typés (./messages.ts est généré
// depuis le C#), aucun objet de l'hôte exposé à la page.

import type { ActionTableau, EtatOutils, NomMarque } from "./editeur";
import { EditeurVisuel } from "./editeur";
import type { Casse } from "./casse";
import type { DepuisEditeur, VersEditeur } from "./messages";

declare const chrome: {
  webview: {
    postMessage(message: unknown): void;
    addEventListener(type: "message", ecoute: (evenement: { data: unknown }) => void): void;
  };
};

/** Taille maximale d'une image, comme l'hôte : au-delà, elle n'est pas lue. */
const IMAGE_MAX = 50 * 1024 * 1024;

/** Le texte part peu après la frappe : l'hôte n'a jamais qu'un instant de retard. */
const DELAI_TEXTE = 250;

const MARQUES: Record<string, NomMarque> = {
  bold: "strong",
  italic: "em",
  underline: "underline",
  strike: "strike",
  superscript: "sup",
  subscript: "sub",
};

const CASSES: Record<string, Casse> = {
  sentence: "phrase",
  lower: "minuscules",
  upper: "capitales",
  words: "mots",
  invert: "inversee",
  alternate: "alternee",
};

const envoyer = (message: DepuisEditeur) => chrome.webview.postMessage(message);

let listeOuverte = false;
let minuterie: ReturnType<typeof setTimeout> | undefined;
let dernierEtat = "";
let derniereSaisie: string | null = null;

function envoyerTexte(id: number | null): void {
  clearTimeout(minuterie);
  envoyer({ kind: "text", id, markdown: editeur.texte() });
}

function deposer(fichiers: File[], position: number | null): void {
  let ici = position;
  for (const fichier of fichiers) {
    const nom = fichier.name || `image.${fichier.type.split("/")[1] ?? "png"}`;
    const lieu = ici;
    ici = null;
    if (fichier.size > IMAGE_MAX) {
      envoyer({ kind: "dropped", name: nom, base64: null, position: lieu });
      continue;
    }
    const lecteur = new FileReader();
    lecteur.onload = () => {
      const adresse = String(lecteur.result);
      envoyer({ kind: "dropped", name: nom, base64: adresse.slice(adresse.indexOf(",") + 1), position: lieu });
    };
    lecteur.readAsDataURL(fichier);
  }
}

const parent = document.getElementById("fiche") as HTMLElement;

const editeur: EditeurVisuel = new EditeurVisuel(parent, {
  onmodification: () => {
    envoyer({ kind: "edited" });
    clearTimeout(minuterie);
    minuterie = setTimeout(() => envoyerTexte(null), DELAI_TEXTE);
  },
  onenregistrer: () => {
    envoyerTexte(null);
    envoyer({ kind: "save" });
  },
  ondeposer: deposer,
  onetat: (etat: EtatOutils) => {
    const nouveau = JSON.stringify(etat);
    if (nouveau !== dernierEtat) {
      dernierEtat = nouveau;
      envoyer({ kind: "tools", ...etat });
    }
  },
  onlien: () => demanderLien(),
  onformule: (formule) => envoyer({ kind: "askFormula", ...formule }),
  onsaisielien: (saisie) => {
    if (!saisie && derniereSaisie === null) return;
    derniereSaisie = saisie?.requete ?? null;
    envoyer({ kind: "linkInput", query: saisie?.requete ?? null, start: saisie?.debut ?? 0, x: saisie?.x ?? 0, y: saisie?.y ?? 0 });
  },
  clavierlien: (touche) => {
    if (!listeOuverte) return false;
    envoyer({ kind: "listKey", key: touche });
    return true;
  },
  onmenu: () => {
    const etat = editeur.etatOutils();
    envoyer({ kind: "menu", ...editeur.contexteDuMenu(), inCode: etat.inCode, inTable: etat.table, onLink: etat.link });
  },
});

function demanderLien(): void {
  const { texte, adresse } = editeur.lienCourant();
  envoyer({ kind: "askLink", label: texte, address: adresse });
}

function traiter(message: VersEditeur): void {
  switch (message.kind) {
    case "load":
      editeur.remplacer(message.text, message.folder);
      break;
    case "theme":
      for (const [nom, valeur] of Object.entries(message.variables)) {
        document.documentElement.style.setProperty(nom, valeur);
      }
      break;
    case "sync":
      envoyerTexte(message.id);
      break;
    case "mark":
      editeur.marque(MARQUES[message.mark] as NomMarque);
      break;
    case "heading":
      editeur.titre(message.level);
      break;
    case "list":
      editeur.liste(message.list);
      break;
    case "case":
      editeur.changerCasse(message.style === null ? null : (CASSES[message.style] as Casse));
      break;
    case "table":
      editeur.insererTableau(message.rows, message.columns);
      break;
    case "tableAction":
      editeur.tableau(message.action as ActionTableau);
      break;
    case "code":
      editeur.insererCode(message.language);
      break;
    case "rule":
      editeur.insererLigne();
      break;
    case "askFormula":
      envoyer({ kind: "askFormula", ...(editeur.formuleSelectionnee() ?? { position: null, tex: "", block: false }) });
      break;
    case "formula":
      editeur.formule(message.tex, message.block, message.position);
      break;
    case "askLink":
      demanderLien();
      break;
    case "link":
      editeur.lien(message.label, message.address);
      break;
    case "unlink":
      editeur.retirerLien();
      break;
    case "clearFormat":
      editeur.effacerMiseEnForme();
      break;
    case "color":
      editeur.couleur(message.color);
      break;
    case "size":
      editeur.taille(message.size);
      break;
    case "align":
      editeur.aligner(message.align);
      break;
    case "image":
      editeur.insererImage(message.link, message.alternative, message.position);
      break;
    case "sheetLink":
      editeur.insererLienFiche(message.label, message.address);
      break;
    case "replaceWord":
      editeur.remplacerMot(message.start, message.end, message.text);
      break;
    case "undo":
      editeur.annuler();
      break;
    case "redo":
      editeur.retablir();
      break;
    case "focus":
      editeur.focus();
      break;
    case "goToHeading":
      editeur.allerAuTitre(message.title, message.occurrence);
      break;
    case "clipboard":
      editeur.focus();
      if (message.action === "cut" || message.action === "copy") document.execCommand(message.action);
      else void editeur.coller(message.action === "pastePlain");
      break;
    case "listOpen":
      listeOuverte = message.open;
      break;
  }
}

chrome.webview.addEventListener("message", (evenement) => traiter(evenement.data as VersEditeur));
envoyer({ kind: "ready" });
