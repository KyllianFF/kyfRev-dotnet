using System.Globalization;
using KyfRev.Domain.Calendar;
using KyfRev.Domain.Randomness;
using KyfRev.Domain.Reviews;
using KyfRev.Domain.Tests.Reference;
using KyfRev.Domain.Tests.Support;

namespace KyfRev.Domain.Tests;

/// <summary>
/// Comparaison avec le cœur Rust de kyfRev 0.x : les mêmes entrées, régénérées à l'identique,
/// doivent donner les mêmes résultats que ceux enregistrés dans <c>fixtures/reference</c>.
/// </summary>
[TestClass]
public sealed class DifferentialTests
{
    [TestMethod]
    public void Ten_thousand_operation_sequences_match_the_rust_core_step_by_step()
    {
        var expected = ReferenceCases.Lines("tracking.txt");
        Assert.HasCount(10_000, expected);

        var mismatches = Enumerable.Range(0, expected.Count)
            .Where(n => TrackingTranscript.For((ulong)n).ReferenceLine != expected[n])
            .ToList();

        Assert.IsEmpty(
            mismatches,
            $"{mismatches.Count} suite(s) divergente(s), dont : {string.Join(", ", mismatches.Take(10))}. "
            + "Comparer avec « cargo run --release --manifest-path tools/reference-vectors/Cargo.toml -- --transcript n ».");
    }

    [TestMethod]
    public void Daily_reminder_draws_match_the_rust_core()
    {
        var expected = ReferenceCases.Lines("draws.txt");
        Assert.HasCount(3_000, expected);

        for (var k = 0; k < expected.Count; k++)
        {
            var random = new SplitMix(ReferenceCases.DrawBase + (ulong)k);
            var today = Dates.D(2027, 1, 1).AddDays(random.Below(400));
            var count = random.Below(40);
            var candidates = new List<DrawCandidate>();
            for (var i = 0; i < count; i++)
            {
                var id = string.Create(CultureInfo.InvariantCulture, $"f{random.Below(30)}");
                candidates.Add(new DrawCandidate(id, today.AddDays(random.Below(150) - 120)));
            }
            var pending = random.Below(5);
            var rules = ReferenceCases.Rules(random.Below(3));
            var seed = random.Next();

            var draw = ReminderDraw.Draw(today, candidates, pending, rules, seed).Ok();

            var actual = string.Create(CultureInfo.InvariantCulture, $"{string.Join(',', draw.Selection)}|{draw.UnservedPriority}");
            Assert.AreEqual(expected[k], actual, $"tirage {k}");
        }
    }

    [TestMethod]
    public void ChaCha8_streams_match_rand_chacha_bit_for_bit()
    {
        var expected = ReferenceCases.Lines("chacha8.txt");
        Assert.HasCount(16, expected);

        for (var s = 0; s < expected.Count; s++)
        {
            var seed = new SplitMix(ReferenceCases.ChaChaBase + (ulong)s).Next();
            var random = ChaCha8Random.FromSeed(seed);
            var outputs = Enumerable.Range(0, 70).Select(_ => random.NextUInt64().ToString(CultureInfo.InvariantCulture));

            Assert.AreEqual(expected[s], string.Create(CultureInfo.InvariantCulture, $"{seed}:{string.Join(' ', outputs)}"));
        }
    }

    [TestMethod]
    public void Daily_seeds_match_the_rust_core()
    {
        var expected = ReferenceCases.Lines("seeds.txt");
        Assert.HasCount(300, expected);

        for (var k = 0; k < expected.Count; k++)
        {
            var random = new SplitMix(ReferenceCases.SeedBase + (ulong)k);
            var id = "v_" + new string([.. Enumerable.Range(0, 26).Select(_ => ReferenceCases.Crockford[random.Below(32)])]);
            var day = Dates.D(2000, 1, 1).AddDays(random.Below(20_000));

            Assert.AreEqual(expected[k], DailySeed.For(id, day).ToString(CultureInfo.InvariantCulture), $"graine {k}");
        }
    }

    [TestMethod]
    public void Calendar_months_and_day_differences_match_the_rust_core()
    {
        var expected = ReferenceCases.Lines("calendar.txt");
        Assert.HasCount(5_000, expected);

        for (var k = 0; k < expected.Count; k++)
        {
            var random = new SplitMix(ReferenceCases.CalendarBase + (ulong)k);
            var day = Dates.D(1990, 1, 1).AddDays(random.Below(40_000));
            var months = random.Below(40);
            var other = Dates.D(1990, 1, 1).AddDays(random.Below(40_000));

            Assert.IsTrue(CivilCalendar.TryAddMonths(day, months, out var shifted));
            var actual = string.Create(CultureInfo.InvariantCulture, $"{RustJson.Day(shifted)}|{CivilCalendar.DaysBetween(day, other)}");
            Assert.AreEqual(expected[k], actual, $"cas {k} ({RustJson.Day(day)} + {months} mois)");
        }
    }
}
