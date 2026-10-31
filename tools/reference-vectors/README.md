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
(`../kyfRev`, version 0.8.0 ; le crate `noyau` n'a pas changé depuis la 0.7.0).

```powershell
cargo run --release --manifest-path tools/reference-vectors/Cargo.toml -- tests/KyfRev.Domain.Tests/fixtures/reference
```

Pour comprendre un écart signalé par un test sur le suivi `n`, afficher sa transcription Rust :

```powershell
cargo run --release --manifest-path tools/reference-vectors/Cargo.toml -- --transcript n
```

Les vecteurs ne se régénèrent que si l'on change volontairement les cas couverts : le
comportement de référence, lui, est figé.
