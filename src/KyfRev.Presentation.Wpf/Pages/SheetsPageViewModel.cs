using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Où une fiche en est.</summary>
public enum SheetState
{
    /// <summary>Peu importe.</summary>
    All,

    /// <summary>En révision.</summary>
    Active,

    /// <summary>À réviser aujourd'hui ou en retard.</summary>
    Due,

    /// <summary>Archivée.</summary>
    Archived,
}

/// <summary>L'ordre de la liste.</summary>
public enum SheetSort
{
    /// <summary>Par dossier, puis par titre, sous un en-tête par dossier.</summary>
    Folder,

    /// <summary>Par titre.</summary>
    Title,

    /// <summary>Les échéances les plus proches d'abord, les fiches archivées à la fin.</summary>
    DueDate,
}

/// <summary>L'en-tête d'un dossier dans la liste : il se lit mais ne se choisit pas.</summary>
/// <param name="Folder">« Sciences › Biologie » ou « Sans dossier ».</param>
/// <param name="Count">Fiches montrées dans ce dossier.</param>
public sealed record SheetFolderRow(string Folder, int Count)
{
    /// <summary>Le nom lu par le lecteur d'écran, et ce qui s'affiche.</summary>
    public override string ToString() => SheetsText.FolderCountOf(Folder, Count);
}

/// <summary>Une fiche, telle que la liste la montre.</summary>
public sealed class SheetRow
{
    internal SheetRow(VaultSheet sheet, DateOnly day, bool showFolder)
    {
        Sheet = sheet;
        var folder = DisplayFolder(sheet.Folder);
        IsLate = sheet.DueDate < day;
        var state = sheet.Archived || sheet.DueDate is not { } due
            ? SheetsText.ArchivedRow
            : due < day ? SheetsText.StepOf(sheet.Step!, DashboardText.DaysLateOf(day.DayNumber - due.DayNumber))
            : due == day ? SheetsText.StepOf(sheet.Step!, SheetsText.DueToday)
            : SheetsText.StepOf(sheet.Step!, Dates.Short(due));
        Meta = showFolder && folder.Length > 0 ? $"{folder} · {state}" : state;
        Details = string.Join(" · ", new[] { folder, string.Join(' ', sheet.Tags.Select(tag => $"#{tag}")), state }.Where(part => part.Length > 0));
    }

    /// <summary>La fiche lue dans le coffre.</summary>
    public VaultSheet Sheet { get; }

    /// <summary>Titre.</summary>
    public string Title => Sheet.Title;

    /// <summary>« J3 · dim. 1 nov. » ou « Archivée », précédé du dossier hors du tri par dossier.</summary>
    public string Meta { get; }

    /// <summary>« Histoire · #partiel · J3 · dim. 1 nov. » : au-dessus de l'aperçu.</summary>
    public string Details { get; }

    /// <summary>L'échéance est passée.</summary>
    public bool IsLate { get; }

    /// <summary>Le fichier de la fiche est introuvable ou en double.</summary>
    public bool FileMissing => !Sheet.FilePresent;

    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => string.Join(", ", new[] { Title, Meta, FileMissing ? DashboardText.FileMissing : string.Empty }.Where(part => part.Length > 0));

    /// <summary>« Sciences › Biologie » ; rien à la racine.</summary>
    internal static string DisplayFolder(string folder) => folder.Replace("/", " › ", StringComparison.Ordinal);
}

/// <summary>
/// Vue « Fiches » : toutes les fiches du coffre, à chercher, filtrer et trier ; la fiche choisie
/// s'affiche à côté quand il y a la place, et s'ouvre sinon d'un clic. On y crée une fiche.
/// </summary>
public sealed partial class SheetsPageViewModel : ObservableObject, INavigationAware
{
    /// <summary>Largeur sous laquelle les filtres se replient.</summary>
    public const double RoomMinimum = 600;

    /// <summary>Largeur à partir de laquelle l'aperçu s'affiche à côté de la liste.</summary>
    public const double PreviewMinimum = 820;

    private static readonly StringComparer FrenchTitles = StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), ignoreCase: false);

    /// <summary>Le choix « Nouveau dossier… » du menu des dossiers.</summary>
    private static readonly object NewFolderChoice = new();

    private readonly SheetUseCases _sheets;
    private readonly FolderUseCases _folders;
    private readonly TemplateUseCases _templates;
    private readonly IReviewOpener _opener;
    private readonly TimeProvider _time;
    private readonly ListFilter _folder;
    private readonly ListFilter _state;
    private readonly ListFilter _sort;
    private IReadOnlyList<VaultSheet>? _entries;

    /// <summary>Numéro du dernier aperçu demandé : une lecture dépassée est ignorée.</summary>
    private int _previewTicket;

    /// <summary>La vue, sur les cas d'usage des fiches, des dossiers et des modèles ; <paramref name="opener"/> ouvre ses fiches.</summary>
    public SheetsPageViewModel(SheetUseCases sheets, FolderUseCases folders, TemplateUseCases templates, IReviewOpener opener, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(opener);
        ArgumentNullException.ThrowIfNull(time);
        _sheets = sheets;
        _folders = folders;
        _templates = templates;
        _opener = opener;
        _time = time;
        Bar = new FilterBarViewModel(BuildRows);
        _folder = Bar.Add(SheetsText.FilterFolder, [new(string.Empty, SheetsText.AllFolders)]);
        _state = Bar.Add(
            SheetsText.FilterState,
            [
                new(SheetState.All, SheetsText.StateAll),
                new(SheetState.Active, SheetsText.StateActive),
                new(SheetState.Due, SheetsText.StateDue),
                new(SheetState.Archived, SheetsText.StateArchived),
            ]);
        _sort = Bar.Add(
            SheetsText.FilterSort,
            [
                new(SheetSort.Folder, SheetsText.SortFolder),
                new(SheetSort.Title, SheetsText.SortTitle),
                new(SheetSort.DueDate, SheetsText.SortDueDate),
            ],
            counts: false);
        NewFolder = new ListFilter(SheetsText.FilterFolder, [new(string.Empty, SheetsText.AtRoot)], counts: false, () => OnPropertyChanged(nameof(IsNewFolder)));
        NewTemplate = new ListFilter(SheetsText.Template, [new(null, SheetsText.EmptyTemplate)], counts: false, () => { });
        Rows = [];
        NewTitle = string.Empty;
        NewFolderName = string.Empty;
    }

    /// <summary>« Fiches ».</summary>
    public static string Title => ShellText.Sheets;

    /// <summary>La recherche et les filtres.</summary>
    public FilterBarViewModel Bar { get; }

    /// <summary>« 25 fiches · 20 en révision · 5 archivées » ; le nombre seul en fenêtre étroite ; rien avant la première lecture.</summary>
    public string Summary
    {
        get
        {
            if (_entries is not { } entries)
            {
                return string.Empty;
            }
            var count = Dates.Plural(entries.Count, SheetsText.CountOneOf, SheetsText.CountManyOf);
            var archived = entries.Count(sheet => sheet.Archived);
            var active = entries.Count - archived;
            return !Bar.HasRoom ? count
                : archived > 1 ? SheetsText.SummaryManyOf(count, active, archived)
                : SheetsText.SummaryOneOf(count, active, archived);
        }
    }

    /// <summary>Les lignes : des fiches (<see cref="SheetRow"/>), et leurs en-têtes (<see cref="SheetFolderRow"/>) dans le tri par dossier.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    public partial IReadOnlyList<object> Rows { get; private set; }

    /// <summary>Au moins une fiche à montrer.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>La fiche choisie, montrée dans l'aperçu.</summary>
    [ObservableProperty]
    public partial object? SelectedRow { get; set; }

    /// <summary>L'aperçu de la fiche choisie ; rien tant qu'aucune n'est lue.</summary>
    [ObservableProperty]
    public partial TodayPreview? Preview { get; private set; }

    /// <summary>Le coffre a été lu, et n'a aucune fiche.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>Le coffre a des fiches, mais aucune ne correspond aux filtres.</summary>
    [ObservableProperty]
    public partial bool HasNoMatch { get; private set; }

    /// <summary>Il y a des fiches : la barre de filtres a un sens.</summary>
    [ObservableProperty]
    public partial bool HasSheets { get; private set; }

    /// <summary>Première lecture en cours.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Refus de la dernière lecture.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Une lecture a été refusée.</summary>
    public bool HasError => Error is not null;

    /// <summary>Largeur de la page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview), nameof(ShowsPreview))]
    public partial double PageWidth { get; set; } = double.PositiveInfinity;

    /// <summary>Assez de largeur pour montrer l'aperçu à côté de la liste.</summary>
    public bool HasPreview => PageWidth >= PreviewMinimum;

    /// <summary>L'aperçu s'affiche : il y a la place et une fiche à montrer.</summary>
    public bool ShowsPreview => HasPreview && HasRows;

    /// <summary>La fenêtre « Nouvelle fiche » est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsCreating { get; private set; }

    /// <summary>Le titre de la nouvelle fiche.</summary>
    [ObservableProperty]
    public partial string NewTitle { get; set; }

    /// <summary>Le dossier de la nouvelle fiche.</summary>
    public ListFilter NewFolder { get; }

    /// <summary>« Nouveau dossier… » est choisi : son nom reste à écrire.</summary>
    public bool IsNewFolder => ReferenceEquals(NewFolder.Value, NewFolderChoice);

    /// <summary>Le nom du nouveau dossier.</summary>
    [ObservableProperty]
    public partial string NewFolderName { get; set; }

    /// <summary>Le modèle de la nouvelle fiche.</summary>
    public ListFilter NewTemplate { get; }

    /// <summary>Pourquoi la fiche n'a pas pu être créée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCreateError))]
    public partial string? CreateError { get; private set; }

    /// <summary>La création a été refusée.</summary>
    public bool HasCreateError => CreateError is not null;

    /// <inheritdoc/>
    public void OnNavigatedTo() => RefreshCommand.Execute(null);

    /// <summary>Montre ces fiches.</summary>
    public void Show(IReadOnlyList<VaultSheet> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;
        Error = null;
        HasSheets = entries.Count > 0;
        OnPropertyChanged(nameof(Summary));
        _folder.Replace([new(string.Empty, SheetsText.AllFolders), .. FolderChoices(entries.Select(sheet => sheet.Folder))]);
        BuildRows();
    }

    partial void OnPageWidthChanged(double value)
    {
        Bar.HasRoom = value >= RoomMinimum;
        OnPropertyChanged(nameof(Summary));
        KeepSelection();
    }

    partial void OnSelectedRowChanged(object? value) => LoadPreviewCommand.Execute(value);

    partial void OnRowsChanged(IReadOnlyList<object> value) => OnPropertyChanged(nameof(ShowsPreview));

    private static IEnumerable<Choice<object?>> FolderChoices(IEnumerable<string> folders) =>
        folders.Where(folder => folder.Length > 0).Distinct().Order(FrenchTitles)
            .Select(folder => new Choice<object?>(folder, SheetRow.DisplayFolder(folder)));

    /// <summary>Relit les fiches.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = _entries is null;
        try
        {
            var outcome = await Task.Run(_sheets.List).ConfigureAwait(true);
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

    /// <summary>Ouvre une fiche ; un en-tête n'ouvre rien.</summary>
    [RelayCommand]
    private void Open(object? row)
    {
        if (row is SheetRow sheet)
        {
            _opener.OpenSheet(sheet.Sheet.Id);
        }
    }

    /// <summary>Lit la fiche choisie, hors du fil de l'interface ; seule la dernière demande compte.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadPreviewAsync(object? row)
    {
        var ticket = ++_previewTicket;
        if (row is not SheetRow sheet)
        {
            Preview = null;
            return;
        }
        var outcome = await Task.Run(() => _sheets.Open(sheet.Sheet.Id)).ConfigureAwait(true);
        if (ticket == _previewTicket)
        {
            Preview = outcome.Succeeded ? TodayPreview.Of(sheet, outcome.Value, _sheets) : TodayPreview.Failed(sheet, outcome.Error);
        }
    }

    /// <summary>« Nouvelle fiche » : la fenêtre s'ouvre, dans le dossier filtré s'il y en a un.</summary>
    [RelayCommand]
    private async Task RequestCreateAsync()
    {
        var (folders, templates) = await Task.Run(() => (_folders.Folders(), _templates.Templates())).ConfigureAwait(true);
        if (!folders.Succeeded || !templates.Succeeded)
        {
            Error = folders.Error ?? templates.Error;
            return;
        }
        NewTitle = string.Empty;
        NewFolderName = string.Empty;
        CreateError = null;
        NewFolder.Replace([new(string.Empty, SheetsText.AtRoot), .. FolderChoices(folders.Value), new(NewFolderChoice, SheetsText.NewFolderChoice)]);
        NewFolder.Selected = NewFolder.Choices.FirstOrDefault(choice => Equals(choice.Value, _folder.Value)) ?? NewFolder.Choices[0];
        NewTemplate.Replace([new(null, SheetsText.EmptyTemplate), .. templates.Value.Select(template => new Choice<object?>(template.Name, template.Name))]);
        NewTemplate.Selected = NewTemplate.Choices[0];
        IsCreating = true;
    }

    /// <summary>La réponse de la fenêtre : la fiche créée s'ouvre ; un refus laisse la fenêtre ouverte, avec sa raison.</summary>
    [RelayCommand]
    private async Task AnswerCreateAsync(bool confirmed)
    {
        IsCreating = false;
        if (!confirmed)
        {
            CreateError = null;
            return;
        }
        var title = NewTitle.Trim();
        if (title.Length == 0)
        {
            CreateError = SheetsText.TitleRequired;
            IsCreating = true;
            return;
        }
        var folder = IsNewFolder ? NewFolderName.Trim() : (string)NewFolder.Value!;
        var template = (string?)NewTemplate.Value;
        var outcome = await Task.Run(() => _sheets.Create(folder, title, template)).ConfigureAwait(true);
        if (!outcome.Succeeded)
        {
            CreateError = outcome.Error;
            IsCreating = true;
            return;
        }
        CreateError = null;
        _opener.OpenSheet(outcome.Value);
    }

    private void BuildRows()
    {
        // Choisir un filtre avant la première lecture ne trie rien.
        if (_entries is not { } entries)
        {
            return;
        }
        var wanted = ShellViewModel.Fold(Bar.Search.Trim());
        var day = Present.Of(_time).Now.Day;
        var sheets = entries.Where(sheet => Matches(sheet, wanted, day)).ToList();
        var sort = (SheetSort)_sort.Value!;
        Rows = sort == SheetSort.Folder ? GroupedByFolder(sheets, day) : [.. Sorted(sheets, sort).Select(sheet => new SheetRow(sheet, day, showFolder: true))];
        IsEmpty = entries.Count == 0;
        HasNoMatch = entries.Count > 0 && sheets.Count == 0;
        KeepSelection();
    }

    private static List<object> GroupedByFolder(List<VaultSheet> sheets, DateOnly day)
    {
        var rows = new List<object>();
        foreach (var group in sheets.GroupBy(sheet => sheet.Folder))
        {
            rows.Add(new SheetFolderRow(group.Key.Length == 0 ? SheetsText.NoFolder : SheetRow.DisplayFolder(group.Key), group.Count()));
            rows.AddRange(group.Select(sheet => new SheetRow(sheet, day, showFolder: false)));
        }
        return rows;
    }

    private static IEnumerable<VaultSheet> Sorted(List<VaultSheet> sheets, SheetSort sort) => sort == SheetSort.Title
        ? sheets.OrderBy(sheet => sheet.Title, FrenchTitles)
        : sheets.OrderBy(sheet => sheet.DueDate ?? DateOnly.MaxValue).ThenBy(sheet => sheet.Title, FrenchTitles);

    private bool Matches(VaultSheet sheet, string wanted, DateOnly day)
    {
        if (wanted.Length > 0 && !ShellViewModel.Fold($"{sheet.Title} {string.Join(' ', sheet.Tags)}").Contains(wanted, StringComparison.Ordinal))
        {
            return false;
        }
        var folder = (string)_folder.Value!;
        if (folder.Length > 0 && sheet.Folder != folder && !sheet.Folder.StartsWith(folder + "/", StringComparison.Ordinal))
        {
            return false;
        }
        return (SheetState)_state.Value! switch
        {
            SheetState.Active => !sheet.Archived,
            SheetState.Due => sheet.DueDate <= day,
            SheetState.Archived => sheet.Archived,
            _ => true,
        };
    }

    /// <summary>Avec l'aperçu, garde la fiche choisie, sinon la première ; sans lui, rien n'est choisi.</summary>
    private void KeepSelection()
    {
        var rows = Rows.OfType<SheetRow>().ToList();
        SelectedRow = !HasPreview ? null
            : rows.FirstOrDefault(row => SelectedRow is SheetRow selected && row.Sheet.Id == selected.Sheet.Id) ?? rows.FirstOrDefault();
    }
}
