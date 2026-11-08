using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.Application.Tests.Support;

namespace KyfRev.Application.Tests;

/// <summary>La recherche dans les fiches et la recherche globale, sur un vrai coffre.</summary>
[TestClass]
public sealed class SearchUseCasesTests
{
    private const string Secret = "cherchéeInavouable";

    private static readonly Anchor Passage = new("La prophase", string.Empty, " précède", 0, 11, ["Mitose"], 5, "sha256:aa");

    private static readonly CancellationToken None = CancellationToken.None;

    [TestMethod]
    public void The_sheets_are_searched_with_their_excerpt()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        var id = Sheet(test, "Biologie", "Mitose", "La prophase précède la métaphase.");

        var hit = test.App.Sheets.Search("METAPHASE", None).Value.Single();

        Assert.AreEqual(id, hit.Id);
        Assert.AreEqual("Biologie", hit.Folder);
        Assert.AreEqual("La prophase précède la |métaphase|.", string.Join('|', hit.Excerpt.Select(piece => piece.Text)));
        Assert.IsTrue(hit.Excerpt[1].Found);
    }

    [TestMethod]
    public void Sheets_found_by_their_title_come_first_then_by_title()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        var byText = Sheet(test, string.Empty, "Alpha", "Une mitose.");
        var second = test.App.Sheets.Create(string.Empty, "Zêta mitose", string.Empty).Value;
        var first = test.App.Sheets.Create("Bio/Cellule", "Mitose", string.Empty).Value;

        var hits = Everywhere(test, "mitose").Value;

        CollectionAssert.AreEqual(new[] { first, second, byText }, hits.Select(hit => hit.Id).ToList());
        Assert.AreEqual(SearchKinds.Sheet, hits[0].Kind);
        Assert.AreEqual("Mitose", hits[0].Title);
        Assert.AreEqual("Bio/Cellule", hits[0].Detail);
        Assert.IsFalse(hits[0].IsArchived);
        Assert.IsNull(hits[0].SheetId);
        Assert.IsNull(hits[0].Text);
        Assert.IsTrue(hits[0].Excerpt.IsEmpty);
        Assert.IsFalse(hits[2].Excerpt.IsEmpty, "trouvée par son texte");
    }

    [TestMethod]
    public void An_archived_sheet_is_found_and_says_so()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        var id = test.App.Sheets.Create(string.Empty, "Mitose", string.Empty).Value;
        Assert.IsTrue(test.App.Reviews.Archive(id).Succeeded);

        Assert.IsTrue(Everywhere(test, "mitose").Value.Single().IsArchived);
    }

    [TestMethod]
    public void Passages_and_definitions_come_after_the_sheets_and_name_their_sheet()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        var sheet = test.App.Sheets.Create(string.Empty, "Mitose", string.Empty).Value;
        var passage = test.App.Annotations.Create(sheet, Passage, "clergé en tête").Value;
        var definition = test.App.Annotations.CreateDefinition(sheet, Passage, new Definition("Prophase", "Une phase du clergé.")).Value;

        var hits = Everywhere(test, "clerge").Value;

        Assert.HasCount(2, hits);
        Assert.AreEqual(SearchKinds.Passage, hits[0].Kind);
        Assert.AreEqual(passage, hits[0].Id);
        Assert.AreEqual("La prophase", hits[0].Title, "un passage se montre par son texte");
        Assert.AreEqual(sheet, hits[0].SheetId);
        Assert.AreEqual("Mitose", hits[0].Detail);
        Assert.AreEqual(SearchKinds.Definition, hits[1].Kind);
        Assert.AreEqual(definition, hits[1].Id);
        Assert.AreEqual("Prophase", hits[1].Title, "une définition, par son terme");
    }

    [TestMethod]
    public void What_the_modules_bring_comes_last_and_only_if_they_answer()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        test.App.Sheets.Create(string.Empty, "Mitose", string.Empty);
        var module = new FakeProvider(Outcome.Of<IReadOnlyList<SearchHit>>([new SearchHit(SearchKinds.Quote, "citations/x.md", "Pascal", [])]));

        var hits = test.App.Search.Everywhere("mitose", [module], None).Value;

        CollectionAssert.AreEqual(new[] { SearchKinds.Sheet, SearchKinds.Quote }, hits.Select(hit => hit.Kind).ToList());
        Assert.AreEqual("mitose", module.Asked);

        var refusal = test.App.Search.Everywhere("mitose", [new FakeProvider(Outcome.Refused<IReadOnlyList<SearchHit>>("Illisible."))], None);
        Assert.AreEqual("Illisible.", refusal.Error);
    }

    [TestMethod]
    public void Without_a_word_nothing_comes_back()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        test.App.Sheets.Create(string.Empty, "Mitose", string.Empty);

        Assert.IsEmpty(Everywhere(test, "   ").Value);
    }

    [TestMethod]
    public void A_cancelled_search_stops_and_gives_what_it_has()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        test.App.Sheets.Create(string.Empty, "Mitose", string.Empty);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.IsEmpty(test.App.Search.Everywhere("mitose", [], cancellation.Token).Value);
        Assert.IsEmpty(test.App.Sheets.Search("mitose", cancellation.Token).Value);
    }

    [TestMethod]
    public void The_modules_are_required() =>
        Assert.ThrowsExactly<ArgumentNullException>(() =>
        {
            using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0));
            test.App.Search.Everywhere("x", null!, None);
        });

    [TestMethod]
    public void What_is_searched_is_never_logged()
    {
        using var folder = new TestFolder();
        using var test = Opened(folder);
        Sheet(test, string.Empty, "Mitose", Secret);

        Assert.IsNotEmpty(test.App.Sheets.Search(Secret, None).Value);
        Assert.IsNotEmpty(Everywhere(test, Secret).Value);

        Assert.IsEmpty(test.Logs.Entries.Where(entry => entry.Message.Contains(Secret, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Une fiche écrite ; rend son identifiant.</summary>
    private static string Sheet(TestApplication test, string folder, string title, string body)
    {
        var id = test.App.Sheets.Create(folder, title, null).Value;
        Assert.IsTrue(test.App.Editing.Save(id, body, test.App.Sheets.Open(id).Value.Fingerprint, 1).Succeeded);
        return id;
    }

    private static Outcome<IReadOnlyList<SearchHit>> Everywhere(TestApplication test, string query) => test.App.Search.Everywhere(query, [], None);

    private static TestApplication Opened(TestFolder folder)
    {
        var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0));
        Assert.IsNull(test.App.Vaults.Create(folder.Vault).OpenError);
        return test;
    }

    /// <summary>Un module d'essai : il répond ce qu'on lui a dit, et note ce qu'on lui a demandé.</summary>
    private sealed class FakeProvider(Outcome<IReadOnlyList<SearchHit>> answer) : ISearchProvider
    {
        public string? Asked { get; private set; }

        public Outcome<IReadOnlyList<SearchHit>> Search(string query, CancellationToken cancellationToken)
        {
            Asked = query;
            return answer;
        }
    }
}
