using System.Collections.Immutable;

namespace KyfRev.Infrastructure.Vault.Analysis;

/// <summary>Gravité d'un constat.</summary>
public enum FindingSeverity
{
    /// <summary>Situation normale ou corrigée d'elle-même à la prochaine ouverture.</summary>
    Information,

    /// <summary>Situation à examiner, sans risque pour les données.</summary>
    Warning,

    /// <summary>Fichier illisible, en double ou d'un format plus récent : jamais réécrit.</summary>
    Error,
}

/// <summary>Constat sur un fichier ou une fiche du coffre.</summary>
/// <param name="Severity">Gravité.</param>
/// <param name="Code">Code stable, pour les traitements automatiques.</param>
/// <param name="Path">Chemin relatif au coffre, s'il s'agit d'un fichier.</param>
/// <param name="Message">Explication destinée à l'utilisateur.</param>
public sealed record Finding(FindingSeverity Severity, string Code, string? Path, string Message);

/// <summary>Comptes du coffre.</summary>
public sealed record VaultSummary
{
    /// <summary>Identifiant du coffre.</summary>
    public string? VaultId { get; init; }

    /// <summary>Date de création du coffre.</summary>
    public DateOnly? CreatedOn { get; init; }

    /// <summary>Fiches suivies et présentes.</summary>
    public int TrackedSheets { get; init; }

    /// <summary>Fiches suivies en révision active.</summary>
    public int ActiveSheets { get; init; }

    /// <summary>Fiches suivies archivées.</summary>
    public int ArchivedSheets { get; init; }

    /// <summary>Fichiers Markdown sans identifiant.</summary>
    public int UntrackedFiles { get; init; }

    /// <summary>Passages annotés (définitions comprises), hors corbeille.</summary>
    public int Annotations { get; init; }

    /// <summary>Définitions, hors corbeille.</summary>
    public int Definitions { get; init; }

    /// <summary>Annotations à la corbeille.</summary>
    public int TrashedAnnotations { get; init; }

    /// <summary>Fiches à la corbeille.</summary>
    public int TrashedSheets { get; init; }

    /// <summary>Versions de fiches gardées.</summary>
    public int Versions { get; init; }

    /// <summary>Citations, hors corbeille.</summary>
    public int Quotes { get; init; }

    /// <summary>Schémas réseau.</summary>
    public int Schemas { get; init; }

    /// <summary>Fichiers de données d'extension.</summary>
    public int ExtensionData { get; init; }

    /// <summary>Fichiers lus.</summary>
    public int FilesRead { get; init; }
}

/// <summary>Résultat de l'analyse d'un coffre, faite en lecture seule.</summary>
public sealed record VaultInspection(string Root, bool IsVault, VaultSummary Summary, ImmutableArray<Finding> Findings)
{
    /// <summary>Vrai si au moins un constat est une erreur.</summary>
    public bool HasErrors => Findings.Any(f => f.Severity == FindingSeverity.Error);
}
