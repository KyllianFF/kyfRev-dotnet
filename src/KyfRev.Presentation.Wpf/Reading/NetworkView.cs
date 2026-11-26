using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Schemas;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Un bloc <c>```reseau</c> d'une fiche : le dessin du schéma qu'il cite, et un double clic ouvre son éditeur. Sans le module, le bloc reste
/// intact dans la fiche et la vue le dit : le schéma n'est pas perdu.
/// </summary>
internal sealed class NetworkView : Border
{
    /// <summary>Le contenu du bloc.</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(string), typeof(NetworkView), new PropertyMetadata(null, (sender, _) =>
        {
            var view = (NetworkView)sender;
            view._shownPage = null;
            view.Redraw();
        }));

    /// <summary>Les pages montrées en boutons, au plus ; les autres passent sous « … ».</summary>
    private const int ShownPages = 3;

    private readonly Image _image = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private readonly TextBlock _notice = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _pageButtons = new() { Orientation = Orientation.Horizontal };
    private readonly Border _pages;
    private readonly Grid _picture = new();
    private IReadOnlyList<SchemaPage> _schemaPages = [];
    private string? _shownPage;
    private int _request;

    public NetworkView()
    {
        _notice.SetResourceReference(TextBlock.ForegroundProperty, "Kyf.Brush.TextSecondary");
        // Un schéma à plusieurs pages (30A) : un sélecteur discret, en haut à droite de l'aperçu.
        _pages = new Border
        {
            Child = _pageButtons,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2),
            Margin = new Thickness(6),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
        };
        _pages.SetResourceReference(Border.BackgroundProperty, "Kyf.Brush.Surface");
        _pages.SetResourceReference(Border.BorderBrushProperty, "Kyf.Brush.Divider");
        AutomationProperties.SetName(_pages, SchemaText.PagesName);
        _picture.Children.Add(_image);
        _picture.Children.Add(_pages);
        Child = _notice;
        Loaded += (_, _) => Redraw();
        // Un aperçu qu'on ne touche pas (l'impression) n'a pas de sélecteur : il montre la page choisie.
        IsHitTestVisibleChanged += (_, _) => ShowPages();
    }

    /// <summary>Les pages du schéma, s'il en a plusieurs.</summary>
    internal IReadOnlyList<SchemaPage> Pages => _schemaPages;

    /// <summary>La page dessinée ; rien pour un schéma d'une page.</summary>
    internal string? ShownPage => _shownPage;

    /// <summary>Le sélecteur des pages est montré.</summary>
    internal bool ShowsPageChoice => ShowsPicture && _pages.Visibility == Visibility.Visible;

    /// <summary>Montre une autre page du schéma ; elle reste choisie pour ce schéma (et s'imprime) le temps de la session.</summary>
    internal void ChoosePage(string page)
    {
        if (page == _shownPage || _schemaPages.All(known => known.Id != page))
        {
            return;
        }
        if (NetworkBlock.ReferenceOf(Source) is { } id && TryFindResource(NetworkBlock.ResourceKey) is INetworkSchemas schemas)
        {
            schemas.ChoosePage(id, page);
        }
        _shownPage = page;
        Redraw();
    }

    /// <inheritdoc cref="SourceProperty"/>
    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Le dessin est affiché.</summary>
    internal bool ShowsPicture => Child == _picture;

    /// <summary>Ce qui s'affiche à la place du dessin.</summary>
    internal string Notice => _notice.Text;

    /// <summary>Le dessin s'obtient une fois tout en place, et se refait à chaque retour sur la vue : le schéma a pu changer.</summary>
    internal Task Drawing { get; private set; } = Task.CompletedTask;

    /// <summary>Un double clic ouvre l'éditeur du schéma cité.</summary>
    internal void OpenEditor()
    {
        if (NetworkBlock.ReferenceOf(Source) is { } id && TryFindResource(NetworkBlock.ResourceKey) is INetworkSchemas { IsActive: true } schemas)
        {
            schemas.Open(id);
        }
    }

    /// <inheritdoc/>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        ArgumentNullException.ThrowIfNull(e);
        if (e.ClickCount == 2)
        {
            OpenEditor();
            e.Handled = true;
        }
    }

    private void Redraw()
    {
        if (IsLoaded)
        {
            Drawing = DrawAsync();
        }
    }

    private void Show(string notice)
    {
        _notice.Text = notice;
        AutomationProperties.SetName(this, notice);
        Child = _notice;
    }

    private async Task DrawAsync()
    {
        var request = ++_request;
        if (TryFindResource(NetworkBlock.ResourceKey) is not INetworkSchemas { IsActive: true } schemas)
        {
            Show(SchemaText.ModuleOff);
            return;
        }
        if (string.IsNullOrWhiteSpace(Source))
        {
            Show(SchemaText.EmptyBlock);
            return;
        }
        var text = await NetworkBlock.TextOfAsync(schemas, Source).ConfigureAwait(true);
        if (request != _request)
        {
            return;
        }
        if (text is null || TryFindResource(MermaidEngine.ResourceKey) is not IMermaidEngine engine)
        {
            Show(SchemaText.Unavailable);
            return;
        }
        _schemaPages = NetworkPages.Of(text);
        var reference = NetworkBlock.ReferenceOf(Source);
        var wanted = _shownPage ?? (reference is null ? null : schemas.ChosenPage(reference));
        _shownPage = _schemaPages.Count == 0 ? null : _schemaPages.FirstOrDefault(page => page.Id == wanted)?.Id ?? _schemaPages[0].Id;
        var picture = await engine.DrawNetworkAsync(NetworkPages.Showing(text, _shownPage), SchemaIsland.ThemeOf(this)).ConfigureAwait(true);
        if (request != _request)
        {
            return;
        }
        if (picture is null)
        {
            Show(SchemaText.Unavailable);
            return;
        }
        using var stream = new MemoryStream(picture.Png.ToArray());
        _image.Source = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        _image.MaxWidth = picture.Width;
        var editable = NetworkBlock.ReferenceOf(Source) is not null;
        Cursor = editable ? Cursors.Hand : null;
        ToolTip = editable ? SchemaText.EditHint : SchemaText.InlineHint;
        AutomationProperties.SetName(this, SchemaText.PictureName);
        Child = _picture;
        ShowPages();
    }

    /// <summary>
    /// Le sélecteur des pages : trois au plus en boutons (la page montrée toujours parmi eux), les autres sous « … ». Il ne se
    /// montre que pour un schéma à plusieurs pages, sur un aperçu qu'on peut toucher.
    /// </summary>
    private void ShowPages()
    {
        _pageButtons.Children.Clear();
        if (_schemaPages.Count < 2 || !IsHitTestVisible || !ShowsPicture)
        {
            _pages.Visibility = Visibility.Collapsed;
            return;
        }
        var shown = _schemaPages.Take(ShownPages).ToList();
        if (_schemaPages.FirstOrDefault(page => page.Id == _shownPage) is { } current && !shown.Contains(current))
        {
            shown[^1] = current;
        }
        foreach (var page in shown)
        {
            _pageButtons.Children.Add(PageButton(page));
        }
        if (_schemaPages.Count > shown.Count)
        {
            _pageButtons.Children.Add(OthersButton());
        }
        _pages.Visibility = Visibility.Visible;
    }

    private ToggleButton PageButton(SchemaPage page)
    {
        var button = new ToggleButton { Content = page.Name, IsChecked = page.Id == _shownPage, ToolTip = SchemaText.PageNamed(page.Name) };
        Discreet(button);
        AutomationProperties.SetName(button, SchemaText.PageNamed(page.Name));
        button.Click += (_, _) =>
        {
            button.IsChecked = true;
            ChoosePage(page.Id);
        };
        return button;
    }

    private Button OthersButton()
    {
        var button = new Button { Content = "…", ToolTip = SchemaText.OtherPages };
        Discreet(button);
        AutomationProperties.SetName(button, SchemaText.OtherPages);
        button.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
            foreach (var page in _schemaPages)
            {
                var item = new MenuItem { Header = page.Name, IsCheckable = true, IsChecked = page.Id == _shownPage };
                item.Click += (_, _) => ChoosePage(page.Id);
                menu.Items.Add(item);
            }
            button.ContextMenu = menu;
            menu.IsOpen = true;
        };
        return button;
    }

    /// <summary>Petit, comme un onglet : le sélecteur ne doit pas prendre le pas sur le dessin.</summary>
    private static void Discreet(ButtonBase button)
    {
        button.SetResourceReference(StyleProperty, button is ToggleButton ? "Kyf.Button.Tab" : "Kyf.Button.Subtle");
        button.SetResourceReference(Control.FontSizeProperty, "Kyf.FontSize.Caption");
        button.MinHeight = 26;
        button.MinWidth = 0;
        button.Padding = new Thickness(8, 0, 8, 0);
    }
}
