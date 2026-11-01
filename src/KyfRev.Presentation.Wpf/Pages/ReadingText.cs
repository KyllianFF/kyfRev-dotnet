using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Les libellés de la lecture d'une fiche.</summary>
public static class ReadingText
{
    /// <summary>Nom du texte de la fiche, pour le lecteur d'écran.</summary>
    public const string Document = "Texte de la fiche";

    /// <summary>Le plan de la fiche.</summary>
    public const string Outline = "Plan";

    /// <summary>Les passages et définitions de la fiche.</summary>
    public const string Passages = "Passages";

    /// <summary>Un passage dont le texte a disparu de la fiche.</summary>
    public const string LostPassage = "Son texte n'est plus dans la fiche.";

    /// <summary>Un lien qui ne mène nulle part.</summary>
    public const string BrokenLink = "Ce lien ne mène à rien.";

    /// <summary>Une fiche liée qui n'existe pas.</summary>
    public const string MissingLinkedSheet = "Cette fiche n'est pas dans le coffre.";

    /// <summary>Un navigateur qui ne s'ouvre pas.</summary>
    public const string CannotOpenAddress = "Cette adresse n'a pas pu être ouverte.";

    /// <summary>Un fichier qui n'est pas une image lisible.</summary>
    public const string UnreadableImage = "Image illisible";

    /// <summary>Une image SVG, qui viendra avec le dessin des diagrammes.</summary>
    public const string SvgLater = "Image SVG, pas encore affichée par cette version";

    /// <summary>Un diagramme, montré sous sa forme de texte.</summary>
    public const string DiagramAsText = "Diagramme, montré ici sous sa forme de texte";

    /// <summary>Une formule que le moteur natif ne sait pas dessiner.</summary>
    public const string FormulaAsText = "Cette formule est montrée sous sa forme de texte.";

    /// <summary>« Passages · 3 ».</summary>
    public static string PassagesOf(int count) => $"{Passages} · {count}";

    /// <summary>Ce qui remplace une image qui n'est pas chargée.</summary>
    public static string Blocked(BlockedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var reason = image.Reason switch
        {
            ImageBlock.Remote => "Image distante bloquée",
            ImageBlock.OutsideVault => "Image hors du coffre",
            _ => "Image hors du coffre (copiez-la dans la fiche)",
        };
        return $"{reason} : {image.Detail}";
    }
}
