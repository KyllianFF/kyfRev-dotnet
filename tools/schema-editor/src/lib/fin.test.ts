// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";
import { recevoir } from "./api";
import { type EtatFin, finir, inscrireEditeur, suivreLesChamps } from "./fin";

type Ecoute = (evenement: Event) => void;

/** Le document, réduit à ce que le suivi des champs écoute ; chaque « change » qui y remonte est compté. */
class FaussePage {
  activeElement: object | null = null;
  readonly changes: object[] = [];
  readonly #ecoutes = new Map<string, Ecoute[]>();

  constructor() {
    suivreLesChamps(this.document);
  }

  get document(): Document {
    return this as unknown as Document;
  }

  addEventListener(type: string, ecoute: Ecoute): void {
    this.#ecoutes.set(type, [...(this.#ecoutes.get(type) ?? []), ecoute]);
  }

  /** Un événement parti de `cible` remonte jusqu'au document. */
  remonter(type: string, cible: object): void {
    if (type === "change") this.changes.push(cible);
    for (const ecoute of this.#ecoutes.get(type) ?? []) ecoute({ type, target: cible } as unknown as Event);
  }
}

/** Un champ de l'éditeur. Le quitter l'applique (« change »), mais seulement quand le moteur a la main. */
class FauxChamp {
  readonly #page: FaussePage;
  readonly #moteurALaMain: boolean;

  constructor(page: FaussePage, moteurALaMain = true) {
    this.#page = page;
    this.#moteurALaMain = moteurALaMain;
  }

  prendre(): void {
    this.#page.activeElement = this;
    this.#page.remonter("focusin", this);
  }

  ecrire(): void {
    this.#page.remonter("input", this);
  }

  blur(): void {
    this.#page.activeElement = null;
    if (this.#moteurALaMain) this.#page.remonter("change", this);
  }

  dispatchEvent(evenement: Event): boolean {
    this.#page.remonter(evenement.type, this);
    return true;
  }
}

let oublier: (() => void) | null = null;

/** L'éditeur ouvert : il retient combien de champs étaient appliqués quand on lui a demandé de finir. */
function ouvrirEditeur(page: FaussePage, etat: EtatFin = "enregistre") {
  const vus: number[] = [];
  oublier = inscrireEditeur(async () => {
    vus.push(page.changes.length);
    return etat;
  });
  return vus;
}

afterEach(() => {
  oublier?.();
  oublier = null;
  vi.unstubAllGlobals();
});

describe("finir avant le verrouillage", () => {
  it("sans schéma ouvert, il ne reste rien", async () => {
    expect(await finir(new FaussePage().document)).toBe("aucun");
  });

  it("le champ où l'on écrivait s'applique en le quittant, avant que l'éditeur enregistre", async () => {
    const page = new FaussePage();
    const vus = ouvrirEditeur(page);
    const champ = new FauxChamp(page);
    champ.prendre();
    champ.ecrire();

    expect(await finir(page.document)).toBe("enregistre");

    expect(page.changes).toEqual([champ]);
    expect(page.activeElement).toBeNull();
    expect(vus).toEqual([1]);
  });

  it("quand le moteur n'a plus la main, quitter le champ ne l'applique pas : l'îlot l'applique, une fois", async () => {
    const page = new FaussePage();
    const vus = ouvrirEditeur(page);
    const champ = new FauxChamp(page, false);
    champ.prendre();
    champ.ecrire();

    await finir(page.document);
    await finir(page.document);

    expect(page.changes).toEqual([champ]);
    expect(vus).toEqual([1, 1]);
  });

  it("un champ déjà appliqué, ou quitté pour un autre, ne l'est pas deux fois", async () => {
    const page = new FaussePage();
    ouvrirEditeur(page);
    const champ = new FauxChamp(page, false);
    champ.prendre();
    champ.ecrire();
    page.remonter("change", champ);
    await finir(page.document);
    expect(page.changes).toEqual([champ]);

    // Échap rétablit la valeur et rend la main au plan : rien n'est à appliquer.
    champ.ecrire();
    new FauxChamp(page).prendre();
    await finir(page.document);
    expect(page.changes).toEqual([champ]);
  });

  it("l'état de l'éditeur revient tel quel ; une erreur de l'éditeur n'est pas tenue pour un enregistrement", async () => {
    const page = new FaussePage();
    ouvrirEditeur(page, "conflit");
    expect(await finir(page.document)).toBe("conflit");

    oublier?.();
    oublier = inscrireEditeur(() => Promise.reject(new Error("panne")));
    expect(await finir(page.document)).toBe("erreur");
  });

  it("l'éditeur fermé est oublié, mais pas celui qui l'a remplacé", async () => {
    const page = new FaussePage();
    const premier = inscrireEditeur(async () => "modifie");
    ouvrirEditeur(page);
    premier();
    expect(await finir(page.document)).toBe("enregistre");

    oublier?.();
    oublier = null;
    expect(await finir(page.document)).toBe("aucun");
  });

  it("l'îlot répond à l'application sous le numéro de sa demande", async () => {
    const postMessage = vi.fn();
    vi.stubGlobal("window", { chrome: { webview: { postMessage } } });
    vi.stubGlobal("document", new FaussePage().document);

    recevoir({ kind: "finir", numero: 4 });

    await vi.waitFor(() =>
      expect(postMessage).toHaveBeenCalledWith({ kind: "call", id: expect.any(Number), method: "etatSauvegarde", args: [4, "aucun"] }),
    );
  });
});
