using System.Windows.Controls;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue d'une fiche ouverte pour la lire et la réviser.</summary>
internal sealed partial class SheetPage : UserControl
{
    public SheetPage()
    {
        InitializeComponent();
    }

    /// <summary>La réponse à la confirmation (révision en avance, J0, archivage) part vers la barre de révision.</summary>
    private void OnConfirmationClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetPageViewModel { Review: { } review })
        {
            review.AnswerCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }
}
