using CsCheck;
using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;
using KyfRev.Domain.Tests.Reference;
using static KyfRev.Domain.Tests.Support.Dates;

namespace KyfRev.Domain.Tests;

/// <summary>Propriétés vérifiées sur des suites d'opérations et des tirages générés au hasard.</summary>
[TestClass]
public sealed class PropertyTests(TestContext testContext)
{
    private enum Operation
    {
        Validate,
        Forget,
        ValidateBackdated,
        Postpone,
        Restart,
        Archive,
        Draw,
        Reminder,
        Include,
    }

    private static readonly Gen<(Operation Kind, int Argument)> OperationGen = Gen.Frequency(
        (6, Gen.Const((Operation.Validate, 0))),
        (2, Gen.Const((Operation.Forget, 0))),
        (1, Gen.Int[1, 4].Select(n => (Operation.ValidateBackdated, n))),
        (1, Gen.Int[1, 19].Select(n => (Operation.Postpone, n))),
        (1, Gen.Const((Operation.Restart, 0))),
        (1, Gen.Const((Operation.Archive, 0))),
        (2, Gen.Const((Operation.Draw, 0))),
        (2, Gen.Int[0, 2].Select(n => (Operation.Reminder, n))),
        (1, Gen.Int[0, 1].Select(n => (Operation.Include, n))));

    /// <summary>
    /// Quelle que soit la suite d'opérations : une opération ne modifie jamais le suivi d'origine ;
    /// l'historique ne fait que grandir ; le suivi reste cohérent ; après une validation, l'étape
    /// suivante tombe exactement n jours plus tard pour une étape Jn (D7) ; un rappel traité libère
    /// toujours la fiche pour un prochain tirage.
    /// </summary>
    [TestMethod]
    public void Any_sequence_of_operations_keeps_the_tracking_invariants()
    {
        var rules = ReminderRules.Default;
        Gen.Select(OperationGen, Gen.Int[0, 89]).Array[1, 79].Sample(steps =>
        {
            var day = D(2026, 10, 9);
            var tracking = SheetCreatedOn(day);
            foreach (var ((kind, argument), advance) in steps)
            {
                day = day.AddDays(advance);
                var now = At(day);
                var before = tracking;
                var snapshot = RustJson.Serialize(before);

                var (next, outcome) = kind switch
                {
                    Operation.Validate => Review(before.ValidateReview(now, null, rules)),
                    Operation.Forget => Review(before.ValidateReview(now, null, rules, ReviewRecall.Forgotten)),
                    Operation.ValidateBackdated => Review(before.ValidateReview(now, day.AddDays(-argument), rules)),
                    Operation.Postpone => (before.Postpone(now, day.AddDays(argument)).Tracking, null),
                    Operation.Restart => (before.Restart(now, ReviewSequence.Default).Tracking, null),
                    Operation.Archive => (before.ArchiveManually(now, rules).Tracking, null),
                    Operation.Draw => (before.MarkDrawn(day).Tracking, null),
                    Operation.Reminder => (before.HandleReminder(now, Answer(argument), rules, ReviewSequence.Default).Tracking, null),
                    _ => (before.IncludeInReminders(now, argument == 1).Tracking, null),
                };

                Assert.AreEqual(snapshot, RustJson.Serialize(before), "une opération a modifié le suivi d'origine");
                if (next is null)
                {
                    continue;
                }
                tracking = next;
                Assert.AreSequenceEqual(before.History, tracking.History.Take(before.History.Length), "historique réécrit");
                Assert.IsNull(tracking.Verify());
                if (tracking.Cycle is { } cycle)
                {
                    Assert.IsGreaterThan(cycle.LastReview, cycle.DueDate);
                }
                if (tracking.Archive is { } archive)
                {
                    Assert.IsInRange(0, rules.RestSequenceMonths.Length - 1, archive.RestLevel);
                }
                if (outcome is NextStepScheduled scheduled && kind == Operation.Validate)
                {
                    Assert.AreEqual(tracking.Cycle!.Sequence.IntervalBefore(tracking.Cycle.Step), CivilCalendar.DaysBetween(day, scheduled.DueDate));
                }
                if (kind == Operation.Forget)
                {
                    // Un oubli ne termine jamais un cycle, ne fait jamais avancer, et ne descend pas sous J1.
                    Assert.IsInstanceOfType<NextStepScheduled>(outcome);
                    Assert.AreEqual(Math.Max(before.Cycle!.Step - 1, 1), tracking.Cycle!.Step);
                    Assert.AreEqual(tracking.Cycle.Sequence.IntervalBefore(tracking.Cycle.Step), CivilCalendar.DaysBetween(day, tracking.Cycle.DueDate));
                    Assert.AreEqual(before.Counters, tracking.Counters);
                }
                if (kind == Operation.Reminder)
                {
                    Assert.IsNull(tracking.Archive?.ReminderDrawnOn);
                    Assert.IsGreaterThan(before.History.Length, tracking.History.Length);
                }
            }
        }, writeLine: testContext.WriteLine);
    }

    /// <summary>
    /// Le tirage ne dépasse jamais les places, ne tire que des fiches éligibles, sans doublon, et
    /// ne dépend pas de l'ordre des candidats.
    /// </summary>
    [TestMethod]
    public void Draws_respect_places_eligibility_and_candidate_order()
    {
        var today = D(2027, 6, 1);
        var candidateGen = Gen.Select(Gen.Int[0, 199], Gen.String[Gen.Char["abcdef"], 1, 3])
            .Select((offset, id) => new DrawCandidate(id, today.AddDays(offset - 100)));

        Gen.Select(candidateGen.Array[0, 39], Gen.Int[0, 3], Gen.Int[0, 5], Gen.ULong).Sample((candidates, pending, maxPerDay, seed) =>
        {
            var rules = ReminderRules.Default with { MaxPerDay = maxPerDay };
            var draw = ReminderDraw.Draw(today, candidates, pending, rules, seed).Ok();

            var places = Math.Max(maxPerDay - pending, 0);
            var eligible = candidates.Where(c => c.EligibleOn <= today).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
            var selection = draw.Selection;
            Assert.HasCount(Math.Min(places, eligible.Count), selection);
            Assert.HasCount(selection.Length, selection.Distinct(StringComparer.Ordinal), "doublon tiré");
            Assert.IsTrue(eligible.IsSupersetOf(selection), "fiche non éligible tirée");

            var reversed = ReminderDraw.Draw(today, candidates.Reverse(), pending, rules, seed).Ok();
            Assert.AreSequenceEqual(selection, reversed.Selection);
        }, writeLine: testContext.WriteLine);
    }

    private static (Tracking? Next, ReviewOutcome? Outcome) Review(ReviewResult result) => (result.Tracking, result.Outcome);

    private static ReminderAnswer Answer(int choice) => choice switch
    {
        0 => ReminderAnswer.Known,
        1 => ReminderAnswer.SomePointsToReview(1),
        _ => ReminderAnswer.ReviewEntirely,
    };
}
