using KyfRev.Application.Modules;
using KyfRev.Application.Text;

namespace KyfRev.Application.UseCases;

/// <summary>
/// Recherche globale (Ctrl+K) : les fiches actives et archivées, les passages et les définitions hors corbeille, puis ce
/// qu'apportent les modules actifs. Les mots cherchés ne sont jamais écrits nulle part, pas même au journal.
/// </summary>
public sealed class SearchUseCases
{
    private readonly ISearchProvider[] _core;

    /// <summary>La recherche sur le coffre ouvert.</summary>
    public SearchUseCases(VaultHost host)
    {
        _core = [new SheetSearch(host), new AnnotationSearch(host)];
    }

    /// <summary>
    /// Ce qui contient tous les mots de <paramref name="query"/>, fiches d'abord ; rien sans mot. Annulée, la recherche
    /// s'arrête où elle en est et rend ce qu'elle a déjà trouvé : à qui l'a annulée de ne pas s'en servir.
    /// </summary>
    /// <param name="query">Le texte cherché.</param>
    /// <param name="modules">Ce que les modules actifs apportent.</param>
    /// <param name="cancellationToken">Une recherche plus récente, ou le verrouillage du coffre, arrête celle-ci.</param>
    [ZeroXCommand("recherche_globale")]
    public Outcome<IReadOnlyList<SearchHit>> Everywhere(string query, IReadOnlyList<ISearchProvider> modules, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var hits = new List<SearchHit>();
        foreach (var provider in _core.Concat(modules))
        {
            var found = provider.Search(query, cancellationToken);
            if (!found.Succeeded)
            {
                return found;
            }
            hits.AddRange(found.Value);
        }
        return Outcome.Of<IReadOnlyList<SearchHit>>(hits);
    }

    /// <summary>Les fiches : celles trouvées par leur titre d'abord, puis par titre.</summary>
    private sealed class SheetSearch(VaultHost host) : ISearchProvider
    {
        public Outcome<IReadOnlyList<SearchHit>> Search(string query, CancellationToken cancellationToken) => host.Use<IReadOnlyList<SearchHit>>(vault =>
            new SearchWords(query).IsEmpty
                ? []
                :
                [
                    .. vault.SearchSheets(query, cancellationToken)
                        .OrderBy(hit => !hit.Excerpt.IsEmpty)
                        .ThenBy(hit => RustText.ToLowercase(hit.Title), Utf8Order.Instance)
                        .Select(hit => new SearchHit(SearchKinds.Sheet, hit.Id, hit.Title, hit.Excerpt) { Detail = hit.Folder, IsArchived = hit.Archived }),
                ]);
    }

    /// <summary>Les passages et les définitions.</summary>
    private sealed class AnnotationSearch(VaultHost host) : ISearchProvider
    {
        public Outcome<IReadOnlyList<SearchHit>> Search(string query, CancellationToken cancellationToken) => host.Use<IReadOnlyList<SearchHit>>(vault =>
        [
            .. vault.SearchAnnotations(query, cancellationToken)
                .Select(hit => new SearchHit(hit.Term is null ? SearchKinds.Passage : SearchKinds.Definition, hit.Id, hit.Term ?? hit.Passage, hit.Excerpt) { SheetId = hit.SheetId, Detail = hit.SheetTitle }),
        ]);
    }
}
