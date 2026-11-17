# Parité fonctionnelle avec la 0.8.0 : relevé sur la liste du §1.1

Relevé du 17 novembre 2026 pour le bilan de J11 (`docs/plan-de-refonte.md`, §6), fait par lecture du code et des essais
de kyfRev 1.1.0-beta.2, sans rien lancer. Source de la liste : le tableau du §1.1 du plan, dont chaque ligne est découpée
en fonctions (92 au total). Le code de la 0.8.0 n'est pas dans ce dépôt : la comparaison porte sur le libellé du §1.1.
Les essais cités existent tous sous ces noms ; ils n'ont pas été relancés pour ce relevé, la CI les lance à chaque
demande de fusion.

États employés :

- **présente** : le code qui la réalise et au moins un essai qui l'exerce ont été trouvés ;
- **présente, sans essai trouvé** : le code est là, je n'ai pas trouvé d'essai ;
- **partielle** : ce qui manque est dit ;
- **absente** : rien trouvé ;
- **abandonnée par décision** : le plan ou le CHANGELOG dit qu'elle est retirée ou remplacée.

Abréviations des chemins : `Domain.Tests` = `tests/KyfRev.Domain.Tests`, `App.Tests` = `tests/KyfRev.Application.Tests`,
`Vault.Tests` = `tests/KyfRev.Infrastructure.Vault.Tests`, `Wpf.Tests` = `tests/KyfRev.Presentation.Wpf.Tests`,
`Win.Tests` = `tests/KyfRev.Infrastructure.Windows.Tests`, `Ext.Tests` = `tests/KyfRev.Extensions.Host.Tests`,
`Accept.Tests` = `tests/KyfRev.Acceptance.Tests`, `Wpf` = `src/KyfRev.Presentation.Wpf`,
`Vault` = `src/KyfRev.Infrastructure.Vault`, `Application` = `src/KyfRev.Application`.

Preuve générale : `App.Tests/Architecture/CommandMapTests.cs : Every_command_of_version_0x_has_a_use_case_or_a_milestone`
vérifie que les 97 commandes de la 0.x ont chacune un cas d'usage (`docs/correspondance-commandes.md`), et
`Vault.Tests/Scenarios/ScenarioTests.cs : Operations_give_the_same_vault_as_version_0x` compare le coffre produit à
celui de la 0.x.

## 1. Révision (cœur)

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Calendrier des J par élément | présente | `Domain.Tests/ScenarioTests.cs : Example_1_punctual_cycle_lasts_55_days`, `Example_1_late_review_shifts_the_rest_of_the_calendar` ; `Domain.Tests/DifferentialTests.cs : Ten_thousand_operation_sequences_match_the_rust_core_step_by_step` ; `src/KyfRev.Domain/Reviews/Tracking.cs`, `Cycle.cs` | Comparé pas à pas au noyau Rust. |
| Valider | présente | `Wpf.Tests/ReviewTests.cs : Validating_moves_to_the_next_step_and_says_so` ; `Application/UseCases/ReviewUseCases.cs : ReviewUseCases.Validate` ; `Tracking.ValidateReview` | |
| Valider antidaté | présente | `Domain.Tests/ScenarioTests.cs : Backdated_and_early_validations` ; `Wpf.Tests/ReviewTests.cs : A_review_done_on_an_earlier_day_counts_from_that_day` ; `App.Tests/UseCaseTests.cs : A_review_can_be_dated_from_the_day_after_the_last_one_to_today` | La 0.x ne le proposait pas à son interface ; la barre de révision le propose. |
| Valider en avance | présente | `Domain.Tests/ScenarioTests.cs : Backdated_and_early_validations` ; `Wpf.Tests/ReviewTests.cs : An_early_review_is_confirmed_first` | |
| Reporter | présente | `Domain.Tests/ScenarioTests.cs : T10_postponement_then_validation` ; `Accept.Tests/Scenarios.cs : T10_a_postponed_review_counts_from_the_day_it_is_done` ; `ReviewUseCases.Postpone` | |
| Recommencer à J0 | présente | `Domain.Tests/ScenarioTests.cs : Restarting_an_active_cycle_interrupts_it` ; `Wpf.Tests/ReviewTests.cs : Postponing_restarting_archiving_and_reminders` ; `ReviewUseCases.Restart` | |
| Archiver | présente | `Domain.Tests/ScenarioTests.cs : Manual_archiving_and_exclusion_from_reminders` ; `Wpf.Tests/ContextMenusTests.cs : Archiving_a_line_waits_for_its_confirmation` ; `ReviewUseCases.Archive` | |
| Séance du jour enchaînée | présente | `Wpf.Tests/SessionTests.cs : A_session_follows_the_items_and_ends_with_a_summary`, `An_interrupted_session_is_kept_and_resumed_the_same_day` ; `Wpf/Pages/StudySessionViewModel.cs` | |
| Rappel actif : texte à trous | présente | `Wpf.Tests/TextHolesTests.cs : Passages_and_bold_are_holes_in_the_order_of_the_text`, `The_page_revises_with_holes_only_if_active_recall_is_chosen` ; `Wpf/Pages/TextHoles.cs` | |
| Rappel actif : passage caché | présente | `Wpf.Tests/ReviewTests.cs : With_active_recall_the_passage_is_a_hole_in_its_context`, `The_cloze_cuts_the_context_as_in_the_0x`, `The_passage_page_reveals_the_hole` ; `Wpf/Pages/ReviewPages.cs : Cloze`, `PassagePageViewModel` | |
| Rappel actif : carte mémoire | présente | `Wpf.Tests/ReviewTests.cs : A_definition_is_a_memory_card` ; `Wpf.Tests/SessionTests.cs : Passages_of_a_session_open_as_memory_cards_and_see_in_sheet_comes_back` | |

## 2. Éléments révisables

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Fiche entière | présente | `Wpf.Tests/ReviewTests.cs : A_sheet_shows_its_text_and_its_review_bar` ; `Accept.Tests/Scenarios.cs : T2_the_J30_review_archives_the_sheet_and_leaves_its_passages_due` | |
| Passage annoté (surlignage, avec ou sans commentaire) | présente | `Wpf.Tests/HighlightingTests.cs : Highlighting_a_selection_creates_the_passage_and_marks_it`, `Annotating_asks_for_a_comment_and_cancelling_creates_nothing` ; `AnnotationUseCases.Create`, `ModifyComment` | |
| Définition (terme + explication) | présente | `Wpf.Tests/HighlightingTests.cs : Defining_proposes_the_selection_and_needs_an_explanation` ; `Wpf.Tests/DefinitionsTests.cs : The_glossary_lists_the_definitions_by_folder_then_term` ; `AnnotationUseCases.CreateDefinition` | |
| Calendrier propre à chaque élément | présente | `Domain.Tests/ScenarioTests.cs : Example_2_annotated_passage_has_its_own_calendar` ; `Accept.Tests/Scenarios.cs : T11_a_new_sheet_and_a_new_passage_are_due_the_next_day`, `T5_a_marked_passage_restarts_alone_and_the_sheet_stays_archived` | Pour la définition, le calendrier propre se lit dans `Wpf.Tests/DefinitionsTests.cs : A_definition_without_a_next_step_says_only_its_place` ; pas d'essai de domaine qui lui soit réservé (elle est suivie comme un passage). |

## 3. Archives et consolidation

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Archivage en fin de cycle | présente | `Domain.Tests/ScenarioTests.cs : T2_end_of_cycle_archives_with_history_intact` ; `Accept.Tests/Scenarios.cs : T2_the_J30_review_archives_the_sheet_and_leaves_its_passages_due` | |
| Archivage manuel | présente | `Domain.Tests/ScenarioTests.cs : Manual_archiving_and_exclusion_from_reminders`, `Manual_archiving_follows_the_include_manual_archiving_rule` ; `Tracking.ArchiveManually` | |
| Rappels tirés au hasard après 2 mois calendaires | présente | `Domain.Tests/ScenarioTests.cs : T3_no_reminder_before_two_calendar_months`, `T3_end_of_month_edge_cases` ; `Domain.Tests/ReminderDrawTests.cs : A_sheet_is_never_drawn_before_it_is_eligible`, `The_draw_is_reproducible_and_independent_of_candidate_order` ; `Domain.Tests/DifferentialTests.cs : Daily_reminder_draws_match_the_rust_core` ; `src/KyfRev.Domain/Reviews/ReminderDraw.cs` | |
| Repos progressif 2 → 4 → 8 → 12 mois | présente | `Domain.Tests/ScenarioTests.cs : T4_known_sheet_stays_archived_and_rests_longer`, `Rest_level_stops_at_the_last_rest_duration` ; `src/KyfRev.Domain/Reviews/ReminderRules.cs : ReminderRules.Default` (`[2, 4, 8, 12]`) | |

## 4. Tableau de bord

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| En retard, Aujourd'hui, Rappels, À venir (7 j) | présente | `Wpf.Tests/DashboardTests.cs : The_list_to_review_comes_first_and_upcoming_follows`, `Empty_states_warnings_and_reminders_read_as_in_the_0x` ; `App.Tests/UseCaseTests.cs : The_dashboard_prepares_the_day_and_lists_what_to_review` ; `Application/Model/Dashboard.cs : Dashboard` (`Overdue`, `Today`, `Reminders`, `Upcoming`, `UpcomingDays = 7`) | |
| Charge des 30 prochains jours (échéances connues et projetées) | présente | `Wpf.Tests/WorkloadTests.cs : There_is_a_column_for_today_and_the_thirty_days_after`, `Known_due_dates_stack_under_projected_steps_on_the_same_day` ; `Domain.Tests/ScenarioTests.cs : Load_forecast_assumes_each_review_is_done_on_its_due_date` ; `Vault/VaultSession.Dashboard.cs` | |
| Déplacement d'une échéance depuis la charge | présente | `Wpf.Tests/WorkloadTests.cs : A_due_date_moves_to_a_lighter_day_and_the_load_is_read_again` ; `Wpf/Pages/WorkloadPageViewModel.cs` | |
| Examens par dossier | présente | `Wpf.Tests/ExamsTests.cs : Fixing_an_exam_asks_a_name_and_a_day`, `Withdrawing_is_confirmed_and_leaves_the_calendars_alone` ; `ExamUseCases.Define`, `Withdraw` | |
| « Resserrer » | présente | `Wpf.Tests/ExamsTests.cs : Tightening_brings_the_due_dates_before_the_exam_after_confirmation` ; `ExamUseCases.Tighten` | |

## 5. Fiches et organisation

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Arborescence de dossiers | présente | `Wpf.Tests/SheetActionsTests.cs : The_folders_nest_and_empty_ones_are_shown`, `A_folder_is_created_renamed_and_moved_and_too_deep_is_only_a_warning` ; `FolderUseCases` | |
| Étiquettes (`etiquettes` / `tags`) | présente | `Vault.Tests/FrontMatterTests.cs : Tags_from_both_keys_are_normalized` ; `Wpf.Tests/SheetActionsTests.cs : The_tags_are_written_as_a_list` ; `Vault/Sheets/FrontMatter.cs` ; `SheetUseCases.SetTags` | |
| Glisser-déposer | présente | `Wpf.Tests/SheetDragTests.cs : Dropping_moves_the_sheet_and_says_so`, `Dropping_moves_a_folder_with_what_it_holds` ; `Wpf/Pages/SheetsPage.xaml.cs`, `SheetsPageViewModel.Actions.cs` | |
| Renommer avec réécriture des liens entrants | présente | `App.Tests/PlanScenarioTests.cs : T13_renaming_or_moving_a_sheet_keeps_annotations_tracking_images_and_incoming_links` ; `Vault.Tests/BacklinksTests.cs : The_list_follows_edits_renames_and_moves` ; `Vault/Links/MarkdownLinks.cs : MarkdownLinks.Rewrite` | |
| Déplacer avec réécriture des liens entrants | présente | mêmes essais T13 ; `Wpf.Tests/SheetActionsTests.cs : A_sheet_moves_to_any_folder_and_to_the_root` ; `Vault/VaultSession.Organisation.cs` | |
| Corbeille restaurable | présente | `Wpf.Tests/SettingsHubTests.cs : What_was_deleted_is_listed_and_comes_back` ; `Vault.Tests/VaultSessionTests.cs : The_trash_tolerates_missing_or_damaged_files` ; `TrashUseCases.RestoreSheet`, `AnnotationUseCases.Restore` | |
| Corbeille jamais purgée | abandonnée par décision | `docs/plan-de-refonte.md` l. 172 : « Règle "rien ne s'efface", amendée le 4 novembre 2026 […] j'ai demandé une suppression définitive : […] toujours confirmée, jamais automatique » ; CHANGELOG l. 610 | Rien n'est purgé automatiquement ; la suppression définitive, confirmée, existe (`TrashUseCases.PurgeSheet`, `Wpf.Tests/SheetActionsTests.cs : An_element_of_the_trash_is_erased_only_after_a_yes`). |
| Modèles de fiche | présente | `Wpf.Tests/SheetsTests.cs : A_template_fills_the_new_sheet` ; `Wpf.Tests/SharingTests.cs : A_template_takes_the_sheet_title_by_default_and_says_where_it_went` ; `Vault/VaultSession.Templates.cs` ; `TemplateUseCases` | |

## 6. Écriture

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Éditeur visuel, aller-retour Markdown fidèle à l'octet | présente | `Wpf.Tests/VisualEditorTests.cs : Typing_in_the_text_saves_markdown_and_nothing_else_changes`, `Coming_and_going_with_the_code_loses_nothing`, `Untouched_extended_format_stays_byte_for_byte` ; `tools/visual-editor/src/markdown.test.ts : « redonne exactement le texte d'origine »` ; `Wpf/Editing/VisualEditor.cs` | |
| Éditeur Markdown | présente | `Wpf.Tests/WritingTests.cs : Writing_shows_the_code_on_the_left_and_its_rendering_on_the_right` ; `Wpf.Tests/MarkdownEditingTests.cs` ; `Wpf/Editing/MarkdownEditor.cs` | |
| Liens `[[` | présente | `Wpf.Tests/WritingTests.cs : Double_brackets_propose_the_other_sheets_and_a_choice_writes_an_ordinary_link` ; `Wpf.Tests/VisualEditorTests.cs : Double_brackets_propose_the_sheets_in_the_text_too` ; `Wpf/Editing/SheetLinks.cs` | |
| Images | présente | `Wpf.Tests/WritingTests.cs : Chosen_images_go_next_to_the_sheet_and_their_links_into_the_text`, `A_dropped_image_lands_where_it_is_dropped` ; `EditingUseCases.AddImage` | |
| Formules | présente | `Wpf.Tests/WritingTests.cs : Rule_and_formula_are_written_as_markdown`, `The_preview_draws_a_formula_and_shows_what_it_cannot_draw` ; `Wpf/Reading/FormulaPreview.cs` | Voir « KaTeX » en lecture pour ce qui se dessine. |
| Galerie Mermaid | présente | `Wpf.Tests/WritingTests.cs : The_gallery_shows_its_models_by_category_and_previews_the_chosen_one`, `The_gallery_writes_a_code_block_in_the_markdown` ; `Wpf/Editing/DiagramGalleryViewModel.cs` ; `src/KyfRev.Rendering/Diagrams/MermaidTemplates.cs` | |
| Versions | présente | `Wpf.Tests/WritingTests.cs : The_history_compares_a_version_with_the_text_and_restores_it` ; `App.Tests/UseCaseTests.cs : Editing_and_versions` ; `EditingUseCases.Versions`, `RestoreVersion` | |
| Conflits | présente | `Wpf.Tests/WritingTests.cs : A_change_on_disk_is_a_conflict_that_writes_nothing`, `Keeping_my_version_puts_the_disk_one_in_the_history` ; `Accept.Tests/Scenarios.cs : T14_a_change_on_disk_while_writing_loses_nothing` | |
| Enregistrement automatique | présente | `Wpf.Tests/WritingTests.cs : A_key_stroke_is_saved_after_a_pause_with_one_version_kept_per_session`, `Leaving_the_view_or_closing_the_window_saves_the_last_stroke` ; `Wpf/Editing/SheetEditorViewModel.cs` | |

## 7. Lecture

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Rendu Markdown | présente | `Wpf.Tests/ReadingTests.cs : Every_construction_gets_its_native_form` ; `tests/KyfRev.Rendering.Tests/SheetRendererTests.cs` ; `Accept.Tests/Scenarios.cs : A_sheet_reads_as_a_document_and_no_longer_as_markdown` ; `Wpf/Reading/DocumentBuilder.cs` | Rendu natif (plan §2.6). |
| KaTeX (formules en lecture) | partielle | `Wpf.Tests/ReadingTests.cs : A_formula_is_drawn_and_one_the_engine_does_not_know_stays_readable` ; `Wpf/Reading/DocumentBuilder.cs : FormulaOrText` (WpfMath) | Moteur remplacé par décision (plan l. 371 : « XAML-Math en natif, avec repli sur le moteur web caché pour les commandes qu'il ne couvre pas »). Le repli annoncé n'a pas été trouvé : une commande inconnue de WpfMath reste affichée en source TeX (`FormulaAsText`). Seules `\tfrac`, `\dfrac` et `\ce{…}` sont traduites. La couverture de KaTeX n'est donc pas égalée. |
| Mermaid isolé | présente | `Wpf.Tests/VisualEditorTests.cs : The_reading_shows_the_picture_of_a_schema`, `The_engine_is_closed_to_the_outside`, `A_drawing_that_never_ends_stops_the_page_and_the_next_one_starts_it_again` ; `Wpf/Editing/MermaidEngine.cs`, `Wpf/Reading/MermaidView.cs` | Dessiné dans une page WebView2 cachée, rendu en image. |
| Plan | présente | `Wpf.Tests/ReadingTests.cs : The_outline_and_the_passages_describe_the_sheet`, `Clicking_the_outline_brings_the_part_to_the_top_and_marks_it` ; `Wpf/Pages/SheetReading.cs : OutlineEntry` | |
| Rétroliens | présente | `Vault.Tests/BacklinksTests.cs : A_sheet_is_led_to_by_those_that_link_to_it` ; `Wpf.Tests/BacklinksTests.cs : The_sheets_that_lead_here_are_listed_with_the_text_around_their_link` ; `SheetUseCases.Backlinks` | |
| Aperçu au survol | présente | `Wpf.Tests/BacklinksTests.cs : Pointing_at_a_link_shows_its_preview_after_a_moment_and_leaving_hides_it` ; `Vault.Tests/BacklinksTests.cs : A_preview_gives_the_start_of_the_sheet_and_its_step` ; `Wpf/Reading/LinkHover.cs` ; `SheetUseCases.Preview` | |
| Mode concentration | présente | `Wpf.Tests/ShellWindowTests.cs : Focus_mode_hides_the_menu_and_zoom_scales_the_workspace` ; `Wpf.Tests/ReviewTests.cs : The_focus_mode_keeps_the_reading_width_and_the_sheet_commands_it` ; `Wpf/Shell/ShellViewModel.cs : IsFocusMode` | |
| Confort de lecture | présente | `Wpf.Tests/GeneralSettingsTests.cs : The_text_of_a_sheet_follows_the_comfort_and_a_link_stays_underlined`, `The_comfort_says_what_the_preferences_say` ; `Wpf/Reading/ReadingComfort.cs` | Taille, interligne et police. Je n'ai pas la liste des réglages de la 0.8.0 pour comparer un à un. |
| Lecture à voix haute | présente | `Wpf.Tests/ReadAloudTests.cs : It_reads_block_after_block_then_stops` ; `Win.Tests/WindowsSpeechTests.cs : The_voices_are_french_and_sorted` ; `Wpf/Pages/ReadAloudViewModel.cs` ; `src/KyfRev.Infrastructure.Windows/WindowsSpeech.cs` | |

## 8. Recherche

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Dans les fiches (tous les mots, sans casse ni accents) | présente | `App.Tests/SearchWordsTests.cs : Each_word_must_be_in_the_title_or_in_the_text`, `A_text_is_compared_without_case_accents_ligatures_or_curly_apostrophes` ; `Vault.Tests/VaultSearchTests.cs : The_text_is_searched_without_case_or_accents` ; `Wpf.Tests/SheetsSearchTests.cs` ; `Application/Text/SearchWords.cs` ; `SheetUseCases.Search` | |
| Recherche globale Ctrl+K | présente | `Wpf.Tests/GlobalSearchTests.cs : The_results_come_by_group_with_their_place_their_nature_and_their_excerpt` ; `Wpf.Tests/ShellWindowTests.cs : Every_shortcut_reaches_its_command` ; `App.Tests/SearchUseCasesTests.cs` ; `SearchUseCases.Everywhere` ; `Wpf/Shell/GlobalSearch.cs` | |

## 9. Statistiques

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Ponctualité | présente | `Vault.Tests/StatisticsTests.cs : Reviews_count_on_the_day_they_are_validated_with_their_delay` ; `Wpf.Tests/StatisticsTests.cs : The_key_figures_read_like_those_of_0x` ; `Application/Model/Statistics.cs` (`OnTime`, `Early`, `Late`) | |
| Série | présente | `Vault.Tests/StatisticsTests.cs : A_streak_runs_until_yesterday_because_the_day_is_not_over` | |
| Carte de chaleur sur 52 semaines | présente | `Vault.Tests/StatisticsTests.cs : The_calendar_only_counts_its_52_weeks_but_the_records_stay` ; `Wpf.Tests/StatisticsTests.cs : The_calendar_goes_from_monday_to_the_end_of_this_week`, `The_level_of_a_day_goes_up_to_the_busiest_day` | |
| Par mois | présente | `Wpf.Tests/StatisticsTests.cs : The_details_skip_the_months_before_the_first_activity` ; `Statistics.ByMonth` | |
| Par dossier | présente | `Vault.Tests/StatisticsTests.cs : Folders_go_from_the_most_reviewed_then_by_name` ; `Statistics.ByFolder` | |
| Tout est recalculé depuis l'historique, rien n'est stocké | présente | `App.Tests/UseCaseTests.cs : The_statistics_read_the_history_of_the_vault` ; `Vault/VaultSession.Statistics.cs` (« Rien n'est écrit ») | Le recalcul est essayé ; l'absence d'écriture se lit dans le code, je n'ai pas trouvé d'essai qui la vérifie pour les statistiques. |

## 10. Citations (module)

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Fichiers `.md` | présente | `tests/KyfRev.Modules.Citations.Tests/CitationsModuleTests.cs : T19_a_module_turned_off_then_on_loses_no_quote_and_a_quote_needs_no_author_nor_date` ; `Vault.Tests/VaultAdapterTests.cs : Quotes_reach_the_session` ; `Vault/Quotes/QuoteFile.cs` ; `src/KyfRev.Modules.Citations/QuoteUseCases.cs` | |
| Citation du jour depuis Wikiquote | présente | `tests/KyfRev.Modules.Citations.Tests/WikiquoteTests.cs : Quotes_of_a_page_match_version_0x`, `Pages_of_the_day_match_version_0x` ; `OnlineQuoteSearchTests.cs : The_first_page_with_something_new_gives_the_quote_of_the_day` ; `src/KyfRev.Modules.Citations/Wikiquote.cs`, `OnlineQuoteSearch.cs` | |
| Stable dans la journée | présente | `tests/KyfRev.Modules.Citations.Tests/DailyQuoteTests.cs : The_quote_of_the_day_is_drawn_from_the_vault_and_kept_all_day` ; `src/KyfRev.Modules.Citations/DailyQuote.cs` | |

## 11. Schémas réseau (module)

Les essais de l'éditeur sont ceux de l'îlot repris de la 0.x, en TypeScript, dans `tools/schema-editor/src` (hors de `tests/`) ;
la CI les lance (`.github/workflows/verification.yml`, `npm test`).

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Éditeur de topologie : équipements | présente | `tools/schema-editor/src/lib/topologie/topologie.test.ts : « catalogue (D39) »` ; `Wpf.Tests/SchemasPageTests.cs : The_real_island_lists_creates_and_edits_a_schema` ; `Wpf/Schemas/SchemaBridge.cs` | |
| Ports | présente | `topologie.test.ts : « ports et stack (scénario 1) », « connexions (scénarios 2, 3, 4) »` ; `tools/schema-editor/src/lib/topologie/ports.ts` | |
| VLAN | présente | `tools/schema-editor/src/lib/topologie/vlans.test.ts : « VLAN des câbles (D51) »` ; `vlans.ts` | |
| Câbles routés | présente | `topologie.test.ts : « câbles coudés, alignement, annotations (D44, D45, D46) », « câbles coudés côte à côte (D52) », « points de passage d'un câble coudé (D55) »` | |
| Plan IP | présente | `tools/schema-editor/src/lib/topologie/outils.test.ts : « plan d'adressage IP (D47) »` ; `adressage.ts : planIp` ; `composants/topologie/PlanIp.svelte` | |
| Exports SVG, PNG et JSON | présente | `tools/schema-editor/src/lib/topologie/sorties.test.ts : « trois formats : deux images, et le modèle qui s'exporte toujours », « SVG autonome… »` ; `Wpf.Tests/SchemasPageTests.cs : The_bridge_exports_and_imports_through_the_pickers` ; `tests/KyfRev.Modules.Schemas.Tests/SchemaModuleTests.cs : An_export_goes_outside_the_vault_and_never_replaces_a_file` | |
| Rangés dans `schemas/*.json` | présente | `SchemaModuleTests.cs : A_schema_is_created_read_saved_and_copied` ; `Vault.Tests/InspectorTests.cs : Schemas_and_extension_data_are_checked` ; `Vault/VaultSession.Schemas.cs` ; `Vault/VaultLayout.cs : Schemas` | |
| Cités par les fiches | présente | `Wpf.Tests/NetworkBlockTests.cs : A_block_cites_a_schema_or_holds_an_old_one`, `A_sheet_reads_its_network_block_as_a_drawing` ; `SchemaModuleTests.cs : The_list_gives_titles_counts_and_the_sheets_that_cite_each_schema` | |

## 12. Échanges

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Partage : HTML autonome | présente | `Wpf.Tests/SharedSheetTests.cs : The_page_stands_alone_and_loads_nothing_from_anywhere` ; `Wpf/Reading/SharedSheet.cs` ; `SheetUseCases.Share` (`ShareFormat.Html`) | |
| Partage : PDF | présente | `Wpf.Tests/SharedSheetTests.cs : The_hidden_page_really_makes_a_pdf` ; `Vault.Tests/VaultBackupsTests.cs : A_shared_pdf_is_written_outside_the_vault_and_overwrites_nothing` ; `SheetUseCases.SharePdf` | |
| Partage : Markdown | présente | `App.Tests/UseCaseTests.cs : Trash_health_backups_exports_sharing_and_import` ; `Vault.Tests/VaultAdapterTests.cs : Health_backups_exports_sharing_and_import_reach_the_session` ; `Vault/VaultSession.Sharing.cs` | |
| Partage : zip (fiche et images) | présente | `Vault.Tests/VaultBackupsTests.cs : A_shared_archive_holds_only_the_files_the_sheet_shows` | |
| Partage : texte | présente | `Wpf.Tests/SharedSheetTests.cs : The_plain_text_reads_like_what_a_browser_gives` ; `Wpf.Tests/SharingTests.cs : The_clipboard_gets_the_page_and_the_text_without_any_folder` | |
| Export CSV (suivi et historique) | présente | `Vault.Tests/VaultBackupsTests.cs : Exports_of_the_reference_vault_are_those_of_version_0x`, `Exports_go_to_an_existing_folder_outside_the_vault` ; `Vault/VaultSession.Exports.cs` ; `ExportUseCases.Export` | |
| Export Markdown (annotations) | présente | `Vault.Tests/VaultBackupsTests.cs : Exports_of_the_reference_vault_are_those_of_version_0x` | |
| Export zip (coffre complet) | présente | `Wpf.Tests/SettingsHubTests.cs : An_export_refused_says_why` (cas `ExportKind.Vault`) ; `Vault.Tests/Scenarios/ScenarioTests.cs : Operations_give_the_same_vault_as_version_0x` (opération d'export « coffre ») | L'essai direct que j'ai lu porte sur le refus ; la réussite passe par les scénarios de la 0.x. |
| Import d'un dossier Obsidian | présente | `Vault.Tests/VaultImportTests.cs : Obsidian_callouts_highlights_and_comments_are_converted_outside_code`, `Obsidian_links_become_markdown_links_outside_code` ; `Wpf.Tests/FolderImportTests.cs` ; `Vault/ObsidianSyntax.cs`, `VaultSession.Import.cs` ; `SheetUseCases.Import` | |

## 13. Coffre

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Sauvegarde quotidienne zip vérifiée | présente | `Vault.Tests/VaultBackupsTests.cs : A_backup_is_verified_and_leaves_no_temporary_file` ; `App.Tests/ApplicationScenarioTests.cs : Opening_prepares_the_day_backs_up_once_a_day_and_remembers_the_vault` ; `Vault/Backups/VaultBackups.cs` | |
| Rotation 14 j / 8 sem. / 24 mois | présente | `Vault.Tests/VaultAdapterTests.cs : Backup_files_are_listed_described_rotated_and_restored` ; `Vault.Tests/VaultBackupsTests.cs : Rotation_keeps_everything_when_nothing_is_outdated` ; `Application/Model/Backups.cs : RotationPolicy(14, 8, 24)` ; `VaultBackups.ToKeep`, `ApplyRotation` | Les durées 14 / 8 / 24 sont dans le code ; je n'ai pas trouvé d'essai en C# qui vérifie chacune des trois durées (la rotation est aussi rejouée par les scénarios de la 0.x, avec des durées données par le scénario). |
| Restauration dans un nouveau dossier | présente | `Wpf.Tests/SettingsHubTests.cs : A_restored_backup_lands_beside_the_vault_and_can_be_opened` ; `Vault.Tests/VaultBackupsTests.cs : A_restore_skips_folder_entries_and_never_overwrites` ; `VaultBackups.Restore` | |
| Panneau Santé (rien n'est réparé en silence) | présente | `Wpf.Tests/HealthTests.cs : Problems_are_counted_and_nothing_is_repaired_without_the_person`, `Each_kind_of_problem_has_its_section_like_in_0x` ; `Vault.Tests/InspectorTests.cs : Inspecting_writes_nothing_in_the_vault` ; `MaintenanceUseCases.Health` ; `Vault/Analysis/VaultInspector.cs` | |
| Verrou entre instances | présente | `Vault.Tests/FileSafetyTests.cs : The_lock_excludes_a_second_opening_and_names_the_holder` ; `Vault.Tests/VaultSessionTests.cs : A_vault_opened_twice_names_who_holds_it` ; `Vault/Files/VaultLock.cs` | Verrou de fichier, compatible avec celui de Rust ; il vaut sur un même poste. |

## 14. Extensions

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Blocs ajoutés par des extensions | présente | `Wpf.Tests/ExtensionPageTests.cs : A_block_of_a_sheet_is_drawn_by_its_extension_and_stays_text_without_it` ; `Wpf.Tests/ExtensionEngineTests.cs : Quiz_draws_its_block_and_its_view_and_keeps_its_score` ; `Wpf/Extensions/ExtensionBlocks.cs` | |
| Vues ajoutées par des extensions | présente | `Wpf.Tests/ExtensionPageTests.cs : The_menu_has_an_entry_for_each_active_extension_with_a_view` ; `Wpf/Extensions/ExtensionPageViewModel.cs` | |
| Extensions HTML/JS isolées | présente | `Wpf.Tests/ExtensionEngineTests.cs : An_extension_reaches_no_network`, `An_extension_touches_neither_the_page_nor_the_application`, `An_extension_cannot_load_the_code_of_another` ; `Ext.Tests/ExtensionLibraryTests.cs : The_four_published_extensions_are_read_as_they_are`, `The_first_launch_takes_the_extensions_of_the_old_version_without_touching_them` ; `Wpf/Extensions/ExtensionEngine.cs` | Chaque extension tourne dans un cadre « sandbox » ; son rendu passe par une liste blanche d'éléments natifs. |
| Catalogue vérifié par empreintes SHA-256 | présente | `Ext.Tests/CatalogueTests.cs : A_file_matches_only_its_size_and_fingerprint`, `A_file_that_differs_from_the_catalogue_stops_the_download_and_leaves_nothing`, `A_signed_catalogue_is_taken_with_its_extensions` ; `src/KyfRev.Extensions.Host/ExtensionCatalogue.cs`, `CatalogueReader.cs` | Le catalogue est de plus signé. |
| Jeton GitHub chiffré par DPAPI | abandonnée par décision | `docs/plan-de-refonte.md` l. 70 : « abandonné par l'amendement du 8 novembre 2026 : le dépôt est public » ; `docs/correspondance-commandes.md` l. 123 à 125 | Connu, non revérifié. |

## 15. Distribution

| Fonction de la 0.8.0 | État | Preuve (fichier : essai ou type) | Remarque |
|---|---|---|---|
| Version portable (`MesRévisions\`) | présente | `Win.Tests/VaultLocationsTests.cs : The_portable_vault_is_relative_only`, `Portable_and_development` ; `Wpf.Tests/WelcomeTests.cs : The_portable_welcome_offers_only_its_planned_vault` ; `tools/portable.ps1`, `tools/portable-tests.ps1` (lancé par la CI) ; `src/KyfRev.Infrastructure.Windows/VaultLocations.cs` | |
| Installeur NSIS | abandonnée par décision | `docs/plan-de-refonte.md` l. 44 : « Installateur : Inno Setup 6 » ; `installer/kyfRev.iss` ; `tools/installer-tests.ps1` (CI) | Remplacé par Inno Setup. Connu, non revérifié. |
| Signature de code facultative | présente, sans essai trouvé | `tools/installer.ps1` (paramètre `-Signature`, l. 23 à 31 et 108) ; `installer/kyfRev.iss` l. 99 à 102 (`#ifdef SignTool`) | Seul l'installateur (et son désinstallateur) peut être signé. Aucun essai n'emprunte ce chemin ; les fichiers publiés ne sont pas signés (`docs/guide-utilisateur.md` l. 18). |
| Recherche de mise à jour à la demande | présente | `Ext.Tests/UpdateCheckerTests.cs : A_newer_signed_version_is_announced_with_its_page_only` ; `Wpf.Tests/UpdateFlowTests.cs : The_button_downloads_checks_and_offers_to_restart_now_or_later` ; `Wpf.Tests/SettingsHubTests.cs : An_update_search_that_fails_says_why_and_offers_nothing` ; `src/KyfRev.Extensions.Host/UpdateChecker.cs` ; `Wpf/Shell/UpdateFlow.cs` | Étendue en 1.1.0-beta.2 : téléchargement et installation depuis l'application (connu, non revérifié). |

## Bilan

| État | Nombre |
|---|---|
| présente | 87 |
| présente, sans essai trouvé | 1 |
| partielle | 1 |
| absente | 0 |
| abandonnée par décision | 3 |
| **Total** | **92** |

### Ce qui n'est pas « présente »

- **KaTeX (formules en lecture)** : partielle. Le moteur est WpfMath, par décision (plan l. 371), mais le repli sur le moteur
  web caché, annoncé à la même ligne, n'a pas été trouvé : une commande que WpfMath ne connaît pas s'affiche en source TeX.
- **Signature de code facultative** : présente, sans essai trouvé. Le crochet existe pour l'installateur seulement.
- **Corbeille jamais purgée** : abandonnée par décision (amendement du 4 novembre 2026, plan l. 172). Aucune purge
  automatique ; suppression définitive confirmée.
- **Jeton GitHub chiffré par DPAPI** : abandonnée par décision (amendement du 8 novembre 2026, plan l. 70).
- **Installeur NSIS** : abandonnée par décision, remplacé par Inno Setup (plan l. 44).

### Réserves sur des fonctions classées « présente »

- Rotation 14 / 8 / 24 : les trois durées ne sont pas vérifiées une à une par un essai en C# que j'aie trouvé.
- Statistiques « rien n'est stocké » : lu dans le code, sans essai dédié.
- Export zip du coffre : l'essai direct lu porte sur le refus.
- Schémas réseau : les essais de l'éditeur sont dans `tools/schema-editor` (vitest), pas dans `tests/`.
- Confort de lecture : taille, interligne et police ; la liste des réglages de la 0.8.0 n'est pas dans ce dépôt.

### Ce que je n'ai pas pu vérifier, et pourquoi

- Aucun essai n'a été lancé ni l'application ouverte :
  « présente » veut dire qu'un essai existe et vise la fonction, pas qu'il passe aujourd'hui.
- Le code de la 0.8.0 n'est pas dans ce dépôt : le comportement exact de chaque fonction de la 0.8.0 est pris dans le tableau
  du §1.1 et dans les commentaires « comme dans la 0.x », sans comparaison avec l'ancienne application.
