using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
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

/// <summary>L'en-tête d'un dossier dans la liste : il se plie et se déplie, mais ne se choisit pas.</summary>
/// <param name="Folder">« Biologie » ou « Sans dossier ».</param>
/// <param name="Count">Fiches montrées dans ce dossier et ses sous-dossiers.</param>
public sealed record SheetFolderRow(string Folder, int Count)
{
    private const double IndentStep = 16;

    /// <summary>Son chemin relatif à <c>fiches/</c> ; vide pour « Sans dossier ».</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Sa profondeur dans l'arborescence, à partir de 0.</summary>
    public int Depth { get; init; }

    /// <summary>Ses fiches et sous-dossiers sont montrés.</summary>
    public bool IsExpanded { get; init; } = true;

    /// <summary>C'est un vrai dossier, et non « Sans dossier ».</summary>
    public bool IsFolder => Path.Length > 0;

    /// <summary>Il est plus profond que ce qui se parcourt bien.</summary>
    public bool IsTooDeep => Path.Split('/').Length > SheetsPageViewModel.CalmDepth;

    /// <summary>Son retrait dans la liste.</summary>
    public Thickness Indent => new(Depth * IndentStep, 8, 0, 2);

    /// <summary>Le chevron qui dit s'il est plié.</summary>
    public string Chevron => IsExpanded ? Icons.ChevronDown : Icons.ChevronRight;

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

    /// <summary>Sa profondeur dans l'arborescence.</summary>
    internal int Depth { get; init; }

    /// <summary>Son retrait dans la liste.</summary>
    public Thickness Indent => new(Depth * 16, 6, 0, 6);

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
public sealed partial class SheetsPageViewModel : ObservableObject, INavigationAware, IForgetsVault
{
    /// <summary>Les entrées que les extensions ajoutent au clic droit d'une fiche ou d'un dossier.</summary>
    public Func<ExtensionMenuZone, IReadOnlyDictionary<string, string>, IReadOnlyList<MenuContribution>>? ExtensionMenus { get; set; }

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
    private readonly ReviewUseCases? _reviews;
    private readonly TrashUseCases? _trash;
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private readonly ListFilter _folder;
    private readonly ListFilter _state;
    private readonly ListFilter _sort;
    private IReadOnlyList<VaultSheet>? _entries;
    private IReadOnlyList<string> _folderPaths = [];
    private string? _createIn;

    /// <summary>Numéro du dernier aperçu demandé : une lecture dépassée est ignorée.</summary>
    private int _previewTicket;

    /// <summary>Change à chaque oubli du coffre : une lecture ou une action partie avant n'y remet rien.</summary>
    private int _generation;

    /// <summary>La vue, sur les cas d'usage des fiches, des dossiers et des modèles ; <paramref name="opener"/> ouvre ses fiches.</summary>
    public SheetsPageViewModel(SheetUseCases sheets, FolderUseCases folders, TemplateUseCases templates, IReviewOpener opener, TimeProvider time, ReviewUseCases? reviews = null, TrashUseCases? trash = null, SheetSharingViewModel? sharing = null, FolderImportViewModel? import = null)
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
        _reviews = reviews;
        _trash = trash;
        Sharing = sharing;
        Import = import;
        Import?.Imported = OnImported;
        Import?.RefusedUnseen = ShowUnreadRefusal;
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

    /// <summary>
    /// Oublie les fiches lues, les dossiers pliés ou proposés, les modèles, la fiche choisie et son aperçu, la recherche,
    /// ce qui est écrit dans les fenêtres, les messages et l'action qui attendait sa confirmation : la page revient à son
    /// état d'avant toute lecture. L'état choisi, le tri et la largeur restent.
    /// </summary>
    public void Forget()
    {
        _generation++;
        _previewTicket++;
        _entries = null;
        _folderPaths = [];
        _createIn = null;
        _collapsed.Clear();
        Bar.Search = string.Empty;
        _folder.Replace([new(string.Empty, SheetsText.AllFolders)]);
        Rows = [];
        SelectedRow = null;
        Preview = null;
        IsEmpty = false;
        HasNoMatch = false;
        HasSheets = false;
        IsLoading = false;
        Error = null;
        IsCreating = false;
        NewTitle = string.Empty;
        NewFolderName = string.Empty;
        NewFolder.Replace([new(string.Empty, SheetsText.AtRoot)]);
        NewTemplate.Replace([new(null, SheetsText.EmptyTemplate)]);
        CreateError = null;
        ForgetAction();
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>Montre ces fiches.</summary>
    public void Show(IReadOnlyList<VaultSheet> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;
        ShowUnreadRefusal();
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
        var generation = _generation;
        IsLoading = _entries is null;
        try
        {
            var (outcome, folders) = await Task.Run(() => (_sheets.List(), _folders.Folders())).ConfigureAwait(true);
            if (generation != _generation)
            {
                // Le coffre a été oublié pendant la lecture : rien n'en revient.
                return;
            }
            if (outcome.Succeeded)
            {
                _folderPaths = folders.Succeeded ? folders.Value : [];
                Show(outcome.Value);
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
        var generation = _generation;
        var (folders, templates) = await Task.Run(() => (_folders.Folders(), _templates.Templates())).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant la lecture : la fenêtre ne s'ouvre pas sur ses dossiers.
            return;
        }
        if (!folders.Succeeded || !templates.Succeeded)
        {
            Error = folders.Error ?? templates.Error;
            return;
        }
        NewTitle = string.Empty;
        NewFolderName = string.Empty;
        CreateError = null;
        NewFolder.Replace([new(string.Empty, SheetsText.AtRoot), .. FolderChoices(folders.Value), new(NewFolderChoice, SheetsText.NewFolderChoice)]);
        var wanted = _createIn ?? _folder.Value;
        _createIn = null;
        NewFolder.Selected = NewFolder.Choices.FirstOrDefault(choice => Equals(choice.Value, wanted)) ?? NewFolder.Choices[0];
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
        var generation = _generation;
        var outcome = await Task.Run(() => _sheets.Create(folder, title, template)).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant la création : la fenêtre ne se rouvre pas, et la fiche ne s'ouvre pas.
            return;
        }
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
        var isFiltered = wanted.Length > 0 || !Equals(_folder.Value, string.Empty) || (SheetState)_state.Value! != SheetState.All;
        Rows = sort == SheetSort.Folder ? Tree(sheets, day, isFiltered) : [.. Sorted(sheets, sort).Select(sheet => new SheetRow(sheet, day, showFolder: true))];
        IsEmpty = entries.Count == 0;
        HasNoMatch = entries.Count > 0 && sheets.Count == 0;
        KeepSelection();
    }

    /// <summary>
    /// L'arborescence : chaque dossier, ses sous-dossiers, puis ses fiches. Sans filtre, les dossiers vides sont montrés :
    /// on peut y ranger des fiches.
    /// </summary>
    private List<object> Tree(List<VaultSheet> sheets, DateOnly day, bool isFiltered)
    {
        var paths = new SortedSet<string>(FrenchTitles);
        foreach (var path in sheets.Select(sheet => sheet.Folder).Concat(isFiltered ? [] : _folderPaths).Where(path => path.Length > 0))
        {
            for (var ancestor = path; ancestor.Length > 0; ancestor = ParentOf(ancestor))
            {
                paths.Add(ancestor);
            }
        }
        var rows = new List<object>();
        var atRoot = sheets.Where(sheet => sheet.Folder.Length == 0).ToList();
        // Même vide, « Sans dossier » reste une destination pour une fiche qu'on glisse hors d'un dossier.
        if (atRoot.Count > 0 || (!isFiltered && paths.Count > 0))
        {
            rows.Add(new SheetFolderRow(SheetsText.NoFolder, atRoot.Count));
            rows.AddRange(atRoot.Select(sheet => new SheetRow(sheet, day, showFolder: false)));
        }
        AddChildren(string.Empty, 0);
        return rows;

        void AddChildren(string parent, int depth)
        {
            foreach (var path in paths.Where(path => ParentOf(path) == parent))
            {
                var isExpanded = !_collapsed.Contains(path);
                rows.Add(new SheetFolderRow(NameOf(path), sheets.Count(sheet => IsWithin(sheet.Folder, path))) { Path = path, Depth = depth, IsExpanded = isExpanded });
                if (isExpanded)
                {
                    AddChildren(path, depth + 1);
                    rows.AddRange(sheets.Where(sheet => sheet.Folder == path).Select(sheet => new SheetRow(sheet, day, showFolder: false) { Depth = depth + 1 }));
                }
            }
        }
    }

    /// <summary>Plie ou déplie un dossier.</summary>
    [RelayCommand]
    private void ToggleFolder(SheetFolderRow row)
    {
        if (row.IsFolder)
        {
            if (!_collapsed.Add(row.Path))
            {
                _collapsed.Remove(row.Path);
            }
            BuildRows();
        }
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
