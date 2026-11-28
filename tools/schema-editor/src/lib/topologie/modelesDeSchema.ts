// Modèles de schéma (31E) : un nouveau schéma part d'un réseau type déjà dessiné, câblé et adressé. Les modèles
// intégrés (Petit bureau, Étage type, Site distant, Lab TP, Datacenter) sont construits avec les opérations de
// l'éditeur : de vrais types du catalogue, des câbles de port à port, des VLAN réglés aux deux bouts. Ceux de
// l'utilisateur (« Mes modèles ») sont des schémas rangés à part dans le coffre (schemas/modeles/) : un schéma
// entier, ou une seule de ses pages. Créer depuis un modèle en fait une copie aux identifiants refaits.
//
// À ne pas confondre avec les modèles matériels d'un schéma (`schema.modeles`), ni avec les modèles de topologie
// du plateau (gabarits.ts), qui se collent dans le schéma ouvert.

import {
  type ConfigVlan,
  type Equipement,
  type Interface,
  nouvelId,
  type Schema,
  schemaVide,
  type Support,
} from "./modele";
import { changerProfil, connecter, creerEquipement, relier } from "./operations";
import { pageValide, pagesDe, surLaPage } from "./pages";

/** Un modèle proposé à la création : intégré à kyfRev, ou enregistré par l'utilisateur dans son coffre. */
export type ModeleDeSchema = {
  /** Identifiant : celui du modèle intégré (« petit-bureau »), ou celui du fichier du coffre (« s_… »). */
  id: string;
  nom: string;
  description: string;
  schema: Schema;
  integre: boolean;
};

const copie = <T>(valeur: T): T => JSON.parse(JSON.stringify(valeur)) as T;

/**
 * Le même schéma, chaque identifiant refait (équipements, interfaces, câbles, relations, VLAN, agrégats, cadres,
 * annotations, ensembles, pages, modèles matériels) et chaque référence suivie : la copie ne partage rien avec
 * l'original. Le schéma reçu n'est pas modifié ; l'identifiant du schéma, lui, est vidé (le coffre en donne un).
 */
export function avecIdentifiantsNeufs(source: Schema): Schema {
  const s = copie(source);
  const ids = new Map<string, string>();
  const ports = new Map<string, string>();
  const neuf = (ancien: string, prefixe: Parameters<typeof nouvelId>[0]): string => {
    const nouveau = nouvelId(prefixe);
    if (ancien) ids.set(ancien, nouveau);
    return nouveau;
  };
  const ref = (ancien: string): string => ids.get(ancien) ?? ancien;
  const port = (equipement: string, iface: string): string => ports.get(`${equipement}/${iface}`) ?? iface;

  for (const p of s.pages ?? []) p.id = neuf(p.id, "p");
  for (const m of s.modeles) m.id = neuf(m.id, "m");
  const anciens = new Map<Equipement, string>();
  for (const e of s.equipements) {
    anciens.set(e, e.id);
    for (const i of e.interfaces) ports.set(`${e.id}/${i.id}`, nouvelId("i"));
    e.id = neuf(e.id, "d");
  }
  for (const g of s.groupes) g.id = neuf(g.id, "g");
  for (const n of s.annotations) n.id = neuf(n.id, "n");

  for (const e of s.equipements) {
    const ancien = anciens.get(e) ?? "";
    if (e.page) e.page = ref(e.page);
    for (const m of e.membres) if (m.modele) m.modele = ref(m.modele);
    for (const i of e.interfaces) {
      i.id = port(ancien, i.id);
      if (i.parent) i.parent = port(ancien, i.parent);
    }
    if (e.gestion?.interface) e.gestion.interface = port(ancien, e.gestion.interface);
  }
  for (const l of s.liens) {
    l.id = nouvelId("l");
    l.a = { equipement: ref(l.a.equipement), interface: port(l.a.equipement, l.a.interface) };
    l.b = { equipement: ref(l.b.equipement), interface: port(l.b.equipement, l.b.interface) };
  }
  for (const r of s.relations) {
    r.id = nouvelId("r");
    if (r.deInterface) r.deInterface = port(r.de, r.deInterface);
    if (r.versInterface) r.versInterface = port(r.vers, r.versInterface);
    r.de = ref(r.de);
    r.vers = ref(r.vers);
  }
  for (const v of s.vlans) v.id = nouvelId("v");
  for (const a of s.agregats) {
    a.id = nouvelId("a");
    a.membres = a.membres.map((m) => port(a.equipement, m));
    a.equipement = ref(a.equipement);
  }
  for (const g of s.groupes) {
    if (g.page) g.page = ref(g.page);
    if (g.parent) g.parent = ref(g.parent);
    g.membres = g.membres.map(ref);
  }
  for (const n of s.annotations) if (n.page) n.page = ref(n.page);
  for (const e of s.ensembles ?? []) {
    e.id = nouvelId("e");
    e.membres = e.membres.map(ref);
  }
  s.id = "";
  return s;
}

/** Le nouveau schéma tiré d'un modèle : une copie aux identifiants refaits, sous le titre choisi. */
export function schemaDepuisModele(modele: Schema, titre: string): Schema {
  const s = avecIdentifiantsNeufs(modele);
  s.titre = titre.trim() || modele.titre;
  delete s.description;
  return s;
}

/**
 * Le modèle tiré d'un schéma, ou d'une seule de ses pages : ce qui est dessiné sur la page, les câbles entre ses
 * équipements, les VLAN et les modèles matériels du schéma. Un modèle n'a qu'une page ; son titre est le nom du
 * modèle, sa description celle du modèle.
 */
export function modeleDepuisSchema(schema: Schema, nom: string, description: string, page?: string): Schema {
  const unePage = page !== undefined && pagesDe(schema).length > 1;
  const source = copie(unePage ? surLaPage(schema, pageValide(schema, page)) : schema);
  const modele = avecIdentifiantsNeufs(source);
  modele.titre = nom.trim() || schema.titre;
  if (description.trim()) modele.description = description.trim();
  else delete modele.description;
  if (unePage) {
    delete modele.pages;
    for (const element of [...modele.equipements, ...modele.groupes, ...modele.annotations]) delete element.page;
    // Un câble vers une autre page n'est pas venu : la place de ses renvois ne sert plus.
    for (const l of modele.liens) {
      delete l.poses?.["renvoi-a"];
      delete l.poses?.["renvoi-b"];
      if (l.poses && Object.keys(l.poses).length === 0) delete l.poses;
    }
  }
  return modele;
}

// --- Les modèles intégrés ---------------------------------------------------------------------------------

/** Petit atelier : poser des équipements du catalogue, les câbler port à port, régler leurs VLAN et adresses. */
function atelier(titre: string) {
  const schema = schemaVide(titre);
  const port = (e: Equipement, nom: string): Interface => {
    const i = e.interfaces.find((x) => x.nom === nom);
    if (!i) throw new Error(`pas de port « ${nom} » sur ${e.nom}`);
    return i;
  };
  const adresser = (i: Interface, adresse: string) => {
    const [ip = "", prefixe = "24"] = adresse.split("/");
    i.adresses = [...i.adresses, { adresse: ip, prefixe: Number(prefixe) }];
  };
  return {
    schema,
    port,
    adresser,
    /** Un équipement du catalogue, nommé, avec son adresse de gestion s'il en a une. */
    poser(type: string, nom: string, x: number, y: number, gestion?: string, vlanGestion?: number): Equipement {
      const e = creerEquipement(schema, type, { x, y });
      e.nom = nom;
      if (gestion) e.gestion = { adresse: gestion, ...(vlanGestion !== undefined ? { vlan: vlanGestion } : {}) };
      return e;
    },
    /**
     * Un câble du port `pa` de `a` au port `pb` de `b`. Les VLAN (`vlan`) se règlent aux deux bouts, comme sur les
     * deux équipements ; l'adresse éventuelle va sur le port de `b` (le poste, le serveur).
     */
    cabler(
      a: Equipement,
      pa: string,
      b: Equipement,
      pb: string,
      options: { vlan?: ConfigVlan; support?: Support; adresse?: string; nom?: string } = {},
    ): void {
      const ia = port(a, pa);
      const ib = port(b, pb);
      connecter(
        schema,
        { equipement: a.id, interface: ia.id },
        { equipement: b.id, interface: ib.id },
        { ...(options.support ? { support: options.support } : {}), ...(options.nom ? { nom: options.nom } : {}) },
      );
      if (options.vlan) {
        ia.vlan = { ...options.vlan };
        ib.vlan = { ...options.vlan };
      }
      if (options.adresse) adresser(ib, options.adresse);
    },
    vlan(numero: number, nom: string, sousReseau: string, couleur: string): void {
      schema.vlans.push({ id: nouvelId("v"), numero, nom, couleur, sousReseaux: [sousReseau] });
    },
    zone(texte: string, x: number, y: number, largeur: number, hauteur: number, couleur: string): void {
      schema.annotations.push({ id: nouvelId("n"), texte, forme: "rectangle", position: { x, y }, largeur, hauteur, couleur });
    },
  };
}

const acces = (vlan: number): ConfigVlan => ({ mode: "acces", acces: vlan });
const trunk = (tagues: string, natif?: number): ConfigVlan => ({
  mode: "trunk",
  tagues,
  ...(natif !== undefined ? { natif } : {}),
});

const BLEU = "#1f62b0";
const VERT = "#167a5a";
const ORANGE = "#a35a00";
const VIOLET = "#6a3fb0";
const GRIS = "#4b5563";

function petitBureau(): Schema {
  const a = atelier("Petit bureau");
  const internet = a.poser("internet", "Internet", 0, -330);
  const box = a.poser("modem", "Box", 0, -190, "192.168.1.1/24");
  const sw = a.poser("switch-non-administrable", "Switch", 0, -40);
  a.cabler(internet, "Internet", box, "PON", { support: "operateur" });
  a.cabler(sw, "8", box, "LAN1", { adresse: "192.168.1.1/24" });
  const postes = [-425, -255, -85, 85].map((x, rang) => {
    const pc = a.poser("pc", `Poste ${rang + 1}`, x, 130);
    a.cabler(sw, String(rang + 1), pc, "Ethernet", { adresse: `192.168.1.${11 + rang}/24` });
    return pc;
  });
  const imprimante = a.poser("imprimante", "Imprimante", 255, 130);
  a.cabler(sw, "5", imprimante, "Ethernet", { adresse: "192.168.1.20/24" });
  const wifi = a.poser("point-acces", "Wi-Fi", 425, 130, "192.168.1.2/24");
  a.cabler(sw, "6", wifi, "eth0", { adresse: "192.168.1.2/24" });
  for (const e of [...postes, imprimante, wifi]) e.passerelle = "192.168.1.1";
  a.zone("LAN 192.168.1.0/24", -520, -100, 1040, 310, BLEU);
  return a.schema;
}

function etageType(): Schema {
  const a = atelier("Étage type");
  a.vlan(10, "Postes", "10.1.10.0/24", BLEU);
  a.vlan(20, "Wi-Fi", "10.1.20.0/24", VERT);
  a.vlan(30, "Téléphonie", "10.1.30.0/24", ORANGE);
  a.vlan(99, "Gestion", "10.1.99.0/24", GRIS);
  const dist = a.poser("switch-distribution", "Distribution", 0, -330, "10.1.99.1/24", 99);
  const sw = a.poser("switch", "SW-Étage", 0, -170, "10.1.99.11/24", 99);
  a.cabler(dist, "1/1", sw, "1/25", { vlan: trunk("10,20,30,99") });
  for (const [rang, x] of [-420, -250].entries()) {
    const ap = a.poser("point-acces", `Borne ${rang + 1}`, x, 0, `10.1.99.${21 + rang}/24`, 99);
    a.cabler(sw, `1/${rang + 1}`, ap, "eth0", { vlan: trunk("20", 99), adresse: `10.1.99.${21 + rang}/24` });
  }
  const postes: [string, string, string, number, number][] = [
    ["Prise 1.01", "pc", "PC Bureau 101", 10, 11],
    ["Prise 1.02", "pc", "PC Bureau 102", 10, 12],
    ["Prise 1.03", "telephone-ip", "Téléphone 103", 30, 13],
  ];
  for (const [rang, [repere, type, nom, vlan, hote]] of postes.entries()) {
    const x = -60 + rang * 190;
    const prise = a.poser("prise-reseau", repere, x, 0);
    const poste = a.poser(type, nom, x, 170);
    a.cabler(sw, `1/${rang + 3}`, prise, "Prise", { vlan: acces(vlan) });
    a.cabler(prise, "Prise", poste, type === "pc" ? "Ethernet" : "LAN", {
      vlan: acces(vlan),
      adresse: `10.1.${vlan}.${hote}/24`,
    });
    poste.passerelle = `10.1.${vlan}.1`;
  }
  return a.schema;
}

function siteDistant(): Schema {
  const a = atelier("Site distant");
  a.vlan(10, "LAN du site", "10.2.0.0/24", BLEU);
  const internet = a.poser("internet", "Internet", -170, -330);
  const siege = a.poser("reseau-distant", "Siège", 230, -330);
  const routeur = a.poser("routeur", "Routeur VPN", -170, -180, "10.2.0.1/24");
  a.cabler(internet, "Internet", routeur, "WAN1", { support: "operateur" });
  // Le siège est joint à travers Internet ; le tunnel, lui, se lit dans la vue logique.
  a.cabler(internet, "Internet", siege, "Réseau", { support: "operateur" });
  relier(a.schema, { genre: "tunnel", de: routeur.id, vers: siege.id, nom: "Tunnel IPsec" });
  const sw = a.poser("switch", "SW-Site", -170, -30, "10.2.0.2/24", 10);
  a.cabler(sw, "1/24", routeur, "LAN1", { vlan: acces(10), adresse: "10.2.0.1/24" });
  const nas = a.poser("nas", "NAS", -450, 130, "10.2.0.10/24");
  a.cabler(sw, "1/1", nas, "LAN1", { vlan: acces(10), adresse: "10.2.0.10/24" });
  const postes = [-260, -80].map((x, rang) => {
    const pc = a.poser("pc", `Poste ${rang + 1}`, x, 130);
    a.cabler(sw, `1/${rang + 2}`, pc, "Ethernet", { vlan: acces(10), adresse: `10.2.0.${21 + rang}/24` });
    return pc;
  });
  const imprimante = a.poser("imprimante", "Imprimante", 100, 130);
  a.cabler(sw, "1/4", imprimante, "Ethernet", { vlan: acces(10), adresse: "10.2.0.30/24" });
  for (const e of [nas, ...postes, imprimante]) e.passerelle = "10.2.0.1";
  return a.schema;
}

function labTp(): Schema {
  const a = atelier("Lab TP");
  a.vlan(10, "Postes", "10.1.10.0/24", BLEU);
  a.vlan(20, "Serveurs", "10.1.20.0/24", VIOLET);
  const r1 = a.poser("routeur", "R1", -250, -280, "10.255.0.1/30");
  const r2 = a.poser("routeur", "R2", 250, -280, "10.255.0.2/30");
  a.cabler(r1, "WAN1", r2, "WAN1", { nom: "Liaison R1-R2", adresse: "10.255.0.2/30" });
  a.adresser(a.port(r1, "WAN1"), "10.255.0.1/30");
  for (const [ilot, routeur, x] of [
    [1, r1, -250],
    [2, r2, 250],
  ] as const) {
    const sw = a.poser("switch", `SW${ilot}`, x, -110, `10.${ilot}.10.2/24`, 10);
    a.cabler(sw, "1/24", routeur, "LAN1", { vlan: trunk("10,20") });
    // Le routeur sur un bâton : une sous-interface par VLAN, chacune passerelle de son réseau.
    const lan = a.port(routeur, "LAN1");
    for (const vlan of [10, 20]) {
      routeur.interfaces.push({
        id: nouvelId("i"),
        nom: `LAN1.${vlan}`,
        genre: "virtuel",
        nature: "sous-interface",
        parent: lan.id,
        admin: "actif",
        macs: [],
        adresses: [{ adresse: `10.${ilot}.${vlan}.1`, prefixe: 24 }],
        vlan: acces(vlan),
      });
    }
    for (const [rang, vlan] of [10, 20].entries()) {
      const pc = a.poser("pc", `PC${ilot}-${rang + 1}`, x - 100 + rang * 200, 60);
      a.cabler(sw, `1/${rang + 1}`, pc, "Ethernet", { vlan: acces(vlan), adresse: `10.${ilot}.${vlan}.11/24` });
      pc.passerelle = `10.${ilot}.${vlan}.1`;
    }
    a.zone(`Îlot ${ilot}`, x - 190, -190, 380, 340, ilot === 1 ? BLEU : VERT);
  }
  // Les VLAN servent aux deux îlots : chacun son réseau.
  for (const v of a.schema.vlans) v.sousReseaux = [`10.1.${v.numero}.0/24`, `10.2.${v.numero}.0/24`];
  return a.schema;
}

function datacenter(): Schema {
  const a = atelier("Datacenter");
  a.vlan(10, "Web", "10.0.10.0/24", BLEU);
  a.vlan(20, "Données", "10.0.20.0/24", VIOLET);
  a.vlan(99, "Gestion", "10.0.99.0/24", GRIS);
  const internet = a.poser("internet", "Internet", 0, -470);
  const pareFeux = [-200, 200].map((x, rang) => {
    const fw = a.poser("pare-feu", `FW-${rang + 1}`, x, -320, `10.0.99.${2 + rang}/24`, 99);
    // Deux ports SFP+ en plus des ports cuivre : la montée vers le cœur est en fibre, à 10 Gbit/s.
    changerProfil(a.schema, fw.id, (e) => {
      const membre = e.membres[0];
      if (membre?.groupes) {
        membre.groupes.push({ nom: "SFP+", motif: "x{p}", nombre: 2, premier: 1, genre: "fibre", connecteur: "SFP+", vitesse: 10000, role: "uplink" });
      }
    });
    a.cabler(internet, "Internet", fw, "port1", { support: "operateur" });
    return fw;
  });
  const [fw1, fw2] = pareFeux as [Equipement, Equipement];
  a.cabler(fw1, "port8", fw2, "port8", { support: "haute-disponibilite", nom: "Lien HA" });
  relier(a.schema, { genre: "haute-disponibilite", de: fw1.id, vers: fw2.id });
  const coeur = a.poser("switch-coeur", "Cœur", 0, -160, "10.0.99.1/24", 99);
  a.cabler(fw1, "x1", coeur, "1/1", { vlan: trunk("10,20,99") });
  a.cabler(fw2, "x1", coeur, "1/2", { vlan: trunk("10,20,99") });
  const serveurs: [string, string, number, number][] = [
    ["SRV-Web-1", "serveur", 10, 11],
    ["SRV-Web-2", "serveur", 10, 12],
    ["SRV-BDD-1", "serveur", 20, 11],
    ["Stockage", "nas", 20, 20],
  ];
  for (const [rang, cote] of ["A", "B"].entries()) {
    const x = rang === 0 ? -260 : 260;
    const tor = a.poser("switch", `ToR-${cote}`, x, 0, `10.0.99.${11 + rang}/24`, 99);
    a.cabler(coeur, `1/${rang + 3}`, tor, "1/25", { vlan: trunk("10,20,99") });
    for (const [n, [nom, type, vlan, hote]] of serveurs.slice(rang * 2, rang * 2 + 2).entries()) {
      const srv = a.poser(type, nom, x - 130 + n * 260, 170, `10.0.99.${21 + rang * 2 + n}/24`, 99);
      a.cabler(tor, `1/${n + 1}`, srv, type === "nas" ? "LAN1" : "eno1", {
        vlan: acces(vlan),
        adresse: `10.0.${vlan}.${hote}/24`,
      });
      srv.passerelle = `10.0.${vlan}.1`;
    }
  }
  return a.schema;
}

/** Les modèles intégrés, dans l'ordre où ils sont proposés. Chacun se construit à neuf à chaque appel. */
export const MODELES_INTEGRES: { id: string; nom: string; description: string; construire: () => Schema }[] = [
  { id: "petit-bureau", nom: "Petit bureau", description: "Box, switch, Wi-Fi, 4 postes", construire: petitBureau },
  { id: "etage-type", nom: "Étage type", description: "Switch d'étage, bornes, prises", construire: etageType },
  { id: "site-distant", nom: "Site distant", description: "Routeur VPN, switch, NAS", construire: siteDistant },
  { id: "lab-tp", nom: "Lab TP", description: "2 routeurs, 2 switchs, VLAN 10/20", construire: labTp },
  { id: "datacenter", nom: "Datacenter", description: "Cœur, pare-feux HA, serveurs", construire: datacenter },
];

/** Les modèles intégrés, prêts à proposer. */
export const modelesIntegres = (): ModeleDeSchema[] =>
  MODELES_INTEGRES.map((m) => ({ id: m.id, nom: m.nom, description: m.description, schema: m.construire(), integre: true }));
