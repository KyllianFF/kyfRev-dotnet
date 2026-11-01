using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// L'accueil, quand aucun coffre n'est ouvert, comme dans la 0.x : créer le coffre que prévoit le
/// mode portable ou installé (en mode installé, un coffre existant peut aussi être repris), ou
/// choisir un dossier pour créer un coffre ou ouvrir un coffre existant. Le message d'un coffre qui
/// n'a pas pu être ouvert y est affiché.
/// </summary>
public sealed partial class WelcomePageViewModel : ObservableObject
{
    private readonly VaultUseCases _vaults;
    private readonly IFolderPicker _picker;
    private readonly Action<AppState> _opened;

    /// <summary>L'accueil pour l'état <paramref name="state"/> ; <paramref name="opened"/> reçoit l'état une fois un coffre ouvert.</summary>
    public WelcomePageViewModel(VaultUseCases vaults, IFolderPicker picker, AppState state, Action<AppState> opened)
    {
        ArgumentNullException.ThrowIfNull(vaults);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(opened);
        _vaults = vaults;
        _picker = picker;
        _opened = opened;
        VaultToCreate = state.VaultToCreate;
        CanOpenExisting = state.VaultToCreate is null || state.Mode == AppMode.Installed;
        Error = state.OpenError;
    }

    /// <summary>Le coffre prévu, à créer ; rien sans mode portable ni installé.</summary>
    public string? VaultToCreate { get; }

    /// <summary>Un coffre est prévu : l'accueil propose de le créer là.</summary>
    public bool HasVaultToCreate => VaultToCreate is not null;

    /// <summary>« Créer le coffre ici » ou « Créer un coffre ».</summary>
    public string CreateLabel => HasVaultToCreate ? ShellText.CreateVaultHere : ShellText.CreateVault;

    /// <summary>Sans coffre prévu, l'accueil rappelle qu'un coffre se crée dans un dossier vide.</summary>
    public bool ExplainsNewVault => !HasVaultToCreate;

    /// <summary>« Ouvrir un coffre existant » est proposé.</summary>
    public bool CanOpenExisting { get; }

    /// <summary>Une ouverture ou une création est en cours.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand), nameof(OpenCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Pourquoi le dernier coffre demandé n'a pas pu être ouvert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Un message d'erreur est affiché.</summary>
    public bool HasError => Error is not null;

    private bool IsIdle => !IsBusy;

    /// <summary>Crée le coffre prévu, ou un coffre dans le dossier vide choisi.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task CreateAsync() => ActAsync(create: true);

    /// <summary>Ouvre le coffre du dossier choisi.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task OpenAsync() => ActAsync(create: false);

    private async Task ActAsync(bool create)
    {
        Error = null;
        var folder = create ? VaultToCreate ?? _picker.Pick(ShellText.ChooseEmptyFolder) : _picker.Pick(ShellText.ChooseVaultFolder);
        if (folder is null)
        {
            return;
        }
        IsBusy = true;
        try
        {
            // L'ouverture lit tout le coffre : hors du fil de l'interface.
            var state = await Task.Run(() => create ? _vaults.Create(folder) : _vaults.Open(folder)).ConfigureAwait(true);
            if (state.Vault is not null)
            {
                _opened(state);
            }
            else
            {
                Error = state.OpenError ?? ShellText.VaultNotOpened;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
