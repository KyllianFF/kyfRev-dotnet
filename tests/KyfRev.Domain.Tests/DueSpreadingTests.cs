using KyfRev.Domain.Reviews;
using static KyfRev.Domain.Tests.Support.Dates;

namespace KyfRev.Domain.Tests;

/// <summary>L'étalement des échéances : un retard à rattraper, des jours de pause à libérer.</summary>
[TestClass]
public sealed class DueSpreadingTests
{
    private static readonly DateOnly Today = D(2026, 11, 10);

    private static readonly int[] Intervals = [1, 3, 7, 14, 30];

    private static string Text(SpreadResult result) =>
        string.Join(' ', result.Moves.Select(move => $"{move.Key}>{move.To.Day}"));

    [TestMethod]
    public void Catching_up_keeps_the_most_fragile_today_and_fills_the_next_days()
    {
        SpreadCandidate[] active =
        [
            new("j30", Today.AddDays(-5), 30), // retard de 5 jours sur 30 : le moins pressé
            new("j1", Today.AddDays(-3), 1), // 3 jours sur 1 : le plus fragile
            new("j3", Today.AddDays(-3), 3),
            new("j7", Today.AddDays(-7), 7),
            new("jour", Today, 14), // dû aujourd'hui, sans retard
            new("demain", Today.AddDays(1), 3), // tombe déjà demain : il y prend une place
            new("loin", Today.AddDays(20), 30),
        ];

        var result = DueSpreading.CatchUp(active, Today, perDay: 2);

        // j1 et j3 (à égalité avec j7, mais l'étape la plus courte d'abord) restent aujourd'hui.
        Assert.AreEqual(2, result.Kept);
        Assert.AreEqual("j7>11 j30>12 jour>12", Text(result));
        Assert.AreEqual(D(2026, 11, 12), result.LastDay);
    }

    [TestMethod]
    public void Nothing_moves_when_the_day_is_under_the_cap()
    {
        var result = DueSpreading.CatchUp([new("a", Today, 1), new("b", Today.AddDays(-1), 3)], Today, perDay: 5);

        Assert.AreEqual(2, result.Kept);
        Assert.IsEmpty(result.Moves);
        Assert.IsNull(result.LastDay);
    }

    [TestMethod]
    public void A_pause_sends_its_days_after_the_return_in_due_order()
    {
        var from = Today.AddDays(2);
        var to = Today.AddDays(4);
        SpreadCandidate[] active =
        [
            new("avant", Today.AddDays(1), 1),
            new("b", Today.AddDays(3), 7),
            new("a", Today.AddDays(2), 3),
            new("c", Today.AddDays(4), 1),
            new("retour", Today.AddDays(5), 3),
            new("après", Today.AddDays(9), 3),
        ];

        var result = DueSpreading.Pause(active, from, to, perDay: 2);

        // Le jour du retour a déjà une échéance : il ne lui reste qu'une place.
        Assert.AreEqual(0, result.Kept);
        Assert.AreEqual("a>15 b>16 c>16", Text(result));
    }

    [TestMethod]
    public void A_cap_too_low_stops_at_the_horizon_instead_of_running_forever()
    {
        var first = Today.AddDays(1);
        // Chaque jour de l'année qui vient est déjà plein.
        var full = Enumerable.Range(0, DueSpreading.HorizonDays + 30).Select(day => new SpreadCandidate($"plein{day}", first.AddDays(day), 30));
        SpreadCandidate[] late = [new("x", Today.AddDays(-2), 1), new("y", Today.AddDays(-1), 1), new("z", Today, 1)];

        var result = DueSpreading.CatchUp([.. full, .. late], Today, perDay: 1);

        Assert.AreEqual(1, result.Kept);
        Assert.HasCount(2, result.Moves);
        Assert.IsTrue(result.Moves.All(move => move.To == first.AddDays(DueSpreading.HorizonDays)));
    }

    [TestMethod]
    public void The_result_does_not_depend_on_the_order_given()
    {
        var active = Enumerable.Range(0, 40).Select(rank => new SpreadCandidate($"e{rank:00}", Today.AddDays(-(rank % 9)), Intervals[rank % 5])).ToArray();

        var one = DueSpreading.CatchUp(active, Today, 4);
        var other = DueSpreading.CatchUp(active.Reverse(), Today, 4);

        Assert.AreEqual(Text(one), Text(other));
        Assert.HasCount(36, one.Moves);
        Assert.IsTrue(one.Moves.GroupBy(move => move.To).All(day => day.Count() <= 4));
        Assert.IsTrue(one.Moves.All(move => move.To > Today));
    }

    [TestMethod]
    public void Bad_arguments_are_refused()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DueSpreading.CatchUp([], Today, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DueSpreading.Pause([], Today.AddDays(3), Today.AddDays(2), 1));
    }
}
