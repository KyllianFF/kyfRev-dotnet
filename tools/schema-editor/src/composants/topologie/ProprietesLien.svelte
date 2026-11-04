<script lang="ts">
  import { affichageDe, estCoude } from "../../lib/topologie/dessin";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import {
    type Agregat,
    type Lien,
    liensDeLInterface,
    STATUTS_LIEN,
    SUPPORTS,
  } from "../../lib/topologie/modele";
  import { interfaceIndisponible } from "../../lib/topologie/operations";
  import { interfacesParMembre } from "../../lib/topologie/ports";
  import {
    agregatDe,
    basculerMembre,
    type ChampsVlan,
    champsDe,
    configDepuis,
    configDuBout,
    creerAgregat,
    ecrireConfigDuBout,
    lireListeVlan,
    lireNumeroVlan,
    retirerAgregat,
    texteVlan,
  } from "../../lib/topologie/vlans";
  import { vue } from "../../lib/topologie/vues";
  import SelecteurInterface from "./SelecteurInterface.svelte";

  // Propriétés d'un lien physique (D39) : extrémités (rebrancher), support,
  // technologie, VLAN et agrégat (D51), documentation, mesures (distinctes du
  // documenté), affichage.
  let { editeur, lien }: { editeur: EditeurSchema; lien: Lien } = $props();

  const schema = $derived(editeur.schema);
  const eqA = $derived(schema.equipements.find((e) => e.id === lien.a.equipement));
  const eqB = $derived(schema.equipements.find((e) => e.id === lien.b.equipement));
  let rebrancher = $state<"a" | "b" | null>(null);

  function modifier(action: (l: Lien) => void, annonce?: string) {
    editeur.agir((s) => {
      const l = s.liens.find((x) => x.id === lien.id);
      if (l) action(l);
    }, annonce);
  }

  function texte<
    K extends "nom" | "description" | "technologie" | "categorie" | "etiquette" | "notes" | "supportPerso",
  >(cle: K, valeur: string) {
    modifier((l) => {
      if (valeur.trim()) l[cle] = valeur.trim();
      else delete l[cle];
    });
  }

  function nombre<K extends "debit" | "longueurM">(cle: K, valeur: string) {
    modifier((l) => {
      const n = Number(valeur);
      if (valeur !== "" && Number.isFinite(n) && n >= 0) l[cle] = cle === "debit" ? Math.round(n) : n;
      else delete l[cle];
    });
  }

  function optique(cle: "connecteurA" | "connecteurB" | "moduleA" | "moduleB", valeur: string) {
    modifier((l) => {
      const o = { ...(l.optique ?? {}) };
      if (valeur.trim()) o[cle] = valeur.trim();
      else delete o[cle];
      if (Object.keys(o).length) l.optique = o;
      else delete l.optique;
    });
  }

  function mesure(cle: "debit" | "date" | "source", valeur: string) {
    modifier((l) => {
      const m = { ...(l.mesure ?? {}) };
      if (cle === "debit") {
        const n = Number(valeur);
        if (valeur !== "" && Number.isFinite(n) && n >= 0) m.debit = Math.round(n);
        else delete m.debit;
      } else if (valeur.trim()) m[cle] = valeur.trim();
      else delete m[cle];
      if (Object.keys(m).length) l.mesure = m;
      else delete l.mesure;
    });
  }

  function brancher(cote: "a" | "b", interfaceId: string) {
    const equipement = cote === "a" ? lien.a.equipement : lien.b.equipement;
    const raison = interfaceIndisponible(schema, { equipement, interface: interfaceId }, lien.id);
    if (raison) {
      editeur.annonce = `Impossible : ${raison}.`;
      return;
    }
    modifier((l) => {
      l[cote] = { equipement, interface: interfaceId };
    }, "Lien rebranché.");
    rebrancher = null;
  }

  const nomPort = (cote: "a" | "b") => {
    const e = cote === "a" ? eqA : eqB;
    return e?.interfaces.find((i) => i.id === lien[cote].interface)?.nom ?? "?";
  };
  const fibre = $derived(["fibre-monomode", "fibre-multimode", "aoc"].includes(lien.support));

  // VLAN (D51) : saisis une fois pour le câble, écrits aux deux bouts (à leurs agrégats s'ils en ont).
  const configA = $derived(configDuBout(schema, lien.a));
  const configB = $derived(configDuBout(schema, lien.b));
  const champs = $derived(champsDe(configA ?? configB));
  const differents = $derived.by(() => {
    const [a, b] = [champsDe(configA), champsDe(configB)];
    return a.nonTague !== b.nonTague || (a.tagues ?? "") !== (b.tagues ?? "");
  });

  function ecrireVlan(cle: keyof ChampsVlan, champ: HTMLInputElement) {
    const valeur = cle === "nonTague" ? lireNumeroVlan(champ.value) : lireListeVlan(champ.value);
    if (valeur === null) {
      editeur.annonce =
        cle === "nonTague"
          ? "VLAN non tagué : un numéro de 1 à 4094."
          : "VLAN tagués : des numéros de 1 à 4094, séparés par des virgules (« 10,20,30-40 »).";
      champ.value = String(champs[cle] ?? "");
      return;
    }
    const nouveaux: ChampsVlan = { ...champs };
    if (valeur === undefined) delete nouveaux[cle];
    else (nouveaux as Record<string, unknown>)[cle] = valeur;
    const id = lien.id;
    editeur.agir((s) => {
      const l = s.liens.find((x) => x.id === id);
      if (!l) return;
      for (const bout of [l.a, l.b])
        ecrireConfigDuBout(s, bout, configDepuis(nouveaux, configDuBout(s, bout)));
    });
    if (typeof valeur === "string") champ.value = valeur;
    // VLAN masqués par la vue (la vue physique ne les montre pas d'office) : dire où les afficher.
    if (valeur !== undefined && !affichageDe(schema, vue(schema.vue)).vlans)
      editeur.annonce =
        "VLAN enregistrés. Pour les voir sur le schéma : menu Affichage (œil), « VLAN des câbles ».";
  }

  const texteDe = (config: Parameters<typeof texteVlan>[1]) =>
    texteVlan(schema, config)?.texte ?? "aucun VLAN";

  // Agrégat de liens (D51) : un agrégat de chaque côté, ses ports choisis ici.
  const agregatA = $derived(agregatDe(schema, lien.a));
  const agregatB = $derived(agregatDe(schema, lien.b));
  const agrege = $derived(agregatA !== undefined || agregatB !== undefined);

  function modifierAgregats(action: (a: Agregat) => void) {
    const ids = new Set([agregatA?.id, agregatB?.id].filter(Boolean));
    editeur.agir((s) => {
      for (const a of s.agregats) if (ids.has(a.id)) action(a);
    });
  }

  /** État d'un port pour l'agrégat du côté `cote` : membre, possible, ou pourquoi pas. */
  function etatPort(
    cote: "a" | "b",
    interfaceId: string,
  ): { membre: boolean; raison: string | null; titre: string } {
    const bout = lien[cote];
    const enFace = cote === "a" ? lien.b : lien.a;
    const agregat = cote === "a" ? agregatA : agregatB;
    const membre = agregat?.membres.includes(interfaceId) ?? false;
    if (interfaceId === bout.interface)
      return { membre: true, raison: "port du câble", titre: "Port du câble : il reste membre" };
    const autre = schema.agregats.find(
      (a) => a.id !== agregat?.id && a.equipement === bout.equipement && a.membres.includes(interfaceId),
    );
    if (autre)
      return {
        membre: false,
        raison: `membre de « ${autre.nom} »`,
        titre: `Déjà membre de l'agrégat « ${autre.nom} »`,
      };
    const vers = liensDeLInterface(schema, bout.equipement, interfaceId)
      .filter((l) => l.id !== lien.id)
      .map((l) => (l.a.equipement === bout.equipement && l.a.interface === interfaceId ? l.b : l.a));
    const ailleurs = vers.find((v) => v.equipement !== enFace.equipement);
    if (ailleurs) {
      const nom = schema.equipements.find((e) => e.id === ailleurs.equipement)?.nom ?? "?";
      return { membre, raison: `câblé vers ${nom}`, titre: `Câblé vers ${nom}` };
    }
    if (vers.length > 0)
      return { membre, raison: null, titre: "Déjà câblé vers l'autre bout : ce câble rejoindra l'agrégat" };
    return {
      membre,
      raison: null,
      titre: membre ? "Membre : cliquer pour le retirer" : "Cliquer pour l'ajouter à l'agrégat",
    };
  }
</script>

<div class="proprietes">
  <h3 class="titre">Lien {lien.nom ? `« ${lien.nom} »` : ""}</h3>

  <details open>
    <summary>Extrémités</summary>
    {#each ["a", "b"] as const as cote (cote)}
      {@const e = cote === "a" ? eqA : eqB}
      <p class="extremite">
        <strong>{e?.nom ?? "?"}</strong> · {nomPort(cote)}
        <button type="button" class="bouton discret petit" onclick={() => (rebrancher = rebrancher === cote ? null : cote)}>Rebrancher…</button>
      </p>
      {#if rebrancher === cote && e}
        <SelecteurInterface {schema} equipement={e} choisie={lien[cote].interface} sauf={lien.id} etiquette={`Interface de ${e.nom}`} onchoisir={(i) => brancher(cote, i)} />
      {/if}
    {/each}
  </details>

  <details open>
    <summary>Support et technologie</summary>
    <div class="champs">
      <label class="large">
        <span>Support physique</span>
        <select class="champ" value={lien.support} onchange={(e) => modifier((l) => (l.support = e.currentTarget.value as Lien["support"]))}>
          {#each SUPPORTS as s (s.valeur)}<option value={s.valeur}>{s.nom}</option>{/each}
        </select>
      </label>
      {#if lien.support === "personnalise"}
        <label class="large">
          <span>Support personnalisé</span>
          <input class="champ" value={lien.supportPerso ?? ""} onchange={(e) => texte("supportPerso", e.currentTarget.value)} />
        </label>
      {/if}
      {#if lien.support === "cuivre"}
        <label>
          <span>Catégorie du câble</span>
          <input class="champ" value={lien.categorie ?? ""} placeholder="Cat6a" onchange={(e) => texte("categorie", e.currentTarget.value)} />
        </label>
      {/if}
      <label>
        <span>Technologie</span>
        <input class="champ" value={lien.technologie ?? ""} placeholder="1000BASE-T" onchange={(e) => texte("technologie", e.currentTarget.value)} />
      </label>
      <label>
        <span>Débit nominal (Mbit/s)</span>
        <input class="champ" type="number" min="0" value={lien.debit ?? ""} onchange={(e) => nombre("debit", e.currentTarget.value)} />
      </label>
      <label>
        <span>Longueur (m)</span>
        <input class="champ" type="number" min="0" step="0.1" value={lien.longueurM ?? ""} onchange={(e) => nombre("longueurM", e.currentTarget.value)} />
      </label>
      <label>
        <span>Étiquette du câble</span>
        <input class="champ" value={lien.etiquette ?? ""} onchange={(e) => texte("etiquette", e.currentTarget.value)} />
      </label>
      <label>
        <span>Statut</span>
        <select class="champ" value={lien.statut} onchange={(e) => modifier((l) => (l.statut = e.currentTarget.value as Lien["statut"]))}>
          {#each STATUTS_LIEN as s (s.valeur)}<option value={s.valeur}>{s.nom}</option>{/each}
        </select>
      </label>
    </div>
  </details>

  <details open>
    <summary>VLAN</summary>
    <p class="aide">Écrits aux deux bouts du câble{agrege ? " (dans leurs agrégats)" : ""}. Tagués : « 10,20,30-40 ».</p>
    <div class="champs">
      <label>
        <span>VLAN non tagué (untagged, natif)</span>
        <input class="champ" inputmode="numeric" list="vlans-du-schema" value={champs.nonTague ?? ""} placeholder="Aucun" onchange={(e) => ecrireVlan("nonTague", e.currentTarget)} />
      </label>
      <label>
        <span>VLAN tagués (tagged)</span>
        <input class="champ" value={champs.tagues ?? ""} placeholder="10,20,30-40" onchange={(e) => ecrireVlan("tagues", e.currentTarget)} />
      </label>
    </div>
    <datalist id="vlans-du-schema">
      {#each schema.vlans as v (v.id)}<option value={v.numero}>{v.nom}</option>{/each}
    </datalist>
    {#if differents}
      <p class="aide attention">
        Les deux bouts diffèrent : {texteDe(configA)} côté {eqA?.nom}, {texteDe(configB)} côté {eqB?.nom}. Une saisie ici les aligne.
      </p>
    {/if}
  </details>

  <details open={agrege}>
    <summary>Agrégat de liens (trunk){agrege ? "" : " : non"}</summary>
    {#if !agrege}
      <p class="aide">
        Plusieurs ports agrégés entre ces deux équipements (trunk HPE/Aruba, port-channel, LACP) : ce câble les représente
        tous, en un seul trait.
      </p>
      <button type="button" class="bouton petit" onclick={() => editeur.agir((s) => creerAgregat(s, lien.id), "Agrégat créé : choisissez ses ports de chaque côté.")}>
        Faire de ce câble un agrégat
      </button>
    {:else}
      <label class="ligne">
        <span>Mode</span>
        <select class="champ" value={(agregatA ?? agregatB)?.mode ?? "lacp"} onchange={(e) => { const mode = e.currentTarget.value as Agregat["mode"]; modifierAgregats((a) => (a.mode = mode)); }}>
          <option value="lacp">LACP</option>
          <option value="statique">Statique</option>
        </select>
      </label>
      {#each ["a", "b"] as const as cote (cote)}
        {@const e = cote === "a" ? eqA : eqB}
        {@const agregat = cote === "a" ? agregatA : agregatB}
        {#if e && agregat}
          <fieldset class="agregat">
            <legend>{e.nom}</legend>
            <label class="ligne">
              <span>Nom</span>
              <input
                class="champ"
                value={agregat.nom}
                onchange={(ev) => {
                  const nom = ev.currentTarget.value.trim();
                  const id = agregat.id;
                  if (nom) editeur.agir((s) => { const a = s.agregats.find((x) => x.id === id); if (a) a.nom = nom; });
                  else ev.currentTarget.value = agregat.nom;
                }}
              />
            </label>
            {#each interfacesParMembre(e) as g (g.membre ?? 0)}
              {@const physiques = g.interfaces.filter((i) => i.nature === "physique")}
              {#if physiques.length}
                {#if g.membre !== null}<p class="membre">Membre {g.membre}</p>{/if}
                <div class="grille-ports" role="group" aria-label={`Ports de l'agrégat ${agregat.nom}`}>
                  {#each physiques as i (i.id)}
                    {@const etat = etatPort(cote, i.id)}
                    <button
                      type="button"
                      class="port"
                      class:membre={etat.membre}
                      aria-pressed={etat.membre}
                      disabled={etat.raison !== null}
                      title={`${i.nom} : ${etat.titre}`}
                      onclick={() => editeur.agir((s) => basculerMembre(s, lien.id, cote, i.id))}
                    >
                      {i.nom}
                    </button>
                  {/each}
                </div>
              {/if}
            {/each}
          </fieldset>
        {/if}
      {/each}
      <button type="button" class="bouton petit" onclick={() => editeur.agir((s) => retirerAgregat(s, lien.id), "Ce câble n'est plus un agrégat.")}>
        Ce n'est plus un agrégat
      </button>
    {/if}
  </details>

  {#if fibre}
    <details>
      <summary>Optique</summary>
      <div class="champs">
        <label><span>Connecteur côté {eqA?.nom}</span><input class="champ" value={lien.optique?.connecteurA ?? ""} placeholder="LC/UPC" onchange={(e) => optique("connecteurA", e.currentTarget.value)} /></label>
        <label><span>Connecteur côté {eqB?.nom}</span><input class="champ" value={lien.optique?.connecteurB ?? ""} onchange={(e) => optique("connecteurB", e.currentTarget.value)} /></label>
        <label><span>Module côté {eqA?.nom}</span><input class="champ" value={lien.optique?.moduleA ?? ""} placeholder="SFP+ SR" onchange={(e) => optique("moduleA", e.currentTarget.value)} /></label>
        <label><span>Module côté {eqB?.nom}</span><input class="champ" value={lien.optique?.moduleB ?? ""} onchange={(e) => optique("moduleB", e.currentTarget.value)} /></label>
      </div>
    </details>
  {/if}

  <details>
    <summary>Mesures relevées</summary>
    <p class="aide">Valeurs mesurées (et non documentées) : kyfRev ne supervise rien, il garde ce que vous avez relevé.</p>
    <div class="champs">
      <label><span>Débit mesuré (Mbit/s)</span><input class="champ" type="number" min="0" value={lien.mesure?.debit ?? ""} onchange={(e) => mesure("debit", e.currentTarget.value)} /></label>
      <label><span>Date</span><input class="champ" type="date" value={lien.mesure?.date ?? ""} onchange={(e) => mesure("date", e.currentTarget.value)} /></label>
      <label class="large"><span>Source</span><input class="champ" value={lien.mesure?.source ?? ""} placeholder="iperf, relevé du switch…" onchange={(e) => mesure("source", e.currentTarget.value)} /></label>
    </div>
  </details>

  <details>
    <summary>Affichage et notes</summary>
    <div class="champs">
      <label>
        <span>Nom</span>
        <input class="champ" value={lien.nom ?? ""} onchange={(e) => texte("nom", e.currentTarget.value)} />
      </label>
      <label>
        <span>Libellés</span>
        <select class="champ" value={lien.libelles ?? ""} onchange={(e) => modifier((l) => { if (e.currentTarget.value) l.libelles = e.currentTarget.value as Lien["libelles"]; else delete l.libelles; })}>
          <option value="">Selon la vue</option>
          <option value="aucun">Aucun</option>
          <option value="ports">Ports</option>
          <option value="nom">Nom du lien</option>
          <option value="tout">Ports et nom</option>
        </select>
      </label>
      <label>
        <span>Tracé</span>
        <select class="champ" value={lien.forme ?? ""} onchange={(e) => modifier((l) => { if (e.currentTarget.value) l.forme = e.currentTarget.value as Lien["forme"]; else delete l.forme; })}>
          <option value="">Selon le schéma</option>
          <option value="droite">Droit</option>
          <option value="coudee">Coudé à angle droit</option>
        </select>
      </label>
      <label>
        <span>Trait</span>
        <select class="champ" value={lien.trait ?? ""} onchange={(e) => modifier((l) => { if (e.currentTarget.value) l.trait = e.currentTarget.value as Lien["trait"]; else delete l.trait; })}>
          <option value="">Selon le support</option>
          <option value="plein">Plein</option>
          <option value="tirets">Tirets</option>
          <option value="points">Points</option>
        </select>
      </label>
      <label>
        <span>Couleur</span>
        <input class="champ" type="color" value={lien.couleur ?? "#5d566d"} onchange={(e) => modifier((l) => (l.couleur = e.currentTarget.value))} />
      </label>
      <label class="large">
        <span>Description</span>
        <input class="champ" value={lien.description ?? ""} onchange={(e) => texte("description", e.currentTarget.value)} />
      </label>
      <label class="large">
        <span>Notes</span>
        <textarea class="champ" rows="2" value={lien.notes ?? ""} onchange={(e) => texte("notes", e.currentTarget.value)}></textarea>
      </label>
    </div>
    <p class="aide">
      Tirer le câble le fait coulisser (le palier d'un câble coudé, ou un point de passage). Double-clic sur le tracé :
      point de passage ; double-clic sur un point : il disparaît.
    </p>
  </details>

  <div class="actions">
    {#if lien.points.length || lien.palier}
      <button
        type="button"
        class="bouton petit"
        title="Oublie les points de passage et le palier coulissé"
        onclick={() =>
          modifier((l) => {
            l.points = [];
            delete l.palier;
          })}>{estCoude(schema, lien) ? "Tracé automatique" : "Tracé droit"}</button
      >
    {/if}
    {#if lien.couleur}
      <button type="button" class="bouton petit" onclick={() => modifier((l) => delete l.couleur)}>Couleur du support</button>
    {/if}
    <button
      type="button"
      class="bouton petit attention"
      onclick={() => {
        editeur.agir((s) => (s.liens = s.liens.filter((l) => l.id !== lien.id)), "Lien retiré : Ctrl+Z pour annuler.");
        editeur.selection = [];
      }}>Retirer le lien</button
    >
  </div>
</div>

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

  details {
    border-top: 1px solid var(--bordure);
    padding-top: 4px;
  }

  summary {
    padding: 4px 0;
    font-weight: 600;
    cursor: pointer;
  }

  .extremite {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 6px;
    margin: 4px 0;
    font-size: 13px;
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
  }

  .large {
    grid-column: 1 / -1;
  }

  .aide {
    margin: 2px 0 6px;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    margin-top: 6px;
  }

  .bouton.petit {
    height: 28px;
    padding: 0 10px;
    font-size: 12px;
  }

  .attention {
    color: var(--retard);
  }

  .ligne {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    margin: 4px 0;
  }

  .ligne .champ {
    width: 60%;
  }

  .agregat {
    margin: 6px 0;
    padding: 4px 8px 6px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
  }

  .agregat legend {
    padding: 0 4px;
    font-size: 13px;
    font-weight: 600;
  }

  .membre {
    margin: 4px 0 2px;
    color: var(--texte-secondaire);
    font-size: 12px;
    font-weight: 600;
  }

  .grille-ports {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(52px, 1fr));
    gap: 3px;
    margin-bottom: 4px;
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

  .port.membre {
    border-color: var(--principale);
    background: var(--pastille-fiche-fond);
    color: var(--pastille-fiche-texte);
    font-weight: 600;
  }

  .port:disabled {
    cursor: default;
    opacity: 0.55;
  }

  .port.membre:disabled {
    opacity: 1;
  }
</style>
