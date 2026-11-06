using System.Text.Json;
using System.Text.Json.Serialization;
using KyfRev.Application;

namespace KyfRev.Extensions.Host;

/// <summary>Une fiche, telle qu'une extension la voit dans une liste.</summary>
/// <param name="Id">Son identifiant.</param>
/// <param name="Title">Son titre.</param>
/// <param name="Folder">Son dossier.</param>
/// <param name="Tags">Ses étiquettes.</param>
/// <param name="IsArchived">Elle est archivée.</param>
public sealed record ExtensionSheet(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("titre")] string Title,
    [property: JsonPropertyName("dossier")] string Folder,
    [property: JsonPropertyName("etiquettes")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("archivee")] bool IsArchived);

/// <summary>Le texte d'une fiche, tel qu'une extension le lit.</summary>
/// <param name="Id">Son identifiant.</param>
/// <param name="Title">Son titre.</param>
/// <param name="Text">Son texte Markdown.</param>
public sealed record ExtensionSheetText(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("titre")] string Title,
    [property: JsonPropertyName("texte")] string Text);

/// <summary>Une fiche qu'une extension crée.</summary>
/// <param name="Title">Son titre.</param>
/// <param name="Folder">Son dossier, relatif à <c>fiches/</c> ; rien : la racine.</param>
/// <param name="Text">Son texte Markdown ; rien : une fiche vide.</param>
/// <param name="Tags">Ses étiquettes.</param>
public sealed record ExtensionNewSheet(string Title, string? Folder, string? Text, IReadOnlyList<string> Tags);

/// <summary>Une ligne du journal d'une extension : ce qu'elle a écrit dans le coffre, et quand.</summary>
/// <param name="At">Quand.</param>
/// <param name="Action">Ce qu'elle a fait : <see cref="Creation"/>, <see cref="Tagging"/> ou <see cref="Writing"/> ; <see cref="Unreadable"/> pour une ligne abîmée.</param>
/// <param name="Sheet">L'identifiant de la fiche.</param>
/// <param name="Title">Le titre de la fiche, à ce moment-là.</param>
/// <param name="Detail">Un mot de plus : le dossier, les étiquettes, ou la taille du texte avant et après.</param>
public sealed record ExtensionJournalEntry(
    [property: JsonPropertyName("quand")] DateTimeOffset At,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("fiche")] string Sheet,
    [property: JsonPropertyName("titre")] string Title,
    [property: JsonPropertyName("detail")] string? Detail)
{
    /// <summary>Elle a créé la fiche.</summary>
    public const string Creation = "creation";

    /// <summary>Elle a changé ses étiquettes.</summary>
    public const string Tagging = "etiquettes";

    /// <summary>Elle a remplacé son texte ; le précédent est dans les versions.</summary>
    public const string Writing = "ecriture";

    /// <summary>Une ligne du journal que kyfRev ne sait pas lire : elle reste telle quelle dans le fichier.</summary>
    public const string Unreadable = "illisible";

    /// <summary>La plus grande part d'une ligne illisible gardée pour la montrer.</summary>
    public const int UnreadableShown = 80;

    /// <summary>Une ligne illisible du journal, montrée pour ce qu'elle est, après les autres.</summary>
    /// <param name="line">La ligne, telle qu'elle est dans le fichier.</param>
    public static ExtensionJournalEntry UnreadableLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new(DateTimeOffset.MinValue, Unreadable, string.Empty, string.Empty, line.Length > UnreadableShown ? line[..UnreadableShown] : line);
    }
}

/// <summary>Ce qu'une extension peut obtenir du coffre, une fois ses permissions vérifiées.</summary>
public interface IExtensionData
{
    /// <summary>La liste des fiches.</summary>
    Task<Outcome<IReadOnlyList<ExtensionSheet>>> SheetsAsync();

    /// <summary>Le texte d'une fiche.</summary>
    /// <param name="id">Son identifiant.</param>
    Task<Outcome<ExtensionSheetText>> SheetAsync(string id);

    /// <summary>Les données d'une extension dans le coffre, en JSON ; rien si elle n'en a pas.</summary>
    /// <param name="extension">Son identifiant.</param>
    Task<Outcome<string?>> StoredAsync(string extension);

    /// <summary>Remplace les données d'une extension dans le coffre.</summary>
    /// <param name="extension">Son identifiant.</param>
    /// <param name="json">Ses données, en JSON.</param>
    Task<Outcome<Done>> StoreAsync(string extension, string json);

    /// <summary>Crée une fiche, et le note au journal de l'extension ; rend son identifiant.</summary>
    /// <param name="extension">L'extension qui la crée.</param>
    /// <param name="sheet">La fiche.</param>
    Task<Outcome<string>> CreateSheetAsync(string extension, ExtensionNewSheet sheet);

    /// <summary>Remplace les étiquettes d'une fiche, et le note au journal ; rend celles enregistrées.</summary>
    /// <param name="extension">L'extension qui les change.</param>
    /// <param name="id">L'identifiant de la fiche.</param>
    /// <param name="tags">Ses étiquettes.</param>
    Task<Outcome<IReadOnlyList<string>>> SetTagsAsync(string extension, string id, IReadOnlyList<string> tags);

    /// <summary>Remplace le texte d'une fiche, l'en-tête gardé ; le précédent va dans ses versions, l'écriture au journal.</summary>
    /// <param name="extension">L'extension qui écrit.</param>
    /// <param name="id">L'identifiant de la fiche.</param>
    /// <param name="text">Son nouveau texte Markdown.</param>
    Task<Outcome<Done>> WriteSheetAsync(string extension, string id, string text);

    /// <summary>Le journal d'une extension, du plus récent au plus ancien.</summary>
    /// <param name="extension">Son identifiant.</param>
    Task<Outcome<IReadOnlyList<ExtensionJournalEntry>>> JournalAsync(string extension);
}

/// <summary>
/// Répond aux demandes d'une extension (<c>kyfrev.fiches…</c>, <c>kyfrev.stockage…</c>). C'est ici, et non dans la page, que ses
/// permissions sont vérifiées : à chaque demande, sur le manifeste installé, et l'extension doit être active. Une extension
/// peut créer des fiches et changer leurs étiquettes (<c>fiches:creation</c>), remplacer leur texte (<c>fiches:ecriture</c>),
/// jamais en supprimer ; chaque écriture va à son journal, et le texte remplacé dans les versions de la fiche. La réponse
/// est le texte JSON à lui rendre.
/// </summary>
public sealed class ExtensionRequests
{
    /// <summary>Ce qu'une extension peut garder dans le coffre, en octets.</summary>
    public const int MaxStoredBytes = 1024 * 1024;

    /// <summary>Le texte le plus long qu'une extension peut écrire dans une fiche, en octets.</summary>
    public const int MaxSheetTextBytes = 1024 * 1024;

    /// <summary>Le titre le plus long d'une fiche qu'elle crée.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Le plus d'étiquettes qu'elle peut donner à une fiche.</summary>
    public const int MaxTags = 50;

    /// <summary>Le plus d'écritures (créations, étiquettes, textes) qu'une extension peut faire en une minute.</summary>
    public const int MaxWritesPerMinute = 60;

    private readonly ExtensionLibrary _library;
    private readonly IExtensionData _data;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, Queue<DateTimeOffset>> _writes = new(StringComparer.Ordinal);

    /// <summary>Les demandes des extensions de cette bibliothèque.</summary>
    /// <param name="library">Les extensions installées.</param>
    /// <param name="data">Le coffre, tel que les extensions le voient.</param>
    public ExtensionRequests(ExtensionLibrary library, IExtensionData data)
        : this(library, data, TimeProvider.System)
    {
    }

    /// <summary>Les demandes des extensions de cette bibliothèque, à l'heure de <paramref name="time"/>.</summary>
    /// <param name="library">Les extensions installées.</param>
    /// <param name="data">Le coffre, tel que les extensions le voient.</param>
    /// <param name="time">L'heure : elle compte les écritures de chaque minute.</param>
    public ExtensionRequests(ExtensionLibrary library, IExtensionData data, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(time);
        _library = library;
        _data = data;
        _time = time;
    }

    /// <summary>Répond à une demande : le texte JSON de la réponse, ou la raison du refus.</summary>
    /// <param name="extension">L'extension qui demande.</param>
    /// <param name="method">Ce qu'elle demande.</param>
    /// <param name="arguments">Ses arguments, le tableau JSON de la demande.</param>
    public async Task<Outcome<string>> AnswerAsync(string extension, string method, JsonElement arguments)
    {
        ArgumentNullException.ThrowIfNull(extension);
        ArgumentNullException.ThrowIfNull(method);
        if (_library.Active(extension) is not { } manifest)
        {
            return Outcome.Refused<string>("extension inactive");
        }
        var granted = _library.Granted(extension);
        return method switch
        {
            "fiches.lister" => Missing(manifest, granted, ExtensionRight.ReadSheets) ?? Json(await _data.SheetsAsync().ConfigureAwait(false)),
            "fiches.lire" => Missing(manifest, granted, ExtensionRight.ReadSheets) ?? Json(await _data.SheetAsync(FirstText(arguments)).ConfigureAwait(false)),
            "stockage.lire" => Missing(manifest, granted, ExtensionRight.Storage) ?? Stored(await _data.StoredAsync(extension).ConfigureAwait(false)),
            "stockage.ecrire" => Missing(manifest, granted, ExtensionRight.Storage) ?? await StoreAsync(extension, arguments).ConfigureAwait(false),
            "fiches.creer" => Missing(manifest, granted, ExtensionRight.CreateSheets) ?? TooMany(extension) ?? await CreateAsync(extension, arguments).ConfigureAwait(false),
            "fiches.etiqueter" => Missing(manifest, granted, ExtensionRight.CreateSheets) ?? TooMany(extension) ?? await TagAsync(extension, arguments).ConfigureAwait(false),
            "fiches.ecrire" => Missing(manifest, granted, ExtensionRight.WriteSheets) ?? TooMany(extension) ?? await WriteAsync(extension, arguments).ConfigureAwait(false),
            _ => Outcome.Refused<string>($"« {method} » n'existe pas"),
        };
    }

    /// <summary>Refuse une écriture de plus quand l'extension en a déjà fait <see cref="MaxWritesPerMinute"/> dans la minute.</summary>
    private Outcome<string>? TooMany(string extension)
    {
        var now = _time.GetUtcNow();
        lock (_writes)
        {
            if (!_writes.TryGetValue(extension, out var recent))
            {
                recent = new Queue<DateTimeOffset>();
                _writes[extension] = recent;
            }
            while (recent.Count > 0 && now - recent.Peek() >= TimeSpan.FromMinutes(1))
            {
                recent.Dequeue();
            }
            if (recent.Count >= MaxWritesPerMinute)
            {
                return Outcome.Refused<string>($"plus de {MaxWritesPerMinute} écritures en une minute : réessayez plus tard");
            }
            recent.Enqueue(now);
            return null;
        }
    }

    private async Task<Outcome<string>> CreateAsync(string extension, JsonElement arguments)
    {
        var title = FirstText(arguments).Trim();
        var options = arguments.ValueKind == JsonValueKind.Array && arguments.GetArrayLength() > 1 && arguments[1].ValueKind == JsonValueKind.Object
            ? arguments[1]
            : default;
        var folder = Text(options, "dossier");
        var text = Text(options, "texte");
        var tags = Tags(options.ValueKind == JsonValueKind.Object && options.TryGetProperty("etiquettes", out var given) ? given : default);
        var refused = title.Length == 0 ? "une fiche a besoin d'un titre"
            : title.Length > MaxTitleLength ? $"titre de plus de {MaxTitleLength} caractères"
            : TextRefused(text) ?? TagsRefused(tags);
        if (refused is not null)
        {
            return Outcome.Refused<string>(refused);
        }
        var created = await _data.CreateSheetAsync(extension, new ExtensionNewSheet(title, folder, text, tags!)).ConfigureAwait(false);
        return Json(created);
    }

    private async Task<Outcome<string>> TagAsync(string extension, JsonElement arguments)
    {
        var tags = Tags(arguments.ValueKind == JsonValueKind.Array && arguments.GetArrayLength() > 1 ? arguments[1] : default);
        if (TagsRefused(tags) is { } refused)
        {
            return Outcome.Refused<string>(refused);
        }
        return Json(await _data.SetTagsAsync(extension, FirstText(arguments), tags!).ConfigureAwait(false));
    }

    private async Task<Outcome<string>> WriteAsync(string extension, JsonElement arguments)
    {
        var text = arguments.ValueKind == JsonValueKind.Array && arguments.GetArrayLength() > 1 && arguments[1].ValueKind == JsonValueKind.String
            ? arguments[1].GetString()
            : null;
        if (text is null)
        {
            return Outcome.Refused<string>("le texte à écrire manque");
        }
        if (TextRefused(text) is { } refused)
        {
            return Outcome.Refused<string>(refused);
        }
        var written = await _data.WriteSheetAsync(extension, FirstText(arguments), text).ConfigureAwait(false);
        return written.Succeeded ? Outcome.Of("null") : Outcome.Refused<string>(written.Error!);
    }

    private static string? Text(JsonElement options, string name) =>
        options.ValueKind == JsonValueKind.Object && options.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Les étiquettes données : un tableau de textes ; rien si ce n'en est pas un.</summary>
    private static List<string>? Tags(JsonElement given) => given.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => [],
        JsonValueKind.Array when given.EnumerateArray().All(tag => tag.ValueKind == JsonValueKind.String) => [.. given.EnumerateArray().Select(tag => tag.GetString()!)],
        _ => null,
    };

    private static string? TagsRefused(List<string>? tags) =>
        tags is null ? "les étiquettes sont un tableau de textes"
        : tags.Count > MaxTags ? $"plus de {MaxTags} étiquettes"
        : tags.Any(tag => tag.Length > 100) ? "étiquette de plus de 100 caractères"
        : null;

    private static string? TextRefused(string? text) =>
        text is not null && System.Text.Encoding.UTF8.GetByteCount(text) > MaxSheetTextBytes ? "texte de plus de 1 Mo" : null;

    private static Outcome<string>? Missing(ExtensionManifest manifest, IReadOnlyList<ExtensionRight> granted, ExtensionRight permission)
    {
        var name = ExtensionRights.NameOf(permission);
        return !manifest.Permissions.Contains(permission) ? Outcome.Refused<string>($"permission « {name} » non demandée dans {ExtensionManifest.FileName}")
            : !granted.Contains(permission) ? Outcome.Refused<string>($"permission « {name} » retirée dans les Paramètres")
            : null;
    }

    private static string FirstText(JsonElement arguments) =>
        arguments.ValueKind == JsonValueKind.Array && arguments.GetArrayLength() > 0 && arguments[0].ValueKind == JsonValueKind.String
            ? arguments[0].GetString()!
            : string.Empty;

    private static Outcome<string> Json<T>(Outcome<T> outcome) =>
        outcome.Succeeded ? Outcome.Of(JsonSerializer.Serialize(outcome.Value)) : Outcome.Refused<string>(outcome.Error!);

    private static Outcome<string> Stored(Outcome<string?> outcome)
    {
        if (!outcome.Succeeded)
        {
            return Outcome.Refused<string>(outcome.Error!);
        }
        return Outcome.Of(outcome.Value ?? "null");
    }

    private async Task<Outcome<string>> StoreAsync(string extension, JsonElement arguments)
    {
        var json = arguments.ValueKind == JsonValueKind.Array && arguments.GetArrayLength() > 0 ? arguments[0].GetRawText() : "null";
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxStoredBytes)
        {
            return Outcome.Refused<string>("données de plus de 1 Mo");
        }
        var stored = await _data.StoreAsync(extension, json).ConfigureAwait(false);
        return stored.Succeeded ? Outcome.Of("null") : Outcome.Refused<string>(stored.Error!);
    }
}
