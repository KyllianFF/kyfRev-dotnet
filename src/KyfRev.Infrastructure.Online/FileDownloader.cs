using System.Collections.Frozen;
using System.Net;
using System.Net.Http.Headers;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Online;

/// <summary>
/// Le téléchargement d'un installateur publié : HTTPS seulement, sans cookie, sur les seuls hôtes des versions publiées de
/// kyfRev. La version publiée renvoie vers le stockage de GitHub : ces redirections-là sont suivies, aucune autre. La
/// taille est bornée, et le téléchargement s'arrête si plus rien n'arrive pendant un moment. Le fichier s'écrit à côté
/// (« .partiel »), et ne prend sa place qu'une fois tout reçu ; interrompu, ce début reste à côté, sans rien remplacer, et le
/// prochain téléchargement l'écrase. Rien n'est effacé ici ; la vérification du contenu revient à qui l'a demandé.
/// </summary>
public sealed class FileDownloader : IFileDownloader, IDisposable
{
    /// <summary>Les seuls hôtes d'où un fichier se télécharge : les versions publiées de GitHub et leur stockage.</summary>
    public static readonly FrozenSet<string> Hosts = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com");

    private const int MaxRedirections = 5;

    private static readonly TimeSpan DefaultStall = TimeSpan.FromSeconds(30);

    private readonly HttpMessageHandler _handler;
    private readonly HttpClient _client;
    private readonly TimeSpan _stall;

    /// <summary>Le téléchargement de l'application.</summary>
    /// <param name="version">Version de kyfRev, annoncée dans l'agent.</param>
    public FileDownloader(string version)
        : this(version, handler: null, DefaultStall)
    {
    }

    /// <summary>Téléchargement réglable, pour les tests (sans <paramref name="handler"/> : la connexion durcie).</summary>
    internal FileDownloader(string version, HttpMessageHandler? handler, TimeSpan stall)
    {
        ArgumentNullException.ThrowIfNull(version);
        _handler = handler ?? OnlineReader.Handler();
        _client = new HttpClient(_handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("kyfRev", version));
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(https://github.com/KyllianFF/kyfRev-dotnet)"));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        _stall = stall;
    }

    /// <inheritdoc/>
    public async Task DownloadAsync(Uri address, string destination, long maxBytes, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrEmpty(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        Allow(address);
        var partial = destination + ".partiel";
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            stall.CancelAfter(_stall);
            using var response = await FollowAsync(address, stall.Token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength > maxBytes)
            {
                throw new OnlineException($"Le fichier de {address.Host} est plus gros qu'annoncé.");
            }
            var source = await response.Content.ReadAsStreamAsync(stall.Token).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await using (target.ConfigureAwait(false))
                {
                    await CopyAsync(source, target, address, maxBytes, progress, stall).ConfigureAwait(false);
                }
            }
            File.Move(partial, destination, overwrite: true);
        }
        catch (Exception exception) when (exception is HttpRequestException or HttpIOException)
        {
            throw new OnlineException($"{address.Host} injoignable : {exception.Message}", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new OnlineException($"Le fichier n'a pas pu s'écrire : {exception.Message}", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OnlineException($"{address.Host} injoignable : délai dépassé.", exception);
        }
    }

    /// <summary>Ferme le client.</summary>
    public void Dispose() => _client.Dispose();

    /// <summary>Une adresse en HTTPS, sur un hôte des versions publiées ; sinon, refusée avant toute connexion.</summary>
    private static void Allow(Uri address)
    {
        if (!address.IsAbsoluteUri || address.Scheme != Uri.UriSchemeHttps)
        {
            throw new OnlineException($"Adresse refusée : « {address} » n'est pas en HTTPS.");
        }
        if (!Hosts.Contains(address.Host))
        {
            throw new OnlineException($"Adresse refusée : {address.Host} n'est pas un hôte des versions publiées de kyfRev.");
        }
    }

    /// <summary>La réponse au fichier, après les redirections permises.</summary>
    private async Task<HttpResponseMessage> FollowAsync(Uri address, CancellationToken cancellationToken)
    {
        var current = address;
        for (var hop = 0; ; hop++)
        {
            var response = await _client.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null || hop >= MaxRedirections)
                {
                    throw new OnlineException($"{current.Host} injoignable : redirection refusée.");
                }
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                Allow(current);
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                response.Dispose();
                throw new OnlineException(status == (int)HttpStatusCode.NotFound
                    ? $"{current.Host} n'a pas ce fichier (statut 404)."
                    : $"{current.Host} injoignable : statut {status}.");
            }
            return response;
        }
    }

    /// <summary>Recopie la réponse sans dépasser la taille permise ; chaque morceau reçu relance le délai d'attente.</summary>
    private async Task CopyAsync(Stream source, Stream target, Uri address, long maxBytes, IProgress<long>? progress, CancellationTokenSource stall)
    {
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk, stall.Token).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new OnlineException($"Le fichier de {address.Host} est plus gros qu'annoncé.");
            }
            await target.WriteAsync(chunk.AsMemory(0, read), stall.Token).ConfigureAwait(false);
            progress?.Report(total);
            stall.CancelAfter(_stall);
        }
    }

}
