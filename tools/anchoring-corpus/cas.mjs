// Cas du corpus d'ancrage : fiches de démonstration de la 0.x, cas ciblés (chaque construction
// Markdown que le rendu connaît), documents tirés au hasard, et modifications pour le ré-ancrage.
// Les caractères délicats sont écrits par leur code, pour que ce fichier reste lisible.

const c = (...codes) => String.fromCodePoint(...codes);
const NBSP = c(0xa0);
const COMBINANT = c(0x301);
const ZWJ = c(0x200d);
const EMOJI = c(0x1f9ec); // ADN
const SCIENTIFIQUE = `${c(0x1f469)}${ZWJ}${c(0x1f52c)}`;
const ARABE = c(0x645, 0x631, 0x62d, 0x628, 0x627);
const CJK = c(0x7d30, 0x80de, 0x5206, 0x88c2);

/** Fiches de démonstration de la 0.x (src/lib/demo.ts). */
const REVOLUTION = `
# Les causes de la Révolution française

## Causes sociales

La société d'Ancien Régime est divisée en trois ordres : le **clergé**, la **noblesse** et le **tiers état**.

> Le tiers état, c'est tout ; il n'a rien été ; il demande à être quelque chose.

| Ordre | Part de la population | Privilèges |
|---|---|---|
| Clergé | ~0,5 % | exemption d'impôts |
| Noblesse | ~1,5 % | droits seigneuriaux |
| Tiers état | ~98 % | aucun |

## Causes financières

La dette de l'État explose : $\\text{intérêts} \\approx \\tfrac{1}{2}\\,\\text{dépenses}$ en 1788[^1].

\`\`\`mermaid
flowchart LR
  A[Crise financière] --> B[États généraux]
  B --> C[Serment du Jeu de paume]
  C --> D[Prise de la Bastille]
\`\`\`

\`\`\`quiz
Q: Qui convoque les états généraux en 1789 ?
R: Louis XVI
Q: Combien d'ordres compte la société d'Ancien Régime ?
R: Trois : le clergé, la noblesse et le tiers état
\`\`\`

\`\`\`chronologie
5 mai 1789 : Ouverture des états généraux
20 juin 1789 : Serment du Jeu de paume
14 juillet 1789 : Prise de la Bastille
\`\`\`

\`\`\`python
def ordres():
    return ["clergé", "noblesse", "tiers état"]
\`\`\`

Voir aussi [la fiche sur l'Ancien Régime](Ancien%20Régime.md), [Les lois de Newton](<../Physique/Les lois de Newton.md>) pour la méthode, et [un article](https://fr.wikipedia.org/wiki/Révolution_française).

[^1]: Ordre de grandeur couramment cité.
`;

const THALES = `
# Les théorèmes de Thalès et Pythagore

## Thalès

Si deux droites parallèles coupent deux sécantes, les longueurs sont proportionnelles :
$\\frac{AM}{AB} = \\frac{AN}{AC} = \\frac{MN}{BC}$.

## Pythagore

Dans un triangle rectangle, le carré de l'hypoténuse est égal à la somme des carrés des deux autres côtés :
$$a^2 + b^2 = c^2$$
`;

const MITOSE = `
# Mitose et méiose

## La mitose

La prophase est la première étape de la mitose : les chromosomes se condensent.

1. Prophase
2. Métaphase
3. Anaphase
4. Télophase

## La méiose

Deux divisions successives produisent **quatre cellules haploïdes** ($n$ chromosomes au lieu de $2n$).
`;

const NEWTON = `
# Les lois de Newton

## Principe fondamental de la dynamique

F = m·a : la somme des forces est égale à la masse fois l'accélération.

$$\\sum \\vec{F} = m\\,\\vec{a}$$

## Action et réaction

Deux corps en interaction exercent l'un sur l'autre des forces opposées.
`;

const auto = { auto: true };

/** Documents dont on relève le texte affiché et des passages. */
export const documents = [
  { nom: "demo-revolution", titre: "Révolution française : les causes", premiereLigneCorps: 6, corps: REVOLUTION, passages: { auto: true, exacts: ["divisée en trois ordres", "tiers état", "Louis XVI", "dépenses", "Ordre de grandeur"] } },
  { nom: "demo-thales", titre: "Les théorèmes de Thalès et Pythagore", premiereLigneCorps: 5, corps: THALES, passages: { auto: true, exacts: ["proportionnelles", "a^2 + b^2 = c^2"] } },
  { nom: "demo-mitose", titre: "Mitose et méiose", premiereLigneCorps: 5, corps: MITOSE, passages: { auto: true, exacts: ["Métaphase", "quatre cellules haploïdes"] } },
  { nom: "demo-newton", titre: "Les lois de Newton", premiereLigneCorps: 5, corps: NEWTON, passages: { auto: true, exacts: ["m·a", "forces opposées"] } },
  { nom: "vide", titre: "Vide", premiereLigneCorps: 1, corps: "", passages: auto },
  { nom: "espaces-seuls", titre: "Espaces", premiereLigneCorps: 1, corps: "   \n\n\t\n", passages: auto },
  { nom: "titre-repete", titre: "Mitose", premiereLigneCorps: 4, corps: "# Mitose\n\nTexte.\n\n## Phases\n\nProphase.\n\n# Autre titre\n\nSuite.\n", passages: { auto: true, exacts: ["Prophase", "Suite"] } },
  { nom: "titre-different", titre: "Mitose", premiereLigneCorps: 4, corps: "# Mitose cellulaire\n\nTexte.\n", passages: auto },
  { nom: "titres-setext", titre: "Setext", premiereLigneCorps: 1, corps: "Grand titre\n===========\n\nSous-titre\n----------\n\nTexte sous le sous-titre.\n\n### Niveau 3\n\n#### Niveau 4\n\n##### Niveau 5\n\n###### Niveau 6\n\nFin.\n", passages: { auto: true, exacts: ["Texte sous", "Fin"] } },
  { nom: "titres-sauts", titre: "Sauts", premiereLigneCorps: 1, corps: "### Commence au niveau 3\n\nA.\n\n# Niveau 1\n\nB.\n\n#### Niveau 4\n\nC.\n\n## Niveau 2\n\nD.\n", passages: { auto: true, exacts: ["A.", "B.", "C.", "D."] } },
  { nom: "emphases", titre: "Emphases", premiereLigneCorps: 1, corps: "Du *italique*, du **gras**, du ***gras italique***, du ~~barré~~, du `code`, et du _souligné à la Markdown_.\n", passages: { auto: true, exacts: ["gras italique", "code"] } },
  { nom: "balises-autorisees", titre: "Balises", premiereLigneCorps: 1, corps: "Un <u>mot souligné</u>, x<sup>2</sup>, H<sub>2</sub>O, <u>sans fermante et <b>gras</b> <script>alert(1)</script>.\n", passages: { auto: true, exacts: ["mot souligné", "<b>gras</b>", "alert(1)"] } },
  { nom: "html-brut", titre: "HTML", premiereLigneCorps: 1, corps: "<div class=\"x\">bloc HTML</div>\n\n<!-- commentaire -->\n\nTexte <span>en ligne</span> &amp; &lt;b&gt; &copy; &#233; &#x1F9EC; &nbsp;fin.\n", passages: { auto: true, exacts: ["bloc HTML", "<b>", "fin."] } },
  { nom: "sauts-de-ligne", titre: "Sauts", premiereLigneCorps: 1, corps: "Ligne douce\nsuite douce.\n\nLigne dure  \nsuite dure.\n\nBarre oblique\\\nsuite.\n", passages: { auto: true, exacts: ["douce\nsuite", "suite dure", "oblique"] } },
  { nom: "listes", titre: "Listes", premiereLigneCorps: 1, corps: "- un\n- deux\n  - deux point un\n  - deux point deux\n- trois\n\n1. premier\n2. second\n\n5. cinq\n6. six\n\n- ample\n\n- liste\n\n* étoile\n+ plus\n\n- [ ] tâche\n- [x] faite\n", passages: { auto: true, exacts: ["deux point un", "second", "ample", "tâche"] } },
  { nom: "listes-paragraphes", titre: "Listes", premiereLigneCorps: 1, corps: "1. Premier point\n\n   Avec un second paragraphe.\n\n   > Une citation dans la liste.\n\n2. Deuxième point\n   ```\n   code dans la liste\n   ```\n", passages: { auto: true, exacts: ["second paragraphe", "citation dans", "code dans"] } },
  { nom: "citations", titre: "Citations", premiereLigneCorps: 1, corps: "> Citation simple\n> sur deux lignes.\n\n> > Citation imbriquée.\n>\n> - avec une liste\n\n> # Titre dans une citation\n", passages: { auto: true, exacts: ["deux lignes", "imbriquée", "Titre dans"] } },
  { nom: "code", titre: "Code", premiereLigneCorps: 1, corps: "```python\ndef f(x):\n    return x * 2  # double\n```\n\n~~~js\nconst a = `gabarit ${1}`;\n~~~\n\n    code indenté\n    deuxième ligne\n\n```\nsans langue\n```\n\n```langue-inconnue\nbrut\n```\n\nEn ligne : `a < b` et ``double `accent` grave``.\n", passages: { auto: true, exacts: ["return x * 2", "gabarit", "code indenté", "a < b"] } },
  { nom: "diagrammes-exclus", titre: "Diagrammes", premiereLigneCorps: 1, corps: "Avant.\n\n```mermaid\ngraph TD; A-->B\n```\n\nEntre.\n\n```reseau\nschema: s_01JQZ8S7ZK4V3H2N8M6X5W9PQR\n```\n\n```quiz\nQ: Question ?\nR: Réponse\n```\n\nAprès.\n", passages: { auto: true, exacts: ["Entre", "Question ?", "Après"] } },
  { nom: "tableaux", titre: "Tableaux", premiereLigneCorps: 1, corps: "| Gauche | Centre | Droite |\n|:---|:---:|---:|\n| a | **b** | `c` |\n| d | | f |\n\nTexte après.\n\n| Seule |\n|---|\n", passages: { auto: true, exacts: ["Centre", "b", "Texte après"] } },
  { nom: "liens", titre: "Liens", premiereLigneCorps: 1, corps: "[Fiche](Autre%20fiche.md), [chevrons](<Autre fiche.md>), [espaces](Autre fiche.md), [externe](https://exemple.org), [courriel](mailto:a@exemple.org), [ancre](#section), [cassé](absent.png), [hors](../../hors.md), <https://exemple.org/auto>, https://exemple.org/linkify et www.exemple.org.\n\n[réf]: https://exemple.org/reference\n\nUn [lien de référence][réf].\n", passages: { auto: true, exacts: ["externe", "linkify", "lien de référence"] } },
  { nom: "images", titre: "Images", premiereLigneCorps: 1, corps: "Avant ![carte](carte.png) après.\n\n![[schéma.png|300]] et ![espace](mon image.png)\n\n![distante](https://exemple.org/x.png) ![donnée](data:image/png;base64,iVBORw0KGgo=) ![hors](../../../x.png)\n\nFin.\n", passages: { auto: true, exacts: ["Avant", "après", "Fin"] } },
  { nom: "notes", titre: "Notes", premiereLigneCorps: 1, corps: "Une affirmation[^a] et une autre[^b].\n\n[^a]: Première note.\n[^b]: Seconde note,\n    sur deux lignes.\n\nUne note en ligne^[écrite sur place].\n", passages: { auto: true, exacts: ["affirmation", "Première note", "écrite sur place"] } },
  { nom: "formules", titre: "Formules", premiereLigneCorps: 1, corps: "En ligne $E = mc^2$ et $ a < b $ et \\$ pas une formule.\n\n$$\n\\int_0^1 x\\,dx = \\frac{1}{2}\n$$\n\nBloc sur une ligne $$x^2$$ et $\\invalide{$ invalide.\n\n$$\n\\begin{aligned} a &= b \\\\ c &= d \\end{aligned}\n$$\n", passages: { auto: true, exacts: ["E = mc^2", "\\int_0^1", "invalide"] } },
  { nom: "separateurs", titre: "Séparateurs", premiereLigneCorps: 1, corps: "Avant.\n\n---\n\nEntre.\n\n***\n\n___\n\nAprès.\n", passages: auto },
  { nom: "unicode", titre: "Unicode", premiereLigneCorps: 1, corps: `${EMOJI} ADN et ${SCIENTIFIQUE} scientifique.\n\nAccent combinant : e${COMBINANT}te et café.\n\n${CJK} et ${ARABE}.\n\nEspace${NBSP}insécable et\ttabulation.\n\n${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI} milieu ${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}${EMOJI}\n`, passages: { auto: true, exacts: ["ADN", "scientifique", `e${COMBINANT}te`, CJK, "milieu"] } },
  { nom: "crlf", titre: "CRLF", premiereLigneCorps: 1, corps: "# Titre\r\n\r\nLigne un\r\nligne deux.\r\n\r\n- a\r\n- b\r\n\r\n```\r\ncode\r\n```\r\n", passages: { auto: true, exacts: ["ligne deux", "code"] } },
  { nom: "caracteres-nuls", titre: "Nuls", premiereLigneCorps: 1, corps: `Avant${c(0)}après et ${c(0xfeff)}BOM au milieu.\n`, passages: auto },
  { nom: "echappements", titre: "Échappements", premiereLigneCorps: 1, corps: "\\*pas italique\\*, \\# pas un titre, \\[pas un lien\\](x), 1\\. pas une liste, \\\\ barre.\n", passages: { auto: true, exacts: ["pas italique", "barre"] } },
  { nom: "long", titre: "Long", premiereLigneCorps: 1, corps: Array.from({ length: 40 }, (_, i) => `Paragraphe ${i + 1} : la même phrase revient, encore et encore, avec un numéro ${i + 1}.`).join("\n\n"), passages: { auto: true, exacts: ["Paragraphe 17", "encore et encore"] } },
];

/** Modifications d'un document : chaque passage annoté avant est ré-ancré dans le texte d'après. */
export const modifications = [
  { nom: "inchange", titre: "Révolution", avant: REVOLUTION, apres: REVOLUTION, exacts: ["divisée en trois ordres", "Louis XVI", "tiers état"] },
  { nom: "paragraphe-ajoute-avant", titre: "Révolution", avant: REVOLUTION, apres: REVOLUTION.replace("## Causes sociales", "## Introduction\n\nUn paragraphe ajouté plus tard.\n\n## Causes sociales"), exacts: ["divisée en trois ordres", "Louis XVI"] },
  { nom: "passage-deplace", titre: "Mitose", avant: MITOSE, apres: MITOSE.replace("La prophase est la première étape de la mitose : les chromosomes se condensent.\n\n", "").replace("## La méiose", "## La méiose\n\nLa prophase est la première étape de la mitose : les chromosomes se condensent."), exacts: ["les chromosomes se condensent", "Métaphase"] },
  { nom: "passage-duplique-contexte-different", titre: "Doublon", avant: "Le clergé paie peu.\n\nLa noblesse aussi.\n", apres: "Le clergé paie peu.\n\nAilleurs : clergé.\n\nLa noblesse aussi.\n", exacts: ["clergé"] },
  { nom: "passage-duplique-meme-contexte", titre: "Doublon", avant: "Un mot ici.\n\nAutre chose.\n", apres: "Un mot ici.\n\nUn mot ici.\n", exacts: ["mot"] },
  { nom: "position-change-meme-texte", titre: "Position", avant: "Alpha bêta gamma.\n", apres: "Préambule.\n\nAlpha bêta gamma.\n", exacts: ["bêta"] },
  { nom: "faute-corrigee", titre: "Faute", avant: "La dette de l'Etat explose à la veille de 1789.\n", apres: "La dette de l'État explose à la veille de 1789.\n", exacts: ["dette de l'Etat explose"] },
  { nom: "mots-ajoutes", titre: "Ajout", avant: "Les intérêts représentent la moitié des dépenses en 1788.\n", apres: "Les intérêts représentent la moitié de toutes les dépenses de l'État en 1788.\n", exacts: ["la moitié des dépenses en 1788"] },
  { nom: "passage-supprime", titre: "Suppression", avant: "Garder ceci. Retirer cette phrase entière. Garder cela.\n", apres: "Garder ceci. Garder cela.\n", exacts: ["Retirer cette phrase entière"] },
  { nom: "passage-court-modifie", titre: "Court", avant: "Le chat dort.\n", apres: "Le chien dort.\n", exacts: ["chat"] },
  { nom: "mise-en-forme-ajoutee", titre: "Forme", avant: "La noblesse a des privilèges.\n", apres: "La **noblesse** a des *privilèges*.\n", exacts: ["noblesse a des privilèges"] },
  { nom: "liste-devenue-paragraphe", titre: "Liste", avant: "- Prophase\n- Métaphase\n", apres: "Prophase, Métaphase.\n", exacts: ["Prophase", "Métaphase"] },
  { nom: "formule-modifiee", titre: "Formule", avant: "Énergie $E = mc^2$ connue.\n", apres: "Énergie $E = m c^{2}$ connue.\n", exacts: ["E = mc^2", "connue"] },
  { nom: "fins-de-ligne-crlf", titre: "CRLF", avant: MITOSE, apres: MITOSE.replaceAll("\n", "\r\n"), exacts: ["Métaphase", "quatre cellules haploïdes"] },
  { nom: "emoji-en-bordure", titre: "Emoji", avant: `${EMOJI.repeat(20)} cible ${EMOJI.repeat(20)}\n`, apres: `Ajout. ${EMOJI.repeat(20)} cible ${EMOJI.repeat(20)}\n`, exacts: ["cible"] },
  { nom: "texte-reecrit", titre: "Réécrit", avant: "La Révolution commence en 1789 avec la réunion des états généraux.\n", apres: "En 1789, les états généraux se réunissent : la Révolution commence.\n", exacts: ["réunion des états généraux", "La Révolution commence en 1789"] },
];

// --- Documents tirés au hasard ------------------------------------------------------------------

/** Générateur reproductible (mulberry32). */
export function hasard(graine) {
  let etat = graine >>> 0;
  return () => {
    etat = (etat + 0x6d2b79f5) >>> 0;
    let t = etat;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const FRAGMENTS = [
  (n) => `# Titre ${n}`,
  (n) => `## Partie ${n}`,
  (n) => `### Sous-partie ${n} avec \`code\``,
  (n) => `Titre setext ${n}\n---`,
  (n) => `Paragraphe ${n} avec du *texte* et du **gras**, puis une suite assez longue pour le contexte.`,
  (n) => `Ligne ${n}  \ncoupée par un saut dur.`,
  (n) => `Ligne ${n}\ncoupée par un saut doux.`,
  (n) => `Lien vers [une fiche](Fiche ${n}.md), [le web](https://exemple.org/${n}) et https://exemple.org/auto${n}.`,
  (n) => `Formule $x_${n} + y^2$ dans la ligne.`,
  (n) => `$$\n\\sum_{i=1}^{${n}} i\n$$`,
  (n) => `Note[^n${n}] dans le texte.\n\n[^n${n}]: Texte de la note ${n}.`,
  (n) => `- élément ${n}\n- autre élément\n  - imbriqué`,
  (n) => `1. étape ${n}\n2. étape suivante`,
  (n) => `> Citation ${n}\n> qui continue.`,
  (n) => `\`\`\`python\nprint(${n})\n\`\`\``,
  () => "```mermaid\ngraph LR; A-->B\n```",
  (n) => `\`\`\`quiz\nQ: Question ${n} ?\nR: Réponse\n\`\`\``,
  (n) => `    indenté ${n}`,
  (n) => `| A${n} | B |\n|---|---|\n| 1 | 2 |`,
  () => "---",
  (n) => `Image ![figure ${n}](figure${n}.png) dans le texte.`,
  (n) => `![[intégrée${n}.png|200]]`,
  (n) => `Balises <u>souligné ${n}</u>, H<sub>2</sub>O et x<sup>${n}</sup>.`,
  (n) => `HTML <b>${n}</b> &amp; entités &eacute;.`,
  (n) => `${EMOJI} emoji ${n} et accent e${COMBINANT} combiné.`,
  (n) => `${CJK} ${n} ${ARABE}`,
  (n) => `Doublon répété. Doublon répété. ${n}`,
];

/** Documents tirés au hasard (3 à 8 fragments). */
export function documentsAuHasard(nombre, graine) {
  const tirer = hasard(graine);
  const liste = [];
  for (let i = 0; i < nombre; i++) {
    const morceaux = [];
    const taille = 3 + Math.floor(tirer() * 6);
    for (let j = 0; j < taille; j++) {
      const fragment = FRAGMENTS[Math.floor(tirer() * FRAGMENTS.length)];
      morceaux.push(fragment(Math.floor(tirer() * 100)));
    }
    let corps = morceaux.map((m, j) => (j === 0 ? m : (tirer() < 0.8 ? "\n\n" : "\n") + m)).join("") + "\n";
    if (tirer() < 0.15) corps = corps.replaceAll("\n", "\r\n");
    liste.push({ nom: `hasard-${String(i + 1).padStart(3, "0")}`, titre: `Titre ${Math.floor(tirer() * 100)}`, premiereLigneCorps: 1 + Math.floor(tirer() * 6), corps, passages: auto });
  }
  return liste;
}

/** Un document modifié au hasard : un fragment retiré, ajouté ou échangé, une lettre changée. */
export function modificationsAuHasard(documentsTires, graine) {
  const tirer = hasard(graine);
  return documentsTires.map((document) => {
    const blocs = document.corps.split(/\n\n|\r\n\r\n/);
    const choix = Math.floor(tirer() * 4);
    const i = Math.floor(tirer() * blocs.length);
    if (choix === 0 && blocs.length > 1) blocs.splice(i, 1);
    else if (choix === 1) blocs.splice(i, 0, FRAGMENTS[Math.floor(tirer() * FRAGMENTS.length)](Math.floor(tirer() * 100)));
    else if (choix === 2 && blocs.length > 1) {
      const j = Math.floor(tirer() * blocs.length);
      [blocs[i], blocs[j]] = [blocs[j], blocs[i]];
    } else {
      // Par caractères, pas par unités UTF-16 : un emoji n'est jamais coupé en deux.
      const caracteres = Array.from(blocs[i] ?? "");
      const k = Math.floor(tirer() * caracteres.length);
      caracteres.splice(k, 1, ...(tirer() < 0.5 ? ["x"] : []));
      blocs[i] = caracteres.join("");
    }
    return { nom: `${document.nom}-modifie`, titre: document.titre, avant: document.corps, apres: blocs.join("\n\n"), auto: true };
  });
}
