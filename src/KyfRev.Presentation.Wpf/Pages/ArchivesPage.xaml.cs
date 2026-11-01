using System.Windows.Controls;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue « Archives ».</summary>
internal sealed partial class ArchivesPage : UserControl
{
    public ArchivesPage()
    {
        InitializeComponent();
        // La disposition des lignes suit la largeur de la page. La page quittée n'a plus de contexte.
        SizeChanged += (_, e) =>
        {
            if (DataContext is ArchivesPageViewModel page)
            {
                page.PageWidth = e.NewSize.Width;
            }
        };
    }

    /// <summary>La réponse à la confirmation part vers la vue.</summary>
    private void OnConfirmationClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is ArchivesPageViewModel page)
        {
            page.AnswerRestartCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }
}
