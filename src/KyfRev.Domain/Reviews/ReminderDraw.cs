using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using KyfRev.Domain.Calendar;
using KyfRev.Domain.Randomness;

namespace KyfRev.Domain.Reviews;

/// <summary>Fiche archivée, incluse dans les rappels et sans rappel en attente.</summary>
public readonly record struct DrawCandidate(string Id, DateOnly EligibleOn);

/// <summary>Résultat d'un tirage quotidien.</summary>
/// <param name="Selection">Identifiants des fiches tirées : prioritaires d'abord, puis tirage au sort.</param>
/// <param name="UnservedPriority">
/// Fiches prioritaires laissées de côté faute de place : le tableau de bord le signale pour
/// qu'aucune fiche ne soit oubliée en silence.
/// </param>
public sealed record DailyDraw(ImmutableArray<string> Selection, int UnservedPriority);

/// <summary>Résultat de <see cref="ReminderDraw.Draw"/>.</summary>
public sealed record DrawResult
{
    private DrawResult(DailyDraw? draw, TrackingError? error)
    {
        Draw = draw;
        Error = error;
    }

    /// <summary>Vrai si le tirage a eu lieu.</summary>
    [MemberNotNullWhen(true, nameof(Draw))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Draw is not null;

    /// <summary>Tirage du jour, en cas de succès.</summary>
    public DailyDraw? Draw { get; }

    /// <summary>Raison du refus (réglages invalides), en cas d'échec.</summary>
    public TrackingError? Error { get; }

    internal static DrawResult Success(DailyDraw draw) => new(draw, null);

    internal static DrawResult Failure(TrackingError error) => new(null, error);
}

/// <summary>Tirage quotidien des rappels de consolidation (section 7.5 de la conception).</summary>
public static class ReminderDraw
{
    /// <summary>
    /// Tire les rappels du jour.
    /// <list type="number">
    /// <item>Places = rappels par jour − rappels déjà en attente.</item>
    /// <item>Les fiches éligibles depuis au moins <see cref="ReminderRules.MaxWaitDays"/> passent
    /// d'abord, de la plus ancienne à la plus récente.</item>
    /// <item>Les places restantes sont tirées au sort, uniformément, parmi les autres.</item>
    /// </list>
    /// Le résultat ne dépend que des arguments : même jour, mêmes candidats et même graine donnent
    /// toujours le même tirage, quel que soit l'ordre des candidats.
    /// </summary>
    public static DrawResult Draw(
        DateOnly today,
        IEnumerable<DrawCandidate> candidates,
        int pendingReminders,
        ReminderRules rules,
        ulong seed)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfNegative(pendingReminders);
        if (rules.Validate() is { } error)
        {
            return DrawResult.Failure(error);
        }
        var places = Math.Max(rules.MaxPerDay - pendingReminders, 0);

        // Un identifiant en double garde sa date d'éligibilité la plus ancienne.
        var eligible = new SortedDictionary<string, DateOnly>(StringComparer.Ordinal);
        foreach (var candidate in candidates.Where(c => c.EligibleOn <= today))
        {
            eligible[candidate.Id] = eligible.TryGetValue(candidate.Id, out var known) && known < candidate.EligibleOn
                ? known
                : candidate.EligibleOn;
        }

        var priority = new List<DrawCandidate>();
        var others = new List<string>();
        foreach (var (id, eligibleOn) in eligible)
        {
            if (CivilCalendar.DaysBetween(eligibleOn, today) >= rules.MaxWaitDays)
            {
                priority.Add(new DrawCandidate(id, eligibleOn));
            }
            else
            {
                others.Add(id);
            }
        }
        priority.Sort(static (a, b) =>
            a.EligibleOn != b.EligibleOn ? a.EligibleOn.CompareTo(b.EligibleOn) : string.CompareOrdinal(a.Id, b.Id));

        var served = Math.Min(priority.Count, places);
        var selection = ImmutableArray.CreateBuilder<string>(places);
        selection.AddRange(priority.Take(served).Select(c => c.Id));

        // Mélange de Fisher-Yates partiel : chaque fiche a la même chance.
        var toDraw = Math.Min(places - served, others.Count);
        var random = ChaCha8Random.FromSeed(seed);
        for (var i = 0; i < toDraw; i++)
        {
            var j = i + random.NextBounded(others.Count - i);
            (others[i], others[j]) = (others[j], others[i]);
        }
        selection.AddRange(others.Take(toDraw));

        return DrawResult.Success(new DailyDraw(selection.ToImmutable(), priority.Count - served));
    }
}
