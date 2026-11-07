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
    }

    /// <summary>Le dossier du coffre chiffré.</summary>
    public string Vault { get; }

    /// <summary>Le nom du coffre : son dossier.</summary>
    public string VaultName => Path.GetFileName(Path.TrimEndingDirectorySeparator(Vault)) is { Length: > 0 } name ? name : Vault;

    /// <summary>Un coffre reste ouvert derrière cet écran : on peut y revenir.</summary>
    public bool CanGoBack => _back is not null;

    /// <summary>La clé de récupération est demandée à la place du mot de passe.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesPassword), nameof(Intro))]
    public partial bool UsesRecoveryKey { get; private set; }

    /// <summary>Le mot de passe est demandé.</summary>
    public bool UsesPassword => !UsesRecoveryKey;

    /// <summary>Ce qui ouvre le coffre, dit en une phrase.</summary>
    public string Intro => UsesRecoveryKey ? EncryptionText.RecoveryIntro : EncryptionText.LockedIntro;

    /// <summary>Retenir la clé sur ce poste ; décoché par défaut.</summary>
    [ObservableProperty]
    public partial bool Remember { get; set; }

    /// <summary>Une ouverture est en cours : la dérivation du mot de passe prend de l'ordre d'une seconde.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand), nameof(OtherVaultCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Rien n'est en cours.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Pourquoi le coffre ne s'est pas ouvert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Un message d'erreur est affiché.</summary>
    public bool HasError => Error is not null;

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
            var state = await Task.Run(() => _vaults.Unlock(Vault, given, remember)).ConfigureAwait(true);
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

    /// <summary>Le mot de passe est perdu : la clé de récupération est demandée.</summary>
    [RelayCommand]
    private void UseRecoveryKey()
    {
        UsesRecoveryKey = true;
        Error = null;
    }

    /// <summary>Retour au mot de passe.</summary>
    [RelayCommand]
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
            var state = await Task.Run(() => _vaults.Open(folder)).ConfigureAwait(true);
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

    /// <summary>Revient au coffre resté ouvert, sans rien changer.</summary>
    [RelayCommand]
    private void Back() => _back?.Invoke();
}
