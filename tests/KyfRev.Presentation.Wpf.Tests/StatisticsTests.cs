using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Statistiques » : ses textes, ceux de la 0.x, son calendrier, et sa page à toutes les largeurs.</summary>
[TestClass]
public sealed class StatisticsTests
{
    private static readonly DateOnly Today = new(2026, 10, 13);
    private static readonly DateOnly From = new(2025, 10, 13);

    [TestMethod]
    public void The_page_needs_its_use_cases()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new StatisticsPageViewModel(null!));
        using var vaults = new TestVaults(AppMode.Development);
        Assert.ThrowsExactly<ArgumentNullException>(() => Page(vaults).Show(null!));
    }

    [TestMethod]
    public void Nothing_yet_says_so()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults);
        Assert.AreEqual("Statistiques", StatisticsPageViewModel.Title);
        Assert.IsFalse(page.IsLoaded);

        page.Show(Empty());

        Assert.IsTrue(page.IsLoaded);
        Assert.AreEqual(StatisticsText.Summary, page.Summary);
        Assert.AreEqual("—", page.Tiles[0].Value, "à l'heure, sans révision");
        Assert.AreEqual("Révision validée", page.Tiles[1].Label);
        Assert.AreEqual("sur 52 semaines", page.Tiles[1].Details.Single());
        Assert.AreEqual("Jour actif", page.Tiles[2].Label);
        Assert.AreEqual("0 / 366", page.Tiles[2].Value);
        Assert.AreEqual("0 jour", page.Tiles[3].Value);
        Assert.AreEqual("une révision aujourd'hui lance la série", page.Tiles[3].Details.Single());
        Assert.IsTrue(page.HasNoMonth);
        Assert.IsTrue(page.HasNoFolder);
        Assert.AreEqual("Calendrier d'activité : 0 jour actif sur 52 semaines", page.CalendarName);
    }

    [TestMethod]
    public void The_key_figures_read_like_those_of_0x()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults);

        page.Show(Busy());

        StringAssert.EndsWith(page.Summary, "depuis le ven. 2 oct.", StringComparison.Ordinal);
        var onTime = page.Tiles[0];
        Assert.AreEqual("À l'heure", onTime.Label);
        Assert.AreEqual("83 %", onTime.Value);
        Assert.AreEqual("8 le jour prévu · 2 en avance · 2 en retard", onTime.Details[0]);
        Assert.AreEqual("retard moyen 2,5 j, au plus 4 jours", onTime.Details[1]);
        var reviews = page.Tiles[1];
        Assert.AreEqual("Révisions validées", reviews.Label);
        Assert.AreEqual("12", reviews.Value);
        Assert.AreEqual("sur 52 semaines, et 2 rappels traités", reviews.Details[0]);
        Assert.AreEqual("meilleur jour : 5, ven. 2 oct.", reviews.Details[1]);
        var active = page.Tiles[2];
        Assert.AreEqual("Jours actifs", active.Label);
        Assert.AreEqual("3 / 366", active.Value[..7]);
        Assert.AreEqual("7 derniers jours : 2 révisions · 30 derniers : 12", active.Details[0]);
        var streak = page.Tiles[3];
        Assert.AreEqual("Série en cours", streak.Label);
        Assert.AreEqual("2 jours", streak.Value);
        Assert.AreEqual("record : 3 jours, jusqu'au sam. 3 oct.", streak.Details[0]);
        StringAssert.StartsWith(streak.Spoken, "Série en cours, 2 jours, record", StringComparison.Ordinal);
        Assert.AreEqual("Calendrier d'activité : 3 jours actifs sur 52 semaines", page.CalendarName);
    }

    [TestMethod]
    public void The_details_skip_the_months_before_the_first_activity()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults);

        page.Show(Busy());

        Assert.HasCount(2, page.Months);
        Assert.AreEqual("septembre 2026", page.Months[0].Label);
        Assert.AreEqual(0.5, page.Months[0].Share);
        Assert.AreEqual(1.0, page.Months[1].Share);
        Assert.AreEqual(new GridLength(0.5, GridUnitType.Star), page.Months[0].Filled);
        Assert.AreEqual(new GridLength(0.5, GridUnitType.Star), page.Months[0].Rest);
        Assert.AreEqual("septembre 2026, 6 révisions, 2 jours actifs", page.Months[0].Spoken);
        var sciences = page.Folders[0];
        Assert.AreEqual("Sciences › Biologie", sciences.Folder);
        Assert.AreEqual("83 %", sciences.OnTime);
        Assert.AreEqual("2,5 j", sciences.AverageDelay);
        Assert.AreEqual("Sciences › Biologie, 2 fiches, 12 révisions, à l'heure 83 %, retard moyen 2,5 j", sciences.Spoken);
        var root = page.Folders[1];
        Assert.AreEqual("Sans dossier", root.Folder);
        Assert.AreEqual("—", root.OnTime);
        Assert.AreEqual("—", root.AverageDelay);
    }

    [TestMethod]
    public void A_percentage_rounds_halves_up_like_0x()
    {
        Assert.AreEqual("—", StatisticsPageViewModel.Percent(0, 0));
        Assert.AreEqual("13 %", StatisticsPageViewModel.Percent(1, 8), "12,5 % s'arrondit au-dessus");
        Assert.AreEqual("100 %", StatisticsPageViewModel.Percent(3, 3));
    }

    [TestMethod]
    public void The_level_of_a_day_goes_up_to_the_busiest_day()
    {
        Assert.AreEqual(0, StatisticsPageViewModel.LevelOf(0, 8));
        Assert.AreEqual(0, StatisticsPageViewModel.LevelOf(1, 0));
        Assert.AreEqual(1, StatisticsPageViewModel.LevelOf(1, 8));
        Assert.AreEqual(1, StatisticsPageViewModel.LevelOf(2, 8));
        Assert.AreEqual(2, StatisticsPageViewModel.LevelOf(3, 8));
        Assert.AreEqual(3, StatisticsPageViewModel.LevelOf(6, 8));
        Assert.AreEqual(4, StatisticsPageViewModel.LevelOf(8, 8));
    }

    [TestMethod]
    public void The_calendar_goes_from_monday_to_the_end_of_this_week()
    {
        var weeks = StatisticsPageViewModel.WeeksOf(From, Today, [new ActivityDay(Today, 2, 1, 1), new ActivityDay(new DateOnly(2026, 10, 2), 1, 0, 0)]);

        Assert.HasCount(53, weeks);
        Assert.IsTrue(weeks.All(week => week.Cells.Count == 7));
        Assert.AreEqual(From, weeks[0].Cells[0].Day);
        Assert.AreEqual(DayOfWeek.Monday, weeks[0].Cells[0].Day.DayOfWeek);
        var today = weeks[^1].Cells[1];
        Assert.AreEqual(Today, today.Day);
        Assert.IsTrue(today.IsToday);
        Assert.AreEqual(4, today.Level);
        Assert.AreEqual(4, today.Total);
        Assert.AreEqual("Mardi 13 octobre 2026 : 3 révisions (2 fiches, 1 passage) et 1 rappel de consolidation", today.Description);
        var future = weeks[^1].Cells[2];
        Assert.IsTrue(future.IsFuture);
        Assert.AreEqual("Mercredi 14 octobre 2026", future.Description);
        Assert.AreEqual("Lundi 13 octobre 2025 : rien", weeks[0].Cells[0].Description);
    }

    [TestMethod]
    public void A_day_says_what_was_done()
    {
        Assert.AreEqual("1 révision (1 fiche)", StatisticsPageViewModel.DetailOf(new ActivityDay(Today, 1, 0, 0)));
        Assert.AreEqual("3 révisions (3 passages)", StatisticsPageViewModel.DetailOf(new ActivityDay(Today, 0, 3, 0)));
        Assert.AreEqual("2 rappels de consolidation", StatisticsPageViewModel.DetailOf(new ActivityDay(Today, 0, 0, 2)));
    }

    [TestMethod]
    public void A_month_label_too_close_to_the_next_one_is_left_out()
    {
        var labels = StatisticsPageViewModel.MonthLabelsOf(StatisticsPageViewModel.WeeksOf(new DateOnly(2026, 8, 24), Today, []));

        // La semaine du 24 août n'ouvre août que deux semaines avant septembre : son libellé est tu.
        Assert.AreEqual(string.Empty, labels[0]);
        Assert.AreEqual("sept", labels[2]);
        Assert.AreEqual("oct", labels[6]);
        Assert.HasCount(8, labels);
    }

    [TestMethod]
    public void The_layout_follows_the_width_and_never_exceeds_it()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults);
        page.Show(Busy());

        Assert.AreEqual(4, page.TileColumns);
        Assert.IsTrue(page.IsSideBySide);
        page.PageWidth = StatisticsPageViewModel.SideBySideMinimum - 1;
        Assert.IsFalse(page.IsSideBySide);
        Assert.AreEqual(4, page.TileColumns);
        page.PageWidth = StatisticsPageViewModel.FourTilesMinimum - 1;
        Assert.AreEqual(2, page.TileColumns);
        page.PageWidth = StatisticsPageViewModel.TwoTilesMinimum - 1;
        Assert.AreEqual(1, page.TileColumns);

        Assert.AreEqual(53, page.WeekCount);
        page.CalendarWidth = 53 * 12;
        Assert.AreEqual(10.0, page.CellSize);
        Assert.AreEqual(12.0, page.RowHeight);
        page.CalendarWidth = 100;
        Assert.AreEqual(6.0, page.CellSize, "une case reste visible");
        page.CalendarWidth = 53 * 100;
        Assert.AreEqual(22.0, page.CellSize, "une case ne grandit pas sans fin");
    }

    [TestMethod]
    public async Task A_refused_reading_says_why_and_a_new_reading_clears_it()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults);

        await page.RefreshCommand.ExecuteAsync(null);

        Assert.IsTrue(page.HasError);
        Assert.AreEqual(VaultHost.NoVaultOpen, page.Error);
        Assert.IsFalse(page.IsLoaded);
        Assert.IsFalse(page.IsLoading);

        page.Show(Empty());
        Assert.IsFalse(page.HasError);
    }

    [STATestMethod]
    public void The_page_shows_everything_without_horizontal_scrolling()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults);
        page.Show(Busy());
        var test = new TestShell();
        var (window, frame) = Show(test, page, width: 1400);
        try
        {
            Assert.AreEqual("Statistiques", Named<TextBlock>(window, "Heading").Text);
            Assert.AreEqual(Visibility.Visible, Named<FrameworkElement>(window, "Figures").Visibility);
            Assert.AreEqual(Visibility.Collapsed, Named<FrameworkElement>(window, "Loading").Visibility);
            Assert.AreEqual(1, Grid.GetColumn(Named<FrameworkElement>(window, "FoldersCard")));
            var scroller = Named<ScrollViewer>(window, "Scroller");
            Assert.IsLessThanOrEqualTo(scroller.ViewportWidth, scroller.ExtentWidth);
            var calendar = Named<ActivityCalendar>(window, "Calendar");
            Assert.IsGreaterThan(0.0, page.CellSize);
            Assert.AreEqual(page.CalendarWidth, calendar.ActualWidth, 0.5);
            Assert.AreEqual(calendar.ActualHeight, page.RowHeight * 7, 0.5, "le calendrier n'a pas de vide sous ses cases");

            frame.Width = 800;
            TestShell.Flush();
            Assert.IsLessThanOrEqualTo(scroller.ViewportWidth, scroller.ExtentWidth, "pas de défilement horizontal, même à l'étroit");
            Assert.AreEqual(0, Grid.GetColumn(Named<FrameworkElement>(window, "FoldersCard")));
            Assert.AreEqual(1, Grid.GetRow(Named<FrameworkElement>(window, "FoldersCard")));
            Assert.IsLessThanOrEqualTo(scroller.ViewportWidth, calendar.ActualWidth);
            Assert.AreEqual(calendar.ActualHeight, page.RowHeight * 7, 0.5, "à l'étroit non plus");
            Assert.IsLessThanOrEqualTo(calendar.ActualHeight, Named<FrameworkElement>(window, "WeekDays").ActualHeight + 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_screen_reader_gets_one_summary_for_the_calendar()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults);
        page.Show(Busy());
        var test = new TestShell();
        var (window, _) = Show(test, page, width: 1100);
        try
        {
            var calendar = UIElementAutomationPeer.CreatePeerForElement(Named<ActivityCalendar>(window, "Calendar"));
            Assert.AreEqual(AutomationControlType.Image, calendar.GetAutomationControlType());
            Assert.AreEqual(page.CalendarName, calendar.GetName());
            Assert.IsNull(calendar.GetChildren());
            var months = UIElementAutomationPeer.CreatePeerForElement(Named<DecorativeItems>(window, "MonthsHeader"));
            Assert.IsFalse(months.IsControlElement());
            Assert.IsFalse(months.IsContentElement());
            Assert.IsNull(months.GetChildren());
        }
        finally
        {
            window.Close();
        }
    }

    private static StatisticsPageViewModel Page(TestVaults vaults) => new(vaults.App.Reminders);

    private static Statistics Empty() => new(
        Today, From, [], null, 0, 0, 0, 0, 0, 0, 0, null, 0, 0, 0, 0, 0, null,
        [.. Enumerable.Range(0, 12).Select(n => new MonthStatistics(new DateOnly(2025, 11, 1).AddMonths(n).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), 0, 0))],
        []);

    private static Statistics Busy() => Empty() with
    {
        Activity =
        [
            new ActivityDay(new DateOnly(2026, 10, 2), 3, 1, 1),
            new ActivityDay(new DateOnly(2026, 10, 3), 2, 0, 1),
            new ActivityDay(new DateOnly(2026, 10, 12), 2, 0, 0),
        ],
        FirstActivity = new DateOnly(2026, 10, 2),
        Reviews = 12,
        Reminders = 2,
        ActiveDays = 3,
        LastSevenDays = 2,
        LastThirtyDays = 12,
        CurrentStreak = 2,
        LongestStreak = 3,
        LongestStreakEnd = new DateOnly(2026, 10, 3),
        OnTime = 8,
        Early = 2,
        Late = 2,
        AverageDelay = 2.5,
        LongestDelay = 4,
        BestDay = new ActivityDay(new DateOnly(2026, 10, 2), 3, 1, 1),
        ByMonth =
        [
            .. Enumerable.Range(0, 10).Select(n => new MonthStatistics(new DateOnly(2025, 11, 1).AddMonths(n).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), 0, 0)),
            new MonthStatistics("2026-09", 6, 2),
            new MonthStatistics("2026-10", 12, 3),
        ],
        ByFolder =
        [
            new FolderStatistics("Sciences/Biologie", 2, 12, 10, 2, 2.5),
            new FolderStatistics(string.Empty, 1, 0, 0, 0, 0),
        ],
    };

    /// <summary>La page dans un cadre de largeur fixe : une fenêtre ne dépasse pas l'écran, et celui de la CI est petit.</summary>
    private static (Window Window, Border Frame) Show(TestShell test, StatisticsPageViewModel page, double width)
    {
        var frame = new Border { Width = width, Height = 800, Child = new StatisticsPage { DataContext = page } };
        var window = new Window { Content = new Canvas { Children = { frame } }, ShowActivated = false, Left = -10000, Top = -10000, Width = 800, Height = 600 };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        return (window, frame);
    }

    private static T Named<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        var found = Find<T>(root, name);
        Assert.IsNotNull(found, name);
        return found;
    }

    private static T? Find<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T element && element.Name == name)
            {
                return element;
            }
            if (Find<T>(child, name) is { } nested)
            {
                return nested;
            }
        }
        return null;
    }
}
