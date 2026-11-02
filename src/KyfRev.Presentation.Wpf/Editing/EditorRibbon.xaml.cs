using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Le ruban d'écriture, comme ceux d'Office : un onglet pour mettre en forme, un autre pour insérer.</summary>
internal sealed partial class EditorRibbon : UserControl
{
    public EditorRibbon()
    {
        InitializeComponent();
        Item(StyleMenu, EditingText.NormalText, page => page.SetHeadingCommand.Execute(0));
        for (var level = 1; level <= 4; level++)
        {
            var chosen = level;
            Item(StyleMenu, EditingText.Heading(level), page => page.SetHeadingCommand.Execute(chosen));
        }
        foreach (var (style, label) in new (CaseStyle Style, string Label)[]
        {
            (CaseStyle.Sentence, EditingText.CaseSentence),
            (CaseStyle.Lower, EditingText.CaseLower),
            (CaseStyle.Upper, EditingText.CaseUpper),
            (CaseStyle.Words, EditingText.CaseWords),
            (CaseStyle.Invert, EditingText.CaseInvert),
            (CaseStyle.Alternate, EditingText.CaseAlternate),
        })
        {
            Item(CaseMenu, label, page => page.ChangeCaseCommand.Execute(style));
        }
        foreach (var language in CodeLanguages.All)
        {
            Item(CodeMenu, language.Name, page => page.InsertCodeCommand.Execute(language.Id));
        }
        _chooser.Chosen += OnTableChosen;
        _tablePopup = new Popup { Child = _chooser, StaysOpen = false, Placement = PlacementMode.Bottom, PlacementTarget = TableButton, AllowsTransparency = false };
    }

    /// <summary>Le choix de la taille d'un tableau.</summary>
    internal TableChooser Chooser => _chooser;

    /// <summary>La liste qui porte ce choix.</summary>
    internal Popup TablePopup => _tablePopup;

    /// <summary>Demande les images à insérer ; fourni par la page.</summary>
    internal Action? ChooseImages { get; set; }

    private readonly TableChooser _chooser = new();
    private readonly Popup _tablePopup;

    private void Item(ContextMenu menu, string header, Action<SheetEditorViewModel> action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) =>
        {
            if (DataContext is SheetEditorViewModel page)
            {
                action(page);
            }
        };
        menu.Items.Add(item);
    }

    private void OnMenuButton(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var menu = button.ContextMenu!;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnTableButton(object sender, RoutedEventArgs e)
    {
        _tablePopup.IsOpen = true;
        _chooser.Focus();
    }

    private void OnTableChosen(int rows, int columns)
    {
        _tablePopup.IsOpen = false;
        if (DataContext is SheetEditorViewModel page)
        {
            page.InsertTableCommand.Execute(new TableSize(rows, columns));
        }
    }

    private void OnImagesButton(object sender, RoutedEventArgs e) => ChooseImages?.Invoke();

    private void OnHomeTab(object sender, RoutedEventArgs e) => ShowTab(home: true);

    private void OnInsertTab(object sender, RoutedEventArgs e) => ShowTab(home: false);

    private void ShowTab(bool home)
    {
        HomeTab.IsChecked = home;
        InsertTab.IsChecked = !home;
        HomePanel.Visibility = home ? Visibility.Visible : Visibility.Collapsed;
        InsertPanel.Visibility = home ? Visibility.Collapsed : Visibility.Visible;
    }
}
