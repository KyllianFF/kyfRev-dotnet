// Lire et écrire les schémas qu'on réunit (31C) ou dont on échange des pages (31D). Le schéma ouvert dans l'éditeur se
// lit dans l'éditeur (ce qu'on y a fait n'est peut-être pas encore enregistré) et ne s'écrit que par lui : ses
// opérations s'annulent par Ctrl+Z, et rien ne vient écraser son fichier dans son dos.

import { api } from "../api";
import type { EditeurSchema } from "./etat.svelte";
import { ecrireSchema, lireSchema } from "./format";
import type { Schema } from "./modele";

let ouvert: EditeurSchema | null = null;

/** L'éditeur qui s'ouvre se fait connaître ; la fonction rendue l'oublie, quand il se ferme. */
export function inscrireSchemaOuvert(editeur: EditeurSchema): () => void {
  ouvert = editeur;
  return () => {
    if (ouvert === editeur) ouvert = null;
  };
}

/** L'éditeur où le schéma `id` est ouvert, s'il l'est. */
export const editeurDe = (id: string | null | undefined): EditeurSchema | null => (id && ouvert?.id === id ? ouvert : null);

/** Un schéma lu pour un échange : son contenu, l'empreinte de la version lue, l'éditeur qui l'a ouvert. */
export type SchemaEchange = { id: string; schema: Schema; empreinte: string; editeur: EditeurSchema | null };

/** Lit le schéma `id` : dans l'éditeur s'il y est ouvert, sinon dans le coffre. */
export async function lireEchange(id: string): Promise<SchemaEchange> {
  const editeur = editeurDe(id);
  if (editeur) {
    return { id, schema: JSON.parse(JSON.stringify(editeur.schema)) as Schema, empreinte: editeur.empreinte, editeur };
  }
  const lu = await api.lireSchema(id);
  const { schema, erreurs } = lireSchema(lu.texte);
  if (erreurs.length > 0) throw new Error(`« ${schema.titre} » contient ${erreurs.length} erreur(s) de structure : corrigez-les d'abord.`);
  return { id: lu.id, schema, empreinte: lu.empreinte, editeur: null };
}

/** Enregistre un schéma lu par `lireEchange` (pas celui de l'éditeur) ; une version changée entre-temps est refusée. */
export async function ecrireEchange(lu: SchemaEchange, schema: Schema): Promise<void> {
  const resultat = await api.enregistrerSchema(lu.id, ecrireSchema({ ...schema, id: lu.id }), lu.empreinte);
  if (resultat.statut === "conflit") {
    throw new Error(`« ${lu.schema.titre} » a été modifié ailleurs entre-temps : rien n'y a été écrit.`);
  }
}

/** Crée un schéma dans le coffre ; rend son identifiant. */
export async function creerEchange(schema: Schema): Promise<string> {
  return (await api.creerSchema(ecrireSchema({ ...schema, id: "" }))).id;
}
