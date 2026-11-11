using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;

namespace KyfRev.Infrastructure.Vault;

public sealed partial class VaultSession
{
    /// <summary>
    /// Étale des échéances (voir <see cref="DueSpreading"/>) : un retard à rattraper, ou des jours de pause à libérer. Sans
    /// <paramref name="apply"/>, rien n'est écrit : c'est le plan, à montrer avant de l'accepter. Appliqué, chaque échéance
    /// déplacée est un report ordinaire, inscrit dans l'historique ; aucun cycle ne recommence.
    /// </summary>
    public SpreadPlan SpreadDueDates(SpreadRequest request, Moment now, bool apply)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PerDay is < 1 or > SpreadRequest.MaxPerDay)
        {
            throw VaultException.InvalidSettings($"le nombre de révisions par jour va de 1 à {SpreadRequest.MaxPerDay}.");
        }
        var items = ActiveDueDates();
        SpreadResult result;
        if (request.PauseFrom is { } from)
        {
            var to = request.PauseTo ?? from;
            if (from <= now.Day)
            {
                throw VaultException.InvalidSettings("la pause commence au plus tôt demain.");
            }
            if (to < from)
            {
                throw VaultException.InvalidSettings("la pause finit avant de commencer.");
            }
            if (CivilCalendar.DaysBetween(from, to) >= SpreadRequest.MaxPauseDays)
            {
                throw VaultException.InvalidSettings($"une pause dure au plus {SpreadRequest.MaxPauseDays} jours.");
            }
            result = DueSpreading.Pause(items.Select(item => item.Candidate), from, to, request.PerDay);
        }
        else
        {
            result = DueSpreading.CatchUp(items.Select(item => item.Candidate), now.Day, request.PerDay);
        }
        if (apply)
        {
            var byKey = items.ToDictionary(item => item.Candidate.Key, StringComparer.Ordinal);
            foreach (var move in result.Moves)
            {
                var (kind, id, _) = byKey[move.Key];
                ApplyTo(kind, id, (tracking, _) => tracking.Postpone(now, move.To));
            }
        }
        return new SpreadPlan(result.Kept, result.Moves.Length, result.LastDay);
    }

    /// <summary>
    /// Ce qui tombe déjà chaque jour, pour le lissage ; rien si l'option est coupée, et l'échéance reste alors exacte.
    /// </summary>
    public Func<DateOnly, int>? SmoothingLoad()
    {
        if (!Settings.SmoothDueDates)
        {
            return null;
        }
        var load = ActiveDueDates().GroupBy(item => item.Candidate.DueDate).ToDictionary(group => group.Key, group => group.Count());
        return day => load.GetValueOrDefault(day);
    }

    /// <summary>Les fiches présentes et leurs passages hors corbeille, en révision et pas suspendus, avec l'étape attendue.</summary>
    private List<(ItemKind Kind, string Id, SpreadCandidate Candidate)> ActiveDueDates()
    {
        var items = new List<(ItemKind, string, SpreadCandidate)>();
        foreach (var entry in _sheets.Values.Where(entry => entry.Presence != FilePresence.Missing))
        {
            if (entry.Sheet.Tracking.DueCycle is { } cycle)
            {
                items.Add((ItemKind.Sheet, entry.Sheet.Id, Candidate(entry.Sheet.Id, cycle)));
            }
            foreach (var annotation in Annotations(entry.Sheet.Id).Where(a => a.TrashedAt is null))
            {
                if (annotation.Tracking.DueCycle is { } passageCycle)
                {
                    items.Add((ItemKind.Passage, annotation.Id, Candidate(annotation.Id, passageCycle)));
                }
            }
        }
        return items;
    }

    private static SpreadCandidate Candidate(string id, Cycle cycle) =>
        new(id, cycle.DueDate, cycle.Sequence.IntervalBefore(cycle.Step));
}
