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

    /// <summary>Un passage dont la position n'est qu'approchée.</summary>
    public const string CandidatePassage = "Position à vérifier : son texte a changé.";

    /// <summary>Copier la sélection.</summary>
    public const string Copy = "Copier";

    /// <summary>Surligner la sélection.</summary>
    public const string Highlight = "Surligner";

    /// <summary>Le raccourci de « Surligner ».</summary>
    public const string HighlightKey = "H";

    /// <summary>Surligner la sélection avec un commentaire.</summary>
    public const string Annotate = "Annoter…";

    /// <summary>Faire de la sélection une définition.</summary>
    public const string Define = "Définir…";

    /// <summary>Amener un passage à l'écran.</summary>
    public const string ShowPassage = "Afficher le passage";

    /// <summary>Amener une définition à l'écran.</summary>
    public const string ShowDefinition = "Afficher la définition";

    /// <summary>Changer le commentaire d'un passage.</summary>
    public const string EditComment = "Modifier le commentaire…";

    /// <summary>Changer une définition.</summary>
    public const string EditDefinition = "Modifier la définition…";

    /// <summary>Accepter la position proposée.</summary>
    public const string ConfirmPosition = "Confirmer la position";

    /// <summary>Refuser la position proposée.</summary>
    public const string KeepWithoutPosition = "Garder sans position";

    /// <summary>Mettre un passage à la corbeille.</summary>
    public const string DeletePassage = "Supprimer le passage…";

    /// <summary>Mettre une définition à la corbeille.</summary>
    public const string DeleteDefinition = "Supprimer la définition…";

    /// <summary>Le titre de la saisie d'un commentaire.</summary>
    public const string CommentTitle = "Commentaire du passage";

    /// <summary>Le titre de la saisie d'une définition.</summary>
    public const string DefinitionTitle = "Définition";

    /// <summary>Le champ du terme.</summary>
    public const string TermLabel = "Terme";

    /// <summary>Le champ de l'explication.</summary>
    public const string ExplanationLabel = "Ce que signifie ce terme";

    /// <summary>Enregistrer une saisie.</summary>
    public const string Save = "Enregistrer";

    /// <summary>Une définition incomplète.</summary>
    public const string DefinitionIncomplete = "Écrivez le terme et son explication.";

    /// <summary>Le titre de la suppression d'un passage.</summary>
    public const string DeletePassageTitle = "Supprimer ce passage ?";

    /// <summary>Le titre de la suppression d'une définition.</summary>
    public const string DeleteDefinitionTitle = "Supprimer cette définition ?";

    /// <summary>Ce que devient ce qu'on supprime.</summary>
    public const string DeleteMessage = "Il va à la corbeille et ne sera plus révisé.";

    /// <summary>Confirmer une suppression.</summary>
    public const string DeleteLabel = "Supprimer";

    /// <summary>Un passage vient d'être surligné.</summary>
    public const string PassageCreated = "Passage surligné : à réviser demain (J1).";

    /// <summary>Une définition vient d'être écrite.</summary>
    public const string DefinitionCreated = "Définition enregistrée : à réviser demain (J1).";

    /// <summary>Une annotation vient de changer.</summary>
    public const string Saved = "Modification enregistrée.";

    /// <summary>Une annotation vient de partir à la corbeille.</summary>
    public const string Deleted = "Mis à la corbeille.";

    /// <summary>Une position vient d'être confirmée.</summary>
    public const string PositionConfirmed = "Position confirmée.";

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
