using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>L'onglet « À propos » : ce qui tourne, où, et sous quelle licence.</summary>
public sealed partial class AboutSettingsViewModel : ObservableObject, INavigationAware
{
    private readonly VaultUseCases _vaults;

    /// <summary>L'onglet, sur le cas d'usage qui rend l'état de l'application.</summary>
    public AboutSettingsViewModel(VaultUseCases vaults)
    {
        ArgumentNullException.ThrowIfNull(vaults);
        _vaults = vaults;
    }

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
    public void OnNavigatedTo() => Show(_vaults.State());

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
