using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace KyfRev.Infrastructure.Online.Tests;

/// <summary>
/// Le lecteur HTTPS durci, sans réseau : un faux gestionnaire joue le serveur. HTTPS seulement,
/// ni redirection ni cookie, réponse bornée en taille et en durée, UTF-8 strict, agent identifié.
/// </summary>
[TestClass]
public sealed class OnlineReaderTests
{
    private static readonly Uri Address = new("https://fr.wikiquote.org/w/api.php?titles=Libert%C3%A9");

    [TestMethod]
    public async Task The_answer_is_read_as_utf8_with_an_identified_agent()
    {
        using var server = new FakeServer((_, _) => Answer(HttpStatusCode.OK, Encoding.UTF8.GetBytes("{\"texte\":\"Liberté, égalité 😀\"}")));
        using var reader = new OnlineReader("1.2.3", server, TimeSpan.FromSeconds(5), 1024);

        Assert.AreEqual("{\"texte\":\"Liberté, égalité 😀\"}", await reader.ReadTextAsync(Address, CancellationToken.None));

        var request = server.Requests.Single();
        Assert.AreEqual(HttpMethod.Get, request.Method);
        Assert.AreEqual(Address, request.RequestUri);
        Assert.AreEqual("kyfRev/1.2.3 (https://github.com/KyllianFF/kyfRev-dotnet)", request.Headers.UserAgent.ToString());
        Assert.AreEqual("application/json", request.Headers.Accept.Single().MediaType);
        Assert.IsFalse(request.Headers.Contains("Cookie"));
    }

    [TestMethod]
    [DataRow("http://fr.wikiquote.org/w/api.php")]
    [DataRow("ftp://fr.wikiquote.org/")]
    [DataRow("file:///C:/coffre/secret.md")]
    [DataRow("api.php")]
    public async Task Only_https_addresses_are_read(string address)
    {
        using var server = new FakeServer((_, _) => Answer(HttpStatusCode.OK, []));
        using var reader = new OnlineReader("1.0.0", server, TimeSpan.FromSeconds(5), 1024);

        var refusal = await Assert.ThrowsExactlyAsync<OnlineException>(() => reader.ReadTextAsync(new Uri(address, UriKind.RelativeOrAbsolute), CancellationToken.None));

        Assert.AreEqual($"Adresse refusée : « {address} » n'est pas en HTTPS.", refusal.Message);
        Assert.IsEmpty(server.Requests);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => reader.ReadTextAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.MovedPermanently, "fr.wikiquote.org injoignable : redirection refusée.")]
    [DataRow(HttpStatusCode.Redirect, "fr.wikiquote.org injoignable : redirection refusée.")]
    [DataRow(HttpStatusCode.PermanentRedirect, "fr.wikiquote.org injoignable : redirection refusée.")]
    [DataRow(HttpStatusCode.NotFound, "fr.wikiquote.org injoignable : statut 404.")]
    [DataRow(HttpStatusCode.TooManyRequests, "fr.wikiquote.org injoignable : statut 429.")]
    [DataRow(HttpStatusCode.ServiceUnavailable, "fr.wikiquote.org injoignable : statut 503.")]
    public async Task Redirections_and_errors_are_refused(HttpStatusCode status, string message)
    {
        using var server = new FakeServer((_, _) =>
        {
            var response = Answer(status, Encoding.UTF8.GetBytes("ailleurs"));
            response.Headers.Location = new Uri("https://exemple.org/piege");
            return response;
        });
        using var reader = new OnlineReader("1.0.0", server, TimeSpan.FromSeconds(5), 1024);

        Assert.AreEqual(message, (await Assert.ThrowsExactlyAsync<OnlineException>(() => reader.ReadTextAsync(Address, CancellationToken.None))).Message);
        Assert.HasCount(1, server.Requests);
    }

    [TestMethod]
    public async Task An_answer_too_large_is_refused_whether_declared_or_streamed()
    {
        using var declared = new FakeServer((_, _) => Answer(HttpStatusCode.OK, new byte[2048]));
        using var reader = new OnlineReader("1.0.0", declared, TimeSpan.FromSeconds(5), 1024);
        Assert.AreEqual("Réponse de fr.wikiquote.org trop volumineuse.", (await Assert.ThrowsExactlyAsync<OnlineException>(() => reader.ReadTextAsync(Address, CancellationToken.None))).Message);

        using var streamed = new FakeServer((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new EndlessStream()) });
        using var endless = new OnlineReader("1.0.0", streamed, TimeSpan.FromSeconds(5), 200_000);
        Assert.AreEqual("Réponse de fr.wikiquote.org trop volumineuse.", (await Assert.ThrowsExactlyAsync<OnlineException>(() => endless.ReadTextAsync(Address, CancellationToken.None))).Message);

        using var exact = new FakeServer((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new EndlessStream(1024)) });
        using var limit = new OnlineReader("1.0.0", exact, TimeSpan.FromSeconds(5), 1024);
        Assert.HasCount(1024, await limit.ReadTextAsync(Address, CancellationToken.None));
    }

    [TestMethod]
    public async Task Bytes_are_read_exactly_even_when_they_are_not_text()
    {
        byte[] body = [0x7B, 0xE9, 0x00, 0xFF, 0x7D];
        using var server = new FakeServer((_, _) => Answer(HttpStatusCode.OK, body));
        using var reader = new OnlineReader("1.0.0", server, TimeSpan.FromSeconds(5), 1024);

        CollectionAssert.AreEqual(body, await reader.ReadBytesAsync(Address, CancellationToken.None), "une empreinte se vérifie sur les octets exacts");
        await Assert.ThrowsExactlyAsync<OnlineException>(() => reader.ReadBytesAsync(new Uri("http://exemple.fr/a.js"), CancellationToken.None));
    }

    [TestMethod]
    public async Task A_text_only_reader_gives_its_text_as_utf8_bytes()
    {
        KyfRev.Application.Ports.IOnlineReader reader = new TextOnly();

        CollectionAssert.AreEqual("é"u8.ToArray(), await reader.ReadBytesAsync(Address, CancellationToken.None));
    }

    [TestMethod]
    public async Task An_answer_not_in_utf8_is_refused()
    {
        using var server = new FakeServer((_, _) => Answer(HttpStatusCode.OK, [0x7B, 0xE9, 0x7D]));
        using var reader = new OnlineReader("1.0.0", server, TimeSpan.FromSeconds(5), 1024);

        var refusal = await Assert.ThrowsExactlyAsync<OnlineException>(() => reader.ReadTextAsync(Address, CancellationToken.None));

        Assert.AreEqual("Réponse de fr.wikiquote.org illisible : pas en UTF-8.", refusal.Message);
        Assert.IsInstanceOfType<DecoderFallbackException>(refusal.InnerException);
    }

    [TestMethod]
    public async Task A_network_failure_or_a_timeout_says_the_site_cannot_be_reached()
    {
        using var offline = new FakeServer(Unreachable);
        using var reader = new OnlineReader("1.0.0", offline, TimeSpan.FromSeconds(5), 1024);
        var refusal = await Assert.ThrowsExactlyAsync<OnlineException>(() => reader.ReadTextAsync(Address, CancellationToken.None));
        Assert.AreEqual("fr.wikiquote.org injoignable : Hôte inconnu.", refusal.Message);
        Assert.IsInstanceOfType<HttpRequestException>(refusal.InnerException);

        using var slow = new FakeServer(NeverAnswers);
        using var impatient = new OnlineReader("1.0.0", slow, TimeSpan.FromMilliseconds(50), 1024);
        Assert.AreEqual("fr.wikiquote.org injoignable : délai dépassé.", (await Assert.ThrowsExactlyAsync<OnlineException>(() => impatient.ReadTextAsync(Address, CancellationToken.None))).Message);
    }

    [TestMethod]
    public async Task A_cancellation_by_the_caller_is_not_a_failure()
    {
        using var slow = new FakeServer(NeverAnswers);
        using var reader = new OnlineReader("1.0.0", slow, TimeSpan.FromSeconds(30), 1024);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<OperationCanceledException>(() => reader.ReadTextAsync(Address, cancellation.Token));
    }

    [TestMethod]
    public void The_connection_follows_no_redirection_keeps_no_cookie_and_bounds_decompression()
    {
        using var handler = OnlineReader.Handler();
        Assert.IsFalse(handler.AllowAutoRedirect);
        Assert.IsFalse(handler.UseCookies);
        Assert.AreEqual(DecompressionMethods.All, handler.AutomaticDecompression);

        using var reader = new OnlineReader("1.0.0");
        Assert.ThrowsExactly<ArgumentNullException>(() => new OnlineReader(null!).Dispose());
    }

    [TestMethod]
    public void Disposing_the_reader_closes_the_connection()
    {
        using var server = new FakeServer((_, _) => Answer(HttpStatusCode.OK, []));
        var reader = new OnlineReader("1.0.0", server, TimeSpan.FromSeconds(5), 1024);
        Assert.IsFalse(server.Disposed);
        reader.Dispose();
        Assert.IsTrue(server.Disposed);
    }

    [TestMethod]
    public void The_failure_keeps_its_message_and_cause()
    {
        Assert.AreEqual("Lecture en ligne impossible.", new OnlineException().Message);
        Assert.AreEqual("hors ligne", new OnlineException("hors ligne").Message);
        var cause = new IOException("coupure");
        Assert.AreSame(cause, new OnlineException("hors ligne", cause).InnerException);
    }

    /// <summary>Serveur qui ne répond jamais : seule l'annulation (délai ou appelant) arrête l'attente.</summary>
    private static Task<HttpResponseMessage> NeverAnswers(HttpRequestMessage request, CancellationToken cancellationToken) =>
        new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously).Task.WaitAsync(cancellationToken);

    private static HttpResponseMessage Unreachable(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("Hôte inconnu.");

    private static HttpResponseMessage Answer(HttpStatusCode status, byte[] body)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return new HttpResponseMessage(status) { Content = content };
    }

    /// <summary>Serveur simulé : note chaque requête et répond comme le test le veut.</summary>
    private sealed class TextOnly : KyfRev.Application.Ports.IOnlineReader
    {
        public Task<string> ReadTextAsync(Uri address, CancellationToken cancellationToken) => Task.FromResult("é");
    }

    private sealed class FakeServer(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private readonly Lock _gate = new();
        private readonly List<HttpRequestMessage> _requests = [];

        public FakeServer(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
            : this((request, token) => Task.FromResult(respond(request, token)))
        {
        }

        public bool Disposed { get; private set; }

        public IReadOnlyList<HttpRequestMessage> Requests
        {
            get
            {
                lock (_gate)
                {
                    return [.. _requests];
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _requests.Add(request);
            }
            return respond(request, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>Flux sans longueur connue, de <paramref name="length"/> octets « a » (sans fin par défaut).</summary>
    private sealed class EndlessStream(long length = long.MaxValue) : Stream
    {
        private long _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = (int)Math.Min(count, length - _position);
            buffer.AsSpan(offset, read).Fill((byte)'a');
            _position += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
