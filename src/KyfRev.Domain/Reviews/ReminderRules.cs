using System.Collections.Immutable;

namespace KyfRev.Domain.Reviews;

/// <summary>Réglages des rappels de consolidation (section 7.5 de la conception).</summary>
/// <param name="MinimumDelayMonths">Délai avant le premier rappel, en mois calendaires (D5).</param>
/// <param name="RestSequenceMonths">Durée de repos après chaque rappel « connue », niveau par niveau.</param>
/// <param name="MaxPerDay">Nombre maximal de rappels en attente en même temps.</param>
/// <param name="MaxWaitDays">Au-delà de cette attente, une fiche éligible passe en priorité.</param>
/// <param name="IncludeManualArchiving">Vrai si une fiche archivée manuellement reçoit aussi des rappels.</param>
public sealed record ReminderRules(
    int MinimumDelayMonths,
    ImmutableArray<int> RestSequenceMonths,
    int MaxPerDay,
    int MaxWaitDays,
    bool IncludeManualArchiving)
{
    /// <summary>2 mois, puis repos de 2, 4, 8 et 12 mois ; 2 rappels au plus ; priorité après 30 jours.</summary>
    public static ReminderRules Default { get; } = new(2, [2, 4, 8, 12], 2, 30, IncludeManualArchiving: true);

    /// <summary>Contrôle les réglages.</summary>
    /// <returns><see langword="null"/> s'ils sont valides, sinon la raison du refus.</returns>
    public TrackingError? Validate()
    {
        if (MinimumDelayMonths <= 0)
        {
            return TrackingError.InvalidRules("le délai minimal doit être d'au moins un mois");
        }
        if (RestSequenceMonths.IsDefaultOrEmpty || RestSequenceMonths.Any(months => months <= 0))
        {
            return TrackingError.InvalidRules("chaque durée de repos doit être d'au moins un mois");
        }
        if (MaxWaitDays <= 0)
        {
            return TrackingError.InvalidRules("l'attente maximale doit être d'au moins un jour");
        }
        if (MaxPerDay < 0)
        {
            return TrackingError.InvalidRules("le nombre de rappels par jour ne peut pas être négatif");
        }
        return null;
    }
}
