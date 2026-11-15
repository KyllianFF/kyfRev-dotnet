using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Ports;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Où en est la mise à jour.</summary>
public enum UpdateStage
{
    /// <summary>Rien n'a été cherché.</summary>
    Idle,

    /// <summary>La recherche est en cours.</summary>
    Checking,

    /// <summary>La version qui tourne est la dernière.</summary>
    UpToDate,

    /// <summary>Une version plus récente existe ; elle ne s'installe pas d'ici, mais sa page peut s'ouvrir.</summary>
    Available,

    /// <summary>Une version plus récente a été proposée au démarrage, et retardée à la prochaine ouverture.</summary>
    Postponed,

    /// <summary>Son installateur se télécharge.</summary>
    Downloading,

    /// <summary>Son installateur est téléchargé et vérifié : il attend le redémarrage de kyfRev.</summary>
    Ready,

    /// <summary>La mise à jour lancée à la dernière fermeture a réussi.</summary>
    Installed,

    /// <summary>La recherche ou le téléchargement n'a pas abouti.</summary>
    Failed,
}

/// <summary>
/// La mise à jour, de la recherche au redémarrage. On la cherche dans Paramètres › À propos, ou au démarrage si on l'a
/// choisi. Une version trouvée se télécharge et se vérifie dans kyfRev ; elle ne s'installe qu'une fois kyfRev fermé,
/// qui ne peut pas remplacer ses propres fichiers : tout de suite (« Redémarrer maintenant » ferme kyfRev, l'installe sans
/// fenêtre et le rouvre), ou à la prochaine fermeture. Au démarrage suivant, kyfRev dit si elle a réussi. Une copie qui
/// ne vient pas de l'installateur (portable, développement) ouvre seulement la page de la version.
/// </summary>
public sealed partial class UpdateFlow : ObservableObject
{
    private const double Megabyte = 1024 * 1024;

    private readonly UpdateChecker _checker;
    private readonly UpdatePackages? _packages;
    private readonly IExternalLinks? _links;
    private UpdateStatus? _found;
    private Step _step;

    /// <summary>Le dialogue de téléchargement a été masqué : le téléchargement continue, et seul son résultat le rouvre.</summary>
    private bool _hidden;

    /// <summary>L'installation est prévue à la fermeture de kyfRev.</summary>
    private bool _armed;

    /// <summary>kyfRev se rouvrira après l'installation.</summary>
    private bool _reopen;

    /// <summary>La mise à jour, cherchée par <paramref name="checker"/>.</summary>
    /// <param name="checker">La recherche de mise à jour.</param>
    /// <param name="packages">Le téléchargement et l'installation ; rien pour une copie qui ne vient pas de l'installateur.</param>
    /// <param name="links">Le navigateur, pour la page d'une version qui ne s'installe pas d'ici.</param>
    public UpdateFlow(UpdateChecker checker, UpdatePackages? packages = null, IExternalLinks? links = null)
    {
        ArgumentNullException.ThrowIfNull(checker);
        _checker = checker;
        _packages = packages;
        _links = links;
    }

    private enum Step
    {
        None,
        Offer,
        Download,
        Ready,
        NotInstalled,
        Failed,
    }

    /// <summary>Ferme kyfRev ; rend faux si la fenêtre est restée ouverte (une conversion en cours).</summary>
    public Func<bool>? CloseApplication { get; set; }

    /// <summary>Une version trouvée s'installe d'ici : kyfRev vient de l'installateur.</summary>
    public bool CanInstall => _packages?.Scope is not null;

    /// <summary>Ce que fait la recherche, selon que kyfRev s'installe d'ici ou non.</summary>
    public string Help => CanInstall ? SettingsHubText.UpdateHelp : SettingsHubText.UpdateHelpPortable;

    /// <summary>Où en est la mise à jour.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloading), nameof(IsReady), nameof(MessageSeverity))]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand), nameof(RestartNowCommand))]
    public partial UpdateStage Stage { get; private set; }

    /// <summary>Le message dit une réussite (à jour, mise à jour installée), ou une information (une version attend).</summary>
    public NotificationSeverity MessageSeverity => Stage is UpdateStage.UpToDate or UpdateStage.Installed ? NotificationSeverity.Success : NotificationSeverity.Info;

    /// <summary>Le libellé du bouton de recherche.</summary>
    [ObservableProperty]
    public partial string CheckLabel { get; private set; } = SettingsHubText.CheckUpdate;

    /// <summary>Ce que la recherche a trouvé, ou ce qui attend.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; private set; } = string.Empty;

    /// <summary>Pourquoi la recherche, le téléchargement ou l'installation n'a pas abouti.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string Error { get; private set; } = string.Empty;

    /// <summary>La version trouvée ne s'installe pas d'ici : sa page peut s'ouvrir.</summary>
    [ObservableProperty]
    public partial bool HasRelease { get; private set; }

    /// <summary>La part téléchargée, de 0 à 100.</summary>
    [ObservableProperty]
    public partial double Progress { get; private set; }

    /// <summary>« 12 Mo sur 59 Mo ».</summary>
    [ObservableProperty]
    public partial string ProgressText { get; private set; } = string.Empty;

    /// <inheritdoc cref="Message"/>
    public bool HasMessage => Message.Length > 0;

    /// <inheritdoc cref="Error"/>
    public bool HasError => Error.Length > 0;

    /// <summary>L'installateur se télécharge.</summary>
    public bool IsDownloading => Stage == UpdateStage.Downloading;

    /// <summary>L'installateur attend le redémarrage de kyfRev.</summary>
    public bool IsReady => Stage == UpdateStage.Ready;

    /// <summary>Le dialogue de la mise à jour est ouvert.</summary>
    [ObservableProperty]
    public partial bool IsDialogOpen { get; private set; }

    /// <summary>Son titre.</summary>
    [ObservableProperty]
    public partial string DialogTitle { get; private set; } = string.Empty;

    /// <summary>Son message.</summary>
    [ObservableProperty]
    public partial string DialogMessage { get; private set; } = string.Empty;

    /// <summary>Son bouton principal.</summary>
    [ObservableProperty]
    public partial string DialogPrimary { get; private set; } = string.Empty;

    /// <summary>Son bouton secondaire ; vide, il n'y en a pas.</summary>
    [ObservableProperty]
    public partial string DialogSecondary { get; private set; } = string.Empty;

    /// <summary>Le dialogue montre le téléchargement : le bouton principal attend qu'il finisse.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDialogPrimaryEnabled))]
    public partial bool IsDialogDownloading { get; private set; }

    /// <summary>Le bouton principal du dialogue répond : pendant le téléchargement, « Redémarrer maintenant » attend.</summary>
    public bool IsDialogPrimaryEnabled => !IsDialogDownloading;

    /// <summary>
    /// Ce qu'il faut faire une fois kyfRev fermé et ses services rendus (il ne tient plus le verrou qui arrête
    /// l'installateur) : lancer l'installation prévue. Rien si aucune ne l'est.
    /// </summary>
    public Action? ExitLaunch
    {
        get
        {
            if (!_armed || _packages is not { } packages || _found is not { IsNewer: true, Installer: { } file } found)
            {
                return null;
            }
            var reopen = _reopen;
            return () => _ = packages.Launch(found.Latest, file, reopen);
        }
    }

    private bool Installable => CanInstall && _found?.Installer is not null;

    /// <summary>
    /// Au démarrage : dit ce qu'est devenue la mise à jour lancée à la dernière fermeture, puis, si on l'a choisi, cherche
    /// une version plus récente et propose de l'installer. Une recherche qui échoue au démarrage ne dit rien.
    /// </summary>
    /// <param name="checkNow">Le réglage « Rechercher une mise à jour au démarrage ».</param>
    public async Task StartAsync(bool checkNow)
    {
        if (_packages?.TakeAftermath(_checker.Current) is { } aftermath)
        {
            if (!aftermath.Installed)
            {
                ShowNotInstalled(aftermath);
                return;
            }
            Stage = UpdateStage.Installed;
            Message = SettingsHubText.UpdateInstalledOf(aftermath.Version);
        }
        if (!checkNow || !CanCheck())
        {
            return;
        }
        var before = Stage;
        Stage = UpdateStage.Checking;
        CheckLabel = SettingsHubText.CheckingUpdate;
        var result = await _checker.CheckAsync(CancellationToken.None).ConfigureAwait(true);
        CheckLabel = SettingsHubText.CheckUpdate;
        if (!result.Succeeded || !result.Value.IsNewer)
        {
            Stage = before;
            return;
        }
        _found = result.Value;
        Stage = UpdateStage.Available;
        HasRelease = !Installable;
        Message = SettingsHubText.UpdateAvailableOf(_found.Latest, _found.Current);
        Open(Step.Offer);
    }

    /// <summary>Le dialogue s'est fermé, par son bouton principal ou non.</summary>
    public async Task AnswerAsync(bool primary)
    {
        var step = _step;
        _step = Step.None;
        IsDialogOpen = false;
        switch (step)
        {
            case Step.Offer when primary && Installable:
                await DownloadAsync(inDialog: true).ConfigureAwait(true);
                break;
            case Step.Offer when primary:
                OpenRelease();
                break;
            case Step.Offer:
                Stage = UpdateStage.Postponed;
                Message = SettingsHubText.UpdatePostponedOf(_found!.Latest);
                break;
            case Step.Download:
                _hidden = true;
                break;
            case Step.Ready or Step.NotInstalled when primary:
                RestartNow();
                break;
            default:
                break;
        }
    }

    /// <summary>Cherche une mise à jour, à la demande ; une version trouvée qui s'installe d'ici se télécharge aussitôt.</summary>
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckAsync()
    {
        Stage = UpdateStage.Checking;
        CheckLabel = SettingsHubText.CheckingUpdate;
        Message = string.Empty;
        Error = string.Empty;
        HasRelease = false;
        var result = await _checker.CheckAsync(CancellationToken.None).ConfigureAwait(true);
        CheckLabel = SettingsHubText.CheckUpdate;
        if (!result.Succeeded)
        {
            Stage = UpdateStage.Failed;
            Error = result.Error!;
            return;
        }
        _found = result.Value;
        if (!_found.IsNewer)
        {
            Stage = UpdateStage.UpToDate;
            Message = SettingsHubText.UpToDateOf(_found.Current);
            return;
        }
        Message = SettingsHubText.UpdateAvailableOf(_found.Latest, _found.Current);
        if (!Installable)
        {
            Stage = UpdateStage.Available;
            HasRelease = true;
            return;
        }
        await DownloadAsync(inDialog: false).ConfigureAwait(true);
    }

    private bool CanCheck() => Stage is not (UpdateStage.Checking or UpdateStage.Downloading);

    /// <summary>Ferme kyfRev pour installer la mise à jour, puis le rouvre ; une conversion en cours le garde ouvert.</summary>
    [RelayCommand(CanExecute = nameof(IsReady))]
    private void RestartNow()
    {
        Error = string.Empty;
        _armed = true;
        _reopen = true;
        if (CloseApplication?.Invoke() == true)
        {
            return;
        }
        // kyfRev reste ouvert : la mise à jour s'installera à sa fermeture, comme avec « Plus tard ».
        _reopen = false;
        Message = SettingsHubText.UpdateAtExitOf(_found!.Latest);
        Error = SettingsHubText.UpdateCannotClose;
    }

    /// <summary>Ouvre la page de la version trouvée, dans le navigateur.</summary>
    [RelayCommand]
    private void OpenRelease()
    {
        if (_found is { } found)
        {
            _links?.Open(found.Page.ToString());
        }
    }

    private async Task DownloadAsync(bool inDialog)
    {
        var found = _found!;
        var file = found.Installer!;
        Stage = UpdateStage.Downloading;
        Error = string.Empty;
        Message = SettingsHubText.UpdateDownloadingShortOf(found.Latest);
        Report(0, file.Size);
        _hidden = !inDialog;
        if (inDialog)
        {
            Open(Step.Download);
        }
        var progress = new Progress<long>(received =>
        {
            if (Stage == UpdateStage.Downloading)
            {
                Report(received, file.Size);
            }
        });
        var result = await _packages!.DownloadAsync(file, progress, CancellationToken.None).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            Stage = UpdateStage.Failed;
            Message = string.Empty;
            Error = result.Error!;
            if (_hidden)
            {
                Dismiss();
            }
            else
            {
                Open(Step.Failed);
            }
            return;
        }
        Report(file.Size, file.Size);
        Stage = UpdateStage.Ready;
        _armed = true;
        Message = SettingsHubText.UpdateAtExitOf(found.Latest);
        Open(Step.Ready);
    }

    /// <summary>La mise à jour lancée à la dernière fermeture n'est pas installée : on peut réessayer si son installateur est encore là.</summary>
    private void ShowNotInstalled(UpdateAftermath aftermath)
    {
        Error = SettingsHubText.UpdateNotInstalledOf(aftermath.Version, _checker.Current, Path.Combine(_packages!.Folder, UpdatePackages.LogFile));
        if (aftermath.Installer is { } file && _packages.Find(file) is not null)
        {
            _found = new UpdateStatus(_checker.Current, aftermath.Version, true, UpdateChecker.PageOf(aftermath.Version)) { Installer = file };
            Stage = UpdateStage.Ready;
            Open(Step.NotInstalled);
        }
        else
        {
            Stage = UpdateStage.Failed;
            Open(Step.Failed);
        }
    }

    private void Report(long received, long total)
    {
        Progress = total > 0 ? Math.Min(100, received * 100.0 / total) : 0;
        ProgressText = SettingsHubText.UpdateProgressOf(Megabytes(received), Megabytes(total));
    }

    private static string Megabytes(long bytes) => (bytes / Megabyte).ToString("0", CultureInfo.CurrentCulture);

    private void Open(Step step)
    {
        var latest = _found?.Latest ?? string.Empty;
        _step = step;
        (DialogTitle, DialogMessage, DialogPrimary, DialogSecondary) = step switch
        {
            Step.Offer when Installable => (SettingsHubText.UpdateOfferTitle, SettingsHubText.UpdateOfferOf(latest, _checker.Current), SettingsHubText.UpdateNow, SettingsHubText.UpdatePostpone),
            Step.Offer => (SettingsHubText.UpdateOfferTitle, SettingsHubText.UpdateAvailableOf(latest, _checker.Current), SettingsHubText.OpenRelease, SettingsHubText.UpdatePostpone),
            Step.Download => (SettingsHubText.UpdateDownloadingTitleOf(latest), SettingsHubText.UpdateDownloading, SettingsHubText.RestartNow, SettingsHubText.UpdateHide),
            Step.Ready => (SettingsHubText.UpdateReadyTitle, ReadyMessage(latest), SettingsHubText.RestartNow, SettingsHubText.RestartLater),
            Step.NotInstalled => (SettingsHubText.UpdateNotInstalledTitle, Error, SettingsHubText.UpdateRetry, DesignText.Close),
            _ => (SettingsHubText.UpdateFailedTitle, Error, DesignText.Close, string.Empty),
        };
        IsDialogDownloading = step == Step.Download;
        _hidden = false;
        IsDialogOpen = true;
    }

    private void Dismiss()
    {
        _step = Step.None;
        IsDialogDownloading = false;
        IsDialogOpen = false;
    }

    private string ReadyMessage(string latest) =>
        _packages?.Scope == InstallScope.AllUsers ? $"{SettingsHubText.UpdateReadyOf(latest)} {SettingsHubText.UpdateAllUsers}" : SettingsHubText.UpdateReadyOf(latest);
}
