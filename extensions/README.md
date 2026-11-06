# Extensions de kyfRev

Les extensions écrites pour kyfRev, au contrat 2 (`"api": 2` dans `extension.json`). Chacune est un dossier :
`extension.json` dit ce qu'elle ajoute et ce qu'elle demande, `extension.js` est son code, et son icône est un SVG.

Pour en installer une : Paramètres, onglet « Modules et extensions », « Installer depuis un dossier… », puis le dossier
de l'extension. kyfRev montre ce qu'elle ajoute et chaque permission avant d'installer.

| Extension | Ce qu'elle fait | Permissions |
| --- | --- | --- |
| [Quiz](quiz/) | Des quiz composés depuis les fiches : la question et la réponse se prennent dans le texte, à la chaîne ; les questions ratées reviennent en premier. Les blocs ` ```quiz ` de la version 1 restent lisibles et deviennent des quiz en un clic. | lecture des fiches, sélection, stockage |
| [Chronologie](chronologie/) | Des frises nommées : « Ajouter à la frise » sur un texte sélectionné, la date proposée, ou les dates d'une fiche à cocher. Chaque date rouvre son passage, « Remettre dans l'ordre » en fait un jeu, et « Ce jour-là » paraît sur Aujourd'hui. Les blocs ` ```chronologie ` restent lisibles. | lecture des fiches, sélection, stockage |
| [Vocabulaire](vocabulaire/) | Un carnet par langue : « Ajouter au vocabulaire » sur un mot sélectionné demande sa traduction et garde sa phrase. Les mots reviennent à J1, J3, J7, J14 et J30, à écrire ou à dévoiler, dans les deux sens ; le menu et Aujourd'hui comptent ceux à revoir. Les blocs ` ```vocabulaire ` restent lisibles. | lecture des fiches, sélection, stockage |

Les extensions de la 0.x, telles qu'elles ont été publiées, sont gardées à l'octet près dans `tests/fixtures/extensions` :
elles servent à vérifier qu'elles tournent toujours sans changement.

Les types de l'objet `kyfrev` sont dans [`docs/extensions/kyfrev.d.ts`](../docs/extensions/kyfrev.d.ts).
