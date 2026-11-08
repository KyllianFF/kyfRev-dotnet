namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Une seule recherche à la fois : la dernière demandée. Elle part une fois la frappe arrêtée un instant, hors du fil de
/// l'interface ; une nouvelle demande arrête la précédente, dont la réponse n'est jamais montrée.
/// </summary>
internal sealed class LatestSearch
{
    private CancellationTokenSource? _current;

    /// <summary>Une recherche est en cours, ou attend la fin de sa pause : sa réponse n'est pas encore arrivée.</summary>
    public bool IsRunning => _current is not null;

    /// <summary>Arrête la recherche en cours : sa réponse ne sera pas montrée.</summary>
    public void Cancel()
    {
        _current?.Cancel();
        _current?.Dispose();
        _current = null;
    }

    /// <summary>Arrête la recherche en cours, puis lance celle-ci.</summary>
    /// <param name="pause">Le temps d'attendre la frappe suivante.</param>
    /// <param name="search">La recherche, sur un autre fil : elle s'arrête quand on le lui dit.</param>
    /// <param name="show">Ce qu'on fait de sa réponse, sur le fil de l'appelant, si aucune autre ne l'a remplacée.</param>
    public async Task RunAsync<T>(TimeSpan pause, Func<CancellationToken, T> search, Action<T> show)
    {
        Cancel();
        var cancellation = new CancellationTokenSource();
        _current = cancellation;
        var token = cancellation.Token;
        if (pause > TimeSpan.Zero)
        {
            await Task.Delay(pause, CancellationToken.None).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }
        }
        var found = await Task.Run(() => search(token), CancellationToken.None).ConfigureAwait(true);
        if (!token.IsCancellationRequested)
        {
            // Elle a répondu : plus rien n'est en cours.
            Cancel();
            show(found);
        }
    }
}
