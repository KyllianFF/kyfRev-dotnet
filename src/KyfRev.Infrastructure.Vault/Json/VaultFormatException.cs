namespace KyfRev.Infrastructure.Vault.Json;

/// <summary>
/// Fichier du coffre illisible : JSON invalide, champ manquant ou de type inattendu, valeur hors
/// limites, suivi incohérent. Levée par les lecteurs, rattrapée fichier par fichier : un fichier
/// illisible est signalé, jamais réécrit.
/// </summary>
public sealed class VaultFormatException : Exception
{
    /// <summary>Crée une exception sans message particulier.</summary>
    public VaultFormatException()
        : base("Fichier du coffre illisible.")
    {
    }

    /// <summary>Crée une exception avec la raison donnée.</summary>
    public VaultFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Crée une exception avec la raison donnée et sa cause.</summary>
    public VaultFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
