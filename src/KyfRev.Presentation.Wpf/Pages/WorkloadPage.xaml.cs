using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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
        if (ContainerOf(e.OriginalSource as DependencyObject) is { IsSelected: true })
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

    private void OnColumnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ShowDetail();
            e.Handled = true;
        }
    }

    /// <summary>Le champ de date prend le focus dès qu'il paraît.</summary>
    private void OnMoveFormVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => MoveDay.Focus());
        }
    }

    private void ShowDetail() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => DetailCard.BringIntoView());

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
