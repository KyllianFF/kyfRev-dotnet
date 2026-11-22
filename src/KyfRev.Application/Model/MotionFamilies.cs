namespace KyfRev.Application.Model;

/// <summary>
/// Les familles d'animations de l'interface, que l'on peut couper une à une dans les Paramètres. Toute animation de kyfRev
/// appartient à l'une d'elles.
/// </summary>
[Flags]
public enum MotionFamilies
{
    /// <summary>Aucune famille.</summary>
    None = 0,

    /// <summary>Le retour au toucher : survol et appui des boutons, coches, interrupteurs.</summary>
    Feedback = 1,

    /// <summary>La sélection qui glisse : menu, onglets, sections des Paramètres, modes d'une fiche.</summary>
    Selection = 2,

    /// <summary>Les changements de vue et de mode : page qui arrive, Lire, Réviser, Écrire, ruban, feuille de l'éditeur.</summary>
    Views = 4,

    /// <summary>Le menu et les volets : menu réduit ou élargi, volets d'une fiche, barres qui glissent.</summary>
    Panels = 8,

    /// <summary>Les menus, les fenêtres de dialogue et les avis.</summary>
    Surfaces = 16,

    /// <summary>Les listes : une ligne qui arrive.</summary>
    Lists = 32,

    /// <summary>L'animation d'ouverture, au lancement.</summary>
    Opening = 64,

    /// <summary>Tout ce qui n'est pas essentiel : il reste le retour au toucher et la sélection qui glisse.</summary>
    BeyondEssential = Views | Panels | Surfaces | Lists | Opening,

    /// <summary>Toutes les familles.</summary>
    All = Feedback | Selection | BeyondEssential,
}
