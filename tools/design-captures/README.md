# Captures du design system

Rend la galerie des composants de `KyfRev.DesignSystem` (typographie, boutons, champs,
pastilles, cartes, notifications, squelettes, état vide, menu, dialogue) dans les trois thèmes,
pour la revue visuelle, et affiche le contraste de chaque paire de rôles.

```powershell
dotnet run tools/design-captures/captures.cs -- docs/captures/j5
```

Fichiers écrits : `galerie-clair.png`, `galerie-sombre.png` et `galerie-contraste-eleve.png`. Le
contraste élevé prend les couleurs système du poste qui lance l'outil : sur un poste sans
contraste élevé, la capture montre la correspondance des rôles, pas un thème réel de Windows.

Le rendu se fait hors écran (`RenderTargetBitmap`, 96 ppp), animations arrêtées.
