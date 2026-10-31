using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using KyfRev.Domain.Reviews;

namespace KyfRev.Domain.Tests.Reference;

/// <summary>
/// Écrit un suivi exactement comme <c>serde_json::to_string</c> l'écrit côté Rust (JSON compact,
/// mêmes clés, même ordre, mêmes formats de dates). Sert uniquement à comparer les deux
/// implémentations ; l'écriture du coffre relève de la couche de persistance.
/// </summary>
internal static class RustJson
{
    private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Serialize(Tracking tracking)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, Options))
        {
            w.WriteStartObject();
            switch (tracking.State)
            {
                case ActiveState { Cycle: var cycle }:
                    w.WriteString("statut", "active");
                    w.WriteStartObject("cycle");
                    w.WriteNumber("numero", cycle.Number);
                    w.WriteStartArray("sequence");
                    foreach (var value in cycle.Sequence.Values)
                    {
                        w.WriteNumberValue(value);
                    }
                    w.WriteEndArray();
                    w.WriteString("demarreLe", Day(cycle.StartedOn));
                    w.WriteNumber("etape", cycle.Step);
                    w.WriteString("echeance", Day(cycle.DueDate));
                    w.WriteString("derniereRevision", Day(cycle.LastReview));
                    w.WriteEndObject();
                    break;
                case ArchivedState { Archive: var archive }:
                    w.WriteString("statut", "archivee");
                    w.WriteStartObject("archive");
                    w.WriteString("archiveeLe", Day(archive.ArchivedOn));
                    w.WriteString("raison", Reason(archive.Reason));
                    w.WriteBoolean("inclureDansRappels", archive.IncludedInReminders);
                    w.WriteNumber("niveauRepos", archive.RestLevel);
                    w.WriteString("eligibleLe", Day(archive.EligibleOn));
                    if (archive.ReminderDrawnOn is { } drawnOn)
                    {
                        w.WriteString("rappelTireLe", Day(drawnOn));
                    }
                    else
                    {
                        w.WriteNull("rappelTireLe");
                    }
                    w.WriteEndObject();
                    break;
                default:
                    throw new InvalidOperationException("état inconnu");
            }

            w.WriteStartObject("compteurs");
            w.WriteNumber("cyclesCommences", tracking.Counters.Started);
            w.WriteNumber("cyclesTermines", tracking.Counters.Completed);
            w.WriteNumber("cyclesInterrompus", tracking.Counters.Interrupted);
            w.WriteEndObject();

            w.WriteStartArray("historique");
            foreach (var trackingEvent in tracking.History)
            {
                WriteEvent(w, trackingEvent);
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Format de <c>jiff::Timestamp</c> : UTC, fraction de seconde sans zéros finaux.</summary>
    public static string Instant(DateTimeOffset instant)
    {
        var utc = instant.UtcDateTime;
        var text = utc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        var fraction = utc.Ticks % TimeSpan.TicksPerSecond;
        if (fraction != 0)
        {
            text += "." + fraction.ToString("0000000", CultureInfo.InvariantCulture).TrimEnd('0');
        }
        return text + "Z";
    }

    private static void WriteEvent(Utf8JsonWriter w, TrackingEvent trackingEvent)
    {
        w.WriteStartObject();
        switch (trackingEvent)
        {
            case ReviewEvent e:
                w.WriteString("evt", "revision");
                w.WriteString("le", Instant(e.At));
                w.WriteString("jour", Day(e.Day));
                w.WriteNumber("cycle", e.CycleNumber);
                w.WriteString("etape", e.StepLabel);
                w.WriteString("prevuLe", Day(e.PlannedOn));
                w.WriteNumber("retardJours", e.DaysLate);
                if (e.Origin is { } origin)
                {
                    w.WriteString("origine", Origin(origin));
                }
                break;
            case PostponementEvent e:
                w.WriteString("evt", "report");
                w.WriteString("le", Instant(e.At));
                w.WriteNumber("cycle", e.CycleNumber);
                w.WriteString("etape", e.StepLabel);
                w.WriteString("de", Day(e.From));
                w.WriteString("vers", Day(e.To));
                break;
            case InterruptionEvent e:
                w.WriteString("evt", "interruption");
                w.WriteString("le", Instant(e.At));
                w.WriteString("jour", Day(e.Day));
                w.WriteNumber("cycle", e.CycleNumber);
                break;
            case ArchivingEvent e:
                w.WriteString("evt", "archivage");
                w.WriteString("le", Instant(e.At));
                w.WriteString("jour", Day(e.Day));
                w.WriteString("raison", Reason(e.Reason));
                break;
            case ReactivationEvent e:
                w.WriteString("evt", "reactivation");
                w.WriteString("le", Instant(e.At));
                w.WriteString("jour", Day(e.Day));
                break;
            case ReminderEvent e:
                w.WriteString("evt", "rappel");
                w.WriteString("le", Instant(e.At));
                w.WriteString("jour", Day(e.Day));
                w.WriteString("tireLe", Day(e.DrawnOn));
                w.WriteString("resultat", Outcome(e.Outcome));
                break;
            case ReminderInclusionEvent e:
                w.WriteString("evt", "inclusion_rappels");
                w.WriteString("le", Instant(e.At));
                w.WriteBoolean("inclure", e.Include);
                break;
            case ModificationEvent e:
                w.WriteString("evt", "modification");
                w.WriteString("le", Instant(e.At));
                w.WriteString("objet", Changed(e.Changed));
                break;
            default:
                throw new InvalidOperationException("événement inconnu");
        }
        w.WriteEndObject();
    }

    private static string Origin(StartOrigin origin) => origin switch
    {
        StartOrigin.Creation => "creation",
        StartOrigin.Adoption => "adoption",
        StartOrigin.Reminder => "rappel",
        StartOrigin.Restart => "redemarrage",
        _ => throw new ArgumentOutOfRangeException(nameof(origin)),
    };

    private static string Reason(ArchiveReason reason) => reason switch
    {
        ArchiveReason.EndOfCycle => "fin_de_cycle",
        ArchiveReason.Manual => "manuelle",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    private static string Outcome(ReminderOutcome outcome) => outcome switch
    {
        ReminderOutcome.Known => "connue",
        ReminderOutcome.SomePointsToReview => "certains_points_a_revoir",
        ReminderOutcome.ReviewEntirely => "a_revoir_entierement",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    private static string Changed(ModifiedObject changed) => changed switch
    {
        ModifiedObject.Comment => "commentaire",
        ModifiedObject.Passage => "passage",
        ModifiedObject.Definition => "definition",
        _ => throw new ArgumentOutOfRangeException(nameof(changed)),
    };
}
