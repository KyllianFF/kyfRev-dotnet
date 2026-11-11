using KyfRev.Domain.Reviews;
using static KyfRev.Domain.Tests.Support.Dates;

namespace KyfRev.Domain.Tests;

/// <summary>Le lissage de la prochaine échéance, en option.</summary>
[TestClass]
public sealed class DueSmoothingTests
{
    private static readonly ReminderRules Rules = ReminderRules.Default;

    [TestMethod]
    [DataRow(1, 0)]
    [DataRow(3, 0)]
    [DataRow(6, 0)]
    [DataRow(7, 1)]
    [DataRow(13, 1)]
    [DataRow(14, 2)]
    [DataRow(29, 2)]
    [DataRow(30, 3)]
    [DataRow(90, 3)]
    public void Short_steps_do_not_move_and_long_ones_move_a_little(int interval, int window) =>
        Assert.AreEqual(window, DueSmoothing.Window(interval));

    [TestMethod]
    public void The_least_loaded_day_wins_then_the_closest_then_the_earliest()
    {
        var exact = D(2026, 11, 20);
        var load = new Dictionary<DateOnly, int> { [exact] = 4, [exact.AddDays(-1)] = 2, [exact.AddDays(1)] = 2, [exact.AddDays(2)] = 0, [exact.AddDays(-2)] = 1 };
        int Load(DateOnly day) => load.GetValueOrDefault(day, 9);

        Assert.AreEqual(exact, DueSmoothing.Choose(exact, 3, Load), "une étape courte reste exacte");
        Assert.AreEqual(exact.AddDays(-1), DueSmoothing.Choose(exact, 7, Load), "à charge égale, le plus tôt");
        Assert.AreEqual(exact.AddDays(2), DueSmoothing.Choose(exact, 14, Load), "le moins chargé de la fenêtre");
        Assert.AreEqual(exact, DueSmoothing.Choose(exact, 30, _ => 0), "rien de chargé : la date exacte");
    }

    [TestMethod]
    public void A_validation_uses_the_load_only_when_it_is_given()
    {
        // J1 le 10, J3 le 13, J7 le 20 : J14 tomberait exactement le 3 novembre, jour déjà chargé.
        var sheet = SheetCreatedOn(D(2026, 10, 9)).ReviewedOnTime(Rules).ReviewedOnTime(Rules);
        var exact = D(2026, 11, 3);
        int Load(DateOnly day) => day == exact ? 5 : day == exact.AddDays(1) ? 1 : 3;

        Assert.AreEqual(exact, sheet.ValidateReview(At(D(2026, 10, 20)), null, Rules).Ok().DueDate(), "sans charge, l'échéance est exacte");

        var smoothed = sheet.ValidateReview(At(D(2026, 10, 20)), null, Rules, load: Load).Ok();
        Assert.AreEqual(("J14", exact.AddDays(1)), (smoothed.Cycle!.StepLabel, smoothed.DueDate()));
        Assert.IsNull(smoothed.Verify());

        // J7 n'a qu'un jour de fenêtre, et J3 aucun.
        var j3 = SheetCreatedOn(D(2026, 10, 9)).ReviewedOnTime(Rules);
        Assert.AreEqual(D(2026, 10, 20), j3.ValidateReview(At(D(2026, 10, 13)), null, Rules, load: day => day == D(2026, 10, 20) ? 0 : 2).Ok().DueDate());
        Assert.AreEqual(D(2026, 10, 13), SheetCreatedOn(D(2026, 10, 9)).ValidateReview(At(D(2026, 10, 10)), null, Rules, load: day => day == D(2026, 10, 13) ? 9 : 0).Ok().DueDate());
    }
}
