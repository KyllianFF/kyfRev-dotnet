using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes du tableau de bord, ceux de la 0.x, lus dans les ressources (<c>Resources/DashboardText.resx</c>).</summary>
public static class DashboardText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.DashboardText", typeof(DashboardText).Assembly);

    /// <summary>« Tout ».</summary>
    public static string All => Text(nameof(All));

    /// <summary>« Toutes vos fiches sont archivées ; les rappels de consolidation apparaîtront ici. ».</summary>
    public static string AllArchived => Text(nameof(AllArchived));

    /// <summary>« Archivée le … ».</summary>
    public static string ArchivedOnOf(object day) => Format("ArchivedOn", day);

    /// <summary>« Commentaire : … ».</summary>
    public static string CommentHintOf(object comment) => Format("CommentHint", comment);

    /// <summary>« cycle … ».</summary>
    public static string CycleOf(object cycle) => Format("Cycle", cycle);

    /// <summary>« Retard … j ».</summary>
    public static string DaysLateOf(object days) => Format("DaysLate", days);

    /// <summary>« Définition ».</summary>
    public static string Definition => Text(nameof(Definition));

    /// <summary>« Rien à réviser aujourd'hui ».</summary>
    public static string EmptyTitle => Text(nameof(EmptyTitle));

    /// <summary>« fichier introuvable ».</summary>
    public static string FileMissing => Text(nameof(FileMissing));

    /// <summary>« 1er rappel ».</summary>
    public static string FirstReminder => Text(nameof(FirstReminder));

    /// <summary>« Créez votre première fiche : sa révision J1 sera due demain. ».</summary>
    public static string FirstSheet => Text(nameof(FirstSheet));

    /// <summary>« … points à vérifier dans le coffre (fichier manquant, lien à revoir…). ».</summary>
    public static string HealthWarningManyOf(object count) => Format("HealthWarningMany", count);

    /// <summary>« … point à vérifier dans le coffre (fichier manquant, lien à revoir…). ».</summary>
    public static string HealthWarningOneOf(object count) => Format("HealthWarningOne", count);

    /// <summary>« Éléments à réviser ».</summary>
    public static string Items => Text(nameof(Items));

    /// <summary>« … éléments à réviser ».</summary>
    public static string ItemsToReviewManyOf(object count) => Format("ItemsToReviewMany", count);

    /// <summary>« … élément à réviser ».</summary>
    public static string ItemsToReviewOneOf(object count) => Format("ItemsToReviewOne", count);

    /// <summary>« dernière révision … ».</summary>
    public static string LastReviewOf(object day) => Format("LastReview", day);

    /// <summary>« Chargement du tableau de bord ».</summary>
    public static string Loading => Text(nameof(Loading));

    /// <summary>« prochaine échéance … ».</summary>
    public static string NextDueOf(object day) => Format("NextDue", day);

    /// <summary>« Prochaine révision : …. ».</summary>
    public static string NextReviewOf(object day) => Format("NextReview", day);

    /// <summary>« rien à réviser ».</summary>
    public static string NothingToReview => Text(nameof(NothingToReview));

    /// <summary>« …e rappel ».</summary>
    public static string NthReminderOf(object rank) => Format("NthReminder", rank);

    /// <summary>« En retard ».</summary>
    public static string Overdue => Text(nameof(Overdue));

    /// <summary>« Passage ».</summary>
    public static string Passage => Text(nameof(Passage));

    /// <summary>« Passage à retrouver ».</summary>
    public static string PassageToRecall => Text(nameof(PassageToRecall));

    /// <summary>« Passages ».</summary>
    public static string Passages => Text(nameof(Passages));

    /// <summary>« … fiches attendent un rappel depuis plus de 30 jours : augmentez le nombre de rappels quotidiens. ».</summary>
    public static string PriorityWarningManyOf(object count) => Format("PriorityWarningMany", count);

    /// <summary>« … fiche attend un rappel depuis plus de 30 jours : augmentez le nombre de rappels quotidiens. ».</summary>
    public static string PriorityWarningOneOf(object count) => Format("PriorityWarningOne", count);

    /// <summary>« Rappel ».</summary>
    public static string Reminder => Text(nameof(Reminder));

    /// <summary>« Rappels de consolidation ».</summary>
    public static string Reminders => Text(nameof(Reminders));

    /// <summary>« Fiche ».</summary>
    public static string Sheet => Text(nameof(Sheet));

    /// <summary>« Fiches ».</summary>
    public static string Sheets => Text(nameof(Sheets));

    /// <summary>« … fiches actives ».</summary>
    public static string SheetsActiveManyOf(object count) => Format("SheetsActiveMany", count);

    /// <summary>« … fiche active ».</summary>
    public static string SheetsActiveOneOf(object count) => Format("SheetsActiveOne", count);

    /// <summary>« … fiches archivées ».</summary>
    public static string SheetsArchivedManyOf(object count) => Format("SheetsArchivedMany", count);

    /// <summary>« … fiche archivée ».</summary>
    public static string SheetsArchivedOneOf(object count) => Format("SheetsArchivedOne", count);

    /// <summary>« Afficher ».</summary>
    public static string Show => Text(nameof(Show));

    /// <summary>« Trier par ».</summary>
    public static string SortBy => Text(nameof(SortBy));

    /// <summary>« nombre de cycles ».</summary>
    public static string SortCycle => Text(nameof(SortCycle));

    /// <summary>« échéance ».</summary>
    public static string SortDueDate => Text(nameof(SortDueDate));

    /// <summary>« dernière révision ».</summary>
    public static string SortLastReview => Text(nameof(SortLastReview));

    /// <summary>« étape J ».</summary>
    public static string SortStep => Text(nameof(SortStep));

    /// <summary>« titre ».</summary>
    public static string SortTitle => Text(nameof(SortTitle));

    /// <summary>« Étape du calendrier ».</summary>
    public static string StepTooltip => Text(nameof(StepTooltip));

    /// <summary>« Aujourd'hui ».</summary>
    public static string Today => Text(nameof(Today));

    /// <summary>« À venir — 7 jours ».</summary>
    public static string Upcoming => Text(nameof(Upcoming));

    private static string Format(string name, object value) => string.Format(CultureInfo.CurrentCulture, Text(name), value);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
