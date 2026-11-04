// Modèles de topologie prêts à l'emploi (D48) : un réseau type, construit avec
// les opérations de l'éditeur (équipements du catalogue, câbles de port à port,
// relations, zones), puis collé au centre du champ comme un copier-coller.

import { type Equipement, type Interface, schemaVide } from "./modele";
import {
  connecter,
  copierEquipements,
  creerEquipement,
  interfaceIndisponible,
  type PressePapiers,
  relier,
} from "./operations";

export type Gabarit = { id: string; nom: string; description: string; construire: () => PressePapiers };

type Fabrique = ReturnType<typeof fabrique>;

/** Petit atelier : créer, nommer, adresser, relier. */
function fabrique() {
  const schema = schemaVide();
  const libre = (e: Equipement, genre?: Interface["genre"]): Interface => {
    const iface = e.interfaces.find(
      (i) =>
        // Un port physique libre, ou l'accès partagé d'un nuage (Internet).
        (i.nature === "physique" || i.multiConnexion === true) &&
        i.genre !== "radio" &&
        i.genre !== "console" &&
        (genre === undefined || i.genre === genre) &&
        interfaceIndisponible(schema, { equipement: e.id, interface: i.id }) === null,
    );
    if (!iface) throw new Error(`plus de port libre sur ${e.nom}`);
    return iface;
  };
  return {
    schema,
    poser(type: string, nom: string, x: number, y: number): Equipement {
      const e = creerEquipement(schema, type, { x, y });
      e.nom = nom;
      return e;
    },
    /** Câble entre deux ports libres ; l'adresse éventuelle va sur le port de `a`, puis de `b`. */
    relier(a: Equipement, b: Equipement, adresseA?: string, adresseB?: string): void {
      const ia = libre(a);
      const ib = libre(b);
      connecter(schema, { equipement: a.id, interface: ia.id }, { equipement: b.id, interface: ib.id });
      for (const [iface, adresse] of [
        [ia, adresseA],
        [ib, adresseB],
      ] as const) {
        if (!adresse) continue;
        const [ip = "", prefixe = "24"] = adresse.split("/");
        iface.adresses = [{ adresse: ip, prefixe: Number(prefixe) }];
      }
    },
    zone(texte: string, x: number, y: number, largeur: number, hauteur: number, couleur: string): void {
      schema.annotations.push({
        id: `n_${schema.annotations.length + 1}`,
        texte,
        forme: "rectangle",
        position: { x, y },
        largeur,
        hauteur,
        couleur,
      });
    },
    contenu(): PressePapiers {
      const copie = copierEquipements(schema, new Set(schema.equipements.map((e) => e.id)));
      return { ...copie, annotations: structuredClone(schema.annotations) };
    },
  };
}

const passerelle = (f: Fabrique, e: Equipement, ip: string) => {
  e.passerelle = ip;
  return f;
};

export const GABARITS: Gabarit[] = [
  {
    id: "petit-reseau",
    nom: "Petit réseau (box)",
    description: "Box Internet, switch, deux PC, une imprimante et un point d'accès en 192.168.1.0/24.",
    construire() {
      const f = fabrique();
      const internet = f.poser("internet", "Internet", 0, -300);
      const box = f.poser("modem", "Box", 0, -150);
      const sw = f.poser("switch-non-administrable", "Switch", 0, 0);
      const pc1 = f.poser("pc", "PC 1", -240, 150);
      const pc2 = f.poser("pc", "PC 2", -80, 150);
      const imp = f.poser("imprimante", "Imprimante", 80, 150);
      const ap = f.poser("point-acces", "Point d'accès", 240, 150);
      f.relier(internet, box);
      f.relier(box, sw, "192.168.1.1/24");
      f.relier(pc1, sw, "192.168.1.10/24");
      f.relier(pc2, sw, "192.168.1.11/24");
      f.relier(imp, sw, "192.168.1.20/24");
      f.relier(ap, sw, "192.168.1.30/24");
      for (const e of [pc1, pc2, imp, ap]) passerelle(f, e, "192.168.1.1");
      f.zone("LAN 192.168.1.0/24", -320, -60, 640, 280, "#1f62b0");
      return f.contenu();
    },
  },
  {
    id: "dmz",
    nom: "Entreprise avec DMZ",
    description: "Pare-feu à trois pattes : Internet, une DMZ (serveur web) et le LAN (serveur, postes).",
    construire() {
      const f = fabrique();
      const internet = f.poser("internet", "Internet", 0, -320);
      const pf = f.poser("pare-feu", "Pare-feu", 0, -160);
      const swDmz = f.poser("switch", "SW DMZ", -300, 0);
      const web = f.poser("serveur", "Serveur web", -300, 160);
      const swLan = f.poser("switch", "SW LAN", 240, 0);
      const ad = f.poser("serveur", "Serveur AD", 80, 160);
      const pc1 = f.poser("pc", "PC 1", 240, 160);
      const pc2 = f.poser("pc", "PC 2", 400, 160);
      f.relier(internet, pf);
      f.relier(pf, swDmz, "172.16.0.1/24");
      f.relier(pf, swLan, "10.0.0.1/24");
      f.relier(web, swDmz, "172.16.0.10/24");
      f.relier(ad, swLan, "10.0.0.10/24");
      f.relier(pc1, swLan, "10.0.0.101/24");
      f.relier(pc2, swLan, "10.0.0.102/24");
      passerelle(f, web, "172.16.0.1");
      for (const e of [ad, pc1, pc2]) passerelle(f, e, "10.0.0.1");
      f.zone("DMZ 172.16.0.0/24", -420, -60, 240, 300, "#c0392b");
      f.zone("LAN 10.0.0.0/24", -20, -60, 520, 300, "#1f62b0");
      return f.contenu();
    },
  },
  {
    id: "vpn-deux-sites",
    nom: "Deux sites reliés par VPN",
    description:
      "Siège et agence, chacun derrière sa passerelle VPN, reliés par un tunnel à travers Internet.",
    construire() {
      const f = fabrique();
      const internet = f.poser("internet", "Internet", 0, -260);
      const vpnA = f.poser("passerelle-vpn", "VPN siège", -280, -120);
      const swA = f.poser("switch", "SW siège", -280, 30);
      const pcA = f.poser("pc", "PC siège", -280, 180);
      const vpnB = f.poser("passerelle-vpn", "VPN agence", 280, -120);
      const swB = f.poser("switch", "SW agence", 280, 30);
      const pcB = f.poser("pc", "PC agence", 280, 180);
      f.relier(internet, vpnA);
      f.relier(internet, vpnB);
      f.relier(vpnA, swA, "10.1.0.1/24");
      f.relier(pcA, swA, "10.1.0.10/24");
      f.relier(vpnB, swB, "10.2.0.1/24");
      f.relier(pcB, swB, "10.2.0.10/24");
      passerelle(f, pcA, "10.1.0.1");
      passerelle(f, pcB, "10.2.0.1");
      relier(f.schema, { genre: "tunnel", de: vpnA.id, vers: vpnB.id, nom: "Tunnel IPsec" });
      f.zone("Siège 10.1.0.0/24", -400, -190, 240, 450, "#1f62b0");
      f.zone("Agence 10.2.0.0/24", 160, -190, 240, 450, "#167a5a");
      return f.contenu();
    },
  },
  {
    id: "lab-virtualisation",
    nom: "Lab de virtualisation",
    description:
      "Un hôte Proxmox et ses machines virtuelles, un NAS pour le stockage, un poste d'administration.",
    construire() {
      const f = fabrique();
      const sw = f.poser("switch", "SW lab", 0, -120);
      const hote = f.poser("proxmox", "Proxmox", -200, 40);
      const nas = f.poser("nas", "NAS", 200, 40);
      const admin = f.poser("pc", "Poste admin", 0, 40);
      const vm1 = f.poser("machine-virtuelle", "VM web", -320, 220);
      const vm2 = f.poser("machine-virtuelle", "VM base", -80, 220);
      f.relier(hote, sw, "192.168.10.10/24");
      f.relier(nas, sw, "192.168.10.20/24");
      f.relier(admin, sw, "192.168.10.100/24");
      relier(f.schema, { genre: "hebergement", de: vm1.id, vers: hote.id });
      relier(f.schema, { genre: "hebergement", de: vm2.id, vers: hote.id });
      relier(f.schema, { genre: "stockage", de: hote.id, vers: nas.id, nom: "NFS" });
      f.zone("Lab 192.168.10.0/24", -420, -200, 720, 500, "#6a3fb0");
      return f.contenu();
    },
  },
];
