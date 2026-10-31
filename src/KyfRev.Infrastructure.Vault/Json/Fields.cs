using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault.Json;

/// <summary>
/// Lecture des champs d'un objet JSON du coffre, avec les mêmes exigences que la version 0.x
/// (types, bornes des entiers, formats de date) et des messages qui nomment le champ fautif.
/// </summary>
internal static partial class Fields
{
    private static readonly JsonDocumentOptions DocumentOptions = new() { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow };

    /// <summary>Analyse un fichier JSON dont la racine doit être un objet.</summary>
    public static JsonObject ParseObject(ReadOnlySpan<byte> utf8)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(utf8, documentOptions: DocumentOptions);
        }
        catch (JsonException exception)
        {
            throw new VaultFormatException($"JSON invalide : {exception.Message}", exception);
        }
        return root as JsonObject ?? throw new VaultFormatException("la racine doit être un objet JSON");
    }

    public static bool Has(JsonObject obj, string key) => obj.ContainsKey(key);

    public static JsonNode Required(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var node))
        {
            throw new VaultFormatException($"champ « {key} » manquant");
        }
        return node ?? throw new VaultFormatException($"champ « {key} » nul");
    }

    public static JsonObject Object(JsonObject obj, string key) =>
        Required(obj, key) as JsonObject ?? throw new VaultFormatException($"« {key} » doit être un objet");

    public static JsonObject? OptionalObject(JsonObject obj, string key) =>
        obj.TryGetPropertyValue(key, out var node) && node is not null
            ? node as JsonObject ?? throw new VaultFormatException($"« {key} » doit être un objet")
            : null;

    public static JsonArray Array(JsonObject obj, string key) =>
        Required(obj, key) as JsonArray ?? throw new VaultFormatException($"« {key} » doit être un tableau");

    /// <summary>Tableau facultatif : absent, il vaut vide ; <c>null</c> est toléré.</summary>
    public static JsonArray? OptionalArray(JsonObject obj, string key) =>
        obj.TryGetPropertyValue(key, out var node) && node is not null
            ? node as JsonArray ?? throw new VaultFormatException($"« {key} » doit être un tableau")
            : null;

    public static string String(JsonObject obj, string key) => StringValue(Required(obj, key), key);

    public static string? OptionalString(JsonObject obj, string key) =>
        obj.TryGetPropertyValue(key, out var node) && node is not null ? StringValue(node, key) : null;

    public static string StringValue(JsonNode node, string what) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : throw new VaultFormatException($"« {what} » doit être un texte");

    public static bool Boolean(JsonObject obj, string key) => Required(obj, key) switch
    {
        JsonValue value when value.GetValueKind() == JsonValueKind.True => true,
        JsonValue value when value.GetValueKind() == JsonValueKind.False => false,
        _ => throw new VaultFormatException($"« {key} » doit être vrai ou faux"),
    };

    public static bool OptionalBoolean(JsonObject obj, string key, bool fallback) =>
        obj.ContainsKey(key) ? Boolean(obj, key) : fallback;

    /// <summary>Entier dans les bornes du type Rust correspondant (u8, u16, u32, usize, i32).</summary>
    public static long Integer(JsonObject obj, string key, long min, long max) => IntegerValue(Required(obj, key), key, min, max);

    public static long IntegerValue(JsonNode node, string what, long min, long max)
    {
        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<long>(out var number) && number >= min && number <= max)
        {
            return number;
        }
        throw new VaultFormatException($"« {what} » doit être un entier entre {min} et {max}");
    }

    public static DateOnly Day(JsonObject obj, string key) => DayValue(String(obj, key), key);

    public static DateOnly? OptionalDay(JsonObject obj, string key) =>
        OptionalString(obj, key) is { } text ? DayValue(text, key) : null;

    public static DateOnly DayValue(string text, string what) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : throw new VaultFormatException($"« {what} » doit être une date AAAA-MM-JJ (« {text} »)");

    public static DateTimeOffset Instant(JsonObject obj, string key) => InstantText.Parse(String(obj, key), key);

    public static DateTimeOffset? OptionalInstant(JsonObject obj, string key) =>
        OptionalString(obj, key) is { } text ? InstantText.Parse(text, key) : null;

    public static VaultPath Path(JsonObject obj, string key)
    {
        var text = String(obj, key);
        return VaultPath.TryParse(text, out var path, out var reason)
            ? path
            : throw new VaultFormatException($"chemin « {text} » refusé : {reason}");
    }

    public static ImmutableArray<string> Strings(JsonArray? array, string what) =>
        array is null ? [] : [.. array.Select(item => item is null ? throw new VaultFormatException($"« {what} » contient null") : StringValue(item, what))];

    /// <summary>Propriétés inconnues, dans leur ordre, copiées pour être réécrites telles quelles.</summary>
    public static ImmutableArray<KeyValuePair<string, JsonNode?>> Unknown(JsonObject obj, params ReadOnlySpan<string> known)
    {
        var knownKeys = known.ToArray();
        return [.. obj.Where(pair => !knownKeys.Contains(pair.Key, StringComparer.Ordinal)).Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, pair.Value?.DeepClone()))];
    }

    /// <summary>Ajoute des propriétés inconnues conservées (copiées : un nœud n'a qu'un parent).</summary>
    public static void AddUnknown(JsonObject obj, IEnumerable<KeyValuePair<string, JsonNode?>> unknown)
    {
        foreach (var (key, value) in unknown)
        {
            obj[key] = value?.DeepClone();
        }
    }

    public static JsonArray StringArray(IEnumerable<string> values) => [.. values.Select(value => (JsonNode?)JsonValue.Create(value))];

    public static string DayText(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// Horodatages au format de <c>jiff::Timestamp</c> : écrits en temps universel (« Z »), fraction
/// de seconde sans zéros finaux ; lus avec n'importe quel décalage et jusqu'à neuf décimales
/// (au-delà de la précision de .NET, cent nanosecondes, la fraction est tronquée).
/// </summary>
internal static partial class InstantText
{
    public static string Format(DateTimeOffset instant)
    {
        var utc = instant.UtcDateTime;
        var text = utc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        var fraction = utc.Ticks % TimeSpan.TicksPerSecond;
        return fraction == 0
            ? text + "Z"
            : text + "." + fraction.ToString("0000000", CultureInfo.InvariantCulture).TrimEnd('0') + "Z";
    }

    public static DateTimeOffset Parse(string text, string what)
    {
        var match = Pattern().Match(text);
        if (!match.Success)
        {
            throw new VaultFormatException($"« {what} » doit être un horodatage RFC 3339 (« {text} »)");
        }
        try
        {
            var g = match.Groups;
            var offset = g["zone"].Value is "Z" or "z"
                ? TimeSpan.Zero
                : new TimeSpan(Number(g["oh"].Value), Number(g["om"].Value), 0) * (g["sign"].Value == "-" ? -1 : 1);
            var instant = new DateTimeOffset(
                Number(g["y"].Value), Number(g["mo"].Value), Number(g["d"].Value),
                Number(g["h"].Value), Number(g["mi"].Value), Number(g["s"].Value), offset);
            var digits = g["f"].Value;
            var ticks = digits.Length == 0 ? 0 : Number(digits.PadRight(7, '0')[..7]);
            return instant.AddTicks(ticks).ToUniversalTime();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new VaultFormatException($"« {what} » : horodatage hors limites (« {text} »)", exception);
        }
    }

    private static int Number(string digits) => int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2})[Tt ](?<h>\d{2}):(?<mi>\d{2}):(?<s>\d{2})(?:[.,](?<f>\d{1,9}))?(?<zone>[Zz]|(?<sign>[+-])(?<oh>\d{2}):?(?<om>\d{2}))$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
