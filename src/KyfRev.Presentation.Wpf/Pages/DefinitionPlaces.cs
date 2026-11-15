using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Un endroit d'une fiche où le terme est écrit : une nouvelle définition peut s'y accrocher.</summary>
/// <param name="SheetId">La fiche.</param>
/// <param name="Where">Son dossier et son titre, puis la partie de la fiche (« Réseau › Commutation › VLAN »).</param>
/// <param name="Excerpt">Le terme dans sa phrase (« … le VLAN natif d'un trunk … »).</param>
/// <param name="Anchor">L'ancre de la définition, à cet endroit.</param>
public sealed record DefinitionPlace(string SheetId, string Where, string Excerpt, Anchor Anchor)
{
    /// <summary>Ce qu'en dit un lecteur d'écran : l'endroit, puis la phrase.</summary>
    public override string ToString() => $"{Where} : {Excerpt}";
}

/// <summary>
/// Les endroits du coffre où un terme est écrit, cherchés comme les mots définis : en mot entier, sans tenir compte des
/// majuscules, au singulier ou au pluriel, hors titres, code et liens, une fois par partie de fiche. Les fiches viennent de la
/// recherche du coffre, les archivées exclues ; on n'en lit que quelques-unes, et quelques endroits suffisent pour choisir.
/// </summary>
internal static class DefinitionPlaces
{
    /// <summary>Le nombre de fiches lues au plus.</summary>
    internal const int Sheets = 12;

    /// <summary>Le nombre d'endroits proposés au plus.</summary>
    internal const int Places = 6;

    /// <summary>Les endroits où <paramref name="term"/> est écrit ; aucun pour un terme trop court ou une recherche refusée.</summary>
    /// <exception cref="OperationCanceledException">Un terme plus récent remplace celui-ci.</exception>
    internal static IReadOnlyList<DefinitionPlace> Find(SheetUseCases sheets, string term, CancellationToken cancellation)
    {
        var clean = term.Trim();
        if (clean.Length < DefinedWords.MinimumLength || !clean.Any(char.IsLetter)
            || sheets.Search(clean, cancellation) is not { Succeeded: true, Value: var hits })
        {
            return [];
        }
        var places = new List<DefinitionPlace>();
        foreach (var hit in hits.Where(hit => !hit.Archived).Take(Sheets))
        {
            cancellation.ThrowIfCancellationRequested();
            if (sheets.Open(hit.Id) is not { Succeeded: true, Value: var sheet })
            {
                continue;
            }
            var reading = SheetReading.Of(sheet, sheets);
            var taken = reading.Highlights.Select(highlight => (highlight.Start, highlight.End)).ToList();
            foreach (var found in DefinedWords.Find(reading.Map, [new DefinedTerm(hit.Id, clean)], taken))
            {
                var anchor = reading.AnchorAt(new TextPosition(found.Start, found.End));
                places.Add(new DefinitionPlace(sheet.Id, WhereOf(hit.Folder, sheet.Title, anchor.Section), ExcerptOf(anchor), anchor));
                if (places.Count == Places)
                {
                    return places;
                }
            }
        }
        return places;
    }

    /// <summary>« Réseau › Commutation › VLAN » : le dossier, le titre de la fiche, puis la partie où le terme est écrit.</summary>
    private static string WhereOf(string folder, string title, IEnumerable<string> section) =>
        string.Join(" › ", new[] { folder.Replace("/", " › ", StringComparison.Ordinal), title }.Concat(section.TakeLast(1)).Where(part => part.Length > 0));

    /// <summary>Le terme dans sa phrase : le contexte s'arrête au bloc du terme (pas de titre voisin), et aux espaces les plus proches.</summary>
    private static string ExcerptOf(Anchor anchor)
    {
        var before = anchor.Before[(anchor.Before.LastIndexOf('\n') + 1)..];
        var after = anchor.After.IndexOf('\n', StringComparison.Ordinal) is var end and >= 0 ? anchor.After[..end] : anchor.After;
        // Un mot coupé par la longueur du contexte s'en va ; un contexte arrêté au bloc garde ses mots entiers.
        var beforeCut = before.Length == anchor.Before.Length && anchor.Before.Length >= Anchoring.Context;
        var afterCut = after.Length == anchor.After.Length && anchor.After.Length >= Anchoring.Context;
        var space = before.IndexOf(' ', StringComparison.Ordinal);
        before = beforeCut && space >= 0 ? before[(space + 1)..] : before;
        space = after.LastIndexOf(' ');
        after = afterCut && space > 0 ? after[..space] : after;
        return $"« {(beforeCut ? "… " : string.Empty)}{before}{anchor.Exact}{after}{(afterCut ? " …" : string.Empty)} »";
    }
}
