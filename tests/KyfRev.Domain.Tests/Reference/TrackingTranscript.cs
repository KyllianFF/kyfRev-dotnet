using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KyfRev.Domain.Reviews;
using KyfRev.Domain.Tests.Support;

namespace KyfRev.Domain.Tests.Reference;

/// <summary>
/// Rejoue la suite d'opérations n exactement comme <c>transcrire_suivi</c> dans
/// <c>tools/reference-vectors/src/main.rs</c> : mêmes tirages SplitMix, dans le même ordre, et
/// même transcription (états JSON, issues, messages d'erreur).
/// </summary>
internal sealed class TrackingTranscript
{
    private readonly StringBuilder _text = new();
    private readonly StringBuilder _codes = new();
    private Tracking _tracking;

    private TrackingTranscript(Tracking tracking)
    {
        _tracking = tracking;
    }

    public string Codes => _codes.ToString();

    public string Text => _text.ToString();

    /// <summary>Ligne attendue dans <c>tracking.txt</c> : codes, puis empreinte de la transcription.</summary>
    public string ReferenceLine =>
        Codes + "|" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Text)));

    public static TrackingTranscript For(ulong n)
    {
        var random = new SplitMix(ReferenceCases.TrackingBase + n);
        var day = DateOnly.FromDayNumber(Dates.D(2026, 1, 1).DayNumber + random.Below(730));
        var passage = random.Below(10) < 3;
        var rulesIndex = random.Below(3);
        var sheetRules = ReferenceCases.Rules(rulesIndex);
        var rules = passage ? null : sheetRules;
        var origin = ReferenceCases.Origins[random.Below(4)];
        var start = Tracking.Start(Dates.At(day), ReferenceCases.Sequence(random.Below(4)), origin).Ok();

        var t = new TrackingTranscript(start);
        t.Line(Invariant($"suivi {n} passage {Bool(passage)} regles {rulesIndex} | {RustJson.Serialize(start)}"));

        var operations = 1 + random.Below(40);
        for (var o = 0; o < operations; o++)
        {
            var advance = random.Below(3) == 0 ? random.Below(91) : random.Below(4);
            day = day.AddDays(advance);
            var now = Dates.At(day);
            t.Line($"jour {RustJson.Day(day)}");

            switch (random.Below(13))
            {
                case <= 2:
                    {
                        DateOnly? reviewDate = random.Below(4) switch
                        {
                            1 => day.AddDays(-random.Below(5)),
                            2 => day.AddDays(1),
                            _ => null,
                        };
                        var label = reviewDate is { } date ? RustJson.Day(date) : "-";
                        var result = t._tracking.ValidateReview(now, reviewDate, rules);
                        t.Record($"valider {label}", result.IsSuccess ? result.Tracking : null, result.Error, Detail(result.Outcome));
                        break;
                    }
                case 3:
                    {
                        var to = day.AddDays(random.Below(20) - 2);
                        t.Record($"reporter {RustJson.Day(to)}", t._tracking.Postpone(now, to));
                        break;
                    }
                case 4:
                    {
                        var index = random.Below(4);
                        t.Record(Invariant($"recommencer {index}"), t._tracking.Restart(now, ReferenceCases.Sequence(index)));
                        break;
                    }
                case 5:
                    t.Record("archiver", t._tracking.ArchiveManually(now, rules));
                    break;
                case 6:
                    t.Record("tirer", t._tracking.MarkDrawn(day));
                    break;
                case 7:
                    {
                        var (answer, label) = random.Below(3) switch
                        {
                            0 => (ReminderAnswer.Known, "connue"),
                            1 => Some(random.Below(3)),
                            _ => (ReminderAnswer.ReviewEntirely, "entierement"),
                        };
                        var index = random.Below(4);
                        var result = t._tracking.HandleReminder(now, answer, sheetRules, ReferenceCases.Sequence(index));
                        t.Record(Invariant($"rappel {label} {index}"), result);
                        break;
                    }
                case 8:
                    {
                        var include = random.Below(2) == 0;
                        t.Record($"inclure {Bool(include)}", t._tracking.IncludeInReminders(now, include));
                        break;
                    }
                case 9:
                    {
                        var index = random.Below(3);
                        var changed = t._tracking.RecordModification(now, ReferenceCases.ModifiedObjects[index]);
                        t.Record(Invariant($"modifier {index}"), changed, error: null, detail: string.Empty);
                        break;
                    }
                case 10:
                    {
                        var eligible = t._tracking.IsEligibleOn(day);
                        t._codes.Append(eligible ? 'T' : 'F');
                        t.Line($"eligible {Bool(eligible)}");
                        break;
                    }
                default:
                    {
                        var until = day.AddDays(random.Below(60));
                        t._codes.Append('p');
                        t.Line(Projection(t._tracking.Cycle, day, until));
                        break;
                    }
            }
        }
        return t;
    }

    private static (ReminderAnswer Answer, string Label) Some(int markedPassages) =>
        (ReminderAnswer.SomePointsToReview(markedPassages), Invariant($"certains {markedPassages}"));

    private static string Projection(Cycle? cycle, DateOnly day, DateOnly until)
    {
        if (cycle is null)
        {
            return "projection aucune";
        }
        var text = new StringBuilder(Invariant($"projection {cycle.StepLabel} retard {cycle.DaysLate(day)} :"));
        foreach (var step in cycle.ProjectedSteps(day, until))
        {
            text.Append(Invariant($" {RustJson.Day(step.Date)}#{step.Step}"));
        }
        return text.ToString();
    }

    private static string Detail(ReviewOutcome? outcome) => outcome switch
    {
        NextStepScheduled next => $"suivante {next.StepLabel} {RustJson.Day(next.DueDate)}",
        CycleCompleted completed => $"terminee {RustJson.Day(completed.ArchivedOn)}",
        _ => string.Empty,
    };

    private static char Code(TrackingErrorCode code) => code switch
    {
        TrackingErrorCode.NotActive => 'a',
        TrackingErrorCode.NotArchived => 'b',
        TrackingErrorCode.AlreadyReviewed => 'c',
        TrackingErrorCode.FutureDate => 'd',
        TrackingErrorCode.BeforeLastReview => 'e',
        TrackingErrorCode.PostponeInPast => 'f',
        TrackingErrorCode.PostponeUnchanged => 'g',
        TrackingErrorCode.NoPendingReminder => 'h',
        TrackingErrorCode.ReminderAlreadyPending => 'i',
        TrackingErrorCode.NotYetEligible => 'j',
        TrackingErrorCode.ExcludedFromReminders => 'k',
        TrackingErrorCode.NoPassageMarked => 'l',
        TrackingErrorCode.InvalidRules => 'm',
        TrackingErrorCode.Inconsistent => 'n',
        TrackingErrorCode.DateOutOfRange => 'o',
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private void Line(string line) => _text.Append(line).Append('\n');

    private void Record(string operation, TrackingResult result) =>
        Record(operation, result.IsSuccess ? result.Tracking : null, result.Error, string.Empty);

    private void Record(string operation, Tracking? tracking, TrackingError? error, string detail)
    {
        if (tracking is not null)
        {
            _tracking = tracking;
            _codes.Append('.');
            var verification = tracking.Verify() is null ? "ok" : "KO";
            Line($"{operation} ok {detail} | {RustJson.Serialize(tracking)} | verifier {verification}");
        }
        else if (error is not null)
        {
            _codes.Append(Code(error.Code));
            Line($"{operation} erreur {error.Message}");
        }
        else
        {
            throw new InvalidOperationException("ni suivi ni erreur");
        }
    }
}
