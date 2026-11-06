using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// Le ruban d'écriture, comme ceux d'Office : un onglet pour mettre en forme, un autre pour insérer. Quand la place manque, il
/// laisse place à une barre courte : le reste s'ouvre en volet, et l'affichage passe sous « ⋯ ».
/// </summary>
internal sealed partial class EditorRibbon : UserControl
{
    /// <summary>En deçà de cette largeur, la barre courte remplace le ruban.</summary>
    internal const double CompactWidth = 760;

    /// <summary>En deçà, le bouton « Tableau » ne garde que son icône.</summary>
    internal const double TightWidth = 580;

    /// <summary>En deçà, « Rétablir » et « Lien » ne restent que dans le volet.</summary>
    internal const double NarrowWidth = 520;

    /// <summary>En deçà, la liste à puces ne reste que dans le volet.</summary>
    internal const double BulletsWidth = 450;

    /// <summary>En deçà, « Mise en forme » ne garde que son icône (la fenêtre la plus étroite lui laisse son libellé).</summary>
    internal const double LabelWidth = 400;

    /// <summary>La hauteur du volet de la barre courte : au-delà, il défile, et le texte reste en vue dessous.</summary>
    internal const double CompactPanelHeight = 200;

    public EditorRibbon()
    {
        InitializeComponent();
        TableTab.IsVisibleChanged += OnTableTabShown;
        SizeChanged += (_, e) => Fit(e.NewSize.Width);
        foreach (var menu in new[] { StyleMenu, CompactStyleMenu })
        {
            Item(menu, EditingText.NormalText, page => page.SetHeadingCommand.Execute(0));
            for (var level = 1; level <= 4; level++)
            {
                var chosen = level;
                Item(menu, EditingText.Heading(level), page => page.SetHeadingCommand.Execute(chosen));
            }
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

    /// <summary>La place manque : la barre courte remplace le ruban.</summary>
    internal bool IsCompact { get; private set; }

    /// <summary>Le volet de la barre courte est ouvert.</summary>
    internal bool IsPanelOpen { get; private set; }

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

    /// <summary>
    /// Le curseur sort du tableau : son onglet disparaît, et l'Accueil revient s'il était ouvert. Un volet replié cache aussi l'onglet,
    /// sans que le curseur ait quitté le tableau : rien ne change alors.
    /// </summary>
    private void OnTableTabShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false && TableTab.Visibility != Visibility.Visible && TablePanel.Visibility == Visibility.Visible)
        {
            ShowTab(HomePanel);
        }
    }

    /// <summary>Montre un onglet du ruban.</summary>
    internal void ShowTab(Panel shown)
    {
        Select(shown);
        Arrange();
    }

    /// <summary>Le ruban suit sa largeur : complet s'il tient, sinon la barre courte et, sur demande, son volet.</summary>
    internal void Fit(double width)
    {
        IsCompact = width < CompactWidth;
        FormatLabel.Visibility = width < LabelWidth ? Visibility.Collapsed : Visibility.Visible;
        CompactTableLabel.Visibility = width < TightWidth ? Visibility.Collapsed : Visibility.Visible;
        CompactBulletsButton.Visibility = width < BulletsWidth ? Visibility.Collapsed : Visibility.Visible;
        var room = width < NarrowWidth ? Visibility.Collapsed : Visibility.Visible;
        CompactRedoButton.Visibility = room;
        CompactLinkButton.Visibility = room;
        Panels.MaxHeight = IsCompact ? CompactPanelHeight : double.PositiveInfinity;
        Arrange();
    }

    /// <summary>« Mise en forme » : ouvre le volet sur l'Accueil ou l'Insertion, ou le referme.</summary>
    private void OnFormatToggle(object sender, RoutedEventArgs e)
    {
        var showsTable = TablePanel.Visibility == Visibility.Visible;
        IsPanelOpen = !IsPanelOpen || showsTable;
        if (IsPanelOpen && showsTable)
        {
            Select(HomePanel);
        }
        Arrange();
    }

    /// <summary>« Tableau » : ouvre le volet sur l'onglet du tableau, ou le referme.</summary>
    private void OnCompactTable(object sender, RoutedEventArgs e)
    {
        IsPanelOpen = !(IsPanelOpen && TablePanel.Visibility == Visibility.Visible);
        if (IsPanelOpen)
        {
            Select(TablePanel);
        }
        Arrange();
    }

    private void Select(Panel shown)
    {
        HomeTab.IsChecked = shown == HomePanel;
        InsertTab.IsChecked = shown == InsertPanel;
        TableTab.IsChecked = shown == TablePanel;
        foreach (var panel in new Panel[] { HomePanel, InsertPanel, TablePanel })
        {
            panel.Visibility = panel == shown ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>Montre ce que la largeur permet : le ruban entier, ou la barre courte avec son volet s'il est ouvert.</summary>
    private void Arrange()
    {
        // Le curseur a quitté le tableau pendant que le volet était replié : l'Accueil reprend sa place.
        if (TablePanel.Visibility == Visibility.Visible && TableTab.Visibility != Visibility.Visible)
        {
            Select(HomePanel);
        }
        var open = !IsCompact || IsPanelOpen;
        CompactBar.Visibility = IsCompact ? Visibility.Visible : Visibility.Collapsed;
        ViewButtons.Visibility = IsCompact ? Visibility.Collapsed : Visibility.Visible;
        TabRow.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        Panels.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        var showsTable = TablePanel.Visibility == Visibility.Visible;
        FormatToggle.IsChecked = IsCompact && IsPanelOpen && !showsTable;
        CompactTableButton.IsChecked = IsCompact && IsPanelOpen && showsTable;
    }
}
