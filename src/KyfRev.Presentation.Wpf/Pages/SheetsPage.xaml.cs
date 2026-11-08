using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue « Fiches ».</summary>
internal sealed partial class SheetsPage : UserControl
{
    private const string DragFormat = "kyfrev/ligne-de-fiches";
    private const double NearEdge = 48;
    private const double CloseToEdge = 16;
    private const int FastLines = 3;

    private Point _pressedAt;
    private object? _pressedRow;
    private object? _dragged;
    private ListBoxItem? _dropTarget;
    private ScrollViewer? _scroller;

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

    /// <summary>Lance le glisser-déposer ; remplacé dans les essais, où rien ne peut le conduire.</summary>
    internal Func<DependencyObject, object, DragDropEffects, DragDropEffects> StartDrag { get; set; } = DragDrop.DoDragDrop;

    /// <summary>Les lignes à faire défiler à cette hauteur du pointeur : près d'un bord, et d'autant plus qu'il en est près.</summary>
    internal static int ScrollLinesAt(double y, double height)
    {
        if (y < CloseToEdge)
        {
            return -FastLines;
        }
        if (y < NearEdge)
        {
            return -1;
        }
        if (y > height - CloseToEdge)
        {
            return FastLines;
        }
        return y > height - NearEdge ? 1 : 0;
    }

    /// <summary>La souris a bougé, bouton appuyé ou non : au-delà du seuil, le glisser-déposer commence.</summary>
    internal void Moved(Point position, bool isPressed)
    {
        if (!isPressed)
        {
            _pressedRow = null;
            return;
        }
        if (_pressedRow is not { } row || DataContext is not SheetsPageViewModel { CanManage: true } page
            || (Math.Abs(position.X - _pressedAt.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(position.Y - _pressedAt.Y) < SystemParameters.MinimumVerticalDragDistance))
        {
            return;
        }
        _pressedRow = null;
        _dragged = row;
        // La liste garde la souris depuis l'appui : sans cela, elle change de ligne choisie sous le pointeur.
        Items.ReleaseMouseCapture();
        StartDrag(Items, new DataObject(DragFormat, DragFormat), DragDropEffects.Move);
        _dragged = null;
        Highlight(null);
        page.ShowDrop(null, null);
    }

    /// <summary>Le pointeur passe au-dessus de cette ligne : fait défiler près des bords, et dit si l'on peut y déposer.</summary>
    internal bool DraggedOver(object? target, double y)
    {
        _scroller ??= Inside<ScrollViewer>(Items);
        if (_scroller is not null)
        {
            var lines = ScrollLinesAt(y, Items.ActualHeight);
            for (var i = 0; i < Math.Abs(lines); i++)
            {
                if (lines < 0)
                {
                    _scroller.LineUp();
                }
                else
                {
                    _scroller.LineDown();
                }
            }
        }
        if (DataContext is not SheetsPageViewModel page)
        {
            return false;
        }
        var folder = page.ShowDrop(_dragged, target);
        Highlight(folder);
        // L'indication se tient loin du pointeur : elle ne cache pas la ligne visée.
        DropHintBox.VerticalAlignment = y > Items.ActualHeight / 2 ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        return folder is not null;
    }

    /// <summary>Le dépôt sur cette ligne.</summary>
    internal Task DroppedOn(object? target)
    {
        var dragged = _dragged;
        Highlight(null);
        return DataContext is SheetsPageViewModel page && page.DropFolderOf(dragged, target) is { } folder
            ? page.DropAsync(dragged!, folder)
            : Task.CompletedTask;
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
            if (page.Sharing is not null)
            {
                items.Add(ActionMenus.Item(SharingText.Share, () => page.RequestShare(row)));
                items.Add(ActionMenus.Item(SharingText.SaveAsTemplate, () => page.RequestSaveAsTemplate(row)));
            }
            items.Add(new Separator());
            items.Add(ActionMenus.Item(row.Sheet.Archived ? SheetActionsText.Restart : SheetActionsText.Archive, () => page.RequestArchiveOrRestart(row)));
            items.Add(ActionMenus.Item(SheetActionsText.Delete, () => page.RequestTrash(row)));
        }
        AddExtensions(items, page, ExtensionMenuZone.Sheet, new Dictionary<string, string> { ["id"] = row.Sheet.Id, ["titre"] = row.Sheet.Title, ["dossier"] = row.Sheet.Folder });
        return items;
    }

    /// <summary>Les entrées des extensions, après celles de kyfRev.</summary>
    private static void AddExtensions(List<Control> items, SheetsPageViewModel page, ExtensionMenuZone zone, IReadOnlyDictionary<string, string> context)
    {
        if (page.ExtensionMenus?.Invoke(zone, context) is not { Count: > 0 } added)
        {
            return;
        }
        items.Add(new Separator());
        items.AddRange(added.Select(entry => ActionMenus.Item(entry.Label, entry.Run)));
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
        AddExtensions(items, page, ExtensionMenuZone.Folder, new Dictionary<string, string> { ["dossier"] = row.Path });
        return items;
    }

    private static List<Control> FreeSpaceMenu(SheetsPageViewModel page)
    {
        List<Control> items =
        [
            ActionMenus.Quick(SheetActionsText.NewSheet, Icons.Document, () => _ = page.CreateInAsync(string.Empty)),
            ActionMenus.Quick(SheetActionsText.NewFolder, Icons.NewFolder, () => page.RequestNewFolder(string.Empty)),
        ];
        if (page.CanImport)
        {
            items.Add(new Separator());
            items.Add(ActionMenus.Item(SharingText.ImportFolder, () => page.RequestImportCommand.Execute(null)));
        }
        return items;
    }

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
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
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

    private void OnItemsPressed(object sender, MouseButtonEventArgs e)
    {
        _pressedAt = e.GetPosition(Items);
        _pressedRow = ContainerOf(e.OriginalSource as DependencyObject)?.DataContext;
    }

    private void OnItemsMoved(object sender, MouseEventArgs e) => Moved(e.GetPosition(Items), e.LeftButton == MouseButtonState.Pressed);

    private void OnItemsDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DraggedOver(ContainerOf(e.OriginalSource as DependencyObject)?.DataContext, e.GetPosition(Items).Y) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnItemsDragLeave(object sender, DragEventArgs e)
    {
        Highlight(null);
        (DataContext as SheetsPageViewModel)?.ShowDrop(null, null);
    }

    private void OnItemsDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        _ = DroppedOn(ContainerOf(e.OriginalSource as DependencyObject)?.DataContext);
    }

    /// <summary>Met en évidence l'en-tête du dossier visé, s'il est à l'écran.</summary>
    private void Highlight(string? folder)
    {
        var container = folder is null ? null : Items.ItemContainerGenerator.ContainerFromItem(Items.Items.OfType<SheetFolderRow>().FirstOrDefault(row => row.Path == folder)) as ListBoxItem;
        if (!ReferenceEquals(container, _dropTarget))
        {
            _dropTarget?.ClearValue(BackgroundProperty);
            _dropTarget = container;
            _dropTarget?.SetResourceReference(BackgroundProperty, "Kyf.Brush.NavigationActiveBackground");
        }
    }

    private static T? Inside<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if ((child as T ?? Inside<T>(child)) is { } found)
            {
                return found;
            }
        }
        return null;
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
