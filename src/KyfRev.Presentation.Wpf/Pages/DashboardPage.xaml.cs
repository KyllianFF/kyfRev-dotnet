using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue « Aujourd'hui » : le tableau de bord.</summary>
internal sealed partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
        // Un clic sur une ligne l'ouvre, même si la liste a déjà traité le clic pour la sélectionner.
        Items.AddHandler(MouseUpEvent, new MouseButtonEventHandler(OnItemsMouseUp), handledEventsToo: true);
    }

    private DashboardPageViewModel Page => (DashboardPageViewModel)DataContext;

    /// <summary>
    /// Entrée ou Espace sur l'en-tête d'un groupe le replie ou le déplie (la liste est alors
    /// reconstruite : le focus est rendu à l'en-tête) ; Entrée sur une ligne l'ouvre.
    /// </summary>
    private void OnItemsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space) || Keyboard.FocusedElement is not ListBoxItem { DataContext: var row })
        {
            return;
        }
        if (row is DashboardGroup group)
        {
            group.ToggleCommand.Execute(null);
            Items.ScrollIntoView(group);
            Items.UpdateLayout();
            (Items.ItemContainerGenerator.ContainerFromItem(group) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && row is DueItemRow or ReminderRow)
        {
            Page.OpenCommand.Execute(row);
            e.Handled = true;
        }
    }

    /// <summary>Un clic gauche sur une ligne (pas sur un en-tête) l'ouvre.</summary>
    private void OnItemsMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && ContainerOf(e.OriginalSource as DependencyObject) is { DataContext: DueItemRow or ReminderRow } container)
        {
            Page.OpenCommand.Execute(container.DataContext);
        }
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
