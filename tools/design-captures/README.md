# Captures du design system

Rend la galerie des composants de `KyfRev.DesignSystem` (typographie, boutons, champs,
pastilles, cartes, notifications, squelettes, état vide, menu, dialogue) dans les trois thèmes,
la coquille de `KyfRev.Presentation.Wpf` (une vue en clair et en sombre, la recherche, l'aide),
l'accueil sans coffre et le tableau de bord d'une journée chargée (en clair et en sombre), une
fiche et un passage en révision (sur un coffre d'essai jetable, effacé ensuite), pour la revue
visuelle ; affiche aussi le contraste de chaque paire de rôles.

```powershell
dotnet run tools/design-captures/captures.cs -- docs/captures/j5
```

Fichiers écrits : `galerie-clair.png`, `galerie-sombre.png`, `galerie-contraste-eleve.png`,
`coquille-clair.png`, `coquille-sombre.png`, `recherche-sombre.png`, `aide-clair.png`,
`accueil-clair.png`, `accueil-sombre.png`, `aujourdhui-clair.png`, `aujourdhui-sombre.png`,
`fiche-clair.png` et `passage-sombre.png`. Les captures de chaque jalon sont rangées dans
`docs/captures/<jalon>` (`j5` : design system et coquille ; `j6` : accueil, tableau de bord,
fiche et passage en révision). Le
contraste élevé prend les couleurs système du poste qui lance l'outil : sur un poste sans
contraste élevé, la capture montre la correspondance des rôles, pas un thème réel de Windows.

Le rendu se fait hors écran (`RenderTargetBitmap`, 96 ppp), animations arrêtées.
