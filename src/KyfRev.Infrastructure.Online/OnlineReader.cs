using System.Net;
using System.Net.Http.Headers;
using System.Text;
using KyfRev.Application;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Online;

/// <summary>
/// Lecteur HTTPS durci, comme celui de la version 0.x : TLS et certificats de Windows, adresse en
/// HTTPS seulement, ni redirection ni cookie, réponse limitée en taille et en durée, agent
/// identifié (politique de Wikimedia). Seule l'adresse demandée part.
/// </summary>
public sealed class OnlineReader : IOnlineReader, IDisposable
{
    /// <summary>Réponse limitée à 2 Mo.</summary>
    public const int MaxResponseBytes = 2 * 1024 * 1024;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private readonly HttpMessageHandler _handler;
    private readonly HttpClient _client;
    private readonly int _maxBytes;

    /// <summary>Le lecteur de l'application.</summary>
    /// <param name="version">Version de kyfRev, annoncée dans l'agent.</param>
    public OnlineReader(string version)
        : this(version, handler: null, DefaultTimeout, MaxResponseBytes)
    {
    }

    /// <summary>Lecteur réglable, pour les tests (sans <paramref name="handler"/> : la connexion durcie).</summary>
    internal OnlineReader(string version, HttpMessageHandler? handler, TimeSpan timeout, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(version);
        _handler = handler ?? Handler();
        _client = new HttpClient(_handler, disposeHandler: true) { Timeout = timeout };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("kyfRev", version));
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(https://github.com/KyllianFF/kyfRev-dotnet)"));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _maxBytes = maxBytes;
    }

    /// <inheritdoc/>
    public async Task<string> ReadTextAsync(Uri address, CancellationToken cancellationToken)
    {
        var bytes = await ReadBytesAsync(address, cancellationToken).ConfigureAwait(false);
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new OnlineException($"Réponse de {address.Host} illisible : pas en UTF-8.", exception);
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]> ReadBytesAsync(Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.IsAbsoluteUri || address.Scheme != Uri.UriSchemeHttps)
        {
            throw new OnlineException($"Adresse refusée : « {address} » n'est pas en HTTPS.");
        }
        try
        {
            using var response = await _client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                throw new OnlineException($"{address.Host} injoignable : redirection refusée.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new OnlineException($"{address.Host} injoignable : statut {(int)response.StatusCode}.");
            }
            if (response.Content.Headers.ContentLength > _maxBytes)
            {
                throw new OnlineException($"Réponse de {address.Host} trop volumineuse.");
            }
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                return await ReadLimitedAsync(stream, address, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (HttpRequestException exception)
        {
            throw new OnlineException($"{address.Host} injoignable : {exception.Message}", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OnlineException($"{address.Host} injoignable : délai dépassé.", exception);
        }
    }

    /// <summary>Connexions sans redirection ni cookie ; la réponse décompressée reste bornée.</summary>
    internal static SocketsHttpHandler Handler() =>
        new() { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.All };

    /// <summary>Ferme le client.</summary>
    public void Dispose() => _client.Dispose();

    /// <summary>Lit la réponse sans dépasser la limite, quelle que soit la taille annoncée.</summary>
    private async Task<byte[]> ReadLimitedAsync(Stream stream, Uri address, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > _maxBytes)
            {
                throw new OnlineException($"Réponse de {address.Host} trop volumineuse.");
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        return buffer.ToArray();
    }
}

/// <summary>Lecture en ligne impossible ou refusée.</summary>
public sealed class OnlineException : OperationException
{
    /// <summary>Échec sans précision.</summary>
    public OnlineException()
        : base("Lecture en ligne impossible.")
    {
    }

    /// <summary>Échec avec le message donné.</summary>
    public OnlineException(string message)
        : base(message)
    {
    }

    /// <summary>Échec avec le message donné et sa cause.</summary>
    public OnlineException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
