using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue de la charge des 30 prochains jours.</summary>
internal sealed partial class WorkloadPage : UserControl
{
    public WorkloadPage()
    {
        InitializeComponent();
        // Un clic sur une colonne : la liste le traite d'abord pour la choisir.
        Chart.AddHandler(MouseUpEvent, new MouseButtonEventHandler(OnColumnReleased), handledEventsToo: true);
    }

    private WorkloadPageViewModel Page => (WorkloadPageViewModel)DataContext;

    /// <summary>La largeur des colonnes décide d'une rangée ou de deux.</summary>
    private void OnChartSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // La page qu'on quitte perd son contexte avant de disparaître.
        if (DataContext is WorkloadPageViewModel page)
        {
            page.ChartWidth = e.NewSize.Width;
        }
    }

    /// <summary>Un clic sur le jour déjà choisi le désélectionne, comme dans la 0.x.</summary>
    private void OnColumnPressed(object sender, MouseButtonEventArgs e)
    {
        // La page quittée n'a plus de modèle : un clic déjà en route n'y fait rien.
        if (DataContext is WorkloadPageViewModel && ContainerOf(e.OriginalSource as DependencyObject) is { IsSelected: true })
        {
            Page.SelectedDay = null;
            e.Handled = true;
        }
    }

    /// <summary>Le détail du jour choisi se montre, même s'il est sous la ligne de flottaison à l'étroit.</summary>
    private void OnColumnReleased(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            ShowDetail();
        }
    }

    /// <summary>Entrée choisit la colonne qui a le focus (la tabulation y arrive sans la choisir), puis amène son détail.</summary>
    private void OnColumnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (ContainerOf(e.OriginalSource as DependencyObject) is { IsSelected: false } column)
            {
                column.SetCurrentValue(ListBoxItem.IsSelectedProperty, true);
            }
            ShowDetail();
            e.Handled = true;
        }
    }

    /// <summary>Le champ de date prend le focus dès qu'il paraît, et le rend quand le formulaire se referme.</summary>
    private void OnMoveFormVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => MoveDay.Focus());
        }
        else if (DataContext is WorkloadPageViewModel page)
        {
            _ = GiveFocusBackAsync(page);
        }
    }

    /// <summary>Un déplacement refusé se montre, même sous la ligne de flottaison ; la date reprend le focus, perdu le temps de l'essai.</summary>
    private void OnMoveErrorVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => MoveErrorMessage.BringIntoView());
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (KeyboardFocus.IsLost() || MoveForm.IsKeyboardFocusWithin)
                {
                    MoveDay.Focus();
                }
            });
        }
    }

    /// <summary>Un déplacement réussi relit encore la charge, ce qui refait lignes et colonnes : le focus attend qu'il ait fini.</summary>
    private async Task GiveFocusBackAsync(WorkloadPageViewModel page)
    {
        if (page.MoveCommand.ExecutionTask is { } moving)
        {
            await moving.ConfigureAwait(true);
        }
        await Dispatcher.InvokeAsync(() => FocusAfterMove(page), DispatcherPriority.Input);
    }

    /// <summary>Le focus resté dans le formulaire revient à « Déplacer… » de sa ligne ou, si l'échéance est partie, au jour choisi.</summary>
    private void FocusAfterMove(WorkloadPageViewModel page)
    {
        if (!IsVisible || !KeyboardFocus.IsLost())
        {
            return;
        }
        if (page.RowOfLastMove is { } row && DueList.ItemContainerGenerator.ContainerFromItem(row) is DependencyObject container && MoveButtonIn(container) is { } button && button.Focus())
        {
            return;
        }
        if (page.SelectedDay is { } day && Chart.ItemContainerGenerator.ContainerFromItem(day) is ListBoxItem column)
        {
            column.Focus();
            column.BringIntoView();
        }
    }

    private void ShowDetail() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => DetailCard.BringIntoView());

    private static Button? MoveButtonIn(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button { Name: "MoveRow" } button)
            {
                return button;
            }
            if (MoveButtonIn(child) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private static ListBoxItem? ContainerOf(DependencyObject? element)
    {
        for (; element is not null; element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
        {
            if (element is ListBoxItem item)
            {
                return item;
            }
        }
        return null;
    }
}
