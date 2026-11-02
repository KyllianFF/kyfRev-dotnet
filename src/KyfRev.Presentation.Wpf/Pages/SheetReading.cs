using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Un titre du plan de la fiche.</summary>
public sealed partial class OutlineEntry : ObservableObject
{
    internal OutlineEntry(RenderElement heading, int indent)
    {
        Heading = heading;
        Title = heading.TextContent().Trim();
        Indent = indent;
    }

    /// <summary>Le titre dans le contenu de la fiche.</summary>
    public RenderElement Heading { get; }

    /// <summary>Son texte.</summary>
    public string Title { get; }

    /// <summary>Son retrait, selon son niveau.</summary>
    public int Indent { get; }

    /// <summary>Le texte affiché est dans cette partie.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }
}

/// <summary>Un passage surligné dans le texte affiché.</summary>
/// <param name="Id">Son annotation.</param>
/// <param name="Start">Son début.</param>
/// <param name="End">Sa fin.</param>
/// <param name="IsDefinition">Une définition, et non un passage.</param>
/// <param name="IsCandidate">Une position approchée, à vérifier.</param>
public sealed record Highlight(string Id, int Start, int End, bool IsDefinition, bool IsCandidate);

/// <summary>Un passage ou une définition de la fiche, dans son panneau.</summary>
public sealed partial class PassageEntry : ObservableObject
{
    private readonly AnchorResult _place;

    internal PassageEntry(AnnotationView annotation, AnchorResult place)
    {
        Annotation = annotation;
        _place = place;
    }

    /// <summary>L'annotation.</summary>
    public AnnotationView Annotation { get; }

    /// <summary>Une définition, et non un passage.</summary>
    public bool IsDefinition => Annotation.Definition is not null;

    /// <summary>Un passage, et non une définition.</summary>
    public bool IsPassage => !IsDefinition;

    /// <summary>Le terme d'une définition, ou le texte d'un passage.</summary>
    public string Title => Annotation.Definition?.Term ?? Annotation.Anchor.Exact;

    /// <summary>Encore masqué dans le texte à trous : le volet le tait aussi.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShownTitle), nameof(ShownDetail), nameof(HasShownDetail))]
    public partial bool IsMasked { get; set; }

    /// <summary>Ce que le volet en montre.</summary>
    public string ShownTitle => !IsMasked ? Title : IsDefinition ? ReadingText.TermToFind : ReadingText.PassageToFind;

    /// <summary>Ce que le volet montre de son explication ou de son commentaire.</summary>
    public string? ShownDetail => IsMasked ? null : Detail;

    /// <summary>Le volet montre une explication ou un commentaire.</summary>
    public bool HasShownDetail => !string.IsNullOrEmpty(ShownDetail);

    /// <summary>L'explication d'une définition, ou le commentaire d'un passage.</summary>
    public string? Detail => Annotation.Definition?.Explanation ?? Annotation.Comment;

    /// <summary>Une explication ou un commentaire est écrit.</summary>
    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    /// <summary>Où il se trouve dans le texte affiché ; rien s'il n'y est plus.</summary>
    public TextPosition? Position => _place.Position;

    /// <summary>État de son ancrage dans le texte actuel.</summary>
    public AnchorState State => _place.State;

    /// <summary>Son texte a disparu de la fiche.</summary>
    public bool IsLost => State == AnchorState.NotFound;

    /// <summary>Sa position n'est qu'approchée : elle est à confirmer.</summary>
    public bool IsCandidate => State == AnchorState.NeedsReview;

    /// <summary>Il ressort, dans le panneau comme dans le texte.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }
}

/// <summary>
/// Une fiche prête à lire : son contenu, son texte affiché, son plan et ses passages. Rien n'y dépend de
/// l'affichage : le document natif en est tiré sur le fil de l'interface.
/// </summary>
public sealed class SheetReading
{
    private readonly Func<string, Outcome<byte[]>> _readImage;
    private readonly int _firstBodyLine;

    private SheetReading(OpenedSheet sheet, Func<string, Outcome<byte[]>> readImage)
    {
        _readImage = readImage;
        _firstBodyLine = sheet.FirstBodyLine;
        Fingerprint = sheet.Fingerprint;
        Title = sheet.Title;
        Details = string.Join("  ·  ", (sheet.Folder.Length > 0 ? [sheet.Folder] : Array.Empty<string>()).Concat(sheet.Tags.Select(tag => $"#{tag}")));
        Folder = ContentTargets.FolderOf(sheet.Path);
        Root = SheetRenderer.Render(sheet.Body, sheet.Title);
        Map = TextMap.Of(Root);
        var headings = Map.Headings.Where(heading => heading.TextContent().Trim().Length > 0).ToList();
        var top = headings.Count > 0 ? headings.Min(heading => heading.HeadingLevel) : 0;
        Outline = [.. headings.Select(heading => new OutlineEntry(heading, heading.HeadingLevel - top))];
        Passages = Place(sheet.Annotations);
    }

    /// <summary>L'empreinte de la fiche lue.</summary>
    public string Fingerprint { get; }

    /// <summary>Le titre de la fiche.</summary>
    public string Title { get; }

    /// <summary>Son dossier et ses étiquettes.</summary>
    public string Details { get; }

    /// <summary>Son dossier dans le coffre, d'où partent ses liens et ses images.</summary>
    public string Folder { get; }

    /// <summary>Son contenu.</summary>
    public RenderElement Root { get; }

    /// <summary>Son texte affiché.</summary>
    public TextMap Map { get; }

    /// <summary>Son plan, sans le titre répété.</summary>
    public IReadOnlyList<OutlineEntry> Outline { get; }

    /// <summary>Ses passages et définitions, hors corbeille, chacun à sa place dans le texte actuel.</summary>
    public IReadOnlyList<PassageEntry> Passages { get; private set; }

    /// <summary>Ce qui se surligne dans le texte.</summary>
    public IReadOnlyList<Highlight> Highlights => [.. Passages
        .Where(passage => passage.Position is not null)
        .Select(passage => new Highlight(passage.Annotation.Id, passage.Position!.Value.Start, passage.Position.Value.End, passage.IsDefinition, passage.IsCandidate))];

    /// <summary>Tire de la fiche ouverte ce qu'il faut pour la lire.</summary>
    public static SheetReading Of(OpenedSheet sheet, SheetUseCases sheets)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(sheets);
        return Of(sheet, sheets.ReadResource);
    }

    /// <summary>Tire d'une fiche ce qu'il faut pour la lire, ses images venant de <paramref name="readImage"/>.</summary>
    internal static SheetReading Of(OpenedSheet sheet, Func<string, Outcome<byte[]>> readImage) => new(sheet, readImage);

    /// <summary>Replace les annotations relues, le texte n'ayant pas changé.</summary>
    public void Update(ImmutableArray<AnnotationView> annotations) => Passages = Place(annotations);

    /// <summary>L'ancre d'une position du texte affiché, avec son contexte et ses repères.</summary>
    public Anchor AnchorAt(TextPosition position) =>
        Anchoring.AnchorOf(Map.Text, position, Map.SectionAt(position.Start), Map.SourceLineAt(position.Start, _firstBodyLine), Fingerprint);

    /// <summary>
    /// Les ancrages à enregistrer : une nouvelle position du même texte, ou un nouvel état. Rien d'autre :
    /// lire une fiche ne réécrit pas ce qui n'a pas changé, et jamais un passage n'est rattaché à un autre texte.
    /// Le repère de section seul ne compte pas : celui d'un titre à formule diffère de la 0.x, pas sa place.
    /// </summary>
    public IEnumerable<(string Id, Anchor Anchor, AnchorState State)> PlacementChanges()
    {
        foreach (var passage in Passages)
        {
            var saved = passage.Annotation.Anchor;
            var anchor = passage is { State: AnchorState.Anchored, Position: { } position } ? AnchorAt(position) : saved;
            if (passage.State != passage.Annotation.AnchorState || !Anchoring.SameAnchor(anchor with { Section = saved.Section }, saved))
            {
                yield return (passage.Annotation.Id, anchor, passage.State);
            }
        }
    }

    /// <summary>Le contenu d'une image du coffre.</summary>
    internal Outcome<byte[]> ReadImage(string path) => _readImage(path);

    private List<PassageEntry> Place(ImmutableArray<AnnotationView> annotations) =>
    [
        .. annotations.Where(annotation => !annotation.InTrash)
            .Select(annotation => new PassageEntry(annotation, Anchoring.Locate(Map.Text, annotation.Anchor, annotation.AnchorState))),
    ];
}
