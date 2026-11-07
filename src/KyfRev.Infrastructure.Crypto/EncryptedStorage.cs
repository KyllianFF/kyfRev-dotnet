using System.IO.Enumeration;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Crypto;

/// <summary>
/// Les fichiers d'un coffre chiffré, vus par la session du coffre comme ceux d'un disque : chaque chemin du coffre est une
/// entrée de l'index, chaque fichier un objet chiffré. Rien n'est écrit en clair, pas même un fichier temporaire : les
/// temporaires de l'écriture atomique contiennent l'objet déjà chiffré.
/// </summary>
/// <remarks>
/// Les chemins sont absolus, dans le dossier du coffre (<see cref="Root"/>), qu'on l'atteigne par son nom ou par son
/// chemin réel. Les dossiers sont ceux de l'index, vides compris ; il n'y a ni lien ni jonction. Les erreurs sont celles
/// que le disque donnerait : <see cref="FileNotFoundException"/>, <see cref="DirectoryNotFoundException"/>,
/// <see cref="IOException"/> pour un nom déjà pris ou un objet altéré, <see cref="UnauthorizedAccessException"/> pour un
/// chemin hors du coffre ou un dossier lu comme un fichier.
/// </remarks>
public sealed class EncryptedStorage : VaultStorage, IDisposable
{
    private readonly EncryptedVault _vault;
    private readonly string _root;
    private readonly string _realRoot;

    private EncryptedStorage(EncryptedVault vault)
    {
        _vault = vault;
        _root = Path.TrimEndingDirectorySeparator(vault.Root);
        _realRoot = Path.TrimEndingDirectorySeparator(vault.Disk.RealPath(vault.Root));
    }

    /// <inheritdoc/>
    public override string Root => _root;

    /// <summary>Le disque où sont posés l'en-tête, l'index et les objets : une sauvegarde les recopie, chiffrés.</summary>
    public override VaultStorage Raw => _vault.Disk;

    /// <summary>L'identifiant du coffre, lu dans son en-tête.</summary>
    public string VaultId => _vault.VaultId;

    /// <summary>Crée un coffre chiffré vide dans le dossier vide <paramref name="root"/> (<see cref="EncryptedVault.Create"/>).</summary>
    public static EncryptedStorage Create(string root, VaultHeader header, ReadOnlySpan<byte> dataKey, VaultStorage disk) =>
        new(EncryptedVault.Create(root, header, dataKey, disk));

    /// <summary>Ouvre le coffre chiffré de <paramref name="root"/> avec sa clé de données (<see cref="EncryptedVault.Open"/>).</summary>
    public static EncryptedStorage Open(string root, ReadOnlySpan<byte> dataKey, VaultStorage disk) =>
        new(EncryptedVault.Open(root, dataKey, disk));

    /// <inheritdoc/>
    public override bool FileExists(string path) => TryInside(path) is { Length: > 0 } relative && _vault.ContainsFile(relative);

    /// <inheritdoc/>
    public override bool DirectoryExists(string path) => TryInside(path) is { } relative && IsFolder(relative);

    /// <inheritdoc/>
    public override byte[] ReadAllBytes(string path)
    {
        var relative = ExistingFile(path);
        try
        {
            return _vault.Read(relative);
        }
        catch (VaultCryptoException exception)
        {
            throw new IOException(exception.Message, exception);
        }
    }

    /// <summary>La taille d'un fichier ; comme <see cref="FileInfo.Length"/>, tout ce qui n'est pas un fichier est introuvable.</summary>
    public override long LengthOf(string path) =>
        TryInside(path) is { Length: > 0 } relative && _vault.ContainsFile(relative)
            ? _vault.LengthOf(relative)
            : throw new FileNotFoundException($"Fichier introuvable : « {path} ».", path);

    /// <inheritdoc/>
    public override DateTime LastWriteTimeUtc(string path) =>
        TryInside(path) is { Length: > 0 } relative && _vault.ContainsFile(relative) ? _vault.LastWriteTimeUtc(relative) : EncryptedVault.Missing;

    /// <inheritdoc/>
    public override bool TryCreateNew(string path, ReadOnlySpan<byte> content)
    {
        var relative = FileToWrite(path);
        if (_vault.ContainsFile(relative) || IsFolder(relative))
        {
            return false;
        }
        _vault.Save(relative, content);
        return true;
    }

    /// <inheritdoc/>
    public override void Replace(string path, ReadOnlySpan<byte> content)
    {
        var relative = FileToWrite(path);
        if (IsFolder(relative))
        {
            throw new UnauthorizedAccessException($"« {path} » est un dossier.");
        }
        _vault.Save(relative, content);
    }

    /// <inheritdoc/>
    public override void CreateDirectory(string path)
    {
        var relative = Inside(path);
        if (relative.Length == 0 || IsFolder(relative))
        {
            return;
        }
        for (var end = relative.IndexOf('/', StringComparison.Ordinal); ; end = relative.IndexOf('/', end + 1))
        {
            if (_vault.ContainsFile(end < 0 ? relative : relative[..end]))
            {
                throw new IOException($"Un fichier porte déjà le nom de ce dossier : « {path} ».");
            }
            if (end < 0)
            {
                break;
            }
        }
        _vault.CreateFolder(relative);
    }

    /// <inheritdoc/>
    public override void MoveFile(string source, string destination)
    {
        var from = ExistingFile(source);
        var to = Inside(destination);
        CheckParent(to, destination);
        if (!SameName(from, to) && (_vault.ContainsFile(to) || IsFolder(to)))
        {
            throw new IOException($"« {destination} » existe déjà.");
        }
        _vault.MoveFile(from, to);
    }

    /// <inheritdoc/>
    public override void MoveDirectory(string source, string destination)
    {
        var from = Inside(source);
        if (from.Length == 0 || !IsFolder(from))
        {
            throw new DirectoryNotFoundException($"Dossier introuvable : « {source} ».");
        }
        var to = Inside(destination);
        if (to.Length == 0 || (!SameName(from, to) && (_vault.ContainsFile(to) || IsFolder(to))))
        {
            throw new IOException($"« {destination} » existe déjà.");
        }
        if (to.StartsWith(from + "/", StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException($"« {source} » ne peut pas aller dans l'un de ses propres sous-dossiers.");
        }
        CheckParent(to, destination);
        _vault.MoveFolder(from, to);
    }

    /// <summary>Efface un fichier, ou un dossier avec tout ce qu'il contient : l'index d'abord, puis les objets. La racine reste.</summary>
    public override void Purge(string path)
    {
        var relative = Inside(path);
        if (relative.Length == 0)
        {
            throw new IOException("La racine du coffre ne s'efface pas.");
        }
        if (_vault.ContainsFile(relative) || _vault.ContainsFolder(relative))
        {
            _vault.Purge(relative);
        }
    }

    /// <inheritdoc/>
    public override IReadOnlyList<StorageEntry> Entries(string folder)
    {
        var relative = ExistingFolder(folder);
        var entries = new List<StorageEntry>();
        foreach (var child in _vault.Folders.Where(other => IsChild(other, relative)))
        {
            entries.Add(new StorageEntry(Path.Combine(folder, NameOf(child)), isDirectory: true));
        }
        foreach (var child in _vault.Files.Where(file => IsChild(file, relative)))
        {
            entries.Add(new StorageEntry(Path.Combine(folder, NameOf(child)), isDirectory: false, () => (_vault.LengthOf(child), _vault.LastWriteTimeUtc(child))));
        }
        return entries;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<string> Files(string folder, string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var expression = FileSystemName.TranslateWin32Expression(pattern);
        return [.. Entries(folder)
            .Where(entry => !entry.IsDirectory && FileSystemName.MatchesWin32Expression(expression, entry.Name, ignoreCase: true))
            .Select(entry => entry.FullName)];
    }

    /// <inheritdoc/>
    public override IReadOnlyList<StorageEntry> AllEntries(string folder)
    {
        var relative = ExistingFolder(folder);
        var facts = _vault.Describe();
        string Full(string path) => Path.Combine([folder, .. path[(relative.Length == 0 ? 0 : relative.Length + 1)..].Split('/')]);
        var entries = new List<StorageEntry>();
        foreach (var child in _vault.Folders.Where(other => IsBelow(other, relative)))
        {
            entries.Add(new StorageEntry(Full(child), isDirectory: true));
        }
        foreach (var child in _vault.Files.Where(file => IsBelow(file, relative)))
        {
            var known = facts[child];
            entries.Add(new StorageEntry(Full(child), isDirectory: false, () => known));
        }
        return entries;
    }

    /// <summary>Le chemin complet : il n'y a pas de lien dans un coffre chiffré.</summary>
    public override string RealPath(string path) => Path.GetFullPath(path);

    /// <summary>Le chemin sous <paramref name="realBase"/>, tel quel : il n'y a pas de lien dans un coffre chiffré.</summary>
    public override string RealPathBelow(string realBase, string relative) => Path.Combine(realBase, relative);

    /// <inheritdoc/>
    public override bool SameEntry(string first, string second) =>
        Exists(first) && Exists(second) && TryInside(first) is { } a && TryInside(second) is { } b && SameName(a, b);

    /// <summary>Relit l'index sur le disque (<see cref="EncryptedVault.Reload"/>).</summary>
    /// <exception cref="IOException">L'index est absent, altéré, ou d'un autre coffre ; celui qu'on avait reste.</exception>
    public override void Reload()
    {
        try
        {
            _vault.Reload();
        }
        catch (VaultCryptoException exception)
        {
            throw new IOException(exception.Message, exception);
        }
    }

    /// <summary>
    /// L'en-tête, l'index, et parmi les objets ceux que l'index nomme : un objet qu'un effacement n'a pas pu retirer n'est
    /// plus du coffre, et ne part pas dans ses sauvegardes. Tout le reste est recopié tel quel.
    /// </summary>
    public override IEnumerable<string> FilesToBackUp(IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var named = _vault.NamedObjects;
        return files.Where(file => ObjectOf(file) is not { } objectId || named.Contains(objectId));
    }

    /// <summary>
    /// Les fichiers d'une sauvegarde de ce coffre : son index, ouvert avec la clé du coffre, nomme chaque fichier et son
    /// objet, déchiffré à la lecture. Un fichier dont l'objet manque à l'archive n'y est pas, comme dans la sauvegarde d'un
    /// coffre en clair à qui manquait un fichier. Une sauvegarde faite avant le chiffrement se lit telle quelle.
    /// </summary>
    /// <exception cref="IOException">La sauvegarde est d'un autre coffre, ou son index est altéré ou absent.</exception>
    public override IReadOnlyList<StoredFile> FilesOfBackup(IReadOnlyList<StoredFile> entries, string backup)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var raw = new Dictionary<string, StoredFile>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            raw.TryAdd(entry.Path, entry);
        }
        if (!raw.ContainsKey(EncryptedVault.HeaderFile))
        {
            return entries;
        }
        if (!raw.TryGetValue(EncryptedVault.IndexFile, out var index))
        {
            throw new IOException("sauvegarde chiffrée sans index");
        }
        IReadOnlyList<(string Path, string Object)> files;
        try
        {
            files = _vault.OpenIndex(index.Read(), out _);
        }
        catch (VaultCryptoException exception)
        {
            throw new IOException($"sauvegarde d'un autre coffre, ou altérée ({exception.Message})", exception);
        }
        var found = new List<StoredFile>();
        foreach (var (path, objectId) in files)
        {
            if (raw.TryGetValue($"{EncryptedVault.Objects}/{objectId}{EncryptedVault.ObjectExtension}", out var sealedEntry))
            {
                found.Add(new StoredFile(path, Math.Max(0, sealedEntry.Length - SealedFile.Overhead), () => OpenObject(objectId, sealedEntry, path)));
            }
        }
        return found;
    }

    /// <summary>Le fichier de l'objet d'un fichier du coffre, pour les essais.</summary>
    internal string ObjectFileOf(string path) => _vault.ObjectFileOf(ExistingFile(path));

    /// <inheritdoc/>
    public void Dispose() => _vault.Dispose();

    private byte[] OpenObject(string objectId, StoredFile sealedEntry, string path)
    {
        try
        {
            return _vault.OpenObject(objectId, sealedEntry.Read());
        }
        catch (VaultCryptoException exception)
        {
            throw new IOException($"« {path} » ne s'ouvre pas : {exception.Message}", exception);
        }
    }

    /// <summary>Le chemin dans le coffre, séparé par <c>/</c> ; vide pour la racine, rien hors du coffre.</summary>
    private string? TryInside(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var full = Path.GetFullPath(path);
        foreach (var root in (string[])[_root, _realRoot])
        {
            var relative = Path.GetRelativePath(root, full);
            if (relative == ".")
            {
                return string.Empty;
            }
            if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return EncryptedVault.TryNormalize(relative);
            }
        }
        return null;
    }

    private string Inside(string path) =>
        TryInside(path) ?? throw new UnauthorizedAccessException($"« {path} » n'est pas dans le coffre chiffré.");

    /// <summary>L'identifiant de l'objet que le fichier posé sur le disque <paramref name="file"/> contient ; rien si ce n'est pas un objet.</summary>
    private string? ObjectOf(string file)
    {
        var full = Path.GetFullPath(file);
        foreach (var root in (string[])[_root, _realRoot])
        {
            var parent = Path.GetDirectoryName(full);
            if (string.Equals(parent, Path.Combine(root, EncryptedVault.Objects), StringComparison.OrdinalIgnoreCase)
                && full.EndsWith(EncryptedVault.ObjectExtension, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileNameWithoutExtension(full);
            }
        }
        return null;
    }

    private string ExistingFile(string path)
    {
        var relative = Inside(path);
        if (relative.Length > 0 && _vault.ContainsFile(relative))
        {
            return relative;
        }
        if (IsFolder(relative))
        {
            throw new UnauthorizedAccessException($"« {path} » est un dossier.");
        }
        CheckParent(relative, path);
        throw new FileNotFoundException($"Fichier introuvable : « {path} ».", path);
    }

    private string ExistingFolder(string path)
    {
        var relative = Inside(path);
        return IsFolder(relative) ? relative : throw new DirectoryNotFoundException($"Dossier introuvable : « {path} ».");
    }

    /// <summary>Le chemin d'un fichier à écrire : son dossier doit exister.</summary>
    private string FileToWrite(string path)
    {
        var relative = Inside(path);
        if (relative.Length == 0)
        {
            throw new UnauthorizedAccessException($"« {path} » est la racine du coffre.");
        }
        CheckParent(relative, path);
        return relative;
    }

    private void CheckParent(string relative, string path)
    {
        var slash = relative.LastIndexOf('/');
        if (slash > 0 && !_vault.ContainsFolder(relative[..slash]))
        {
            throw new DirectoryNotFoundException($"Dossier introuvable pour « {path} ».");
        }
    }

    private bool IsFolder(string relative) => relative.Length == 0 || _vault.ContainsFolder(relative);

    private static bool SameName(string first, string second) => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private static string NameOf(string path) => path[(path.LastIndexOf('/') + 1)..];

    /// <summary>Vrai si <paramref name="path"/> est directement dans <paramref name="folder"/> (vide : la racine).</summary>
    private static bool IsChild(string path, string folder)
    {
        var slash = path.LastIndexOf('/');
        return folder.Length == 0 ? slash < 0 : slash == folder.Length && IsBelow(path, folder);
    }

    /// <summary>Vrai si <paramref name="path"/> est sous <paramref name="folder"/>, à toute profondeur (vide : la racine).</summary>
    private static bool IsBelow(string path, string folder) =>
        folder.Length == 0 || (path.Length > folder.Length && path[folder.Length] == '/' && path.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
}
