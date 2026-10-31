# Journal des modifications

Toutes les modifications notables de kyfRev (génération .NET) sont consignées ici.

Le format suit [Keep a Changelog 1.1.0](https://keepachangelog.com/fr/1.1.0/) et le projet
respecte le [versionnement sémantique](https://semver.org/lang/fr/spec/v2.0.0.html).

## [Non publié]

### Ajouté

- Domaine (`KyfRev.Domain`) : méthode des J (séquence, cycles, échéances depuis la révision
  réellement faite, report, antidatation, redémarrage), archivage, rappels de consolidation
  (éligibilité en mois calendaires, repos progressif, tirage quotidien reproductible avec
  priorité anti-oubli), contrôle d'intégrité d'un suivi relu et graine du jour. Le suivi est
  immuable : une opération refusée ne modifie jamais rien.
- Tests du domaine : scénarios datés de la conception, propriétés sur des suites d'opérations
  aléatoires, et comparaison avec le cœur de la version 0.x sur 10 000 suites d'opérations,
  3 000 tirages, des flux ChaCha8 et 5 000 calculs de dates (`tools/reference-vectors`).
- Tests sous MSTest et Microsoft Testing Platform ; couverture minimale de 95 % des lignes
  exigée par l'intégration continue ; tests de mutation (Stryker.NET, score minimal de 80 %)
  par `tools/mutation.ps1` ou par le workflow « Mutation » lancé à la demande.
- Socle du dépôt : solution .NET 10 (`KyfRev.slnx`), SDK figé, configuration commune de
  compilation et d'analyse (avertissements bloquants, style imposé), gestion centralisée des
  paquets avec fichiers de verrouillage et audit des vulnérabilités.
- Gouvernance : crochets git et intégration continue qui vérifient l'identité des commits, le
  nom des branches, les fusions sur `main` et l'absence de secrets ; détection des raccourcis
  interdits (tests désactivés, avertissements masqués, exceptions avalées).
- Documentation : plan de refonte validé, glossaire du domaine, règles de contribution,
  politique de sécurité.

[Non publié]: https://github.com/KyllianFF/kyfRev-dotnet/commits/main
