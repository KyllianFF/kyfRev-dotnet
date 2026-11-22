using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Shell;

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
    private ListBox? _pressedList;
    private SheetsPageViewModel? _model;
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
        PlacesList.ContextMenu = new ContextMenu();
        PlacesList.ContextMenuOpening += OnPlaceMenu;
        // L'aperçu a le menu de la fiche, le même que sa ligne (rangée 23B).
        PreviewHost.AddHandler(ContextMenuOpeningEvent, new ContextMenuEventHandler((_, e) =>
            PreviewMenus.Open(e, () => DataContext is SheetsPageViewModel { Preview.Row: SheetRow row } ? RowMenuItems(row) : [])));
        // Le volet des dossiers suit la vue : à gauche de la liste s'il y tient, dans le tiroir sinon.
        DataContextChanged += (_, _) => Follow(DataContext as SheetsPageViewModel);
        // La disposition suit la largeur de la page. La page quittée n'a plus de contexte.
        SizeChanged += (_, e) =>
        {
            if (DataContext is SheetsPageViewModel page)
            {
                page.PageWidth = e.NewSize.Width;
            }
        };
    }

    /// <summary>Suit la vue : où est le volet, et la liste qui arrive quand on change de dossier.</summary>
    private void Follow(SheetsPageViewModel? model)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model.PlaceChanged -= OnPlaceChanged;
        }
        _model = model;
        if (model is not null)
        {
            model.PropertyChanged += OnModelChanged;
            model.PlaceChanged += OnPlaceChanged;
        }
        PlacePanel();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SheetsPageViewModel.ShowsPane))
        {
            PlacePanel();
        }
        else if (e.PropertyName == nameof(SheetsPageViewModel.ShowsPlacesSheet) && _model is { ShowsPlacesSheet: true })
        {
            // Le tiroir s'ouvre : le clavier va au dossier choisi.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FocusChosenPlace);
        }
    }

    /// <summary>Le volet va là où il se montre : à gauche de la liste, ou dans le tiroir.</summary>
    internal void PlacePanel()
    {
        var host = _model is { ShowsPane: false } ? PlacesDrawerHost : PaneHost;
        if (ReferenceEquals(PlacesPanel.Parent, host))
        {
            return;
        }
        ((Border)PlacesPanel.Parent).Child = null;
        host.Child = PlacesPanel;
    }

    /// <summary>On a choisi un autre dossier : la liste arrive, en fondu, de quelques pixels plus bas.</summary>
    private void OnPlaceChanged(object? sender, EventArgs e) => Transitions.Enter(Items, DesignKeys.FastDuration, rise: 8, kind: MotionKinds.Views);

    private void FocusChosenPlace()
    {
        if (PlacesList.SelectedItem is { } chosen && PlacesList.ItemContainerGenerator.ContainerFromItem(chosen) is ListBoxItem item)
        {
            item.Focus();
        }
    }

    /// <summary>Un clic à côté du tiroir le referme.</summary>
    private void OnScrimPressed(object sender, MouseButtonEventArgs e)
    {
        _model?.ArePlacesOpen = false;
        e.Handled = true;
    }

    /// <summary>Échap referme le tiroir, et rend le clavier au bouton qui l'a ouvert.</summary>
    private void OnDrawerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _model is { ShowsPlacesSheet: true })
        {
            _model.ArePlacesOpen = false;
            PlacesButton.Focus();
            e.Handled = true;
        }
    }

    /// <summary>Dans le volet, ← plie un dossier ouvert et → l'ouvre.</summary>
    private void OnPlacesKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is ListBoxItem { DataContext: SheetFolderRow { IsFolder: true, HasChildren: true } folder } && _model is { } page
            && ((e.Key == Key.Left && folder.IsExpanded) || (e.Key == Key.Right && !folder.IsExpanded)))
        {
            page.ToggleFolderCommand.Execute(folder);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => FocusPlace(folder.Path));
            e.Handled = true;
        }
    }

    private void FocusPlace(string path)
    {
        var shown = PlacesList.Items.OfType<SheetFolderRow>().FirstOrDefault(row => row.IsFolder && row.Path == path);
        if (shown is not null && PlacesList.ItemContainerGenerator.ContainerFromItem(shown) is ListBoxItem item)
        {
            item.Focus();
        }
    }

    /// <summary>Le menu d'une entrée du volet : celui d'un dossier, ou celui de la place libre.</summary>
    private void OnPlaceMenu(object sender, ContextMenuEventArgs e) =>
        ActionMenus.Fill(PlacesList.ContextMenu!, RowMenuItems(ContainerOf(e.OriginalSource as DependencyObject)?.DataContext), e);

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
        var list = _pressedList ?? Items;
        // La liste garde la souris depuis l'appui : sans cela, elle change de ligne choisie sous le pointeur.
        list.ReleaseMouseCapture();
        StartDrag(list, new DataObject(DragFormat, DragFormat), DragDropEffects.Move);
        _dragged = null;
        Highlight(null);
        page.ShowDrop(null, null);
    }

    /// <summary>Le pointeur passe au-dessus de cette ligne : fait défiler près des bords, et dit si l'on peut y déposer.</summary>
    internal bool DraggedOver(object? target, double y, ListBox? over = null)
    {
        _scroller ??= Inside<ScrollViewer>(Items);
        if (_scroller is not null && (over is null || ReferenceEquals(over, Items)))
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
            SheetRow sheet => SheetRowMenu(page, sheet),
            SheetFolderRow { IsFolder: true } folder => FolderMenu(page, folder),
            _ => FreeSpaceMenu(page),
        };
    }

    /// <summary>Le menu d'une fiche, le même que partout (rangée 23A), sur les actions du Carnet.</summary>
    private static List<Control> SheetRowMenu(SheetsPageViewModel page, SheetRow row) => SheetMenu.Items(row.Target, new CarnetSheetMenu(page, row));

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
    /// <summary>Une étiquette proposée est prise ; la saisie reprend à sa suite.</summary>
    private void OnTagSuggestionClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string tag } && DataContext is SheetsPageViewModel page)
        {
            page.TakeTagCommand.Execute(tag);
            ActionInputBox.Focus();
            ActionInputBox.CaretIndex = ActionInputBox.Text.Length;
        }
    }

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

    /// <summary>Entrée sur une fiche l'ouvre ; sur l'intertitre d'un sous-dossier, elle mène à ce dossier.</summary>
    private void OnItemsKeyDown(object sender, KeyEventArgs e)
    {
        // Entrée sur l'intertitre d'un sous-dossier mène à ce dossier.
        if (e.Key == Key.Enter && e.OriginalSource is ListBoxItem { DataContext: SheetFolderRow { IsFolder: true } folder } && DataContext is SheetsPageViewModel owner)
        {
            owner.GoTo(folder.Path);
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
            // Un clic sur l'intertitre d'un sous-dossier mène à ce dossier.
            owner.GoTo(folder.Path);
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
        _pressedList = sender as ListBox;
        _pressedAt = e.GetPosition(Items);
        _pressedRow = ContainerOf(e.OriginalSource as DependencyObject)?.DataContext;
    }

    private void OnItemsMoved(object sender, MouseEventArgs e) => Moved(e.GetPosition(Items), e.LeftButton == MouseButtonState.Pressed);

    private void OnItemsDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DraggedOver(ContainerOf(e.OriginalSource as DependencyObject)?.DataContext, e.GetPosition(Items).Y, sender as ListBox) ? DragDropEffects.Move : DragDropEffects.None;
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

    /// <summary>Met en évidence le dossier visé : son entrée dans le volet, ou son intertitre dans la liste, s'il est à l'écran.</summary>
    private void Highlight(string? folder)
    {
        var container = folder is null ? null : Visible(PlacesList, folder) ?? Visible(Items, folder);
        if (!ReferenceEquals(container, _dropTarget))
        {
            _dropTarget?.ClearValue(BackgroundProperty);
            _dropTarget = container;
            _dropTarget?.SetResourceReference(BackgroundProperty, "Kyf.Brush.NavigationActiveBackground");
        }

        static ListBoxItem? Visible(ListBox list, string folder) =>
            list.IsVisible && list.Items.OfType<SheetFolderRow>().FirstOrDefault(row => row.TakesDrops && row.Path == folder) is { } row
                ? list.ItemContainerGenerator.ContainerFromItem(row) as ListBoxItem
                : null;
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

    /// <summary>Une étiquette proposée est prise ; la saisie reprend à sa suite.</summary>
    private void OnNewTagSuggestionClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string tag } && DataContext is SheetsPageViewModel page)
        {
            page.TakeNewTagCommand.Execute(tag);
            NewTagsBox.Focus();
            NewTagsBox.CaretIndex = NewTagsBox.Text.Length;
        }
    }

    /// <summary>« Dans Biologie › Cellule » : la liste des dossiers s'ouvre sous le bouton.</summary>
    private void OnNewFolderClick(object sender, RoutedEventArgs e)
    {
        var menu = NewFolderMenu.ContextMenu;
        menu.PlacementTarget = NewFolderMenu;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnNewFolderChosen(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu { DataContext: ListFilter filter } && e.OriginalSource is MenuItem { DataContext: Choice<object?> choice })
        {
            filter.Selected = choice;
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

    /// <summary>
    /// Le menu d'une fiche dans le Carnet : ses fenêtres pour renommer, déplacer, archiver ou supprimer ; les actions communes
    /// pour ce qu'il ne fait pas lui-même (reporter, rappels).
    /// </summary>
    private sealed class CarnetSheetMenu(SheetsPageViewModel page, SheetRow row) : ISheetMenuHost
    {
        public bool CanManage => page.CanManage;

        public bool CanShare => page.CanManage && page.Sharing is not null;

        public void Read(SheetTarget sheet) => page.OpenCommand.Execute(row);

        public void Revise(SheetTarget sheet) => page.Revise(row);

        public void Write(SheetTarget sheet) => page.Write(row);

        public void PostponeTomorrow(SheetTarget sheet) => page.SheetActions?.PostponeTomorrow(sheet);

        public void Archive(SheetTarget sheet) => page.RequestArchiveOrRestart(row);

        public void Restart(SheetTarget sheet) => page.RequestArchiveOrRestart(row);

        public void ToggleReminders(SheetTarget sheet) => page.SheetActions?.ToggleReminders(sheet);

        public void Rename(SheetTarget sheet) => page.RequestRename(row);

        public void Tags(SheetTarget sheet) => page.RequestTags(row);

        public void Move(SheetTarget sheet) => page.RequestMove(row);

        public void Print(SheetTarget sheet) => page.Print(row);

        public void Share(SheetTarget sheet) => page.RequestShare(row);

        public void SaveAsTemplate(SheetTarget sheet) => page.RequestSaveAsTemplate(row);

        public void ToggleNote(SheetTarget sheet) => _ = page.ToggleNoteAsync(row);

        public void Trash(SheetTarget sheet) => page.RequestTrash(row);

        public IReadOnlyList<MenuContribution> Extensions(SheetTarget sheet) =>
            page.ExtensionMenus?.Invoke(ExtensionMenuZone.Sheet, new Dictionary<string, string> { ["id"] = sheet.Id, ["titre"] = sheet.Title, ["dossier"] = sheet.Folder }) ?? [];
    }
}
