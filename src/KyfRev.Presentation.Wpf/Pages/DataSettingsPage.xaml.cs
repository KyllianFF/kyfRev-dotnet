using System.Windows.Controls;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>L'onglet « Données » de la vue « Paramètres ».</summary>
internal sealed partial class DataSettingsPage : UserControl
{
    public DataSettingsPage()
    {
        InitializeComponent();
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
}
