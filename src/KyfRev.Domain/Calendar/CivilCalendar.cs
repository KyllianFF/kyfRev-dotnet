namespace KyfRev.Domain.Calendar;

/// <summary>
/// Arithmétique de dates civiles, sans heure ni fuseau : le passage à l'heure d'été ou
/// d'hiver n'a aucun effet sur les échéances.
/// </summary>
public static class CivilCalendar
{
    private const int MonthsPerYear = 12;

    /// <summary>Ajoute <paramref name="days"/> jours (négatif pour reculer).</summary>
    /// <returns><see langword="false"/> si le résultat sort du calendrier.</returns>
    public static bool TryAddDays(DateOnly day, long days, out DateOnly result)
    {
        var dayNumber = day.DayNumber + days;
        if (dayNumber < DateOnly.MinValue.DayNumber || dayNumber > DateOnly.MaxValue.DayNumber)
        {
            result = default;
            return false;
        }
        result = DateOnly.FromDayNumber((int)dayNumber);
        return true;
    }

    /// <summary>
    /// Ajoute <paramref name="months"/> mois calendaires (décision D5). Le jour du mois est
    /// conservé ; s'il n'existe pas dans le mois d'arrivée, on prend le dernier jour de ce mois
    /// (31 décembre + 2 mois = 28 février, ou 29 les années bissextiles).
    /// </summary>
    /// <returns><see langword="false"/> si le résultat sort du calendrier.</returns>
    public static bool TryAddMonths(DateOnly day, long months, out DateOnly result)
    {
        var monthIndex = (day.Year * (long)MonthsPerYear) + (day.Month - 1) + months;
        var year = Math.DivRem(monthIndex, MonthsPerYear, out var monthOffset);
        if (year < DateOnly.MinValue.Year || year > DateOnly.MaxValue.Year)
        {
            result = default;
            return false;
        }
        var month = (int)monthOffset + 1;
        var dayOfMonth = Math.Min(day.Day, DateTime.DaysInMonth((int)year, month));
        result = new DateOnly((int)year, month, dayOfMonth);
        return true;
    }

    /// <summary>
    /// Nombre de jours de <paramref name="start"/> à <paramref name="end"/>, négatif si
    /// <paramref name="end"/> précède <paramref name="start"/>.
    /// </summary>
    public static int DaysBetween(DateOnly start, DateOnly end) => end.DayNumber - start.DayNumber;
}
