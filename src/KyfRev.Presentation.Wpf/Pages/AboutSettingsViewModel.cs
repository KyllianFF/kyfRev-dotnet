using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>L'onglet « À propos » : ce qui tourne, où, et sous quelle licence.</summary>
public sealed partial class AboutSettingsViewModel : ObservableObject, INavigationAware, IForgetsVault
{
    private readonly VaultUseCases _vaults;
    private readonly UpdateChecker? _updates;
    private readonly IExternalLinks? _links;
    private readonly ILegacyInstallation? _legacy;
    private Uri? _release;

    /// <summary>L'onglet, sur le cas d'usage qui rend l'état de l'application ; la recherche de mise à jour s'y ajoute si elle est là, et la 0.x y est signalée si elle est encore installée.</summary>
    public AboutSettingsViewModel(VaultUseCases vaults, UpdateChecker? updates = null, IExternalLinks? links = null, ILegacyInstallation? legacy = null)
    {
        ArgumentNullException.ThrowIfNull(vaults);
        _vaults = vaults;
        _updates = updates;
        _links = links;
        _legacy = legacy;
    }

    /// <summary>La 0.x est encore installée : le message qui propose de la retirer, sinon vide.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLegacy))]
    public partial string LegacyMessage { get; private set; } = string.Empty;

    /// <inheritdoc cref="LegacyMessage"/>
    public bool HasLegacy => LegacyMessage.Length > 0;

    /// <summary>« kyfRev 1.0.0-alpha.0 ».</summary>
    [ObservableProperty]
    public partial string Version { get; private set; } = string.Empty;

    /// <summary>« installé (le programme dans Program Files, vos données dans votre profil) ».</summary>
    [ObservableProperty]
    public partial string Mode { get; private set; } = string.Empty;

    /// <summary>Le dossier du coffre ouvert.</summary>
    [ObservableProperty]
    public partial string Vault { get; private set; } = string.Empty;

    /// <summary>La recherche de mise à jour est proposée.</summary>
    public bool CanCheckUpdates => _updates is not null;

    /// <summary>Le libellé du bouton de recherche.</summary>
    [ObservableProperty]
    public partial string CheckLabel { get; private set; } = SettingsHubText.CheckUpdate;

    /// <summary>Ce que la recherche a trouvé.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdateMessage))]
    public partial string UpdateMessage { get; private set; } = string.Empty;

    /// <summary>Pourquoi la recherche n'a pas abouti.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdateError))]
    public partial string UpdateError { get; private set; } = string.Empty;

    /// <summary>Une version plus récente attend : sa page peut s'ouvrir.</summary>
    [ObservableProperty]
    public partial bool HasRelease { get; private set; }

    /// <inheritdoc cref="UpdateMessage"/>
    public bool HasUpdateMessage => UpdateMessage.Length > 0;

    /// <inheritdoc cref="UpdateError"/>
    public bool HasUpdateError => UpdateError.Length > 0;

    /// <inheritdoc/>
    public void OnNavigatedTo()
    {
        Show(_vaults.State());
        LegacyMessage = _legacy?.Find() is { } found ? SettingsHubText.LegacyInstalledOf(found.Version) : string.Empty;
    }

    /// <summary>Ouvre la page des applications installées de Windows : c'est là que la 0.x se retire.</summary>
    [RelayCommand]
    private void OpenInstalledApps() => _legacy?.OpenInstalledApps();

    /// <summary>Oublie le dossier du coffre ; ce qui tourne et sous quel mode reste affiché.</summary>
    public void Forget() => Vault = string.Empty;

    /// <summary>Montre cet état de l'application.</summary>
    public void Show(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Version = SettingsHubText.VersionOf(state.Version);
        Mode = state.Mode switch
        {
            AppMode.Portable => SettingsHubText.ModePortable,
            AppMode.Installed => SettingsHubText.ModeInstalled,
            _ => SettingsHubText.ModeDevelopment,
        };
        Vault = state.Vault ?? string.Empty;
    }

    /// <summary>Cherche une mise à jour, seulement à la demande : rien n'est téléchargé ni installé.</summary>
    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        if (_updates is null)
        {
            return;
        }
        CheckLabel = SettingsHubText.CheckingUpdate;
        UpdateMessage = string.Empty;
        UpdateError = string.Empty;
        HasRelease = false;
        var result = await _updates.CheckAsync(CancellationToken.None).ConfigureAwait(true);
        CheckLabel = SettingsHubText.CheckUpdate;
        if (!result.Succeeded)
        {
            UpdateError = result.Error!;
            return;
        }
        _release = result.Value.Page;
        HasRelease = result.Value.IsNewer;
        UpdateMessage = result.Value.IsNewer ? SettingsHubText.UpdateAvailableOf(result.Value.Latest, result.Value.Current) : SettingsHubText.UpToDateOf(result.Value.Current);
    }

    /// <summary>Ouvre la page de la version trouvée, dans le navigateur.</summary>
    [RelayCommand]
    private void OpenRelease()
    {
        if (_release is { } page)
        {
            _links?.Open(page.ToString());
        }
    }
}
