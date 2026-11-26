<script lang="ts">
  import { tick } from "svelte";
  import { definition } from "../../lib/topologie/catalogue";
  import {
    ajouterPlage,
    basculerActivation,
    basculerPoe,
    bornesDeLaPlage,
    mettreEnAcces,
    mettreEnTrunk,
    numeroAffiche,
    type Plage,
    plageProposee,
    suiteEntre,
    texteVlanDuPort,
  } from "../../lib/topologie/configuration";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import {
    type Equipement,
    GENRES_INTERFACE,
    type GroupePorts,
    type Interface,
    liensDeLInterface,
    NATURES_INTERFACE,
    nouvelId,
    ROLES_INTERFACE,
    vitesseEnClair,
  } from "../../lib/topologie/modele";
  import { ajouterInterface, renommerInterface } from "../../lib/topologie/operations";
  import { cleDOrigine, groupesDuMembre, nomDePort } from "../../lib/topologie/ports";
  import {
    agregatDe,
    type ChampsVlan,
    champsDe,
    configDepuis,
    configDuBout,
    ecrireConfigDuBout,
    lireListeVlan,
    lireNumeroVlan,
  } from "../../lib/topologie/vlans";

  // L'onglet Ports du volet (30D) : les membres du stack, la façade en petit (un carré par port, plein s'il est
  // relié), le tableau des ports, ce qu'on règle sur plusieurs ports d'un coup, une plage de ports à ajouter, puis
  // le profil des ports et le détail du port ouvert.
  let {
    editeur,
    equipement,
    changer,
  }: {
    editeur: EditeurSchema;
    equipement: Equipement;
    /** Change le profil des ports (membres, groupes) : appliqué, ou décisions demandées pour les liens touchés. */
    changer: (action: (e: Equipement) => void) => void;
  } = $props();

  const schema = $derived(editeur.schema);
  const def = $derived(definition(equipement.type));
  const empile = $derived(equipement.membres.length > 1);
  const numeros = $derived(new Set(equipement.membres.map((m) => m.numero)));
  /** Interfaces d'un stack rattachées à aucun membre (interfaces VLAN, ajoutées à la main). */
  const sansMembre = $derived(empile && equipement.interfaces.some((i) => i.membre === undefined || !numeros.has(i.membre)));

  let membreVu = $state<number | "autres">(1);
  const membreActif = $derived<number | "autres">(
    membreVu === "autres" ? (sansMembre ? "autres" : (equipement.membres[0]?.numero ?? 1)) : numeros.has(membreVu) ? membreVu : (equipement.membres[0]?.numero ?? 1),
  );
  const vues = $derived(
    !empile
      ? equipement.interfaces
      : equipement.interfaces.filter((i) =>
          membreActif === "autres" ? i.membre === undefined || !numeros.has(i.membre) : i.membre === membreActif,
        ),
  );
  const facade = $derived(vues.filter((i) => i.nature === "physique" && i.genre !== "radio"));
  /** Le membre qui reçoit une plage : celui qu'on regarde, ou le seul. */
  const membrePlage = $derived(membreActif === "autres" ? null : empile ? membreActif : (equipement.membres[0]?.numero ?? 1));

  // --- Ce à quoi chaque port est relié ------------------------------------------------------------------------------

  const relies = $derived(
    new Map(
      equipement.interfaces.map((i) => [
        i.id,
        liensDeLInterface(schema, equipement.id, i.id).map((l) => {
          const autre = l.a.equipement === equipement.id && l.a.interface === i.id ? l.b : l.a;
          const e = schema.equipements.find((x) => x.id === autre.equipement);
          const port = e?.interfaces.find((x) => x.id === autre.interface)?.nom;
          return { lien: l.id, texte: port ? `${e?.nom ?? "?"} · ${port}` : (e?.nom ?? "?") };
        }),
      ]),
    ),
  );

  /** Ports d'un agrégat sans câble à eux : le câble de l'agrégat les représente (D51). */
  const representes = $derived(
    new Map(
      schema.agregats
        .filter((a) => a.equipement === equipement.id)
        .flatMap((a) => {
          const cable = a.membres.flatMap((m) => relies.get(m) ?? [])[0];
          return cable
            ? a.membres
                .filter((m) => (relies.get(m) ?? []).length === 0)
                .map((m) => [m, { lien: cable.lien, texte: `${cable.texte} (agrégat ${a.nom})` }] as const)
            : [];
        }),
    ),
  );
  const estRelie = (id: string) => (relies.get(id) ?? []).length > 0 || representes.has(id);
  const configDe = (i: Interface) => configDuBout(schema, { equipement: equipement.id, interface: i.id });

  const libres = $derived(facade.filter((i) => !estRelie(i.id)).length);
  const trunks = $derived(vues.filter((i) => configDe(i)?.mode === "trunk").length);

  // --- Choisir des ports -----------------------------------------------------------------------------------------------

  let choisis = $state<string[]>([]);
  let ancre = $state<string | null>(null);
  let interfaceChoisie = $state<string | null>(null);
  /** Le port de la façade qui reçoit la tabulation. */
  let courant = $state<string | null>(null);
  const iface = $derived(equipement.interfaces.find((i) => i.id === interfaceChoisie) ?? null);
  const ordreDeLaFacade = $derived(facade.map((i) => i.id));
  const ordreDuTableau = $derived(vues.map((i) => i.id));
  const tabulable = $derived(courant && ordreDeLaFacade.includes(courant) ? courant : ordreDeLaFacade[0]);
  const ports = $derived(equipement.interfaces.filter((i) => choisis.includes(i.id)));

  // D'un membre à l'autre, ce qui était choisi reste ; un port qui n'existe plus quitte le choix.
  $effect(() => {
    const presents = new Set(equipement.interfaces.map((i) => i.id));
    if (choisis.some((id) => !presents.has(id))) choisis = choisis.filter((id) => presents.has(id));
  });

  /** Un clic choisit ce port seul ; Maj, la suite depuis le dernier ; Ctrl, l'ajoute ou le retire. */
  function choisir(id: string, evenement: { shiftKey: boolean; ctrlKey: boolean; metaKey: boolean }, ordre: string[], seul = true) {
    const ajout = evenement.ctrlKey || evenement.metaKey;
    if (evenement.shiftKey && ancre) {
      const suite = suiteEntre(ordre, ancre, id);
      choisis = [...new Set([...(ajout ? choisis : []), ...suite])];
      return;
    }
    ancre = id;
    if (ajout || !seul) choisis = choisis.includes(id) ? choisis.filter((x) => x !== id) : [...choisis, id];
    else choisis = choisis.length === 1 && choisis[0] === id ? [] : [id];
  }

  let listeFacade = $state<HTMLDivElement>();
  function clavierDeLaFacade(evenement: KeyboardEvent) {
    const cible = evenement.target as HTMLElement;
    const id = cible.dataset.port;
    if (!id) return;
    const rang = ordreDeLaFacade.indexOf(id);
    if (evenement.key === "Escape" && choisis.length > 0) {
      // Échap lâche d'abord les ports choisis ; l'équipement reste choisi.
      evenement.preventDefault();
      evenement.stopPropagation();
      choisis = [];
      return;
    }
    if (evenement.key === " " || evenement.key === "Enter") {
      evenement.preventDefault();
      evenement.stopPropagation();
      choisir(id, evenement, ordreDeLaFacade, false);
      return;
    }
    const carres = [...(listeFacade?.querySelectorAll<HTMLElement>("[role=option]") ?? [])];
    let vers = -1;
    if (evenement.key === "ArrowRight") vers = Math.min(carres.length - 1, rang + 1);
    else if (evenement.key === "ArrowLeft") vers = Math.max(0, rang - 1);
    else if (evenement.key === "Home") vers = 0;
    else if (evenement.key === "End") vers = carres.length - 1;
    else if (evenement.key === "ArrowDown" || evenement.key === "ArrowUp") {
      // Une rangée plus bas ou plus haut : le carré le plus proche à la verticale.
      const ici = cible.getBoundingClientRect();
      const bas = evenement.key === "ArrowDown";
      const autres = carres
        .map((c, k) => ({ k, r: c.getBoundingClientRect() }))
        .filter(({ r }) => (bas ? r.top > ici.top + 2 : r.top < ici.top - 2));
      const voisine = bas ? Math.min(...autres.map((a) => a.r.top)) : Math.max(...autres.map((a) => a.r.top));
      vers =
        autres
          .filter((a) => Math.abs(a.r.top - voisine) < 2)
          .sort((a, b) => Math.abs(a.r.left - ici.left) - Math.abs(b.r.left - ici.left))[0]?.k ?? -1;
    } else return;
    // Les flèches sont pour la façade : l'équipement ne bouge pas sur le plan.
    evenement.preventDefault();
    evenement.stopPropagation();
    const suivant = ordreDeLaFacade[vers];
    if (vers < 0 || !suivant) return;
    courant = suivant;
    carres[vers]?.focus();
    if (evenement.shiftKey) choisir(suivant, { shiftKey: true, ctrlKey: false, metaKey: false }, ordreDeLaFacade);
  }

  const tousChoisis = $derived(vues.length > 0 && vues.every((i) => choisis.includes(i.id)));
  const certainsChoisis = $derived(vues.some((i) => choisis.includes(i.id)));

  // --- Régler plusieurs ports ----------------------------------------------------------------------------------------

  let reglage = $state<"acces" | "trunk" | "plage" | null>(null);
  let vlanAcces = $state("");
  let natif = $state("");
  let tagues = $state("");
  const tousPoe = $derived(ports.length > 0 && ports.every((p) => p.poe?.fournit));
  const tousDesactives = $derived(ports.length > 0 && ports.every((p) => p.admin === "desactive"));

  function ouvrirReglage(quoi: "acces" | "trunk") {
    if (reglage === quoi) {
      reglage = null;
      return;
    }
    const configs = ports.map((p) => champsDe(configDe(p)));
    const commun = <T,>(lire: (c: ChampsVlan) => T) => {
      const valeurs = new Set(configs.map(lire));
      return valeurs.size === 1 ? [...valeurs][0] : undefined;
    };
    vlanAcces = quoi === "acces" ? String(commun((c) => (c.tagues ? undefined : c.nonTague)) ?? "") : vlanAcces;
    natif = String(commun((c) => (c.tagues ? c.nonTague : undefined)) ?? "");
    tagues = commun((c) => c.tagues) ?? "";
    reglage = quoi;
    void tick().then(() => document.querySelector<HTMLInputElement>(".reglage input")?.focus());
  }

  const lesPorts = (n: number) => (n > 1 ? `${n} ports` : "Le port");

  function appliquerAcces(evenement: SubmitEvent) {
    evenement.preventDefault();
    const n = lireNumeroVlan(vlanAcces);
    if (n === null || n === undefined) {
      editeur.annonce = "VLAN d'accès : un numéro de 1 à 4094.";
      return;
    }
    const ids = [...choisis];
    if (editeur.agir((s) => mettreEnAcces(s, equipement.id, ids, n), `${lesPorts(ids.length)} en accès sur le VLAN ${n}.`))
      reglage = null;
  }

  function appliquerTrunk(evenement: SubmitEvent) {
    evenement.preventDefault();
    const n = lireNumeroVlan(natif);
    const liste = lireListeVlan(tagues);
    if (n === null) {
      editeur.annonce = "VLAN natif : un numéro de 1 à 4094, ou rien.";
      return;
    }
    if (!liste) {
      editeur.annonce = "VLAN tagués : des numéros de 1 à 4094, séparés par des virgules (« 10,20,30-40 »).";
      return;
    }
    const ids = [...choisis];
    const fait = editeur.agir(
      (s) => mettreEnTrunk(s, equipement.id, ids, { tagues: liste, ...(n !== undefined ? { natif: n } : {}) }),
      `${lesPorts(ids.length)} en trunk : ${n !== undefined ? `U ${n} · ` : ""}T ${liste}.`,
    );
    if (fait) reglage = null;
  }

  function poe() {
    const ids = [...choisis];
    const fourni = { valeur: false };
    editeur.agir((s) => {
      fourni.valeur = basculerPoe(s, equipement.id, ids);
    });
    editeur.annonce = fourni.valeur ? `${lesPorts(ids.length)} fournissent le PoE.` : `${lesPorts(ids.length)} ne fournissent plus le PoE.`;
  }

  function activation() {
    const ids = [...choisis];
    const etat = { valeur: "actif" as Interface["admin"] };
    editeur.agir((s) => {
      etat.valeur = basculerActivation(s, equipement.id, ids);
    });
    editeur.annonce = `${lesPorts(ids.length)} ${etat.valeur === "desactive" ? "désactivé" : "réactivé"}${ids.length > 1 ? "s" : ""}.`;
  }

  // --- Une plage de ports -----------------------------------------------------------------------------------------------

  const proposee = $derived(membrePlage === null ? null : plageProposee(equipement, schema.modeles, membrePlage));
  let plage = $state<Plage | null>(null);
  const bornes = $derived(proposee ? bornesDeLaPlage(proposee) : null);
  const apercu = $derived(plage && plage.motif.includes("{p}") ? bornesDeLaPlage(plage) : null);

  function ouvrirPlage() {
    if (reglage === "plage") {
      reglage = null;
      return;
    }
    const membre = membrePlage ?? 1;
    plage = proposee
      ? { ...proposee }
      : {
          membre,
          motif: empile ? "{m}/{p}" : "port{p}",
          de: 1,
          a: 8,
          genre: "cuivre",
          connecteur: "RJ45",
          vitesse: 1000,
        };
    reglage = "plage";
    void tick().then(() => document.querySelector<HTMLInputElement>(".reglage.plage input")?.focus());
  }

  function ajouterLaPlage(evenement: SubmitEvent) {
    evenement.preventDefault();
    const voulue = plage ? { ...$state.snapshot(plage), de: Number(plage.de), a: Number(plage.a) } : null;
    if (!voulue) return;
    let n = 0;
    const [premier, dernier] = bornesDeLaPlage(voulue);
    if (editeur.agir((s) => (n = ajouterPlage(s, equipement.id, voulue)))) {
      editeur.annonce = `${n > 1 ? `${n} ports ajoutés` : "Un port ajouté"} : ${premier}${n > 1 ? ` à ${dernier}` : ""}.`;
      reglage = null;
    }
  }

  // --- Le détail d'un port ----------------------------------------------------------------------------------------------

  function ouvrir(id: string) {
    interfaceChoisie = interfaceChoisie === id ? null : id;
    if (interfaceChoisie) void tick().then(() => document.querySelector(".interface")?.scrollIntoView({ block: "nearest" }));
  }

  function modifier(action: (e: Equipement) => void, annonce?: string) {
    const id = equipement.id;
    editeur.agir((s) => {
      const e = s.equipements.find((x) => x.id === id);
      if (e) action(e);
    }, annonce);
  }

  function modifierInterface(action: (i: Interface) => void) {
    const id = interfaceChoisie;
    modifier((e) => {
      const i = e.interfaces.find((x) => x.id === id);
      if (i) action(i);
    });
  }

  function renommer(nom: string) {
    const id = interfaceChoisie;
    if (!id) return;
    editeur.agir((s) => renommerInterface(s, { equipement: equipement.id, interface: id }, nom));
  }

  /** « 10.0.0.1/24 », une par ligne. */
  function ecrireAdresses(texte: string) {
    modifierInterface((i) => {
      i.adresses = texte
        .split(/[\n,;]+/)
        .map((l) => l.trim())
        .filter(Boolean)
        .slice(0, 32)
        .map((l) => {
          const [adresse = "", prefixe] = l.split("/");
          const n = Number(prefixe);
          return prefixe !== undefined && Number.isInteger(n) && n >= 0 && n <= 128 ? { adresse, prefixe: n } : { adresse };
        });
    });
  }

  function retirerInterface() {
    const id = interfaceChoisie;
    if (!id) return;
    if ((relies.get(id) ?? []).length > 0) {
      editeur.annonce = "Cette interface porte un câble : retirez ou rebranchez d'abord le lien.";
      return;
    }
    editeur.agir((s) => {
      const e = s.equipements.find((x) => x.id === equipement.id);
      if (!e) return;
      e.interfaces = e.interfaces.filter((i) => i.id !== id);
      if (e.gestion?.interface === id) delete e.gestion.interface;
      for (const a of s.agregats) if (a.equipement === e.id) a.membres = a.membres.filter((m) => m !== id);
      s.agregats = s.agregats.filter((a) => a.membres.length > 0);
    }, "Interface retirée.");
    interfaceChoisie = null;
  }

  // VLAN de l'interface ouverte (D51) : ceux de son agrégat si elle en est membre.
  const agregatChoisi = $derived(iface ? agregatDe(schema, { equipement: equipement.id, interface: iface.id }) : undefined);
  const champsVlan = $derived(iface ? champsDe(configDe(iface)) : {});

  function ecrireVlan(cle: keyof ChampsVlan, champ: HTMLInputElement) {
    const id = interfaceChoisie;
    if (!id) return;
    const valeur = cle === "nonTague" ? lireNumeroVlan(champ.value) : lireListeVlan(champ.value);
    if (valeur === null) {
      editeur.annonce =
        cle === "nonTague"
          ? "VLAN non tagué : un numéro de 1 à 4094."
          : "VLAN tagués : des numéros de 1 à 4094, séparés par des virgules (« 10,20,30-40 »).";
      champ.value = String(champsVlan[cle] ?? "");
      return;
    }
    const nouveaux: ChampsVlan = { ...champsVlan };
    if (valeur === undefined) delete nouveaux[cle];
    else (nouveaux as Record<string, unknown>)[cle] = valeur;
    const bout = { equipement: equipement.id, interface: id };
    editeur.agir((s) => ecrireConfigDuBout(s, bout, configDepuis(nouveaux, configDuBout(s, bout))));
    if (typeof valeur === "string") champ.value = valeur;
  }

  // --- Le profil des ports (D39) ------------------------------------------------------------------------------------

  const nombreDeMembres = $derived(equipement.membres.length);
  const groupesDuPremier = $derived(equipement.membres[0]?.groupes ?? []);
  const membreDuProfil = $derived(
    empile && membreActif !== "autres" ? equipement.membres.find((m) => m.numero === membreActif) : undefined,
  );

  /**
   * Change un groupe de ports de chaque membre (ou du seul membre `seul`). Un groupe qui suivait celui-ci (même
   * motif, numéros à la suite : les uplinks après les ports d'accès) se décale avec lui ; ses ports gardent leur
   * identité, donc leurs câbles (D51).
   */
  function changerGroupe(indice: number, cle: keyof GroupePorts, valeur: string, seul?: number) {
    changer((e) => {
      for (const membre of e.membres) {
        if (seul !== undefined && membre.numero !== seul) continue;
        const groupes = (membre.groupes ?? groupesDuMembre(membre, schema.modeles)).map((g) => ({ ...g }));
        const g = groupes[indice];
        if (!g) continue;
        const premierAvant = g.premier;
        const finAvant = g.premier + g.nombre;
        if (cle === "nombre" || cle === "premier" || cle === "vitesse") {
          const n = Number(valeur);
          if (cle === "vitesse" && !valeur) delete g.vitesse;
          else if (Number.isFinite(n) && n >= 0) (g as Record<string, unknown>)[cle] = Math.round(n);
        } else if (cle === "connecteur" && !valeur.trim()) delete g.connecteur;
        else (g as Record<string, unknown>)[cle] = valeur.trim();
        const rekey = (groupe: number, de: number) => {
          for (const i of e.interfaces) {
            const [m, gr, numero] = (i.origine ?? "").split(":").map(Number);
            if (m === membre.numero && gr === groupe && numero !== undefined) i.origine = cleDOrigine(m, groupe, numero + de);
          }
        };
        if (g.premier !== premierAvant) rekey(indice, g.premier - premierAvant);
        const decalage = g.premier + g.nombre - finAvant;
        if (decalage !== 0 && (cle === "nombre" || cle === "premier")) {
          let fin = finAvant;
          for (const [k, suivant] of groupes.entries()) {
            if (k <= indice || suivant.motif !== g.motif || suivant.premier !== fin) continue;
            rekey(k, decalage);
            fin = suivant.premier + suivant.nombre;
            suivant.premier += decalage;
          }
        }
        membre.groupes = groupes;
      }
    });
  }

  /** Une interface de plus (sous-interface, interface virtuelle…), sur le membre regardé s'il y a un stack. */
  function ajouterUneInterface() {
    let cree = "";
    const numero = empile && membreActif !== "autres" ? membreActif : null;
    editeur.agir((s) => {
      const e = s.equipements.find((x) => x.id === equipement.id);
      if (!e) return;
      if (numero === null) {
        cree = ajouterInterface(s, e.id).id;
        return;
      }
      const membre = e.membres.find((m) => m.numero === numero);
      const motif = (membre ? groupesDuMembre(membre, s.modeles) : [])[0]?.motif ?? "{m}/{p}";
      const pris = new Set(e.interfaces.map((i) => i.nom));
      let n = Math.max(0, ...e.interfaces.filter((i) => i.membre === numero).map((i) => i.numero ?? 0)) + 1;
      while (pris.has(nomDePort(motif, numero, n))) n++;
      cree = ajouterInterface(s, e.id, { membre: numero, numero: n, nom: nomDePort(motif, numero, n) }).id;
    }, "Interface ajoutée.");
    if (cree) interfaceChoisie = cree;
  }

  function ajouterGroupe() {
    changer((e) => {
      for (const membre of e.membres) {
        membre.groupes = [
          ...(membre.groupes ?? []),
          { nom: "Ports", motif: "port{p}", nombre: 2, premier: 1, genre: "cuivre", connecteur: "RJ45", vitesse: 1000 },
        ];
      }
    });
  }

  function retirerGroupe(indice: number) {
    changer((e) => {
      for (const membre of e.membres) membre.groupes = (membre.groupes ?? []).filter((_, i) => i !== indice);
    });
  }

  function appliquerModele(modeleId: string) {
    const modele = schema.modeles.find((m) => m.id === modeleId);
    if (!modele) return;
    changer((e) => {
      for (const membre of e.membres) {
        membre.modele = modele.id;
        membre.groupes = modele.groupes.map((g) => ({ ...g }));
      }
      e.reference = modele.reference;
      if (modele.fabricant) e.fabricant = modele.fabricant;
    });
  }

  function enregistrerModele() {
    const reference = equipement.reference || `${def.nom} ${equipement.interfaces.length} ports`;
    editeur.agir((s) => {
      s.modeles.push({
        id: nouvelId("m"),
        type: equipement.type,
        reference,
        ...(equipement.fabricant ? { fabricant: equipement.fabricant } : {}),
        groupes: groupesDuPremier.map((g) => ({ ...g })),
      });
    }, `Modèle matériel « ${reference} » enregistré dans le schéma.`);
  }

  const agregatsDeLEquipement = $derived(schema.agregats.filter((a) => a.equipement === equipement.id));
  const MODES: Record<string, string> = { acces: "Accès", trunk: "Trunk", hybride: "Hybride" };
</script>

<div class="ports">
  {#if empile || sansMembre}
    <div class="ligne-haute">
      <div class="membres" role="group" aria-label="Membre du stack">
        {#each equipement.membres as m (m.numero)}
          <button type="button" aria-pressed={membreActif === m.numero} onclick={() => (membreVu = m.numero)}>Membre {m.numero}</button>
        {/each}
        {#if sansMembre}
          <button type="button" aria-pressed={membreActif === "autres"} title="Interfaces rattachées à aucun membre (VLAN, ajoutées à la main)" onclick={() => (membreVu = "autres")}>Autres</button>
        {/if}
      </div>
      <span class="resume">{libres} {libres > 1 ? "libres" : "libre"}{trunks > 0 ? ` · ${trunks} ${trunks > 1 ? "trunks" : "trunk"}` : ""}</span>
    </div>
  {:else if facade.length > 0}
    <p class="ligne-haute"><span class="resume seul">{libres} {libres > 1 ? "libres" : "libre"}{trunks > 0 ? ` · ${trunks} ${trunks > 1 ? "trunks" : "trunk"}` : ""}</span></p>
  {/if}

  {#if facade.length > 0}
    <!-- svelte-ignore a11y_interactive_supports_focus -->
    <div
      bind:this={listeFacade}
      class="facade"
      role="listbox"
      aria-multiselectable="true"
      aria-label={empile && membreActif !== "autres" ? `Façade du membre ${membreActif}` : "Façade"}
      aria-describedby="aide-facade"
      onkeydown={clavierDeLaFacade}
    >
      {#each facade as i (i.id)}
        {@const relie = estRelie(i.id)}
        <!-- svelte-ignore a11y_click_events_have_key_events -->
        <div
          role="option"
          class="carre"
          class:relie
          class:choisi={choisis.includes(i.id)}
          class:desactive={i.admin === "desactive"}
          data-port={i.id}
          aria-selected={choisis.includes(i.id)}
          aria-label="{i.nom}{relie ? ', relié' : ', libre'}{i.admin === 'desactive' ? ', désactivé' : ''}"
          title="{i.nom}{relie ? ` → ${(relies.get(i.id) ?? [])[0]?.texte ?? representes.get(i.id)?.texte ?? ''}` : ' : libre'}"
          tabindex={tabulable === i.id ? 0 : -1}
          onfocus={() => (courant = i.id)}
          onclick={(e) => choisir(i.id, e, ordreDeLaFacade)}
        >
          {numeroAffiche(i)}
        </div>
      {/each}
    </div>
    <p id="aide-facade" class="visuellement-cache">Un clic choisit un port ; Maj et clic, une suite ; Ctrl et clic en ajoute un. Au clavier : les flèches, puis Espace.</p>
  {/if}

  {#if choisis.length > 0}
    <div class="lot" role="toolbar" aria-label="Ports choisis">
      <b><span class="lot-long">{choisis.length} {choisis.length > 1 ? "ports choisis" : "port choisi"}</span><span class="lot-court">{choisis.length} {choisis.length > 1 ? "choisis" : "choisi"}</span></b>
      <button type="button" class="bouton petit" data-action="acces" aria-expanded={reglage === "acces"} onclick={() => ouvrirReglage("acces")}>Accès VLAN…</button>
      <button type="button" class="bouton petit" data-action="trunk" aria-expanded={reglage === "trunk"} onclick={() => ouvrirReglage("trunk")}>Trunk…</button>
      <button type="button" class="bouton petit" data-action="poe" aria-pressed={tousPoe} title={tousPoe ? "Ne plus fournir le PoE" : "Fournir le PoE"} onclick={poe}>PoE</button>
      <button type="button" class="bouton petit" data-action="activation" onclick={activation}>{tousDesactives ? "Activer" : "Désactiver"}</button>
      <button type="button" class="bouton petit discret" aria-label="Ne plus choisir ces ports" title="Ne plus choisir" onclick={() => { choisis = []; reglage = reglage === "plage" ? reglage : null; }}>✕</button>
    </div>
    {#if reglage === "acces"}
      <form class="reglage" onsubmit={appliquerAcces}>
        <label>
          <span>VLAN d'accès</span>
          <input class="champ" inputmode="numeric" bind:value={vlanAcces} placeholder="10" />
        </label>
        <button type="submit" class="bouton petit principal">Appliquer</button>
        <button type="button" class="bouton petit" onclick={() => (reglage = null)}>Annuler</button>
      </form>
    {:else if reglage === "trunk"}
      <form class="reglage" onsubmit={appliquerTrunk}>
        <label>
          <span>VLAN natif</span>
          <input class="champ court" inputmode="numeric" bind:value={natif} placeholder="Aucun" />
        </label>
        <label class="long">
          <span>VLAN tagués</span>
          <input class="champ" bind:value={tagues} placeholder="10,20,30-40" />
        </label>
        <button type="submit" class="bouton petit principal">Appliquer</button>
        <button type="button" class="bouton petit" onclick={() => (reglage = null)}>Annuler</button>
      </form>
    {/if}
  {/if}

  {#if vues.length > 0}
    <div class="defile">
      <table class="tableau" aria-label={empile && membreActif !== "autres" ? `Ports du membre ${membreActif}` : "Ports"}>
        <thead>
          <tr>
            <th class="col-case" scope="col">
              <input
                type="checkbox"
                aria-label="Choisir tous ces ports"
                checked={tousChoisis}
                {@attach (c: HTMLInputElement) => {
                  c.indeterminate = certainsChoisis && !tousChoisis;
                }}
                onchange={(e) => {
                  const ids = new Set(ordreDuTableau);
                  choisis = e.currentTarget.checked ? [...new Set([...choisis, ...ids])] : choisis.filter((id) => !ids.has(id));
                }}
              />
            </th>
            <th scope="col">Port</th>
            <th class="col-nom" scope="col">Nom</th>
            <th scope="col">Mode</th>
            <th scope="col">VLAN</th>
            <th class="col-vitesse" scope="col">Vitesse</th>
            <th scope="col">Relié à</th>
          </tr>
        </thead>
        <tbody>
          {#each vues as i (i.id)}
            {@const config = configDe(i)}
            {@const relie = (relies.get(i.id) ?? [])[0] ?? representes.get(i.id)}
            <!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_noninteractive_element_interactions -->
            <tr
              class:choisi={choisis.includes(i.id)}
              class:ouvert={interfaceChoisie === i.id}
              class:desactive={i.admin === "desactive"}
              data-port={i.id}
              onclick={(e) => {
                if (!(e.target as Element).closest("input, button, a, select")) ouvrir(i.id);
              }}
            >
              <td class="col-case">
                <input
                  type="checkbox"
                  aria-label="Choisir {i.nom}"
                  checked={choisis.includes(i.id)}
                  onclick={(e) => {
                    e.preventDefault();
                    choisir(i.id, e, ordreDuTableau, false);
                  }}
                />
              </td>
              <td class="col-port">
                <button type="button" class="nom-port" aria-expanded={interfaceChoisie === i.id} title="{i.nom} : le détail du port" onclick={() => ouvrir(i.id)}>
                  <span class="point" class:allume={relie !== undefined} aria-hidden="true"></span>{i.nom}
                </button>
              </td>
              <td class="col-nom">{#if i.description}{i.description}{:else}<span class="vide">—</span>{/if}</td>
              <td>
                {#if config}
                  <span class="mode mode-{config.mode}">{MODES[config.mode]}</span>
                {:else}
                  <span class="vide">—</span>
                {/if}
              </td>
              <td class="col-vlan">{texteVlanDuPort(config) || "—"}</td>
              <td class="col-vitesse">{vitesseEnClair(i.vitesse) || "—"}</td>
              <td class="col-relie">
                {#if relie}
                  <button type="button" class="lien-texte" title="Choisir ce câble" onclick={() => (editeur.selection = [relie.lien])}>{relie.texte}</button>
                {:else}
                  <span class="vide">—</span>
                {/if}
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {:else}
    <p class="aide">Aucun port.</p>
  {/if}

  {#if membrePlage !== null}
    <button type="button" class="bouton petit ajouter" data-action="plage" aria-expanded={reglage === "plage"} onclick={ouvrirPlage}>
      {bornes ? `Ajouter des ports : ${bornes[0]} à ${bornes[1]}…` : "Ajouter des ports…"}
    </button>
    {#if reglage === "plage" && plage}
      <form class="reglage plage" onsubmit={ajouterLaPlage}>
        <label class="long">
          <span>Nom des ports ({"{p}"} : le numéro)</span>
          <input class="champ" bind:value={plage.motif} spellcheck="false" />
        </label>
        <label>
          <span>Du numéro</span>
          <input class="champ court" type="number" min="0" bind:value={plage.de} />
        </label>
        <label>
          <span>au numéro</span>
          <input class="champ court" type="number" min="0" bind:value={plage.a} />
        </label>
        <p class="apercu" aria-live="polite">
          {#if apercu && Number(plage.a) >= Number(plage.de)}
            {Number(plage.a) - Number(plage.de) + 1} ports : {apercu[0]}{Number(plage.a) > Number(plage.de) ? ` à ${apercu[1]}` : ""}
          {:else}
            Le nom contient {"{p}"}, et la plage va d'un numéro au même ou à un plus grand.
          {/if}
        </p>
        <button type="submit" class="bouton petit principal">Ajouter</button>
        <button type="button" class="bouton petit" onclick={() => (reglage = null)}>Annuler</button>
      </form>
    {/if}
  {/if}

  {#if iface}
    <fieldset class="interface">
      <legend>Port « {iface.nom} »</legend>
      <button type="button" class="bouton discret petit fermer" aria-label="Fermer le détail du port" onclick={() => (interfaceChoisie = null)}>✕</button>
      <p class="aide">
        {(relies.get(iface.id) ?? []).length
          ? `Relié à ${(relies.get(iface.id) ?? []).map((r) => r.texte).join(", ")}.`
          : representes.has(iface.id)
            ? `Relié à ${representes.get(iface.id)?.texte}.`
            : "Libre."}
        Identifiant interne {iface.id} : il ne change pas quand on renomme.
      </p>
      <div class="champs">
        <label>
          <span>Nom affiché</span>
          <input class="champ" value={iface.nom} onchange={(e) => renommer(e.currentTarget.value)} />
        </label>
        <label>
          <span>Description</span>
          <input class="champ" value={iface.description ?? ""} placeholder="Uplink, borne du hall…" onchange={(e) => modifierInterface((i) => { if (e.currentTarget.value.trim()) i.description = e.currentTarget.value.trim(); else delete i.description; })} />
        </label>
        <label>
          <span>Genre</span>
          <select class="champ" value={iface.genre} onchange={(e) => modifierInterface((i) => (i.genre = e.currentTarget.value as Interface["genre"]))}>
            {#each GENRES_INTERFACE as x (x.valeur)}<option value={x.valeur}>{x.nom}</option>{/each}
          </select>
        </label>
        <label>
          <span>Nature</span>
          <select class="champ" value={iface.nature} onchange={(e) => modifierInterface((i) => (i.nature = e.currentTarget.value as Interface["nature"]))}>
            {#each NATURES_INTERFACE as x (x.valeur)}<option value={x.valeur}>{x.nom}</option>{/each}
          </select>
        </label>
        <label>
          <span>Rôle</span>
          <select class="champ" value={iface.role ?? ""} onchange={(e) => modifierInterface((i) => { if (e.currentTarget.value) i.role = e.currentTarget.value as Interface["role"]; else delete i.role; })}>
            <option value="">—</option>
            {#each ROLES_INTERFACE as x (x.valeur)}<option value={x.valeur}>{x.nom}</option>{/each}
          </select>
        </label>
        <label>
          <span>Connecteur</span>
          <input class="champ" value={iface.connecteur ?? ""} placeholder="RJ45, SFP+, LC…" onchange={(e) => modifierInterface((i) => { if (e.currentTarget.value.trim()) i.connecteur = e.currentTarget.value.trim(); else delete i.connecteur; })} />
        </label>
        <label>
          <span>Vitesse (Mbit/s){iface.vitesse ? ` · ${vitesseEnClair(iface.vitesse)}` : ""}</span>
          <input class="champ" type="number" min="0" value={iface.vitesse ?? ""} onchange={(e) => modifierInterface((i) => { const n = Number(e.currentTarget.value); if (n > 0) i.vitesse = Math.round(n); else delete i.vitesse; })} />
        </label>
        <label>
          <span>État administratif</span>
          <select class="champ" value={iface.admin} onchange={(e) => modifierInterface((i) => (i.admin = e.currentTarget.value as Interface["admin"]))}>
            <option value="actif">Activée</option>
            <option value="desactive">Désactivée</option>
          </select>
        </label>
        <label>
          <span>État opérationnel (documenté)</span>
          <select class="champ" value={iface.oper ?? ""} onchange={(e) => modifierInterface((i) => { if (e.currentTarget.value) i.oper = e.currentTarget.value as Interface["oper"]; else delete i.oper; })}>
            <option value="">Non renseigné</option>
            <option value="up">Up</option>
            <option value="down">Down</option>
            <option value="inconnu">Inconnu</option>
          </select>
        </label>
        {#if iface.nature === "sous-interface" || iface.nature === "vlan"}
          <label>
            <span>Interface parente</span>
            <select class="champ" value={iface.parent ?? ""} onchange={(e) => modifierInterface((i) => { if (e.currentTarget.value) i.parent = e.currentTarget.value; else delete i.parent; })}>
              <option value="">—</option>
              {#each equipement.interfaces.filter((x) => x.id !== iface.id && x.nature === "physique") as x (x.id)}<option value={x.id}>{x.nom}</option>{/each}
            </select>
          </label>
        {/if}
        {#if agregatChoisi}
          <p class="aide large">Membre de l'agrégat « {agregatChoisi.nom} » : ses VLAN sont ceux de l'agrégat.</p>
        {/if}
        <label>
          <span>VLAN non tagué (untagged)</span>
          <input class="champ" inputmode="numeric" value={champsVlan.nonTague ?? ""} placeholder="Aucun" onchange={(e) => ecrireVlan("nonTague", e.currentTarget)} />
        </label>
        <label>
          <span>VLAN tagués (tagged)</span>
          <input class="champ" value={champsVlan.tagues ?? ""} placeholder="10,20,30-40" onchange={(e) => ecrireVlan("tagues", e.currentTarget)} />
        </label>
        <label class="large">
          <span>Adresses IP (une par ligne, « 10.0.0.1/24 », IPv4 ou IPv6)</span>
          <textarea class="champ" rows="2" value={iface.adresses.map((a) => `${a.adresse}${a.prefixe !== undefined ? `/${a.prefixe}` : ""}`).join("\n")} onchange={(e) => ecrireAdresses(e.currentTarget.value)}></textarea>
        </label>
        <label class="large">
          <span>Adresses MAC (une par ligne)</span>
          <textarea class="champ" rows="1" value={iface.macs.join("\n")} onchange={(e) => modifierInterface((i) => (i.macs = e.currentTarget.value.split(/\s+/).filter(Boolean).slice(0, 16)))}></textarea>
        </label>
        <label class="case">
          <input type="checkbox" checked={iface.poe?.fournit ?? false} onchange={(e) => modifierInterface((i) => (i.poe = { ...(i.poe ?? {}), fournit: e.currentTarget.checked || undefined }))} />
          Fournit le PoE
        </label>
        <label class="case">
          <input type="checkbox" checked={iface.poe?.recoit ?? false} onchange={(e) => modifierInterface((i) => (i.poe = { ...(i.poe ?? {}), recoit: e.currentTarget.checked || undefined }))} />
          Alimentée en PoE
        </label>
        <label class="case large">
          <input type="checkbox" checked={iface.multiConnexion ?? false} onchange={(e) => modifierInterface((i) => { if (e.currentTarget.checked) i.multiConnexion = true; else delete i.multiConnexion; })} />
          Accepte plusieurs câbles (cas particulier : concentrateur, bus)
        </label>
      </div>
      <button type="button" class="bouton discret petit attention" onclick={retirerInterface}>Retirer l'interface</button>
    </fieldset>
  {/if}

  {#if agregatsDeLEquipement.length > 0}
    <h4 class="titre-section">Agrégats</h4>
    <ul class="agregats">
      {#each agregatsDeLEquipement as a (a.id)}
        <li>
          {a.nom} ({a.mode === "lacp" ? "LACP" : "statique"}) :
          {a.membres.map((m) => equipement.interfaces.find((i) => i.id === m)?.nom ?? "?").join(", ")}
        </li>
      {/each}
    </ul>
  {/if}

  <details class="profil">
    <summary>Profil des ports{nombreDeMembres > 1 ? " (chaque membre)" : ""}</summary>
    {#if schema.modeles.some((m) => m.type === equipement.type)}
      <label class="ligne">
        <span>Modèle matériel</span>
        <select class="champ" value={equipement.membres[0]?.modele ?? ""} onchange={(e) => appliquerModele(e.currentTarget.value)}>
          <option value="">Profil propre à l'équipement</option>
          {#each schema.modeles.filter((m) => m.type === equipement.type) as m (m.id)}
            <option value={m.id}>{m.fabricant ? `${m.fabricant} ` : ""}{m.reference}</option>
          {/each}
        </select>
      </label>
    {/if}
    {#if groupesDuPremier.length > 0}
      <div class="ports-rapides" role="group" aria-label="Ports de chaque membre">
        <span>{nombreDeMembres > 1 ? "Ports de chaque membre" : "Ports"}</span>
        {#each groupesDuPremier as g, indice (indice)}
          <label title={`${g.nom} : ${g.motif.replace("{m}", "1").replace("{p}", String(g.premier))}…`}>
            <input class="champ nombre" type="number" min="0" max="256" value={g.nombre} onchange={(e) => changerGroupe(indice, "nombre", e.currentTarget.value)} />
            {g.nom.toLowerCase()}
          </label>
        {/each}
      </div>
    {/if}
    {#if membreDuProfil}
      {@const numero = membreDuProfil.numero}
      <div class="ports-rapides membre-seul" role="group" aria-label={`Ports du membre ${numero}`}>
        <span>Membre {numero} seulement</span>
        {#each groupesDuMembre(membreDuProfil, schema.modeles) as gp, indice (indice)}
          <label>
            <input class="champ nombre" type="number" min="0" max="256" value={gp.nombre} onchange={(e) => changerGroupe(indice, "nombre", e.currentTarget.value, numero)} />
            {gp.nom.toLowerCase()}
          </label>
        {/each}
        <span class="aide">Stack de modèles différents.</span>
      </div>
    {/if}
    <p class="aide">Motif du nom : <code>{"{m}"}</code> membre, <code>{"{p}"}</code> numéro (« Gi{"{m}"}/0/{"{p}"} », « LAN{"{p}"} »).</p>
    {#each groupesDuPremier as g, indice (indice)}
      <div class="groupe-ports">
        <input class="champ" aria-label="Nom du groupe" value={g.nom} onchange={(e) => changerGroupe(indice, "nom", e.currentTarget.value)} />
        <input class="champ" aria-label="Motif du nom" value={g.motif} onchange={(e) => changerGroupe(indice, "motif", e.currentTarget.value)} />
        <input class="champ" aria-label="Nombre de ports" type="number" min="0" value={g.nombre} onchange={(e) => changerGroupe(indice, "nombre", e.currentTarget.value)} />
        <input class="champ" aria-label="Premier numéro" type="number" min="0" value={g.premier} onchange={(e) => changerGroupe(indice, "premier", e.currentTarget.value)} />
        <select class="champ" aria-label="Genre" value={g.genre} onchange={(e) => changerGroupe(indice, "genre", e.currentTarget.value)}>
          {#each GENRES_INTERFACE as x (x.valeur)}<option value={x.valeur}>{x.nom}</option>{/each}
        </select>
        <input class="champ" aria-label="Vitesse (Mbit/s)" type="number" min="0" value={g.vitesse ?? ""} placeholder="Mbit/s" onchange={(e) => changerGroupe(indice, "vitesse", e.currentTarget.value)} />
        <button type="button" class="bouton discret petit" aria-label="Retirer ce groupe de ports" onclick={() => retirerGroupe(indice)}>✕</button>
      </div>
    {/each}
    <div class="boutons">
      <button type="button" class="bouton petit" onclick={ajouterGroupe}>Ajouter un groupe de ports</button>
      <button type="button" class="bouton petit" onclick={enregistrerModele}>Enregistrer comme modèle matériel</button>
      <button type="button" class="bouton petit" title={empile && membreActif !== "autres" ? `Une interface de plus sur le membre ${membreActif}` : "Sous-interface, interface virtuelle…"} onclick={ajouterUneInterface}>Ajouter une interface</button>
    </div>
  </details>
</div>

<style>
  .ports {
    display: flex;
    flex-direction: column;
    gap: 10px;
    container-type: inline-size;
  }

  .ligne-haute {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 6px 8px;
    margin: 0;
  }

  .membres {
    display: inline-flex;
    flex-wrap: wrap;
    gap: 2px;
    padding: 2px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
  }

  .membres button {
    min-height: 26px;
    padding: 0 10px;
    border: none;
    border-radius: 6px;
    background: transparent;
    color: var(--texte);
    font-size: 12px;
  }

  .membres button:hover {
    background: var(--surface-2);
  }

  .membres button[aria-pressed="true"] {
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
    font-weight: 600;
  }

  .resume {
    margin-left: auto;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .resume.seul {
    margin-left: 0;
  }

  .facade {
    display: flex;
    flex-wrap: wrap;
    gap: 3px;
    padding: 6px;
    border-radius: var(--rayon);
    background: var(--surface-2);
  }

  .carre {
    display: grid;
    min-width: 22px;
    height: 18px;
    padding: 0 2px;
    border: 1px solid var(--bordure);
    border-radius: 3px;
    background: var(--surface);
    color: var(--texte);
    font-size: 10px;
    font-variant-numeric: tabular-nums;
    line-height: 1;
    cursor: pointer;
    user-select: none;
    place-items: center;
  }

  .carre.relie {
    border-color: var(--principale);
    background: var(--principale);
    color: var(--sur-principale);
    font-weight: 600;
  }

  .carre.desactive {
    border-style: dashed;
  }

  .carre.choisi {
    box-shadow:
      0 0 0 1px var(--surface-2),
      0 0 0 3px var(--focus);
  }

  .carre:focus-visible {
    outline: 2px solid var(--focus);
    outline-offset: 1px;
  }

  .lot {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 5px;
    padding: 6px 6px 6px 8px;
    border-radius: var(--rayon);
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
    font-size: 12px;
  }

  .lot b {
    margin-right: 2px;
  }

  .lot-court {
    display: none;
  }

  .lot .bouton.petit {
    height: 26px;
    padding: 0 8px;
  }

  .lot .bouton.discret {
    justify-content: center;
    width: 26px;
    margin-left: auto;
    padding: 0;
    color: inherit;
  }

  .bouton[aria-pressed="true"] {
    border-color: var(--principale);
    background: var(--principale);
    color: var(--sur-principale);
  }

  .reglage {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    gap: 8px;
    padding: 8px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
  }

  .reglage label {
    flex: 0 1 110px;
  }

  .reglage label.long {
    flex: 1 1 160px;
  }

  .reglage .champ {
    height: 30px;
  }

  .apercu {
    flex: 1 0 100%;
    margin: 0;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .defile {
    overflow-x: auto;
  }

  .tableau {
    width: 100%;
    border-collapse: collapse;
    font-size: 11.5px;
  }

  .tableau th {
    padding: 4px 4px;
    border-bottom: 1px solid var(--bordure);
    color: var(--texte-secondaire);
    font-size: 11px;
    font-weight: 600;
    text-align: left;
    white-space: nowrap;
  }

  .tableau td {
    padding: 3px 4px;
    border-bottom: 1px solid var(--bordure);
    white-space: nowrap;
  }

  .tableau tbody tr {
    cursor: pointer;
  }

  .tableau tbody tr:hover td {
    background: var(--surface-2);
  }

  .tableau tr.choisi td {
    background: var(--navigation-active-fond);
  }

  .tableau tr.ouvert td {
    box-shadow: inset 0 -2px 0 var(--focus);
  }

  .tableau tr.desactive .nom-port {
    color: var(--texte-secondaire);
    text-decoration: line-through;
  }

  .col-case {
    width: 22px;
    padding-right: 0;
  }

  .col-case input {
    margin: 0;
    vertical-align: middle;
  }

  .col-nom {
    max-width: 104px;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .nom-port {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    padding: 2px 0;
    border: none;
    background: none;
    color: var(--texte);
    font-size: 11.5px;
    font-variant-numeric: tabular-nums;
  }

  .point {
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: var(--bordure);
    opacity: 0.6;
  }

  .point.allume {
    background: var(--principale);
    opacity: 1;
  }

  .mode {
    display: inline-block;
    padding: 0 6px;
    border: 1px solid var(--bordure);
    border-radius: 7px;
    font-size: 11px;
    line-height: 17px;
  }

  .mode-acces {
    border-color: transparent;
    background: var(--surface-2);
  }

  .mode-trunk {
    border-color: var(--principale);
    color: var(--principale);
    font-weight: 600;
  }

  .vide {
    color: var(--texte-secondaire);
  }

  .lien-texte {
    max-width: 132px;
    overflow: hidden;
    padding: 0;
    border: none;
    background: none;
    color: var(--texte);
    font-size: 11.5px;
    text-align: left;
    text-decoration: underline dotted;
    text-overflow: ellipsis;
    white-space: nowrap;
    cursor: pointer;
  }

  .ajouter {
    align-self: flex-start;
  }

  .bouton.petit {
    height: 28px;
    padding: 0 10px;
    font-size: 12px;
  }

  label {
    display: flex;
    flex-direction: column;
    gap: 2px;
    min-width: 0;
    font-size: 12px;
  }

  label > span {
    color: var(--texte-secondaire);
  }

  .champ {
    height: 30px;
    font-size: 13px;
  }

  .champ.court {
    width: 90px;
  }

  textarea.champ {
    height: auto;
    padding: 4px 8px;
    resize: vertical;
  }

  .champs {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 6px 8px;
    padding: 4px 0 8px;
  }

  .large {
    grid-column: 1 / -1;
  }

  .case {
    flex-direction: row;
    align-items: center;
    gap: 6px;
  }

  .aide {
    margin: 2px 0 6px;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .interface {
    position: relative;
    margin: 0;
    padding: 6px 8px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
  }

  .interface legend {
    padding: 0 4px;
    font-weight: 600;
  }

  .fermer {
    position: absolute;
    top: 2px;
    right: 4px;
  }

  .attention {
    color: var(--retard);
  }

  .titre-section {
    margin: 0;
    color: var(--texte-secondaire);
    font-size: 11px;
    font-weight: 700;
    letter-spacing: 0.02em;
    text-transform: uppercase;
  }

  .agregats {
    margin: 0;
    padding: 0;
    font-size: 12px;
    list-style: none;
  }

  .profil {
    border-top: 1px solid var(--bordure);
    padding-top: 6px;
  }

  .profil summary {
    padding: 4px 0;
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
  }

  .ligne {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    margin: 4px 0;
  }

  .groupe-ports {
    display: grid;
    grid-template-columns: 1.2fr 1.1fr 0.6fr 0.6fr 1fr 0.8fr auto;
    gap: 3px;
    margin-bottom: 3px;
  }

  .groupe-ports .champ {
    min-width: 0;
    padding: 0 4px;
    font-size: 12px;
  }

  /* Place de la flèche des listes déroulantes (theme.css). */
  .groupe-ports select.champ {
    padding-right: 20px;
  }

  .boutons {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    margin: 6px 0;
  }

  .ports-rapides {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 4px 10px;
    margin: 4px 0;
    font-size: 12px;
  }

  .ports-rapides > span:first-child {
    color: var(--texte-secondaire);
  }

  .ports-rapides label {
    flex-direction: row;
    align-items: center;
    gap: 4px;
  }

  .ports-rapides .nombre {
    width: 58px;
    height: 26px;
    padding: 0 4px;
  }

  .membre-seul {
    padding: 4px 6px;
    border-radius: var(--rayon);
    background: var(--surface-2);
  }

  /* Dans le volet, la vitesse cède sa place la première ; elle reste dans le détail du port. */
  @container (max-width: 519px) {
    .col-vitesse {
      display: none;
    }
  }

  /* Fenêtre étroite (30F) : le tiroir garde le port, le mode, le VLAN et ce à quoi il est relié ; le reste en
     touchant la ligne. Les ports se choisissent sur la façade. */
  @media (max-width: 620px) {
    .col-case,
    .col-nom,
    .col-vitesse {
      display: none;
    }

    .lot-long {
      display: none;
    }

    .lot-court {
      display: inline;
    }
  }
</style>
