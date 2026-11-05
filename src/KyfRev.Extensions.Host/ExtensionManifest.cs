using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using KyfRev.Application;

namespace KyfRev.Extensions.Host;

/// <summary>Ce qu'une extension peut demander, et rien d'autre.</summary>
public enum ExtensionRight
{
    /// <summary>Lire la liste des fiches et leur texte, jamais les modifier (<c>fiches:lecture</c>).</summary>
    ReadSheets,

    /// <summary>Garder ses propres données dans le coffre (<c>stockage</c>).</summary>
    Storage,
}

/// <summary>
/// Le fichier <c>extension.json</c> d'une extension, aux champs fermés : un champ inconnu refuse l'extension. Le contrat est
/// celui de la 0.x (<c>extensions.rs</c>), pour que les extensions déjà écrites fonctionnent sans modification.
/// </summary>
/// <param name="Id">Identifiant, qui est aussi le nom de son dossier.</param>
/// <param name="Name">Nom affiché.</param>
/// <param name="Version">Version, telle que l'auteur l'écrit.</param>
/// <param name="Description">Ce qu'elle fait.</param>
/// <param name="Author">Son auteur, s'il se nomme.</param>
/// <param name="Entry">Script chargé dans le cadre isolé, relatif à son dossier.</param>
/// <param name="Permissions">Ce qu'elle demande.</param>
/// <param name="Blocks">Langages de bloc qu'elle dessine (<c>quiz</c> pour un bloc <c>```quiz</c>).</param>
/// <param name="ViewTitle">Libellé de sa vue dans le menu ; rien si elle n'en a pas.</param>
public sealed record ExtensionManifest(
    string Id,
    string Name,
    string Version,
    string Description,
    string? Author,
    string Entry,
    ImmutableArray<ExtensionRight> Permissions,
    ImmutableArray<string> Blocks,
    string? ViewTitle)
{
    /// <summary>Nom du fichier, à la racine du dossier d'une extension.</summary>
    public const string FileName = "extension.json";

    private const string DefaultEntry = "extension.js";
    private const int MinIdLength = 2;
    private const int MaxIdLength = 40;
    private const int MaxNameLength = 60;
    private const int MaxVersionBytes = 20;
    private const int MaxDescriptionLength = 300;
    private const int MaxBlocks = 10;
    private const int MaxBlockLength = 30;
    private const int MaxViewTitleLength = 40;

    private static readonly string[] Fields = ["id", "nom", "version", "description", "auteur", "entree", "permissions", "blocs", "vue"];

    /// <summary>Langages de bloc déjà pris par kyfRev.</summary>
    private static readonly string[] ReservedBlocks = ["mermaid", "reseau", "math", "tex", "katex", "mermaid-js", "smartart"];

    /// <summary>Vrai pour un identifiant d'extension : 2 à 40 minuscules, chiffres ou tirets, sans tiret aux bords.</summary>
    /// <param name="id">L'identifiant à vérifier.</param>
    public static bool IsValidId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.Length is >= MinIdLength and <= MaxIdLength && id.All(IsLowerOrDigitOrDash) && !id.StartsWith('-') && !id.EndsWith('-');
    }

    /// <summary>Lit et vérifie le contenu d'un <c>extension.json</c>.</summary>
    /// <param name="json">Le contenu du fichier.</param>
    public static Outcome<ExtensionManifest> Parse(ReadOnlyMemory<byte> json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return Read(document.RootElement);
        }
        catch (JsonException exception)
        {
            return Unreadable(exception.Message);
        }
    }

    private static Outcome<ExtensionManifest> Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Unreadable("un objet est attendu");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!Fields.Contains(property.Name, StringComparer.Ordinal))
            {
                return Unreadable($"champ « {property.Name} » inconnu");
            }
            if (!seen.Add(property.Name))
            {
                return Unreadable($"champ « {property.Name} » en double");
            }
        }
        if (Text(root, "id") is not { } id || Text(root, "nom") is not { } name || Text(root, "version") is not { } version)
        {
            return Unreadable("« id », « nom » et « version » sont obligatoires et sont des textes");
        }
        if (IsNotText(root, "description") || IsNotText(root, "auteur") || IsNotText(root, "entree"))
        {
            return Unreadable("« description », « auteur » et « entree » sont des textes");
        }
        if (!TryPermissions(root, out var permissions, out var refused))
        {
            return Unreadable(refused);
        }
        if (!TryBlocks(root, out var blocks))
        {
            return Unreadable("« blocs » est une liste de textes");
        }
        if (!TryView(root, out var view))
        {
            return Unreadable("« vue » est un objet avec un « titre »");
        }
        var manifest = new ExtensionManifest(
            id,
            name,
            version,
            Text(root, "description") ?? string.Empty,
            Text(root, "auteur"),
            (Text(root, "entree") ?? DefaultEntry).Replace('\\', '/'),
            permissions,
            blocks,
            view);
        return manifest.Refusal() is { } refusal ? Outcome.Refused<ExtensionManifest>(refusal) : Outcome.Of(manifest);
    }

    private static Outcome<ExtensionManifest> Unreadable(string reason) => Outcome.Refused<ExtensionManifest>($"{FileName} illisible : {reason}");

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool IsNotText(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.String;

    private static bool TryPermissions(JsonElement root, out ImmutableArray<ExtensionRight> permissions, out string refused)
    {
        permissions = [];
        refused = string.Empty;
        if (!root.TryGetProperty("permissions", out var list))
        {
            return true;
        }
        if (list.ValueKind != JsonValueKind.Array)
        {
            refused = "« permissions » est une liste";
            return false;
        }
        var read = ImmutableArray.CreateBuilder<ExtensionRight>();
        foreach (var item in list.EnumerateArray())
        {
            var text = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (text == "fiches:lecture")
            {
                read.Add(ExtensionRight.ReadSheets);
            }
            else if (text == "stockage")
            {
                read.Add(ExtensionRight.Storage);
            }
            else
            {
                refused = $"permission « {item} » inconnue";
                return false;
            }
        }
        permissions = read.ToImmutable();
        return true;
    }

    private static bool TryBlocks(JsonElement root, out ImmutableArray<string> blocks)
    {
        blocks = [];
        if (!root.TryGetProperty("blocs", out var list))
        {
            return true;
        }
        if (list.ValueKind != JsonValueKind.Array || list.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
        {
            return false;
        }
        blocks = [.. list.EnumerateArray().Select(item => item.GetString()!)];
        return true;
    }

    private static bool TryView(JsonElement root, out string? view)
    {
        view = null;
        if (!root.TryGetProperty("vue", out var value))
        {
            return true;
        }
        view = value.ValueKind == JsonValueKind.Object ? Text(value, "titre") : null;
        return view is not null;
    }

    private static bool IsLowerOrDigitOrDash(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';

    private static bool IsValidBlock(string language) =>
        language.Length is >= 1 and <= MaxBlockLength && language.All(IsLowerOrDigitOrDash) && !ReservedBlocks.Contains(language, StringComparer.Ordinal);

    private static int Letters(string text) => text.EnumerateRunes().Count();

    /// <summary>Ce qui empêche de garder ce manifeste ; rien s'il est bon.</summary>
    private string? Refusal()
    {
        if (!IsValidId(Id))
        {
            return $"identifiant « {Id} » invalide : 2 à 40 minuscules, chiffres ou tirets";
        }
        if (string.IsNullOrWhiteSpace(Name) || Letters(Name) > MaxNameLength)
        {
            return "nom vide ou de plus de 60 caractères";
        }
        if (string.IsNullOrWhiteSpace(Version) || Encoding.UTF8.GetByteCount(Version) > MaxVersionBytes)
        {
            return "version vide ou de plus de 20 caractères";
        }
        if (Letters(Description) > MaxDescriptionLength)
        {
            return "description de plus de 300 caractères";
        }
        if (ExtensionPath.Refusal(Entry) is { } entry)
        {
            return $"chemin « {Entry} » refusé : {entry}";
        }
        if (!Entry.EndsWith(".js", StringComparison.Ordinal) || Entry.Split('/').Any(part => part.StartsWith('.')))
        {
            return "l'entrée doit être un fichier .js, qui n'est pas caché";
        }
        if (Blocks.Length > MaxBlocks)
        {
            return "10 types de bloc au plus";
        }
        if (Blocks.FirstOrDefault(language => !IsValidBlock(language)) is { } block)
        {
            return $"type de bloc « {block} » invalide ou réservé";
        }
        if (ViewTitle is not null && (string.IsNullOrWhiteSpace(ViewTitle) || Letters(ViewTitle) > MaxViewTitleLength))
        {
            return "titre de vue vide ou de plus de 40 caractères";
        }
        return Blocks.IsEmpty && ViewTitle is null ? "l'extension n'ajoute ni bloc ni vue" : null;
    }
}
