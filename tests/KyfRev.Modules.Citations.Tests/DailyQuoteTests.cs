using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Domain.Calendar;
using KyfRev.Modules.Citations.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.Modules.Citations.Tests;

/// <summary>
/// La citation du jour et les pages à lire, sur un vrai coffre. Leur comparaison détaillée avec la
/// 0.x est faite par le scénario 12 de la suite du coffre.
/// </summary>
[TestClass]
public sealed class DailyQuoteTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);

    [TestMethod]
    public void The_quote_of_the_day_is_drawn_from_the_vault_and_kept_all_day()
    {
        using var test = new CitationsTest();
        test.Activate();
        using var services = test.Services(out _);
        var quotes = services.GetRequiredService<QuoteUseCases>();
        Assert.IsNull(quotes.QuoteOfTheDay().Value);

        foreach (var text in new[] { "Première citation.", "Deuxième citation.", "Troisième citation." })
        {
            Assert.IsTrue(quotes.Add(new QuoteContent(text, null, null, QuoteSource.None, null, [])).Succeeded);
        }
        var morning = quotes.QuoteOfTheDay().Value!.Path;
        test.Time.Advance(TimeSpan.FromHours(10));
        Assert.AreEqual(morning, quotes.QuoteOfTheDay().Value!.Path);
        var drawn = new HashSet<string>(StringComparer.Ordinal);
        for (var day = 0; day < 30; day++)
        {
            test.Time.Advance(TimeSpan.FromDays(1));
            drawn.Add(quotes.QuoteOfTheDay().Value!.Path);
        }
        Assert.HasCount(3, drawn);
    }

    [TestMethod]
    public void Pages_to_read_follow_the_preferences_and_stop_once_found()
    {
        using var test = new CitationsTest();
        var host = test.Test.App.Host;
        Assert.IsEmpty(host.Use(vault => DailyQuote.PagesToRead(vault, Day)).Value);

        test.Activate(online: false);
        Assert.IsEmpty(host.Use(vault => DailyQuote.PagesToRead(vault, Day)).Value);

        test.Activate();
        var pages = host.Use(vault => DailyQuote.PagesToRead(vault, Day)).Value;
        Assert.HasCount(Wikiquote.PagesPerDay, pages);
        var id = host.Use(vault => vault.Id).Value;
        CollectionAssert.AreEqual(Wikiquote.PagesOfTheDay(id, Day, Wikiquote.Domains.Select(d => d.Name)).ToList(), pages.ToList());

        var now = new Moment(Day, new DateTimeOffset(2026, 10, 1, 7, 0, 0, TimeSpan.Zero));
        var kept = host.Use(vault => vault.KeepOnlineQuote("Sagesse", [new QuoteContent("La sagesse vient avec l'âge.", null, null, QuoteSource.None, null, [])], now)).Value;
        Assert.IsNotNull(kept);
        Assert.IsEmpty(host.Use(vault => DailyQuote.PagesToRead(vault, Day)).Value);
        Assert.HasCount(Wikiquote.PagesPerDay, host.Use(vault => DailyQuote.PagesToRead(vault, Day.AddDays(1))).Value);
        Assert.AreEqual(kept.Path, host.Use(vault => DailyQuote.Of(vault, Day)).Value!.Path);
    }

    [TestMethod]
    public void An_unreadable_quote_folder_gives_no_quote_of_the_day()
    {
        var store = new UnreadableQuotes();
        Assert.IsNull(DailyQuote.Of(store, "c_1", Day));
        Assert.AreEqual(1, store.Reads);
        Assert.ThrowsExactly<ArgumentNullException>(() => DailyQuote.Of(null!, Day));
        Assert.ThrowsExactly<ArgumentNullException>(() => DailyQuote.PagesToRead(null!, Day));
    }

    /// <summary>Citations dont le dossier ne peut pas être lu.</summary>
    private sealed class UnreadableQuotes : IQuoteStore
    {
        public int Reads { get; private set; }

        public QuoteList Quotes()
        {
            Reads++;
            throw new WikiquoteException("dossier des citations illisible");
        }

        public Quote? OnlineQuoteOf(DateOnly day) => null;

        public DateOnly? OnlineQuoteDay() => null;

        public Quote AddQuote(QuoteContent content, Moment now) => throw new NotSupportedException();

        public Quote ModifyQuote(string path, QuoteContent content, Moment now) => throw new NotSupportedException();

        public string TrashQuote(string path) => throw new NotSupportedException();

        public string RestoreQuote(string path) => throw new NotSupportedException();

        public Quote? KeepOnlineQuote(string page, IReadOnlyList<QuoteContent> found, Moment now) => throw new NotSupportedException();
    }
}
