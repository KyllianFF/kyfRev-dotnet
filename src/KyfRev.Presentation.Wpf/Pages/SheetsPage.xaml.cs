using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue « Fiches ».</summary>
internal sealed partial class SheetsPage : UserControl
{
    public SheetsPage()
    {
        InitializeComponent();
        // Un clic sur une ligne l'ouvre quand il n'y a pas d'aperçu, même si la liste a déjà traité le clic.
        Items.AddHandler(MouseUpEvent, new MouseButtonEventHandler(OnItemsMouseUp), handledEventsToo: true);
        // La disposition suit la largeur de la page. La page quittée n'a plus de contexte.
        SizeChanged += (_, e) =>
        {
            if (DataContext is SheetsPageViewModel page)
            {
                page.PageWidth = e.NewSize.Width;
            }
        };
    }

    /// <summary>Un en-tête de dossier se lit mais ne se choisit pas : le choix précédent reste.</summary>
    private void OnItemsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SheetsPageViewModel && Items.SelectedItem is SheetFolderRow)
        {
            Items.SetCurrentValue(Selector.SelectedItemProperty, e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null);
        }
    }

    /// <summary>Entrée sur une ligne l'ouvre.</summary>
    private void OnItemsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.FocusedElement is ListBoxItem { DataContext: SheetRow row } && DataContext is SheetsPageViewModel page)
        {
            page.OpenCommand.Execute(row);
            e.Handled = true;
        }
    }

    /// <summary>Avec l'aperçu, un clic choisit la ligne ; un double clic l'ouvre.</summary>
    private void OnItemsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SheetsPageViewModel { HasPreview: true } page && Items.SelectedItem is SheetRow row)
        {
            page.OpenCommand.Execute(row);
        }
    }

    private void OnItemsMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SheetsPageViewModel { HasPreview: false } page && e.ChangedButton == MouseButton.Left
            && ContainerOf(e.OriginalSource as DependencyObject) is { DataContext: SheetRow row })
        {
            page.OpenCommand.Execute(row);
        }
    }

    private void OnCreateClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetsPageViewModel page)
        {
            page.AnswerCreateCommand.Execute(e.Choice == DialogChoice.Primary);
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
