using System.Globalization;
using KyfRev.Domain.Reviews;
using static KyfRev.Domain.Tests.Support.Dates;

namespace KyfRev.Domain.Tests;

/// <summary>Tirage quotidien des rappels de consolidation (section 7.5 de la conception).</summary>
[TestClass]
public sealed class ReminderDrawTests
{
    private static readonly ReminderRules Rules = ReminderRules.Default;

    [TestMethod]
    public void A_sheet_is_never_drawn_before_it_is_eligible()
    {
        DrawCandidate[] candidates = [new("f1", D(2027, 2, 3))];
        Assert.IsEmpty(Draw(D(2027, 2, 2), candidates, 0, Rules, 42).Selection);
        Assert.AreEqual("f1", Assert.ContainsSingle(Draw(D(2027, 2, 3), candidates, 0, Rules, 42).Selection));
    }

    [TestMethod]
    [DataRow(0, 2)]
    [DataRow(1, 1)]
    [DataRow(2, 0)]
    [DataRow(5, 0)]
    public void Places_are_limited_by_pending_reminders(int pending, int drawn)
    {
        var candidates = Enumerable.Range(0, 10).Select(i => new DrawCandidate(Id(i), D(2027, 1, 1))).ToArray();
        Assert.HasCount(drawn, Draw(D(2027, 1, 10), candidates, pending, Rules, 1).Selection);
    }

    [TestMethod]
    public void Oldest_waiting_sheets_go_first_and_saturation_is_reported()
    {
        DrawCandidate[] candidates =
        [
            new("recente", D(2027, 2, 25)),
            new("vieille-2", D(2027, 1, 20)),
            new("vieille-1", D(2027, 1, 5)),
            new("vieille-3", D(2027, 1, 30)),
        ];
        // Trois fiches attendent depuis au moins 30 jours, mais deux places seulement.
        var draw = Draw(D(2027, 3, 1), candidates, 0, Rules, 7);
        string[] expected = ["vieille-1", "vieille-2"];
        Assert.AreSequenceEqual(expected, draw.Selection);
        Assert.AreEqual(1, draw.UnservedPriority);
    }

    [TestMethod]
    public void A_sheet_becomes_a_priority_after_exactly_the_maximum_wait()
    {
        var rules = Rules with { MaxPerDay = 1 };
        DrawCandidate[] candidates = [new("recente", D(2027, 1, 31)), new("ancienne", D(2027, 1, 30))];
        // Le 1er mars, « ancienne » attend depuis 30 jours pile : elle passe d'abord.
        var draw = Draw(D(2027, 3, 1), candidates, 0, rules, 5);
        Assert.AreEqual("ancienne", Assert.ContainsSingle(draw.Selection));
        Assert.AreEqual(0, draw.UnservedPriority);
    }

    [TestMethod]
    public void Priority_ties_are_broken_by_identifier()
    {
        DrawCandidate[] candidates = [new("b", D(2027, 1, 1)), new("a", D(2027, 1, 1)), new("c", D(2027, 1, 1))];
        string[] expected = ["a", "b"];
        Assert.AreSequenceEqual(expected, Draw(D(2027, 3, 1), candidates, 0, Rules, 7).Selection);
    }

    [TestMethod]
    public void Priority_sheets_come_before_the_random_draw()
    {
        var rules = Rules with { MaxPerDay = 3 };
        DrawCandidate[] candidates = [new("p", D(2027, 1, 1)), new("x", D(2027, 2, 20)), new("y", D(2027, 2, 21)), new("z", D(2027, 2, 22))];
        var draw = Draw(D(2027, 3, 1), candidates, 0, rules, 11);
        Assert.HasCount(3, draw.Selection);
        Assert.AreEqual("p", draw.Selection[0]);
        Assert.AreEqual(0, draw.UnservedPriority);
    }

    [TestMethod]
    [DataRow("2027-02-20", "2027-01-01")]
    [DataRow("2027-01-01", "2027-02-20")]
    public void A_duplicate_identifier_keeps_its_oldest_eligibility(string first, string second)
    {
        DrawCandidate[] candidates = [new("f", Iso(first)), new("f", Iso(second)), new("g", D(2027, 1, 15))];
        var draw = Draw(D(2027, 3, 1), candidates, 0, Rules with { MaxPerDay = 1 }, 3);
        Assert.AreEqual("f", Assert.ContainsSingle(draw.Selection));
        Assert.AreEqual(1, draw.UnservedPriority);
    }

    [TestMethod]
    public void The_draw_is_reproducible_and_independent_of_candidate_order()
    {
        var candidates = Enumerable.Range(0, 20).Select(i => new DrawCandidate(Id(i), D(2027, 1, 1))).ToList();
        var first = Draw(D(2027, 1, 15), candidates, 0, Rules, 99);
        candidates.Reverse();
        var second = Draw(D(2027, 1, 15), candidates, 0, Rules, 99);
        Assert.AreSequenceEqual(first.Selection, second.Selection);
        Assert.AreEqual(first.UnservedPriority, second.UnservedPriority);
        Assert.AreNotEqual(string.Join(",", first.Selection), string.Join(",", Draw(D(2027, 1, 15), candidates, 0, Rules, 100).Selection));
    }

    [TestMethod]
    public void The_random_draw_is_uniform()
    {
        var rules = Rules with { MaxPerDay = 1 };
        var candidates = Enumerable.Range(0, 10).Select(i => new DrawCandidate(Id(i), D(2027, 1, 1))).ToArray();
        var counts = new Dictionary<string, int>();
        for (var seed = 0UL; seed < 10_000; seed++)
        {
            var id = Assert.ContainsSingle(Draw(D(2027, 1, 10), candidates, 0, rules, seed).Selection);
            counts[id] = counts.GetValueOrDefault(id) + 1;
        }
        Assert.HasCount(10, counts);
        foreach (var count in counts.Values)
        {
            Assert.IsInRange(850, 1150, count);
        }
    }

    [TestMethod]
    public void Invalid_rules_are_refused()
    {
        var result = ReminderDraw.Draw(D(2027, 1, 1), [], 0, Rules with { MaxWaitDays = 0 }, 1);
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Draw);
        Assert.AreEqual(TrackingErrorCode.InvalidRules, result.Error.Code);
    }

    [TestMethod]
    public void A_negative_number_of_pending_reminders_is_a_programming_error() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ReminderDraw.Draw(D(2027, 1, 1), [], -1, Rules, 1));

    private static string Id(int i) => string.Create(CultureInfo.InvariantCulture, $"f{i:00}");

    private static DailyDraw Draw(DateOnly today, IEnumerable<DrawCandidate> candidates, int pending, ReminderRules rules, ulong seed) =>
        ReminderDraw.Draw(today, candidates, pending, rules, seed).Ok();
}
