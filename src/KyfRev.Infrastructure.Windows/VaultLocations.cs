using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// Où vivent l'application et les coffres, comme dans la 0.x (<c>emplacements.rs</c>), selon ce
/// qui se trouve à côté de l'exécutable :
/// <list type="bullet">
/// <item><description><b>portable</b> (<c>portable.json</c>) : tout reste dans le dossier transportable ;
/// le coffre est celui qu'il indique, par un chemin relatif ;</description></item>
/// <item><description><b>installé</b> (<c>installation.json</c>, écrit par l'installateur) : chaque
/// utilisateur a ses données dans son profil ; le coffre prévu est dans ses Documents ou ses
/// données d'application, au choix fait à l'installation ;</description></item>
/// <item><description><b>développement</b> (ni l'un ni l'autre) : le dernier coffre est retenu à côté
/// de l'exécutable.</description></item>
/// </list>
/// En mode installé, le dernier coffre est retenu dans <c>%LOCALAPPDATA%\kyfRev\v1</c>. Celui de la
/// 0.x (<c>%LOCALAPPDATA%\kyfRev</c>) est lu, jamais modifié : le premier lancement reprend le
/// coffre qu'elle avait ouvert. Les fichiers sont lus comme par la 0.x : un fichier absent,
/// illisible ou mal formé est ignoré.
/// </summary>
public sealed class VaultLocations : IVaultLocations
{
    private const string ProfileFolder = "kyfRev";
    private const string OwnFolder = "v1";
    private const string LocalConfig = "config-locale.json";
    private const string LastVault = "dernierCoffre";

    /// <summary>Dossier de suivi d'un coffre au format 1 : présent, le coffre existe.</summary>
    private const string TrackingFolder = ".suivi";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        // Écrit comme la 0.x : seuls les guillemets, barres obliques inverses et caractères de contrôle sont échappés.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _localData;
    private readonly string? _legacyLocalData;
    private readonly string? _planned;

    private VaultLocations(AppMode mode, string localData, string? legacyLocalData, string? planned)
    {
        Mode = mode;
        _localData = localData;
        _legacyLocalData = legacyLocalData;
        _planned = planned;
    }

    /// <inheritdoc/>
    public AppMode Mode { get; }

    /// <summary>
    /// Dossier des données propres à ce poste (journal de diagnostic, dernier coffre) : à côté de
    /// l'exécutable en mode portable ou en développement, <c>%LOCALAPPDATA%\kyfRev\v1</c> installé.
    /// </summary>
    public string LocalData => _localData;

    /// <summary>Les emplacements de l'exécutable en cours, pour l'utilisateur en cours.</summary>
    public static VaultLocations Detect()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return From(AppContext.BaseDirectory, new ProfileFolders(
            local.Length > 0 ? local : Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)));
    }

    /// <summary>Mode et emplacements pour un exécutable rangé dans <paramref name="applicationFolder"/>.</summary>
    internal static VaultLocations From(string applicationFolder, ProfileFolders profile)
    {
        var folder = Path.TrimEndingDirectorySeparator(applicationFolder);
        if (ReadText(Path.Combine(folder, "portable.json"), "coffre") is { } relative && RelativeOn(folder, relative) is { } portableVault)
        {
            return new VaultLocations(AppMode.Portable, folder, null, portableVault);
        }
        // Rangement choisi à l'installation : « documents » ou « donnees » ; toute autre valeur rend le fichier invalide.
        var parent = ReadText(Path.Combine(folder, "installation.json"), "coffre") switch
        {
            "documents" => profile.Documents,
            "donnees" => profile.RoamingData,
            _ => null,
        };
        if (parent is not null)
        {
            var local = Path.Combine(profile.LocalData, ProfileFolder);
            var planned = parent.Length > 0 ? Path.Combine(parent, ProfileFolder, "Coffre") : null;
            return new VaultLocations(AppMode.Installed, Path.Combine(local, OwnFolder), local, planned);
        }
        return new VaultLocations(AppMode.Development, folder, null, null);
    }

    /// <inheritdoc/>
    public string? VaultToOpen() =>
        Mode == AppMode.Portable
            ? _planned
            : ReadLastVault(_localData) ?? (_legacyLocalData is { } legacy ? ReadLastVault(legacy) : null) ?? _planned;

    /// <inheritdoc/>
    public string? VaultToCreate() =>
        _planned is { } planned && !Path.Exists(Path.Combine(planned, TrackingFolder)) ? planned : null;

    /// <summary>
    /// Retient le coffre ouvert (rien en mode portable). Comme dans la 0.x, un échec d'écriture ne
    /// bloque rien : le coffre sera seulement à rouvrir à la main au prochain démarrage.
    /// </summary>
    public bool Remember(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (Mode == AppMode.Portable)
        {
            return true;
        }
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString(LastVault, root);
            writer.WriteEndObject();
        }
        try
        {
            Directory.CreateDirectory(_localData);
            File.WriteAllBytes(Path.Combine(_localData, LocalConfig), buffer.WrittenSpan.ToArray());
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// <paramref name="relative"/> résolu depuis <paramref name="folder"/>, à condition d'être
    /// réellement relatif (« ../Coffre ») : un chemin absolu casserait la portabilité.
    /// </summary>
    internal static string? RelativeOn(string folder, string relative)
    {
        if (Path.IsPathRooted(relative))
        {
            return null;
        }
        var result = folder;
        foreach (var part in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]))
        {
            if (part == "..")
            {
                result = Path.GetDirectoryName(result) ?? result;
            }
            else if (part is not ("" or "."))
            {
                result = Path.Combine(result, part);
            }
        }
        return result;
    }

    private static string? ReadLastVault(string folder) => ReadText(Path.Combine(folder, LocalConfig), LastVault);

    /// <summary>
    /// Le texte de la propriété <paramref name="name"/> d'un fichier JSON ; rien si le fichier
    /// manque, est illisible, mal formé (une marque d'ordre des octets comprise, comme dans la 0.x)
    /// ou si la propriété n'est pas un texte.
    /// </summary>
    private static string? ReadText(string file, string name)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Dossiers du profil de l'utilisateur.</summary>
/// <param name="LocalData"><c>%LOCALAPPDATA%</c>.</param>
/// <param name="Documents">Documents ; vide s'il n'existe pas.</param>
/// <param name="RoamingData"><c>%APPDATA%</c> ; vide s'il n'existe pas.</param>
internal sealed record ProfileFolders(string LocalData, string Documents, string RoamingData);
