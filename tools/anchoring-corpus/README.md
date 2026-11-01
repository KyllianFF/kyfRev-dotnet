# Corpus d'ancrage

Produit le corpus de référence de l'ancrage, « Markdown → texte affiché → positions », depuis
kyfRev 0.x. La lecture native du jalon J7 (`KyfRev.Rendering`) devra le reproduire à
l'identique avant d'écrire la moindre ancre (plan de refonte, §2.6 et §4.5, critère T12).

## Ce qui est calculé

Les fonctions de la 0.x elles-mêmes, et non une réécriture : `rendre` (`src/lib/rendu.ts` :
markdown-it, notes, KaTeX, balises `<u>`, `<sup>` et `<sub>`, DOMPurify), puis `carteDu`, `rogner`,
`ancreDe`, `sectionDe`, `ligneSourceDe` et `ancrer` (`src/lib/ancrage.ts`), appelées comme le fait
la lecture d'une fiche (`src/composants/Fiche.svelte` : rendu, titre répété, ancrage, puis dessin des
diagrammes). Elles s'exécutent dans Microsoft Edge, sans fenêtre : c'est le moteur de WebView2, donc
celui de la 0.x.

- `tests/fixtures/ancrage/projection.json` : pour chaque document, son HTML (formules réduites à
  leur source), le texte affiché, ses morceaux (nœuds texte et formules, positions en unités UTF-16)
  et des passages : la sélection, puis l'ancre enregistrée (texte exact, 32 caractères de contexte de
  chaque côté, section, ligne source).
- `tests/fixtures/ancrage/reancrage.json` : des passages ancrés dans un document, puis retrouvés
  après sa modification, depuis l'état « ancrée » et depuis l'état « introuvable ».

- `tests/fixtures/ancrage/analyse.json` : les mêmes projections, sans le HTML, pour plus de mille cas
  limites de l'analyse Markdown (`analyse.mjs`, un groupe par construction). Le critère T12 reste le
  corpus d'origine ; ces cas confrontent chaque branche de `KyfRev.Rendering` à la 0.x.

Les documents (`cas.mjs`) : les quatre fiches de démonstration de la 0.x, une trentaine de cas
ciblés (chaque construction Markdown reconnue, Unicode, CRLF, caractères nuls…) et 200 documents
tirés au hasard ; les modifications : seize cas ciblés (déplacement, doublon, faute corrigée, mots
ajoutés, suppression, mise en forme, formule, CRLF…) et 100 documents modifiés au hasard. La
génération est reproductible : deux exécutions donnent les mêmes fichiers, à l'octet.

## Ce que montre le corpus (utile pour J7)

- Les sauts de ligne de mise en forme du HTML de markdown-it font partie du texte : une liste
  ajoute des lignes vides entre ses éléments, un saut dur (`<br>`) donne deux sauts de ligne.
- Le titre répété en tête de fiche reste dans le texte ; il est seulement ignoré par `sectionDe`.
- Les blocs Mermaid et réseau sont exclus, mais le saut de ligne qui les suit reste. Les blocs
  d'extension (```` ```quiz ````…) comptent comme du code : l'ancrage a lieu avant leur dessin.
- Une formule devient `$source$` ou `$$source$$`, source sans ses espaces de bord.
- Les entités sont décodées ; le HTML brut d'une fiche reste du texte.
- Les blocs de code n'ont pas de ligne source (`ligneSource` vaut `null`).

## Régénérer

Prérequis : le dépôt kyfRev 0.x cloné à côté de ce dépôt (ou désigné par la variable
`KYFREV_0X`), ses dépendances installées (`pnpm install`), Node 24 et Microsoft Edge.

```powershell
node tools/anchoring-corpus/generer.mjs
```

Seul le champ `source` change quand la 0.x avance : le corpus d'origine n'a pas à être réécrit pour
autant. Le commit de la 0.x, la version d'Edge et celles des bibliothèques de rendu sont inscrits dans
chaque fichier (`source`). Les tests `AnchoringCorpusTests` vérifient que le corpus reste cohérent
(morceaux, contexte, sélections rognées, ré-ancrages qui ne rattachent jamais un autre texte).
