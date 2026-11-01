using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes de la séance d'étude, lus dans les ressources (<c>Resources/SessionText.resx</c>).</summary>
public static class SessionText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.SessionText", typeof(SessionText).Assembly);

    /// <summary>« Revenir à Aujourd'hui ».</summary>
    public static string BackToToday => Text(nameof(BackToToday));

    /// <summary>« Faits : … ».</summary>
    public static string DoneOf(object count) => Format("Done", count);

    /// <summary>« Durée : … ».</summary>
    public static string DurationOf(object duration) => Format("Duration", duration);

    /// <summary>« Quitter le plein écran ».</summary>
    public static string ExitFullScreen => Text(nameof(ExitFullScreen));

    /// <summary>« Terminer la séance ».</summary>
    public static string Finish => Text(nameof(Finish));

    /// <summary>« Plein écran ».</summary>
    public static string FullScreen => Text(nameof(FullScreen));

    /// <summary>« … min … s ».</summary>
    public static string MinutesSecondsOf(object minutes, object seconds) => Format("MinutesSeconds", minutes, seconds);

    /// <summary>« Suivant ».</summary>
    public static string Next => Text(nameof(Next));

    /// <summary>« Reportés : … ».</summary>
    public static string PostponedOf(object count) => Format("Postponed", count);

    /// <summary>« Séance : … / … ».</summary>
    public static string ProgressOf(object position, object total) => Format("Progress", position, total);

    /// <summary>« Progression de la séance : élément … sur … ».</summary>
    public static string ProgressNameOf(object position, object total) => Format("ProgressName", position, total);

    /// <summary>« Restants : … ».</summary>
    public static string RemainingOf(object count) => Format("Remaining", count);

    /// <summary>« Reprendre la séance (… restants) ».</summary>
    public static string ResumeManyOf(object count) => Format("ResumeMany", count);

    /// <summary>« Reprendre la séance (… restant) ».</summary>
    public static string ResumeOneOf(object count) => Format("ResumeOne", count);

    /// <summary>« … éléments passés en revue. ».</summary>
    public static string ReviewedManyOf(object count) => Format("ReviewedMany", count);

    /// <summary>« … élément passé en revue. ».</summary>
    public static string ReviewedOneOf(object count) => Format("ReviewedOne", count);

    /// <summary>« … s ».</summary>
    public static string SecondsOf(object seconds) => Format("Seconds", seconds);

    /// <summary>« Séance ».</summary>
    public static string Session => Text(nameof(Session));

    /// <summary>« Passer ».</summary>
    public static string Skip => Text(nameof(Skip));

    /// <summary>« Commencer (…) ».</summary>
    public static string StartOf(object count) => Format("Start", count);

    /// <summary>« Séance terminée ».</summary>
    public static string SummaryTitle => Text(nameof(SummaryTitle));

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
