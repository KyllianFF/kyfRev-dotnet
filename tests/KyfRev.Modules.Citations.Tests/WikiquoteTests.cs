using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.Application.Model;

namespace KyfRev.Modules.Citations.Tests;

/// <summary>
/// Pages du jour, domaines et citations extraites d'un wikitexte, comparés à la version 0.x : les
/// vecteurs de <c>fixtures/wikiquote</c> viennent du crate <c>coffre</c> (voir
/// <c>tools/reference-vectors --wikiquote</c>).
/// </summary>
[TestClass]
public sealed class WikiquoteTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "fixtures", "wikiquote");

    [TestMethod]
    public void Pages_of_the_day_match_version_0x()
    {
        var lines = File.ReadAllLines(Path.Combine(Folder, "pages.txt"));
        Assert.HasCount(6080, lines);
        foreach (var line in lines)
        {
            var fields = line.Split('\t');
            var day = DateOnly.ParseExact(fields[1], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var pages = Wikiquote.PagesOfTheDay(fields[0], day, JsonSerializer.Deserialize<string[]>(fields[2])!);
            Assert.AreEqual(fields[3], string.Join(',', pages.Select(page => $"{page.Domain}:{page.Title}")), line);
        }
    }

    [TestMethod]
    public void Valid_domains_match_version_0x()
    {
        var lines = File.ReadAllLines(Path.Combine(Folder, "domaines.txt"));
        Assert.HasCount(400, lines);
        foreach (var line in lines)
        {
            var fields = line.Split('\t');
            Assert.AreEqual(fields[1], string.Join(',', Wikiquote.ValidDomains(JsonSerializer.Deserialize<string[]>(fields[0])!)), line);
        }
    }

    [TestMethod]
    public void Quotes_of_a_page_match_version_0x()
    {
        var cases = JsonNode.Parse(File.ReadAllText(Path.Combine(Folder, "citations.json")))!.AsArray();
        Assert.HasCount(806, cases);
        var found = 0;
        foreach (var (item, n) in cases.Select((item, n) => (item!, n)))
        {
            var page = new WikiquotePage(item["domaine"]!.GetValue<string>(), item["titre"]!.GetValue<string>());
            var wikitext = item["wikitexte"]!.GetValue<string>();
            var quotes = Wikiquote.QuotesOfPage(wikitext, page);
            var actual = new JsonArray([.. quotes.Select(Serde)]);
            Assert.IsTrue(JsonNode.DeepEquals(item["citations"], actual), $"cas {n} :\n{wikitext}\nattendu {item["citations"]!.ToJsonString()}\nobtenu {actual.ToJsonString()}");
            found += quotes.Count;
        }
        Assert.AreEqual(284, found);
    }

    [TestMethod]
    public void A_character_across_the_entity_window_is_never_cut()
    {
        // Avant son correctif, la 0.x coupait la fenêtre de 12 octets au milieu du « é » et
        // paniquait : la recherche du jour restait bloquée. Le « & » sans « ; » proche reste tel quel.
        var page = new WikiquotePage("philosophie", "Sagesse");
        var quotes = Wikiquote.QuotesOfPage("{{citation|Laurel & ses amis été là, tous ensemble.}}\n{{Réf Livre|titre=Souvenirs|auteur=Quelqu'un|année=1950}}\n", page);

        var quote = quotes.Single();
        Assert.AreEqual("Laurel & ses amis été là, tous ensemble.", quote.Text);
        Assert.AreEqual("Quelqu'un", quote.Author);
        Assert.AreEqual("Souvenirs", quote.Source.Title);
        Assert.AreEqual(SourceKind.Book, quote.Source.Kind);
        Assert.AreEqual("https://fr.wikiquote.org/wiki/Sagesse", quote.Source.Address);
    }

    [TestMethod]
    public void Page_address_and_domains()
    {
        Assert.AreEqual("https://fr.wikiquote.org/wiki/Bon_sens", new WikiquotePage("philosophie", "Bon sens").Address);
        Assert.HasCount(4, Wikiquote.Domains);
        Assert.IsEmpty(Wikiquote.PagesOfTheDay("c_1", new DateOnly(2026, 10, 1), ["cuisine"]));
    }

    /// <summary><c>ContenuCitation</c> tel que serde l'écrit (champs en camelCase, genre en snake_case).</summary>
    private static JsonObject Serde(QuoteContent quote) => new()
    {
        ["auteur"] = quote.Author,
        ["contexte"] = quote.Context,
        ["date"] = quote.Date,
        ["etiquettes"] = new JsonArray([.. quote.Tags.Select(tag => (JsonNode?)tag)]),
        ["source"] = new JsonObject
        {
            ["genre"] = quote.Source.Kind switch
            {
                null => null,
                SourceKind.Book => "livre",
                SourceKind.Newspaper => "journal",
                SourceKind.Article => "article",
                SourceKind.Speech => "discours",
                SourceKind.Website => "site",
                SourceKind.Film => "film",
                SourceKind.Song => "chanson",
                SourceKind.Broadcast => "emission",
                SourceKind.Other => "autre",
                _ => "inconnue",
            },
            ["page"] = quote.Source.Page,
            ["titre"] = quote.Source.Title,
            ["url"] = quote.Source.Address,
        },
        ["texte"] = quote.Text,
    };
}
