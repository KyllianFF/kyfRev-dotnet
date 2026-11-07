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
/// <param name="current">Le mot de passe actuel.</param>
/// <param name="newPassword">Le nouveau mot de passe.</param>
/// <param name="confirmation">Le nouveau mot de passe, tapé une seconde fois.</param>
public sealed class PasswordChange(string current, string newPassword, string confirmation)
{
    /// <summary>Le mot de passe actuel.</summary>
    public string Current { get; } = current;

    /// <summary>Le nouveau mot de passe.</summary>
    public string NewPassword { get; } = newPassword;

    /// <summary>Le nouveau mot de passe, tapé une seconde fois.</summary>
    public string Confirmation { get; } = confirmation;
}

/// <summary>
/// La carte « Chiffrement » de l'onglet « Données », pour un coffre chiffré : retenir sa clé sur ce poste, le délai du
/// verrouillage automatique, le verrouillage tout de suite, un nouveau mot de passe et une nouvelle clé de récupération.
/// Les mots de passe passent des champs aux commandes sans être gardés, et chaque dérivation se fait hors du fil de
/// l'interface. La nouvelle clé de récupération ne se montre qu'une fois : sa fenêtre ne se ferme qu'une fois la clé
/// rangée, et la carte l'oublie alors.
/// </summary>
public sealed partial class EncryptionSettingsViewModel : ObservableObject, IForgetsVault
{
    private readonly EncryptionUseCases _encryption;
    private readonly SettingsUseCases _settings;
    private readonly Func<bool> _isEncrypted;
    private readonly Func<Task>? _lockNow;
    private readonly Action<string> _copy;

    /// <summary>Vrai pendant que la carte montre ce qu'elle a lu : rien n'est enregistré, rien n'est demandé.</summary>
    private bool _showing;

    /// <summary>Change à chaque oubli du coffre : une dérivation partie avant n'y remet rien.</summary>
    private int _generation;

    /// <summary>Une clé de récupération a été créée pendant un oubli, sans être montrée : la prochaine lecture le dit.</summary>
    private bool _unseenKey;

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

    /// <summary>Une dérivation est en cours : elle prend de l'ordre d'une seconde.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; private set; }

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

    /// <summary>La nouvelle clé de récupération est montrée, une seule fois.</summary>
    [ObservableProperty]
    public partial bool IsShowingRecoveryKey { get; private set; }

    /// <summary>Pourquoi la fenêtre ouverte a été rouverte : un mot de passe refusé, ou absent.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDialogError))]
    public partial string? DialogError { get; private set; }

    /// <summary>La fenêtre ouverte montre un refus.</summary>
    public bool HasDialogError => DialogError is not null;

    /// <summary>La nouvelle clé de récupération, tant que sa fenêtre est ouverte ; vide sinon.</summary>
    [ObservableProperty]
    public partial string RecoveryKey { get; private set; } = string.Empty;

    /// <summary>« J'ai rangé ma clé de récupération » : la fenêtre de la clé ne se ferme qu'une fois coché.</summary>
    [ObservableProperty]
    public partial bool IsKeyStored { get; set; }

    /// <summary>La copie de la clé, ou ce qui manque pour fermer sa fenêtre.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasKeyMessage))]
    public partial string? KeyMessage { get; private set; }

    /// <summary>La fenêtre de la clé a quelque chose à dire.</summary>
    public bool HasKeyMessage => KeyMessage is not null;

    /// <summary>
    /// Relit la carte : si le coffre ouvert est chiffré, si sa clé est retenue sur ce poste, et le délai du verrouillage
    /// automatique. Une fenêtre ouverte le reste ; la clé de récupération qu'elle montre aussi, tant qu'elle n'est pas rangée.
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
            if (_unseenKey)
            {
                _unseenKey = false;
                Error = EncryptionText.RecoveryKeyUnseen;
            }
            var preferences = _settings.Get();
            if (!preferences.Succeeded)
            {
                Error = preferences.Error;
                return;
            }
            var minutes = preferences.Value.LockAfterMinutes;
            LockAfterChoices = ChoicesWith(minutes);
            LockAfter = LockAfterChoices.First(choice => choice.Value == minutes);
        }
        finally
        {
            _showing = false;
        }
    }

    /// <summary>
    /// Oublie ce que la carte a lu du coffre quand il se verrouille : qu'il est chiffré, sa clé retenue, le délai, les
    /// fenêtres ouvertes, la clé de récupération montrée et les messages. Une dérivation partie avant n'y remet rien ; la
    /// carte se relit à la prochaine ouverture de l'onglet.
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
        DialogError = null;
        RecoveryKey = string.Empty;
        IsKeyStored = false;
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

    /// <summary>Le délai s'enregistre aussitôt choisi, sans rien changer d'autre.</summary>
    partial void OnLockAfterChanged(SettingChoice<int>? value)
    {
        if (_showing || value is null)
        {
            return;
        }
        var minutes = value.Value;
        var outcome = _settings.Update(preferences => preferences with { LockAfterMinutes = minutes });
        Error = outcome.Error;
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
            DialogError = null;
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
    /// La réponse de la fenêtre « Se souvenir sur cet ordinateur » : le mot de passe, ou rien si elle a été fermée sans
    /// (rien n'est alors retenu). Un refus la rouvre, avec sa raison.
    /// </summary>
    [RelayCommand]
    private async Task AnswerRememberAsync(string? password)
    {
        if (!IsAskingRemember)
        {
            return;
        }
        IsAskingRemember = false;
        if (password is null)
        {
            DialogError = null;
            ShowRemembered();
            return;
        }
        if (password.Length == 0)
        {
            DialogError = EncryptionText.EnterPassword;
            IsAskingRemember = true;
            return;
        }
        var generation = _generation;
        var outcome = await DeriveAsync(() => _encryption.Remember(VaultSecret.Password(password))).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant la dérivation : la carte n'y remet rien.
            return;
        }
        if (!outcome.Succeeded)
        {
            DialogError = outcome.Error;
            IsAskingRemember = true;
            return;
        }
        DialogError = null;
        Message = EncryptionText.Remembered;
        ShowRemembered();
    }

    /// <summary>« Changer le mot de passe… » : sa fenêtre s'ouvre.</summary>
    [RelayCommand]
    private void RequestChangePassword()
    {
        Error = null;
        Message = null;
        DialogError = null;
        IsChangingPassword = true;
    }

    /// <summary>
    /// La réponse de la fenêtre « Changer le mot de passe » : les trois saisies, ou rien si elle a été fermée sans. Un refus
    /// (mot de passe actuel faux, confirmation différente, nouveau trop court) la rouvre, avec sa raison.
    /// </summary>
    [RelayCommand]
    private async Task AnswerChangePasswordAsync(PasswordChange? change)
    {
        if (!IsChangingPassword)
        {
            return;
        }
        IsChangingPassword = false;
        if (change is null)
        {
            DialogError = null;
            return;
        }
        if (change.Current.Length == 0)
        {
            DialogError = EncryptionText.EnterCurrentPassword;
            IsChangingPassword = true;
            return;
        }
        var generation = _generation;
        var outcome = await DeriveAsync(() => _encryption.ChangePassword(VaultSecret.Password(change.Current), change.NewPassword, change.Confirmation)).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant la dérivation : ni la fenêtre ni le message ne reviennent.
            return;
        }
        if (!outcome.Succeeded)
        {
            DialogError = outcome.Error;
            IsChangingPassword = true;
            return;
        }
        DialogError = null;
        Message = EncryptionText.PasswordChanged;
    }

    /// <summary>« Créer une nouvelle clé de récupération… » : sa fenêtre demande le mot de passe.</summary>
    [RelayCommand]
    private void RequestNewRecoveryKey()
    {
        Error = null;
        Message = null;
        DialogError = null;
        IsAskingRecoveryKey = true;
    }

    /// <summary>
    /// La réponse de la fenêtre « Nouvelle clé de récupération » : le mot de passe, ou rien si elle a été fermée sans. Un
    /// refus la rouvre, avec sa raison ; accepté, la nouvelle clé est montrée, une seule fois.
    /// </summary>
    [RelayCommand]
    private async Task AnswerNewRecoveryKeyAsync(string? password)
    {
        if (!IsAskingRecoveryKey)
        {
            return;
        }
        IsAskingRecoveryKey = false;
        if (password is null)
        {
            DialogError = null;
            return;
        }
        if (password.Length == 0)
        {
            DialogError = EncryptionText.EnterPassword;
            IsAskingRecoveryKey = true;
            return;
        }
        var generation = _generation;
        var outcome = await DeriveAsync(() => _encryption.NewRecoveryKey(VaultSecret.Password(password))).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant la création : la clé ne se montre pas, mais la prochaine lecture dit qu'elle a changé.
            _unseenKey |= outcome.Succeeded;
            return;
        }
        if (!outcome.Succeeded)
        {
            DialogError = outcome.Error;
            IsAskingRecoveryKey = true;
            return;
        }
        DialogError = null;
        RecoveryKey = outcome.Value;
        IsKeyStored = false;
        KeyMessage = null;
        IsShowingRecoveryKey = true;
    }

    /// <summary>« Copier » : la clé va dans le presse-papiers ; s'il refuse, elle se recopie à la main.</summary>
    [RelayCommand]
    private void CopyRecoveryKey()
    {
        if (RecoveryKey.Length == 0)
        {
            return;
        }
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
    /// La fenêtre de la clé se ferme : seulement une fois « J'ai rangé ma clé de récupération » coché, sinon elle se rouvre
    /// et le dit. La clé est alors oubliée.
    /// </summary>
    [RelayCommand]
    private void CloseRecoveryKey()
    {
        if (!IsShowingRecoveryKey)
        {
            return;
        }
        IsShowingRecoveryKey = false;
        if (!IsKeyStored)
        {
            KeyMessage = EncryptionText.KeyStoredFirst;
            IsShowingRecoveryKey = true;
            return;
        }
        RecoveryKey = string.Empty;
        IsKeyStored = false;
        KeyMessage = null;
        Message = EncryptionText.RecoveryKeyCreated;
    }

    /// <summary>« Verrouiller maintenant (Ctrl+Maj+L) ».</summary>
    [RelayCommand(CanExecute = nameof(CanLockNow))]
    private Task LockNowAsync() => _lockNow is { } lockNow ? lockNow() : Task.CompletedTask;

    /// <summary>Une dérivation du mot de passe, hors du fil de l'interface.</summary>
    private async Task<Outcome<T>> DeriveAsync<T>(Func<Outcome<T>> derive)
    {
        IsBusy = true;
        Error = null;
        Message = null;
        try
        {
            return await Task.Run(derive).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Montre si la clé est retenue, sans rien demander ni oublier.</summary>
    private void ShowRemembered()
    {
        _showing = true;
        IsRemembered = IsEncrypted && _encryption.IsRemembered;
        _showing = false;
    }
}
