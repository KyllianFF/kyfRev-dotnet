using KyfRev.Domain.Reviews;
using static KyfRev.Domain.Tests.Support.Dates;

namespace KyfRev.Domain.Tests;

/// <summary>Une simple note : une fiche tenue hors des révisions, que « Mettre en révision » y fait entrer.</summary>
[TestClass]
public sealed class NoteTests
{
    private static readonly ReminderRules Rules = ReminderRules.Default;

    [TestMethod]
    public void A_note_leaves_the_reviews_and_adds_nothing_to_the_history()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ReviewedOnTime(Rules);

        var note = sheet.MakeNote(At(D(2026, 10, 11))).Ok();

        Assert.IsTrue(note.IsNote);
        Assert.IsTrue(note.IsSuspended, "une note est suspendue, sans attendre d'être reprise");
        Assert.IsNull(note.DueCycle);
        Assert.AreEqual(sheet.Cycle! with { SuspendedOn = D(2026, 10, 11), IsNote = true }, note.Cycle);
        Assert.AreSequenceEqual(sheet.History, note.History);
        Assert.AreEqual(sheet.Counters, note.Counters);
        Assert.IsNull(note.Verify());
        Assert.IsFalse(sheet.IsNote, "le suivi d'origine ne change pas");

        var later = At(D(2026, 10, 20));
        Assert.AreEqual(TrackingErrorCode.AlreadyNote, note.MakeNote(later).Refused().Code);
        Assert.AreEqual(TrackingErrorCode.Suspended, note.ValidateReview(later, null, Rules).Refused().Code);
        Assert.AreEqual(TrackingErrorCode.Suspended, note.Postpone(later, D(2026, 10, 25)).Refused().Code);
    }

    [TestMethod]
    public void A_suspended_item_made_a_note_keeps_the_day_it_was_suspended()
    {
        var suspended = SheetCreatedOn(D(2026, 10, 9)).Suspend(At(D(2026, 10, 10))).Ok();

        var note = suspended.MakeNote(At(D(2026, 10, 15))).Ok();

        Assert.AreEqual(D(2026, 10, 10), note.Cycle!.SuspendedOn);
        Assert.IsFalse(note.Resume().Ok().IsNote, "reprise, ce n'est plus une note");
    }

    [TestMethod]
    public void A_note_never_reviewed_starts_at_J0_the_day_it_is_put_in_review()
    {
        var note = SheetCreatedOn(D(2026, 10, 9)).MakeNote(At(D(2026, 10, 9))).Ok();

        var learned = note.PutInReview(At(D(2026, 11, 20)), ReviewSequence.Default).Ok();

        Assert.IsFalse(learned.IsNote);
        Assert.IsFalse(learned.IsSuspended);
        Assert.AreEqual(("J1", D(2026, 11, 21)), (learned.DueCycle!.StepLabel, learned.DueDate()), "son J0 est le jour où l'on décide de l'apprendre");
        Assert.AreEqual(D(2026, 11, 20), learned.DueCycle!.StartedOn);
        Assert.IsNull(learned.Verify());

        // Le jour même de sa création, rien à recommencer : elle reprend simplement.
        var sameDay = note.PutInReview(At(D(2026, 10, 9)), ReviewSequence.Default).Ok();
        Assert.AreEqual(note.Counters, sameDay.Counters);
        Assert.AreSequenceEqual(note.History, sameDay.History);
        Assert.AreEqual(("J1", D(2026, 10, 10)), (sameDay.DueCycle!.StepLabel, sameDay.DueDate()));
    }

    [TestMethod]
    public void A_note_already_reviewed_comes_back_where_it_was()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ReviewedOnTime(Rules);
        var note = sheet.MakeNote(At(D(2026, 10, 11))).Ok();

        var back = note.PutInReview(At(D(2026, 11, 20)), ReviewSequence.Default).Ok();

        Assert.AreEqual(sheet.Cycle, back.Cycle, "même étape, même échéance");
        Assert.AreSequenceEqual(sheet.History, back.History);
        Assert.AreEqual(sheet.Counters, back.Counters);
    }

    [TestMethod]
    public void Only_an_active_note_is_put_in_review_and_a_restart_or_an_archiving_ends_the_note()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9));
        var note = sheet.MakeNote(At(D(2026, 10, 9))).Ok();

        Assert.AreEqual(TrackingErrorCode.NotNote, sheet.PutInReview(At(D(2026, 10, 12)), ReviewSequence.Default).Refused().Code);
        Assert.IsFalse(note.Restart(At(D(2026, 10, 12)), ReviewSequence.Default).Ok().IsNote);
        var archived = note.ArchiveManually(At(D(2026, 10, 12)), Rules).Ok();
        Assert.IsFalse(archived.IsNote);
        Assert.AreEqual(TrackingErrorCode.NotActive, archived.MakeNote(At(D(2026, 10, 13))).Refused().Code);
        Assert.AreEqual(TrackingErrorCode.NotActive, archived.PutInReview(At(D(2026, 10, 13)), ReviewSequence.Default).Refused().Code);
    }
}
