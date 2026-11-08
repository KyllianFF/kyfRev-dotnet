using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.Application.UseCases;
using KyfRev.Modules.Citations.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.Modules.Citations.Tests;

/// <summary>
/// Le module Citations assemblé comme le fera l'application : chargé seulement si le coffre
/// l'active, il apporte alors ses cas d'usage et ses points d'extension ; le cœur n'en dépend pas.
/// </summary>
[TestClass]
public sealed class CitationsModuleTests
{
    [TestMethod]
    public void A_disabled_module_is_not_loaded_and_the_core_still_works()
    {
        using var test = new CitationsTest();
        Assert.IsFalse(test.Test.App.Settings.Get().Value.QuotesActive);

        using var services = test.Services(out var loaded);

        Assert.IsEmpty(loaded);
        Assert.IsNull(services.GetService<QuoteUseCases>());
        Assert.IsNull(services.GetService<OnlineQuoteSearch>());
        Assert.IsEmpty(services.GetServices<INavigationContributor>());
        Assert.IsEmpty(services.GetServices<IDashboardWidget>());
        Assert.IsEmpty(services.GetServices<IVaultHealthCheck>());
        Assert.IsEmpty(services.GetServices<ISearchProvider>(), "ses citations ne sont pas cherchées");
        var id = test.Test.App.Sheets.Create(string.Empty, "Mitose", string.Empty);
        Assert.IsTrue(id.Succeeded);
        Assert.AreEqual("fiches/Mitose.md", test.Test.App.Sheets.Open(id.Value).Value.Path);
        Assert.IsEmpty(test.Reader.Requests);
    }

    [TestMethod]
    public void An_active_module_brings_its_use_cases_and_extension_points()
    {
        using var test = new CitationsTest();
        test.Activate();

        using var services = test.Services(out var loaded);

        Assert.AreEqual(CitationsModule.ModuleId, loaded.Single().Id);
        Assert.AreEqual("Citations", loaded.Single().Name);
        Assert.AreEqual(new NavigationEntry("citations", "Citations", 60), services.GetServices<INavigationContributor>().Single().Entry);
        var widget = services.GetServices<IDashboardWidget>().Single();
        Assert.AreEqual("citation-du-jour", widget.Id);
        Assert.AreEqual(10, widget.Order);
        Assert.IsNull(widget.Card());
        Assert.IsEmpty(services.GetServices<IVaultHealthCheck>().Single().Notices());
        Assert.AreSame(services.GetRequiredService<QuoteUseCases>(), services.GetRequiredService<QuoteUseCases>());
    }

    [TestMethod]
    public void The_dashboard_card_credits_the_quote_of_the_day()
    {
        using var test = new CitationsTest();
        test.Activate();
        using var services = test.Services(out _);
        var quotes = services.GetRequiredService<QuoteUseCases>();
        var widget = services.GetServices<IDashboardWidget>().Single();

        var path = quotes.Add(Content("Le cœur a ses raisons.", "Blaise Pascal", "1670", "Pensées")).Value.Path;
        Assert.AreEqual(new DashboardCard("Citation du jour", "Le cœur a ses raisons.", "Blaise Pascal, Pensées (1670)"), widget.Card());

        Assert.IsTrue(quotes.Modify(path, Content("Le cœur a ses raisons.", null, "1670", null)).Succeeded);
        Assert.AreEqual("1670", widget.Card()!.Detail);
        Assert.IsTrue(quotes.Modify(path, Content("Connais-toi toi-même.", "Socrate", "mai", null)).Succeeded);
        Assert.AreEqual("Socrate", widget.Card()!.Detail);
        Assert.IsTrue(quotes.Modify(path, Content("Anonyme.", null, null, null)).Succeeded);
        Assert.IsNull(widget.Card()!.Detail);

        test.Test.App.Host.Dispose();
        Assert.IsNull(widget.Card());
    }

    [TestMethod]
    public void The_global_search_finds_quotes_by_their_author_or_their_text()
    {
        using var test = new CitationsTest();
        test.Activate();
        using var services = test.Services(out _);
        var quotes = services.GetRequiredService<QuoteUseCases>();
        var search = services.GetServices<ISearchProvider>().Single();
        var path = quotes.Add(Content("Le cœur a ses raisons.", "Blaise Pascal", "1670", "Pensées")).Value.Path;
        quotes.Add(Content("Connais-toi toi-même.", null, null, null));

        // L'auteur et l'œuvre ont suffi : le texte se lit à la place de l'extrait.
        var byAuthor = search.Search("pascal pensees", CancellationToken.None).Value.Single();
        Assert.AreEqual(SearchKinds.Quote, byAuthor.Kind);
        Assert.AreEqual(path, byAuthor.Id);
        Assert.AreEqual("Blaise Pascal", byAuthor.Title);
        Assert.IsTrue(byAuthor.Excerpt.IsEmpty);
        Assert.AreEqual("Le cœur a ses raisons.", byAuthor.Text);

        // Trouvée par son texte : l'extrait montre les mots.
        var byText = search.Search("COEUR", CancellationToken.None).Value.Single();
        Assert.AreEqual("cœur", byText.Excerpt.Single(piece => piece.Found).Text);
        Assert.IsNull(byText.Text);

        // Sans auteur, le titre est vide : la vue dit « Auteur inconnu ».
        Assert.AreEqual(string.Empty, search.Search("connais", CancellationToken.None).Value.Single().Title);

        Assert.IsEmpty(search.Search("  ", CancellationToken.None).Value);
        test.Test.App.Host.Dispose();
        Assert.AreEqual(VaultHost.NoVaultOpen, search.Search("pascal", CancellationToken.None).Error);
    }

    [TestMethod]
    public void Unreadable_quote_files_are_reported_in_the_vault_health()
    {
        using var test = new CitationsTest();
        test.Activate();
        test.Write("citations/Illisible.md", "---\nauteur: [\n---\nTexte.\n");
        using var services = test.Services(out _);
        var check = services.GetServices<IVaultHealthCheck>().Single();

        var notice = check.Notices().Single();
        Assert.AreEqual("citations/Illisible.md", notice.Path);
        Assert.StartsWith("Citation illisible : ", notice.Message);

        test.Test.App.Host.Dispose();
        Assert.IsEmpty(check.Notices());
    }

    [TestMethod]
    public void Quote_use_cases_work_on_the_open_vault_and_are_refused_without_one()
    {
        using var test = new CitationsTest();
        test.Activate();
        using var services = test.Services(out _);
        var quotes = services.GetRequiredService<QuoteUseCases>();

        var added = quotes.Add(Content("  Rien ne se perd.  ", "Lavoisier", "1789", null)).Value;
        Assert.AreEqual("Rien ne se perd.", added.Content.Text);
        Assert.AreEqual(1789, added.Year);
        var trashed = quotes.Trash(added.Path).Value;
        Assert.StartsWith("citations/.supprimees/", trashed);
        Assert.AreEqual(trashed, quotes.Quotes().Value.Trash.Single().Path);
        Assert.IsFalse(quotes.Purge(added.Path).Succeeded, "seule une citation à la corbeille s'efface");
        Assert.AreEqual(added.Path, quotes.Restore(trashed).Value);
        Assert.AreEqual(added.Path, quotes.QuoteOfTheDay().Value!.Path);
        Assert.IsFalse(quotes.Add(Content("   ", null, null, null)).Succeeded);
        var erased = quotes.Trash(quotes.Add(Content("À effacer.", null, null, null)).Value.Path).Value;
        Assert.IsTrue(quotes.Purge(erased).Succeeded);
        Assert.IsEmpty(quotes.Quotes().Value.Trash);
        Assert.IsFalse(quotes.Purge(erased).Succeeded, "elle n'existe plus");

        test.Test.App.Host.Dispose();
        Assert.AreEqual(VaultHost.NoVaultOpen, quotes.Quotes().Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, quotes.QuoteOfTheDay().Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, quotes.Add(Content("x", null, null, null)).Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, quotes.Modify(added.Path, Content("x", null, null, null)).Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, quotes.Trash(added.Path).Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, quotes.Restore(added.Path).Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, quotes.Purge(added.Path).Error);
    }

    [TestMethod]
    public void The_module_follows_the_vault_preferences()
    {
        var module = new CitationsModule();
        Assert.ThrowsExactly<ArgumentNullException>(() => module.IsActiveFor(null!));
        using var test = new CitationsTest();
        var preferences = test.Test.App.Settings.Get().Value;
        Assert.IsFalse(module.IsActiveFor(preferences));
        Assert.IsTrue(module.IsActiveFor(preferences with { QuotesActive = true, QuotesOnline = false }));
    }

    private static QuoteContent Content(string text, string? author, string? date, string? title) =>
        new(text, author, date, title is null ? QuoteSource.None : new QuoteSource(SourceKind.Book, title, null, null), null, []);
}
