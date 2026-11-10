using System.Collections.Immutable;
using System.Globalization;
using KyfRev.Application.Text;
using KyfRev.Domain.Reviews;

namespace KyfRev.Infrastructure.Vault;

/// <summary>
/// Statistiques de régularité, comme dans la 0.x (<c>statistiques.rs</c>) : l'historique de chaque fiche, passage et
/// définition, agrégé par jour, par mois et par dossier. Rien n'est écrit.
/// </summary>
public sealed partial class VaultSession
{
    /// <summary>Statistiques de régularité au jour <paramref name="day"/>.</summary>
    public Statistics Statistics(DateOnly day)
    {
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        var from = monday.AddDays(-7 * Application.Model.Statistics.WeeksShown);

        var days = new SortedDictionary<DateOnly, ActivityTally>();
        var total = new ReviewTally();
        var folders = new Dictionary<string, ReviewTally>(StringComparer.Ordinal);
        ReviewTally FolderTally(string folder)
        {
            if (!folders.TryGetValue(folder, out var tally))
            {
                folders[folder] = tally = new ReviewTally();
            }
            return tally;
        }

        foreach (var entry in _sheets.Values)
        {
            var tally = FolderTally(DisplayFolder(entry.Sheet.Path));
            tally.Sheets++;
            Tally(entry.Sheet.Tracking, sheet: true, from, days, total, tally);
        }
        foreach (var annotation in AllAnnotations)
        {
            var folder = _sheets.TryGetValue(annotation.SheetId, out var entry) ? DisplayFolder(entry.Sheet.Path) : string.Empty;
            Tally(annotation.Tracking, sheet: false, from, days, total, FolderTally(folder));
        }

        // Les séries se comptent depuis toujours : le calendrier n'en voit qu'une partie.
        var (longest, longestEnd, streak) = (0, (DateOnly?)null, 0);
        DateOnly? previous = null;
        foreach (var active in days.Keys)
        {
            streak = previous == active.AddDays(-1) ? streak + 1 : 1;
            if (streak > longest)
            {
                (longest, longestEnd) = (streak, active);
            }
            previous = active;
        }
        var current = 0;
        // La journée n'est pas finie : une série qui s'arrête hier est encore en cours.
        for (var cursor = days.ContainsKey(day) ? day : day.AddDays(-1); days.ContainsKey(cursor); cursor = cursor.AddDays(-1))
        {
            current++;
        }

        var activity = days.Where(pair => pair.Key >= from && pair.Key <= day).Select(pair => pair.Value.ToDay(pair.Key)).ToImmutableArray();
        int ReviewsSince(int count)
        {
            var start = day.AddDays(1 - count);
            return days.Where(pair => pair.Key >= start && pair.Key <= day).Sum(pair => pair.Value.Reviews);
        }

        ActivityDay? best = null;
        foreach (var candidate in activity)
        {
            // Comme le « max_by_key » de la 0.x : à égalité, le plus récent.
            if (best is null || candidate.Total >= best.Total)
            {
                best = candidate;
            }
        }

        return new Statistics(
            day,
            from,
            activity,
            days.Count > 0 ? days.Keys.First() : null,
            total.Reviews,
            total.Reminders,
            activity.Length,
            ReviewsSince(7),
            ReviewsSince(30),
            current,
            longest,
            longestEnd,
            total.OnTime,
            total.Early,
            total.Late,
            total.AverageDelay,
            total.LongestDelay,
            best,
            [.. Enumerable.Range(0, Application.Model.Statistics.MonthsShown).Reverse().Select(back => MonthStatisticsOf(day, back, days))],
            [
                .. folders
                    .Select(pair => new FolderStatistics(pair.Key, pair.Value.Sheets, pair.Value.Reviews, pair.Value.OnTime + pair.Value.Early, pair.Value.Late, pair.Value.AverageDelay))
                    .OrderByDescending(folder => folder.Reviews)
                    .ThenBy(folder => RustText.ToLowercase(folder.Folder), Utf8Order.Instance),
            ])
        {
            Forgotten = total.Forgotten,
        };
    }

    /// <summary>« 2026-10 » pour le mois de <paramref name="day"/>, décalé de <paramref name="back"/> mois en arrière.</summary>
    internal static string MonthKey(DateOnly day, int back) =>
        new DateOnly(day.Year, day.Month, 1).AddMonths(-back).ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static MonthStatistics MonthStatisticsOf(DateOnly day, int back, SortedDictionary<DateOnly, ActivityTally> days)
    {
        var month = MonthKey(day, back);
        var ofMonth = days.Where(pair => pair.Key.ToString("yyyy-MM", CultureInfo.InvariantCulture) == month).ToList();
        return new MonthStatistics(month, ofMonth.Sum(pair => pair.Value.Reviews), ofMonth.Count);
    }

    /// <summary>
    /// Inscrit l'historique d'un élément : chaque jour dans <paramref name="days"/> (depuis toujours), et ce qui tombe dans
    /// le calendrier dans <paramref name="total"/> et <paramref name="folder"/>. Un J0 a une origine : ce n'est pas une révision.
    /// </summary>
    private static void Tally(Tracking tracking, bool sheet, DateOnly from, SortedDictionary<DateOnly, ActivityTally> days, ReviewTally total, ReviewTally folder)
    {
        ActivityTally Day(DateOnly day)
        {
            if (!days.TryGetValue(day, out var activity))
            {
                days[day] = activity = new ActivityTally();
            }
            return activity;
        }

        foreach (var historyEvent in tracking.History)
        {
            switch (historyEvent)
            {
                case ReviewEvent { Origin: null } review:
                    if (sheet)
                    {
                        Day(review.Day).Sheets++;
                    }
                    else
                    {
                        Day(review.Day).Passages++;
                    }
                    if (review.Day >= from)
                    {
                        total.AddReview(review.DaysLate, review.Forgotten);
                        folder.AddReview(review.DaysLate, review.Forgotten);
                    }
                    break;
                case ReminderEvent reminder:
                    Day(reminder.Day).Reminders++;
                    if (reminder.Day >= from)
                    {
                        total.Reminders++;
                        folder.Reminders++;
                    }
                    break;
            }
        }
    }

    private sealed class ActivityTally
    {
        public int Sheets { get; set; }

        public int Passages { get; set; }

        public int Reminders { get; set; }

        public int Reviews => Sheets + Passages;

        public ActivityDay ToDay(DateOnly day) => new(day, Sheets, Passages, Reminders);
    }

    private sealed class ReviewTally
    {
        private long _totalDelay;

        public int Sheets { get; set; }

        public int Reviews { get; private set; }

        public int OnTime { get; private set; }

        public int Early { get; private set; }

        public int Late { get; private set; }

        public int LongestDelay { get; private set; }

        public int Reminders { get; set; }

        public int Forgotten { get; private set; }

        public double AverageDelay => Late == 0 ? 0 : (double)_totalDelay / Late;

        public void AddReview(int daysLate, bool forgotten)
        {
            Reviews++;
            Forgotten += forgotten ? 1 : 0;
            if (daysLate > 0)
            {
                Late++;
                _totalDelay += daysLate;
                LongestDelay = Math.Max(LongestDelay, daysLate);
            }
            else if (daysLate == 0)
            {
                OnTime++;
            }
            else
            {
                Early++;
            }
        }
    }
}
