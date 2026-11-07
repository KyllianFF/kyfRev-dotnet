using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes du coffre chiffré, lus dans les ressources (<c>Resources/EncryptionText.resx</c>).</summary>
public static class EncryptionText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.EncryptionText", typeof(EncryptionText).Assembly);

    /// <summary>« Revenir au coffre ouvert ».</summary>
    public static string BackToVault => Text(nameof(BackToVault));

    /// <summary>« Saisissez le mot de passe du coffre. ».</summary>
    public static string EnterPassword => Text(nameof(EnterPassword));

    /// <summary>« Saisissez la clé de récupération du coffre. ».</summary>
    public static string EnterRecoveryKey => Text(nameof(EnterRecoveryKey));

    /// <summary>« Ce coffre est chiffré ».</summary>
    public static string LockedHeading => Text(nameof(LockedHeading));

    /// <summary>« Ses fiches ne se lisent qu'avec son mot de passe. ».</summary>
    public static string LockedIntro => Text(nameof(LockedIntro));

    /// <summary>« Coffre chiffré » : le nom de l'écran de déverrouillage.</summary>
    public static string LockedPage => Text(nameof(LockedPage));

    /// <summary>« J'ai perdu mon mot de passe… ».</summary>
    public static string LostPassword => Text(nameof(LostPassword));

    /// <summary>« Ce coffre n'a pas pu être ouvert. ».</summary>
    public static string NotUnlocked => Text(nameof(NotUnlocked));

    /// <summary>« Ouvrir un autre coffre… ».</summary>
    public static string OtherVault => Text(nameof(OtherVault));

    /// <summary>« Mot de passe ».</summary>
    public static string Password => Text(nameof(Password));

    /// <summary>Comment recopier la clé de récupération.</summary>
    public static string RecoveryHelp => Text(nameof(RecoveryHelp));

    /// <summary>« Sans son mot de passe, sa clé de récupération l'ouvre. ».</summary>
    public static string RecoveryIntro => Text(nameof(RecoveryIntro));

    /// <summary>« Clé de récupération ».</summary>
    public static string RecoveryKey => Text(nameof(RecoveryKey));

    /// <summary>« Se souvenir sur cet ordinateur ».</summary>
    public static string Remember => Text(nameof(Remember));

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
}
