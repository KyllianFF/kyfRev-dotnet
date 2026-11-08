using System.Windows.Controls;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>La fenêtre « Importer un dossier de notes », posée sur la vue « Fiches ».</summary>
internal sealed partial class FolderImportDialog : UserControl
{
    public FolderImportDialog()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as FolderImportViewModel)?.Dismiss();
    }

    /// <summary>La réponse à la fenêtre part vers sa vue.</summary>
    private void OnClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as FolderImportViewModel)?.AnswerCommand.Execute(e.Choice == DialogChoice.Primary);
}
