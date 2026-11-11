using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes de la vue « Statistiques », ceux de la 0.x, lus dans les ressources (<c>Resources/StatisticsText.resx</c>).</summary>
public static class StatisticsText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.StatisticsText", typeof(StatisticsText).Assembly);

    /// <summary>« {0} jours actifs ».</summary>
    public static string ActiveDayManyOf(object count) => Format("ActiveDayMany", count);

    /// <summary>« {0} jour actif ».</summary>
    public static string ActiveDayOneOf(object count) => Format("ActiveDayOne", count);

    /// <summary>« Jours actifs ».</summary>
    public static string ActiveDaysColumn => Text(nameof(ActiveDaysColumn));

    /// <summary>« Jours actifs ».</summary>
    public static string ActiveMany => Text(nameof(ActiveMany));

    /// <summary>« Jour actif ».</summary>
    public static string ActiveOne => Text(nameof(ActiveOne));

    /// <summary>« {0} / {1} ».</summary>
    public static string ActiveValueOf(object active, object days) => Format("ActiveValue", active, days);

    /// <summary>«  et  ».</summary>
    public static string And => Text(nameof(And));

    /// <summary>« Retard moyen ».</summary>
    public static string AverageDelayColumn => Text(nameof(AverageDelayColumn));

    /// <summary>« meilleur jour : {0}, {1} ».</summary>
    public static string BestDayOf(object total, object day) => Format("BestDay", total, day);

    /// <summary>« dont {0} oubliée ».</summary>
    public static string ForgottenOneOf(object count) => Format("ForgottenOne", count);

    /// <summary>« dont {0} oubliées ».</summary>
    public static string ForgottenManyOf(object count) => Format("ForgottenMany", count);

    /// <summary>« Par dossier ».</summary>
    public static string ByFolder => Text(nameof(ByFolder));

    /// <summary>« Par mois ».</summary>
    public static string ByMonth => Text(nameof(ByMonth));

    /// <summary>« Calendrier d'activité : {0} sur 52 semaines ».</summary>
    public static string CalendarNameOf(object days) => Format("CalendarName", days);

    /// <summary>« 52 semaines ».</summary>
    public static string CalendarPeriod => Text(nameof(CalendarPeriod));

    /// <summary>« Calendrier d'activité ».</summary>
    public static string CalendarTitle => Text(nameof(CalendarTitle));

    /// <summary>« {0} : {1} ».</summary>
    public static string DayDescriptionOf(object day, object detail) => Format("DayDescription", day, detail);

    /// <summary>« {0} jours ».</summary>
    public static string DayManyOf(object count) => Format("DayMany", count);

    /// <summary>« {0} jour ».</summary>
    public static string DayOneOf(object count) => Format("DayOne", count);

    /// <summary>« retard moyen {0} j, au plus {1} ».</summary>
    public static string DelayDetailOf(object average, object longest) => Format("DelayDetail", average, longest);

    /// <summary>« {0} j ».</summary>
    public static string DelayValueOf(object delay) => Format("DelayValue", delay);

    /// <summary>« Dossier ».</summary>
    public static string FolderColumn => Text(nameof(FolderColumn));

    /// <summary>« {0}, {1}, {2}, à l'heure {3}, retard moyen {4} ».</summary>
    public static string FolderSpokenOf(object folder, object sheets, object reviews, object onTime, object delay) => Format("FolderSpoken", folder, sheets, reviews, onTime, delay);

    /// <summary>« Ven ».</summary>
    public static string Friday => Text(nameof(Friday));

    /// <summary>« {0} rappels traités ».</summary>
    public static string HandledManyOf(object count) => Format("HandledMany", count);

    /// <summary>« {0} rappel traité ».</summary>
    public static string HandledOneOf(object count) => Format("HandledOne", count);

    /// <summary>« Chiffres clés ».</summary>
    public static string KeyFigures => Text(nameof(KeyFigures));

    /// <summary>« Moins ».</summary>
    public static string Less => Text(nameof(Less));

    /// <summary>« Calcul des statistiques… ».</summary>
    public static string Loading => Text(nameof(Loading));

    /// <summary>« Lun ».</summary>
    public static string Monday => Text(nameof(Monday));

    /// <summary>« Mois ».</summary>
    public static string MonthColumn => Text(nameof(MonthColumn));

    /// <summary>« {0}, {1}, {2} ».</summary>
    public static string MonthSpokenOf(object month, object reviews, object days) => Format("MonthSpoken", month, reviews, days);

    /// <summary>« Plus ».</summary>
    public static string More => Text(nameof(More));

    /// <summary>« Sans dossier ».</summary>
    public static string NoFolder => Text(nameof(NoFolder));

    /// <summary>« Pas encore de révision. ».</summary>
    public static string NoMonth => Text(nameof(NoMonth));

    /// <summary>« Pas encore de fiche. ».</summary>
    public static string NoSheet => Text(nameof(NoSheet));

    /// <summary>« — ».</summary>
    public static string None => Text(nameof(None));

    /// <summary>« Une révision compte le jour où elle est validée ; la création d'une… ».</summary>
    public static string Note => Text(nameof(Note));

    /// <summary>« rien ».</summary>
    public static string Nothing => Text(nameof(Nothing));

    /// <summary>« À l'heure ».</summary>
    public static string OnTimeColumn => Text(nameof(OnTimeColumn));

    /// <summary>« {0} le jour prévu · {1} en avance · {2} en retard ».</summary>
    public static string OnTimeDetailOf(object onTime, object early, object late) => Format("OnTimeDetail", onTime, early, late);

    /// <summary>« À l'heure ».</summary>
    public static string OnTimeLabel => Text(nameof(OnTimeLabel));

    /// <summary>« sur 52 semaines ».</summary>
    public static string OverWeeks => Text(nameof(OverWeeks));

    /// <summary>« sur 52 semaines, et {0} ».</summary>
    public static string OverWeeksWithRemindersOf(object reminders) => Format("OverWeeksWithReminders", reminders);

    /// <summary>« {0} passages ».</summary>
    public static string PassageManyOf(object count) => Format("PassageMany", count);

    /// <summary>« {0} passage ».</summary>
    public static string PassageOneOf(object count) => Format("PassageOne", count);

    /// <summary>« {0} % ».</summary>
    public static string PercentOf(object value) => Format("Percent", value);

    /// <summary>« 7 derniers jours : {0} · 30 derniers : {1} ».</summary>
    public static string RecentDetailOf(object week, object month) => Format("RecentDetail", week, month);

    /// <summary>« record : {0}, jusqu'au {1} ».</summary>
    public static string RecordOf(object streak, object day) => Format("Record", streak, day);

    /// <summary>« {0} rappels de consolidation ».</summary>
    public static string ReminderManyOf(object count) => Format("ReminderMany", count);

    /// <summary>« {0} rappel de consolidation ».</summary>
    public static string ReminderOneOf(object count) => Format("ReminderOne", count);

    /// <summary>« {0} révisions ».</summary>
    public static string ReviewManyOf(object count) => Format("ReviewMany", count);

    /// <summary>« {0} révision ».</summary>
    public static string ReviewOneOf(object count) => Format("ReviewOne", count);

    /// <summary>« Révisions ».</summary>
    public static string ReviewsColumn => Text(nameof(ReviewsColumn));

    /// <summary>« {0} ({1}) ».</summary>
    public static string ReviewsDetailOf(object reviews, object parts) => Format("ReviewsDetail", reviews, parts);

    /// <summary>« Révisions validées ».</summary>
    public static string ReviewsMany => Text(nameof(ReviewsMany));

    /// <summary>« Révision validée ».</summary>
    public static string ReviewsOne => Text(nameof(ReviewsOne));

    /// <summary>« {0} fiches ».</summary>
    public static string SheetManyOf(object count) => Format("SheetMany", count);

    /// <summary>« {0} fiche ».</summary>
    public static string SheetOneOf(object count) => Format("SheetOne", count);

    /// <summary>« Fiches ».</summary>
    public static string SheetsColumn => Text(nameof(SheetsColumn));

    /// <summary>« Cette semaine ».</summary>
    public static string WeekLabel => Text(nameof(WeekLabel));

    /// <summary>« moyenne des 4 semaines d'avant : {0} par semaine ».</summary>
    public static string WeekAverageOf(object average) => Format("WeekAverage", average);

    /// <summary>« Votre régularité : révisions validées (fiches, passages, définition… ».</summary>
    public static string Summary => Text(nameof(Summary));

    /// <summary>« Votre régularité : révisions validées (fiches, passages, définition… ».</summary>
    public static string SummarySinceOf(object day) => Format("SummarySince", day);

    /// <summary>« Statistiques ».</summary>
    public static string Title => Text(nameof(Title));

    /// <summary>« Mer ».</summary>
    public static string Wednesday => Text(nameof(Wednesday));

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » introuvable.");
}
