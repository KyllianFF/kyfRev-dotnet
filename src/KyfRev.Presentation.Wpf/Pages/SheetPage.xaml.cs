using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using KyfRev.DesignSystem.Controls;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue d'une fiche ouverte pour la lire et la réviser : son plan, son texte et ses passages.</summary>
internal sealed partial class SheetPage : UserControl
{
    public SheetPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) =>
        {
            if (DataContext is SheetPageViewModel page)
            {
                page.PageWidth = e.NewSize.Width;
            }
        };
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is SheetPageViewModel page)
            {
                page.PageWidth = ActualWidth;
            }
        };
    }

    /// <summary>La réponse à la confirmation (révision en avance, J0, archivage) part vers la barre de révision.</summary>
    private void OnConfirmationClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetPageViewModel { Review: { } review })
        {
            review.AnswerCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    /// <summary>Revenir au texte referme le volet ouvert par-dessus.</summary>
    private void OnTextPressed(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SheetPageViewModel page)
        {
            page.CloseOverlayCommand.Execute(null);
        }
    }

    private void OnOutlineEntryClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page && ((FrameworkElement)sender).DataContext is OutlineEntry entry)
        {
            page.CloseOverlayCommand.Execute(null);
            SheetText.Show(entry.Heading);
        }
    }

    private void OnPassageEntryClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page && ((FrameworkElement)sender).DataContext is PassageEntry entry)
        {
            ShowPassage(page, entry);
        }
    }

    private void ShowPassage(SheetPageViewModel page, PassageEntry entry)
    {
        page.CloseOverlayCommand.Execute(null);
        page.ActivateCommand.Execute(entry.Annotation.Id);
        if (entry.Position is { } position)
        {
            SheetText.Show(position.Start);
        }
    }

    /// <summary>Touche H : surligne la sélection, comme dans la 0.x.</summary>
    private void OnTextKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.None && DataContext is SheetPageViewModel page && SelectedPosition() is { } position)
        {
            e.Handled = true;
            page.HighlightCommand.Execute(position);
        }
    }

    /// <summary>Un clic sur un passage surligné le fait ressortir dans le panneau.</summary>
    private void OnTextReleased(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SheetPageViewModel page && SheetText.Selection is { IsEmpty: true } && SheetText.AnnotationsAt(e.OriginalSource) is [var id, ..])
        {
            page.ActivateCommand.Execute(id);
        }
    }

    /// <summary>Le menu du texte : ce qu'on peut faire de la sélection, ou du passage visé.</summary>
    private void OnTextMenu(object sender, ContextMenuEventArgs e)
    {
        Fill(TextMenu, TextMenuItems(e.OriginalSource), e);
    }

    private void OnPassageEntryMenu(object sender, ContextMenuEventArgs e)
    {
        var button = (FrameworkElement)sender;
        if (DataContext is SheetPageViewModel page && button.DataContext is PassageEntry entry)
        {
            Fill(button.ContextMenu!, PassageMenuItems(page, entry), e);
        }
    }

    private static void Fill(ContextMenu menu, IReadOnlyList<Control> items, ContextMenuEventArgs e)
    {
        menu.Items.Clear();
        foreach (var item in items)
        {
            menu.Items.Add(item);
        }
        e.Handled = items.Count == 0;
    }

    private static MenuItem Item(string header, Action action, string? shortcut = null)
    {
        var item = new MenuItem { Header = header, InputGestureText = shortcut ?? string.Empty };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>La position sélectionnée, si elle peut devenir un passage.</summary>
    private TextPosition? SelectedPosition() =>
        SheetText.CurrentSelection() is { } position && position.End - position.Start <= SheetPageViewModel.PassageMaximum ? position : null;

    internal IReadOnlyList<Control> TextMenuItems(object? source)
    {
        if (DataContext is not SheetPageViewModel page)
        {
            return [];
        }
        if (SelectedPosition() is { } position)
        {
            return
            [
                new MenuItem { Header = ReadingText.Copy, Command = ApplicationCommands.Copy, CommandTarget = SheetText },
                new Separator(),
                Item(ReadingText.Highlight, () => page.HighlightCommand.Execute(position), ReadingText.HighlightKey),
                Item(ReadingText.Annotate, () => page.StartAnnotatingCommand.Execute(position)),
                Item(ReadingText.Define, () => page.StartDefiningCommand.Execute(position)),
            ];
        }
        var entry = page.Passages.FirstOrDefault(candidate => SheetText.AnnotationsAt(source).Contains(candidate.Annotation.Id));
        return entry is null ? [] : PassageMenuItems(page, entry);
    }

    internal IReadOnlyList<Control> PassageMenuItems(SheetPageViewModel page, PassageEntry entry)
    {
        var items = new List<Control>();
        if (entry.Position is not null)
        {
            items.Add(Item(entry.IsDefinition ? ReadingText.ShowDefinition : ReadingText.ShowPassage, () => ShowPassage(page, entry)));
        }
        items.Add(Item(entry.IsDefinition ? ReadingText.EditDefinition : ReadingText.EditComment, () => page.StartEditingCommand.Execute(entry)));
        if (entry.IsCandidate)
        {
            items.Add(Item(ReadingText.ConfirmPosition, () => page.ConfirmPositionCommand.Execute(entry)));
            items.Add(Item(ReadingText.KeepWithoutPosition, () => page.KeepWithoutPositionCommand.Execute(entry)));
        }
        items.Add(new Separator());
        items.Add(Item(entry.IsDefinition ? ReadingText.DeleteDefinition : ReadingText.DeletePassage, () => page.RequestDeleteCommand.Execute(entry)));
        return items;
    }

    private void OnEditorClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page)
        {
            page.AnswerEditorCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    private void OnDeleteClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page)
        {
            page.AnswerDeleteCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }
}

/// <summary>Le retrait d'un titre du plan, selon son niveau.</summary>
public sealed class IndentConverter : IValueConverter
{
    private const double Step = 12;

    private IndentConverter()
    {
    }

    /// <summary>L'unique instance.</summary>
    public static IndentConverter Instance { get; } = new();

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new Thickness((int)value * Step, 0, 0, 0);

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
