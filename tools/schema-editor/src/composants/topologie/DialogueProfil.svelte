<script lang="ts">
  import { onMount } from "svelte";
  import { definition } from "../../lib/topologie/catalogue";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import type { Equipement, Interface } from "../../lib/topologie/modele";
  import { appliquerProfil } from "../../lib/topologie/operations";
  import { effetsDuRetrait, regenerer } from "../../lib/topologie/ports";

  // Moins de ports, ou un membre de stack retiré (D39) : chaque lien qui
  // tombait sur une interface retirée est montré, avec un choix explicite
  // (réaffecter à une interface qui reste, ou retirer le lien). Rien n'est
  // retiré en silence ; « Annuler » ne change rien.
  let {
    editeur,
    equipementId,
    modifier,
    retirees,
    onfermer,
  }: {
    editeur: EditeurSchema;
    equipementId: string;
    modifier: (e: Equipement) => void;
    retirees: Interface[];
    onfermer: () => void;
  } = $props();

  let dialogue: HTMLDialogElement;
  const schema = $derived(editeur.schema);
  const equipement = $derived(schema.equipements.find((e) => e.id === equipementId));
  const effets = $derived(effetsDuRetrait(schema, equipementId, retirees));
  const retireesIds = $derived(new Set(retirees.map((i) => i.id)));

  /** Interfaces qui resteront, libres ou non, après le changement. */
  const restantes = $derived.by(() => {
    if (!equipement) return [];
    const essai = JSON.parse(JSON.stringify($state.snapshot(equipement))) as Equipement;
    modifier(essai);
    return regenerer(essai, schema.modeles, definition(essai.type).multiConnexion).interfaces;
  });
  const occupees = $derived(
    new Set(
      schema.liens
        .flatMap((l) => [l.a, l.b])
        .filter((b) => b.equipement === equipementId)
        .map((b) => b.interface),
    ),
  );

  let decisions = $state<Record<string, string>>({});
  const complet = $derived(effets.liens.every((l) => decisions[l.id]));
  const nomDistant = (lien: (typeof effets.liens)[number]) => {
    const distant = lien.a.equipement === equipementId && retireesIds.has(lien.a.interface) ? lien.b : lien.a;
    const e = schema.equipements.find((x) => x.id === distant.equipement);
    return `${e?.nom ?? "?"} ${e?.interfaces.find((i) => i.id === distant.interface)?.nom ?? ""}`;
  };
  const nomLocal = (lien: (typeof effets.liens)[number]) => {
    const local = lien.a.equipement === equipementId && retireesIds.has(lien.a.interface) ? lien.a : lien.b;
    return retirees.find((i) => i.id === local.interface)?.nom ?? "?";
  };

  onMount(() => dialogue.showModal());

  function valider(evenement: SubmitEvent) {
    evenement.preventDefault();
    const carte = new Map<string, { reaffecter: string } | "retirer">();
    for (const [lien, choix] of Object.entries(decisions)) {
      carte.set(lien, choix === "retirer" ? "retirer" : { reaffecter: choix });
    }
    if (
      editeur.agir(
        (s) => appliquerProfil(s, equipementId, modifier, carte),
        "Profil appliqué ; les liens touchés ont été traités.",
      )
    ) {
      dialogue.close();
    }
  }
</script>

<dialog bind:this={dialogue} aria-labelledby="titre-profil" onclose={onfermer}>
  <form onsubmit={valider}>
    <h2 id="titre-profil">Des liens tombent sur des interfaces retirées</h2>
    <p class="aide">
      Le changement retire {retirees.length} interface(s) de « {equipement?.nom} ». Décidez pour chaque lien : le
      rebrancher sur une interface qui reste, ou le retirer.
    </p>
    <table>
      <thead>
        <tr><th scope="col">Interface retirée</th><th scope="col">Relié à</th><th scope="col">Décision</th></tr>
      </thead>
      <tbody>
        {#each effets.liens as lien (lien.id)}
          <tr>
            <td>{nomLocal(lien)}</td>
            <td>{nomDistant(lien)}</td>
            <td>
              <select class="champ" aria-label={`Décision pour le lien de ${nomLocal(lien)}`} bind:value={decisions[lien.id]}>
                <option value="" disabled selected>Choisir…</option>
                <option value="retirer">Retirer ce lien</option>
                <optgroup label="Réaffecter à">
                  {#each restantes.filter((i) => !occupees.has(i.id) && !retireesIds.has(i.id) && !Object.values(decisions).includes(i.id)) as i (i.id)}
                    <option value={i.id}>{i.nom}</option>
                  {/each}
                  {#if decisions[lien.id] && decisions[lien.id] !== "retirer"}
                    <option value={decisions[lien.id]}>{restantes.find((i) => i.id === decisions[lien.id])?.nom}</option>
                  {/if}
                </optgroup>
              </select>
            </td>
          </tr>
        {/each}
      </tbody>
    </table>
    {#if effets.relations.length + effets.agregats.length > 0}
      <p class="aide">
        {effets.relations.length} relation(s) et {effets.agregats.length} agrégat(s) perdront aussi la référence à ces
        interfaces.
      </p>
    {/if}
    <div class="actions">
      <button type="button" class="bouton" onclick={() => dialogue.close()}>Annuler</button>
      <button type="submit" class="bouton principal" disabled={!complet}>Appliquer</button>
    </div>
  </form>
</dialog>

<style>
  dialog {
    width: min(640px, calc(100vw - 32px));
    padding: 20px 22px;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
    color: var(--texte);
  }

  dialog::backdrop {
    background: var(--voile);
  }

  h2 {
    font-size: 18px;
  }

  .aide {
    color: var(--texte-secondaire);
    font-size: 13px;
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 13px;
  }

  th,
  td {
    padding: 5px 6px;
    border-bottom: 1px solid var(--bordure);
    text-align: left;
  }

  .champ {
    height: 30px;
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 14px;
  }
</style>
