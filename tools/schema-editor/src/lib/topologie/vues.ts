// Vues d'un schéma (D39) : chacune filtre le même modèle (rien n'est copié)
// et choisit ce qui s'affiche : câbles, genres de relations, noms de ports.

import type { GenreRelation, IdVue, Lien, Relation, Schema, Vue } from "./modele";

export const VUES: Vue[] = [
  {
    id: "physique",
    nom: "Physique",
    liens: true,
    relations: ["association-wifi", "operateur", "haute-disponibilite"],
    ports: true,
    vlans: false,
  },
  {
    id: "logique",
    nom: "Logique et VLAN",
    liens: true,
    relations: ["tunnel", "association-wifi", "operateur", "dependance", "haute-disponibilite"],
    ports: false,
    vlans: true,
  },
  {
    id: "virtualisation",
    nom: "Virtualisation",
    liens: true,
    relations: ["hebergement", "cluster", "reseau-docker", "volume", "dependance"],
    ports: false,
    vlans: false,
  },
  {
    id: "stockage",
    nom: "Stockage",
    liens: true,
    relations: ["stockage", "volume", "hebergement"],
    ports: true,
    vlans: false,
  },
  {
    id: "simplifiee",
    nom: "Simplifiée",
    liens: true,
    relations: ["tunnel", "association-wifi", "operateur", "haute-disponibilite"],
    ports: false,
    vlans: false,
  },
];

export const vue = (id: IdVue): Vue => VUES.find((v) => v.id === id) ?? (VUES[0] as Vue);

export const relationVisible = (relation: Relation, v: Vue): boolean => v.relations.includes(relation.genre);

/** Liens visibles : en vue Stockage, seulement ceux qui touchent une interface de stockage. */
export function liensVisibles(schema: Schema, v: Vue): Lien[] {
  if (!v.liens) return [];
  if (v.id !== "stockage") return schema.liens;
  const stockage = new Set<string>();
  for (const e of schema.equipements) {
    for (const i of e.interfaces)
      if (i.genre === "stockage" || i.role === "stockage") stockage.add(`${e.id}/${i.id}`);
  }
  return schema.liens.filter(
    (l) =>
      l.support === "fibre-channel" ||
      stockage.has(`${l.a.equipement}/${l.a.interface}`) ||
      stockage.has(`${l.b.equipement}/${l.b.interface}`),
  );
}

export const GENRES_PAR_VUE = (v: Vue): GenreRelation[] => v.relations;
