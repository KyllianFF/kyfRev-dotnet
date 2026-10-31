using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace KyfRev.Domain.Reviews;

/// <summary>Nature d'une séquence des J refusée.</summary>
public enum SequenceErrorCode
{
    /// <summary>Moins de deux étapes.</summary>
    TooShort,

    /// <summary>La première étape n'est pas J0.</summary>
    MustStartWithZero,

    /// <summary>Une étape n'est pas strictement supérieure à la précédente.</summary>
    NotIncreasing,

    /// <summary>Une étape dépasse <see cref="ReviewSequence.MaxValue"/>.</summary>
    ValueTooLarge,
}

/// <summary>Séquence des J refusée, avec un message destiné à l'utilisateur.</summary>
public sealed record SequenceError(SequenceErrorCode Code, string Message)
{
    internal static SequenceError TooShort() =>
        new(SequenceErrorCode.TooShort, "La séquence doit compter au moins deux étapes.");

    internal static SequenceError MustStartWithZero() =>
        new(SequenceErrorCode.MustStartWithZero, "La séquence doit commencer par J0.");

    internal static SequenceError NotIncreasing(int position) =>
        new(
            SequenceErrorCode.NotIncreasing,
            string.Create(CultureInfo.InvariantCulture, $"Les étapes doivent être strictement croissantes (étape {position}).")
        );

    internal static SequenceError ValueTooLarge(int max) =>
        new(
            SequenceErrorCode.ValueTooLarge,
            string.Create(CultureInfo.InvariantCulture, $"Une étape ne peut pas dépasser J{max}.")
        );
}

/// <summary>Résultat de <see cref="ReviewSequence.Create"/>.</summary>
public sealed record SequenceCreation
{
    private SequenceCreation(ReviewSequence? sequence, SequenceError? error)
    {
        Sequence = sequence;
        Error = error;
    }

    /// <summary>Vrai si la séquence a été acceptée.</summary>
    [MemberNotNullWhen(true, nameof(Sequence))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Sequence is not null;

    /// <summary>Séquence créée, en cas de succès.</summary>
    public ReviewSequence? Sequence { get; }

    /// <summary>Raison du refus, en cas d'échec.</summary>
    public SequenceError? Error { get; }

    internal static SequenceCreation Success(ReviewSequence sequence) => new(sequence, null);

    internal static SequenceCreation Failure(SequenceError error) => new(null, error);
}
