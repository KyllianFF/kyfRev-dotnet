using KyfRev.Domain.Calendar;
using KyfRev.Domain.Randomness;
using KyfRev.Domain.Reviews;
using static KyfRev.Domain.Tests.Support.Dates;

namespace KyfRev.Domain.Tests;

/// <summary>Séquence des J, calendrier civil, réglages des rappels et graine du jour.</summary>
[TestClass]
public sealed class BuildingBlockTests
{
    [TestMethod]
    public void Default_sequence()
    {
        var sequence = ReviewSequence.Default;
        Assert.AreEqual(5, sequence.LastStep);
        Assert.AreEqual("J0", sequence.Label(0));
        Assert.AreEqual("J7", sequence.Label(3));
        Assert.AreEqual("J30", sequence.Label(5));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => sequence.Label(6));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => sequence.Label(-1));
        Assert.AreEqual(1, sequence.IntervalBefore(1));
        Assert.AreEqual(7, sequence.IntervalBefore(3));
        Assert.AreEqual(30, sequence.IntervalBefore(5));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => sequence.IntervalBefore(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => sequence.IntervalBefore(6));
        Assert.AreEqual(55, sequence.TotalDurationDays);
        Assert.AreEqual("J0, J1, J3, J7, J14, J30", sequence.ToString());
    }

    [TestMethod]
    [DataRow(new[] { 0 }, SequenceErrorCode.TooShort, "La séquence doit compter au moins deux étapes.")]
    [DataRow(new[] { 1, 3 }, SequenceErrorCode.MustStartWithZero, "La séquence doit commencer par J0.")]
    [DataRow(new[] { 0, 3, 3 }, SequenceErrorCode.NotIncreasing, "Les étapes doivent être strictement croissantes (étape 2).")]
    [DataRow(new[] { 0, 5, 4 }, SequenceErrorCode.NotIncreasing, "Les étapes doivent être strictement croissantes (étape 2).")]
    [DataRow(new[] { 0, 4000 }, SequenceErrorCode.ValueTooLarge, "Une étape ne peut pas dépasser J3650.")]
    public void Invalid_sequences_are_refused(int[] values, SequenceErrorCode code, string message)
    {
        var creation = ReviewSequence.Create(values);
        Assert.IsFalse(creation.IsSuccess);
        Assert.IsNull(creation.Sequence);
        Assert.AreEqual(new SequenceError(code, message), creation.Error);
    }

    [TestMethod]
    public void The_longest_valid_steps_are_accepted()
    {
        var creation = ReviewSequence.Create([0, 1, ReviewSequence.MaxValue]);
        Assert.IsTrue(creation.IsSuccess);
        Assert.IsNull(creation.Error);
        Assert.AreEqual("J3650", creation.Sequence.Label(2));
    }

    [TestMethod]
    public void Sequences_are_compared_by_value()
    {
        var longer = ReviewSequence.Create([0, 1, 3, 7, 14, 30, 60]);
        var same = ReviewSequence.Create([0, 1, 3, 7, 14, 30]);
        Assert.IsTrue(longer.IsSuccess && same.IsSuccess);
        Assert.AreEqual(115, longer.Sequence.TotalDurationDays);
        Assert.AreEqual(ReviewSequence.Default, same.Sequence);
        Assert.AreEqual(ReviewSequence.Default.GetHashCode(), same.Sequence.GetHashCode());
        Assert.AreNotEqual(ReviewSequence.Default, longer.Sequence);
        Assert.AreNotEqual(ReviewSequence.Default.GetHashCode(), longer.Sequence.GetHashCode());
        Assert.IsFalse(ReviewSequence.Default.Equals((object?)null));
        Assert.IsFalse(ReviewSequence.Default.Equals((ReviewSequence?)null));
        Assert.IsTrue(ReviewSequence.Default.Equals((object)same.Sequence));
    }

    [TestMethod]
    [DataRow("2026-10-09", 2, "2026-12-09")]
    [DataRow("2026-12-03", 2, "2027-02-03")]
    [DataRow("2026-12-31", 2, "2027-02-28")]
    [DataRow("2027-08-31", 2, "2027-10-31")]
    [DataRow("2027-12-31", 2, "2028-02-29")]
    [DataRow("2027-03-31", -1, "2027-02-28")]
    [DataRow("2027-01-15", 0, "2027-01-15")]
    [DataRow("2027-01-15", 12, "2028-01-15")]
    public void Calendar_months_fall_back_to_the_last_day_of_the_month(string day, int months, string expected)
    {
        Assert.IsTrue(CivilCalendar.TryAddMonths(Iso(day), months, out var result));
        Assert.AreEqual(Iso(expected), result);
    }

    [TestMethod]
    public void Days_and_differences()
    {
        Assert.IsTrue(CivilCalendar.TryAddDays(D(2026, 10, 28), 16, out var later));
        Assert.AreEqual(D(2026, 11, 13), later);
        Assert.AreEqual(5, CivilCalendar.DaysBetween(D(2026, 10, 13), D(2026, 10, 18)));
        Assert.AreEqual(-2, CivilCalendar.DaysBetween(D(2026, 10, 20), D(2026, 10, 18)));
        Assert.AreEqual(62, CivilCalendar.DaysBetween(D(2026, 12, 3), D(2027, 2, 3)));
    }

    [TestMethod]
    public void Dates_outside_the_calendar_are_refused()
    {
        Assert.IsFalse(CivilCalendar.TryAddDays(DateOnly.MaxValue, 1, out _));
        Assert.IsFalse(CivilCalendar.TryAddDays(DateOnly.MinValue, -1, out _));
        Assert.IsTrue(CivilCalendar.TryAddDays(DateOnly.MaxValue, 0, out var last));
        Assert.AreEqual(DateOnly.MaxValue, last);
        Assert.IsTrue(CivilCalendar.TryAddDays(DateOnly.MinValue, 0, out var first));
        Assert.AreEqual(DateOnly.MinValue, first);
        Assert.IsFalse(CivilCalendar.TryAddMonths(D(9999, 12, 1), 1, out _));
        Assert.IsFalse(CivilCalendar.TryAddMonths(D(1, 1, 31), -1, out _));
        Assert.IsTrue(CivilCalendar.TryAddMonths(D(9999, 11, 30), 1, out var lastMonth));
        Assert.AreEqual(D(9999, 12, 30), lastMonth);
        Assert.IsTrue(CivilCalendar.TryAddMonths(D(1, 2, 28), -1, out var firstMonth));
        Assert.AreEqual(D(1, 1, 28), firstMonth);
    }

    [TestMethod]
    public void Operations_that_leave_the_calendar_are_refused()
    {
        var lastDay = DateOnly.MaxValue;
        var start = Tracking.Start(At(lastDay), ReviewSequence.Default, StartOrigin.Creation).Refused();
        Assert.AreEqual(TrackingErrorCode.DateOutOfRange, start.Code);
        Assert.AreEqual("Date hors des limites du calendrier.", start.Message);

        var sheet = SheetCreatedOn(lastDay.AddDays(-1));
        Assert.AreEqual(TrackingErrorCode.DateOutOfRange, sheet.ValidateReview(At(lastDay), null, null).Refused().Code);
        Assert.AreEqual(TrackingErrorCode.DateOutOfRange, sheet.Restart(At(lastDay), ReviewSequence.Default).Refused().Code);
        Assert.AreEqual(TrackingErrorCode.DateOutOfRange, sheet.ArchiveManually(At(lastDay), ReminderRules.Default).Refused().Code);
        Assert.IsEmpty(sheet.Cycle!.ProjectedSteps(lastDay, lastDay));
    }

    [TestMethod]
    public void Days_late_are_never_negative()
    {
        var cycle = SheetCreatedOn(D(2026, 10, 9)).Cycle!;
        Assert.AreEqual(0, cycle.DaysLate(D(2026, 10, 9)));
        Assert.AreEqual(0, cycle.DaysLate(D(2026, 10, 10)));
        Assert.AreEqual(3, cycle.DaysLate(D(2026, 10, 13)));
    }

    [TestMethod]
    public void Default_reminder_rules_are_valid()
    {
        var rules = ReminderRules.Default;
        Assert.IsNull(rules.Validate());
        Assert.AreEqual((2, 2, 30, true), (rules.MinimumDelayMonths, rules.MaxPerDay, rules.MaxWaitDays, rules.IncludeManualArchiving));
        int[] rest = [2, 4, 8, 12];
        Assert.AreSequenceEqual(rest, rules.RestSequenceMonths);
        Assert.IsNull((rules with { MaxPerDay = 0, MinimumDelayMonths = 1, MaxWaitDays = 1, RestSequenceMonths = [1] }).Validate());
    }

    [TestMethod]
    [DataRow(0, new[] { 2 }, 30, 2, "le délai minimal doit être d'au moins un mois")]
    [DataRow(2, new int[0], 30, 2, "chaque durée de repos doit être d'au moins un mois")]
    [DataRow(2, new[] { 2, 0 }, 30, 2, "chaque durée de repos doit être d'au moins un mois")]
    [DataRow(2, new[] { 2 }, 0, 2, "l'attente maximale doit être d'au moins un jour")]
    [DataRow(2, new[] { 2 }, 30, -1, "le nombre de rappels par jour ne peut pas être négatif")]
    public void Invalid_reminder_rules_are_refused(int delay, int[] rest, int maxWait, int maxPerDay, string reason)
    {
        var rules = new ReminderRules(delay, [.. rest], maxPerDay, maxWait, IncludeManualArchiving: true);
        var error = rules.Validate() ?? throw new AssertFailedException("Réglages acceptés à tort.");
        Assert.AreEqual(TrackingErrorCode.InvalidRules, error.Code);
        Assert.AreEqual("Réglages des rappels invalides : " + reason, error.Message);
    }

    [TestMethod]
    public void Default_rest_sequence_must_not_be_uninitialized()
    {
        var rules = ReminderRules.Default with { RestSequenceMonths = default };
        Assert.AreEqual(TrackingErrorCode.InvalidRules, rules.Validate()?.Code);
    }

    [TestMethod]
    public void Daily_seed_is_stable_for_a_vault_and_a_day()
    {
        var seed = DailySeed.For("v_01JA8", D(2027, 2, 3));
        Assert.AreEqual(seed, DailySeed.For("v_01JA8", D(2027, 2, 3)));
        Assert.AreNotEqual(seed, DailySeed.For("v_01JA8", D(2027, 2, 4)));
        Assert.AreNotEqual(seed, DailySeed.For("v_autre", D(2027, 2, 3)));
    }

    [TestMethod]
    public void Bounded_draws_reject_an_empty_range()
    {
        var random = ChaCha8Random.FromSeed(42);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextBounded(0));
        Assert.AreEqual(0, random.NextBounded(1));
        for (var i = 0; i < 1000; i++)
        {
            Assert.IsInRange(0, 6, random.NextBounded(7));
        }
    }

    [TestMethod]
    public void A_negative_number_of_marked_passages_is_a_programming_error()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ReminderAnswer.SomePointsToReview(-1));
        Assert.AreEqual(2, ReminderAnswer.SomePointsToReview(2).MarkedPassages);
        Assert.AreEqual(ReminderOutcome.SomePointsToReview, ReminderAnswer.SomePointsToReview(2).Outcome);
    }
}
