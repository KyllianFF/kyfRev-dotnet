<script lang="ts">
  import { definition, TYPES } from "../../lib/topologie/catalogue";
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
    STATUTS,
    vitesseEnClair,
  } from "../../lib/topologie/modele";
  import {
    ajouterInterface,
    auNombreDeMembres,
    changerProfil,
    dupliquerEquipements,
    renommerInterface,
    retirerEquipements,
  } from "../../lib/topologie/operations";
  import {
    cleDOrigine,
    groupesDuMembre,
    interfacesParMembre,
    MEMBRES_MAX,
    nomDePort,
  } from "../../lib/topologie/ports";
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
  import DialogueProfil from "./DialogueProfil.svelte";

  // Propriétés d'un équipement (D39) : l'essentiel d'abord, le reste replié.
  let {
    editeur,
    equipement,
    onconnecter,
  }: {
    editeur: EditeurSchema;
    equipement: Equipement;
    onconnecter: (source: string) => void;
  } = $props();

  const schema = $derived(editeur.schema);
  const def = $derived(definition(equipement.type));
  let interfaceChoisie = $state<string | null>(null);
  const iface = $derived(equipement.interfaces.find((i) => i.id === interfaceChoisie) ?? null);
  let profil = $state<{ modifier: (e: Equipement) => void; retirees: Interface[]; liens: string[] } | null>(
    null,
  );

  /** Modifie l'équipement (annulable). */
  function modifier(action: (e: Equipement) => void, annonce?: string) {
    editeur.agir((s) => {
      const e = s.equipements.find((x) => x.id === equipement.id);
      if (e) action(e);
    }, annonce);
  }

  const texteOuRien = (valeur: string) => valeur.trim() || undefined;
  function ecrireTexte<K extends keyof Equipement>(cle: K, valeur: string) {
    modifier((e) => {
      const propre = texteOuRien(valeur);
      if (propre === undefined && cle !== "nom") delete e[cle];
      else (e as Record<string, unknown>)[cle] = propre ?? e.nom;
    });
  }

  function ecrireGestion(cle: "interface" | "adresse" | "vlan", valeur: string) {
    modifier((e) => {
      const gestion = { ...(e.gestion ?? {}) };
      if (cle === "vlan") {
        const n = Number(valeur);
        if (Number.isInteger(n) && n >= 1 && n <= 4094) gestion.vlan = n;
        else delete gestion.vlan;
      } else if (valeur.trim()) gestion[cle] = valeur.trim();
      else delete gestion[cle];
      e.gestion = gestion;
    });
  }

  function ecrirePropriete(cle: string, valeur: string | number | boolean | null) {
    modifier((e) => {
      if (valeur === null || valeur === "") delete e.proprietes[cle];
      else e.proprietes[cle] = valeur;
    });
  }

  /** Changement de profil (membres, groupes de ports) : appliqué, ou décisions demandées pour les liens touchés. */
  function changer(action: (e: Equipement) => void) {
    const avant = editeur.commencerGeste();
    const resultat = changerProfil(editeur.schema, equipement.id, action);
    if (resultat.applique) {
      editeur.finirGeste(avant);
      editeur.annonce = "Interfaces régénérées.";
      return;
    }
    profil = { modifier: action, retirees: resultat.retirees, liens: resultat.effets.liens.map((l) => l.id) };
  }

  const nombreDeMembres = $derived(equipement.membres.length);
  const groupesDuPremier = $derived(equipement.membres[0]?.groupes ?? []);
  /** Membre dont on règle les ports à part (stack de switchs différents). */
  let membreRegle = $state<number | null>(null);

  /**
   * Change un groupe de ports de chaque membre (ou du seul membre `seul`). Un
   * groupe qui suivait celui-ci (même motif, numéros à la suite : les uplinks
   * après les ports d'accès) se décale avec lui ; ses ports gardent leur
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
            if (m === membre.numero && gr === groupe && numero !== undefined)
              i.origine = cleDOrigine(m, groupe, numero + de);
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

  /** Ports d'un membre, tous groupes confondus. */
  const portsDuMembre = (numero: number | null) =>
    equipement.interfaces.filter((i) => numero === null || i.membre === numero).length;

  /** Une interface de plus sur un membre précis du stack (« 2/29 »). */
  function ajouterSurMembre(numero: number | null) {
    let cree = "";
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
    });
    if (cree) interfaceChoisie = cree;
  }

  function ajouterGroupe() {
    changer((e) => {
      for (const membre of e.membres) {
        membre.groupes = [
          ...(membre.groupes ?? []),
          {
            nom: "Ports",
            motif: "port{p}",
            nombre: 2,
            premier: 1,
            genre: "cuivre",
            connecteur: "RJ45",
            vitesse: 1000,
          },
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

  const occupants = $derived(
    new Map(
      equipement.interfaces.map((i) => [
        i.id,
        liensDeLInterface(schema, equipement.id, i.id).map((l) => {
          const autre = l.a.equipement === equipement.id && l.a.interface === i.id ? l.b : l.a;
          const e = schema.equipements.find((x) => x.id === autre.equipement);
          return `${e?.nom ?? "?"} ${e?.interfaces.find((x) => x.id === autre.interface)?.nom ?? ""}`.trim();
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
          const cable = a.membres.flatMap((m) => occupants.get(m) ?? [])[0];
          return cable
            ? a.membres
                .filter((m) => (occupants.get(m) ?? []).length === 0)
                .map((m) => [m, `${cable} (agrégat ${a.nom})`] as const)
            : [];
        }),
    ),
  );

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
          return prefixe !== undefined && Number.isInteger(n) && n >= 0 && n <= 128
            ? { adresse, prefixe: n }
            : { adresse };
        });
    });
  }

  function retirerInterface() {
    const id = interfaceChoisie;
    if (!id) return;
    if ((occupants.get(id) ?? []).length > 0) {
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

  // VLAN de l'interface choisie (D51) : ceux de son agrégat si elle en est membre.
  const agregatChoisi = $derived(
    iface ? agregatDe(schema, { equipement: equipement.id, interface: iface.id }) : undefined,
  );
  const champsVlan = $derived(
    iface ? champsDe(configDuBout(schema, { equipement: equipement.id, interface: iface.id })) : {},
  );

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

  const agregatsDeLEquipement = $derived(schema.agregats.filter((a) => a.equipement === equipement.id));

  const liensDeLEquipement = $derived(
    schema.liens
      .filter((l) => l.a.equipement === equipement.id || l.b.equipement === equipement.id)
      .map((l) => {
        const local = l.a.equipement === equipement.id ? l.a : l.b;
        const distant = local === l.a ? l.b : l.a;
        const e = schema.equipements.find((x) => x.id === distant.equipement);
        return {
          id: l.id,
          local: equipement.interfaces.find((i) => i.id === local.interface)?.nom ?? "?",
          distant: `${e?.nom ?? "?"} · ${e?.interfaces.find((i) => i.id === distant.interface)?.nom ?? "?"}`,
        };
      }),
  );
</script>

<div class="proprietes">
  <h3 class="titre">{equipement.nom} <span class="type">{def.nom}</span></h3>

  <details open>
    <summary>Général</summary>
    <div class="champs">
      <label>
        <span>Nom</span>
        <input class="champ" value={equipement.nom} onchange={(e) => ecrireTexte("nom", e.currentTarget.value)} />
      </label>
      <label>
        <span>Nom d'hôte</span>
        <input class="champ" value={equipement.nomHote ?? ""} onchange={(e) => ecrireTexte("nomHote", e.currentTarget.value)} />
      </label>
      <label>
        <span>Type</span>
        <select class="champ" value={equipement.type} onchange={(e) => modifier((x) => (x.type = e.currentTarget.value))}>
          {#each TYPES as t (t.type)}<option value={t.type}>{t.nom}</option>{/each}
        </select>
      </label>
      <label>
        <span>Statut</span>
        <select class="champ" value={equipement.statut} onchange={(e) => modifier((x) => (x.statut = e.currentTarget.value as Equipement["statut"]))}>
          {#each STATUTS as s (s.valeur)}<option value={s.valeur}>{s.nom}</option>{/each}
        </select>
      </label>
      <label class="large">
        <span>Rôle</span>
        <input class="champ" value={equipement.role ?? ""} placeholder="Cœur de réseau, accès étage 2…" onchange={(e) => ecrireTexte("role", e.currentTarget.value)} />
      </label>
      <label class="large">
        <span>Description</span>
        <textarea class="champ" rows="2" value={equipement.description ?? ""} onchange={(e) => ecrireTexte("description", e.currentTarget.value)}></textarea>
      </label>
    </div>
  </details>

  <details open>
    <summary>Ports et interfaces <span class="compte">{equipement.interfaces.length}</span></summary>
    {#if def.empilable}
      <label class="ligne">
        <span>Membres du stack</span>
        <input
          class="champ court"
          type="number"
          min="1"
          max={MEMBRES_MAX}
          value={nombreDeMembres}
          onchange={(e) => changer(auNombreDeMembres(Number(e.currentTarget.value)))}
        />
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
    <details class="profil">
      <summary>Profil des ports{nombreDeMembres > 1 ? " (chaque membre)" : ""}</summary>
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
      </div>
    </details>

    <div class="interfaces">
      {#each interfacesParMembre(equipement) as g (g.membre ?? 0)}
        {#if g.membre !== null}
          {@const numero = g.membre}
          {@const membre = equipement.membres.find((m) => m.numero === numero)}
          <div class="tete-membre">
            <p class="membre">Membre {numero} <span class="compte">· {portsDuMembre(numero)} ports</span></p>
            {#if membre}
              <button type="button" class="bouton discret petit" aria-expanded={membreRegle === numero} onclick={() => (membreRegle = membreRegle === numero ? null : numero)}>Ses ports…</button>
            {/if}
            <button type="button" class="bouton discret petit" title={`Ajouter une interface au membre ${numero}`} onclick={() => ajouterSurMembre(numero)}>+ Interface</button>
          </div>
          {#if membre && membreRegle === numero}
            <div class="ports-rapides membre-seul" role="group" aria-label={`Ports du membre ${numero}`}>
              {#each groupesDuMembre(membre, schema.modeles) as gp, indice (indice)}
                <label>
                  <input class="champ nombre" type="number" min="0" max="256" value={gp.nombre} onchange={(e) => changerGroupe(indice, "nombre", e.currentTarget.value, numero)} />
                  {gp.nom.toLowerCase()}
                </label>
              {/each}
              <span class="aide">Ce membre seulement (stack de modèles différents).</span>
            </div>
          {/if}
        {/if}
        <div class="grille-ports">
          {#each g.interfaces as i (i.id)}
            {@const occupant = occupants.get(i.id) ?? []}
            {@const represente = representes.get(i.id)}
            <button
              type="button"
              class="port"
              class:occupe={occupant.length > 0 || represente !== undefined}
              class:choisi={interfaceChoisie === i.id}
              title={`${i.nom}${occupant.length ? ` → ${occupant.join(", ")}` : represente ? ` → ${represente}` : " : libre"}`}
              onclick={() => (interfaceChoisie = interfaceChoisie === i.id ? null : i.id)}
            >
              {i.nom}
            </button>
          {/each}
        </div>
      {/each}
      {#if nombreDeMembres <= 1}
        <button type="button" class="bouton petit" onclick={() => ajouterSurMembre(null)}>Ajouter une interface</button>
      {/if}
    </div>

    {#if iface}
      <fieldset class="interface">
        <legend>Interface « {iface.nom} »</legend>
        <p class="aide">
          {(occupants.get(iface.id) ?? []).length
            ? `Reliée à ${(occupants.get(iface.id) ?? []).join(", ")}.`
            : representes.has(iface.id)
              ? `Reliée à ${representes.get(iface.id)}.`
              : "Libre."}
          Identifiant interne {iface.id} : il ne change pas quand on renomme.
        </p>
        <div class="champs">
          <label>
            <span>Nom affiché</span>
            <input class="champ" value={iface.nom} onchange={(e) => renommer(e.currentTarget.value)} />
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
          <label class="large">
            <span>Description</span>
            <input class="champ" value={iface.description ?? ""} onchange={(e) => modifierInterface((i) => { if (e.currentTarget.value.trim()) i.description = e.currentTarget.value.trim(); else delete i.description; })} />
          </label>
        </div>
        <button type="button" class="bouton discret petit attention" onclick={retirerInterface}>Retirer l'interface</button>
      </fieldset>
    {/if}
  </details>

  <details open>
    <summary>Connexions <span class="compte">{liensDeLEquipement.length}</span></summary>
    {#if liensDeLEquipement.length === 0}<p class="aide">Aucun câble.</p>{/if}
    <ul class="connexions">
      {#each liensDeLEquipement as l (l.id)}
        <li><button type="button" class="lien-texte" onclick={() => (editeur.selection = [l.id])}>{l.local} → {l.distant}</button></li>
      {/each}
    </ul>
    <button type="button" class="bouton petit" onclick={() => onconnecter(equipement.id)}>Connecter…</button>
    {#if agregatsDeLEquipement.length > 0}
      <p class="membre">Agrégats</p>
      <ul class="connexions">
        {#each agregatsDeLEquipement as a (a.id)}
          <li>
            {a.nom} ({a.mode === "lacp" ? "LACP" : "statique"}) :
            {a.membres.map((m) => equipement.interfaces.find((i) => i.id === m)?.nom ?? "?").join(", ")}
          </li>
        {/each}
      </ul>
    {/if}
  </details>

  <details>
    <summary>Réseau et gestion</summary>
    <div class="champs">
      <label>
        <span>Interface de gestion</span>
        <select class="champ" value={equipement.gestion?.interface ?? ""} onchange={(e) => ecrireGestion("interface", e.currentTarget.value)}>
          <option value="">—</option>
          {#each equipement.interfaces as i (i.id)}<option value={i.id}>{i.nom}</option>{/each}
        </select>
      </label>
      <label>
        <span>Adresse de gestion</span>
        <input class="champ" value={equipement.gestion?.adresse ?? ""} placeholder="10.0.0.2" onchange={(e) => ecrireGestion("adresse", e.currentTarget.value)} />
      </label>
      <label>
        <span>VLAN de gestion</span>
        <input class="champ" type="number" min="1" max="4094" value={equipement.gestion?.vlan ?? ""} onchange={(e) => ecrireGestion("vlan", e.currentTarget.value)} />
      </label>
      <label>
        <span>Passerelle</span>
        <input class="champ" value={equipement.passerelle ?? ""} onchange={(e) => ecrireTexte("passerelle", e.currentTarget.value)} />
      </label>
      <label>
        <span>DNS</span>
        <input class="champ" value={equipement.dns ?? ""} onchange={(e) => ecrireTexte("dns", e.currentTarget.value)} />
      </label>
      <label>
        <span>VRF ou contexte</span>
        <input class="champ" value={equipement.vrf ?? ""} onchange={(e) => ecrireTexte("vrf", e.currentTarget.value)} />
      </label>
      {#if equipement.interfaces.some((i) => i.poe?.fournit)}
        <label>
          <span>Budget PoE (W)</span>
          <input class="champ" type="number" min="0" value={equipement.poeBudgetW ?? ""} onchange={(e) => modifier((x) => { const n = Number(e.currentTarget.value); if (n > 0) x.poeBudgetW = n; else delete x.poeBudgetW; })} />
        </label>
      {/if}
      <label>
        <span>Système, firmware ou version</span>
        <input class="champ" value={equipement.systeme ?? ""} onchange={(e) => ecrireTexte("systeme", e.currentTarget.value)} />
      </label>
    </div>
  </details>

  {#if def.champs.length > 0}
    <details>
      <summary>Propriétés : {def.nom.toLowerCase()}</summary>
      <div class="champs">
        {#each def.champs as c (c.cle)}
          {#if c.genre === "oui-non"}
            <label class="case">
              <input type="checkbox" checked={equipement.proprietes[c.cle] === true} onchange={(e) => ecrirePropriete(c.cle, e.currentTarget.checked || null)} />
              {c.nom}
            </label>
          {:else if c.genre === "choix"}
            <label>
              <span>{c.nom}</span>
              <select class="champ" value={String(equipement.proprietes[c.cle] ?? "")} onchange={(e) => ecrirePropriete(c.cle, e.currentTarget.value || null)}>
                <option value="">—</option>
                {#each c.options ?? [] as o (o)}<option value={o}>{o}</option>{/each}
              </select>
            </label>
          {:else}
            <label>
              <span>{c.nom}{c.unite ? ` (${c.unite})` : ""}</span>
              <input
                class="champ"
                type={c.genre === "nombre" ? "number" : "text"}
                placeholder={c.aide ?? ""}
                value={String(equipement.proprietes[c.cle] ?? "")}
                onchange={(e) => ecrirePropriete(c.cle, c.genre === "nombre" ? (e.currentTarget.value === "" ? null : Number(e.currentTarget.value)) : e.currentTarget.value.trim() || null)}
              />
            </label>
          {/if}
        {/each}
      </div>
    </details>
  {/if}

  <details>
    <summary>Emplacement et inventaire</summary>
    <div class="champs">
      {#each [["fabricant", "Fabricant"], ["reference", "Modèle"], ["inventaire", "Référence d'inventaire"], ["serie", "Numéro de série"], ["site", "Site"], ["salle", "Salle"], ["baie", "Baie"], ["positionRack", "Position dans la baie (U)"], ["contact", "Responsable ou contact"], ["garantie", "Garantie ou maintenance"], ["documentation", "Adresse de la documentation"], ["referenceSecret", "Référence dans le gestionnaire de secrets"]] as [cle, nom] (cle)}
        <label>
          <span>{nom}</span>
          <input class="champ" value={String(equipement[cle as keyof Equipement] ?? "")} onchange={(e) => ecrireTexte(cle as keyof Equipement, e.currentTarget.value)} />
        </label>
      {/each}
      <label class="large">
        <span>Étiquettes (séparées par des virgules)</span>
        <input class="champ" value={equipement.tags.join(", ")} onchange={(e) => modifier((x) => (x.tags = e.currentTarget.value.split(",").map((t) => t.trim()).filter(Boolean).slice(0, 32)))} />
      </label>
      <label class="large">
        <span>Notes</span>
        <textarea class="champ" rows="3" value={equipement.notes ?? ""} onchange={(e) => ecrireTexte("notes", e.currentTarget.value)}></textarea>
      </label>
      <p class="aide large">Aucun mot de passe ni secret ici : seulement la référence où le trouver.</p>
    </div>
  </details>

  <div class="actions">
    <button type="button" class="bouton petit" onclick={() => editeur.agir((s) => { editeur.selection = dupliquerEquipements(s, new Set([equipement.id])); }, "Équipement dupliqué.")}>Dupliquer</button>
    <button type="button" class="bouton petit" onclick={() => modifier((x) => { if (x.verrou) delete x.verrou; else x.verrou = true; })}>{equipement.verrou ? "Déverrouiller" : "Verrouiller"}</button>
    {#if def.hebergeur}
      <button type="button" class="bouton petit" onclick={() => modifier((x) => { if (x.replie) delete x.replie; else x.replie = true; })}>{equipement.replie ? "Déplier" : "Replier"}</button>
    {/if}
    <button type="button" class="bouton petit attention" onclick={() => { editeur.agir((s) => retirerEquipements(s, new Set([equipement.id])), "Équipement retiré, avec ses liens : Ctrl+Z pour annuler."); editeur.selection = []; }}>Retirer</button>
  </div>
</div>

{#if profil}
  <DialogueProfil
    {editeur}
    equipementId={equipement.id}
    modifier={profil.modifier}
    retirees={profil.retirees}
    onfermer={() => (profil = null)}
  />
{/if}

<style>
  .proprietes {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  .titre {
    margin: 0 0 4px;
    font-size: 15px;
  }

  .type {
    color: var(--texte-secondaire);
    font-size: 12px;
    font-weight: 400;
  }

  details {
    border-top: 1px solid var(--bordure);
    padding-top: 4px;
  }

  summary {
    padding: 4px 0;
    font-weight: 600;
    cursor: pointer;
  }

  .compte {
    color: var(--texte-secondaire);
    font-weight: 400;
  }

  .champs {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 6px 8px;
    padding: 4px 0 8px;
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

  textarea.champ {
    height: auto;
    padding: 4px 8px;
    resize: vertical;
  }

  .large {
    grid-column: 1 / -1;
  }

  .case {
    flex-direction: row;
    align-items: center;
    gap: 6px;
  }

  .ligne {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    margin: 4px 0;
  }

  .court {
    width: 80px;
  }

  .aide {
    margin: 2px 0 6px;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .profil {
    border: none;
  }

  .groupe-ports {
    display: grid;
    grid-template-columns: 1.2fr 1.1fr 0.6fr 0.6fr 1fr 0.8fr auto;
    gap: 3px;
    margin-bottom: 3px;
  }

  .groupe-ports .champ {
    padding: 0 4px;
    font-size: 12px;
  }

  /* Place de la flèche des listes déroulantes (theme.css). */
  .groupe-ports select.champ {
    padding-right: 20px;
  }

  .boutons,
  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    margin: 6px 0;
  }

  .membre {
    margin: 6px 0 3px;
    color: var(--texte-secondaire);
    font-size: 12px;
    font-weight: 600;
  }

  .tete-membre {
    display: flex;
    align-items: center;
    gap: 4px;
  }

  .tete-membre .membre {
    flex: 1;
  }

  .tete-membre .bouton.petit {
    height: 24px;
    padding: 0 6px;
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

  .grille-ports {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(52px, 1fr));
    gap: 3px;
    margin-bottom: 6px;
  }

  .port {
    overflow: hidden;
    padding: 3px 4px;
    border: 1px solid var(--bordure);
    border-radius: 4px;
    background: var(--surface);
    color: var(--texte);
    font-size: 11px;
    text-overflow: ellipsis;
    white-space: nowrap;
    cursor: pointer;
  }

  .port.occupe {
    border-color: var(--principale);
    background: var(--pastille-fiche-fond);
    color: var(--pastille-fiche-texte);
  }

  .port.choisi {
    outline: 2px solid var(--focus);
  }

  .interface {
    margin: 6px 0;
    padding: 6px 8px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
  }

  .interface legend {
    padding: 0 4px;
    font-weight: 600;
  }

  .connexions {
    margin: 0 0 6px;
    padding: 0;
    list-style: none;
  }

  .lien-texte {
    padding: 2px 0;
    border: none;
    background: none;
    color: var(--lien);
    font-size: 12px;
    text-align: left;
    cursor: pointer;
  }

  .bouton.petit {
    height: 28px;
    padding: 0 10px;
    font-size: 12px;
  }

  .attention {
    color: var(--retard);
  }
</style>
