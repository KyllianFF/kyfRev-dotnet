using System.Diagnostics.CodeAnalysis;

namespace KyfRev.Domain.Reviews;

/// <summary>Issue d'une révision validée.</summary>
public abstract record ReviewOutcome
{
    private protected ReviewOutcome()
    {
    }
}

/// <summary>L'étape suivante est planifiée.</summary>
/// <param name="StepLabel">Étape désormais attendue, par exemple « J7 ».</param>
/// <param name="DueDate">Son échéance.</param>
public sealed record NextStepScheduled(string StepLabel, DateOnly DueDate) : ReviewOutcome;

/// <summary>La dernière étape vient d'être validée : l'élément est archivé.</summary>
public sealed record CycleCompleted(DateOnly ArchivedOn) : ReviewOutcome;

/// <summary>Réponse de l'utilisateur à un rappel de consolidation.</summary>
public readonly record struct ReminderAnswer
{
    private ReminderAnswer(ReminderOutcome outcome, int markedPassages)
    {
        Outcome = outcome;
        MarkedPassages = markedPassages;
    }

    /// <summary>« Fiche connue ».</summary>
    public static ReminderAnswer Known => new(ReminderOutcome.Known, 0);

    /// <summary>« Fiche à revoir entièrement ».</summary>
    public static ReminderAnswer ReviewEntirely => new(ReminderOutcome.ReviewEntirely, 0);

    /// <summary>Résultat inscrit dans l'historique.</summary>
    public ReminderOutcome Outcome { get; }

    /// <summary>
    /// Passages marqués à revoir, créés ou remis à J0 par l'appelant (« Certains points sont à
    /// revoir » seulement).
    /// </summary>
    public int MarkedPassages { get; }

    /// <summary>« Certains points sont à revoir », avec le nombre de passages marqués.</summary>
    public static ReminderAnswer SomePointsToReview(int markedPassages)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(markedPassages);
        return new(ReminderOutcome.SomePointsToReview, markedPassages);
    }
}

/// <summary>Résultat d'une opération sur un suivi : le nouveau suivi, ou la raison du refus.</summary>
public sealed record TrackingResult
{
    private TrackingResult(Tracking? tracking, TrackingError? error)
    {
        Tracking = tracking;
        Error = error;
    }

    /// <summary>Vrai si l'opération a été acceptée.</summary>
    [MemberNotNullWhen(true, nameof(Tracking))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Tracking is not null;

    /// <summary>Nouveau suivi, en cas de succès. Le suivi d'origine n'est jamais modifié.</summary>
    public Tracking? Tracking { get; }

    /// <summary>Raison du refus, en cas d'échec.</summary>
    public TrackingError? Error { get; }

    internal static TrackingResult Success(Tracking tracking) => new(tracking, null);

    internal static TrackingResult Failure(TrackingError error) => new(null, error);
}

/// <summary>Résultat d'une validation de révision : le nouveau suivi et l'issue, ou la raison du refus.</summary>
public sealed record ReviewResult
{
    private ReviewResult(Tracking? tracking, ReviewOutcome? outcome, TrackingError? error)
    {
        Tracking = tracking;
        Outcome = outcome;
        Error = error;
    }

    /// <summary>Vrai si la révision a été validée.</summary>
    [MemberNotNullWhen(true, nameof(Tracking), nameof(Outcome))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Tracking is not null;

    /// <summary>Nouveau suivi, en cas de succès.</summary>
    public Tracking? Tracking { get; }

    /// <summary>Étape suivante planifiée, ou fin de cycle.</summary>
    public ReviewOutcome? Outcome { get; }

    /// <summary>Raison du refus, en cas d'échec.</summary>
    public TrackingError? Error { get; }

    internal static ReviewResult Success(Tracking tracking, ReviewOutcome outcome) => new(tracking, outcome, null);

    internal static ReviewResult Failure(TrackingError error) => new(null, null, error);
}
