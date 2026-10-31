# Journal des modifications

Toutes les modifications notables de kyfRev (génération .NET) sont consignées ici.

Le format suit [Keep a Changelog 1.1.0](https://keepachangelog.com/fr/1.1.0/) et le projet
respecte le [versionnement sémantique](https://semver.org/lang/fr/spec/v2.0.0.html).

## [Non publié]

### Ajouté

- Socle du dépôt : solution .NET 10 (`KyfRev.slnx`), SDK figé, configuration commune de
  compilation et d'analyse (avertissements bloquants, style imposé), gestion centralisée des
  paquets avec fichiers de verrouillage et audit des vulnérabilités.
- Gouvernance : crochets git et intégration continue qui vérifient l'identité des commits, le
  nom des branches, les fusions sur `main` et l'absence de secrets ; détection des raccourcis
  interdits (tests désactivés, avertissements masqués, exceptions avalées).
- Documentation : plan de refonte validé, glossaire du domaine, règles de contribution,
  politique de sécurité.

[Non publié]: https://github.com/KyllianFF/kyfRev-dotnet/commits/main
