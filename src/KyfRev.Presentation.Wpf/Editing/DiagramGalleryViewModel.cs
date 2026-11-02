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
/// La galerie, comme celle d'Office : des catégories, une vignette par modèle, et un aperçu qui suit le texte saisi.
/// </summary>
public sealed partial class DiagramGalleryViewModel : ObservableObject
{
    private readonly IReadOnlyList<GalleryItem> _all;

    private DiagramGalleryViewModel(GalleryKind kind, IReadOnlyList<GalleryItem> all, int? position)
    {
        Kind = kind;
        _all = all;
        Position = position;
        Categories = [EditingText.GalleryAll, .. all.Select(item => item.Category).Distinct(StringComparer.Ordinal)];
        SelectedCategory = EditingText.GalleryAll;
        SelectedItem = all[0];
    }

    /// <summary>Ce que la galerie propose d'insérer.</summary>
    public GalleryKind Kind { get; }

    /// <summary>Le bloc qu'elle remplace ; rien : elle en insère un.</summary>
    public int? Position { get; }

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

    /// <summary>Le texte saisi, un élément par ligne.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    [NotifyPropertyChangedFor(nameof(MermaidCode))]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>Le modèle choisi s'écrit en Mermaid.</summary>
    public bool IsMermaid => Kind != GalleryKind.SmartArt;

    /// <summary>Le modèle choisi est dessiné par kyfRev.</summary>
    public bool IsDrawn => !IsMermaid;

    /// <summary>Le dessin du modèle choisi, avec le texte saisi ; rien pour un modèle Mermaid.</summary>
    public DiagramScene? Preview => !IsMermaid && SelectedItem is { } item ? SmartArt.Draw(item.Id, Text) : null;

    /// <summary>Le code du modèle choisi, avec le texte saisi ; rien pour un SmartArt.</summary>
    public string? MermaidCode => IsMermaid ? MermaidTemplates.Code(SelectedItem?.Id, Text) : null;

    /// <summary>Le code se montre sous l'aperçu.</summary>
    [ObservableProperty]
    public partial bool ShowsCode { get; set; }

    /// <summary>Ce qu'il faut écrire dans la saisie.</summary>
    public string InputHelp => Kind == GalleryKind.Chart ? EditingText.GalleryOneValuePerLine : EditingText.GalleryOnePerLine;

    /// <summary>Une galerie vide, pour insérer.</summary>
    public static DiagramGalleryViewModel For(GalleryKind kind) => new(kind, ItemsOf(kind), null);

    /// <summary>Une galerie ouverte sur un SmartArt de la fiche, pour le modifier ; rien si ce bloc n'en est pas un.</summary>
    /// <param name="info">Ce qui suit les accents graves de son bloc.</param>
    /// <param name="code">Le contenu de son bloc.</param>
    /// <param name="position">Où il se trouve.</param>
    public static DiagramGalleryViewModel? ForExisting(string info, string code, int position)
    {
        var parts = (info ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not [SmartArt.Language, var layout])
        {
            return null;
        }
        var gallery = new DiagramGalleryViewModel(GalleryKind.SmartArt, ItemsOf(GalleryKind.SmartArt), position);
        var item = gallery._all.FirstOrDefault(candidate => candidate.Id == layout);
        if (item is null)
        {
            return null;
        }
        gallery.SelectedItem = item;
        gallery.Text = code;
        return gallery;
    }

    /// <summary>Ce qui s'écrit dans le fichier ; rien si aucun modèle n'est choisi, ou s'il n'y a rien à dessiner.</summary>
    public (string Info, string Code)? Source()
    {
        if (SelectedItem is not { } item || SmartArt.Items(Text).IsEmpty)
        {
            return null;
        }
        return IsMermaid ? (MermaidTemplates.Language, MermaidCode!) : SmartArt.Source(item.Id, Text);
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        if (SelectedItem is { } chosen && !Items.Contains(chosen))
        {
            SelectedItem = Items.FirstOrDefault();
        }
    }

    /// <summary>Changer de modèle garde ce qui est écrit, comme dans Office : seul un exemple encore intact se remplace.</summary>
    partial void OnSelectedItemChanged(GalleryItem? oldValue, GalleryItem? newValue)
    {
        if (newValue is not null && (string.IsNullOrWhiteSpace(Text) || Text == oldValue?.Sample))
        {
            Text = newValue.Sample;
        }
    }

    private static List<GalleryItem> ItemsOf(GalleryKind kind)
    {
        if (kind == GalleryKind.SmartArt)
        {
            return [.. SmartArt.Layouts.Select(layout => new GalleryItem(layout.Id, layout.Name, layout.Category, layout.Description, layout.Sample, SmartArt.Draw(layout.Id, layout.Sample)!))];
        }
        var family = kind == GalleryKind.Chart ? MermaidFamily.Chart : MermaidFamily.Schema;
        return [.. MermaidTemplates.All.Where(template => template.Family == family)
            .Select(template => new GalleryItem(template.Id, template.Name, template.Category, template.Description, template.Sample, MermaidTemplates.Thumbnail(template)))];
    }
}
