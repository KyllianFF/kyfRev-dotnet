namespace KyfRev.Application;

/// <summary>Issue d'un cas d'usage sans valeur à rendre.</summary>
public readonly record struct Done;

/// <summary>
/// Issue d'un cas d'usage : sa valeur, ou le refus, avec un message en français prêt à afficher
/// (celui de la version 0.x). Une erreur de programmation n'est jamais transformée en refus.
/// </summary>
/// <typeparam name="T">Valeur rendue en cas de réussite.</typeparam>
public sealed class Outcome<T>
{
    private readonly T? _value;

    internal Outcome(T value)
    {
        _value = value;
    }

    internal Outcome(string error)
    {
        Error = error;
    }

    /// <summary>Le cas d'usage a abouti.</summary>
    public bool Succeeded => Error is null;

    /// <summary>Raison du refus ; rien en cas de réussite.</summary>
    public string? Error { get; }

    /// <summary>Valeur rendue.</summary>
    /// <exception cref="InvalidOperationException">Le cas d'usage a été refusé.</exception>
    public T Value => Succeeded ? _value! : throw new InvalidOperationException(Error);
}

/// <summary>Fabrique des issues de cas d'usage.</summary>
public static class Outcome
{
    /// <summary>Réussite, avec sa valeur.</summary>
    public static Outcome<T> Of<T>(T value) => new(value);

    /// <summary>Réussite sans valeur.</summary>
    public static Outcome<Done> Done() => new(default(Done));

    /// <summary>Refus, avec son message.</summary>
    public static Outcome<T> Refused<T>(string error)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);
        return new Outcome<T>(error);
    }
}
