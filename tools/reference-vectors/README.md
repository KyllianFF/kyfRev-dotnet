# Vecteurs de référence du cœur Rust

Ce petit programme exécute le cœur métier de kyfRev 0.x (crate `noyau`, en Rust) sur des
milliers de cas tirés d'un générateur SplitMix64 à graine fixe. Il écrit les **résultats** dans
`tests/KyfRev.Domain.Tests/fixtures/reference/`. La suite de tests C# régénère les mêmes entrées,
rejoue `KyfRev.Domain` et compare.

| Fichier | Contenu |
|---|---|
| `tracking.txt` | 10 000 suites d'opérations sur un suivi : une lettre par opération (réussite, nature d'erreur, requête) et l'empreinte SHA-256 de la transcription complète (états JSON, issues, messages d'erreur) |
| `draws.txt` | 3 000 tirages quotidiens de rappels : sélection et prioritaires non servis |
| `chacha8.txt` | 16 flux ChaCha8 (`seed_from_u64`) : 70 sorties `next_u64` chacun |
| `seeds.txt` | 300 graines du jour (FNV-1a de `identifiant|date`) |
| `calendar.txt` | 5 000 ajouts de mois calendaires et écarts en jours |

## Régénérer

Prérequis : Rust stable, et le dépôt kyfRev 0.x cloné **à côté** de ce dépôt
(`../kyfRev`, version 0.8.0 ; le crate `noyau` n'a pas changé depuis la 0.7.0). Le clone doit
inclure le passage de `zip` en 9.0.5 (correctif de sécurité GHSA-m665-v7pm-7grj) : l'outil
épingle la même version que le crate `coffre`, qu'il compile.

```powershell
cargo run --release --manifest-path tools/reference-vectors/Cargo.toml -- tests/KyfRev.Domain.Tests/fixtures/reference
```

Pour comprendre un écart signalé par un test sur le suivi `n`, afficher sa transcription Rust :

```powershell
cargo run --release --manifest-path tools/reference-vectors/Cargo.toml -- --transcript n
```

Les vecteurs ne se régénèrent que si l'on change volontairement les cas couverts : le
comportement de référence, lui, est figé.

## Coffre de référence

`--coffre <dossier>` construit, avec le crate `coffre` de la 0.x, un coffre synthétique au
contenu fictif. Il contient chaque sorte de fichier du format 1 : suivis actifs et archivés,
passages et définitions, corbeilles (annotations d'une fiche supprimée comprises), versions,
journal d'opération, tirage, citations, examen, schéma et données d'extension. Ses suivis
réunissent chaque sorte d'événement, d'origine de J0, de résultat de rappel et d'état
d'ancrage. Il est versionné dans
`tests/KyfRev.Infrastructure.Vault.Tests/fixtures/coffre-reference`, où les tests vérifient que
chaque fichier JSON est relu puis réécrit à l'octet près.

```powershell
$env:COMPUTERNAME = 'POSTE-ESSAI'   # nom de poste fictif inscrit dans verrou-info.json
cargo run --release --manifest-path tools/reference-vectors/Cargo.toml -- --coffre tests/KyfRev.Infrastructure.Vault.Tests/fixtures/coffre-reference
```

Les identifiants (ULID) et certaines dates viennent de l'horloge : le coffre n'est régénéré que
pour couvrir un nouveau cas, puis relu en entier.

`--ouvrir <dossier> <secondes>` ouvre un coffre comme kyfRev 0.x (verrou compris) et le garde
ouvert ; il sert à `tools/interop/verrou.ps1`, qui vérifie que le verrou est respecté dans les
deux sens entre les deux versions.

## Fonctions pures et scénarios du coffre

`--vecteurs-coffre <dossier>` écrit `noms.txt`, `entetes.txt` et `liens.txt` : 9 000 cas tirés au
hasard (Unicode, CRLF, BOM, crochets, chevrons, pourcentages, blocs de code) sur les noms de
fichiers, les éditions d'en-tête et les liens Markdown du crate `coffre`. Ils sont versionnés dans
`tests/KyfRev.Infrastructure.Vault.Tests/fixtures/reference` ; `--fonctions <famille> <n>`
affiche le détail du cas `n` (familles `noms`, `entetes`, `liens`).

```powershell
cargo run --release --manifest-path tools/reference-vectors/Cargo.toml -- --vecteurs-coffre tests/KyfRev.Infrastructure.Vault.Tests/fixtures/reference
```

`--scenarios <dossier>` joue chaque scénario (fiches, organisation, corbeille, analyse et journal, annotations, rappels et réglages, citations, schémas et modèles, sauvegardes, exports, partage, import et réparations, fiche ouverte et état de révision, citation du jour) de `tests/KyfRev.Infrastructure.Vault.Tests/fixtures/scenarios`, ou le seul scénario `<dossier>`
(une suite d'opérations décrite dans `scenario.json`) avec le crate `coffre`, et écrit à côté le
coffre obtenu (`attendu/`), le résultat de chaque opération, les identifiants créés dans l'ordre
et la liste des dossiers. Les tests C# rejouent le scénario avec les mêmes identifiants et
exigent un coffre identique à l'octet près. Ce qui est écrit hors du coffre (sauvegardes,
exports, partages, dossier importé) l'est dans `attendu.dehors/`, effacé à la fin : les résultats
en donnent le contenu. Les exports datent les événements en UTC, comme les tests C#.

```powershell
$env:COMPUTERNAME = 'POSTE-ESSAI'
cargo run --release --manifest-path tools/reference-vectors/Cargo.toml -- --scenarios tests/KyfRev.Infrastructure.Vault.Tests/fixtures/scenarios
```

`--sante <dossier>` ouvre un coffre comme kyfRev 0.x et résume « Santé du coffre » ; il sert à
`tools/interop/ecriture.ps1`, qui vérifie qu'un coffre écrit par la génération .NET se rouvre
dans la 0.x sans aucun signalement.

`--sauvegarder <coffre> <dossier>` et `--restaurer <archive> <dossier>` sauvegardent et
restaurent comme kyfRev 0.x ; ils servent à `tools/interop/sauvegardes.ps1`, qui vérifie que
chaque version restaure à l'identique les sauvegardes de l'autre.

`--exports <coffre> <dossier>` exporte un coffre comme kyfRev 0.x (suivi, historique,
annotations, citations, en UTC) ; appliqué à une copie du coffre de référence, il produit
`tests/KyfRev.Infrastructure.Vault.Tests/fixtures/exports-reference`, que les tests C#
reproduisent à l'octet.

## Citation du jour en ligne

`--wikiquote <dossier>` écrit, avec le module `wikiquote` du crate `coffre` :

| Fichier | Contenu |
|---|---|
| `pages.txt` | 6 080 tirages des pages du jour : identifiant de coffre, jour, domaines choisis (valides ou non), pages rendues |
| `domaines.txt` | 400 listes de domaines saisis, et les domaines reconnus |
| `citations.json` | 806 wikitextes (ceux des tests de la 0.x, puis des pages tirées au hasard de morceaux typiques de Wikiquote) et les citations retenues, telles que serde les écrit |

Ils sont versionnés dans `tests/KyfRev.Modules.Citations.Tests/fixtures/wikiquote`. Un wikitexte
sur lequel la 0.x panique est marqué `"panique": true` (un seul : un caractère accentué qui
chevauche la fenêtre de 12 octets d'une entité) ; la suite C# vérifie qu'il passe.

```powershell
cargo run --release --manifest-path tools/reference-vectors/Cargo.toml -- --wikiquote tests/KyfRev.Modules.Citations.Tests/fixtures/wikiquote
```
