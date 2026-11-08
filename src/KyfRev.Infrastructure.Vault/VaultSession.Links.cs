using System.Collections.Immutable;
using KyfRev.Application.Ports;
using KyfRev.Application.Text;
using KyfRev.Infrastructure.Vault.Links;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault;

/// <summary>
/// Liens cassés des fiches, sans relire tout le coffre à chaque fois : les liens d'une fiche sont
/// relevés quand elle est lue (à l'analyse, ou ici), avec la date et la taille du fichier lu ; une
/// fiche dont le fichier n'a pas changé n'est pas relue. L'existence des cibles est vérifiée dans
/// un relevé du coffre fait en un seul parcours. Ce qui passe par un lien symbolique ou une
/// jonction est vérifié comme avant, chemin par chemin.
/// </summary>
public sealed partial class VaultSession
{
    private readonly Dictionary<string, SheetLinks> _links = new(StringComparer.Ordinal);

    /// <summary>
    /// Liens et images des fiches présentes qui mènent à un fichier absent du coffre, triés par
    /// titre puis destination. Les adresses externes ne sont pas vérifiées.
    /// </summary>
    public IReadOnlyList<BrokenLink> BrokenLinks()
    {
        var snapshot = VaultSnapshot.Of(_storage, _realRoot);
        var broken = new List<BrokenLink>();
        foreach (var (id, entry) in _sheets)
        {
            if (entry.Presence != FilePresence.Present || TargetsOf(id, entry.Sheet.Path, snapshot) is not { } targets)
            {
                continue;
            }
            var folder = entry.Sheet.Path.Parent?.Value ?? string.Empty;
            foreach (var target in targets.Where(target => !Exists(folder, target, snapshot)))
            {
                broken.Add(new BrokenLink(id, entry.Sheet.Title, target));
            }
        }
        // Tri stable, comme celui de la 0.x.
        return [.. broken.OrderBy(link => link.Title, Utf8Order.Instance).ThenBy(link => link.Target, Utf8Order.Instance)];
    }

    /// <summary>Relève les liens d'une fiche que l'analyse vient de lire.</summary>
    private void RememberLinks(VaultPath path, string text, DateTime modified, long length) =>
        _links[path.Value] = new SheetLinks(modified, length, LinkTargets(text));

    /// <summary>Destinations locales des liens et images d'un texte, décodées, sans ancre ni paramètres.</summary>
    private static ImmutableArray<string> LinkTargets(string text) =>
    [
        .. MarkdownLinks.Find(text)
            .Select(link => LocalTarget(text[link.Start..link.End]))
            .OfType<string>(),
    ];

    /// <summary>La destination locale d'un lien, décodée, sans ancre ni paramètres ; rien pour une adresse externe.</summary>
    private static string? LocalTarget(string raw)
    {
        var path = raw.IndexOfAny(['#', '?']) is var cut and >= 0 ? raw[..cut] : raw;
        return MarkdownLinks.IsExternal(path) ? null : MarkdownLinks.Decode(path);
    }

    /// <summary>Destinations des liens de la fiche ; relue seulement si son fichier a changé. Rien si elle est illisible.</summary>
    private ImmutableArray<string>? TargetsOf(string id, VaultPath path, VaultSnapshot snapshot)
    {
        var file = snapshot.Exists(path.Value) == true ? snapshot.FileOf(path.Value) : ((DateTime Modified, long Length)?)null;
        if (file is { } known && _links.TryGetValue(path.Value, out var links) && links.Modified == known.Modified && links.Length == known.Length)
        {
            return links.Targets;
        }
        if (TextOf(id) is not { } text)
        {
            return null;
        }
        var targets = LinkTargets(text);
        if (file is { } read)
        {
            // Date et taille relevées avant la lecture : un changement entre-temps fera relire la fiche.
            _links[path.Value] = new SheetLinks(read.Modified, read.Length, targets);
        }
        return targets;
    }

    /// <summary>La destination mène à un fichier ou un dossier du coffre.</summary>
    private bool Exists(string folder, string target, VaultSnapshot snapshot)
    {
        if (MarkdownLinks.Resolve(folder, target) is not { } resolved || !VaultPath.TryParse(resolved, out var path, out _))
        {
            return false;
        }
        return snapshot.Exists(path.Value) ?? LeadsToAFile(folder, target);
    }

    /// <summary>Liens d'une fiche, relevés sur le fichier de cette date et de cette taille.</summary>
    private sealed record SheetLinks(DateTime Modified, long Length, ImmutableArray<string> Targets);

    /// <summary>
    /// Relevé du coffre en un parcours : chaque fichier et dossier, ses attributs, sa date et sa
    /// taille. Le parcours n'entre pas dans les liens symboliques ni les jonctions.
    /// </summary>
    private sealed class VaultSnapshot
    {
        private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        private readonly Dictionary<string, (bool IsReparsePoint, DateTime Modified, long Length)> _entries;

        private VaultSnapshot(Dictionary<string, (bool IsReparsePoint, DateTime Modified, long Length)> entries)
        {
            _entries = entries;
        }

        public static VaultSnapshot Of(VaultStorage storage, string realRoot)
        {
            var start = Path.TrimEndingDirectorySeparator(realRoot).Length + 1;
            var found = new Dictionary<string, (bool, DateTime, long)>(PathComparer);
            foreach (var entry in storage.AllEntries(realRoot))
            {
                found[entry.FullName[start..].Replace(Path.DirectorySeparatorChar, '/')] = (entry.IsReparsePoint, entry.LastWriteTimeUtc, entry.Length);
            }
            return new VaultSnapshot(found);
        }

        /// <summary>Date et taille d'un fichier relevé.</summary>
        public (DateTime Modified, long Length) FileOf(string relative)
        {
            var entry = _entries[relative];
            return (entry.Modified, entry.Length);
        }

        /// <summary>
        /// Vrai si l'élément existe, faux s'il manque ; rien si son chemin passe par un lien
        /// symbolique ou une jonction (la vérification se fait alors chemin par chemin).
        /// </summary>
        public bool? Exists(string relative)
        {
            for (var end = relative.IndexOf('/', StringComparison.Ordinal); ; end = relative.IndexOf('/', end + 1))
            {
                if (!_entries.TryGetValue(end < 0 ? relative : relative[..end], out var entry))
                {
                    return false;
                }
                if (entry.IsReparsePoint)
                {
                    return null;
                }
                if (end < 0)
                {
                    return true;
                }
            }
        }
    }
}
