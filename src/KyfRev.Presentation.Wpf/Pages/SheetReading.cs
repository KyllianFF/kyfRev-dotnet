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

/// <summary>Un passage ou une définition de la fiche, dans son panneau.</summary>
public sealed class PassageEntry
{
    internal PassageEntry(AnnotationView annotation)
    {
        Annotation = annotation;
    }

    /// <summary>L'annotation.</summary>
    public AnnotationView Annotation { get; }

    /// <summary>Une définition, et non un passage.</summary>
    public bool IsDefinition => Annotation.Definition is not null;

    /// <summary>Un passage, et non une définition.</summary>
    public bool IsPassage => !IsDefinition;

    /// <summary>Le terme d'une définition, ou le texte d'un passage.</summary>
    public string Title => Annotation.Definition?.Term ?? Annotation.Anchor.Exact;

    /// <summary>L'explication d'une définition, ou le commentaire d'un passage.</summary>
    public string? Detail => Annotation.Definition?.Explanation ?? Annotation.Comment;

    /// <summary>Une explication ou un commentaire est écrit.</summary>
    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    /// <summary>Son texte a disparu de la fiche.</summary>
    public bool IsLost => Annotation.AnchorState == AnchorState.NotFound;
}

/// <summary>
/// Une fiche prête à lire : son contenu, son texte affiché, son plan et ses passages. Rien n'y dépend de
/// l'affichage : le document natif en est tiré sur le fil de l'interface.
/// </summary>
public sealed class SheetReading
{
    private readonly Func<string, Outcome<byte[]>> _readImage;

    private SheetReading(OpenedSheet sheet, Func<string, Outcome<byte[]>> readImage)
    {
        _readImage = readImage;
        Title = sheet.Title;
        Details = string.Join("  ·  ", (sheet.Folder.Length > 0 ? [sheet.Folder] : Array.Empty<string>()).Concat(sheet.Tags.Select(tag => $"#{tag}")));
        Folder = ContentTargets.FolderOf(sheet.Path);
        Root = SheetRenderer.Render(sheet.Body, sheet.Title);
        Map = TextMap.Of(Root);
        var headings = Map.Headings.Where(heading => heading.TextContent().Trim().Length > 0).ToList();
        var top = headings.Count > 0 ? headings.Min(heading => heading.HeadingLevel) : 0;
        Outline = [.. headings.Select(heading => new OutlineEntry(heading, heading.HeadingLevel - top))];
        Passages = [.. sheet.Annotations.Where(annotation => !annotation.InTrash).Select(annotation => new PassageEntry(annotation))];
    }

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

    /// <summary>Ses passages et définitions, hors corbeille.</summary>
    public IReadOnlyList<PassageEntry> Passages { get; }

    /// <summary>Tire de la fiche ouverte ce qu'il faut pour la lire.</summary>
    public static SheetReading Of(OpenedSheet sheet, SheetUseCases sheets)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(sheets);
        return Of(sheet, sheets.ReadResource);
    }

    /// <summary>Tire d'une fiche ce qu'il faut pour la lire, ses images venant de <paramref name="readImage"/>.</summary>
    internal static SheetReading Of(OpenedSheet sheet, Func<string, Outcome<byte[]>> readImage) => new(sheet, readImage);

    /// <summary>Où se trouve un passage dans le texte affiché ; rien s'il n'y est plus.</summary>
    public TextPosition? PositionOf(AnnotationView annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return Anchoring.Locate(Map.Text, annotation.Anchor, annotation.AnchorState).Position;
    }

    /// <summary>Le contenu d'une image du coffre.</summary>
    internal Outcome<byte[]> ReadImage(string path) => _readImage(path);
}
