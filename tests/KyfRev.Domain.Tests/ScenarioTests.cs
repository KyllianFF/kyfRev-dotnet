using KyfRev.Domain.Reviews;
using static KyfRev.Domain.Tests.Support.Dates;

namespace KyfRev.Domain.Tests;

/// <summary>Scénarios datés du dossier de conception (sections 7 et 10).</summary>
[TestClass]
public sealed class ScenarioTests
{
    private static readonly ReminderRules Rules = ReminderRules.Default;

    // --- Calendrier (D2, D3, D7) ---------------------------------------------------------

    [TestMethod]
    public void T11_creation_counts_as_J0_and_J1_is_due_the_next_day()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9));

        Assert.AreEqual(TrackingStatus.Active, sheet.Status);
        Assert.AreEqual(D(2026, 10, 10), sheet.DueDate());
        Assert.AreEqual("J1", sheet.Cycle!.StepLabel);
        Assert.AreEqual(1, sheet.Counters.Started);
        var j0 = Assert.IsInstanceOfType<ReviewEvent>(Assert.ContainsSingle(sheet.History));
        Assert.AreEqual(StartOrigin.Creation, j0.Origin);
        Assert.AreEqual(0, j0.DaysLate);

        Assert.AreEqual(D(2026, 10, 15), SheetCreatedOn(D(2026, 10, 14)).DueDate());
    }

    [TestMethod]
    public void Load_forecast_assumes_each_review_is_done_on_its_due_date()
    {
        // Créée le 9 octobre, J1 due le 10. Vue du 9 octobre sur 30 jours : J3 le 13, J7 le 20,
        // J14 le 3 novembre ; J30 (3 décembre) tombe au-delà.
        var cycle = SheetCreatedOn(D(2026, 10, 9)).Cycle!;
        (DateOnly, string)[] expected = [(D(2026, 10, 13), "J3"), (D(2026, 10, 20), "J7"), (D(2026, 11, 3), "J14")];
        Assert.AreSequenceEqual(expected, cycle.ProjectedSteps(D(2026, 10, 9), D(2026, 11, 8)).Select(p => (p.Date, cycle.Sequence.Label(p.Step))));

        // En retard (J1 attendue le 10, vue du 15) : la révision est supposée faite aujourd'hui,
        // et la suite se calcule depuis le 15 (D7).
        DateOnly[] late = [D(2026, 10, 18), D(2026, 10, 25), D(2026, 11, 8)];
        Assert.AreSequenceEqual(late, cycle.ProjectedSteps(D(2026, 10, 15), D(2026, 11, 14)).Select(p => p.Date));

        // Dernière étape attendue : rien après, le cycle se termine.
        var sheet = SheetCreatedOn(D(2026, 10, 9));
        for (var i = 0; i < 4; i++)
        {
            sheet = sheet.ReviewedOnTime(Rules);
        }
        Assert.AreEqual("J30", sheet.Cycle!.StepLabel);
        Assert.IsEmpty(sheet.Cycle.ProjectedSteps(D(2026, 11, 3), D(2027, 1, 1)));
    }

    [TestMethod]
    public void Example_1_punctual_cycle_lasts_55_days()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9));
        (string, DateOnly)[] expected =
        [
            ("J1", D(2026, 10, 10)),
            ("J3", D(2026, 10, 13)),
            ("J7", D(2026, 10, 20)),
            ("J14", D(2026, 11, 3)),
            ("J30", D(2026, 12, 3)),
        ];
        foreach (var step in expected)
        {
            Assert.AreEqual(step, (sheet.Cycle!.StepLabel, sheet.Cycle.DueDate));
            sheet = sheet.ReviewedOnTime(Rules);
        }
        Assert.AreEqual(TrackingStatus.Archived, sheet.Status);
        Assert.AreEqual(55, ReviewSequence.Default.TotalDurationDays);
    }

    [TestMethod]
    [DataRow("2026-10-15", "2026-10-22", "2026-11-05", "2026-12-05")]
    [DataRow("2026-10-18", "2026-10-25", "2026-11-08", "2026-12-08")]
    public void Example_1_late_review_shifts_the_rest_of_the_calendar(string doneOn, string j7, string j14, string j30)
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ReviewedOnTime(Rules);
        sheet = sheet.ValidateReview(At(Iso(doneOn)), null, Rules).Ok();
        Assert.AreEqual(Iso(j7), sheet.DueDate());
        sheet = sheet.ReviewedOnTime(Rules);
        Assert.AreEqual(Iso(j14), sheet.DueDate());
        sheet = sheet.ReviewedOnTime(Rules);
        Assert.AreEqual(Iso(j30), sheet.DueDate());
    }

    [TestMethod]
    public void T10_postponement_then_validation()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ReviewedOnTime(Rules); // J1 le 10, J3 attendu le 13
        sheet = sheet.Postpone(At(D(2026, 10, 12)), D(2026, 10, 14)).Ok();
        Assert.AreEqual(D(2026, 10, 14), sheet.DueDate());
        Assert.AreEqual("J3", sheet.Cycle!.StepLabel, "un report n'est pas une révision");

        sheet = sheet.ValidateReview(At(D(2026, 10, 14)), null, Rules).Ok();
        Assert.AreEqual(D(2026, 10, 21), sheet.DueDate());
        Assert.Contains(e => e is PostponementEvent { From.Day: 13, To.Day: 14 }, sheet.History);
    }

    [TestMethod]
    public void Example_2_annotated_passage_has_its_own_calendar()
    {
        var passage = SheetCreatedOn(D(2026, 10, 14));
        var dueDates = new List<DateOnly> { passage.DueDate() };
        while (passage.Status == TrackingStatus.Active)
        {
            passage = passage.ReviewedOnTime(Rules);
            if (passage.Cycle is { } cycle)
            {
                dueDates.Add(cycle.DueDate);
            }
        }
        DateOnly[] expected = [D(2026, 10, 15), D(2026, 10, 18), D(2026, 10, 25), D(2026, 11, 8), D(2026, 12, 8)];
        Assert.AreSequenceEqual(expected, dueDates);
    }

    [TestMethod]
    public void Refused_validations_report_their_reason()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9));
        var nextDay = At(D(2026, 10, 10));

        var sameDay = sheet.ValidateReview(At(D(2026, 10, 9)), null, Rules).Refused();
        Assert.AreEqual(TrackingErrorCode.AlreadyReviewed, sameDay.Code);
        Assert.AreEqual("Une révision est déjà enregistrée le 2026-10-09.", sameDay.Message);

        var future = sheet.ValidateReview(nextDay, D(2026, 10, 11), Rules).Refused();
        Assert.AreEqual(TrackingErrorCode.FutureDate, future.Code);
        Assert.AreEqual("Impossible de valider une révision à une date future (2026-10-11).", future.Message);

        var before = sheet.ValidateReview(nextDay, D(2026, 10, 8), Rules).Refused();
        Assert.AreEqual(TrackingErrorCode.BeforeLastReview, before.Code);
        Assert.AreEqual("La date 2026-10-08 précède la dernière révision (2026-10-09).", before.Message);

        var past = sheet.Postpone(nextDay, D(2026, 10, 10)).Refused();
        Assert.AreEqual(TrackingErrorCode.PostponeInPast, past.Code);
        Assert.AreEqual("Le report doit viser une date postérieure à aujourd'hui (2026-10-10).", past.Message);

        var unchanged = sheet.Postpone(At(D(2026, 10, 9)), D(2026, 10, 10)).Refused();
        Assert.AreEqual(TrackingErrorCode.PostponeUnchanged, unchanged.Code);
        Assert.AreEqual("Cette révision est déjà prévue le 2026-10-10.", unchanged.Message);
    }

    [TestMethod]
    public void Backdated_and_early_validations()
    {
        // Révisée le 10, saisie le 12 : J3 est dû le 13 (10 + 3).
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ValidateReview(At(D(2026, 10, 12)), D(2026, 10, 10), Rules).Ok();
        Assert.AreEqual(D(2026, 10, 13), sheet.DueDate());
        var backdated = Assert.IsInstanceOfType<ReviewEvent>(sheet.History[^1]);
        Assert.AreEqual(D(2026, 10, 10), backdated.Day);
        Assert.AreEqual(D(2026, 10, 12), DateOnly.FromDateTime(backdated.At.UtcDateTime));

        // J3 validé en avance le 12 : retard négatif, J7 dû le 19.
        sheet = sheet.ValidateReview(At(D(2026, 10, 12)), null, Rules).Ok();
        Assert.AreEqual(D(2026, 10, 19), sheet.DueDate());
        Assert.AreEqual(-1, Assert.IsInstanceOfType<ReviewEvent>(sheet.History[^1]).DaysLate);
    }

    [TestMethod]
    public void Changing_the_sequence_does_not_touch_the_current_cycle()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ReviewedOnTime(Rules);
        var creation = ReviewSequence.Create([0, 2, 5, 10]);
        Assert.IsTrue(creation.IsSuccess);

        // La séquence d'un cycle est copiée à son démarrage ; seul « Recommencer » en prend une nouvelle.
        Assert.AreEqual(ReviewSequence.Default, sheet.Cycle!.Sequence);
        sheet = sheet.Restart(At(D(2026, 10, 11)), creation.Sequence).Ok();
        Assert.AreEqual(creation.Sequence, sheet.Cycle!.Sequence);
        Assert.AreEqual(D(2026, 10, 13), sheet.DueDate());
    }

    // --- Archivage et rappels -------------------------------------------------------------

    [TestMethod]
    public void T2_end_of_cycle_archives_with_history_intact()
    {
        var sheet = SheetArchivedOnDecember3();

        var archive = sheet.Archive!;
        Assert.AreEqual(D(2026, 12, 3), archive.ArchivedOn);
        Assert.AreEqual(ArchiveReason.EndOfCycle, archive.Reason);
        Assert.IsTrue(archive.IncludedInReminders);
        Assert.AreEqual(1, sheet.Counters.Completed);
        (string, DateOnly)[] expected =
            [("J0", D(2026, 10, 9)), ("J1", D(2026, 10, 10)), ("J3", D(2026, 10, 13)), ("J7", D(2026, 10, 20)), ("J14", D(2026, 11, 3)), ("J30", D(2026, 12, 3))];
        Assert.AreSequenceEqual(expected, sheet.History.OfType<ReviewEvent>().Select(e => (e.StepLabel, e.Day)));
        Assert.IsNull(sheet.Verify());
    }

    [TestMethod]
    public void T3_no_reminder_before_two_calendar_months()
    {
        var sheet = SheetArchivedOnDecember3();
        var eligibleOn = sheet.Archive!.EligibleOn;
        Assert.AreEqual(D(2027, 2, 3), eligibleOn);

        DrawCandidate[] candidates = [new("f", eligibleOn)];
        for (var day = D(2026, 12, 3); day < D(2027, 2, 3); day = day.AddDays(1))
        {
            Assert.IsFalse(sheet.IsEligibleOn(day), $"éligible trop tôt le {day:O}");
            Assert.IsEmpty(Draw(day, candidates).Selection);
        }
        Assert.IsTrue(sheet.IsEligibleOn(D(2027, 2, 3)));
        Assert.AreEqual("f", Assert.ContainsSingle(Draw(D(2027, 2, 3), candidates).Selection));
    }

    [TestMethod]
    [DataRow("2026-12-31", "2027-02-28")]
    [DataRow("2027-12-31", "2028-02-29")]
    [DataRow("2027-08-31", "2027-10-31")]
    public void T3_end_of_month_edge_cases(string archivedOn, string eligibleOn)
    {
        var sheet = SheetCreatedOn(Iso(archivedOn)).ArchiveManually(At(Iso(archivedOn)), Rules).Ok();
        Assert.AreEqual(Iso(eligibleOn), sheet.Archive!.EligibleOn);
    }

    [TestMethod]
    public void T4_known_sheet_stays_archived_and_rests_longer()
    {
        var sheet = SheetArchivedOnDecember3().MarkDrawn(D(2027, 2, 8)).Ok();
        sheet = sheet.HandleReminder(At(D(2027, 2, 8)), ReminderAnswer.Known, Rules, ReviewSequence.Default).Ok();

        var archive = sheet.Archive!;
        Assert.AreEqual((1, D(2027, 6, 8)), (archive.RestLevel, archive.EligibleOn));
        Assert.IsNull(archive.ReminderDrawnOn);
        Assert.AreEqual(1, sheet.Counters.Started, "aucun cycle créé");
        var reminder = Assert.IsInstanceOfType<ReminderEvent>(sheet.History[^1]);
        Assert.AreEqual((ReminderOutcome.Known, D(2027, 2, 8)), (reminder.Outcome, reminder.DrawnOn));

        sheet = sheet.MarkDrawn(D(2027, 6, 8)).Ok()
            .HandleReminder(At(D(2027, 6, 8)), ReminderAnswer.Known, Rules, ReviewSequence.Default).Ok();
        Assert.AreEqual(D(2028, 2, 8), sheet.Archive!.EligibleOn);
        Assert.IsNull(sheet.Verify());
    }

    [TestMethod]
    public void Rest_level_stops_at_the_last_rest_duration()
    {
        var sheet = SheetArchivedOnDecember3();
        var day = sheet.Archive!.EligibleOn;
        int[] expectedLevels = [1, 2, 3, 3, 3];
        foreach (var expected in expectedLevels)
        {
            sheet = sheet.MarkDrawn(day).Ok().HandleReminder(At(day), ReminderAnswer.Known, Rules, ReviewSequence.Default).Ok();
            Assert.AreEqual(expected, sheet.Archive!.RestLevel);
            day = sheet.Archive.EligibleOn;
        }
        // 2027-02-03 puis repos de 4, 8, 12, 12 et 12 mois.
        Assert.AreEqual(D(2031, 2, 3), day);
    }

    [TestMethod]
    public void T5_some_points_to_review()
    {
        var sheet = SheetArchivedOnDecember3().MarkDrawn(D(2027, 2, 8)).Ok();

        var refused = sheet.HandleReminder(At(D(2027, 2, 8)), ReminderAnswer.SomePointsToReview(0), Rules, ReviewSequence.Default).Refused();
        Assert.AreEqual(TrackingErrorCode.NoPassageMarked, refused.Code);
        Assert.AreEqual("Marquez au moins un passage à revoir, ou choisissez « Fiche connue ».", refused.Message);

        sheet = sheet.HandleReminder(At(D(2027, 2, 8)), ReminderAnswer.SomePointsToReview(1), Rules, ReviewSequence.Default).Ok();
        Assert.AreEqual(TrackingStatus.Archived, sheet.Status);
        Assert.AreEqual(D(2027, 4, 8), sheet.Archive!.EligibleOn);
        Assert.AreEqual(0, sheet.Archive.RestLevel);
        Assert.AreEqual(ReminderOutcome.SomePointsToReview, Assert.IsInstanceOfType<ReminderEvent>(sheet.History[^1]).Outcome);

        // Le passage marqué est un élément indépendant, avec son propre J0.
        var passage = SheetCreatedOn(D(2027, 2, 8));
        Assert.AreEqual(D(2027, 2, 9), passage.DueDate());
        Assert.IsTrue(passage.ValidateReview(At(D(2027, 2, 9)), null, null).IsSuccess);
    }

    [TestMethod]
    public void T6_sheet_to_review_entirely_restarts_at_J0_and_keeps_its_history()
    {
        var archived = SheetArchivedOnDecember3();
        var sheet = archived.MarkDrawn(D(2027, 2, 8)).Ok()
            .HandleReminder(At(D(2027, 2, 8)), ReminderAnswer.ReviewEntirely, Rules, ReviewSequence.Default).Ok();

        var cycle = sheet.Cycle!;
        Assert.AreEqual((2, D(2027, 2, 8), D(2027, 2, 9)), (cycle.Number, cycle.StartedOn, cycle.DueDate));
        Assert.AreEqual((2, 1), (sheet.Counters.Started, sheet.Counters.Completed));
        Assert.AreSequenceEqual(archived.History, sheet.History.Take(archived.History.Length));
        Assert.AreEqual(ReminderOutcome.ReviewEntirely, Assert.IsInstanceOfType<ReminderEvent>(sheet.History[^3]).Outcome);
        Assert.IsInstanceOfType<ReactivationEvent>(sheet.History[^2]);
        var j0 = Assert.IsInstanceOfType<ReviewEvent>(sheet.History[^1]);
        Assert.AreEqual((StartOrigin.Reminder, 2), (j0.Origin, j0.CycleNumber));
        Assert.IsNull(sheet.Verify());
    }

    [TestMethod]
    public void Reminder_needs_a_draw_and_eligibility()
    {
        var sheet = SheetArchivedOnDecember3();

        var noReminder = sheet.HandleReminder(At(D(2027, 2, 8)), ReminderAnswer.Known, Rules, ReviewSequence.Default).Refused();
        Assert.AreEqual(TrackingErrorCode.NoPendingReminder, noReminder.Code);
        Assert.AreEqual("Aucun rappel de consolidation n'est en attente pour cette fiche.", noReminder.Message);

        var tooEarly = sheet.MarkDrawn(D(2027, 2, 2)).Refused();
        Assert.AreEqual(TrackingErrorCode.NotYetEligible, tooEarly.Code);
        Assert.AreEqual("Cette fiche ne pourra être rappelée qu'à partir du 2027-02-03.", tooEarly.Message);

        sheet = sheet.MarkDrawn(D(2027, 2, 3)).Ok();
        Assert.AreEqual(D(2027, 2, 3), sheet.Archive!.ReminderDrawnOn);
        var twice = sheet.MarkDrawn(D(2027, 2, 4)).Refused();
        Assert.AreEqual(TrackingErrorCode.ReminderAlreadyPending, twice.Code);
        Assert.AreEqual("Un rappel de consolidation est déjà en attente pour cette fiche.", twice.Message);
        Assert.IsFalse(sheet.IsEligibleOn(D(2027, 2, 4)), "un rappel en attente n'est pas tiré une seconde fois");
    }

    [TestMethod]
    public void Manual_archiving_and_exclusion_from_reminders()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ArchiveManually(At(D(2026, 10, 20)), Rules).Ok();
        var archive = sheet.Archive!;
        Assert.AreEqual(ArchiveReason.Manual, archive.Reason);
        Assert.IsTrue(archive.IncludedInReminders);
        Assert.AreEqual(D(2026, 12, 20), archive.EligibleOn);
        Assert.AreEqual(1, sheet.Counters.Interrupted);
        Assert.AreEqual(ArchiveReason.Manual, Assert.IsInstanceOfType<ArchivingEvent>(sheet.History[^1]).Reason);

        sheet = sheet.IncludeInReminders(At(D(2026, 10, 21)), include: false).Ok();
        Assert.IsFalse(sheet.IsEligibleOn(D(2027, 1, 1)));
        var excluded = sheet.MarkDrawn(D(2027, 1, 1)).Refused();
        Assert.AreEqual(TrackingErrorCode.ExcludedFromReminders, excluded.Code);
        Assert.AreEqual("Cette fiche est exclue des rappels de consolidation.", excluded.Message);

        // Remise en révision manuelle : nouveau cycle, J0 le jour même.
        sheet = sheet.Restart(At(D(2027, 1, 5)), ReviewSequence.Default).Ok();
        Assert.AreEqual(2, sheet.Cycle!.Number);
        Assert.IsInstanceOfType<ReactivationEvent>(sheet.History[^2]);
        Assert.IsNull(sheet.Verify());
    }

    [TestMethod]
    public void Manual_archiving_follows_the_include_manual_archiving_rule()
    {
        var rules = Rules with { IncludeManualArchiving = false };
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ArchiveManually(At(D(2026, 10, 20)), rules).Ok();
        Assert.IsFalse(sheet.Archive!.IncludedInReminders);
        Assert.IsFalse(sheet.IsEligibleOn(D(2030, 1, 1)));
    }

    [TestMethod]
    public void An_archived_passage_has_no_reminders()
    {
        var passage = SheetCreatedOn(D(2026, 10, 14));
        while (passage.Status == TrackingStatus.Active)
        {
            passage = passage.ValidateReview(At(passage.DueDate()), null, rules: null).Ok();
        }
        Assert.IsFalse(passage.Archive!.IncludedInReminders);
        Assert.AreEqual(passage.Archive.ArchivedOn, passage.Archive.EligibleOn);
        Assert.IsFalse(passage.IsEligibleOn(D(2030, 1, 1)));
        Assert.IsNull(passage.Verify());

        var manual = SheetCreatedOn(D(2026, 10, 14)).ArchiveManually(At(D(2026, 10, 20)), rules: null).Ok();
        Assert.IsFalse(manual.Archive!.IncludedInReminders);
    }

    [TestMethod]
    public void Restarting_an_active_cycle_interrupts_it()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ReviewedOnTime(Rules)
            .Restart(At(D(2026, 10, 12)), ReviewSequence.Default).Ok();

        Assert.AreEqual((2, 1, 0), (sheet.Counters.Started, sheet.Counters.Interrupted, sheet.Counters.Completed));
        Assert.AreEqual(D(2026, 10, 13), sheet.DueDate());
        Assert.AreEqual(1, Assert.IsInstanceOfType<InterruptionEvent>(sheet.History[^2]).CycleNumber);
        Assert.AreEqual(StartOrigin.Restart, Assert.IsInstanceOfType<ReviewEvent>(sheet.History[^1]).Origin);
        Assert.IsNull(sheet.Verify());
    }

    [TestMethod]
    public void Recording_a_modification_leaves_the_calendar_untouched()
    {
        var passage = SheetCreatedOn(D(2026, 10, 14));
        var modified = passage
            .RecordModification(At(D(2026, 10, 15)), ModifiedObject.Comment)
            .RecordModification(At(D(2026, 10, 15)), ModifiedObject.Passage);

        Assert.AreEqual(passage.State, modified.State);
        Assert.AreEqual(passage.Counters, modified.Counters);
        Assert.AreEqual(D(2026, 10, 15), modified.DueDate());
        Assert.HasCount(3, modified.History);
        Assert.IsInstanceOfType<ReviewEvent>(modified.History[0]);
        Assert.AreEqual(ModifiedObject.Comment, Assert.IsInstanceOfType<ModificationEvent>(modified.History[1]).Changed);
        Assert.AreEqual(ModifiedObject.Passage, Assert.IsInstanceOfType<ModificationEvent>(modified.History[2]).Changed);
        Assert.IsNull(modified.Verify());
    }

    /// <summary>Fiche de l'exemple 1, révisée ponctuellement jusqu'à la fin de son cycle.</summary>
    private static Tracking SheetArchivedOnDecember3()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9));
        for (var i = 0; i < 5; i++)
        {
            sheet = sheet.ReviewedOnTime(Rules);
        }
        return sheet;
    }

    private static DailyDraw Draw(DateOnly today, DrawCandidate[] candidates) =>
        ReminderDraw.Draw(today, candidates, pendingReminders: 0, Rules, seed: 1).Ok();
}
