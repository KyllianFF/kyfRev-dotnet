using System.IO.Enumeration;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Vault.Files;

/// <summary>
/// Le disque tel quel : les fichiers d'un coffre en clair. Chaque méthode est celle de <c>System.IO</c>, d'<see cref="AtomicFile"/>
/// ou de <see cref="RealPaths"/>.
/// </summary>
public sealed class DiskStorage : VaultStorage
{
    private DiskStorage()
    {
    }

    /// <summary>Le disque.</summary>
    public static DiskStorage Instance { get; } = new();

    /// <inheritdoc/>
    public override bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public override bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc/>
    public override byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    /// <summary>Lit le fichier en partage complet : une autre application peut l'avoir ouvert, même en écriture.</summary>
    public override byte[] ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buffer = new byte[stream.Length];
        stream.ReadExactly(buffer);
        return buffer;
    }

    /// <inheritdoc/>
    public override string ReadAllText(string path) => File.ReadAllText(path);

    /// <inheritdoc/>
    public override long LengthOf(string path) => new FileInfo(path).Length;

    /// <inheritdoc/>
    public override DateTime LastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    /// <inheritdoc/>
    public override bool TryCreateNew(string path, ReadOnlySpan<byte> content) => AtomicFile.TryCreateNew(path, content);

    /// <inheritdoc/>
    public override void Replace(string path, ReadOnlySpan<byte> content) => AtomicFile.Replace(path, content);

    /// <inheritdoc/>
    public override void CreateDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc/>
    public override void MoveFile(string source, string destination) => File.Move(source, destination);

    /// <inheritdoc/>
    public override void MoveDirectory(string source, string destination) => Directory.Move(source, destination);

    /// <summary>Le seul point du stockage sur le disque qui efface : un dossier avec son contenu, ou un fichier.</summary>
    public override void Purge(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <inheritdoc/>
    public override IReadOnlyList<StorageEntry> Entries(string folder) =>
        [.. new DirectoryInfo(folder).EnumerateFileSystemInfos().Select(info => new StorageEntry(info))];

    /// <inheritdoc/>
    public override IReadOnlyList<string> Files(string folder, string pattern) => [.. Directory.EnumerateFiles(folder, pattern)];

    /// <inheritdoc/>
    public override IReadOnlyList<StorageEntry> AllEntries(string folder)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true };
        var entries = new FileSystemEnumerable<StorageEntry>(
            folder,
            (ref FileSystemEntry entry) => new StorageEntry(
                entry.ToFullPath(),
                entry.FileName.ToString(),
                entry.IsDirectory,
                entry.Length,
                entry.LastWriteTimeUtc.UtcDateTime,
                (entry.Attributes & FileAttributes.ReparsePoint) != 0),
            options)
        {
            ShouldRecursePredicate = (ref FileSystemEntry entry) => (entry.Attributes & FileAttributes.ReparsePoint) == 0,
        };
        return [.. entries];
    }

    /// <inheritdoc/>
    public override string RealPath(string path) => RealPaths.OfExistingPart(path);

    /// <inheritdoc/>
    public override string RealPathBelow(string realBase, string relative) => RealPaths.OfExistingPartBelow(realBase, relative);

    /// <inheritdoc/>
    public override bool SameEntry(string first, string second) => RealPaths.SameEntry(first, second);
}
