using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.Text;
using KyfRev.Domain.Randomness;

namespace KyfRev.Modules.Citations;

/// <summary>La citation du jour et les pages de Wikiquote à lire pour la trouver, comme dans la version 0.x.</summary>
public static class DailyQuote
{
    /// <summary>
    /// Citation du jour : celle trouvée en ligne ce jour-là, sinon une citation du coffre tirée au
    /// hasard, la même toute la journée ; rien si le coffre n'en a aucune.
    /// </summary>
    public static Quote? Of(IVault vault, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(vault);
        return Of(vault, vault.Id, day);
    }

    /// <summary>Citation du jour parmi celles de <paramref name="store"/>, tirée avec l'identifiant du coffre.</summary>
    internal static Quote? Of(IQuoteStore store, string vaultId, DateOnly day)
    {
        if (store.OnlineQuoteOf(day) is { } online)
        {
            return online;
        }
        QuoteList list;
        try
        {
            list = store.Quotes();
        }
        catch (OperationException)
        {
            // Dossier des citations illisible : pas de citation du jour, comme dans la 0.x.
            return null;
        }
        var quotes = list.Quotes.OrderBy(quote => quote.Path, Utf8Order.Instance).ToList();
        return quotes.Count == 0 ? null : quotes[(int)(DailySeed.For(vaultId, day) % (ulong)quotes.Count)];
    }

    /// <summary>
    /// Pages de Wikiquote à lire ce jour-là ; aucune si le module Citations ou la recherche en
    /// ligne est coupé, ou si la citation du jour a déjà été trouvée.
    /// </summary>
    public static IReadOnlyList<WikiquotePage> PagesToRead(IVault vault, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(vault);
        var preferences = vault.Preferences;
        return !preferences.QuotesActive || !preferences.QuotesOnline || vault.OnlineQuoteDay() == day
            ? []
            : Wikiquote.PagesOfTheDay(vault.Id, day, preferences.QuoteDomains);
    }
}
