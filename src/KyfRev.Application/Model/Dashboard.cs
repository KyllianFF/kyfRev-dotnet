using System.Collections.Immutable;
using KyfRev.Domain.Reviews;

namespace KyfRev.Application.Model;

/// <summary>Une fiche ou un passage dont une étape est due, ou le sera bientôt.</summary>
/// <param name="Kind">Fiche ou passage.</param>
/// <param name="Id">Identifiant de la fiche ou de l'annotation.</param>
/// <param name="SheetId">Fiche (celle d'origine, pour un passage).</param>
/// <param name="Title">Titre de la fiche (« Fiche introuvable » si elle a disparu).</param>
/// <param name="Excerpt">Passage surligné, abrégé (passages seulement).</param>
/// <param name="Term">Terme d'une définition.</param>
/// <param name="Section">Titres qui mènent au passage (vide pour une fiche).</param>
/// <param name="Comment">Commentaire d'un passage, abrégé : un indice, pas la réponse.</param>
/// <param name="Folder">Dossier relatif à <c>fiches/</c> (vide à la racine).</param>
/// <param name="Tags">Étiquettes de la fiche.</param>
/// <param name="Step">Étape attendue (« J7 »).</param>
/// <param name="DueDate">Échéance.</param>
/// <param name="DaysLate">Jours de retard (0 si l'échéance n'est pas dépassée).</param>
/// <param name="LastReview">Dernière révision validée.</param>
/// <param name="Cycle">Numéro du cycle.</param>
/// <param name="FilePresent">Faux si le fichier de la fiche est introuvable ou en double.</param>
public sealed record DueItem(
    ItemKind Kind,
    string Id,
    string SheetId,
    string Title,
    string? Excerpt,
    string? Term,
    ImmutableArray<string> Section,
    string? Comment,
    string Folder,
    ImmutableArray<string> Tags,
    string Step,
    DateOnly DueDate,
    int DaysLate,
    DateOnly LastReview,
    int Cycle,
    bool FilePresent);

/// <summary>Une étape qui tombera tel jour si chaque révision d'ici là est faite à son échéance.</summary>
/// <param name="Kind">Fiche ou passage.</param>
/// <param name="Id">Identifiant de la fiche ou de l'annotation.</param>
/// <param name="SheetId">Fiche.</param>
/// <param name="Title">Titre de la fiche.</param>
/// <param name="Excerpt">Passage abrégé.</param>
/// <param name="Term">Terme d'une définition.</param>
/// <param name="Section">Titres qui mènent au passage.</param>
/// <param name="Step">Étape projetée.</param>
public sealed record ProjectedItem(
    ItemKind Kind,
    string Id,
    string SheetId,
    string Title,
    string? Excerpt,
    string? Term,
    ImmutableArray<string> Section,
    string Step);

/// <summary>Un jour de la charge à venir.</summary>
/// <param name="Day">Jour.</param>
/// <param name="Due">Échéances connues ce jour-là ; aujourd'hui, les retards en font partie.</param>
/// <param name="Projected">Étapes attendues ce jour-là si chaque révision est faite à son échéance.</param>
public sealed record WorkloadDay(DateOnly Day, ImmutableArray<DueItem> Due, ImmutableArray<ProjectedItem> Projected);

/// <summary>Ce qui a été révisé un jour donné.</summary>
/// <param name="Day">Jour.</param>
/// <param name="Reviews">Révisions validées ce jour-là ; ouvrir un cycle (J0) n'en est pas une.</param>
public sealed record ReviewDay(DateOnly Day, int Reviews);

/// <summary>Un rappel de consolidation tiré, en attente de réponse.</summary>
/// <param name="SheetId">Fiche.</param>
/// <param name="Title">Titre.</param>
/// <param name="Folder">Dossier relatif à <c>fiches/</c>.</param>
/// <param name="Tags">Étiquettes.</param>
/// <param name="ArchivedOn">Jour de l'archivage.</param>
/// <param name="DrawnOn">Jour du tirage.</param>
/// <param name="PreviousReminders">Rappels déjà traités pour cette fiche.</param>
public sealed record PendingReminder(string SheetId, string Title, string Folder, ImmutableArray<string> Tags, DateOnly ArchivedOn, DateOnly DrawnOn, int PreviousReminders);

/// <summary>
/// Tableau de bord d'un jour, comme dans la 0.x : la liste à réviser (retards, puis aujourd'hui)
/// d'abord, les rappels, ce qui vient dans la semaine, la charge des 30 jours. La citation du jour
/// n'y figure pas : le module Citations l'apporte (<see cref="Modules.IDashboardWidget"/>).
/// </summary>
/// <param name="Day">Jour.</param>
/// <param name="Overdue">En retard, du plus ancien au plus récent.</param>
/// <param name="Today">Dû aujourd'hui, fiches d'abord, puis passages.</param>
/// <param name="Reminders">Rappels en attente, du plus ancien tirage au plus récent.</param>
/// <param name="Upcoming">Les sept prochains jours, par échéance.</param>
/// <param name="Workload">Aujourd'hui puis les 30 jours suivants.</param>
/// <param name="RecentReviews">Les sept derniers jours, aujourd'hui compris, du plus ancien au plus récent.</param>
/// <param name="RemindersPerDay">Rappels tirés au plus chaque jour.</param>
/// <param name="ActiveRecall">Rappel actif des passages : masqués jusqu'au clic.</param>
/// <param name="Exams">Examens à venir, du plus proche au plus lointain.</param>
/// <param name="NextDueDate">Prochaine échéance après aujourd'hui.</param>
/// <param name="ActiveSheets">Fiches en révision.</param>
/// <param name="ArchivedSheets">Fiches archivées.</param>
/// <param name="HealthProblems">Problèmes de « Santé du coffre » (liens cassés compris).</param>
/// <param name="UnservedPriorities">Fiches qui attendent un rappel depuis trop longtemps, faute de place.</param>
/// <param name="QuotesActive">Module Citations actif.</param>
/// <param name="SchemasActive">Module Schémas réseau actif.</param>
public sealed record Dashboard(
    DateOnly Day,
    ImmutableArray<DueItem> Overdue,
    ImmutableArray<DueItem> Today,
    ImmutableArray<PendingReminder> Reminders,
    ImmutableArray<DueItem> Upcoming,
    ImmutableArray<WorkloadDay> Workload,
    ImmutableArray<ReviewDay> RecentReviews,
    int RemindersPerDay,
    bool ActiveRecall,
    IReadOnlyList<UpcomingExam> Exams,
    DateOnly? NextDueDate,
    int ActiveSheets,
    int ArchivedSheets,
    int HealthProblems,
    int UnservedPriorities,
    bool QuotesActive,
    bool SchemasActive)
{
    /// <summary>Jours affichés dans « À venir ».</summary>
    public const int UpcomingDays = 7;

    /// <summary>Jours de la charge à venir, après aujourd'hui.</summary>
    public const int WorkloadDays = 30;

    /// <summary>Jours de l'activité récente, aujourd'hui compris.</summary>
    public const int RecentDays = 7;
}

/// <summary>Où en est une fiche archivée vis-à-vis des rappels de consolidation.</summary>
public enum ReminderStatus
{
    /// <summary>Au repos jusqu'à sa date d'éligibilité.</summary>
    Resting,

    /// <summary>Peut être tirée au sort pour un rappel.</summary>
    Eligible,

    /// <summary>Tirée, en attente de réponse.</summary>
    Pending,

    /// <summary>Exclue des rappels.</summary>
    Excluded,
}

/// <summary>Une ligne de la vue Archives.</summary>
/// <param name="Id">Fiche.</param>
/// <param name="Title">Titre.</param>
/// <param name="Folder">Dossier relatif à <c>fiches/</c>.</param>
/// <param name="Tags">Étiquettes.</param>
/// <param name="ArchivedOn">Jour de l'archivage.</param>
/// <param name="ManualArchiving">Archivée à la main, avant la fin de son cycle.</param>
/// <param name="ReminderStatus">Où elle en est des rappels.</param>
/// <param name="EligibleOn">Jour à partir duquel un rappel peut être tiré.</param>
/// <param name="ReminderDrawnOn">Jour du tirage en attente.</param>
/// <param name="CompletedCycles">Cycles terminés.</param>
/// <param name="RemindersHandled">Rappels traités.</param>
/// <param name="LastReminderOn">Jour du dernier rappel traité.</param>
/// <param name="LastReminderOutcome">Réponse au dernier rappel.</param>
/// <param name="FilePresent">Faux si le fichier est introuvable ou en double.</param>
public sealed record ArchivedSheetEntry(
    string Id,
    string Title,
    string Folder,
    ImmutableArray<string> Tags,
    DateOnly ArchivedOn,
    bool ManualArchiving,
    ReminderStatus ReminderStatus,
    DateOnly EligibleOn,
    DateOnly? ReminderDrawnOn,
    int CompletedCycles,
    int RemindersHandled,
    DateOnly? LastReminderOn,
    ReminderOutcome? LastReminderOutcome,
    bool FilePresent);

/// <summary>Une demande d'étalement des échéances.</summary>
/// <param name="PerDay">Révisions par jour, au plus.</param>
/// <param name="PauseFrom">Premier jour d'une pause ; rien : c'est le retard qui s'étale.</param>
/// <param name="PauseTo">Dernier jour de la pause ; rien : un seul jour.</param>
public sealed record SpreadRequest(int PerDay, DateOnly? PauseFrom = null, DateOnly? PauseTo = null)
{
    /// <summary>Le plus grand plafond accepté.</summary>
    public const int MaxPerDay = 500;

    /// <summary>La plus longue pause acceptée, en jours.</summary>
    public const int MaxPauseDays = 90;
}

/// <summary>Ce que fait un étalement.</summary>
/// <param name="Kept">Échéances gardées aujourd'hui (rattrapage seulement).</param>
/// <param name="Moved">Échéances déplacées.</param>
/// <param name="LastDay">Le dernier jour où il en pose ; rien si rien ne bouge.</param>
public sealed record SpreadPlan(int Kept, int Moved, DateOnly? LastDay);
