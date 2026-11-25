using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Vault.Files;

/// <summary>
/// La présence des postes qui ont le coffre ouvert, dans un dossier synchronisé : chacun écrit son propre fichier,
/// <c>.suivi/postes/&lt;poste&gt;.json</c>, à l'ouverture puis chaque minute, et le dit fermé en partant. Ainsi un poste
/// n'écrit jamais le fichier d'un autre. Une présence qui n'a pas été renouvelée depuis <see cref="Freshness"/> est
/// ignorée : poste éteint, en veille, ou synchronisation en retard. Comme le verrou, ces fichiers restent en clair, sur le
/// disque, même dans un coffre chiffré : ils ne disent que le nom du poste et des heures.
/// </summary>
public static class ComputerPresence
{
    /// <summary>Le temps au-delà duquel une présence qui n'a pas été renouvelée est ignorée.</summary>
    public static readonly TimeSpan Freshness = TimeSpan.FromMinutes(3);

    private const int Format = 1;

    /// <summary>Le nom de ce poste.</summary>
    public static string ThisComputer => Environment.MachineName;

    /// <summary>
    /// Écrit la présence de <paramref name="computer"/> : ouvert depuis <paramref name="since"/>, vu à
    /// <paramref name="now"/>, ou fermé. Un échec d'écriture n'a pas de conséquence : la présence n'est qu'un avis.
    /// </summary>
    /// <returns>Faux si elle n'a pas pu être écrite.</returns>
    public static bool Announce(string root, VaultStorage disk, string computer, DateTimeOffset since, DateTimeOffset now, bool open)
    {
        ArgumentNullException.ThrowIfNull(disk);
        ArgumentException.ThrowIfNullOrEmpty(computer);
        var document = new JsonObject
        {
            ["format"] = Format,
            ["poste"] = computer,
            ["depuis"] = since.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["vu"] = now.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["ouvert"] = open,
        };
        try
        {
            var folder = FolderOf(root);
            disk.CreateDirectory(folder);
            disk.Replace(Path.Combine(folder, FileNameOf(computer)), Encoding.UTF8.GetBytes(document.ToJsonString()));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Les autres postes qui ont le coffre ouvert : leur présence dit « ouvert » et a été renouvelée depuis moins de
    /// <see cref="Freshness"/>. Un fichier illisible, ou en cours de synchronisation, est passé.
    /// </summary>
    public static IReadOnlyList<OtherComputer> Others(string root, VaultStorage disk, string computer, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(disk);
        var folder = FolderOf(root);
        IReadOnlyList<StorageEntry> entries;
        try
        {
            if (!disk.DirectoryExists(folder))
            {
                return [];
            }
            entries = disk.Entries(folder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
        var others = new List<OtherComputer>();
        foreach (var entry in entries.Where(entry => !entry.IsDirectory && entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            if (Read(disk, entry.FullName) is { } other
                && !string.Equals(other.Name, computer, StringComparison.OrdinalIgnoreCase)
                && now - other.SeenAt < Freshness
                && other.SeenAt - now < Freshness)
            {
                others.Add(other);
            }
        }
        return [.. others.OrderBy(other => other.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>La présence d'un poste, si elle se lit et dit le coffre ouvert ; rien sinon.</summary>
    private static OtherComputer? Read(VaultStorage disk, string path)
    {
        try
        {
            if (JsonNode.Parse(disk.ReadAllBytes(path)) is not JsonObject document
                || document["format"]?.GetValue<int>() != Format
                || document["ouvert"]?.GetValue<bool>() != true
                || document["poste"]?.GetValue<string>() is not { Length: > 0 } name
                || !DateTimeOffset.TryParse(document["depuis"]?.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var since)
                || !DateTimeOffset.TryParse(document["vu"]?.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var seen))
            {
                return null;
            }
            return new OtherComputer(name, since, seen);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static string FolderOf(string root) => VaultLayout.Absolute(root, VaultLayout.Presence);

    /// <summary>Le nom du fichier d'un poste : ses lettres, chiffres, tirets et soulignés, le reste remplacé.</summary>
    internal static string FileNameOf(string computer) =>
        new string([.. computer.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_')]) + ".json";
}
