using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes de la charge à venir, ceux de la 0.x, lus dans les ressources (<c>Resources/WorkloadText.resx</c>).</summary>
public static class WorkloadText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.WorkloadText", typeof(WorkloadText).Assembly);

    /// <summary>« Charge — 30 prochains jours ».</summary>
    public static string Title => Text(nameof(Title));

    /// <summary>« Charge ».</summary>
    public static string ShortTitle => Text(nameof(ShortTitle));

    /// <summary>« Aucune révision prévue d'ici 30 jours. ».</summary>
    public static string Empty => Text(nameof(Empty));

    /// <summary>« Chargement de la charge à venir ».</summary>
    public static string Loading => Text(nameof(Loading));

    /// <summary>« En moyenne … par jour ».</summary>
    public static string AverageOf(object average) => Format("Average", average);

    /// <summary>« … · le plus chargé : … (…) ».</summary>
    public static string PeakOf(object average, object day, object total) => Format("Peak", average, day, total);

    /// <summary>« Légende ».</summary>
    public static string Legend => Text(nameof(Legend));

    /// <summary>« Échéances ».</summary>
    public static string LegendDue => Text(nameof(LegendDue));

    /// <summary>« Projetées, si vous révisez à temps ».</summary>
    public static string LegendProjected => Text(nameof(LegendProjected));

    /// <summary>« Moyenne ».</summary>
    public static string LegendAverage => Text(nameof(LegendAverage));

    /// <summary>« Révisions par jour : choisissez un jour pour le détail ».</summary>
    public static string Days => Text(nameof(Days));

    /// <summary>« auj. ».</summary>
    public static string TodayShort => Text(nameof(TodayShort));

    /// <summary>« aucune révision ».</summary>
    public static string NoReview => Text(nameof(NoReview));

    /// <summary>« … révision ».</summary>
    public static string ReviewsOneOf(object count) => Format("ReviewsOne", count);

    /// <summary>« … révisions ».</summary>
    public static string ReviewsManyOf(object count) => Format("ReviewsMany", count);

    /// <summary>« …, dont … projetée ».</summary>
    public static string WithProjectedOneOf(object reviews, object projected) => Format("WithProjectedOne", reviews, projected);

    /// <summary>« …, dont … projetées ».</summary>
    public static string WithProjectedManyOf(object reviews, object projected) => Format("WithProjectedMany", reviews, projected);

    /// <summary>« … : … » : le jour, puis ses révisions.</summary>
    public static string DayNameOf(object day, object reviews) => Format("DayName", day, reviews);

    /// <summary>« … · … » : le jour choisi, et ses révisions.</summary>
    public static string DayTitleOf(object day, object reviews) => Format("DayTitle", day, reviews);

    /// <summary>« S'y ajoutent les rappels de consolidation : … fiche archivée par jour au plus, tirées au hasard. ».</summary>
    public static string RemindersOneOf(object count) => Format("RemindersOne", count);

    /// <summary>« S'y ajoutent les rappels de consolidation : … fiches archivées par jour au plus, tirées au hasard. ».</summary>
    public static string RemindersManyOf(object count) => Format("RemindersMany", count);

    /// <summary>« Échéances ».</summary>
    public static string Due => Text(nameof(Due));

    /// <summary>« Projetées ».</summary>
    public static string Projected => Text(nameof(Projected));

    /// <summary>« si les révisions d'ici là sont faites à temps ; elles suivent l'échéance qu'on déplace ».</summary>
    public static string ProjectedHelp => Text(nameof(ProjectedHelp));

    /// <summary>« Déplacer… ».</summary>
    public static string MoveAction => Text(nameof(MoveAction));

    /// <summary>« Déplacer … : … ».</summary>
    public static string MoveNameOf(object step, object name) => Format("MoveName", step, name);

    /// <summary>« Déplacer … (…) au ».</summary>
    public static string MoveLabelOf(object step, object name) => Format("MoveLabel", step, name);

    /// <summary>« Déplacer ».</summary>
    public static string MoveConfirm => Text(nameof(MoveConfirm));

    /// <summary>« Annuler ».</summary>
    public static string MoveCancel => Text(nameof(MoveCancel));

    /// <summary>« Au-delà des 30 jours affichés. ».</summary>
    public static string BeyondChart => Text(nameof(BeyondChart));

    /// <summary>« Ce jour-là : aucune révision. ».</summary>
    public static string HintEmpty => Text(nameof(HintEmpty));

    /// <summary>« Ce jour-là : …. ».</summary>
    public static string HintOf(object reviews) => Format("Hint", reviews);

    /// <summary>« … (…) déplacée au …. ».</summary>
    public static string MovedOf(object step, object name, object day) => Format("Moved", step, name, day);

    /// <summary>« passage de « … » ».</summary>
    public static string HiddenPassageOf(object title) => Format("HiddenPassage", title);

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
