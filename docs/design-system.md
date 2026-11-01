# Design system de kyfRev

`src/KyfRev.DesignSystem` réunit l'identité visuelle de kyfRev, « Veillée », et les composants
WPF qui l'appliquent. Il ne dépend d'aucune autre couche ; la présentation l'emploie, jamais
l'inverse.

Veillée a été choisie en novembre 2026 parmi plusieurs propositions : une interface pensée pour
les longues séances de révision, lisible avant tout (Atkinson Hyperlegible), douce le jour (vert
sauge et forêt) et chaude le soir (un sombre brun plutôt que bleuté), aux formes arrondies sans
être enfantines.

![Galerie, thème clair](captures/j5/galerie-clair.png)

Les captures sont dans [`captures/j5`](captures/j5) : la galerie des composants en
[clair](captures/j5/galerie-clair.png), [sombre](captures/j5/galerie-sombre.png) et
[contraste élevé](captures/j5/galerie-contraste-eleve.png) (avec les couleurs système du poste
qui les a produites), et la coquille ([claire](captures/j5/coquille-clair.png),
[sombre](captures/j5/coquille-sombre.png), [recherche](captures/j5/recherche-sombre.png),
[aide](captures/j5/aide-clair.png)). `tools/design-captures` les régénère ; la galerie
s'ouvre aussi dans l'application (`kyfRev --galerie`).

## Jetons

### Gammes

Deux gammes de dix nuances (50 à 900), de la plus claire à la plus foncée (`Tokens/Palette.cs`) :

| Gamme | Code | Emploi | Nuances clés |
|---|---|---|---|
| Forêt | `Palette.Forest` | l'action, ce qui est fait, la sélection | 700 `#2e6a4e` (clair), 300 `#86c7a1` (sombre) |
| Ambre | `Palette.Amber` | le retard, l'avertissement, les passages surlignés | 700 `#93540f` (clair), 300 `#eba458` (sombre) |

S'y ajoutent, pour chaque thème, les neutres et les couleurs fonctionnelles : « Jour », clair,
teinté de sauge (fond `#edefe9`, cartes `#fafbf7`), et « Veillée », sombre et chaud (fond
`#191714`, cartes `#24211c`). L'erreur est brique (`#a33a2b`, `#f0a090`), hors des deux gammes,
pour ne jamais se confondre avec un retard. `Palette.cs` est le seul fichier où une couleur
s'écrit : un test refuse toute couleur écrite ailleurs dans l'interface (code ou XAML, valeur
hexadécimale ou couleur nommée).

### Rôles

Les composants n'emploient que des rôles (`ColorRole`, 38 rôles), traduits en couleur par le
thème (`ThemeColors`) :

| Rôle | Clair (Jour) | Sombre (Veillée) | Contraste élevé |
|---|---|---|---|
| `Background`, `Surface`, `SurfaceAlt` | `#edefe9`, `#fafbf7`, `#e3e8e0` | `#191714`, `#24211c`, `#2e2a24` | fenêtre |
| `Text`, `TextSecondary` | `#1d2822`, `#4f5c54` | `#efe9dc`, `#b4ab9b` | texte de fenêtre |
| `Primary` / survol / enfoncé | forêt 700 / 800 / 900 | forêt 300 / 200 / 100 | mise en évidence |
| `OnPrimary` | blanc | `#13201a` | texte mis en évidence |
| `Link`, `Focus` | forêt 700 | forêt 300 | lien, texte de fenêtre |
| `ControlBorder` | `#77837a` | `#877e70` | texte de fenêtre |
| `Divider` (décoratif) | `#d3d9d0` | `#38332b` | texte de fenêtre |
| Repères Fiche, Passage, Rappel | forêt 700, ambre 700, texte secondaire | forêt 300, ambre 300, texte secondaire | texte de fenêtre |
| Pastilles Fiche, Passage | texte 800 sur fond 100 | texte 100 sur fond 900 | texte sur fenêtre |
| `Selection`, `Highlight`, `HighlightActive` | forêt 200, ambre 200, ambre 300 | forêt 800, ambre 800, ambre 700 | mise en évidence |
| `NavigationActiveBackground` | forêt 100 | forêt 900 | mise en évidence |
| `Critical`, `Warning`, `Success`, `Info` | `#a33a2b`, ambre 700, forêt 700, forêt 800 | `#f0a090`, ambre 300, forêt 300, forêt 100 | texte de fenêtre |

### Contrastes

`ContrastRequirements` liste les 51 paires de rôles que les composants emploient : 4,5:1 au
moins pour un texte (WCAG 1.4.3), 3:1 pour un contour ou un repère (1.4.11). Les tests les
vérifient toutes, dans chaque thème :

| Thème | Textes (33 paires) | Contours et repères (18 paires) |
|---|---|---|
| Clair (Jour) | 4,94:1 au moins (retard sur son fond) | 3,18:1 au moins (contour sur `SurfaceAlt`) |
| Sombre (Veillée) | 4,94:1 au moins (texte sur le surlignage actif) | 3,56:1 au moins (contour sur `SurfaceAlt`) |

- Le contour des contrôles (`ControlBorder`) atteint 3:1 sur tous les fonds. Le trait clair des
  séparateurs et des cartes (`Divider`) reste décoratif : il ne porte jamais seul une information.
- En contraste élevé, chaque paire tombe sur une paire de couleurs de Windows dont Windows
  garantit le contraste (texte sur fenêtre, texte mis en évidence sur mise en évidence, lien sur
  fenêtre) ; les ombres disparaissent, les contours restent.
- Un état ne dépend jamais de la seule couleur : chaque pastille et chaque notification porte
  une icône et un libellé, l'élément choisi d'une liste passe en gras.

### Échelles

| Échelle | Valeurs |
|---|---|
| Typographie (taille / interligne) | `Caption` 13/20, `Body` 15/24 (et `BodyStrong`), `BodyLarge` 17/24, `Subtitle` 21/28, `Title` 34/40, `Display` 48/56 |
| Lecture des fiches | 17, 19 ou 21 px, interligne 1,65 (réglage de la 0.x) |
| Polices | Atkinson Hyperlegible Next (interface, titres, fiches) et Atkinson Hyperlegible Mono (étapes, dates, compteurs, code), livrées avec kyfRev ; Segoe Fluent Icons ou Segoe MDL2 Assets (icônes) |
| Espacements | grille de 4 px : 4, 8, 12, 16, 20, 24, 32, 40, 48 |
| Rayons | 12 (contrôles), 20 (cartes, menus), 24 (dialogues) ; pastilles en arrondi complet |
| Élévations | « Card » (ombre douce des cartes), « Flyout » (menus) et « Dialog » ; aucune en contraste élevé |
| Tailles | contrôles de 40 px de haut au moins, 44 pour le bouton principal et les éléments de liste |
| Durées | 120 et 220 ms, décélération douce ; nulles, et squelettes immobiles, si Windows demande de réduire les animations |

### Polices

Les deux familles Atkinson sont livrées dans `src/KyfRev.DesignSystem/Fonts`, en fichiers
statiques (WPF ne choisit pas la graisse d'une police variable), avec leur licence SIL Open
Font License 1.1, copiée à côté de l'exécutable. Elles viennent des dépôts officiels, à un
commit figé :

| Famille | Fichiers | Source |
|---|---|---|
| Atkinson Hyperlegible Next | Regular, Italic, SemiBold, SemiBoldItalic, Bold, BoldItalic | `googlefonts/atkinson-hyperlegible-next` à `7925f50f` |
| Atkinson Hyperlegible Mono | Regular, SemiBold | `googlefonts/atkinson-hyperlegible-next-mono` à `154d5036` |

## Composants

| Composant | Code | Rôle |
|---|---|---|
| Boutons | styles `Kyf.Button` (secondaire, par défaut), `Kyf.Button.Primary`, `Kyf.Button.Subtle`, `Kyf.Button.Icon` | une seule action principale par vue |
| Champ | style de `TextBox`, indication `Field.Placeholder` | contour à 3:1, contour épaissi au focus, indication lue comme aide |
| Carte | `Card`, repère `Marker` (fiche, passage, rappel) | ombre douce ; le repère est un court trait en haut |
| Pastille | `Pill`, sorte `Kind` | icône et libellé : fiche, passage, rappel, retard, archivée, à vérifier |
| Menu | styles de `ContextMenu`, `Menu`, `MenuItem` | coche, raccourci, sous-menus |
| Listes | styles de `ListBox`, `ListBoxItem` | survol, élément choisi : fond vert pâle et texte en gras (menu de la coquille) |
| Icône | `Icon` | décorative : police d'icônes de Windows, ignorée par le lecteur d'écran |
| Dialogue | `Dialog` | voile, focus tenu à l'intérieur, Échap ferme, Entrée valide |
| Notification | `Notification`, gravité `Severity` | annoncée par le lecteur d'écran (tout de suite pour une erreur) |
| État vide | `EmptyState` | icône, titre, explication, action |
| Squelette | `Skeleton` | chargement, immobile si les animations sont réduites |
| Texte | styles `Kyf.Text.*` | l'échelle typographique |

Le focus clavier est toujours visible : un anneau de 2 px décalé de 2 px (`Kyf.FocusVisual`), ou
le contour épaissi des champs. Les textes des composants (« Fermer », « Annuler »…) sont lus
dans `Resources/DesignText.resx`.

Chaque composant se présente à UI Automation (lecteurs d'écran, Accessibility Insights) : le
dialogue comme un volet marqué « dialogue » et nommé par son titre, la notification comme un
groupe annoncé dès qu'il paraît, la pastille et le squelette comme un texte, l'état vide comme un
groupe nommé par son titre et décrit par son explication. Les composants qui ne s'actionnent pas
ne prennent pas le focus.

## Coquille

`KyfRev.Presentation.Wpf` assemble ces composants dans la fenêtre de l'application :

| Élément | Clavier | Rôle |
|---|---|---|
| Menu latéral | flèches, Tab | les vues de la 0.x dans le même ordre, celles des modules actifs à leur rang, Santé du coffre et Paramètres en bas |
| Recherche de commandes | Ctrl+K | aller à une vue, changer de thème, de zoom ou de mode ; sans tenir compte des accents ni de la casse |
| Aide | F1 | les raccourcis clavier |
| Mode concentration | Ctrl+Maj+F | masque le menu |
| Zoom | Ctrl+plus, Ctrl+moins, Ctrl+0 | de 80 à 200 %, par pas de 10 % |
| Vues du menu | Ctrl+1 à Ctrl+9 | les neuf premières vues |

Le thème suit Windows (clair ou sombre, barre de titre comprise) ou le choix fait dans la
recherche ou dans la galerie ; le contraste élevé de Windows passe avant tout choix, et un
changement dans Windows s'applique sans redémarrage. Les vues encore à porter présentent ce
qu'elles feront.

Les règles automatiques d'Accessibility Insights (FastPass) sont appliquées par Axe.Windows à
l'application lancée pour de vrai : chaque vue, l'aide et la recherche, en clair et en sombre,
sans aucune erreur (`tests/KyfRev.Presentation.Wpf.Tests`).

![Coquille, thème sombre, recherche ouverte](captures/j5/recherche-sombre.png)

## Emploi

```csharp
var design = new DesignResources(ThemeKind.Light, Motion.SystemPrefersReducedMotion);
Application.Current.Resources.MergedDictionaries.Add(design.Dictionary);
design.Theme = ThemeKind.Dark;   // l'interface suit, sans rechargement
```

Les styles nomment leurs ressources par `DesignKeys` (`Kyf.Brush.Primary`, `Kyf.FontSize.Body`…),
toujours en `DynamicResource` pour suivre le thème.
