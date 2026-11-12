// État de l'éditeur de schémas réseau (D39) : le schéma ouvert, la
// sélection, l'historique (annuler, rétablir) et la sauvegarde automatique.
//
// Chaque opération passe par `agir` : le schéma est copié avant, puis
// l'enregistrement est programmé. Un enregistrement refusé (conflit, erreur)
// ne perd rien : le schéma reste en mémoire, l'état le dit, et l'on peut
// réessayer ou reprendre la version du disque.

import { api, messageErreur } from "../api";
import type { EtatFin } from "../fin";
import { diagnostiquer } from "./diagnostic";
import { ecrireSchema, erreursStructurelles, lireSchema } from "./format";
import type { Extremite, IdVue, Schema, Support } from "./modele";
import { schemaVide } from "./modele";
import { avecEnsembles, degrouper, ensemblesDe, groupables, grouper, Refus } from "./operations";

/** Support choisi (« auto » : d'après les ports) et bout déjà branché. */
export type CableEnMain = {
  support: Support | "auto";
  depart: Extremite | null;
  lien?: string;
  bout?: "a" | "b";
};

export type EtatSauvegarde = "enregistre" | "modifie" | "enregistrement" | "erreur" | "conflit";

const HISTORIQUE_MAX = 80;
const DELAI_SAUVEGARDE = 700;
/** Avant le verrouillage, les enregistrements tentés au plus : une modification arrivée pendant l'écriture part à son tour. */
const ESSAIS_FIN = 3;

export class EditeurSchema {
  schema = $state<Schema>(schemaVide());
  id = $state("");
  empreinte = "";
  sauvegarde = $state<EtatSauvegarde>("enregistre");
  messageSauvegarde = $state<string | null>(null);
  /** Version du disque, en cas de conflit. */
  conflit = $state<{ texte: string; empreinte: string } | null>(null);
  selection = $state<string[]>([]);
  /** Message bref après une action (« Supprimé : Ctrl+Z pour annuler »). */
  annonce = $state<string | null>(null);
  /**
   * Câble « en main » (D42), comme dans les simulateurs : on le prend (bouton
   * « Ajouter un lien » ou bout d'un câble existant), on clique un équipement
   * puis on choisit le port. `depart` est le bout déjà branché ; `lien` et
   * `bout`, le câble existant dont on a détaché ce bout.
   */
  cable = $state<CableEnMain | null>(null);

  #passe: string[] = [];
  #futur: string[] = [];
  peutAnnuler = $state(false);
  peutRetablir = $state(false);
  #minuterie: ReturnType<typeof setTimeout> | null = null;
  #enCours: Promise<void> | null = null;
  /** Schéma ancien format (dans la fiche) : créé dans le coffre au premier enregistrement. */
  #aCreer = false;
  #onconverti: ((id: string) => void) | null = null;

  readonly constats = $derived(diagnostiquer(this.schema));

  /** Ouvre le schéma `id` du coffre. */
  async ouvrir(id: string): Promise<void> {
    const lu = await api.lireSchema(id);
    const { schema, erreurs } = lireSchema(lu.texte);
    this.schema = schema;
    this.id = lu.id;
    this.empreinte = lu.empreinte;
    this.sauvegarde = "enregistre";
    if (erreurs.length > 0) {
      this.messageSauvegarde = `Ce schéma contient ${erreurs.length} erreur(s) de structure : corrigez-les avant tout enregistrement.`;
    }
  }

  /** Ouvre un schéma au format 1 (rangé dans la fiche) : il sera créé dans le coffre au premier enregistrement. */
  ouvrirAncien(source: string, onconverti: (id: string) => void): void {
    this.schema = lireSchema(source).schema;
    this.#aCreer = true;
    this.#onconverti = onconverti;
    this.sauvegarde = "modifie";
    this.messageSauvegarde =
      "Ancien format : le schéma sera rangé dans le coffre et la fiche n'en gardera qu'une référence.";
    this.programmer();
  }

  /** Copie du schéma, avant une opération. */
  #instantane(): string {
    return JSON.stringify($state.snapshot(this.schema));
  }

  /**
   * Applique une opération : copie pour l'annulation, puis enregistrement
   * programmé. Une opération refusée (`Refus`) ne laisse aucune trace.
   */
  agir(operation: (schema: Schema) => void, annonce?: string): boolean {
    const avant = this.#instantane();
    try {
      operation(this.schema);
    } catch (erreur) {
      this.schema = JSON.parse(avant);
      if (erreur instanceof Refus) {
        this.annonce = erreur.message;
        return false;
      }
      throw erreur;
    }
    this.#memoriser(avant);
    if (annonce) this.annonce = annonce;
    this.programmer();
    return true;
  }

  /** Mémorise l'état d'avant un geste continu (déplacement, redimensionnement). */
  commencerGeste(): string {
    return this.#instantane();
  }

  /** Fin du geste : mémorisé seulement si quelque chose a changé. */
  finirGeste(avant: string): void {
    if (this.#instantane() === avant) return;
    this.#memoriser(avant);
    this.programmer();
  }

  #memoriser(avant: string): void {
    this.#passe.push(avant);
    if (this.#passe.length > HISTORIQUE_MAX) this.#passe.shift();
    this.#futur = [];
    this.peutAnnuler = true;
    this.peutRetablir = false;
  }

  annuler(): void {
    const avant = this.#passe.pop();
    if (avant === undefined) return;
    this.#futur.push(this.#instantane());
    this.schema = JSON.parse(avant);
    this.#apresHistorique();
  }

  retablir(): void {
    const apres = this.#futur.pop();
    if (apres === undefined) return;
    this.#passe.push(this.#instantane());
    this.schema = JSON.parse(apres);
    this.#apresHistorique();
  }

  #apresHistorique(): void {
    this.peutAnnuler = this.#passe.length > 0;
    this.peutRetablir = this.#futur.length > 0;
    const presents = new Set([
      ...this.schema.equipements.map((e) => e.id),
      ...this.schema.liens.map((l) => l.id),
      ...this.schema.relations.map((r) => r.id),
      ...this.schema.groupes.map((g) => g.id),
      ...this.schema.annotations.map((n) => n.id),
    ]);
    this.selection = this.selection.filter((id) => presents.has(id));
    this.programmer();
  }

  changerVue(vue: IdVue): void {
    this.schema.vue = vue;
    this.programmer();
  }

  /** Programme l'enregistrement automatique. */
  programmer(): void {
    if (this.sauvegarde !== "conflit") this.sauvegarde = "modifie";
    if (this.#minuterie) clearTimeout(this.#minuterie);
    this.#minuterie = setTimeout(() => void this.enregistrer(), DELAI_SAUVEGARDE);
  }

  /** Enregistre tout de suite (fermeture, Ctrl+S). */
  async enregistrer(): Promise<void> {
    if (this.#minuterie) clearTimeout(this.#minuterie);
    this.#minuterie = null;
    if (this.#enCours) await this.#enCours;
    if (this.sauvegarde === "enregistre" || this.sauvegarde === "conflit") return;
    this.#enCours = this.#ecrire().finally(() => {
      this.#enCours = null;
    });
    await this.#enCours;
  }

  /**
   * Avant le verrouillage du coffre : l'enregistrement qui attendait part tout de suite (un refusé est retenté), et
   * l'état dit ce qu'il en reste. Un conflit attend le choix de l'utilisateur : rien ne le tranche ici.
   */
  async finir(): Promise<EtatFin> {
    await this.enregistrer();
    for (let essai = 1; essai < ESSAIS_FIN && (this.sauvegarde === "modifie" || this.sauvegarde === "enregistrement"); essai++) {
      await this.enregistrer();
    }
    return this.sauvegarde === "enregistrement" ? "modifie" : this.sauvegarde;
  }

  async #ecrire(): Promise<void> {
    const erreurs = erreursStructurelles(this.schema);
    if (erreurs.length > 0) {
      this.sauvegarde = "erreur";
      this.messageSauvegarde = `Non enregistré : ${erreurs[0]}`;
      return;
    }
    this.sauvegarde = "enregistrement";
    try {
      if (this.#aCreer) {
        const lu = await api.creerSchema(ecrireSchema({ ...$state.snapshot(this.schema), id: "" }));
        this.#aCreer = false;
        this.id = lu.id;
        this.schema.id = lu.id;
        this.empreinte = lu.empreinte;
        this.#onconverti?.(lu.id);
        // Le texte écrit par le cœur porte l'identifiant : on enregistre la version complète.
      }
      const texte = ecrireSchema($state.snapshot(this.schema));
      const resultat = await api.enregistrerSchema(this.id, texte, this.empreinte);
      if (resultat.statut === "conflit") {
        this.sauvegarde = "conflit";
        this.conflit = { texte: resultat.corpsDisque, empreinte: resultat.empreinteDisque };
        this.messageSauvegarde = "Le schéma a été modifié ailleurs depuis son ouverture.";
        return;
      }
      this.empreinte = resultat.empreinte;
      this.sauvegarde = this.#minuterie ? "modifie" : "enregistre";
      this.messageSauvegarde = null;
    } catch (erreur) {
      this.sauvegarde = "erreur";
      this.messageSauvegarde = `Non enregistré : ${messageErreur(erreur)}. Vos modifications sont gardées ; réessayez.`;
    }
  }

  /** Conflit : garder la version de l'éditeur (elle remplace celle du disque). */
  async garderMaVersion(): Promise<void> {
    if (!this.conflit) return;
    this.empreinte = this.conflit.empreinte;
    this.conflit = null;
    this.sauvegarde = "modifie";
    await this.enregistrer();
  }

  /** Conflit : reprendre la version du disque (la mienne reste annulable). */
  reprendreLeDisque(): void {
    if (!this.conflit) return;
    const avant = this.#instantane();
    try {
      this.schema = lireSchema(this.conflit.texte).schema;
    } catch (erreur) {
      this.messageSauvegarde = `Version du disque illisible : ${messageErreur(erreur)}`;
      return;
    }
    this.#memoriser(avant);
    this.empreinte = this.conflit.empreinte;
    this.conflit = null;
    this.sauvegarde = "enregistre";
    this.messageSauvegarde = null;
  }

  /** Remplace le contenu par un schéma importé (annulable) ; refusé s'il est invalide. */
  importer(texte: string): string | null {
    let lecture: ReturnType<typeof lireSchema>;
    try {
      lecture = lireSchema(texte);
    } catch (erreur) {
      return `Import refusé : ${messageErreur(erreur)}.`;
    }
    if (lecture.erreurs.length > 0) {
      return `Import refusé : ${lecture.erreurs.length} erreur(s) de structure, dont « ${lecture.erreurs[0]} ». Rien n'a été modifié.`;
    }
    const id = this.id;
    this.agir((schema) => {
      Object.assign(schema, { ...lecture.schema, id });
    });
    this.selection = [];
    return null;
  }

  // --- Sélection ---

  estChoisi(id: string): boolean {
    return this.selection.includes(id);
  }

  /** Choisit `id` ; s'il est groupé, tout son ensemble vient avec lui (et repart avec lui). */
  choisir(id: string, ajouter = false): void {
    const lot = avecEnsembles(this.schema, [id]);
    if (!ajouter) this.selection = lot;
    else if (this.estChoisi(id)) this.selection = this.selection.filter((x) => !lot.includes(x));
    else this.selection = [...new Set([...this.selection, ...lot])];
  }

  /** `ids`, élargis aux ensembles qu'ils touchent. */
  avecEnsembles(ids: string[]): string[] {
    return avecEnsembles(this.schema, ids);
  }

  /** `id` fait partie d'un ensemble. */
  estGroupe(id: string): boolean {
    return ensemblesDe(this.schema, [id]).length > 0;
  }

  /** Ce que la sélection permet : grouper (deux éléments au moins, pas déjà un seul ensemble), dégrouper. */
  readonly groupement = $derived.by(() => {
    const presents = groupables(this.schema);
    const elements = this.selection.filter((id) => presents.has(id));
    const touches = ensemblesDe(this.schema, elements);
    const unSeul = touches.length === 1 && elements.every((id) => touches[0]?.membres.includes(id)) && elements.length > 1;
    return { elements: elements.length, unSeul, peutGrouper: elements.length >= 2 && !unSeul, peutDegrouper: touches.length > 0 };
  });

  /** Groupe la sélection (Ctrl+G). */
  grouper(): void {
    const ids = new Set(this.selection);
    let membres: string[] = [];
    const fait = this.agir((schema) => {
      membres = grouper(schema, ids).membres;
    }, "Groupés : ils se choisissent et se déplacent ensemble. Ctrl+Maj+G les dégroupe.");
    if (fait) this.selection = membres;
  }

  /** Dégroupe ce que touche la sélection (Ctrl+Maj+G) : les éléments restent choisis, chacun pour soi. */
  degrouper(): void {
    const ids = new Set(this.selection);
    this.agir((schema) => {
      degrouper(schema, ids);
    }, "Dégroupés : chaque élément se choisit de nouveau seul.");
  }

  toutChoisir(): void {
    this.selection = this.schema.equipements.map((e) => e.id);
  }
}
