using System.Globalization;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Les libellés de l'écriture d'une fiche.</summary>
public static class EditingText
{
    /// <summary>Lire la fiche.</summary>
    public const string Read = "Lire";

    /// <summary>Écrire la fiche.</summary>
    public const string Write = "Écrire";

    /// <summary>Le raccourci qui passe de l'un à l'autre.</summary>
    public const string WriteKey = "Ctrl+E";

    /// <summary>Passer à l'écriture depuis le texte de la fiche.</summary>
    public const string EditSheet = "Modifier la fiche";

    /// <summary>Nom du champ d'écriture, pour le lecteur d'écran.</summary>
    public const string Source = "Texte de la fiche, en Markdown";

    /// <summary>Nom du rendu, pour le lecteur d'écran.</summary>
    public const string PreviewName = "Rendu de la fiche";

    /// <summary>Nom du texte modifiable, pour le lecteur d'écran.</summary>
    public const string VisualText = "Texte de la fiche, modifiable";

    /// <summary>Afficher le code à gauche du rendu.</summary>
    public const string ShowMarkdown = "Afficher le Markdown";

    /// <summary>L'éditeur visuel n'a pas démarré.</summary>
    public const string VisualUnavailable = "Le texte mis en forme n'a pas pu s'ouvrir : la fiche s'écrit ici en Markdown. Rien n'est perdu.";

    /// <summary>Le presse-papiers est tenu par un autre programme.</summary>
    public const string ClipboardBusy = "Le presse-papiers est occupé par un autre programme : rien n'a été collé. Réessayez dans un instant.";

    /// <summary>Coller sans mise en forme.</summary>
    public const string PastePlain = "Coller sans mise en forme";

    /// <summary>Effacer la mise en forme.</summary>
    public const string ClearFormat = "Effacer la mise en forme";

    /// <summary>Modifier un lien.</summary>
    public const string EditLink = "Modifier le lien";

    /// <summary>Retirer un lien.</summary>
    public const string RemoveLink = "Retirer le lien";

    /// <summary>Ajouter une ligne au-dessus.</summary>
    public const string RowBefore = "Insérer une ligne au-dessus";

    /// <summary>Ajouter une ligne en dessous.</summary>
    public const string RowAfter = "Insérer une ligne en dessous";

    /// <summary>Ajouter une colonne à gauche.</summary>
    public const string ColumnBefore = "Insérer une colonne à gauche";

    /// <summary>Ajouter une colonne à droite.</summary>
    public const string ColumnAfter = "Insérer une colonne à droite";

    /// <summary>Supprimer une ligne.</summary>
    public const string DeleteRow = "Supprimer la ligne";

    /// <summary>Supprimer une colonne.</summary>
    public const string DeleteColumn = "Supprimer la colonne";

    /// <summary>Supprimer un tableau.</summary>
    public const string DeleteTable = "Supprimer le tableau";

    /// <summary>Le titre de la saisie d'une formule.</summary>
    public const string FormulaTitle = "Insérer une formule";

    /// <summary>La source d'une formule.</summary>
    public const string FormulaSource = "Formule (LaTeX)";

    /// <summary>Une formule sur sa propre ligne.</summary>
    public const string FormulaBlock = "Sur sa propre ligne";

    /// <summary>L'aperçu de la formule.</summary>
    public const string FormulaPreview = "Aperçu";

    /// <summary>Les exemples de formules.</summary>
    public const string FormulaExamples = "Exemples";

    /// <summary>Montrer le rendu.</summary>
    public const string Preview = "Aperçu";

    /// <summary>Tout est enregistré.</summary>
    public const string Saved = "Enregistré";

    /// <summary>Une frappe attend d'être enregistrée.</summary>
    public const string Modified = "Modifié";

    /// <summary>L'enregistrement est en cours.</summary>
    public const string Saving = "Enregistrement…";

    /// <summary>La fiche a changé sur le disque.</summary>
    public const string InConflict = "Conflit avec le disque";

    /// <summary>L'enregistrement a échoué.</summary>
    public const string SaveFailed = "Échec de l'enregistrement";

    /// <summary>Ce qui s'est passé, en cas de conflit.</summary>
    public const string ConflictMessage = "Cette fiche a été modifiée hors de kyfRev depuis son ouverture. Rien n'est perdu : choisissez la version à garder, l'autre va dans l'historique.";

    /// <summary>Garder le texte en cours d'écriture.</summary>
    public const string KeepMine = "Garder ma version";

    /// <summary>Reprendre le texte du disque.</summary>
    public const string ReloadDisk = "Recharger la version du disque";

    /// <summary>Refermer un refus.</summary>
    public const string Dismiss = "Fermer";

    /// <summary>Réessayer l'enregistrement.</summary>
    public const string Retry = "Réessayer";

    /// <summary>Le premier onglet du ruban.</summary>
    public const string Home = "Accueil";

    /// <summary>Le deuxième onglet du ruban.</summary>
    public const string Insert = "Insertion";

    /// <summary>Annuler.</summary>
    public const string Undo = "Annuler (Ctrl+Z)";

    /// <summary>Rétablir.</summary>
    public const string Redo = "Rétablir (Ctrl+Y)";

    /// <summary>Le groupe de l'historique.</summary>
    public const string HistoryGroup = "Historique";

    /// <summary>Le groupe du style.</summary>
    public const string StyleGroup = "Style";

    /// <summary>Le groupe de la police.</summary>
    public const string FontGroup = "Police";

    /// <summary>Le groupe du paragraphe.</summary>
    public const string ParagraphGroup = "Paragraphe";

    /// <summary>Le groupe des blocs.</summary>
    public const string BlocksGroup = "Blocs";

    /// <summary>Le groupe des liens et des médias.</summary>
    public const string LinksGroup = "Liens et médias";

    /// <summary>Le groupe des autres insertions.</summary>
    public const string OtherGroup = "Autres";

    /// <summary>Du texte ordinaire.</summary>
    public const string NormalText = "Texte normal";

    /// <summary>Le style du paragraphe.</summary>
    public const string StyleName = "Style du paragraphe";

    /// <summary>Gras.</summary>
    public const string Bold = "Gras (Ctrl+B)";

    /// <summary>Italique.</summary>
    public const string Italic = "Italique (Ctrl+I)";

    /// <summary>Souligné.</summary>
    public const string Underline = "Souligné (Ctrl+U)";

    /// <summary>Barré.</summary>
    public const string Strike = "Barré";

    /// <summary>Exposant.</summary>
    public const string Superscript = "Exposant";

    /// <summary>Indice.</summary>
    public const string Subscript = "Indice";

    /// <summary>Modifier la casse.</summary>
    public const string ChangeCase = "Modifier la casse (Maj+F3 : la suivante)";

    /// <summary>Majuscule en début de phrase, écrit dans sa casse.</summary>
    public const string CaseSentence = "Majuscule en début de phrase";

    /// <summary>Minuscules, écrit dans sa casse.</summary>
    public const string CaseLower = "minuscules";

    /// <summary>Capitales, écrit dans sa casse.</summary>
    public const string CaseUpper = "CAPITALES";

    /// <summary>Première lettre des mots, écrit dans sa casse.</summary>
    public const string CaseWords = "1re Lettre Des Mots En Majuscule";

    /// <summary>Inverser la casse, écrit dans sa casse.</summary>
    public const string CaseInvert = "iNVERSER lA cASSE";

    /// <summary>Une lettre sur deux, écrit dans sa casse.</summary>
    public const string CaseAlternate = "uNe LeTtRe SuR dEuX";

    /// <summary>Liste à puces.</summary>
    public const string Bullets = "Liste à puces";

    /// <summary>La culture des dates de l'écriture.</summary>
    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>L'historique d'une fiche.</summary>
    public const string History = "Historique";

    /// <summary>Le titre de l'historique.</summary>
    public const string HistoryTitle = "Historique des versions";

    /// <summary>Fermer l'historique.</summary>
    public const string HistoryClose = "Fermer";

    /// <summary>La liste des versions.</summary>
    public const string HistoryVersions = "Versions";

    /// <summary>Le côté du texte actuel.</summary>
    public const string HistoryCurrent = "Texte actuel";

    /// <summary>Aucune version encore.</summary>
    public const string HistoryEmpty = "Aucune version pour l'instant. La première est gardée dès le premier enregistrement de chaque séance d'écriture, et avant toute restauration.";

    /// <summary>Demander à remettre une version.</summary>
    public const string HistoryRestoreAsk = "Restaurer cette version…";

    /// <summary>Remettre la version.</summary>
    public const string HistoryRestore = "Restaurer";

    /// <summary>Renoncer à remettre la version.</summary>
    public const string HistoryCancel = "Annuler";

    /// <summary>Ce que fait la remise d'une version.</summary>
    public const string HistoryRestoreNote = "Le texte actuel sera d'abord gardé dans l'historique.";

    /// <summary>La version n'a pas été remise.</summary>
    public const string VersionNotRestored = "La version n'a pas été remise : voyez le message de l'éditeur.";

    /// <summary>Comparer les deux versions d'un conflit.</summary>
    public const string ConflictCompare = "Comparer";

    /// <summary>Le titre de la comparaison d'un conflit.</summary>
    public const string ConflictCompareTitle = "La fiche a changé hors de kyfRev";

    /// <summary>Le côté de la version du disque.</summary>
    public const string ConflictDisk = "Version du disque";

    /// <summary>Le côté de ma version.</summary>
    public const string ConflictMine = "Ma version";

    /// <summary>Combien de versions, et ce qu'elles pèsent.</summary>
    public static string VersionsSummary(int count, long size) =>
        string.Create(Culture, $"{count} version{(count > 1 ? "s" : string.Empty)} · {FileSize(size)}");

    /// <summary>Une taille de fichier.</summary>
    public static string FileSize(long size) => size switch
    {
        < 1024 => string.Create(Culture, $"{size} o"),
        < 1024 * 1024 => string.Create(Culture, $"{size / 1024.0:0.#} ko"),
        _ => string.Create(Culture, $"{size / (1024.0 * 1024):0.#} Mo"),
    };

    /// <summary>Le groupe des dessins.</summary>
    public const string IllustrationsGroup = "Illustrations";

    /// <summary>Insérer un schéma.</summary>
    public const string Schema = "Schéma";

    /// <summary>Insérer un graphique.</summary>
    public const string Chart = "Graphique";

    /// <summary>La saisie de la galerie des graphiques.</summary>
    public const string GalleryOneValuePerLine = "Une valeur par ligne (libellé : valeur)";

    /// <summary>Montrer le code d'un schéma ou d'un graphique.</summary>
    public const string GalleryShowCode = "Voir le code Mermaid";

    /// <summary>Insérer un SmartArt.</summary>
    public const string SmartArt = "SmartArt";

    /// <summary>Le titre de la galerie des SmartArt.</summary>
    public const string GallerySmartArtTitle = "Insérer un SmartArt";

    /// <summary>Le titre de la galerie des schémas.</summary>
    public const string GallerySchemaTitle = "Insérer un schéma";

    /// <summary>Le titre de la galerie des graphiques.</summary>
    public const string GalleryChartTitle = "Insérer un graphique";

    /// <summary>La catégorie de tous les modèles.</summary>
    public const string GalleryAll = "Tous";

    /// <summary>La liste des catégories de la galerie.</summary>
    public const string GalleryCategories = "Catégories";

    /// <summary>La liste des modèles de la galerie.</summary>
    public const string GalleryModels = "Modèles";

    /// <summary>La saisie de la galerie.</summary>
    public const string GalleryOnePerLine = "Un élément par ligne";

    /// <summary>Insérer ce qui est choisi dans la galerie.</summary>
    public const string GalleryInsert = "Insérer";

    /// <summary>Liste de tâches.</summary>
    public const string Tasks = "Liste de tâches";

    /// <summary>Taille de la police.</summary>
    public const string FontSize = "Taille de la police";

    /// <summary>Agrandir le texte.</summary>
    public const string FontGrow = "Agrandir le texte";

    /// <summary>Réduire le texte.</summary>
    public const string FontShrink = "Réduire le texte";

    /// <summary>Couleur du texte.</summary>
    public const string TextColor = "Couleur du texte";

    /// <summary>La couleur du thème.</summary>
    public const string ColorAutomatic = "Automatique";

    /// <summary>Une couleur écrite.</summary>
    public const string ColorOther = "Autre couleur (#rrggbb)";

    /// <summary>Appliquer la couleur écrite.</summary>
    public const string ColorApply = "Appliquer";

    /// <summary>La couleur écrite ne se lit pas.</summary>
    public const string ColorInvalid = "Écrivez six chiffres hexadécimaux, de 0 à 9 et de a à f.";

    /// <summary>Aligner à gauche.</summary>
    public const string AlignLeft = "Aligner à gauche (Ctrl+L)";

    /// <summary>Centrer.</summary>
    public const string AlignCenter = "Centrer";

    /// <summary>Aligner à droite.</summary>
    public const string AlignRight = "Aligner à droite (Ctrl+R)";

    /// <summary>Justifier.</summary>
    public const string AlignJustify = "Justifier (Ctrl+J)";

    /// <summary>La taille du texte normal, dans la liste.</summary>
    public static string FontSizeNormal(int pixels) => string.Create(CultureInfo.InvariantCulture, $"{pixels} (normale)");

    /// <summary>Liste numérotée.</summary>
    public const string Numbers = "Liste numérotée";

    /// <summary>Insérer un tableau.</summary>
    public const string Table = "Tableau";

    /// <summary>Insérer un bloc de code, ou changer le langage de celui où se trouve le curseur.</summary>
    public const string Code = "Code";

    /// <summary>Le détail du bouton du bloc de code.</summary>
    public const string CodeHelp = "Bloc de code : choisir son langage. Dans un bloc, change son langage.";

    /// <summary>Du code sans langage.</summary>
    public const string PlainCode = "Texte brut";

    /// <summary>Insérer un lien.</summary>
    public const string Link = "Lien";

    /// <summary>Insérer des images.</summary>
    public const string Images = "Images";

    /// <summary>Insérer une formule.</summary>
    public const string Formula = "Formule";

    /// <summary>Insérer une ligne horizontale.</summary>
    public const string Rule = "Ligne";

    /// <summary>Le titre de la saisie d'un lien.</summary>
    public const string LinkTitle = "Insérer un lien";

    /// <summary>Le texte d'un lien.</summary>
    public const string LinkLabel = "Texte affiché";

    /// <summary>L'adresse d'un lien.</summary>
    public const string LinkAddress = "Adresse";

    /// <summary>Le bouton de la saisie d'un lien.</summary>
    public const string InsertLink = "Insérer";

    /// <summary>Le titre du choix des images.</summary>
    public const string ChooseImages = "Choisir des images";

    /// <summary>Les fichiers proposés au choix des images.</summary>
    public const string ImageFilter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.avif;*.bmp;*.svg";

    /// <summary>Nom de la liste des fiches ouverte par « [[ ».</summary>
    public const string Proposals = "Fiches proposées";

    /// <summary>Nom de la grille des tableaux.</summary>
    public const string TableGrid = "Taille du tableau : flèches pour choisir, Entrée pour insérer";

    /// <summary>Ajouter un mot au dictionnaire.</summary>
    public const string AddToDictionary = "Ajouter au dictionnaire";

    /// <summary>Couper.</summary>
    public const string Cut = "Couper";

    /// <summary>Copier.</summary>
    public const string Copy = "Copier";

    /// <summary>Coller.</summary>
    public const string Paste = "Coller";

    /// <summary>« 4 colonnes × 3 lignes ».</summary>
    public static string TableSize(int rows, int columns) =>
        string.Create(CultureInfo.InvariantCulture, $"{columns} colonne{(columns > 1 ? "s" : string.Empty)} × {rows} ligne{(rows > 1 ? "s" : string.Empty)}");

    /// <summary>Une image trop lourde pour être ajoutée.</summary>
    public static string ImageTooLarge(string name) => $"« {name} » dépasse 50 Mo : image non ajoutée.";

    /// <summary>Le nom d'un titre.</summary>
    public static string Heading(int level) => string.Create(CultureInfo.InvariantCulture, $"Titre {level}");
}
