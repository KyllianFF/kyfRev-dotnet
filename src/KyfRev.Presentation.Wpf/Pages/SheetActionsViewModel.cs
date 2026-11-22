using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Les actions sur une fiche, depuis n'importe quelle vue (rangée 23A des maquettes) : Aujourd'hui, les Archives, un aperçu,
/// la fiche ouverte, la recherche. Une seule fenêtre, posée dans la fenêtre principale, demande ce qu'il faut (un titre, des
/// étiquettes, un dossier, une confirmation) ; ce qui a abouti se dit en bas de la fenêtre, et la vue affichée est relue.
/// </summary>
public sealed partial class SheetActionsViewModel : ObservableObject, ISheetMenuHost
{
    /// <summary>Au plus autant d'étiquettes proposées à la fois.</summary>
    private const int MaxTagSuggestions = 8;

    private readonly SheetUseCases _sheets;
    private readonly FolderUseCases _folders;
    private readonly ReviewUseCases _reviews;
    private readonly ReminderUseCases _reminders;
    private readonly TrashUseCases _trash;
    private readonly IReviewOpener _opener;
    private readonly TimeProvider _time;
    private SheetAction _action;
    private VaultSheet? _sheet;
    private IReadOnlyList<string> _knownTags = [];
    private string _confirmLabel = string.Empty;

    /// <summary>Les actions, sur les cas d'usage du coffre ; <paramref name="opener"/> ouvre les fiches.</summary>
    public SheetActionsViewModel(SheetUseCases sheets, FolderUseCases folders, ReviewUseCases reviews, ReminderUseCases reminders, TrashUseCases trash, IReviewOpener opener, TimeProvider time, SheetSharingViewModel? sharing = null)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(reviews);
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(trash);
        ArgumentNullException.ThrowIfNull(opener);
        ArgumentNullException.ThrowIfNull(time);
        (_sheets, _folders, _reviews, _reminders, _trash, _opener, _time, Sharing) = (sheets, folders, reviews, reminders, trash, opener, time, sharing);
    }

    /// <summary>Une action a abouti : la vue affichée se relit.</summary>
    public event EventHandler? Changed;

    /// <summary>Une fiche a quitté le coffre (mise à la corbeille) : si elle est ouverte, on la quitte.</summary>
    public Action<string>? SheetGone { get; set; }

    /// <summary>Partager une fiche, ou l'enregistrer comme modèle ; rien sans cas d'usage.</summary>
    public SheetSharingViewModel? Sharing { get; }

    /// <summary>Les entrées que les extensions ajoutent au clic droit d'une fiche.</summary>
    public Func<ExtensionMenuZone, IReadOnlyDictionary<string, string>, IReadOnlyList<MenuContribution>>? ExtensionMenus { get; set; }

    /// <inheritdoc/>
    public bool CanManage => true;

    /// <inheritdoc/>
    public bool CanShare => Sharing is not null;

    /// <summary>La fenêtre est ouverte.</summary>
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
    public partial string ActionInput { get; set; } = string.Empty;

    /// <summary>Étiquettes déjà utilisées dans le coffre, proposées pendant qu'on en saisit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTagSuggestions))]
    public partial IReadOnlyList<string> TagSuggestions { get; private set; } = [];

    /// <summary>Il y a des étiquettes à proposer.</summary>
    public bool HasTagSuggestions => TagSuggestions.Count > 0;

    /// <summary>Les dossiers où déplacer la fiche ; aucun hors déplacement.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionDestination))]
    public partial IReadOnlyList<SettingChoice<string>> ActionDestinations { get; private set; } = [];

    /// <summary>Elle propose une destination.</summary>
    public bool HasActionDestination => ActionDestinations.Count > 0;

    /// <summary>La destination choisie.</summary>
    [ObservableProperty]
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

    /// <summary>Ce qui vient d'aboutir, ou d'être refusé hors de la fenêtre ; dit en bas de la fenêtre principale.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDone))]
    public partial string? Done { get; private set; }

    /// <summary>Quelque chose vient d'aboutir.</summary>
    public bool HasDone => Done is not null;

    /// <summary>On quitte la vue : la fenêtre se referme sans rien faire.</summary>
    public void Dismiss()
    {
        IsActing = false;
        ActionError = null;
    }

    /// <summary>Le message s'en va.</summary>
    [RelayCommand]
    private void DismissDone() => Done = null;

    partial void OnActionInputChanged(string value) => SuggestTags();

    /// <inheritdoc/>
    public void Read(SheetTarget sheet) => _opener.OpenSheet(Of(sheet).Id);

    /// <inheritdoc/>
    public void Revise(SheetTarget sheet) => _opener.ReviseSheet(Of(sheet).Id);

    /// <inheritdoc/>
    public void Write(SheetTarget sheet) => _opener.WriteSheet(Of(sheet).Id);

    /// <inheritdoc/>
    public void Print(SheetTarget sheet) => _opener.PrintSheet(Of(sheet).Id);

    /// <inheritdoc/>
    public void Share(SheetTarget sheet) => Sharing?.RequestShare(Of(sheet).Id, sheet.Title);

    /// <inheritdoc/>
    public void SaveAsTemplate(SheetTarget sheet) => Sharing?.RequestSaveAsTemplate(Of(sheet).Id, sheet.Title, message => Done = message);

    /// <inheritdoc/>
    public IReadOnlyList<MenuContribution> Extensions(SheetTarget sheet) =>
        ExtensionMenus?.Invoke(ExtensionMenuZone.Sheet, new Dictionary<string, string> { ["id"] = sheet.Id, ["titre"] = sheet.Title, ["dossier"] = sheet.Folder }) ?? [];

    /// <inheritdoc/>
    public void PostponeTomorrow(SheetTarget sheet) =>
        _ = ActAsync(() => _reviews.Postpone(ItemKind.Sheet, sheet.Id, Present.Of(_time).Now.Day.AddDays(1)).Error, SheetActionsText.PostponedTomorrow);

    /// <inheritdoc/>
    public void ToggleReminders(SheetTarget sheet)
    {
        var include = sheet.IsExcludedFromReminders == true;
        _ = ActAsync(
            () => _reminders.Include(sheet.Id, include).Error,
            include ? ArchivesText.IncludedMessageOf(sheet.Title) : ArchivesText.ExcludedMessageOf(sheet.Title));
    }

    /// <inheritdoc/>
    public void ToggleNote(SheetTarget sheet)
    {
        var wasNote = sheet.IsNote;
        _ = ActAsync(() => (wasNote ? _reviews.PutInReview(sheet.Id) : _reviews.MakeNote(sheet.Id)).Error, wasNote ? SheetsText.NowReview : SheetsText.NowNote);
    }

    /// <inheritdoc/>
    public void Rename(SheetTarget sheet) => _ = AskAsync(sheet, SheetAction.Rename, SheetActionsText.RenameTitle, null, SheetActionsText.RenameConfirm, found =>
    {
        ActionInputLabel = SheetActionsText.TitleLabel;
        ActionInput = found.Title;
    });

    /// <inheritdoc/>
    public void Tags(SheetTarget sheet) => _ = AskAsync(sheet, SheetAction.Tags, SheetActionsText.TagsTitleOf(sheet.Title), null, SheetActionsText.SaveConfirm, found =>
    {
        ActionInputLabel = SheetActionsText.TagsLabel;
        ActionInput = string.Join(", ", found.Tags);
        SuggestTags();
    });

    /// <inheritdoc/>
    public void Move(SheetTarget sheet) => _ = AskAsync(sheet, SheetAction.Move, SheetActionsText.MoveTitleOf(sheet.Title), null, SheetActionsText.MoveConfirm, found =>
    {
        var folders = _folders.Folders();
        ActionDestinations =
        [
            new(string.Empty, SheetsText.AtRoot),
            .. (folders.Succeeded ? folders.Value : []).Order(StringComparer.CurrentCulture).Select(path => new SettingChoice<string>(path, SheetRow.DisplayFolder(path))),
        ];
        ActionDestination = ActionDestinations.FirstOrDefault(choice => choice.Value == found.Folder) ?? ActionDestinations[0];
    });

    /// <inheritdoc/>
    public void Archive(SheetTarget sheet) =>
        _ = AskAsync(sheet, SheetAction.Archive, SheetActionsText.ArchiveTitle, SheetActionsText.ArchiveMessageOf(sheet.Title), SheetActionsText.ArchiveConfirm, _ => { });

    /// <inheritdoc/>
    public void Restart(SheetTarget sheet) =>
        _ = AskAsync(sheet, SheetAction.Restart, SheetActionsText.RestartTitle, SheetActionsText.RestartMessageOf(sheet.Title), SheetActionsText.RestartConfirm, _ => { });

    /// <inheritdoc/>
    public void Trash(SheetTarget sheet) =>
        _ = AskAsync(sheet, SheetAction.Trash, SheetActionsText.TrashTitle, SheetActionsText.TrashMessageOf(sheet.Title), SheetActionsText.TrashConfirm, _ => CanErase = true);

    /// <summary>Prend une étiquette proposée : elle remplace celle en cours de saisie, et la suivante peut se taper.</summary>
    [RelayCommand]
    private void TakeTag(string tag)
    {
        var cut = ActionInput.LastIndexOf(',');
        var kept = cut < 0 ? string.Empty : ActionInput[..(cut + 1)].TrimEnd() + " ";
        ActionInput = $"{kept}{tag}, ";
    }

    /// <summary>La réponse de la fenêtre : un refus la laisse ouverte, avec sa raison.</summary>
    [RelayCommand]
    private async Task AnswerActionAsync(bool confirmed)
    {
        IsActing = false;
        if (!confirmed || _sheet is not { } sheet)
        {
            ActionError = null;
            return;
        }
        var input = ActionInput.Trim();
        if (_action == SheetAction.Rename && input.Length == 0)
        {
            ActionError = SheetActionsText.NameRequired;
            IsActing = true;
            return;
        }
        var (action, destination, erases) = (_action, ActionDestination?.Value ?? string.Empty, ErasesForGood);
        var refusal = await Task.Run(() => action switch
        {
            SheetAction.Rename => _sheets.ChangeTitle(sheet.Id, input).Error,
            SheetAction.Tags => _sheets.SetTags(sheet.Id, [.. input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]).Error,
            SheetAction.Move => _sheets.Move(sheet.Id, destination).Error,
            SheetAction.Archive => _reviews.Archive(sheet.Id).Error,
            SheetAction.Restart => _reviews.Restart(ItemKind.Sheet, sheet.Id).Error,
            _ => _trash.TrashSheet(sheet.Id).Error ?? (erases ? _trash.PurgeSheet(sheet.Id).Error : null),
        }).ConfigureAwait(true);
        if (refusal is not null)
        {
            ActionError = refusal;
            IsActing = true;
            return;
        }
        ActionError = null;
        Done = action switch
        {
            SheetAction.Rename => SheetActionsText.Renamed,
            SheetAction.Tags => SheetActionsText.TagsSaved,
            SheetAction.Move => SheetActionsText.Moved,
            SheetAction.Archive => SheetActionsText.Archived,
            SheetAction.Restart => SheetActionsText.Restarted,
            _ => erases ? SheetActionsText.Erased : SheetActionsText.Trashed,
        };
        if (action == SheetAction.Trash)
        {
            SheetGone?.Invoke(sheet.Id);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Une action sans fenêtre : elle se fait, se dit, et la vue se relit ; un refus se dit à la même place.</summary>
    private async Task ActAsync(Func<string?> run, string done)
    {
        var refusal = await Task.Run(run).ConfigureAwait(true);
        Done = refusal ?? done;
        if (refusal is null)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Lit la fiche dans le coffre, puis ouvre la fenêtre ; une fiche introuvable se dit en bas de la fenêtre.</summary>
    private async Task AskAsync(SheetTarget target, SheetAction action, string title, string? message, string confirm, Action<VaultSheet> prepare)
    {
        var listed = await Task.Run(_sheets.List).ConfigureAwait(true);
        if (!listed.Succeeded || listed.Value.FirstOrDefault(sheet => sheet.Id == target.Id) is not { } found)
        {
            Done = listed.Error ?? SheetActionsText.SheetMissing;
            return;
        }
        (_action, _sheet, _confirmLabel) = (action, found, confirm);
        _knownTags = [.. listed.Value.SelectMany(sheet => sheet.Tags)];
        ActionTitle = title;
        ActionMessage = message;
        ActionInputLabel = null;
        ActionInput = string.Empty;
        ActionDestinations = [];
        ActionDestination = null;
        CanErase = false;
        ErasesForGood = false;
        ActionError = null;
        TagSuggestions = [];
        OnPropertyChanged(nameof(ActionConfirm));
        prepare(found);
        IsActing = true;
    }

    /// <summary>
    /// Propose les étiquettes du coffre qui commencent comme celle en cours de saisie, sans celles déjà écrites : reprendre une
    /// étiquette existante évite d'en créer deux pour la même chose.
    /// </summary>
    private void SuggestTags()
    {
        if (_action != SheetAction.Tags || !IsActing && ActionInputLabel is null)
        {
            TagSuggestions = [];
            return;
        }
        var typed = ActionInput.Split(',').Select(tag => tag.Trim().TrimStart('#')).ToList();
        var current = ShellViewModel.Fold(typed.Count > 0 ? typed[^1] : string.Empty);
        var written = typed.Take(Math.Max(typed.Count - 1, 0)).Select(ShellViewModel.Fold).ToHashSet(StringComparer.Ordinal);
        TagSuggestions =
        [
            .. _knownTags
                .GroupBy(tag => tag, StringComparer.Ordinal)
                .Where(group => !written.Contains(ShellViewModel.Fold(group.Key))
                    && ShellViewModel.Fold(group.Key).StartsWith(current, StringComparison.Ordinal)
                    && ShellViewModel.Fold(group.Key) != current)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.CurrentCulture)
                .Take(MaxTagSuggestions)
                .Select(group => group.Key),
        ];
    }

    private static SheetTarget Of(SheetTarget sheet) => sheet ?? throw new ArgumentNullException(nameof(sheet));
}
