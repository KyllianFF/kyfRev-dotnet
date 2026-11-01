using System.Collections.Immutable;
using KyfRev.Application.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;

namespace KyfRev.Infrastructure.Vault;

/// <summary>
/// Tableau de bord « Aujourd'hui » et vue Archives, comme dans la 0.x (<c>tableau.rs</c>,
/// <c>archives.rs</c>) : mêmes listes, mêmes tris (titres en minuscules de Rust, comparés octet
/// par octet), même charge des 30 jours.
/// </summary>
public sealed partial class VaultSession
{
    /// <summary>Longueur au-delà de laquelle un passage ou un commentaire est abrégé.</summary>
    private const int ExcerptLength = 160;

    /// <summary>Tableau de bord du jour <paramref name="day"/>.</summary>
    /// <exception cref="VaultException">Le journal des tirages est illisible.</exception>
    public Dashboard Dashboard(DateOnly day)
    {
        var horizon = day.AddDays(Application.Model.Dashboard.UpcomingDays);
        var workloadEnd = day.AddDays(Application.Model.Dashboard.WorkloadDays);
        var items = new List<DueItem>();
        var projected = new List<(DateOnly Date, ProjectedItem Item)>();
        void Project(DueItem item, Cycle cycle)
        {
            foreach (var step in cycle.ProjectedSteps(day, workloadEnd))
            {
                projected.Add((step.Date, new ProjectedItem(item.Kind, item.Id, item.SheetId, item.Title, item.Excerpt, item.Term, item.Section, cycle.Sequence.Label(step.Step))));
            }
        }

        foreach (var entry in _sheets.Values)
        {
            if (entry.Sheet.Tracking.Cycle is not { } cycle)
            {
                continue;
            }
            var sheet = entry.Sheet;
            var item = new DueItem(
                ItemKind.Sheet, sheet.Id, sheet.Id, sheet.Title, null, null, [], null, DisplayFolder(sheet.Path), sheet.Tags,
                cycle.StepLabel, cycle.DueDate, cycle.DaysLate(day), cycle.LastReview, cycle.Number, entry.Presence == FilePresence.Present);
            items.Add(item);
            Project(item, cycle);
        }
        foreach (var annotation in AllAnnotations.Where(a => a.TrashedAt is null))
        {
            if (annotation.Tracking.Cycle is not { } cycle)
            {
                continue;
            }
            var sheet = _sheets.GetValueOrDefault(annotation.SheetId);
            var item = new DueItem(
                ItemKind.Passage,
                annotation.Id,
                annotation.SheetId,
                sheet?.Sheet.Title ?? "Fiche introuvable",
                Abbreviate(annotation.Anchor.Exact),
                annotation.Definition?.Term,
                annotation.Anchor.Section,
                annotation.Comment is { } comment ? Abbreviate(comment) : null,
                sheet is null ? string.Empty : DisplayFolder(sheet.Sheet.Path),
                sheet?.Sheet.Tags ?? [],
                cycle.StepLabel,
                cycle.DueDate,
                cycle.DaysLate(day),
                cycle.LastReview,
                cycle.Number,
                sheet?.Presence == FilePresence.Present);
            items.Add(item);
            Project(item, cycle);
        }

        var next = items.Where(item => item.DueDate > day).Select(item => (DateOnly?)item.DueDate).Min();
        var workload = Enumerable.Range(0, Application.Model.Dashboard.WorkloadDays + 1)
            .Select(offset => (Day: day.AddDays(offset), Due: new List<DueItem>(), Projected: new List<ProjectedItem>()))
            .ToList();
        int? Slot(DateOnly date)
        {
            var offset = CivilCalendar.DaysBetween(day, date > day ? date : day);
            return offset <= Application.Model.Dashboard.WorkloadDays ? offset : null;
        }
        foreach (var item in items)
        {
            if (Slot(item.DueDate) is { } slot)
            {
                workload[slot].Due.Add(item);
            }
        }
        foreach (var (date, item) in projected)
        {
            if (Slot(date) is { } slot)
            {
                workload[slot].Projected.Add(item);
            }
        }

        var overdue = items.Where(item => item.DueDate < day).OrderBy(item => item.DueDate).ThenBy(item => item, TitleOrder.Instance);
        var today = items.Where(item => item.DueDate == day).OrderBy(item => item.Kind == ItemKind.Passage).ThenBy(item => item, TitleOrder.Instance);
        var upcoming = items.Where(item => item.DueDate > day && item.DueDate <= horizon).OrderBy(item => item.DueDate).ThenBy(item => item, TitleOrder.Instance);

        var reminders = new List<PendingReminder>();
        var (active, archived) = (0, 0);
        foreach (var entry in _sheets.Values)
        {
            var tracking = entry.Sheet.Tracking;
            if (tracking.Archive is not { } archive)
            {
                active++;
                continue;
            }
            archived++;
            if (archive.ReminderDrawnOn is { } drawnOn)
            {
                reminders.Add(new PendingReminder(
                    entry.Sheet.Id, entry.Sheet.Title, DisplayFolder(entry.Sheet.Path), entry.Sheet.Tags, archive.ArchivedOn, drawnOn,
                    tracking.History.Count(e => e is ReminderEvent)));
            }
        }

        return new Dashboard(
            day,
            [.. overdue],
            [.. today],
            [.. reminders.OrderBy(r => r.DrawnOn).ThenBy(r => RustText.ToLowercase(r.Title), Utf8Order.Instance)],
            [.. upcoming],
            [.. workload.Select(slot => new WorkloadDay(
                slot.Day,
                [.. slot.Due.OrderBy(item => item.Kind == ItemKind.Passage).ThenBy(item => item, TitleOrder.Instance)],
                [.. slot.Projected.OrderBy(item => item.Kind == ItemKind.Passage).ThenBy(item => RustText.ToLowercase(item.Title), Utf8Order.Instance)]))],
            Settings.Reminders.MaxPerDay,
            Settings.ActiveRecall,
            UpcomingExams(day),
            next,
            active,
            archived,
            Health.Problems + AnnotationsToCheck + BrokenLinks().Count,
            ReadDraws().Draws is { Length: > 0 } draws ? (int)draws[^1].UnservedPriority : 0,
            Settings.Quotes.Active,
            SchemasActive);
    }

    /// <summary>Fiches archivées, les plus récemment archivées d'abord.</summary>
    public IReadOnlyList<ArchivedSheetEntry> Archives(DateOnly day) =>
    [
        .. _sheets.Values
            .Where(entry => entry.Sheet.Tracking.Archive is not null)
            .Select(entry =>
            {
                var tracking = entry.Sheet.Tracking;
                var archive = tracking.Archive!;
                var status = !archive.IncludedInReminders ? ReminderStatus.Excluded
                    : archive.ReminderDrawnOn is not null ? ReminderStatus.Pending
                    : archive.EligibleOn <= day ? ReminderStatus.Eligible
                    : ReminderStatus.Resting;
                var reminders = tracking.History.OfType<ReminderEvent>().ToList();
                return new ArchivedSheetEntry(
                    entry.Sheet.Id,
                    entry.Sheet.Title,
                    DisplayFolder(entry.Sheet.Path),
                    entry.Sheet.Tags,
                    archive.ArchivedOn,
                    archive.Reason == ArchiveReason.Manual,
                    status,
                    archive.EligibleOn,
                    archive.ReminderDrawnOn,
                    tracking.Counters.Completed,
                    reminders.Count,
                    reminders.Count > 0 ? reminders[^1].Day : null,
                    reminders.Count > 0 ? reminders[^1].Outcome : null,
                    entry.Presence == FilePresence.Present);
            })
            .OrderByDescending(archived => archived.ArchivedOn)
            .ThenBy(archived => RustText.ToLowercase(archived.Title), Utf8Order.Instance),
    ];

    /// <summary>Blancs réduits à une espace ; au-delà de 160 caractères, coupé et terminé par « … ».</summary>
    internal static string Abbreviate(string text)
    {
        var clean = RustText.CollapseWhitespace(text);
        return RustText.CharCount(clean) <= ExcerptLength ? clean : RustText.Take(clean, ExcerptLength - 1) + "…";
    }

    /// <summary>Ordre des éléments à réviser de la 0.x : titre en minuscules, puis passage abrégé (aucun d'abord).</summary>
    private sealed class TitleOrder : IComparer<DueItem>
    {
        public static readonly TitleOrder Instance = new();

        public int Compare(DueItem? x, DueItem? y)
        {
            var byTitle = Utf8Order.Instance.Compare(RustText.ToLowercase(x!.Title), RustText.ToLowercase(y!.Title));
            if (byTitle != 0)
            {
                return byTitle;
            }
            return (x.Excerpt, y.Excerpt) switch
            {
                (null, null) => 0,
                (null, _) => -1,
                (_, null) => 1,
                var (a, b) => Utf8Order.Instance.Compare(a, b),
            };
        }
    }
}
