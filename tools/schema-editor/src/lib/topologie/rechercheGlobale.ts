// Recherche dans tous les schémas du coffre (31E) : un équipement (son nom, son nom d'hôte), une adresse IP (entière,
// son début « 10.0.30. », ou un réseau « 10.0.30.0/24 ») ou un VLAN (son numéro, son nom). Chaque résultat dit son
// schéma et sa page. Une même adresse portée par deux équipements, dans deux schémas ou dans un seul, est signalée
// sans rien bloquer : ce peut être deux réseaux distincts. Les schémas se lisent une fois (coffreSchemas.ts) et
// s'indexent une fois ; seule la recherche se refait à chaque frappe.

import { ipv4 } from "./adressage";
import { grappesHauteDispo } from "./diagnostic";
import { adresseAffichee } from "./dessin";
import type { Equipement, Schema, Vlan } from "./modele";
import { aPlusieursPages, nomDeLaPage, pageDeLElement } from "./pages";

/** Une recherche commence à deux caractères. */
export const RECHERCHE_MIN = 2;
/** Résultats montrés par groupe, avant « N de plus ». */
export const PLAFOND = 6;
/** Résultats montrés par groupe, une fois « N de plus » choisi. */
export const PLAFOND_OUVERT = 200;

/** Un schéma du coffre, lu. Un schéma archivé se cherche aussi, marqué comme tel ; la corbeille, non. */
export type SchemaCharge = { id: string; titre: string; schema: Schema; archive?: boolean };

export type GenreResultat = "equipement" | "adresse" | "vlan";

export type Resultat = {
  /** Clé unique dans la réponse. */
  cle: string;
  genre: GenreResultat;
  schemaId: string;
  schemaTitre: string;
  archive: boolean;
  /** La page de l'équipement, dans un schéma à plusieurs pages. */
  pageId?: string;
  pageNom?: string;
  /** L'équipement à montrer ; rien pour un VLAN (il vaut pour tout le schéma). */
  objetId?: string;
  nom: string;
  /** Type du catalogue de l'équipement, pour son icône. */
  type?: string;
  /** L'adresse montrée (avec son préfixe s'il est connu). */
  adresse?: string;
  /** Ce qui correspond, quand ce n'est ni le nom ni l'adresse (« nom d'hôte sw-etage1 », le nom du VLAN). */
  detail?: string;
  /** La couleur d'un VLAN. */
  couleur?: string;
  /** L'adresse sert aussi à un autre équipement, ici ou dans un autre schéma. */
  memeAdresse?: boolean;
};

export type GroupeResultats = {
  genre: GenreResultat;
  titre: string;
  resultats: Resultat[];
  /** Ceux qui ne sont pas montrés : « N de plus ». */
  restants: number;
};

/** Le conseil discret qui accompagne une adresse en double. */
export type Conseil = { adresse: string; texte: string };

export type ReponseRecherche = { groupes: GroupeResultats[]; conseils: Conseil[]; total: number; schemas: number };

type Lieu = { schemaId: string; schemaTitre: string; archive: boolean; pageId?: string; pageNom?: string };
type AdresseIndexee = { cle: string; affichee: string; valeur: number | null };
type EquipementIndexe = {
  lieu: Lieu;
  equipement: Equipement;
  nom: string;
  hote: string;
  adresses: AdresseIndexee[];
  principale: string;
};
type VlanIndexe = { lieu: Lieu; vlan: Vlan; nom: string };
type Usage = { schemaId: string; schemaTitre: string; proprietaire: string };

/** Les schémas, prêts à chercher. */
export type Index = {
  equipements: EquipementIndexe[];
  vlans: VlanIndexe[];
  /** Pour chaque adresse : qui la porte (un équipement, ou une paire haute disponibilité). */
  usages: Map<string, Usage[]>;
  schemas: number;
};

/** Sans accents, sans casse, ligatures défaites : « Cœur » se trouve en tapant « coeur ». */
const simple = (texte: string) =>
  texte.normalize("NFD").replace(/\p{Diacritic}/gu, "").toLowerCase().replaceAll("œ", "oe").replaceAll("æ", "ae");

/** La forme comparée d'une adresse : sans préfixe, en minuscules. */
const cleAdresse = (adresse: string) => adresse.trim().split("/")[0]?.toLowerCase() ?? "";

/** Les adresses qu'on ne compte pas en double : aucune, ou locales au lien. */
const ignoree = (cle: string) => cle === "" || cle === "0.0.0.0" || cle.startsWith("fe80:") || cle === "::1" || cle.startsWith("127.");

/** Indexe les schémas une fois : leurs équipements, leurs adresses, leurs VLAN, et qui porte chaque adresse. */
export function indexer(charges: SchemaCharge[]): Index {
  const equipements: EquipementIndexe[] = [];
  const vlans: VlanIndexe[] = [];
  const usages = new Map<string, Usage[]>();
  for (const c of charges) {
    const archive = c.archive === true;
    const lieu: Lieu = { schemaId: c.id, schemaTitre: c.titre || c.schema.titre, archive };
    const pages = aPlusieursPages(c.schema);
    // Une paire haute disponibilité porte légitimement une même adresse (VIP) : elle compte pour un.
    const grappes = grappesHauteDispo(c.schema);
    for (const e of c.schema.equipements) {
      const page = pages ? pageDeLElement(c.schema, e) : undefined;
      const ici: Lieu = page !== undefined ? { ...lieu, pageId: page, pageNom: nomDeLaPage(c.schema, page) } : lieu;
      const vues = new Map<string, AdresseIndexee>();
      const noter = (affichee: string) => {
        const cle = cleAdresse(affichee);
        if (!cle || vues.has(cle)) return;
        vues.set(cle, { cle, affichee: affichee.trim(), valeur: ipv4(cle) });
      };
      for (const i of e.interfaces) {
        for (const a of i.adresses) noter(`${a.adresse}${a.prefixe !== undefined ? `/${a.prefixe}` : ""}`);
      }
      if (e.gestion?.adresse) noter(e.gestion.adresse);
      const adresses = [...vues.values()];
      equipements.push({
        lieu: ici,
        equipement: e,
        nom: simple(e.nom),
        hote: simple(e.nomHote ?? ""),
        adresses,
        principale: adresseAffichee(e),
      });
      const proprietaire = grappes.get(e.id) ?? e.id;
      for (const a of adresses) {
        if (ignoree(a.cle)) continue;
        const liste = usages.get(a.cle) ?? [];
        if (!liste.some((u) => u.schemaId === c.id && u.proprietaire === proprietaire)) {
          liste.push({ schemaId: c.id, schemaTitre: lieu.schemaTitre, proprietaire });
        }
        usages.set(a.cle, liste);
      }
    }
    for (const v of c.schema.vlans) vlans.push({ lieu, vlan: v, nom: simple(v.nom) });
  }
  return { equipements, vlans, usages, schemas: charges.length };
}

/** Ce que demande le texte, s'il s'écrit comme une adresse : son début, ou un réseau IPv4 (« 10.0.30.0/24 »). */
function requeteAdresse(texte: string): { debut: string } | { de: number; a: number } | null {
  if (!/^[0-9a-f.:/]+$/i.test(texte) || !/[.:]/.test(texte)) return null;
  const [ip = "", prefixe] = texte.split("/");
  if (prefixe !== undefined && prefixe !== "") {
    const n = ipv4(ip);
    const p = Number(prefixe);
    if (n !== null && Number.isInteger(p) && p >= 0 && p <= 32) {
      const taille = 2 ** (32 - p);
      const de = Math.floor(n / taille) * taille;
      return { de, a: de + taille };
    }
  }
  // Un réseau en cours de saisie (« 10.0.30.0/ ») : ses adresses commencent par « 10.0.30. ».
  if (prefixe !== undefined && ipv4(ip) !== null) return { debut: ip.replace(/(?:\.0)+$/, ".") };
  return { debut: ip.toLowerCase() };
}

/** Le numéro de VLAN demandé (« 30 », « vlan 30 », « VLAN30 ») ; sinon, le nom cherché (vide : tous les VLAN). */
function requeteVlan(texte: string): { numero: number } | { nom: string } {
  const numero = /^(?:vlan\s*)?(\d{1,4})$/i.exec(texte);
  if (numero) return { numero: Number(numero[1]) };
  return { nom: simple(texte.replace(/^vlan\b\s*/i, "")) };
}

const NOMBRES = ["", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf", "dix"];
const enLettres = (n: number) => NOMBRES[n] ?? String(n);

/** Combien d'équipements (ou de paires) portent cette adresse, et dans combien de schémas. */
function usagesDe(index: Index, adresse: string): Usage[] {
  const cle = cleAdresse(adresse);
  return ignoree(cle) ? [] : (index.usages.get(cle) ?? []);
}

/** Le conseil d'une adresse en double : dans deux schémas, ce peut être deux réseaux ; dans un seul, c'est à vérifier. */
export function conseilDe(index: Index, adresse: string): Conseil | null {
  const usages = usagesDe(index, adresse);
  if (usages.length < 2) return null;
  const ip = cleAdresse(adresse);
  const schemas = new Set(usages.map((u) => u.schemaId));
  const texte =
    schemas.size > 1
      ? `${ip} sert dans ${enLettres(schemas.size)} schémas : à vérifier s'ils sont sur le même réseau.`
      : `${ip} sert à ${enLettres(usages.length)} équipements de « ${usages[0]?.schemaTitre ?? ""} » : à vérifier.`;
  return { adresse: ip, texte };
}

const parNom = (a: { nom: string; schemaTitre: string }, b: { nom: string; schemaTitre: string }) =>
  a.nom.localeCompare(b.nom, "fr") || a.schemaTitre.localeCompare(b.schemaTitre, "fr");

/**
 * Cherche `texte` dans les schémas indexés, à partir de deux caractères. Les résultats sont groupés (équipements,
 * adresses, VLAN) et chaque groupe montre `PLAFOND` résultats, sauf ceux de `ouverts` (« N de plus » choisi).
 */
export function chercherPartout(index: Index, texte: string, ouverts: ReadonlySet<GenreResultat> = new Set()): ReponseRecherche {
  const t = texte.trim();
  if (t.length < RECHERCHE_MIN) return { groupes: [], conseils: [], total: 0, schemas: index.schemas };
  const voulu = simple(t);
  const marque = (r: Resultat): Resultat => (r.adresse && usagesDe(index, r.adresse).length > 1 ? { ...r, memeAdresse: true } : r);

  // Les équipements par leur nom ou leur nom d'hôte : ceux qui commencent par le texte d'abord.
  const parLeNom: (Resultat & { debut: boolean })[] = [];
  for (const x of index.equipements) {
    const parHote = !x.nom.includes(voulu) && x.hote.includes(voulu);
    if (!x.nom.includes(voulu) && !parHote) continue;
    parLeNom.push({
      ...x.lieu,
      cle: `e:${x.lieu.schemaId}:${x.equipement.id}`,
      genre: "equipement",
      objetId: x.equipement.id,
      nom: x.equipement.nom,
      type: x.equipement.type,
      ...(x.principale ? { adresse: x.principale } : {}),
      ...(parHote ? { detail: `nom d'hôte ${x.equipement.nomHote}` } : {}),
      debut: (parHote ? x.hote : x.nom).startsWith(voulu),
    });
  }
  parLeNom.sort((a, b) => Number(b.debut) - Number(a.debut) || parNom(a, b));

  // Les adresses : une ligne par équipement et par adresse qui correspond, dans l'ordre des adresses.
  const requete = requeteAdresse(t);
  const parAdresse: (Resultat & { valeur: number | null })[] = [];
  if (requete) {
    for (const x of index.equipements) {
      for (const a of x.adresses) {
        const correspond =
          "debut" in requete ? a.cle.startsWith(requete.debut) : a.valeur !== null && a.valeur >= requete.de && a.valeur < requete.a;
        if (!correspond) continue;
        parAdresse.push({
          ...x.lieu,
          cle: `a:${x.lieu.schemaId}:${x.equipement.id}:${a.cle}`,
          genre: "adresse",
          objetId: x.equipement.id,
          nom: x.equipement.nom,
          type: x.equipement.type,
          adresse: a.affichee,
          valeur: a.valeur,
        });
      }
    }
    parAdresse.sort(
      (a, b) =>
        (a.valeur ?? Number.POSITIVE_INFINITY) - (b.valeur ?? Number.POSITIVE_INFINITY) ||
        (a.adresse ?? "").localeCompare(b.adresse ?? "") ||
        parNom(a, b),
    );
  }

  // Les VLAN : par numéro, ou par nom.
  const vlan = requeteVlan(t);
  const parVlan: Resultat[] = index.vlans
    .filter((x) => ("numero" in vlan ? x.vlan.numero === vlan.numero : x.nom.includes(vlan.nom)))
    .map((x) => ({
      ...x.lieu,
      cle: `v:${x.lieu.schemaId}:${x.vlan.id}`,
      genre: "vlan" as const,
      nom: `VLAN ${x.vlan.numero}`,
      detail: x.vlan.nom,
      ...(x.vlan.sousReseaux.length > 0 ? { adresse: x.vlan.sousReseaux.join(", ") } : {}),
      ...(x.vlan.couleur ? { couleur: x.vlan.couleur } : {}),
    }))
    .sort((a, b) => Number(a.nom.slice(5)) - Number(b.nom.slice(5)) || a.schemaTitre.localeCompare(b.schemaTitre, "fr"));

  const groupe = (
    genre: GenreResultat,
    titre: string,
    tous: (Resultat & { debut?: boolean; valeur?: number | null })[],
  ): GroupeResultats => {
    const plafond = ouverts.has(genre) ? PLAFOND_OUVERT : PLAFOND;
    const montres = tous.slice(0, plafond).map(({ debut: _debut, valeur: _valeur, ...r }) => (r.genre === "vlan" ? r : marque(r)));
    return { genre, titre, resultats: montres, restants: tous.length - montres.length };
  };
  const groupes = [
    groupe("equipement", "Équipements", parLeNom),
    groupe("adresse", "Adresses IP", parAdresse),
    groupe("vlan", "VLAN", parVlan),
  ].filter((g) => g.resultats.length > 0);

  // Un conseil par adresse en double montrée, trois au plus.
  const conseils: Conseil[] = [];
  for (const r of groupes.flatMap((g) => g.resultats)) {
    if (!r.memeAdresse || !r.adresse || conseils.length >= 3) continue;
    const conseil = conseilDe(index, r.adresse);
    if (conseil && !conseils.some((c) => c.adresse === conseil.adresse)) conseils.push(conseil);
  }
  return {
    groupes,
    conseils,
    total: parLeNom.length + parAdresse.length + parVlan.length,
    schemas: index.schemas,
  };
}
