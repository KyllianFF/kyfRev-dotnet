using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La page des données de la vue « Paramètres », qui sert deux sections : « Coffre et chiffrement » et « Sauvegardes et
/// exports ». Les mots de passe et les clés de récupération de la carte « Chiffrement »
/// ne se lient pas : quand leur fenêtre se ferme, la vue les passe à la commande, puis vide leurs champs, et ne les garde
/// nulle part.
/// </summary>
internal sealed partial class DataSettingsPage : UserControl
{
    public DataSettingsPage()
    {
        InitializeComponent();
        // La vue peut servir à un autre onglet : rien de ce qui a été tapé ne reste.
        DataContextChanged += (_, _) => ClearPasswords();
        // Une clé de récupération copiée au clavier ou par le menu ne va pas plus loin que par « Copier ».
        foreach (var box in new[] { RecoveryKeyText, RememberRecoveryBox, CurrentRecoveryBox, PromptRecoveryBox })
        {
            DataObject.AddCopyingHandler(box, OnSecretCopying);
        }
    }

    /// <summary>La réponse à l'effacement part vers la vue.</summary>
    private void OnEraseClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is DataSettingsViewModel page)
        {
            page.AnswerEraseCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    /// <summary>La réponse à la confirmation part vers la vue.</summary>
    private void OnConfirmationClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is DataSettingsViewModel page)
        {
            page.AnswerRestoreCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    /// <summary>
    /// « Se souvenir sur cet ordinateur » : le mot de passe, ou la clé de récupération, part vers la carte ; rien si la
    /// fenêtre est fermée sans.
    /// </summary>
    private void OnRememberClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is not DataSettingsViewModel { Encryption: { } encryption })
        {
            ClearPasswords();
            return;
        }
        var secret = e.Choice == DialogChoice.Primary ? SecretOf(encryption, RememberPasswordBox, RememberRecoveryBox) : null;
        ClearPasswords();
        encryption.AnswerRememberCommand.Execute(secret);
    }

    /// <summary>« Changer le mot de passe » : les trois saisies partent vers la carte ; rien si la fenêtre est fermée sans.</summary>
    private void OnChangePasswordClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is not DataSettingsViewModel { Encryption: { } encryption })
        {
            ClearPasswords();
            return;
        }
        var change = e.Choice == DialogChoice.Primary
            ? new PasswordChange(SecretOf(encryption, CurrentPasswordBox, CurrentRecoveryBox), NewPasswordBox.Password, ConfirmPasswordBox.Password)
            : null;
        ClearPasswords();
        encryption.AnswerChangePasswordCommand.Execute(change);
    }

    /// <summary>
    /// « Nouvelle clé de récupération » : le mot de passe, ou la clé de récupération actuelle, part vers la carte ; rien si
    /// la fenêtre est fermée sans.
    /// </summary>
    private void OnRecoveryPromptClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is not DataSettingsViewModel { Encryption: { } encryption })
        {
            ClearPasswords();
            return;
        }
        var secret = e.Choice == DialogChoice.Primary ? SecretOf(encryption, RecoveryPasswordBox, PromptRecoveryBox) : null;
        ClearPasswords();
        encryption.AnswerNewRecoveryKeyCommand.Execute(secret);
    }

    /// <summary>Ce qu'a saisi la fenêtre : la clé de récupération si elle la demande, le mot de passe sinon.</summary>
    private static string SecretOf(EncryptionSettingsViewModel encryption, PasswordBox password, TextBox recoveryKey) =>
        encryption.UsesRecoveryKey ? recoveryKey.Text : password.Password;

    /// <summary>
    /// La fenêtre de la clé de récupération se ferme : « Enregistrer cette clé » l'enregistre, si elle est rangée (sinon la
    /// carte la rouvre) ; « Annuler » ou Échap la laissent de côté, et l'ancienne reste la bonne.
    /// </summary>
    private void OnRecoveryKeyClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is not DataSettingsViewModel { Encryption: { } encryption })
        {
            return;
        }
        if (e.Choice == DialogChoice.Primary)
        {
            encryption.SaveRecoveryKeyCommand.Execute(null);
        }
        else
        {
            encryption.CancelRecoveryKeyCommand.Execute(null);
        }
    }

    /// <summary>Un champ de secret paraît, à l'ouverture de sa fenêtre ou quand la case le fait paraître : il prend le focus.</summary>
    private void OnSecretShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            var field = (Control)sender;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (field.IsVisible)
                {
                    field.Focus();
                }
            });
        }
    }

    /// <summary>Ce qui est copié d'un champ de clé de récupération reste hors de l'historique du presse-papiers.</summary>
    private static void OnSecretCopying(object sender, DataObjectCopyingEventArgs e) => SecretClipboard.Protect(e.DataObject);

    /// <summary>
    /// Une fenêtre qui se cache sans réponse (le coffre verrouillé, l'onglet quitté) ne garde rien de ce qui y a été tapé.
    /// Les champs sont vidés juste après : la réponse d'une fenêtre fermée par ses boutons les a déjà lus.
    /// </summary>
    private void OnPasswordDialogVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, ClearPasswords);
        }
    }

    private void ClearPasswords()
    {
        RememberPasswordBox.Clear();
        CurrentPasswordBox.Clear();
        NewPasswordBox.Clear();
        ConfirmPasswordBox.Clear();
        RecoveryPasswordBox.Clear();
        RememberRecoveryBox.Clear();
        CurrentRecoveryBox.Clear();
        PromptRecoveryBox.Clear();
    }
}
