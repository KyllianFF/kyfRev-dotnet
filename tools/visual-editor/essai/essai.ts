// La page d'essai de l'éditeur, hors de l'application et hors du paquet : elle joue l'hôte (window.chrome.webview),
// charge une fiche d'exemple et envoie les messages « theme », « page », « paper » et « load » d'après l'adresse.
//
//   npm run dev, puis http://localhost:5173/essai.html?papier=grands-carreaux&page=a5&police=manuscrite
//
//   papier  uni | petits-carreaux | grands-carreaux | lignes | points | millimetre | aucun     (grands-carreaux)
//   pas     le pas des lignes d'écriture, en pixels ; 0 : aucune ligne                (30.08 ou 37.76 selon le papier)
//   page    sans-fin | a4 | a5 | a6                                                    (sans-fin)
//   marge   les marges de la feuille, en millimètres                                   (15)
//   police  application | georgia | manuscrite | feutre | code                         (application)
//   taille  la taille du texte, en pixels                                              (17)
//   theme   clair | sombre                                                             (clair)
//   fiche   courte | longue (assez de texte pour plusieurs pages) | vide               (courte)
//   traits  une couleur pour les lignes du papier, pour mieux les voir : traits=red    (celle du thème)
//
// Dans la console : `essai.envoyer({ kind: "paper", style: "points", step: 37.76 })` envoie un message de l'hôte,
// `essai.recus` garde ceux de l'éditeur, et `essai.verifier()` dit de combien chaque bloc de texte s'écarte des lignes.

import type { DepuisEditeur, VersEditeur } from "../src/messages";

const parametres = new URLSearchParams(location.search);
const choix = <T extends string>(nom: string, possibles: readonly T[], defaut: T): T => {
  const valeur = parametres.get(nom);
  return (possibles as readonly string[]).includes(valeur ?? "") ? (valeur as T) : defaut;
};
const nombre = (nom: string, defaut: number): number => {
  const valeur = Number.parseFloat(parametres.get(nom) ?? "");
  return Number.isFinite(valeur) ? valeur : defaut;
};

const MM = 96 / 25.4;

/** Les pas que donne l'application, arrondis au trois-centième de pouce : 8 mm et 10 mm. */
const PAS: Record<string, number> = {
  "grands-carreaux": 30.08,
  lignes: 30.08,
  "petits-carreaux": 37.76,
  points: 37.76,
  millimetre: 37.76,
};

const FEUILLES = { a4: [210, 297], a5: [148, 210], a6: [105, 148] } as const;

const POLICES = {
  application: "var(--police)",
  georgia: "Georgia, serif",
  manuscrite: '"Segoe Print", cursive',
  feutre: '"Ink Free", "Segoe Print", cursive',
  code: "var(--police-code)",
} as const;

const CLAIR: Record<string, string> = {
  "--fond": "#edefe9",
  "--surface": "#fafbf7",
  "--surface-2": "#e3e8e0",
  "--bordure": "#d3d9d0",
  "--texte": "#1d2822",
  "--texte-secondaire": "#4f5c54",
  "--lien": "#1f5f8b",
  "--principale": "#2f6b4f",
  "--focus": "#1f5f8b",
  "--selection": "#cfe0f0",
  "--sur-selection": "#1d2822",
  "--surlignage": "#eadcf5",
  "--retard": "#a33a2b",
  "--avertissement": "#8a5a12",
  "--succes": "#2f6b4f",
  "--repere-rappel": "#8a5a12",
  "--repere-passage": "#4a5fa8",
  "--pastille-passage-fond": "#dfe5f5",
  "--tableau-sobre-bande": "#f1f3ee",
  "--tableau-foret-entete": "#2f6b4f",
  "--tableau-foret-entete-texte": "#ffffff",
  "--tableau-foret-bande": "#e4efe8",
  "--tableau-foret-trait": "#b9d2c4",
  "--schema": "light",
  "--clarte-min": "0",
  "--clarte-max": "0.45",
};

const SOMBRE: Record<string, string> = {
  ...CLAIR,
  "--fond": "#191714",
  "--surface": "#24211c",
  "--surface-2": "#2e2a24",
  "--bordure": "#38332b",
  "--texte": "#efe9dc",
  "--texte-secondaire": "#b4ab9b",
  "--lien": "#9cc4e4",
  "--principale": "#8fc7a8",
  "--selection": "#3a4a5c",
  "--sur-selection": "#efe9dc",
  "--retard": "#f0a090",
  "--tableau-sobre-bande": "#2a2620",
  "--tableau-foret-entete": "#8fc7a8",
  "--tableau-foret-entete-texte": "#13201a",
  "--tableau-foret-bande": "#26302a",
  "--tableau-foret-trait": "#3f5246",
  "--schema": "dark",
  "--clarte-min": "0.72",
  "--clarte-max": "1",
};

/** Une petite image, dessinée ici : l'éditeur n'affiche que celles du coffre et les « data:image ». */
function image(largeur: number, hauteur: number): string {
  const toile = document.createElement("canvas");
  toile.width = largeur;
  toile.height = hauteur;
  const dessin = toile.getContext("2d");
  if (dessin) {
    dessin.fillStyle = "#cfe0f0";
    dessin.fillRect(0, 0, largeur, hauteur);
    dessin.strokeStyle = "#1f5f8b";
    dessin.lineWidth = 2;
    dessin.strokeRect(1, 1, largeur - 2, hauteur - 2);
    dessin.beginPath();
    dessin.moveTo(0, hauteur);
    dessin.lineTo(largeur / 2, hauteur / 4);
    dessin.lineTo(largeur, hauteur);
    dessin.stroke();
  }
  return toile.toDataURL("image/png");
}

const COURTE = () => `# Le cycle de l'eau

L'eau de la Terre ne se perd pas : elle change d'état et de lieu, sans cesse. Ce premier paragraphe est assez long pour passer à la ligne plusieurs fois, et montrer que chaque ligne de texte se pose sur une ligne du papier.

Un deuxième paragraphe, après Entrée : une ligne vide le sépare du premier, et une seule.

Une ligne, puis Maj+Entrée :\\
la suite reprend sur la ligne d'après, sans ligne vide.\\
Encore une, avec du **gras**, de l'*italique*, du \`code\`, un exposant m<sup>2</sup> et une formule $E = mc^2$.

## Les étapes

- l'évaporation des océans ;
- la condensation en nuages ;
- les précipitations, puis le ruissellement.

Le texte reprend une ligne après la liste.

| Étape | État de l'eau | Où |
| --- | --- | --- |
| Évaporation | gaz | au-dessus des océans |
| Condensation | liquide | dans les nuages |
| Précipitations | liquide ou solide | sur les continents |

Ce paragraphe suit le tableau : il commence sur la première ligne entière sous lui.

![Un schéma](${image(320, 137)})

Ce paragraphe suit l'image.

\`\`\`python
def cycle(eau):
    return condenser(evaporer(eau))
\`\`\`

Ce paragraphe suit le bloc de code.

$$
Q = m \\cdot L_v
$$

Après la formule, puis un trait :

---

> Une citation : son texte est sur les lignes, lui aussi,
> même quand elle passe à la ligne plusieurs fois parce qu'elle est longue, vraiment assez longue pour cela.

### Un titre de niveau 3

1. Premier point numéroté.
2. Deuxième point, avec une sous-liste :
   - un sous-point ;
   - un autre.

Dernier paragraphe de la fiche d'essai.
`;

const LONGUE = () => {
  const court = COURTE();
  const paragraphes = Array.from(
    { length: 14 },
    (_, rang) =>
      `Paragraphe ${rang + 1} de la fiche longue : assez de texte pour remplir plusieurs pages, et voir que la première ligne de chaque page se pose sur une ligne du papier, comme les autres.`,
  );
  const lignes = Array.from({ length: 30 }, (_, rang) => `| Ligne ${rang + 1} | valeur ${rang * 7} | remarque |`);
  return `${court}\n## Un long tableau\n\n| Nom | Valeur | Remarque |\n| --- | --- | --- |\n${lignes.join("\n")}\n\n${paragraphes.join("\n\n")}\n\n> ${paragraphes.slice(0, 6).join("\n>\n> ")}\n\n${court.replace("# Le cycle de l'eau", "# Encore une fois")}`;
};

const recus: DepuisEditeur[] = [];
type Ecoute = (evenement: { data: unknown }) => void;
const ecoutes: Ecoute[] = [];
const envoyer = (message: VersEditeur) => {
  for (const ecoute of ecoutes) ecoute({ data: message });
};

function demarrer(): void {
  const papier = choix("papier", ["uni", "petits-carreaux", "grands-carreaux", "lignes", "points", "millimetre", "aucun"], "grands-carreaux");
  const page = choix("page", ["sans-fin", "a4", "a5", "a6"], "sans-fin");
  const police = choix("police", ["application", "georgia", "manuscrite", "feutre", "code"], "application");
  const fiche = choix("fiche", ["courte", "longue", "vide"], "courte");
  const theme = choix("theme", ["clair", "sombre"], "clair");
  envoyer({
    kind: "theme",
    variables: {
      ...(theme === "sombre" ? SOMBRE : CLAIR),
      "--taille-lecture": `${nombre("taille", 17)}px`,
      "--interligne-lecture": "1.65",
      "--police-lecture": POLICES[police],
      "--couleur-forcee": "",
      ...(parametres.has("traits") ? { "--bordure": parametres.get("traits") ?? "" } : {}),
    },
  });
  if (page === "sans-fin") envoyer({ kind: "page", width: 0, height: 0, margin: 0 });
  else {
    const [largeur, hauteur] = FEUILLES[page];
    envoyer({ kind: "page", width: largeur * MM, height: hauteur * MM, margin: nombre("marge", 15) * MM });
  }
  envoyer({ kind: "paper", style: papier === "aucun" ? "" : papier, step: nombre("pas", PAS[papier] ?? 0) });
  envoyer({ kind: "load", text: fiche === "vide" ? "" : fiche === "longue" ? LONGUE() : COURTE(), folder: "", drawn: [] });
}

/**
 * De combien chaque bloc de texte s'écarte des lignes d'écriture : son haut, compté depuis le haut du texte de sa page,
 * devrait être un nombre entier de pas (un titre est décalé à l'œil : ce décalage est retiré). Rien sans lignes.
 */
function verifier(): { bloc: string; page: number; ecart: number }[] {
  const racine = document.documentElement;
  const pas = Number.parseFloat(racine.style.getPropertyValue("--pas"));
  const editeur = document.querySelector<HTMLElement>(".editeur-visuel");
  const cadre = document.getElementById("fiche");
  if (!(pas > 0) || !editeur || !cadre) return [];
  const style = getComputedStyle(cadre);
  const feuille = Number.parseFloat(style.getPropertyValue("--page-hauteur")) || 0;
  const marge = Number.parseFloat(style.getPropertyValue("--page-marge")) || 0;
  const origine = editeur.getBoundingClientRect().top + marge;
  const blocs = editeur.querySelectorAll<HTMLElement>(":is(p, h1, h2, h3, h4, h5, h6):not(:is(table, .bloc-code) *)");
  return [...blocs].map((bloc) => {
    const levee = /^H\d$/.test(bloc.tagName) ? Number.parseFloat(getComputedStyle(bloc).top) || 0 : 0;
    const haut = bloc.getBoundingClientRect().top - levee - origine;
    const page = feuille > 0 ? Math.max(0, Math.floor((haut + 0.5) / (feuille + 16))) : 0;
    const dans = haut - page * (feuille + 16);
    const ecart = dans - Math.round(dans / pas) * pas;
    return { bloc: `${bloc.tagName.toLowerCase()} « ${(bloc.textContent ?? "").slice(0, 24)} »`, page: page + 1, ecart: Math.round(ecart * 1000) / 1000 };
  });
}

const hote = {
  postMessage(message: unknown) {
    recus.push(message as DepuisEditeur);
    if ((message as DepuisEditeur).kind === "ready") demarrer();
  },
  addEventListener(_type: "message", ecoute: Ecoute) {
    ecoutes.push(ecoute);
  },
};

const fenetre = window as unknown as { chrome?: Record<string, unknown>; essai: unknown };
fenetre.chrome = Object.assign(fenetre.chrome ?? {}, { webview: hote });
fenetre.essai = { envoyer, recus, verifier };

// L'éditeur parle à son hôte dès qu'il se charge : il ne se charge qu'une fois l'hôte en place.
void import("../src/main");
