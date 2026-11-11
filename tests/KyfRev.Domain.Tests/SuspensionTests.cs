using KyfRev.Domain.Reviews;
using static KyfRev.Domain.Tests.Support.Dates;

namespace KyfRev.Domain.Tests;

/// <summary>Suspendre et reprendre : l'élément garde sa place, mais ne se présente plus à réviser.</summary>
[TestClass]
public sealed class SuspensionTests
{
    private static readonly ReminderRules Rules = ReminderRules.Default;

    [TestMethod]
    public void A_suspended_item_keeps_its_step_and_due_date_and_adds_nothing_to_the_history()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ReviewedOnTime(Rules);

        var suspended = sheet.Suspend(At(D(2026, 10, 11))).Ok();

        Assert.IsTrue(suspended.IsSuspended);
        Assert.IsNull(suspended.DueCycle);
        Assert.AreEqual(sheet.Cycle! with { SuspendedOn = D(2026, 10, 11) }, suspended.Cycle);
        Assert.AreSequenceEqual(sheet.History, suspended.History);
        Assert.AreEqual(sheet.Counters, suspended.Counters);
        Assert.IsNull(suspended.Verify());
        Assert.IsFalse(sheet.IsSuspended, "le suivi d'origine ne change pas");
    }

    [TestMethod]
    public void Suspended_it_is_neither_validated_nor_postponed_until_resumed()
    {
        var suspended = SheetCreatedOn(D(2026, 10, 9)).Suspend(At(D(2026, 10, 9))).Ok();
        var later = At(D(2026, 10, 20));

        Assert.AreEqual(TrackingErrorCode.Suspended, suspended.ValidateReview(later, null, Rules).Refused().Code);
        Assert.AreEqual(TrackingErrorCode.Suspended, suspended.Postpone(later, D(2026, 10, 25)).Refused().Code);
        Assert.AreEqual(TrackingErrorCode.Suspended, suspended.Suspend(later).Refused().Code);

        var resumed = suspended.Resume().Ok();
        Assert.IsFalse(resumed.IsSuspended);
        Assert.AreEqual(("J1", D(2026, 10, 10)), (resumed.DueCycle!.StepLabel, resumed.DueDate()), "là où il en était : l'échéance passée le met en retard");
        Assert.AreEqual(10, Assert.IsInstanceOfType<ReviewEvent>(resumed.ValidateReview(later, null, Rules).Ok().History[^1]).DaysLate);
    }

    [TestMethod]
    public void Only_an_active_suspended_item_resumes_and_a_restart_or_an_archiving_clears_the_suspension()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9));
        var suspended = sheet.Suspend(At(D(2026, 10, 9))).Ok();

        Assert.AreEqual(TrackingErrorCode.NotSuspended, sheet.Resume().Refused().Code);
        Assert.IsFalse(suspended.Restart(At(D(2026, 10, 12)), ReviewSequence.Default).Ok().IsSuspended);
        var archived = suspended.ArchiveManually(At(D(2026, 10, 12)), Rules).Ok();
        Assert.IsFalse(archived.IsSuspended);
        Assert.AreEqual(TrackingErrorCode.NotActive, archived.Suspend(At(D(2026, 10, 13))).Refused().Code);
        Assert.AreEqual(TrackingErrorCode.NotActive, archived.Resume().Refused().Code);
    }
}
