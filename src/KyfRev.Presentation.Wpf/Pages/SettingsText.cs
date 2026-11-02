using System.Globalization;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Les libellés de la vue « Paramètres ».</summary>
public static class SettingsText
{
    /// <summary>Le titre de la vue.</summary>
    public const string Title = "Paramètres";

    /// <summary>La lecture des réglages.</summary>
    public const string Loading = "Chargement des réglages";

    /// <summary>La première section.</summary>
    public const string Calendar = "Calendrier des révisions";

    /// <summary>Les étapes.</summary>
    public const string Steps = "Étapes après J0, en jours";

    /// <summary>Ce que veulent dire les étapes.</summary>
    public const string StepsHelp = "J1 = 1 jour après la révision précédente. Par défaut : 1, 3, 7, 14, 30.";

    /// <summary>Le rappel actif.</summary>
    public const string ActiveRecall = "Rappel actif (passages, texte à trous des fiches)";

    /// <summary>Ce que fait le rappel actif.</summary>
    public const string ActiveRecallHelp = "À la révision d'un passage, votre commentaire et le texte qui l'entoure viennent d'abord : le passage reste caché jusqu'à « Afficher le passage ». À la révision d'une fiche, ses passages surlignés et son texte en gras sont masqués jusqu'au clic. Se le rappeler avant de le relire aide à le retenir.";

    /// <summary>La seconde section.</summary>
    public const string Reminders = "Rappels de consolidation";

    /// <summary>Le nombre de rappels.</summary>
    public const string MaxPerDay = "Rappels par jour, au plus";

    /// <summary>L'attente avant la priorité.</summary>
    public const string MaxWaitDays = "Priorité après une attente de (jours)";

    /// <summary>Les repos.</summary>
    public const string RestMonths = "Repos après chaque « Fiche connue », en mois";

    /// <summary>Ce que veulent dire les repos.</summary>
    public const string RestMonthsHelp = "Par défaut : 2, 4, 8, 12 (puis 12).";

    /// <summary>Les fiches archivées à la main.</summary>
    public const string IncludeManualArchiving = "Inclure dans les rappels les fiches archivées à la main";

    /// <summary>Enregistrer.</summary>
    public const string Save = "Enregistrer les réglages";

    /// <summary>Ce que change l'enregistrement : à savoir avant d'enregistrer, pas après.</summary>
    public const string SaveHelp = "Les cycles en cours gardent leur calendrier ; les nouveaux suivent ces réglages.";

    /// <summary>L'enregistrement a réussi.</summary>
    public const string Saved = "Réglages enregistrés.";

    /// <summary>Une liste de nombres mal écrite.</summary>
    public const string WholeNumbers = "Saisissez des nombres entiers, séparés par des virgules.";

    /// <summary>Un nombre de rappels hors limites.</summary>
    public static string MaxPerDayRange { get; } = string.Create(CultureInfo.InvariantCulture, $"Le nombre de rappels par jour va de 1 à {SettingsPageViewModel.MaxPerDayLimit}.");

    /// <summary>Une attente hors limites.</summary>
    public static string MaxWaitDaysRange { get; } = string.Create(CultureInfo.InvariantCulture, $"L'attente avant la priorité va de 1 à {SettingsPageViewModel.MaxWaitDaysLimit} jours.");

    /// <summary>Ce que donne une séquence pour une fiche créée le jour dit.</summary>
    public static string Preview(IEnumerable<(int Step, DateOnly Day)> steps) =>
        "Une fiche créée aujourd'hui reviendra : "
        + string.Join(", ", steps.Select(step => string.Create(DefinitionsText.Culture, $"J{step.Step} le {step.Day:d MMM}")));
}
