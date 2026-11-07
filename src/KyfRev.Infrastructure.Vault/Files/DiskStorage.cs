using System.IO.Enumeration;

namespace KyfRev.Infrastructure.Vault.Files;

/// <summary>Le disque tel quel : chaque méthode est celle de <c>System.IO</c>, d'<see cref="AtomicFile"/> ou de <see cref="RealPaths"/>.</summary>
internal sealed class DiskStorage : VaultStorage
{
    public override bool FileExists(string path) => File.Exists(path);

    public override bool DirectoryExists(string path) => Directory.Exists(path);

    public override byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public override byte[] ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buffer = new byte[stream.Length];
        stream.ReadExactly(buffer);
        return buffer;
    }

    public override string ReadAllText(string path) => File.ReadAllText(path);

    public override long LengthOf(string path) => new FileInfo(path).Length;

    public override DateTime LastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    public override bool TryCreateNew(string path, ReadOnlySpan<byte> content) => AtomicFile.TryCreateNew(path, content);

    public override void Replace(string path, ReadOnlySpan<byte> content) => AtomicFile.Replace(path, content);

    public override void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public override void MoveFile(string source, string destination) => File.Move(source, destination);

    public override void MoveDirectory(string source, string destination) => Directory.Move(source, destination);

    public override void DeleteFile(string path) => File.Delete(path);

    public override void DeleteDirectory(string path) => Directory.Delete(path, recursive: true);

    public override IReadOnlyList<StorageEntry> Entries(string folder) =>
        [.. new DirectoryInfo(folder).EnumerateFileSystemInfos().Select(info => new StorageEntry(info))];

    public override IReadOnlyList<string> Files(string folder, string pattern) => [.. Directory.EnumerateFiles(folder, pattern)];

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

    public override string RealPath(string path) => RealPaths.OfExistingPart(path);

    public override string RealPathBelow(string realBase, string relative) => RealPaths.OfExistingPartBelow(realBase, relative);

    public override bool SameEntry(string first, string second) => RealPaths.SameEntry(first, second);
}
