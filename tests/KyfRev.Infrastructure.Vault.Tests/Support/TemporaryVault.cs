using System.Security.Cryptography;

namespace KyfRev.Infrastructure.Vault.Tests.Support;

/// <summary>Copie jetable du coffre de référence (ou dossier vide), effacée à la fin du test.</summary>
internal sealed class TemporaryVault : IDisposable
{
    private TemporaryVault(string root)
    {
        Root = root;
    }

    /// <summary>Coffre de référence produit par la version 0.x (voir tools/reference-vectors).</summary>
    public static string Reference => Path.Combine(AppContext.BaseDirectory, "fixtures", "coffre-reference");

    public string Root { get; }

    public static TemporaryVault CopyOfReference()
    {
        var vault = Empty();
        foreach (var file in Directory.EnumerateFiles(Reference, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(vault.Root, Path.GetRelativePath(Reference, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        return vault;
    }

    public static TemporaryVault Empty()
    {
        var root = Path.Combine(Path.GetTempPath(), "kyfrev-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TemporaryVault(root);
    }

    public string PathOf(string relative) => Path.Combine([Root, .. relative.Split('/')]);

    public void Write(string relative, string content)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>Empreinte, taille et date de chaque fichier : de quoi prouver que rien n'a été écrit.</summary>
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
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
