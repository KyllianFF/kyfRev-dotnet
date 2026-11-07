using System.IO.Compression;
using System.Security.Cryptography;
using KyfRev.Application.Ports;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Backups;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;

namespace KyfRev.Infrastructure.Vault;

public sealed partial class VaultSession
{
    private const long MaxRestoredFileBytes = 50L * 1024 * 1024;

    /// <summary>
    /// Sauvegarde tout le coffre (sauf le verrou et les temporaires) dans <paramref name="folder"/>,
    /// hors du coffre, sous le nom <c>coffre-AAAA-MM-JJTHH-MM-SS.zip</c>, puis relit l'archive et
    /// vérifie chaque fichier.
    /// </summary>
    /// <param name="folder">Dossier des sauvegardes.</param>
    /// <param name="timestamp">Heure locale inscrite dans le nom.</param>
    public BackupFile Backup(string folder, DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(folder);
        CreateDirectory(DiskStorage.Instance, folder);
        if (RealPaths.IsWithin(Io(folder, () => RealPaths.OfExistingPart(folder)), _realRoot))
        {
            throw VaultException.BackupInsideVault(folder);
        }

        var files = FilesToBackup();
        var temporary = Path.Combine(folder, $".sauvegarde-{RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyz0123456789", 6)}.tmp");
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using (var stream = Io(temporary, () => new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)))
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var file in files)
                    {
                        var bytes = ReadAllBytes(_storage.Raw, file.ToAbsolute(Root));
                        var entry = archive.CreateEntry(file.Value, CompressionLevel.Optimal);
                        using (var entryStream = entry.Open())
                        {
                            entryStream.Write(bytes);
                        }
                        expected[file.Value] = Fingerprint.Of(bytes);
                    }
                }
                stream.Flush(flushToDisk: true);
            }

            var name = VaultBackups.FileName(timestamp);
            var target = Path.Combine(folder, name);
            for (var number = 2; RealPaths.Exists(target); number++)
            {
                target = Path.Combine(folder, name.Replace(".zip", $"-{number}.zip", StringComparison.Ordinal));
            }
            Io(target, () =>
            {
                File.Move(temporary, target, overwrite: false);
                return true;
            });
            return new BackupFile(target, timestamp, Verify(target, expected));
        }
        finally
        {
            // Un temporaire qui n'a pas été mis en place ne doit pas rester.
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// Fiche manquante : son fichier est recopié depuis une sauvegarde, avec ses ressources, puis
    /// le coffre est réanalysé. Rien n'est écrasé : si un autre fichier occupe sa place, la copie
    /// reçoit le suffixe « (restaurée) ».
    /// </summary>
    /// <returns>Le chemin où la fiche a été remise.</returns>
    public VaultPath RestoreSheetFromBackup(string id, string backup, Moment now)
    {
        ArgumentNullException.ThrowIfNull(backup);
        var entry = Sheet(id);
        if (entry.Presence != FilePresence.Missing)
        {
            throw VaultException.SheetNotMissing(entry.Sheet.Title);
        }
        using var archive = VaultBackups.Open(backup);
        if (archive.Entries.Count > VaultBackups.MaxFiles)
        {
            throw VaultException.InvalidBackup(backup, "trop de fichiers");
        }
        var files = FilesOfBackup(archive, backup);
        (VaultPath Path, byte[] Bytes)? Read(StoredFile file)
        {
            if (file.Path.EndsWith('/') || file.Length > MaxRestoredFileBytes
                || !VaultPath.TryParse(file.Path, out var path, out _))
            {
                return null;
            }
            try
            {
                return (path, file.Read());
            }
            catch (Exception exception) when (IsIo(exception))
            {
                throw VaultException.InvalidBackup(backup, exception.Message);
            }
        }

        // 1. La fiche : un .md du dossier « fiches » qui porte cet identifiant.
        (VaultPath Path, byte[] Bytes)? found = null;
        foreach (var file in files)
        {
            if (Read(file) is not { } read || !read.Path.IsWithin(SheetsFolder) || !read.Path.Name.EndsWith(".md", StringComparison.Ordinal))
            {
                continue;
            }
            if (TryDecode(read.Bytes, out var text) && FrontMatter.Read(text) is { IsSuccess: true } reading && reading.Header.Id == id)
            {
                found = read;
                break;
            }
        }
        var (origin, bytes) = found ?? throw VaultException.NotInBackup(entry.Sheet.Title);

        // 2. Ses ressources, à leur place d'origine (les liens y mènent).
        var resources = FileNames.ResourcesFolder(origin);
        var toRestore = files.Select(Read).OfType<(VaultPath Path, byte[] Bytes)>().Where(r => r.Path.IsWithin(resources)).ToList();

        // 3. Écriture, sans jamais écraser.
        var stem = WithoutMarkdownExtension(origin.Name);
        var target = origin;
        for (var number = 1; _storage.Exists(Confine(target)); number++)
        {
            var suffix = number == 1 ? " (restaurée)" : $" (restaurée {number})";
            target = origin.Parent is { } parent ? parent.Join($"{stem}{suffix}.md") : VaultPath.Create($"{stem}{suffix}.md");
        }
        var absolute = Confine(target);
        CreateDirectory(Path.GetDirectoryName(absolute)!);
        CreateFile(absolute, bytes);
        foreach (var (path, content) in toRestore)
        {
            var destination = Confine(path);
            if (_storage.Exists(destination))
            {
                continue;
            }
            CreateDirectory(Path.GetDirectoryName(destination)!);
            CreateFile(destination, content);
        }
        Analyze(now);
        return target;
    }

    /// <summary>
    /// Tous les fichiers du coffre tels qu'ils sont posés sur le disque, sauf le verrou et les temporaires, triés : ceux
    /// d'un coffre chiffré le restent dans la sauvegarde. Le stockage écarte ce qui n'est plus du coffre : pour un coffre
    /// chiffré, un objet que son index ne nomme plus.
    /// </summary>
    private List<VaultPath> FilesToBackup()
    {
        var excluded = new HashSet<string>([VaultLayout.LockFile, VaultLayout.LockInfoFile], StringComparer.Ordinal);
        var found = new List<string>();
        var toVisit = new Stack<string>([Root]);
        while (toVisit.TryPop(out var current))
        {
            foreach (var entry in Io(current, () => _storage.Raw.Entries(current)))
            {
                if (entry.IsLink)
                {
                    continue;
                }
                if (entry.IsDirectory)
                {
                    toVisit.Push(entry.FullName);
                }
                else if (!entry.Name.StartsWith(AtomicFile.TemporaryPrefix, StringComparison.Ordinal))
                {
                    found.Add(entry.FullName);
                }
            }
        }
        var files = new List<VaultPath>();
        foreach (var file in _storage.FilesToBackUp(found))
        {
            if (!VaultPath.TryFromAbsolute(Root, file, out var path, out var reason))
            {
                throw VaultException.InvalidPath(Path.GetRelativePath(Root, file).Replace('\\', '/'), reason);
            }
            if (!excluded.Contains(path.Value))
            {
                files.Add(path);
            }
        }
        files.Sort();
        return files;
    }

    /// <summary>Relit l'archive : mêmes fichiers, même contenu. Rend le nombre de fichiers du coffre qu'elle contient.</summary>
    private int Verify(string backup, Dictionary<string, string> expected)
    {
        using var archive = VaultBackups.Open(backup);
        if (archive.Entries.Count != expected.Count)
        {
            throw VaultException.InvalidBackup(backup, "nombre de fichiers différent");
        }
        foreach (var entry in archive.Entries)
        {
            var bytes = VaultBackups.ReadExactly(entry, backup, entry.FullName);
            if (!expected.TryGetValue(entry.FullName, out var fingerprint) || fingerprint != Fingerprint.Of(bytes))
            {
                throw VaultException.InvalidBackup(backup, $"contenu différent pour « {entry.FullName} »");
            }
        }
        return FilesOfBackup(archive, backup).Count;
    }

    /// <summary>Les fichiers du coffre que contient une sauvegarde, lus comme ce coffre les lit : déchiffrés s'il est chiffré.</summary>
    private IReadOnlyList<StoredFile> FilesOfBackup(ZipArchive archive, string backup)
    {
        try
        {
            return _storage.FilesOfBackup(VaultBackups.Entries(archive, backup), backup);
        }
        catch (Exception exception) when (IsIo(exception))
        {
            throw VaultException.InvalidBackup(backup, exception.Message);
        }
    }
}
