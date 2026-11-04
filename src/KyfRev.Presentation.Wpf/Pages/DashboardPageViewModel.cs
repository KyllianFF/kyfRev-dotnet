using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.Modules;
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

/// <summary>Disposition de la vue « Aujourd'hui », selon la largeur de la page.</summary>
public enum TodayLayout
{
    /// <summary>Une colonne : les encarts défilent à la suite de la liste.</summary>
    OneColumn,

    /// <summary>La liste et une colonne latérale.</summary>
    WithSide,

    /// <summary>La liste, l'aperçu de la ligne choisie et la colonne latérale.</summary>
    WithPreview,

    /// <summary>La liste en deux cartes, l'aperçu et la colonne latérale.</summary>
    WithSplit,
}

/// <summary>
/// La vue « Aujourd'hui » : ce qu'il y a à réviser et de quoi commencer, puis la liste (en retard,
/// aujourd'hui, rappels de consolidation, sept jours à venir) ; à côté si la place le permet, à la
/// suite sinon, les jours à venir, les révisions de la semaine et les encarts des modules. Filtre
/// (tout, fiches, passages) et tri (échéance, étape J, cycles, dernière révision, titre) ;
/// avertissements de « Santé du coffre » et de rappels en souffrance ; état vide. Le tableau de bord
/// est relu chaque fois que la vue s'ouvre ; une seule liste virtualisée porte en-têtes et lignes,
/// pour qu'un grand coffre s'affiche vite. Sur les grands écrans, la ligne choisie se lit à côté de
/// la liste (<see cref="TodayPreview"/>) ; sur les plus grands, la liste se partage en deux cartes.
/// </summary>
public sealed partial class DashboardPageViewModel : ObservableObject, INavigationAware
{
    private static readonly StringComparer FrenchTitles = StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), ignoreCase: false);

    private readonly ReminderUseCases _reminders;
    private readonly SheetUseCases _sheets;
    private readonly IReviewOpener _opener;
    private readonly DashboardGroup _overdue;
    private readonly DashboardGroup _today;
    private readonly DashboardGroup _reminderGroup;
    private readonly DashboardGroup _upcoming;
    private readonly DashboardExtras _extras;
    private readonly IEnumerable<IDashboardWidget> _widgets;
    private Dashboard? _dashboard;
    private IReadOnlyList<DueItem> _toStart = [];

    /// <summary>Numéro du dernier aperçu demandé : une lecture dépassée est ignorée.</summary>
    private int _previewTicket;

    /// <summary>
    /// La vue, sur les cas d'usage des rappels (qui rendent le tableau de bord) et des fiches (qui
    /// nourrissent l'aperçu) ; <paramref name="opener"/> ouvre ses lignes, <paramref name="widgets"/> sont
    /// les encarts apportés par les modules.
    /// </summary>
    public DashboardPageViewModel(ReminderUseCases reminders, SheetUseCases sheets, IReviewOpener opener, IEnumerable<IDashboardWidget>? widgets = null)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(opener);
        _reminders = reminders;
        _sheets = sheets;
        _opener = opener;
        _widgets = widgets ?? [];
        _extras = new DashboardExtras(this);
        _overdue = new DashboardGroup(DashboardText.Overdue, Icons.Clock, DashboardTone.Overdue, expanded: true, BuildRows);
        _today = new DashboardGroup(DashboardText.Today, Icons.Calendar, DashboardTone.Normal, expanded: true, BuildRows);
        _reminderGroup = new DashboardGroup(DashboardText.Reminders, Icons.Sync, DashboardTone.Reminder, expanded: true, BuildRows);
        _upcoming = new DashboardGroup(DashboardText.Upcoming, Icons.Calendar, DashboardTone.Normal, expanded: true, BuildRows);
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
        SecondRows = [];
        Subtitle = string.Empty;
        DayLabel = string.Empty;
        ReviewNoun = string.Empty;
        WeekNoun = string.Empty;
        Chips = [];
        SideReminders = [];
        Forecast = [];
        Week = [];
        Cards = [];
        Footer = string.Empty;
        StartLabel = string.Empty;
        ResumeLabel = string.Empty;
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
    [NotifyPropertyChangedFor(nameof(SortName))]
    public partial Choice<DashboardSort> SelectedSort { get; set; }

    /// <summary>
    /// Les lignes : l'état vide s'il n'y a rien à réviser, puis chaque groupe non vide (son
    /// en-tête, et ses éléments s'il est déplié).
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<object> Rows { get; private set; }

    /// <summary>Largeur à partir de laquelle la colonne latérale tient à côté de la liste.</summary>
    public const double SideColumnMinimum = 880;

    /// <summary>Largeur à partir de laquelle l'aperçu tient à côté de la liste.</summary>
    public const double PreviewMinimum = 1550;

    /// <summary>Largeur à partir de laquelle la liste se partage en deux cartes.</summary>
    public const double SplitMinimum = 2150;

    /// <summary>Largeur à partir de laquelle les boutons et le retard tiennent sur la ligne.</summary>
    public const double RoomyMinimum = 600;

    /// <summary>Largeur de la page, en pixels indépendants, zoom déjà compté.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Layout), nameof(HasRoom), nameof(HasPreview))]
    public partial double PageWidth { get; set; }

    /// <summary>Disposition de la vue.</summary>
    public TodayLayout Layout => LayoutFor(PageWidth);

    /// <summary>La ligne choisie se lit à côté de la liste : un clic la choisit, un double clic l'ouvre.</summary>
    public bool HasPreview => Layout is TodayLayout.WithPreview or TodayLayout.WithSplit;

    /// <summary>La ligne choisie, quand la page a la place d'un aperçu.</summary>
    [ObservableProperty]
    public partial object? SelectedRow { get; set; }

    /// <summary>L'aperçu de la ligne choisie ; rien sans ligne choisie, le temps de la première lecture comprise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPreview))]
    public partial TodayPreview? Preview { get; private set; }

    /// <summary>Un aperçu est à montrer.</summary>
    public bool ShowsPreview => Preview is not null;

    /// <summary>La seconde carte de la liste, quand elle se partage en deux : aujourd'hui et à venir.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<object> SecondRows { get; private set; }

    /// <summary>
    /// La page est assez large pour tout garder sur une ligne ; sinon, la liste passe avant le reste :
    /// le tri se réduit à son icône, le pied de liste disparaît.
    /// </summary>
    public bool HasRoom => PageWidth >= RoomyMinimum;

    /// <summary>« Samedi 1 novembre ».</summary>
    [ObservableProperty]
    public partial string DayLabel { get; private set; }

    /// <summary>Éléments à réviser, rappels compris.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReviews))]
    public partial int ReviewCount { get; private set; }

    /// <summary>Il y a quelque chose à réviser.</summary>
    public bool HasReviews => ReviewCount > 0;

    /// <summary>« éléments à réviser » ; « Rien à réviser aujourd'hui » s'il n'y a rien.</summary>
    [ObservableProperty]
    public partial string ReviewNoun { get; private set; }

    /// <summary>Le détail : en retard, aujourd'hui, rappels ; rien pour ce qui est à zéro.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<HeroChip> Chips { get; private set; }

    /// <summary>Rappels à montrer dans la colonne latérale ; sans elle, ils restent dans la liste.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSideReminders))]
    public partial IReadOnlyList<ReminderRow> SideReminders { get; private set; }

    /// <summary>La colonne latérale montre des rappels.</summary>
    public bool HasSideReminders => SideReminders.Count > 0;

    /// <summary>Les sept prochains jours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasForecast))]
    public partial IReadOnlyList<ForecastDay> Forecast { get; private set; }

    /// <summary>Une échéance tombe dans les sept prochains jours.</summary>
    public bool HasForecast => Forecast.Any(day => day.Count > 0);

    /// <summary>Les sept derniers jours, aujourd'hui compris.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<WeekBar> Week { get; private set; }

    /// <summary>Révisions validées ces sept derniers jours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWeek))]
    public partial int WeekTotal { get; private set; }

    /// <summary>Au moins une révision validée ces sept derniers jours.</summary>
    public bool HasWeek => WeekTotal > 0;

    /// <summary>« révisions validées ».</summary>
    [ObservableProperty]
    public partial string WeekNoun { get; private set; }

    /// <summary>Ce que les modules vont chercher au-dehors, une fois la vue affichée ; vrai si un encart a changé.</summary>
    public Func<Task<bool>>? LookForNews { get; init; }

    /// <summary>Encarts des modules, ceux qui ont quelque chose à montrer aujourd'hui.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCards))]
    public partial IReadOnlyList<DashboardCard> Cards { get; private set; }

    /// <summary>Un encart est à montrer.</summary>
    public bool HasCards => Cards.Count > 0;

    /// <summary>« Trier par échéance ».</summary>
    public string SortName => $"{DashboardText.SortBy} {SelectedSort.Label}";

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

    /// <summary>« Commencer (5) » : une séance sur la liste du jour (en retard, puis aujourd'hui), filtrée et triée.</summary>
    [ObservableProperty]
    public partial string StartLabel { get; private set; }

    /// <summary>Il y a de quoi faire une séance.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool CanStart { get; private set; }

    /// <summary>« Reprendre la séance (3 restants) ».</summary>
    [ObservableProperty]
    public partial string ResumeLabel { get; private set; }

    /// <summary>Une séance interrompue aujourd'hui peut être reprise.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    public partial bool CanResume { get; private set; }

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
            var (outcome, cards) = await Task.Run(() => (_reminders.Dashboard(), ReadCards())).ConfigureAwait(true);
            if (outcome.Succeeded)
            {
                Show(outcome.Value, cards);
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
        if (LookForNews is { } lookForNews && await lookForNews().ConfigureAwait(true))
        {
            Cards = await Task.Run(ReadCards).ConfigureAwait(true);
        }
    }

    /// <summary>Ouvre une ligne : la fiche ou le passage à réviser, ou la fiche d'un rappel. Un en-tête n'ouvre rien.</summary>
    [RelayCommand]
    private void Open(object? row)
    {
        switch (row)
        {
            case DueItemRow item:
                _opener.Open(item.Item, item.ActiveRecall);
                break;
            case ReminderRow reminder:
                _opener.OpenReminder(reminder.Reminder);
                break;
        }
    }

    /// <summary>Lance une séance sur la liste du jour, dans l'ordre affiché.</summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start() => _opener.StartSession(_toStart, _dashboard?.ActiveRecall ?? true);

    /// <summary>Reprend la séance interrompue.</summary>
    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume() => _opener.ResumeSession();

    /// <summary>Affiche un tableau de bord déjà lu, et les encarts <paramref name="cards"/> s'ils sont donnés.</summary>
    public void Show(Dashboard dashboard, IReadOnlyList<DashboardCard>? cards = null)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        Error = null;
        Cards = cards ?? Cards;
        var resumable = _opener.ResumableItems();
        CanResume = resumable is > 0;
        ResumeLabel = resumable is { } remaining ? Dates.Plural(remaining, SessionText.ResumeOneOf, SessionText.ResumeManyOf) : string.Empty;
        BuildRows(dashboard);
        // Retenu, et annoncé, une fois les lignes prêtes.
        _dashboard = dashboard;
        OnPropertyChanged(nameof(Dashboard));
    }

    partial void OnSelectedFilterChanged(Choice<DashboardFilter> value) => BuildRows();

    partial void OnSelectedSortChanged(Choice<DashboardSort> value) => BuildRows();

    partial void OnPageWidthChanged(double oldValue, double newValue)
    {
        // Les rappels et les encarts changent de place : la liste n'est refaite que si la disposition change.
        if (LayoutFor(oldValue) != LayoutFor(newValue))
        {
            BuildRows();
        }
    }

    partial void OnSelectedRowChanged(object? value) => LoadPreviewCommand.Execute(value);

    private static TodayLayout LayoutFor(double width) => width switch
    {
        >= SplitMinimum => TodayLayout.WithSplit,
        >= PreviewMinimum => TodayLayout.WithPreview,
        >= SideColumnMinimum => TodayLayout.WithSide,
        _ => TodayLayout.OneColumn,
    };

    /// <summary>Ce qui identifie une ligne d'une lecture à l'autre ; rien pour un en-tête.</summary>
    private static string? KeyOf(object? row) => row switch
    {
        DueItemRow item => item.Item.Id,
        ReminderRow reminder => reminder.Reminder.SheetId,
        _ => null,
    };

    /// <summary>Lit la fiche de la ligne choisie, hors du fil de l'interface ; seule la dernière demande compte.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadPreviewAsync(object? row)
    {
        var ticket = ++_previewTicket;
        var sheet = row switch
        {
            DueItemRow item => item.Item.SheetId,
            ReminderRow reminder => reminder.Reminder.SheetId,
            _ => null,
        };
        if (sheet is null)
        {
            Preview = null;
            return;
        }
        var outcome = await Task.Run(() => _sheets.Open(sheet)).ConfigureAwait(true);
        if (ticket == _previewTicket)
        {
            Preview = outcome.Succeeded ? TodayPreview.Of(row!, outcome.Value, _sheets) : TodayPreview.Failed(row!, outcome.Error);
        }
    }

    /// <summary>Garde la ligne choisie d'une lecture à l'autre ; sinon, la première ligne.</summary>
    private void KeepSelection(IEnumerable<object> rows)
    {
        var key = KeyOf(SelectedRow);
        var candidates = HasPreview ? rows.Where(row => KeyOf(row) is not null).ToList() : [];
        SelectedRow = candidates.Find(row => KeyOf(row) == key) ?? candidates.FirstOrDefault();
    }

    private List<DashboardCard> ReadCards() => [.. _widgets.OrderBy(widget => widget.Order).Select(widget => widget.Card()).OfType<DashboardCard>()];

    private void BuildRows()
    {
        if (_dashboard is { } dashboard)
        {
            BuildRows(dashboard);
        }
    }

    private void BuildRows(Dashboard dashboard)
    {
        var overdue = Prepare(dashboard.Overdue);
        var today = Prepare(dashboard.Today);
        var upcoming = Prepare(dashboard.Upcoming);
        var reminders = SelectedFilter.Value == DashboardFilter.Passages ? [] : dashboard.Reminders;
        var total = overdue.Count + today.Count + reminders.Length;
        _toStart = [.. overdue, .. today];
        CanStart = _toStart.Count > 0;
        StartLabel = SessionText.StartOf(_toStart.Count);

        var rows = new List<object>();
        if (total == 0)
        {
            rows.Add(new EmptyDashboardRow(
                dashboard.NextDueDate is { } next ? DashboardText.NextReviewOf(Dates.Long(next))
                : dashboard.ActiveSheets + dashboard.ArchivedSheets == 0 ? DashboardText.FirstSheet
                : DashboardText.AllArchived));
        }
        // En deux cartes : les retards et les rappels d'un côté, aujourd'hui et à venir de l'autre.
        var second = Layout == TodayLayout.WithSplit ? [] : rows;
        Add(rows, _overdue, overdue.Select(item => new DueItemRow(item, dashboard.ActiveRecall, showDueDate: false)));
        Add(second, _today, today.Select(item => new DueItemRow(item, dashboard.ActiveRecall, showDueDate: false)));
        var hasSide = Layout is TodayLayout.WithSide or TodayLayout.WithPreview;
        Add(rows, _reminderGroup, (hasSide ? [] : reminders).Select(reminder => new ReminderRow(reminder)));
        Add(second, _upcoming, upcoming.Select(item => new DueItemRow(item, dashboard.ActiveRecall, showDueDate: true)));
        SideReminders = hasSide ? [.. reminders.Select(reminder => new ReminderRow(reminder))] : [];
        var comingDays = Layout == TodayLayout.WithSplit ? 2 * Application.Model.Dashboard.UpcomingDays : Application.Model.Dashboard.UpcomingDays;
        Forecast = [.. dashboard.Workload.Skip(1).Take(comingDays).Select(day => new ForecastDay(day.Day, day.Due.Length))];
        var most = dashboard.RecentReviews.Select(day => day.Reviews).DefaultIfEmpty().Max();
        Week = [.. dashboard.RecentReviews.Select(day => new WeekBar(day, most, day.Day == dashboard.Day))];
        WeekTotal = dashboard.RecentReviews.Sum(day => day.Reviews);
        WeekNoun = WeekTotal > 1 ? DashboardText.WeekNounMany : DashboardText.WeekNounOne;
        if (Layout == TodayLayout.OneColumn && (HasForecast || HasWeek || HasCards))
        {
            rows.Add(_extras);
        }
        Rows = rows;
        SecondRows = ReferenceEquals(second, rows) ? [] : second;
        KeepSelection(ReferenceEquals(second, rows) ? rows : [.. rows, .. second]);

        DayLabel = Dates.Capitalized(Dates.Long(dashboard.Day));
        ReviewCount = total;
        ReviewNoun = total == 0 ? DashboardText.EmptyTitle : total > 1 ? DashboardText.ReviewNounMany : DashboardText.ReviewNounOne;
        List<HeroChip> chips = [];
        if (overdue.Count > 0)
        {
            chips.Add(new(PillKind.Overdue, DashboardText.OverdueChipOf(overdue.Count)));
        }
        if (today.Count > 0)
        {
            chips.Add(new(PillKind.Today, DashboardText.TodayChipOf(today.Count)));
        }
        if (reminders.Length > 0)
        {
            chips.Add(new(PillKind.Reminder, Dates.Plural(reminders.Length, DashboardText.ReminderChipOneOf, DashboardText.ReminderChipManyOf)));
        }
        Chips = chips;
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
