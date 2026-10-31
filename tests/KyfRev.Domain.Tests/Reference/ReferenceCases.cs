using KyfRev.Domain.Reviews;

namespace KyfRev.Domain.Tests.Reference;

/// <summary>
/// Constantes partagées avec <c>tools/reference-vectors/src/main.rs</c> et lecture de ses fichiers.
/// </summary>
internal static class ReferenceCases
{
    public const ulong TrackingBase = 0;
    public const ulong DrawBase = 1_000_000;
    public const ulong ChaChaBase = 2_000_000;
    public const ulong SeedBase = 3_000_000;
    public const ulong CalendarBase = 4_000_000;

    public const string Crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static readonly int[][] Sequences = [[0, 1, 3, 7, 14, 30], [0, 1], [0, 2, 5, 9], [0, 1, 3, 7, 14, 30, 60]];

    public static readonly StartOrigin[] Origins =
        [StartOrigin.Creation, StartOrigin.Adoption, StartOrigin.Reminder, StartOrigin.Restart];

    public static readonly ModifiedObject[] ModifiedObjects =
        [ModifiedObject.Comment, ModifiedObject.Passage, ModifiedObject.Definition];

    public static ReviewSequence Sequence(int index)
    {
        var creation = ReviewSequence.Create(Sequences[index]);
        return creation.IsSuccess ? creation.Sequence : throw new AssertFailedException(creation.Error.Message);
    }

    public static ReminderRules Rules(int index) => index switch
    {
        0 => ReminderRules.Default,
        1 => new ReminderRules(1, [1, 3], 1, 10, IncludeManualArchiving: false),
        _ => new ReminderRules(3, [2, 4, 8, 12, 24], 3, 45, IncludeManualArchiving: true),
    };

    /// <summary>Lignes utiles d'un fichier de référence (sans les commentaires d'en-tête).</summary>
    public static IReadOnlyList<string> Lines(string file) =>
        [.. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "reference", file)).Where(line => !line.StartsWith('#'))];
}
