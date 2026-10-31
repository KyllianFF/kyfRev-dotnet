# Glossaire du domaine : français ↔ code

L'interface et la documentation sont en français ; les identifiants du code sont en anglais.
Ce glossaire fixe la correspondance. Les **clés JSON du coffre restent celles du format 1**
(en français) : la couche de persistance fait la traduction.

## Révision

| Terme | Code | Clé JSON (format 1) | Définition |
|---|---|---|---|
| Méthode des J | — | — | Révisions espacées : l'étape Jn tombe n jours après la révision précédente réellement faite. |
| Séquence | `ReviewSequence` | `sequence` | Liste croissante des J, commençant par 0 (défaut `[0, 1, 3, 7, 14, 30]`). |
| Étape | `Step` | `etape` | Index dans la séquence ; son libellé est « J7 ». |
| Cycle | `Cycle` | `cycle` | Passage complet de J0 à la dernière étape. |
| Échéance | `DueDate` | `echeance` | Date (locale) à laquelle l'étape attendue est due. |
| Révision | `Review` | `revision` | Validation d'une étape ; une seule par élément et par jour. |
| Reporter | `Postpone` | `report` | Déplacer l'échéance sans réviser. |
| Recommencer à J0 | `Restart` | `redemarrage` | Ouvrir un nouveau cycle le jour même. |
| Interruption | `Interruption` | `interruption` | Cycle abandonné avant sa dernière étape. |
| Archiver | `Archive` | `archivage` | Sortir l'élément des révisions actives (fin de cycle ou manuel). |
| Réactivation | `Reactivation` | `reactivation` | Retour d'un élément archivé en révision. |
| Origine du J0 | `StartOrigin` | `origine` | `creation`, `adoption`, `rappel`, `redemarrage`. |
| Suivi | `Tracking` | fichier `.suivi/fiches/<id>.json` | État, compteurs et historique d'un élément révisable. |
| Historique | `History` | `historique` | Événements en ajout seul ; jamais modifiés ni effacés. |
| Événement | `TrackingEvent` | `evt` | Révision, report, interruption, archivage, réactivation, rappel, inclusion, modification. |
| Compteurs | `CycleCounters` | `compteurs` | Cycles commencés, terminés, interrompus. |
| Élément révisable | `ReviewItem` | — | Fiche, passage annoté ou définition. |
| Séance | `StudySession` | — | Enchaînement des éléments dus du jour. |
| Rappel actif, texte à trous | `ActiveRecall`, `Cloze` | `rappelActif` | Passages et gras masqués jusqu'au clic pendant la révision. |

## Consolidation

| Terme | Code | Clé JSON (format 1) | Définition |
|---|---|---|---|
| Rappel de consolidation | `ConsolidationReminder` | `rappel` | Relecture d'une fiche archivée, tirée au hasard. |
| Règles des rappels | `ReminderRules` | `rappels` | Délai minimal, repos, places par jour, attente maximale. |
| Tirage du jour | `DailyDraw` | `tirages` | Sélection quotidienne, enregistrée dans `rappels.json`. |
| Niveau de repos | `RestLevel` | `niveauRepos` | Index dans la séquence de repos (2, 4, 8, 12 mois). |
| Éligible le | `EligibleOn` | `eligibleLe` | Date à partir de laquelle la fiche peut être tirée. |
| Fiche connue | `Known` | `connue` | Issue : repos plus long. |
| Certains points à revoir | `SomePointsToReview` | `certains_points_a_revoir` | Issue : repos remis à zéro, passages marqués à J0. |
| À revoir entièrement | `ReviewEntirely` | `a_revoir_entierement` | Issue : nouveau cycle. |

## Contenu

| Terme | Code | Clé JSON (format 1) | Définition |
|---|---|---|---|
| Coffre | `Vault` | `.suivi/coffre.json` | Dossier qui contient fiches, suivis et réglages. |
| Fiche | `Sheet` | fichier `fiches/**.md` | Fiche de révision en Markdown, avec en-tête YAML. |
| Dossier | `Folder` | — | Sous-dossier de `fiches/`. |
| Étiquette | `Tag` | `etiquettes` (ou `tags`) | Mot-clé de classement, écrit dans l'en-tête. |
| Ressource | `Attachment` | dossier `<Titre>.ressources/` | Image ou pièce jointe d'une fiche. |
| Passage annoté | `Passage` | `annotations` | Surlignage, avec ou sans commentaire, révisé par la méthode des J. |
| Définition | `Definition` | `definition` | Annotation qui porte un terme et son explication. |
| Annotation | `Annotation` | `.suivi/annotations/<id>.json` | Passage ou définition rattaché à une fiche par son identifiant. |
| Ancre | `Anchor` | `ancre` | Citation exacte, contexte et position dans le texte rendu. |
| État d'ancrage | `AnchorState` | `etatAncrage` | `ancree` (`Anchored`), `a_verifier` (`NeedsReview`), `introuvable` (`NotFound`). |
| Texte rendu | `RenderedText` | — | Projection du Markdown sur laquelle les ancres sont comptées. |
| Empreinte | `Fingerprint` | `empreinte` | `sha256:` des octets bruts d'un fichier. |
| Modèle de fiche | `SheetTemplate` | dossier `modeles/` | Gabarit proposé à la création d'une fiche. |
| Citation | `Quote` | dossier `citations/` | Citation culturelle (module facultatif). |
| Citation du jour | `DailyQuote` | — | Celle trouvée en ligne ce jour-là, sinon une citation du coffre tirée au hasard du jour. |
| Citation trouvée en ligne | `OnlineQuoteSearch` | `.suivi/citation-en-ligne.json` | Citation du jour retenue sur le Wikiquote francophone, rangée dans `citations/` (`origine: wikiquote`). |
| Schéma réseau | `NetworkDiagram` | dossier `schemas/` | Topologie réseau citée par les fiches (module facultatif). |

## Coffre et fichiers

| Terme | Code | Clé JSON (format 1) | Définition |
|---|---|---|---|
| Verrou | `VaultLock` | `.suivi/verrou` | Empêche deux instances d'ouvrir le même coffre. |
| Journal d'opération | `OperationJournal` | `.suivi/operation.json` | Opération multi-fichiers rejouée si elle a été interrompue. |
| Version | `SheetVersion` | `.suivi/versions/` | Copie d'une fiche avant écrasement. |
| Corbeille | `Trash` | `fiches/.supprimees/` | Éléments supprimés, restaurables, jamais purgés. |
| Sauvegarde | `Backup` | `Sauvegardes/coffre-*.zip` | Copie zip vérifiée du coffre, hors du coffre. |
| Santé du coffre | `VaultHealth` | — | Diagnostic sans réparation silencieuse. |
| Rotation | `RotationPolicy` | — | Copies conservées : 14 quotidiennes, 8 hebdomadaires, 24 mensuelles. |
| Restauration | `Restore` | `Coffre (restauré le AAAA-MM-JJ)` | Sauvegarde remise dans un nouveau dossier, jamais par-dessus le coffre. |
| Export | `Export` | `kyfRev - suivi AAAA-MM-JJ.csv`… | Fichier écrit hors du coffre, sans jamais écraser. |
| Partage | `ShareSheet` | — | Une fiche en Markdown, archive avec images, texte ou page web. |
| Import | `ImportFolder` | `fiches/<nom>` | Copie d'un dossier de notes (Obsidian…), liens `[[…]]` convertis. |
| Lien cassé | `BrokenLink` | — | Lien d'une fiche vers un fichier absent du coffre. |
| Garder l'original | `KeepOriginal` | — | Doublon résolu : les copies reçoivent un identifiant neuf. |
| Examen | `Exam` | `examens` | Date d'examen d'un dossier. |
| Resserrer | `Tighten` | — | Ramener les échéances avant la date d'examen. |
| Réglages | `VaultSettings` | `.suivi/coffre.json` | Paramètres propres au coffre. |
| Module | `Module` | `modules` | Fonction interne activable par coffre (citations, schémas). |
| Extension | `Extension` | `extension.json` | Code tiers isolé qui ajoute des blocs ou une vue. |

## Application

| Terme | Code | Clé JSON (format 1) | Définition |
|---|---|---|---|
| Cas d'usage | `*UseCases` (`ReviewUseCases.Validate`…) | — | Une opération de l'application, reprise d'une commande de la 0.x (`[ZeroXCommand]`). |
| Port | `IVault`, `IQuoteStore`, `IVaultStore`, `IBackupFiles`, `IRecentVaults`, `IOnlineReader` | — | Ce dont les cas d'usage ont besoin, fourni par l'infrastructure. |
| Issue | `Outcome<T>` | — | Valeur rendue, ou refus avec un message prêt à afficher. |
| Refus | `OperationException` | — | Opération refusée par un port, avec le message de la 0.x. |
| Présent | `Present` | — | Jour local, instant exact et heure locale, lus sur l'horloge injectée. |
| Fiche ouverte | `OpenedSheet` | — | Fiche lue pour la lecture ou la révision ; l'ouvrir ne valide rien. |
| État de révision | `ReviewState` | — | Où en est une fiche ou un passage dans son calendrier, un jour donné. |
| Module | `IKyfRevModule`, `ModuleCatalog` | réglages du coffre (`citations.active`…) | Fonction facultative compilée avec l'application : ses services ne sont déclarés que si le coffre l'active. |
| Point d'extension | `INavigationContributor`, `IDashboardWidget`, `IVaultHealthCheck`, `ISearchProvider`, `IExportFormat`, `IBlockRenderer` | — | Ce qu'un module peut apporter : entrée du menu, encart du tableau de bord, remarque de santé, résultats de recherche, export, rendu d'un bloc. |
| Lecteur en ligne | `IOnlineReader`, `OnlineReader` | — | Seul accès au réseau : HTTPS, ni redirection ni cookie, réponse bornée en taille et en durée. |
