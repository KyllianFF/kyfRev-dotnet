using System.IO.Enumeration;

namespace KyfRev.Infrastructure.Vault.Files;

/// <summary>Ce qui laisse un service de synchronisation (Proton Drive, OneDrive, iCloud…) envoyer les fichiers d'un coffre.</summary>
public static class SyncedFiles
{
    /// <summary>« Toujours conserver sur cet appareil » : l'épinglage d'un service de synchronisation, à garder tel quel.</summary>
    private const FileAttributes Pinned = (FileAttributes)0x80000;

    /// <summary>« Libérer de l'espace » : l'inverse de l'épinglage, à garder aussi.</summary>
    private const FileAttributes Unpinned = (FileAttributes)0x100000;

    /// <summary>
    /// Les attributs qu'un appel à <see cref="File.SetAttributes(string, FileAttributes)"/> pose : celui-ci les remplace tous,
    /// et les autres (point d'analyse, compression…) ne se posent pas ainsi.
    /// </summary>
    private const FileAttributes Settable = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive
        | FileAttributes.Offline | FileAttributes.NotContentIndexed | Pinned | Unpinned;

    /// <summary>
    /// Retire l'attribut « temporaire » de Windows des fichiers du coffre. Jusqu'au 15 novembre 2026, kyfRev le laissait à chaque
    /// fichier qu'il écrivait, et un service de synchronisation n'envoie pas un fichier qui le porte : un autre poste recevait le
    /// coffre sans son <c>.suivi/coffre.json</c>. Le contenu et la date des fichiers ne changent pas ; les temporaires d'une
    /// écriture en cours gardent le leur, et le parcours n'entre pas dans les liens.
    /// </summary>
    /// <param name="root">Le dossier du coffre, sur le disque.</param>
    /// <returns>Les fichiers libérés, et ceux qui gardent l'attribut, faute de pouvoir le changer : la prochaine ouverture réessaie.</returns>
    public static (int Released, int Refused) Release(string root)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true };
        var marked = new FileSystemEnumerable<(string Path, FileAttributes Attributes)>(
            root,
            (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.Attributes),
            options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory
                && (entry.Attributes & FileAttributes.Temporary) != 0
                && !entry.FileName.StartsWith(AtomicFile.TemporaryPrefix, StringComparison.Ordinal),
            ShouldRecursePredicate = (ref FileSystemEntry entry) => !DiskStorage.IsLink(ref entry),
        };
        var (released, refused) = (0, 0);
        foreach (var (path, attributes) in marked.ToList())
        {
            try
            {
                var kept = attributes & Settable & ~FileAttributes.Temporary;
                File.SetAttributes(path, kept == 0 ? FileAttributes.Normal : kept);
                released++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                refused++;
            }
        }
        return (released, refused);
    }
}
