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

/// <summary>Ce que la liste montre : tout, les fiches de révision seules, ou les simples notes seules.</summary>
public enum SheetKind
{
    /// <summary>Fiches de révision et notes.</summary>
    All,

    /// <summary>Les fiches de révision, en cours ou archivées.</summary>
    Review,

    /// <summary>Les simples notes.</summary>
    Notes,
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

/// <summary>Un modèle proposé dans le volet « Nouvelle fiche » (rangée 22B) : son nom, et ce qu'il contient.</summary>
/// <param name="Value">Le nom du modèle ; rien pour une fiche vide.</param>
/// <param name="Name">Ce qui s'affiche.</param>
/// <param name="Description">Ce qu'il contient, en une ligne.</param>
public sealed record TemplateTile(string? Value, string Name, string Description)
{
    /// <summary>Le plus de lignes qu'une vignette dessine.</summary>
    internal const int ShapeLines = 4;

    /// <summary>La plus longue ligne dessinée, en pixels : une ligne de texte de cette longueur, ou plus, la remplit.</summary>
    internal const double ShapeWidth = 72;

    /// <summary>
    /// L'allure du modèle, en traits : ses premières lignes, longues ou courtes comme elles le sont, les titres plus
    /// épais. Une fiche vide n'en a que deux, à peine marqués.
    /// </summary>
    public IReadOnlyList<TemplateLine> Shape { get; init; } = [new(ShapeWidth * 0.7, IsHeading: false), new(ShapeWidth * 0.45, IsHeading: false)];

    /// <summary>Une vignette pour un modèle, dessinée d'après son texte.</summary>
    public static TemplateTile Of(string name, string description, string body) => new(name, name, description) { Shape = ShapeOf(body) };

    /// <summary>Les traits des premières lignes non vides d'un texte Markdown.</summary>
    internal static IReadOnlyList<TemplateLine> ShapeOf(string body) =>
        [.. body.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Take(ShapeLines)
            .Select(line =>
            {
                var heading = line.StartsWith('#');
                var text = line.TrimStart('#', '-', '*', '>', ' ');
                return new TemplateLine(Math.Clamp(text.Length / 28d, 0.3, 1) * ShapeWidth, heading);
            })];

    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => Description.Length > 0 ? $"{Name}, {Description}" : Name;
}

/// <summary>Un trait dans une vignette de modèle : une ligne du modèle, sa longueur, et si c'est un titre.</summary>
/// <param name="Width">Longueur, en pixels.</param>
/// <param name="IsHeading">Un titre : un trait plus épais.</param>
public sealed record TemplateLine(double Width, bool IsHeading);

/// <summary>Ce qu'on choisit dans le volet des dossiers : ce que la liste du milieu montre.</summary>
public enum SheetPlace
{
    /// <summary>Toutes les fiches, rangées par dossier.</summary>
    All,

    /// <summary>Les fiches à réviser aujourd'hui ou en retard.</summary>
    Due,

    /// <summary>Un dossier, avec ses sous-dossiers.</summary>
    Folder,

    /// <summary>Les fiches posées à la racine du coffre, dans aucun dossier.</summary>
    Unfiled,
}

/// <summary>
/// Un dossier : une entrée du volet des dossiers (rangée 21B des maquettes), qui se choisit et, s'il a des sous-dossiers,
/// se plie ; ou l'intertitre d'un sous-dossier dans la liste du milieu, qui ne se choisit pas.
/// </summary>
/// <param name="Folder">« Biologie », « Toutes les fiches » ou « Non rangées » ; « Cellule › Noyau » pour un intertitre.</param>
/// <param name="Count">Fiches de ce dossier et de ses sous-dossiers.</param>
public sealed record SheetFolderRow(string Folder, int Count)
{
    private const double IndentStep = 16;

    /// <summary>Son chemin relatif à <c>fiches/</c> ; vide pour ce qui n'est pas un vrai dossier.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Ce que cette entrée montre quand on la choisit.</summary>
    public SheetPlace Place { get; init; } = SheetPlace.Folder;

    /// <summary>Sa profondeur dans l'arborescence, à partir de 0.</summary>
    public int Depth { get; init; }

    /// <summary>Ses sous-dossiers sont montrés.</summary>
    public bool IsExpanded { get; init; } = true;

    /// <summary>Il a des sous-dossiers : son chevron le plie et le déplie.</summary>
    public bool HasChildren { get; init; }

    /// <summary>La première entrée des dossiers : l'intertitre « Dossiers » est au-dessus d'elle.</summary>
    public bool StartsFolders { get; init; }

    /// <summary>C'est un vrai dossier, qu'on renomme, déplace ou supprime.</summary>
    public bool IsFolder => Path.Length > 0;

    /// <summary>On peut y déposer une fiche ou un dossier : un vrai dossier, ou la racine (« Non rangées »).</summary>
    public bool TakesDrops => Place is SheetPlace.Folder or SheetPlace.Unfiled;

    /// <summary>Il est plus profond que ce qui se parcourt bien.</summary>
    public bool IsTooDeep => Path.Split('/').Length > SheetsPageViewModel.CalmDepth;

    /// <summary>Son retrait dans le volet.</summary>
    public Thickness Indent => new(Depth * IndentStep, 0, 0, 0);

    /// <summary>Le chevron qui dit s'il est plié.</summary>
    public string Chevron => IsExpanded ? Icons.ChevronDown : Icons.ChevronRight;

    /// <summary>Son icône : la liste, l'horloge, le dossier, ou la fiche seule.</summary>
    public string Glyph => Place switch
    {
        SheetPlace.All => Icons.ListLines,
        SheetPlace.Due => Icons.Clock,
        SheetPlace.Unfiled => Icons.Document,
        _ => Icons.FolderOpen,
    };

    /// <summary>Le nom lu par le lecteur d'écran, et ce qui s'affiche.</summary>
    public override string ToString() => SheetsText.FolderCountOf(Folder, Count);
}

/// <summary>Ce qu'est un document de la liste : son étiquette en prend la couleur.</summary>
public enum SheetRowKind
{
    /// <summary>Une fiche de révision, à jour.</summary>
    Review,

    /// <summary>Une fiche de révision en retard.</summary>
    Late,

    /// <summary>Une fiche archivée.</summary>
    Archived,

    /// <summary>Une simple note.</summary>
    Note,
}

/// <summary>Une fiche, telle que la liste la montre.</summary>
public sealed class SheetRow
{
    internal SheetRow(VaultSheet sheet, DateOnly day, bool showFolder)
    {
        Sheet = sheet;
        var folder = DisplayFolder(sheet.Folder);
        IsLate = !sheet.IsNote && sheet.DueDate < day;
        IsDueToday = !sheet.IsNote && !sheet.Archived && sheet.DueDate == day;
        var state = sheet.IsNote ? SheetsText.NoteRow
            : sheet.Archived || sheet.DueDate is not { } due ? SheetsText.ArchivedRow
            : due < day ? SheetsText.StepOf(sheet.Step!, DashboardText.DaysLateOf(day.DayNumber - due.DayNumber))
            : due == day ? SheetsText.StepOf(sheet.Step!, SheetsText.DueToday)
            : SheetsText.StepOf(sheet.Step!, Dates.Short(due));
        State = state;
        Place = showFolder ? folder : string.Empty;
        Kind = sheet.IsNote ? SheetRowKind.Note : sheet.Archived || sheet.DueDate is null ? SheetRowKind.Archived : IsLate ? SheetRowKind.Late : SheetRowKind.Review;
        Meta = showFolder && folder.Length > 0 ? $"{folder} · {state}" : state;
        Details = string.Join(" · ", new[] { folder, string.Join(' ', sheet.Tags.Select(tag => $"#{tag}")), state }.Where(part => part.Length > 0));
    }

    /// <summary>La fiche lue dans le coffre.</summary>
    public VaultSheet Sheet { get; }

    /// <summary>Titre.</summary>
    public string Title => Sheet.Title;

    /// <summary>« J3 · dim. 1 nov. » ou « Archivée », précédé du dossier hors du tri par dossier.</summary>
    public string Meta { get; }

    /// <summary>« J3 · dim. 1 nov. », « Archivée » ou « Note » : l'étiquette à droite de la ligne.</summary>
    public string State { get; }

    /// <summary>Son dossier, sous le titre ; vide dans le tri par dossier, où l'en-tête le dit déjà.</summary>
    public string Place { get; }

    /// <summary>Elle a un dossier à montrer sous son titre.</summary>
    public bool HasPlace => Place.Length > 0;

    /// <summary>Ce qu'est le document, pour la couleur de son étiquette.</summary>
    public SheetRowKind Kind { get; }

    /// <summary>« Histoire · #partiel · J3 · dim. 1 nov. » : au-dessus de l'aperçu.</summary>
    public string Details { get; }

    /// <summary>L'échéance est passée.</summary>
    public bool IsLate { get; }

    /// <summary>Elle est à réviser aujourd'hui même : sa pastille est un anneau.</summary>
    public bool IsDueToday { get; }

    /// <summary>Ce que son menu sait d'elle.</summary>
    internal SheetTarget Target => new(Sheet.Id, Sheet.Title) { IsArchived = Sheet.Archived, IsNote = Sheet.IsNote, Folder = Sheet.Folder };

    /// <summary>Le fichier de la fiche est introuvable ou en double.</summary>
    public bool FileMissing => !Sheet.FilePresent;

    /// <summary>Le passage de son texte où se trouvent les mots cherchés ; vide si le titre ou les étiquettes ont suffi.</summary>
    public IReadOnlyList<ExcerptPiece> Excerpt { get; init; } = [];

    /// <summary>Elle a un extrait à montrer.</summary>
    public bool HasExcerpt => Excerpt.Count > 0;

    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => string.Join(", ", new[] { Title, Meta, FileMissing ? DashboardText.FileMissing : string.Empty, string.Concat(Excerpt.Select(piece => piece.Text)) }.Where(part => part.Length > 0));

    /// <summary>« Sciences › Biologie » ; rien à la racine.</summary>
    internal static string DisplayFolder(string folder) => folder.Replace("/", " › ", StringComparison.Ordinal);
}

/// <summary>
/// Vue « Fiches » : toutes les fiches du coffre, à chercher (titre, étiquettes et texte, avec un
/// extrait), filtrer et trier ; la fiche choisie s'affiche à côté quand il y a la place, et s'ouvre
/// sinon d'un clic. On y crée une fiche.
/// </summary>
public sealed partial class SheetsPageViewModel : ObservableObject, INavigationAware, IForgetsVault, IStopsSearch
{
    /// <summary>Les actions communes sur une fiche : ce que le Carnet ne fait pas lui-même (reporter, rappels).</summary>
    public SheetActionsViewModel? SheetActions { get; set; }

    /// <summary>Les entrées que les extensions ajoutent au clic droit d'une fiche ou d'un dossier.</summary>
    public Func<ExtensionMenuZone, IReadOnlyDictionary<string, string>, IReadOnlyList<MenuContribution>>? ExtensionMenus { get; set; }

    /// <summary>Largeur sous laquelle les filtres se replient.</summary>
    public const double RoomMinimum = 600;

    /// <summary>Largeur à partir de laquelle l'aperçu s'affiche à côté de la liste.</summary>
    public const double PreviewMinimum = 820;

    /// <summary>Largeur à partir de laquelle le volet des dossiers tient à gauche de la liste ; en deçà, un bouton l'ouvre.</summary>
    public const double PaneMinimum = 1000;

    /// <summary>Largeur à partir de laquelle le volet et la liste s'élargissent (écran très large).</summary>
    public const double WideMinimum = 1800;

    private static readonly StringComparer FrenchTitles = StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), ignoreCase: false);

    /// <summary>Le choix « Nouveau dossier… » du menu des dossiers.</summary>
    private static readonly object NewFolderChoice = new();

    /// <summary>Le temps d'attendre la frappe suivante avant de chercher dans les fiches.</summary>
    private static readonly TimeSpan SearchPause = TimeSpan.FromMilliseconds(200);

    private readonly SheetUseCases _sheets;
    private readonly FolderUseCases _folders;
    private readonly TemplateUseCases _templates;
    private readonly IReviewOpener _opener;
    private readonly TimeProvider _time;
    private readonly ReviewUseCases? _reviews;
    private readonly TrashUseCases? _trash;
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private readonly ListFilter _state;

    /// <summary>Ce que le volet des dossiers a choisi : toutes les fiches au départ.</summary>
    private (SheetPlace Kind, string Path) _place = (SheetPlace.All, string.Empty);

    /// <summary>Le volet se refait : la sélection qui passe un instant à rien ne change pas ce qui est choisi.</summary>
    private bool _rebuildingPlaces;
    private readonly HashSet<string> _chosenTags = new(StringComparer.OrdinalIgnoreCase);
    private readonly ListFilter _sort;
    private readonly LatestSearch _textSearch = new();
    private bool _searchStopped;
    private IReadOnlyList<VaultSheet>? _entries;
    private IReadOnlyList<string> _folderPaths = [];
    private string? _createIn;

    /// <summary>La réponse à la recherche : l'extrait de chaque fiche trouvée ; rien tant qu'aucune réponse n'est arrivée.</summary>
    private Dictionary<string, IReadOnlyList<ExcerptPiece>>? _found;

    /// <summary>Numéro du dernier aperçu demandé : une lecture dépassée est ignorée.</summary>
    private int _previewTicket;

    /// <summary>La liste se refait : la fiche choisie se retrouve à la fin, et l'aperçu n'a pas à se vider entre-temps.</summary>
    private bool _rebuilding;

    /// <summary>Un dossier plié ou déplié : le contenu des fiches n'a pas changé, l'aperçu de la fiche gardée reste tel quel.</summary>
    private bool _sameSheets;

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
        Bar = new FilterBarViewModel(BuildRows) { SearchPlaceholder = SheetsText.SearchPlaceholder, SearchName = SheetsText.SearchName, SearchChanged = OnSearchTextChanged };
        // Le dossier se choisit dans le volet de gauche, plus dans les filtres.
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
        Bar.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FilterBarViewModel.ShowsFilters))
            {
                OnPropertyChanged(nameof(ShowsTags));
            }
        };
        NewFolder = new ListFilter(SheetsText.FilterFolder, [new(string.Empty, SheetsText.AtRoot)], counts: false, () =>
        {
            OnPropertyChanged(nameof(IsNewFolder));
            OnPropertyChanged(nameof(NewFolderLabel));
            OnPropertyChanged(nameof(NewFolderIsNamed));
        });
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
            return PageWidth < RoomMinimum ? count
                : archived > 1 ? SheetsText.SummaryManyOf(count, active, archived)
                : SheetsText.SummaryOneOf(count, active, archived);
        }
    }

    /// <summary>Les étiquettes des fiches du dossier et de l'état choisis : une fiche doit porter toutes celles qui sont choisies.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTags), nameof(HasTags))]
    public partial IReadOnlyList<TagChip> Tags { get; private set; } = [];

    /// <summary>Au moins une étiquette est choisie.</summary>
    public bool HasChosenTags => _chosenTags.Count > 0;

    /// <summary>Le filtre se voit : sur une fenêtre étroite, il se replie avec les autres, sauf s'il agit.</summary>
    public bool ShowsTags => Tags.Count > 0 && (AreTagsOpen || HasChosenTags);

    /// <summary>Il y a des étiquettes à proposer.</summary>
    public bool HasTags => Tags.Count > 0;

    /// <summary>Les étiquettes sont dépliées.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTags))]
    public partial bool AreTagsOpen { get; set; }

    /// <summary>Les lignes : des fiches (<see cref="SheetRow"/>), et leurs en-têtes (<see cref="SheetFolderRow"/>) dans le tri par dossier.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    public partial IReadOnlyList<object> Rows { get; private set; }

    /// <summary>Au moins une fiche à montrer.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>
    /// Le volet des dossiers : « Toutes les fiches », « À réviser aujourd'hui », l'arborescence des dossiers (ses
    /// sous-dossiers pliés cachés), puis « Non rangées ».
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<SheetFolderRow> Places { get; private set; } = [];

    /// <summary>L'entrée choisie dans le volet : la liste du milieu montre ses fiches.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaceName))]
    public partial SheetFolderRow? SelectedPlace { get; set; }

    /// <summary>« Biologie » : le nom de ce qui est choisi, sur le bouton du volet replié.</summary>
    public string PlaceName => SelectedPlace?.Folder ?? SheetsText.AllSheets;

    /// <summary>Le volet replié est demandé ouvert, par-dessus la page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPlacesSheet))]
    public partial bool ArePlacesOpen { get; set; }

    /// <summary>Le tiroir des dossiers est ouvert : demandé, et le volet ne tient pas à côté de la liste.</summary>
    public bool ShowsPlacesSheet => ArePlacesOpen && !ShowsPane;

    /// <summary>Le volet tient à gauche de la liste ; sinon, un bouton l'ouvre par-dessus. Tant que la largeur n'est pas connue, non.</summary>
    public bool ShowsPane => PageWidth >= PaneMinimum && !double.IsInfinity(PageWidth);

    /// <summary>Le volet replié s'ouvre par le bas, comme sur un téléphone ; sinon par la gauche.</summary>
    public bool PlacesFromBottom => PageWidth < RoomMinimum;

    /// <summary>Écran très large : le volet et la liste prennent plus de place.</summary>
    public bool IsWide => PageWidth >= WideMinimum && !double.IsInfinity(PageWidth);

    /// <summary>« Biologie », « Toutes les fiches », ou « Dans tout le coffre » pendant une recherche : au-dessus de la liste.</summary>
    [ObservableProperty]
    public partial string ListTitle { get; private set; } = SheetsText.AllSheets;

    /// <summary>« 3 fiches » : ce que la liste montre.</summary>
    [ObservableProperty]
    public partial string ListCount { get; private set; } = string.Empty;

    /// <summary>Ce qui est choisi dans le volet n'a aucune fiche, sans qu'aucun filtre n'y soit pour rien.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaceEmptyText), nameof(PlaceTakesSheets))]
    public partial bool IsPlaceEmpty { get; private set; }

    /// <summary>« Ce dossier est vide. », « Rien à réviser pour l'instant. » ou « Toutes vos fiches sont rangées. ».</summary>
    public string PlaceEmptyText => _place.Kind switch
    {
        SheetPlace.Due => SheetsText.NothingDue,
        SheetPlace.Unfiled => SheetsText.AllFiled,
        _ => SheetsText.EmptyPlace,
    };

    /// <summary>On peut écrire une fiche là où l'on est : un dossier, ou la racine ; pas dans « À réviser ».</summary>
    public bool PlaceTakesSheets => _place.Kind != SheetPlace.Due;

    /// <summary>La liste du milieu montre autre chose : la vue la fait arriver.</summary>
    public event EventHandler? PlaceChanged;

    partial void OnSelectedPlaceChanged(SheetFolderRow? value)
    {
        if (_rebuildingPlaces || value is null)
        {
            return;
        }
        ArePlacesOpen = false;
        var place = (value.Place, value.Path);
        if (place == _place)
        {
            return;
        }
        _place = place;
        BuildRows();
        PlaceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Va dans ce dossier, depuis son intertitre dans la liste : il est choisi dans le volet, ses parents dépliés pour le
    /// montrer. La recherche en cours s'arrête : on veut voir ce dossier.
    /// </summary>
    internal void GoTo(string folder)
    {
        if (_place == (SheetPlace.Folder, folder) && Bar.Search.Length == 0)
        {
            return;
        }
        for (var ancestor = ParentOf(folder); ancestor.Length > 0; ancestor = ParentOf(ancestor))
        {
            _collapsed.Remove(ancestor);
        }
        _place = (SheetPlace.Folder, folder);
        Bar.Search = string.Empty;
        BuildRows();
        PlaceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ouvre ou ferme le volet replié.</summary>
    [RelayCommand]
    private void TogglePlaces() => ArePlacesOpen = !ArePlacesOpen;

    /// <summary>« Nouveau dossier », au bas du volet : dans le dossier choisi, à la racine sinon.</summary>
    [RelayCommand]
    private void NewFolderHere()
    {
        ArePlacesOpen = false;
        RequestNewFolder(_place.Kind == SheetPlace.Folder ? _place.Path : string.Empty);
    }

    /// <summary>
    /// Refait le volet : les dossiers du coffre, même vides, et le nombre de fiches de chacun (parmi celles que montrent
    /// « Tout », « Fiches » ou « Notes »). Rien ne change s'il est le même, pour que le choix ne saute pas.
    /// </summary>
    private void BuildPlaces(IReadOnlyList<VaultSheet> entries, DateOnly day)
    {
        var shown = entries.Where(sheet => (Kind != SheetKind.Review || !sheet.IsNote) && (Kind != SheetKind.Notes || sheet.IsNote)).ToList();
        var paths = new SortedSet<string>(FrenchTitles);
        foreach (var path in shown.Select(sheet => sheet.Folder).Concat(entries.Select(sheet => sheet.Folder)).Concat(_folderPaths).Where(path => path.Length > 0))
        {
            for (var ancestor = path; ancestor.Length > 0; ancestor = ParentOf(ancestor))
            {
                paths.Add(ancestor);
            }
        }
        var places = new List<SheetFolderRow>
        {
            new(SheetsText.AllSheets, shown.Count) { Place = SheetPlace.All },
            new(SheetsText.DueSheets, shown.Count(sheet => IsDue(sheet, day))) { Place = SheetPlace.Due },
        };
        var first = true;
        AddChildren(string.Empty, 0);
        places.Add(new SheetFolderRow(SheetsText.NoFolder, shown.Count(sheet => sheet.Folder.Length == 0)) { Place = SheetPlace.Unfiled, StartsFolders = first });
        if (!places.SequenceEqual(Places))
        {
            _rebuildingPlaces = true;
            try
            {
                Places = places;
            }
            finally
            {
                _rebuildingPlaces = false;
            }
        }
        // Le dossier choisi s'est caché sous un parent plié : c'est le parent qui est choisi, comme dans l'Explorateur. Il a
        // disparu (supprimé, renommé, déplacé) : on revient à toutes les fiches.
        var chosen = Places.FirstOrDefault(row => row.Place == _place.Kind && row.Path == _place.Path);
        for (var ancestor = _place.Kind == SheetPlace.Folder && paths.Contains(_place.Path) ? ParentOf(_place.Path) : string.Empty; chosen is null && ancestor.Length > 0; ancestor = ParentOf(ancestor))
        {
            chosen = Places.FirstOrDefault(row => row.IsFolder && row.Path == ancestor);
        }
        chosen ??= Places[0];
        _place = (chosen.Place, chosen.Path);
        _rebuildingPlaces = true;
        try
        {
            SelectedPlace = chosen;
        }
        finally
        {
            _rebuildingPlaces = false;
        }

        void AddChildren(string parent, int depth)
        {
            foreach (var path in paths.Where(path => ParentOf(path) == parent))
            {
                var hasChildren = paths.Any(other => ParentOf(other) == path);
                var isExpanded = !_collapsed.Contains(path);
                places.Add(new SheetFolderRow(NameOf(path), shown.Count(sheet => IsWithin(sheet.Folder, path)))
                {
                    Path = path,
                    Depth = depth,
                    HasChildren = hasChildren,
                    IsExpanded = isExpanded,
                    StartsFolders = first,
                });
                first = false;
                if (hasChildren && isExpanded)
                {
                    AddChildren(path, depth + 1);
                }
            }
        }
    }

    private static bool IsDue(VaultSheet sheet, DateOnly day) => !sheet.IsNote && !sheet.Archived && sheet.DueDate <= day;

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
    [NotifyPropertyChangedFor(nameof(HasPreview), nameof(ShowsPreview), nameof(ShowsPane), nameof(PlacesFromBottom), nameof(IsWide), nameof(ShowsPlacesSheet))]
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

    /// <summary>Ce que la liste montre : tout, les fiches de révision, ou les notes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsAll), nameof(ShowsReviewOnly), nameof(ShowsNotesOnly))]
    public partial SheetKind Kind { get; set; }

    partial void OnKindChanged(SheetKind value) => BuildRows();

    /// <summary>La liste montre tout.</summary>
    public bool ShowsAll
    {
        get => Kind == SheetKind.All;
        set => Choose(value, SheetKind.All);
    }

    /// <summary>La liste ne montre que les fiches de révision.</summary>
    public bool ShowsReviewOnly
    {
        get => Kind == SheetKind.Review;
        set => Choose(value, SheetKind.Review);
    }

    /// <summary>La liste ne montre que les notes.</summary>
    public bool ShowsNotesOnly
    {
        get => Kind == SheetKind.Notes;
        set => Choose(value, SheetKind.Notes);
    }

    private void Choose(bool chosen, SheetKind kind)
    {
        if (chosen)
        {
            Kind = kind;
        }
    }

    /// <summary>« Tout · 42 ».</summary>
    public string KindAllLabel => SheetsText.KindOf(SheetsText.KindAll, _entries?.Count ?? 0);

    /// <summary>« Fiches de révision · 31 ».</summary>
    public string KindReviewLabel => SheetsText.KindOf(SheetsText.KindReview, _entries?.Count(sheet => !sheet.IsNote) ?? 0);

    /// <summary>« Notes · 11 ».</summary>
    public string KindNotesLabel => SheetsText.KindOf(SheetsText.KindNotes, _entries?.Count(sheet => sheet.IsNote) ?? 0);

    /// <summary>« Nouvelle note » a été demandée : la fenêtre s'ouvre avec la case cochée.</summary>
    private bool _createsNote;

    /// <summary>« Nouvelle note » : la même fenêtre que « Nouvelle fiche », pour une simple note.</summary>
    [RelayCommand]
    private Task RequestCreateNoteAsync()
    {
        _createsNote = true;
        return RequestCreateAsync();
    }

    /// <summary>La nouvelle fiche est une simple note : elle n'entre pas dans les révisions.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewDialogTitle), nameof(NewDialogConfirm))]
    public partial bool NewIsNote { get; set; }

    /// <summary>La nouvelle fiche est une fiche de révision : l'autre côté du choix « Fiche de révision / Note ».</summary>
    public bool NewIsReview
    {
        get => !NewIsNote;
        set
        {
            if (value)
            {
                NewIsNote = false;
            }
        }
    }

    partial void OnNewIsNoteChanged(bool value) => OnPropertyChanged(nameof(NewIsReview));

    /// <summary>Les modèles, en vignettes : une fiche vide, puis ceux du coffre et ceux livrés avec kyfRev.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<TemplateTile> NewTemplates { get; private set; } = [];

    /// <summary>La vignette choisie ; elle choisit le modèle.</summary>
    [ObservableProperty]
    public partial TemplateTile? NewTemplateTile { get; set; }

    partial void OnNewTemplateTileChanged(TemplateTile? value)
    {
        if (value is not null && NewTemplate.Choices.FirstOrDefault(choice => Equals(choice.Value, value.Value)) is { } choice)
        {
            NewTemplate.Selected = choice;
        }
    }

    /// <summary>« Biologie › Cellule » : où ira la fiche, sur le bouton qui le change.</summary>
    public string NewFolderLabel => NewFolder.Selected.Label;

    /// <summary>« Dans », devant un dossier ; rien devant « À la racine » ni « Nouveau dossier… », qui se disent seuls.</summary>
    public bool NewFolderIsNamed => NewFolder.Value is string { Length: > 0 };

    /// <summary>« Nouvelle fiche » ou « Nouvelle note ».</summary>
    public string NewDialogTitle => NewIsNote ? SheetsText.NewNote : SheetsText.NewSheet;

    /// <summary>« Créer la fiche » ou « Créer la note ».</summary>
    public string NewDialogConfirm => NewIsNote ? SheetsText.CreateNote : SheetsText.CreateSheet;

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
        _textSearch.Cancel();
        _searchStopped = false;
        _found = null;
        _entries = null;
        _folderPaths = [];
        _createIn = null;
        _collapsed.Clear();
        _chosenTags.Clear();
        Tags = [];
        OnPropertyChanged(nameof(HasChosenTags));
        Bar.Search = string.Empty;
        _place = (SheetPlace.All, string.Empty);
        _rebuildingPlaces = true;
        try
        {
            Places = [];
            SelectedPlace = null;
        }
        finally
        {
            _rebuildingPlaces = false;
        }
        ArePlacesOpen = false;
        IsPlaceEmpty = false;
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

    /// <inheritdoc/>
    public void StopSearch()
    {
        _searchStopped |= _textSearch.IsRunning;
        _textSearch.Cancel();
    }

    /// <inheritdoc/>
    public void ResumeSearch()
    {
        if (_searchStopped && Bar.Search.Trim().Length > 0)
        {
            SearchTextCommand.Execute(TimeSpan.Zero);
        }
        _searchStopped = false;
    }

    /// <summary>Montre ces fiches.</summary>
    public void Show(IReadOnlyList<VaultSheet> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;
        ShowUnreadRefusal();
        HasSheets = entries.Count > 0;
        OnPropertyChanged(nameof(Summary));
        BuildRows();
    }

    partial void OnPageWidthChanged(double value)
    {
        // La recherche et les filtres tiennent dans la colonne de la liste : les filtres s'y déplient à la demande.
        Bar.HasRoom = false;
        // Le volet tient de nouveau à côté de la liste : le tiroir n'a plus lieu d'être.
        if (ShowsPane)
        {
            ArePlacesOpen = false;
        }
        OnPropertyChanged(nameof(Summary));
        KeepSelection();
    }

    partial void OnSelectedRowChanged(object? value)
    {
        // Une liste refaite perd un instant sa ligne choisie, avant de la retrouver : vider l'aperçu puis le relire le faisait
        // clignoter à chaque dossier plié ou déplié.
        if (_rebuilding && (value is null || (_sameSheets && IsPreviewed(value))))
        {
            return;
        }
        LoadPreviewCommand.Execute(value);
    }

    private bool IsPreviewed(object row) =>
        row is SheetRow wanted && Preview is { HasBody: true, Row: SheetRow shown } && shown.Sheet.Id == wanted.Sheet.Id;

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
                // Une fiche renommée, déplacée ou modifiée : la recherche en cours est refaite.
                if (Bar.Search.Trim().Length > 0)
                {
                    await SearchTextAsync(TimeSpan.Zero).ConfigureAwait(true);
                }
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

    /// <summary>Le texte cherché change : la recherche part une fois la frappe arrêtée un instant ; sans texte, on en oublie la réponse.</summary>
    private void OnSearchTextChanged()
    {
        if (Bar.Search.Trim().Length > 0)
        {
            SearchTextCommand.Execute(SearchPause);
        }
        else
        {
            _textSearch.Cancel();
            _found = null;
        }
    }

    /// <summary>Cherche le texte dans les fiches, hors du fil de l'interface : la liste garde sa réponse précédente jusqu'à la nouvelle.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task SearchTextAsync(TimeSpan pause)
    {
        var text = Bar.Search.Trim();
        return _textSearch.RunAsync(pause, token => _sheets.Search(text, token), outcome =>
        {
            if (!outcome.Succeeded)
            {
                Error = outcome.Error;
                return;
            }
            Error = null;
            _found = outcome.Value.ToDictionary(hit => hit.Id, IReadOnlyList<ExcerptPiece> (hit) => hit.Excerpt, StringComparer.Ordinal);
            BuildRows();
        });
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
        // Sur le filtre « Notes », c'est une note qu'on vient écrire.
        // « Nouvelle note » fait une note, « Nouvelle fiche » une fiche de révision : le bouton décide, sans case à cocher.
        NewIsNote = _createsNote;
        _createsNote = false;
        CreateError = null;
        NewFolder.Replace([new(string.Empty, SheetsText.AtRoot), .. FolderChoices(folders.Value), new(NewFolderChoice, SheetsText.NewFolderChoice)]);
        var wanted = _createIn ?? (_place.Kind == SheetPlace.Folder ? _place.Path : string.Empty);
        _createIn = null;
        NewFolder.Selected = NewFolder.Choices.FirstOrDefault(choice => Equals(choice.Value, wanted)) ?? NewFolder.Choices[0];
        NewTemplate.Replace([new(null, SheetsText.EmptyTemplate), .. templates.Value.Select(template => new Choice<object?>(template.Name, template.Name))]);
        NewTemplate.Selected = NewTemplate.Choices[0];
        NewTemplates = [new(null, SheetsText.EmptyTemplate, SheetsText.EmptyTemplateDescription), .. templates.Value.Select(template => TemplateTile.Of(template.Name, template.Description, template.Body))];
        NewTemplateTile = NewTemplates[0];
        OnPropertyChanged(nameof(NewFolderLabel));
        OnPropertyChanged(nameof(NewFolderIsNamed));
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
        var asNote = NewIsNote;
        var outcome = await Task.Run(() => asNote ? _sheets.CreateNote(folder, title, template) : _sheets.Create(folder, title, template)).ConfigureAwait(true);
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
        RefreshTags(entries, day);
        var sheets = entries.Where(sheet => Matches(sheet, wanted, day)).ToList();
        var sort = (SheetSort)_sort.Value!;
        OnPropertyChanged(nameof(KindAllLabel));
        OnPropertyChanged(nameof(KindReviewLabel));
        OnPropertyChanged(nameof(KindNotesLabel));
        var isFiltered = Kind != SheetKind.All || HasChosenTags || (SheetState)_state.Value! != SheetState.All;
        BuildPlaces(entries, day);
        // Une recherche porte sur tout le coffre : chaque fiche trouvée dit son dossier.
        var searching = wanted.Length > 0;
        var (kind, root) = searching ? (SheetPlace.All, string.Empty) : _place;
        var inPlace = entries.Where(sheet => IsIn(sheet, kind, root, day)).ToList();
        sheets = [.. sheets.Where(sheet => IsIn(sheet, kind, root, day))];
        ListTitle = searching ? SheetsText.SearchEverywhere : SelectedPlace?.Folder ?? SheetsText.AllSheets;
        ListCount = Dates.Plural(sheets.Count, SheetsText.CountOneOf, SheetsText.CountManyOf);
        _rebuilding = true;
        try
        {
            Rows = sort != SheetSort.Folder || kind is SheetPlace.Due or SheetPlace.Unfiled
                ? [.. (kind == SheetPlace.Due && sort == SheetSort.Folder ? Sorted(sheets, SheetSort.DueDate) : Sorted(sheets, sort == SheetSort.Folder ? SheetSort.Title : sort))
                    .Select(sheet => RowOf(sheet, day, showFolder: kind != SheetPlace.Unfiled))]
                : Grouped(sheets, day, kind == SheetPlace.Folder ? root : null);
            IsEmpty = entries.Count == 0;
            // Un dossier vide n'est pas une recherche sans réponse : on y propose d'écrire.
            IsPlaceEmpty = entries.Count > 0 && inPlace.Count == 0 && !searching && kind != SheetPlace.All;
            OnPropertyChanged(nameof(PlaceEmptyText));
            OnPropertyChanged(nameof(PlaceTakesSheets));
            HasNoMatch = entries.Count > 0 && sheets.Count == 0 && !IsPlaceEmpty && (searching || isFiltered);
            KeepSelection();
        }
        finally
        {
            _rebuilding = false;
        }
        if (SelectedRow is null && Preview is not null)
        {
            // Plus rien n'est choisi : l'aperçu resté le temps de refaire la liste s'en va.
            LoadPreviewCommand.Execute(null);
        }
    }

    /// <summary>
    /// La liste rangée par dossier : dans un dossier (<paramref name="root"/>), ses propres fiches d'abord, puis celles de
    /// chaque sous-dossier sous son intertitre (« Cellule », « Cellule › Noyau ») ; pour tout le coffre (<c>null</c>), un
    /// intertitre par dossier, et les fiches non rangées à la fin. Les dossiers sans fiche montrée n'ont pas d'intertitre.
    /// </summary>
    private List<object> Grouped(List<VaultSheet> sheets, DateOnly day, string? root)
    {
        var rows = new List<object>();
        if (root is not null)
        {
            rows.AddRange(sheets.Where(sheet => sheet.Folder == root).Select(sheet => RowOf(sheet, day, showFolder: false)));
        }
        var folders = new SortedSet<string>(sheets.Select(sheet => sheet.Folder).Where(folder => folder.Length > 0 && folder != root), FrenchTitles);
        foreach (var folder in folders)
        {
            var inside = sheets.Where(sheet => sheet.Folder == folder).ToList();
            var label = root is { Length: > 0 } ? folder[(root.Length + 1)..] : folder;
            rows.Add(new SheetFolderRow(SheetRow.DisplayFolder(label), inside.Count) { Path = folder });
            rows.AddRange(inside.Select(sheet => RowOf(sheet, day, showFolder: false)));
        }
        if (root is null && sheets.Where(sheet => sheet.Folder.Length == 0).ToList() is { Count: > 0 } unfiled)
        {
            rows.Add(new SheetFolderRow(SheetsText.NoFolder, unfiled.Count) { Place = SheetPlace.Unfiled });
            rows.AddRange(unfiled.Select(sheet => RowOf(sheet, day, showFolder: false)));
        }
        return rows;
    }

    /// <summary>La fiche est dans ce que montre le volet.</summary>
    private static bool IsIn(VaultSheet sheet, SheetPlace kind, string root, DateOnly day) => kind switch
    {
        SheetPlace.Due => IsDue(sheet, day),
        SheetPlace.Folder => IsWithin(sheet.Folder, root),
        SheetPlace.Unfiled => sheet.Folder.Length == 0,
        _ => true,
    };

    /// <summary>Retire tous les filtres d'étiquette.</summary>
    [RelayCommand]
    private void ClearTags()
    {
        _chosenTags.Clear();
        foreach (var chip in Tags)
        {
            chip.IsChosen = false;
        }
        OnPropertyChanged(nameof(HasChosenTags));
        OnPropertyChanged(nameof(ShowsTags));
        BuildRows();
    }

    private void OnTagToggled(TagChip chip)
    {
        if (_chosenTags.Contains(chip.Label) == chip.IsChosen)
        {
            return;
        }
        if (!_chosenTags.Add(chip.Label))
        {
            _chosenTags.Remove(chip.Label);
        }
        OnPropertyChanged(nameof(HasChosenTags));
        OnPropertyChanged(nameof(ShowsTags));
        BuildRows();
    }

    /// <summary>
    /// Les pastilles, à partir des fiches du dossier et de l'état choisis ; une étiquette choisie reste, même sans fiche, pour
    /// pouvoir la retirer. Rien ne change si les comptes sont les mêmes : la pastille qu'on vient de presser garde le focus.
    /// </summary>
    private void RefreshTags(IEnumerable<VaultSheet> entries, DateOnly day)
    {
        var tally = TagChip.Tally(entries.Where(sheet => IsInKindAndState(sheet, day) && IsIn(sheet, _place.Kind, _place.Path, day)), FrenchTitles);
        var known = tally.Select(tag => tag.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<(string Label, int Count)> wanted = [.. tally, .. _chosenTags.Where(tag => !known.Contains(tag)).Select(tag => (tag, 0))];
        if (!wanted.SequenceEqual(Tags.Select(chip => (chip.Label, chip.Count))))
        {
            Tags = [.. wanted.Select(tag => new TagChip(tag.Label, tag.Count, _chosenTags.Contains(tag.Label), OnTagToggled))];
        }
    }

    private SheetRow RowOf(VaultSheet sheet, DateOnly day, bool showFolder) =>
        new(sheet, day, showFolder) { Excerpt = _found?.GetValueOrDefault(sheet.Id) ?? [] };

    /// <summary>Plie ou déplie un dossier du volet : ses sous-dossiers se cachent ou se montrent.</summary>
    [RelayCommand]
    private void ToggleFolder(SheetFolderRow row)
    {
        if (row.IsFolder && row.HasChildren)
        {
            if (!_collapsed.Add(row.Path))
            {
                _collapsed.Remove(row.Path);
            }
            _sameSheets = true;
            try
            {
                BuildRows();
            }
            finally
            {
                _sameSheets = false;
            }
        }
    }

    private static IEnumerable<VaultSheet> Sorted(List<VaultSheet> sheets, SheetSort sort) => sort == SheetSort.Title
        ? sheets.OrderBy(sheet => sheet.Title, FrenchTitles)
        : sheets.OrderBy(sheet => sheet.DueDate ?? DateOnly.MaxValue).ThenBy(sheet => sheet.Title, FrenchTitles);

    private bool Matches(VaultSheet sheet, string wanted, DateOnly day)
    {
        // Tant que la réponse de la recherche n'est pas arrivée, le titre et les étiquettes suffisent.
        if (wanted.Length > 0 && !(_found?.ContainsKey(sheet.Id) ?? ShellViewModel.Fold($"{sheet.Title} {string.Join(' ', sheet.Tags)}").Contains(wanted, StringComparison.Ordinal)))
        {
            return false;
        }
        return _chosenTags.All(tag => sheet.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) && IsInKindAndState(sheet, day);
    }

    private bool IsInKindAndState(VaultSheet sheet, DateOnly day)
    {
        if ((Kind == SheetKind.Review && sheet.IsNote) || (Kind == SheetKind.Notes && !sheet.IsNote))
        {
            return false;
        }
        return (SheetState)_state.Value! switch
        {
            SheetState.Active => !sheet.Archived && !sheet.IsNote,
            SheetState.Due => !sheet.IsNote && sheet.DueDate <= day,
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
