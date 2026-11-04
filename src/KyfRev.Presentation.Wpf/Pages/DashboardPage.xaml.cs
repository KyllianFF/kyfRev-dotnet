using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue « Aujourd'hui » : le tableau de bord.</summary>
internal sealed partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
        // Un clic sur une ligne l'ouvre, même si la liste a déjà traité le clic pour la sélectionner.
        Items.AddHandler(MouseUpEvent, new MouseButtonEventHandler(OnItemsMouseUp), handledEventsToo: true);
        SecondItems.AddHandler(MouseUpEvent, new MouseButtonEventHandler(OnItemsMouseUp), handledEventsToo: true);
        SortMenu.AddHandler(MenuItem.ClickEvent, new RoutedEventHandler(OnSortChosen));
        foreach (var list in new ListBox[] { Items, SecondItems })
        {
            list.ContextMenu = new ContextMenu();
            list.ContextMenuOpening += OnRowMenu;
        }
        // La disposition suit la largeur de la page, zoom et menu déjà comptés.
        SizeChanged += (_, e) => Page.PageWidth = e.NewSize.Width;
    }

    private DashboardPageViewModel Page => (DashboardPageViewModel)DataContext;

    /// <summary>
    /// Entrée ou Espace sur l'en-tête d'un groupe le replie ou le déplie (la liste est alors
    /// reconstruite : le focus est rendu à l'en-tête) ; Entrée sur une ligne l'ouvre.
    /// </summary>
    private void OnItemsKeyDown(object sender, KeyEventArgs e)
    {
        var list = (ListBox)sender;
        if (e.Key is not (Key.Enter or Key.Space) || Keyboard.FocusedElement is not ListBoxItem { DataContext: var row })
        {
            return;
        }
        if (row is DashboardGroup group)
        {
            group.ToggleCommand.Execute(null);
            list.ScrollIntoView(group);
            list.UpdateLayout();
            (list.ItemContainerGenerator.ContainerFromItem(group) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && row is DueItemRow or ReminderRow)
        {
            Page.OpenCommand.Execute(row);
            e.Handled = true;
        }
    }

    /// <summary>
    /// La suite de la liste (les jours à venir, la semaine, les encarts) se lit mais ne se choisit pas ;
    /// une ligne choisie s'affiche dans l'aperçu.
    /// </summary>
    private void OnItemsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // La page qu'on quitte perd son contexte, puis sa liste se vide : il n'y a plus rien à choisir.
        if (DataContext is not DashboardPageViewModel)
        {
            return;
        }
        // SetCurrentValue : la liaison de SelectedItem à la ligne choisie doit survivre.
        var list = (ListBox)sender;
        if (list.SelectedItem is DashboardExtras)
        {
            list.SetCurrentValue(Selector.SelectedItemProperty, null);
        }
        else if (Page.HasPreview && list.SelectedItem is DueItemRow or ReminderRow)
        {
            Page.SelectedRow = list.SelectedItem;
            (ReferenceEquals(list, Items) ? SecondItems : Items).SetCurrentValue(Selector.SelectedItemProperty, null);
        }
    }

    /// <summary>Avec l'aperçu, un clic choisit la ligne ; un double clic l'ouvre.</summary>
    private void OnItemsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Page.HasPreview && ((ListBox)sender).SelectedItem is (DueItemRow or ReminderRow) and var row)
        {
            Page.OpenCommand.Execute(row);
        }
    }

    /// <summary>« Trier » : les tris proposés, dans un menu ouvert sous le bouton.</summary>
    private void OnSortClick(object sender, RoutedEventArgs e)
    {
        SortMenu.PlacementTarget = SortButton;
        SortMenu.Placement = PlacementMode.Bottom;
        SortMenu.IsOpen = true;
    }

    private void OnSortChosen(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is MenuItem { DataContext: Choice<DashboardSort> choice })
        {
            Page.SelectedSort = choice;
        }
    }

    /// <summary>Un clic gauche sur une ligne (pas sur un en-tête) l'ouvre.</summary>
    private void OnItemsMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!Page.HasPreview && e.ChangedButton == MouseButton.Left && ContainerOf(e.OriginalSource as DependencyObject) is { DataContext: DueItemRow or ReminderRow } container)
        {
            Page.OpenCommand.Execute(container.DataContext);
        }
    }

    /// <summary>Le menu d'une ligne : ce qu'on peut en faire d'ici.</summary>
    private void OnRowMenu(object sender, ContextMenuEventArgs e)
    {
        var list = (ListBox)sender;
        ActionMenus.Fill(list.ContextMenu!, RowMenuItems(ContainerOf(e.OriginalSource as DependencyObject)?.DataContext), e);
    }

    /// <summary>Les entrées du menu de cette ligne ; aucune pour un en-tête.</summary>
    internal IReadOnlyList<Control> RowMenuItems(object? row)
    {
        switch (row)
        {
            case DueItemRow item:
                List<Control> items =
                [
                    ActionMenus.Quick(DashboardText.ReviewAction, Icons.Review, () => Page.OpenCommand.Execute(item)),
                    ActionMenus.Quick(DashboardText.OpenSheet, Icons.Document, () => Page.OpenSheetOf(item)),
                ];
                if (Page.CanAct)
                {
                    items.Add(new Separator());
                    items.Add(ActionMenus.Item(ReviewText.PostponeTomorrow, () => _ = Page.PostponeTomorrowAsync(item)));
                    items.Add(ActionMenus.Item(ReviewText.ArchiveAction, () => Page.RequestArchive(item)));
                }
                return items;
            case ReminderRow:
                return [ActionMenus.Quick(DashboardText.OpenSheet, Icons.Document, () => Page.OpenCommand.Execute(row))];
            default:
                return [];
        }
    }

    /// <summary>La réponse à la mise aux archives part vers la vue.</summary>
    private void OnArchiveClosed(object sender, DialogClosedEventArgs e) => Page.AnswerArchiveCommand.Execute(e.Choice == DialogChoice.Primary);

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
