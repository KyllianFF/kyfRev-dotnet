using System.Globalization;
using KyfRev.Application.Ports;

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

    /// <summary>Le moteur d'affichage de Windows manque sur cet ordinateur : l'éditeur visuel ne peut pas démarrer.</summary>
    public const string VisualEngineMissing = "Le moteur d'affichage de Windows (WebView2) est introuvable sur cet ordinateur : la fiche s'écrit ici en Markdown. Rien n'est perdu.";

    /// <summary>Le presse-papiers est tenu par un autre programme.</summary>
    public const string ClipboardBusy = "Le presse-papiers est occupé par un autre programme : rien n'a été collé. Réessayez dans un instant.";

    /// <summary>Coller sans mise en forme.</summary>
    public const string PastePlain = "Coller sans mise en forme";

    /// <summary>« Autres mises en forme ».</summary>
    public const string MoreFormat = "Autres mises en forme";

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

    /// <summary>Retire le bloc de code où est le curseur.</summary>
    public const string DeleteCode = "Supprimer ce bloc de code";

    /// <summary>Retire l'image visée.</summary>
    public const string DeleteImage = "Supprimer l'image";

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

    /// <summary>L'onglet du ruban qui n'apparaît que dans un tableau.</summary>
    public const string TableTab = "Tableau";

    /// <summary>L'onglet du ruban qui n'apparaît qu'une image choisie.</summary>
    public const string ImageTab = "Image";

    /// <summary>Le bouton qui fait commencer la suite sur la page suivante.</summary>
    public const string PageBreak = "Saut de page";

    /// <summary>Le bouton qui laisse une page entière vide.</summary>
    public const string BlankPage = "Page blanche";

    /// <summary>Ce que fait « Saut de page ».</summary>
    public const string PageBreakHelp = "Saut de page : ce qui suit le curseur commence la page suivante";

    /// <summary>Ce que fait « Page blanche ».</summary>
    public const string BlankPageHelp = "Page blanche : une page entière reste vide après le curseur";

    /// <summary>Le groupe des sauts de page, dans l'onglet Insertion.</summary>
    public const string PagesGroup = "Pages";

    /// <summary>Le groupe du papier, dans l'onglet Mise en page.</summary>
    public const string PaperGroup = "Papier";

    /// <summary>Le bouton qui montre ou cache le papier derrière le texte.</summary>
    public const string PaperToggle = "Montrer le papier de la feuille (à l'écran seulement : il ne s'imprime pas)";

    /// <summary>Une feuille unie.</summary>
    public const string PaperPlain = "Papier uni";

    /// <summary>Un quadrillage de 5 mm.</summary>
    public const string PaperSmallSquares = "Petits carreaux (5 mm)";

    /// <summary>La réglure des cahiers d'écolier.</summary>
    public const string PaperLargeSquares = "Grands carreaux (réglure Seyès)";

    /// <summary>Un cahier ligné.</summary>
    public const string PaperLines = "Lignes (cahier)";

    /// <summary>Un point tous les 5 mm.</summary>
    public const string PaperDots = "Points (5 mm)";

    /// <summary>Du papier millimétré.</summary>
    public const string PaperGraph = "Millimétré";

    /// <summary>L'onglet de la mise en page.</summary>
    public const string PageTab = "Mise en page";

    /// <summary>Le groupe du format de la feuille.</summary>
    public const string PageSheetGroup = "Feuille";

    /// <summary>La feuille sans pages.</summary>
    public const string PageEndless = "Feuille sans fin";

    /// <summary>Le format A4.</summary>
    public const string PageA4 = "A4 (21 × 29,7 cm)";

    /// <summary>Le format A5.</summary>
    public const string PageA5 = "A5 (14,8 × 21 cm)";

    /// <summary>Le format A6.</summary>
    public const string PageA6 = "A6 (10,5 × 14,8 cm)";

    /// <summary>Le groupe de l'orientation.</summary>
    public const string PageOrientationGroup = "Orientation";

    /// <summary>La feuille en hauteur.</summary>
    public const string PagePortrait = "Portrait";

    /// <summary>La feuille en largeur.</summary>
    public const string PageLandscape = "Paysage";

    /// <summary>Le groupe des marges.</summary>
    public const string PageMarginsGroup = "Marges";

    /// <summary>Des marges de 1,27 cm.</summary>
    public const string PageMarginsNarrow = "Marges étroites (1,27 cm)";

    /// <summary>Des marges de 2 cm.</summary>
    public const string PageMarginsNormal = "Marges normales (2 cm)";

    /// <summary>Des marges de 3 cm.</summary>
    public const string PageMarginsWide = "Marges larges (3 cm)";

    /// <summary>Le champ des marges sur mesure.</summary>
    public const string PageMarginsCustom = "Marges sur mesure, en centimètres";

    /// <summary>Un demi-centimètre de marge en moins.</summary>
    public const string PageMarginLess = "Réduire les marges";

    /// <summary>Un demi-centimètre de marge en plus.</summary>
    public const string PageMarginMore = "Agrandir les marges";

    /// <summary>L'unité des marges.</summary>
    public const string PageCentimetres = "cm";

    /// <summary>Le groupe de la portée du choix.</summary>
    public const string PageScopeGroup = "S'applique";

    /// <summary>Le choix vaut pour la fiche ouverte.</summary>
    public const string PageScopeSheet = "À cette fiche";

    /// <summary>Le choix vaut pour toutes les fiches.</summary>
    public const string PageScopeAll = "À toutes les fiches";

    /// <summary>Ce que change « À cette fiche ».</summary>
    public const string PageScopeSheetHelp = "Cette fiche garde sa propre mise en page, sur cet ordinateur";

    /// <summary>Ce que change « À toutes les fiches ».</summary>
    public const string PageScopeAllHelp = "La mise en page vaut pour toutes les fiches qui n'ont pas la leur, sur cet ordinateur";

    /// <summary>Le groupe de la taille d'une image.</summary>
    public const string ImageSizeGroup = "Taille";

    /// <summary>Un quart de la largeur du texte.</summary>
    public const string ImageSmall = "Petite";

    /// <summary>La moitié de la largeur du texte.</summary>
    public const string ImageMedium = "Moyenne";

    /// <summary>Les trois quarts de la largeur du texte.</summary>
    public const string ImageLarge = "Grande";

    /// <summary>Toute la largeur du texte.</summary>
    public const string ImageFull = "Pleine largeur";

    /// <summary>La taille du fichier, sans dépasser le texte.</summary>
    public const string ImageNatural = "Taille d'origine";

    /// <summary>Rétrécit l'image de cinq points.</summary>
    public const string ImageNarrower = "Rétrécir l'image";

    /// <summary>Élargit l'image de cinq points.</summary>
    public const string ImageWider = "Élargir l'image";

    /// <summary>Le groupe de la place d'une image.</summary>
    public const string ImagePlaceGroup = "Position";

    /// <summary>Dans le fil du texte.</summary>
    public const string ImageInline = "Dans le texte";

    /// <summary>Seule sur sa ligne, à gauche.</summary>
    public const string ImageLeft = "À gauche";

    /// <summary>Seule sur sa ligne, au centre.</summary>
    public const string ImageCenter = "Au centre";

    /// <summary>Seule sur sa ligne, à droite.</summary>
    public const string ImageRight = "À droite";

    /// <summary>Le groupe du cadre d'une image.</summary>
    public const string ImageFrameGroup = "Cadrage";

    /// <summary>Ouvre la fenêtre de rognage.</summary>
    public const string ImageCrop = "Rogner…";

    /// <summary>Rend l'image entière.</summary>
    public const string ImageUncrop = "Image entière";

    /// <summary>Le groupe de la révision d'une image.</summary>
    public const string ImageReviewGroup = "Révision";

    /// <summary>Le bouton de rognage, dans le groupe Taille.</summary>
    public const string ImageCropShort = "Rogner";

    /// <summary>La hauteur de l'image, en centimètres.</summary>
    public const string ImageHeight = "Hauteur";

    /// <summary>Sa largeur, en centimètres.</summary>
    public const string ImageWidth = "Largeur";

    /// <summary>Ce que les champs de taille expliquent.</summary>
    public const string ImageSizeHelp = "En centimètres. L'image garde ses proportions : changer l'une change l'autre.";

    /// <summary>Le groupe des réglages à retirer.</summary>
    public const string ImageResetGroup = "Rétablir";

    /// <summary>Ouvre la fenêtre des zones masquées.</summary>
    public const string ImageMask = "Masquer des zones…";

    /// <summary>Une zone masquée.</summary>
    public const string ImageOneMask = "1 zone masquée";

    /// <summary>Plusieurs zones masquées, après leur nombre.</summary>
    public const string ImageManyMasks = "zones masquées";

    /// <summary>Le groupe de la description d'une image.</summary>
    public const string ImageAltGroup = "Description";

    /// <summary>Ce que le champ de description attend.</summary>
    public const string ImageAltHelp = "Ce que montre l'image : lu par un lecteur d'écran, et affiché si l'image manque.";

    /// <summary>Retire largeur, place et cadre ; les zones masquées restent.</summary>
    public const string ImageReset = "Rétablir";

    /// <summary>Le titre de l'image est une légende écrite à la main.</summary>
    public const string ImageCaption = "Cette image a un titre écrit à la main : il sert de légende, et ses réglages ne peuvent pas s'y écrire. Retirez-le dans le code Markdown pour la régler ici.";

    /// <summary>Le bouton de la barre courte qui ouvre toute la mise en forme.</summary>
    public const string Formatting = "Mise en forme";

    /// <summary>Ce que montre le volet de la barre courte.</summary>
    public const string FormattingHelp = "Toute la mise en forme : Accueil, Insertion et, dans un tableau, Tableau";

    /// <summary>Le menu « ⋯ » de la barre courte.</summary>
    public const string ViewMenu = "Affichage";

    /// <summary>Le groupe des en-têtes.</summary>
    public const string HeadersGroup = "En-têtes";

    /// <summary>La première ligne est l'en-tête.</summary>
    public const string HeaderRow = "Première ligne";

    /// <summary>La première colonne est un en-tête.</summary>
    public const string HeaderColumn = "Première colonne";

    /// <summary>La dernière ligne ressort : une synthèse, un total.</summary>
    public const string TotalRow = "Dernière ligne";

    /// <summary>Le groupe des bandes.</summary>
    public const string BandsGroup = "Bandes";

    /// <summary>Aucune bande.</summary>
    public const string BandsNone = "Aucune";

    /// <summary>Une ligne sur deux.</summary>
    public const string BandsRows = "Lignes";

    /// <summary>Une colonne sur deux.</summary>
    public const string BandsColumns = "Colonnes";

    /// <summary>Le groupe des traits.</summary>
    public const string LinesGroup = "Traits";

    /// <summary>Tous les traits.</summary>
    public const string LinesAll = "Tous";

    /// <summary>Les traits entre les lignes.</summary>
    public const string LinesHorizontal = "Horizontaux";

    /// <summary>Les traits entre les colonnes.</summary>
    public const string LinesVertical = "Verticaux";

    /// <summary>Le seul contour.</summary>
    public const string LinesOutline = "Contour";

    /// <summary>Le seul trait sous l'en-tête.</summary>
    public const string LinesHeader = "Sous l'en-tête";

    /// <summary>Aucun trait.</summary>
    public const string LinesNone = "Aucun";

    /// <summary>Le groupe des thèmes de tableau.</summary>
    public const string ThemeGroup = "Thème";

    /// <summary>Les neutres de l'application.</summary>
    public const string ThemeSober = "Sobre";

    /// <summary>Vert forêt.</summary>
    public const string ThemeForest = "Forêt";

    /// <summary>Ambre.</summary>
    public const string ThemeAmber = "Ambre";

    /// <summary>Bleu ardoise.</summary>
    public const string ThemeSlate = "Ardoise";

    /// <summary>Prune.</summary>
    public const string ThemePlum = "Prune";

    /// <summary>Encre, le plus contrasté.</summary>
    public const string ThemeInk = "Encre";

    /// <summary>Le groupe de la colonne du curseur.</summary>
    public const string ColumnGroup = "Colonne";

    /// <summary>Aligner la colonne à gauche.</summary>
    public const string ColumnLeft = "Aligner la colonne à gauche";

    /// <summary>Centrer la colonne.</summary>
    public const string ColumnCenter = "Centrer la colonne";

    /// <summary>Aligner la colonne à droite.</summary>
    public const string ColumnRight = "Aligner la colonne à droite";

    /// <summary>Les cases de la colonne deviennent des trous en révision.</summary>
    public const string HiddenInReview = "Cacher en révision";

    /// <summary>L'aide de la colonne cachée en révision.</summary>
    public const string HiddenInReviewHelp = "En révision, chaque case de cette colonne se cache et se découvre d'un clic ; l'en-tête reste lisible.";

    /// <summary>Trier les lignes selon la colonne du curseur.</summary>
    public const string SortRows = "Trier A → Z";

    /// <summary>L'aide du tri.</summary>
    public const string SortRowsHelp = "Range les lignes selon cette colonne, de A à Z ; un second clic, de Z à A. L'en-tête et la dernière ligne qui ressort restent à leur place.";

    /// <summary>Insérer une ligne au-dessus, dans l'onglet Tableau.</summary>
    public const string RowBeforeShort = "Ligne au-dessus";

    /// <summary>Insérer une ligne en dessous, dans l'onglet Tableau.</summary>
    public const string RowAfterShort = "Ligne en dessous";

    /// <summary>Supprimer la ligne, dans l'onglet Tableau.</summary>
    public const string DeleteRowShort = "Retirer la ligne";

    /// <summary>Insérer une colonne à gauche, dans l'onglet Tableau.</summary>
    public const string ColumnBeforeShort = "Colonne à gauche";

    /// <summary>Insérer une colonne à droite, dans l'onglet Tableau.</summary>
    public const string ColumnAfterShort = "Colonne à droite";

    /// <summary>Supprimer la colonne, dans l'onglet Tableau.</summary>
    public const string DeleteColumnShort = "Retirer la colonne";

    /// <summary>Supprimer le tableau, dans l'onglet Tableau.</summary>
    public const string DeleteTableShort = "Retirer le tableau";

    /// <summary>Le groupe des lignes et colonnes.</summary>
    public const string RowsColumnsGroup = "Lignes et colonnes";

    /// <summary>Le groupe de la largeur.</summary>
    public const string WidthGroup = "Largeur";

    /// <summary>La largeur de son contenu.</summary>
    public const string WidthFit = "Ajustée";

    /// <summary>Toute la largeur du texte.</summary>
    public const string WidthPage = "Pleine page";

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

    /// <summary>Le bouton du code dans le texte.</summary>
    public const string InlineCode = "Code dans le texte";

    /// <summary>Le bouton de la citation.</summary>
    public const string Quote = "Citation";

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

    /// <summary>Insérer ce qui est choisi dans la galerie.</summary>
    public const string GalleryInsert = "Insérer";

    /// <summary>Passer de la galerie à l'éditeur du modèle choisi.</summary>
    public const string GalleryContinue = "Continuer";

    /// <summary>Ce qui attend après la galerie.</summary>
    public const string GalleryNextStep = "Ses éléments se composent à l'étape suivante.";

    /// <summary>Titre de l'éditeur, pour un nouveau dessin.</summary>
    public static string DiagramEditorNewOf(string model) => $"Nouveau : {model}";

    /// <summary>Titre de l'éditeur, pour un dessin de la fiche.</summary>
    public static string DiagramEditorEditOf(string model) => $"Modifier : {model}";

    /// <summary>Le choix du modèle, pour le lecteur d'écran.</summary>
    public const string DiagramModel = "Modèle";

    /// <summary>Ajouter un élément à une liste.</summary>
    public const string DiagramAddItem = "Ajouter un élément";

    /// <summary>Ajouter une ligne à un graphique.</summary>
    public const string DiagramAddRow = "Ajouter une ligne";

    /// <summary>La colonne du libellé.</summary>
    public const string DiagramLabelHeader = "Libellé";

    /// <summary>La colonne de la valeur.</summary>
    public const string DiagramValueHeader = "Valeur";

    /// <summary>La colonne de la seconde valeur.</summary>
    public const string DiagramSecondValueHeader = "Courbe";

    /// <summary>Une valeur illisible.</summary>
    public const string DiagramValueWarning = "Une valeur absente, nulle ou qui n'est pas un nombre compte pour 1.";

    /// <summary>Plus d'éléments que le modèle n'en montre.</summary>
    public static string DiagramLimitOf(int maximum) => string.Create(CultureInfo.InvariantCulture, $"Ce modèle montre {maximum} éléments au plus : les suivants restent écrits, sans se dessiner.");

    /// <summary>Les touches d'une hiérarchie sans clic sur le dessin.</summary>
    public const string DiagramKeysNoClick = "Entrée : même niveau · Ctrl+Entrée : enfant · Alt+flèches : déplacer.";

    /// <summary>Les touches d'une liste.</summary>
    public const string DiagramListKeys = "Entrée : ajouter · Alt+↑ et Alt+↓ : déplacer · Suppr sur une ligne vide : la retirer.";

    /// <summary>Valider l'éditeur, pour une hiérarchie de la fiche.</summary>
    public const string DiagramEditorSave = "Enregistrer";

    /// <summary>La liste des éléments, pour le lecteur d'écran.</summary>
    public const string DiagramItems = "Éléments de la hiérarchie";

    /// <summary>Un élément, pour le lecteur d'écran.</summary>
    public const string DiagramItem = "Texte de l'élément";

    /// <summary>Ajouter sous l'élément choisi.</summary>
    public const string DiagramAddChild = "Ajouter un enfant";

    /// <summary>Le même, avec son raccourci.</summary>
    public const string DiagramAddChildKey = "Ajouter sous cet élément (Ctrl+Entrée)";

    /// <summary>Ajouter au niveau de l'élément choisi.</summary>
    public const string DiagramAddSibling = "Ajouter au même niveau";

    /// <summary>Le même, avec son raccourci.</summary>
    public const string DiagramAddSiblingKey = "Ajouter après cet élément (Entrée)";

    /// <summary>L'action rapide : un enfant.</summary>
    public const string DiagramChild = "Enfant";

    /// <summary>L'action rapide : un élément au même niveau.</summary>
    public const string DiagramSibling = "Même niveau";

    /// <summary>Descendre d'un niveau.</summary>
    public const string DiagramIndent = "Décaler vers la droite";

    /// <summary>Son raccourci.</summary>
    public const string DiagramIndentShortcut = "Alt+→";

    /// <summary>Le même, avec son raccourci.</summary>
    public const string DiagramIndentKey = "Le placer sous l'élément d'avant (Alt+→)";

    /// <summary>Remonter d'un niveau.</summary>
    public const string DiagramOutdent = "Décaler vers la gauche";

    /// <summary>Son raccourci.</summary>
    public const string DiagramOutdentShortcut = "Alt+←";

    /// <summary>Le même, avec son raccourci.</summary>
    public const string DiagramOutdentKey = "Le remonter d'un niveau (Alt+←)";

    /// <summary>Échanger avec l'élément d'avant.</summary>
    public const string DiagramMoveUp = "Monter";

    /// <summary>Son raccourci.</summary>
    public const string DiagramMoveUpShortcut = "Alt+↑";

    /// <summary>Le même, avec son raccourci.</summary>
    public const string DiagramMoveUpKey = "Monter (Alt+↑)";

    /// <summary>Échanger avec l'élément d'après.</summary>
    public const string DiagramMoveDown = "Descendre";

    /// <summary>Son raccourci.</summary>
    public const string DiagramMoveDownShortcut = "Alt+↓";

    /// <summary>Le même, avec son raccourci.</summary>
    public const string DiagramMoveDownKey = "Descendre (Alt+↓)";

    /// <summary>Retirer l'élément choisi.</summary>
    public const string DiagramDelete = "Retirer cet élément";

    /// <summary>Le dessin de haut en bas.</summary>
    public const string DiagramDown = "De haut en bas";

    /// <summary>Le dessin de gauche à droite.</summary>
    public const string DiagramAcross = "De gauche à droite";

    /// <summary>Les touches de la liste.</summary>
    public const string DiagramKeys = "Entrée : même niveau · Ctrl+Entrée : enfant · Alt+flèches : déplacer. Un clic sur une boîte la choisit.";

    /// <summary>Modifier le dessin visé par le clic droit.</summary>
    public const string EditDrawing = "Modifier";

    /// <summary>Montrer le code du dessin visé par le clic droit.</summary>
    public const string ShowDrawingCode = "Voir le code";

    /// <summary>Supprimer un SmartArt de la fiche.</summary>
    public const string DeleteSmartArt = "Supprimer ce SmartArt";

    /// <summary>Supprimer un graphique de la fiche.</summary>
    public const string DeleteChart = "Supprimer ce graphique";

    /// <summary>Retirer de la fiche le bloc d'un schéma réseau : le schéma reste dans le coffre.</summary>
    public const string DeleteNetwork = "Retirer ce schéma de la fiche";

    /// <summary>Le bouton du ruban.</summary>
    public const string NetworkButton = "Réseau";

    /// <summary>Son infobulle.</summary>
    public const string NetworkTip = "Insérer un schéma réseau";

    /// <summary>Titre de l'insertion d'un schéma réseau.</summary>
    public const string NetworkTitle = "Insérer un schéma réseau";

    /// <summary>Le nom d'un nouveau schéma.</summary>
    public const string NetworkNewLabel = "Nom d'un nouveau schéma";

    /// <summary>La liste des schémas du coffre.</summary>
    public const string NetworkExistingLabel = "Ou un schéma du coffre";

    /// <summary>Insérer une copie.</summary>
    public const string NetworkCopy = "Insérer une copie indépendante, plutôt que le schéma lui-même";

    /// <summary>Supprimer un schéma de la fiche.</summary>
    public const string DeleteSchema = "Supprimer ce schéma";

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

    /// <summary>Accepter un mot sans l'écrire dans le dictionnaire de Windows : un coffre chiffré ne le nourrit pas.</summary>
    public const string AcceptUntilClosing = "Accepter jusqu'à la fermeture de kyfRev";

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

    /// <summary>Ce que le menu d'un mot propose, selon ce que le correcteur en fera.</summary>
    public static string AddWord(ISpelling spelling)
    {
        ArgumentNullException.ThrowIfNull(spelling);
        return spelling.AddsUntilClosing ? AcceptUntilClosing : AddToDictionary;
    }

    /// <summary>Le nom d'un titre.</summary>
    public static string Heading(int level) => string.Create(CultureInfo.InvariantCulture, $"Titre {level}");
}
