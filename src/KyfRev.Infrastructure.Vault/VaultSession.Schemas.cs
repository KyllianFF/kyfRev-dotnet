using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.Application.Text;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault;

/// <summary>Un schéma lu sur le disque : son texte et la preuve de la version lue.</summary>
/// <param name="Id">Identifiant (<c>s_</c> + ULID).</param>
/// <param name="Text">Texte JSON.</param>
/// <param name="Fingerprint">À rendre à l'enregistrement : un enregistrement fait sur une autre version est refusé.</param>
public sealed record SchemaContent(string Id, string Text, string Fingerprint);

/// <remarks>
/// Schémas réseau : un fichier <c>schemas/&lt;id&gt;.json</c> par schéma. Le coffre ne connaît pas
/// leur modèle (il appartient à l'interface) : il vérifie seulement qu'un schéma est un objet JSON
/// raisonnable, au format connu, portant le bon identifiant, puis l'écrit de façon atomique avec
/// détection des modifications concurrentes. Aucun schéma n'est jamais effacé : un schéma archivé va dans
/// <c>schemas/archives/</c>, un schéma « supprimé » dans <c>schemas/.supprimes/</c>, d'où il se restaure. Les versions
/// qui ne connaissent pas ces dossiers ne les lisent pas, et n'y touchent pas.
/// </remarks>
public sealed partial class VaultSession
{
    /// <summary>Taille maximale d'un schéma : 8 Mo.</summary>
    public const int MaxSchemaBytes = 8 * 1024 * 1024;

    /// <summary>Dernier format de schéma connu de cette version.</summary>
    public const int SchemaFormat = 2;

    private const int MaxSchemaExportBytes = 4 * MaxSchemaBytes;
    private const string DefaultSchemaFileName = "Schéma réseau";

    /// <summary>Les schémas lisibles du coffre, par titre, avec les fiches qui les insèrent.</summary>
    public IReadOnlyList<SchemaSummary> Schemas() => SchemasIn(SchemaPlace.Active);

    /// <summary>Les schémas archivés et ceux de la corbeille, par titre.</summary>
    public IReadOnlyList<SchemaSummary> SchemasAside() => SchemasIn(SchemaPlace.Archived, SchemaPlace.Trash);

    /// <summary>
    /// Range un schéma : l'archive, le met à la corbeille, ou le remet dans la liste. Le fichier est déplacé tel quel,
    /// sous la protection du journal d'opération ; rien n'est effacé.
    /// </summary>
    public void MoveSchema(string id, SchemaPlace place)
    {
        var from = AllPlaces.Select(known => SchemaFile(id, known)).FirstOrDefault(path => _storage.FileExists(Confine(path)))
            ?? throw VaultException.UnknownSchema(id);
        var to = SchemaFile(id, place);
        if (from != to)
        {
            MoveUnderJournal($"rangement du schéma {id}", [new PlannedMove(from, to)]);
        }
    }

    private static readonly SchemaPlace[] AllPlaces = [SchemaPlace.Active, SchemaPlace.Archived, SchemaPlace.Trash];

    private static string SchemaFolder(SchemaPlace place) => place switch
    {
        SchemaPlace.Archived => VaultLayout.Schemas + "/archives",
        SchemaPlace.Trash => VaultLayout.Schemas + "/.supprimes",
        _ => VaultLayout.Schemas,
    };

    private static VaultPath SchemaFile(string id, SchemaPlace place)
    {
        ArgumentNullException.ThrowIfNull(id);
        return VaultIds.IsSchemaId(id) ? VaultPath.Create($"{SchemaFolder(place)}/{id}.json") : throw VaultException.UnknownSchema(id);
    }

    private IReadOnlyList<SchemaSummary> SchemasIn(params SchemaPlace[] places)
    {
        var folders = places
            .Select(place => (Place: place, Folder: VaultLayout.Absolute(Root, SchemaFolder(place))))
            .Where(pair => _storage.DirectoryExists(pair.Folder))
            .ToList();
        if (folders.Count == 0)
        {
            return [];
        }
        // Qui insère quoi : une seule lecture de chaque fiche.
        var citations = new Dictionary<string, List<SchemaSheet>>(StringComparer.Ordinal);
        foreach (var entry in _sheets.Values.Where(entry => entry.Presence == FilePresence.Present))
        {
            string text;
            try
            {
                text = ReadSheet(entry.Sheet.Id).Text;
            }
            catch (VaultException)
            {
                continue;
            }
            foreach (var cited in References(text).Distinct(StringComparer.Ordinal))
            {
                if (!citations.TryGetValue(cited, out var sheets))
                {
                    citations[cited] = sheets = [];
                }
                sheets.Add(new SchemaSheet(entry.Sheet.Id, entry.Sheet.Title));
            }
        }
        var schemas = new List<SchemaSummary>();
        foreach (var (place, folder) in folders)
        {
            foreach (var file in _storage.Files(folder, "*.json"))
            {
                if (SummaryOf(file, citations) is { } summary)
                {
                    schemas.Add(summary with { Place = place });
                }
            }
        }
        return [.. schemas.OrderBy(schema => RustText.ToLowercase(schema.Title), Utf8Order.Instance).ThenBy(schema => schema.Id, StringComparer.Ordinal)];
    }

    /// <summary>Écrit un export dans <paramref name="folder"/>, hors du coffre, sans jamais écraser ; rend le chemin écrit.</summary>
    public string ExportSchema(string folder, string name, string extension, ReadOnlySpan<byte> content)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (extension is not ("json" or "svg" or "png" or "csv"))
        {
            throw VaultException.InvalidPath(extension, "export d'un schéma : JSON, SVG, PNG ou CSV seulement");
        }
        if (content.Length > MaxSchemaExportBytes)
        {
            throw VaultException.InvalidSchema("export de plus de 32 Mo");
        }
        CheckOutputFolder(folder);
        var stem = FileNames.ForTitle(name.Trim());
        return CreateWithoutOverwriting(folder, stem.Length == 0 ? DefaultSchemaFileName : stem, extension, content);
    }

    /// <summary>Lit un fichier choisi pour l'import d'un schéma : <c>.json</c>, au plus 8 Mo, en UTF-8. Rien n'est écrit ici.</summary>
    public static string ReadSchemaToImport(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw VaultException.InvalidPath(path, "un fichier .json est attendu");
        }
        byte[] bytes;
        try
        {
            if (new FileInfo(path).Length > MaxSchemaBytes)
            {
                throw VaultException.InvalidSchema("fichier de plus de 8 Mo");
            }
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw VaultException.Io(path, exception);
        }
        return TryDecode(bytes, out var text) ? text : throw VaultException.InvalidSchema("fichier non UTF-8");
    }

    /// <summary>Lit un schéma.</summary>
    public SchemaContent ReadSchema(string id)
    {
        var path = SchemaPath(id);
        if (!_storage.FileExists(path))
        {
            throw VaultException.UnknownSchema(id);
        }
        var bytes = ReadAllBytes(path);
        return TryDecode(bytes, out var text)
            ? new SchemaContent(id, text, Fingerprint.Of(bytes))
            : throw VaultException.InvalidSchema("fichier non UTF-8");
    }

    /// <summary>Crée un schéma (nouveau, importé ou dupliqué) : vérifié, il reçoit un identifiant neuf.</summary>
    public SchemaContent CreateSchema(string text)
    {
        var schema = VerifySchema(text, expectedId: null);
        var id = $"s_{_ids.Next()}";
        schema["id"] = id;
        var bytes = Encoding.UTF8.GetBytes(SchemaText(schema));
        var path = SchemaPath(id);
        CreateDirectory(Path.GetDirectoryName(path)!);
        CreateFile(path, bytes);
        return new SchemaContent(id, Encoding.UTF8.GetString(bytes), Fingerprint.Of(bytes));
    }

    /// <summary>
    /// Enregistre une nouvelle version d'un schéma, si le fichier est toujours celui qui a été lu.
    /// </summary>
    /// <returns>La nouvelle empreinte.</returns>
    /// <exception cref="VaultException">Conflit si le fichier a changé depuis sa lecture : rien n'est écrit.</exception>
    public string SaveSchema(string id, string text, string expectedFingerprint)
    {
        _ = VerifySchema(text, id);
        var current = ReadSchema(id);
        if (current.Fingerprint != expectedFingerprint)
        {
            throw VaultException.Conflict(current.Fingerprint);
        }
        var bytes = Encoding.UTF8.GetBytes(text);
        ReplaceFile(SchemaPath(id), bytes);
        return Fingerprint.Of(bytes);
    }

    /// <summary>Copie indépendante d'un schéma, titrée « … (copie) ».</summary>
    public SchemaContent DuplicateSchema(string id)
    {
        var read = ReadSchema(id);
        var schema = VerifySchema(read.Text, id);
        var title = schema["titre"] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : "Schéma réseau";
        schema["titre"] = $"{title} (copie)";
        return CreateSchema(SchemaText(schema));
    }

    /// <summary>
    /// Vérifie qu'un texte est un schéma acceptable : objet JSON d'au plus 8 Mo, au format 1 à
    /// <see cref="SchemaFormat"/>, portant <paramref name="expectedId"/> s'il est donné.
    /// </summary>
    private static JsonObject VerifySchema(string text, string? expectedId)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Encoding.UTF8.GetByteCount(text) > MaxSchemaBytes)
        {
            throw VaultException.InvalidSchema("plus de 8 Mo");
        }
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
            // Les doublons de clés ne se révèlent qu'au parcours de l'objet.
            _ = node?.ToJsonString();
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw VaultException.InvalidSchema($"JSON illisible ({exception.Message})");
        }
        if (node is not JsonObject schema)
        {
            throw VaultException.InvalidSchema("un objet JSON est attendu");
        }
        var format = (schema["format"] is JsonValue number
            && number.GetValueKind() == JsonValueKind.Number
            && ulong.TryParse(number.ToJsonString(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (ulong?)null) ?? throw VaultException.InvalidSchema("numéro de format absent");
        if (format is < 1 or > SchemaFormat)
        {
            throw VaultException.InvalidSchema($"format {format} inconnu (cette version de kyfRev lit jusqu'au format {SchemaFormat})");
        }
        if (expectedId is not null
            && !(schema["id"] is JsonValue id && id.GetValueKind() == JsonValueKind.String && id.GetValue<string>() == expectedId))
        {
            throw VaultException.InvalidSchema("l'identifiant ne correspond pas au fichier");
        }
        return schema;
    }

    /// <summary>Texte d'un schéma comme la version 0.x l'écrit : clés triées, indentation de deux espaces, saut de ligne final.</summary>
    /// <summary>Le résumé d'un fichier de schéma ; rien s'il ne se lit pas : la vue Santé s'en occupe.</summary>
    private SchemaSummary? SummaryOf(string file, Dictionary<string, List<SchemaSheet>> citations)
    {
        var id = Path.GetFileNameWithoutExtension(file);
        JsonObject schema;
        DateTimeOffset modified;
        try
        {
            schema = VerifySchema(_storage.ReadAllText(file), id);
            modified = new DateTimeOffset(_storage.LastWriteTimeUtc(file), TimeSpan.Zero);
        }
        catch (Exception exception) when (exception is VaultException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
        var title = schema["titre"] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : string.Empty;
        var devices = schema["equipements"] is JsonArray list ? list.Count : 0;
        var sheets = citations.TryGetValue(id, out var citing) ? citing : [];
        return new SchemaSummary(id, title, devices, modified, [.. sheets.OrderBy(sheet => RustText.ToLowercase(sheet.Title), Utf8Order.Instance)]);
    }

    /// <summary>Les identifiants de schéma cités par une fiche (<c>schema: s_…</c>).</summary>
    private static IEnumerable<string> References(string text)
    {
        const string marker = "schema:";
        for (var at = text.IndexOf(marker, StringComparison.Ordinal); at >= 0; at = text.IndexOf(marker, at + marker.Length, StringComparison.Ordinal))
        {
            var start = at + marker.Length;
            while (start < text.Length && text[start] is ' ' or '\t')
            {
                start++;
            }
            var end = start;
            while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] == '_'))
            {
                end++;
            }
            var id = text[start..end];
            if (VaultIds.IsSchemaId(id))
            {
                yield return id;
            }
        }
    }

    private static string SchemaText(JsonObject schema) => SerdeJson.Pretty(SerdeJson.Canonical(schema)) + "\n";

    /// <summary>
    /// Le fichier d'un schéma : dans la liste, ou aux archives s'il y est (une fiche qui l'insère le montre toujours, et il
    /// s'y modifie). Un schéma à la corbeille est introuvable tant qu'il n'est pas restauré.
    /// </summary>
    private string SchemaPath(string id)
    {
        var active = Confine(SchemaFile(id, SchemaPlace.Active));
        var archived = Confine(SchemaFile(id, SchemaPlace.Archived));
        return !_storage.FileExists(active) && _storage.FileExists(archived) ? archived : active;
    }
}
