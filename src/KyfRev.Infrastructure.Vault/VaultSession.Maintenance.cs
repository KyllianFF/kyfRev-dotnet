using System.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Links;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;
using KyfRev.Infrastructure.Vault.Text;

namespace KyfRev.Infrastructure.Vault;

/// <remarks>
/// Santé du coffre et éléments supprimés : chaque problème est signalé et attend une décision ;
/// rien n'est jamais effacé ni réparé en silence. Les réparations ne font qu'ajouter (un suivi, un
/// identifiant neuf pour une copie) ; l'ancien contenu d'un fichier modifié est d'abord gardé
/// dans les versions.
/// </remarks>
public sealed partial class VaultSession
{
    /// <summary>Rapport complet, liens cassés compris (les fiches sont relues).</summary>
    public HealthDetails HealthDetails()
    {
        var health = Health;
        ConcernedPassage Passage(StoredAnnotation annotation) =>
            new(annotation.Id, annotation.SheetId, TitleOf(annotation.SheetId), annotation.Anchor.Exact, annotation.AnchorState);
        // Une annotation disparue depuis l'analyse n'a plus rien à signaler.
        var orphans = health.OrphanAnnotations
            .Select(id => AllAnnotations.FirstOrDefault(a => a.Id == id))
            .OfType<StoredAnnotation>()
            .Select(Passage)
            .ToList();
        var toCheck = AllAnnotations.Where(a => a.TrashedAt is null && a.AnchorState != AnchorState.Anchored).Select(Passage).ToList();
        var brokenLinks = BrokenLinks();
        return new HealthDetails(
            [.. health.Moved.Select(move => new MovedSheet(TitleOf(move.Id), move.From.Value, move.To.Value))],
            [.. health.ModifiedOutside.Select(Concerned)],
            [.. health.Missing.Select(Concerned)],
            [.. health.Duplicates.Select(duplicate => new DuplicateDetails(duplicate.Id, TitleOf(duplicate.Id), [.. duplicate.Paths.Select(path => path.Value)]))],
            [.. health.Untracked.Select(path => path.Value)],
            [.. health.WithoutTracking.Select(file => new ConcernedSheet(file.Id, WithoutMarkdownExtension(file.Path.Name), file.Path.Value))],
            [.. health.Unreadable],
            [.. health.LinksToCheck.Select(path => path.Value)],
            brokenLinks,
            orphans,
            toCheck,
            health.Problems + toCheck.Count + brokenLinks.Count);
    }

    /// <summary>
    /// Liens et images des fiches présentes qui mènent à un fichier absent du coffre, triés par
    /// titre puis destination. Les adresses externes ne sont pas vérifiées.
    /// </summary>
    public IReadOnlyList<BrokenLink> BrokenLinks()
    {
        var broken = new List<BrokenLink>();
        foreach (var (id, entry) in _sheets)
        {
            if (entry.Presence != FilePresence.Present)
            {
                continue;
            }
            string text;
            try
            {
                text = ReadSheet(id).Text;
            }
            catch (VaultException)
            {
                continue;
            }
            var folder = entry.Sheet.Path.Parent?.Value ?? string.Empty;
            foreach (var link in MarkdownLinks.Find(text))
            {
                var raw = text[link.Start..link.End];
                var cut = raw.IndexOfAny(['#', '?']);
                var rawPath = cut < 0 ? raw : raw[..cut];
                if (MarkdownLinks.IsExternal(rawPath))
                {
                    continue;
                }
                var target = MarkdownLinks.Decode(rawPath);
                if (!LeadsToAFile(folder, target))
                {
                    broken.Add(new BrokenLink(id, entry.Sheet.Title, target));
                }
            }
        }
        // Tri stable, comme celui de la 0.x.
        return [.. broken.OrderBy(link => link.Title, Utf8Order.Instance).ThenBy(link => link.Target, Utf8Order.Instance)];
    }

    /// <summary>
    /// Doublon d'identifiant : <paramref name="original"/> garde l'identifiant et son suivi ;
    /// chaque copie reçoit un identifiant neuf et devient une nouvelle fiche (J0).
    /// </summary>
    /// <returns>Les nouveaux identifiants.</returns>
    public IReadOnlyList<string> KeepOriginal(string id, VaultPath original, Moment now)
    {
        ArgumentNullException.ThrowIfNull(original);
        var duplicate = Health.Duplicates.FirstOrDefault(d => d.Id == id) ?? throw VaultException.UnknownSheet(id);
        if (!duplicate.Paths.Contains(original))
        {
            throw VaultException.InvalidPath(original.Value, "ce fichier ne fait pas partie du doublon");
        }
        var created = new List<string>();
        foreach (var copy in duplicate.Paths.Where(path => path != original))
        {
            var (bytes, text) = ReadText(copy);
            var newId = $"f_{_ids.Next()}";
            var updated = FrontMatter.ReplaceField(text, "id", newId);
            SaveVersion(newId, bytes, now);
            ReplaceFile(Confine(copy), Encoding.UTF8.GetBytes(updated));
            CreateTracking(newId, copy, updated, now);
            created.Add(newId);
        }
        Analyze(now);
        return created;
    }

    /// <summary>Fichier qui porte un identifiant sans fichier de suivi : son suivi est recréé (J0), sans toucher au fichier.</summary>
    /// <returns>L'identifiant de la fiche.</returns>
    public string RecreateTracking(VaultPath path, Moment now)
    {
        ArgumentNullException.ThrowIfNull(path);
        var id = Health.WithoutTracking.FirstOrDefault(file => file.Path == path)?.Id
            ?? throw VaultException.InvalidPath(path.Value, "ce fichier n'attend pas de suivi");
        if (!VaultIds.IsSheetId(id) || _sheets.ContainsKey(id))
        {
            throw VaultException.UnknownSheet(id);
        }
        var (_, text) = ReadText(path);
        CreateTracking(id, path, text, now);
        Analyze(now);
        return id;
    }

    /// <summary>Annotations à la corbeille, les plus récentes d'abord (« Éléments supprimés »). Rien n'y est jamais purgé.</summary>
    public IReadOnlyList<TrashedAnnotation> TrashedAnnotations() =>
    [
        .. AllAnnotations
            .Where(annotation => annotation.TrashedAt is not null)
            .Select(annotation => new TrashedAnnotation(
                annotation.Id, annotation.SheetId, TitleOf(annotation.SheetId), annotation.Anchor.Exact, annotation.Comment, annotation.TrashedAt!.Value))
            .OrderByDescending(element => element.TrashedAt),
    ];

    private string TitleOf(string id) => _sheets.TryGetValue(id, out var entry) ? entry.Sheet.Title : id;

    private ConcernedSheet Concerned(string id) =>
        new(id, TitleOf(id), _sheets.TryGetValue(id, out var entry) ? entry.Sheet.Path.Value : string.Empty);

    /// <summary>La destination, résolue depuis le dossier de la fiche, existe dans le coffre.</summary>
    private bool LeadsToAFile(string folder, string target)
    {
        if (MarkdownLinks.Resolve(folder, target) is not { } resolved || !VaultPath.TryParse(resolved, out var path, out _))
        {
            return false;
        }
        try
        {
            return RealPaths.Exists(Confine(path));
        }
        catch (VaultException)
        {
            return false;
        }
    }

    private (byte[] Bytes, string Text) ReadText(VaultPath path)
    {
        var bytes = ReadAllBytes(Confine(path));
        return (bytes, Decode(bytes, path));
    }

    /// <summary>Nouveau suivi (J0 par adoption) pour un fichier de fiche déjà écrit.</summary>
    private void CreateTracking(string id, VaultPath path, string text, Moment now)
    {
        var header = ReadHeader(text, path);
        var title = string.IsNullOrWhiteSpace(header.Title) ? WithoutMarkdownExtension(path.Name) : header.Title;
        var tracking = Started(Tracking.Start(now, Settings.Sequence, StartOrigin.Adoption));
        var sheet = new TrackedSheetFile(1, id, path, title, header.Tags, Fingerprint.Of(Encoding.UTF8.GetBytes(text)), now.Instant, tracking, UnknownProperties.None);
        WriteTracking(sheet, create: true);
    }
}
