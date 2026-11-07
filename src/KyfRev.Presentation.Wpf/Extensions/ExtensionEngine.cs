using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Interop;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Editing;
using Microsoft.Web.WebView2.Core;

namespace KyfRev.Presentation.Wpf.Extensions;

/// <summary>La réponse d'une extension : ce qu'elle affiche, passé par la liste blanche, ou la raison pour laquelle elle n'a pas répondu.</summary>
/// <param name="Content">Ce qu'elle affiche ; rien pour une action.</param>
/// <param name="Error">La raison de l'échec ; rien si elle a répondu.</param>
public sealed record ExtensionAnswer(ExtensionContent? Content, string? Error)
{
    /// <summary>Ce qu'elle a rendu d'autre qu'un affichage : un nombre, un encart.</summary>
    public JsonElement? Value { get; init; }

    /// <summary>Elle a répondu.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Error is null;
}

/// <summary>Ce qu'une extension demande à l'application : redessiner sa vue, l'ouvrir.</summary>
/// <param name="extension">L'extension.</param>
public sealed class ExtensionEventArgs(string extension) : EventArgs
{
    /// <summary>L'extension.</summary>
    public string Extension { get; } = extension;
}

/// <summary>
/// Fait tourner le code des extensions dans une page cachée, chacune dans son cadre « sandbox » d'origine opaque. Le moteur a son
/// propre processus et ses propres données : il ne résout aucune adresse, ne sert que ce que dit l'application, et n'est
/// démarré qu'au premier besoin. Une extension qui ne répond pas ne bloque rien : le moteur est fermé, et il repartira.
/// </summary>
public sealed class ExtensionEngine : IDisposable, IExtensionFaults
{
    /// <summary>Les erreurs gardées pour chaque extension : les plus récentes.</summary>
    public const int MaxFaults = 20;

    private readonly Dictionary<string, List<ExtensionFault>> _faults = new(StringComparer.Ordinal);

    private const string Host = "extensions.kyfrev.invalid";
    private const string Origin = $"https://{Host}";
    private const string StartAddress = $"{Origin}/hote.html";
    private const string FramePrefix = $"{Origin}/cadre/";
    private const string ResourcePrefix = "kyfrev-extensions/";
    private const int HiddenSize = 400;
    private const int MaxCount = 999;
    private const int MaxCardText = 300;
    private const int PopupWindow = unchecked((int)0x80000000);

    /// <summary>Aucune résolution d'adresse : ce moteur n'a aucune raison d'atteindre un réseau.</summary>
    private const string NoNetwork = "--host-resolver-rules=\"MAP * ~NOTFOUND\" --dns-prefetch-disable --disable-background-networking";

    /// <summary>
    /// WebRTC envoie ses paquets UDP sans tenir compte de la politique du document : un serveur STUN ou TURN choisi par l'extension
    /// recevrait ce qu'elle veut. Il est retiré de chaque document, avant tout script de la page.
    /// </summary>
    private const string NoWebRtc = "for (const name of Object.getOwnPropertyNames(globalThis)) { if (/^(webkit)?RTC/.test(name)) delete globalThis[name]; }";

    private const string PagePolicy = "default-src 'none'; script-src 'self'; frame-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
    private const string FilePolicy = "default-src 'none'; sandbox";
    private const string HtmlType = "text/html; charset=utf-8";
    private const string ScriptType = "text/javascript; charset=utf-8";

    /// <summary>L'arbre d'un rendu descend plus loin que la limite ordinaire.</summary>
    private static readonly JsonDocumentOptions MessageOptions = new() { MaxDepth = 256 };

    private readonly string _dataFolder;
    private readonly ExtensionLibrary _library;
    private readonly ExtensionRequests _requests;
    private readonly Dictionary<int, Pending> _pending = [];
    private readonly Dictionary<string, TaskCompletionSource<string?>> _frames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _waitingOnUser = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource> _answeredByUser = new(StringComparer.Ordinal);
    private TaskCompletionSource? _ready;
    private Task? _starting;
    private HwndSource? _window;
    private CoreWebView2Controller? _controller;
    private int _nextRequest;
    private bool _disposed;

    /// <summary>Un moteur pour les extensions de cette bibliothèque.</summary>
    /// <param name="dataFolder">Le dossier de ses données, à lui seul.</param>
    /// <param name="library">Les extensions installées.</param>
    /// <param name="requests">Ce qui répond à leurs demandes, permissions vérifiées.</param>
    public ExtensionEngine(string dataFolder, ExtensionLibrary library, ExtensionRequests requests)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataFolder);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(requests);
        _dataFolder = dataFolder;
        _library = library;
        _requests = requests;
    }

    /// <summary>Une extension demande de redessiner sa vue.</summary>
    public event EventHandler<ExtensionEventArgs>? RefreshAsked;

    /// <summary>Une extension demande d'ouvrir sa vue.</summary>
    public event EventHandler<ExtensionEventArgs>? ViewAsked;

    /// <inheritdoc/>
    public event EventHandler<ExtensionEventArgs>? FaultsChanged;

    /// <inheritdoc/>
    public IReadOnlyList<ExtensionFault> Faults(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _faults.TryGetValue(id, out var faults) ? [.. Enumerable.Reverse(faults)] : [];
    }

    /// <inheritdoc/>
    public void ForgetFaults(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (_faults.Remove(id))
        {
            FaultsChanged?.Invoke(this, new ExtensionEventArgs(id));
        }
    }

    /// <summary>Le temps qu'une extension a pour se charger et pour répondre. Il ne court pas pendant qu'elle attend l'utilisateur.</summary>
    public TimeSpan Patience { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Ce que l'application montre pour les extensions : la sélection guidée, une fiche ouverte à un passage.</summary>
    public IExtensionScreen? Screen { get; set; }

    /// <summary>Le temps que le moteur a pour démarrer : le tout premier démarrage sur une machine neuve est long.</summary>
    public TimeSpan StartPatience { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Ce qui a été refusé : aucune adresse hors de l'application n'est jamais servie.</summary>
    internal List<string> Refused { get; } = [];

    /// <summary>Demande l'écran de la vue d'une extension.</summary>
    /// <param name="id">L'extension.</param>
    /// <param name="dark">Le thème est sombre.</param>
    public Task<ExtensionAnswer> RenderViewAsync(string id, bool dark) =>
        AskAsync(id, "vue", n => new { type = "rendre-vue", n, contexte = Context(dark) });

    /// <summary>Demande le dessin d'un bloc à l'extension qui le déclare.</summary>
    /// <param name="id">L'extension.</param>
    /// <param name="language">Le langage du bloc.</param>
    /// <param name="source">Le texte du bloc.</param>
    /// <param name="dark">Le thème est sombre.</param>
    public Task<ExtensionAnswer> RenderBlockAsync(string id, string language, string source, bool dark) =>
        AskAsync(id, $"bloc « {language} »", n => new { type = "rendre-bloc", n, langage = language, source, contexte = Context(dark) });

    /// <summary>Transmet un clic ou un changement sur un élément <c>data-action</c> de sa vue.</summary>
    /// <param name="id">L'extension.</param>
    /// <param name="name">Le nom de l'action.</param>
    /// <param name="value">Sa valeur : un texte, un booléen ou rien.</param>
    /// <param name="data">Les attributs <c>data-</c> de l'élément.</param>
    public Task<ExtensionAnswer> ActAsync(string id, string name, object? value, IReadOnlyDictionary<string, string> data) =>
        AskAsync(id, $"action « {name} »", n => new { type = "action", n, nom = name, valeur = value, donnees = data });

    /// <summary>Lance une commande déclarée par une extension, avec ce sur quoi elle porte.</summary>
    /// <param name="id">L'extension.</param>
    /// <param name="command">La commande.</param>
    /// <param name="context">Ce sur quoi elle porte : la fiche ou le dossier du clic droit.</param>
    public Task<ExtensionAnswer> CommandAsync(string id, string command, IReadOnlyDictionary<string, string> context) =>
        AskAsync(id, $"commande « {command} »", n => new { type = "commande", n, nom = command, contexte = context });

    /// <summary>Le nombre qu'une extension montre près de son entrée du menu ; rien s'il n'y en a pas.</summary>
    /// <param name="id">L'extension.</param>
    public async Task<int?> CountAsync(string id)
    {
        var answer = await AskAsync(id, "compteur", n => new { type = "compteur", n }).ConfigureAwait(true);
        return answer.Value is { ValueKind: JsonValueKind.Number } value && value.TryGetDouble(out var count) && count >= 1
            ? (int)Math.Min(count, MaxCount)
            : null;
    }

    /// <summary>L'encart d'une extension sur « Aujourd'hui » : son texte et sa précision ; rien si elle n'a rien à dire.</summary>
    /// <param name="id">L'extension.</param>
    public async Task<(string Text, string? Detail)?> CardAsync(string id)
    {
        var answer = await AskAsync(id, "encart", n => new { type = "encart", n }).ConfigureAwait(true);
        if (answer.Value is not { ValueKind: JsonValueKind.Object } card || TextOf(card, "texte") is not { Length: > 0 } text)
        {
            return null;
        }
        static string Short(string value) => value.Length > MaxCardText ? value[..MaxCardText] : value;
        return (Short(text), TextOf(card, "detail") is { Length: > 0 } detail ? Short(detail) : null);
    }

    /// <summary>Oublie le cadre d'une extension désactivée, retirée ou réinstallée : le prochain usage charge son nouveau script.</summary>
    /// <param name="id">L'extension.</param>
    public void Forget(string id)
    {
        if (_frames.Remove(id, out var loading))
        {
            loading.TrySetResult("extension retirée");
            Post(new { kind = "oublier", id });
        }
    }

    /// <summary>Ferme le moteur : au prochain usage, chaque extension repart de zéro. Elles gardent en mémoire ce qu'elles ont lu d'un coffre : il le faut quand il change.</summary>
    public void Restart() => Stop();

    /// <summary>Exécute un script dans la page cachée, pour les essais.</summary>
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

    private static object Context(bool dark) => new { theme = dark ? "sombre" : "clair" };

    private static byte[]? ResourceOf(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + name);
        if (stream is null)
        {
            return null;
        }
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static string? TextOf(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>La politique d'un cadre : ses propres scripts, et rien d'autre.</summary>
    private static string FramePolicy(string id) =>
        $"default-src 'none'; script-src {Origin}/cadre.js {Origin}/{id}/; img-src data:; connect-src 'none'; frame-src 'none'; child-src 'none'; worker-src 'none'; form-action 'none'; base-uri 'none'; frame-ancestors {Origin}; sandbox allow-scripts";

    private static string Headers(string contentType, string policy) =>
        string.Create(CultureInfo.InvariantCulture, $"Content-Type: {contentType}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nX-DNS-Prefetch-Control: off\r\nContent-Security-Policy: {policy}");

    /// <summary>Demande, et garde l'erreur s'il y en a une : le mode développeur la montre sous l'extension.</summary>
    private async Task<ExtensionAnswer> AskAsync(string id, string where, Func<int, object> message)
    {
        var answer = await AskOnceAsync(id, message).ConfigureAwait(true);
        if (answer.Error is { } error && _library.Active(id) is { } manifest)
        {
            // Sous l'extension, son nom en tête de l'erreur se répéterait.
            var named = $"extension « {manifest.Name} » : ";
            error = error.StartsWith(named, StringComparison.Ordinal) ? error[named.Length..] : error;
            if (!_faults.TryGetValue(id, out var faults))
            {
                faults = [];
                _faults[id] = faults;
            }
            faults.Add(new ExtensionFault(DateTimeOffset.Now, where, error));
            if (faults.Count > MaxFaults)
            {
                faults.RemoveAt(0);
            }
            FaultsChanged?.Invoke(this, new ExtensionEventArgs(id));
        }
        return answer;
    }

    private async Task<ExtensionAnswer> AskOnceAsync(string id, Func<int, object> message)
    {
        if (_disposed)
        {
            return new ExtensionAnswer(null, "extension inactive");
        }
        if (_library.Active(id) is not { } manifest)
        {
            return new ExtensionAnswer(null, "extension inactive");
        }
        if (!await EnsureStartedAsync().ConfigureAwait(true))
        {
            return new ExtensionAnswer(null, $"extension « {manifest.Name} » : le moteur web ne démarre pas");
        }
        if (await LoadAsync(manifest).ConfigureAwait(true) is { } loadError)
        {
            return new ExtensionAnswer(null, $"extension « {manifest.Name} » : {loadError}");
        }
        var n = ++_nextRequest;
        var answer = new TaskCompletionSource<ExtensionAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[n] = new Pending(id, manifest.Name, answer);
        Post(new { kind = "vers-cadre", id, message = message(n) });
        // Une extension qui attend l'utilisateur (une sélection guidée) n'est pas bloquée, et quand il a répondu, elle a de
        // nouveau toute sa patience : pas seulement le reste de celle qui courait.
        var answered = AnsweredByUser(id);
        while (true)
        {
            var finished = await Task.WhenAny(answer.Task, Task.Delay(Patience), answered).ConfigureAwait(true);
            if (finished == answer.Task)
            {
                return await answer.Task.ConfigureAwait(true);
            }
            if (finished == answered)
            {
                answered = AnsweredByUser(id);
            }
            else if (!_waitingOnUser.Contains(id))
            {
                Stop();
                return new ExtensionAnswer(null, $"extension « {manifest.Name} » : pas de réponse");
            }
        }
    }

    /// <summary>Se termine la prochaine fois que l'utilisateur aura répondu à cette extension.</summary>
    private Task AnsweredByUser(string id)
    {
        if (!_answeredByUser.TryGetValue(id, out var answered))
        {
            answered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _answeredByUser[id] = answered;
        }
        return answered.Task;
    }

    /// <summary>Crée le cadre d'une extension au premier usage et charge son script ; rend ce qui l'en empêche.</summary>
    private async Task<string?> LoadAsync(ExtensionManifest manifest)
    {
        if (!_frames.TryGetValue(manifest.Id, out var loading))
        {
            loading = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _frames[manifest.Id] = loading;
            var parts = manifest.Entry.Split('/').Select(Uri.EscapeDataString);
            Post(new { kind = "charger", id = manifest.Id, url = $"{Origin}/{manifest.Id}/{string.Join('/', parts)}" });
        }
        if (await Task.WhenAny(loading.Task, Task.Delay(Patience)).ConfigureAwait(true) != loading.Task)
        {
            Stop();
            return "ne répond pas";
        }
        var error = await loading.Task.ConfigureAwait(true);
        if (error is not null)
        {
            Forget(manifest.Id);
        }
        return error;
    }

    private void Post(object message) =>
        _controller?.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));

    private void Stop()
    {
        foreach (var pending in _pending.Values)
        {
            pending.Answer.TrySetResult(new ExtensionAnswer(null, $"extension « {pending.Name} » : le moteur s'est arrêté"));
        }
        _pending.Clear();
        foreach (var loading in _frames.Values)
        {
            loading.TrySetResult("le moteur s'est arrêté");
        }
        _frames.Clear();
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
        catch (Exception exception) when (exception is TimeoutException or WebView2RuntimeNotFoundException or InvalidOperationException or COMException or ArgumentException)
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
            Width = HiddenSize,
            Height = HiddenSize,
        });
        var environment = await CoreWebView2Environment.CreateAsync(null, _dataFolder, new CoreWebView2EnvironmentOptions { Language = "fr-FR", AdditionalBrowserArguments = NoNetwork }).ConfigureAwait(true);
        var controller = await environment.CreateCoreWebView2ControllerAsync(_window.Handle).ConfigureAwait(true);
        controller.Bounds = new Rectangle(0, 0, HiddenSize, HiddenSize);
        controller.IsVisible = true;
        _controller = controller;
        var web = controller.CoreWebView2;
        VisualEditor.Harden(web);
        web.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        web.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
        web.WebResourceRequested += OnResourceRequested;
        await web.AddScriptToExecuteOnDocumentCreatedAsync(NoWebRtc).ConfigureAwait(true);
        web.WebMessageReceived += OnMessage;
        web.NavigationStarting += (_, e) => e.Cancel = !Allowed(e.Uri, StartAddress);
        web.FrameNavigationStarting += (_, e) => e.Cancel = !Allowed(e.Uri, null);
        web.ProcessFailed += (_, _) => Stop();
        _ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        web.Navigate(StartAddress);
        await _ready.Task.WaitAsync(StartPatience).ConfigureAwait(true);
    }

    /// <summary>Seules la page et l'adresse exacte d'un cadre se chargent : un cadre ne peut aller nulle part ailleurs.</summary>
    private bool Allowed(string address, string? page)
    {
        var allowed = address == page || (page is null && address.StartsWith(FramePrefix, StringComparison.Ordinal) && _frames.ContainsKey(address[FramePrefix.Length..]));
        if (!allowed)
        {
            Refused.Add(address);
        }
        return allowed;
    }

    private async void OnResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var web = (CoreWebView2)sender!;
        var address = new Uri(e.Request.Uri);
        using var deferral = e.GetDeferral();
        var served = address.Host == Host && e.Request.Method == "GET" ? await Task.Run(() => ContentOf(Uri.UnescapeDataString(address.AbsolutePath))).ConfigureAwait(true) : null;
        if (_disposed || _controller is null)
        {
            return;
        }
        if (served is null)
        {
            Refused.Add(e.Request.Uri);
        }
        e.Response = served is { } content
            ? web.Environment.CreateWebResourceResponse(new MemoryStream(content.Bytes), 200, "OK", Headers(content.Type, content.Policy))
            : web.Environment.CreateWebResourceResponse(null, 403, "Forbidden", "Cache-Control: no-store");
    }

    /// <summary>Ce que l'application sert : ses pages, et les fichiers d'une extension active. Rien d'autre n'existe.</summary>
    private Served? ContentOf(string path)
    {
        switch (path)
        {
            case "/hote.html":
                return new Served(ResourceOf("hote.html")!, HtmlType, PagePolicy);
            case "/hote.js":
                return new Served(ResourceOf("hote.js")!, ScriptType, FilePolicy);
            case "/cadre.js":
                return new Served(ResourceOf("cadre.js")!, ScriptType, FilePolicy);
            default:
                break;
        }
        if (path.StartsWith("/cadre/", StringComparison.Ordinal))
        {
            var id = path["/cadre/".Length..];
            return _library.Active(id) is null ? null : new Served(ResourceOf("cadre.html")!, HtmlType, FramePolicy(id));
        }
        var slash = path.IndexOf('/', 1);
        if (slash < 0)
        {
            return null;
        }
        var file = path[(slash + 1)..];
        return _library.Content(path[1..slash], file) is { } bytes ? new Served(bytes, ExtensionPath.ContentTypeOf(file)!, FilePolicy) : null;
    }

    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // Seule la page parle à l'application ; un cadre qui trouverait le canal ne serait pas écouté.
        if (e.Source != StartAddress)
        {
            Refused.Add(e.Source);
            return;
        }
        using var message = JsonDocument.Parse(e.WebMessageAsJson, MessageOptions);
        var root = message.RootElement;
        var kind = TextOf(root, "kind");
        if (kind == "pret")
        {
            _ready?.TrySetResult();
            return;
        }
        if (TextOf(root, "id") is not { } id)
        {
            return;
        }
        switch (kind)
        {
            case "charge":
                if (_frames.TryGetValue(id, out var loaded))
                {
                    loaded.TrySetResult(null);
                }
                break;
            case "erreur":
                if (_frames.TryGetValue(id, out var failed))
                {
                    failed.TrySetResult(TextOf(root, "erreur") ?? "erreur au chargement");
                }
                break;
            case "resultat":
                TakeResult(id, root);
                break;
            case "requete" when TextOf(root, "methode") == "vue.ouvrir":
                ViewAsked?.Invoke(this, new ExtensionEventArgs(id));
                Post(new { kind = "vers-cadre", id, message = new { type = "reponse", n = root.GetProperty("n").GetInt32(), resultat = (object?)null } });
                break;
            case "requete" when TextOf(root, "methode") is "selection.demander" or "fiches.ouvrir":
                _ = AnswerScreenAsync(id, root.GetProperty("n").GetInt32(), TextOf(root, "methode")!, root.GetProperty("args").Clone());
                break;
            case "requete":
                _ = AnswerRequestAsync(id, root.GetProperty("n").GetInt32(), root.GetProperty("methode").GetString()!, root.GetProperty("args").Clone());
                break;
            case "rafraichir":
                RefreshAsked?.Invoke(this, new ExtensionEventArgs(id));
                break;
            default:
                break;
        }
    }

    /// <summary>Un résultat n'est pris que pour une demande faite à cette extension : elle ne peut pas répondre à la place d'une autre.</summary>
    private void TakeResult(string id, JsonElement root)
    {
        var n = root.GetProperty("n").GetInt32();
        if (!_pending.TryGetValue(n, out var pending) || pending.Extension != id)
        {
            return;
        }
        _pending.Remove(n);
        if (TextOf(root, "erreur") is { } error)
        {
            pending.Answer.TrySetResult(new ExtensionAnswer(null, $"extension « {pending.Name} » : {error}"));
            return;
        }
        if (!root.TryGetProperty("contenu", out var tree) || tree.ValueKind != JsonValueKind.Array)
        {
            pending.Answer.TrySetResult(new ExtensionAnswer(null, null) { Value = root.TryGetProperty("valeur", out var only) && only.ValueKind != JsonValueKind.Null ? only.Clone() : null });
            return;
        }
        var value = root.TryGetProperty("valeur", out var given) && given.ValueKind != JsonValueKind.Null ? given.Clone() : (JsonElement?)null;
        var content = ExtensionContent.Read(tree);
        pending.Answer.TrySetResult(content.Succeeded
            ? new ExtensionAnswer(content.Value, null) { Value = value }
            : new ExtensionAnswer(null, $"extension « {pending.Name} » : {content.Error}"));
    }

    private async Task AnswerRequestAsync(string id, int n, string method, JsonElement arguments)
    {
        var answer = await _requests.AnswerAsync(id, method, arguments).ConfigureAwait(true);
        Post(new
        {
            kind = "vers-cadre",
            id,
            message = answer.Succeeded
                ? (object)new { type = "reponse", n, resultat = JsonDocument.Parse(answer.Value).RootElement }
                : new { type = "reponse", n, erreur = answer.Error },
        });
    }

    /// <summary>
    /// Ce qui passe par l'écran, permissions vérifiées : la sélection guidée (« selection ») et une fiche ouverte à un passage
    /// (« fiches:lecture »). Pendant que l'utilisateur répond, le temps ne court pas.
    /// </summary>
    private async Task AnswerScreenAsync(string id, int n, string method, JsonElement arguments)
    {
        var message = await ScreenAnswerAsync(id, n, method, arguments).ConfigureAwait(true);
        Post(new { kind = "vers-cadre", id, message });
    }

    private async Task<object> ScreenAnswerAsync(string id, int n, string method, JsonElement arguments)
    {
        object refused(string error) => new { type = "reponse", n, erreur = error };
        if (_library.Active(id) is not { } manifest)
        {
            return refused("extension inactive");
        }
        var right = method == "fiches.ouvrir" ? ExtensionRight.ReadSheets : ExtensionRight.Selection;
        var name = ExtensionRights.NameOf(right);
        if (!manifest.Permissions.Contains(right))
        {
            return refused($"permission « {name} » non demandée dans {ExtensionManifest.FileName}");
        }
        if (!_library.Granted(id).Contains(right))
        {
            return refused($"permission « {name} » retirée dans les Paramètres");
        }
        if (Screen is not { } screen)
        {
            return refused("l'application ne montre rien pour les extensions ici");
        }
        var first = TextAt(arguments, 0);
        var options = arguments.ValueKind == JsonValueKind.Array && arguments.GetArrayLength() > 1 && arguments[1].ValueKind == JsonValueKind.Object ? arguments[1] : default;
        if (method == "fiches.ouvrir")
        {
            var problem = screen.OpenSheet(first ?? string.Empty, NumberOf(options, "debut"), NumberOf(options, "fin"));
            return problem is null ? new { type = "reponse", n, resultat = (object?)null } : refused(problem);
        }
        _waitingOnUser.Add(id);
        try
        {
            var (answer, refusal) = await screen.AskSelectionAsync(new SelectionAsk(manifest.Name, first ?? string.Empty, options.ValueKind == JsonValueKind.Object ? TextOf(options, "fiche") : null, !IsFalse(options, "saisie"))).ConfigureAwait(true);
            return answer is null
                ? refused(refusal ?? "sélection impossible")
                : new
                {
                    type = "reponse",
                    n,
                    resultat = new { choix = answer.Choice, texte = answer.Text, fiche = answer.SheetId, titre = answer.SheetTitle, debut = answer.Start, fin = answer.End },
                };
        }
        finally
        {
            _waitingOnUser.Remove(id);
            if (_answeredByUser.Remove(id, out var answered))
            {
                answered.TrySetResult();
            }
        }
    }

    private static string? TextAt(JsonElement list, int index) =>
        list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > index && list[index].ValueKind == JsonValueKind.String ? list[index].GetString() : null;

    private static int? NumberOf(JsonElement options, string name) =>
        options.ValueKind == JsonValueKind.Object && options.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= 0
            ? number
            : null;

    private static bool IsFalse(JsonElement options, string name) =>
        options.ValueKind == JsonValueKind.Object && options.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.False;

    private sealed record Pending(string Extension, string Name, TaskCompletionSource<ExtensionAnswer> Answer);

    private sealed record Served(byte[] Bytes, string Type, string Policy);
}
