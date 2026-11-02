using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KyfRev.Application;
using KyfRev.Application.Ports;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Rendering.Diagrams;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// Le texte mis en forme, qui se modifie à sa place : ProseMirror dans WebView2. La page ne vient que de
/// l'application, ne navigue nulle part, n'a ni outils de développement ni objet de l'hôte, et ne parle que par
/// le bus de messages. Une fiche piégée n'y exécute rien : son HTML n'est que du texte.
/// </summary>
internal sealed class VisualEditor : UserControl, IEditorSurface, IDisposable
{
    /// <summary>La page servie : un nom réservé, qui ne mène à rien hors de l'application.</summary>
    internal const string Host = "editeur.kyfrev.invalid";

    private const string StartAddress = $"https://{Host}/index.html";
    private const string ResourcePrefix = "kyfrev-web/";
    private const string SecurityPolicy = "default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    private static readonly TimeSpan SyncPatience = TimeSpan.FromSeconds(2);

    /// <summary>Les variables d'environnement qui ouvriraient un port de débogage ou changeraient de moteur.</summary>
    private static readonly string[] NeutralizedVariables =
    [
        "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
        "WEBVIEW2_BROWSER_EXECUTABLE_FOLDER",
        "WEBVIEW2_USER_DATA_FOLDER",
        "WEBVIEW2_RELEASE_CHANNEL_PREFERENCE",
        "WEBVIEW2_PIPE_FOR_SCRIPT_DEBUGGER",
        "WEBVIEW2_WAIT_FOR_SCRIPT_DEBUGGER",
    ];

    private static readonly (string Variable, string Resource)[] ThemeColors =
    [
        ("--fond", "Kyf.Brush.Background"),
        ("--surface", "Kyf.Brush.Surface"),
        ("--surface-2", "Kyf.Brush.SurfaceAlt"),
        ("--bordure", "Kyf.Brush.Divider"),
        ("--texte", "Kyf.Brush.Text"),
        ("--texte-secondaire", "Kyf.Brush.TextSecondary"),
        ("--actif-fond", "Kyf.Brush.NavigationActiveBackground"),
        ("--actif-texte", "Kyf.Brush.NavigationActiveText"),
        ("--pastille-fond", "Kyf.Brush.PillPassageBackground"),
        ("--pastille-texte", "Kyf.Brush.PillPassageText"),
        ("--lien", "Kyf.Brush.Link"),
        ("--principale", "Kyf.Brush.Primary"),
        ("--focus", "Kyf.Brush.Focus"),
        ("--selection", "Kyf.Brush.Selection"),
        ("--sur-selection", "Kyf.Brush.OnSelection"),
        ("--surlignage", "Kyf.Brush.Highlight"),
        ("--surlignage-actif", "Kyf.Brush.HighlightActive"),
        ("--retard", "Kyf.Brush.Critical"),
        ("--avertissement", "Kyf.Brush.Warning"),
        ("--avertissement-fond", "Kyf.Brush.WarningBackground"),
        ("--succes", "Kyf.Brush.Success"),
        ("--repere-rappel", "Kyf.Brush.MarkerReminder"),
        ("--repere-passage", "Kyf.Brush.MarkerPassage"),
        ("--pastille-fiche-fond", "Kyf.Brush.PillSheetBackground"),
        ("--pastille-passage-fond", "Kyf.Brush.PillPassageBackground"),
    ];

    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register(
        "ThemeProbe", typeof(Brush), typeof(VisualEditor), new PropertyMetadata(null, (sender, _) => ((VisualEditor)sender).QueueTheme()));

    /// <summary>Un seul par fil : l'objet COM ne se partage pas entre deux fils d'interface.</summary>
    [ThreadStatic]
    private static Task<CoreWebView2Environment>? _environment;

    private readonly WebView2 _view = new();
    private readonly Image _snapshot = new() { Visibility = Visibility.Collapsed, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Func<string, Outcome<byte[]>> _readResource;
    private readonly string _dataFolder;
    private readonly string _folder;
    private readonly ISpelling _spelling;
    private readonly Queue<EditorRequest> _pending = new();
    private readonly Dictionary<int, TaskCompletionSource> _syncs = [];
    private bool _ready;
    private bool _stale;
    private bool _themeQueued;
    private int _lastSync;
    private bool _covered;
    private bool _configured;
    private bool _disposed;

    public VisualEditor(string folder, Func<string, Outcome<byte[]>> readResource, ISpelling spelling, string dataFolder)
    {
        _folder = folder;
        _readResource = readResource;
        _spelling = spelling;
        _dataFolder = dataFolder;
        Text = string.Empty;
        Focusable = false;
        IsTabStop = false;
        System.Windows.Automation.AutomationProperties.SetAutomationId(_view, "VisualText");
        System.Windows.Automation.AutomationProperties.SetName(_view, EditingText.VisualText);
        _snapshot.MouseLeftButtonDown += (_, _) => SnapshotClicked?.Invoke(this, EventArgs.Empty);
        Content = new Grid { Children = { _view, _snapshot } };
        SetResourceReference(ThemeProbeProperty, "Kyf.Brush.Text");
        Loaded += OnLoaded;
    }

    /// <inheritdoc/>
    public event EventHandler? TextEdited;

    /// <inheritdoc/>
    public event EventHandler<EditorTools>? ToolsChanged;

    /// <inheritdoc/>
    public event EventHandler<LinkInput?>? LinkInputChanged;

    /// <inheritdoc/>
    public event EventHandler<LinkQuestion>? LinkAsked;

    /// <inheritdoc/>
    public event EventHandler<FormulaQuestion>? FormulaAsked;

    /// <inheritdoc/>
    public event EventHandler<DiagramQuestion>? DiagramAsked;

    /// <inheritdoc/>
    public event EventHandler? SaveAsked;

    /// <inheritdoc/>
    public event EventHandler<Key>? ListKeyPressed;

    /// <summary>Une image est collée ou déposée ; sans contenu, elle est trop lourde.</summary>
    public event Action<string, byte[]?, int?>? ImageDropped;

    /// <summary>Le moteur n'a pas pu démarrer, ou s'est arrêté.</summary>
    public event EventHandler<string>? Failed;

    /// <summary>Un clic sur l'image du texte, pendant qu'un volet le recouvre.</summary>
    public event EventHandler? SnapshotClicked;

    /// <summary>La page est prête et a reçu ce qu'elle attendait.</summary>
    public bool IsReady => _ready;

    /// <inheritdoc/>
    public string Text { get; private set; }

    /// <inheritdoc/>
    public Point InputPoint { get; private set; }

    /// <summary>Les éléments du dernier menu demandé par la page.</summary>
    internal IReadOnlyList<Control> LastMenuItems { get; private set; } = [];

    /// <summary>Le moteur, pour les essais.</summary>
    internal WebView2 View => _view;

    /// <summary>Le texte est recouvert, et remplacé par son image.</summary>
    internal bool IsCovered => _covered;

    /// <summary>
    /// Le moteur a sa propre fenêtre, toujours au premier plan : pendant qu'un dialogue ou un volet de l'application
    /// le recouvre, son image prend sa place, et rien ne se voit du changement.
    /// </summary>
    internal async Task SetCoveredAsync(bool covered)
    {
        if (covered == _covered || _disposed)
        {
            return;
        }
        _covered = covered;
        if (covered)
        {
            await ShowSnapshotAsync().ConfigureAwait(true);
            if (_covered && !_disposed)
            {
                _view.Visibility = Visibility.Hidden;
            }
        }
        else
        {
            _snapshot.Visibility = Visibility.Collapsed;
            _snapshot.Source = null;
            _view.Visibility = Visibility.Visible;
            Post(new FocusRequest());
        }
    }

    /// <summary>La taille de l'interface vaut aussi pour le texte.</summary>
    internal void SetZoom(double zoom)
    {
        if (!_disposed)
        {
            _view.ZoomFactor = zoom;
        }
    }

    /// <summary>Met l'image du texte à sa place, pour les captures.</summary>
    internal async Task ShowSnapshotAsync()
    {
        if (_configured)
        {
            using var stream = new MemoryStream();
            await _view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream).ConfigureAwait(true);
            stream.Position = 0;
            _snapshot.Source = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            _snapshot.Width = ActualWidth;
            _snapshot.Height = ActualHeight;
            _snapshot.Stretch = Stretch.Fill;
            _snapshot.Visibility = Visibility.Visible;
        }
    }

    /// <inheritdoc/>
    public async Task SyncAsync()
    {
        if (!_ready || !_stale)
        {
            return;
        }
        var id = ++_lastSync;
        var answered = new TaskCompletionSource();
        _syncs[id] = answered;
        Post(new SyncRequest(id));
        await Task.WhenAny(answered.Task, Task.Delay(SyncPatience)).ConfigureAwait(true);
        _syncs.Remove(id);
    }

    /// <inheritdoc/>
    public void ReplaceAll(string text)
    {
        Text = text;
        _stale = false;
        Post(new LoadRequest(text, _folder));
    }

    /// <inheritdoc/>
    public void ToggleMark(InlineMark mark) => Post(new MarkRequest(mark));

    /// <inheritdoc/>
    public void SetHeading(int level) => Post(new HeadingRequest(level));

    /// <inheritdoc/>
    public void ToggleList(ListKind kind) => Post(new ListRequest(kind));

    /// <inheritdoc/>
    public void InsertDiagram(string info, string code, int? position) => Post(new InsertDiagramRequest(info, code, position));

    /// <inheritdoc/>
    public void SetColor(string? color) => Post(new ColorRequest(color));

    /// <inheritdoc/>
    public void SetFontSize(string? size) => Post(new SizeRequest(size));

    /// <inheritdoc/>
    public void SetAlign(TextAlign align) => Post(new AlignRequest(align));

    /// <inheritdoc/>
    public void ChangeCase(CaseStyle? style) => Post(new CaseRequest(style));

    /// <inheritdoc/>
    public void InsertTable(int rows, int columns) => Post(new TableRequest(rows, columns));

    /// <inheritdoc/>
    public void InsertCode(string language) => Post(new CodeRequest(language));

    /// <inheritdoc/>
    public void InsertRule() => Post(new RuleRequest());

    /// <inheritdoc/>
    public void AskFormula() => Post(new AskFormulaRequest());

    /// <inheritdoc/>
    public void InsertFormula(string tex, bool block, int? position) => Post(new FormulaRequest(tex, block, position));

    /// <inheritdoc/>
    public void AskLink() => Post(new AskLinkRequest());

    /// <inheritdoc/>
    public void InsertLink(string label, string address) => Post(new LinkRequest(label, address));

    /// <inheritdoc/>
    public void InsertImage(string link, string alternative, int? position) => Post(new ImageRequest(link, alternative, position));

    /// <inheritdoc/>
    public void InsertSheetLink(string label, string address) => Post(new SheetLinkRequest(label, address));

    /// <inheritdoc/>
    public void SetProposalsOpen(bool open) => Post(new ListOpenRequest(open));

    /// <inheritdoc/>
    public void GoToHeading(string title, int occurrence) => Post(new GoToHeadingRequest(title, occurrence));

    /// <inheritdoc/>
    public void Undo() => Post(new UndoRequest());

    /// <inheritdoc/>
    public void Redo() => Post(new RedoRequest());

    /// <inheritdoc/>
    void IEditorSurface.Focus()
    {
        if (!_disposed)
        {
            _view.Focus();
            Post(new FocusRequest());
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        _ready = false;
        _configured = false;
        _pending.Clear();
        ClearValue(ThemeProbeProperty);
        _view.Dispose();
    }

    /// <summary>Lit une expression dans la page, pour les essais.</summary>
    internal Task<string> RunScriptAsync(string script) => _view.ExecuteScriptAsync(script);

    /// <summary>Traite un message de la page.</summary>
    internal void Receive(EditorEvent message)
    {
        switch (message)
        {
            case ReadyEvent:
                _ready = true;
                SendTheme();
                while (_pending.TryDequeue(out var request))
                {
                    Send(request);
                }
                break;
            case EditedEvent:
                _stale = true;
                TextEdited?.Invoke(this, EventArgs.Empty);
                break;
            case TextEvent text:
                Text = text.Markdown;
                _stale = false;
                if (text.Id is { } id && _syncs.TryGetValue(id, out var answered))
                {
                    answered.TrySetResult();
                }
                break;
            case SaveEvent:
                SaveAsked?.Invoke(this, EventArgs.Empty);
                break;
            case ToolsEvent tools:
                ToolsChanged?.Invoke(this, new EditorTools(tools.Bold, tools.Italic, tools.Underline, tools.Strike, tools.Superscript, tools.Subscript, tools.Heading, tools.List, tools.CanUndo, tools.CanRedo, tools.Color, tools.Size, tools.Align));
                break;
            case AskDiagramEvent diagram:
                Send(new DiagramRequest(diagram.Language, diagram.Code, SvgOf(diagram.Language, diagram.Code)));
                break;
            case EditDiagramEvent edit:
                DiagramAsked?.Invoke(this, new DiagramQuestion(edit.Language, edit.Code, edit.Position));
                break;
            case AskLinkEvent link:
                LinkAsked?.Invoke(this, new LinkQuestion(link.Label, link.Address));
                break;
            case AskFormulaEvent formula:
                FormulaAsked?.Invoke(this, new FormulaQuestion(formula.Tex, formula.Block, formula.Position));
                break;
            case LinkInputEvent input:
                InputPoint = new Point(input.X, input.Y);
                LinkInputChanged?.Invoke(this, input.Query is null ? null : new LinkInput(input.Query, input.Start));
                break;
            case ListKeyEvent key:
                ListKeyPressed?.Invoke(this, KeyOf(key.Key));
                break;
            case MenuEvent menu:
                OpenMenu(menu);
                break;
            case DroppedEvent dropped:
                ImageDropped?.Invoke(dropped.Name, dropped.Base64 is null ? null : Convert.FromBase64String(dropped.Base64), dropped.Position);
                break;
        }
    }

    /// <summary>Les éléments du menu natif, selon ce que la page a dit de l'endroit visé.</summary>
    internal List<Control> MenuItems(MenuEvent menu, SpellingCorrection? correction)
    {
        var items = new List<Control>();
        if (correction is not null && menu.Word is { } word)
        {
            foreach (var suggestion in correction.Suggestions)
            {
                var start = menu.WordStart + correction.Start;
                var item = Item(suggestion, new ReplaceWordRequest(start, start + correction.Length, suggestion));
                item.FontWeight = FontWeights.SemiBold;
                items.Add(item);
            }
            var add = new MenuItem { Header = EditingText.AddToDictionary };
            add.Click += (_, _) => _ = Task.Run(() => _spelling.Add(word));
            items.Add(add);
            items.Add(new Separator());
        }
        items.Add(Item(EditingText.Cut, new ClipboardRequest("cut")));
        items.Add(Item(EditingText.Copy, new ClipboardRequest("copy")));
        items.Add(Item(EditingText.Paste, new ClipboardRequest("paste")));
        items.Add(Item(EditingText.PastePlain, new ClipboardRequest("pastePlain")));
        if (!menu.InCode)
        {
            items.Add(new Separator());
            items.Add(Item(EditingText.Bold, new MarkRequest(InlineMark.Bold)));
            items.Add(Item(EditingText.Italic, new MarkRequest(InlineMark.Italic)));
            items.Add(Item(EditingText.Underline, new MarkRequest(InlineMark.Underline)));
            items.Add(Item(EditingText.Strike, new MarkRequest(InlineMark.Strike)));
            items.Add(Item(EditingText.Superscript, new MarkRequest(InlineMark.Superscript)));
            items.Add(Item(EditingText.Subscript, new MarkRequest(InlineMark.Subscript)));
            items.Add(Item(EditingText.ClearFormat, new ClearFormatRequest()));
            items.Add(new Separator());
            items.Add(Item(menu.OnLink ? EditingText.EditLink : EditingText.Link, new AskLinkRequest()));
            if (menu.OnLink)
            {
                items.Add(Item(EditingText.RemoveLink, new UnlinkRequest()));
            }
        }
        if (menu.InTable)
        {
            items.Add(new Separator());
            items.Add(Item(EditingText.RowBefore, new TableActionRequest("rowBefore")));
            items.Add(Item(EditingText.RowAfter, new TableActionRequest("rowAfter")));
            items.Add(Item(EditingText.ColumnBefore, new TableActionRequest("columnBefore")));
            items.Add(Item(EditingText.ColumnAfter, new TableActionRequest("columnAfter")));
            items.Add(Item(EditingText.DeleteRow, new TableActionRequest("deleteRow")));
            items.Add(Item(EditingText.DeleteColumn, new TableActionRequest("deleteColumn")));
            items.Add(Item(EditingText.DeleteTable, new TableActionRequest("deleteTable")));
        }
        return items;
    }

    private static Key KeyOf(string key) => key switch
    {
        "ArrowDown" => Key.Down,
        "ArrowUp" => Key.Up,
        "Tab" => Key.Tab,
        "Escape" => Key.Escape,
        _ => Key.Enter,
    };

    private static string ContentTypeOf(string path) => Path.GetExtension(path).ToUpperInvariant() switch
    {
        ".HTML" => "text/html; charset=utf-8",
        ".JS" => "text/javascript; charset=utf-8",
        ".CSS" => "text/css; charset=utf-8",
        ".WOFF2" => "font/woff2",
        ".TTF" => "font/ttf",
        ".PNG" => "image/png",
        ".JPG" or ".JPEG" => "image/jpeg",
        ".GIF" => "image/gif",
        ".WEBP" => "image/webp",
        ".AVIF" => "image/avif",
        ".BMP" => "image/bmp",
        ".SVG" => "image/svg+xml",
        _ => "application/octet-stream",
    };

    /// <summary>L'environnement partagé. Rien de ce qui vient de l'extérieur ne le règle : ni port de débogage, ni autre moteur.</summary>
    private static Task<CoreWebView2Environment> EnvironmentOf(string dataFolder)
    {
        if (_environment is null)
        {
            foreach (var variable in NeutralizedVariables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
            _environment = CoreWebView2Environment.CreateAsync(null, dataFolder, new CoreWebView2EnvironmentOptions { Language = "fr-FR" });
        }
        return _environment;
    }

    private MenuItem Item(string header, EditorRequest request)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => Post(request);
        return item;
    }

    private void OpenMenu(MenuEvent menu)
    {
        // Le menu attend sa réponse, le temps d'une frappe : le correcteur vit sur les fils de travail.
        var correction = menu.Word is { } word ? Task.Run(() => _spelling.Correct(word)).GetAwaiter().GetResult() : null;
        LastMenuItems = MenuItems(menu, correction);
        var context = new ContextMenu { Placement = PlacementMode.MousePoint, PlacementTarget = this };
        foreach (var item in LastMenuItems)
        {
            context.Items.Add(item);
        }
        context.Closed += (_, _) => _view.Focus();
        context.IsOpen = true;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (_disposed)
        {
            return;
        }
        try
        {
            var environment = await EnvironmentOf(_dataFolder).ConfigureAwait(true);
            await _view.EnsureCoreWebView2Async(environment).ConfigureAwait(true);
            if (_disposed)
            {
                return;
            }
            Configure(_view.CoreWebView2);
            _configured = true;
            _view.CoreWebView2.Navigate(StartAddress);
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException or InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException or IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            if (!_disposed)
            {
                Failed?.Invoke(this, exception.Message);
            }
        }
    }

    private void Configure(CoreWebView2 web)
    {
        var settings = web.Settings;
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsPinchZoomEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsWebMessageEnabled = true;
        web.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        web.WebResourceRequested += OnResourceRequested;
        web.WebMessageReceived += OnMessage;
        web.NavigationStarting += (_, e) => e.Cancel = e.Uri != StartAddress;
        web.NewWindowRequested += (_, e) => e.Handled = true;
        web.DownloadStarting += (_, e) => e.Cancel = true;
        web.PermissionRequested += (_, e) => e.State = e.PermissionKind == CoreWebView2PermissionKind.ClipboardRead ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
        web.ProcessFailed += (_, e) => Failed?.Invoke(this, e.ProcessFailedKind.ToString());
    }

    /// <summary>Le dessin d'un bloc, en SVG ; rien : ce n'est qu'un bloc de code.</summary>
    private static string? SvgOf(string info, string code)
    {
        var parts = info.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts is [SmartArt.Language, var layout] && SmartArt.Draw(layout, code) is { } scene ? DiagramSvg.Write(scene) : null;
    }

    /// <summary>Seul ce qui vient de l'application et du coffre est servi ; tout le reste est refusé.</summary>
    private void OnResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var web = (CoreWebView2)sender!;
        var address = new Uri(e.Request.Uri);
        var content = address.Host == Host ? ContentOf(Uri.UnescapeDataString(address.AbsolutePath)) : null;
        e.Response = content is null
            ? web.Environment.CreateWebResourceResponse(null, 403, "Forbidden", "Cache-Control: no-store")
            : web.Environment.CreateWebResourceResponse(
                new MemoryStream(content),
                200,
                "OK",
                string.Create(CultureInfo.InvariantCulture, $"Content-Type: {ContentTypeOf(address.AbsolutePath)}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: {SecurityPolicy}"));
    }

    private byte[]? ContentOf(string path)
    {
        if (path.StartsWith("/coffre/", StringComparison.Ordinal))
        {
            var outcome = _readResource(path["/coffre/".Length..]);
            return outcome.Succeeded ? outcome.Value : null;
        }
        if (path.StartsWith("/polices/Atkinson", StringComparison.Ordinal))
        {
            return FontOf(path["/polices/".Length..]);
        }
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + path.TrimStart('/'));
        if (stream is null)
        {
            return null;
        }
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static byte[]? FontOf(string file)
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri($"/KyfRev.DesignSystem;component/Fonts/{file}", UriKind.Relative));
        if (resource is null)
        {
            return null;
        }
        using var memory = new MemoryStream();
        resource.Stream.CopyTo(memory);
        return memory.ToArray();
    }

    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (JsonSerializer.Deserialize<EditorEvent>(e.WebMessageAsJson, VisualContract.Options) is { } message)
        {
            Receive(message);
        }
    }

    private void Post(EditorRequest request)
    {
        if (_disposed)
        {
            return;
        }
        if (_ready)
        {
            Send(request);
        }
        else
        {
            _pending.Enqueue(request);
        }
    }

    /// <summary>Ce qui arrive après la fermeture du moteur (un changement de thème, une frappe en attente) ne va nulle part.</summary>
    private void Send(EditorRequest request)
    {
        if (!_disposed)
        {
            _view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(request, VisualContract.Options));
        }
    }

    private void QueueTheme()
    {
        if (_ready && !_themeQueued)
        {
            _themeQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                _themeQueued = false;
                SendTheme();
            });
        }
    }

    private void SendTheme()
    {
        var variables = new Dictionary<string, string>
        {
            ["--taille-lecture"] = string.Create(CultureInfo.InvariantCulture, $"{Typography.BodyLarge.Size}px"),
            ["--interligne-lecture"] = string.Create(CultureInfo.InvariantCulture, $"{Typography.ReadingLineSpacing}"),
        };
        foreach (var (variable, resource) in ThemeColors)
        {
            if (TryFindResource(resource) is SolidColorBrush brush)
            {
                variables[variable] = string.Create(CultureInfo.InvariantCulture, $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}");
            }
        }
        if (TryFindResource("Kyf.Brush.Background") is SolidColorBrush { Color: var ground })
        {
            var dark = (0.2126 * ground.R) + (0.7152 * ground.G) + (0.0722 * ground.B) < 128;
            variables["--schema"] = dark ? "dark" : "light";
            variables["--clarte-min"] = string.Create(CultureInfo.InvariantCulture, $"{(dark ? TokenColor.ReadableLightnessOnDark : 0)}");
            variables["--clarte-max"] = string.Create(CultureInfo.InvariantCulture, $"{(dark ? 1 : TokenColor.ReadableLightnessOnLight)}");
            _view.DefaultBackgroundColor = new TokenColor(ground.A, ground.R, ground.G, ground.B).ToDrawingColor();
        }
        variables["--couleur-forcee"] = TryFindResource(DesignKeys.IsHighContrast) is true ? "currentColor" : string.Empty;
        Send(new ThemeRequest(variables));
    }
}
