using System.Windows.Controls;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue d'un passage ou d'une définition à réviser : la carte mémoire.</summary>
internal sealed partial class PassagePage : UserControl
{
    public PassagePage()
    {
        InitializeComponent();
    }

    /// <summary>La réponse à la confirmation (révision en avance, J0, archivage) part vers la barre de révision.</summary>
    private void OnConfirmationClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is PassagePageViewModel { Review: { } review })
        {
            review.AnswerCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }
}
