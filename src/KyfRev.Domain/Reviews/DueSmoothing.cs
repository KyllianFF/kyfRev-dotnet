using KyfRev.Domain.Calendar;

namespace KyfRev.Domain.Reviews;

/// <summary>
/// Lissage de la prochaine échéance, en option : plutôt que de tomber exactement n jours après la révision, une étape
/// longue peut se poser sur un jour voisin moins chargé. Dix fiches créées après un même cours ne reviennent alors plus
/// toutes le même jour. Sans l'option, la règle D7 tient exactement.
/// </summary>
public static class DueSmoothing
{
    /// <summary>
    /// De combien de jours une étape peut s'écarter de son échéance exacte : rien avant J7, un jour à partir de J7, deux à
    /// partir de J14, trois à partir de J30. Les étapes courtes ne bougent pas : un jour d'écart y pèserait trop.
    /// </summary>
    public static int Window(int interval) => interval switch
    {
        >= 30 => 3,
        >= 14 => 2,
        >= 7 => 1,
        _ => 0,
    };

    /// <summary>
    /// Le jour le moins chargé autour de l'échéance exacte ; à charge égale, le plus proche d'elle, puis le plus tôt. Le
    /// calcul ne dépend que de ses entrées.
    /// </summary>
    /// <param name="exact">L'échéance exacte : la révision plus l'intervalle.</param>
    /// <param name="interval">Jours de l'étape : 7 pour un J7.</param>
    /// <param name="load">Ce qui tombe déjà un jour donné.</param>
    public static DateOnly Choose(DateOnly exact, int interval, Func<DateOnly, int> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        var best = exact;
        var bestLoad = load(exact);
        for (var distance = 1; distance <= Window(interval); distance++)
        {
            // Le plus tôt d'abord : à charge et distance égales, il l'emporte.
            foreach (var offset in (ReadOnlySpan<int>)[-distance, distance])
            {
                if (CivilCalendar.TryAddDays(exact, offset, out var candidate) && load(candidate) < bestLoad)
                {
                    (best, bestLoad) = (candidate, load(candidate));
                }
            }
        }
        return best;
    }
}
