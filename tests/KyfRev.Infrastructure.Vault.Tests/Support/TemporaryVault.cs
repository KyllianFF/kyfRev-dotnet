using System.Security.Cryptography;
using KyfRev.Application.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Ids;

namespace KyfRev.Infrastructure.Vault.Tests.Support;

/// <summary>Où un essai pose son coffre : sur le disque tel quel, ou chiffré.</summary>
public enum StorageKind
{
    /// <summary>Le disque tel quel : un coffre en clair.</summary>
    Disk,

    /// <summary>Un coffre chiffré : un index et des objets opaques.</summary>
    Encrypted,
}

/// <summary>
/// Copie jetable du coffre de référence (ou dossier vide), effacée à la fin du test. Un coffre chiffré y est lu et écrit
/// par le même stockage que sa session : ce que l'essai y change, la session le voit comme un changement fait hors d'elle.
/// </summary>
internal sealed class TemporaryVault : IDisposable
{
    /// <summary>Un en-tête et sa clé pour tous les coffres chiffrés des essais : dériver un vrai réglage prend une fraction de seconde.</summary>
    private static readonly Lazy<(VaultHeader Header, byte[] Key)> Shared = new(() =>
    {
        var (header, key, _) = VaultHeader.Create("v_01M69JFV2A3WD8KH55EHWC1G7N", "un mot de passe d'essai", PasswordKeys.NewPbkdf2());
        return (header, key);
    });

    private readonly EncryptedStorage? _encrypted;

    private TemporaryVault(string root, EncryptedStorage? encrypted)
    {
        Root = root;
        _encrypted = encrypted;
    }

    /// <summary>Coffre de référence produit par la version 0.x (voir tools/reference-vectors).</summary>
    public static string Reference => Path.Combine(AppContext.BaseDirectory, "fixtures", "coffre-reference");

    /// <summary>La clé de données des coffres chiffrés des essais.</summary>
    public static ReadOnlySpan<byte> Key => Shared.Value.Key;

    public string Root { get; }

    public StorageKind Kind => _encrypted is null ? StorageKind.Disk : StorageKind.Encrypted;

    /// <summary>Là où vivent les fichiers du coffre : le disque, ou le stockage chiffré.</summary>
    public VaultStorage Storage => _encrypted ?? VaultStorage.Disk;

    public static TemporaryVault CopyOfReference(StorageKind kind = StorageKind.Disk)
    {
        var vault = Empty(kind);
        foreach (var file in Directory.EnumerateFiles(Reference, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Reference, file).Replace('\\', '/');
            // Le verrou reste sur le disque, même pour un coffre chiffré.
            if (kind == StorageKind.Disk || relative is VaultLayout.LockFile or VaultLayout.LockInfoFile)
            {
                var target = vault.PathOf(relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            else
            {
                vault.WriteBytes(relative, File.ReadAllBytes(file));
                vault.SetLastWriteTimeUtc(relative, File.GetLastWriteTimeUtc(file));
            }
        }
        return vault;
    }

    public static TemporaryVault Empty() => Empty(StorageKind.Disk);

    public static TemporaryVault Empty(StorageKind kind)
    {
        var root = Path.Combine(Path.GetTempPath(), "kyfrev-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TemporaryVault(root, kind == StorageKind.Encrypted ? EncryptedStorage.Create(root, Shared.Value.Header, Shared.Value.Key) : null);
    }

    /// <summary>Ouvre le coffre chiffré de <paramref name="root"/> avec la clé des essais.</summary>
    public static EncryptedStorage OpenEncrypted(string root) => EncryptedStorage.Open(root, Key);

    /// <summary>Vrai si le coffre de <paramref name="root"/>, chiffré ou non, contient le fichier <paramref name="relative"/>.</summary>
    public static bool HasVaultFile(string root, string relative)
    {
        var path = Path.Combine([root, .. relative.Split('/')]);
        if (!EncryptedVault.IsEncrypted(root))
        {
            return File.Exists(path);
        }
        using var storage = OpenEncrypted(root);
        return storage.FileExists(path);
    }

    public string PathOf(string relative) => Path.Combine([Root, .. relative.Split('/')]);

    public VaultSession Initialize(Moment now, UlidGenerator? ids = null) => VaultSession.Initialize(Root, now, ids, Storage);

    public VaultSession Open(Moment now, UlidGenerator? ids = null) => VaultSession.Open(Root, now, ids, Storage);

    public void Write(string relative, string content)
    {
        if (_encrypted is null)
        {
            var path = PathOf(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return;
        }
        WriteBytes(relative, System.Text.Encoding.UTF8.GetBytes(content));
    }

    public void WriteBytes(string relative, byte[] content)
    {
        var path = PathOf(relative);
        Storage.CreateDirectory(Path.GetDirectoryName(path)!);
        if (_encrypted is null)
        {
            File.WriteAllBytes(path, content);
        }
        else
        {
            _encrypted.Replace(path, content);
        }
    }

    /// <summary>Un fichier de <paramref name="length"/> octets nuls.</summary>
    public void WriteZeros(string relative, long length)
    {
        if (_encrypted is null)
        {
            using var file = File.Create(PathOf(relative));
            file.SetLength(length);
            return;
        }
        WriteBytes(relative, new byte[length]);
    }

    /// <summary>Copie un fichier, sa date comprise.</summary>
    public void Copy(string from, string to)
    {
        if (_encrypted is null)
        {
            File.Copy(PathOf(from), PathOf(to));
            return;
        }
        WriteBytes(to, ReadBytes(from));
        SetLastWriteTimeUtc(to, Storage.LastWriteTimeUtc(PathOf(from)));
    }

    public void Append(string relative, string text) =>
        WriteBytes(relative, [.. ReadBytes(relative), .. System.Text.Encoding.UTF8.GetBytes(text)]);

    public byte[] ReadBytes(string relative) => Storage.ReadAllBytes(PathOf(relative));

    public string ReadText(string relative) => Storage.ReadAllText(PathOf(relative));

    public bool FileExists(string relative) => Storage.FileExists(PathOf(relative));

    public bool DirectoryExists(string relative) => Storage.DirectoryExists(PathOf(relative));

    public void CreateDirectory(string relative) => Storage.CreateDirectory(PathOf(relative));

    /// <summary>Les dossiers de <paramref name="relative"/>, chemins complets.</summary>
    public string[] Directories(string relative) =>
        [.. Storage.Entries(PathOf(relative)).Where(entry => entry.IsDirectory).Select(entry => entry.FullName)];

    /// <summary>Efface un fichier, ou un dossier et tout ce qu'il contient.</summary>
    public void Delete(string relative)
    {
        var path = PathOf(relative);
        if (Storage.DirectoryExists(path))
        {
            Storage.DeleteDirectory(path);
        }
        else
        {
            Storage.DeleteFile(path);
        }
    }

    /// <summary>Déplace un fichier ou un dossier, en créant le dossier qui le reçoit.</summary>
    public void Move(string from, string to)
    {
        var (source, destination) = (PathOf(from), PathOf(to));
        Storage.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (Storage.DirectoryExists(source))
        {
            Storage.MoveDirectory(source, destination);
        }
        else
        {
            Storage.MoveFile(source, destination);
        }
    }

    /// <summary>La date d'un fichier, ou celle de son objet dans un coffre chiffré.</summary>
    public void SetLastWriteTimeUtc(string relative, DateTime time) => File.SetLastWriteTimeUtc(OnDisk(relative), time);

    /// <summary>Tient le fichier (ou son objet) ouvert, sans partage : personne d'autre ne peut l'écrire.</summary>
    public FileStream Hold(string relative) => new(OnDisk(relative), FileMode.Open, FileAccess.Read, FileShare.None);

    /// <summary>
    /// Chaque fichier du coffre tel que la session le voit, et ce qui est posé à côté sur le disque (le verrou, un dossier de
    /// sauvegarde refusé) ; pour un coffre chiffré, ni son en-tête, ni son index, ni ses objets.
    /// </summary>
    public SortedDictionary<string, byte[]> Files()
    {
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            var relative = Relative(file);
            if (_encrypted is null || !IsEncryptedStructure(relative))
            {
                files[relative] = File.ReadAllBytes(file);
            }
        }
        foreach (var entry in _encrypted?.AllEntries(Root) ?? [])
        {
            if (!entry.IsDirectory)
            {
                files[Relative(entry.FullName)] = _encrypted!.ReadAllBytes(entry.FullName);
            }
        }
        return files;
    }

    /// <summary>Les dossiers, comme <see cref="Files"/>, dans l'ordre UTF-8.</summary>
    public List<string> Folders()
    {
        var folders = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var folder in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories))
        {
            var relative = Relative(folder);
            if (_encrypted is null || !IsEncryptedStructure(relative))
            {
                folders.Add(relative);
            }
        }
        foreach (var entry in _encrypted?.AllEntries(Root) ?? [])
        {
            if (entry.IsDirectory)
            {
                folders.Add(Relative(entry.FullName));
            }
        }
        return [.. folders.Order(Utf8Order.Instance)];
    }

    /// <summary>Empreinte, taille et date de chaque fichier sur le disque : de quoi prouver que rien n'a été écrit.</summary>
    public SortedDictionary<string, string> Snapshot()
    {
        var snapshot = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            snapshot[Path.GetRelativePath(Root, file)] =
                $"{Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)))}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        }
        return snapshot;
    }

    public void Dispose()
    {
        _encrypted?.Dispose();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static bool IsEncryptedStructure(string relative) =>
        relative is EncryptedVault.HeaderFile or EncryptedVault.IndexFile or EncryptedVault.Objects
        || relative.StartsWith(EncryptedVault.Objects + "/", StringComparison.Ordinal);

    private string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private string OnDisk(string relative) => _encrypted?.ObjectFileOf(PathOf(relative)) ?? PathOf(relative);
}
