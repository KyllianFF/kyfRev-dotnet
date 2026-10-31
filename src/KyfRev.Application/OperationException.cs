namespace KyfRev.Application;

/// <summary>
/// Opération refusée par un port (coffre, disque…), avec un message en français prêt à afficher.
/// Les cas d'usage la changent en <see cref="Outcome{T}"/> refusé ; toute autre exception est
/// une erreur de programmation et remonte telle quelle.
/// </summary>
public abstract class OperationException : Exception
{
    /// <summary>Refus sans précision.</summary>
    protected OperationException()
    {
    }

    /// <summary>Refus avec le message donné.</summary>
    protected OperationException(string message)
        : base(message)
    {
    }

    /// <summary>Refus avec le message donné et sa cause.</summary>
    protected OperationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
