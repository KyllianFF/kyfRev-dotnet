namespace KyfRev.DesignSystem;

/// <summary>
/// Les familles d'animations de l'interface. Toute animation appartient à l'une d'elles, et chaque famille se coupe à part
/// (<see cref="DesignResources.MotionOff"/>) : ses transitions n'ont alors pas lieu, les autres gardent leurs durées.
/// </summary>
[Flags]
public enum MotionKinds
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

    /// <summary>Toutes les familles.</summary>
    All = Feedback | Selection | Views | Panels | Surfaces | Lists | Opening,
}
