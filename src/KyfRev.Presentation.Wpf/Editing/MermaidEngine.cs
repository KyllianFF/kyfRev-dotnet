using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using Microsoft.Web.WebView2.Core;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Un dessin de Mermaid.</summary>
/// <param name="Png">Son image, pour la lecture.</param>
/// <param name="Image">Son adresse <c>data:</c>, pour l'éditeur.</param>
/// <param name="Width">Sa largeur naturelle.</param>
/// <param name="Height">Sa hauteur naturelle.</param>
public sealed record MermaidPicture(ReadOnlyMemory<byte> Png, string Image, double Width, double Height);

/// <summary>Ce qui dessine les schémas et les graphiques.</summary>
public interface IMermaidEngine
{
    /// <summary>Dessine ce code avec ces couleurs ; rien s'il ne se dessine pas.</summary>
    /// <param name="code">Le code Mermaid.</param>
    /// <param name="theme">Les couleurs du thème, sous les noms de Mermaid.</param>
    Task<MermaidPicture?> DrawAsync(string code, IReadOnlyDictionary<string, string> theme);
}

/// <summary>
/// Dessine les schémas et les graphiques dans une page cachée : le même moteur web que l'éditeur, aussi fermé, et qui ne voit
/// jamais le coffre. Il ne démarre qu'au premier schéma. Un dessin qui ne finit pas (une fiche piégée) ne bloque rien : le
/// moteur est fermé, et il repartira pour le suivant.
/// </summary>
public sealed class MermaidEngine : IMermaidEngine, IDisposable
{
    /// <summary>La clé sous laquelle les vues le trouvent, dans les ressources de l'application.</summary>
    public const string ResourceKey = "Kyf.Mermaid";

    private const string Host = "dessins.kyfrev.invalid";
    private const string StartAddress = $"https://{Host}/diagrammes.html";
    private const int CacheSize = 64;
    private const int HiddenWidth = 1600;
    private const int HiddenHeight = 1200;
    private const int PopupWindow = unchecked((int)0x80000000);

    private readonly string _dataFolder;
    private readonly Dictionary<int, TaskCompletionSource<MermaidPicture?>> _pending = [];
    private readonly Dictionary<string, MermaidPicture> _cache = new(StringComparer.Ordinal);
    private TaskCompletionSource? _ready;
    private Task? _starting;
    private HwndSource? _window;
    private CoreWebView2Controller? _controller;
    private int _nextId;
    private bool _disposed;

    /// <summary>Un moteur qui garde ses données dans ce dossier.</summary>
    public MermaidEngine(string dataFolder)
    {
        _dataFolder = dataFolder;
    }

    /// <summary>Le temps qu'un dessin peut prendre.</summary>
    public TimeSpan Patience { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Les couleurs d'un thème, sous les noms de Mermaid.</summary>
    /// <param name="element">L'élément dont les ressources donnent le thème.</param>
    public static IReadOnlyDictionary<string, string> ThemeOf(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        string Color(ColorRole role) => element.TryFindResource(DesignKeys.Brush(role)) is SolidColorBrush brush
            ? string.Create(CultureInfo.InvariantCulture, $"#{brush.Color.R:x2}{brush.Color.G:x2}{brush.Color.B:x2}")
            : "gray";
        var ground = element.TryFindResource(DesignKeys.Brush(ColorRole.Background)) is SolidColorBrush { Color: var color } ? color : default;
        return new Dictionary<string, string>
        {
            ["darkMode"] = (0.2126 * ground.R) + (0.7152 * ground.G) + (0.0722 * ground.B) < 128 ? "true" : "false",
            ["background"] = Color(ColorRole.Background),
            ["primaryColor"] = Color(ColorRole.NavigationActiveBackground),
            ["primaryTextColor"] = Color(ColorRole.NavigationActiveText),
            ["primaryBorderColor"] = Color(ColorRole.Primary),
            ["secondaryColor"] = Color(ColorRole.PillPassageBackground),
            ["secondaryTextColor"] = Color(ColorRole.PillPassageText),
            ["secondaryBorderColor"] = Color(ColorRole.PillPassageText),
            ["tertiaryColor"] = Color(ColorRole.SurfaceAlt),
            ["tertiaryTextColor"] = Color(ColorRole.Text),
            ["tertiaryBorderColor"] = Color(ColorRole.TextSecondary),
            ["lineColor"] = Color(ColorRole.TextSecondary),
            ["textColor"] = Color(ColorRole.Text),
            ["titleColor"] = Color(ColorRole.Text),
            ["mainBkg"] = Color(ColorRole.NavigationActiveBackground),
            ["nodeBorder"] = Color(ColorRole.Primary),
            ["nodeTextColor"] = Color(ColorRole.NavigationActiveText),
            ["clusterBkg"] = Color(ColorRole.SurfaceAlt),
            ["edgeLabelBackground"] = Color(ColorRole.Background),
            ["actorBkg"] = Color(ColorRole.NavigationActiveBackground),
            ["actorTextColor"] = Color(ColorRole.NavigationActiveText),
            ["actorLineColor"] = Color(ColorRole.TextSecondary),
            ["signalColor"] = Color(ColorRole.Text),
            ["signalTextColor"] = Color(ColorRole.Text),
            ["labelTextColor"] = Color(ColorRole.Text),
            ["noteBkgColor"] = Color(ColorRole.PillPassageBackground),
            ["noteTextColor"] = Color(ColorRole.PillPassageText),
            ["pie1"] = Color(ColorRole.Primary),
            ["pie2"] = Color(ColorRole.PillPassageText),
            ["pie3"] = Color(ColorRole.TextSecondary),
            ["pie4"] = Color(ColorRole.Info),
            ["pie5"] = Color(ColorRole.Success),
            ["pie6"] = Color(ColorRole.Warning),
            ["pieTitleTextColor"] = Color(ColorRole.Text),
            ["pieSectionTextColor"] = Color(ColorRole.OnPrimary),
            ["pieLegendTextColor"] = Color(ColorRole.Text),
        };
    }

    /// <inheritdoc/>
    public async Task<MermaidPicture?> DrawAsync(string code, IReadOnlyDictionary<string, string> theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (_disposed)
        {
            return null;
        }
        var key = string.Join('\n', theme.Values) + '\n' + code;
        if (_cache.TryGetValue(key, out var known))
        {
            return known;
        }
        if (!await EnsureStartedAsync().ConfigureAwait(true) || _controller is null)
        {
            return null;
        }
        var id = ++_nextId;
        var answer = new TaskCompletionSource<MermaidPicture?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = answer;
        _controller.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { id, code, theme }));
        var first = await Task.WhenAny(answer.Task, Task.Delay(Patience)).ConfigureAwait(true);
        if (first != answer.Task)
        {
            Stop();
            return null;
        }
        var picture = await answer.Task.ConfigureAwait(true);
        if (picture is not null)
        {
            if (_cache.Count >= CacheSize)
            {
                _cache.Clear();
            }
            _cache[key] = picture;
        }
        return picture;
    }

    /// <summary>Exécute un script dans la page, pour les essais.</summary>
    internal async Task<string> RunScriptAsync(string script)
    {
        await EnsureStartedAsync().ConfigureAwait(true);
        return await _controller!.CoreWebView2.ExecuteScriptAsync(script).ConfigureAwait(true);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        Stop();
    }

    private void Stop()
    {
        foreach (var answer in _pending.Values)
        {
            answer.TrySetResult(null);
        }
        _pending.Clear();
        _controller?.Close();
        _controller = null;
        _window?.Dispose();
        _window = null;
        _starting = null;
        _ready = null;
    }

    private async Task<bool> EnsureStartedAsync()
    {
        try
        {
            _starting ??= StartAsync();
            await _starting.ConfigureAwait(true);
            return _controller is not null;
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException or InvalidOperationException or COMException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            Stop();
            return false;
        }
    }

    private async Task StartAsync()
    {
        _window = new HwndSource(new HwndSourceParameters("kyfRev")
        {
            WindowStyle = PopupWindow,
            PositionX = -32000,
            PositionY = -32000,
            Width = HiddenWidth,
            Height = HiddenHeight,
        });
        var environment = await VisualEditor.EnvironmentOf(_dataFolder).ConfigureAwait(true);
        var controller = await environment.CreateCoreWebView2ControllerAsync(_window.Handle).ConfigureAwait(true);
        controller.Bounds = new Rectangle(0, 0, HiddenWidth, HiddenHeight);
        controller.IsVisible = true;
        _controller = controller;
        var web = controller.CoreWebView2;
        VisualEditor.Harden(web);
        web.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        web.WebResourceRequested += (_, e) => VisualEditor.Serve(web, e, Host, path => VisualEditor.PageContent(path));
        web.WebMessageReceived += OnMessage;
        web.NavigationStarting += (_, e) => e.Cancel = e.Uri != StartAddress;
        web.ProcessFailed += (_, _) => Stop();
        _ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        web.Navigate(StartAddress);
        await _ready.Task.WaitAsync(Patience).ConfigureAwait(true);
    }

    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var message = JsonDocument.Parse(e.WebMessageAsJson);
        var root = message.RootElement;
        var kind = root.GetProperty("kind").GetString();
        if (kind == "ready")
        {
            _ready?.TrySetResult();
            return;
        }
        var id = root.GetProperty("id").GetInt32();
        if (!_pending.Remove(id, out var answer))
        {
            return;
        }
        answer.TrySetResult(kind == "drawn"
            ? new MermaidPicture(
                Convert.FromBase64String(root.GetProperty("png").GetString()!),
                root.GetProperty("image").GetString()!,
                root.GetProperty("width").GetDouble(),
                root.GetProperty("height").GetDouble())
            : null);
    }
}
