using KyfRev.Application.UseCases;
using KyfRev.Modules.Citations.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.Modules.Citations.Tests;

/// <summary>
/// La recherche de la citation du jour sur Wikiquote, avec un lecteur simulé : quand elle part,
/// ce qu'elle demande, et comment elle se retient après un échec ou une journée sans nouveauté.
/// </summary>
[TestClass]
public sealed class OnlineQuoteSearchTests
{
    [TestMethod]
    public async Task Nothing_is_requested_when_the_module_or_the_online_search_is_off()
    {
        using var test = new CitationsTest();
        using (var services = test.Services(out _))
        {
            Assert.IsNull(services.GetService<OnlineQuoteSearch>());
        }

        test.Activate(online: false);
        using var offline = test.Services(out _);
        Assert.IsNull(await offline.GetRequiredService<OnlineQuoteSearch>().SearchAsync(CancellationToken.None));
        Assert.IsEmpty(test.Reader.Requests);

        test.Test.App.Host.Dispose();
        Assert.IsNull(await offline.GetRequiredService<OnlineQuoteSearch>().SearchAsync(CancellationToken.None));
        Assert.IsEmpty(test.Reader.Requests);
    }

    [TestMethod]
    public async Task The_first_page_with_something_new_gives_the_quote_of_the_day()
    {
        using var test = new CitationsTest();
        test.Activate();
        using var services = test.Services(out _);
        test.Reader.Respond = (_, _) => Task.FromResult(WikiquoteApi.Page(WikiquoteApi.Quote("Le cœur a ses raisons que la raison ne connaît point.")));
        var quotes = services.GetRequiredService<QuoteUseCases>();

        var host = test.Test.App.Host;
        var first = host.Use(vault => DailyQuote.PagesToRead(vault, new DateOnly(2026, 10, 1))).Value[0];

        var found = (await quotes.SearchQuoteOfTheDayAsync(CancellationToken.None)).Value;

        Assert.IsNotNull(found);
        Assert.AreEqual("wikiquote", found.Origin);
        Assert.AreEqual("Blaise Pascal", found.Content.Author);
        Assert.AreEqual(OnlineQuoteSearch.PageAddress(first.Title), test.Reader.Requests.Single());
        Assert.AreEqual(first.Address, found.Content.Source.Address);
        Assert.Contains(first.Domain, found.Content.Tags);
        Assert.AreEqual(found.Path, quotes.QuoteOfTheDay().Value!.Path);
        Assert.Contains($"Citation du jour trouvée sur Wikiquote : {found.Path}", test.Test.Logs.Entries.Select(entry => entry.Message));

        // Trouvée : plus aucune requête ce jour-là.
        Assert.IsNull((await quotes.SearchQuoteOfTheDayAsync(CancellationToken.None)).Value);
        Assert.HasCount(1, test.Reader.Requests);
    }

    [TestMethod]
    public async Task A_day_without_anything_new_reads_three_pages_at_most_then_waits_for_tomorrow()
    {
        using var test = new CitationsTest();
        test.Activate();
        using var services = test.Services(out _);
        var search = services.GetRequiredService<OnlineQuoteSearch>();

        Assert.IsNull(await search.SearchAsync(CancellationToken.None));
        Assert.HasCount(Wikiquote.PagesPerDay, test.Reader.Requests);
        Assert.Contains("Rien de nouveau sur Wikiquote aujourd'hui.", test.Test.Logs.Entries.Select(entry => entry.Message));

        Assert.IsNull(await search.SearchAsync(CancellationToken.None));
        Assert.HasCount(Wikiquote.PagesPerDay, test.Reader.Requests);

        test.Time.Advance(TimeSpan.FromDays(1));
        test.Reader.Respond = (_, _) => Task.FromResult(WikiquoteApi.Page(WikiquoteApi.Quote("Le bon sens est la chose du monde la mieux partagée.", "René Descartes")));
        Assert.AreEqual("René Descartes", (await search.SearchAsync(CancellationToken.None))!.Content.Author);
        Assert.HasCount(Wikiquote.PagesPerDay + 1, test.Reader.Requests);
    }

    [TestMethod]
    public async Task After_a_failure_the_search_waits_an_hour()
    {
        using var test = new CitationsTest();
        test.Activate();
        using var services = test.Services(out _);
        var search = services.GetRequiredService<OnlineQuoteSearch>();
        test.Reader.Respond = (_, _) => throw new WikiquoteException("fr.wikiquote.org injoignable : délai dépassé.");

        Assert.IsNull(await search.SearchAsync(CancellationToken.None));
        Assert.HasCount(1, test.Reader.Requests);
        Assert.Contains("Citation du jour en ligne impossible : fr.wikiquote.org injoignable : délai dépassé.", test.Test.Logs.Warnings);

        test.Time.Advance(TimeSpan.FromMinutes(59));
        Assert.IsNull(await search.SearchAsync(CancellationToken.None));
        Assert.HasCount(1, test.Reader.Requests);

        test.Time.Advance(TimeSpan.FromMinutes(1));
        test.Reader.Respond = (_, _) => Task.FromResult(WikiquoteApi.Page(WikiquoteApi.Quote("Rien ne se perd, rien ne se crée, tout se transforme.", "Antoine Lavoisier")));
        Assert.IsNotNull(await search.SearchAsync(CancellationToken.None));
        Assert.HasCount(2, test.Reader.Requests);
    }

    [TestMethod]
    [DataRow("{ pas du JSON", "réponse de Wikiquote illisible : ")]
    [DataRow("{\"query\":{\"pages\":[{\"revisions\":[{\"slots\":{\"main\":{\"content\":\"\\ud800\"}}}]}]}}", "réponse de Wikiquote illisible : ")]
    [DataRow("{\"query\":{\"pages\":[]}}", "page « ")]
    [DataRow("{\"query\":{\"pages\":[{\"revisions\":[{\"slots\":{\"main\":{\"content\":12}}}]}]}}", "page « ")]
    [DataRow("[]", "page « ")]
    public async Task An_unusable_answer_is_a_failure(string answer, string reason)
    {
        using var test = new CitationsTest();
        test.Activate();
        using var services = test.Services(out _);
        test.Reader.Respond = (_, _) => Task.FromResult(answer);

        Assert.IsNull(await services.GetRequiredService<OnlineQuoteSearch>().SearchAsync(CancellationToken.None));

        Assert.StartsWith("Citation du jour en ligne impossible : " + reason, test.Test.Logs.Warnings.Single());
    }

    [TestMethod]
    public async Task A_missing_page_is_named_in_the_failure()
    {
        using var test = new CitationsTest();
        test.Activate();
        using var services = test.Services(out _);
        test.Reader.Respond = (_, _) => Task.FromResult(WikiquoteApi.Missing("Sagesse"));

        Assert.IsNull(await services.GetRequiredService<OnlineQuoteSearch>().SearchAsync(CancellationToken.None));

        Assert.MatchesRegex("^Citation du jour en ligne impossible : page « [^»]+ » introuvable sur Wikiquote$", test.Test.Logs.Warnings.Single());
    }

    [TestMethod]
    public async Task One_search_at_a_time_and_a_vault_closed_meanwhile_is_a_failure()
    {
        using var test = new CitationsTest();
        test.Activate();
        using var services = test.Services(out _);
        var search = services.GetRequiredService<OnlineQuoteSearch>();
        var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        test.Reader.Respond = (_, _) => answer.Task;

        var first = search.SearchAsync(CancellationToken.None);
        Assert.IsNull(await search.SearchAsync(CancellationToken.None));
        Assert.HasCount(1, test.Reader.Requests);

        test.Test.App.Host.Dispose();
        answer.SetResult(WikiquoteApi.Page(WikiquoteApi.Quote("Une citation qui arrive trop tard.")));
        Assert.IsNull(await first);
        Assert.AreEqual("Citation du jour en ligne impossible : " + VaultHost.NoVaultOpen, test.Test.Logs.Warnings.Single());
    }

    [TestMethod]
    public async Task A_cancelled_search_can_start_again()
    {
        using var test = new CitationsTest();
        test.Activate();
        using var services = test.Services(out _);
        var search = services.GetRequiredService<OnlineQuoteSearch>();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        test.Reader.Respond = (_, token) => Task.FromCanceled<string>(token);

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => search.SearchAsync(cancellation.Token));

        test.Reader.Respond = (_, _) => Task.FromResult(WikiquoteApi.Page(WikiquoteApi.Quote("Une citation après l'annulation.")));
        Assert.IsNotNull(await search.SearchAsync(CancellationToken.None));
    }

    [TestMethod]
    public void The_wikitext_is_found_like_a_json_pointer()
    {
        Assert.AreEqual("objet", OnlineQuoteSearch.Wikitext("""{"query":{"pages":{"0":{"revisions":{"0":{"slots":{"main":{"content":"objet"}}}}}}}}""", "P"));
        Assert.AreEqual("second", OnlineQuoteSearch.Wikitext("""{"query":{"pages":[{"revisions":[{"slots":{"main":{"content":"premier","content":"second"}}}]}]}}""", "P"));
        Assert.AreEqual("page « P » introuvable sur Wikiquote", Assert.ThrowsExactly<WikiquoteException>(() => OnlineQuoteSearch.Wikitext("""{"query":{"pages":{"1":{}}}}""", "P")).Message);
    }

    [TestMethod]
    public void The_page_address_carries_only_the_page_title()
    {
        var address = OnlineQuoteSearch.PageAddress("Liberté");

        Assert.AreEqual(
            "https://fr.wikiquote.org/w/api.php?action=query&prop=revisions&rvprop=content&rvslots=main&titles=Libert%C3%A9&format=json&formatversion=2",
            address.AbsoluteUri);
    }

    [TestMethod]
    public void The_failure_exception_keeps_its_message()
    {
        Assert.IsFalse(string.IsNullOrEmpty(new WikiquoteException().Message));
        var cause = new InvalidOperationException("cause");
        Assert.AreSame(cause, new WikiquoteException("échec", cause).InnerException);
    }
}
