# Journal des modifications

Toutes les modifications notables de kyfRev (génération .NET) sont consignées ici.

Le format suit [Keep a Changelog 1.1.0](https://keepachangelog.com/fr/1.1.0/) et le projet
respecte le [versionnement sémantique](https://semver.org/lang/fr/spec/v2.0.0.html).

## [Non publié]

### Ajouté

- Couche Application (`KyfRev.Application`) : 56 cas d'usage, un par commande de la version 0.x,
  regroupés par domaine (coffre, fiches, édition, dossiers, corbeille, révision, rappels,
  annotations, examens, réglages, modèles, santé, sauvegardes, exports). Ils passent par des ports
  (le coffre, les sauvegardes, la mémoire des coffres) et prennent la date du jour d'une horloge
  injectée (`TimeProvider`), dans le fuseau local. Un refus rend le message de la 0.x.
- À l'ouverture d'un coffre, comme en 0.x : la journée est préparée, une sauvegarde est faite si
  aucune ne l'a été ce jour-là, puis la rotation ; un échec est journalisé sans bloquer
  l'ouverture. Le coffre précédent n'est fermé qu'une fois le nouveau ouvert.
- Vue d'une fiche ouverte et état de révision d'une fiche ou d'un passage, identiques à la 0.x
  (onzième scénario comparé, 49 opérations).
- Tableau de correspondance des 97 commandes de la 0.x (`docs/correspondance-commandes.md`),
  vérifié par les tests.
- Tests d'architecture : dépendances entre couches, aucune opération publique au nom de
  suppression, effacement de fichiers limité aux trois cas prévus (rotation des sauvegardes,
  fichiers temporaires).

- Corpus d'ancrage « Markdown → texte affiché → positions » (`tests/fixtures/ancrage`), calculé
  par les sources mêmes de la version 0.x dans Microsoft Edge : 230 documents (fiches de
  démonstration, cas ciblés, documents tirés au hasard), 2 355 passages ancrés et 1 024
  ré-ancrages après modification. La lecture native (J7) devra le reproduire à l'identique ;
  l'outil `tools/anchoring-corpus` le régénère, à l'octet près.
- Les exports du coffre de référence (suivi, historique, annotations, citations) sont comparés à
  l'octet à ceux de la 0.x : chaque sorte d'événement de suivi y figure.
- Coffre, sauvegardes, exports et santé, identiques à la version 0.x :
  - sauvegardes `.zip` vérifiées après écriture, hors du coffre, sans le verrou ; sauvegarde
    quotidienne ; rotation (14 jours, 8 semaines, 24 mois) qui ne touche que les copies nommées
    exactement comme des sauvegardes, jamais un dossier de coffre ;
  - restauration du coffre entier dans un nouveau dossier, ou d'une fiche manquante avec ses
    ressources, sans jamais écraser ; archive refusée avant toute écriture si un nom sort du
    coffre, si la taille annoncée est démesurée ou si une entrée ment sur sa taille ou son
    contenu (CRC-32) ;
  - exports : suivi et historique en CSV pour Excel (protégés contre l'injection de formules),
    annotations en Markdown, citations en Markdown et CSV, coffre entier ;
  - partage d'une fiche : Markdown, archive avec ses images, texte, page web aux images intégrées ;
  - import d'un dossier de notes (Obsidian, Typora…) : liens `[[…]]` convertis, pièces jointes
    copiées sous un nom sûr, dossiers cachés et types exécutables écartés, rien d'écrasé ;
  - « Santé du coffre » détaillée (liens cassés, passages à vérifier), doublon résolu en gardant
    l'original, suivi recréé, annotations supprimées.
- Un dixième scénario (114 opérations) comparé à la 0.x, et le script
  `tools/interop/sauvegardes.ps1` : chaque version restaure à l'identique les sauvegardes de
  l'autre.
- Coffre, écritures complémentaires, identiques à la version 0.x :
  - annotations : passages et définitions, commentaire et définition modifiés, ré-ancrage,
    passage changé (avec ou sans retour à J0), corbeille, méthode des J propre à chaque passage ;
    l'analyse signale les fichiers d'annotations illisibles (jamais réécrits) et les annotations
    orphelines ;
  - rappels : tirage du jour (une seule fois par jour), réponses aux rappels, exclusion et
    réinclusion ;
  - réglages et examens : réglages vérifiés, module Schémas, examens par dossier, échéances
    ramenées avant l'examen ;
  - citations : ajout, modification qui garde les champs d'autres outils, corbeille, citation du
    jour trouvée en ligne retenue dans le coffre ;
  - schémas réseau : création, enregistrement avec détection de conflit, copie ;
  - modèles de fiche (intégrés et du coffre) et données d'extension.
- Trois scénarios de plus (178 opérations) comparés à la 0.x : les neuf scénarios donnent des
  coffres identiques à l'octet près.
- Coffre, écriture des fiches (`VaultSession`), identique à la version 0.x :
  - ouverture : verrou, reprise d'une opération interrompue, analyse qui suit les fiches
    déplacées, modifiées, renommées ou étiquetées hors de l'application ;
  - fiches : création, adoption d'un fichier existant, enregistrement avec versions une fois
    par session et conflits, corps seul avec en-tête conservé, versions gardées et restaurées,
    étiquettes, opérations de la méthode des J ;
  - organisation : changement de titre, déplacement, ressources, dossiers, avec réécriture des
    liens sous la protection du journal d'opération ;
  - corbeille : fiche ou dossier entier, restauration à côté d'une place reprise ;
  - confinement au coffre, y compris face aux liens symboliques et aux jonctions.
- Noms de fichiers, éditions d'en-tête et liens Markdown comparés à la 0.x sur 9 000 cas, et six
  scénarios (195 opérations) rejoués avec les mêmes identifiants : coffres identiques à l'octet près.
- Script `tools/interop/ecriture.ps1` : un coffre écrit par cette version se rouvre dans la 0.x
  sans aucun signalement.
- Coffre (`KyfRev.Infrastructure.Vault`), lecture fidèle du format 1 :
  - chemins validés selon les mêmes règles que la version 0.x ;
  - empreintes SHA-256 des octets bruts ;
  - écriture atomique (temporaire, écriture forcée sur le disque, mise en place sans écrasement) ;
  - verrou du coffre compatible avec la version 0.x, dans les deux sens ;
  - lecture et réécriture à l'octet près de tous les fichiers JSON du coffre ;
  - lecture de l'en-tête YAML des fiches.
- Les champs inconnus sont désormais conservés à tous les niveaux des fichiers.
- Un en-tête YAML qui contient plusieurs documents est refusé, comme en 0.x.
- Outil `kyfrev-coffre verifier` : analyse un coffre en lecture seule (fiches déplacées,
  modifiées, manquantes, en double ou sans suivi, fichiers illisibles, format plus récent,
  opération interrompue, annotations orphelines) et produit un rapport en texte ou en JSON.
- Coffre de référence produit par la version 0.x (chaque sorte de fichier, d'événement de
  suivi, de résultat de rappel et d'état d'ancrage) et script d'interopération du verrou
  (`tools/interop/verrou.ps1`).
- Domaine (`KyfRev.Domain`) : méthode des J (séquence, cycles, échéances depuis la révision
  réellement faite, report, antidatation, redémarrage), archivage, rappels de consolidation
  (éligibilité en mois calendaires, repos progressif, tirage quotidien reproductible avec
  priorité anti-oubli), contrôle d'intégrité d'un suivi relu et graine du jour. Le suivi est
  immuable : une opération refusée ne modifie jamais rien.
- Tests du domaine : scénarios datés de la conception, propriétés sur des suites d'opérations
  aléatoires, et comparaison avec le cœur de la version 0.x sur 10 000 suites d'opérations,
  3 000 tirages, des flux ChaCha8 et 5 000 calculs de dates (`tools/reference-vectors`).
- Tests sous MSTest et Microsoft Testing Platform ; couverture minimale de 95 % des lignes
  exigée par l'intégration continue pour chaque module, mesurée par ses propres tests ; tests de mutation (Stryker.NET, score minimal de 80 %)
  par `tools/mutation.ps1` ou par le workflow « Mutation » lancé à la demande.
- Socle du dépôt : solution .NET 10 (`KyfRev.slnx`), SDK figé, configuration commune de
  compilation et d'analyse (avertissements bloquants, style imposé), gestion centralisée des
  paquets avec fichiers de verrouillage et audit des vulnérabilités.
- Gouvernance : crochets git et intégration continue qui vérifient l'identité des commits, le
  nom des branches, les fusions sur `main` et l'absence de secrets ; détection des raccourcis
  interdits (tests désactivés, avertissements masqués, exceptions avalées).
- Documentation : plan de refonte validé, glossaire du domaine, règles de contribution,
  politique de sécurité.

### Modifié

- L'examen d'un dossier se « retire » (`WithdrawExam`) : plus aucun nom de suppression dans les
  opérations publiques.

[Non publié]: https://github.com/KyllianFF/kyfRev-dotnet/commits/main
