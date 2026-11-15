<script lang="ts">
  import Minus from "@lucide/svelte/icons/minus";
  import Plus from "@lucide/svelte/icons/plus";
  import Trash2 from "@lucide/svelte/icons/trash-2";
  import { tick } from "svelte";
  import { traitDuSupport } from "../../lib/topologie/dessin";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import { SUPPORTS, type Support } from "../../lib/topologie/modele";
  import {
    AIDE_AGREGAT_MINIMUM,
    agreger,
    alignerVlans,
    aDesReglages,
    CABLES_MAX,
    changerTypeDeCable,
    conseilsDeLiaison,
    desagreger,
    ecrireVlanDAcces,
    ecrireVlansDuTrunk,
    type GroupeDeCables,
    lireVlansDuTrunk,
    MEMBRES_MIN,
    mettreEnTrunk,
    reglerMembres,
    tirerCable,
  } from "../../lib/topologie/simplifie";
  import { lireNumeroVlan } from "../../lib/topologie/vlans";

  // Bulle des câbles choisis en mode simplifié (11A) : un câble, ou plusieurs choisis avec Ctrl+clic, qui
  // se règlent alors ensemble. Leur type, et deux options qui ne se confondent pas. « Agrégat de liens »
  // réunit des câbles choisis entre deux mêmes équipements en un seul lien (le dessin les cercle) ;
  // « Trunk » leur fait transporter plusieurs VLAN (le dessin l'annote). Sans trunk, les ports sont en
  // accès : un seul VLAN, non tagué, annoté lui aussi. Chaque réglage est le vrai réglage du modèle.
  let {
    editeur,
    groupe,
    onretirer,
    onavance,
  }: {
    editeur: EditeurSchema;
    groupe: GroupeDeCables;
    onretirer: () => void;
    onavance: () => void;
  } = $props();

  /** Les types les plus posés, sous un nom court ; les autres supports du modèle suivent, sous le leur. */
  const COURANTS: { valeur: Support; nom: string }[] = [
    { valeur: "cuivre", nom: "Cuivre" },
    { valeur: "fibre-multimode", nom: "Fibre" },
    { valeur: "console", nom: "Série" },
    { valeur: "radio", nom: "Sans fil" },
  ];
  const AUTRES = SUPPORTS.filter(
    (s) => s.valeur !== "personnalise" && !COURANTS.some((c) => c.valeur === s.valeur),
  ).map((s) => ({ valeur: s.valeur, nom: traitDuSupport(s.valeur).nom }));
  const nomEntier = (support: Support) => SUPPORTS.find((s) => s.valeur === support)?.nom ?? support;

  let autresOuverts = $state(false);
  const types = $derived(
    autresOuverts || AUTRES.some((t) => t.valeur === groupe.support) ? [...COURANTS, ...AUTRES] : COURANTS,
  );
  const ids = $derived(groupe.cables.map((l) => l.id));
  const plusieurs = $derived(groupe.cables.length > 1);
  /** Ce que disent les annonces : le câble, les câbles d'une même liaison, ou les câbles choisis. */
  const sujet = $derived(
    !groupe.memePaire
      ? `Les ${groupe.cables.length} câbles choisis`
      : plusieurs
        ? `Les ${groupe.cables.length} câbles choisis entre ${groupe.a.nom} et ${groupe.b.nom}`
        : `Câble entre ${groupe.a.nom} et ${groupe.b.nom}`,
  );
  const conseils = $derived(conseilsDeLiaison(editeur.schema, groupe));

  function changerType(support: Support) {
    if (support === groupe.support) return;
    const cables = ids;
    editeur.agir(
      (s) => {
        for (const id of cables) changerTypeDeCable(s, id, support);
      },
      `${sujet} : ${nomEntier(support).toLowerCase()}.`,
    );
  }

  // --- Un câble de plus, à part ---

  /** Un câble seul choisi : un autre câble entre les deux mêmes équipements, du même type, à régler à part. */
  function ajouterACote() {
    const [a, b, support] = [groupe.a, groupe.b, groupe.cables[0]?.support];
    let nouveau = "";
    const fait = editeur.agir((s) => {
      nouveau = tirerCable(s, a.id, b.id, support ? { support } : {}).id;
    });
    if (!fait) return;
    editeur.selection = [nouveau];
    editeur.annonce = `Un câble de plus entre ${a.nom} et ${b.nom}, choisi : il se règle à part.`;
  }

  // --- L'agrégat choisi seul : ses câbles, qui sont ses ports ---

  /** Le « − » s'arrête : pourquoi (dit sous le compteur, une fois qu'on l'a demandé). */
  let aideMoins = $state<string | null>(null);

  function regler(voulu: number) {
    aideMoins = null;
    const agregat = groupe.agregat;
    if (!agregat) return;
    if (voulu < MEMBRES_MIN) {
      aideMoins = AIDE_AGREGAT_MINIMUM;
      return;
    }
    if (editeur.agir((s) => reglerMembres(s, agregat.lien.id, voulu))) {
      editeur.annonce = `Agrégat entre ${groupe.a.nom} et ${groupe.b.nom} : ${voulu} câbles.`;
    }
  }

  // --- Agrégat de liens ---

  /** Coché : le câble choisi est un agrégat, choisi seul (le décocher en refait des câbles à part). */
  const agrege = $derived(groupe.agregat !== null && !plusieurs);

  function basculerAgregat() {
    aideMoins = null;
    if (groupe.agregatImpossible) {
      editeur.annonce = groupe.agregatImpossible;
      return;
    }
    const entre = `${groupe.a.nom} et ${groupe.b.nom}`;
    if (agrege && groupe.agregat) {
      const lien = groupe.agregat.lien.id;
      let cables: string[] = [];
      if (!editeur.agir((s) => (cables = desagreger(s, lien).map((l) => l.id)))) return;
      editeur.selection = cables;
      editeur.annonce = `Plus d'agrégat entre ${entre} : ${cables.length} câble(s) à part, chacun sur son port, tous choisis.`;
      return;
    }
    // Un seul câble reste au modèle : les réglages propres aux autres (nom, étiquette…) ne suivent pas.
    const regles = groupe.ordinaires.filter((l) => aDesReglages(editeur.schema, l)).length;
    const choisis = ids;
    let garde = "";
    if (!editeur.agir((s) => (garde = agreger(s, choisis).id))) return;
    editeur.selection = [garde];
    editeur.annonce =
      `Agrégat de liens entre ${entre} : ${groupe.nombre} câbles sous un cercle.` +
      (regles > 1 ? " Les réglages propres aux autres câbles (nom, étiquette…) ne sont pas gardés : Ctrl+Z annule." : "");
  }

  // --- Trunk et ses VLAN ---

  let erreur = $state<{ texte: string; champ: "non-tague" | "tagues" | "acces" } | null>(null);
  let champNonTague = $state<HTMLInputElement>();
  let champTagues = $state<HTMLInputElement>();
  let champAcces = $state<HTMLInputElement>();

  /** Le VLAN d'un câble en accès : un seul numéro, non tagué. Vide, il se retire. */
  function ecrireVlanAcces() {
    const lu = lireNumeroVlan(champAcces?.value ?? "");
    if (lu === null) {
      erreur = { texte: "Le VLAN d'un port d'accès est un seul numéro, de 1 à 4094.", champ: "acces" };
      void tick().then(() => document.getElementById("bulle-vlans-erreur")?.scrollIntoView({ block: "nearest" }));
      return;
    }
    erreur = null;
    if (lu === groupe.vlans.nonTague && !groupe.cablesDifferents) return;
    const cables = ids;
    editeur.agir(
      (s) => ecrireVlanDAcces(s, cables, lu),
      lu !== undefined ? `${sujet} : VLAN ${lu} en accès, non tagué.` : `${sujet} : plus de VLAN.`,
    );
  }

  function basculerTrunk() {
    const actif = !groupe.trunk;
    const cables = ids;
    erreur = null;
    editeur.agir(
      (s) => mettreEnTrunk(s, cables, actif),
      actif
        ? `${sujet} : trunk. Ses VLAN non tagué et tagués se règlent juste dessous.`
        : `${sujet} : plus de trunk, les ports reviennent en accès.`,
    );
  }

  /** Les deux champs se lisent ensemble : rien n'est écrit tant que l'un des deux est faux. */
  function ecrireVlans() {
    const lus = lireVlansDuTrunk(champNonTague?.value ?? "", champTagues?.value ?? "");
    if ("erreur" in lus) {
      erreur = { texte: lus.erreur, champ: lus.champ };
      // Le message vient sous les champs : il se montre, même dans une bulle qui défile.
      void tick().then(() => document.getElementById("bulle-vlans-erreur")?.scrollIntoView({ block: "nearest" }));
      return;
    }
    erreur = null;
    const { nonTague, tagues } = lus.champs;
    if (nonTague === groupe.vlans.nonTague && (tagues ?? "") === (groupe.vlans.tagues ?? "") && !groupe.cablesDifferents) {
      return;
    }
    const cables = ids;
    const texte = [nonTague !== undefined ? `non tagué ${nonTague}` : "", tagues ? `tagués ${tagues}` : ""]
      .filter(Boolean)
      .join(", ");
    editeur.agir(
      (s) => ecrireVlansDuTrunk(s, cables, lus.champs),
      texte ? `${sujet} : trunk, VLAN ${texte}.` : `${sujet} : trunk sans VLAN.`,
    );
  }

  /** Pendant la saisie, une erreur corrigée s'efface tout de suite. */
  function relire() {
    if (!erreur) return;
    const corrige =
      erreur.champ === "acces"
        ? lireNumeroVlan(champAcces?.value ?? "") !== null
        : !("erreur" in lireVlansDuTrunk(champNonTague?.value ?? "", champTagues?.value ?? ""));
    if (corrige) erreur = null;
  }

  function aligner(equipement: string, nom: string) {
    const cables = ids;
    editeur.agir((s) => alignerVlans(s, cables, equipement), `Les deux bouts ont maintenant les VLAN de ${nom}.`);
  }

  /** Entrée valide le champ (il s'applique en le quittant puis en y revenant), sans fermer la bulle. */
  function entree(evenement: KeyboardEvent) {
    if (evenement.key !== "Enter") return;
    evenement.preventDefault();
    const champ = evenement.currentTarget as HTMLInputElement;
    champ.blur();
    champ.focus();
  }

  /** Flèches dans les types : comme des boutons radio, le type atteint est choisi. */
  function clavierDesTypes(evenement: KeyboardEvent) {
    const sens =
      evenement.key === "ArrowRight" || evenement.key === "ArrowDown"
        ? 1
        : evenement.key === "ArrowLeft" || evenement.key === "ArrowUp"
          ? -1
          : 0;
    if (sens === 0) return;
    evenement.preventDefault();
    const boutons = [...(evenement.currentTarget as HTMLElement).querySelectorAll<HTMLElement>("[role=radio]")];
    const rang = boutons.indexOf(document.activeElement as HTMLElement);
    const cible = boutons[(rang + sens + boutons.length) % boutons.length];
    cible?.focus();
    cible?.click();
  }
</script>

<h2 id="titre-bulle">{groupe.memePaire ? `${groupe.a.nom} ↔ ${groupe.b.nom}` : `${groupe.cables.length} câbles choisis`}</h2>
<p class="aide" id="bulle-choix">
  {#if plusieurs}
    {groupe.memePaire ? `${groupe.cables.length} câbles choisis : ` : ""}ce que vous réglez ici vaut pour tous.
  {:else}
    Glissez le câble pour changer son tracé ; Ctrl+clic sur d'autres câbles pour les régler ensemble.
  {/if}
</p>
<div>
  <span class="libelle" id="bulle-type">Type de câble</span>
  <!-- svelte-ignore a11y_interactive_supports_focus -->
  <div class="types" role="radiogroup" aria-labelledby="bulle-type" onkeydown={clavierDesTypes}>
    {#each types as t, i (t.valeur)}
      {@const trait = traitDuSupport(t.valeur)}
      {@const choisi = groupe.support === t.valeur}
      <button
        type="button"
        class="type"
        class:autre={i >= COURANTS.length}
        role="radio"
        aria-checked={choisi}
        aria-label={nomEntier(t.valeur)}
        title={nomEntier(t.valeur)}
        data-support={t.valeur}
        tabindex={choisi || (groupe.support === null && i === 0) ? 0 : -1}
        onclick={() => changerType(t.valeur)}
      >
        <svg width="46" height="8" aria-hidden="true">
          <line x1="2" y1="4" x2="44" y2="4" stroke={trait.couleur} stroke-width={trait.epaisseur} stroke-dasharray={trait.tirets} stroke-linecap="round" />
        </svg>
        {t.nom}
      </button>
    {/each}
    {#if types.length === COURANTS.length}
      <button type="button" class="type plus" aria-expanded="false" onclick={() => (autresOuverts = true)}>
        Autres types…
      </button>
    {/if}
  </div>
</div>

{#if agrege && groupe.agregat}
  <div class="option">
    <span class="nom-option" id="bulle-nombre">Nombre de câbles</span>
    <div class="pas" role="group" aria-labelledby="bulle-nombre">
      <button
        type="button"
        aria-label="Un câble de moins"
        aria-disabled={groupe.agregat.ports <= MEMBRES_MIN}
        aria-describedby={aideMoins ? "bulle-nombre-aide" : undefined}
        disabled={groupe.horsDePortee !== null}
        onclick={() => regler((groupe.agregat?.ports ?? MEMBRES_MIN) - 1)}
      >
        <Minus size={16} aria-hidden="true" />
      </button>
      <output aria-live="polite">{groupe.agregat.ports}</output>
      <button
        type="button"
        aria-label="Un câble de plus"
        disabled={groupe.horsDePortee !== null || groupe.agregat.ports >= CABLES_MAX}
        onclick={() => regler((groupe.agregat?.ports ?? MEMBRES_MIN) + 1)}
      >
        <Plus size={16} aria-hidden="true" />
      </button>
    </div>
  </div>
  {#if aideMoins}
    <p class="aide" id="bulle-nombre-aide" role="status">{aideMoins}</p>
  {/if}
{/if}

<div class="option">
  <span class="nom-option" id="bulle-agregat">Agrégat de liens</span>
  <button
    type="button"
    class="interrupteur"
    role="switch"
    data-option="agregat"
    aria-checked={agrege}
    aria-labelledby="bulle-agregat"
    aria-describedby={groupe.agregatImpossible ? "bulle-agregat-aide" : undefined}
    aria-disabled={groupe.agregatImpossible !== null}
    onclick={basculerAgregat}
  ></button>
</div>
{#if groupe.agregatImpossible}
  <p class="aide" id="bulle-agregat-aide">{groupe.agregatImpossible}</p>
{/if}

<div class="option">
  <span class="nom-option" id="bulle-trunk">Trunk</span>
  <button
    type="button"
    class="interrupteur"
    role="switch"
    data-option="trunk"
    aria-checked={groupe.trunk}
    aria-labelledby="bulle-trunk"
    aria-controls={groupe.trunk ? "bulle-vlans" : undefined}
    onclick={basculerTrunk}
  ></button>
</div>
{#if groupe.trunk}
  <div class="vlans" id="bulle-vlans">
    <div>
      <label for="bulle-vlan-non-tague">VLAN non tagué (untagged)</label>
      <input
        bind:this={champNonTague}
        id="bulle-vlan-non-tague"
        class="champ code"
        value={groupe.cablesDifferents ? "" : (groupe.vlans.nonTague ?? "")}
        inputmode="numeric"
        autocomplete="off"
        placeholder={groupe.cablesDifferents ? "différents" : "aucun"}
        aria-invalid={erreur?.champ === "non-tague"}
        aria-describedby={erreur ? "bulle-vlans-erreur" : undefined}
        onchange={ecrireVlans}
        oninput={relire}
        onkeydown={entree}
      />
    </div>
    <div>
      <label for="bulle-vlans-tagues">VLAN tagués (tagged)</label>
      <input
        bind:this={champTagues}
        id="bulle-vlans-tagues"
        class="champ code"
        value={groupe.cablesDifferents ? "" : (groupe.vlans.tagues ?? "")}
        autocomplete="off"
        spellcheck="false"
        placeholder={groupe.cablesDifferents ? "différents" : "10,20,30-40"}
        aria-invalid={erreur?.champ === "tagues"}
        aria-describedby={erreur ? "bulle-vlans-erreur" : undefined}
        onchange={ecrireVlans}
        oninput={relire}
        onkeydown={entree}
      />
    </div>
  </div>
  {#if erreur}
    <p id="bulle-vlans-erreur" class="erreur" role="alert">{erreur.texte}</p>
  {/if}
{:else}
  <!-- Sans trunk, les ports sont en accès : un seul VLAN, qui passe sans étiquette. -->
  <div class="vlans" id="bulle-vlans">
    <div>
      <label for="bulle-vlan-acces">VLAN non tagué (untagged)</label>
      <input
        bind:this={champAcces}
        id="bulle-vlan-acces"
        class="champ code"
        value={groupe.cablesDifferents ? "" : (groupe.vlans.nonTague ?? "")}
        inputmode="numeric"
        autocomplete="off"
        placeholder={groupe.cablesDifferents ? "différents" : "aucun"}
        aria-invalid={erreur?.champ === "acces"}
        aria-describedby={erreur ? "bulle-vlans-erreur bulle-acces-aide" : "bulle-acces-aide"}
        onchange={ecrireVlanAcces}
        oninput={relire}
        onkeydown={entree}
      />
    </div>
    <p class="aide" id="bulle-acces-aide">Port d'accès : un seul VLAN. Pour en faire passer plusieurs, cochez « Trunk ».</p>
  </div>
  {#if erreur}
    <p id="bulle-vlans-erreur" class="erreur" role="alert">{erreur.texte}</p>
  {/if}
{/if}

{#if groupe.boutsDifferents}
  <div class="ecart" role="group" aria-labelledby="bulle-ecart">
    <p class="aide" id="bulle-ecart">
      Les deux bouts n'ont pas les mêmes VLAN : {groupe.a.nom} : {groupe.boutsDifferents.a} ; {groupe.b.nom} : {groupe.boutsDifferents.b}.
    </p>
    <div class="actions">
      <button type="button" class="bouton" data-aligner="a" onclick={() => aligner(groupe.a.id, groupe.a.nom)}>Aligner sur {groupe.a.nom}</button>
      <button type="button" class="bouton" data-aligner="b" onclick={() => aligner(groupe.b.id, groupe.b.nom)}>Aligner sur {groupe.b.nom}</button>
    </div>
  </div>
{:else if groupe.cablesDifferents}
  <p class="aide">Ces câbles n'ont pas tous les mêmes VLAN : ce que vous saisissez ici vaudra pour tous.</p>
{/if}

{#each conseils as conseil (conseil)}
  <p class="conseil" role="note">{conseil}</p>
{/each}

<div class="actions">
  {#if !plusieurs}
    <button type="button" class="bouton discret" data-action="a-cote" onclick={ajouterACote}>
      <Plus size={16} aria-hidden="true" /> Ajouter un câble à côté
    </button>
  {/if}
  <button type="button" class="bouton discret danger" title="Supprimer (Suppr) ; Ctrl+Z annule" onclick={onretirer}>
    <Trash2 size={16} aria-hidden="true" /> Supprimer {groupe.nombre > 1 ? "les câbles" : "le câble"}
  </button>
</div>
{#if groupe.horsDePortee}
  <button type="button" class="vers-avance" onclick={onavance}>Passer en mode avancé</button>
{/if}

<style>
  /* Les quatre types courants sur une ligne ; les autres, au nom plus long, deux par ligne. */
  .types {
    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: 6px;
  }

  .type {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 6px;
    min-width: 0;
    min-height: 48px;
    padding: 6px 2px;
    border: 1px solid var(--bordure);
    border-radius: 10px;
    background: var(--surface);
    color: var(--texte);
    font-size: 12px;
  }

  .type:hover {
    border-color: var(--texte-secondaire);
  }

  .type[aria-checked="true"] {
    border-color: var(--principale);
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
    font-weight: 600;
  }

  .type.autre {
    grid-column: span 2;
    min-height: 44px;
  }

  .type svg {
    max-width: 100%;
  }

  .type.plus {
    grid-column: 1 / -1;
    min-height: 28px;
    padding: 0;
    border-color: transparent;
    color: var(--texte-secondaire);
    text-decoration: underline;
  }

  /* Une option : son nom à gauche, sa commande à droite. */
  .option {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    min-height: 36px;
  }

  .nom-option {
    font-size: 13px;
  }

  .pas {
    display: inline-flex;
    align-items: center;
    border: 1px solid var(--trait-fort, var(--bordure));
    border-radius: 8px;
    overflow: hidden;
  }

  .pas button {
    display: grid;
    width: 44px;
    height: 34px;
    padding: 0;
    border: none;
    background: transparent;
    color: var(--texte);
    place-items: center;
  }

  .pas button:hover:not(:disabled, [aria-disabled="true"]) {
    background: var(--surface-2);
  }

  .pas button:disabled,
  .pas button[aria-disabled="true"] {
    opacity: 0.4;
  }

  .pas output {
    min-width: 28px;
    font-weight: 700;
    font-variant-numeric: tabular-nums;
    text-align: center;
  }

  /* L'interrupteur : sa cible fait 44 px de large, son état se lit à la place du rond et à la couleur. */
  .interrupteur {
    position: relative;
    display: block;
    flex: none;
    width: 44px;
    height: 24px;
    padding: 0;
    border: 1px solid var(--trait-fort, var(--texte-secondaire));
    border-radius: 12px;
    background: var(--surface);
  }

  .interrupteur::before {
    position: absolute;
    inset: -8px 0;
    content: "";
  }

  .interrupteur::after {
    position: absolute;
    top: 3px;
    left: 4px;
    width: 16px;
    height: 16px;
    border-radius: 8px;
    background: var(--texte-secondaire);
    content: "";
    transition: left var(--duree);
  }

  .interrupteur[aria-checked="true"] {
    border-color: var(--principale);
    background: var(--principale);
  }

  .interrupteur[aria-checked="true"]::after {
    left: 23px;
    background: var(--sur-principale);
  }

  .interrupteur[aria-disabled="true"] {
    opacity: 0.5;
    cursor: not-allowed;
  }

  /* Les deux champs du trunk, sous son interrupteur. */
  .vlans {
    display: grid;
    gap: 6px;
    padding-left: 10px;
    border-left: 2px solid var(--bordure);
  }

  .code {
    font-family: var(--police-code);
  }

  .champ[aria-invalid="true"] {
    border-color: var(--retard);
  }

  .erreur {
    margin: 0;
    color: var(--retard);
    font-size: 12px;
  }

  .ecart {
    display: grid;
    gap: 6px;
    padding: 8px;
    border-radius: 8px;
    background: var(--avertissement-fond);
  }

  .ecart .aide {
    color: var(--avertissement);
  }

  /* Un conseil : il informe, il n'empêche rien. */
  .conseil {
    margin: 0;
    padding-left: 10px;
    border-left: 2px solid var(--avertissement);
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  @media (prefers-reduced-motion: reduce) {
    .interrupteur::after {
      transition: none;
    }
  }
</style>
