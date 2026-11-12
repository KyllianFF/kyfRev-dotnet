using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Modules.Citations;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Une sauvegarde, telle que la liste la montre.</summary>
/// <param name="Name">Le nom de son fichier.</param>
/// <param name="Moment">« 12 oct. 2026, 08:30 ».</param>
/// <param name="Size">« 2,4 Mo ».</param>
public sealed record BackupRow(string Name, string Moment, string Size)
{
    /// <summary>Le nom du bouton : toutes les lignes ont le même, le moment les distingue.</summary>
    public string RestoreName => SettingsHubText.RestoreBackupOf(Moment);
}

/// <summary>Un élément supprimé, restaurable.</summary>
public sealed class TrashRow
{
    internal TrashRow(string title, string detail, IRelayCommand restore, IRelayCommand? openSheet = null, Func<string?>? eraser = null)
    {
        Title = title;
        Detail = detail;
        Restore = restore;
        OpenSheet = openSheet;
        Eraser = eraser;
    }

    /// <summary>Le titre de la fiche, ou le passage.</summary>
    public string Title { get; }

    /// <summary>Ce que c'est, et d'où ça vient.</summary>
    public string Detail { get; }

    /// <summary>Le remet à sa place.</summary>
    public IRelayCommand Restore { get; }

    /// <summary>Le nom du bouton : toutes les lignes ont le même, le titre les distingue.</summary>
    public string RestoreName => SettingsHubText.RestoreItemOf(Title);

    /// <summary>Ouvre la fiche d'où vient le passage ; rien pour une fiche, qui est elle-même supprimée.</summary>
    public IRelayCommand? OpenSheet { get; }

    /// <summary>Le passage a une fiche à ouvrir.</summary>
    public bool HasSheet => OpenSheet is not null;

    /// <summary>Le nom du bouton « Fiche ».</summary>
    public string OpenSheetName => SettingsHubText.OpenSheetOf(Detail);

    /// <summary>Le nom du bouton qui l'efface pour de bon.</summary>
    public string EraseName => SettingsHubText.EraseItemOf(Title);

    /// <summary>L'efface pour de bon ; rend son refus, ou rien.</summary>
    internal Func<string?>? Eraser { get; }
}

/// <summary>
/// Les sections « Coffre et chiffrement » et « Sauvegardes et exports » : le coffre, ses sauvegardes, ce qui a été supprimé et les exports, comme dans la 0.x. Rien n'est
/// jamais purgé, et un export ne remplace aucun fichier.
/// </summary>
public sealed partial class DataSettingsViewModel : ObservableObject, INavigationAware, IForgetsVault
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly VaultUseCases _vaults;
    private readonly BackupUseCases _backups;
    private readonly TrashUseCases _trash;
    private readonly AnnotationUseCases _annotations;
    private readonly ExportUseCases _exports;
    private readonly SettingsUseCases _settings;
    private readonly IFolderPicker _picker;
    private readonly IReviewOpener _opener;
    private readonly Action _vaultChanged;
    private readonly Func<QuoteUseCases?> _quotes;
    private BackupRow? _restoring;
    private TrashRow? _erasing;

    /// <summary>Change à chaque oubli du coffre : une lecture ou une action partie avant n'y remet rien.</summary>
    private int _generation;

    /// <summary>L'onglet ; <paramref name="vaultChanged"/> ramène l'application sur le coffre qui vient d'être ouvert.</summary>
    public DataSettingsViewModel(
        VaultUseCases vaults,
        BackupUseCases backups,
        TrashUseCases trash,
        AnnotationUseCases annotations,
        ExportUseCases exports,
        SettingsUseCases settings,
        IFolderPicker picker,
        IReviewOpener opener,
        Action vaultChanged,
        Func<QuoteUseCases?>? quotes = null)
    {
        ArgumentNullException.ThrowIfNull(vaults);
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(trash);
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(exports);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(opener);
        ArgumentNullException.ThrowIfNull(vaultChanged);
        _vaults = vaults;
        _backups = backups;
        _trash = trash;
        _annotations = annotations;
        _exports = exports;
        _settings = settings;
        _picker = picker;
        _opener = opener;
        _vaultChanged = vaultChanged;
        _quotes = quotes ?? (() => null);
    }

    /// <summary>« Ouvert : … ».</summary>
    [ObservableProperty]
    public partial string VaultLine { get; private set; } = string.Empty;

    /// <summary>Ce qui se passe quand on change de coffre, selon le mode de l'application.</summary>
    [ObservableProperty]
    public partial string VaultHelp { get; private set; } = string.Empty;

    /// <summary>« Une sauvegarde .zip quotidienne, vérifiée, rangée hors du coffre dans … ».</summary>
    [ObservableProperty]
    public partial string BackupHelp { get; private set; } = string.Empty;

    /// <summary>Les sauvegardes, de la plus récente à la plus ancienne.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackups), nameof(HasNoBackup))]
    public partial IReadOnlyList<BackupRow> Backups { get; private set; } = [];

    /// <summary>Il y a au moins une sauvegarde.</summary>
    public bool HasBackups => Backups.Count > 0;

    /// <summary>Aucune sauvegarde pour l'instant.</summary>
    public bool HasNoBackup => IsLoaded && Backups.Count == 0;

    /// <summary>Garder 14 quotidiennes, 8 hebdomadaires, 24 mensuelles.</summary>
    [ObservableProperty]
    public partial bool Rotation { get; set; }

    /// <summary>Le dossier de la sauvegarde qui vient d'être restaurée, à ouvrir si l'on veut.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRestoredVault))]
    public partial string? RestoredVault { get; private set; }

    /// <summary>Une sauvegarde vient d'être restaurée.</summary>
    public bool HasRestoredVault => RestoredVault is not null;

    /// <summary>Les éléments supprimés.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTrash), nameof(HasNoTrash))]
    public partial IReadOnlyList<TrashRow> Trash { get; private set; } = [];

    /// <summary>Il y a au moins un élément supprimé.</summary>
    public bool HasTrash => Trash.Count > 0;

    /// <summary>Aucun élément supprimé.</summary>
    public bool HasNoTrash => IsLoaded && Trash.Count == 0;

    /// <summary>Le module Citations est actif : ses citations s'exportent.</summary>
    [ObservableProperty]
    public partial bool ExportsQuotes { get; private set; }

    /// <summary>L'onglet a été lu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoBackup), nameof(HasNoTrash))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>Une opération est en cours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Rien n'est en cours : les boutons répondent.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Refus de la dernière opération.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Une opération a été refusée.</summary>
    public bool HasError => Error is not null;

    /// <summary>Résultat de la dernière action.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    /// <summary>Une action vient d'aboutir.</summary>
    public bool HasMessage => Message is not null;

    /// <summary>L'effacement d'un élément attend sa confirmation.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingErase { get; private set; }

    /// <summary>Ce que la confirmation de l'effacement annonce.</summary>
    [ObservableProperty]
    public partial string EraseMessage { get; private set; } = string.Empty;

    /// <summary>La restauration d'une sauvegarde attend sa confirmation.</summary>
    [ObservableProperty]
    public partial bool IsConfirming { get; private set; }

    /// <summary>Ce que la confirmation annonce.</summary>
    [ObservableProperty]
    public partial string ConfirmMessage { get; private set; } = string.Empty;

    /// <summary>La carte « Chiffrement », qui ne se montre que pour un coffre chiffré ; rien : l'onglet ne la propose pas.</summary>
    public EncryptionSettingsViewModel? Encryption { get; init; }

    /// <summary>« Chiffrer ce coffre… » : la page de la conversion s'ouvre ; rien : l'onglet ne le propose pas.</summary>
    public Action? EncryptionAsked { get; init; }

    /// <summary>Le coffre ouvert est en clair, et peut se chiffrer d'ici.</summary>
    [ObservableProperty]
    public partial bool CanEncrypt { get; private set; }

    /// <inheritdoc/>
    public void OnNavigatedTo()
    {
        Error = null;
        Message = null;
        RestoredVault = null;
        Encryption?.Reload();
        CanEncrypt = EncryptionAsked is not null && _vaults.State() is { Vault: not null, Encrypted: false };
        ReloadCommand.Execute(null);
    }

    /// <summary>« Chiffrer ce coffre… » : la page de la conversion remplace les vues, le coffre restant ouvert derrière.</summary>
    [RelayCommand]
    private void RequestEncryption()
    {
        if (CanEncrypt && IsIdle)
        {
            EncryptionAsked?.Invoke();
        }
    }

    /// <summary>
    /// Oublie ce que l'onglet a lu du coffre : son chemin, ses sauvegardes, ce qui a été supprimé, les messages et les
    /// confirmations en attente, et ce que la carte « Chiffrement » en montrait ; l'onglet se relit à sa prochaine ouverture.
    /// </summary>
    public void Forget()
    {
        _generation++;
        Encryption?.Forget();
        CanEncrypt = false;
        _restoring = null;
        _erasing = null;
        VaultLine = string.Empty;
        VaultHelp = string.Empty;
        BackupHelp = string.Empty;
        Backups = [];
        Rotation = false;
        RestoredVault = null;
        Trash = [];
        ExportsQuotes = false;
        IsLoaded = false;
        Error = null;
        Message = null;
        IsConfirmingErase = false;
        EraseMessage = string.Empty;
        IsConfirming = false;
        ConfirmMessage = string.Empty;
    }

    /// <summary>« 2,4 Mo » pour une taille en octets.</summary>
    internal static string SizeOf(long bytes) => bytes switch
    {
        < 1024 => SettingsHubText.BytesOf(bytes.ToString(French)),
        < 1024 * 1024 => SettingsHubText.KilobytesOf((bytes / 1024.0).ToString("0.#", French)),
        _ => SettingsHubText.MegabytesOf((bytes / (1024.0 * 1024)).ToString("0.#", French)),
    };

    private static string Moment(DateTime timestamp) => timestamp.ToString("d MMM yyyy, HH:mm", French);

    /// <summary>Relit tout l'onglet.</summary>
    [RelayCommand]
    private async Task ReloadAsync()
    {
        var state = _vaults.State();
        VaultLine = SettingsHubText.VaultOpenOf(state.Vault ?? string.Empty);
        VaultHelp = state.Mode == AppMode.Portable ? SettingsHubText.VaultHelpPortable : SettingsHubText.VaultHelpInstalled;
        var quotes = _quotes();
        ExportsQuotes = quotes is not null;
        var generation = _generation;
        var (backups, sheets, passages, quoteList) = await Task.Run(() => (_backups.List(), _trash.TrashedSheets(), _trash.TrashedAnnotations(), quotes?.Quotes())).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant la lecture : elle n'y remet rien.
            return;
        }
        if (!backups.Succeeded)
        {
            Error = backups.Error;
            return;
        }
        BackupHelp = SettingsHubText.BackupHelpOf(backups.Value.Folder);
        Rotation = backups.Value.Rotation;
        Backups = [.. backups.Value.Backups.Select(backup => new BackupRow(backup.Name, Moment(backup.Timestamp), SizeOf(backup.Size)))];
        var rows = new List<TrashRow>();
        if (sheets.Succeeded)
        {
            rows.AddRange(sheets.Value.Select(SheetRow));
        }
        if (passages.Succeeded)
        {
            rows.AddRange(passages.Value.Select(PassageRow));
        }
        if (quoteList is { Succeeded: true } listed)
        {
            rows.AddRange(listed.Value.Trash.Select(quote => QuoteRow(quotes!, quote)));
        }
        Trash = rows;
        IsLoaded = true;
    }

    private TrashRow SheetRow(TrashedSheet sheet)
    {
        var parts = new List<string> { SettingsHubText.SheetKind };
        if (sheet.Folder.Length > 0)
        {
            parts.Add(sheet.Folder.Replace("/", " › ", StringComparison.Ordinal));
        }
        parts.Add(SettingsHubText.TrashedOnOf(sheet.TrashedAt.LocalDateTime.ToString("d MMMM yyyy", French)));
        if (sheet.Annotations > 0)
        {
            parts.Add(Dates.Plural(sheet.Annotations, SettingsHubText.AnnotationOneOf, SettingsHubText.AnnotationManyOf));
        }
        return new TrashRow(
            sheet.Title,
            string.Join(" · ", parts),
            new AsyncRelayCommand(() => ActAsync(() => _trash.RestoreSheet(sheet.Id).Error, SettingsHubText.RestoredOf(sheet.Title))),
            eraser: () => _trash.PurgeSheet(sheet.Id).Error);
    }

    private TrashRow PassageRow(TrashedAnnotation passage)
    {
        var detail = passage.Comment is { Length: > 0 } comment ? string.Join(" · ", passage.Title, comment) : passage.Title;
        return new TrashRow(
            SettingsHubText.QuotedOf(passage.Exact),
            detail,
            new AsyncRelayCommand(() => ActAsync(() => _annotations.Restore(passage.Id).Error, SettingsHubText.PassageRestored)),
            new RelayCommand(() => _opener.OpenSheet(passage.SheetId)),
            () => _annotations.Purge(passage.Id).Error);
    }

    private TrashRow QuoteRow(QuoteUseCases quotes, Quote quote) => new(
        SettingsHubText.QuotedOf(quote.Content.Text),
        QuotesText.DataKindOf(quote.Content.Author ?? QuotesText.UnknownAuthor),
        new AsyncRelayCommand(() => ActAsync(() => quotes.Restore(quote.Path).Error, QuotesText.Restored)),
        eraser: () => quotes.Purge(quote.Path).Error);

    /// <summary>Demande confirmation avant d'effacer un élément pour de bon.</summary>
    [RelayCommand]
    private void RequestErase(TrashRow row)
    {
        _erasing = row;
        EraseMessage = SettingsHubText.EraseConfirmOf(row.Title);
        IsConfirmingErase = true;
    }

    /// <summary>La réponse à l'effacement : rien n'est effacé sans un oui.</summary>
    [RelayCommand]
    private async Task AnswerEraseAsync(bool confirmed)
    {
        IsConfirmingErase = false;
        if (confirmed && _erasing is { Eraser: { } eraser } row)
        {
            await ActAsync(eraser, SettingsHubText.ErasedOf(row.Title)).ConfigureAwait(true);
        }
    }

    /// <summary>« Sauvegarder maintenant ».</summary>
    [RelayCommand]
    private Task BackupNowAsync() => ActAsync(() => _backups.BackupNow().Error, SettingsHubText.BackupDone);

    /// <summary>Demande confirmation avant de restaurer une sauvegarde.</summary>
    [RelayCommand]
    private void RequestRestore(BackupRow backup)
    {
        _restoring = backup;
        ConfirmMessage = SettingsHubText.RestoreConfirmOf(backup.Moment);
        IsConfirming = true;
    }

    /// <summary>La réponse à la confirmation : le coffre actuel n'est jamais modifié, la sauvegarde va dans un nouveau dossier.</summary>
    [RelayCommand]
    private async Task AnswerRestoreAsync(bool confirmed)
    {
        IsConfirming = false;
        if (!confirmed || _restoring is not { } backup)
        {
            return;
        }
        IsBusy = true;
        Error = null;
        Message = null;
        try
        {
            var generation = _generation;
            var outcome = await Task.Run(() => _backups.Restore(backup.Name)).ConfigureAwait(true);
            if (generation != _generation)
            {
                // Le coffre a été oublié pendant l'extraction : son dossier ne se montre pas.
                return;
            }
            if (outcome.Succeeded)
            {
                RestoredVault = outcome.Value;
                Message = SettingsHubText.BackupRestoredOf(outcome.Value);
            }
            else
            {
                Error = outcome.Error;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>« Ouvrir ce coffre » : la sauvegarde qui vient d'être restaurée.</summary>
    [RelayCommand]
    private Task OpenRestoredVaultAsync() => RestoredVault is { } folder ? ChangeVaultAsync(() => _vaults.Open(folder)) : Task.CompletedTask;

    /// <summary>« Ouvrir un autre coffre… ».</summary>
    [RelayCommand]
    private Task OpenVaultAsync() =>
        _picker.Pick(SettingsHubText.PickVault) is { } folder ? ChangeVaultAsync(() => _vaults.Open(folder)) : Task.CompletedTask;

    /// <summary>« Créer un nouveau coffre… ».</summary>
    [RelayCommand]
    private Task CreateVaultAsync() =>
        _picker.Pick(SettingsHubText.PickNewVault) is { } folder ? ChangeVaultAsync(() => _vaults.Create(folder)) : Task.CompletedTask;

    /// <summary>
    /// Ce qui montre le déverrouillage d'un coffre chiffré choisi ici, l'actuel restant ouvert derrière ; rien : un coffre
    /// chiffré ne s'ouvre pas d'ici.
    /// </summary>
    public Action<AppState>? VaultLocked { get; init; }

    /// <summary>Ouvrir un coffre peut prendre du temps (la journée à préparer, sa sauvegarde) ; un refus laisse l'actuel ouvert.</summary>
    private async Task ChangeVaultAsync(Func<AppState> opening)
    {
        var before = _vaults.State().Vault;
        var generation = _generation;
        IsBusy = true;
        Error = null;
        Message = null;
        try
        {
            var state = await Task.Run(opening).ConfigureAwait(true);
            if (generation != _generation)
            {
                // Le coffre s'est verrouillé pendant l'ouverture : son écran de déverrouillage reste, rien ne le remplace.
                return;
            }
            if (state.Locked is not null)
            {
                // Un coffre chiffré attend sa clé : son écran la demande, l'actuel reste ouvert jusque-là.
                if (VaultLocked is { } locked)
                {
                    locked(state);
                }
                else
                {
                    Error = state.OpenError ?? EncryptionText.NotUnlocked;
                }
            }
            else if (state.OpenError is { } problem)
            {
                Error = problem;
            }
            else if (state.Vault == before)
            {
                Message = SettingsHubText.VaultAlreadyOpen;
            }
            else
            {
                RestoredVault = null;
                _vaultChanged();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>La rotation s'enregistre aussitôt, sans rien changer d'autre.</summary>
    [RelayCommand]
    private void SaveRotation()
    {
        var rotation = Rotation;
        var outcome = _settings.Update(preferences => preferences with { BackupRotation = rotation });
        Error = outcome.Error;
    }

    /// <summary>« Suivi des révisions (CSV) », « Annotations (Markdown) », « Coffre complet (.zip) ».</summary>
    [RelayCommand]
    private async Task ExportAsync(ExportKind kind)
    {
        if (_picker.Pick(SettingsHubText.PickExport) is not { } folder)
        {
            return;
        }
        IsBusy = true;
        Error = null;
        Message = null;
        try
        {
            var outcome = await Task.Run(() => _exports.Export(kind, folder)).ConfigureAwait(true);
            if (outcome.Succeeded)
            {
                Message = SettingsHubText.ExportedOf(folder);
            }
            else
            {
                Error = outcome.Error;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Une action, puis l'onglet relu ; <paramref name="action"/> rend son refus, ou rien.</summary>
    private async Task ActAsync(Func<string?> action, string done)
    {
        IsBusy = true;
        Error = null;
        Message = null;
        try
        {
            var generation = _generation;
            var refusal = await Task.Run(action).ConfigureAwait(true);
            if (generation != _generation)
            {
                // Le coffre a été oublié pendant l'action : ni son message ni la relecture.
                return;
            }
            if (refusal is not null)
            {
                Error = refusal;
                return;
            }
            Message = done;
            await ReloadAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
