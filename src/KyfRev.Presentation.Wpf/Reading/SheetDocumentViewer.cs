using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Rendering;
using LinkTarget = KyfRev.Rendering.LinkTarget;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Le texte d'une fiche, en document natif : sélection sur plusieurs blocs, défilement vertical seul.
/// Un lien vers un autre endroit de la fiche s'y suit ; les autres passent par <see cref="OpenLinkCommand"/>.
/// </summary>
internal sealed partial class SheetDocumentViewer : FlowDocumentScrollViewer
{
    /// <summary>Hauteur au-dessus d'un titre amené en haut de la vue.</summary>
    private const double TopMargin = 8;

    // Un titre amené par le plan arrive à TopMargin ; à quelques pixels près, il est encore « en haut de la vue ».
    private const double TopTolerance = 16;

    /// <summary>L'écart entre un lien et son aperçu, que le pointeur franchit sans le perdre.</summary>
    private const double LinkPopupGap = 6;

    public static readonly DependencyProperty ReadingProperty = DependencyProperty.Register(
        nameof(Reading), typeof(SheetReading), typeof(SheetDocumentViewer), new PropertyMetadata(null, OnContentChanged));

    public static readonly DependencyProperty ShowsHeaderProperty = DependencyProperty.Register(
        nameof(ShowsHeader), typeof(bool), typeof(SheetDocumentViewer), new PropertyMetadata(true, OnContentChanged));

    public static readonly DependencyProperty OpenLinkCommandProperty = DependencyProperty.Register(
        nameof(OpenLinkCommand), typeof(ICommand), typeof(SheetDocumentViewer), new PropertyMetadata(null, OnContentChanged));

    public static readonly DependencyProperty KeepsScrollProperty = DependencyProperty.Register(
        nameof(KeepsScroll), typeof(bool), typeof(SheetDocumentViewer));

    public static readonly DependencyProperty HighlightsProperty = DependencyProperty.Register(
        nameof(Highlights), typeof(IReadOnlyList<Highlight>), typeof(SheetDocumentViewer), new PropertyMetadata(null, OnHighlightsChanged));

    public static readonly DependencyProperty ActiveHighlightProperty = DependencyProperty.Register(
        nameof(ActiveHighlight), typeof(string), typeof(SheetDocumentViewer), new PropertyMetadata(null, OnHighlightsChanged));

    public static readonly DependencyProperty WordsProperty = DependencyProperty.Register(
        nameof(Words), typeof(IReadOnlyList<DefinedWordMark>), typeof(SheetDocumentViewer), new PropertyMetadata(null, OnWordsChanged));

    /// <summary>
    /// Ce que fait Ctrl+P quand le clavier est dans le texte : ouvrir la fenêtre « Imprimer » de l'application. Sans
    /// commande, le raccourci ne fait rien ; jamais il n'ouvre la fenêtre d'impression de Windows.
    /// </summary>
    public static readonly DependencyProperty PrintCommandProperty = DependencyProperty.Register(
        nameof(PrintCommand), typeof(ICommand), typeof(SheetDocumentViewer));

    /// <summary>L'aperçu des liens vers une fiche ; rien : ils n'en ont pas.</summary>
    public static readonly DependencyProperty LinkPreviewProperty = DependencyProperty.Register(
        nameof(LinkPreview), typeof(LinkPreviewViewModel), typeof(SheetDocumentViewer), new PropertyMetadata(null, OnLinkPreviewChanged));

    private readonly LinkHover _hover;
    private DocumentBuilder? _built;
    private Hyperlink? _pointed;
    private Hyperlink? _described;
    private Window? _window;

    /// <summary>Le texte à trous.</summary>
    public static readonly DependencyProperty HolesProperty = DependencyProperty.Register(
        nameof(Holes), typeof(TextHoles), typeof(SheetDocumentViewer), new PropertyMetadata(null, OnHolesChanged));

    private ScrollViewer? _scroller;
    private double? _restoreOffset;
    private Block? _spoken;
    private bool _rebuildsWhenShown;

    public SheetDocumentViewer()
    {
        IsToolBarVisible = false;
        MinZoom = 100;
        MaxZoom = 100;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        SetResourceReference(SelectionBrushProperty, "Kyf.Brush.Selection");
        SetResourceReference(PaperProperty, SheetPaper.ResourceKey);
        SetResourceReference(ComfortProperty, ReadingComfort.ResourceKey);
        Template = LayeredTemplate.Value;
        _hover = new LinkHover(ReadLinkPreview, HideLinkPreview, () => LinkPopup?.IsOpen == true, link => !IsMasked(link));
        // Le texte prend pour lui les mouvements de souris et le focus : ces événements-là sont suivis même traités.
        AddHandler(Mouse.MouseMoveEvent, new MouseEventHandler((_, e) => PointAt(e.LeftButton == MouseButtonState.Pressed ? null : e.OriginalSource)), handledEventsToo: true);
        AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) => FocusReached(e.NewFocus)), handledEventsToo: true);
        AddHandler(Keyboard.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) => FocusLeft(e.OldFocus)), handledEventsToo: true);
        // Les traits sous le texte prennent leur couleur du thème, connu une fois la vue en place.
        Loaded += (_, _) =>
        {
            ApplyHighlights();
            WatchWindow(Window.GetWindow(this));
        };
        Unloaded += (_, _) =>
        {
            _hover.Hide();
            WatchWindow(null);
        };
        SizeChanged += (_, e) =>
        {
            if (e.WidthChanged && Layout is not { IsPaged: true })
            {
                // La colonne de texte d'une feuille sans fin reste centrée dans la vue.
                ApplyPagePadding();
            }
            FitTables();
            RestartPages();
        };
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true && _rebuildsWhenShown)
            {
                Rebuild();
            }
            if (e.NewValue is true)
            {
                SchedulePages();
            }
            if (e.NewValue is false)
            {
                _hover.Hide();
            }
        };
    }

    /// <inheritdoc cref="PrintCommandProperty"/>
    public ICommand? PrintCommand
    {
        get => (ICommand?)GetValue(PrintCommandProperty);
        set => SetValue(PrintCommandProperty, value);
    }

    /// <summary>
    /// Ctrl+P, que le texte prend pour lui avant la page : la vue d'origine ouvrirait ici la fenêtre d'impression de
    /// Windows, sans aperçu ni mise en page. C'est celle de l'application qui s'ouvre.
    /// </summary>
    protected override void OnPrintCommand()
    {
        if (PrintCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    /// <inheritdoc cref="LinkPreviewProperty"/>
    public LinkPreviewViewModel? LinkPreview
    {
        get => (LinkPreviewViewModel?)GetValue(LinkPreviewProperty);
        set => SetValue(LinkPreviewProperty, value);
    }

    /// <summary>La fenêtre de l'aperçu d'un lien ; rien avant le premier.</summary>
    internal Popup? LinkPopup { get; private set; }

    /// <summary>Le lien dont l'aperçu est montré, ou en cours de lecture.</summary>
    internal Hyperlink? PreviewedLink => _hover.Current;

    /// <summary>Le lien vers une fiche que la souris vise.</summary>
    internal Hyperlink? PointedLink => _pointed;

    /// <summary>L'aperçu partira bientôt, faute de pointeur.</summary>
    internal bool IsHidingPreview => _hover.IsHiding;

    /// <inheritdoc cref="HolesProperty"/>
    public TextHoles? Holes
    {
        get => (TextHoles?)GetValue(HolesProperty);
        set => SetValue(HolesProperty, value);
    }

    /// <summary>La fiche à montrer.</summary>
    public SheetReading? Reading
    {
        get => (SheetReading?)GetValue(ReadingProperty);
        set => SetValue(ReadingProperty, value);
    }

    /// <summary>Le titre et les étiquettes de la fiche ouvrent le texte.</summary>
    public bool ShowsHeader
    {
        get => (bool)GetValue(ShowsHeaderProperty);
        set => SetValue(ShowsHeaderProperty, value);
    }

    /// <summary>Ouvre un lien vers l'extérieur ou vers une autre fiche ; sans elle, ces liens restent du texte.</summary>
    public ICommand? OpenLinkCommand
    {
        get => (ICommand?)GetValue(OpenLinkCommandProperty);
        set => SetValue(OpenLinkCommandProperty, value);
    }

    /// <summary>Refaire le document garde l'endroit affiché : celui d'un rendu qu'on écrit.</summary>
    public bool KeepsScroll
    {
        get => (bool)GetValue(KeepsScrollProperty);
        set => SetValue(KeepsScrollProperty, value);
    }

    /// <summary>Les passages à surligner dans le texte.</summary>
    public IReadOnlyList<Highlight>? Highlights
    {
        get => (IReadOnlyList<Highlight>?)GetValue(HighlightsProperty);
        set => SetValue(HighlightsProperty, value);
    }

    /// <summary>L'annotation qui ressort.</summary>
    public string? ActiveHighlight
    {
        get => (string?)GetValue(ActiveHighlightProperty);
        set => SetValue(ActiveHighlightProperty, value);
    }

    /// <summary>Les mots définis ailleurs dans le coffre, à marquer dans le texte.</summary>
    public IReadOnlyList<DefinedWordMark>? Words
    {
        get => (IReadOnlyList<DefinedWordMark>?)GetValue(WordsProperty);
        set => SetValue(WordsProperty, value);
    }

    /// <summary>Se termine quand les images du document sont chargées.</summary>
    public Task ImagesLoaded => _built?.Images ?? Task.CompletedTask;

    /// <summary>Il reste du contenu à ajouter ou des formules à dessiner.</summary>
    public bool HasPendingWork => _built?.HasPendingWork == true;

    /// <summary>
    /// Le gabarit de la vue : la zone qui défile, entre la couche des feuilles et celle des écarts, et la place de la barre de
    /// recherche du texte.
    /// </summary>
    private static readonly Lazy<ControlTemplate> LayeredTemplate = new(() => (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
        """
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="FlowDocumentScrollViewer">
          <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <Grid>
              <Grid.RowDefinitions>
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
              </Grid.RowDefinitions>
              <Grid x:Name="PART_Layers">
                <ScrollViewer x:Name="PART_ContentHost" CanContentScroll="True" />
              </Grid>
              <Border x:Name="PART_FindToolBarHost" Grid.Row="1" Visibility="Collapsed" HorizontalAlignment="Left" />
            </Grid>
          </Border>
        </ControlTemplate>
        """));

    public override void OnApplyTemplate()
    {
        _scroller?.ScrollChanged -= OnScrollChanged;
        base.OnApplyTemplate();
        _scroller = GetTemplateChild("PART_ContentHost") as ScrollViewer;
        _scroller?.ScrollChanged += OnScrollChanged;
        // Les feuilles se dessinent sous le texte, et l'écart entre elles par-dessus : les deux couches entourent la zone qui défile.
        if (GetTemplateChild("PART_Layers") is Grid layers)
        {
            (PagePaper.Parent as Panel)?.Children.Remove(PagePaper);
            (PageBands.Parent as Panel)?.Children.Remove(PageBands);
            layers.Children.Insert(0, PagePaper);
            layers.Children.Add(PageBands);
        }
    }

    /// <summary>La souris sort du texte.</summary>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        PointAt(null);
    }

    /// <summary>Un clic dans le texte referme l'aperçu.</summary>
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        _hover.Hide();
        base.OnPreviewMouseDown(e);
    }

    /// <summary>Échap referme l'aperçu d'abord.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _hover.Current is not null)
        {
            _hover.Hide();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }

    /// <summary>La souris vise <paramref name="source"/> : ce qu'elle a sous elle dans le texte, ou rien hors de lui.</summary>
    internal void PointAt(object? source)
    {
        if (LinkPreview is null)
        {
            return;
        }
        var link = SheetLinkAt(source);
        if (ReferenceEquals(link, _pointed))
        {
            return;
        }
        if (_pointed is not null)
        {
            _hover.Leave();
        }
        _pointed = link;
        if (link is not null)
        {
            _hover.Enter(link, atOnce: false);
        }
    }

    /// <summary>Le focus arrive sur <paramref name="element"/> : un lien vers une fiche montre son aperçu sans attendre.</summary>
    internal void FocusReached(object? element)
    {
        if (LinkPreview is not null && SheetLinkAt(element) is { } link)
        {
            _hover.Enter(link, atOnce: true);
        }
    }

    /// <summary>Le focus quitte <paramref name="element"/>.</summary>
    internal void FocusLeft(object? element)
    {
        if (LinkPreview is not null && SheetLinkAt(element) is not null)
        {
            _hover.Leave();
        }
    }

    /// <summary>Le lien vers une fiche qui contient <paramref name="source"/> ; rien s'il n'y en a pas.</summary>
    private static Hyperlink? SheetLinkAt(object? source)
    {
        for (var element = source as DependencyObject; element is not null and not FlowDocument; element = LogicalTreeHelper.GetParent(element))
        {
            if (element is Hyperlink { Tag: SheetLink } link)
            {
                return link;
            }
        }
        return null;
    }

    private static void OnLinkPreviewChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (SheetDocumentViewer)sender;
        viewer._hover.Hide();
        if (e.OldValue is LinkPreviewViewModel old)
        {
            old.PropertyChanged -= viewer.OnLinkPreviewContentChanged;
        }
        if (e.NewValue is LinkPreviewViewModel preview)
        {
            preview.PropertyChanged += viewer.OnLinkPreviewContentChanged;
        }
    }

    /// <summary>L'aperçu s'est vidé (la fiche a été ouverte, ou il n'y en a pas) : sa fenêtre se referme.</summary>
    private void OnLinkPreviewContentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is LinkPreviewViewModel { IsOpen: false })
        {
            _hover.Hide();
        }
    }

    private void ReadLinkPreview(Hyperlink link)
    {
        if (LinkPreview is { } preview && link.Tag is SheetLink target)
        {
            _ = ReadLinkPreviewAsync(preview, link, target);
        }
    }

    private async Task ReadLinkPreviewAsync(LinkPreviewViewModel preview, Hyperlink link, SheetLink target)
    {
        // Le pointeur est peut-être parti pendant la lecture, ou passé à un autre lien.
        if (!await preview.ShowAsync(target.Path).ConfigureAwait(true) || !ReferenceEquals(link, _hover.Current))
        {
            return;
        }
        if (preview.IsOpen)
        {
            ShowLinkPopup(preview, link);
        }
        else
        {
            _hover.Hide();
        }
    }

    /// <summary>Le lecteur d'écran a l'aperçu comme description du lien.</summary>
    private void ShowLinkPopup(LinkPreviewViewModel preview, Hyperlink link)
    {
        var popup = LinkPopup ??= BuildLinkPopup();
        popup.DataContext = preview;
        Place(popup, link);
        popup.IsOpen = true;
        Describe(link, preview.Description);
    }

    /// <summary>L'aperçu se pose sous le lien, dans la largeur du texte.</summary>
    private void Place(Popup popup, Hyperlink link)
    {
        var target = (FrameworkElement?)_scroller ?? this;
        var place = link.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        popup.PlacementTarget = target;
        popup.PlacementRectangle = place;
        popup.HorizontalOffset = place.IsEmpty ? 0 : Math.Max(-place.Left, Math.Min(0, target.ActualWidth - LinkPreviewCard.CardWidth - place.Left));
    }

    /// <summary>
    /// Le focus amène son lien à l'écran, et le défilement n'arrive qu'après le début de l'aperçu : celui-ci reste, posé sous
    /// le lien à sa nouvelle place, tant que le lien se voit.
    /// </summary>
    private bool FollowsFocusedLink(double viewportHeight)
    {
        if (_hover.Current is not { IsKeyboardFocused: true } link)
        {
            return false;
        }
        var place = link.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        if (place.IsEmpty || place.Bottom <= 0 || place.Top >= viewportHeight)
        {
            return false;
        }
        if (LinkPopup is { IsOpen: true } popup)
        {
            Place(popup, link);
        }
        return true;
    }

    /// <summary>Un trou encore masqué couvre une partie de cet élément : l'aperçu d'un lien masqué donnerait la réponse.</summary>
    private bool IsMasked(DependencyObject element) =>
        _built?.Layer.HoleAt(element) is not null
        || LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>().Any(IsMasked);

    private void Describe(Hyperlink? link, string? description)
    {
        _described?.ClearValue(AutomationProperties.HelpTextProperty);
        _described = link;
        _described?.SetValue(AutomationProperties.HelpTextProperty, description);
    }

    private Popup BuildLinkPopup()
    {
        var card = new LinkPreviewCard();
        card.MouseEnter += (_, _) => _hover.Keep();
        card.MouseLeave += (_, _) => _hover.Release();
        return new Popup { Child = card, Placement = PlacementMode.Bottom, VerticalOffset = LinkPopupGap, AllowsTransparency = true, StaysOpen = true };
    }

    private void HideLinkPreview()
    {
        LinkPreview?.Hide();
        LinkPopup?.IsOpen = false;
        Describe(null, null);
    }

    /// <summary>Déplacer la fenêtre ou la quitter referme l'aperçu : il n'en fait pas partie, et resterait à sa place.</summary>
    private void WatchWindow(Window? window)
    {
        _window?.Deactivated -= OnWindowLeft;
        _window?.LocationChanged -= OnWindowLeft;
        _window = window;
        _window?.Deactivated += OnWindowLeft;
        _window?.LocationChanged += OnWindowLeft;
    }

    private void OnWindowLeft(object? sender, EventArgs e) => _hover.Hide();

    /// <summary>Amène un titre en haut de la vue.</summary>
    public void Show(RenderElement heading)
    {
        _built?.Complete();
        if (_built is not null && _built.Headings.TryGetValue(heading, out var paragraph))
        {
            ShowAtTop(paragraph);
        }
    }

    /// <summary>Amène en haut de la vue l'endroit d'une position du texte affiché.</summary>
    public void Show(int position)
    {
        if (_built is null || Reading is null)
        {
            return;
        }
        _built.Complete();
        if (_built.Layer.ElementAt(position) is { } element)
        {
            ShowAtTop(element);
        }
    }

    /// <summary>Ce qui se lit à voix haute : tout le texte se termine d'abord.</summary>
    internal IReadOnlyList<SpokenBlock> SpokenBlocks() => _built?.SpokenBlocks() ?? [];

    /// <summary>Met en avant le bloc lu à voix haute, et l'amène au milieu de la vue ; rien pour n'en mettre aucun.</summary>
    internal void MarkSpoken(Block? block)
    {
        _spoken?.ClearValue(TextElement.BackgroundProperty);
        _spoken = block;
        if (block is null)
        {
            return;
        }
        block.SetResourceReference(TextElement.BackgroundProperty, "Kyf.Brush.PillSheetBackground");
        if (_scroller is { } scroller)
        {
            UpdateLayout();
            var top = block.ContentStart.GetCharacterRect(LogicalDirection.Forward).Top;
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset + top - (scroller.ViewportHeight / 2));
        }
    }

    /// <summary>Sélectionne un texte, en positions du texte affiché, et le fait venir à l'écran ; faux s'il n'est pas montré.</summary>
    public bool Select(int start, int end)
    {
        if (_built is null || Reading is null)
        {
            return false;
        }
        _built.Complete();
        if (_built.Layer.PointerAt(start, isStart: true) is not { } from || _built.Layer.PointerAt(end, isStart: false) is not { } to)
        {
            return false;
        }
        Selection?.Select(from, to);
        Show(start);
        return true;
    }

    /// <summary>La sélection dans le texte affiché, sans ses espaces de bord ; rien si elle n'y mène à rien.</summary>
    public TextPosition? CurrentSelection()
    {
        if (_built is null || Reading is null || Selection is not { IsEmpty: false } selection)
        {
            return null;
        }
        var start = _built.Layer.PositionOf(selection.Start, isStart: true);
        var end = _built.Layer.PositionOf(selection.End, isStart: false);
        return start is { } from && end is { } to ? Anchoring.Trim(Reading.Map.Text, from, to) : null;
    }

    /// <summary>Les annotations surlignées à l'endroit visé par la souris.</summary>
    public IReadOnlyList<string> AnnotationsAt(object? source) => _built?.Layer.IdsAt(source) ?? [];

    /// <summary>Le mot défini ailleurs, marqué à l'endroit visé par la souris ; rien s'il n'y en a pas.</summary>
    public DefinedWordMark? WordAt(object? source) => _built?.Layer.WordAt(source);

    private static void OnWordsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (SheetDocumentViewer)sender;
        viewer._built?.Layer.SetWords(viewer.Words ?? []);
    }

    private static void OnContentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((SheetDocumentViewer)sender).Rebuild();

    private static void OnHighlightsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((SheetDocumentViewer)sender).ApplyHighlights();

    private void ApplyHighlights()
    {
        _built?.Layer.Set(Highlights ?? [], ActiveHighlight);
        ComputeHoles();
    }

    private static void OnHolesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (SheetDocumentViewer)sender;
        if (e.OldValue is TextHoles old)
        {
            old.Changed -= viewer.OnHolesChanged;
            old.PropertyChanged -= viewer.OnHolesActivated;
        }
        if (e.NewValue is TextHoles holes)
        {
            holes.Changed += viewer.OnHolesChanged;
            holes.PropertyChanged += viewer.OnHolesActivated;
        }
        viewer.ComputeHoles();
    }

    /// <summary>Les trous n'existent pas en lecture : ils se calculent quand la révision commence.</summary>
    private void OnHolesActivated(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TextHoles.IsActive))
        {
            ComputeHoles();
        }
    }

    private void OnHolesChanged(object? sender, EventArgs e)
    {
        _built?.Layer.SetHoles(Holes?.Hidden ?? []);
        ShowMasks();
        // Un trou vient de se poser sur le lien dont l'aperçu est montré.
        if (_hover.Current is { } link && IsMasked(link))
        {
            _hover.Hide();
        }
    }

    /// <summary>Les trous dépendent de tout le texte : il se termine d'abord.</summary>
    private void ComputeHoles()
    {
        if (_built is not { } built)
        {
            return;
        }
        if (Holes is not { IsActive: true } holes)
        {
            built.Layer.SetHoles([]);
            ShowMasks();
            return;
        }
        built.Complete();
        // Les masques des images sont des trous comme les autres, à la place de leur image dans le texte.
        var masks = built.MaskedImages.SelectMany(image => Enumerable.Range(0, image.Overlay.Masks.Length)
            .Select(mask => new TextHole(ImageMaskOverlay.KeyOf(image.Overlay.ImageIndex, mask), image.Position, image.Position, null)));
        var marked = TextHoles.Compute(Highlights ?? [], built.BoldRanges, built.ReviewCells).Concat(masks).OrderBy(hole => hole.Start);
        // Sans gras ni passage, la fiche se relirait simplement : ses parties se rappellent alors une à une, sous leur titre.
        holes.Set(marked.Any() ? [.. marked] : TextHoles.Sections(built.HeadingRanges, built.TextLength));
    }

    /// <summary>Pose sur chaque image les masques encore cachés ; hors révision, aucun.</summary>
    private void ShowMasks()
    {
        if (_built is not { } built)
        {
            return;
        }
        var hidden = Holes is { IsActive: true } holes ? holes.Hidden.Select(hole => hole.Key).ToHashSet(StringComparer.Ordinal) : [];
        foreach (var (overlay, _) in built.MaskedImages)
        {
            overlay.SetHidden(Enumerable.Range(0, overlay.Masks.Length).Where(mask => hidden.Contains(ImageMaskOverlay.KeyOf(overlay.ImageIndex, mask))));
        }
    }

    /// <summary>Les caches des images masquées de la fiche, dans l'ordre.</summary>
    internal IReadOnlyList<ImageMaskOverlay> MaskOverlays => _built is { } built ? [.. built.MaskedImages.Select(image => image.Overlay)] : [];

    /// <summary>Affiche le trou visé par la souris ; rend faux s'il n'y en a pas.</summary>
    internal bool RevealHoleAt(object? source)
    {
        if (source is ImageMaskOverlay overlay)
        {
            if (overlay.MaskAt(Mouse.GetPosition(overlay)) is not { } mask)
            {
                return false;
            }
            Holes?.Reveal(ImageMaskOverlay.KeyOf(overlay.ImageIndex, mask));
            return true;
        }
        if (_built?.Layer.HoleAt(source) is not { } hole)
        {
            return false;
        }
        Holes?.Reveal(hole.Key);
        return true;
    }

    /// <summary>Affiche le premier trou encore masqué, et l'amène à l'écran ; rend faux s'il n'y en a pas.</summary>
    internal bool RevealNextHole()
    {
        if (Holes is not { IsActive: true } holes || holes.RevealNext() is not { } hole)
        {
            return false;
        }
        _built?.Layer.ElementAt(hole.Start)?.BringIntoView();
        return true;
    }

    /// <summary>
    /// Le confort de lecture (taille, interligne, police), trouvé dans les ressources : quand il change, le texte montré se
    /// refait avec lui.
    /// </summary>
    private static readonly DependencyProperty ComfortProperty = DependencyProperty.Register(
        "Comfort", typeof(ReadingComfort), typeof(SheetDocumentViewer), new PropertyMetadata(null, (viewer, e) => ((SheetDocumentViewer)viewer).FollowComfort(e.NewValue as ReadingComfort)));

    /// <summary>Le confort avec lequel le document montré a été construit.</summary>
    private ReadingComfort? _builtComfort;

    private void FollowComfort(ReadingComfort? comfort)
    {
        if (Document is not null && (comfort ?? ReadingComfort.Default) != _builtComfort)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        // Un document caché ne se construit pas : l'interface a mieux à faire, et il se construira s'il se montre.
        _rebuildsWhenShown = IsLoaded && !IsVisible;
        if (_rebuildsWhenShown)
        {
            return;
        }
        _spoken = null;
        _hover.Hide();
        _pointed = null;
        _restoreOffset = KeepsScroll && _scroller is { VerticalOffset: > 0 } scroller ? scroller.VerticalOffset : null;
        _built?.Cancel();
        if (Reading is { } reading)
        {
            var drawsMermaid = TryFindResource(MermaidEngine.ResourceKey) is not null;
            // Sur un papier réglé, les lignes du texte prennent le pas du papier.
            _step = StepOf(PaperShown);
            _builtComfort = TryFindResource(ReadingComfort.ResourceKey) as ReadingComfort ?? ReadingComfort.Default;
            var comfort = _builtComfort with { Step = _step };
            _built = new DocumentBuilder(reading, Follow, OpenLinkCommand is not null, ShowsHeader, key => TryFindResource(key) as System.Windows.Media.Brush, comfort) { DrawsMermaid = drawsMermaid, Pictures = TryFindResource(MermaidEngine.ResourceKey) as IMermaidEngine, ExtensionBlocks = TryFindResource(ExtensionBlocks.ResourceKey) as ExtensionBlocks };
            _built.Layer.Set(Highlights ?? [], ActiveHighlight);
            _built.Layer.SetWords(Words ?? []);
            Document = _built.Build();
            ForgetPages();
            FitTables();
            ComputeHoles();
        }
        else
        {
            _built = null;
            Document = null;
            ForgetPages();
        }
    }

    private void Follow(LinkTarget target)
    {
        if (target is PageAnchor anchor)
        {
            _built?.Complete();
            if (_built is not null && _built.Identified.TryGetValue(anchor.Identifier, out var element))
            {
                ShowAtTop(element);
            }
        }
        else
        {
            OpenLinkCommand?.Execute(target);
        }
    }

    /// <summary>
    /// Les tableaux pleine largeur se partagent la largeur réelle du texte : chaque colonne garde son plus long mot tant que le
    /// tableau le permet. Des proportions d'étoiles ne le peuvent pas seules : une fenêtre étroite les resserrait toutes d'autant.
    /// </summary>
    internal void FitTables()
    {
        if (Document is not { } document || ActualWidth <= 0)
        {
            return;
        }
        var available = ActualWidth - Padding.Left - Padding.Right - SystemParameters.VerticalScrollBarWidth - (Layout is { IsPaged: true } ? 0 : 2 * EndlessSide);
        foreach (var table in Tables(document))
        {
            if (table.Tag is not TableSizing sizing)
            {
                continue;
            }
            var widths = sizing.For(available - table.Margin.Left - table.Margin.Right);
            for (var column = 0; column < widths.Count && column < table.Columns.Count; column++)
            {
                var width = new GridLength(widths[column] / DocumentBuilder.StarUnit, GridUnitType.Star);
                if (table.Columns[column].Width != width)
                {
                    table.Columns[column].Width = width;
                }
            }
        }
    }

    private static IEnumerable<Table> Tables(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is Table table)
            {
                yield return table;
            }
            foreach (var inner in Tables(child))
            {
                yield return inner;
            }
        }
    }

    private void ShowAtTop(TextElement element)
    {
        if (_scroller is null)
        {
            element.BringIntoView();
            return;
        }
        UpdateLayout();
        var top = element.ContentStart.GetCharacterRect(LogicalDirection.Forward).Top;
        _scroller.ScrollToVerticalOffset(_scroller.VerticalOffset + top - TopMargin);
    }

    /// <summary>
    /// La partie en cours : le dernier titre arrivé en haut de la vue, et le premier tant qu'aucun n'y est. Tout en bas de la
    /// fiche, les derniers titres ne peuvent plus monter : le dernier titre visible est alors la partie en cours.
    /// </summary>
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Le texte a grandi (un morceau de plus, une image arrivée) ou la vue a changé de taille : les pages se refont.
        if (e.ExtentHeightChange != 0)
        {
            FollowWholeHeight();
        }
        if (e.ExtentHeightChange != 0 || e.ViewportHeightChange != 0 || e.ViewportWidthChange != 0)
        {
            SchedulePages();
        }
        if (e.VerticalChange != 0 || e.ExtentHeightChange != 0 || e.ViewportHeightChange != 0 || e.ViewportWidthChange != 0)
        {
            UpdateBands();
        }
        // L'aperçu est posé sous un lien qui vient de bouger ; seul celui du focus le suit.
        if (e.VerticalChange != 0 && !FollowsFocusedLink(e.ViewportHeight))
        {
            _hover.Hide();
        }
        // Le document se remplit par morceaux : l'endroit gardé revient dès qu'il existe.
        if (_restoreOffset is { } offset && _scroller is { } scroller && (e.ExtentHeight >= offset + e.ViewportHeight || !HasPendingWork))
        {
            _restoreOffset = null;
            scroller.ScrollToVerticalOffset(offset);
        }
        if (_built is null || Reading is null || Reading.Outline.Count == 0)
        {
            return;
        }
        var atEnd = e.ExtentHeight > e.ViewportHeight && e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 1 && !HasPendingWork;
        var line = atEnd ? ActualHeight : TopMargin + TopTolerance;
        OutlineEntry? current = null;
        foreach (var entry in Reading.Outline)
        {
            if (!_built.Headings.TryGetValue(entry.Heading, out var paragraph))
            {
                continue;
            }
            if (current is not null && paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward).Top > line)
            {
                break;
            }
            current = entry;
        }
        foreach (var entry in Reading.Outline)
        {
            entry.IsActive = ReferenceEquals(entry, current);
        }
    }
}
