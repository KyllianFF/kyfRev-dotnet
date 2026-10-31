# kyfRev

Application de bureau Windows de **fiches de révision en Markdown**, révisées selon la
**méthode des J** (J0, J1, J3, J7, J14, J30) : chaque étape tombe n jours après la révision
réellement faite. Les fiches terminées sont archivées, puis rappelées au hasard pour
consolidation. Les passages annotés et les définitions ont leur propre calendrier.

Ce dépôt est la **nouvelle génération de kyfRev, en C# .NET**. Elle reprend sans conversion
les coffres de la version 0.x et vise la parité fonctionnelle en version 1.0.0.

## État

Refonte en cours, jalon **J1 : socle du dépôt**. Le plan complet, les décisions et les
critères de validation de chaque jalon sont dans [docs/plan-de-refonte.md](docs/plan-de-refonte.md).

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
dotnet test KyfRev.slnx
dotnet format KyfRev.slnx --verify-no-changes
dotnet slopwatch analyze --no-baseline --fail-on warning
```

Les règles de contribution (branches, commits, fusions, versions) sont dans
[CONTRIBUTING.md](CONTRIBUTING.md), les changements de chaque version dans le
[journal des modifications](CHANGELOG.md), et la politique de sécurité dans
[SECURITY.md](SECURITY.md).

## Licence

[MIT](LICENSE) © 2026 KyllianFF.
