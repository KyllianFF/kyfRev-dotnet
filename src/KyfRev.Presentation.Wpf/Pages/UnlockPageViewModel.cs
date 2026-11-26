using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// L'écran de déverrouillage d'un coffre chiffré : son mot de passe l'ouvre, ou, perdu, sa clé de récupération ; sa clé
/// peut être retenue sur ce poste (décoché par défaut). Rien du coffre n'est lu avant : ni le menu ni la recherche ne sont
/// là. Un autre coffre peut être ouvert à la place ; si un coffre était déjà ouvert (le changement venait des paramètres),
/// on peut aussi y revenir.
/// </summary>
public sealed partial class UnlockPageViewModel : ObservableObject
{
    private readonly VaultUseCases _vaults;
    private readonly IFolderPicker _picker;
    private readonly Action<AppState> _opened;
    private readonly Action<AppState> _locked;
    private readonly Action? _back;

    /// <summary>L'écran pour le coffre chiffré qu'attend <paramref name="state"/> (<see cref="AppState.Locked"/>).</summary>
    /// <param name="vaults">Ouverture des coffres.</param>
    /// <param name="picker">Le choix d'un autre coffre.</param>
    /// <param name="state">L'état qui attend une clé, avec la raison d'un refus.</param>
    /// <param name="opened">Reçoit l'état une fois un coffre ouvert.</param>
    /// <param name="locked">Reçoit l'état quand l'autre coffre choisi est lui aussi chiffré.</param>
    /// <param name="back">Revient au coffre ouvert ; rien s'il n'y en a pas.</param>
    public UnlockPageViewModel(VaultUseCases vaults, IFolderPicker picker, AppState state, Action<AppState> opened, Action<AppState> locked, Action? back = null)
    {
        ArgumentNullException.ThrowIfNull(vaults);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(locked);
        if (state.Locked is not { } vault)
        {
            throw new ArgumentException("L'état n'attend la clé d'aucun coffre.", nameof(state));
        }
        _vaults = vaults;
        _picker = picker;
        _opened = opened;
        _locked = locked;
        _back = back;
        Vault = vault;
        Error = state.OpenError;
        BackLabel = state.Vault is null ? EncryptionText.BackToWelcome : EncryptionText.BackToVault;
        UsesHello = vaults.UsesHello;
        OffersHello = vaults.OffersHello(vault);
        // Sa clé est retenue avec Windows Hello : l'ouvrir par le mot de passe, une fois, ne l'oublie pas.
        Remember = OffersHello;
    }

    /// <summary>Windows Hello est configuré sur ce poste : la case de la clé retenue le dit.</summary>
    public bool UsesHello { get; }

    /// <summary>« Se souvenir sur cet ordinateur », ou, avec Windows Hello, « Ouvrir avec Windows Hello sur cet ordinateur ».</summary>
    public string RememberLabel => UsesHello ? EncryptionText.RememberHello : EncryptionText.Remember;

    /// <summary>Ce que fait la case.</summary>
    public string RememberHelp => UsesHello ? EncryptionText.RememberHelloHelp : EncryptionText.RememberHelp;

    /// <summary>Le coffre s'ouvre avec Windows Hello : son bouton remplace le mot de passe, qui reste à un clic.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSecret), nameof(Intro))]
    public partial bool OffersHello { get; private set; }

    /// <summary>Le mot de passe, ou la clé de récupération, est demandé.</summary>
    public bool ShowsSecret => !OffersHello;

    /// <summary>Le dossier du coffre chiffré.</summary>
    public string Vault { get; }

    /// <summary>Le nom du coffre : son dossier.</summary>
    public string VaultName => Path.GetFileName(Path.TrimEndingDirectorySeparator(Vault)) is { Length: > 0 } name ? name : Vault;

    /// <summary>On peut quitter cet écran : pour le coffre resté ouvert derrière, ou sinon pour l'accueil.</summary>
    public bool CanGoBack => _back is not null;

    /// <summary>« Revenir au coffre ouvert », ou « Retour à l'accueil » quand aucun coffre ne l'est.</summary>
    public string BackLabel { get; }

    /// <summary>La clé de récupération est demandée à la place du mot de passe.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesPassword), nameof(Intro))]
    public partial bool UsesRecoveryKey { get; private set; }

    /// <summary>Le mot de passe est demandé.</summary>
    public bool UsesPassword => !UsesRecoveryKey;

    /// <summary>Ce qui ouvre le coffre, dit en une phrase.</summary>
    public string Intro => OffersHello ? EncryptionText.HelloIntro : UsesRecoveryKey ? EncryptionText.RecoveryIntro : EncryptionText.LockedIntro;

    /// <summary>Retenir la clé sur ce poste ; décoché par défaut.</summary>
    [ObservableProperty]
    public partial bool Remember { get; set; }

    /// <summary>Une ouverture est en cours : la dérivation du mot de passe prend de l'ordre d'une seconde.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand), nameof(OtherVaultCommand), nameof(BackCommand), nameof(UseRecoveryKeyCommand), nameof(UsePasswordCommand), nameof(UnlockWithHelloCommand), nameof(TypePasswordCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Rien n'est en cours.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Pourquoi le coffre ne s'est pas ouvert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Un message d'erreur est affiché.</summary>
    public bool HasError => Error is not null;

    /// <summary>La question quand un autre poste a le coffre ouvert ; sans elle, un tel coffre est refusé.</summary>
    public Func<AppState, Func<AppState>, Task<AppState?>>? Settle { get; init; }

    /// <summary>L'état, une fois la question posée si un autre poste a le coffre ouvert ; rien si l'utilisateur renonce.</summary>
    private async Task<AppState?> SettledAsync(AppState state, Func<AppState> retry) =>
        state.Occupied is null ? state
        : Settle is { } settle ? await settle(state, retry).ConfigureAwait(true)
        : state with { Occupied = null, OpenError = ComputersText.Occupied };

    /// <summary>
    /// Ouvre le coffre avec <paramref name="secret"/> : son mot de passe, ou sa clé de récupération telle qu'elle a été
    /// recopiée. La vue le passe sans le garder, et vide le champ du mot de passe.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task UnlockAsync(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            Error = UsesRecoveryKey ? EncryptionText.EnterRecoveryKey : EncryptionText.EnterPassword;
            return;
        }
        var given = UsesRecoveryKey ? VaultSecret.RecoveryKey(secret) : VaultSecret.Password(secret);
        var remember = Remember;
        Error = null;
        IsBusy = true;
        try
        {
            AppState Unlock() => _vaults.Unlock(Vault, given, remember);
            if (await SettledAsync(await Task.Run(Unlock).ConfigureAwait(true), Unlock).ConfigureAwait(true) is not { } state)
            {
                return;
            }
            if (state.Locked is null && state.OpenError is null && state.Vault is not null)
            {
                _opened(state);
            }
            else
            {
                Error = state.OpenError ?? EncryptionText.NotUnlocked;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// « Ouvrir avec Windows Hello » : Windows demande le code PIN, le visage ou l'empreinte, puis le coffre s'ouvre. Si
    /// l'utilisateur renonce, l'écran reste, sans message ; un refus de Windows se dit.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task UnlockWithHelloAsync()
    {
        if (!OffersHello)
        {
            return;
        }
        Error = null;
        IsBusy = true;
        try
        {
            var state = await Task.Run(() => _vaults.UnlockWithHello(Vault)).ConfigureAwait(true);
            if (state.Locked is null && state.OpenError is null && state.Vault is not null)
            {
                _opened(state);
            }
            else
            {
                Error = state.OpenError;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>« Utiliser le mot de passe » : au lieu de Windows Hello, cette fois.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void TypePassword()
    {
        Error = null;
        OffersHello = false;
    }

    /// <summary>Le mot de passe est perdu : la clé de récupération est demandée.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void UseRecoveryKey()
    {
        UsesRecoveryKey = true;
        Error = null;
    }

    /// <summary>Retour au mot de passe.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void UsePassword()
    {
        UsesRecoveryKey = false;
        Error = null;
    }

    /// <summary>Ouvre un autre coffre, choisi par son dossier ; chiffré lui aussi, son propre écran le demande.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task OtherVaultAsync()
    {
        if (_picker.Pick(ShellText.ChooseVaultFolder) is not { } folder)
        {
            return;
        }
        Error = null;
        IsBusy = true;
        try
        {
            // L'ouverture lit tout le coffre : hors du fil de l'interface.
            AppState Open() => _vaults.Open(folder);
            if (await SettledAsync(await Task.Run(Open).ConfigureAwait(true), Open).ConfigureAwait(true) is not { } state)
            {
                return;
            }
            if (state.Locked is not null)
            {
                _locked(state);
            }
            else if (state.OpenError is null && state.Vault is not null)
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

    /// <summary>
    /// Quitte cet écran sans rien changer : pour le coffre resté ouvert, ou pour l'accueil. Rien pendant une ouverture,
    /// qui changerait de coffre juste après.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void Back() => _back?.Invoke();
}
