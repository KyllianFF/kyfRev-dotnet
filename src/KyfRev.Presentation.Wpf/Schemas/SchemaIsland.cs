using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Editing;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace KyfRev.Presentation.Wpf.Schemas;

/// <summary>
/// L'îlot des schémas réseau : la vue et l'éditeur de topologie de la 0.x, dans le moteur web de l'application. Il est fermé comme
/// l'éditeur de texte : pas de réseau, pas d'objet de l'hôte, et seuls ses propres fichiers lui sont servis. Tout passe par
/// <see cref="SchemaBridge"/>.
/// </summary>
internal sealed class SchemaIsland : UserControl, IDisposable
{
    internal const string Host = "schemas.kyfrev.invalid";

    private const string StartAddress = $"https://{Host}/index.html";
    private const string ResourcePrefix = "kyfrev-schemas/";

    /// <summary>Le nom de chaque couleur dans la page, et sa couleur dans l'application.</summary>
    private static readonly (string Variable, string Resource)[] ThemeColors =
    [
        ("--fond", "Kyf.Brush.Background"),
        ("--surface", "Kyf.Brush.Surface"),
        ("--surface-2", "Kyf.Brush.SurfaceAlt"),
        ("--bordure", "Kyf.Brush.Divider"),
        ("--texte", "Kyf.Brush.Text"),
        ("--texte-secondaire", "Kyf.Brush.TextSecondary"),
        ("--navigation-active-fond", "Kyf.Brush.NavigationActiveBackground"),
        ("--navigation-active-texte", "Kyf.Brush.NavigationActiveText"),
        ("--pastille-fiche-fond", "Kyf.Brush.PillSheetBackground"),
        ("--pastille-fiche-texte", "Kyf.Brush.PillSheetText"),
        ("--lien", "Kyf.Brush.Link"),
        ("--principale", "Kyf.Brush.Primary"),
        ("--principale-survol", "Kyf.Brush.PrimaryHover"),
        ("--sur-principale", "Kyf.Brush.OnPrimary"),
        ("--graphique-1", "Kyf.Brush.Primary"),
        ("--focus", "Kyf.Brush.Focus"),
        ("--retard", "Kyf.Brush.Critical"),
        ("--retard-fond", "Kyf.Brush.CriticalBackground"),
        ("--avertissement", "Kyf.Brush.Warning"),
        ("--avertissement-fond", "Kyf.Brush.WarningBackground"),
        ("--succes", "Kyf.Brush.Success"),
        ("--succes-fond", "Kyf.Brush.SuccessBackground"),
    ];

    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register(
        "ThemeProbe", typeof(Brush), typeof(SchemaIsland), new PropertyMetadata(null, (sender, _) => ((SchemaIsland)sender).SendTheme()));

    private readonly WebView2 _view = new();
    private readonly Image _snapshot = new() { Visibility = Visibility.Collapsed, Stretch = Stretch.Fill };
    private readonly SchemaBridge _bridge;
    private readonly string _dataFolder;
    private readonly Dictionary<int, TaskCompletionSource<bool>> _finishing = [];
    private int _lastFinish;
    private string? _toOpen;
    private bool _ready;
    private bool _covered;
    private bool _disposed;

    public SchemaIsland(SchemaBridge bridge, string dataFolder)
    {
        _bridge = bridge;
        _dataFolder = dataFolder;
        Focusable = false;
        IsTabStop = false;
        System.Windows.Automation.AutomationProperties.SetAutomationId(_view, "SchemasIsland");
        System.Windows.Automation.AutomationProperties.SetName(_view, SchemaText.IslandName);
        var ground = new Grid { Children = { _view, _snapshot } };
        ground.SetResourceReference(Panel.BackgroundProperty, "Kyf.Brush.Background");
        Content = ground;
        SetResourceReference(ThemeProbeProperty, "Kyf.Brush.Text");
        Loaded += OnLoaded;
        _bridge.Finished += OnFinished;
    }

    /// <summary>Le moteur n'a pas pu démarrer, ou s'est arrêté.</summary>
    public event EventHandler<string>? Failed;

    /// <summary>Un processus du moteur s'est arrêté puis a été relancé par WebView2 (le GPU, un utilitaire) : la page continue.</summary>
    public event EventHandler<string>? Restarted;

    /// <summary>La page a chargé son code.</summary>
    internal bool IsReady => _ready;

    /// <summary>L'îlot est recouvert, et remplacé par son image.</summary>
    internal bool IsCovered => _covered;

    /// <summary>Le fond du moteur, avant même sa première page.</summary>
    internal System.Drawing.Color Ground => _view.DefaultBackgroundColor;

    /// <summary>Le temps laissé à la page pour finir ce qui est en cours ; au-delà, rien n'est tenu pour enregistré.</summary>
    internal TimeSpan FinishPatience { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Les couleurs de l'application, sous les noms de la page ; pour dessiner un schéma hors de l'îlot aussi.</summary>
    internal static Dictionary<string, string> ThemeOf(FrameworkElement element)
    {
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (variable, resource) in ThemeColors)
        {
            if (element.TryFindResource(resource) is SolidColorBrush brush)
            {
                colors[variable] = string.Create(CultureInfo.InvariantCulture, $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}");
            }
        }
        return colors;
    }

    /// <summary>Ouvre l'éditeur sur ce schéma, dès que la page est prête.</summary>
    internal void OpenSchema(string id)
    {
        _toOpen = id;
        SendPendingOpen();
    }

    /// <summary>Le moteur a sa propre fenêtre, au premier plan : pendant qu'un volet de l'application le recouvre, son image prend sa place.</summary>
    internal async Task SetCoveredAsync(bool covered)
    {
        if (covered == _covered || _disposed)
        {
            return;
        }
        _covered = covered;
        if (covered)
        {
            if (_view.CoreWebView2 is { } web)
            {
                using var stream = new MemoryStream();
                await web.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream).ConfigureAwait(true);
                stream.Position = 0;
                _snapshot.Source = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                _snapshot.Visibility = Visibility.Visible;
            }
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
        }
    }

    /// <summary>La taille de l'interface vaut aussi pour l'îlot.</summary>
    internal void SetZoom(double zoom)
    {
        if (!_disposed)
        {
            _view.ZoomFactor = zoom;
        }
    }

    /// <summary>
    /// Avant le verrouillage du coffre : le champ où l'on écrivait s'applique, et le schéma ouvert enregistre ce qui attendait.
    /// Vrai si rien ne reste à enregistrer, ou si aucun schéma n'est ouvert ; faux si l'enregistrement a été refusé, s'est heurté à
    /// une version modifiée ailleurs, ou si la page n'a pas répondu à temps.
    /// </summary>
    internal async Task<bool> FinishAsync()
    {
        if (!_ready || _disposed)
        {
            // Une page qui n'a pas encore chargé son code n'a rien pu modifier ; un îlot fermé n'a plus rien à enregistrer.
            return true;
        }
        var request = ++_lastFinish;
        var answered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _finishing[request] = answered;
        Post(new JsonObject { ["kind"] = "finir", ["numero"] = request }.ToJsonString());
        try
        {
            return await answered.Task.WaitAsync(FinishPatience).ConfigureAwait(true);
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            _finishing.Remove(request);
        }
    }

    /// <summary>Exécute un script dans la page, pour les essais.</summary>
    internal Task<string> RunScriptAsync(string script) => _view.ExecuteScriptAsync(script);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _bridge.Finished -= OnFinished;
            // La page fermée ne répondra plus : ce qu'elle finissait n'est pas tenu pour enregistré.
            foreach (var waiting in _finishing.Values)
            {
                waiting.TrySetResult(false);
            }
            _view.Dispose();
        }
    }

    /// <summary>Un fichier de la page, venu de l'application.</summary>
    internal static byte[]? PageContent(string path)
    {
        if (path.StartsWith("/polices/", StringComparison.Ordinal))
        {
            return VisualEditor.PageContent(path);
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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (_disposed)
        {
            return;
        }
        try
        {
            PaintGround();
            var environment = await VisualEditor.EnvironmentOf(_dataFolder).ConfigureAwait(true);
            await _view.EnsureCoreWebView2Async(environment).ConfigureAwait(true);
            if (_disposed)
            {
                return;
            }
            Configure(_view.CoreWebView2);
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
        VisualEditor.Harden(web);
        web.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        web.WebResourceRequested += (_, e) => VisualEditor.Serve(web, e, Host, PageContent);
        web.WebMessageReceived += OnMessage;
        web.NavigationStarting += (_, e) => e.Cancel = e.Uri != StartAddress;
        web.ProcessFailed += (_, e) =>
        {
            if (WebProcesses.StopsThePage(e.ProcessFailedKind))
            {
                Failed?.Invoke(this, e.ProcessFailedKind.ToString());
            }
            else
            {
                Restarted?.Invoke(this, e.ProcessFailedKind.ToString());
            }
        };
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = e.WebMessageAsJson;
        if (message.Contains("\"ready\"", StringComparison.Ordinal))
        {
            _ready = true;
            SendTheme();
            SendPendingOpen();
            return;
        }
        // Une demande de la page qui échoue (un fichier refusé, un moteur fermé entre-temps) reste sans réponse : elle
        // n'arrête pas l'application.
        try
        {
            var answer = await _bridge.ReceiveAsync(message).ConfigureAwait(true);
            if (answer is not null)
            {
                Post(answer);
            }
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException or ObjectDisposedException or NotSupportedException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            if (!_disposed)
            {
                Failed?.Invoke(this, exception.Message);
            }
        }
    }

    private void OnFinished(object? sender, FinishReport report)
    {
        if (_finishing.Remove(report.Request, out var waiting))
        {
            waiting.TrySetResult(report.Done);
        }
    }

    /// <summary>
    /// Envoie un message à la page. Le moteur a pu se fermer pendant que la réponse se préparait (la fenêtre s'est fermée) : il n'y a
    /// alors plus personne à qui répondre, et rien ne doit lever d'exception dans un gestionnaire d'événement.
    /// </summary>
    private void Post(string json)
    {
        if (!_disposed && _view.CoreWebView2 is { } web)
        {
            web.PostWebMessageAsJson(json);
        }
    }

    /// <summary>Le fond du moteur prend celui de l'application dès avant sa première page : aucun éclair blanc à l'ouverture, en sombre surtout.</summary>
    private void PaintGround()
    {
        if (!_disposed && TryFindResource("Kyf.Brush.Background") is SolidColorBrush { Color: var ground })
        {
            _view.DefaultBackgroundColor = new TokenColor(ground.A, ground.R, ground.G, ground.B).ToDrawingColor();
        }
    }

    private void SendPendingOpen()
    {
        if (_ready && !_disposed && _toOpen is { } id)
        {
            _toOpen = null;
            Post(new JsonObject { ["kind"] = "open", ["id"] = id }.ToJsonString());
        }
    }

    /// <summary>Envoie les couleurs à la page ; le thème peut changer pendant qu'elle est ouverte.</summary>
    private void SendTheme()
    {
        PaintGround();
        if (!_ready || _disposed)
        {
            return;
        }
        var variables = new JsonObject();
        foreach (var (variable, color) in ThemeOf(this))
        {
            variables[variable] = color;
        }
        if (TryFindResource("Kyf.Brush.Background") is SolidColorBrush { Color: var ground })
        {
            var dark = (0.2126 * ground.R) + (0.7152 * ground.G) + (0.0722 * ground.B) < 128;
            variables["--schema"] = dark ? "dark" : "light";
            variables["--voile"] = dark ? "rgb(0 0 0 / 60%)" : "rgb(20 30 25 / 35%)";
            _view.DefaultBackgroundColor = new TokenColor(ground.A, ground.R, ground.G, ground.B).ToDrawingColor();
        }
        Post(new JsonObject { ["kind"] = "theme", ["variables"] = variables }.ToJsonString());
    }
}
