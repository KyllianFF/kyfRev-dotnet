using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes de la vue « Archives », ceux de la 0.x, lus dans les ressources (<c>Resources/ArchivesText.resx</c>).</summary>
public static class ArchivesText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.ArchivesText", typeof(ArchivesText).Assembly);

    /// <summary>« … : … ».</summary>
    public static string ActionOnOf(object action, object title) => Format("ActionOn", action, title);

    /// <summary>« Tous ».</summary>
    public static string AllCycles => Text(nameof(AllCycles));

    /// <summary>« Tous ».</summary>
    public static string AllFolders => Text(nameof(AllFolders));

    /// <summary>« Tous ».</summary>
    public static string AllStatuses => Text(nameof(AllStatuses));

    /// <summary>« archivée le … (à la main) ».</summary>
    public static string ArchivedByHandOf(object day) => Format("ArchivedByHand", day);

    /// <summary>« archivée le … ».</summary>
    public static string ArchivedOnOf(object day) => Format("ArchivedOn", day);

    /// <summary>« … cycles terminés ».</summary>
    public static string CompletedCyclesManyOf(object count) => Format("CompletedCyclesMany", count);

    /// <summary>« … cycle terminé ».</summary>
    public static string CompletedCyclesOneOf(object count) => Format("CompletedCyclesOne", count);

    /// <summary>« … fiches archivées ».</summary>
    public static string CountManyOf(object count) => Format("CountMany", count);

    /// <summary>« … fiche archivée ».</summary>
    public static string CountOneOf(object count) => Format("CountOne", count);

    /// <summary>« Éligible à un rappel ».</summary>
    public static string Eligible => Text(nameof(Eligible));

    /// <summary>« Une fiche est archivée à la fin de son dernier cycle, ou à la main depuis sa page de révision. ».</summary>
    public static string EmptyDescription => Text(nameof(EmptyDescription));

    /// <summary>« Aucune fiche archivée pour l'instant ».</summary>
    public static string EmptyTitle => Text(nameof(EmptyTitle));

    /// <summary>« Exclure des rappels ».</summary>
    public static string Exclude => Text(nameof(Exclude));

    /// <summary>« Exclue des rappels ».</summary>
    public static string Excluded => Text(nameof(Excluded));

    /// <summary>« « … » est exclue des rappels de consolidation. ».</summary>
    public static string ExcludedMessageOf(object title) => Format("ExcludedMessage", title);

    /// <summary>« Cycles ».</summary>
    public static string FilterCycles => Text(nameof(FilterCycles));

    /// <summary>« Dossier ».</summary>
    public static string FilterFolder => Text(nameof(FilterFolder));

    /// <summary>« Archivée ».</summary>
    public static string FilterPeriod => Text(nameof(FilterPeriod));

    /// <summary>« Rappels ».</summary>
    public static string FilterReminders => Text(nameof(FilterReminders));

    /// <summary>« Tri ».</summary>
    public static string FilterSort => Text(nameof(FilterSort));

    /// <summary>« Réinclure ».</summary>
    public static string Include => Text(nameof(Include));

    /// <summary>« « … » est réincluse dans les rappels de consolidation. ».</summary>
    public static string IncludedMessageOf(object title) => Format("IncludedMessage", title);

    /// <summary>« dernier rappel le … : … ».</summary>
    public static string LastReminderOf(object day, object outcome) => Format("LastReminder", day, outcome);

    /// <summary>« Chargement des archives… ».</summary>
    public static string Loading => Text(nameof(Loading));

    /// <summary>« Aucune fiche archivée ne correspond à ces filtres. ».</summary>
    public static string NoMatch => Text(nameof(NoMatch));

    /// <summary>« à revoir entièrement ».</summary>
    public static string OutcomeEntirely => Text(nameof(OutcomeEntirely));

    /// <summary>« connue ».</summary>
    public static string OutcomeKnown => Text(nameof(OutcomeKnown));

    /// <summary>« certains points à revoir ».</summary>
    public static string OutcomeSomePoints => Text(nameof(OutcomeSomePoints));

    /// <summary>« Rappel en attente ».</summary>
    public static string Pending => Text(nameof(Pending));

    /// <summary>« N'importe quand ».</summary>
    public static string PeriodAll => Text(nameof(PeriodAll));

    /// <summary>« Ce dernier mois ».</summary>
    public static string PeriodLastMonth => Text(nameof(PeriodLastMonth));

    /// <summary>« Ces six derniers mois ».</summary>
    public static string PeriodLastSixMonths => Text(nameof(PeriodLastSixMonths));

    /// <summary>« Il y a plus de six mois ».</summary>
    public static string PeriodOlder => Text(nameof(PeriodOlder));

    /// <summary>« rien n'est jamais supprimé ; les fiches reviennent de temps en temps en rappel de consolidation. ».</summary>
    public static string Promise => Text(nameof(Promise));

    /// <summary>« Remettre en révision… ».</summary>
    public static string Restart => Text(nameof(Restart));

    /// <summary>« Remettre en révision ».</summary>
    public static string RestartConfirm => Text(nameof(RestartConfirm));

    /// <summary>« Ouvrir ».</summary>
    public static string Open => Text(nameof(Open));

    /// <summary>« « … » redevient active : un nouveau cycle commence aujourd'hui (J0) et J1 sera dû demain. Son historique est conservé. ».</summary>
    public static string RestartMessageOf(object title) => Format("RestartMessage", title);

    /// <summary>« Remettre en révision ? ».</summary>
    public static string RestartTitle => Text(nameof(RestartTitle));

    /// <summary>« « … » est de nouveau en révision : J1 demain. ».</summary>
    public static string RestartedMessageOf(object title) => Format("RestartedMessage", title);

    /// <summary>« Au repos jusqu'au … ».</summary>
    public static string RestingUntilOf(object day) => Format("RestingUntil", day);

    /// <summary>« Archivage récent ».</summary>
    public static string SortArchiving => Text(nameof(SortArchiving));

    /// <summary>« Nombre de cycles ».</summary>
    public static string SortCycles => Text(nameof(SortCycles));

    /// <summary>« Titre ».</summary>
    public static string SortTitle => Text(nameof(SortTitle));

    /// <summary>« Éligibles ».</summary>
    public static string StatusEligible => Text(nameof(StatusEligible));

    /// <summary>« Exclues des rappels ».</summary>
    public static string StatusExcluded => Text(nameof(StatusExcluded));

    /// <summary>« Au repos ».</summary>
    public static string StatusResting => Text(nameof(StatusResting));

    /// <summary>« 3 ou plus ».</summary>
    public static string ThreeOrMoreCycles => Text(nameof(ThreeOrMoreCycles));

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
