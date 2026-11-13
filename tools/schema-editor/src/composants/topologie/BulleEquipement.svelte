<script lang="ts">
  import CopyPlus from "@lucide/svelte/icons/copy-plus";
  import Trash2 from "@lucide/svelte/icons/trash-2";
  import { adresseAffichee } from "../../lib/topologie/dessin";
  import type { EditeurSchema } from "../../lib/topologie/etat.svelte";
  import type { Equipement } from "../../lib/topologie/modele";
  import { ecrireAdresse, lireAdresse } from "../../lib/topologie/simplifie";

  // Bulle d'un équipement choisi en mode simplifié (11A) : son nom et son adresse IP, rien d'autre.
  // L'adresse est celle que le dessin écrit sous l'équipement ; elle s'écrit comme adresse de gestion.
  let {
    editeur,
    equipement,
    ondupliquer,
    onretirer,
    onavance,
  }: {
    editeur: EditeurSchema;
    equipement: Equipement;
    ondupliquer: () => void;
    onretirer: () => void;
    /** Passer en mode avancé, en gardant l'équipement choisi. */
    onavance: () => void;
  } = $props();

  const adresse = $derived(adresseAffichee(equipement));
  let erreur = $state<string | null>(null);

  function renommer(champ: HTMLInputElement) {
    const nom = champ.value.trim();
    if (!nom) {
      champ.value = equipement.nom;
      editeur.annonce = "Un équipement doit avoir un nom.";
      return;
    }
    const id = equipement.id;
    editeur.agir((s) => {
      const e = s.equipements.find((x) => x.id === id);
      if (e) e.nom = nom;
    });
  }

  function ecrire(champ: HTMLInputElement) {
    const lue = lireAdresse(champ.value);
    if ("erreur" in lue) {
      // Rien n'est écrit : le champ garde la saisie, pour la corriger.
      erreur = lue.erreur;
      return;
    }
    erreur = null;
    if ((lue.adresse ?? "") === adresse) {
      champ.value = adresse;
      return;
    }
    const id = equipement.id;
    let reste = "";
    const fait = editeur.agir((s) => {
      reste = ecrireAdresse(s, id, champ.value);
    });
    if (!fait) return;
    champ.value = reste;
    if (lue.adresse === null) {
      editeur.annonce = reste
        ? `Adresse retirée. Il reste ${reste}, posée sur une interface en mode avancé : videz encore le champ pour la retirer.`
        : "Adresse retirée : plus rien ne s'écrit sous l'équipement.";
    }
  }

  /** Entrée valide le champ (il s'applique en le quittant puis en y revenant), sans fermer la bulle. */
  function entree(evenement: KeyboardEvent) {
    if (evenement.key !== "Enter") return;
    evenement.preventDefault();
    const champ = evenement.currentTarget as HTMLInputElement;
    champ.blur();
    champ.focus();
  }
</script>

<h2 id="titre-bulle">{equipement.nom}</h2>
{#key equipement.id}
  <div>
    <label for="bulle-nom">Nom</label>
    <input
      id="bulle-nom"
      class="champ"
      value={equipement.nom}
      autocomplete="off"
      onchange={(e) => renommer(e.currentTarget)}
      onkeydown={entree}
    />
  </div>
  <div>
    <label for="bulle-adresse">Adresse IP (facultative)</label>
    <input
      id="bulle-adresse"
      class="champ code"
      value={adresse}
      autocomplete="off"
      spellcheck="false"
      inputmode="url"
      aria-invalid={erreur !== null}
      aria-describedby={erreur ? "bulle-adresse-erreur" : "bulle-adresse-aide"}
      onchange={(e) => ecrire(e.currentTarget)}
      oninput={(e) => {
        if (erreur && !("erreur" in lireAdresse(e.currentTarget.value))) erreur = null;
      }}
      onkeydown={entree}
    />
  </div>
{/key}
{#if erreur}
  <p id="bulle-adresse-erreur" class="erreur" role="alert">{erreur}</p>
{:else}
  <p id="bulle-adresse-aide" class="aide">Sans adresse, rien ne s'écrit sous l'équipement.</p>
{/if}
<div class="actions">
  <button type="button" class="bouton discret" title="Dupliquer (Ctrl+D)" onclick={ondupliquer}>
    <CopyPlus size={16} aria-hidden="true" /> Dupliquer
  </button>
  <button type="button" class="bouton discret danger" title="Supprimer (Suppr) ; Ctrl+Z annule" onclick={onretirer}>
    <Trash2 size={16} aria-hidden="true" /> Supprimer
  </button>
</div>
<button type="button" class="vers-avance" onclick={onavance}>
  Interfaces, VLAN, détails : passer en mode avancé
</button>

<style>
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
</style>
