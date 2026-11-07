using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Vue du déverrouillage. Le mot de passe ne se lie pas : la vue le passe à la commande au moment d'ouvrir, puis vide son
/// champ, et ne le garde nulle part.
/// </summary>
internal sealed partial class UnlockPage : UserControl
{
    public UnlockPage()
    {
        InitializeComponent();
    }

    /// <summary>Ouvre le coffre avec le mot de passe ou la clé de récupération saisis.</summary>
    private void OnUnlock(object sender, RoutedEventArgs e)
    {
        if (DataContext is not UnlockPageViewModel page)
        {
            return;
        }
        var secret = page.UsesRecoveryKey ? RecoveryBox.Text : PasswordBox.Password;
        PasswordBox.Clear();
        page.UnlockCommand.Execute(secret);
    }

    /// <summary>Le champ qui apparaît prend le focus : on y tape tout de suite.</summary>
    private void OnSecretShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true)
        {
            return;
        }
        Control field = ReferenceEquals(sender, RecoveryPart) ? RecoveryBox : PasswordBox;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => field.Focus());
    }
}
