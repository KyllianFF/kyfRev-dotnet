using KyfRev.Domain.Calendar;
using KyfRev.Domain.Randomness;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Text;

namespace KyfRev.Infrastructure.Vault;

public sealed partial class VaultSession
{
    /// <summary>Examens passés depuis plus de 7 jours : plus montrés (mais gardés).</summary>
    private const int DaysShownAfterExam = 7;

    private static readonly VaultPath DrawsFile = VaultPath.Parse(VaultLayout.DrawsFile);

    /// <summary>
    /// Passages d'une fiche marqués ce jour-là : leur cycle a commencé ce jour (passage créé,
    /// remis à J0 ou remis en révision).
    /// </summary>
    public int MarkedPassages(string sheetId, DateOnly day) =>
        Annotations(sheetId).Count(a => a.TrashedAt is null && a.Tracking.Cycle?.StartedOn == day);

    /// <summary>
    /// Enregistre la réponse à un rappel de consolidation. « Certains points sont à revoir »
    /// exige au moins un passage marqué ce jour : c'est le coffre qui les compte.
    /// </summary>
    public Tracking HandleReminder(string sheetId, ReminderOutcome choice, Moment now)
    {
        var answer = choice switch
        {
            ReminderOutcome.Known => ReminderAnswer.Known,
            ReminderOutcome.SomePointsToReview => ReminderAnswer.SomePointsToReview(MarkedPassages(sheetId, now.Day)),
            _ => ReminderAnswer.ReviewEntirely,
        };
        var (rules, sequence) = (Settings.Reminders, Settings.Sequence);
        return ApplyToTracking(sheetId, tracking => tracking.HandleReminder(now, answer, rules, sequence));
    }

    /// <summary>« Exclure des rappels » ou « Réinclure » une fiche archivée.</summary>
    public Tracking IncludeInReminders(string sheetId, bool include, Moment now) =>
        ApplyToTracking(sheetId, tracking => tracking.IncludeInReminders(now, include));

    /// <summary>
    /// Prépare la journée : tirage quotidien des rappels de consolidation, une seule fois par jour.
    /// Rouvrir le coffre le même jour ne refait pas le tirage.
    /// </summary>
    public DayPreparation PrepareDay(Moment now)
    {
        var day = now.Day;
        var journal = ReadDraws();
        if (!journal.Draws.IsEmpty && journal.Draws[^1].Day >= day)
        {
            return new DayPreparation(day, AlreadyPrepared: true, [], 0);
        }

        var candidates = new List<DrawCandidate>();
        var pending = 0;
        foreach (var entry in _sheets.Values.Where(entry => entry.Presence == FilePresence.Present))
        {
            if (entry.Sheet.Tracking.Archive is not { } archive)
            {
                continue;
            }
            if (archive.ReminderDrawnOn is not null)
            {
                pending++;
            }
            else if (entry.Sheet.Tracking.IsEligibleOn(day))
            {
                candidates.Add(new DrawCandidate(entry.Sheet.Id, archive.EligibleOn));
            }
        }
        var result = ReminderDraw.Draw(day, candidates, pending, Settings.Reminders, DailySeed.For(Settings.Id, day));
        if (!result.IsSuccess)
        {
            throw VaultException.Refused(result.Error);
        }
        var draw = result.Draw;
        foreach (var id in draw.Selection)
        {
            ApplyToTracking(id, tracking => tracking.MarkDrawn(day));
        }

        journal = journal with
        {
            Format = 1,
            Draws = journal.Draws.Add(new RecordedDraw(day, draw.Selection, draw.UnservedPriority, UnknownProperties.None)),
        };
        ReplaceFile(Confine(DrawsFile), journal.Write());
        return new DayPreparation(day, AlreadyPrepared: false, draw.Selection, draw.UnservedPriority);
    }

    /// <summary>Fixe (ou remplace) l'examen d'un dossier (relatif à <c>fiches/</c>, vide pour tout le coffre).</summary>
    public void DefineExam(string folder, string name, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(name);
        var cleanName = name.Trim();
        if (cleanName.Length == 0)
        {
            throw VaultException.InvalidSettings("donnez un nom à l'examen.");
        }
        var cleanFolder = folder.Trim().Trim('/');
        var exams = Settings.Exams
            .Where(exam => exam.Folder != cleanFolder)
            .Append(new ExamSetting(cleanFolder, cleanName, day, UnknownProperties.None))
            .OrderBy(exam => exam.Day)
            .ThenBy(exam => RustText.ToLowercase(exam.Folder), Utf8Order.Instance);
        SaveSettings(Settings with { Exams = [.. exams] });
    }

    /// <summary>Retire l'examen d'un dossier ; rien ne change aux calendriers.</summary>
    public void WithdrawExam(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var cleanFolder = folder.Trim().Trim('/');
        SaveSettings(Settings with { Exams = [.. Settings.Exams.Where(exam => exam.Folder != cleanFolder)] });
    }

    /// <summary>Les examens, du plus proche au plus lointain ; ceux passés depuis plus de sept jours ne sont plus montrés.</summary>
    public IReadOnlyList<UpcomingExam> UpcomingExams(DateOnly day)
    {
        var upcoming = new List<UpcomingExam>();
        foreach (var exam in Settings.Exams)
        {
            var daysLeft = CivilCalendar.DaysBetween(day, exam.Day);
            if (daysLeft < -DaysShownAfterExam)
            {
                continue;
            }
            var eve = exam.Day.AddDays(-1);
            var items = ItemsInFolder(exam.Folder);
            upcoming.Add(new UpcomingExam(exam.Folder, exam.Name, exam.Day, daysLeft, items.Count, items.Count(item => item.Due > eve)));
        }
        return [.. upcoming.OrderBy(exam => exam.Day).ThenBy(exam => RustText.ToLowercase(exam.Name), Utf8Order.Instance)];
    }

    /// <summary>
    /// Ramène avant l'examen les échéances du dossier qui tombent après sa veille, réparties de
    /// demain à la veille (reports ordinaires, inscrits dans l'historique).
    /// </summary>
    /// <returns>Nombre d'échéances déplacées ; aucune si l'examen est demain ou passé.</returns>
    public int TightenBeforeExam(string folder, Moment now)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var cleanFolder = folder.Trim().Trim('/');
        var exam = Settings.Exams.FirstOrDefault(e => e.Folder == cleanFolder)
            ?? throw VaultException.InvalidSettings("aucun examen pour ce dossier.");
        var eve = exam.Day.AddDays(-1);
        var tomorrow = now.Day.AddDays(1);
        if (eve < tomorrow)
        {
            return 0;
        }
        var toMove = ItemsInFolder(cleanFolder)
            .Where(item => item.Due > eve)
            .OrderBy(item => item.Due)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
        long days = CivilCalendar.DaysBetween(tomorrow, eve) + 1;
        for (var i = 0; i < toMove.Count; i++)
        {
            var to = tomorrow.AddDays((int)(i * days / toMove.Count));
            ApplyTo(toMove[i].Kind, toMove[i].Id, (tracking, _) => tracking.Postpone(now, to));
        }
        return toMove.Count;
    }

    /// <summary>Fiches et passages actifs d'un dossier (sous-dossiers compris), avec leur échéance.</summary>
    private List<(ItemKind Kind, string Id, DateOnly Due)> ItemsInFolder(string folder)
    {
        var items = new List<(ItemKind, string, DateOnly)>();
        foreach (var entry in _sheets.Values.Where(entry => entry.Presence != FilePresence.Missing))
        {
            var sheetFolder = DisplayFolder(entry.Sheet.Path);
            var inFolder = folder.Length == 0
                || sheetFolder == folder
                || sheetFolder.StartsWith(folder + "/", StringComparison.Ordinal);
            if (!inFolder)
            {
                continue;
            }
            if (entry.Sheet.Tracking.Cycle is { } cycle)
            {
                items.Add((ItemKind.Sheet, entry.Sheet.Id, cycle.DueDate));
            }
            foreach (var annotation in Annotations(entry.Sheet.Id).Where(a => a.TrashedAt is null).OrderBy(a => a.Anchor.Start))
            {
                if (annotation.Tracking.Cycle is { } passageCycle)
                {
                    items.Add((ItemKind.Passage, annotation.Id, passageCycle.DueDate));
                }
            }
        }
        return items;
    }

    private DrawJournalFile ReadDraws()
    {
        var path = Confine(DrawsFile);
        if (!File.Exists(path))
        {
            return new DrawJournalFile(0, [], UnknownProperties.None);
        }
        try
        {
            return DrawJournalFile.Read(ReadAllBytes(path));
        }
        catch (VaultFormatException exception)
        {
            throw VaultException.UnreadableTracking(VaultLayout.DrawsFile, exception.Message);
        }
    }
}
