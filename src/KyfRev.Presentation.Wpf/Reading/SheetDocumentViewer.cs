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
internal sealed class SheetDocumentViewer : FlowDocumentScrollViewer
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
        _hover = new LinkHover(ReadLinkPreview, HideLinkPreview, () => LinkPopup?.IsOpen == true);
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
        SizeChanged += (_, _) => FitTables();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true && _rebuildsWhenShown)
            {
                Rebuild();
            }
            if (e.NewValue is false)
            {
                _hover.Hide();
            }
        };
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

    public override void OnApplyTemplate()
    {
        _scroller?.ScrollChanged -= OnScrollChanged;
        base.OnApplyTemplate();
        _scroller = GetTemplateChild("PART_ContentHost") as ScrollViewer;
        _scroller?.ScrollChanged += OnScrollChanged;
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
        ArgumentNullException.ThrowIfNull(e);
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

    /// <summary>L'aperçu se pose sous le lien, dans la largeur du texte ; le lecteur d'écran l'a comme description du lien.</summary>
    private void ShowLinkPopup(LinkPreviewViewModel preview, Hyperlink link)
    {
        var popup = LinkPopup ??= BuildLinkPopup();
        var target = (FrameworkElement?)_scroller ?? this;
        var place = link.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        popup.DataContext = preview;
        popup.PlacementTarget = target;
        popup.PlacementRectangle = place;
        popup.HorizontalOffset = place.IsEmpty ? 0 : Math.Max(-place.Left, Math.Min(0, target.ActualWidth - LinkPreviewCard.CardWidth - place.Left));
        popup.IsOpen = true;
        Describe(link, preview.Description);
    }

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

    private void OnHolesChanged(object? sender, EventArgs e) => _built?.Layer.SetHoles(Holes?.Hidden ?? []);

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
            return;
        }
        built.Complete();
        holes.Set(TextHoles.Compute(Highlights ?? [], built.BoldRanges, built.ReviewCells));
    }

    /// <summary>Affiche le trou visé par la souris ; rend faux s'il n'y en a pas.</summary>
    internal bool RevealHoleAt(object? source)
    {
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
            _built = new DocumentBuilder(reading, Follow, OpenLinkCommand is not null, ShowsHeader, key => TryFindResource(key) as System.Windows.Media.Brush, TryFindResource(ReadingComfort.ResourceKey) as ReadingComfort) { DrawsMermaid = drawsMermaid, Pictures = TryFindResource(MermaidEngine.ResourceKey) as IMermaidEngine, ExtensionBlocks = TryFindResource(ExtensionBlocks.ResourceKey) as ExtensionBlocks };
            _built.Layer.Set(Highlights ?? [], ActiveHighlight);
            _built.Layer.SetWords(Words ?? []);
            Document = _built.Build();
            FitTables();
            ComputeHoles();
        }
        else
        {
            _built = null;
            Document = null;
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
        var available = ActualWidth - Padding.Left - Padding.Right - SystemParameters.VerticalScrollBarWidth;
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
        // L'aperçu est posé sous un lien qui vient de bouger.
        if (e.VerticalChange != 0)
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
