// Les actions sur un équipement, pour « ⋯ » dans son volet et pour le clic droit sur le plan : une seule liste, dans
// le même ordre. Avec plusieurs équipements choisis, seules restent celles qui agissent sur toute la sélection.

import { definition } from "./catalogue";
import type { ConfigurationCopiee } from "./configuration";
import type { Schema } from "./modele";

export type ActionEquipement =
  | "connecter"
  | "dupliquer"
  | "copier"
  | "verrouiller"
  | "replier"
  | "remettre-nom"
  | "copier-configuration"
  | "coller-configuration"
  | "coller-sur-la-selection"
  | "retirer";

export type EntreeEquipement = {
  action: ActionEquipement;
  nom: string;
  /** Le raccourci qui fait la même chose, écrit à droite. */
  touche?: string;
  titre?: string;
  desactivee?: boolean;
  /** En couleur d'alerte (Retirer…). */
  danger?: boolean;
  /** Un trait la sépare de l'entrée d'avant. */
  apresUnTrait?: boolean;
};

/**
 * Les entrées du menu des équipements `ids` (ceux du schéma ; les autres sont ignorés). Le mode simplifié ne garde
 * que ce qui y a un sens : ni connexion par port, ni configuration des ports.
 */
export function entreesEquipement(
  schema: Schema,
  ids: readonly string[],
  options: { simplifie: boolean; copiee: ConfigurationCopiee | null },
): EntreeEquipement[] {
  const voulus = new Set(ids);
  const equipements = schema.equipements.filter((e) => voulus.has(e.id));
  const { simplifie, copiee } = options;
  if (equipements.length === 0) return [];
  const retirer: EntreeEquipement = { action: "retirer", nom: "Retirer…", danger: true, apresUnTrait: true };

  if (equipements.length > 1) {
    const entrees: EntreeEquipement[] = [
      { action: "dupliquer", nom: "Dupliquer", touche: "Ctrl+D" },
      { action: "copier", nom: "Copier", touche: "Ctrl+C" },
    ];
    if (!simplifie && copiee && equipements.some((e) => e.id !== copiee.source)) {
      entrees.push({
        action: "coller-sur-la-selection",
        nom: `Coller la configuration de « ${copiee.nom} »`,
        titre: "Chaque port de même nom, ou à la même place, reprend le mode, les VLAN, l'activation et le PoE ; le VLAN de gestion suit. Ctrl+Z annule.",
        apresUnTrait: true,
      });
    }
    return [...entrees, retirer];
  }

  const [e] = equipements as [Schema["equipements"][number]];
  const entrees: EntreeEquipement[] = [];
  if (!simplifie) entrees.push({ action: "connecter", nom: "Connecter…" });
  entrees.push(
    { action: "dupliquer", nom: "Dupliquer", touche: "Ctrl+D" },
    { action: "copier", nom: "Copier", touche: "Ctrl+C" },
    { action: "verrouiller", nom: e.verrou ? "Déverrouiller la place" : "Verrouiller la place" },
  );
  if (definition(e.type).hebergeur)
    entrees.push({ action: "replier", nom: e.replie ? "Déplier ce qu'il héberge" : "Replier ce qu'il héberge" });
  if (e.pose)
    entrees.push({
      action: "remettre-nom",
      nom: "Remettre le nom à sa place",
      titre: "Oublie la position et l'angle de son nom : il retrouve sa place sous l'équipement",
    });
  if (!simplifie) {
    entrees.push(
      { action: "copier-configuration", nom: "Copier la configuration", apresUnTrait: true },
      {
        action: "coller-configuration",
        nom: "Coller sur…",
        desactivee: !copiee,
        titre: copiee
          ? `Reprendre la configuration de « ${copiee.nom} » sur d'autres équipements`
          : "Copiez d'abord la configuration d'un équipement",
      },
    );
  }
  return [...entrees, retirer];
}
