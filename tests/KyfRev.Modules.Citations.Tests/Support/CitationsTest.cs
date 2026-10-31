using System.Text.Json;
using KyfRev.Application.Modules;
using KyfRev.Application.Ports;
using KyfRev.Application.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyfRev.Modules.Citations.Tests.Support;

/// <summary>Lecteur en ligne simulé : chaque adresse demandée est notée, la réponse est au choix du test.</summary>
internal sealed class FakeReader : IOnlineReader
{
    private readonly Lock _gate = new();
    private readonly List<Uri> _requests = [];

    /// <summary>Réponse à une adresse ; par défaut, une page sans citation.</summary>
    public Func<Uri, CancellationToken, Task<string>> Respond { get; set; } = (_, _) => Task.FromResult(WikiquoteApi.Page(string.Empty));

    public IReadOnlyList<Uri> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    public Task<string> ReadTextAsync(Uri address, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _requests.Add(address);
        }
        return Respond(address, cancellationToken);
    }
}

/// <summary>Réponses de l'API de MediaWiki, telles que Wikiquote les rend.</summary>
internal static class WikiquoteApi
{
    /// <summary>Une page et son wikitexte.</summary>
    public static string Page(string wikitext) => JsonSerializer.Serialize(new
    {
        batchcomplete = true,
        query = new
        {
            pages = new[]
            {
                new
                {
                    pageid = 1,
                    ns = 0,
                    title = "Page",
                    revisions = new[] { new { slots = new { main = new { contentmodel = "wikitext", contentformat = "text/x-wiki", content = wikitext } } } },
                },
            },
        },
    });

    /// <summary>Une page qui n'existe pas.</summary>
    public static string Missing(string title) =>
        JsonSerializer.Serialize(new { batchcomplete = true, query = new { pages = new[] { new { ns = 0, title, missing = true } } } });

    /// <summary>Une citation complète, avec sa référence.</summary>
    public static string Quote(string text, string author = "Blaise Pascal") =>
        $"{{{{citation|citation={text}}}}}\n{{{{Réf Livre|titre=Pensées|auteur={author}|année=1670}}}}\n";
}

/// <summary>
/// Un vrai coffre ouvert par l'application, et les services assemblés comme le fera la racine de
/// composition : le cœur (coffre ouvert, horloge, lecteur en ligne, journal), puis les modules
/// actifs pour ce coffre.
/// </summary>
internal sealed class CitationsTest : IDisposable
{
    private readonly TestFolder _folder = new();

    public CitationsTest(FakeTime? time = null)
    {
        Test = new TestApplication(time ?? FakeTime.AtLocal(2026, 10, 1, 9, 0));
        Assert.IsNull(Test.App.Vaults.Create(_folder.Vault).OpenError);
    }

    public TestApplication Test { get; }

    public FakeTime Time => Test.Time;

    public FakeReader Reader { get; } = new();

    public string VaultFolder => _folder.Vault;

    /// <summary>Active le module Citations (et la recherche en ligne, au choix) pour ce coffre.</summary>
    public void Activate(bool online = true)
    {
        var preferences = Test.App.Settings.Get().Value;
        Assert.IsTrue(Test.App.Settings.Change(preferences with { QuotesActive = true, QuotesOnline = online }).Succeeded);
    }

    /// <summary>Les services du coffre ouvert ; <paramref name="loaded"/> reçoit les modules chargés.</summary>
    public ServiceProvider Services(out IReadOnlyList<IKyfRevModule> loaded)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Test.App.Host);
        services.AddSingleton<TimeProvider>(Test.Time);
        services.AddSingleton<IOnlineReader>(Reader);
        services.AddSingleton<ILoggerFactory>(Test.Logs);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        loaded = new ModuleCatalog([new CitationsModule()]).Register(services, Test.App.Settings.Get().Value);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public void Write(string relativePath, string content)
    {
        var path = Path.Combine(_folder.Vault, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        Test.Dispose();
        _folder.Dispose();
    }
}
