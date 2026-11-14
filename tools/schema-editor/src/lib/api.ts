// Ce que l'îlot demande à l'application : chaque appel part vers l'hôte WPF et sa réponse revient
// sous son numéro. L'îlot ne voit ni le coffre ni le disque : il n'a que ces méthodes.

import { type EtatFin, finir } from "./fin";
import type { ResultatEnregistrement } from "./generes/ResultatEnregistrement";
import type { SchemaLu } from "./generes/SchemaLu";
import type { SchemaResume } from "./generes/SchemaResume";

/** Où un schéma est rangé : dans la liste, aux archives, ou à la corbeille (d'où il se restaure). */
export type EtatSchema = "actif" | "archive" | "corbeille";
export type SchemaRange = SchemaResume & { etat: EtatSchema };

type Attente = { resoudre: (valeur: never) => void; rejeter: (raison: unknown) => void };

type Reponse = { kind: "answer"; id: number; ok: true; value: unknown } | { kind: "answer"; id: number; ok: false; error: string };

export type DepuisHote =
  | Reponse
  | { kind: "theme"; variables: Record<string, string> }
  | { kind: "open"; id: string }
  | { kind: "finir"; numero: number }
  | { kind: "revenu" };

const attentes = new Map<number, Attente>();
let suivant = 1;

function hote(): { postMessage: (message: unknown) => void } {
  const vue = (window as unknown as { chrome?: { webview?: { postMessage: (message: unknown) => void } } }).chrome?.webview;
  if (!vue) throw new Error("L'îlot n'a pas d'hôte.");
  return vue;
}

/** Envoie un message sans attendre de réponse. */
export function signaler(kind: string): void {
  hote().postMessage({ kind });
}

function appeler<T>(method: string, ...args: unknown[]): Promise<T> {
  return new Promise<T>((resoudre, rejeter) => {
    const id = suivant++;
    attentes.set(id, { resoudre: resoudre as (valeur: never) => void, rejeter });
    hote().postMessage({ kind: "call", id, method, args });
  });
}

/** Reçoit ce que l'hôte envoie : une réponse, le thème, un schéma à ouvrir, ou ce qui est en cours à finir. */
export function recevoir(message: DepuisHote): void {
  if (message.kind === "theme") {
    for (const [nom, valeur] of Object.entries(message.variables)) {
      document.documentElement.style.setProperty(nom, valeur);
    }
    document.dispatchEvent(new Event("theme"));
    return;
  }
  if (message.kind === "open") {
    document.dispatchEvent(new CustomEvent("ouvrir-schema", { detail: message.id }));
    return;
  }
  if (message.kind === "revenu") {
    // La vue est de nouveau affichée, l'îlot gardé ouvert depuis : la liste relit le coffre.
    document.dispatchEvent(new Event("schemas-revenus"));
    return;
  }
  if (message.kind === "finir") {
    // Le coffre va se verrouiller : l'application attend de savoir ce qu'il reste, sous le numéro de sa demande.
    const numero = message.numero;
    void finir()
      .then((etat) => api.etatSauvegarde(numero, etat))
      .catch(() => undefined);
    return;
  }
  const attente = attentes.get(message.id);
  if (!attente) return;
  attentes.delete(message.id);
  if (message.ok) attente.resoudre(message.value as never);
  else attente.rejeter(message.error);
}

/** Des octets en base 64 : un message de l'hôte ne porte que du texte. */
function base64(octets: Uint8Array): string {
  let texte = "";
  for (let debut = 0; debut < octets.length; debut += 0x8000) {
    texte += String.fromCharCode(...octets.subarray(debut, debut + 0x8000));
  }
  return btoa(texte);
}

export const api = {
  schemas: () => appeler<SchemaResume[]>("schemas"),
  schemasRanges: () => appeler<SchemaRange[]>("schemasRanges"),
  rangerSchema: (id: string, etat: EtatSchema) => appeler<null>("rangerSchema", id, etat),
  lireSchema: (id: string) => appeler<SchemaLu>("lireSchema", id),
  creerSchema: (texte: string) => appeler<SchemaLu>("creerSchema", texte),
  enregistrerSchema: (id: string, texte: string, empreinte: string) =>
    appeler<ResultatEnregistrement>("enregistrerSchema", id, texte, empreinte),
  dupliquerSchema: (id: string) => appeler<SchemaLu>("dupliquerSchema", id),
  exporterSchema: (dossier: string, nom: string, octets: Uint8Array) => appeler<string>("exporterSchema", dossier, nom, base64(octets)),
  /** L'image PNG d'un schéma va dans le presse-papiers ; une erreur dit pourquoi ce n'est pas fait. */
  copierImageSchema: (octets: Uint8Array) => appeler<null>("copierImageSchema", base64(octets)),
  choisirDossier: (titre: string) => appeler<string | null>("choisirDossier", titre),
  choisirFichierSchema: () => appeler<string | null>("choisirFichierSchema"),
  lireImportSchema: (chemin: string) => appeler<string>("lireImportSchema", chemin),
  ouvrirFiche: (id: string) => appeler<null>("ouvrirFiche", id),
  editeurFerme: () => appeler<null>("editeurFerme"),
  etatSauvegarde: (numero: number, etat: EtatFin) => appeler<null>("etatSauvegarde", numero, etat),
};

export function messageErreur(erreur: unknown): string {
  if (typeof erreur === "string") return erreur;
  if (erreur instanceof Error) return erreur.message;
  return "Une erreur inattendue s'est produite.";
}
