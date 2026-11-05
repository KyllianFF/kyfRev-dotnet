using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Rendering.Diagrams;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Ce que la galerie propose d'insérer.</summary>
public enum GalleryKind
{
    /// <summary>Les schémas.</summary>
    Schema,

    /// <summary>Les graphiques.</summary>
    Chart,

    /// <summary>Les SmartArt.</summary>
    SmartArt,
}

/// <summary>Un modèle de la galerie.</summary>
/// <param name="Id">Son nom dans le fichier.</param>
/// <param name="Name">Son nom à l'écran.</param>
/// <param name="Category">Sa catégorie.</param>
/// <param name="Description">À quoi il sert.</param>
/// <param name="Sample">Un exemple, un élément par ligne.</param>
/// <param name="Thumbnail">Sa vignette.</param>
public sealed record GalleryItem(string Id, string Name, string Category, string Description, string Sample, DiagramScene Thumbnail)
{
    /// <summary>Son nom, que dit le lecteur d'écran dans la liste.</summary>
    public override string ToString() => Name;
}

/// <summary>
/// La galerie, comme celle d'Office : des catégories, une vignette par modèle, et un aperçu de son exemple. Elle sert à choisir le modèle :
/// ses éléments s'écrivent ensuite dans l'éditeur.
/// </summary>
public sealed partial class DiagramGalleryViewModel : ObservableObject
{
    private readonly IReadOnlyList<GalleryItem> _all;

    private DiagramGalleryViewModel(GalleryKind kind, IReadOnlyList<GalleryItem> all)
    {
        Kind = kind;
        _all = all;
        Categories = [EditingText.GalleryAll, .. all.Select(item => item.Category).Distinct(StringComparer.Ordinal)];
        SelectedCategory = EditingText.GalleryAll;
        SelectedItem = all[0];
    }

    /// <summary>Ce que la galerie propose d'insérer.</summary>
    public GalleryKind Kind { get; }

    /// <summary>Le titre de la galerie.</summary>
    public string Title => Kind switch
    {
        GalleryKind.Schema => EditingText.GallerySchemaTitle,
        GalleryKind.Chart => EditingText.GalleryChartTitle,
        _ => EditingText.GallerySmartArtTitle,
    };

    /// <summary>Les catégories, dont « Tous ».</summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>La catégorie montrée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Items))]
    public partial string SelectedCategory { get; set; }

    /// <summary>Les modèles de la catégorie montrée.</summary>
    public IReadOnlyList<GalleryItem> Items =>
        SelectedCategory == EditingText.GalleryAll ? _all : [.. _all.Where(item => item.Category == SelectedCategory)];

    /// <summary>Le modèle choisi.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    [NotifyPropertyChangedFor(nameof(MermaidCode))]
    public partial GalleryItem? SelectedItem { get; set; }

    /// <summary>Le modèle choisi s'écrit en Mermaid.</summary>
    public bool IsMermaid => Kind != GalleryKind.SmartArt;

    /// <summary>Le modèle choisi est dessiné par kyfRev.</summary>
    public bool IsDrawn => !IsMermaid;

    /// <summary>Le dessin de l'exemple du modèle choisi ; rien pour un modèle Mermaid.</summary>
    public DiagramScene? Preview => !IsMermaid && SelectedItem is { } item ? SmartArt.Draw(item.Id, item.Sample) : null;

    /// <summary>Le code de l'exemple du modèle choisi ; rien pour un SmartArt.</summary>
    public string? MermaidCode => IsMermaid && SelectedItem is { } item ? MermaidTemplates.Code(item.Id, item.Sample) : null;

    /// <summary>Une galerie, pour insérer.</summary>
    public static DiagramGalleryViewModel For(GalleryKind kind) => new(kind, ItemsOf(kind));

    /// <summary>Les modèles d'une famille.</summary>
    internal static List<GalleryItem> ItemsOf(GalleryKind kind)
    {
        if (kind == GalleryKind.SmartArt)
        {
            return [.. SmartArt.Layouts.Select(layout => new GalleryItem(layout.Id, layout.Name, layout.Category, layout.Description, layout.Sample, SmartArt.Draw(layout.Id, layout.Sample)!))];
        }
        var family = kind == GalleryKind.Chart ? MermaidFamily.Chart : MermaidFamily.Schema;
        return [.. MermaidTemplates.All.Where(template => template.Family == family)
            .Select(template => new GalleryItem(template.Id, template.Name, template.Category, template.Description, template.Sample, MermaidTemplates.Thumbnail(template)))];
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        if (SelectedItem is { } chosen && !Items.Contains(chosen))
        {
            SelectedItem = Items.FirstOrDefault();
        }
    }
}
