using System.Collections.Immutable;
using KyfRev.Domain.Calendar;

namespace KyfRev.Domain.Reviews;

/// <summary>Une échéance à placer : celle d'une fiche ou d'un passage en révision.</summary>
/// <param name="Key">Ce qui la désigne pour l'appelant.</param>
/// <param name="DueDate">Son échéance actuelle.</param>
/// <param name="Interval">Jours de l'étape attendue : 7 pour un J7.</param>
public readonly record struct SpreadCandidate(string Key, DateOnly DueDate, int Interval);

/// <summary>Une échéance déplacée par l'étalement.</summary>
/// <param name="Key">Ce qui la désigne pour l'appelant.</param>
/// <param name="To">Son nouveau jour.</param>
public readonly record struct SpreadMove(string Key, DateOnly To);

/// <summary>Le résultat d'un étalement : ce qui reste où il est, et ce qui bouge.</summary>
/// <param name="Kept">Échéances gardées aujourd'hui (rattrapage seulement).</param>
/// <param name="Moves">Échéances déplacées, dans l'ordre où elles ont été placées.</param>
public sealed record SpreadResult(int Kept, ImmutableArray<SpreadMove> Moves)
{
    /// <summary>Le dernier jour utilisé ; rien si rien ne bouge.</summary>
    public DateOnly? LastDay => Moves.IsEmpty ? null : Moves.Max(move => move.To);
}

/// <summary>
/// Étale des échéances sur les jours qui suivent, sans dépasser un plafond par jour. Deux usages : rattraper un retard
/// (retour d'absence) et libérer des jours annoncés (pause). Rien n'est révisé ni recommencé : ce sont des reports
/// ordinaires, que l'appelant inscrit dans l'historique. Le calcul ne dépend que de ses entrées.
/// </summary>
public static class DueSpreading
{
    /// <summary>Au-delà, ce qui reste se pose sur le dernier jour : un plafond trop bas ne repousse pas sans fin.</summary>
    public const int HorizonDays = 365;

    /// <summary>
    /// Rattrapage : de ce qui est dû aujourd'hui ou avant, les plus fragiles restent aujourd'hui, à concurrence du plafond ;
    /// le reste part sur les jours suivants. Le plus fragile est celui dont le retard pèse le plus face à son étape : un J1
    /// en retard de trois jours passe avant un J30 en retard de cinq.
    /// </summary>
    /// <param name="active">Toutes les échéances en révision.</param>
    /// <param name="today">Aujourd'hui.</param>
    /// <param name="perDay">Plafond par jour, au moins 1.</param>
    public static SpreadResult CatchUp(IEnumerable<SpreadCandidate> active, DateOnly today, int perDay)
    {
        ArgumentNullException.ThrowIfNull(active);
        ArgumentOutOfRangeException.ThrowIfLessThan(perDay, 1);
        var all = active.ToList();
        var late = all
            .Where(item => item.DueDate <= today)
            .OrderByDescending(item => (double)CivilCalendar.DaysBetween(item.DueDate, today) / Math.Max(item.Interval, 1))
            .ThenBy(item => item.Interval)
            .ThenBy(item => item.DueDate)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToList();
        var kept = Math.Min(perDay, late.Count);
        return new SpreadResult(kept, Place(late.Skip(kept), today.AddDays(1), perDay, LoadOf(all.Where(item => item.DueDate > today))));
    }

    /// <summary>
    /// Pause : ce qui tombe du <paramref name="from"/> au <paramref name="to"/> inclus part sur les jours qui suivent le
    /// retour, dans l'ordre des échéances.
    /// </summary>
    /// <param name="active">Toutes les échéances en révision.</param>
    /// <param name="from">Premier jour de la pause.</param>
    /// <param name="to">Dernier jour de la pause.</param>
    /// <param name="perDay">Plafond par jour, au moins 1.</param>
    public static SpreadResult Pause(IEnumerable<SpreadCandidate> active, DateOnly from, DateOnly to, int perDay)
    {
        ArgumentNullException.ThrowIfNull(active);
        ArgumentOutOfRangeException.ThrowIfLessThan(perDay, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(to, from);
        var all = active.ToList();
        var during = all
            .Where(item => item.DueDate >= from && item.DueDate <= to)
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.Interval)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToList();
        return new SpreadResult(0, Place(during, to.AddDays(1), perDay, LoadOf(all.Where(item => item.DueDate > to))));
    }

    private static Dictionary<DateOnly, int> LoadOf(IEnumerable<SpreadCandidate> staying) =>
        staying.GroupBy(item => item.DueDate).ToDictionary(group => group.Key, group => group.Count());

    /// <summary>Remplit chaque jour jusqu'au plafond, ce qui y tombe déjà compris.</summary>
    private static ImmutableArray<SpreadMove> Place(IEnumerable<SpreadCandidate> items, DateOnly first, int perDay, Dictionary<DateOnly, int> load)
    {
        var moves = ImmutableArray.CreateBuilder<SpreadMove>();
        var day = first;
        var last = first.AddDays(HorizonDays);
        var free = perDay - load.GetValueOrDefault(day);
        foreach (var item in items)
        {
            while (free <= 0 && day < last)
            {
                day = day.AddDays(1);
                free = perDay - load.GetValueOrDefault(day);
            }
            moves.Add(new SpreadMove(item.Key, day));
            free--;
        }
        return moves.ToImmutable();
    }
}
