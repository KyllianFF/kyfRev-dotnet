using System.Net;
using System.Net.Http.Headers;

namespace KyfRev.Infrastructure.Online.Tests;

/// <summary>
/// Le téléchargement d'un installateur, sans réseau : un faux gestionnaire joue GitHub. HTTPS et hôtes des versions publiées
/// seulement, redirections vers leur stockage et aucune autre, taille bornée, attente bornée, et rien à la place du fichier
/// tant qu'il n'est pas reçu en entier.
/// </summary>
[TestClass]
public sealed class FileDownloaderTests
{
    private static readonly Uri Release = new("https://github.com/KyllianFF/kyfRev-dotnet/releases/download/v1.2.0/kyfRev-1.2.0-installation.exe");

    private static readonly Uri Storage = new("https://release-assets.githubusercontent.com/github-production-release-asset/1/abc?sig=x");

    private static readonly byte[] Content = [.. Enumerable.Range(0, 200_000).Select(index => (byte)index)];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task The_release_redirects_to_its_storage_and_the_file_arrives_whole()
    {
        using var folder = new Folder();
        using var server = new FakeServer(request => request.RequestUri == Release ? Redirect(Storage) : Answer(Content));
        using var downloader = new FileDownloader("1.2.3", server, TimeSpan.FromSeconds(5));
        var destination = Path.Combine(folder.Path, "kyfRev-1.2.0-installation.exe");
        var seen = new List<long>();

        await downloader.DownloadAsync(Release, destination, Content.Length, new Inline(seen.Add), TestContext.CancellationToken);

        CollectionAssert.AreEqual(Content, await File.ReadAllBytesAsync(destination, TestContext.CancellationToken));
        CollectionAssert.AreEqual(new[] { Release, Storage }, server.Requests.Select(request => request.RequestUri).ToArray());
        Assert.AreEqual("kyfRev/1.2.3 (https://github.com/KyllianFF/kyfRev-dotnet)", server.Requests[0].Headers.UserAgent.ToString());
        Assert.AreEqual("application/octet-stream", server.Requests[0].Headers.Accept.Single().MediaType);
        Assert.AreEqual(Content.Length, seen[^1]);
        Assert.IsFalse(File.Exists(destination + ".partiel"));
    }

    [TestMethod]
    [DataRow("http://github.com/KyllianFF/kyfRev-dotnet/releases/download/v1/a.exe", "Adresse refusée : « http://github.com/KyllianFF/kyfRev-dotnet/releases/download/v1/a.exe » n'est pas en HTTPS.")]
    [DataRow("https://exemple.org/kyfRev.exe", "Adresse refusée : exemple.org n'est pas un hôte des versions publiées de kyfRev.")]
    public async Task Only_the_hosts_of_published_versions_are_asked(string address, string message)
    {
        using var folder = new Folder();
        using var server = new FakeServer(_ => Answer(Content));
        using var downloader = new FileDownloader("1.0.0", server, TimeSpan.FromSeconds(5));

        var refusal = await Assert.ThrowsExactlyAsync<OnlineException>(() => downloader.DownloadAsync(new Uri(address), Path.Combine(folder.Path, "a.exe"), 10, null, TestContext.CancellationToken));

        Assert.AreEqual(message, refusal.Message);
        Assert.IsEmpty(server.Requests);
    }

    [TestMethod]
    [DataRow("https://exemple.org/piege.exe", "Adresse refusée : exemple.org n'est pas un hôte des versions publiées de kyfRev.")]
    [DataRow("http://objects.githubusercontent.com/a.exe", "Adresse refusée : « http://objects.githubusercontent.com/a.exe » n'est pas en HTTPS.")]
    public async Task A_redirection_elsewhere_is_refused_and_nothing_is_written(string location, string message)
    {
        using var folder = new Folder();
        using var server = new FakeServer(_ => Redirect(new Uri(location)));
        using var downloader = new FileDownloader("1.0.0", server, TimeSpan.FromSeconds(5));
        var destination = Path.Combine(folder.Path, "a.exe");

        var refusal = await Assert.ThrowsExactlyAsync<OnlineException>(() => downloader.DownloadAsync(Release, destination, Content.Length, null, TestContext.CancellationToken));

        Assert.AreEqual(message, refusal.Message);
        Assert.IsEmpty(Directory.GetFiles(folder.Path));
    }

    [TestMethod]
    public async Task Redirections_end_somewhere_and_a_relative_one_stays_on_its_host()
    {
        using var folder = new Folder();
        using var loop = new FakeServer(_ => Redirect(new Uri("/encore", UriKind.Relative)));
        using var downloader = new FileDownloader("1.0.0", loop, TimeSpan.FromSeconds(5));

        var refusal = await Assert.ThrowsExactlyAsync<OnlineException>(() => downloader.DownloadAsync(Release, Path.Combine(folder.Path, "a.exe"), 10, null, TestContext.CancellationToken));

        Assert.AreEqual("github.com injoignable : redirection refusée.", refusal.Message);
        Assert.HasCount(6, loop.Requests);
        Assert.AreEqual(new Uri("https://github.com/encore"), loop.Requests[^1].RequestUri);

        using var nowhere = new FakeServer(_ => new HttpResponseMessage(HttpStatusCode.Found));
        using var lost = new FileDownloader("1.0.0", nowhere, TimeSpan.FromSeconds(5));
        Assert.AreEqual("github.com injoignable : redirection refusée.", (await Assert.ThrowsExactlyAsync<OnlineException>(() => lost.DownloadAsync(Release, Path.Combine(folder.Path, "a.exe"), 10, null, TestContext.CancellationToken))).Message);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound, "github.com n'a pas ce fichier (statut 404).")]
    [DataRow(HttpStatusCode.ServiceUnavailable, "github.com injoignable : statut 503.")]
    public async Task An_error_status_is_said(HttpStatusCode status, string message)
    {
        using var folder = new Folder();
        using var server = new FakeServer(_ => new HttpResponseMessage(status));
        using var downloader = new FileDownloader("1.0.0", server, TimeSpan.FromSeconds(5));

        var refusal = await Assert.ThrowsExactlyAsync<OnlineException>(() => downloader.DownloadAsync(Release, Path.Combine(folder.Path, "a.exe"), 10, null, TestContext.CancellationToken));

        Assert.AreEqual(message, refusal.Message);
    }

    [TestMethod]
    public async Task A_file_larger_than_announced_is_refused_whether_declared_or_streamed()
    {
        using var folder = new Folder();
        var destination = Path.Combine(folder.Path, "a.exe");
        using var declared = new FakeServer(_ => Answer(Content));
        using var first = new FileDownloader("1.0.0", declared, TimeSpan.FromSeconds(5));
        Assert.AreEqual("Le fichier de github.com est plus gros qu'annoncé.", (await Assert.ThrowsExactlyAsync<OnlineException>(() => first.DownloadAsync(Release, destination, 1000, null, TestContext.CancellationToken))).Message);

        using var streamed = new FakeServer(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(Content)) });
        using var second = new FileDownloader("1.0.0", streamed, TimeSpan.FromSeconds(5));
        Assert.AreEqual("Le fichier de github.com est plus gros qu'annoncé.", (await Assert.ThrowsExactlyAsync<OnlineException>(() => second.DownloadAsync(Release, destination, 1000, null, TestContext.CancellationToken))).Message);
        Assert.IsFalse(File.Exists(destination), "rien à la place du fichier ; son début reste à côté, sans rien effacer");
    }

    [TestMethod]
    public async Task A_download_that_stalls_stops_and_keeps_the_previous_file()
    {
        using var folder = new Folder();
        var destination = Path.Combine(folder.Path, "a.exe");
        await File.WriteAllTextAsync(destination, "précédent", TestContext.CancellationToken);
        using var server = new FakeServer(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) });
        using var downloader = new FileDownloader("1.0.0", server, TimeSpan.FromMilliseconds(200));

        var refusal = await Assert.ThrowsExactlyAsync<OnlineException>(() => downloader.DownloadAsync(Release, destination, 1000, null, TestContext.CancellationToken));

        Assert.AreEqual("github.com injoignable : délai dépassé.", refusal.Message);
        Assert.AreEqual("précédent", await File.ReadAllTextAsync(destination, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task A_download_stopped_by_the_caller_is_not_a_network_error()
    {
        using var folder = new Folder();
        using var stop = new CancellationTokenSource();
        using var server = new FakeServer(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) });
        using var downloader = new FileDownloader("1.0.0", server, TimeSpan.FromSeconds(30));
        var destination = Path.Combine(folder.Path, "a.exe");
        stop.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<OperationCanceledException>(() => downloader.DownloadAsync(Release, destination, 1000, null, stop.Token));

        Assert.IsFalse(File.Exists(destination));
    }

    [TestMethod]
    public async Task A_connection_failure_or_an_unwritable_place_is_said()
    {
        using var folder = new Folder();
        using var refused = new FakeServer(_ => throw new HttpRequestException("connexion refusée"));
        using var first = new FileDownloader("1.0.0", refused, TimeSpan.FromSeconds(5));
        Assert.AreEqual("github.com injoignable : connexion refusée", (await Assert.ThrowsExactlyAsync<OnlineException>(() => first.DownloadAsync(Release, Path.Combine(folder.Path, "a.exe"), 1000, null, TestContext.CancellationToken))).Message);

        using var server = new FakeServer(_ => Answer([1, 2, 3]));
        using var second = new FileDownloader("1.0.0", server, TimeSpan.FromSeconds(5));
        var refusal = await Assert.ThrowsExactlyAsync<OnlineException>(() => second.DownloadAsync(Release, Path.Combine(folder.Path, "absent", "a.exe"), 1000, null, TestContext.CancellationToken));
        Assert.StartsWith("Le fichier n'a pas pu s'écrire : ", refusal.Message);
    }

    [TestMethod]
    public async Task Its_arguments_are_checked()
    {
        using var downloader = new FileDownloader("1.0.0");
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => downloader.DownloadAsync(null!, "a.exe", 1, null, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => downloader.DownloadAsync(Release, string.Empty, 1, null, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => downloader.DownloadAsync(Release, "a.exe", 0, null, TestContext.CancellationToken));
        Assert.ThrowsExactly<ArgumentNullException>(() => new FileDownloader(null!));
    }

    private static HttpResponseMessage Answer(byte[] body)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentLength = body.Length;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static HttpResponseMessage Redirect(Uri location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = location;
        return response;
    }

    private sealed class Folder : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("kyfrev-telechargement-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    /// <summary>Reçoit la progression tout de suite, sur le fil qui la donne.</summary>
    private sealed class Inline(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }

    /// <summary>Le serveur : répond à chaque demande, qu'il garde.</summary>
    private sealed class FakeServer(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        private readonly Lock _gate = new();
        private readonly List<HttpRequestMessage> _requests = [];

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
            return Task.FromResult(respond(request));
        }
    }

    /// <summary>Une réponse dont rien n'arrive, jusqu'à ce qu'on arrête de l'attendre.</summary>
    private sealed class StalledStream : Stream
    {
        /// <summary>Rien ne la termine : la lecture n'en sort que lorsqu'on cesse de l'attendre.</summary>
        private readonly TaskCompletionSource _nothing = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _nothing.Task.WaitAsync(cancellationToken);
            return 0;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
