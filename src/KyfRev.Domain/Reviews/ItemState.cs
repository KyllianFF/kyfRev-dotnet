namespace KyfRev.Domain.Reviews;

/// <summary>
/// État d'un élément révisable. Un élément actif a toujours un cycle, un élément archivé
/// jamais : les états incohérents ne sont pas représentables.
/// </summary>
public abstract record ItemState
{
    private protected ItemState()
    {
    }
}

/// <summary>Élément en révision active.</summary>
public sealed record ActiveState(Cycle Cycle) : ItemState;

/// <summary>Élément archivé.</summary>
public sealed record ArchivedState(ArchiveInfo Archive) : ItemState;

/// <summary>Statut d'un élément révisable.</summary>
public enum TrackingStatus
{
    /// <summary>En révision active.</summary>
    Active,

    /// <summary>Archivé.</summary>
    Archived,
}

/// <summary>Raison de l'archivage d'un élément.</summary>
public enum ArchiveReason
{
    /// <summary>La dernière étape du cycle a été validée.</summary>
    EndOfCycle,

    /// <summary>Archivage manuel avant la fin du cycle.</summary>
    Manual,
}

/// <summary>Informations d'un élément archivé.</summary>
/// <param name="ArchivedOn">Jour de l'archivage.</param>
/// <param name="Reason">Fin de cycle ou archivage manuel.</param>
/// <param name="IncludedInReminders">Vrai si l'élément peut recevoir des rappels de consolidation.</param>
/// <param name="RestLevel">Index dans la séquence de repos (2, 4, 8, 12 mois par défaut).</param>
/// <param name="EligibleOn">Date à partir de laquelle un rappel de consolidation peut être tiré.</param>
/// <param name="ReminderDrawnOn">Date du tirage, tant que le rappel n'a pas été traité.</param>
public sealed record ArchiveInfo(
    DateOnly ArchivedOn,
    ArchiveReason Reason,
    bool IncludedInReminders,
    int RestLevel,
    DateOnly EligibleOn,
    DateOnly? ReminderDrawnOn);
