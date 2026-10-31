using System.Globalization;
using System.IO.Compression;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault.Backups;

/// <summary>
/// Sauvegardes <c>.zip</c> du coffre, rotation et restauration, comme dans la version 0.x.
/// La rotation est la seule suppression automatique : elle ne vise que des copies
/// <c>coffre-AAAA-MM-JJTHH-MM-SS.zip</c> d'un dossier situé hors de tout coffre. Une restauration
/// se fait toujours dans un nouveau dossier : le coffre existant n'est jamais écrasé.
/// </summary>
public static class VaultBackups
{
    /// <summary>Limites de restauration contre une archive piégée.</summary>
    internal const int MaxFiles = 200_000;

    internal const long MaxTotalBytes = 20L * 1024 * 1024 * 1024;

    private static readonly uint[] CrcTable = [.. Enumerable.Range(0, 256).Select(n =>
    {
        var crc = (uint)n;
        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return crc;
    })];

    /// <summary><c>coffre-2026-10-09T18-30-05.zip</c>.</summary>
    public static string FileName(DateTime timestamp) =>
        $"coffre-{timestamp.ToString("yyyy-MM-dd'T'HH-mm-ss", CultureInfo.InvariantCulture)}.zip";

    /// <summary>Horodatage lu dans un nom de sauvegarde ; rien si le nom ne suit pas exactement le modèle (un tel fichier n'est jamais touché).</summary>
    public static DateTime? TimestampOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!name.StartsWith("coffre-", StringComparison.Ordinal) || !name.EndsWith(".zip", StringComparison.Ordinal) || name.Length < 11)
        {
            return null;
        }
        var core = name[7..^4];
        if (core.Length < 19 || !core.All(char.IsAscii))
        {
            return null;
        }
        var (stamp, suffix) = (core[..19], core[19..]);
        if (suffix.Length > 0 && !(suffix[0] == '-' && suffix.Length > 1 && suffix[1..].All(char.IsAsciiDigit)))
        {
            return null;
        }
        var t = stamp.IndexOf('T', StringComparison.Ordinal);
        if (t < 0)
        {
            return null;
        }
        var (date, time) = (stamp[..t], stamp[(t + 1)..]);
        var parts = time.Split('-');
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            || parts.Length != 3 || time.Length != 8
            || !TryPart(parts[0], 24, out var hour) || !TryPart(parts[1], 60, out var minute) || !TryPart(parts[2], 60, out var second))
        {
            return null;
        }
        return day.ToDateTime(new TimeOnly(hour, minute, second));

        static bool TryPart(string text, int limit, out int value) =>
            int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) && value >= 0 && value < limit;
    }

    /// <summary>Sauvegardes reconnues dans <paramref name="folder"/>, de la plus récente à la plus ancienne.</summary>
    public static IReadOnlyList<(DateTime Timestamp, string Path)> List(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (!RealPaths.Exists(folder))
        {
            return [];
        }
        var backups = new List<(DateTime, string)>();
        foreach (var file in VaultSession.Io(folder, () => new DirectoryInfo(folder).EnumerateFiles().ToList()))
        {
            if (file.LinkTarget is null && TimestampOf(file.Name) is { } timestamp)
            {
                backups.Add((timestamp, file.FullName));
            }
        }
        return [.. backups.OrderByDescending(b => b.Item1).ThenByDescending(b => b.Item2, StringComparer.Ordinal)];
    }

    /// <summary>Vrai si aucune sauvegarde n'a encore été faite ce jour-là.</summary>
    public static bool DailyBackupNeeded(string folder, DateOnly day) =>
        !List(folder).Any(backup => DateOnly.FromDateTime(backup.Timestamp) == day);

    /// <summary>
    /// Sauvegardes à conserver : la plus récente de chacun des derniers jours, des dernières
    /// semaines (ISO) et des derniers mois, et toujours la toute dernière.
    /// </summary>
    public static IReadOnlySet<string> ToKeep(IEnumerable<(DateTime Timestamp, string Path)> backups, RotationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(policy);
        var sorted = backups.OrderByDescending(b => b.Timestamp).ThenByDescending(b => b.Path, StringComparer.Ordinal).ToList();
        var kept = new HashSet<string>(StringComparer.Ordinal);
        if (sorted.Count > 0)
        {
            kept.Add(sorted[0].Path);
        }
        void Keep(Func<DateTime, (int, int, int)> key, int count)
        {
            var seen = new HashSet<(int, int, int)>();
            foreach (var (timestamp, path) in sorted)
            {
                if (seen.Count == count && !seen.Contains(key(timestamp)))
                {
                    break;
                }
                if (seen.Add(key(timestamp)))
                {
                    kept.Add(path);
                }
            }
        }
        Keep(t => (t.Year, t.Month, t.Day), policy.Days);
        Keep(t => (ISOWeek.GetYear(t), ISOWeek.GetWeekOfYear(t), 0), policy.Weeks);
        Keep(t => (t.Year, t.Month, 0), policy.Months);
        return kept;
    }

    /// <summary>
    /// Applique la rotation dans <paramref name="folder"/> et rend les copies retirées. Le dossier ne
    /// doit pas être un coffre, et seuls les fichiers nommés exactement comme des sauvegardes sont concernés.
    /// </summary>
    public static IReadOnlyList<string> ApplyRotation(string folder, RotationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (RealPaths.Exists(Path.Combine(folder, VaultLayout.Tracking)))
        {
            throw VaultException.BackupInsideVault(folder);
        }
        var backups = List(folder);
        var kept = ToKeep(backups, policy);
        var removed = new List<string>();
        foreach (var (_, path) in backups)
        {
            if (!kept.Contains(path))
            {
                RemoveOutdatedCopy(folder, path);
                removed.Add(path);
            }
        }
        return removed;
    }

    /// <summary>
    /// Restaure une sauvegarde dans un nouveau dossier « Coffre (restauré le AAAA-MM-JJ) » créé dans
    /// <paramref name="parentFolder"/>. Tous les noms sont vérifiés avant la moindre écriture.
    /// </summary>
    /// <returns>Le dossier du coffre restauré.</returns>
    public static string Restore(string backup, string parentFolder, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(parentFolder);
        using var archive = Open(backup);
        if (archive.Entries.Count > MaxFiles)
        {
            throw VaultException.InvalidBackup(backup, "trop de fichiers");
        }
        var entries = new List<(ZipArchiveEntry Entry, VaultPath Path)>();
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }
            if (!VaultPath.TryParse(entry.FullName, out var path, out _))
            {
                throw VaultException.InvalidBackup(backup, $"chemin refusé « {entry.FullName} »");
            }
            total = total > long.MaxValue - entry.Length ? long.MaxValue : total + entry.Length;
            entries.Add((entry, path));
        }
        if (total > MaxTotalBytes)
        {
            throw VaultException.InvalidBackup(backup, "contenu décompressé trop volumineux");
        }
        if (!entries.Any(e => e.Path.Value == VaultLayout.SettingsFile))
        {
            throw VaultException.InvalidBackup(backup, "ce n'est pas une sauvegarde de coffre (coffre.json absent)");
        }

        VaultSession.CreateDirectory(parentFolder);
        var name = $"Coffre (restauré le {day:yyyy-MM-dd})";
        var target = Path.Combine(parentFolder, name);
        for (var number = 2; RealPaths.Exists(target); number++)
        {
            target = Path.Combine(parentFolder, $"{name} ({number})");
        }
        VaultSession.CreateDirectory(target);
        foreach (var (entry, path) in entries)
        {
            var bytes = ReadExactly(entry, backup, path.Value);
            var destination = path.ToAbsolute(target);
            VaultSession.CreateDirectory(Path.GetDirectoryName(destination)!);
            VaultSession.CreateFile(destination, bytes);
        }
        return target;
    }

    internal static ZipArchive Open(string backup)
    {
        var stream = VaultSession.Io(backup, () => File.OpenRead(backup));
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch (InvalidDataException exception)
        {
            stream.Dispose();
            throw VaultException.InvalidBackup(backup, exception.Message);
        }
    }

    /// <summary>
    /// Contenu d'une entrée, lu dans la limite de sa taille annoncée : une entrée mensongère est
    /// refusée. .NET cesse de décompresser à la taille annoncée sans rien signaler : le CRC-32 de
    /// l'archive est donc vérifié, sans quoi une entrée plus longue serait tronquée en silence.
    /// </summary>
    internal static byte[] ReadExactly(ZipArchiveEntry entry, string backup, string name)
    {
        var bytes = ReadAnnounced(entry, backup, name);
        return Crc32(bytes) == entry.Crc32 ? bytes : throw VaultException.InvalidBackup(backup, $"contenu altéré pour « {name} »");
    }

    /// <summary>CRC-32 (ISO-HDLC), celui que l'archive annonce pour chaque entrée.</summary>
    internal static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return ~crc;
    }

    private static byte[] ReadAnnounced(ZipArchiveEntry entry, string backup, string name)
    {
        try
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > entry.Length)
                {
                    break;
                }
            }
            return buffer.Length == entry.Length ? buffer.ToArray() : throw VaultException.InvalidBackup(backup, $"taille incohérente pour « {name} »");
        }
        catch (InvalidDataException exception)
        {
            throw VaultException.InvalidBackup(backup, exception.Message);
        }
    }

    /// <summary>Seul point du code qui efface un fichier : une copie de sauvegarde périmée, dans son dossier.</summary>
    private static void RemoveOutdatedCopy(string folder, string path)
    {
        var name = Path.GetFileName(path);
        if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)), StringComparison.OrdinalIgnoreCase)
            || TimestampOf(name) is null)
        {
            throw VaultException.InvalidBackup(path, "ce fichier n'est pas une copie de sauvegarde");
        }
        VaultSession.Io(path, () =>
        {
            File.Delete(path);
            return true;
        });
    }
}
