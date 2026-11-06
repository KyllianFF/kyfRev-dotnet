using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.Application;

namespace KyfRev.Extensions.Host;

/// <summary>Un fichier d'une extension du catalogue : son chemin dans son dossier, sa taille et son empreinte.</summary>
/// <param name="Path">Son chemin, avec des « / ».</param>
/// <param name="Size">Sa taille, en octets.</param>
/// <param name="Sha256">Son empreinte SHA-256, en hexadécimal minuscule.</param>
public sealed record CatalogueFile(string Path, long Size, string Sha256);

/// <summary>Une extension du catalogue.</summary>
/// <param name="Id">Son identifiant, celui de son manifeste et de son dossier.</param>
/// <param name="Name">Son nom.</param>
/// <param name="Version">Sa version.</param>
/// <param name="Summary">Ce qu'elle fait, en une phrase.</param>
/// <param name="Permissions">Ce qu'elle demande.</param>
/// <param name="Files">Ses fichiers.</param>
public sealed record CatalogueEntry(string Id, string Name, string Version, string Summary, ImmutableArray<string> Permissions, ImmutableArray<CatalogueFile> Files);

/// <summary>Un catalogue vérifié.</summary>
/// <param name="Number">Son numéro : chaque publication l'augmente, et un catalogue plus ancien que le dernier vu est refusé.</param>
/// <param name="PublishedOn">Le jour de sa publication.</param>
/// <param name="KeyId">La clé qui l'a signé.</param>
/// <param name="Extensions">Ses extensions.</param>
public sealed record Catalogue(int Number, DateOnly PublishedOn, string KeyId, ImmutableArray<CatalogueEntry> Extensions);

/// <summary>Une clé publique de publication, que l'application connaît.</summary>
/// <param name="Id">Son nom (« kyfrev-1 »).</param>
/// <param name="PublicKey">Sa clé publique, en PEM (SubjectPublicKeyInfo, ECDSA P-256).</param>
public sealed record CatalogueKey(string Id, string PublicKey);

/// <summary>
/// Le catalogue signé des extensions. Il est publié dans le dépôt (<c>catalogue/catalogue.json</c> et
/// <c>catalogue/catalogue.sig</c>), signé sur le poste de la personne qui publie, avec une clé que personne d'autre ne
/// garde ; l'application ne connaît que les clés publiques. Un catalogue n'est pris que si sa signature est bonne, par
/// une clé connue, et si son numéro n'est pas plus ancien que le dernier vu : on ne peut pas lui resservir une version
/// signée plus ancienne. Chaque fichier d'une extension est ensuite vérifié par sa taille et son empreinte.
/// </summary>
public static class ExtensionCatalogue
{
    /// <summary>La version du format.</summary>
    public const int Format = 1;

    /// <summary>Un catalogue plus gros est refusé avant d'être lu.</summary>
    public const int MaxCatalogueBytes = 1024 * 1024;

    /// <summary>Une extension du catalogue a au plus autant de fichiers que l'installation en accepte.</summary>
    public const int MaxFiles = 200;

    private static readonly JsonSerializerOptions Written = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Les clés publiques de publication que cette version de kyfRev connaît.</summary>
    public static ImmutableArray<CatalogueKey> KnownKeys { get; } = ReadKeys();

    /// <summary>Vérifie un catalogue et sa signature ; rend le catalogue, ou la raison du refus.</summary>
    /// <param name="catalogue">Les octets exacts de <c>catalogue.json</c>.</param>
    /// <param name="signature">Les octets de <c>catalogue.sig</c>.</param>
    /// <param name="keys">Les clés publiques connues.</param>
    /// <param name="lastSeen">Le numéro du dernier catalogue pris sur ce poste ; rien si aucun.</param>
    public static Outcome<Catalogue> Verify(byte[] catalogue, byte[] signature, IReadOnlyList<CatalogueKey> keys, int? lastSeen)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(keys);
        if (catalogue.Length > MaxCatalogueBytes)
        {
            return Outcome.Refused<Catalogue>("Le catalogue est trop gros : il n'est pas lu.");
        }
        if (ReadSignature(signature) is not var (keyId, bytes))
        {
            return Outcome.Refused<Catalogue>("La signature du catalogue est illisible : il n'est pas pris.");
        }
        if (keys.FirstOrDefault(key => key.Id == keyId) is not { } known)
        {
            return Outcome.Refused<Catalogue>($"Le catalogue est signé par une clé inconnue (« {keyId} ») : il n'est pas pris.");
        }
        using (var key = ECDsa.Create())
        {
            key.ImportFromPem(known.PublicKey);
            if (!key.VerifyData(catalogue, bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            {
                return Outcome.Refused<Catalogue>("La signature du catalogue ne correspond pas : il a pu être modifié, il n'est pas pris.");
            }
        }
        if (Parse(catalogue, keyId) is not { } read)
        {
            return Outcome.Refused<Catalogue>("Le catalogue est signé mais illisible : il n'est pas pris.");
        }
        if (read.Number < lastSeen)
        {
            return Outcome.Refused<Catalogue>($"Ce catalogue (n° {read.Number}) est plus ancien que le dernier vu (n° {lastSeen}) : il n'est pas pris.");
        }
        return Outcome.Of(read);
    }

    /// <summary>Vérifie un fichier téléchargé : sa taille et son empreinte doivent être celles du catalogue.</summary>
    public static bool Matches(CatalogueFile file, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(content);
        return content.Length == file.Size && string.Equals(Hash(content), file.Sha256, StringComparison.Ordinal);
    }

    /// <summary>L'empreinte SHA-256 d'un contenu, en hexadécimal minuscule.</summary>
    public static string Hash(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>L'entrée du catalogue d'une extension, tirée de son dossier : son manifeste et chacun de ses fichiers.</summary>
    /// <param name="folder">Le dossier de l'extension (<c>extensions/quiz</c>).</param>
    public static Outcome<CatalogueEntry> EntryOf(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var manifest = ExtensionManifest.Parse(File.ReadAllBytes(Path.Combine(folder, ExtensionManifest.FileName)));
        if (!manifest.Succeeded)
        {
            return Outcome.Refused<CatalogueEntry>(manifest.Error!);
        }
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(path => (Relative: Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/'), Content: File.ReadAllBytes(path)))
            .Select(file => new CatalogueFile(file.Relative, file.Content.Length, Hash(file.Content)))
            .ToImmutableArray();
        var value = manifest.Value;
        return Outcome.Of(new CatalogueEntry(
            value.Id,
            value.Name,
            value.Version,
            value.Description,
            [.. value.Permissions.Select(ExtensionRights.NameOf)],
            files));
    }

    /// <summary>
    /// Ce qui ne va pas entre un catalogue et le dossier des extensions publiées : une extension ou un fichier qui manque,
    /// ou qui diffère. Rien : le catalogue décrit exactement ce que l'application téléchargera.
    /// </summary>
    public static IReadOnlyList<string> Differences(Catalogue catalogue, string extensions)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(extensions);
        var differences = new List<string>();
        foreach (var entry in catalogue.Extensions)
        {
            var folder = Path.Combine(extensions, entry.Id);
            if (!Directory.Exists(folder))
            {
                differences.Add($"{entry.Id} : dossier absent");
                continue;
            }
            foreach (var file in entry.Files)
            {
                var path = Path.Combine(folder, file.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path) || !Matches(file, File.ReadAllBytes(path)))
                {
                    differences.Add($"{entry.Id}/{file.Path} : différent du catalogue");
                }
            }
            if (Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Count() != entry.Files.Length)
            {
                differences.Add($"{entry.Id} : des fichiers ne sont pas dans le catalogue");
            }
        }
        return differences;
    }

    /// <summary>Écrit un catalogue, toujours de la même façon : ce sont ces octets qui sont signés.</summary>
    public static byte[] Write(int number, DateOnly publishedOn, IEnumerable<CatalogueEntry> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        var root = new JsonObject
        {
            ["format"] = Format,
            ["numero"] = number,
            ["publieLe"] = publishedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["extensions"] = new JsonArray([.. extensions.OrderBy(entry => entry.Id, StringComparer.Ordinal).Select(entry => (JsonNode)new JsonObject
            {
                ["id"] = entry.Id,
                ["nom"] = entry.Name,
                ["version"] = entry.Version,
                ["resume"] = entry.Summary,
                ["permissions"] = new JsonArray([.. entry.Permissions.Select(permission => (JsonNode?)permission)]),
                ["fichiers"] = new JsonArray([.. entry.Files.OrderBy(file => file.Path, StringComparer.Ordinal).Select(file => (JsonNode)new JsonObject
                {
                    ["chemin"] = file.Path,
                    ["taille"] = file.Size,
                    ["sha256"] = file.Sha256,
                })]),
            })]),
        };
        return Encoding.UTF8.GetBytes(root.ToJsonString(Written).ReplaceLineEndings("\n") + "\n");
    }

    /// <summary>Signe un catalogue : la signature et le nom de la clé, en JSON.</summary>
    public static byte[] Sign(byte[] catalogue, ECDsa key, string keyId)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(keyId);
        var signature = key.SignData(catalogue, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var root = new JsonObject { ["cle"] = keyId, ["signature"] = Convert.ToBase64String(signature) };
        return Encoding.UTF8.GetBytes(root.ToJsonString(Written).ReplaceLineEndings("\n") + "\n");
    }

    private static (string KeyId, byte[] Bytes)? ReadSignature(byte[] signature)
    {
        try
        {
            var root = JsonNode.Parse(signature)?.AsObject();
            return root?["cle"]?.GetValue<string>() is { Length: > 0 } keyId && root["signature"]?.GetValue<string>() is { } text
                ? (keyId, Convert.FromBase64String(text))
                : null;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static Catalogue? Parse(byte[] catalogue, string keyId)
    {
        try
        {
            var root = Object(JsonNode.Parse(catalogue));
            if (Value<int>(root, "format") != Format)
            {
                return null;
            }
            var extensions = Array(root, "extensions").Select(node => Object(node)).Select(entry => new CatalogueEntry(
                Value<string>(entry, "id"),
                Value<string>(entry, "nom"),
                Value<string>(entry, "version"),
                entry.ContainsKey("resume") ? Value<string>(entry, "resume") : string.Empty,
                [.. Array(entry, "permissions").Select(permission => Object<string>(permission))],
                [.. Array(entry, "fichiers").Select(node => Object(node)).Select(file => new CatalogueFile(
                    Value<string>(file, "chemin"),
                    Value<long>(file, "taille"),
                    Value<string>(file, "sha256")))])).ToImmutableArray();
            var valid = extensions.All(entry => ExtensionManifest.IsValidId(entry.Id)
                && entry.Files.Length is > 0 and <= MaxFiles
                && entry.Files.All(file => ExtensionPath.Refusal(file.Path) is null && file.Size >= 0 && file.Sha256.Length == 64));
            return valid
                ? new Catalogue(Value<int>(root, "numero"), DateOnly.ParseExact(Value<string>(root, "publieLe"), "yyyy-MM-dd", CultureInfo.InvariantCulture), keyId, extensions)
                : null;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static JsonObject Object(JsonNode? node) => node as JsonObject ?? throw new FormatException("objet attendu");

    private static T Object<T>(JsonNode? node) => node is JsonValue value ? value.GetValue<T>() : throw new FormatException("valeur attendue");

    private static T Value<T>(JsonObject parent, string key) => Object<T>(parent[key]);

    private static JsonArray Array(JsonObject parent, string key) => parent[key] as JsonArray ?? throw new FormatException($"« {key} » attendu");

    /// <summary>Les clés publiques livrées avec l'application (<c>cles-catalogue.json</c>, embarqué).</summary>
    private static ImmutableArray<CatalogueKey> ReadKeys()
    {
        using var stream = typeof(ExtensionCatalogue).Assembly.GetManifestResourceStream("cles-catalogue.json");
        if (stream is null)
        {
            return [];
        }
        var root = JsonNode.Parse(stream) as JsonArray ?? [];
        return [.. root.Select(node => Object(node)).Select(key => new CatalogueKey(Value<string>(key, "id"), Value<string>(key, "publique")))];
    }
}
