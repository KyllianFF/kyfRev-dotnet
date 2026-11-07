using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// L'onglet « Données » de la vue « Paramètres ». Les mots de passe de la carte « Chiffrement » ne se lient pas : quand leur
/// fenêtre se ferme, la vue les passe à la commande, puis vide leurs champs, et ne les garde nulle part.
/// </summary>
internal sealed partial class DataSettingsPage : UserControl
{
    public DataSettingsPage()
    {
        InitializeComponent();
        // La vue peut servir à un autre onglet : rien de ce qui a été tapé ne reste.
        DataContextChanged += (_, _) => ClearPasswords();
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

    /// <summary>« Se souvenir sur cet ordinateur » : le mot de passe part vers la carte ; rien si la fenêtre est fermée sans.</summary>
    private void OnRememberClosed(object sender, DialogClosedEventArgs e)
    {
        var password = e.Choice == DialogChoice.Primary ? RememberPasswordBox.Password : null;
        ClearPasswords();
        if (DataContext is DataSettingsViewModel { Encryption: { } encryption })
        {
            encryption.AnswerRememberCommand.Execute(password);
        }
    }

    /// <summary>« Changer le mot de passe » : les trois saisies partent vers la carte ; rien si la fenêtre est fermée sans.</summary>
    private void OnChangePasswordClosed(object sender, DialogClosedEventArgs e)
    {
        var change = e.Choice == DialogChoice.Primary ? new PasswordChange(CurrentPasswordBox.Password, NewPasswordBox.Password, ConfirmPasswordBox.Password) : null;
        ClearPasswords();
        if (DataContext is DataSettingsViewModel { Encryption: { } encryption })
        {
            encryption.AnswerChangePasswordCommand.Execute(change);
        }
    }

    /// <summary>« Nouvelle clé de récupération » : le mot de passe part vers la carte ; rien si la fenêtre est fermée sans.</summary>
    private void OnRecoveryPromptClosed(object sender, DialogClosedEventArgs e)
    {
        var password = e.Choice == DialogChoice.Primary ? RecoveryPasswordBox.Password : null;
        ClearPasswords();
        if (DataContext is DataSettingsViewModel { Encryption: { } encryption })
        {
            encryption.AnswerNewRecoveryKeyCommand.Execute(password);
        }
    }

    /// <summary>La fenêtre de la clé de récupération se ferme, si la clé est rangée ; sinon la carte la rouvre.</summary>
    private void OnRecoveryKeyClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is DataSettingsViewModel { Encryption: { } encryption })
        {
            encryption.CloseRecoveryKeyCommand.Execute(null);
        }
    }

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
    }
}
