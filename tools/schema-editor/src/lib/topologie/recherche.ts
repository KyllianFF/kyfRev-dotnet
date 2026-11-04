// Recherche dans un schéma (D49) : un équipement par son nom, son nom d'hôte,
// son type, une adresse ou un port ; un câble par son nom ou son étiquette ;
// une annotation par son texte. Sans accents ni casse.

import { definition } from "./catalogue";
import type { Schema } from "./modele";

export type Trouve = {
  /** Objet à montrer et à choisir. */
  id: string;
  genre: "equipement" | "lien" | "annotation";
  libelle: string;
  /** Ce qui correspond, quand ce n'est pas le nom (« adresse 10.0.0.1 », « port Gi1/0/3 »). */
  detail?: string;
};

const simple = (texte: string) =>
  texte
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase()
    // Les ligatures ne se décomposent pas : « Cœur » se trouve en tapant « coeur ».
    .replaceAll("œ", "oe")
    .replaceAll("æ", "ae");

/** Ce qui correspond à `texte` dans le schéma, les noms d'abord ; au plus `maximum` réponses. */
export function chercherDansSchema(schema: Schema, texte: string, maximum = 30): Trouve[] {
  const voulu = simple(texte.trim());
  if (!voulu) return [];
  const contient = (t: string | undefined) => t !== undefined && simple(t).includes(voulu);
  const parNom: Trouve[] = [];
  const autres: Trouve[] = [];
  for (const e of schema.equipements) {
    if (contient(e.nom)) {
      parNom.push({ id: e.id, genre: "equipement", libelle: e.nom, detail: definition(e.type).nom });
      continue;
    }
    const detail = contient(e.nomHote)
      ? `nom d'hôte ${e.nomHote}`
      : contient(e.gestion?.adresse)
        ? `adresse de gestion ${e.gestion?.adresse}`
        : (() => {
            for (const i of e.interfaces) {
              const a = i.adresses.find((x) => contient(x.adresse));
              if (a)
                return `adresse ${a.adresse}${a.prefixe !== undefined ? `/${a.prefixe}` : ""} (${i.nom})`;
            }
            const port = e.interfaces.find((i) => contient(i.nom));
            if (port) return `port ${port.nom}`;
            if (contient(definition(e.type).nom)) return definition(e.type).nom;
            if (contient(e.role)) return `rôle ${e.role}`;
            return undefined;
          })();
    if (detail) autres.push({ id: e.id, genre: "equipement", libelle: e.nom, detail });
  }
  for (const l of schema.liens) {
    if (contient(l.nom) || contient(l.etiquette)) {
      const a = schema.equipements.find((e) => e.id === l.a.equipement)?.nom ?? "?";
      const b = schema.equipements.find((e) => e.id === l.b.equipement)?.nom ?? "?";
      autres.push({
        id: l.id,
        genre: "lien",
        libelle: l.nom || l.etiquette || "Câble",
        detail: `câble ${a} – ${b}`,
      });
    }
  }
  for (const n of schema.annotations) {
    if (contient(n.texte))
      autres.push({
        id: n.id,
        genre: "annotation",
        libelle: n.texte.split("\n")[0] ?? "",
        detail: "annotation",
      });
  }
  return [...parNom, ...autres].slice(0, maximum);
}
