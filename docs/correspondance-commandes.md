# Correspondance des commandes de kyfRev 0.x

Les 97 commandes que l'interface de kyfRev 0.x peut appeler (`src-tauri/src/lib.rs`), et ce qui
les reprend dans la génération .NET : un cas d'usage de `KyfRev.Application` ou d'un module, ou
le jalon qui l'apportera. Chaque cas d'usage porte l'attribut `[ZeroXCommand]` de sa commande ;
un test (`CommandMapTests`) vérifie que ce tableau et le code restent d'accord.

## Reprises par les cas d'usage (J4.1)

| Commande 0.x | Cas d'usage | Jalon | Remarque |
|---|---|---|---|
| `etat_application` | `VaultUseCases.State` | J4.1 | Mode (portable, installé, développement) et coffre prévu lus par `IVaultLocations` (J6.2). Au démarrage, `VaultUseCases.OpenAtStartup` reprend `ouvrir_au_demarrage`. |
| `ouvrir_coffre` | `VaultUseCases.Open` | J4.1 | Prépare la journée, fait la sauvegarde du jour et la rotation, retient le coffre. |
| `creer_coffre` | `VaultUseCases.Create` | J4.1 | |
| `definir_examen` | `ExamUseCases.Define` | J4.1 | |
| `retirer_examen` | `ExamUseCases.Withdraw` | J4.1 | |
| `resserrer_avant_examen` | `ExamUseCases.Tighten` | J4.1 | |
| `creer_fiche` | `SheetUseCases.Create` | J4.1 | |
| `modeles` | `TemplateUseCases.Templates` | J4.1 | |
| `enregistrer_modele` | `TemplateUseCases.SaveAsTemplate` | J4.1 | |
| `dossiers` | `FolderUseCases.Folders` | J4.1 | |
| `ouvrir_fiche` | `SheetUseCases.Open` | J4.1 | |
| `id_de_fiche` | `SheetUseCases.IdAt` | J4.1 | |
| `valider_revision` | `ReviewUseCases.Validate` | J4.1 | |
| `reporter_revision` | `ReviewUseCases.Postpone` | J4.1 | La date arrive typée : l'interface la valide. |
| `recommencer_cycle` | `ReviewUseCases.Restart` | J4.1 | |
| `archiver_fiche` | `ReviewUseCases.Archive` | J4.1 | |
| `creer_annotation` | `AnnotationUseCases.Create` | J4.1 | |
| `modifier_commentaire` | `AnnotationUseCases.ModifyComment` | J4.1 | |
| `creer_definition` | `AnnotationUseCases.CreateDefinition` | J4.1 | |
| `modifier_definition` | `AnnotationUseCases.ModifyDefinition` | J4.1 | |
| `reancrer_annotation` | `AnnotationUseCases.Reanchor` | J4.1 | |
| `modifier_passage` | `AnnotationUseCases.ModifyPassage` | J4.1 | |
| `mettre_annotation_a_la_corbeille` | `AnnotationUseCases.Trash` | J4.1 | |
| `restaurer_annotation` | `AnnotationUseCases.Restore` | J4.1 | |
| `importer_dossier` | `SheetUseCases.Import` | J4.1 | |
| `changer_titre_fiche` | `SheetUseCases.ChangeTitle` | J4.1 | |
| `modifier_etiquettes_fiche` | `SheetUseCases.SetTags` | J4.1 | |
| `deplacer_fiche` | `SheetUseCases.Move` | J4.1 | |
| `mettre_fiche_a_la_corbeille` | `TrashUseCases.TrashSheet` | J4.1 | |
| `restaurer_fiche_de_la_corbeille` | `TrashUseCases.RestoreSheet` | J4.1 | |
| `fiches_a_la_corbeille` | `TrashUseCases.TrashedSheets` | J4.1 | |
| `partager_fiche` | `SheetUseCases.Share` | J4.1 | |
| `creer_dossier` | `FolderUseCases.Create` | J4.1 | |
| `deplacer_dossier` | `FolderUseCases.Move` | J4.1 | |
| `mettre_dossier_a_la_corbeille` | `TrashUseCases.TrashFolder` | J4.1 | |
| `enregistrer_fiche` | `EditingUseCases.Save` | J4.1 | |
| `garder_en_version` | `EditingUseCases.KeepAsVersion` | J4.1 | |
| `versions` | `EditingUseCases.Versions` | J4.1 | |
| `lire_version` | `EditingUseCases.ReadVersion` | J4.1 | |
| `restaurer_version` | `EditingUseCases.RestoreVersion` | J4.1 | |
| `ajouter_image` | `EditingUseCases.AddImage` | J4.1 | |
| `traiter_rappel` | `ReminderUseCases.Handle` | J4.1 | |
| `inclure_dans_rappels` | `ReminderUseCases.Include` | J4.1 | |
| `rapport_sante` | `MaintenanceUseCases.Health` | J4.1 | |
| `analyser_coffre` | `MaintenanceUseCases.Analyze` | J4.1 | |
| `suivre_fichier` | `SheetUseCases.Track` | J4.1 | |
| `recreer_suivi` | `MaintenanceUseCases.RecreateTracking` | J4.1 | |
| `garder_original` | `MaintenanceUseCases.KeepOriginal` | J4.1 | |
| `restaurer_fiche` | `MaintenanceUseCases.RestoreFromBackup` | J4.1 | |
| `elements_a_la_corbeille` | `TrashUseCases.TrashedAnnotations` | J4.1 | |
| `reglages` | `SettingsUseCases.Get` | J4.1 | |
| `modifier_reglages` | `SettingsUseCases.Change` | J4.1 | |
| `sauvegardes` | `BackupUseCases.List` | J4.1 | |
| `sauvegarder_maintenant` | `BackupUseCases.BackupNow` | J4.1 | |
| `restaurer_sauvegarde` | `BackupUseCases.Restore` | J4.1 | |
| `exporter` | `ExportUseCases.Export` | J4.1 | |

## Reprises par le module Citations (J4.2)

Ces cas d'usage ne sont chargés que si le coffre active le module Citations.

| Commande 0.x | Cas d'usage | Jalon | Remarque |
|---|---|---|---|
| `citations` | `QuoteUseCases.Quotes` | J4.2 | |
| `ajouter_citation` | `QuoteUseCases.Add` | J4.2 | |
| `modifier_citation` | `QuoteUseCases.Modify` | J4.2 | |
| `mettre_citation_a_la_corbeille` | `QuoteUseCases.Trash` | J4.2 | |
| `restaurer_citation` | `QuoteUseCases.Restore` | J4.2 | |
| `chercher_citation_du_jour` | `QuoteUseCases.SearchQuoteOfTheDayAsync` | J4.2 | Par le lecteur HTTPS durci de `KyfRev.Infrastructure.Online`. |

## Reprises par les cas d'usage du parcours de révision (J6.1)

| Commande 0.x | Cas d'usage | Jalon | Remarque |
|---|---|---|---|
| `tableau_de_bord` | `ReminderUseCases.Dashboard` | J6.1 | La journée y est préparée de nouveau après minuit. La citation du jour vient du module Citations, comme encart du tableau de bord. |
| `archives` | `ReminderUseCases.Archives` | J6.1 | |

## Apportées par les jalons suivants

| Commande 0.x | Cas d'usage | Jalon | Remarque |
|---|---|---|---|
| `appliquer_theme` | — | J5 | Barre de titre de Windows : coquille WPF. |
| `appliquer_zoom` | — | J5 | Taille de l'interface : coquille WPF. |
| `journaliser_erreur_interface` | — | J5 | Journal de diagnostic. |
| `diagnostic` | — | J5 | Journal de diagnostic. |
| `corriger_mot` | — | J7 | Correcteur de Windows. |
| `ajouter_au_dictionnaire` | — | J7 | Correcteur de Windows. |
| `statistiques` | — | J9 | Statistiques et charge. |
| `retroliens` | — | J9 | Rétroliens. |
| `apercu_fiche` | — | J9 | Aperçu au survol. |
| `liste_des_fiches` | `SheetUseCases.List` | J7.1 | Vue « Fiches » ; l'arborescence reste à J9. |
| `chercher_dans_les_fiches` | — | J9 | Recherche dans les fiches. |
| `recherche_globale` | — | J9 | Recherche globale. |
| `liste_des_definitions` | — | J9 | Vue « Définitions ». |
| `schemas` | — | J10 | Module Schémas réseau. |
| `lire_schema` | — | J10 | Module Schémas réseau (le coffre sait déjà lire un schéma). |
| `creer_schema` | — | J10 | Module Schémas réseau (le coffre sait déjà créer un schéma). |
| `enregistrer_schema` | — | J10 | Module Schémas réseau (le coffre sait déjà l'enregistrer). |
| `dupliquer_schema` | — | J10 | Module Schémas réseau (le coffre sait déjà le copier). |
| `exporter_schema` | — | J10 | Module Schémas réseau. |
| `lire_import_schema` | — | J10 | Module Schémas réseau. |
| `rechercher_mise_a_jour` | — | J10 | Mise à jour avec manifeste signé. |
| `extensions` | — | J10 | Hôte des extensions. |
| `lire_extension` | — | J10 | Hôte des extensions. |
| `installer_extension` | — | J10 | Hôte des extensions. |
| `activer_extension` | — | J10 | Hôte des extensions. |
| `retirer_extension` | — | J10 | Hôte des extensions. |
| `donnees_extension` | — | J10 | Hôte des extensions (le coffre sait déjà lire ces données). |
| `enregistrer_donnees_extension` | — | J10 | Hôte des extensions (le coffre sait déjà les écrire). |
| `catalogue_extensions` | — | J10 | Catalogue signé. |
| `installer_extension_du_catalogue` | — | J10 | Catalogue signé. |
| `etat_jeton_github` | — | J10 | Jeton GitHub. |
| `enregistrer_jeton_github` | — | J10 | Jeton GitHub. |
| `oublier_jeton_github` | — | J10 | Jeton GitHub. |
