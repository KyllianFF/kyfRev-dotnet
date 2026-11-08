using KyfRev.Application.Text;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;

namespace KyfRev.Infrastructure.Vault;

/// <remarks>
/// Recherche, comme dans la 0.x (<c>recherche.rs</c>). Un élément est retenu si chacun des mots cherchés se trouve dans son
/// titre ou dans son texte : pour une fiche, titre et étiquettes, puis texte (l'en-tête n'en fait pas partie) ; pour une
/// définition, le terme, puis l'explication, le passage et le commentaire ; pour un passage, le passage et le commentaire ;
/// pour une citation, l'auteur, l'œuvre et les étiquettes, puis le texte et le contexte. Tout se lit de bout en bout par
/// le stockage du coffre : aucun index, et rien de ce qui est cherché ou trouvé n'est écrit.
/// </remarks>
public sealed partial class VaultSession
{
    /// <summary>
    /// Les fiches qui contiennent tous les mots de <paramref name="query"/>, chacune avec un extrait de son texte, par
    /// identifiant. Une fiche dont le fichier est introuvable n'est cherchée que dans son titre et ses étiquettes.
    /// </summary>
    /// <param name="query">Le texte cherché.</param>
    /// <param name="cancellationToken">Annulée, la recherche s'arrête à la fiche en cours et rend ce qu'elle a déjà trouvé.</param>
    public IReadOnlyList<SheetHit> SearchSheets(string query, CancellationToken cancellationToken)
    {
        var words = new SearchWords(query);
        var found = new List<SheetHit>();
        foreach (var entry in _sheets.Values.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
            var sheet = entry.Sheet;
            var text = entry.Presence == FilePresence.Missing ? string.Empty : TextToSearch(sheet.Path);
            if (words.Match($"{sheet.Title} {string.Join(' ', sheet.Tags)}", text[FrontMatter.Split(text).BodyStart..]) is { } excerpt)
            {
                found.Add(new SheetHit(sheet.Id, sheet.Title, DisplayFolder(sheet.Path), sheet.Tracking.Archive is not null, excerpt));
            }
        }
        return found;
    }

    /// <summary>Les passages et les définitions hors corbeille qui contiennent tous les mots, par titre de fiche, puis par terme ; rien sans mot.</summary>
    /// <param name="query">Le texte cherché.</param>
    /// <param name="cancellationToken">Annulée, la recherche s'arrête et rend ce qu'elle a déjà trouvé.</param>
    public IReadOnlyList<AnnotationHit> SearchAnnotations(string query, CancellationToken cancellationToken)
    {
        var words = new SearchWords(query);
        if (words.IsEmpty)
        {
            return [];
        }
        var found = new List<AnnotationHit>();
        foreach (var annotation in AllAnnotations.Where(a => a.TrashedAt is null).TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
            var comment = annotation.Comment ?? string.Empty;
            var (title, body) = annotation.Definition is { } definition
                ? (definition.Term, $"{definition.Explanation}\n{annotation.Anchor.Exact}\n{comment}")
                : (string.Empty, $"{annotation.Anchor.Exact}\n{comment}");
            if (words.Match(title, body) is { } excerpt)
            {
                var sheetTitle = _sheets.TryGetValue(annotation.SheetId, out var entry) ? entry.Sheet.Title : "Fiche introuvable";
                found.Add(new AnnotationHit(annotation.Id, annotation.SheetId, sheetTitle, annotation.Definition?.Term, Abbreviate(annotation.Anchor.Exact), excerpt));
            }
        }
        return
        [
            .. found
                .OrderBy(hit => RustText.ToLowercase(hit.SheetTitle), Utf8Order.Instance)
                .ThenBy(hit => hit.Term is not null)
                .ThenBy(hit => RustText.ToLowercase(hit.Term ?? string.Empty), Utf8Order.Instance)
                .ThenBy(hit => hit.Passage, Utf8Order.Instance),
        ];
    }

    /// <summary>Les citations hors corbeille qui contiennent tous les mots, par auteur (aucun d'abord), puis par texte ; rien sans mot.</summary>
    /// <param name="query">Le texte cherché.</param>
    /// <param name="cancellationToken">Annulée, la recherche s'arrête et rend ce qu'elle a déjà trouvé.</param>
    public IReadOnlyList<QuoteHit> SearchQuotes(string query, CancellationToken cancellationToken)
    {
        var words = new SearchWords(query);
        if (words.IsEmpty)
        {
            return [];
        }
        var found = new List<QuoteHit>();
        foreach (var quote in Quotes().Quotes.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
            var content = quote.Content;
            var title = string.Join(' ', content.Author ?? string.Empty, content.Source.Title ?? string.Empty, string.Join(' ', content.Tags));
            if (words.Match(title, $"{content.Text}\n{content.Context}") is { } excerpt)
            {
                found.Add(new QuoteHit(quote.Path, content.Author, Abbreviate(content.Text), excerpt));
            }
        }
        return
        [
            .. found
                .OrderBy(hit => hit.Author is not null)
                .ThenBy(hit => RustText.ToLowercase(hit.Author ?? string.Empty), Utf8Order.Instance)
                .ThenBy(hit => RustText.ToLowercase(hit.Text), Utf8Order.Instance),
        ];
    }

    /// <summary>Le texte d'un fichier du coffre ; vide s'il est hors du coffre, illisible ou mal encodé : il n'est alors pas cherché.</summary>
    private string TextToSearch(VaultPath path)
    {
        try
        {
            return TryReadAllBytes(Confine(path)) is { } bytes && TryDecode(bytes, out var text) ? text : string.Empty;
        }
        catch (VaultException)
        {
            return string.Empty;
        }
    }
}
