using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>L'onglet « À propos » : ce qui tourne, où, et sous quelle licence.</summary>
public sealed partial class AboutSettingsViewModel : ObservableObject, INavigationAware, IForgetsVault
{
    private readonly VaultUseCases _vaults;
    private readonly ILegacyInstallation? _legacy;

    /// <summary>L'onglet, sur le cas d'usage qui rend l'état de l'application ; la 0.x y est signalée si elle est encore là.</summary>
    public AboutSettingsViewModel(VaultUseCases vaults, ILegacyInstallation? legacy = null)
    {
        ArgumentNullException.ThrowIfNull(vaults);
        _vaults = vaults;
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
}
