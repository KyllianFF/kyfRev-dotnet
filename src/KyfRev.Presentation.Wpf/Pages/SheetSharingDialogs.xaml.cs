using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Les fenêtres « Partager » et « Enregistrer comme modèle », posées sur la page qui les demande.</summary>
internal sealed partial class SheetSharingDialogs : UserControl
{
    public SheetSharingDialogs()
    {
        InitializeComponent();
        // La page quittée perd souvent son contexte avant de se décharger : l'un ou l'autre ferme les fenêtres.
        Unloaded += (_, _) => (DataContext as SheetSharingViewModel)?.Dismiss();
        DataContextChanged += (_, e) => (e.OldValue as SheetSharingViewModel)?.Dismiss();
    }

    private void OnShareClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetSharingViewModel)?.AnswerShareCommand.Execute(e.Choice == DialogChoice.Primary);

    private void OnTemplateClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetSharingViewModel)?.AnswerTemplateCommand.Execute(e.Choice == DialogChoice.Primary);

    /// <summary>Un nom se saisit tout de suite : le champ prend le focus, son texte sélectionné, après l'ouverture du dialogue.</summary>
    private void OnTemplateNameShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
            {
                TemplateNameBox.Focus();
                TemplateNameBox.SelectAll();
            });
        }
    }
}
