namespace KyfRev.DesignSystem.Tokens;

/// <summary>
/// Rôle d'une couleur. Les composants n'emploient que des rôles ; chaque thème leur donne une
/// valeur (<see cref="ThemeColors"/>), et un test vérifie le contraste de chaque paire employée
/// (<see cref="ContrastRequirements"/>).
/// </summary>
public enum ColorRole
{
    /// <summary>Fond de la fenêtre.</summary>
    Background,

    /// <summary>Fond des cartes, des champs et des menus.</summary>
    Surface,

    /// <summary>Fond secondaire : survol, blocs de code, squelettes de chargement.</summary>
    SurfaceAlt,

    /// <summary>Séparateurs décoratifs : jamais le seul repère d'un contrôle.</summary>
    Divider,

    /// <summary>Contour d'un contrôle (champ, bouton secondaire, case) : au moins 3:1.</summary>
    ControlBorder,

    /// <summary>Texte.</summary>
    Text,

    /// <summary>Texte secondaire (détails, légendes).</summary>
    TextSecondary,

    /// <summary>Bouton principal, onglet actif.</summary>
    Primary,

    /// <summary>Bouton principal survolé.</summary>
    PrimaryHover,

    /// <summary>Bouton principal enfoncé.</summary>
    PrimaryPressed,

    /// <summary>Texte et icône sur la couleur principale.</summary>
    OnPrimary,

    /// <summary>Liens.</summary>
    Link,

    /// <summary>Anneau de focus (2 px, décalé de 2 px).</summary>
    Focus,

    /// <summary>Repère latéral d'une fiche.</summary>
    MarkerSheet,

    /// <summary>Repère latéral d'un passage annoté.</summary>
    MarkerPassage,

    /// <summary>Repère latéral d'un rappel de consolidation.</summary>
    MarkerReminder,

    /// <summary>Fond de la pastille « Fiche ».</summary>
    PillSheetBackground,

    /// <summary>Texte de la pastille « Fiche ».</summary>
    PillSheetText,

    /// <summary>Fond de la pastille « Passage ».</summary>
    PillPassageBackground,

    /// <summary>Texte de la pastille « Passage ».</summary>
    PillPassageText,

    /// <summary>Fond de la pastille « Rappel ».</summary>
    PillReminderBackground,

    /// <summary>Texte de la pastille « Rappel ».</summary>
    PillReminderText,

    /// <summary>Sélection de texte en cours (vert pâle).</summary>
    Selection,

    /// <summary>Texte sélectionné.</summary>
    OnSelection,

    /// <summary>Surlignage enregistré (ambre) : distinct de la sélection.</summary>
    Highlight,

    /// <summary>Surlignage de l'annotation ouverte.</summary>
    HighlightActive,

    /// <summary>Texte surligné.</summary>
    OnHighlight,

    /// <summary>Fond de l'entrée active du menu latéral.</summary>
    NavigationActiveBackground,

    /// <summary>Texte de l'entrée active du menu latéral.</summary>
    NavigationActiveText,

    /// <summary>Erreur : brique, hors des deux gammes, pour ne jamais se confondre avec un retard ou une action.</summary>
    Critical,

    /// <summary>Fond des erreurs.</summary>
    CriticalBackground,

    /// <summary>Retard et avertissement (annotation à vérifier) : ambre.</summary>
    Warning,

    /// <summary>Fond des retards et des avertissements.</summary>
    WarningBackground,

    /// <summary>Réussite.</summary>
    Success,

    /// <summary>Fond des réussites.</summary>
    SuccessBackground,

    /// <summary>Information.</summary>
    Info,

    /// <summary>Fond des informations.</summary>
    InfoBackground,

    /// <summary>Mot réservé d'un bloc de code.</summary>
    CodeKeyword,

    /// <summary>Texte entre guillemets d'un bloc de code.</summary>
    CodeString,

    /// <summary>Nombre d'un bloc de code.</summary>
    CodeNumber,

    /// <summary>Nom de fonction ou de balise d'un bloc de code.</summary>
    CodeTitle,

    /// <summary>Voile sous les dialogues (transparent, décoratif).</summary>
    Scrim,
}
