using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Vue de la conversion du coffre. Les mots de passe et la clé de récupération ne se lient pas : la vue les passe à la
/// commande, puis vide leurs champs, et ne les garde nulle part ; une étape qui se cache ne garde rien de ce qui y a été
/// tapé. La clé de récupération copiée, par le bouton, le clavier ou le menu, reste hors de l'historique du presse-papiers.
/// </summary>
internal sealed partial class ConversionPage : UserControl
{
    public ConversionPage()
    {
        InitializeComponent();
        // La vue peut servir à une autre conversion : rien de ce qui a été tapé ne reste.
        DataContextChanged += (_, _) => ClearSecrets();
        foreach (var box in new[] { RecoveryKeyText, DecryptRecoveryBox })
        {
            DataObject.AddCopyingHandler(box, OnSecretCopying);
        }
    }

    /// <summary>« Continuer » : le mot de passe, deux fois, part vers la page ; les champs se vident.</summary>
    private void OnPrepare(object sender, RoutedEventArgs e)
    {
        var password = new NewPassword(NewPasswordBox.Password, ConfirmPasswordBox.Password);
        ClearSecrets();
        if (DataContext is ConversionPageViewModel page)
        {
            page.PrepareCommand.Execute(password);
        }
    }

    /// <summary>« Déchiffrer » : le mot de passe, ou la clé de récupération, part vers la page ; le champ se vide.</summary>
    private void OnDecrypt(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ConversionPageViewModel page)
        {
            ClearSecrets();
            return;
        }
        var secret = page.UsesRecoveryKey ? DecryptRecoveryBox.Text : DecryptPasswordBox.Password;
        ClearSecrets();
        page.DecryptCommand.Execute(secret);
    }

    /// <summary>Une étape qui se cache ne garde rien de ce qui y a été tapé ; les champs se vident juste après.</summary>
    private void OnPartVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, ClearSecrets);
        }
    }

    /// <summary>Un champ de secret paraît, à son étape ou quand la case le fait paraître : il prend le focus.</summary>
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

    private void ClearSecrets()
    {
        NewPasswordBox.Clear();
        ConfirmPasswordBox.Clear();
        DecryptPasswordBox.Clear();
        DecryptRecoveryBox.Clear();
    }
}
