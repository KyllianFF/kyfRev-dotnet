import { describe, expect, it } from "vitest";
import { dessinerSchema, mettreEnPage } from "./dessin";
import { diagnostiquer } from "./diagnostic";
import { ecrireSchema, lireSchema } from "./format";
import { type Equipement, interfaceDe, interfaceOccupee, type Schema, schemaVide } from "./modele";
import { connecter, creerEquipement, Refus } from "./operations";
import {
  aDesReglages,
  AIDE_AGREGAT_MINIMUM,
  AIDE_AGREGAT_SEUL,
  agreger,
  alignerVlans,
  CABLES_MAX,
  CLE_MODE,
  cablesEntre,
  changerTypeDeCable,
  conseilsDeLiaison,
  desagreger,
  ecrireAdresse,
  ecrireVlansDuTrunk,
  effacer,
  epurer,
  groupeDeCables,
  lireAdresse,
  lireMode,
  lireVlansDuTrunk,
  mettreEnTrunk,
  portsLibres,
  reglerMembres,
  reglerNombreDeCables,
  retenirMode,
  retirerCable,
  tirerCable,
} from "./simplifie";
import {
  agregatDe,
  basculerMembre,
  configDuBout,
  creerAgregat,
  ecrireConfigDuBout,
  enAgregat,
  estTrunk as enTrunk,
} from "./vlans";
import { vue } from "./vues";

const nomDuPort = (schema: Schema, bout: { equipement: string; interface: string }) =>
  interfaceDe(schema, bout)?.nom;
const port = (e: Equipement, nom: string) => {
  const i = e.interfaces.find((x) => x.nom === nom);
  if (!i) throw new Error(`port ${nom} absent`);
  return { equipement: e.id, interface: i.id };
};
const avertissementsDeSupport = (schema: Schema) =>
  diagnostiquer(schema).filter((c) => c.message.includes("support"));

/** Deux switches, un cœur, un serveur, un PC et Internet. */
function site() {
  const schema = schemaVide("Mode simplifié");
  const sw1 = creerEquipement(schema, "switch", { x: 0, y: 0 });
  const sw2 = creerEquipement(schema, "switch", { x: 400, y: 0 });
  const coeur = creerEquipement(schema, "switch-coeur", { x: 200, y: -200 });
  const serveur = creerEquipement(schema, "serveur", { x: 600, y: -200 });
  const pc = creerEquipement(schema, "pc", { x: 0, y: 200 });
  const internet = creerEquipement(schema, "internet", { x: 200, y: -400 });
  return { schema, sw1, sw2, coeur, serveur, pc, internet };
}

describe("mode retenu sur le poste", () => {
  const memoire = () => {
    const valeurs = new Map<string, string>();
    return {
      valeurs,
      getItem: (cle: string) => valeurs.get(cle) ?? null,
      setItem: (cle: string, valeur: string) => void valeurs.set(cle, valeur),
    };
  };

  it("la première fois : simplifié ; le mode choisi est retenu sous sa propre clé", () => {
    const poste = memoire();
    expect(lireMode(poste)).toBe("simplifie");
    expect(retenirMode("avance", poste)).toBe(true);
    expect(poste.valeurs.get(CLE_MODE)).toBe("avance");
    expect(lireMode(poste)).toBe("avance");
    retenirMode("simplifie", poste);
    expect(lireMode(poste)).toBe("simplifie");
  });

  it("une valeur inconnue, un stockage absent ou qui refuse : simplifié, sans erreur", () => {
    const poste = memoire();
    poste.valeurs.set(CLE_MODE, "expert");
    expect(lireMode(poste)).toBe("simplifie");
    expect(lireMode(null)).toBe("simplifie");
    expect(retenirMode("avance", null)).toBe(false);
    const ferme = {
      getItem: () => {
        throw new Error("refusé");
      },
      setItem: () => {
        throw new Error("refusé");
      },
    };
    expect(lireMode(ferme)).toBe("simplifie");
    expect(retenirMode("avance", ferme)).toBe(false);
  });
});

describe("câble sans choisir de port", () => {
  it("prend de chaque côté le premier port libre qui convient", () => {
    const { schema, sw1, pc } = site();
    const lien = tirerCable(schema, sw1.id, pc.id);
    expect(nomDuPort(schema, lien.a)).toBe("1/1");
    expect(nomDuPort(schema, lien.b)).toBe("Ethernet");
    expect(lien.support).toBe("cuivre");
    expect(lien.debit).toBe(1000);
    expect(avertissementsDeSupport(schema)).toEqual([]);
  });

  it("deux équipements qui ont des ports fibre : la fibre des deux côtés, pas un port cuivre", () => {
    const { schema, sw1, coeur } = site();
    const lien = tirerCable(schema, sw1.id, coeur.id);
    // Le cœur n'a que de la fibre : le switch y va par son premier uplink, pas par un port d'accès.
    expect(nomDuPort(schema, lien.a)).toBe("1/25");
    expect(nomDuPort(schema, lien.b)).toBe("1/1");
    expect(lien.support).toBe("fibre-multimode");
    expect(avertissementsDeSupport(schema)).toEqual([]);
  });

  it("aucun port libre : un port est créé, l'équipement ne refuse jamais", () => {
    const { schema, sw1, sw2, pc } = site();
    tirerCable(schema, sw1.id, pc.id);
    // Le PC n'a qu'un port câblable (son Wi-Fi est une radio) : le second câble crée un port.
    const avant = pc.interfaces.length;
    const second = tirerCable(schema, sw2.id, pc.id);
    expect(pc.interfaces.length).toBe(avant + 1);
    expect(interfaceDe(schema, second.b)?.genre).toBe("cuivre");
    expect(interfaceDe(schema, second.b)?.nature).toBe("physique");
    expect(nomDuPort(schema, second.a)).toBe("1/1");
    // Le schéma reste valide et se relit sans perte : ce sont de vrais ports et de vrais liens.
    const relu = lireSchema(ecrireSchema(schema));
    expect(relu.erreurs).toEqual([]);
    expect(relu.schema.liens.length).toBe(2);
  });

  it("des ports libres qui ne vont pas ensemble : l'équipement aux nombreux ports en reçoit un du bon genre", () => {
    const { schema, coeur, serveur } = site();
    const avant = coeur.interfaces.length;
    const lien = tirerCable(schema, coeur.id, serveur.id);
    // Le serveur (cuivre) garde son premier port ; le cœur (fibre) reçoit un port cuivre.
    expect(nomDuPort(schema, lien.b)).toBe("eno1");
    expect(coeur.interfaces.length).toBe(avant + 1);
    expect(interfaceDe(schema, lien.a)?.genre).toBe("cuivre");
    expect(lien.support).toBe("cuivre");
    expect(avertissementsDeSupport(schema)).toEqual([]);
  });

  it("les ports de gestion en dernier ; console, USB et radio jamais", () => {
    const { schema, sw1, serveur, pc } = site();
    expect(portsLibres(schema, serveur).map((i) => i.nom)).toEqual(["eno1", "eno2", "eno3", "eno4", "BMC"]);
    expect(portsLibres(schema, pc).map((i) => i.nom)).toEqual(["Ethernet"]);
    for (let n = 0; n < 4; n++) tirerCable(schema, sw1.id, serveur.id);
    expect(portsLibres(schema, serveur).map((i) => i.nom)).toEqual(["BMC"]);
  });

  it("un nuage accepte tous les câbles sur sa seule interface", () => {
    const { schema, sw1, sw2, internet } = site();
    const [un, deux] = [tirerCable(schema, sw1.id, internet.id), tirerCable(schema, sw2.id, internet.id)];
    expect(un.b.interface).toBe(deux.b.interface);
    expect(internet.interfaces.length).toBe(1);
  });

  it("refuse de relier un équipement à lui-même, ou un équipement qui n'existe pas", () => {
    const { schema, sw1 } = site();
    expect(() => tirerCable(schema, sw1.id, sw1.id)).toThrow(Refus);
    expect(() => tirerCable(schema, sw1.id, "d_absent")).toThrow(Refus);
    expect(schema.liens).toEqual([]);
  });
});

describe("plusieurs câbles entre deux mêmes équipements", () => {
  it("recommencer tire un câble de plus, du même type, sur les ports suivants", () => {
    const { schema, sw1, sw2 } = site();
    const un = tirerCable(schema, sw1.id, sw2.id);
    // Dans l'autre sens : c'est toujours la même paire.
    const deux = tirerCable(schema, sw2.id, sw1.id);
    expect(cablesEntre(schema, sw1.id, sw2.id).map((l) => l.id)).toEqual([un.id, deux.id]);
    expect(nomDuPort(schema, un.a)).toBe("1/1");
    expect(nomDuPort(schema, deux.b)).toBe("1/2");
    expect(deux.support).toBe(un.support);
    expect(groupeDeCables(schema, deux.id)?.cables.length).toBe(2);
  });

  it("un câble de plus suit le type et le trunk du groupe", () => {
    const { schema, sw1, sw2 } = site();
    const un = tirerCable(schema, sw1.id, sw2.id);
    changerTypeDeCable(schema, un.id, "fibre-multimode");
    mettreEnTrunk(schema, [un.id], true);
    const deux = tirerCable(schema, sw1.id, sw2.id);
    expect(deux.support).toBe("fibre-multimode");
    expect(interfaceDe(schema, deux.a)?.genre).toBe("fibre");
    expect(enTrunk(schema, deux)).toBe(true);
    expect(groupeDeCables(schema, un.id)).toMatchObject({ support: "fibre-multimode", trunk: true, agregat: null });
    // Et les mêmes VLAN que les autres câbles du trunk.
    ecrireVlansDuTrunk(schema, [un.id, deux.id], { nonTague: 1, tagues: "10,20" });
    const trois = tirerCable(schema, sw1.id, sw2.id);
    expect(configDuBout(schema, trois.a)).toEqual({ mode: "trunk", natif: 1, tagues: "10,20" });
    expect(configDuBout(schema, trois.b)).toEqual({ mode: "trunk", natif: 1, tagues: "10,20" });
  });

  it("le nombre de câbles se règle de 1 à 8, par + et par −", () => {
    const { schema, sw1, sw2 } = site();
    tirerCable(schema, sw1.id, sw2.id);
    reglerNombreDeCables(schema, sw1.id, sw2.id, 3);
    expect(cablesEntre(schema, sw1.id, sw2.id).length).toBe(3);
    reglerNombreDeCables(schema, sw1.id, sw2.id, 99);
    expect(cablesEntre(schema, sw1.id, sw2.id).length).toBe(CABLES_MAX);
    expect(() => tirerCable(schema, sw1.id, sw2.id)).toThrow(/Pas plus de 8/);
    reglerNombreDeCables(schema, sw1.id, sw2.id, 2);
    expect(cablesEntre(schema, sw1.id, sw2.id).map((l) => nomDuPort(schema, l.a))).toEqual(["1/1", "1/2"]);
    reglerNombreDeCables(schema, sw1.id, sw2.id, 0);
    expect(cablesEntre(schema, sw1.id, sw2.id).length).toBe(1);
    // Les ports rendus sont libres : le prochain câble les reprend.
    expect(interfaceOccupee(schema, sw1.id, port(sw1, "1/2").interface)).toBe(false);
  });

  it("« − » garde les câbles réglés en avancé ; s'ils le sont tous, il refuse sans rien retirer", () => {
    const { schema, sw1, sw2 } = site();
    const nomme = tirerCable(schema, sw1.id, sw2.id);
    nomme.nom = "Lien principal";
    const simple = tirerCable(schema, sw1.id, sw2.id);
    const etiquete = tirerCable(schema, sw1.id, sw2.id);
    etiquete.etiquette = "C-042";
    expect(aDesReglages(schema, nomme)).toBe(true);
    expect(aDesReglages(schema, simple)).toBe(false);
    reglerNombreDeCables(schema, sw1.id, sw2.id, 2);
    expect(cablesEntre(schema, sw1.id, sw2.id).map((l) => l.id)).toEqual([nomme.id, etiquete.id]);
    expect(() => reglerNombreDeCables(schema, sw1.id, sw2.id, 1)).toThrow(/outil Effacer/);
    expect(cablesEntre(schema, sw1.id, sw2.id).length).toBe(2);
  });
});

describe("type de câble", () => {
  it("de la fibre demandée sur des ports cuivre : le câble passe sur des ports fibre libres", () => {
    const { schema, sw1, sw2 } = site();
    const lien = tirerCable(schema, sw1.id, sw2.id);
    expect(lien.support).toBe("cuivre");
    changerTypeDeCable(schema, lien.id, "fibre-multimode");
    expect(lien.support).toBe("fibre-multimode");
    expect([nomDuPort(schema, lien.a), nomDuPort(schema, lien.b)]).toEqual(["1/25", "1/25"]);
    expect(lien.debit).toBe(10000);
    expect(interfaceOccupee(schema, sw1.id, port(sw1, "1/1").interface)).toBe(false);
    expect(avertissementsDeSupport(schema)).toEqual([]);
    // Et retour au cuivre : de nouveau sur des ports cuivre.
    changerTypeDeCable(schema, lien.id, "cuivre");
    expect([nomDuPort(schema, lien.a), nomDuPort(schema, lien.b)]).toEqual(["1/1", "1/1"]);
    expect(lien.debit).toBe(1000);
    expect(avertissementsDeSupport(schema)).toEqual([]);
  });

  it("aucun port du bon genre : il est créé ; il ne reste pas quand plus rien ne s'en sert", () => {
    const { schema, sw1, pc } = site();
    const ports = pc.interfaces.map((i) => i.nom);
    const lien = tirerCable(schema, sw1.id, pc.id);
    changerTypeDeCable(schema, lien.id, "fibre-monomode");
    expect(interfaceDe(schema, lien.b)?.genre).toBe("fibre");
    expect(nomDuPort(schema, lien.a)).toBe("1/25");
    expect(interfaceOccupee(schema, pc.id, port(pc, "Ethernet").interface)).toBe(false);
    expect(avertissementsDeSupport(schema)).toEqual([]);
    // Retour au cuivre : le câble reprend le port Ethernet, et le port fibre créé pour lui disparaît.
    changerTypeDeCable(schema, lien.id, "cuivre");
    expect(nomDuPort(schema, lien.b)).toBe("Ethernet");
    expect(pc.interfaces.map((i) => i.nom)).toEqual(ports);
    // Un port créé puis réglé à la main (une adresse) reste, même sans câble.
    const second = tirerCable(schema, sw1.id, pc.id);
    const cree = interfaceDe(schema, second.b);
    expect(cree?.nom).toBe("if1");
    retirerCable(schema, second.id);
    expect(pc.interfaces.map((i) => i.nom)).toEqual(ports);
    const troisieme = tirerCable(schema, sw1.id, pc.id);
    (interfaceDe(schema, troisieme.b) as NonNullable<typeof cree>).adresses = [{ adresse: "10.0.0.7" }];
    retirerCable(schema, troisieme.id);
    expect(pc.interfaces.map((i) => i.nom)).toEqual([...ports, "if1"]);
  });

  it("un type qui va sur tout port ne déplace rien ; le trunk suit un câble déplacé", () => {
    const { schema, sw1, sw2 } = site();
    const lien = tirerCable(schema, sw1.id, sw2.id);
    mettreEnTrunk(schema, [lien.id], true);
    changerTypeDeCable(schema, lien.id, "console");
    expect([nomDuPort(schema, lien.a), nomDuPort(schema, lien.b)]).toEqual(["1/1", "1/1"]);
    changerTypeDeCable(schema, lien.id, "fibre-multimode");
    expect(nomDuPort(schema, lien.a)).toBe("1/25");
    expect(enTrunk(schema, lien)).toBe(true);
    // L'ancien port ne garde pas le trunk : un câble posé là plus tard ne naît pas en trunk.
    expect(interfaceDe(schema, port(sw1, "1/1"))?.vlan).toBeUndefined();
  });

  it("les câbles d'un agrégat ont tous le même type : ses ports changent ensemble", () => {
    const { schema, sw1, sw2 } = site();
    tirerCable(schema, sw1.id, sw2.id);
    tirerCable(schema, sw1.id, sw2.id);
    const lien = agreger(schema, sw1.id, sw2.id);
    const nomsDesPorts = (e: Equipement) =>
      agregatDe(schema, { equipement: e.id, interface: (e.id === lien.a.equipement ? lien.a : lien.b).interface })
        ?.membres.map((m) => e.interfaces.find((i) => i.id === m)?.nom);
    expect(nomsDesPorts(sw1)).toEqual(["1/1", "1/2"]);
    changerTypeDeCable(schema, lien.id, "fibre-multimode");
    expect(lien.support).toBe("fibre-multimode");
    expect(nomsDesPorts(sw1)).toEqual(["1/25", "1/26"]);
    expect(nomsDesPorts(sw2)).toEqual(["1/25", "1/26"]);
    expect(nomDuPort(schema, lien.a)).toBe("1/25");
    expect(avertissementsDeSupport(schema)).toEqual([]);
    expect(groupeDeCables(schema, lien.id)?.nombre).toBe(2);
  });
});

describe("trunk", () => {
  it("l'interrupteur met les deux bouts en mode VLAN trunk, et les remet en accès", () => {
    const { schema, sw1, sw2 } = site();
    const lien = tirerCable(schema, sw1.id, sw2.id);
    expect(enTrunk(schema, lien)).toBe(false);
    mettreEnTrunk(schema, [lien.id], true);
    expect(configDuBout(schema, lien.a)).toEqual({ mode: "trunk" });
    expect(configDuBout(schema, lien.b)).toEqual({ mode: "trunk" });
    expect(enTrunk(schema, lien)).toBe(true);
    expect(diagnostiquer(schema).some((c) => c.message.includes("VLAN différents"))).toBe(false);
    // Le réglage est celui du modèle : il se relit.
    const relu = lireSchema(ecrireSchema(schema)).schema;
    expect(enTrunk(relu, relu.liens[0] as (typeof relu.liens)[number])).toBe(true);
    mettreEnTrunk(schema, [lien.id], false);
    expect(configDuBout(schema, lien.a)).toBeUndefined();
    expect(enTrunk(schema, lien)).toBe(false);
  });

  it("aller et retour sans rien perdre d'une configuration VLAN existante", () => {
    const { schema, sw1, sw2 } = site();
    const lien = tirerCable(schema, sw1.id, sw2.id);
    ecrireConfigDuBout(schema, lien.a, { mode: "acces", acces: 10, voix: 40 });
    ecrireConfigDuBout(schema, lien.b, { mode: "trunk", natif: 1, tagues: "10,20" });
    mettreEnTrunk(schema, [lien.id], true);
    expect(configDuBout(schema, lien.a)).toEqual({ mode: "trunk", acces: 10, voix: 40 });
    expect(configDuBout(schema, lien.b)).toEqual({ mode: "trunk", natif: 1, tagues: "10,20" });
    mettreEnTrunk(schema, [lien.id], false);
    expect(configDuBout(schema, lien.a)).toEqual({ mode: "acces", acces: 10, voix: 40 });
    expect(configDuBout(schema, lien.b)).toEqual({ mode: "acces", natif: 1, tagues: "10,20" });
    mettreEnTrunk(schema, [lien.id], true);
    expect(configDuBout(schema, lien.b)).toEqual({ mode: "trunk", natif: 1, tagues: "10,20" });
  });

  it("un câble retiré ne laisse pas ses ports en trunk ; un trunk réglé en avancé reste au port", () => {
    const { schema, sw1, sw2 } = site();
    const [un, deux] = [tirerCable(schema, sw1.id, sw2.id), tirerCable(schema, sw1.id, sw2.id)];
    mettreEnTrunk(schema, [un.id, deux.id], true);
    ecrireConfigDuBout(schema, deux.a, { mode: "trunk", tagues: "10,20" });
    retirerCable(schema, un.id);
    retirerCable(schema, deux.id);
    expect(interfaceDe(schema, port(sw1, "1/1"))?.vlan).toBeUndefined();
    expect(interfaceDe(schema, port(sw1, "1/2"))?.vlan).toEqual({ mode: "trunk", tagues: "10,20" });
    // Le prochain câble évite le port déjà réglé, et ne naît pas en trunk.
    const suivant = tirerCable(schema, sw1.id, sw2.id);
    expect(nomDuPort(schema, suivant.a)).toBe("1/1");
    expect(enTrunk(schema, suivant)).toBe(false);
  });

  it("effacer un équipement libère proprement les ports d'en face", () => {
    const { schema, sw1, sw2, pc } = site();
    const lien = tirerCable(schema, sw1.id, sw2.id);
    tirerCable(schema, sw1.id, pc.id);
    mettreEnTrunk(schema, [lien.id], true);
    effacer(schema, new Set([sw1.id]));
    expect(schema.equipements.some((e) => e.id === sw1.id)).toBe(false);
    expect(schema.liens).toEqual([]);
    expect(interfaceDe(schema, port(sw2, "1/1"))?.vlan).toBeUndefined();
    // Un équipement verrouillé reste, avec ses câbles.
    sw2.verrou = true;
    tirerCable(schema, sw2.id, pc.id);
    effacer(schema, new Set([sw2.id]));
    expect(schema.equipements.some((e) => e.id === sw2.id)).toBe(true);
    expect(schema.liens.length).toBe(1);
  });
});

describe("adresse IP d'un équipement", () => {
  it("lit IPv4 et IPv6, avec ou sans préfixe ; vide : pas d'adresse", () => {
    expect(lireAdresse("  10.187.10.3 ")).toEqual({ adresse: "10.187.10.3" });
    expect(lireAdresse("10.187.10.3/24")).toEqual({ adresse: "10.187.10.3/24" });
    expect(lireAdresse("fd00::1")).toEqual({ adresse: "fd00::1" });
    expect(lireAdresse("2001:db8:0:1:0:0:0:2/64")).toEqual({ adresse: "2001:db8:0:1:0:0:0:2/64" });
    expect(lireAdresse("::")).toEqual({ adresse: "::" });
    expect(lireAdresse("   ")).toEqual({ adresse: null });
  });

  it("refuse ce qui n'est pas une adresse, avec un message qui dit quoi corriger", () => {
    for (const faux of ["10.0.0", "10.0.0.256", "serveur", "10.0.0.1 /24", "1:2:3", "fd00:::1", "12345::1"]) {
      expect(lireAdresse(faux), faux).toMatchObject({ erreur: expect.stringContaining("adresse IP") });
    }
    expect(lireAdresse("10.0.0.1/33")).toMatchObject({ erreur: expect.stringContaining("0 à 32") });
    expect(lireAdresse("fd00::1/129")).toMatchObject({ erreur: expect.stringContaining("0 à 128") });
    expect(lireAdresse("10.0.0.1/24/8")).toMatchObject({ erreur: expect.stringContaining("préfixe") });
  });

  it("s'écrit comme adresse de gestion : c'est elle que le dessin montre, et que l'avancé relit", () => {
    const { schema, coeur } = site();
    expect(ecrireAdresse(schema, coeur.id, "10.187.10.3")).toBe("10.187.10.3");
    expect(coeur.gestion?.adresse).toBe("10.187.10.3");
    expect(dessinerSchema(schema)).toContain(">10.187.10.3<");
    // Refusée : rien ne change.
    expect(() => ecrireAdresse(schema, coeur.id, "10.187.10")).toThrow(Refus);
    expect(coeur.gestion?.adresse).toBe("10.187.10.3");
    // Vidée : plus d'adresse, et plus rien sous l'équipement (aucun texte de remplacement).
    expect(ecrireAdresse(schema, coeur.id, "")).toBe("");
    expect(coeur.gestion).toBeUndefined();
    const svg = dessinerSchema(schema);
    expect(svg).not.toContain("10.187.10.3");
    expect(svg).not.toMatch(/sans adresse/i);
    expect(svg.match(/font-family="Cascadia Mono/g)).toBeNull();
  });

  it("vider retire l'adresse montrée ; une adresse d'interface posée en avancé reste jusqu'à ce qu'on la vide aussi", () => {
    const { schema, serveur } = site();
    serveur.gestion = { vlan: 99 };
    (serveur.interfaces[0] as (typeof serveur.interfaces)[number]).adresses = [{ adresse: "10.0.0.5", prefixe: 24 }];
    expect(ecrireAdresse(schema, serveur.id, "10.0.0.9")).toBe("10.0.0.9");
    // L'adresse de gestion partie, celle de l'interface reparaît ; le VLAN de gestion n'a pas bougé.
    expect(ecrireAdresse(schema, serveur.id, "")).toBe("10.0.0.5/24");
    expect(serveur.gestion).toEqual({ vlan: 99 });
    expect(ecrireAdresse(schema, serveur.id, "")).toBe("");
    expect(serveur.interfaces[0]?.adresses).toEqual([]);
  });
});

describe("ce que le mode simplifié dessine", () => {
  it("ni nom de port, ni VLAN d'un port d'accès, même si le schéma en demande en avancé ; le trunk, lui, est annoté", () => {
    const { schema, sw1, sw2, pc } = site();
    const lien = tirerCable(schema, sw1.id, sw2.id);
    const acces = connecter(schema, port(sw1, "1/5"), port(pc, "Ethernet"), { libelles: "tout", nom: "Poste" });
    ecrireConfigDuBout(schema, acces.a, { mode: "acces", acces: 20 });
    mettreEnTrunk(schema, [lien.id], true);
    schema.affichage = { ports: true, vlans: true };
    const epure = epurer(schema);
    const page = mettreEnPage(epure, vue("simplifiee"), new Set());
    const etiquettes = [...page.etiquettes.values()].flat();
    expect(etiquettes.map((e) => e.texte).sort()).toEqual(["Poste", "Trunk"]);
    const svg = dessinerSchema(epure, { vue: vue("simplifiee"), legende: true });
    expect(svg).not.toMatch(/>1\/1<|VLAN 20/);
    expect(svg).toContain(">Trunk<");
    // Un trunk n'est pas un agrégat : pas de cercle, ni sur le dessin ni en légende.
    expect(svg).not.toContain("Cercle");
    // Le schéma lui-même n'est pas touché : en avancé, ports et VLAN sont toujours là.
    expect(schema.affichage).toEqual({ ports: true, vlans: true });
    expect(acces.libelles).toBe("tout");
    const avance = [...mettreEnPage(schema, vue("physique"), new Set()).etiquettes.values()].flat();
    expect(avance.some((e) => e.genre === "port")).toBe(true);
    expect(avance.some((e) => e.genre === "vlan")).toBe(true);
  });
});

describe("agrégat de liens", () => {
  /** Deux switches reliés par `nombre` câbles ordinaires. */
  function liaison(nombre: number) {
    const lieu = site();
    const cables = Array.from({ length: nombre }, () => tirerCable(lieu.schema, lieu.sw1.id, lieu.sw2.id));
    return { ...lieu, cables };
  }
  const membres = (schema: Schema, bout: { equipement: string; interface: string }) =>
    (agregatDe(schema, bout)?.membres ?? []).map((m) => nomDuPort(schema, { equipement: bout.equipement, interface: m }));

  it("coché : les câbles de la liaison deviennent un agrégat LACP, un port par câble de chaque côté", () => {
    const { schema, sw1, sw2 } = liaison(3);
    const lien = agreger(schema, sw1.id, sw2.id);
    // Le vrai agrégat du modèle : un câble, et les ports de chaque côté.
    expect(schema.liens.length).toBe(1);
    expect(schema.agregats.length).toBe(2);
    expect(agregatDe(schema, lien.a)).toMatchObject({ nom: "Trk1", mode: "lacp" });
    expect(membres(schema, lien.a)).toEqual(["1/1", "1/2", "1/3"]);
    expect(membres(schema, lien.b)).toEqual(["1/1", "1/2", "1/3"]);
    expect(enAgregat(schema, lien)).toBe(true);
    // Un agrégat n'est pas un trunk.
    expect(enTrunk(schema, lien)).toBe(false);
    expect(groupeDeCables(schema, lien.id)).toMatchObject({
      nombre: 3,
      agregat: { ports: 3, nom: "Trk1" },
      ordinaires: [],
      horsDePortee: null,
      trunk: false,
    });
    const relu = lireSchema(ecrireSchema(schema));
    expect(relu.erreurs).toEqual([]);
    expect(relu.schema.agregats).toEqual(schema.agregats);
  });

  it("un seul câble : refusé, avec l'aide qui dit quoi faire", () => {
    const { schema, sw1, sw2 } = liaison(1);
    expect(() => agreger(schema, sw1.id, sw2.id)).toThrow(AIDE_AGREGAT_SEUL);
    expect(AIDE_AGREGAT_SEUL).toBe("Un agrégat regroupe au moins deux câbles : ajoutez-en un.");
    expect(schema.agregats).toEqual([]);
    expect(schema.liens.length).toBe(1);
  });

  it("des câbles de types différents prennent le type du câble gardé", () => {
    const { schema, sw1, sw2, cables } = liaison(2);
    changerTypeDeCable(schema, (cables[1] as (typeof cables)[number]).id, "fibre-multimode");
    const lien = agreger(schema, sw1.id, sw2.id);
    expect(lien.support).toBe("cuivre");
    expect(membres(schema, lien.a)).toEqual(["1/1", "1/2"]);
    expect(avertissementsDeSupport(schema)).toEqual([]);
  });

  it("décoché : chaque port retrouve son câble, et le réglage VLAN de l'agrégat est reporté sur chacun", () => {
    const { schema, sw1, sw2 } = liaison(3);
    const lien = agreger(schema, sw1.id, sw2.id);
    ecrireVlansDuTrunk(schema, [lien.id], { nonTague: 1, tagues: "10,20" });
    // Le réglage est celui de l'agrégat, pas des ports.
    expect(agregatDe(schema, lien.a)?.vlan).toEqual({ mode: "trunk", natif: 1, tagues: "10,20" });
    const cables = desagreger(schema, lien.id);
    expect(schema.agregats).toEqual([]);
    expect(cables.length).toBe(3);
    expect(cablesEntre(schema, sw1.id, sw2.id).map((l) => [nomDuPort(schema, l.a), nomDuPort(schema, l.b)])).toEqual([
      ["1/1", "1/1"],
      ["1/2", "1/2"],
      ["1/3", "1/3"],
    ]);
    for (const cable of cables) {
      expect(cable.support).toBe(lien.support);
      expect(interfaceDe(schema, cable.a)?.vlan).toEqual({ mode: "trunk", natif: 1, tagues: "10,20" });
      expect(interfaceDe(schema, cable.b)?.vlan).toEqual({ mode: "trunk", natif: 1, tagues: "10,20" });
      expect(enTrunk(schema, cable)).toBe(true);
    }
    expect(groupeDeCables(schema, lien.id)).toMatchObject({ nombre: 3, agregat: null, trunk: true });
    // Un agrégat sans VLAN : des câbles sans réglage.
    const second = agreger(schema, sw1.id, sw2.id);
    mettreEnTrunk(schema, [second.id], false);
    for (const cable of desagreger(schema, second.id)) expect(enTrunk(schema, cable)).toBe(false);
  });

  it("« + » et « − » ajoutent et rendent des ports de l'agrégat, de 2 à 8", () => {
    const { schema, sw1, sw2 } = liaison(2);
    const lien = agreger(schema, sw1.id, sw2.id);
    reglerMembres(schema, lien.id, 4);
    expect(membres(schema, lien.a)).toEqual(["1/1", "1/2", "1/3", "1/4"]);
    expect(membres(schema, lien.b)).toEqual(["1/1", "1/2", "1/3", "1/4"]);
    expect(schema.liens.length).toBe(1);
    expect(groupeDeCables(schema, lien.id)?.nombre).toBe(4);
    reglerMembres(schema, lien.id, 2);
    expect(membres(schema, lien.a)).toEqual(["1/1", "1/2"]);
    // À deux, « − » s'arrête : il faut d'abord décocher.
    expect(() => reglerMembres(schema, lien.id, 1)).toThrow(AIDE_AGREGAT_MINIMUM);
    expect(() => reglerMembres(schema, lien.id, CABLES_MAX + 1)).toThrow(/Pas plus de 8/);
    expect(membres(schema, lien.a)).toEqual(["1/1", "1/2"]);
    // L'outil Câble, sur une liaison agrégée, ajoute un port à l'agrégat.
    expect(tirerCable(schema, sw1.id, sw2.id).id).toBe(lien.id);
    expect(membres(schema, lien.b)).toEqual(["1/1", "1/2", "1/3"]);
    expect(schema.liens.length).toBe(1);
  });

  it("un port de plus se crée quand l'équipement n'en a plus ; rendu, il disparaît", () => {
    const { schema, sw1, pc } = site();
    tirerCable(schema, sw1.id, pc.id);
    tirerCable(schema, sw1.id, pc.id);
    const lien = agreger(schema, sw1.id, pc.id);
    const avant = pc.interfaces.length;
    reglerMembres(schema, lien.id, 3);
    expect(pc.interfaces.length).toBe(avant + 1);
    reglerMembres(schema, lien.id, 2);
    expect(pc.interfaces.length).toBe(avant);
  });

  it("supprimer le câble d'un agrégat retire l'agrégat et rend ses ports", () => {
    const { schema, sw1, sw2 } = liaison(2);
    const lien = agreger(schema, sw1.id, sw2.id);
    mettreEnTrunk(schema, [lien.id], true);
    retirerCable(schema, lien.id);
    expect(schema.liens).toEqual([]);
    expect(schema.agregats).toEqual([]);
    expect(interfaceDe(schema, port(sw1, "1/1"))?.vlan).toBeUndefined();
    expect(portsLibres(schema, sw1).slice(0, 2).map((i) => i.nom)).toEqual(["1/1", "1/2"]);
  });

  it("un agrégat fait en avancé se règle d'ici ; ce que le simplifié ne sait pas dire est nommé", () => {
    const { schema, sw1, sw2 } = site();
    const lien = connecter(schema, port(sw1, "1/1"), port(sw2, "1/1"));
    creerAgregat(schema, lien.id, "statique");
    basculerMembre(schema, lien.id, "a", port(sw1, "1/2").interface);
    basculerMembre(schema, lien.id, "b", port(sw2, "1/2").interface);
    expect(groupeDeCables(schema, lien.id)).toMatchObject({ nombre: 2, agregat: { ports: 2 }, horsDePortee: null });
    reglerMembres(schema, lien.id, 3);
    expect(agregatDe(schema, lien.a)?.mode).toBe("statique");
    expect(desagreger(schema, lien.id).length).toBe(3);
    // Deux agrégats sur la même liaison : le simplifié n'en règle qu'un.
    const un = agreger(schema, sw1.id, sw2.id);
    const autre = connecter(schema, port(sw1, "1/10"), port(sw2, "1/10"));
    creerAgregat(schema, autre.id);
    expect(groupeDeCables(schema, un.id)?.horsDePortee).toMatch(/plusieurs agrégats \(Trk1, Trk2\)/);
    expect(() => agreger(schema, sw1.id, sw2.id)).toThrow(/plusieurs agrégats/);
    // Un agrégat d'un seul côté.
    schema.agregats = schema.agregats.filter((a) => a.equipement !== sw2.id || !a.membres.includes(autre.b.interface));
    retirerCable(schema, un.id);
    expect(groupeDeCables(schema, autre.id)?.horsDePortee).toMatch(/que du côté de SW 1/);
  });
});

describe("VLAN d'un trunk", () => {
  it("lit le VLAN non tagué (un numéro) et les VLAN tagués (une liste), remis au propre", () => {
    expect(lireVlansDuTrunk(" 1 ", "10, 20 30-40")).toEqual({ champs: { nonTague: 1, tagues: "10,20,30-40" } });
    expect(lireVlansDuTrunk("", "10,20")).toEqual({ champs: { tagues: "10,20" } });
    expect(lireVlansDuTrunk("99", "")).toEqual({ champs: { nonTague: 99 } });
    expect(lireVlansDuTrunk("", "  ")).toEqual({ champs: {} });
  });

  it("refuse, en disant quoi corriger et dans quel champ", () => {
    for (const faux of ["0", "4095", "dix", "1,2", "1.5"]) {
      expect(lireVlansDuTrunk(faux, "10"), faux).toMatchObject({ champ: "non-tague", erreur: expect.stringContaining("1 à 4094") });
    }
    for (const faux of ["10,abc", "0,10", "4095", "40-30", "10-20-30"]) {
      expect(lireVlansDuTrunk("1", faux), faux).toMatchObject({ champ: "tagues", erreur: expect.stringContaining("10,20,30-40") });
    }
    // Le VLAN non tagué répété dans les tagués, seul ou dans une plage.
    expect(lireVlansDuTrunk("10", "10,20")).toMatchObject({ champ: "tagues", erreur: expect.stringContaining("VLAN 10 est déjà") });
    expect(lireVlansDuTrunk("35", "10,30-40")).toMatchObject({ champ: "tagues", erreur: expect.stringContaining("VLAN 35 est déjà") });
    expect(lireVlansDuTrunk("41", "10,30-40")).toEqual({ champs: { nonTague: 41, tagues: "10,30-40" } });
  });

  it("s'écrit sur les deux bouts à l'identique, en trunk, en gardant VLAN voix et PVID", () => {
    const { schema, sw1, sw2 } = site();
    const lien = tirerCable(schema, sw1.id, sw2.id);
    ecrireConfigDuBout(schema, lien.a, { mode: "acces", acces: 10, voix: 40 });
    ecrireVlansDuTrunk(schema, [lien.id], { nonTague: 1, tagues: "10,20,30" });
    expect(configDuBout(schema, lien.a)).toEqual({ mode: "trunk", natif: 1, tagues: "10,20,30", voix: 40 });
    expect(configDuBout(schema, lien.b)).toEqual({ mode: "trunk", natif: 1, tagues: "10,20,30" });
    expect(groupeDeCables(schema, lien.id)).toMatchObject({
      trunk: true,
      vlans: { nonTague: 1, tagues: "10,20,30" },
      boutsDifferents: null,
      cablesDifferents: false,
    });
    expect(diagnostiquer(schema).some((c) => c.message.includes("VLAN différents"))).toBe(false);
    // Un champ vidé retire ce VLAN ; le trunk reste un trunk.
    ecrireVlansDuTrunk(schema, [lien.id], { tagues: "10" });
    expect(configDuBout(schema, lien.b)).toEqual({ mode: "trunk", tagues: "10" });
    // Décoché puis recoché : ce qui était saisi revient.
    mettreEnTrunk(schema, [lien.id], false);
    expect(enTrunk(schema, lien)).toBe(false);
    expect(groupeDeCables(schema, lien.id)?.trunk).toBe(false);
    mettreEnTrunk(schema, [lien.id], true);
    expect(groupeDeCables(schema, lien.id)?.vlans).toEqual({ tagues: "10" });
  });

  it("sur un agrégat, il s'écrit sur l'agrégat de chaque côté", () => {
    const { schema, sw1, sw2 } = site();
    tirerCable(schema, sw1.id, sw2.id);
    tirerCable(schema, sw1.id, sw2.id);
    const lien = agreger(schema, sw1.id, sw2.id);
    ecrireVlansDuTrunk(schema, [lien.id], { nonTague: 1, tagues: "10,20" });
    expect(agregatDe(schema, lien.a)?.vlan).toEqual({ mode: "trunk", natif: 1, tagues: "10,20" });
    expect(agregatDe(schema, lien.b)?.vlan).toEqual({ mode: "trunk", natif: 1, tagues: "10,20" });
    expect(interfaceDe(schema, lien.a)?.vlan).toBeUndefined();
    expect(groupeDeCables(schema, lien.id)).toMatchObject({ trunk: true, agregat: { ports: 2 }, nombre: 2 });
  });

  it("deux bouts aux réglages différents : la bulle le dit, et aligne sur le bout choisi", () => {
    const { schema, sw1, sw2 } = site();
    const lien = tirerCable(schema, sw1.id, sw2.id);
    ecrireConfigDuBout(schema, lien.a, { mode: "trunk", natif: 1, tagues: "10,20" });
    ecrireConfigDuBout(schema, lien.b, { mode: "acces", acces: 10 });
    const groupe = groupeDeCables(schema, lien.id);
    expect(groupe?.trunk).toBe(false);
    expect(groupe?.boutsDifferents).toEqual({ a: "trunk, U 1 · T 10,20", b: "accès, VLAN 10" });
    alignerVlans(schema, [lien.id], sw1.id);
    expect(configDuBout(schema, lien.b)).toEqual({ mode: "trunk", natif: 1, tagues: "10,20" });
    expect(groupeDeCables(schema, lien.id)).toMatchObject({ trunk: true, boutsDifferents: null });
    // Dans l'autre sens : le bout sans réglage l'emporte.
    ecrireConfigDuBout(schema, lien.b, undefined);
    expect(groupeDeCables(schema, lien.id)?.boutsDifferents).toEqual({ a: "trunk, U 1 · T 10,20", b: "aucun réglage" });
    alignerVlans(schema, [lien.id], sw2.id);
    expect(configDuBout(schema, lien.a)).toBeUndefined();
    expect(groupeDeCables(schema, lien.id)?.boutsDifferents).toBeNull();
  });

  it("des câbles parallèles aux VLAN différents : signalé ; ce qu'on saisit vaut pour tous", () => {
    const { schema, sw1, sw2 } = site();
    const [un, deux] = [tirerCable(schema, sw1.id, sw2.id), tirerCable(schema, sw1.id, sw2.id)];
    ecrireVlansDuTrunk(schema, [un.id], { tagues: "10" });
    ecrireVlansDuTrunk(schema, [deux.id], { tagues: "20" });
    expect(groupeDeCables(schema, un.id)).toMatchObject({ trunk: true, cablesDifferents: true, vlans: { tagues: "10" } });
    ecrireVlansDuTrunk(schema, [un.id, deux.id], { nonTague: 5, tagues: "10,20" });
    expect(groupeDeCables(schema, un.id)).toMatchObject({ cablesDifferents: false, vlans: { nonTague: 5, tagues: "10,20" } });
  });
});

describe("bonnes pratiques : des conseils, jamais des blocages", () => {
  const conseils = (schema: Schema, lienId: string) => {
    const groupe = groupeDeCables(schema, lienId);
    if (!groupe) throw new Error("liaison absente");
    return conseilsDeLiaison(schema, groupe);
  };

  it("deux câbles entre deux commutateurs sans agrégat : une boucle ; avec l'agrégat, plus de conseil", () => {
    const { schema, sw1, sw2, coeur, serveur } = site();
    const un = tirerCable(schema, sw1.id, sw2.id);
    expect(conseils(schema, un.id)).toEqual([]);
    tirerCable(schema, sw1.id, sw2.id);
    expect(conseils(schema, un.id)).toEqual([
      "Deux câbles entre deux commutateurs sans agrégat forment une boucle : le spanning tree n'en laissera passer qu'un. Cochez « Agrégat de liens ».",
    ]);
    const lien = agreger(schema, sw1.id, sw2.id);
    expect(conseils(schema, lien.id)).toEqual([]);
    // Un cœur est un commutateur ; un serveur n'en est pas un (deux câbles vers lui ne font pas de boucle).
    const versCoeur = tirerCable(schema, sw1.id, coeur.id);
    tirerCable(schema, sw1.id, coeur.id);
    expect(conseils(schema, versCoeur.id)[0]).toContain("forment une boucle");
    const versServeur = tirerCable(schema, sw1.id, serveur.id);
    tirerCable(schema, sw1.id, serveur.id);
    expect(conseils(schema, versServeur.id)).toEqual([]);
  });

  it("trunk sans VLAN, VLAN 1 non tagué, trunk vers un poste", () => {
    const { schema, sw1, sw2, pc } = site();
    const lien = tirerCable(schema, sw1.id, sw2.id);
    mettreEnTrunk(schema, [lien.id], true);
    expect(conseils(schema, lien.id)).toEqual(["Indiquez les VLAN que ce trunk transporte."]);
    ecrireVlansDuTrunk(schema, [lien.id], { nonTague: 1, tagues: "10,20" });
    expect(conseils(schema, lien.id)).toEqual([
      "Le VLAN 1 comme VLAN non tagué d'un trunk est déconseillé : préférez un VLAN réservé à cet usage.",
    ]);
    ecrireVlansDuTrunk(schema, [lien.id], { nonTague: 99, tagues: "10,20" });
    expect(conseils(schema, lien.id)).toEqual([]);
    const versPoste = tirerCable(schema, sw1.id, pc.id);
    expect(conseils(schema, versPoste.id)).toEqual([]);
    ecrireVlansDuTrunk(schema, [versPoste.id], { tagues: "10,20" });
    expect(conseils(schema, versPoste.id)).toEqual([
      "Un poste se branche d'ordinaire sur un port d'accès, pas sur un trunk.",
    ]);
    // Un téléphone IP lit les VLAN : pas de conseil.
    const telephone = creerEquipement(schema, "telephone-ip", { x: 0, y: 400 });
    const versTelephone = tirerCable(schema, sw1.id, telephone.id);
    ecrireVlansDuTrunk(schema, [versTelephone.id], { nonTague: 20, tagues: "40" });
    expect(conseils(schema, versTelephone.id)).toEqual([]);
  });

  it("un agrégat aux ports de genres ou de débits différents : dit comme le diagnostic", () => {
    const { schema, sw1, sw2 } = site();
    const lien = connecter(schema, port(sw1, "1/1"), port(sw2, "1/1"));
    creerAgregat(schema, lien.id);
    basculerMembre(schema, lien.id, "a", port(sw1, "1/25").interface);
    basculerMembre(schema, lien.id, "b", port(sw2, "1/2").interface);
    expect(conseils(schema, lien.id)).toEqual([
      "Agrégat « Trk1 » : ports de genres différents (cuivre et fibre). Un agrégat veut des liens identiques.",
      "Agrégat « Trk1 » : vitesses différentes entre ses ports (1 Gbit/s et 10 Gbit/s). Un agrégat veut des liens identiques.",
    ]);
  });

  it("aucun conseil n'empêche quoi que ce soit", () => {
    const { schema, sw1, pc } = site();
    const lien = tirerCable(schema, sw1.id, pc.id);
    ecrireVlansDuTrunk(schema, [lien.id], { nonTague: 1 });
    expect(conseils(schema, lien.id).length).toBe(2);
    expect(enTrunk(schema, lien)).toBe(true);
    expect(lireSchema(ecrireSchema(schema)).erreurs).toEqual([]);
  });
});
