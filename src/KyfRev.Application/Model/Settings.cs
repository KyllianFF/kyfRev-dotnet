using System.Collections.Immutable;

namespace KyfRev.Application.Model;

/// <summary>Réglages modifiables depuis l'écran Paramètres.</summary>
/// <param name="Steps">Étapes après J0, en jours (« 1, 3, 7, 14, 30 »).</param>
/// <param name="ActiveRecall">Rappel actif pour les passages.</param>
/// <param name="MaxPerDay">Rappels de consolidation en attente au plus.</param>
/// <param name="MaxWaitDays">Attente au-delà de laquelle une fiche éligible passe en priorité.</param>
/// <param name="RestSequenceMonths">Repos successifs entre deux rappels, en mois.</param>
/// <param name="IncludeManualArchiving">Les fiches archivées à la main reviennent aussi en rappel.</param>
/// <param name="BackupRotation">Rotation des sauvegardes (14 j / 8 sem. / 24 mois).</param>
/// <param name="QuotesActive">Module Citations.</param>
/// <param name="QuotesOnline">Citation du jour cherchée sur Wikiquote.</param>
/// <param name="QuoteDomains">Domaines de la citation en ligne.</param>
/// <param name="SchemasActive">Module Schémas réseau.</param>
/// <param name="LockAfterMinutes">
/// Un coffre chiffré se verrouille après ces minutes d'inactivité ; 0 : jamais. Un coffre en clair le garde sans s'en servir.
/// </param>
public sealed record VaultPreferences(
    ImmutableArray<int> Steps,
    bool ActiveRecall,
    int MaxPerDay,
    int MaxWaitDays,
    ImmutableArray<int> RestSequenceMonths,
    bool IncludeManualArchiving,
    bool BackupRotation,
    bool QuotesActive,
    bool QuotesOnline,
    ImmutableArray<string> QuoteDomains,
    bool SchemasActive,
    int LockAfterMinutes = VaultPreferences.DefaultLockAfterMinutes)
{
    /// <summary>
    /// Lissage des échéances : à partir de J7, la prochaine étape peut se poser un à trois jours avant ou après son échéance
    /// exacte, sur le jour le moins chargé. Coupé par défaut : l'écart entre deux révisions vaut alors exactement le J.
    /// </summary>
    public bool SmoothDueDates { get; init; }

    /// <summary>Délai du verrouillage automatique par défaut : 15 minutes d'inactivité.</summary>
    public const int DefaultLockAfterMinutes = 15;

    /// <summary>Délais proposés pour le verrouillage automatique, en minutes, dans l'ordre de l'écran ; 0 : jamais.</summary>
    public static readonly ImmutableArray<int> LockAfterChoices = [5, 15, 30, 60, 0];
}
