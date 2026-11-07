using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KyfRev.Infrastructure.Crypto;

/// <summary>
/// Un coffre chiffré sur le disque. Son en-tête, en clair, ne contient rien de secret (<see cref="HeaderFile"/>). Chaque
/// fichier du coffre devient un objet opaque, <c>objets/&lt;identifiant&gt;.kyf</c>, et un index chiffré (<see cref="IndexFile"/>)
/// relie chaque chemin à son objet : ni les titres, ni les dossiers, ni les tailles exactes ne se lisent sur le disque. Seuls
/// le nombre d'objets et leurs tailles chiffrées se voient.
/// </summary>
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
    private const string IndexId = "index";

    private const string TemporaryPrefix = ".ecriture-";

    private static readonly char[] Crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();

    private readonly string _root;
    private readonly byte[] _key;
    private readonly SortedDictionary<string, (string Object, long Length)> _files = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _folders = new(StringComparer.Ordinal);
    private bool _disposed;

    private EncryptedVault(string root, string vaultId, ReadOnlySpan<byte> dataKey)
    {
        _root = root;
        VaultId = vaultId;
        _key = dataKey.ToArray();
    }

    /// <summary>L'identifiant du coffre, lu dans son en-tête.</summary>
    public string VaultId { get; }

    /// <summary>Les chemins des fichiers du coffre, séparés par <c>/</c>, dans l'ordre.</summary>
    public IReadOnlyCollection<string> Files => _files.Keys;

    /// <summary>Les dossiers du coffre, vides compris, dans l'ordre.</summary>
    public IReadOnlyCollection<string> Folders => _folders;

    /// <summary>Vrai si <paramref name="root"/> contient un coffre chiffré.</summary>
    public static bool IsEncrypted(string root) => File.Exists(Path.Combine(root, HeaderFile));

    /// <summary>L'en-tête du coffre chiffré de <paramref name="root"/>.</summary>
    /// <exception cref="VaultCryptoException">L'en-tête est absent ou illisible.</exception>
    public static VaultHeader ReadHeader(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        try
        {
            return VaultHeader.Read(File.ReadAllBytes(Path.Combine(root, HeaderFile)));
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
    public static EncryptedVault Create(string root, VaultHeader header, ReadOnlySpan<byte> dataKey)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(header);
        if (!Directory.Exists(root) || Directory.EnumerateFileSystemEntries(root).Any())
        {
            throw new VaultCryptoException("Un coffre chiffré se crée dans un dossier vide.");
        }
        Directory.CreateDirectory(Path.Combine(root, Objects));
        Replace(Path.Combine(root, HeaderFile), header.Write());
        var vault = new EncryptedVault(root, header.VaultId, dataKey);
        vault.Flush();
        return vault;
    }

    /// <summary>Ouvre le coffre chiffré de <paramref name="root"/> avec sa clé de données, et lit son index.</summary>
    /// <exception cref="VaultCryptoException">L'index est absent, altéré, ou d'un autre coffre.</exception>
    public static EncryptedVault Open(string root, ReadOnlySpan<byte> dataKey)
    {
        var header = ReadHeader(root);
        var vault = new EncryptedVault(root, header.VaultId, dataKey);
        try
        {
            vault.ReadIndex();
            return vault;
        }
        catch
        {
            vault.Dispose();
            throw;
        }
    }

    /// <summary>Le contenu du fichier <paramref name="path"/>.</summary>
    /// <exception cref="VaultCryptoException">Le fichier n'est pas dans l'index, ou son objet manque ou a été altéré.</exception>
    public byte[] Read(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_files.TryGetValue(Normalized(path), out var entry))
        {
            throw new VaultCryptoException($"« {path} » n'est pas dans le coffre chiffré.");
        }
        byte[] sealedBytes;
        try
        {
            sealedBytes = File.ReadAllBytes(ObjectPath(entry.Object));
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
        var objectId = _files.TryGetValue(key, out var entry) ? entry.Object : NewObjectId();
        Replace(ObjectPath(objectId), SealedFile.Seal(_key, VaultId, objectId, content));
        _files[key] = (objectId, content.Length);
        AddParents(key);
    }

    /// <summary>Retient un dossier, vide ou non : il reviendra au déchiffrement.</summary>
    public void AddFolder(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = Normalized(path);
        _folders.Add(key);
        AddParents(key);
    }

    /// <summary>La taille en clair d'un fichier, d'après l'index.</summary>
    public long LengthOf(string path) =>
        _files.TryGetValue(Normalized(path), out var entry) ? entry.Length : throw new VaultCryptoException($"« {path} » n'est pas dans le coffre chiffré.");

    /// <summary>Écrit l'index, chiffré, de façon atomique.</summary>
    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var files = new JsonObject();
        foreach (var (path, entry) in _files)
        {
            files[path] = new JsonObject { ["objet"] = entry.Object, ["taille"] = entry.Length };
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
            Replace(Path.Combine(_root, IndexFile), SealedFile.Seal(_key, VaultId, IndexId, plain));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            CryptographicOperations.ZeroMemory(_key);
            _disposed = true;
        }
    }

    /// <summary>Un chemin du coffre : relatif, séparé par <c>/</c>, sans <c>..</c> ni segment vide.</summary>
    internal static string Normalized(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var parts = path.Replace('\\', '/').Split('/');
        if (parts.Any(part => part.Length == 0 || part is "." or ".." || part.Contains(':', StringComparison.Ordinal)))
        {
            throw new VaultCryptoException($"« {path} » n'est pas un chemin de coffre.");
        }
        return string.Join('/', parts);
    }

    private void ReadIndex()
    {
        byte[] sealedBytes;
        try
        {
            sealedBytes = File.ReadAllBytes(Path.Combine(_root, IndexFile));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new VaultCryptoException("L'index du coffre chiffré est introuvable ou illisible.", exception);
        }
        var plain = SealedFile.Open(_key, VaultId, IndexId, sealedBytes);
        try
        {
            var index = JsonNode.Parse(plain) as JsonObject ?? throw new VaultCryptoException("L'index du coffre chiffré est illisible.");
            if ((int?)index["format"] != IndexFormat)
            {
                throw new VaultCryptoException("L'index du coffre chiffré est d'un format inconnu.");
            }
            foreach (var (path, value) in index["fichiers"] as JsonObject ?? [])
            {
                var objectId = (string?)value?["objet"] ?? throw new VaultCryptoException("L'index du coffre chiffré est illisible.");
                if (objectId.Length != 26 || objectId.Any(c => !Crockford.Contains(c)))
                {
                    throw new VaultCryptoException("L'index du coffre chiffré désigne un objet invalide.");
                }
                _files[Normalized(path)] = (objectId, (long?)value?["taille"] ?? 0);
            }
            foreach (var folder in index["dossiers"] as JsonArray ?? [])
            {
                _folders.Add(Normalized((string?)folder ?? string.Empty));
            }
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

    private void AddParents(string path)
    {
        for (var slash = path.LastIndexOf('/'); slash > 0; slash = path.LastIndexOf('/', slash - 1))
        {
            _folders.Add(path[..slash]);
        }
    }

    private string ObjectPath(string objectId) => Path.Combine(_root, Objects, objectId + ObjectExtension);

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

    /// <summary>Écrit un fichier de façon atomique : un fichier temporaire voisin, puis un remplacement.</summary>
    private static void Replace(string path, byte[] content)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, TemporaryPrefix + Path.GetRandomFileName());
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, path, overwrite: true);
    }
}
