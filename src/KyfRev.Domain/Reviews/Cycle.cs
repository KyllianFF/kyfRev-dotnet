using System.Collections.Immutable;
using KyfRev.Domain.Calendar;

namespace KyfRev.Domain.Reviews;

/// <summary>
/// Le cycle en cours : un passage complet de J0 à la dernière étape.
/// </summary>
/// <param name="Number">Numéro du cycle, à partir de 1.</param>
/// <param name="Sequence">
/// Copie de la séquence au démarrage du cycle : modifier la séquence dans les réglages ne
/// touche pas un cycle en cours.
/// </param>
/// <param name="StartedOn">Jour du J0 de ce cycle.</param>
/// <param name="Step">Index de l'étape attendue, c'est-à-dire la prochaine à valider.</param>
/// <param name="DueDate">Échéance de l'étape attendue.</param>
/// <param name="LastReview">Jour de la dernière révision validée.</param>
/// <param name="SuspendedOn">
/// Jour où l'élément a été suspendu ; rien s'il ne l'est pas. Suspendu, il garde son étape et son échéance, mais ne se
/// présente plus à réviser tant qu'il n'est pas repris.
/// </param>
public sealed record Cycle(
    int Number,
    ReviewSequence Sequence,
    DateOnly StartedOn,
    int Step,
    DateOnly DueDate,
    DateOnly LastReview,
    DateOnly? SuspendedOn = null)
{
    /// <summary>« J7 » si l'étape attendue est J7.</summary>
    public string StepLabel => Sequence.Label(Step);

    /// <summary>Jours de retard au jour donné (0 si l'échéance n'est pas dépassée).</summary>
    public int DaysLate(DateOnly day) => Math.Max(CivilCalendar.DaysBetween(DueDate, day), 0);

    /// <summary>
    /// Étapes suivantes qui tomberont jusqu'au <paramref name="until"/> inclus si chaque
    /// révision est faite le jour de son échéance (une échéance dépassée : le jour même), comme
    /// le prévoit la règle D7. L'étape attendue n'en fait pas partie, et rien ne vient après la
    /// dernière (le cycle se termine). Sert à prévoir la charge (D27).
    /// </summary>
    public ImmutableArray<ProjectedStep> ProjectedSteps(DateOnly day, DateOnly until)
    {
        var date = DueDate > day ? DueDate : day;
        var projected = ImmutableArray.CreateBuilder<ProjectedStep>();
        for (var step = Step + 1; step <= Sequence.LastStep; step++)
        {
            if (!CivilCalendar.TryAddDays(date, Sequence.IntervalBefore(step), out date) || date > until)
            {
                break;
            }
            projected.Add(new ProjectedStep(date, step));
        }
        return projected.ToImmutable();
    }
}

/// <summary>Étape projetée : la date à laquelle elle tombera et son index dans la séquence.</summary>
public readonly record struct ProjectedStep(DateOnly Date, int Step);

/// <summary>Nombre de cycles commencés, terminés et interrompus d'un élément.</summary>
public readonly record struct CycleCounters(int Started, int Completed, int Interrupted);
