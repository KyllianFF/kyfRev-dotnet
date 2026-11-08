using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes du coffre chiffré, lus dans les ressources (<c>Resources/EncryptionText.resx</c>).</summary>
public static class EncryptionText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.EncryptionText", typeof(EncryptionText).Assembly);

    /// <summary>« Retour » : l'étape d'avant.</summary>
    public static string Back => Text(nameof(Back));

    /// <summary>« Revenir au coffre chiffré » : après un déchiffrement.</summary>
    public static string BackToEncrypted => Text(nameof(BackToEncrypted));

    /// <summary>« Revenir au coffre en clair » : après un chiffrement.</summary>
    public static string BackToPlain => Text(nameof(BackToPlain));

    /// <summary>« Revenir au coffre ouvert ».</summary>
    public static string BackToVault => Text(nameof(BackToVault));

    /// <summary>« Retour à l'accueil ».</summary>
    public static string BackToWelcome => Text(nameof(BackToWelcome));

    /// <summary>« Annuler » : la conversion n'a pas commencé, le coffre revient.</summary>
    public static string Cancel => Text(nameof(Cancel));

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

    /// <summary>« Vérification de la clé de récupération… » : la carte dérive la clé tapée.</summary>
    public static string CheckingRecoveryKey => Text(nameof(CheckingRecoveryKey));

    /// <summary>« Confirmez le nouveau mot de passe ».</summary>
    public static string ConfirmPassword => Text(nameof(ConfirmPassword));

    /// <summary>« Continuer » : l'étape suivante.</summary>
    public static string Continue => Text(nameof(Continue));

    /// <summary>Ce que fait la conversion, et ce qu'une coupure laisse.</summary>
    public static string ConversionHelp => Text(nameof(ConversionHelp));

    /// <summary>« Conversion du coffre » : le titre de sa page.</summary>
    public static string ConversionPage => Text(nameof(ConversionPage));

    /// <summary>« Étape 3 sur 3 : la conversion ».</summary>
    public static string ConversionStepTitle => Text(nameof(ConversionStepTitle));

    /// <summary>« 12 / 412 » : <paramref name="done"/> fichiers sur <paramref name="total"/>.</summary>
    /// <param name="done">Les fichiers faits.</param>
    /// <param name="total">Les fichiers à faire.</param>
    public static string CountOf(int done, int total) => Format("Count", done, total);

    /// <summary>« Conversion en cours… ».</summary>
    public static string Converting => Text(nameof(Converting));

    /// <summary>« Copier ».</summary>
    public static string Copy => Text(nameof(Copy));

    /// <summary>« Copier la clé de récupération » : le nom du bouton « Copier ».</summary>
    public static string CopyRecoveryKey => Text(nameof(CopyRecoveryKey));

    /// <summary>« Créer » : le bouton qui crée une nouvelle clé de récupération.</summary>
    public static string Create => Text(nameof(Create));

    /// <summary>« Mot de passe actuel ».</summary>
    public static string CurrentPassword => Text(nameof(CurrentPassword));

    /// <summary>Ce qu'il faut faire d'un coffre déchiffré.</summary>
    public static string DecryptedNext => Text(nameof(DecryptedNext));

    /// <summary>Le dossier où le coffre déchiffré s'écrira.</summary>
    public static string DecryptedParent => Text(nameof(DecryptedParent));

    /// <summary>« Déchiffrer ce coffre » : le titre de la page.</summary>
    public static string DecryptHeading => Text(nameof(DecryptHeading));

    /// <summary>Ce que fait un déchiffrement.</summary>
    public static string DecryptIntro => Text(nameof(DecryptIntro));

    /// <summary>« La conversion » : le titre de la progression d'un déchiffrement.</summary>
    public static string DecryptionTitle => Text(nameof(DecryptionTitle));

    /// <summary>« Déchiffrer le coffre » : le bouton qui commence le déchiffrement.</summary>
    public static string DecryptNow => Text(nameof(DecryptNow));

    /// <summary>« Déchiffrer vers un dossier… » : la carte Chiffrement.</summary>
    public static string DecryptVault => Text(nameof(DecryptVault));

    /// <summary>Ce que fait « Déchiffrer vers un dossier… ».</summary>
    public static string DecryptVaultHelp => Text(nameof(DecryptVaultHelp));

    /// <summary>Ce que protège encore un coffre déchiffré : rien.</summary>
    public static string DecryptWarning => Text(nameof(DecryptWarning));

    /// <summary>« Vérification du mot de passe… » : la dérivation prend de l'ordre d'une seconde.</summary>
    public static string Deriving => Text(nameof(Deriving));

    /// <summary>Ce qu'il faut faire après un chiffrement.</summary>
    public static string EncryptedNext => Text(nameof(EncryptedNext));

    /// <summary>« Chiffrer ce coffre » : le titre de la page.</summary>
    public static string EncryptHeading => Text(nameof(EncryptHeading));

    /// <summary>Ce que fait un chiffrement, et ce qu'il ne fait pas.</summary>
    public static string EncryptIntro => Text(nameof(EncryptIntro));

    /// <summary>« Chiffrer le coffre » : le bouton qui commence la conversion.</summary>
    public static string EncryptNow => Text(nameof(EncryptNow));

    /// <summary>« Chiffrer ce coffre… » : la carte du coffre, pour un coffre en clair.</summary>
    public static string EncryptVault => Text(nameof(EncryptVault));

    /// <summary>Ce que fait « Chiffrer ce coffre… ».</summary>
    public static string EncryptVaultHelp => Text(nameof(EncryptVaultHelp));

    /// <summary>« Saisissez le mot de passe actuel du coffre. ».</summary>
    public static string EnterCurrentPassword => Text(nameof(EnterCurrentPassword));

    /// <summary>« Saisissez le mot de passe du coffre. ».</summary>
    public static string EnterPassword => Text(nameof(EnterPassword));

    /// <summary>« Saisissez la clé de récupération du coffre. ».</summary>
    public static string EnterRecoveryKey => Text(nameof(EnterRecoveryKey));

    /// <summary>« Fichiers déchiffrés » : la première barre d'un déchiffrement.</summary>
    public static string FilesDecrypted => Text(nameof(FilesDecrypted));

    /// <summary>« Fichiers chiffrés » : la première barre d'un chiffrement.</summary>
    public static string FilesEncrypted => Text(nameof(FilesEncrypted));

    /// <summary>La seconde barre de la conversion.</summary>
    public static string FilesVerified => Text(nameof(FilesVerified));

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

    /// <summary>Ce qui manque pour commencer le chiffrement.</summary>
    public static string KeyStoredBeforeEncrypting => Text(nameof(KeyStoredBeforeEncrypting));

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

    /// <summary>Le coffre n'a pas pu être fermé pour la conversion.</summary>
    public static string NotClosed => Text(nameof(NotClosed));

    /// <summary>« Ce coffre n'a pas pu être ouvert. ».</summary>
    public static string NotUnlocked => Text(nameof(NotUnlocked));

    /// <summary>L'ancienne clé de récupération n'ouvre plus le coffre, mais encore ses anciennes sauvegardes.</summary>
    public static string OldRecoveryKey => Text(nameof(OldRecoveryKey));

    /// <summary>« Ouvrir le coffre en clair » : après un déchiffrement.</summary>
    public static string OpenDecrypted => Text(nameof(OpenDecrypted));

    /// <summary>« Ouvrir le coffre chiffré » : après un chiffrement.</summary>
    public static string OpenEncrypted => Text(nameof(OpenEncrypted));

    /// <summary>« Ouverture du coffre… » : après la conversion.</summary>
    public static string Opening => Text(nameof(Opening));

    /// <summary>« Ouvrir un autre coffre… ».</summary>
    public static string OtherVault => Text(nameof(OtherVault));

    /// <summary>« Mot de passe ».</summary>
    public static string Password => Text(nameof(Password));

    /// <summary>Le mot de passe est changé.</summary>
    public static string PasswordChanged => Text(nameof(PasswordChanged));

    /// <summary>« Au moins 12 caractères. ».</summary>
    public static string PasswordLengthOf(object count) => Format("PasswordLength", count);

    /// <summary>Un mot de passe changé juste avant un verrouillage : la lecture suivante du coffre le dit.</summary>
    public static string PasswordChangedWhileLocking => Text(nameof(PasswordChangedWhileLocking));

    /// <summary>Ce que fait le mot de passe d'un coffre chiffré.</summary>
    public static string PasswordStepHelp => Text(nameof(PasswordStepHelp));

    /// <summary>« Étape 1 sur 3 : le mot de passe ».</summary>
    public static string PasswordStepTitle => Text(nameof(PasswordStepTitle));

    /// <summary>Le titre du choix du dossier d'un coffre déchiffré.</summary>
    public static string PickDecryptedParent => Text(nameof(PickDecryptedParent));

    /// <summary>« Choisir un autre dossier… ».</summary>
    public static string PickOtherParent => Text(nameof(PickOtherParent));

    /// <summary>Comment recopier la clé de récupération.</summary>
    public static string RecoveryHelp => Text(nameof(RecoveryHelp));

    /// <summary>« Sans son mot de passe, sa clé de récupération l'ouvre. ».</summary>
    public static string RecoveryIntro => Text(nameof(RecoveryIntro));

    /// <summary>« Clé de récupération ».</summary>
    public static string RecoveryKey => Text(nameof(RecoveryKey));

    /// <summary>La nouvelle clé de récupération est créée.</summary>
    public static string RecoveryKeyCreated => Text(nameof(RecoveryKeyCreated));

    /// <summary>La nouvelle clé de récupération annulée : l'ancienne reste la bonne.</summary>
    public static string RecoveryKeyKept => Text(nameof(RecoveryKeyKept));

    /// <summary>Une nouvelle clé de récupération qu'un verrouillage a empêché d'enregistrer : la lecture suivante du coffre le dit.</summary>
    public static string RecoveryKeyNotSaved => Text(nameof(RecoveryKeyNotSaved));

    /// <summary>Une nouvelle clé de récupération enregistrée juste avant un verrouillage : la lecture suivante du coffre le dit.</summary>
    public static string RecoveryKeySavedWhileLocking => Text(nameof(RecoveryKeySavedWhileLocking));

    /// <summary>Que faire de la clé de récupération affichée : elle ne le sera plus.</summary>
    public static string RecoveryKeyShown => Text(nameof(RecoveryKeyShown));

    /// <summary>« Votre clé de récupération » : le titre de la fenêtre qui montre la nouvelle.</summary>
    public static string RecoveryKeyShownTitle => Text(nameof(RecoveryKeyShownTitle));

    /// <summary>« Étape 2 sur 3 : la clé de récupération ».</summary>
    public static string RecoveryStepTitle => Text(nameof(RecoveryStepTitle));

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

    /// <summary>« Enregistrer cette clé » : le bouton qui remplace l'ancienne clé de récupération par la nouvelle.</summary>
    public static string SaveRecoveryKey => Text(nameof(SaveRecoveryKey));

    /// <summary>« Enregistrement de la nouvelle clé de récupération… » : la carte écrit l'en-tête.</summary>
    public static string SavingRecoveryKey => Text(nameof(SavingRecoveryKey));

    /// <summary>« 3 · Conversion » : la troisième étape du chiffrement.</summary>
    public static string StepConversion => Text(nameof(StepConversion));

    /// <summary>« 1 · Mot de passe » : la première étape du chiffrement.</summary>
    public static string StepPassword => Text(nameof(StepPassword));

    /// <summary>« 2 · Clé de récupération » : la deuxième étape du chiffrement.</summary>
    public static string StepRecoveryKey => Text(nameof(StepRecoveryKey));

    /// <summary>« Ouvrir ».</summary>
    public static string Unlock => Text(nameof(Unlock));

    /// <summary>« Ouverture du coffre… ».</summary>
    public static string Unlocking => Text(nameof(Unlocking));

    /// <summary>« Ouvrir avec le mot de passe ».</summary>
    public static string UsePassword => Text(nameof(UsePassword));

    /// <summary>« J'utilise la clé de récupération » : une fenêtre de la carte la demande au lieu du mot de passe.</summary>
    public static string WithRecoveryKey => Text(nameof(WithRecoveryKey));

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);
}
