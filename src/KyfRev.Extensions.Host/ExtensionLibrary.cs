using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using KyfRev.Application;

namespace KyfRev.Extensions.Host;

/// <summary>Une extension installée sur ce poste.</summary>
/// <param name="Manifest">Son manifeste.</param>
/// <param name="IsActive">Elle est active.</param>
public sealed record InstalledExtension(ExtensionManifest Manifest, bool IsActive);

/// <summary>Ce qu'on sait d'une extension avant de l'installer, pour la confirmation.</summary>
/// <param name="Manifest">Son manifeste.</param>
/// <param name="FileCount">Son nombre de fichiers.</param>
/// <param name="Size">Sa taille totale, en octets.</param>
/// <param name="InstalledVersion">La version déjà installée, qu'elle remplacerait.</param>
public sealed record ExtensionPreview(ExtensionManifest Manifest, int FileCount, long Size, string? InstalledVersion);

/// <summary>
/// Les extensions installées, dans un dossier du poste et jamais dans le coffre : ouvrir un coffre reçu d'ailleurs n'exécute aucun
/// code. Elles ne s'installent que depuis un dossier choisi par l'utilisateur. Rien n'est effacé : une extension retirée, ou
/// remplacée par une nouvelle version, est rangée dans <c>.anciennes</c>. Reprend <c>extensions.rs</c> de la 0.x, et son fichier d'état.
/// </summary>
public sealed class ExtensionLibrary
{
    private const string StateFile = "etat.json";
    private const string OldFolder = ".anciennes";
    private const string InstallingPrefix = ".installation-";
    private const int MaxFiles = 200;
    private const long MaxSize = 10 * 1024 * 1024;
    private const int MaxDepth = 5;

    private static readonly JsonSerializerOptions StateOptions = new() { WriteIndented = true, IndentSize = 2 };

    private readonly string _folder;
    private readonly TimeProvider _clock;

    /// <summary>La bibliothèque rangée dans ce dossier.</summary>
    /// <param name="folder">Le dossier des extensions, sur le poste.</param>
    /// <param name="clock">L'horloge qui date ce qui est rangé.</param>
    public ExtensionLibrary(string folder, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentNullException.ThrowIfNull(clock);
        _folder = folder;
        _clock = clock;
    }

    /// <summary>Les extensions installées, par nom. Un dossier dont le manifeste ne va pas est ignoré.</summary>
    [ZeroXCommand("extensions")]
    public IReadOnlyList<InstalledExtension> Installed()
    {
        if (!Directory.Exists(_folder))
        {
            return [];
        }
        var active = ReadActive(_folder);
        return
        [
            .. new DirectoryInfo(_folder).EnumerateDirectories()
                .Where(folder => !folder.Name.StartsWith('.'))
                .Select(folder => (folder.Name, Manifest: ReadManifest(folder.FullName)))
                .Where(found => found.Manifest.Succeeded && found.Manifest.Value.Id == found.Name)
                .Select(found => new InstalledExtension(found.Manifest.Value, active.Contains(found.Name)))
                .OrderBy(extension => extension.Manifest.Name, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    /// <summary>Le manifeste d'une extension active ; rien pour une autre.</summary>
    /// <param name="id">Son identifiant.</param>
    public ExtensionManifest? Active(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return ExtensionManifest.IsValidId(id) && ReadActive(_folder).Contains(id) && ReadManifest(Path.Combine(_folder, id)) is { Succeeded: true } manifest && manifest.Value.Id == id
            ? manifest.Value
            : null;
    }

    /// <summary>Un fichier d'une extension active, d'un type accepté ; rien pour tout autre chemin.</summary>
    /// <param name="id">Son identifiant.</param>
    /// <param name="path">Le chemin du fichier dans son dossier, avec des « / ».</param>
    public byte[]? Content(string id, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (Active(id) is null || ExtensionPath.Refusal(path) is not null || ExtensionPath.ContentTypeOf(path) is null || path.Split('/').Any(part => part.StartsWith('.')))
        {
            return null;
        }
        try
        {
            return File.ReadAllBytes(Path.Combine([_folder, id, .. path.Split('/')]));
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            return null;
        }
    }

    /// <summary>Lit l'extension d'un dossier, sans rien installer.</summary>
    /// <param name="source">Le dossier choisi.</param>
    [ZeroXCommand("lire_extension")]
    public Outcome<ExtensionPreview> Read(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        var manifest = ReadManifest(source);
        if (!manifest.Succeeded)
        {
            return Outcome.Refused<ExtensionPreview>(manifest.Error!);
        }
        try
        {
            var inventory = Inventory(source);
            if (!inventory.Succeeded)
            {
                return Outcome.Refused<ExtensionPreview>(inventory.Error!);
            }
            var installed = Installed().FirstOrDefault(extension => extension.Manifest.Id == manifest.Value.Id);
            return Outcome.Of(new ExtensionPreview(manifest.Value, inventory.Value.Files.Count, inventory.Value.Size, installed?.Manifest.Version));
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            return Outcome.Refused<ExtensionPreview>(exception.Message);
        }
    }

    /// <summary>Copie l'extension d'un dossier et l'active. Une version déjà installée est rangée.</summary>
    /// <param name="source">Le dossier choisi.</param>
    [ZeroXCommand("installer_extension")]
    public Outcome<InstalledExtension> Install(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        var manifest = ReadManifest(source);
        if (!manifest.Succeeded)
        {
            return Outcome.Refused<InstalledExtension>(manifest.Error!);
        }
        if (IsWithin(Path.GetFullPath(source), Path.GetFullPath(_folder)))
        {
            return Outcome.Refused<InstalledExtension>("ce dossier est déjà celui des extensions installées");
        }
        try
        {
            var inventory = Inventory(source);
            if (!inventory.Succeeded)
            {
                return Outcome.Refused<InstalledExtension>(inventory.Error!);
            }
            Place(source, manifest.Value.Id, inventory.Value.Files);
            WriteActive(_folder, [.. ReadActive(_folder).Where(id => id != manifest.Value.Id), manifest.Value.Id]);
            return Outcome.Of(new InstalledExtension(manifest.Value, true));
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            return Outcome.Refused<InstalledExtension>(exception.Message);
        }
    }

    /// <summary>Active ou désactive une extension installée.</summary>
    /// <param name="id">Son identifiant.</param>
    /// <param name="active">Vrai pour l'activer.</param>
    [ZeroXCommand("activer_extension")]
    public Outcome<Done> SetActive(string id, bool active)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!Installed().Any(extension => extension.Manifest.Id == id))
        {
            return Outcome.Refused<Done>($"extension « {id} » introuvable");
        }
        try
        {
            var others = ReadActive(_folder).Where(other => other != id);
            WriteActive(_folder, active ? [.. others, id] : [.. others]);
            return Outcome.Done();
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            return Outcome.Refused<Done>(exception.Message);
        }
    }

    /// <summary>Retire une extension : son dossier est rangé dans <c>.anciennes</c>, ses données restent dans le coffre.</summary>
    /// <param name="id">Son identifiant.</param>
    [ZeroXCommand("retirer_extension")]
    public Outcome<Done> Retire(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!ExtensionManifest.IsValidId(id))
        {
            return Outcome.Refused<Done>("identifiant d'extension invalide");
        }
        try
        {
            WriteActive(_folder, [.. ReadActive(_folder).Where(other => other != id)]);
            Store(id);
            return Outcome.Done();
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            return Outcome.Refused<Done>(exception.Message);
        }
    }

    /// <summary>
    /// Au premier lancement, reprend les extensions de la 0.x et leur état, sans rien modifier chez elle. Ne fait rien si ce poste
    /// a déjà un dossier d'extensions ; une extension qui ne se copie pas reste à réinstaller.
    /// </summary>
    /// <param name="legacy">Le dossier des extensions de la 0.x.</param>
    public void Import(string legacy)
    {
        ArgumentException.ThrowIfNullOrEmpty(legacy);
        if (Directory.Exists(_folder) || !Directory.Exists(legacy))
        {
            return;
        }
        List<string> imported = [];
        foreach (var folder in new DirectoryInfo(legacy).EnumerateDirectories().Where(folder => !folder.Name.StartsWith('.')))
        {
            try
            {
                if (ReadManifest(folder.FullName) is { Succeeded: true } manifest && manifest.Value.Id == folder.Name && Inventory(folder.FullName) is { Succeeded: true } inventory)
                {
                    Place(folder.FullName, folder.Name, inventory.Value.Files);
                    imported.Add(folder.Name);
                }
            }
            catch (Exception exception) when (IsFileProblem(exception))
            {
                continue;
            }
        }
        try
        {
            Directory.CreateDirectory(_folder);
            WriteActive(_folder, [.. ReadActive(legacy).Where(imported.Contains)]);
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            return;
        }
    }

    private static bool IsFileProblem(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static bool IsWithin(string path, string folder) =>
        path.Equals(folder, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static Outcome<ExtensionManifest> ReadManifest(string folder)
    {
        byte[] content;
        try
        {
            content = File.ReadAllBytes(Path.Combine(folder, ExtensionManifest.FileName));
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            return Outcome.Refused<ExtensionManifest>($"{ExtensionManifest.FileName} introuvable dans ce dossier");
        }
        var manifest = ExtensionManifest.Parse(content);
        if (manifest.Succeeded && !File.Exists(Path.Combine([folder, .. manifest.Value.Entry.Split('/')])))
        {
            return Outcome.Refused<ExtensionManifest>($"script d'entrée « {manifest.Value.Entry} » absent");
        }
        return manifest;
    }

    /// <summary>Les fichiers à copier, vérifiés, avec des « / ». Les fichiers et dossiers cachés sont ignorés.</summary>
    private static Outcome<(List<string> Files, long Size)> Inventory(string source)
    {
        List<string> files = [];
        long size = 0;
        return Walk(new DirectoryInfo(source), string.Empty, 0, files, ref size) is { } refusal
            ? Outcome.Refused<(List<string>, long)>(refusal)
            : Outcome.Of((files, size));
    }

    private static string? Walk(DirectoryInfo folder, string relative, int depth, List<string> files, ref long size)
    {
        if (depth > MaxDepth)
        {
            return $"« {relative} » : plus de {MaxDepth} niveaux de dossiers";
        }
        foreach (var entry in folder.EnumerateFileSystemInfos().OrderBy(entry => entry.Name, StringComparer.Ordinal))
        {
            if (entry.Name.StartsWith('.'))
            {
                continue;
            }
            var path = relative.Length == 0 ? entry.Name : $"{relative}/{entry.Name}";
            if (ExtensionPath.Refusal(path) is { } refused)
            {
                return $"chemin « {path} » refusé : {refused}";
            }
            if (entry.LinkTarget is not null || entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return $"« {path} » : lien symbolique refusé";
            }
            if (entry is DirectoryInfo directory)
            {
                if (Walk(directory, path, depth + 1, files, ref size) is { } deeper)
                {
                    return deeper;
                }
            }
            else
            {
                if (ExtensionPath.ContentTypeOf(entry.Name) is null)
                {
                    return $"« {path} » : type de fichier refusé (scripts .js, styles, images et textes seulement)";
                }
                size += ((FileInfo)entry).Length;
                files.Add(path);
            }
            if (files.Count > MaxFiles || size > MaxSize)
            {
                return "extension trop grosse (200 fichiers et 10 Mo au plus)";
            }
        }
        return null;
    }

    private static HashSet<string> ReadActive(string folder)
    {
        try
        {
            return [.. JsonSerializer.Deserialize<ExtensionState>(File.ReadAllBytes(Path.Combine(folder, StateFile)))?.Active ?? []];
        }
        catch (Exception exception) when (IsFileProblem(exception) || exception is JsonException)
        {
            return [];
        }
    }

    private static void WriteActive(string folder, List<string> active)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, StateFile), JsonSerializer.SerializeToUtf8Bytes(new ExtensionState(active), StateOptions));
    }

    /// <summary>Copie dans un dossier provisoire, puis met en place : une installation interrompue ne laisse rien de bancal.</summary>
    private void Place(string source, string id, List<string> files)
    {
        var installing = Path.Combine(_folder, InstallingPrefix + id);
        Store(InstallingPrefix + id);
        Directory.CreateDirectory(installing);
        foreach (var file in files)
        {
            var target = Path.Combine([installing, .. file.Split('/')]);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine([source, .. file.Split('/')]), target);
        }
        Store(id);
        Directory.Move(installing, Path.Combine(_folder, id));
    }

    /// <summary>Range un dossier de la bibliothèque dans <c>.anciennes</c>, sous un nom daté ; rien n'est effacé.</summary>
    private void Store(string name)
    {
        var current = Path.Combine(_folder, name);
        if (!Directory.Exists(current))
        {
            return;
        }
        var old = Path.Combine(_folder, OldFolder);
        Directory.CreateDirectory(old);
        var moment = _clock.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var target = Path.Combine(old, $"{name}-{moment}");
        for (var number = 2; Directory.Exists(target); number++)
        {
            target = Path.Combine(old, $"{name}-{moment}-{number}");
        }
        Directory.Move(current, target);
    }

    private sealed record ExtensionState([property: JsonPropertyName("actives")] List<string> Active);
}
