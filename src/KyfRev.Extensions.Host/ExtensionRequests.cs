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
}

/// <summary>
/// Répond aux demandes d'une extension (<c>kyfrev.fiches…</c>, <c>kyfrev.stockage…</c>). C'est ici, et non dans la page, que ses
/// permissions sont vérifiées : à chaque demande, sur le manifeste installé, et l'extension doit être active. Aucune demande
/// n'écrit dans une fiche. La réponse est le texte JSON à lui rendre.
/// </summary>
public sealed class ExtensionRequests
{
    /// <summary>Ce qu'une extension peut garder dans le coffre, en octets.</summary>
    public const int MaxStoredBytes = 1024 * 1024;

    private readonly ExtensionLibrary _library;
    private readonly IExtensionData _data;

    /// <summary>Les demandes des extensions de cette bibliothèque.</summary>
    /// <param name="library">Les extensions installées.</param>
    /// <param name="data">Le coffre, tel que les extensions le voient.</param>
    public ExtensionRequests(ExtensionLibrary library, IExtensionData data)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(data);
        _library = library;
        _data = data;
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
            _ => Outcome.Refused<string>($"« {method} » n'existe pas"),
        };
    }

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
