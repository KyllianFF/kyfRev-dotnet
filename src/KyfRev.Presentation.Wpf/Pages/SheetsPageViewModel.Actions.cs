using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Ce que la fenêtre d'action de la vue « Fiches » est en train de demander.</summary>
internal enum SheetAction
{
    Rename,
    Tags,
    Move,
    Archive,
    Restart,
    Trash,
    NewFolder,
    RenameFolder,
    MoveFolder,
    TrashFolder,
}

/// <summary>Les actions du clic droit sur les fiches et les dossiers : une seule fenêtre, qui s'adapte.</summary>
public sealed partial class SheetsPageViewModel
{
    /// <summary>Un dossier et trois sous-dossiers : au-delà, l'arborescence devient difficile à parcourir.</summary>
    internal const int CalmDepth = 4;

    private SheetAction _action;
    private VaultSheet? _actionSheet;
    private string _actionFolder = string.Empty;
    private string _confirmLabel = string.Empty;

    /// <summary>La fenêtre d'action est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsActing { get; private set; }

    /// <summary>Son titre.</summary>
    [ObservableProperty]
    public partial string ActionTitle { get; private set; } = string.Empty;

    /// <summary>Ce qu'elle annonce, s'il y a lieu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionMessage))]
    public partial string? ActionMessage { get; private set; }

    /// <summary>Elle annonce quelque chose.</summary>
    public bool HasActionMessage => ActionMessage is not null;

    /// <summary>Ce que le champ demande ; rien sans champ.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionInput))]
    public partial string? ActionInputLabel { get; private set; }

    /// <summary>Elle a un champ à remplir.</summary>
    public bool HasActionInput => ActionInputLabel is not null;

    /// <summary>Ce qui est écrit dans le champ.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DepthWarning), nameof(HasDepthWarning))]
    public partial string ActionInput { get; set; } = string.Empty;

    /// <summary>Les destinations proposées ; aucune hors déplacement.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionDestination))]
    public partial IReadOnlyList<SettingChoice<string>> ActionDestinations { get; private set; } = [];

    /// <summary>Elle propose une destination.</summary>
    public bool HasActionDestination => ActionDestinations.Count > 0;

    /// <summary>La destination choisie.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DepthWarning), nameof(HasDepthWarning))]
    public partial SettingChoice<string>? ActionDestination { get; set; }

    /// <summary>Elle propose de supprimer définitivement.</summary>
    [ObservableProperty]
    public partial bool CanErase { get; private set; }

    /// <summary>La suppression définitive est cochée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionConfirm))]
    public partial bool ErasesForGood { get; set; }

    /// <summary>Le libellé du bouton principal.</summary>
    public string ActionConfirm => ErasesForGood ? SheetActionsText.EraseConfirm : _confirmLabel;

    /// <summary>Le refus de la dernière action, fenêtre ouverte.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionError))]
    public partial string? ActionError { get; private set; }

    /// <summary>La dernière action a été refusée.</summary>
    public bool HasActionError => ActionError is not null;

    /// <summary>Ce qui vient d'aboutir.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDone))]
    public partial string? Done { get; private set; }

    /// <summary>Une action vient d'aboutir.</summary>
    public bool HasDone => Done is not null;

    /// <summary>Pendant un glisser-déposer : où irait la ligne.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDropHint))]
    public partial string? DropHint { get; private set; }

    /// <summary>Une ligne glissée peut être déposée ici.</summary>
    public bool HasDropHint => DropHint is not null;

    /// <summary>Ce dossier serait trop profond : on le permet, on le dit.</summary>
    public string? DepthWarning => WantedDepth() is { } depth and > CalmDepth ? SheetActionsText.DepthWarningOf(depth) : null;

    /// <summary>Il y a de quoi prévenir.</summary>
    public bool HasDepthWarning => DepthWarning is not null;

    /// <summary>Les actions sur les fiches sont disponibles : la vue a ses cas d'usage.</summary>
    internal bool CanManage => _reviews is not null && _trash is not null;

    /// <summary>Partager une fiche, ou l'enregistrer comme modèle ; rien sans cas d'usage.</summary>
    public SheetSharingViewModel? Sharing { get; }

    /// <summary>Importer un dossier de notes ; rien sans cas d'usage.</summary>
    public FolderImportViewModel? Import { get; }

    /// <summary>La vue sait importer : le bouton, le clic droit et l'état vide le proposent.</summary>
    public bool CanImport => Import is not null;

    /// <summary>Partager une fiche.</summary>
    internal void RequestShare(SheetRow row) => Sharing?.RequestShare(row.Sheet.Id, row.Title);

    /// <summary>Enregistrer une fiche comme modèle : ce qui en est dit s'affiche dans la vue.</summary>
    internal void RequestSaveAsTemplate(SheetRow row) => Sharing?.RequestSaveAsTemplate(row.Sheet.Id, row.Title, message => Done = message);

    /// <summary>Importer un dossier de notes.</summary>
    [RelayCommand]
    private void RequestImport() => Import?.Request();

    /// <summary>Un dossier a été importé : la liste est relue, et il est dit où, comme dans la 0.x.</summary>
    private void OnImported(ImportReport report)
    {
        Done = report.Sheets > 0 ? SharingText.ImportedOf(report.Folder) : null;
        RefreshCommand.Execute(null);
    }

    /// <summary>Ouvre une fiche pour la réviser.</summary>
    internal void Revise(SheetRow row) => _opener.ReviseSheet(row.Sheet.Id);

    /// <summary>Ouvre une fiche pour l'écrire.</summary>
    internal void Write(SheetRow row) => _opener.WriteSheet(row.Sheet.Id);

    /// <summary>Renommer une fiche.</summary>
    internal void RequestRename(SheetRow row)
    {
        Ask(SheetAction.Rename, row.Sheet, string.Empty, SheetActionsText.RenameTitle, null, SheetActionsText.RenameConfirm);
        ActionInputLabel = SheetActionsText.TitleLabel;
        ActionInput = row.Title;
    }

    /// <summary>Changer les étiquettes d'une fiche.</summary>
    internal void RequestTags(SheetRow row)
    {
        Ask(SheetAction.Tags, row.Sheet, string.Empty, SheetActionsText.TagsTitleOf(row.Title), null, SheetActionsText.SaveConfirm);
        ActionInputLabel = SheetActionsText.TagsLabel;
        ActionInput = string.Join(", ", row.Sheet.Tags);
    }

    /// <summary>Déplacer une fiche.</summary>
    internal void RequestMove(SheetRow row)
    {
        Ask(SheetAction.Move, row.Sheet, string.Empty, SheetActionsText.MoveTitleOf(row.Title), null, SheetActionsText.MoveConfirm);
        OfferDestinations(row.Sheet.Folder, []);
    }

    /// <summary>Archiver une fiche, ou la remettre en révision.</summary>
    internal void RequestArchiveOrRestart(SheetRow row)
    {
        if (row.Sheet.Archived)
        {
            Ask(SheetAction.Restart, row.Sheet, string.Empty, SheetActionsText.RestartTitle, SheetActionsText.RestartMessageOf(row.Title), SheetActionsText.RestartConfirm);
        }
        else
        {
            Ask(SheetAction.Archive, row.Sheet, string.Empty, SheetActionsText.ArchiveTitle, SheetActionsText.ArchiveMessageOf(row.Title), SheetActionsText.ArchiveConfirm);
        }
    }

    /// <summary>Supprimer une fiche : à la corbeille, ou pour de bon si c'est coché.</summary>
    internal void RequestTrash(SheetRow row)
    {
        Ask(SheetAction.Trash, row.Sheet, string.Empty, SheetActionsText.TrashTitle, SheetActionsText.TrashMessageOf(row.Title), SheetActionsText.TrashConfirm);
        CanErase = true;
    }

    /// <summary>Nouveau dossier, à la racine ou dans <paramref name="parent"/>.</summary>
    internal void RequestNewFolder(string parent)
    {
        var title = parent.Length == 0 ? SheetActionsText.NewFolderTitle : SheetActionsText.NewFolderInTitleOf(NameOf(parent));
        Ask(SheetAction.NewFolder, null, parent, title, null, SheetActionsText.CreateConfirm);
        ActionInputLabel = SheetActionsText.FolderNameLabel;
    }

    /// <summary>Renommer un dossier.</summary>
    internal void RequestRenameFolder(SheetFolderRow row)
    {
        Ask(SheetAction.RenameFolder, null, row.Path, SheetActionsText.RenameFolderTitle, null, SheetActionsText.RenameConfirm);
        ActionInputLabel = SheetActionsText.FolderNameLabel;
        ActionInput = NameOf(row.Path);
    }

    /// <summary>Déplacer un dossier, ailleurs qu'en lui-même.</summary>
    internal void RequestMoveFolder(SheetFolderRow row)
    {
        Ask(SheetAction.MoveFolder, null, row.Path, SheetActionsText.MoveTitleOf(NameOf(row.Path)), null, SheetActionsText.MoveConfirm);
        OfferDestinations(ParentOf(row.Path), [row.Path]);
    }

    /// <summary>Supprimer un dossier : à la corbeille, ou pour de bon si c'est coché.</summary>
    internal void RequestTrashFolder(SheetFolderRow row)
    {
        var count = _entries?.Count(sheet => IsWithin(sheet.Folder, row.Path)) ?? 0;
        var message = count == 0
            ? SheetActionsText.TrashEmptyFolderMessageOf(NameOf(row.Path))
            : SheetActionsText.TrashFolderMessageOf(NameOf(row.Path), Dates.Plural(count, SheetActionsText.SheetCountOneOf, SheetActionsText.SheetCountManyOf));
        Ask(SheetAction.TrashFolder, null, row.Path, SheetActionsText.TrashFolderTitle, message, SheetActionsText.TrashConfirm);
        CanErase = true;
    }

    /// <summary>
    /// Le dossier où irait une ligne déposée sur une autre : un dossier, ou celui d'une fiche quand la liste montre l'arborescence.
    /// Rien si elle y est déjà, ou pour un dossier qu'on poserait en lui-même.
    /// </summary>
    internal string? DropFolderOf(object? dragged, object? target)
    {
        var folder = target switch
        {
            SheetFolderRow row => row.Path,
            SheetRow row when Rows.OfType<SheetFolderRow>().Any() => row.Sheet.Folder,
            _ => null,
        };
        if (folder is null)
        {
            return null;
        }
        return dragged switch
        {
            SheetRow sheet when sheet.Sheet.Folder != folder => folder,
            SheetFolderRow { IsFolder: true } moved when ParentOf(moved.Path) != folder && !IsWithin(folder, moved.Path) => folder,
            _ => null,
        };
    }

    /// <summary>Dit où irait la ligne glissée, et rend ce dossier ; rien si elle ne peut pas aller là.</summary>
    internal string? ShowDrop(object? dragged, object? target)
    {
        var folder = DropFolderOf(dragged, target);
        DropHint = folder switch
        {
            null => null,
            "" => SheetActionsText.DropAtRoot,
            _ => SheetActionsText.DropInOf(SheetRow.DisplayFolder(folder)),
        };
        return folder;
    }

    /// <summary>Range la ligne déposée. Un dossier qui deviendrait trop profond passe par le dialogue et son avertissement.</summary>
    internal async Task DropAsync(object dragged, string folder)
    {
        DropHint = null;
        var (action, sheet, path) = dragged switch
        {
            SheetRow row => (SheetAction.Move, row.Sheet, string.Empty),
            SheetFolderRow row => (SheetAction.MoveFolder, null, row.Path),
            _ => throw new ArgumentOutOfRangeException(nameof(dragged)),
        };
        if (dragged is SheetFolderRow moved && Segments(Join(folder, NameOf(moved.Path))) > CalmDepth)
        {
            RequestMoveFolder(moved);
            ActionDestination = ActionDestinations.First(choice => choice.Value == folder);
            return;
        }
        var generation = _generation;
        var refusal = await Task.Run(() => Run(action, sheet, path, string.Empty, folder, erases: false)).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant le rangement : il n'est pas relu, et son refus nommerait ce qui est oublié.
            return;
        }
        await RefreshAsync().ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }
        Error = refusal;
        Done = refusal is null ? SheetActionsText.Moved : null;
    }

    /// <summary>« Nouvelle fiche » dans ce dossier.</summary>
    internal Task CreateInAsync(string folder)
    {
        _createIn = folder;
        return RequestCreateAsync();
    }

    private static string NameOf(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static string ParentOf(string path) => path.LastIndexOf('/') is var slash and >= 0 ? path[..slash] : string.Empty;

    private static string Join(string parent, string name) => parent.Length == 0 ? name : $"{parent}/{name}";

    private static bool IsWithin(string folder, string parent) =>
        folder == parent || folder.StartsWith(parent + "/", StringComparison.Ordinal);

    private void Ask(SheetAction action, VaultSheet? sheet, string folder, string title, string? message, string confirm)
    {
        (_action, _actionSheet, _actionFolder, _confirmLabel) = (action, sheet, folder, confirm);
        ActionTitle = title;
        ActionMessage = message;
        ActionInputLabel = null;
        ActionInput = string.Empty;
        ActionDestinations = [];
        ActionDestination = null;
        CanErase = false;
        ErasesForGood = false;
        ActionError = null;
        Done = null;
        OnPropertyChanged(nameof(ActionConfirm));
        OnPropertyChanged(nameof(DepthWarning));
        OnPropertyChanged(nameof(HasDepthWarning));
        IsActing = true;
    }

    /// <summary>Ferme la fenêtre d'action, et oublie ce qu'elle visait, ce qui y est écrit, ce qui vient d'aboutir et l'indice du glisser-déposer.</summary>
    private void ForgetAction()
    {
        _action = default;
        _actionSheet = null;
        _actionFolder = string.Empty;
        _confirmLabel = string.Empty;
        IsActing = false;
        ActionTitle = string.Empty;
        ActionMessage = null;
        ActionInputLabel = null;
        ActionInput = string.Empty;
        ActionDestinations = [];
        ActionDestination = null;
        CanErase = false;
        ErasesForGood = false;
        ActionError = null;
        Done = null;
        DropHint = null;
        OnPropertyChanged(nameof(ActionConfirm));
        OnPropertyChanged(nameof(DepthWarning));
        OnPropertyChanged(nameof(HasDepthWarning));
    }

    private void OfferDestinations(string current, IReadOnlyList<string> excluded)
    {
        ActionDestinations =
        [
            new(string.Empty, SheetsText.AtRoot),
            .. _folderPaths
                .Where(path => !excluded.Any(folder => IsWithin(path, folder)))
                .Order(FrenchTitles)
                .Select(path => new SettingChoice<string>(path, SheetRow.DisplayFolder(path))),
        ];
        ActionDestination = ActionDestinations.FirstOrDefault(choice => choice.Value == current) ?? ActionDestinations[0];
    }

    /// <summary>La profondeur qu'aurait le dossier créé ou déplacé ; rien pour les autres actions.</summary>
    private int? WantedDepth() => _action switch
    {
        SheetAction.NewFolder when IsActing && ActionInput.Trim().Length > 0 => Segments(Join(_actionFolder, ActionInput.Trim())),
        SheetAction.MoveFolder when IsActing && ActionDestination is { } destination => Segments(Join(destination.Value, NameOf(_actionFolder))),
        _ => null,
    };

    private static int Segments(string path) => path.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>La réponse de la fenêtre : un refus la laisse ouverte, avec sa raison.</summary>
    [RelayCommand]
    private async Task AnswerActionAsync(bool confirmed)
    {
        IsActing = false;
        if (!confirmed)
        {
            ActionError = null;
            return;
        }
        var input = ActionInput.Trim();
        if (_action is SheetAction.Rename or SheetAction.NewFolder or SheetAction.RenameFolder && input.Length == 0)
        {
            ActionError = SheetActionsText.NameRequired;
            IsActing = true;
            return;
        }
        var (action, sheet, folder, destination, erases) = (_action, _actionSheet, _actionFolder, ActionDestination?.Value ?? string.Empty, ErasesForGood);
        var generation = _generation;
        var refusal = await Task.Run(() => Run(action, sheet, folder, input, destination, erases)).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant l'action : ni sa réponse ni la relecture qui la suit n'y reviennent.
            return;
        }
        if (refusal is not null)
        {
            ActionError = refusal;
            IsActing = true;
            return;
        }
        ActionError = null;
        Done = DoneMessage(action, erases);
        await RefreshAsync().ConfigureAwait(true);
    }

    private string? Run(SheetAction action, VaultSheet? sheet, string folder, string input, string destination, bool erases) => action switch
    {
        SheetAction.Rename => _sheets.ChangeTitle(sheet!.Id, input).Error,
        SheetAction.Tags => _sheets.SetTags(sheet!.Id, [.. input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]).Error,
        SheetAction.Move => _sheets.Move(sheet!.Id, destination).Error,
        SheetAction.Archive => _reviews!.Archive(sheet!.Id).Error,
        SheetAction.Restart => _reviews!.Restart(ItemKind.Sheet, sheet!.Id).Error,
        SheetAction.Trash => _trash!.TrashSheet(sheet!.Id).Error ?? (erases ? _trash.PurgeSheet(sheet.Id).Error : null),
        SheetAction.NewFolder => _folders.Create(Join(folder, input)).Error,
        SheetAction.RenameFolder => _folders.Move(folder, Join(ParentOf(folder), input)).Error,
        SheetAction.MoveFolder => _folders.Move(folder, Join(destination, NameOf(folder))).Error,
        _ => erases ? _trash!.PurgeFolder(folder).Error : _trash!.TrashFolder(folder).Error,
    };

    private static string DoneMessage(SheetAction action, bool erases) => action switch
    {
        SheetAction.Rename => SheetActionsText.Renamed,
        SheetAction.Tags => SheetActionsText.TagsSaved,
        SheetAction.Move or SheetAction.MoveFolder => SheetActionsText.Moved,
        SheetAction.Archive => SheetActionsText.Archived,
        SheetAction.Restart => SheetActionsText.Restarted,
        SheetAction.NewFolder => SheetActionsText.FolderCreated,
        SheetAction.RenameFolder => SheetActionsText.FolderRenamed,
        _ => erases ? SheetActionsText.Erased : SheetActionsText.Trashed,
    };
}
