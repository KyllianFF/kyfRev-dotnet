namespace KyfRev.Application.Ports;

/// <summary>
/// Windows Hello sur ce poste (rangée 29C) : une clé propre à kyfRev, gardée par Windows, qui scelle un secret sans rien
/// demander et ne le rend qu'avec le code PIN, le visage ou l'empreinte de l'utilisateur. Un programme de la session ne
/// peut pas le rouvrir seul.
/// </summary>
public interface IWindowsHello
{
    /// <summary>Vrai si Windows Hello est configuré pour l'utilisateur sur ce poste.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Scelle <paramref name="secret"/> avec la clé Windows Hello de kyfRev, créée la première fois (Windows demande alors
    /// à l'utilisateur de confirmer que c'est lui) ; rien si la création a été annulée.
    /// </summary>
    /// <exception cref="OperationException">Windows Hello refuse : la raison se dit.</exception>
    byte[]? Seal(ReadOnlySpan<byte> secret);

    /// <summary>
    /// Rouvre un secret scellé : Windows demande le code PIN, le visage ou l'empreinte, avec <paramref name="message"/>.
    /// Rien si l'utilisateur annule.
    /// </summary>
    /// <exception cref="OperationException">La clé n'existe plus, ou Windows Hello refuse : la raison se dit.</exception>
    byte[]? Open(byte[] sealedSecret, string message);
}

/// <summary>Sans Windows Hello : rien ne se scelle, et la clé retenue reste celle de la session.</summary>
public sealed class NoWindowsHello : IWindowsHello
{
    /// <summary>L'instance unique.</summary>
    public static NoWindowsHello Instance { get; } = new();

    private NoWindowsHello()
    {
    }

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    public byte[]? Seal(ReadOnlySpan<byte> secret) => null;

    /// <inheritdoc/>
    public byte[]? Open(byte[] sealedSecret, string message) => null;
}
