namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Icônes du design system : caractères de la police d'icônes de Windows (Segoe Fluent Icons,
/// Segoe MDL2 Assets à défaut), désignés par leur code.
/// </summary>
public static class Icons
{
    /// <summary>Document : une fiche.</summary>
    public static string Document { get; } = Glyph(0xE8A5);

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
