using KyfRev.Application.Model;
using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;

namespace KyfRev.Application.Ports;

/// <summary>
/// Le coffre ouvert, tel que les cas d'usage le voient. Chaque refus est une
/// <see cref="OperationException"/> qui porte le message de la version 0.x. Le fermer libère son
/// verrou.
/// </summary>
public interface IVault :
    IDisposable,
    ISheetStore,
    ISearchStore,
    ISheetEditor,
    IFolderStore,
    ITrashStore,
    IReviewStore,
    IAnnotationStore,
    IReminderStore,
    IExamStore,
    ISettingsStore,
    IHealthStore,
    IBackupWriter,
    IExporter,
    ITemplateStore,
    IQuoteStore,
    ISchemaStore,
    IExtensionDataStore
{
    /// <summary>Dossier du coffre.</summary>
    string Root { get; }

    /// <summary>Identifiant du coffre (<c>coffre.json</c>) : graine des tirages du jour.</summary>
    string Id { get; }
}

/// <summary>Fiches : création, lecture, titre, étiquettes, rangement, adoption, import et partage.</summary>
public interface ISheetStore
{
    /// <summary>Crée une fiche dans <paramref name="folder"/> (relatif à <c>fiches/</c>, rien : la racine) ; sa création vaut J0.</summary>
    /// <returns>Son identifiant.</returns>
    string CreateSheet(string? folder, string title, string body, Moment now);

    /// <summary>Ouvre une fiche présente pour la lire ou la réviser.</summary>
    OpenedSheet OpenSheet(string id, DateOnly day);

    /// <summary>Identifiant de la fiche située à <paramref name="path"/> (chemin dans le coffre) ; rien s'il n'y en a pas.</summary>
    string? SheetIdAt(string path);

    /// <summary>Toutes les fiches, par dossier puis par titre.</summary>
    IReadOnlyList<VaultSheet> SheetList();

    /// <summary>Les fiches qui mènent à la fiche <paramref name="id"/>, par titre.</summary>
    /// <exception cref="OperationCanceledException">La demande a été annulée.</exception>
    IReadOnlyList<Backlink> Backlinks(string id, CancellationToken cancellation);

    /// <summary>Le début de la fiche située à <paramref name="path"/> (chemin dans le coffre) ; rien s'il n'y en a pas.</summary>
    SheetPreview? PreviewOf(string path, DateOnly day);

    /// <summary>Le glossaire : les définitions hors corbeille, par dossier, puis par terme.</summary>
    IReadOnlyList<VaultDefinition> Definitions(DateOnly day);

    /// <summary>Change le titre : en-tête mis à jour, fichier renommé, liens réécrits.</summary>
    void ChangeTitle(string id, string title, Moment now);

    /// <summary>Remplace les étiquettes ; rend celles réellement enregistrées.</summary>
    IReadOnlyList<string> SetTags(string id, IReadOnlyList<string> tags, Moment now);

    /// <summary>Range la fiche dans <paramref name="folder"/> (rien : la racine).</summary>
    void MoveSheet(string id, string? folder, Moment now);

    /// <summary>Adopte un fichier <c>.md</c> sans identifiant (« Suivre cette fiche ») ; ce jour vaut J0.</summary>
    /// <returns>Son identifiant.</returns>
    string TrackFile(string path, Moment now);

    /// <summary>Importe un dossier de notes dans <c>fiches/&lt;nom&gt;</c>.</summary>
    ImportReport ImportFolder(string source, string name, bool track, Moment now);

    /// <summary>Écrit la fiche au format demandé dans un dossier hors du coffre ; rend le fichier écrit.</summary>
    string ShareSheet(string id, ShareFormat format, string folder, string? content);

    /// <summary>Écrit le PDF de la fiche, préparé par l'interface, dans un dossier hors du coffre ; rend le fichier écrit.</summary>
    string ShareSheetAsPdf(string id, string folder, ReadOnlyMemory<byte> pdf);

    /// <summary>Une page préparée pour le partage, ses images du coffre intégrées : rien d'autre n'en sort.</summary>
    string EmbedSharedImages(string page);
}

/// <summary>
/// Recherche dans le coffre : une lecture de bout en bout, par le stockage du coffre, en clair comme chiffré. Aucun index
/// n'est écrit, et rien de ce qui est cherché ou trouvé ne quitte le coffre. Annulée, une recherche rend ce qu'elle a déjà trouvé.
/// </summary>
public interface ISearchStore
{
    /// <summary>Les fiches qui contiennent tous les mots, dans leur titre, leurs étiquettes ou leur texte, par identifiant.</summary>
    IReadOnlyList<SheetHit> SearchSheets(string query, CancellationToken cancellationToken);

    /// <summary>Les passages et les définitions hors corbeille qui contiennent tous les mots, par titre de fiche, puis par terme ; rien sans mot.</summary>
    IReadOnlyList<AnnotationHit> SearchAnnotations(string query, CancellationToken cancellationToken);

    /// <summary>Les citations hors corbeille qui contiennent tous les mots, par auteur, puis par texte ; rien sans mot.</summary>
    IReadOnlyList<QuoteHit> SearchQuotes(string query, CancellationToken cancellationToken);
}

/// <summary>Édition : enregistrement avec détection de conflit, versions, ressources.</summary>
public interface ISheetEditor
{
    /// <summary>Enregistre le corps (l'en-tête est conservé), sauf si la fiche a changé depuis sa lecture.</summary>
    SaveResult SaveBody(string id, string body, string expectedFingerprint, ulong session, Moment now);

    /// <summary>
    /// Ajoute une note prise à la volée à la fiche « Boîte de réception », créée et suspendue au besoin ; rend son
    /// identifiant.
    /// </summary>
    string CaptureToInbox(string text, Moment now);

    /// <summary>Range un texte dans les versions de la fiche, sans toucher au fichier.</summary>
    void KeepAsVersion(string id, string body, Moment now);

    /// <summary>Versions gardées, de la plus récente à la plus ancienne.</summary>
    IReadOnlyList<SheetVersion> Versions(string id);

    /// <summary>Corps d'une version.</summary>
    string ReadVersion(string id, string name);

    /// <summary>Remet une version, sauf si la fiche a changé depuis sa lecture.</summary>
    SaveResult RestoreVersion(string id, string name, string expectedFingerprint, Moment now);

    /// <summary>Copie une ressource à côté de la fiche ; rend le lien relatif à insérer.</summary>
    string AddResource(string id, string name, ReadOnlySpan<byte> content);

    /// <summary>Contenu d'une ressource du dossier des fiches (une image), pour l'affichage.</summary>
    byte[] ReadResource(string path);
}

/// <summary>Dossiers de fiches.</summary>
public interface IFolderStore
{
    /// <summary>Dossiers, relatifs à <c>fiches/</c>.</summary>
    IReadOnlyList<string> Folders();

    /// <summary>Crée un dossier ; rend son chemin.</summary>
    string CreateFolder(string folder);

    /// <summary>Renomme ou déplace un dossier, ses fiches et leurs liens ; rend son nouveau chemin.</summary>
    string MoveFolder(string folder, string destination, Moment now);
}

/// <summary>Corbeille : rien n'y est jamais purgé, tout se restaure.</summary>
public interface ITrashStore
{
    /// <summary>Met une fiche, ses ressources, son suivi et ses annotations à la corbeille.</summary>
    void TrashSheet(string id, Moment now);

    /// <summary>Remet une fiche de la corbeille à sa place (ou à côté).</summary>
    void RestoreSheet(string id, Moment now);

    /// <summary>Fiches à la corbeille.</summary>
    IReadOnlyList<TrashedSheet> TrashedSheets();

    /// <summary>Met un dossier entier à la corbeille ; rend le nombre de fiches rangées.</summary>
    int TrashFolder(string folder, Moment now);

    /// <summary>Annotations à la corbeille, les plus récentes d'abord.</summary>
    IReadOnlyList<TrashedAnnotation> TrashedAnnotations();

    /// <summary>Efface pour de bon une fiche déjà à la corbeille, avec ses images, son suivi et ses annotations.</summary>
    void PurgeSheet(string id);

    /// <summary>Efface pour de bon un dossier et tout ce qu'il contient ; rend le nombre de fiches effacées.</summary>
    int PurgeFolder(string folder, Moment now);
}

/// <summary>Méthode des J d'une fiche ou d'un passage.</summary>
public interface IReviewStore
{
    /// <summary>
    /// Valide la révision attendue (une seule par élément et par jour). <paramref name="reviewDate"/> antidate la
    /// révision (« je l'ai révisée hier ») ; rien : aujourd'hui. Avec <see cref="ReviewRecall.Forgotten"/>, l'étape
    /// recule d'un cran au lieu d'avancer.
    /// </summary>
    ReviewOutcome ValidateReview(ItemKind kind, string id, Moment now, DateOnly? reviewDate = null, ReviewRecall recall = ReviewRecall.Remembered);

    /// <summary>Reporte l'échéance au jour <paramref name="newDueDate"/>.</summary>
    void Postpone(ItemKind kind, string id, Moment now, DateOnly newDueDate);

    /// <summary>Recommence le cycle à J0, avec la séquence du coffre.</summary>
    void Restart(ItemKind kind, string id, Moment now);

    /// <summary>Suspend un élément actif : il garde son étape et son échéance, mais ne se présente plus à réviser.</summary>
    void Suspend(ItemKind kind, string id, Moment now);

    /// <summary>Reprend un élément suspendu, là où il en était.</summary>
    void ResumeReview(ItemKind kind, string id);

    /// <summary>Fait d'une fiche active une simple note, tenue hors des révisions.</summary>
    void MakeNote(string id, Moment now);

    /// <summary>Met une note en révision : à J0 si elle n'a jamais été révisée, sinon là où elle en était.</summary>
    void PutInReview(string id, Moment now);

    /// <summary>Archive une fiche à la main.</summary>
    void ArchiveSheet(string id, Moment now);

    /// <summary>Où en est l'élément, au jour donné.</summary>
    ReviewState ReviewStateOf(ItemKind kind, string id, DateOnly day);

    /// <summary>Tableau de bord du jour donné.</summary>
    Dashboard Dashboard(DateOnly day);

    /// <summary>Statistiques de régularité au jour donné.</summary>
    Statistics Statistics(DateOnly day);
}

/// <summary>Passages annotés et définitions.</summary>
public interface IAnnotationStore
{
    /// <summary>Surligne un passage (commentaire facultatif) ; sa création vaut J0.</summary>
    string CreateAnnotation(string sheetId, Anchor anchor, string? comment, Moment now);

    /// <summary>Pose une définition sur un passage ; sa création vaut J0.</summary>
    string CreateDefinition(string sheetId, Anchor anchor, Definition definition, Moment now);

    /// <summary>Change le terme ou l'explication d'une définition.</summary>
    void ModifyDefinition(string id, Definition definition, Moment now);

    /// <summary>Marque le terme d'une définition dans toutes les fiches, ou seulement à sa place ; l'historique ne change pas.</summary>
    void MarkDefinitionEverywhere(string id, bool everywhere);

    /// <summary>Change ou retire le commentaire.</summary>
    void ModifyComment(string id, string? comment, Moment now);

    /// <summary>Enregistre un ancrage recalculé.</summary>
    void Reanchor(string id, Anchor anchor, AnchorState state, Moment now);

    /// <summary>Passage changé par l'utilisateur ; <paramref name="restart"/> repart de J0.</summary>
    void ModifyPassage(string id, Anchor anchor, bool restart, Moment now);

    /// <summary>Met une annotation à la corbeille.</summary>
    void TrashAnnotation(string id, Moment now);

    /// <summary>Remet une annotation de la corbeille.</summary>
    void RestoreAnnotation(string id, Moment now);

    /// <summary>Efface pour de bon une annotation déjà à la corbeille.</summary>
    void PurgeAnnotation(string id);
}

/// <summary>Rappels de consolidation.</summary>
public interface IReminderStore
{
    /// <summary>Enregistre la réponse à un rappel.</summary>
    void HandleReminder(string sheetId, ReminderOutcome outcome, Moment now);

    /// <summary>Exclut une fiche archivée des rappels, ou l'y réinclut.</summary>
    void IncludeInReminders(string sheetId, bool include, Moment now);

    /// <summary>Tirage du jour, fait une seule fois par jour.</summary>
    DayPreparation PrepareDay(Moment now);

    /// <summary>Fiches archivées, les plus récemment archivées d'abord.</summary>
    IReadOnlyList<ArchivedSheetEntry> Archives(DateOnly day);
}

/// <summary>Examens par dossier.</summary>
public interface IExamStore
{
    /// <summary>Fixe ou remplace l'examen d'un dossier (vide : tout le coffre).</summary>
    void DefineExam(string folder, string name, DateOnly day);

    /// <summary>Retire l'examen d'un dossier ; les calendriers ne changent pas.</summary>
    void WithdrawExam(string folder);

    /// <summary>Ramène avant l'examen les échéances qui tombent après ; rend leur nombre.</summary>
    int TightenBeforeExam(string folder, Moment now);

    /// <summary>Remet en révision les fiches archivées du dossier d'un examen ; rend leur nombre.</summary>
    int RestartArchivedBeforeExam(string folder, Moment now);

    /// <summary>
    /// Étale un retard sur les jours suivants, ou libère des jours de pause. Sans <paramref name="apply"/>, rien n'est
    /// écrit : c'est le plan. Appliqué, chaque échéance déplacée est un report ordinaire.
    /// </summary>
    SpreadPlan SpreadDueDates(SpreadRequest request, Moment now, bool apply);
}

/// <summary>Réglages du coffre.</summary>
public interface ISettingsStore
{
    /// <summary>Réglages tels que l'écran Paramètres les montre.</summary>
    VaultPreferences Preferences { get; }

    /// <summary>Enregistre les réglages, après vérification.</summary>
    void ChangePreferences(VaultPreferences preferences);
}

/// <summary>Santé du coffre et réparations.</summary>
public interface IHealthStore
{
    /// <summary>Rapport complet, liens cassés compris.</summary>
    HealthDetails HealthDetails();

    /// <summary>Analyse complète du coffre.</summary>
    void Analyze(Moment now);

    /// <summary>Recrée le suivi d'un fichier qui porte un identifiant sans fichier de suivi ; rend l'identifiant.</summary>
    string RecreateTracking(string path, Moment now);

    /// <summary>Doublon : <paramref name="original"/> garde l'identifiant, les copies en reçoivent un neuf ; rend ceux-ci.</summary>
    IReadOnlyList<string> KeepOriginal(string id, string original, Moment now);

    /// <summary>Recopie une fiche manquante depuis une sauvegarde ; rend le chemin où elle a été remise.</summary>
    string RestoreSheetFromBackup(string id, string backup, Moment now);
}

/// <summary>Sauvegarde du coffre ouvert.</summary>
public interface IBackupWriter
{
    /// <summary>Sauvegarde tout le coffre dans <paramref name="folder"/>, hors du coffre, puis la vérifie.</summary>
    BackupFile Backup(string folder, DateTime timestamp);
}

/// <summary>Exports, écrits hors du coffre sans jamais rien écraser.</summary>
public interface IExporter
{
    /// <summary>Écrit l'export dans <paramref name="folder"/> ; rend les fichiers créés.</summary>
    IReadOnlyList<string> Export(ExportKind kind, string folder, DateTime timestamp, TimeZoneInfo zone);
}

/// <summary>Modèles de fiche.</summary>
public interface ITemplateStore
{
    /// <summary>Modèles du coffre, puis les intégrés.</summary>
    IReadOnlyList<SheetTemplate> Templates();

    /// <summary>Corps d'une nouvelle fiche d'après un modèle (vide s'il est inconnu).</summary>
    string TemplateBody(string name, string title, string folder, DateOnly day);

    /// <summary>Enregistre le corps d'une fiche comme modèle du coffre ; rend le nom retenu.</summary>
    string SaveAsTemplate(string id, string name);
}

/// <summary>Données des extensions dans le coffre : le coffre ne connaît pas leur contenu.</summary>
public interface IExtensionDataStore
{
    /// <summary>Les données d'une extension, en JSON ; rien si elle n'en a pas.</summary>
    string? ExtensionData(string id);

    /// <summary>Remplace les données d'une extension : du JSON valide, 1 Mo au plus.</summary>
    void SaveExtensionData(string id, string json);

    /// <summary>Le journal de ce qu'une extension a écrit dans le coffre, une ligne JSON par écriture ; rien s'il est vide.</summary>
    string? ExtensionJournal(string id);

    /// <summary>Ajoute une ligne au journal d'une extension ; les plus anciennes partent au-delà de mille.</summary>
    void AppendExtensionJournal(string id, string line);
}

/// <summary>Schémas réseau du coffre (module Schémas réseau) : le coffre ne connaît pas leur modèle.</summary>
public interface ISchemaStore
{
    /// <summary>Les schémas lisibles, par titre, avec les fiches qui les insèrent.</summary>
    IReadOnlyList<SchemaSummary> Schemas();

    /// <summary>Les schémas archivés et ceux de la corbeille, par titre.</summary>
    IReadOnlyList<SchemaSummary> SchemasAside();

    /// <summary>Range un schéma : l'archive, le met à la corbeille, ou le remet dans la liste. Rien n'est effacé.</summary>
    void MoveSchema(string id, SchemaPlace place);

    /// <summary>Lit un schéma.</summary>
    SchemaDocument ReadSchema(string id);

    /// <summary>Crée un schéma (nouveau, importé) : vérifié, il reçoit un identifiant neuf.</summary>
    SchemaDocument CreateSchema(string text);

    /// <summary>Enregistre un schéma, s'il est toujours celui qui a été lu ; sinon, rend ce qu'il est devenu.</summary>
    SaveResult SaveSchema(string id, string text, string expectedFingerprint);

    /// <summary>Copie indépendante d'un schéma, titrée « … (copie) ».</summary>
    SchemaDocument DuplicateSchema(string id);

    /// <summary>Écrit un export (JSON, SVG, PNG ou CSV) hors du coffre, sans rien écraser ; rend le fichier créé.</summary>
    string ExportSchema(string folder, string name, string extension, ReadOnlySpan<byte> content);

    /// <summary>Lit un fichier choisi pour l'import : rien n'est écrit dans le coffre.</summary>
    string ReadSchemaToImport(string path);
}

/// <summary>Citations du coffre (module Citations).</summary>
public interface IQuoteStore
{
    /// <summary>Toutes les citations, celles de la corbeille à part, et les fichiers illisibles.</summary>
    QuoteList Quotes();

    /// <summary>Ajoute une citation ; seul le texte est obligatoire.</summary>
    Quote AddQuote(QuoteContent content, Moment now);

    /// <summary>Modifie la citation <paramref name="path"/> ; les champs d'autres outils sont conservés.</summary>
    Quote ModifyQuote(string path, QuoteContent content, Moment now);

    /// <summary>Range la citation dans la corbeille ; rend son nouveau chemin.</summary>
    string TrashQuote(string path);

    /// <summary>Remet une citation de la corbeille ; rend son nouveau chemin.</summary>
    string RestoreQuote(string path);

    /// <summary>Efface pour de bon une citation déjà à la corbeille.</summary>
    void PurgeQuote(string path);

    /// <summary>
    /// Retient la citation du jour parmi celles trouvées sur la page <paramref name="page"/> : une
    /// citation pas encore dans le coffre, tirée au hasard du jour. Rien si la page n'apporte
    /// rien de nouveau.
    /// </summary>
    Quote? KeepOnlineQuote(string page, IReadOnlyList<QuoteContent> found, Moment now);

    /// <summary>La citation trouvée en ligne ce jour-là, si elle est toujours là (hors corbeille).</summary>
    Quote? OnlineQuoteOf(DateOnly day);

    /// <summary>Jour où une citation a été trouvée en ligne pour la dernière fois ; rien si aucune.</summary>
    DateOnly? OnlineQuoteDay();
}
