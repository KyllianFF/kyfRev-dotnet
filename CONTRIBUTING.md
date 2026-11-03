# Contribuer à kyfRev

## Préparer le poste

```powershell
pwsh tools/setup.ps1
```

Le script règle l'identité git du dépôt, active les crochets de `.githooks/` et installe les
outils figés (gitleaks dans `.tools/`, outils .NET de `.config/dotnet-tools.json`).

## Identité des commits

Tous les commits ont pour **auteur et committer** :

```
KyllianFF <113480351+KyllianFF@users.noreply.github.com>
```

Aucun message ne mentionne de co-auteur ni ne porte d'adresse web en ligne finale.
Le crochet `commit-msg` et l'intégration continue le vérifient sur chaque commit.

## Branches

On ne travaille jamais directement sur `main`. Chaque modification passe par une branche
nommée `<type>/<sujet>`, le sujet en minuscules (lettres, chiffres, `.`, `_`, `-`) :

| Type | Usage |
|---|---|
| `feature/` | Nouvelle fonction |
| `fix/` | Correctif |
| `hotfix/` | Correctif urgent d'une version publiée, tiré de son étiquette |
| `release/` | Préparation d'une version (`release/1.0.0`) |
| `chore/` | Outillage, dépendances, intégration continue |
| `refactor/` | Restructuration sans changement de comportement |
| `docs/` | Documentation |
| `test/` | Tests seuls |

## Fusionner dans main

Les fusions se font **en local**, sans avance rapide, pour que le committer reste KyllianFF
(une fusion depuis l'interface de GitHub inscrirait GitHub comme committer) :

```powershell
git switch main
git pull
git merge --no-ff feature/mon-sujet
git push
```

Le crochet `pre-push` et l'intégration continue refusent tout commit fait directement sur
`main` et toute fusion d'une branche mal nommée. Une demande de fusion ouverte sur GitHub pour
la relecture est marquée fusionnée automatiquement après l'envoi.

Une demande de fusion s'ouvre **en brouillon** tant qu'elle n'est pas prête : un envoi sur une
branche ne lance que la gouvernance, et les jobs sur Windows, qui consomment le quota d'Actions,
ne tournent que sur une demande de fusion prête et sur `main`. Pour plusieurs branches empilées,
seule la dernière a besoin d'être prête : elle contient les autres, et `main` ne reçoit qu'un
envoi une fois toutes les fusions faites.

Quand l'intégration continue ne peut pas tourner (quota d'Actions épuisé), `tools/verification-sandbox.ps1`
refait ses étapes sur le commit en cours dans Windows Sandbox, un Windows vierge et jetable, et finit par un
bilan : Smart App Control n'y bloque pas les assemblys tout juste compilées, et le poste reste utilisable.
`tools/verification-locale.ps1` fait la même chose directement sur le poste, si Windows Sandbox n'est pas
activé. Ses scénarios tapent alors de vraies touches : il ne faut toucher ni au clavier ni à la souris pendant
ce temps, ou passer `-SansClavier`. Dans les deux cas, ce qui n'a pas pu être essayé est marqué « non vérifié »,
et non réussi.

## Avant chaque envoi

- `dotnet build`, `dotnet test --solution KyfRev.slnx` et `dotnet format --verify-no-changes`
  passent ; la couverture reste d'au moins 95 % des lignes (contrôlée par l'intégration
  continue : sur un poste où Smart App Control est actif, Windows refuse de charger les
  assemblys instrumentées par coverlet, la mesure se lit alors dans l'artefact « couverture ») ;
- une modification du domaine garde un score de mutation d'au moins 80 %
  (`pwsh tools/mutation.ps1`, ou le workflow « Mutation ») ;
- `dotnet slopwatch analyze --no-baseline --fail-on warning` ne signale rien : aucun test
  désactivé, aucun avertissement masqué, aucune exception avalée, aucune version de paquet
  hors de `Directory.Packages.props` ;
- la rubrique `[Non publié]` du [CHANGELOG](CHANGELOG.md) décrit la modification.

## Dépendances

- Versions définies uniquement dans `Directory.Packages.props`, ajoutées par `dotnet add package`.
- Une version n'est adoptée que si elle est publiée depuis au moins **14 jours**, sous licence
  permissive. L'audit NuGet bloque la compilation à la moindre vulnérabilité connue.
- Un paquet publié avec l'application livre le texte de sa licence, ou celui-ci est rangé dans
  `installer/licences-nuget` : sans lui, `tools/avis-nuget.ps1` arrête la construction de l'installateur.
- Les actions GitHub sont épinglées par empreinte de commit.

## Versions

- [Versionnement sémantique](https://semver.org/lang/fr/) strict. La version a une source
  unique : `<Version>` dans `Directory.Build.props`.
- Préversions `1.0.0-alpha.N` puis `1.0.0-beta.N`, puis `1.0.0` à la parité avec la 0.x.
- Une version se prépare dans `release/x.y.z` : version, section datée du CHANGELOG, puis
  fusion dans `main` et étiquette annotée `vX.Y.Z` créée en local.
