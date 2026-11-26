<script lang="ts">
  import { tick } from "svelte";
  import { definition } from "../../lib/topologie/catalogue";
  import {
    apercuDuCollage,
    type ConfigurationCopiee,
    collerSurLesCibles,
    copierConfiguration,
  } from "../../lib/topologie/configuration";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import type { ActionEquipement } from "../../lib/topologie/menuEquipement";
  import type { Equipement, Schema } from "../../lib/topologie/modele";
  import { dupliquerEquipements, retirerEquipements } from "../../lib/topologie/operations";
  import { retirerCable } from "../../lib/topologie/simplifie";
  import { configurationCopiee } from "../../lib/topologie/volet.svelte";

  // Ce que font les entrées du menu d'un équipement (« ⋯ » de son volet, clic droit sur le plan) : une seule fois ici,
  // pour les deux menus, avec les deux fenêtres qu'elles ouvrent, « Coller sur… » et « Retirer… ». Chaque action est
  // annulable d'un Ctrl+Z, et dit ce qu'elle a fait.
  let {
    editeur,
    simplifie = false,
    onconnecter,
    oncopier,
    onrendre,
  }: {
    editeur: EditeurSchema;
    simplifie?: boolean;
    onconnecter: (source: string) => void;
    /** Copier la sélection (Ctrl+C). */
    oncopier: () => void;
    /** Rendre la main au plan, quand ce qui avait ouvert le menu n'est plus là. */
    onrendre: () => void;
  } = $props();

  type Origine = HTMLElement | SVGElement | null;

  /** Le clavier revient à ce qui avait ouvert le menu, ou au plan. */
  function rendre(origine: Origine) {
    void tick().then(() => {
      if (origine?.isConnected) origine.focus();
      else onrendre();
    });
  }

  function modifier(id: string, action: (e: Equipement) => void, annonce?: string) {
    editeur.agir((s) => {
      const e = s.equipements.find((x) => x.id === id);
      if (e) action(e);
    }, annonce);
  }

  const copiee = $derived(configurationCopiee.valeur);
  let coller = $state<{ cochees: string[]; origine: Origine } | null>(null);
  let parNomSeulement = $state(false);
  let aRetirer = $state<{ ids: string[]; origine: Origine } | null>(null);

  /** Fait l'action du menu sur les équipements `ids` ; `origine` reprend la main ensuite. */
  export function agir(action: ActionEquipement, ids: string[], origine: Origine = null): void {
    const un = ids.length === 1 ? editeur.schema.equipements.find((e) => e.id === ids[0]) : undefined;
    switch (action) {
      case "connecter":
        if (un) onconnecter(un.id);
        return;
      case "dupliquer":
        editeur.agir(
          (s) => {
            editeur.selection = dupliquerEquipements(s, new Set(ids));
          },
          ids.length > 1 ? "Équipements dupliqués." : "Équipement dupliqué.",
        );
        rendre(origine);
        return;
      case "copier":
        oncopier();
        rendre(origine);
        return;
      case "verrouiller":
        if (!un) return;
        modifier(
          un.id,
          (x) => {
            if (x.verrou) delete x.verrou;
            else x.verrou = true;
          },
          un.verrou ? "Place déverrouillée." : "Place verrouillée : l'équipement ne bouge plus sur le plan.",
        );
        rendre(origine);
        return;
      case "replier":
        if (!un) return;
        modifier(
          un.id,
          (x) => {
            if (x.replie) delete x.replie;
            else x.replie = true;
          },
          un.replie ? "Déplié : ce qu'il héberge est de nouveau dessiné." : "Replié : ce qu'il héberge n'est plus dessiné.",
        );
        rendre(origine);
        return;
      case "remettre-nom":
        if (!un) return;
        modifier(un.id, (x) => delete x.pose, "Nom remis à sa place.");
        rendre(origine);
        return;
      case "copier-configuration":
        if (!un) return;
        configurationCopiee.valeur = copierConfiguration($state.snapshot(editeur.schema) as Schema, un.id);
        editeur.annonce = `Configuration de « ${un.nom} » copiée : « Coller sur… » la reprend sur d'autres équipements.`;
        rendre(origine);
        return;
      case "coller-configuration":
        if (!copiee) return;
        parNomSeulement = false;
        coller = { cochees: ids.filter((id) => id !== copiee.source), origine };
        return;
      case "coller-sur-la-selection":
        if (copiee) collerSur(ids, copiee, false);
        rendre(origine);
        return;
      case "retirer":
        aRetirer = { ids, origine };
        return;
    }
  }

  /** Colle en une fois (un seul Ctrl+Z) et dit toujours ce qui s'est passé, même quand rien n'a changé. */
  function collerSur(ids: string[], source: ConfigurationCopiee, nomSeulement: boolean) {
    let message = "";
    const fait = editeur.agir((s) => {
      message = collerSurLesCibles(s, ids, source, nomSeulement ? "nom" : "place");
    });
    if (fait) editeur.annonce = message;
  }

  // --- « Coller sur… » ----------------------------------------------------------------------------------------------

  const autres = $derived(
    copiee
      ? editeur.schema.equipements
          .filter((e) => e.id !== copiee.source)
          .sort((a, b) => Number(b.type === copiee.type) - Number(a.type === copiee.type) || a.nom.localeCompare(b.nom, "fr"))
      : [],
  );
  const apercus = $derived(
    copiee && coller ? new Map(autres.map((e) => [e.id, apercuDuCollage(e, copiee)])) : new Map<string, ReturnType<typeof apercuDuCollage>>(),
  );
  /** La place des ports servirait sur un équipement coché : on la propose, avec un exemple. */
  const exemplePlace = $derived(
    coller ? coller.cochees.map((id) => apercus.get(id)).find((a) => a && a.parPlace > 0)?.exemple : undefined,
  );

  function ditCeQuiSeraRepris(id: string): string {
    const a = apercus.get(id);
    if (!a) return "";
    const place = parNomSeulement ? 0 : a.parPlace;
    const n = a.parNom + place;
    if (n === 0) return "aucun port en commun";
    const ports = `${n} ${n > 1 ? "ports" : "port"}`;
    if (place === 0) return `${ports} de même nom`;
    return a.parNom === 0 ? `${ports}, par leur place` : `${ports}, dont ${place} par leur place`;
  }

  // --- « Retirer… » ---------------------------------------------------------------------------------------------------

  const retires = $derived(aRetirer ? editeur.schema.equipements.filter((e) => aRetirer?.ids.includes(e.id)) : []);
  const cablesRetires = $derived(
    aRetirer
      ? editeur.schema.liens.filter((l) => aRetirer?.ids.includes(l.a.equipement) || aRetirer?.ids.includes(l.b.equipement)).length
      : 0,
  );

  // Une fenêtre fermée rend la main à ce qui avait ouvert le menu. L'origine se lit avant d'effacer l'état qui la porte.
  function finirRetrait() {
    if (!aRetirer) return;
    const retour = aRetirer.origine;
    aRetirer = null;
    rendre(retour);
  }

  function finirCollage() {
    if (!coller) return;
    const retour = coller.origine;
    coller = null;
    rendre(retour);
  }

  function retirer(s: Schema, ids: Set<string>) {
    // Mode simplifié : un port libéré ne garde pas le trunk posé pour son câble.
    if (simplifie) for (const l of s.liens.filter((x) => ids.has(x.a.equipement) || ids.has(x.b.equipement))) retirerCable(s, l.id);
    retirerEquipements(s, ids);
  }
</script>

{#if aRetirer && retires.length > 0}
  {@const seul = retires.length === 1 ? retires[0] : undefined}
  <dialog
    class="confirmer"
    aria-labelledby="titre-retirer"
    {@attach (d: HTMLDialogElement) => d.showModal()}
    onclose={finirRetrait}
  >
    <form
      method="dialog"
      onsubmit={(e) => {
        e.preventDefault();
        const ids = new Set(retires.map((x) => x.id));
        const dialogue = e.currentTarget.closest("dialog");
        editeur.agir(
          (s) => retirer(s, ids),
          ids.size === 1
            ? "Équipement retiré, avec ses liens : Ctrl+Z pour annuler."
            : `${ids.size} équipements retirés, avec leurs liens : Ctrl+Z pour annuler.`,
        );
        editeur.selection = [];
        dialogue?.close();
        // Les équipements retirés, la fenêtre n'a plus rien à montrer : elle s'en va tout de suite.
        finirRetrait();
      }}
    >
      <h2 id="titre-retirer">{seul ? `Retirer « ${seul.nom} » ?` : `Retirer ${retires.length} équipements ?`}</h2>
      <p class="aide">
        {#if seul}
          {cablesRetires === 0
            ? "L'équipement quitte le schéma."
            : `L'équipement quitte le schéma avec ${cablesRetires === 1 ? "son câble" : `ses ${cablesRetires} câbles`}.`}
        {:else}
          {cablesRetires === 0
            ? "Ils quittent le schéma."
            : `Ils quittent le schéma avec ${cablesRetires === 1 ? "leur câble" : `leurs ${cablesRetires} câbles`}.`}
        {/if}
        Ctrl+Z annule.
      </p>
      <div class="boutons-dialogue">
        <button type="button" class="bouton" onclick={(e) => e.currentTarget.closest("dialog")?.close()}>Annuler</button>
        <button type="submit" class="bouton principal danger">Retirer</button>
      </div>
    </form>
  </dialog>
{/if}

{#if coller && copiee}
  {@const source = copiee}
  <dialog
    class="confirmer"
    aria-labelledby="titre-coller"
    {@attach (d: HTMLDialogElement) => d.showModal()}
    onclose={finirCollage}
  >
    <form
      method="dialog"
      onsubmit={(e) => {
        e.preventDefault();
        const dialogue = e.currentTarget.closest("dialog");
        if (!coller || coller.cochees.length === 0) return;
        collerSur(coller.cochees, source, parNomSeulement);
        dialogue?.close();
      }}
    >
      <h2 id="titre-coller">Coller la configuration de « {source.nom} »</h2>
      <p class="aide">
        Chaque port de même nom reprend le mode, les VLAN, l'activation et le PoE ; le VLAN de gestion suit. Les noms,
        les adresses et les câbles ne changent pas. Ctrl+Z annule.
      </p>
      {#if autres.length === 0}
        <p class="aide">Aucun autre équipement dans ce schéma.</p>
      {:else}
        <fieldset class="cibles">
          <legend class="visuellement-cache">Équipements qui reçoivent la configuration</legend>
          {#each autres as e (e.id)}
            <label class="case">
              <input type="checkbox" name="cible" value={e.id} bind:group={coller.cochees} />
              <span class="cible">
                <span class="nom-cible">{e.nom}</span>
                <span class="detail-cible">{definition(e.type).nom} · {ditCeQuiSeraRepris(e.id)}</span>
              </span>
            </label>
          {/each}
        </fieldset>
        {#if exemplePlace}
          <fieldset class="par">
            <legend>Ports aux noms différents</legend>
            <label class="case">
              <input type="radio" name="par" value="place" checked={!parNomSeulement} onchange={() => (parNomSeulement = false)} />
              <span>Par leur place : même membre, même numéro («&nbsp;{exemplePlace.de}&nbsp;» sur «&nbsp;{exemplePlace.vers}&nbsp;»)</span>
            </label>
            <label class="case">
              <input type="radio" name="par" value="nom" checked={parNomSeulement} onchange={() => (parNomSeulement = true)} />
              <span>Par leur nom seulement</span>
            </label>
          </fieldset>
        {/if}
      {/if}
      <div class="boutons-dialogue">
        <button type="button" class="bouton" onclick={(e) => e.currentTarget.closest("dialog")?.close()}>Annuler</button>
        <button
          type="submit"
          class="bouton principal"
          disabled={coller.cochees.length === 0}
          title={coller.cochees.length === 0 ? "Cochez au moins un équipement" : undefined}
        >Coller</button>
      </div>
    </form>
  </dialog>
{/if}

<style>
  dialog.confirmer {
    width: min(460px, calc(100vw - 32px));
    padding: 18px 20px;
  }

  dialog.confirmer h2 {
    margin-bottom: 8px;
    font-size: 17px;
  }

  .aide {
    margin: 2px 0 6px;
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .case {
    display: flex;
    flex-direction: row;
    align-items: center;
    gap: 8px;
    min-width: 0;
    font-size: 13px;
  }

  .cibles {
    display: flex;
    flex-direction: column;
    gap: 2px;
    max-height: min(260px, 40vh);
    margin: 8px 0 0;
    padding: 4px;
    overflow-y: auto;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
  }

  .cibles .case {
    min-height: 40px;
    padding: 3px 6px;
    border-radius: 6px;
  }

  .cibles .case:hover {
    background: var(--surface-2);
  }

  .cible {
    display: flex;
    flex: 1;
    flex-direction: column;
    min-width: 0;
  }

  .nom-cible,
  .detail-cible {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .nom-cible {
    color: var(--texte);
  }

  .detail-cible {
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .par {
    display: flex;
    flex-direction: column;
    gap: 4px;
    margin: 10px 0 0;
    padding: 0;
    border: none;
  }

  .par legend {
    margin-bottom: 4px;
    padding: 0;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .par .case {
    align-items: flex-start;
  }

  .par .case input {
    margin-top: 2px;
  }

  .boutons-dialogue {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 14px;
  }

  .bouton.danger {
    border-color: var(--retard);
    background: var(--retard);
  }

  .bouton.danger:hover {
    border-color: var(--retard);
    background: var(--retard);
    filter: brightness(0.92);
  }
</style>
