using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>La fenêtre des actions sur une fiche, et ce qui a abouti : dans la fenêtre principale, au-dessus de toutes les vues.</summary>
internal sealed partial class SheetActionsView : UserControl
{
    public SheetActionsView()
    {
        InitializeComponent();
    }

    /// <summary>La réponse à la fenêtre part vers les actions.</summary>
    private void OnActionClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetActionsViewModel actions)
        {
            actions.AnswerActionCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    /// <summary>Le message s'en va : seul, ou d'un clic.</summary>
    private void OnDoneClosed(object sender, RoutedEventArgs e) => (DataContext as SheetActionsViewModel)?.DismissDoneCommand.Execute(null);

    /// <summary>Une étiquette proposée est prise ; la saisie reprend à sa suite.</summary>
    private void OnTagSuggestionClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string tag } && DataContext is SheetActionsViewModel actions)
        {
            actions.TakeTagCommand.Execute(tag);
            ActionInputBox.Focus();
            ActionInputBox.CaretIndex = ActionInputBox.Text.Length;
        }
    }

    /// <summary>Un nom se saisit tout de suite : le champ prend le focus, son texte choisi, une fois la fenêtre ouverte.</summary>
    private void OnActionInputShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
            {
                ActionInputBox.Focus();
                ActionInputBox.SelectAll();
            });
        }
    }
}
