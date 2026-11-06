namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Ce que demandent les colonnes d'un tableau pleine largeur : leur plus long mot (<paramref name="Minimum"/>) et leur plus
/// longue case sur une ligne (<paramref name="Natural"/>). La vue en tire leurs largeurs pour la largeur réelle du texte.
/// </summary>
/// <param name="Minimum">Pour chaque colonne, la largeur de son plus long mot, marges comprises.</param>
/// <param name="Natural">Pour chaque colonne, la largeur de sa plus longue case sur une seule ligne, marges comprises.</param>
internal sealed record TableSizing(IReadOnlyList<double> Minimum, IReadOnlyList<double> Natural)
{
    /// <summary>
    /// Les largeurs des colonnes pour <paramref name="available"/> pixels, comme les tableaux d'une page web. Avec assez de place,
    /// chacune grandit en proportion de ce qu'elle demande ; sinon chacune garde son plus long mot, et le reste se partage selon
    /// ce que chacune demande en plus. Quand même les mots ne tiennent plus, ils se partagent la place à proportion.
    /// </summary>
    public IReadOnlyList<double> For(double available)
    {
        var least = Minimum.Sum();
        var most = Natural.Sum();
        if (available >= most || most <= least)
        {
            return Natural;
        }
        if (available <= least)
        {
            return Minimum;
        }
        var extra = (available - least) / (most - least);
        return [.. Minimum.Select((width, column) => width + ((Natural[column] - width) * extra))];
    }
}
