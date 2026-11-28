// Ajouter des pages à un autre schéma (31D) : les copier (le schéma d'où elles viennent ne change pas) ou les déplacer
// (elles le quittent). Les identifiants sont refaits, les VLAN et les modèles de matériel rejoignent ceux de la cible
// (reunion.ts). Un câble ne va pas d'un schéma à l'autre : entre une page qui part et une page qui reste, il ne suit
// pas, et le compte rendu le dit ; entre deux pages qui partent ensemble, il suit.

import { type Page, type Schema, schemaVide } from "./modele";
import { pageDeLElement, pagesDe, supprimerPage } from "./pages";
import { extrairePages, type Fusion, fusionner, renumeroter } from "./reunion";
import { Refus, retirerEquipements } from "./operations";

export type Transfert = Fusion & {
  /** Câbles entre une page prise et une page laissée : non copiés, ou retirés du schéma d'origine s'il s'agit de déplacer. */
  cablesRetires: number;
  /** Relations entre une page prise et une page laissée, de même. */
  relationsRetirees: number;
};

/** Les pages `ids` de `source` (une page implicite a l'identifiant vide), dans l'ordre des onglets. */
function pagesVoulues(source: Schema, ids: string[]): string[] {
  const voulues = new Set(ids);
  return pagesDe(source)
    .map((p) => p.id)
    .filter((id) => voulues.has(id));
}

/**
 * Copie les pages `ids` de `source` dans `cible` (modifiée), après les siennes ; `source` n'est pas touché. Une cible
 * vide prend ces pages à la place de la sienne.
 */
export function copierPages(source: Schema, ids: string[], cible: Schema): Transfert {
  const voulues = pagesVoulues(source, ids);
  if (voulues.length === 0) throw new Refus("Aucune page à ajouter.");
  const { morceau, cablesHors, relationsHors } = extrairePages(source, voulues, source.titre);
  const fusion = fusionner(cible, renumeroter(morceau), {
    prefixe: source.titre,
    conflitVlan: (vlan, garde, autre) =>
      `VLAN ${vlan} : « ${garde} » dans « ${cible.titre} », « ${autre} » dans « ${source.titre} ». Le nom de « ${cible.titre} » est gardé.`,
  });
  return { ...fusion, cablesRetires: cablesHors, relationsRetirees: relationsHors };
}

/**
 * Retire les pages `ids` d'un schéma (modifié), avec tout ce qui y est, câbles vers les autres pages compris. Si elles
 * y étaient toutes, il ne reste qu'une page vide.
 */
export function retirerPages(schema: Schema, ids: string[]): void {
  const voulues = pagesVoulues(schema, ids);
  if (voulues.length === 0) throw new Refus("Cette page n'existe plus.");
  const toutes = pagesDe(schema);
  const restantes = toutes.filter((p) => !voulues.includes(p.id));
  for (const id of voulues) if ((schema.pages?.length ?? 0) >= 2) supprimerPage(schema, id);
  if (restantes.length > 0) return;
  // La dernière page partait aussi : le schéma se vide, et redevient d'une seule page, implicite.
  const reste = pagesDe(schema)[0]?.id ?? "";
  const ici = <T extends { page?: string }>(x: T) => pageDeLElement(schema, x) === reste;
  retirerEquipements(schema, new Set(schema.equipements.filter(ici).map((e) => e.id)));
  schema.groupes = [];
  schema.annotations = [];
  delete schema.ensembles;
  delete schema.pages;
}

/** Déplace les pages `ids` de `source` vers `cible` : copiées, puis retirées de `source` (tous deux modifiés). */
export function deplacerPages(source: Schema, ids: string[], cible: Schema): Transfert {
  const transfert = copierPages(source, ids, cible);
  retirerPages(source, ids);
  return transfert;
}

/** Un nouveau schéma fait d'une page de `source`, à son nom ; `source` n'est pas touché. */
export function schemaDeLaPage(source: Schema, page: string): { schema: Schema; transfert: Transfert } {
  const nom = pagesDe(source).find((p) => p.id === page)?.nom ?? "";
  const schema: Schema = {
    ...schemaVide(nom),
    vue: source.vue,
    ...(source.cablesCoudes ? { cablesCoudes: true } : {}),
    ...(source.affichage ? { affichage: JSON.parse(JSON.stringify(source.affichage)) as Schema["affichage"] } : {}),
  };
  const transfert = copierPages(source, [page], schema);
  return { schema, transfert };
}

/** « 2 pages », « 1 câble »… */
export const compte = (n: number, un: string, plusieurs: string): string => `${n} ${n > 1 ? plusieurs : un}`;

/** Le compte rendu d'un ajout de pages, en phrases. */
export function compteRendu(
  transfert: Transfert,
  quoi: { source: string; cible: string; deplacer: boolean },
): string[] {
  const noms = transfert.pages.map((p: Page) => `« ${p.nom} »`).join(", ");
  const phrases = [
    `${transfert.pages.length > 1 ? `${transfert.pages.length} pages ${quoi.deplacer ? "déplacées" : "ajoutées"}` : `Page ${quoi.deplacer ? "déplacée" : "ajoutée"}`} dans « ${quoi.cible} » : ${noms}.`,
  ];
  if (transfert.cablesRetires > 0) {
    phrases.push(
      quoi.deplacer
        ? `${compte(transfert.cablesRetires, "câble allait", "câbles allaient")} vers une page restée dans « ${quoi.source} » : ${transfert.cablesRetires > 1 ? "ils sont retirés" : "il est retiré"}, un câble ne va pas d'un schéma à l'autre.`
        : `${compte(transfert.cablesRetires, "câble va", "câbles vont")} vers une page qui n'est pas copiée : ${transfert.cablesRetires > 1 ? "ils restent" : "il reste"} dans « ${quoi.source} » seulement.`,
    );
  }
  if (transfert.relationsRetirees > 0) {
    phrases.push(
      `${compte(transfert.relationsRetirees, "relation", "relations")} vers une page laissée ${quoi.deplacer ? (transfert.relationsRetirees > 1 ? "sont retirées" : "est retirée") : (transfert.relationsRetirees > 1 ? "ne sont pas copiées" : "n'est pas copiée")}.`,
    );
  }
  phrases.push(...transfert.avertissements);
  return phrases;
}
