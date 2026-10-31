namespace KyfRev.Infrastructure.Vault.Files;

/// <summary>
/// Chemins réels : liens symboliques et jonctions résolus, composant par composant. Sert à
/// garantir qu'un chemin du coffre n'en sort pas, même par un lien (comme <c>canonicalize</c> en
/// 0.x). Les autres points d'analyse (fichiers OneDrive à la demande) ne sont pas des liens.
/// </summary>
internal static class RealPaths
{
    private const int MaxLinks = 40;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Chemin réel de la plus longue partie existante de <paramref name="path"/> (le reste,
    /// inexistant, n'a pas de lien à résoudre).
    /// </summary>
    public static string OfExistingPart(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? full;
        var current = root;
        var links = 0;
        foreach (var component in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Combine(current, component);
            FileSystemInfo info = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);
            if (!info.Exists)
            {
                break;
            }
            if (info.LinkTarget is not null)
            {
                if (++links > MaxLinks)
                {
                    throw new IOException($"Trop de liens symboliques dans « {path} ».");
                }
                next = OfExistingPart(info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? next);
            }
            current = next;
        }
        return current;
    }

    /// <summary>Vrai si <paramref name="path"/> est <paramref name="folder"/> ou se trouve dessous.</summary>
    public static bool IsWithin(string path, string folder)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(folder);
        return string.Equals(path, trimmed, PathComparison)
            || (path.StartsWith(trimmed, PathComparison) && path.Length > trimmed.Length && path[trimmed.Length] == Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Vrai si <paramref name="first"/> et <paramref name="second"/> désignent le même élément
    /// existant ; sous Windows, la casse ne les distingue pas.
    /// </summary>
    public static bool SameEntry(string first, string second) =>
        Exists(first) && Exists(second) && string.Equals(OfExistingPart(first), OfExistingPart(second), PathComparison);

    /// <summary>Fichier ou dossier existant (un lien cassé n'existe pas).</summary>
    public static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
