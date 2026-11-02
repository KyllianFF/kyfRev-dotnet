# kyfRev

Application de bureau Windows de **fiches de révision en Markdown**, révisées selon la
**méthode des J** (J0, J1, J3, J7, J14, J30) : chaque étape tombe n jours après la révision
réellement faite. Les fiches terminées sont archivées, puis rappelées au hasard pour
consolidation. Les passages annotés et les définitions ont leur propre calendrier.

Ce dépôt est la **nouvelle génération de kyfRev, en C# .NET**. Elle reprend sans conversion
les coffres de la version 0.x et vise la parité fonctionnelle en version 1.0.0.

## État

Refonte en cours, jalon **J5 : design system et coquille WPF**. Le plan complet, les
décisions et les critères de validation de chaque jalon sont dans [docs/plan-de-refonte.md](docs/plan-de-refonte.md).

Vérifier un coffre existant, sans rien y écrire :

```powershell
dotnet run --project src/KyfRev.Cli -- verifier "$env:USERPROFILE\Documents\kyfRev\Coffre"
```

Ouvrir la coquille de l'application (menu, recherche Ctrl+K, aide F1, mode concentration,
zoom), ou directement la galerie des composants ([design system](docs/design-system.md)) :

```powershell
dotnet run --project src/KyfRev.App
dotnet run --project src/KyfRev.App -- --galerie
```

## Principes

- **Vos données restent des fichiers ordinaires** : Markdown et JSON dans un dossier, le
  « coffre », lisible sans kyfRev. Chiffrement au repos en option.
- **Rien ne s'efface** : une fiche supprimée va dans une corbeille restaurable, jamais purgée.
- **Hors ligne par défaut** : aucune connexion sans action ou réglage explicite.
- **Accessible** : contrastes vérifiés, clavier d'abord, clair, sombre et contraste élevé.

## Développer

Prérequis : Windows 11 (ou Windows 10 22H2), [PowerShell 7](https://learn.microsoft.com/powershell/),
git, et le SDK .NET indiqué dans [global.json](global.json).

Préparer le poste (identité git, crochets, outils figés et vérifiés) :

```powershell
pwsh tools/setup.ps1
```

Compiler, tester, vérifier :

```powershell
dotnet build KyfRev.slnx
dotnet test --solution KyfRev.slnx
dotnet format KyfRev.slnx --verify-no-changes
dotnet slopwatch analyze --no-baseline --fail-on warning
```

Tests de mutation du domaine (quelques minutes ; aussi disponibles à la demande dans
l'onglet Actions, workflow « Mutation ») :

```powershell
pwsh tools/mutation.ps1
```

Construire l'installateur (avec [Inno Setup 6](https://jrsoftware.org/isinfo.php), la 6.7.3
comme dans l'intégration continue), puis l'essayer sans droits administrateur : installation,
mise à jour, retour en arrière refusé, désinstallation, dans un dossier temporaire :

```powershell
$installateur = pwsh tools/installer.ps1
pwsh tools/installer-tests.ps1 -Installateur $installateur
```

Les tests du domaine comparent aussi l'implémentation C# au cœur Rust de la version 0.x, à
partir de vecteurs de référence versionnés : voir [tools/reference-vectors](tools/reference-vectors/README.md).

Les règles de contribution (branches, commits, fusions, versions) sont dans
[CONTRIBUTING.md](CONTRIBUTING.md), les changements de chaque version dans le
[journal des modifications](CHANGELOG.md), et la politique de sécurité dans
[SECURITY.md](SECURITY.md).

## Licence

[MIT](LICENSE) © 2026 KyllianFF.

Le code repris d'autres projets garde sa licence : voir les avis de
[`KyfRev.Rendering`](src/KyfRev.Rendering/THIRD-PARTY-NOTICES.md) et de
[l'éditeur](src/KyfRev.Presentation.Wpf/Editing/THIRD-PARTY-NOTICES.md), et les licences des
[polices](src/KyfRev.DesignSystem/Fonts).
