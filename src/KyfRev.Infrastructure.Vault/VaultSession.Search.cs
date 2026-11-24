using KyfRev.Application.Text;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;

namespace KyfRev.Infrastructure.Vault;

/// <remarks>
/// Recherche, comme dans la 0.x (<c>recherche.rs</c>). Un élément est retenu si chacun des mots cherchés se trouve dans son
/// titre ou dans son texte : pour une fiche, titre et étiquettes, puis texte (l'en-tête n'en fait pas partie) ; pour une
/// définition, le terme, puis l'explication, le passage et le commentaire ; pour un passage, le passage et le commentaire ;
/// pour une citation, l'auteur, l'œuvre et les étiquettes, puis le texte et le contexte. Tout se lit de bout en bout par
/// le stockage du coffre : aucun index, et rien de ce qui est cherché ou trouvé n'est écrit. Le texte d'une fiche lue reste
/// en mémoire, avec la date et la taille de son fichier, le temps de la session : une recherche suivante ne relit que les
/// fiches qui ont changé.
/// </remarks>
public sealed partial class VaultSession
{
    /// <summary>Le texte gardé pour la recherche, en caractères, au plus : au-delà, les fiches se relisent à chaque fois.</summary>
    internal long MaxSearchCharacters { get; set; } = 32L * 1024 * 1024;

    private readonly Dictionary<string, SearchText> _searchTexts = new(StringComparer.OrdinalIgnoreCase);
    private long _searchCharacters;

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
        var files = SheetFiles();
        foreach (var entry in _sheets.Values.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
            var sheet = entry.Sheet;
            var body = entry.Presence == FilePresence.Missing ? string.Empty : BodyToSearch(sheet.Path, files);
            if (words.Match($"{sheet.Title} {string.Join(' ', sheet.Tags)}", body) is { } excerpt)
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

    /// <summary>La date et la taille de chaque fichier du dossier des fiches, relevées en un parcours ; rien s'il ne se parcourt pas.</summary>
    private Dictionary<string, (DateTime Modified, long Length)> SheetFiles()
    {
        var files = new Dictionary<string, (DateTime Modified, long Length)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var entry in _storage.AllEntries(SheetsFolder.ToAbsolute(Root)).Where(entry => !entry.IsDirectory))
            {
                files[entry.FullName] = (entry.LastWriteTimeUtc, entry.Length);
            }
        }
        catch (Exception exception) when (IsIo(exception))
        {
            // Sans relevé, chaque fiche se relit : la recherche reste juste, seulement plus lente.
            files.Clear();
        }
        return files;
    }

    /// <summary>Le texte d'une fiche, sans son en-tête : celui gardé si son fichier n'a changé ni de date ni de taille, sinon relu.</summary>
    private string BodyToSearch(VaultPath path, Dictionary<string, (DateTime Modified, long Length)> files)
    {
        var absolute = path.ToAbsolute(Root);
        (DateTime Modified, long Length)? file = files.TryGetValue(absolute, out var facts) ? facts : null;
        if (file is { } known && _searchTexts.TryGetValue(absolute, out var kept) && kept.Modified == known.Modified && kept.Length == known.Length)
        {
            return kept.Body;
        }
        var text = TextToSearch(path);
        var body = text[FrontMatter.Split(text).BodyStart..];
        if (file is { } read)
        {
            // Date et taille relevées avant la lecture : un changement entre-temps fera relire la fiche.
            KeepForSearch(absolute, new SearchText(read.Modified, read.Length, body));
        }
        return body;
    }

    private void KeepForSearch(string path, SearchText text)
    {
        var before = _searchTexts.TryGetValue(path, out var old) ? old.Body.Length : 0;
        if (_searchCharacters - before + text.Body.Length > MaxSearchCharacters)
        {
            return;
        }
        _searchCharacters += text.Body.Length - before;
        _searchTexts[path] = text;
    }

    /// <summary>
    /// Oublie le texte gardé d'un fichier que le coffre réécrit : deux écritures de même taille dans le même instant de
    /// l'horloge du disque auraient la même date.
    /// </summary>
    private void ForgetSearchText(string absolute)
    {
        if (_searchTexts.Remove(absolute, out var old))
        {
            _searchCharacters -= old.Body.Length;
        }
    }

    /// <summary>Oublie les textes gardés pour la recherche : l'analyse a pu déplacer ou retirer des fiches.</summary>
    private void ForgetSearchTexts()
    {
        _searchTexts.Clear();
        _searchCharacters = 0;
    }

    /// <summary>Le texte d'une fiche gardé pour la recherche, lu sur le fichier de cette date et de cette taille.</summary>
    private sealed record SearchText(DateTime Modified, long Length, string Body);

    /// <summary>
    /// Le texte d'un fichier du coffre ; vide s'il est hors du coffre, illisible ou mal encodé : il n'est alors pas cherché.
    /// Comme dans la 0.x, un fichier qu'un autre programme tient ouvert en écriture (un éditeur, une synchronisation) se lit.
    /// </summary>
    private string TextToSearch(VaultPath path)
    {
        try
        {
            return TryDecode(_storage.ReadShared(Confine(path)), out var text) ? text : string.Empty;
        }
        catch (Exception exception) when (exception is VaultException or IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
