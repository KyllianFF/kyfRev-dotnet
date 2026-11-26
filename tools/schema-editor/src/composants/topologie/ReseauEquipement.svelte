<script lang="ts">
  import {
    ajouterInterfaceVlan,
    conseilsReseau,
    interfacesDeRoutage,
    routeEntreVlan,
    vlanSansInterface,
  } from "../../lib/topologie/configuration";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import { type Adresse, type Equipement, type Interface, liensDeLInterface } from "../../lib/topologie/modele";
  import { lireNumeroVlan } from "../../lib/topologie/vlans";

  // L'onglet Réseau du volet (30D) : la gestion à part (adresse, VLAN, interface), l'adressage, puis les interfaces
  // de routage par VLAN si l'équipement route ; enfin les conseils, jamais bloquants : ceux de bonne pratique, et ce
  // que le diagnostic du schéma dit de cet équipement.
  let { editeur, equipement }: { editeur: EditeurSchema; equipement: Equipement } = $props();

  const schema = $derived(editeur.schema);
  const routage = $derived(interfacesDeRoutage(equipement));
  const route = $derived(routeEntreVlan(equipement));
  const conseils = $derived(conseilsReseau(schema, equipement));
  /**
   * Ce que le diagnostic du schéma dit de cet équipement : gestion, adresses en double. Ce qu'il dit d'un câble reste
   * dans le volet du schéma, comme les erreurs de structure.
   */
  const constats = $derived(
    editeur.constats.filter(
      (c) => c.niveau !== "erreur" && c.objets.includes(equipement.id) && !schema.liens.some((l) => l.id === c.objets[0]),
    ),
  );

  function modifier(action: (e: Equipement) => void, annonce?: string) {
    const id = equipement.id;
    editeur.agir((s) => {
      const e = s.equipements.find((x) => x.id === id);
      if (e) action(e);
    }, annonce);
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

  function ecrireTexte(cle: "passerelle" | "dns" | "vrf", valeur: string) {
    modifier((e) => {
      if (valeur.trim()) e[cle] = valeur.trim();
      else delete e[cle];
    });
  }

  function modifierInterface(id: string, action: (i: Interface) => void) {
    modifier((e) => {
      const i = e.interfaces.find((x) => x.id === id);
      if (i) action(i);
    });
  }

  /** Le VLAN d'une interface de routage : son nom suit s'il était celui d'avant (« Vlan10 » devient « Vlan20 »). */
  function changerVlan(i: Interface, champ: HTMLInputElement) {
    const n = lireNumeroVlan(champ.value);
    if (n === null || n === undefined) {
      champ.value = String(i.vlan?.acces ?? "");
      editeur.annonce = "Un numéro de VLAN va de 1 à 4094.";
      return;
    }
    if (routage.some((x) => x.id !== i.id && x.vlan?.acces === n)) {
      champ.value = String(i.vlan?.acces ?? "");
      editeur.annonce = `Le VLAN ${n} a déjà son interface de routage.`;
      return;
    }
    const avant = i.vlan?.acces;
    modifierInterface(i.id, (x) => {
      x.vlan = { ...(x.vlan ?? { mode: "acces" }), mode: "acces", acces: n };
      if (avant !== undefined && x.nom === `Vlan${avant}`) x.nom = `Vlan${n}`;
    });
  }

  function lireAdresse(texte: string): Adresse[] {
    const propre = texte.trim();
    if (!propre) return [];
    const [adresse = "", prefixe] = propre.split("/");
    const p = Number(prefixe);
    return [prefixe !== undefined && Number.isInteger(p) && p >= 0 && p <= 128 ? { adresse, prefixe: p } : { adresse }];
  }

  const texteAdresse = (a: Adresse | undefined) => (a ? `${a.adresse}${a.prefixe !== undefined ? `/${a.prefixe}` : ""}` : "");

  function ajouter() {
    const numero = vlanSansInterface(schema, equipement);
    editeur.agir((s) => {
      ajouterInterfaceVlan(s, equipement.id, numero);
    }, `Interface de routage du VLAN ${numero} ajoutée.`);
  }

  function retirer(i: Interface) {
    if (liensDeLInterface(schema, equipement.id, i.id).length > 0) {
      editeur.annonce = "Cette interface porte un câble : retirez d'abord le lien.";
      return;
    }
    editeur.agir((s) => {
      const e = s.equipements.find((x) => x.id === equipement.id);
      if (!e) return;
      e.interfaces = e.interfaces.filter((x) => x.id !== i.id);
      if (e.gestion?.interface === i.id) delete e.gestion.interface;
    }, `Interface de routage « ${i.nom} » retirée.`);
  }
</script>

<div class="reseau">
  <h4 class="titre-section">Gestion</h4>
  <div class="champs">
    <label>
      <span>Adresse de gestion</span>
      <input class="champ" value={equipement.gestion?.adresse ?? ""} placeholder="10.0.99.2/24" spellcheck="false" onchange={(e) => ecrireGestion("adresse", e.currentTarget.value)} />
    </label>
    <label>
      <span>VLAN de gestion</span>
      <input class="champ" type="number" min="1" max="4094" value={equipement.gestion?.vlan ?? ""} onchange={(e) => ecrireGestion("vlan", e.currentTarget.value)} />
    </label>
    <label class="large">
      <span>Interface de gestion</span>
      <select class="champ" value={equipement.gestion?.interface ?? ""} onchange={(e) => ecrireGestion("interface", e.currentTarget.value)}>
        <option value="">—</option>
        {#each equipement.interfaces as i (i.id)}<option value={i.id}>{i.nom}</option>{/each}
      </select>
    </label>
  </div>

  <h4 class="titre-section">Adressage</h4>
  <div class="champs">
    <label>
      <span>Passerelle</span>
      <input class="champ" value={equipement.passerelle ?? ""} spellcheck="false" onchange={(e) => ecrireTexte("passerelle", e.currentTarget.value)} />
    </label>
    <label>
      <span>DNS</span>
      <input class="champ" value={equipement.dns ?? ""} spellcheck="false" onchange={(e) => ecrireTexte("dns", e.currentTarget.value)} />
    </label>
    <label class="large">
      <span>VRF ou contexte</span>
      <input class="champ" value={equipement.vrf ?? ""} onchange={(e) => ecrireTexte("vrf", e.currentTarget.value)} />
    </label>
  </div>

  {#if route}
    <h4 class="titre-section" id="titre-routage">Interfaces de routage (VLAN)</h4>
    {#if routage.length > 0}
      <table class="tableau" aria-labelledby="titre-routage">
        <thead>
          <tr>
            <th scope="col">VLAN</th>
            <th scope="col">Nom</th>
            <th scope="col">Adresse</th>
            <th scope="col"><span class="visuellement-cache">Retirer</span></th>
          </tr>
        </thead>
        <tbody>
          {#each routage as i (i.id)}
            {@const numero = i.vlan?.acces}
            {@const declare = schema.vlans.find((v) => v.numero === numero)}
            <tr>
              <td class="col-vlan">
                <input class="champ" inputmode="numeric" aria-label="VLAN de {i.nom}" value={numero ?? ""} onchange={(e) => changerVlan(i, e.currentTarget)} />
              </td>
              <td class="col-nom" title={declare ? `VLAN ${declare.numero} du schéma` : i.nom}>{declare?.nom || i.nom}</td>
              <td>
                <input
                  class="champ"
                  aria-label="Adresse de {i.nom}"
                  placeholder="10.0.10.1/24"
                  spellcheck="false"
                  value={texteAdresse(i.adresses[0])}
                  onchange={(e) => {
                    const lue = lireAdresse(e.currentTarget.value);
                    modifierInterface(i.id, (x) => (x.adresses = [...lue, ...x.adresses.slice(1)]));
                  }}
                />
              </td>
              <td class="col-retirer">
                <button type="button" class="bouton discret petit" aria-label="Retirer l'interface {i.nom}" title="Retirer" onclick={() => retirer(i)}>✕</button>
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    {:else}
      <p class="aide">Aucune : une par VLAN routé, avec l'adresse qui sert de passerelle à ce VLAN.</p>
    {/if}
    <button type="button" class="bouton petit ajouter" data-action="ajouter-svi" onclick={ajouter}>Ajouter une interface VLAN</button>
  {/if}

  {#if conseils.length + constats.length > 0}
    <ul class="conseils" aria-label="Conseils">
      {#each conseils as c, k (k)}
        <li class="conseil">{c}</li>
      {/each}
      {#each constats as c, k (k)}
        <li class="conseil" class:manque={c.niveau === "manque"}>{c.message}</li>
      {/each}
    </ul>
  {/if}
</div>

<style>
  .reseau {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .titre-section {
    margin: 2px 0 0;
    color: var(--texte-secondaire);
    font-size: 11px;
    font-weight: 700;
    letter-spacing: 0.02em;
    text-transform: uppercase;
  }

  .champs {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 8px;
    margin-bottom: 4px;
  }

  label {
    display: flex;
    flex-direction: column;
    gap: 3px;
    min-width: 0;
    font-size: 12px;
  }

  label > span {
    color: var(--texte-secondaire);
  }

  .champ {
    height: 32px;
    font-size: 13px;
  }

  .large {
    grid-column: 1 / -1;
  }

  .tableau {
    width: 100%;
    border-collapse: collapse;
    font-size: 12px;
    table-layout: fixed;
  }

  .tableau th {
    padding: 4px 4px;
    border-bottom: 1px solid var(--bordure);
    color: var(--texte-secondaire);
    font-size: 11px;
    font-weight: 600;
    text-align: left;
  }

  .tableau th:first-child {
    width: 64px;
  }

  .tableau th:last-child {
    width: 34px;
  }

  .tableau td {
    padding: 3px 4px;
    border-bottom: 1px solid var(--bordure);
  }

  .tableau .champ {
    height: 28px;
    padding: 0 6px;
    font-size: 12px;
  }

  .col-nom {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .col-retirer .bouton {
    width: 28px;
    height: 28px;
    padding: 0;
    justify-content: center;
  }

  .bouton.petit {
    height: 28px;
    padding: 0 10px;
    font-size: 12px;
  }

  .ajouter {
    align-self: flex-start;
  }

  .aide {
    margin: 0;
    color: var(--texte-secondaire);
    font-size: 12px;
  }

  .conseils {
    display: flex;
    flex-direction: column;
    gap: 6px;
    margin: 4px 0 0;
    padding: 0;
    list-style: none;
  }

  .conseil {
    position: relative;
    padding: 7px 10px 7px 24px;
    border-radius: var(--rayon);
    background: var(--avertissement-fond);
    color: var(--texte);
    font-size: 12px;
    line-height: 17px;
  }

  .conseil::before {
    position: absolute;
    top: 12px;
    left: 10px;
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: var(--avertissement);
    content: "";
  }

  .conseil.manque::before {
    background: var(--texte-secondaire);
  }
</style>
