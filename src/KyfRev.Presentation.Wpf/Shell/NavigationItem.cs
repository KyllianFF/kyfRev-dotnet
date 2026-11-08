using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Application.Model;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Une entrée du menu latéral et la page qu'elle affiche.</summary>
/// <param name="Id">Identifiant stable (« aujourdhui », « citations »…).</param>
/// <param name="Label">Libellé.</param>
/// <param name="Glyph">Icône (police d'icônes de Windows).</param>
/// <param name="Order">Rang dans le menu ; 900 et plus : en bas du menu.</param>
/// <param name="Page">Modèle de vue de la page.</param>
/// <param name="Picture">Une icône dessinée, à la place du glyphe (celle d'une extension).</param>
/// <param name="Badge">Un compteur près du libellé.</param>
public sealed record NavigationItem(string Id, string Label, string Glyph, int Order, object Page, NavigationPicture? Picture = null, NavigationBadge? Badge = null)
{
    /// <summary>L'entrée a une icône dessinée.</summary>
    public bool HasPicture => Picture is not null;

    /// <summary>Rang à partir duquel une entrée va en bas du menu (santé du coffre, paramètres).</summary>
    public const int SecondaryOrder = 900;

    /// <summary>En bas du menu.</summary>
    public bool IsSecondary => Order >= SecondaryOrder;

    /// <summary>Le libellé : c'est aussi le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => Label;
}

/// <summary>Une ligne de la recherche (Ctrl+K) : une commande, ou ce qui a été trouvé dans le coffre.</summary>
/// <param name="Label">Libellé, cherché sans tenir compte de la casse ni des accents.</param>
/// <param name="Detail">Raccourci clavier d'une commande, nature d'un résultat (« définition ») ; ou rien.</param>
/// <param name="Glyph">Icône.</param>
/// <param name="Execute">Ce que fait la commande.</param>
public sealed record PaletteEntry(string Label, string? Detail, string Glyph, Action Execute)
{
    /// <summary>Son groupe.</summary>
    public PaletteGroup Group { get; init; } = PaletteGroup.Commands;

    /// <summary>Où se trouve un résultat : le dossier d'une fiche, la fiche d'un passage.</summary>
    public string? Place { get; init; }

    /// <summary>« · Histoire », à la suite du libellé ; vide sans lieu.</summary>
    public string PlaceText => Place is { Length: > 0 } place ? ShellText.SearchPlaceOf(place) : string.Empty;

    /// <summary>Ce qui se met en avant dans son libellé.</summary>
    public PaletteAccent Accent { get; init; }

    /// <summary>Le texte autour des mots trouvés.</summary>
    public IReadOnlyList<ExcerptPiece> Excerpt { get; init; } = [];

    /// <summary>Elle a un extrait à montrer.</summary>
    public bool HasExcerpt => Excerpt.Count > 0;

    /// <summary>Le libellé, puis où cela se trouve, sa nature et son extrait : c'est aussi le nom lu par le lecteur d'écran.</summary>
    public override string ToString() =>
        string.Join(", ", new[] { Label, Place, Group.HasHeading ? Detail : null, string.Concat(Excerpt.Select(piece => piece.Text)) }.Where(part => part is { Length: > 0 }));
}

/// <summary>Un raccourci clavier, tel que l'aide le présente.</summary>
/// <param name="Keys">Touches (« Ctrl+K »).</param>
/// <param name="Description">Ce qu'il fait.</param>
public sealed record Shortcut(string Keys, string Description);

/// <summary>Une icône dessinée : une géométrie, en traits ou en formes pleines, toujours de la couleur du texte.</summary>
/// <param name="Geometry">Ses tracés.</param>
/// <param name="Stroked">Des traits ; sinon, des formes pleines.</param>
public sealed record NavigationPicture(Geometry Geometry, bool Stroked);

/// <summary>Le compteur d'une entrée du menu, qui change sans refaire le menu.</summary>
public sealed partial class NavigationBadge : ObservableObject
{
    /// <summary>Ce qu'il affiche ; rien pour ne rien afficher.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    public partial string? Text { get; set; }

    /// <summary>Il a quelque chose à afficher.</summary>
    public bool IsShown => Text is not null;
}

/// <summary>Une entrée ajoutée à un menu par une extension.</summary>
/// <param name="Label">Son libellé.</param>
/// <param name="Run">Ce qu'elle fait.</param>
public sealed record MenuContribution(string Label, Action Run);
