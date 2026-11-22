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
public sealed partial class DashboardPageViewModel : ObservableObject, INavigationAware, IForgetsVault
{
    private static readonly StringComparer FrenchTitles = StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), ignoreCase: false);

    private readonly ReminderUseCases _reminders;
    private readonly SheetUseCases _sheets;
    private readonly IReviewOpener _opener;
    private readonly DashboardGroup _examGroup;
    private readonly DashboardGroup _overdue;
    private readonly DashboardGroup _today;
    private readonly DashboardGroup _reminderGroup;
    private readonly DashboardGroup _upcoming;
    private readonly DashboardExtras _extras;
    private readonly IEnumerable<IDashboardWidget> _widgets;
    private readonly ReviewUseCases? _reviews;
    private IReadOnlyList<DashboardCard> _extensionCards = [];
    private DueItemRow? _toArchive;
    private Dashboard? _dashboard;
    private static readonly Choice<string> AllScopes = new(string.Empty, DashboardText.AllScopes);

    private readonly DashboardGroup _suspended;
    private IReadOnlyList<DueItem> _toStart = [];

    /// <summary>Numéro du dernier aperçu demandé : une lecture dépassée est ignorée.</summary>
    private int _previewTicket;

    /// <summary>Nombre d'oublis du coffre : une lecture partie avant le dernier n'y remet rien.</summary>
    private int _generation;

    /// <summary>
    /// La vue, sur les cas d'usage des rappels (qui rendent le tableau de bord) et des fiches (qui
    /// nourrissent l'aperçu) ; <paramref name="opener"/> ouvre ses lignes, <paramref name="widgets"/> sont
    /// les encarts apportés par les modules, <paramref name="time"/> l'horloge d'où part le formulaire d'un examen.
    /// </summary>
    public DashboardPageViewModel(ReminderUseCases reminders, SheetUseCases sheets, IReviewOpener opener, IEnumerable<IDashboardWidget>? widgets = null, ReviewUseCases? reviews = null, ExamUseCases? exams = null, FolderUseCases? folders = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(opener);
        _reminders = reminders;
        _sheets = sheets;
        _opener = opener;
        _widgets = widgets ?? [];
        _reviews = reviews;
        _extras = new DashboardExtras(this);
        Exams = exams is not null && folders is not null ? new DashboardExams(exams, folders, RefreshAsync, time ?? TimeProvider.System) : null;
        Spread = exams is not null ? new DashboardSpread(exams, RefreshAsync, time ?? TimeProvider.System) : null;
        _examGroup = new DashboardGroup(ExamText.Exams, Icons.Calendar, DashboardTone.Normal, expanded: true, BuildRows);
        _overdue = new DashboardGroup(DashboardText.Overdue, Icons.Clock, DashboardTone.Overdue, expanded: true, BuildRows);
        _today = new DashboardGroup(DashboardText.Today, Icons.Calendar, DashboardTone.Normal, expanded: true, BuildRows);
        _reminderGroup = new DashboardGroup(DashboardText.Reminders, Icons.Sync, DashboardTone.Reminder, expanded: true, BuildRows);
        _upcoming = new DashboardGroup(DashboardText.Upcoming, Icons.Calendar, DashboardTone.Normal, expanded: true, BuildRows);
        _suspended = new DashboardGroup(DashboardText.Suspended, Icons.Clock, DashboardTone.Normal, expanded: false, BuildRows);
        Tiles = [new(DayTileKind.Overdue, DashboardText.Overdue), new(DayTileKind.Today, DashboardText.Today), new(DayTileKind.Reminders, DashboardText.RemindersShort)];
        Scopes = [AllScopes];
        SelectedScope = AllScopes;
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
        Progress = string.Empty;
    }

    /// <summary>« Aujourd'hui ».</summary>
    public static string Title => DashboardText.Today;

    /// <summary>Les examens : les fixer, les retirer, resserrer les échéances ; rien sans leurs cas d'usage.</summary>
    public DashboardExams? Exams { get; }

    /// <summary>L'étalement des échéances : un retard à rattraper, une pause à prévoir ; rien sans ses cas d'usage.</summary>
    public DashboardSpread? Spread { get; }

    /// <summary>« Étaler… » est proposé.</summary>
    public bool CanSpread => Spread is not null;

    /// <summary>La fenêtre « Nouvelle fiche » est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsCreatingSheet { get; private set; }

    /// <summary>Le titre de la fiche à créer.</summary>
    [ObservableProperty]
    public partial string NewSheetTitle { get; set; } = string.Empty;

    /// <summary>Le refus de la création ; rien si elle n'a pas été refusée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNewSheetError))]
    public partial string? NewSheetError { get; private set; }

    /// <summary>La création a été refusée.</summary>
    public bool HasNewSheetError => NewSheetError is not null;

    /// <summary>
    /// Où la fiche sera créée : le dossier choisi dans la liste « dossier ou étiquette », sinon la racine. La fenêtre le dit.
    /// </summary>
    public string NewSheetPlace => NewSheetFolder.Length == 0
        ? DashboardText.NewSheetAtRoot
        : DashboardText.NewSheetInOf(NewSheetFolder.Replace("/", " › ", StringComparison.Ordinal));

    private string NewSheetFolder => SelectedScope?.Value is { } scope && scope.StartsWith(FolderScope, StringComparison.Ordinal) ? scope[FolderScope.Length..] : string.Empty;

    /// <summary>Ouvre la fenêtre « Nouvelle fiche ».</summary>
    [RelayCommand]
    private void NewSheet()
    {
        NewSheetTitle = string.Empty;
        NewSheetError = null;
        OnPropertyChanged(nameof(NewSheetPlace));
        IsCreatingSheet = true;
    }

    /// <summary>La réponse de la fenêtre : la fiche créée s'ouvre en écriture ; un refus laisse la fenêtre ouverte, avec sa raison.</summary>
    [RelayCommand]
    private async Task AnswerNewSheetAsync(bool confirmed)
    {
        IsCreatingSheet = false;
        if (!confirmed)
        {
            NewSheetError = null;
            return;
        }
        var title = NewSheetTitle.Trim();
        if (title.Length == 0)
        {
            NewSheetError = SheetsText.TitleRequired;
            IsCreatingSheet = true;
            return;
        }
        var folder = NewSheetFolder;
        var generation = _generation;
        var outcome = await Task.Run(() => _sheets.Create(folder.Length == 0 ? null : folder, title, null)).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant la création : la fenêtre ne se rouvre pas, et la fiche ne s'ouvre pas.
            return;
        }
        if (!outcome.Succeeded)
        {
            NewSheetError = outcome.Error;
            IsCreatingSheet = true;
            return;
        }
        NewSheetError = null;
        _opener.WriteSheet(outcome.Value);
    }

    /// <summary>« Examen… » est proposé.</summary>
    public bool CanPlanExams => Exams is not null;

    /// <summary>Filtres proposés.</summary>
    public IReadOnlyList<Choice<DashboardFilter>> Filters { get; }

    /// <summary>
    /// Le filtre écrit (rangée 26C) : « fiche » ou « passage » choisit le genre, un nom de dossier ou d'étiquette y restreint
    /// la liste (la nouvelle fiche va alors dans ce dossier), le reste cherche dans les titres, les passages, les dossiers et
    /// les étiquettes, sans tenir compte des accents ni des majuscules. La séance suit la liste.
    /// </summary>
    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    /// <summary>Les mots du filtre écrit qui ne sont ni un genre ni un dossier ou une étiquette, repliés.</summary>
    private List<string> _words = [];

    /// <summary>Les tuiles du jour : en retard, aujourd'hui, rappels.</summary>
    public IReadOnlyList<DayTile> Tiles { get; }

    /// <summary>Les tuiles se montrent : il y a quelque chose à réviser aujourd'hui, filtre ou non.</summary>
    [ObservableProperty]
    public partial bool HasTiles { get; private set; }

    /// <summary>La part de la journée déjà faite, de 0 à 1.</summary>
    [ObservableProperty]
    public partial double DoneShare { get; private set; }

    /// <summary>« 1 sur 5 fait · prochain : Zèbre ».</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProgress))]
    public partial string Progress { get; private set; }

    /// <summary>La journée a quelque chose à faire ou de déjà fait.</summary>
    public bool HasProgress => Progress.Length > 0;

    /// <summary>La tuile choisie ; aucune : tout se montre.</summary>
    private DayTileKind? Chosen => Tiles.FirstOrDefault(tile => tile.IsChosen)?.Kind;

    /// <summary>Choisit une tuile, ou rend tout si elle l'était déjà ; une tuile vide ne se choisit pas.</summary>
    [RelayCommand]
    private void ChooseTile(DayTile? tile)
    {
        if (tile is null || (tile.Count == 0 && !tile.IsChosen))
        {
            return;
        }
        var chosen = !tile.IsChosen;
        foreach (var each in Tiles)
        {
            each.IsChosen = chosen && ReferenceEquals(each, tile);
        }
        BuildRows();
    }

    partial void OnQueryChanged(string value)
    {
        ApplyQuery();
        BuildRows();
    }

    /// <summary>Lit le filtre écrit : le genre, le dossier ou l'étiquette nommés, et les mots qui restent.</summary>
    private void ApplyQuery()
    {
        var words = Query.Split([' ', ',', ';', '	'], StringSplitOptions.RemoveEmptyEntries).Select(ShellViewModel.Fold).ToList();
        var sheets = words.RemoveAll(word => word is "fiche" or "fiches") > 0;
        var passages = words.RemoveAll(word => word is "passage" or "passages") > 0;
        var rest = string.Join(' ', words);
        var scope = rest.Length == 0 ? null : Scopes.Skip(1).FirstOrDefault(choice => NamesOf(choice).Contains(rest, StringComparer.Ordinal));
        _applyingQuery = true;
        try
        {
            SelectedFilter = Filters.First(choice => choice.Value == (sheets == passages ? DashboardFilter.All : sheets ? DashboardFilter.Sheets : DashboardFilter.Passages));
            SelectedScope = scope ?? AllScopes;
        }
        finally
        {
            _applyingQuery = false;
        }
        _words = scope is null ? words : [];
    }

    /// <summary>Comment s'écrit un dossier (son chemin, son nom) ou une étiquette (avec ou sans #), replié.</summary>
    private static IEnumerable<string> NamesOf(Choice<string> scope)
    {
        if (scope.Value.StartsWith(FolderScope, StringComparison.Ordinal))
        {
            var folder = scope.Value[FolderScope.Length..];
            yield return ShellViewModel.Fold(folder);
            yield return ShellViewModel.Fold(folder[(folder.LastIndexOf('/') + 1)..]);
            yield break;
        }
        var tag = ShellViewModel.Fold(scope.Value[TagScope.Length..]);
        yield return tag;
        yield return "#" + tag;
    }

    /// <summary>Chaque mot du filtre écrit se trouve dans le titre, le passage, le terme, le dossier ou les étiquettes.</summary>
    private bool Matches(params IEnumerable<string?> parts)
    {
        if (_words.Count == 0)
        {
            return true;
        }
        var text = ShellViewModel.Fold(string.Join(' ', parts.Where(part => part is not null)));
        return _words.All(word => text.Contains(word, StringComparison.Ordinal));
    }

    private bool _applyingQuery;

    /// <summary>
    /// Dossiers et étiquettes de ce qui est à réviser, « Tout » d'abord : en choisir un y restreint la liste, donc la séance.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<Choice<string>> Scopes { get; private set; }

    /// <summary>Le dossier ou l'étiquette choisi ; « Tout » sinon.</summary>
    [ObservableProperty]
    public partial Choice<string>? SelectedScope { get; set; }

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

    /// <summary>La carte « Premiers pas », montrée à la place de l'état vide dans un coffre qui débute ; rien : jamais montrée.</summary>
    public FirstSteps? FirstSteps { get; init; }

    /// <summary>Les encarts des extensions, demandés à leur code après ceux des modules.</summary>
    public Func<Task<IReadOnlyList<DashboardCard>>>? ExtensionCards { get; set; }

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
    [NotifyPropertyChangedFor(nameof(StartHint))]
    public partial bool CanStart { get; private set; }

    /// <summary>Pourquoi « Commencer » est grisé ; rien quand il ne l'est pas.</summary>
    public string? StartHint => CanStart ? null : DashboardText.NothingToStart;

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
    public void OnNavigatedTo()
    {
        Exams?.ClearFeedback();
        Spread?.ClearFeedback();
        RefreshCommand.Execute(null);
    }

    /// <summary>
    /// Oublie tout ce qui vient du coffre : la liste, la ligne choisie et son aperçu, le résumé du jour, les jours à venir,
    /// la semaine, les encarts, les avertissements, la séance à commencer ou à reprendre et la mise aux archives en
    /// attente. Le filtre, le tri, la largeur et les groupes repliés restent ; la prochaine ouverture relit le coffre.
    /// </summary>
    public void Forget()
    {
        _generation++;
        _previewTicket++;
        _dashboard = null;
        OnPropertyChanged(nameof(Dashboard));
        _toStart = [];
        _toArchive = null;
        _extensionCards = [];
        IsConfirmingArchive = false;
        SelectedRow = null;
        Preview = null;
        Exams?.Forget();
        Spread?.Forget();
        IsCreatingSheet = false;
        NewSheetTitle = string.Empty;
        NewSheetError = null;
        _examGroup.Count = 0;
        _overdue.Count = 0;
        _today.Count = 0;
        _reminderGroup.Count = 0;
        _upcoming.Count = 0;
        _suspended.Count = 0;
        Scopes = [AllScopes];
        SelectedScope = AllScopes;
        Query = string.Empty;
        foreach (var tile in Tiles)
        {
            tile.IsChosen = false;
            tile.Count = 0;
        }
        HasTiles = false;
        DoneShare = 0;
        Progress = string.Empty;
        Rows = [];
        SecondRows = [];
        Subtitle = string.Empty;
        DayLabel = string.Empty;
        ReviewCount = 0;
        ReviewNoun = string.Empty;
        Chips = [];
        SideReminders = [];
        Forecast = [];
        Week = [];
        WeekTotal = 0;
        WeekNoun = string.Empty;
        Cards = [];
        Footer = string.Empty;
        HealthWarning = null;
        PriorityWarning = null;
        Error = null;
        IsLoading = false;
        StartLabel = string.Empty;
        CanStart = false;
        ResumeLabel = string.Empty;
        CanResume = false;
    }

    /// <summary>Relit le tableau de bord (la journée est préparée de nouveau après minuit).</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        // Le coffre oublié pendant la lecture : elle n'y remet rien.
        var generation = _generation;
        IsLoading = _dashboard is null;
        try
        {
            // Le calcul lit tout le coffre : hors du fil de l'interface.
            var (outcome, cards) = await Task.Run(() => (_reminders.Dashboard(), ReadCards())).ConfigureAwait(true);
            if (generation != _generation)
            {
                return;
            }
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
            if (generation == _generation)
            {
                IsLoading = false;
            }
        }
        if (LookForNews is { } lookForNews && await lookForNews().ConfigureAwait(true))
        {
            var news = await Task.Run(ReadCards).ConfigureAwait(true);
            if (generation != _generation)
            {
                return;
            }
            Cards = [.. news, .. _extensionCards];
        }
        if (ExtensionCards is { } extensionCards)
        {
            var added = await extensionCards().ConfigureAwait(true);
            if (generation != _generation)
            {
                return;
            }
            Cards = [.. Cards.Where(card => !_extensionCards.Contains(card)), .. added];
            _extensionCards = added;
        }
    }

    /// <summary>La mise aux archives attend sa confirmation.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingArchive { get; private set; }

    /// <summary>Les actions de révision sont disponibles depuis le menu d'une ligne.</summary>
    internal bool CanAct => _reviews is not null;

    /// <summary>Ouvre « Santé du coffre » : ce que l'avertissement du jour annonce s'y lit en détail.</summary>
    [RelayCommand]
    private void OpenHealth() => _opener.OpenHealth();

    /// <summary>Ouvre la charge des 30 prochains jours, avec le filtre de la liste.</summary>
    [RelayCommand]
    private void OpenWorkload() => _opener.OpenWorkload(SelectedFilter.Value);

    /// <summary>Ouvre la charge sur un jour de « À venir », choisi et détaillé.</summary>
    [RelayCommand]
    private void OpenWorkloadOn(ForecastDay? day)
    {
        if (day is not null)
        {
            _opener.OpenWorkload(SelectedFilter.Value, day.Day);
        }
    }

    /// <summary>Les actions communes sur une fiche, pour son clic droit (rangée 23A) ; rien sans elles.</summary>
    public SheetActionsViewModel? SheetActions { get; set; }

    /// <summary>Le menu d'une fiche ; la vue peut l'ouvrir ou la réviser à sa façon.</summary>
    internal ISheetMenuHost SheetMenuHost(Action<SheetTarget>? read = null, Action<SheetTarget>? revise = null) => new SharedSheetMenu(SheetActions, _opener, read, revise);

    /// <summary>Ouvre la fiche d'une ligne, pour la relire.</summary>
    internal void OpenSheetOf(DueItemRow row) => _opener.OpenSheet(row.Item.SheetId);

    /// <summary>Ouvre la fiche d'une ligne sur sa fenêtre « Imprimer ».</summary>
    internal void PrintSheetOf(DueItemRow row) => _opener.PrintSheet(row.Item.SheetId);

    /// <summary>Reporte une ligne à demain, puis relit le tableau de bord.</summary>
    internal async Task PostponeTomorrowAsync(DueItemRow row)
    {
        if (_reviews is null || _dashboard is null)
        {
            return;
        }
        var (item, reviews, tomorrow) = (row.Item, _reviews, _dashboard.Day.AddDays(1));
        await ActOnRowAsync(() => reviews.Postpone(item.Kind, item.Id, tomorrow).Error).ConfigureAwait(true);
    }

    /// <summary>Demande confirmation avant d'archiver la fiche d'une ligne.</summary>
    internal void RequestArchive(DueItemRow row)
    {
        _toArchive = row;
        IsConfirmingArchive = true;
    }

    /// <summary>La réponse à la mise aux archives.</summary>
    [RelayCommand]
    private async Task AnswerArchiveAsync(bool confirmed)
    {
        IsConfirmingArchive = false;
        if (confirmed && _toArchive is { } row && _reviews is { } reviews)
        {
            await ActOnRowAsync(() => reviews.Archive(row.Item.SheetId).Error).ConfigureAwait(true);
        }
    }

    private async Task ActOnRowAsync(Func<string?> action)
    {
        var generation = _generation;
        var error = await Task.Run(action).ConfigureAwait(true);
        // Le coffre oublié pendant l'action : sa réponse n'y remet rien, et rien n'est relu.
        if (generation != _generation)
        {
            return;
        }
        Error = error;
        if (Error is null)
        {
            await RefreshAsync().ConfigureAwait(true);
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

    partial void OnSelectedFilterChanged(Choice<DashboardFilter> value)
    {
        if (!_applyingQuery)
        {
            BuildRows();
        }
    }

    partial void OnSelectedScopeChanged(Choice<string>? value)
    {
        // Pendant que la liste des choix se refait, la sélection passe par rien : les lignes attendent.
        if (!_buildingScopes && !_applyingQuery && value is not null)
        {
            BuildRows();
        }
    }

    /// <summary>
    /// Refait les choix de dossier et d'étiquette d'après ce qui est à réviser, sans perdre celui qui est fait s'il existe
    /// encore ; disparu, on revient à « Tout ».
    /// </summary>
    private void BuildScopes(Dashboard dashboard)
    {
        var items = dashboard.Overdue.Concat(dashboard.Today).Concat(dashboard.Upcoming).ToList();
        var folders = items
            .SelectMany(item => Ancestors(item.Folder))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.CurrentCulture)
            .Select(folder => new Choice<string>(FolderScope + folder, DashboardText.FolderScopeOf(folder.Replace("/", " › ", StringComparison.Ordinal))));
        var tags = items
            .SelectMany(item => item.Tags)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.CurrentCulture)
            .Select(tag => new Choice<string>(TagScope + tag, DashboardText.TagScopeOf(tag)));
        var kept = SelectedScope?.Value ?? string.Empty;
        Choice<string>[] scopes = [AllScopes, .. folders, .. tags];
        if (scopes.SequenceEqual(Scopes) && SelectedScope is not null)
        {
            return;
        }
        _buildingScopes = true;
        try
        {
            Scopes = scopes;
            SelectedScope = Scopes.FirstOrDefault(scope => scope.Value == kept) ?? AllScopes;
        }
        finally
        {
            _buildingScopes = false;
        }
    }

    /// <summary>Un dossier et ceux qui le contiennent : « Biologie/Cellule » donne « Biologie » et « Biologie/Cellule ».</summary>
    private static IEnumerable<string> Ancestors(string folder)
    {
        for (var end = folder.IndexOf('/', StringComparison.Ordinal); end >= 0; end = folder.IndexOf('/', end + 1))
        {
            yield return folder[..end];
        }
        if (folder.Length > 0)
        {
            yield return folder;
        }
    }

    /// <summary>Ce qui est dans ce dossier (ou un dossier qu'il contient), ou porte cette étiquette, est dans le choix ; tout l'est sans choix.</summary>
    private bool InScope(string folder, IEnumerable<string> tags)
    {
        var scope = SelectedScope?.Value ?? string.Empty;
        if (scope.StartsWith(FolderScope, StringComparison.Ordinal))
        {
            var chosen = scope[FolderScope.Length..];
            return folder == chosen || folder.StartsWith(chosen + "/", StringComparison.Ordinal);
        }
        return !scope.StartsWith(TagScope, StringComparison.Ordinal) || tags.Contains(scope[TagScope.Length..], StringComparer.Ordinal);
    }

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

    /// <summary>
    /// La ligne à qui rendre le focus après une action sur un examen : celle de son dossier ; retiré, l'en-tête du groupe
    /// s'il en reste d'autres ; rien s'il n'en reste aucun (le focus va alors à « Examen… »).
    /// </summary>
    internal object? ExamRowToFocus() =>
        (object?)Rows.OfType<ExamRow>().FirstOrDefault(row => row.Exam.Folder == Exams?.LastFolder)
        ?? (Rows.OfType<ExamRow>().Any() ? _examGroup : null);

    private List<DashboardCard> ReadCards() => [.. _widgets.OrderBy(widget => widget.Order).Select(widget => widget.Card()).OfType<DashboardCard>()];

    private void BuildRows()
    {
        if (_dashboard is { } dashboard)
        {
            BuildRows(dashboard);
        }
    }

    /// <summary>La carte suit ce tableau de bord, et dit si elle se montre ; son bouton et son lien « Masquer » agissent sur cette vue.</summary>
    private bool FollowedBy(FirstSteps steps, Dashboard dashboard)
    {
        steps.NewSheet ??= () => NewSheetCommand.Execute(null);
        steps.Hidden ??= BuildRows;
        return steps.Follow(dashboard);
    }

    private void BuildRows(Dashboard dashboard)
    {
        BuildScopes(dashboard);
        var overdue = Prepare(dashboard.Overdue);
        var today = Prepare(dashboard.Today);
        var upcoming = Prepare(dashboard.Upcoming);
        List<PendingReminder> reminders = SelectedFilter.Value == DashboardFilter.Passages ? [] : [.. dashboard.Reminders.Where(reminder => InScope(reminder.Folder, reminder.Tags)).Where(reminder => Matches(reminder.Title, reminder.Folder, string.Join(' ', reminder.Tags)))];
        ShowTiles(dashboard, overdue, today, reminders);
        // Une tuile choisie ne garde que les siens : dans la liste, dans la séance, dans le résumé.
        var chosen = Chosen;
        if (chosen is { } kind)
        {
            overdue = kind == DayTileKind.Overdue ? overdue : [];
            today = kind == DayTileKind.Today ? today : [];
            reminders = kind == DayTileKind.Reminders ? reminders : [];
            upcoming = [];
        }
        var total = overdue.Count + today.Count + reminders.Count;
        _toStart = [.. overdue, .. today];
        CanStart = _toStart.Count > 0;
        StartLabel = CanStart ? SessionText.StartOf(_toStart.Count) : SessionText.StartNothing;
        Progress = ProgressOf(dashboard);

        // Les actions vont à leur section : « Étaler… » à la première de la journée (un retard à rattraper, sinon une pause
        // à prévoir), « Examen… » aux examens, qui se montrent même sans examen pour en fixer un.
        var spread = Spread is { } spreading ? new GroupAction(SpreadText.Action, SpreadText.ActionName, spreading.OpenCommand) : null;
        var spreadHere = new[] { (Group: _overdue, overdue.Count), (Group: _today, today.Count), (Group: _upcoming, upcoming.Count) }.FirstOrDefault(group => group.Count > 0).Group;
        foreach (var group in new[] { _overdue, _today, _upcoming })
        {
            group.Action = ReferenceEquals(group, spreadHere) ? spread : null;
        }
        _examGroup.Action = Exams is { } exams ? new GroupAction(ExamText.NewExam, ExamText.NewExamName, exams.NewCommand) : null;

        var rows = new List<object>();
        if (chosen is null)
        {
            Add(rows, _examGroup, dashboard.Exams.Select(exam => new ExamRow(exam)), always: Exams is not null);
        }
        // Les premiers pas se cochent d'après ce tableau de bord, que la carte se montre ou non.
        var showsFirstSteps = FirstSteps is { } steps && FollowedBy(steps, dashboard);
        // Une tuile choisie a toujours quelque chose : ni premiers pas ni état vide avec elle.
        if (total == 0 && showsFirstSteps)
        {
            rows.Add(FirstSteps!);
        }
        else if (total == 0)
        {
            // Le filtre cache ce qui est dû : c'est lui qu'il faut dire, pas un coffre vide.
            var hidden = HasTiles && (Query.Length > 0 || SelectedFilter.Value != DashboardFilter.All || SelectedScope != AllScopes);
            rows.Add(new EmptyDashboardRow(
                hidden ? DashboardText.NothingMatches
                : dashboard.NextDueDate is { } next ? DashboardText.NextReviewOf(Dates.Long(next))
                : dashboard.ActiveSheets + dashboard.ArchivedSheets == 0 ? DashboardText.FirstSheet
                : DashboardText.AllArchived));
        }
        // En deux cartes : les retards et les rappels d'un côté, aujourd'hui et à venir de l'autre.
        var second = Layout == TodayLayout.WithSplit ? [] : rows;
        Add(rows, _overdue, overdue.Select(item => new DueItemRow(item, dashboard.ActiveRecall, showDueDate: false)));
        Add(second, _today, today.Select(item => new DueItemRow(item, dashboard.ActiveRecall, showDueDate: false)));
        var hasSide = Layout is TodayLayout.WithSide or TodayLayout.WithPreview;
        // Les rappels choisis par leur tuile viennent dans la liste, même quand la colonne latérale les montre d'habitude.
        var remindersAside = hasSide && chosen != DayTileKind.Reminders;
        Add(rows, _reminderGroup, (remindersAside ? [] : reminders).Select(reminder => new ReminderRow(reminder)));
        Add(second, _upcoming, upcoming.Select(item => new DueItemRow(item, dashboard.ActiveRecall, showDueDate: true)));
        // Les suspendus ne sont ni dus ni comptés : repliés, ils attendent qu'on les ouvre pour les reprendre.
        Add(second, _suspended, (chosen is null ? Prepare(dashboard.Suspended) : []).Select(item => new DueItemRow(item, dashboard.ActiveRecall, showDueDate: true)));
        SideReminders = remindersAside ? [.. reminders.Select(reminder => new ReminderRow(reminder))] : [];
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
        if (reminders.Count > 0)
        {
            chips.Add(new(PillKind.Reminder, Dates.Plural(reminders.Count, DashboardText.ReminderChipOneOf, DashboardText.ReminderChipManyOf)));
        }
        Chips = chips;
        Subtitle = $"{Dates.Capitalized(Dates.Long(dashboard.Day))} · {(total == 0 ? DashboardText.NothingToReview : Dates.Plural(total, DashboardText.ItemsToReviewOneOf, DashboardText.ItemsToReviewManyOf))}";
        HealthWarning = dashboard.HealthProblems > 0 ? Dates.Plural(dashboard.HealthProblems, DashboardText.HealthWarningOneOf, DashboardText.HealthWarningManyOf) : null;
        PriorityWarning = dashboard.UnservedPriorities > 0 ? Dates.Plural(dashboard.UnservedPriorities, DashboardText.PriorityWarningOneOf, DashboardText.PriorityWarningManyOf) : null;
        var footer = $"{Dates.Plural(dashboard.ActiveSheets, DashboardText.SheetsActiveOneOf, DashboardText.SheetsActiveManyOf)} · {Dates.Plural(dashboard.ArchivedSheets, DashboardText.SheetsArchivedOneOf, DashboardText.SheetsArchivedManyOf)}";
        Footer = dashboard.NextDueDate is { } nextDue && total > 0 ? $"{footer} · {DashboardText.NextDueOf(Dates.Short(nextDue))}" : footer;
    }

    /// <summary>
    /// Compte les tuiles d'après la liste filtrée ; une tuile choisie qui se vide se relâche. Elles se montrent dès que la
    /// journée a quelque chose, même si le filtre n'en garde rien.
    /// </summary>
    private void ShowTiles(Dashboard dashboard, List<DueItem> overdue, List<DueItem> today, List<PendingReminder> reminders)
    {
        foreach (var tile in Tiles)
        {
            tile.Count = tile.Kind switch
            {
                DayTileKind.Overdue => overdue.Count,
                DayTileKind.Today => today.Count,
                _ => reminders.Count,
            };
            tile.IsChosen &= tile.Count > 0;
        }
        HasTiles = dashboard.Overdue.Length + dashboard.Today.Length + dashboard.Reminders.Length > 0;
    }

    /// <summary>
    /// « 1 sur 5 fait · prochain : Zèbre » : les révisions validées aujourd'hui sur la journée entière (sans filtre), et
    /// ce que la séance ouvrira d'abord ; rien si la journée n'a rien eu.
    /// </summary>
    private string ProgressOf(Dashboard dashboard)
    {
        var done = dashboard.RecentReviews.FirstOrDefault(day => day.Day == dashboard.Day)?.Reviews ?? 0;
        var day = done + dashboard.Overdue.Length + dashboard.Today.Length + dashboard.Reminders.Length;
        DoneShare = day == 0 ? 0 : (double)done / day;
        if (day == 0)
        {
            return string.Empty;
        }
        var part = done > 1 ? DashboardText.ProgressManyOf(done, day) : DashboardText.ProgressOneOf(done, day);
        return _toStart.Count > 0 ? $"{part} · {DashboardText.NextOf(_toStart[0].Title)}"
            : done == day ? $"{part} · {DashboardText.DayDone}"
            : part;
    }

    /// <summary>L'en-tête d'un groupe non vide (ou vide, s'il se montre <paramref name="always"/>), et ses lignes s'il est déplié.</summary>
    private static void Add(List<object> rows, DashboardGroup group, IEnumerable<object> items, bool always = false)
    {
        var all = items.ToList();
        group.Count = all.Count;
        if (all.Count == 0 && !always)
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
        var filtered = (SelectedFilter.Value switch
        {
            DashboardFilter.Sheets => items.Where(item => item.Kind == ItemKind.Sheet),
            DashboardFilter.Passages => items.Where(item => item.Kind == ItemKind.Passage),
            _ => items,
        }).Where(item => InScope(item.Folder, item.Tags)).Where(item => Matches(item.Title, item.Excerpt, item.Term, item.Folder, string.Join(' ', item.Tags)));
        return SelectedSort.Value switch
        {
            DashboardSort.Step => [.. filtered.OrderBy(item => StepValue(item.Step))],
            DashboardSort.Cycle => [.. filtered.OrderByDescending(item => item.Cycle)],
            DashboardSort.LastReview => [.. filtered.OrderBy(item => item.LastReview)],
            DashboardSort.Title => [.. filtered.OrderBy(item => item.Title, FrenchTitles)],
            _ => [.. filtered],
        };
    }

    private const string FolderScope = "d:";
    private const string TagScope = "t:";
    private bool _buildingScopes;

    /// <summary>Le nombre d'une étape (« J14 » → 14), 0 sans chiffre.</summary>
    internal static int StepValue(string step)
    {
        var digits = new string([.. step.Where(char.IsAsciiDigit)]);
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
