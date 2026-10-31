namespace KyfRev.Domain.Reviews;

/// <summary>
/// Événement de l'historique d'un élément révisable. L'historique ne fait que grandir :
/// aucun événement n'est jamais modifié ni effacé.
/// </summary>
/// <remarks>
/// Chaque événement porte <see cref="At"/>, l'instant exact en temps universel ; la plupart
/// portent aussi le jour local auquel l'action compte (antérieur à <see cref="At"/> pour une
/// validation antidatée).
/// </remarks>
public abstract record TrackingEvent
{
    private protected TrackingEvent(DateTimeOffset at)
    {
        At = at;
    }

    /// <summary>Instant exact de l'action, en temps universel.</summary>
    public DateTimeOffset At { get; }
}

/// <summary>Une révision validée. <paramref name="Origin"/> n'est présente que pour un J0.</summary>
/// <param name="At">Instant exact de la validation.</param>
/// <param name="Day">Jour auquel la révision compte.</param>
/// <param name="CycleNumber">Numéro du cycle.</param>
/// <param name="StepLabel">Étape validée, par exemple « J3 ».</param>
/// <param name="PlannedOn">Échéance prévue de cette étape.</param>
/// <param name="DaysLate">Jours de retard ; négatif pour une révision faite en avance.</param>
/// <param name="Origin">Ce qui a ouvert le cycle, pour un J0 seulement.</param>
public sealed record ReviewEvent(
    DateTimeOffset At,
    DateOnly Day,
    int CycleNumber,
    string StepLabel,
    DateOnly PlannedOn,
    int DaysLate,
    StartOrigin? Origin) : TrackingEvent(At);

/// <summary>Une échéance déplacée. Ne compte pas comme une révision.</summary>
public sealed record PostponementEvent(
    DateTimeOffset At,
    int CycleNumber,
    string StepLabel,
    DateOnly From,
    DateOnly To) : TrackingEvent(At);

/// <summary>Cycle abandonné avant sa dernière étape (redémarrage ou archivage manuel).</summary>
public sealed record InterruptionEvent(DateTimeOffset At, DateOnly Day, int CycleNumber) : TrackingEvent(At);

/// <summary>Archivage de l'élément.</summary>
public sealed record ArchivingEvent(DateTimeOffset At, DateOnly Day, ArchiveReason Reason) : TrackingEvent(At);

/// <summary>Retour d'un élément archivé en révision active.</summary>
public sealed record ReactivationEvent(DateTimeOffset At, DateOnly Day) : TrackingEvent(At);

/// <summary>Résultat d'un rappel de consolidation.</summary>
public sealed record ReminderEvent(
    DateTimeOffset At,
    DateOnly Day,
    DateOnly DrawnOn,
    ReminderOutcome Outcome) : TrackingEvent(At);

/// <summary>Inclusion dans les rappels de consolidation, ou exclusion.</summary>
public sealed record ReminderInclusionEvent(DateTimeOffset At, bool Include) : TrackingEvent(At);

/// <summary>
/// Modification d'un passage annoté (commentaire, passage ou définition) ; le calendrier n'en
/// est pas affecté.
/// </summary>
public sealed record ModificationEvent(DateTimeOffset At, ModifiedObject Changed) : TrackingEvent(At);

/// <summary>Ce qui a ouvert un cycle (son J0).</summary>
public enum StartOrigin
{
    /// <summary>Création de la fiche ou du passage (D3).</summary>
    Creation,

    /// <summary>Fichier existant ajouté au suivi (« Suivre cette fiche »).</summary>
    Adoption,

    /// <summary>Rappel « Fiche à revoir entièrement ».</summary>
    Reminder,

    /// <summary>« Recommencer à J0 » ou « Remettre en révision ».</summary>
    Restart,
}

/// <summary>Ce qu'une modification a changé dans un passage annoté.</summary>
public enum ModifiedObject
{
    /// <summary>Le commentaire.</summary>
    Comment,

    /// <summary>Le passage lui-même.</summary>
    Passage,

    /// <summary>Le terme ou l'explication d'une définition.</summary>
    Definition,
}

/// <summary>Résultat d'un rappel de consolidation, tel qu'inscrit dans l'historique.</summary>
public enum ReminderOutcome
{
    /// <summary>« Fiche connue » : la fiche se repose plus longtemps.</summary>
    Known,

    /// <summary>« Certains points sont à revoir » : repos remis au premier niveau.</summary>
    SomePointsToReview,

    /// <summary>« Fiche à revoir entièrement » : nouveau cycle.</summary>
    ReviewEntirely,
}
