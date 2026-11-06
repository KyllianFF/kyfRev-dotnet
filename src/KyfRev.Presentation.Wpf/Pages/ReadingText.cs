using System.Globalization;

using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Les libellés de la lecture d'une fiche.</summary>
public static class ReadingText
{
    /// <summary>Nom du texte de la fiche, pour le lecteur d'écran.</summary>
    public const string Document = "Texte de la fiche";

    /// <summary>La case d'une tâche faite, pour le lecteur d'écran.</summary>
    public const string TaskDone = "Tâche faite";

    /// <summary>La case d'une tâche à faire, pour le lecteur d'écran.</summary>
    public const string TaskOpen = "Tâche à faire";

    /// <summary>Réviser une fiche.</summary>
    public const string Revise = "Réviser";

    /// <summary>Tout afficher, dans le texte à trous.</summary>
    public const string RevealAllHoles = "Tout afficher";

    /// <summary>Tout masquer de nouveau, dans le texte à trous.</summary>
    public const string HideAllHoles = "Remasquer";

    /// <summary>Un passage encore masqué, dans le volet.</summary>
    public const string PassageToFind = "Passage à retrouver";

    /// <summary>Un terme encore masqué, dans le volet.</summary>
    public const string TermToFind = "Terme à retrouver";

    /// <summary>Le plan de la fiche.</summary>
    public const string Outline = "Plan";

    /// <summary>Le menu « ⋯ » de l'en-tête étroit : le plan et les passages.</summary>
    public const string PanelsMenu = "Plan et passages";

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

    /// <summary>« Écouter d'ici ».</summary>
    public const string ListenFromHere = "Écouter d'ici";

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
    public const string SvgLater = "Image SVG, que ce poste ne peut pas afficher sans le moteur WebView2";

    /// <summary>Un diagramme, montré sous sa forme de texte.</summary>
    public const string DiagramAsText = "Diagramme, montré ici sous sa forme de texte";

    /// <summary>Le nom d'un bloc dessiné par une extension, pour le lecteur d'écran.</summary>
    public static string ExtensionBlockOf(string language) => $"Bloc « {language} »";

    /// <summary>Un bloc d'extension qui ne s'est pas dessiné : son texte reste, avec la raison.</summary>
    public static string ExtensionBlockFailedOf(string language, string reason) => $"Le bloc « {language} » ne s'affiche pas ({reason}) : le voici sous sa forme de texte.";

    /// <summary>La consigne d'une sélection guidée, avec le nom de l'extension qui la demande.</summary>
    public static string GuidedPromptOf(string extension, string prompt) => $"{extension} : {prompt}";

    /// <summary>« Prendre la sélection » sans rien de sélectionné.</summary>
    public const string GuidedNeedsSelection = "Sélectionnez d'abord un texte dans la fiche, ou écrivez-le ci-dessous.";

    /// <summary>Une sélection guidée sans fiche ouverte.</summary>
    public const string GuidedNoSheet = "aucune fiche ouverte : ouvrez-en une, ou donnez-la avec { fiche }";

    /// <summary>Le bouton qui prend la sélection.</summary>
    public const string GuidedTake = "Prendre la sélection";

    /// <summary>Le clic droit sur un texte, pendant une sélection guidée.</summary>
    public static string GuidedTakeFor(string extension) => $"Prendre pour « {extension} »";

    /// <summary>Le champ pour écrire ce qui manque.</summary>
    public const string GuidedTypePlaceholder = "Ou écrivez-le ici, s'il n'est pas dans la fiche";

    /// <summary>Le bouton qui valide le texte écrit.</summary>
    public const string GuidedTypeConfirm = "Valider le texte";

    /// <summary>Le bouton qui passe.</summary>
    public const string GuidedSkip = "Passer";

    /// <summary>Le bouton qui termine la série.</summary>
    public const string GuidedStop = "Terminer";

    /// <summary>Plus aucune extension active ne dessine ce langage.</summary>
    public static string NoExtensionBlockOf(string language) => $"aucune extension active ne dessine les blocs « {language} »";

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

    /// <summary>Où l'on en est dans le texte à trous.</summary>
    public static string HolesSummary(int revealed, int total) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Texte à trous : {revealed} sur {total} affichés.");

    private const int ShortLength = 60;

    /// <summary>Écouter la fiche.</summary>
    public const string Listen = "Écouter";

    /// <summary>La région du lecteur.</summary>
    public const string ListenRegion = "Écouter la fiche";

    /// <summary>Lancer la lecture.</summary>
    public const string ListenPlay = "Lire";

    /// <summary>Mettre en pause.</summary>
    public const string ListenPause = "Pause";

    /// <summary>Reprendre.</summary>
    public const string ListenResume = "Reprendre";

    /// <summary>Arrêter.</summary>
    public const string ListenStop = "Arrêter";

    /// <summary>Le bloc précédent.</summary>
    public const string ListenPrevious = "Bloc précédent";

    /// <summary>Le bloc suivant.</summary>
    public const string ListenNext = "Bloc suivant";

    /// <summary>Fermer le lecteur.</summary>
    public const string ListenClose = "Fermer le lecteur";

    /// <summary>Lentement.</summary>
    public const string RateSlow = "Lente";

    /// <summary>À vitesse normale.</summary>
    public const string RateNormal = "Normale";

    /// <summary>Un peu plus vite.</summary>
    public const string RateFaster = "Un peu plus vite";

    /// <summary>Vite.</summary>
    public const string RateFast = "Rapide";

    /// <summary>Sans voix.</summary>
    public const string NoVoice = "aucune";

    /// <summary>Rien à lire.</summary>
    public const string NothingToListen = "Rien à lire dans cette fiche.";

    /// <summary>Pas de voix française.</summary>
    public const string NoFrenchVoice = "Aucune voix française n'est installée dans Windows (Paramètres › Heure et langue › Voix).";

    /// <summary>Une formule, telle qu'elle se dit.</summary>
    public const string SpokenFormula = "formule";

    /// <summary>Une image, telle qu'elle se dit.</summary>
    public const string SpokenImage = "image";

    /// <summary>« Vitesse : Normale ».</summary>
    public static string RateOf(string rate) => $"Vitesse : {rate}";

    /// <summary>« Voix : Hortense ».</summary>
    public static string VoiceOf(string voice) => $"Voix : {voice}";

    /// <summary>Le nom d'une voix sans ce qui le rallonge : « Microsoft Hortense Desktop » se dit « Hortense ».</summary>
    public static string ShortVoice(string voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        return voice.Replace("Microsoft ", string.Empty, StringComparison.Ordinal).Replace(" Desktop", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>La voix n'a pas pu lire.</summary>
    public static string VoiceFailed(string? reason) => $"La voix de Windows n'a pas pu lire la fiche ({reason}). Réessayez, ou choisissez une autre voix.";

    /// <summary>Choisir une autre place pour un passage perdu ou incertain.</summary>
    public const string RebindLost = "Choisir un autre passage…";

    /// <summary>Changer la place d'un passage.</summary>
    public const string RebindOther = "Modifier le passage…";

    /// <summary>Le rattacher à la sélection.</summary>
    public const string RebindHere = "Rattacher à la sélection";

    /// <summary>Le titre de la confirmation.</summary>
    public const string RebindTitle = "Changer le passage ?";

    /// <summary>Garder sa progression.</summary>
    public const string RebindKeep = "Conserver la progression";

    /// <summary>Repartir de J0.</summary>
    public const string RebindRestart = "Recommencer à J0";

    /// <summary>Renoncer.</summary>
    public const string RebindCancel = "Annuler";

    /// <summary>Le passage est changé, sa progression reste.</summary>
    public const string RebindKept = "Passage modifié, progression conservée.";

    /// <summary>Le passage est changé, il repart de J0.</summary>
    public const string RebindRestarted = "Passage modifié : retour à J0.";

    /// <summary>Rien n'est sélectionné.</summary>
    public const string RebindNoSelection = "Sélectionnez d'abord le nouveau passage dans la fiche.";

    /// <summary>Remettre un passage de la corbeille.</summary>
    public const string Restore = "Restaurer";

    /// <summary>Le nom du bouton, pour un lecteur d'écran.</summary>
    public const string RestoreNamed = "Restaurer {0}";

    /// <summary>Le passage est de retour.</summary>
    public const string Restored = "Passage restauré.";

    /// <summary>« Corbeille · 2 ».</summary>
    public static string TrashOf(int count) => string.Create(CultureInfo.InvariantCulture, $"Corbeille · {count}");

    /// <summary>Une définition à la corbeille.</summary>
    public static string TrashedDefinition(string term) => $"Définition : {term}";

    /// <summary>Un passage à la corbeille.</summary>
    public static string TrashedPassage(string exact) => $"« {Short(exact)} »";

    /// <summary>Ce qu'il faut faire pour rattacher un passage.</summary>
    public static string RebindPrompt(string title) => $"Sélectionnez dans la fiche le nouveau passage pour « {Short(title)} ».";

    /// <summary>La question posée pour confirmer une nouvelle place.</summary>
    public static string RebindMessage(string exact) => $"Nouveau passage : « {Short(exact)} ». Sa progression dans le calendrier peut être conservée, ou repartir de J0.";

    private static string Short(string text) => text.Length <= ShortLength ? text : string.Concat(text.AsSpan(0, ShortLength), "…");
}
