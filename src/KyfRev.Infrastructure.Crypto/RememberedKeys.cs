using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Crypto;

/// <summary>
/// Les clés retenues sur ce poste, hors de tout coffre, dans <c>cles-retenues.json</c>. Chacune est déjà protégée par
/// Windows pour la session de l'utilisateur (<see cref="RememberedKey"/>) : le fichier ne se lit nulle part ailleurs. Une
/// clé est retenue pour un coffre (son identifiant) dans un dossier : deux copies du même coffre ont chacune la leur.
/// </summary>
/// <param name="folder">Le dossier des données de ce poste.</param>
/// <param name="disk">Le disque, qui écrit le fichier de façon atomique.</param>
public sealed class RememberedKeys(string folder, VaultStorage disk)
{
    /// <summary>Le nom du fichier.</summary>
    public const string FileName = "cles-retenues.json";

    private const int Format = 1;

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Le chemin du fichier.</summary>
    public string Path => System.IO.Path.Combine(folder, FileName);

    /// <summary>La clé protégée retenue pour le coffre <paramref name="vaultId"/> de <paramref name="root"/> ; rien sinon.</summary>
    public byte[]? Find(string vaultId, string root)
    {
        ArgumentNullException.ThrowIfNull(vaultId);
        var place = Place(root);
        return Read()?.FirstOrDefault(entry => entry.VaultId == vaultId && SamePlace(entry.Root, place))?.Key;
    }

    /// <summary>
    /// Retient la clé protégée <paramref name="protectedKey"/> pour ce coffre et ce dossier ; faux si elle n'a pas pu
    /// l'être, et rien n'est écrit sur un fichier qui ne se lit pas : les autres clés qu'il retient restent.
    /// </summary>
    public bool Keep(string vaultId, string root, byte[] protectedKey)
    {
        ArgumentNullException.ThrowIfNull(vaultId);
        ArgumentNullException.ThrowIfNull(protectedKey);
        var place = Place(root);
        if (Read() is not { } entries)
        {
            return false;
        }
        var kept = entries.Where(entry => !SamePlace(entry.Root, place)).ToList();
        kept.Add(new Entry(vaultId, place, protectedKey));
        return Write(kept);
    }

    /// <summary>
    /// Oublie la clé retenue pour le dossier <paramref name="root"/>, quel que soit son coffre : le fichier est réécrit
    /// sans elle. Vrai s'il n'y avait rien à oublier ; faux si le fichier ne se lit pas, ou n'a pas pu être réécrit.
    /// </summary>
    public bool Forget(string root)
    {
        var place = Place(root);
        if (Read() is not { } entries)
        {
            return false;
        }
        var kept = entries.Where(entry => !SamePlace(entry.Root, place)).ToList();
        return kept.Count == entries.Count || Write(kept);
    }

    /// <summary>Un dossier écrit en entier, sans séparateur final : la même place s'écrit toujours de la même façon.</summary>
    private static string Place(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        return System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(root));
    }

    /// <summary>Comme Windows : deux chemins qui ne diffèrent que par la casse sont le même dossier.</summary>
    private static bool SamePlace(string first, string second) => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Les clés retenues : aucune si le fichier manque, ou s'il est abîmé (une clé mal lue se redemande, et le fichier se
    /// réécrit) ; rien s'il ne se lit pas pour l'instant, ou s'il vient d'une version plus récente : il ne faut pas
    /// l'écraser.
    /// </summary>
    private List<Entry>? Read()
    {
        byte[] bytes;
        try
        {
            bytes = disk.ReadAllBytes(Path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        try
        {
            if (JsonNode.Parse(bytes) is not JsonObject document)
            {
                return [];
            }
            if (document["format"] is { } format && format.GetValue<int>() != Format)
            {
                return null;
            }
            if (document["cles"] is not JsonArray keys)
            {
                return [];
            }
            var entries = new List<Entry>();
            foreach (var node in keys)
            {
                if (node is JsonObject entry
                    && entry["coffre"]?.GetValue<string>() is { Length: > 0 } vaultId
                    && entry["dossier"]?.GetValue<string>() is { Length: > 0 } root
                    && entry["cle"]?.GetValue<string>() is { Length: > 0 } key)
                {
                    entries.Add(new Entry(vaultId, root, Convert.FromBase64String(key)));
                }
            }
            return entries;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return [];
        }
    }

    private bool Write(List<Entry> entries)
    {
        var keys = new JsonArray();
        foreach (var entry in entries)
        {
            keys.Add(new JsonObject
            {
                ["coffre"] = entry.VaultId,
                ["dossier"] = entry.Root,
                ["cle"] = Convert.ToBase64String(entry.Key),
            });
        }
        var document = new JsonObject { ["format"] = Format, ["cles"] = keys };
        try
        {
            disk.CreateDirectory(folder);
            disk.Replace(Path, JsonSerializer.SerializeToUtf8Bytes(document, Indented));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record Entry(string VaultId, string Root, byte[] Key);
}
