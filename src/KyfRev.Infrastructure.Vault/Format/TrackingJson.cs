using System.Text.Json.Nodes;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Json;

namespace KyfRev.Infrastructure.Vault.Format;

/// <summary>
/// Suivi d'un élément révisable tel qu'il est aplati dans les fichiers du coffre (fiches et
/// annotations) : <c>statut</c>, <c>cycle</c> ou <c>archive</c>, <c>compteurs</c>,
/// <c>historique</c>. Relu, le suivi passe le contrôle d'intégrité du domaine.
/// </summary>
internal static class TrackingJson
{
    /// <summary>Clés réservées au suivi dans l'objet qui le contient.</summary>
    public static readonly string[] Keys = ["statut", "cycle", "archive", "compteurs", "historique"];

    private const long U32 = uint.MaxValue;

    public static Tracking Read(JsonObject obj)
    {
        ItemState state = Fields.String(obj, "statut") switch
        {
            "active" => new ActiveState(ReadCycle(Fields.Object(obj, "cycle"))),
            "archivee" => new ArchivedState(ReadArchive(Fields.Object(obj, "archive"))),
            var other => throw new VaultFormatException($"statut « {other} » inconnu"),
        };
        var counters = Fields.Object(obj, "compteurs");
        var history = Fields.Array(obj, "historique")
            .Select(item => item as JsonObject ?? throw new VaultFormatException("un événement de l'historique doit être un objet"))
            .Select(ReadEvent);
        var restored = Tracking.Restore(
            state,
            new CycleCounters(
                (int)Fields.Integer(counters, "cyclesCommences", 0, int.MaxValue),
                (int)Fields.Integer(counters, "cyclesTermines", 0, int.MaxValue),
                (int)Fields.Integer(counters, "cyclesInterrompus", 0, int.MaxValue)),
            history);
        return restored.IsSuccess ? restored.Tracking : throw new VaultFormatException(restored.Error.Message);
    }

    public static void Write(Tracking tracking, JsonObject target)
    {
        switch (tracking.State)
        {
            case ActiveState { Cycle: var cycle }:
                target["statut"] = "active";
                target["cycle"] = new JsonObject
                {
                    ["numero"] = cycle.Number,
                    ["sequence"] = new JsonArray([.. cycle.Sequence.Values.Select(value => (JsonNode?)JsonValue.Create(value))]),
                    ["demarreLe"] = Fields.DayText(cycle.StartedOn),
                    ["etape"] = cycle.Step,
                    ["echeance"] = Fields.DayText(cycle.DueDate),
                    ["derniereRevision"] = Fields.DayText(cycle.LastReview),
                };
                // Absent de la version 0.x : écrit seulement pour un élément suspendu.
                if (cycle.SuspendedOn is { } suspendedOn)
                {
                    target["cycle"]!["suspendueLe"] = Fields.DayText(suspendedOn);
                }
                // Absent lui aussi de la version 0.x : écrit seulement pour une note.
                if (cycle.IsNote)
                {
                    target["cycle"]!["horsRevision"] = true;
                }
                break;
            case ArchivedState { Archive: var archive }:
                target["statut"] = "archivee";
                target["archive"] = new JsonObject
                {
                    ["archiveeLe"] = Fields.DayText(archive.ArchivedOn),
                    ["raison"] = Reason(archive.Reason),
                    ["inclureDansRappels"] = archive.IncludedInReminders,
                    ["niveauRepos"] = archive.RestLevel,
                    ["eligibleLe"] = Fields.DayText(archive.EligibleOn),
                    ["rappelTireLe"] = archive.ReminderDrawnOn is { } drawnOn ? Fields.DayText(drawnOn) : null,
                };
                break;
            default:
                throw new InvalidOperationException("État de suivi inconnu.");
        }
        target["compteurs"] = new JsonObject
        {
            ["cyclesCommences"] = tracking.Counters.Started,
            ["cyclesTermines"] = tracking.Counters.Completed,
            ["cyclesInterrompus"] = tracking.Counters.Interrupted,
        };
        target["historique"] = new JsonArray([.. tracking.History.Select(e => (JsonNode?)WriteEvent(e))]);
    }

    private static Cycle ReadCycle(JsonObject obj)
    {
        var values = Fields.Array(obj, "sequence").Select(item => (int)Fields.IntegerValue(item ?? throw new VaultFormatException("« sequence » contient null"), "sequence", 0, ushort.MaxValue));
        var sequence = ReviewSequence.Create(values);
        if (!sequence.IsSuccess)
        {
            throw new VaultFormatException(sequence.Error.Message);
        }
        return new Cycle(
            (int)Fields.Integer(obj, "numero", 0, U32),
            sequence.Sequence,
            Fields.Day(obj, "demarreLe"),
            (int)Fields.Integer(obj, "etape", 0, int.MaxValue),
            Fields.Day(obj, "echeance"),
            Fields.Day(obj, "derniereRevision"),
            Fields.OptionalDay(obj, "suspendueLe"),
            // Une note est toujours suspendue : sans ce jour, la marque seule ne la sort pas des révisions.
            Fields.OptionalBoolean(obj, "horsRevision", fallback: false) && Fields.OptionalDay(obj, "suspendueLe") is not null);
    }

    private static ArchiveInfo ReadArchive(JsonObject obj) => new(
        Fields.Day(obj, "archiveeLe"),
        ReadReason(Fields.String(obj, "raison")),
        Fields.Boolean(obj, "inclureDansRappels"),
        (int)Fields.Integer(obj, "niveauRepos", 0, int.MaxValue),
        Fields.Day(obj, "eligibleLe"),
        Fields.OptionalDay(obj, "rappelTireLe"));

    private static TrackingEvent ReadEvent(JsonObject obj)
    {
        var at = Fields.Instant(obj, "le");
        return Fields.String(obj, "evt") switch
        {
            "revision" => new ReviewEvent(
                at,
                Fields.Day(obj, "jour"),
                (int)Fields.Integer(obj, "cycle", 0, U32),
                Fields.String(obj, "etape"),
                Fields.Day(obj, "prevuLe"),
                (int)Fields.Integer(obj, "retardJours", int.MinValue, int.MaxValue),
                Fields.OptionalString(obj, "origine") is { } origin ? ReadOrigin(origin) : null,
                Fields.OptionalString(obj, "souvenir") is { } recall && ReadForgotten(recall)),
            "report" => new PostponementEvent(
                at,
                (int)Fields.Integer(obj, "cycle", 0, U32),
                Fields.String(obj, "etape"),
                Fields.Day(obj, "de"),
                Fields.Day(obj, "vers")),
            "interruption" => new InterruptionEvent(at, Fields.Day(obj, "jour"), (int)Fields.Integer(obj, "cycle", 0, U32)),
            "archivage" => new ArchivingEvent(at, Fields.Day(obj, "jour"), ReadReason(Fields.String(obj, "raison"))),
            "reactivation" => new ReactivationEvent(at, Fields.Day(obj, "jour")),
            "rappel" => new ReminderEvent(at, Fields.Day(obj, "jour"), Fields.Day(obj, "tireLe"), ReadOutcome(Fields.String(obj, "resultat"))),
            "inclusion_rappels" => new ReminderInclusionEvent(at, Fields.Boolean(obj, "inclure")),
            "modification" => new ModificationEvent(at, ReadChanged(Fields.String(obj, "objet"))),
            var other => throw new VaultFormatException($"événement « {other} » inconnu"),
        };
    }

    private static JsonObject WriteEvent(TrackingEvent trackingEvent)
    {
        var at = InstantText.Format(trackingEvent.At);
        return trackingEvent switch
        {
            ReviewEvent e => WithRecall(WithOrigin(
                new JsonObject
                {
                    ["evt"] = "revision",
                    ["le"] = at,
                    ["jour"] = Fields.DayText(e.Day),
                    ["cycle"] = e.CycleNumber,
                    ["etape"] = e.StepLabel,
                    ["prevuLe"] = Fields.DayText(e.PlannedOn),
                    ["retardJours"] = e.DaysLate,
                },
                e.Origin), e.Forgotten),
            PostponementEvent e => new JsonObject
            {
                ["evt"] = "report",
                ["le"] = at,
                ["cycle"] = e.CycleNumber,
                ["etape"] = e.StepLabel,
                ["de"] = Fields.DayText(e.From),
                ["vers"] = Fields.DayText(e.To),
            },
            InterruptionEvent e => new JsonObject { ["evt"] = "interruption", ["le"] = at, ["jour"] = Fields.DayText(e.Day), ["cycle"] = e.CycleNumber },
            ArchivingEvent e => new JsonObject { ["evt"] = "archivage", ["le"] = at, ["jour"] = Fields.DayText(e.Day), ["raison"] = Reason(e.Reason) },
            ReactivationEvent e => new JsonObject { ["evt"] = "reactivation", ["le"] = at, ["jour"] = Fields.DayText(e.Day) },
            ReminderEvent e => new JsonObject
            {
                ["evt"] = "rappel",
                ["le"] = at,
                ["jour"] = Fields.DayText(e.Day),
                ["tireLe"] = Fields.DayText(e.DrawnOn),
                ["resultat"] = Outcome(e.Outcome),
            },
            ReminderInclusionEvent e => new JsonObject { ["evt"] = "inclusion_rappels", ["le"] = at, ["inclure"] = e.Include },
            ModificationEvent e => new JsonObject { ["evt"] = "modification", ["le"] = at, ["objet"] = Changed(e.Changed) },
            _ => throw new InvalidOperationException("Événement inconnu."),
        };
    }

    private static JsonObject WithOrigin(JsonObject review, StartOrigin? origin)
    {
        if (origin is { } value)
        {
            review["origine"] = Origin(value);
        }
        return review;
    }

    /// <summary>
    /// « souvenir » n'est écrit que pour un oubli : une révision validée s'écrit comme avant, octet
    /// pour octet, et la 0.x, qui ignore cette clé, relit le fichier.
    /// </summary>
    private static JsonObject WithRecall(JsonObject review, bool forgotten)
    {
        if (forgotten)
        {
            review["souvenir"] = "oublie";
        }
        return review;
    }

    private static bool ReadForgotten(string text) => text switch
    {
        "oublie" => true,
        _ => throw new VaultFormatException($"souvenir « {text} » inconnu"),
    };

    private static string Origin(StartOrigin origin) => origin switch
    {
        StartOrigin.Creation => "creation",
        StartOrigin.Adoption => "adoption",
        StartOrigin.Reminder => "rappel",
        StartOrigin.Restart => "redemarrage",
        _ => throw new ArgumentOutOfRangeException(nameof(origin)),
    };

    private static StartOrigin ReadOrigin(string text) => text switch
    {
        "creation" => StartOrigin.Creation,
        "adoption" => StartOrigin.Adoption,
        "rappel" => StartOrigin.Reminder,
        "redemarrage" => StartOrigin.Restart,
        _ => throw new VaultFormatException($"origine « {text} » inconnue"),
    };

    private static string Reason(ArchiveReason reason) => reason switch
    {
        ArchiveReason.EndOfCycle => "fin_de_cycle",
        ArchiveReason.Manual => "manuelle",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    private static ArchiveReason ReadReason(string text) => text switch
    {
        "fin_de_cycle" => ArchiveReason.EndOfCycle,
        "manuelle" => ArchiveReason.Manual,
        _ => throw new VaultFormatException($"raison d'archivage « {text} » inconnue"),
    };

    private static string Outcome(ReminderOutcome outcome) => outcome switch
    {
        ReminderOutcome.Known => "connue",
        ReminderOutcome.SomePointsToReview => "certains_points_a_revoir",
        ReminderOutcome.ReviewEntirely => "a_revoir_entierement",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    private static ReminderOutcome ReadOutcome(string text) => text switch
    {
        "connue" => ReminderOutcome.Known,
        "certains_points_a_revoir" => ReminderOutcome.SomePointsToReview,
        "a_revoir_entierement" => ReminderOutcome.ReviewEntirely,
        _ => throw new VaultFormatException($"résultat de rappel « {text} » inconnu"),
    };

    private static string Changed(ModifiedObject changed) => changed switch
    {
        ModifiedObject.Comment => "commentaire",
        ModifiedObject.Passage => "passage",
        ModifiedObject.Definition => "definition",
        _ => throw new ArgumentOutOfRangeException(nameof(changed)),
    };

    private static ModifiedObject ReadChanged(string text) => text switch
    {
        "commentaire" => ModifiedObject.Comment,
        "passage" => ModifiedObject.Passage,
        "definition" => ModifiedObject.Definition,
        _ => throw new VaultFormatException($"objet modifié « {text} » inconnu"),
    };
}
