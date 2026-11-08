using KyfRev.Application.Text;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Links;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;

namespace KyfRev.Infrastructure.Vault;

/// <summary>
/// Rétroliens et aperçu d'une fiche liée, comme dans la version 0.x (<c>retroliens.rs</c>). Rien n'est enregistré : les
/// fiches qui mènent ici se trouvent d'après les liens déjà relevés (voir <see cref="BrokenLinks"/>), et seules celles-là
/// sont lues pour leur extrait. Le coffre peut donc être grand.
/// </summary>
public sealed partial class VaultSession
{
    /// <summary>Les fiches présentes qui mènent à la fiche <paramref name="id"/>, par titre, avec le texte autour de leur premier lien.</summary>
    /// <exception cref="OperationCanceledException">La demande a été annulée.</exception>
    public IReadOnlyList<Backlink> Backlinks(string id, CancellationToken cancellation)
    {
        var target = Sheet(id).Sheet.Path;
        var snapshot = VaultSnapshot.Of(_storage, _realRoot);
        var backlinks = new List<Backlink>();
        foreach (var (sourceId, entry) in _sheets)
        {
            cancellation.ThrowIfCancellationRequested();
            if (sourceId == id || entry.Presence != FilePresence.Present)
            {
                continue;
            }
            var folder = entry.Sheet.Path.Parent?.Value ?? string.Empty;
            if (TargetsOf(sourceId, entry.Sheet.Path, snapshot) is { } targets
                && targets.Any(candidate => LeadsTo(folder, candidate, target))
                && BacklinkFrom(entry.Sheet, folder, target) is { } backlink)
            {
                backlinks.Add(backlink);
            }
        }
        // Tri stable, comme celui de la 0.x.
        return [.. backlinks.OrderBy(backlink => RustText.ToLowercase(backlink.Title), Utf8Order.Instance)];
    }

    /// <summary>Le début de la fiche située à <paramref name="path"/> (chemin dans le coffre) ; rien si aucune fiche n'y est.</summary>
    /// <param name="path">Son chemin.</param>
    /// <param name="day">Le jour où son étape est lue.</param>
    public SheetPreview? PreviewAt(string path, DateOnly day)
    {
        if (_sheets.Values.FirstOrDefault(entry => entry.Sheet.Path.Value == path) is not { Sheet: var sheet })
        {
            return null;
        }
        var text = TextOf(sheet.Id) ?? string.Empty;
        var state = ReviewState.Of(sheet.Tracking, day);
        return new SheetPreview(
            sheet.Id,
            sheet.Title,
            DisplayedFolder(sheet.Path),
            sheet.Tags,
            sheet.Tracking.Archive is not null,
            SheetExcerpts.Shorten(SheetExcerpts.PlainText(text[FrontMatter.Split(text).BodyStart..], sheet.Title), SheetExcerpts.PreviewLength),
            state.Step,
            state.DueDate);
    }

    /// <summary>
    /// La destination, relative au dossier <paramref name="folder"/>, mène à <paramref name="sheet"/>. Le nom du fichier
    /// écarte d'abord, sans calcul, presque tous les liens.
    /// </summary>
    private static bool LeadsTo(string folder, string target, VaultPath sheet) =>
        target.Contains(sheet.Name, StringComparison.Ordinal)
        && MarkdownLinks.Resolve(folder, target) is { } resolved
        && VaultPath.TryParse(resolved, out var path, out _)
        && path.Value == sheet.Value;

    private static string DisplayedFolder(VaultPath path)
    {
        var parent = path.Parent?.Value ?? string.Empty;
        return parent.StartsWith(VaultLayout.Sheets, StringComparison.Ordinal) ? parent[VaultLayout.Sheets.Length..].TrimStart('/') : parent;
    }

    /// <summary>Le rétrolien d'une fiche, relue maintenant ; rien si elle est illisible ou si son lien a disparu.</summary>
    private Backlink? BacklinkFrom(TrackedSheetFile source, string folder, VaultPath target)
    {
        if (TextOf(source.Id) is not { } text)
        {
            return null;
        }
        var links = MarkdownLinks.Find(text).Where(link => LocalTarget(text[link.Start..link.End]) is { } local && LeadsTo(folder, local, target)).ToList();
        return links.Count == 0
            ? null
            : new Backlink(source.Id, source.Title, DisplayedFolder(source.Path), source.Tracking.Archive is not null, SheetExcerpts.AroundLink(text, links[0]), links.Count);
    }

    /// <summary>Le texte d'une fiche ; rien si elle ne peut pas être lue.</summary>
    private string? TextOf(string id)
    {
        try
        {
            return ReadSheet(id).Text;
        }
        catch (VaultException)
        {
            return null;
        }
    }
}
