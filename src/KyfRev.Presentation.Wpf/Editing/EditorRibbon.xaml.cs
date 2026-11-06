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
        TableTab.IsVisibleChanged += OnTableTabShown;
        Item(StyleMenu, EditingText.NormalText, page => page.SetHeadingCommand.Execute(0));
        for (var level = 1; level <= 4; level++)
        {
            var chosen = level;
            Item(StyleMenu, EditingText.Heading(level), page => page.SetHeadingCommand.Execute(chosen));
        }
        foreach (var pixels in FontSizes.Steps)
        {
            var label = pixels == FontSizes.Normal ? EditingText.FontSizeNormal(pixels) : pixels.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Item(SizeMenu, label, page => page.SetFontSizeCommand.Execute(pixels));
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
        _colorPopup = new Popup { Child = _colors, StaysOpen = false, Placement = PlacementMode.Bottom, PlacementTarget = ColorButton, AllowsTransparency = false };
        _colors.Chosen += OnColorChosen;
        _colors.Cancelled += OnColorCancelled;
    }

    /// <summary>Le choix de la taille d'un tableau.</summary>
    internal TableChooser Chooser => _chooser;

    /// <summary>La liste qui porte ce choix.</summary>
    internal Popup TablePopup => _tablePopup;

    /// <summary>Le choix de la couleur du texte.</summary>
    internal ColorChooser Colors => _colors;

    /// <summary>La liste qui porte ce choix.</summary>
    internal Popup ColorPopup => _colorPopup;

    /// <summary>Demande les images à insérer ; fourni par la page.</summary>
    internal Action? ChooseImages { get; set; }

    private readonly TableChooser _chooser = new();
    private readonly Popup _tablePopup;
    private readonly ColorChooser _colors = new();
    private readonly Popup _colorPopup;

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

    private void OnColorButton(object sender, RoutedEventArgs e)
    {
        _colors.Prepare();
        _colorPopup.IsOpen = true;
        _colors.AutomaticButton.Focus();
    }

    private void OnColorCancelled() => _colorPopup.IsOpen = false;

    private void OnColorChosen(string? color)
    {
        _colorPopup.IsOpen = false;
        if (DataContext is SheetEditorViewModel page)
        {
            page.SetColorCommand.Execute(color);
        }
    }

    private void OnImagesButton(object sender, RoutedEventArgs e) => ChooseImages?.Invoke();

    private void OnHomeTab(object sender, RoutedEventArgs e) => ShowTab(HomePanel);

    private void OnInsertTab(object sender, RoutedEventArgs e) => ShowTab(InsertPanel);

    private void OnTableTab(object sender, RoutedEventArgs e) => ShowTab(TablePanel);

    /// <summary>Le curseur sort du tableau : son onglet disparaît, et l'Accueil revient s'il était ouvert.</summary>
    private void OnTableTabShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false && TablePanel.Visibility == Visibility.Visible)
        {
            ShowTab(HomePanel);
        }
    }

    /// <summary>Montre un onglet du ruban.</summary>
    internal void ShowTab(Panel shown)
    {
        HomeTab.IsChecked = shown == HomePanel;
        InsertTab.IsChecked = shown == InsertPanel;
        TableTab.IsChecked = shown == TablePanel;
        foreach (var panel in new Panel[] { HomePanel, InsertPanel, TablePanel })
        {
            panel.Visibility = panel == shown ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
