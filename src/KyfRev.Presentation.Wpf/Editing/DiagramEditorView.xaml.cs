using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>L'éditeur d'une hiérarchie : la liste de ses éléments, et son dessin.</summary>
internal sealed partial class DiagramEditorView : UserControl
{
    private DiagramEditorViewModel? _editor;

    public DiagramEditorView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            _editor?.NodeToFocus -= OnNodeToFocus;
            _editor = e.NewValue as DiagramEditorViewModel;
            _editor?.NodeToFocus += OnNodeToFocus;
            if (_editor?.Selected is { } first)
            {
                OnNodeToFocus(this, first);
            }
        };
    }

    /// <summary>Le menu d'un élément : ce qui s'y fait, puis le presse-papiers de son champ.</summary>
    internal List<Control> NodeMenuItems(TextBox? field)
    {
        if (_editor is not { } editor)
        {
            return [];
        }
        var items = new List<Control>
        {
            ActionMenus.Quick(EditingText.DiagramChild, Icons.Add, editor.AddChildCommand, this),
            ActionMenus.Quick(EditingText.DiagramSibling, Icons.Add, editor.AddSiblingCommand, this),
            new Separator(),
            Command(EditingText.DiagramIndent, editor.IndentCommand, EditingText.DiagramIndentShortcut),
            Command(EditingText.DiagramOutdent, editor.OutdentCommand, EditingText.DiagramOutdentShortcut),
            Command(EditingText.DiagramMoveUp, editor.MoveUpCommand, EditingText.DiagramMoveUpShortcut),
            Command(EditingText.DiagramMoveDown, editor.MoveDownCommand, EditingText.DiagramMoveDownShortcut),
            new Separator(),
            Command(EditingText.DiagramDelete, editor.DeleteCommand, null),
        };
        if (field is not null)
        {
            items.Add(new Separator());
            items.Add(ActionMenus.Item(EditingText.Cut, field.Cut));
            items.Add(ActionMenus.Item(EditingText.Copy, field.Copy));
            items.Add(ActionMenus.Item(EditingText.Paste, field.Paste));
        }
        return items;
    }

    private static MenuItem Command(string label, ICommand command, string? shortcut)
    {
        var item = ActionMenus.Item(label, () => command.Execute(null), shortcut);
        item.IsEnabled = command.CanExecute(null);
        return item;
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

    /// <summary>La liste vient de changer : le champ de l'élément prend le focus une fois en place, après celui du dialogue.</summary>
    private void OnNodeToFocus(object? sender, DiagramNode node) =>
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            if (NodesList.ItemContainerGenerator.ContainerFromItem(node) is DependencyObject container && Inside<TextBox>(container) is { } field)
            {
                field.Focus();
                field.CaretIndex = field.Text.Length;
                field.BringIntoView();
            }
        });

    private void OnNodeFocused(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DiagramNode node })
        {
            _editor?.Select(node);
        }
    }

    private void OnNodeKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_editor?.TakeKey(key, Keyboard.Modifiers) == true)
        {
            e.Handled = true;
        }
    }

    private void OnNodeMenu(object sender, ContextMenuEventArgs e)
    {
        if (sender is TextBox { DataContext: DiagramNode node, ContextMenu: { } menu } field)
        {
            _editor?.Select(node);
            ActionMenus.Fill(menu, NodeMenuItems(field), e);
        }
    }

    private void OnPreviewClicked(object sender, MouseButtonEventArgs e) => SelectBoxAt(e.GetPosition(PreviewView));

    private void OnPreviewMenu(object sender, ContextMenuEventArgs e)
    {
        if (PreviewView.ContextMenu is not { } menu)
        {
            return;
        }
        if (PreviewView.BoxAt(new Point(e.CursorLeft, e.CursorTop)) is { } box)
        {
            _editor?.SelectBox(box);
            ActionMenus.Fill(menu, NodeMenuItems(null), e);
        }
        else
        {
            ActionMenus.Fill(menu, [], e);
        }
    }

    private void SelectBoxAt(Point point)
    {
        if (PreviewView.BoxAt(point) is { } box)
        {
            _editor?.SelectBox(box);
        }
    }
}
