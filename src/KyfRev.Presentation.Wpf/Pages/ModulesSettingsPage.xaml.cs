using System.Windows.Controls;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>L'onglet « Modules et extensions » de la vue « Paramètres ».</summary>
internal sealed partial class ModulesSettingsPage : UserControl
{
    public ModulesSettingsPage()
    {
        InitializeComponent();
    }

    private void OnInstallClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is ModulesSettingsViewModel page)
        {
            page.Extensions.AnswerInstallCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    private void OnRemoveClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is ModulesSettingsViewModel page)
        {
            page.Extensions.AnswerRemoveCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }
}
