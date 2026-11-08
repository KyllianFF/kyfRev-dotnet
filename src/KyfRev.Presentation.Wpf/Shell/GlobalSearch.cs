using System.Collections.Immutable;
using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Ce qu'il faut à la coquille pour chercher dans le coffre : la racine de composition le lui donne.</summary>
/// <param name="UseCases">La recherche.</param>
/// <param name="Providers">Ce que les modules actifs apportent, relu à chaque recherche.</param>
/// <param name="Open">Ouvre un résultat.</param>
public sealed record GlobalSearch(SearchUseCases UseCases, Func<IReadOnlyList<ISearchProvider>> Providers, Action<SearchHit> Open);

/// <summary>Un groupe de résultats dans la recherche (Ctrl+K) : les commandes, sans titre, ou ce qui a été trouvé.</summary>
/// <param name="Label">Son titre ; vide pour les commandes.</param>
/// <param name="Total">Ce qui a été trouvé.</param>
/// <param name="Shown">Ce qui est montré.</param>
public sealed record PaletteGroup(string Label, int Total, int Shown)
{
    /// <summary>Le groupe des commandes : il vient d'abord, sans titre.</summary>
    public static PaletteGroup Commands { get; } = new(string.Empty, 0, 0);

    /// <summary>Il a un titre à montrer.</summary>
    public bool HasHeading => Label.Length > 0;

    /// <summary>« Fiches · 12 ».</summary>
    public string Heading => ShellText.SearchGroupOf(Label, Total);

    /// <summary>Il reste des résultats qu'on ne montre pas.</summary>
    public bool HasMoreHits => Total > Shown;

    /// <summary>« 8 autres résultats : précisez la recherche. ».</summary>
    public string MoreHits => Dates.Plural(Total - Shown, ShellText.SearchMoreOneOf, ShellText.SearchMoreManyOf);
}

/// <summary>Ce qui se met en avant dans le titre d'un résultat.</summary>
public enum PaletteAccent
{
    /// <summary>Rien.</summary>
    None,

    /// <summary>Le terme d'une définition.</summary>
    Term,

    /// <summary>Un passage surligné.</summary>
    Passage,
}

/// <summary>Les résultats de la recherche globale, tels que la recherche (Ctrl+K) les montre.</summary>
internal static class SearchEntries
{
    /// <summary>Résultats montrés par groupe, au plus : au-delà, on précise la recherche.</summary>
    public const int MaximumPerGroup = 20;

    /// <summary>Les lignes des résultats : fiches, puis passages et définitions, puis citations.</summary>
    public static List<PaletteEntry> Of(IReadOnlyList<SearchHit> hits, Action<SearchHit> open)
    {
        var entries = new List<PaletteEntry>();
        void AddGroup(string label, IEnumerable<SearchHit> group, Func<SearchHit, PaletteEntry> entry)
        {
            var found = group.ToList();
            var heading = new PaletteGroup(label, found.Count, Math.Min(found.Count, MaximumPerGroup));
            entries.AddRange(found.Take(MaximumPerGroup).Select(hit => entry(hit) with { Group = heading, Excerpt = ExcerptOf(hit) }));
        }

        AddGroup(ShellText.Sheets, hits.Where(hit => hit.Kind == SearchKinds.Sheet), hit => SheetEntry(hit, open));
        AddGroup(ShellText.SearchGroupAnnotations, hits.Where(hit => hit.Kind is SearchKinds.Passage or SearchKinds.Definition), hit => AnnotationEntry(hit, open));
        AddGroup(QuotesText.Title, hits.Where(hit => hit.Kind == SearchKinds.Quote), hit => QuoteEntry(hit, open));
        return entries;
    }

    private static ImmutableArray<ExcerptPiece> ExcerptOf(SearchHit hit) =>
        hit.Excerpt.IsEmpty && hit.Text is { } text ? [new(text, false)] : hit.Excerpt;

    private static PaletteEntry SheetEntry(SearchHit hit, Action<SearchHit> open) =>
        new(hit.Title, hit.IsArchived ? ShellText.SearchBadgeArchived : null, hit.IsArchived ? Icons.Archive : Icons.Document, () => open(hit))
        {
            Place = hit.Detail is { Length: > 0 } folder ? SheetRow.DisplayFolder(folder) : null,
        };

    private static PaletteEntry AnnotationEntry(SearchHit hit, Action<SearchHit> open)
    {
        var isDefinition = hit.Kind == SearchKinds.Definition;
        return new PaletteEntry(
            isDefinition ? hit.Title : ShellText.SearchQuotedOf(hit.Title),
            isDefinition ? ShellText.SearchBadgeDefinition : ShellText.SearchBadgePassage,
            isDefinition ? Icons.Dictionary : Icons.Highlighter,
            () => open(hit))
        {
            Place = hit.Detail,
            Accent = isDefinition ? PaletteAccent.Term : PaletteAccent.Passage,
        };
    }

    private static PaletteEntry QuoteEntry(SearchHit hit, Action<SearchHit> open) =>
        new(hit.Title.Length > 0 ? hit.Title : QuotesText.UnknownAuthor, null, Icons.Quote, () => open(hit));
}

/// <summary>Ouvre un résultat de la recherche globale : une fiche, un passage ou une définition dans sa fiche, une citation dans sa vue.</summary>
/// <param name="opener">Ouvre les fiches.</param>
/// <param name="shell">Mène à la vue des citations.</param>
public sealed class SearchHitOpener(IReviewOpener opener, ShellViewModel shell)
{
    /// <summary>Ouvre ce résultat ; rien pour une nature inconnue.</summary>
    public void Open(SearchHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        switch (hit.Kind)
        {
            case SearchKinds.Sheet:
                opener.OpenSheet(hit.Id);
                break;
            case SearchKinds.Passage or SearchKinds.Definition when hit.SheetId is { } sheet:
                opener.OpenPassageInSheet(sheet, hit.Id);
                break;
            case SearchKinds.Quote:
                OpenQuote(hit.Id);
                break;
            default:
                break;
        }
    }

    private void OpenQuote(string path)
    {
        if (shell.MainNavigation.Concat(shell.SecondaryNavigation).FirstOrDefault(item => item.Page is QuotesPageViewModel) is { Page: QuotesPageViewModel page } entry)
        {
            shell.Navigate(entry);
            page.ShowOnly(path);
        }
    }
}
