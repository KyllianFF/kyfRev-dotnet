using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.Infrastructure.Vault.Files;
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
/// détection des modifications concurrentes. Aucun schéma n'est jamais effacé.
/// </remarks>
public sealed partial class VaultSession
{
    /// <summary>Taille maximale d'un schéma : 8 Mo.</summary>
    public const int MaxSchemaBytes = 8 * 1024 * 1024;

    /// <summary>Dernier format de schéma connu de cette version.</summary>
    public const int SchemaFormat = 2;

    /// <summary>Lit un schéma.</summary>
    public SchemaContent ReadSchema(string id)
    {
        var path = SchemaPath(id);
        if (!File.Exists(path))
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
    private static string SchemaText(JsonObject schema) => SerdeJson.Pretty(SerdeJson.Canonical(schema)) + "\n";

    private string SchemaPath(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return VaultIds.IsSchemaId(id)
            ? Confine(VaultPath.Create($"{VaultLayout.Schemas}/{id}.json"))
            : throw VaultException.UnknownSchema(id);
    }
}
