using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// Les fichiers de la page de l'éditeur, posés sur le disque, un dossier par version : le moteur les lit lui-même. Les lui
/// servir depuis .NET le ferait entrer dans du code .NET sur ses propres fils, et la fin de l'un d'eux ferait planter le
/// processus.
/// </summary>
internal static partial class EditorPage
{
    private const string ResourcePrefix = "kyfrev-web/";

    private static readonly Lazy<(string Version, IReadOnlyList<(string Name, byte[] Content)> Files)> Contents = new(Read);

    /// <summary>Pose les fichiers de cette version, sauf ceux qui y sont déjà, intacts, et retire les autres versions.</summary>
    /// <param name="dataFolder">Le dossier des données du moteur.</param>
    /// <returns>Le dossier que le moteur sert, et le chemin de cette version dedans.</returns>
    public static (string Folder, string Version) Prepare(string dataFolder)
    {
        var (version, files) = Contents.Value;
        var root = Path.Combine(dataFolder, "page");
        foreach (var (name, content) in files)
        {
            var path = Path.Combine(root, version, name);
            if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(content))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, content);
            }
        }
        foreach (var older in Directory.EnumerateDirectories(root).Where(folder => Path.GetFileName(folder) != version))
        {
            TryDelete(older);
        }
        return (root, version);
    }

    /// <summary>Une ancienne version encore ouverte par un autre programme partira la prochaine fois.</summary>
    private static bool TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static (string Version, IReadOnlyList<(string Name, byte[] Content)> Files) Read()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var files = new List<(string Name, byte[] Content)>();
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            files.Add((resource[ResourcePrefix.Length..].Replace('\\', '/'), Bytes(stream)));
        }
        var fonts = Encoding.UTF8.GetString(files.Single(file => file.Name == "polices.css").Content);
        foreach (var font in FontName().Matches(fonts).Select(match => match.Groups[1].Value))
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri($"/KyfRev.DesignSystem;component/Fonts/{font}", UriKind.Relative))!;
            files.Add(($"polices/{font}", Bytes(resource.Stream)));
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (name, content) in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(name));
            hash.AppendData(content);
        }
        return (Convert.ToHexStringLower(hash.GetHashAndReset())[..16], files);
    }

    private static byte[] Bytes(Stream stream)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    [GeneratedRegex(@"url\(""polices/([^""]+)""\)")]
    private static partial Regex FontName();
}
