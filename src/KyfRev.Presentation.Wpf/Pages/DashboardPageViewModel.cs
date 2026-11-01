using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Ce que montre le tableau de bord.</summary>
public enum DashboardFilter
{
    /// <summary>Fiches et passages.</summary>
    All,

    /// <summary>Fiches seulement.</summary>
    Sheets,

    /// <summary>Passages seulement (les rappels, qui portent sur des fiches, sont masqués).</summary>
    Passages,
}

/// <summary>Ordre des éléments dans chaque groupe.</summary>
public enum DashboardSort
{
    /// <summary>Par échéance : l'ordre du coffre (retards du plus ancien au plus récent, fiches d'abord aujourd'hui).</summary>
    DueDate,

    /// <summary>Par étape J, croissante.</summary>
    Step,

    /// <summary>Par nombre de cycles, décroissant.</summary>
    Cycle,

    /// <summary>Par dernière révision, de la plus ancienne à la plus récente.</summary>
    LastReview,

    /// <summary>Par titre, dans l'ordre du français.</summary>
    Title,
}

/// <summary>
/// La vue « Aujourd'hui », comme dans la 0.x (<c>Aujourdhui.svelte</c>) : la liste à réviser
/// toujours en premier (en retard, puis aujourd'hui), les rappels de consolidation, puis les sept
/// jours à venir, replié. Filtre (tout, fiches, passages) et tri (échéance, étape J, cycles,
/// dernière révision, titre) ; avertissements de « Santé du coffre » et de rappels en souffrance ;
/// état vide. Le tableau de bord est relu chaque fois que la vue s'ouvre ; une seule liste
/// virtualisée porte en-têtes et lignes, pour qu'un grand coffre s'affiche vite.
/// </summary>
public sealed partial class DashboardPageViewModel : ObservableObject, INavigationAware
{
    private static readonly StringComparer FrenchTitles = StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), ignoreCase: false);

    private readonly ReminderUseCases _reminders;
    private readonly DashboardGroup _overdue;
    private readonly DashboardGroup _today;
    private readonly DashboardGroup _reminderGroup;
    private readonly DashboardGroup _upcoming;
    private Dashboard? _dashboard;

    /// <summary>La vue, sur les cas d'usage des rappels (qui rendent le tableau de bord).</summary>
    public DashboardPageViewModel(ReminderUseCases reminders)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        _reminders = reminders;
        _overdue = new DashboardGroup(DashboardText.Overdue, Icons.Clock, DashboardTone.Overdue, expanded: true, BuildRows);
        _today = new DashboardGroup(DashboardText.Today, Icons.Calendar, DashboardTone.Normal, expanded: true, BuildRows);
        _reminderGroup = new DashboardGroup(DashboardText.Reminders, Icons.Sync, DashboardTone.Reminder, expanded: true, BuildRows);
        // Comme dans la 0.x, « À venir » est replié d'emblée : la liste du jour d'abord.
        _upcoming = new DashboardGroup(DashboardText.Upcoming, Icons.Calendar, DashboardTone.Normal, expanded: false, BuildRows);
        Filters = [new(DashboardFilter.All, DashboardText.All), new(DashboardFilter.Sheets, DashboardText.Sheets), new(DashboardFilter.Passages, DashboardText.Passages)];
        Sorts =
        [
            new(DashboardSort.DueDate, DashboardText.SortDueDate),
            new(DashboardSort.Step, DashboardText.SortStep),
            new(DashboardSort.Cycle, DashboardText.SortCycle),
            new(DashboardSort.LastReview, DashboardText.SortLastReview),
            new(DashboardSort.Title, DashboardText.SortTitle),
        ];
        SelectedFilter = Filters[0];
        SelectedSort = Sorts[0];
        Rows = [];
        Subtitle = string.Empty;
        Footer = string.Empty;
    }

    /// <summary>« Aujourd'hui ».</summary>
    public static string Title => DashboardText.Today;

    /// <summary>Filtres proposés.</summary>
    public IReadOnlyList<Choice<DashboardFilter>> Filters { get; }

    /// <summary>Tris proposés.</summary>
    public IReadOnlyList<Choice<DashboardSort>> Sorts { get; }

    /// <summary>Filtre choisi.</summary>
    [ObservableProperty]
    public partial Choice<DashboardFilter> SelectedFilter { get; set; }

    /// <summary>Tri choisi.</summary>
    [ObservableProperty]
    public partial Choice<DashboardSort> SelectedSort { get; set; }

    /// <summary>
    /// Les lignes : l'état vide s'il n'y a rien à réviser, puis chaque groupe non vide (son
    /// en-tête, et ses éléments s'il est déplié).
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<object> Rows { get; private set; }

    /// <summary>« Samedi 1 novembre · 3 éléments à réviser ».</summary>
    [ObservableProperty]
    public partial string Subtitle { get; private set; }

    /// <summary>« 12 fiches actives · 3 fiches archivées · prochaine échéance lun. 3 nov. ».</summary>
    [ObservableProperty]
    public partial string Footer { get; private set; }

    /// <summary>Points à vérifier dans le coffre ; rien s'il n'y en a pas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHealthWarning))]
    public partial string? HealthWarning { get; private set; }

    /// <summary>Un avertissement de santé est affiché.</summary>
    public bool HasHealthWarning => HealthWarning is not null;

    /// <summary>Fiches qui attendent un rappel depuis trop longtemps ; rien s'il n'y en a pas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPriorityWarning))]
    public partial string? PriorityWarning { get; private set; }

    /// <summary>Un avertissement de rappels en souffrance est affiché.</summary>
    public bool HasPriorityWarning => PriorityWarning is not null;

    /// <summary>Le tableau de bord n'a pas pu être lu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Un refus est affiché.</summary>
    public bool HasError => Error is not null;

    /// <summary>Première lecture en cours : des squelettes tiennent la place de la liste.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Le tableau de bord lu en dernier.</summary>
    public Dashboard? Dashboard => _dashboard;

    /// <inheritdoc/>
    public void OnNavigatedTo() => RefreshCommand.Execute(null);

    /// <summary>Relit le tableau de bord (la journée est préparée de nouveau après minuit).</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = _dashboard is null;
        try
        {
            // Le calcul lit tout le coffre : hors du fil de l'interface.
            var outcome = await Task.Run(_reminders.Dashboard).ConfigureAwait(true);
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

    /// <summary>Affiche un tableau de bord déjà lu.</summary>
    public void Show(Dashboard dashboard)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        Error = null;
        _dashboard = dashboard;
        OnPropertyChanged(nameof(Dashboard));
        BuildRows();
    }

    partial void OnSelectedFilterChanged(Choice<DashboardFilter> value) => BuildRows();

    partial void OnSelectedSortChanged(Choice<DashboardSort> value) => BuildRows();

    private void BuildRows()
    {
        if (_dashboard is not { } dashboard)
        {
            return;
        }
        var overdue = Prepare(dashboard.Overdue);
        var today = Prepare(dashboard.Today);
        var upcoming = Prepare(dashboard.Upcoming);
        var reminders = SelectedFilter.Value == DashboardFilter.Passages ? [] : dashboard.Reminders;
        var total = overdue.Count + today.Count + reminders.Length;

        var rows = new List<object>();
        if (total == 0)
        {
            rows.Add(new EmptyDashboardRow(
                dashboard.NextDueDate is { } next ? DashboardText.NextReviewOf(Dates.Long(next))
                : dashboard.ActiveSheets + dashboard.ArchivedSheets == 0 ? DashboardText.FirstSheet
                : DashboardText.AllArchived));
        }
        Add(rows, _overdue, overdue.Select(item => new DueItemRow(item, dashboard.ActiveRecall, showDueDate: false)));
        Add(rows, _today, today.Select(item => new DueItemRow(item, dashboard.ActiveRecall, showDueDate: false)));
        Add(rows, _reminderGroup, reminders.Select(reminder => new ReminderRow(reminder)));
        Add(rows, _upcoming, upcoming.Select(item => new DueItemRow(item, dashboard.ActiveRecall, showDueDate: true)));
        Rows = rows;

        Subtitle = $"{Dates.Capitalized(Dates.Long(dashboard.Day))} · {(total == 0 ? DashboardText.NothingToReview : Dates.Plural(total, DashboardText.ItemsToReviewOneOf, DashboardText.ItemsToReviewManyOf))}";
        HealthWarning = dashboard.HealthProblems > 0 ? Dates.Plural(dashboard.HealthProblems, DashboardText.HealthWarningOneOf, DashboardText.HealthWarningManyOf) : null;
        PriorityWarning = dashboard.UnservedPriorities > 0 ? Dates.Plural(dashboard.UnservedPriorities, DashboardText.PriorityWarningOneOf, DashboardText.PriorityWarningManyOf) : null;
        var footer = $"{Dates.Plural(dashboard.ActiveSheets, DashboardText.SheetsActiveOneOf, DashboardText.SheetsActiveManyOf)} · {Dates.Plural(dashboard.ArchivedSheets, DashboardText.SheetsArchivedOneOf, DashboardText.SheetsArchivedManyOf)}";
        Footer = dashboard.NextDueDate is { } nextDue && total > 0 ? $"{footer} · {DashboardText.NextDueOf(Dates.Short(nextDue))}" : footer;
    }

    /// <summary>L'en-tête d'un groupe non vide, et ses lignes s'il est déplié.</summary>
    private static void Add(List<object> rows, DashboardGroup group, IEnumerable<object> items)
    {
        var all = items.ToList();
        group.Count = all.Count;
        if (all.Count == 0)
        {
            return;
        }
        rows.Add(group);
        if (group.IsExpanded)
        {
            rows.AddRange(all);
        }
    }

    /// <summary>Filtre puis trie, comme la 0.x : l'échéance garde l'ordre du coffre, les autres tris sont stables.</summary>
    private List<DueItem> Prepare(IEnumerable<DueItem> items)
    {
        var filtered = SelectedFilter.Value switch
        {
            DashboardFilter.Sheets => items.Where(item => item.Kind == ItemKind.Sheet),
            DashboardFilter.Passages => items.Where(item => item.Kind == ItemKind.Passage),
            _ => items,
        };
        return SelectedSort.Value switch
        {
            DashboardSort.Step => [.. filtered.OrderBy(item => StepValue(item.Step))],
            DashboardSort.Cycle => [.. filtered.OrderByDescending(item => item.Cycle)],
            DashboardSort.LastReview => [.. filtered.OrderBy(item => item.LastReview)],
            DashboardSort.Title => [.. filtered.OrderBy(item => item.Title, FrenchTitles)],
            _ => [.. filtered],
        };
    }

    /// <summary>Le nombre d'une étape (« J14 » → 14), 0 sans chiffre.</summary>
    internal static int StepValue(string step)
    {
        var digits = new string([.. step.Where(char.IsAsciiDigit)]);
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
