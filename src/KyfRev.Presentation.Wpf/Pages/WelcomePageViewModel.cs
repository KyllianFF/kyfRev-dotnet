using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Ce que l'accueil propose en plus de créer et d'ouvrir un coffre. La coquille le lui donne quand elle le montre.
/// </summary>
/// <param name="SampleFolder">Où créer le coffre d'exemple ; rien : un dossier vide est demandé.</param>
/// <param name="CreateSample">Crée le coffre d'exemple dans ce dossier s'il est vide, l'ouvre, et rend l'état ; ou dit pourquoi il ne l'a pas fait.</param>
/// <param name="OpenGuide">Ouvre le guide dans kyfRev ; rien : l'accueil ne le propose pas.</param>
public sealed record WelcomeOffers(string? SampleFolder, Func<string, Outcome<AppState>> CreateSample, Action? OpenGuide);

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
    private readonly Action<AppState>? _locked;

    /// <summary>
    /// L'accueil pour l'état <paramref name="state"/> ; <paramref name="opened"/> reçoit l'état une fois un coffre ouvert,
    /// <paramref name="locked"/> celui d'un coffre chiffré qui attend sa clé.
    /// </summary>
    public WelcomePageViewModel(VaultUseCases vaults, IFolderPicker picker, AppState state, Action<AppState> opened, Action<AppState>? locked = null)
    {
        ArgumentNullException.ThrowIfNull(vaults);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(opened);
        _vaults = vaults;
        _picker = picker;
        _opened = opened;
        _locked = locked;
        VaultToCreate = state.VaultToCreate;
        CanOpenExisting = state.VaultToCreate is null || state.Mode == AppMode.Installed;
        Error = state.OpenError;
    }

    /// <summary>Le coffre prévu, à créer ; rien sans mode portable ni installé.</summary>
    public string? VaultToCreate { get; }

    /// <summary>
    /// La question quand un autre poste a le coffre ouvert : elle rejoue l'ouverture comme l'utilisateur l'a choisi, ou ne
    /// rend rien ; sans elle, un tel coffre est refusé.
    /// </summary>
    public Func<AppState, Func<AppState>, Task<AppState?>>? Settle { get; init; }

    /// <summary>Reprend l'ouverture de <paramref name="state"/>, faite avant l'accueil (au démarrage) : la question y est posée.</summary>
    public void Continue(AppState state, Func<AppState> retry)
    {
        ArgumentNullException.ThrowIfNull(state);
        _retry = retry;
        Show(state);
    }

    private Func<AppState>? _retry;

    /// <summary>Un coffre est prévu : l'accueil propose de le créer là.</summary>
    public bool HasVaultToCreate => VaultToCreate is not null;

    /// <summary>Ce que fait cette entrée : le coffre prévu, dont le chemin suit, ou un dossier vide à choisir.</summary>
    public string CreateHelp => HasVaultToCreate ? WelcomeText.CreateHere : WelcomeText.CreateAnywhere;

    /// <summary>Le coffre d'exemple et le guide, quand la coquille les propose.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTrySample), nameof(CanOpenGuide))]
    [NotifyCanExecuteChangedFor(nameof(TrySampleCommand))]
    public partial WelcomeOffers? Offers { get; set; }

    /// <summary>« Essayer avec un coffre d'exemple » est proposé.</summary>
    public bool CanTrySample => Offers is not null;

    /// <summary>« Lire le guide » est proposé.</summary>
    public bool CanOpenGuide => Offers?.OpenGuide is not null;

    /// <summary>Sans coffre prévu, l'accueil rappelle qu'un coffre se crée dans un dossier vide.</summary>
    public bool ExplainsNewVault => !HasVaultToCreate;

    /// <summary>« Ouvrir un coffre existant » est proposé.</summary>
    public bool CanOpenExisting { get; }

    /// <summary>Une ouverture ou une création est en cours.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand), nameof(OpenCommand), nameof(TrySampleCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Pourquoi le dernier coffre demandé n'a pas pu être ouvert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Un message d'erreur est affiché.</summary>
    public bool HasError => Error is not null;

    private bool IsIdle => !IsBusy;

    private bool CanStartSample => IsIdle && Offers is not null;

    /// <summary>
    /// Crée le coffre d'exemple dans son dossier, à part du coffre prévu, puis l'ouvre ; s'il y est déjà, il est rouvert tel
    /// qu'il est.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartSample))]
    private async Task TrySampleAsync()
    {
        if (Offers is not { } offers)
        {
            return;
        }
        Error = null;
        var folder = offers.SampleFolder ?? _picker.Pick(WelcomeText.ChooseSampleFolder);
        if (folder is null)
        {
            return;
        }
        IsBusy = true;
        try
        {
            // Le coffre vit ses premières semaines en accéléré : hors du fil de l'interface.
            var created = await Task.Run(() => offers.CreateSample(folder)).ConfigureAwait(true);
            if (created.Succeeded)
            {
                Show(created.Value);
            }
            else
            {
                Error = created.Error;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Ouvre le guide, lu dans kyfRev.</summary>
    [RelayCommand]
    private void OpenGuide() => Offers?.OpenGuide?.Invoke();

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
            _retry = () => _vaults.Open(folder);
            Show(await Task.Run(() => create ? _vaults.Create(folder) : _vaults.Open(folder)).ConfigureAwait(true));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>La question posée, l'ouverture rejouée ; rien n'arrive si l'utilisateur a choisi de ne pas l'ouvrir.</summary>
    private async Task SettleAsync(Func<AppState, Func<AppState>, Task<AppState?>> settle, AppState state, Func<AppState> retry)
    {
        IsBusy = true;
        try
        {
            if (await settle(state, retry).ConfigureAwait(true) is { } settled)
            {
                Show(settled);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Ce qu'il advient du coffre demandé : ouvert, en attente de sa clé, ou refusé avec sa raison.</summary>
    private void Show(AppState state)
    {
        if (state.Occupied is not null)
        {
            if (Settle is { } settle && _retry is { } retry)
            {
                _ = SettleAsync(settle, state, retry);
            }
            else
            {
                Error = ComputersText.Occupied;
            }
            return;
        }
        if (state.Locked is not null && _locked is not null)
        {
            _locked(state);
        }
        else if (state.Vault is not null)
        {
            _opened(state);
        }
        else
        {
            Error = state.OpenError ?? ShellText.VaultNotOpened;
        }
    }
}
