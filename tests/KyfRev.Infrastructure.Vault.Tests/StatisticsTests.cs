using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>Les statistiques de régularité, comme celles de la 0.x (<c>statistiques.rs</c>).</summary>
public abstract class StatisticsTests(StorageKind storage)
{
    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose"], 5, "sha256:aa");

    private static DateOnly October(int day) => new(2026, 10, day);

    private static Moment On(DateOnly day) => new(day, new DateTimeOffset(day, new TimeOnly(12, 0), TimeSpan.Zero));

    [TestMethod]
    public void An_empty_vault_has_nothing_to_count()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, On(October(1)));

        var statistics = vault.Statistics(October(13));

        Assert.AreEqual(October(13), statistics.Day);
        Assert.AreEqual(new DateOnly(2025, 10, 13), statistics.From, "le lundi d'il y a 52 semaines");
        Assert.IsEmpty(statistics.Activity);
        Assert.IsNull(statistics.FirstActivity);
        Assert.AreEqual(0, statistics.Reviews);
        Assert.AreEqual(0, statistics.ActiveDays);
        Assert.AreEqual(0, statistics.CurrentStreak);
        Assert.AreEqual(0, statistics.LongestStreak);
        Assert.IsNull(statistics.LongestStreakEnd);
        Assert.AreEqual(0.0, statistics.AverageDelay);
        Assert.IsNull(statistics.BestDay);
        Assert.IsEmpty(statistics.ByFolder);
        Assert.HasCount(12, statistics.ByMonth);
        Assert.AreEqual("2025-11", statistics.ByMonth[0].Month);
        Assert.AreEqual("2026-10", statistics.ByMonth[^1].Month);
    }

    [TestMethod]
    public void Creating_a_sheet_is_not_a_review()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, On(October(1)));
        vault.CreateSheet("Biologie", "Mitose", "La prophase.\n", On(October(1)));

        var statistics = vault.Statistics(October(1));

        Assert.IsNull(statistics.FirstActivity);
        Assert.AreEqual(0, statistics.Reviews);
        Assert.AreEqual(1, statistics.ByFolder.Single().Sheets);
    }

    [TestMethod]
    public void Reviews_count_on_the_day_they_are_validated_with_their_delay()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, On(October(1)));
        var sheet = vault.CreateSheet("Biologie", "Mitose", "La prophase.\n", On(October(1)));
        var passage = vault.CreateAnnotation(sheet, Passage, null, On(October(1)));
        vault.ValidateReview(ItemKind.Sheet, sheet, On(October(2)));
        vault.ValidateReview(ItemKind.Passage, passage, On(October(2)));
        vault.ValidateReview(ItemKind.Sheet, sheet, On(October(4)));
        var delay = October(13).DayNumber - vault.ReviewStateOf(ItemKind.Sheet, sheet, October(13)).DueDate!.Value.DayNumber;
        vault.ValidateReview(ItemKind.Sheet, sheet, On(October(13)));

        var statistics = vault.Statistics(October(13));

        Assert.AreEqual(October(2), statistics.FirstActivity);
        Assert.AreEqual(4, statistics.Reviews);
        Assert.AreEqual(2, statistics.OnTime);
        Assert.AreEqual(1, statistics.Early);
        Assert.AreEqual(1, statistics.Late);
        Assert.AreEqual((double)delay, statistics.AverageDelay);
        Assert.AreEqual(delay, statistics.LongestDelay);
        Assert.AreEqual(3, statistics.ActiveDays);
        var first = statistics.Activity[0];
        Assert.AreEqual(new ActivityDay(October(2), 1, 1, 0), first);
        Assert.AreEqual(first, statistics.BestDay);
        Assert.AreEqual(1, statistics.LastSevenDays);
        Assert.AreEqual(4, statistics.LastThirtyDays);
        Assert.AreEqual(new MonthStatistics("2026-10", 4, 3), statistics.ByMonth[^1]);
        var biology = statistics.ByFolder.Single();
        Assert.AreEqual("Biologie", biology.Folder);
        Assert.AreEqual(1, biology.Sheets);
        Assert.AreEqual(4, biology.Reviews);
        Assert.AreEqual(3, biology.OnTime, "à l'heure : le jour prévu ou avant");
        Assert.AreEqual(1, biology.Late);
        Assert.AreEqual((double)delay, biology.AverageDelay);
    }

    [TestMethod]
    public void A_streak_runs_until_yesterday_because_the_day_is_not_over()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, On(October(1)));
        var sheet = vault.CreateSheet(null, "Mitose", "La prophase.\n", On(October(1)));
        vault.ValidateReview(ItemKind.Sheet, sheet, On(October(2)));
        vault.ValidateReview(ItemKind.Sheet, sheet, On(October(3)));

        Assert.AreEqual(2, vault.Statistics(October(3)).CurrentStreak);
        Assert.AreEqual(2, vault.Statistics(October(4)).CurrentStreak);
        Assert.AreEqual(0, vault.Statistics(October(5)).CurrentStreak);
        var later = vault.Statistics(October(5));
        Assert.AreEqual(2, later.LongestStreak);
        Assert.AreEqual(October(3), later.LongestStreakEnd);
        Assert.AreEqual(October(3), later.BestDay?.Day, "à égalité, le jour le plus récent");
    }

    [TestMethod]
    public void The_calendar_only_counts_its_52_weeks_but_the_records_stay()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, On(October(1)));
        var sheet = vault.CreateSheet(null, "Mitose", "La prophase.\n", On(October(1)));
        vault.ValidateReview(ItemKind.Sheet, sheet, On(October(2)));

        var statistics = vault.Statistics(new DateOnly(2028, 10, 2));

        Assert.AreEqual(0, statistics.Reviews);
        Assert.IsEmpty(statistics.Activity);
        Assert.IsNull(statistics.BestDay);
        Assert.AreEqual(October(2), statistics.FirstActivity);
        Assert.AreEqual(1, statistics.LongestStreak);
        Assert.AreEqual(0, statistics.ByFolder.Single().Reviews);
    }

    [TestMethod]
    public void A_handled_reminder_counts_apart_from_the_reviews()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, On(October(1)));
        var sheet = vault.CreateSheet(null, "Mitose", "La prophase.\n", On(October(1)));
        vault.ArchiveSheet(sheet, On(October(2)));
        var day = new DateOnly(2027, 2, 1);
        vault.PrepareDay(On(day));
        vault.HandleReminder(sheet, ReminderOutcome.Known, On(day));

        var statistics = vault.Statistics(day);

        Assert.AreEqual(1, statistics.Reminders);
        Assert.AreEqual(0, statistics.Reviews);
        Assert.AreEqual(new ActivityDay(day, 0, 0, 1), statistics.Activity.Single());
        Assert.AreEqual(1, statistics.CurrentStreak);
        Assert.AreEqual(0, statistics.ByMonth[^1].Reviews);
        Assert.AreEqual(1, statistics.ByMonth[^1].ActiveDays);
    }

    [TestMethod]
    public void Folders_go_from_the_most_reviewed_then_by_name()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, On(October(1)));
        var reviewed = vault.CreateSheet("Zoologie", "Le chat", "Le chat.\n", On(October(1)));
        vault.CreateSheet("histoire", "Rome", "Rome.\n", On(October(1)));
        vault.CreateSheet("Biologie", "Mitose", "La prophase.\n", On(October(1)));
        vault.CreateSheet(null, "À trier", "Rien.\n", On(October(1)));
        vault.ValidateReview(ItemKind.Sheet, reviewed, On(October(2)));

        var folders = vault.Statistics(October(2)).ByFolder;

        CollectionAssert.AreEqual(new[] { "Zoologie", string.Empty, "Biologie", "histoire" }, folders.Select(row => row.Folder).ToArray());
        Assert.AreEqual(0.0, folders[1].AverageDelay);
    }
}

/// <summary>Sur le disque tel quel.</summary>
[TestClass]
public sealed class StatisticsOnDiskTests() : StatisticsTests(StorageKind.Disk);

/// <summary>Sur un coffre chiffré.</summary>
[TestClass]
public sealed class StatisticsEncryptedTests() : StatisticsTests(StorageKind.Encrypted);
