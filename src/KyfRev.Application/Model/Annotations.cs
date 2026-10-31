namespace KyfRev.Application.Model;

/// <summary>État d'ancrage d'une annotation.</summary>
public enum AnchorState
{
    /// <summary>Position retrouvée sans ambiguïté.</summary>
    Anchored,

    /// <summary>Position incertaine : l'utilisateur doit confirmer.</summary>
    NeedsReview,

    /// <summary>Passage introuvable : signalé, jamais rattaché ailleurs en silence.</summary>
    NotFound,
}

/// <summary>Une fiche ou un passage annoté : les deux ont leur propre calendrier des J.</summary>
public enum ItemKind
{
    /// <summary>Une fiche.</summary>
    Sheet,

    /// <summary>Un passage annoté ou une définition.</summary>
    Passage,
}
