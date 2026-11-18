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
        EnlargeCompactIcons(CompactBar);
        // Tous les onglets ont la hauteur de l'Accueil : le texte de la fiche ne bouge pas quand on passe de l'un à l'autre.
        HomePanel.SizeChanged += (_, e) =>
        {
            if (HomePanel.IsVisible && e.NewSize.Height > 0)
            {
                // La hauteur d'un groupe de l'Accueil : ceux des autres onglets la prennent, et leurs titres se posent sur la
                // même ligne, tout en bas.
                var line = HomePanel.Children.OfType<FrameworkElement>().Where(group => group.IsVisible).Select(group => group.ActualHeight).DefaultIfEmpty(0).Max();
                _homeHeights[IsReduced] = (e.NewSize.Height, line);
                FollowHome();
            }
        };
        TableTab.IsVisibleChanged += OnTableTabShown;
        ImageTab.IsVisibleChanged += OnImageTabShown;
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
        foreach (var points in FontSizes.Steps)
        {
            var label = points == FontSizes.Normal ? EditingText.FontSizeNormal(points) : points.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Item(SizeMenu, label, page => page.SetFontSizeCommand.Execute(points));
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
        // Après les menus qu'il reprend : « Plus » range aussi la casse.
        BuildMoreMenu();
        ShowDisplay();
    }

    /// <summary>
    /// Le ruban est réduit : une seule rangée d'outils, sans le nom des groupes, les outils qui servent moins sous « Plus ».
    /// Étendu, il montre tout, sur deux rangées, comme un traitement de texte.
    /// </summary>
    public static readonly DependencyProperty IsReducedProperty = DependencyProperty.Register(
        nameof(IsReduced), typeof(bool), typeof(EditorRibbon), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (ribbon, _) => ((EditorRibbon)ribbon).ShowDisplay()));

    /// <inheritdoc cref="IsReducedProperty"/>
    public bool IsReduced
    {
        get => (bool)GetValue(IsReducedProperty);
        set => SetValue(IsReducedProperty, value);
    }

    /// <summary>La hauteur de l'Accueil et celle d'un de ses groupes, vues dans chaque affichage.</summary>
    private readonly Dictionary<bool, (double Panel, double Line)> _homeHeights = [];

    /// <summary>Ce que « Plus » range : à cocher selon le texte sous le curseur, et grisé quand l'outil ne peut pas servir.</summary>
    private readonly List<(MenuItem Item, Func<SheetEditorViewModel, bool> Checked, Func<SheetEditorViewModel, bool> Enabled)> _more = [];

    /// <summary>Les autres onglets prennent la hauteur de l'Accueil dans l'affichage en cours, dès qu'elle est connue.</summary>
    private void FollowHome()
    {
        var known = _homeHeights.TryGetValue(IsReduced, out var heights);
        foreach (var panel in new WrapPanel[] { InsertPanel, TablePanel, ImagePanel, PagePanel })
        {
            panel.MinHeight = known ? heights.Panel : 0;
            panel.ItemHeight = known && heights.Line > 0 ? heights.Line : double.NaN;
        }
    }

    /// <summary>Applique l'affichage choisi : ce qui se montre, ce qui se range sous « Plus », et la forme des grands boutons.</summary>
    private void ShowDisplay()
    {
        var reduced = IsReduced;
        ReducedItem.IsChecked = reduced;
        ExtendedItem.IsChecked = !reduced;
        var rare = reduced ? Visibility.Collapsed : Visibility.Visible;
        foreach (var button in new[] { GrowButton, ShrinkButton, CaseButton, StrikeButton, SuperscriptButton, SubscriptButton, InlineCodeButton, AlignRightButton, AlignJustifyButton })
        {
            button.Visibility = rare;
        }
        MoreGroup.Visibility = reduced ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (rows, second) in new[] { (FontRows, FontSecondRow), (ParagraphRows, ParagraphSecondRow) })
        {
            rows.Orientation = reduced ? Orientation.Horizontal : Orientation.Vertical;
            second.Margin = reduced ? default : new Thickness(0, 2, 0, 0);
        }
        foreach (var button in new[] { TableButton, CodeButton, SchemaButton, ChartButton, NetworkButton, SmartArtButton, LinkButton, ImagesButton, FormulaButton, RuleButton, PageBreakButton, BlankPageButton, ImageCropButton, ImageMaskButton })
        {
            Lay(button, reduced);
        }
        // L'image : la hauteur et la largeur côte à côte, et les trois retours à l'origine sur une ligne.
        Grid.SetRow(ImageWidthGlyph, reduced ? 0 : 1);
        Grid.SetColumn(ImageWidthGlyph, reduced ? 2 : 0);
        Grid.SetRow(ImageWidthBox, reduced ? 0 : 1);
        Grid.SetColumn(ImageWidthBox, reduced ? 3 : 1);
        ImageHeightBox.Margin = reduced ? new Thickness(0, 0, 12, 0) : new Thickness(0, 0, 0, 2);
        foreach (var (button, column) in new[] { (ImageNarrowerButton, 2), (ImageWiderButton, 3) })
        {
            Grid.SetColumn(button, reduced ? column + 2 : column);
            Grid.SetRowSpan(button, reduced ? 1 : 2);
        }
        ImageResetGrid.Rows = reduced ? 1 : 2;
        ImageResetGrid.Columns = reduced ? 3 : 2;
        FollowHome();
    }

    /// <summary>
    /// Un grand outil : son icône au-dessus de son nom, ou, en réduit, à sa gauche dans un bouton de la hauteur des autres.
    /// </summary>
    private static void Lay(Button button, bool reduced)
    {
        button.SetResourceReference(StyleProperty, reduced ? "Kyf.Button.Tool" : "Kyf.Button.ToolLarge");
        if (reduced)
        {
            button.Padding = new Thickness(8, 0, 8, 0);
        }
        else
        {
            button.ClearValue(PaddingProperty);
        }
        switch (button.Content)
        {
            case StackPanel { Children.Count: > 0 } content:
                content.Orientation = reduced ? Orientation.Horizontal : Orientation.Vertical;
                var picture = (FrameworkElement)content.Children[0];
                picture.Margin = reduced ? new Thickness(0, 0, 6, 0) : default;
                picture.VerticalAlignment = VerticalAlignment.Center;
                if (picture is KyfRev.DesignSystem.Controls.Icon icon)
                {
                    icon.FontSize = reduced ? 16 : 20;
                }
                foreach (var label in content.Children.OfType<TextBlock>().Skip(picture is TextBlock ? 1 : 0))
                {
                    label.VerticalAlignment = VerticalAlignment.Center;
                }
                break;
            case RibbonGlyph glyph:
                glyph.Width = glyph.Height = reduced ? 20 : 28;
                break;
            default:
                break;
        }
    }

    private void OnReducedItem(object sender, RoutedEventArgs e) => Display(reduced: true);

    private void OnExtendedItem(object sender, RoutedEventArgs e) => Display(reduced: false);

    /// <summary>Le choix du menu d'affichage : la page le retient. Recliquer le choix en cours ne le décoche pas.</summary>
    private void Display(bool reduced)
    {
        SetCurrentValue(IsReducedProperty, reduced);
        ShowDisplay();
    }

    private void BuildMoreMenu()
    {
        void Mark(string header, InlineMark mark, Func<EditorTools, bool> on) =>
            More(header, page => page.ToggleMarkCommand, mark, page => page.Tools is { } tools && on(tools));
        Mark(EditingText.Strike, InlineMark.Strike, tools => tools.Strike);
        Mark(EditingText.Superscript, InlineMark.Superscript, tools => tools.Superscript);
        Mark(EditingText.Subscript, InlineMark.Subscript, tools => tools.Subscript);
        Mark(EditingText.InlineCode, InlineMark.Code, tools => tools.Code);
        MoreMenu.Items.Add(new Separator());
        More(EditingText.FontGrow, page => page.StepFontSizeCommand, 1);
        More(EditingText.FontShrink, page => page.StepFontSizeCommand, -1);
        var cases = new MenuItem { Header = EditingText.CaseMenuTitle };
        foreach (var item in CaseMenu.Items.OfType<MenuItem>())
        {
            var copy = new MenuItem { Header = item.Header };
            copy.Click += (_, _) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            cases.Items.Add(copy);
        }
        MoreMenu.Items.Add(cases);
        MoreMenu.Items.Add(new Separator());
        More(EditingText.AlignRight, page => page.SetAlignCommand, TextAlign.Right, page => page.IsAlignRight);
        More(EditingText.AlignJustify, page => page.SetAlignCommand, TextAlign.Justify, page => page.IsAlignJustify);
    }

    private void More(string header, Func<SheetEditorViewModel, System.Windows.Input.ICommand> command, object parameter, Func<SheetEditorViewModel, bool>? isChecked = null)
    {
        var item = new MenuItem { Header = header, IsCheckable = isChecked is not null };
        item.Click += (_, _) =>
        {
            if (DataContext is SheetEditorViewModel page && command(page).CanExecute(parameter))
            {
                command(page).Execute(parameter);
            }
        };
        MoreMenu.Items.Add(item);
        _more.Add((item, isChecked ?? (_ => false), page => command(page).CanExecute(parameter)));
    }

    /// <summary>« Plus » : le menu dit d'abord ce qui est en vigueur sous le curseur, puis s'ouvre sous son bouton.</summary>
    private void OnMoreButton(object sender, RoutedEventArgs e)
    {
        var page = DataContext as SheetEditorViewModel;
        foreach (var (item, isChecked, isEnabled) in _more)
        {
            item.IsChecked = page is not null && isChecked(page);
            item.IsEnabled = page is not null && isEnabled(page);
        }
        OnMenuButton(sender, e);
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

    /// <summary>La taille d'une icône de la barre courte, et le côté de son bouton.</summary>
    internal const double CompactIconSize = 18;

    internal const double CompactButtonSide = 40;

    /// <summary>Dans la barre courte, les icônes sont grandes et leurs boutons faciles à viser.</summary>
    private static void EnlargeCompactIcons(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            switch (child)
            {
                case KyfRev.DesignSystem.Controls.Icon icon when icon.FontSize != 10:
                    icon.FontSize = CompactIconSize;
                    break;
                case System.Windows.Controls.Primitives.ButtonBase button:
                    button.MinHeight = CompactButtonSide;
                    button.MinWidth = Math.Max(button.MinWidth, CompactButtonSide);
                    EnlargeCompactIcons(button);
                    break;
                case System.Windows.Controls.ContextMenu:
                    break;
                default:
                    EnlargeCompactIcons(child);
                    break;
            }
        }
    }

    private void OnImageTab(object sender, RoutedEventArgs e) => ShowTab(ImagePanel);

    private void OnPageTab(object sender, RoutedEventArgs e) => ShowTab(PagePanel);

    /// <summary>La mise en page de la fiche ouverte ; sans elle, l'onglet n'existe pas.</summary>
    public static readonly DependencyProperty PageSetupProperty = DependencyProperty.Register(
        nameof(PageSetup), typeof(Pages.PageSetupViewModel), typeof(EditorRibbon), new PropertyMetadata(null, (ribbon, e) => ((EditorRibbon)ribbon).OnPageSetupChanged(e.NewValue is not null)));

    /// <inheritdoc cref="PageSetupProperty"/>
    public Pages.PageSetupViewModel? PageSetup
    {
        get => (Pages.PageSetupViewModel?)GetValue(PageSetupProperty);
        set => SetValue(PageSetupProperty, value);
    }

    private void OnPageSetupChanged(bool exists)
    {
        PageTab.Visibility = exists ? Visibility.Visible : Visibility.Collapsed;
        if (!exists && PagePanel.Visibility == Visibility.Visible)
        {
            ShowTab(HomePanel);
        }
    }

    /// <summary>Entrée, dans un champ de taille, applique ce qui est tapé, comme en le quittant.</summary>
    private void OnSizeBoxKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && sender is TextBox box)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
        }
    }

    /// <summary>L'image n'est plus choisie : son onglet disparaît, et l'Accueil revient s'il était ouvert.</summary>
    private void OnImageTabShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false && ImageTab.Visibility != Visibility.Visible && ImagePanel.Visibility == Visibility.Visible)
        {
            ShowTab(HomePanel);
        }
    }

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
        // La barre courte n'a que des icônes, en grand : leur nom se lit au survol, et pour un lecteur d'écran.
        FormatLabel.Visibility = Visibility.Collapsed;
        CompactTableLabel.Visibility = Visibility.Collapsed;
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
        ImageTab.IsChecked = shown == ImagePanel;
        PageTab.IsChecked = shown == PagePanel;
        foreach (var panel in new Panel[] { HomePanel, InsertPanel, TablePanel, ImagePanel, PagePanel })
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
        // De même pour l'image qui n'est plus choisie.
        if (ImagePanel.Visibility == Visibility.Visible && ImageTab.Visibility != Visibility.Visible)
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
