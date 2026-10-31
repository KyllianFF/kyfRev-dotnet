using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;

namespace KyfRev.Domain.Tests.Support;

/// <summary>Raccourcis de dates et de résultats pour des tests lisibles.</summary>
internal static class Dates
{
    public static DateOnly D(int year, int month, int day) => new(year, month, day);

    public static DateOnly Iso(string day) => DateOnly.ParseExact(day, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Le jour donné, à midi en temps universel.</summary>
    public static Moment At(DateOnly day) => new(day, new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

    public static Tracking Ok(this TrackingResult result) =>
        result.IsSuccess ? result.Tracking : throw new AssertFailedException($"Opération refusée : {result.Error.Message}");

    public static Tracking Ok(this ReviewResult result) =>
        result.IsSuccess ? result.Tracking : throw new AssertFailedException($"Validation refusée : {result.Error.Message}");

    public static TrackingError Refused(this TrackingResult result) =>
        result.IsSuccess ? throw new AssertFailedException("L'opération aurait dû être refusée.") : result.Error;

    public static TrackingError Refused(this ReviewResult result) =>
        result.IsSuccess ? throw new AssertFailedException("La validation aurait dû être refusée.") : result.Error;

    public static DailyDraw Ok(this DrawResult result) =>
        result.IsSuccess ? result.Draw : throw new AssertFailedException($"Tirage refusé : {result.Error.Message}");

    /// <summary>Crée une fiche le jour donné avec la séquence par défaut (J0 = création).</summary>
    public static Tracking SheetCreatedOn(DateOnly day) =>
        Tracking.Start(At(day), ReviewSequence.Default, StartOrigin.Creation).Ok();

    public static DateOnly DueDate(this Tracking tracking) =>
        tracking.Cycle?.DueDate ?? throw new AssertFailedException("Élément archivé : aucune échéance.");

    /// <summary>Valide l'étape attendue le jour même de son échéance.</summary>
    public static Tracking ReviewedOnTime(this Tracking tracking, ReminderRules? rules) =>
        tracking.ValidateReview(At(tracking.DueDate()), reviewDate: null, rules).Ok();
}
