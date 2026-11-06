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

    /// <summary>Voir le texte sélectionné dans une fiche, quand on clique son bouton (<c>selection</c>, contrat 2).</summary>
    Selection,

    /// <summary>Créer des fiches et changer leurs étiquettes (<c>fiches:creation</c>, contrat 2).</summary>
    CreateSheets,

    /// <summary>Modifier le texte des fiches, version gardée et journal (<c>fiches:ecriture</c>, contrat 2).</summary>
    WriteSheets,
}

/// <summary>Ce que risque l'utilisateur en accordant une permission : ce que dit la confirmation, et comment.</summary>
public enum ExtensionRisk
{
    /// <summary>Elle ne touche qu'à ce qui est à elle.</summary>
    Low,

    /// <summary>Elle voit le contenu des fiches, ou en crée.</summary>
    Notable,

    /// <summary>Elle modifie les fiches : une alerte rouge.</summary>
    High,
}

/// <summary>Les noms et le poids des permissions.</summary>
public static class ExtensionRights
{
    /// <summary>Le nom d'une permission dans <c>extension.json</c>.</summary>
    /// <param name="right">La permission.</param>
    public static string NameOf(ExtensionRight right) => right switch
    {
        ExtensionRight.ReadSheets => "fiches:lecture",
        ExtensionRight.Storage => "stockage",
        ExtensionRight.Selection => "selection",
        ExtensionRight.CreateSheets => "fiches:creation",
        _ => "fiches:ecriture",
    };

    /// <summary>Ce que risque l'utilisateur en l'accordant.</summary>
    /// <param name="right">La permission.</param>
    public static ExtensionRisk RiskOf(ExtensionRight right) => right switch
    {
        ExtensionRight.Storage => ExtensionRisk.Low,
        ExtensionRight.WriteSheets => ExtensionRisk.High,
        _ => ExtensionRisk.Notable,
    };

    /// <summary>La permission de ce nom ; rien pour un nom inconnu, ou réservé au contrat 2 dans un manifeste qui ne le déclare pas.</summary>
    /// <param name="name">Son nom dans <c>extension.json</c>.</param>
    /// <param name="api">Le contrat du manifeste.</param>
    public static ExtensionRight? Parse(string? name, int api) => name switch
    {
        "fiches:lecture" => ExtensionRight.ReadSheets,
        "stockage" => ExtensionRight.Storage,
        "selection" when api >= 2 => ExtensionRight.Selection,
        "fiches:creation" when api >= 2 => ExtensionRight.CreateSheets,
        "fiches:ecriture" when api >= 2 => ExtensionRight.WriteSheets,
        _ => null,
    };
}

/// <summary>Où une extension ajoute une entrée au clic droit.</summary>
public enum ExtensionMenuZone
{
    /// <summary>Une fiche, dans la vue « Fiches » (<c>fiche</c>).</summary>
    Sheet,

    /// <summary>Un dossier, dans la vue « Fiches » (<c>dossier</c>).</summary>
    Folder,

    /// <summary>Un texte sélectionné dans une fiche, en lecture (<c>selection</c>) : il faut la permission « selection ».</summary>
    Selection,
}

/// <summary>Une commande d'une extension : dans Ctrl+K, et là où un menu la reprend.</summary>
/// <param name="Id">Son identifiant, celui que reçoit <c>kyfrev.commande</c>.</param>
/// <param name="Title">Son libellé.</param>
public sealed record ExtensionCommand(string Id, string Title);

/// <summary>Une entrée qu'une extension ajoute au clic droit.</summary>
/// <param name="Zone">Où.</param>
/// <param name="Command">La commande qu'elle lance.</param>
/// <param name="Title">Son libellé.</param>
public sealed record ExtensionMenuEntry(ExtensionMenuZone Zone, string Command, string Title);

/// <summary>
/// Le fichier <c>extension.json</c> d'une extension, aux champs fermés : un champ inconnu refuse l'extension. Sans <c>"api"</c>, c'est
/// le contrat de la 0.x (<c>extensions.rs</c>), pour que les extensions déjà écrites fonctionnent sans modification. Avec <c>"api": 2</c>,
/// une extension déclare aussi ce qu'elle ajoute à l'interface : l'application le dessine sans démarrer son code.
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

    /// <summary>Le contrat le plus récent.</summary>
    public const int LatestApi = 2;

    private const string DefaultEntry = "extension.js";
    private const int MinIdLength = 2;
    private const int MaxIdLength = 40;
    private const int MaxNameLength = 60;
    private const int MaxVersionBytes = 20;
    private const int MaxDescriptionLength = 300;
    private const int MaxBlocks = 10;
    private const int MaxBlockLength = 30;
    private const int MaxViewTitleLength = 40;
    private const int MaxCommands = 20;
    private const int MaxMenus = 10;
    private const int MaxTitleLength = 60;

    private static readonly string[] Fields = ["id", "nom", "version", "description", "auteur", "entree", "permissions", "blocs", "vue"];

    private static readonly string[] FieldsOfApi2 = [.. Fields, "api", "icone", "commandes", "menus", "encart"];

    /// <summary>Langages de bloc déjà pris par kyfRev.</summary>
    private static readonly string[] ReservedBlocks = ["mermaid", "reseau", "math", "tex", "katex", "mermaid-js", "smartart"];

    /// <summary>Le contrat du manifeste : 1 pour celui de la 0.x, 2 pour celui qui ajoute à l'interface.</summary>
    public int Api { get; init; } = 1;

    /// <summary>Son icône, un fichier SVG de son dossier ; rien pour celle des extensions.</summary>
    public string? Icon { get; init; }

    /// <summary>Sa vue montre un compteur dans le menu (<c>kyfrev.compteur</c>).</summary>
    public bool HasCounter { get; init; }

    /// <summary>Ses commandes, dans Ctrl+K.</summary>
    public ImmutableArray<ExtensionCommand> Commands { get; init; } = [];

    /// <summary>Ses entrées au clic droit.</summary>
    public ImmutableArray<ExtensionMenuEntry> Menus { get; init; } = [];

    /// <summary>Le titre de son encart sur « Aujourd'hui » (<c>kyfrev.encart</c>) ; rien si elle n'en a pas.</summary>
    public string? CardTitle { get; init; }

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
        var api = 1;
        if (root.TryGetProperty("api", out var declared))
        {
            if (declared.ValueKind != JsonValueKind.Number || !declared.TryGetInt32(out api) || api is < 1 or > LatestApi)
            {
                return Unreadable($"« api » vaut 1 ou {LatestApi}");
            }
        }
        var fields = api >= 2 ? FieldsOfApi2 : Fields;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name != "api" && !fields.Contains(property.Name, StringComparer.Ordinal))
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
        if (IsNotText(root, "description") || IsNotText(root, "auteur") || IsNotText(root, "entree") || IsNotText(root, "icone"))
        {
            return Unreadable("« description », « auteur », « entree » et « icone » sont des textes");
        }
        if (!TryPermissions(root, api, out var permissions, out var refused) || !TryBlocks(root, out var blocks, out refused)
            || !TryView(root, api, out var view, out var counter, out refused) || !TryCommands(root, out var commands, out refused)
            || !TryMenus(root, out var menus, out refused) || !TryCard(root, out var card, out refused))
        {
            return Unreadable(refused);
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
            view)
        {
            Api = api,
            Icon = Text(root, "icone")?.Replace('\\', '/'),
            HasCounter = counter,
            Commands = commands,
            Menus = menus,
            CardTitle = card,
        };
        return manifest.Refusal() is { } refusal ? Outcome.Refused<ExtensionManifest>(refusal) : Outcome.Of(manifest);
    }

    private static Outcome<ExtensionManifest> Unreadable(string reason) => Outcome.Refused<ExtensionManifest>($"{FileName} illisible : {reason}");

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool IsNotText(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.String;

    private static bool TryPermissions(JsonElement root, int api, out ImmutableArray<ExtensionRight> permissions, out string refused)
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
            if (ExtensionRights.Parse(item.ValueKind == JsonValueKind.String ? item.GetString() : null, api) is not { } right)
            {
                refused = $"permission « {item} » inconnue";
                return false;
            }
            read.Add(right);
        }
        permissions = read.ToImmutable();
        return true;
    }

    private static bool TryBlocks(JsonElement root, out ImmutableArray<string> blocks, out string refused)
    {
        blocks = [];
        refused = "« blocs » est une liste de textes";
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

    private static bool TryView(JsonElement root, int api, out string? view, out bool counter, out string refused)
    {
        view = null;
        counter = false;
        refused = "« vue » est un objet avec un « titre »";
        if (!root.TryGetProperty("vue", out var value))
        {
            return true;
        }
        view = value.ValueKind == JsonValueKind.Object ? Text(value, "titre") : null;
        if (view is not null && api >= 2 && value.TryGetProperty("compteur", out var shown))
        {
            if (shown.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                refused = "« compteur » vaut true ou false";
                return false;
            }
            counter = shown.GetBoolean();
        }
        return view is not null;
    }

    private static bool TryCommands(JsonElement root, out ImmutableArray<ExtensionCommand> commands, out string refused)
    {
        commands = [];
        refused = "« commandes » est une liste d'objets avec un « id » et un « titre »";
        if (!root.TryGetProperty("commandes", out var list))
        {
            return true;
        }
        if (list.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var read = ImmutableArray.CreateBuilder<ExtensionCommand>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Text(item, "id") is not { } id || Text(item, "titre") is not { } title)
            {
                return false;
            }
            read.Add(new ExtensionCommand(id, title));
        }
        commands = read.ToImmutable();
        return true;
    }

    private static bool TryMenus(JsonElement root, out ImmutableArray<ExtensionMenuEntry> menus, out string refused)
    {
        menus = [];
        refused = "« menus » est un objet : « fiche », « dossier » et « selection », chacun une liste d'objets avec une « commande » et un « titre »";
        if (!root.TryGetProperty("menus", out var zones))
        {
            return true;
        }
        if (zones.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        var read = ImmutableArray.CreateBuilder<ExtensionMenuEntry>();
        foreach (var zone in zones.EnumerateObject())
        {
            ExtensionMenuZone? where = zone.Name switch
            {
                "fiche" => ExtensionMenuZone.Sheet,
                "dossier" => ExtensionMenuZone.Folder,
                "selection" => ExtensionMenuZone.Selection,
                _ => null,
            };
            if (where is null || zone.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            foreach (var item in zone.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || Text(item, "commande") is not { } command || Text(item, "titre") is not { } title)
                {
                    return false;
                }
                read.Add(new ExtensionMenuEntry(where.Value, command, title));
            }
        }
        menus = read.ToImmutable();
        return true;
    }

    private static bool TryCard(JsonElement root, out string? card, out string refused)
    {
        card = null;
        refused = "« encart » est un objet avec un « titre »";
        if (!root.TryGetProperty("encart", out var value))
        {
            return true;
        }
        card = value.ValueKind == JsonValueKind.Object ? Text(value, "titre") : null;
        return card is not null;
    }

    private static bool IsLowerOrDigitOrDash(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';

    private static bool IsValidBlock(string language) =>
        language.Length is >= 1 and <= MaxBlockLength && language.All(IsLowerOrDigitOrDash) && !ReservedBlocks.Contains(language, StringComparer.Ordinal);

    private static bool IsValidTitle(string title, int max) => !string.IsNullOrWhiteSpace(title) && Letters(title) <= max;

    private static int Letters(string text) => text.EnumerateRunes().Count();

    /// <summary>Ce qui empêche de garder ce manifeste ; rien s'il est bon.</summary>
    private string? Refusal()
    {
        if (!IsValidId(Id))
        {
            return $"identifiant « {Id} » invalide : 2 à 40 minuscules, chiffres ou tirets";
        }
        if (!IsValidTitle(Name, MaxNameLength))
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
        if (Icon is not null && (ExtensionPath.Refusal(Icon) is not null || !Icon.EndsWith(".svg", StringComparison.Ordinal) || Icon.Split('/').Any(part => part.StartsWith('.'))))
        {
            return "l'icône doit être un fichier .svg de son dossier, qui n'est pas caché";
        }
        if (Blocks.Length > MaxBlocks)
        {
            return "10 types de bloc au plus";
        }
        if (Blocks.FirstOrDefault(language => !IsValidBlock(language)) is { } block)
        {
            return $"type de bloc « {block} » invalide ou réservé";
        }
        if (ViewTitle is not null && !IsValidTitle(ViewTitle, MaxViewTitleLength))
        {
            return "titre de vue vide ou de plus de 40 caractères";
        }
        return ApiRefusal() ?? (Blocks.IsEmpty && ViewTitle is null && Commands.IsEmpty && CardTitle is null ? "l'extension n'ajoute ni bloc, ni vue, ni commande, ni encart" : null);
    }

    /// <summary>Ce que le contrat 2 ajoute, vérifié : des commandes nommées, des menus qui en lancent une, des titres courts.</summary>
    private string? ApiRefusal()
    {
        if (Commands.Length > MaxCommands || Menus.Length > MaxMenus)
        {
            return "20 commandes et 10 entrées de menu au plus";
        }
        if (Commands.FirstOrDefault(command => command.Id.Length > MaxIdLength || command.Id.Length == 0 || !command.Id.All(IsLowerOrDigitOrDash) || !IsValidTitle(command.Title, MaxTitleLength)) is { } bad)
        {
            return $"commande « {bad.Id} » invalide : un identifiant en minuscules, chiffres ou tirets, et un titre de 60 caractères au plus";
        }
        if (Commands.GroupBy(command => command.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is { } twice)
        {
            return $"commande « {twice.Key} » en double";
        }
        if (Menus.FirstOrDefault(menu => !Commands.Any(command => command.Id == menu.Command) || !IsValidTitle(menu.Title, MaxTitleLength)) is { } lost)
        {
            return $"entrée de menu « {lost.Title} » : elle lance une commande déclarée, et son titre a 60 caractères au plus";
        }
        if (Menus.Any(menu => menu.Zone == ExtensionMenuZone.Selection) && !Permissions.Contains(ExtensionRight.Selection))
        {
            return "une entrée de menu « selection » demande la permission « selection »";
        }
        return CardTitle is not null && !IsValidTitle(CardTitle, MaxTitleLength) ? "titre d'encart vide ou de plus de 60 caractères" : null;
    }
}
