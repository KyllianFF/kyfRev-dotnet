# Écrire une extension pour kyfRev

Une extension ajoute à kyfRev une vue dans le menu, des commandes, des entrées au clic droit, un encart sur « Aujourd'hui » ou
le dessin de blocs dans les fiches. Elle s'écrit en JavaScript, sans rien compiler ni installer : un dossier, un manifeste,
un script.

Son code tourne isolé, dans un moteur web sans réseau. Il ne voit rien de l'application : il demande, par l'objet `kyfrev`, et
kyfRev vérifie chaque demande sur les permissions accordées. Ce qu'il affiche passe par une liste blanche et devient des
éléments natifs de l'application, aux couleurs du thème.

Le plus court chemin : Paramètres, onglet « Modules et extensions », **Créer une extension…**. kyfRev crée un dossier prêt à
modifier, l'installe après confirmation, et allume le mode développeur. Les quatre extensions du dossier
[`extensions/`](../../extensions/) sont des exemples complets.

## Le dossier

| Fichier | Rôle |
| --- | --- |
| `extension.json` | Le manifeste : qui elle est, ce qu'elle ajoute, ce qu'elle demande. |
| `extension.js` | Son code (un autre nom se donne par `entree`). |
| une icône `.svg` | L'icône de son entrée du menu, facultative. |
| `kyfrev.d.ts`, `jsconfig.json` | Pour l'éditeur seulement : VS Code complète et vérifie les appels. |

200 fichiers et 10 Mo au plus. À l'installation, kyfRev copie le dossier sur ce poste ; une version installée auparavant est
rangée, jamais effacée.

## Le manifeste

```json
{
  "api": 2,
  "id": "mon-extension",
  "nom": "Mon extension",
  "version": "1.0.0",
  "description": "Ce qu'elle fait, en une ou deux phrases.",
  "auteur": "Votre nom",
  "icone": "icone.svg",
  "permissions": ["fiches:lecture", "selection", "stockage"],
  "blocs": ["frise"],
  "vue": { "titre": "Mon extension", "compteur": true },
  "commandes": [{ "id": "nouveau", "titre": "Nouvelle chose" }],
  "menus": {
    "fiche": [{ "commande": "nouveau", "titre": "Créer depuis cette fiche" }],
    "dossier": [{ "commande": "nouveau", "titre": "Créer depuis ce dossier" }],
    "selection": [{ "commande": "nouveau", "titre": "Ajouter à mon extension" }]
  },
  "encart": { "titre": "Mon extension" }
}
```

- `api` : 2 pour ce contrat. Sans lui, c'est le contrat de la 0.x : des blocs et une vue, rien d'autre.
- `id` : 2 à 40 minuscules, chiffres ou tirets. C'est lui qui fait qu'une nouvelle version remplace l'ancienne.
- `nom` (60 caractères), `version` (20 octets), `description` (300 caractères), `auteur` : ce que l'installation montre.
- `icone` : un SVG du dossier, fait de tracés simples (`path`, `line`, `polyline`, `polygon`, `rect`, `circle`), en trait
  (`fill="none"`) ou plein. Sa couleur suit le thème.
- `blocs` : les langages de bloc qu'elle dessine (10 au plus) ; `mermaid`, `reseau`, `math` et les autres langages de kyfRev
  sont réservés.
- `vue` : son entrée du menu ; `compteur` affiche le nombre que rend `kyfrev.compteur`.
- `commandes` : 20 au plus, un identifiant en minuscules et un titre de 60 caractères. Elles paraissent dans la recherche
  (Ctrl+K), sous la forme « Nom : Titre ».
- `menus` : des entrées au clic droit d'une fiche, d'un dossier ou d'un texte sélectionné en lecture (cette dernière zone
  demande la permission `selection`), 10 en tout, chacune lançant une commande.
- `encart` : son encart sur « Aujourd'hui ».

## Les permissions

| Permission | Ce qu'elle permet | Poids à l'installation |
| --- | --- | --- |
| `fiches:lecture` | Lister les fiches, lire leur texte, en ouvrir une à un passage | alerte |
| `selection` | Voir le texte sélectionné quand on choisit son entrée, demander une sélection guidée | alerte |
| `fiches:creation` | Créer des fiches, changer leurs étiquettes | alerte |
| `fiches:ecriture` | Remplacer le texte d'une fiche ; le texte d'avant reste dans ses versions | alerte rouge |
| `stockage` | Garder ses propres données dans le coffre, 1 Mo au plus | aucune |

L'installation montre chaque permission, et une mise à jour marque celles qui sont nouvelles. L'utilisateur peut en retirer une
dans les Paramètres : la demande est alors refusée, avec une erreur qui dit laquelle manque. Aucune extension ne peut accéder à
Internet, lire un fichier du poste, lancer un programme ni supprimer une fiche.

## L'objet `kyfrev`

Tous les appels à kyfRev rendent une promesse. Les types exacts sont dans [`kyfrev.d.ts`](kyfrev.d.ts).

### Ce qu'elle déclare

```js
kyfrev.vue(async ({ theme }) => "<h1>Ma vue</h1>");            // l'écran de sa vue
kyfrev.action("compter", async (valeur, donnees) => { … });    // un bouton, un champ, un formulaire de la vue
kyfrev.commande("nouveau", async (contexte) => { … });         // une commande, depuis Ctrl+K ou le clic droit
kyfrev.compteur(async () => 3);                                 // le nombre près de son entrée du menu
kyfrev.encart(async () => ({ texte: "3 à revoir", detail: "…" })); // son encart sur « Aujourd'hui », ou null
kyfrev.bloc("frise", async (source, { theme }) => "<ol>…</ol>"); // le dessin d'un bloc ```frise d'une fiche
```

Le contexte d'une commande dit sur quoi elle porte : rien depuis Ctrl+K ; `id`, `titre`, `dossier` depuis le clic droit d'une
fiche ; `dossier` depuis celui d'un dossier ; `id`, `titre`, `dossier`, `texte`, `debut`, `fin` depuis celui d'un texte
sélectionné. Après une action ou une commande, la vue se redessine ; `kyfrev.rafraichir()` la redessine à tout moment, et
`kyfrev.ouvrirVue()` l'ouvre.

### Ce qu'elle demande

```js
const fiches = await kyfrev.fiches.lister();            // [{ id, titre, dossier, etiquettes, archivee }]
const { texte } = await kyfrev.fiches.lire(id);         // le Markdown de la fiche
await kyfrev.fiches.ouvrir(id, { debut, fin });         // l'ouvre en lecture, le passage sélectionné
const pris = await kyfrev.selection.demander("Sélectionnez la question", { fiche: id });
// pris.choix : "selection" (pris.texte, pris.fiche, pris.debut, pris.fin), "saisie" (écrit à la main), "passer", "terminer"
const nouvelle = await kyfrev.fiches.creer("Titre", { dossier: "Cours", texte: "…", etiquettes: ["a"] });
await kyfrev.fiches.etiqueter(id, ["a", "b"]);
await kyfrev.fiches.ecrire(id, "Le nouveau texte");
const donnees = await kyfrev.stockage.lire();           // ses données, ou null
await kyfrev.stockage.ecrire({ … });
```

La sélection guidée montre une barre au-dessus de la fiche : la consigne, « Prendre la sélection », un champ pour écrire ce qui
manque, « Passer » et « Terminer ». Le temps ne court pas pendant que l'utilisateur répond : une extension peut enchaîner les
demandes, comme le Quiz qui prend une question puis sa réponse, à la chaîne.

## Ce qu'une vue peut afficher

Une vue, un bloc, rendent du HTML simple. kyfRev garde :

- les titres `h1` à `h6`, les paragraphes, `strong`, `em`, `code`, `pre`, `blockquote`, `hr`, `br` ;
- les listes `ul`, `ol`, `li`, et `details` / `summary` ;
- les tableaux `table`, `thead`, `tbody`, `tr`, `th`, `td` ;
- les boutons, les champs `input` (texte, nombre, date, case à cocher, bouton radio), `select`, `textarea`, `label`,
  `fieldset` et les formulaires `form` ;
- les barres `progress` ;
- les classes `principal` et `discret` (boutons), `secondaire` (texte), `actions` (une rangée de boutons), `carte`, `pastille`,
  `question`, `reponse`, `frise`, `quiz`, `titre-extension`.

Tout le reste disparaît : style, lien, image, script, iframe. Les couleurs et les polices sont celles de l'application.

Un élément avec `data-action="nom"` appelle `kyfrev.action("nom", …)` : un bouton au clic, avec son `data-valeur` ; un champ
quand on appuie sur Entrée ou qu'on le quitte, avec son texte ; une case, avec son état. Un `form data-action="nom"` envoie
d'un coup ses champs nommés (`name`) : un objet `{ nom: texte, case: true/false, radio: valeur }`. Les autres attributs
`data-…` arrivent dans le second argument de l'action.

Échappez toujours le texte qui vient des fiches ou de l'utilisateur avant de le mettre dans le HTML (voir la fonction
`echapper` des exemples).

## Les limites

- Une demande a 10 secondes pour aboutir, sauf pendant que l'utilisateur répond à une sélection guidée.
- Les données gardées : 1 Mo. Le texte écrit dans une fiche : 1 Mo ; un titre : 200 caractères ; 50 étiquettes de
  100 caractères.
- 60 écritures par minute (créations, étiquettes, textes).
- Le compteur va jusqu'à 999 ; le texte et le détail d'un encart, 300 caractères chacun.

Chaque écriture dans une fiche est notée au journal de l'extension, dans le coffre ; l'utilisateur le voit sous la liste des
extensions, dans les Paramètres.

## Essayer, corriger, partager

Avec le mode développeur (Paramètres, « Modules et extensions »), chaque extension montre le dossier d'où elle vient,
**Recharger**, qui reprend ses fichiers, et ses dernières erreurs : ce qui a échoué (sa vue, une action, une commande…) et
pourquoi. Une permission nouvelle passe par la confirmation, comme une mise à jour.

Pour la partager, donnez son dossier : on l'installe par « Installer depuis un dossier… ».
