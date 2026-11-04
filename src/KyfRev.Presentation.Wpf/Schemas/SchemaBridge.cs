using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Modules.Schemas;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Schemas;

/// <summary>
/// Ce que l'îlot des schémas peut demander à l'application : une liste fermée d'appels. L'îlot ne voit ni le coffre ni le disque, et
/// un appel inconnu ou mal formé est refusé. Les noms sont ceux de la 0.x, que l'éditeur repris attend tels quels.
/// </summary>
internal sealed class SchemaBridge(SchemaUseCases schemas, IFolderPicker folders, Func<string?> pickFile, Action<string> openSheet, Action? editorClosed = null)
{
    private const string Saved = "enregistre";
    private const string Conflict = "conflit";

    /// <summary>Traite un message de l'îlot ; rend la réponse à lui envoyer, s'il en attend une.</summary>
    /// <param name="message">Le message, en JSON.</param>
    public async Task<string?> ReceiveAsync(string message)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(message) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
        if (root is null || Text(root["kind"]) != "call" || root["id"] is not JsonValue identifier || !identifier.TryGetValue<int>(out var id))
        {
            return null;
        }
        JsonObject answer;
        try
        {
            var (value, error) = await CallAsync(Text(root["method"]) ?? string.Empty, root["args"] as JsonArray ?? []).ConfigureAwait(true);
            answer = error is null
                ? new JsonObject { ["kind"] = "answer", ["id"] = id, ["ok"] = true, ["value"] = value }
                : new JsonObject { ["kind"] = "answer", ["id"] = id, ["ok"] = false, ["error"] = error };
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        {
            answer = new JsonObject { ["kind"] = "answer", ["id"] = id, ["ok"] = false, ["error"] = exception.Message };
        }
        return answer.ToJsonString();
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string Argument(JsonArray args, int index) =>
        index < args.Count && Text(args[index]) is { } text ? text : throw new ArgumentException(SchemaText.BadCall);

    private static (JsonNode? Value, string? Error) Answer<T>(Outcome<T> outcome, Func<T, JsonNode?> map) =>
        outcome.Succeeded ? (map(outcome.Value), null) : (null, outcome.Error);

    private static JsonObject Document(SchemaDocument document) =>
        new() { ["id"] = document.Id, ["texte"] = document.Text, ["empreinte"] = document.Fingerprint };

    private static JsonArray Summaries(IReadOnlyList<SchemaSummary> list)
    {
        var rows = new JsonArray();
        foreach (var schema in list)
        {
            var sheets = new JsonArray();
            foreach (var sheet in schema.Sheets)
            {
                sheets.Add(new JsonObject { ["id"] = sheet.Id, ["titre"] = sheet.Title });
            }
            rows.Add(new JsonObject
            {
                ["id"] = schema.Id,
                ["titre"] = schema.Title,
                ["equipements"] = schema.Devices,
                ["modifieLe"] = schema.ModifiedAt?.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                ["fiches"] = sheets,
            });
        }
        return rows;
    }

    private static JsonObject SaveAnswer(SaveResult result) => result switch
    {
        SheetSaved saved => new JsonObject { ["statut"] = Saved, ["empreinte"] = saved.Fingerprint },
        SaveConflict conflict => new JsonObject { ["statut"] = Conflict, ["empreinteDisque"] = conflict.DiskFingerprint, ["corpsDisque"] = conflict.DiskBody },
        _ => throw new InvalidOperationException(result.GetType().Name),
    };

    private async Task<(JsonNode? Value, string? Error)> CallAsync(string method, JsonArray args)
    {
        switch (method)
        {
            case "schemas":
                return Answer(await Task.Run(schemas.Schemas).ConfigureAwait(true), Summaries);
            case "lireSchema":
                var read = Argument(args, 0);
                return Answer(await Task.Run(() => schemas.Read(read)).ConfigureAwait(true), Document);
            case "creerSchema":
                var created = Argument(args, 0);
                return Answer(await Task.Run(() => schemas.Create(created)).ConfigureAwait(true), Document);
            case "enregistrerSchema":
                var (id, text, fingerprint) = (Argument(args, 0), Argument(args, 1), Argument(args, 2));
                return Answer(await Task.Run(() => schemas.Save(id, text, fingerprint)).ConfigureAwait(true), SaveAnswer);
            case "dupliquerSchema":
                var duplicated = Argument(args, 0);
                return Answer(await Task.Run(() => schemas.Duplicate(duplicated)).ConfigureAwait(true), Document);
            case "exporterSchema":
                var (folder, name, content) = (Argument(args, 0), Argument(args, 1), Convert.FromBase64String(Argument(args, 2)));
                return Answer(await Task.Run(() => schemas.Export(folder, name, content)).ConfigureAwait(true), path => path);
            case "choisirDossier":
                return (folders.Pick(Argument(args, 0)), null);
            case "choisirFichierSchema":
                return (pickFile(), null);
            case "lireImportSchema":
                var imported = Argument(args, 0);
                return Answer(await Task.Run(() => schemas.ReadImport(imported)).ConfigureAwait(true), importedText => importedText);
            case "ouvrirFiche":
                openSheet(Argument(args, 0));
                return (null, null);
            case "editeurFerme":
                editorClosed?.Invoke();
                return (null, null);
            default:
                throw new ArgumentException(SchemaText.BadCall);
        }
    }
}
