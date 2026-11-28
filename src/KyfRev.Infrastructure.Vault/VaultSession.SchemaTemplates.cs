using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.Application.Text;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault;

/// <remarks>
/// Modèles de schéma de l'utilisateur (« Mes modèles ») : un fichier <c>schemas/modeles/&lt;id&gt;.json</c> par modèle, écrit
/// comme un schéma (son titre est le nom du modèle, sa description celle du modèle). Rangés à part, ils ne sont ni dans la
/// liste des schémas ni lisibles par une fiche : un nouveau schéma en est une copie, aux identifiants refaits par l'îlot. Un
/// modèle retiré va dans <c>schemas/modeles/.supprimes/</c>, sous la protection du journal d'opération : rien n'est effacé.
/// </remarks>
public sealed partial class VaultSession
{
    private const string TemplateFolder = VaultLayout.Schemas + "/modeles";
    private const string RemovedTemplateFolder = TemplateFolder + "/.supprimes";

    /// <summary>Les modèles de schéma de l'utilisateur, par nom ; un fichier qui ne se lit pas comme un schéma n'y est pas.</summary>
    public IReadOnlyList<SchemaContent> SchemaTemplates()
    {
        var folder = VaultLayout.Absolute(Root, TemplateFolder);
        if (!_storage.DirectoryExists(folder))
        {
            return [];
        }
        var templates = new List<(string Title, SchemaContent Content)>();
        foreach (var file in _storage.Files(folder, "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (VaultIds.IsSchemaId(id) && TemplateOf(file, id) is { } template)
            {
                templates.Add(template);
            }
        }
        return [.. templates
            .OrderBy(template => RustText.ToLowercase(template.Title), Utf8Order.Instance)
            .ThenBy(template => template.Content.Id, StringComparer.Ordinal)
            .Select(template => template.Content)];
    }

    /// <summary>Enregistre un modèle de schéma : vérifié comme un schéma, il reçoit un identifiant neuf.</summary>
    public SchemaContent CreateSchemaTemplate(string text)
    {
        var schema = VerifySchema(text, expectedId: null);
        var id = $"s_{_ids.Next()}";
        schema["id"] = id;
        var bytes = Encoding.UTF8.GetBytes(SchemaText(schema));
        var path = Confine(TemplateFile(id, removed: false));
        CreateDirectory(Path.GetDirectoryName(path)!);
        CreateFile(path, bytes);
        return new SchemaContent(id, Encoding.UTF8.GetString(bytes), Fingerprint.Of(bytes));
    }

    /// <summary>Retire un modèle de schéma : il n'est plus proposé, et son fichier est rangé à part, pas effacé.</summary>
    public void RemoveSchemaTemplate(string id)
    {
        var from = TemplateFile(id, removed: false);
        if (!_storage.FileExists(Confine(from)))
        {
            throw VaultException.UnknownSchema(id);
        }
        MoveUnderJournal($"retrait du modèle de schéma {id}", [new PlannedMove(from, TemplateFile(id, removed: true))]);
    }

    private static VaultPath TemplateFile(string id, bool removed)
    {
        ArgumentNullException.ThrowIfNull(id);
        return VaultIds.IsSchemaId(id)
            ? VaultPath.Create($"{(removed ? RemovedTemplateFolder : TemplateFolder)}/{id}.json")
            : throw VaultException.UnknownSchema(id);
    }

    /// <summary>Un modèle lu sur le disque, avec son nom ; rien s'il ne se lit pas comme un schéma portant son identifiant.</summary>
    private (string Title, SchemaContent Content)? TemplateOf(string file, string id)
    {
        try
        {
            var bytes = ReadAllBytes(file);
            if (!TryDecode(bytes, out var text))
            {
                return null;
            }
            var schema = VerifySchema(text, id);
            var title = schema["titre"] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : string.Empty;
            return (title, new SchemaContent(id, text, Fingerprint.Of(bytes)));
        }
        catch (VaultException)
        {
            return null;
        }
    }
}
