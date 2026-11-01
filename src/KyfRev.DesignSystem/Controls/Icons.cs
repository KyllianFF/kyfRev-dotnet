namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Icônes du design system : caractères de la police d'icônes de Windows (Segoe Fluent Icons,
/// Segoe MDL2 Assets à défaut), désignés par leur code.
/// </summary>
public static class Icons
{
    /// <summary>Document : une fiche.</summary>
    public static string Document { get; } = Glyph(0xE8A5);

    /// <summary>Maison : l'accueil.</summary>
    public static string Home { get; } = Glyph(0xE80F);

    /// <summary>Dossier ouvert : ouvrir un coffre existant.</summary>
    public static string FolderOpen { get; } = Glyph(0xE838);

    /// <summary>Nouveau dossier : créer un coffre.</summary>
    public static string NewFolder { get; } = Glyph(0xE8F4);

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

    /// <summary>Icône d'une sorte de pastille.</summary>
    public static string Of(PillKind kind) => kind switch
    {
        PillKind.Sheet => Document,
        PillKind.Passage => Highlighter,
        PillKind.Reminder => Sync,
        PillKind.Overdue => Clock,
        PillKind.Archived => Archive,
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

    private static string Glyph(int code) => ((char)code).ToString();
}
