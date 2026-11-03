using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Un chiffre clé : son libellé, sa valeur, et ce qui l'explique.</summary>
public sealed record StatisticsTile(string Label, string Value, IReadOnlyList<string> Details)
{
    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public string Spoken => string.Join(", ", new[] { Label, Value }.Concat(Details));
}

/// <summary>Un jour du calendrier d'activité.</summary>
/// <param name="Day">Le jour.</param>
/// <param name="Total">Révisions et rappels du jour.</param>
/// <param name="Level">De 0 (rien) à 4 (les jours les plus chargés du calendrier).</param>
/// <param name="IsFuture">Après aujourd'hui : la case reste vide.</param>
/// <param name="IsToday">Aujourd'hui.</param>
/// <param name="Description">« Mardi 13 octobre 2026 : 3 révisions (2 fiches, 1 passage) ».</param>
public sealed record CalendarCell(DateOnly Day, int Total, int Level, bool IsFuture, bool IsToday, string Description);

/// <summary>Une semaine du calendrier d'activité, du lundi au dimanche.</summary>
public sealed record CalendarWeek(IReadOnlyList<CalendarCell> Cells);

/// <summary>Une ligne du détail par mois.</summary>
/// <param name="Label">« octobre 2026 ».</param>
/// <param name="Reviews">Révisions validées.</param>
/// <param name="ActiveDays">Jours actifs.</param>
/// <param name="Share">Part du mois le plus chargé, de 0 à 1.</param>
public sealed record MonthRow(string Label, int Reviews, int ActiveDays, double Share)
{
    /// <summary>La largeur de la barre, en étoiles.</summary>
    public System.Windows.GridLength Filled => new(Share, System.Windows.GridUnitType.Star);

    /// <summary>Ce qui reste à côté de la barre, en étoiles.</summary>
    public System.Windows.GridLength Rest => new(1 - Share, System.Windows.GridUnitType.Star);

    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public string Spoken => StatisticsText.MonthSpokenOf(
        Label,
        Dates.Plural(Reviews, StatisticsText.ReviewOneOf, StatisticsText.ReviewManyOf),
        Dates.Plural(ActiveDays, StatisticsText.ActiveDayOneOf, StatisticsText.ActiveDayManyOf));
}

/// <summary>Une ligne du détail par dossier.</summary>
/// <param name="Folder">« Sciences › Biologie », ou « Sans dossier ».</param>
/// <param name="Sheets">Fiches.</param>
/// <param name="Reviews">Révisions validées.</param>
/// <param name="OnTime">« 83 % », ou « — » sans révision.</param>
/// <param name="AverageDelay">« 2,5 j », ou « — » sans retard.</param>
public sealed record FolderRow(string Folder, int Sheets, int Reviews, string OnTime, string AverageDelay)
{
    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public string Spoken => StatisticsText.FolderSpokenOf(
        Folder,
        Dates.Plural(Sheets, StatisticsText.SheetOneOf, StatisticsText.SheetManyOf),
        Dates.Plural(Reviews, StatisticsText.ReviewOneOf, StatisticsText.ReviewManyOf),
        OnTime,
        AverageDelay);
}

/// <summary>Vue « Statistiques », comme dans la 0.x : la régularité, sans filtre ni période à choisir.</summary>
public sealed partial class StatisticsPageViewModel : ObservableObject, INavigationAware
{
    /// <summary>Largeur sous laquelle les deux détails passent l'un sous l'autre.</summary>
    public const double SideBySideMinimum = 1000;

    /// <summary>Largeur sous laquelle les chiffres clés passent sur deux colonnes.</summary>
    public const double FourTilesMinimum = 900;

    /// <summary>Largeur sous laquelle les chiffres clés passent sur une colonne.</summary>
    public const double TwoTilesMinimum = 460;

    private const double CellMinimum = 6;
    private const double CellMaximum = 22;
    private const double CellGap = 2;

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly ReminderUseCases _reminders;
    private bool _shown;

    /// <summary>La vue, sur les cas d'usage qui rendent les statistiques.</summary>
    public StatisticsPageViewModel(ReminderUseCases reminders)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        _reminders = reminders;
    }

    /// <summary>« Statistiques ».</summary>
    public static string Title => StatisticsText.Title;

    /// <summary>La phrase d'en-tête : ce qui est compté, et depuis quand.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = StatisticsText.Summary;

    /// <summary>Les quatre chiffres clés.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<StatisticsTile> Tiles { get; private set; } = [];

    /// <summary>Les semaines du calendrier.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekCount), nameof(CellSize), nameof(RowHeight))]
    public partial IReadOnlyList<CalendarWeek> Weeks { get; private set; } = [];

    /// <summary>Au-dessus de chaque semaine, le mois qu'elle ouvre, ou rien.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> MonthLabels { get; private set; } = [];

    /// <summary>Ce que le lecteur d'écran dit du calendrier.</summary>
    [ObservableProperty]
    public partial string CalendarName { get; private set; } = string.Empty;

    /// <summary>Les mois, sans ceux d'avant la première activité.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoMonth), nameof(HasMonths))]
    public partial IReadOnlyList<MonthRow> Months { get; private set; } = [];

    /// <summary>Les dossiers, du plus révisé au moins révisé.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoFolder), nameof(HasFolders))]
    public partial IReadOnlyList<FolderRow> Folders { get; private set; } = [];

    /// <summary>Les statistiques sont là.</summary>
    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    /// <summary>Première lecture en cours.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Refus de la dernière lecture.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>La lecture a été refusée.</summary>
    public bool HasError => Error is not null;

    /// <summary>Largeur de la page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TileColumns), nameof(IsSideBySide))]
    public partial double PageWidth { get; set; } = double.PositiveInfinity;

    /// <summary>Largeur disponible pour les semaines du calendrier.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CellSize), nameof(RowHeight))]
    public partial double CalendarWidth { get; set; } = 900;

    /// <summary>Les chiffres clés, sur quatre, deux ou une colonne selon la largeur.</summary>
    public int TileColumns => PageWidth >= FourTilesMinimum ? 4 : PageWidth >= TwoTilesMinimum ? 2 : 1;

    /// <summary>Les détails par mois et par dossier tiennent côte à côte.</summary>
    public bool IsSideBySide => PageWidth >= SideBySideMinimum;

    /// <summary>Colonnes du calendrier.</summary>
    public int WeekCount => Math.Max(1, Weeks.Count);

    /// <summary>La hauteur d'une case : le calendrier occupe la largeur de la page, sans jamais la dépasser.</summary>
    public double CellSize => Math.Clamp(Math.Floor((CalendarWidth / WeekCount) - CellGap), CellMinimum, CellMaximum);

    /// <summary>La hauteur d'une ligne du calendrier : la case et son espace ; les noms des jours s'y alignent.</summary>
    public double RowHeight => CellSize + CellGap;

    /// <summary>Aucun mois à montrer.</summary>
    public bool HasNoMonth => Months.Count == 0;

    /// <summary>Au moins un mois à montrer : une liste vide n'a pas de place à l'écran, et n'a pas à être annoncée.</summary>
    public bool HasMonths => !HasNoMonth;

    /// <summary>Au moins un dossier à montrer.</summary>
    public bool HasFolders => !HasNoFolder;

    /// <summary>Aucun dossier à montrer.</summary>
    public bool HasNoFolder => Folders.Count == 0;

    /// <inheritdoc/>
    public void OnNavigatedTo() => RefreshCommand.Execute(null);

    /// <summary>Montre ces statistiques.</summary>
    public void Show(Statistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        _shown = true;
        Error = null;
        Summary = statistics.FirstActivity is { } first ? StatisticsText.SummarySinceOf(Dates.Short(first).TrimEnd('.')) : StatisticsText.Summary;
        Tiles = BuildTiles(statistics);
        var weeks = WeeksOf(statistics.From, statistics.Day, statistics.Activity);
        Weeks = weeks;
        MonthLabels = MonthLabelsOf(weeks);
        CalendarName = StatisticsText.CalendarNameOf(Days(statistics.ActiveDays, StatisticsText.ActiveDayOneOf, StatisticsText.ActiveDayManyOf));
        var firstMonth = statistics.ByMonth.ToList().FindIndex(month => month.Reviews > 0 || month.ActiveDays > 0);
        var months = firstMonth < 0 ? [] : statistics.ByMonth.Skip(firstMonth).ToList();
        var busiest = Math.Max(1, months.Count == 0 ? 1 : months.Max(month => month.Reviews));
        Months = [.. months.Select(month => new MonthRow(MonthLong(month.Month), month.Reviews, month.ActiveDays, (double)month.Reviews / busiest))];
        Folders =
        [
            .. statistics.ByFolder.Select(folder => new FolderRow(
                folder.Folder.Length > 0 ? folder.Folder.Replace("/", " › ", StringComparison.Ordinal) : StatisticsText.NoFolder,
                folder.Sheets,
                folder.Reviews,
                Percent(folder.OnTime, folder.Reviews),
                folder.Late > 0 ? StatisticsText.DelayValueOf(Number(folder.AverageDelay)) : StatisticsText.None)),
        ];
        IsLoaded = true;
    }

    /// <summary>« 83 % », ou « — » sans révision.</summary>
    internal static string Percent(int part, int total) =>
        total <= 0 ? StatisticsText.None : StatisticsText.PercentOf(((int)Math.Floor((part * 100.0 / total) + 0.5)).ToString(French));

    /// <summary>De 1 à 4 selon la charge du jour par rapport au jour le plus chargé ; 0 sans activité.</summary>
    internal static int LevelOf(int total, int maximum) =>
        total <= 0 || maximum <= 0 ? 0 : Math.Min(4, Math.Max(1, (int)Math.Ceiling(total * 4.0 / maximum)));

    /// <summary>Les semaines du calendrier, du lundi <paramref name="from"/> à la semaine de <paramref name="day"/> incluse.</summary>
    internal static IReadOnlyList<CalendarWeek> WeeksOf(DateOnly from, DateOnly day, IReadOnlyList<ActivityDay> activity)
    {
        var byDay = activity.ToDictionary(active => active.Day);
        var maximum = activity.Count == 0 ? 0 : activity.Max(active => active.Total);
        var columns = Math.Max(1, (int)Math.Ceiling((day.DayNumber - from.DayNumber + 1) / 7.0));
        var weeks = new List<CalendarWeek>(columns);
        for (var column = 0; column < columns; column++)
        {
            var cells = new CalendarCell[7];
            for (var row = 0; row < 7; row++)
            {
                var date = from.AddDays((column * 7) + row);
                var active = byDay.GetValueOrDefault(date);
                var total = active?.Total ?? 0;
                cells[row] = new CalendarCell(date, total, LevelOf(total, maximum), date > day, date == day, DescriptionOf(date, day, active));
            }
            weeks.Add(new CalendarWeek(cells));
        }
        return weeks;
    }

    /// <summary>Le mois ouvert par chaque semaine ; un libellé trop serré contre le suivant est tu.</summary>
    internal static IReadOnlyList<string> MonthLabelsOf(IReadOnlyList<CalendarWeek> weeks)
    {
        var opening = new List<int>();
        for (var column = 0; column < weeks.Count; column++)
        {
            var first = weeks[column].Cells[0].Day;
            if (column == 0 || first.Month != weeks[column - 1].Cells[0].Day.Month)
            {
                opening.Add(column);
            }
        }
        var labels = new string[weeks.Count];
        Array.Fill(labels, string.Empty);
        for (var i = 0; i < opening.Count; i++)
        {
            if (i == opening.Count - 1 || opening[i + 1] - opening[i] >= 3)
            {
                labels[opening[i]] = weeks[opening[i]].Cells[0].Day.ToString("MMM", French).Replace(".", string.Empty, StringComparison.Ordinal);
            }
        }
        return labels;
    }

    /// <summary>« 3 révisions (2 fiches, 1 passage) et 1 rappel de consolidation ».</summary>
    internal static string DetailOf(ActivityDay active)
    {
        var parts = new List<string>();
        if (active.Sheets > 0)
        {
            parts.Add(Dates.Plural(active.Sheets, StatisticsText.SheetOneOf, StatisticsText.SheetManyOf));
        }
        if (active.Passages > 0)
        {
            parts.Add(Dates.Plural(active.Passages, StatisticsText.PassageOneOf, StatisticsText.PassageManyOf));
        }
        var pieces = new List<string>();
        if (active.Reviews > 0)
        {
            var reviews = Dates.Plural(active.Reviews, StatisticsText.ReviewOneOf, StatisticsText.ReviewManyOf);
            pieces.Add(StatisticsText.ReviewsDetailOf(reviews, string.Join(", ", parts)));
        }
        if (active.Reminders > 0)
        {
            pieces.Add(Dates.Plural(active.Reminders, StatisticsText.ReminderOneOf, StatisticsText.ReminderManyOf));
        }
        return string.Join(StatisticsText.And, pieces);
    }

    private static string DescriptionOf(DateOnly date, DateOnly today, ActivityDay? active)
    {
        var name = Dates.Capitalized(date.ToString("dddd d MMMM yyyy", French));
        return date > today ? name : StatisticsText.DayDescriptionOf(name, active is null ? StatisticsText.Nothing : DetailOf(active));
    }

    private static string MonthLong(string month) =>
        DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("MMMM yyyy", French);

    private static string Number(double value) => value.ToString("0.#", French);

    private static string Days(int count, Func<object, string> one, Func<object, string> many) => Dates.Plural(count, one, many);

    private static IReadOnlyList<StatisticsTile> BuildTiles(Statistics statistics)
    {
        var onTime = new List<string> { StatisticsText.OnTimeDetailOf(statistics.OnTime, statistics.Early, statistics.Late) };
        if (statistics.Late > 0)
        {
            onTime.Add(StatisticsText.DelayDetailOf(Number(statistics.AverageDelay), Days(statistics.LongestDelay, StatisticsText.DayOneOf, StatisticsText.DayManyOf)));
        }
        var reviews = new List<string>
        {
            statistics.Reminders > 0
                ? StatisticsText.OverWeeksWithRemindersOf(Dates.Plural(statistics.Reminders, StatisticsText.HandledOneOf, StatisticsText.HandledManyOf))
                : StatisticsText.OverWeeks,
        };
        if (statistics.BestDay is { } best)
        {
            reviews.Add(StatisticsText.BestDayOf(best.Total, Dates.Short(best.Day)));
        }
        var calendarDays = statistics.Day.DayNumber - statistics.From.DayNumber + 1;
        return
        [
            new(StatisticsText.OnTimeLabel, Percent(statistics.OnTime + statistics.Early, statistics.Reviews), onTime),
            new(statistics.Reviews > 1 ? StatisticsText.ReviewsMany : StatisticsText.ReviewsOne, statistics.Reviews.ToString(French), reviews),
            new(
                statistics.ActiveDays > 1 ? StatisticsText.ActiveMany : StatisticsText.ActiveOne,
                StatisticsText.ActiveValueOf(statistics.ActiveDays, calendarDays),
                [StatisticsText.RecentDetailOf(Dates.Plural(statistics.LastSevenDays, StatisticsText.ReviewOneOf, StatisticsText.ReviewManyOf), statistics.LastThirtyDays)]),
            new(
                StatisticsText.StreakLabel,
                Days(statistics.CurrentStreak, StatisticsText.DayOneOf, StatisticsText.DayManyOf),
                [
                    statistics.LongestStreak <= 0
                        ? StatisticsText.StreakHint
                        : StatisticsText.RecordOf(
                            Days(statistics.LongestStreak, StatisticsText.DayOneOf, StatisticsText.DayManyOf),
                            Dates.Short(statistics.LongestStreakEnd.GetValueOrDefault())),
                ]),
        ];
    }

    /// <summary>Relit les statistiques.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = !_shown;
        try
        {
            var outcome = await Task.Run(_reminders.Statistics).ConfigureAwait(true);
            if (outcome.Succeeded)
            {
                Show(outcome.Value);
            }
            else
            {
                Error = outcome.Error;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
