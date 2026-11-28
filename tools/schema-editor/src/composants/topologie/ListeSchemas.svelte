<script lang="ts">
  import Ellipsis from "@lucide/svelte/icons/ellipsis";
  import Pin from "@lucide/svelte/icons/pin";
  import { pluriel } from "../../lib/format";
  import { type CarteSchema, type CleTri, quand, type Tri } from "../../lib/topologie/accueil";
  import NomEditable from "./NomEditable.svelte";

  // L'accueil en liste (31B) : une ligne par schéma, les mêmes choix que la grille. Un clic sur un en-tête trie par sa
  // colonne (un second, dans l'autre sens) ; la case de l'en-tête choisit tout ce qui est affiché, ou le lâche.
  let {
    cartes,
    choisis,
    tri,
    edition,
    ontrier,
    onclic,
    oncase,
    ontout,
    onplus,
    onmenu,
    onrenommer,
    onvalider,
    onannuler,
  }: {
    cartes: CarteSchema[];
    choisis: string[];
    tri: Tri;
    /** Le schéma dont le nom est en cours de saisie. */
    edition: string | null;
    ontrier: (cle: CleTri) => void;
    onclic: (id: string, evenement: MouseEvent) => void;
    oncase: (id: string, evenement: MouseEvent) => void;
    ontout: (choisir: boolean) => void;
    onplus: (id: string, evenement: MouseEvent) => void;
    onmenu: (id: string, evenement: MouseEvent) => void;
    onrenommer: (id: string) => void;
    onvalider: (id: string, nom: string) => void;
    onannuler: () => void;
  } = $props();

  const COLONNES: { cle: CleTri; nom: string; nombre?: boolean }[] = [
    { cle: "nom", nom: "Schéma" },
    { cle: "pages", nom: "Pages", nombre: true },
    { cle: "equipements", nom: "Équipements", nombre: true },
    { cle: "etiquettes", nom: "Étiquettes" },
    { cle: "points", nom: "Diagnostic" },
    { cle: "fiches", nom: "Cité dans" },
    { cle: "modifie", nom: "Modifié" },
  ];

  const tous = $derived(cartes.length > 0 && cartes.every((c) => choisis.includes(c.id)));
  const certains = $derived(!tous && cartes.some((c) => choisis.includes(c.id)));

  /** La case de l'en-tête, à moitié cochée quand une partie seulement est choisie. */
  function indetermine(caseTout: HTMLInputElement, certains: boolean) {
    caseTout.indeterminate = certains;
    return { update: (valeur: boolean) => (caseTout.indeterminate = valeur) };
  }

  /** F2 renomme le schéma dont le nom a le clavier. */
  function touche(id: string, evenement: KeyboardEvent) {
    if (evenement.key === "F2" && edition !== id) {
      evenement.preventDefault();
      onrenommer(id);
    }
  }
</script>

<div class="tableau">
  <table>
    <thead>
      <tr>
        <th scope="col" class="case">
          <input
            type="checkbox"
            checked={tous}
            use:indetermine={certains}
            aria-label={tous ? "Lâcher tous les schémas affichés" : "Choisir tous les schémas affichés"}
            onclick={(e) => ontout(e.currentTarget.checked)}
          />
        </th>
        {#each COLONNES as c (c.cle)}
          <th
            scope="col"
            class:nombre={c.nombre}
            aria-sort={tri.cle === c.cle ? (tri.sens === "asc" ? "ascending" : "descending") : undefined}
          >
            <button type="button" class="entete" onclick={() => ontrier(c.cle)}>
              {c.nom}
              <span class="sens" aria-hidden="true">{tri.cle === c.cle ? (tri.sens === "asc" ? "▲" : "▼") : ""}</span>
            </button>
          </th>
        {/each}
        <th scope="col" class="plus"><span class="visuellement-cache">Actions</span></th>
      </tr>
    </thead>
    <tbody>
      {#each cartes as c (c.id)}
        {@const choisie = choisis.includes(c.id)}
        <tr class:choisie data-id={c.id} oncontextmenu={(e) => onmenu(c.id, e)}>
          <td class="case">
            <input
              type="checkbox"
              checked={choisie}
              aria-label={`Choisir « ${c.titre} »`}
              onclick={(e) => oncase(c.id, e)}
            />
          </td>
          <th scope="row" class="schema" onkeydown={(e) => touche(c.id, e)}>
            {#if c.epingle}<span class="epingle" title="Épinglé"><Pin size={12} aria-hidden="true" /><span class="visuellement-cache">Épinglé</span></span>{/if}
            <NomEditable
              titre={c.titre}
              edition={edition === c.id}
              bouton
              onouvrir={(e) => onclic(c.id, e)}
              onrenommer={() => onrenommer(c.id)}
              onvalider={(nom) => onvalider(c.id, nom)}
              {onannuler}
            />
          </th>
          <td class="nombre">{c.pages}</td>
          <td class="nombre">{c.equipements}</td>
          <td class="etiquettes">{c.etiquettes.join(", ") || "—"}</td>
          <td>
            {#if c.points > 0}<span class="a-verifier">{c.points} à vérifier</span>{:else}—{/if}
          </td>
          <td>{c.fiches > 0 ? pluriel(c.fiches, "fiche") : "—"}</td>
          <td>{c.modifieLe ? quand(c.modifieLe) : "—"}</td>
          <td class="plus">
            <button
              type="button"
              class="bouton discret"
              aria-haspopup="menu"
              aria-label={`Plus d'actions sur « ${c.titre} »`}
              title="Plus d'actions"
              onclick={(e) => onplus(c.id, e)}
            >
              <Ellipsis size={16} aria-hidden="true" />
            </button>
          </td>
        </tr>
      {/each}
    </tbody>
  </table>
</div>

<style>
  /* Fenêtre étroite : le tableau défile dans son cadre, la page ne défile pas de côté. */
  .tableau {
    overflow-x: auto;
    border: 1px solid var(--bordure);
    border-radius: var(--rayon-carte);
    background: var(--surface);
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 13px;
  }

  th,
  td {
    padding: 0 10px;
    height: 40px;
    border-bottom: 1px solid var(--bordure);
    text-align: left;
    white-space: nowrap;
  }

  tbody tr:last-child > * {
    border-bottom: none;
  }

  thead th {
    height: 36px;
    color: var(--texte-secondaire);
    font-size: 12px;
    font-weight: 600;
  }

  .entete {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    padding: 4px 0;
    border: none;
    background: none;
    color: inherit;
    font-weight: 600;
  }

  .entete:hover {
    color: var(--texte);
  }

  .sens {
    font-size: 9px;
  }

  .nombre {
    text-align: right;
  }

  .nombre .entete {
    flex-direction: row-reverse;
  }

  .case {
    width: 36px;
    padding-right: 0;
  }

  .case input {
    width: 16px;
    height: 16px;
    margin: 0;
    vertical-align: middle;
    accent-color: var(--principale);
    cursor: pointer;
  }

  .schema {
    max-width: 320px;
    font-weight: 600;
  }

  .schema :global(.nom) {
    display: inline-block;
    max-width: 280px;
    vertical-align: middle;
  }

  .epingle {
    display: inline-flex;
    margin-right: 4px;
    color: var(--principale);
    vertical-align: middle;
  }

  .etiquettes {
    max-width: 220px;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .a-verifier {
    padding: 1px 8px;
    border-radius: 999px;
    background: var(--avertissement-fond);
    color: var(--avertissement);
    font-size: 12px;
  }

  tbody tr:hover > * {
    background: var(--surface-2);
  }

  tr.choisie > * {
    background: var(--navigation-active-fond);
  }

  .plus {
    width: 44px;
    padding: 0 4px;
    text-align: right;
  }

  .plus .bouton {
    width: 32px;
    height: 30px;
    justify-content: center;
    padding: 0;
  }
</style>
