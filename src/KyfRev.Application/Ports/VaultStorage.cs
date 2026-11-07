using System.Text;

namespace KyfRev.Application.Ports;

/// <summary>
/// Là où vivent les fichiers d'un coffre. Toute lecture, écriture, création, déplacement, effacement, énumération,
/// vérification d'existence ou de date d'un fichier du coffre passe par ici : le disque tel quel pour un coffre en clair
/// (<c>DiskStorage</c>, dans <c>KyfRev.Infrastructure.Vault</c>), le stockage d'un coffre chiffré sinon
/// (<c>EncryptedStorage</c>, dans <c>KyfRev.Infrastructure.Crypto</c>). La racine de composition choisit lequel.
/// </summary>
/// <remarks>
/// <para>
/// Les chemins sont absolus, comme ceux du disque. Un stockage lié à un dossier (<see cref="Root"/>) n'accepte que des
/// chemins de ce dossier. Les erreurs sont celles du disque : <see cref="IOException"/> (dont
/// <see cref="FileNotFoundException"/> et <see cref="DirectoryNotFoundException"/>) ou
/// <see cref="UnauthorizedAccessException"/>, et chaque méthode se comporte comme son équivalent de <c>System.IO</c>.
/// </para>
/// <para>
/// Ce qui n'est pas un fichier du coffre reste sur le disque, hors d'ici : le verrou (<c>.suivi/verrou</c> et
/// <c>.suivi/verrou-info.json</c>), les dossiers des sauvegardes et des exports, les notes à importer.
/// </para>
/// </remarks>
public abstract class VaultStorage
{
    /// <summary>Le dossier que ce stockage sert ; rien pour le disque entier.</summary>
    public virtual string? Root => null;

    /// <summary>
    /// Les fichiers tels qu'ils sont posés sur le disque : le stockage lui-même pour le disque, les objets chiffrés et
    /// leur index pour un coffre chiffré. Une sauvegarde les recopie tels quels.
    /// </summary>
    public virtual VaultStorage Raw => this;

    /// <summary>Comme <see cref="File.Exists(string)"/>.</summary>
    public abstract bool FileExists(string path);

    /// <summary>Comme <see cref="Directory.Exists(string)"/>.</summary>
    public abstract bool DirectoryExists(string path);

    /// <summary>Fichier ou dossier existant (un lien cassé n'existe pas).</summary>
    public bool Exists(string path) => FileExists(path) || DirectoryExists(path);

    /// <summary>Comme <see cref="File.ReadAllBytes(string)"/>.</summary>
    public abstract byte[] ReadAllBytes(string path);

    /// <summary>
    /// Lit un fichier sans gêner qui l'aurait ouvert, même en écriture : la lecture d'une analyse qui n'écrit rien.
    /// </summary>
    public virtual byte[] ReadShared(string path) => ReadAllBytes(path);

    /// <summary>Comme <see cref="File.ReadAllText(string)"/> : UTF-8, ou l'encodage qu'annonce une marque d'ordre des octets.</summary>
    public virtual string ReadAllText(string path)
    {
        using var reader = new StreamReader(new MemoryStream(ReadAllBytes(path)), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>Le contenu d'un fichier, ou rien s'il ne se lit pas.</summary>
    public byte[]? TryReadAllBytes(string path)
    {
        try
        {
            return ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>La taille d'un fichier, comme <see cref="FileInfo.Length"/>.</summary>
    public abstract long LengthOf(string path);

    /// <summary>Comme <see cref="File.GetLastWriteTimeUtc(string)"/> : le 1er janvier 1601 pour un fichier absent.</summary>
    public abstract DateTime LastWriteTimeUtc(string path);

    /// <summary>Crée le fichier de façon atomique, sans jamais écraser.</summary>
    /// <returns><see langword="false"/> si le fichier existe déjà ; rien n'est alors modifié.</returns>
    public abstract bool TryCreateNew(string path, ReadOnlySpan<byte> content);

    /// <summary>Remplace le fichier, ou le crée, de façon atomique : après une coupure, il est l'ancien ou le nouveau.</summary>
    public abstract void Replace(string path, ReadOnlySpan<byte> content);

    /// <summary>Comme <see cref="Directory.CreateDirectory(string)"/>.</summary>
    public abstract void CreateDirectory(string path);

    /// <summary>Comme <see cref="File.Move(string, string)"/> : jamais par-dessus un fichier existant.</summary>
    public abstract void MoveFile(string source, string destination);

    /// <summary>Comme <see cref="Directory.Move(string, string)"/>.</summary>
    public abstract void MoveDirectory(string source, string destination);

    /// <summary>
    /// Efface pour de bon un fichier, ou un dossier avec tout ce qu'il contient ; rien à faire s'il n'existe pas. Seuls
    /// l'effacement demandé depuis la corbeille et le dossier temporaire d'une conversion y recourent.
    /// </summary>
    public abstract void Purge(string path);

    /// <summary>
    /// Ce que contient un dossier, sans y descendre, comme <see cref="DirectoryInfo.EnumerateFileSystemInfos()"/> : fichiers
    /// et dossiers, cachés compris, dans l'ordre du disque.
    /// </summary>
    public abstract IReadOnlyList<StorageEntry> Entries(string folder);

    /// <summary>
    /// Les fichiers d'un dossier dont le nom suit <paramref name="pattern"/>, comme
    /// <see cref="Directory.EnumerateFiles(string, string)"/>.
    /// </summary>
    public abstract IReadOnlyList<string> Files(string folder, string pattern);

    /// <summary>
    /// Tout ce que contient un dossier, à toute profondeur, en un seul parcours : il n'entre pas dans les liens ni les
    /// jonctions, et passe ce qui ne se lit pas.
    /// </summary>
    public abstract IReadOnlyList<StorageEntry> AllEntries(string folder);

    /// <summary>Le chemin réel de la plus longue partie existante de <paramref name="path"/>, liens résolus.</summary>
    public abstract string RealPath(string path);

    /// <summary>Le chemin réel de <paramref name="relative"/> sous <paramref name="realBase"/>, déjà réel.</summary>
    public abstract string RealPathBelow(string realBase, string relative);

    /// <summary>Vrai si les deux chemins désignent le même élément existant ; sous Windows, la casse ne les distingue pas.</summary>
    public abstract bool SameEntry(string first, string second);

    /// <summary>
    /// Les fichiers du coffre qu'une sauvegarde contient, lus comme ce stockage les lit : ses entrées telles quelles pour le
    /// disque, les fichiers que nomme son index, déchiffrés, pour un coffre chiffré.
    /// </summary>
    /// <param name="entries">Les entrées de l'archive.</param>
    /// <param name="backup">Le chemin de l'archive, pour les messages.</param>
    public virtual IReadOnlyList<StoredFile> FilesOfBackup(IReadOnlyList<StoredFile> entries, string backup) => entries;

    /// <summary>
    /// Parmi les fichiers posés sur le disque (<see cref="Raw"/>), ceux qu'une sauvegarde recopie : tous pour le disque ;
    /// pour un coffre chiffré, tous sauf un objet que son index ne nomme plus.
    /// </summary>
    /// <param name="files">Les chemins complets des fichiers posés sur le disque.</param>
    public virtual IEnumerable<string> FilesToBackUp(IEnumerable<string> files) => files;

    /// <summary>
    /// Relit ce que le stockage garde en mémoire, une fois le verrou du coffre pris : un autre processus a pu écrire depuis
    /// l'ouverture du stockage. Rien à relire pour le disque ; l'index pour un coffre chiffré.
    /// </summary>
    /// <exception cref="IOException">Ce qu'il faut relire ne se lit plus ; le stockage reste tel qu'il était.</exception>
    public virtual void Reload()
    {
    }
}

/// <summary>Un élément d'un dossier : fichier ou dossier, son nom, sa taille, sa date.</summary>
public sealed class StorageEntry
{
    private readonly FileSystemInfo? _info;
    private Func<(long Length, DateTime LastWriteTimeUtc)>? _facts;
    private long _length;
    private DateTime _lastWriteTimeUtc;

    /// <summary>Un élément d'un stockage sans liens, dont la taille et la date ne se lisent qu'à la demande.</summary>
    /// <param name="fullName">Le chemin complet.</param>
    /// <param name="isDirectory">Un dossier plutôt qu'un fichier.</param>
    /// <param name="facts">Lit la taille et la date ; un dossier n'en a pas besoin.</param>
    public StorageEntry(string fullName, bool isDirectory, Func<(long Length, DateTime LastWriteTimeUtc)>? facts = null)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        FullName = fullName;
        Name = Path.GetFileName(fullName);
        IsDirectory = isDirectory;
        _facts = isDirectory ? null : facts;
    }

    /// <summary>Un élément relevé sur le disque en un parcours, sans relire ses attributs.</summary>
    public StorageEntry(string fullName, string name, bool isDirectory, long length, DateTime lastWriteTimeUtc, bool isReparsePoint)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        ArgumentNullException.ThrowIfNull(name);
        FullName = fullName;
        Name = name;
        IsDirectory = isDirectory;
        _length = length;
        _lastWriteTimeUtc = lastWriteTimeUtc;
        IsReparsePoint = isReparsePoint;
    }

    /// <summary>Un élément du disque ; la cible d'un lien ne se lit qu'à la demande.</summary>
    public StorageEntry(FileSystemInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        _info = info;
        FullName = info.FullName;
        Name = info.Name;
        IsDirectory = info is DirectoryInfo;
        _length = info is FileInfo file ? file.Length : 0;
        _lastWriteTimeUtc = info.LastWriteTimeUtc;
        IsReparsePoint = info.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    /// <summary>Le chemin complet.</summary>
    public string FullName { get; }

    /// <summary>Le nom, sans dossier.</summary>
    public string Name { get; }

    /// <summary>Un dossier plutôt qu'un fichier.</summary>
    public bool IsDirectory { get; }

    /// <summary>La taille d'un fichier ; 0 pour un dossier.</summary>
    public long Length
    {
        get
        {
            ReadFacts();
            return _length;
        }
    }

    /// <summary>La date de dernière écriture.</summary>
    public DateTime LastWriteTimeUtc
    {
        get
        {
            ReadFacts();
            return _lastWriteTimeUtc;
        }
    }

    /// <summary>Un point d'analyse du disque : lien, jonction, ou fichier en ligne d'un service de synchronisation.</summary>
    public bool IsReparsePoint { get; }

    /// <summary>Un lien symbolique ou une jonction, qui pourrait mener hors du coffre.</summary>
    public bool IsLink => _info?.LinkTarget is not null;

    private void ReadFacts()
    {
        if (_facts is { } facts)
        {
            (_length, _lastWriteTimeUtc) = facts();
            _facts = null;
        }
    }
}

/// <summary>Un fichier lu ailleurs que dans le coffre (une entrée de sauvegarde) : son chemin, sa taille, et de quoi le lire.</summary>
/// <param name="Path">Le chemin, séparé par <c>/</c>.</param>
/// <param name="Length">La taille annoncée.</param>
/// <param name="Read">Lit le contenu, vérifié.</param>
public sealed record StoredFile(string Path, long Length, Func<byte[]> Read);
