using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>L'onglet « À propos » : ce qui tourne, où, sous quelle licence, et sa mise à jour.</summary>
public sealed partial class AboutSettingsViewModel : ObservableObject, INavigationAware, IForgetsVault
{
    private readonly VaultUseCases _vaults;
    private readonly MachineSettings? _machine;
    private readonly ILegacyInstallation? _legacy;
    private readonly DateOnly? _released;

    /// <summary>
    /// L'onglet, sur le cas d'usage qui rend l'état de l'application ; la mise à jour s'y ajoute si elle est là, avec le
    /// choix de la chercher au démarrage si les préférences de ce poste le sont aussi. La 0.x y est signalée si elle est
    /// encore installée, et le jour de publication s'affiche s'il est connu.
    /// </summary>
    public AboutSettingsViewModel(VaultUseCases vaults, UpdateFlow? updates = null, MachineSettings? machine = null, ILegacyInstallation? legacy = null, DateOnly? released = null)
    {
        ArgumentNullException.ThrowIfNull(vaults);
        _vaults = vaults;
        Updates = updates;
        _machine = machine;
        _legacy = legacy;
        _released = released;
        CheckAtStartup = machine?.Current.CheckUpdatesAtStartup ?? false;
    }

    /// <summary>La 0.x est encore installée : le message qui propose de la retirer, sinon vide.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLegacy))]
    public partial string LegacyMessage { get; private set; } = string.Empty;

    /// <inheritdoc cref="LegacyMessage"/>
    public bool HasLegacy => LegacyMessage.Length > 0;

    /// <summary>« kyfRev 1.0.0-beta.1, publiée le 8 novembre 2026 » ; le numéro seul pour une construction hors publication.</summary>
    [ObservableProperty]
    public partial string Version { get; private set; } = string.Empty;

    /// <summary>« installé (le programme dans Program Files, vos données dans votre profil) ».</summary>
    [ObservableProperty]
    public partial string Mode { get; private set; } = string.Empty;

    /// <summary>Le dossier du coffre ouvert.</summary>
    [ObservableProperty]
    public partial string Vault { get; private set; } = string.Empty;

    /// <summary>La mise à jour : sa recherche, son téléchargement, son installation.</summary>
    public UpdateFlow? Updates { get; }

    /// <summary>La recherche de mise à jour est proposée.</summary>
    public bool CanCheckUpdates => Updates is not null;

    /// <summary>Le choix de chercher au démarrage est proposé.</summary>
    public bool CanCheckAtStartup => Updates is not null && _machine is not null;

    /// <summary>kyfRev cherche une mise à jour à chaque démarrage ; retenu sur ce poste.</summary>
    [ObservableProperty]
    public partial bool CheckAtStartup { get; set; }

    /// <inheritdoc/>
    public void OnNavigatedTo()
    {
        Show(_vaults.State());
        CheckAtStartup = _machine?.Current.CheckUpdatesAtStartup ?? false;
        LegacyMessage = _legacy?.Find() switch
        {
            null => string.Empty,
            { SharesThisFolder: true } shared => SettingsHubText.LegacySharedOf(shared.Version),
            var found => SettingsHubText.LegacyInstalledOf(found.Version),
        };
    }

    /// <summary>Ouvre la page des applications installées de Windows : c'est là que la 0.x se retire.</summary>
    [RelayCommand]
    private void OpenInstalledApps() => _legacy?.OpenInstalledApps();

    /// <summary>Ouvre le guide, lu dans kyfRev ; rien : la section ne le propose pas.</summary>
    public Action? OpenGuide { get; set; }

    /// <summary>« Lire le guide » est proposé.</summary>
    public bool CanOpenGuide => OpenGuide is not null;

    /// <summary>« Lire le guide ».</summary>
    [RelayCommand]
    private void OpenGuideNow() => OpenGuide?.Invoke();

    /// <summary>Oublie le dossier du coffre ; ce qui tourne et sous quel mode reste affiché.</summary>
    public void Forget() => Vault = string.Empty;

    /// <summary>Montre cet état de l'application.</summary>
    public void Show(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Version = _released is { } day ? SettingsHubText.VersionReleasedOf(state.Version, Dates.Spelled(day)) : SettingsHubText.VersionOf(state.Version);
        Mode = state.Mode switch
        {
            AppMode.Portable => SettingsHubText.ModePortable,
            AppMode.Installed => SettingsHubText.ModeInstalled,
            _ => SettingsHubText.ModeDevelopment,
        };
        Vault = state.Vault ?? string.Empty;
    }

    partial void OnCheckAtStartupChanged(bool value)
    {
        if (_machine is { } machine && value != machine.Current.CheckUpdatesAtStartup)
        {
            machine.Update(preferences => preferences with { CheckUpdatesAtStartup = value });
        }
    }
}
