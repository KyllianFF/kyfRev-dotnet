using System.Collections.Immutable;

namespace KyfRev.Application.Model;

/// <summary>Ce qui a été fait un jour donné.</summary>
/// <param name="Day">Le jour.</param>
/// <param name="Sheets">Révisions de fiches validées.</param>
/// <param name="Passages">Révisions de passages et de définitions validées.</param>
/// <param name="Reminders">Rappels de consolidation traités.</param>
public sealed record ActivityDay(DateOnly Day, int Sheets, int Passages, int Reminders)
{
    /// <summary>Révisions validées ce jour (fiches, passages et définitions).</summary>
    public int Reviews => Sheets + Passages;

    /// <summary>Révisions validées et rappels traités ce jour.</summary>
    public int Total => Reviews + Reminders;
}

/// <summary>L'activité d'un mois.</summary>
/// <param name="Month">Le mois : « 2026-10 ».</param>
/// <param name="Reviews">Révisions validées.</param>
/// <param name="ActiveDays">Jours où quelque chose a été fait.</param>
public sealed record MonthStatistics(string Month, int Reviews, int ActiveDays);

/// <summary>La régularité d'un dossier.</summary>
/// <param name="Folder">Dossier relatif à <c>fiches/</c> (vide à la racine).</param>
/// <param name="Sheets">Fiches du dossier, actives ou archivées.</param>
/// <param name="Reviews">Révisions validées.</param>
/// <param name="OnTime">Révisions faites le jour prévu ou avant.</param>
/// <param name="Late">Révisions faites en retard.</param>
/// <param name="AverageDelay">Retard moyen des révisions en retard, en jours (0 sans retard).</param>
public sealed record FolderStatistics(string Folder, int Sheets, int Reviews, int OnTime, int Late, double AverageDelay);

/// <summary>
/// Statistiques de régularité. Une révision validée compte le jour où elle l'a été, un rappel de consolidation le jour
/// où il a été traité ; les J0 ne sont pas des révisions. Les séries se comptent depuis toujours, le reste sur le calendrier.
/// </summary>
/// <param name="Day">Le jour du calcul.</param>
/// <param name="From">Premier jour du calendrier : le lundi d'il y a 52 semaines.</param>
/// <param name="Activity">Jours du calendrier où quelque chose a été fait, du plus ancien au plus récent.</param>
/// <param name="FirstActivity">Première révision validée ou premier rappel traité, depuis toujours.</param>
/// <param name="Reviews">Révisions validées sur le calendrier.</param>
/// <param name="Reminders">Rappels de consolidation traités sur le calendrier.</param>
/// <param name="ActiveDays">Jours actifs sur le calendrier.</param>
/// <param name="LastSevenDays">Révisions validées ces 7 derniers jours, aujourd'hui compris.</param>
/// <param name="LastThirtyDays">Révisions validées ces 30 derniers jours, aujourd'hui compris.</param>
/// <param name="CurrentStreak">Jours consécutifs avec une activité, jusqu'à aujourd'hui ou jusqu'à hier.</param>
/// <param name="LongestStreak">Plus longue série de jours consécutifs, depuis toujours.</param>
/// <param name="LongestStreakEnd">Dernier jour de la plus longue série.</param>
/// <param name="OnTime">Révisions faites le jour prévu.</param>
/// <param name="Early">Révisions faites en avance.</param>
/// <param name="Late">Révisions faites en retard.</param>
/// <param name="AverageDelay">Retard moyen des révisions en retard, en jours (0 sans retard).</param>
/// <param name="LongestDelay">Plus long retard, en jours.</param>
/// <param name="BestDay">Le jour le plus chargé du calendrier.</param>
/// <param name="ByMonth">Les douze derniers mois, du plus ancien au plus récent.</param>
/// <param name="ByFolder">Les dossiers, du plus révisé au moins révisé.</param>
public sealed record Statistics(
    DateOnly Day,
    DateOnly From,
    ImmutableArray<ActivityDay> Activity,
    DateOnly? FirstActivity,
    int Reviews,
    int Reminders,
    int ActiveDays,
    int LastSevenDays,
    int LastThirtyDays,
    int CurrentStreak,
    int LongestStreak,
    DateOnly? LongestStreakEnd,
    int OnTime,
    int Early,
    int Late,
    double AverageDelay,
    int LongestDelay,
    ActivityDay? BestDay,
    ImmutableArray<MonthStatistics> ByMonth,
    ImmutableArray<FolderStatistics> ByFolder)
{
    /// <summary>Parmi les révisions du calendrier, celles où l'utilisateur a dit « J'avais oublié ».</summary>
    public int Forgotten { get; init; }

    /// <summary>Semaines entières du calendrier, avant la semaine en cours.</summary>
    public const int WeeksShown = 52;

    /// <summary>Mois du détail par mois, le mois en cours compris.</summary>
    public const int MonthsShown = 12;
}
