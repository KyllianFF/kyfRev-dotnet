using System.Globalization;

namespace KyfRev.Domain.Reviews;

/// <summary>Nature d'une opération refusée sur un élément révisable.</summary>
public enum TrackingErrorCode
{
    /// <summary>L'élément n'est pas en révision active.</summary>
    NotActive,

    /// <summary>L'élément n'est pas archivé.</summary>
    NotArchived,

    /// <summary>Une révision est déjà enregistrée ce jour-là.</summary>
    AlreadyReviewed,

    /// <summary>Validation à une date future.</summary>
    FutureDate,

    /// <summary>Validation à une date antérieure à la dernière révision.</summary>
    BeforeLastReview,

    /// <summary>Report vers aujourd'hui ou une date passée.</summary>
    PostponeInPast,

    /// <summary>Report vers l'échéance déjà prévue.</summary>
    PostponeUnchanged,

    /// <summary>Aucun rappel de consolidation en attente.</summary>
    NoPendingReminder,

    /// <summary>Un rappel de consolidation est déjà en attente.</summary>
    ReminderAlreadyPending,

    /// <summary>La fiche n'est pas encore éligible aux rappels.</summary>
    NotYetEligible,

    /// <summary>La fiche est exclue des rappels.</summary>
    ExcludedFromReminders,

    /// <summary>« Certains points à revoir » sans aucun passage marqué.</summary>
    NoPassageMarked,

    /// <summary>Réglages des rappels invalides.</summary>
    InvalidRules,

    /// <summary>Suivi incohérent (contrôle d'intégrité).</summary>
    Inconsistent,

    /// <summary>Date hors des limites du calendrier.</summary>
    DateOutOfRange,

    /// <summary>L'élément est suspendu : il se reprend d'abord.</summary>
    Suspended,

    /// <summary>L'élément n'est pas suspendu.</summary>
    NotSuspended,
}

/// <summary>Opération refusée, avec un message destiné à l'utilisateur.</summary>
public sealed record TrackingError(TrackingErrorCode Code, string Message)
{
    internal static TrackingError NotActive() =>
        new(TrackingErrorCode.NotActive, "Cet élément n'est pas en révision active.");

    internal static TrackingError NotArchived() =>
        new(TrackingErrorCode.NotArchived, "Cet élément n'est pas archivé.");

    internal static TrackingError AlreadyReviewed(DateOnly day) =>
        new(TrackingErrorCode.AlreadyReviewed, $"Une révision est déjà enregistrée le {Iso(day)}.");

    internal static TrackingError FutureDate(DateOnly day) =>
        new(TrackingErrorCode.FutureDate, $"Impossible de valider une révision à une date future ({Iso(day)}).");

    internal static TrackingError BeforeLastReview(DateOnly day, DateOnly lastReview) =>
        new(TrackingErrorCode.BeforeLastReview, $"La date {Iso(day)} précède la dernière révision ({Iso(lastReview)}).");

    internal static TrackingError PostponeInPast(DateOnly today) =>
        new(TrackingErrorCode.PostponeInPast, $"Le report doit viser une date postérieure à aujourd'hui ({Iso(today)}).");

    internal static TrackingError PostponeUnchanged(DateOnly day) =>
        new(TrackingErrorCode.PostponeUnchanged, $"Cette révision est déjà prévue le {Iso(day)}.");

    internal static TrackingError NoPendingReminder() =>
        new(TrackingErrorCode.NoPendingReminder, "Aucun rappel de consolidation n'est en attente pour cette fiche.");

    internal static TrackingError ReminderAlreadyPending() =>
        new(TrackingErrorCode.ReminderAlreadyPending, "Un rappel de consolidation est déjà en attente pour cette fiche.");

    internal static TrackingError NotYetEligible(DateOnly eligibleOn) =>
        new(TrackingErrorCode.NotYetEligible, $"Cette fiche ne pourra être rappelée qu'à partir du {Iso(eligibleOn)}.");

    internal static TrackingError ExcludedFromReminders() =>
        new(TrackingErrorCode.ExcludedFromReminders, "Cette fiche est exclue des rappels de consolidation.");

    internal static TrackingError NoPassageMarked() =>
        new(TrackingErrorCode.NoPassageMarked, "Marquez au moins un passage à revoir, ou choisissez « Fiche connue ».");

    internal static TrackingError InvalidRules(string reason) =>
        new(TrackingErrorCode.InvalidRules, $"Réglages des rappels invalides : {reason}");

    internal static TrackingError Inconsistent(string reason) =>
        new(TrackingErrorCode.Inconsistent, $"Le suivi est incohérent : {reason}");

    internal static TrackingError Suspended() =>
        new(TrackingErrorCode.Suspended, "Cet élément est suspendu : reprenez-le d'abord.");

    internal static TrackingError NotSuspended() =>
        new(TrackingErrorCode.NotSuspended, "Cet élément n'est pas suspendu.");

    internal static TrackingError DateOutOfRange() =>
        new(TrackingErrorCode.DateOutOfRange, "Date hors des limites du calendrier.");

    /// <summary>Date au format AAAA-MM-JJ, quelle que soit la culture.</summary>
    private static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
