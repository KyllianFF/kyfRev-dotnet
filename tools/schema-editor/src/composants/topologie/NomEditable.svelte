<script lang="ts">
  // Le nom d'un schéma sur l'accueil (31E) : un double clic le renomme sur place, Entrée garde, Échap annule. Un clic
  // simple ouvre le détail, un instant plus tard : le temps de voir si c'est un double clic. Au clavier (Entrée sur le
  // nom de la liste), le détail s'ouvre tout de suite ; F2 et « Renommer » du clic droit renomment aussi.
  let {
    titre,
    edition,
    bouton = false,
    niveau = "h3",
    onouvrir,
    onrenommer,
    onvalider,
    onannuler,
  }: {
    titre: string;
    /** Le champ de saisie est ouvert. */
    edition: boolean;
    /** Le nom est un bouton (la liste) : il prend le clavier, Entrée ouvre le détail. */
    bouton?: boolean;
    niveau?: "h3" | "span";
    /** Un clic sur le nom (avec ses touches tenues : Ctrl et Maj choisissent). */
    onouvrir: (evenement: MouseEvent) => void;
    /** Un double clic : ouvrir le champ. */
    onrenommer: () => void;
    onvalider: (nom: string) => void;
    onannuler: () => void;
  } = $props();

  /** Le délai d'un double clic : un clic seul n'ouvre le détail qu'après. */
  const DOUBLE_CLIC = 260;
  let attente: ReturnType<typeof setTimeout> | undefined;
  let fini = false;

  function clic(evenement: MouseEvent) {
    clearTimeout(attente);
    // Au clavier (detail 0), ou pour choisir (Ctrl, Maj) : tout de suite.
    if (evenement.detail === 0 || evenement.ctrlKey || evenement.metaKey || evenement.shiftKey) {
      onouvrir(evenement);
      return;
    }
    if (evenement.detail > 1) return;
    const copie = { ctrlKey: false, metaKey: false, shiftKey: false, detail: 1 } as MouseEvent;
    attente = setTimeout(() => onouvrir(copie), DOUBLE_CLIC);
  }

  function double(evenement: MouseEvent) {
    clearTimeout(attente);
    evenement.preventDefault();
    evenement.stopPropagation();
    onrenommer();
  }

  /** Le champ prend le clavier, son texte choisi ; il ne garde qu'une fois (Entrée, ou la main portée ailleurs). */
  function saisir(champ: HTMLInputElement) {
    fini = false;
    champ.focus();
    champ.select();
  }

  function finir(champ: HTMLInputElement, garder: boolean) {
    if (fini) return;
    fini = true;
    const nom = champ.value.trim();
    if (garder && nom && nom !== titre) onvalider(nom);
    else onannuler();
  }
</script>

{#if edition}
  <input
    class="champ renommer"
    value={titre}
    maxlength="200"
    aria-label={`Nouveau nom de « ${titre} »`}
    use:saisir
    onkeydown={(e) => {
      e.stopPropagation();
      if (e.key === "Enter") {
        e.preventDefault();
        finir(e.currentTarget, true);
      } else if (e.key === "Escape") {
        e.preventDefault();
        finir(e.currentTarget, false);
      }
    }}
    onclick={(e) => e.stopPropagation()}
    ondblclick={(e) => e.stopPropagation()}
    onblur={(e) => finir(e.currentTarget, true)}
  />
{:else if bouton}
  <button type="button" class="nom" title="Double clic : renommer" onclick={clic} ondblclick={double}>{titre}</button>
{:else if niveau === "h3"}
  <!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_noninteractive_element_interactions -->
  <h3 class="nom" title="Double clic : renommer" onclick={clic} ondblclick={double}>{titre}</h3>
{:else}
  <!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_static_element_interactions -->
  <span class="nom" title="Double clic : renommer" onclick={clic} ondblclick={double}>{titre}</span>
{/if}

<style>
  .nom {
    margin: 0;
    overflow: hidden;
    font-size: 15px;
    font-weight: 600;
    text-overflow: ellipsis;
    white-space: nowrap;
    cursor: pointer;
  }

  button.nom {
    max-width: 100%;
    padding: 0;
    border: none;
    background: none;
    color: inherit;
    font-size: inherit;
    text-align: left;
  }

  button.nom:hover {
    text-decoration: underline;
  }

  .renommer {
    height: 30px;
    font-size: 14px;
    font-weight: 600;
  }
</style>
