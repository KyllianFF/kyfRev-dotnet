// Plan d'adressage IP d'un schéma (D47) : toutes les adresses, rangées par
// sous-réseau, avec leur VLAN, les passerelles et les doublons. Lecture seule :
// rien n'est modifié ici.

import type { Schema } from "./modele";

export type LigneIp = {
  adresse: string;
  prefixe?: number;
  equipement: string;
  nomEquipement: string;
  /** Interface portant l'adresse ; absente pour une adresse de gestion seule. */
  nomInterface?: string;
  vlan?: number;
  gestion: boolean;
  /** Même adresse ailleurs dans le schéma. */
  doublon: boolean;
};

export type SousReseau = {
  /** « 192.168.1.0/24 », ou null (adresses sans masque connu, IPv6). */
  reseau: string | null;
  vlans: string[];
  passerelles: string[];
  /** Adresses utilisables (hors réseau et diffusion). */
  capacite: number | null;
  lignes: LigneIp[];
};

export type PlanIp = { sousReseaux: SousReseau[]; total: number; doublons: number };

/** Adresse IPv4 en entier (ou null). */
export function ipv4(texte: string): number | null {
  const parties = texte.trim().split(".");
  if (parties.length !== 4) return null;
  let n = 0;
  for (const p of parties) {
    if (!/^\d{1,3}$/.test(p)) return null;
    const v = Number(p);
    if (v > 255) return null;
    n = n * 256 + v;
  }
  return n;
}

const texteIpv4 = (n: number) => [24, 16, 8, 0].map((d) => Math.floor(n / 2 ** d) % 256).join(".");

/** Réseau d'une adresse IPv4 (« 192.168.1.0/24 »). */
export function reseauDe(adresse: string, prefixe: number): string | null {
  const n = ipv4(adresse);
  if (n === null || prefixe < 0 || prefixe > 32) return null;
  const taille = 2 ** (32 - prefixe);
  return `${texteIpv4(Math.floor(n / taille) * taille)}/${prefixe}`;
}

/** Adresses utilisables d'un préfixe. */
export const capaciteDe = (prefixe: number): number =>
  prefixe >= 32 ? 1 : prefixe === 31 ? 2 : 2 ** (32 - prefixe) - 2;

/** Une adresse écrite « a.b.c.d/p » ou seule. */
function separer(texte: string): { adresse: string; prefixe?: number } {
  const [adresse = "", prefixe] = texte.trim().split("/");
  const p = prefixe !== undefined ? Number(prefixe) : Number.NaN;
  return Number.isInteger(p) && p >= 0 && p <= 128 ? { adresse, prefixe: p } : { adresse };
}

/** Le plan d'adressage du schéma. */
export function planIp(schema: Schema): PlanIp {
  const lignes: LigneIp[] = [];
  for (const e of schema.equipements) {
    const vues = new Set<string>();
    for (const i of e.interfaces) {
      for (const a of i.adresses) {
        if (!a.adresse.trim()) continue;
        vues.add(a.adresse.trim());
        lignes.push({
          adresse: a.adresse.trim(),
          ...(a.prefixe !== undefined ? { prefixe: a.prefixe } : {}),
          equipement: e.id,
          nomEquipement: e.nom,
          nomInterface: i.nom,
          ...(i.vlan?.acces !== undefined ? { vlan: i.vlan.acces } : {}),
          gestion: e.gestion?.interface === i.id,
          doublon: false,
        });
      }
    }
    if (e.gestion?.adresse) {
      const { adresse, prefixe } = separer(e.gestion.adresse);
      if (adresse && !vues.has(adresse)) {
        lignes.push({
          adresse,
          ...(prefixe !== undefined ? { prefixe } : {}),
          equipement: e.id,
          nomEquipement: e.nom,
          ...(e.gestion.vlan !== undefined ? { vlan: e.gestion.vlan } : {}),
          gestion: true,
          doublon: false,
        });
      }
    }
  }

  // Doublons : la même adresse sur deux lignes.
  const comptes = new Map<string, number>();
  for (const l of lignes) comptes.set(l.adresse, (comptes.get(l.adresse) ?? 0) + 1);
  for (const l of lignes) l.doublon = (comptes.get(l.adresse) ?? 0) > 1;

  // Sous-réseaux connus : ceux des adresses à masque, et ceux déclarés dans les VLAN.
  const declares = schema.vlans.flatMap((v) =>
    v.sousReseaux.map((s) => ({ ...separer(s), vlan: `${v.numero} ${v.nom}`.trim() })),
  );
  const reseauDeLigne = (l: LigneIp): string | null => {
    if (ipv4(l.adresse) === null) return null;
    if (l.prefixe !== undefined) return reseauDe(l.adresse, l.prefixe);
    // Sans masque : le sous-réseau déclaré (ou déjà vu) qui la contient.
    const candidats = [
      ...declares.filter((d) => d.prefixe !== undefined),
      ...lignes
        .filter((x) => x.prefixe !== undefined)
        .map((x) => ({ adresse: x.adresse, prefixe: x.prefixe })),
    ];
    for (const c of candidats) {
      const r = reseauDe(c.adresse, c.prefixe ?? 32);
      if (r && r === reseauDe(l.adresse, c.prefixe ?? 32)) return r;
    }
    return null;
  };

  const groupes = new Map<string, LigneIp[]>();
  for (const l of lignes) {
    const cle = reseauDeLigne(l) ?? "";
    groupes.set(cle, [...(groupes.get(cle) ?? []), l]);
  }
  const ordre = (a: string, b: string) => {
    const na = ipv4(a.split("/")[0] ?? "");
    const nb = ipv4(b.split("/")[0] ?? "");
    if (na === null || nb === null) return na === null ? 1 : -1;
    return na - nb;
  };
  const sousReseaux: SousReseau[] = [...groupes.entries()]
    .sort(([a], [b]) => (a === "" ? 1 : b === "" ? -1 : ordre(a, b)))
    .map(([reseau, ls]) => {
      const prefixe = reseau ? Number(reseau.split("/")[1]) : null;
      const vlans = new Set<string>();
      for (const d of declares) {
        if (reseau && d.prefixe !== undefined && reseauDe(d.adresse, d.prefixe) === reseau) vlans.add(d.vlan);
      }
      for (const l of ls) if (l.vlan !== undefined && vlans.size === 0) vlans.add(String(l.vlan));
      const passerelles = new Set<string>();
      for (const l of ls) {
        const p = schema.equipements.find((e) => e.id === l.equipement)?.passerelle;
        if (p && reseau && prefixe !== null && reseauDe(p, prefixe) === reseau) passerelles.add(p);
      }
      return {
        reseau: reseau || null,
        vlans: [...vlans],
        passerelles: [...passerelles],
        capacite: prefixe === null ? null : capaciteDe(prefixe),
        lignes: ls.sort((a, b) => ordre(a.adresse, b.adresse)),
      };
    });
  return { sousReseaux, total: lignes.length, doublons: lignes.filter((l) => l.doublon).length };
}

/** Le plan en CSV (point-virgule, comme l'attend un tableur en français). */
export function planEnCsv(plan: PlanIp): string {
  const champ = (v: string | number | undefined) => {
    const t = v === undefined ? "" : String(v);
    return /[;"\n]/.test(t) ? `"${t.replaceAll('"', '""')}"` : t;
  };
  const lignes = [
    ["Sous-réseau", "Adresse", "Préfixe", "Équipement", "Interface", "VLAN", "Gestion", "Doublon"].join(";"),
  ];
  for (const s of plan.sousReseaux) {
    for (const l of s.lignes) {
      lignes.push(
        [
          s.reseau ?? "",
          l.adresse,
          l.prefixe,
          l.nomEquipement,
          l.nomInterface,
          l.vlan,
          l.gestion ? "oui" : "",
          l.doublon ? "oui" : "",
        ]
          .map(champ)
          .join(";"),
      );
    }
  }
  return `${lignes.join("\r\n")}\r\n`;
}
