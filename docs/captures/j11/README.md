# Captures de J11

État des vues de kyfRev au 17 novembre 2026, après la 1.1.0-beta.2. Les captures de `docs/captures/j5`, `j6` et `j7`
datent des 1er et 2 novembre : elles restent comme pièces de la validation de ces jalons, et ne montrent plus l'état
de l'application.

Deux sources :

- **L'outil de captures** (`dotnet run tools/design-captures/captures.cs -- <dossier>`) : la galerie des composants, la
  coquille à chaque largeur, l'accueil, Aujourd'hui, les archives, les fiches, les définitions, la lecture, l'écriture, la
  révision, la séance et les galeries de schémas. Rendu hors écran, sur un coffre d'essai jetable.
- **Les essais de présentation** (`tests/KyfRev.Presentation.Wpf.Tests`, avec la variable d'environnement
  `KYFREV_CAPTURES`) : les Paramètres (`parametres-<largeur>-<thème>-<section>.png`), le Carnet (`carnet-…`), la mise à
  jour (`maj-…`), l'impression (`impression-…`), le papier des feuilles (`lecture-…`, `ecriture-lignes-a5.png`,
  `apercu-feuille.png`), la fiche à plusieurs largeurs (`fiche-<largeur>-…`), les schémas réseau (`schemas-…`), le réglage
  désigné par la recherche (`reglage-designe-…`). Leur contenu est celui des essais : du texte de remplissage, pas des
  fiches d'exemple.

Ce qui manque : l'écran de déverrouillage d'un coffre chiffré, l'onglet Tableau et l'onglet Image de l'écriture, la santé
du coffre et les statistiques n'ont pas de capture ici. Le contraste élevé prend les couleurs système du poste qui lance
l'outil : sans contraste élevé activé dans Windows, la capture montre la correspondance des rôles, pas un vrai thème.
