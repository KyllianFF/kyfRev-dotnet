<script lang="ts">
  import { onMount, untrack } from "svelte";
  import { diagnostiquer } from "../../lib/topologie/diagnostic";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import {
    type Extremite,
    interfaceDe,
    STATUTS_LIEN,
    type StatutLien,
    SUPPORTS,
    type Support,
  } from "../../lib/topologie/modele";
  import { connecter, interfaceIndisponible, Refus, supportPropose } from "../../lib/topologie/operations";
  import SelecteurInterface from "./SelecteurInterface.svelte";

  // Connexion de deux interfaces par un câble (D39) : source, destination,
  // caractéristiques du lien, vérification, validation. Les avertissements
  // n'empêchent pas de valider (on documente aussi les anomalies).
  let {
    editeur,
    source = null,
    destination = null,
    onfermer,
  }: {
    editeur: EditeurSchema;
    source?: string | null;
    destination?: string | null;
    onfermer: (lien?: string) => void;
  } = $props();

  const schema = $derived(editeur.schema);
  let dialogue: HTMLDialogElement;
  let equipementA = $state(untrack(() => source ?? ""));
  let equipementB = $state(untrack(() => destination ?? ""));
  let interfaceA = $state<string | null>(null);
  let interfaceB = $state<string | null>(null);
  let support = $state<Support>("cuivre");
  let supportChoisi = false;
  let technologie = $state("");
  let debit = $state<number | null>(null);
  let longueurM = $state<number | null>(null);
  let etiquette = $state("");
  let statut = $state<StatutLien>("actif");
  let nom = $state("");
  let refus = $state<string | null>(null);

  const eqA = $derived(schema.equipements.find((e) => e.id === equipementA));
  const eqB = $derived(schema.equipements.find((e) => e.id === equipementB));
  const tries = $derived([...schema.equipements].sort((a, b) => a.nom.localeCompare(b.nom, "fr")));

  /** Première interface libre (câblable) d'un équipement. */
  function premiereLibre(equipement: string): string | null {
    const e = schema.equipements.find((x) => x.id === equipement);
    return (
      e?.interfaces.find(
        (i) => !interfaceIndisponible(schema, { equipement, interface: i.id }) && i.genre !== "virtuel",
      )?.id ??
      e?.interfaces.find((i) => !interfaceIndisponible(schema, { equipement, interface: i.id }))?.id ??
      null
    );
  }

  onMount(() => {
    dialogue.showModal();
    if (equipementA) interfaceA = premiereLibre(equipementA);
    if (equipementB) interfaceB = premiereLibre(equipementB);
  });

  // Support proposé d'après les interfaces, tant qu'on ne l'a pas choisi.
  $effect(() => {
    const a =
      interfaceA && equipementA
        ? interfaceDe(schema, { equipement: equipementA, interface: interfaceA })
        : undefined;
    const b =
      interfaceB && equipementB
        ? interfaceDe(schema, { equipement: equipementB, interface: interfaceB })
        : undefined;
    if (!supportChoisi) support = supportPropose(a, b);
    const vitesse = Math.min(a?.vitesse ?? Number.POSITIVE_INFINITY, b?.vitesse ?? Number.POSITIVE_INFINITY);
    debit = Number.isFinite(vitesse) ? vitesse : null;
  });

  const bouts = $derived(
    equipementA && equipementB && interfaceA && interfaceB
      ? ([
          { equipement: equipementA, interface: interfaceA },
          { equipement: equipementB, interface: interfaceB },
        ] as [Extremite, Extremite])
      : null,
  );

  const proprietes = $derived({
    support,
    statut,
    ...(technologie.trim() ? { technologie: technologie.trim() } : {}),
    ...(debit ? { debit } : {}),
    ...(longueurM ? { longueurM } : {}),
    ...(etiquette.trim() ? { etiquette: etiquette.trim() } : {}),
    ...(nom.trim() ? { nom: nom.trim() } : {}),
  });

  /** Vérification sur une copie : refus éventuel et avertissements propres à ce lien. */
  const verification = $derived.by(() => {
    if (!bouts) return { refus: null, constats: [] };
    const copie = JSON.parse(JSON.stringify($state.snapshot(schema)));
    try {
      const lien = connecter(copie, bouts[0], bouts[1], proprietes);
      return {
        refus: null,
        constats: diagnostiquer(copie).filter((c) => c.niveau !== "manque" && c.objets.includes(lien.id)),
      };
    } catch (erreur) {
      return { refus: erreur instanceof Refus ? erreur.message : String(erreur), constats: [] };
    }
  });

  function valider(evenement: SubmitEvent) {
    evenement.preventDefault();
    if (!bouts) return;
    let cree: string | undefined;
    const fait = editeur.agir((s) => {
      cree = connecter(s, bouts[0], bouts[1], proprietes).id;
    }, "Lien créé.");
    if (!fait) {
      refus = editeur.annonce;
      return;
    }
    dialogue.close();
    onfermer(cree);
  }
</script>

<dialog bind:this={dialogue} class="connexion" aria-labelledby="titre-connexion" onclose={() => onfermer()}>
  <form onsubmit={valider}>
    <h2 id="titre-connexion">Connecter deux interfaces</h2>
    <div class="extremites">
      <section>
        <label class="ligne">
          <span>1. Équipement source</span>
          <select class="champ" bind:value={equipementA} onchange={() => (interfaceA = premiereLibre(equipementA))}>
            <option value="" disabled>Choisir…</option>
            {#each tries as e (e.id)}<option value={e.id}>{e.nom}</option>{/each}
          </select>
        </label>
        {#if eqA}
          <SelecteurInterface {schema} equipement={eqA} choisie={interfaceA} etiquette="2. Interface source" onchoisir={(i) => (interfaceA = i)} />
        {/if}
      </section>
      <section>
        <label class="ligne">
          <span>3. Équipement destination</span>
          <select class="champ" bind:value={equipementB} onchange={() => (interfaceB = premiereLibre(equipementB))}>
            <option value="" disabled>Choisir…</option>
            {#each tries as e (e.id)}<option value={e.id}>{e.nom}</option>{/each}
          </select>
        </label>
        {#if eqB}
          <SelecteurInterface {schema} equipement={eqB} choisie={interfaceB} etiquette="4. Interface destination" onchoisir={(i) => (interfaceB = i)} />
        {/if}
      </section>
    </div>

    <fieldset class="caracteristiques">
      <legend>5. Caractéristiques du lien</legend>
      <label>
        <span>Support physique</span>
        <select class="champ" bind:value={support} onchange={() => (supportChoisi = true)}>
          {#each SUPPORTS as s (s.valeur)}<option value={s.valeur}>{s.nom}</option>{/each}
        </select>
      </label>
      <label>
        <span>Technologie</span>
        <input class="champ" bind:value={technologie} placeholder="1000BASE-T, 10GBASE-SR…" />
      </label>
      <label>
        <span>Débit nominal (Mbit/s)</span>
        <input class="champ" type="number" min="1" bind:value={debit} />
      </label>
      <label>
        <span>Longueur (m)</span>
        <input class="champ" type="number" min="0" step="0.1" bind:value={longueurM} />
      </label>
      <label>
        <span>Étiquette du câble</span>
        <input class="champ" bind:value={etiquette} placeholder="C-042" />
      </label>
      <label>
        <span>Statut</span>
        <select class="champ" bind:value={statut}>
          {#each STATUTS_LIEN as s (s.valeur)}<option value={s.valeur}>{s.nom}</option>{/each}
        </select>
      </label>
      <label class="large">
        <span>Nom (facultatif)</span>
        <input class="champ" bind:value={nom} placeholder="Uplink cœur" />
      </label>
    </fieldset>

    <section class="verification" aria-live="polite">
      <h3>6. Vérification</h3>
      {#if !bouts}
        <p class="neutre">Choisissez les deux interfaces.</p>
      {:else if verification.refus}
        <p class="refus" role="alert">{verification.refus}</p>
      {:else if verification.constats.length === 0}
        <p class="ok">Aucune incohérence repérée.</p>
      {:else}
        <ul>
          {#each verification.constats as c, i (i)}<li class="avertissement">{c.message}</li>{/each}
        </ul>
        <p class="neutre">Ces avertissements n'empêchent pas de valider : le schéma peut documenter une anomalie.</p>
      {/if}
      {#if refus}<p class="refus" role="alert">{refus}</p>{/if}
    </section>

    <div class="actions">
      <button type="button" class="bouton" onclick={() => dialogue.close()}>Annuler</button>
      <button type="submit" class="bouton principal" disabled={!bouts || verification.refus !== null}>7. Valider le lien</button>
    </div>
  </form>
</dialog>

<style>
  dialog {
    width: min(920px, calc(100vw - 32px));
    max-height: calc(100vh - 32px);
    padding: 20px 22px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    color: var(--texte);
    box-shadow: 0 16px 48px rgb(0 0 0 / 0.25);
  }

  dialog::backdrop {
    background: var(--voile);
  }

  h2 {
    margin-bottom: 12px;
    font-size: 19px;
  }

  h3 {
    margin: 0 0 6px;
    font-size: 14px;
  }

  .extremites {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
    gap: 14px;
  }

  section {
    display: flex;
    flex-direction: column;
    gap: 8px;
    min-width: 0;
  }

  label {
    display: flex;
    flex-direction: column;
    gap: 3px;
    font-size: 13px;
  }

  label > span {
    color: var(--texte-secondaire);
  }

  .ligne > span {
    font-weight: 600;
    color: var(--texte);
  }

  .caracteristiques {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(170px, 1fr));
    gap: 8px 12px;
    margin: 14px 0;
    padding: 10px 12px 12px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
  }

  .caracteristiques legend {
    padding: 0 4px;
    font-weight: 600;
  }

  .large {
    grid-column: 1 / -1;
  }

  .verification ul {
    margin: 0;
    padding-left: 18px;
  }

  .avertissement {
    color: var(--avertissement);
  }

  .refus {
    margin: 0;
    color: var(--retard);
  }

  .ok {
    margin: 0;
    color: var(--succes);
  }

  .neutre {
    margin: 4px 0 0;
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 14px;
  }
</style>
