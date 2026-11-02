using System.Collections.Immutable;

namespace KyfRev.Rendering.Diagrams;

/// <summary>Le rôle d'une forme : c'est le thème qui donne sa couleur et celle du texte posé dessus.</summary>
public enum DiagramTone
{
    /// <summary>La couleur principale du dessin.</summary>
    Main,

    /// <summary>Ce qui se distingue : un centre, un sommet, un résultat.</summary>
    Accent,

    /// <summary>Ce qui reste en retrait.</summary>
    Quiet,
}

/// <summary>Un point d'un dessin.</summary>
/// <param name="X">Depuis la gauche.</param>
/// <param name="Y">Depuis le haut.</param>
public readonly record struct DiagramPoint(double X, double Y);

/// <summary>Une forme d'un dessin.</summary>
public abstract record DiagramShape;

/// <summary>Un rectangle.</summary>
/// <param name="X">Sa gauche.</param>
/// <param name="Y">Son haut.</param>
/// <param name="Width">Sa largeur.</param>
/// <param name="Height">Sa hauteur.</param>
/// <param name="Radius">L'arrondi de ses coins.</param>
/// <param name="Tone">Son rôle.</param>
public sealed record DiagramRectangle(double X, double Y, double Width, double Height, double Radius, DiagramTone Tone) : DiagramShape;

/// <summary>Une ellipse.</summary>
/// <param name="CenterX">Son centre, depuis la gauche.</param>
/// <param name="CenterY">Son centre, depuis le haut.</param>
/// <param name="RadiusX">Son rayon en largeur.</param>
/// <param name="RadiusY">Son rayon en hauteur.</param>
/// <param name="Tone">Son rôle.</param>
/// <param name="Opacity">Son opacité, pour voir ce qu'elle recouvre.</param>
public sealed record DiagramEllipse(double CenterX, double CenterY, double RadiusX, double RadiusY, DiagramTone Tone, double Opacity = 1) : DiagramShape;

/// <summary>Un polygone fermé.</summary>
/// <param name="Points">Ses sommets.</param>
/// <param name="Tone">Son rôle.</param>
public sealed record DiagramPolygon(ImmutableArray<DiagramPoint> Points, DiagramTone Tone) : DiagramShape;

/// <summary>Un trait.</summary>
/// <param name="From">Son départ.</param>
/// <param name="To">Son arrivée.</param>
/// <param name="HasArrow">Une flèche à son arrivée.</param>
public sealed record DiagramLine(DiagramPoint From, DiagramPoint To, bool HasArrow) : DiagramShape;

/// <summary>Un texte, centré sur son point.</summary>
/// <param name="CenterX">Son centre, depuis la gauche.</param>
/// <param name="CenterY">Son centre, depuis le haut.</param>
/// <param name="Lines">Ses lignes, déjà coupées.</param>
/// <param name="Size">La taille de sa police.</param>
/// <param name="Tone">La forme sur laquelle il est posé ; rien : sur le fond de la fiche.</param>
public sealed record DiagramText(double CenterX, double CenterY, ImmutableArray<string> Lines, double Size, DiagramTone? Tone) : DiagramShape;

/// <summary>Un dessin, prêt à afficher ou à écrire : il ne dépend ni de l'affichage, ni du thème.</summary>
/// <param name="Width">Sa largeur logique.</param>
/// <param name="Height">Sa hauteur logique.</param>
/// <param name="Shapes">Ses formes, de la plus au fond à la plus devant.</param>
/// <param name="Description">Ce qu'il montre, pour le lecteur d'écran.</param>
public sealed record DiagramScene(double Width, double Height, ImmutableArray<DiagramShape> Shapes, string Description);
