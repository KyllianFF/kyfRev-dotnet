using System.Collections.Immutable;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Une colonne de la charge à venir : un jour, ses échéances connues sous ses étapes projetées.</summary>
public sealed partial class LoadDay : ObservableObject
{
    internal LoadDay(WorkloadDay day, bool isToday, bool isPeak, Func<double, double> height, double average, double plotHeight)
    {
        Source = day;
        Day = day.Day;
        DueCount = day.Due.Length;
        ProjectedCount = day.Projected.Length;
        IsPeak = isPeak;
        DueHeight = height(DueCount);
        ProjectedHeight = height(ProjectedCount);
        AverageHeight = height(average);
        PlotHeight = plotHeight;
        Label = isToday ? WorkloadText.TodayShort : day.Day.Day.ToString(CultureInfo.InvariantCulture);
        Month = day.Day.Day == 1 ? Dates.MonthName(day.Day) : string.Empty;
        IsWeekend = day.Day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        Reviews = WorkloadPageViewModel.ReviewsOf(Total);
        var spoken = Reviews;
        if (ProjectedCount > 0)
        {
            spoken = Dates.Plural(ProjectedCount, count => WorkloadText.WithProjectedOneOf(Reviews, count), count => WorkloadText.WithProjectedManyOf(Reviews, count));
        }
        Name = WorkloadText.DayNameOf(Dates.Long(day.Day), spoken);
    }

    /// <summary>Le jour, tel que le coffre le donne (filtré).</summary>
    public WorkloadDay Source { get; }

    /// <summary>Le jour.</summary>
    public DateOnly Day { get; }

    /// <summary>Échéances connues.</summary>
    public int DueCount { get; }

    /// <summary>Étapes projetées.</summary>
    public int ProjectedCount { get; }

    /// <summary>Révisions ce jour-là.</summary>
    public int Total => DueCount + ProjectedCount;

    /// <summary>« 3 révisions », « aucune révision ».</summary>
    public string Reviews { get; }

    /// <summary>Le jour le plus chargé.</summary>
    public bool IsPeak { get; }

    /// <summary>Samedi ou dimanche.</summary>
    public bool IsWeekend { get; }

    /// <summary>Hauteur des échéances connues.</summary>
    public double DueHeight { get; }

    /// <summary>Hauteur des étapes projetées.</summary>
    public double ProjectedHeight { get; }

    /// <summary>Hauteur du trait de la moyenne, 0 s'il n'y a rien à moyenner.</summary>
    public double AverageHeight { get; }

    /// <summary>Hauteur de la zone où les colonnes montent, leur nombre compris.</summary>
    public double PlotHeight { get; }

    /// <summary>Les deux segments se suivent : un fin écart les sépare.</summary>
    public bool HasBoth => DueCount > 0 && ProjectedCount > 0;

    /// <summary>Le jour de ce mois (« 14 »), ou « auj. ».</summary>
    public string Label { get; }

    /// <summary>« nov. » sous le 1er du mois ; vide sinon.</summary>
    public string Month { get; }

    /// <summary>« mardi 3 novembre : 3 révisions, dont 1 projetée » : l'infobulle et le nom lu par le lecteur d'écran.</summary>
    public string Name { get; }

    /// <summary>Le jour est choisi : son nombre de révisions s'écrit au-dessus de la colonne.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsValue))]
    public partial bool IsSelected { get; set; }

    /// <summary>Le nombre de révisions s'écrit au-dessus de la colonne : le jour le plus chargé et le jour choisi.</summary>
    public bool ShowsValue => Total > 0 && (IsPeak || IsSelected);

    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => Name;
}

/// <summary>Une étape projetée d'un jour choisi : elle suit l'échéance, et ne se déplace pas.</summary>
public sealed class ProjectedRow
{
    internal ProjectedRow(ProjectedItem item, bool activeRecall)
    {
        Step = item.Step;
        var hidden = activeRecall && item.Term is null && item.Excerpt is not null;
        IsHidden = hidden;
        (Heading, Source) = (hidden, item.Term, item.Excerpt) switch
        {
            (true, _, _) => (DashboardText.PassageToRecall, $"· {string.Join(" › ", [item.Title, .. item.Section.TakeLast(1)])}"),
            (_, { } term, _) => (term, $"· {item.Title}"),
            (_, null, { } excerpt) => ($"« {excerpt} »", $"· {item.Title}"),
            _ => (item.Title, string.Empty),
        };
    }

    /// <summary>« J7 ».</summary>
    public string Step { get; }

    /// <summary>Passage caché par le rappel actif.</summary>
    public bool IsHidden { get; }

    /// <summary>Le titre, le terme, le passage, ou « Passage à retrouver ».</summary>
    public string Heading { get; }

    /// <summary>« · Titre de la fiche », vide pour une fiche.</summary>
    public string Source { get; }

    /// <summary>Le nom lu par le lecteur d'écran, pour une ligne.</summary>
    public string Spoken => ToString();

    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => string.Join(" ", new[] { Step, Heading, Source }.Where(part => part.Length > 0));
}

/// <summary>
/// La charge des 30 prochains jours (<c>ChargeAVenir.svelte</c>) : une colonne par jour, d'aujourd'hui (retards compris) à
/// J+30, les échéances connues sous les étapes projetées, la moyenne et le jour le plus chargé. Un jour choisi montre ses
/// révisions ; une échéance se déplace vers un jour moins chargé (un report ordinaire), et le jour le moins chargé à trois
/// jours au plus est proposé. Le filtre Tout, Fiches, Passages s'applique. Tout vient du tableau de bord, relu à chaque
/// ouverture hors du fil de l'interface ; rien n'en reste quand le coffre se verrouille.
/// </summary>
public sealed partial class WorkloadPageViewModel : ObservableObject, INavigationAware, IForgetsVault
{
    /// <summary>Largeur à partir de laquelle les 31 colonnes tiennent sur une rangée.</summary>
    public const double OneRowMinimum = 682;

    /// <summary>Colonnes sur une rangée.</summary>
    public const int WideColumns = 31;

    /// <summary>Colonnes de chacune des deux rangées, à l'étroit : assez larges pour être visées et étiquetées.</summary>
    public const int NarrowColumns = 16;

    /// <summary>Hauteur de la colonne la plus chargée, sur une rangée.</summary>
    public const double TallBar = 96;

    /// <summary>Hauteur de la colonne la plus chargée, sur deux rangées.</summary>
    public const double ShortBar = 56;

    /// <summary>Place du nombre au-dessus de la colonne.</summary>
    public const double ValueRoom = 18;

    private const double SmallestBar = 3;
    private const int NearbyDays = 3;

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly ReminderUseCases _reminders;
    private readonly ReviewUseCases _reviews;
    private readonly IReviewOpener _opener;
    private Action? _back;
    private Dashboard? _dashboard;

    /// <summary>Numéro de la dernière lecture demandée : une lecture dépassée n'affiche rien.</summary>
    private int _reading;

    /// <summary>Nombre d'oublis du coffre : un déplacement parti avant le dernier n'y remet rien.</summary>
    private int _generation;

    /// <summary>La liste se refait : le jour choisi reste, avec ce qui est écrit dessous.</summary>
    private bool _rebuilding;

    /// <summary>La vue, sur le tableau de bord (les rappels le rendent), les reports, et l'ouverture des révisions.</summary>
    public WorkloadPageViewModel(ReminderUseCases reminders, ReviewUseCases reviews, IReviewOpener opener)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(reviews);
        ArgumentNullException.ThrowIfNull(opener);
        _reminders = reminders;
        _reviews = reviews;
        _opener = opener;
        Filters = [new(DashboardFilter.All, DashboardText.All), new(DashboardFilter.Sheets, DashboardText.Sheets), new(DashboardFilter.Passages, DashboardText.Passages)];
        SelectedFilter = Filters[0];
        Days = [];
        Summary = string.Empty;
        Note = string.Empty;
        DayTitle = string.Empty;
        DueRows = [];
        ProjectedRows = [];
        Message = string.Empty;
        Error = string.Empty;
    }

    /// <summary>« Charge — 30 prochains jours ».</summary>
    public static string Title => WorkloadText.Title;

    /// <summary>Filtres proposés.</summary>
    public IReadOnlyList<Choice<DashboardFilter>> Filters { get; }

    /// <summary>Filtre choisi.</summary>
    [ObservableProperty]
    public partial Choice<DashboardFilter> SelectedFilter { get; set; }

    /// <summary>Première lecture en cours.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Largeur de la zone des colonnes : elle décide d'une rangée ou de deux.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChartColumns))]
    public partial double ChartWidth { get; set; }

    /// <summary>Colonnes par rangée.</summary>
    public int ChartColumns => ColumnsFor(ChartWidth);

    /// <summary>Aujourd'hui puis les 30 jours suivants.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<LoadDay> Days { get; private set; }

    /// <summary>Révisions sur les 31 jours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoad), nameof(ShowsEmpty))]
    public partial int Total { get; private set; }

    /// <summary>Il y a au moins une révision à venir.</summary>
    public bool HasLoad => Total > 0;

    /// <summary>« Aucune révision prévue d'ici 30 jours. » est affiché, une fois la lecture faite.</summary>
    public bool ShowsEmpty => Total == 0 && Days.Count > 0;

    /// <summary>« En moyenne 2,3 par jour · le plus chargé : lun. 12 oct. (9) ».</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; }

    /// <summary>La note sur les rappels de consolidation ; vide sans fiche archivée, ou sur les seuls passages.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    public partial string Note { get; private set; }

    /// <summary>La note est affichée.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>Le jour choisi ; rien tant qu'aucun ne l'est.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial LoadDay? SelectedDay { get; set; }

    /// <summary>Un jour est choisi : ses révisions sont affichées.</summary>
    public bool HasSelection => SelectedDay is not null;

    /// <summary>« Mardi 3 novembre · 3 révisions ».</summary>
    [ObservableProperty]
    public partial string DayTitle { get; private set; }

    /// <summary>Les échéances du jour choisi.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDue))]
    public partial IReadOnlyList<DueItemRow> DueRows { get; private set; }

    /// <summary>Le jour choisi a des échéances.</summary>
    public bool HasDue => DueRows.Count > 0;

    /// <summary>Les étapes projetées du jour choisi.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProjected))]
    public partial IReadOnlyList<ProjectedRow> ProjectedRows { get; private set; }

    /// <summary>Le jour choisi a des étapes projetées.</summary>
    public bool HasProjected => ProjectedRows.Count > 0;

    /// <summary>L'échéance en cours de déplacement.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMoving), nameof(MoveLabel))]
    [NotifyCanExecuteChangedFor(nameof(MoveCommand))]
    public partial DueItemRow? ToMove { get; private set; }

    /// <summary>Le choix d'un jour est affiché.</summary>
    public bool IsMoving => ToMove is not null;

    /// <summary>« Déplacer J7 (Mitose) au ».</summary>
    public string MoveLabel => ToMove is { } row ? WorkloadText.MoveLabelOf(row.Step, NameOf(row.Item, row.ActiveRecall)) : string.Empty;

    /// <summary>Le jour visé.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MoveHint))]
    [NotifyCanExecuteChangedFor(nameof(MoveCommand))]
    public partial DateOnly? MoveTo { get; set; }

    /// <summary>Le premier jour où déplacer : demain, un report ne pouvant viser aujourd'hui.</summary>
    [ObservableProperty]
    public partial DateOnly? EarliestMove { get; private set; }

    /// <summary>« Ce jour-là : 3 révisions. » pour le jour visé.</summary>
    public string MoveHint
    {
        get
        {
            if (MoveTo is not { } day)
            {
                return string.Empty;
            }
            if (Days.FirstOrDefault(candidate => candidate.Day == day) is { } aimed)
            {
                return aimed.Total == 0 ? WorkloadText.HintEmpty : WorkloadText.HintOf(aimed.Reviews);
            }
            return Days.Count > 0 && day > Days[^1].Day ? WorkloadText.BeyondChart : string.Empty;
        }
    }

    /// <summary>Un déplacement est en cours.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Ce que le dernier déplacement a fait.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; private set; }

    /// <summary>Un message est affiché.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>Pourquoi la lecture ou le déplacement a été refusé.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string Error { get; private set; }

    /// <summary>Un refus est affiché.</summary>
    public bool HasError => Error.Length > 0;

    private bool CanMove => IsMoving && MoveTo is not null && !IsBusy;

    /// <summary>
    /// Le jour proposé pour alléger <paramref name="day"/> : le moins chargé à trois jours au plus, avant ou après, à partir
    /// de demain. À égalité, le plus proche, puis le plus tardif ; rien si aucun ne convient.
    /// </summary>
    internal static DateOnly? ProposedDay(IReadOnlyList<LoadDay> days, DateOnly day)
    {
        var start = days.ToList().FindIndex(candidate => candidate.Day == day);
        if (start < 0)
        {
            return null;
        }
        LoadDay? best = null;
        var bestDistance = int.MaxValue;
        for (var gap = -NearbyDays; gap <= NearbyDays; gap++)
        {
            var index = start + gap;
            if (gap == 0 || index < 1 || index >= days.Count)
            {
                continue;
            }
            var candidate = days[index];
            var distance = Math.Abs(gap);
            var better = best is null
                || candidate.Total < best.Total
                || (candidate.Total == best.Total && (distance < bestDistance || (distance == bestDistance && candidate.Day > best.Day)));
            if (better)
            {
                (best, bestDistance) = (candidate, distance);
            }
        }
        return best?.Day;
    }

    /// <summary>« aucune révision », « 1 révision », « 3 révisions ».</summary>
    internal static string ReviewsOf(int count) => count == 0 ? WorkloadText.NoReview : Dates.Plural(count, WorkloadText.ReviewsOneOf, WorkloadText.ReviewsManyOf);

    /// <summary>Prépare la vue à s'ouvrir : le filtre de la liste, et de quoi y revenir.</summary>
    internal void Prepare(DashboardFilter filter, Action back)
    {
        _back = back;
        SelectedFilter = Filters.First(choice => choice.Value == filter);
        SelectedDay = null;
        ToMove = null;
        MoveTo = null;
        Message = string.Empty;
        Error = string.Empty;
    }

    /// <inheritdoc/>
    public void OnNavigatedTo() => RefreshCommand.Execute(null);

    /// <summary>Oublie tout ce qui vient du coffre ; le filtre et la largeur restent, la prochaine ouverture relit le coffre.</summary>
    public void Forget()
    {
        _generation++;
        _reading++;
        _dashboard = null;
        SelectedDay = null;
        Days = [];
        Total = 0;
        OnPropertyChanged(nameof(ShowsEmpty));
        Summary = string.Empty;
        Note = string.Empty;
        DayTitle = string.Empty;
        DueRows = [];
        ProjectedRows = [];
        ToMove = null;
        MoveTo = null;
        EarliestMove = null;
        Message = string.Empty;
        Error = string.Empty;
        IsLoading = false;
        IsBusy = false;
    }

    /// <summary>Relit le tableau de bord, hors du fil de l'interface ; seule la dernière lecture demandée s'affiche.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RefreshAsync()
    {
        var reading = ++_reading;
        IsLoading = _dashboard is null;
        var outcome = await Task.Run(_reminders.Dashboard).ConfigureAwait(true);
        if (reading != _reading)
        {
            return;
        }
        IsLoading = false;
        if (outcome.Succeeded)
        {
            _dashboard = outcome.Value;
            Rebuild();
        }
        else
        {
            Error = outcome.Error!;
        }
    }

    /// <summary>Retour à la vue d'où la charge a été ouverte.</summary>
    [RelayCommand]
    private void Back() => _back?.Invoke();

    /// <summary>Ouvre une échéance, pour la réviser.</summary>
    [RelayCommand]
    private void Open(DueItemRow row) => _opener.Open(row.Item, row.ActiveRecall);

    /// <summary>« Déplacer… » : le choix d'un jour paraît, sur le jour le moins chargé alentour.</summary>
    [RelayCommand]
    private void PrepareMove(DueItemRow row)
    {
        var tomorrow = _dashboard!.Day.AddDays(1);
        EarliestMove = tomorrow;
        ToMove = row;
        MoveTo = (SelectedDay is { } day ? ProposedDay(Days, day.Day) : null) ?? tomorrow;
        Message = string.Empty;
        Error = string.Empty;
    }

    /// <summary>Referme le choix d'un jour.</summary>
    [RelayCommand]
    private void CancelMove()
    {
        ToMove = null;
        MoveTo = null;
    }

    /// <summary>Déplace l'échéance au jour visé (un report), puis relit le tableau de bord.</summary>
    [RelayCommand(CanExecute = nameof(CanMove))]
    private async Task MoveAsync()
    {
        var (row, day, generation) = (ToMove!, MoveTo!.Value, _generation);
        IsBusy = true;
        Error = string.Empty;
        try
        {
            var outcome = await Task.Run(() => _reviews.Postpone(row.Item.Kind, row.Item.Id, day)).ConfigureAwait(true);
            if (generation != _generation)
            {
                return;
            }
            if (!outcome.Succeeded)
            {
                Error = outcome.Error!;
                return;
            }
            Message = WorkloadText.MovedOf(row.Step, NameOf(row.Item, row.ActiveRecall), Dates.Long(day));
            ToMove = null;
            MoveTo = null;
            await RefreshAsync().ConfigureAwait(true);
        }
        finally
        {
            if (generation == _generation)
            {
                IsBusy = false;
            }
        }
    }

    partial void OnSelectedFilterChanged(Choice<DashboardFilter> value)
    {
        ToMove = null;
        Rebuild();
    }

    partial void OnChartWidthChanged(double oldValue, double newValue)
    {
        if (ColumnsFor(oldValue) != ColumnsFor(newValue))
        {
            Rebuild();
        }
    }

    partial void OnSelectedDayChanged(LoadDay? oldValue, LoadDay? newValue)
    {
        oldValue?.IsSelected = false;
        newValue?.IsSelected = true;
        ShowDetail();
        if (!_rebuilding)
        {
            ToMove = null;
            MoveTo = null;
            Message = string.Empty;
            Error = string.Empty;
        }
    }

    private static int ColumnsFor(double width) => width >= OneRowMinimum ? WideColumns : NarrowColumns;

    /// <summary>Un jour réduit à ce que le filtre montre.</summary>
    private static WorkloadDay Filtered(WorkloadDay day, DashboardFilter filter) => filter switch
    {
        DashboardFilter.Sheets => day with { Due = [.. day.Due.Where(item => item.Kind == ItemKind.Sheet)], Projected = [.. day.Projected.Where(item => item.Kind == ItemKind.Sheet)] },
        DashboardFilter.Passages => day with { Due = [.. day.Due.Where(item => item.Kind == ItemKind.Passage)], Projected = [.. day.Projected.Where(item => item.Kind == ItemKind.Passage)] },
        _ => day,
    };

    /// <summary>Le terme, le passage ou le titre ; un passage caché par le rappel actif n'est que nommé par sa fiche.</summary>
    private static string NameOf(DueItem item, bool activeRecall) =>
        item.Term
        ?? (activeRecall && item.Excerpt is not null ? WorkloadText.HiddenPassageOf(item.Title)
            : item.Excerpt is { } excerpt ? $"« {excerpt} »"
            : item.Title);

    /// <summary>Refait les colonnes, le résumé et le détail ; le jour choisi reste choisi.</summary>
    private void Rebuild()
    {
        if (_dashboard is not { } dashboard)
        {
            return;
        }
        var filter = SelectedFilter.Value;
        ImmutableArray<WorkloadDay> days = [.. dashboard.Workload.Select(day => Filtered(day, filter))];
        var totals = days.Select(day => day.Due.Length + day.Projected.Length).ToList();
        var sum = totals.Sum();
        var most = Math.Max(1, totals.Max());
        var average = (double)sum / days.Length;
        var tallest = ChartColumns == WideColumns ? TallBar : ShortBar;
        double Height(double count) => count == 0 ? 0 : Math.Max(SmallestBar, count / most * tallest);
        var peak = sum == 0 ? -1 : totals.IndexOf(totals.Max());
        var kept = SelectedDay?.Day;
        _rebuilding = true;
        try
        {
            Days = [.. days.Select((day, index) => new LoadDay(day, index == 0, index == peak, Height, average, tallest + ValueRoom))];
            SelectedDay = Days.FirstOrDefault(day => day.Day == kept);
            Total = sum;
            OnPropertyChanged(nameof(ShowsEmpty));
            ShowDetail();
        }
        finally
        {
            _rebuilding = false;
        }
        var averageText = WorkloadText.AverageOf(average.ToString("0.#", French));
        Summary = sum == 0 ? string.Empty : WorkloadText.PeakOf(averageText, Dates.Short(days[peak].Day), totals[peak]);
        Note = filter != DashboardFilter.Passages && dashboard.ArchivedSheets > 0
            ? Dates.Plural(dashboard.RemindersPerDay, WorkloadText.RemindersOneOf, WorkloadText.RemindersManyOf)
            : string.Empty;
        OnPropertyChanged(nameof(MoveHint));
    }

    private void ShowDetail()
    {
        if (SelectedDay is not { } day || _dashboard is not { } dashboard)
        {
            DayTitle = string.Empty;
            DueRows = [];
            ProjectedRows = [];
            return;
        }
        DayTitle = WorkloadText.DayTitleOf(Dates.Capitalized(Dates.Long(day.Day)), day.Reviews);
        DueRows = [.. day.Source.Due.Select(item => new DueItemRow(item, dashboard.ActiveRecall, showDueDate: false))];
        ProjectedRows = [.. day.Source.Projected.Select(item => new ProjectedRow(item, dashboard.ActiveRecall))];
    }
}
