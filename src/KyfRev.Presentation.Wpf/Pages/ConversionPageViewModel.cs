using System.IO;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Le sens d'une conversion.</summary>
public enum ConversionDirection
{
    /// <summary>« Chiffrer ce coffre » : un coffre en clair devient un coffre chiffré, dans un nouveau dossier voisin.</summary>
    Encrypt,

    /// <summary>« Déchiffrer vers un dossier… » : un coffre chiffré redevient un coffre en clair, dans un nouveau dossier.</summary>
    Decrypt,
}

/// <summary>L'étape d'une conversion, telle que la page la montre.</summary>
public enum ConversionStage
{
    /// <summary>Le mot de passe du futur coffre chiffré, tapé deux fois.</summary>
    Password,

    /// <summary>La clé de récupération du futur coffre chiffré, montrée une seule fois.</summary>
    RecoveryKey,

    /// <summary>Le secret du coffre chiffré à déchiffrer, et le dossier où écrire le coffre en clair.</summary>
    Secret,

    /// <summary>La conversion tourne : les fichiers s'écrivent, puis se relisent.</summary>
    Converting,

    /// <summary>La conversion a réussi : le nouveau coffre peut s'ouvrir, ou l'ancien se rouvrir.</summary>
    Done,

    /// <summary>La conversion a refusé ou échoué : rien n'a été écrit, l'ancien coffre se rouvre.</summary>
    Failed,
}

/// <summary>Le mot de passe d'un futur coffre chiffré, tel que sa page le passe : rien de lui ne s'affiche ni ne se garde.</summary>
/// <param name="password">Le mot de passe.</param>
/// <param name="confirmation">Le mot de passe, tapé une seconde fois.</param>
public sealed class NewPassword(string password, string confirmation)
{
    /// <summary>Le mot de passe.</summary>
    public string Password { get; } = password;

    /// <summary>Le mot de passe, tapé une seconde fois.</summary>
    public string Confirmation { get; } = confirmation;
}

/// <summary>
/// La conversion du coffre ouvert, sur une page à elle, hors de l'espace de travail : « Chiffrer ce coffre », en trois
/// étapes (le mot de passe, la clé de récupération, la conversion), ou « Déchiffrer vers un dossier… ». Le coffre reste
/// ouvert derrière la page tant que la conversion n'a pas commencé : « Annuler » y revient. Elle commence quand il est
/// fermé (ce qui s'écrit s'enregistre, et les vues oublient, comme à un verrouillage), pour que rien ne s'y écrive pendant
/// la copie ; la source reste intacte, et la copie s'écrit toujours dans un nouveau dossier, chaque fichier relu et
/// comparé. Ensuite, aucun coffre n'est ouvert : la page ouvre le nouveau, ou rouvre l'ancien. Un coffre chiffré s'ouvre
/// avec son mot de passe, sur l'écran de déverrouillage.
/// </summary>
public sealed partial class ConversionPageViewModel : ObservableObject
{
    private readonly VaultUseCases _vaults;
    private readonly EncryptionUseCases _encryption;
    private readonly Func<object, Task<bool>> _close;
    private readonly Action<AppState> _opened;
    private readonly Action _cancel;
    private readonly IFolderPicker _picker;
    private readonly Action<string> _copy;

    /// <summary>Le chiffrement préparé, tant que sa clé de récupération n'est pas rangée ; rien sinon.</summary>
    private EncryptionDraft? _draft;

    /// <summary>La page d'une conversion du coffre ouvert, <paramref name="source"/>.</summary>
    /// <param name="vaults">Ouverture, fermeture et conversion des coffres.</param>
    /// <param name="encryption">Les clés du coffre chiffré ouvert : son secret se vérifie avant qu'il ne se ferme.</param>
    /// <param name="direction">Chiffrer, ou déchiffrer.</param>
    /// <param name="source">Le coffre ouvert, à convertir.</param>
    /// <param name="close">Ferme le coffre pour la page qu'on lui donne, affichée (<see cref="VaultLocking.CloseForAsync"/>) ; faux s'il reste ouvert.</param>
    /// <param name="opened">Ce qui suit l'ouverture d'un coffre : l'espace de travail, ou l'écran de déverrouillage.</param>
    /// <param name="cancel">Le retour au coffre ouvert, la conversion annulée avant d'avoir commencé.</param>
    /// <param name="picker">Le choix du dossier où écrire un coffre déchiffré.</param>
    /// <param name="copy">Copie la clé de récupération dans le presse-papiers ; peut lever <see cref="ExternalException"/>.</param>
    public ConversionPageViewModel(
        VaultUseCases vaults,
        EncryptionUseCases encryption,
        ConversionDirection direction,
        string source,
        Func<object, Task<bool>> close,
        Action<AppState> opened,
        Action cancel,
        IFolderPicker picker,
        Action<string> copy)
    {
        ArgumentNullException.ThrowIfNull(vaults);
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentNullException.ThrowIfNull(close);
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(cancel);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(copy);
        _vaults = vaults;
        _encryption = encryption;
        _close = close;
        _opened = opened;
        _cancel = cancel;
        _picker = picker;
        _copy = copy;
        Direction = direction;
        Source = source;
        Parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(source)) ?? source;
        Stage = direction == ConversionDirection.Encrypt ? ConversionStage.Password : ConversionStage.Secret;
    }

    /// <summary>« Au moins 12 caractères. » : ce que demande le mot de passe d'un coffre chiffré.</summary>
    public static string PasswordLength => EncryptionText.PasswordLengthOf(VaultSecret.MinimumPasswordLength);

    /// <summary>Chiffrer, ou déchiffrer.</summary>
    public ConversionDirection Direction { get; }

    /// <summary>Le coffre à convertir ; il reste intact.</summary>
    public string Source { get; }

    /// <summary>« Chiffrer ce coffre », ou « Déchiffrer ce coffre ».</summary>
    public string Heading => Direction == ConversionDirection.Encrypt ? EncryptionText.EncryptHeading : EncryptionText.DecryptHeading;

    /// <summary>Ce que fait la conversion, et ce qu'elle ne fait pas.</summary>
    public string Intro => Direction == ConversionDirection.Encrypt ? EncryptionText.EncryptIntro : EncryptionText.DecryptIntro;

    /// <summary>« Étape 3 sur 3 : la conversion », ou, pour un déchiffrement, « La conversion ».</summary>
    public string ConversionTitle => Direction == ConversionDirection.Encrypt ? EncryptionText.ConversionStepTitle : EncryptionText.DecryptionTitle;

    /// <summary>Le chiffrement a des étapes numérotées : le mot de passe, la clé de récupération, la conversion.</summary>
    public bool ShowsSteps => Direction == ConversionDirection.Encrypt;

    /// <summary>L'étape montrée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPassword), nameof(IsRecoveryKey), nameof(IsSecret), nameof(IsConverting), nameof(IsDone), nameof(IsFailed), nameof(IsFinished), nameof(CanCancel))]
    [NotifyPropertyChangedFor(nameof(IsStepOne), nameof(IsStepTwo), nameof(IsStepThree))]
    public partial ConversionStage Stage { get; private set; }

    /// <summary>L'étape du mot de passe.</summary>
    public bool IsPassword => Stage == ConversionStage.Password;

    /// <summary>L'étape de la clé de récupération.</summary>
    public bool IsRecoveryKey => Stage == ConversionStage.RecoveryKey;

    /// <summary>L'étape du secret et du dossier, pour un déchiffrement.</summary>
    public bool IsSecret => Stage == ConversionStage.Secret;

    /// <summary>La conversion tourne.</summary>
    public bool IsConverting => Stage == ConversionStage.Converting;

    /// <summary>La conversion a réussi.</summary>
    public bool IsDone => Stage == ConversionStage.Done;

    /// <summary>La conversion a refusé ou échoué.</summary>
    public bool IsFailed => Stage == ConversionStage.Failed;

    /// <summary>La conversion est finie, réussie ou non : aucun coffre n'est ouvert.</summary>
    public bool IsFinished => IsDone || IsFailed;

    /// <summary>La conversion n'a pas commencé : le coffre est encore ouvert derrière la page.</summary>
    public bool CanCancel => Stage is ConversionStage.Password or ConversionStage.RecoveryKey or ConversionStage.Secret;

    /// <summary>L'étape 1 est l'étape montrée.</summary>
    public bool IsStepOne => IsPassword;

    /// <summary>L'étape 2 est l'étape montrée.</summary>
    public bool IsStepTwo => IsRecoveryKey;

    /// <summary>L'étape 3 est l'étape montrée, ou finie.</summary>
    public bool IsStepThree => IsConverting || IsFinished;

    /// <summary>Le dossier où le coffre déchiffré s'écrira : à côté du coffre chiffré, ou ailleurs.</summary>
    [ObservableProperty]
    public partial string Parent { get; private set; }

    /// <summary>« J'utilise la clé de récupération » : le déchiffrement la demande au lieu du mot de passe.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesPassword), nameof(SecretLabel))]
    public partial bool UsesRecoveryKey { get; set; }

    /// <summary>Le déchiffrement demande le mot de passe.</summary>
    public bool UsesPassword => !UsesRecoveryKey;

    /// <summary>« Mot de passe », ou « Clé de récupération ».</summary>
    public string SecretLabel => UsesRecoveryKey ? EncryptionText.RecoveryKey : EncryptionText.Password;

    /// <summary>La clé de récupération du futur coffre chiffré, à l'étape 2 ; vide sinon.</summary>
    [ObservableProperty]
    public partial string RecoveryKey { get; private set; } = string.Empty;

    /// <summary>« J'ai rangé ma clé de récupération » : la conversion ne commence qu'une fois coché.</summary>
    [ObservableProperty]
    public partial bool IsKeyStored { get; set; }

    /// <summary>La copie de la clé, ou ce qui manque pour continuer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasKeyMessage))]
    public partial string? KeyMessage { get; private set; }

    /// <summary>L'étape de la clé a quelque chose à dire.</summary>
    public bool HasKeyMessage => KeyMessage is not null;

    /// <summary>Une dérivation, une conversion ou une ouverture est en cours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Rien n'est en cours : la page répond.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Ce qui est en cours, tant que la page l'est.</summary>
    [ObservableProperty]
    public partial string BusyMessage { get; private set; } = EncryptionText.Deriving;

    /// <summary>Refus de la dernière action de la page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Une action a été refusée.</summary>
    public bool HasError => Error is not null;

    /// <summary>Ce que la conversion a fait, une fois finie.</summary>
    [ObservableProperty]
    public partial string Result { get; private set; } = string.Empty;

    /// <summary>Le nouveau coffre, une fois écrit et vérifié ; rien sinon.</summary>
    [ObservableProperty]
    public partial string? Destination { get; private set; }

    /// <summary>« Fiches chiffrées » ou « Fichiers déchiffrés » : les fichiers écrits.</summary>
    public string WritingLabel => Direction == ConversionDirection.Encrypt ? EncryptionText.FilesEncrypted : EncryptionText.FilesDecrypted;

    /// <summary>Les fichiers écrits.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WritingCount))]
    public partial int Written { get; private set; }

    /// <summary>Les fichiers relus et comparés.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerifyingCount))]
    public partial int Verified { get; private set; }

    /// <summary>Les fichiers à convertir ; 0 tant que la conversion ne les a pas comptés.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WritingCount), nameof(VerifyingCount), nameof(Maximum))]
    public partial int Total { get; private set; }

    /// <summary>Le bout des barres de progression : au moins 1, pour une barre vide plutôt qu'indéfinie.</summary>
    public int Maximum => Math.Max(Total, 1);

    /// <summary>« 12 / 412 » : les fichiers écrits.</summary>
    public string WritingCount => EncryptionText.CountOf(Written, Total);

    /// <summary>« 12 / 412 » : les fichiers relus.</summary>
    public string VerifyingCount => EncryptionText.CountOf(Verified, Total);

    /// <summary>« Ouvrir le coffre chiffré », ou « Ouvrir le coffre en clair ».</summary>
    public string OpenNewLabel => Direction == ConversionDirection.Encrypt ? EncryptionText.OpenEncrypted : EncryptionText.OpenDecrypted;

    /// <summary>« Revenir au coffre en clair », ou « Revenir au coffre chiffré ».</summary>
    public string ReturnLabel => Direction == ConversionDirection.Encrypt ? EncryptionText.BackToPlain : EncryptionText.BackToEncrypted;

    /// <summary>
    /// Étape 1 : prépare le chiffrement, sans rien écrire, avec le mot de passe tapé deux fois ; la clé de récupération du
    /// futur coffre se montre ensuite. Un refus (deux saisies différentes, trop court) le dit, et l'étape reste.
    /// </summary>
    [RelayCommand]
    private async Task PrepareAsync(NewPassword? password)
    {
        if (!IsPassword || IsBusy || password is null)
        {
            return;
        }
        Error = null;
        var outcome = await RunAsync(EncryptionText.Deriving, () => _vaults.PrepareEncryption(password.Password, password.Confirmation)).ConfigureAwait(true);
        if (!IsPassword)
        {
            // La page a quitté cette étape pendant la dérivation : rien ne se montre, et la clé préparée s'efface.
            if (outcome.Succeeded)
            {
                outcome.Value.Dispose();
            }
            return;
        }
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            return;
        }
        _draft = outcome.Value;
        RecoveryKey = outcome.Value.RecoveryKey;
        IsKeyStored = false;
        KeyMessage = null;
        Stage = ConversionStage.RecoveryKey;
    }

    /// <summary>« Copier » : la clé va dans le presse-papiers ; s'il refuse, elle se recopie à la main.</summary>
    [RelayCommand]
    private void CopyRecoveryKey()
    {
        if (!IsRecoveryKey)
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

    /// <summary>« Retour » : de la clé au mot de passe ; la clé préparée est oubliée, et une autre viendra.</summary>
    [RelayCommand]
    private void BackToPassword()
    {
        if (!IsRecoveryKey || IsBusy)
        {
            return;
        }
        DropDraft();
        Error = null;
        Stage = ConversionStage.Password;
    }

    /// <summary>
    /// Étape 3 : une fois la clé rangée, le coffre se ferme, puis se chiffre dans un nouveau dossier voisin, chaque fichier
    /// relu et comparé. Sans la case cochée, l'étape reste et le dit ; si ce qui s'écrit n'a pas pu s'enregistrer, le coffre
    /// reste ouvert et la page le dit.
    /// </summary>
    [RelayCommand]
    private async Task EncryptAsync()
    {
        if (!IsRecoveryKey || IsBusy || _draft is not { } draft)
        {
            return;
        }
        KeyMessage = null;
        if (!IsKeyStored)
        {
            KeyMessage = EncryptionText.KeyStoredBeforeEncrypting;
            return;
        }
        if (!await CloseAsync().ConfigureAwait(true))
        {
            return;
        }
        _draft = null;
        RecoveryKey = string.Empty;
        try
        {
            await ConvertAsync(progress => _vaults.Encrypt(draft, progress)).ConfigureAwait(true);
        }
        finally
        {
            draft.Dispose();
        }
    }

    /// <summary>« Choisir un autre dossier… » : où écrire le coffre déchiffré.</summary>
    [RelayCommand]
    private void PickParent()
    {
        if (!IsSecret || IsBusy)
        {
            return;
        }
        if (_picker.Pick(EncryptionText.PickDecryptedParent) is { } folder)
        {
            Parent = folder;
        }
    }

    /// <summary>
    /// Déchiffre : le secret est d'abord vérifié, coffre ouvert ; puis le coffre se ferme et se déchiffre dans un nouveau
    /// dossier de <see cref="Parent"/>, chaque fichier relu et comparé. Un secret refusé le dit, et l'étape reste.
    /// </summary>
    [RelayCommand]
    private async Task DecryptAsync(string? secret)
    {
        if (!IsSecret || IsBusy || secret is null)
        {
            return;
        }
        Error = null;
        if (secret.Length == 0)
        {
            Error = UsesRecoveryKey ? EncryptionText.EnterRecoveryKey : EncryptionText.EnterPassword;
            return;
        }
        var given = UsesRecoveryKey ? VaultSecret.RecoveryKey(secret) : VaultSecret.Password(secret);
        var checking = await RunAsync(UsesRecoveryKey ? EncryptionText.CheckingRecoveryKey : EncryptionText.Deriving, () => _encryption.Check(given)).ConfigureAwait(true);
        if (!IsSecret)
        {
            return;
        }
        if (!checking.Succeeded)
        {
            Error = checking.Error;
            return;
        }
        if (!await CloseAsync().ConfigureAwait(true))
        {
            return;
        }
        var parent = Parent;
        await ConvertAsync(progress => _vaults.Decrypt(Source, parent, given, progress)).ConfigureAwait(true);
    }

    /// <summary>« Annuler » : la conversion n'a pas commencé ; le coffre, resté ouvert, revient.</summary>
    [RelayCommand]
    private void Cancel()
    {
        if (!CanCancel || IsBusy)
        {
            return;
        }
        DropDraft();
        _cancel();
    }

    /// <summary>
    /// « Ouvrir le coffre chiffré » (ou en clair) : le nouveau coffre s'ouvre ; chiffré, l'écran de déverrouillage demande
    /// son mot de passe.
    /// </summary>
    [RelayCommand]
    private Task OpenNewAsync() => IsDone && Destination is { } destination ? OpenAsync(destination) : Task.CompletedTask;

    /// <summary>« Revenir au coffre en clair » (ou chiffré) : l'ancien coffre, intact, se rouvre.</summary>
    [RelayCommand]
    private Task ReturnAsync() => IsFinished ? OpenAsync(Source) : Task.CompletedTask;

    /// <summary>Ferme le coffre pour cette page ; faux, et la page le dit, s'il reste ouvert.</summary>
    private async Task<bool> CloseAsync()
    {
        Error = null;
        if (await _close(this).ConfigureAwait(true))
        {
            return true;
        }
        Error = EncryptionText.NotClosed;
        return false;
    }

    /// <summary>La conversion, hors du fil de l'interface, avec sa progression ; puis ce qu'elle a fait.</summary>
    private async Task ConvertAsync(Func<IProgress<ConversionProgress>, ConversionReport> convert)
    {
        Stage = ConversionStage.Converting;
        Written = 0;
        Verified = 0;
        Total = 0;
        var progress = new Progress<ConversionProgress>(Show);
        var report = await RunAsync(EncryptionText.Converting, () => Outcome.Of(convert(progress))).ConfigureAwait(true);
        Destination = report.Value.Succeeded ? report.Value.Destination : null;
        if (report.Value.Succeeded)
        {
            Result = report.Value.Message + " " + (Direction == ConversionDirection.Encrypt ? EncryptionText.EncryptedNext : EncryptionText.DecryptedNext);
            Stage = ConversionStage.Done;
        }
        else
        {
            Result = report.Value.Message;
            Stage = ConversionStage.Failed;
        }
    }

    /// <summary>Montre où en est la conversion.</summary>
    private void Show(ConversionProgress progress)
    {
        Total = progress.Total;
        if (progress.Step == ConversionStep.Writing)
        {
            Written = progress.Done;
        }
        else
        {
            Written = progress.Total;
            Verified = progress.Done;
        }
    }

    /// <summary>Ouvre <paramref name="folder"/> ; ce qui suit est l'espace de travail, ou l'écran de déverrouillage.</summary>
    private async Task OpenAsync(string folder)
    {
        if (IsBusy)
        {
            return;
        }
        Error = null;
        var outcome = await RunAsync(EncryptionText.Opening, () => Outcome.Of(_vaults.Open(folder))).ConfigureAwait(true);
        var state = outcome.Value;
        if (state.Locked is null && state.OpenError is { } error)
        {
            Error = error;
            return;
        }
        _opened(state);
    }

    /// <summary>Le travail <paramref name="work"/>, hors du fil de l'interface.</summary>
    private async Task<Outcome<T>> RunAsync<T>(string busy, Func<Outcome<T>> work)
    {
        BusyMessage = busy;
        IsBusy = true;
        try
        {
            return await Task.Run(work).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Le chiffrement préparé est oublié, sa clé effacée : rien n'a changé.</summary>
    private void DropDraft()
    {
        _draft?.Dispose();
        _draft = null;
        RecoveryKey = string.Empty;
        IsKeyStored = false;
        KeyMessage = null;
    }
}
