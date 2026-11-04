<script lang="ts">
  import Copy from "@lucide/svelte/icons/copy";
  import Download from "@lucide/svelte/icons/download";
  import TriangleAlert from "@lucide/svelte/icons/triangle-alert";
  import X from "@lucide/svelte/icons/x";
  import { onMount } from "svelte";
  import { capaciteDe, planEnCsv, planIp } from "../../lib/topologie/adressage";
  import type { Schema } from "../../lib/topologie/modele";

  // Plan d'adressage IP du schéma (D47) : les adresses par sous-réseau, avec
  // VLAN, passerelles, taux d'occupation et doublons ; copie pour un tableur,
  // export CSV ; un clic sur un équipement l'amène au centre du plan.
  let {
    schema,
    onmontrer,
    onexporter,
    onfermer,
  }: {
    schema: Schema;
    onmontrer: (id: string) => void;
    onexporter: (csv: string) => void;
    onfermer: () => void;
  } = $props();

  let boite: HTMLDialogElement;
  let copie = $state(false);
  const plan = $derived(planIp(schema));

  onMount(() => boite.showModal());

  async function copier() {
    try {
      await navigator.clipboard.writeText(planEnCsv(plan).replaceAll(";", "\t"));
      copie = true;
      setTimeout(() => (copie = false), 2000);
    } catch {
      copie = false;
    }
  }

  const occupation = (n: number, capacite: number | null) =>
    capacite ? `${n} / ${capacite} (${Math.round((100 * n) / capacite)} %)` : `${n}`;
</script>

<dialog
  bind:this={boite}
  class="plan-ip"
  aria-labelledby="titre-plan-ip"
  oncancel={(e) => {
    e.preventDefault();
    onfermer();
  }}
>
  <header>
    <h2 id="titre-plan-ip">Plan d'adressage IP</h2>
    <p class="resume">
      {plan.total} adresse(s) · {plan.sousReseaux.filter((s) => s.reseau).length} sous-réseau(x)
      {#if plan.doublons > 0}
        · <span class="alerte"><TriangleAlert size={14} aria-hidden="true" /> {plan.doublons} adresse(s) en double</span>
      {/if}
    </p>
    <div class="actions">
      <button type="button" class="bouton" onclick={copier} disabled={plan.total === 0} title="Copier le tableau, à coller dans un tableur">
        <Copy size={16} aria-hidden="true" /> {copie ? "Copié" : "Copier"}
      </button>
      <button type="button" class="bouton" onclick={() => onexporter(planEnCsv(plan))} disabled={plan.total === 0}>
        <Download size={16} aria-hidden="true" /> Exporter en CSV
      </button>
      <button type="button" class="bouton discret" aria-label="Fermer" title="Fermer (Échap)" onclick={onfermer}>
        <X size={16} aria-hidden="true" />
      </button>
    </div>
  </header>

  {#if plan.total === 0}
    <p class="vide">
      Aucune adresse dans ce schéma. Les adresses se saisissent dans les propriétés d'un équipement (interfaces, gestion).
    </p>
  {:else}
    <div class="sous-reseaux">
      {#each plan.sousReseaux as s (s.reseau ?? "")}
        {@const prefixe = s.reseau ? Number(s.reseau.split("/")[1]) : null}
        <section aria-label={s.reseau ?? "Sans sous-réseau connu"}>
          <h3>
            <span class="reseau">{s.reseau ?? "Sans masque connu"}</span>
            {#if s.vlans.length}<span class="etiquette">VLAN {s.vlans.join(", ")}</span>{/if}
            {#if s.passerelles.length}<span class="etiquette">passerelle {s.passerelles.join(", ")}</span>{/if}
            <span class="occupation">{occupation(s.lignes.length, prefixe === null ? null : capaciteDe(prefixe))}</span>
          </h3>
          <table>
            <thead>
              <tr><th scope="col">Adresse</th><th scope="col">Équipement</th><th scope="col">Interface</th><th scope="col">VLAN</th></tr>
            </thead>
            <tbody>
              {#each s.lignes as l, i (`${l.equipement}-${l.adresse}-${i}`)}
                <tr class:doublon={l.doublon}>
                  <td class="adresse">
                    {l.adresse}{l.prefixe !== undefined ? `/${l.prefixe}` : ""}
                    {#if l.doublon}<span class="alerte" title="Cette adresse figure aussi ailleurs dans le schéma"> (en double)</span>{/if}
                  </td>
                  <td>
                    <button type="button" class="lien-texte" title="Montrer sur le plan" onclick={() => onmontrer(l.equipement)}>{l.nomEquipement}</button>
                  </td>
                  <td>{l.nomInterface ?? ""}{l.gestion ? " (gestion)" : ""}</td>
                  <td>{l.vlan ?? ""}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </section>
      {/each}
    </div>
  {/if}
</dialog>

<style>
  .plan-ip {
    width: min(760px, calc(100vw - 32px));
    max-height: calc(100vh - 64px);
    padding: 0;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
    background: var(--surface);
    color: var(--texte);
  }

  .plan-ip::backdrop {
    background: rgb(0 0 0 / 0.35);
  }

  header {
    position: sticky;
    top: 0;
    display: grid;
    grid-template-columns: 1fr auto;
    gap: 2px 12px;
    padding: 14px 16px 10px;
    border-bottom: 1px solid var(--bordure);
    background: var(--surface);
  }

  h2 {
    font-size: 18px;
  }

  .resume {
    grid-column: 1;
    margin: 0;
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  .actions {
    display: flex;
    grid-row: 1 / span 2;
    grid-column: 2;
    align-items: center;
    gap: 6px;
  }

  .alerte {
    display: inline-flex;
    align-items: center;
    gap: 3px;
    color: var(--retard);
    font-weight: 600;
  }

  .sous-reseaux {
    padding: 4px 16px 16px;
  }

  h3 {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: 8px;
    margin: 14px 0 6px;
    font-size: 15px;
  }

  .reseau {
    font-family: var(--police-code, Consolas, monospace);
  }

  .etiquette {
    padding: 1px 8px;
    border-radius: 999px;
    background: var(--surface-2);
    color: var(--texte-secondaire);
    font-family: var(--police);
    font-size: 12px;
    font-weight: 500;
  }

  .occupation {
    margin-left: auto;
    color: var(--texte-secondaire);
    font-family: var(--police);
    font-size: 12px;
    font-weight: 500;
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 13px;
  }

  th {
    padding: 4px 8px;
    border-bottom: 1px solid var(--bordure);
    color: var(--texte-secondaire);
    font-size: 12px;
    font-weight: 600;
    text-align: left;
  }

  td {
    padding: 4px 8px;
    border-bottom: 1px solid var(--surface-2);
  }

  td.adresse {
    font-family: var(--police-code, Consolas, monospace);
  }

  tr.doublon td {
    background: var(--retard-fond);
  }

  .lien-texte {
    padding: 0;
    border: none;
    background: none;
    color: var(--principale);
    font: inherit;
    text-align: left;
    text-decoration: underline;
    cursor: pointer;
  }

  .vide {
    padding: 24px 16px;
    color: var(--texte-secondaire);
  }
</style>
