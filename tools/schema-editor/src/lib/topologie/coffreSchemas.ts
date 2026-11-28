// Les schémas du coffre, lus pour la recherche dans tous les schémas (31E). Un schéma n'est relu que s'il a changé
// depuis sa dernière lecture (sa date d'écriture) : rouvrir la recherche ne relit pas tout le coffre, et taper ne
// relit rien. Les schémas archivés en sont, marqués ; ceux de la corbeille, non.

import { api, type SchemaRange } from "../api";
import type { SchemaResume } from "../generes/SchemaResume";
import { lireSchema } from "./format";
import type { Schema } from "./modele";
import type { SchemaCharge } from "./rechercheGlobale";

const lus = new Map<string, { modifieLe: string; schema: Schema }>();

/** Oublie ce qui a été lu : la prochaine lecture relit tout. */
export function oublierSchemasLus(): void {
  lus.clear();
}

/** Les schémas du coffre (ceux de la liste, puis les archivés), lus ; un schéma illisible n'y est pas. */
export async function schemasDuCoffre(): Promise<SchemaCharge[]> {
  const actifs: SchemaResume[] = await api.schemas();
  const ranges: SchemaRange[] = await api.schemasRanges();
  const tous = [
    ...actifs.map((resume) => ({ resume, archive: false })),
    ...ranges.filter((r) => r.etat === "archive").map((resume) => ({ resume, archive: true })),
  ];
  const charges: SchemaCharge[] = [];
  for (const { resume, archive } of tous) {
    const connu = lus.get(resume.id);
    let schema = connu && resume.modifieLe !== null && connu.modifieLe === resume.modifieLe ? connu.schema : undefined;
    if (!schema) {
      try {
        schema = lireSchema((await api.lireSchema(resume.id)).texte).schema;
      } catch {
        // Un schéma qui ne se lit pas ne se cherche pas : la vue Schémas le dit sur sa carte.
        lus.delete(resume.id);
        continue;
      }
      if (resume.modifieLe !== null) lus.set(resume.id, { modifieLe: resume.modifieLe, schema });
    }
    charges.push({ id: resume.id, titre: resume.titre, schema, archive });
  }
  const presents = new Set(tous.map((t) => t.resume.id));
  for (const id of [...lus.keys()]) if (!presents.has(id)) lus.delete(id);
  return charges;
}
