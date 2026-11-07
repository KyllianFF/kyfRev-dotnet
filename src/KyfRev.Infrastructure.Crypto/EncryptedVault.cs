using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Crypto;

/// <summary>
/// Un coffre chiffré sur le disque. Son en-tête, en clair, ne contient rien de secret (<see cref="HeaderFile"/>). Chaque
/// fichier du coffre devient un objet opaque, <c>objets/&lt;identifiant&gt;.kyf</c>, et un index chiffré (<see cref="IndexFile"/>)
/// relie chaque chemin à son objet : ni les titres, ni les dossiers ne se lisent sur le disque. Seuls le nombre d'objets
/// et leurs tailles se voient.
/// </summary>
/// <remarks>
/// <para>
/// Comme sous Windows, la casse ne distingue pas deux chemins, mais elle est gardée : un coffre déchiffré revient tel qu'il
/// était. Le disque passe par un <see cref="VaultStorage"/> : celui de Windows, ou dans les essais un disque qui échoue à
/// la demande.
/// </para>
/// <para>
/// Deux façons d'écrire. <see cref="Write"/> et <see cref="AddFolder"/> écrivent les objets tout de suite et l'index au
/// <see cref="Flush"/> : une conversion qui écrit tout le coffre d'un coup. Les autres méthodes (<see cref="Save"/>,
/// <see cref="MoveFile"/>…) écrivent l'index aussitôt, ou ne changent rien si son écriture échoue : celles de la session
/// du coffre.
/// </para>
/// </remarks>
public sealed class EncryptedVault : IDisposable
{
    /// <summary>L'en-tête, à la racine : le format, l'identifiant du coffre, les enveloppes de la clé de données.</summary>
    public const string HeaderFile = "kyfrev-chiffre.json";

    /// <summary>L'index chiffré, à la racine.</summary>
    public const string IndexFile = "index.kyf";

    /// <summary>Le dossier des objets.</summary>
    public const string Objects = "objets";

    /// <summary>L'extension d'un objet.</summary>
    public const string ObjectExtension = ".kyf";

    /// <summary>Le format de l'index.</summary>
    public const int IndexFormat = 1;

    /// <summary>L'identifiant qui lie l'index à son coffre, dans ses données associées.</summary>
    internal const string IndexId = "index";

    /// <summary>La date d'un fichier absent, comme le disque la donne : le 1er janvier 1601.</summary>
    internal static readonly DateTime Missing = DateTime.FromFileTimeUtc(0);

    private static readonly char[] Crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();

    private readonly byte[] _key;
    private Dictionary<string, Entry> _files = new(StringComparer.OrdinalIgnoreCase);
    private SortedSet<string> _folders = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    private EncryptedVault(string root, VaultStorage disk, string vaultId, ReadOnlySpan<byte> dataKey)
    {
        Root = root;
        Disk = disk;
        VaultId = vaultId;
        _key = dataKey.ToArray();
    }

    /// <summary>L'identifiant du coffre, lu dans son en-tête.</summary>
    public string VaultId { get; }

    /// <summary>Le dossier du coffre, chemin complet.</summary>
    public string Root { get; }

    /// <summary>Le disque où sont posés l'en-tête, l'index et les objets.</summary>
    public VaultStorage Disk { get; }

    /// <summary>Les chemins des fichiers du coffre, séparés par <c>/</c>, dans l'ordre.</summary>
    public IReadOnlyCollection<string> Files => [.. _files.Values.Select(entry => entry.Path).Order(StringComparer.Ordinal)];

    /// <summary>Les dossiers du coffre, vides compris, dans l'ordre.</summary>
    public IReadOnlyCollection<string> Folders => _folders;

    /// <summary>Vrai si <paramref name="root"/> contient un coffre chiffré.</summary>
    public static bool IsEncrypted(string root) => File.Exists(Path.Combine(root, HeaderFile));

    /// <summary>L'en-tête du coffre chiffré de <paramref name="root"/>, lu sur le disque, ou par <paramref name="disk"/>.</summary>
    /// <exception cref="VaultCryptoException">L'en-tête est absent ou illisible.</exception>
    public static VaultHeader ReadHeader(string root, VaultStorage? disk = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var path = Path.Combine(root, HeaderFile);
        try
        {
            return VaultHeader.Read(disk is null ? File.ReadAllBytes(path) : disk.ReadAllBytes(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new VaultCryptoException("L'en-tête du coffre chiffré est introuvable ou illisible.", exception);
        }
    }

    /// <summary>
    /// Crée un coffre chiffré vide dans <paramref name="root"/>, qui doit exister et être vide : son en-tête, puis un index sans
    /// fichier. La clé de données est celle que l'en-tête enveloppe.
    /// </summary>
    /// <param name="root">Le dossier, vide.</param>
    /// <param name="header">L'en-tête.</param>
    /// <param name="dataKey">La clé de données que l'en-tête enveloppe ; le coffre en garde une copie, effacée à sa libération.</param>
    /// <param name="disk">Le disque où poser l'en-tête, l'index et les objets.</param>
    public static EncryptedVault Create(string root, VaultHeader header, ReadOnlySpan<byte> dataKey, VaultStorage disk)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(disk);
        root = Path.GetFullPath(root);
        if (!disk.DirectoryExists(root) || disk.Entries(root).Count > 0)
        {
            throw new VaultCryptoException("Un coffre chiffré se crée dans un dossier vide.");
        }
        disk.CreateDirectory(Path.Combine(root, Objects));
        disk.Replace(Path.Combine(root, HeaderFile), header.Write());
        var vault = new EncryptedVault(root, disk, header.VaultId, dataKey);
        try
        {
            vault.Flush();
            return vault;
        }
        catch
        {
            vault.Dispose();
            throw;
        }
    }

    /// <summary>Ouvre le coffre chiffré de <paramref name="root"/> avec sa clé de données, et lit son index. Rien n'est écrit.</summary>
    /// <param name="root">Le dossier du coffre.</param>
    /// <param name="dataKey">Sa clé de données ; le coffre en garde une copie, effacée à sa libération.</param>
    /// <param name="disk">Le disque où sont posés l'en-tête, l'index et les objets.</param>
    /// <exception cref="VaultCryptoException">L'index est absent, altéré, ou d'un autre coffre.</exception>
    public static EncryptedVault Open(string root, ReadOnlySpan<byte> dataKey, VaultStorage disk)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(disk);
        root = Path.GetFullPath(root);
        var header = ReadHeader(root, disk);
        var vault = new EncryptedVault(root, disk, header.VaultId, dataKey);
        try
        {
            vault.Reload();
            return vault;
        }
        catch
        {
            vault.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Relit l'index sur le disque : un autre processus a pu y écrire depuis l'ouverture. La session du coffre le relit
    /// une fois son verrou pris. Si l'index ne s'ouvre pas, celui qu'on avait reste.
    /// </summary>
    /// <exception cref="VaultCryptoException">L'index est absent, altéré, ou d'un autre coffre.</exception>
    public void Reload()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        byte[] sealedIndex;
        try
        {
            sealedIndex = Disk.ReadAllBytes(Path.Combine(Root, IndexFile));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new VaultCryptoException("L'index du coffre chiffré est introuvable ou illisible.", exception);
        }
        var files = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        var named = OpenIndex(sealedIndex, out var folders);
        foreach (var (path, objectId) in named)
        {
            if (!files.TryAdd(path, new Entry(path, objectId)))
            {
                throw new VaultCryptoException("L'index du coffre chiffré nomme deux fois le même fichier.");
            }
        }
        _files = files;
        _folders = new SortedSet<string>(folders, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Vrai si le fichier <paramref name="path"/> est dans le coffre.</summary>
    /// <exception cref="ObjectDisposedException">Le coffre est libéré.</exception>
    public bool ContainsFile(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _files.ContainsKey(Normalized(path));
    }

    /// <summary>Vrai si le dossier <paramref name="path"/> est dans le coffre.</summary>
    /// <exception cref="ObjectDisposedException">Le coffre est libéré.</exception>
    public bool ContainsFolder(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _folders.Contains(Normalized(path));
    }

    /// <summary>Le contenu du fichier <paramref name="path"/>.</summary>
    /// <exception cref="VaultCryptoException">Le fichier n'est pas dans l'index, ou son objet manque ou a été altéré.</exception>
    public byte[] Read(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var entry = EntryOf(path);
        byte[] sealedBytes;
        try
        {
            sealedBytes = Disk.ReadAllBytes(ObjectPath(entry.Object));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new VaultCryptoException($"L'objet de « {path} » est introuvable ou illisible.", exception);
        }
        return SealedFile.Open(_key, VaultId, entry.Object, sealedBytes);
    }

    /// <summary>
    /// Écrit <paramref name="content"/> sous le chemin <paramref name="path"/> : un objet neuf pour un nouveau fichier, le même
    /// objet réécrit (nonce neuf) pour un fichier existant. L'index ne change sur le disque qu'à <see cref="Flush"/>.
    /// </summary>
    public void Write(string path, ReadOnlySpan<byte> content)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = Normalized(path);
        if (_files.TryGetValue(key, out var entry))
        {
            WriteObject(entry.Object, content);
            return;
        }
        var objectId = NewObjectId();
        WriteObject(objectId, content);
        Add(Canonical(key), objectId);
    }

    /// <summary>Retient un dossier, vide ou non : il reviendra au déchiffrement. L'index ne change sur le disque qu'à <see cref="Flush"/>.</summary>
    public void AddFolder(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = Canonical(Normalized(path));
        _folders.Add(key);
        AddParents(key);
    }

    /// <summary>Écrit l'index, chiffré, de façon atomique.</summary>
    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var files = new JsonObject();
        foreach (var entry in _files.Values.OrderBy(entry => entry.Path, StringComparer.Ordinal))
        {
            files[entry.Path] = new JsonObject { ["objet"] = entry.Object };
        }
        var index = new JsonObject
        {
            ["format"] = IndexFormat,
            ["fichiers"] = files,
            ["dossiers"] = new JsonArray([.. _folders.Select(folder => (JsonNode?)JsonValue.Create(folder))]),
        };
        var plain = JsonSerializer.SerializeToUtf8Bytes(index);
        try
        {
            Disk.Replace(Path.Combine(Root, IndexFile), SealedFile.Seal(_key, VaultId, IndexId, plain));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>
    /// Écrit un fichier et, s'il est nouveau, l'index aussitôt. Un fichier existant garde son objet, remplacé d'un coup : il
    /// est l'ancien ou le nouveau, jamais un mélange. Si l'index ne s'écrit pas, le nouveau fichier n'existe pas.
    /// </summary>
    public void Save(string path, ReadOnlySpan<byte> content)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = Normalized(path);
        if (_files.TryGetValue(key, out var entry))
        {
            WriteObject(entry.Object, content);
            return;
        }
        var objectId = NewObjectId();
        WriteObject(objectId, content);
        try
        {
            Commit(() => Add(Canonical(key), objectId));
        }
        catch
        {
            _ = TryPurgeObject(objectId);
            throw;
        }
    }

    /// <summary>Crée un dossier, et ses parents, puis écrit l'index.</summary>
    public void CreateFolder(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = Normalized(path);
        if (!_folders.Contains(key))
        {
            Commit(() =>
            {
                var actual = Canonical(key);
                _folders.Add(actual);
                AddParents(actual);
            });
        }
    }

    /// <summary>Déplace ou renomme un fichier, puis écrit l'index ; son objet ne change pas.</summary>
    public void MoveFile(string source, string destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var entry = EntryOf(source);
        var target = Normalized(destination);
        Commit(() =>
        {
            _files.Remove(entry.Path);
            Add(Canonical(target), entry.Object);
        });
    }

    /// <summary>Déplace ou renomme un dossier avec tout ce qu'il contient, puis écrit l'index ; aucun objet ne change.</summary>
    public void MoveFolder(string source, string destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var from = FolderOf(source);
        var target = Normalized(destination);
        Commit(() =>
        {
            var to = Canonical(target);
            var files = _files.Values.Where(entry => IsWithin(entry.Path, from)).ToList();
            var folders = _folders.Where(folder => IsWithin(folder, from)).ToList();
            foreach (var entry in files)
            {
                _files.Remove(entry.Path);
            }
            _folders.ExceptWith(folders);
            foreach (var entry in files)
            {
                Add(to + entry.Path[from.Length..], entry.Object);
            }
            foreach (var folder in folders)
            {
                _folders.Add(to + folder[from.Length..]);
            }
            AddParents(to);
        });
    }

    /// <summary>
    /// Efface pour de bon un fichier, ou un dossier et tout ce qu'il contient : il quitte l'index, écrit aussitôt, puis ses
    /// objets sont effacés. Seul l'effacement demandé depuis la corbeille y recourt.
    /// </summary>
    /// <exception cref="VaultCryptoException">Ni fichier ni dossier ne porte ce chemin.</exception>
    /// <exception cref="LeftOnDiskException">
    /// Un objet n'a pas pu être effacé (un autre programme le tient ouvert) : le chemin a quitté le coffre, mais l'objet
    /// reste sur le disque. Le message le nomme ; aucune sauvegarde ne le recopie.
    /// </exception>
    public void Purge(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = Normalized(path);
        List<Entry> files;
        if (_files.TryGetValue(key, out var file))
        {
            files = [file];
            Commit(() => _files.Remove(file.Path));
        }
        else
        {
            var folder = FolderOf(path);
            files = [.. _files.Values.Where(entry => IsWithin(entry.Path, folder))];
            Commit(() =>
            {
                foreach (var entry in files)
                {
                    _files.Remove(entry.Path);
                }
                _folders.RemoveWhere(other => IsWithin(other, folder));
            });
        }
        var left = files.Select(entry => TryPurgeObject(entry.Object)).OfType<string>().ToList();
        if (left.Count > 0)
        {
            var objects = string.Join(" ; ", left);
            throw new LeftOnDiskException(left.Count == 1
                ? $"« {path} » a quitté le coffre, mais son objet n'a pas pu être effacé : {objects}. Il reste sur le disque ; aucune sauvegarde ne le recopie."
                : $"« {path} » a quitté le coffre, mais {left.Count} de ses objets n'ont pas pu être effacés : {objects}. Ils restent sur le disque ; aucune sauvegarde ne les recopie.");
        }
    }

    /// <summary>La taille en clair d'un fichier : celle de son objet, moins ce qu'ajoute le chiffrement.</summary>
    public long LengthOf(string path) => Math.Max(0, Disk.LengthOf(ObjectPath(EntryOf(path).Object)) - SealedFile.Overhead);

    /// <summary>La date de dernière écriture d'un fichier : celle de son objet, qu'un déplacement ne change pas.</summary>
    public DateTime LastWriteTimeUtc(string path) => Disk.LastWriteTimeUtc(ObjectPath(EntryOf(path).Object));

    /// <summary>La taille en clair et la date de chaque fichier, relevées en un seul parcours des objets.</summary>
    public IReadOnlyDictionary<string, (long Length, DateTime LastWriteTimeUtc)> Describe()
    {
        // Un objet renommé en minuscules reste le même, comme pour NTFS.
        var objects = new Dictionary<string, (long, DateTime)>(StringComparer.OrdinalIgnoreCase);
        var folder = Path.Combine(Root, Objects);
        if (Disk.DirectoryExists(folder))
        {
            foreach (var entry in Disk.Entries(folder).Where(entry => !entry.IsDirectory && entry.Name.EndsWith(ObjectExtension, StringComparison.OrdinalIgnoreCase)))
            {
                objects[entry.Name[..^ObjectExtension.Length]] = (Math.Max(0, entry.Length - SealedFile.Overhead), entry.LastWriteTimeUtc);
            }
        }
        return _files.Values.ToDictionary(
            entry => entry.Path,
            entry => objects.TryGetValue(entry.Object, out var facts) ? facts : (0L, Missing),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Libère le coffre : sa copie de la clé est effacée, et son index en clair (les chemins, donc les titres et les
    /// dossiers) oublié.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            CryptographicOperations.ZeroMemory(_key);
            _files = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            _folders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            _disposed = true;
        }
    }

    /// <summary>
    /// Les fichiers que nomme un index chiffré lu ailleurs (dans une sauvegarde), avec leur objet ; et ses dossiers.
    /// </summary>
    /// <exception cref="VaultCryptoException">L'index est altéré, d'un autre coffre, ou illisible.</exception>
    internal IReadOnlyList<(string Path, string Object)> OpenIndex(byte[] sealedIndex, out IReadOnlyList<string> folders)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var plain = SealedFile.Open(_key, VaultId, IndexId, sealedIndex);
        try
        {
            var index = JsonNode.Parse(plain) as JsonObject ?? throw new VaultCryptoException("L'index du coffre chiffré est illisible.");
            if ((int?)index["format"] != IndexFormat)
            {
                throw new VaultCryptoException("L'index du coffre chiffré est d'un format inconnu.");
            }
            var files = new List<(string, string)>();
            foreach (var (path, value) in index["fichiers"] as JsonObject ?? [])
            {
                var objectId = (string?)value?["objet"] ?? throw new VaultCryptoException("L'index du coffre chiffré est illisible.");
                if (objectId.Length != 26 || objectId.Any(c => !Crockford.Contains(c)))
                {
                    throw new VaultCryptoException("L'index du coffre chiffré désigne un objet invalide.");
                }
                files.Add((Normalized(path), objectId));
            }
            folders = [.. (index["dossiers"] as JsonArray ?? []).Select(folder => Normalized((string?)folder ?? string.Empty))];
            return files;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new VaultCryptoException("L'index du coffre chiffré est illisible.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>Le contenu d'un objet lu ailleurs (dans une sauvegarde), déchiffré.</summary>
    internal byte[] OpenObject(string objectId, byte[] sealedBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return SealedFile.Open(_key, VaultId, objectId, sealedBytes);
    }

    /// <summary>Les objets que l'index nomme : les seuls qu'une sauvegarde recopie. Leur casse ne compte pas, comme pour NTFS.</summary>
    internal HashSet<string> NamedObjects => _files.Values.Select(entry => entry.Object).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Le fichier de l'objet de <paramref name="path"/>, pour les essais.</summary>
    internal string ObjectFileOf(string path) => ObjectPath(EntryOf(path).Object);

    /// <summary>Un chemin du coffre : relatif, séparé par <c>/</c>, sans <c>..</c> ni segment vide.</summary>
    internal static string Normalized(string path) =>
        TryNormalize(path) ?? throw new VaultCryptoException($"« {path} » n'est pas un chemin de coffre.");

    /// <summary>Comme <see cref="Normalized"/>, sans exception : rien si ce n'est pas un chemin de coffre.</summary>
    internal static string? TryNormalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var parts = path.Replace('\\', '/').Split('/');
        return parts.Any(part => part.Length == 0 || part is "." or ".." || part.Contains(':', StringComparison.Ordinal))
            ? null
            : string.Join('/', parts);
    }

    /// <summary>Le chemin de l'objet <paramref name="objectId"/>.</summary>
    internal string ObjectPath(string objectId) => Path.Combine(Root, Objects, objectId + ObjectExtension);

    /// <summary>Vrai si <paramref name="path"/> est <paramref name="folder"/> ou se trouve dessous.</summary>
    private static bool IsWithin(string path, string folder) =>
        path.Equals(folder, StringComparison.OrdinalIgnoreCase)
        || (path.Length > folder.Length && path[folder.Length] == '/' && path.StartsWith(folder, StringComparison.OrdinalIgnoreCase));

    private Entry EntryOf(string path) =>
        _files.TryGetValue(Normalized(path), out var entry) ? entry : throw new VaultCryptoException($"« {path} » n'est pas dans le coffre chiffré.");

    private string FolderOf(string path) =>
        _folders.TryGetValue(Normalized(path), out var folder) ? folder : throw new VaultCryptoException($"Le dossier « {path} » n'est pas dans le coffre chiffré.");

    /// <summary>
    /// Change l'index, puis l'écrit ; si son écriture échoue, l'index en mémoire redevient celui du disque.
    /// </summary>
    private void Commit(Action change)
    {
        var files = new Dictionary<string, Entry>(_files, _files.Comparer);
        var folders = new SortedSet<string>(_folders, _folders.Comparer);
        try
        {
            change();
            Flush();
        }
        catch
        {
            _files = files;
            _folders = folders;
            throw;
        }
    }

    private void Add(string path, string objectId)
    {
        _files[path] = new Entry(path, objectId);
        AddParents(path);
    }

    /// <summary>Le chemin, ses dossiers écrits avec la casse qu'ils ont déjà : un fichier rejoint le dossier qui existe.</summary>
    private string Canonical(string key)
    {
        var slash = key.LastIndexOf('/');
        if (slash < 0)
        {
            return key;
        }
        var parent = key[..slash];
        return (_folders.TryGetValue(parent, out var actual) ? actual : Canonical(parent)) + key[slash..];
    }

    private void AddParents(string path)
    {
        for (var slash = path.LastIndexOf('/'); slash > 0; slash = path.LastIndexOf('/', slash - 1))
        {
            _folders.Add(path[..slash]);
        }
    }

    private void WriteObject(string objectId, ReadOnlySpan<byte> content) =>
        Disk.Replace(ObjectPath(objectId), SealedFile.Seal(_key, VaultId, objectId, content));

    /// <summary>
    /// Le seul point du coffre chiffré qui efface : un objet que l'index ne nomme plus. S'il reste, aucune entrée ne le
    /// nomme et aucune sauvegarde ne le recopie ; seule sa taille se voit sur le disque.
    /// </summary>
    /// <returns>Rien si l'objet est effacé ; sinon son fichier et la raison, pour le dire.</returns>
    private string? TryPurgeObject(string objectId)
    {
        try
        {
            Disk.Purge(ObjectPath(objectId));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"{Objects}/{objectId}{ObjectExtension} ({exception.Message})";
        }
    }

    /// <summary>Un identifiant d'objet : 26 caractères de base32 de Crockford, tirés au hasard (130 bits).</summary>
    private static string NewObjectId()
    {
        var bytes = RandomNumberGenerator.GetBytes(26);
        return string.Create(26, bytes, (characters, random) =>
        {
            for (var i = 0; i < characters.Length; i++)
            {
                characters[i] = Crockford[random[i] & 31];
            }
        });
    }

    /// <summary>Un fichier de l'index : son chemin, tel qu'écrit, et son objet.</summary>
    private sealed record Entry(string Path, string Object);
}
