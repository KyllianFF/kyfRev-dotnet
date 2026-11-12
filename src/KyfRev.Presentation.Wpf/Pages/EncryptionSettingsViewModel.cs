using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Un changement de mot de passe, tel que sa fenêtre le passe à la carte : rien de lui ne s'affiche ni ne se garde.</summary>
/// <param name="current">Le mot de passe actuel, ou la clé de récupération (<see cref="EncryptionSettingsViewModel.UsesRecoveryKey"/>).</param>
/// <param name="newPassword">Le nouveau mot de passe.</param>
/// <param name="confirmation">Le nouveau mot de passe, tapé une seconde fois.</param>
public sealed class PasswordChange(string current, string newPassword, string confirmation)
{
    /// <summary>Le mot de passe actuel, ou la clé de récupération.</summary>
    public string Current { get; } = current;

    /// <summary>Le nouveau mot de passe.</summary>
    public string NewPassword { get; } = newPassword;

    /// <summary>Le nouveau mot de passe, tapé une seconde fois.</summary>
    public string Confirmation { get; } = confirmation;
}

/// <summary>
/// La carte « Chiffrement » de la section « Coffre et chiffrement », pour un coffre chiffré : retenir sa clé sur ce poste, le délai du
/// verrouillage automatique, le verrouillage tout de suite, un nouveau mot de passe et une nouvelle clé de récupération.
/// Chaque fenêtre accepte le mot de passe ou, s'il est perdu, la clé de récupération. Les secrets passent des champs aux
/// commandes sans être gardés, et chaque dérivation ou écriture se fait hors du fil de l'interface.
/// <para>
/// La nouvelle clé de récupération est d'abord préparée, sans rien écrire, et montrée une seule fois. Elle ne remplace
/// l'ancienne qu'une fois « J'ai rangé ma clé de récupération » coché et la fenêtre fermée par « Enregistrer cette clé » :
/// annulée, interrompue par un verrouillage ou par la fermeture de l'application, elle ne change rien. Ce qu'un verrouillage
/// a interrompu après coup (un mot de passe changé, une clé en cours d'enregistrement) se dit à la prochaine lecture de la
/// carte pour ce même coffre.
/// </para>
/// </summary>
public sealed partial class EncryptionSettingsViewModel : ObservableObject, IForgetsVault
{
    private readonly EncryptionUseCases _encryption;
    private readonly SettingsUseCases _settings;
    private readonly Func<bool> _isEncrypted;
    private readonly Func<Task>? _lockNow;
    private readonly Action<string> _copy;

    /// <summary>Les refus qu'un verrouillage a empêché de dire, par coffre : sa prochaine lecture les dit.</summary>
    private readonly Dictionary<string, string> _pendingErrors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Les résultats qu'un verrouillage a empêché de dire, par coffre : sa prochaine lecture les dit.</summary>
    private readonly Dictionary<string, string> _pendingMessages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Vrai pendant que la carte montre ce qu'elle a lu : rien n'est enregistré, rien n'est demandé.</summary>
    private bool _showing;

    /// <summary>Change à chaque oubli du coffre : une dérivation ou une écriture partie avant n'y remet rien.</summary>
    private int _generation;

    /// <summary>La clé de récupération préparée et montrée, pas encore enregistrée ; rien sinon.</summary>
    private RecoveryKeyDraft? _draft;

    /// <summary>La carte, sur le coffre ouvert.</summary>
    /// <param name="encryption">Les clés du coffre ouvert.</param>
    /// <param name="settings">Ses réglages : le délai du verrouillage automatique.</param>
    /// <param name="isEncrypted">Dit si le coffre ouvert est chiffré : la carte ne se montre que pour lui.</param>
    /// <param name="lockNow">Verrouille le coffre ; rien : le bouton n'est pas proposé.</param>
    /// <param name="copy">Copie un texte dans le presse-papiers ; peut lever <see cref="ExternalException"/>.</param>
    public EncryptionSettingsViewModel(EncryptionUseCases encryption, SettingsUseCases settings, Func<bool> isEncrypted, Func<Task>? lockNow, Action<string> copy)
    {
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(isEncrypted);
        ArgumentNullException.ThrowIfNull(copy);
        _encryption = encryption;
        _settings = settings;
        _isEncrypted = isEncrypted;
        _lockNow = lockNow;
        _copy = copy;
    }

    /// <summary>« Au moins 12 caractères. » : ce que demande un nouveau mot de passe.</summary>
    public static string PasswordLength => EncryptionText.PasswordLengthOf(VaultSecret.MinimumPasswordLength);

    /// <summary>
    /// Le délai que suit le verrouillage automatique, tenu à jour de ce que la carte lit et enregistre ; rien : la carte ne
    /// le tient pas.
    /// </summary>
    public LockDelay? LockDelay { get; init; }

    /// <summary>Le coffre ouvert est chiffré : la carte se montre.</summary>
    [ObservableProperty]
    public partial bool IsEncrypted { get; private set; }

    /// <summary>
    /// La clé du coffre est retenue sur ce poste. Cochée, la fenêtre demande le mot de passe, qui la retient ; décochée, la
    /// clé est oubliée aussitôt.
    /// </summary>
    [ObservableProperty]
    public partial bool IsRemembered { get; set; }

    /// <summary>Les délais du verrouillage automatique, dont celui en vigueur s'il n'est pas l'un d'eux.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SettingChoice<int>> LockAfterChoices { get; private set; } = [];

    /// <summary>Le délai du verrouillage automatique, enregistré aussitôt choisi ; rien tant que la carte n'a rien lu.</summary>
    [ObservableProperty]
    public partial SettingChoice<int>? LockAfter { get; set; }

    /// <summary>Le coffre peut être verrouillé d'ici.</summary>
    public bool CanLockNow => _lockNow is not null;

    /// <summary>« Déchiffrer vers un dossier… » : la page de la conversion s'ouvre ; rien : la carte ne le propose pas.</summary>
    public Action? DecryptionAsked { get; init; }

    /// <summary>Le coffre peut être déchiffré d'ici.</summary>
    public bool CanDecrypt => DecryptionAsked is not null;

    /// <summary>Une dérivation ou une écriture est en cours : elle prend de l'ordre d'une seconde.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Ce qui est en cours, tant que la carte l'est : la vérification du mot de passe, ou l'enregistrement de la clé.</summary>
    [ObservableProperty]
    public partial string BusyMessage { get; private set; } = EncryptionText.Deriving;

    /// <summary>Rien n'est en cours : la carte répond.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Refus de la dernière action de la carte.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Une action a été refusée.</summary>
    public bool HasError => Error is not null;

    /// <summary>Résultat de la dernière action de la carte.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    /// <summary>Une action vient d'aboutir.</summary>
    public bool HasMessage => Message is not null;

    /// <summary>La fenêtre « Se souvenir sur cet ordinateur » demande le mot de passe.</summary>
    [ObservableProperty]
    public partial bool IsAskingRemember { get; private set; }

    /// <summary>La fenêtre « Changer le mot de passe » est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsChangingPassword { get; private set; }

    /// <summary>La fenêtre « Nouvelle clé de récupération » demande le mot de passe.</summary>
    [ObservableProperty]
    public partial bool IsAskingRecoveryKey { get; private set; }

    /// <summary>
    /// « J'utilise la clé de récupération » : la fenêtre ouverte demande la clé de récupération au lieu du mot de passe.
    /// Décochée à chaque fenêtre ouverte.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesPassword), nameof(SecretLabel), nameof(CurrentSecretLabel))]
    public partial bool UsesRecoveryKey { get; set; }

    /// <summary>La fenêtre ouverte demande le mot de passe.</summary>
    public bool UsesPassword => !UsesRecoveryKey;

    /// <summary>« Mot de passe », ou « Clé de récupération » : le champ du secret d'une fenêtre.</summary>
    public string SecretLabel => UsesRecoveryKey ? EncryptionText.RecoveryKey : EncryptionText.Password;

    /// <summary>« Mot de passe actuel », ou « Clé de récupération » : le premier champ de « Changer le mot de passe ».</summary>
    public string CurrentSecretLabel => UsesRecoveryKey ? EncryptionText.RecoveryKey : EncryptionText.CurrentPassword;

    /// <summary>La nouvelle clé de récupération, préparée, est montrée une seule fois.</summary>
    [ObservableProperty]
    public partial bool IsShowingRecoveryKey { get; private set; }

    /// <summary>Pourquoi la fenêtre ouverte a été rouverte : un secret refusé, ou absent.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDialogError))]
    public partial string? DialogError { get; private set; }

    /// <summary>La fenêtre ouverte montre un refus.</summary>
    public bool HasDialogError => DialogError is not null;

    /// <summary>La nouvelle clé de récupération, tant que sa fenêtre est ouverte ; vide sinon.</summary>
    [ObservableProperty]
    public partial string RecoveryKey { get; private set; } = string.Empty;

    /// <summary>« J'ai rangé ma clé de récupération » : la clé ne s'enregistre qu'une fois coché.</summary>
    [ObservableProperty]
    public partial bool IsKeyStored { get; set; }

    /// <summary>La copie de la clé, ou ce qui manque pour l'enregistrer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasKeyMessage))]
    public partial string? KeyMessage { get; private set; }

    /// <summary>La fenêtre de la clé a quelque chose à dire.</summary>
    public bool HasKeyMessage => KeyMessage is not null;

    /// <summary>
    /// Relit la carte : si le coffre ouvert est chiffré, si sa clé est retenue sur ce poste, et le délai du verrouillage
    /// automatique. Une fenêtre ouverte le reste ; la clé de récupération qu'elle montre aussi, tant qu'elle n'est ni
    /// enregistrée ni annulée. Ce qu'un verrouillage a empêché de dire sur ce coffre se dit, une fois.
    /// </summary>
    public void Reload()
    {
        Error = null;
        Message = null;
        _showing = true;
        try
        {
            IsEncrypted = _isEncrypted();
            IsRemembered = IsEncrypted && _encryption.IsRemembered;
            if (!IsEncrypted)
            {
                LockAfter = null;
                LockAfterChoices = [];
                return;
            }
            var preferences = _settings.Get();
            if (!preferences.Succeeded)
            {
                // Ce qui attend d'être dit le sera à une lecture qui aboutit.
                Error = preferences.Error;
                return;
            }
            if (_pendingErrors.Count + _pendingMessages.Count > 0 && _encryption.Vault is { } vault)
            {
                Error = Take(_pendingErrors, vault);
                Message = Take(_pendingMessages, vault);
            }
            ShowLockAfter(preferences.Value.LockAfterMinutes);
        }
        finally
        {
            _showing = false;
        }
    }

    /// <summary>
    /// Oublie ce que la carte a lu du coffre quand il se verrouille, dès que le verrouillage commence : qu'il est chiffré,
    /// sa clé retenue, le délai, les fenêtres ouvertes, la clé de récupération préparée (qui n'a rien changé) et les
    /// messages. Une dérivation ou une écriture partie avant n'y remet rien ; la carte se relit à la prochaine ouverture de
    /// l'onglet.
    /// </summary>
    public void Forget()
    {
        _generation++;
        _showing = true;
        IsEncrypted = false;
        IsRemembered = false;
        LockAfter = null;
        LockAfterChoices = [];
        _showing = false;
        IsAskingRemember = false;
        IsChangingPassword = false;
        IsAskingRecoveryKey = false;
        IsShowingRecoveryKey = false;
        UsesRecoveryKey = false;
        DialogError = null;
        DropDraft();
        KeyMessage = null;
        Error = null;
        Message = null;
    }

    /// <summary>Les délais proposés, dont <paramref name="current"/> s'il n'en est pas un (réglé ailleurs), à sa place ; « Jamais » en dernier.</summary>
    internal static IReadOnlyList<SettingChoice<int>> ChoicesWith(int current)
    {
        var minutes = VaultPreferences.LockAfterChoices.ToList();
        if (!minutes.Contains(current))
        {
            var place = minutes.FindIndex(choice => choice == 0 || choice > current);
            minutes.Insert(place < 0 ? minutes.Count : place, current);
        }
        return [.. minutes.Select(choice => new SettingChoice<int>(choice, LabelOf(choice)))];
    }

    /// <summary>« 5 minutes », « 1 heure », ou « Jamais » pour 0.</summary>
    internal static string LabelOf(int minutes) => minutes switch
    {
        0 => EncryptionText.LockNever,
        _ when minutes % 60 == 0 => Dates.Plural(minutes / 60, EncryptionText.HourOneOf, EncryptionText.HourManyOf),
        _ => Dates.Plural(minutes, EncryptionText.MinuteOneOf, EncryptionText.MinuteManyOf),
    };

    /// <summary>
    /// Le délai s'enregistre aussitôt choisi, sans rien changer d'autre, et le verrouillage automatique le suit. Refusé, la
    /// liste remontre le délai enregistré, qui reste en vigueur, et le refus se dit, même s'il est le même que le précédent.
    /// </summary>
    partial void OnLockAfterChanged(SettingChoice<int>? value)
    {
        if (_showing || value is null)
        {
            return;
        }
        Error = null;
        var minutes = value.Value;
        var outcome = _settings.Update(preferences => preferences with { LockAfterMinutes = minutes });
        if (outcome.Succeeded)
        {
            LockDelay?.Set(outcome.Value.LockAfterMinutes);
            return;
        }
        var error = outcome.Error;
        _showing = true;
        try
        {
            if (_settings.Get() is { Succeeded: true, Value: var stored })
            {
                ShowLockAfter(stored.LockAfterMinutes);
            }
        }
        finally
        {
            _showing = false;
        }
        Error = error;
    }

    /// <summary>Cochée, la fenêtre demande le mot de passe ; décochée, la clé retenue est oubliée aussitôt.</summary>
    partial void OnIsRememberedChanged(bool value)
    {
        if (_showing)
        {
            return;
        }
        Error = null;
        Message = null;
        if (value)
        {
            OpenDialog();
            IsAskingRemember = true;
            return;
        }
        var outcome = _encryption.Forget();
        if (outcome.Succeeded)
        {
            Message = EncryptionText.Forgotten;
        }
        else
        {
            Error = outcome.Error;
        }
        ShowRemembered();
    }

    /// <summary>
    /// La réponse de la fenêtre « Se souvenir sur cet ordinateur » : le mot de passe ou la clé de récupération, ou rien si
    /// elle a été fermée sans (rien n'est alors retenu). Un refus la rouvre, avec sa raison.
    /// </summary>
    [RelayCommand]
    private async Task AnswerRememberAsync(string? secret)
    {
        if (!IsAskingRemember)
        {
            return;
        }
        IsAskingRemember = false;
        DialogError = null;
        if (secret is null)
        {
            ShowRemembered();
            return;
        }
        if (secret.Length == 0)
        {
            Reopen(() => IsAskingRemember = true, EnterSecret);
            return;
        }
        var given = SecretOf(secret);
        var generation = _generation;
        var outcome = await RunAsync(Checking, () => _encryption.Remember(given)).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant la dérivation : la carte n'y remet rien.
            return;
        }
        if (!outcome.Succeeded)
        {
            Reopen(() => IsAskingRemember = true, outcome.Error);
            return;
        }
        Message = EncryptionText.Remembered;
        ShowRemembered();
    }

    /// <summary>« Changer le mot de passe… » : sa fenêtre s'ouvre.</summary>
    [RelayCommand]
    private void RequestChangePassword()
    {
        Error = null;
        Message = null;
        OpenDialog();
        IsChangingPassword = true;
    }

    /// <summary>
    /// La réponse de la fenêtre « Changer le mot de passe » : les trois saisies, ou rien si elle a été fermée sans. Un refus
    /// (secret actuel faux, confirmation différente, nouveau trop court) la rouvre, avec sa raison. Un changement qui aboutit
    /// une fois le verrouillage commencé se dit à la prochaine lecture de ce coffre.
    /// </summary>
    [RelayCommand]
    private async Task AnswerChangePasswordAsync(PasswordChange? change)
    {
        if (!IsChangingPassword)
        {
            return;
        }
        IsChangingPassword = false;
        DialogError = null;
        if (change is null)
        {
            return;
        }
        if (change.Current.Length == 0)
        {
            Reopen(() => IsChangingPassword = true, UsesRecoveryKey ? EncryptionText.EnterRecoveryKey : EncryptionText.EnterCurrentPassword);
            return;
        }
        var current = SecretOf(change.Current);
        var generation = _generation;
        string? vault = null;
        var outcome = await RunAsync(Checking, () =>
        {
            // Lu hors du fil de l'interface : une longue opération peut tenir le coffre.
            vault = _encryption.Vault;
            return _encryption.ChangePassword(current, change.NewPassword, change.Confirmation);
        }).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le verrouillage a commencé pendant la dérivation : ni la fenêtre ni le message ne reviennent, mais un mot de
            // passe changé se dit à la prochaine lecture de ce coffre.
            if (outcome.Succeeded && vault is not null)
            {
                _pendingMessages[vault] = EncryptionText.PasswordChangedWhileLocking;
            }
            return;
        }
        if (!outcome.Succeeded)
        {
            Reopen(() => IsChangingPassword = true, outcome.Error);
            return;
        }
        Message = EncryptionText.PasswordChanged;
    }

    /// <summary>« Créer une nouvelle clé de récupération… » : sa fenêtre demande le mot de passe.</summary>
    [RelayCommand]
    private void RequestNewRecoveryKey()
    {
        Error = null;
        Message = null;
        OpenDialog();
        IsAskingRecoveryKey = true;
    }

    /// <summary>
    /// La réponse de la fenêtre « Nouvelle clé de récupération » : le mot de passe ou la clé de récupération actuelle, ou
    /// rien si elle a été fermée sans. Un refus la rouvre, avec sa raison ; accepté, la nouvelle clé est préparée, sans rien
    /// écrire, et montrée une seule fois. Préparée une fois le verrouillage commencé, elle est abandonnée : rien n'a changé.
    /// </summary>
    [RelayCommand]
    private async Task AnswerNewRecoveryKeyAsync(string? secret)
    {
        if (!IsAskingRecoveryKey)
        {
            return;
        }
        IsAskingRecoveryKey = false;
        DialogError = null;
        if (secret is null)
        {
            return;
        }
        if (secret.Length == 0)
        {
            Reopen(() => IsAskingRecoveryKey = true, EnterSecret);
            return;
        }
        var given = SecretOf(secret);
        var generation = _generation;
        var outcome = await RunAsync(Checking, () => _encryption.DraftRecoveryKey(given)).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant la préparation : rien n'a été écrit, l'ancienne clé reste la bonne.
            return;
        }
        if (!outcome.Succeeded)
        {
            Reopen(() => IsAskingRecoveryKey = true, outcome.Error);
            return;
        }
        _draft = outcome.Value;
        RecoveryKey = outcome.Value.Key;
        IsKeyStored = false;
        KeyMessage = null;
        IsShowingRecoveryKey = true;
    }

    /// <summary>
    /// « Copier » : la clé va dans le presse-papiers ; s'il refuse, elle se recopie à la main. Le message se redit à chaque
    /// copie.
    /// </summary>
    [RelayCommand]
    private void CopyRecoveryKey()
    {
        if (RecoveryKey.Length == 0)
        {
            return;
        }
        KeyMessage = null;
        try
        {
            _copy(RecoveryKey);
            KeyMessage = EncryptionText.KeyCopied;
        }
        catch (ExternalException)
        {
            KeyMessage = EncryptionText.KeyNotCopied;
        }
    }

    /// <summary>
    /// « Enregistrer cette clé » : seulement une fois « J'ai rangé ma clé de récupération » coché, sinon la fenêtre se
    /// rouvre et le dit. La clé est alors écrite dans l'en-tête du coffre, et l'ancienne ne l'ouvre plus ; la carte
    /// l'oublie. Un refus le dit, et l'ancienne reste valable. Une écriture qui finit une fois le verrouillage commencé se
    /// dit à la prochaine lecture de ce coffre.
    /// </summary>
    [RelayCommand]
    private async Task SaveRecoveryKeyAsync()
    {
        if (!IsShowingRecoveryKey || _draft is not { } draft)
        {
            return;
        }
        IsShowingRecoveryKey = false;
        KeyMessage = null;
        if (!IsKeyStored)
        {
            IsShowingRecoveryKey = true;
            KeyMessage = EncryptionText.KeyStoredFirst;
            return;
        }
        DropDraft();
        var generation = _generation;
        var outcome = await RunAsync(EncryptionText.SavingRecoveryKey, () => _encryption.SaveRecoveryKey(draft)).ConfigureAwait(true);
        if (generation != _generation)
        {
            if (outcome.Succeeded)
            {
                _pendingMessages[draft.Root] = EncryptionText.RecoveryKeySavedWhileLocking;
            }
            else
            {
                _pendingErrors[draft.Root] = EncryptionText.RecoveryKeyNotSaved;
            }
            return;
        }
        if (outcome.Succeeded)
        {
            Message = EncryptionText.RecoveryKeyCreated;
        }
        else
        {
            Error = outcome.Error;
        }
    }

    /// <summary>« Annuler », ou la fenêtre de la clé fermée sans l'enregistrer : la clé préparée est oubliée, rien n'a changé.</summary>
    [RelayCommand]
    private void CancelRecoveryKey()
    {
        if (!IsShowingRecoveryKey)
        {
            return;
        }
        IsShowingRecoveryKey = false;
        KeyMessage = null;
        DropDraft();
        Message = EncryptionText.RecoveryKeyKept;
    }

    /// <summary>« Déchiffrer vers un dossier… » : la page de la conversion remplace les vues, le coffre restant ouvert derrière.</summary>
    [RelayCommand(CanExecute = nameof(CanDecrypt))]
    private void RequestDecryption()
    {
        if (IsEncrypted && IsIdle)
        {
            DecryptionAsked?.Invoke();
        }
    }

    /// <summary>« Verrouiller maintenant (Ctrl+Maj+L) ».</summary>
    [RelayCommand(CanExecute = nameof(CanLockNow))]
    private Task LockNowAsync() => _lockNow is { } lockNow ? lockNow() : Task.CompletedTask;

    /// <summary>« Vérification du mot de passe… », ou de la clé de récupération : ce que fait la dérivation.</summary>
    private string Checking => UsesRecoveryKey ? EncryptionText.CheckingRecoveryKey : EncryptionText.Deriving;

    /// <summary>« Saisissez le mot de passe du coffre. », ou sa clé de récupération.</summary>
    private string EnterSecret => UsesRecoveryKey ? EncryptionText.EnterRecoveryKey : EncryptionText.EnterPassword;

    /// <summary>Le secret saisi : le mot de passe, ou la clé de récupération si la fenêtre la demande.</summary>
    private VaultSecret SecretOf(string text) => UsesRecoveryKey ? VaultSecret.RecoveryKey(text) : VaultSecret.Password(text);

    /// <summary>Le texte en attente pour <paramref name="vault"/>, une seule fois ; rien s'il n'y en a pas.</summary>
    private static string? Take(Dictionary<string, string> pending, string vault) =>
        pending.Remove(vault, out var text) ? text : null;

    /// <summary>Une fenêtre s'ouvre : sans refus, et sur le mot de passe.</summary>
    private void OpenDialog()
    {
        DialogError = null;
        UsesRecoveryKey = false;
    }

    /// <summary>
    /// Rouvre la fenêtre, puis y montre <paramref name="error"/> : le refus apparaît dans une fenêtre affichée, et se redit
    /// même s'il est le même que le précédent.
    /// </summary>
    private void Reopen(Action open, string? error)
    {
        open();
        DialogError = error;
    }

    /// <summary>La clé préparée est oubliée, sans avoir rien changé.</summary>
    private void DropDraft()
    {
        _draft = null;
        RecoveryKey = string.Empty;
        IsKeyStored = false;
    }

    /// <summary>Une dérivation du mot de passe, ou une écriture de l'en-tête, hors du fil de l'interface.</summary>
    private async Task<Outcome<T>> RunAsync<T>(string busy, Func<Outcome<T>> work)
    {
        BusyMessage = busy;
        IsBusy = true;
        Error = null;
        Message = null;
        try
        {
            return await Task.Run(work).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Montre le délai enregistré, parmi les choix, et le donne au verrouillage automatique.</summary>
    private void ShowLockAfter(int minutes)
    {
        LockAfterChoices = ChoicesWith(minutes);
        LockAfter = LockAfterChoices.First(choice => choice.Value == minutes);
        LockDelay?.Set(minutes);
    }

    /// <summary>Montre si la clé est retenue, sans rien demander ni oublier.</summary>
    private void ShowRemembered()
    {
        _showing = true;
        IsRemembered = IsEncrypted && _encryption.IsRemembered;
        _showing = false;
    }
}
