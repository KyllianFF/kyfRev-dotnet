using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace KyfRev.Domain.Reviews;

/// <summary>
/// Séquence des J, par exemple J0, J1, J3, J7, J14, J30. La valeur n de l'étape « Jn » est
/// le nombre de jours qui la séparent de la révision précédente réellement validée (D7).
/// </summary>
public sealed class ReviewSequence : IEquatable<ReviewSequence>
{
    /// <summary>Valeur maximale d'une étape : dix ans.</summary>
    public const int MaxValue = 3650;

    private ReviewSequence(ImmutableArray<int> values)
    {
        Values = values;
    }

    /// <summary>J0 → J1 → J3 → J7 → J14 → J30.</summary>
    public static ReviewSequence Default { get; } = new([0, 1, 3, 7, 14, 30]);

    /// <summary>Valeurs des étapes, dans l'ordre.</summary>
    public ImmutableArray<int> Values { get; }

    /// <summary>Index de la dernière étape (J30 dans la séquence par défaut).</summary>
    public int LastStep => Values.Length - 1;

    /// <summary>Durée d'un cycle complet en étant ponctuel : 55 jours par défaut.</summary>
    public int TotalDurationDays => Values.Skip(1).Sum();

    /// <summary>
    /// Vérifie et crée une séquence : au moins deux étapes, commençant par J0, strictement
    /// croissantes, aucune au-delà de <see cref="MaxValue"/>.
    /// </summary>
    public static SequenceCreation Create(IEnumerable<int> values)
    {
        var steps = values.ToImmutableArray();
        if (steps.Length < 2)
        {
            return SequenceCreation.Failure(SequenceError.TooShort());
        }
        if (steps[0] != 0)
        {
            return SequenceCreation.Failure(SequenceError.MustStartWithZero());
        }
        for (var position = 1; position < steps.Length; position++)
        {
            if (steps[position] <= steps[position - 1])
            {
                return SequenceCreation.Failure(SequenceError.NotIncreasing(position));
            }
        }
        if (steps.Any(value => value > MaxValue))
        {
            return SequenceCreation.Failure(SequenceError.ValueTooLarge(MaxValue));
        }
        return SequenceCreation.Success(new ReviewSequence(steps));
    }

    /// <summary>« J7 » pour l'étape d'index 3 de la séquence par défaut.</summary>
    /// <exception cref="ArgumentOutOfRangeException">L'étape n'existe pas.</exception>
    public string Label(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(step, LastStep);
        return string.Create(CultureInfo.InvariantCulture, $"J{Values[step]}");
    }

    /// <summary>
    /// Nombre de jours entre la révision précédente et l'étape <paramref name="step"/> (D7).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// L'étape est J0, qui ouvre le cycle sans intervalle, ou n'existe pas.
    /// </exception>
    public int IntervalBefore(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(step, LastStep);
        return Values[step];
    }

    /// <inheritdoc/>
    public bool Equals([NotNullWhen(true)] ReviewSequence? other) =>
        other is not null && Values.SequenceEqual(other.Values);

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as ReviewSequence);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in Values)
        {
            hash.Add(value);
        }
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() => string.Join(", ", Values.Select((_, step) => Label(step)));
}
