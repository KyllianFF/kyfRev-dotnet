using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue « Aujourd'hui » : le tableau de bord.</summary>
internal sealed partial class DashboardPage : UserControl
{
    /// <summary>Les actions d'un examen, ouvertes sous sa ligne ou son bouton.</summary>
    private readonly ContextMenu _examMenu = new();

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
        else if (row is ExamRow exam)
        {
            // Les boutons de la ligne ne prennent pas le focus : ses actions passent par ce menu.
            OpenExamMenu(exam, (UIElement)Keyboard.FocusedElement);
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
            case ExamRow exam when Page.Exams is { IsBusy: false } exams:
                List<Control> entries = [];
                if (exam.CanTighten)
                {
                    entries.Add(ActionMenus.Item(ExamText.TightenAction, () => exams.RequestTighten(exam)));
                }
                entries.Add(ActionMenus.Item(ExamText.EditAction, () => exams.EditCommand.Execute(exam)));
                entries.Add(ActionMenus.Item(ExamText.WithdrawAction, () => exams.RequestWithdraw(exam)));
                return entries;
            default:
                return [];
        }
    }

    /// <summary>« Resserrer… », sur la ligne d'un examen.</summary>
    private void OnTightenClick(object sender, RoutedEventArgs e) => Page.Exams!.RequestTighten((ExamRow)((FrameworkElement)sender).DataContext);

    /// <summary>« ⋯ », sur la ligne d'un examen : ses actions, sous le bouton.</summary>
    private void OnExamMenuClick(object sender, RoutedEventArgs e) => OpenExamMenu((ExamRow)((FrameworkElement)sender).DataContext, (UIElement)sender);

    private void OpenExamMenu(ExamRow exam, UIElement target)
    {
        _examMenu.Items.Clear();
        foreach (var item in RowMenuItems(exam))
        {
            _examMenu.Items.Add(item);
        }
        _examMenu.PlacementTarget = target;
        _examMenu.Placement = PlacementMode.Bottom;
        _examMenu.IsOpen = _examMenu.Items.Count > 0;
    }

    /// <summary>La réponse à la fenêtre « Nouvelle fiche ».</summary>
    private void OnNewSheetClosed(object sender, DialogClosedEventArgs e) => _ = Page.AnswerNewSheetCommand.ExecuteAsync(e.Choice == DialogChoice.Primary);

    /// <summary>La réponse à la fenêtre d'étalement ; le focus revient au bouton qui l'a ouverte.</summary>
    private void OnSpreadClosed(object sender, DialogClosedEventArgs e)
    {
        _ = Page.Spread!.AnswerCommand.ExecuteAsync(e.Choice == DialogChoice.Primary);
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (IsVisible && KeyboardFocus.IsLost())
            {
                SpreadButton.Focus();
            }
        });
    }

    /// <summary>La réponse au formulaire d'un examen part vers les examens.</summary>
    private void OnExamEditorClosed(object sender, DialogClosedEventArgs e) => _ = FocusExamAfterAsync(Page.Exams!.AnswerEditorCommand.ExecuteAsync(e.Choice == DialogChoice.Primary));

    /// <summary>La réponse à la confirmation d'un examen part vers les examens.</summary>
    private void OnExamConfirmationClosed(object sender, DialogClosedEventArgs e) => _ = FocusExamAfterAsync(Page.Exams!.AnswerCommand.ExecuteAsync(e.Choice == DialogChoice.Primary));

    /// <summary>La réponse relit le tableau de bord, ce qui refait les lignes : le focus attend qu'elle ait fini.</summary>
    private async Task FocusExamAfterAsync(Task answer)
    {
        await answer.ConfigureAwait(true);
        await Dispatcher.InvokeAsync(FocusExam, DispatcherPriority.Input);
    }

    /// <summary>
    /// Le focus perdu avec la ligne refaite (il remonte alors à la liste) revient à l'examen ; retiré, à l'en-tête de son
    /// groupe, ou au bouton qui en crée un.
    /// </summary>
    private void FocusExam()
    {
        var lost = KeyboardFocus.IsLost() || ReferenceEquals(Keyboard.FocusedElement, Items);
        if (!IsVisible || !lost || DataContext is not DashboardPageViewModel { Exams: { IsEditing: false, IsConfirming: false } } page)
        {
            return;
        }
        if (page.ExamRowToFocus() is not { } row)
        {
            NewExamButton.Focus();
            return;
        }
        Items.ScrollIntoView(row);
        Items.UpdateLayout();
        (Items.ItemContainerGenerator.ContainerFromItem(row) as ListBoxItem)?.Focus();
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
