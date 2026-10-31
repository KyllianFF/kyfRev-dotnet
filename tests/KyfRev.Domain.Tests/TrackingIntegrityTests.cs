using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;
using KyfRev.Domain.Tests.Reference;
using static KyfRev.Domain.Tests.Support.Dates;

namespace KyfRev.Domain.Tests;

/// <summary>Contrôle d'intégrité d'un suivi relu depuis le disque, et garanties d'immuabilité.</summary>
[TestClass]
public sealed class TrackingIntegrityTests
{
    private static readonly Tracking Sheet = SheetCreatedOn(D(2026, 10, 9)).ReviewedOnTime(ReminderRules.Default);

    [TestMethod]
    public void A_consistent_tracking_is_restored_identically()
    {
        var restored = Tracking.Restore(Sheet.State, Sheet.Counters, Sheet.History).Ok();
        Assert.AreEqual(RustJson.Serialize(Sheet), RustJson.Serialize(restored));
    }

    [TestMethod]
    public void Counters_that_do_not_match_the_history_are_detected()
    {
        var error = Tracking.Restore(Sheet.State, Sheet.Counters with { Completed = 3 }, Sheet.History).Refused();
        Assert.AreEqual(TrackingErrorCode.Inconsistent, error.Code);
        Assert.AreEqual("Le suivi est incohérent : les compteurs ne correspondent pas à l'historique", error.Message);
    }

    [TestMethod]
    public void A_history_that_does_not_start_with_J0_is_detected()
    {
        const string expected = "Le suivi est incohérent : l'historique doit commencer par un J0";
        Assert.AreEqual(expected, Tracking.Restore(Sheet.State, Sheet.Counters, []).Refused().Message);
        Assert.AreEqual(expected, Tracking.Restore(Sheet.State, Sheet.Counters, Sheet.History.RemoveAt(0)).Refused().Message);

        TrackingEvent[] otherFirst = [new ModificationEvent(Sheet.History[0].At, ModifiedObject.Comment), .. Sheet.History];
        Assert.AreEqual(expected, Tracking.Restore(Sheet.State, Sheet.Counters, otherFirst).Refused().Message);
    }

    [TestMethod]
    [DataRow(2, 1, "numéro de cycle incohérent")]
    [DataRow(1, 0, "étape hors de la séquence")]
    [DataRow(1, 6, "étape hors de la séquence")]
    public void An_impossible_cycle_is_detected(int number, int step, string reason)
    {
        var cycle = Sheet.Cycle! with { Number = number, Step = step };
        Assert.AreEqual(
            "Le suivi est incohérent : " + reason,
            Tracking.Restore(new ActiveState(cycle), Sheet.Counters, Sheet.History).Refused().Message);
    }

    [TestMethod]
    public void Cycle_dates_out_of_order_are_detected()
    {
        var cycle = Sheet.Cycle!;
        foreach (var broken in new[] { cycle with { DueDate = cycle.LastReview }, cycle with { StartedOn = cycle.LastReview.AddDays(1) } })
        {
            Assert.AreEqual(
                "Le suivi est incohérent : dates du cycle incohérentes",
                Tracking.Restore(new ActiveState(broken), Sheet.Counters, Sheet.History).Refused().Message);
        }
    }

    [TestMethod]
    public void A_cycle_count_that_does_not_add_up_is_detected()
    {
        var archived = Sheet.ArchiveManually(At(D(2026, 10, 12)), ReminderRules.Default).Ok();
        // Un état actif avec les compteurs et l'historique d'un élément archivé.
        Assert.AreEqual(
            "Le suivi est incohérent : nombre de cycles commencés incohérent",
            Tracking.Restore(Sheet.State, archived.Counters, archived.History).Refused().Message);
    }

    [TestMethod]
    public void An_eligibility_date_before_archiving_is_detected()
    {
        var archived = Sheet.ArchiveManually(At(D(2026, 10, 12)), ReminderRules.Default).Ok();
        var broken = archived.Archive! with { EligibleOn = D(2026, 10, 1) };
        Assert.AreEqual(
            "Le suivi est incohérent : date d'éligibilité antérieure à l'archivage",
            Tracking.Restore(new ArchivedState(broken), archived.Counters, archived.History).Refused().Message);

        var sameDay = archived.Archive with { EligibleOn = archived.Archive.ArchivedOn };
        Assert.IsTrue(Tracking.Restore(new ArchivedState(sameDay), archived.Counters, archived.History).IsSuccess);
    }

    [TestMethod]
    public void Operations_never_modify_the_original_tracking()
    {
        var before = RustJson.Serialize(Sheet);

        _ = Sheet.ValidateReview(At(D(2026, 10, 13)), null, ReminderRules.Default);
        _ = Sheet.Postpone(At(D(2026, 10, 11)), D(2026, 10, 20));
        _ = Sheet.Restart(At(D(2026, 10, 11)), ReviewSequence.Default);
        _ = Sheet.ArchiveManually(At(D(2026, 10, 11)), ReminderRules.Default);
        _ = Sheet.RecordModification(At(D(2026, 10, 11)), ModifiedObject.Definition);

        Assert.AreEqual(before, RustJson.Serialize(Sheet));
    }

    [TestMethod]
    public void Operations_on_the_wrong_state_are_refused()
    {
        var archived = Sheet.ArchiveManually(At(D(2026, 10, 12)), ReminderRules.Default).Ok();

        Assert.AreEqual(TrackingErrorCode.NotActive, archived.ValidateReview(At(D(2026, 10, 13)), null, null).Refused().Code);
        Assert.AreEqual("Cet élément n'est pas en révision active.", archived.Postpone(At(D(2026, 10, 13)), D(2026, 10, 20)).Refused().Message);
        Assert.AreEqual(TrackingErrorCode.NotActive, archived.ArchiveManually(At(D(2026, 10, 13)), null).Refused().Code);

        Assert.AreEqual("Cet élément n'est pas archivé.", Sheet.MarkDrawn(D(2026, 10, 13)).Refused().Message);
        Assert.AreEqual(TrackingErrorCode.NotArchived, Sheet.IncludeInReminders(At(D(2026, 10, 13)), true).Refused().Code);
        Assert.AreEqual(
            TrackingErrorCode.NotArchived,
            Sheet.HandleReminder(At(D(2026, 10, 13)), ReminderAnswer.Known, ReminderRules.Default, ReviewSequence.Default).Refused().Code);
        Assert.IsFalse(Sheet.IsEligibleOn(D(2030, 1, 1)));
    }

    [TestMethod]
    public void Including_twice_records_nothing()
    {
        var archived = Sheet.ArchiveManually(At(D(2026, 10, 12)), ReminderRules.Default).Ok();
        var same = archived.IncludeInReminders(At(D(2026, 10, 13)), include: true).Ok();
        Assert.AreSame(archived, same);
    }

    [TestMethod]
    public void Excluding_a_drawn_sheet_cancels_its_pending_reminder()
    {
        var drawn = Sheet.ArchiveManually(At(D(2026, 10, 12)), ReminderRules.Default).Ok().MarkDrawn(D(2026, 12, 12)).Ok();
        var excluded = drawn.IncludeInReminders(At(D(2026, 12, 13)), include: false).Ok();
        Assert.IsNull(excluded.Archive!.ReminderDrawnOn);
        Assert.IsFalse(excluded.Archive.IncludedInReminders);

        var included = excluded.IncludeInReminders(At(D(2026, 12, 14)), include: true).Ok();
        Assert.IsTrue(included.IsEligibleOn(D(2026, 12, 14)));
        Assert.IsFalse(included.IsEligibleOn(D(2026, 12, 11)));
        bool[] inclusions = [false, true];
        Assert.AreSequenceEqual(inclusions, included.History.OfType<ReminderInclusionEvent>().Select(e => e.Include));
    }

    [TestMethod]
    public void Invalid_rules_are_refused_before_archiving()
    {
        var invalid = ReminderRules.Default with { MinimumDelayMonths = 0 };
        var error = Sheet.ArchiveManually(At(D(2026, 10, 12)), invalid).Refused();
        Assert.AreEqual(TrackingErrorCode.InvalidRules, error.Code);
        Assert.AreEqual("Réglages des rappels invalides : le délai minimal doit être d'au moins un mois", error.Message);
    }

    [TestMethod]
    public void Invalid_rules_are_refused_when_the_last_step_archives_the_sheet()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9));
        for (var i = 0; i < 4; i++)
        {
            sheet = sheet.ReviewedOnTime(ReminderRules.Default);
        }
        var invalid = ReminderRules.Default with { RestSequenceMonths = [] };
        var error = sheet.ValidateReview(At(sheet.DueDate()), null, invalid).Refused();
        Assert.AreEqual("Réglages des rappels invalides : chaque durée de repos doit être d'au moins un mois", error.Message);
    }

    [TestMethod]
    public void Invalid_rules_are_refused_when_handling_a_reminder()
    {
        var drawn = Sheet.ArchiveManually(At(D(2026, 10, 12)), ReminderRules.Default).Ok().MarkDrawn(D(2026, 12, 12)).Ok();
        var invalid = ReminderRules.Default with { MaxWaitDays = 0 };
        var error = drawn.HandleReminder(At(D(2026, 12, 12)), ReminderAnswer.Known, invalid, ReviewSequence.Default).Refused();
        Assert.AreEqual(TrackingErrorCode.InvalidRules, error.Code);
    }

    [TestMethod]
    public void Reminders_that_would_leave_the_calendar_are_refused()
    {
        var last = DateOnly.MaxValue;
        var j0 = new ReviewEvent(At(last.AddDays(-20)).Instant, last.AddDays(-20), 1, "J0", last.AddDays(-20), 0, StartOrigin.Creation);
        var archive = new ArchiveInfo(last.AddDays(-10), ArchiveReason.Manual, IncludedInReminders: true, 0, last.AddDays(-5), last.AddDays(-5));
        var drawn = Tracking.Restore(
            new ArchivedState(archive),
            new CycleCounters(Started: 1, Completed: 0, Interrupted: 1),
            [j0, new InterruptionEvent(j0.At, archive.ArchivedOn, 1), new ArchivingEvent(j0.At, archive.ArchivedOn, ArchiveReason.Manual)]).Ok();

        foreach (var answer in new[] { ReminderAnswer.Known, ReminderAnswer.SomePointsToReview(1), ReminderAnswer.ReviewEntirely })
        {
            Assert.AreEqual(TrackingErrorCode.DateOutOfRange, drawn.HandleReminder(At(last), answer, ReminderRules.Default, ReviewSequence.Default).Refused().Code);
        }
    }

    [TestMethod]
    public void Including_again_keeps_a_recorded_draw_like_the_rust_core()
    {
        // État relu tel quel : fiche exclue mais dont le tirage est encore enregistré. Comme dans
        // la version 0.x, la réinclure garde ce tirage ; seule l'exclusion l'efface.
        var archived = Sheet.ArchiveManually(At(D(2026, 10, 12)), ReminderRules.Default).Ok();
        var odd = archived.Archive! with { IncludedInReminders = false, ReminderDrawnOn = D(2026, 12, 12) };
        var restored = Tracking.Restore(new ArchivedState(odd), archived.Counters, archived.History).Ok();

        var included = restored.IncludeInReminders(At(D(2026, 12, 13)), include: true).Ok();
        Assert.AreEqual(D(2026, 12, 12), included.Archive!.ReminderDrawnOn);
    }

    [TestMethod]
    public void Missing_arguments_are_programming_errors()
    {
        var now = At(D(2026, 10, 13));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tracking.Start(now, null!, StartOrigin.Creation));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tracking.Restore(null!, Sheet.Counters, Sheet.History));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tracking.Restore(Sheet.State, Sheet.Counters, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Sheet.Restart(now, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Sheet.HandleReminder(now, ReminderAnswer.Known, null!, ReviewSequence.Default));
        Assert.ThrowsExactly<ArgumentNullException>(() => Sheet.HandleReminder(now, ReminderAnswer.Known, ReminderRules.Default, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ReminderDraw.Draw(D(2027, 1, 1), null!, 0, ReminderRules.Default, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => ReminderDraw.Draw(D(2027, 1, 1), [], 0, null!, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => KyfRev.Domain.Randomness.DailySeed.For(null!, D(2027, 1, 1)));
    }

    [TestMethod]
    public void A_moment_is_always_kept_in_universal_time()
    {
        var local = new DateTimeOffset(2026, 10, 9, 14, 0, 0, TimeSpan.FromHours(2));
        var moment = new Moment(D(2026, 10, 9), local);
        Assert.AreEqual(TimeSpan.Zero, moment.Instant.Offset);
        Assert.AreEqual(local, moment.Instant);
        Assert.AreEqual(D(2026, 10, 9), moment.Day);
    }
}
