# Captures du design system

Rend la galerie des composants de `KyfRev.DesignSystem` (typographie, boutons, champs,
pastilles, cartes, notifications, squelettes, état vide, menu, dialogue) dans les trois thèmes,
la coquille de `KyfRev.Presentation.Wpf` (une vue en clair et en sombre, la recherche, l'aide,
et le menu à chaque largeur de fenêtre : onglets, colonne d'icônes, barre latérale),
l'accueil sans coffre et le tableau de bord d'une journée chargée (en clair et en sombre), une
fiche et un passage en révision, la séance d'étude et son bilan (sur un coffre d'essai
jetable, effacé ensuite), pour la revue visuelle ; affiche aussi le contraste de chaque paire de
rôles.

```powershell
dotnet run tools/design-captures/captures.cs -- docs/captures/j5
```

Fichiers écrits : `galerie-clair.png`, `galerie-sombre.png`, `galerie-contraste-eleve.png`,
`coquille-clair.png`, `coquille-sombre.png`, `recherche-sombre.png`, `aide-clair.png`,
`coquille-etroite-clair.png` (390 px), `coquille-etroite-plus-sombre.png` (une vue rangée sous
« Plus »), `aide-etroite-sombre.png`, `coquille-moyenne-sombre.png` (1 024 px),
`coquille-large-clair.png` (1 460 px), `accueil-clair.png`, `accueil-sombre.png`, `aujourdhui-clair.png`, `aujourdhui-sombre.png`,
`aujourdhui-etroit-clair.png` (390 px), `aujourdhui-tablette-sombre.png` (834 px),
`aujourdhui-ecran-clair.png` (1 600 px),
`fiche-clair.png`, `passage-sombre.png`, `seance-clair.png` et `bilan-sombre.png`. Les
captures de chaque jalon sont rangées dans `docs/captures/<jalon>` (`j5` : design system et
coquille ; `j6` : accueil, tableau de bord, fiche et passage en révision, séance d'étude). Le
contraste élevé prend les couleurs système du poste qui lance l'outil : sur un poste sans
contraste élevé, la capture montre la correspondance des rôles, pas un thème réel de Windows.

Les captures de la vue « Aujourd'hui » montrent un exemple d'encart de module : les modules ne
sont pas encore branchés dans l'application.

Le rendu se fait hors écran (`RenderTargetBitmap`, 96 ppp), animations arrêtées.
