using KyfRev.Domain.Calendar;

namespace KyfRev.Application;

/// <summary>
/// Le présent selon l'horloge injectée : le jour local (toutes les échéances), l'instant exact
/// (l'historique) et l'heure locale (le nom des sauvegardes, la date des exports). Il n'y a pas
/// d'autre source de date dans l'application.
/// </summary>
/// <param name="Now">Jour local et instant exact.</param>
/// <param name="Local">Date et heure locales.</param>
public readonly record struct Present(Moment Now, DateTime Local)
{
    /// <summary>Le présent selon <paramref name="time"/>, dans son fuseau local.</summary>
    public static Present Of(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var local = time.GetLocalNow();
        return new Present(new Moment(DateOnly.FromDateTime(local.DateTime), local), local.DateTime);
    }
}
