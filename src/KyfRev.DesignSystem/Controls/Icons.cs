namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Icônes du design system : caractères de la police d'icônes de Windows (Segoe Fluent Icons,
/// Segoe MDL2 Assets à défaut), désignés par leur code.
/// </summary>
public static class Icons
{
    /// <summary>Des blocs assemblés : un SmartArt.</summary>
    public static string SmartArt { get; } = Glyph(0xE8A9);

    /// <summary>Des boîtes reliées : un schéma.</summary>
    public static string Schema { get; } = Glyph(0xF003);

    /// <summary>Case vide : une tâche à faire.</summary>
    public static string TaskOpen { get; } = Glyph(0xE739);

    /// <summary>Case cochée : une tâche faite.</summary>
    public static string TaskDone { get; } = Glyph(0xE73A);

    /// <summary>Cases et lignes : une liste de tâches.</summary>
    public static string TaskList { get; } = Glyph(0xE9D5);

    /// <summary>Lignes à gauche.</summary>
    public static string AlignLeft { get; } = Glyph(0xE8E4);

    /// <summary>Lignes au centre.</summary>
    public static string AlignCenter { get; } = Glyph(0xE8E3);

    /// <summary>Lignes à droite.</summary>
    public static string AlignRight { get; } = Glyph(0xE8E2);

    /// <summary>Lignes de toute la largeur.</summary>
    public static string AlignJustify { get; } = Glyph(0xE700);

    /// <summary>Document : une fiche.</summary>
    public static string Document { get; } = Glyph(0xE8A5);

    /// <summary>Flèche qui revient : annuler.</summary>
    public static string Undo { get; } = Glyph(0xE7A7);

    /// <summary>Flèche qui repart : rétablir.</summary>
    public static string Redo { get; } = Glyph(0xE7A6);

    /// <summary>Gras.</summary>
    public static string Bold { get; } = Glyph(0xE8DD);

    /// <summary>Italique.</summary>
    public static string Italic { get; } = Glyph(0xE8DB);

    /// <summary>Souligné.</summary>
    public static string Underline { get; } = Glyph(0xE8DC);

    /// <summary>Barré.</summary>
    public static string Strike { get; } = Glyph(0xEDE0);

    /// <summary>Liste à puces.</summary>
    public static string Bullets { get; } = Glyph(0xE8FD);

    /// <summary>Lignes de texte : à côté d'un numéro, une liste numérotée.</summary>
    public static string ListLines { get; } = Glyph(0xEA37);

    /// <summary>Chevron vers le bas : un menu s'ouvre.</summary>
    public static string ChevronDown { get; } = Glyph(0xE70D);

    /// <summary>Grille : un tableau.</summary>
    public static string Table { get; } = Glyph(0xE80A);

    /// <summary>Accolades : du code.</summary>
    public static string Code { get; } = Glyph(0xE943);

    /// <summary>Maillon : un lien.</summary>
    public static string Link { get; } = Glyph(0xE71B);

    /// <summary>Paysage : une image.</summary>
    public static string Picture { get; } = Glyph(0xE8B9);

    /// <summary>Trait : une ligne horizontale.</summary>
    public static string Rule { get; } = Glyph(0xE94E);

    /// <summary>Flèche vers la gauche : revenir à la vue précédente.</summary>
    public static string Back { get; } = Glyph(0xE72B);

    /// <summary>Œil : montrer ce qui est caché.</summary>
    public static string Show { get; } = Glyph(0xE7B3);

    /// <summary>Maison : l'accueil.</summary>
    public static string Home { get; } = Glyph(0xE80F);

    /// <summary>Dossier ouvert : ouvrir un coffre existant.</summary>
    public static string FolderOpen { get; } = Glyph(0xE838);

    /// <summary>Nouveau dossier : créer un coffre.</summary>
    public static string NewFolder { get; } = Glyph(0xE8F4);

    /// <summary>Deux feuilles : copier.</summary>
    public static string Copy { get; } = Glyph(0xE8C8);

    /// <summary>Ciseaux : couper.</summary>
    public static string Cut { get; } = Glyph(0xE8C6);

    /// <summary>Presse-papiers : coller.</summary>
    public static string Paste { get; } = Glyph(0xE77F);

    /// <summary>Chevron vers la droite : un dossier replié.</summary>
    public static string ChevronRight { get; } = Glyph(0xE76C);

    /// <summary>Poubelle : supprimer.</summary>
    public static string Trash { get; } = Glyph(0xE74D);

    /// <summary>Crayon : modifier.</summary>
    public static string Edit { get; } = Glyph(0xE70F);

    /// <summary>Bulle : commenter.</summary>
    public static string Comment { get; } = Glyph(0xE90A);

    /// <summary>Toque d'étudiant : réviser.</summary>
    public static string Review { get; } = Glyph(0xE7BE);

    /// <summary>Surligneur : un passage annoté.</summary>
    public static string Highlighter { get; } = Glyph(0xE7E6);

    /// <summary>Flèches circulaires : un rappel.</summary>
    public static string Sync { get; } = Glyph(0xE895);

    /// <summary>Horloge : un retard.</summary>
    public static string Clock { get; } = Glyph(0xE823);

    /// <summary>Boîte : une fiche archivée.</summary>
    public static string Archive { get; } = Glyph(0xE7B8);

    /// <summary>Triangle : un avertissement.</summary>
    public static string Warning { get; } = Glyph(0xE7BA);

    /// <summary>Information.</summary>
    public static string Info { get; } = Glyph(0xE946);

    /// <summary>Coche dans un cercle : une réussite.</summary>
    public static string Success { get; } = Glyph(0xE930);

    /// <summary>Croix dans un cercle : une erreur.</summary>
    public static string Error { get; } = Glyph(0xEA39);

    /// <summary>Croix : fermer.</summary>
    public static string Close { get; } = Glyph(0xE711);

    /// <summary>Écouter.</summary>
    public static string Speaker { get; } = Glyph(0xE767);

    /// <summary>Lire.</summary>
    public static string Play { get; } = Glyph(0xE768);

    /// <summary>Mettre en pause.</summary>
    public static string Pause { get; } = Glyph(0xE769);

    /// <summary>Arrêter.</summary>
    public static string Stop { get; } = Glyph(0xE71A);

    /// <summary>Le bloc précédent.</summary>
    public static string Previous { get; } = Glyph(0xE76B);

    /// <summary>Le bloc suivant.</summary>
    public static string Next { get; } = Glyph(0xE76C);

    /// <summary>Calendrier : le jour.</summary>
    public static string Calendar { get; } = Glyph(0xE787);

    /// <summary>Dictionnaire : les définitions.</summary>
    public static string Dictionary { get; } = Glyph(0xE82D);

    /// <summary>Graphique : les statistiques.</summary>
    public static string Chart { get; } = Glyph(0xE9D2);

    /// <summary>Bulles : les citations.</summary>
    public static string Quote { get; } = Glyph(0xE8F2);

    /// <summary>Cœur : la santé du coffre.</summary>
    public static string Health { get; } = Glyph(0xEB51);

    /// <summary>Engrenage : les paramètres.</summary>
    public static string Settings { get; } = Glyph(0xE713);

    /// <summary>Palette : la galerie des composants.</summary>
    public static string Palette { get; } = Glyph(0xE790);

    /// <summary>Pièce de puzzle : une vue apportée par un module.</summary>
    public static string Puzzle { get; } = Glyph(0xEA86);

    /// <summary>Loupe : rechercher.</summary>
    public static string Search { get; } = Glyph(0xE721);

    /// <summary>Point d'interrogation : l'aide.</summary>
    public static string Help { get; } = Glyph(0xE897);

    /// <summary>Plein écran : le mode concentration.</summary>
    public static string FocusMode { get; } = Glyph(0xE740);

    /// <summary>Loupe plus : agrandir.</summary>
    public static string ZoomIn { get; } = Glyph(0xE8A3);

    /// <summary>Loupe moins : réduire.</summary>
    public static string ZoomOut { get; } = Glyph(0xE71F);

    /// <summary>Soleil : le thème.</summary>
    public static string Theme { get; } = Glyph(0xE706);

    /// <summary>Trois points : les autres vues, les autres actions.</summary>
    public static string More { get; } = Glyph(0xE712);

    /// <summary>Icône d'une sorte de pastille.</summary>
    public static string Of(PillKind kind) => kind switch
    {
        PillKind.Sheet => Document,
        PillKind.Passage => Highlighter,
        PillKind.Reminder => Sync,
        PillKind.Overdue => Clock,
        PillKind.Archived => Archive,
        PillKind.Today => Calendar,
        _ => Warning,
    };

    /// <summary>Icône d'une gravité de notification.</summary>
    public static string Of(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Info => Info,
        NotificationSeverity.Success => Success,
        NotificationSeverity.Warning => Warning,
        _ => Error,
    };

    /// <summary>Un réseau.</summary>
    public static string Network { get; } = Glyph(0xE968);

    /// <summary>Ajouter.</summary>
    public static string Add { get; } = Glyph(0xE710);

    /// <summary>Monter.</summary>
    public static string MoveUp { get; } = Glyph(0xE74A);

    /// <summary>Descendre.</summary>
    public static string MoveDown { get; } = Glyph(0xE74B);

    /// <summary>Décaler vers la droite : un niveau de plus.</summary>
    public static string IndentMore { get; } = Glyph(0xE72A);

    private static string Glyph(int code) => ((char)code).ToString();
}
