using System.Text;

namespace KyfRev.Acceptance.Tests;

/// <summary>Ce qu'a vu un passage de <see cref="DiskScan"/>, en chemins relatifs au dossier parcouru.</summary>
/// <param name="Files">Tout ce qui a été parcouru.</param>
/// <param name="Skipped">Les fichiers qui n'ont pas pu être lus : tenus par un autre programme.</param>
/// <param name="Findings">Où un marqueur se lit, et sous quelle forme.</param>
internal sealed record ScanResult(IReadOnlyList<string> Files, IReadOnlyList<string> Skipped, IReadOnlyList<string> Findings);

/// <summary>
/// Cherche des marqueurs dans les octets bruts d'un dossier et dans ses noms de fichiers. Un marqueur est un mot ASCII sans
/// séparateur : pris dans du JSON échappé, une adresse encodée ou un texte aux accents décomposés, il reste entier. Ce qui
/// est compressé ou chiffré ne se lit pas ici : c'est une limite de la preuve, dite dans <c>docs/chiffrement.md</c>.
/// </summary>
internal sealed class DiskScan
{
    private readonly IReadOnlyList<string> _markers;

    private readonly List<(string Marker, string Form, byte[] Bytes, bool AnyCase)> _needles;

    public DiskScan(IEnumerable<string> markers)
    {
        _markers = [.. markers];
        _needles = [.. _markers.SelectMany(marker => FormsOf(marker).Select(form => (marker, form.Name, form.Bytes, form.AnyCase)))];
    }

    /// <summary>Parcourt <paramref name="folder"/> ; un fichier tenu est noté, pas une erreur.</summary>
    public ScanResult Of(string folder)
    {
        var files = new List<string>();
        var skipped = new List<string>();
        var findings = new List<string>();
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        foreach (var entry in Directory.EnumerateFileSystemEntries(folder, "*", options))
        {
            var relative = Path.GetRelativePath(folder, entry);
            findings.AddRange(_markers.Where(marker => relative.Contains(marker, StringComparison.OrdinalIgnoreCase)).Select(marker => $"{relative} : « {marker} » dans le nom"));
            if (!File.Exists(entry))
            {
                continue;
            }
            files.Add(relative);
            try
            {
                findings.AddRange(FindingsIn(ReadShared(entry)).Select(found => $"{relative} : {found}"));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                skipped.Add(relative);
            }
        }
        return new ScanResult(files, skipped, findings);
    }

    /// <summary>Vrai si plus aucun programme ne tient un fichier de <paramref name="folder"/> ouvert, en lecture comme en écriture.</summary>
    public static bool IsReleased(string folder)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false }))
            {
                using var alone = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Le marqueur tel que l'écriraient l'UTF-8, l'UTF-16 et leur base64, à chacun des trois alignements.</summary>
    private static IEnumerable<(string Name, byte[] Bytes, bool AnyCase)> FormsOf(string marker)
    {
        foreach (var (name, encoding) in new (string, Encoding)[] { ("UTF-8", Encoding.UTF8), ("UTF-16LE", Encoding.Unicode), ("UTF-16BE", Encoding.BigEndianUnicode) })
        {
            var bytes = encoding.GetBytes(marker);
            yield return (name, bytes, true);
            for (var shift = 0; shift < 3; shift++)
            {
                yield return ($"{name} en base64, décalé de {shift}", Encoding.ASCII.GetBytes(Base64Core(bytes, shift)), false);
            }
        }
    }

    /// <summary>Les caractères du base64 qui ne dépendent que de <paramref name="bytes"/> : ni de ce qui précède, ni du remplissage.</summary>
    private static string Base64Core(byte[] bytes, int shift)
    {
        var text = Convert.ToBase64String([.. new byte[shift], .. bytes]);
        var start = shift == 0 ? 0 : shift + 1;
        return text[start..(text.Length - ((shift + bytes.Length) % 3 == 0 ? 0 : 4))];
    }

    private static byte[] ReadShared(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static byte[] LowerAscii(byte[] bytes)
    {
        var lower = (byte[])bytes.Clone();
        for (var at = 0; at < lower.Length; at++)
        {
            if (lower[at] is >= (byte)'A' and <= (byte)'Z')
            {
                lower[at] |= 0x20;
            }
        }
        return lower;
    }

    private List<string> FindingsIn(byte[] content)
    {
        var lowered = LowerAscii(content);
        return
        [
            .. _needles
                .Where(needle => needle.AnyCase ? lowered.AsSpan().IndexOf(LowerAscii(needle.Bytes)) >= 0 : content.AsSpan().IndexOf(needle.Bytes) >= 0)
                .Select(needle => $"« {needle.Marker} » en {needle.Form}"),
        ];
    }
}
