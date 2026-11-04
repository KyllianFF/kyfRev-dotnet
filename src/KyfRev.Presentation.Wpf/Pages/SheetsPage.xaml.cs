using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
        Items.ContextMenu = new ContextMenu();
        Items.ContextMenuOpening += OnRowMenu;
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

    /// <summary>Le menu de ce qui est visé : une fiche, un dossier, ou la place libre de la liste.</summary>
    private void OnRowMenu(object sender, ContextMenuEventArgs e) =>
        ActionMenus.Fill(Items.ContextMenu!, RowMenuItems(ContainerOf(e.OriginalSource as DependencyObject)?.DataContext), e);

    /// <summary>Les entrées du menu de cette ligne ; sans ligne, celles de la place libre.</summary>
    internal IReadOnlyList<Control> RowMenuItems(object? row)
    {
        if (DataContext is not SheetsPageViewModel page)
        {
            return [];
        }
        return row switch
        {
            SheetRow sheet => SheetMenu(page, sheet),
            SheetFolderRow { IsFolder: true } folder => FolderMenu(page, folder),
            _ => FreeSpaceMenu(page),
        };
    }

    private static List<Control> SheetMenu(SheetsPageViewModel page, SheetRow row)
    {
        List<Control> items =
        [
            ActionMenus.Quick(SheetActionsText.Open, Icons.Document, () => page.OpenCommand.Execute(row)),
            ActionMenus.Quick(SheetActionsText.Review, Icons.Review, () => page.Revise(row), isEnabled: !row.Sheet.Archived),
            ActionMenus.Quick(SheetActionsText.Write, Icons.Edit, () => page.Write(row)),
        ];
        if (page.CanManage)
        {
            items.Add(new Separator());
            items.Add(ActionMenus.Item(SheetActionsText.Rename, () => page.RequestRename(row)));
            items.Add(ActionMenus.Item(SheetActionsText.Tags, () => page.RequestTags(row)));
            items.Add(ActionMenus.Item(SheetActionsText.Move, () => page.RequestMove(row)));
            items.Add(new Separator());
            items.Add(ActionMenus.Item(row.Sheet.Archived ? SheetActionsText.Restart : SheetActionsText.Archive, () => page.RequestArchiveOrRestart(row)));
            items.Add(ActionMenus.Item(SheetActionsText.Delete, () => page.RequestTrash(row)));
        }
        return items;
    }

    private static List<Control> FolderMenu(SheetsPageViewModel page, SheetFolderRow row)
    {
        List<Control> items =
        [
            ActionMenus.Quick(SheetActionsText.NewSheet, Icons.Document, () => _ = page.CreateInAsync(row.Path)),
            ActionMenus.Quick(SheetActionsText.NewFolder, Icons.NewFolder, () => page.RequestNewFolder(row.Path)),
        ];
        if (page.CanManage)
        {
            items.Add(new Separator());
            items.Add(ActionMenus.Item(SheetActionsText.Rename, () => page.RequestRenameFolder(row)));
            items.Add(ActionMenus.Item(SheetActionsText.Move, () => page.RequestMoveFolder(row)));
            items.Add(new Separator());
            items.Add(ActionMenus.Item(SheetActionsText.Delete, () => page.RequestTrashFolder(row)));
        }
        return items;
    }

    private static List<Control> FreeSpaceMenu(SheetsPageViewModel page) =>
    [
        ActionMenus.Quick(SheetActionsText.NewSheet, Icons.Document, () => _ = page.CreateInAsync(string.Empty)),
        ActionMenus.Quick(SheetActionsText.NewFolder, Icons.NewFolder, () => page.RequestNewFolder(string.Empty)),
    ];

    /// <summary>La réponse à la fenêtre d'action part vers la vue.</summary>
    private void OnActionClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetsPageViewModel page)
        {
            page.AnswerActionCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    /// <summary>Un nom se saisit tout de suite : le champ prend le focus, son texte sélectionné, après l'ouverture du dialogue.</summary>
    private void OnActionInputShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                ActionInputBox.Focus();
                ActionInputBox.SelectAll();
            });
        }
    }

    /// <summary>Entrée, Espace ou les flèches plient un dossier ; le focus lui est rendu, la liste étant reconstruite.</summary>
    private void ToggleFolderFromKeyboard(SheetsPageViewModel page, SheetFolderRow row)
    {
        page.ToggleFolderCommand.Execute(row);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var shown = Items.Items.OfType<SheetFolderRow>().FirstOrDefault(other => other.Path == row.Path);
            if (shown is not null)
            {
                Items.ScrollIntoView(shown);
                (Items.ItemContainerGenerator.ContainerFromItem(shown) as ListBoxItem)?.Focus();
            }
        });
    }

    /// <summary>Entrée sur une ligne l'ouvre ; sur un dossier, Entrée, Espace et les flèches le plient ou le déplient.</summary>
    private void OnItemsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is ListBoxItem { DataContext: SheetFolderRow { IsFolder: true } folder } && DataContext is SheetsPageViewModel owner
            && (e.Key is Key.Enter or Key.Space || (e.Key == Key.Left && folder.IsExpanded) || (e.Key == Key.Right && !folder.IsExpanded)))
        {
            ToggleFolderFromKeyboard(owner, folder);
            e.Handled = true;
            return;
        }
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
        if (DataContext is SheetsPageViewModel owner && e.ChangedButton == MouseButton.Left
            && ContainerOf(e.OriginalSource as DependencyObject) is { DataContext: SheetFolderRow { IsFolder: true } folder })
        {
            owner.ToggleFolderCommand.Execute(folder);
            return;
        }
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
