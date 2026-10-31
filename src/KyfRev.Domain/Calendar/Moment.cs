namespace KyfRev.Domain.Calendar;

/// <summary>
/// Le moment présent tel que l'application le voit.
/// </summary>
public readonly record struct Moment
{
    /// <summary>
    /// Crée un moment à partir de la date locale du jour et de l'horodatage exact.
    /// </summary>
    /// <param name="day">Date locale du jour, qui sert à tous les calculs d'échéance.</param>
    /// <param name="instant">Horodatage exact, ramené en temps universel.</param>
    public Moment(DateOnly day, DateTimeOffset instant)
    {
        Day = day;
        Instant = instant.ToUniversalTime();
    }

    /// <summary>Date locale du jour : toutes les échéances se calculent à partir d'elle.</summary>
    public DateOnly Day { get; }

    /// <summary>Horodatage exact, en temps universel, inscrit dans l'historique.</summary>
    public DateTimeOffset Instant { get; }
}
