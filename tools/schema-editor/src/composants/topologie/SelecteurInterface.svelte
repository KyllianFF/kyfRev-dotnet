<script lang="ts">
  import type { Equipement, Schema } from "../../lib/topologie/modele";
  import { liensDeLInterface, vitesseEnClair } from "../../lib/topologie/modele";
  import { interfaceIndisponible } from "../../lib/topologie/operations";
  import { interfacesParMembre } from "../../lib/topologie/ports";

  // Choix d'une interface d'un équipement (D39) : groupées par membre de
  // stack, chacune avec sa disponibilité ; une interface occupée dit à quoi.
  let {
    schema,
    equipement,
    choisie = null,
    sauf,
    etiquette,
    onchoisir,
  }: {
    schema: Schema;
    equipement: Equipement;
    choisie?: string | null;
    /** Lien en cours de modification : ses propres extrémités restent disponibles. */
    sauf?: string;
    etiquette: string;
    onchoisir: (interfaceId: string) => void;
  } = $props();

  let recherche = $state("");
  let libresSeulement = $state(false);

  const groupes = $derived(
    interfacesParMembre(equipement).map((g) => ({
      ...g,
      interfaces: g.interfaces
        .filter((i) => !recherche.trim() || i.nom.toLowerCase().includes(recherche.trim().toLowerCase()))
        .map((i) => {
          const raison = interfaceIndisponible(schema, { equipement: equipement.id, interface: i.id }, sauf);
          const autre = liensDeLInterface(schema, equipement.id, i.id)
            .filter((l) => l.id !== sauf)
            .map((l) => (l.a.equipement === equipement.id && l.a.interface === i.id ? l.b : l.a))
            .map((bout) => schema.equipements.find((e) => e.id === bout.equipement)?.nom ?? "?");
          return { iface: i, raison, autre };
        })
        .filter((x) => !libresSeulement || !x.raison),
    })),
  );
  const libres = $derived(groupes.reduce((n, g) => n + g.interfaces.filter((x) => !x.raison).length, 0));
</script>

<fieldset class="selecteur">
  <legend>{etiquette}</legend>
  <div class="filtres">
    <input class="champ" type="search" placeholder="Filtrer (1/5, eth0…)" aria-label="Filtrer les interfaces" bind:value={recherche} />
    <label class="case"><input type="checkbox" bind:checked={libresSeulement} /> Libres seulement</label>
  </div>
  <p class="resume">{libres} interface(s) disponible(s) sur {equipement.interfaces.length}</p>
  {#if equipement.interfaces.length === 0}
    <p class="vide">Cet équipement n'a pas encore d'interface : ajoutez-en dans ses propriétés.</p>
  {/if}
  <div class="groupes">
    {#each groupes as g (g.membre ?? 0)}
      <div class="groupe-membre" role="group" aria-label={g.membre !== null ? `Membre ${g.membre}` : "Interfaces"}>
        {#if g.membre !== null}<p class="titre-membre">Membre {g.membre}</p>{/if}
        <div class="grille">
          {#each g.interfaces as x (x.iface.id)}
            <button
              type="button"
              class="port"
              class:choisi={choisie === x.iface.id}
              class:occupe={x.raison !== null}
              disabled={x.raison !== null}
              aria-pressed={choisie === x.iface.id}
              title={x.raison
                ? `${x.iface.nom} : ${x.raison}${x.autre.length ? ` (vers ${x.autre.join(", ")})` : ""}`
                : `${x.iface.nom} — libre${x.iface.vitesse ? `, ${vitesseEnClair(x.iface.vitesse)}` : ""}${x.iface.connecteur ? `, ${x.iface.connecteur}` : ""}`}
              onclick={() => onchoisir(x.iface.id)}
            >
              <span class="nom-port">{x.iface.nom}</span>
              <span class="etat">{x.raison ? (x.autre.length ? `→ ${x.autre[0]}` : "indisponible") : x.iface.genre === "fibre" ? "fibre" : "libre"}</span>
            </button>
          {/each}
        </div>
      </div>
    {/each}
  </div>
</fieldset>

<style>
  .selecteur {
    min-width: 0;
    margin: 0;
    padding: 8px 10px 10px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon);
  }

  legend {
    padding: 0 4px;
    font-weight: 600;
  }

  .filtres {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
  }

  .filtres .champ {
    flex: 1 1 140px;
    width: auto;
    height: 30px;
  }

  .case {
    display: flex;
    align-items: center;
    gap: 4px;
    font-size: 13px;
  }

  .resume,
  .vide,
  .titre-membre {
    margin: 6px 0 4px;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .titre-membre {
    font-weight: 600;
  }

  .groupes {
    max-height: 260px;
    overflow-y: auto;
  }

  .grille {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(78px, 1fr));
    gap: 4px;
  }

  .port {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    min-width: 0;
    padding: 4px 6px;
    border: 1px solid var(--bordure);
    border-radius: 4px;
    background: var(--surface);
    color: var(--texte);
    font-size: 12px;
    text-align: left;
    cursor: pointer;
  }

  .port:hover:not(:disabled) {
    border-color: var(--principale);
  }

  .port.choisi {
    border-color: var(--principale);
    background: var(--navigation-active-fond);
    color: var(--navigation-active-texte);
    font-weight: 600;
  }

  .port.occupe {
    border-style: dashed;
    background: var(--surface-2);
    color: var(--texte-secondaire);
    cursor: not-allowed;
  }

  .nom-port,
  .etat {
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .etat {
    color: var(--texte-secondaire);
    font-size: 10px;
  }
</style>
