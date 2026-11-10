using KyfRev.Domain.Reviews;
using static KyfRev.Domain.Tests.Support.Dates;

namespace KyfRev.Domain.Tests;

/// <summary>« J'avais oublié » : la révision compte, et l'étape recule d'un cran.</summary>
[TestClass]
public sealed class ForgottenReviewTests
{
    private static readonly ReminderRules Rules = ReminderRules.Default;

    [TestMethod]
    public void A_validated_review_and_a_J0_are_not_forgotten()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ReviewedOnTime(Rules);

        Assert.IsFalse(sheet.History.OfType<ReviewEvent>().Any(review => review.Forgotten));
    }

    [TestMethod]
    public void Forgetting_at_J7_brings_J3_back_three_days_later()
    {
        // J1 le 10, J3 le 13 : J7 est attendu le 20.
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ReviewedOnTime(Rules).ReviewedOnTime(Rules);
        Assert.AreEqual("J7", sheet.Cycle!.StepLabel);

        var result = sheet.ValidateReview(At(D(2026, 10, 20)), null, Rules, ReviewRecall.Forgotten);

        var forgotten = result.Ok();
        Assert.AreEqual(new NextStepScheduled("J3", D(2026, 10, 23)), result.Outcome);
        Assert.AreEqual("J3", forgotten.Cycle!.StepLabel);
        Assert.AreEqual(D(2026, 10, 20), forgotten.Cycle.LastReview);
        var review = Assert.IsInstanceOfType<ReviewEvent>(forgotten.History[^1]);
        Assert.AreEqual(("J7", true, 0), (review.StepLabel, review.Forgotten, review.DaysLate));
        Assert.AreEqual(sheet.Counters, forgotten.Counters, "un oubli n'ouvre pas de cycle");
        Assert.IsNull(forgotten.Verify());

        // Retrouvée, la fiche reprend sa montée : J3 puis J7.
        var again = forgotten.ReviewedOnTime(Rules);
        Assert.AreEqual(("J7", D(2026, 10, 30)), (again.Cycle!.StepLabel, again.DueDate()));
    }

    [TestMethod]
    public void Forgetting_at_J1_keeps_J1_for_the_next_day()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9));

        var forgotten = sheet.ValidateReview(At(D(2026, 10, 12)), null, Rules, ReviewRecall.Forgotten).Ok();

        Assert.AreEqual(("J1", D(2026, 10, 13)), (forgotten.Cycle!.StepLabel, forgotten.DueDate()));
        Assert.AreEqual(2, Assert.IsInstanceOfType<ReviewEvent>(forgotten.History[^1]).DaysLate);
    }

    [TestMethod]
    public void Forgetting_the_last_step_does_not_archive()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9));
        while (sheet.Cycle!.Step < sheet.Cycle.Sequence.LastStep)
        {
            sheet = sheet.ReviewedOnTime(Rules);
        }
        Assert.AreEqual("J30", sheet.Cycle.StepLabel);
        var due = sheet.DueDate();

        var forgotten = sheet.ValidateReview(At(due), null, Rules, ReviewRecall.Forgotten).Ok();

        Assert.AreEqual(TrackingStatus.Active, forgotten.Status);
        Assert.AreEqual(("J14", due.AddDays(14)), (forgotten.Cycle!.StepLabel, forgotten.DueDate()));
        Assert.AreEqual(0, forgotten.Counters.Completed);
    }

    [TestMethod]
    public void A_forgotten_review_obeys_the_same_refusals()
    {
        var sheet = SheetCreatedOn(D(2026, 10, 9));

        Assert.AreEqual(TrackingErrorCode.AlreadyReviewed, sheet.ValidateReview(At(D(2026, 10, 9)), null, Rules, ReviewRecall.Forgotten).Refused().Code);
        var archived = sheet.ArchiveManually(At(D(2026, 10, 10)), Rules).Ok();
        Assert.AreEqual(TrackingErrorCode.NotActive, archived.ValidateReview(At(D(2026, 10, 11)), null, Rules, ReviewRecall.Forgotten).Refused().Code);
    }
}
