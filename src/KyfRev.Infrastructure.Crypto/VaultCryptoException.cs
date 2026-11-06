using KyfRev.Application;

namespace KyfRev.Infrastructure.Crypto;

/// <summary>
/// Un déchiffrement refusé : mauvais mot de passe, mauvaise clé, fichier altéré, échangé ou illisible. Rien n'est écrit
/// quand elle est levée, et son message ne dit jamais rien du contenu.
/// </summary>
public sealed class VaultCryptoException : OperationException
{
    /// <summary>Refus sans précision.</summary>
    public VaultCryptoException()
        : base("Déchiffrement refusé.")
    {
    }

    /// <summary>Refus avec le message donné.</summary>
    public VaultCryptoException(string message)
        : base(message)
    {
    }

    /// <summary>Refus avec le message et la cause donnés.</summary>
    public VaultCryptoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
