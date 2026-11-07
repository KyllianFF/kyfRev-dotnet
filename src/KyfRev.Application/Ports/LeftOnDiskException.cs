namespace KyfRev.Application.Ports;

/// <summary>
/// Un effacement définitif (<see cref="VaultStorage.Purge"/>) a eu lieu, mais une part est restée sur le disque : un autre
/// programme la tenait ouverte. Ce qui devait être effacé a quitté le coffre ; ce qui reste n'est plus nommé, et aucune
/// sauvegarde ne le recopie. À la différence d'une autre <see cref="IOException"/>, où rien n'a changé, il n'y a rien à
/// refaire : les effacements suivants peuvent continuer.
/// </summary>
public sealed class LeftOnDiskException : IOException
{
    /// <summary>Une part sans précision est restée sur le disque.</summary>
    public LeftOnDiskException()
        : base("L'effacement a eu lieu, mais une part est restée sur le disque.")
    {
    }

    /// <summary>Avec le message donné, qui nomme ce qui reste.</summary>
    public LeftOnDiskException(string message)
        : base(message)
    {
    }

    /// <summary>Avec le message donné et sa cause.</summary>
    public LeftOnDiskException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
