using System.Globalization;
using System.Text.Json;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Json;

namespace KyfRev.Infrastructure.Vault.Tests.Scenarios;

/// <summary>
/// Opérations des scénarios sur la lecture : la fiche ouverte et l'état de révision, passés par
/// le port des cas d'usage (<see cref="VaultAdapter"/>), comme <c>scenarios/lecture.rs</c>.
/// </summary>
internal sealed partial class ScenarioRunner
{
    private VaultAdapter Port => new(Vault);

    private string ExecuteReading(string op, JsonElement operation, Moment now)
    {
        switch (op)
        {
            case "ouvrir_fiche":
                return Describe(Port.OpenSheet(Sheet(operation), now.Day));
            case "etat_de":
                {
                    var passage = Raw(operation, "genre") == "passage";
                    var id = Ref(operation, passage ? "annotation" : "fiche");
                    return Describe(Port.ReviewStateOf(passage ? ItemKind.Passage : ItemKind.Sheet, id, now.Day));
                }
            default:
                throw new InvalidOperationException($"Opération inconnue : {op}");
        }
    }

    private static string Describe(ReviewState e) =>
        $"{(e.Status == ReviewStatus.Active ? "Active" : "Archivee")} [{string.Join(',', e.Steps.Select(s => $"{s.Label}:{DayOrDash(s.DoneOn)}:{s.DaysLate}"))}]"
        + $" {e.Step ?? "-"} {DayOrDash(e.DueDate)} {(e.Cycle is { } cycle ? cycle.ToString(CultureInfo.InvariantCulture) : "-")} {DayOrDash(e.LastReview)}"
        + $" {e.DaysLate} {Bool(e.ReviewedToday)} {DayOrDash(e.ArchivedOn)} {Bool(e.ReminderPending)} {Bool(e.IncludedInReminders)} {DayOrDash(e.EligibleOn)}"
        + $" {Bool(e.StartedToday)} {e.CyclesStarted} {e.CyclesCompleted}";

    private static string Describe(OpenedSheet f) =>
        $"{f.Id} {Json(f.Title)} {f.Path} {Json(f.Folder)} {List(f.Tags)} {Json(f.Body)} {f.FirstBodyLine} {f.Fingerprint} {DayOrDash(f.CreatedOn)}"
        + $" {InstantText.Format(f.ModifiedAt)} <{Describe(f.Review)}> [{string.Join(';', f.Annotations.Select(a =>
            $"{a.Id} {Optional(a.Comment)} {Optional(a.Definition?.Term)} {State(a.AnchorState)} {Bool(a.InTrash)} <{Describe(a.Review)}>"))}]";

    private static string DayOrDash(DateOnly? day) => day is { } d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "-";
}
