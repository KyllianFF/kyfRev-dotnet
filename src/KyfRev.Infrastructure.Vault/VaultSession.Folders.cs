using System.Text;
using KyfRev.Application.Ports;
using KyfRev.Application.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Links;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault;

public sealed partial class VaultSession
{
    /// <summary>
    /// Dossiers de <c>fiches/</c> (relatifs à lui), triés : ceux qui existent sur le disque, vides
    /// compris, et ceux des fiches suivies. Ni la corbeille ni les dossiers de ressources n'en font partie.
    /// </summary>
    public IReadOnlyList<string> Folders()
    {
        var folders = _sheets.Values
            .Select(entry => DisplayFolder(entry.Sheet.Path))
            .Where(folder => folder.Length > 0)
            .SelectMany(folder =>
            {
                var parts = folder.Split('/');
                return Enumerable.Range(1, parts.Length).Select(n => string.Join('/', parts[..n]));
            })
            .Concat(FoldersOnDisk())
            .OrderBy(RustText.ToLowercase, Utf8Order.Instance)
            .ToList();
        // Seuls les doublons voisins sont retirés, comme en 0.x (Vec::dedup).
        return [.. folders.Where((folder, i) => i == 0 || folders[i - 1] != folder)];
    }

    /// <summary>
    /// Crée le dossier <paramref name="folder"/> (relatif à <c>fiches/</c> ; « A/B » crée les deux
    /// niveaux). Un dossier qui existe déjà est refusé.
    /// </summary>
    /// <returns>Son chemin, relatif à <c>fiches/</c>.</returns>
    public string CreateFolder(string folder)
    {
        var path = FolderPath(folder);
        var absolute = Confine(path);
        if (_storage.Exists(absolute))
        {
            throw VaultException.AlreadyExists(absolute);
        }
        CreateDirectory(absolute);
        return RelativeToSheets(path);
    }

    /// <summary>
    /// Déplace ou renomme le dossier <paramref name="folder"/>, qui devient <paramref name="to"/>
    /// (relatifs à <c>fiches/</c>), avec tout ce qu'il contient ; les liens qui visent son contenu,
    /// et ceux de ses fiches vers l'extérieur, sont réécrits. Refusé si la destination existe
    /// déjà (sauf simple changement de casse) ou se trouve dans le dossier lui-même.
    /// </summary>
    /// <returns>Le nouveau chemin, relatif à <c>fiches/</c>.</returns>
    public string MoveFolder(string folder, string to, Moment now)
    {
        var from = FolderPath(folder);
        var target = FolderPath(to);
        var absolute = Confine(from);
        if (!_storage.DirectoryExists(absolute))
        {
            throw VaultException.UnknownFolder(RelativeToSheets(from));
        }
        if (target == from)
        {
            return RelativeToSheets(target);
        }
        // Windows ne distingue pas la casse : « bio/Cellule » est dans « Bio ».
        var fromLower = RustText.ToLowercase(from.Value);
        var targetLower = RustText.ToLowercase(target.Value);
        if (targetLower.StartsWith(fromLower + "/", StringComparison.Ordinal))
        {
            throw VaultException.InvalidPath(RelativeToSheets(target), "un dossier ne peut pas aller dans l'un de ses propres sous-dossiers");
        }
        var targetAbsolute = Confine(target);
        if (_storage.Exists(targetAbsolute) && !_storage.SameEntry(absolute, targetAbsolute))
        {
            throw VaultException.AlreadyExists(targetAbsolute);
        }
        Execute(modified: null, PlanFolder(from, target), now);
        return RelativeToSheets(target);
    }

    /// <summary>Prépare le déplacement du dossier : un seul déplacement, puis la réécriture des liens concernés.</summary>
    private OperationJournalFile PlanFolder(VaultPath from, VaultPath to)
    {
        // Où se retrouve chaque cible (fichier ou dossier) après l'opération.
        string? Move(string linked) =>
            linked.StartsWith(from.Value, StringComparison.Ordinal)
            && (linked.Length == from.Value.Length || linked[from.Value.Length] == '/')
                ? to.Value + linked[from.Value.Length..]
                : null;

        var writes = new List<PlannedWrite>();
        var files = _sheets.Values
            .Where(entry => entry.Presence == FilePresence.Present)
            .Select(entry => entry.Sheet.Path)
            .Concat(Health.UntrackedList)
            .ToList();
        foreach (var path in files)
        {
            if (TryReadText(path) is not var (bytes, text))
            {
                continue;
            }
            var folder = path.Parent?.Value ?? string.Empty;
            // Un fichier du dossier déplacé est réécrit à sa nouvelle place, et ses liens sont
            // recalculés depuis son nouveau dossier.
            var (destination, newFolder) = Move(path.Value) is { } moved
                ? (VaultPath.Create(moved), VaultPath.Create(moved).Parent?.Value ?? string.Empty)
                : (path, folder);
            if (MarkdownLinks.Rewrite(text, folder, newFolder, Move) is { } rewritten)
            {
                writes.Add(new PlannedWrite(destination, Fingerprint.Of(bytes), rewritten));
            }
        }
        return new OperationJournalFile(
            1,
            OperationState.InProgress,
            $"déplacement du dossier {from} vers {to}",
            [new PlannedMove(from, to)],
            [.. writes],
            UnknownProperties.None);
    }

    /// <summary>Dossiers présents sous <c>fiches/</c>, hors dossiers cachés et dossiers de ressources.</summary>
    private List<string> FoldersOnDisk()
    {
        var found = new List<string>();
        var toVisit = new Stack<string>([SheetsFolder.ToAbsolute(Root)]);
        while (toVisit.TryPop(out var current))
        {
            List<StorageEntry> entries;
            try
            {
                entries = [.. _storage.Entries(current).Where(entry => entry.IsDirectory)];
            }
            catch (Exception exception) when (IsIo(exception))
            {
                // Un dossier illisible est passé : la liste est indicative.
                continue;
            }
            foreach (var entry in entries)
            {
                if (entry.Name.StartsWith('.') || entry.Name.EndsWith(VaultLayout.ResourcesSuffix, StringComparison.Ordinal) || entry.IsLink)
                {
                    continue;
                }
                if (VaultPath.TryFromAbsolute(Root, entry.FullName, out var path, out _))
                {
                    found.Add(RelativeToSheets(path));
                    toVisit.Push(entry.FullName);
                }
            }
        }
        return found;
    }

    /// <summary>
    /// Chemin d'un dossier de fiches (« Réseau/Cisco », relatif à <c>fiches/</c>), validé : ni
    /// vide, ni caché (la corbeille), ni dossier de ressources. Les espaces autour de chaque nom
    /// sont retirés.
    /// </summary>
    private static VaultPath FolderPath(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var parts = folder.Split('/', '\\').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();
        if (parts.Count == 0)
        {
            throw VaultException.InvalidPath(folder, "un dossier doit avoir un nom");
        }
        if (parts.Any(part => part.StartsWith('.')))
        {
            throw VaultException.InvalidPath(folder, "un nom de dossier ne peut pas commencer par un point");
        }
        if (parts.Any(part => Extension(part) is { } extension && Ascii.EqualsIgnoreCase(extension, "ressources")))
        {
            throw VaultException.InvalidPath(folder, "« .ressources » est réservé aux images d'une fiche");
        }
        return VaultPath.Create($"{VaultLayout.Sheets}/{string.Join('/', parts)}");
    }

    /// <summary>« fiches/Réseau/Cisco » → « Réseau/Cisco ».</summary>
    private static string RelativeToSheets(VaultPath path) =>
        path.Value.StartsWith(VaultLayout.Sheets, StringComparison.Ordinal)
            ? path.Value[VaultLayout.Sheets.Length..].TrimStart('/')
            : path.Value;

    /// <summary>Dossier d'une fiche, relatif à <c>fiches/</c> : « fiches/Histoire/XVIIIe/x.md » → « Histoire/XVIIIe ».</summary>
    private static string DisplayFolder(VaultPath path)
    {
        var parent = path.Parent?.Value ?? string.Empty;
        return parent.StartsWith(VaultLayout.Sheets, StringComparison.Ordinal)
            ? parent[VaultLayout.Sheets.Length..].TrimStart('/')
            : parent;
    }
}
