using System.Globalization;
using System.Text.Json;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using Microsoft.Extensions.Logging;

namespace KyfRev.Modules.Citations;

/// <summary>
/// Recherche de la citation du jour sur le Wikiquote francophone, comme dans la version 0.x : la
/// seule requête réseau que kyfRev fasse de lui-même.
/// <list type="bullet">
/// <item>Adresse fixe : l'API de fr.wikiquote.org, par le lecteur durci (<see cref="IOnlineReader"/>).
/// Seul le titre de la page demandée part.</item>
/// <item>Seulement si le module Citations et la recherche en ligne sont actifs, et tant que la
/// citation du jour n'est pas trouvée : une page lue par jour d'ordinaire, trois au plus.</item>
/// <item>En cas d'échec : nouvel essai une heure plus tard au plus tôt ; en attendant, la
/// citation du jour est tirée du coffre.</item>
/// </list>
/// </summary>
public sealed partial class OnlineQuoteSearch(VaultHost host, IOnlineReader reader, TimeProvider time, ILogger<OnlineQuoteSearch> logger)
{
    /// <summary>API du Wikiquote francophone.</summary>
    public const string Endpoint = "https://fr.wikiquote.org/w/api.php";

    private static readonly TimeSpan WaitAfterFailure = TimeSpan.FromHours(1);

    private readonly Lock _gate = new();
    private bool _running;
    private DateTimeOffset? _lastFailure;
    private DateOnly? _nothingNewOn;

    /// <summary>
    /// Cherche la citation du jour, si besoin ; rend la citation retenue, ou rien (déjà trouvée,
    /// recherche coupée, hors ligne…).
    /// </summary>
    public async Task<Quote?> SearchAsync(CancellationToken cancellationToken)
    {
        var now = Present.Of(time).Now;
        var pages = host.Use(vault => DailyQuote.PagesToRead(vault, now.Day));
        if (!pages.Succeeded || pages.Value.Count == 0)
        {
            return null;
        }
        lock (_gate)
        {
            var waiting = _lastFailure is { } failure && time.GetUtcNow() - failure < WaitAfterFailure;
            if (_running || waiting || _nothingNewOn == now.Day)
            {
                return null;
            }
            _running = true;
        }

        Quote? quote = null;
        string? error = null;
        try
        {
            quote = await ReadPagesAsync(pages.Value, now, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationException exception)
        {
            error = exception.Message;
        }
        finally
        {
            lock (_gate)
            {
                _running = false;
            }
        }

        lock (_gate)
        {
            if (error is not null)
            {
                _lastFailure = time.GetUtcNow();
                LogFailed(logger, error);
            }
            else if (quote is null)
            {
                _nothingNewOn = now.Day;
                LogNothingNew(logger);
            }
            else
            {
                _lastFailure = null;
                LogFound(logger, quote.Path);
            }
        }
        return quote;
    }

    /// <summary>Adresse de l'API pour le wikitexte d'une page.</summary>
    internal static Uri PageAddress(string title) =>
        new($"{Endpoint}?action=query&prop=revisions&rvprop=content&rvslots=main&titles={Uri.EscapeDataString(title)}&format=json&formatversion=2");

    /// <summary>Wikitexte de la page dans la réponse de l'API (<c>/query/pages/0/revisions/0/slots/main/content</c>).</summary>
    internal static string Wikitext(string json, string title)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var content = Step(document.RootElement, "query") is { } query && Step(query, "pages") is { } pages && Step(pages, "0") is { } page
                && Step(page, "revisions") is { } revisions && Step(revisions, "0") is { } revision
                && Step(revision, "slots") is { } slots && Step(slots, "main") is { } main
                && Step(main, "content") is { ValueKind: JsonValueKind.String } text
                    ? text.GetString()
                    : null;
            return content ?? throw new WikiquoteException($"page « {title} » introuvable sur Wikiquote");
        }
        catch (JsonException exception)
        {
            throw new WikiquoteException($"réponse de Wikiquote illisible : {exception.Message}", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new WikiquoteException($"réponse de Wikiquote illisible : {exception.Message}", exception);
        }
    }

    /// <summary>Un pas de pointeur JSON, comme <c>Value::pointer</c> de serde_json : clé d'un objet, ou rang dans un tableau.</summary>
    private static JsonElement? Step(JsonElement element, string token) => element.ValueKind switch
    {
        JsonValueKind.Object when element.TryGetProperty(token, out var value) => value,
        JsonValueKind.Array when int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var rank) && rank < element.GetArrayLength() => element[rank],
        _ => null,
    };

    private async Task<Quote?> ReadPagesAsync(IReadOnlyList<WikiquotePage> pages, Domain.Calendar.Moment now, CancellationToken cancellationToken)
    {
        foreach (var page in pages)
        {
            var json = await reader.ReadTextAsync(PageAddress(page.Title), cancellationToken).ConfigureAwait(false);
            var found = Wikiquote.QuotesOfPage(Wikitext(json, page.Title), page);
            LogPageRead(logger, page.Title, found.Count);
            var kept = host.Use(vault => vault.KeepOnlineQuote(page.Title, found, now));
            if (!kept.Succeeded)
            {
                throw new WikiquoteException(kept.Error!);
            }
            if (kept.Value is { } quote)
            {
                return quote;
            }
        }
        return null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Page « {Page} » de Wikiquote lue : {Count} citation(s).")]
    private static partial void LogPageRead(ILogger logger, string page, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Citation du jour trouvée sur Wikiquote : {Path}")]
    private static partial void LogFound(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rien de nouveau sur Wikiquote aujourd'hui.")]
    private static partial void LogNothingNew(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Citation du jour en ligne impossible : {Reason}")]
    private static partial void LogFailed(ILogger logger, string reason);
}

/// <summary>Réponse de Wikiquote inexploitable, ou citation impossible à retenir.</summary>
internal sealed class WikiquoteException : OperationException
{
    public WikiquoteException()
    {
    }

    public WikiquoteException(string message)
        : base(message)
    {
    }

    public WikiquoteException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
