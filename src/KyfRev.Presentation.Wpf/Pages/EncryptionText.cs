using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes du coffre chiffré, lus dans les ressources (<c>Resources/EncryptionText.resx</c>).</summary>
public static class EncryptionText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.EncryptionText", typeof(EncryptionText).Assembly);

    /// <summary>« Revenir au coffre ouvert ».</summary>
    public static string BackToVault => Text(nameof(BackToVault));

    /// <summary>« Retour à l'accueil ».</summary>
    public static string BackToWelcome => Text(nameof(BackToWelcome));

    /// <summary>Ce que protège le chiffrement : le texte de la carte « Chiffrement ».</summary>
    public static string CardHelp => Text(nameof(CardHelp));

    /// <summary>« Chiffrement » : le titre de la carte des paramètres.</summary>
    public static string CardTitle => Text(nameof(CardTitle));

    /// <summary>« Changer » : le bouton qui change le mot de passe.</summary>
    public static string Change => Text(nameof(Change));

    /// <summary>« Changer le mot de passe… ».</summary>
    public static string ChangePassword => Text(nameof(ChangePassword));

    /// <summary>Ce qu'un nouveau mot de passe change, et ce qu'il ne change pas.</summary>
    public static string ChangePasswordHelp => Text(nameof(ChangePasswordHelp));

    /// <summary>« Changer le mot de passe » : le titre de sa fenêtre.</summary>
    public static string ChangePasswordTitle => Text(nameof(ChangePasswordTitle));

    /// <summary>« Fermer ».</summary>
    public static string Close => Text(nameof(Close));

    /// <summary>« Confirmez le nouveau mot de passe ».</summary>
    public static string ConfirmPassword => Text(nameof(ConfirmPassword));

    /// <summary>« Copier ».</summary>
    public static string Copy => Text(nameof(Copy));

    /// <summary>« Copier la clé de récupération » : le nom du bouton « Copier ».</summary>
    public static string CopyRecoveryKey => Text(nameof(CopyRecoveryKey));

    /// <summary>« Créer » : le bouton qui crée une nouvelle clé de récupération.</summary>
    public static string Create => Text(nameof(Create));

    /// <summary>« Mot de passe actuel ».</summary>
    public static string CurrentPassword => Text(nameof(CurrentPassword));

    /// <summary>« Vérification du mot de passe… » : la dérivation prend de l'ordre d'une seconde.</summary>
    public static string Deriving => Text(nameof(Deriving));

    /// <summary>« Saisissez le mot de passe actuel du coffre. ».</summary>
    public static string EnterCurrentPassword => Text(nameof(EnterCurrentPassword));

    /// <summary>« Saisissez le mot de passe du coffre. ».</summary>
    public static string EnterPassword => Text(nameof(EnterPassword));

    /// <summary>« Saisissez la clé de récupération du coffre. ».</summary>
    public static string EnterRecoveryKey => Text(nameof(EnterRecoveryKey));

    /// <summary>La clé n'est plus retenue sur ce poste.</summary>
    public static string Forgotten => Text(nameof(Forgotten));

    /// <summary>« 2 heures ».</summary>
    public static string HourManyOf(object count) => Format("HourMany", count);

    /// <summary>« 1 heure ».</summary>
    public static string HourOneOf(object count) => Format("HourOne", count);

    /// <summary>La clé de récupération est copiée.</summary>
    public static string KeyCopied => Text(nameof(KeyCopied));

    /// <summary>La clé de récupération n'a pas pu être copiée : elle se recopie à la main.</summary>
    public static string KeyNotCopied => Text(nameof(KeyNotCopied));

    /// <summary>« J'ai rangé ma clé de récupération ».</summary>
    public static string KeyStored => Text(nameof(KeyStored));

    /// <summary>La fenêtre de la clé ne se ferme qu'une fois la clé rangée.</summary>
    public static string KeyStoredFirst => Text(nameof(KeyStoredFirst));

    /// <summary>« Verrouillage automatique ».</summary>
    public static string LockAfter => Text(nameof(LockAfter));

    /// <summary>Quand le coffre se verrouille de lui-même.</summary>
    public static string LockAfterHelp => Text(nameof(LockAfterHelp));

    /// <summary>« Ce coffre est chiffré ».</summary>
    public static string LockedHeading => Text(nameof(LockedHeading));

    /// <summary>« Ses fiches ne se lisent qu'avec son mot de passe. ».</summary>
    public static string LockedIntro => Text(nameof(LockedIntro));

    /// <summary>« Coffre chiffré » : le nom de l'écran de déverrouillage.</summary>
    public static string LockedPage => Text(nameof(LockedPage));

    /// <summary>« Jamais » : aucun verrouillage après un délai sans rien toucher.</summary>
    public static string LockNever => Text(nameof(LockNever));

    /// <summary>« Verrouiller maintenant (Ctrl+Maj+L) ».</summary>
    public static string LockNow => Text(nameof(LockNow));

    /// <summary>« J'ai perdu mon mot de passe… ».</summary>
    public static string LostPassword => Text(nameof(LostPassword));

    /// <summary>« 15 minutes ».</summary>
    public static string MinuteManyOf(object count) => Format("MinuteMany", count);

    /// <summary>« 1 minute ».</summary>
    public static string MinuteOneOf(object count) => Format("MinuteOne", count);

    /// <summary>« Nouveau mot de passe ».</summary>
    public static string NewPassword => Text(nameof(NewPassword));

    /// <summary>« Créer une nouvelle clé de récupération… ».</summary>
    public static string NewRecoveryKey => Text(nameof(NewRecoveryKey));

    /// <summary>Ce que demande une nouvelle clé de récupération, et ce qu'elle remplace.</summary>
    public static string NewRecoveryKeyHelp => Text(nameof(NewRecoveryKeyHelp));

    /// <summary>« Nouvelle clé de récupération » : le titre de la fenêtre qui demande le mot de passe.</summary>
    public static string NewRecoveryKeyTitle => Text(nameof(NewRecoveryKeyTitle));

    /// <summary>« Ce coffre n'a pas pu être ouvert. ».</summary>
    public static string NotUnlocked => Text(nameof(NotUnlocked));

    /// <summary>L'ancienne clé de récupération n'ouvre plus le coffre, mais encore ses anciennes sauvegardes.</summary>
    public static string OldRecoveryKey => Text(nameof(OldRecoveryKey));

    /// <summary>« Ouvrir un autre coffre… ».</summary>
    public static string OtherVault => Text(nameof(OtherVault));

    /// <summary>« Mot de passe ».</summary>
    public static string Password => Text(nameof(Password));

    /// <summary>Le mot de passe est changé.</summary>
    public static string PasswordChanged => Text(nameof(PasswordChanged));

    /// <summary>« Au moins 12 caractères. ».</summary>
    public static string PasswordLengthOf(object count) => Format("PasswordLength", count);

    /// <summary>Comment recopier la clé de récupération.</summary>
    public static string RecoveryHelp => Text(nameof(RecoveryHelp));

    /// <summary>« Sans son mot de passe, sa clé de récupération l'ouvre. ».</summary>
    public static string RecoveryIntro => Text(nameof(RecoveryIntro));

    /// <summary>« Clé de récupération ».</summary>
    public static string RecoveryKey => Text(nameof(RecoveryKey));

    /// <summary>La nouvelle clé de récupération est créée.</summary>
    public static string RecoveryKeyCreated => Text(nameof(RecoveryKeyCreated));

    /// <summary>Que faire de la clé de récupération affichée : elle ne le sera plus.</summary>
    public static string RecoveryKeyShown => Text(nameof(RecoveryKeyShown));

    /// <summary>« Votre clé de récupération » : le titre de la fenêtre qui montre la nouvelle.</summary>
    public static string RecoveryKeyShownTitle => Text(nameof(RecoveryKeyShownTitle));

    /// <summary>Une nouvelle clé de récupération a été créée pendant le verrouillage, sans être montrée.</summary>
    public static string RecoveryKeyUnseen => Text(nameof(RecoveryKeyUnseen));

    /// <summary>« Se souvenir sur cet ordinateur ».</summary>
    public static string Remember => Text(nameof(Remember));

    /// <summary>Pourquoi le mot de passe est demandé pour retenir la clé.</summary>
    public static string RememberAsk => Text(nameof(RememberAsk));

    /// <summary>« Retenir » : le bouton qui retient la clé sur ce poste.</summary>
    public static string RememberButton => Text(nameof(RememberButton));

    /// <summary>La clé est retenue sur ce poste.</summary>
    public static string Remembered => Text(nameof(Remembered));

    /// <summary>Ce que retenir la clé permet, et ce qu'il ne protège plus.</summary>
    public static string RememberHelp => Text(nameof(RememberHelp));

    /// <summary>« Ouvrir ».</summary>
    public static string Unlock => Text(nameof(Unlock));

    /// <summary>« Ouverture du coffre… ».</summary>
    public static string Unlocking => Text(nameof(Unlocking));

    /// <summary>« Ouvrir avec le mot de passe ».</summary>
    public static string UsePassword => Text(nameof(UsePassword));

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);
}
